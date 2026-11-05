// Lunarward：Profile 的落盘与默认值（D28：设置 → 运行环境（全局））。
// 位置：$XDG_CONFIG_HOME|~/.config/lunarward/profiles/<id>.json
using Starward.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;


using Starward.Language;

namespace Starward.Features.Runtime;


public static class RuntimeProfileStore
{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };


    /// <summary>用户创建的模板前缀存放位置（与实例前缀分开，避免被当成实例清理）。</summary>
    public static string TemplatesFolder
    {
        get
        {
            string data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x
                ? x
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(data, "lunarward", "prefix-templates");
        }
    }


    public static string ProfilesFolder
    {
        get
        {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } custom
                ? custom
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configHome, "lunarward", "profiles");
        }
    }


    /// <summary>读取全部 Profile；为空时创建一个基于系统现状的默认 Profile。</summary>
    public static List<RuntimeProfile> LoadAll(ILogger? logger = null)
    {
        Directory.CreateDirectory(ProfilesFolder);
        var profiles = new List<RuntimeProfile>();
        foreach (string file in Directory.EnumerateFiles(ProfilesFolder, "*.json"))
        {
            try
            {
                RuntimeProfile? profile = JsonSerializer.Deserialize<RuntimeProfile>(File.ReadAllText(file), JsonOptions);
                if (profile is not null)
                {
                    profiles.Add(profile);
                }
            }
            catch (Exception ex)
            {
                // 静默跳过会让人以为「我建的配置丢了」，但目录里其实躺着半截 JSON。
                // 改名留证据，再记一条警告（L-62）。
                logger?.LogWarning(ex, "Load runtime profile failed, moving aside: {file}", file);
                try
                {
                    File.Move(file, $"{file}.corrupt-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
                }
                catch
                {
                }
            }
        }
        if (profiles.Count == 0)
        {
            RuntimeProfile profile = CreateDefault();
            Save(profile, logger);
            profiles.Add(profile);
        }
        return profiles.OrderBy(x => x.Name, StringComparer.CurrentCulture).ToList();
    }


    public static void Save(RuntimeProfile profile, ILogger? logger = null)
    {
        try
        {
            Directory.CreateDirectory(ProfilesFolder);
            string file = Path.Combine(ProfilesFolder, profile.Id + ".json");
            JsonFile.WriteAtomic(file, profile, JsonOptions);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Save runtime profile failed: {id}", profile.Id);
        }
    }


    /// <summary>按系统现状生成默认 Profile：优先 Proton 的 wine，其次 PATH 里的 wine。</summary>
    private static RuntimeProfile CreateDefault()
    {
        string? wine = Features.GameLauncher.GameLauncherService.ResolveWine();
        string? prefix = Environment.GetEnvironmentVariable("LUNARWARD_WINEPREFIX");
        if (string.IsNullOrWhiteSpace(prefix))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string winePrefix = Path.Combine(home, ".wine");
            prefix = Directory.Exists(winePrefix) ? winePrefix : null;
        }
        return new RuntimeProfile
        {
            Name = string.IsNullOrWhiteSpace(wine) ? Lang.Lunarward_Profile_DefaultNameNoWine : string.Format(Lang.Lunarward_Profile_DefaultName, GetRunnerName(wine)),
            RunnerType = wine?.Contains("Proton", StringComparison.OrdinalIgnoreCase) == true ? "proton" : "wine",
            RunnerPath = wine,
            PrefixMode = "external",
            PrefixPath = prefix,
        };
    }


    private static string GetRunnerName(string wine)
    {
        try
        {
            // .../Proton - Experimental/files/bin/wine → Proton - Experimental
            DirectoryInfo? bin = new FileInfo(wine).Directory;
            return bin?.Parent?.Parent?.Name ?? Path.GetFileName(wine);
        }
        catch (Exception)
        {
            return Path.GetFileName(wine);
        }
    }

}
