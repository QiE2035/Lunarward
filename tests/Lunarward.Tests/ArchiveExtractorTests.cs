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


public class ArchiveExtractorTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-arch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }


    /// <summary>造一个 .tar.gz：两层的目录 + 两个文件。</summary>
    private static void WriteTarGz(string path, params (string Name, string Content)[] entries)
    {
        using FileStream file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Fastest);
        using var writer = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false);
        foreach ((string name, string content) in entries)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            var entry = new PaxTarEntry(TarEntryType.RegularFile, name)
            {
                DataStream = new MemoryStream(bytes),
            };
            writer.WriteEntry(entry);
        }
    }


    [Fact]
    public async Task Extract_TarGz_RoundTrip()
    {
        string root = NewTempDir();
        try
        {
            string archive = Path.Combine(root, "wine-fake-1.2.3.tar.gz");
            WriteTarGz(archive,
                ("fake/bin/wine", "#!/bin/sh\necho wine\n"),
                ("fake/share/readme.txt", "hello"));
            string dest = Path.Combine(root, "out");

            await ArchiveExtractor.ExtractAsync(archive, dest);

            Assert.True(File.Exists(Path.Combine(dest, "fake/bin/wine")));
            Assert.Equal("hello", File.ReadAllText(Path.Combine(dest, "fake/share/readme.txt")));
            Assert.Equal("#!/bin/sh\necho wine\n", File.ReadAllText(Path.Combine(dest, "fake/bin/wine")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public async Task Extract_Zip_RoundTrip()
    {
        string root = NewTempDir();
        try
        {
            string archive = Path.Combine(root, "dxvk-3.1.1.zip");
            using (FileStream file = File.Create(archive))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                using StreamWriter w = new(zip.CreateEntry("dxvk/x64/d3d11.dll").Open());
                w.Write("dll");
            }
            string dest = Path.Combine(root, "out");

            await ArchiveExtractor.ExtractAsync(archive, dest);

            Assert.Equal("dll", File.ReadAllText(Path.Combine(dest, "dxvk/x64/d3d11.dll")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public async Task Extract_RejectsPathTraversalEntry()
    {
        string root = NewTempDir();
        try
        {
            string archive = Path.Combine(root, "evil.tar.gz");
            WriteTarGz(archive, ("../evil.txt", "pwn"));
            string dest = Path.Combine(root, "out");

            await Assert.ThrowsAsync<InvalidDataException>(
                () => ArchiveExtractor.ExtractAsync(archive, dest));
            Assert.False(File.Exists(Path.Combine(root, "evil.txt")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public async Task Extract_Cancelled_Throws()
    {
        string root = NewTempDir();
        try
        {
            string archive = Path.Combine(root, "big.tar.gz");
            WriteTarGz(archive, ("a.txt", "x"), ("b.txt", "y"));
            string dest = Path.Combine(root, "out");
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => ArchiveExtractor.ExtractAsync(archive, dest, null, null, cts.Token));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Fact]
    public async Task Extract_UnsupportedFormat_Throws()
    {
        string root = NewTempDir();
        try
        {
            string archive = Path.Combine(root, "sample.rar");
            await File.WriteAllTextAsync(archive, "not an archive");
            await Assert.ThrowsAsync<NotSupportedException>(
                () => ArchiveExtractor.ExtractAsync(archive, Path.Combine(root, "out")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    [Theory]
    [InlineData("wine-staging-9.22.tar.gz", true)]
    [InlineData("dxvk-3.1.1.tar.xz", true)]
    [InlineData("dxvk-3.1.1.tar.zst", true)]
    [InlineData("vkd3d-proton-2.13.zip", true)]
    [InlineData("wine.tar", true)]
    [InlineData("wine.tgz", true)]
    [InlineData("wine.7z", false)]
    [InlineData("wine.rar", false)]
    public void IsSupported_Matrix(string fileName, bool expected)
    {
        Assert.Equal(expected, ArchiveExtractor.IsSupported(fileName));
    }


    [Theory]
    [InlineData("a/b/c.txt", true)]
    [InlineData("plain.txt", true)]
    [InlineData("../escape.txt", false)]
    [InlineData("/etc/passwd", true)]
    [InlineData("a/../../escape.txt", false)]
    public void ResolveTarget_GuardsAgainstEscape(string name, bool allowed)
    {
        string root = Path.Combine(Path.GetTempPath(), "lw-arch-root");
        if (allowed)
        {
            string target = ArchiveExtractor.ResolveTarget(root, name);
            Assert.StartsWith(Path.GetFullPath(root), target);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => ArchiveExtractor.ResolveTarget(root, name));
        }
    }


    [Fact]
    public async Task Extract_Progress_ReportsEntries()
    {
        string root = NewTempDir();
        try
        {
            string archive = Path.Combine(root, "p.tar.gz");
            WriteTarGz(archive, ("a.txt", "1"), ("b.txt", "2"));
            var progress = new Progress<ArchiveProgress>();
            int last = 0;
            var reporter = new InlineProgress(p => last = p.Entries);

            await ArchiveExtractor.ExtractAsync(archive, Path.Combine(root, "out"), reporter);

            Assert.Equal(2, last);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }


    private sealed class InlineProgress : IProgress<ArchiveProgress>
    {
        private readonly Action<ArchiveProgress> _action;
        public InlineProgress(Action<ArchiveProgress> action) => _action = action;
        public void Report(ArchiveProgress value) => _action(value);
    }
}
