using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core;
using Starward.Core.HoYoPlay;
using System.Threading;

namespace Starward.Features;

[INotifyPropertyChanged]
public abstract partial class PageBase : Page
{

    public GameId CurrentGameId { get; protected set => SetProperty(ref field, value); }

    public GameBiz CurrentGameBiz { get; protected set => SetProperty(ref field, value); }

    private readonly CancellationTokenSource UnloadCancellationTokenSource = new();

    protected CancellationToken UnloadCancellationToken => UnloadCancellationTokenSource.Token;


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


    // LUNARWARD-DEVIATION: D94。上游每次导航都新建页面实例，Lunarward 缓存页面后同一实例会反复
    // Loaded/Unloaded，也必须能在切换到另一个游戏时把内容整套重载，否则页面会停在第一次的数据上。
    protected string? ContentGameId;


    private void PageEx_Unloaded(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        Loaded -= PageEx_Loaded;
        Unloaded -= PageEx_Unloaded;
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
        // LUNARWARD-DEVIATION: D94（见上）。
        string? gameId = CurrentGameId?.Id;
        if (ContentGameId is not null && ContentGameId != gameId)
        {
            ContentGameId = gameId;
            OnGameChanged();
        }
    }


    /// <summary>
    /// 缓存页面导航到另一个游戏时调用（上游此时会新建页面并重跑 OnLoaded）。
    /// </summary>
    // LUNARWARD-DEVIATION: D94。
    protected virtual void OnGameChanged()
    {

    }


    protected virtual void OnLoaded()
    {

    }


    protected virtual void OnUnloaded()
    {

    }


    protected void TextBlock_IsTextTrimmedChanged(TextBlock sender, IsTextTrimmedChangedEventArgs args)
    {
        if (sender.FontSize > 12)
        {
            sender.FontSize -= 1;
        }
    }

}
