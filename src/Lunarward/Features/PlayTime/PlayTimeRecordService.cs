using Dapper;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.Database;
using Starward.Features.GameLauncher;
using Starward.Features.HoYoPlay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.PlayTime;

internal class PlayTimeRecordService
{

    private readonly ILogger<PlayTimeRecordService> _logger;

    private readonly HoYoPlayService _hoYoPlayService;

    private readonly PlayTimeStatsService _playTimeStatsService;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, byte> _recordingPids = new();

    private static bool IsRecording(int pid) => _recordingPids.ContainsKey(pid);



    public PlayTimeRecordService(ILogger<PlayTimeRecordService> logger, HoYoPlayService hoYoPlayService, PlayTimeStatsService playTimeStatsService)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
        _playTimeStatsService = playTimeStatsService;
    }



    public async Task LogPlayTimeAsync(GameBiz biz, int pid)
    {
        try
        {
            biz = biz.IsBilibili() ? $"{biz.Game}_cn" : biz;
            // Lunarward：按 pid 去重就够了。以前用的是一把全局命名互斥量，而且从不释放，
            // 结果第二个游戏进去就被挡住、时长恒为 0（L-62）。
            if (!_recordingPids.TryAdd(pid, 0))
            {
                _logger.LogWarning("Game process ({biz}, {gamePid}) is already being recorded", biz, pid);
                return;
            }
            _logger.LogInformation("Start to log playtime ({biz}, {pid})", biz, pid);
            // Lunarward：Linux 上我们 exec 的是 wine/Proton 的 loader，它可能立刻 re-exec 掉自己，
            // 拿到的 PID 一秒后就没了。此时不放弃记录，改用「最近一次 Start 事件」作为起点，
            // 会话结束仍由 wineserver -w 判定（见 PlayTimeSession）。
            Process? process = null;
            DateTime startTime;
            try
            {
                process = Process.GetProcessById(pid);
                startTime = process.StartTime;
                LogStartState(biz, process);
            }
            catch (ArgumentException)
            {
                DateTime? lastStart = GetLastStartTime(biz, pid);
                // pid 会被复用：拿到的 Start 可能是上一轮游戏的，直接当起点会把空闲时间算成游玩（L-62）。
                if (lastStart is { } stale && DateTime.Now - stale > TimeSpan.FromHours(12))
                {
                    lastStart = null;
                }
                startTime = lastStart ?? DateTime.Now;
                _logger.LogInformation("Playtime: process {pid} already gone, fall back to last start time {start} ({biz})", pid, startTime, biz);
                Log(biz, pid, PlayTimeState.Start, new DateTimeOffset(startTime).ToUnixTimeMilliseconds(), "wine loader exited early");
            }
            var sw = Stopwatch.StartNew();
            long last = 0;
            if (!OperatingSystem.IsWindows())
            {
                // Linux：用 wineserver -w 判断「这个前缀里的 wine 会话」是否结束（见 PlayTimeSession），
                // 并每 30 秒落一次 Interruption=true 的心跳记录，避免启动器被杀时整段时长丢失。
                using var cts = new CancellationTokenSource();
                string? prefix = PlayTimeSession.ActivePrefix ?? Features.GameLauncher.GameLauncherService.ResolvePrefix(biz.ToString());
                Task session = PlayTimeSession.WaitForSessionEndAsync(pid, prefix, cts.Token);
                while (!session.IsCompleted)
                {
                    Task exited = await Task.WhenAny(session, Task.Delay(20000));
                    if (exited == session)
                    {
                        break;
                    }
                    if (sw.ElapsedMilliseconds - last > 30000)
                    {
                        Log(biz, pid, PlayTimeState.Play);
                        SavePlayTimeStats(biz, pid, startTime, DateTime.Now, interruption: true);
                        last = sw.ElapsedMilliseconds;
                    }
                }
                cts.Cancel();
                var endTime = DateTimeOffset.Now;
                Log(biz, pid, PlayTimeState.Stop, endTime.ToUnixTimeMilliseconds(), $"wine session [{endTime}]");
                SavePlayTimeStats(biz, pid, startTime, endTime.DateTime, interruption: false);
            }
            else
            {
                while (true)
                {
                    await Task.Delay(Random.Shared.Next(800, 1200));
                    if (process.HasExited)
                    {
                        var now = DateTimeOffset.Now;
                        Log(biz, pid, PlayTimeState.Stop, now.ToUnixTimeMilliseconds(), $"{process.ProcessName} [{now}]");
                        SavePlayTimeStats(biz, pid, process.StartTime, now.DateTime);
                        break;
                    }
                    else
                    {
                        if (sw.ElapsedMilliseconds - last > 30000)
                        {
                            Log(biz, pid, PlayTimeState.Play);
                            last = sw.ElapsedMilliseconds;
                        }
                    }
                }
            }
            DatabaseService.SetValue($"playtime_total_{biz}", _playTimeStatsService.GetPlayTimeTotal(biz));
            _logger.LogInformation("End log playtime ({biz}, {pid})", biz, pid);
        }
        catch (Exception ex)
        {
            Log(biz, pid, PlayTimeState.Error, 0, ex.Message);
            _logger.LogError(ex, "Log play time: GameBiz {biz}, Pid {pid}", biz, pid);
        }
        finally
        {
            _recordingPids.TryRemove(pid, out _);
        }
    }



    /// <summary>取该 PID 最近一次的 Start 事件时间；没有就返回 null。</summary>
    private static DateTime? GetLastStartTime(GameBiz biz, int pid)
    {
        try
        {
            using var dapper = DatabaseService.CreateConnection();
            var items = System.Linq.Enumerable.ToList(dapper.Query<PlayTimeItemStruct>(
                "SELECT * FROM PlayTimeItem WHERE GameBiz = @biz AND Pid = @pid ORDER BY TimeStamp DESC LIMIT 1;",
                new { biz = biz.ToString(), pid }));
            if (items.Count > 0 && items[0].TimeStamp > 0)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(items[0].TimeStamp).DateTime;
            }
        }
        catch
        {
        }
        return null;
    }




    private void LogStartState(GameBiz biz, Process process)
    {
        var startTime = new DateTimeOffset(process.StartTime);
        Log(biz, process.Id, PlayTimeState.Start, startTime.ToUnixTimeMilliseconds(), $"{process.ProcessName} [{startTime}]");
        using var dapper = DatabaseService.CreateConnection();
        var last = dapper.QueryFirstOrDefault<PlayTimeItemStruct>("SELECT * FROM PlayTimeItem WHERE GameBiz = @biz AND Pid = @Id ORDER BY TimeStamp DESC LIMIT 1;", new { biz = biz.ToString(), process.Id });
        DateTimeOffset time = startTime;
        if (last.TimeStamp > startTime.ToUnixTimeMilliseconds())
        {
            time = DateTimeOffset.FromUnixTimeMilliseconds(last.TimeStamp);
        }

        var now = DateTimeOffset.Now;
        if (now - time >= TimeSpan.FromSeconds(60))
        {
            // 补全从开始游戏到开始记录游戏时间之间的记录
            List<PlayTimeItem> list = new List<PlayTimeItem>();
            while (true)
            {
                time = time.AddMilliseconds(Random.Shared.Next(30_000, 32_000));
                if (time < now)
                {
                    list.Add(new PlayTimeItem
                    {
                        TimeStamp = time.ToUnixTimeMilliseconds(),
                        GameBiz = biz,
                        Pid = process.Id,
                        State = PlayTimeState.Play,
                    });
                }
                else
                {
                    break;
                }
            }
            using var t = dapper.BeginTransaction();
            dapper.Execute("INSERT OR REPLACE INTO PlayTimeItem (TimeStamp, GameBiz, Pid, State, CursorPos, Message) VALUES (@TimeStamp, @GameBiz, @Pid, @State, @CursorPos, @Message);", list, t);
            t.Commit();
        }
    }



    private void Log(GameBiz biz, int pid, PlayTimeState state, long ts = 0, string? message = null)
    {
        try
        {
            using var dapper = DatabaseService.CreateConnection();
            var item = new PlayTimeItem
            {
                TimeStamp = ts == 0 ? DateTimeOffset.Now.ToUnixTimeMilliseconds() : ts,
                GameBiz = biz,
                Pid = pid,
                State = state,
                Message = message,
            };
            dapper.Execute("INSERT OR REPLACE INTO PlayTimeItem (TimeStamp, GameBiz, Pid, State, CursorPos, Message) VALUES (@TimeStamp, @GameBiz, @Pid, @State, @CursorPos, @Message);", item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Log play time: GameBiz {biz}, Pid {pid}, State {state}, Message {message}", biz, pid, state, message);
        }
    }



    private void SavePlayTimeStats(GameBiz biz, int pid, DateTime startTime, DateTime endTime, bool interruption = false)
    {
        try
        {
            using var dapper = DatabaseService.CreateConnection();
            var stats = new PlayTimeStats
            {
                GameBiz = biz,
                Pid = pid,
                StartTime = new DateTimeOffset(startTime).ToUnixTimeMilliseconds(),
                EndTime = new DateTimeOffset(endTime).ToUnixTimeMilliseconds(),
                Interruption = interruption,
            };
            using var t = dapper.BeginTransaction();
            dapper.Execute("INSERT OR REPLACE INTO PlayTimeStats (GameBiz, Pid, StartTime, EndTime, Interruption, Type) VALUES (@GameBiz, @Pid, @StartTime, @EndTime, @Interruption, @Type);", stats);
            dapper.Execute("DELETE FROM PlayTimeItem WHERE GameBiz = @GameBiz AND Pid = @Pid AND TimeStamp >= @StartTime AND TimeStamp <= @EndTime;", stats);
            t.Commit();
            _logger.LogInformation("Save play time stats: GameBiz {biz}, Pid {pid}, StartTime {startTime}, EndTime {endTime}, Interruption {interruption}", biz, pid, startTime, endTime, false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Save play time stats: GameBiz {biz}, Pid {pid}, StartTime {startTime}, EndTime {endTime}, Interruption {interruption}", biz, pid, startTime, endTime, false);
        }
    }




    #region Start process to log playtime



    /// <summary>
    /// 启动进程记录游戏时间，返回游戏进程
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public async Task<Process?> StartProcessToLogAsync(GameId gameId)
    {
        try
        {
            var biz = gameId.GameBiz;
            string name = await GetGameExeNameWithoutExtensionAsync(gameId);
            for (int i = 0; i < 15; i++)
            {
                await Task.Delay(2000);
                var processes = Process.GetProcessesByName(name);
                if (processes.Length == 0)
                {
                    if (i < 5)
                    {
                        continue;
                    }
                    // 未找到游戏进程
                    return null;
                }
                foreach (var process in processes)
                {
                    if (IsRecording(process.Id))
                    {
                        // 已经有记录在进行（本进程内检查；启动器自身单实例）
                        _logger.LogInformation("Game process ({biz}, {gamePid}) is already being recorded", biz, process.Id);
                        continue;
                    }
                    if (process.SessionId != Process.GetCurrentProcess().SessionId)
                    {
                        // 游戏进程不在当前会话
                        _logger.LogWarning("Game process ({biz}, {gamePid}) is not in the current session", biz, process.Id);
                        continue;
                    }
                    _logger.LogInformation("Start to log playtime ({biz}, {pid})", biz, process.Id);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = AppConfig.StarwardExecutePath,
                        Arguments = $"playtime --biz {biz} --pid {process.Id}",
                        CreateNoWindow = true,
                    });
                    return process;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start process to log play time");
        }
        return null;
    }


    /// <summary>
    /// 启动进程记录游戏时间
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="pid"></param>
    /// <returns></returns>
    public async Task StartProcessToLogAsync(GameId gameId, int pid)
    {
        try
        {
            Process process = Process.GetProcessById(pid);
            var biz = gameId.GameBiz;
            string name = await GetGameExeNameWithoutExtensionAsync(gameId);
            // Lunarward：Linux 上我们启动的是 wine，进程名是 wine/wine64 而不是游戏 exe 名；
            // 真正跑游戏的进程是它的子进程，这里按「启动器进程」记录时长即可（L-46）。
            if (OperatingSystem.IsWindows() && process.ProcessName != name)
            {
                _logger.LogWarning("Game process ({biz}, {gamePid}) is not the expected process ({name})", biz, pid, process.ProcessName);
                return;
            }
            if (!OperatingSystem.IsWindows() && process.ProcessName is not ("wine" or "wine64" or "wine-preloader"))
            {
                _logger.LogWarning("Linux play time: unexpected launcher process ({biz}, {gamePid}, {name})", biz, pid, process.ProcessName);
            }
            if (IsRecording(pid))
            {
                _logger.LogWarning("Game process ({biz}, {gamePid}) is already being recorded", biz, pid);
                return;
            }

            Process? p = Process.Start(new ProcessStartInfo
            {
                FileName = AppConfig.StarwardExecutePath,
                Arguments = $"playtime --biz {biz} --pid {process.Id}",
                CreateNoWindow = true,
            });
            _logger.LogInformation("Start process to log play time: GameBiz {biz}, Pid {pid}, ProcessId {processId}", biz, pid, p?.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Start process to log play time: GameBiz {biz}, Pid {pid}", gameId.GameBiz, pid);
        }
    }




    /// <summary>
    /// 游戏进程名，不带 .exe 扩展名
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public async Task<string> GetGameExeNameWithoutExtensionAsync(GameId gameId)
    {
        string? name = GameLauncherService.GetGameExeName(gameId.GameBiz);
        if (string.IsNullOrWhiteSpace(name))
        {
            var config = await _hoYoPlayService.GetGameConfigAsync(gameId);
            name = config?.ExeFileName;
        }
        return name?.Replace(".exe", "") ?? throw new ArgumentOutOfRangeException($"Unknown game ({gameId.Id}, {gameId.GameBiz}).");
    }



    #endregion



}
