using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Starward.Features.Runtime;
using Xunit;

namespace Lunarward.Tests;

/// <summary>
/// D78：预检「游戏架构」。判断逻辑全部单测；PE 头解析本身交给 PeNet，只验证「读不出来时不要炸」。
/// </summary>
public class PeArchitectureTests
{
    [Theory]
    [InlineData(PeArch.X64, "win32", true)]
    [InlineData(PeArch.X64, "win64", false)]
    [InlineData(PeArch.X64, null, false)]
    [InlineData(PeArch.X86, "win32", false)]
    [InlineData(PeArch.Arm64, "win64", true)]
    [InlineData(PeArch.Unknown, "win32", false)]
    public void Conflict_OnlyFailsOnRealMismatches(PeArch exe, string? prefixArch, bool expected)
    {
        Assert.Equal(expected, PeArchitecture.Conflict(exe, prefixArch) is not null);
    }


    [Fact]
    public void Conflict_ExplainsBadExeFormatFor64BitInWin32Prefix()
    {
        (string Problem, string Fix)? conflict = PeArchitecture.Conflict(PeArch.X64, "win32");

        Assert.NotNull(conflict);
        Assert.Contains("BAD EXE FORMAT", conflict!.Value.Problem);
        Assert.Contains("win64", conflict.Value.Fix);
    }


    [Theory]
    [InlineData(PeArch.X86, "win64", true)]
    [InlineData(PeArch.X86, "win32", false)]
    [InlineData(PeArch.X64, "win64", false)]
    public void SoftNotice_OnlyWarnsFor32BitInsideWin64Prefix(PeArch exe, string prefixArch, bool expected)
    {
        Assert.Equal(expected, PeArchitecture.SoftNotice(exe, prefixArch) is not null);
    }


    [Theory]
    [InlineData(null, "win64")]
    [InlineData("", "win64")]
    [InlineData("win64", "win64")]
    [InlineData("WIN64", "win64")]
    [InlineData("win32", "win32")]
    [InlineData("wow64", "win64")]
    [InlineData("随便写的", "win64")]
    public void NormalizePrefixArch_FollowsProfileRules(string? input, string expected)
    {
        Assert.Equal(expected, PeArchitecture.NormalizePrefixArch(input));
    }


    [Fact]
    public void Describe_MissingFileIsUnknown()
    {
        Assert.Equal(PeArch.Unknown, PeArchitecture.Describe(null));
        Assert.Equal(PeArch.Unknown, PeArchitecture.Describe(""));
        Assert.Equal(PeArch.Unknown, PeArchitecture.Describe(Path.Combine(Path.GetTempPath(), "lunarward-missing-" + Guid.NewGuid().ToString("n"))));
    }


    [Fact]
    public void Describe_TextFileIsUnknownInsteadOfThrowing()
    {
        string path = Path.Combine(Path.GetTempPath(), "lunarward-not-pe-" + Guid.NewGuid().ToString("n")[..8] + ".exe");
        try
        {
            File.WriteAllText(path, "这不是 PE 文件");
            Assert.Equal(PeArch.Unknown, PeArchitecture.Describe(path));
        }
        finally
        {
            File.Delete(path);
        }
    }


    /// <summary>自己拼一个最小 PE32+（x64）文件，验证 PeNet 那条路真的读得到 machine。</summary>
    [Fact]
    public void Describe_MinimalPe64_IsX64()
    {
        string path = Path.Combine(Path.GetTempPath(), "lunarward-min-pe-" + Guid.NewGuid().ToString("n")[..8] + ".exe");
        try
        {
            File.WriteAllBytes(path, BuildMinimalPe(machine: 0x8664, pe32Plus: true));
            Assert.Equal(PeArch.X64, PeArchitecture.Describe(path));
        }
        finally
        {
            File.Delete(path);
        }
    }


    [Fact]
    public void Describe_MinimalPe32_IsX86()
    {
        string path = Path.Combine(Path.GetTempPath(), "lunarward-min-pe-" + Guid.NewGuid().ToString("n")[..8] + ".exe");
        try
        {
            File.WriteAllBytes(path, BuildMinimalPe(machine: 0x014c, pe32Plus: false));
            Assert.Equal(PeArch.X86, PeArchitecture.Describe(path));
        }
        finally
        {
            File.Delete(path);
        }
    }


    /// <summary>
    /// 最小可解析 PE：DOS 头（e_lfanew=0x40）+ PE 签名 + COFF 头 + 可选头 + 1 个 .text 节。
    /// 只包含 PeNet 读 machine / magic 需要的字段，其余填 0。
    /// </summary>
    private static byte[] BuildMinimalPe(ushort machine, bool pe32Plus)
    {
        int optionalSize = pe32Plus ? 0xF0 : 0xE0;
        int peOffset = 0x40;
        int coffOffset = peOffset + 4;
        int optionalOffset = coffOffset + 20;
        int sectionOffset = optionalOffset + optionalSize;
        int rawDataOffset = 0x400;
        var buffer = new byte[rawDataOffset + 0x200];

        // DOS 头
        buffer[0] = (byte) 'M';
        buffer[1] = (byte) 'Z';
        BitConverter.GetBytes(peOffset).CopyTo(buffer, 0x3C);

        // PE 签名
        buffer[peOffset] = (byte) 'P';
        buffer[peOffset + 1] = (byte) 'E';
        buffer[peOffset + 2] = 0;
        buffer[peOffset + 3] = 0;

        // COFF 头
        BitConverter.GetBytes(machine).CopyTo(buffer, coffOffset);
        BitConverter.GetBytes((ushort) 1).CopyTo(buffer, coffOffset + 2);          // NumberOfSections
        BitConverter.GetBytes((ushort) optionalSize).CopyTo(buffer, coffOffset + 16); // SizeOfOptionalHeader
        BitConverter.GetBytes((ushort) 0x0022).CopyTo(buffer, coffOffset + 18);   // EXECUTABLE_IMAGE | LARGE_ADDRESS_AWARE

        // 可选头
        BitConverter.GetBytes(pe32Plus ? (ushort) 0x20B : (ushort) 0x10B).CopyTo(buffer, optionalOffset);
        buffer[optionalOffset + 2] = 14;                                          // MajorLinkerVersion
        if (pe32Plus)
        {
            BitConverter.GetBytes(0x1000L).CopyTo(buffer, optionalOffset + 24);    // ImageBase
            BitConverter.GetBytes(0x1000).CopyTo(buffer, optionalOffset + 32);     // SectionAlignment
            BitConverter.GetBytes(0x200).CopyTo(buffer, optionalOffset + 36);      // FileAlignment
            BitConverter.GetBytes(0x0004).CopyTo(buffer, optionalOffset + 68);     // Subsystem = WINDOWS_CUI
        }
        else
        {
            BitConverter.GetBytes(0x1000).CopyTo(buffer, optionalOffset + 28);     // ImageBase
            BitConverter.GetBytes(0x1000).CopyTo(buffer, optionalOffset + 32);     // SectionAlignment
            BitConverter.GetBytes(0x200).CopyTo(buffer, optionalOffset + 36);      // FileAlignment
            BitConverter.GetBytes(0x0004).CopyTo(buffer, optionalOffset + 68);     // Subsystem
        }

        // .text 节
        Encoding.ASCII.GetBytes(".text").CopyTo(buffer, sectionOffset);
        BitConverter.GetBytes(0x200).CopyTo(buffer, sectionOffset + 8);            // VirtualSize
        BitConverter.GetBytes(0x1000).CopyTo(buffer, sectionOffset + 12);          // VirtualAddress
        BitConverter.GetBytes(0x200).CopyTo(buffer, sectionOffset + 16);           // SizeOfRawData
        BitConverter.GetBytes(rawDataOffset).CopyTo(buffer, sectionOffset + 20);   // PointerToRawData
        BitConverter.GetBytes(0x60000020).CopyTo(buffer, sectionOffset + 36);      // CODE | EXECUTE | READ

        return buffer;
    }
}


public class PrefixArchDetectionTests
{

    [Fact]
    public void TryDetectPrefixArch_ReadsWineArchFromSystemReg()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-arch-" + Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "system.reg"),
                "WINE REGISTRY Version 2\n;; All keys relative to REGISTRY\\\\Machine\n\n#arch=win32\n");
            Assert.Equal("win32", PeArchitecture.TryDetectPrefixArch(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }


    [Fact]
    public void TryDetectPrefixArch_ReturnsNullWhenMissing()
    {
        Assert.Null(PeArchitecture.TryDetectPrefixArch(Path.Combine(Path.GetTempPath(), "lw-nope-" + Guid.NewGuid().ToString("n"))));
        Assert.Null(PeArchitecture.TryDetectPrefixArch(null));
    }
}
