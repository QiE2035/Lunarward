using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starward.Features.Runtime;
using Xunit;

namespace Lunarward.Tests;


/// <summary>
/// D81：HUD / gamemode / gamescope 的展开。全是纯函数，所以直接对展开结果下断言；
/// PATH 查询通过 <c>Enhancements.FindInPath(binary, path)</c> 的 path 参数钉到临时目录，
/// 不依赖 CI 机器上装没装 mangohud。
/// </summary>
public class EnhancementsTests
{

    private static RuntimeProfile MakeProfile(Action<RuntimeProfile>? configure = null)
    {
        var profile = new RuntimeProfile
        {
            Name = "测试",
            RunnerId = "/usr/bin/wine",
            RunnerPath = "/usr/bin/wine",
            PrefixPath = "/tmp/prefix",
        };
        configure?.Invoke(profile);
        return profile;
    }


    [Fact]
    public void EnvironmentVariables_HudOff_IsEmpty()
    {
        Assert.Empty(Enhancements.EnvironmentVariables(MakeProfile()));
        Assert.Empty(Enhancements.EnvironmentVariables(null));
    }


    [Fact]
    public void EnvironmentVariables_Dxvk_UsesDefaultOptionsWhenBlank()
    {
        Dictionary<string, string> env = Enhancements.EnvironmentVariables(MakeProfile(x => x.Hud = "dxvk"));
        Assert.Equal(Enhancements.DefaultDxvkHud, env["DXVK_HUD"]);
        Assert.False(env.ContainsKey("MANGOHUD"));
    }


    [Fact]
    public void EnvironmentVariables_Dxvk_UsesCustomOptions()
    {
        Dictionary<string, string> env = Enhancements.EnvironmentVariables(MakeProfile(x =>
        {
            x.Hud = "dxvk";
            x.HudOptions = "fps,devinfo";
        }));
        Assert.Equal("fps,devinfo", env["DXVK_HUD"]);
    }


    [Fact]
    public void EnvironmentVariables_MangoHud_SetsFlagAndConfig()
    {
        Dictionary<string, string> env = Enhancements.EnvironmentVariables(MakeProfile(x =>
        {
            x.Hud = "mangohud";
            x.HudOptions = "fps_limit=60";
        }));
        Assert.Equal("1", env["MANGOHUD"]);
        Assert.Equal("fps_limit=60", env["MANGOHUD_CONFIG"]);
        Assert.False(env.ContainsKey("DXVK_HUD"));
    }


    [Fact]
    public void EnvironmentVariables_MangoHudWithGameScope_DoesNotInject()
    {
        Dictionary<string, string> env = Enhancements.EnvironmentVariables(MakeProfile(x =>
        {
            x.Hud = "mangohud";
            x.GameScope = true;
        }));
        Assert.Empty(env);
    }


    [Theory]
    [InlineData("dxvk", "dxvk")]
    [InlineData("DXVK", "dxvk")]
    [InlineData("mangohud", "mangohud")]
    [InlineData("mango", "mangohud")]
    [InlineData("off", "off")]
    [InlineData("", "off")]
    [InlineData(null, "off")]
    [InlineData("nonsense", "off")]
    public void NormalizeHud_MapsAliases(string? input, string expected)
    {
        Assert.Equal(expected, Enhancements.NormalizeHud(input));
    }


    [Fact]
    public void Wrappers_GameScopeIsOutermostAndUsesDefaultArgs()
    {
        List<string> wrappers = Enhancements.Wrappers(MakeProfile(x => x.GameScope = true));
        Assert.Equal(["gamescope -f"], wrappers);
    }


    [Fact]
    public void Wrappers_OrderIsGamescopeThenGamemodeThenUser()
    {
        List<string> wrappers = Enhancements.Wrappers(MakeProfile(x =>
        {
            x.GameScope = true;
            x.GameScopeArgs = "-f -W 2560 -H 1440";
            x.GameMode = true;
            x.Wrappers = ["env FOO=1", "  "];
        }));
        Assert.Equal(["gamescope -f -W 2560 -H 1440", "gamemoderun", "env FOO=1"], wrappers);
    }


    [Fact]
    public void RequiredBinaries_SkipsMangoHudUnderGameScope()
    {
        RuntimeProfile profile = MakeProfile(x =>
        {
            x.Hud = "mangohud";
            x.GameMode = true;
            x.GameScope = true;
        });
        Assert.Equal(["gamescope", "gamemoderun"], Enhancements.RequiredBinaries(profile));
    }


    [Fact]
    public void FindInPath_SearchesGivenDirectoriesOnly()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-enh-" + Guid.NewGuid().ToString("n")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string executable = Path.Combine(dir, "gamescope");
            string notExecutable = Path.Combine(dir, "gamemoderun");
            File.WriteAllText(executable, "#!/bin/sh\n");
            File.WriteAllText(notExecutable, "#!/bin/sh\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                // R124：没执行位的同名文件（下载残留、坏脚本）不算命中。
                File.SetUnixFileMode(notExecutable, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            Assert.Equal(executable, Enhancements.FindInPath("gamescope", dir));
            Assert.Null(Enhancements.FindInPath("gamemoderun", dir));
            Assert.Null(Enhancements.FindInPath(notExecutable, dir));
            Assert.Null(Enhancements.FindInPath("/nonexistent/gamemoderun", dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }


    [Fact]
    public void Describe_MentionsMissingBinary()
    {
        RuntimeProfile profile = MakeProfile(x =>
        {
            x.Hud = "dxvk";
            x.GameMode = true;
        });
        string text = Enhancements.Describe(profile, "/nonexistent-dir");
        Assert.Contains("DXVK_HUD=" + Enhancements.DefaultDxvkHud, text);
        Assert.Contains("gamemoderun", text);
        Assert.Contains("缺少程序：gamemoderun", text);
        Assert.Equal("没有开启任何增强", Enhancements.Describe(MakeProfile(), path: "/nonexistent-dir"));
    }


    [Fact]
    public void Build_AppliesEnhancementsToArgvAndEnvironment()
    {
        RuntimeProfile profile = MakeProfile(x =>
        {
            x.Hud = "dxvk";
            x.GameMode = true;
            x.Args = "-dx11";
        });
        LaunchRecipe recipe = LaunchRecipeBuilder.Build(profile, "/usr/bin/wine", "/tmp/prefix", "/games/game.exe", null,
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal(["gamemoderun", "/usr/bin/wine", "/games/game.exe", "-dx11"], recipe.Argv);
        Assert.Equal(Enhancements.DefaultDxvkHud, recipe.Environment["DXVK_HUD"]);
        Assert.Equal("/tmp/prefix", recipe.Environment["WINEPREFIX"]);
    }


    [Fact]
    public void Build_RecipeEnvStillOverridesInjectedHud()
    {
        RuntimeProfile profile = MakeProfile(x =>
        {
            x.Hud = "dxvk";
            x.Env["DXVK_HUD"] = "fps";
        });
        LaunchRecipe recipe = LaunchRecipeBuilder.Build(profile, "/usr/bin/wine", "/tmp/prefix", "/games/game.exe", null,
            new Dictionary<string, string>(StringComparer.Ordinal));

        Assert.Equal("fps", recipe.Environment["DXVK_HUD"]);
        Assert.Contains(recipe.Overrides, x => x.Key == "DXVK_HUD" && x.RecipeValue == "fps");
    }

    [Fact]
    public void ValidateGameScopeArgs_EmptyIsClean()
    {
        Assert.Empty(Enhancements.ValidateGameScopeArgs(null));
        Assert.Empty(Enhancements.ValidateGameScopeArgs(""));
        Assert.Empty(Enhancements.ValidateGameScopeArgs("  "));
        Assert.Empty(Enhancements.ValidateGameScopeArgs("-f -W 2560 -H 1440"));
    }


    [Fact]
    public void ValidateGameScopeArgs_FlagsShellEscapes()
    {
        IReadOnlyList<string> problems = Enhancements.ValidateGameScopeArgs("-f \"2560x1440\"");

        Assert.Contains(problems, x => x.Contains("不用 shell 引号"));
    }


    [Fact]
    public void ValidateGameScopeArgs_FlagsTrailingGameArgs()
    {
        IReadOnlyList<string> problems = Enhancements.ValidateGameScopeArgs("-f -- -windowed");

        Assert.Contains(problems, x => x.Contains("后面的参数会交给游戏本身"));
    }


    [Fact]
    public void ValidateGameScopeArgs_RequiresResolutionPairs()
    {
        Assert.Contains(Enhancements.ValidateGameScopeArgs("-f -W 2560"), x => x.Contains("缺 -H"));
        Assert.Contains(Enhancements.ValidateGameScopeArgs("-f -h 720"), x => x.Contains("缺 -w"));
        Assert.Empty(Enhancements.ValidateGameScopeArgs("-f -w 1280 -h 720"));
    }



    [Fact]
    public void ToggleArg_AddsFlagAtEnd()
    {
        Assert.Equal("-f --hdr-enabled", Enhancements.ToggleArg("-f", "--hdr-enabled", true));
    }


    [Fact]
    public void ToggleArg_DoesNotDuplicate()
    {
        Assert.Equal("--hdr-enabled -f", Enhancements.ToggleArg("--hdr-enabled -f", "--hdr-enabled", true));
    }


    [Fact]
    public void ToggleArg_RemovesFlag()
    {
        Assert.Equal("-f", Enhancements.ToggleArg("-f --adaptive-sync", "--adaptive-sync", false));
    }


    [Fact]
    public void ToggleArg_EmptyArgsWithFlagOn_KeepsJustTheFlag()
    {
        Assert.Equal("--hdr-enabled", Enhancements.ToggleArg("", "--hdr-enabled", true));
    }


    [Fact]
    public void ToggleArg_RemovingLastToken_FallsBackToDefault()
    {
        Assert.Equal(Enhancements.DefaultGameScopeArgs, Enhancements.ToggleArg("--adaptive-sync", "--adaptive-sync", false));
    }


    [Fact]
    public void Presets_AlignWithEachOther()
    {
        Assert.Equal(5, Enhancements.HudPresetOptions.Length);
        Assert.Equal(4, Enhancements.GameScopePresetArgs.Length);
        Assert.Equal("", Enhancements.HudPresetOptions[0]);
        Assert.Equal("", Enhancements.GameScopePresetArgs[0]);
        foreach (string preset in Enhancements.GameScopePresetArgs.Skip(1))
        {
            Assert.Contains("-W ", preset, StringComparison.Ordinal);
            Assert.Contains(" -H ", preset, StringComparison.Ordinal);
        }
    }

    // ---- R101 / R124 补充：MangoHud 走 Vulkan 层判定、可执行位、缺什么的口径 ----

    [Theory]
    [InlineData("dxvk", "DXVK HUD")]
    [InlineData("d3d", "DXVK HUD")]
    [InlineData("mangohud", "MangoHud")]
    [InlineData("mango", "MangoHud")]
    [InlineData("", "关闭")]
    [InlineData(null, "关闭")]
    public void HudText_NamesPreset(string? hud, string expected)
    {
        Assert.Equal(expected, Enhancements.HudText(hud));
    }


    [Fact]
    public void RequiredBinaries_NeverAsksForMangoHudCommand()
    {
        Assert.Empty(Enhancements.RequiredBinaries(null));
        Assert.Equal(new[] { "gamescope" }, Enhancements.RequiredBinaries(MakeProfile(x => x.GameScope = true)));
        Assert.Equal(new[] { "gamemoderun" }, Enhancements.RequiredBinaries(MakeProfile(x => x.GameMode = true)));
        Assert.DoesNotContain("mangohud", Enhancements.RequiredBinaries(MakeProfile(x => x.Hud = Enhancements.HudMangoHud)));
    }


    [Fact]
    public void HasMangoHudLayer_DetectsLayerManifest()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-layer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // 只看「能不能在额外的层目录里认出来」：本机装了 MangoHud 时会扫到系统目录，
            // 所以这里不断言「空目录 = false」（那取决于跑测试的机器）。
            File.WriteAllText(Path.Combine(dir, "MangoHud.json"), "{}");
            Assert.True(Enhancements.HasMangoHudLayer(dir));
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
            }
        }
    }


    [Fact]
    public void MissingRequirements_MangoHudWithoutLayer_AndUnderGameScopeSkipsIt()
    {
        string emptyDir = Path.Combine(Path.GetTempPath(), "lw-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);
        try
        {
            // 本机装了 MangoHud 层时不会被点名，这两种情况都合法，断言跟着实际探测走。
            bool hasLayer = Enhancements.HasMangoHudLayer(emptyDir);
            IReadOnlyList<string> missing = Enhancements.MissingRequirements(MakeProfile(x => x.Hud = Enhancements.HudMangoHud), emptyDir);
            if (hasLayer)
            {
                Assert.DoesNotContain("MangoHud（Vulkan 层）", missing);
            }
            else
            {
                Assert.Contains("MangoHud（Vulkan 层）", missing);
            }

            IReadOnlyList<string> withScope = Enhancements.MissingRequirements(
                MakeProfile(x =>
                {
                    x.Hud = Enhancements.HudMangoHud;
                    x.GameScope = true;
                }),
                emptyDir);
            Assert.DoesNotContain("MangoHud（Vulkan 层）", withScope);
            Assert.Contains("gamescope", withScope);
        }
        finally
        {
            try
            {
                Directory.Delete(emptyDir, recursive: true);
            }
            catch
            {
            }
        }
    }


    [Fact]
    public void IsExecutableFile_ChecksUnixModeBits()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-exec-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string plain = Path.Combine(dir, "plain");
            File.WriteAllText(plain, "#!/bin/sh\n");
            File.SetUnixFileMode(plain, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Assert.False(Enhancements.IsExecutableFile(plain));

            File.SetUnixFileMode(plain, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Assert.True(Enhancements.IsExecutableFile(plain));

            Assert.False(Enhancements.IsExecutableFile(Path.Combine(dir, "missing")));
            Assert.False(Enhancements.IsExecutableFile(null));
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
            }
        }
    }


    [Fact]
    public void Describe_CoversEachEnhancementBranch()
    {
        Assert.Equal("没有开启任何增强", Enhancements.Describe(null));

        string emptyDir = Path.Combine(Path.GetTempPath(), "lw-none-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);
        try
        {
            string dxvk = Enhancements.Describe(MakeProfile(x => x.Hud = Enhancements.HudDxvk), emptyDir);
            Assert.StartsWith("DXVK_HUD=", dxvk);

            string mangoUnderScope = Enhancements.Describe(
                MakeProfile(x =>
                {
                    x.Hud = Enhancements.HudMangoHud;
                    x.GameScope = true;
                }),
                emptyDir);
            Assert.Contains("MangoHud（gamescope 下不额外注入）", mangoUnderScope);
            // 空参数按默认 -f 展示
            Assert.Contains("gamescope -f", mangoUnderScope);

            string mode = Enhancements.Describe(MakeProfile(x => x.GameMode = true), emptyDir);
            Assert.Contains("gamemoderun", mode);
            Assert.Contains("缺少程序：gamemoderun", mode);
        }
        finally
        {
            try
            {
                Directory.Delete(emptyDir, recursive: true);
            }
            catch
            {
            }
        }
    }
}
