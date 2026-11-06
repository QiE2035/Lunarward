// Lunarward：Linux 托盘（org.kde.StatusNotifierItem + com.canonical.dbusmenu）。
//
// 上游用 H.NotifyIcon 的 TaskbarIcon（Win32 Shell_NotifyIcon）。Linux 桌面没有该 API，
// 现代 KDE/GNOME 走 StatusNotifierItem（G0-6 已在 KDE 上验证通路）。
// 这里只做 M2 需要的最小集合：托盘图标 + 单击唤回主窗口 + 右键菜单（显示主窗口/退出）。
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tmds.DBus;
using Starward.Features.ViewHost;

namespace Starward.Features.Tray;

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
    // KDE 的 plasmashell 用 Properties.GetAll 一次性拉全部属性（实测只发 GetAll，不发逐个 Get）；
    // 少了这条，宿主拿不到 Menu/IconName/IconPixmap → 右键菜单不出现、托盘图标显示成占位。
    Task<IDictionary<string, object>> GetAllAsync();
    Task SetAsync(string prop, object val);
    Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler);
    Task ActivateAsync(int x, int y);
    Task SecondaryActivateAsync(int x, int y);
    Task ContextMenuAsync(int x, int y);
    Task ScrollAsync(int delta, string orientation);
}

[DBusInterface("com.canonical.dbusmenu")]
public interface IDBusMenu : IDBusObject
{
    // D-Bus 签名是 GetLayout(i i as)：parentId 必须是 int。
    // 之前写成 uint，序列化出来是 "uias"，plasmashell 匹配不到 → 从不调用，菜单永远为空。
    Task<(uint revision, (int id, IDictionary<string, object> properties, object[] children) layout)> GetLayoutAsync(int parentId, int recursionDepth, string[] propertyNames);
    Task EventAsync(int id, string eventId, object data, uint timestamp);
    Task AboutToShowAsync(int id);
    Task<object> GetAsync(string prop);
    Task<IDictionary<string, object>> GetAllAsync();
}

internal sealed class NullDisposable : IDisposable
{
    public static readonly NullDisposable Instance = new();
    public void Dispose() { }
}


internal sealed class TrayMenu : IDBusMenu
{
    private readonly Action _show;
    private readonly Action _quit;
    private readonly Action _openSettings;
    private readonly Action _openScreenshotFolder;

    public TrayMenu(Action show, Action quit, Action openSettings, Action openScreenshotFolder)
    {
        _show = show;
        _quit = quit;
        _openSettings = openSettings;
        _openScreenshotFolder = openScreenshotFolder;
    }

    public ObjectPath ObjectPath => new("/MenuBar");

    public Task<(uint, (int, IDictionary<string, object>, object[]))> GetLayoutAsync(int parentId, int recursionDepth, string[] propertyNames)
    {
        Serilog.Log.Information("[tray] GetLayout parent={parent} depth={depth}", parentId, recursionDepth);

        IDictionary<string, object> Root() => new Dictionary<string, object> { ["children-display"] = "submenu" };
        (int, IDictionary<string, object>, object[]) Item(int id, string label, bool enabled = true)
            => (id, new Dictionary<string, object>
            {
                ["label"] = label,
                ["enabled"] = enabled,
                ["visible"] = true,
                ["type"] = "standard",
            }, Array.Empty<object>());

        var children = new object[]
        {
            Item(1, "显示主窗口"),
            Item(3, "打开设置"),
            Item(4, "打开截图文件夹"),
            Item(2, "退出 Lunarward"),
        };
        return Task.FromResult<(uint, (int, IDictionary<string, object>, object[]))>((1u, (0, Root(), children)));
    }

    public Task EventAsync(int id, string eventId, object data, uint timestamp)
    {
        Serilog.Log.Information("[tray] menu Event id={id} event={event} data={data}", id, eventId, data);
        if (eventId is "clicked")
        {
            switch (id)
            {
                case 1:
                    _show();
                    break;
                case 2:
                    _quit();
                    break;
                case 3:
                    _openSettings();
                    break;
                case 4:
                    _openScreenshotFolder();
                    break;
            }
        }
        return Task.CompletedTask;
    }

    public Task AboutToShowAsync(int id)
    {
        Serilog.Log.Information("[tray] menu AboutToShow id={id}", id);
        return Task.CompletedTask;
    }

    public Task<IDictionary<string, object>> GetAllAsync() => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>
    {
        ["Version"] = 3u,
        ["TextDirection"] = "ltr",
        ["Status"] = "normal",
        ["IconThemePath"] = Array.Empty<string>(),
    });


    public Task<object> GetAsync(string prop) => Task.FromResult(prop switch
    {
        "Version" => (object)3u,
        "TextDirection" => "ltr",
        "Status" => "normal",
        "IconThemePath" => Array.Empty<string>(),
        _ => (object)"",
    });

    public Task SetAsync(string prop, object val) => Task.CompletedTask;
    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler) => Task.FromResult<IDisposable>(NullDisposable.Instance);
}


internal sealed class SniObject : IStatusNotifierItem
{
    private readonly Action _activate;
    private readonly Action _menu;

    public SniObject(Action activate, Action menu)
    {
        _activate = activate;
        _menu = menu;
    }

    public ObjectPath ObjectPath => new("/StatusNotifierItem");

    public Task<IDictionary<string, object>> GetAllAsync() => Task.FromResult<IDictionary<string, object>>(new Dictionary<string, object>
    {
        ["Category"] = "ApplicationStatus",
        ["Id"] = "lunarward",
        ["Title"] = "Lunarward",
        ["Status"] = "Active",
        // IconName 留空：开发运行没把图标装进 XDG icon theme，KDE 解析不到名字就显示成占位方块；
        // 参考微信托盘（IconName 为空 + 只给 IconPixmap），这里同样只依赖 pixmap。
        ["IconName"] = "",
        ["IconPixmap"] = TrayIconPixmap.Load(),
        ["IconThemePath"] = "",
        // ItemIsMenu=true 时左键也走菜单；KDE 下菜单打不开就变成「左右键都没反应」。
        // 改成 false：左键 = Activate（显示主窗口），右键 = 菜单。
        ["ItemIsMenu"] = false,
        ["Menu"] = new ObjectPath("/MenuBar"),
        ["WindowId"] = 0,
    });


    public Task<object> GetAsync(string prop) => Task.FromResult(prop switch
    {
        "Category" => (object)"ApplicationStatus",
        "Id" => "lunarward",
        "Title" => "Lunarward",
        "Status" => "Active",
        "IconName" => "",
        "IconPixmap" => TrayIconPixmap.Load(),
        "IconThemePath" => "",
        "ItemIsMenu" => false,
        "Menu" => new ObjectPath("/MenuBar"),
        "WindowId" => 0,
        _ => (object)"",
    });

    public Task SetAsync(string prop, object val) => Task.CompletedTask;
    public Task<IDisposable> WatchPropertiesAsync(Action<PropertyChanges> handler) => Task.FromResult<IDisposable>(NullDisposable.Instance);
    public Task ActivateAsync(int x, int y)
    {
        Serilog.Log.Information("[tray] Activate ({x},{y})", x, y);
        _activate();
        return Task.CompletedTask;
    }
    public Task SecondaryActivateAsync(int x, int y)
    {
        Serilog.Log.Information("[tray] SecondaryActivate ({x},{y})", x, y);
        _activate();
        return Task.CompletedTask;
    }
    public Task ContextMenuAsync(int x, int y)
    {
        // 宿主找不到 dbusmenu 时会退化成调这个方法（plasmashell：
        // "Could not find DBusMenu interface, falling back to calling ContextMenu()"）。
        // 宿主里画不了我们自己的菜单，至少把主窗口叫出来，别让右键彻底没反应。
        Serilog.Log.Information("[tray] ContextMenu ({x},{y})", x, y);
        _menu();
        return Task.CompletedTask;
    }
    public Task ScrollAsync(int delta, string orientation) => Task.CompletedTask;
}


public static class StatusNotifierTray
{
    private static Connection? _connection;
    private static string? _serviceName;

    public static bool IsRunning => _connection is not null;

    /// <summary>托盘是否真的注册成功（决定了「关闭窗口」能不能安全地降级成隐藏到托盘）。</summary>
    public static bool Available { get; private set; }

    /// <summary>启动托盘。失败只记录日志并返回 false，不影响主窗口（托盘是增强项）。</summary>
    public static async Task<bool> StartAsync(ILogger? logger = null)
    {
        if (_connection is not null)
        {
            return Available;
        }
        try
        {
            var connection = new Connection(Address.Session!);
            await connection.ConnectAsync();
            var menu = new TrayMenu(ShowMainWindow, QuitApplication, OpenSettings, OpenScreenshotFolder);
            var sni = new SniObject(ShowMainWindow, ShowMainWindow);
            await connection.RegisterObjectAsync(menu);
            await connection.RegisterObjectAsync(sni);
            string serviceName = $"org.kde.StatusNotifierItem-{Environment.ProcessId}-1";
            await connection.RegisterServiceAsync(serviceName);
            var watcher = connection.CreateProxy<IStatusNotifierWatcher>("org.kde.StatusNotifierWatcher", "/StatusNotifierWatcher");
            await watcher.RegisterStatusNotifierItemAsync(serviceName);
            _connection = connection;
            _serviceName = serviceName;
            Available = true;
            logger?.LogInformation("托盘已注册：{Service}", serviceName);
            return true;
        }
        catch (Exception ex)
        {
            Available = false;
            logger?.LogWarning(ex, "托盘注册失败（非致命）：没有 StatusNotifierHost 时关闭窗口将直接退出");
            return false;
        }
    }


    public static void Stop()
    {
        try
        {
            if (_connection is not null && _serviceName is not null)
            {
                _connection.UnregisterServiceAsync(_serviceName).GetAwaiter().GetResult();
                _connection.Dispose();
            }
        }
        catch { }
        _connection = null;
        _serviceName = null;
        Available = false;
    }


    private static void OpenSettings()
    {
        ShowMainWindow();
        Features.ViewHost.MainWindow.Current?.DispatcherQueue.TryEnqueue(() => Features.ViewHost.MainWindow.Current?.OpenSettings());
    }


    private static void OpenScreenshotFolder()
    {
        string folder = Features.Screenshot.ScreenCaptureLinux.DefaultFolder;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", $"\"{folder}\"") { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Open screenshot folder failed");
        }
    }


    private static void ShowMainWindow()
    {
        try
        {
            var window = MainWindow.Current;
            if (window is null)
            {
                return;
            }
            window.DispatcherQueue.TryEnqueue(() =>
            {
                // Uno 的 Show/Activate 对「已隐藏/最小化（unmap）」的窗口不生效（L-84/D47），
                // 用 Helpers.X11Window（libxcb map + _NET_ACTIVE_WINDOW）真正把它叫回来。
                if (!Helpers.X11Window.Show(window.Title))
                {
                    window.AppWindow.Show();
                }
                window.Activate();
            });
        }
        catch { }
    }


    private static void QuitApplication()
    {
        try
        {
            MainWindow.Current?.DispatcherQueue.TryEnqueue(() => App.Current.Exit());
        }
        catch { }
    }
}


/// <summary>
/// 托盘图标。SNI 要求 ARGB32 大端像素数组，用 SkiaSharp 把内置 logo 缩到 22px 生成一次。
/// </summary>
internal static class TrayIconPixmap
{
    private static (int, int, byte[])[]? _cache;

    public static (int, int, byte[])[] Load()
    {
        if (_cache is not null)
        {
            return _cache;
        }
        try
        {
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "lunarward.png");
            if (!System.IO.File.Exists(path))
            {
                return _cache = Array.Empty<(int, int, byte[])>();
            }
            using var bitmap = SkiaSharp.SKBitmap.Decode(path);
            if (bitmap is null)
            {
                return _cache = Array.Empty<(int, int, byte[])>();
            }
            using var resized = bitmap.Resize(new SkiaSharp.SKImageInfo(22, 22), SkiaSharp.SKFilterQuality.High);
            var bytes = new byte[22 * 22 * 4];
            int i = 0;
            for (int y = 0; y < 22; y++)
            {
                for (int x = 0; x < 22; x++)
                {
                    var c = resized.GetPixel(x, y);
                    // SNI 的 IconPixmap 是 ARGB32，网络字节序（大端）
                    bytes[i++] = c.Alpha;
                    bytes[i++] = c.Red;
                    bytes[i++] = c.Green;
                    bytes[i++] = c.Blue;
                }
            }
            _cache = new[] { (22, 22, bytes) };
        }
        catch
        {
            _cache = Array.Empty<(int, int, byte[])>();
        }
        return _cache;
    }
}
