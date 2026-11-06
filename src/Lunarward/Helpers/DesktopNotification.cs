// Lunarward：桌面通知（Linux 走 org.freedesktop.Notifications）。
//
// 上游用 Win32 的 Toast 通知；Linux 桌面上没有 WinRT toast，标准做法是 D-Bus 通知规范。
// 这里优先调用 notify-send（libnotify 自带，几乎所有发行版都有），
// 没有的话退回 gdbus 直接调 org.freedesktop.Notifications.Notify。
// 两者都没有时只记日志，不影响主流程。
using System;
using System.Diagnostics;
using System.IO;

namespace Starward.Helpers;

internal static class DesktopNotification
{

    /// <summary>发一条桌面通知。不会抛异常，失败只写日志。</summary>
    public static void Show(string title, string body, string? icon = "lunarward", int timeoutMs = 6000)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            string? notifySend = FindExecutable("notify-send");
            if (notifySend is not null)
            {
                var psi = new ProcessStartInfo(notifySend)
                {
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
                psi.ArgumentList.Add("-a");
                psi.ArgumentList.Add("Lunarward");
                if (!string.IsNullOrWhiteSpace(icon))
                {
                    psi.ArgumentList.Add("-i");
                    psi.ArgumentList.Add(icon!);
                }
                psi.ArgumentList.Add("-t");
                psi.ArgumentList.Add(timeoutMs.ToString());
                psi.ArgumentList.Add(title);
                psi.ArgumentList.Add(body);
                using Process? process = Process.Start(psi);
                process?.WaitForExit(3000);
                return;
            }

            string? gdbus = FindExecutable("gdbus");
            if (gdbus is not null)
            {
                var psi = new ProcessStartInfo(gdbus) { UseShellExecute = false };
                psi.ArgumentList.Add("call");
                psi.ArgumentList.Add("--session");
                psi.ArgumentList.Add("--dest");
                psi.ArgumentList.Add("org.freedesktop.Notifications");
                psi.ArgumentList.Add("--object-path");
                psi.ArgumentList.Add("/org/freedesktop/Notifications");
                psi.ArgumentList.Add("--method");
                psi.ArgumentList.Add("org.freedesktop.Notifications.Notify");
                psi.ArgumentList.Add("Lunarward");
                psi.ArgumentList.Add("0");
                psi.ArgumentList.Add(icon ?? "");
                psi.ArgumentList.Add(title);
                psi.ArgumentList.Add(body);
                psi.ArgumentList.Add("[]");
                psi.ArgumentList.Add("{}");
                psi.ArgumentList.Add(timeoutMs.ToString());
                using Process? process = Process.Start(psi);
                process?.WaitForExit(3000);
                return;
            }

            Serilog.Log.Debug("没有 notify-send 也没有 gdbus，跳过桌面通知：{Title}", title);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "桌面通知发送失败：{Title}", title);
        }
    }


    private static string? FindExecutable(string name)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        foreach (string dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch
            {
                // 忽略非法 PATH 项
            }
        }
        return null;
    }

}
