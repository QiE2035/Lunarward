// Lunarward：游戏 ↔ 运行配置（Profile）的绑定（D28）。
// 单独存一份 JSON，不写进 AppConfig 的 SQLite 设置表（那份表是上游契约，尽量不改动）。
using Starward.Helpers;
using Starward.Core;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Starward.Features.Runtime;


public static class ProfileBinding
{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };


    /// <summary>诊断包用：把「游戏 → 方案 id」的绑定关系原样导出。</summary>
    public static string Describe()
    {
        try
        {
            return JsonSerializer.Serialize(Load(), new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return "读取绑定失败：" + ex;
        }
    }


    private static string FilePath
    {
        get
        {
            string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } x
                ? x
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(config, "lunarward", "profile-bindings.json");
        }
    }


    /// <summary>最近一次读取失败的原因（页面据此提示；null = 正常）。</summary>
    public static string? LastError { get; private set; }


    /// <summary>
    /// R09：读失败不再当成空字典。坏文件原地保留（不静默覆盖），先尝试 <c>.bak</c> 恢复；
    /// 调用方通过返回值区分「没有绑定」和「绑定文件坏了」。（D104）
    /// </summary>
    private static bool TryLoad(out Dictionary<string, string> map, out string? error)
    {
        map = new Dictionary<string, string>(StringComparer.Ordinal);
        error = null;
        LastError = null;
        try
        {
            if (!File.Exists(FilePath))
            {
                return true;
            }
            string json = File.ReadAllText(FilePath);
            Dictionary<string, string>? parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed is null)
            {
                error = "绑定文件内容为空";
            }
            else
            {
                map = new Dictionary<string, string>(parsed, StringComparer.Ordinal);
                return true;
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Serilog.Log.Warning(ex, "Read game profile bindings failed: {file}", FilePath);
        }
        // 主文件坏了：先试 .bak（JsonFile 每次保存都会留一份）
        try
        {
            string bak = FilePath + ".bak";
            if (File.Exists(bak))
            {
                Dictionary<string, string>? parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(bak));
                if (parsed is not null)
                {
                    map = new Dictionary<string, string>(parsed, StringComparer.Ordinal);
                    Serilog.Log.Warning("Recovered game profile bindings from {bak}", bak);
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Read game profile bindings backup failed: {file}", FilePath + ".bak");
        }
        LastError = error ?? "绑定文件无法解析";
        return false;
    }


    private static Dictionary<string, string> Load()
    {
        // 读给展示/统计用：坏了就返回空并在 LastError 里留原因，但绝不写回（Set 会拒绝覆盖）。
        TryLoad(out Dictionary<string, string> map, out _);
        return map;
    }


    public static string? Get(GameBiz biz) => Load().GetValueOrDefault(biz.Value);


    /// <summary>
    /// 写绑定。R09：原文件损坏且没有可用备份时直接抛错，避免「只写进一条」的字典把其余绑定抹掉。
    /// </summary>
    public static void Set(GameBiz biz, string? profileId)
    {
        if (!TryLoad(out Dictionary<string, string> map, out string? error))
        {
            throw new InvalidOperationException($"游戏绑定文件已损坏，已保留原文件；请修复或删除 {FilePath}（{error}）");
        }
        if (string.IsNullOrWhiteSpace(profileId))
        {
            map.Remove(biz.Value);
        }
        else
        {
            map[biz.Value] = profileId;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        JsonFile.WriteAtomic(FilePath, map, JsonOptions);
    }


    /// <summary>哪些游戏正绑定着这份配方（§4.2）：删配方前必须列出来，不能静默留下悬空绑定。</summary>
    /// <summary>已绑定的游戏数量（概览页统计用，不暴露字典本体）。</summary>
    public static int BindingCount() => Load().Count;


    /// <summary>绑定指向的方案已经不存在（换机、迁移、手删 json 之后都会这样）。
    /// 启动时会静默回落到第一份方案（L-62），所以这里要能被界面看见。</summary>
    public static IReadOnlyList<(GameBiz Biz, string ProfileId)> Dangling(IEnumerable<string> existingIds)
    {
        var alive = new HashSet<string>(existingIds ?? [], StringComparer.OrdinalIgnoreCase);
        var result = new List<(GameBiz, string)>();
        foreach (KeyValuePair<string, string> pair in Load())
        {
            if (string.IsNullOrWhiteSpace(pair.Value) || alive.Contains(pair.Value))
            {
                continue;
            }
            result.Add((new GameBiz(pair.Key), pair.Value));
        }
        result.Sort((a, b) => string.CompareOrdinal(a.Item1.Value, b.Item1.Value));
        return result;
    }


    public static IReadOnlyList<GameBiz> Usages(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return [];
        }
        return Load()
            .Where(x => string.Equals(x.Value, profileId, StringComparison.Ordinal))
            .Select(x => new GameBiz(x.Key))
            .OrderBy(x => x.ToGameName(), StringComparer.CurrentCulture)
            .ToList();
    }


    /// <summary>清掉所有指向这份配方的绑定，返回清掉的条数（删配方时调用，与 §4.2 一致）。</summary>
    public static int ClearFor(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return 0;
        }
        Dictionary<string, string> map = Load();
        List<string> keys = map.Where(x => string.Equals(x.Value, profileId, StringComparison.Ordinal))
            .Select(x => x.Key)
            .ToList();
        if (keys.Count == 0)
        {
            return 0;
        }
        foreach (string key in keys)
        {
            map.Remove(key);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        JsonFile.WriteAtomic(FilePath, map, JsonOptions);
        return keys.Count;
    }


    /// <summary>
    /// 首次使用（§5）：给出这份游戏该用的配方，必要时建一份同名配方并立刻绑定。
    /// 三种情况——①绑定有效：直接用；②有同名配方：绑过去；③都没有：沿用当前默认配方的运行器/图形层
    /// 建一份同名配方（前缀留空 = 拿自己的默认目录，避免两份环境互相踩锁），并绑定。
    /// 只在「唯一那份配方没有任何游戏在用」时就地改名，不制造第二份。
    /// </summary>
    public static (RuntimeProfile? Profile, bool Created, bool Bound) EnsureFor(GameBiz? biz = null, ILogger? logger = null)
    {
        IReadOnlyList<RuntimeProfile> all = RuntimeProfileStore.LoadAll(logger);
        if (all.Count == 0)
        {
            return (null, false, false);
        }
        if (biz is not { } b || string.IsNullOrWhiteSpace(b.Value))
        {
            return (all[0], false, false);
        }
        string? boundId = Get(b);
        if (!string.IsNullOrWhiteSpace(boundId))
        {
            RuntimeProfile? bound = all.FirstOrDefault(p => p.Id == boundId);
            if (bound is not null)
            {
                return (bound, false, false);
            }
        }
        string gameName = b.ToGameName();
        if (string.IsNullOrWhiteSpace(gameName))
        {
            gameName = b.Value;
        }
        string sanitized = ComponentNaming.Sanitize(gameName).Trim();
        RuntimeProfile? named = all.FirstOrDefault(p =>
            string.Equals(p.Name.Trim(), gameName.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(p.Name.Trim(), sanitized, StringComparison.OrdinalIgnoreCase));
        if (named is not null)
        {
            Set(b, named.Id);
            logger?.LogInformation("游戏 {biz} 首次使用，绑定同名环境：{name}（{id}）", b.Value, named.Name, named.Id);
            return (named, false, true);
        }
        RuntimeProfile fallback = all[0];
        if (all.Count == 1 && Usages(fallback.Id).Count == 0)
        {
            // 只有一份没人用的配方（多半是 LoadAll 自动建的默认那份）：就地改名并绑定，不制造第二份
            string oldName = fallback.Name;
            fallback.Name = gameName;
            RuntimeProfileStore.Save(fallback, logger);
            Set(b, fallback.Id);
            logger?.LogInformation("默认环境改名为 {name}（原 {old}）并绑定到 {biz}（id {id}）", gameName, oldName, b.Value, fallback.Id);
            return (fallback, true, true);
        }
        RuntimeProfile created = new()
        {
            Name = gameName,
            RunnerId = fallback.RunnerId,
            RunnerPath = fallback.RunnerPath,
            DxvkId = fallback.DxvkId,
            Vkd3dId = fallback.Vkd3dId,
            Env = new Dictionary<string, string>(fallback.Env),
            Args = fallback.Args,
            Wrappers = new List<string>(fallback.Wrappers),
            PrefixArch = fallback.PrefixArch,
            Shell = fallback.Shell,
            GameOutput = fallback.GameOutput,
            Notes = $"首次启动自动创建（沿用「{fallback.Name}」的运行器与图形层）",
        };
        RuntimeProfileStore.Save(created, logger);
        Set(b, created.Id);
        logger?.LogInformation("为 {biz} 新建环境 {name}（{id}）", b.Value, created.Name, created.Id);
        return (created, true, true);
    }


    /// <summary>取该游戏使用的 Profile；未绑定过则回落到第一个（默认）配置。</summary>
    public static RuntimeProfile? Resolve(GameBiz? biz = null)
    {
        IReadOnlyList<RuntimeProfile> all = RuntimeProfileStore.LoadAll();
        if (all.Count == 0)
        {
            return null;
        }
        if (biz is not null && biz.Value is { } b && Get(b) is string id)
        {
            RuntimeProfile? bound = all.FirstOrDefault(p => p.Id == id);
            if (bound is not null)
            {
                return bound;
            }
            // 绑定指向的配置被删了：以前会静默用「第一个配置」启动，等于换了个 wine/前缀还毫无察觉（L-62）。
            // D86：只在第一次提示。这条路径每次启动都会走到（回落是对的），但日志不该每次刷一遍；
            // 用户之后可以在「运行环境 → 方案」页的提示卡里看到并修复。
            if (Helpers.OneShotHints.TryTake("profile-binding-dangling"))
            {
                Serilog.Log.Warning("Profile binding for {biz} points to missing profile {id}; falling back to {fallback}. "
                                    + "Re-bind it in the game settings.", b, id, all[0].Name);
            }
        }
        return all[0];
    }

}
