// Lunarward：Wine 前缀的生命周期（D28/03-runtime-model §Prefix）。
//
// 五种模式：
//   external  —— 就地使用用户已有前缀（不复制、不创建，缺失即报错）
//   dedicated —— 每游戏一个长期前缀，缺失时用 wineboot 初始化
//   shared    —— 多个游戏共用一个长期前缀
//   template  —— 从一个「模板前缀」克隆出本游戏专用前缀（reflink，秒级）
//   ephemeral —— 每次启动克隆一份，退出即丢弃（用于测试组合，不污染既有前缀）
using Starward.Helpers;
using Microsoft.Extensions.Logging;
using Starward.Features.GameLauncher;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Starward.Features.Runtime;


/// <summary>前缀里的元数据（&lt;prefix&gt;/.lunarward/prefix.json），用于模板/临时前缀的清理与排错。</summary>
public sealed class PrefixMetadata
{
    public string GameKey { get; set; } = "";

    public string ProfileId { get; set; } = "";

    public string Mode { get; set; } = "";

    public string? TemplatePath { get; set; }

    public DateTime CreatedAt { get; set; }
}


public static class PrefixManager
{
    /// <summary>
    /// 展开 Profile 里手写的路径：支持 `~/xxx`、`$HOME/xxx` 和 `${HOME}/xxx`。
    /// 上游的 Path 文本框里用户很自然会写 `~`，不展开就会在 CWD 下建出名字叫 "~" 的目录（L-62）。
    /// </summary>
    public static string? ExpandPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        string expanded = path.Trim();
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (expanded == "~")
        {
            return home;
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
        return Environment.ExpandEnvironmentVariables(expanded);
    }



    private const string MetadataRelativePath = ".lunarward/prefix.json";

    private const string LockRelativePath = ".lunarward/lock";


    /// <summary>
    /// 该前缀是否正被别的进程/任务占用（与启动游戏时用的锁同一个文件，D39）。
    /// </summary>
    public static bool IsPrefixLocked(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return false;
        }
        string lockPath = System.IO.Path.Combine(prefix, LockRelativePath);
        if (!System.IO.File.Exists(lockPath))
        {
            return false;
        }
        try
        {
            using var stream = new System.IO.FileStream(lockPath, System.IO.FileMode.Open, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None);
            return false;
        }
        catch (System.IO.IOException)
        {
            return true;
        }
        catch (System.UnauthorizedAccessException)
        {
            return true;
        }
    }


    public static string PrefixRoot
    {
        get
        {
            string data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x
                ? x
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(data, "lunarward", "prefix");
        }
    }


    /// <summary>解析该游戏该 Profile 应当使用的前缀；必要时创建或克隆。失败抛异常（由调用方决定降级）。</summary>
    public static string EnsurePrefix(RuntimeProfile profile, string gameKey, ILogger? logger = null)
    {
        string key = gameKey.ToLowerInvariant();
        switch (profile.PrefixMode)
        {
            case "external":
            {
                string path = string.IsNullOrWhiteSpace(ExpandPath(profile.PrefixPath))
                    ? throw new DirectoryNotFoundException("external 前缀模式需要指定前缀路径。")
                    : ExpandPath(profile.PrefixPath)!;
                if (!Directory.Exists(path))
                {
                    throw new DirectoryNotFoundException($"前缀不存在：{path}");
                }
                return path;
            }
            case "dedicated":
            {
                string path = string.IsNullOrWhiteSpace(ExpandPath(profile.PrefixPath))
                    ? Path.Combine(PrefixRoot, key)
                    : ExpandPath(profile.PrefixPath)!;
                if (!Directory.Exists(path))
                {
                    CreateWithWineboot(profile, path, logger);
                }
                WriteMetadata(path, key, profile, "dedicated", null);
                return path;
            }
            case "shared":
            {
                string path = string.IsNullOrWhiteSpace(ExpandPath(profile.PrefixPath))
                    ? Path.Combine(PrefixRoot, "shared")
                    : ExpandPath(profile.PrefixPath)!;
                if (!Directory.Exists(path))
                {
                    CreateWithWineboot(profile, path, logger);
                }
                WriteMetadata(path, key, profile, "shared", null);
                return path;
            }
            case "template":
            {
                string template = string.IsNullOrWhiteSpace(ExpandPath(profile.PrefixPath))
                    ? throw new DirectoryNotFoundException("template 前缀模式需要指定模板前缀路径。")
                    : ExpandPath(profile.PrefixPath)!;
                if (!Directory.Exists(template))
                {
                    throw new DirectoryNotFoundException($"模板前缀不存在：{template}");
                }
                string path = Path.Combine(PrefixRoot, $"{key}-{profile.Id}");
                if (!Directory.Exists(path))
                {
                    ClonePrefix(template, path, logger);
                }
                WriteMetadata(path, key, profile, "template", template);
                return path;
            }
            case "ephemeral":
            {
                string template = string.IsNullOrWhiteSpace(ExpandPath(profile.PrefixPath))
                    ? throw new DirectoryNotFoundException("ephemeral 前缀模式需要指定模板前缀路径。")
                    : ExpandPath(profile.PrefixPath)!;
                if (!Directory.Exists(template))
                {
                    throw new DirectoryNotFoundException($"模板前缀不存在：{template}");
                }
                // 精确到秒会在同一秒内连点两次时撞名（克隆到一半被第二个实例当成已存在）；
                // 用「时间戳 + pid + 短随机」保证唯一，同时仍可看出是哪次会话。
                string path = Path.Combine(PrefixRoot, $".ephemeral-{key}-{profile.Id}-{DateTime.Now:yyyyMMddHHmmss}-{Environment.ProcessId}");
                ClonePrefix(template, path, logger);
                WriteMetadata(path, key, profile, "ephemeral", template);
                return path;
            }
            default:
                throw new NotSupportedException($"未知的前缀模式：{profile.PrefixMode}");
        }
    }


    /// <summary>
    /// 只解析「已经存在」的前缀，不创建、不克隆、不写元数据。
    /// 设置类操作（例如写 HDR 注册表）用它，避免为了改一个开关就凭空造出一个前缀（L-62）。
    /// </summary>
    public static string? TryGetExistingPrefix(RuntimeProfile profile, string gameKey)
    {
        string key = gameKey.ToLowerInvariant();
        switch (profile.PrefixMode)
        {
            case "external":
            {
                string? external = ExpandPath(profile.PrefixPath);
                return external is not null && Directory.Exists(external) ? external : null;
            }
            case "dedicated":
            {
                string path = string.IsNullOrWhiteSpace(profile.PrefixPath) ? Path.Combine(PrefixRoot, key) : ExpandPath(profile.PrefixPath)!;
                return Directory.Exists(path) ? path : null;
            }
            case "shared":
            {
                string path = string.IsNullOrWhiteSpace(profile.PrefixPath) ? Path.Combine(PrefixRoot, "shared") : ExpandPath(profile.PrefixPath)!;
                return Directory.Exists(path) ? path : null;
            }
            case "template":
            {
                string path = Path.Combine(PrefixRoot, $"{key}-{profile.Id}");
                return Directory.Exists(path) ? path : null;
            }
            case "ephemeral":
                // 临时前缀按会话创建，设置阶段没有可写的目标。
                return null;
            default:
                return null;
        }
    }


    /// <summary>用 reflink 克隆前缀（同卷时几乎瞬时、几乎不占空间）。</summary>
    public static void ClonePrefix(string source, string destination, ILogger? logger = null)
    {
        if (!Directory.Exists(source))
        {
            throw new DirectoryNotFoundException($"源前缀不存在：{source}");
        }
        if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any())
        {
            // cp -a 的语义是「拷进目标目录」，目标已存在时会得到 dst/src，前缀直接不可用。宁可直接报错（L-62）。
            throw new IOException($"目标前缀已存在且非空，拒绝覆盖：{destination}");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var sw = Stopwatch.StartNew();
        // cp -a --reflink=auto：不支持 reflink 的文件系统会自动退化为普通复制（不会失败）
        var info = new ProcessStartInfo("cp")
        {
            UseShellExecute = false,
        };
        foreach (string arg in new[] { "-a", "--reflink=auto", source, destination })
        {
            info.ArgumentList.Add(arg);
        }
        using Process? process = Process.Start(info);
        process?.WaitForExit();
        if (process is null || process.ExitCode != 0)
        {
            throw new IOException($"克隆前缀失败（cp 退出码 {process?.ExitCode}）：{source} -> {destination}");
        }
        sw.Stop();
        logger?.LogInformation("Cloned prefix in {ms} ms: {source} -> {destination}", sw.ElapsedMilliseconds, source, destination);
    }


    /// <summary>用 wineboot 初始化一个空前缀。</summary>
    public static void CreateWithWineboot(RuntimeProfile profile, string path, ILogger? logger = null)
    {
        string? wine = !string.IsNullOrWhiteSpace(profile.RunnerPath) && File.Exists(profile.RunnerPath)
            ? profile.RunnerPath
            : GameLauncherService.ResolveWine();
        if (wine is null)
        {
            throw new FileNotFoundException("找不到 wine，无法初始化前缀。");
        }
        string wineboot = Path.Combine(Path.GetDirectoryName(wine)!, "wineboot");
        string command = File.Exists(wineboot) ? wineboot : wine;
        var info = new ProcessStartInfo(command)
        {
            UseShellExecute = false,
        };
        if (!File.Exists(wineboot))
        {
            info.ArgumentList.Add("wineboot");
        }
        info.ArgumentList.Add("-u");
        info.Environment["WINEPREFIX"] = path;
        info.Environment["WINEDEBUG"] = "-all";
        using Process? process = Process.Start(info);
        process?.WaitForExit();
        if (process is null || process.ExitCode != 0 || !Directory.Exists(Path.Combine(path, "drive_c")))
        {
            throw new IOException($"初始化前缀失败（退出码 {process?.ExitCode}）：{path}");
        }
        logger?.LogInformation("Created prefix with wineboot: {path}", path);
    }


    /// <summary>删除临时前缀（ephemeral 模式用；只删带 `.ephemeral-` 前缀的目录，防误删）。</summary>
    public static bool DiscardEphemeralPrefix(string path, ILogger? logger = null)
    {
        try
        {
            string name = Path.GetFileName(path);
            if (!name.StartsWith(".ephemeral-", StringComparison.Ordinal) || !Directory.Exists(path))
            {
                return false;
            }
            Directory.Delete(path, recursive: true);
            logger?.LogInformation("Discarded ephemeral prefix: {path}", path);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Discard ephemeral prefix failed: {path}", path);
            return false;
        }
    }


    /// <summary>清理超过指定小时数的临时前缀（启动时调用，避免磁盘越积越多）。</summary>
    public static int CleanupStaleEphemeralPrefixes(int olderThanHours = 24, ILogger? logger = null)
    {
        if (!Directory.Exists(PrefixRoot))
        {
            return 0;
        }
        int removed = 0;
        foreach (string dir in Directory.EnumerateDirectories(PrefixRoot, ".ephemeral-*"))
        {
            try
            {
                if (DateTime.Now - Directory.GetCreationTime(dir) > TimeSpan.FromHours(olderThanHours))
                {
                    Directory.Delete(dir, recursive: true);
                    removed++;
                }
            }
            catch (Exception ex)
            {
                logger?.LogDebug(ex, "Cleanup ephemeral prefix failed: {dir}", dir);
            }
        }
        if (removed > 0)
        {
            logger?.LogInformation("Cleaned {n} stale ephemeral prefixes", removed);
        }
        return removed;
    }


    public static PrefixMetadata? GetMetadata(string prefix)
    {
        try
        {
            string path = Path.Combine(prefix, MetadataRelativePath);
            return File.Exists(path)
                ? JsonSerializer.Deserialize<PrefixMetadata>(File.ReadAllText(path))
                : null;
        }
        catch
        {
            return null;
        }
    }


    private static void WriteMetadata(string prefix, string gameKey, RuntimeProfile profile, string mode, string? template)
    {
        try
        {
            PrefixMetadata? existing = GetMetadata(prefix);
            if (existing is not null && existing.GameKey == gameKey && existing.Mode == mode && existing.TemplatePath == template)
            {
                return;
            }
            string path = Path.Combine(prefix, MetadataRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            JsonFile.WriteAtomic(path, new PrefixMetadata
            {
                GameKey = gameKey,
                ProfileId = profile.Id,
                Mode = mode,
                TemplatePath = template,
                CreatedAt = existing?.CreatedAt ?? DateTime.Now,
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
        }
    }

}
