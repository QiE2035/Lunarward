// LUNARWARD-DEVIATION: 上游这个类是 gRPC 客户端（GameInstaller.GameInstallerClient），
// 安装/更新跑在独立的 Starward.RPC 进程里。移植侧不拆进程，直接在同进程里调 Starward.GameInstall.GameInstallRunner，
// 公开 API 与上游保持一致（D128）。D6 里「换成 Unix socket」的方案随上游 0.19.0 的进程内 Runner 一并作废。
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.GameInstall;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameInstall;

internal class GameInstallService
{

    private readonly ILogger<GameInstallService> _logger;

    private readonly GameInstallRunner _gameInstallRunner;

    private readonly HoYoPlayService _hoYoPlayService;

    private readonly SemaphoreSlim _getProgressSemaphore = new(1);


    public GameInstallService(ILogger<GameInstallService> logger, HoYoPlayService hoYoPlayService, GameInstallRunner gameInstallRunner)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
        _gameInstallRunner = gameInstallRunner;
    }



    private ConcurrentDictionary<GameId, GameInstallProgress> _tasks = new();


    /// <summary>
    /// 过场动画更新用的块列表。Runner 的 <see cref="GameInstallRequest"/> 需要它，
    /// 但 <see cref="GameInstallProgress"/> 不带这个字段，所以按 GameId 记下来供暂停/继续时重建请求（D135）。
    /// </summary>
    private readonly ConcurrentDictionary<GameId, List<string>> _cutsceneBlockLists = new();


    public GameInstallProgress? GetGameInstallTask(GameId gameId)
    {
        return _tasks.GetValueOrDefault(gameId);
    }


    /// <summary>
    /// 轮询 Runner 里的任务进度（上游这里是订阅 gRPC 流，移植侧改成定时拉取）。
    /// </summary>
    public async void StartUpdateTaskProgress()
    {
        if (_getProgressSemaphore.CurrentCount == 0)
        {
            return;
        }
        try
        {
            await _getProgressSemaphore.WaitAsync().ConfigureAwait(false);
            while (true)
            {
                foreach (GameInstallProgressDTO dto in _gameInstallRunner.GetAllProgress())
                {
                    AddOrUpdateTask(dto);
                }
                if (_tasks.IsEmpty)
                {
                    break;
                }
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTaskProgress");
        }
        finally
        {
            _getProgressSemaphore.Release();
            if (!_tasks.IsEmpty)
            {
                // D134：上游这里在重启轮询前先 Task.Delay(1000)（等 RPC 进程重启）。进程内没有 RPC，
                // 但退避本身还有用——GetTaskProgress 若持续抛异常，立即重启会退化成热循环。
                await Task.Delay(1000);
                StartUpdateTaskProgress();
            }
        }
    }


    private GameInstallProgress AddOrUpdateTask(GameInstallProgressDTO dto)
    {
        GameInstallProgress value = GameInstallProgress.FromDTO(dto);
        GameId gameId = value.GameId;
        if (_tasks.TryGetValue(gameId, out GameInstallProgress? task))
        {
            task.LauncherConfig = value.LauncherConfig;
            task.GameId = value.GameId;
            task.InstallPath = value.InstallPath;
            task.Operation = value.Operation;
            task.AudioLanguage = value.AudioLanguage;
            task.CategoryScenario = value.CategoryScenario;
            task.Timestamp = value.Timestamp;
            task.CurrentStep = value.CurrentStep;
            task.TotalStep = value.TotalStep;
            task.Step = value.Step;
            task.State = value.State;
            task.ErrorMessage = value.ErrorMessage;
            task.PercentProgress = value.PercentProgress;
            task.ProgressDisplay = value.ProgressDisplay;
            task.NetworkTotalBytes = value.NetworkTotalBytes;
            task.NetworkFinishedBytes = value.NetworkFinishedBytes;
            task.StorageTotalBytes = value.StorageTotalBytes;
            task.StorageFinishedBytes = value.StorageFinishedBytes;
            task.HardLinkBytes = value.HardLinkBytes;
            task.NetworkDownloadSpeed = value.NetworkDownloadSpeed;
            task.RemainTimeSeconds = value.RemainTimeSeconds;
        }
        else
        {
            task = value;
            _tasks.TryAdd(gameId, task);
        }
        if (task.State is RunnerState.Stop or RunnerState.Finish)
        {
            _tasks.TryRemove(gameId, out _);
        }
        return task;
    }


    public async Task<GameInstallProgress?> StartInstallAsync(GameId gameId, string installPath, string categoryScenario, AudioLanguage audioLanguage, CancellationToken cancellation)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.Install, gameId, installPath, categoryScenario, audioLanguage, cancellation);
    }


    public async Task<GameInstallProgress?> ResumeDownloadAsync(GameId gameId, string installPath, CancellationToken cancellation)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.Install, gameId, installPath, null, AudioLanguage.None, cancellation);
    }


    public async Task<GameInstallProgress?> StartPredownloadAsync(GameId gameId, string installPath, CancellationToken cancellation)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.Predownload, gameId, installPath, null, AudioLanguage.None, cancellation);
    }


    public async Task<GameInstallProgress?> StartUpdateAsync(GameId gameId, string installPath, CancellationToken cancellation)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.Update, gameId, installPath, null, AudioLanguage.None, cancellation);
    }


    public async Task<GameInstallProgress?> StartRepairAsync(GameId gameId, string installPath, CancellationToken cancellation)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.Repair, gameId, installPath, null, AudioLanguage.None, cancellation);
    }


    public async Task<GameInstallProgress?> StarUpdatePluginAsync(GameId gameId, string installPath, CancellationToken cancellation)
    {
        return await StartOrContinueTaskAsync(GameInstallOperation.UpdatePlugin, gameId, installPath, null, AudioLanguage.None, cancellation);
    }


    public async Task<GameInstallProgress?> StarHotUpdateAsync(GameId gameId, string installPath, CancellationToken cancellation)
    {
        if (gameId.GameBiz.Game is GameBiz.hk4e)
        {
            return await StartOrContinueTaskAsync(GameInstallOperation.HotUpdate, gameId, installPath, null, AudioLanguage.None, cancellation);
        }
        else
        {
            throw new NotSupportedException($"Hot update operation is not support for ({gameId})");
        }
    }


    public async Task<GameInstallProgress?> UpdateCutsceneAsync(GameId gameId, string installPath, List<string> blockList, CancellationToken cancellation)
    {
        if (gameId.GameBiz.Game is GameBiz.hk4e)
        {
            return await StartOrContinueTaskAsync(GameInstallOperation.UpdateCutscene, gameId, installPath, null, AudioLanguage.None, cancellation, blockList);
        }
        else
        {
            throw new NotSupportedException($"Update cutscene operation is not support for ({gameId})");
        }
    }


    private async Task<GameInstallProgress?> StartOrContinueTaskAsync(GameInstallOperation operation, GameId gameId, string installPath, string? categoryScenario, AudioLanguage audioLanguage, CancellationToken cancellation, List<string>? blockList = null)
    {
        LauncherConfig? launcher = _hoYoPlayService.GetLauncherConfig(gameId);
        if (launcher is null)
        {
            throw new ArgumentNullException($"LauncherConfig of game ({gameId}) is null");
        }
        var request = new GameInstallRequest
        {
            GameId = gameId,
            LauncherConfig = launcher,
            InstallPath = installPath,
            Operation = operation,
            CategoryScenario = categoryScenario,
            AudioLanguage = audioLanguage,
            PassportUserInfo = launcher.IsBeta ? _hoYoPlayService.PassportUserInfo : null,
            CutsceneBlockList = blockList ?? new List<string>(),
        };
        if (blockList is { Count: > 0 })
        {
            _cutsceneBlockLists[gameId] = blockList;
        }
        else
        {
            _cutsceneBlockLists.TryRemove(gameId, out _);
        }
        _logger.LogInformation("""
            Start game install task: 
            Operation: {operation}
            GameId: {gameId} {gameBiz}
            InstallPath: {installPath}
            AudioLanguage: {audioLanguage}
            """, operation, gameId.Id, gameId.GameBiz, installPath, audioLanguage);
        int limit = Math.Clamp(AppConfig.SpeedLimitKBPerSecond * 1024, 0, int.MaxValue);
        GameInstallRunner runner = _gameInstallRunner;
        runner.SetRateLimiter(limit > 0, limit);
        await runner.CreateContextAsync(request, cancellation).ConfigureAwait(false);
        GameInstallProgress progress = runner.StartOrContinueTask(request);
        StartUpdateTaskProgress();
        return AddOrUpdateProgress(progress);
    }


    /// <summary>
    /// Runner 直接给出 <see cref="GameInstallProgress"/>，这里按 GameId 归并到本地的任务表。
    /// </summary>
    private GameInstallProgress AddOrUpdateProgress(GameInstallProgress value)
    {
        GameId gameId = value.GameId;
        if (_tasks.TryGetValue(gameId, out GameInstallProgress? task))
        {
            // D135：与上游 AddOrUpdateTask 一致拷全 21 个字段。以前只合并 7 个，
            // 于是 Pause/Continue/Stop 的即时返回值里速度、字节数、剩余时间都是旧值。
            task.LauncherConfig = value.LauncherConfig;
            task.GameId = value.GameId;
            task.InstallPath = value.InstallPath;
            task.Operation = value.Operation;
            task.AudioLanguage = value.AudioLanguage;
            task.CategoryScenario = value.CategoryScenario;
            task.Timestamp = value.Timestamp;
            task.CurrentStep = value.CurrentStep;
            task.TotalStep = value.TotalStep;
            task.Step = value.Step;
            task.State = value.State;
            task.ErrorMessage = value.ErrorMessage;
            task.PercentProgress = value.PercentProgress;
            task.ProgressDisplay = value.ProgressDisplay;
            task.NetworkTotalBytes = value.NetworkTotalBytes;
            task.NetworkFinishedBytes = value.NetworkFinishedBytes;
            task.StorageTotalBytes = value.StorageTotalBytes;
            task.StorageFinishedBytes = value.StorageFinishedBytes;
            task.HardLinkBytes = value.HardLinkBytes;
            task.NetworkDownloadSpeed = value.NetworkDownloadSpeed;
            task.RemainTimeSeconds = value.RemainTimeSeconds;
        }
        else
        {
            task = value;
            _tasks.TryAdd(gameId, task);
        }
        // D134：与上游 AddOrUpdateTask 一致——Stop/Finish 之后任务不该留在表里，
        // 否则 _tasks 永不为空（后台变常驻 1 Hz 轮询），GetGameInstallTask 还会返回僵尸任务。
        if (task.State is RunnerState.Stop or RunnerState.Finish)
        {
            _tasks.TryRemove(gameId, out _);
        }
        return task;
    }


    public Task<GameInstallProgress> PauseTaskAsync(GameInstallProgress task)
    {
        // D135：以前声明成 async 却没有 await，等于在 UI 线程同步跑完；
        // Runner 这几个入口本身是同步落状态，用 Task.FromResult 明确表达。
        _logger.LogInformation("Pause game install task: {gameId} {gameBiz}", task.GameId.Id, task.GameId.GameBiz);
        GameInstallProgress progress = _gameInstallRunner.PauseTask(ToRequest(task));
        StartUpdateTaskProgress();
        return Task.FromResult(AddOrUpdateProgress(progress));
    }


    public Task<GameInstallProgress> ContinueTaskAsync(GameInstallProgress task)
    {
        // D135：以前声明成 async 却没有 await，等于在 UI 线程同步跑完；
        // Runner 这几个入口本身是同步落状态，用 Task.FromResult 明确表达。
        _logger.LogInformation("Continue game install task: {gameId} {gameBiz}", task.GameId.Id, task.GameId.GameBiz);
        GameInstallProgress progress = _gameInstallRunner.StartOrContinueTask(ToRequest(task));
        StartUpdateTaskProgress();
        return Task.FromResult(AddOrUpdateProgress(progress));
    }


    public Task<GameInstallProgress> StopTaskAsync(GameInstallProgress task)
    {
        // D135：以前声明成 async 却没有 await，等于在 UI 线程同步跑完；
        // Runner 这几个入口本身是同步落状态，用 Task.FromResult 明确表达。
        _logger.LogInformation("Stop game install task: {gameId} {gameBiz}", task.GameId.Id, task.GameId.GameBiz);
        GameInstallProgress progress = _gameInstallRunner.StopTask(ToRequest(task));
        StartUpdateTaskProgress();
        return Task.FromResult(AddOrUpdateProgress(progress));
    }


    private GameInstallRequest ToRequest(GameInstallProgress task)
    {
        return new GameInstallRequest
        {
            GameId = task.GameId,
            LauncherConfig = task.LauncherConfig,
            InstallPath = task.InstallPath,
            Operation = task.Operation,
            AudioLanguage = task.AudioLanguage,
            CategoryScenario = task.CategoryScenario,
            // D135：上游 ToRequestDTO 会带上登录票据（beta 渠道）与过场动画块列表；
            // 少了这两项，暂停后继续会丢票据、续传会漏块。
            PassportUserInfo = task.LauncherConfig.IsBeta ? _hoYoPlayService.PassportUserInfo : null,
            CutsceneBlockList = _cutsceneBlockLists.GetValueOrDefault(task.GameId) ?? new(),
        };
    }


    public async Task<bool> StartUninstallAsync(GameId gameId, string installPath, CancellationToken cancellation)
    {
        _logger.LogInformation("""
            Start uninstall game:
            GameId: {gameId}
            InstallPath: {installPath}
            """, gameId, installPath);
        var request = new UninstallGameRequest
        {
            GameId = gameId,
            InstallPath = installPath,
        };
        try
        {
            GameConfig? config = await _hoYoPlayService.GetGameConfigAsync(gameId, cancellation).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(config?.GameScreenshotDir))
            {
                request.ScreenshotPath = Path.GetFullPath(Path.Combine(installPath, config.GameScreenshotDir));
                if (Directory.Exists(AppConfig.ScreenshotFolder))
                {
                    request.ScreenshotBackupPath = Path.Combine(AppConfig.ScreenshotFolder, Path.GetFileNameWithoutExtension(config.ExeFileName));
                }
                else if (Directory.Exists(AppConfig.UserDataFolder))
                {
                    request.ScreenshotBackupPath = Path.Combine(AppConfig.UserDataFolder, "Screenshots", Path.GetFileNameWithoutExtension(config.ExeFileName));
                }
            }
        }
        catch { }
        UninstallGameResponse response = await _gameInstallRunner.UninstallGameAsync(request, cancellation).ConfigureAwait(false);
        if (response.Success)
        {
            _tasks.TryRemove(gameId, out _);
            return true;
        }
        else
        {
            throw new Exception(response.ErrorMessage);
        }
    }


}
