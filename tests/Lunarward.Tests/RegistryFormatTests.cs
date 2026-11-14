// Lunarward：`wine reg` 输出解析与 .reg 生成的单元测试（L1）。
// 这几个函数是「切游戏卡 4 秒」那条链路的落点，格式解析错了会静默丢值，所以固定成用例。
using Starward.Features.GameAccount;
using System.Collections.Generic;
using Xunit;

namespace Lunarward.Tests;

public class RegistryFormatTests
{
    [Fact]
    public void ParseLine_ReadsDwordAsInt()
    {
        var value = RegistryFormat.ParseLine("    App_LastUserID_h2841727341    REG_DWORD    0x64", out string? name);
        Assert.Equal("App_LastUserID_h2841727341", name);
        Assert.Equal(100, value);
    }

    [Fact]
    public void ParseLine_ReadsBinaryBytes()
    {
        var value = RegistryFormat.ParseLine("    MIHOYOSDK_ADL_PROD_CN_h3123967166    REG_BINARY    01,02,FF", out string? name);
        Assert.Equal("MIHOYOSDK_ADL_PROD_CN_h3123967166", name);
        Assert.Equal(new byte[] { 1, 2, 0xFF }, Assert.IsType<byte[]>(value));
    }

    [Fact]
    public void ParseLine_ReadsString()
    {
        var value = RegistryFormat.ParseLine("    Path    REG_SZ    C:\\Program Files\\Genshin", out string? name);
        Assert.Equal("Path", name);
        Assert.Equal(@"C:\Program Files\Genshin", value);
    }

    [Fact]
    public void ParseLine_RejectsNonValueLines()
    {
        Assert.Null(RegistryFormat.ParseLine("HKEY_CURRENT_USER\\Software\\miHoYo", out string? name));
        Assert.Null(name);
    }

    [Fact]
    public void ParseValues_SkipsHeaderAndDefaultAndKeepsNamed()
    {
        const string output = "\r\nHKEY_CURRENT_USER\\Software\\miHoYo\\A\r\n    (Default)    REG_SZ    x\r\n    Width    REG_DWORD    0x500\r\n    Blob    REG_BINARY    0a,0b\r\n\r\n";
        var values = RegistryFormat.ParseValues(output);
        Assert.Equal(2, values.Count);
        Assert.Equal(0x500, values["Width"]);
        Assert.Equal(new byte[] { 0x0a, 0x0b }, values["Blob"]);
    }

    [Fact]
    public void ParseValues_IsCaseInsensitive()
    {
        var values = RegistryFormat.ParseValues("    Width    REG_DWORD    0x1");
        Assert.True(values.ContainsKey("width"));
    }

    [Fact]
    public void BuildRegFile_WritesDwordHexAndString()
    {
        var text = RegistryFormat.BuildRegFile("HKCU\\Software\\miHoYo\\A", new Dictionary<string, object>
        {
            ["Flag"] = 1,
            ["Blob"] = new byte[] { 0xde, 0xad },
            ["Name"] = "中文\"引号\"",
        });
        Assert.Contains("[HKCU\\Software\\miHoYo\\A]", text);
        Assert.Contains("\"Flag\"=dword:00000001", text);
        Assert.Contains("\"Blob\"=hex:de,ad", text);
        Assert.Contains("\"Name\"=\"中文\\\"引号\\\"\"", text);
        Assert.StartsWith("Windows Registry Editor Version 5.00", text);
    }

    [Theory]
    [InlineData("HKEY_CURRENT_USER\\Software\\miHoYo", "HKCU\\Software\\miHoYo")]
    [InlineData("hkey_current_user\\Software", "HKCU\\Software")]
    [InlineData("HKCU\\Software", "HKCU\\Software")]
    [InlineData("HKEY_LOCAL_MACHINE\\Software\\Wine", "HKLM\\Software\\Wine")]
    public void NormalizeKey_ShortensRoots(string input, string expected)
        => Assert.Equal(expected, RegistryFormat.NormalizeKey(input));

    [Fact]
    public void ToHex_UsesLowercaseTwoDigits()
        => Assert.Equal("00,0f,ff", RegistryFormat.ToHex(new byte[] { 0, 15, 255 }));

    [Fact]
    public void BuildRegFileBytes_StartsWithUtf16LeBom()
    {
        // L-128 回归：没有 BOM 时 wine reg import 返回 0 却什么都不写，这个断言钉死编码约定。
        byte[] bytes = RegistryFormat.BuildRegFileBytes(
            @"HKCU\Software\LunarwardSelftest",
            new Dictionary<string, object> { ["Flag"] = 1, ["Name"] = "自检" });
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        string text = System.Text.Encoding.Unicode.GetString(bytes[2..]);
        Assert.Contains("Windows Registry Editor Version 5.00", text);
        Assert.Contains("LunarwardSelftest", text);
    }
}
