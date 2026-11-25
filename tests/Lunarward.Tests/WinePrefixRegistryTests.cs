using Starward.GameInstall.Service;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Lunarward.Tests;

/// <summary>
/// WinePrefixRegistry：从 wine 前缀的 user.reg 里读出热更新需要的 selectedServerName（D132）。
/// 夹具都是临时目录里手写的最小 .reg 片段，不依赖真实 wine。
/// </summary>
public class WinePrefixRegistryTests
{
    private static string NewTempDir()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-winereg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string NewPrefix(string userReg, string? keyPath = null)
    {
        string prefix = NewTempDir();
        Directory.CreateDirectory(Path.Combine(prefix, "drive_c"));
        File.WriteAllText(Path.Combine(prefix, "user.reg"), userReg, new UTF8Encoding(false));
        return prefix;
    }

    private const string GenshinKey = @"Software\miHoYo\Genshin Impact";

    private static string NewPrefixRaw(string userReg)
    {
        string prefix = NewTempDir();
        Directory.CreateDirectory(Path.Combine(prefix, "drive_c"));
        File.WriteAllText(Path.Combine(prefix, "user.reg"), userReg, new UTF8Encoding(false));
        return prefix;
    }

    private static string Hex(byte[] bytes) => string.Join(",", bytes.Select(x => x.ToString("x2")));

    private static string RegFile(string keyPath, string valueName, string data, int foldAt = 0)
    {
        var sb = new StringBuilder();
        sb.Append("WINE REGISTRY Version 2\n");
        sb.Append("# all keys relative to \\\\User\\\\S-1-5-21-0-0-0-1000\n\n");
        sb.Append('[').Append(keyPath.Replace("\\", "\\\\")).Append("] 1750000000\n");
        string line = "#time=1d9a0b2c0\n\"" + valueName + "\"=" + data + "\n";
        if (foldAt > 0 && line.Length > foldAt)
        {
            // wine 会把超长行折成「行尾反斜杠 + 缩进续行」
            var folded = new StringBuilder();
            int index = 0;
            while (index < line.Length)
            {
                int take = Math.Min(foldAt, line.Length - index);
                folded.Append(line.AsSpan(index, take)).Append(index + take < line.Length ? "\\\n  " : "");
                index += take;
            }
            sb.Append(folded);
        }
        else
        {
            sb.Append(line);
        }
        sb.Append("\n[Software\\\\miHoYo\\\\Other] 1750000001\n\"x\"=hex:01\n");
        return sb.ToString();
    }

    [Fact]
    public void FindPrefix_WalksUpFromDeepInstallPath()
    {
        string prefix = NewPrefix(RegFile(GenshinKey, "v", "hex:00"));
        string installPath = Path.Combine(prefix, "drive_c", "Games", "Genshin Impact");
        Directory.CreateDirectory(installPath);

        Assert.Equal(prefix, WinePrefixRegistry.FindPrefix(installPath));
    }

    [Fact]
    public void FindPrefix_ReturnsNullWithoutMarkers()
    {
        string dir = NewTempDir();
        Directory.CreateDirectory(Path.Combine(dir, "drive_c"));  // 有 drive_c 但没有 user.reg
        Assert.Null(WinePrefixRegistry.FindPrefix(Path.Combine(dir, "drive_c")));
        Assert.Null(WinePrefixRegistry.FindPrefix(""));
        Assert.Null(WinePrefixRegistry.FindPrefix(null));
    }

    [Fact]
    public void ReadUserRegBinary_ParsesHexValue()
    {
        byte[] payload = Encoding.UTF8.GetBytes("{\"selectedServerName\":\"prod_official_usa\"}");
        string prefix = NewPrefix(RegFile(GenshinKey, "GENERAL_DATA_h2389025596", "hex:" + Hex(payload)));

        byte[]? actual = WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "GENERAL_DATA_h2389025596");

        Assert.NotNull(actual);
        Assert.Equal(payload, actual);
    }

    [Fact]
    public void ReadUserRegBinary_JoinsFoldedLines()
    {
        // 真实 user.reg 里长 REG_BINARY 会折行，续行前有缩进空格
        byte[] payload = Enumerable.Range(0, 200).Select(x => (byte)x).ToArray();
        string prefix = NewPrefix(RegFile(GenshinKey, "GENERAL_DATA_h2389025596", "hex:" + Hex(payload), foldAt: 40));

        byte[]? actual = WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "GENERAL_DATA_h2389025596");

        Assert.Equal(payload, actual);
    }

    [Fact]
    public void ReadUserRegBinary_MatchesEscapedKeyAndValueName()
    {
        // 键名里的非 ASCII 在文件里写成 \xNNNN，值名比较也应忽略大小写
        byte[] payload = Encoding.UTF8.GetBytes("{}");
        string prefix = NewPrefix(RegFile(@"Software\miHoYo\原神", "General_Data", "hex:" + Hex(payload)));

        byte[]? actual = WinePrefixRegistry.ReadUserRegBinary(prefix, @"Software\miHoYo\原神", "general_data");

        Assert.Equal(payload, actual);
    }

    [Fact]
    public void ReadUserRegBinary_ReturnsNullForMissingKeyOrUnsupportedType()
    {
        string prefix = NewPrefix(RegFile(GenshinKey, "s", "hex(2):41,00"));

        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(prefix, @"Software\miHoYo\Nope", "s"));
        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "missing"));
        // hex(2) 是 REG_EXPAND_SZ，不是 REG_BINARY
        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "s"));
        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, ""));
        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(null, GenshinKey, "s"));
        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(Path.Combine(NewTempDir(), "nope"), GenshinKey, "s"));
    }

    [Fact]
    public void FindPrefix_AcceptsFilePathInsidePrefix()
    {
        string prefix = NewPrefix(RegFile(GenshinKey, "v", "hex:00"));
        string file = Path.Combine(prefix, "drive_c", "Games", "launcher.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "");

        // 传文件路径时从父目录开始（DirectoryInfo.Exists 对文件为 false）
        Assert.Equal(prefix, WinePrefixRegistry.FindPrefix(file));
    }

    [Fact]
    public void FindPrefix_GivesUpAfterDepthLimit()
    {
        string root = NewTempDir();
        string deep = root;
        for (int i = 0; i < 20; i++)
        {
            deep = Path.Combine(deep, "d" + i);
        }
        Directory.CreateDirectory(deep);

        Assert.Null(WinePrefixRegistry.FindPrefix(Path.Combine(deep, "not-created-yet", "x")));
    }

    [Fact]
    public void ReadUserRegBinary_StopsAtNextSection()
    {
        // 目标键里没有这个值，值出现在下一个段里 → 不能跨段命中
        string reg = "[" + GenshinKey.Replace("\\", "\\\\") + "] 1\n\"other\"=hex:00\n"
                   + "[Software\\\\miHoYo\\\\Other] 2\n\"target\"=hex:7b,7d\n";
        string prefix = NewPrefixRaw(reg);

        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "target"));
    }

    [Fact]
    public void ReadUserRegBinary_SkipsCommentsTimestampsAndDefaults()
    {
        byte[] payload = { 0x7b, 0x7d };
        string reg = "[" + GenshinKey.Replace("\\", "\\\\") + "] 1\n"
                   + "#time=1d9a0b2c0\n"
                   + "\n"
                   + "   # indented comment\n"
                   + "@=\"default value\"\n"
                   + "0x1d9a0b2c0\n"
                   + "\"target\"=hex:" + Hex(payload) + "\n";
        string prefix = NewPrefixRaw(reg);

        Assert.Equal(payload, WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "target"));
    }

    [Fact]
    public void ReadUserRegBinary_IgnoresMalformedLines()
    {
        string key = GenshinKey.Replace("\\", "\\\\");
        string reg = "[\n[]\n[a\n[" + key + "] 1\n"
                   + "\"unterminated\n"                          // 没有收尾引号 → 跳过
                   + "\"target\"\n"                              // 命中值名但没有 '='
                   ;
        string prefix = NewPrefixRaw(reg);

        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "target"));
    }

    [Fact]
    public void ReadUserRegBinary_RejectsNonBinaryValues()
    {
        string key = GenshinKey.Replace("\\", "\\\\");
        string reg = "[" + key + "] 1\n"
                   + "\"word\"=dword:00000001\n"
                   + "\"bad\"=Hex:zz\n"
                   + "\"empty\"=hex:\n";
        string prefix = NewPrefixRaw(reg);

        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "word"));
        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "bad"));
        Assert.Empty(WinePrefixRegistry.ReadUserRegBinary(prefix, GenshinKey, "empty")!);
        // 前缀/键名/值名空白一律 null
        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(prefix, " ", "empty"));
        Assert.Null(WinePrefixRegistry.ReadUserRegBinary(" ", GenshinKey, "empty"));
    }

    [Fact]
    public void ReadUserRegBinary_DecodesEveryEscape()
    {
        // 段名里用尽 wine 的转义：\\ \" \n \r \t \0 \xNN \xNNNN \XNN，以及未知转义与结尾裸反斜杠
        string key = "A\\B\"C\nD\rE\tF\0G\x41H\x0053I\x12\x34Jq\\";
        string escaped = @"A\\B\""C\nD\rE\tF\0G\x41H\x0053I\x12\x34Jq\\";
        string reg = "[" + escaped + "] 1\n\"target\"=hex:01\n";
        string prefix = NewPrefixRaw(reg);

        Assert.Equal(new byte[] { 0x01 }, WinePrefixRegistry.ReadUserRegBinary(prefix, key, "target"));
    }
}
