// Lunarward：应用背景（壁纸）。
//
// 上游用 Win2D + MediaPlayer 播放视频壁纸、用 CanvasBitmap 做缩放；Linux 侧 M2 先做静态图：
//   - 图片：BitmapImage 直接加载（file:// 与 ms-appx://）
//   - 视频：暂时退化为该游戏的静态兜底图（视频解码要接 LibVLC/GStreamer，排在 M3）
// 选图逻辑照抄上游 UpdateBackgroundAsync 的语义：优先「建议背景」→ 失败/超时用兜底图。
using System.Collections.Generic;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;
using Starward.Core.HoYoPlay;
using Starward.Features.ViewHost;
using System;
using System.Linq;
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

    // E：起播后 2 秒内一帧都没来就说明「播放器起来了但没解码」，这类问题以前只能靠 CPU 占用才能发现。
    private long _videoFrameCount;

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

    // 「建议背景」接口在 Linux 上常要 5–6 秒（首包 TLS + 弱网），而 GameLauncherPage 每次 Loaded 都会
    // 触发一次背景更新：不缓存的话每切一次页都要等一轮超时，并重放一次「回退图 + 换强调色」，
    // 用户看到的就是「切回启动器卡一下、主题闪一下」。失败结果同样缓存，避免连续重试。
    private static readonly TimeSpan SuggestedBackgroundTtl = TimeSpan.FromMinutes(5);

    // 「没拿到」只记 30 秒：一次超时不应该让视频壁纸 5 分钟内都起不来（F-21）。
    private static readonly TimeSpan SuggestedBackgroundFailureTtl = TimeSpan.FromSeconds(30);
    private readonly Dictionary<string, (GameBackground? Background, DateTime At)> _suggestedBackgroundCache = new();
    private string? _lastAccentFile;

    private string? _lastBackgroundFile;

    /// <summary>当前已经应用好背景的游戏；页面切来切去时不要重做（D62）。</summary>
    private GameId? _appliedGameId;

    /// <summary>视频壁纸正在播；此时 BackgroundImageSource 可能是 null，守卫要把它算作「已应用」。</summary>
    private bool _videoActive;


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
        // 不再压暗壁纸（用户反馈「背景有点暗」）：Windows 上壁纸被系统亚克力模糊+压暗，
        // 这里没有模糊，之前用 0.42 的不透明度近似，观感明显偏暗。改为原样显示，
        // 文字可读性交给上层面板自身的半透明背景与四角渐变（D54）。
        _image.Opacity = 1.0;
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


    /// <summary>
    /// 取「建议背景」，成功与失败都缓存 <see cref="SuggestedBackgroundTtl"/>：
    /// 失败时返回 null 由调用方走静态兜底图，但不再每次切页都重新等一轮超时。
    /// </summary>
    private async Task<GameBackground?> GetSuggestedBackgroundCachedAsync(GameId gameId, CancellationToken token)
    {
        string cacheKey = gameId.Id ?? string.Empty;
        if (_suggestedBackgroundCache.TryGetValue(cacheKey, out (GameBackground? Background, DateTime At) cached))
        {
            // 拿到过背景：正常缓存 5 分钟；没拿到（超时/异常）：只缓存 30 秒，别让一次网络抖动影响好几分钟。
            TimeSpan ttl = cached.Background is null ? SuggestedBackgroundFailureTtl : SuggestedBackgroundTtl;
            if (DateTime.UtcNow - cached.At < ttl)
            {
                return cached.Background;
            }
        }
        // 上游给 1 秒 API 超时；Linux 首包要 TLS 握手，1 秒几乎必然超时（L-71），这里放宽到 6 秒。
        int apiTimeoutMs = int.TryParse(Environment.GetEnvironmentVariable("LUNARWARD_BG_TIMEOUT_MS"), out int dbgMs) && dbgMs > 0 ? dbgMs : 4000;
        try
        {
            // 超时往往只是首包 TLS 慢（L-71）：按 1x / 2x / 4x 退避重试，仍失败才算「拿不到」。
            int[] attempts = { apiTimeoutMs, apiTimeoutMs * 2 };
            for (int i = 0; i < attempts.Length; i++)
            {
                try
                {
                    using CancellationTokenSource apiTimeout = new(TimeSpan.FromMilliseconds(attempts[i]));
                    using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, apiTimeout.Token);
                    GameBackground? suggested = await _backgroundService.GetSuggestedGameBackgroundAsync(gameId, linked.Token);
                    _suggestedBackgroundCache[cacheKey] = (suggested, DateTime.UtcNow);
                    Serilog.Log.Debug("[background] suggested: {Type} stop={Stop} video={Video}", suggested?.Type, suggested?.StopVideo, suggested?.Video?.Url);
                    return suggested;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    if (i == attempts.Length - 1)
                    {
                        throw;
                    }
                    Serilog.Log.Debug("[background] suggested background timed out after {Ms} ms, retrying", attempts[i]);
                }
            }
            return null;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            _suggestedBackgroundCache[cacheKey] = (null, DateTime.UtcNow);
            Serilog.Log.Information("[background] get suggested background timed out (retried once); retry in {Seconds} s", SuggestedBackgroundFailureTtl.TotalSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _suggestedBackgroundCache[cacheKey] = (null, DateTime.UtcNow);
            Serilog.Log.Warning(ex, "[background] get suggested background failed");
            return null;
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
        await UpdateBackgroundCoreAsync(requested);
    }


    private async Task UpdateBackgroundCoreAsync(GameBackground? requested)
    {
        GameId? gameId = CurrentGameId;
        // 没换游戏、用户也没在切换器里点新背景：当前这张就是对的，别把视频停掉再重下一遍。
        if (requested is null && gameId is not null && gameId == _appliedGameId && (BackgroundImageSource is not null || _videoActive))
        {
            Serilog.Log.Debug("[background] keep current background for {game}", gameId.Id);
            return;
        }
        StopVideoBackground();
        // 先用本地已有的背景/兜底图把画面填上（纯磁盘 IO，几百毫秒），网络那一段慢也看不见空白。
        if (BackgroundImageSource is null)
        {
            try
            {
                string quickPath = BackgroundService.GetCachedBackgroundFile(gameId) ?? BackgroundService.GetFallbackBackgroundImage(gameId);
                BackgroundImageSource = await LoadBackgroundImageAsync(quickPath, null, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[background] quick background failed");
            }
        }
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
                // 调试入口也要记状态：否则守卫失效，每次进页面都会重启播放器（日志里反复 video background started）。
                _appliedGameId = gameId;
                _videoActive = true;
                return;
            }

            string? filePath = null;
            GameBackground? background = null;
            try
            {
                if (requested is null)
                {
                    background = await GetSuggestedBackgroundCachedAsync(gameId, token);
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
                            _appliedGameId = gameId;
                            _videoActive = true;
                            // 视频在播时先把「暂停后要看到的那张静态图（海报+主题文字层）」在后台准备好：
                            // 否则点暂停要先等海报下载（≤3s）+ 主题图下载（≤6s）+ Skia 合成，好几秒才出图（真机复现）。
                            _ = PreloadPausedBackgroundAsync(background, token);
                            // 视频壁纸同样要按海报取强调色（上游只在静态分支取色；D50 统一成取海报）。
                            _ = ApplyAccentFromPosterAsync(background, token);
                            return;
                        }
                        // 没有 libvlc / 拿不到视频文件时：已有画面就保持，什么都没有才退回兜底图（不黑屏）
                        if (BackgroundImageSource is not null || _videoActive)
                        {
                            Serilog.Log.Information("[background] video unavailable, keeping current picture");
                            return;
                        }
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
                // 拿不到建议背景 ≠ 现在没有背景：只要画面上已经有东西（尤其视频正在播），就保持现状。
                if (BackgroundImageSource is not null || _videoActive)
                {
                    Serilog.Log.Information("[background] suggested background timed out, keeping current picture");
                    return;
                }
                Serilog.Log.Information("[background] get suggested background timed out, using fallback image");
                filePath = BackgroundService.GetFallbackBackgroundImage(gameId);
            }
            catch (Exception ex)
            {
                if (BackgroundImageSource is not null || _videoActive)
                {
                    Serilog.Log.Warning(ex, "[background] get suggested background failed, keeping current picture");
                    return;
                }
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
                // 图没变（预加载海报时也会写 _lastBackgroundFile），但游戏维度已经应用过，记下来供守卫早退。
                _appliedGameId = gameId;
                return;
            }
            _lastBackgroundFile = filePath;
            _appliedGameId = gameId;
            CurrentGameBackground = background;
            _ = RecordAppliedBackgroundAsync(background, filePath, token);
            // 上游静态背景也会把 Theme 图（透明底的标题文字层）叠在底图上（AppBackground.xaml.cs:419-437）；
            // 之前只贴底图，所以暂停/静态时左上角没有标题文字（实测主题图 99.1% 像素透明，是纯文字层）。
            BackgroundImageSource = await LoadBackgroundImageAsync(filePath, background, token);
            // 上游在静态背景分支调用 AccentColorHelper.ChangeAppAccentColor（AppBackground.xaml.cs:551），
            // Linux 侧从未实现过（D50）：这里按图取主色写回主题字典。
            // 同一张图只在第一次换色：ChangeAppAccentColor 会触发整棵树的主题重解析，重复调用会闪。
            if (filePath != _lastAccentFile)
            {
                _lastAccentFile = filePath;
                Starward.Helpers.AccentColorHelper.ApplyFromFile(filePath);
            }
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
            Serilog.Log.Warning("[background] video skipped: libvlc unavailable");
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
            if (token.IsCancellationRequested)
            {
                Serilog.Log.Debug("[background] video skipped: cancelled");
                return false;
            }
            if (string.IsNullOrWhiteSpace(videoPath))
            {
                Serilog.Log.Warning("[background] video skipped: no video file (download failed or url missing)");
                return false;
            }
            if (!File.Exists(videoPath))
            {
                Serilog.Log.Warning("[background] video skipped: file not found: {path}", videoPath);
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
            Interlocked.Exchange(ref _videoFrameCount, 0);
            Serilog.Log.Information("[background] video background started: {path}", videoPath);
            _ = WarnIfNoVideoFrameAsync(videoPath);
            _ = RecordAppliedBackgroundAsync(background, videoPath, token);
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
            Interlocked.Increment(ref _videoFrameCount);
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


    /// <summary>
    /// 与上游对齐：应用完背景后记「上次用的背景」和「当时的背景 id 列表」，
    /// 下次启动时 GetSuggestedGameBackgroundAsync 才能判断「有没有新背景、上次是静态还是视频」。
    /// 上游在 AppBackground.xaml.cs:249-251 做同样的事；Linux 侧一直都漏了（D69）。
    /// </summary>
    private async Task RecordAppliedBackgroundAsync(GameBackground? background, string? filePath, CancellationToken token)
    {
        try
        {
            if (background is null || filePath is null)
            {
                return;
            }
            if (background.Type is GameBackground.BACKGROUND_TYPE_CUSTOM)
            {
                return;
            }
            Starward.Core.GameBiz biz = CurrentGameId.GameBiz;
            AppConfig.SetBg(biz, Path.GetFileName(filePath));
            List<GameBackground> list = await _backgroundService.GetGameBackgroundsAsync(CurrentGameId, token);
            if (!token.IsCancellationRequested && list.Count > 0)
            {
                AppConfig.SetGameBackgroundIds(biz, string.Join(',', list.Select(x => x.Id)));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[background] record applied background failed");
        }
    }


    /// <summary>
    /// 起播后 5 秒还没有任何一帧 → 记 Warning（F-21 的教训：界面静止时只有 CPU 才能看出来）。
    ///
    /// 阈值从 2 秒放宽到 5 秒（2026-11-13 真机实测）：VP9 1080p 首帧要经过「VLC 缓冲 ~1.0 s
    /// （VLC 日志 `Stream buffering done (1033 ms)`）+ 解码 + 一次 8 MB 整帧拷贝」，
    /// 首帧通常落在 2.0–3.0 s 之间。2 秒阈值把正常起播也报成了故障，导致误判为回归。
    /// </summary>
    private async Task WarnIfNoVideoFrameAsync(string videoPath)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            if (_currentVideoPath != videoPath)
            {
                return;
            }
            if (Interlocked.Read(ref _videoFrameCount) == 0)
            {
                Serilog.Log.Warning("[background] video started but no frame in 5000 ms: {path}", videoPath);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[background] video frame watchdog failed");
        }
    }


    private void StopVideoBackground()
    {
        _videoPlayer?.Dispose();
        _videoPlayer = null;
        _currentVideoPath = null;
        _videoActive = false;
        _videoSurface?.Dispose();
        _videoSurface = null;
        _videoWidth = 0;
        _videoHeight = 0;
        _videoCanvas.Visibility = Visibility.Collapsed;
        _image.Visibility = Visibility.Visible;
        _image.Opacity = 1.0;
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
