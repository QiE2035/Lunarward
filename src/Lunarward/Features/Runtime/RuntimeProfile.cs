// Lunarward：运行环境（Profile）数据模型。
// 语义见 docs/linux-port/03-runtime-model.md；M1 只实现「列出 + 打开调试 Shell」，
// 组件管理、前缀克隆、伴随后启动留给 M2（见 12-shims-and-gaps.md）。
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;


using Starward.Language;

namespace Starward.Features.Runtime;


/// <summary>
/// 一套可复用的运行环境组合：runner + 图形组件 + 前缀策略 + 环境变量。
/// </summary>
public sealed class RuntimeProfile
{

    public string Id { get; set; } = Guid.NewGuid().ToString("n")[..8];


    /// <summary>用户可见名称。</summary>
    public string Name { get; set; } = "";


    /// <summary>wine | proton | umu | custom</summary>
    public string RunnerType { get; set; } = "wine";


    /// <summary>wine 可执行文件（proton 时为 .../files/bin/wine）。</summary>
    public string? RunnerPath { get; set; }


    /// <summary>template | dedicated | shared | external | ephemeral</summary>
    public string PrefixMode { get; set; } = "external";


    /// <summary>Wine 前缀路径（template 模式下为模板路径）。</summary>
    public string? PrefixPath { get; set; }


    /// <summary>附加环境变量，覆盖全局默认。</summary>
    public Dictionary<string, string> Environment { get; set; } = new();


    /// <summary>调试 Shell 使用的 shell 程序。</summary>
    public string Shell { get; set; } = "";


    /// <summary>游戏本体的输出可见性（D27）：silent / panel / terminal。捕获始终开启，这里只控制显示。</summary>
    public string GameOutput { get; set; } = "silent";


    /// <summary>选用的图形组件（DXVK / VKD3D-PROTON）的 Id；空表示沿用前缀里已有的。</summary>
    public string? GraphicsId { get; set; }


    /// <summary>启动前把所选图形组件安装/更新到前缀（D31/组件层）。</summary>
    public bool EnsureGraphics { get; set; } = true;


    [JsonIgnore]
    public string Summary
    {
        get
        {
            string runner = string.IsNullOrWhiteSpace(RunnerPath) ? RunnerType : RunnerName;
            string prefix = string.IsNullOrWhiteSpace(PrefixPath) ? PrefixModeText : $"{PrefixModeText} · {ShortenHome(PrefixPath)}";
            return $"{runner}  ·  {prefix}";
        }
    }


    [JsonIgnore]
    public string NameAndPrefix => $"{Name}  ·  {PrefixModeText}";


    private static string ShortenHome(string path)
    {
        string? home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
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
                return RunnerType;
            }
            try
            {
                // Proton: .../Proton - Experimental/files/bin/wine → Proton - Experimental
                DirectoryInfo? bin = new FileInfo(RunnerPath).Directory;
                DirectoryInfo? files = bin?.Parent;
                DirectoryInfo? root = files?.Parent;
                if (root is not null && string.Equals(files!.Name, "files", StringComparison.OrdinalIgnoreCase))
                {
                    return root.Name;
                }
                return Path.GetFileName(RunnerPath);
            }
            catch (Exception)
            {
                return RunnerPath;
            }
        }
    }


    [JsonIgnore]
    public string PrefixModeText => PrefixMode switch
    {
        "template" => Lang.Lunarward_PrefixMode_Template,
        "dedicated" => Lang.Lunarward_PrefixMode_Dedicated,
        "shared" => Lang.Lunarward_PrefixMode_Shared,
        "external" => Lang.Lunarward_PrefixMode_External,
        "ephemeral" => Lang.Lunarward_PrefixMode_Ephemeral,
        _ => PrefixMode,
    };

}
