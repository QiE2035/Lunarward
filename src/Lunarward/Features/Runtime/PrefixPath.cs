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

    /// <summary>
    /// R44：方案没有显式前缀路径时的受管目录。
    /// 目录名优先用稳定的 <paramref name="slug"/>（方案改显示名不再换目录），
    /// 没有 slug 的旧配置退回按名字推导；结果必须落在 <paramref name="root"/> 内（R35）。
    /// </summary>
    public static string DefaultPrefixDirectory(string root, string? slug, string? name, string? id)
    {
        string segment = slug?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(segment))
        {
            string legacyName = string.IsNullOrWhiteSpace(name) ? (id ?? "default") : name!;
            segment = ComponentNaming.Sanitize(legacyName).ToLowerInvariant();
            // Sanitize 的兜底值是 "default"：名字全是不合法字符（如 "///"）时用 Id，避免几份方案共用一个目录。
            if (string.IsNullOrWhiteSpace(segment)
                || (string.Equals(segment, "default", StringComparison.Ordinal)
                    && !string.Equals(legacyName.Trim(), "default", StringComparison.OrdinalIgnoreCase)))
            {
                segment = string.IsNullOrWhiteSpace(id) ? "default" : id!;
            }
        }
        string fullRoot = Path.GetFullPath(root);
        string combined = Path.GetFullPath(Path.Combine(fullRoot, segment));
        if (!combined.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            combined = Path.Combine(fullRoot, string.IsNullOrWhiteSpace(id) ? "default" : id!);
        }
        return combined;
    }


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
