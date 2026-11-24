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
        // R125：`/usr/local/bin/wine` 是自己编的，不是发行版的系统包——以前被 `/usr/` 吞成「系统 Wine」，
        // 于是同一个二进制在详情页叫系统 Wine、在列表里叫 usr。只认真正的发行版目录。
        foreach (string systemRoot in new[] { "/usr/bin/", "/usr/sbin/", "/bin/", "/sbin/" })
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
        // R125：不再只认 `~/.local/share` 与 `/opt`——`/run/media/<user>/…`、`/mnt/…`、`~/Games/…`
        // 下的同名运行器以前来源为空，列表里两行一模一样，分不清是哪个。
        string? picked = SegmentAfter(path, "/.local/share/")
            ?? SegmentAfter(path, "/opt/")
            ?? SegmentAfterSkip(path, "/run/media/", skip: 1)
            ?? SegmentAfterSkip(path, "/media/", skip: 1)
            ?? SegmentAfter(path, "/mnt/");
        if (picked is null)
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home) && path.StartsWith(home + "/", StringComparison.Ordinal))
            {
                picked = SegmentAfter(path, home + "/");
            }
        }
        return picked ?? "";
    }


    /// <summary>取标记后面的第一段（`/opt/foo/bin/wine` → `foo`）。</summary>
    private static string? SegmentAfter(string path, string marker)
    {
        int at = path.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }
        int start = at + marker.Length;
        int slash = path.IndexOf('/', start);
        return slash > start ? path[start..slash] : null;
    }


    /// <summary>取标记后面第 <paramref name="skip"/> + 1 段（挂载点要先跳过用户名）。</summary>
    private static string? SegmentAfterSkip(string path, string marker, int skip)
    {
        int at = path.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }
        int cursor = at + marker.Length;
        for (int i = 0; i < skip; i++)
        {
            int next = path.IndexOf('/', cursor);
            if (next < 0)
            {
                return null;
            }
            cursor = next + 1;
        }
        int end = path.IndexOf('/', cursor);
        return end > cursor ? path[cursor..end] : null;
    }


    /// <summary>重名时才动的名字：`x` → `x（anime-game-launcher）`；来源为空就原样返回。</summary>
    public static string WithOrigin(string name, string path)
    {
        string origin = Origin(path);
        return string.IsNullOrEmpty(origin) ? name : $"{name}（{origin}）";
    }
}
