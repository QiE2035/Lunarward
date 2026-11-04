// Lunarward：chunk 级复用估算。
//
// Sophon 的清单按 chunk 描述文件：换个版本时，一个 400 MB 的 pck 往往只有少量 chunk 变化，
// 官方启动器因此只需要下几十 MB。我们之前的 InstallPipeline 是「整文件重下」，于是把
// 33 GB 的变更全算成下载量。这里先用两个版本的清单算出「有多少 chunk 本地已有」，
// 验证可行后再让下载器真正复用（见 InstallPipeline 的 reuse 分支）。
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Starward.Features.GameInstall.Sophon;


public sealed class ChunkReuseStats
{
    public long NewTotalBytes { get; set; }

    public long ReusableBytes { get; set; }

    public long DownloadBytes { get; set; }

    public int NewChunks { get; set; }

    public int ReusedChunks { get; set; }

    public int FilesConsidered { get; set; }

    public List<(string Path, long NewBytes, long DownloadBytes)> WorstFiles { get; } = new();
}


public static class ChunkReusePlanner
{
    /// <summary>
    /// 用「旧版本清单里出现过的 chunk id → 旧文件里的位置」建索引。
    /// 只要新旧两个版本的某个 chunk 内容相同，它的 id 就相同（Sophon 用分块哈希命名）。
    /// </summary>
    public static Dictionary<string, (string File, long Offset, long Size)> BuildIndex(IEnumerable<SophonChunkFile> oldFiles)
    {
        var index = new Dictionary<string, (string, long, long)>(StringComparer.Ordinal);
        foreach (SophonChunkFile file in oldFiles)
        {
            if (file.IsFolder || file.Chunks is null)
            {
                continue;
            }
            foreach (SophonChunk chunk in file.Chunks)
            {
                index.TryAdd(chunk.Id, (file.File, chunk.Offset, chunk.UncompressedSize));
            }
        }
        return index;
    }


    public static ChunkReuseStats Measure(
        IReadOnlyDictionary<string, (string File, long Offset, long Size)> index,
        IEnumerable<SophonChunkFile> newFiles,
        string installPath)
    {
        var stats = new ChunkReuseStats();
        var worst = new List<(string Path, long NewBytes, long DownloadBytes)>();
        foreach (SophonChunkFile file in newFiles)
        {
            if (file.IsFolder || file.Chunks is null || file.Chunks.Count == 0)
            {
                continue;
            }
            stats.FilesConsidered++;
            long newBytes = 0;
            long download = 0;
            foreach (SophonChunk chunk in file.Chunks)
            {
                newBytes += chunk.UncompressedSize;
                if (index.TryGetValue(chunk.Id, out var old))
                {
                    // 本地旧文件必须真的存在，否则「复用」是纸面上的。
                    if (File.Exists(Path.Combine(installPath, old.File)))
                    {
                        stats.ReusedChunks++;
                        stats.ReusableBytes += chunk.UncompressedSize;
                        continue;
                    }
                }
                stats.NewChunks++;
                download += chunk.CompressedSize;
            }
            stats.NewTotalBytes += newBytes;
            stats.DownloadBytes += download;
            if (download > 0)
            {
                worst.Add((file.File, newBytes, download));
            }
        }
        stats.WorstFiles.AddRange(worst.OrderByDescending(x => x.DownloadBytes).Take(10));
        return stats;
    }
}
