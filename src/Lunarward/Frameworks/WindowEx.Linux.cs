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
        // Lunarward：Uno 的 AppWindow.Show() 在 X11 上对「被 unmap 过的窗口」不一定重新 map，
        // 先让 xdotool 把它 map 回来（见 Helpers/X11Window.cs 与 L-84），失败再退回 Uno 的实现。
        if (!Helpers.X11Window.Show(Title))
        {
            AppWindow.Show();
        }
        Activate();
    }

    public virtual void Hide()
    {
        // Lunarward：AppWindow.Hide() 与 Window.Hide() 在 Uno 6.7.135 的 X11 后端上都是空操作
        // （实测调用后 Map State 仍是 IsViewable，L-84）。真正生效的是对 X11 窗口 unmap。
        if (!Helpers.X11Window.Hide(Title))
        {
            AppWindow.Hide();
        }
    }

    public virtual void Minimize()
    {
        // 用 AppWindow.Hide() 而不是 presenter.Minimize()：Uno 6.7.135 的 OverlappedPresenter.Restore()
        // 不会把 X11 窗口重新 map 回来（实测 Map State 一直是 IsUnMapped），配合 Hide/Show 才能来回。
        // AppWindow.Hide() 在 Uno 6.7.135 的 X11 后端上是空操作（实测窗口仍 IsViewable），
        // 所以最小化仍用 presenter.Minimize()；唤回走 X11KeyState.MapWindowByTitle（见 L-45）。
        if (AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.Minimize();
        }
    }


    public virtual void ShowFromMinimized()
    {
        try
        {
            if (!Helpers.X11Window.Show(Title))
            {
                AppWindow.Show();
            }
            Activate();
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
