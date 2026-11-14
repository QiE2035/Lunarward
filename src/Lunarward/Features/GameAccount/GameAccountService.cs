using Dapper;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Features.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameAccount;

public class GameAccountService
{

    private readonly ILogger<GameAccountService> _logger;



    public GameAccountService(ILogger<GameAccountService> logger)
    {
        _logger = logger;
    }



    public GameAccount? GetGameAccountFromRegistry(GameBiz biz)
        => GetGameAccountFromRegistryAsync(biz).GetAwaiter().GetResult();



    /// <summary>
    /// UI 路径必须用这个：读注册表是一次 `wine reg` fork（冷 1 s、热 0.13 s），
    /// 同步版会把 UI 线程卡住（L-127）。整键一次取回，多个值不再重复 fork。
    /// </summary>
    public async Task<GameAccount?> GetGameAccountFromRegistryAsync(GameBiz biz, CancellationToken cancellationToken = default)
    {
        string key = biz.GetGameRegistryKey();
        string keyName = biz.Server switch
        {
            "cn" => GameRegistry.MIHOYOSDK_ADL_PROD_CN_h3123967166,
            "global" => GameRegistry.MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810,
            _ => throw new ArgumentOutOfRangeException($"Unknown region {biz}"),
        };
        var values = await WineRegistry.GetKeyValuesAsync(key, cancellationToken).ConfigureAwait(false);
        values.TryGetValue(keyName, out object? raw);
        if (raw is byte[] adl)
        {
            var account = new GameAccount
            {
                SHA256 = Convert.ToHexString(SHA256.HashData(adl)),
                GameBiz = biz,
                Value = adl,
                Name = "-",
            };
            if (biz.Game is GameBiz.bh3)
            {
                values.TryGetValue(GameRegistry.GENERAL_DATA_V2_LastLoginUserId_h47158221, out object? uidValue);
                if (uidValue is int uid)
                {
                    account.Uid = uid;
                }
            }
            if (biz.Game is GameBiz.hk4e)
            {
                values.TryGetValue(GameRegistry.__LastUid___h2153286551, out object? uidBytesValue);
                if (uidBytesValue is byte[] uidBytes)
                {
                    string uidStr = Encoding.UTF8.GetString(uidBytes).Trim();
                    if (long.TryParse(uidStr, out long uid))
                    {
                        account.Uid = uid;
                    }
                }
            }
            if (biz.Game is GameBiz.hkrpg)
            {
                values.TryGetValue(GameRegistry.App_LastUserID_h2841727341, out object? uidValue);
                if (uidValue is int uid)
                {
                    account.Uid = uid;
                }
            }
            return account;
        }
        return null;
    }



    public IEnumerable<GameAccount> GetGameAccountsFromDatabase(GameBiz biz)
    {
        using var dapper = DatabaseService.CreateConnection();
        var list = dapper.Query<GameAccount>("SELECT * FROM GameAccount WHERE GameBiz = @biz;", new { biz });
        foreach (var item in list)
        {
            item.IsSaved = true;
            yield return item;
        }
    }



    public List<GameAccount> GetGameAccounts(GameBiz biz)
        => GetGameAccountsAsync(biz).GetAwaiter().GetResult();



    public async Task<List<GameAccount>> GetGameAccountsAsync(GameBiz biz, CancellationToken cancellationToken = default)
    {
        var databaseAccounts = GetGameAccountsFromDatabase(biz).ToList();
        var regAccount = await GetGameAccountFromRegistryAsync(biz, cancellationToken).ConfigureAwait(false);
        return MergeAccounts(databaseAccounts, regAccount);
    }



    private static List<GameAccount> MergeAccounts(List<GameAccount> databaseAccounts, GameAccount? regAccount)
    {
        if (regAccount != null)
        {
            if (databaseAccounts.FirstOrDefault(x => x.SHA256 == regAccount.SHA256) is GameAccount ga)
            {
                databaseAccounts.Remove(ga);
                databaseAccounts.Insert(0, ga);
            }
            else
            {
                databaseAccounts.Insert(0, regAccount);
            }
        }
        return databaseAccounts;
    }



    public IEnumerable<int> GetSuggestionUids(GameBiz biz)
    {
        using var dapper = DatabaseService.CreateConnection();
        List<int> uids = dapper.Query<int>("SELECT DISTINCT Uid FROM GameAccount WHERE GameBiz = @biz AND Uid > 0;", new { biz }).ToList();
        return uids.Distinct().Order();
    }



    public void SaveGameAccount(GameAccount account)
    {
        account.Name ??= "";
        account.IsSaved = true;
        using var dapper = DatabaseService.CreateConnection();
        dapper.Execute("INSERT OR REPLACE INTO GameAccount (SHA256, GameBiz, Uid, Name, Value, Time) VALUES (@SHA256, @GameBiz, @Uid, @Name, @Value, @Time);", account);
        _logger.LogInformation("Save account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }



    public void DeleteGameAccount(GameAccount account)
    {
        using var dapper = DatabaseService.CreateConnection();
        dapper.Execute("DELETE FROM GameAccount WHERE SHA256=@SHA256;", account);
        _logger.LogInformation("Delete account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }



    public void ChangeGameAccount(GameAccount account)
        => ChangeGameAccountAsync(account).GetAwaiter().GetResult();



    /// <summary>切换账号：多个值合成一次 `reg import`（原来 2–4 次 fork，每次 0.1–1 s）。</summary>
    public async Task ChangeGameAccountAsync(GameAccount account, CancellationToken cancellationToken = default)
    {
        string key = account.GameBiz.GetGameRegistryKey();
        string keyName = account.GameBiz.Server switch
        {
            "cn" => GameRegistry.MIHOYOSDK_ADL_PROD_CN_h3123967166,
            "global" => GameRegistry.MIHOYOSDK_ADL_PROD_OVERSEA_h1158948810,
            _ => throw new ArgumentOutOfRangeException($"Unknown region {account.GameBiz}"),
        };
        var values = new Dictionary<string, object> { [keyName] = account.Value };
        if (account.GameBiz.Game is GameBiz.hkrpg && account.Uid > 0)
        {
            values[GameRegistry.App_LastUserID_h2841727341] = (int)account.Uid;
        }
        if (account.GameBiz.Game is GameBiz.bh3 && account.Uid > 0)
        {
            values[GameRegistry.GENERAL_DATA_V2_LastLoginUserId_h47158221] = (int)account.Uid;
        }
        if (account.GameBiz.Game is GameBiz.hk4e && account.Uid > 0)
        {
            values[GameRegistry.__LastUid___h2153286551] = Encoding.UTF8.GetBytes($"{account.Uid}\0");
        }
        bool ok = await WineRegistry.SetValuesAsync(key, values, cancellationToken).ConfigureAwait(false);
        if (!ok)
        {
            _logger.LogWarning("Change account {name} ({biz}) 写注册表失败", account.Name, account.GameBiz);
            return;
        }
        _logger.LogInformation("Change account {name} ({biz}) successfully!", account.Name, account.GameBiz);
    }


}
