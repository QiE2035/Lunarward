// Lunarward：启动前预检（D33「占位必须诚实」的延伸）。
//
// 目的：把「为什么启动不了」在启动**之前**说清楚，而不是让用户对着没有反应的按钮猜。
// 每条检查给出 通过/警告/失败 + 一句人话 + 修法入口。
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Features.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Starward.Features.GameLauncher;


/// <summary>预检项对应的「可点修复动作」。None 表示只能看文字提示。</summary>
public enum PreflightAction
{
    None,
    OpenGameSettings,
    OpenRuntimeSettings,
    OpenPrefixFolder,
    RebuildPrefix,
    InstallGraphics,
    ShowPrefixProcesses,
}


/// <summary>检查分层（D73 §6）：组件 / 前缀 / 图形 / 启动，四项归并成一张清单。</summary>
public enum PreflightLayer
{
    Component,
    Prefix,
    Graphics,
    Launch,
}


public enum PreflightStatus
{
    Ok,
    Warning,
    Failed,
}


public sealed class PreflightItem
{
    public string Name { get; init; } = "";

    public PreflightStatus Status { get; init; }

    public string Detail { get; init; } = "";

    public string? Fix { get; init; }

    /// <summary>能直接执行的动作；UI 据此放一个按钮，而不是只写「请去设置里改」。</summary>
    public PreflightAction Action { get; init; }

    /// <summary>所属层（D73 §6 四层检查）。</summary>
    public PreflightLayer Layer { get; set; }
}


public sealed class PreflightResult
{
    public List<PreflightItem> Items { get; } = new();

    public bool CanLaunch => Items.All(x => x.Status != PreflightStatus.Failed);

    public string Summary => string.Join("\n", Items
        .Where(x => x.Status != PreflightStatus.Ok)
        .Select(x => $"· {x.Name}：{x.Detail}" + (x.Fix is null ? "" : $"（{x.Fix}）")));
}


public static class LaunchPreflight
{

    public static PreflightResult Check(GameBiz biz, string exePath, RuntimeProfile? profile, ILogger? logger = null)
    {
        PreflightResult result = new();

        // 1) 游戏可执行文件
        result.Items.Add(File.Exists(exePath)
            ? new PreflightItem { Name = "游戏可执行文件", Status = PreflightStatus.Ok, Detail = exePath }
            : new PreflightItem
            {
                Name = "游戏可执行文件",
                Status = PreflightStatus.Failed,
                Detail = $"找不到 {exePath}",
                Fix = "在「游戏设置」里确认安装路径",
                Action = PreflightAction.OpenGameSettings,
            });

        // 2) 运行器
        // Profile 显式指定的运行器不存在时必须判失败：静默回退到自动探测会让人以为在用某个 Proton，
        // 实际却跑在别的 wine 上（真机启动一次踩到过，见 L-44）。
        string? specified = profile?.RunnerPath;
        string? wine;
        if (!string.IsNullOrWhiteSpace(specified))
        {
            wine = File.Exists(specified) ? specified : null;
            result.Items.Add(wine is null
                ? new PreflightItem
                {
                    Name = "Wine 运行器",
                    Status = PreflightStatus.Failed,
                    Detail = $"该配置指定的运行器不存在：{specified}",
                    Fix = "在「运行环境」里重新选择运行器，或把运行器路径清空以使用自动探测",
                    Action = PreflightAction.OpenRuntimeSettings,
                }
                : new PreflightItem { Name = "Wine 运行器", Status = PreflightStatus.Ok, Detail = wine });
        }
        else
        {
            wine = GameLauncherService.ResolveWine();
            result.Items.Add(wine is null
                ? new PreflightItem
                {
                    Name = "Wine 运行器",
                    Status = PreflightStatus.Failed,
                    Detail = "既没有指定运行器，也没在 Steam/PATH 里找到 wine",
                    Fix = "设置环境变量 LUNARWARD_WINE，或在「运行环境」里给配置指定运行器",
                    Action = PreflightAction.OpenRuntimeSettings,
                }
                : new PreflightItem { Name = "Wine 运行器", Status = PreflightStatus.Ok, Detail = wine });
        }

        // 3) 前缀
        string? prefix = null;
        // 预检必须和启动用同一个 key：启动走的是 exe 文件名（GameLauncherService.Linux 里 key = 去掉扩展名的 exe），
        // 这里以前用 biz.Value，template/ephemeral 模式会算成另一个前缀，预检报告的和实际用的不是同一个（L-62）。
        string gameKey = Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant();
        try
        {
            // 预检只「看」不「建」：以前这里直接 EnsurePrefix，点一次「预检」就会 wineboot/clone 出前缀，
            // external 前缀还会被塞进 .lunarward/（L-62）。没准备好就如实说「启动时会准备」。
            prefix = profile is not null
                ? PrefixManager.TryGetExistingPrefix(profile, gameKey)
                : GameLauncherService.ResolvePrefix(gameKey);
            if (prefix is null)
            {
                result.Items.Add(profile is not null
                    ? new PreflightItem
                    {
                        Name = "Wine 前缀",
                        Status = PreflightStatus.Ok,
                        Detail = $"尚未准备，启动时创建：{PrefixManagerShort(profile)}",
                    }
                    : new PreflightItem
                    {
                        Name = "Wine 前缀",
                        Status = PreflightStatus.Failed,
                        Detail = "解析不到可用的前缀",
                        Fix = "在「运行环境」里配置前缀路径，或设置 LUNARWARD_WINEPREFIX",
                        Action = PreflightAction.OpenRuntimeSettings,
                    });
            }
            else
            {
                result.Items.Add(new PreflightItem { Name = "Wine 前缀", Status = PreflightStatus.Ok, Detail = prefix });
            }
        }
        catch (Exception ex)
        {
            result.Items.Add(new PreflightItem
            {
                Name = "Wine 前缀",
                Status = PreflightStatus.Failed,
                Detail = ex.Message,
                Fix = "在「运行环境」里检查该配置的前缀路径",
                Action = PreflightAction.OpenRuntimeSettings,
            });
        }

        // 3.5) 前缀结构：external 模式很容易指到「前缀根目录」或普通文件夹，
        // 那样 wine 会安静地在里面新建一个空前缀，游戏随后因缺运行库/图形组件起不来（实测绝区零）。
        if (prefix is not null && Directory.Exists(prefix))
        {
            bool hasDriveC = Directory.Exists(Path.Combine(prefix, "drive_c"));
            bool hasRegistry = File.Exists(Path.Combine(prefix, "system.reg")) || File.Exists(Path.Combine(prefix, "user.reg"));
            if (!hasDriveC || !hasRegistry)
            {
                result.Items.Add(new PreflightItem
                {
                    Name = "前缀结构",
                    Status = PreflightStatus.Warning,
                    Detail = hasDriveC ? "缺少 system.reg/user.reg（不像已初始化的 wine 前缀）" : "缺少 drive_c（不像 wine 前缀）",
                    Fix = "确认选的是真正的前缀目录，而不是存放前缀的上级目录；wine 会在给定路径里新建空前缀",
                    Action = PreflightAction.OpenPrefixFolder,
                });
            }
        }

        // 4) 前缀可写 + 未被占用
        if (prefix is not null && Directory.Exists(prefix))
        {
            try
            {
                string probe = Path.Combine(prefix, $".lunarward-probe-{Guid.NewGuid():n}");
                File.WriteAllText(probe, "probe");
                File.Delete(probe);
                result.Items.Add(new PreflightItem { Name = "前缀可写", Status = PreflightStatus.Ok, Detail = "ok" });
            }
            catch (Exception ex)
            {
                result.Items.Add(new PreflightItem
                {
                    Name = "前缀可写",
                    Status = PreflightStatus.Failed,
                    Detail = ex.Message,
                    Fix = "检查该目录的所有者与权限",
                    Action = PreflightAction.OpenPrefixFolder,
                });
            }

            // 只读检查占用：以前这里会 Directory.CreateDirectory(prefix/.lunarward) 再建锁文件，
            // 结果「点一下预检」就往 external 前缀（例如 ~/.wine）里塞了 .lunarward/（L-62）。
            bool locked = PrefixManager.IsPrefixLocked(prefix);
            result.Items.Add(locked
                ? new PreflightItem
                {
                    Name = "前缀占用",
                    Status = PreflightStatus.Failed,
                    Detail = "这个前缀正被另一个实例使用",
                    Fix = "等它退出，或给这个游戏换一份独立的前缀",
                    Action = PreflightAction.ShowPrefixProcesses,
                }
                : new PreflightItem { Name = "前缀占用", Status = PreflightStatus.Ok, Detail = "未被占用" });
        }

        // 5) 图形组件
        if (profile is not null)
        {
            if (string.IsNullOrWhiteSpace(profile.GraphicsId))
            {
                result.Items.Add(new PreflightItem
                {
                    Name = "图形组件",
                    Status = PreflightStatus.Warning,
                    Detail = "未选择 DXVK/VKD3D，将沿用前缀里已有的实现",
                    Fix = "在「运行环境」里选择组件（可选）",
                    Action = PreflightAction.OpenRuntimeSettings,
                });
            }
            else
            {
                Component? component = ComponentStore.GetGraphics().FirstOrDefault(c => c.Id == profile.GraphicsId);
                result.Items.Add(component is null
                    ? new PreflightItem
                    {
                        Name = "图形组件",
                        Status = PreflightStatus.Failed,
                        Detail = $"配置里选的组件已找不到：{profile.GraphicsId}",
                        Fix = "重新选择图形组件",
                        Action = PreflightAction.OpenRuntimeSettings,
                    }
                    : new PreflightItem { Name = "图形组件", Status = PreflightStatus.Ok, Detail = component.DisplayName });
            }
        }

        AssignLayers(result.Items);
        foreach (PreflightItem item in result.Items.Where(x => x.Status != PreflightStatus.Ok))
        {
            logger?.LogWarning("[preflight] {status} {name}: {detail}", item.Status, item.Name, item.Detail);
        }
        return result;
    }


    /// <summary>
    /// 归并成一张清单：先按层（组件 → 前缀 → 图形 → 启动），层内按严重程度（失败 → 警告 → 通过）。
    /// </summary>
    private static void AssignLayers(List<PreflightItem> items)
    {
        foreach (PreflightItem item in items)
        {
            item.Layer = item.Name switch
            {
                "游戏可执行文件" or "Wine 运行器" => PreflightLayer.Component,
                "Wine 前缀" or "前缀结构" or "前缀可写" or "前缀占用" => PreflightLayer.Prefix,
                "图形组件" => PreflightLayer.Graphics,
                _ => PreflightLayer.Launch,
            };
        }
        items.Sort((a, b) =>
        {
            int byLayer = a.Layer.CompareTo(b.Layer);
            if (byLayer != 0)
            {
                return byLayer;
            }
            return b.Status.CompareTo(a.Status);
        });
    }


    private static string PrefixManagerShort(RuntimeProfile profile)
    {
        try
        {
            return EnvironmentResolver.EffectivePrefixPath(profile);
        }
        catch (Exception)
        {
            return profile.PrefixPath ?? "(默认目录)";
        }
    }

}


/// <summary>预检未通过：带完整结果，UI 可以逐项展示而不是只给一句「启动失败」。</summary>
public sealed class LaunchPreflightException : Exception
{

    public PreflightResult Result { get; }

    public LaunchPreflightException(PreflightResult result) : base("启动前检查未通过：" + Environment.NewLine + result.Summary)
    {
        Result = result;
    }

}
