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
    public void Build_UsesProfileArgsWhenPresent_AndFillsPrefix()
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
        Assert.Equal(new[] { "gamemoderun", "/w", "/games/g.exe", "-windowed" }, recipe.Argv);
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



    [Fact]
    public void MigrateFromLegacy_TemplateMode_ClearsPrefixAndKeepsTemplateInNotes()
    {
        var profile = new RuntimeProfile
        {
            Name = "老的模板方案",
            LegacyPrefixMode = "template",
            PrefixPath = "/home/u/.local/share/lunarward/prefix-templates/tpl1",
            SchemaVersion = 1,
        };

        Assert.True(profile.MigrateFromLegacy());

        Assert.Null(profile.PrefixPath);
        Assert.Null(profile.LegacyPrefixMode);
        Assert.Contains("prefix-templates/tpl1", profile.Notes);
        Assert.Equal(2, profile.SchemaVersion);
    }


    [Fact]
    public void MigrateFromLegacy_ExternalMode_KeepsPath()
    {
        var profile = new RuntimeProfile
        {
            Name = "外部前缀",
            LegacyPrefixMode = "external",
            PrefixPath = "/mnt/games/prefix",
            SchemaVersion = 1,
        };

        profile.MigrateFromLegacy();

        Assert.Equal("/mnt/games/prefix", profile.PrefixPath);
        Assert.Null(profile.LegacyPrefixMode);
    }


    [Fact]
    public void MigrateFromLegacy_MovesEnvironmentDictionary_AndDropsEmptyOne()
    {
        var profile = new RuntimeProfile
        {
            Name = "旧环境变量",
            LegacyEnvironment = new Dictionary<string, string> { ["DXVK_HUD"] = "fps", ["WINEDLLOVERRIDES"] = "d3d11=n" },
        };

        Assert.True(profile.MigrateFromLegacy());

        Assert.Equal("fps", profile.Env["DXVK_HUD"]);
        Assert.Equal("d3d11=n", profile.Env["WINEDLLOVERRIDES"]);
        Assert.Null(profile.LegacyEnvironment);

        var empty = new RuntimeProfile { Name = "空字典", LegacyEnvironment = new Dictionary<string, string>() };
        Assert.True(empty.MigrateFromLegacy());
        Assert.Null(empty.LegacyEnvironment);
    }


    [Fact]
    public void MigrateFromLegacy_EnsureGraphicsFalse_ClearsGraphicsId()
    {
        var profile = new RuntimeProfile { Name = "关了自动装", LegacyEnsureGraphics = false, GraphicsId = "dxvk-3.1.1" };

        Assert.True(profile.MigrateFromLegacy());

        Assert.Null(profile.GraphicsId);
        Assert.Null(profile.LegacyEnsureGraphics);
    }


    [Fact]
    public void MigrateFromLegacy_SecondRun_ReportsNoChange()
    {
        var profile = new RuntimeProfile { Name = "已经新格式", SchemaVersion = 2 };

        Assert.False(profile.MigrateFromLegacy());
    }
}
