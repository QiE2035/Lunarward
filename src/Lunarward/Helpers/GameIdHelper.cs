// Lunarward：0.19.0 起 AppConfig 只存 GameId.Id 字符串（AppConfig.CurrentGameBiz 已 [Obsolete(error: true)]），
// 而移植侧多处逻辑仍以 GameBiz 为单位（运行环境绑定、伴随启动、启动计划描述）。
// 这里集中做「当前游戏 id → GameBiz」的反查，避免每个调用点各写一遍（D128）。
using System.Linq;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameSelector;

namespace Starward.Helpers;

internal static class GameIdHelper
{
    /// <summary>当前选中游戏的 GameBiz；没有选中或条目未登记时返回 null。</summary>
    public static GameBiz? CurrentGameBiz()
    {
        string? id = AppConfig.CurrentGameId;
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }
        GameEntry? entry = GameEntryService.GetAllEntries().FirstOrDefault(x => x.GameId == id);
        if (entry is null)
        {
            return null;
        }
        var biz = new GameBiz(entry.GameBiz);
        return string.IsNullOrWhiteSpace(biz.Value) ? null : biz;
    }
}
