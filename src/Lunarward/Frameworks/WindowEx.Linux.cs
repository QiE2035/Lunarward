// Lunarward：WindowEx 的 Linux 实现。上游 Frameworks/WindowEx.cs 依赖 Vanara/User32 子类化，保留但不编译。
// 这里用 Uno 的 Window.AppWindow 完成窗口动作；Windows 专有的图标/拖拽区/标题栏配色降级为 no-op。
using CommunityToolkit.Mvvm.Messaging;
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
        // Lunarward：上游各个窗口都在 InitializeWindow 里打开 ExtendsContentIntoTitleBar，指望自绘标题栏
        // 提供拖动/关闭；Uno 的 X11 后端没有这套实现，结果是既没有 WM 装饰也没有自绘按钮 → 窗口
        // 拖不动、也关不掉（真机复现）。这里统一交还系统标题栏（D49）。
        try
        {
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = false;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "disable content-into-titlebar failed");
        }
        bool shown = Helpers.X11Window.Show(Title);
        if (!shown)
        {
            AppWindow.Show();
        }
        Activate();
        ClampToScreenInBackground();
        // X11 后端在「窗口已存在、只是被 unmap」的情况下不会触发 Window.Activated，
        // 于是隐藏→唤回时没人发激活消息：视频壁纸停在暂停状态、其它依赖激活的动画也不恢复（实测）。
        // 这里在真正 map 成功后补发一条，语义与 MainWindow.OnWindowActivated 一致。
        if (shown)
        {
            WeakReferenceMessenger.Default.Send(new Features.ViewHost.MainWindowStateChangedMessage
            {
                Activate = true,
                CurrentTime = DateTimeOffset.Now,
            });
        }
    }

    /// <summary>
    /// Lunarward：X11 下窗口可能被摆到屏幕外（Uno 的 MoveAndResize 是空操作，resize 之后没人重新摆位），
    /// 这里在 map 之后把「大于屏幕 90% 的窗口」缩回来并居中（D49）。
    /// </summary>
    private void ClampToScreenInBackground()
    {
        _ = ClampToScreenAsync();
    }


    /// <summary>
    /// Lunarward：窗口想要的初始尺寸（DIP）。上游各窗口靠 InitializeWindow/CenterInScreen 自己设尺寸，
    /// 而 Uno 的 X11 后端在 map 之后才应用默认尺寸，这里在 map 后按 DIP×缩放补一次（L-35、D49）。
    /// </summary>
    protected virtual (int Width, int Height)? DefaultWindowSizeInDips => null;


    private async System.Threading.Tasks.Task ClampToScreenAsync()
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(350);
            if (!Helpers.X11Window.TryGetScreenSize(out int screenWidth, out int screenHeight))
            {
                Serilog.Log.Warning("clamp: xcb_get_geometry(root) failed");
                return;
            }
            if (DefaultWindowSizeInDips is (int dipWidth, int dipHeight))
            {
                double scale = UIScale <= 0 ? 1 : UIScale;
                AppWindow.Resize(new SizeInt32 { Width = (int)Math.Round(dipWidth * scale), Height = (int)Math.Round(dipHeight * scale) });
                await System.Threading.Tasks.Task.Delay(120);
            }
            // 窗口已经 map 出来：补上 _NET_WM_ICON（Uno 的 X11 后端不做这件事）
            Helpers.AppIcon.Apply(Title);
            SizeInt32 size = AppWindow.Size;
            int maxWidth = Math.Max(400, (int)(screenWidth * 0.9));
            int maxHeight = Math.Max(300, (int)(screenHeight * 0.9));
            int width = Math.Min(size.Width, maxWidth);
            int height = Math.Min(size.Height, maxHeight);
            if (width == size.Width && height == size.Height)
            {
                Serilog.Log.Information("clamp: size {Width}x{Height} fits screen {ScreenWidth}x{ScreenHeight}", size.Width, size.Height, screenWidth, screenHeight);
                return;
            }
            AppWindow.Resize(new SizeInt32 { Width = width, Height = height });
            Helpers.X11Window.SetBounds(Title, Math.Max(0, (screenWidth - width) / 2), Math.Max(0, (screenHeight - height) / 2), width, height);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "clamp window to screen failed");
        }
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
