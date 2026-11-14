// Lunarward：分片下载的纯算术（D73 §2.3）。
//
// 单独成文件是为了能被单元测试直接链接：下载编排本身要网络，这些边界值不用。
using System;

namespace Starward.Features.Runtime;


public static class ComponentDownloadMath
{
    /// <summary>分片数：不足一片也算一片。</summary>
    public static int ChunkCount(long total, int chunkSize)
    {
        if (total <= 0 || chunkSize <= 0)
        {
            return 0;
        }
        return (int)((total + chunkSize - 1) / chunkSize);
    }


    /// <summary>第 <paramref name="index"/> 片的 (起点, 长度)；越过末尾时长度 0。</summary>
    public static (long Start, long Length) PlanChunk(long total, int index, int chunkSize)
    {
        long start = (long)index * chunkSize;
        long length = Math.Min(chunkSize, Math.Max(0, total - start));
        return (start, length);
    }


    /// <summary>分片中间文件名：<c>.download/&lt;文件名&gt;.part.&lt;n&gt;</c>。</summary>
    public static string PartPath(string workDir, string fileName, int index)
        => System.IO.Path.Combine(workDir, $"{fileName}.part.{index}");


    /// <summary>
    /// 镜像前缀拼接：<c>https://mirror/x</c> + <c>https://host/a/b</c> → <c>https://mirror/x/host/a/b</c>。
    /// 失败时用户看到的是真实 URL（不猜、不吞）。
    /// </summary>
    public static string ApplyMirror(Uri url, string? mirrorPrefix)
    {
        if (string.IsNullOrWhiteSpace(mirrorPrefix))
        {
            return url.ToString();
        }
        string prefix = mirrorPrefix.EndsWith('/') ? mirrorPrefix : mirrorPrefix + "/";
        string tail = url.ToString();
        int scheme = tail.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
        {
            tail = tail[(scheme + 3)..];
        }
        return prefix + tail;
    }
}
