using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AppLifecycle;
using Starward.Features.Background;
using Starward.Features.Database;
using Starward.Features.GameLauncher;
using Starward.Features.Overlay;
using Starward.Features.Screenshot;
using Starward.Frameworks;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Vanara.PInvoke;
using Windows.Graphics;


namespace Starward.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class MainWindow : WindowEx
{


    public static new MainWindow Current { get; private set; }


    public MainWindow()
    {
        Current = this;
        MainWindowId = AppWindow.Id;
        this.InitializeComponent();
        this.Activated += OnWindowActivated;
        InitializeMainWindow();
        this.Activated += OnFirstActivated;
        this.Activated += OnIconActivatedOnce;
        if (Environment.GetEnvironmentVariable("LUNARWARD_SELFTEST") is "1")
        {
            this.Activated += OnSelfTestActivated;
        }
        App.Current.EnsureSystemTray();
        RunDebugWindowAction();
        WeakReferenceMessenger.Default.Register<AccentColorChangedMessage>(this, OnAccentColorChanged);
        WeakReferenceMessenger.Default.Register<GameStartedMessage>(this, OnGameStarted);
    }


    private static bool _selfTestStarted;


    /// <summary>
    /// `--selftest-ui`：进程内 UI 自检（L1.5）。Uno 的 Skia/X11 后端没有 AT-SPI 树，
    /// 只能像 Uno 自己的 RuntimeTests 那样在进程内断言，所以这里自己读视觉树/布局结果。
    /// </summary>
    private void OnSelfTestActivated(object? sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (_selfTestStarted)
        {
            return;
        }
        _selfTestStarted = true;
        _ = RunSelfTestAsync();
    }


    private async System.Threading.Tasks.Task RunSelfTestAsync()
    {
        await System.Threading.Tasks.Task.Delay(600);
        int failed;
        try
        {
            failed = await Features.Dev.UiSelfTest.RunAsync(this, TimeSpan.FromSeconds(1));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAIL  自检本身抛异常    {ex.GetType().Name}: {ex.Message}");
            failed = 1;
        }
        // 自检是有终态的批处理模式：跑完直接退出，避免 CI 里挂住
        Environment.Exit(failed == 0 ? 0 : 1);
    }


    private static bool _iconApplied;


    /// <summary>
    /// 第一次激活时给窗口补上 _NET_WM_ICON。Uno 的 X11 后端不做这件事，
    /// 任务栏/Alt-Tab 里就只有通用占位方块（真机截图可见）。
    /// </summary>
    private void OnIconActivatedOnce(object? sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
    {
        if (_iconApplied)
        {
            return;
        }
        _iconApplied = true;
        _ = System.Threading.Tasks.Task.Delay(300).ContinueWith(_ => Helpers.AppIcon.Apply(Title), System.Threading.Tasks.TaskScheduler.Default);
    }


    private void InitializeMainWindow()
    {
        Title = "Lunarward";
        // Lunarward：不接管标题栏。上游在 Windows 上 ExtendsContentIntoTitleBar = true 后靠
        // AppWindow.TitleBar.SetDragRectangles 提供拖拽区，而 Uno 的 X11 后端没有实现该 API
        // （WindowEx.SetDragRectangles 只能是空实现），结果就是「拖不动 + 自绘按钮点不动」。
        // 改成不扩展内容区，由窗口管理器绘制真正的标题栏（拖拽/最小化/关闭全部免费且可靠）。
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = false;
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.Closing += AppWindow_Closing;
        Content.KeyDown += Content_KeyDown;
        CenterInScreen(1200, 676);
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }
    }



    /// <summary>Lunarward：窗口首次激活时才有 RasterizationScale，此时按缩放换算物理尺寸（L-35）。</summary>
    private void OnFirstActivated(object sender, WindowActivatedEventArgs e)
    {
        this.Activated -= OnFirstActivated;
        try
        {
            double scale = Content?.XamlRoot?.RasterizationScale ?? 1;
            if (scale <= 0)
            {
                scale = 1;
            }
            AppWindow.Resize(new SizeInt32 { Width = (int)Math.Round(1200 * scale), Height = (int)Math.Round(676 * scale) });

        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "resize main window failed");
        }
    }


    /// <summary>LUNARWARD_WINDOW_ACTION=minimize|close：无人值守验证窗口动作（xdotool 在 XWayland 下点不到应用）。</summary>
    private async void RunDebugWindowAction()
    {
        string? act = Starward.Features.Dev.DevSwitches.WindowAction;
        if (string.IsNullOrEmpty(act))
        {
            return;
        }
        await Task.Delay(3000);
        try
        {
            if (act == "minimize")
            {
                Minimize();
            }
            else if (act == "close")
            {
                Close();
            }
            else if (act == "gamesetting")
            {
                // 开发期直达：打开「游戏设置」对话框（验证布局用，见 12 号表）
                var dialog = new Features.GameLauncher.GameLauncherSettingDialog
                {
                    CurrentGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(AppConfig.CurrentGameBiz),
                    CurrentGameBiz = AppConfig.CurrentGameBiz,
                    XamlRoot = (Content as Microsoft.UI.Xaml.FrameworkElement)?.XamlRoot,
                };
                _ = dialog.ShowAsync();
            }
            else if (act == "backgroundview")
            {
                // 开发期直达：打开背景图库窗口
                new Features.Background.BackgroundViewWindow().Show();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[window-action] {action} failed", act);
        }
    }


    public override void Show()
    {
        double uiScale = UIScale;
        if (Math.Abs(AppWindow.Size.Width - 1200 * uiScale) > 10 || Math.Abs(AppWindow.Size.Height - 676 * uiScale) > 10)
        {
            CenterInScreen(1200, 676);
        }
        base.Show();
    }



    public void ShowByGamepad()
    {
        CenterInScreen(1200, 676);
        // Lunarward：Linux 无 SetCursorPos，光标交给合成器（D30 补名字不补行为）。
        base.Show();
    }



    private void OnGameStarted(object _, GameStartedMessage __)
    {
        StartGameAction action = AppConfig.StartGameAction;
        if (action is StartGameAction.Hide)
        {
            this.Hide();
        }
        else if (action is StartGameAction.Minimize)
        {
            this.Minimize();
        }
    }



    private async void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        try
        {
            MainWindowCloseOption option = AppConfig.CloseWindowOption;
            // C1：配置是「隐藏到托盘」但没有可用的托盘宿主时，隐藏就等于把自己关进笼子
            //（只能靠 `lunarward --show` 捞回来）。这种情况下让窗口正常关闭、进程随之退出。
            if (option is MainWindowCloseOption.Hide && !Features.Tray.StatusNotifierTray.Available)
            {
                Serilog.Log.Warning("关闭窗口：托盘不可用，隐藏会无法唤回，改为直接退出");
                return;
            }
            args.Cancel = true;
            if (option is not MainWindowCloseOption.Hide and not MainWindowCloseOption.Exit)
            {
                var dialog = new MainWindowCloseDialog
                {
                    Title = Lang.ExperienceSettingPage_CloseWindowOption,
                    PrimaryButtonText = Lang.Common_Confirm,
                    CloseButtonText = Lang.Common_Cancel,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot,
                };
                var result = await dialog.ShowAsync();
                if (result is not ContentDialogResult.Primary)
                {
                    return;
                }
                option = dialog.MainWindowCloseOption.Value;
                AppConfig.CloseWindowOption = option;
            }
            if (option is MainWindowCloseOption.Hide)
            {
                Hide();
            }
            if (option is MainWindowCloseOption.Exit)
            {
                Close();
                AppInstance.GetCurrent().UnregisterKey();
                Task backupTask = Task.Run(DatabaseService.AutoBackupToAppDataLocal);
                Task timeTask = Task.Delay(30000);
                await Task.WhenAny(backupTask, timeTask);
                App.Current.Exit();
            }
        }
        catch { }
    }



    private void OnAccentColorChanged(object _, AccentColorChangedMessage __)
    {
        FrameworkElement ele = (FrameworkElement)Content;
        ele.RequestedTheme = ele.ActualTheme switch
        {
            ElementTheme.Light => ElementTheme.Dark,
            ElementTheme.Dark => ElementTheme.Light,
            _ => ElementTheme.Default,
        };
        ele.RequestedTheme = ElementTheme.Default;
    }



    private void Content_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key is Windows.System.VirtualKey.Escape)
        {
            Hide();
        }
    }



    public override void Hide()
    {
        base.Hide();
        WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage { Hide = true, CurrentTime = DateTimeOffset.Now });
    }



    private DateTimeOffset _lastActivatedTime = DateTimeOffset.Now;


    // Lunarward：Win32 子类化（WM_ACTIVATE / WM_SYSCOMMAND / WM_WTSSESSION_CHANGE /
    // WM_DEVICECHANGE）在 Linux 上没有对应物。这里改用 Uno 的 Window.Activated 事件
    // 只保留「激活时间」这一处有实际用途的逻辑，其余（双击最大化、锁屏、U盘插拔）
    // 交给合成器/桌面环境，不再自行处理（D30：补名字可以，补行为不行）。
    private void OnWindowActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState.ToString() == "Deactivated")
        {
            return;
        }
        var now = DateTimeOffset.Now;
        WeakReferenceMessenger.Default.Send(new MainWindowStateChangedMessage
        {
            Activate = true,
            CurrentTime = now,
            LastActivatedTime = _lastActivatedTime,
        });
        _lastActivatedTime = now;
    }



    // Lunarward：WTS 会话通知（wtsapi32.dll）是 Windows 专有，Linux 端会话切换由 compositor/logind 通知，M2 接入。



    /// <summary>托盘菜单用：把主视图导航到设置页。</summary>
    /// <summary>根视图（L1.5 UI 自检用；x:Name 生成的字段对其他类不可见）。</summary>
    public MainView? RootView => MainContentHost;


    public void OpenSettings() => MainContentHost?.NavigateToSettings();


    /// <summary>打开设置并直达「运行环境」（预检修复按钮用）。</summary>
    public void OpenRuntimeSettings()
    {
        Features.Setting.SettingNavigation.SetTarget(typeof(Features.Setting.RuntimeEnvironmentSetting));
        MainContentHost?.NavigateToSettings();
    }


    /// <summary>打开当前游戏的设置页（预检修复按钮用）。</summary>
    public void OpenGameSettings() => MainContentHost?.NavigateToGameSettings();

}
