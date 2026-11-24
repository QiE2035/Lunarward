using CommunityToolkit.Mvvm.ComponentModel;
using Starward.Core.HoYoPlay;
using System.Collections.Generic;


namespace Starward.Features.GameSelector;

public partial class GameBizDisplay : ObservableObject
{

    public GameInfo GameInfo { get; set; }


    public List<GameBizIcon> Servers { get; set; } = new();


    /// <summary>卡片宽度（DIP），由 GameSelector 按可用宽度均分后写入（D56）。</summary>
    public double CardWidth
    {
        get => field; set => SetProperty(ref field, value);
    } = 192;


    /// <summary>卡片高度（DIP），保持 16:9。</summary>
    public double CardHeight
    {
        get => field; set => SetProperty(ref field, value);
    } = 108;

}
