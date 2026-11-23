using Starward.Helpers;
using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Lunarward.Tests;

public class JsonFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lw-json-" + Guid.NewGuid().ToString("n"));

    public JsonFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void WriteAtomic_Creates_File_Without_Temp()
    {
        string path = Path.Combine(_dir, "a.json");
        JsonFile.WriteAtomic(path, "{\"x\":1}");
        Assert.Equal("{\"x\":1}", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void WriteAtomic_ConcurrentSaves_DoNotShareTempFile()
    {
        // R08：固定 `path.tmp` 时，并发保存会互相踩（后改名者把前一个临时文件搬走/读到半截）。
        string path = Path.Combine(_dir, "concurrent.json");
        string[] payloads = new string[24];
        for (int i = 0; i < payloads.Length; i++)
        {
            payloads[i] = "{\"n\":" + i + "}";
        }

        Parallel.For(0, payloads.Length, i => JsonFile.WriteAtomic(path, payloads[i]));

        string text = File.ReadAllText(path);
        Assert.Contains(text, payloads);
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }


    [Fact]
    public void WriteAtomic_Keeps_Backup_On_Overwrite()
    {
        string path = Path.Combine(_dir, "b.json");
        JsonFile.WriteAtomic(path, "one");
        JsonFile.WriteAtomic(path, "two");
        Assert.Equal("two", File.ReadAllText(path));
        Assert.Equal("one", File.ReadAllText(path + ".bak"));
    }

    [Fact]
    public void WriteAtomic_Creates_Missing_Directory()
    {
        string path = Path.Combine(_dir, "nested/dir/c.json");
        JsonFile.WriteAtomic(path, "x");
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void ReadOrRecover_Returns_Default_For_Missing()
    {
        string path = Path.Combine(_dir, "missing.json");
        Assert.Null(JsonFile.ReadOrRecover<object>(path));
    }

    [Fact]
    public void ReadOrRecover_Moves_Corrupt_File_Aside()
    {
        string path = Path.Combine(_dir, "corrupt.json");
        File.WriteAllText(path, "{not json");
        Assert.Null(JsonFile.ReadOrRecover<object>(path));
        Assert.False(File.Exists(path));
        Assert.NotEmpty(Directory.GetFiles(_dir, "corrupt.json.corrupt-*"));
    }
}
