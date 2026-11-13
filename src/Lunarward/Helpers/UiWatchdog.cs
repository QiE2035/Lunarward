using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.UI.Dispatching;
using Serilog;

namespace Starward.Helpers;

/// <summary>
/// UI 线程看门狗（默认关闭，用 <c>LUNARWARD_UI_WATCHDOG=1</c> 打开）。
///
/// 用途：本程序在 Linux 上偶发「整窗画面停住、点东西要等几秒才响应」，开发机复现不出来。
/// 后台线程每 100 ms 往 DispatcherQueue 投一个探针，测「投进去 → 真的在 UI 线程执行」的延迟。
/// UI 线程被挡住多少毫秒，这里就记多少毫秒，事后能在日志里定位时刻与规模。
///
/// 只在设置环境变量时启用；发行版默认不产生额外线程与日志（D35）。
/// </summary>
internal static class UiWatchdog
{
    private const int PingIntervalMs = 100;

    private const int StallThresholdMs = 300;

    private static int _started;

    public static bool Enabled => Environment.GetEnvironmentVariable("LUNARWARD_UI_WATCHDOG") is "1";

    public static void Start(DispatcherQueue? queue)
    {
        if (queue is null || !Enabled)
        {
            return;
        }
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }
        var thread = new Thread(() => RunLoop(queue))
        {
            IsBackground = true,
            Name = "Lunarward UI Watchdog",
        };
        thread.Start();
        Log.Information("[watchdog] UI 看门狗已启用（每 {interval} ms 投一次探针，超过 {threshold} ms 记为卡顿）", PingIntervalMs, StallThresholdMs);
    }

    private static void RunLoop(DispatcherQueue queue)
    {
        var sw = new Stopwatch();
        var session = Stopwatch.StartNew();
        int stallCount = 0;
        long worstMs = 0;
        long lastReportMs = 0;
        while (true)
        {
            try
            {
                Thread.Sleep(PingIntervalMs);
                using var done = new ManualResetEventSlim(false);
                sw.Restart();
                if (!queue.TryEnqueue(() => done.Set()))
                {
                    continue;
                }
                long waited;
                if (done.Wait(PingIntervalMs * 300))
                {
                    waited = sw.ElapsedMilliseconds;
                }
                else
                {
                    // 30 秒都没执行：几乎必然是真的死锁，先记下来再继续等。
                    waited = sw.ElapsedMilliseconds;
                    Log.Error("[watchdog] UI 线程 30 秒没有执行探针，疑似死锁");
                }
                if (waited >= StallThresholdMs)
                {
                    stallCount++;
                    if (waited > worstMs)
                    {
                        worstMs = waited;
                    }
                    Log.Warning("[watchdog] UI 线程卡顿 {waited} ms（累计 {count} 次，最差 {worst} ms）", waited, stallCount, worstMs);
                }
                long now = session.ElapsedMilliseconds;
                if (now - lastReportMs >= 60_000)
                {
                    lastReportMs = now;
                    Log.Information("[watchdog] {seconds} 秒汇总：卡顿 {count} 次，最差 {worst} ms", now / 1000, stallCount, worstMs);
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "[watchdog] 探针失败");
            }
        }
    }
}
