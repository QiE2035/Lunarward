// Lunarward：直接读 wine 注册表文件（D86）的单元测试。
// 这条路是「改画质/启动游戏要冻 4–5 秒」的解法，解析错了会静默丢值（比慢更糟），所以把格式细节都钉成用例。
using Starward.Features.GameAccount;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Lunarward.Tests;

public class WineRegistryFilesTests
{
    /// <summary>临时前缀目录：造一个 user.reg / system.reg 出来，测完删掉。</summary>
    private sealed class TempPrefix : IDisposable
    {
        public string Path { get; }

        public TempPrefix(string fileName, string content)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lw-regtest-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(Path);
            File.WriteAllText(System.IO.Path.Combine(Path, fileName), content, new UTF8Encoding(false));
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch
            {
            }
        }
    }


    private const string SampleUserReg = """
        WINE REGISTRY Version 2
        ;; All keys relative to \\User\\S-1-5-21-0-0-0-1000

        [Software\\miHoYo\\\x539f\x795e] 1794411065
        #time=1dd71f1aa78bfe4
        "__LastUid___h2153286551"=hex:31,39,39,35,36,39,33,36,38,00
        "port_h2088097884"=dword:0000afd5
        "Path"="/home/qie2035/games"
        ""=""
        @="default"

        [Software\\Other]
        "K"="must not leak"
        """;


    [Fact]
    public void Unescape_DecodesWineHexEscape()
    {
        Assert.Equal(@"Software\miHoYo\原神", WineRegistryFiles.Unescape(@"Software\\miHoYo\\\x539f\x795e", escapeControls: false));
    }


    [Fact]
    public void Unescape_NameKeepsLiteralBackslashN()
    {
        // 值名里没有换行语义：\n 只是 n 前面带个反斜杠（这里的输入是两个字符 \ + n）
        Assert.Equal("NOnX", WineRegistryFiles.Unescape(@"NO\nX", escapeControls: false));
    }


    [Fact]
    public void Unescape_StringHonoursControlEscapes()
    {
        Assert.Equal("a\nb\0c\rd\"e", WineRegistryFiles.Unescape("a\\nb\\0c\\rd\\\"e", escapeControls: true));
    }


    [Fact]
    public void TryReadKeyValues_ReadsBinaryDwordAndString()
    {
        using var prefix = new TempPrefix("user.reg", SampleUserReg);
        var values = WineRegistryFiles.TryReadKeyValues(prefix.Path, @"HKCU\Software\miHoYo\原神", out string? reason);

        Assert.Null(reason);
        Assert.NotNull(values);
        Assert.Equal(new byte[] { 0x31, 0x39, 0x39, 0x35, 0x36, 0x39, 0x33, 0x36, 0x38, 0x00 },
            Assert.IsType<byte[]>(values!["__LastUid___h2153286551"]));
        Assert.Equal(0xafd5, Assert.IsType<int>(values["port_h2088097884"]));
        Assert.Equal("/home/qie2035/games", Assert.IsType<string>(values["Path"]));
        // 默认值（@= / ""=）没有名字，与 RegistryFormat.ParseValues 一致地跳过
        Assert.False(values.ContainsKey(""));
        Assert.False(values.ContainsKey("@"));
    }


    [Fact]
    public void TryReadKeyValues_DoesNotLeakValuesFromNextSection()
    {
        using var prefix = new TempPrefix("user.reg", SampleUserReg);
        var values = WineRegistryFiles.TryReadKeyValues(prefix.Path, @"HKCU\Software\miHoYo\原神", out _);

        Assert.NotNull(values);
        Assert.False(values!.ContainsKey("K"));
    }


    [Fact]
    public void TryReadKeyValues_FoldsContinuedBinaryLines()
    {
        const string reg = """
            WINE REGISTRY Version 2

            [Software\\miHoYo\\Test]
            "Long"=hex:7b,22,64,65,\
              76,69,63,65,22,3a,22,22\
              ,7d
            """;
        using var prefix = new TempPrefix("user.reg", reg);
        var values = WineRegistryFiles.TryReadKeyValues(prefix.Path, @"HKCU\Software\miHoYo\Test", out _);

        Assert.NotNull(values);
        var bytes = Assert.IsType<byte[]>(values!["Long"]);
        // {"device":""} —— 折行必须拼全，截断会让账号票据解析失败
        Assert.Equal("{\"device\":\"\"}", Encoding.UTF8.GetString(bytes));
    }


    [Fact]
    public void TryReadKeyValues_ReturnsNullWhenKeyIsMissing()
    {
        using var prefix = new TempPrefix("user.reg", SampleUserReg);
        // 文件里没有这个键 → 返回 null 让调用方回落 wine（wine 内存里可能刚写、还没落盘）
        Assert.Null(WineRegistryFiles.TryReadKeyValues(prefix.Path, @"HKCU\Software\miHoYo\绝区零", out string? reason));
        Assert.Equal("注册表文件里没有这个键", reason);
    }


    [Fact]
    public void TryReadKeyValues_ReturnsNullWhenFileIsMissing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-regtest-empty-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(WineRegistryFiles.TryReadKeyValues(dir, @"HKCU\Software\miHoYo\原神", out string? reason));
            Assert.Equal("注册表文件不存在", reason);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }


    [Fact]
    public void TryReadKeyValues_ReadsSystemRegForLocalMachine()
    {
        const string reg = """
            WINE REGISTRY Version 2

            [Software\\miHoYo\\HKLMOnly]
            "Value"=dword:00000001
            """;
        using var prefix = new TempPrefix("system.reg", reg);
        var values = WineRegistryFiles.TryReadKeyValues(prefix.Path, @"HKLM\Software\miHoYo\HKLMOnly", out _);

        Assert.NotNull(values);
        Assert.Equal(1, Assert.IsType<int>(values!["Value"]));
    }


    [Fact]
    public void TryReadKeyValues_RejectsUnsupportedRoot()
    {
        using var prefix = new TempPrefix("user.reg", SampleUserReg);
        Assert.Null(WineRegistryFiles.TryReadKeyValues(prefix.Path, @"HKEY_WEIRD\Software", out string? reason));
        Assert.Equal("根键不支持文件读取", reason);
    }


    [Fact]
    public void TryReadKeyValues_ReturnsEmptyDictionaryForEmptySection()
    {
        const string reg = """
            WINE REGISTRY Version 2

            [Software\\miHoYo\\Empty]
            #time=1dd71f1aa78bfe4

            [Software\\Other]
            "K"="V"
            """;
        using var prefix = new TempPrefix("user.reg", reg);
        var values = WineRegistryFiles.TryReadKeyValues(prefix.Path, @"HKCU\Software\miHoYo\Empty", out _);

        // 键存在但没有值：这不是「不知道」，是确定的空
        Assert.NotNull(values);
        Assert.Empty(values!);
    }
}
