// Lunarward：运行环境（配方）的落盘与迁移（D73 §8）。
// 位置：$XDG_CONFIG_HOME|~/.config/lunarward/env/<id>.json（每份一个文件，删除/分享/回滚粒度自然）。
// 旧位置（profiles/<id>.json，带 PrefixMode 字段）在本文件里一次性迁移过来。
using Microsoft.Extensions.Logging;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;


namespace Starward.Features.Runtime;


public static class RuntimeProfileStore
{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
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


    private static string ConfigHome
    {
        get
        {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } custom
                ? custom
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configHome, "lunarward");
        }
    }


    /// <summary>配方目录（D73 §8 的 <c>env/</c>）。</summary>
    public static string EnvironmentsFolder => Path.Combine(ConfigHome, "env");


    /// <summary>旧配方目录（profiles/），只用于迁移。</summary>
    public static string LegacyProfilesFolder => Path.Combine(ConfigHome, "profiles");


    /// <summary>兼容别名：部分老代码/诊断包仍按这个名字取目录，等价于 <see cref="EnvironmentsFolder"/>。</summary>
    public static string ProfilesFolder => EnvironmentsFolder;


    /// <summary>读取全部配方；为空时创建一份基于系统现状的默认配方。</summary>
    public static List<RuntimeProfile> LoadAll(ILogger? logger = null)
    {
        Directory.CreateDirectory(EnvironmentsFolder);
        MigrateFromLegacyFolder(logger);

        var profiles = new List<RuntimeProfile>();
        foreach (string file in Directory.EnumerateFiles(EnvironmentsFolder, "*.json"))
        {
            try
            {
                RuntimeProfile? profile = JsonSerializer.Deserialize<RuntimeProfile>(File.ReadAllText(file), JsonOptions);
                if (profile is not null)
                {
                    bool changed = MigrateLegacy(profile, logger);
                    profiles.Add(profile);
                    if (changed)
                    {
                        Save(profile, logger);
                    }
                }
            }
            catch (Exception ex)
            {
                // 静默跳过会让人以为「我建的配置丢了」，但目录里其实躺着半截 JSON。
                // 改名留证据，再记一条警告（L-62）。
                logger?.LogWarning(ex, "Load runtime environment failed, moving aside: {file}", file);
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


    /// <summary>
    /// 把旧 <c>profiles/</c> 迁移到 <c>env/</c>：读完即改名，避免下次再迁一遍（也可作为用户回滚的证据）。
    /// </summary>
    private static void MigrateFromLegacyFolder(ILogger? logger)
    {
        try
        {
            if (!Directory.Exists(LegacyProfilesFolder))
            {
                return;
            }
            string[] files = Directory.GetFiles(LegacyProfilesFolder, "*.json");
            int migrated = 0;
            foreach (string file in files)
            {
                string id = Path.GetFileNameWithoutExtension(file);
                string target = Path.Combine(EnvironmentsFolder, id + ".json");
                if (File.Exists(target))
                {
                    continue;
                }
                try
                {
                    RuntimeProfile? profile = JsonSerializer.Deserialize<RuntimeProfile>(File.ReadAllText(file), JsonOptions);
                    if (profile is null)
                    {
                        continue;
                    }
                    MigrateLegacy(profile, logger);
                    JsonFile.WriteAtomic(target, profile, JsonOptions);
                    migrated++;
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Migrate legacy profile failed: {file}", file);
                }
            }
            string archive = LegacyProfilesFolder + ".migrated-" + DateTime.Now.ToString("yyyyMMddHHmmss");
            Directory.Move(LegacyProfilesFolder, archive);
            logger?.LogInformation("Migrated {count} legacy profiles to env/, old folder -> {archive}", migrated, archive);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Migrate legacy profiles folder failed.");
        }
    }


    /// <summary>
    /// 旧字段归一（D73 阶段 2）：模型层的纯内存规则在 <see cref="RuntimeProfile.MigrateFromLegacy"/>，
    /// 这里只补「依赖环境」的两步——前缀路径规范化、运行器解析。
    /// </summary>
    private static bool MigrateLegacy(RuntimeProfile profile, ILogger? logger)
    {
        bool changed = profile.MigrateFromLegacy();
        if (!string.IsNullOrWhiteSpace(profile.PrefixPath))
        {
            string? normalizedPrefix = PrefixManager.ExpandPath(profile.PrefixPath);
            if (!string.Equals(normalizedPrefix, profile.PrefixPath, StringComparison.Ordinal))
            {
                profile.PrefixPath = normalizedPrefix;
                changed = true;
            }
        }
        EnvironmentResolver.Refresh(profile, null, logger);
        return changed;
    }


    public static void Save(RuntimeProfile profile, ILogger? logger = null)
    {
        try
        {
            // 存盘前规范化前缀路径：带尾斜杠的 WINEPREFIX 会让 wine 认为「不是同一个前缀」
            // 而直接退出（实测 `wine: launching from steam.exe stub!`）。
            profile.PrefixPath = PrefixManager.ExpandPath(profile.PrefixPath);
            Directory.CreateDirectory(EnvironmentsFolder);
            string file = Path.Combine(EnvironmentsFolder, profile.Id + ".json");
            JsonFile.WriteAtomic(file, profile, JsonOptions);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Save runtime environment failed: {id}", profile.Id);
        }
    }


    public static void Delete(RuntimeProfile profile, ILogger? logger = null)
    {
        try
        {
            string file = Path.Combine(EnvironmentsFolder, profile.Id + ".json");
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Delete runtime environment failed: {id}", profile.Id);
        }
    }


    /// <summary>按系统现状生成默认配方：优先已发现的运行器，前缀用 LUNARWARD_WINEPREFIX 或 ~/.wine。</summary>
    private static RuntimeProfile CreateDefault()
    {
        Component? runner = ComponentStore.GetRunners().FirstOrDefault(x => x.Usable == true)
            ?? ComponentStore.GetRunners().FirstOrDefault();
        string? prefix = Environment.GetEnvironmentVariable("LUNARWARD_WINEPREFIX");
        if (string.IsNullOrWhiteSpace(prefix))
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string winePrefix = Path.Combine(home, ".wine");
            prefix = Directory.Exists(winePrefix) ? winePrefix : null;
        }
        return new RuntimeProfile
        {
            SchemaVersion = 2,
            Name = string.IsNullOrWhiteSpace(runner?.Name) ? "默认" : runner!.Name,
            RunnerId = runner?.Id,
            RunnerPath = runner?.Path,
            PrefixPath = prefix,
        };
    }

}
