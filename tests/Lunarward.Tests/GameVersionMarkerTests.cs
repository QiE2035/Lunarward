using Starward.Features.GameInstall.Sophon;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Lunarward.Tests;

public class GameVersionMarkerTests
{

    [Fact]
    public void WriteGameVersion_Replaces_Only_The_Version_Line()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-ver-" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            string config = Path.Combine(dir, "config.ini");
            System.IO.File.WriteAllLines(config, new[]
            {
                "[General]",
                "channel=1",
                "cps=mihoyo",
                "game_version=1.2.3",
                "sub_channel=0",
            });

            Assert.True(GameVersionMarker.WriteGameVersion(dir, "9.9.9"));
            Assert.Equal("9.9.9", GameVersionMarker.ReadGameVersion(dir));

            string[] lines = System.IO.File.ReadAllLines(config);
            Assert.Contains("channel=1", lines);
            Assert.Contains("cps=mihoyo", lines);
            Assert.Contains("sub_channel=0", lines);
            Assert.Contains("game_version=9.9.9", lines);
            Assert.DoesNotContain("game_version=1.2.3", lines);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }


    [Fact]
    public void WriteGameVersion_Fails_Without_Config()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-ver-" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            Assert.False(GameVersionMarker.WriteGameVersion(dir, "9.9.9"));
            Assert.Null(GameVersionMarker.ReadGameVersion(dir));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

}
