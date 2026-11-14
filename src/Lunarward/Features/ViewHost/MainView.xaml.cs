using CommunityToolkit.Mvvm.ComponentModel;
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
using Starward.Features.GameSetting;
using Starward.Features.RPC;
using Starward.Features.Screenshot;
using Starward.Features.SelfQuery;
using Starward.Features.Setting;
using Starward.Features.Update;
using Starward.Helpers;
using System;
using System.Net.Http;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;


namespace Starward.Features.ViewHost;

[INotifyPropertyChanged]
public sealed partial class MainView : UserControl
{


    private readonly ILogger<MainView> _logger = AppConfig.GetLogger<MainView>();


    public GameId? CurrentGameId { get; private set => SetProperty(ref field, value); }


    private GameFeatureConfig CurrentGameFeatureConfig { get; set; }



    public MainView()
    {
        this.InitializeComponent();
        InitializeMainView();
        // Lunarward：Uno 的 TeachingTip 不会自动消失，切到非启动器页时手动收起引导浮层
        MainView_Frame.Navigated += (_, _) =>
        {
            if (MainView_Frame.SourcePageType != typeof(Features.GameLauncher.GameLauncherPage))
            {
                GameSelector?.HideSelectGameTip();
            }
        };
    }



    private void InitializeMainView()
    {
        this.Loaded += MainView_Loaded;
        GameId? gameId = GameSelector.CurrentGameId;
        if (gameId?.GameBiz == GameBiz.bh3_global)
        {
            string? id = AppConfig.LastGameIdOfBH3Global;
            if (!string.IsNullOrWhiteSpace(id))
            {
                gameId.Id = id;
            }
        }
        CurrentGameId = gameId;
        CurrentGameFeatureConfig = GameFeatureConfig.FromGameId(CurrentGameId);
        UpdateNavigationView();
        WeakReferenceMessenger.Default.Register<MainViewNavigateMessage>(this, OnMainViewNavigateMessageReceived);
        WeakReferenceMessenger.Default.Register<BH3GlobalGameServerChangedMessage>(this, OnBH3GlobalGameServerChanged);
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, (_, _) => _ = CheckUpdateOrShowRecentUpdateContentAsync());
    }




    private static bool _scanTipShown;


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
            if (GameSelector is not null && GameSelector.FullBackgroundVisible)
            {
                GameSelector.CloseLayer();
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


    private static bool _hotkeyHintShown;


    private static void ShowHotkeyUnavailableHintOnce()
    {
        if (_hotkeyHintShown || Helpers.X11KeyState.Available)
        {
            return;
        }
        _hotkeyHintShown = true;
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
                        var service = AppConfig.GetService<Starward.RPC.GameInstall.GameInstallService>();
                        // 别写 /tmp：那里在不少机器上是 tmpfs，这种调试下载会直接把内存写满（L-63）。
                        string devTarget = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "games", "lw-ui-progress");
                        var task = await service.StartPredownloadAsync(CurrentGameId, devTarget, Starward.Core.AudioLanguage.Chinese);
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
                        Starward.Core.GameBiz dbgBiz = AppConfig.CurrentGameBiz;
                        if (string.IsNullOrEmpty(dbgBiz.Value) || CurrentGameId is null)
                        {
                            Serilog.Log.Warning("[dev] predownload/install: no current game selected");
                            return;
                        }
                        string? installPath = AppConfig.GetGameInstallPath(dbgBiz)
                                              ?? System.IO.Path.Combine(AppConfig.UserDataFolder!, "Games", dbgBiz.Value);
                        if (DevSwitches.Page == "predownload")
                        {
                            var dialog = new Features.GameInstall.PreDownloadDialog { CurrentGameId = CurrentGameId, InstallationPath = installPath, XamlRoot = XamlRoot };
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
        // Lunarward：首次启动扫描到游戏时给一次反馈（上游等价行为是「自动查找」后的提示）
        if (!_scanTipShown && !OperatingSystem.IsWindows())
        {
            _scanTipShown = true;
            var known = Features.GameInstall.GameInstallScanner.KnownGames;
            if (known.Count > 0)
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
        AppConfig.GetService<RpcService>().TrySetEnviromentAsync();
        LogUploadService.Start();
        if (AppConfig.EnableGamepadController)
        {
            await Task.Delay(1000);
            var queue = (Content as Microsoft.UI.Xaml.FrameworkElement)?.DispatcherQueue;
            _ = Task.Run(() => GamepadController.Initialize(queue));
        }
    }




    private void GameSelector_CurrentGameChanged(object? sender, (GameId, bool DoubleTapped) e)
    {
        if (e.Item1.GameBiz == GameBiz.bh3_global)
        {
            // 崩坏3国际服区服
            string? id = AppConfig.LastGameIdOfBH3Global;
            if (!string.IsNullOrWhiteSpace(id))
            {
                e.Item1.Id = id;
            }
        }
        CurrentGameId = e.Item1;
        CurrentGameFeatureConfig = GameFeatureConfig.FromGameId(CurrentGameId);
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
                GameSelector.DevSelectGame(target);
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
        if (CurrentGameId?.GameBiz == GameBiz.bh3_global)
        {
            CurrentGameId.Id = message.GameId;
            OnPropertyChanged(nameof(CurrentGameId));
            NavigateTo(typeof(GameLauncherPage), CurrentGameId, new SuppressNavigationTransitionInfo());
        }
    }




    #region Navigation





    private void UpdateNavigationView()
    {
        NavigationViewItem_Launcher.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GameLauncherPage)).ToVisibility();
        NavigationViewItem_GameSetting.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GameSettingPage)).ToVisibility();
        NavigationViewItem_Screenshot.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(ScreenshotPage)).ToVisibility();
        NavigationViewItem_GachaLog.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GachaLogPage)).ToVisibility();
        NavigationViewItem_HoyolabToolbox.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GameRecordPage)).ToVisibility();
        NavigationViewItem_SelfQuery.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(SelfQueryPage)).ToVisibility();
        NavigationViewItem_GenshinBeyondGacha.Visibility = CurrentGameFeatureConfig.SupportedPages.Contains(nameof(GenshinBeyondGachaPage)).ToVisibility();

        // 抽卡记录名称
        string gachalogText = CurrentGameId?.GameBiz.Game switch
        {
            GameBiz.hk4e => Lang.GachaLogService_WishRecords,
            GameBiz.hkrpg => Lang.GachaLogService_WarpRecords,
            GameBiz.nap => Lang.GachaLogService_SignalSearchRecords,
            _ => "",
        };

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
                        nameof(GameSettingPage) => typeof(GameSettingPage),
                        nameof(ScreenshotPage) => typeof(ScreenshotPage),
                        nameof(GachaLogPage) => typeof(GachaLogPage),
                        nameof(GameRecordPage) => typeof(GameRecordPage),
                        nameof(SelfQueryPage) => typeof(SelfQueryPage),
                        nameof(GenshinBeyondGachaPage) => typeof(GenshinBeyondGachaPage),
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
        else if (page.Name is not nameof(SettingPage) && !CurrentGameFeatureConfig.SupportedPages.Contains(page.Name))
        {
            page = typeof(GameLauncherPage);
        }
        if (page.Name is nameof(GameLauncherPage))
        {
            MainView_NavigationView.SelectedItem = NavigationViewItem_Launcher;
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
        NavigateTo(message.Page);
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




    private async void CheckSystemProxy()
    {
        try
        {
            await Task.Delay(1500);
            Uri? proxy = HttpClient.DefaultProxy.GetProxy(new Uri("https://starward.scighost.com"));
            if (proxy is not null)
            {
                InAppToast.MainWindow?.Information(Lang.MainView_CheckSystemProxy_SystemProxyIsEnabled, proxy.ToString(), 5000);
            }
        }
        catch { }
    }


}



file static class BoolToVisibilityExtension
{

    public static Visibility ToVisibility(this bool value)
    {
        return value ? Visibility.Visible : Visibility.Collapsed;
    }


}
