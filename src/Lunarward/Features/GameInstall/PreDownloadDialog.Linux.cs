// Lunarward：预下载对话框。
//
// 上游在这里点「开始预下载」后把任务交给 RPC 子进程，对话框关闭，进度画在启动器页面上
// （通过 GameInstallTaskStartedMessage 广播）。Linux 侧 M3 一样：任务交给主进程内的
// GameInstallService，对话框关闭并提示；进度由启动器页面读 GameInstallContext 显示。

using Microsoft.UI.Xaml.Controls;
using CommunityToolkit.Mvvm.Input;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameInstall.Sophon;
using Starward.Helpers;
using Starward.RPC.GameInstall;
using System;
using System.Collections.Generic;
using System.IO;

namespace Starward.Features.GameInstall;


public sealed partial class PreDownloadDialog : ContentDialog, System.ComponentModel.INotifyPropertyChanged
{

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;


    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));


    private readonly GameInstallService _installService = AppConfig.GetService<GameInstallService>();

    private readonly AudioLanguage _audioLanguage = AudioLanguage.Chinese;


    public GameId CurrentGameId { get; set; } = null!;


    public string InstallationPath { get; set; } = "";


    private string? _availableSpaceText;

    public string? AvailableSpaceText
    {
        get => _availableSpaceText;
        private set
        {
            _availableSpaceText = value;
            OnPropertyChanged(nameof(AvailableSpaceText));
        }
    }


    private string? _packageSizeText;

    public string? PackageSizeText
    {
        get => _packageSizeText;
        private set
        {
            _packageSizeText = value;
            OnPropertyChanged(nameof(PackageSizeText));
        }
    }


    private string? _unzipSpaceText;

    public string? UnzipSpaceText
    {
        get => _unzipSpaceText;
        private set
        {
            _unzipSpaceText = value;
            OnPropertyChanged(nameof(UnzipSpaceText));
        }
    }


    public IRelayCommand StartPredownloadCommand { get; }


    public IRelayCommand CloseCommand { get; }


    public PreDownloadDialog()
    {
        InitializeComponent();
        StartPredownloadCommand = new AsyncRelayCommand(StartPredownloadAsync);
        CloseCommand = new RelayCommand(() => Hide());
        Opened += (_, _) => _ = LoadSizeInfoAsync();
    }


    private string ResolveInstallPath()
    {
        if (!string.IsNullOrWhiteSpace(InstallationPath))
        {
            return InstallationPath;
        }
        InstallationPath = AppConfig.GetGameInstallPath(CurrentGameId.GameBiz)
                           ?? Path.Combine(AppConfig.UserDataFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "lunarward"), "Games", CurrentGameId.GameBiz.Value);
        return InstallationPath;
    }


    private async System.Threading.Tasks.Task LoadSizeInfoAsync()
    {
        try
        {
            ResolveInstallPath();
            var fields = new List<string> { "game" };
            fields.AddRange(SophonClient.DetectInstalledAudioFields(InstallationPath));
            var sophon = new SophonClient(AppConfig.GetService<Starward.Core.HoYoPlay.HoYoPlayClient>());
            var info = await sophon.GetBuildInfoAsync(CurrentGameId, preDownload: true, fields, default);
            if (info is null)
            {
                // 预下载不是每个版本都有（版本已转正式更新时就没有）。这里必须如实告诉用户，
                // 否则界面只剩一行空白的「下载大小：」，看着像坏了（M3 小问题清单）。
                Serilog.Log.Information("PreDownload: no predownload package for {game}", CurrentGameId.GameBiz);
                if (TextBlock_PredownloadUnavailable is not null)
                {
                    TextBlock_PredownloadUnavailable.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                }
                if (Button_StartPredownload is not null)
                {
                    Button_StartPredownload.IsEnabled = false;
                }
                return;
            }
            Serilog.Log.Information("PreDownload: chunk build total={total}", info.TotalCompressedBytes);
            // getBuild 给的是「完整游戏」体积（136 GB 量级）；预下载的实际下载量要用
            // getPatchBuild 的 stats[本地版本]，否则用户会以为要重下整包（L-61 第二段）。
            string? localVersion = SophonClient.TryReadLocalVersion(InstallationPath);
            long? patchBytes = await sophon.GetPatchDownloadBytesAsync(CurrentGameId, localVersion ?? "", fields, preDownload: true);
            if (patchBytes is > 0)
            {
                Serilog.Log.Information("PreDownload: delta from {version} = {bytes}", localVersion, patchBytes.Value);
                PackageSizeText = FormatSize(patchBytes.Value);
                // 预下载只落变更文件；变更文件解压后的体积与下载体积同量级（差分数据本身已含新内容），
                // 这里按同一数值提示，避免再显示 140 GB 的整包体积吓人。
                UnzipSpaceText = FormatSize(patchBytes.Value);
            }
            else
            {
                Serilog.Log.Information("PreDownload: no patch stats for {version}, fall back to full size", localVersion);
                PackageSizeText = FormatSize(info.TotalCompressedBytes);
                UnzipSpaceText = FormatSize(info.TotalUncompressedBytes);
            }
            string volume = InstallationPath;
            while (!string.IsNullOrEmpty(volume) && !Directory.Exists(volume))
            {
                volume = Path.GetDirectoryName(volume) ?? "";
            }
            if (!string.IsNullOrEmpty(volume))
            {
                long free = new DriveInfo(volume).AvailableFreeSpace;
                AvailableSpaceText = FormatSize(free);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Load predownload size failed");
            PackageSizeText = null;
            UnzipSpaceText = null;
        }
    }


    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return $"{value:0.##} {units[unit]}";
    }


    private async System.Threading.Tasks.Task StartPredownloadAsync()
    {
        try
        {
            string path = ResolveInstallPath();
            Directory.CreateDirectory(path);
            GameInstallContext? task = await _installService.StartPredownloadAsync(CurrentGameId, path, _audioLanguage);
            if (task is null)
            {
                InAppToast.MainWindow?.Warning(null, $"{Starward.Language.Lang.Lunarward_Error_InstallDirInvalid}", 4000);
                return;
            }
            InAppToast.MainWindow?.Success(null, "预下载已开始，进度显示在启动器页面上", 4000);
            Hide();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Start predownload failed");
            InAppToast.MainWindow?.Error((string?)null, $"{Starward.Language.Lang.Lunarward_Error_LaunchFailed}{ex.Message}", 6000);
        }
    }

}
