// Lunarward：上游通过 WinRT 互操作给窗口挂 Mica/Acrylic 背景（Windows 专有）。
// Linux 侧没有等价的 SystemBackdrop API，这里用「回退色 + 半透明叠层」近似：
// 读取属性里的 FallbackColor，作为窗口内容背景；真正的合成器模糊（KWin blur）留到 M3。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Starward.Helpers;

public record SystemBackdropProperty
{
    public uint FallbackColorDark { get; init; }
    public uint FallbackColorLight { get; init; }
    public float LuminosityOpacityDark { get; init; }
    public float LuminosityOpacityLight { get; init; }
    public uint TintColorDark { get; init; }
    public uint TintColorLight { get; init; }
    public float TintOpacityDark { get; init; }
    public float TintOpacityLight { get; init; }

    public static readonly SystemBackdropProperty AcrylicDefault = new()
    {
        FallbackColorDark = 0xFF545454,
        FallbackColorLight = 0xFFD3D3D3,
        LuminosityOpacityDark = 0.64f,
        LuminosityOpacityLight = 0.64f,
        TintColorDark = 0xFF545454,
        TintColorLight = 0xFFD3D3D3,
        TintOpacityDark = 0,
        TintOpacityLight = 0,
    };

    public static readonly SystemBackdropProperty MicaDefault = new()
    {
        FallbackColorDark = 0xFF202020,
        FallbackColorLight = 0xFFF3F3F3,
        LuminosityOpacityDark = 1,
        LuminosityOpacityLight = 1,
        TintColorDark = 0xFF202020,
        TintColorLight = 0xFFF3F3F3,
        TintOpacityDark = 0.8f,
        TintOpacityLight = 0.5f,
    };

    public static readonly SystemBackdropProperty MicaAltDefault = new()
    {
        FallbackColorDark = 0xFF202020,
        FallbackColorLight = 0xFFE8E8E8,
        LuminosityOpacityDark = 1,
        LuminosityOpacityLight = 1,
        TintColorDark = 0xFF0A0A0A,
        TintColorLight = 0xFFDADADA,
        TintOpacityDark = 0,
        TintOpacityLight = 0,
    };
}

public class SystemBackdropHelper
{
    private readonly Window _window;

    private readonly SystemBackdropProperty _property;

    private bool _isDark = true;

    public SystemBackdropHelper(Window window, SystemBackdropProperty property)
    {
        _window = window;
        _property = property;
    }

    public SystemBackdropHelper TrySetAcrylic(bool isDark = true)
    {
        _isDark = isDark;
        Apply();
        return this;
    }

    public SystemBackdropHelper TrySetMica(bool isDark = true)
    {
        _isDark = isDark;
        Apply();
        return this;
    }

    private void Apply()
    {
        uint color = _isDark ? _property.FallbackColorDark : _property.FallbackColorLight;
        if (color == 0)
        {
            return;
        }
        if (_window.Content is FrameworkElement element)
        {
            element.Background = new SolidColorBrush(Color.FromArgb(
                (byte)((color >> 24) & 0xFF), (byte)((color >> 16) & 0xFF), (byte)((color >> 8) & 0xFF), (byte)(color & 0xFF)));
        }
    }
}

public static class ColorExtensions
{
    public static Color ToColor(this uint value)
        => Color.FromArgb((byte)((value >> 24) & 0xFF), (byte)((value >> 16) & 0xFF), (byte)((value >> 8) & 0xFF), (byte)(value & 0xFF));
}
