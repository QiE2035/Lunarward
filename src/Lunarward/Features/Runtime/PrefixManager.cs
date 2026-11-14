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
using System.Collections.Generic;
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


    public DateTime CreatedAt { get; set; }
}


public static class PrefixManager
{
    /// <summary>
    /// 展开 Profile 里手写的路径：支持 `~/xxx`、`$HOME/xxx` 和 `${HOME}/xxx`。
    /// 上游的 Path 文本框里用户很自然会写 `~`，不展开就会在 CWD 下建出名字叫 "~" 的目录（L-62）。
    /// </summary>
    public static string? ExpandPath(string? path) => PrefixPath.Expand(path);



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


    /// <summary>前缀的一次性快照：给「运行环境」页面用（可见即可，不改任何东西）。</summary>
    public sealed class PrefixInspection
    {
        public string Path { get; init; } = "";

        public bool Exists { get; init; }

        public bool HasDriveC { get; init; }

        public bool HasRegistry { get; init; }

        public bool Locked { get; init; }

        public long SizeBytes { get; init; }

        public bool SizeIsStale { get; init; }

        public string? GameKey { get; init; }

        public string? ProfileId { get; init; }

        public string? Mode { get; init; }

        /// <summary>"我们的"（位于 PrefixRoot 下）还是外部目录。</summary>
        public bool Managed { get; init; }
    }


    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Size, DateTime At)> SizeCache = new();


    /// <summary>目录大小：带 10 分钟缓存（大前缀 du 一次要几百毫秒）。</summary>
    private static long ComputeSize(string path, out bool stale)
    {
        stale = false;
        if (SizeCache.TryGetValue(path, out (long Size, DateTime At) cached)
            && DateTime.UtcNow - cached.At < TimeSpan.FromMinutes(10))
        {
            return cached.Size;
        }
        stale = true;
        long size = 0;
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            foreach (string file in Directory.EnumerateFiles(path, "*", options))
            {
                try
                {
                    size += new FileInfo(file).Length;
                }
                catch
                {
                    // 单个文件读不到就跳过，不要因为一个损坏条目让整页报错
                }
            }
        }
        catch
        {
            return 0;
        }
        SizeCache[path] = (size, DateTime.UtcNow);
        return size;
    }


    /// <summary>检查一个前缀：存在性、结构、占用、大小。</summary>
    public static PrefixInspection Inspect(string? path, bool computeSize = true)
    {
        string prefix = ExpandPath(path) ?? "";
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return new PrefixInspection();
        }
        bool exists = Directory.Exists(prefix);
        if (!exists)
        {
            return new PrefixInspection { Path = prefix };
        }
        bool stale = false;
        long size = computeSize ? ComputeSize(prefix, out stale) : 0;
        PrefixInspection result = new()
        {
            Path = prefix,
            Exists = true,
            HasDriveC = Directory.Exists(Path.Combine(prefix, "drive_c")),
            HasRegistry = File.Exists(Path.Combine(prefix, "system.reg")) || File.Exists(Path.Combine(prefix, "user.reg")),
            Locked = IsPrefixLocked(prefix),
            SizeBytes = size,
            SizeIsStale = computeSize && stale,
            Managed = IsUnderPrefixRoot(prefix),
        };
        try
        {
            string meta = Path.Combine(prefix, ".lunarward", "prefix.json");
            if (File.Exists(meta))
            {
                using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(meta));
                result = new PrefixInspection
                {
                    Path = result.Path,
                    Exists = result.Exists,
                    HasDriveC = result.HasDriveC,
                    HasRegistry = result.HasRegistry,
                    Locked = result.Locked,
                    SizeBytes = result.SizeBytes,
                    SizeIsStale = result.SizeIsStale,
                    Managed = result.Managed,
                    GameKey = doc.RootElement.TryGetProperty("GameKey", out JsonElement g) ? g.GetString() : null,
                    ProfileId = doc.RootElement.TryGetProperty("ProfileId", out JsonElement p) ? p.GetString() : null,
                    Mode = doc.RootElement.TryGetProperty("Mode", out JsonElement m) ? m.GetString() : null,
                };
            }
        }
        catch
        {
            // 元数据坏了不影响页面显示
        }
        return result;
    }


    private static bool IsUnderPrefixRoot(string path)
    {
        try
        {
            string root = PrefixRoot.TrimEnd('/') + "/";
            return path.StartsWith(root, StringComparison.Ordinal)
                || string.Equals(path.TrimEnd('/'), PrefixRoot.TrimEnd('/'), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }


    /// <summary>页面上要显示的全部前缀：PrefixRoot 下的目录 + 方案里指定的外部目录。</summary>
    public static IReadOnlyList<PrefixInspection> EnumerateKnownPrefixes(IReadOnlyList<RuntimeProfile> profiles, bool computeSize = true)
    {
        List<string> paths = new();
        try
        {
            if (Directory.Exists(PrefixRoot))
            {
                paths.AddRange(Directory.EnumerateDirectories(PrefixRoot));
            }
        }
        catch
        {
            // 目录不可读就只显示方案里登记的
        }
        foreach (RuntimeProfile profile in profiles)
        {
            string? path = ExpandPath(profile.PrefixPath);
            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path!);
            }
        }
        List<PrefixInspection> result = new();
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string path in paths)
        {
            string full;
            try
            {
                full = Path.GetFullPath(path).TrimEnd('/');
            }
            catch
            {
                continue;
            }
            if (!seen.Add(full) || full.Length == 0)
            {
                continue;
            }
            result.Add(Inspect(full, computeSize));
        }
        return result;
    }


    /// <summary>
    /// 解析该配方使用的前缀；不存在就按默认架构新建（<c>wineboot -u</c>）。
    /// D73 §3.1：前缀只是路径——两个配方指同一路径就是共享，用完删就是临时，不再有「模式」。
    /// 失败抛异常（由调用方决定是否降级）。
    /// </summary>
    public static string EnsurePrefix(RuntimeProfile profile, string gameKey, ILogger? logger = null)
    {
        string path = EnvironmentResolver.EffectivePrefixPath(profile);
        if (!Directory.Exists(path))
        {
            // 上级目录可能是「前缀根目录」或随便一个文件夹：wine 会安静地在里面建空前缀，
            if (Directory.Exists(Path.GetDirectoryName(path)) && IsUnderPrefixRoot(path) is false)
            {
                logger?.LogInformation("Prefix {path} 不存在，将新建（配方未指定路径时落在默认目录）", path);
            }
            CreateWithWineboot(profile, path, logger);
            TightenPermissions(path);
        }
        else if (!Directory.Exists(Path.Combine(path, "drive_c")))
        {
            throw new IOException($"目录存在但不像 Wine 前缀（缺少 drive_c）：{path}。请换一个路径，或删掉该目录后重试。");
        }
        WriteMetadata(path, gameKey, profile);
        return path;
    }


    /// <summary>
    /// 前缀目录收紧到 0700：里面是 wine 注册表、游戏账号 token、登录 cookie，
    /// 默认 0755 会让同机其他用户读到。失败只记日志（不改外部目录的既有权限不是错误）。
    /// </summary>
    private static void TightenPermissions(string prefix)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            File.SetUnixFileMode(prefix, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "tighten prefix permissions failed: {Prefix}", prefix);
        }
    }


    /// <summary>
    /// 只解析「已经存在」的前缀，不创建、不写元数据。
    /// 设置类操作（例如写 HDR 注册表）用它，避免为了改一个开关就凭空造出一个前缀（L-62）。
    /// </summary>
    public static string? TryGetExistingPrefix(RuntimeProfile profile, string gameKey)
    {
        string path = EnvironmentResolver.EffectivePrefixPath(profile);
        return Directory.Exists(path) ? path : null;
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


    /// <summary>
    /// 在前缀里留一份侧写（D73 §3.5）：只记「谁用过、什么时候建的」，不碰注册表结构、不改目录名。
    /// </summary>
    private static void WriteMetadata(string prefix, string gameKey, RuntimeProfile profile)
    {
        try
        {
            PrefixMetadata? existing = GetMetadata(prefix);
            if (existing is not null && existing.GameKey == gameKey && existing.ProfileId == profile.Id)
            {
                return;
            }
            string path = Path.Combine(prefix, MetadataRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            JsonFile.WriteAtomic(path, new PrefixMetadata
            {
                GameKey = gameKey,
                ProfileId = profile.Id,
                Mode = "environment",
                CreatedAt = existing?.CreatedAt ?? DateTime.Now,
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            // 侧写失败不影响启动
        }
    }

}
