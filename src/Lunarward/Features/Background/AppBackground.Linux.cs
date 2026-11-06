// Lunarward：应用背景（壁纸）。
//
// 上游用 Win2D + MediaPlayer 播放视频壁纸、用 CanvasBitmap 做缩放；Linux 侧 M2 先做静态图：
//   - 图片：BitmapImage 直接加载（file:// 与 ms-appx://）
//   - 视频：暂时退化为该游戏的静态兜底图（视频解码要接 LibVLC/GStreamer，排在 M3）
// 选图逻辑照抄上游 UpdateBackgroundAsync 的语义：优先「建议背景」→ 失败/超时用兜底图。
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Starward.Core.HoYoPlay;
using Starward.Features.ViewHost;
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Background;


public sealed partial class AppBackground : UserControl, INotifyPropertyChanged
{
    private static readonly int _videoFrameIntervalMs = int.TryParse(Environment.GetEnvironmentVariable("LUNARWARD_VIDEO_FPS"), out int fps) && fps > 0 ? Math.Max(1000 / fps, 1) : 1000 / 30;

    private long _lastVideoInvalidate;

    private static readonly bool _skipVideoPaint = Environment.GetEnvironmentVariable("LUNARWARD_VIDEO_NOPAINT") is "1";


    private static AppBackground? _current;

    /// <summary>当前背景控件实例（上游同名语义）。</summary>
    public static AppBackground Current
    {
        get => _current ??= new AppBackground();
        private set => _current = value;
    }


    private readonly BackgroundService _backgroundService = AppConfig.GetService<BackgroundService>();

    private CancellationTokenSource? _updateBackgroundCts;

    private string? _lastBackgroundFile;


    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


    private readonly Image _image = new()
    {
        Stretch = Stretch.UniformToFill,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    // 视频帧走 SkiaSharp 画布：Uno/Skia 的 Image 对 WriteableBitmap 的像素更新不重绘（L-87），
    // 反复赋 Source、甚至双缓冲换实例都无效，实测两次截图差异 0.00%。
    private readonly SkiaSharp.Views.Windows.SKXamlCanvas _videoCanvas = new()
    {
        Opacity = 0.6,
        Visibility = Visibility.Collapsed,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    private SkiaSharp.SKBitmap? _videoSurface;

    private int _videoWidth;

    private int _videoHeight;


    public AppBackground()
    {
        Current = this;
        // 上游 AppBackground.xaml.cs 里注册了 BackgroundChangedMessage；Linux 侧之前漏了这条，
        // 结果「点背景切换器 → 发消息 → 没人消费」，表现就是切了没反应（L-70）。
        WeakReferenceMessenger.Default.Register<AppBackground, BackgroundChangedMessage>(this, (r, m) => r.OnBackgroundChangedMessage(m));
        // 上游在 OnMainWindowStateChanged 里对视频做 Pause/Play（隐藏、锁屏时停，激活时续）；
        // Linux 版之前没注册这条消息，视频永远在跑（L-70 同类遗漏）。
        WeakReferenceMessenger.Default.Register<AppBackground, MainWindowStateChangedMessage>(this, (r, m) => r.OnWindowStateChangedMessage(m));
        // 上游 AppBackground.xaml 用 Win2D 画视频帧、用 ImageEx 贴静态图；这两个在这里都不可用，
        // 所以不走 InitializeComponent，直接放一个 Image 承担「贴壁纸」这件事（视频壁纸见 M3）。
        //
        // 可读性：Windows 上壁纸会被系统亚克力模糊+压暗，这里没有模糊，只能靠「深色底 + 降低壁纸不透明度」近似，
        // 否则亮色壁纸会让上层文字完全看不清（实测默认兜底图就是一张明亮的过场 CG）。
        _image.Opacity = 0.42;
        _videoCanvas.PaintSurface += VideoCanvas_PaintSurface;
        Content = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x16, 0x16, 0x1A)),
            Children = { _image, _videoCanvas },
        };
    }


    private GameId? _currentGameId;

    public GameId? CurrentGameId
    {
        get => _currentGameId;
        set
        {
            if (_currentGameId == value)
            {
                return;
            }
            _currentGameId = value;
            Raise(nameof(CurrentGameId));
            _ = UpdateBackgroundAsync();
        }
    }


    private void OnBackgroundChangedMessage(BackgroundChangedMessage message)
    {
        try
        {
            _ = UpdateBackgroundAsync(message.GameBackground);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] apply selected background failed");
        }
    }


    /// <summary>
    /// 隐藏/锁屏时暂停视频壁纸，重新激活时继续（上游 AppBackground.xaml.cs:591-608 的同名逻辑）。
    /// </summary>
    private void OnWindowStateChangedMessage(MainWindowStateChangedMessage message)
    {
        try
        {
            if (_videoPlayer is null)
            {
                return;
            }
            if (message.Activate)
            {
                _videoPlayer.Resume();
            }
            else if (message.Hide || message.SessionLock)
            {
                _videoPlayer.Pause();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[background] toggle video playback failed");
        }
    }


    /// <summary>
    /// 载入静态背景：底图 + 主题图（透明底的标题文字层）合成一张图。
    /// 主题图下载失败/不存在时退回单张底图，不黑屏。
    /// </summary>
    private async Task<ImageSource?> LoadBackgroundImageAsync(string filePath, GameBackground? background, CancellationToken token)
    {
        string? themePath = null;
        // 自定义背景是用户自己的图，上游不给它叠主题图，这里保持一致。
        if (background is not null
            && background.Type is not GameBackground.BACKGROUND_TYPE_CUSTOM
            && background.Theme?.Url is { Length: > 0 } themeUrl)
        {
            try
            {
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(6));
                themePath = await _backgroundService.GetBackgroundFileAsync(themeUrl, timeout.Token);
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "[background] theme image unavailable, using plain background");
            }
        }
        if (token.IsCancellationRequested)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(themePath) || !File.Exists(themePath))
        {
            return ToBitmap(filePath);
        }
        try
        {
            byte[] png = await Task.Run(() => ComposeBackgroundPng(filePath, themePath), token);
            if (token.IsCancellationRequested)
            {
                return null;
            }
            var bitmap = new BitmapImage();
            using var stream = new MemoryStream(png);
            await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            return bitmap;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] compose background with theme failed, using plain background");
            return ToBitmap(filePath);
        }
    }


    /// <summary>SkiaSharp 合成：底图铺满 + 主题图按上游语义整幅拉伸叠加，输出 PNG。</summary>
    private static byte[] ComposeBackgroundPng(string backgroundPath, string themePath)
    {
        using var background = SkiaSharp.SKBitmap.Decode(backgroundPath);
        using var theme = SkiaSharp.SKBitmap.Decode(themePath);
        if (background is null || theme is null)
        {
            throw new InvalidOperationException("decode background/theme failed");
        }
        var info = new SkiaSharp.SKImageInfo(background.Width, background.Height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul);
        using var surface = SkiaSharp.SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SkiaSharp.SKColors.Black);
        canvas.DrawBitmap(background, new SkiaSharp.SKRect(0, 0, background.Width, background.Height));
        canvas.DrawBitmap(theme, new SkiaSharp.SKRect(0, 0, background.Width, background.Height));
        canvas.Flush();
        using var image = surface.Snapshot();
        using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }


    private ImageSource? _backgroundImageSource;

    public ImageSource? BackgroundImageSource
    {
        get => _backgroundImageSource;
        set
        {
            _backgroundImageSource = value;
            _image.Source = value;
            Raise(nameof(BackgroundImageSource));
        }
    }


    private ImageSource? _placehoderImageSource;

    public ImageSource? PlacehoderImageSource
    {
        get => _placehoderImageSource;
        set
        {
            _placehoderImageSource = value;
            Raise(nameof(PlacehoderImageSource));
        }
    }


    private bool _isUpdateBackgroundRunning;

    public bool IsUpdateBackgroundRunning
    {
        get => _isUpdateBackgroundRunning;
        set
        {
            _isUpdateBackgroundRunning = value;
            Raise(nameof(IsUpdateBackgroundRunning));
        }
    }


    public GameBackground? CurrentGameBackground { get; private set; }


    public string? DisplayArguments { get; set; }


    private static readonly Uri DefaultBackground = new("ms-appx:///Assets/Image/UI_CutScene_1130320101A.png");


    private bool _disposed;


    /// <param name="requested">
    /// 用户从切换器里显式选中的背景；为 null 时按「建议背景」逻辑走（启动/换游戏）。
    /// 上游同名方法只处理「建议背景」，切换器是通过 BackgroundChangedMessage 把选中的背景塞进来的。
    /// </param>
    public async Task UpdateBackgroundAsync(GameBackground? requested = null)
    {
        StopVideoBackground();
        GameId? gameId = CurrentGameId;
        if (gameId is null)
        {
            BackgroundImageSource = new BitmapImage(DefaultBackground);
            return;
        }
        try
        {
            IsUpdateBackgroundRunning = true;
            _updateBackgroundCts?.Cancel();
            _updateBackgroundCts = new CancellationTokenSource();
            CancellationToken token = _updateBackgroundCts.Token;

            // 调试开关（M3 验证用）：直接指定本地视频，跳过 API 类型判断
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("LUNARWARD_VIDEO_BG"))
                && await TryStartVideoBackgroundAsync(null, token))
            {
                return;
            }

            string? filePath = null;
            GameBackground? background = null;
            try
            {
                if (requested is null)
                {
                    // 上游给 1 秒 API 超时 + 3 秒下载超时；这里只是拿「建议背景」，同样限时
                    // 上游给 1 秒：Windows 上够用，Linux 首包要 TLS 握手，1 秒几乎必然超时 → 永远回退静态兜底（L-71）。
                    int apiTimeoutMs = int.TryParse(Environment.GetEnvironmentVariable("LUNARWARD_BG_TIMEOUT_MS"), out int dbgMs) && dbgMs > 0 ? dbgMs : 6000;
                    using CancellationTokenSource apiTimeout = new(TimeSpan.FromMilliseconds(apiTimeoutMs));
                    background = await _backgroundService.GetSuggestedGameBackgroundAsync(gameId, apiTimeout.Token);
                    Serilog.Log.Debug("[background] suggested: {Type} stop={Stop} video={Video}", background?.Type, background?.StopVideo, background?.Video?.Url);
                }
                else
                {
                    background = requested;
                }
                if (background is not null)
                {
                    if (background.Type is GameBackground.BACKGROUND_TYPE_VIDEO && !background.StopVideo)
                    {
                        // 视频壁纸：帧由 LibVLCSharp 取（poc/video-spike 验证过），
                        // 主题图（海报，带游戏标题文字）按上游语义整幅拉伸画在帧上。
                        if (await TryStartVideoBackgroundAsync(background, token))
                        {
                            // 视频在播时先把「暂停后要看到的那张静态图（海报+主题文字层）」在后台准备好：
                            // 否则点暂停要先等海报下载（≤3s）+ 主题图下载（≤6s）+ Skia 合成，好几秒才出图（真机复现）。
                            _ = PreloadPausedBackgroundAsync(background, token);
                            // 视频壁纸同样要按海报取强调色（上游只在静态分支取色；D50 统一成取海报）。
                            _ = ApplyAccentFromPosterAsync(background, token);
                            return;
                        }
                        // 没有 libvlc / 拿不到视频文件时退回静态兜底图，不黑屏
                        filePath = BackgroundService.GetFallbackBackgroundImage(gameId);
                    }
                    else if (background.Type is GameBackground.BACKGROUND_TYPE_CUSTOM)
                    {
                        filePath = background.Background.Url;
                    }
                    else
                    {
                        using CancellationTokenSource downloadTimeout = new(TimeSpan.FromSeconds(3));
                        filePath = await _backgroundService.GetBackgroundFileAsync(background.Background.Url, downloadTimeout.Token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Serilog.Log.Information("[background] get suggested background timed out, using fallback image");
                filePath = BackgroundService.GetFallbackBackgroundImage(gameId);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[background] get suggested background failed");
                filePath = BackgroundService.GetFallbackBackgroundImage(gameId);
            }
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (string.IsNullOrWhiteSpace(filePath))
            {
                filePath = BackgroundService.GetFallbackBackgroundImage(gameId);
            }
            if (filePath == _lastBackgroundFile)
            {
                return;
            }
            _lastBackgroundFile = filePath;
            CurrentGameBackground = background;
            // 上游静态背景也会把 Theme 图（透明底的标题文字层）叠在底图上（AppBackground.xaml.cs:419-437）；
            // 之前只贴底图，所以暂停/静态时左上角没有标题文字（实测主题图 99.1% 像素透明，是纯文字层）。
            BackgroundImageSource = await LoadBackgroundImageAsync(filePath, background, token);
            // 上游在静态背景分支调用 AccentColorHelper.ChangeAppAccentColor（AppBackground.xaml.cs:551），
            // Linux 侧从未实现过（D50）：这里按图取主色写回主题字典。
            Starward.Helpers.AccentColorHelper.ApplyFromFile(filePath);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] update failed");
        }
        finally
        {
            IsUpdateBackgroundRunning = false;
        }
    }


    private VideoBackgroundPlayer? _videoPlayer;

    private string? _currentVideoPath;

    // 旧的 WriteableBitmap 双缓冲方案已废弃（Uno/Skia 不重绘，L-87），保留字段清理见 StopVideoBackground。






    /// <summary>
    /// 启动视频壁纸：下载/定位视频文件与主题图，起播放器，把合成帧写进 WriteableBitmap。
    /// 返回 false 表示「这条视频用不了」，调用方退回静态图。
    /// </summary>
    private async Task<bool> TryStartVideoBackgroundAsync(GameBackground? background, CancellationToken token)
    {
        if (!VideoBackgroundPlayer.IsAvailable)
        {
            return false;
        }
        try
        {
            string? videoPath = Environment.GetEnvironmentVariable("LUNARWARD_VIDEO_BG");
            string? themePath = Environment.GetEnvironmentVariable("LUNARWARD_VIDEO_OVERLAY");
            if (!string.IsNullOrWhiteSpace(videoPath))
            {
                // 调试：直接指定本地视频与主题图，跳过 API 下载（M3 验证用）
                if (!File.Exists(videoPath))
                {
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(themePath) && !File.Exists(themePath))
                {
                    themePath = null;
                }
            }
            if (string.IsNullOrWhiteSpace(videoPath) && background?.Video is not null)
            {
                // L-70：视频文件在 Video.Url（mp4），Background.Url 只是静态海报。
                // 之前拿 Background.Url 当视频喂给 libvlc，必然失败 → 永远退回静态图。
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(6));
                videoPath = await _backgroundService.GetBackgroundFileAsync(background.Video.Url, timeout.Token);
            }
            try
            {
                if (string.IsNullOrWhiteSpace(themePath) && background?.Theme is not null)
                {
                    using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(6));
                    themePath = await _backgroundService.GetBackgroundFileAsync(background.Theme.Url, timeout.Token);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[background] theme image unavailable, video plays without poster text");
            }
            if (token.IsCancellationRequested || string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            {
                return false;
            }

            if (_videoPlayer is not null && string.Equals(_currentVideoPath, videoPath, StringComparison.Ordinal))
            {
                // 同一条视频已经在播：重复调用（换游戏/刷新）不该重启播放器，否则会闪一帧黑。
                return true;
            }
            StopVideoBackground();
            _videoPlayer = new VideoBackgroundPlayer();
            _videoPlayer.SetOverlay(themePath);
            int width = _videoPlayer.Width;
            int height = _videoPlayer.Height;
            _videoPlayer.FrameReady += (pixels, w, h) =>
            {
                DispatcherQueue.TryEnqueue(() => PresentVideoFrame(pixels, w, h));
            };
            if (!_videoPlayer.Start(videoPath))
            {
                StopVideoBackground();
                return false;
            }
            _image.Visibility = Visibility.Collapsed;
            _videoCanvas.Visibility = Visibility.Visible;
            _currentVideoPath = videoPath;
            Serilog.Log.Information("[background] video background started: {path}", videoPath);
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] start video background failed");
            StopVideoBackground();
            return false;
        }
    }


    private void PresentVideoFrame(byte[] pixels, int width, int height)
    {
        try
        {
            if (_disposed || width <= 0 || height <= 0)
            {
                return;
            }
            if (_videoSurface is null || _videoWidth != width || _videoHeight != height)
            {
                _videoSurface?.Dispose();
                _videoSurface = new SkiaSharp.SKBitmap(new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Opaque));
                _videoWidth = width;
                _videoHeight = height;
            }
            nint target = _videoSurface.GetPixels();
            if (target == 0)
            {
                return;
            }
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, target, Math.Min(pixels.Length, _videoSurface.ByteCount));
            // 每次 Invalidate 都会触发一次整窗合成（不是只重画这块 Skia 画布），实测占 ~1 个核。
            // 按目标帧率节流，避免把 UI 线程压满导致整体「很卡」。
            if (!_skipVideoPaint)
            {
                long now = Environment.TickCount64;
                if (now - _lastVideoInvalidate >= _videoFrameIntervalMs)
                {
                    _lastVideoInvalidate = now;
                    _videoCanvas.Invalidate();
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] present video frame failed");
            StopVideoBackground();
        }
    }


    private void VideoCanvas_PaintSurface(object? sender, SkiaSharp.Views.Windows.SKPaintSurfaceEventArgs e)
    {
        SkiaSharp.SKCanvas canvas = e.Surface.Canvas;
        SkiaSharp.SKBitmap? bitmap = _videoSurface;
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return;
        }
        // UniformToFill：按画布比例裁切，保持与静态壁纸一致的观感
        float scale = Math.Max((float)e.Info.Width / bitmap.Width, (float)e.Info.Height / bitmap.Height);
        float drawWidth = bitmap.Width * scale;
        float drawHeight = bitmap.Height * scale;
        var destination = new SkiaSharp.SKRect((e.Info.Width - drawWidth) / 2f, (e.Info.Height - drawHeight) / 2f, (e.Info.Width + drawWidth) / 2f, (e.Info.Height + drawHeight) / 2f);
        // 帧是 1920x1080，画布约 1875x1056：几乎 1:1，但仍有缩放。
        // SkiaSharp 的 DrawBitmap 不带 paint 时用最近邻采样，缩放后就是「锯齿感很强」；
        // 这里显式给双线性 + 线性 mipmap。
        // High = 双三次，2MP/帧在 CPU 上很贵；Low = 双线性，1920→1875 这种 2% 缩放肉眼看不出差别。
        using var paint = new SkiaSharp.SKPaint
        {
            IsAntialias = false,
            FilterQuality = SkiaSharp.SKFilterQuality.Low,
        };
        canvas.DrawBitmap(bitmap, new SkiaSharp.SKRect(0, 0, bitmap.Width, bitmap.Height), destination, paint);
    }


    /// <summary>
    /// 预加载「暂停视频后要显示的那张静态图」：海报底图 + 主题文字层合成结果写进隐藏的 <see cref="_image"/>，
    /// 这样点暂停时 <see cref="StopVideoBackground"/> 一显示就是最终画面（D49）。
    /// </summary>
    /// <summary>视频壁纸时按海报图（Background.Url）取强调色。</summary>
    private async Task ApplyAccentFromPosterAsync(GameBackground background, CancellationToken token)
    {
        try
        {
            string? url = background.Background?.Url;
            if (string.IsNullOrWhiteSpace(url))
            {
                return;
            }
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(3));
            string file = await _backgroundService.GetBackgroundFileAsync(url, timeout.Token);
            if (!string.IsNullOrWhiteSpace(file))
            {
                Starward.Helpers.AccentColorHelper.ApplyFromFile(file);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[background] accent from poster failed");
        }
    }


    private async System.Threading.Tasks.Task PreloadPausedBackgroundAsync(GameBackground background, CancellationToken token)
    {
        try
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(3));
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeout.Token);
            string? filePath = await _backgroundService.GetBackgroundFileAsync(background.Background.Url, linked.Token);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return;
            }
            ImageSource source = await LoadBackgroundImageAsync(filePath, background, linked.Token);
            if (token.IsCancellationRequested)
            {
                return;
            }
            // 视频已经起来了才会走到这里；此时 _image 是 Collapsed，只更新内容不显示。
            _backgroundImageSource = source;
            _image.Source = source;
            _lastBackgroundFile = filePath;
            Serilog.Log.Information("[background] paused image preloaded: {file}", filePath);
        }
        catch (OperationCanceledException)
        {
            // 正常：切走了或超时
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[background] preload paused image failed");
        }
    }


    private void StopVideoBackground()
    {
        _videoPlayer?.Dispose();
        _videoPlayer = null;
        _currentVideoPath = null;
        _videoSurface?.Dispose();
        _videoSurface = null;
        _videoWidth = 0;
        _videoHeight = 0;
        _videoCanvas.Visibility = Visibility.Collapsed;
        _image.Visibility = Visibility.Visible;
        _image.Opacity = 0.42;
    }


    /// <summary>
    /// 把本地文件名/路径变成 BitmapImage。上游直接把字符串塞进 new Uri，
    /// 传进来的若是「裸文件名」就成了相对 URI，Linux 下必然解码失败、背景一片黑（L-74）。
    /// </summary>
    private static BitmapImage? ToBitmap(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        try
        {
            string full = Path.GetFullPath(path);
            if (!File.Exists(full))
            {
                Serilog.Log.Warning("[background] file not found: {file}", path);
                return null;
            }
            return new BitmapImage(new Uri(full));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] cannot load background {file}", path);
            return null;
        }
    }


    /// <summary>设置自定义背景文件（上游同签名；UI 里换壁纸用）。</summary>
    public async Task ChangeCustomBackgroundAsync(XamlRoot xamlRoot)
    {
        string? file = await _backgroundService.ChangeCustomBackgroundFileAsync(xamlRoot);
        if (string.IsNullOrWhiteSpace(file))
        {
            return;
        }
        _lastBackgroundFile = null;
        BitmapImage? image = ToBitmap(file);
        if (image is null)
        {
            // SkiaSharp 没有 AVIF/JXL 解码器，遇到这类文件时给一句人话，而不是「选了没反应」（D51）
            Serilog.Log.Warning("custom background decode failed: {file}", file);
            Helpers.InAppToast.MainWindow?.Warning("这张图片读不了：支持 png / jpg / webp / gif。", null, 8000);
            return;
        }
        BackgroundImageSource = image;
    }

}
