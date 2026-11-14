using Starward.Features.Runtime;
using System;
using System.IO;
using Xunit;

namespace Lunarward.Tests;

/// <summary>
/// 运行环境阶段 1（组件层）的纯逻辑：分片规划、布局归一化、manifest 往返（D73）。
/// 这些是最容易在真实下载/导入时才炸的边界，先在这里钉住。
/// </summary>
public class ComponentRuntimeTests
{
    [Theory]
    [InlineData(0, 8388608, 0)]
    [InlineData(1, 8388608, 1)]
    [InlineData(8388607, 8388608, 1)]
    [InlineData(8388608, 8388608, 1)]
    [InlineData(8388609, 8388608, 2)]
    public void ChunkCount_RoundsUp(long total, int chunkSize, int expected)
    {
        Assert.Equal(expected, ComponentDownloadMath.ChunkCount(total, chunkSize));
    }

    [Fact]
    public void ChunkCount_RejectsNonPositiveChunkSize()
    {
        Assert.Equal(0, ComponentDownloadMath.ChunkCount(1024, 0));
        Assert.Equal(0, ComponentDownloadMath.ChunkCount(-1, 4096));
    }

    [Fact]
    public void PlanChunk_LastChunkIsTrimmedToRemainder()
    {
        const long total = 20_000_000;
        const int chunk = 8 * 1024 * 1024;
        (long start, long length) = ComponentDownloadMath.PlanChunk(total, 0, chunk);
        Assert.Equal(0, start);
        Assert.Equal(chunk, length);
        (long start2, long length2) = ComponentDownloadMath.PlanChunk(total, 2, chunk);
        Assert.Equal(2L * chunk, start2);
        Assert.Equal(total - 2L * chunk, length2);
    }

    [Fact]
    public void PlanChunk_BeyondEndIsZeroLength()
    {
        (long start, long length) = ComponentDownloadMath.PlanChunk(1024, 5, 4096);
        Assert.Equal(20480, start);
        Assert.Equal(0, length);
    }

    [Fact]
    public void ApplyMirror_KeepsHostPathAfterScheme()
    {
        string url = ComponentDownloadMath.ApplyMirror(new Uri("https://github.com/a/b/releases/download/v1/x.tar.zst"), "https://mirror.example/gh");
        Assert.Equal("https://mirror.example/gh/github.com/a/b/releases/download/v1/x.tar.zst", url);
    }

    [Fact]
    public void ApplyMirror_EmptyPrefixIsIdentity()
    {
        Uri uri = new("https://github.com/a/b/x.tar.zst");
        Assert.Equal(uri.ToString(), ComponentDownloadMath.ApplyMirror(uri, null));
        Assert.Equal(uri.ToString(), ComponentDownloadMath.ApplyMirror(uri, "  "));
    }

    [Fact]
    public void FindRunnerRoot_DetectsBinLayout()
    {
        string root = Path.Combine(Path.GetTempPath(), "lw-layout-bin-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            File.WriteAllText(Path.Combine(root, "bin", "wine"), "#!/bin/sh\n");
            (string layout, string found, string wine) = ComponentLayouts.FindRunnerRoot(root);
            Assert.Equal(ComponentLayouts.Bin, layout);
            Assert.Equal(root, found);
            Assert.Equal(Path.Combine(root, "bin", "wine"), wine);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindRunnerRoot_DetectsProtonFilesBinLayout()
    {
        string root = Path.Combine(Path.GetTempPath(), "lw-layout-proton-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string nested = Path.Combine(root, "ge-proton-9");
            Directory.CreateDirectory(Path.Combine(nested, "files", "bin"));
            File.WriteAllText(Path.Combine(nested, "files", "bin", "wine"), "#!/bin/sh\n");
            (string layout, string found, string wine) = ComponentLayouts.FindRunnerRoot(root);
            Assert.Equal(ComponentLayouts.FilesBin, layout);
            Assert.Equal(nested, found);
            Assert.EndsWith(Path.Combine("files", "bin", "wine"), wine);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindRunnerRoot_DetectsBareExeLayout()
    {
        string root = Path.Combine(Path.GetTempPath(), "lw-layout-bare-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "wine"), "#!/bin/sh\n");
            (string layout, string found, string wine) = ComponentLayouts.FindRunnerRoot(root);
            Assert.Equal(ComponentLayouts.BareExe, layout);
            Assert.Equal(root, found);
            Assert.EndsWith("wine", wine);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void FindRunnerRoot_ReturnsEmptyWhenNoWine()
    {
        string root = Path.Combine(Path.GetTempPath(), "lw-layout-none-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            (_, _, string wine) = ComponentLayouts.FindRunnerRoot(root);
            Assert.Equal("", wine);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Manifest_RoundTripsThroughDisk()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var manifest = new ComponentManifest
            {
                Id = "wine-staging-9.22",
                Kind = "wine",
                Name = "wine-staging",
                Version = "9.22",
                Layout = ComponentLayouts.FilesBin,
                Source = new ComponentManifestSource { Url = "https://example/x.tar.zst", MirrorPrefix = "", DownloadedAt = DateTimeOffset.Now },
                Digest = new ComponentManifestDigest { Value = new string('a', 64) },
            };
            ComponentManifestStore.Write(dir, manifest);
            ComponentManifest? read = ComponentManifestStore.TryRead(dir);
            Assert.NotNull(read);
            Assert.Equal("wine-staging-9.22", read!.Id);
            Assert.Equal(ComponentLayouts.FilesBin, read.Layout);
            Assert.Equal(64, read.Digest!.Value.Length);
            Assert.Equal(1, read.SchemaVersion);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Manifest_TryReadReturnsNullWhenMissing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-manifest-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(ComponentManifestStore.TryRead(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Theory]
    [InlineData("wine-z-9.9.tar.gz", "wine-z")]
    [InlineData("wine-tkg-11.19.tar.gz", "wine-tkg")]
    [InlineData("dxvk-3.1.1.tar.gz", "dxvk")]
    public void SuggestName_KeepsCompoundNameDropsTrailingVersion(string file, string expected)
    {
        // 只去尾部版本号与架构标记；复合名不能被拆（wine-z 不能变成 z）
        Assert.Equal(expected, ComponentNaming.SuggestName(file, "wine"));
    }

    [Theory]
    [InlineData("wine-staging-9.22-x86_64.tar.xz", "wine-staging")]
    [InlineData("GE-Proton9-20.tar.gz", "GE-Proton9-20")]
    [InlineData("vkd3d-proton-2.14.1.tar.zst", "vkd3d-proton")]
    public void SuggestName_HandlesRealAssetNames(string file, string expected)
    {
        Assert.Equal(expected, ComponentNaming.SuggestName(file, "wine"));
    }

    [Fact]
    public void Sanitize_ReplacesWhitespaceAndSlashes()
    {
        Assert.Equal("a-b-c", ComponentNaming.Sanitize("a b/c"));
    }
}
