using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
// Lunarward：截图设置页。上游整页围绕 DirectX 捕获与 HDR 色彩管理（D31 决定 M3 重做）。
// M1 保留可用的那部分设置项（文件夹、格式、质量、剪贴板），色彩管理项先恒为关闭。
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI;
using Starward.Frameworks;

namespace Starward.Features.Setting;

public sealed partial class ScreenshotSetting : PageBase
{
    public string? ScreenshotFolder { get; set; }

    /// <summary>上游拼写即为 ScreenshotForamt（历史遗留），保持一致以免改 XAML。</summary>
    public int ScreenshotForamt { get; set; }

    public int ScreenshotQuality { get; set; } = 95;

    public bool AutoCopyScreenshotToClipboard { get; set; }

    public bool AutoConvertScreenshotToSDR { get; set; }

    public bool EnableScreenshotColorManagement { get; set; }

    public bool ScreenshotQualityVisibility => true;

    [RelayCommand]
    private void ChangeScreenshotFolder() { }

    [RelayCommand]
    private void OpenScreenshotFolder() { }

    [RelayCommand]
    private void ClearThumbnailCache() { }

    [RelayCommand]
    private void TestCapture() { }

    private void HotkeyInput_HotkeyDeleted(object sender, HotkeyInputEventArg e) { }
    private void HotkeyInput_HotkeyEditFinished(object sender, HotkeyEditFinishedEventArg e) { }
    private void HotkeyInput_HotkeyEditing(object sender, HotkeyInputEventArg e) { }
    private void TextBlock_IsTextTrimmedChanged(Microsoft.UI.Xaml.Controls.TextBlock sender, IsTextTrimmedChangedEventArgs e) { }
    public string? WindowHandle { get; set; }
}
