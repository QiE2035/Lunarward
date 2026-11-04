// Lunarward：Win2D 的 CanvasSwapChainPanel 在 Linux 无对应实现。
// 这里提供同名占位控件（Panel），使 XAML 可编译；实际截图/背景渲染在 M2/M3 用 SkiaSharp 重写。
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.Graphics.Canvas.UI.Xaml;

public partial class CanvasSwapChainPanel : Panel
{
}
