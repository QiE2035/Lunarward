// Lunarward：用 libxcb 直接和 X 服务器说话，补上 Uno 6.7.135 的 X11 后端缺的两件事：
//   1. 隐藏窗口——X11WindowWrapper 没有 HideCore，AppWindow.Hide() 是空操作（D47 / L-84）；
//   2. 唤回窗口——Uno 的 Activate() 只发 XRaiseWindow，而恢复被最小化/隐藏的窗口需要向 root
//      发 _NET_ACTIVE_WINDOW 客户端消息（EWMH），Uno 源码里那段恰好被注释掉了。
// 选 xcb 而不是 Xlib：xcb 的错误按连接异步返回（xcb_request_check 可检查），不需要安装进程级
// XSetErrorHandler（Xlib 默认错误处理会直接退出进程），也避开 L-45 记录的多线程 Display 问题——
// 这里自建连接、所有调用串行化，与 Uno 自己的连接互不相干。
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Serilog;

namespace Starward.Helpers;

public static class X11Window
{
    private const string LibXcb = "libxcb.so.1";

    private const byte XCB_CLIENT_MESSAGE = 32;
    private const uint XA_WINDOW = 33;
    private const uint XA_CARDINAL = 6;
    private const uint XCB_EVENT_MASK_SUBSTRUCTURE_REDIRECT = 0x00100000;
    private const uint XCB_EVENT_MASK_SUBSTRUCTURE_NOTIFY = 0x00080000;
    private const uint XCB_CURRENT_TIME = 0;

    private static readonly object Gate = new();
    private static bool _available;
    private static IntPtr _connection;
    private static uint _root;
    private static uint _atomActiveWindow;
    private static uint _atomClientList;
    private static uint _atomWmPid;
    private static uint _cachedWindow;

    [DllImport(LibXcb)] private static extern IntPtr xcb_connect(string? displayName, out int screenNumber);
    [DllImport(LibXcb)] private static extern void xcb_disconnect(IntPtr connection);
    [DllImport(LibXcb)] private static extern int xcb_connection_has_error(IntPtr connection);
    [DllImport(LibXcb)] private static extern IntPtr xcb_get_setup(IntPtr connection);
    [DllImport(LibXcb)] private static extern void xcb_flush(IntPtr connection);
    [DllImport(LibXcb)] private static extern int xcb_unmap_window(IntPtr connection, uint window);
    [DllImport(LibXcb)] private static extern int xcb_map_window(IntPtr connection, uint window);
    [DllImport(LibXcb)] private static extern IntPtr xcb_intern_atom(IntPtr connection, byte onlyIfExists, ushort nameLength, string name);
    [DllImport(LibXcb)] private static extern IntPtr xcb_intern_atom_reply(IntPtr connection, IntPtr cookie, out IntPtr error);
    [DllImport(LibXcb)] private static extern IntPtr xcb_get_property(IntPtr connection, byte delete, uint window, uint property, uint type, uint offset, uint length);
    [DllImport(LibXcb)] private static extern IntPtr xcb_get_property_reply(IntPtr connection, IntPtr cookie, out IntPtr error);
    [DllImport(LibXcb)] private static extern uint xcb_send_event(IntPtr connection, byte propagate, uint destination, uint eventMask, byte[] eventData);
    [DllImport(LibXcb)] private static extern IntPtr xcb_get_window_attributes(IntPtr connection, uint window);
    [DllImport(LibXcb)] private static extern IntPtr xcb_get_window_attributes_reply(IntPtr connection, IntPtr cookie, out IntPtr error);
    [DllImport(LibXcb)] private static extern IntPtr xcb_get_geometry(IntPtr connection, uint drawable);
    [DllImport(LibXcb)] private static extern IntPtr xcb_get_geometry_reply(IntPtr connection, IntPtr cookie, out IntPtr error);
    [DllImport(LibXcb)] private static extern int xcb_configure_window(IntPtr connection, uint window, ushort valueMask, int[] valueList);
    [DllImport(LibXcb)] private static extern IntPtr xcb_query_tree(IntPtr connection, uint window);
    [DllImport(LibXcb)] private static extern IntPtr xcb_query_tree_reply(IntPtr connection, IntPtr cookie, out IntPtr error);
    [DllImport(LibXcb)] private static extern IntPtr xcb_change_property(IntPtr connection, byte mode, uint window, uint property, uint type, byte format, uint dataLength, uint[] data);
    [DllImport("libc")] private static extern void free(IntPtr pointer);

    private const ushort XCB_CONFIG_WINDOW_X = 1 << 0;
    private const ushort XCB_CONFIG_WINDOW_Y = 1 << 1;
    private const ushort XCB_CONFIG_WINDOW_WIDTH = 1 << 2;
    private const ushort XCB_CONFIG_WINDOW_HEIGHT = 1 << 3;

    /// <summary>连接是否可用（没有 DISPLAY 或 libxcb 缺失时为 false，调用方应回退到 Uno 的实现）。</summary>
    public static bool IsAvailable
    {
        get
        {
            lock (Gate)
            {
                EnsureInitialized();
                return _available;
            }
        }
    }

    /// <summary>隐藏窗口：把窗口从 X 的映射里摘掉（任务栏/焦点随之消失）。</summary>
    public static bool Hide(string? title)
    {
        lock (Gate)
        {
            EnsureInitialized();
            if (!_available)
            {
                return false;
            }
            uint window = FindWindow(title);
            if (window == 0)
            {
                Log.Warning("X11Window.Hide: 找不到窗口（title={Title}）", title);
                return false;
            }
            try
            {
                xcb_unmap_window(_connection, window);
                xcb_flush(_connection);
                Log.Information("X11Window: hide 0x{Window:X}", window);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "X11Window.Hide failed");
                return false;
            }
        }
    }

    /// <summary>唤回窗口：重新 map 并请求窗口管理器激活（恢复最小化靠的就是 _NET_ACTIVE_WINDOW）。</summary>
    public static bool Show(string? title)
    {
        lock (Gate)
        {
            EnsureInitialized();
            if (!_available)
            {
                return false;
            }
            uint window = FindWindow(title);
            if (window == 0)
            {
                Log.Warning("X11Window.Show: 找不到窗口（title={Title}）", title);
                return false;
            }
            try
            {
                xcb_map_window(_connection, window);
                SendActiveWindow(window);
                xcb_flush(_connection);
                Log.Information("X11Window: show 0x{Window:X}", window);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "X11Window.Show failed");
                return false;
            }
        }
    }

    /// <summary>取 X 屏幕（root window）的像素尺寸；失败返回 false。</summary>
    public static bool TryGetScreenSize(out int width, out int height)
    {
        width = 0;
        height = 0;
        lock (Gate)
        {
            uint root = GetRootWindow();
            if (root == 0)
            {
                return false;
            }
            try
            {
                IntPtr cookie = xcb_get_geometry(_connection, root);
                IntPtr reply = xcb_get_geometry_reply(_connection, cookie, out IntPtr error);
                if (reply == IntPtr.Zero || error != IntPtr.Zero)
                {
                    if (reply != IntPtr.Zero)
                    {
                        free(reply);
                    }
                    return false;
                }
                // xcb_get_geometry_reply_t：response_type(1) depth(1) sequence(2) length(4) root(4)@8
                // x(2)@12 y(2)@14 width(2)@16 height(2)@18
                width = Marshal.ReadInt16(reply, 16);
                height = Marshal.ReadInt16(reply, 18);
                free(reply);
                return width > 0 && height > 0;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "X11Window.TryGetScreenSize failed");
                return false;
            }
        }
    }

    /// <summary>直接改窗口的位置与大小（Uno 的 AppWindow.MoveAndResize 在 X11 上是空操作）。</summary>
    public static bool SetBounds(string? title, int x, int y, int width, int height)
    {
        lock (Gate)
        {
            uint window = FindWindow(title);
            if (window == 0)
            {
                return false;
            }
            try
            {
                ushort mask = (ushort)(XCB_CONFIG_WINDOW_X | XCB_CONFIG_WINDOW_Y | XCB_CONFIG_WINDOW_WIDTH | XCB_CONFIG_WINDOW_HEIGHT);
                xcb_configure_window(_connection, window, mask, new[] { x, y, width, height });
                xcb_flush(_connection);
                Log.Information("X11Window: bounds 0x{Window:X} {X},{Y} {W}x{H}", window, x, y, width, height);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "X11Window.SetBounds failed");
                return false;
            }
        }
    }

    /// <summary>
    /// 设置窗口图标（_NET_WM_ICON）。Uno 的 X11 后端从不设置这个属性，
    /// 结果任务栏/Alt-Tab 里显示成通用占位——用户第一眼就会觉得「这软件怪怪的」。
    /// </summary>
    /// <param name="title">窗口标题（用来定位窗口）</param>
    /// <param name="width">图标宽度（像素）</param>
    /// <param name="height">图标高度（像素）</param>
    /// <param name="argb">按行优先排列的 0xAARRGGBB 像素，长度必须是 width*height</param>
    public static bool SetIcon(string? title, int width, int height, uint[] argb)
    {
        if (width <= 0 || height <= 0 || argb.Length != width * height)
        {
            return false;
        }
        lock (Gate)
        {
            EnsureInitialized();
            if (!_available)
            {
                return false;
            }
            try
            {
                uint window = FindWindow(title);
                if (window == 0)
                {
                    return false;
                }
                uint atom = InternAtom("_NET_WM_ICON");
                if (atom == 0)
                {
                    return false;
                }
                // 数据格式：[width, height, 像素...]（CARDINAL 数组，32 位）
                uint[] data = new uint[argb.Length + 2];
                data[0] = (uint)width;
                data[1] = (uint)height;
                Array.Copy(argb, 0, data, 2, argb.Length);
                xcb_change_property(_connection, 0 /* Replace */, window, atom, XA_CARDINAL, 32, (uint)data.Length, data);
                xcb_flush(_connection);
                Log.Information("X11Window: 已设置窗口图标 {Width}x{Height}", width, height);
                return true;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "X11Window.SetIcon failed");
                return false;
            }
        }
    }


    /// <summary>root window（懒初始化；不可用时返回 0）。</summary>
    public static uint GetRootWindow()
    {
        lock (Gate)
        {
            EnsureInitialized();
            return _available ? _root : 0;
        }
    }

    private static void EnsureInitialized()
    {
        if (_connection != IntPtr.Zero || _available)
        {
            return;
        }
        try
        {
            _connection = xcb_connect(null, out _);
            if (_connection == IntPtr.Zero || xcb_connection_has_error(_connection) != 0)
            {
                Log.Warning("X11Window: 无法连接 X 服务器（DISPLAY={Display}）", Environment.GetEnvironmentVariable("DISPLAY"));
                _connection = IntPtr.Zero;
                return;
            }
            if (!TryReadRootWindow(out _root))
            {
                Log.Warning("X11Window: 读不到 root window");
                xcb_disconnect(_connection);
                _connection = IntPtr.Zero;
                return;
            }
            _atomActiveWindow = InternAtom("_NET_ACTIVE_WINDOW");
            _atomClientList = InternAtom("_NET_CLIENT_LIST");
            _atomWmPid = InternAtom("_NET_WM_PID");
            _available = _root != 0 && _atomActiveWindow != 0 && _atomClientList != 0 && _atomWmPid != 0;
            if (!_available)
            {
                Log.Warning("X11Window: 缺少必需的 EWMH atom，回退到 Uno 的实现");
            }
            else
            {
                Log.Information("X11Window: 已连接 X 服务器 root=0x{Root:X} active=0x{Active:X} clientList=0x{List:X} pid=0x{Pid:X}",
                    _root, _atomActiveWindow, _atomClientList, _atomWmPid);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "X11Window: 初始化 xcb 失败");
            _available = false;
            _connection = IntPtr.Zero;
        }
    }

    /// <summary>从 xcb_setup_t 里取第一个 screen 的 root window（setup 结构是固定布局 + 变长尾部）。</summary>
    private static bool TryReadRootWindow(out uint root)
    {
        root = 0;
        IntPtr setup = xcb_get_setup(_connection);
        if (setup == IntPtr.Zero)
        {
            return false;
        }
        // xcb_setup_t：header 40 字节（vendor_len 在 24、roots_len 在 28、pixmap_formats_len 在 29），
        // 之后是 vendor 字符串（4 字节对齐）、pixmap formats（每项 8 字节）、最后是 screen 数组。
        int vendorLength = Marshal.ReadInt16(setup, 24);
        int rootsLength = Marshal.ReadByte(setup, 28);
        int formatsLength = Marshal.ReadByte(setup, 29);
        int offset = 40 + ((vendorLength + 3) & ~3) + formatsLength * 8;
        if (rootsLength <= 0)
        {
            return false;
        }
        root = (uint)Marshal.ReadInt32(setup, offset);
        return root != 0;
    }

    private static uint InternAtom(string name)
    {
        IntPtr cookie = xcb_intern_atom(_connection, 0, (ushort)name.Length, name);
        IntPtr reply = xcb_intern_atom_reply(_connection, cookie, out IntPtr error);
        if (reply == IntPtr.Zero || error != IntPtr.Zero)
        {
            if (reply != IntPtr.Zero)
            {
                free(reply);
            }
            return 0;
        }
        // xcb_intern_atom_reply_t: response_type(1) pad(1) sequence(2) length(4) atom(4) ...
        uint atom = (uint)Marshal.ReadInt32(reply, 8);
        free(reply);
        return atom;
    }

    /// <summary>
    /// 在本进程的窗口里找目标窗口：遍历 _NET_CLIENT_LIST，先按 _NET_WM_PID 匹配；
    /// Uno 的 X11 顶层窗口不一定设置 _NET_WM_PID（实测 KDE 下没有），所以再按 WM_NAME / WM_CLASS 兜底。
    /// </summary>
    private static uint FindWindow(string? title)
    {
        // 窗口被 unmap 之后，窗口管理器会把它从 _NET_CLIENT_LIST 里摘掉，所以必须记住上次找到的窗口。
        if (_cachedWindow != 0)
        {
            if (IsWindowValid(_cachedWindow))
            {
                return _cachedWindow;
            }
            _cachedWindow = 0;
        }
        uint found = FindWindowFromClientList(title);
        if (found == 0)
        {
            // _NET_CLIENT_LIST 只列「已被 WM 接管」的窗口，刚 map 出来的顶层窗口可能还不在里面；
            // 直接遍历窗口树按 WM_CLASS / WM_NAME 找（xdotool search 用的也是这条路）。
            found = FindWindowFromTree(title);
        }
        if (found != 0)
        {
            _cachedWindow = found;
        }
        return found;
    }

    private static uint FindWindowFromTree(string? title)
    {
        try
        {
            IntPtr cookie = xcb_query_tree(_connection, _root);
            IntPtr reply = xcb_query_tree_reply(_connection, cookie, out IntPtr error);
            if (reply == IntPtr.Zero || error != IntPtr.Zero)
            {
                if (reply != IntPtr.Zero)
                {
                    free(reply);
                }
                return 0;
            }
            int childrenLength = Marshal.ReadInt16(reply, 16);
            IntPtr data = reply + 32;
            uint fallback = 0;
            for (int i = 0; i < childrenLength; i++)
            {
                uint window = (uint)Marshal.ReadInt32(data, i * 4);
                if (!string.IsNullOrEmpty(title) && MatchesName(window, title))
                {
                    free(reply);
                    return window;
                }
                // 标题可能是 "Lunarward" 而 WM_CLASS 是 ("Lunarward","Lunarward")；
                // 有些窗口标题被上游改写（例如 "Starward - Background Viewer"），用类名兜底。
                string? wmClass = ReadStringProperty(window, "WM_CLASS");
                if (fallback == 0 && !string.IsNullOrEmpty(wmClass)
                    && wmClass.Contains("Lunarward", StringComparison.Ordinal)
                    && IsWindowValid(window))
                {
                    fallback = window;
                }
            }
            free(reply);
            if (fallback != 0)
            {
                Log.Information("X11Window: 按 WM_CLASS 找到窗口 0x{Window:X}", fallback);
            }
            return fallback;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "FindWindowFromTree failed");
            return 0;
        }
    }


    private static bool IsWindowValid(uint window)
    {
        try
        {
            IntPtr cookie = xcb_get_window_attributes(_connection, window);
            IntPtr reply = xcb_get_window_attributes_reply(_connection, cookie, out IntPtr error);
            bool valid = reply != IntPtr.Zero && error == IntPtr.Zero;
            if (reply != IntPtr.Zero)
            {
                free(reply);
            }
            return valid;
        }
        catch
        {
            return false;
        }
    }

    private static uint FindWindowFromClientList(string? title)
    {
        List<uint> clients = ReadCardinalList(_root, _atomClientList, XA_WINDOW);
        if (clients.Count == 0)
        {
            Log.Warning("X11Window: _NET_CLIENT_LIST 为空（root=0x{Root:X}）", _root);
            return 0;
        }
        int pid = Environment.ProcessId;
        List<uint> byPid = new();
        List<uint> byName = new();
        foreach (uint client in clients)
        {
            List<uint> pidList = ReadCardinalList(client, _atomWmPid, XA_CARDINAL);
            if (pidList.Count > 0 && (int)pidList[0] == pid)
            {
                byPid.Add(client);
                continue;
            }
            if (!string.IsNullOrEmpty(title) && MatchesName(client, title))
            {
                byName.Add(client);
            }
        }
        if (byPid.Count > 0)
        {
            return byPid[0];
        }
        if (byName.Count > 0)
        {
            return byName[0];
        }
        Log.Warning("X11Window: 客户端窗口 {Count} 个，未匹配到本进程窗口（pid={Pid}）", clients.Count, pid);
        return 0;
    }

    private static bool MatchesName(uint window, string title)
    {
        string? name = ReadStringProperty(window, "WM_NAME");
        if (string.Equals(name, title, StringComparison.Ordinal))
        {
            return true;
        }
        string? wmClass = ReadStringProperty(window, "WM_CLASS");
        if (string.IsNullOrEmpty(wmClass))
        {
            return false;
        }
        foreach (string part in wmClass.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part, title, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static List<uint> ReadCardinalList(uint window, uint property, uint type)
    {
        List<uint> values = new();
        IntPtr cookie = xcb_get_property(_connection, 0, window, property, type, 0, 1024);
        IntPtr reply = xcb_get_property_reply(_connection, cookie, out IntPtr error);
        if (reply == IntPtr.Zero || error != IntPtr.Zero)
        {
            if (reply != IntPtr.Zero)
            {
                free(reply);
            }
            return values;
        }
        // xcb_get_property_reply_t: response_type(0) format(1) sequence(2) length(4) type(8) bytes_after(12) value_len(16) pad(20)
        int format = Marshal.ReadByte(reply, 1);
        int valueLength = Marshal.ReadInt32(reply, 16);
        if (format == 32 && valueLength > 0)
        {
            IntPtr data = reply + 32;
            for (int i = 0; i < valueLength; i++)
            {
                values.Add((uint)Marshal.ReadInt32(data, i * 4));
            }
        }
        free(reply);
        return values;
    }

    private static string? ReadStringProperty(uint window, string propertyName)
    {
        uint atom = InternAtom(propertyName);
        if (atom == 0)
        {
            return null;
        }
        IntPtr cookie = xcb_get_property(_connection, 0, window, atom, 0, 0, 64);
        IntPtr reply = xcb_get_property_reply(_connection, cookie, out IntPtr error);
        if (reply == IntPtr.Zero || error != IntPtr.Zero)
        {
            if (reply != IntPtr.Zero)
            {
                free(reply);
            }
            return null;
        }
        int valueLength = Marshal.ReadInt32(reply, 16);
        string? value = valueLength > 0 ? Marshal.PtrToStringAnsi(reply + 32, valueLength) : null;
        free(reply);
        return value;
    }

    /// <summary>向 root 发 _NET_ACTIVE_WINDOW 客户端消息（source=2 pager，timestamp=CurrentTime），KDE/GNOME 据此恢复并聚焦。</summary>
    private static void SendActiveWindow(uint window)
    {
        byte[] ev = new byte[32];
        ev[0] = XCB_CLIENT_MESSAGE;
        ev[1] = 32; // format
        WriteUInt32(ev, 4, window);
        WriteUInt32(ev, 8, _atomActiveWindow);
        WriteUInt32(ev, 12, 2); // source indication: 2 = pager
        WriteUInt32(ev, 16, XCB_CURRENT_TIME);
        uint mask = XCB_EVENT_MASK_SUBSTRUCTURE_REDIRECT | XCB_EVENT_MASK_SUBSTRUCTURE_NOTIFY;
        // 返回的是 xcb_void_cookie_t（sequence），不是错误码；真正的协议错误要 xcb_request_check 才拿得到。
        _ = xcb_send_event(_connection, 0, _root, mask, ev);
    }

    private static void WriteUInt32(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }
}
