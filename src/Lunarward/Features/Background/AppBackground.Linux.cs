// Lunarward：应用背景（壁纸）。
//
// 上游用 Win2D + MediaPlayer 播放视频壁纸、用 CanvasBitmap 做缩放；Linux 侧：
//   - 图片：BitmapImage 直接加载（file:// 与 ms-appx://），主题文字层用 SkiaSharp 合成
//   - 视频：LibVLCSharp（vmem 回调）取帧，双缓冲 WriteableBitmap 交替贴给 Image（见 VideoBackgroundPlayer）
// 选图逻辑对齐上游 0.19.0 的 AppBackground.xaml.cs：自定义背景 → 背景列表选中项 → 兜底图。
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    // 合成间隔（毫秒）。0 = 还没测出来；以前这里写死 1000/30，等于替视频决定帧率——
    // 24 fps 的素材被当成「帧来得慢、不用限」，60 fps 的被砍掉一半（用户 2026-11-24 指出）。
    // 现在的口径：量视频自己的帧到达节奏（VLC 按源帧率送帧），用实测间隔限合成。
    // LUNARWARD_VIDEO_FPS 仍可显式覆盖（开发开关，Release 需 LUNARWARD_DEV=1，硬规则 6）。
    private static readonly int _videoFrameIntervalOverride = int.TryParse(Features.Dev.DevSwitches.DevEnv("LUNARWARD_VIDEO_FPS"), out int fps) && fps > 0 ? Math.Max(1000 / fps, 1) : 0;

    private long _videoFrameIntervalMs;

    /// <summary>最近 16 帧的到达时刻（TickCount64），用来量源帧率。</summary>
    private readonly long[] _videoFrameTicks = new long[16];

    private int _videoFrameTickCount;

    private int _videoFrameTickNext;

    private bool _videoFrameRateLogged;

    private long _lastVideoInvalidate;

    // E：起播后 2 秒内一帧都没来就说明「播放器起来了但没解码」，这类问题以前只能靠 CPU 占用才能发现。
    private long _videoFrameCount;

    /// <summary>开发开关（Release 需 LUNARWARD_DEV=1）：只合成不贴图，用来分离「解码贵」还是「贴图贵」。</summary>
    private static readonly bool _skipVideoPaint = Features.Dev.DevSwitches.DevEnv("LUNARWARD_VIDEO_NOPAINT") is "1";


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
    // 0.19.0 起接口返回「整个背景列表 + 选中项」，缓存也一起存。
    private readonly Dictionary<string, (List<GameBackgroundModel>? Models, GameBackgroundModel? Selected, DateTime At)> _suggestedBackgroundCache = new();

    /// <summary>当前生效的自定义背景文件；用来判断用户有没有换/删自定义背景。</summary>
    private string? _customBgPath;

    private string? _lastAccentFile;

    private string? _lastBackgroundFile;

    /// <summary>当前已经应用好背景的游戏；页面切来切去时不要重做（D62）。</summary>
    private GameId? _appliedGameId;

    /// <summary>B22：用户改了自定义背景，下一次更新不能因为「还是同一个游戏」被守卫挡掉。</summary>
    private bool _backgroundDirty;

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

    // 视频帧贴图：两块 WriteableBitmap 交替赋给 Image。
    // 早先每帧往同一个 WriteableBitmap 里写像素再赋回同一个实例，Source 值没变 → Image 不重新采样，
    // 画面停住（L-71，修法是先赋 null 再赋回）。改成两块交替就自然每次都是新实例，不必用那个技巧。
    // 之前这里用过 SKXamlCanvas，但 Uno 7 不再提供 Uno 版的 SkiaSharp.Views 运行时程序集
    // （UNOB0023），所以回到 WriteableBitmap（L-184）。
    private readonly Image _videoImage = new()
    {
        Visibility = Visibility.Collapsed,
        Stretch = Stretch.UniformToFill,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
    };

    private WriteableBitmap? _videoBitmapA;

    private WriteableBitmap? _videoBitmapB;

    private bool _videoBitmapFlip;

    private byte[]? _videoPixelBuffer;

    // 视频帧双缓冲：
    // 解码线程（libVLC vmem 回调）拷进 back，然后在锁内跟 front 交换；
    // UI 线程只负责读取 front 画一次。这样 8.3 MB/帧的 Marshal.Copy 不再占用 UI 线程，
    // 而且 UI 队列里最多只有一个待处理的 Invalidate（合并），不会随切换/负载堆积。
    private readonly object _frameLock = new();

    private SkiaSharp.SKBitmap? _videoFront;

    private SkiaSharp.SKBitmap? _videoBack;

    private int _videoWidth;

    private int _videoHeight;

    private int _paintQueued;

    private bool _hasVideoFrame;


    public AppBackground()
    {
        Current = this;
        // UI 卡顿看门狗：默认关闭，LUNARWARD_UI_WATCHDOG=1 时启用（见 Helpers/UiWatchdog.cs）。
        Starward.Helpers.UiWatchdog.Start(DispatcherQueue);
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
        Content = new Grid
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(0xFF, 0x16, 0x16, 0x1A)),
            Children = { _image, _videoImage },
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
            // 0.19.0 起「选中哪张背景」由 GameBackgroundModel 表示；消息里带的还是上游 DTO，
            // 按 Id 映射回当前列表里的模型，再走与切换器相同的入口。
            GameBackgroundModel? model = GameBackgrounds?.FirstOrDefault(x => x.Id == message.GameBackground?.Id);
            if (model is not null && GameBackgrounds is { } models)
            {
                _ = ChangeCurrentBackgroundIndexAsync(models.IndexOf(model));
                return;
            }
            // B22：换自定义壁纸/开关自定义背景发的是无参消息，必须让守卫失效并丢掉建议背景缓存。
            _backgroundDirty = true;
            _suggestedBackgroundCache.Clear();
            _ = UpdateBackgroundAsync();
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
        // 自定义背景是用户自己的图，上游不给它叠主题图：调用方传 background = null 即可。
        // LUNARWARD-DEVIATION: 0.19.0 删掉了 BACKGROUND_TYPE_CUSTOM，自定义背景改由
        // BackgroundService.TryGetCustomBgFilePath 返回本地路径，不再是一种 GameBackground.Type。
        if (background?.Theme?.Url is { Length: > 0 } themeUrl)
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
    /// 取「当前游戏的背景列表 + 选中项」（上游 0.19.0 的 GetGameBackgroundsAsync 形状），
    /// 成功与失败都各缓存一段时间：失败时返回空列表 + null 由调用方走静态兜底图，
    /// 但不再每次切页都重新等一轮超时。
    /// </summary>
    private async Task<(List<GameBackgroundModel> Models, GameBackgroundModel? Selected)> GetSuggestedBackgroundCachedAsync(GameId gameId, CancellationToken token)
    {
        string cacheKey = gameId.Id ?? string.Empty;
        if (_suggestedBackgroundCache.TryGetValue(cacheKey, out (List<GameBackgroundModel>? Models, GameBackgroundModel? Selected, DateTime At) cached))
        {
            // 拿到过背景：正常缓存 5 分钟；没拿到（超时/异常）：只缓存 30 秒，别让一次网络抖动影响好几分钟。
            TimeSpan ttl = cached.Selected is null ? SuggestedBackgroundFailureTtl : SuggestedBackgroundTtl;
            if (DateTime.UtcNow - cached.At < ttl)
            {
                return (cached.Models ?? new List<GameBackgroundModel>(), cached.Selected);
            }
        }
        // 上游给 1 秒 API 超时；Linux 首包要 TLS 握手，1 秒几乎必然超时（L-71），这里放宽到 4 秒（可调）。
        int apiTimeoutMs = int.TryParse(Features.Dev.DevSwitches.DevEnv("LUNARWARD_BG_TIMEOUT_MS"), out int dbgMs) && dbgMs > 0 ? dbgMs : 4000;
        try
        {
            // 超时往往只是首包 TLS 慢（L-71）：按 1x / 2x 退避重试，仍失败才算「拿不到」。
            int[] attempts = { apiTimeoutMs, apiTimeoutMs * 2 };
            for (int i = 0; i < attempts.Length; i++)
            {
                try
                {
                    using CancellationTokenSource apiTimeout = new(TimeSpan.FromMilliseconds(attempts[i]));
                    using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(token, apiTimeout.Token);
                    (List<GameBackgroundModel> models, GameBackgroundModel? selected) = await _backgroundService.GetGameBackgroundsAsync(gameId, linked.Token);
                    _suggestedBackgroundCache[cacheKey] = (models, selected, DateTime.UtcNow);
                    Serilog.Log.Debug("[background] backgrounds: {Count}, selected {Id} type {Type}", models.Count, selected?.Id, selected?.Type);
                    return (models, selected);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    if (i == attempts.Length - 1)
                    {
                        throw;
                    }
                    Serilog.Log.Debug("[background] game backgrounds timed out after {Ms} ms, retrying", attempts[i]);
                }
            }
            return (new List<GameBackgroundModel>(), null);
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            _suggestedBackgroundCache[cacheKey] = (null, null, DateTime.UtcNow);
            Serilog.Log.Information("[background] get game backgrounds timed out (retried once); retry in {Seconds} s", SuggestedBackgroundFailureTtl.TotalSeconds);
            return (new List<GameBackgroundModel>(), null);
        }
        catch (Exception ex)
        {
            _suggestedBackgroundCache[cacheKey] = (null, null, DateTime.UtcNow);
            Serilog.Log.Warning(ex, "[background] get game backgrounds failed");
            return (new List<GameBackgroundModel>(), null);
        }
    }


    /// <summary>SkiaSharp 合成：底图铺满 + 主题图按上游语义整幅拉伸叠加，输出 PNG。</summary>
    private static byte[] ComposeBackgroundPng(string backgroundPath, string themePath)
    {
        using var background = Starward.Helpers.ImageDecode.Load(backgroundPath);
        using var theme = Starward.Helpers.ImageDecode.Load(themePath);
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
            if (value is null)
            {
                // 上游：清空背景时把旧图留作占位图。Linux 侧没有 XAML 占位层，
                // 画面上已有的图不能抹掉（否则切背景失败就黑屏），只记占位图。
                if (_backgroundImageSource is not null)
                {
                    PlaceholderImageSource = _backgroundImageSource;
                }
                return;
            }
            _backgroundImageSource = value;
            _image.Source = value;
            Raise(nameof(BackgroundImageSource));
        }
    }


    private ImageSource? _placeholderImageSource;

    /// <summary>占位图（上游 AppBackground.xaml 的 x:Bind 目标，Linux 侧保留属性以对齐 XAML）。</summary>
    public ImageSource? PlaceholderImageSource
    {
        get => _placeholderImageSource;
        set
        {
            _placeholderImageSource = value;
            Raise(nameof(PlaceholderImageSource));
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


    private ObservableCollection<GameBackgroundModel>? _gameBackgrounds;

    /// <summary>当前游戏可用的背景列表（上游 0.19.0 的 AppBackground.GameBackgrounds）。</summary>
    public ObservableCollection<GameBackgroundModel>? GameBackgrounds
    {
        get => _gameBackgrounds;
        private set
        {
            if (ReferenceEquals(_gameBackgrounds, value))
            {
                return;
            }
            _gameBackgrounds = value;
            Raise(nameof(GameBackgrounds));
        }
    }


    private GameBackgroundModel? _currentGameBackground;

    /// <summary>当前选中的背景（上游 0.19.0 起是 GameBackgroundModel，不再是 GameBackground）。</summary>
    public GameBackgroundModel? CurrentGameBackground
    {
        get => _currentGameBackground;
        set
        {
            if (ReferenceEquals(_currentGameBackground, value))
            {
                return;
            }
            _currentGameBackground = value;
            Raise(nameof(CurrentGameBackground));
        }
    }


    private bool _isVideoPlaying;

    public bool IsVideoPlaying
    {
        get => _isVideoPlaying;
        set
        {
            if (_isVideoPlaying == value)
            {
                return;
            }
            _isVideoPlaying = value;
            Raise(nameof(IsVideoPlaying));
        }
    }


    private int _videoBgVolume = Math.Clamp(AppConfig.VideoBgVolume, 0, 100);

    public int VideoBgVolume
    {
        get => _videoBgVolume;
        set
        {
            if (_videoBgVolume == value)
            {
                return;
            }
            _videoBgVolume = value;
            AppConfig.VideoBgVolume = value;
            // LUNARWARD-DEVIATION: VideoBackgroundPlayer（libvlc）以 --no-audio 启动，暂不支持音量；
            // 这里只持久化设置（上游是 _mediaPlayer.Volume）。播放器文件不在本次授权范围内。
            Raise(nameof(VideoBgVolume));
        }
    }


    private string? _themeImageUrl;

    public string? ThemeImageUrl
    {
        get => _themeImageUrl;
        set
        {
            _themeImageUrl = value;
            Raise(nameof(ThemeImageUrl));
        }
    }


    public string? DisplayArguments { get; set; }


    private static readonly Uri DefaultBackground = new("ms-appx:///Assets/Image/UI_CutScene_1130320101A.png");


    private bool _disposed;


    /// <summary>
    /// 刷新背景（上游 0.19.0 同名方法）：自定义背景 → 背景列表选中项 → 兜底图。
    /// </summary>
    public async Task UpdateBackgroundAsync()
    {
        await UpdateBackgroundCoreAsync(null);
    }


    /// <param name="requested">
    /// 用户从切换器里显式选中的背景；为 null 时按「建议背景」逻辑走（启动/换游戏）。
    /// </param>
    private async Task UpdateBackgroundCoreAsync(GameBackgroundModel? requested)
    {
        GameId? gameId = CurrentGameId;
        // 自定义背景是本地文件，判断它比走接口便宜得多：用户换/删了自定义背景必须立刻反映出来。
        // 上游 0.19.0 的 UpdateBackgroundAsync 也是第一段就处理自定义背景。
        string? customBgPath = null;
        bool hasCustomBg = gameId is not null && BackgroundService.TryGetCustomBgFilePath(gameId, out customBgPath);
        bool customBgChanged = hasCustomBg != (_customBgPath is not null) || !string.Equals(customBgPath, _customBgPath, StringComparison.Ordinal);
        // 没换游戏、用户也没在切换器里点新背景、自定义背景也没变：当前这张就是对的，别把视频停掉再重下一遍。
        if (requested is null && !_backgroundDirty && !customBgChanged && gameId is not null && gameId == _appliedGameId && (BackgroundImageSource is not null || _videoActive))
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
                // 0.19.0：GetFallbackBackgroundImage() 不再按游戏取「上次背景」，
                // 「上次用的背景」改由 GetLastAppBackgroundFile 单拿（上游 UpdateBackgroundAsync 同序）。
                string? quickPath = gameId is null ? null : BackgroundService.GetLastAppBackgroundFile(gameId);
                quickPath ??= BackgroundService.GetFallbackBackgroundImage();
                if (quickPath is not null)
                {
                    BackgroundImageSource = await LoadBackgroundImageAsync(quickPath, null, CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "[background] quick background failed");
            }
        }
        if (gameId is null)
        {
            GameBackgrounds = null;
            CurrentGameBackground = null;
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
            if (!string.IsNullOrWhiteSpace(Features.Dev.DevSwitches.DevEnv("LUNARWARD_VIDEO_BG"))
                && await TryStartVideoBackgroundAsync(null, token))
            {
                // 调试入口也要记状态：否则守卫失效，每次进页面都会重启播放器（日志里反复 video background started）。
                _appliedGameId = gameId;
                _videoActive = true;
                IsVideoPlaying = true;
                return;
            }

            // 0.19.0：自定义背景优先于游戏背景列表，且不受「列表里选中哪张」影响。
            if (hasCustomBg && customBgPath is not null)
            {
                _customBgPath = customBgPath;
                GameBackgrounds = null;
                CurrentGameBackground = null;
                if (BackgroundService.FileIsSupportedVideo(customBgPath))
                {
                    if (await TryStartVideoBackgroundAsync(null, token, customBgPath))
                    {
                        _appliedGameId = gameId;
                        _videoActive = true;
                        IsVideoPlaying = true;
                        return;
                    }
                    Serilog.Log.Warning("[background] custom video cannot play, falling back to game background");
                }
                else
                {
                    await ApplyStaticBackgroundAsync(customBgPath, null, token);
                    _appliedGameId = gameId;
                    return;
                }
            }
            _customBgPath = null;

            GameBackgroundModel? selected = requested;
            if (selected is null)
            {
                var (models, suggestion) = await GetSuggestedBackgroundCachedAsync(gameId, token);
                GameBackgrounds = new ObservableCollection<GameBackgroundModel>(models);
                selected = suggestion;
            }
            else if (GameBackgrounds is null)
            {
                // 切换器里点的那张来自「还没拉过列表」的场景（例如页面直接从消息里拿）：补一次列表，序号才对得上。
                var (models, _) = await GetSuggestedBackgroundCachedAsync(gameId, token);
                GameBackgrounds = new ObservableCollection<GameBackgroundModel>(models);
            }
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (selected is null)
            {
                await ApplyStaticBackgroundAsync(BackgroundService.GetFallbackBackgroundImage(), null, token);
                CurrentGameBackground = null;
                _appliedGameId = gameId;
                SaveGameBackgroundSetting();
                return;
            }
            bool isVideo = selected.Type is 1 && selected.GameBackground.Type is GameBackground.BACKGROUND_TYPE_VIDEO;
            if (isVideo)
            {
                // 视频壁纸：帧由 LibVLCSharp 取，主题图（海报，带游戏标题文字）按上游语义整幅拉伸画在帧上。
                if (await TryStartVideoBackgroundAsync(selected.GameBackground, token))
                {
                    _appliedGameId = gameId;
                    _videoActive = true;
                    IsVideoPlaying = true;
                    CurrentGameBackground = selected;
                    SaveGameBackgroundSetting();
                    // 视频在播时先把「暂停后要看到的那张静态图（海报+主题文字层）」在后台准备好：
                    // 否则点暂停要先等海报下载（≤3s）+ 主题图下载（≤6s）+ Skia 合成，好几秒才出图（真机复现）。
                    _ = PreloadPausedBackgroundAsync(selected.GameBackground, token);
                    // 视频壁纸同样要按海报取强调色（上游只在静态分支取色；D50 统一成取海报）。
                    _ = ApplyAccentFromPosterAsync(selected.GameBackground, token);
                    return;
                }
                // 没有 libvlc / 拿不到视频文件时：已有画面就保持，什么都没有才退回兜底图（不黑屏）
                if (BackgroundImageSource is not null || _videoActive)
                {
                    Serilog.Log.Information("[background] video unavailable, keeping current picture");
                    CurrentGameBackground = selected;
                    return;
                }
                await ApplyStaticBackgroundAsync(BackgroundService.GetFallbackBackgroundImage(), null, token);
            }
            else
            {
                string? filePath = await DownloadBackgroundFileAsync(selected.GameBackground, token);
                await ApplyStaticBackgroundAsync(filePath, selected.GameBackground, token);
            }
            CurrentGameBackground = selected;
            _appliedGameId = gameId;
            SaveGameBackgroundSetting();
        }
        catch (OperationCanceledException)
        {
            // 拿不到建议背景 ≠ 现在没有背景：只要画面上已经有东西（尤其视频正在播），就保持现状。
            if (BackgroundImageSource is null && !_videoActive)
            {
                Serilog.Log.Information("[background] cancelled before any background, using fallback image");
                BackgroundImageSource = ToBitmap(BackgroundService.GetFallbackBackgroundImage());
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] update failed");
            if (BackgroundImageSource is null && !_videoActive)
            {
                BackgroundImageSource = ToBitmap(BackgroundService.GetFallbackBackgroundImage());
            }
        }
        finally
        {
            IsUpdateBackgroundRunning = false;
        }
    }


    /// <summary>下载/定位某张背景的静态图（上游 SetImageBackgroundAsync 里取 Background.Url 那一步）。</summary>
    private async Task<string?> DownloadBackgroundFileAsync(GameBackground background, CancellationToken token)
    {
        if (background.Background?.Url is { Length: > 0 } url)
        {
            try
            {
                using CancellationTokenSource downloadTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                downloadTimeout.CancelAfter(TimeSpan.FromSeconds(3));
                string? file = await _backgroundService.GetBackgroundFileAsync(url, downloadTimeout.Token);
                if (!string.IsNullOrWhiteSpace(file))
                {
                    return file;
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                Serilog.Log.Debug("[background] download background timed out: {url}", url);
            }
        }
        return BackgroundService.GetFallbackBackgroundImage();
    }


    /// <summary>
    /// 贴一张静态背景（底图 + 主题文字层），取强调色并记录「上次用的背景」。
    /// 对应上游 SetImageBackgroundAsync(string file, CancellationToken)（那里的解码/取色是 WinRT 实现）。
    /// </summary>
    private async Task ApplyStaticBackgroundAsync(string? filePath, GameBackground? background, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }
        // 视频切静态：先停播放器（StopVideoBackground 会把预加载好的静态画面显示出来）。
        StopVideoBackground();
        if (token.IsCancellationRequested)
        {
            return;
        }
        if (filePath == _lastBackgroundFile)
        {
            // 图没变（视频暂停时预加载的就是这张）：画面已经对了，别重解码、别重取色（换色会触发整棵树重解析主题，闪）。
            return;
        }
        _lastBackgroundFile = filePath;
        // 与上游 SetImageBackgroundAsync 一致：把主题图 URL 也暴露给 XAML（Linux 侧是自己合成，不依赖它）。
        ThemeImageUrl = background?.Theme?.Url;
        ImageSource? source = await LoadBackgroundImageAsync(filePath, background, token);
        if (token.IsCancellationRequested)
        {
            return;
        }
        BackgroundImageSource = source ?? ToBitmap(filePath);
        // 上游在静态背景分支调用 AccentColorHelper.ChangeAppAccentColor（AppBackground.xaml.cs:551），
        // Linux 侧按图取主色写回主题字典；同一张图只换一次色（重复调用会闪）。
        if (filePath != _lastAccentFile)
        {
            _lastAccentFile = filePath;
            Starward.Helpers.AccentColorHelper.ApplyFromFile(filePath);
        }
        RecordAppliedBackground(background, filePath);
    }


    /// <summary>切到某张静态背景（上游 SetImageBackgroundAsync(GameBackground)）。</summary>
    private async Task SetImageBackgroundAsync(GameBackground background, CancellationToken token)
    {
        string? filePath = await DownloadBackgroundFileAsync(background, token);
        await ApplyStaticBackgroundAsync(filePath, background, token);
    }


    /// <summary>切到某张视频背景（上游 SetVideoBackgroundAsync(GameBackground)）。</summary>
    private async Task SetVideoBackgroundAsync(GameBackground background, CancellationToken token)
    {
        if (await TryStartVideoBackgroundAsync(background, token))
        {
            ThemeImageUrl = background.Theme?.Url;
            IsVideoPlaying = true;
            _videoActive = true;
            _ = PreloadPausedBackgroundAsync(background, token);
            _ = ApplyAccentFromPosterAsync(background, token);
            return;
        }
        // LUNARWARD-DEVIATION: 上游 MediaPlayer 起不来会弹 toast 并落回静态图；Linux 没有 libvlc 时
        // 保持当前画面（没有画面才用兜底图），避免在切换器里点视频变成黑屏。
        Serilog.Log.Warning("[background] video background unavailable, keeping image");
        if (BackgroundImageSource is null)
        {
            await ApplyStaticBackgroundAsync(BackgroundService.GetFallbackBackgroundImage(), null, token);
        }
    }


    /// <summary>把「选中第几张 + 每张的类型」写进设置（上游 SaveGameBackgroundSetting 的格式）。</summary>
    private void SaveGameBackgroundSetting()
    {
        try
        {
            if (CurrentGameId is not { } gameId)
            {
                return;
            }
            if (GameBackgrounds is not null && CurrentGameBackground is not null)
            {
                int index = GameBackgrounds.IndexOf(CurrentGameBackground);
                if (index >= 0)
                {
                    // 上游 0.19.0 的格式："选中序号;id,type;id,type;..."
                    string content = $"{index};{string.Join(';', GameBackgrounds.Select(x => $"{x.Id},{x.Type}"))}";
                    AppConfig.SetGameBackground(gameId, content);
                }
            }
            else
            {
                AppConfig.SetGameBackground(gameId, null);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[background] save background setting failed");
        }
    }


    /// <summary>切换器里点圆点/滑滚轮（上游 ChangeCurrentBackgroundIndexAsync）。</summary>
    public async Task ChangeCurrentBackgroundIndexAsync(int value)
    {
        try
        {
            if (GameBackgrounds is null)
            {
                return;
            }
            if (value < 0 || value >= GameBackgrounds.Count)
            {
                return;
            }
            GameBackgroundModel model = GameBackgrounds[value];
            if (CurrentGameBackground == model)
            {
                return;
            }
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
            if (model.Type is 1)
            {
                await SetVideoBackgroundAsync(model.GameBackground, cts.Token);
            }
            else
            {
                await SetImageBackgroundAsync(model.GameBackground, cts.Token);
            }
            CurrentGameBackground = model;
            SaveGameBackgroundSetting();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] change background failed");
        }
    }


    /// <summary>视频/静态切换（上游 PlayPauseVideo：把当前模型的 Type 在 1/0 之间翻转并持久化）。</summary>
    public async void PlayPauseVideo()
    {
        try
        {
            if (CurrentGameBackground is null)
            {
                return;
            }
            if (CurrentGameBackground.GameBackground.Type is not GameBackground.BACKGROUND_TYPE_VIDEO)
            {
                return;
            }
            CurrentGameBackground.Type = CurrentGameBackground.Type is 1 ? 0 : 1;
            SaveGameBackgroundSetting();
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(30));
            if (CurrentGameBackground.Type is 1)
            {
                await SetVideoBackgroundAsync(CurrentGameBackground.GameBackground, cts.Token);
            }
            else
            {
                await SetImageBackgroundAsync(CurrentGameBackground.GameBackground, cts.Token);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] play/pause video failed");
        }
    }


    /// <summary>当前背景对应的本地文件（视频在播时是视频文件，否则是静态图）。</summary>
    private async Task<string?> GetCurrentBackgroundFileAsync()
    {
        if (CurrentGameBackground is null)
        {
            return null;
        }
        string? url = CurrentGameBackground.GameBackground.Type is GameBackground.BACKGROUND_TYPE_VIDEO && IsVideoPlaying
            ? CurrentGameBackground.GameBackground.Video?.Url
            : CurrentGameBackground.GameBackground.Background?.Url;
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }
        return await _backgroundService.GetBackgroundFileAsync(url, CancellationToken.None);
    }


    /// <summary>复制当前背景文件到剪贴板（上游 CopyCurrentBackgroundAsync）。</summary>
    public async Task CopyCurrentBackgroundAsync()
    {
        try
        {
            string? path = await GetCurrentBackgroundFileAsync();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(path);
            // Linux 侧 ClipboardHelper 写的是 text/uri-list（文件管理器可粘贴），与上游语义一致。
            Starward.Helpers.ClipboardHelper.SetStorageItems(Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy, file);
            Helpers.InAppToast.MainWindow?.Information(Starward.Language.Lang.Common_CopiedToClipboard);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] copy background failed");
        }
    }


    /// <summary>把当前背景另存为（上游 SaveCurrentBackgroundAsync）。</summary>
    public async Task SaveCurrentBackgroundAsync()
    {
        try
        {
            string? path = await GetCurrentBackgroundFileAsync();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }
            // LUNARWARD-DEVIATION: 上游保存后用 Launcher.LaunchFolderAsync 在资源管理器里选中该文件；
            // Linux 没有等价能力（也不该随便拉起文件管理器），保存完只提示落地路径。
            string? savePath = await Starward.Helpers.FileDialogHelper.OpenSaveFileDialogAsync(this.XamlRoot, Path.GetFileName(path));
            if (string.IsNullOrWhiteSpace(savePath))
            {
                return;
            }
            File.Copy(path, savePath, true);
            Helpers.InAppToast.MainWindow?.Information(savePath);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] save background failed");
        }
    }


    private VideoBackgroundPlayer? _videoPlayer;

    private string? _currentVideoPath;

    // 旧的 WriteableBitmap 双缓冲方案已废弃（Image 复用同一实例不重新采样，见 L-71），保留字段清理见 StopVideoBackground。






    /// <summary>
    /// 启动视频壁纸：下载/定位视频文件与主题图，起播放器，把合成帧写进 WriteableBitmap。
    /// 返回 false 表示「这条视频用不了」，调用方退回静态图。
    /// </summary>
    /// <param name="customVideoPath">自定义背景（UserData/bg 下的本地视频）；null 时按 API/调试变量取。</param>
    private async Task<bool> TryStartVideoBackgroundAsync(GameBackground? background, CancellationToken token, string? customVideoPath = null)
    {
        if (!VideoBackgroundPlayer.IsAvailable)
        {
            Serilog.Log.Warning("[background] video skipped: libvlc unavailable");
            return false;
        }
        try
        {
            // customVideoPath 优先（0.19.0 的自定义背景），其次 M3 调试变量。
            string? videoPath = customVideoPath ?? Features.Dev.DevSwitches.DevEnv("LUNARWARD_VIDEO_BG");
            string? themePath = Features.Dev.DevSwitches.DevEnv("LUNARWARD_VIDEO_OVERLAY");
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
            _videoPlayer.FrameReady += OnVideoFrameArrived;
            if (!_videoPlayer.Start(videoPath))
            {
                StopVideoBackground();
                return false;
            }
            _image.Visibility = Visibility.Collapsed;
            _videoImage.Visibility = Visibility.Visible;
            _currentVideoPath = videoPath;
            Interlocked.Exchange(ref _videoFrameCount, 0);
            Serilog.Log.Information("[background] video background started: {path}", videoPath);
            _ = WarnIfNoVideoFrameAsync(videoPath);
            RecordAppliedBackground(background, videoPath);
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] start video background failed");
            StopVideoBackground();
            return false;
        }
    }


    /// <summary>
    /// 量视频自己的帧节奏，返回「两次合成之间至少隔多少毫秒」。
    /// 前 8 帧的间隔还不代表稳态（起播、seek 之后会有跳变），所以样本不够时返回 0（不限）；
    /// 取最近 16 帧间隔的均值再打 8 折，既不会因为抖动把正常帧丢掉，又能在 UI 忙时合并重绘。
    /// 上限 240 fps / 下限 10 fps：异常值（暂停后恢复的第一帧、掉帧）不至于把间隔拉爆。
    /// </summary>
    private long MeasureFrameInterval(long now)
    {
        _videoFrameTicks[_videoFrameTickNext] = now;
        _videoFrameTickNext = (_videoFrameTickNext + 1) % _videoFrameTicks.Length;
        if (_videoFrameTickCount < _videoFrameTicks.Length)
        {
            _videoFrameTickCount++;
        }
        if (_videoFrameTickCount < 8)
        {
            return 0;
        }
        long first = long.MaxValue;
        long last = long.MinValue;
        int deltas = 0;
        long sum = 0;
        for (int i = 0; i < _videoFrameTickCount; i++)
        {
            long tick = _videoFrameTicks[i];
            if (tick == 0)
            {
                continue;
            }
            if (tick < first)
            {
                first = tick;
            }
            if (tick > last)
            {
                last = tick;
            }
            deltas++;
        }
        deltas -= 1;
        if (deltas < 7 || last <= first)
        {
            return 0;
        }
        sum = last - first;
        double average = (double)sum / deltas;
        if (average < 1000.0 / 240 || average > 1000.0 / 10)
        {
            return 0;
        }
        return Math.Max((long)(average * 0.8), 1);
    }


    /// <summary>
    /// libVLC 的 vmem 回调线程上执行：拷进 back 缓冲、交换、再按目标帧率**合并**地请求一次重绘。
    /// 合并的意思是「UI 队列里最多挂一个 Invalidate」——UI 线程忙的时候直接丢帧，
    /// 而不是把任务越堆越多（堆积的表现就是画面停住、点东西几秒后才响应）。
    /// </summary>
    private void OnVideoFrameArrived(byte[] pixels, int width, int height)
    {
        try
        {
            if (_disposed || width <= 0 || height <= 0)
            {
                return;
            }
            Interlocked.Increment(ref _videoFrameCount);
            lock (_frameLock)
            {
                if (_videoBack is null || _videoWidth != width || _videoHeight != height)
                {
                    _videoFront?.Dispose();
                    _videoBack?.Dispose();
                    var info = new SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Opaque);
                    _videoFront = new SkiaSharp.SKBitmap(info);
                    _videoBack = new SkiaSharp.SKBitmap(info);
                    _videoWidth = width;
                    _videoHeight = height;
                    _hasVideoFrame = false;
                }
                nint target = _videoBack.GetPixels();
                if (target == 0)
                {
                    return;
                }
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, target, Math.Min(pixels.Length, _videoBack.ByteCount));
                (_videoFront, _videoBack) = (_videoBack, _videoFront);
                _hasVideoFrame = true;
            }
            if (_skipVideoPaint)
            {
                return;
            }
            long now = Environment.TickCount64;
            if (_videoFrameIntervalOverride > 0)
            {
                _videoFrameIntervalMs = _videoFrameIntervalOverride;
            }
            else
            {
                _videoFrameIntervalMs = MeasureFrameInterval(now);
                if (_videoFrameIntervalMs > 0 && !_videoFrameRateLogged)
                {
                    _videoFrameRateLogged = true;
                    Serilog.Log.Information("video background: measured {period:F1} ms/frame, composite interval {interval} ms",
                        _videoFrameIntervalMs / 0.8, _videoFrameIntervalMs);
                }
            }
            if (now - _lastVideoInvalidate < _videoFrameIntervalMs)
            {
                return;
            }
            if (Interlocked.Exchange(ref _paintQueued, 1) != 0)
            {
                // 已经有一个待处理的合成：本帧丢弃。
                return;
            }
            _lastVideoInvalidate = now;
            if (!DispatcherQueue.TryEnqueue(() =>
            {
                Interlocked.Exchange(ref _paintQueued, 0);
                PresentVideoFrame();
            }))
            {
                Interlocked.Exchange(ref _paintQueued, 0);
            }
        }
        catch (Exception ex)
        {
            Interlocked.Exchange(ref _paintQueued, 0);
            Serilog.Log.Warning(ex, "[background] present video frame failed");
            // B52：这是 LibVLC 的帧回调线程，改 XAML/Dispose 播放器都必须回到 UI 线程。
            if (!DispatcherQueue.TryEnqueue(StopVideoBackground))
            {
                Serilog.Log.Debug("[background] dispatcher unavailable while stopping video background");
            }
        }
    }


    /// <summary>
    /// 把当前帧贴到 <see cref="_videoImage"/>。必须在 UI 线程调用。
    /// 两块 WriteableBitmap 交替使用：每次赋给 Image 的 Source 都是不同实例，
    /// 所以不需要「先赋 null 再赋回」那种渡让（L-71 / L-184）。
    /// </summary>
    private void PresentVideoFrame()
    {
        lock (_frameLock)
        {
            if (!_hasVideoFrame)
            {
                return;
            }
            SkiaSharp.SKBitmap? bitmap = _videoFront;
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return;
            }
            int width = bitmap.Width;
            int height = bitmap.Height;
            int length = width * height * 4;
            if (_videoPixelBuffer is null
                || _videoPixelBuffer.Length < length
                || _videoBitmapA is null
                || _videoBitmapB is null
                || _videoBitmapA.PixelWidth != width
                || _videoBitmapA.PixelHeight != height)
            {
                _videoPixelBuffer = new byte[length];
                _videoBitmapA = new WriteableBitmap(width, height);
                _videoBitmapB = new WriteableBitmap(width, height);
                _videoBitmapFlip = false;
            }
            // UniformToFill 的裁切交给 Image 的 Stretch 去做（与静态壁纸一致的观感），
            // 这里只负责把帧的像素原样搬过去。
            bitmap.GetPixelSpan().CopyTo(_videoPixelBuffer.AsSpan(0, length));
            WriteableBitmap target = _videoBitmapFlip ? _videoBitmapB : _videoBitmapA;
            _videoBitmapFlip = !_videoBitmapFlip;
            using (Stream stream = target.PixelBuffer.AsStream())
            {
                stream.Write(_videoPixelBuffer, 0, length);
            }
            _videoImage.Source = target;
        }
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
    /// 记「上次用的背景」（上游 SetImageBackgroundAsync/StartMediaPlayer 里的
    /// <c>AppConfig.SetBg(CurrentGameId, Path.GetFileName(file))</c>）。
    /// 0.19.0 去掉了 GetGameBackgroundIds/SetGameBackgroundIds（列表与选中项都存进 GetGameBackground 的字符串），
    /// 这里只写单个文件名，列表索引由 SaveGameBackgroundSetting 负责。
    /// </summary>
    private void RecordAppliedBackground(GameBackground? background, string? filePath)
    {
        try
        {
            if (background is null || string.IsNullOrWhiteSpace(filePath) || CurrentGameId is not { } gameId)
            {
                return;
            }
            AppConfig.SetBg(gameId, Path.GetFileName(filePath));
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
        _backgroundDirty = false;
        _videoPlayer?.Dispose();
        _videoPlayer = null;
        _currentVideoPath = null;
        _videoActive = false;
        lock (_frameLock)
        {
            _videoFront?.Dispose();
            _videoBack?.Dispose();
            _videoFront = null;
            _videoBack = null;
            _videoWidth = 0;
            _videoHeight = 0;
            _hasVideoFrame = false;
        }
        Interlocked.Exchange(ref _paintQueued, 0);
        IsVideoPlaying = false;
        _videoImage.Visibility = Visibility.Collapsed;
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
