// Lunarward：安装对话框。
//
// 上游在 RPC 子进程里下载安装包；Linux 侧 M3 走主进程内的 GameInstallService。
// 差异：M3 只支持「分块下载 + 文件级续传」，不写压缩包解压流程（Sophon 分块本身就是解压后的文件）。

using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.Input;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Helpers;
using Starward.RPC.GameInstall;
using System;
using System.IO;

namespace Starward.Features.GameInstall;


public sealed partial class InstallGameDialog : ContentDialog
{

    private readonly GameInstallService _installService = AppConfig.GetService<GameInstallService>();

    private readonly AudioLanguage _audioLanguage = AudioLanguage.Chinese;


    public GameId CurrentGameId { get; set; } = null!;


    public string? InstallationPath { get; set; }


    public string? AvailableSpaceText { get; set; }


    public string? UnzipSpaceText { get; set; }


    public string? ErrorMessage { get; set; }


    public bool AutomaticallyCreateSubfolderForInstall { get; set; }


    public IRelayCommand ChangeInstallationPathCommand { get; }

    public IRelayCommand CloseCommand { get; }

    public IRelayCommand StartInstallationCommand { get; }


    public InstallGameDialog()
    {
        InitializeComponent();
        ChangeInstallationPathCommand = new AsyncRelayCommand(ChangePathAsync);
        CloseCommand = new RelayCommand(() => Hide());
        StartInstallationCommand = new AsyncRelayCommand(StartInstallationAsync);
    }


    private async System.Threading.Tasks.Task ChangePathAsync()
    {
        try
        {
            string? folder = await FileDialogHelper.PickFolderAsync(XamlRoot);
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }
            if (AutomaticallyCreateSubfolderForInstall)
            {
                folder = Path.Combine(folder, CurrentGameId.GameBiz.Value);
            }
            InstallationPath = folder;
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Pick install folder failed");
            ErrorMessage = $"选择目录失败：{ex.Message}";
        }
    }


    private async System.Threading.Tasks.Task StartInstallationAsync()
    {
        string? path = InstallationPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            ErrorMessage = "请先选择安装目录";
            return;
        }
        try
        {
            Directory.CreateDirectory(path);
            GameInstallContext? task = await _installService.StartInstallAsync(CurrentGameId, path, _audioLanguage);
            if (task is null)
            {
                ErrorMessage = Starward.Language.Lang.Lunarward_Error_InstallDirInvalid;
                return;
            }
            InAppToast.MainWindow?.Success(null, $"已开始安装到 {path}，进度显示在启动器页面上", 5000);
            Hide();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Start install failed");
            ErrorMessage = $"{Starward.Language.Lang.Lunarward_Error_LaunchFailed}{ex.Message}";
        }
    }

}
