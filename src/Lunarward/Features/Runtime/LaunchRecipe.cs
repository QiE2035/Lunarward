// Lunarward：运行环境的「展开」（D73 §4.3 / §4.4）。
//
// 环境是配方，展开结果（LaunchRecipe）才是运行依据：argv、env、被覆盖的自动变量都在这里算出来，
// 界面预览与 --launch-plan 打的是同一份结果。
// 这里全是纯函数，不碰进程、不扫盘（便于单测）。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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


    /// <summary>可直接贴进终端的一行命令（带引号）。</summary>
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
            if (quote == '\0' && c == '\\' && i + 1 < text.Length)
            {
                current.Append(text[++i]);
                has = true;
                continue;
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
        var argv = new List<string>();
        if (wrappers is not null)
        {
            foreach (string wrapper in wrappers)
            {
                if (string.IsNullOrWhiteSpace(wrapper))
                {
                    continue;
                }
                argv.AddRange(SplitArgs(wrapper));
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
        argv.AddRange(SplitArgs(extraArgs));
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
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string key, string value) in auto)
        {
            merged[key] = value;
        }
        overrides = new List<EnvOverride>();
        if (recipe is null)
        {
            return merged;
        }
        foreach ((string key, string value) in recipe)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }
            if (auto.TryGetValue(key, out string? autoValue) && !string.Equals(autoValue, value, StringComparison.Ordinal))
            {
                overrides.Add(new EnvOverride(key, autoValue, value));
            }
            merged[key] = value;
        }
        overrides.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.OrdinalIgnoreCase));
        return merged;
    }


    public static LaunchRecipe Build(
        RuntimeProfile? profile,
        string runnerPath,
        string prefixPath,
        string exePath,
        string? gameArgs,
        IReadOnlyDictionary<string, string> autoEnvironment)
    {
        Dictionary<string, string> merged = MergeEnvironment(autoEnvironment, profile?.Env, out List<EnvOverride> overrides);
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
        return new LaunchRecipe
        {
            RunnerPath = runnerPath,
            PrefixPath = prefixPath,
            Argv = BuildArgv(profile?.Wrappers, runnerPath, exePath, string.IsNullOrWhiteSpace(profile?.Args) ? gameArgs : profile!.Args),
            Environment = merged,
            Overrides = overrides,
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
