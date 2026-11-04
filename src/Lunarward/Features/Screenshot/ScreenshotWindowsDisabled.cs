// Lunarward：截图子系统的窗口页在 M1 不参与编译（D31 / 12-shims-and-gaps.md ⛔）。
// 这里只保留被其它已启用页面引用到的窗口类型，行为为空——不假装能看图。
// M3 用 SkiaSharp 重画整套查看/转码界面后，这些类型会被真实实现取代。
using Microsoft.UI.Xaml;
using Starward.Frameworks;
using System;

namespace Starward.Features.Screenshot;

/// <summary>看图窗口：M3 重写。当前仅让工具箱的入口可编译。</summary>
public sealed class ImageViewWindow2 : WindowEx
{
    public ImageViewWindow2()
    {
        Title = "图片查看（M3 实现）";
        Content = new Microsoft.UI.Xaml.Controls.TextBlock
        {
            Text = "图片查看器将在 M3 用 SkiaSharp 重写。",
            Margin = new Thickness(24),
        };
    }

    /// <summary>上游按 AppWindowId 定位窗口；Linux 无该概念，参数忽略。</summary>
    public void ShowWindow(Microsoft.UI.WindowId windowId) => Activate();
}
