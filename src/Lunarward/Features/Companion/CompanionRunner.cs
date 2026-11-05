// Lunarward：按 Profile 的环境把后启动程序拉起来。只负责"进程与环境"，不实现任何注入/打补丁逻辑。
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Features.GameLauncher;
using Starward.Features.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Companion;

public sealed class CompanionRunner
{
    private readonly ILogger<CompanionRunner> _logger;

    public CompanionRunner(ILogger<CompanionRunner> logger)
    {
        _logger = logger;
    }

    /// <summary>游戏启动后拉起所有"伴随启动"的条目（串行，按列表顺序）。</summary>
    /// <summary>
    /// 按触发时机拉起与游戏绑定的后启动程序。
    /// trigger: on_game_started / after_delay / on_game_exit（D24）。
    /// </summary>
    public async Task StartCompanionsAsync(GameBiz biz, string trigger = "on_game_started", CancellationToken cancellationToken = default)
    {
        IReadOnlyList<CompanionEntry> entries = CompanionStore.Load(biz);
        foreach (CompanionEntry entry in entries.Where(x => x.Enabled && x.Companion && string.Equals(x.Trigger, trigger, StringComparison.Ordinal)))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            try
            {
                if (entry.DelayMs > 0)
                {
                    await Task.Delay(entry.DelayMs, cancellationToken);
                }
                StartEntry(biz, entry);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Start companion {name} failed", entry.Name);
            }
        }
    }

    /// <summary>手动启动一条（"立即启动"按钮）。</summary>
    public Process? StartEntry(GameBiz biz, CompanionEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Path) || !File.Exists(entry.Path))
        {
            throw new FileNotFoundException($"找不到后启动程序：{entry.Path}", entry.Path);
        }
        ProcessStartInfo info = CreateStartInfo(entry, biz);
        // 手动启动前检查前缀占用：同一个 prefix 里再起一个 wine 会话，轻则互相干扰，重则把游戏带崩（D39）。
        string? targetPrefix = info.Environment.TryGetValue("WINEPREFIX", out string? lockedPrefix) ? lockedPrefix : null;
        if (Starward.Features.Runtime.PrefixManager.IsPrefixLocked(targetPrefix))
        {
            throw new InvalidOperationException($"{Starward.Language.Lang.Lunarward_Error_PrefixLocked}{targetPrefix}");
        }
        _logger.LogInformation("Start companion {name}: {file} {args} (prefix={prefix})",
            entry.Name, info.FileName, string.Join(' ', info.ArgumentList), info.Environment.TryGetValue("WINEPREFIX", out string? prefix) ? prefix : "");
        Process? process = Process.Start(info);
        // D27：后启动程序的输出同样要捕获，否则「patch/hook 没生效」时无从查起。
        if (process is not null && info.RedirectStandardOutput)
        {
            Features.Runtime.OutputCapture.Attach($"companion:{entry.Id}", process);
        }
        return process;
    }

    public ProcessStartInfo CreateStartInfo(CompanionEntry entry, GameBiz? biz = null)
    {
        string workDir = string.IsNullOrWhiteSpace(entry.WorkingDirectory)
            ? System.IO.Path.GetDirectoryName(entry.Path) ?? System.Environment.CurrentDirectory
            : entry.WorkingDirectory;

        bool isWindowsExe = entry.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            || entry.Path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)
            || entry.Path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase);
        string? wine = GameLauncherService.ResolveWine();
        ProcessStartInfo info = new()
        {
            // .exe 不能直接 exec，必须经 wine（与游戏启动同一条路，但不套 shell，L-30 相关约束）。
            FileName = isWindowsExe ? (wine ?? "wine") : entry.Path,
            WorkingDirectory = workDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        if (isWindowsExe)
        {
            info.ArgumentList.Add(entry.Path);
        }
        foreach (string arg in GameLauncherService.SplitArguments(entry.Arguments))
        {
            info.ArgumentList.Add(arg);
        }

        // 环境：先铺 Profile（或游戏的默认 Profile），再让条目自己的 env 覆盖。
        RuntimeProfile? profile = ResolveProfile(entry);
        if (profile is not null)
        {
            foreach (KeyValuePair<string, string> pair in ProfileShellService.BuildEnvironment(profile, biz?.Value))
            {
                info.Environment[pair.Key] = pair.Value;
            }
        }
        else
        {
            if (!info.Environment.ContainsKey("WINEPREFIX"))
            {
                info.Environment["WINEPREFIX"] = GameLauncherService.ResolvePrefix(System.IO.Path.GetFileNameWithoutExtension(entry.Path));
            }
            if (wine is not null)
            {
                info.Environment["LUNARWARD_WINE"] = wine;
            }
        }
        foreach (KeyValuePair<string, string> pair in entry.Environment)
        {
            info.Environment[pair.Key] = pair.Value;
        }
        info.Environment["LUNARWARD_COMPANION"] = entry.Id;
        return info;
    }

    private RuntimeProfile? ResolveProfile(CompanionEntry entry)
    {
        IReadOnlyList<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll();
        if (profiles.Count == 0)
        {
            return null;
        }
        if (!string.IsNullOrWhiteSpace(entry.ProfileId))
        {
            return profiles.FirstOrDefault(x => x.Id == entry.ProfileId)
                ?? profiles.FirstOrDefault(x => x.Name == entry.ProfileId);
        }
        return profiles[0];
    }
}
