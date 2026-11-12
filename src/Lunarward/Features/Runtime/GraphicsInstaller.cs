// Lunarward：把选定的 DXVK / VKD3D-PROTON 装进 Wine 前缀（M2 组件层）。
// 只做「复制 + 记录」，不下载、不联网：组件来源由用户放在系统目录或 ~/.local/share/lunarward/components。
using Starward.Helpers;
using Microsoft.Extensions.Logging;
using Starward.Features.GameLauncher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Starward.Features.Runtime;


/// <summary>前缀里当前安装的图形组件记录（&lt;prefix&gt;/.lunarward/graphics.json）。</summary>
public sealed class InstalledGraphics
{

    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Kind { get; set; } = "";

    public string Source { get; set; } = "";

    public DateTime InstalledAt { get; set; }

    public List<string> Files { get; set; } = new();

    /// <summary>每个文件的长度；老记录可能没有，校验时退化为只看存在性。</summary>
    public Dictionary<string, long> Sizes { get; set; } = new();

}


public static class GraphicsInstaller
{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };


    private static string RecordPath(string prefix) => Path.Combine(prefix, ".lunarward", "graphics.json");


    /// <summary>读前缀里已安装的组件记录；没有则返回 null。</summary>
    public static InstalledGraphics? GetInstalled(string prefix)
    {
        try
        {
            string path = RecordPath(prefix);
            if (!File.Exists(path))
            {
                return null;
            }
            return JsonSerializer.Deserialize<InstalledGraphics>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }


    /// <summary>把组件装进前缀；已装同一 Id 且文件齐全时直接跳过。</summary>
    public static bool EnsureInstalled(string prefix, Component component, ILogger? logger = null)
    {
        if (component.Kind is not (ComponentKind.Dxvk or ComponentKind.Vkd3dProton))
        {
            throw new ArgumentException($"不是图形组件：{component.Kind}", nameof(component));
        }
        if (!Directory.Exists(prefix))
        {
            logger?.LogWarning("Prefix not found, skip graphics install: {prefix}", prefix);
            return false;
        }

        List<(string Source, string Target)> files = CollectFiles(component);
        if (files.Count == 0)
        {
            logger?.LogWarning("No graphics files found in {path}", component.Path);
            return false;
        }

        InstalledGraphics? installed = GetInstalled(prefix);
        if (installed is not null
            && installed.Id == component.Id
            && installed.Files.All(f => File.Exists(f)))
        {
            logger?.LogDebug("Graphics already installed: {id}", component.Id);
            return false;
        }

        BackupRecord(prefix);

        string system32 = Path.Combine(prefix, "drive_c", "windows", "system32");
        string syswow64 = Path.Combine(prefix, "drive_c", "windows", "syswow64");
        Directory.CreateDirectory(system32);
        List<string> written = new();
        Dictionary<string, long> sizes = new();
        foreach ((string source, string target) in files)
        {
            string dir = target.EndsWith("32.dll") || target.StartsWith("x86", StringComparison.Ordinal) ? syswow64 : system32;
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, Path.GetFileName(target));
            File.Copy(source, dest, overwrite: true);
            written.Add(dest);
            sizes[dest] = new FileInfo(dest).Length;
        }

        InstalledGraphics record = new()
        {
            Id = component.Id,
            Name = component.Name,
            Kind = component.KindText,
            Source = component.Path,
            InstalledAt = DateTime.Now,
            Files = written,
            Sizes = sizes,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(RecordPath(prefix))!);
        JsonFile.WriteAtomic(RecordPath(prefix), record, JsonOptions);
        logger?.LogInformation("Installed {name} ({kind}) into {prefix}: {count} files", component.Name, component.KindText, prefix, written.Count);
        return true;
    }


    /// <summary>写记录前留一份 .bak，出问题能按上一份回滚（D63 §5）。</summary>
    private static void BackupRecord(string prefix)
    {
        try
        {
            string path = RecordPath(prefix);
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "backup graphics record failed");
        }
    }


    /// <summary>前缀里图形层的状态：记录 + 实际文件核对结果。</summary>
    public sealed class GraphicsStatus
    {
        public InstalledGraphics? Record { get; init; }

        public int TotalFiles { get; init; }

        public int MissingFiles { get; init; }

        /// <summary>「已装 DXVK 3.1.1 · 与记录一致 / 不一致（N 个缺失）/ 未安装」。</summary>
        public string Text { get; init; } = "未安装";

        public bool IsGuardBroken => Record is not null && MissingFiles > 0;
    }


    /// <summary>核对该前缀的图形层：文件是否还在、大小是否对得上（不做 SHA，D63 §5 第 4 条）。</summary>
    public static GraphicsStatus Verify(string prefix)
    {
        InstalledGraphics? record = GetInstalled(prefix);
        if (record is null || record.Files.Count == 0)
        {
            return new GraphicsStatus { Text = "未安装" };
        }
        int missing = 0;
        foreach (string file in record.Files)
        {
            try
            {
                FileInfo info = new(file);
                if (!info.Exists)
                {
                    missing++;
                }
                else if (record.Sizes.TryGetValue(file, out long expected) && expected > 0 && info.Length != expected)
                {
                    missing++;
                }
            }
            catch
            {
                missing++;
            }
        }
        string name = string.IsNullOrWhiteSpace(record.Name) ? record.Id : record.Name;
        string text = missing == 0
            ? $"已装 {name} · 与记录一致"
            : $"已装 {name} · 不一致（{missing} 个缺失）";
        return new GraphicsStatus
        {
            Record = record,
            TotalFiles = record.Files.Count,
            MissingFiles = missing,
            Text = text,
        };
    }


    /// <summary>从前缀里移除图形层：按记录删文件、删记录（保留 .bak）。外部前缀默认不允许调用。</summary>
    public static bool Remove(string prefix, ILogger? logger = null)
    {
        InstalledGraphics? record = GetInstalled(prefix);
        if (record is null)
        {
            return false;
        }
        int removed = 0;
        foreach (string file in record.Files)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    removed++;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "delete graphics file failed: {file}", file);
            }
        }
        try
        {
            File.Delete(RecordPath(prefix));
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "delete graphics record failed: {prefix}", prefix);
        }
        logger?.LogInformation("Removed graphics {name} from {prefix}: {count} files", record.Id, prefix, removed);
        return true;
    }


    /// <summary>收集组件里需要覆盖进前缀的 dll（按架构分目录时取 x64 为主、x86 可选）。</summary>
    private static List<(string Source, string Target)> CollectFiles(Component component)
    {
        List<(string, string)> result = new();
        string root = component.Path;
        string? x64 = Directory.Exists(Path.Combine(root, "x64")) ? Path.Combine(root, "x64") : null;
        string? x86 = Directory.Exists(Path.Combine(root, "x86")) ? Path.Combine(root, "x86") : null;
        if (x64 is null)
        {
            // 平铺目录（不区分架构）：只取顶层 dll
            foreach (string file in Directory.EnumerateFiles(root, "*.dll", SearchOption.TopDirectoryOnly))
            {
                result.Add((file, "x64/" + Path.GetFileName(file)));
            }
            return result;
        }
        foreach (string file in Directory.EnumerateFiles(x64, "*.dll", SearchOption.TopDirectoryOnly))
        {
            result.Add((file, "x64/" + Path.GetFileName(file)));
        }
        if (x86 is not null)
        {
            foreach (string file in Directory.EnumerateFiles(x86, "*.dll", SearchOption.TopDirectoryOnly))
            {
                result.Add((file, "x86/" + Path.GetFileName(file)));
            }
        }
        return result;
    }


    /// <summary>按 Profile 的 RunnerPath 推断前缀路径（未配置前缀时用 ~/.wine）。</summary>
    public static string ResolvePrefixPath(RuntimeProfile profile, string gameKey)
    {
        if (!string.IsNullOrWhiteSpace(profile.PrefixPath))
        {
            return profile.PrefixPath!;
        }
        return GameLauncherService.ResolvePrefix(gameKey);
    }

}
