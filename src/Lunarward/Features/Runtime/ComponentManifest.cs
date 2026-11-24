// Lunarward：受管组件的 manifest 与布局归一化（D73 §2.2）。
//
// 预编译包解压出来的层级各家不同（wine-tkg-10.13/bin/wine、GE-Proton/files/bin/wine、
// 裸 exe），不归一化的话后面每个构建都要特判。这里只做「找出 root 与 wine 在哪」，
// 以及把结论写进 manifest.json，让下一次探测不用再猜。
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Starward.Features.Runtime;


/// <summary>布局：运行器可执行文件相对 root 的位置。</summary>
public static class ComponentLayouts
{
    /// <summary>…/bin/wine（普通 wine 构建）。</summary>
    public const string Bin = "bin";

    /// <summary>…/files/bin/wine（Proton 形态）。</summary>
    public const string FilesBin = "files-bin";

    /// <summary>目录本身就是可执行文件旁边（裸 exe）。</summary>
    public const string BareExe = "bare-exe";


    /// <summary>
    /// 在一个解压目录里找运器的根。先看自己，再向下最多四层找 <c>bin/wine</c> /
    /// <c>files/bin/wine</c> / <c>wine</c>，返回 (layout, root, winePath)。
    /// 找不到时 layout 为空、winePath 为空。
    /// </summary>
    public static (string Layout, string Root, string WinePath) FindRunnerRoot(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return ("", "", "");
        }
        var candidates = new Queue<(string Dir, int Depth)>();
        candidates.Enqueue((directory, 0));
        while (candidates.Count > 0)
        {
            (string dir, int depth) = candidates.Dequeue();
            string filesBin = Path.Combine(dir, "files", "bin", "wine");
            if (File.Exists(filesBin))
            {
                return (FilesBin, dir, filesBin);
            }
            string bin = Path.Combine(dir, "bin", "wine");
            if (File.Exists(bin))
            {
                return (Bin, dir, bin);
            }
            string bare = Path.Combine(dir, "wine");
            if (File.Exists(bare) && (depth > 0 || !Directory.Exists(Path.Combine(dir, "bin"))))
            {
                return (BareExe, dir, bare);
            }
            if (depth >= 3)
            {
                continue;
            }
            foreach (string child in SafeDirs(dir))
            {
                candidates.Enqueue((child, depth + 1));
            }
        }
        return ("", "", "");
    }


    /// <summary>按 layout + root 拼出 wine 可执行文件路径。</summary>
    public static string WinePath(string root, string layout) => layout switch
    {
        FilesBin => Path.Combine(root, "files", "bin", "wine"),
        Bin => Path.Combine(root, "bin", "wine"),
        BareExe => Path.Combine(root, "wine"),
        _ => "",
    };


    private static IEnumerable<string> SafeDirs(string dir)
    {
        string[] items;
        try
        {
            items = Directory.GetDirectories(dir);
        }
        catch
        {
            yield break;
        }
        foreach (string item in items)
        {
            string name = Path.GetFileName(item);
            // 跳过明显的非运行器目录，避免在几百个目录里乱翻
            if (name is "share" or "lib" or "lib64" or "include" or "doc" or "man" or "__pycache__")
            {
                continue;
            }
            yield return item;
        }
    }
}


/// <summary>
/// 图形层布局（R59）：导入校验与 <see cref="ComponentStore.GetGraphics"/> 必须用同一套判定，
/// 否则会出现「导入成功但组件列表永远看不到」。
/// 归档顶层常带 README/LICENSE 等散文件（顶层不是唯一目录），所以这里向下最多三层找 dll。
/// </summary>
public static class GraphicsLayouts
{
    /// <summary>DXVK 的主标记；VKD3D-Proton 用 d3d12.dll。</summary>
    public static string MarkerName(bool vkd3d) => vkd3d ? "d3d12.dll" : "d3d11.dll";

    /// <summary>目录本层是否像图形层根（x64/&lt;marker&gt; 或平铺的 &lt;marker&gt;）。</summary>
    public static bool IsRoot(string directory, bool vkd3d = false)
    {
        if (!Directory.Exists(directory))
        {
            return false;
        }
        string marker = MarkerName(vkd3d);
        return File.Exists(Path.Combine(directory, "x64", marker))
               || File.Exists(Path.Combine(directory, marker));
    }

    /// <summary>
    /// 在解压目录里找图形层根：先自己，再向下最多三层；优先 DXVK（d3d11），找不到再按 VKD3D（d3d12）。
    /// 返回 null 表示不像图形层。
    /// </summary>
    public static string? FindRoot(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return null;
        }
        foreach (bool vkd3d in new[] { false, true })
        {
            var queue = new Queue<string>();
            queue.Enqueue(directory);
            for (int depth = 0; depth <= 3 && queue.Count > 0; depth++)
            {
                int width = queue.Count;
                for (int i = 0; i < width; i++)
                {
                    string dir = queue.Dequeue();
                    if (IsRoot(dir, vkd3d))
                    {
                        return dir;
                    }
                    if (depth == 3)
                    {
                        continue;
                    }
                    try
                    {
                        foreach (string child in Directory.GetDirectories(dir))
                        {
                            queue.Enqueue(child);
                        }
                    }
                    catch
                    {
                        // 读不了的目录直接跳过
                    }
                }
            }
        }
        return null;
    }
}


/// <summary>受管组件的来源信息（manifest.source）。</summary>
public sealed class ComponentManifestSource
{
    public string Url { get; set; } = "";

    public string MirrorPrefix { get; set; } = "";

    public DateTimeOffset DownloadedAt { get; set; }
}


/// <summary>校验信息（manifest.digest）。</summary>
public sealed class ComponentManifestDigest
{
    public string Algo { get; set; } = "sha256";

    public string Value { get; set; } = "";

    /// <summary>true = 由源端提供；false = 首次下载后自记（TOFU，见 D73 §2.3）。</summary>
    public bool FromApi { get; set; }
}


/// <summary>components/&lt;kind&gt;/&lt;name&gt;-&lt;version&gt;/manifest.json</summary>
public sealed class ComponentManifest
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>稳定标识，写进环境（配方）。</summary>
    public string Id { get; set; } = "";

    /// <summary>wine / proton / graphics</summary>
    public string Kind { get; set; } = "wine";

    public string Name { get; set; } = "";

    public string Version { get; set; } = "";

    /// <summary>相对组件目录；"." 表示目录本身。</summary>
    public string Root { get; set; } = ".";

    /// <summary>bin / files-bin / bare-exe；图形层为空。</summary>
    public string Layout { get; set; } = "";

    public string Origin { get; set; } = "managed";

    public ComponentManifestSource? Source { get; set; }

    public ComponentManifestDigest? Digest { get; set; }

    public bool LayoutNormalized { get; set; }

    public string? Notes { get; set; }
}


public static class ComponentNaming
{
    /// <summary>
    /// 从归档文件名推一个组件名：去掉扩展名、架构标记与**结尾**的版本号，
    /// 不要把中间的词一起吃掉（`wine-z-9.9.tar.gz` → `wine-z`）。
    /// </summary>
    public static string SuggestName(string fileName, string kind)
    {
        string name = fileName;
        foreach (string ext in new[] { ".tar.xz", ".tar.gz", ".tar.zst", ".tgz", ".tar" })
        {
            if (name.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^ext.Length];
                break;
            }
        }
        name = Regex.Replace(name, @"[-_.](amd64|x86_64|x64|i386|x86|win64|win32)$", "", RegexOptions.IgnoreCase);
        name = Regex.Replace(name, @"[-_.]?v?\d+(\.\d+)+$", "", RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(name) ? kind : name;
    }


    public static string Sanitize(string name)
    {
        // R35：以前只替换 Path.GetInvalidFileNameChars()（Linux 只有 '/' 和 '\0'），
        // 名字叫 ".." 会把前缀算到父目录、"." 会算到 PrefixRoot 本身。
        // 现在只保留字母/数字/下划线/点/短横线/CJK，其余一律折成 '-'，再把 ".."/"." 去掉。
        name ??= "";
        foreach (char c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '-');
        }
        // R102：保留**所有** Unicode 字母与数字（俄语、越南语、希腊语的组件名以前会被整段折成 '-'），
        // 只把控制符、标点、空白折成 '-'，再收拾连续短横线。
        var builder = new StringBuilder(name.Length);
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c is '.' or '-' or '_')
            {
                builder.Append(c);
            }
            else
            {
                builder.Append('-');
            }
        }
        name = Regex.Replace(builder.ToString(), "-{2,}", "-");
        name = name.Trim('-', '.', ' ');
        if (string.IsNullOrWhiteSpace(name))
        {
            return "default";
        }
        // R102：以前没有长度上限，某些 tar 的超长前缀名直接把目录名顶到 255 字节上限。
        // 超过 80 个字符就截断，并补 8 位哈希，避免「前 80 字符相同的两个组件」撞同一个目录。
        if (name.Length > 80)
        {
            string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8].ToLowerInvariant();
            name = name[..71].TrimEnd('-', '.', ' ') + "-" + hash;
        }
        return name;
    }
}


public static class ComponentManifestStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };


    public static string PathFor(string componentDir) => Path.Combine(componentDir, "manifest.json");


    public static ComponentManifest? TryRead(string componentDir)
    {
        string path = PathFor(componentDir);
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            return JsonFile.ReadOrRecover<ComponentManifest>(path, JsonOptions);
        }
        catch
        {
            return null;
        }
    }


    public static void Write(string componentDir, ComponentManifest manifest)
    {
        JsonFile.WriteAtomic(PathFor(componentDir), manifest, JsonOptions);
    }
}
