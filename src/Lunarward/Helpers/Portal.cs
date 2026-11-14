// Lunarward：xdg-desktop-portal 客户端（24 号 P2/P3/P4）。
//
// 为什么要 portal：Flatpak / 沙盒 / 纯 Wayland 会话里，直接 fork `xdg-open`、`kdialog`、`spectacle`
// 都可能被拦（沙盒没有宿主文件系统视图，Wayland 不允许任意截屏）。portal 是这些场景的官方通道，
// 桌面自己决定用什么 UI，我们只发请求。
//
// 所有调用都是 D-Bus 异步往返，不 fork 外部进程（硬规则 10）；
// 没有 portal 时返回 false/null，调用方退回原来的外部命令路径。
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus;

namespace Starward.Helpers;


[DBusInterface("org.freedesktop.portal.OpenURI")]
public interface IOpenUriPortal : IDBusObject
{
    Task<ObjectPath> OpenURIAsync(string parentWindow, string uri, IDictionary<string, object> options);
}


[DBusInterface("org.freedesktop.portal.FileChooser")]
public interface IFileChooserPortal : IDBusObject
{
    Task<ObjectPath> OpenFileAsync(string parentWindow, string title, IDictionary<string, object> options);

    Task<ObjectPath> SaveFileAsync(string parentWindow, string title, IDictionary<string, object> options);
}


[DBusInterface("org.freedesktop.portal.Screenshot")]
public interface IScreenshotPortal : IDBusObject
{
    Task<ObjectPath> ScreenshotAsync(string parentWindow, IDictionary<string, object> options);

    Task<ObjectPath> PickColorAsync(string parentWindow, IDictionary<string, object> options);
}


[DBusInterface("org.freedesktop.portal.Request")]
public interface IPortalRequest : IDBusObject
{
    Task CloseAsync();

    Task<IDisposable> WatchResponseAsync(Action<(uint Response, IDictionary<string, object> Results)> handler,
        Action<Exception>? onError = null);
}


/// <summary>portal 选文件的结果：<see cref="Handled"/> 为 true 表示 portal 已经答复（含用户取消），<see cref="Paths"/> 是选中的路径。</summary>
internal readonly record struct PortalPickResult(bool Handled, string[] Paths)
{
    public static PortalPickResult NotHandled => new(false, Array.Empty<string>());

    public static PortalPickResult FromPaths(string[] paths) => new(true, paths);
}


/// <summary>xdg-desktop-portal 的薄封装：打开链接 / 选文件 / 截屏。</summary>
internal static class Portal
{

    private const string Service = "org.freedesktop.portal.Desktop";
    private static readonly ObjectPath DesktopPath = new("/org/freedesktop/portal/desktop");
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromMinutes(5);
    private static readonly object Gate = new();
    private static Connection? _connection;
    private static int _unavailable;


    private static async Task<Connection?> GetConnectionAsync()
    {
        if (Volatile.Read(ref _unavailable) == 1)
        {
            return null;
        }
        Connection? existing = Volatile.Read(ref _connection);
        if (existing is not null)
        {
            return existing;
        }
        lock (Gate)
        {
            if (_connection is not null)
            {
                return _connection;
            }
        }
        try
        {
            var connection = new Connection(Address.Session);
            await connection.ConnectAsync().ConfigureAwait(false);
            lock (Gate)
            {
                _connection ??= connection;
            }
            return _connection;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[portal] 连不上会话总线");
            Interlocked.Exchange(ref _unavailable, 1);
            return null;
        }
    }


    /// <summary>
    /// 同步版「用桌面打开 URI」：只等到「请求被 portal 接受」（≤ <paramref name="timeoutMs"/>），
    /// 用户实际看到什么由桌面决定。返回 false 表示 portal 不可用，调用方退回外部命令。
    /// 之所以能同步：OpenURI 的方法调用本身是毫秒级的，真正的「已打开」响应在后台继续等。
    /// </summary>
    internal static bool TryOpenUri(string uri, int timeoutMs = 800)
    {
        try
        {
            Task<bool> dispatch = DispatchOpenUriAsync(uri);
            if (!dispatch.Wait(timeoutMs))
            {
                Serilog.Log.Debug("[portal] OpenURI 派发超时（{Timeout} ms）", timeoutMs);
                return false;
            }
            return dispatch.Result;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[portal] TryOpenUri 失败：{Uri}", uri);
            return false;
        }
    }


    private static async Task<bool> DispatchOpenUriAsync(string uri)
    {
        Connection? connection = await GetConnectionAsync().ConfigureAwait(false);
        if (connection is null)
        {
            return false;
        }
        IOpenUriPortal portal = connection.CreateProxy<IOpenUriPortal>(Service, DesktopPath);
        var options = new Dictionary<string, object>
        {
            ["handle_token"] = NewToken(),
            ["ask"] = false,
        };
        ObjectPath handle = await portal.OpenURIAsync("", uri, options).ConfigureAwait(false);
        // 后台等响应，只记日志（用户可能取消「用哪个程序打开」）
        _ = Task.Run(async () =>
        {
            try
            {
                (uint code, _) = await AwaitResponseAsync(connection, handle, CancellationToken.None).ConfigureAwait(false);
                if (code != 0)
                {
                    Serilog.Log.Information("[portal] OpenURI 被取消（response={Code}）：{Uri}", code, uri);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "[portal] OpenURI 响应处理失败");
            }
        });
        return true;
    }


    private static string NewToken() => "lw" + Guid.NewGuid().ToString("N")[..16];


    /// <summary>
    /// 用桌面默认程序打开 URI（http/https/文件都由桌面决定）。
    /// 返回 false 表示 portal 不可用或用户拒绝，调用方退回外部命令。
    /// </summary>
    public static async Task<bool> OpenUriAsync(string uri)
    {
        Connection? connection = await GetConnectionAsync().ConfigureAwait(false);
        if (connection is null || !Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }
        try
        {
            IOpenUriPortal portal = connection.CreateProxy<IOpenUriPortal>(Service, DesktopPath);
            var options = new Dictionary<string, object>
            {
                ["handle_token"] = NewToken(),
                ["ask"] = false,
            };
            // 本地文件/目录走 open-directory、open-uri 对 file:// 也接受
            ObjectPath handle = await portal.OpenURIAsync("", uri, options).ConfigureAwait(false);
            (uint code, _) = await AwaitResponseAsync(connection, handle, CancellationToken.None).ConfigureAwait(false);
            if (code != 0)
            {
                Serilog.Log.Information("[portal] OpenURI 被拒绝（response={Code}）：{Uri}", code, uri);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[portal] OpenURI 失败：{Uri}", uri);
            return false;
        }
    }


    /// <summary>
    /// 弹桌面自己的文件选择框。返回选中的绝对路径（可能多个）；取消或无 portal 返回 null。
    /// <paramref name="globs"/> 形如 "*.png"，为空表示不过滤。
    /// </summary>
    public static async Task<PortalPickResult> PickFilesAsync(string title, bool multiple, bool save,
        string? suggestedName = null, IReadOnlyList<string>? globs = null, string? acceptLabel = null,
        bool directory = false, CancellationToken ct = default)
    {
        Connection? connection = await GetConnectionAsync().ConfigureAwait(false);
        if (connection is null)
        {
            return PortalPickResult.NotHandled;
        }
        try
        {
            IFileChooserPortal portal = connection.CreateProxy<IFileChooserPortal>(Service, DesktopPath);
            var options = new Dictionary<string, object>
            {
                ["handle_token"] = NewToken(),
                ["multiple"] = multiple,
                ["modal"] = true,
            };
            if (directory)
            {
                options["directory"] = true;
            }
            if (globs is { Count: > 0 })
            {
                string label = string.IsNullOrWhiteSpace(acceptLabel) ? "支持的文件" : acceptLabel!;
                var patterns = new List<(uint, string)>();
                foreach (string glob in globs)
                {
                    patterns.Add((0u, glob));
                }
                options["filters"] = new[] { (label, patterns.ToArray()) };
            }
            ObjectPath handle;
            if (save)
            {
                if (!string.IsNullOrWhiteSpace(suggestedName))
                {
                    options["current_name"] = suggestedName!;
                }
                handle = await portal.SaveFileAsync("", title, options).ConfigureAwait(false);
            }
            else
            {
                handle = await portal.OpenFileAsync("", title, options).ConfigureAwait(false);
            }
            (uint code, IDictionary<string, object> results) = await AwaitResponseAsync(connection, handle, ct).ConfigureAwait(false);
            if (code != 0 || !results.TryGetValue("uris", out object? raw) || raw is not string[] uris || uris.Length == 0)
            {
                return PortalPickResult.FromPaths(Array.Empty<string>());
            }
            var paths = new List<string>(uris.Length);
            foreach (string uri in uris)
            {
                paths.Add(UriToPath(uri));
            }
            return PortalPickResult.FromPaths(paths.ToArray());
        }
        catch (OperationCanceledException)
        {
            return PortalPickResult.FromPaths(Array.Empty<string>());
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[portal] FileChooser 失败");
            return PortalPickResult.NotHandled;
        }
    }


    /// <summary>
    /// 让桌面截屏。返回图片的本地路径；取消或无 portal 返回 null。
    /// <paramref name="interactive"/> 为 true 时由桌面弹交互式选区（KDE 上是 spectacle 的 UI）。
    /// </summary>
    public static async Task<string?> ScreenshotAsync(bool interactive = true, CancellationToken ct = default)
    {
        Connection? connection = await GetConnectionAsync().ConfigureAwait(false);
        if (connection is null)
        {
            return null;
        }
        try
        {
            IScreenshotPortal portal = connection.CreateProxy<IScreenshotPortal>(Service, DesktopPath);
            var options = new Dictionary<string, object>
            {
                ["handle_token"] = NewToken(),
                ["interactive"] = interactive,
                ["modal"] = true,
            };
            ObjectPath handle = await portal.ScreenshotAsync("", options).ConfigureAwait(false);
            (uint code, IDictionary<string, object> results) = await AwaitResponseAsync(connection, handle, ct).ConfigureAwait(false);
            if (code != 0 || !results.TryGetValue("uri", out object? raw) || raw is not string uri)
            {
                return null;
            }
            return UriToPath(uri);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[portal] Screenshot 失败");
            return null;
        }
    }


    private static async Task<(uint Code, IDictionary<string, object> Results)> AwaitResponseAsync(
        Connection connection, ObjectPath handle, CancellationToken ct)
    {
        if (handle == default || string.IsNullOrEmpty(handle.ToString()))
        {
            return (2, new Dictionary<string, object>());
        }
        IPortalRequest request = connection.CreateProxy<IPortalRequest>(Service, handle);
        var completion = new TaskCompletionSource<(uint, IDictionary<string, object>)>(TaskCreationOptions.RunContinuationsAsynchronously);
        using IDisposable subscription = await request.WatchResponseAsync(
            response => completion.TrySetResult((response.Response, response.Results)),
            error => completion.TrySetException(error)).ConfigureAwait(false);
        using var registration = ct.Register(() =>
        {
            completion.TrySetCanceled(ct);
            _ = request.CloseAsync().ContinueWith(static t => _ = t.Exception, TaskScheduler.Default);
        });
        Task finished = await Task.WhenAny(completion.Task, Task.Delay(ResponseTimeout, ct)).ConfigureAwait(false);
        if (finished != completion.Task)
        {
            ct.ThrowIfCancellationRequested();
            Serilog.Log.Information("[portal] 等待用户响应超时（{Seconds} s）：{Handle}", (int)ResponseTimeout.TotalSeconds, handle);
            return (2, new Dictionary<string, object>());
        }
        return await completion.Task.ConfigureAwait(false);
    }


    /// <summary>file:// URI → 本地路径；非 file URI 原样返回（调用方按 URI 处理）。</summary>
    internal static string UriToPath(string uri)
    {
        if (Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed) && parsed.IsFile)
        {
            return parsed.LocalPath;
        }
        return uri;
    }

}
