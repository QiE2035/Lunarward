using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
// 只解析不启动：`lunarward --shell-plan` 打印每个 Profile 的调试 Shell 命令行与环境变量
        if (args.Length >= 1 && args[0] is "--shell-plan")
        {
            foreach (Features.Runtime.RuntimeProfile profile in Features.Runtime.RuntimeProfileStore.LoadAll())
            {
                System.Diagnostics.ProcessStartInfo? info = Features.Runtime.ProfileShellService.CreateShellStartInfo(profile);
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
            Console.WriteLine(Features.GameLauncher.GameLauncherService.DescribeLaunchPlan(args[1], args.Length > 2 ? args[2] : null));
            return;
        }

        // 截图（D31）：`lunarward --screenshot`，打印落盘路径。`--capture-test` 是同义旧名。
        if (args.Length >= 1 && args[0] is "--screenshot" or "--capture-test")
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

        // 实际下载：`lunarward --install-run <gameKey> [--predownload] [--target <dir>] [--limit-mb N|--all] [--parallel N] [--write-version]`
        //           `lunarward --install-fetch <gameKey> <相对路径> [--predownload] [--target <dir>]`
        if (args.Length >= 2 && args[0] is "--install-run" or "--install-fetch")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz dlBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            bool dlFetch = args[0] is "--install-fetch";
            string? dlOnlyFile = dlFetch ? (args.Length >= 3 ? args[2] : null) : null;
            if (dlFetch && dlOnlyFile is null)
            {
                Console.WriteLine("用法：--install-fetch <gameKey> <相对路径> [--predownload] [--target <dir>]");
                return;
            }
            bool dlPre = Array.IndexOf(args, "--predownload") >= 0;
            bool dlAll = Array.IndexOf(args, "--all") >= 0;
            long dlLimitMb = 200;
            int dlLimitIndex = Array.IndexOf(args, "--limit-mb");
            if (dlLimitIndex >= 0 && args.Length > dlLimitIndex + 1 && long.TryParse(args[dlLimitIndex + 1], out long parsedLimit))
            {
                dlLimitMb = parsedLimit;
            }
            int dlParallel = 4;
            int dlParallelIndex = Array.IndexOf(args, "--parallel");
            if (dlParallelIndex >= 0 && args.Length > dlParallelIndex + 1 && int.TryParse(args[dlParallelIndex + 1], out int parsedParallel) && parsedParallel > 0)
            {
                dlParallel = parsedParallel;
            }
            string? dlTargetDir = null;
            int dlTargetAt = Array.IndexOf(args, "--target");
            if (dlTargetAt >= 0 && args.Length > dlTargetAt + 1)
            {
                dlTargetDir = args[dlTargetAt + 1];
            }
            Starward.Features.GameInstall.Sophon.InstallDownloadRunner.RunAsync(dlBiz, dlOnlyFile, dlPre, dlAll, dlLimitMb, dlParallel, dlTargetDir, args.Contains("--write-version"))
                                                                .GetAwaiter()
                                                                .GetResult();
            return;
        }

#if DEBUG
        // 开发期：不经过鼠标，直接跑一次安装/更新并按 UI 读取的字段打印进度（真机联调用，Release 不含）。
        // 用法：--dev-install-probe <gameKey> [--limit-mb N] [--pause-after N]
        if (args.Length >= 2 && args[0] is "--dev-install-probe")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz probeBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            int limitIndex = Array.IndexOf(args, "--limit-mb");
            if (limitIndex >= 0 && limitIndex + 1 < args.Length)
            {
                Environment.SetEnvironmentVariable("LUNARWARD_DEV_INSTALL_LIMIT_MB", args[limitIndex + 1]);
            }
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            Starward.RPC.GameInstall.GameInstallService service = AppConfig.GetService<Starward.RPC.GameInstall.GameInstallService>();
            string? probePath = AppConfig.GetGameInstallPath(probeBiz);
            if (string.IsNullOrEmpty(probePath))
            {
                Console.WriteLine("这个游戏没有配置安装路径");
                return;
            }
            Starward.Core.HoYoPlay.GameId probeGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(probeBiz);
            Starward.Core.AudioLanguage probeAudio = Starward.Core.AudioLanguage.Chinese;
            bool probeRepair = args.Contains("--op-repair");
            var probeTask = probeRepair
                ? service.StartRepairAsync(probeGameId, probePath, probeAudio).GetAwaiter().GetResult()
                : service.StartUpdateAsync(probeGameId, probePath, probeAudio).GetAwaiter().GetResult();
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
                long total = probeTask.Progress_DownloadTotalBytes;
                long finish = probeTask.Progress_DownloadFinishBytes;
                long writeTotal = probeTask.Progress_WriteTotalBytes;
                long writeFinish = probeTask.Progress_WriteFinishBytes;
                double percent = writeTotal > 0 ? (double) writeFinish / writeTotal : (total > 0 ? (double) finish / total : 0);
                var liveTask = service.GetGameInstallTask(probeGameId);
                Console.WriteLine($"state={probeTask.State} live={liveTask?.State} {finish}/{total} B ({percent:P1}, ctx {probeTask.Progress_Percent:P1}) speed={probeTask.NetworkDownloadSpeed / 1048576.0:F2}MB/s remain={probeTask.RemainTimeSeconds}s err={probeTask.ErrorMessage}");
                if (pauseAfter >= 0 && !paused && finish > pauseAfter * 1048576L)
                {
                    service.PauseTaskAsync(probeTask).GetAwaiter().GetResult();
                    paused = true;
                    Thread.Sleep(2000);
                    Console.WriteLine($"paused state={probeTask.State}");
                    service.ContinueTaskAsync(probeTask).GetAwaiter().GetResult();
                    Console.WriteLine($"continued state={probeTask.State}");
                }
                if (probeTask.State is Starward.RPC.GameInstall.GameInstallState.Finish or Starward.RPC.GameInstall.GameInstallState.Stop or Starward.RPC.GameInstall.GameInstallState.Error or Starward.RPC.GameInstall.GameInstallState.Paused)
                {
                    break;
                }
            }
            return;
        }

        // 开发期：把某个游戏的安装路径写进配置（用于真机联调，Release 不含）。
        if (args.Length >= 3 && args[0] is "--dev-set-install-path" && Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz devBiz))
        {
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            AppConfig.SetGameInstallPath(devBiz, args[2]);
            AppConfig.SaveConfiguration();
            Console.WriteLine($"install_path_{devBiz}={AppConfig.GetGameInstallPath(devBiz)}");
            return;
        }
#endif

        // chunk 级复用估算：`lunarward --install-chunk-plan <gameKey> [--predownload] [--local <ver>]`
        if (args.Length >= 2 && args[0] is "--install-chunk-plan")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz cpBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            var cpGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(cpBiz);
            string cpInstall = AppConfig.GetGameInstallPath(cpBiz) ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", cpBiz.Value);
            int cpDirIndex = Array.IndexOf(args, "--install-dir");
            if (cpDirIndex >= 0 && cpDirIndex + 1 < args.Length)
            {
                cpInstall = args[cpDirIndex + 1];
            }
            var cpFields = new List<string> { "game" };
            cpFields.AddRange(Starward.Features.GameInstall.Sophon.SophonClient.DetectInstalledAudioFields(cpInstall));
            int cpLocalIndex = Array.IndexOf(args, "--local");
            string? cpLocal = cpLocalIndex >= 0 && cpLocalIndex + 1 < args.Length ? args[cpLocalIndex + 1] : Starward.Features.GameInstall.Sophon.SophonClient.TryReadLocalVersion(cpInstall);
            if (string.IsNullOrEmpty(cpLocal))
            {
                Console.WriteLine("读不到本地版本（可用 --local 指定）");
                Environment.ExitCode = 1;
                return;
            }
            var cpHyp = AppConfig.GetService<Starward.Core.HoYoPlay.HoYoPlayClient>();
            var cpSophon = new Starward.Features.GameInstall.Sophon.SophonClient(cpHyp);
            var cpNew = cpSophon.GetBuildInfoAsync(cpGameId, args.Contains("--predownload"), cpFields, default).GetAwaiter().GetResult();
            var cpOld = cpSophon.GetBuildInfoAsync(cpGameId, false, cpFields, default, cpLocal).GetAwaiter().GetResult();
            if (cpNew is null || cpOld is null)
            {
                Console.WriteLine($"拿不到清单：new={cpNew is not null} old={cpOld is not null}（本地版本 {cpLocal}）");
                Environment.ExitCode = 1;
                return;
            }
            var cpIndex = Starward.Features.GameInstall.Sophon.ChunkReusePlanner.BuildIndex(cpOld.Manifests.Values.SelectMany(x => x));
            var cpStats = Starward.Features.GameInstall.Sophon.ChunkReusePlanner.Measure(cpIndex, cpNew.Manifests.Values.SelectMany(x => x), cpInstall);
            Console.WriteLine($"本地版本 {cpLocal} → 目标 {cpNew.Package.Tag}；索引块 {cpIndex.Count}");
            Console.WriteLine($"新版本解压体积 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(cpStats.NewTotalBytes)}（{cpStats.NewChunks + cpStats.ReusedChunks} 块）");
            Console.WriteLine($"可复用 {cpStats.ReusedChunks} 块 / {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(cpStats.ReusableBytes)}");
            Console.WriteLine($"需下载 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(cpStats.DownloadBytes)}（{cpStats.NewChunks} 块）");
            foreach (var worst in cpStats.WorstFiles)
            {
                Console.WriteLine($"  {worst.Path}  新 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(worst.NewBytes)}  → 下载 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(worst.DownloadBytes)}");
            }
            return;
        }

        // chunk 级复用实测：`lunarward --install-chunk-run <gameKey> [--predownload] [--local <ver>] [--install-dir <旧安装>] [--target <目录>] [--limit-mb N]`
        if (args.Length >= 2 && args[0] is "--install-chunk-run")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz crBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            var crGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(crBiz);
            string crInstall = AppConfig.GetGameInstallPath(crBiz) ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", crBiz.Value);
            int crDirIndex = Array.IndexOf(args, "--install-dir");
            if (crDirIndex >= 0 && crDirIndex + 1 < args.Length)
            {
                crInstall = args[crDirIndex + 1];
            }
            int crTargetIndex = Array.IndexOf(args, "--target");
            string crTarget = crTargetIndex >= 0 && crTargetIndex + 1 < args.Length
                ? args[crTargetIndex + 1]
                : crInstall + "-chunktarget";
            int crLimitIndex = Array.IndexOf(args, "--limit-mb");
            long crLimit = crLimitIndex >= 0 && crLimitIndex + 1 < args.Length && long.TryParse(args[crLimitIndex + 1], out long crLimitMb) ? crLimitMb * 1024 * 1024 : 0;
            var crFields = new List<string> { "game" };
            crFields.AddRange(Starward.Features.GameInstall.Sophon.SophonClient.DetectInstalledAudioFields(crInstall));
            int crLocalIndex = Array.IndexOf(args, "--local");
            string? crLocal = crLocalIndex >= 0 && crLocalIndex + 1 < args.Length ? args[crLocalIndex + 1] : Starward.Features.GameInstall.Sophon.SophonClient.TryReadLocalVersion(crInstall);
            if (string.IsNullOrEmpty(crLocal))
            {
                Console.WriteLine("读不到本地版本（可用 --local 指定）");
                Environment.ExitCode = 1;
                return;
            }
            var crHyp = AppConfig.GetService<Starward.Core.HoYoPlay.HoYoPlayClient>();
            var crSophon = new Starward.Features.GameInstall.Sophon.SophonClient(crHyp);
            var crNew = crSophon.GetBuildInfoAsync(crGameId, args.Contains("--predownload"), crFields, default).GetAwaiter().GetResult();
            var crOld = crSophon.GetBuildInfoAsync(crGameId, false, crFields, default, crLocal).GetAwaiter().GetResult();
            if (crNew is null || crOld is null)
            {
                Console.WriteLine($"拿不到清单：new={crNew is not null} old={crOld is not null}");
                Environment.ExitCode = 1;
                return;
            }
            var crIndex = Starward.Features.GameInstall.Sophon.ChunkReusePlanner.BuildIndex(crOld.Manifests.Values.SelectMany(x => x));
            var crOldHash = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in crOld.Manifests.Values.SelectMany(x => x))
            {
                if (!f.IsFolder)
                {
                    crOldHash[f.File] = f.MD5;
                }
            }
            Console.WriteLine($"本地 {crLocal} → 目标 {crNew.Package.Tag}；写入 {crTarget}");
            using var crHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            long crTotalDownload = 0, crTotalReuse = 0, crFiles = 0, crFailed = 0;
            var crClock = System.Diagnostics.Stopwatch.StartNew();
            foreach (var (field, fileList) in crNew.Manifests)
            {
                if (!crNew.ChunkUrlPrefixes.TryGetValue(field, out string? prefix) || string.IsNullOrEmpty(prefix))
                {
                    Console.WriteLine($"[{field}] 没有 chunk 下载前缀，跳过");
                    continue;
                }
                // 内容完全没变的文件整份跳过（复用比例的分母不含它们）。
                int crOnly = Array.IndexOf(args, "--only-file");
                string? crOnlyFile = crOnly >= 0 && crOnly + 1 < args.Length ? args[crOnly + 1] : null;
                var need = new List<Starward.Features.GameInstall.Sophon.SophonChunkFile>();
                foreach (var f in fileList)
                {
                    if (f.IsFolder || (crOldHash.TryGetValue(f.File, out string? oldHash) && string.Equals(oldHash, f.MD5, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }
                    if (crOnlyFile is not null && !string.Equals(f.File, crOnlyFile, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    need.Add(f);
                }
                Console.WriteLine($"[{field}] 待处理 {need.Count} 个文件 → {prefix}");
                var part = Starward.Features.GameInstall.Sophon.ChunkReuseDownloader.RunAsync(
                    crHttp, need, crIndex, crInstall, crTarget, prefix, crLimit,
                    (done, total) => Task.CompletedTask, default).GetAwaiter().GetResult();
                crTotalDownload += part.DownloadedBytes;
                crTotalReuse += part.ReusedBytes;
                crFiles += part.Files;
                crFailed += part.FailedFiles;
                Console.WriteLine($"[{field}] 完成 {part.Files} 文件，下载 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(part.DownloadedBytes)}，复用 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(part.ReusedBytes)}，续传 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(part.ResumedBytes)}，失败 {part.FailedFiles}");
                foreach (var (file, error) in part.Errors)
                {
                    Console.WriteLine($"  FAIL {file}: {error}");
                }
            }
            crClock.Stop();
            Console.WriteLine($"合计：{crFiles} 文件，下载 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(crTotalDownload)}，复用 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(crTotalReuse)}，失败 {crFailed}，耗时 {crClock.Elapsed.TotalSeconds:F1}s");
            return;
        }

        // 组合编排估算（不下东西）：`lunarward --update-plan <gameKey> [--predownload] [--local <ver>] [--install-dir <目录>]`
        if (args.Length >= 2 && args[0] is "--update-plan")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz upBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            var upGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(upBiz);
            string upInstall = AppConfig.GetGameInstallPath(upBiz) ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", upBiz.Value);
            int upDir = Array.IndexOf(args, "--install-dir");
            if (upDir >= 0 && upDir + 1 < args.Length)
            {
                upInstall = args[upDir + 1];
            }
            var upFields = new List<string> { "game" };
            upFields.AddRange(Starward.Features.GameInstall.Sophon.SophonClient.DetectInstalledAudioFields(upInstall));
            int upLocalIndex = Array.IndexOf(args, "--local");
            string? upLocal = upLocalIndex >= 0 && upLocalIndex + 1 < args.Length ? args[upLocalIndex + 1] : Starward.Features.GameInstall.Sophon.SophonClient.TryReadLocalVersion(upInstall);
            if (string.IsNullOrEmpty(upLocal))
            {
                Console.WriteLine("读不到本地版本（可用 --local 指定）");
                Environment.ExitCode = 1;
                return;
            }
            var upHyp = AppConfig.GetService<Starward.Core.HoYoPlay.HoYoPlayClient>();
            var upBranch = upHyp.GetGameBranchAsync(Starward.Core.HoYoPlay.LauncherId.FromGameId(upGameId)!, "zh-cn", upGameId, default).GetAwaiter().GetResult();
            var upPackage = args.Contains("--predownload") ? upBranch?.PreDownload : upBranch?.Main;
            if (upBranch is null || upPackage is null)
            {
                Console.WriteLine("拿不到 branch/package");
                Environment.ExitCode = 1;
                return;
            }
            using var upHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var upClock = System.Diagnostics.Stopwatch.StartNew();
            var upPlan = Starward.Features.GameInstall.Sophon.UpdateOrchestrator.PlanAsync(
                upHttp, upBranch, upPackage, upFields, upInstall, args.Contains("--predownload"), upLocal, default).GetAwaiter().GetResult();
            upClock.Stop();
            if (upPlan is null)
            {
                Console.WriteLine("清单不可用");
                Environment.ExitCode = 1;
                return;
            }
            Console.WriteLine($"本地 {upLocal} → 目标 {upPackage.Tag}；变更 {upPlan.ChangedFiles} 文件 / 整文件 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(upPlan.FullFileBytes)}");
            Console.WriteLine($"补丁覆盖 {upPlan.PatchFiles} 文件 / {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(upPlan.PatchBytes)}");
            Console.WriteLine($"其余 {upPlan.ReuseFiles} 文件：块下载 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(upPlan.ReuseDownloadBytes)}，块复用 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(upPlan.ReuseReusedBytes)}");
            Console.WriteLine($"合计需要下载 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(upPlan.TotalDownloadBytes)}（整文件是 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(upPlan.FullFileBytes)}）；估算耗时 {upClock.Elapsed.TotalSeconds:F1}s");
            return;
        }

        // 组合编排实测：`lunarward --update-run <gameKey> [--predownload] [--local <ver>] [--install-dir <目录>] [--limit-mb N]`
        if (args.Length >= 2 && args[0] is "--update-run")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz urBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            var urGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(urBiz);
            string urInstall = AppConfig.GetGameInstallPath(urBiz) ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", urBiz.Value);
            int urDir = Array.IndexOf(args, "--install-dir");
            if (urDir >= 0 && urDir + 1 < args.Length)
            {
                urInstall = args[urDir + 1];
            }
            int urLimitIndex = Array.IndexOf(args, "--limit-mb");
            long urLimit = urLimitIndex >= 0 && urLimitIndex + 1 < args.Length && long.TryParse(args[urLimitIndex + 1], out long urLimitMb) ? urLimitMb * 1024 * 1024 : long.MaxValue;
            var urFields = new List<string> { "game" };
            urFields.AddRange(Starward.Features.GameInstall.Sophon.SophonClient.DetectInstalledAudioFields(urInstall));
            int urLocalIndex = Array.IndexOf(args, "--local");
            string? urLocal = urLocalIndex >= 0 && urLocalIndex + 1 < args.Length ? args[urLocalIndex + 1] : Starward.Features.GameInstall.Sophon.SophonClient.TryReadLocalVersion(urInstall);
            if (string.IsNullOrEmpty(urLocal))
            {
                Console.WriteLine("读不到本地版本（可用 --local 指定）");
                Environment.ExitCode = 1;
                return;
            }
            var urHyp = AppConfig.GetService<Starward.Core.HoYoPlay.HoYoPlayClient>();
            var urBranch = urHyp.GetGameBranchAsync(Starward.Core.HoYoPlay.LauncherId.FromGameId(urGameId)!, "zh-cn", urGameId, default).GetAwaiter().GetResult();
            var urPackage = args.Contains("--predownload") ? urBranch?.PreDownload : urBranch?.Main;
            if (urBranch is null || urPackage is null)
            {
                Console.WriteLine("拿不到 branch/package");
                Environment.ExitCode = 1;
                return;
            }
            using var urHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            var urClock = System.Diagnostics.Stopwatch.StartNew();
            var urResult = Starward.Features.GameInstall.Sophon.UpdateOrchestrator.RunAsync(
                urHttp, urBranch, urPackage, urFields, urInstall, args.Contains("--predownload"), urLocal,
                onProgress: p =>
                {
                    Console.WriteLine($"  进度 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(p.FinishedBytes)} / {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(p.TotalBytes)}  files {p.FinishedFiles}/{p.TotalFiles}  failed {p.FailedFiles}");
                },
                isPaused: null,
                limitBytes: urLimit,
                cancellationToken: default).GetAwaiter().GetResult();
            urClock.Stop();
            if (urResult is null)
            {
                Console.WriteLine("清单不可用，需退回整文件下载");
                Environment.ExitCode = 1;
                return;
            }
            Console.WriteLine($"补丁 {urResult.PatchedFiles} 文件 / {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(urResult.PatchedBytes)}");
            Console.WriteLine($"复用 {urResult.ReuseFiles} 文件 / 下载 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(urResult.DownloadedBytes)} / 复用 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(urResult.ReusedBytes)}");
            Console.WriteLine($"失败 {urResult.FailedFiles}；合计下载 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(urResult.TotalDownloadBytes)}；耗时 {urClock.Elapsed.TotalSeconds:F1}s");
            foreach (string err in urResult.Errors)
            {
                Console.WriteLine($"  ERR {err}");
            }
            return;
        }

        // 预下载体积（诊断用）：`lunarward --predownload-size <gameKey>`
        if (args.Length >= 2 && args[0] is "--predownload-size")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz pdsBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            var pdsGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(pdsBiz);
            string pdsPath = AppConfig.GetGameInstallPath(pdsBiz) ?? "";
            var fields = new List<string> { "game" };
            fields.AddRange(Starward.Features.GameInstall.Sophon.SophonClient.DetectInstalledAudioFields(pdsPath));
            var swPds = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var hyp = AppConfig.GetService<Starward.Core.HoYoPlay.HoYoPlayClient>();
                var sophon = new Starward.Features.GameInstall.Sophon.SophonClient(hyp);
                var info = sophon.GetBuildInfoAsync(pdsGameId, preDownload: true, fields, default).GetAwaiter().GetResult();
                Console.WriteLine($"耗时 {swPds.ElapsedMilliseconds} ms");
                if (info is null)
                {
                    Console.WriteLine("没有预下载清单（该版本可能未开放预下载）");
                    return;
                }
                Console.WriteLine($"整包 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(info.TotalCompressedBytes)} / 解压后 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(info.TotalUncompressedBytes)} / 类别 {string.Join(",", info.Manifests.Keys)}");
                string? lv = Starward.Features.GameInstall.Sophon.SophonClient.TryReadLocalVersion(pdsPath);
                long? delta = sophon.GetPatchDownloadBytesAsync(pdsGameId, lv ?? "", fields, preDownload: true).GetAwaiter().GetResult();
                Console.WriteLine(delta is > 0
                    ? $"本地 {lv} → 预下载实际下载 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(delta.Value)}"
                    : "没有可用的补丁体积（本地版本缺失或无 stats）");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"失败（{swPds.ElapsedMilliseconds} ms）：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // 走安装服务（与 UI 同一路径）：`lunarward --install-task <gameKey> <目录> [--predownload] [--audio Chinese]`
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
            var itAudio = Starward.Core.AudioLanguage.Chinese;
            int itAudioAt = Array.IndexOf(args, "--audio");
            if (itAudioAt >= 0 && args.Length > itAudioAt + 1 && Enum.TryParse(args[itAudioAt + 1], true, out Starward.Core.AudioLanguage parsedAudio))
            {
                itAudio = parsedAudio;
            }
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            var itService = AppConfig.GetService<Starward.RPC.GameInstall.GameInstallService>();
            var itGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(itBiz);
            var itTask = itPre
                ? itService.StartPredownloadAsync(itGameId, itDir, itAudio).GetAwaiter().GetResult()
                : itService.StartUpdateAsync(itGameId, itDir, itAudio).GetAwaiter().GetResult();
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
            while (itTask.State is Starward.RPC.GameInstall.GameInstallState.Waiting
                   or Starward.RPC.GameInstall.GameInstallState.Downloading
                   or Starward.RPC.GameInstall.GameInstallState.Verifying
                   or Starward.RPC.GameInstall.GameInstallState.Merging)
            {
                if (itWatch.Elapsed.TotalSeconds >= itStopAfter)
                {
                    Console.WriteLine($"已跑 {itStopAfter} 秒，主动停止（验证用，避免整包下载）。");
                    itService.StopTaskAsync(itTask).GetAwaiter().GetResult();
                    break;
                }
                Console.WriteLine($"状态={itTask.State} 进度={itTask.Progress_Percent:P1} 已下={Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(itTask.Progress_DownloadFinishBytes)}/{Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(itTask.Progress_DownloadTotalBytes)} 速度={itTask.NetworkDownloadSpeed / 1024} KB/s 剩余={itTask.RemainTimeSeconds}s");
                Thread.Sleep(3000);
            }
            Console.WriteLine($"结束：状态={itTask.State} 错误={itTask.ErrorMessage ?? "(无)"}");
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
            var vw = Starward.Features.GameInstall.Sophon.GameVersionMarker.WriteAll(vwDir, vwVersion);
            Console.WriteLine(vw.Count == 0 ? "没有写入任何标记" : $"已写入：{string.Join("，", vw)}（原文件备份为 config.ini.bak）");
            return;
        }

        // 安装/预下载规划（只算不下）：`lunarward --install-plan <gameKey> [--predownload] [--md5]`
        if (args.Length >= 2 && args[0] is "--install-plan")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz ipBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            bool preDownload = args.Contains("--predownload");
            bool checkMD5 = args.Contains("--md5");
            try
            {
                AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化环境失败：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            string? configured = AppConfig.GetGameInstallPath(ipBiz);
            string installPath = configured ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", ipBiz.Value);
            int ipDirIndex = Array.IndexOf(args, "--install-dir");
            if (ipDirIndex >= 0 && ipDirIndex + 1 < args.Length)
            {
                installPath = args[ipDirIndex + 1];
            }
            Console.WriteLine($"安装目录：{installPath}");
            if (Starward.Core.HoYoPlay.GameId.FromGameBiz(ipBiz) is not Starward.Core.HoYoPlay.GameId ipGameId)
            {
                Console.WriteLine($"无法映射到 GameId：{ipBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            var audio = Starward.Features.GameInstall.Sophon.SophonClient.DetectInstalledAudioFields(installPath);
            Console.WriteLine($"已装语音包：{(audio.Count == 0 ? "(无)" : string.Join(", ", audio))}");
            try
            {
                var httpClient = AppConfig.GetService<Starward.Core.HoYoPlay.HoYoPlayClient>();
                var sophon = new Starward.Features.GameInstall.Sophon.SophonClient(httpClient, AppConfig.GetLogger<App>());
                var info = sophon.GetBuildInfoAsync(ipGameId, preDownload, audio).GetAwaiter().GetResult();
                if (info is null)
                {
                    Console.WriteLine(preDownload ? "官方接口没有预下载清单（当前无预下载）。" : "官方接口没有返回清单。");
                    return;
                }
                Console.WriteLine($"版本：{info.Package.Tag}（{(preDownload ? "预下载" : "当前")}）  build={info.Build.BuildId}");
                Console.WriteLine($"清单类别：{string.Join(", ", info.Manifests.Select(x => $"{x.Key}({x.Value.Count})"))}");
                var files = info.Manifests.Values.SelectMany(x => x).ToList();
                Console.WriteLine($"按清单，解压后共 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(info.TotalUncompressedBytes)}");
                var plan = Starward.Features.GameInstall.Sophon.InstallPlanner.CreateAsync(files, installPath, checkMD5).GetAwaiter().GetResult();
                Console.WriteLine(Starward.Features.GameInstall.Sophon.InstallPlanner.Describe(plan));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"规划失败：{ex}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // 探针：用 Hi3Helper.Sophon 拉同一份清单，与自研解析结果对照：`lunarward --sophon-probe <gameKey>`
        if (args.Length >= 2 && args[0] is "--sophon-probe")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz spBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            try
            {
                AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化环境失败：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            if (Starward.Core.HoYoPlay.GameId.FromGameBiz(spBiz) is not Starward.Core.HoYoPlay.GameId spGameId)
            {
                Console.WriteLine($"无法映射到 GameId：{spBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            string? spConfigured = AppConfig.GetGameInstallPath(spBiz);
            string spInstallPath = spConfigured ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", spBiz.Value);
            var spAudio = Starward.Features.GameInstall.Sophon.SophonClient.DetectInstalledAudioFields(spInstallPath);
            try
            {
                var spHttp = AppConfig.GetService<Starward.Core.HoYoPlay.HoYoPlayClient>();
                var spSophon = new Starward.Features.GameInstall.Sophon.SophonClient(spHttp, AppConfig.GetLogger<App>());
                var spInfo = spSophon.GetBuildInfoAsync(spGameId, false, spAudio).GetAwaiter().GetResult();
                if (spInfo is null)
                {
                    Console.WriteLine("没有清单。");
                    return;
                }
                var spPackage = spInfo.Package;
                string spUrl = $"https://downloader-api.mihoyo.com/downloader/sophon_chunk/api/getBuild?branch={spPackage.Branch}&package_id={spPackage.PackageId}&password={spPackage.Password}&tag={spPackage.Tag}";
                Console.WriteLine($"build url: {spUrl.Replace(spPackage.Password ?? "", "***")}");
                using var spClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(10) };
                var spPair = Hi3Helper.Sophon.SophonManifest.CreateSophonChunkManifestInfoPair(spClient, spUrl, "game", CancellationToken.None).GetAwaiter().GetResult();
                Console.WriteLine($"pair: found={spPair.IsFound} code={spPair.ReturnCode} msg={spPair.ReturnMessage} files={spPair.ChunksInfo.FilesCount} chunks={spPair.ChunksInfo.ChunksCount}");
                long spTotal = 0;
                int spCount = 0;
                var spEnumerator = Hi3Helper.Sophon.SophonManifest.EnumerateAsync(spClient, spPair, null, CancellationToken.None).GetAsyncEnumerator();
                try
                {
                    while (spEnumerator.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                    {
                        var asset = spEnumerator.Current;
                        if (asset is null || asset.IsDirectory) continue;
                        spCount++;
                        spTotal += asset.AssetSize;
                    }
                }
                finally
                {
                    spEnumerator.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }
                Console.WriteLine($"Hi3Helper.Sophon：{spCount} 个文件，解压后 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(spTotal)}");
                var spFiles = spInfo.Manifests["game"];
                Console.WriteLine($"自研解析：{spFiles.Count(x => !x.IsFolder)} 个文件，解压后 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(spFiles.Where(x => !x.IsFolder).Sum(x => x.Size))}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"探针失败：{ex}");
                Environment.ExitCode = 1;
            }
            return;
        }

        // 应用补丁（写 target 目录，不碰安装目录）：`lunarward --patch-run <gameKey> --target <dir> [--local <ver>] [--only-slice] [--limit-files N]`
        // 打印本地版本标记：`lunarward --version-status <gameKey>`
        if (args.Length >= 2 && args[0] is "--version-status")
        {
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz vsBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            string? vsConfigured = AppConfig.GetGameInstallPath(vsBiz);
            string vsInstall = vsConfigured ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", vsBiz.Value);
            Console.WriteLine($"安装目录：{vsInstall}");
            Console.WriteLine(Starward.Features.GameInstall.Sophon.GameVersionMarker.Describe(vsInstall));
            return;
        }

        // 差分更新（Hi3Helper.Sophon 的 SophonPatch 链路）：
        //   `--patch-plan <gameKey> [--local <ver>]`                        只读：枚举可用差分包
        //   `--patch-run <gameKey> --target <dir> [--local <ver>] [--limit N] [--apply-to-install]`
        //                                                       默认只下载差分数据到 --target，不碰安装目录
        if (args.Length >= 2 && args[0] is "--patch-plan" or "--patch-run")
        {
            bool ptApply = args[0] is "--patch-run" && args.Contains("--apply-to-install");
            bool ptPreDownload = args.Contains("--predownload");
            int ptFromIndex = Array.IndexOf(args, "--from");
            if (ptFromIndex >= 0 && ptFromIndex + 1 < args.Length)
            {
                // 兼容写法：--from 等价于 --local
            }
            if (!Starward.Core.GameBiz.TryParse(args[1], out Starward.Core.GameBiz ptBiz))
            {
                Console.WriteLine($"未知游戏：{args[1]}");
                Environment.ExitCode = 1;
                return;
            }
            try
            {
                AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化环境失败：{ex.GetType().Name}: {ex.Message}");
                Environment.ExitCode = 1;
            }
            string? ptConfigured = AppConfig.GetGameInstallPath(ptBiz);
            string ptInstall = ptConfigured ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", ptBiz.Value);
            int ptLocalIndex = Array.IndexOf(args, "--local");
            string? ptLocal = ptLocalIndex >= 0 && ptLocalIndex + 1 < args.Length ? args[ptLocalIndex + 1] : Starward.Features.GameInstall.Sophon.SophonClient.TryReadLocalVersion(ptInstall);
            int ptTargetIndex = Array.IndexOf(args, "--target");
            string? ptTarget = ptTargetIndex >= 0 && ptTargetIndex + 1 < args.Length ? args[ptTargetIndex + 1] : null;
            // 安全测试用：允许把「安装目录」指到别处（比如 reflink 克隆的副本），不动真实安装。
            int ptInstallDirIndex = Array.IndexOf(args, "--install-dir");
            if (ptInstallDirIndex >= 0 && ptInstallDirIndex + 1 < args.Length)
            {
                ptInstall = args[ptInstallDirIndex + 1];
            }
            int ptLimit = -1;
            int ptLimitIndex = Array.IndexOf(args, "--limit");
            if (ptLimitIndex >= 0 && ptLimitIndex + 1 < args.Length)
            {
                _ = int.TryParse(args[ptLimitIndex + 1], out ptLimit);
            }
            if (string.IsNullOrEmpty(ptLocal))
            {
                Console.WriteLine("读不到本地版本（可用 --local 指定）");
                Environment.ExitCode = 1;
                return;
            }
            if (Starward.Core.HoYoPlay.GameId.FromGameBiz(ptBiz) is not Starward.Core.HoYoPlay.GameId ptGameId)
            {
                Console.WriteLine($"无法映射到 GameId：{ptBiz.Value}");
                Environment.ExitCode = 1;
                return;
            }
            try
            {
                var ptHoYo = AppConfig.GetService<Starward.Core.HoYoPlay.HoYoPlayClient>();
                string ptLauncher = Starward.Core.HoYoPlay.LauncherId.FromGameId(ptGameId)!;
                var ptBranch = ptHoYo.GetGameBranchAsync(ptLauncher, "zh-cn", ptGameId, CancellationToken.None).GetAwaiter().GetResult();
                Starward.Core.HoYoPlay.GameBranchPackage? ptSelected = ptPreDownload ? ptBranch?.PreDownload : ptBranch?.Main;
                if (ptSelected is not Starward.Core.HoYoPlay.GameBranchPackage ptPackage)
                {
                    Console.WriteLine("拿不到 branch package");
                    return;
                }
                using var ptHttp = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                var ptFields = new List<string> { "game" };
                ptFields.AddRange(Starward.Features.GameInstall.Sophon.SophonClient.DetectInstalledAudioFields(ptInstall));
                var ptAssets = new List<Hi3Helper.Sophon.SophonPatchAsset>();
                foreach (string ptField in ptFields)
                {
                    var ptPatchPair = Starward.Features.GameInstall.Sophon.SophonLibrary.GetPatchPairAsync(ptHttp, ptPackage, ptField, ptLocal, CancellationToken.None).GetAwaiter().GetResult();
                    Console.WriteLine($"[{ptField}] 补丁清单：found={ptPatchPair.IsFound} code={ptPatchPair.ReturnCode} files={ptPatchPair.ChunksInfo.FilesCount} chunks={ptPatchPair.ChunksInfo.ChunksCount}");
                    if (!ptPatchPair.IsFound)
                    {
                        continue;
                    }
                    var ptMainPair = Starward.Features.GameInstall.Sophon.SophonLibrary.GetChunkPairAsync(ptHttp, ptPackage, ptField, false, CancellationToken.None).GetAwaiter().GetResult();
                    var ptEnum = Hi3Helper.Sophon.SophonPatch.EnumerateUpdateAsync(ptHttp, ptPatchPair, ptMainPair, ptLocal, null, CancellationToken.None).GetAsyncEnumerator();
                    try
                    {
                        while (ptEnum.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                        {
                            if (ptEnum.Current is { } ptAsset)
                            {
                                ptAssets.Add(ptAsset);
                                if (ptLimit > 0 && ptAssets.Count >= ptLimit)
                                {
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        ptEnum.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                    if (ptLimit > 0 && ptAssets.Count >= ptLimit)
                    {
                        break;
                    }
                }
                // 只保留真正需要下补丁的条目（库自带去重：旧版本里没变、或已被新文件覆盖的条目会被剔除）
                ptAssets = Hi3Helper.Sophon.SophonPatch.EnsureOnlyGetDedupPatchAssets(ptAssets).ToList();
                long ptTotal = ptAssets.Sum(x => x.PatchSize);
                Console.WriteLine($"可用补丁：{ptAssets.Count} 个条目，差分数据 {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(ptTotal)}");
                foreach (var ptAsset in ptAssets.Take(10))
                {
                    Console.WriteLine($"  {ptAsset.TargetFilePath}  {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(ptAsset.PatchSize)}  {ptAsset.PatchMethod}");
                }
                if (!ptApply)
                {
                    Console.WriteLine(ptTarget is null ? "（只读模式；加 --patch-run --target <dir> --apply-to-install 才会真正修复/更新）" : $"（只读模式；补丁数据目录 {ptTarget}）");
                    return;
                }
                if (ptTarget is null)
                {
                    Console.WriteLine("--apply-to-install 需要 --target 指定补丁数据目录");
                    return;
                }
                System.IO.Directory.CreateDirectory(ptTarget);
                int ptApplied = 0;
                foreach (var ptAsset in ptAssets)
                {
                    try
                    {
                        ptAsset.DownloadPatchAsync(ptHttp, ptTarget, true, null, null, CancellationToken.None).GetAwaiter().GetResult();
                        ptAsset.ApplyPatchUpdateAsync(ptHttp, ptInstall, ptTarget, false, null, null, null, CancellationToken.None).GetAwaiter().GetResult();
                        ptApplied++;
                        string ptTargetFile = System.IO.Path.Combine(ptInstall, ptAsset.TargetFilePath);
                        string ptExpected = ptAsset.MainAssetInfo?.AssetHash ?? "";
                        string ptActual = System.IO.File.Exists(ptTargetFile)
                            ? Convert.ToHexString(System.Security.Cryptography.MD5.HashData(System.IO.File.ReadAllBytes(ptTargetFile)))
                            : "";
                        bool ptHashOk = string.IsNullOrEmpty(ptExpected) || string.Equals(ptExpected, ptActual, StringComparison.OrdinalIgnoreCase);
                        Console.WriteLine($"  {(ptHashOk ? "OK " : "MISMATCH")}  {ptAsset.TargetFilePath}  md5={ptActual}  expect={ptExpected}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ERR {ptAsset.TargetFilePath}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                Console.WriteLine($"应用完成：{ptApplied} / {ptAssets.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"补丁链路失败：{ex}");
            }
            return;
        }


        // 只读诊断：列出扫描到的运行器与图形组件（D56）
        Starward.Helpers.DataFolder.Unify();
        if (args.Length >= 1 && args[0] is "--list-components")
        {
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
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
            AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
            var runners = Starward.Features.Runtime.ComponentStore.GetRunners(true);
            if (args.Contains("--json"))
            {
                var payload = runners.Select(r => new
                {
                    name = r.DisplayName,
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
                Console.WriteLine($"  {mark} {r.DisplayName,-14} {version,-20} {r.LayoutText,-9} {r.Path}{reason}");
            }
            return;
        }

        // 诊断包：`lunarward --diagnose [--out DIR] [--game BIZ]`
        if (args.Contains("--diagnose"))
        {
            try
            {
                AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();
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
            // CLI 下也要先读配置（安装路径存在 SQLite 设置表里）
            try
            {
                AppConfig.LoadConfiguration();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取配置失败：{ex.GetType().Name}: {ex.Message}");
            }
            string? exeName = Starward.Features.GameLauncher.GameLauncherService.GetGameExeName(pfBiz);
            string? installPath = AppConfig.GetGameInstallPath(pfBiz);
            // 显式给路径时优先用它；否则用配置里的安装路径；再不行按常见布局猜一个（只为排错）
            string exe = args.Length >= 3
                ? args[2]
                : (installPath is null || exeName is null
                    ? System.IO.Path.Combine(AppConfig.GetGameInstallPath(pfBiz) ?? "", exeName ?? "")
                    : System.IO.Path.Combine(installPath, exeName));
            if ((exe.Length == 0 || !System.IO.File.Exists(exe)) && exeName is not null)
            {
                string guess = System.IO.Path.Combine(AppConfig.GetGameInstallPath(pfBiz) ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", pfBiz.Value), exeName);
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
            Console.WriteLine($"profile : {prof.Name}  模式={prof.PrefixMode}  模板/路径={prof.PrefixPath}");
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                string prefix = Features.Runtime.PrefixManager.EnsurePrefix(prof, args[1], null);
                sw.Stop();
                var meta = Features.Runtime.PrefixManager.GetMetadata(prefix);
                Console.WriteLine($"prefix  : {prefix}  ({sw.ElapsedMilliseconds} ms)");
                Console.WriteLine($"meta    : game={meta?.GameKey} mode={meta?.Mode} template={meta?.TemplatePath}");
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
            foreach (var c in Features.Runtime.ComponentStore.GetRunners(true))
            {
                Console.WriteLine($"{c.KindText}\t{c.Name}\t{c.Version}\t{c.Id}");
            }
            return;
        }
        if (args.Length >= 3 && args[0] is "--graphics-install")
        {
            var c = Features.Runtime.ComponentStore.GetGraphics(true).FirstOrDefault(x => x.Id == args[2]);
            if (c is null)
            {
                Console.WriteLine($"找不到组件：{args[2]}");
                return;
            }
            bool changed = Features.Runtime.GraphicsInstaller.EnsureInstalled(args[1], c);
            var installed = Features.Runtime.GraphicsInstaller.GetInstalled(args[1]);
            Console.WriteLine(changed ? $"已安装 {c.Name} -> {args[1]}" : $"已是最新（未改动）：{installed?.Name}");
            if (installed is not null)
            {
                Console.WriteLine($"记录：{installed.Name} / {installed.InstalledAt:yyyy-MM-dd HH:mm} / {installed.Files.Count} 个文件");
            }
            return;
        }

        // L-72：Uno 的 X11 后端在没有 DISPLAY 时会抛异常/静默失败，而且在 HostBuilder.Build() 里就炸，
        // 比 App.OnLaunched 更早。这里先给一句人话，避免 IDE 里「进程在跑但没窗口」的悬案。
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            Console.Error.WriteLine("[lunarward] 没有 DISPLAY / WAYLAND_DISPLAY，无法创建窗口。若从 IDE 启动，请确认运行配置继承了图形会话的环境变量。");
            Environment.Exit(3);
            return;
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
