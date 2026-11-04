// Lunarward：上游用 Win32 SetWinEventHook 追踪游戏窗口并维护「最近活跃游戏」。
// Linux 侧 M1 用进程表 + /proc 判断存活；窗口层（overlay）留到 M4。
using Starward.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Linq;

namespace Starward.Features.Overlay;

internal class RunningGame
{
    public required GameBiz GameBiz { get; init; }

    public required Process Process { get; init; }

    public DateTime StartTime { get; } = DateTime.Now;

    public nint WindowHandle { get; set; }

    public bool IsActive => !HasExited;

    public bool HasExited
    {
        get
        {
            try
            {
                return Process.HasExited;
            }
            catch
            {
                return true;
            }
        }
    }
}

internal static class RunningGameService
{
    private static readonly Lock _lock = new();

    private static readonly List<RunningGame> _runningGames = new();

    public static void AddRuninngGame(GameBiz gameBiz, Process process)
    {
        lock (_lock)
        {
            _runningGames.RemoveAll(x => x.HasExited);
            if (_runningGames.Any(x => x.Process.Id == process.Id))
            {
                return;
            }
            _runningGames.Add(new RunningGame { GameBiz = gameBiz, Process = process });
        }
    }

    public static int GetRunningGameCount()
    {
        lock (_lock)
        {
            _runningGames.RemoveAll(x => x.HasExited);
            return _runningGames.Count;
        }
    }

    public static RunningGame? GetLatestActiveGame()
    {
        lock (_lock)
        {
            _runningGames.RemoveAll(x => x.HasExited);
            return _runningGames.LastOrDefault();
        }
    }

    /// <summary>Overlay 依赖 Windows 的窗口钩子，M4 再实现。</summary>
    public static bool OpenOverlayWindow() => false;
}
