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
using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Background;


public sealed partial class AppBackground : UserControl, INotifyPropertyChanged
{

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


    public AppBackground()
    {
        Current = this;
        // 上游 AppBackground.xaml.cs 里注册了 BackgroundChangedMessage；Linux 侧之前漏了这条，
        // 结果「点背景切换器 → 发消息 → 没人消费」，表现就是切了没反应（L-70）。
        WeakReferenceMessenger.Default.Register<AppBackground, BackgroundChangedMessage>(this, (r, m) => r.OnBackgroundChangedMessage(m));
        // 上游 AppBackground.xaml 用 Win2D 画视频帧、用 ImageEx 贴静态图；这两个在这里都不可用，
        // 所以不走 InitializeComponent，直接放一个 Image 承担「贴壁纸」这件事（视频壁纸见 M3）。
        //
        // 可读性：Windows 上壁纸会被系统亚克力模糊+压暗，这里没有模糊，只能靠「深色底 + 降低壁纸不透明度」近似，
        // 否则亮色壁纸会让上层文字完全看不清（实测默认兜底图就是一张明亮的过场 CG）。
        _image.Opacity = 0.42;
        Content = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x16, 0x16, 0x1A)),
            Children = { _image },
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
            BackgroundImageSource = ToBitmap(filePath);
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

    private WriteableBitmap? _videoBitmap;

    private byte[]? _videoPixels;


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
            _videoBitmap = new WriteableBitmap(width, height);
            _videoPixels = new byte[width * height * 4];
            _videoPlayer.FrameReady += (pixels, w, h) =>
            {
                DispatcherQueue.TryEnqueue(() => PresentVideoFrame(pixels, w, h));
            };
            if (!_videoPlayer.Start(videoPath))
            {
                StopVideoBackground();
                return false;
            }
            _image.Opacity = 0.6;
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
            if (_videoBitmap is null || _disposed)
            {
                return;
            }
            System.Buffer.BlockCopy(pixels, 0, _videoPixels!, 0, pixels.Length);
            using (Stream stream = _videoBitmap.PixelBuffer.AsStream())
            {
                stream.Write(_videoPixels!, 0, _videoPixels!.Length);
            }
            // Uno/Skia 下把同一个 WriteableBitmap 再赋给 Source 不会触发重绘（引用没变），
            // 先置空再赋值才能让合成器重新采样（L-71）。
            _image.Source = null;
            _image.Source = _videoBitmap;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] present video frame failed");
            StopVideoBackground();
        }
    }


    private void StopVideoBackground()
    {
        _videoPlayer?.Dispose();
        _videoPlayer = null;
        _currentVideoPath = null;
        _videoBitmap = null;
        _videoPixels = null;
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
        BackgroundImageSource = ToBitmap(file);
    }

}
