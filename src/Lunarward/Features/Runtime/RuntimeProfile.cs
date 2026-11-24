// Lunarward：运行环境（Environment，即「配方」）数据模型（D73 §4）。
//
// 字段：name / runnerId / dxvkId / vkd3dId / prefixPath / env / args / wrappers / notes（D83 把图形层拆成两个）。
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

    public int SchemaVersion { get; set; } = 4;


    public string Id { get; set; } = Guid.NewGuid().ToString("n")[..8];


    /// <summary>用户可见名称。</summary>
    public string Name { get; set; } = "";


    /// <summary>
    /// R44：前缀目录的稳定名字（首次生成后不再随 <see cref="Name"/> 变）。
    /// 以前 <c>EffectivePrefixPath</c> 直接拿 Name 推导目录，改个名就等于换前缀、旧前缀（DXVK/注册表/字体）变孤儿。
    /// </summary>
    public string? PrefixSlug { get; set; }


    /// <summary>1) 引用的运行器组件 id（真值）。</summary>
    public string? RunnerId { get; set; }


    /// <summary>3a) DXVK 组件 id（d3d9/10/11 → Vulkan）；null = 不装。</summary>
    public string? DxvkId { get; set; }


    /// <summary>3b) VKD3D-Proton 组件 id（d3d12 → Vulkan）；null = 不装。与 <see cref="DxvkId"/> 互不影响。</summary>
    public string? Vkd3dId { get; set; }


    /// <summary>R41：旧版（D73 之前）的 graphicsId，仅用于迁移；迁移后置空。</summary>
    [JsonPropertyName("graphicsId")]
    public string? LegacyGraphicsId { get; set; }


    /// <summary>当前选中的图形组件 id（两类各自独立，这里只用于「有没有配图形层」这种粗判断）。</summary>
    [JsonIgnore]
    public bool HasGraphics => !string.IsNullOrWhiteSpace(DxvkId) || !string.IsNullOrWhiteSpace(Vkd3dId);


    /// <summary>4) 前缀目录（真值）；空则落到 <c>~/.local/share/lunarward/prefix/&lt;name&gt;</c>（与 PrefixManager.PrefixRoot 一致）。</summary>
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


    /// <summary>HUD（D81）：off / dxvk / mangohud。展开成 DXVK_HUD 或 MANGOHUD 环境变量。</summary>
    public string Hud { get; set; } = "off";


    /// <summary>HUD 的自定义参数；空则用各家的默认值。</summary>
    public string HudOptions { get; set; } = "";


    /// <summary>用 gamemoderun 包一层（D81）。</summary>
    public bool GameMode { get; set; }


    /// <summary>用 gamescope 包一层（D81）：它必须是最外层，且开着时不注入 MANGOHUD，避免两层叠加。</summary>
    public bool GameScope { get; set; }


    /// <summary>gamescope 的参数；空则用 <c>-f</c>。</summary>
    public string GameScopeArgs { get; set; } = "";


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
