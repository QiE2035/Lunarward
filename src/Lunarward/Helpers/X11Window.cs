// Lunarward：主窗口的隐藏 / 显示。
//
// 为什么不用 WinUI 的 Window.Hide() / AppWindow.Hide()：
// 实测 Uno 6.7.135 的 X11 后端里两者都是空操作——调用后窗口仍是 Map State: IsViewable
// （见 docs/linux-port/13-lessons.md L-84）。而 OverlappedPresenter.Restore() 也不会把窗口重新 map。
// 因此这里直接对 X11 窗口做 unmap / map：xdotool 作为外部进程执行，避免在进程内再开一个
// Xlib 连接（L-45：Xlib 在 Uno 进程内做窗口操作容易互相踩）。
//
// 窗口定位用 pid + 标题：AppWindow.Id 在 Linux 上没有映射到 X11 window id（实测恒为 0x1），
// 只有 _NET_WM_PID + 标题是稳定可查的。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Helpers;

internal static class X11Window
{

    /// <summary>把标题为 <paramref name="title"/> 的窗口从屏幕上移除（unmap，不进任务栏）。</summary>
    public static bool Hide(string title)
    {
        if (!TryFindWindow(title, out string id))
        {
            Log($"hide: window not found ({title})");
            return false;
        }
        if (!TryRun("xdotool", "windowunmap", id))
        {
            return false;
        }
        Log($"hide: unmapped {id}");
        return true;
    }


    /// <summary>把窗口映射回屏幕并激活。返回 false 表示找不到窗口或 xdotool 失败。</summary>
    public static bool Show(string title)
    {
        if (!TryFindWindow(title, out string id))
        {
            Log($"show: window not found ({title})");
            return false;
        }
        // windowmap 之后必须 activate，否则焦点还在别的窗口上（KDE 下表现为「窗口出来了但点不动」）。
        if (!TryRun("xdotool", "windowmap", id))
        {
            return false;
        }
        TryRun("xdotool", "windowactivate", id);
        Log($"show: mapped {id}");
        return true;
    }


    public static bool IsMapped(string title)
    {
        if (!TryFindWindow(title, out string id))
        {
            return false;
        }
        string? output = Run("xwininfo", "-id", id);
        return output is not null && output.Contains("IsViewable", StringComparison.Ordinal);
    }


    /// <summary>按「进程 id + 精确标题」查找 X11 window id。</summary>
    private static bool TryFindWindow(string title, out string id)
    {
        id = "";
        string? output = Run("xdotool", "search", "--pid", Environment.ProcessId.ToString(), "--name", "^" + System.Text.RegularExpressions.Regex.Escape(title) + "$");
        if (string.IsNullOrWhiteSpace(output))
        {
            return false;
        }
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Length > 0 && line.All(char.IsAsciiDigit))
            {
                id = line;
                return true;
            }
        }
        return false;
    }


    private static bool TryRun(string file, params string[] arguments) => Run(file, arguments) is not null;


    private static string? Run(string file, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo(file)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }
            using Process process = Process.Start(info)!;
            // stderr 必须同时读，否则 xdotool 输出多时会把父进程卡在 WaitForExit 上。
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(5000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // 进程可能自己退出了。
                }
                Log($"{file}: timeout");
                return null;
            }
            string output = stdout.GetAwaiter().GetResult();
            string error = stderr.GetAwaiter().GetResult();
            if (process.ExitCode != 0)
            {
                Log($"{file} {string.Join(' ', arguments)}: exit {process.ExitCode} {error.Trim()}");
                return null;
            }
            return output;
        }
        catch (Exception ex)
        {
            // 没装 xdotool 或没有 DISPLAY 时走到这里：调用方退回 WinUI 的（无效但无害的）实现。
            Log($"{file}: {ex.GetType().Name} {ex.Message}");
            return null;
        }
    }


    private static void Log(string message) => Serilog.Log.Information("[x11window] {message}", message);

}
