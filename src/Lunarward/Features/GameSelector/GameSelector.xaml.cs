using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameLauncher;
using Starward.GameInstall;
using Starward.Features.Setting;
using Starward.Features.ViewHost;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Windows.Foundation;


namespace Starward.Features.GameSelector;

[INotifyPropertyChanged]
public sealed partial class GameSelector : UserControl
{


    public event EventHandler<(GameId, bool DoubleTapped)>? CurrentGameChanged;


    private readonly HoYoPlayService _hoyoplayService = AppConfig.GetService<HoYoPlayService>();


    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();



    public GameSelector()
    {
        this.InitializeComponent();
        InitializeGameSelector();
        this.Loaded += GameSelector_Loaded;
        WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, OnLanguageChanged);
        WeakReferenceMessenger.Default.Register<MainWindowStateChangedMessage>(this, OnMainWindowStateChanged);
        WeakReferenceMessenger.Default.Register<MainWindowDragRectAdaptToGameIconMessage>(this, OnMainWindowStateChanged);
    }



    public GameBiz CurrentGameBiz { get; set; }


    public GameId? CurrentGameId { get; set; }


    public ObservableCollection<GameBizIcon> GameBizIcons { get; set => SetProperty(ref field, value); } = new();


    public GameBizIcon? CurrentGameBizIcon { get; set => SetProperty(ref field, value); }


    public bool IsPinned { get; set => SetProperty(ref field, value); }


    #region LUNARWARD-DEVIATION: 移植侧选择层状态的持久化

    // 上游 0.19.0 删除了 AppConfig.CachedGameInfo / SelectedGameBizs / IsGameBizSelectorPinned /
    // CurrentGameBiz（[Obsolete(..., true)]），当前游戏只记 AppConfig.CurrentGameId (string?)。
    // GameSelector 是移植侧独有的选择层（上游用 GameSwitcher/LauncherConfigView 代替），
    // 它需要「已固定的游戏区服列表 / 缓存的游戏信息 / 是否固定」三份状态，这里改用 AppConfig
    // 的通用键值接口（与 GameInstallScanner 的 GameScanRoots 同一套），键名沿用旧属性名，
    // 这样升级后原有用户的选择不会丢。

    private const string SelectedGameBizsKey = "SelectedGameBizs";

    private const string CachedGameInfoKey = "CachedGameInfo";

    private const string IsGameBizSelectorPinnedKey = "IsGameBizSelectorPinned";

    private const string CurrentGameBizKey = "CurrentGameBiz";

    /// <summary>所有可被 GameId 反查到的游戏区服。</summary>
    private static readonly GameBiz[] AllGameBizs =
    [
        GameBiz.hk4e_cn, GameBiz.hk4e_bilibili, GameBiz.hk4e_global,
        GameBiz.hkrpg_cn, GameBiz.hkrpg_bilibili, GameBiz.hkrpg_global,
        GameBiz.nap_cn, GameBiz.nap_bilibili, GameBiz.nap_global,
        GameBiz.bh3_cn, GameBiz.bh3_global,
    ];


    /// <summary>当前选择的游戏区服（以 AppConfig.CurrentGameId 为准，旧键仅作迁移兜底）。</summary>
    private static GameBiz LoadCurrentGameBiz()
    {
        string? id = AppConfig.CurrentGameId;
        if (string.IsNullOrWhiteSpace(id))
        {
            return GameBiz.TryParse(AppConfig.GetValue<string>(null, CurrentGameBizKey), out GameBiz old) ? old : default;
        }
        foreach (GameBiz biz in AllGameBizs)
        {
            if (GameId.FromGameBiz(biz)?.Id == id)
            {
                return biz;
            }
        }
        return default;
    }


    /// <summary>
    /// 记录当前选择的游戏区服：写上游的 AppConfig.CurrentGameId，同时更新旧键给尚未迁移的移植侧代码读。
    /// </summary>
    private static void SaveCurrentGameBiz(GameBiz gameBiz)
    {
        AppConfig.SetValue(gameBiz.Value, CurrentGameBizKey);
        if (GameId.FromGameBiz(gameBiz) is GameId gameId)
        {
            AppConfig.CurrentGameId = gameId.Id;
        }
    }


    /// <summary>记录中且位于可移动存储上的安装目录（对应已删除的 AppConfig.GetGameInstallPathRemovable）。</summary>
    private static bool IsGameInstallPathInRemovableStorage(GameId gameId)
    {
        return GameLauncherService.GetGameInstallPath(gameId, out bool storageRemoved) is not null && storageRemoved;
    }


    /// <summary>写入/清除游戏安装目录（对应已删除的 AppConfig.SetGameInstallPath）。</summary>
    private void SetGameInstallPath(GameId gameId, string? path)
    {
        try
        {
            if (_hoyoplayService.GetLauncherConfig(gameId) is not null)
            {
                _gameLauncherService.ChangeGameInstallPath(gameId, path);
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        // 兜底：HoYoPlay client 还没恢复（首启、没拉过启动器列表）时直接写 GameEntry 表，
        // 行为与 GameLauncherService.ChangeGameInstallPath 一致，只是跳过图标固定消息。
        if (string.IsNullOrWhiteSpace(path))
        {
            GameEntryService.SetInstallPathNull(gameId);
        }
        else
        {
            string relativePath = GameLauncherService.GetRelativePathIfInRemovableStorage(path, out bool removable);
            GameEntryService.SetInstallPath(gameId, LauncherId.FromGameBiz(gameId.GameBiz) ?? LauncherId.ChinaOfficial, relativePath, removable);
        }
    }

    #endregion


    private bool ignoreDpiChanged = false;

    private double lastScale = 1;



    public void InitializeGameSelector()
    {
        List<GameInfo> gameInfos = GetCachedGameInfos();
        InitializeGameIconsArea(gameInfos);
        InitializeGameServerArea(gameInfos);
        InitializeInstalledGamesCommand.Execute(null);
    }



    private async void GameSelector_Loaded(object sender, RoutedEventArgs e)
    {
        this.XamlRoot.Changed -= XamlRoot_Changed;
        this.XamlRoot.Changed += XamlRoot_Changed;
        await Task.Delay(1000);
        await UpdateGameInfoAsync();
    }



    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (ignoreDpiChanged)
        {
            return;
        }
        if (lastScale != sender.RasterizationScale)
        {
            lastScale = sender.RasterizationScale;
            UpdateDragRectangles();
        }
    }





    private async void OnLanguageChanged(object? _, LanguageChangedMessage __)
    {
        this.Bindings.Update();
        await UpdateGameInfoAsync();
    }




    private async void OnMainWindowStateChanged(object? _, MainWindowStateChangedMessage message)
    {
        try
        {
            if (message.Activate && (message.ElapsedOver(TimeSpan.FromMinutes(10)) || message.IsCrossingHour))
            {
                await UpdateGameInfoAsync();
            }
        }
        catch { }
    }



    private void OnMainWindowStateChanged(object? _, MainWindowDragRectAdaptToGameIconMessage message)
    {
        if (!message.IgnoreDpiChanged)
        {
            UpdateDragRectangles();
        }
        ignoreDpiChanged = message.IgnoreDpiChanged;
    }




    private List<GameInfo> GetCachedGameInfos()
    {
        try
        {
            string? json = AppConfig.GetValue<string>(null, CachedGameInfoKey);
            if (!string.IsNullOrWhiteSpace(json))
            {
                return JsonSerializer.Deserialize<List<GameInfo>>(json) ?? [];
            }
        }
        catch { }
        return [];
    }




    #region Game Icon


    /// <summary>
    /// 初始化游戏图标区域
    /// </summary>
    private void InitializeGameIconsArea(List<GameInfo> gameInfos)
    {
        try
        {
            GameBizIcons.CollectionChanged -= GameBizIcons_CollectionChanged;
            GameBizIcons.Clear();

            // 从配置文件读取已选的 GameBiz
            string? bizs = AppConfig.GetValue<string>(null, SelectedGameBizsKey);
            foreach (string str in bizs?.Split(',')?.Distinct() ?? [])
            {
                if (GameBiz.TryParse(str, out GameBiz biz))
                {
                    // 已知的 GameBiz
                    GameBizIcons.Add(new GameBizIcon(biz));
                }
                else if (gameInfos.FirstOrDefault(x => x.GameBiz == biz) is GameInfo info)
                {
                    // 由 HoYoPlay API 获取，但未适配的 GameBiz
                    GameBizIcons.Add(new GameBizIcon(info));
                }
            }

            // 选取当前游戏
            GameBiz lastSelectedGameBiz = LoadCurrentGameBiz();
            if (GameBizIcons.FirstOrDefault(x => x.GameBiz == lastSelectedGameBiz) is GameBizIcon icon)
            {
                CurrentGameBizIcon = icon;
                CurrentGameBizIcon.IsSelected = true;
                CurrentGameBiz = lastSelectedGameBiz;
            }
            else if (lastSelectedGameBiz.IsKnown())
            {
                CurrentGameBizIcon = new GameBizIcon(lastSelectedGameBiz);
                CurrentGameBizIcon.IsSelected = true;
                CurrentGameBiz = lastSelectedGameBiz;
            }
            else if (gameInfos.FirstOrDefault(x => x.GameBiz == lastSelectedGameBiz) is GameInfo info)
            {
                CurrentGameBizIcon = new GameBizIcon(info);
                CurrentGameBizIcon.IsSelected = true;
                CurrentGameBiz = lastSelectedGameBiz;
            }

            CurrentGameId = CurrentGameBizIcon?.GameId;
            if (CurrentGameId is not null)
            {
                CurrentGameChanged?.Invoke(this, (CurrentGameId, false));
            }

            if (AppConfig.GetValue(false, IsGameBizSelectorPinnedKey))
            {
                Pin();
            }

            GameBizIcons.CollectionChanged += GameBizIcons_CollectionChanged;
        }
        catch { }
        if (GameBizIcons.Count == 0 && CurrentGameBizIcon is null)
        {
            TeachTip_SelectGame.IsOpen = true;
        }
        // Lunarward：Uno 的 TeachingTip 不会像 WinUI 那样自动消失，选中游戏/切页时手动收起
        CurrentGameChanged += (_, _) => HideSelectGameTip();
    }



    /// <summary>收起「点击此处选择或切换游戏」提示（Uno 下 TeachingTip 需要手动关闭）。</summary>
    public void HideSelectGameTip()
    {
        if (TeachTip_SelectGame is not null)
        {
            TeachTip_SelectGame.IsOpen = false;
        }
    }

    /// <summary>
    /// 游戏图标区域是否可见
    /// </summary>
    public bool GameIconsAreaVisible
    {
        get => Grid_GameIconsArea.Translation == Vector3.Zero;
        set
        {
            if (value)
            {
                Grid_GameIconsArea.Translation = Vector3.Zero;
            }
            else
            {
                Grid_GameIconsArea.Translation = new Vector3(0, -100, 0);
            }
            UpdateDragRectangles();
        }
    }


    /// <summary>
    /// 更新窗口拖拽区域
    /// </summary>
    public void UpdateDragRectangles()
    {
        try
        {
            double x = Border_CurrentGameIcon.ActualWidth;
            if (GameIconsAreaVisible)
            {
                x = Border_CurrentGameIcon.ActualWidth + Grid_GameIconsArea.ActualWidth;
            }
            this.XamlRoot.SetWindowDragRectangles([new Rect(x, 0, 10000, 48)]);
        }
        catch { }
    }



    /// <summary>
    /// 鼠标移入到当前游戏图标
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Border_CurrentGameIcon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        // 显示所有游戏图标
        GameIconsAreaVisible = true;
    }



    /// <summary>
    /// 鼠标移出当前游戏图标
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Border_CurrentGameIcon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (FullBackgroundVisible || IsPinned)
        {
            // 当前游戏图标被固定或者全屏显示时，不隐藏所有游戏图标
            return;
        }
        if (sender is UIElement ele)
        {
            var postion = e.GetCurrentPoint(sender as UIElement).Position;
            if (postion.X > ele.ActualSize.X - 1 && postion.Y > 0 && postion.Y < ele.ActualSize.Y)
            {
                // 从右侧移出，此时进入到所有游戏图标区域，不隐藏
                return;
            }
        }
        // 其他方向移出，隐藏所有游戏图标
        GameIconsAreaVisible = false;
    }



    /// <summary>
    /// 鼠标移出所有游戏图标区域
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid_GameIconsArea_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (FullBackgroundVisible || IsPinned)
        {
            return;
        }
        GameIconsAreaVisible = false;
    }



    /// <summary>
    /// 点击没有被选择的游戏图标
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid_GameIcon_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GameBizIcon icon)
        {
            if (CurrentGameBizIcon is not null)
            {
                CurrentGameBizIcon.IsSelected = false;
            }

            CurrentGameBizIcon = icon;
            CurrentGameBiz = icon.GameBiz;
            CurrentGameId = icon.GameId;
            icon.IsSelected = true;

            CurrentGameChanged?.Invoke(this, (icon.GameId, false));
            SaveCurrentGameBiz(icon.GameBiz);
        }
    }



#if DEBUG
    /// <summary>
    /// 开发用：按 biz 直接切游戏（等价于点图标选服），用于无人值守复现「切换游戏后左下角不刷新」。
    /// </summary>
    public void DevSelectGame(Starward.Core.GameBiz biz)
    {
        if (string.IsNullOrWhiteSpace(biz.Value))
        {
            return;
        }
        CurrentGameBiz = biz;
        CurrentGameId = Starward.Core.HoYoPlay.GameId.FromGameBiz(biz);
        SaveCurrentGameBiz(biz);
        CurrentGameChanged?.Invoke(this, (CurrentGameId, false));
        Serilog.Log.Information("[dev] switched game to {biz}", biz.Value);
    }
#endif


    /// <summary>
    /// 双击没有被选择的游戏图标
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid_GameIcon_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GameBizIcon icon)
        {
            if (CurrentGameBizIcon is not null)
            {
                CurrentGameBizIcon.IsSelected = false;
            }

            CurrentGameBizIcon = icon;
            CurrentGameBiz = icon.GameBiz;
            CurrentGameId = icon.GameId;
            icon.IsSelected = true;
            HideFullBackground();

            CurrentGameChanged?.Invoke(this, (icon.GameId, true));
            SaveCurrentGameBiz(icon.GameBiz);
        }
    }


    /// <summary>
    /// 鼠标移入到游戏图标
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid_GameIcon_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GameBizIcon icon)
        {
            if (!icon.IsSelected)
            {
                icon.MaskOpacity = 0;
            }
        }
    }



    /// <summary>
    /// 鼠标移出游戏图标
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid_GameIcon_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GameBizIcon icon)
        {
            if (!icon.IsSelected)
            {
                icon.MaskOpacity = 1;
            }
        }
    }



    /// <summary>
    /// 游戏图标区域大小变化
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid_GameIconsArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDragRectangles();
    }



    private void ScrollViewer_GameBizs_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateGameCardSize();
    }


    /// <summary>
    /// 按可用宽度均分卡片宽度（D56）：一张卡片 192 DIP 时 6 个游戏要 1232 DIP，
    /// 超过窗口可用宽度就会被裁掉；Uno 后端又没有横向滚动条，只能把卡片压到刚好铺满。
    /// </summary>
    private void UpdateGameCardSize()
    {
        try
        {
            int count = GameBizDisplays?.Count ?? 0;
            if (count == 0)
            {
                return;
            }
            double avail = ScrollViewer_GameBizs.ActualWidth - 12; // StackPanel 右侧留白
            if (avail <= 0)
            {
                return;
            }
            const double spacing = 16;
            double cardWidth = Math.Floor((avail - (spacing * (count - 1))) / count);
            cardWidth = Math.Clamp(cardWidth, 140, 192);
            double cardHeight = Math.Round(cardWidth * 108.0 / 192.0);
            foreach (var display in GameBizDisplays)
            {
                if (Math.Abs(display.CardWidth - cardWidth) > 0.5)
                {
                    display.CardWidth = cardWidth;
                    display.CardHeight = cardHeight;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "game selector card size failed");
        }
    }



    /// <summary>
    /// 固定游戏待选区
    /// </summary>
    [RelayCommand]
    private void Pin()
    {
        IsPinned = !IsPinned;
        if (IsPinned)
        {
            GameIconsAreaVisible = true;
        }
        else
        {
            // 避免在固定时更换当前游戏，取消固定后，左上角的图标不改变的问题
            var temp = CurrentGameBizIcon;
            CurrentGameBizIcon = null;
            CurrentGameBizIcon = temp;
            if (!FullBackgroundVisible)
            {
                GameIconsAreaVisible = false;
            }
        }
        AppConfig.SetValue(IsPinned, IsGameBizSelectorPinnedKey);
    }



    /// <summary>
    /// 待选游戏变更时，保存配置
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void GameBizIcons_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        try
        {
            var sb = new StringBuilder();
            foreach (var icon in GameBizIcons)
            {
                sb.Append(icon.GameBiz);
                sb.Append(',');
            }
            AppConfig.SetValue(sb.ToString().TrimEnd(','), SelectedGameBizsKey);
        }
        catch { }
    }



    #endregion





    #region Full Background 黑色半透明背景


    public bool FullBackgroundVisible => Border_FullBackground.Opacity > 0;


    [RelayCommand]
    private void ShowFullBackground()
    {
        Border_FullBackground.Opacity = 1;
        Border_FullBackground.IsHitTestVisible = true;
        Border_FullBackground.Visibility = Visibility.Visible;
        Border_Pin.Opacity = 1;
        Border_Pin.IsHitTestVisible = true;
        Border_Pin.Visibility = Visibility.Visible;
        GameIconsAreaVisible = true;
    }


    /// <summary>关闭游戏选择层（Esc 用；只收层，不动主窗口）。</summary>
    public void CloseLayer() => HideFullBackground();


    private void HideFullBackground()
    {
        Border_FullBackground.Opacity = 0;
        Border_FullBackground.IsHitTestVisible = false;
        Border_FullBackground.Visibility = Visibility.Collapsed;
        Border_Pin.Opacity = 0;
        Border_Pin.IsHitTestVisible = false;
        Border_Pin.Visibility = Visibility.Collapsed;
        if (!IsPinned)
        {
            GameIconsAreaVisible = false;
        }
    }


    private void Border_FullBackground_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        _isGameBizDisplayPressed = false;
        var position = e.GetPosition(sender as UIElement);
        if (position.X <= Border_CurrentGameIcon.ActualWidth && position.Y <= Border_CurrentGameIcon.ActualHeight)
        {
            Border_FullBackground.Opacity = 0;
            Border_FullBackground.IsHitTestVisible = false;
            Border_FullBackground.Visibility = Visibility.Collapsed;
            Border_Pin.Opacity = 0;
            Border_Pin.IsHitTestVisible = false;
            Border_Pin.Visibility = Visibility.Collapsed;
        }
        else
        {
            HideFullBackground();
        }
    }


    #endregion





    #region Game Server



    public List<GameBizDisplay> GameBizDisplays { get; set => SetProperty(ref field, value); }




    /// <summary>
    /// 初始化游戏服务器选择区域
    /// </summary>
    private void InitializeGameServerArea(List<GameInfo> gameInfos)
    {
        try
        {
            var list = new List<GameBizDisplay>();

            if (LanguageUtil.FilterLanguage(CultureInfo.CurrentUICulture.Name) is "zh-cn")
            {
                // 当前语言为简体中文时，游戏信息显示从中国官服获取的内容
                foreach (var info in gameInfos)
                {
                    if (info.GameBiz.IsChinaServer() && !info.GameBiz.IsBilibili())
                    {
                        list.Add(new GameBizDisplay { GameInfo = info });
                    }
                }
            }
            else
            {
                // 当前语言不为简体中文时，游戏信息显示从国际服获取的内容
                foreach (var info in gameInfos)
                {
                    if (info.GameBiz.IsGlobalServer())
                    {
                        list.Add(new GameBizDisplay { GameInfo = info });
                    }
                }
            }

            // 分类每个游戏的服务器信息
            foreach (var item in list)
            {
                string game = item.GameInfo.GameBiz.Game;
                foreach (string suffix in (string[])["_cn", "_global", "_bilibili"])
                {
                    GameBiz biz = game + suffix;
                    if (biz.IsKnown())
                    {
                        var server = new GameBizIcon(biz)
                        {
                            IsPinned = GameBizIcons.Any(x => x.GameBiz == biz),
                        };
                        item.Servers.Add(server);
                    }
                    else if (gameInfos.FirstOrDefault(x => x.GameBiz == biz) is GameInfo info)
                    {
                        var server = new GameBizIcon(info)
                        {
                            IsPinned = GameBizIcons.Any(x => x.GameBiz == biz),
                        };
                        item.Servers.Add(server);
                    }
                }
            }
            GameBizDisplays = new(list);
            DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, UpdateGameCardSize);
            if (Features.Dev.DevSwitches.IsEnabled && Environment.GetEnvironmentVariable("LUNARWARD_DEV_SELECTOR") == "1")
            {
                // 验证用：Wayland 下合成点击不可靠（L-36），直接展开选择层截图。
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1800);
                    DispatcherQueue?.TryEnqueue(ShowFullBackground);
                });
            }
        }
        catch { }
    }



    /// <summary>
    /// 拉取所有已启用启动器的游戏信息并缓存。
    /// LUNARWARD-DEVIATION: 移植侧原来的 Features/HoYoPlay/HoYoPlayService.UpdateGameInfoListAsync
    /// 已随上游 0.19.0 的合并删除（现在用 Starward.GameInstall 的 HoYoPlayService + LauncherConfig），
    /// 这里按原语义重建：先刷新启动器客户端映射，再逐个拿游戏信息，最后写缓存。
    /// 旧实现还会预下载游戏背景图，那个下载器随旧模块一起没了，这里不再做（背景图由 Background 模块按需拉取）。
    /// </summary>
    private async Task<List<GameInfo>> UpdateGameInfoListAsync(CancellationToken cancellationToken = default)
    {
        List<GameInfo> gameInfos = [];
        List<LauncherConfigModel> launchers = LauncherConfigModel.GetEnabled();
        await _hoyoplayService.UpdateLauncherClientMapAsync(launchers.Select(x => x.ToLauncherConfig()), cancellationToken);
        foreach (LauncherConfigModel launcher in launchers)
        {
            try
            {
                gameInfos.AddRange(await _hoyoplayService.GetGameInfosAsync(launcher.ToLauncherConfig(), cancellationToken));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }
        AppConfig.SetValue(JsonSerializer.Serialize(gameInfos), CachedGameInfoKey);
        return gameInfos;
    }



    /// <summary>
    /// 更新所有游戏服务器信息，更新游戏图标
    /// </summary>
    /// <returns></returns>
    private async Task UpdateGameInfoAsync()
    {
        try
        {
            List<GameInfo> gameInfos = await UpdateGameInfoListAsync();
            InitializeGameServerArea(gameInfos);
            foreach (GameBizIcon icon in GameBizIcons)
            {
                if (icon.GameBiz.IsKnown())
                {
                    icon.UpdateInfo();
                }
                else
                {
                    GameInfo info = await _hoyoplayService.GetGameInfoAsync(icon.GameId);
                    icon.UpdateInfo(info);
                }
            }
            await InitializeInstalledGamesCommand.ExecuteAsync(null);
        }
        catch { }
    }




    /// <summary>
    /// 游戏服务器选择区域是否可见
    /// </summary>
    private bool _isGameBizDisplayPressed = false;


    /// <summary>
    /// 鼠标点击游戏服务器选择区域，显示游戏服务器选择菜单
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid_GameBizDisplay_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement ele)
        {
            e.Handled = true;
            _isGameBizDisplayPressed = !_isGameBizDisplayPressed;
            FlyoutBase.GetAttachedFlyout(ele)?.ShowAt(ele, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.Bottom,
                ShowMode = FlyoutShowMode.Transient,
            });
        }
    }


    /// <summary>
    /// 鼠标移入到游戏服务器选择区域
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Grid_GameBizDisplay_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement ele && _isGameBizDisplayPressed)
        {
            FlyoutBase.GetAttachedFlyout(ele)?.ShowAt(ele, new FlyoutShowOptions
            {
                Placement = FlyoutPlacementMode.Bottom,
                ShowMode = FlyoutShowMode.Transient,
            });
        }
    }


    /// <summary>
    /// 点击服务器图标
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Button_GameServer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.DataContext is GameBizIcon server)
            {
                if (CurrentGameBizIcon is not null)
                {
                    CurrentGameBizIcon.IsSelected = false;
                }

                if (GameBizIcons.FirstOrDefault(x => x.GameId == server.GameId) is GameBizIcon icon)
                {
                    CurrentGameBizIcon = icon;
                    CurrentGameBiz = icon.GameBiz;
                    CurrentGameId = icon.GameId;
                    icon.IsSelected = true;
                }
                else
                {
                    CurrentGameBizIcon = server;
                    server.IsSelected = true;
                }

                CurrentGameChanged?.Invoke(this, (server.GameId, false));
                // 关闭弹出的服务器选择菜单
                if (VisualTreeHelper.GetOpenPopupsForXamlRoot(this.XamlRoot).FirstOrDefault() is Popup popup)
                {
                    popup.IsOpen = false;
                }
                HideFullBackground();
                _isGameBizDisplayPressed = false;
                SaveCurrentGameBiz(server.GameBiz);
            }
        }
        catch { }
    }


    /// <summary>
    /// 固定游戏服务器图标到待选区
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Button_PinGameBiz_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is GameBizIcon server)
        {
            var biz = server.GameBiz;
            if (GameBizIcons.FirstOrDefault(x => x.GameBiz == biz) is GameBizIcon icon)
            {
                GameBizIcons.Remove(icon);
                server.IsPinned = false;
            }
            else
            {
                GameBizIcons.Add(server);
                server.IsPinned = true;
            }
        }
    }




    #endregion





    #region Installed Games


    /// <summary>
    /// 已安装游戏的实际占用空间
    /// </summary>
    public string? InstalledGamesActualSize { get; set => SetProperty(ref field, value); }


    /// <summary>
    /// 已安装游戏的总空间
    /// </summary>
    public string? InstalledGamesSavedSize { get; set => SetProperty(ref field, value); }




    public ObservableCollection<GameBizIcon> InstalledGames { get; set; } = new();



    private CancellationTokenSource? _initializeInstalledGamesCancellationTokenSource;


    /// <summary>
    /// 初始化已安装游戏列表，计算已安装游戏的实际占用空间
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task InitializeInstalledGamesAsync()
    {
        const double GB = 1 << 30;
        try
        {
            _initializeInstalledGamesCancellationTokenSource?.Cancel();
            _initializeInstalledGamesCancellationTokenSource = new();
            CancellationToken token = _initializeInstalledGamesCancellationTokenSource.Token;

            InstalledGames.Clear();
            InstalledGamesActualSize = null;
            InstalledGamesSavedSize = null;
            List<FileInfo> files = new();

            foreach (GameBizDisplay display in GameBizDisplays)
            {
                List<FileInfo> _duplicateFiles = new();
                int serverCount = 0;
                foreach (GameBizIcon server in display.Servers)
                {
                    string? installPath = GameLauncherService.GetGameInstallPath(server.GameId);
                    if (Directory.Exists(installPath))
                    {
                        server.InstallPath = installPath;
                        var _files = new DirectoryInfo(installPath).EnumerateFiles("*", SearchOption.AllDirectories).ToList();
                        server.TotalSize = _files.Sum(x => x.Length);
                        InstalledGames.Add(server);
                        if (_files.Count() > 0)
                        {
                            serverCount++;
                            _duplicateFiles.AddRange(_files);
                        }
                    }
                }
                if (serverCount > 1)
                {
                    files.AddRange(_duplicateFiles);
                }
            }
            long totalSize = InstalledGames.Sum(x => x.TotalSize);
            InstalledGamesActualSize = $"{totalSize / GB:F2}GB";

            if (token.IsCancellationRequested)
            {
                return;
            }

            if (files.Count > 0)
            {
                (long fileSize, long actualSize) = await Task.Run(() =>
                {
                    long size = 0;
                    Dictionary<string, long> dic = new();
                    foreach (var file in files)
                    {
                        size += file.Length;
                        // Lunarward：Windows 用 FILE_ID_INFO 去重；Linux 用 stat 的 (st_dev, st_ino)，硬链接得到同一个键（B27）
                        string key = Starward.Helpers.PosixIdentity.TryGetFileIdentity(file.FullName, out string identity)
                            ? identity
                            : $"{file.Length}_{file.LastWriteTimeUtc.Ticks}_{file.FullName}";
                        dic[key] = file.Length;
                    }
                    return (size, dic.Values.Sum());
                }, token);

                if (token.IsCancellationRequested)
                {
                    return;
                }

                InstalledGamesActualSize = $"{(totalSize - fileSize + actualSize) / GB:F2}GB";
                InstalledGamesSavedSize = $"{(fileSize - actualSize) / GB:F2}GB";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }




    /// <summary>
    /// 从注册表自动搜索已安装的游戏
    /// </summary>
    [RelayCommand]
    public void AutoSearchInstalledGames()
    {
        try
        {
            List<GameInfo> gameInfos = GetCachedGameInfos();
            var sb = new StringBuilder();
            foreach (GameInfo item in gameInfos)
            {
                GameBiz gameBiz = item.GameBiz;
                if (item.GameBiz.IsBilibili())
                {
                    gameBiz = $"{gameBiz.Game}_bilibili";
                }
                string? path = GameLauncherService.GetGameInstallPath(Starward.Core.HoYoPlay.GameId.FromGameBiz(gameBiz)!);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    if (Directory.Exists(path) || IsGameInstallPathInRemovableStorage(Starward.Core.HoYoPlay.GameId.FromGameBiz(gameBiz)!))
                    {
                        sb.Append(gameBiz);
                        sb.Append(',');
                        continue;
                    }
                }
                // Lunarward：Linux 没有注册表，改为按可执行文件名扫目录（GameInstallScanner，D25）
                if (!OperatingSystem.IsWindows())
                {
                    // D95：扫描本身由 GameInstallScanner 做；这里只清理「记录了但目录已不存在」的失效路径，
                    // 旧实现直接 continue，失效路径会一直留在已安装列表里（B36）。
                    if (!string.IsNullOrWhiteSpace(path) && !Directory.Exists(path) && !IsGameInstallPathInRemovableStorage(Starward.Core.HoYoPlay.GameId.FromGameBiz(gameBiz)!))
                    {
                        SetGameInstallPath(Starward.Core.HoYoPlay.GameId.FromGameBiz(gameBiz)!, null);
                    }
                    continue;
                }
                string key = "";
                if (gameBiz.Server is "cn")
                {
                    key = $@"HKEY_CURRENT_USER\Software\miHoYo\HYP\1_1\{gameBiz}";
                }
                else if (gameBiz.Server is "global")
                {
                    key = $@"HKEY_CURRENT_USER\Software\Cognosphere\HYP\1_0\{gameBiz}";
                }
                if (!string.IsNullOrWhiteSpace(key))
                {
                    path = Registry.GetValue(key, "GameInstallPath", null) as string;
                    if (Directory.Exists(path))
                    {
                        SetGameInstallPath(Starward.Core.HoYoPlay.GameId.FromGameBiz(gameBiz)!, path);
                        sb.Append(gameBiz);
                        sb.Append(',');
                        continue;
                    }
                }
            }
            AppConfig.SetValue(sb.ToString().TrimEnd(','), SelectedGameBizsKey);
            InitializeGameSelector();
            if (!IsPinned)
            {
                Pin();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }




    /// <summary>
    /// 防止点击已安装游戏列表时，触发 <see cref="Border_FullBackground_Tapped(object, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs)"/>
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void Expander_InstalledGamesActualSize_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        e.Handled = true;
    }



    #endregion







}


