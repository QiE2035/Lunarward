using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text;
using ValveKeyValue;

namespace Starward.Features.Runtime;

/// <summary>
/// Steam 库目录（D78）。
///
/// 之前只认 `~/.local/share/Steam` 与 `~/.steam/steam` 两处硬编码路径，Steam 装在别的盘
/// （`libraryfolders.vdf` 里的自定义库）就扫不到 Proton。这里用 ValveKeyValue 读 VDF，
/// 把「默认根 + VDF 里登记的库」合起来，供组件扫描与 Steam 前缀设置使用。
///
/// 只读文本，不需要 Steam 客户端在运行；VDF 损坏/缺失时静默退回默认根（扫描不能因此失败）。
/// </summary>
internal static class SteamLibrary
{
    /// <summary>Steam 安装根候选（原生包、旧符号链接布局、Flatpak 各一份）。</summary>
    public static IReadOnlyList<string> RootCandidates(string? home = null)
    {
        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var roots = new List<string>
        {
            Path.Combine(home, ".local/share/Steam"),
            Path.Combine(home, ".steam/steam"),
            Path.Combine(home, ".var/app/com.valvesoftware.Steam/.local/share/Steam"),
            Path.Combine(home, ".var/app/com.valvesoftware.Steam/data/Steam"),
        };
        return roots.Distinct(StringComparer.Ordinal).ToList();
    }


    /// <summary>`libraryfolders.vdf` 候选路径（每个安装根一份）。</summary>
    public static IEnumerable<string> VdfCandidates(string? home = null)
        => RootCandidates(home).Select(root => Path.Combine(root, "steamapps", "libraryfolders.vdf"));


    /// <summary>解析 VDF 文本，返回其中登记的库根路径（新格式 `"0" { "path" ... }` 与旧格式 `"1" "/path"` 都认）。</summary>
    public static IReadOnlyList<string> ParseLibraries(string? vdfText)
    {
        if (string.IsNullOrWhiteSpace(vdfText))
        {
            return Array.Empty<string>();
        }
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(vdfText));
            return ParseLibraries(stream);
        }
        catch (Exception)
        {
            // VDF 是 Steam 自己写的，语法坏了说明文件正在被写；这一轮当作没有自定义库
            return Array.Empty<string>();
        }
    }


    /// <summary>解析 VDF 流，返回其中登记的库根路径。</summary>
    public static IReadOnlyList<string> ParseLibraries(Stream stream)
    {
        var result = new List<string>();
        KVObject root;
        try
        {
            root = KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Deserialize(stream);
        }
        catch (Exception)
        {
            return result;
        }
        foreach (KeyValuePair<string, KVObject> entry in root.Children)
        {
            KVObject child = entry.Value;
            string? path = child.IsCollection ? ReadString(child, "path") : ReadImplicitString(entry.Key, child);
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }
            if (!path.StartsWith('/'))
            {
                // 旧格式里还有 TimeNextStatsReport / ContentStatsID 之类的数字字段，只认绝对路径。
                // R126：双系统下 `D:\\SteamLibrary` 这类值也走这里——以前一声不响丢掉，
                // 用户只能看到「没扫到 Proton」；留一条 Debug，排查时能从日志里看到是哪一行被忽略。
                if (!path.All(char.IsAsciiDigit))
                {
                    Serilog.Log.Debug("Steam library path ignored (not absolute): {path}", path);
                }
                continue;
            }
            string normalized = path.Length > 1 ? path.TrimEnd('/') : path;
            if (!result.Contains(normalized, StringComparer.Ordinal))
            {
                result.Add(normalized);
            }
        }
        return result;
    }


    /// <summary>
    /// 全部库根：默认安装根（存在的）+ VDF 里登记的自定义库（存在的），按出现顺序去重。
    /// </summary>
    public static IReadOnlyList<string> ReadLibraries(string? home = null)
    {
        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var libraries = new List<string>();
        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
            {
                // R126：同 ParseLibraries，认不出的库根留在日志里。
                if (!string.IsNullOrWhiteSpace(path))
                {
                    Serilog.Log.Debug("Steam library root ignored (not absolute): {path}", path);
                }
                return;
            }
            string normalized = path.Length > 1 ? path.TrimEnd('/') : path;
            if (!libraries.Contains(normalized, StringComparer.Ordinal))
            {
                libraries.Add(normalized);
            }
        }

        foreach (string root in RootCandidates(home))
        {
            if (Directory.Exists(root))
            {
                Add(root);
            }
        }

        foreach (string vdf in VdfCandidates(home))
        {
            if (!File.Exists(vdf))
            {
                continue;
            }
            try
            {
                using var stream = File.OpenRead(vdf);
                foreach (string library in ParseLibraries(stream))
                {
                    Add(library);
                }
            }
            catch (Exception)
            {
                // 读不到就只当作没有自定义库
            }
        }
        return libraries;
    }


    /// <summary>
    /// 兼容工具与 Proton 的扫描根：每个库的 `steamapps/compatibilitytools.d`（自定义 Proton）
    /// 与 `steamapps/common`（Steam 官方 Proton 装在这里，目录名以 `Proton` 开头）。
    /// </summary>
    public static IEnumerable<string> CompatRoots(string? home = null)
    {
        foreach (string library in ReadLibraries(home))
        {
            yield return Path.Combine(library, "steamapps", "compatibilitytools.d");
            yield return Path.Combine(library, "steamapps", "common");
        }
    }


    /// <summary>`steamapps/common` 里只有 `Proton*` 目录是运行器，其余是游戏本体。</summary>
    public static bool IsProtonFolder(string directory)
    {
        // R126：`common` 下只有 `Proton *`、`GE-Proton*` 与 `Proton-*` 这几种；`GE-Proton` 不以
        // `Proton` 开头，所以两条前缀都要判——以前写成「第二条是冗余」的注释是错的，这次把注释改对。
        string name = Path.GetFileName(directory);
        return name.StartsWith("Proton", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("GE-Proton", StringComparison.OrdinalIgnoreCase);
    }


    private static string? ReadString(KVObject obj, string key)
    {
        try
        {
            KVObject? child = obj[key];
            return child is null ? null : ReadImplicitString(child);
        }
        catch (Exception)
        {
            return null;
        }
    }


    private static string? ReadImplicitString(string key, KVObject obj)
    {
        // 旧格式：`"1" "/path"`，key 是库序号；其它 key（TimeNextStatsReport 等）不是路径
        if (!key.All(char.IsDigit))
        {
            return null;
        }
        return ReadImplicitString(obj);
    }


    private static string? ReadImplicitString(KVObject obj)
    {
        try
        {
            return (string?) obj;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
