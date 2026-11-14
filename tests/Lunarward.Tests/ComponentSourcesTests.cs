// Lunarward：组件源清单与命名的纯逻辑（D73 §2.2）。
//
// ListAssetsAsync 要网络，不进单测；这里只钉「配置合并」「归档判定」「版本抠取」。
using Starward.Features.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Lunarward.Tests;


[Collection("env-serial")]
public class ComponentSourcesTests : IDisposable
{
    private readonly string _configRoot;

    private readonly string? _previousConfigRoot;


    public ComponentSourcesTests()
    {
        _configRoot = Path.Combine(Path.GetTempPath(), "lw-sources-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configRoot);
        _previousConfigRoot = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _configRoot);
    }


    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _previousConfigRoot);
        try
        {
            Directory.Delete(_configRoot, recursive: true);
        }
        catch
        {
            // 清理失败不影响结论
        }
    }


    [Fact]
    public void FilePath_FollowsXdgConfigHome()
    {
        Assert.Equal(Path.Combine(_configRoot, "lunarward", "components-sources.json"), ComponentSources.FilePath);
    }


    [Fact]
    public void Load_WithoutUserFile_ReturnsDefaultsSortedByRank()
    {
        IReadOnlyList<ComponentSourceEntry> sources = ComponentSources.Load();

        Assert.Equal(ComponentSources.Defaults.Count, sources.Count);
        Assert.Equal(sources.OrderBy(x => x.Rank).Select(x => x.Id), sources.Select(x => x.Id));
        Assert.All(sources, x => Assert.False(string.IsNullOrWhiteSpace(x.Owner)));
    }


    [Fact]
    public void Load_UserEntryWithSameId_OverridesDefault()
    {
        ComponentSources.SaveUserEntries(new[]
        {
            new ComponentSourceEntry
            {
                Id = "wine-staging",
                DisplayName = "自定义 staging",
                Kind = "wine",
                Owner = "someone",
                Repo = "wine-builds",
                Rank = 1,
                MirrorPrefix = "https://mirror.example/",
            },
        });

        IReadOnlyList<ComponentSourceEntry> sources = ComponentSources.Load();

        ComponentSourceEntry overridden = sources.Single(x => x.Id == "wine-staging");
        Assert.Equal("自定义 staging", overridden.DisplayName);
        Assert.Equal("https://mirror.example/", overridden.MirrorPrefix);
        Assert.Equal("wine-staging", sources[0].Id);
        Assert.Equal(ComponentSources.Defaults.Count, sources.Count);
    }


    [Fact]
    public void Load_UserEntryWithNewId_IsAppended()
    {
        ComponentSources.SaveUserEntries(new[]
        {
            new ComponentSourceEntry { Id = "my-wine", DisplayName = "自建", Kind = "wine", Owner = "me", Repo = "wine", Rank = 99 },
        });

        IReadOnlyList<ComponentSourceEntry> sources = ComponentSources.Load();

        Assert.Equal("my-wine", sources[^1].Id);
        Assert.Equal(ComponentSources.Defaults.Count + 1, sources.Count);
    }


    [Fact]
    public void Load_CorruptedUserFile_FallsBackToDefaults()
    {
        string path = ComponentSources.FilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "{ this is not json");

        IReadOnlyList<ComponentSourceEntry> sources = ComponentSources.Load();

        Assert.Equal(ComponentSources.Defaults.Count, sources.Count);
    }


    [Theory]
    [InlineData("wine-tkg.tar.xz", true)]
    [InlineData("wine.tar.gz", true)]
    [InlineData("wine.tar.zst", true)]
    [InlineData("wine.TGZ", true)]
    [InlineData("wine.zip", false)]
    [InlineData("wine.exe", false)]
    public void IsArchive_RecognizesSupportedFormats(string name, bool expected)
    {
        Assert.Equal(expected, ComponentSources.IsArchive(name));
    }


    [Theory]
    [InlineData("wine.tar.zst", true)]
    [InlineData("wine.tar.gz", true)]
    [InlineData("wine.tgz", true)]
    [InlineData("wine.tar.xz", false)]
    public void PrefersZstd_MarksFormatsTarCanReadDirectly(string name, bool expected)
    {
        Assert.Equal(expected, ComponentSources.PrefersZstd(name));
    }


    [Theory]
    [InlineData("dxvk-3.1.1.tar.gz", "v3.1.1", "3.1.1")]
    [InlineData("wine-z-9.9-x86_64.tar.xz", "9.9", "9.9")]
    [InlineData("GE-Proton9-20.tar.gz", "GE-Proton9-20", "GE-Proton9-20")]
    public void GuessVersion_PrefersNumberFromFileName(string fileName, string tag, string expected)
    {
        Assert.Equal(expected, ComponentSources.GuessVersion(fileName, tag));
    }


    [Fact]
    public void GuessVersion_WithoutNumber_FallsBackToTag()
    {
        Assert.Equal("9-20", ComponentSources.GuessVersion("GE-Proton.tar.gz", "v9-20"));
        Assert.Equal("", ComponentSources.GuessVersion("wine.tar.gz", ""));
    }
}
