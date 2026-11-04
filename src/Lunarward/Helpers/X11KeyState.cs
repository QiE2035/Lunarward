// Lunarward：X11 全局热键的底层。
//
// 为什么不用 XGrabKey：XGrabKey 会「独占」组合键（别的程序收不到），而且需要我们处理 XEvent 结构体布局；
// 我们只需要「按下了 Alt+S」这种级别的事件，用 XQueryKeymap 轮询（100ms）足够，且不会抢别人的键。
// Wayland 下 Wayland 客户端看不到全局键盘状态 —— 这时 Available=false，界面如实显示「注册失败」。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Vanara.PInvoke;

namespace Starward.Helpers;

internal static class X11KeyState
{
    private const int PollIntervalMs = 25;

    private static nint _display;

    private static Timer? _timer;

    private static readonly Dictionary<string, byte> _keycodeCache = new();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int XErrorHandler(nint display, nint errorEvent);

    // 必须用静态字段持有，否则委托会被 GC 回收，X11 回调时就是野指针。
    private static XErrorHandler? _errorHandler;

    public static bool Available => _display != 0;

    public static bool EnsureStarted()
    {
        if (_display != 0)
        {
            return true;
        }
        try
        {
            _display = Native.XOpenDisplay(IntPtr.Zero);
            if (_display == 0)
            {
                return false;
            }
            // 注意：不要在这里装 XSetErrorHandler / 不要 XSendEvent、XMapRaised ——
            // Xlib 的错误处理器是进程级全局的，改它会干扰 GTK 自己的处理，实测直接 abort；
            // 这个类只做只读的 XQueryKeymap 轮询。
            _timer = new Timer(_ => Poll(), null, 0, PollIntervalMs);
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "X11 unavailable for hotkeys");
            _display = 0;
            return false;
        }
    }

    private static void Poll()
    {
        try
        {
            HotkeyManager.PollOnce();
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Hotkey poll failed");
        }
    }

    public static bool IsCombinationDown(User32.HotKeyModifiers modifiers, User32.VK key)
    {
        if (_display == 0)
        {
            return false;
        }
        if (!IsKeyDown(KeycodeFor(key)))
        {
            return false;
        }
        if (modifiers.HasFlag(User32.HotKeyModifiers.MOD_ALT) && !IsKeyDown(KeycodeForName("Alt_L")))
        {
            return false;
        }
        if (modifiers.HasFlag(User32.HotKeyModifiers.MOD_CONTROL) && !IsKeyDown(KeycodeForName("Control_L")))
        {
            return false;
        }
        if (modifiers.HasFlag(User32.HotKeyModifiers.MOD_SHIFT) && !IsKeyDown(KeycodeForName("Shift_L")))
        {
            return false;
        }
        if (modifiers.HasFlag(User32.HotKeyModifiers.MOD_WIN) && !IsKeyDown(KeycodeForName("Super_L")))
        {
            return false;
        }
        return true;
    }

    private static bool IsKeyDown(byte keycode)
    {
        if (keycode == 0)
        {
            return false;
        }
        var keys = new byte[32];
        unsafe
        {
            fixed (byte* p = keys)
            {
                // XQueryKeymap 返回 256 位键盘状态：第 keycode 位为 1 表示按下
                Native.XQueryKeymap(_display, p);
            }
        }
        return (keys[keycode >> 3] & (1 << (keycode & 7))) != 0;
    }

    private static byte KeycodeFor(User32.VK key)
    {
        string? name = key switch
        {
            >= User32.VK.VK_F1 and <= User32.VK.VK_F12 => "F" + (key - User32.VK.VK_F1 + 1),
            >= User32.VK.VK_0 and <= User32.VK.VK_9 => ((char)('0' + (key - User32.VK.VK_0))).ToString(),
            >= User32.VK.VK_A and <= User32.VK.VK_Z => ((char)('a' + (key - User32.VK.VK_A))).ToString(),
            User32.VK.VK_SNAPSHOT => "Print",
            User32.VK.VK_PAUSE => "Pause",
            User32.VK.VK_SPACE => "space",
            _ => null,
        };
        return name is null ? (byte)0 : KeycodeForName(name);
    }

    private static byte KeycodeForName(string keysymName)
    {
        if (_keycodeCache.TryGetValue(keysymName, out byte cached))
        {
            return cached;
        }
        nint keysym = Native.XStringToKeysym(keysymName);
        byte keycode = keysym == 0 ? (byte)0 : (byte)Native.XKeysymToKeycode(_display, keysym);
        _keycodeCache[keysymName] = keycode;
        return keycode;
    }

    /// <summary>
    /// 把标题匹配的顶层窗口 map 回来。
    /// Uno 6.7.135 的 OverlappedPresenter.Restore() / AppWindow.Show() 在 X11 上都不会重新 map
    /// （实测窗口一直是 IsUnMapped），所以「最小化后再唤回」只能走这一条路。
    /// </summary>
    public static bool MapWindowByTitle(string title)
    {
        if (_display == 0)
        {
            return false;
        }
        nint root = Native.XDefaultRootWindow(_display);
        if (Native.XQueryTree(_display, root, out nint _, out nint _, out nint children, out uint count) == 0)
        {
            return false;
        }
        bool mapped = false;
        try
        {
            for (uint i = 0; i < count; i++)
            {
                nint child = Marshal.ReadIntPtr(children, (int)i * IntPtr.Size);
                nint namePtr = Native.XFetchName(_display, child, out nint _);
                if (namePtr == 0)
                {
                    continue;
                }
                try
                {
                    string? windowTitle = Marshal.PtrToStringUTF8(namePtr);
                    // 只认标题完全相同的顶层窗口：Contains 会误伤子窗口/其它程序，映射错窗口会把进程搞崩。
                    if (string.Equals(windowTitle, title, StringComparison.Ordinal))
                    {
                        Native.XMapRaised(_display, child);
                        mapped = true;
                    }
                }
                finally
                {
                    Native.XFree(namePtr);
                }
            }
        }
        finally
        {
            if (children != 0)
            {
                Native.XFree(children);
            }
        }
        return mapped;
    }

    /// <summary>
    /// 请求窗口管理器激活窗口（EWMH `_NET_ACTIVE_WINDOW`）。
    /// 直接 XMapRaised 在重定向型 WM（KWin/Mutter）下不够：根窗口的子窗口是 WM 的 frame，
    /// 映射 frame 不会改变客户端的 Iconic 状态；发 EWMH 消息才是「请把它恢复并置前」的正规做法。
    /// </summary>

    /// <summary>
    /// 通过 `xdotool` 唤醒窗口。
    /// 教训（L-45）：不要在本进程里做 XQueryTree/XFetchName/XSendEvent —— Xlib 不是线程安全的，
    /// 在计时器线程上与 GTK 并发使用会直接 `free(): invalid pointer` 崩掉进程；只读的 XQueryKeymap 不分配内存所以侥幸没事。
    /// xdotool 自己维护连接，用子进程隔离最安全；没有 xdotool 时返回 false，调用方降级。
    /// </summary>
    public static bool ActivateWindowWithXdotool(string title, int timeoutMs = 1500)
    {
        try
        {
            var search = Process.Start(new ProcessStartInfo("xdotool")
            {
                ArgumentList = { "search", "--name", "^" + title + "$" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (search is null)
            {
                return false;
            }
            string output = search.StandardOutput.ReadToEnd();
            if (!search.WaitForExit(timeoutMs) || search.ExitCode != 0)
            {
                return false;
            }
            string? id = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }
            var activate = Process.Start(new ProcessStartInfo("xdotool")
            {
                ArgumentList = { "windowactivate", "--sync", id },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            return activate is not null && activate.WaitForExit(timeoutMs);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "xdotool activation failed");
            return false;
        }
    }

    /// <summary>Xlib 里只需要这几个函数；用 DllImport 直接调，不引入完整的 X11 绑定。</summary>
    private static partial class Native
    {
        private const string Lib = "libX11.so.6";

        [DllImport(Lib)]
        internal static extern nint XOpenDisplay(IntPtr displayName);

        [DllImport(Lib)]
        internal static extern nint XStringToKeysym([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport(Lib)]
        internal static extern byte XKeysymToKeycode(nint display, nint keysym);

        [DllImport(Lib)]
        internal static extern unsafe int XQueryKeymap(nint display, byte* keys);

        [DllImport(Lib)]
        internal static extern nint XDefaultRootWindow(nint display);

        [DllImport(Lib)]
        internal static extern nint XSetErrorHandler(XErrorHandler handler);

        [DllImport(Lib)]
        internal static extern int XQueryTree(nint display, nint window, out nint root, out nint parent, out nint children, out uint count);

        [DllImport(Lib)]
        internal static extern nint XFetchName(nint display, nint window, out nint name);

        [DllImport(Lib)]
        internal static extern int XMapRaised(nint display, nint window);

        [DllImport(Lib)]
        internal static extern int XFree(nint memory);

        [DllImport(Lib)]
        internal static extern nint XInternAtom(nint display, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, bool onlyIfExists);

        [DllImport(Lib)]
        internal static extern int XSendEvent(nint display, nint window, bool propagate, long eventMask, nint eventPtr);

        [DllImport(Lib)]
        internal static extern int XFlush(nint display);
    }
}
