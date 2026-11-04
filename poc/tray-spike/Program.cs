// G0-6 尖峰：在 KDE 上验证 StatusNotifierItem 托盘宿主 + org.freedesktop.Notifications 通知。
// 只验证「协议通路」，不追求完整托盘菜单（完整实现见 M2 的 ITrayService）。
using System.Diagnostics;
using Tmds.DBus;

namespace Lunarward.TraySpike;

[DBusInterface("org.kde.StatusNotifierWatcher")]
public interface IStatusNotifierWatcher : IDBusObject
{
    Task RegisterStatusNotifierItemAsync(string service);
    Task<object> GetAsync(string prop);
}

[DBusInterface("org.kde.StatusNotifierItem")]
public interface IStatusNotifierItem : IDBusObject
{
    Task<object> GetAsync(string prop);
    Task SetAsync(string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    Task ActivateAsync(int x, int y);
    Task SecondaryActivateAsync(int x, int y);
    Task ContextMenuAsync(int x, int y);
    Task ScrollAsync(int delta, string orientation);
}

[DBusInterface("org.freedesktop.Notifications")]
public interface INotifications : IDBusObject
{
    Task<uint> NotifyAsync(string appName, uint replacesId, string appIcon, string summary, string body,
                            string[] actions, IDictionary<string, object> hints, int expireTimeout);
    Task CloseNotificationAsync(uint id);
}

internal sealed class NullDisposable : IDisposable
{
    public static readonly NullDisposable Instance = new();
    public void Dispose() { }
}

internal sealed class SniObject : IStatusNotifierItem
{
    public ObjectPath ObjectPath => new("/StatusNotifierItem");
    public Task<object> GetAsync(string prop) => Task.FromResult(prop switch
    {
        "Category" => (object)"ApplicationStatus",
        "Id" => "lunarward-poc",
        "Title" => "Lunarward PoC",
        "Status" => "Active",
        "IconName" => "applications-games",
        "IconThemePath" => "",
        "ItemIsMenu" => false,
        "Menu" => new ObjectPath("/NO_DBUSMENU"),
        "WindowId" => 0,
        _ => "",
    });
    public Task SetAsync(string prop, object val) => Task.CompletedTask;
    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler) => Task.FromResult<IDisposable>(NullDisposable.Instance);
    public Task ActivateAsync(int x, int y) { Console.WriteLine("  [托盘] Activate 被调用"); return Task.CompletedTask; }
    public Task SecondaryActivateAsync(int x, int y) { Console.WriteLine("  [托盘] SecondaryActivate 被调用"); return Task.CompletedTask; }
    public Task ContextMenuAsync(int x, int y) { Console.WriteLine("  [托盘] ContextMenu 被调用"); return Task.CompletedTask; }
    public Task ScrollAsync(int delta, string orientation) => Task.CompletedTask;
}

internal static class Program
{
    private static async Task<int> Main()
    {
        var conn = new Connection(Address.Session!);
        await conn.ConnectAsync();
        Console.WriteLine("[1] 已连接会话总线");

        // --- 托盘：注册 StatusNotifierItem ---
        var sni = new SniObject();
        await conn.RegisterObjectAsync(sni);
        var serviceName = $"org.kde.StatusNotifierItem-{Environment.ProcessId}-1";
        await conn.RegisterServiceAsync(serviceName);
        Console.WriteLine($"[2] 已导出 SNI 对象 /StatusNotifierItem，服务名 {serviceName}");

        var watcher = conn.CreateProxy<IStatusNotifierWatcher>("org.kde.StatusNotifierWatcher", "/StatusNotifierWatcher");
        await watcher.RegisterStatusNotifierItemAsync(serviceName);
        Console.WriteLine("[3] 已向 StatusNotifierWatcher 注册");

        await Task.Delay(800);
        var registered = await watcher.GetAsync("RegisteredStatusNotifierItems");
        var list = (registered as IEnumerable<object>)?.Select(o => o.ToString()).ToArray()
                   ?? (registered is string s ? new[] { s } : Array.Empty<string>());
        var hits = list.Count(x => x != null && x.Contains(serviceName));
        Console.WriteLine($"[4] Watcher 当前条目({list.Length}): {string.Join(", ", list.Select(Shorten))}");
        Console.WriteLine(hits > 0 ? "[4] 托盘注册: PASS" : "[4] 托盘注册: FAIL（watcher 列表中找不到本服务）");

        // --- 通知 ---
        var notifications = conn.CreateProxy<INotifications>("org.freedesktop.Notifications", "/org/freedesktop/Notifications");
        var id = await notifications.NotifyAsync("Lunarward", 0, "applications-games",
            "Lunarward G0-6", "托盘与通知通路验证：这是一条测试通知。",
            Array.Empty<string>(), new Dictionary<string, object>(), 4000);
        Console.WriteLine($"[5] 通知已发送，id={id}: {(id > 0 ? "PASS" : "FAIL")}");

        Console.WriteLine("[6] 保持 6 秒以便观察托盘图标与通知…");
        await Task.Delay(6000);

        await notifications.CloseNotificationAsync(id);
        await conn.UnregisterServiceAsync(serviceName);
        conn.Dispose();

        var ok = hits > 0 && id > 0;
        Console.WriteLine(ok ? "\nG0-6(KDE): PASS" : "\nG0-6(KDE): FAIL");
        return ok ? 0 : 1;

        static string Shorten(string? s) => s is null ? "" : (s.Length > 60 ? s[..60] + "…" : s);
    }
}
