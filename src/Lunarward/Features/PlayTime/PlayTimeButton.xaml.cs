using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Core;
using Starward.Features.Database;
using System;
using System.Threading.Tasks;


namespace Starward.Features.PlayTime;

[INotifyPropertyChanged]
public sealed partial class PlayTimeButton : UserControl
{


    public GameBiz CurrentGameBiz { get; set; }


    private readonly ILogger<PlayTimeButton> _logger = AppConfig.GetLogger<PlayTimeButton>();



    public PlayTimeButton()
    {
        this.InitializeComponent();
    }



    public TimeSpan PlayTimeTotal { get; set => SetProperty(ref field, value); }



    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        InitializePlayTime();
    }



    private void InitializePlayTime()
    {
        try
        {
            GameBiz gameBiz = CurrentGameBiz.IsBilibili() ? $"{CurrentGameBiz.Game}_cn" : CurrentGameBiz;
            PlayTimeTotal = DatabaseService.GetValue<TimeSpan>($"playtime_total_{gameBiz}", out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize play time");
        }
    }



    /// <summary>
    /// 切换游戏时重读总时长（D94）。上游页面每次导航都重建，本控件随页面重新 Loaded；
    /// Lunarward 缓存页面后控件不重建，只能显式换目标游戏再读一次。
    /// </summary>
    public void RefreshPlayTime(GameBiz gameBiz)
    {
        CurrentGameBiz = gameBiz;
        InitializePlayTime();
    }



    public static string TimeSpanToString(TimeSpan timeSpan)
    {
        int totalMinutes = (int)Math.Round(timeSpan.TotalMinutes);
        int hours = totalMinutes / 60, minutes = totalMinutes % 60;
        return $"{hours}h {minutes}m";
    }


    [RelayCommand]
    private async Task OpenStatsDialogAsync()
    {
        await new PlayTimeStatsDialog
        {
            CurrentGameBiz = CurrentGameBiz,
            XamlRoot = this.XamlRoot,
        }.ShowAsync();
        InitializePlayTime();
    }


}
