// Lunarward：把选定的 DXVK / VKD3D-PROTON 装进 Wine 前缀（M2 组件层）。
// 只做「复制 + 记录」，不下载、不联网：组件来源由用户放在系统目录或 ~/.local/share/lunarward/components。
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

        string system32 = Path.Combine(prefix, "drive_c", "windows", "system32");
        string syswow64 = Path.Combine(prefix, "drive_c", "windows", "syswow64");
        Directory.CreateDirectory(system32);
        List<string> written = new();
        foreach ((string source, string target) in files)
        {
            string dir = target.EndsWith("32.dll") || target.StartsWith("x86", StringComparison.Ordinal) ? syswow64 : system32;
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, Path.GetFileName(target));
            File.Copy(source, dest, overwrite: true);
            written.Add(dest);
        }

        InstalledGraphics record = new()
        {
            Id = component.Id,
            Name = component.Name,
            Kind = component.KindText,
            Source = component.Path,
            InstalledAt = DateTime.Now,
            Files = written,
        };
        Directory.CreateDirectory(Path.GetDirectoryName(RecordPath(prefix))!);
        File.WriteAllText(RecordPath(prefix), JsonSerializer.Serialize(record, JsonOptions));
        logger?.LogInformation("Installed {name} ({kind}) into {prefix}: {count} files", component.Name, component.KindText, prefix, written.Count);
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
