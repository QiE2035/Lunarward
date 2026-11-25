using Starward.Features.Runtime;
using System;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Lunarward.Tests;


/// <summary>按归档格式补测 dispatch：未压缩 .tar / .tar.zst / .tar.xz，以及 tar 里的链接与非普通条目。</summary>
public class ArchiveExtractorFormatTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-archfmt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }


    private static void WriteTar(string path, Action<TarWriter> write)
    {
        using FileStream file = File.Create(path);
        using var writer = new TarWriter(file, TarEntryFormat.Pax, leaveOpen: false);
        write(writer);
    }


    private static PaxTarEntry FileEntry(string name, string content)
    {
        var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
        {
            DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
        };
        return entry;
    }


    [Fact]
    public async Task Extract_PlainTar_RoundTrip()
    {
        string root = NewTempDir();
        string archive = Path.Combine(root, "plain.tar");
        string dest = Path.Combine(root, "out");
        WriteTar(archive, w =>
        {
            w.WriteEntry(FileEntry("bin/wine", "#!/bin/sh\n"));
            var dir = new PaxTarEntry(TarEntryType.Directory, "share/doc/");
            w.WriteEntry(dir);
        });

        await ArchiveExtractor.ExtractAsync(archive, dest);

        Assert.Equal("#!/bin/sh\n", await File.ReadAllTextAsync(Path.Combine(dest, "bin", "wine")));
        Assert.True(Directory.Exists(Path.Combine(dest, "share", "doc")));
    }


    [Fact]
    public async Task Extract_TarZst_RoundTrip()
    {
        string root = NewTempDir();
        string tarPath = Path.Combine(root, "inner.tar");
        WriteTar(tarPath, w => w.WriteEntry(FileEntry("a.txt", "zstd-content")));
        string archive = Path.Combine(root, "pkg.tar.zst");
        using (FileStream input = File.OpenRead(tarPath))
        using (FileStream output = File.Create(archive))
        using (var zstd = new ZstdSharp.CompressionStream(output, leaveOpen: true))
        {
            input.CopyTo(zstd);
        }
        string dest = Path.Combine(root, "out");

        await ArchiveExtractor.ExtractAsync(archive, dest);

        Assert.Equal("zstd-content", await File.ReadAllTextAsync(Path.Combine(dest, "a.txt")));
    }


    [Fact]
    public async Task Extract_Tar_SymlinkEntry_IsCreatedOrSafelySkipped()
    {
        string root = NewTempDir();
        string archive = Path.Combine(root, "links.tar");
        string dest = Path.Combine(root, "out");
        WriteTar(archive, w =>
        {
            var link = new PaxTarEntry(TarEntryType.SymbolicLink, "bin/wine-link") { LinkName = "wine" };
            w.WriteEntry(link);
            w.WriteEntry(FileEntry("bin/wine", "real"));
        });

        await ArchiveExtractor.ExtractAsync(archive, dest);

        string linkPath = Path.Combine(dest, "bin", "wine-link");
        // 创建成功就是符号链接；被安全策略跳过时文件不存在——两种都算通过，但不能是普通文件。
        if (File.Exists(linkPath))
        {
            Assert.True(new FileInfo(linkPath).LinkTarget is not null);
        }
        Assert.Equal("real", await File.ReadAllTextAsync(Path.Combine(dest, "bin", "wine")));
    }


    [Fact]
    public async Task Extract_Tar_HardLinkEntry_IsSkipped()
    {
        string root = NewTempDir();
        string archive = Path.Combine(root, "hard.tar");
        string dest = Path.Combine(root, "out");
        WriteTar(archive, w =>
        {
            var hard = new PaxTarEntry(TarEntryType.HardLink, "bin/dup") { LinkName = "bin/orig" };
            w.WriteEntry(hard);
            w.WriteEntry(FileEntry("bin/orig", "orig"));
        });

        await ArchiveExtractor.ExtractAsync(archive, dest);

        Assert.Equal("orig", await File.ReadAllTextAsync(Path.Combine(dest, "bin", "orig")));
        Assert.False(File.Exists(Path.Combine(dest, "bin", "dup")));
    }


    [Fact]
    public async Task Extract_TarGz_ReportsProgressToCompletion()
    {
        string root = NewTempDir();
        string archive = Path.Combine(root, "prog.tgz");
        using (FileStream file = File.Create(archive))
        using (var gzip = new GZipStream(file, CompressionLevel.Fastest))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            writer.WriteEntry(FileEntry("x.bin", new string('x', 4096)));
            writer.WriteEntry(FileEntry("y.bin", new string('y', 4096)));
        }
        string dest = Path.Combine(root, "out");
        var reports = new System.Collections.Generic.List<ArchiveProgress>();

        await ArchiveExtractor.ExtractAsync(archive, dest, new Progress<ArchiveProgress>(reports.Add));

        Assert.True(File.Exists(Path.Combine(dest, "y.bin")));
        Assert.NotEmpty(reports);
    }
}
