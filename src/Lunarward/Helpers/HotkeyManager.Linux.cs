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
    public static HotkeyInfo ShowMainWindow { get; private set; } = new(nameof(AppConfig.ShowMainWindowHotkey), nameof(AppConfig.ShowMainWindowHotkey), 44444, User32.HotKeyModifiers.MOD_ALT, User32.VK.VK_S);

    public static HotkeyInfo ScreenshotCapture { get; private set; } = new(nameof(AppConfig.ScreenshotCaptureHotkey), nameof(AppConfig.ScreenshotCaptureHotkey), 44445, User32.HotKeyModifiers.MOD_ALT, User32.VK.VK_D);

    private static readonly Dictionary<int, HotkeyInfo> _hotkeys = new();

    private static nint _display;

    private static bool _polling;

    private static readonly HashSet<int> _pressed = new();

    public static void InitializeHotkey(nint hwnd)
    {
        // B24：无论 X11 是否可用，都把两项热键登记进 _hotkeys，设置页才能显示已保存的组合键。
        bool x11 = X11KeyState.EnsureStarted();
        if (!x11)
        {
            Serilog.Log.Information("Hotkeys unavailable (no X11 display); values are shown read-only.");
        }
        foreach (HotkeyInfo item in new[] { ShowMainWindow, ScreenshotCapture })
        {
            // 读回上次保存的组合键；上游把「0」当作「用户删掉了这个热键」。
            User32.HotKeyModifiers modifiers = item.DefaultModifiers;
            User32.VK key = item.DefaultKey;
            (User32.HotKeyModifiers Modifiers, User32.VK Key)? configured = ParseHotkey(AppConfig.GetValue<string>(null, item.ConfigSetting));
            if (configured is { } mk)
            {
                if (mk.Modifiers == 0 && mk.Key == 0)
                {
                    _hotkeys[item.Id] = item;
                    item.Modifiers = 0;
                    item.Key = 0;
                    Serilog.Log.Information("Hotkey {id} is disabled by user", item.Id);
                    continue;
                }
                modifiers = mk.Modifiers;
                key = mk.Key;
            }
            if (!x11)
            {
                // 没有 X11：保留配置原值，标记为不可注册（界面显示警告），但不落盘默认值（B55/B56）。
                item.Modifiers = modifiers;
                item.Key = key;
                item.IsRegistered = false;
                item.Error = new Win32Error(1409);
                _hotkeys[item.Id] = item;
                Serilog.Log.Information("Hotkey {id} unavailable without X11", item.Id);
                continue;
            }
            Win32Error error = RegisterHotkey(hwnd, item.Id, modifiers, key);
            item.Error = error;
            if (error.Failed)
            {
                Serilog.Log.Information("Hotkey {id} not available", item.Id);
            }
        }
    }


    /// <summary>把 AppConfig 里的 "修饰键+按键" 解析成枚举；格式来自上游（例如 Alt+S = "1+83"）。</summary>
    private static (User32.HotKeyModifiers Modifiers, User32.VK Key)? ParseHotkey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        // 上游把单独的 "0" 当作「用户删除了这个热键」。
        if (string.Equals(text.Trim(), "0", StringComparison.Ordinal))
        {
            return ((User32.HotKeyModifiers)0, (User32.VK)0);
        }
        string[] parts = text.Split('+');
        if (parts.Length != 2
            || !int.TryParse(parts[0], out int modifiers)
            || !int.TryParse(parts[1], out int key))
        {
            return null;
        }
        return ((User32.HotKeyModifiers)modifiers, (User32.VK)key);
    }


    private static string FormatHotkey(User32.HotKeyModifiers modifiers, User32.VK key) => $"{(int)modifiers}+{(int)key}";

    public static void UnregisterAll() => _pressed.Clear();

    public static Win32Error RegisterHotkey(nint hwnd, int id, User32.HotKeyModifiers modifiers, User32.VK key)
    {
        HotkeyInfo? info = GetHotkeyInfo(id);
        if (info is null)
        {
            // B75：未知 id 不再静默覆盖截图热键，按上游返回参数错误。
            return new Win32Error(160);
        }
        User32.HotKeyModifiers oldModifiers = info.Modifiers;
        User32.VK oldKey = info.Key;
        info.Modifiers = modifiers;
        info.Key = key;
        info.IsRegistered = true;
        _pressed.Remove(id);
        if (!X11KeyState.Available)
        {
            // 没有 X11（纯 Wayland）时如实失败，界面会显示「注册失败」，而不是假装成功。
            info.Error = new Win32Error(1409);
            return info.Error;
        }
        if (modifiers == 0 && key == 0)
        {
            info.IsRegistered = false;
            info.Error = default;
            return default;
        }
        byte keycode = X11KeyState.KeycodeForPublic(key);
        if (keycode == 0)
        {
            // B26：轮询层认不出的键，直接报失败，别让 UI 显示「已注册」却永远不触发。
            info.Error = new Win32Error(1409);
            Serilog.Log.Information("Hotkey {id}: key {key} has no X11 keycode", id, (int)key);
            return info.Error;
        }
        info.Error = default;
        // B56：只有注册成功且组合键确实变化时才落盘。
        if (oldModifiers != modifiers || oldKey != key)
        {
            SaveHotkey(info);
        }
        return default;
    }


    public static Win32Error UnregisterHotkey(nint hwnd, int id)
    {
        if (_hotkeys.TryGetValue(id, out HotkeyInfo? info))
        {
            // 轮询实现没有 OS 句柄可注销：停掉轮询触发即可。
            info.IsRegistered = false;
            info.Error = default;
        }
        _pressed.Remove(id);
        return default;
    }


    public static Win32Error DeleteHotkey(nint hwnd, int id)
    {
        if (_hotkeys.TryGetValue(id, out HotkeyInfo? info))
        {
            info.Modifiers = 0;
            info.Key = 0;
            info.IsRegistered = false;
            info.Error = default;
            // 和上游一致：写 "0" 表示这个热键被用户删除，下次启动不再注册。
            SaveHotkey(info);
        }
        _pressed.Remove(id);
        return default;
    }


    public static void InitializeHotkeyInput(Features.Setting.HotkeyInput hotkeyInput)
    {
        if (GetHotkeyInfo(hotkeyInput.HotkeyId) is HotkeyInfo info)
        {
            hotkeyInput.SetHotkey((uint)info.Modifiers, (uint)(int)info.Key);
            // B23：注册失败要让设置页显示警告，而不是看起来一切正常。
            hotkeyInput.State = info.Error.Succeeded ? Features.Setting.HoykeyInputState.None : Features.Setting.HoykeyInputState.Warning;
        }
    }


    private static HotkeyInfo GetOrCreateHotkeyInfo(int id)
    {
        if (_hotkeys.TryGetValue(id, out HotkeyInfo? existing))
        {
            return existing;
        }
        HotkeyInfo known = id == ShowMainWindow.Id ? ShowMainWindow : ScreenshotCapture;
        _hotkeys[id] = known;
        return known;
    }


    private static void SaveHotkey(HotkeyInfo info)
    {
        if (string.IsNullOrEmpty(info.ConfigSetting))
        {
            return;
        }
        try
        {
            AppConfig.SetValue(FormatHotkey(info.Modifiers, info.Key), info.ConfigSetting);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Failed to save hotkey {name}", info.ConfigSetting);
        }
    }

    public static HotkeyInfo? GetHotkeyInfo(int id) => _hotkeys.TryGetValue(id, out var info) ? info : null;

    /// <summary>每 25ms（`X11KeyState.PollIntervalMs`）看一次键盘状态：组合键「从松开到按下」的那一下触发一次，避免长按连发。</summary>
    internal static void PollOnce()
    {
        foreach (HotkeyInfo info in _hotkeys.Values.ToArray())
        {
            if (!info.IsRegistered || (info.Modifiers == 0 && info.Key == 0))
            {
                continue;
            }
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
                        // Uno 的 Restore/Show 在 X11 后端都不生效，真正起作用的是 EWMH 的激活请求（D47）。
                        if (!X11Window.Show(window.Title))
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
        public HotkeyInfo(string name, string configSetting, int id, User32.HotKeyModifiers modifiers, User32.VK key)
        {
            Name = name;
            ConfigSetting = configSetting;
            Id = id;
            DefaultModifiers = modifiers;
            DefaultKey = key;
            Modifiers = modifiers;
            Key = key;
        }

        public string Name { get; }

        /// <summary>AppConfig 里的字段名，用来持久化用户改过的组合键。</summary>
        public string ConfigSetting { get; }

        public int Id { get; set; }

        public User32.HotKeyModifiers DefaultModifiers { get; }

        public User32.VK DefaultKey { get; }

        public User32.HotKeyModifiers Modifiers { get; set; }

        public User32.VK Key { get; set; }

        public bool IsRegistered { get; set; }

        public Win32Error Error { get; set; }
    }
}
