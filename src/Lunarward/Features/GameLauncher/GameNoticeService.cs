using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.GameNotice;
using Starward.Features.GameAccount;
using Starward.Features.GameRecord;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameLauncher;

internal class GameNoticeService
{


    private readonly ILogger<GameNoticeService> _logger;


    private readonly GameRecordService _gameRecordService;


    private readonly GameNoticeClient _gameNoticeClient;



    public GameNoticeService(ILogger<GameNoticeService> logger, GameRecordService gameRecordService, GameNoticeClient gameNoticeClient)
    {
        _logger = logger;
        _gameRecordService = gameRecordService;
        _gameNoticeClient = gameNoticeClient;
    }




    public string GetGameNoticeUrl(GameBiz biz)
        => GetGameNoticeUrlAsync(biz).GetAwaiter().GetResult();



    /// <summary>UI 路径必须用这个：uid 可能要读 wine 注册表（一次 fork 0.1–4 s），不能阻塞 UI 线程（L-127）。</summary>
    public async Task<string> GetGameNoticeUrlAsync(GameBiz biz, CancellationToken cancellationToken = default)
    {
        long uid = await GetLastUidAsync(biz, cancellationToken).ConfigureAwait(false);
        return GameNoticeClient.GetGameNoticeUrl(biz, uid, CultureInfo.CurrentUICulture.Name);
    }




    public async Task<bool> IsNoticeAlertAsync(GameBiz gameBiz, CancellationToken cancellationToken = default)
    {
        long uid = await GetLastUidAsync(gameBiz, cancellationToken).ConfigureAwait(false);
        if (uid is 0)
        {
            return false;
        }
        return await _gameNoticeClient.IsNoticeAlertAsync(gameBiz, uid, CultureInfo.CurrentUICulture.Name, cancellationToken);
    }




    private async Task<long> GetLastUidAsync(GameBiz gameBiz, CancellationToken cancellationToken)
    {
        var role = _gameRecordService.GetLastSelectGameRecordRoleOrTheFirstOne(gameBiz);
        if (role is not null)
        {
            return role.Uid;
        }
        return await GetUidFromRegistryAsync(gameBiz, cancellationToken).ConfigureAwait(false);
    }




    private static async Task<long> GetUidFromRegistryAsync(GameBiz gameBiz, CancellationToken cancellationToken)
    {
        string key = gameBiz.GetGameRegistryKey();
        // 每个游戏读的是同一个键，GetKeyValuesAsync 一次 fork 取回全部值（L-127）。
        if (gameBiz.Game is GameBiz.bh3)
        {
            if (await WineRegistry.GetValueAsync(key, "GENERAL_DATA_V2_LastLoginUserId_h47158221", null, cancellationToken).ConfigureAwait(false) is int uid)
            {
                return uid;
            }
        }
        else if (gameBiz.Game is GameBiz.hk4e)
        {
            if (await WineRegistry.GetValueAsync(key, "__LastUid___h2153286551", null, cancellationToken).ConfigureAwait(false) is byte[] bytes)
            {
                string str = Encoding.UTF8.GetString(bytes).Trim();
                if (long.TryParse(str, out long uid))
                {
                    return uid;
                }
            }
        }
        else if (gameBiz.Game is GameBiz.hkrpg)
        {
            if (await WineRegistry.GetValueAsync(key, "App_LastUserID_h2841727341", null, cancellationToken).ConfigureAwait(false) is int uid)
            {
                return uid;
            }
        }
        // 绝区零（nap）等没有已知的 uid 键，直接返回 0，不碰注册表——这也是「切到绝区零秒切」的原因。
        return 0;
    }





}
