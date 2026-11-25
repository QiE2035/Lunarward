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
            task.State = value.State;
            task.ErrorMessage = value.ErrorMessage;
            task.Step = value.Step;
            task.CurrentStep = value.CurrentStep;
            task.TotalStep = value.TotalStep;
            task.PercentProgress = value.PercentProgress;
            task.ProgressDisplay = value.ProgressDisplay;
        }
        else
        {
            task = value;
            _tasks.TryAdd(gameId, task);
        }
        return task;
    }


    public async Task<GameInstallProgress> PauseTaskAsync(GameInstallProgress task)
    {
        _logger.LogInformation("Pause game install task: {gameId} {gameBiz}", task.GameId.Id, task.GameId.GameBiz);
        GameInstallProgress progress = _gameInstallRunner.PauseTask(ToRequest(task));
        StartUpdateTaskProgress();
        return AddOrUpdateProgress(progress);
    }


    public async Task<GameInstallProgress> ContinueTaskAsync(GameInstallProgress task)
    {
        _logger.LogInformation("Continue game install task: {gameId} {gameBiz}", task.GameId.Id, task.GameId.GameBiz);
        GameInstallProgress progress = _gameInstallRunner.StartOrContinueTask(ToRequest(task));
        StartUpdateTaskProgress();
        return AddOrUpdateProgress(progress);
    }


    public async Task<GameInstallProgress> StopTaskAsync(GameInstallProgress task)
    {
        _logger.LogInformation("Stop game install task: {gameId} {gameBiz}", task.GameId.Id, task.GameId.GameBiz);
        GameInstallProgress progress = _gameInstallRunner.StopTask(ToRequest(task));
        StartUpdateTaskProgress();
        return AddOrUpdateProgress(progress);
    }


    private static GameInstallRequest ToRequest(GameInstallProgress task)
    {
        return new GameInstallRequest
        {
            GameId = task.GameId,
            LauncherConfig = task.LauncherConfig,
            InstallPath = task.InstallPath,
            Operation = task.Operation,
            AudioLanguage = task.AudioLanguage,
            CategoryScenario = task.CategoryScenario,
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
