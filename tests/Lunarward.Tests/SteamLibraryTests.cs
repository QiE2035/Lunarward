using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Starward.Features.Runtime;
using Xunit;

namespace Lunarward.Tests;

/// <summary>
/// D78：Steam 库发现。之前只认两处硬编码路径，自定义库（`libraryfolders.vdf`）里的 Proton 扫不到。
/// </summary>
public class SteamLibraryTests
{
    private const string NewFormatVdf = """
        "libraryfolders"
        {
        	"0"
        	{
        		"path"		"/home/u/.local/share/Steam"
        		"label"		""
        		"contentid"		"1234567890"
        		"apps"
        		{
        			"228980"		"1024"
        		}
        	}
        	"1"
        	{
        		"path"		"/run/media/u/games/SteamLibrary"
        		"label"		""
        	}
        }
        """;

    private const string OldFormatVdf = """
        "LibraryFolders"
        {
        	"TimeNextStatsReport"		"1700000000"
        	"ContentStatsID"		"-123456789"
        	"1"		"/mnt/ssd/SteamLibrary"
        }
        """;


    [Fact]
    public void ParseLibraries_NewFormat_ReturnsEveryPath()
    {
        IReadOnlyList<string> libraries = SteamLibrary.ParseLibraries(NewFormatVdf);

        Assert.Equal(2, libraries.Count);
        Assert.Contains("/home/u/.local/share/Steam", libraries);
        Assert.Contains("/run/media/u/games/SteamLibrary", libraries);
    }


    [Fact]
    public void ParseLibraries_OldFormat_IgnoresNonPathKeys()
    {
        IReadOnlyList<string> libraries = SteamLibrary.ParseLibraries(OldFormatVdf);

        Assert.Single(libraries);
        Assert.Equal("/mnt/ssd/SteamLibrary", libraries[0]);
    }


    [Fact]
    public void ParseLibraries_TrailingSlashIsNormalizedAndDuplicatesCollapse()
    {
        const string vdf = """
            "libraryfolders"
            {
            	"0" { "path" "/games/Steam/" }
            	"1" { "path" "/games/Steam" }
            }
            """;

        IReadOnlyList<string> libraries = SteamLibrary.ParseLibraries(vdf);

        Assert.Single(libraries);
        Assert.Equal("/games/Steam", libraries[0]);
    }


    [Fact]
    public void ParseLibraries_GarbageText_ReturnsEmptyInsteadOfThrowing()
    {
        Assert.Empty(SteamLibrary.ParseLibraries("这不是 VDF {{{"));
        Assert.Empty(SteamLibrary.ParseLibraries(""));
        Assert.Empty(SteamLibrary.ParseLibraries((string?) null));
    }


    [Fact]
    public void ReadLibraries_MergesVdfLibrariesWithExistingRoots()
    {
        string home = Path.Combine(Path.GetTempPath(), "lunarward-steam-" + Guid.NewGuid().ToString("n")[..8]);
        string defaultRoot = Path.Combine(home, ".local/share/Steam");
        string customRoot = Path.Combine(home, "games/SteamLibrary");
        try
        {
            Directory.CreateDirectory(Path.Combine(defaultRoot, "steamapps"));
            Directory.CreateDirectory(Path.Combine(customRoot, "steamapps", "compatibilitytools.d"));
            File.WriteAllText(Path.Combine(defaultRoot, "steamapps", "libraryfolders.vdf"), $$"""
                "libraryfolders"
                {
                	"0" { "path" "{{defaultRoot}}" }
                	"1" { "path" "{{customRoot}}" }
                }
                """);

            IReadOnlyList<string> libraries = SteamLibrary.ReadLibraries(home);

            Assert.Contains(defaultRoot, libraries);
            Assert.Contains(customRoot, libraries);
            // 只登记默认根时不会重复出现
            Assert.Equal(libraries.Count, libraries.Distinct().Count());
        }
        finally
        {
            if (Directory.Exists(home))
            {
                Directory.Delete(home, recursive: true);
            }
        }
    }


    [Fact]
    public void CompatRoots_CoverCompatToolAndCommonOfEveryLibrary()
    {
        string home = Path.Combine(Path.GetTempPath(), "lunarward-steam-" + Guid.NewGuid().ToString("n")[..8]);
        string root = Path.Combine(home, ".local/share/Steam");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "steamapps"));
            File.WriteAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"), $$"""
                "libraryfolders"
                {
                	"0" { "path" "{{root}}" }
                }
                """);

            List<string> roots = SteamLibrary.CompatRoots(home).ToList();

            Assert.Contains(Path.Combine(root, "steamapps", "compatibilitytools.d"), roots);
            Assert.Contains(Path.Combine(root, "steamapps", "common"), roots);
        }
        finally
        {
            if (Directory.Exists(home))
            {
                Directory.Delete(home, recursive: true);
            }
        }
    }


    [Theory]
    [InlineData("Proton 9.0", true)]
    [InlineData("Proton - Experimental", true)]
    [InlineData("GE-Proton9-20", true)]
    [InlineData("Counter-Strike Global Offensive", false)]
    [InlineData("Genshin Impact", false)]
    public void IsProtonFolder_OnlyAcceptsProtonNames(string folder, bool expected)
    {
        Assert.Equal(expected, SteamLibrary.IsProtonFolder("/x/steamapps/common/" + folder));
    }
}
