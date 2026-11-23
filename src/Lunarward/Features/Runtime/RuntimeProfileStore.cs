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


    /// <summary>兼容别名：部分老代码/诊断包仍按这个名字取目录，等价于 <see cref="EnvironmentsFolder"/>。</summary>
    public static string ProfilesFolder => EnvironmentsFolder;


    /// <summary>读取全部配方；为空时创建一份基于系统现状的默认配方。</summary>
    public static List<RuntimeProfile> LoadAll(ILogger? logger = null)
    {
        Directory.CreateDirectory(EnvironmentsFolder);

        var profiles = new List<RuntimeProfile>();
        foreach (string file in Directory.EnumerateFiles(EnvironmentsFolder, "*.json"))
        {
            try
            {
                RuntimeProfile? profile = JsonSerializer.Deserialize<RuntimeProfile>(File.ReadAllText(file), JsonOptions);
                if (profile is not null)
                {
                    bool changed = Normalize(profile, logger);
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
    /// 读入后归一：前缀路径展开、运行器解析（D83 起不再做旧字段迁移——开发阶段直接破坏式改，
    /// 旧 JSON 里的 <c>GraphicsId</c> 会被忽略，方案需要重新选图形层）。
    /// </summary>
    private static bool Normalize(RuntimeProfile profile, ILogger? logger)
    {
        bool changed = false;
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
        Component? runner = null;
        string? preferred = RuntimeDefaults.DefaultRunnerId;
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            // D85：用户可以把某个运行器设为「新方案默认」，这里优先用它
            runner = ComponentStore.GetRunners().FirstOrDefault(x => x.Id == preferred);
        }
        runner ??= ComponentStore.GetRunners().FirstOrDefault(x => x.Usable == true)
            ?? ComponentStore.GetRunners().FirstOrDefault();
        // R06：默认配方不再把 PrefixPath 指向 ~/.wine —— 那会把 DXVK 的 d3d11/dxgi 覆盖进全局前缀、
        // 把侧写写进 ~/.wine/.lunarward，污染其它 wine 应用。空 = 受管默认目录（PrefixRoot/<名字>，D74 第 1 条）。
        // 只有用户显式设了 LUNARWARD_WINEPREFIX 才尊重它。
        string? prefix = Environment.GetEnvironmentVariable("LUNARWARD_WINEPREFIX");
        if (string.IsNullOrWhiteSpace(prefix))
        {
            prefix = null;
        }
        return new RuntimeProfile
        {
            Name = string.IsNullOrWhiteSpace(runner?.Name) ? "默认" : runner!.Name,
            RunnerId = runner?.Id,
            RunnerPath = runner?.Path,
            PrefixPath = prefix,
            // 设计 §4.2 的可见默认值：不做 dll 覆盖的话，wine 首次启动会弹 wine-mono/gecko 安装框。
            Env = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WINEDLLOVERRIDES"] = "mscoree,mshtml=",
            },
        };
    }

}
