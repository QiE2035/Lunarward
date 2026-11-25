using CommunityToolkit.Mvvm.ComponentModel;
using Starward.Language;
using Starward.Features.Runtime;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Documents;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Background;
using Starward.Features.CloudGame;
using Starward.Features.GameInstall;
using Starward.Features.Overlay;
using Starward.Features.ViewHost;
using Starward.GameInstall;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Starward.GameInstall;
using Starward.Features;
using System.Timers;


namespace Starward.Features.GameLauncher;

public sealed partial class GameLauncherPage : PageBase
{

    private readonly ILogger<GameLauncherPage> _logger = AppConfig.GetLogger<GameLauncherPage>();

    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();

    private readonly GameInstallService _gameInstallService = AppConfig.GetService<GameInstallService>();

    private readonly HoYoPlayService _hoYoPlayService = AppConfig.GetService<HoYoPlayService>();


    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer _dispatchTimer;


    public GameLauncherPage()
    {
        this.InitializeComponent();
        _dispatchTimer = DispatcherQueue.CreateTimer();
        _dispatchTimer.Interval = TimeSpan.FromMilliseconds(100);
        _dispatchTimer.Tick += UpdateGameInstallTaskProgress;
    }



    private bool _gameChangedHandled;


    protected override void OnLoaded()
    {
        // B30：缓存页切游戏时 OnGameChanged 已经重载过内容，紧接着的 Loaded 别再跑一遍（两次并发初始化）。
        if (_gameChangedHandled)
        {
            _gameChangedHandled = false;
        }
        else
        {
            LoadGameContent();
        }
        StartInstallTaskWatcher();
        // D94：页面缓存后 Tick 会在第一次 Unloaded 被摘掉，重新挂载要接回来，否则安装进度不再更新。
        _dispatchTimer.Tick -= UpdateGameInstallTaskProgress;
        _dispatchTimer.Tick += UpdateGameInstallTaskProgress;
        WeakReferenceMessenger.Default.Register<GameInstallPathChangedMessage>(this, OnGameInstallPathChanged);
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, OnMainWindowStateChanged);
        WeakReferenceMessenger.Default.Register<RemovableStorageDeviceChangedMessage>(this, OnRemovableStorageDeviceChanged);
        WeakReferenceMessenger.Default.Register<GameInstallTaskStartedMessage>(this, OnGameInstallTaskStarted);
        WeakReferenceMessenger.Default.Register<BackgroundChangedMessage>(this, OnBackgroundChanged);
        WeakReferenceMessenger.Default.Register<GameLauncherPage, CurrentGameChangedMessage>(this, (r, m) => r.OnCurrentGameChanged(m));
    }



    /// <summary>
    /// 缓存页面导航到另一个游戏（D94）：等价于上游重建页面，把整页内容换成新游戏的。
    /// </summary>
    protected override void OnGameChanged()
    {
        _gameChangedHandled = true;
        LoadGameContent();
    }



    /// <summary>
    /// 按 <see cref="PageBase.CurrentGameId"/> 重载本页全部内容。上游每次导航都新建页面、字段回到默认值，
    /// 这里手动把上款游戏残留的页面级状态清掉，再跑一遍上游 OnLoaded 里的初始化（D94）。
    /// </summary>
    private void LoadGameContent()
    {
        try
        {
            GameInstallPath = null;
            IsInstallPathRemovableTipEnabled = false;
            IsPredownloadButtonEnabled = false;
            IsPredownloadFinished = false;
            IsDX12OptionVisible = false;
            GameServers = null;
            SelectedGameServer = null;
            _gameInstallTask = null;
            DXConfig = null;
            Button_CloudGame.Visibility = Visibility.Collapsed;
            BackgroundImages = null!;
            currentBackgroundImageIndex = 0;
            OnPropertyChanged(nameof(CurrentBackgroundImageIndex));
            // 下挂控件同样不会随页面缓存重建：显式让它们换成当前游戏（D94）。
            Button_PlayTime.RefreshPlayTime(CurrentGameBiz);
            GameBannerAndPost.CurrentGameId = CurrentGameId;

            InitializeGameFeature();
            CheckGameVersion();
            UpdateGameInstallTask();
            CheckCloudGame();
            _ = InitializeGameServerAsync();
            _ = InitializeBackgameImageSwitcherAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load game content {GameBiz}", CurrentGameBiz);
        }
    }



    /// <summary>切换游戏（页面已缓存，不会重建）：兜底没走导航的情况，例如在设置页里切游戏（D62/D94）。</summary>
    private void OnCurrentGameChanged(CurrentGameChangedMessage message)
    {
        try
        {
            string? gameId = message.GameId?.Id;
            if (ContentGameId == gameId)
            {
                return;
            }
            CurrentGameId = message.GameId;
            CurrentGameBiz = message.GameId.GameBiz;
            ContentGameId = gameId;
            LoadGameContent();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[launcher] refresh for current game failed");
        }
    }


    protected override void OnUnloaded()
    {
        StopInstallTaskWatcher();
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _dispatchTimer.Tick -= UpdateGameInstallTaskProgress;
        _dispatchTimer.Stop();
    }



    private void InitializeGameFeature()
    {
        GameFeatureConfig feature = GameFeatureConfig.FromGameId(CurrentGameId);
        if (feature.SupportCloudGame)
        {
            Button_CloudGame.Visibility = Visibility.Visible;
        }
    }



    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InstalledLocateGameEnabled))]
    public partial GameState GameState { get; set; }



    [RelayCommand]
    private async Task ClickStartGameButtonAsync()
    {
        await Task.Delay(1);
        switch (GameState)
        {
            case GameState.None:
                break;
            case GameState.StartGame:
                await StartGameAsync();
                break;
            case GameState.GameIsRunning:
            case GameState.InstallGame:
                await InstallGameAsync();
                break;
            case GameState.Installing:
                await ChangeGameInstallTaskStateAsync();
                break;
            case GameState.UpdateGame:
                await UpdateGameAsync();
                break;
            case GameState.UpdatePlugin:
                await UpdatePluginAsync();
                break;
            case GameState.ResumeDownload:
                await ResumeDownloadAsync();
                break;
            case GameState.ComingSoon:
                break;
            case GameState.Reservation:
                await OpenReservationUrlAsync();
                break;
            case GameState.HotUpdate:
                await HotUpdateAsync();
                break;
            default:
                break;
        }
    }




    #region Game Server


    public List<GameServerConfig>? GameServers { get; set => SetProperty(ref field, value); }

    [ObservableProperty]
    public partial GameServerConfig? SelectedGameServer { get; set; }
    partial void OnSelectedGameServerChanged(GameServerConfig? oldValue, GameServerConfig? newValue)
    {
        if (oldValue is not null && newValue is not null)
        {
            AppConfig.LastGameIdOfBH3Global = newValue.GameId;
            WeakReferenceMessenger.Default.Send(new BH3GlobalGameServerChangedMessage(newValue.GameId));
        }
    }


    /// <summary>
    /// 初始化区服选项，仅崩坏三国际服使用
    /// </summary>
    /// <returns></returns>
    private async Task InitializeGameServerAsync()
    {
        try
        {
            if (CurrentGameBiz == GameBiz.bh3_global)
            {
                GameInfo? gameInfo = await _hoYoPlayService.GetGameInfoAsync(CurrentGameId);
                if (gameInfo?.GameServerConfigs?.Count > 0)
                {
                    GameServers = gameInfo.GameServerConfigs;
                    if (GameServers.FirstOrDefault(x => x.GameId == CurrentGameId.Id) is GameServerConfig config)
                    {
                        SelectedGameServer = config;
                    }
                    else
                    {
                        SelectedGameServer = GameServers.FirstOrDefault();
                        if (SelectedGameServer is not null)
                        {
                            CurrentGameId = new GameId(SelectedGameServer.GameId, GameBiz.bh3_global);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize game server");
        }
    }



    #endregion



    #region Game Version


    public string? GameInstallPath { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// 可移动存储设备提示
    /// </summary>
    public bool IsInstallPathRemovableTipEnabled { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// 已安装？定位游戏
    /// </summary>
    public bool InstalledLocateGameEnabled => GameState is GameState.InstallGame && !IsInstallPathRemovableTipEnabled;

    /// <summary>
    /// 预下载按钮是否可用
    /// </summary>
    public bool IsPredownloadButtonEnabled { get; set => SetProperty(ref field, value); }

    /// <summary>
    /// 预下载是否完成
    /// </summary>
    public bool IsPredownloadFinished { get; set => SetProperty(ref field, value); }

    private Version? localGameVersion;

    private Version? latestGameVersion;

    private Version? predownloadGameVersion;

    private string? reservationUrl;


    /// <summary>
    /// 是否显示 DX12 选项
    /// </summary>
    public bool IsDX12OptionVisible { get; set => SetProperty(ref field, value); }


    /// <summary>
    /// DX12 配置
    /// </summary>
    public GameDXConfig? DXConfig { get; set => SetProperty(ref field, value); }


    /// <summary>
    /// 启用 DX12
    /// </summary>
    private bool _enableDX12;
    public bool EnableDX12
    {
        get => _enableDX12; set
        {
            if (SetProperty(ref _enableDX12, value))
            {
                AppConfig.SetEnableDX12(CurrentGameId, value);
            }
        }
    }


    private CancellationTokenSource? _checkGameVersionTokenSource;


    private async void CheckGameVersion()
    {
        try
        {
            _checkGameVersionTokenSource?.Cancel();
            _checkGameVersionTokenSource = CancellationTokenSource.CreateLinkedTokenSource(UnloadCancellationToken);
            CancellationToken cancellation = _checkGameVersionTokenSource.Token;

            GameInstallPath = GameLauncherService.GetGameInstallPath(CurrentGameId, out bool storageRemoved);
            IsInstallPathRemovableTipEnabled = storageRemoved;
            if (GameInstallPath is null || storageRemoved)
            {
                if (await CheckGameReservationAsync(cancellation))
                {
                    Button_PlayTime.Visibility = Visibility.Collapsed;
                    GameState = GameState.Reservation;
                }
                else
                {
                    GameState = GameState.InstallGame;
                }
                return;
            }
            bool isGameExeExists = await _gameLauncherService.IsGameExeExistsAsync(CurrentGameId, cancellation: cancellation);
            localGameVersion = await _gameLauncherService.GetLocalGameVersionAsync(CurrentGameId, cancellation: cancellation);
            if (isGameExeExists && localGameVersion != null)
            {
                GameState = GameState.StartGame;
            }
            else
            {
                GameState = GameState.ResumeDownload;
                return;
            }
            await CheckGameRunningAsync(cancellation);
            (latestGameVersion, predownloadGameVersion) = await _gameLauncherService.GetLatestGameVersionAsync(CurrentGameId, cancellation);
            if (latestGameVersion > localGameVersion)
            {
                GameState = GameState.UpdateGame;
                return;
            }
            if (predownloadGameVersion > localGameVersion)
            {
                IsPredownloadButtonEnabled = true;
                IsPredownloadFinished = await _gameLauncherService.CheckPreDownloadFinishedAsync(CurrentGameId, cancellation: cancellation);
            }
            await CheckDX12ConfigAsync(cancellation);
            if (GameState is not GameState.GameIsRunning)
            {
                if (await _gameLauncherService.CheckPluginUpdateAsync(CurrentGameId, cancellation: cancellation))
                {
                    GameState = GameState.UpdatePlugin;
                }
                else
                {
                    try
                    {
                        if (await _gameLauncherService.CheckGenshinHotUpdateAsync(CurrentGameId, cancellation: cancellation))
                        {
                            GameState = GameState.HotUpdate;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "CheckGenshinHotUpdateAsync");
                        if (ex.Message.Contains("CutsceneDispatchInfo"))
                        {
                            InAppToast.MainWindow?.Warning(Lang.StartGameButton_HotUpdate, Lang.GameLauncherSettingDialog_CutsceneResourceManifestIsUnavailable);
                        }
                        else
                        {
                            InAppToast.MainWindow?.Warning(Lang.GameLauncherPage_HotUpdateCheckFailed, ex.Message);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) when (UnloadCancellationToken.IsCancellationRequested) { }
        catch (miHoYoApiException ex) when (ex.ReturnCode is -100 or -403)
        {
            if (ex.ReturnCode is -100)
            {
                InAppToast.MainWindow?.Warning(null, Lang.InstallGameDialog_LoginExpiredPleaseLogInAgain);
            }
            if (ex.ReturnCode is -403)
            {
                InAppToast.MainWindow?.Warning(null, Lang.InstallGameDialog_CurrentAccountDoesnTHaveBetaTestingAccess);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check game version");
        }
    }


    private async Task<bool> CheckGameReservationAsync(CancellationToken cancellation)
    {
        try
        {
            var gameInfo = await _hoYoPlayService.GetGameInfoAsync(CurrentGameId, cancellation);
            if (gameInfo is null)
            {
                return false;
            }
            if (gameInfo.DisplayStatus is GameInfoDisplayStatus.LAUNCHER_GAME_DISPLAY_STATUS_RESERVATION_ENABLED)
            {
                reservationUrl = gameInfo.Reservation?.Link;
                return true;
            }
        }
        catch { }
        return false;
    }


    private async Task OpenReservationUrlAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(reservationUrl))
            {
                await Launcher.LaunchUriAsync(new(reservationUrl));
            }
        }
        catch { }
    }


    /// <summary>
    /// 检查 DX12 配置
    /// </summary>
    /// <returns></returns>
    private async Task CheckDX12ConfigAsync(CancellationToken cancellation)
    {
        try
        {
            var gameConfig = await _hoYoPlayService.GetGameConfigAsync(CurrentGameId, cancellation);
            if (gameConfig?.EnableDxSwitch is true)
            {
                DXConfig = await _hoYoPlayService.GetDXConfigAsync(CurrentGameId, GPUInfoHelper.GetGPUInfos(), cancellation);
                if (DXConfig?.EnableDXSwitch is true && !DXConfig.AutoMode)
                {
                    IsDX12OptionVisible = true;
                    _enableDX12 = AppConfig.GetEnableDX12(CurrentGameId) ?? DXConfig.UseDX12ByDefault;
                    OnPropertyChanged(nameof(EnableDX12));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check DX12 config");
        }
    }


    /// <summary>
    /// 显示 DX12 说明对话框
    /// </summary>
    private async void Hyperlink_DX12Intro_Click(Hyperlink sender, HyperlinkClickEventArgs args)
    {
        if (DXConfig is not null)
        {
            await new DX12IntroDialog { GameDXConfig = DXConfig, XamlRoot = this.XamlRoot }.ShowAsync();
        }
    }


    /// <summary>
    /// 定位游戏路径
    /// </summary>
    /// <returns></returns>
    private async Task LocateGameAsync()
    {
        try
        {
            string? folder = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                if (DriveHelper.GetDriveType(folder) is DriveType.Network && !new Uri(folder).IsUnc)
                {
                    InAppToast.MainWindow?.Warning(null, Lang.InstallGameDialog_MappedNetworkDrivesAreNotSupportedPleaseUseANetworkSharePathStartingWithDoubleBackslashes, 0);
                }
                else
                {
                    _gameLauncherService.ChangeGameInstallPath(CurrentGameId, folder);
                    CheckGameVersion();
                    WeakReferenceMessenger.Default.Send(new GameInstallPathChangedMessage());
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Locate game");
        }
    }


    /// <summary>
    /// 定位游戏路径
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="args"></param>
    private async void Hyperlink_LocateGame_Click(Hyperlink sender, HyperlinkClickEventArgs args)
    {
        await LocateGameAsync();
    }


    private void OnGameInstallPathChanged(object _, GameInstallPathChangedMessage message)
    {
        CheckGameVersion();
        UpdateGameInstallTask();
    }


    private void OnMainWindowStateChanged(object _, MainWindowStateChangedMessage message)
    {
        try
        {
            if (message.Activate && (message.ElapsedOver(TimeSpan.FromMinutes(10)) || message.IsCrossingHour))
            {
                CheckGameVersion();
            }
        }
        catch { }
    }


    private void OnRemovableStorageDeviceChanged(object _, RemovableStorageDeviceChangedMessage message)
    {
        try
        {
            CheckGameVersion();
        }
        catch { }
    }


    #endregion



    #region Start Game

    private System.Timers.Timer processTimer;


    [ObservableProperty]
    private partial Process? GameProcess { get; set; }


    partial void OnGameProcessChanged(Process? oldValue, Process? newValue)
    {
        processTimer?.Stop();
        if (processTimer is null)
        {
            processTimer = new(1000);
            processTimer.Elapsed += (_, _) => CheckGameExited();
        }
        if (newValue != null)
        {
            processTimer?.Start();
            RunningGameInfo = $"{newValue.ProcessName}.exe ({newValue.Id})";
            RunningGameService.AddRuninngGame(CurrentGameBiz, newValue);
        }
        else
        {
            RunningGameInfo = null;
            _logger.LogInformation("Game process exited");
        }
    }


    public string? RunningGameInfo { get; set => SetProperty(ref field, value); }


    public string? RunningGameTime { get; set => SetProperty(ref field, value); }


    private async Task<bool> CheckGameRunningAsync(CancellationToken cancellation)
    {
        try
        {
            GameProcess = await _gameLauncherService.GetGameProcessAsync(CurrentGameId, cancellation);
            if (GameProcess != null)
            {
                GameState = GameState.GameIsRunning;
                RunningGameTime = TimeSpanToString(DateTime.Now - GameProcess.StartTime);
                _logger.LogInformation("Game is running ({name}, {pid})", GameProcess.ProcessName, GameProcess.Id);
                return true;
            }
        }
        catch { }
        return false;
    }


    private void CheckGameExited()
    {
        try
        {
            if (GameProcess != null)
            {
                if (GameProcess.HasExited)
                {
                    DispatcherQueue.TryEnqueue(() => RunningGameTime = null);
                    DispatcherQueue.TryEnqueue(CheckGameVersion);
                    GameProcess = null;
                }
                else
                {
                    DispatcherQueue?.TryEnqueue(() => RunningGameTime = TimeSpanToString(DateTime.Now - GameProcess.StartTime));
                }
            }
        }
        catch { }
    }


    private static string TimeSpanToString(TimeSpan value)
    {
        return $"{value.Days * 24 + value.Hours:D2}:{value.Minutes:D2}:{value.Seconds:D2}";
    }


    [RelayCommand]
    private async Task StartGameAsync()
    {
        try
        {
            var process = await _gameLauncherService.StartGameAsync(CurrentGameId, cancellation: UnloadCancellationToken);
            if (process is not null)
            {
                GameState = GameState.GameIsRunning;
                GameProcess = process;
                WeakReferenceMessenger.Default.Send(new GameStartedMessage());
            }
        }
        catch (OperationCanceledException) when (UnloadCancellationToken.IsCancellationRequested) { }
        catch (FileNotFoundException)
        {
            CheckGameVersion();
        }
        catch (LaunchPreflightException ex)
        {
            _logger.LogWarning(ex, "Start game blocked by preflight");
            await ShowPreflightDialogAsync(ex.Result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start game");
            if (!OperatingSystem.IsWindows())
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_Launch_Failed + " " + ex.Message, 6000);
            }
        }
    }


    /// <summary>预检未通过时逐项说明「哪一项、为什么、怎么修」，并且能直接点修复。</summary>
    private async Task ShowPreflightDialogAsync(PreflightResult result)
    {
        var panel = new StackPanel { Spacing = 10, MinWidth = 460 };
        foreach (PreflightItem item in result.Items)
        {
            var row = new StackPanel { Spacing = 2 };
            string mark = item.Status switch
            {
                PreflightStatus.Ok => "✓",
                PreflightStatus.Warning => "!",
                _ => "✕",
            };
            row.Children.Add(new TextBlock
            {
                Text = $"{mark}  {item.Name} — {item.Detail}",
                TextWrapping = TextWrapping.Wrap,
                FontWeight = item.Status is PreflightStatus.Failed ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
            });
            if (!string.IsNullOrWhiteSpace(item.Fix))
            {
                var fixRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                fixRow.Children.Add(new TextBlock
                {
                    Text = "    " + item.Fix,
                    FontSize = 12,
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 380,
                    VerticalAlignment = VerticalAlignment.Center,
                });
                if (item.Action != PreflightAction.None)
                {
                    var button = new Button
                    {
                        Content = GetPreflightActionText(item.Action),
                        FontSize = 12,
                        Padding = new Thickness(8, 2, 8, 2),
                    };
                    PreflightAction action = item.Action;
                    button.Click += async (_, _) => await RunPreflightActionAsync(action);
                    fixRow.Children.Add(button);
                }
                row.Children.Add(fixRow);
            }
            panel.Children.Add(row);
        }
        var dialog = new ContentDialog
        {
            Title = Lang.Lunarward_Preflight_Title,
            Content = new ScrollViewer { Content = panel, MaxHeight = 440 },
            PrimaryButtonText = Lang.Lunarward_Preflight_OK,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
        };
        await dialog.ShowAsync();
    }


    private static string GetPreflightActionText(PreflightAction action) => action switch
    {
        PreflightAction.OpenGameSettings => Lang.Lunarward_Preflight_Action_OpenGameSettings,
        PreflightAction.OpenRuntimeSettings => Lang.Lunarward_Preflight_Action_OpenRuntimeSettings,
        PreflightAction.OpenPrefixFolder => Lang.Lunarward_Preflight_Action_OpenPrefixFolder,
        PreflightAction.RebuildPrefix => Lang.Lunarward_Preflight_Action_RebuildPrefix,
        PreflightAction.InstallGraphics => Lang.Lunarward_Preflight_Action_InstallGraphics,
        PreflightAction.ShowPrefixProcesses => Lang.Lunarward_Preflight_Action_ShowPrefixProcesses,
        PreflightAction.OpenGameFolder => Lang.Lunarward_Preflight_Action_OpenGameFolder,
        _ => "",
    };


    /// <summary>执行预检项上的修复动作。每个动作独立 try/catch：修不了也要说清楚，而不是静默。</summary>
    private async Task RunPreflightActionAsync(PreflightAction action)
    {
        try
        {
            switch (action)
            {
                case PreflightAction.OpenGameSettings:
                    MainWindow.Current?.OpenGameSettings();
                    return;

                case PreflightAction.OpenRuntimeSettings:
                    // R26：带上当前游戏绑定的方案与「增强」意图，别再永远落在第一份方案上。
                    MainWindow.Current?.OpenRuntimeSettings(Features.Runtime.ProfileBinding.Resolve(CurrentGameBiz)?.Id);
                    return;

                case PreflightAction.OpenGameFolder:
                {
                    string? folder = GameInstallPath;
                    if (!string.IsNullOrWhiteSpace(folder) && File.Exists(folder))
                    {
                        folder = Path.GetDirectoryName(folder);
                    }
                    if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                    {
                        Helpers.InAppToast.MainWindow?.Warning("还没配置游戏安装目录。", null, 4000);
                        return;
                    }
                    Helpers.OpenUrl.Open(folder);
                    return;
                }

                case PreflightAction.OpenPrefixFolder:
                {
                    string? prefix = await ResolveCurrentPrefixAsync();
                    if (prefix is null || !Directory.Exists(prefix))
                    {
                        Helpers.InAppToast.MainWindow?.Warning("还没有前缀目录，启动一次游戏后会创建。", null, 4000);
                        return;
                    }
                    Helpers.OpenUrl.Open(prefix);
                    return;
                }

                case PreflightAction.RebuildPrefix:
                {
                    RuntimeProfile? profile = ProfileBinding.Resolve(CurrentGameBiz);
                    string? prefix = await ResolveCurrentPrefixAsync();
                    if (profile is null || prefix is null)
                    {
                        Helpers.InAppToast.MainWindow?.Warning("这个游戏还没有绑定运行配置。", null, 4000);
                        return;
                    }
                    string prefixForRebuild = Features.Runtime.EnvironmentResolver.EffectivePrefixPath(profile);
                    if (!prefixForRebuild.StartsWith(Features.Runtime.PrefixManager.PrefixRoot, StringComparison.Ordinal))
                    {
                        Helpers.InAppToast.MainWindow?.Warning("这个前缀目录不在启动器管理范围内（像是你自己指定的位置），不会替你删除。要重建请先把它改成启动器目录下的路径。", null, 6000);
                        return;
                    }
                    var confirm = new ContentDialog
                    {
                        Title = "重建前缀",
                        Content = new TextBlock
                        {
                            Text = $"将删除并重新创建：\n{prefix}\n\n游戏本体与存档不受影响，但前缀里手工装过的补丁/组件会丢失。",
                            TextWrapping = TextWrapping.Wrap,
                        },
                        PrimaryButtonText = "重建",
                        CloseButtonText = "取消",
                        DefaultButton = ContentDialogButton.Close,
                        XamlRoot = this.XamlRoot,
                    };
                    if (await confirm.ShowAsync() != ContentDialogResult.Primary)
                    {
                        return;
                    }
                    string exePath = await ResolveCurrentExePathAsync() ?? "";
                    string gameKey = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();
                    if (Directory.Exists(prefix))
                    {
                        Directory.Delete(prefix, true);
                    }
                    // 创建前缀 = 一次 wineboot（十几秒）或 cp 克隆，必须在后台线程跑（L-127）
                    string created = await Task.Run(() => PrefixManager.EnsurePrefix(
                        profile,
                        string.IsNullOrEmpty(gameKey) ? CurrentGameBiz.Value : gameKey,
                        _logger));
                    Helpers.InAppToast.MainWindow?.Success($"前缀已重建：{created}", null, 5000);
                    return;
                }

                case PreflightAction.InstallGraphics:
                {
                    RuntimeProfile? profile = ProfileBinding.Resolve(CurrentGameBiz);
                    string? prefix = await ResolveCurrentPrefixAsync() ?? (profile is null ? null : await TryEnsureForGraphicsAsync(profile));
                    List<Component> components = profile is null
                        ? new()
                        : ComponentStore.GetGraphics()
                            .Where(c => c.Id == profile.DxvkId || c.Id == profile.Vkd3dId)
                            .ToList();
                    if (prefix is null || components.Count == 0)
                    {
                        Helpers.InAppToast.MainWindow?.Warning("请先在「运行环境」里选择一个 DXVK 或 VKD3D 组件。", null, 5000);
                        MainWindow.Current?.OpenRuntimeSettings();
                        return;
                    }
                    bool ok = await Task.Run(() => GraphicsInstaller.EnsureInstalled(prefix, components, _logger));
                    if (ok)
                    {
                        Helpers.InAppToast.MainWindow?.Success($"{string.Join(" · ", components.Select(x => x.DisplayName))} 已安装到前缀。", null, 4000);
                    }
                    else
                    {
                        Helpers.InAppToast.MainWindow?.Error((string?)null, "安装图形组件失败，详情见日志。", 5000);
                    }
                    return;
                }

                case PreflightAction.ShowPrefixProcesses:
                {
                    string? prefix = await ResolveCurrentPrefixAsync();
                    string output = prefix is null ? "" : await Helpers.ProcessQuery.FindUsersAsync(prefix);
                    await new ContentDialog
                    {
                        Title = "前缀占用",
                        Content = new ScrollViewer
                        {
                            MaxHeight = 320,
                            Content = new TextBlock
                            {
                                Text = string.IsNullOrWhiteSpace(output)
                                    ? "没有找到占用该前缀的进程（可能只是上次异常退出残留的锁文件）。\n再启动一次会尝试接管。"
                                    : output,
                                TextWrapping = TextWrapping.Wrap,
                                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("monospace"),
                                FontSize = 12,
                            },
                        },
                        PrimaryButtonText = "知道了",
                        XamlRoot = this.XamlRoot,
                    }.ShowAsync();
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "preflight action {action}", action);
            Helpers.InAppToast.MainWindow?.Error((string?)null, "修复动作失败：" + ex.Message, 6000);
        }
    }


    private async Task<string?> ResolveCurrentPrefixAsync()
    {
        RuntimeProfile? profile = ProfileBinding.Resolve(CurrentGameBiz);
        string? exe = await ResolveCurrentExePathAsync();
        string gameKey = Path.GetFileNameWithoutExtension(exe ?? "").ToLowerInvariant();
        if (profile is not null && !string.IsNullOrEmpty(gameKey))
        {
            return PrefixManager.TryGetExistingPrefix(profile);
        }
        return string.IsNullOrEmpty(gameKey) ? null : GameLauncherService.ResolvePrefix(gameKey);
    }


    private async Task<string?> ResolveCurrentExePathAsync()
    {
        try
        {
            string? folder = GameLauncherService.GetGameInstallPath(Starward.Core.HoYoPlay.GameId.FromGameBiz(CurrentGameBiz)!);
            if (string.IsNullOrWhiteSpace(folder))
            {
                return null;
            }
            string name = await _gameLauncherService.GetGameExeNameAsync(CurrentGameId, CancellationToken.None);
            string exe = Path.Combine(folder, name);
            return File.Exists(exe) ? exe : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "resolve game exe");
            return null;
        }
    }


    private async Task<string?> TryEnsureForGraphicsAsync(RuntimeProfile profile)
    {
        try
        {
            string? exe = await ResolveCurrentExePathAsync();
            string gameKey = Path.GetFileNameWithoutExtension(exe ?? "").ToLowerInvariant();
            return await Task.Run(() => PrefixManager.EnsurePrefix(
                profile,
                string.IsNullOrEmpty(gameKey) ? CurrentGameBiz.Value : gameKey,
                _logger));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ensure prefix for graphics");
            return null;
        }
    }


    #endregion



    #region Install Game


    private async Task InstallGameAsync()
    {
        try
        {
            if (_gameInstallTask is null)
            {
                await new InstallGameDialog { CurrentGameId = CurrentGameId, XamlRoot = this.XamlRoot, }.ShowAsync();
            }
            else
            {
                await ChangeGameInstallTaskStateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install game {GameBiz}", CurrentGameBiz);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private async Task ResumeDownloadAsync()
    {
        try
        {
            if (!Directory.Exists(GameInstallPath))
            {
                CheckGameVersion();
                return;
            }
            var task = await _gameInstallService.ResumeDownloadAsync(CurrentGameId, GameInstallPath, UnloadCancellationToken);
            if (task is not null)
            {
                _gameInstallTask = task;
                _dispatchTimer.Start();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume download {GameBiz}", CurrentGameBiz);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    [RelayCommand]
    private async Task PredownloadAsync()
    {
        try
        {
            if (_gameInstallTask is null)
            {
                await new PreDownloadDialog { CurrentGameId = this.CurrentGameId, XamlRoot = this.XamlRoot }.ShowAsync();
            }
            else if (_gameInstallTask.Operation is GameInstallOperation.Predownload)
            {
                if (_gameInstallTask.State is RunnerState.Stop or RunnerState.Paused or RunnerState.Error or RunnerState.Queue)
                {
                    await _gameInstallService.ContinueTaskAsync(_gameInstallTask);
                    _dispatchTimer.Start();
                }
                else if (_gameInstallTask.State is RunnerState.Running)
                {
                    await _gameInstallService.PauseTaskAsync(_gameInstallTask);
                    _dispatchTimer.Start();
                }
                else
                {
                    // GameInstallState.Stop
                    CheckGameVersion();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(PredownloadAsync));
            if (_gameInstallTask?.Operation is GameInstallOperation.Predownload)
            {
                _gameInstallTask.State = RunnerState.Error;
                _gameInstallTask.ErrorMessage = ex.Message;
            }
            else
            {
                InAppToast.MainWindow?.Error(ex);
            }
        }
    }


    private async Task UpdateGameAsync()
    {
        try
        {
            if (localGameVersion is not null && latestGameVersion > localGameVersion)
            {
                GameInstallProgress? task = await _gameInstallService.StartUpdateAsync(CurrentGameId, GameInstallPath!, UnloadCancellationToken);
                if (task is not null)
                {
                    _gameInstallTask = task;
                    _dispatchTimer.Start();
                }
            }
            else
            {
                CheckGameVersion();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update game {GameBiz}", CurrentGameBiz);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private async Task UpdatePluginAsync()
    {
        try
        {
            if (GameState is not GameState.GameIsRunning && Directory.Exists(GameInstallPath))
            {
                GameInstallProgress? task = await _gameInstallService.StarUpdatePluginAsync(CurrentGameId, GameInstallPath!, UnloadCancellationToken);
                if (task is not null)
                {
                    _gameInstallTask = task;
                    _dispatchTimer.Start();
                }
            }
            else
            {
                CheckGameVersion();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update plugin {GameBiz}", CurrentGameBiz);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    private async Task HotUpdateAsync()
    {
        try
        {
            if (GameState is not GameState.GameIsRunning && Directory.Exists(GameInstallPath))
            {
                GameInstallProgress? task = await _gameInstallService.StarHotUpdateAsync(CurrentGameId, GameInstallPath!, UnloadCancellationToken);
                if (task is not null)
                {
                    _gameInstallTask = task;
                    _dispatchTimer.Start();
                }
            }
            else
            {
                CheckGameVersion();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update plugin {GameBiz}", CurrentGameBiz);
            InAppToast.MainWindow?.Error(ex);
        }
    }


    #endregion



    #region Game Install Task


    private GameInstallProgress? _gameInstallTask;


    private async Task ChangeGameInstallTaskStateAsync()
    {
        try
        {
            if (_gameInstallTask is null)
            {
                CheckGameVersion();
            }
            else if (_gameInstallTask.Operation is not GameInstallOperation.Predownload)
            {
                if (_gameInstallTask.State is RunnerState.Stop or RunnerState.Paused or RunnerState.Error or RunnerState.Queue)
                {
                    await _gameInstallService.ContinueTaskAsync(_gameInstallTask);
                    _dispatchTimer.Start();
                }
                else if (_gameInstallTask.State is RunnerState.Running)
                {
                    await _gameInstallService.PauseTaskAsync(_gameInstallTask);
                    _dispatchTimer.Start();
                }
                else
                {
                    // GameInstallState.Stop
                    CheckGameVersion();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change game install task state {GameBiz}", CurrentGameBiz);
        }
    }


    private void UpdateGameInstallTask()
    {
        try
        {
            _gameInstallTask = _gameInstallService.GetGameInstallTask(CurrentGameId);
            if (_gameInstallTask is not null)
            {
                if (_gameInstallTask.Operation is GameInstallOperation.Predownload)
                {
                    IsPredownloadButtonEnabled = true;
                }
                _dispatchTimer.Start();
            }
        }
        catch { }
    }


    private void OnGameInstallTaskStarted(object _, GameInstallTaskStartedMessage message)
    {
        if (message.InstallTask.GameId == CurrentGameId)
        {
            _gameInstallTask = message.InstallTask;
            _dispatchTimer.Start();
        }
    }


    private void UpdateGameInstallTaskProgress(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        if (_gameInstallTask is null)
        {
            _dispatchTimer.Stop();
            return;
        }
        try
        {
            if (_gameInstallTask.Operation is GameInstallOperation.Predownload)
            {
                Button_Predownload.UpdateGameInstallTaskState(_gameInstallTask);
            }
            else
            {
                GameState = GameState.Installing;
                Button_StartGame.UpdateGameInstallTaskState(_gameInstallTask);
            }
            if (_gameInstallTask.State is RunnerState.Error)
            {
                _dispatchTimer.Stop();
            }
            else if (_gameInstallTask.State is RunnerState.Stop or RunnerState.Finish)
            {
                _dispatchTimer.Stop();
                _gameInstallTask = null;
                CheckGameVersion();
            }
        }
        catch { }
    }


    #endregion



    #region Drop Background File


    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            Border_BackgroundDragIn.Opacity = 1;
        }
    }


    private async void RootGrid_Drop(object sender, DragEventArgs e)
    {
        Border_BackgroundDragIn.Opacity = 0;
        var defer = e.GetDeferral();
        try
        {
            if ((await e.DataView.GetStorageItemsAsync()).FirstOrDefault() is StorageFile file)
            {
                string? name = await BackgroundService.ChangeCustomBackgroundFileAsync(file.Path);
                if (string.IsNullOrWhiteSpace(name))
                {
                    return;
                }
                AppConfig.SetCustomBg(CurrentGameId, name);
                await AppBackground.Current.UpdateBackgroundAsync();
            }
        }
        catch (COMException ex)
        {
            InAppToast.MainWindow?.Error(Lang.GameLauncherSettingDialog_CannotDecodeFile);
            _logger.LogError(ex, "Change custom background failed");
        }
        catch (Exception ex)
        {
            InAppToast.MainWindow?.Error(Lang.GameLauncherSettingDialog_AnUnknownErrorOccurredPleaseCheckTheLogs);
            _logger.LogError(ex, "Change custom background failed");
        }
        defer.Complete();
    }


    private void RootGrid_DragLeave(object sender, DragEventArgs e)
    {
        Border_BackgroundDragIn.Opacity = 0;
    }


    #endregion



    #region Game Setting


    [RelayCommand]
    private async Task OpenGameLauncherSettingDialogAsync()
    {
        await new GameLauncherSettingDialog { CurrentGameId = this.CurrentGameId, XamlRoot = this.XamlRoot }.ShowAsync();
    }




    #endregion



    #region Switch Background Image


    private const string PlayIcon = "\uF5B0";

    private const string PauseIcon = "\uE769";


    /// <summary>当前游戏可选的背景（0.19.0 起是 GameBackgroundModel：Id / Type / Last / GameBackground）。</summary>
    public List<GameBackgroundModel> BackgroundImages { get; set => SetProperty(ref field, value); }

    public bool CanStopVideo { get; set => SetProperty(ref field, value); }

    public string StartStopButtonIcon { get; set => SetProperty(ref field, value); }


    private int currentBackgroundImageIndex;
    public int CurrentBackgroundImageIndex
    {
        get => currentBackgroundImageIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, Math.Max(0, (BackgroundImages?.Count ?? 1) - 1));
            if (SetProperty(ref currentBackgroundImageIndex, clamped))
            {
                ChangeBackgroundImageIndex(clamped);
                UpdateBackgroundDots();
            }
        }
    }


    private readonly List<Button> _backgroundDots = new();


    /// <summary>
    /// 生成背景切换的圆点按钮。Uno 的 PipsPager 在内嵌 ItemsRepeater 模板下既不渲染也不响应点击（L-69），
    /// 这里直接用 Button + FontIcon，点击即切换。
    /// </summary>
    private void RebuildBackgroundDots()
    {
        StackPanel_BackgroundDots.Children.Clear();
        _backgroundDots.Clear();
        int count = BackgroundImages?.Count ?? 0;
        for (int i = 0; i < count; i++)
        {
            int index = i;
            var icon = new FontIcon
            {
                Glyph = "\uEA3B",
                FontSize = 8,
                IsTextScaleFactorEnabled = false,
            };
            var button = new Button
            {
                Width = 20,
                Height = 12,
                Padding = new Thickness(0),
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Content = icon,
                Tag = index,
            };
            button.Click += (_, _) => CurrentBackgroundImageIndex = index;
            ToolTipService.SetToolTip(button, $"{index + 1}");
            _backgroundDots.Add(button);
            StackPanel_BackgroundDots.Children.Add(button);
        }
        UpdateBackgroundDots();
    }


    private void UpdateBackgroundDots()
    {
        for (int i = 0; i < _backgroundDots.Count; i++)
        {
            if (_backgroundDots[i].Content is FontIcon icon)
            {
                icon.Foreground = i == currentBackgroundImageIndex
                    ? (Brush)Application.Current.Resources["AccentTextFillColorPrimaryBrush"]
                    : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            }
        }
    }


    private void OnBackgroundChanged(object _, BackgroundChangedMessage message)
    {
        // 0.19.0：换自定义壁纸/换游戏时消息不带具体背景，重读 AppBackground 的状态即可。
        _ = InitializeBackgameImageSwitcherAsync();
    }


    /// <summary>视频按钮状态统一从 AppBackground 同步（0.19.0 的 IsVideoPlaying / CurrentGameBackground）。</summary>
    private void UpdateVideoButtonState()
    {
        GameBackgroundModel? current = AppBackground.Current.CurrentGameBackground;
        CanStopVideo = current is not null && current.GameBackground.Type is GameBackground.BACKGROUND_TYPE_VIDEO;
        StartStopButtonIcon = AppBackground.Current.IsVideoPlaying ? PauseIcon : PlayIcon;
    }


    private async Task InitializeBackgameImageSwitcherAsync()
    {
        try
        {
            CanStopVideo = false;
            StartStopButtonIcon = PauseIcon;
            // LUNARWARD-DEVIATION: 上游 0.19.0 把内联切换器整个换成了 BackgroundSwitcher 控件，
            // 这里保留旧的圆点切换条（GameLauncherPage.xaml 不在本次授权范围），
            // 但列表与选中项统一从 AppBackground 读，避免两套状态。
            await AppBackground.Current.UpdateBackgroundAsync();
            BackgroundImages = AppBackground.Current.GameBackgrounds?.ToList() ?? new List<GameBackgroundModel>();
            if (BackgroundImages.Count > 1)
            {
                Border_SwitchBackgroundImage.Visibility = Visibility.Visible;
                RebuildBackgroundDots();
                GameBackgroundModel? current = AppBackground.Current.CurrentGameBackground;
                if (current is not null)
                {
                    int index = BackgroundImages.IndexOf(current);
                    if (index >= 0)
                    {
                        currentBackgroundImageIndex = Math.Clamp(index, 0, BackgroundImages.Count - 1);
                        OnPropertyChanged(nameof(CurrentBackgroundImageIndex));
                    }
                }
                UpdateVideoButtonState();
            }
            else
            {
                Border_SwitchBackgroundImage.Visibility = Visibility.Collapsed;
            }
#if DEBUG
            // 开发期验证背景切换（没有可用的合成点击）：LUNARWARD_DEV_BG_CYCLE=1 时 6 秒后切到下一张
            if (Environment.GetEnvironmentVariable("LUNARWARD_DEV_BG_CYCLE") == "1")
            {
                _logger.LogInformation("[dev] backgrounds: {List}", string.Join(" | ", BackgroundImages.Select((x, i) => $"{i}:{x.Type}:{Path.GetFileName(x.GameBackground.Background?.Url)}{(x.GameBackground.Video is null ? "" : "+video")}")));
            }
            if (Environment.GetEnvironmentVariable("LUNARWARD_DEV_BG_CYCLE") == "1" && BackgroundImages.Count > 1)
            {
                var timer = DispatcherQueue.CreateTimer();
                timer.Interval = TimeSpan.FromSeconds(6);
                timer.Tick += (_, _) =>
                {
                    timer.Stop();
                    CurrentBackgroundImageIndex = (currentBackgroundImageIndex + 1) % BackgroundImages.Count;
                    _logger.LogInformation("[dev] bg cycle -> {Index}/{Count}", currentBackgroundImageIndex, BackgroundImages.Count);
                };
                timer.Start();
            }
#endif
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize background image switcher {GameBiz}", CurrentGameBiz);
        }
    }


    private async void ChangeBackgroundImageIndex(int index)
    {
        try
        {
            if (index < 0 || index >= BackgroundImages.Count)
            {
                return;
            }
            await AppBackground.Current.ChangeCurrentBackgroundIndexAsync(index);
            UpdateVideoButtonState();
            UpdateBackgroundDots();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change background image {GameBiz}", CurrentGameBiz);
        }
    }


    private void Border_SwitchBackgroundImage_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Border_SwitchBackgroundImage.Opacity = 1;
    }


    private void Border_SwitchBackgroundImage_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        Border_SwitchBackgroundImage.Opacity = 0;
    }


    int _switchBackgroundTotalDelta = 0;

    private void Border_SwitchBackgroundImage_PointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        int delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        _switchBackgroundTotalDelta += delta;
        if (_switchBackgroundTotalDelta <= -120)
        {
            CurrentBackgroundImageIndex++;
            _switchBackgroundTotalDelta = 0;
        }
        else if (_switchBackgroundTotalDelta >= 120)
        {
            CurrentBackgroundImageIndex--;
            _switchBackgroundTotalDelta = 0;
        }
    }


    [RelayCommand]
    public void OpenBackgroundViewWindow()
    {
        try
        {
            new BackgroundViewWindow().Show();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open background view window.");
        }
    }


    [RelayCommand]
    private async Task CopyCurrentBackgroundImageAsync()
    {
        try
        {
            await AppBackground.Current.CopyCurrentBackgroundAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Copy current background image {GameBiz}", CurrentGameBiz);
        }
    }


    [RelayCommand]
    private async Task SaveCurrentBackgroundImageAsync()
    {
        try
        {
            await AppBackground.Current.SaveCurrentBackgroundAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save as current background image {GameBiz}", CurrentGameBiz);
        }
    }


    [RelayCommand]
    private void StartOrStopVideoBackground()
    {
        try
        {
            AppBackground.Current.PlayPauseVideo();
            GameBackgroundModel? current = AppBackground.Current.CurrentGameBackground;
            StartStopButtonIcon = current?.Type is 1 ? PauseIcon : PlayIcon;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start or stop video background {GameBiz}", CurrentGameBiz);
        }
    }


    #endregion



    #region Cloud Game


    private void CheckCloudGame()
    {
        try
        {
            Process? process = CloudGameService.GetCloudGameProcess(CurrentGameId);
            if (process is not null)
            {
                RunningGameService.AddRuninngGame(CurrentGameBiz, process);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check cloud game {GameBiz}", CurrentGameBiz);
        }
    }


    #endregion


}
