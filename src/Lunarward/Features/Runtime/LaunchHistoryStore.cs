// Lunarward：方案启动历史（A 项优化的数据源）。
//
// 为什么另写一份而不是解析 logs/launch/*.json：那些报告只在「真的走到 Process.Start」时写，
// 预检失败根本不会留下文件，而用户最想看的恰恰是「为什么这次没起来」。
// 这里用 JSONL 追加，一次启动一行，读完按时间倒序给页面用。
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starward.Features.Runtime;


/// <summary>一次启动尝试（不管是起来了还是被预检拦下）。</summary>
public sealed class LaunchAttempt
{

    public DateTimeOffset At { get; set; }

    public string? ProfileId { get; set; }

    public string? ProfileName { get; set; }

    public string? GameBiz { get; set; }

    /// <summary>已启动 / 预检未通过 / 启动失败。</summary>
    public string Outcome { get; set; } = "";

    public bool Ok => Outcome == LaunchOutcome.Started;

    /// <summary>失败原因（预检项明细或异常信息）；成功时是运行器版本之类的短说明。</summary>
    public string? Detail { get; set; }

    /// <summary>从进界到进程起来的耗时；预检失败时是被拦下的耗时。</summary>
    public long? DurationMs { get; set; }

    [JsonIgnore]
    public TimeSpan? Duration => DurationMs is { } ms ? TimeSpan.FromMilliseconds(ms) : null;


    public string Describe()
    {
        string when = At.LocalDateTime.ToString("MM-dd HH:mm");
        string took = DurationMs is { } ms ? $" · {FormatDuration(ms)}" : "";
        string detail = string.IsNullOrWhiteSpace(Detail) ? "" : $" · {Detail}";
        return $"{when} {Outcome}{took}{detail}";
    }


    internal static string FormatDuration(long ms)
    {
        if (ms < 1000)
        {
            return $"{ms} ms";
        }
        if (ms < 60_000)
        {
            return $"{ms / 1000.0:0.0} s";
        }
        return $"{ms / 60_000} 分 {(ms % 60_000) / 1000} 秒";
    }

}


public static class LaunchOutcome
{
    public const string Started = "已启动";

    public const string PreflightFailed = "预检未通过";

    public const string StartFailed = "启动失败";
}


public static class LaunchHistoryStore
{

    /// <summary>超过这个行数就压到 <see cref="KeepLines"/>，避免文件无限长。</summary>
    private const int CompactThreshold = 400;

    private const int KeepLines = 200;


    public static string FilePath
    {
        get
        {
            string data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x
                ? x
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(data, "lunarward", "logs", "launch-history.jsonl");
        }
    }


    public static void Record(LaunchAttempt attempt, ILogger? logger = null)
    {
        try
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string line = JsonSerializer.Serialize(attempt, JsonOptions);
            File.AppendAllText(path, line + "\n");
            CompactIfNeeded(path);
        }
        catch (Exception ex)
        {
            // 历史只是辅助信息，写不进去不能影响启动
            logger?.LogDebug(ex, "write launch history failed");
        }
    }


    /// <summary>按方案取最近几次尝试（新的在前）。profileId 为空时返回全部方案。</summary>
    public static IReadOnlyList<LaunchAttempt> Recent(string? profileId, int limit = 3)
    {
        try
        {
            string path = FilePath;
            if (!File.Exists(path))
            {
                return [];
            }
            return Parse(File.ReadLines(path), profileId, limit);
        }
        catch
        {
            return [];
        }
    }


    /// <summary>解析 JSONL（坏行直接跳过：手工改过、写一半断电都不该让页面炸）。</summary>
    public static IReadOnlyList<LaunchAttempt> Parse(IEnumerable<string> lines, string? profileId, int limit = 3)
    {
        List<LaunchAttempt> all = [];
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }
            try
            {
                LaunchAttempt? attempt = JsonSerializer.Deserialize<LaunchAttempt>(line, JsonOptions);
                if (attempt is not null)
                {
                    all.Add(attempt);
                }
            }
            catch (JsonException)
            {
            }
        }
        return all
            .Where(x => profileId is null || x.ProfileId == profileId)
            .OrderByDescending(x => x.At)
            .Take(Math.Max(1, limit))
            .ToList();
    }


    private static void CompactIfNeeded(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length <= CompactThreshold)
        {
            return;
        }
        File.WriteAllLines(path, lines[^KeepLines..]);
    }


    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

}
