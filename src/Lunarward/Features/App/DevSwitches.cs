// Lunarward：开发/验证用的直达开关。
//
// 背景：UI 自动化在 Wayland/XWayland 下不可靠（点不到控件、键盘事件抓不稳，见 L-36），
// 所以截图与排错一律靠「启动时直接落到某一页/某个状态」。
// 约定：
//   * 命令行参数是**对外形态**：`--page`、`--subpage`、`--game`、`--scroll`、`--profile-editor`、`--window-action`；
//   * 同名环境变量（LUNARWARD_*）保留为脚本兼容，优先级低于命令行；
//   * 全部属于开发设施，正式版应当移除（登记在 12-shims-and-gaps.md）。
using System;
using System.Linq;

namespace Starward.Features.Dev;

internal static class DevSwitches
{

    /// <summary>
    /// 开发设施总开关：Debug 构建默认开启，Release 构建必须显式 `LUNARWARD_DEV=1`
    /// （防止发行版里留下能真下载/真启动的入口，见 00-decisions D35）。
    /// </summary>
    internal static bool IsEnabled
    {
        get
        {
#if DEBUG
            return true;
#else
            return Environment.GetEnvironmentVariable("LUNARWARD_DEV") is "1" or "true";
#endif
        }
    }


    private static string[] Args => Environment.GetCommandLineArgs().Skip(1).ToArray();

    /// <summary>读 `--name value` 或 `--name=value`；没有则回落到环境变量。</summary>
    private static string? Read(string argName, string envName)
    {
        if (!IsEnabled)
        {
            return null;
        }
        string[] args = Args;
        string flag = "--" + argName;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].StartsWith(flag + "=", StringComparison.OrdinalIgnoreCase))
            {
                return args[i][(flag.Length + 1)..];
            }
            if (args[i].Equals(flag, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }
        string? env = Environment.GetEnvironmentVariable(envName);
        return string.IsNullOrWhiteSpace(env) ? null : env;
    }

    private static bool Flag(string argName, string envName)
        => Read(argName, envName) is string value && value is "1" or "true";

    /// <summary>直接跳到某个主页：launcher / gamesetting / screenshot / gacha / record / selfquery / beyond / setting / login。</summary>
    public static string? Page => Read("page", "LUNARWARD_PAGE");

    /// <summary>设置页里直接进入某个子页（AboutSetting、RuntimeEnvironmentSetting…）。</summary>
    public static string? SubPage => Read("subpage", "LUNARWARD_SUBPAGE");

    /// <summary>直接选中某个游戏（GameBiz 值，如 hk4e_cn）。</summary>
    public static string? Game => Read("game", "LUNARWARD_GAME");

    /// <summary>页面启动后滚动到的像素位置（截图用）。</summary>
    public static int Scroll =>
        int.TryParse(Read("scroll", "LUNARWARD_SCROLL"), out int offset) ? offset : 0;

    /// <summary>打开第一个配置的编辑对话框（截图用）。</summary>
    public static bool ProfileEditor => Flag("profile-editor", "LUNARWARD_PROFILE_EDITOR");

    /// <summary>截图页加载后直接打开查看器，指向第 N 张（-1 表示不打开）。</summary>
    public static int ViewerIndex =>
        int.TryParse(Read("viewer", "LUNARWARD_VIEWER"), out int index) ? index : -1;

    /// <summary>minimize / close：无人值守验证窗口动作。</summary>
    public static string? WindowAction => Read("window-action", "LUNARWARD_WINDOW_ACTION");
}
