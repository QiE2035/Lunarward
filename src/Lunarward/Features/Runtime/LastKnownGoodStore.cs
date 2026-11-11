// Lunarward：记录每个方案「上次能跑起来」的组合，供一键切回（D58：先做出来体验，给一条退路）。
//
// 只记事实，不做判断：哪次启动成功了，就把当时的运行器路径/版本、前缀路径、时间写下来。
// 换运行器导致环境坏了，用户可以把这一条切回去，而不用回忆「之前用的是哪个 wine」。
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Starward.Features.Runtime;


public sealed class LastGoodEntry
{

    /// <summary>方案 Id（RuntimeProfile.Id）。</summary>
    public string ProfileId { get; set; } = "";

    /// <summary>当时用的运行器可执行文件路径。</summary>
    public string? RunnerPath { get; set; }

    /// <summary>当时的版本串（`wine --version`，取不到就空）。</summary>
    public string RunnerVersion { get; set; } = "";

    /// <summary>当时使用的前缀路径（可能为空：external 且用户没填）。</summary>
    public string? PrefixPath { get; set; }

    /// <summary>这次成功启动的时间。</summary>
    public DateTimeOffset At { get; set; }

}


public static class LastKnownGoodStore
{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };


    /// <summary>与 profiles 目录同级：$XDG_CONFIG_HOME|~/.config/lunarward/last-good.json</summary>
    public static string FilePath
    {
        get
        {
            string folder = Path.GetDirectoryName(RuntimeProfileStore.ProfilesFolder) ?? RuntimeProfileStore.ProfilesFolder;
            return Path.Combine(folder, "last-good.json");
        }
    }


    private static Dictionary<string, LastGoodEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new Dictionary<string, LastGoodEntry>(StringComparer.Ordinal);
            }
            string json = File.ReadAllText(FilePath);
            Dictionary<string, LastGoodEntry>? map = JsonSerializer.Deserialize<Dictionary<string, LastGoodEntry>>(json, JsonOptions);
            return map is null
                ? new Dictionary<string, LastGoodEntry>(StringComparer.Ordinal)
                : new Dictionary<string, LastGoodEntry>(map, StringComparer.Ordinal);
        }
        catch
        {
            // 文件坏了就当没有记录：这个文件只用于「切回去」，不该阻塞任何流程
            return new Dictionary<string, LastGoodEntry>(StringComparer.Ordinal);
        }
    }


    /// <summary>取某个方案上次成功启动时的组合。</summary>
    public static LastGoodEntry? Get(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }
        return Load().TryGetValue(profileId, out LastGoodEntry? entry) ? entry : null;
    }


    /// <summary>记录一次成功启动（启动进程真的起来了再调用）。</summary>
    public static void Remember(RuntimeProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            return;
        }
        Dictionary<string, LastGoodEntry> map = Load();
        map[profile.Id] = new LastGoodEntry
        {
            ProfileId = profile.Id,
            RunnerPath = profile.RunnerPath,
            RunnerVersion = string.IsNullOrWhiteSpace(profile.RunnerPath) ? "" : ComponentStore.ReadRunnerVersion(profile.RunnerPath) ?? "",
            PrefixPath = profile.PrefixPath,
            At = DateTimeOffset.Now,
        };
        try
        {
            JsonFile.WriteAtomic(FilePath, JsonSerializer.Serialize(map, JsonOptions));
        }
        catch (Exception ex)
        {
            // 写不进去也不算启动失败
            Serilog.Log.Debug(ex, "last-good: save failed");
        }
    }


    /// <summary>清掉某个方案的记录（可选动作，用于「忘记上次可用」）。</summary>
    public static void Clear(string? profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }
        Dictionary<string, LastGoodEntry> map = Load();
        if (map.Remove(profileId))
        {
            try
            {
                JsonFile.WriteAtomic(FilePath, JsonSerializer.Serialize(map, JsonOptions));
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "last-good: save failed");
            }
        }
    }


    /// <summary>格式化给界面看的一句摘要。</summary>
    public static string Describe(LastGoodEntry? entry)
    {
        if (entry is null)
        {
            return "";
        }
        string runner = string.IsNullOrWhiteSpace(entry.RunnerPath) ? "(未记录路径)" : entry.RunnerPath!;
        string version = string.IsNullOrWhiteSpace(entry.RunnerVersion) ? "" : $" · {entry.RunnerVersion}";
        return $"{runner}{version} · {entry.At:yyyy-MM-dd HH:mm}";
    }

}
