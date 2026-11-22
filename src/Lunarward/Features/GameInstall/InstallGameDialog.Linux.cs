// Lunarward：安装对话框。
//
// 上游在 RPC 子进程里下载安装包；Linux 侧 M3 走主进程内的 GameInstallService。
// 差异：M3 只支持「分块下载 + 文件级续传」，不写压缩包解压流程（Sophon 分块本身就是解压后的文件）。
//
// D95：行为对齐上游 —— 打开即预填默认目录并算体积、路径/空间/错误文本随属性变化刷新、
// 语音包可选、允许开始时写安装路径并广播 GameInstallTaskStartedMessage。

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameLauncher;
using Starward.Features.HoYoPlay;
using Starward.Helpers;
using Starward.RPC.GameInstall;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.GameInstall;


[INotifyPropertyChanged]
public sealed partial class InstallGameDialog : ContentDialog
{

    private const double GB = 1 << 30;


    private readonly GameInstallService _installService = AppConfig.GetService<GameInstallService>();

    private readonly HoYoPlayService _hoYoPlayService = AppConfig.GetService<HoYoPlayService>();


    public InstallGameDialog()
    {
        InitializeComponent();
        this.Loaded += InstallGameDialog_Loaded;
        CloseCommand = new RelayCommand(() => Hide());
    }


    public GameId CurrentGameId { get; set; } = null!;


    private void InstallGameDialog_Loaded(object sender, RoutedEventArgs e)
    {
        if (CurrentGameId is null)
        {
            Serilog.Log.Warning("InstallGameDialog loaded without CurrentGameId");
            Hide();
            return;
        }
        SetDefaultInstallationPath();
        _ = GetGamePackageAsync();
    }


    /// <summary>D95：默认目录取「上次使用的安装根目录」，否则 $HOME/Games（上游 Windows 是 Program Files/miHoYo）。</summary>
    private void SetDefaultInstallationPath()
    {
        try
        {
            string baseFolder = AppConfig.DefaultGameInstallationPath ?? "";
            if (string.IsNullOrWhiteSpace(baseFolder) || !Directory.Exists(baseFolder))
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                baseFolder = Path.Combine(home, "Games");
            }
            SetInstallationPath(Path.Combine(baseFolder, CurrentGameId.GameBiz.Value));
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Set default install path failed");
        }
    }


    /// <summary>D95：拉游戏配置，决定语音包是否可选、算解压体积，并刷新「开始安装」可用状态。</summary>
    private async Task GetGamePackageAsync()
    {
        try
        {
            GameConfig? config = await _hoYoPlayService.GetGameConfigAsync(CurrentGameId);
            if (config is null)
            {
                // 上游在 RPC 子进程里已经预热过配置，这里直连网络失败很常见；
                // 保持对话框可见并给出可操作的提示，比直接隐藏更像「能用」（D95 边界）。
                Serilog.Log.Warning("GameConfig of ({GameBiz}) is null.", CurrentGameId.GameBiz);
                ErrorMessage = Starward.Language.Lang.Lunarward_Error_ManifestUnavailable;
                Button_StartInstallation.IsEnabled = false;
                return;
            }
            if (!string.IsNullOrWhiteSpace(config.AudioPackageScanDir))
            {
                _needAudioPackage = true;
                Segmented_SelectLanguage.Visibility = Visibility.Visible;
                SetDefaultAudioPackage();
                ComputeAudioLanguage();
            }
            if (GameFeatureConfig.FromGameId(CurrentGameId).SupportHardLink)
            {
                StackPanel_HardLink.Visibility = Visibility.Visible;
            }
            if (config.DefaultDownloadMode is DownloadMode.DOWNLOAD_MODE_CHUNK)
            {
                GameBranch? branch = await _hoYoPlayService.GetGameBranchAsync(CurrentGameId);
                if (branch is not null)
                {
                    _gameSophonChunkBuild = await _hoYoPlayService.GetGameSophonChunkBuildAsync(branch, branch.Main);
                }
            }
            if (_gameSophonChunkBuild is null)
            {
                _gamePackage = await _hoYoPlayService.GetGamePackageAsync(CurrentGameId);
            }
            ComputePackageSize();
            CheckCanStartInstallation();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Get game package failed");
        }
    }


    private void SetDefaultAudioPackage()
    {
        if (_needAudioPackage)
        {
            Segmented_SelectLanguage.SelectedIndex = CultureInfo.CurrentUICulture.Name[..2] switch
            {
                "zh" => 0,
                "en" => 1,
                "ja" => 2,
                "ko" => 3,
                _ => 2,
            };
            ComputeAudioLanguage();
        }
    }


    private void ComputeAudioLanguage()
    {
        _audioLanguage = AudioLanguage.None;
        foreach (SegmentedItem item in Segmented_SelectLanguage.SelectedItems.Cast<SegmentedItem>())
        {
            if (item.Tag is string lang)
            {
                _audioLanguage |= lang switch
                {
                    "zh-cn" => AudioLanguage.Chinese,
                    "en-us" => AudioLanguage.English,
                    "ja-jp" => AudioLanguage.Japanese,
                    "ko-kr" => AudioLanguage.Korean,
                    _ => AudioLanguage.None,
                };
            }
        }
    }


    private void ComputePackageSize()
    {
        try
        {
            long size = 0;
            List<string?> langs = Segmented_SelectLanguage.SelectedItems.Cast<SegmentedItem>().Select(x => x.Tag as string).ToList();
            if (_gamePackage is not null)
            {
                size += _gamePackage.Main.Major!.GamePackages.Sum(x => x.DecompressedSize);
                foreach (string? lang in langs)
                {
                    if (_gamePackage.Main.Major.AudioPackages.FirstOrDefault(x => x.Language == lang) is GamePackageFile file)
                    {
                        size += file.DecompressedSize;
                    }
                }
            }
            else if (_gameSophonChunkBuild is not null)
            {
                foreach (GameSophonChunkManifest manifest in _gameSophonChunkBuild.Manifests)
                {
                    if (manifest.MatchingField.Length is 5 or 10 && manifest.MatchingField.Contains('-'))
                    {
                        // 跳过语音包 zh-cn or mini-zh-cn
                        continue;
                    }
                    size += manifest.Stats.UncompressedSize;
                }
                foreach (string? lang in langs)
                {
                    if (_gameSophonChunkBuild.Manifests.FirstOrDefault(x => x.MatchingField == lang) is GameSophonChunkManifest audioManifest)
                    {
                        size += audioManifest.Stats.UncompressedSize;
                    }
                }
            }
            UnzipSpaceBytes = size;
            TextBlock_AvailableSpace.Foreground = size > 0 && AvailableSpaceBytes > 0 && size > AvailableSpaceBytes
                ? App.Current.Resources["SystemFillColorCautionBrush"] as Brush
                : App.Current.Resources["TextFillColorSecondaryBrush"] as Brush;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Compute package size failed");
        }
    }


    private void CheckCanStartInstallation()
    {
        try
        {
            ErrorMessage = null;
            Button_StartInstallation.IsEnabled = false;
            if (_gamePackage is not null || _gameSophonChunkBuild is not null)
            {
                string? path = InstallationPath;
                if (string.IsNullOrWhiteSpace(path))
                {
                    ErrorMessage = Starward.Language.Lang.Lunarward_Error_InstallDirInvalid;
                }
                else if (Path.GetPathRoot(path) == path.TrimEnd(Path.DirectorySeparatorChar))
                {
                    ErrorMessage = Starward.Language.Lang.LauncherPage_PleaseDoNotSelectTheRootDirectoryOfADrive;
                }
                else if (Path.IsPathFullyQualified(path))
                {
                    // 需要语音包时必须选了语音包；不需要时必须没选（上游同款异或）
                    if (!(_needAudioPackage ^ Segmented_SelectLanguage.SelectedItems.Count > 0))
                    {
                        Button_StartInstallation.IsEnabled = true;
                    }
                }
                else
                {
                    ErrorMessage = Starward.Language.Lang.Lunarward_Error_InstallDirInvalid;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Check can start installation failed");
        }
    }


    private void SetInstallationPath(string path)
    {
        try
        {
            TextBlock_InstallationPath.FontSize = 14;
            InstallationPath = path;
            AvailableSpaceBytes = DriveHelper.GetDriveAvailableSpace(path);
            CheckCanStartInstallation();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Set installation path failed");
        }
    }


    private void Segmented_SelectLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ComputeAudioLanguage();
        ComputePackageSize();
        CheckCanStartInstallation();
    }


    [RelayCommand]
    private async Task ChangeInstallationPathAsync()
    {
        try
        {
            string? path = await FileDialogHelper.PickFolderAsync(XamlRoot);
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                _selectPath = path;
                if (AutomaticallyCreateSubfolderForInstall)
                {
                    path = Path.Combine(path, CurrentGameId.GameBiz.Value);
                }
                ErrorMessage = null;
                SetInstallationPath(path);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Change installation path failed");
            ErrorMessage = $"选择目录失败：{ex.Message}";
        }
    }


    [RelayCommand]
    private async Task StartInstallationAsync()
    {
        try
        {
            string? path = InstallationPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                ErrorMessage = Starward.Language.Lang.Lunarward_Error_InstallDirInvalid;
                return;
            }
            Directory.CreateDirectory(path);
            GameInstallContext? task = await _installService.StartInstallAsync(CurrentGameId, path, _audioLanguage);
            if (task is not null && task.State is not GameInstallState.Stop and not GameInstallState.Error)
            {
                // D95：上游语义 —— 任务真正开起来之后才写安装路径、广播、关闭对话框
                GameLauncherService.ChangeGameInstallPath(CurrentGameId, path);
                WeakReferenceMessenger.Default.Send(new GameInstallTaskStartedMessage(task));
                if (_selectPath is not null && path.EndsWith(CurrentGameId.GameBiz.Value, StringComparison.Ordinal))
                {
                    AppConfig.DefaultGameInstallationPath = _selectPath;
                }
                Hide();
            }
            else
            {
                ErrorMessage = Starward.Language.Lang.Lunarward_Error_InstallDirInvalid;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Start install failed");
            ErrorMessage = $"{Starward.Language.Lang.Lunarward_Error_LaunchFailed}{ex.Message}";
        }
    }


    // Lunarward：上游这段在 InstallGameDialog.xaml.cs（本工程 <Compile Remove>），
    // 安装路径过长时自动缩一号字，避免被硬切成两行省略号（D46 复核结论 1）。
    private void TextBlock_IsTextTrimmedChanged(Microsoft.UI.Xaml.Controls.TextBlock sender, IsTextTrimmedChangedEventArgs args)
    {
        if (sender.FontSize > 12)
        {
            sender.FontSize--;
        }
    }


    // ---------------------------------------------------------------- 状态

    private bool _needAudioPackage;

    private AudioLanguage _audioLanguage;

    private GamePackage? _gamePackage;

    private GameSophonChunkBuild? _gameSophonChunkBuild;

    private string? _selectPath;


    public string? InstallationPath { get; set => SetProperty(ref field, value); }

    public string? ErrorMessage { get; set => SetProperty(ref field, value); }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UnzipSpaceText))]
    public partial long UnzipSpaceBytes { get; set; }

    public string UnzipSpaceText => UnzipSpaceBytes == 0 ? "..." : $"{UnzipSpaceBytes / GB:F2} GB";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AvailableSpaceText))]
    public partial long AvailableSpaceBytes { get; set; }

    public string AvailableSpaceText => AvailableSpaceBytes == 0 ? "..." : $"{AvailableSpaceBytes / GB:F2} GB";

    [ObservableProperty]
    public partial bool AutomaticallyCreateSubfolderForInstall { get; set; } = AppConfig.AutomaticallyCreateSubfolderForInstall;

    partial void OnAutomaticallyCreateSubfolderForInstallChanged(bool value)
    {
        AppConfig.AutomaticallyCreateSubfolderForInstall = value;
        if (!string.IsNullOrWhiteSpace(_selectPath))
        {
            SetInstallationPath(value
                ? Path.Combine(_selectPath, CurrentGameId.GameBiz.Value)
                : _selectPath);
        }
    }


    public IRelayCommand CloseCommand { get; }

}
