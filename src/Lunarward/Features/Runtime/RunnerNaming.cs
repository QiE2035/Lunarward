// Lunarward：运行器的显示名（D87）。
// 两件事都在这里，因为它们是纯字符串逻辑，要能被单测直接覆盖（D37：不链接整个 ComponentStore）：
//   1) PATH 里那个 wine 叫什么——只有装在系统目录的才叫「系统 Wine」；
//   2) 同名运行器怎么区分——两个第三方启动器各带一份同名运行器时追加来源。
using System;
using System.IO;

namespace Starward.Features.Runtime;

internal static class RunnerNaming
{
    /// <summary>
    /// PATH 里那个 wine 的显示名。只把装在系统目录的叫「系统 Wine」；`/opt`、`~/.local`
    /// 这类自己装的定制版本改用目录名——否则 `/opt/spritz-wine-tkg/bin/wine` 会显示成
    /// 「系统 Wine」，而方案详情里按配置显示 `wine`，同一个文件两个名字（2026-11-18 实测）。
    /// 入参是**已解析符号链接**的路径：`/usr/local/bin/wine → /opt/x/bin/wine` 应该算 /opt 的。
    /// </summary>
    public static string WineDisplayName(string path, string exe)
    {
        string fallback = exe == "wine" ? "系统 Wine" : "系统 Wine (64)";
        foreach (string systemRoot in new[] { "/usr/", "/bin/", "/sbin/" })
        {
            if (path.StartsWith(systemRoot, StringComparison.Ordinal))
            {
                return fallback;
            }
        }
        // .../spritz-wine-tkg/bin/wine → spritz-wine-tkg
        string? bin = Path.GetDirectoryName(path);
        string? home = string.IsNullOrEmpty(bin) ? null : Path.GetDirectoryName(bin);
        string name = string.IsNullOrEmpty(home) ? "" : Path.GetFileName(home);
        return string.IsNullOrWhiteSpace(name) ? fallback : name;
    }


    /// <summary>
    /// 重名时用来区分的来源片段：`~/.local/share/&lt;x&gt;/…` → x，`/opt/&lt;x&gt;/…` → x，
    /// 系统包 → 空（本来就不会重名）。
    /// </summary>
    public static string Origin(string path)
    {
        const string share = "/.local/share/";
        int at = path.IndexOf(share, StringComparison.Ordinal);
        if (at >= 0)
        {
            int start = at + share.Length;
            int slash = path.IndexOf('/', start);
            if (slash > start)
            {
                return path[start..slash];
            }
        }
        if (path.StartsWith("/opt/", StringComparison.Ordinal))
        {
            int slash = path.IndexOf('/', 5);
            if (slash > 5)
            {
                return path[5..slash];
            }
        }
        return "";
    }


    /// <summary>重名时才动的名字：`x` → `x（anime-game-launcher）`；来源为空就原样返回。</summary>
    public static string WithOrigin(string name, string path)
    {
        string origin = Origin(path);
        return string.IsNullOrEmpty(origin) ? name : $"{name}（{origin}）";
    }
}
