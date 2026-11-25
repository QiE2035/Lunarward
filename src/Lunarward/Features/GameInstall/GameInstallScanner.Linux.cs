// Lunarward：Linux 侧「识别已有安装」（D25 的 M1 范围）。
// 上游 AutoSearchInstalledGames 靠注册表 HKCU\Software\miHoYo\HYP\... 找安装路径，
// Linux 没有注册表可读，改为扫目录：按每个游戏的可执行文件名在候选根目录下找（深度 ≤ 2）。
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameSelector;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Starward.Features.GameInstall;

internal static class GameInstallScanner
{

    /// <summary>本次扫描新识别到的游戏（供 UI 提示用）。</summary>
    public static IReadOnlyList<GameBiz> LastDetectedGames { get; private set; } = Array.Empty<GameBiz>();

    /// <summary>扫描结束后所有有效安装（含先前已记录的），供 UI 提示用。</summary>
    public static IReadOnlyList<GameBiz> KnownGames { get; private set; } = Array.Empty<GameBiz>();


    private static readonly GameBiz[] _knownGameBizs =
    [
        GameBiz.hk4e_cn, GameBiz.hk4e_bilibili, GameBiz.hk4e_global,
        GameBiz.hkrpg_cn, GameBiz.hkrpg_global,
        GameBiz.nap_cn, GameBiz.nap_global,
        GameBiz.bh3_cn, GameBiz.bh3_global,
    ];


    /// <summary>
    /// 扫描候选目录并把命中的安装路径写进 AppConfig，返回命中的游戏数量。
    /// </summary>
    private static List<string> _detected = new();


    /// <summary>
    /// 读安装路径。LUNARWARD-DEVIATION: 上游 0.19.0 把安装路径从 AppConfig 移到 GameEntry 表
    /// （AppConfig.GetGameInstallPath/SetGameInstallPath 已 [Obsolete(..., true)]），这里统一走
    /// GameLauncherService 的静态读写入口，避免依赖 HoYoPlayService 的初始化时机（扫描发生在启动早期）。
    /// </summary>
    private static string? GetInstallPath(GameBiz biz)
    {
        GameId? gameId = GameId.FromGameBiz(biz);
        return gameId is null ? null : Features.GameLauncher.GameLauncherService.GetGameInstallPath(gameId);
    }


    /// <summary>写安装路径（GameEntry 表；可移动存储记相对路径）。</summary>
    private static void SetInstallPath(GameBiz biz, string? path)
    {
        GameId? gameId = GameId.FromGameBiz(biz);
        if (gameId is null)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(path))
        {
            GameEntryService.SetInstallPathNull(gameId);
            return;
        }
        string launcherId = LauncherId.FromGameBiz(biz) ?? LauncherId.ChinaOfficial;
        string relativePath = Features.GameLauncher.GameLauncherService.GetRelativePathIfInRemovableStorage(path, out bool removable);
        GameEntryService.SetInstallPath(gameId, launcherId, relativePath, removable);
    }


    public static int ScanAndApply(ILogger? logger = null, bool force = false)
    {
        int count = 0;
        List<GameBiz> detected = new();
        try
        {
            // 增量：24 小时内且已知安装都还在，就不重复扫盘（D56）。用户改了扫描根会 MarkDirty。
            if (!force && IsCacheFresh())
            {
                bool allAlive = _knownGameBizs.All(b =>
                {
                    string? p = GetInstallPath(b);
                    return string.IsNullOrWhiteSpace(p) || Directory.Exists(p);
                });
                if (allAlive)
                {
                    logger?.LogDebug("skip game scan: cache is fresh");
                    KnownGames = _knownGameBizs
                        .Where(b => { string? p = GetInstallPath(b); return !string.IsNullOrWhiteSpace(p) && Directory.Exists(p); })
                        .ToArray();
                    LastDetectedGames = Array.Empty<GameBiz>();
                    return 0;
                }
            }
            List<string> roots = GetCandidateRoots();
            foreach (GameBiz biz in _knownGameBizs)
            {
                string? exeName = Features.GameLauncher.GameLauncherService.GetGameExeName(biz);
                if (string.IsNullOrWhiteSpace(exeName))
                {
                    continue;
                }
                string? existing = GetInstallPath(biz);
                if (!string.IsNullOrWhiteSpace(existing) && Directory.Exists(existing))
                {
                    if (NameMatches(existing, biz))
                    {
                        continue;
                    }
                    // 旧版本按 exe 名匹配过，可能把国服路径错记成国际服；目录名对不上就重扫
                }
                foreach (string root in roots)
                {
                    string? dir = FindGameDirectory(root, exeName, biz);
                    if (dir is not null)
                    {
                        SetInstallPath(biz, dir);
                        logger?.LogInformation("自动识别到 {biz} 安装目录：{dir}", biz, dir);
                        detected.Add(biz);
                        count++;
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "扫描已安装游戏失败");
        }
        LastDetectedGames = detected;
        KnownGames = _knownGameBizs
            .Where(b => { string? p = GetInstallPath(b); return !string.IsNullOrWhiteSpace(p) && Directory.Exists(p); })
            .ToArray();
        // 收尾：同一路径被多个 gameBiz 认领时，只保留目录名对得上的那个（国服/国际服 exe 同名）
        try
        {
            var claims = new Dictionary<string, List<GameBiz>>(StringComparer.Ordinal);
            foreach (GameBiz biz in _knownGameBizs)
            {
                string? path = GetInstallPath(biz);
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }
                if (!claims.TryGetValue(path, out var list))
                {
                    claims[path] = list = [];
                }
                list.Add(biz);
            }
            foreach ((string path, List<GameBiz> bizs) in claims)
            {
                if (bizs.Count <= 1)
                {
                    continue;
                }
                foreach (GameBiz biz in bizs)
                {
                    if (!NameMatches(path, biz))
                    {
                        SetInstallPath(biz, null);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // 收尾去重失败不该让整次扫描失败，但必须留下痕迹：否则「明明装了却认不到」会完全没有线索。
            logger?.LogWarning(ex, "清理重复安装路径记录失败（不影响本次扫描结果）");
        }

        TouchCache();
        return count;
    }


    /// <summary>默认的游戏扫描根（D56）：常见自建目录 + Steam/Flatpak 库。</summary>
    public static string[] DefaultScanRoots()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return
        [
            Path.Combine(home, "games"),
            Path.Combine(home, "Games"),
            Path.Combine(home, "Games", "miHoYo"),
            "/opt",
            Path.Combine(home, ".local", "share", "Steam", "steamapps", "common"),
            Path.Combine(home, ".steam", "steam", "steamapps", "common"),
            Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam", "steamapps", "common"),
        ];
    }


    /// <summary>用户配置的扫描根（设置里可改，换行或 `:` 分隔；空则用默认）。</summary>
    public static IReadOnlyList<string> ScanRoots
    {
        get
        {
            try
            {
                string? raw = AppConfig.GetValue<string>(null, "GameScanRoots");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    string[] custom = raw.Split(new[] { ':', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                         .Where(Directory.Exists)
                                         .ToArray();
                    if (custom.Length > 0)
                    {
                        return custom;
                    }
                }
            }
            catch
            {
            }
            return DefaultScanRoots().Where(Directory.Exists).ToArray();
        }
    }


    public static void SetScanRoots(IEnumerable<string> roots)
    {
        AppConfig.SetValue(string.Join(Environment.NewLine, roots.Where(x => !string.IsNullOrWhiteSpace(x))), "GameScanRoots");
        MarkDirty();
    }


    /// <summary>下次启动强制重扫（用户增删扫描根、手动点「重新扫描」时用）。</summary>
    public static void MarkDirty()
    {
        try
        {
            AppConfig.SetValue("", "GameScanLastUtc");
        }
        catch
        {
        }
    }


    private static bool IsCacheFresh()
    {
        try
        {
            string? raw = AppConfig.GetValue<string>(null, "GameScanLastUtc");
            if (string.IsNullOrWhiteSpace(raw) || !DateTime.TryParse(raw, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime last))
            {
                return false;
            }
            return DateTime.UtcNow - last < TimeSpan.FromHours(24);
        }
        catch
        {
            return false;
        }
    }


    private static void TouchCache()
    {
        try
        {
            AppConfig.SetValue(DateTime.UtcNow.ToString("O"), "GameScanLastUtc");
        }
        catch
        {
        }
    }


    private static List<string> GetCandidateRoots()
    {
        // 只有「设置里配置的根 → 内置默认根」这一条来源（D134：删掉 LUNARWARD_GAME_ROOTS 逃生口，
        // 它绕过设置页持久化，用户看不见自己多设了哪些根）。
        List<string> roots = ScanRoots.Where(Directory.Exists).Distinct().ToList();
        foreach (string root in roots)
        {
            Serilog.Log.Debug("game scan root: {root}", root);
        }
        return roots;
    }


    /// <summary>
    /// 目录名对应的游戏别名（D95：真实世界目录名是「Genshin Impact」「Star Rail」这类，
    /// 既不含 biz，也不含 cn/global，旧实现的 game+server 双条件永远匹配不上）。
    /// </summary>
    private static string[] GameAliases(GameBiz biz) => biz.Game.ToLowerInvariant() switch
    {
        "hk4e" => ["hk4e", "genshin", "yuanshen", "genshinimpact", "原神"],
        "hkrpg" => ["hkrpg", "starrail", "star rail", "honkaistarrail", "星穹铁道", "崩坏：星穹铁道", "崩坏星穹铁道"],
        "nap" => ["nap", "zenless", "zenlesszonezero", "绝区零"],
        "bh3" => ["bh3", "honkaiimpact", "honkai impact", "崩坏3", "崩坏三"],
        _ => [biz.Game.ToLowerInvariant()],
    };


    private static string[] ServerMarkers(string server) => server.ToLowerInvariant() switch
    {
        "cn" => ["cn", "chinese", "国服", "官服", "bilibili", "b服", "miyoushe", "米游社"],
        "global" => ["global", "国际服", "海外", "hoyoverse", "os"],
        _ => [server.ToLowerInvariant()],
    };


    private static string Normalize(string name)
        => new(name.ToLowerInvariant().Where(c => !char.IsWhiteSpace(c) && c is not '_' and not '-' and not '.').ToArray());


    private static bool HasGlobalMarker(string dir)
        => Directory.Exists(Path.Combine(dir, "GenshinImpact_Data")) || File.Exists(Path.Combine(dir, "GenshinImpact.exe"));


    /// <summary>
    /// 目录名是否对得上这个 gameBiz：别名命中 + 区服标记（没标记时按国服/国际服数据目录判定）。
    /// </summary>
    private static bool NameMatches(string dir, GameBiz biz)
    {
        try
        {
            string raw = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }
            string name = Normalize(raw);
            string key = Normalize(biz.Value);
            if (name == key)
            {
                return true;
            }
            if (!GameAliases(biz).Select(Normalize).Any(alias => alias.Length > 0 && name.Contains(alias)))
            {
                return false;
            }
            string server = biz.Server.ToLowerInvariant();
            string other = server == "global" ? "cn" : "global";
            if (ServerMarkers(other).Select(Normalize).Any(marker => marker.Length > 0 && name.Contains(marker)))
            {
                return false;
            }
            if (ServerMarkers(server).Select(Normalize).Any(marker => marker.Length > 0 && name.Contains(marker)))
            {
                return true;
            }
            // 目录名不带区服标记：国际服用数据目录/exe 名区分（hk4e 的 GenshinImpact_Data），其余按国服处理
            return server == "cn" || HasGlobalMarker(dir);
        }
        catch
        {
            return false;
        }
    }


    /// <summary>
    /// 在 root 下按广度优先找包含 exeName 的目录（深度 ≤ 3，D95：旧实现只看两层，
    /// `~/Games/miHoYo/Genshin Impact` 这种三层目录认不到）。符号链接目录不跟随，避免环路。
    /// </summary>
    private static string? FindGameDirectory(string root, string exeName, GameBiz biz)
    {
        try
        {
            string fullRoot = Path.GetFullPath(root);
            if (File.Exists(Path.Combine(fullRoot, exeName)) && NameMatches(fullRoot, biz))
            {
                return fullRoot;
            }
            var queue = new Queue<(string Dir, int Depth)>();
            queue.Enqueue((fullRoot, 0));
            var visited = new HashSet<string>(StringComparer.Ordinal) { fullRoot };
            while (queue.Count > 0)
            {
                (string dir, int depth) = queue.Dequeue();
                if (depth >= 3)
                {
                    continue;
                }
                IEnumerable<string> children;
                try
                {
                    children = Directory.EnumerateDirectories(dir);
                }
                catch
                {
                    continue;
                }
                foreach (string child in children)
                {
                    if (!visited.Add(child))
                    {
                        continue;
                    }
                    if (new DirectoryInfo(child).Attributes.HasFlag(FileAttributes.ReparsePoint))
                    {
                        continue;
                    }
                    if (File.Exists(Path.Combine(child, exeName)) && NameMatches(child, biz))
                    {
                        return child;
                    }
                    queue.Enqueue((child, depth + 1));
                }
            }
        }
        catch (Exception)
        {
            // 权限不足/目录消失都跳过，不影响其它候选根目录
        }
        return null;
    }

}
