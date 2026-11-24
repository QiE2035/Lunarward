// Lunarward：输出捕获与启动历史的落盘路径（D27 / D73）。
//
// 两处都按 XDG_DATA_HOME 决定文件位置，并且都是「写失败不能让游戏启动失败」的旁路逻辑——
// 这类代码以前完全没有用例，出错只能等用户发现「面板空白 / 历史不涨」。
using Starward.Features.Runtime;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Lunarward.Tests;


[Collection("env-serial")]
public class RuntimeStorageTests : IDisposable
{
    private readonly string _dataRoot;

    private readonly string? _previousDataHome;


    public RuntimeStorageTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "lw-data-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dataRoot);
        _previousDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _dataRoot);
    }


    public void Dispose()
    {
        Environment.SetEnvironmentVariable("XDG_DATA_HOME", _previousDataHome);
        try
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
        catch
        {
            // 清理失败不影响结论
        }
    }


    [Fact]
    public void OutputCapture_FollowsXdgDataHome()
    {
        OutputCapture.Append("game:hk4e_cn", "hello");

        string folder = Path.Combine(_dataRoot, "lunarward", "logs", "output");
        string[] files = Directory.GetFiles(folder);
        Assert.Single(files);
        Assert.Contains("hello", File.ReadAllText(files[0]));
    }


    [Fact]
    public void OutputCapture_Recent_ReturnsNewestLast_AndFiltersByScope()
    {
        OutputCapture.Append("game:aaa", "one");
        OutputCapture.Append("companion:bbb", "two");
        OutputCapture.Append("game:aaa", "three");

        var all = OutputCapture.Recent(10, "game:aaa");
        Assert.Equal(new[] { "one", "three" }, all.Select(x => x.Text));

        var last = OutputCapture.Recent(1, "game:aaa");
        Assert.Equal("three", Assert.Single(last).Text);
    }


    [Fact]
    public void OutputCapture_DropsEmptyLines()
    {
        int before = OutputCapture.Recent(5000).Count;
        OutputCapture.Append("game:aaa", "");
        OutputCapture.Append("game:aaa", null!);

        Assert.Equal(before, OutputCapture.Recent(5000).Count);
    }


    [Fact]
    public void OutputCapture_MarksErrorLines()
    {
        OutputCapture.Append("game:aaa", "boom", isError: true);

        OutputLine line = OutputCapture.Recent(1, "game:aaa").Single();
        Assert.True(line.IsError);
    }


    [Theory]
    [InlineData("panel", OutputVisibility.Panel)]
    [InlineData("PANEL", OutputVisibility.Panel)]
    [InlineData("terminal", OutputVisibility.Terminal)]
    [InlineData("", OutputVisibility.Silent)]
    [InlineData(null, OutputVisibility.Silent)]
    [InlineData("nonsense", OutputVisibility.Silent)]
    public void ParseVisibility_MapsSwitchValues(string? value, OutputVisibility expected)
    {
        Assert.Equal(expected, OutputCapture.ParseVisibility(value));
    }


    [Fact]
    public void LaunchHistoryStore_RecordThenRecent_RoundTrips()
    {
        LaunchHistoryStore.Record(new LaunchAttempt
        {
            At = DateTimeOffset.Now,
            ProfileId = "p1",
            ProfileName = "原神",
            GameBiz = "hk4e_cn",
            Outcome = LaunchOutcome.Started,
            DurationMs = 1200,
        });
        LaunchHistoryStore.Record(new LaunchAttempt
        {
            At = DateTimeOffset.Now,
            ProfileId = "p2",
            ProfileName = "绝区零",
            GameBiz = "nap_cn",
            Outcome = LaunchOutcome.PreflightFailed,
            Detail = "Wine 运行器：找不到",
        });

        LaunchAttempt mine = Assert.Single(LaunchHistoryStore.Recent("p1"));
        Assert.Equal(LaunchOutcome.Started, mine.Outcome);
        Assert.True(mine.Ok);
        Assert.Equal(TimeSpan.FromMilliseconds(1200), mine.Duration);
        Assert.Equal(2, LaunchHistoryStore.Recent(null, 10).Count);
    }


    [Fact]
    public void LaunchHistoryStore_FileLivesUnderXdgDataHome()
    {
        LaunchHistoryStore.Record(new LaunchAttempt { At = DateTimeOffset.Now, ProfileId = "p1", Outcome = LaunchOutcome.Started });

        Assert.StartsWith(_dataRoot, LaunchHistoryStore.FilePath);
        Assert.True(File.Exists(LaunchHistoryStore.FilePath));
    }


    [Fact]
    public void LaunchHistoryStore_Recent_WithoutFile_IsEmpty()
    {
        Assert.Empty(LaunchHistoryStore.Recent("nothing-here"));
    }


    [Fact]
    public void LaunchHistoryStore_Record_KeepsFileBounded()
    {
        // 400 行以上会压到 200 行（CompactThreshold / KeepLines），否则 jsonl 只增不减。
        DateTimeOffset start = DateTimeOffset.Now;
        for (int i = 0; i < 460; i++)
        {
            LaunchHistoryStore.Record(new LaunchAttempt
            {
                At = start.AddSeconds(i),
                ProfileId = "p1",
                Outcome = LaunchOutcome.Started,
            });
        }

        string[] lines = File.ReadAllLines(LaunchHistoryStore.FilePath);
        Assert.InRange(lines.Length, 200, 260);
        LaunchAttempt newest = LaunchHistoryStore.Recent("p1", 1).Single();
        Assert.Equal(start.AddSeconds(459).ToUnixTimeSeconds(), newest.At.ToUnixTimeSeconds());
    }


    [Fact]
    public void LaunchHistoryStore_CorruptedFile_DoesNotThrow()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LaunchHistoryStore.FilePath)!);
        File.WriteAllText(LaunchHistoryStore.FilePath, "{ broken json\n");

        Assert.Empty(LaunchHistoryStore.Recent("p1"));

        // 坏行不该阻止后续追加
        LaunchHistoryStore.Record(new LaunchAttempt { At = DateTimeOffset.Now, ProfileId = "p1", Outcome = LaunchOutcome.Started });
        Assert.Single(LaunchHistoryStore.Recent("p1"));
    }


    [Fact]
    public void Describe_FixesCultureForTimestampsAndDurations()
    {
        var attempt = new LaunchAttempt
        {
            At = new DateTimeOffset(2026, 11, 24, 9, 30, 0, TimeSpan.FromHours(8)),
            ProfileId = "p1",
            GameBiz = "hk4e_cn",
            Outcome = LaunchOutcome.Started,
            DurationMs = 1500,
        };

        string text = attempt.Describe();

        Assert.Contains("09:30", text);
        Assert.Contains("1.5 s", text);
    }

    [Fact]
    public void OutputCapture_Attach_CollectsStdoutAndStderr()
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo("/bin/sh")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("echo from-stdout; echo from-stderr >&2");
        process.Start();

        OutputCapture.Attach("game:proc", process);
        process.WaitForExit(5000);
        // Attach 内部的 WaitForExit 是异步的，给它一点时间把管道尾部读完。
        for (int i = 0; i < 200 && OutputCapture.Recent(500, "game:proc").Count < 2; i++)
        {
            System.Threading.Thread.Sleep(10);
        }

        var lines = OutputCapture.Recent(50, "game:proc");
        Assert.Contains(lines, x => x.Text.Contains("from-stdout"));
        Assert.Contains(lines, x => x.Text.Contains("from-stderr") && x.IsError);
    }


    [Fact]
    public void OutputCapture_Recent_ClampsCount()
    {
        OutputCapture.Append("game:clamp", "only");

        Assert.Single(OutputCapture.Recent(0, "game:clamp"));
        Assert.Single(OutputCapture.Recent(-5, "game:clamp"));
    }
}
