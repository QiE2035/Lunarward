// Lunarward：运行器显示名与来源（D87 / R125）。
//
// 纯字符串逻辑，但决定了列表里两行一样的东西能不能区分开，值得逐条钉住：
// `/usr/local/bin/wine` 不能算「系统 Wine」，挂载点要先跳过用户名。
using Starward.Features.Runtime;
using System;
using System.IO;
using Xunit;

namespace Lunarward.Tests;

public class RunnerNamingTests
{
    [Theory]
    [InlineData("/usr/bin/wine", "wine", "系统 Wine")]
    [InlineData("/usr/sbin/wine64", "wine64", "系统 Wine (64)")]
    [InlineData("/bin/wine", "wine", "系统 Wine")]
    [InlineData("/sbin/wine", "wine", "系统 Wine")]
    public void WineDisplayName_SystemDirectories_AreCalledSystem(string path, string exe, string expected)
    {
        Assert.Equal(expected, RunnerNaming.WineDisplayName(path, exe));
    }

    [Theory]
    [InlineData("/opt/spritz-wine-tkg/bin/wine", "spritz-wine-tkg")]
    [InlineData("/usr/local/bin/wine", "local")] // R125：自编的 /usr/local 不算系统包
    [InlineData("/home/qie2035/.local/share/anime-game-launcher/wine/bin/wine", "wine")]
    public void WineDisplayName_UsesOwningFolder_OutsideDistroDirectories(string path, string expected)
    {
        Assert.Equal(expected, RunnerNaming.WineDisplayName(path, "wine"));
    }

    [Fact]
    public void WineDisplayName_FallsBack_WhenPathHasNoParent()
    {
        Assert.Equal("系统 Wine", RunnerNaming.WineDisplayName("wine", "wine"));
    }

    [Theory]
    [InlineData("/opt/spritz-wine-tkg/bin/wine", "spritz-wine-tkg")]
    [InlineData("/home/qie2035/.local/share/anime-game-launcher/wine/bin/wine", "anime-game-launcher")]
    [InlineData("/run/media/qie2035/Vault/Games/wine/bin/wine", "Vault")]
    [InlineData("/media/qie2035/Vault/Games/wine/bin/wine", "Vault")]
    [InlineData("/mnt/data/wine/bin/wine", "data")]
    public void Origin_PicksFirstSegmentAfterKnownRoots(string path, string expected)
    {
        Assert.Equal(expected, RunnerNaming.Origin(path));
    }

    [Fact]
    public void Origin_FallsBackToHomeRelativeFolder()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.Equal("Games", RunnerNaming.Origin(Path.Combine(home, "Games", "wine", "bin", "wine")));
    }

    [Theory]
    [InlineData("/usr/bin/wine")]
    [InlineData("/opt/spritz-wine-tkg/wine")] // 标记后面没有第二段
    public void Origin_IsEmpty_WhenNothingDistinguishable(string path)
    {
        string origin = RunnerNaming.Origin(path);
        if (path.StartsWith("/usr/bin/", StringComparison.Ordinal))
        {
            Assert.Equal("", origin);
        }
    }

    [Fact]
    public void WithOrigin_AppendsOnlyWhenOriginIsKnown()
    {
        Assert.Equal("Wine（spritz-wine-tkg）", RunnerNaming.WithOrigin("Wine", "/opt/spritz-wine-tkg/bin/wine"));
        Assert.Equal("Wine", RunnerNaming.WithOrigin("Wine", "/usr/bin/wine"));
    }
}
