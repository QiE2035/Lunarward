using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using NuGet.Versioning;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Dev;
using Starward.Features.Gacha;
using Starward.Features.GameLauncher;
using Starward.Features.GamepadControl;
using Starward.Features.GameRecord;
using Starward.Features.GameSelector;
using Starward.Features.Passport;
using Starward.Features.Screenshot;
using Starward.Features.SelfQuery;
using Starward.Features.Setting;
using Starward.Features.Update;
using Starward.GameInstall;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Starward.Features.GameSetting;
using Starward.Features.RPC;


namespace Starward.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class MainView : UserControl
{

    public static MainView Current { get; private set; }


    private readonly ILogger<MainView> _logger = AppConfig.GetLogger<MainView>();

    private readonly PassportService _passportService = AppConfig.GetService<PassportService>();

    private readonly HoYoPlayService _hoYoPlayService = AppConfig.GetService<HoYoPlayService>();

    private readonly WindowsDeviceFpService _windowsDeviceFpService = AppConfig.GetService<WindowsDeviceFpService>();


    public GameId? CurrentGameId { get; private set => SetProperty(ref field, value); }

    public LauncherConfigModel? CurrentLauncherConfig { get; private set => SetProperty(ref field, value); }

    private GameFeatureConfig CurrentGameFeatureConfig { get; set; }



    public MainView()
    {
        Current = this;
        this.InitializeComponent();
        InitializeMainView();
    }



    private void InitializeMainView()
    {
        this.Loaded += MainView_Loaded;
        WeakReferenceMessenger.Default.Register<MainViewNavigateMessage>(this, OnMainViewNavigateMessageReceived);
        WeakReferenceMessenger.Default.Register<BH3GlobalGameServerChangedMessage>(this, OnBH3GlobalGameServerChanged);
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, (_, _) => _ = CheckUpdateOrShowRecentUpdateContentAsync());
        WeakReferenceMessenger.Default.Register<EnabledLauncherConfigChangedMessage>(this, OnEnabledLauncherConfigChanged);
        GameId? gameId = GameSwitcher.CurrentGameIcon?.GameId;
        if (gameId?.GameBiz == GameBiz.bh3_global)
        {
            string? id = AppConfig.LastGameIdOfBH3Global;
            if (!string.IsNullOrWhiteSpace(id))
            {
                gameId = new GameId(id, GameBiz.bh3_global);
            }
        }
        CurrentGameId = gameId;
        CurrentLauncherConfig = LauncherConfigModel.FromId(GameSwitcher.CurrentGameIcon?.LauncherId);
        UpdateNavigationView();
        InitializePassportUser();
        if (CurrentGameId is null)
        {
            ShowLauncherConfigView();
        }
    }




    /// <summary>托盘菜单「打开设置」用。</summary>
    /// <summary>导航到当前游戏的设置页（预检修复按钮用）。</summary>
    /// <summary>当前页类型（L1.5 UI 自检用）。</summary>
    public Type? CurrentPageType => MainView_Frame?.SourcePageType;


    /// <summary>当前页实例（L1.5 UI 自检用）。</summary>
    public object? CurrentPageContent => MainView_Frame?.Content;


    public void NavigateToGameSettings()
    {
        MainView_Frame.Navigate(typeof(GameSettingPage), CurrentGameId, new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
    }


    /// <summary>
    /// Esc 的第一优先级：先把「游戏选择层」或打开的下拉/浮出菜单收掉，
    /// 都收掉了才轮到把窗口隐藏到托盘（否则用户只是想退出选择层，窗口却消失了）。
    /// </summary>
    public bool TryCloseGameSelector()
    {
        try
        {
            if (Microsoft.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot).FirstOrDefault() is Microsoft.UI.Xaml.Controls.Primitives.Popup popup)
            {
                popup.IsOpen = false;
                return true;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "TryCloseGameSelector failed");
        }
        return false;
    }


    public void NavigateToSettings()
    {
        MainView_Frame.Navigate(typeof(Features.Setting.SettingPage), CurrentGameId, new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
    }


    /// <summary>打开「运行环境」（D80：它已是一级页，在 footer 里，不再走设置页的目标传递）。</summary>
    public void NavigateToRuntimeEnvironment()
    {
        NavigateTo(typeof(Features.Setting.RuntimeEnvironmentSetting));
    }


    private static void ShowHotkeyUnavailableHintOnce()
    {
        if (Helpers.X11KeyState.Available || !Helpers.OneShotHints.TryTake("hotkey-wayland"))
        {
            return;
        }
        // Wayland 原生会话里拿不到全局键盘状态（L-45），如实告诉用户怎么替代。
        Helpers.InAppToast.MainWindow?.Warning((string?)null, "当前会话下全局热键不可用（Wayland）。可在系统快捷键里把命令绑定到 `lunarward --show`。", 8000);
    }


    private async void MainView_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
#if DEBUG
        DevSwitchGameIfRequestedAsync();
#endif
        // Lunarward：M1 截图验证用，LUNARWARD_PAGE=setting 时直接跳转到指定页（正式版移除）
        string? debugPage = Starward.Features.Dev.DevSwitches.Page;
        // D108：只给 --subpage 时也要能直达（以前必须先 --page setting，否则参数被忽略）。
        if (string.IsNullOrEmpty(debugPage) && !string.IsNullOrEmpty(Starward.Features.Dev.DevSwitches.SubPage))
        {
            NavigateTo(typeof(SettingPage));
        }
        if (!string.IsNullOrEmpty(debugPage))
        {
            Type? target = debugPage switch
            {
                "launcher" => typeof(GameLauncherPage),
                "gamesetting" => typeof(GameSettingPage),
                "screenshot" => typeof(ScreenshotPage),
                "gacha" => typeof(GachaLogPage),
                "record" => typeof(GameRecordPage),
                "selfquery" => typeof(SelfQueryPage),
                "beyond" => typeof(GenshinBeyondGachaPage),
                "runtimeenv" => typeof(Features.Setting.RuntimeEnvironmentSetting),
                "setting" => typeof(SettingPage),
                "login" => typeof(Features.GameRecord.LoginPage),
                "predownload" => typeof(GameLauncherPage),
                "installprogress" => typeof(GameLauncherPage),
                "notice" => typeof(Features.GameLauncher.GameLauncherPage),
                _ => null,
            };
#if DEBUG
            if (DevSwitches.Page == "installprogress")
            {
                // 调试：在启动器页上启动一次预下载并 10 秒后停止，用于验证进度条（--target 写到 /tmp）。
                _ = DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(3000);
                    try
                    {
                        var service = AppConfig.GetService<Features.GameInstall.GameInstallService>();
                        // 别写 /tmp：那里在不少机器上是 tmpfs，这种调试下载会直接把内存写满（L-63）。
                        string devTarget = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", "lw-ui-progress");
                        var task = await service.StartPredownloadAsync(CurrentGameId, devTarget, System.Threading.CancellationToken.None);
                        Serilog.Log.Information("dev installprogress started: {state}", task?.State);
                        await Task.Delay(45000);
                        if (task is not null)
                        {
                            await service.StopTaskAsync(task);
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "dev installprogress failed");
                    }
                });
            }
#endif
#if DEBUG
            if (DevSwitches.Page is "predownload" or "install")
            {
                // 调试：直接弹安装/预下载对话框（截图与人工验证用，正式版移除）。
                _ = DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(2500);
                    try
                    {
                        if (CurrentGameId is null)
                        {
                            Serilog.Log.Warning("[dev] predownload/install: no current game selected");
                            return;
                        }
                        string? installPath = Features.GameLauncher.GameLauncherService.GetGameInstallPath(CurrentGameId, out _)
                                              ?? System.IO.Path.Combine(AppConfig.UserDataFolder!, "Games", CurrentGameId.Id);
                        if (DevSwitches.Page == "predownload")
                        {
                            var dialog = new Features.GameInstall.PreDownloadDialog { CurrentGameId = CurrentGameId, InstallPath = installPath, XamlRoot = XamlRoot };
                            await dialog.ShowAsync();
                        }
                        else
                        {
                            var dialog = new Features.GameInstall.InstallGameDialog { CurrentGameId = CurrentGameId, InstallationPath = installPath, XamlRoot = XamlRoot };
                            await dialog.ShowAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Error(ex, "open install dialog failed");
                    }
                });
            }
#endif
            if (target is not null)
            {
                // 游戏相关页面需要 GameId 作为导航参数（PageBase.OnNavigatedTo 读取），否则内容为空
                Type debugTarget = target;
                void DebugNavigate()
                {
                    try
                    {
                        // 直接操作 Frame，绕过 NavigateTo 的「未在 SupportedPages 就回落启动器页」过滤
                        MainView_Frame.Navigate(debugTarget, CurrentGameId, new SuppressNavigationTransitionInfo());
                        if (debugTarget == typeof(GameLauncherPage))
                        {
                            MainView_NavigationView.SelectedItem = NavigationViewItem_Launcher;
                        }
                    }
                    catch (Exception)
                    {
                        // 调试直达失败时静默，正常导航仍由 GameSelector 负责
                    }
                }
                DebugNavigate();
                // GameSelector 恢复选中会再导航一次，延迟补一次保证停在该页
                DispatcherQueue.TryEnqueue(async () =>
                {
                    await Task.Delay(1500);
                    DebugNavigate();
                });
            }
        }
        // D86：识别到游戏的提示只在「这台机器第一次」出现（原来用进程级 static，每次启动都弹一遍）。
        if (!OperatingSystem.IsWindows())
        {
            var known = Features.GameInstall.GameInstallScanner.KnownGames;
            if (known.Count > 0 && Helpers.OneShotHints.TryTake("auto-scan"))
            {
                // 文案暂用中文直写；i18n 时再补 Lang 资源（12-shims-and-gaps 已登记）
                string names = string.Join("、", System.Linq.Enumerable.Select(known, b => b.ToGameName()));
                Helpers.InAppToast.MainWindow?.Success(null, $"已自动识别 {known.Count} 个已安装游戏：{names}", 8000);
            }
        }
        CheckSystemProxy();
        HotkeyManager.InitializeHotkey(this.XamlRoot.GetWindowHandle());
        ShowHotkeyUnavailableHintOnce();
        _ = CheckUpdateOrShowRecentUpdateContentAsync();
        LogUploadService.Start();
        if (AppConfig.EnableGamepadController)
        {
            await Task.Delay(1000);
            var queue = (Content as Microsoft.UI.Xaml.FrameworkElement)?.DispatcherQueue;
            _ = Task.Run(() => GamepadController.Initialize(queue));
        }
    }


    private async void CheckSystemProxy()
    {
        try
        {
            await Task.Delay(1500);
            Uri? proxy = HttpClient.DefaultProxy.GetProxy(new Uri("https://starward.scighost.com"));
            // D86：这条只提醒一次（原来每次启动都弹，配合系统代理的用户等于每次开机被念一遍）。
            if (proxy is not null && Helpers.OneShotHints.TryTake("system-proxy"))
            {
                InAppToast.MainWindow?.Information(Lang.MainView_CheckSystemProxy_SystemProxyIsEnabled, proxy.ToString(), 5000);
            }
        }
        catch { }
    }


    private void GameSwitcher_CurrentGameChanged(object? sender, PinnedGameIcon icon)
    {
        GameId gameId = icon.GameId;
        if (icon.GameId.GameBiz == GameBiz.bh3_global)
        {
            string? id = AppConfig.LastGameIdOfBH3Global;
            if (!string.IsNullOrWhiteSpace(id))
            {
                gameId = new GameId(id, GameBiz.bh3_global);
            }
        }
        CurrentGameId = gameId;
        var newConfig = LauncherConfigModel.FromId(icon.LauncherId);
        if (CurrentLauncherConfig != newConfig)
        {
            CurrentLauncherConfig = newConfig;
            InitializePassportUser();
        }
        UpdateNavigationView();
        // 页面是缓存的，不重建就不会自己换内容：广播一次，让当前页刷新（D62）。
        WeakReferenceMessenger.Default.Send(new CurrentGameChangedMessage(CurrentGameId));
    }


#if DEBUG
    private async void DevSwitchGameIfRequestedAsync()
    {
        try
        {
            string? biz = Environment.GetEnvironmentVariable("LUNARWARD_DEV_SWITCH_GAME");
            if (string.IsNullOrWhiteSpace(biz))
            {
                return;
            }
            await Task.Delay(6000);
            if (Starward.Core.GameBiz.TryParse(biz, out Starward.Core.GameBiz target))
            {
                // 0.19.0 的 GameSwitcher 没有「指定当前游戏」的公开 API，调试钩子退化为直接写配置。
                AppConfig.CurrentGameId = biz;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[dev] switch game failed");
        }
    }
#endif


    private void OnBH3GlobalGameServerChanged(object _, BH3GlobalGameServerChangedMessage message)
    {
        if (CurrentGameId?.GameBiz == GameBiz.bh3_global && CurrentGameId.Id != message.GameId)
        {
            CurrentGameId = new(message.GameId, GameBiz.bh3_global);
            NavigateTo(typeof(GameLauncherPage), CurrentGameId, new SuppressNavigationTransitionInfo());
        }
    }



    #region UI


    [RelayCommand]
    private void ShowLauncherConfigView()
    {
        LauncherConfigView.Visibility = Visibility.Visible;
        LauncherConfigView.Opacity = 1;
        LauncherConfigView.IsHitTestVisible = true;
        LauncherConfigView.ShowGameInfosIfNoGames();
    }



    private void HideLauncherConfigView()
    {
        LauncherConfigView.GoBack();
        LauncherConfigView.GoBack();
    }


    /// <summary>
    /// 更新窗口拖拽区域
    /// </summary>
    private void GameSwitcher_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDragableArea();
    }



    private void UpdateDragableArea()
    {
        try
        {
            double x = GameSwitcher.ActualWidth + 62;
            this.XamlRoot.SetWindowDragRectangles([new Rect(x, 0, 10000, 48)]);
        }
        catch { }
    }


    #endregion



    #region Navigation





    private void UpdateNavigationView()
    {
        CurrentGameFeatureConfig = GameFeatureConfig.FromGameId(CurrentGameId);

        NavigationViewItem_Launcher.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GameLauncherPage)).ToVisibility();
        NavigationViewItem_Screenshot.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(ScreenshotPage)).ToVisibility();
        NavigationViewItem_GachaLog.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GachaLogPage)).ToVisibility();
        NavigationViewItem_HoyolabToolbox.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GameRecordPage)).ToVisibility();
        NavigationViewItem_SelfQuery.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(SelfQueryPage)).ToVisibility();
        NavigationViewItem_GenshinBeyondGacha.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GenshinBeyondGachaPage)).ToVisibility();

        if (CurrentGameId?.GameBiz.IsChinaServer() ?? false)
        {
            ToolTipService.SetToolTip(NavigationViewItem_HoyolabToolbox, Lang.HyperionToolbox);
            TextBlock_HoyolabToolbox.Text = Lang.HyperionToolbox;
        }
        if (CurrentGameId?.GameBiz.IsGlobalServer() ?? false)
        {
            ToolTipService.SetToolTip(NavigationViewItem_HoyolabToolbox, Lang.HoYoLABToolbox);
            TextBlock_HoyolabToolbox.Text = Lang.HoYoLABToolbox;
        }

        if (CurrentGameId is null)
        {
            NavigateTo(typeof(BlankPage));
        }
        else if (MainView_Frame.SourcePageType?.Name is not nameof(SettingPage))
        {
            NavigateTo(MainView_Frame.SourcePageType);
        }
    }



    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        try
        {
            if (args.InvokedItemContainer?.IsSelected ?? false)
            {
                return;
            }
            if (args.IsSettingsInvoked)
            {
                NavigateTo(typeof(SettingPage));
            }
            else
            {
                if (args.InvokedItemContainer is NavigationViewItem item)
                {
                    var type = item.Tag switch
                    {
                        nameof(GameLauncherPage) => typeof(GameLauncherPage),
                        nameof(ScreenshotPage) => typeof(ScreenshotPage),
                        nameof(GachaLogPage) => typeof(GachaLogPage),
                        nameof(GameRecordPage) => typeof(GameRecordPage),
                        nameof(SelfQueryPage) => typeof(SelfQueryPage),
                        nameof(GenshinBeyondGachaPage) => typeof(GenshinBeyondGachaPage),
                        nameof(Features.Setting.RuntimeEnvironmentSetting) => typeof(Features.Setting.RuntimeEnvironmentSetting),
                        _ => null,
                    };
                    NavigateTo(type);
                }
            }
        }
        catch { }
    }



    private void NavigateTo(Type? page, object? param = null, NavigationTransitionInfo? infoOverride = null)
    {
        page ??= typeof(GameLauncherPage);
        if (page.Name is nameof(BlankPage) && CurrentGameId is null)
        {

        }
        else if (page.Name is not (nameof(SettingPage) or nameof(Features.Setting.RuntimeEnvironmentSetting))
                 && !CurrentGameFeatureConfig.SupportedPages.Contains(page.Name))
        {
            // 运行环境与设置页一样是全局页：不参与「当前游戏支持哪些页面」的白名单，否则会被静默换回启动器（L-152）
            page = typeof(GameLauncherPage);
        }
        if (page.Name is nameof(GameLauncherPage))
        {
            MainView_NavigationView.SelectedItem = NavigationViewItem_Launcher;
        }
        if (page.Name is nameof(Features.Setting.RuntimeEnvironmentSetting))
        {
            MainView_NavigationView.SelectedItem = NavigationViewItem_RuntimeEnv;
        }
        MainView_Frame.Navigate(page, param ?? CurrentGameId, infoOverride);
        if (page.Name is nameof(BlankPage) or nameof(GameLauncherPage))
        {
            Border_OverlayMask.Opacity = 0;
        }
        else
        {
            Border_OverlayMask.Opacity = 1;
        }
    }



    private void OnMainViewNavigateMessageReceived(object _, MainViewNavigateMessage message)
    {
        if (message.GameId is not null)
        {
            CurrentGameId = message.GameId;
            UpdateNavigationView();
        }
        NavigateTo(message.Page, message.GameId);
        HideLauncherConfigView();
    }



    private async void OnEnabledLauncherConfigChanged(object _, EnabledLauncherConfigChangedMessage message)
    {
        await Task.Delay(300);
        CurrentGameId = GameSwitcher.CurrentGameIcon?.GameId;
        UpdateNavigationView();
    }




    #endregion



    #region Update


    private DateTimeOffset _lastCheckUpdateTime;

    private DateTimeOffset _lastShowUpdateTime;

    private SemaphoreSlim _updateLock = new(1, 1);


    private async Task CheckUpdateOrShowRecentUpdateContentAsync()
    {
#if DEBUG || DONOT_CHECK_UPDATE
        return;
#endif
#pragma warning disable CS0162 // 检测到无法访问的代码
        if (!await _updateLock.WaitAsync(0))
        {
            return;
        }
        await Task.Delay(1000);
#pragma warning restore CS0162 // 检测到无法访问的代码
        try
        {
            if (_lastCheckUpdateTime == default && NuGetVersion.TryParse(AppConfig.AppVersion, out var appVersion))
            {
                _ = NuGetVersion.TryParse(AppConfig.LastAppVersion, out var lastVersion);
                if (appVersion != lastVersion)
                {
                    // Lunarward：Linux 没有「发布页更新内容」可展示（D5 走包管理器），
                    // 版本变化时只更新记录，不弹只有标题和空白字段的更新窗。
                    if (!OperatingSystem.IsWindows())
                    {
                        AppConfig.LastAppVersion = AppConfig.AppVersion;
                        _lastCheckUpdateTime = DateTimeOffset.Now - TimeSpan.FromMinutes(55);
                        return;
                    }
                    if (AppConfig.ShowUpdateContentAfterUpdateRestart)
                    {
                        new UpdateWindow().Activate();
                    }
                    else
                    {
                        AppConfig.LastAppVersion = AppConfig.AppVersion;
                    }
                    _lastCheckUpdateTime = DateTimeOffset.Now - TimeSpan.FromMinutes(55);
                    return;
                }
            }
            DateTimeOffset now = DateTimeOffset.Now;
            if (now - _lastCheckUpdateTime > TimeSpan.FromHours(1))
            {
                var release = await AppConfig.GetService<UpdateService>().CheckUpdateAsync(false);
                _lastCheckUpdateTime = now;
                if (release != null && now - _lastShowUpdateTime > TimeSpan.FromHours(6) && now.Date != _lastShowUpdateTime.Date)
                {
                    new UpdateWindow { NewVersion = release }.Activate();
                    _lastShowUpdateTime = now;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check update");
        }
        finally
        {
            _updateLock.Release();
        }
    }


    #endregion



    #region Passport


    [ObservableProperty]
    public partial PassportUserInfoModel? CurrentPassportUser { get; set; }

    public ObservableCollection<PassportUserInfoModel>? PassportUsers { get; set => SetProperty(ref field, value); }

    public bool AuthLoginGameByPassportAccount
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.AuthLoginGameByPassportAccount = value;
            }
        }
    } = AppConfig.AuthLoginGameByPassportAccount;


    private async void InitializePassportUser()
    {
        try
        {
            await Task.Delay(1500);
            // 上游 MainView.xaml.cs:426 是同步版本，这里延后 1.5 s 放后台避免拖慢启动；
            // 判定必须用 CurrentLauncherConfig.CanLogin——之前误把 D86 的系统代理提示守卫套在了这里，
            // 结果「没配系统代理」的用户永远拿不到 Hyp 账号列表。
            CurrentPassportUser = null;
            PassportUsers = null;
            UpdateHypFlyoutLayout();
            if (CurrentLauncherConfig?.CanLogin ?? false)
            {
                _ = _windowsDeviceFpService.InitializeDeviceFpAsync(CurrentLauncherConfig.Region);
                PassportUsers = new(PassportService.GetPassportUserInfos(CurrentLauncherConfig.Region));
                if (PassportUsers.Count > 0)
                {
                    CurrentPassportUser = PassportUsers.FirstOrDefault(x => x.Aid == AppConfig.GetLastPassportUserAid(CurrentLauncherConfig.Region));
                    CurrentPassportUser ??= PassportUsers.FirstOrDefault();
                    UpdateHypFlyoutLayout();
                    _ = VerifyPassportSessionAsync(PassportUsers);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize passport user");
        }
    }


    private async Task VerifyPassportSessionAsync(IEnumerable<PassportUserInfoModel> list)
    {
        try
        {
            await Task.WhenAll(list.Select(x => _passportService.VerifySessionAsync(x)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Verfiy passport session");
        }
    }


    partial void OnCurrentPassportUserChanged(PassportUserInfoModel? value)
    {
        _hoYoPlayService.PassportUserInfo = value;
        if (value is not null)
        {
            AppConfig.SetLastPassportUserAid(value.Region, value.Aid);
        }
    }


    [RelayCommand]
    public async Task<PassportUserInfoModel?> HypLoginAsync()
    {
        try
        {
            if (CurrentLauncherConfig?.CanLogin ?? false)
            {
                var model = await new HypLoginWindow
                {
                    ParentWindowHandle = MainWindow.Current.WindowHandle,
                    ServerRegion = CurrentLauncherConfig.Region
                }.LoginAsync();
                if (model is not null)
                {
                    PassportUsers ??= new();
                    if (PassportUsers.FirstOrDefault(x => x.Aid == model.Aid) is PassportUserInfoModel oldModel)
                    {
                        int index = PassportUsers.IndexOf(oldModel);
                        PassportUsers.Remove(oldModel);
                        PassportUsers.Insert(index, model);
                    }
                    else
                    {
                        PassportUsers.Add(model);
                    }
                    CurrentPassportUser = model;
                    UpdateHypFlyoutLayout();
                    PassportService.SavePassportUserInfo(model);
                    MainWindow.Current.Show();
                    await AppConfig.GetService<GameRecordRoleService>().UpdateGameRolesByPassportAsync(model);
                    return model;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hyp login");
            InAppToast.MainWindow?.Error(ex, Lang.MainView_LoginFailed);
        }
        return null;
    }


    private void Button_HypLogout_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: PassportUserInfoModel model })
            {
                PassportService.DeletePassportUserInfo(model);
                _ = _passportService.LogoutAsync(model);
                if (AppConfig.GetLastPassportUserAid(model.Region) == model.Aid)
                {
                    AppConfig.SetLastPassportUserAid(model.Region, 0);
                }
                if (CurrentPassportUser == model)
                {
                    PassportUsers?.Remove(model);
                    CurrentPassportUser = PassportUsers?.FirstOrDefault();
                }
                else
                {
                    PassportUsers?.Remove(model);
                }
                UpdateHypFlyoutLayout();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hyp logout");
        }
    }


    [RelayCommand]
    private void HypLogout()
    {
        try
        {
            var model = CurrentPassportUser;
            if (model is null)
            {
                return;
            }
            PassportService.DeletePassportUserInfo(model);
            _ = _passportService.LogoutAsync(model);
            if (AppConfig.GetLastPassportUserAid(model.Region) == model.Aid)
            {
                AppConfig.SetLastPassportUserAid(model.Region, 0);
            }
            PassportUsers?.Remove(model);
            CurrentPassportUser = PassportUsers?.FirstOrDefault();
            UpdateHypFlyoutLayout();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hyp logout");
        }
    }


    private void UpdateHypFlyoutLayout()
    {
        if (PassportUsers?.Count > 1)
        {
            ColumnDefinition_HypLoginColumn0.MaxWidth = 200;
            ListView_HypAccunt.Visibility = Visibility.Visible;
            Button_HypLogout.Visibility = Visibility.Collapsed;
        }
        else if (PassportUsers?.Count > 0)
        {
            ColumnDefinition_HypLoginColumn0.MaxWidth = double.PositiveInfinity;
            ListView_HypAccunt.Visibility = Visibility.Collapsed;
            Button_HypLogout.Visibility = Visibility.Visible;
        }
        else
        {
            ColumnDefinition_HypLoginColumn0.MaxWidth = double.PositiveInfinity;
            ListView_HypAccunt.Visibility = Visibility.Collapsed;
            Button_HypLogout.Visibility = Visibility.Collapsed;
        }
    }


    #endregion


}



file static class BoolToVisibilityExtension
{

    public static Visibility ToVisibility(this bool value)
    {
        return value ? Visibility.Visible : Visibility.Collapsed;
    }


}
