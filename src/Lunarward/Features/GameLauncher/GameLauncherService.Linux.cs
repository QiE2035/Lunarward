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
    /// <summary>游戏运行期间持有的前缀锁（同一前缀不允许两个实例同时跑）。按前缀分槽，避免第二个会话把第一个的锁顶掉（R05）。</summary>
    private static readonly Dictionary<string, FileStream> _prefixLocks = new(StringComparer.Ordinal);


    private static string NormalizePrefixKey(string prefix)
    {
        string full = Path.GetFullPath(prefix);
        return full.Length > 1 ? full.TrimEnd(Path.DirectorySeparatorChar) : full;
    }


    /// <summary>独占该前缀；被占用时返回 false。</summary>
    private static bool TryLockPrefix(string prefix)
    {
        string key = NormalizePrefixKey(prefix);
        lock (_prefixLocks)
        {
            if (_prefixLocks.ContainsKey(key))
            {
                return false;
            }
            try
            {
                string lockFile = Path.Combine(key, ".lunarward", "lock");
                Directory.CreateDirectory(Path.GetDirectoryName(lockFile)!);
                var stream = new FileStream(lockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                stream.WriteByte((byte)'1');
                stream.Flush();
                _prefixLocks[key] = stream;
                Features.PlayTime.PlayTimeSession.ActivePrefix = key;
                return true;
            }
            catch (IOException)
            {
                // 被另一个实例占用是正常情况，由调用方转成「前缀被占用」。
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                Serilog.Log.Warning("Prefix lock is not writable: {prefix}", prefix);
                return false;
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Try lock prefix failed: {prefix}", prefix);
                return false;
            }
        }
    }


    private static void ReleasePrefixLock(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return;
        }
        string key = NormalizePrefixKey(prefix);
        FileStream? stream = null;
        lock (_prefixLocks)
        {
            if (_prefixLocks.Remove(key, out FileStream? held))
            {
                stream = held;
            }
        }
        try
        {
            stream?.Dispose();
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Dispose prefix lock failed: {prefix}", prefix);
        }
    }


    /// <summary>拿前缀锁；被占用时抛出可读错误（R05/R46）。</summary>
    private static void AcquirePrefixLock(string prefix)
    {
        if (!TryLockPrefix(prefix))
        {
            throw new InvalidOperationException($"前缀正被另一个实例使用：{prefix}。请先关闭正在运行的游戏，或换一个前缀。");
        }
    }


    private static void RunExitCompanions(GameBiz biz)
    {
        try
        {
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
    private static void AttachSessionCleanup(Process? process, GameBiz gameBiz, string? prefix)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                // 不能用 process.Exited：wine 的 loader 常常在游戏真正退出前就先退了，
                // 那样会导致「前缀锁提前释放、临时前缀被删掉"（L-62）。改用 wine 会话结束作为判据。
                // 前缀显式传进来（R05）：多会话时不依赖静态 ActivePrefix，否则 A 的清理会释放 B 的锁。
                if (process is not null)
                {
                    try
                    {
                        await Features.PlayTime.PlayTimeSession.WaitForSessionEndAsync(process.Id, prefix, gameBiz: gameBiz);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "Wait for wine session end failed; cleaning up anyway");
                    }
                }
            }
            finally
            {
                ReleasePrefixLock(prefix);
                RunExitCompanions(gameBiz);
            }
        });
    }


    private ProcessStartInfo CreateGameProcessStartInfo(GameBiz gameBiz, string exe, string? arg, out bool viaWine)
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
            // B31：运行时配方按「本次启动的游戏」解析，deep link/CLI 启动非当前选中游戏时才是对的。
            profile = Features.Runtime.ProfileBinding.Resolve(gameBiz);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resolve profile failed, fallback to environment defaults.");
        }

        // D27：输出捕获与可见性级别无关，永远开着重定向；panel 只是把捕获结果显示出来（M3 面板 UI）。
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        // R107：不指定就按 locale 猜（LANG=C 时中文/俄文输出全是问号）；wine 与游戏都给 UTF-8 更稳。
        info.StandardOutputEncoding = System.Text.Encoding.UTF8;
        info.StandardErrorEncoding = System.Text.Encoding.UTF8;

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
        // R49：前缀校验失败降级到 ~/.wine 时，不能再把图形层装进这个降级前缀（会影响全机 wine 程序）。
        bool degradedPrefix = false;
        if (profile is not null)
        {
            string expected = Features.Runtime.EnvironmentResolver.EffectivePrefixPath(profile);
            // 前缀不存在就要跑 wineboot（约十几秒），先给用户一个明确提示（D73 §3.1：前缀只是路径）
            if (!Directory.Exists(expected))
            {
                Helpers.InAppToast.MainWindow?.Information("首次启动：正在初始化 Wine 前缀，可能需要十几秒…", null, 8000);
            }
            // R46：先拿锁、再建前缀。以前 wineboot 在锁外执行，两个实例同时首启同一方案会一起写
            // drive_c / 注册表，前缀半损坏而两边都继续启动。
            prefix = expected;
            AcquirePrefixLock(prefix);
            try
            {
                prefix = Features.Runtime.PrefixManager.EnsurePrefix(profile, key.ToLowerInvariant(), _logger);
                if (!string.Equals(NormalizePrefixKey(prefix), NormalizePrefixKey(expected), StringComparison.Ordinal))
                {
                    // EnsurePrefix 返回了别的路径（老配置/显式覆盖）时，锁要跟着换到真实前缀上。
                    ReleasePrefixLock(expected);
                    AcquirePrefixLock(prefix);
                }
            }
            catch (Exception ex)
            {
                ReleasePrefixLock(prefix);
                ReleasePrefixLock(expected);
                // 不再静默降级：用户配的前缀坏了还照常启动、换个前缀跑，是最难查的一类「假生效」（R03）。
                degradedPrefix = true;
                _logger.LogError(ex, "Ensure prefix failed, falling back to environment defaults.");
                Helpers.InAppToast.MainWindow?.Warning(null, $"前缀创建/校验失败，本次改用默认前缀：{ex.Message}", 8000);
                prefix = ResolvePrefix(key.ToLowerInvariant());
                if (prefix is null)
                {
                    throw new DirectoryNotFoundException(
                        $"未找到可用的 wine prefix。请设置 LUNARWARD_WINEPREFIX，或先创建 {Path.Combine(PrefixRoot, key.ToLowerInvariant())}。");
                }
                AcquirePrefixLock(prefix);
            }
        }
        else
        {
            prefix = ResolvePrefix(key.ToLowerInvariant());
            if (prefix is null)
            {
                throw new DirectoryNotFoundException(
                    $"未找到可用的 wine prefix。请设置 LUNARWARD_WINEPREFIX，或先创建 {Path.Combine(PrefixRoot, key.ToLowerInvariant())}。");
            }
            AcquirePrefixLock(prefix);
        }

        // R48：锁已经拿到，后面任何异常（装配/参数/Start 前的失败）都必须释放它，
        // 否则该前缀在整个进程生命周期内都显示「正被另一个实例使用」。
        try
        {
            // M2：启动前把选定的 DXVK / VKD3D 装进前缀（D83 两类各自独立，可以同时装；
            // 失败只告警，不阻断启动——组件缺失时游戏仍可用自带 d3d 实现）
            if (profile is not null && !degradedPrefix)
            {
                try
                {
                    List<Features.Runtime.Component> graphics = Features.Runtime.ComponentStore
                        .GetGraphics()
                        .Where(c => c.Id == profile.DxvkId || c.Id == profile.Vkd3dId)
                        .ToList();
                    if (graphics.Count > 0)
                    {
                        bool changed = Features.Runtime.GraphicsInstaller.EnsureInstalled(prefix, graphics, _logger);
                        if (changed)
                        {
                            _logger.LogInformation("Graphics prepared: {names}", string.Join(", ", graphics.Select(x => x.Name)));
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Profile has no graphics component selected; skip install.");
                    }
                    if (!string.IsNullOrWhiteSpace(profile.DxvkId) && graphics.All(c => c.Id != profile.DxvkId))
                    {
                        _logger.LogWarning("DXVK component not found: {id}", profile.DxvkId);
                    }
                    if (!string.IsNullOrWhiteSpace(profile.Vkd3dId) && graphics.All(c => c.Id != profile.Vkd3dId))
                    {
                        _logger.LogWarning("VKD3D component not found: {id}", profile.Vkd3dId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Ensure graphics failed, continue without it.");
                }
            }

            info.Environment["WINEPREFIX"] = prefix;
            // 记录本次真正用的运行器：记录子进程靠它挑对的 wineserver（自定义运行器 + 系统 wineserver 混用会让 -w 立刻返回）。
            Features.PlayTime.PlayTimeSession.ActiveWine = wine;
            // 不接管 wine 的输出：游戏日志独立，避免阻塞 GUI 线程（D27 输出可见性模型）。
            info.Environment["WINEDEBUG"] = info.Environment.TryGetValue("WINEDEBUG", out var dbg) ? dbg : "-all";

            // R19：自动注入层与调试 Shell / RunOnce 共用同一份装配（含 Proton 变量与增强开关）。
            Dictionary<string, string> autoEnvironment = Features.Runtime.RuntimeEnvironment.Assemble(profile, key, prefix);
            // R02：参数合并只在 Build 里做一次（追加，不替换）——这里传游戏自带参数原始值。
            // R18：PATH 里没有的 wrapper 跳过并记下来，启动前提示；不再让 Process.Start 直接抛。
            Features.Runtime.LaunchRecipe recipe = Features.Runtime.LaunchRecipeBuilder.Build(
                profile, wine, prefix, exe, arg, autoEnvironment,
                wrapper => Features.Runtime.Enhancements.FindInPath(wrapper) is not null);
            // R01：以 recipe.Environment 为唯一依据整体下发（继承环境 + Proton 变量 + 增强 HUD + 配方覆盖，
            // 且 WINEPREFIX 已被 Build 固定成真实前缀）。之前只回灌 profile.Env，导致 HUD 全静默失效、
            // 用户写 WINEPREFIX 还能把加锁/DXVK 用的前缀和进程实际前缀拆成两个。
            foreach ((string envKey, string envValue) in recipe.Environment)
            {
                info.Environment[envKey] = envValue;
            }
            if (recipe.MissingWrappers.Count > 0)
            {
                string names = string.Join("、", recipe.MissingWrappers);
                _logger.LogWarning("Wrappers not found in PATH, skipped: {names}", names);
                Helpers.InAppToast.MainWindow?.Warning(null, $"未找到启动包装程序，已跳过：{names}", 8000);
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
        catch
        {
            ReleasePrefixLock(prefix);
            throw;
        }
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
        int filesIndex = full.IndexOf("/files/", StringComparison.Ordinal);
        if (filesIndex < 0)
        {
            return;
        }
        string toolRoot = full[..filesIndex];
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
        // R108：与 RuntimeEnvironment 共用「前置 + 保留旧值」的实现，别再各写一套。
        Features.Runtime.RuntimeEnvironment.PrependLibraryPath(info.Environment, libCandidates.Where(Directory.Exists));

        info.Environment["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = steamRoot;
        info.Environment.TryAdd("STEAM_COMPAT_INSTALL_PATH", toolRoot);
        info.Environment["PROTONPATH"] = toolRoot;
    }

    /// <summary>
    /// 只解析不启动：给 CLI（`lunarward --launch-plan &lt;exe&gt;`）与调试 shell 用，
    /// 用来确认 wine / prefix / 参数解析结果，不产生任何副作用。
    /// </summary>
    internal static string DescribeLaunchPlan(string exe, string? arg, string? profileHint = null, GameBiz? gameBizHint = null)
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
            // B31：运行时配方按「本次启动的游戏」解析，deep link/CLI 启动非当前选中游戏时才是对的。
            profile = Features.Runtime.ProfileBinding.Resolve(gameBizHint ?? AppConfig.CurrentGameBiz);
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

        // R38：与真实启动同源——用同一份装配、同一套 wrapper 过滤，plan 里看到的就是启动时的命令行。
        Dictionary<string, string> auto = Features.Runtime.RuntimeEnvironment.Assemble(profile, null, prefix);
        LaunchRecipe recipe = LaunchRecipeBuilder.Build(profile, wine, prefix, exe, arg, auto,
            wrapper => Features.Runtime.Enhancements.FindInPath(wrapper) is not null);
        sb.AppendLine($"命令    : {recipe.CommandLine}");
        if (recipe.MissingWrappers.Count > 0)
        {
            sb.AppendLine($"包装    : 未找到并跳过 {string.Join("、", recipe.MissingWrappers)}");
        }
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
