// Lunarward：安装/预下载/更新规划（只比对，不下载）。
//
// 判据优先级：文件大小 → MD5（可选）。默认只比大小——真实安装有几十 GB，
// 每次规划都算全量 MD5 会让「点开设置页」变成几分钟的磁盘扫描。

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameInstall.Sophon;


public enum InstallFileState
{
    Unchanged,
    Changed,
    Missing,
}


public sealed class InstallFilePlan
{
    public required string Path { get; init; }

    public required InstallFileState State { get; init; }

    public required long Size { get; init; }

    public required long CompressedBytes { get; init; }

    /// <summary>本地大小（缺失时为 -1）。</summary>
    public required long LocalSize { get; init; }

    public bool MD5Checked { get; init; }
}


public sealed class InstallPlan
{
    public required string InstallPath { get; init; }

    public required IReadOnlyList<InstallFilePlan> Files { get; init; }

    public long TotalDownloadBytes => Files.Where(x => x.State != InstallFileState.Unchanged).Sum(x => x.CompressedBytes);

    public int ChangedCount => Files.Count(x => x.State == InstallFileState.Changed);

    public int MissingCount => Files.Count(x => x.State == InstallFileState.Missing);

    public int UnchangedCount => Files.Count(x => x.State == InstallFileState.Unchanged);

    /// <summary>是否可以在本地版本基础上增量更新（有变更但绝大部分文件仍在）。</summary>
    public bool CanIncremental => MissingCount * 2 < Files.Count;
}


public static class InstallPlanner
{

    /// <summary>
    /// 用清单和本地目录算出一份安装计划。
    /// </summary>
    /// <param name="sophonFiles">清单文件（多个类别合并后）</param>
    /// <param name="installPath">本地安装目录（不存在视为全新安装）</param>
    /// <param name="checkMD5">是否对大小相同的文件逐一核对 MD5</param>
    public static async Task<InstallPlan> CreateAsync(IEnumerable<SophonChunkFile> sophonFiles, string installPath, bool checkMD5 = false, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        var plans = new List<InstallFilePlan>();
        int index = 0;
        foreach (SophonChunkFile file in sophonFiles.Where(x => !x.IsFolder).OrderBy(x => x.File, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(++index);
            string localPath = System.IO.Path.Combine(installPath, file.File.Replace('/', System.IO.Path.DirectorySeparatorChar));
            long localSize = -1;
            InstallFileState state = InstallFileState.Missing;
            bool md5Checked = false;
            try
            {
                var info = new FileInfo(localPath);
                if (info.Exists)
                {
                    localSize = info.Length;
                    if (localSize != file.Size)
                    {
                        state = InstallFileState.Changed;
                    }
                    else if (!checkMD5 || !string.IsNullOrEmpty(file.MD5))
                    {
                        if (checkMD5)
                        {
                            string localMD5 = await ComputeMD5Async(localPath, cancellationToken);
                            md5Checked = true;
                            state = string.Equals(localMD5, file.MD5, StringComparison.OrdinalIgnoreCase)
                                ? InstallFileState.Unchanged
                                : InstallFileState.Changed;
                        }
                        else
                        {
                            state = InstallFileState.Unchanged;
                        }
                    }
                    else
                    {
                        state = InstallFileState.Unchanged;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                state = InstallFileState.Missing;
            }
            catch (DirectoryNotFoundException)
            {
                state = InstallFileState.Missing;
            }

            plans.Add(new InstallFilePlan
            {
                Path = file.File,
                State = state,
                Size = file.Size,
                CompressedBytes = file.Chunks.Sum(x => x.CompressedSize),
                LocalSize = localSize,
                MD5Checked = md5Checked,
            });
        }
        return new InstallPlan { InstallPath = installPath, Files = plans };
    }


    public static async Task<string> ComputeMD5Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 20, useAsync: true);
        byte[] hash = await MD5.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }


    /// <summary>把计划压成给人看的多行摘要。</summary>
    public static string Describe(InstallPlan plan, int top = 10)
    {
        var lines = new List<string>
        {
            $"安装目录：{plan.InstallPath}",
            $"文件总数：{plan.Files.Count}  未变：{plan.UnchangedCount}  变更：{plan.ChangedCount}  缺失：{plan.MissingCount}",
            $"需下载：{FormatSize(plan.TotalDownloadBytes)}" + (plan.CanIncremental ? "（可增量更新）" : "（更接近全新安装）"),
        };
        var biggest = plan.Files.Where(x => x.State != InstallFileState.Unchanged)
                                .OrderByDescending(x => x.CompressedBytes)
                                .Take(top)
                                .ToList();
        if (biggest.Count > 0)
        {
            lines.Add($"最大的 {biggest.Count} 项：");
            foreach (InstallFilePlan item in biggest)
            {
                string mark = item.State == InstallFileState.Missing ? "缺失" : "变更";
                lines.Add($"  [{mark}] {item.Path}  {FormatSize(item.Size)}  （下载 {FormatSize(item.CompressedBytes)}）");
            }
        }
        return string.Join(Environment.NewLine, lines);
    }


    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit == 0 ? $"{bytes} B" : $"{value:0.##} {units[unit]}";
    }

}
