// Lunarward：命令行开关的唯一声明表（D79）。
//
// 以前 `--help` 是一段手写字符串，和 `Program.Linux.cs` 里 48 个 `args[0] is "--x"` 分支
// 各写一遍：加开关忘了写帮助（这轮就发生过：`--profile-ensure` 事后补的），
// 或者描述写成旧的（`--preflight` 一直写着「六项」）。
//
// 现在帮助与校验都从这张表出；`scripts/test/lint-cli-help.sh` 再把
// 「源码里的 args[0] 开关 ⊆ 本表」这条不变量卡住。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Starward.Helpers;

/// <summary>一个命令行开关（第一 token 命令，或命令后面的修饰项）。</summary>
internal sealed record CliSwitch(string Name, string Args, string Description, string Group)
{
    /// <summary>`--preflight &lt;biz&gt;` 这样的左边一列。</summary>
    public string Signature => string.IsNullOrEmpty(Args) ? Name : $"{Name} {Args}";
}


/// <summary>
/// 命令行开关表 + 帮助渲染 + 未知开关提示。
/// 只做「声明」这一层：真正的执行分支仍然在 `Program.Linux.cs`（D79 明确不做大重写）。
/// </summary>
internal static class CliSpec
{
    public const string GroupDiagnose = "环境诊断";

    public const string GroupRuntime = "运行环境";

    public const string GroupComponent = "组件与图形层";

    public const string GroupInstall = "安装与更新";

    public const string GroupMisc = "窗口 / 协议 / 其它";

    public const string GroupGui = "界面直跳（开发 / 截图用）";

    public const string GroupModifier = "修饰开关（跟在命令后面）";


    /// <summary>第一 token 命令。</summary>
    public static IReadOnlyList<CliSwitch> Commands { get; } = new CliSwitch[]
    {
        // 环境诊断
        new("--version", "", "打印版本", GroupDiagnose),
        new("--help", "", "打印本帮助（也可用 -h）", GroupDiagnose),
        new("--preflight", "<biz>", "启动前预检：运行身份 / 组件 / 游戏架构 / 前缀 / 图形", GroupDiagnose),
        new("--launch-plan", "<exe> [参数…]", "只解析不启动，打印配方、argv、环境变量与覆盖项", GroupDiagnose),
        new("--profile-ensure", "[biz]", "解析这个游戏的环境绑定，首次使用会自动建或改名", GroupDiagnose),
        new("--diagnose", "[--out DIR] [--game BIZ]", "生成诊断包 zip（默认 ~/Downloads）", GroupDiagnose),
        new("--output-tail", "[行数]", "打印日志尾部（默认 50 行，上限 5000）", GroupDiagnose),
        new("--screenshot", "", "截图并存到设置目录", GroupDiagnose),
        new("--convert-screenshots", "[目录]", "把截图目录里的旧格式转成当前格式", GroupDiagnose),
        new("--image-info", "<图片>", "看某个图片能不能解（Skia / Magick 各报一行）", GroupDiagnose),
        new("--dev-install-probe", "<biz>", "调试：探测安装环境并打印结论", GroupDiagnose),
        new("--dev-set-install-path", "<biz> <目录>", "调试：写死游戏安装目录并保存配置", GroupDiagnose),

        // 运行环境
        new("--list-runners", "[--json]", "列出发现的运行器（布局 / 可用性 / 原因）", GroupRuntime),
        new("--list-components", "[--json]", "列出组件（含图形层）", GroupRuntime),
        new("--prefix-ensure", "<名字|id>", "按方案准备 Wine 前缀", GroupRuntime),
        new("--prefix-template", "<目录>", "把已有前缀登记成模板", GroupRuntime),
        new("--run-once", "<名字|id> <程序> [参数…]", "在方案环境里跑一次程序", GroupRuntime),
        new("--shell-plan", "", "打印每个方案的调试 Shell 命令", GroupRuntime),
        new("--runner-check", "<wine> [前缀]", "检查某个 wine 能不能在这个前缀里跑起来", GroupRuntime),

        // 组件与图形层
        new("--component-scan", "", "列出组件下载源", GroupComponent),
        new("--component-list", "<源 id>", "列出某个源的可用归档（名称 / 大小 / 版本）", GroupComponent),
        new("--component-health", "<id|名字>", "组件健康检查：一句结论 + 可复制的原文", GroupComponent),
        new("--component-import", "<归档>", "导入归档为受管组件（配 --kind）", GroupComponent),
        new("--component-download", "<源 id>", "下载并导入最新版本（分片续传）", GroupComponent),
        new("--graphics-list", "", "列出图形层组件", GroupComponent),
        new("--graphics-install", "<前缀> <名字|id|路径>", "给前缀装图形层", GroupComponent),
        new("--graphics-verify", "<前缀>", "检查前缀里的图形层", GroupComponent),
        new("--graphics-remove", "<前缀>", "移除前缀里的图形层", GroupComponent),
        new("--graphics-import", "<目录>", "把目录里的图形层导入为受管组件", GroupComponent),

        // 安装与更新
        new("--install-plan", "<biz>", "只算不装：打印安装计划", GroupInstall),
        new("--install-run", "<biz> [--limit-mb N]", "执行安装", GroupInstall),
        new("--install-fetch", "<biz> <相对路径>", "只取单个文件（配 --target / --predownload）", GroupInstall),
        new("--install-chunk-plan", "<biz>", "打印 zstd 分块安装计划", GroupInstall),
        new("--install-chunk-run", "<biz>", "执行 zstd 分块安装", GroupInstall),
        new("--update-plan", "<biz>", "打印更新计划", GroupInstall),
        new("--update-run", "<biz> [--limit-mb N]", "执行更新", GroupInstall),
        new("--patch-plan", "<biz>", "打印增量补丁计划", GroupInstall),
        new("--patch-run", "<biz>", "执行增量补丁（配 --apply-to-install / --from）", GroupInstall),
        new("--predownload-size", "<biz>", "预下载体积", GroupInstall),
        new("--install-task", "<biz> <任务>", "安装子任务（走安装管线内部分支）", GroupInstall),
        new("--sophon-probe", "<biz>", "探测 Sophon 服务与分支信息", GroupInstall),
        new("--version-status", "<biz>", "读游戏目录里记录的版本", GroupInstall),
        new("--version-write", "<目录> <版本>", "写游戏版本（三道校验 + 写前备份）", GroupInstall),
        new("--companion-plan", "<biz>", "打印伴随启动计划（后启动程序，不启动游戏）", GroupInstall),
        new("--companion-run", "<biz> <程序> [参数…]", "按伴随启动跑一次", GroupInstall),

        // 窗口 / 协议 / 其它
        new("--hide", "", "启动即隐藏主窗口", GroupMisc),
        new("--show", "", "唤回已运行的主窗口", GroupMisc),
        new("--new-instance", "", "跳过单实例检查", GroupMisc),
        new("--register-protocol", "", "注册 URL 协议", GroupMisc),
        new("--unregister-protocol", "", "取消注册 URL 协议", GroupMisc),
        new("--protocol-status", "", "查看 URL 协议注册状态", GroupMisc),
        new("--database-backup", "<输出.zip>", "把数据库备份成单文件归档（RG-07）", GroupMisc),
        new("--update-check", "", "查询 AUR 版本", GroupMisc),
        new("--url-plan", "<url>", "展开 URL 参数（不启动）", GroupMisc),
        new("--webkit-login", "--url <页> --output <文件>", "登录窗（本程序 + WebKitGTK 6.0，独立进程）", GroupMisc),
        new("--selftest-ui", "", "进程内 UI 自检（11 条断言）", GroupMisc),

        // 开发 / 截图用：直接决定「开窗后落到哪个状态」，由 Features/App/DevSwitches.cs 读。
        new("--page", "<页面>", "启动后直接跳到某个主页", GroupGui),
        new("--subpage", "<子页>", "设置页里直接进入某个子页", GroupGui),
        new("--scroll", "<像素>", "页面加载后滚动到指定位置（截图用）", GroupGui),
        new("--viewer", "<序号>", "截图页加载后打开第 N 张查看器", GroupGui),
        new("--profile-editor", "", "打开第一份运行环境的编辑对话框（截图用）", GroupGui),
        new("--window-action", "minimize|close", "无人值守验证窗口动作", GroupGui),
    };


    /// <summary>命令后面的修饰开关（不作为第一 token 使用）。</summary>
    public static IReadOnlyList<CliSwitch> Modifiers { get; } = new CliSwitch[]
    {
        new("--profile", "<id|名字>", "指定运行环境方案", GroupModifier),
        new("--game", "<biz>", "指定游戏", GroupModifier),
        new("--json", "", "输出 JSON（脚本消费）", GroupModifier),
        new("--out", "<目录/文件>", "输出位置", GroupModifier),
        new("--kind", "wine|graphics", "组件类别", GroupModifier),
        new("--limit-mb", "<N>", "限速（MB/s）或体积上限", GroupModifier),
        new("--target", "<目录>", "目标目录", GroupModifier),
        new("--predownload", "", "只预下载，不落盘安装", GroupModifier),
        new("--apply-to-install", "", "把补丁结果应用到游戏目录", GroupModifier),
        new("--from", "<序号>", "从第几个分块/文件开始", GroupModifier),
        new("--only-file", "<相对路径>", "只处理单个文件", GroupModifier),
        new("--pause-after", "<秒>", "跑够多少秒后自动暂停（联调用）", GroupModifier),
        new("--url", "<url>", "登录页地址（配 --webkit-login）", GroupModifier),
        new("--output", "<文件>", "输出文件（配 --webkit-login）", GroupModifier),
        new("--timeout", "<秒>", "超时秒数", GroupModifier),
        new("--md5", "<值>", "校验值", GroupModifier),
        new("--parallel", "", "并行执行", GroupModifier),
    };


    /// <summary>全部已知开关名（命令 + 修饰）。</summary>
    public static IReadOnlySet<string> Known { get; } = new HashSet<string>(
        Commands.Concat(Modifiers).Select(x => x.Name).Append("-h").Append("-v"),
        StringComparer.Ordinal);


    /// <summary>渲染分组帮助。</summary>
    public static string RenderHelp(string? version = null)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Lunarward —— 米哈游 / HoYoverse 游戏的 Linux 综合启动器（Starward 移植）");
        if (!string.IsNullOrWhiteSpace(version))
        {
            builder.AppendLine(version);
        }
        builder.AppendLine();
        builder.AppendLine("用法：lunarward [命令] [参数]");
        builder.AppendLine("不带命令时启动图形界面。");
        builder.AppendLine();

        foreach (string group in new[] { GroupDiagnose, GroupRuntime, GroupComponent, GroupInstall, GroupMisc, GroupGui, GroupModifier })
        {
            List<CliSwitch> items = Commands.Concat(Modifiers).Where(x => x.Group == group).ToList();
            if (items.Count == 0)
            {
                continue;
            }
            builder.AppendLine(group);
            int width = items.Max(x => DisplayWidth(x.Signature)) + 2;
            int limit = Math.Min(width, 46);
            foreach (CliSwitch item in items)
            {
                builder.Append("  ");
                builder.Append(item.Signature);
                builder.Append(' ', Math.Max(2, limit - DisplayWidth(item.Signature)));
                builder.Append(item.Description);
                builder.AppendLine();
            }
            builder.AppendLine();
        }
        return builder.ToString().TrimEnd() + Environment.NewLine;
    }


    /// <summary>给拼错的开关找最接近的一个（编辑距离 ≤ 4 且长度接近才给）。</summary>
    public static string? Suggest(string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || !token.StartsWith('-'))
        {
            return null;
        }
        string needle = token.Split('=')[0].Trim();
        string? best = null;
        int bestDistance = int.MaxValue;
        foreach (string name in Known)
        {
            int distance = Levenshtein(needle, name);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = name;
            }
        }
        if (best is null || bestDistance > 4)
        {
            return null;
        }
        return best;
    }


    /// <summary>
    /// 未知开关检查：只认第一 token（后面的位置参数是游戏参数，不能拦）。
    /// 返回 true 表示已经打印错误并应当以 <paramref name="exitCode"/> 退出。
    /// </summary>
    public static bool TryRejectUnknown(string[]? args, out int exitCode, out string message)
    {
        exitCode = 0;
        message = "";
        if (args is null || args.Length == 0)
        {
            return false;
        }
        string first = args[0];
        if (!first.StartsWith('-') || Known.Contains(first))
        {
            return false;
        }
        string? suggestion = Suggest(first);
        message = suggestion is null
            ? $"未知开关：{first}\n用 `lunarward --help` 查看全部开关。"
            : $"未知开关：{first}\n最接近的是：{suggestion}\n用 `lunarward --help` 查看全部开关。";
        exitCode = 2;
        return true;
    }


    /// <summary>按终端显示宽度算（CJK 与全角符号占两列），否则中文签名会把下一列推歪。</summary>
    public static int DisplayWidth(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }
        int width = 0;
        foreach (char c in text)
        {
            width += IsWide(c) ? 2 : 1;
        }
        return width;
    }


    private static bool IsWide(char c)
    {
        return c >= 0x1100 && (
            c <= 0x115F                               // Hangul Jamo
            || c == 0x2329 || c == 0x232A
            || (c >= 0x2E80 && c <= 0xA4CF)            // CJK 部首 … 彝文
            || (c >= 0xAC00 && c <= 0xD7A3)            // Hangul 音节
            || (c >= 0xF900 && c <= 0xFAFF)            // CJK 兼容
            || (c >= 0xFE30 && c <= 0xFE6F)            // CJK 兼容标点
            || (c >= 0xFF00 && c <= 0xFF60)            // 全角 ASCII
            || (c >= 0xFFE0 && c <= 0xFFE6)
            || (c >= 0x3000 && c <= 0x303E));          // CJK 标点（含 …、，已在上面覆盖，保险）
    }


    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0)
        {
            return b.Length;
        }
        if (b.Length == 0)
        {
            return a.Length;
        }
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }
        for (int i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
