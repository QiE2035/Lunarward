// Lunarward：上游通过 XamlRoot 取 HWND 做窗口句柄相关操作；Linux 无 HWND，返回 0。
using Microsoft.UI.Xaml;
using System;

namespace Starward.Helpers;

public static class XamlRootExtension
{
    public static nint GetWindowHandle(this XamlRoot? xamlRoot) => 0;

    /// <summary>D134：上游用 `GetDpiForWindow` 算缩放；Linux 侧按 XamlRoot 的 RasterizationScale 取。</summary>
    public static float GetUIScaleFactor(this XamlRoot? xamlRoot)
    {
        try
        {
            return (float)(xamlRoot?.RasterizationScale ?? 1.0);
        }
        catch (Exception)
        {
            return 1f;
        }
    }

    /// <summary>Linux 上 AppWindow 仍由 Uno 提供（窗口装饰/尺寸），从 XamlRoot 反查。</summary>
    public static Microsoft.UI.Windowing.AppWindow? GetAppWindow(this XamlRoot? xamlRoot)
    {
        if (xamlRoot is null)
        {
            return null;
        }
        try
        {
            var id = xamlRoot.ContentIslandEnvironment.AppWindowId;
            return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
        }
        catch (Exception)
        {
            // Uno 的 ContentIslandEnvironment 目前是 NotImplemented，改查我们自己的窗口登记表。
            // L-183：以前无条件回落到主窗口，子窗口/对话框拿到的是主窗口的 id（DisplayInformation 会按主窗口算 DPI）。
            if (Features.WindowBase.FindAppWindow(xamlRoot) is { } owner)
            {
                return owner;
            }
            try
            {
                return Features.ViewHost.MainWindow.Current?.AppWindow;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>Linux 无「标题栏拖拽区」概念，no-op。</summary>
    public static void SetWindowDragRectangles(this XamlRoot? xamlRoot, Windows.Foundation.Rect[]? rects) { }
}
