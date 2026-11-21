using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core;
using Starward.Core.HoYoPlay;

namespace Starward.Frameworks;

[INotifyPropertyChanged]
public abstract partial class PageBase : Page
{


    public GameId CurrentGameId { get; protected set => SetProperty(ref field, value); }


    public GameBiz CurrentGameBiz { get; protected set => SetProperty(ref field, value); }


    /// <summary>
    /// 本页当前内容对应的游戏 Id（D94）。页面缓存（NavigationCacheMode="Required"）会复用实例，
    /// 导航到别的游戏时靠它判断要不要重新初始化；null 表示页面还没加载过。
    /// </summary>
    protected string? ContentGameId { get; set; }



    public PageBase()
    {
        Loaded += PageEx_Loaded;
        Unloaded += PageEx_Unloaded;
    }



    private void PageEx_Loaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        ContentGameId = CurrentGameId?.Id;
        OnLoaded();
    }


    private void PageEx_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        // D94：上游每次导航都新建页面实例，所以 OnLoaded 只跑一次是正确语义；Lunarward 缓存页面后
        // 同一实例会反复 Loaded/Unloaded，必须每次重新挂载都跑，否则离开过本页之后消息订阅、计时器、
        // 数据加载就永远停在第一次的状态。
        OnUnloaded();
    }



    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        if (e.Parameter is GameId id)
        {
            CurrentGameId = id;
            CurrentGameBiz = id.GameBiz;
        }
        else if (e.Parameter is GameBiz biz)
        {
            CurrentGameBiz = biz;
            CurrentGameId = GameId.FromGameBiz(biz)!;
        }
        // D94：缓存页面导航到另一个游戏，等价于上游重建页面 —— 让子类把内容整套重载。
        string? gameId = CurrentGameId?.Id;
        if (ContentGameId is not null && ContentGameId != gameId)
        {
            ContentGameId = gameId;
            OnGameChanged();
        }
    }



    protected virtual void OnLoaded()
    {

    }



    protected virtual void OnUnloaded()
    {

    }



    /// <summary>
    /// 缓存页面导航到另一个游戏时调用（上游此时会新建页面并重跑 OnLoaded）。
    /// </summary>
    protected virtual void OnGameChanged()
    {

    }


}
