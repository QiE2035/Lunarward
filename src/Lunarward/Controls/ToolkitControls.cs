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
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        // Uno 7 的 ItemsPanelTemplate 只有 (object, NewFrameworkTemplateBuilder) 构造，而这个构造在
        // Debug 与 Release 的参考程序集里不一致（CS1729），所以不再替换 ItemsPanel；
        // 改为在模板应用后把默认的 ItemsStackPanel 调成横向（L-184）。
        if (ItemsPanelRoot is ItemsStackPanel panel)
        {
            panel.Orientation = Orientation.Horizontal;
        }
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
/// <summary>
/// 上游来自 CommunityToolkit.WinUI.Controls 的折行布局。
/// 这里用 NonVirtualizingLayout 实现（ItemsRepeater 支持）：按可用宽度折行，行高取该行最高项。
/// 之前这个类是空壳，ItemsRepeater 里的标签会全部叠在 (0,0)（L-184）。
/// </summary>
public partial class WrapLayout : Microsoft.UI.Xaml.Controls.NonVirtualizingLayout
{
    public double HorizontalSpacing { get; set; }

    public double VerticalSpacing { get; set; }

    protected override Size MeasureOverride(Microsoft.UI.Xaml.Controls.NonVirtualizingLayoutContext context, Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? double.PositiveInfinity : availableSize.Width;
        double x = 0, y = 0, rowHeight = 0;
        for (int i = 0; i < context.Children.Count; i++)
        {
            UIElement child = context.Children[i];
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size size = child.DesiredSize;
            if (x > 0 && x + size.Width > width)
            {
                x = 0;
                y += rowHeight + VerticalSpacing;
                rowHeight = 0;
            }
            x += size.Width + HorizontalSpacing;
            rowHeight = Math.Max(rowHeight, size.Height);
        }
        return new Size(double.IsInfinity(width) ? x : width, y + rowHeight);
    }

    protected override Size ArrangeOverride(Microsoft.UI.Xaml.Controls.NonVirtualizingLayoutContext context, Size finalSize)
    {
        double x = 0, y = 0, rowHeight = 0;
        for (int i = 0; i < context.Children.Count; i++)
        {
            UIElement child = context.Children[i];
            Size size = child.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width)
            {
                x = 0;
                y += rowHeight + VerticalSpacing;
                rowHeight = 0;
            }
            child.Arrange(new Rect(x, y, size.Width, size.Height));
            x += size.Width + HorizontalSpacing;
            rowHeight = Math.Max(rowHeight, size.Height);
        }
        return finalSize;
    }
}


/// <summary>上游 CommunityToolkit 的 EqualPanel：一行塞满等宽格子，塞不下就换行（GridView 的分类卡片用）。</summary>
public partial class EqualPanel : Panel
{
    public double Spacing { get; set; } = 8;

    public double MinItemWidth { get; set; } = 120;

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = double.IsInfinity(availableSize.Width) ? MinItemWidth : availableSize.Width;
        int columns = Math.Max(1, (int)Math.Floor((width + Spacing) / (MinItemWidth + Spacing)));
        double itemWidth = Math.Max(1, (width - (columns - 1) * Spacing) / columns);
        double y = 0, rowHeight = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            UIElement child = Children[i];
            child.Measure(new Size(itemWidth, double.PositiveInfinity));
            rowHeight = Math.Max(rowHeight, child.DesiredSize.Height);
            if ((i + 1) % columns == 0)
            {
                y += rowHeight + Spacing;
                rowHeight = 0;
            }
        }
        if (Children.Count % columns != 0)
        {
            y += rowHeight;
        }
        return new Size(width, Math.Max(0, y - (Children.Count > 0 ? Spacing : 0)));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double width = double.IsInfinity(finalSize.Width) ? MinItemWidth : finalSize.Width;
        int columns = Math.Max(1, (int)Math.Floor((width + Spacing) / (MinItemWidth + Spacing)));
        double itemWidth = Math.Max(1, (width - (columns - 1) * Spacing) / columns);
        double x = 0, y = 0, rowHeight = 0;
        for (int i = 0; i < Children.Count; i++)
        {
            UIElement child = Children[i];
            double height = child.DesiredSize.Height;
            child.Arrange(new Rect(x, y, itemWidth, height));
            rowHeight = Math.Max(rowHeight, height);
            x += itemWidth + Spacing;
            if ((i + 1) % columns == 0)
            {
                x = 0;
                y += rowHeight + Spacing;
                rowHeight = 0;
            }
        }
        return finalSize;
    }
}
