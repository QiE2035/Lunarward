// Lunarward：D5 —— Linux 版不做自更新安装器（AUR / AppImage / 包管理器负责升级）。
// 本窗口保留上游结构与交互：检查更新、展示更新内容（WebView2 渲染 changelog）、
// 「提醒我稍后」「忽略此版本」。区别只在「立即更新」不可用，改成提示用包管理器升级。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Starward.Setup.Core;
using Starward.Frameworks;
using System;
using System.Threading.Tasks;

namespace Starward.Features.Update;

public sealed partial class UpdateWindow : WindowEx
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

    public bool IsUpdateNowEnabled => false;

    public bool IsUpdateRemindLatterEnabled => true;

    public bool AutoRestartWhenUpdateFinished { get; set; }

    public bool ShowUpdateContentAfterUpdateRestart { get; set; }

    public ReleaseInfoDetail? NewVersion { get; set; }

    [RelayCommand]
    private void UpdateNow()
    {
        // Linux：交给包管理器/AUR 助手，不自行替换文件
        ErrorMessage = "Linux 版通过包管理器（AUR/AppImage）升级，请在系统中完成更新。";
    }

    [RelayCommand]
    private void RemindMeLatter() => Close();

    [RelayCommand]
    private void IgnoreThisVersion() => Close();

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
