// Lunarward：把选定的 DXVK / VKD3D-PROTON 装进 Wine 前缀（M2 组件层）。
// 只做「复制 + 记录」，不下载、不联网：组件来源由用户放在系统目录或 ~/.local/share/lunarward/components。
// DXVK 与 VKD3D-Proton 是两个独立层（d3d9/10/11 vs d3d12），同一个前缀里可以同时存在（D83）。
using Starward.Helpers;
using Microsoft.Extensions.Logging;
using Starward.Features.GameLauncher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Starward.Features.Runtime;


/// <summary>前缀里某个图形组件的安装记录（&lt;prefix&gt;/.lunarward/graphics.json 的一个元素）。</summary>
public sealed class InstalledGraphics
{

    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>组件类别：「DXVK」或「VKD3D-Proton」，与 <see cref="Component.KindText"/> 一致。</summary>
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


    /// <summary>把组件类别映射成记录里的 Kind 文案（与 <see cref="Component.KindText"/> 一致）。</summary>
    public static string KindTextOf(ComponentKind kind) => kind switch
    {
        ComponentKind.Dxvk => "DXVK",
        ComponentKind.Vkd3dProton => "VKD3D-Proton",
        _ => kind.ToString(),
    };


    /// <summary>读前缀里已安装的全部图形组件记录；没有则返回空表。</summary>
    public static List<InstalledGraphics> GetAllInstalled(string prefix)
    {
        try
        {
            string path = RecordPath(prefix);
            if (!File.Exists(path))
            {
                return new();
            }
            string json = File.ReadAllText(path);
            List<InstalledGraphics>? list = null;
            try
            {
                list = JsonSerializer.Deserialize<List<InstalledGraphics>>(json);
            }
            catch (JsonException)
            {
                // 旧格式是单个对象（D83 之前的 schema）：读进来当一条，避免留下没人认领的 dll。
                InstalledGraphics? one = JsonSerializer.Deserialize<InstalledGraphics>(json);
                if (one is not null)
                {
                    list = new() { one };
                }
            }
            return list?.Where(x => x is not null).ToList() ?? new();
        }
        catch
        {
            return new();
        }
    }


    /// <summary>读某一类图形组件的记录（前缀里 DXVK 与 VKD3D 各一条）。</summary>
    public static InstalledGraphics? GetInstalled(string prefix, ComponentKind kind)
    {
        string kindText = KindTextOf(kind);
        return GetAllInstalled(prefix).FirstOrDefault(x => string.Equals(x.Kind, kindText, StringComparison.OrdinalIgnoreCase));
    }


    private static void SaveAll(string prefix, List<InstalledGraphics> records)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RecordPath(prefix))!);
        JsonFile.WriteAtomic(RecordPath(prefix), records, JsonOptions);
    }


    /// <summary>把一组组件装进前缀（DXVK / VKD3D 可各一个）；返回是否改动了磁盘。</summary>
    public static bool EnsureInstalled(string prefix, IEnumerable<Component> components, ILogger? logger = null)
    {
        bool changed = false;
        foreach (Component component in components)
        {
            changed |= EnsureInstalled(prefix, component, logger);
        }
        return changed;
    }


    /// <summary>把组件装进前缀；同类已装同一 Id 且文件齐全时直接跳过（不会动另一类）。</summary>
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

        List<(string Source, string Target, bool Is32Bit)> files = CollectFiles(component);
        if (files.Count == 0)
        {
            logger?.LogWarning("No graphics files found in {path}", component.Path);
            return false;
        }

        List<InstalledGraphics> records = GetAllInstalled(prefix);
        string kindText = KindTextOf(component.Kind);
        InstalledGraphics? existing = records.FirstOrDefault(x => string.Equals(x.Kind, kindText, StringComparison.OrdinalIgnoreCase));
        if (existing is not null
            && existing.Id == component.Id
            && existing.Files.Count > 0
            && existing.Files.All(file => File.Exists(file)
                && new FileInfo(file).Length > 0
                && (!existing.Sizes.TryGetValue(file, out long expected) || expected <= 0 || new FileInfo(file).Length == expected)))
        {
            logger?.LogDebug("Graphics already installed: {id}", component.Id);
            return false;
        }

        BackupRecord(prefix);

        string system32 = Path.Combine(prefix, "drive_c", "windows", "system32");
        string syswow64 = Path.Combine(prefix, "drive_c", "windows", "syswow64");
        Directory.CreateDirectory(system32);
        // R56：先全部拷进 staging，再逐个替换目标；中途失败按备份回滚。以前直接覆盖目标，
        // 拷一半失败就是新旧 dll 混搭（BAD EXE FORMAT），而界面还显示「已安装」。
        string staging = Path.Combine(prefix, ".lunarward", "graphics-staging", kindText);
        string backupDir = Path.Combine(prefix, ".lunarward", "graphics-backup", kindText);
        Directory.CreateDirectory(staging);
        var plan = new List<(string Staged, string Dest)>();
        var rollback = new List<(string Dest, string? Saved)>();
        try
        {
            foreach ((string source, string target, bool is32Bit) in files)
            {
                string dir = is32Bit ? syswow64 : system32;
                Directory.CreateDirectory(dir);
                string dest = Path.Combine(dir, Path.GetFileName(target));
                string staged = Path.Combine(staging, Path.GetFileName(target));
                File.Copy(source, staged, overwrite: true);
                plan.Add((staged, dest));
            }
            foreach ((string staged, string dest) in plan)
            {
                string? saved = null;
                if (File.Exists(dest))
                {
                    Directory.CreateDirectory(backupDir);
                    saved = Path.Combine(backupDir, Path.GetFileName(dest));
                    File.Copy(dest, saved, overwrite: true);
                }
                File.Move(staged, dest, overwrite: true);
                rollback.Add((dest, saved));
            }
        }
        catch (Exception ex)
        {
            foreach ((string dest, string? saved) in Enumerable.Reverse(rollback))
            {
                try
                {
                    if (saved is not null)
                    {
                        File.Copy(saved, dest, overwrite: true);
                    }
                    else if (File.Exists(dest))
                    {
                        File.Delete(dest);
                    }
                }
                catch (Exception rollbackEx)
                {
                    logger?.LogWarning(rollbackEx, "Rollback graphics file failed: {file}", dest);
                }
            }
            logger?.LogError(ex, "Install graphics failed, rolled back: {name}", component.Name);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }
            }
            catch (Exception cleanupEx)
            {
                logger?.LogDebug(cleanupEx, "Cleanup graphics staging failed: {path}", staging);
            }
        }

        List<string> written = plan.Select(x => x.Dest).ToList();
        var sizes = written.ToDictionary(f => f, f => new FileInfo(f).Length, StringComparer.Ordinal);

        // 换版本时先删掉旧记录里、这次没再写的那几个文件（同类的 dll 名基本重合，剩下的就是真正的残留）。
        if (existing is not null)
        {
            foreach (string stale in existing.Files.Where(f => !written.Contains(f, StringComparer.Ordinal)))
            {
                try
                {
                    if (File.Exists(stale))
                    {
                        File.Delete(stale);
                    }
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "delete stale graphics file failed: {file}", stale);
                }
            }
            records.Remove(existing);
        }

        records.Add(new InstalledGraphics
        {
            Id = component.Id,
            Name = component.Name,
            Kind = kindText,
            Source = component.Path,
            InstalledAt = DateTime.Now,
            Files = written,
            Sizes = sizes,
        });
        SaveAll(prefix, records);
        logger?.LogInformation("Installed {name} ({kind}) into {prefix}: {count} files", component.Name, kindText, prefix, written.Count);
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


    /// <summary>前缀里图形层的状态：每类组件的记录 + 实际文件核对结果。</summary>
    public sealed class GraphicsStatus
    {
        /// <summary>每一类已装的组件及其缺失文件数。</summary>
        public List<(InstalledGraphics Record, int MissingFiles)> Items { get; init; } = new();

        public int TotalFiles => Items.Sum(x => x.Record.Files.Count);

        public int MissingFiles => Items.Sum(x => x.MissingFiles);

        public bool AnyInstalled => Items.Count > 0;

        /// <summary>某一类是否已装且文件齐全。</summary>
        public bool IsHealthy(ComponentKind kind)
        {
            (InstalledGraphics Record, int MissingFiles) item = Items
                .FirstOrDefault(x => string.Equals(x.Record.Kind, KindTextOf(kind), StringComparison.OrdinalIgnoreCase));
            return item.Record is not null && item.MissingFiles == 0;
        }

        /// <summary>某一类的记录（没装则 null）。</summary>
        public InstalledGraphics? RecordOf(ComponentKind kind)
            => Items.FirstOrDefault(x => string.Equals(x.Record.Kind, KindTextOf(kind), StringComparison.OrdinalIgnoreCase)).Record;

        /// <summary>「DXVK 3.1.1 · VKD3D-Proton 2.14 · 与记录一致」/「未安装」。</summary>
        public string Text { get; init; } = "未安装";

        public bool IsGuardBroken => MissingFiles > 0;
    }


    /// <summary>核对该前缀的图形层：按各类记录看文件是否还在、大小是否对得上（不做 SHA，D63 §5 第 4 条）。</summary>
    public static GraphicsStatus Verify(string prefix)
    {
        List<InstalledGraphics> records = GetAllInstalled(prefix);
        if (records.Count == 0)
        {
            return new GraphicsStatus { Text = "未安装" };
        }
        List<(InstalledGraphics, int)> items = new();
        foreach (InstalledGraphics record in records)
        {
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
            items.Add((record, missing));
        }
        string parts = string.Join(" · ", items.Select(x => string.IsNullOrWhiteSpace(x.Item1.Name) ? x.Item1.Id : x.Item1.Name));
        int totalMissing = items.Sum(x => x.Item2);
        return new GraphicsStatus
        {
            Items = items,
            Text = totalMissing == 0 ? $"{parts} · 与记录一致" : $"{parts} · 不一致（{totalMissing} 个缺失）",
        };
    }


    /// <summary>从前缀里移除某一类图形组件（kind 为空则全部移除）：按记录删文件、删记录（保留 .bak）。</summary>
    public static bool Remove(string prefix, ComponentKind? kind = null, ILogger? logger = null)
    {
        List<InstalledGraphics> records = GetAllInstalled(prefix);
        if (records.Count == 0)
        {
            return false;
        }
        string? kindText = kind is null ? null : KindTextOf(kind.Value);
        // 注意 targets 必须是副本：删完文件后要按它过滤 records，边遍历边 Remove 会抛「集合被修改」（L-157）
        List<InstalledGraphics> targets = (kindText is null
            ? records
            : records.Where(x => string.Equals(x.Kind, kindText, StringComparison.OrdinalIgnoreCase))).ToList();
        if (targets.Count == 0)
        {
            return false;
        }
        int removed = 0;
        List<InstalledGraphics> keep = new();
        foreach (InstalledGraphics record in targets)
        {
            List<string> survivors = new();
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
                    survivors.Add(file);
                }
                if (survivors.Count == 0 && File.Exists(file))
                {
                    survivors.Add(file);
                }
            }
            if (survivors.Count == 0)
            {
                logger?.LogInformation("Removed graphics {name} ({kind}) from {prefix}", record.Id, record.Kind, prefix);
            }
            else
            {
                // R37：文件没删掉就不能把记录删掉，否则这些 dll 永远没人认领（也无法再「移除」）。
                logger?.LogWarning("[graphics] {count} file(s) of {name} could not be deleted, keep record", survivors.Count, record.Id);
                record.Files = survivors;
                keep.Add(record);
            }
        }
        records = records.Where(x => !targets.Contains(x) || keep.Contains(x)).ToList();
        try
        {
            if (records.Count == 0)
            {
                File.Delete(RecordPath(prefix));
            }
            else
            {
                SaveAll(prefix, records);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "delete graphics record failed: {prefix}", prefix);
        }
        logger?.LogInformation("Removed graphics from {prefix}: {count} files", prefix, removed);
        return true;
    }


    /// <summary>
    /// 收集组件里需要覆盖进前缀的 dll。R17：架构由「来源目录」决定，不再靠文件名猜
    /// （以前 `target.EndsWith("32.dll")` 会把名字里恰好含 32 的 64 位 dll 放进 syswow64）。
    /// DXVK 2.x 发布物是 `x64/ + x32/`（VKD3D-Proton 是 `x86/`），两种都要认，平铺目录按 64 位处理。
    /// </summary>
    private static List<(string Source, string Target, bool Is32Bit)> CollectFiles(Component component)
    {
        List<(string, string, bool)> result = new();
        string root = component.Path;
        // R56：组件目录被删/被移走时 EnumerateFiles 直接抛，批量安装会在这里中断，
        // 前面装好的组件之后静默跳过。
        if (!Directory.Exists(root))
        {
            return result;
        }
        string? x64 = Directory.Exists(Path.Combine(root, "x64")) ? Path.Combine(root, "x64") : null;
        string? x32 = Directory.Exists(Path.Combine(root, "x32")) ? Path.Combine(root, "x32")
            : Directory.Exists(Path.Combine(root, "x86")) ? Path.Combine(root, "x86")
            : null;
        if (x64 is null && x32 is null)
        {
            // 平铺目录（不区分架构）：只取顶层 dll，按 64 位装
            foreach (string file in Directory.EnumerateFiles(root, "*.dll", SearchOption.TopDirectoryOnly))
            {
                result.Add((file, "x64/" + Path.GetFileName(file), false));
            }
            return result;
        }
        if (x64 is not null)
        {
            foreach (string file in Directory.EnumerateFiles(x64, "*.dll", SearchOption.TopDirectoryOnly))
            {
                result.Add((file, "x64/" + Path.GetFileName(file), false));
            }
        }
        if (x32 is not null)
        {
            foreach (string file in Directory.EnumerateFiles(x32, "*.dll", SearchOption.TopDirectoryOnly))
            {
                result.Add((file, "x86/" + Path.GetFileName(file), true));
            }
        }
        return result;
    }

}
