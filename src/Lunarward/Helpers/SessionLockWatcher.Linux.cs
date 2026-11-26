// Lunarward：锁屏/解锁通知（Linux，D135）。
//
// 上游 MainWindow 靠 Win32 的 WM_WTSSESSION_CHANGE 收锁屏事件，再通过 MainWindowStateChangedMessage
// 让背景层暂停视频。Linux 没有该消息，这里订阅两条最常见的通道并复用同一条消息：
//   1) org.freedesktop.ScreenSaver.ActiveChanged（KDE/GNOME 等桌面都实现了，参数是 bool active）；
//   2) org.freedesktop.login1 的 Session.Lock / Unlock（无桌面环境的会话，例如纯 X 或 Xvfb）。
// 两条都失败（没有 system bus、非 logind 桌面）只记日志：最坏情况退回「锁屏后视频继续播」。
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Starward.Features.ViewHost;
using System;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;

namespace Starward.Helpers;


[DBusInterface("org.freedesktop.ScreenSaver")]
public interface IScreenSaver : IDBusObject
{
    Task<IDisposable> WatchActiveChangedAsync(Action<bool> callback);
}


[DBusInterface("org.freedesktop.login1.Manager")]
public interface ILogin1Manager : IDBusObject
{
    Task<ObjectPath> GetSessionByPIDAsync(uint pid);

    Task<ObjectPath> GetSessionAsync(string sessionId);
}


[DBusInterface("org.freedesktop.login1.Session")]
public interface ILogin1Session : IDBusObject
{
    Task<IDisposable> WatchLockAsync(Action callback);

    Task<IDisposable> WatchUnlockAsync(Action callback);
}


internal static class SessionLockWatcher
{

    private const string Login1 = "org.freedesktop.login1";
    private const string ScreenSaver = "org.freedesktop.ScreenSaver";
    private static readonly ObjectPath Login1ManagerPath = new("/org/freedesktop/login1");
    private static readonly ObjectPath ScreenSaverPath = new("/org/freedesktop/ScreenSaver");


    /// <summary>订阅锁屏信号。不会阻塞调用线程，失败只写日志。</summary>
    public static void Start(ILogger? logger = null)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        _ = Task.Run(() => RunAsync(logger));
    }


    private static async Task RunAsync(ILogger? logger)
    {
        Task screenSaver = Task.Run(() => WatchScreenSaverAsync(logger));
        Task logind = Task.Run(() => WatchLogindAsync(logger));
        await Task.WhenAll(screenSaver, logind).ConfigureAwait(false);
    }


    private static async Task WatchScreenSaverAsync(ILogger? logger)
    {
        try
        {
            using Connection connection = new(Address.Session);
            await connection.ConnectAsync().ConfigureAwait(false);
            IScreenSaver proxy = connection.CreateProxy<IScreenSaver>(ScreenSaver, ScreenSaverPath);
            await proxy.WatchActiveChangedAsync(Send).ConfigureAwait(false);
            logger?.LogInformation("Watching org.freedesktop.ScreenSaver.ActiveChanged");
            await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Subscribe ScreenSaver.ActiveChanged failed");
        }
    }


    private static async Task WatchLogindAsync(ILogger? logger)
    {
        try
        {
            using Connection connection = new(Address.System);
            await connection.ConnectAsync().ConfigureAwait(false);
            ILogin1Manager manager = connection.CreateProxy<ILogin1Manager>(Login1, Login1ManagerPath);
            ObjectPath sessionPath;
            try
            {
                sessionPath = await manager.GetSessionByPIDAsync((uint)Environment.ProcessId).ConfigureAwait(false);
            }
            catch (DBusException)
            {
                // 嵌套会话（Xvfb、容器、或父进程不是会话 leader）里 PID 查不到，
                // 退回 XDG_SESSION_ID，最后才试 logind 的 "auto"。
                sessionPath = default;
                string? envSession = Environment.GetEnvironmentVariable("XDG_SESSION_ID");
                if (!string.IsNullOrWhiteSpace(envSession))
                {
                    try
                    {
                        sessionPath = await manager.GetSessionAsync(envSession).ConfigureAwait(false);
                    }
                    catch (DBusException)
                    {
                        sessionPath = default;
                    }
                }
                if (sessionPath == default)
                {
                    sessionPath = await manager.GetSessionAsync("auto").ConfigureAwait(false);
                }
            }
            ILogin1Session session = connection.CreateProxy<ILogin1Session>(Login1, sessionPath);
            await session.WatchLockAsync(() => Send(true)).ConfigureAwait(false);
            await session.WatchUnlockAsync(() => Send(false)).ConfigureAwait(false);
            logger?.LogInformation("Watching logind session lock on {path}", sessionPath.ToString());
            await Task.Delay(Timeout.Infinite).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Subscribe logind session lock failed");
        }
    }


    private static void Send(bool locked)
    {
        // 与上游 WM_WTSSESSION_CHANGE 的 WTS_SESSION_LOCK 分支同语义：SessionLock=true 时背景暂停
        WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage
        {
            SessionLock = locked,
            CurrentTime = DateTimeOffset.Now,
        });
    }

}
