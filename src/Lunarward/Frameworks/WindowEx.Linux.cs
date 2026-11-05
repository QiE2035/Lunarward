// Lunarward：WindowEx 的 Linux 实现。上游 Frameworks/WindowEx.cs 依赖 Vanara/User32 子类化，保留但不编译。
// 这里用 Uno 的 Window.AppWindow 完成窗口动作；Windows 专有的图标/拖拽区/标题栏配色降级为 no-op。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.Graphics;

namespace Starward.Frameworks;

public abstract partial class WindowEx : Window
{
    public IntPtr WindowHandle => IntPtr.Zero;

    public double UIScale => Content?.XamlRoot?.RasterizationScale ?? 1d;

    public static Microsoft.UI.WindowId MainWindowId { get; protected set; }

    protected WindowEx()
    {
        AppWindow.Closing += (_, _) => { };
    }

    public virtual void Show()
    {
        // Lunarward：Uno 的 AppWindow.Show() 只在首次调用时 map（内部 WasShown 之后就只 Activate），
        // 而 Activate() 只发 XRaiseWindow，恢复不了被隐藏/最小化的窗口（L-84）。
        // 这里用 Helpers.X11Window（libxcb）做 map + _NET_ACTIVE_WINDOW，失败再退回 Uno 的实现。
        if (!Helpers.X11Window.Show(Title))
        {
            AppWindow.Show();
        }
        Activate();
    }

    public virtual void Hide()
    {
        // AppWindow.Hide() 在 Uno 6.7.135 的 X11 后端上是空操作（D47）。
        if (!Helpers.X11Window.Hide(Title))
        {
            AppWindow.Hide();
        }
    }

    public virtual void Minimize()
    {
        // 最小化本身用 presenter.Minimize()（实测有效，窗口立刻 IsUnMapped）；
        // 注意 presenter.Restore() 并不能把它 map 回来（实测），唤回统一走 Show() → X11Window。
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
    }


    public virtual void ShowFromMinimized()
    {
        try
        {
            Show();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "ShowFromMinimized failed");
        }
    }

    public virtual void CenterInScreen(int? width = null, int? height = null)
    {
        // Uno 桌面端未暴露屏幕尺寸，先只处理尺寸，位置交给合成器。
        if (width is int w && height is int h)
        {
            // Lunarward：Uno 的 X11 后端里 MoveAndResize 是空操作，只有 Resize 真正改窗口尺寸。
            // 参数是 DIP，AppWindow 要物理像素，故按 UIScale 放大（否则 1.25 缩放下内容被挤）。
            double scale = UIScale <= 0 ? 1 : UIScale;
            AppWindow.Resize(new SizeInt32 { Width = (int)Math.Round(w * scale), Height = (int)Math.Round(h * scale) });
            AppWindow.MoveAndResize(new RectInt32 { X = AppWindow.Position.X, Y = AppWindow.Position.Y, Width = (int)Math.Round(w * scale), Height = (int)Math.Round(h * scale) });
        }
    }

    public void SetIcon(string? iconPath = null) { }

    public void SetDragRectangles(params RectInt32[] value) { }

    public void AdaptTitleBarButtonColorToActuallTheme() { }

    public virtual void ChangeAccentColor(Windows.UI.Color? backColor = null, Windows.UI.Color? foreColor = null) { }

    protected static bool ShouldAppsUseDarkMode() => true;

    protected static bool ShouldSystemUseDarkMode() => true;
}
