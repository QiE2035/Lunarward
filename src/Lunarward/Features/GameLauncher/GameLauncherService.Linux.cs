// Lunarward：Linux 启动路径。
//
// 约束（见 docs/linux-port/03-runtime-model.md §"首进程"）：
//   部分米哈游游戏要求「游戏进程是 wine 的第一个进程」，否则反作弊/保护模块会崩溃。
//   因此这里不用 `wine start`、不套 shell、不前置任何 wrapper 进程，
//   直接 `wine <game.exe> <args>` —— 游戏自己就是 wine 的首个被启动进程。
//
// 组件解析顺序（M1 简化版，M2 换成完整 Component/Profile 解析）：
//   1. 环境变量 LUNARWARD_WINE
//   2. Steam 的 Proton Experimental（files/bin/wine）
//   3. PATH 里的 wine
//
// 前缀解析顺序（同上）：
//   1. 环境变量 LUNARWARD_WINEPREFIX
//   2. ~/.wine（已存在时）
//   3. ~/.local/share/lunarward/prefix/<game>（不存在则报错，M2 由 Profile 创建）
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Starward.Core;
using Starward.Features.Runtime;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Starward.Features.GameLauncher;

internal partial class GameLauncherService
{
    /// <summary>Linux 下的 prefix 根目录（XDG_DATA_HOME/lunarward/prefix）。</summary>
    private static string PrefixRoot
    {
        get
        {
            string data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x
                ? x
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(data, "lunarward", "prefix");
        }
    }

    /// <summary>按优先级找 wine 可执行文件；找不到返回 null。</summary>
    internal static string? ResolveWine()
    {
        string? env = Environment.GetEnvironmentVariable("LUNARWARD_WINE");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env))
        {
            return env;
        }

        // Steam 自带的 Proton：不经过 proton 脚本、直接用它内置的 wine，
        // 这样游戏仍然是 wine 的首个进程（用 proton 脚本会多一层 exec 包装）。
        string steam = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "Steam", "steamapps", "common");
        foreach (var dir in new[] { "Proton - Experimental", "Proton 9.0", "Proton Hotfix" })
        {
            var candidate = Path.Combine(steam, dir, "files", "bin", "wine");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(path))
        {
            foreach (var dir in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = Path.Combine(dir, "wine");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        return null;
    }

    /// <summary>解析本次启动要用的 WINEPREFIX；返回 null 表示没有可用的前缀。</summary>
    internal static string? ResolvePrefix(string gameKey)
    {
        string? env = Features.Runtime.PrefixManager.ExpandPath(Environment.GetEnvironmentVariable("LUNARWARD_WINEPREFIX"));
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
        {
            return env;
        }
        string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wine");
        if (Directory.Exists(legacy))
        {
            return legacy;
        }
        string perGame = Path.Combine(PrefixRoot, gameKey);
        return Directory.Exists(perGame) ? perGame : null;
    }

    /// <summary>
    /// 构造启动游戏的 ProcessStartInfo。
    /// <paramref name="exe"/> 为 .exe 时经 wine 启动，为原生可执行文件时直接启动（兼容第三方工具）。
    /// </summary>
    /// <summary>游戏运行期间持有的前缀锁（同一前缀不允许两个实例同时跑）。</summary>
    private static FileStream? _prefixLockStream;


    /// <summary>独占该前缀；被占用时返回 false（不阻断，由预检决定）。</summary>
    private static bool TryLockPrefix(string prefix)
    {
        try
        {
            string lockFile = Path.Combine(prefix, ".lunarward", "lock");
            Directory.CreateDirectory(Path.GetDirectoryName(lockFile)!);
            Features.PlayTime.PlayTimeSession.ActivePrefix = prefix;
            _prefixLockStream = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            _prefixLockStream.WriteByte((byte)'1');
            _prefixLockStream.Flush();
            return true;
        }
        catch
        {
            // 被占用是正常情况（另一个实例在跑），由预检的「前缀占用」项报告给用户
            return false;
        }
    }


    private static void ReleasePrefixLock()
    {
        try
        {
            _prefixLockStream?.Dispose();
        }
        catch
        {
        }
        _prefixLockStream = null;
    }


    private static void RunExitCompanions()
    {
        try
        {
            GameBiz biz = AppConfig.CurrentGameBiz;
            var runner = AppConfig.GetService<Features.Companion.CompanionRunner>();
            Task ignored = runner.StartCompanionsAsync(biz, "on_game_exit");
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "on_game_exit companions failed");
        }
    }


    /// <summary>
    /// 游戏进程退出后：释放前缀锁，并触发 on_game_exit 的后启动程序（D24）。
    /// 前缀不再有「临时模式」（D73 §3.1），想用完即弃就让路径落在临时目录、自己删。
    /// </summary>
    private static void AttachSessionCleanup(Process? process)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // 不能用 process.Exited：wine 的 loader 常常在游戏真正退出前就先退了，
                // 那样会导致「前缀锁提前释放、临时前缀被删掉"（L-62）。改用 wine 会话结束作为判据。
                if (process is not null)
                {
                    try
                    {
                        await Features.PlayTime.PlayTimeSession.WaitForSessionEndAsync(process.Id, Features.PlayTime.PlayTimeSession.ActivePrefix);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "Wait for wine session end failed; cleaning up anyway");
                    }
                }
            }
            finally
            {
                ReleasePrefixLock();
                RunExitCompanions();
            }
        });
    }


    private ProcessStartInfo CreateGameProcessStartInfo(string exe, string? arg, out bool viaWine)
    {
        string? workDir = Path.GetDirectoryName(exe);
        var info = new ProcessStartInfo
        {
            UseShellExecute = false,
            WorkingDirectory = workDir,
        };

        // D27：capture 永远开着（崩溃时能翻日志），下方确定运行器后再打开重定向。

        bool isWindowsExe = exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);
        viaWine = isWindowsExe;

        if (!isWindowsExe)
        {
            info.FileName = exe;
            if (!string.IsNullOrWhiteSpace(arg))
            {
                foreach (var a in SplitArguments(arg))
                {
                    info.ArgumentList.Add(a);
                }
            }
            return info;
        }

        // M2：优先按该游戏绑定的 Profile 决定运行器与前缀（未绑定则回落到默认 Profile）
        Features.Runtime.RuntimeProfile? profile = null;
        try
        {
            profile = Features.Runtime.ProfileBinding.Resolve(AppConfig.CurrentGameBiz);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resolve profile failed, fallback to environment defaults.");
        }

        // D27：输出捕获与可见性级别无关，永远开着重定向；panel 只是把捕获结果显示出来（M3 面板 UI）。
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;

        string? wine = !string.IsNullOrWhiteSpace(profile?.RunnerPath) && File.Exists(profile!.RunnerPath)
            ? profile.RunnerPath
            : ResolveWine();
        if (wine is null)
        {
            throw new FileNotFoundException(
                "未找到 wine。请设置环境变量 LUNARWARD_WINE，或安装 Proton/wine 后重试。");
        }
        string key = Path.GetFileNameWithoutExtension(exe);
        string? prefix;
        if (profile is not null)
        {
            try
            {
                // 前缀不存在就要跑 wineboot（约十几秒），先给用户一个明确提示（D73 §3.1：前缀只是路径）
                string expected = Features.Runtime.EnvironmentResolver.EffectivePrefixPath(profile);
                if (!Directory.Exists(expected))
                {
                    Helpers.InAppToast.MainWindow?.Information("首次启动：正在初始化 Wine 前缀，可能需要十几秒…", null, 8000);
                }
                prefix = Features.Runtime.PrefixManager.EnsurePrefix(profile, key.ToLowerInvariant(), _logger);
                TryLockPrefix(prefix);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ensure prefix failed, fallback to environment defaults.");
                prefix = ResolvePrefix(key.ToLowerInvariant());
            }
        }
        else
        {
            prefix = ResolvePrefix(key.ToLowerInvariant());
        }
        if (prefix is null)
        {
            throw new DirectoryNotFoundException(
                $"未找到可用的 wine prefix。请设置 LUNARWARD_WINEPREFIX，或先创建 {Path.Combine(PrefixRoot, key.ToLowerInvariant())}。");
        }

        // M2：启动前把选定的 DXVK/VKD3D 装进前缀（失败只告警，不阻断启动——组件缺失时游戏仍可用自带 d3d 实现）
        if (profile is not null && !string.IsNullOrWhiteSpace(profile.GraphicsId))
        {
            try
            {
                Features.Runtime.Component? graphics = Features.Runtime.ComponentStore
                    .GetGraphics()
                    .FirstOrDefault(c => c.Id == profile.GraphicsId);
                if (graphics is not null)
                {
                    bool changed = Features.Runtime.GraphicsInstaller.EnsureInstalled(prefix, graphics, _logger);
                    if (changed)
                    {
                        _logger.LogInformation("Graphics prepared: {name}", graphics.Name);
                    }
                }
                else
                {
                    _logger.LogWarning("Graphics component not found: {id}", profile.GraphicsId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ensure graphics failed, continue without it.");
            }
        }
        else if (profile is not null)
        {
            _logger.LogDebug("Profile has no graphics component selected; skip install.");
        }

        info.Environment["WINEPREFIX"] = prefix;
        // 记录本次真正用的运行器：记录子进程靠它挑对的 wineserver（自定义运行器 + 系统 wineserver 混用会让 -w 立刻返回）。
        Features.PlayTime.PlayTimeSession.ActiveWine = wine;
        // 不接管 wine 的输出：游戏日志独立，避免阻塞 GUI 线程（D27 输出可见性模型）。
        info.Environment["WINEDEBUG"] = info.Environment.TryGetValue("WINEDEBUG", out var dbg) ? dbg : "-all";
        ApplyWineRuntimeEnvironment(info, wine);

        // D73 §4.3/§4.4：配方 env 覆盖运行器自动注入的值（覆盖要留痕），wrappers 按顺序排在 wine 前面。
        var autoEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in info.Environment)
        {
            autoEnvironment[pair.Key] = pair.Value ?? "";
        }
        string effectiveArgs = string.IsNullOrWhiteSpace(profile?.Args) ? (arg ?? "") : profile!.Args;
        Features.Runtime.LaunchRecipe recipe = Features.Runtime.LaunchRecipeBuilder.Build(
            profile, wine, prefix, exe, effectiveArgs, autoEnvironment);
        if (profile?.Env is { Count: > 0 })
        {
            foreach ((string envKey, string envValue) in profile.Env)
            {
                info.Environment[envKey] = envValue;
            }
        }
        foreach (Features.Runtime.EnvOverride overridden in recipe.Overrides)
        {
            _logger.LogWarning("环境变量 {key} 被配方覆盖：自动注入 {auto} → 配方 {recipe}", overridden.Key, overridden.AutoValue, overridden.RecipeValue);
        }
        info.FileName = recipe.Argv[0];
        info.ArgumentList.Clear();
        for (int i = 1; i < recipe.Argv.Count; i++)
        {
            info.ArgumentList.Add(recipe.Argv[i]);
        }
        return info;
    }

    /// <summary>
    /// 给「直接调 Proton 的 <c>files/bin/wine</c>」补上 Proton 运行时环境。
    /// 不补的话 wine 找不到 Proton 自带的 builtin dll 与库，游戏往往启动几秒就退出（不是崩溃日志能看出来的那种）。
    /// 这里只补环境变量，不引入 Proton 的 python 启动器，也不改「游戏是 wine 首个进程」的约束。
    /// </summary>
    internal static void ApplyWineRuntimeEnvironment(ProcessStartInfo info, string winePath)
    {
        string full = Path.GetFullPath(winePath);
        int idx = full.IndexOf("/steamapps/common/", StringComparison.Ordinal);
        if (idx < 0)
        {
            return;
        }
        // wine = <steam>/steamapps/common/<tool>/files/bin/wine
        string steamRoot = full[..idx];
        string toolRoot = full[..full.IndexOf("/files/", StringComparison.Ordinal)];
        string files = Path.Combine(toolRoot, "files");

        string[] dllPathCandidates = { Path.Combine(files, "lib", "wine"), Path.Combine(files, "lib64", "wine") };
        string? dllPath = dllPathCandidates.FirstOrDefault(Directory.Exists);
        if (dllPath is not null)
        {
            string existing = info.Environment.TryGetValue("WINEDLLPATH", out string? old) ? old! : "";
            info.Environment["WINEDLLPATH"] = string.IsNullOrEmpty(existing) ? dllPath : dllPath + ":" + existing;
        }

        string[] libCandidates =
        {
            Path.Combine(files, "lib", "x86_64-linux-gnu"),
            Path.Combine(files, "lib", "i386-linux-gnu"),
            Path.Combine(files, "lib64"),
            Path.Combine(files, "lib"),
        };
        string[] libs = libCandidates.Where(Directory.Exists).ToArray();
        if (libs.Length > 0)
        {
            string existing = info.Environment.TryGetValue("LD_LIBRARY_PATH", out string? old) ? old! : "";
            string joined = string.Join(":", libs);
            info.Environment["LD_LIBRARY_PATH"] = string.IsNullOrEmpty(existing) ? joined : joined + ":" + existing;
        }

        info.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = steamRoot;
        info.Environment.TryAdd("STEAM_COMPAT_INSTALL_PATH", toolRoot);
        info.Environment["PROTONPATH"] = toolRoot;
    }

    /// <summary>
    /// 只解析不启动：给 CLI（`lunarward --launch-plan &lt;exe&gt;`）与调试 shell 用，
    /// 用来确认 wine / prefix / 参数解析结果，不产生任何副作用。
    /// </summary>
    internal static string DescribeLaunchPlan(string exe, string? arg, string? profileHint = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"exe     : {exe}");
        sb.AppendLine($"exists  : {File.Exists(exe)}");

        // 和真启动走同一套解析：绑定 → 配方 → argv/env 覆盖（D73 §4）。
        RuntimeProfile? profile = null;
        if (!string.IsNullOrWhiteSpace(profileHint))
        {
            profile = Features.Runtime.RuntimeProfileStore.LoadAll()
                .FirstOrDefault(x => string.Equals(x.Id, profileHint, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(x.Name, profileHint, StringComparison.OrdinalIgnoreCase));
            if (profile is null)
            {
                sb.AppendLine($"配方    : 找不到「{profileHint}」");
            }
        }
        else
        {
            profile = Features.Runtime.ProfileBinding.Resolve(AppConfig.CurrentGameBiz);
        }

        if (profile is null)
        {
            sb.AppendLine("配方    : (没有可用的运行环境，按系统 wine 兜底)");
            string? bareWine = ResolveWine();
            sb.AppendLine($"wine    : {bareWine ?? "(未找到)"}");
            string? barePrefix = ResolvePrefix(Path.GetFileNameWithoutExtension(exe).ToLowerInvariant());
            sb.AppendLine($"prefix  : {barePrefix ?? "(未找到)"}");
            return sb.ToString();
        }

        IReadOnlyList<Component> runners = ComponentStore.GetRunners();
        Component? runner = Features.Runtime.EnvironmentResolver.ResolveRunner(profile, runners);
        string? wine = runner?.Path ?? ResolveWine();
        string prefix = Features.Runtime.EnvironmentResolver.EffectivePrefixPath(profile);

        sb.AppendLine($"配方    : {profile.Name} ({profile.Id})");
        sb.AppendLine($"运行器  : {runner?.DisplayName ?? "(未找到，退系统 wine)"}");
        sb.AppendLine($"wine    : {wine ?? "(未找到)"}");
        sb.AppendLine($"前缀    : {prefix}{(Directory.Exists(prefix) ? "" : " （不存在，启动时创建）")}");
        if (wine is null)
        {
            return sb.ToString();
        }

        var auto = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["WINEPREFIX"] = prefix,
            ["WINEDEBUG"] = "-all",
        };
        LaunchRecipe recipe = LaunchRecipeBuilder.Build(profile, wine, prefix, exe, arg, auto);
        sb.AppendLine($"命令    : {recipe.CommandLine}");
        foreach ((string key, string value) in recipe.SortedEnvironment)
        {
            sb.AppendLine($"env     : {key}={value}");
        }
        foreach (EnvOverride item in recipe.Overrides)
        {
            sb.AppendLine($"覆盖    : {item.Key} 自动={item.AutoValue} → 配方={item.RecipeValue}");
        }
        return sb.ToString();
    }


    /// <summary>按 shell 规则粗略切分参数串（支持 "与 ' 包裹）。</summary>
    internal static IEnumerable<string> SplitArguments(string raw)
    {
        var sb = new StringBuilder();
        char quote = '\0';
        foreach (char c in raw)
        {
            if (quote != '\0')
            {
                if (c == quote) { quote = '\0'; }
                else { sb.Append(c); }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (char.IsWhiteSpace(c))
            {
                if (sb.Length > 0) { yield return sb.ToString(); sb.Clear(); }
            }
            else
            {
                sb.Append(c);
            }
        }
        if (sb.Length > 0) { yield return sb.ToString(); }
    }
}
