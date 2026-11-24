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

    [Fact]
    public void ReadAndDescribe_CoverMissingAndPresentAudioMarkers()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-ver-" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(GameVersionMarker.ReadGameVersion(dir));
            Assert.Contains("(缺失)", GameVersionMarker.Describe(dir));

            File.WriteAllLines(Path.Combine(dir, "config.ini"), new[] { "channel=1", "game_version=5.1.0" });
            Assert.Equal("5.1.0", GameVersionMarker.ReadGameVersion(dir));

            // 未知语音字段不读文件
            Assert.Null(GameVersionMarker.ReadAudioVersion(dir, "fr-fr"));
            // 已知字段但文件不在
            Assert.Null(GameVersionMarker.ReadAudioVersion(dir, "zh-cn"));

            File.WriteAllText(Path.Combine(dir, "Audio_Chinese_pkg_version"), "{\"a\":1}\n{\"b\":2}\n");
            Assert.Equal("{\"a\":1}\n{\"b\":2}", GameVersionMarker.ReadAudioVersion(dir, "zh-cn"));

            string described = GameVersionMarker.Describe(dir);
            Assert.Contains("game_version=5.1.0", described);
            Assert.Contains("Audio_Chinese_pkg_version=清单 1 行", described); // 去掉尾部换行后只剩一个分隔

            List<string> written = GameVersionMarker.WriteAll(dir, "5.2.0");
            Assert.Contains("config.ini: game_version=5.2.0", written);
            Assert.Equal("5.2.0", GameVersionMarker.ReadGameVersion(dir));
            Assert.False(File.Exists(Path.Combine(dir, "config.ini.lunarward-tmp")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }


    [Fact]
    public void WriteGameVersion_Appends_WhenVersionLineMissing()
    {
        string dir = Path.Combine(Path.GetTempPath(), "lw-ver-" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllLines(Path.Combine(dir, "config.ini"), new[] { "channel=1" });

            Assert.True(GameVersionMarker.WriteGameVersion(dir, "7.0.0"));
            string[] lines = File.ReadAllLines(Path.Combine(dir, "config.ini"));
            Assert.Contains("channel=1", lines);
            Assert.Equal("game_version=7.0.0", lines[^1]);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
