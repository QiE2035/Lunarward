// Lunarward：桌面通知（Linux 走 D-Bus org.freedesktop.Notifications，24 号 P2）。
//
// 上游用 Win32 的 Toast 通知；Linux 上没有 WinRT toast，标准做法是桌面通知规范。
// 这里**直接走 D-Bus**（Tmds.DBus 已是项目依赖），不再 fork notify-send：
// fork 会违反硬规则 10（UI 线程不 fork 外部进程），而且 notify-send 并非所有桌面都有。
// D-Bus 失败时才退回 notify-send / gdbus，两个都没有就只写日志。
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;

namespace Starward.Helpers;


[DBusInterface("org.freedesktop.Notifications")]
public interface IDesktopNotifications : IDBusObject
{
    Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body,
        string[] actions, IDictionary<string, object> hints, int expireTimeout);

    Task CloseNotificationAsync(uint id);

    Task<string[]> GetCapabilitiesAsync();
}


internal static class DesktopNotification
{
    private static readonly ObjectPath NotificationsPath = new("/org/freedesktop/Notifications");
    private const string Service = "org.freedesktop.Notifications";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);


    /// <summary>发一条桌面通知。不会抛异常，失败只写日志。</summary>
    public static void Show(string title, string body, string? icon = "lunarward", int timeoutMs = 6000)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        // 后台跑：D-Bus 往返可能几十毫秒，但绝不能在 UI 线程上等
        _ = Task.Run(async () =>
        {
            if (await TrySendAsync(title, body, icon, timeoutMs).ConfigureAwait(false))
            {
                return;
            }
            FallbackToProcess(title, body, icon, timeoutMs);
        });
    }


    private static async Task<bool> TrySendAsync(string title, string body, string? icon, int timeoutMs)
    {
        try
        {
            using Connection connection = new(Address.Session);
            await connection.ConnectAsync().ConfigureAwait(false);
            IDesktopNotifications proxy = connection.CreateProxy<IDesktopNotifications>(Service, NotificationsPath);
            var hints = new Dictionary<string, object>
            {
                ["desktop-entry"] = "lunarward",
            };
            Task<uint> notify = proxy.NotifyAsync("Lunarward", 0, icon ?? "", title, body,
                Array.Empty<string>(), hints, timeoutMs);
            Task finished = await Task.WhenAny(notify, Task.Delay(Timeout)).ConfigureAwait(false);
            if (finished != notify)
            {
                Serilog.Log.Debug("[notify] D-Bus 超时（{Timeout} ms）", (int)Timeout.TotalMilliseconds);
                return false;
            }
            await notify.ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[notify] D-Bus 通知失败，退回外部命令");
            return false;
        }
    }


    /// <summary>兜底：notify-send，再不行用 gdbus。只在 D-Bus 直连失败时走到这里。</summary>
    private static void FallbackToProcess(string title, string body, string? icon, int timeoutMs)
    {
        try
        {
            string? notifySend = FindExecutable("notify-send");
            if (notifySend is not null)
            {
                var psi = new System.Diagnostics.ProcessStartInfo(notifySend)
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
                using var process = System.Diagnostics.Process.Start(psi);
                return;
            }

            string? gdbus = FindExecutable("gdbus");
            if (gdbus is not null)
            {
                var psi = new System.Diagnostics.ProcessStartInfo(gdbus) { UseShellExecute = false };
                psi.ArgumentList.Add("call");
                psi.ArgumentList.Add("--session");
                psi.ArgumentList.Add("--dest");
                psi.ArgumentList.Add(Service);
                psi.ArgumentList.Add("--object-path");
                psi.ArgumentList.Add("/org/freedesktop/Notifications");
                psi.ArgumentList.Add("--method");
                psi.ArgumentList.Add("org.freedesktop.Notifications.Notify");
                psi.ArgumentList.Add("Lunarward");
                psi.ArgumentList.Add("0");
                psi.ArgumentList.Add("");
                psi.ArgumentList.Add(title);
                psi.ArgumentList.Add(body);
                psi.ArgumentList.Add("[]");
                psi.ArgumentList.Add("{}");
                psi.ArgumentList.Add(timeoutMs.ToString());
                using var process = System.Diagnostics.Process.Start(psi);
                return;
            }

            Serilog.Log.Information("[notify] 没有可用的通知后端：{Title} — {Body}", title, body);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[notify] 通知发送失败");
        }
    }


    private static string? FindExecutable(string name)
    {
        try
        {
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            foreach (string dir in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = System.IO.Path.Combine(dir, name);
                if (System.IO.File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[notify] 查找 {Name} 失败", name);
        }
        return null;
    }
}
