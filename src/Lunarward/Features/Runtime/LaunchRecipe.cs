// Lunarward：运行环境的「展开」（D73 §4.3 / §4.4）。
//
// 环境是配方，展开结果（LaunchRecipe）才是运行依据：argv、env、被覆盖的自动变量都在这里算出来，
// 界面预览与 --launch-plan 打的是同一份结果。
// 这里全是纯函数，不碰进程、不扫盘（便于单测）。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Starward.Features.Runtime;


/// <summary>配方 env 覆盖了运行器自动注入的同名变量（D73 §4.3：要标红，不静默）。</summary>
public sealed record EnvOverride(string Key, string AutoValue, string RecipeValue);


public sealed class LaunchRecipe
{
    public string RunnerPath { get; init; } = "";

    public string PrefixPath { get; init; } = "";

    /// <summary>最终 argv（不含 argv[0] 之外的解释层）：wrappers… + runner + exe + args。</summary>
    public List<string> Argv { get; init; } = new();

    public Dictionary<string, string> Environment { get; init; } = new(StringComparer.Ordinal);

    public List<EnvOverride> Overrides { get; init; } = new();

    /// <summary>PATH 里找不到、被跳过的 wrapper（R18：缺程序时跳过并提示，不让启动直接失败）。</summary>
    public List<string> MissingWrappers { get; init; } = new();


    /// <summary>可直接贴进终端的一行命令（带引号）。</summary>
    /// <summary>R109：被丢掉的 env 键（非法字符 / 值带 NUL），界面与预检要提示，不能静默。</summary>
    public List<string> InvalidEnvironmentKeys { get; init; } = new();


    public string CommandLine => LaunchRecipeBuilder.JoinCommandLine(Argv);


    public IEnumerable<KeyValuePair<string, string>> SortedEnvironment =>
        Environment.OrderBy(x => x.Key, StringComparer.Ordinal);
}


public static class LaunchRecipeBuilder
{

    /// <summary>
    /// 解析一行参数：支持单/双引号包裹与反斜杠转义，不做变量展开（不是 shell）。
    /// </summary>
    public static List<string> SplitArgs(string? text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }
        var current = new StringBuilder();
        char quote = '\0';
        bool has = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quote == '\0' && (c == '"' || c == '\''))
            {
                quote = c;
                has = true;
                continue;
            }
            if (quote != '\0' && c == quote)
            {
                quote = '\0';
                continue;
            }
            if (c == '\\' && i + 1 < text.Length)
            {
                char next = text[i + 1];
                // 只吞「转义引号/转义反斜杠」这一种反斜杠：
                // 引号内是 \" 与 \\（Windows 风格），引号外只有 \' 与 \"；
                // 其余反斜杠是字面量，否则 C:\Games\… 这种参数会被吃成 C:Gamess…（R36）。
                if (quote != '\0' ? (next == quote || next == '\\') : (next == '"' || next == '\''))
                {
                    current.Append(next);
                    i++;
                    has = true;
                    continue;
                }
            }
            if (quote == '\0' && char.IsWhiteSpace(c))
            {
                if (has || current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    has = false;
                }
                continue;
            }
            current.Append(c);
            has = true;
        }
        if (has || current.Length > 0)
        {
            result.Add(current.ToString());
        }
        return result;
    }


    /// <summary>
    /// argv = 每个 wrapper 拆成若干 token（按顺序） + 运行器 + 游戏 exe + 启动参数。
    /// </summary>
    public static List<string> BuildArgv(IReadOnlyList<string>? wrappers, string runnerPath, string exePath,
        string? extraArgs)
    {
        return BuildArgv(wrappers, runnerPath, exePath, extraArgs, null, null);
    }


    public static List<string> BuildArgv(IReadOnlyList<string>? wrappers, string runnerPath, string exePath,
        string? extraArgs, Func<string, bool>? wrapperAvailable, List<string>? missingWrappers)
        => BuildArgv(wrappers, runnerPath, exePath, SplitArgs(extraArgs), wrapperAvailable, missingWrappers);


    /// <summary>R110：参数已经拆好再进来——游戏自带参数与配方参数各拆各的，不拼成一行再回炉（引号跨层会吃字符）。</summary>
    public static List<string> BuildArgv(IReadOnlyList<string>? wrappers, string runnerPath, string exePath,
        IEnumerable<string> extraArgs, Func<string, bool>? wrapperAvailable, List<string>? missingWrappers)
    {
        var argv = new List<string>();
        if (wrappers is not null)
        {
            foreach (string wrapper in wrappers)
            {
                if (string.IsNullOrWhiteSpace(wrapper))
                {
                    continue;
                }
                List<string> tokens = SplitArgs(wrapper);
                if (tokens.Count == 0)
                {
                    continue;
                }
                if (wrapperAvailable is not null && !wrapperAvailable(tokens[0]))
                {
                    missingWrappers?.Add(wrapper);
                    continue;
                }
                argv.AddRange(tokens);
            }
        }
        if (!string.IsNullOrWhiteSpace(runnerPath))
        {
            argv.Add(runnerPath);
        }
        if (!string.IsNullOrWhiteSpace(exePath))
        {
            argv.Add(exePath);
        }
        argv.AddRange(extraArgs);
        return argv;
    }


    /// <summary>
    /// env 三层合并：自动注入 → 配方 env → 进程环境（进程环境覆盖由 OS 自己完成，这里只算前两层）。
    /// 配方覆盖了自动注入的 key 会被记进 <paramref name="overrides"/>，供界面标红。
    /// </summary>
    public static Dictionary<string, string> MergeEnvironment(
        IReadOnlyDictionary<string, string> auto,
        IReadOnlyDictionary<string, string>? recipe,
        out List<EnvOverride> overrides)
        => MergeEnvironment(auto, recipe, out overrides, out _);


    /// <summary>
    /// R109：env 键必须能真的进环境（`^[A-Za-z_][A-Za-z0-9_]*$`），值不能带 NUL；
    /// 已知键大小写归一（以前 `wineprefix` 与 `WINEPREFIX` 被当成两个键，后者静默覆盖前者）。
    /// </summary>
    public static Dictionary<string, string> MergeEnvironment(
        IReadOnlyDictionary<string, string> auto,
        IReadOnlyDictionary<string, string>? recipe,
        out List<EnvOverride> overrides,
        out List<string> invalidKeys)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in auto)
        {
            merged[key] = value;
        }
        overrides = new List<EnvOverride>();
        invalidKeys = new List<string>();
        if (recipe is null)
        {
            return merged;
        }
        foreach ((string rawKey, string value) in recipe)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                continue;
            }
            if (!IsValidEnvKey(rawKey) || value.Contains('\0'))
            {
                invalidKeys.Add(rawKey);
                continue;
            }
            string key = CanonicalEnvKey(rawKey);
            if (auto.TryGetValue(key, out string? autoValue) && !string.Equals(autoValue, value, StringComparison.Ordinal))
            {
                overrides.Add(new EnvOverride(key, autoValue, value));
            }
            merged[key] = value;
        }
        overrides.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
        return merged;
    }


    private static readonly Regex EnvKeyPattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>R109：键是否像环境变量名（否则 wine / execve 会静默忽略，用户却以为生效了）。</summary>
    public static bool IsValidEnvKey(string key) => EnvKeyPattern.IsMatch(key);

    private static readonly Dictionary<string, string> CanonicalKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WINEPREFIX"] = "WINEPREFIX",
        ["WINEDEBUG"] = "WINEDEBUG",
        ["WINEDLLPATH"] = "WINEDLLPATH",
        ["WINEDLLOVERRIDES"] = "WINEDLLOVERRIDES",
        ["WINEESYNC"] = "WINEESYNC",
        ["WINEFSYNC"] = "WINEFSYNC",
        ["PATH"] = "PATH",
        ["LD_LIBRARY_PATH"] = "LD_LIBRARY_PATH",
        ["LD_PRELOAD"] = "LD_PRELOAD",
        ["DXVK_HUD"] = "DXVK_HUD",
        ["DXVK_STATE_CACHE"] = "DXVK_STATE_CACHE",
        ["DXVK_CONFIG_FILE"] = "DXVK_CONFIG_FILE",
        ["MANGOHUD"] = "MANGOHUD",
        ["MANGOHUD_CONFIG"] = "MANGOHUD_CONFIG",
        ["PROTONPATH"] = "PROTONPATH",
        ["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = "STEAM_COMPAT_CLIENT_INSTALL_PATH",
        ["STEAM_COMPAT_INSTALL_PATH"] = "STEAM_COMPAT_INSTALL_PATH",
        ["LUNARWARD_PROFILE"] = "LUNARWARD_PROFILE",
        ["LUNARWARD_PROFILE_ID"] = "LUNARWARD_PROFILE_ID",
    };

    /// <summary>R109：已知键统一成大写写法；未知键原样保留（别把用户自己的变量名改了）。</summary>
    public static string CanonicalEnvKey(string key)
        => CanonicalKeys.TryGetValue(key, out string? canonical) ? canonical : key;


    public static LaunchRecipe Build(
        RuntimeProfile? profile,
        string runnerPath,
        string prefixPath,
        string exePath,
        string? gameArgs,
        IReadOnlyDictionary<string, string> autoEnvironment,
        Func<string, bool>? wrapperAvailable = null)
    {
        // 增强开关（D81）展开成「自动注入层」的同名变量：用户在配方 env 里手写 DXVK_HUD 依然能覆盖它，并会被标红。
        var auto = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in autoEnvironment)
        {
            auto[key] = value;
        }
        foreach ((string key, string value) in Enhancements.EnvironmentVariables(profile))
        {
            auto[key] = value;
        }
        Dictionary<string, string> merged = MergeEnvironment(auto, profile?.Env, out List<EnvOverride> overrides,
            out List<string> invalidEnvKeys);
        if (!string.IsNullOrWhiteSpace(prefixPath))
        {
            // 配方自己写 WINEPREFIX 会和「前缀检查用的路径」打架：保留真实前缀，但把这次覆盖记下来。
            // 自动注入层里的 WINEPREFIX 是我们自己填的（对不对由前缀检查负责），不算用户覆盖，否则每次启动都会报一条假冲突。
            if (profile?.Env.TryGetValue("WINEPREFIX", out string? recipePrefix) == true
                && !string.Equals(recipePrefix, prefixPath, StringComparison.Ordinal))
            {
                overrides.RemoveAll(x => x.Key == "WINEPREFIX");
                overrides.Add(new EnvOverride("WINEPREFIX", prefixPath, recipePrefix));
                overrides.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
            }
            merged["WINEPREFIX"] = prefixPath;
        }
        var missingWrappers = new List<string>();
        var extraArgs = SplitArgs(gameArgs);
        extraArgs.AddRange(SplitArgs(profile?.Args));
        List<string> argv = BuildArgv(Enhancements.Wrappers(profile), runnerPath, exePath,
            extraArgs, wrapperAvailable, missingWrappers);
        return new LaunchRecipe
        {
            RunnerPath = runnerPath,
            PrefixPath = prefixPath,
            // 配方参数只做追加，绝不替换游戏自带参数：login_auth_ticket / -use-d3d12 / -popupwindow 都在这边，
            // 一旦被吞掉扫码登录回填就静默失效（R02）。
            Argv = argv,
            Environment = merged,
            Overrides = overrides,
            InvalidEnvironmentKeys = invalidEnvKeys,
            MissingWrappers = missingWrappers,
        };
    }


    /// <summary>把 argv 拼成一行可复制的命令（含最小必要的引号）。</summary>
    public static string JoinCommandLine(IEnumerable<string> argv)
    {
        var sb = new StringBuilder();
        foreach (string token in argv)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }
            sb.Append(QuoteIfNeeded(token));
        }
        return sb.ToString();
    }


    private static string QuoteIfNeeded(string token)
    {
        if (token.Length > 0 && token.All(c => char.IsLetterOrDigit(c) || "/._-+=:,@%^".Contains(c)))
        {
            return token;
        }
        return "'" + token.Replace("'", "'\\''") + "'";
    }
}
