// Lunarward：前缀路径的纯逻辑（可单测，不碰文件系统）。
//
// 两件事在这里定死：
//   1) `~` / `$HOME` / 环境变量要展开——用户在文本框里写 `~/games/prefix` 是常态；
//   2) 去掉尾部分隔符——wine 对 WINEPREFIX 做字符串前缀比较，带尾斜杠会被当成另一个前缀，
//      表现为 `wine: launching from steam.exe stub!` 后直接退出（真机实测，L-92）。
using System;
using System.IO;

namespace Starward.Features.Runtime;


public static class PrefixPath
{

    /// <summary>展开用户目录与环境变量，并去掉尾部分隔符（保留根目录 "/"）。</summary>
    public static string? Expand(string? path, string? home = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        string expanded = path.Trim();
        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (expanded == "~")
        {
            return Normalize(home);
        }
        if (expanded.StartsWith("~/", StringComparison.Ordinal))
        {
            expanded = Path.Combine(home, expanded[2..]);
        }
        else if (expanded.StartsWith("$HOME/", StringComparison.Ordinal))
        {
            expanded = Path.Combine(home, expanded[6..]);
        }
        else if (expanded.StartsWith("${HOME}/", StringComparison.Ordinal))
        {
            expanded = Path.Combine(home, expanded[8..]);
        }
        expanded = Environment.ExpandEnvironmentVariables(expanded);
        return Normalize(expanded);
    }


    /// <summary>只做规范化：去空白、去尾部分隔符（根目录保持 "/"）。</summary>
    public static string Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }
        string trimmed = path.Trim();
        if (trimmed.Length > 1)
        {
            trimmed = trimmed.TrimEnd('/');
        }
        return trimmed;
    }

}
