// Lunarward：进程输出捕获（D27）。
//
// 约定（写进 12-shims-and-gaps.md）：
//   * 捕获**永远开着**：游戏/后启动程序的 stdout+stderr 一律进环形缓冲并落日志文件，
//     否则崩溃时什么都拿不到（Windows 下上游也是靠日志，不是靠控制台）。
//   * 可见性级别只控制「显示」，不控制「是否捕获」：
//       silent  —— 只落文件，用户看不到；
//       panel   —— 额外显示在启动器里的输出面板（M2 先提供 CLI/日志，面板 UI 在 M3）；
//       terminal—— 直接用终端跑，字符走终端自己的管道（D27 要求 `-e` 直 exec，不套 shell）。
//   * 脱敏：命中 (?i)(token|secret|cookie|auth) 的行在写面板前打码，落盘保持原样以便排错。
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Starward.Features.Runtime;

public enum OutputVisibility
{
    Silent = 0,
    Panel = 1,
    Terminal = 2,
}

/// <summary>一条被捕获的输出行。</summary>
public sealed record OutputLine(DateTime Time, string Scope, string Text, bool IsError);

public static class OutputCapture
{
    private const int Capacity = 5000;

    private static readonly ConcurrentQueue<OutputLine> _lines = new();
    private static readonly Regex _secret = new(@"(?i)(token|secret|cookie|auth)", RegexOptions.Compiled);
    private static readonly object _fileLock = new();

    /// <summary>新增一行；Scope 用 "game:hk4e_cn" / "companion:ab12cd34" 这种形式，便于按来源过滤。</summary>
    public static void Append(string scope, string text, bool isError = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        var line = new OutputLine(DateTime.Now, scope, text, isError);
        _lines.Enqueue(line);
        while (_lines.Count > Capacity && _lines.TryDequeue(out _))
        {
        }
        AppendToFile(line);
    }

    public static IReadOnlyList<OutputLine> Recent(int count = 200, string? scope = null)
    {
        IEnumerable<OutputLine> query = _lines;
        if (!string.IsNullOrWhiteSpace(scope))
        {
            query = query.Where(x => x.Scope.Equals(scope, StringComparison.OrdinalIgnoreCase));
        }
        return query.Reverse().Take(Math.Clamp(count, 1, Capacity)).Reverse().ToArray();
    }

    /// <summary>给面板用的文本：命中敏感词的行打码。</summary>
    public static string Redact(string text) => _secret.IsMatch(text) ? _secret.Replace(text, "$1=***") : text;

    private static void AppendToFile(OutputLine line)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "lunarward", "logs", "output");
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, $"{DateTime.Now:yyyyMMdd}.log");
            lock (_fileLock)
            {
                File.AppendAllText(file, $"{line.Time:HH:mm:ss.fff} [{line.Scope}]{(line.IsError ? " [err]" : "")} {line.Text}{Environment.NewLine}");
            }
        }
        catch
        {
            // 日志写不进去不能拖垮游戏启动。
        }
    }

    /// <summary>把进程的 stdout/stderr 接到捕获器上（异步逐行读）。</summary>
    public static void Attach(string scope, System.Diagnostics.Process process)
    {
        process.OutputDataReceived += (_, e) => Append(scope, e.Data ?? string.Empty);
        process.ErrorDataReceived += (_, e) => Append(scope, e.Data ?? string.Empty, isError: true);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
    }

    public static OutputVisibility ParseVisibility(string? value) => value?.ToLowerInvariant() switch
    {
        "panel" => OutputVisibility.Panel,
        "terminal" => OutputVisibility.Terminal,
        _ => OutputVisibility.Silent,
    };
}
