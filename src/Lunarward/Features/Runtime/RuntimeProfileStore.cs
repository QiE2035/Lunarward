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


    /// <summary>
    /// 配方 Id 只允许字母/数字/下划线/短横（D104/R11）：Id 要拼进文件名，带 <c>/</c> 或 <c>..</c>
    /// 的 Id 会把保存/删除写到目录外，或者让两个不同 Id 指向同一个文件。
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex IdPattern = new("^[A-Za-z0-9_-]{1,32}$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);


    public static bool IsValidId(string? id) => !string.IsNullOrEmpty(id) && IdPattern.IsMatch(id);


    private static string ProfileFile(string id)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentException($"非法的运行环境 Id：{id}", nameof(id));
        }
        string file = Path.GetFullPath(Path.Combine(EnvironmentsFolder, id + ".json"));
        string folder = Path.GetFullPath(EnvironmentsFolder) + Path.DirectorySeparatorChar;
        if (!file.StartsWith(folder, StringComparison.Ordinal))
        {
            throw new ArgumentException($"运行环境路径越界：{id}", nameof(id));
        }
        return file;
    }


    /// <summary>读取全部配方；为空时创建一份基于系统现状的默认配方。</summary>
    public static List<RuntimeProfile> LoadAll(ILogger? logger = null)
    {
        // R24：只有「第一次跑」才自动建默认方案；用户删光所有方案后不该又被凭空造回来。
        bool firstRun = !Directory.Exists(EnvironmentsFolder);
        Directory.CreateDirectory(EnvironmentsFolder);
        MigrateLegacyProfiles(logger);

        var profiles = new List<RuntimeProfile>();
        foreach (string file in Directory.EnumerateFiles(EnvironmentsFolder, "*.json"))
        {
            try
            {
                RuntimeProfile? profile = JsonSerializer.Deserialize<RuntimeProfile>(File.ReadAllText(file), JsonOptions);
                if (profile is not null)
                {
                    bool changed = Normalize(profile, logger);
                    // R11：文件名才是落盘真理。Id 与文件名不一致（手改、复制文件）时以文件名为准，
                    // 否则下一次保存会写出第二份同名 Id 的文件，删除只删其中一份。
                    string fileId = Path.GetFileNameWithoutExtension(file);
                    if (IsValidId(fileId) && !string.Equals(profile.Id, fileId, StringComparison.Ordinal))
                    {
                        logger?.LogWarning("Runtime environment id mismatch, using file name: {fileId} (was {id})", fileId, profile.Id);
                        profile.Id = fileId;
                        changed = true;
                    }
                    else if (!IsValidId(profile.Id))
                    {
                        profile.Id = Guid.NewGuid().ToString("n")[..8];
                        logger?.LogWarning("Runtime environment id invalid, regenerated: {id}", profile.Id);
                        changed = true;
                    }
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
        if (profiles.Count == 0 && firstRun)
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
        // R45：JSON 里显式 null 会覆盖属性初始化器（反序列化不报错），故障延后到启动/枚举时变成 NRE。
        if (profile.Env is null)
        {
            profile.Env = new Dictionary<string, string>(StringComparer.Ordinal);
            changed = true;
        }
        if (profile.Wrappers is null)
        {
            profile.Wrappers = new List<string>();
            changed = true;
        }
        if (profile.Name is null)
        {
            profile.Name = "";
            changed = true;
        }
        // R44：老配置没有 PrefixSlug，按「原来那套按名字推导」补一个——升级后目录不变；
        // 之后改名只改显示名，前缀目录跟 slug 走。
        if (string.IsNullOrWhiteSpace(profile.PrefixSlug))
        {
            string slug = ComponentNaming.Sanitize(profile.Name).Trim().ToLowerInvariant();
            profile.PrefixSlug = string.IsNullOrWhiteSpace(slug) ? profile.Id : slug;
            changed = true;
        }
        if (!string.IsNullOrWhiteSpace(profile.PrefixPath))
        {
            string? normalizedPrefix = PrefixManager.ExpandPath(profile.PrefixPath);
            if (!string.Equals(normalizedPrefix, profile.PrefixPath, StringComparison.Ordinal))
            {
                profile.PrefixPath = normalizedPrefix;
                changed = true;
            }
        }
        // R41：旧版（D73 之前）只有 graphicsId，一个 id 同时在装 DXVK/VKD3D；按组件的 Kind 迁移成两列。
        if (!string.IsNullOrWhiteSpace(profile.LegacyGraphicsId))
        {
            string legacy = profile.LegacyGraphicsId!;
            Component? component = ComponentStore.GetGraphics().FirstOrDefault(x => x.Id == legacy);
            if (component is not null)
            {
                if (component.Kind == ComponentKind.Vkd3dProton)
                {
                    profile.Vkd3dId = component.Id;
                }
                else
                {
                    profile.DxvkId = component.Id;
                }
                logger?.LogInformation("Migrated legacy graphics id {id} -> DXVK={dxvk} VKD3D={vkd3d}", legacy, profile.DxvkId, profile.Vkd3dId);
            }
            else
            {
                logger?.LogWarning("Legacy graphics id {id} not found, cleared; please pick a graphics component again", legacy);
            }
            profile.LegacyGraphicsId = null;
            changed = true;
        }
        // RunnerPath 只是缓存：以 RunnerId 为准，读配置时不在这里 fork `wine --version`（R21）。
        EnvironmentResolver.Refresh(profile, ComponentStore.CachedRunners, logger);
        return changed;
    }


    /// <summary>保存一份配方；返回是否真的落盘（R07：调用方要按结果提示，不能无条件弹「已保存」）。</summary>
    public static bool Save(RuntimeProfile profile, ILogger? logger = null)
    {
        try
        {
            // 存盘前规范化前缀路径：带尾斜杠的 WINEPREFIX 会让 wine 认为「不是同一个前缀」
            // 而直接退出（实测 `wine: launching from steam.exe stub!`）。
            profile.PrefixPath = PrefixManager.ExpandPath(profile.PrefixPath);
            Directory.CreateDirectory(EnvironmentsFolder);
            JsonFile.WriteAtomic(ProfileFile(profile.Id), profile, JsonOptions);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Save runtime environment failed: {id}", profile.Id);
            return false;
        }
    }


    /// <summary>删除配方文件；返回是否删除成功（R09：调用方要提示失败，不能弹「已删除」）。</summary>
    public static bool Delete(RuntimeProfile profile, ILogger? logger = null)
    {
        try
        {
            string file = ProfileFile(profile.Id);
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Delete runtime environment failed: {id}", profile.Id);
            return false;
        }
    }


    /// <summary>
    /// R10：旧版本把配方放在 <c>config/lunarward/profiles/</c>，迁移代码曾被删除，升级后等于「配置全丢」。
    /// 这里逐份导入到 <c>env/</c>：全部成功才把旧目录改名存档；有任何一份失败就保留旧目录，下次再试。
    /// </summary>
    private static void MigrateLegacyProfiles(ILogger? logger)
    {
        string legacy = Path.Combine(ConfigHome, "profiles");
        if (!Directory.Exists(legacy))
        {
            return;
        }
        List<string> failed = new();
        foreach (string file in Directory.EnumerateFiles(legacy, "*.json"))
        {
            try
            {
                RuntimeProfile? profile = JsonSerializer.Deserialize<RuntimeProfile>(File.ReadAllText(file), JsonOptions);
                if (profile is null)
                {
                    failed.Add(file);
                    continue;
                }
                profile.Id = Path.GetFileNameWithoutExtension(file);
                if (!IsValidId(profile.Id))
                {
                    profile.Id = Guid.NewGuid().ToString("n")[..8];
                }
                profile.PrefixPath = PrefixManager.ExpandPath(profile.PrefixPath);
                string target = ProfileFile(profile.Id);
                if (!File.Exists(target))
                {
                    JsonFile.WriteAtomic(target, profile, JsonOptions);
                }
                File.Delete(file);
                logger?.LogInformation("Migrated runtime environment: {file} -> {id}", file, profile.Id);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Migrate runtime environment failed: {file}", file);
                failed.Add(file);
            }
        }
        if (failed.Count == 0)
        {
            try
            {
                Directory.Move(legacy, legacy + ".migrated-" + DateTime.Now.ToString("yyyyMMddHHmmss"));
                logger?.LogInformation("Legacy runtime environment folder archived");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Archive legacy runtime environment folder failed");
            }
        }
        else
        {
            logger?.LogWarning("Legacy runtime environment migration incomplete: {count} file(s) failed, folder kept", failed.Count);
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
        string name = string.IsNullOrWhiteSpace(runner?.Name) ? "默认" : runner!.Name;
        return new RuntimeProfile
        {
            Name = name,
            PrefixSlug = string.IsNullOrWhiteSpace(ComponentNaming.Sanitize(name).Trim())
                ? null
                : ComponentNaming.Sanitize(name).Trim().ToLowerInvariant(),
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
