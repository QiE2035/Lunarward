// Lunarward：把「配方里的 runnerId」解析成真正要执行的文件（D73 §4.1 字段 2）。
//
// runnerId 是引用的组件 id（受管组件形如 wine-staging-10.13，其余为探测得到的 id）。
// RunnerPath 只是缓存：所有解析都在这里做，UI 绑定不要自己去扫盘（D72）。
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Starward.Features.Runtime;


public static class EnvironmentResolver
{


    /// <summary>按 id 找运行器组件；找不到退回路径匹配，再退回第一个可用运行器。</summary>
    public static Component? ResolveRunner(RuntimeProfile? profile, IReadOnlyList<Component>? runners = null)
    {
        if (profile is null)
        {
            return null;
        }
        runners ??= ComponentStore.GetRunners();
        List<Component> wineRunners = runners.Where(x => x.Kind is ComponentKind.Wine or ComponentKind.Proton).ToList();
        if (!string.IsNullOrWhiteSpace(profile.RunnerId))
        {
            Component? byId = wineRunners.FirstOrDefault(x => string.Equals(x.Id, profile.RunnerId, StringComparison.Ordinal));
            if (byId is not null)
            {
                return byId;
            }
        }
        if (!string.IsNullOrWhiteSpace(profile.RunnerPath))
        {
            string wanted = Path.GetFullPath(profile.RunnerPath!);
            Component? byPath = wineRunners.FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x.Path)
                && string.Equals(Path.GetFullPath(x.Path), wanted, StringComparison.Ordinal));
            if (byPath is not null)
            {
                return byPath;
            }
        }
        return wineRunners.FirstOrDefault(x => x.Usable == true) ?? wineRunners.FirstOrDefault();
    }


    /// <summary>该配方实际会用的运行器可执行文件；解析不到返回 null。</summary>
    public static string? ResolveRunnerPath(RuntimeProfile? profile, IReadOnlyList<Component>? runners = null)
    {
        if (profile is null)
        {
            return null;
        }
        Component? component = ResolveRunner(profile, runners);
        if (component is not null && !string.IsNullOrWhiteSpace(component.Path))
        {
            return component.Path;
        }
        // 组件列表里都没有：旧配置里存的路径仍然尊重（用户可能指向了扫描不到的位置）
        if (!string.IsNullOrWhiteSpace(profile.RunnerPath) && File.Exists(profile.RunnerPath!))
        {
            return profile.RunnerPath;
        }
        return null;
    }


    /// <summary>读配置后调用：把 RunnerId / RunnerPath 对齐到当前磁盘状态（幂等）。</summary>
    public static void Refresh(RuntimeProfile? profile, IReadOnlyList<Component>? runners = null, ILogger? logger = null)
    {
        if (profile is null)
        {
            return;
        }
        Component? component = ResolveRunner(profile, runners);
        if (component is null)
        {
            return;
        }
        if (!string.Equals(profile.RunnerId, component.Id, StringComparison.Ordinal))
        {
            logger?.LogInformation("Environment {id}: runner {from} -> {to}", profile.Id, profile.RunnerId ?? "(未指定)", component.Id);
            profile.RunnerId = component.Id;
        }
        profile.RunnerPath = component.Path;
    }


    /// <summary>配方的有效前缀路径：显式路径优先，否则默认到 <c>prefixes/&lt;name&gt;</c>。</summary>
    public static string EffectivePrefixPath(RuntimeProfile? profile)
    {
        string? configured = PrefixManager.ExpandPath(profile?.PrefixPath);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured!;
        }
        string name = string.IsNullOrWhiteSpace(profile?.Name) ? (profile?.Id ?? "default") : profile!.Name;
        return Path.Combine(PrefixManager.PrefixRoot, ComponentNaming.Sanitize(name).ToLowerInvariant());
    }

}
