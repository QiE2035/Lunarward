// Lunarward：运行器显示名（D87）——「同一个文件两个名字」与「下拉里两行一模一样」的回归。
using Starward.Features.Runtime;
using Xunit;

namespace Lunarward.Tests;

public class ComponentNamingTests
{
    // 系统目录里的 wine 才叫「系统 Wine」；自己装到 /opt 或 ~/.local 的用目录名，
    // 否则详情页按配置显示 `wine`、下拉显示「系统 Wine」，看起来像两个东西。
    [Theory]
    [InlineData("/usr/bin/wine", "wine", "系统 Wine")]
    [InlineData("/usr/bin/wine64", "wine64", "系统 Wine (64)")]
    [InlineData("/bin/wine", "wine", "系统 Wine")]
    [InlineData("/sbin/wine", "wine", "系统 Wine")]
    [InlineData("/opt/spritz-wine-tkg/bin/wine", "wine", "spritz-wine-tkg")]
    [InlineData("/opt/spritz-wine-tkg/bin/wine64", "wine64", "spritz-wine-tkg")]
    [InlineData("/home/qie2035/.local/share/sleepy-launcher/runners/x/bin/wine", "wine", "x")]
    public void WineDisplayName_UsesDirectoryNameOutsideSystemRoots(string path, string exe, string expected)
    {
        Assert.Equal(expected, RunnerNaming.WineDisplayName(path, exe));
    }


    [Fact]
    public void WineDisplayName_FallsBackWhenThereIsNoParentDirectory()
    {
        Assert.Equal("系统 Wine", RunnerNaming.WineDisplayName("/wine", "wine"));
    }


    // 重名时才有来源片段：两个启动器各自带一份同名运行器，目录名完全一样。
    [Theory]
    [InlineData("/home/qie2035/.local/share/sleepy-launcher/runners/spritz-wine-tkg-staging-wow64-11.19-1/bin/wine", "sleepy-launcher")]
    [InlineData("/home/qie2035/.local/share/anime-game-launcher/runners/spritz-wine-tkg-staging-wow64-11.19-1/bin/wine", "anime-game-launcher")]
    [InlineData("/opt/spritz-wine-tkg/bin/wine", "spritz-wine-tkg")]
    [InlineData("/usr/bin/wine", "")]
    public void OriginOf_NamesTheOwningFolder(string path, string expected)
    {
        Assert.Equal(expected, RunnerNaming.Origin(path));
    }
}
