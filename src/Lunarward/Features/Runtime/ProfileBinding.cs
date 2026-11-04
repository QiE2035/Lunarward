// Lunarward：游戏 ↔ 运行配置（Profile）的绑定（D28）。
// 单独存一份 JSON，不写进 AppConfig 的 SQLite 设置表（那份表是上游契约，尽量不改动）。
using Starward.Core;
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


    private static Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? new();
            }
        }
        catch
        {
        }
        return new();
    }


    public static string? Get(GameBiz biz) => Load().GetValueOrDefault(biz.Value);


    public static void Set(GameBiz biz, string? profileId)
    {
        Dictionary<string, string> map = Load();
        if (string.IsNullOrWhiteSpace(profileId))
        {
            map.Remove(biz.Value);
        }
        else
        {
            map[biz.Value] = profileId;
        }
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(map, JsonOptions));
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
        }
        return all[0];
    }

}
