using CommunityToolkit.Mvvm.ComponentModel;
using Starward.Language;
using Starward.Features.Runtime;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Background;
using Starward.Features.CloudGame;
using Starward.Features.GameInstall;
using Starward.Features.HoYoPlay;
using Starward.Features.Overlay;
using Starward.Features.ViewHost;
using Starward.Frameworks;
using Starward.Helpers;
using Starward.RPC.GameInstall;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Timers;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;


namespace Starward.Features.GameLauncher;

public sealed partial class GameLauncherPage : PageBase
{


    private readonly ILogger<GameLauncherPage> _logger = AppConfig.GetLogger<GameLauncherPage>();

    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();

    private readonly GamePackageService _gamePackageService = AppConfig.GetService<GamePackageService>();

    private readonly BackgroundService _backgroundService = AppConfig.GetService<BackgroundService>();

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



    protected override void OnLoaded()
    {
        StartInstallTaskWatcher();
        InitializeGameFeature();
        CheckGameVersion();
        UpdateGameInstallTask();
        CheckCloudGame();
        _ = InitializeGameServerAsync();
        _ = InitializeBackgameImageSwitcherAsync();
        WeakReferenceMessenger.Default.Register<GameInstallPathChangedMessage>(this, OnGameInstallPathChanged);
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, OnMainWindowStateChanged);
        WeakReferenceMessenger.Default.Register<RemovableStorageDeviceChangedMessage>(this, OnRemovableStorageDeviceChanged);
        WeakReferenceMessenger.Default.Register<GameInstallTaskStartedMessage>(this, OnGameInstallTaskStarted);
        WeakReferenceMessenger.Default.Register<BackgroundChangedMessage>(this, OnBackgroundChanged);
        WeakReferenceMessenger.Default.Register<GameLauncherPage, CurrentGameChangedMessage>(this, (r, m) => r.OnCurrentGameChanged(m));
    }



    /// <summary>切换游戏（页面已缓存，不会重建）：把本页与下挂控件换成新游戏的内容（D62）。</summary>
    private void OnCurrentGameChanged(CurrentGameChangedMessage message)
    {
        try
        {
            if (CurrentGameId == message.GameId)
            {
                return;
            }
            CurrentGameId = message.GameId;
            CurrentGameBiz = message.GameId.GameBiz;
            InitializeGameFeature();
            CheckGameVersion();
            UpdateGameInstallTask();
            _ = InitializeGameServerAsync();
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
        BackgroundImages = null!;
    }




    private void InitializeGameFeature()
    {
        GameFeatureConfig feature = GameFeatureConfig.FromGameId(CurrentGameId);
        if (feature.SupportCloudGame)
        {
            Button_CloudGame.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
        if (feature.SupportGameAccountSwitcher && AppConfig.EnableGameAccountSwitcher)
        {
            EnableGameAccountSwitcher = true;
        }
    }


    public bool EnableGameAccountSwitcher { get; set => SetProperty(ref field, value); }



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
            case GameState.ResumeDownload:
                await ResumeDownloadAsync();
                break;
            case GameState.ComingSoon:
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
            GameInfo? gameInfo;
            if (CurrentGameBiz == GameBiz.bh3_global)
            {
                gameInfo = await _hoYoPlayService.GetGameInfoAsync(GameId.FromGameBiz(GameBiz.bh3_global)!);
            }
            else
            {
                gameInfo = await _hoYoPlayService.GetGameInfoAsync(CurrentGameId);
            }
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
                        CurrentGameId.Id = SelectedGameServer.GameId;
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


    private bool isGameExeExists;


    /// <summary>
    /// 是否显示 DX12 选项
    /// </summary>
    public bool IsDX12OptionVisible { get; set => SetProperty(ref field, value); }


    /// <summary>
    /// DX12 配置
    /// </summary>
    private GameDXConfig? _dxConfig;


    /// <summary>
    /// 启用 DX12
    /// </summary>
    public bool EnableDX12
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.SetEnableDX12(CurrentGameBiz, value);
            }
        }
    }


    private async void CheckGameVersion()
    {
        try
        {
            GameInstallPath = GameLauncherService.GetGameInstallPath(CurrentGameId, out bool storageRemoved);
            IsInstallPathRemovableTipEnabled = storageRemoved;
            if (GameInstallPath is null || storageRemoved)
            {
                GameState = GameState.InstallGame;
                return;
            }
            isGameExeExists = await _gameLauncherService.IsGameExeExistsAsync(CurrentGameId);
            localGameVersion = await _gameLauncherService.GetLocalGameVersionAsync(CurrentGameId);
            if (isGameExeExists && localGameVersion != null)
            {
                GameState = GameState.StartGame;
            }
            else
            {
                GameState = GameState.ResumeDownload;
                return;
            }
            await CheckGameRunningAsync();
            (latestGameVersion, predownloadGameVersion) = await _gameLauncherService.GetLatestGameVersionAsync(CurrentGameId);
            if (latestGameVersion > localGameVersion)
            {
                GameState = GameState.UpdateGame;
                return;
            }
            if (predownloadGameVersion > localGameVersion)
            {
                IsPredownloadButtonEnabled = true;
                IsPredownloadFinished = await _gamePackageService.CheckPreDownloadFinishedAsync(CurrentGameId);
            }
            _ = CheckDX12ConfigAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check game version");
        }
    }



    /// <summary>
    /// 检查 DX12 配置
    /// </summary>
    /// <returns></returns>
    private async Task CheckDX12ConfigAsync()
    {
        try
        {
            EnableDX12 = AppConfig.GetEnableDX12(CurrentGameBiz);
            if (EnableDX12)
            {
                IsDX12OptionVisible = true;
            }

            List<GameDXConfig> dxConfigs = await _hoYoPlayService.GetGameDXConfigsAsync([CurrentGameId]);
            _dxConfig = dxConfigs.FirstOrDefault(x => x.GameId == CurrentGameId);

            if (_dxConfig?.EnableDXSwitch is true)
            {
                IsDX12OptionVisible = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check DX12 config");
        }
    }


    /// <summary>
    /// 获取 DX12 启动参数
    /// </summary>
    /// <returns></returns>
    public string? GetDX12LaunchArgument()
    {
        if (EnableDX12 && _dxConfig is not null)
        {
            return _dxConfig.CmdArgs;
        }
        return null;
    }


    /// <summary>
    /// 显示 DX12 说明对话框
    /// </summary>
    private async void Hyperlink_DX12Intro_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        if (_dxConfig is not null)
        {
            await new DX12IntroDialog { GameDXConfig = _dxConfig, XamlRoot = this.XamlRoot }.ShowAsync();
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
                    GameLauncherService.ChangeGameInstallPath(CurrentGameId, folder);
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
    private async void Hyperlink_LocateGame_Click(Microsoft.UI.Xaml.Documents.Hyperlink sender, Microsoft.UI.Xaml.Documents.HyperlinkClickEventArgs args)
    {
        await LocateGameAsync();
    }




    private void OnGameInstallPathChanged(object _, GameInstallPathChangedMessage message)
    {
        CheckGameVersion();
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




    private Timer processTimer;


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


    private async Task<bool> CheckGameRunningAsync()
    {
        try
        {
            GameProcess = await _gameLauncherService.GetGameProcessAsync(CurrentGameId);
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
            var process = await _gameLauncherService.StartGameAsync(CurrentGameId);
            if (process is not null)
            {
                GameState = GameState.GameIsRunning;
                GameProcess = process;
                WeakReferenceMessenger.Default.Send(new GameStartedMessage());
            }
        }
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
        PreflightAction.OpenGameSettings => "打开游戏设置",
        PreflightAction.OpenRuntimeSettings => "打开运行环境",
        PreflightAction.OpenPrefixFolder => "打开前缀目录",
        PreflightAction.RebuildPrefix => "重建前缀",
        PreflightAction.InstallGraphics => "安装图形组件",
        PreflightAction.ShowPrefixProcesses => "查看占用进程",
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
                    MainWindow.Current?.OpenRuntimeSettings();
                    return;

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
                    if (profile.PrefixMode == "external")
                    {
                        Helpers.InAppToast.MainWindow?.Warning("当前配置使用的是已有目录，启动器不会删除它。要重建请先在「运行环境」里改成新建。", null, 6000);
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
                    string created = PrefixManager.EnsurePrefix(profile, string.IsNullOrEmpty(gameKey) ? CurrentGameBiz.Value : gameKey, _logger);
                    Helpers.InAppToast.MainWindow?.Success($"前缀已重建：{created}", null, 5000);
                    return;
                }

                case PreflightAction.InstallGraphics:
                {
                    RuntimeProfile? profile = ProfileBinding.Resolve(CurrentGameBiz);
                    string? prefix = await ResolveCurrentPrefixAsync() ?? (profile is null ? null : await TryEnsureForGraphicsAsync(profile));
                    Component? component = profile is null ? null : ComponentStore.GetGraphics().FirstOrDefault(c => c.Id == profile.GraphicsId);
                    if (prefix is null || component is null)
                    {
                        Helpers.InAppToast.MainWindow?.Warning("请先在「运行环境」里选择一个 DXVK/VKD3D 组件。", null, 5000);
                        MainWindow.Current?.OpenRuntimeSettings();
                        return;
                    }
                    bool ok = GraphicsInstaller.EnsureInstalled(prefix, component, _logger);
                    if (ok)
                    {
                        Helpers.InAppToast.MainWindow?.Success($"{component.DisplayName} 已安装到前缀。", null, 4000);
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
            return PrefixManager.TryGetExistingPrefix(profile, gameKey);
        }
        return string.IsNullOrEmpty(gameKey) ? null : GameLauncherService.ResolvePrefix(gameKey);
    }


    private async Task<string?> ResolveCurrentExePathAsync()
    {
        try
        {
            string? folder = GameLauncherService.GetGameInstallPath(CurrentGameBiz);
            if (string.IsNullOrWhiteSpace(folder))
            {
                return null;
            }
            string name = await _gameLauncherService.GetGameExeNameAsync(CurrentGameId);
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
            return PrefixManager.EnsurePrefix(profile, string.IsNullOrEmpty(gameKey) ? CurrentGameBiz.Value : gameKey, _logger);
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
            AudioLanguage audio = await _gamePackageService.GetAudioLanguageAsync(CurrentGameId, GameInstallPath);
            var task = await _gameInstallService.StartInstallAsync(CurrentGameId, GameInstallPath, audio);
            if (task is not null)
            {
                _gameInstallTask = task;
                _dispatchTimer.Start();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Resume download {GameBiz}", CurrentGameBiz);
        }
    }






    #endregion




    #region Predownload




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
                if (_gameInstallTask.State is GameInstallState.Stop or GameInstallState.Paused or GameInstallState.Error or GameInstallState.Queueing)
                {
                    await _gameInstallService.ContinueTaskAsync(_gameInstallTask);
                    _dispatchTimer.Start();
                }
                else if (_gameInstallTask.State is GameInstallState.Waiting or GameInstallState.Downloading or GameInstallState.Decompressing or GameInstallState.Merging or GameInstallState.Verifying)
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
                _gameInstallTask.State = GameInstallState.Error;
                _gameInstallTask.ErrorMessage = ex.Message;
            }
        }
    }





    #endregion



    #region Update



    private async Task UpdateGameAsync()
    {
        try
        {
            if (localGameVersion is not null && latestGameVersion > localGameVersion)
            {
                AudioLanguage audio = await _gamePackageService.GetAudioLanguageAsync(CurrentGameId, GameInstallPath);
                GameInstallContext? task = await _gameInstallService.StartUpdateAsync(CurrentGameId, GameInstallPath!, audio);
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
        }
    }



    #endregion



    #region Game Install Task




    private GameInstallContext? _gameInstallTask;



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
                if (_gameInstallTask.State is GameInstallState.Stop or GameInstallState.Paused or GameInstallState.Error or GameInstallState.Queueing)
                {
                    await _gameInstallService.ContinueTaskAsync(_gameInstallTask);
                    _dispatchTimer.Start();
                }
                else if (_gameInstallTask.State is GameInstallState.Waiting or GameInstallState.Downloading or GameInstallState.Decompressing or GameInstallState.Merging or GameInstallState.Verifying)
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
            _gameInstallTask ??= _gameInstallService.GetGameInstallTask(CurrentGameId);
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
            if (_gameInstallTask.State is GameInstallState.Error)
            {
                _dispatchTimer.Stop();
            }
            else if (_gameInstallTask.State is GameInstallState.Stop or GameInstallState.Finish)
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




    private void RootGrid_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            Border_BackgroundDragIn.Opacity = 1;
        }
    }




    private async void RootGrid_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
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
                AppConfig.SetCustomBg(CurrentGameBiz, name);
                AppConfig.SetEnableCustomBg(CurrentGameBiz, true);
                WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage());
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



    private void RootGrid_DragLeave(object sender, Microsoft.UI.Xaml.DragEventArgs e)
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


    public List<GameBackground> BackgroundImages { get; set => SetProperty(ref field, value); }

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
        if (message.GameBackground is null)
        {
            _ = InitializeBackgameImageSwitcherAsync();
        }
    }


    private async Task InitializeBackgameImageSwitcherAsync()
    {
        try
        {
            CanStopVideo = false;
            BackgroundImages = await _backgroundService.GetGameBackgroundsAsync(CurrentGameId);
            if (BackgroundImages.Count > 1)
            {
                Border_SwitchBackgroundImage.Visibility = Visibility.Visible;
                RebuildBackgroundDots();
                GameBackground? currentBackground = await _backgroundService.GetSuggestedGameBackgroundAsync(CurrentGameId);
                if (currentBackground != null && BackgroundImages.FirstOrDefault(x => x.Id == currentBackground.Id) is GameBackground current)
                {
                    currentBackgroundImageIndex = Math.Clamp(BackgroundImages.IndexOf(current), 0, BackgroundImages.Count - 1);
                    OnPropertyChanged(nameof(CurrentBackgroundImageIndex));
                    CanStopVideo = current.Type is GameBackground.BACKGROUND_TYPE_VIDEO;
                    if (CanStopVideo)
                    {
                        current.StopVideo = currentBackground.StopVideo;
                        StartStopButtonIcon = current.StopVideo ? PlayIcon : PauseIcon;
                    }
                }
            }
            else
            {
                Border_SwitchBackgroundImage.Visibility = Visibility.Collapsed;
            }
#if DEBUG
            // 开发期验证背景切换（没有可用的合成点击）：LUNARWARD_DEV_BG_CYCLE=1 时 6 秒后切到下一张
            if (Environment.GetEnvironmentVariable("LUNARWARD_DEV_BG_CYCLE") == "1")
            {
                _logger.LogInformation("[dev] backgrounds: {List}", string.Join(" | ", BackgroundImages.Select((x, i) => $"{i}:{x.Type}:{Path.GetFileName(x.Background.Url)}{(x.Video is null ? "" : "+video")}")));
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


    private void ChangeBackgroundImageIndex(int index)
    {
        try
        {
            if (index < 0 || index >= BackgroundImages.Count)
            {
                return;
            }
            GameBackground current = BackgroundImages[index];
            WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage(current));
            CanStopVideo = current.Type is GameBackground.BACKGROUND_TYPE_VIDEO;
            if (CanStopVideo)
            {
                StartStopButtonIcon = current.StopVideo ? PlayIcon : PauseIcon;
            }
        }
        catch { }
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
            string? path = null;
            GameBackground? background = AppBackground.Current.CurrentGameBackground;
            if (background?.Type is GameBackground.BACKGROUND_TYPE_CUSTOM)
            {
                path = background.Background.Url;
            }
            else if (background?.Type is GameBackground.BACKGROUND_TYPE_VIDEO && !background.StopVideo)
            {
                string name = Path.GetFileName(background.Video.Url);
                path = BackgroundService.GetBgFilePath(name);
            }
            else if (background is not null)
            {
                string name = Path.GetFileName(background.Background.Url);
                path = BackgroundService.GetBgFilePath(name);
            }
            if (File.Exists(path))
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, file);
                InAppToast.MainWindow?.Information(Lang.Common_CopiedToClipboard);
            }
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
            string? path = null;
            GameBackground? background = AppBackground.Current.CurrentGameBackground;
            if (background?.Type is GameBackground.BACKGROUND_TYPE_CUSTOM)
            {
                path = background.Background.Url;
            }
            else if (background?.Type is GameBackground.BACKGROUND_TYPE_VIDEO && !background.StopVideo)
            {
                string name = Path.GetFileName(background.Video.Url);
                path = BackgroundService.GetBgFilePath(name);
            }
            else if (background is not null)
            {
                string name = Path.GetFileName(background.Background.Url);
                path = BackgroundService.GetBgFilePath(name);
            }
            if (File.Exists(path))
            {
                var savePath = await FileDialogHelper.OpenSaveFileDialogAsync(this.XamlRoot, Path.GetFileName(path));
                if (!string.IsNullOrWhiteSpace(savePath))
                {
                    File.Copy(path, savePath, true);
                    var file = await StorageFile.GetFileFromPathAsync(savePath);
                    var options = new FolderLauncherOptions();
                    options.ItemsToSelect.Add(file);
                    await Launcher.LaunchFolderAsync(await file.GetParentAsync(), options);
                }
            }
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
            GameBackground current = BackgroundImages[CurrentBackgroundImageIndex];
            if (current.Type is GameBackground.BACKGROUND_TYPE_VIDEO)
            {
                current.StopVideo = !current.StopVideo;
                StartStopButtonIcon = current.StopVideo ? PlayIcon : PauseIcon;
                WeakReferenceMessenger.Default.Send(new BackgroundChangedMessage(current));
            }
        }
        catch { }
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
