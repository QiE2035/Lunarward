// Lunarward：配方展开（argv / env 三层 / 覆盖检测）的纯逻辑（D73 §4.3 §4.4）。
using Starward.Features.Runtime;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Lunarward.Tests;


public class LaunchRecipeTests
{


    [Fact]
    public void SplitArgs_HandlesQuotesAndSpaces()
    {
        Assert.Equal(new[] { "-windowed", "-nologo" }, LaunchRecipeBuilder.SplitArgs("  -windowed   -nologo "));
        Assert.Equal(new[] { "a b", "c" }, LaunchRecipeBuilder.SplitArgs("\"a b\" c"));
        Assert.Equal(new[] { "it's" }, LaunchRecipeBuilder.SplitArgs("'it'\\'s"));
        Assert.Empty(LaunchRecipeBuilder.SplitArgs("   "));
        Assert.Empty(LaunchRecipeBuilder.SplitArgs(null));
    }


    [Fact]
    public void BuildArgv_PutsWrappersBeforeRunner()
    {
        var argv = LaunchRecipeBuilder.BuildArgv(
            new[] { "gamemoderun", "mangohud --dlsym" },
            "/usr/bin/wine",
            "/games/YuanShen.exe",
            "-windowed");

        Assert.Equal(new[] { "gamemoderun", "mangohud", "--dlsym", "/usr/bin/wine", "/games/YuanShen.exe", "-windowed" }, argv);
    }


    [Fact]
    public void BuildArgv_WithoutWrappers_StartsWithRunner()
    {
        var argv = LaunchRecipeBuilder.BuildArgv(null, "/w", "game.exe", "");
        Assert.Equal(new[] { "/w", "game.exe" }, argv);
    }


    [Fact]
    public void MergeEnvironment_RecipeOverridesAutoAndRecordsConflict()
    {
        var auto = new Dictionary<string, string> { ["WINEPREFIX"] = "/old", ["WINEDEBUG"] = "-all" };
        var recipe = new Dictionary<string, string> { ["WINEPREFIX"] = "/new", ["WINEDLLOVERRIDES"] = "mscoree,mshtml=" };

        var merged = LaunchRecipeBuilder.MergeEnvironment(auto, recipe, out List<EnvOverride> overrides);

        Assert.Equal("/new", merged["WINEPREFIX"]);
        Assert.Equal("-all", merged["WINEDEBUG"]);
        Assert.Equal("mscoree,mshtml=", merged["WINEDLLOVERRIDES"]);
        EnvOverride conflict = Assert.Single(overrides);
        Assert.Equal("WINEPREFIX", conflict.Key);
        Assert.Equal("/old", conflict.AutoValue);
        Assert.Equal("/new", conflict.RecipeValue);
    }


    [Fact]
    public void MergeEnvironment_SameValueIsNotAConflict()
    {
        var auto = new Dictionary<string, string> { ["WINEDEBUG"] = "-all" };
        var recipe = new Dictionary<string, string> { ["WINEDEBUG"] = "-all" };

        LaunchRecipeBuilder.MergeEnvironment(auto, recipe, out List<EnvOverride> overrides);

        Assert.Empty(overrides);
    }


    [Fact]
    public void Build_AppendsProfileArgsAndKeepsGameArgs()
    {
        var profile = new RuntimeProfile
        {
            Name = "genshin",
            Args = "-windowed",
            Wrappers = new List<string> { "gamemoderun" },
            Env = new Dictionary<string, string> { ["DXVK_HUD"] = "fps" },
        };
        var auto = new Dictionary<string, string> { ["WINEPREFIX"] = "/auto", ["WINEDEBUG"] = "-all" };

        LaunchRecipe recipe = LaunchRecipeBuilder.Build(profile, "/w", "/prefix", "/games/g.exe", "-ignored", auto);

        Assert.Equal("/prefix", recipe.Environment["WINEPREFIX"]);
        Assert.Equal("fps", recipe.Environment["DXVK_HUD"]);
        // R02：游戏自带参数在前、配方参数追加在后，两者都不能丢。
        Assert.Equal(new[] { "gamemoderun", "/w", "/games/g.exe", "-ignored", "-windowed" }, recipe.Argv);
        Assert.Empty(recipe.Overrides);
    }


    [Fact]
    public void Build_RecipeTryingToChangePrefix_KeepsRealPrefixAndRecordsConflict()
    {
        var profile = new RuntimeProfile
        {
            Name = "x",
            Env = new Dictionary<string, string> { ["WINEPREFIX"] = "/elsewhere" },
        };

        LaunchRecipe recipe = LaunchRecipeBuilder.Build(profile, "/w", "/prefix", "/g.exe", "", new Dictionary<string, string>());

        Assert.Equal("/prefix", recipe.Environment["WINEPREFIX"]);
        EnvOverride conflict = Assert.Single(recipe.Overrides);
        Assert.Equal("/elsewhere", conflict.RecipeValue);
    }


    [Fact]
    public void Build_WithoutProfile_FallsBackToCallerArguments()
    {
        LaunchRecipe recipe = LaunchRecipeBuilder.Build(null, "/w", "/p", "/games/g.exe", "-x", new Dictionary<string, string>());

        Assert.Equal(new[] { "/w", "/games/g.exe", "-x" }, recipe.Argv);
        Assert.Empty(recipe.Overrides);
        Assert.Equal("/p", recipe.Environment["WINEPREFIX"]);
    }


    [Fact]
    public void JoinCommandLine_QuotesTokensWithSpaces()
    {
        string line = LaunchRecipeBuilder.JoinCommandLine(new[] { "/w", "/games/My Game/g.exe", "-x" });

        Assert.Equal("/w '/games/My Game/g.exe' -x", line);
    }


    [Fact]
    public void RuntimeProfile_SummaryShowsPrefixOrDefault()
    {
        var profile = new RuntimeProfile { Name = "a", RunnerPath = "/usr/bin/wine", PrefixPath = "/home/x/.wine" };
        Assert.Contains("wine", profile.Summary);
        Assert.Contains(".wine", profile.Summary);

        var bare = new RuntimeProfile { Name = "b" };
        Assert.Contains("启动时创建", bare.Summary);
    }


    [Theory]
    [InlineData(null, null, false)]
    [InlineData("dxvk-1", null, true)]
    [InlineData(null, "vkd3d-1", true)]
    [InlineData("dxvk-1", "vkd3d-1", true)]
    public void HasGraphics_IsTrueWhenEitherLayerIsSelected(string? dxvkId, string? vkd3dId, bool expected)
    {
        var profile = new RuntimeProfile { Name = "p", DxvkId = dxvkId, Vkd3dId = vkd3dId };

        Assert.Equal(expected, profile.HasGraphics);
    }
}


public class LaunchRecipeArgsMergeTests
{

    [Fact]
    public void Build_AppendsRecipeArgsAfterGameArgs()
    {
        var profile = new RuntimeProfile { Name = "t", Args = "-dx11" };

        LaunchRecipe recipe = LaunchRecipeBuilder.Build(
            profile, "/usr/bin/wine", "/prefix", "/games/YuanShen.exe",
            "login_auth_ticket=abc -popupwindow", new Dictionary<string, string>());

        Assert.Equal(
            new[] { "/usr/bin/wine", "/games/YuanShen.exe", "login_auth_ticket=abc", "-popupwindow", "-dx11" },
            recipe.Argv);
    }


    [Fact]
    public void Build_WithoutRecipeArgs_KeepsGameArgs()
    {
        var profile = new RuntimeProfile { Name = "t" };

        LaunchRecipe recipe = LaunchRecipeBuilder.Build(
            profile, "/w", "/p", "game.exe", "-use-d3d12", new Dictionary<string, string>());

        Assert.Equal(new[] { "/w", "game.exe", "-use-d3d12" }, recipe.Argv);
    }
}
