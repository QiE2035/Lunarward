using Starward.Features.GameInstall.Sophon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Lunarward.Tests;

public class InstallPlannerTests
{

    private static SophonChunkFile File(string path, long size, string md5 = "", long compressed = 0)
        => new()
        {
            File = path,
            Size = size,
            MD5 = md5,
            Chunks = compressed > 0
                     ? new List<SophonChunk> { new() { Id = Guid.NewGuid().ToString("N"), CompressedSize = compressed, UncompressedSize = size } }
                     : new List<SophonChunk>(),
        };


    [Fact]
    public async Task Plan_Classifies_Unchanged_Changed_Missing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            await System.IO.File.WriteAllTextAsync(Path.Combine(dir, "same.bin"), new string('a', 100));
            await System.IO.File.WriteAllTextAsync(Path.Combine(dir, "diff.bin"), new string('b', 50));

            var files = new List<SophonChunkFile>
            {
                File("same.bin", 100, compressed: 10),
                File("diff.bin", 100, compressed: 20),
                File("missing.bin", 100, compressed: 30),
            };

            InstallPlan plan = await InstallPlanner.CreateAsync(files, dir);

            Assert.Equal(1, plan.UnchangedCount);
            Assert.Equal(1, plan.ChangedCount);
            Assert.Equal(1, plan.MissingCount);
            // 只统计非 Unchanged 的压缩体积
            Assert.Equal(50, plan.TotalDownloadBytes);
            // 缺失 1 / 总计 3 → 1*2 < 3 成立，按现有判据算「可增量」
            Assert.True(plan.CanIncremental);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }


    [Fact]
    public async Task Plan_Detects_SameSize_DifferentContent_When_MD5_Checked()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Combine(dir, "same-size.bin");
            string content = new string('x', 100);
            await System.IO.File.WriteAllTextAsync(path, content);
            string actualMd5 = await InstallPlanner.ComputeMD5Async(path);

            var wrong = new List<SophonChunkFile> { File("same-size.bin", 100, md5: new string('0', 32)) };
            InstallPlan planWrong = await InstallPlanner.CreateAsync(wrong, dir, checkMD5: true);
            Assert.Equal(InstallFileState.Changed, planWrong.Files[0].State);

            var right = new List<SophonChunkFile> { File("same-size.bin", 100, md5: actualMd5) };
            InstallPlan planRight = await InstallPlanner.CreateAsync(right, dir, checkMD5: true);
            Assert.Equal(InstallFileState.Unchanged, planRight.Files[0].State);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }


    [Theory]
    [InlineData(0, "0 B")]
    [InlineData(1023, "1023 B")]
    [InlineData(1024, "1 KB")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1024L * 1024 * 5, "5 MB")]
    public void FormatSize_Is_Human_Readable(long bytes, string expected)
    {
        Assert.Equal(expected, InstallPlanner.FormatSize(bytes));
    }


    [Fact]
    public void Describe_Mentions_Counts()
    {
        var plan = new InstallPlan
        {
            InstallPath = "/tmp/lw",
            Files = new List<InstallFilePlan>
            {
                new() { Path = "a", State = InstallFileState.Unchanged, Size = 1, CompressedBytes = 0, LocalSize = 1 },
                new() { Path = "b", State = InstallFileState.Changed, Size = 1, CompressedBytes = 5, LocalSize = 1 },
            },
        };
        string text = InstallPlanner.Describe(plan);
        Assert.Contains("1", text);
    }

}
