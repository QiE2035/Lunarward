// Lunarward：手柄控制依赖 Windows GameInput（SharpGameInput），Linux 暂不可用。
// 这里镜像上游公开成员，使设置页可用；真实手柄支持计划走 evdev/uinput（M4）。
using Microsoft.UI.Dispatching;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Starward.Features.GamepadControl;

public static class GamepadController
{
    public static void Initialize(DispatcherQueue? dispatcherQueue = null) { }

    public static bool Initialized { get; private set; }

    public static bool NeedInstallGameInputRedist => false;

    public static bool GameInputRedistInstalled => true;

    public static bool GameInputRedistOutdate => false;

    public static bool EnableGamepadSimulateInput { get; set; }

    public static bool GamepadConnected { get; set; }

    public static int GamepadGuideButtonMode { get; set; }

    public static int GamepadShareButtonMode { get; set; }

    public static (string[] Modifiers, string[] Keys) GamepadGuideButtonMapKeys { get; set; }

    public static (string[] Modifiers, string[] Keys) GamepadShareButtonMapKeys { get; set; }

    public static void Start() { }

    public static void RestoreGamepadGuideButtonForGameBar() { }

    public static void Stop() { }

    public static bool SetGamepadGuideButtonMapKeys(string? value, [NotNullWhen(true)] out string? keysTextOrErrorKey)
    {
        keysTextOrErrorKey = "Gamepad_NotSupportedOnLinux";
        return false;
    }

    public static bool SetGamepadShareButtonMapKeys(string? value, [NotNullWhen(true)] out string? keysTextOrErrorKey)
    {
        keysTextOrErrorKey = "Gamepad_NotSupportedOnLinux";
        return false;
    }

    public static string GetGamepadGuideButtonMapKeysText() => "";

    public static string GetGamepadShareButtonMapKeysText() => "";

    public static readonly Dictionary<string, int> KeyNameToVirtualKey = new();
}
