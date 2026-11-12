using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Starward.Core.HoYoPlay;
using Starward.Features.HoYoPlay;
using Starward.Features.ViewHost;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.System;


namespace Starward.Features.GameLauncher;

[INotifyPropertyChanged]
public sealed partial class GameBannerAndPost : UserControl
{


    private Microsoft.UI.Dispatching.DispatcherQueueTimer _bannerTimer;


    /// <summary>当前已加载内容属于哪个游戏；页面被缓存后据此判断要不要重新拉取。</summary>
    private GameId? _contentGameId;


    private readonly ILogger<GameBannerAndPost> _logger = AppConfig.GetLogger<GameBannerAndPost>();


    private readonly HoYoPlayService _hoYoPlayService = AppConfig.GetService<HoYoPlayService>();


    private readonly GameNoticeService _gameNoticeService = AppConfig.GetService<GameNoticeService>();


    private GameId _currentGameId;

    /// <summary>当前游戏。页面缓存后不会重建，切换游戏时必须靠这里自己换内容（D62）。</summary>
    public GameId CurrentGameId
    {
        get => _currentGameId;
        set
        {
            if (_currentGameId == value)
            {
                return;
            }
            _currentGameId = value;
            if (IsLoaded)
            {
                _ = ReloadForCurrentGameAsync();
            }
        }
    }


    private async Task ReloadForCurrentGameAsync()
    {
        try
        {
            _contentGameId = CurrentGameId;
            await UpdateGameContentAsync();
            await UpdateGameNoticeAlertAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[banner] reload for current game failed");
        }
    }



    public GameBannerAndPost()
    {
        this.InitializeComponent();
        this.Loaded += GameBannerAndPost_Loaded;
        this.Unloaded += GameBannerAndPost_Unloaded;
        _bannerTimer = DispatcherQueue.CreateTimer();
        _bannerTimer.Interval = TimeSpan.FromSeconds(5);
        _bannerTimer.IsRepeating = true;
        _bannerTimer.Tick += _bannerTimer_Tick;
    }






    private async void GameBannerAndPost_Loaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, OnMainWindowStateChanged);
        WeakReferenceMessenger.Default.Register<GameAnnouncementSettingChangedMessage>(this, OnGameAnnouncementSettingChanged);
        if (_contentGameId != CurrentGameId || Banners is null || PostGroups is null)
        {
            _contentGameId = CurrentGameId;
            await UpdateGameContentAsync();
        }
        await UpdateGameNoticeAlertAsync();
    }


    private void GameBannerAndPost_Unloaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _bannerTimer.Stop();
        _bannerTimer.Tick -= _bannerTimer_Tick;
        // Lunarward：这里**不要**把 Banners/PostGroups 置 null。
        // 页面加了 NavigationCacheMode 之后控件实例会复用，一旦把 ItemsSource 清空，
        // Uno 的 Pivot 在重新赋值为新列表时不会重建条目，左下角公告区就只剩空卡片（真机复现）。
    }



    private void OnMainWindowStateChanged(object _, MainWindowStateChangedMessage message)
    {
        try
        {
            if (message.Activate)
            {
                _bannerTimer.Start();
            }
            else if (message.Hide || message.SessionLock)
            {
                _bannerTimer.Stop();
            }
        }
        catch { }
    }




    private async void OnGameAnnouncementSettingChanged(object _, GameAnnouncementSettingChangedMessage message)
    {
        // 没有设置取消，网络不好时可能会造成状态异常，懒得写了
        if (AppConfig.EnableBannerAndPost)
        {
            ShowBannerAndPost = true;
            await UpdateGameContentAsync();
            if (AppConfig.DisableGameNoticeRedHot)
            {
                IsGameNoticesAlert = false;
            }
            else
            {
                await UpdateGameNoticeAlertAsync();
            }
        }
        else
        {
            ShowBannerAndPost = false;
        }
    }



    public List<GameBanner>? Banners { get; set => SetProperty(ref field, value); }





    public List<GamePostGroup>? PostGroups { get; set => SetProperty(ref field, value); }





    public bool IsGameNoticesAlert { get; set => SetProperty(ref field, value); }




    public bool ShowBannerAndPost
    {
        get => this.Opacity == 1;
        set
        {
            if (value && Banners?.Count > 0 && PostGroups?.Count > 0)
            {
                _bannerTimer.Start();
                this.Opacity = 1;
                this.IsHitTestVisible = true;
            }
            else
            {
                _bannerTimer.Stop();
                this.Opacity = 0;
                this.IsHitTestVisible = false;
            }
        }
    }




    private async Task UpdateGameContentAsync()
    {
        try
        {
            var content = await _hoYoPlayService.GetGameContentAsync(CurrentGameId);
            if (content is null || !AppConfig.EnableBannerAndPost)
            {
                ShowBannerAndPost = false;
                return;
            }
            Banners = content.Banners;
            // Uno 的 Pivot 会复用条目容器：仅仅换 ItemsSource 的内容不会重建 tab 内容（真机复现：
            // 换游戏后 banner 变了、三个 tab 里还是上一个游戏的活动）。先清空、等一次布局周期再赋新列表。
            Pivot_Posts.ItemsSource = null;
            PostGroups = GamePostGroup.FromGameContent(content);
            await Task.Delay(60);
            Pivot_Posts.ItemsSource = PostGroups;
            ShowBannerAndPost = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get game launcher content ({CurrentGameId})", CurrentGameId);
        }
    }




    private async Task UpdateGameNoticeAlertAsync()
    {
        try
        {
            if (GameFeatureConfig.FromGameId(CurrentGameId).InGameNoticesWindow)
            {
                Button_InGameNotices.Visibility = Visibility.Visible;
            }
            else
            {
                Button_InGameNotices.Visibility = Visibility.Collapsed;
                return;
            }
            if (AppConfig.DisableGameNoticeRedHot)
            {
                IsGameNoticesAlert = false;
            }
            else
            {
                IsGameNoticesAlert = await _gameNoticeService.IsNoticeAlertAsync(CurrentGameId.GameBiz);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get game launcher content ({CurrentGameId})", CurrentGameId);
        }
    }




    private void _bannerTimer_Tick(Microsoft.UI.Dispatching.DispatcherQueueTimer sender, object args)
    {
        try
        {
            if (Banners?.Count > 0)
            {
                FlipView_Banner.SelectedIndex = (FlipView_Banner.SelectedIndex + 1) % Banners.Count;
            }
        }
        catch { }
    }



    private void Image_Banner_Loaded(object sender, RoutedEventArgs e)
    {
        // FlipView 会把指针事件标记为已处理，普通的 Tapped 订阅收不到；
        // 这里用 handledEventsToo:true 注册，保证点轮播一定进我们的处理。
        if (sender is UIElement element)
        {
            element.AddHandler(UIElement.TappedEvent, new TappedEventHandler(Image_Banner_Tapped), true);
        }
    }


    private void PostRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: GamePost post } && !string.IsNullOrWhiteSpace(post.Link))
            {
                Helpers.OpenUrl.Open(post.Link);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "open post link failed");
        }
        e.Handled = true;
    }


    private void Image_Banner_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Uno 下 DataTemplate 里的元素拿不到 DataContext，退到 FlipView 当前选中项。
        GameBanner? banner = (sender as FrameworkElement)?.DataContext as GameBanner
                             ?? FlipView_Banner?.SelectedItem as GameBanner;
        if (banner is not null && !string.IsNullOrWhiteSpace(banner.Image?.Link))
        {
            Starward.Helpers.OpenUrl.Open(banner.Image.Link);
        }
        else
        {
            Serilog.Log.Information("Banner tapped but no GameBanner available");
        }
    }


    private void Grid_BannerContainer_Tapped(object sender, TappedRoutedEventArgs e)
    {
        // Uno 下 DataTemplate 里的 Image.Tapped 不触发、容器 DataContext 为 null，
        // 只能从 FlipView 的选中项取当前轮播条目。
        if (FlipView_Banner?.SelectedItem is GameBanner banner && !string.IsNullOrWhiteSpace(banner.Image?.Link))
        {
            Starward.Helpers.OpenUrl.Open(banner.Image.Link);
        }
        else
        {
            Serilog.Log.Information("Banner tapped but no selected item (selected={Selected})", FlipView_Banner?.SelectedItem is null ? "<null>" : FlipView_Banner.SelectedItem.GetType().Name);
        }
    }



    /// <summary>
    /// 隐藏 FilpView 中自动出现的翻页按键
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void FlipView_Banner_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var grid = VisualTreeHelper.GetChild(FlipView_Banner, 0);
            if (grid != null)
            {
                var count = VisualTreeHelper.GetChildrenCount(grid);
                if (count > 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        var child = VisualTreeHelper.GetChild(grid, i);
                        if (child is Button button)
                        {

                            button.IsHitTestVisible = false;
                            button.Opacity = 0;
                        }
                    }
                }
            }
        }
        catch { }

#if DEBUG
        // 开发期验证：环境变量 LUNARWARD_DEV_BANNER_AUTOLINK=1 时，加载后自动打开当前轮播链接，
        // 用于在无法可靠合成鼠标点击的环境里验证链接取值与 xdg-open 链路。
        if (Environment.GetEnvironmentVariable("LUNARWARD_DEV_BANNER_AUTOLINK") == "1")
        {
            _ = DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(1500);
                if (FlipView_Banner?.SelectedItem is GameBanner devBanner && !string.IsNullOrWhiteSpace(devBanner.Image?.Link))
                {
                    Serilog.Log.Information("[dev] banner autolink: {Link}", devBanner.Image.Link);
                    Starward.Helpers.OpenUrl.Open(devBanner.Image.Link);
                }
                else
                {
                    Serilog.Log.Information("[dev] banner autolink: no banner selected");
                }
            });
        }
#endif
    }




    private void Grid_BannerContainer_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        _bannerTimer.Stop();
        Border_PipsPager.Visibility = Visibility.Visible;
    }



    private void Grid_BannerContainer_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        _bannerTimer.Start();
        Border_PipsPager.Visibility = Visibility.Collapsed;
    }






    [RelayCommand]
    private void OpenGameNoticeWindow()
    {
        try
        {
            // 公告页是米哈游的网页，需要登录态与 JS SDK；Uno 的 WebView2 在本机只显示黑底（真机截图），
            // 窗口里给不出内容。这里按方案 A 直接交给系统浏览器打开（D40 的延续）：
            // 内嵌窗口留待以后换成外部 WebKitGTK 再启用。
            var service = AppConfig.GetService<GameNoticeService>();
            string url = service.GetGameNoticeUrl(CurrentGameId.GameBiz);
            if (!Helpers.OpenUrl.Open(url))
            {
                Helpers.InAppToast.MainWindow?.Warning("没能打开公告：系统里没有可用的浏览器？", null, 6000);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "open game notice failed");
        }
    }





    public static string AddOne(int number)
    {
        return (number + 1).ToString();
    }




}
