// Lunarward：在某个方案的环境里跑一次程序（D64「在这个方案下运行」的第二形态）。
// 用途：不启动游戏，但要用**完全相同**的 WINEPREFIX / WINEDLLPATH / 环境变量跑一次补丁、
// 安装器或调试工具。与「伴随启动」共用 ProfileShellService.BuildEnvironment，避免环境漂移。
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;



namespace Starward.Features.Runtime;


public static class RunOnceService
{

    /// <summary>可执行文件是不是 Windows 程序（需要经 wine 启动）。</summary>
    public static bool IsWindowsExecutable(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".bat", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".msi", StringComparison.OrdinalIgnoreCase);
    }


    /// <summary>
    /// 组装启动参数（不启动）。Windows 程序走方案里的 wine，Linux 程序直接执行。
    /// </summary>
    public static ProcessStartInfo? CreateStartInfo(RuntimeProfile profile, string executable, string? arguments = null, string? workingDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
        {
            return null;
        }
        ProcessStartInfo info;
        if (IsWindowsExecutable(executable))
        {
            string? wine = PrefixManager.ExpandPath(profile.RunnerPath);
            if (string.IsNullOrWhiteSpace(wine) || !File.Exists(wine))
            {
                return null;
            }
            info = new ProcessStartInfo(wine) { UseShellExecute = false };
            info.ArgumentList.Add(executable);
        }
        else
        {
            info = new ProcessStartInfo(executable) { UseShellExecute = false };
        }
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            // 参数交给 shell 语义的调用方自己拆：这里按空格切分，支持引号包裹（够用且可预期）
            foreach (string arg in SplitArguments(arguments))
            {
                info.ArgumentList.Add(arg);
            }
        }
        info.WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? Path.GetDirectoryName(executable) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : workingDirectory;
        foreach ((string key, string value) in ProfileShellService.BuildEnvironment(profile))
        {
            info.Environment[key] = value;
        }
        return info;
    }


    /// <summary>启动并立刻返回进程；输出重定向到日志目录，避免工具的输出没人看。</summary>
    public static Process? Start(RuntimeProfile profile, string executable, string? arguments = null, ILogger? logger = null)
    {
        ProcessStartInfo? info = CreateStartInfo(profile, executable, arguments);
        if (info is null)
        {
            logger?.LogWarning("Run once refused: {exe} (runner missing or file not found)", executable);
            return null;
        }
        Process process = Process.Start(info) ?? throw new InvalidOperationException("Process.Start returned null");
        logger?.LogInformation("Run once started: {exe} args={args} pid={pid}", executable, arguments ?? "", process.Id);
        return process;
    }


    /// <summary>按空格切分参数，支持 "…" 与 '…' 包裹（不做转义解释，避免半吊子 shell 解析）。</summary>
    internal static List<string> SplitArguments(string text)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        char quote = '\0';
        foreach (char c in text)
        {
            if (quote != '\0')
            {
                if (c == quote)
                {
                    quote = '\0';
                }
                else
                {
                    current.Append(c);
                }
                continue;
            }
            if (c is '"' or '\'')
            {
                quote = c;
                continue;
            }
            if (char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }
            current.Append(c);
        }
        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }
        return result;
    }

}
