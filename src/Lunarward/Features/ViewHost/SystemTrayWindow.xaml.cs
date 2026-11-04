using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Starward.Features.Setting;
using Starward.Frameworks;
using Starward.Helpers;
using Vanara.PInvoke;
using Windows.Foundation;


namespace Starward.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class SystemTrayWindow : WindowEx
{




    public SystemTrayWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
        SetTrayIcon();
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, (_, _) => this.Bindings.Update());
    }




    private unsafe void InitializeWindow()
    {
        new SystemBackdropHelper(this, SystemBackdropProperty.AcrylicDefault with
        {
            TintColorLight = 0xFFE7E7E7,
            TintColorDark = 0xFF404040
        }).TrySetAcrylic(true);

        AppWindow.IsShownInSwitchers = false;
        AppWindow.Closing += (s, e) => e.Cancel = true;
        this.Activated += SystemTrayWindow_Activated;
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsMinimizable = false;
            presenter.IsResizable = false;
            presenter.IsAlwaysOnTop = true;
        }

        // Lunarward：上游用 GetWindowLongPtr/SetWindowLong 去掉标题栏边框、用 DwmSetWindowAttribute
        // 设圆角。Linux 下这两件事都由 WindowEx 的无边框窗口 + 合成器负责（D30）。

        Show();
        Hide();
    }



    private void SetTrayIcon()
    {
        try
        {
            // Lunarward：上游从 exe 资源里 LoadIcon 取托盘图标。Linux 托盘 M2 走
            // org.kde.StatusNotifierItem，图标由桌面主题/IconPixmap 提供（D30）。
            trayIcon.ToolTipText = "Lunarward";
        }
        catch { }
    }




    private void SystemTrayWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState.ToString() == "Deactivated")
        {
            Hide();
        }
    }



    [RelayCommand]
    public override void Show()
    {
        RootGrid.RequestedTheme = ShouldSystemUseDarkMode() ? ElementTheme.Dark : ElementTheme.Light;
        // Lunarward：上游用 Win32 计算托盘菜单弹出位置。Linux 的托盘由
        // org.kde.StatusNotifierItem 提供（M2 实装），菜单定位交给合成器，
        // 这里退化为「贴右上角显示」，M2 换成托盘协议给出的坐标。
        RootGrid.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        int width = (int)(RootGrid.DesiredSize.Width * UIScale);
        int height = (int)(RootGrid.DesiredSize.Height * UIScale);
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
        if (area is not null)
        {
            AppWindow.Move(new Windows.Graphics.PointInt32
            {
                X = area.WorkArea.X + area.WorkArea.Width - width,
                Y = area.WorkArea.Y,
            });
        }
        base.Show();
    }



    [RelayCommand]
    public override void Hide()
    {
        base.Hide();
    }



    [RelayCommand]
    public void ShowMainWindow()
    {
        App.Current.EnsureMainWindow();
    }


    [RelayCommand]
    private void Exit()
    {
        App.Current.Exit();
    }


    private void WindowEx_Closed(object sender, WindowEventArgs args)
    {
        trayIcon?.Dispose();
    }


}
