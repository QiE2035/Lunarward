using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
// Lunarward：截图设置页。上游整页围绕 DirectX 捕获与 HDR 色彩管理（D31 决定 M3 重做）。
// 这里保留可用的那部分设置项（文件夹、格式、质量、剪贴板），色彩管理项恒为关闭。
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using Starward.Features;
using System;
using System.Threading.Tasks;

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

    public bool AutoCopyScreenshotToClipboard { get; set; }

    public bool AutoConvertScreenshotToSDR { get; set; }

    public bool EnableScreenshotColorManagement { get; set; }

    public Microsoft.UI.Xaml.Visibility ScreenshotQualityVisibility => ScreenshotForamt > 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public string? WindowHandle { get; set; }


    public ScreenshotSetting()
    {
        // 上游 ScreenshotSetting.xaml.cs 的 ctor 第一句就是它；本文件取代了那份 code-behind，
        // 漏掉会得到一页空白（XAML 编译出来的 InitializeComponent 不会被框架自动调用）。
        InitializeComponent();
        ScreenshotFolder = AppConfig.ScreenshotFolder;
        AutoCopyScreenshotToClipboard = AppConfig.AutoCopyScreenshotToClipboard;
    }


    private string EffectiveFolder => string.IsNullOrWhiteSpace(ScreenshotFolder)
        ? Features.Screenshot.ScreenCaptureLinux.DefaultFolder
        : ScreenshotFolder!;


    protected override void OnNavigatedFrom(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        // 设置页用普通属性 + 双向绑定，离开页面时统一落盘（Uno 下逐属性 setter 落盘会写太多次文件）
        AppConfig.ScreenshotFolder = string.IsNullOrWhiteSpace(ScreenshotFolder) ? null : ScreenshotFolder;
        AppConfig.AutoCopyScreenshotToClipboard = AutoCopyScreenshotToClipboard;
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


    private void HotkeyInput_HotkeyDeleted(object sender, HotkeyInputEventArg e)
    {
        // LINT-ALLOW: 热键由 HotkeyManager 统一接管（X11 轮询），输入控件只做展示
    }
    private void HotkeyInput_HotkeyEditFinished(object sender, HotkeyEditFinishedEventArg e)
    {
        // LINT-ALLOW: 同上，注册在 HotkeyManager.Register 里完成
    }
    private void HotkeyInput_HotkeyEditing(object sender, HotkeyInputEventArg e)
    {
        // LINT-ALLOW: 编辑中的实时预览对 X11 轮询没有意义
    }
    // Lunarward：上游这段逻辑在 ScreenshotSetting.xaml.cs（本工程 <Compile Remove>），
    // 这里补回来：文字被截断一次就缩小一号（最小 12），避免长路径被硬切（D46 复核结论 1）。
    private void TextBlock_IsTextTrimmedChanged(Microsoft.UI.Xaml.Controls.TextBlock sender, IsTextTrimmedChangedEventArgs e)
    {
        if (sender.FontSize > 12)
        {
            sender.FontSize -= 1;
        }
    }
}
