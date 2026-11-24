using Starward.Features.Runtime;
using Xunit;

namespace Lunarward.Tests;

public class PrefixPathTests
{
    private const string Home = "/home/tester";

    [Fact]
    public void Expand_Home_Tilde_Returns_Home() =>
        Assert.Equal(Home, PrefixPath.Expand("~", Home));

    [Fact]
    public void Expand_Tilde_With_Subpath() =>
        Assert.Equal($"{Home}/games/prefix", PrefixPath.Expand("~/games/prefix", Home));

    [Fact]
    public void Expand_Home_Variable()
    {
        Assert.Equal($"{Home}/x", PrefixPath.Expand("$HOME/x", Home));
        Assert.Equal($"{Home}/y", PrefixPath.Expand("${HOME}/y", Home));
    }

    [Fact]
    public void Expand_Trims_Trailing_Slash()
    {
        // 尾斜杠会让 wine 直接走 stub 分支退出（L-92）
        Assert.Equal("/opt/wine/prefix", PrefixPath.Expand("/opt/wine/prefix/", Home));
        Assert.Equal($"{Home}/p", PrefixPath.Expand("~/p/", Home));
    }

    [Fact]
    public void Expand_Keeps_Root_Slash() =>
        Assert.Equal("/", PrefixPath.Expand("/", Home));

    [Fact]
    public void Expand_Null_Or_Blank_Returns_Null()
    {
        Assert.Null(PrefixPath.Expand(null, Home));
        Assert.Null(PrefixPath.Expand("   ", Home));
    }

    [Fact]
    public void Normalize_Is_Idempotent()
    {
        Assert.Equal("/a/b", PrefixPath.Normalize(PrefixPath.Normalize("/a/b/")));
    }

    [Fact]
    public void DefaultPrefixDirectory_EscapesRoot_WhenSlugIsTraversal()
    {
        // R52：Id/slug 来自 JSON，可能被手改成 "../x"，结果必须仍落在 root 内。
        string root = "/tmp/lw-prefixes";
        Assert.StartsWith(root + "/", PrefixPath.DefaultPrefixDirectory(root, "../escape", null, "abc"));
        Assert.StartsWith(root + "/", PrefixPath.DefaultPrefixDirectory(root, null, "../../x", "../z"));
        // 名字含非法字符时退回 Id，而不是叫 default
        Assert.Equal("/tmp/lw-prefixes/abc", PrefixPath.DefaultPrefixDirectory(root, null, "///", "abc"));
    }


    [Fact]
    public void Expand_BareHomeVariables_And_RelativePaths()
    {
        string home = "/home/someone";
        Assert.Equal(home, PrefixPath.Expand("$HOME", home));
        Assert.Equal(home, PrefixPath.Expand("${HOME}", home));
        Assert.Equal($"{home}/games/p", PrefixPath.Expand("${HOME}/games/p", home));
        // 相对路径立刻绝对化，不跟着 CWD 漂移
        string? relative = PrefixPath.Expand("prefix-here", home);
        Assert.True(System.IO.Path.IsPathRooted(relative));
    }
}
