using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using System.Runtime.CompilerServices;

namespace Starward.Helpers;

// Lunarward: partial 是必需的，不是装饰——去掉会 CS0260（同一编译单元里另有一处分部声明，来源未查明，见 18-upstream-table.md 复核记录）
public partial class PointerCursor : DependencyObject
{
    public static readonly DependencyProperty CursorShapeProperty =
        DependencyProperty.RegisterAttached("CursorShape", typeof(InputSystemCursorShape), typeof(PointerCursor), new PropertyMetadata(default));

    public static void SetCursorShape(UIElement element, InputSystemCursorShape value)
    {
        SetProtectedCursor(element, InputSystemCursor.Create(value));
        element.SetValue(CursorShapeProperty, value);
    }

    public static InputSystemCursorShape GetCursorShape(UIElement element)
    {
        return element.GetValue(CursorShapeProperty) switch
        {
            InputSystemCursorShape e => e,
            _ => InputSystemCursorShape.Arrow,
        };
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "set_ProtectedCursor")]
    static extern void SetProtectedCursor(UIElement element, InputCursor cursor);
}
