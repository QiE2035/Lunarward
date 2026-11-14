// Lunarward：运行环境（Environment，即「配方」）数据模型（D73 §4）。
//
// 八个字段：name / runnerId / graphicsId / prefixPath / env / args / wrappers / notes。
// 「前缀模式」不再是配置项——前缀就是 prefixPath 一个路径字段（D73 §3.1），
// 两个配方指向同一路径就是共享，用完删就是临时。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;

namespace Starward.Features.Runtime;


public sealed class RuntimeProfile
{

    public int SchemaVersion { get; set; } = 2;


    public string Id { get; set; } = Guid.NewGuid().ToString("n")[..8];


    /// <summary>用户可见名称。</summary>
    public string Name { get; set; } = "";


    /// <summary>1) 引用的运行器组件 id（真值）。</summary>
    public string? RunnerId { get; set; }


    /// <summary>3) 图形组件 id；null = 不装图形层。</summary>
    public string? GraphicsId { get; set; }


    /// <summary>4) 前缀目录（真值）；空则落到 <c>~/.local/share/lunarward/prefixes/&lt;name&gt;</c>。</summary>
    public string? PrefixPath { get; set; }


    /// <summary>5) 配方 env：覆盖运行器自动注入的同名变量，覆盖会在界面标红（D73 §4.3）。</summary>
    [JsonPropertyName("env")]
    public Dictionary<string, string> Env { get; set; } = new();


    /// <summary>6) 启动参数，原样拼在游戏 exe 之后。</summary>
    public string Args { get; set; } = "";


    /// <summary>7) 命令包装：按顺序拼在 <c>wine</c> 前面，每项可以是「带参数的一条命令」。</summary>
    public List<string> Wrappers { get; set; } = new();


    /// <summary>8) 备注。</summary>
    public string? Notes { get; set; }


    /// <summary>新建前缀时选定的架构（只读展示，建完不能改）。</summary>
    public string PrefixArch { get; set; } = "win64";


    /// <summary>调试 Shell 使用的 shell 程序（Linux 补充功能，不在八字段内）。</summary>
    public string Shell { get; set; } = "";


    /// <summary>游戏本体输出可见性（D27）：silent / panel / terminal。</summary>
    public string GameOutput { get; set; } = "silent";


    /// <summary>
    /// 运行器路径缓存。<see cref="RunnerId"/> 才是真值；这里由
    /// <see cref="EnvironmentResolver.Refresh"/> 在读取配置/换运行器时刷新，
    /// 目的是让 UI 绑定和已有调用点不必在属性里扫盘（D72：UI 线程不做阻塞 IO）。
    /// </summary>
    public string? RunnerPath { get; set; }


    // ---- 以下字段只为读旧配置存在，迁移后会被清空或忽略（D73 阶段 2）----

    /// <summary>旧字段：template | dedicated | shared | external | ephemeral。</summary>
    [JsonPropertyName("PrefixMode")]
    public string? LegacyPrefixMode { get; set; }


    /// <summary>旧字段：与 <see cref="GraphicsId"/> 冗余的开关（null = 已迁移完毕，不再写盘）。</summary>
    [JsonPropertyName("EnsureGraphics")]
    public bool? LegacyEnsureGraphics { get; set; }


    /// <summary>旧字段：wine | proton | umu | custom。</summary>
    [JsonPropertyName("RunnerType")]
    public string? LegacyRunnerType { get; set; }


    /// <summary>旧字段：环境变量字典的旧键名。</summary>
    [JsonPropertyName("Environment")]
    public Dictionary<string, string>? LegacyEnvironment { get; set; }


    /// <summary>
    /// 把旧字段（PrefixMode / Environment / EnsureGraphics / RunnerType）归一到新字段。
    /// 纯内存操作，方便单测；前缀路径规范化与运行器刷新由 <see cref="RuntimeProfileStore"/> 负责。
    /// 返回 true 表示改过东西，需要回写。
    /// </summary>
    internal bool MigrateFromLegacy()
    {
        bool changed = false;

        if (LegacyEnvironment is { Count: 0 })
        {
            LegacyEnvironment = null;
            changed = true;
        }
        if (LegacyEnvironment is { Count: > 0 })
        {
            foreach ((string key, string value) in LegacyEnvironment)
            {
                Env.TryAdd(key, value);
            }
            LegacyEnvironment = null;
            changed = true;
        }

        // 旧的「自动安装图形层」开关已并入 GraphicsId（D64）
        if (LegacyEnsureGraphics is false && !string.IsNullOrWhiteSpace(GraphicsId))
        {
            GraphicsId = null;
            changed = true;
        }
        if (LegacyEnsureGraphics is not null)
        {
            LegacyEnsureGraphics = null;
            changed = true;
        }

        // 前缀模式取消（D73 §3.1）：前缀只是路径
        if (!string.IsNullOrWhiteSpace(LegacyPrefixMode))
        {
            string mode = LegacyPrefixMode!.Trim().ToLowerInvariant();
            if (mode is "template" or "ephemeral")
            {
                // 这两种模式的 Path 指向「模板」，直接当实例用会让游戏跑进模板里
                if (!string.IsNullOrWhiteSpace(PrefixPath))
                {
                    Notes = string.IsNullOrWhiteSpace(Notes)
                        ? $"原 {mode} 模式的模板前缀：{PrefixPath}"
                        : Notes;
                }
                PrefixPath = null;
            }
            LegacyPrefixMode = null;
            changed = true;
        }

        if (LegacyRunnerType is not null)
        {
            LegacyRunnerType = null;
            changed = true;
        }

        if (SchemaVersion < 2)
        {
            SchemaVersion = 2;
            changed = true;
        }
        return changed;
    }


    [JsonIgnore]
    public IReadOnlyDictionary<string, string> EnvReadOnly => Env;


    [JsonIgnore]
    public string Summary
    {
        get
        {
            string runner = string.IsNullOrWhiteSpace(RunnerPath) ? "未选择运行器" : RunnerName;
            string prefix = string.IsNullOrWhiteSpace(PrefixPath) ? "前缀：启动时创建" : $"前缀：{ShortenHome(PrefixPath!)}";
            return $"{runner}  ·  {prefix}";
        }
    }


    [JsonIgnore]
    public string NameAndPrefix => $"{Name}  ·  {PrefixText}";


    /// <summary>卡片第三行：前缀的缩写路径。</summary>
    [JsonIgnore]
    public string PrefixText => string.IsNullOrWhiteSpace(PrefixPath) ? "（启动时创建）" : ShortenHome(PrefixPath!);


    [JsonIgnore]
    public string WrappersText => Wrappers.Count == 0 ? "" : string.Join(" ", Wrappers);


    [JsonIgnore]
    public string ArgsText => Args;


    private static string ShortenHome(string path)
    {
        string? home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && path.StartsWith(home, StringComparison.Ordinal))
        {
            return "~" + path[home.Length..];
        }
        return path;
    }


    [JsonIgnore]
    public string RunnerName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(RunnerPath))
            {
                return string.IsNullOrWhiteSpace(RunnerId) ? "（未选择）" : RunnerId!;
            }
            try
            {
                // Proton: .../Proton - Experimental/files/bin/wine → Proton - Experimental
                DirectoryInfo? bin = new FileInfo(RunnerPath!).Directory;
                DirectoryInfo? files = bin?.Parent;
                DirectoryInfo? root = files?.Parent;
                if (root is not null && string.Equals(files!.Name, "files", StringComparison.OrdinalIgnoreCase))
                {
                    return root.Name;
                }
                return Path.GetFileName(RunnerPath!);
            }
            catch (Exception)
            {
                return RunnerPath!;
            }
        }
    }


    /// <summary>环境变量冲突：配方覆盖了运行器自动注入的值（D73 §4.3 要标红）。</summary>
    [JsonIgnore]
    public HashSet<string> OverriddenKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

}
