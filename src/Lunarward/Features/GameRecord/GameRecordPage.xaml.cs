using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core;
using Starward.Core.GameRecord;
using Starward.Features.GameRecord.Genshin;
using Starward.Features.GameRecord.StarRail;
using Starward.Features.GameRecord.ZZZ;
using Starward.Features.Passport;
using Starward.Features.ViewHost;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Starward.Features.GameLauncher;
using Starward.Features;
using System.Net.Http;


namespace Starward.Features.GameRecord;

public sealed partial class GameRecordPage : PageBase
{

    private readonly ILogger<GameRecordPage> _logger = AppConfig.GetLogger<GameRecordPage>();

    private readonly GameRecordRoleService _gameRecordService = AppConfig.GetService<GameRecordRoleService>();


    public GameRecordPage()
    {
        this.InitializeComponent();
    }



    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        InitializeNavigationViewItemVisibility();
    }


    protected override async void OnLoaded()
    {
        if (AppConfig.HoyolabToolboxPaneOpen)
        {
            OpenNavigationViewPane();
        }
        else
        {
            CloseNavigationViewPane();
        }
        WeakReferenceMessenger.Default.Register<HypThirdPartyLoginMessage>(this, (r, m) =>
        {
            DispatcherQueue.TryEnqueue(() => _ = InitializeGameRoleAsync());
        });
        await Task.Delay(16);
        NavigateTo(typeof(BlankPage));
        await InitializeGameRoleAsync();
    }



    protected override void OnGameChanged()
    {
        // B51：缓存实例切游戏时不会重新 OnLoaded，这里重新载入角色与设备信息。
        DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                // 切换游戏后必须先把上一款游戏的页签与内容清掉：
                // InitializeNavigationViewItemVisibility 只负责「本游戏要显示哪些」，不重置旧的可见项。
                InitializeNavigationViewItemVisibility();
                // 上游 0.19.0 的 GameRecordRoleService 不再有 IsHoyolab / Language（region 由 passport 带，
                // 语言由各分游戏 service 自己按当前 culture 取），这里不再设置。
                CurrentRole = null;
                GameRoleList = null!;
                NavigateTo(typeof(BlankPage));
                if (await CheckAgreementAsync())
                {
                    LoadGameRoles();
                    await UpdateDeviceInfoAsync();
                    await RefreshGameRoleHeadIconSilentlyAsync();
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Reload game record page after game changed");
            }
        });
    }


    /// <summary>
    /// LUNARWARD-DEVIATION: 上游 0.19.0 起战绩工具不再弹免责声明（接口侧不再要求），但移植侧
    /// 保留了 AppConfig.AcceptHoyolabToolboxAgreement 与这段 5 秒倒计时对话框（D61 定下的交互），
    /// 这里补回被上游同步覆盖掉的实现。
    /// </summary>
    private async Task<bool> CheckAgreementAsync()
    {
        try
        {
            if (!AppConfig.AcceptHoyolabToolboxAgreement)
            {
                var dialog = new ContentDialog
                {
                    Title = Lang.Common_Disclaimer,
                    Content = Lang.HoyolabToolboxPage_DisclaimerContent,
                    PrimaryButtonText = Lang.Common_Accept + " (5s)",
                    SecondaryButtonText = Lang.Common_Reject,
                    IsPrimaryButtonEnabled = false,
                    DefaultButton = ContentDialogButton.Secondary,
                    XamlRoot = this.XamlRoot,
                };
                var resultTask = dialog.ShowAsync();
                bool cancel = false;
                for (int i = 0; i < 5; i++)
                {
                    for (int j = 0; j < 10; j++)
                    {
                        await Task.Delay(100);
                        if (resultTask.Status is Windows.Foundation.AsyncStatus.Completed)
                        {
                            cancel = true;
                            break;
                        }
                    }
                    if (cancel)
                    {
                        break;
                    }
                    dialog.PrimaryButtonText = Lang.Common_Accept + $" ({4 - i}s)";
                }
                dialog.PrimaryButtonText = Lang.Common_Accept;
                dialog.IsPrimaryButtonEnabled = true;
                var result = await resultTask;
                if (result is ContentDialogResult.Primary)
                {
                    AppConfig.AcceptHoyolabToolboxAgreement = true;
                }
                else
                {
                    WeakReferenceMessenger.Default.Send(new MainViewNavigateMessage(typeof(GameLauncherPage)));
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check agreement.");
            return false;
        }
    }



    protected override void OnUnloaded()
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        // 注意：不要在 Unloaded 里 -= XAML 的 Tapped。页面是缓存实例，Unloaded 后还会再挂载，
        // XAML 只连接一次，退订了就再也接不回来（旧代码因此让「战绩」/「更新设备信息」在第二次进入后失效）。
        CurrentRole = null;
        GameRoleList = null!;
        _battleChronicleWindow = null;
    }



    #region Navigate


    public Thickness NavigationViewItemContentMargin { get; set => SetProperty(ref field, value); } = new Thickness(-2, 0, 0, 0);


    // Close pane
    private void Grid_Avatar_1_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        CloseNavigationViewPane();
    }


    // Open pane
    private void Border_Avatar_2_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        OpenNavigationViewPane();
    }


    private void OpenNavigationViewPane()
    {
        NavigationViewItemContentMargin = new Thickness(-2, 0, 0, 0);
        NavigationView_Toolbox.IsPaneOpen = true;
        Grid_Avatar_1.Visibility = Visibility.Visible;
        Border_Avatar_2.Visibility = Visibility.Collapsed;
        AppConfig.HoyolabToolboxPaneOpen = true;
    }


    private void CloseNavigationViewPane()
    {
        NavigationViewItemContentMargin = new Thickness(2, 0, 0, 0);
        NavigationView_Toolbox.IsPaneOpen = false;
        Grid_Avatar_1.Visibility = Visibility.Collapsed;
        Border_Avatar_2.Visibility = Visibility.Visible;
        AppConfig.HoyolabToolboxPaneOpen = false;
    }


    private static readonly string[] AllToolboxItemNames =
    [
        nameof(NavigationViewItem_BattleChronicle),
        nameof(NavigationViewItem_TravelersDiary),
        nameof(NavigationViewItem_SpiralAbyss),
        nameof(NavigationViewItem_ImaginariumTheater),
        nameof(NavigationViewItem_StygianOnslaught),
        nameof(NavigationViewItem_TrailblazeMonthlyCalendar),
        nameof(NavigationViewItem_SimulatedUniverse),
        nameof(NavigationViewItem_ForgottenHall),
        nameof(NavigationViewItem_PureFiction),
        nameof(NavigationViewItem_ApocalypticShadow),
        nameof(NavigationViewItem_ChallengePeak),
        nameof(NavigationViewItem_InterKnotMonthlyReport),
        nameof(NavigationViewItem_ShiyuDefense),
        nameof(NavigationViewItem_DeadlyAssault),
    ];


    private void InitializeNavigationViewItemVisibility()
    {
        // 先全部收起，再按当前游戏逐项打开：不预置 Collapsed 会让上一款游戏的页签残留（用户报的 tab 遗留）。
        foreach (string name in AllToolboxItemNames)
        {
            if (FindName(name) is NavigationViewItem item)
            {
                item.Visibility = Visibility.Collapsed;
            }
        }
        NavigationViewItem_UpdateDeviceInfo.Visibility = CurrentGameBiz.IsGlobalServer() ? Visibility.Collapsed : Visibility.Visible;

        if (CurrentGameBiz.Game is GameBiz.bh3)
        {
            NavigationViewItem_BattleChronicle.Visibility = Visibility.Visible;
            // 崩坏3战绩图片
            Image_BattleChronicle.Source = new BitmapImage(new("ms-appx:///Assets/Image/4d94fbd5ff63c8b4344876ce21e04d10_2581928258151711511.png"));
        }
        else if (CurrentGameBiz.Game is GameBiz.hk4e)
        {
            NavigationViewItem_BattleChronicle.Visibility = Visibility.Visible;
            NavigationViewItem_TravelersDiary.Visibility = Visibility.Visible;
            NavigationViewItem_SpiralAbyss.Visibility = Visibility.Visible;
            NavigationViewItem_ImaginariumTheater.Visibility = Visibility.Visible;
            NavigationViewItem_StygianOnslaught.Visibility = Visibility.Visible;
            // 原神战绩图片
            Image_BattleChronicle.Source = new BitmapImage(new("ms-appx:///Assets/Image/ced4deac2162690105bbc8baad2b51a3_4109616186965788891.png"));
        }
        else if (CurrentGameBiz.Game is GameBiz.hkrpg)
        {
            NavigationViewItem_BattleChronicle.Visibility = Visibility.Visible;
            NavigationViewItem_TrailblazeMonthlyCalendar.Visibility = Visibility.Visible;
            NavigationViewItem_SimulatedUniverse.Visibility = Visibility.Visible;
            NavigationViewItem_ForgottenHall.Visibility = Visibility.Visible;
            NavigationViewItem_PureFiction.Visibility = Visibility.Visible;
            NavigationViewItem_ApocalypticShadow.Visibility = Visibility.Visible;
            NavigationViewItem_ChallengePeak.Visibility = Visibility.Visible;
            // 铁道战绩图片
            Image_BattleChronicle.Source = new BitmapImage(new("ms-appx:///Assets/Image/ade9545750299456a3fcbc8c3b63521d_2941971308029698042.png"));
        }
        else if (CurrentGameBiz.Game is GameBiz.nap)
        {
            NavigationViewItem_BattleChronicle.Visibility = Visibility.Visible;
            NavigationViewItem_InterKnotMonthlyReport.Visibility = Visibility.Visible;
            NavigationViewItem_ShiyuDefense.Visibility = Visibility.Visible;
            NavigationViewItem_DeadlyAssault.Visibility = Visibility.Visible;
            // 绝区零战绩图片
            Image_BattleChronicle.Source = new BitmapImage(new("ms-appx:///Assets/Image/bc8f0b7384b306c80f2a1fcca9f3d14b_8590605504999484795.png"));
        }
    }


    private void NavigateTo(Type? page, object? parameter = null, bool force_navigate = false)
    {
        if (page is null)
        {
            return;
        }
        if (!force_navigate && frame.SourcePageType == page)
        {
            return;
        }
        frame.Navigate(page, parameter ?? CurrentRole);
    }


    private void NavigationView_Toolbox_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        try
        {
            var item = args.InvokedItemContainer as NavigationViewItem;
            if (item != null)
            {
                if (args.InvokedItemContainer?.IsSelected ?? false)
                {
                    return;
                }
                var type = item.Tag switch
                {
                    nameof(TravelersDiaryPage) => typeof(TravelersDiaryPage),
                    nameof(SpiralAbyssPage) => typeof(SpiralAbyssPage),
                    nameof(ImaginariumTheaterPage) => typeof(ImaginariumTheaterPage),
                    nameof(StygianOnslaughtPage) => typeof(StygianOnslaughtPage),
                    nameof(TrailblazeCalendarPage) => typeof(TrailblazeCalendarPage),
                    nameof(SimulatedUniversePage) => typeof(SimulatedUniversePage),
                    nameof(ForgottenHallPage) => typeof(ForgottenHallPage),
                    nameof(PureFictionPage) => typeof(PureFictionPage),
                    nameof(ApocalypticShadowPage) => typeof(ApocalypticShadowPage),
                    nameof(ChallengePeakPage) => typeof(ChallengePeakPage),
                    nameof(InterKnotMonthlyReportPage) => typeof(InterKnotMonthlyReportPage),
                    nameof(ShiyuDefensePage) => typeof(ShiyuDefensePage),
                    nameof(DeadlyAssaultPage) => typeof(DeadlyAssaultPage),
                    _ => null,
                };
                NavigateTo(type);
            }
        }
        catch { }
    }


    private void NavigationViewItem_BattleChronicle_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        ShowBattleChronicleWindow();
    }


    private BattleChronicleWindow? _battleChronicleWindow;


    private void ShowBattleChronicleWindow()
    {
        // 窗口关闭后 AppWindow is null
        if (_battleChronicleWindow?.AppWindow is null)
        {
            _battleChronicleWindow = new BattleChronicleWindow
            {
                CurrentRole = CurrentRole,
            };
        }
        else if (_battleChronicleWindow.CurrentRole != CurrentRole)
        {
            _battleChronicleWindow.CurrentRole = CurrentRole;
        }
        _battleChronicleWindow.Activate();
    }


    #endregion




    #region Game Role Info



    public GameRecordRole? CurrentRole
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(AvatarUrl));
                OnPropertyChanged(nameof(ShowRecordEmptyState));
            }
        }
    }


    public List<GameRecordRole> GameRoleList
    {
        get;
        set
        {
            SetProperty(ref field, value);
            OnPropertyChanged(nameof(ShowRecordEmptyState));
        }
    }


    /// <summary>没有角色（未登录 / 未添加角色）时显示空态，避免内容区一片空白（D61）。</summary>
    public bool ShowRecordEmptyState => CurrentRole is null || GameRoleList is null || GameRoleList.Count == 0;


    public string AvatarUrl => !string.IsNullOrWhiteSpace(CurrentRole?.HeadIcon) ? CurrentRole.HeadIcon : $"ms-appx:///Assets/Image/icon_{(CurrentGameBiz.IsGlobalServer() ? "hoyolab" : "hyperion")}.png";


    private void LoadGameRoles(GameRecordRole? role = null)
    {
        try
        {
            if (role != null)
            {
                AppConfig.SetLastSelectGameRecordRole(CurrentGameBiz, role.Uid);
            }
            role ??= GameRecordRoleService.GetLastSelectGameRecordRoleOrTheFirstOne(CurrentGameBiz);
            var list = GameRecordRoleService.GetGameRoles(CurrentGameBiz);
            CurrentRole = role ?? list.FirstOrDefault();
            GameRoleList = list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load game roles ({gameBiz}).", CurrentGameBiz);
        }
    }


    /// <summary>
    /// 进入页面时初始化游戏角色信息
    /// </summary>
    /// <returns></returns>
    private async Task InitializeGameRoleAsync()
    {
        try
        {
            LoadGameRoles();
            if (CurrentRole is null)
            {
                await AddGameRolesByPassportAsync();
            }
            else
            {
                await UpdateSelectedGameRoleAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize game role ({gameBiz}).", CurrentGameBiz);
        }
    }


    /// <summary>
    /// 没有游戏角色时，通过当前通行证账号获取游戏角色
    /// </summary>
    /// <returns></returns>
    private async Task AddGameRolesByPassportAsync()
    {
        var passport = MainView.Current.CurrentPassportUser;
        if (passport is null)
        {
            return;
        }
        try
        {
            await _gameRecordService.UpdateGameRolesByPassportAsync(passport);
            LoadGameRoles();
            if (CurrentRole is not null)
            {
                await UpdateSelectedGameRoleAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get game roles by passport ({aid}).", passport.Aid);
        }
    }


    /// <summary>
    /// 更新当前选中的游戏角色信息，失败时跳过
    /// </summary>
    /// <returns></returns>
    private async Task UpdateSelectedGameRoleAsync()
    {
        GameRecordRole? role = CurrentRole;
        if (role is null)
        {
            return;
        }
        try
        {
            await _gameRecordService.UpdateGameRoleAsync(role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update game role ({gameBiz}, {uid}).", role.GameBiz, role.Uid);
        }
        LoadGameRoles();
    }


    /// <summary>
    /// 空态「登录」入口（移植侧 XAML 独有，上游 0.19.0 不会走到没有角色态的页面）。
    /// 对齐上游：登录统一走 MainView.HypLoginAsync（HypLoginWindow 内嵌 WebKitGTK，D29/D32），
    /// 不再跳移植侧自建的 LoginPage —— 上游 0.19.0 已经把这个页面删掉了（D138）。
    /// </summary>
    [RelayCommand]
    private Task WebLoginAsync() => LoginAsync();


    [RelayCommand]
    private async Task UpdateGameRolesCookieAsync()
    {
        try
        {
            if (GameRoleList is null)
            {
                return;
            }
            var aids = GameRoleList.Select(x => x.Aid).Distinct().ToList();
            await Parallel.ForEachAsync(aids, UnloadCancellationToken, async (aid, token) =>
            {
                try
                {
                    var passport = PassportService.GetPassportUserInfo(aid);
                    if (passport is null || passport.ErrorType is not PassportErrorType.Success)
                    {
                        return;
                    }
                    await _gameRecordService.UpdateGameRolesByPassportAsync(passport, token);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "UpdateGameRolesCookieAsync");
                }
            });
            LoadGameRoles();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateGameRolesCookieAsync");
        }
    }


    [RelayCommand]
    private async Task LoginAsync()
    {
        try
        {
            var model = await MainView.Current.HypLoginAsync();
            if (model is not null)
            {
                var role = GameRecordRoleService.GetGameRoles(CurrentGameBiz).FirstOrDefault(x => x.Aid == model.Aid);
                LoadGameRoles(role);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Login failed: {ex}");
        }
    }


    private void ListView_GameRoles_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is GameRecordRole role)
        {
            CurrentRole = role;
            AppConfig.SetLastSelectGameRecordRole(CurrentGameBiz, role.Uid);
        }
    }


    private void MenuFlyoutItem_DeleteGameRole_Click(object sender, RoutedEventArgs e)
    {
        GameRecordRole? gameRole = null;
        try
        {
            if (sender is FrameworkElement { Tag: GameRecordRole role })
            {
                gameRole = role;
                GameRecordRoleService.DeleteGameRole(role);
                LoadGameRoles();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete game role ({gameBiz}, {uid}).", gameRole?.GameBiz, gameRole?.Uid);
        }
    }


    private async void NavigationViewItem_UpdateDeviceInfo_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        await UpdateDeviceInfoAsync(true);
    }


    // LUNARWARD-DEVIATION: 上游同步把这个入口连同方法一起覆盖成了空壳（只清缓存、不再重取指纹），
    // 移植侧照旧在点击后重新申请一次并给成功提示，所以这里补回 UpdateDeviceInfoAsync。
    private async Task UpdateDeviceInfoAsync(bool forceUpdate = false)
    {
        try
        {
            ServerRegion region = CurrentGameBiz.IsGlobalServer() ? ServerRegion.HoYoverse : ServerRegion.miHoYo;
            // 0.19.0 起设备指纹由 WindowsDeviceFpService 持有（AppConfig.HyperionDeviceId/DeviceFp 已 [Obsolete]，
            // D128）；它的 Linux 实现自己维护 MIHOYOSDK_DEVICE_FP，不读注册表/DXGI/SMBIOS。
            WindowsDeviceFpService deviceFpService = AppConfig.GetService<WindowsDeviceFpService>();
            if (forceUpdate)
            {
                // 先让 GameRecordServiceBase 丢掉缓存，否则拿回来的还是旧指纹
                GameRecordServiceBase.ResetDeviceInfo();
                await deviceFpService.InitializeDeviceFpAsync(region);
            }
            _logger.LogDebug("Device fp: {deviceFp}", deviceFpService.GetDeviceFp());
            // 重新生成含新指纹的 AndroidDeviceInfo（写回 AppConfig.AndroidDeviceInfo），各分游戏客户端随后取用
            await _gameRecordService.GetDeviceInfoAsync(region);
            if (forceUpdate)
            {
                InAppToast.MainWindow?.Success(Lang.HoyolabToolboxPage_TheDeviceFingerprintIsAlreadyUpdated);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update device info");
            if (forceUpdate)
            {
                InAppToast.MainWindow?.Error(ex);
            }
        }
    }



    /// <summary>切换游戏后台刷新一次当前角色的头像（失败不提示）。上游同步时被覆盖删除，此处补回。</summary>
    private async Task RefreshGameRoleHeadIconSilentlyAsync()
    {
        try
        {
            if (CurrentRole is not null)
            {
                await _gameRecordService.UpdateGameRoleAsync(CurrentRole);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update game role head icon silently ({gameBiz}, {uid}).", CurrentRole?.GameBiz, CurrentRole?.Uid);
        }
    }


    #endregion


}
