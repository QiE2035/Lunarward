using Starward.Features.Runtime;
using System;
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
}
