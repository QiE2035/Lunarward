using Starward.Features.GameInstall.Sophon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Lunarward.Tests;

/// <summary>
/// 分块复用估算的纯逻辑测试（T-RT 之外的安装侧核心）。
/// 覆盖：索引去重、旧文件缺失时不虚报复用、下载量按压缩体积累计、最费流量排序。
/// </summary>
public class ChunkReusePlannerTests
{

    private static SophonChunk Chunk(string id, long compressed, long uncompressed, long offset = 0)
        => new() { Id = id, CompressedSize = compressed, UncompressedSize = uncompressed, Offset = offset };


    private static SophonChunkFile ChunkFile(string path, params SophonChunk[] chunks)
        => new() { File = path, Size = chunks.Sum(c => c.UncompressedSize), Chunks = chunks.ToList() };


    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-crp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }


    [Fact]
    public void BuildIndex_Deduplicates_By_ChunkId()
    {
        var index = ChunkReusePlanner.BuildIndex(new[]
        {
            ChunkFile("a.bin", Chunk("x", 10, 100), Chunk("y", 20, 200)),
            ChunkFile("b.bin", Chunk("x", 10, 100), Chunk("z", 30, 300)),
        });

        Assert.Equal(3, index.Count);
        Assert.Equal("a.bin", index["x"].File); // TryAdd：先出现的胜出
        Assert.Equal(200, index["y"].Size);
    }


    [Fact]
    public void BuildIndex_Skips_Folders_And_NullChunks()
    {
        var folder = new SophonChunkFile { File = "dir", IsFolder = true, Chunks = new List<SophonChunk> { Chunk("a", 1, 1) } };
        var noChunks = new SophonChunkFile { File = "empty.bin", Chunks = null! };

        var index = ChunkReusePlanner.BuildIndex(new[] { folder, noChunks });

        Assert.Empty(index);
    }


    [Fact]
    public void Measure_Counts_Reuse_When_Old_File_Exists()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "old.bin"), new byte[300]);

            var index = ChunkReusePlanner.BuildIndex(new[] { ChunkFile("old.bin", Chunk("c1", 10, 100), Chunk("c2", 10, 200)) });
            var stats = ChunkReusePlanner.Measure(index, new[] { ChunkFile("new.bin", Chunk("c1", 10, 100), Chunk("c2", 10, 200)) }, dir);

            Assert.Equal(1, stats.FilesConsidered);
            Assert.Equal(2, stats.ReusedChunks);
            Assert.Equal(0, stats.NewChunks);
            Assert.Equal(300, stats.ReusableBytes);
            Assert.Equal(300, stats.NewTotalBytes);
            Assert.Equal(0, stats.DownloadBytes);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }


    [Fact]
    public void Measure_Does_Not_Claim_Reuse_When_Old_File_Missing()
    {
        string dir = NewTempDir();
        try
        {
            var index = ChunkReusePlanner.BuildIndex(new[] { ChunkFile("gone.bin", Chunk("c1", 7, 100)) });
            var stats = ChunkReusePlanner.Measure(index, new[] { ChunkFile("new.bin", Chunk("c1", 7, 100)) }, dir);

            Assert.Equal(0, stats.ReusedChunks);
            Assert.Equal(1, stats.NewChunks);
            // 下载量按「压缩体积」算（真实传输字节），不是解压后的尺寸
            Assert.Equal(7, stats.DownloadBytes);
            Assert.Equal(100, stats.NewTotalBytes);
            Assert.Equal(0, stats.ReusableBytes);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }


    [Fact]
    public void Measure_Mixed_Reuse_And_Download()
    {
        string dir = NewTempDir();
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "old.bin"), new byte[100]);

            var index = ChunkReusePlanner.BuildIndex(new[] { ChunkFile("old.bin", Chunk("keep", 5, 100)) });
            var stats = ChunkReusePlanner.Measure(index, new[]
            {
                ChunkFile("new.bin", Chunk("keep", 5, 100), Chunk("fresh", 40, 400)),
            }, dir);

            Assert.Equal(1, stats.ReusedChunks);
            Assert.Equal(1, stats.NewChunks);
            Assert.Equal(100, stats.ReusableBytes);
            Assert.Equal(40, stats.DownloadBytes);
            Assert.Equal(500, stats.NewTotalBytes);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }


    [Fact]
    public void Measure_Sorts_WorstFiles_By_Download_Volume()
    {
        string dir = NewTempDir();
        try
        {
            var stats = ChunkReusePlanner.Measure(
                new Dictionary<string, (string File, long Offset, long Size)>(),
                new[]
                {
                    ChunkFile("small.bin", Chunk("s", 10, 100)),
                    ChunkFile("big.bin", Chunk("b", 900, 9000)),
                    ChunkFile("mid.bin", Chunk("m", 100, 1000)),
                },
                dir);

            Assert.Equal(new[] { "big.bin", "mid.bin", "small.bin" }, stats.WorstFiles.Select(x => x.Path));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }


    [Fact]
    public void Measure_Ignores_Files_Without_Chunks()
    {
        string dir = NewTempDir();
        try
        {
            var folder = new SophonChunkFile { File = "dir", IsFolder = true, Chunks = new List<SophonChunk>() };
            var noChunks = new SophonChunkFile { File = "x.bin", Chunks = new List<SophonChunk>() };

            var stats = ChunkReusePlanner.Measure(new Dictionary<string, (string, long, long)>(), new[] { folder, noChunks }, dir);

            Assert.Equal(0, stats.FilesConsidered);
            Assert.Equal(0, stats.NewTotalBytes);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

}
