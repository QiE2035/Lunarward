// Lunarward：「谁占着这个前缀」——预检失败时给用户可执行的答案（24 号 §4「顺手做」）。
//
// 以前 fork `fuser -v` / `lsof -t +D`：两者都不一定装了，而且 lsof 扫大树很慢。
// 现在直接读 /proc/<pid>/fd/* 的符号链接，按路径前缀匹配 —— 只读、不依赖外部命令、可取消。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Helpers;


public static class ProcessQuery
{

    /// <summary>
    /// 列出「打开着 <paramref name="path"/> 及其子路径」的进程。
    /// 返回多行文本（`PID 1234  firefox` 形式），没有命中返回空字符串。
    /// </summary>
    public static async Task<string> FindUsersAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }
        string normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        try
        {
            return await Task.Run(() => Scan(normalized, ct), ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return "";
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "ProcessQuery: 扫描 /proc 失败");
            return "";
        }
    }


    private static string Scan(string normalized, CancellationToken ct)
    {
        var lines = new List<string>();
        string prefix = normalized + "/";
        foreach (string processDir in Directory.EnumerateDirectories("/proc"))
        {
            ct.ThrowIfCancellationRequested();
            string name = Path.GetFileName(processDir);
            if (!int.TryParse(name, out int pid) || pid == Environment.ProcessId)
            {
                continue;
            }
            string fdDir = Path.Combine(processDir, "fd");
            IEnumerable<string> fds;
            try
            {
                fds = Directory.EnumerateFileSystemEntries(fdDir);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue; // 别的用户的进程读不到，跳过
            }
            string? hit = null;
            try
            {
                foreach (string fd in fds)
                {
                    string? target;
                    try
                    {
                        target = new FileInfo(fd).LinkTarget;
                    }
                    catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                    {
                        continue;
                    }
                    if (string.IsNullOrEmpty(target))
                    {
                        continue;
                    }
                    // 有的内核把已删除文件写成 "path (deleted)"
                    string trimmed = target.EndsWith(" (deleted)", StringComparison.Ordinal)
                        ? target[..^" (deleted)".Length]
                        : target;
                    if (string.Equals(trimmed, normalized, StringComparison.Ordinal)
                        || trimmed.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        hit = trimmed;
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "ProcessQuery: 检查 PID {Pid} 失败", pid);
            }
            if (hit is not null)
            {
                lines.Add($"PID {pid,-7} {ReadCommandLine(pid)}({hit})");
            }
        }
        return lines.Count == 0 ? "" : string.Join(Environment.NewLine, lines);
    }


    private static string ReadCommandLine(int pid)
    {
        try
        {
            string comm = File.ReadAllText($"/proc/{pid}/comm").Trim();
            return string.IsNullOrEmpty(comm) ? "?" : comm;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return "?";
        }
    }
}
