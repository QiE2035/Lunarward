// Lunarward：Linux 侧「识别已有安装」（D25 的 M1 范围）。
// 上游 AutoSearchInstalledGames 靠注册表 HKCU\Software\miHoYo\HYP\... 找安装路径，
// Linux 没有注册表可读，改为扫目录：按每个游戏的可执行文件名在候选根目录下找（深度 ≤ 2）。
using Microsoft.Extensions.Logging;
using Starward.Core;
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


    public static int ScanAndApply(ILogger? logger = null)
    {
        int count = 0;
        List<GameBiz> detected = new();
        try
        {
            List<string> roots = GetCandidateRoots();
            foreach (GameBiz biz in _knownGameBizs)
            {
                string? exeName = Features.GameLauncher.GameLauncherService.GetGameExeName(biz);
                if (string.IsNullOrWhiteSpace(exeName))
                {
                    continue;
                }
                string? existing = AppConfig.GetGameInstallPath(biz);
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
                        AppConfig.SetGameInstallPath(biz, dir);
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
            .Where(b => { string? p = AppConfig.GetGameInstallPath(b); return !string.IsNullOrWhiteSpace(p) && Directory.Exists(p); })
            .ToArray();
        // 收尾：同一路径被多个 gameBiz 认领时，只保留目录名对得上的那个（国服/国际服 exe 同名）
        try
        {
            var claims = new Dictionary<string, List<GameBiz>>(StringComparer.Ordinal);
            foreach (GameBiz biz in _knownGameBizs)
            {
                string? path = AppConfig.GetGameInstallPath(biz);
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
                        AppConfig.SetGameInstallPath(biz, null);
                    }
                }
            }
        }
        catch { }

        return count;
    }


    private static List<string> GetCandidateRoots()
    {
        List<string> roots = [];
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string? extra = Environment.GetEnvironmentVariable("LUNARWARD_GAME_ROOTS");
        if (!string.IsNullOrWhiteSpace(extra))
        {
            roots.AddRange(extra.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }
        roots.Add(Path.Combine(home, "games"));
        roots.Add(Path.Combine(home, "Games"));
        roots.Add(Path.Combine(home, "Games", "miHoYo"));
        roots.Add(Path.Combine(home, ".local", "share", "Steam", "steamapps", "common"));
        roots.Add(Path.Combine(home, ".steam", "steam", "steamapps", "common"));
        return roots.Where(Directory.Exists).Distinct().ToList();
    }


    /// <summary>
    /// 在 root 下按「直接子目录、子目录的下一级」顺序找包含 exeName 的目录。
    /// </summary>
    /// <summary>
    /// 目录名是否对得上这个 gameBiz（`hk4e_cn` 这种精确命中，或目录名里含 game+server）。
    /// </summary>
    private static bool NameMatches(string dir, GameBiz biz)
    {
        string name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar)).ToLowerInvariant();
        string key = biz.Value.ToLowerInvariant();
        if (name == key)
        {
            return true;
        }
        string game = biz.Game.ToLowerInvariant();
        string server = biz.Server.ToLowerInvariant();
        return name.Contains(game) && name.Contains(server);
    }


    private static string? FindGameDirectory(string root, string exeName, GameBiz biz)
    {
        try
        {
            if (File.Exists(Path.Combine(root, exeName)) && NameMatches(root, biz))
            {
                return root;
            }
            foreach (string dir in Directory.EnumerateDirectories(root))
            {
                if (File.Exists(Path.Combine(dir, exeName)))
                {
                    // 同名的 exe 在多个服务器版本里相同（YuanShen.exe / StarRail.exe），
                    // 只有目录名能对上 gameBiz 时才算命中，避免把国服错认成国际服。
                    if (NameMatches(dir, biz))
                    {
                        return dir;
                    }
                    continue;
                }
                foreach (string sub in Directory.EnumerateDirectories(dir))
                {
                    if (File.Exists(Path.Combine(sub, exeName)) && NameMatches(sub, biz))
                    {
                        return sub;
                    }
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
