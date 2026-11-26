using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Xaml;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Database;
using Starward.Features.GameLauncher;
using Starward.Features.GamepadControl;
using Starward.Features.Passport;
using Starward.Features.ViewHost;
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using Vanara.PInvoke;
using Windows.ApplicationModel.Activation;


namespace Starward;

public partial class App : Application
{

    private readonly DispatcherQueue _uiDispatcherQueue;

    /// <summary>Lunarward：低频强制 GC，压住视频壁纸 + 大图缓存长期驻留的内存峰值。</summary>
    private readonly Timer _gcTimer = new(TimeSpan.FromMinutes(10));


    public static new App Current => (App)Application.Current;


    public App()
    {
        this.InitializeComponent();
        RequestedTheme = ApplicationTheme.Dark;
        _uiDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        UnhandledException += App_UnhandledException;
        _gcTimer.Elapsed += (_, _) => GC.Collect();
        _gcTimer.Start();
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

        if (AppConfig.UserDataFolder is null)
        {
            // 自检模式（CI 的 xvfb）没有人和欢迎页交互，直接按默认配置继续；
            // 否则进程会卡在 WelcomeWindow 的 WaitAsync 上，`--selftest-ui` 永远等不到主窗口（L-185）。
            if (Environment.GetEnvironmentVariable("LUNARWARD_SELFTEST") is "1")
            {
                AppConfig.LoadConfiguration();
            }
            else if (await new WelcomeWindow().WaitAsync())
            {
                AppConfig.LoadConfiguration();
            }
            else
            {
                Environment.Exit(0);
            }
        }

        // Lunarward：游戏启动后拉起「伴随启动」的后启动程序（D24/D28）
        Features.Companion.CompanionAutoStart.Register(AppConfig.GetService<Features.Companion.CompanionRunner>(), AppConfig.GetLogger<App>());
        // LUNARWARD-DEVIATION: 上游这里用 AppInstance.GetCurrent() 重定向单实例激活，Uno 上没有该实现，
        // Linux 单实例由下面的 SingleInstancePipe（文件锁 + Unix socket）负责（D6）。
        // 亚克力画刷的替代（Uno 只把 AcrylicBrush 实现成 FallbackColor，会把壁纸盖住）见 App.xaml 的
        // LunarwardSurfaceBrush 与 D31 / M3。
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
                // 0.19.0 起当前游戏用 GameId 记（AppConfig.CurrentGameId 是 string?），GameBiz 已过时（D128）。
                AppConfig.CurrentGameId = debugGame;
            }
        }

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
            onStartGame: payload => OnUi(() =>
            {
                // D138/F-25：payload 是 `<biz>` 或 `<biz>\t<install_path>`；install_path 来自深链查询串，
                // 第三方启动器（如 umu / Steam 快捷方式）靠它指定非默认安装目录。
                string[] parts = payload.Split('\t', 2);
                string bizName = parts[0].Trim();
                string? launchInstallPath = parts.Length > 1 && parts[1].Length > 0 ? parts[1] : null;
                Serilog.Log.Information("Second instance asked to start game {biz} (install path: {path})", bizName, launchInstallPath ?? "(default)");
                if (GameBiz.TryParse(bizName, out GameBiz biz) && GameId.FromGameBiz(biz) is GameId gameId)
                {
                    EnsureMainWindow();
                    // R39：以前把 Task 直接丢掉，预检失败/启动抛异常都没有任何反馈。
                    async Task LaunchAsync()
                    {
                        try
                        {
                            await AppConfig.GetService<GameLauncherService>().StartGameAsync(gameId, launchInstallPath);
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
        // D138/F-25：上游把 game_biz 放在路径里（`starward://startgame/hk4e_cn?install_path=…`），
        // 移植侧以前只认 `?biz=`，于是第三方启动器拼的深链会被当成「没有 biz」而被静默丢弃。
        string? biz = uri.AbsolutePath.Trim('/') is { Length: > 0 } pathBiz ? Uri.UnescapeDataString(pathBiz) : null;
        string? pid = null;
        string? installPath = null;
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
            else if (name.Equals("install_path", StringComparison.OrdinalIgnoreCase))
            {
                installPath = value;
            }
        }
        // B57：上游支持 startgame / playtime / test 三种 host，这里把 query 展开成内部参数。
        switch (host)
        {
            case "startgame":
            {
                List<string> list = ["startgame"];
                if (!string.IsNullOrWhiteSpace(biz))
                {
                    list.Add("--biz");
                    list.Add(biz!);
                }
                if (!string.IsNullOrWhiteSpace(installPath))
                {
                    list.Add("--install-path");
                    list.Add(installPath!);
                }
                return [.. list];
            }
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
            // D135：上游靠 WM_WTSSESSION_CHANGE 通知锁屏，Linux 改订阅 logind 的 Session.Lock/Unlock
            Helpers.SessionLockWatcher.Start(AppConfig.GetLogger<App>());
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
                // Lunarward：Linux 的单实例是文件锁 + Unix socket，退出时由 SingleInstancePipe 自己清理；
                // 上游这里用 AppInstance.GetCurrent().UnregisterKey()，Uno 上没有该实现（D128）。
                DatabaseService.AutoBackupToAppDataLocal();
            }
            catch { }
        }
        Application.Current.Exit();
    }





}
