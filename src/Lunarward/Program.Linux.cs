using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
// Lunarward：Linux 桌面入口点。
// 上游 Program.cs 的 Main 被 `#if DISABLE_XAML_GENERATED_MAIN` 包着且是 WinUI 专有启动方式，
// 在 Uno 上不适用；这里按 Uno 单项目模板提供 UnoPlatformHostBuilder 入口。
using Uno.UI.Hosting;

namespace Starward;

internal class LunarwardProgram
{
    [STAThread]
    public static void Main(string[] args)
    {
        // B44：后台线程与启动早期的崩溃也要落盘（上游 Program.cs 注册了同一个处理器）。
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            try
            {
                string text = $"[UnhandledException] {DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{e.ExceptionObject}{Environment.NewLine}";
                string? logFile = null;
                try
                {
                    logFile = Starward.AppConfig.LogFile;
                }
                catch
                {
                }
                if (!string.IsNullOrWhiteSpace(logFile))
                {
                    File.AppendAllText(logFile!, text);
                }
                Console.Error.WriteLine(text);
            }
            catch
            {
            }
        };
// 无副作用查询（不需要 DISPLAY）：`lunarward --version` / `--help`
        string appVersion = Starward.AppConfig.AppVersion;
        if (string.IsNullOrWhiteSpace(appVersion))
        {
            appVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
        }
        if (args.Length >= 1 && args[0] is "--version" or "-v")
        {
            Console.WriteLine($"Lunarward {appVersion}");
            return;
        }
        if (args.Length >= 1 && args[0] is "--help" or "-h")
        {
            // 帮助文本唯一来源是 Helpers/CliSpec.cs（D79）；lint-cli-help.sh 卡住「源码里的开关 ⊆ 这张表」。
            Console.WriteLine(Helpers.CliSpec.RenderHelp($"版本 {appVersion}"));
            return;
        }
        // 未知开关：只认第一 token（位置参数是游戏自己的参数，不能拦），直接 exit 2 并给最接近的候选。
        if (Helpers.CliSpec.TryRejectUnknown(args, out int unknownExitCode, out string unknownMessage))
        {
            Console.Error.WriteLine(unknownMessage);
            Environment.ExitCode = unknownExitCode;
            return;
        }

        // 0.19.0 整体删除了移植侧自写的 Sophon 管线（Starward.Features.GameInstall.Sophon），
        // 只服务 --install-chunk-* 的 LinkOrCopy / LocalCopyMatches / GetBuildForTest 随之删除；
        // 安装/更新改由上游 Starward.GameInstall.GameInstallRunner（CLI 走 `--install-task`）承担。

        // 体积打印：上游没有现成格式化函数，CLI 里统一在这里转一次。
        static string FormatSize(long bytes)
        {
            string[] units = new[] { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }
            return unit == 0 ? $"{bytes} B" : $"{value:F2} {units[unit]}";
        }

        // 环境检查：移植侧是 async 的 `CheckEnviromentAsync()`（Linux 要异步弹 NoPermissionWindow），
        // 上游 0.19.0 是同步 `CheckEnviroment()`；这里按移植侧版本直接调（LUNARWARD-DEVIATION，D128）。
        static void InitEnviroment() => AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();


        // `--profile-ensure` 不带 biz 时的缺省值：0.19.0 删了 AppConfig.CurrentGameBiz（Obsolete 且当真），
        // 只剩 CurrentGameId（HoYoPlay 的 GameId.Id），反查交给 Helpers.GameIdHelper。
        static Starward.Core.GameBiz? GetCurrentGameBiz() => Helpers.GameIdHelper.CurrentGameBiz();


        // 上游的 `rpc` 子命令（Program.cs → RpcRunner）在移植侧没有对应实现（M4 占位，D6）。
        // 这里显式拒掉，避免 `lunarward rpc` 被当成「没有参数」去开主窗口（D134）。
        if (args.Length >= 1 && args[0].ToLowerInvariant() is "rpc")
        {
            Console.Error.WriteLine("rpc 子进程在 Linux 上未实现（M4 占位）。");
            Environment.ExitCode = 2;
            return;
        }

        // 深链启动游戏（对齐上游 `src/Starward/Program.cs:86`：`HandleUrlProtocolAsync` 直接起进程后退出）。
        // 主实例没跑时，桌面环境会把 `lunarward://startgame/...` 交给本进程；以前这条路径什么都不做。
        if (args.Length >= 1 && args[0].ToLowerInvariant() is "startgame")
        {
            InitEnviroment();
            string startBiz = "";
            string? startInstallPath = null;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] is "--biz" && i + 1 < args.Length)
                {
                    startBiz = args[++i];
                }
                else if (args[i] is "--install-path" && i + 1 < args.Length)
                {
                    startInstallPath = args[++i];
                }
            }
            if (!Starward.Core.GameBiz.TryParse(startBiz, out Starward.Core.GameBiz startBizValue)
                || Starward.Core.HoYoPlay.GameId.FromGameBiz(startBizValue) is not { } startGameId)
            {
                Console.Error.WriteLine($"未知游戏：{startBiz}");
                Environment.ExitCode = 1;
                return;
            }
            try
            {
                AppConfig.GetService<Starward.Features.GameLauncher.GameLauncherService>()
                    .StartGameAsync(startGameId, startInstallPath).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "startgame from url failed: {game}", startGameId.Id);
                Console.Error.WriteLine($"启动失败：{ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // 游玩时长记账（对齐上游 `src/Starward/Program.cs:48` 的 playtime 分支）：
        // 云游戏按钮会以 `lunarward playtime --biz hk4e_cn --pid <pid>` 拉起本程序，
        // 这里只记账然后退出——不走 GUI，否则会多开一个主窗口（D134）。
        if (args.Length >= 1 && args[0].ToLowerInvariant() is "playtime")
        {
            InitEnviroment();
            // 手写解析即可：只有两个开关，不为它拉 ConfigurationBuilder（AddCommandLine 要额外 using）。
            string playtimeBiz = "";
            int playtimePid = 0;
            for (int i = 1; i < args.Length; i++)
            {
                if (args[i] is "--biz" && i + 1 < args.Length)
                {
                    playtimeBiz = args[++i];
                }
                else if (args[i] is "--pid" && i + 1 < args.Length && int.TryParse(args[++i], out int parsedPid))
                {
                    playtimePid = parsedPid;
                }
            }
            if (playtimePid > 0 && Starward.Core.GameBiz.TryParse(playtimeBiz, out Starward.Core.GameBiz playBiz))
            {
                AppConfig.GetService<Features.PlayTime.PlayTimeRecordService>().LogPlayTimeAsync(playBiz, playtimePid).GetAwaiter().GetResult();
            }
            return;
        }

        // 登录助手（D76）：登录窗是独立进程，实现换成「本程序 + WebKitGTK 6.0」，不再依赖 python3。
        if (args.Length >= 1 && args[0] is "--webkit-login")
        {
            if (!Features.GameRecord.WebKitLoginHost.IsAvailable())
            {
                Console.Error.WriteLine("没有找到 libwebkitgtk-6.0（Arch 上装 webkitgtk-6.0）。");
                Environment.ExitCode = 1;
                return;
            }
            Environment.ExitCode = Features.GameRecord.WebKitLoginHost.Run(args[1..]);
            return;
        }

        // 只解析不启动：`lunarward --shell-plan` 打印每个 Profile 的调试 Shell 命令行与环境变量
        if (args.Length >= 1 && args[0] is "--shell-plan")
        {
            // R50：--game <biz> 让 Shell 的 WINEPREFIX 与真实启动一致（否则落进 ~/.wine）
            int shellGameIndex = System.Array.IndexOf(args, "--game");
            string? shellGameKey = shellGameIndex > 0 && shellGameIndex + 1 < args.Length ? args[shellGameIndex + 1] : null;
            foreach (Features.Runtime.RuntimeProfile profile in Features.Runtime.RuntimeProfileStore.LoadAll())
            {
                System.Diagnostics.ProcessStartInfo? info = Features.Runtime.ProfileShellService.CreateShellStartInfo(profile, shellGameKey);
                Console.WriteLine(info is null
                    ? $"{profile.Name}: <no terminal emulator found>"
                    : $"{profile.Name}: {info.FileName} {string.Join(' ', info.ArgumentList)} | WINEPREFIX={GetEnv(info, "WINEPREFIX")} LUNARWARD_WINE={GetEnv(info, "LUNARWARD_WINE")} profile={GetEnv(info, "LUNARWARD_PROFILE")}");
            }
            return;
        }

        // 只解析不启动：`lunarward --launch-plan /path/to/Game.exe "arg1 arg2"`
        // 用来看清 wine / prefix / argv 的解析结果（等价于「启动前的最后一公里」自检）。
        if (args.Length >= 2 && args[0] is "--launch-plan")
        {
            // 解析失败要能被脚本看见：不存在就返回 1，而不是打印一行 exists: False 然后 0 退出（L2 用例发现）
            if (!System.IO.File.Exists(args[1]))
            {
                Console.WriteLine($"找不到可执行文件：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            string? hint = null;
            int planIdx = Array.IndexOf(args, "--profile");
            if (planIdx > 0 && planIdx + 1 < args.Length)
            {
                hint = args[planIdx + 1];
            }
            Console.WriteLine(Features.GameLauncher.GameLauncherService.DescribeLaunchPlan(args[1], args.Length > 2 && args[2] is not "--profile" ? args[2] : null, hint));
            return;
        }

        // 绑定兜底：`lunarward --profile-ensure [biz]`，等价于「首次启动这个游戏」时的环境解析（D73 §5）。
        // 真启动里同样的逻辑由 GameLauncherService 走；这里只是让脚本能观测「绑到了谁 / 有没有新建」。
        if (args.Length >= 1 && args[0] is "--profile-ensure")
        {
            AppConfig.LoadConfiguration();
            Starward.Core.GameBiz? ensureBiz = args.Length > 1 ? new Starward.Core.GameBiz(args[1]) : GetCurrentGameBiz();
            if (ensureBiz is null)
            {
                Console.WriteLine("没有指名游戏，也没有当前游戏：请用 `--profile-ensure <biz>`（biz 列表见 --help）。");
                Environment.ExitCode = 1;
                return;
            }
            (Features.Runtime.RuntimeProfile? ensured, bool created, bool newlyBound) = Features.Runtime.ProfileBinding.EnsureFor(ensureBiz.Value);
            if (ensured is null)
            {
                Console.WriteLine("没有可用的运行环境，也没有可复制的模板。");
                Environment.ExitCode = 1;
                return;
            }
            Console.WriteLine($"biz     : {ensureBiz.Value}");
            Console.WriteLine($"配方    : {ensured.Name} ({ensured.Id})");
            Console.WriteLine($"created : {created}");
            Console.WriteLine($"bound   : {newlyBound}");
            Console.WriteLine($"prefix  : {(string.IsNullOrWhiteSpace(ensured.PrefixPath) ? "(启动时创建)" : ensured.PrefixPath)}");
            return;
        }

        // 截图（D31）：`lunarward --screenshot`，打印落盘路径。`--capture-test` 是同义旧名。
        if (args.Length >= 1 && args[0] is "--screenshot")
        {
            string? file = Features.Screenshot.ScreenCaptureLinux.CaptureAsync(notify: false).GetAwaiter().GetResult();
            Console.WriteLine(file is null ? "截图失败：没有可用的截图工具" : file);
            if (file is null)
            {
                Environment.ExitCode = 1;
            }
            return;
        }

        // 检查更新（D5，AUR）：`lunarward --update-check`，只查不装。
        if (args.Length >= 1 && args[0] is "--update-check")
        {
            AppConfig.LoadConfiguration();
            string? latest = Features.Update.UpdateService.GetPackageVersionAsync().GetAwaiter().GetResult();
            string current = string.IsNullOrWhiteSpace(AppConfig.AppVersion)
                ? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0"
                : AppConfig.AppVersion;
            if (latest is null)
            {
                Console.WriteLine($"当前版本 {current}；未找到 AUR 包（或网络不可用），按「无更新」处理。");
            }
            else
            {
                Console.WriteLine($"当前版本 {current}；AUR 最新 {latest}。");
            }
            return;
        }

        // 查看今日的进程输出（D27）：`lunarward --output-tail [n]`，读的是落盘日志，跨进程可见。
        if (args.Length >= 1 && args[0] is "--output-tail")
        {
            int count = args.Length >= 2 && int.TryParse(args[1], out int n) ? Math.Clamp(n, 1, 5000) : 50;
            string file = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "lunarward", "logs", "output",
                $"{DateTime.Now:yyyyMMdd}.log");
            if (!File.Exists(file))
            {
                Console.WriteLine("（今天还没有捕获到输出）");
                return;
            }
            foreach (string line in File.ReadLines(file).TakeLast(count))
            {
                Console.WriteLine(line);
            }
            return;
        }

        // 批量转换截图：`lunarward --convert-screenshots [folder]`
        if (args.Length >= 1 && args[0] is "--convert-screenshots")
        {
            string folder = args.Length >= 2 ? args[1] : Features.Screenshot.ScreenCaptureLinux.DefaultFolder;
            var files = Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder).Where(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase)).ToList()
                : new List<string>();
            var (ok, skip, failed) = Features.Screenshot.ScreenshotConverter.ConvertToJpeg(files);
            Console.WriteLine($"folder={folder} ok={ok} skip={skip} failed={failed}");
            return;
        }

        // 只解析不执行：`lunarward --url-plan "lunarward://startgame?biz=hk4e_cn"`，打印深链被展开成什么参数。
        if (args.Length >= 2 && args[0] is "--url-plan")
        {
            Console.WriteLine(string.Join(' ', App.ExpandUrlArgument(new[] { args[1] })));
            return;
        }

        // 只解析不启动：`lunarward --companion-plan <gameKey>`，打印每个后启动程序最终的命令行与环境。
        if (args.Length >= 2 && args[0] is "--companion-plan")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz biz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            var runner = new Features.Companion.CompanionRunner(Microsoft.Extensions.Logging.Abstractions.NullLogger<Features.Companion.CompanionRunner>.Instance);
            foreach (var entry in Features.Companion.CompanionStore.Load(biz))
            {
                var info = runner.CreateStartInfo(entry);
                Console.WriteLine($"[{entry.Id}] {entry.Name} companion={entry.Companion} enabled={entry.Enabled} trigger={entry.Trigger} delay={entry.DelayMs}ms");
                Console.WriteLine($"    cmd: {info.FileName} {string.Join(' ', info.ArgumentList)}");
                Console.WriteLine($"    dir: {info.WorkingDirectory}");
                Console.WriteLine($"    env: WINEPREFIX={GetEnv(info, "WINEPREFIX")} LUNARWARD_WINE={GetEnv(info, "LUNARWARD_WINE")} profile={GetEnv(info, "LUNARWARD_PROFILE")} companion={GetEnv(info, "LUNARWARD_COMPANION")}");
            }
            return;
        }

        // 真正启动一条：`lunarward --companion-run <gameKey> <id>`（排错用，正常走 UI）
        if (args.Length >= 3 && args[0] is "--companion-run")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz runBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            var runner = new Features.Companion.CompanionRunner(Microsoft.Extensions.Logging.Abstractions.NullLogger<Features.Companion.CompanionRunner>.Instance);
            var entry = Features.Companion.CompanionStore.Load(runBiz).FirstOrDefault(x => x.Id == args[2]);
            if (entry is null)
            {
                Console.WriteLine($"找不到条目：{args[2]}");
                Environment.ExitCode = 1;
                return;
            }
            try
            {
                var process = runner.StartEntry(runBiz, entry);
                Console.WriteLine($"已启动 {entry.Name} pid={process?.Id}");
                // 排错用途：等它退出一小会儿，好让输出捕获把 stdout/stderr 读干净（D27）。
                process?.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"启动失败：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // URL 协议（x-scheme-handler/lunarward）：供打包脚本与排查使用
        if (args.Length >= 1 && args[0] is "--register-protocol" or "--unregister-protocol" or "--protocol-status")
        {
            switch (args[0])
            {
                case "--register-protocol":
                    Starward.Features.UrlProtocol.UrlProtocolService.RegisterProtocol();
                    Console.WriteLine("已注册 x-scheme-handler/lunarward");
                    break;
                case "--unregister-protocol":
                    Starward.Features.UrlProtocol.UrlProtocolService.UnregisterProtocol();
                    Console.WriteLine("已取消注册");
                    break;
                default:
                    Console.WriteLine(Starward.Features.UrlProtocol.UrlProtocolService.IsProtocolRegistered() ? "已注册" : "未注册");
                    break;
            }
            return;
        }

        // 运行器自检：`lunarward --runner-check <winePath>`（只跑 wine --version 与 wine cmd /c echo，不启动游戏）
        if (args.Length >= 2 && args[0] is "--runner-check")
        {
            string winePath = args[1];
            string prefix = Environment.GetEnvironmentVariable("LUNARWARD_WINEPREFIX")
                ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wine");
            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = winePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            info.ArgumentList.Add("--version");
            info.Environment["WINEPREFIX"] = prefix;
            info.Environment["WINEDEBUG"] = "-all";
            Starward.Features.GameLauncher.GameLauncherService.ApplyWineRuntimeEnvironment(info, winePath);
            Console.WriteLine($"prefix: {prefix}");
            foreach (string key in new[] { "WINEDLLPATH", "LD_LIBRARY_PATH", "PROTONPATH", "STEAM_COMPAT_CLIENT_INSTALL_PATH" })
            {
                string value = info.Environment.TryGetValue(key, out string? v) ? v : "(未设置)";
                Console.WriteLine($"{key}={value}");
            }
            using (var proc = System.Diagnostics.Process.Start(info)!)
            {
                string stdout = proc.StandardOutput.ReadToEnd().Trim();
                string stderr = proc.StandardError.ReadToEnd().Trim();
                proc.WaitForExit(20000);
                Console.WriteLine($"wine --version -> [{proc.ExitCode}] {stdout} {stderr}");
            }
            var echo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = winePath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            echo.ArgumentList.Add("cmd");
            echo.ArgumentList.Add("/c");
            echo.ArgumentList.Add("echo lunarward-ok");
            echo.Environment["WINEPREFIX"] = prefix;
            echo.Environment["WINEDEBUG"] = "-all";
            Starward.Features.GameLauncher.GameLauncherService.ApplyWineRuntimeEnvironment(echo, winePath);
            using (var proc = System.Diagnostics.Process.Start(echo)!)
            {
                string stdout = proc.StandardOutput.ReadToEnd().Trim();
                string stderr = proc.StandardError.ReadToEnd().Trim();
                proc.WaitForExit(60000);
                Console.WriteLine($"wine cmd /c echo -> [{proc.ExitCode}] {stdout} {stderr}");
            }
            return;
        }

        // 导入图形组件：`lunarward --graphics-import <dir>`
        if (args.Length >= 2 && args[0] is "--graphics-import")
        {
            string source = args[1];
            bool isDxvk = System.IO.File.Exists(System.IO.Path.Combine(source, "x64", "d3d11.dll"));
            bool isVkd3d = !isDxvk && System.IO.File.Exists(System.IO.Path.Combine(source, "x64", "d3d12.dll"));
            if (!isDxvk && !isVkd3d)
            {
                Console.WriteLine("目录里没有 x64/d3d11.dll（DXVK）或 x64/d3d12.dll（VKD3D）");
                return;
            }
            string kind = isDxvk ? "dxvk" : "vkd3d-proton";
            string name = System.IO.Path.GetFileName(source.TrimEnd('/'));
            string target = System.IO.Path.Combine(Starward.Features.Runtime.ComponentStore.ComponentsFolder, kind, name);
            foreach (string file in System.IO.Directory.EnumerateFiles(source, "*", System.IO.SearchOption.AllDirectories))
            {
                string dest = System.IO.Path.Combine(target, System.IO.Path.GetRelativePath(source, file));
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
                if (!System.IO.File.Exists(dest))
                {
                    System.IO.File.Copy(file, dest);
                }
            }
            Console.WriteLine($"已导入 {kind} → {target}");
            return;
        }

        // `--install-run` / `--install-fetch` 已删除：它们直接驱动移植侧自写的 Sophon 下载管线
        // （InstallDownloadRunner，支持「只下单个文件」「限 N MB 就停」「--parallel」「--target」），
        // 0.19.0 把安装/更新整体收进上游 Starward.GameInstall.GameInstallRunner，没有这些细粒度开关。
        // 现在要真下载请用 `--install-task <gameKey> [--predownload] [--run-seconds N]`。

#if DEBUG
        // 开发期：不经过鼠标，直接跑一次安装/更新并按 UI 读取的字段打印进度（真机联调用，Release 不含）。
        // 用法：--dev-install-probe <gameKey> [--pause-after N] [--op-repair]
        // 0.19.0 起任务由进程内 facade Starward.Features.GameInstall.GameInstallService 驱动（RPC 服务已删除），
        // 进度字段换成上游的 GameInstallProgress（网络/写入字节、PercentProgress、NetworkDownloadSpeed）。
        if (args.Length >= 2 && args[0] is "--dev-install-probe")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz probeBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            InitEnviroment();
            Starward.Core.HoYoPlay.GameId? probeGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(probeBiz);
            if (probeGameId is null)
            {
                Console.WriteLine($"这个游戏没有对应的 HoYoPlay 条目：{probeBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            string? probePath = Features.GameLauncher.GameLauncherService.GetGameInstallPath(probeGameId);
            if (string.IsNullOrEmpty(probePath))
            {
                Console.WriteLine("这个游戏没有配置安装路径");
                return;
            }
            Starward.Features.GameInstall.GameInstallService service = AppConfig.GetService<Starward.Features.GameInstall.GameInstallService>();
            bool probeRepair = args.Contains("--op-repair");
            var probeTask = (probeRepair
                                ? service.StartRepairAsync(probeGameId, probePath, default)
                                : service.StartUpdateAsync(probeGameId, probePath, default))
                           .GetAwaiter()
                           .GetResult();
            if (probeTask is null)
            {
                Console.WriteLine("任务没能启动");
                return;
            }
            Console.WriteLine($"state={probeTask.State} path={probePath} ref={System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(probeTask)} same={ReferenceEquals(probeTask, service.GetGameInstallTask(probeGameId))}");
            int pauseAfter = -1;
            int pauseIndex = Array.IndexOf(args, "--pause-after");
            if (pauseIndex >= 0 && pauseIndex + 1 < args.Length && int.TryParse(args[pauseIndex + 1], out int parsedPause))
            {
                pauseAfter = parsedPause;
            }
            bool paused = false;
            for (int i = 0; i < 600; i++)
            {
                Thread.Sleep(500);
                // 进度对象是快照，必须每轮重新取一次实时任务
                Starward.GameInstall.GameInstallProgress live = service.GetGameInstallTask(probeGameId) ?? probeTask;
                Console.WriteLine($"state={live.State} step={live.Step} {live.NetworkFinishedBytes}/{live.NetworkTotalBytes} B (网络 {live.PercentProgress:P1}) 磁盘 {live.StorageFinishedBytes}/{live.StorageTotalBytes} B speed={live.NetworkDownloadSpeed / 1048576.0:F2}MB/s remain={live.RemainTimeSeconds}s err={live.ErrorMessage}");
                if (pauseAfter >= 0 && !paused && live.NetworkFinishedBytes > pauseAfter * 1048576L)
                {
                    service.PauseTaskAsync(live).GetAwaiter().GetResult();
                    paused = true;
                    Thread.Sleep(2000);
                    Console.WriteLine($"paused state={service.GetGameInstallTask(probeGameId)?.State}");
                    service.ContinueTaskAsync(live).GetAwaiter().GetResult();
                    Console.WriteLine($"continued state={service.GetGameInstallTask(probeGameId)?.State}");
                }
                if (live.State is Starward.GameInstall.RunnerState.Finish or Starward.GameInstall.RunnerState.Stop or Starward.GameInstall.RunnerState.Error or Starward.GameInstall.RunnerState.Paused)
                {
                    break;
                }
            }
            return;
        }

        // 开发期：把某个游戏的安装路径写进配置（用于真机联调，Release 不含）。
        if (args.Length >= 3 && args[0] is "--dev-set-install-path" && Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz devBiz))
        {
            InitEnviroment();
            Starward.Core.HoYoPlay.GameId? devGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(devBiz);
            if (devGameId is null)
            {
                Console.WriteLine($"这个游戏没有对应的 HoYoPlay 条目：{devBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            try
            {
                // 0.19.0 删除了 AppConfig.SetGameInstallPath（Obsolete(true)），安装路径改存进游戏条目
                Starward.Features.GameLauncher.GameLauncherService launcher = AppConfig.GetService<Starward.Features.GameLauncher.GameLauncherService>();
                launcher.ChangeGameInstallPath(devGameId, args[2]);
                AppConfig.SaveConfiguration();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入失败：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
                return;
            }
            Console.WriteLine($"install_path_{devBiz}={Features.GameLauncher.GameLauncherService.GetGameInstallPath(devGameId)}");
            return;
        }
#endif
        // `--install-chunk-plan` / `--install-chunk-run` 已删除：块级复用估算与实测都建立在移植侧自写的
        // Sophon 链路上（ChunkReusePlanner / InstallDownloadRunner），0.19.0 上游没有对应实现。
        // 想看体积请用 `--install-plan` / `--predownload-size`（改走官方 chunk 清单统计）。
        // 更新估算（只读，不下载）：`lunarward --update-plan <gameKey> [--predownload] [--local <ver>] [--install-dir <目录>]`
        // 0.19.0 上游没有独立「计划器」，这里只按官方接口算：目标版本、整包体积、以及从本地版本起的差分包体积；
        // 真正下载由安装器按块复用决定（`--install-task`）。
        if (args.Length >= 2 && args[0] is "--update-plan")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz upBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            InitEnviroment();
            Starward.Core.HoYoPlay.GameId? upGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(upBiz);
            if (upGameId is null)
            {
                Console.WriteLine($"这个游戏没有对应的 HoYoPlay 条目：{upBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            string upInstall = Features.GameLauncher.GameLauncherService.GetGameInstallPath(upGameId)
                               ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", upBiz.Value);
            int upDir = Array.IndexOf(args, "--install-dir");
            if (upDir >= 0 && upDir + 1 < args.Length)
            {
                upInstall = args[upDir + 1];
            }
            bool upPreDownload = args.Contains("--predownload");
            var upClock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // 取数与上游安装/预下载对话框一致：branch → 场景/语音 → 本地版本 → 官方 chunk 清单
                Starward.GameInstall.HoYoPlayService upHoYoPlay = AppConfig.GetService<Starward.GameInstall.HoYoPlayService>();
                Starward.GameInstall.GameConfigService upConfig = AppConfig.GetService<Starward.GameInstall.GameConfigService>();
                Starward.GameInstall.GameResourceService upResource = AppConfig.GetService<Starward.GameInstall.GameResourceService>();
                Starward.Core.HoYoPlay.GameBranch? upBranch = upHoYoPlay.GetGameBranchAsync(upGameId, default).GetAwaiter().GetResult();
                if (upBranch is null)
                {
                    Console.WriteLine("官方接口没有返回 branch 信息（离线或接口变更）");
                    Environment.ExitCode = 1;
                    return;
                }
                Starward.Core.HoYoPlay.GameBranchPackage? upPackage = upPreDownload ? upBranch.PreDownload : upBranch.Main;
                if (upPackage is null)
                {
                    Console.WriteLine("当前没有预下载（该版本可能未开放预下载）");
                    Environment.ExitCode = 1;
                    return;
                }
                Starward.Core.AudioLanguage upAudio = upConfig.GetAudioLanguageAsync(upGameId, upInstall, default).GetAwaiter().GetResult();
                string upScenario = upConfig.GetCategoryScenarioAsync(upGameId, upInstall, default).GetAwaiter().GetResult() ?? Starward.Core.HoYoPlay.CategoryScenario.CATEGORY_SCENARIO_FULL;
                int upLocalIndex = Array.IndexOf(args, "--local");
                string? upLocal = upLocalIndex >= 0 && upLocalIndex + 1 < args.Length
                                      ? args[upLocalIndex + 1]
                                      : Starward.GameInstall.GameConfigService.GetGameConfigIniAsync(upInstall, default).GetAwaiter().GetResult()?.GameVersion?.ToString();
                Console.WriteLine($"安装目录：{upInstall}");
                Console.WriteLine($"本地版本：{upLocal ?? "(未知)"}  目标版本：{upPackage.Tag}  语音：{upAudio}  场景：{upScenario}");
                Starward.Core.HoYoPlay.GameSophonChunkBuild? upBuild = upHoYoPlay.GetGameSophonChunkBuildAsync(upGameId, upPackage, null, default).GetAwaiter().GetResult();
                if (upBuild is null)
                {
                    Console.WriteLine("清单不可用（官方接口没有返回 chunk build）");
                    Environment.ExitCode = 1;
                    return;
                }
                var upSizes = Starward.GameInstall.GameResourceService.GetChunkDownloadPackageSize(upPackage, upBuild, upScenario, upAudio);
                long upFullBytes = upSizes.CompressedGameBytes + upSizes.CompressedAudioBytes;
                long upFullUnzip = upSizes.UncompressedGameBytes + upSizes.UncompressedAudioBytes;
                Console.WriteLine($"整包：需要下载 {FormatSize(upFullBytes)}，解压后共 {FormatSize(upFullUnzip)}");
                // 差分包：只有本地版本在官方 DiffTags 里才有
                if (upLocal is not null && upPackage.DiffTags is { } upDiffTags && upDiffTags.Contains(upLocal))
                {
                    var upPatches = upResource.GetGameSophonPatchManifestsAsync(upGameId, upPackage, upInstall, upScenario, upAudio, default).GetAwaiter().GetResult();
                    long upPatchBytes = 0;
                    bool upHasPatch = false;
                    foreach (var upPatch in upPatches)
                    {
                        if (upPatch.Stats is not null && upPatch.Stats.TryGetValue(upLocal, out var upStats) && upStats is not null)
                        {
                            upHasPatch = true;
                            upPatchBytes += upStats.CompressedSize;
                        }
                    }
                    if (upHasPatch)
                    {
                        Console.WriteLine($"差分：从 {upLocal} 起需要下载 {FormatSize(upPatchBytes)}（比整包省 {FormatSize(upFullBytes - upPatchBytes)}）");
                    }
                    else
                    {
                        Console.WriteLine($"差分：官方列出从 {upLocal} 起的差分包，但清单里没有对应统计（按整包 {FormatSize(upFullBytes)} 估）");
                    }
                }
                else if (upLocal is null)
                {
                    Console.WriteLine("差分：读不到本地版本（可用 --local 指定），只能给整包体积");
                }
                else
                {
                    string upTags = upPackage.DiffTags is { Count: > 0 } upTagList ? string.Join(", ", upTagList) : "无";
                    Console.WriteLine($"差分：官方没有从 {upLocal} 起的差分包（官方列出：{upTags}）");
                }
                Console.WriteLine($"耗时 {upClock.ElapsedMilliseconds} ms（只读估算；实际下载量由安装器按块复用决定）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"失败（{upClock.ElapsedMilliseconds} ms）：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // `--update-run` 已删除：它把「补丁 + 块复用 + 下载」串起来实跑，属于自研 UpdateOrchestrator；
        // 上游等价能力是 Starward.GameInstall.GameInstallRunner（CLI 走 `--install-task`）。只读估算保留在 `--update-plan`。
        // 预下载体积（诊断用）：`lunarward --predownload-size <gameKey>`
        if (args.Length >= 2 && args[0] is "--predownload-size")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz pdsBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            InitEnviroment();
            Starward.Core.HoYoPlay.GameId? pdsGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(pdsBiz);
            if (pdsGameId is null)
            {
                Console.WriteLine($"这个游戏没有对应的 HoYoPlay 条目：{pdsBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            string pdsInstall = Features.GameLauncher.GameLauncherService.GetGameInstallPath(pdsGameId)
                                ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", pdsBiz.Value);
            var pdsClock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // 取数与上游 PreDownloadDialog 一致：branch → 场景/语音 → 本地版本 → 官方 chunk 清单
                Starward.GameInstall.HoYoPlayService pdsHoYoPlay = AppConfig.GetService<Starward.GameInstall.HoYoPlayService>();
                Starward.GameInstall.GameConfigService pdsConfig = AppConfig.GetService<Starward.GameInstall.GameConfigService>();
                Starward.GameInstall.GameResourceService pdsResource = AppConfig.GetService<Starward.GameInstall.GameResourceService>();
                Starward.Core.HoYoPlay.GameBranch? pdsBranch = pdsHoYoPlay.GetGameBranchAsync(pdsGameId, default).GetAwaiter().GetResult();
                if (pdsBranch is null)
                {
                    Console.WriteLine("官方接口没有返回 branch 信息（离线或接口变更）");
                    Environment.ExitCode = 1;
                    return;
                }
                Starward.Core.HoYoPlay.GameBranchPackage? pdsPre = pdsBranch.PreDownload;
                if (pdsPre is null)
                {
                    Console.WriteLine("当前没有预下载（该版本可能未开放预下载）");
                    return;
                }
                Starward.Core.AudioLanguage pdsAudio = pdsConfig.GetAudioLanguageAsync(pdsGameId, pdsInstall, default).GetAwaiter().GetResult();
                string pdsScenario = pdsConfig.GetCategoryScenarioAsync(pdsGameId, pdsInstall, default).GetAwaiter().GetResult() ?? Starward.Core.HoYoPlay.CategoryScenario.CATEGORY_SCENARIO_FULL;
                string? pdsLocal = Starward.GameInstall.GameConfigService.GetGameConfigIniAsync(pdsInstall, default).GetAwaiter().GetResult()?.GameVersion?.ToString();
                Console.WriteLine($"安装目录：{pdsInstall}");
                Console.WriteLine($"本地版本：{pdsLocal ?? "(未知)"}  语音：{pdsAudio}  场景：{pdsScenario}");
                long pdsPatchBytes = 0;
                bool pdsHasPatch = false;
                if (pdsLocal is not null && pdsPre.DiffTags is { } pdsDiffTags && pdsDiffTags.Contains(pdsLocal))
                {
                    // 有差分包：直接查「从本地版本起」的补丁体积（与 PreDownloadDialog 的分支一致）
                    var pdsPatches = pdsResource.GetGameSophonPatchManifestsAsync(pdsGameId, pdsPre, pdsInstall, pdsScenario, pdsAudio, default).GetAwaiter().GetResult();
                    foreach (var pdsPatch in pdsPatches)
                    {
                        if (pdsPatch.Stats is not null && pdsPatch.Stats.TryGetValue(pdsLocal, out var pdsStats) && pdsStats is not null)
                        {
                            pdsHasPatch = true;
                            pdsPatchBytes += pdsStats.CompressedSize;
                        }
                    }
                }
                long pdsFull = 0;
                Starward.Core.HoYoPlay.GameSophonChunkBuild? pdsBuild = pdsHoYoPlay.GetGameSophonChunkBuildAsync(pdsGameId, pdsPre, null, default).GetAwaiter().GetResult();
                if (pdsBuild is not null)
                {
                    var pdsSizes = Starward.GameInstall.GameResourceService.GetChunkDownloadPackageSize(pdsPre, pdsBuild, pdsScenario, pdsAudio);
                    pdsFull = pdsSizes.CompressedGameBytes + pdsSizes.CompressedAudioBytes;
                    Console.WriteLine($"预下载版本 {pdsPre.Tag}：整包下载 {FormatSize(pdsFull)}");
                }
                Console.WriteLine($"耗时 {pdsClock.ElapsedMilliseconds} ms");
                if (pdsHasPatch)
                {
                    Console.WriteLine($"本地 {pdsLocal} → 预下载实际下载 {FormatSize(pdsPatchBytes)}（差分）");
                }
                else if (pdsLocal is not null)
                {
                    long pdsOnly = pdsResource.GetPredownloadResourceSizeAsync(pdsGameId, pdsBranch, pdsInstall, pdsScenario, pdsAudio, pdsLocal, default).GetAwaiter().GetResult();
                    Console.WriteLine($"本地 {pdsLocal} → 预下载实际下载 {FormatSize(pdsOnly)}");
                }
                else
                {
                    Console.WriteLine($"预下载需要下载 {FormatSize(pdsFull)}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"失败（{pdsClock.ElapsedMilliseconds} ms）：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // 走安装服务（与 UI 同一路径）：`lunarward --install-task <gameKey> <目录> [--predownload] [--run-seconds N]`
        if (args.Length >= 3 && args[0] is "--install-task")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz itBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            string itDir = args[2];
            bool itPre = args.Contains("--predownload");
            InitEnviroment();
            Starward.Core.HoYoPlay.GameId? itGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(itBiz);
            if (itGameId is null)
            {
                Console.WriteLine($"这个游戏没有对应的 HoYoPlay 条目：{itBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            // 0.19.0：任务由进程内 facade 驱动；语音/场景由服务自己从 config.ini 读（不再有 --audio）
            Starward.Features.GameInstall.GameInstallService itService = AppConfig.GetService<Starward.Features.GameInstall.GameInstallService>();
            var itTask = (itPre
                              ? itService.StartPredownloadAsync(itGameId, itDir, default)
                              : itService.StartUpdateAsync(itGameId, itDir, default))
                         .GetAwaiter()
                         .GetResult();
            if (itTask is null)
            {
                Console.WriteLine("任务没有启动（安装目录为空？）");
                return;
            }
            int itStopAfter = 6;
            int itStopAt = Array.IndexOf(args, "--run-seconds");
            if (itStopAt >= 0 && args.Length > itStopAt + 1 && int.TryParse(args[itStopAt + 1], out int parsedStop))
            {
                itStopAfter = parsedStop;
            }
            var itWatch = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                // 进度对象是快照，每轮重新取实时任务
                Starward.GameInstall.GameInstallProgress itLive = itService.GetGameInstallTask(itGameId) ?? itTask;
                if (itLive.State is not (Starward.GameInstall.RunnerState.Running or Starward.GameInstall.RunnerState.Queue))
                {
                    break;
                }
                if (itWatch.Elapsed.TotalSeconds >= itStopAfter)
                {
                    Console.WriteLine($"已跑 {itStopAfter} 秒，主动停止（验证用，避免整包下载）。");
                    itService.StopTaskAsync(itLive).GetAwaiter().GetResult();
                    break;
                }
                Console.WriteLine($"状态={itLive.State} 进度={itLive.PercentProgress:P1} 已下={FormatSize(itLive.NetworkFinishedBytes)}/{FormatSize(itLive.NetworkTotalBytes)} 速度={itLive.NetworkDownloadSpeed / 1024} KB/s 剩余={itLive.RemainTimeSeconds}s");
                Thread.Sleep(3000);
            }
            Starward.GameInstall.GameInstallProgress itDone = itService.GetGameInstallTask(itGameId) ?? itTask;
            Console.WriteLine($"结束：状态={itDone.State} 错误={itDone.ErrorMessage ?? "(无)"}");
            return;
        }

        // 直接写版本标记（诊断/修复用）：`lunarward --version-write <安装目录> <版本号>`
        if (args.Length >= 3 && args[0] is "--version-write")
        {
            // 三道校验：目录里必须有 config.ini、版本号形态合法、写前备份（避免误写坏用户数据）。
            string vwDir = args[1];
            string vwVersion = args[2];
            string vwConfig = System.IO.Path.Combine(vwDir, "config.ini");
            if (!System.IO.File.Exists(vwConfig))
            {
                Console.WriteLine($"拒绝：{vwConfig} 不存在（不是游戏安装目录？）");
                Environment.ExitCode = 1;
                return;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(vwVersion, @"^\d+\.\d+\.\d+$"))
            {
                Console.WriteLine($"拒绝：版本号 \"{vwVersion}\" 不是 x.y.z 形态");
                Environment.ExitCode = 1;
                return;
            }
            System.IO.File.Copy(vwConfig, vwConfig + ".bak", overwrite: true);
            // 0.19.0 删除了 Sophon.GameVersionMarker：版本号就写在 config.ini 的 [General] game_version
            Starward.GameInstall.Model.GameConfigIni? vwIni = Starward.GameInstall.GameConfigService.GetGameConfigIniAsync(vwDir, default).GetAwaiter().GetResult();
            if (vwIni is null)
            {
                Console.WriteLine($"拒绝：读不出 {vwConfig}（不是游戏安装目录？）");
                Environment.ExitCode = 1;
                return;
            }
            vwIni.GameVersion = System.Version.Parse(vwVersion);
            Starward.GameInstall.GameConfigService.SetGameConfigIniAsync(vwDir, vwIni, default).GetAwaiter().GetResult();
            Console.WriteLine($"已写入：config.ini game_version={vwVersion}（原文件备份为 config.ini.bak）");
            return;
        }

        // 安装/预下载规划（只算不下）：`lunarward --install-plan <gameKey> [--predownload] [--install-dir <目录>]`
        // 0.19.0：自研 SophonClient / InstallPlanner 已删除，这里改成官方 chunk 清单的体积统计，
        // 取数与上游 InstallGameDialog / PreDownloadDialog 一致（不再有 --md5 / 逐文件计划）。
        if (args.Length >= 2 && args[0] is "--install-plan")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz ipBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            InitEnviroment();
            Starward.Core.HoYoPlay.GameId? ipGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(ipBiz);
            if (ipGameId is null)
            {
                Console.WriteLine($"这个游戏没有对应的 HoYoPlay 条目：{ipBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            bool ipPreDownload = args.Contains("--predownload");
            string ipInstallPath = Features.GameLauncher.GameLauncherService.GetGameInstallPath(ipGameId)
                                   ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", ipBiz.Value);
            int ipDirIndex = Array.IndexOf(args, "--install-dir");
            if (ipDirIndex >= 0 && ipDirIndex + 1 < args.Length)
            {
                ipInstallPath = args[ipDirIndex + 1];
            }
            Console.WriteLine($"安装目录：{ipInstallPath}");
            Starward.GameInstall.GameConfigService ipConfig = AppConfig.GetService<Starward.GameInstall.GameConfigService>();
            Starward.GameInstall.HoYoPlayService ipHoYoPlay = AppConfig.GetService<Starward.GameInstall.HoYoPlayService>();
            Starward.Core.AudioLanguage ipAudio = ipConfig.GetAudioLanguageAsync(ipGameId, ipInstallPath, default).GetAwaiter().GetResult();
            string ipScenario = ipConfig.GetCategoryScenarioAsync(ipGameId, ipInstallPath, default).GetAwaiter().GetResult() ?? Starward.Core.HoYoPlay.CategoryScenario.CATEGORY_SCENARIO_FULL;
            Console.WriteLine($"已装语音包：{ipAudio}（场景 {ipScenario}）");
            try
            {
                Starward.Core.HoYoPlay.GameBranch? ipBranch = ipHoYoPlay.GetGameBranchAsync(ipGameId, default).GetAwaiter().GetResult();
                if (ipBranch is null)
                {
                    Console.WriteLine("官方接口没有返回 branch 信息（离线或接口变更）");
                    Environment.ExitCode = 1;
                    return;
                }
                Starward.Core.HoYoPlay.GameBranchPackage? ipPackage = ipPreDownload ? ipBranch.PreDownload : ipBranch.Main;
                if (ipPackage is null)
                {
                    Console.WriteLine(ipPreDownload ? "官方接口没有预下载清单（当前无预下载）。" : "官方接口没有返回清单。");
                    return;
                }
                Starward.Core.HoYoPlay.GameSophonChunkBuild? ipBuild = ipHoYoPlay.GetGameSophonChunkBuildAsync(ipGameId, ipPackage, null, default).GetAwaiter().GetResult();
                if (ipBuild is null)
                {
                    Console.WriteLine("没有返回 chunk 清单。");
                    Environment.ExitCode = 1;
                    return;
                }
                var ipSizes = Starward.GameInstall.GameResourceService.GetChunkDownloadPackageSize(ipPackage, ipBuild, ipScenario, ipAudio);
                Console.WriteLine($"版本：{ipPackage.Tag}（{(ipPreDownload ? "预下载" : "当前")}）  build={ipBuild.BuildId}");
                Console.WriteLine($"清单类别：{string.Join(", ", ipBuild.Manifests.Select(x => x.MatchingField))}");
                Console.WriteLine($"按清单，需要下载 {FormatSize(ipSizes.CompressedGameBytes + ipSizes.CompressedAudioBytes)}，解压后共 {FormatSize(ipSizes.UncompressedGameBytes + ipSizes.UncompressedAudioBytes)}（语音 {ipAudio}）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"规划失败：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // 打印本地版本标记：`lunarward --version-status <gameKey>`
        if (args.Length >= 2 && args[0] is "--version-status")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz vsBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            InitEnviroment();
            Starward.Core.HoYoPlay.GameId? vsGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(vsBiz);
            string vsInstall = (vsGameId is null ? null : Features.GameLauncher.GameLauncherService.GetGameInstallPath(vsGameId))
                               ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", vsBiz.Value);
            Console.WriteLine($"安装目录：{vsInstall}");
            Starward.GameInstall.Model.GameConfigIni? vsIni = Starward.GameInstall.GameConfigService.GetGameConfigIniAsync(vsInstall, default).GetAwaiter().GetResult();
            if (vsIni is null)
            {
                Console.WriteLine($"没有 config.ini（{System.IO.Path.Combine(vsInstall, "config.ini")}）");
                return;
            }
            Console.WriteLine(vsIni.GameVersion is null ? "config.ini 里没有版本号" : $"本地版本：{vsIni.GameVersion}");
            if (!string.IsNullOrWhiteSpace(vsIni.GameBiz))
            {
                Console.WriteLine($"config.ini game_biz：{vsIni.GameBiz}");
            }
            return;
        }

        // 差分更新（只读：枚举官方差分包，不再下载/应用）：
        //   `--patch-plan <gameKey> [--local <ver>] [--predownload] [--install-dir <目录>]`
        // 0.19.0 删除了 Hi3Helper.Sophon 的 SophonPatch 链路，`--patch-run` 一并删除；
        // 真正修复/更新走上游 GameInstallRunner（CLI：`--install-task`）。
        if (args.Length >= 2 && args[0] is "--patch-plan")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz ptBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            InitEnviroment();
            Starward.Core.HoYoPlay.GameId? ptGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(ptBiz);
            if (ptGameId is null)
            {
                Console.WriteLine($"这个游戏没有对应的 HoYoPlay 条目：{ptBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            bool ptPreDownload = args.Contains("--predownload");
            string ptInstall = Features.GameLauncher.GameLauncherService.GetGameInstallPath(ptGameId)
                               ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", ptBiz.Value);
            int ptDirIndex = Array.IndexOf(args, "--install-dir");
            if (ptDirIndex >= 0 && ptDirIndex + 1 < args.Length)
            {
                ptInstall = args[ptDirIndex + 1];
            }
            var ptClock = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                Starward.GameInstall.GameConfigService ptConfig = AppConfig.GetService<Starward.GameInstall.GameConfigService>();
                Starward.GameInstall.HoYoPlayService ptHoYoPlay = AppConfig.GetService<Starward.GameInstall.HoYoPlayService>();
                Starward.GameInstall.GameResourceService ptResource = AppConfig.GetService<Starward.GameInstall.GameResourceService>();
                Starward.Core.AudioLanguage ptAudio = ptConfig.GetAudioLanguageAsync(ptGameId, ptInstall, default).GetAwaiter().GetResult();
                string ptScenario = ptConfig.GetCategoryScenarioAsync(ptGameId, ptInstall, default).GetAwaiter().GetResult() ?? Starward.Core.HoYoPlay.CategoryScenario.CATEGORY_SCENARIO_FULL;
                int ptLocalIndex = Array.IndexOf(args, "--local");
                string? ptLocal = ptLocalIndex >= 0 && ptLocalIndex + 1 < args.Length
                    ? args[ptLocalIndex + 1]
                    : Starward.GameInstall.GameConfigService.GetGameConfigIniAsync(ptInstall, default).GetAwaiter().GetResult()?.GameVersion?.ToString();
                if (string.IsNullOrEmpty(ptLocal))
                {
                    Console.WriteLine("读不到本地版本（可用 --local 指定）");
                    Environment.ExitCode = 1;
                    return;
                }
                Starward.Core.HoYoPlay.GameBranch? ptBranch = ptHoYoPlay.GetGameBranchAsync(ptGameId, default).GetAwaiter().GetResult();
                if (ptBranch is null)
                {
                    Console.WriteLine("官方接口没有返回 branch 信息（离线或接口变更）");
                    Environment.ExitCode = 1;
                    return;
                }
                Starward.Core.HoYoPlay.GameBranchPackage? ptPackage = ptPreDownload ? ptBranch.PreDownload : ptBranch.Main;
                if (ptPackage is null)
                {
                    Console.WriteLine("官方接口没有返回清单。");
                    Environment.ExitCode = 1;
                    return;
                }
                Console.WriteLine($"本地 {ptLocal} → 目标 {ptPackage.Tag}（{(ptPreDownload ? "预下载" : "当前")}）");
                if (ptPackage.DiffTags is { Count: > 0 } ptDiffTags && !ptDiffTags.Contains(ptLocal))
                {
                    Console.WriteLine($"没有从 {ptLocal} 起的差分包（官方列出：{string.Join(", ", ptDiffTags)}）");
                    return;
                }
                var ptPatches = ptResource.GetGameSophonPatchManifestsAsync(ptGameId, ptPackage, ptInstall, ptScenario, ptAudio, default).GetAwaiter().GetResult();
                long ptBytes = 0;
                int ptFields = 0;
                foreach (var ptPatch in ptPatches)
                {
                    if (ptPatch.Stats is null || !ptPatch.Stats.TryGetValue(ptLocal, out var ptStats) || ptStats is null)
                    {
                        continue;
                    }
                    ptFields++;
                    ptBytes += ptStats.CompressedSize;
                    Console.WriteLine($"  [{ptPatch.MatchingField}] {ptStats.FileCount} 文件 / {ptStats.ChunkCount} 块：下载 {FormatSize(ptStats.CompressedSize)}，解压后 {FormatSize(ptStats.UncompressedSize)}");
                }
                if (ptFields == 0)
                {
                    Console.WriteLine($"没有从 {ptLocal} 起的差分包");
                    return;
                }
                Console.WriteLine($"可用补丁：{ptFields} 个类别，共需下载 {FormatSize(ptBytes)}（耗时 {ptClock.ElapsedMilliseconds} ms）");
                Console.WriteLine("（只读；真正修复/更新请用 `--install-task`）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"补丁链路失败（{ptClock.ElapsedMilliseconds} ms）：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }


        // 只读诊断：列出扫描到的运行器与图形组件（D56）
        Starward.Helpers.DataFolder.Unify();
        // 在某个方案的环境里跑一次程序（D64）：与游戏启动共用同一套 env，便于跑补丁/安装器
        if (args.Length >= 3 && args[0] is "--run-once")
        {
            InitEnviroment();
            string wanted = args[1];
            string exe = args[2];
            // R50：--game <biz> 从附加参数里摘出来，它的值才是这次启动用的游戏 key
            var runOnceExtra = new System.Collections.Generic.List<string>();
            string? runOnceGameKey = null;
            for (int i = 3; i < args.Length; i++)
            {
                if (args[i] is "--game" && i + 1 < args.Length)
                {
                    runOnceGameKey = args[++i];
                    continue;
                }
                runOnceExtra.Add(args[i]);
            }
            string extra = string.Join(' ', runOnceExtra);
            List<Starward.Features.Runtime.RuntimeProfile> profiles = Starward.Features.Runtime.RuntimeProfileStore.LoadAll();
            Starward.Features.Runtime.RuntimeProfile? profile = profiles.FirstOrDefault(x => x.Id == wanted)
                ?? profiles.FirstOrDefault(x => string.Equals(x.Name, wanted, StringComparison.Ordinal));
            if (profile is null)
            {
                Console.WriteLine($"没有这个方案：{wanted}（用 --list-profiles 看有哪些）");
                Environment.ExitCode = 1;
                return;
            }
            using System.Diagnostics.Process? process = Starward.Features.Runtime.RunOnceService.Start(profile, exe, extra, null, runOnceGameKey);
            if (process is null)
            {
                Console.WriteLine("没能启动：文件不存在，或方案没有可用的 wine");
                Environment.ExitCode = 1;
                return;
            }
            Console.WriteLine($"已启动 pid={process.Id}，等待退出…");
            if (!process.WaitForExit(TimeSpan.FromMinutes(5)))
            {
                Console.WriteLine("等了 5 分钟还没退出，放手了（进程仍在跑）");
                return;
            }
            Console.WriteLine($"退出码：{process.ExitCode}");
            Environment.ExitCode = process.ExitCode;
            return;
        }

        // 图形层的前缀侧操作：给脚本/排错用（D63 §5）
        if (args.Length >= 2 && args[0] is "--graphics-install" or "--graphics-verify" or "--graphics-remove")
        {
            InitEnviroment();
            string prefix = args[1];
            if (!Directory.Exists(prefix))
            {
                Console.WriteLine($"前缀不存在：{prefix}");
                Environment.ExitCode = 1;
                return;
            }
            if (args[0] is "--graphics-install")
            {
                if (args.Length < 3)
                {
                    Console.WriteLine("用法：--graphics-install <prefix> <组件 id|名字|路径>");
                    Environment.ExitCode = 1;
                    return;
                }
                string wanted = args[2];
                Starward.Features.Runtime.Component? component = Starward.Features.Runtime.ComponentStore
                    .GetGraphics(false)
                    .FirstOrDefault(x => x.Id == wanted || x.Name == wanted || x.Path == wanted);
                if (component is null)
                {
                    Console.WriteLine($"没有这个图形组件：{wanted}（用 --list-components 看可用的）");
                    Environment.ExitCode = 1;
                    return;
                }
                bool installed = Starward.Features.Runtime.GraphicsInstaller.EnsureInstalled(prefix, component);
                Console.WriteLine(installed ? $"已安装 {component.Name} 到 {prefix}" : "记录与文件都齐全，已跳过");
            }
            else if (args[0] is "--graphics-verify")
            {
                Starward.Features.Runtime.GraphicsInstaller.GraphicsStatus status =
                    Starward.Features.Runtime.GraphicsInstaller.Verify(prefix);
                Console.WriteLine($"{status.Text}（{status.TotalFiles - status.MissingFiles}/{status.TotalFiles}）");
            }
            else
            {
                bool removed = Starward.Features.Runtime.GraphicsInstaller.Remove(prefix);
                Console.WriteLine(removed ? "已移除图形层" : "没有找到图形层记录");
            }
            return;
        }

        // 图像解码自检（24 号 P4）：Skia / Magick 两条后端各报一句，便于验收 AVIF / JXL
        if (args.Length >= 1 && args[0] is "--image-info")
        {
            if (args.Length < 2)
            {
                Console.WriteLine("用法：lunarward --image-info <图片路径>");
                Environment.ExitCode = 1;
                return;
            }
            string imagePath = args[1];
            if (!System.IO.File.Exists(imagePath))
            {
                Console.WriteLine($"找不到文件：{imagePath}");
                Environment.ExitCode = 1;
                return;
            }
            using SkiaSharp.SKBitmap? bitmap = Starward.Helpers.ImageDecode.Load(imagePath, 256);
            Console.WriteLine($"文件：{imagePath}");
            bool skia = false;
            try
            {
                using var probeStream = System.IO.File.OpenRead(imagePath);
                using SkiaSharp.SKCodec? probeCodec = SkiaSharp.SKCodec.Create(probeStream);
                skia = probeCodec?.Info.Width > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[debug] Skia 探测异常：{ex.GetType().Name}");
            }
            Console.WriteLine($"Skia 可解码：{(skia ? "是" : "否")}");
            Console.WriteLine($"Magick 可用：{(Starward.Helpers.MagickCodec.IsAvailable ? "是" : "否")}");
            if (bitmap is null)
            {
                Console.WriteLine("结果：两种后端都解不开");
                Environment.ExitCode = 1;
                return;
            }
            Console.WriteLine($"结果：{bitmap.Width}x{bitmap.Height}（长边限 256）");
            return;
        }

        // 受管组件（D73 阶段 1）：清单 / 健康 / 导入 / 下载
        if (args.Length >= 1 && args[0] is "--component-scan" or "--component-list" or "--component-health" or "--component-import" or "--component-download")
        {
            InitEnviroment();
            string kindOf(string[] argv)
            {
                for (int i = 0; i < argv.Length - 1; i++)
                {
                    if (argv[i] is "--kind")
                    {
                        return argv[i + 1];
                    }
                }
                return "";
            }

            if (args[0] is "--component-scan")
            {
                Console.WriteLine($"受管目录：{Starward.Features.Runtime.ComponentStore.ComponentsFolder}");
                Console.WriteLine($"下载源：{Starward.Features.Runtime.ComponentSources.FilePath}");
                foreach (Starward.Features.Runtime.ComponentSourceEntry entry in Starward.Features.Runtime.ComponentSources.Load())
                {
                    Console.WriteLine($"  [{entry.Kind}] {entry.Id,-14} {entry.DisplayName}  ({entry.Owner}/{entry.Repo})");
                }
                return;
            }

            if (args[0] is "--component-list")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("用法：--component-list <源 id>（用 --component-scan 看源）");
                    Environment.ExitCode = 1;
                    return;
                }
                Starward.Features.Runtime.ComponentSourceEntry? listSource = Starward.Features.Runtime.ComponentSources
                    .Load().FirstOrDefault(x => x.Id == args[1]);
                if (listSource is null)
                {
                    Console.WriteLine($"没有这个源：{args[1]}");
                    Environment.ExitCode = 1;
                    return;
                }
                using var listHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                IReadOnlyList<Starward.Features.Runtime.ComponentReleaseAsset> listAssets;
                try
                {
                    listAssets = Starward.Features.Runtime.ComponentSources
                        .ListAssetsAsync(listHttp, listSource, 10, new ConsoleLogger()).GetAwaiter().GetResult();
                }
                catch (Starward.Features.Runtime.ComponentSourceException ex)
                {
                    Console.WriteLine(ex.Message);
                    Environment.ExitCode = 1;
                    return;
                }
                if (listAssets.Count == 0)
                {
                    Console.WriteLine($"源 {listSource.Id} 没有可用归档（没有匹配的资产，检查 AssetPattern）");
                    Environment.ExitCode = 1;
                    return;
                }
                Console.WriteLine($"源 {listSource.Id}（{listSource.DisplayName}）共 {listAssets.Count} 个归档：");
                foreach (Starward.Features.Runtime.ComponentReleaseAsset item in listAssets)
                {
                    Console.WriteLine($"  {item.Name,-44} {item.Size / 1048576,6} MB  {item.Version}");
                }
                return;
            }

            if (args[0] is "--component-health")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("用法：--component-health <组件 id|名字>");
                    Environment.ExitCode = 1;
                    return;
                }
                string wanted = args[1];
                Starward.Features.Runtime.Component? target = Starward.Features.Runtime.ComponentStore
                    .GetRunners(false).Concat(Starward.Features.Runtime.ComponentStore.GetGraphics(false))
                    .FirstOrDefault(x => string.Equals(x.Id, wanted, StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(x.Name, wanted, StringComparison.OrdinalIgnoreCase)
                                         || string.Equals(x.DisplayName, wanted, StringComparison.OrdinalIgnoreCase));
                if (target is null)
                {
                    Console.WriteLine($"没有这个组件：{wanted}（用 --list-components 或 --component-scan 看可用的）");
                    Environment.ExitCode = 1;
                    return;
                }
                Starward.Features.Runtime.ComponentHealthResult health = Starward.Features.Runtime.ComponentHealth
                    .CheckAsync(target).GetAwaiter().GetResult();
                Console.WriteLine($"{health.Name}：{health.Summary}（耗时 {health.Elapsed.TotalMilliseconds:F0} ms）");
                Console.Write(health.Output);
                Environment.ExitCode = health.Ok ? 0 : 1;
                return;
            }

            if (args[0] is "--component-import")
            {
                if (args.Length < 2)
                {
                    Console.WriteLine("用法：--component-import <归档路径> [--kind wine|graphics]");
                    Environment.ExitCode = 1;
                    return;
                }
                string kind = kindOf(args);
                if (kind is not ("wine" or "graphics" or "proton"))
                {
                    Console.WriteLine("需要 --kind wine|graphics（proton 归档用 wine）");
                    Environment.ExitCode = 1;
                    return;
                }
                try
                {
                    Starward.Features.Runtime.ComponentManifest manifest = Starward.Features.Runtime.ComponentImporter
                        .ImportAsync(args[1], kind, logger: new ConsoleLogger()).GetAwaiter().GetResult();
                    Console.WriteLine($"已导入：{manifest.Id}（{manifest.Name} {manifest.Version}，layout={manifest.Layout}）");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"导入失败：{ex.Message}");
                    Environment.ExitCode = 1;
                }
                return;
            }

            // --component-download
            if (args.Length < 2)
            {
                Console.WriteLine("用法：--component-download <源 id>（用 --component-scan 看源）");
                Environment.ExitCode = 1;
                return;
            }
            Starward.Features.Runtime.ComponentSourceEntry? source = Starward.Features.Runtime.ComponentSources
                .Load().FirstOrDefault(x => x.Id == args[1]);
            if (source is null)
            {
                Console.WriteLine($"没有这个源：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
            // 警告要能看见（例如 HTTP 403 / 限流），否则只会得到一句「没有可用归档」
            Microsoft.Extensions.Logging.ILogger consoleLogger = new ConsoleLogger();
            IReadOnlyList<Starward.Features.Runtime.ComponentReleaseAsset> assets = Starward.Features.Runtime.ComponentSources
                .ListAssetsAsync(http, source, 8, consoleLogger).GetAwaiter().GetResult();
            if (assets.Count == 0)
            {
                Console.WriteLine($"源 {source.Id} 没有可用归档（网络或过滤规则问题）");
                Environment.ExitCode = 1;
                return;
            }
            Starward.Features.Runtime.ComponentReleaseAsset asset = assets[0];
            Console.WriteLine($"下载 {asset.Name}（{asset.Size / 1024 / 1024} MB）← {asset.Url}");
            string downloadDir = Path.Combine(Starward.Features.Runtime.ComponentStore.ComponentsFolder, ".download");
            Directory.CreateDirectory(downloadDir);
            try
            {
                Starward.Features.Runtime.ComponentDownloadResult result = Starward.Features.Runtime.ComponentDownloader
                    .DownloadAsync(http, new Uri(asset.Url), downloadDir, asset.Name, asset.Sha256,
                        mirrorPrefix: source.MirrorPrefix, connections: 4,
                        chunkSize: Starward.Features.Runtime.ComponentDownloader.DefaultChunkSize, logger: consoleLogger,
                        new Progress<Starward.Features.Runtime.ComponentDownloadProgress>(p =>
                        {
                            if (p.Total is long total and > 0)
                            {
                                Console.Write($"\r  {p.Received * 100 / total}%  {p.BytesPerSecond / 1024 / 1024:F1} MB/s  ({p.ActiveConnections} 连接)   ");
                            }
                        })).GetAwaiter().GetResult();
                Console.WriteLine();
                Console.WriteLine($"下载完成：{result.Bytes} 字节，连接 {result.Connections}{(result.DegradedToSingle ? "（已降级单流）" : "")}，digest {(result.DigestMatched ? "一致" : "未校验")}");
                Starward.Features.Runtime.ComponentManifest manifest = Starward.Features.Runtime.ComponentImporter
                    .ImportAsync(result.FilePath, source.Kind == "graphics" ? "graphics" : "wine",
                        null, null, result.Sha256, asset.Sha256, asset.Url, source.MirrorPrefix, consoleLogger).GetAwaiter().GetResult();
                try
                {
                    File.Delete(result.FilePath);
                }
                catch
                {
                    // 归档删不掉不影响结果
                }
                Console.WriteLine($"已导入：{manifest.Id}（{manifest.Name} {manifest.Version}）");
            }
            catch (IOException ex) when (ex.Message.StartsWith("组件目录已存在", StringComparison.Ordinal))
            {
                // 同名同版本已经导过了：不是失败，别让脚本以为出错
                Console.WriteLine($"跳过：{ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"下载失败：{ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        if (args.Length >= 1 && args[0] is "--list-components")
        {
            InitEnviroment();
            Console.WriteLine($"扫描根：{string.Join(", ", Starward.Features.Runtime.ComponentStore.ScanRoots)}");
            Console.WriteLine("运行器：");
            foreach (Starward.Features.Runtime.Component runner in Starward.Features.Runtime.ComponentStore.GetRunners(true))
            {
                Console.WriteLine($"  [{runner.KindText}] {runner.DisplayName}  {runner.Path}");
            }
            Console.WriteLine("图形组件：");
            foreach (Starward.Features.Runtime.Component gfx in Starward.Features.Runtime.ComponentStore.GetGraphics(true))
            {
                Console.WriteLine($"  [{gfx.KindText}] {gfx.Name}  {gfx.Path}");
            }
            return;
        }

        // 运行器可用性一览（D58）：只做无副作用检查，不建前缀
        if (args.Length >= 1 && args[0] is "--list-runners")
        {
            InitEnviroment();
            var runners = Starward.Features.Runtime.ComponentStore.GetRunners(true);
            if (args.Contains("--json"))
            {
                var payload = runners.Select(r => new
                {
                    name = r.Name,
                    path = r.Path,
                    version = r.Version,
                    layout = r.Layout,
                    usable = r.Usable,
                    reason = r.UsabilityReason,
                    wineserver = Starward.Features.Runtime.ComponentStore.FindWineserver(r.Path),
                }).ToArray();
                Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                return;
            }
            Console.WriteLine($"扫描根：{string.Join(", ", Starward.Features.Runtime.ComponentStore.ScanRoots)}");
            foreach (var r in runners)
            {
                string mark = r.Usable == true ? "✓" : "✗";
                string version = string.IsNullOrWhiteSpace(r.Version) ? "(无版本)" : r.Version;
                string reason = string.IsNullOrWhiteSpace(r.UsabilityReason) ? "" : "  ← " + r.UsabilityReason;
                // 名字列用 Name（不加版本）：版本已经有单独一列，DisplayName 会让它出现两次
                Console.WriteLine($"  {mark} {r.Name,-14} {version,-20} {r.LayoutText,-9} {r.Path}{reason}");
            }
            return;
        }

        // 数据库备份：`lunarward --database-backup <输出.zip>`（RG-07 的自动化入口）
        if (args.Contains("--database-backup"))
        {
            int backupIndex = Array.IndexOf(args, "--database-backup");
            string? backupTarget = backupIndex + 1 < args.Length ? args[backupIndex + 1] : null;
            if (string.IsNullOrWhiteSpace(backupTarget) || backupTarget.StartsWith("--"))
            {
                Console.Error.WriteLine("用法：lunarward --database-backup <输出.zip>");
                Environment.ExitCode = 1;
                return;
            }
            try
            {
                InitEnviroment();
                string fullPath = Path.GetFullPath(backupTarget);
                Starward.Features.Database.DatabaseService.BackupToArchive(fullPath);
                Console.WriteLine($"已备份到 {fullPath}（{new FileInfo(fullPath).Length} 字节）");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"备份失败：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // 诊断包：`lunarward --diagnose [--out DIR] [--game BIZ]`
        if (args.Contains("--diagnose"))
        {
            try
            {
                InitEnviroment();
                string? outDir = null;
                int outIndex = Array.IndexOf(args, "--out");
                if (outIndex >= 0 && outIndex + 1 < args.Length)
                {
                    outDir = args[outIndex + 1];
                }
                Starward.Core.GameBiz? diagBiz = null;
                int gameIndex = Array.IndexOf(args, "--game");
                if (gameIndex >= 0 && gameIndex + 1 < args.Length && Starward.Core.GameBiz.TryParse(args[gameIndex + 1], out Starward.Core.GameBiz parsedBiz))
                {
                    diagBiz = parsedBiz;
                }
                string zip = Starward.Helpers.DiagnosticBundle.CreateAsync(outDir, diagBiz, null).GetAwaiter().GetResult();
                Console.WriteLine(zip);
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine("导出诊断包失败：" + ex.Message);
                Environment.ExitCode = 1;
                return;
            }
        }

        if (args.Length >= 2 && args[0] is "--preflight")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz pfBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            // CLI 下也要先做环境初始化：安装路径 0.19.0 起存在 SQLite 的 GameEntry 表里，
            // 只调 LoadConfiguration 不会建库建表，查询会报 no such table: GameEntry。
            try
            {
                InitEnviroment();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取配置失败：{ex.GetType().Name}: {ex.Message}");
            }
            string? exeName = Starward.Features.GameLauncher.GameLauncherService.GetGameExeName(pfBiz);
            // 0.19.0：安装路径存在游戏条目里（AppConfig.GetGameInstallPath 已 Obsolete(true)，会编译报错）
            Starward.Core.HoYoPlay.GameId? pfGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(pfBiz);
            string? installPath = pfGameId is null ? null : Starward.Features.GameLauncher.GameLauncherService.GetGameInstallPath(pfGameId);
            // 显式给路径时优先用它；否则用游戏条目里的安装路径；再不行按常见布局猜一个（只为排错）
            string exe = args.Length >= 3
                ? args[2]
                : System.IO.Path.Combine(installPath ?? "", exeName ?? "");
            if ((exe.Length == 0 || !System.IO.File.Exists(exe)) && exeName is not null)
            {
                string guess = System.IO.Path.Combine(installPath ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", pfBiz.Value), exeName);
                if (System.IO.File.Exists(guess))
                {
                    exe = guess;
                }
            }
            var pfProfile = Starward.Features.Runtime.ProfileBinding.Resolve(pfBiz);
            var pf = Starward.Features.GameLauncher.LaunchPreflight.Check(pfBiz, exe, pfProfile);
            Console.WriteLine($"profile : {pfProfile?.Name ?? "(none)"}");
            foreach (var item in pf.Items)
            {
                Console.WriteLine($"[{item.Status,-7}] {item.Name,-12} {item.Detail}{(item.Fix is null ? "" : "  → " + item.Fix)}");
            }
            Console.WriteLine(pf.CanLaunch ? "结论：可以启动" : "结论：不要启动（见上面 Failed 项）");
            // 脚本里会拿它当门禁（与 --launch-plan 一致）：不能启动就返回非 0。
            if (!pf.CanLaunch)
            {
                Environment.ExitCode = 1;
            }
            return;
        }

        // 把现有前缀做成模板：`lunarward --prefix-template <源目录> [名称]`
        if (args.Length >= 2 && args[0] is "--prefix-template")
        {
            string source = args[1];
            if (!System.IO.Directory.Exists(System.IO.Path.Combine(source, "drive_c")))
            {
                Console.WriteLine("这个目录不像 Wine 前缀（缺少 drive_c）。");
                return;
            }
            string name = args.Length >= 3 ? args[2] : $"{System.IO.Path.GetFileName(source.TrimEnd('/'))}-{DateTime.Now:MMddHHmm}";
            string destination = System.IO.Path.Combine(Features.Runtime.RuntimeProfileStore.TemplatesFolder, name);
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Features.Runtime.PrefixManager.ClonePrefix(source, destination, null);
                sw.Stop();
                Console.WriteLine($"模板已创建：{destination}（{sw.ElapsedMilliseconds} ms）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"失败：{ex.GetType().Name}: {ex.Message}");
            }
            return;
        }

        // 前缀排错：`lunarward --prefix-ensure <gameKey> [profileId]`
        if (args.Length >= 2 && args[0] is "--prefix-ensure")
        {
            var profiles = Features.Runtime.RuntimeProfileStore.LoadAll();
            var prof = profiles.FirstOrDefault(p => p.Id == (args.Length >= 3 ? args[2] : "")) ?? profiles.FirstOrDefault();
            if (prof is null)
            {
                Console.WriteLine("还没有任何运行配置。");
                return;
            }
            Console.WriteLine($"profile : {prof.Name}  运行器={prof.RunnerId ?? prof.RunnerPath ?? "(未指定)"}  前缀={prof.PrefixPath ?? "(默认)"}");
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                string prefix = Features.Runtime.PrefixManager.EnsurePrefix(prof, args[1], null);
                sw.Stop();
                var meta = Features.Runtime.PrefixManager.GetMetadata(prefix);
                Console.WriteLine($"prefix  : {prefix}  ({sw.ElapsedMilliseconds} ms)");
                Console.WriteLine($"meta    : game={meta?.GameKey} profile={meta?.ProfileId} since={meta?.CreatedAt:yyyy-MM-dd HH:mm}");
                Console.WriteLine($"exists  : {System.IO.Directory.Exists(System.IO.Path.Combine(prefix, "drive_c"))} (drive_c)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"失败：{ex.GetType().Name}: {ex.Message}");
            }
            return;
        }

        // 组件排错：`lunarward --graphics-list` / `--graphics-install <prefix> <componentId>`
        if (args.Length >= 1 && args[0] is "--graphics-list")
        {
            foreach (var c in Features.Runtime.ComponentStore.GetGraphics(true))
            {
                Console.WriteLine($"{c.KindText}\t{c.Name}\t{c.Version}\t{c.Id}");
            }
            return;
        }
        // L-72：Uno 的 X11 后端在没有 DISPLAY 时会抛异常/静默失败，而且在 HostBuilder.Build() 里就炸，
        // 比 App.OnLaunched 更早。这里先给一句人话，避免 IDE 里「进程在跑但没窗口」的悬案。
        // 参数像命令但没有任何分支接住：不要静默进 GUI（否则缺一个参数就会变成「命令没反应，反而弹出窗口」）。
        // 允许的「纯 GUI 开关」只有下面几个，其余以 - 开头的首参数一律判为未知命令。
        if (args.Length > 0 && args[0].StartsWith('-'))
        {
            // 纯 GUI 开关 = 不需要子命令、直接决定「开窗后落到哪个状态」的那批。
            // DevSwitches 里的调试参数（--page/--subpage/--game/--scroll/--viewer/--profile-editor/--window-action）
            // 也必须放行，否则 `lunarward --subpage RuntimeEnvironmentSetting` 会被当成未知命令（2026-11-14 回归）。
            string[] guiFlags =
            {
                "--hide", "--show", "--new-instance", "--selftest-ui",
                "--page", "--subpage", "--game", "--scroll", "--viewer", "--profile-editor", "--window-action",
            };
            if (!guiFlags.Contains(args[0]))
            {
                Console.WriteLine($"未知命令或不完整：{string.Join(' ', args)}");
                Console.WriteLine("运行 lunarward --help 查看可用命令。");
                Environment.ExitCode = 1;
                return;
            }
        }

        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            Console.Error.WriteLine("[lunarward] 没有 DISPLAY / WAYLAND_DISPLAY，无法创建窗口。若从 IDE 启动，请确认运行配置继承了图形会话的环境变量。");
            Environment.Exit(3);
            return;
        }

        // `--selftest-ui`：进程内 UI 自检（L1.5，见 Features/Dev/UiSelfTest.cs）。
        // 这里只把开关塞进环境变量，真正执行在 MainWindow 第一次 Activated 之后（那时窗口已 map、首页已 OnLoaded）。
        if (args.Contains("--selftest-ui"))
        {
            Environment.SetEnvironmentVariable("LUNARWARD_SELFTEST", "1");
        }


        // GUI 路径同样要先跑一次环境检查：CacheFolder / UserDataFolder / config 都在里面初始化。
        // 少了这一步，App.OnLaunched 会看到 UserDataFolder 为 null，弹出欢迎向导，之后
        // 再拿 CacheFolder 拼日志目录就是 null → 启动即挂（0.19.0 合并后漏掉了这次调用）。
        InitEnviroment();

        // D134：AppImage 没有安装步骤，桌面关联（x-scheme-handler/lunarward）只能运行时补。
        // 以前要用户手动跑 `--register-protocol`，否则网页点深链毫无反应；这里只在没注册时做一次，
        // 且扔到后台（update-desktop-database / xdg-mime 都是 fork，硬规则 10 不许占 UI 线程）。
        if (Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 })
        {
            _ = Task.Run(() =>
            {
                try
                {
                    if (!Starward.Features.UrlProtocol.UrlProtocolService.IsProtocolRegistered())
                    {
                        Starward.Features.UrlProtocol.UrlProtocolService.RegisterProtocol();
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Debug(ex, "AppImage protocol registration failed");
                }
            });
        }

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new Starward.App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseWin32()
            .Build();

        host.Run();
    }

    private static string GetEnv(System.Diagnostics.ProcessStartInfo info, string key)
        => info.Environment.TryGetValue(key, out string? value) ? value ?? "" : "";
}


/// <summary>
/// CLI 用的极简 logger：把 Warning/Error 原样打到 stdout，方便脚本排错。
/// </summary>
internal sealed class ConsoleLogger : Microsoft.Extensions.Logging.ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        => logLevel >= Microsoft.Extensions.Logging.LogLevel.Warning;

    public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel,
        Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }
        string message = formatter(state, exception);
        Console.WriteLine($"[{logLevel.ToString().ToLowerInvariant()}] {message}");
        if (exception is not null)
        {
            Console.WriteLine(exception.Message);
        }
    }
}
