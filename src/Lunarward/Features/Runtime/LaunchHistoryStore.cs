// Lunarward：方案启动历史（A 项优化的数据源）。
//
// 为什么另写一份而不是解析 logs/launch/*.json：那些报告只在「真的走到 Process.Start」时写，
// 预检失败根本不会留下文件，而用户最想看的恰恰是「为什么这次没起来」。
// 这里用 JSONL 追加，一次启动一行，读完按时间倒序给页面用。
using Microsoft.Extensions.Logging;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
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
        // R112：自定义格式也要显式 InvariantCulture——th-TH 的佛历会把年份算成 2569。
        string when = At.LocalDateTime.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
        string took = DurationMs is { } ms ? $" · {FormatDuration(ms)}" : "";
        string detail = string.IsNullOrWhiteSpace(Detail) ? "" : $" · {Detail}";
        return $"{when} {Outcome}{took}{detail}";
    }


    internal static string FormatDuration(long ms)
    {
        // R112：de-DE 下 `:0.0` 会给出 `1,5 s`，日志里再解析就错。
        if (ms < 1000)
        {
            return ms.ToString(CultureInfo.InvariantCulture) + " ms";
        }
        if (ms < 60_000)
        {
            return (ms / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }
        return (ms / 60_000).ToString(CultureInfo.InvariantCulture) + " 分 "
            + ((ms % 60_000) / 1000).ToString(CultureInfo.InvariantCulture) + " 秒";
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


    /// <summary>R112：进程内串行；跨进程用「独占句柄 + 短暂重试」，两个实例同时启动时不会互相截断。</summary>
    private static readonly object Gate = new();


    public static void Record(LaunchAttempt attempt, ILogger? logger = null)
    {
        try
        {
            string path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string line = JsonSerializer.Serialize(attempt, JsonOptions);
            lock (Gate)
            {
                AppendLine(path, line + "\n");
                CompactIfNeeded(path);
            }
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


    /// <summary>跨进程独占追加：拿不到句柄就等一小会儿重试（最多 ~300 ms），超时也不影响启动。</summary>
    private static void AppendLine(string path, string line)
    {
        byte[] bytes = System.Text.Encoding.UTF8.GetBytes(line);
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.None);
                stream.Write(bytes, 0, bytes.Length);
                return;
            }
            catch (IOException) when (attempt < 15)
            {
                System.Threading.Thread.Sleep(20);
            }
        }
    }


    private static void CompactIfNeeded(string path)
    {
        string[] lines = File.ReadAllLines(path);
        if (lines.Length <= CompactThreshold)
        {
            return;
        }
        // R42：截断也要原子写，否则崩在中间就是半份历史。
        JsonFile.WriteAtomic(path, string.Join(Environment.NewLine, lines[^KeepLines..]) + Environment.NewLine);
    }


    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

}
