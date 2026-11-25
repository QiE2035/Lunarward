using Starward.GameInstall.Service;
using System;
using System.IO;
using Xunit;

namespace Lunarward.Tests;

/// <summary>硬链接兼容层：文件系统判断与建链接（D131，上游只在 NTFS/ReFS 上做，Linux 要能走同一套）。</summary>
public class HardLinkCompatTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-hardlink-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void IsSupported_EmptyOrMissingPath_IsFalse()
    {
        Assert.False(HardLinkCompat.IsSupported(null));
        Assert.False(HardLinkCompat.IsSupported(""));
        Assert.False(HardLinkCompat.IsSupported("   "));
        Assert.False(HardLinkCompat.IsSupported("/definitely/not/a/real/path/" + Guid.NewGuid().ToString("N")));
    }

    [Fact]
    public void IsSupported_TempDirectory_IsTrueOnUnix()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        Assert.True(HardLinkCompat.IsSupported(Path.GetTempPath()));
    }

    [Fact]
    public void IsSupported_DirectoryInsideTemp_IsTrue()
    {
        string dir = NewTempDir();
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            Assert.True(HardLinkCompat.IsSupported(dir));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryCreate_SharesContent()
    {
        string dir = NewTempDir();
        try
        {
            string target = Path.Combine(dir, "target.bin");
            string link = Path.Combine(dir, "link.bin");
            File.WriteAllText(target, "abc");

            if (!HardLinkCompat.TryCreate(link, target))
            {
                return; // 该文件系统不支持硬链接（例如容器里的 overlay 变体），不判失败
            }

            Assert.True(File.Exists(link));
            Assert.Equal("abc", File.ReadAllText(link));

            // 硬链接共享 inode：改目标文件，链接处读到的是同一份数据
            File.WriteAllText(target, "xyz");
            Assert.Equal("xyz", File.ReadAllText(link));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryCreate_ExistingLinkPath_IsFalse()
    {
        string dir = NewTempDir();
        try
        {
            string target = Path.Combine(dir, "target.bin");
            string link = Path.Combine(dir, "link.bin");
            File.WriteAllText(target, "abc");
            File.WriteAllText(link, "occupied");

            Assert.False(HardLinkCompat.TryCreate(link, target));
            Assert.Equal("occupied", File.ReadAllText(link));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryCreate_MissingTarget_IsFalse()
    {
        string dir = NewTempDir();
        try
        {
            Assert.False(HardLinkCompat.TryCreate(Path.Combine(dir, "link.bin"), Path.Combine(dir, "missing.bin")));
            Assert.False(File.Exists(Path.Combine(dir, "link.bin")));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void TryCreate_AcrossFileSystems_IsFalse()
    {
        const string other = "/dev/shm";
        if (!Directory.Exists(other))
        {
            return;
        }
        string dir = NewTempDir();
        string? link = null;
        try
        {
            string target = Path.Combine(dir, "target.bin");
            File.WriteAllText(target, "abc");
            link = Path.Combine(other, "lw-hardlink-" + Guid.NewGuid().ToString("N"));
            bool created = HardLinkCompat.TryCreate(link, target);
            if (created)
            {
                // 同一文件系统（例如本机 /tmp 与 /dev/shm 都是同一个 tmpfs 实例）时跳过
                File.Delete(link);
                link = null;
                return;
            }
            Assert.False(File.Exists(link));
        }
        finally
        {
            if (link is not null && File.Exists(link))
            {
                File.Delete(link);
            }
            Directory.Delete(dir, true);
        }
    }
}
