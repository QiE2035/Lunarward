// Lunarward：后启动程序（Companion）条目模型。对应 D24/D28 与 03-runtime-model.md。
using System;
using System.Collections.Generic;
using System.IO;

namespace Starward.Features.Companion;

/// <summary>
/// 一条「后启动程序」：可以只手动启动，也可以伴随游戏启动（companion = true）。
/// 真正的注入/打补丁行为不在本程序内实现，这里只负责「按正确的环境与时机把进程拉起来」。
/// </summary>
public sealed class CompanionEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n")[..8];

    /// <summary>显示名称，例如「XX 帧率解锁」。</summary>
    public string Name { get; set; } = "新条目";

    /// <summary>可执行文件或脚本的绝对路径。</summary>
    public string Path { get; set; } = "";

    /// <summary>附加参数，按空格切分（与游戏启动一致的手写切分规则）。</summary>
    public string Arguments { get; set; } = "";

    /// <summary>工作目录，留空则用 <see cref="Path"/> 所在目录。</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>使用哪个 Profile 的环境；null 表示"跟随游戏当前使用的 Profile"。</summary>
    public string? ProfileId { get; set; }

    /// <summary>额外环境变量，覆盖 Profile 中的同名项。</summary>
    public Dictionary<string, string> Environment { get; set; } = new();

    /// <summary>启动时机：process = 独立进程；script = 交给 shell 执行；inject = 预留（只编排时机，不做注入）。</summary>
    public string Mode { get; set; } = "process";

    /// <summary>
    /// 触发时机（D24/03-runtime-model.md）：
    /// on_game_started = 游戏进程拉起后；after_delay = 延迟 DelayMs 后；on_game_exit = 游戏退出后。
    /// 注入型（inject）同样只按这些时机编排，不实现注入本身。
    /// </summary>
    public string Trigger { get; set; } = "on_game_started";

    /// <summary>伴随启动：满足 Trigger 时自动拉起。默认关闭（D24）。</summary>
    public bool Companion { get; set; }

    /// <summary>启动后延迟多少毫秒再拉起，用于等游戏窗口出现。</summary>
    public int DelayMs { get; set; }

    /// <summary>是否启用（禁用后即使伴随启动也不会拉起）。</summary>
    public bool Enabled { get; set; } = true;

    public string DisplayArguments => string.IsNullOrWhiteSpace(Arguments)
        ? System.IO.Path.GetFileName(Path)
        : $"{System.IO.Path.GetFileName(Path)} {Arguments}";

    public string TriggerText => Trigger switch
    {
        "after_delay" => "游戏启动后延迟",
        "on_game_exit" => "游戏退出后",
        _ => "游戏启动后",
    };


    public string DisplayProfile => string.IsNullOrWhiteSpace(ProfileId) ? "跟随游戏 Profile" : ProfileId;

    public override string ToString() => Name;
}
