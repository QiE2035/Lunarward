// Lunarward：上游大量页面通过 Vanara.PInvoke 使用「全局热键」相关常量与错误码。
// 这里只补最小的一组类型（热键的修饰键/虚拟键/Win32Error），让页面源码保持原样；
// 真正的原生调用（XGrabKey / portal GlobalShortcuts）在 Helpers/HotkeyManager.Linux.cs。
// 注意：这是有意的「名字兼容层」，范围仅限热键，不覆盖 Vanara 其它 API。
using System;

namespace Vanara.PInvoke;

/// <summary>仅保留热键路径需要的错误码语义。</summary>
// Win32Error 由 Vanara.Core（上游传递引用）提供，这里不再重复定义。

public static class User32
{
    /// <summary>
    /// 窗口样式索引。上游用 Vanara 的同一组常量；这里只补移植侧代码引用到的项。
    /// </summary>
    [Flags]
    public enum WindowLongFlags : int
    {
        GWL_EXSTYLE = -20,
        GWL_HWNDPARENT = -8,
        GWL_STYLE = -16,
    }


    /// <summary>
    /// 仅 Windows 分支会调用（移植侧用 <c>OperatingSystem.IsWindows()</c> 守卫）；
    /// Linux 下不会被命中，因此这里保留真 P/Invoke 而不做假实现。
    /// </summary>
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);


    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);


    public static nint SetWindowLong(nint hWnd, WindowLongFlags nIndex, nint dwNewLong)
    {
        if (IntPtr.Size == 8)
        {
            return SetWindowLongPtr64(hWnd, (int)nIndex, dwNewLong);
        }
        return SetWindowLong32(hWnd, (int)nIndex, (int)dwNewLong);
    }


    [Flags]
    public enum HotKeyModifiers : uint
    {
        MOD_ALT = 0x0001,
        MOD_CONTROL = 0x0002,
        MOD_SHIFT = 0x0004,
        MOD_WIN = 0x0008,
        MOD_NOREPEAT = 0x4000,
    }

    /// <summary>虚拟键码（Win32 编号，Linux 侧在热键实现里映射到 keysym）。</summary>
    public enum VK : ushort
    {
        VK_LBUTTON = 0x01, VK_RBUTTON = 0x02, VK_CANCEL = 0x03, VK_MBUTTON = 0x04,
        VK_BACK = 0x08, VK_TAB = 0x09, VK_CLEAR = 0x0C, VK_RETURN = 0x0D,
        VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12, VK_PAUSE = 0x13,
        VK_CAPITAL = 0x14, VK_ESCAPE = 0x1B, VK_SPACE = 0x20,
        VK_PRIOR = 0x21, VK_NEXT = 0x22, VK_END = 0x23, VK_HOME = 0x24,
        VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28,
        VK_SNAPSHOT = 0x2C, VK_INSERT = 0x2D, VK_DELETE = 0x2E,
        VK_0 = 0x30, VK_1 = 0x31, VK_2 = 0x32, VK_3 = 0x33, VK_4 = 0x34,
        VK_5 = 0x35, VK_6 = 0x36, VK_7 = 0x37, VK_8 = 0x38, VK_9 = 0x39,
        VK_A = 0x41, VK_B = 0x42, VK_C = 0x43, VK_D = 0x44, VK_E = 0x45,
        VK_F = 0x46, VK_G = 0x47, VK_H = 0x48, VK_I = 0x49, VK_J = 0x4A,
        VK_K = 0x4B, VK_L = 0x4C, VK_M = 0x4D, VK_N = 0x4E, VK_O = 0x4F,
        VK_P = 0x50, VK_Q = 0x51, VK_R = 0x52, VK_S = 0x53, VK_T = 0x54,
        VK_U = 0x55, VK_V = 0x56, VK_W = 0x57, VK_X = 0x58, VK_Y = 0x59,
        VK_Z = 0x5A,
        VK_LWIN = 0x5B, VK_RWIN = 0x5C,
        VK_NUMPAD0 = 0x60, VK_NUMPAD1 = 0x61, VK_NUMPAD2 = 0x62, VK_NUMPAD3 = 0x63,
        VK_NUMPAD4 = 0x64, VK_NUMPAD5 = 0x65, VK_NUMPAD6 = 0x66, VK_NUMPAD7 = 0x67,
        VK_NUMPAD8 = 0x68, VK_NUMPAD9 = 0x69,
        VK_MULTIPLY = 0x6A, VK_ADD = 0x6B, VK_SUBTRACT = 0x6D, VK_DECIMAL = 0x6E, VK_DIVIDE = 0x6F,
        VK_F1 = 0x70, VK_F2 = 0x71, VK_F3 = 0x72, VK_F4 = 0x73, VK_F5 = 0x74, VK_F6 = 0x75,
        VK_F7 = 0x76, VK_F8 = 0x77, VK_F9 = 0x78, VK_F10 = 0x79, VK_F11 = 0x7A, VK_F12 = 0x7B,
        VK_NUMLOCK = 0x90, VK_SCROLL = 0x91,
        VK_OEM_1 = 0xBA, VK_OEM_PLUS = 0xBB, VK_OEM_COMMA = 0xBC, VK_OEM_MINUS = 0xBD,
        VK_OEM_PERIOD = 0xBE, VK_OEM_2 = 0xBF, VK_OEM_3 = 0xC0,
        VK_OEM_4 = 0xDB, VK_OEM_5 = 0xDC, VK_OEM_6 = 0xDD, VK_OEM_7 = 0xDE,
    }
}
