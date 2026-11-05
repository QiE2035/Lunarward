// Lunarward：日志与临时文件的「定期回收」。
//
// Serilog 的 File sink 只负责写，不管删；我们每天一个 Starward_yymmdd.log，
// 不清理就会一直涨（L-62）。这里在启动时做一次轻量清理：
//   - log/ 下超过 14 天的 Starward_*.log
//   - logs/output、logs/launch 下超过 14 天的文件
// 只删我们自己命名的文件，不动用户目录里的其它东西。

using Serilog;
using Starward.Helpers;
using System;
using System.IO;

namespace Starward.Helpers;

internal static class Housekeeping
{

    private const int RetentionDays = 14;


    public static void Run(string cacheFolder)
    {
        try
        {
            DateTime threshold = DateTime.Now.AddDays(-RetentionDays);
            PruneDirectory(Path.Combine(cacheFolder, "log"), "Starward_*.log", threshold);
            PruneDirectory(Path.Combine(cacheFolder, "logs", "output"), "*.log", threshold);
            PruneDirectory(Path.Combine(cacheFolder, "logs", "launch"), "*.json", threshold);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Housekeeping failed");
        }
    }


    private static void PruneDirectory(string folder, string pattern, DateTime threshold)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }
        int removed = 0;
        foreach (string file in Directory.EnumerateFiles(folder, pattern, SearchOption.TopDirectoryOnly))
        {
            try
            {
                if (File.GetLastWriteTime(file) < threshold)
                {
                    File.Delete(file);
                    removed++;
                }
            }
            catch
            {
                // 正被别的实例写着的日志删不掉，跳过即可
            }
        }
        if (removed > 0)
        {
            Log.Information("Housekeeping: removed {count} old files from {folder}", removed, folder);
        }
    }


    /// <summary>
    /// 清理某个安装目录里「超过 3 天没动过」的断点续传临时文件。
    /// 保留 3 天是为了让断点续传真的能跨天恢复；再久基本就是用户放弃了。
    /// </summary>
    public static void PruneStaleTempFiles(string installPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(installPath) || !Directory.Exists(installPath))
            {
                return;
            }
            DateTime threshold = DateTime.Now.AddDays(-3);
            int removed = 0;
            long bytes = 0;
            foreach (string file in Directory.EnumerateFiles(installPath, "*.lunarward-tmp", SearchOption.AllDirectories))
            {
                try
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTime < threshold)
                    {
                        bytes += info.Length;
                        info.Delete();
                        removed++;
                    }
                }
                catch
                {
                }
            }
            if (removed > 0)
            {
                Log.Information("Housekeeping: removed {count} stale temp files ({size}) under {path}",
                                removed, Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(bytes), installPath);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Prune stale temp files failed: {path}", installPath);
        }
    }

}
