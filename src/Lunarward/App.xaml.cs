using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
<<<<<<< /tmp/_ours
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameLauncher;
||||||| /tmp/_base
using Microsoft.Windows.AppLifecycle;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameLauncher;
=======
using Microsoft.Windows.AppLifecycle;
using Starward.Features.Database;
>>>>>>> /tmp/_theirs
using Starward.Features.GamepadControl;
<<<<<<< /tmp/_ours
||||||| /tmp/_base
using Starward.Features.UrlProtocol;
=======
using Starward.Features.Passport;
>>>>>>> /tmp/_theirs
using Starward.Features.ViewHost;
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using Vanara.PInvoke;
using Windows.ApplicationModel.Activation;


namespace Starward;

public partial class App : Application
{

    private readonly DispatcherQueue _uiDispatcherQueue;


    public static new App Current => (App)Application.Current;


    public App()
    {
        this.InitializeComponent();
        RequestedTheme = ApplicationTheme.Dark;
        _uiDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        UnhandledException += App_UnhandledException;
<<<<<<< /tmp/_ours
||||||| /tmp/_base
        _gcTimer.Elapsed += (_, _) => GC.Collect();
=======
        _gcTimer.Elapsed += (_, _) => GC.Collect();
        _gcTimer.Start();
>>>>>>> /tmp/_theirs
    }


    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        string logFile = AppConfig.LogFile;
        if (string.IsNullOrWhiteSpace(logFile))
        {
            string logFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Starward.Helpers.DataFolder.AppFolderName, "log");
            Directory.CreateDirectory(logFolder);
            logFile = Path.Combine(logFolder, $"Starward_{DateTime.Now:yyMMdd}.log");
        }
        var sb = new StringBuilder();
        sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] App Crash:");
        sb.AppendLine(e.Exception.ToString());
        if (e.Exception.Data.Count > 0)
        {
            foreach (DictionaryEntry item in e.Exception.Data)
            {
                sb.AppendLine($"{item.Key}: {item.Value}");
            }
        }
        using var fs = File.Open(logFile, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        using var sw = new StreamWriter(fs);
        sw.Write(sb);
    }


    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs _)
    {
        string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();

<<<<<<< /tmp/_ours
        await AppConfig.CheckEnviromentAsync();

        if (args.Length > 0 && await HandleStartupAsync(args))
||||||| /tmp/_base
        if (args.Length > 0 && args[0].ToLower().StartsWith("starward://test/"))
=======

        if (AppConfig.UserDataFolder is null)
>>>>>>> /tmp/_theirs
        {
<<<<<<< /tmp/_ours
            return;
||||||| /tmp/_base
            new TestUrlProtocolWindow().Activate();
            return;
=======
            if (await new WelcomeWindow().WaitAsync())
            {
                AppConfig.LoadConfiguration();
            }
            else
            {
                Environment.Exit(0);
            }
>>>>>>> /tmp/_theirs
        }

<<<<<<< /tmp/_ours
        // Lunarward：游戏启动后拉起「伴随启动」的后启动程序（D24/D28）
        Features.Companion.CompanionAutoStart.Register(AppConfig.GetService<Features.Companion.CompanionRunner>(), AppConfig.GetLogger<App>());

        // Lunarward：Uno 的 AcrylicBrush 只实现为 FallbackColor（96% 不透明的深色），会把壁纸完全盖住。
        // 这里换成半透明画刷，近似 Windows 上「壁纸透过亚克力」的观感（真模糊见 D31 / M3）。
        if (!OperatingSystem.IsWindows())
||||||| /tmp/_base
        await AppConfig.CheckEnviromentAsync();

        if (args.Length > 0 && await HandleStartupAsync(args))
=======
        instance = AppInstance.FindOrRegisterForKey("Starward.Main");
        if (!instance.IsCurrent)
>>>>>>> /tmp/_theirs
        {
<<<<<<< /tmp/_ours
            ApplyAcrylicFallback();
||||||| /tmp/_base
            return;
=======
            await instance.RedirectActivationToAsync(instance.GetActivatedEventArgs());
            Environment.Exit(0);
>>>>>>> /tmp/_theirs
        }
<<<<<<< /tmp/_ours

        // Lunarward：Linux 首次运行先识别已有安装（注册表不可用，改为扫目录，D25）
        if (!OperatingSystem.IsWindows())
        {
            // D86：结果以 KnownGames 为准。ScanAndApply 在「缓存还新鲜」时直接 return 0，
            // 拿返回值写日志会一直显示「识别到 0 个」，与实际认到的游戏数量无关。
            // D95：扫描是磁盘 IO，放后台线程，避免「启动先卡几秒」（B65）。
            await System.Threading.Tasks.Task.Run(() => Features.GameInstall.GameInstallScanner.ScanAndApply());
            int knownGames = Features.GameInstall.GameInstallScanner.KnownGames.Count;
            if (knownGames > 0)
            {
                Serilog.Log.Information("[scan] 识别到 {n} 个已安装游戏", knownGames);
            }
            // M1 验证用：LUNARWARD_GAME=hk4e_cn 直接选中某个游戏（正式版移除）
            string? debugGame = Starward.Features.Dev.DevSwitches.Game;
            if (!string.IsNullOrWhiteSpace(debugGame) && Starward.Core.GameBiz.TryParse(debugGame, out Starward.Core.GameBiz debugBiz))
            {
                AppConfig.SelectedGameBizs = debugGame;
                AppConfig.CurrentGameBiz = debugBiz;
            }
        }
||||||| /tmp/_base

        instance = AppInstance.GetCurrent();
        instance.Activated += AppInstance_Activated;
=======
        instance = AppInstance.GetCurrent();
        instance.Activated += AppInstance_Activated;
>>>>>>> /tmp/_theirs

<<<<<<< /tmp/_ours
        // Lunarward：Linux 单实例 = 文件锁（判定谁在跑）+ Unix socket（把「再次启动」变成「唤起已有窗口」）。
        // 只有第二个实例会走到这里；它把意图发给主实例后立刻退出，不再开窗口（D6）。
        // --new-instance：跳过单实例检查（Rider/调试器里同时跑多个实例时用；也用于排查「没窗口」）。
        bool newInstance = Environment.GetCommandLineArgs().Contains("--new-instance");
        if (!newInstance && !TryAcquireSingleInstanceLock())
        {
            if (Starward.Features.SingleInstance.SingleInstancePipe.NotifyPrimary(Environment.GetCommandLineArgs().Skip(1).ToArray()))
            {
                Environment.Exit(0);
            }
            // 有人占着锁但 socket 没人接：多半是上次崩溃/被 kill 留下的陈旧锁。
            // 以前这里直接 Exit(0)，表现就是「启动没反应、也没有窗口」（L-72）。
            Serilog.Log.Warning("Single instance lock is held but no primary answered; treating it as stale.");
            Console.Error.WriteLine("[lunarward] 单实例锁被占用但没有主实例响应，判定为残留锁，尝试接管……");
            Starward.Features.SingleInstance.SingleInstancePipe.RemoveStaleSocket();
            TryReleaseSingleInstanceLock();
            if (!TryAcquireSingleInstanceLock())
            {
                Console.Error.WriteLine($"[lunarward] 无法接管残留锁：{SingleInstanceLockPath()}");
                Serilog.Log.Error("Failed to take over stale single instance lock {Path}", SingleInstanceLockPath());
                Environment.Exit(2);
            }
            Serilog.Log.Information("Took over stale single instance lock.");
        }
        // socket 监听跑在线程池上，而这三个回调都要碰 UI（建窗口/激活/退出），
        // 不切回 UI 线程会在随机时机崩在 X11 调用里（L-65）。
        Microsoft.UI.Dispatching.DispatcherQueue uiDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        void OnUi(Action action)
        {
            if (uiDispatcher is null || uiDispatcher.HasThreadAccess)
            {
                action();
            }
            else
            {
                uiDispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "Single instance callback failed");
                    }
                });
            }
        }
        Starward.Features.SingleInstance.SingleInstancePipe.StartListening(
            onShow: () => OnUi(() =>
            {
                Serilog.Log.Information("Second instance asked to show the main window.");
                EnsureMainWindow();
                m_MainWindow?.Activate();
            }),
            onExit: () => OnUi(Exit),
            onStartGame: bizName => OnUi(() =>
            {
                Serilog.Log.Information("Second instance asked to start game {biz}", bizName);
                if (GameBiz.TryParse(bizName, out GameBiz biz) && GameId.FromGameBiz(biz) is GameId gameId)
                {
                    EnsureMainWindow();
                    // R39：以前把 Task 直接丢掉，预检失败/启动抛异常都没有任何反馈。
                    async Task LaunchAsync()
                    {
                        try
                        {
                            await AppConfig.GetService<GameLauncherService>().StartGameAsync(gameId);
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Error(ex, "Second instance launch failed: {game}", gameId.Id);
                            Helpers.InAppToast.MainWindow?.Error(ex.Message, null, 8000);
                        }
                    }
                    Task launchTask = LaunchAsync();
                    Serilog.Log.Debug("Second instance launch scheduled: {status}", launchTask.Status);
                }
            }));
||||||| /tmp/_base
        var main = AppInstance.FindOrRegisterForKey("main");
        if (!main.IsCurrent)
        {
            await main.RedirectActivationToAsync(instance.GetActivatedEventArgs());
            Environment.Exit(0);
        }
=======
>>>>>>> /tmp/_theirs
        if (Environment.GetCommandLineArgs().Contains("--hide"))
        {
            // Lunarward：Linux 的托盘是 StatusNotifierItem，不需要（也不应该）创建上游的 SystemTrayWindow 占位控件。
            if (OperatingSystem.IsWindows())
            {
                m_SystemTrayWindow = new SystemTrayWindow();
            }
            else
            {
                // Linux 的托盘和全局热键都挂在主窗口上（托盘在 MainWindow 构造里建，热键在 MainView.Loaded 里装），
                // 只建不显示会变成「进程在跑、没有任何入口」，只能去任务管理器杀（L-75）。
                // 所以还是把窗口建出来，等托盘/热键就绪后再藏起来。
                m_MainWindow = new MainWindow();
                m_MainWindow.Activate();
                System.Threading.Tasks.Task ignoredHide = HideAfterShownAsync(m_MainWindow);
            }
        }
        else
        {
            m_MainWindow = new MainWindow();
            m_MainWindow.Activate();
        }

#if DEBUG
        // 开发期验证强调色：LUNARWARD_DEV_ACCENT=#RRGGBB 会在启动后覆盖一次 Accent 色，
        // 用来确认 ThemeResource 的刷新链路是否生效（发行版不含）。
        await ApplyDevAccentAsync();
#endif
    }


#if DEBUG
    private static async Task ApplyDevAccentAsync()
    {
        string? devAccent = Environment.GetEnvironmentVariable("LUNARWARD_DEV_ACCENT");
        if (string.IsNullOrWhiteSpace(devAccent))
        {
            return;
        }
        try
        {
            await Task.Delay(1500);
            string hex = devAccent.TrimStart('#');
            if (hex.Length == 6 &&
                byte.TryParse(hex[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
                byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
                byte.TryParse(hex[4..], System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                Starward.Helpers.AccentColorHelper.ChangeAppAccentColor(Windows.UI.Color.FromArgb(255, r, g, b));
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "apply dev accent");
        }
    }
#endif


    /// <summary>启动参数 --hide 在 Linux 上的落地：先让窗口完成加载（托盘/热键），再隐藏。</summary>
    private static async Task HideAfterShownAsync(MainWindow window)
    {
        try
        {
            await Task.Delay(800);
            // B12：没有 StatusNotifierWatcher 的会话里，隐藏窗口等于让进程变成没有入口的隐形进程。
            if (!Features.Tray.StatusNotifierTray.Available)
            {
                Serilog.Log.Warning("Started with --hide but the system tray is unavailable; keeping the window visible");
                return;
            }
            window.Hide();
            Serilog.Log.Information("Started hidden (--hide); system tray and global hotkey are ready.");
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Hide after startup failed");
        }
    }


<<<<<<< /tmp/_ours
    private async Task<bool> HandleStartupAsync(string[] args)
    {
        // lunarward://startgame?biz=hk4e_cn → startgame --biz hk4e_cn
        // 桌面文件里写的是 `Exec="…" %u`，浏览器/其它程序点链接时传进来的就是这条 URL。
        args = ExpandUrlArgument(args);
        IConfiguration config = new ConfigurationBuilder().AddCommandLine(args).Build();
        Helpers.Housekeeping.Run(AppConfig.CacheFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "lunarward"));
        if (args[0].ToLower() is "rpc")
        {
            // Lunarward: RPC 子进程（D6 待换成 Unix domain socket）暂不启用
            _ = args;
            Environment.Exit(0);
        }
        if (args[0].ToLower() is "playtime")
        {
            int pid = config.GetValue<int>("pid");
            GameBiz biz = (GameBiz)config.GetValue<string>("biz");
            if (pid > 0)
            {
                var playtime = AppConfig.GetService<Features.PlayTime.PlayTimeRecordService>();
                await playtime.LogPlayTimeAsync(biz, pid);
            }
            Environment.Exit(0);
        }

        if (args[0].ToLower() is "startgame")
        {
            GameBiz biz = (GameBiz)config.GetValue<string>("biz");
            GameId? gameId = GameId.FromGameBiz(biz);
            if (gameId is not null)
            {
                await AppConfig.GetService<GameLauncherService>().StartGameAsync(gameId);
            }
            Environment.Exit(0);
        }

        return false;
    }




    /// <summary>
    /// 把 `lunarward://<host>?<query>` 展开成内部命令行参数。
    /// 目前只支持 `startgame`；未知 host 原样返回，交给后面的分支报错而不是假装成功。
    /// </summary>
    internal static string[] ExpandUrlArgument(string[] args)
    {
        if (args.Length == 0)
        {
            return args;
        }
        string first = args[0];
        if (!first.StartsWith("lunarward://", StringComparison.OrdinalIgnoreCase))
        {
            return args;
        }
        if (!Uri.TryCreate(first, UriKind.Absolute, out Uri? uri))
        {
            return args;
        }
        string host = uri.Host.ToLowerInvariant();
        string? biz = null;
        string? pid = null;
        foreach (string pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = pair.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            string name = pair[..eq];
            string value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            if (name.Equals("biz", StringComparison.OrdinalIgnoreCase))
            {
                biz = value;
            }
            else if (name.Equals("pid", StringComparison.OrdinalIgnoreCase))
            {
                pid = value;
            }
        }
        // B57：上游支持 startgame / playtime / test 三种 host，这里把 query 展开成内部参数。
        switch (host)
        {
            case "startgame":
                return biz is null ? ["startgame"] : ["startgame", "--biz", biz];
            case "playtime":
            {
                List<string> list = ["playtime"];
                if (!string.IsNullOrWhiteSpace(biz))
                {
                    list.Add("--biz");
                    list.Add(biz!);
                }
                if (!string.IsNullOrWhiteSpace(pid))
                {
                    list.Add("--pid");
                    list.Add(pid!);
                }
                return [.. list];
            }
            case "test":
                return ["test"];
            default:
                return args;
        }
    }

||||||| /tmp/_base
    private async Task<bool> HandleStartupAsync(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder().AddCommandLine(args).Build();
        if (args[0].ToLower() is "rpc")
        {
            RpcRunner.Run(args);
            Environment.Exit(0);
        }
        if (args[0].ToLower() is "playtime")
        {
            int pid = config.GetValue<int>("pid");
            GameBiz biz = (GameBiz)config.GetValue<string>("biz");
            if (pid > 0)
            {
                var playtime = AppConfig.GetService<Features.PlayTime.PlayTimeRecordService>();
                await playtime.LogPlayTimeAsync(biz, pid);
            }
            Environment.Exit(0);
        }

        if (args[0].ToLower() is "startgame")
        {
            GameBiz biz = (GameBiz)config.GetValue<string>("biz");
            GameId? gameId = GameId.FromGameBiz(biz);
            if (gameId is not null)
            {
                await AppConfig.GetService<GameLauncherService>().StartGameAsync(gameId);
            }
            Environment.Exit(0);
        }

        if (args[0].ToLower().StartsWith("starward://"))
        {
            if (await UrlProtocolService.HandleUrlProtocolAsync(args[0]))
            {
                Environment.Exit(0);
            }
        }

        return false;
    }

=======
>>>>>>> /tmp/_theirs

    private MainWindow m_MainWindow;

    private SystemTrayWindow m_SystemTrayWindow;



    public void EnsureMainWindow()
    {
        m_MainWindow ??= new MainWindow();
        m_MainWindow.Activate();
        m_MainWindow.Show();
    }


    /// <summary>
    /// 启动 Linux 托盘，并在失败时明确告知用户「关闭窗口 = 退出」（C1）。
    /// 没有托盘宿主的会话（例如某些纯净的 Wayland 合成器）里，把窗口藏起来就再也叫不回来。
    /// </summary>
    private static async Task StartTrayAsync()
    {
        bool available = await Features.Tray.StatusNotifierTray.StartAsync(AppConfig.GetLogger<App>());
        if (available)
        {
            return;
        }
        Serilog.Log.Warning("系统托盘不可用：后续关闭窗口将直接退出应用");
        for (int i = 0; i < 40; i++)
        {
            await Task.Delay(250);
            MainWindow? window = Features.ViewHost.MainWindow.Current;
            if (window is not null)
            {
                window.DispatcherQueue.TryEnqueue(() =>
                    Helpers.InAppToast.MainWindow?.Warning(
                        "当前桌面没有系统托盘，关闭窗口会直接退出应用。", null, 8000));
                return;
            }
        }
    }


    public void EnsureSystemTray()
    {
        // Lunarward：Linux 托盘走 org.kde.StatusNotifierItem（StatusNotifierTray），
        // 不再创建上游的 SystemTrayWindow（其 H.NotifyIcon 依赖在 Linux 是占位控件，
        // 会卡住主窗口初始化，见 docs/linux-port/12-shims-and-gaps.md）。
        if (!OperatingSystem.IsWindows())
        {
            _ = StartTrayAsync();
            // 退出时摘掉 D-Bus 注册，否则托盘区会留一个点不动的死图标（L-62）
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Features.Tray.StatusNotifierTray.Stop();
            return;
        }
        m_SystemTrayWindow ??= new SystemTrayWindow();
    }



    private static FileStream? _instanceLock;


    /// <summary>单实例锁：~/.local/state/lunarward/lunarward.lock（D6 之后会换成 Unix socket 以支持参数转发）。</summary>
    internal static string SingleInstanceLockPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state", "lunarward", "lunarward.lock");


    internal static bool TryAcquireSingleInstanceLock()
    {
<<<<<<< /tmp/_ours
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state", "lunarward");
            Directory.CreateDirectory(dir);
            _instanceLock = new FileStream(SingleInstanceLockPath(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
||||||| /tmp/_base
        _uiDispatcherQueue.TryEnqueue(EnsureMainWindow);
=======
        try
        {
            _uiDispatcherQueue.TryEnqueue(EnsureMainWindow);
            if (e.Data is ILaunchActivatedEventArgs { Arguments: string arguments })
            {
                string?[] args = Shell32.CommandLineToArgvW(arguments);
                if (args.Length > 1)
                {
                    string? url = args[1];
                    if (url?.StartsWith("starward-hyp-login://") ?? false)
                    {
                        WeakReferenceMessenger.Default.Send(new HypThirdPartyLoginMessage(url));
                    }
                }
            }
        }
        catch { }
>>>>>>> /tmp/_theirs
    }


    internal static void TryReleaseSingleInstanceLock()
    {
        try
        {
            _instanceLock?.Dispose();
        }
        catch { }
        _instanceLock = null;
    }


    public static object? FindInstanceForKey(string key) => null;



    public async void Exit(bool quickExit = false)
    {
        m_MainWindow?.Close();
        m_SystemTrayWindow?.Close();
        GamepadController.RestoreGamepadGuideButtonForGameBar();
        if (!quickExit)
        {
            try
            {
                var timer = new Timer(30_000);
                timer.Elapsed += (_, _) => Environment.Exit(0);
                timer.Start();
                AppInstance.GetCurrent().UnregisterKey();
                DatabaseService.AutoBackupToAppDataLocal();
            }
            catch { }
        }
        Application.Current.Exit();
    }




    /// <summary>用半透明画刷替换 AcrylicBrush（Uno 只实现 FallbackColor，会盖住壁纸）。</summary>
    private static void ApplyAcrylicFallback()
    {
        try
        {
            Application.Current.Resources["CustomAcrylicBrush"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xD0, 0x1C, 0x1C, 0x20));
            Application.Current.Resources["CustomOverlayAcrylicBrush"] = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(0xA8, 0x10, 0x10, 0x12));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[background] replace acrylic brush failed");
        }
    }

}
