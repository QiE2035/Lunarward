// Lunarward：D5 —— Linux 版不做自更新安装器（AUR / AppImage / 包管理器负责升级）。
// 本窗口保留上游结构与交互：检查更新、展示更新内容（WebView2 渲染 changelog）、
// 「提醒我稍后」「忽略此版本」。区别只在「立即更新」不可用，改成提示用包管理器升级。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Setup.Core;
using Starward.Features;
using System;
using System.Threading.Tasks;

namespace Starward.Features.Update;

public sealed partial class UpdateWindow : WindowBase
{
    public UpdateWindow()
    {
        InitializeComponent();
    }

    public string ChannelText { get; set; } = "";

    public string? ErrorMessage { get; set; }

    public bool IsProgressBarVisible { get; set; }

    public bool IsProgressTextVisible { get; set; }

    public string? ProgressBytesText { get; set; }

    public string? ProgressPercentText { get; set; }

    /// <summary>
    /// Lunarward：Linux 上不能像 Windows 那样下载安装包直接替换，走包管理器（D5）。
    /// L-183：以前这里恒 false，按钮直接灰掉、也没有任何解释，用户会以为「检查更新没结果」；
    /// 现在允许点击，点了给出明确说明（见 <see cref="UpdateNow"/>）。
    /// </summary>
    public bool IsUpdateNowEnabled => true;

    public bool IsUpdateRemindLatterEnabled => true;

    public bool AutoRestartWhenUpdateFinished { get; set; }

    public bool ShowUpdateContentAfterUpdateRestart { get; set; }

    public ReleaseInfoDetail? NewVersion { get; set; }

    [RelayCommand]
    private void UpdateNow()
    {
        // Linux：交给包管理器/AUR 助手，不自行替换文件。
        // 必须走 SetErrorMessage，否则 ErrorMessage 变了但 x:Bind 不会刷新（L-183）。
        SetErrorMessage("Linux 版通过包管理器（AUR / AppImage）升级，请在系统里完成更新后重新打开。");
    }

    [RelayCommand]
    private void RemindMeLatter() => Close();

    [RelayCommand]
    private void IgnoreThisVersion()
    {
        // B08：真正把版本写进配置，否则同一版本过几个小时又会弹出来。
        AppConfig.IgnoreVersion = NewVersion?.Version ?? AppConfig.AppVersion;
        Close();
    }


    /// <summary>B28：发布页 / 完整包两个超链接按 Tag 分流。</summary>
    private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? tag = (sender as Microsoft.UI.Xaml.FrameworkElement)?.Tag as string;
            string? url = tag switch
            {
                "release" => $"https://github.com/Scighost/Starward/releases/tag/{NewVersion?.Version}",
                "package" => NewVersion?.PackageUrl,
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(url))
            {
                Helpers.OpenUrl.Open(url!);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "打开更新链接失败");
        }
    }


    /// <summary>B59：手动下载标记与提示；更新内容走 WebView2（Linux 无 WebView2，保持引擎缺口）。</summary>
    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (NewVersion?.DisableAutoUpdate == true)
        {
            SetErrorMessage(Starward.Language.Lang.UpdatePage_YouNeedToManuallyDownloadTheNewVersionPackage);
        }
        else if (!string.IsNullOrWhiteSpace(ErrorMessage))
        {
            SetErrorMessage(ErrorMessage);
        }
    }


    private void SetErrorMessage(string? message)
    {
        ErrorMessage = message;
        if ((Content as FrameworkElement)?.FindName("TextBlock_ErrorMessage") is TextBlock block)
        {
            block.Text = message ?? "";
            block.Visibility = string.IsNullOrWhiteSpace(message) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    [RelayCommand]
    private void Retry() => _ = CheckUpdateAsync();

    [RelayCommand]
    private void Restart() => Close();

    private async Task CheckUpdateAsync()
    {
        try
        {
            NewVersion = await AppConfig.GetService<UpdateService>().GetLatestVersionAsync();
            ErrorMessage = NewVersion is null ? "无法获取最新版本信息" : null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    public string? Architecture { get; set; }
    public string? BuildTime { get; set; }
    public string? InstallType { get; set; }
    public string? PackageSize { get; set; }
    public string? PackageUrl { get; set; }

    /// <summary>上游把 null/空串映射成 Collapsed，用于「有更新内容才显示」。</summary>
    public static Visibility StringToVisibility(string? value)
        => string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>字节数转可读字符串（上游实现同名，M1 只保留展示能力）。</summary>
    public static string ByteLengthToString(long value)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = value;
        int i = 0;
        while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
        return $"{size:0.#} {units[i]}";
    }
}
