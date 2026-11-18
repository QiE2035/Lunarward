// Lunarward：方案启动历史（A–I 的 E 项）——JSONL 解析、坏行容错、时长的可读化。
using Starward.Features.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Lunarward.Tests;

public class LaunchHistoryStoreTests
{

    private static string Line(string profileId, string at, string outcome, string? detail = null, long? durationMs = null)
    {
        string detailJson = detail is null ? "null" : $"\"{detail}\"";
        string durationJson = durationMs is null ? "null" : durationMs.Value.ToString();
        return $"{{\"At\":\"{at}\",\"ProfileId\":\"{profileId}\",\"ProfileName\":\"p\",\"GameBiz\":\"hk4e_cn\"," +
               $"\"Outcome\":\"{outcome}\",\"Detail\":{detailJson},\"DurationMs\":{durationJson}}}";
    }


    [Fact]
    public void Parse_ReturnsNewestFirst_AndFiltersByProfile()
    {
        string[] lines =
        [
            Line("aaa", "2026-11-16T10:00:00+08:00", LaunchOutcome.Started),
            Line("bbb", "2026-11-16T10:05:00+08:00", LaunchOutcome.PreflightFailed, "Wine 运行器：找不到"),
            Line("aaa", "2026-11-16T10:10:00+08:00", LaunchOutcome.StartFailed, "Process.Start 返回 null"),
        ];
        IReadOnlyList<LaunchAttempt> recent = LaunchHistoryStore.Parse(lines, "aaa", 3);
        Assert.Equal(2, recent.Count);
        Assert.Equal(LaunchOutcome.StartFailed, recent[0].Outcome);
        Assert.Equal(LaunchOutcome.Started, recent[1].Outcome);
        Assert.False(recent[0].Ok);
        Assert.True(recent[1].Ok);
    }


    [Fact]
    public void Parse_SkipsBrokenLines()
    {
        string[] lines =
        [
            "{ this is not json",
            "",
            "   ",
            Line("aaa", "2026-11-16T10:00:00+08:00", LaunchOutcome.Started),
        ];
        IReadOnlyList<LaunchAttempt> recent = LaunchHistoryStore.Parse(lines, "aaa", 3);
        Assert.Single(recent);
    }


    [Fact]
    public void Parse_RespectsLimit()
    {
        string[] lines = Enumerable.Range(0, 8)
            .Select(i => Line("aaa", $"2026-11-16T10:0{i}:00+08:00", LaunchOutcome.Started))
            .ToArray();
        Assert.Equal(3, LaunchHistoryStore.Parse(lines, "aaa", 3).Count);
        Assert.Equal(1, LaunchHistoryStore.Parse(lines, "aaa", 1).Count);
    }


    [Fact]
    public void Parse_WithoutProfileFilter_ReturnsAllProfiles()
    {
        string[] lines =
        [
            Line("aaa", "2026-11-16T10:00:00+08:00", LaunchOutcome.Started),
            Line("bbb", "2026-11-16T10:01:00+08:00", LaunchOutcome.Started),
        ];
        Assert.Equal(2, LaunchHistoryStore.Parse(lines, null, 5).Count);
    }


    [Theory]
    [InlineData(0L, "0 ms")]
    [InlineData(999L, "999 ms")]
    [InlineData(1000L, "1.0 s")]
    [InlineData(1250L, "1.3 s")]
    [InlineData(60_000L, "1 分 0 秒")]
    [InlineData(125_000L, "2 分 5 秒")]
    public void FormatDuration_IsReadable(long ms, string expected)
    {
        Assert.Equal(expected, LaunchAttempt.FormatDuration(ms));
    }


    [Fact]
    public void Describe_ContainsOutcomeDurationAndDetail()
    {
        var attempt = new LaunchAttempt
        {
            At = new DateTimeOffset(2026, 11, 16, 10, 5, 0, TimeSpan.FromHours(8)),
            Outcome = LaunchOutcome.PreflightFailed,
            Detail = "Wine 运行器：找不到",
            DurationMs = 1250,
        };
        string text = attempt.Describe();
        Assert.Contains("11-16 10:05", text);
        Assert.Contains(LaunchOutcome.PreflightFailed, text);
        Assert.Contains("1.3 s", text);
        Assert.Contains("Wine 运行器", text);
    }


    [Fact]
    public void Describe_WithoutDetail_EndsAfterOutcome()
    {
        var attempt = new LaunchAttempt
        {
            At = new DateTimeOffset(2026, 11, 16, 10, 5, 0, TimeSpan.FromHours(8)),
            Outcome = LaunchOutcome.Started,
            DurationMs = 500,
        };
        Assert.EndsWith($"{LaunchOutcome.Started} · 500 ms", attempt.Describe());
    }

}
