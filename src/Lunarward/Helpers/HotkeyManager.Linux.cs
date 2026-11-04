// Lunarward：全局热键在 Linux 需要 portal（org.freedesktop.portal.GlobalShortcuts）
// 或 X11 XGrabKey；M2 落地。这里提供与上游同签名的空实现：
// 注册一律返回 ERROR_HOTKEY_ALREADY_REGISTERED 之外的普通失败码，UI 会显示「注册失败」提示。
using System;
using System.Collections.Generic;
using System.Linq;
using Vanara.PInvoke;

namespace Starward.Helpers;

internal static class HotkeyManager
{
    public static HotkeyInfo ShowMainWindow { get; private set; } = new(nameof(AppConfig.ShowMainWindowHotkey), 44444, User32.HotKeyModifiers.MOD_ALT, User32.VK.VK_S);

    public static HotkeyInfo ScreenshotCapture { get; private set; } = new(nameof(AppConfig.ScreenshotCaptureHotkey), 44445, User32.HotKeyModifiers.MOD_ALT, User32.VK.VK_D);

    private static readonly Dictionary<int, HotkeyInfo> _hotkeys = new();

    private static nint _display;

    private static bool _polling;

    private static readonly HashSet<int> _pressed = new();

    public static void InitializeHotkey(nint hwnd)
    {
        // Linux 没有 RegisterHotKey：这里改用「轮询 XQueryKeymap」实现全局热键（见 X11KeyState）。
        if (!X11KeyState.EnsureStarted())
        {
            Serilog.Log.Information("Hotkeys unavailable (no X11 display); skipped.");
            return;
        }
        foreach (HotkeyInfo item in new[] { ShowMainWindow, ScreenshotCapture })
        {
            Win32Error error = RegisterHotkey(hwnd, item.Id, item.Modifiers, item.Key);
            item.Error = error;
            if (error.Failed)
            {
                Serilog.Log.Information("Hotkey {id} not available", item.Id);
            }
        }
    }

    public static void UnregisterAll() => _pressed.Clear();

    public static Win32Error RegisterHotkey(nint hwnd, int id, User32.HotKeyModifiers modifiers, User32.VK key)
    {
        _hotkeys[id] = new HotkeyInfo($"{id}", id, modifiers, key);
        if (!X11KeyState.Available)
        {
            // 没有 X11（纯 Wayland）时如实失败，界面会显示「注册失败」，而不是假装成功。
            return new Win32Error(1409);
        }
        return default;
    }

    public static Win32Error UnregisterHotkey(nint hwnd, int id) => default;

    public static Win32Error DeleteHotkey(nint hwnd, int id)
    {
        _hotkeys.Remove(id);
        _pressed.Remove(id);
        return default;
    }

    public static void InitializeHotkeyInput(object hotkeyInput) { }

    public static HotkeyInfo? GetHotkeyInfo(int id) => _hotkeys.TryGetValue(id, out var info) ? info : null;

    /// <summary>每 100ms 看一次键盘状态：组合键「从松开到按下」的那一下触发一次，避免长按连发。</summary>
    internal static void PollOnce()
    {
        foreach (HotkeyInfo info in _hotkeys.Values.ToArray())
        {
            bool down = X11KeyState.IsCombinationDown(info.Modifiers, info.Key);
            bool was = _pressed.Contains(info.Id);
            if (down && !was)
            {
                _pressed.Add(info.Id);
                Fire(info);
            }
            else if (!down && was)
            {
                _pressed.Remove(info.Id);
            }
        }
    }

    private static void Fire(HotkeyInfo info)
    {
        try
        {
            if (info.Id == ShowMainWindow.Id)
            {
                Serilog.Log.Information("Hotkey: show main window");
                Features.ViewHost.MainWindow.Current?.DispatcherQueue.TryEnqueue(() =>
                {
                    var window = Features.ViewHost.MainWindow.Current;
                    if (window is null)
                    {
                        return;
                    }
                    // 最小化的窗口光 Activate 不会回来，要先 Restore（Windows 上 ShowWindow 语义）。
                    try
                    {
                        if (window.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter
                            && presenter.State == Microsoft.UI.Windowing.OverlappedPresenterState.Minimized)
                        {
                            presenter.Restore();
                        }
                        // Uno 的 Restore/Show 在 X11 上不生效，兜底直接 XMapRaised（见 X11KeyState.MapWindowByTitle）
                        // Uno 的 Restore/Show 在 X11 后端都是空操作，真正生效的是 EWMH 的激活请求。
                        if (!X11KeyState.ActivateWindowWithXdotool("Starward"))
                        {
                            Serilog.Log.Information("hotkey: EWMH activate found no window");
                        }
                        window.Activate();
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Debug(ex, "Restore presenter failed");
                    }
                    window.Activate();
                });
            }
            else if (info.Id == ScreenshotCapture.Id)
            {
                // D31：外部工具捕获（spectacle / grim / gnome-screenshot），保存到 ~/Pictures/Lunarward。
                Serilog.Log.Information("Hotkey: screenshot capture");
                _ = Features.Screenshot.ScreenCaptureLinux.CaptureAsync().ContinueWith(t =>
                {
                    string? file = t.IsCompletedSuccessfully ? t.Result : null;
                    var window = Features.ViewHost.MainWindow.Current;
                    window?.DispatcherQueue.TryEnqueue(() =>
                    {
                        if (file is null)
                        {
                            Helpers.InAppToast.MainWindow?.Error((string?)null, "截图失败：没有可用的截图工具", 5000);
                        }
                        else
                        {
                            Helpers.InAppToast.MainWindow?.Success((string?)null, $"截图已保存：{file}", 5000);
                        }
                    });
                });
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Hotkey action failed");
        }
    }

    public class HotkeyInfo
    {
        public HotkeyInfo(string name, int id, User32.HotKeyModifiers modifiers, User32.VK key)
        {
            Name = name;
            Id = id;
            Modifiers = modifiers;
            Key = key;
        }

        public string Name { get; }

        public int Id { get; set; }

        public User32.HotKeyModifiers Modifiers { get; set; }

        public User32.VK Key { get; set; }

        public Win32Error Error { get; set; }
    }
}
