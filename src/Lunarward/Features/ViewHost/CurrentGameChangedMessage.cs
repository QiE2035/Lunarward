// Lunarward：主界面切换游戏时广播一次，让「已经缓存住」的页面能自己刷新。
// 背景：页面加了 NavigationCacheMode（D60）后，切换游戏不再触发页面重建，
// 依赖 OnLoaded / 导航参数的地方会保持旧游戏的内容（左下角公告/资讯就是典型）。
using Starward.Core.HoYoPlay;

namespace Starward.Features.ViewHost;


public class CurrentGameChangedMessage(GameId gameId)
{

    public GameId GameId { get; } = gameId;

}
