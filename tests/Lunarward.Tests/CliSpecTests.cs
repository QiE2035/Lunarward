using System;
using System.Linq;
using Starward.Helpers;
using Xunit;

namespace Lunarward.Tests;

/// <summary>
/// D79：命令行帮助的唯一来源是 `src/Lunarward/Helpers/CliSpec.cs`。
/// 这里锁住「表 → 帮助文本」和「表 → 未知开关校验」这两条不变量；
/// 「源码里的开关 ⊆ 表」由 `scripts/test/lint-cli-help.sh` 卡（它要 grep 源文件）。
/// </summary>
public class CliSpecTests
{
    [Fact]
    public void RenderHelp_ListsEveryCommandAndModifier()
    {
        string help = CliSpec.RenderHelp("版本 9.9.9");

        foreach (var item in CliSpec.Commands.Concat(CliSpec.Modifiers))
        {
            Assert.Contains(item.Name, help, StringComparison.Ordinal);
        }
        Assert.Contains("版本 9.9.9", help, StringComparison.Ordinal);
        Assert.Contains("用法：lunarward", help, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("环境诊断")]
    [InlineData("运行环境")]
    [InlineData("组件与图形层")]
    [InlineData("安装与更新")]
    [InlineData("窗口 / 协议 / 其它")]
    [InlineData("修饰开关（跟在命令后面）")]
    public void RenderHelp_HasGroupHeading(string group)
    {
        Assert.Contains(group, CliSpec.RenderHelp(), StringComparison.Ordinal);
    }

    [Fact]
    public void Known_IsCompleteAndDeduplicated()
    {
        var declared = CliSpec.Commands.Select(x => x.Name)
            .Concat(CliSpec.Modifiers.Select(x => x.Name))
            .ToList();

        Assert.Equal(declared.Count, declared.Distinct(StringComparer.Ordinal).Count());
        foreach (string name in declared)
        {
            Assert.Contains(name, CliSpec.Known);
        }
        Assert.Contains("-h", CliSpec.Known);
        Assert.Contains("-v", CliSpec.Known);
    }

    [Fact]
    public void EveryDeclaredSwitchIsWellFormed()
    {
        foreach (var item in CliSpec.Commands.Concat(CliSpec.Modifiers))
        {
            // 上游的 CLI 里既有 `--preflight` 这种开关，也有 `playtime` / `startgame` 这种裸子命令
            // （桌面环境的深链与云游戏按钮按这种方式拉起程序），两类都要能登记进表。
            bool wellFormed = item.Name.StartsWith("-", StringComparison.Ordinal)
                || (item.Name.Length > 0 && item.Name.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c)));
            Assert.True(wellFormed, $"命令名既不是开关也不是小写子命令：{item.Name}");
            Assert.False(string.IsNullOrWhiteSpace(item.Description), item.Name);
            Assert.False(string.IsNullOrWhiteSpace(item.Group), item.Name);
        }
    }


    [Theory]
    [InlineData("startgame")]
    [InlineData("playtime")]
    [InlineData("rpc")]
    public void BareSubcommandsAreDeclared(string name)
    {
        Assert.Contains(CliSpec.Commands, x => x.Name == name);
        Assert.Contains(name, CliSpec.Known);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("--preflight")]
    [InlineData("-h")]
    [InlineData("-v")]
    public void TryRejectUnknown_AcceptsKnownFirstToken(string token)
    {
        Assert.False(CliSpec.TryRejectUnknown(new[] { token }, out int code, out string message));
        Assert.Equal(0, code);
        Assert.Equal("", message);
    }

    [Fact]
    public void TryRejectUnknown_IgnoresPositionalArguments()
    {
        // 游戏自己的参数可能也以 - 开头（比如 Game.exe -dx11），第一 token 是路径时不能拦。
        Assert.False(CliSpec.TryRejectUnknown(new[] { "/games/GenshinImpact.exe", "-dx11" }, out _, out _));
        Assert.False(CliSpec.TryRejectUnknown(new[] { "hk4e_cn" }, out _, out _));
        Assert.False(CliSpec.TryRejectUnknown(Array.Empty<string>(), out _, out _));
        Assert.False(CliSpec.TryRejectUnknown(null, out _, out _));
    }

    [Fact]
    public void TryRejectUnknown_ReturnsExitTwoWithSuggestion()
    {
        Assert.True(CliSpec.TryRejectUnknown(new[] { "--componet-list" }, out int code, out string message));
        Assert.Equal(2, code);
        Assert.Contains("未知开关：--componet-list", message, StringComparison.Ordinal);
        Assert.Contains("--component-list", message, StringComparison.Ordinal);
        Assert.Contains("--help", message, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRejectUnknown_WithoutCloseMatchOmitsSuggestion()
    {
        Assert.True(CliSpec.TryRejectUnknown(new[] { "--zzzzzzzzzz" }, out int code, out string message));
        Assert.Equal(2, code);
        Assert.Contains("未知开关", message, StringComparison.Ordinal);
        Assert.DoesNotContain("最接近的是", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--componet-list", "--component-list")]
    [InlineData("--prefix-ensur", "--prefix-ensure")]
    [InlineData("--profile-ensur", "--profile-ensure")]
    public void Suggest_FindsNearMiss(string token, string expected)
    {
        Assert.Equal(expected, CliSpec.Suggest(token));
    }

    [Fact]
    public void Suggest_StripsValueAfterEquals()
    {
        // `--component-list=wine` 这种带值写法要先切掉值再比。
        Assert.Equal("--component-list", CliSpec.Suggest("--component-list=wine"));
    }

    [Fact]
    public void Suggest_IgnoresNonSwitchTokens()
    {
        Assert.Null(CliSpec.Suggest("hk4e_cn"));
        Assert.Null(CliSpec.Suggest(""));
        Assert.Null(CliSpec.Suggest(null));
    }

    [Fact]
    public void Signature_AppendsArgumentsWhenPresent()
    {
        Assert.Equal("--profile-ensure [biz]", new CliSwitch("--profile-ensure", "[biz]", "d", "g").Signature);
        Assert.Equal("--help", new CliSwitch("--help", "", "d", "g").Signature);
    }

    [Fact]
    public void DisplayWidth_CountsWideCharactersTwice()
    {
        Assert.Equal(3, CliSpec.DisplayWidth("--a"));
        Assert.Equal(4, CliSpec.DisplayWidth("命名"));
        Assert.Equal(6, CliSpec.DisplayWidth("--名字"));  // 两个 ASCII 横杠各 1 列，两个汉字各 2 列
        Assert.Equal(0, CliSpec.DisplayWidth(""));
        Assert.Equal(0, CliSpec.DisplayWidth(null));
    }
}
