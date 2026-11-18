// Lunarward：HUD / gamemode / gamescope 的展开（D81）。
//
// 对标 an-anime-team 的做法（`anime-launcher-sdk/src/config/schema_blanks/hud.rs`）：
//   HUD 是枚举，不是一个自由文本的环境变量；开 gamescope 时不注入 MANGOHUD，否则监控层会叠两层。
// 这里全是纯函数，不碰进程、不扫盘（PATH 查询单独放 FindInPath，便于测试时换成固定目录）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Starward.Features.Runtime;


public static class Enhancements
{

    public const string HudOff = "off";

    public const string HudDxvk = "dxvk";

    public const string HudMangoHud = "mangohud";

    /// <summary>DXVK HUD 的默认项（与 an-anime-team 一致）。</summary>
    public const string DefaultDxvkHud = "fps,frametimes,gpuload";

    /// <summary>MangoHud 的默认项；留空表示用用户自己的 MangoHud 配置。</summary>
    public const string DefaultMangoHudOptions = "";

    /// <summary>gamescope 没填参数时的默认值。</summary>
    public const string DefaultGameScopeArgs = "-f";


    public static string NormalizeHud(string? hud)
    {
        return (hud ?? "").Trim().ToLowerInvariant() switch
        {
            HudDxvk or "d3d" => HudDxvk,
            HudMangoHud or "mango" => HudMangoHud,
            _ => HudOff,
        };
    }


    public static string HudText(string? hud) => NormalizeHud(hud) switch
    {
        HudDxvk => "DXVK HUD",
        HudMangoHud => "MangoHud",
        _ => "关闭",
    };


    /// <summary>HUD 展开出的环境变量；开 gamescope 时不注入 MANGOHUD（D81）。</summary>
    public static Dictionary<string, string> EnvironmentVariables(RuntimeProfile? profile)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (profile is null)
        {
            return env;
        }
        switch (NormalizeHud(profile.Hud))
        {
            case HudDxvk:
                env["DXVK_HUD"] = string.IsNullOrWhiteSpace(profile.HudOptions) ? DefaultDxvkHud : profile.HudOptions.Trim();
                break;
            case HudMangoHud:
                if (profile.GameScope)
                {
                    // gamescope 自己就是合成器里的监控层，再叠 MangoHud 会出现两套帧数（an-anime-team 同样处理）。
                    break;
                }
                env["MANGOHUD"] = "1";
                if (!string.IsNullOrWhiteSpace(profile.HudOptions))
                {
                    env["MANGOHUD_CONFIG"] = profile.HudOptions.Trim();
                }
                break;
        }
        return env;
    }


    /// <summary>包装顺序（从外到内）：gamescope → gamemoderun → 用户自己的 wrappers → wine。</summary>
    public static List<string> Wrappers(RuntimeProfile? profile)
    {
        var wrappers = new List<string>();
        if (profile is null)
        {
            return wrappers;
        }
        if (profile.GameScope)
        {
            List<string> scopeArgs = LaunchRecipeBuilder.SplitArgs(profile.GameScopeArgs);
            if (scopeArgs.Count == 0)
            {
                scopeArgs = LaunchRecipeBuilder.SplitArgs(DefaultGameScopeArgs);
            }
            wrappers.Add(string.Join(' ', new[] { "gamescope" }.Concat(scopeArgs)));
        }
        if (profile.GameMode)
        {
            wrappers.Add("gamemoderun");
        }
        foreach (string wrapper in profile.Wrappers)
        {
            if (!string.IsNullOrWhiteSpace(wrapper))
            {
                wrappers.Add(wrapper);
            }
        }
        return wrappers;
    }


    /// <summary>某张方案真正会用到的外部程序（给「有没有装」的提示用）。</summary>
    /// <summary>
    /// gamescope 参数的静态检查（P2）。参数是直接塞进 argv 的，不过 shell，
    /// 所以这里只挑「常见误解」：shell 转义写法、把游戏参数当选项、分辨率只写一半。
    /// 返回的每条都是提示文案，不阻断保存。
    /// </summary>
    public static IReadOnlyList<string> ValidateGameScopeArgs(string? args)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(args))
        {
            return problems;
        }
        List<string> tokens = LaunchRecipeBuilder.SplitArgs(args);
        if (tokens.Count == 0)
        {
            problems.Add("参数解析为空，检查引号是否配对");
            return problems;
        }
        // 引号会被 SplitArgs 吃掉，所以这一条必须查原始串（L-154）
        if (args.Contains('"'))
        {
            problems.Add("参数是直接交给 gamescope 的，不用 shell 引号：直接写 -W 2560 -H 1440 即可");
        }
        foreach (string token in tokens)
        {
            if (token.IndexOfAny([';', '|', '&', '>', '<', '`', '$']) >= 0)
            {
                problems.Add($"参数是直接交给 gamescope 的，不用 shell 重定向：{token}");
                break;
            }
        }
        int dashDash = tokens.IndexOf("--");
        if (dashDash >= 0 && dashDash < tokens.Count - 1)
        {
            problems.Add("「--」后面的参数会交给游戏本身，不会传给 gamescope");
        }
        CheckPair("-W", "-H", "输出分辨率");
        CheckPair("-w", "-h", "游戏内部分辨率");
        return problems;

        void CheckPair(string first, string second, string what)
        {
            bool hasFirst = tokens.Contains(first);
            bool hasSecond = tokens.Contains(second);
            if (hasFirst && !hasSecond)
            {
                problems.Add($"{what}要成对：现在只写了 {first}，缺 {second}");
            }
            else if (hasSecond && !hasFirst)
            {
                problems.Add($"{what}要成对：现在只写了 {second}，缺 {first}");
            }
        }
    }


    /// <summary>gamescope 参数预设（I）：下标 0 是「不预设」，与下拉的占位项对齐。</summary>
    public static readonly string[] GameScopePresetArgs =
    [
        "",
        "-f -W 1920 -H 1080",
        "-f -W 2560 -H 1440",
        "-W 1280 -H 720",
    ];


    /// <summary>HUD 参数预设（I）：下标 0 是「不预设」。</summary>
    public static readonly string[] HudPresetOptions =
    [
        "",
        "fps",
        "fps,frametimes,gpuload,cpu_stats,gpu_stats",
        "fps,frametime",
        "gpu_temp,gpu_load,core_load",
    ];


    /// <summary>在参数串里加上 / 去掉一个开关（HDR、VRR 用）；顺序保留，空串回落到默认。</summary>
    public static string ToggleArg(string? args, string flag, bool on)
    {
        List<string> tokens = string.IsNullOrWhiteSpace(args)
            ? new List<string>()
            : LaunchRecipeBuilder.SplitArgs(args).ToList();
        bool has = tokens.Contains(flag, StringComparer.Ordinal);
        if (on && !has)
        {
            tokens.Add(flag);
        }
        if (!on && has)
        {
            tokens.RemoveAll(x => x == flag);
        }
        string text = string.Join(" ", tokens).Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            return text;
        }
        return on ? flag : DefaultGameScopeArgs;
    }


    public static IReadOnlyList<string> RequiredBinaries(RuntimeProfile? profile)
    {
        var bins = new List<string>();
        if (profile is null)
        {
            return bins;
        }
        if (profile.GameScope)
        {
            bins.Add("gamescope");
        }
        if (profile.GameMode)
        {
            bins.Add("gamemoderun");
        }
        if (NormalizeHud(profile.Hud) == HudMangoHud && !profile.GameScope)
        {
            bins.Add("mangohud");
        }
        return bins;
    }


    /// <summary>在 PATH 里找可执行文件；找不到返回 null。测试里用 <paramref name="path"/> 指定固定目录。</summary>
    public static string? FindInPath(string binary, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(binary))
        {
            return null;
        }
        if (binary.Contains('/'))
        {
            return File.Exists(binary) ? binary : null;
        }
        string searchPath = path ?? Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string dir in searchPath.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(dir, binary);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }


    /// <summary>展开结果给界面/预检看的一句话；缺程序时点名，不静默降级。</summary>
    public static string Describe(RuntimeProfile? profile, string? path = null)
    {
        var parts = new List<string>();
        string hud = NormalizeHud(profile?.Hud);
        if (hud == HudDxvk)
        {
            parts.Add($"DXVK_HUD={EnvironmentVariables(profile)["DXVK_HUD"]}");
        }
        else if (hud == HudMangoHud)
        {
            parts.Add(profile?.GameScope == true ? "MangoHud（gamescope 下不额外注入）" : "MANGOHUD=1");
        }
        if (profile?.GameMode == true)
        {
            parts.Add("gamemoderun");
        }
        if (profile?.GameScope == true)
        {
            List<string> scopeArgs = LaunchRecipeBuilder.SplitArgs(profile.GameScopeArgs);
            if (scopeArgs.Count == 0)
            {
                scopeArgs = LaunchRecipeBuilder.SplitArgs(DefaultGameScopeArgs);
            }
            parts.Add("gamescope " + string.Join(' ', scopeArgs));
        }
        if (parts.Count == 0)
        {
            return "没有开启任何增强";
        }
        var missing = RequiredBinaries(profile).Where(x => FindInPath(x, path) is null).ToList();
        if (missing.Count > 0)
        {
            parts.Add($"缺少程序：{string.Join("、", missing)}");
        }
        return string.Join(" · ", parts);
    }

}
