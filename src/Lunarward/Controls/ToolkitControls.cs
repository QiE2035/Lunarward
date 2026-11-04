// Lunarward：CommunityToolkit.WinUI.Controls 在 Linux 上无可用版本（TFM 为 net8.0-windows）。
// 这里实现上游实际用到的三个控件：Segmented / SegmentedItem / WrapPanel，保持 XAML 不改。
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;

namespace CommunityToolkit.WinUI.Controls;

/// <summary>Segmented 的选项：等价于 ListViewItem。</summary>
public partial class SegmentedItem : ListViewItem
{
}

/// <summary>
/// 简易 Segmented：基于 ListView，横向排列，支持多选（上游用 SelectedItems 判定）。
/// </summary>
public partial class Segmented : ListView
{
    public Segmented()
    {
        SelectionMode = ListViewSelectionMode.Multiple;
        ItemsPanel = new ItemsPanelTemplate(() => new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0 });
    }
}

/// <summary>简易 WrapPanel：按可用宽度折行，支持间距。</summary>
public partial class WrapPanel : Panel
{
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    public double HorizontalSpacing { get; set; }

    public double VerticalSpacing { get; set; }

    public double? ItemWidth { get; set; }

    public double? ItemHeight { get; set; }

    protected override Size MeasureOverride(Size availableSize)
    {
        var lineWidth = 0.0;
        var lineHeight = 0.0;
        var totalWidth = 0.0;
        var totalHeight = 0.0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }
            var w = ItemWidth ?? double.PositiveInfinity;
            var h = ItemHeight ?? double.PositiveInfinity;
            child.Measure(new Size(w, h));
            var size = child.DesiredSize;
            var cw = ItemWidth ?? size.Width;
            var ch = ItemHeight ?? size.Height;

            if (lineWidth > 0 && lineWidth + HorizontalSpacing + cw > availableSize.Width)
            {
                totalWidth = System.Math.Max(totalWidth, lineWidth);
                totalHeight += lineHeight + VerticalSpacing;
                lineWidth = cw;
                lineHeight = ch;
            }
            else
            {
                lineWidth += (lineWidth > 0 ? HorizontalSpacing : 0) + cw;
                lineHeight = System.Math.Max(lineHeight, ch);
            }
        }

        totalWidth = System.Math.Max(totalWidth, lineWidth);
        totalHeight += lineHeight;
        return new Size(double.IsInfinity(totalWidth) ? 0 : totalWidth, double.IsInfinity(totalHeight) ? 0 : totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var x = 0.0;
        var y = 0.0;
        var lineHeight = 0.0;

        foreach (var child in Children)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }
            var size = child.DesiredSize;
            var cw = ItemWidth ?? size.Width;
            var ch = ItemHeight ?? size.Height;

            if (x > 0 && x + HorizontalSpacing + cw > finalSize.Width)
            {
                x = 0;
                y += lineHeight + VerticalSpacing;
                lineHeight = 0;
            }
            else if (x > 0)
            {
                x += HorizontalSpacing;
            }

            child.Arrange(new Windows.Foundation.Rect(x, y, cw, ch));
            x += cw;
            lineHeight = System.Math.Max(lineHeight, ch);
        }

        return finalSize;
    }
}

/// <summary>
/// CommunityToolkit 的 WrapLayout 在 Uno 上不可用：Uno 的 VirtualizingLayout 不会调用
/// MeasureOverride/ArrangeOverride，自定义 Layout 实际不生效（已实测，见 12-shims-and-gaps.md）。
/// 需要换行排布的地方改用本文件的 <see cref="WrapPanel"/>（ItemsPanelTemplate 里用）。
/// 保留这个类型只是为了让上游 XAML 里可能残留的引用仍能编译。
/// </summary>
public partial class WrapLayout : Microsoft.UI.Xaml.Controls.VirtualizingLayout
{
}
