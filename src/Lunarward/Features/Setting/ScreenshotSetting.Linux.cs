using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
// Lunarward：截图设置页。上游整页围绕 DirectX 捕获与 HDR 色彩管理（D31 决定 M3 重做）。
// 这里保留可用的那部分设置项（文件夹、格式、质量、剪贴板），色彩管理项恒为关闭。
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using Microsoft.Extensions.Logging;
using Starward.Helpers;
using Starward.Features;
using System;
using System.Threading.Tasks;
using Vanara.PInvoke;

namespace Starward.Features.Setting;

public sealed partial class ScreenshotSetting : PageBase
{
    public string? ScreenshotFolder { get; set; }

    /// <summary>上游拼写即为 ScreenshotForamt（历史遗留），保持一致以免改 XAML。
    /// 0: PNG, 1: AVIF, 2: JPEG XL。</summary>
    public int ScreenshotForamt
    {
        get => AppConfig.ScreenCaptureSavedFormat;
        set
        {
            if (value < 0)
            {
                return;
            }
            if (AppConfig.ScreenCaptureSavedFormat != value)
            {
                AppConfig.ScreenCaptureSavedFormat = value;
                OnPropertyChanged(nameof(ScreenshotForamt));
                OnPropertyChanged(nameof(ScreenshotQualityVisibility));
            }
        }
    }

    /// <summary>B17：这是三段式的索引（0 Middle / 1 High / 2 Lossless），不是 JPEG 质量数值。</summary>
    public int ScreenshotQuality
    {
        get => Math.Clamp(AppConfig.ScreenCaptureEncodeQuality, 0, 2);
        set
        {
            int index = Math.Clamp(value, 0, 2);
            if (AppConfig.ScreenCaptureEncodeQuality != index)
            {
                AppConfig.ScreenCaptureEncodeQuality = index;
                OnPropertyChanged(nameof(ScreenshotQuality));
            }
        }
    }

    // D134：与上游一致，三个开关的 setter 直接写 AppConfig（上游 ScreenshotSetting.xaml.cs 同）。
    // 之前是纯自动属性 + OnNavigatedFrom 统一回写，两个只被记录、从未落盘的开关就丢了。
    public bool AutoCopyScreenshotToClipboard
    {
        get => AppConfig.AutoCopyScreenshotToClipboard;
        set
        {
            if (AppConfig.AutoCopyScreenshotToClipboard != value)
            {
                AppConfig.AutoCopyScreenshotToClipboard = value;
                OnPropertyChanged(nameof(AutoCopyScreenshotToClipboard));
            }
        }
    }

    public bool AutoConvertScreenshotToSDR
    {
        get => AppConfig.AutoConvertScreenshotToSDR;
        set
        {
            if (AppConfig.AutoConvertScreenshotToSDR != value)
            {
                AppConfig.AutoConvertScreenshotToSDR = value;
                OnPropertyChanged(nameof(AutoConvertScreenshotToSDR));
            }
        }
    }

    public bool EnableScreenshotColorManagement
    {
        get => AppConfig.EnableScreenshotColorManagement;
        set
        {
            if (AppConfig.EnableScreenshotColorManagement != value)
            {
                AppConfig.EnableScreenshotColorManagement = value;
                OnPropertyChanged(nameof(EnableScreenshotColorManagement));
            }
        }
    }

    public Microsoft.UI.Xaml.Visibility ScreenshotQualityVisibility => ScreenshotForamt > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    // D135/F-16：上游是 nint（XamlRoot.GetWindowHandle），移植侧以前是恒 null 的 string?，
    // 传给 HotkeyInput 的 WindowHandle 也就永远是空。HotkeyManager 的 Linux 实现忽略 hwnd，
    // 但类型与取值要和上游一致，避免下次同步时看不懂。
    public nint WindowHandle => XamlRoot.GetWindowHandle();


    private readonly Microsoft.Extensions.Logging.ILogger<ScreenshotSetting> _logger =
        AppConfig.GetLogger<ScreenshotSetting>();


    public ScreenshotSetting()
    {
        // 上游 ScreenshotSetting.xaml.cs 的 ctor 第一句就是它；本文件取代了那份 code-behind，
        // 漏掉会得到一页空白（XAML 编译出来的 InitializeComponent 不会被框架自动调用）。
        InitializeComponent();
        ScreenshotFolder = AppConfig.ScreenshotFolder;
        AutoCopyScreenshotToClipboard = AppConfig.AutoCopyScreenshotToClipboard;
        // 上游 ScreenshotSetting.xaml.cs:34-39 同样在 ctor 里做；漏掉这一句，控件显示「点击设置」
        // 而实际热键仍是配置文件里的旧值（D135）。
        try
        {
            Helpers.HotkeyManager.InitializeHotkeyInput(HotkeyInput_ScreenshotCapture);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initialize screenshot hotkey input failed");
        }
    }


    private string EffectiveFolder => string.IsNullOrWhiteSpace(ScreenshotFolder)
        ? Features.Screenshot.ScreenCaptureLinux.DefaultFolder
        : ScreenshotFolder!;


    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // 设置页用普通属性 + 双向绑定，离开页面时统一落盘（Uno 下逐属性 setter 落盘会写太多次文件）
        AppConfig.ScreenshotFolder = string.IsNullOrWhiteSpace(ScreenshotFolder) ? null : ScreenshotFolder;
        // 格式/质量已经由属性 setter 即时落盘；Linux 捕获统一出 PNG，质量只影响「批量转换」（B17）。
    }


    [RelayCommand]
    private async Task ChangeScreenshotFolderAsync()
    {
        string? folder = await Helpers.FileDialogHelper.PickFolderAsync(XamlRoot);
        if (!string.IsNullOrWhiteSpace(folder))
        {
            ScreenshotFolder = folder;
            AppConfig.ScreenshotFolder = folder;
            // XAML 里是 OneTime 绑定，改完要主动刷一次，否则路径文本还是旧的
            Bindings.Update();
        }
    }


    [RelayCommand]
    private void OpenScreenshotFolder()
    {
        try
        {
            System.IO.Directory.CreateDirectory(EffectiveFolder);
        }
        catch
        {
        }
        Helpers.OpenUrl.Open(EffectiveFolder);
    }


    [RelayCommand]
    private void ClearThumbnailCache()
    {
        try
        {
            string cache = System.IO.Path.Combine(AppConfig.CacheDirectory, "thumb");
            if (System.IO.Directory.Exists(cache))
            {
                System.IO.Directory.Delete(cache, true);
                Helpers.InAppToast.MainWindow?.Success("已清理缩略图缓存");
            }
            else
            {
                Helpers.InAppToast.MainWindow?.Information("没有可清理的缩略图缓存", null, 3000);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Clear screenshot thumbnail cache failed");
            Helpers.InAppToast.MainWindow?.Error(ex.Message);
        }
    }


    [RelayCommand]
    private async Task TestCaptureAsync()
    {
        string? file = await Features.Screenshot.ScreenCaptureLinux.CaptureAsync(EffectiveFolder);
        if (file is null)
        {
            Helpers.InAppToast.MainWindow?.Error("截图失败：没有找到可用的截图工具（spectacle / grim / gnome-screenshot）", null, 8000);
        }
        else
        {
            Helpers.InAppToast.MainWindow?.Success($"已保存到 {file}");
        }
    }


    // D135：这三个处理器以前是空体（带 LINT-ALLOW），上游 ScreenshotSetting.xaml.cs:68-110 的
    // 注册/注销/落盘全在这里 —— 结果是用户改完截图热键既不生效也不落盘，失败也看不到提示。
    // 移植侧的 HotkeyManager（X11 轮询）与上游同签名，只是忽略 hwnd，所以这里直接照搬上游逻辑。
    private void HotkeyInput_HotkeyDeleted(object sender, HotkeyInputEventArg e)
    {
        try
        {
            XamlRoot.Content.Focus(FocusState.Programmatic);
            Helpers.HotkeyManager.DeleteHotkey(0, e.HotkeyId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hotkey deleted");
        }
    }


    private void HotkeyInput_HotkeyEditing(object sender, HotkeyInputEventArg e)
    {
        try
        {
            Helpers.HotkeyManager.UnregisterHotkey(0, e.HotkeyId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hotkey editing");
        }
    }


    private void HotkeyInput_HotkeyEditFinished(object sender, HotkeyEditFinishedEventArg e)
    {
        try
        {
            XamlRoot.Content.Focus(FocusState.Programmatic);
            if (!e.HotkeyAvaliable)
            {
                return;
            }
            Win32Error error = Helpers.HotkeyManager.RegisterHotkey(0, e.HotkeyId, (User32.HotKeyModifiers)e.fsModifiers, (User32.VK)e.Key);
            if (error.Succeeded)
            {
                if (e.HotkeyChanged)
                {
                    ((HotkeyInput)sender).State = HoykeyInputState.Success;
                }
                return;
            }
            if (e.HotkeyChanged)
            {
                string hotkey = HotkeyInput.GetHotkeyText(e.fsModifiers, (uint)e.Key);
                // Windows 上这个码是「已被其它程序占用」；X11 轮询拿不到独占权，这里只会是「按键无 keycode」。
                if (error == (Win32Error)1409)
                {
                    Helpers.InAppToast.MainWindow?.Warning(null, string.Format(Lang.HotkeyManager_TheShortcutKeys0IsAlreadyInUse, hotkey), 5000);
                }
                else
                {
                    Helpers.InAppToast.MainWindow?.Warning(null, string.Format(Lang.HotkeyManager_FailedToRegisterTheShortcutKeys0, hotkey), 5000);
                }
                ((HotkeyInput)sender).State = HoykeyInputState.Warning;
            }
            else if (Helpers.HotkeyManager.GetHotkeyInfo(e.HotkeyId)?.Error.Failed ?? false)
            {
                ((HotkeyInput)sender).State = HoykeyInputState.Warning;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hotkey edit finished");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.HotkeySetting_FailedToRegisterTheShortcutKeys);
        }
    }
}
