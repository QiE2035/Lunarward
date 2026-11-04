// Lunarward：断点续传的纯逻辑（不碰 IO，方便单测）。
//
// chunk 是「按块顺序写」的，所以临时文件的长度就等于「已经写完多少块」。
// 这里只负责把长度换算成块数；真实写盘与截断由 ChunkReuseDownloader 完成。
using System;
using System.Collections.Generic;

namespace Starward.Features.GameInstall.Sophon;


public static class ChunkResume
{
    /// <summary>
    /// 已有文件长度能覆盖到第几块（从 0 开始计数）。
    /// 长度落在块中间时，只算到上一个完整块，剩下的半块由调用方截掉重写。
    /// </summary>
    /// <param name="existingLength">临时文件当前长度（字节）</param>
    /// <param name="chunkSizes">各块解压后的长度（按写入顺序）</param>
    public static int CountCompletedChunks(long existingLength, IReadOnlyList<long> chunkSizes)
    {
        ArgumentNullException.ThrowIfNull(chunkSizes);
        if (existingLength <= 0)
        {
            return 0;
        }
        long cumulative = 0;
        int count = 0;
        for (int i = 0; i < chunkSizes.Count; i++)
        {
            long size = chunkSizes[i];
            if (size < 0)
            {
                // 清单坏了就当不能续传，别把长度算飞。
                return 0;
            }
            long next = cumulative + size;
            if (next > existingLength)
            {
                break;
            }
            cumulative = next;
            count = i + 1;
        }
        return count;
    }


    /// <summary>前 <paramref name="count"/> 块解压后的总长度，即续传点应该截到的位置。</summary>
    public static long LengthOfChunks(IReadOnlyList<long> chunkSizes, int count)
    {
        ArgumentNullException.ThrowIfNull(chunkSizes);
        long total = 0;
        int limit = Math.Min(Math.Max(count, 0), chunkSizes.Count);
        for (int i = 0; i < limit; i++)
        {
            long size = chunkSizes[i];
            if (size < 0)
            {
                return total;
            }
            total += size;
        }
        return total;
    }
}
