// Lunarward：Linux 侧「游戏会话何时结束」的判定。
//
// Windows 上游戏进程就是游戏本体，进程退出 = 会话结束。
// Linux 上我们 exec 的是 wine，wine 自己会有 preloader / wineserver 等进程，
// 直接盯 launcher 拿到的那个 PID 会在某些组合下过早结束（例如游戏由 wine 重新 fork）。
//
// 最可靠的判据是「这个前缀里的 wineserver 什么时候退出」——wineserver 存活即代表仍有 wine 进程
// 使用该前缀。`wineserver -w` 正是等待它退出。拿不到 wineserver 时退回「进程 + 子孙进程」轮询。

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.PlayTime;

internal static class PlayTimeSession
{

    /// <summary>当前这次启动使用的前缀，由 GameLauncherService 在启动成功后写入。</summary>
    public static string? ActivePrefix { get; set; }


    /// <summary>当前这次启动真正用的 wine 可执行文件（可能是自定义运行器，不是 PATH/PATH 里的 Proton）。</summary>
    public static string? ActiveWine { get; set; }


    /// <summary>
    /// 等待游戏会话结束。直到 <paramref name="pid"/> 对应的 wine 会话（或该进程及其子孙）全部退出。
    /// </summary>
    public static async Task WaitForSessionEndAsync(int pid, string? prefix, CancellationToken cancellationToken = default)
    {
        string? wineserver = ResolveWineserver();
        if (wineserver is not null && !string.IsNullOrWhiteSpace(prefix) && Directory.Exists(prefix))
        {
            // 有的运行器（spritz-wine / Proton 变体）拿到 exe 后先起一个 steam.exe 存根，存根退出、
            // 真正的游戏是孙进程。此刻若立刻跑 `wineserver -w`，会因为「前缀里还没有 wine 进程」
            // 而瞬间返回，把会话误判成已结束（实测绝区零：窗口还在，游玩时长 0 秒、前缀锁被提前释放）。
            // 先等这一步确实把 wine 进程拉起来，再进入阻塞等待。
            bool spawned = await WaitForWineProcessAsync(prefix, pid, TimeSpan.FromSeconds(30), cancellationToken);
            if (!spawned)
            {
                await WaitForProcessTreeEndAsync(pid, cancellationToken);
                return;
            }
            try
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = wineserver,
                        ArgumentList = { "-w" },
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    },
                };
                process.StartInfo.Environment["WINEPREFIX"] = prefix;
                process.StartInfo.Environment["WINEDEBUG"] = "-all";
                process.Start();
                await process.WaitForExitAsync(cancellationToken);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // wineserver 不可用或参数不被支持，退回到进程轮询。
            }
        }
        await WaitForProcessTreeEndAsync(pid, cancellationToken);
    }


    /// <summary>
    /// 等这次启动真正产生挂在该前缀上的 wine 进程（最多 <paramref name="timeout"/>）。
    /// 启动进程已经退出且仍没有 wine 进程时，视为「没起来」并提前返回。
    /// </summary>
    private static async Task<bool> WaitForWineProcessAsync(string prefix, int pid, TimeSpan timeout, CancellationToken cancellationToken)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (HasWineProcessForPrefix(prefix))
            {
                return true;
            }
            if (!IsProcessAlive(pid))
            {
                // 存根先退出、wine 进程稍后才挂上前缀：再给 3 秒宽限。
                await Task.Delay(3000, cancellationToken);
                return HasWineProcessForPrefix(prefix);
            }
            await Task.Delay(500, cancellationToken);
        }
        return HasWineProcessForPrefix(prefix);
    }


    /// <summary>是否有进程带着 WINEPREFIX=&lt;prefix&gt; 在这个前缀里跑（读 /proc/*/environ）。</summary>
    private static bool HasWineProcessForPrefix(string prefix)
    {
        string needle = "WINEPREFIX=" + prefix;
        try
        {
            foreach (string dir in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(dir), out _))
                {
                    continue;
                }
                try
                {
                    string env = File.ReadAllText(Path.Combine(dir, "environ"));
                    if (env.Contains(needle + '\0', StringComparison.Ordinal) || env.EndsWith(needle, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
                catch
                {
                    // 进程刚退出 / 权限不足：跳过。
                }
            }
        }
        catch
        {
        }
        return false;
    }


    private static bool IsProcessAlive(int pid)
    {
        try
        {
            return Directory.Exists($"/proc/{pid}");
        }
        catch
        {
            return false;
        }
    }


    private static async Task WaitForProcessTreeEndAsync(int pid, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!HasLiveWineProcess(pid))
            {
                return;
            }
            await Task.Delay(1000, cancellationToken);
        }
    }


    /// <summary>该 PID 或其子孙里是否还有活着的 wine 相关进程。</summary>
    private static bool HasLiveWineProcess(int rootPid)
    {
        try
        {
            HashSet<int> tree = GetProcessTree(rootPid);
            if (tree.Count == 0)
            {
                return false;
            }
            foreach (int pid in tree)
            {
                string comm = ReadComm(pid);
                if (comm.StartsWith("wine", StringComparison.Ordinal) || comm.StartsWith("wineserver", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            // 还有子孙活着（游戏本体可能是 exe 名不可预期的进程），也算会话未结束。
            return tree.Count > 1;
        }
        catch
        {
            return false;
        }
    }


    private static HashSet<int> GetProcessTree(int rootPid)
    {
        var parentOf = new Dictionary<int, int>();
        foreach (string dir in Directory.EnumerateDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out int pid))
            {
                continue;
            }
            try
            {
                string stat = File.ReadAllText(Path.Combine(dir, "stat"));
                int close = stat.LastIndexOf(')');
                string[] rest = stat[(close + 2)..].Split(' ');
                if (int.TryParse(rest[1], out int ppid))
                {
                    parentOf[pid] = ppid;
                }
            }
            catch
            {
                // 进程在读取过程中退出是常态
            }
        }

        var result = new HashSet<int>();
        if (!parentOf.ContainsKey(rootPid))
        {
            return result;
        }
        result.Add(rootPid);
        bool added = true;
        while (added)
        {
            added = false;
            foreach ((int pid, int ppid) in parentOf)
            {
                if (!result.Contains(pid) && result.Contains(ppid))
                {
                    result.Add(pid);
                    added = true;
                }
            }
        }
        return result;
    }


    private static string ReadComm(int pid)
    {
        try
        {
            return File.ReadAllText($"/proc/{pid}/comm").Trim();
        }
        catch
        {
            return string.Empty;
        }
    }


    private static string? ResolveWineserver()
    {
        // 优先用父进程显式传下来的 wineserver：子进程里 ResolveWine 可能回落到别的 Proton/wine，
        // 版本不同会让 `-w` 立刻返回（L-92）。
        string? explicitServer = Environment.GetEnvironmentVariable("LUNARWARD_WINESERVER");
        if (!string.IsNullOrWhiteSpace(explicitServer) && File.Exists(explicitServer))
        {
            return explicitServer;
        }
        string? wine = Features.GameLauncher.GameLauncherService.ResolveWine();
        if (wine is not null)
        {
            string? dir = Path.GetDirectoryName(wine);
            if (dir is not null)
            {
                string candidate = Path.Combine(dir, "wineserver");
                if (File.Exists(candidate))
                {
                    return candidate;
                }
                // Proton 的 wineserver 在 files/bin 里；wine 通常也在同目录，上面已覆盖。
            }
        }
        foreach (string? dir in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(dir, "wineserver");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

}
