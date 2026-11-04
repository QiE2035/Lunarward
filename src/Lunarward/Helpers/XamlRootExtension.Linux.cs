// Lunarward：上游通过 XamlRoot 取 HWND 做窗口句柄相关操作；Linux 无 HWND，返回 0。
using Microsoft.UI.Xaml;
using System;

namespace Starward.Helpers;

public static class XamlRootExtension
{
    public static nint GetWindowHandle(this XamlRoot? xamlRoot) => 0;

    /// <summary>Linux 上 AppWindow 仍由 Uno 提供（窗口装饰/尺寸），从 XamlRoot 反查。</summary>
    public static Microsoft.UI.Windowing.AppWindow? GetAppWindow(this XamlRoot? xamlRoot)
    {
        if (xamlRoot is null)
        {
            return null;
        }
        try
        {
            // Uno 的 XamlRoot.ContentIslandEnvironment 目前是 NotImplemented，直接兜底到主窗口
            var id = xamlRoot.ContentIslandEnvironment.AppWindowId;
            return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
        }
        catch (Exception)
        {
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
