using Starward.Features.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Lunarward.Tests;


public class RuntimeProfileTests
{
    private static string Root => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lw-prefix-root");


    /// <summary>
    /// R44：前缀目录由 PrefixSlug 决定，改显示名不换目录
    /// （否则「统一改名」会把已装的 DXVK/注册表留在孤儿前缀里）。
    /// </summary>
    [Fact]
    public void DefaultPrefixDirectory_KeepsSlugWhenNameChanges()
    {
        string before = PrefixPath.DefaultPrefixDirectory(Root, "genshin", "原神", "0011a2b3");
        string after = PrefixPath.DefaultPrefixDirectory(Root, "genshin", "原神·国服", "0011a2b3");

        Assert.EndsWith(System.IO.Path.Combine("lw-prefix-root", "genshin"), before);
        Assert.Equal(before, after);
    }


    /// <summary>R44：没有 slug 的旧配置（或手写 JSON）仍按名字推导，升级后目录不变。</summary>
    [Fact]
    public void DefaultPrefixDirectory_LegacyProfileWithoutSlug_UsesSanitizedName()
    {
        string path = PrefixPath.DefaultPrefixDirectory(Root, null, "My Profile!", "0011a2b3");

        Assert.EndsWith(System.IO.Path.Combine("lw-prefix-root", "my-profile"), path);
    }


    /// <summary>R44：名字都是不可用字符时退回 Id，不落成空目录名。</summary>
    [Fact]
    public void DefaultPrefixDirectory_FallsBackToId_WhenNameSanitizesEmpty()
    {
        string path = PrefixPath.DefaultPrefixDirectory(Root, null, "///", "abc12345");

        Assert.EndsWith(System.IO.Path.Combine("lw-prefix-root", "abc12345"), path);
    }


    /// <summary>
    /// R65：给新游戏复制方案必须逐字段复制增强开关。以前手写字段清单漏了 HUD / gamemode / gamescope，
    /// 用户开着 HUD 启动新游戏会静默全关。这里用反射兜底：以后再加字段忘了复制，这条会直接红。
    /// </summary>
    [Fact]
    public void CopyWithNewId_CopiesEveryFieldExceptIdentityAndPrefix()
    {
        var source = new Starward.Features.Runtime.RuntimeProfile
        {
            Name = "原神",
            PrefixSlug = "genshin",
            PrefixPath = "/tmp/prefix/genshin",
            Notes = "备注",
            RunnerId = "runner-1",
            RunnerPath = "/opt/wine/bin/wine",
            DxvkId = "dxvk-1",
            Vkd3dId = "vkd3d-1",
            Args = "-dx11",
            PrefixArch = "win32",
            Shell = "/bin/bash",
            GameOutput = "terminal",
            Hud = "mangohud",
            HudOptions = "--fps",
            GameMode = true,
            GameScope = true,
            GameScopeArgs = "-f -r 60",
            Env = new Dictionary<string, string> { ["A"] = "1" },
            Wrappers = new List<string> { "gamemoderun" },
        };

        Starward.Features.Runtime.RuntimeProfile copy = source.CopyWithNewId();

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(16, copy.Id.Length);
        Assert.Equal("", copy.Name);
        Assert.Null(copy.PrefixSlug);
        Assert.Null(copy.PrefixPath);
        Assert.True(copy.GameMode);
        Assert.True(copy.GameScope);
        Assert.Equal("mangohud", copy.Hud);
        Assert.Equal("--fps", copy.HudOptions);
        Assert.Equal("-f -r 60", copy.GameScopeArgs);
        Assert.Equal("terminal", copy.GameOutput);
        Assert.Equal("-dx11", copy.Args);
        Assert.Equal("win32", copy.PrefixArch);
        Assert.Equal("/bin/bash", copy.Shell);
        Assert.Equal("runner-1", copy.RunnerId);
        Assert.Equal("dxvk-1", copy.DxvkId);
        Assert.Equal("vkd3d-1", copy.Vkd3dId);
        Assert.NotSame(source.Env, copy.Env);
        Assert.Equal("1", copy.Env["A"]);
        Assert.NotSame(source.Wrappers, copy.Wrappers);
        Assert.Equal("gamemoderun", Assert.Single(copy.Wrappers));
    }


    /// <summary>R65：反射兜底——除身份/前缀/备注外的公开可写属性都必须相等。</summary>
    [Fact]
    public void CopyWithNewId_LeavesNoFieldBehind_Reflection()
    {
        var source = new Starward.Features.Runtime.RuntimeProfile
        {
            Name = "S", PrefixSlug = "s", PrefixPath = "/tmp/s", Notes = "n",
            RunnerId = "r", DxvkId = "d", Vkd3dId = "v", Hud = "dxvk", GameMode = true, GameScope = true,
        };
        Starward.Features.Runtime.RuntimeProfile copy = source.CopyWithNewId();

        var skipped = new HashSet<string> { "Id", "Name", "PrefixSlug", "PrefixPath", "Notes", "Env", "Wrappers" };
        foreach (System.Reflection.PropertyInfo property in typeof(Starward.Features.Runtime.RuntimeProfile)
                     .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite || skipped.Contains(property.Name) || property.GetIndexParameters().Length > 0)
            {
                continue;
            }
            Assert.True(Equals(property.GetValue(source), property.GetValue(copy)), $"字段 {property.Name} 没有被复制");
        }
    }


    /// <summary>R69：方案名规范化——去控制字符、去路径分隔符、折叠空白、限长。</summary>
    [Theory]
    [InlineData("  原神  ", "原神")]
    [InlineData("a/b:c*d?e", "a b c d e")]
    [InlineData("", "未命名")]
    [InlineData("   ", "未命名")]
    public void NormalizeName_Sanitizes(string input, string expected)
    {
        Assert.Equal(expected, Starward.Features.Runtime.RuntimeProfile.NormalizeName(input));
    }
}
