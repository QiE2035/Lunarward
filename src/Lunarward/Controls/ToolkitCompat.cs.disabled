// Lunarward：CommunityToolkit.WinUI 在 Linux 上不可用（其 TFM 为 net8.0-windows），
// 这里提供上游 XAML 实际用到的少数附加属性，保持页面 XAML 不改。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CommunityToolkit.WinUI;

public static class UIElementExtensions
{
    public static bool GetClipToBounds(DependencyObject obj) => (bool)obj.GetValue(ClipToBoundsProperty);

    public static void SetClipToBounds(DependencyObject obj, bool value) => obj.SetValue(ClipToBoundsProperty, value);

    public static readonly DependencyProperty ClipToBoundsProperty =
        DependencyProperty.RegisterAttached("ClipToBounds", typeof(bool), typeof(UIElementExtensions),
            new PropertyMetadata(false, OnClipToBoundsChanged));

    private static void OnClipToBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe || e.NewValue is not true)
        {
            return;
        }
        void Apply()
        {
            if (fe.ActualWidth > 0 && fe.ActualHeight > 0)
            {
                fe.Clip = new RectangleGeometry { Rect = new Windows.Foundation.Rect(0, 0, fe.ActualWidth, fe.ActualHeight) };
            }
        }
        fe.SizeChanged += (_, _) => Apply();
        Apply();
    }
}

public static class VisualExtensions
{
    // 上游仅用于 3D/透视相关的 Normalize，Linux 侧先接受参数、不产生视觉影响。
    public static void SetNormalize(DependencyObject obj, object? value) => obj.SetValue(NormalizeProperty, value);

    public static object? GetNormalize(DependencyObject obj) => obj.GetValue(NormalizeProperty);

    public static readonly DependencyProperty NormalizeProperty =
        DependencyProperty.RegisterAttached("Normalize", typeof(object), typeof(VisualExtensions), new PropertyMetadata(null));

    public static void SetNormalizedCenterPoint(DependencyObject obj, object? value) => obj.SetValue(NormalizedCenterPointProperty, value);

    public static object? GetNormalizedCenterPoint(DependencyObject obj) => obj.GetValue(NormalizedCenterPointProperty);

    public static readonly DependencyProperty NormalizedCenterPointProperty =
        DependencyProperty.RegisterAttached("NormalizedCenterPoint", typeof(object), typeof(VisualExtensions), new PropertyMetadata(null));
}

/// <summary>AttachedDropShadow 的 Linux 占位实现：接受同名字段，真正阴影由 ThemeShadow/合成器承担。</summary>
public partial class AttachedDropShadow : Microsoft.UI.Xaml.DependencyObject
{
    public double BlurRadius { get; set; }
    public object? CastTo { get; set; }
    public object? Offset { get; set; }
    public Windows.UI.Color? Color { get; set; }
}

/// <summary>
/// CommunityToolkit 的 Effects 附加属性（`cu:Effects.Shadow`）。
/// 上游用它把 AttachedDropShadow 挂到元素上；Linux 上真正的阴影由 ThemeShadow/合成器承担，
/// 这里只提供同名附加属性让 XAML 能编译并保持结构（D30：补名字不补行为）。
/// </summary>
public static class Effects
{
    public static readonly Microsoft.UI.Xaml.DependencyProperty ShadowProperty =
        Microsoft.UI.Xaml.DependencyProperty.RegisterAttached("Shadow", typeof(object),
            typeof(Effects), new Microsoft.UI.Xaml.PropertyMetadata(null));

    public static object? GetShadow(Microsoft.UI.Xaml.DependencyObject element)
        => element.GetValue(ShadowProperty);

    public static void SetShadow(Microsoft.UI.Xaml.DependencyObject element, object? value)
        => element.SetValue(ShadowProperty, value);
}
