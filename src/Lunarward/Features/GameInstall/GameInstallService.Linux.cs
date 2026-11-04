// Lunarward：游戏安装 / 更新 / 预下载的 Linux 实现。
//
// 上游把这套流程放在 Starward.RPC 子进程里（gRPC + 命名管道，D6 决定换成 Unix socket）；
// Linux 侧 M3 先把流程直接放进主进程，接口与上游保持一致，UI 不用改。
// 差分（patch）分支还没在真实更新上验证过，所以这里只用分块（chunk）下载，
// 中断后续跑靠「文件级续传」（InstallPipeline 内的大小 + MD5 命中即跳过）。

using Microsoft.Extensions.DependencyInjection;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameInstall.Sophon;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.RPC.GameInstall;


public partial class GameInstallService
{

    private readonly ConcurrentDictionary<GameId, GameInstallContext> _tasks = new();

    private readonly ConcurrentDictionary<GameId, CancellationTokenSource> _cancellations = new();

    private readonly ConcurrentDictionary<GameId, bool> _paused = new();

    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(30) };


    public GameInstallContext? GetGameInstallTask(GameId gameId)
        => _tasks.TryGetValue(gameId, out GameInstallContext? task) ? task : null;


    public Task<GameInstallContext?> StartInstallAsync(GameId gameId, string installPath, AudioLanguage audio, GameInstallDownloadMode mode = GameInstallDownloadMode.Undefined)
        => StartAsync(gameId, installPath, audio, GameInstallOperation.Install, preDownload: false);


    public Task<GameInstallContext?> StartUpdateAsync(GameId gameId, string? installPath, AudioLanguage audio)
        => StartAsync(gameId, installPath ?? AppConfig.GetGameInstallPath(gameId.GameBiz) ?? "", audio, GameInstallOperation.Update, preDownload: false);


    public Task<GameInstallContext?> StartPredownloadAsync(GameId gameId, string? installPath, AudioLanguage audio)
        => StartAsync(gameId, installPath ?? AppConfig.GetGameInstallPath(gameId.GameBiz) ?? "", audio, GameInstallOperation.Predownload, preDownload: true);


    public Task<GameInstallContext?> StartRepairAsync(GameId gameId, string? installPath, AudioLanguage audio)
        => StartAsync(gameId, installPath ?? AppConfig.GetGameInstallPath(gameId.GameBiz) ?? "", audio, GameInstallOperation.Repair, preDownload: false);


    public async Task<bool> StartUninstallAsync(GameId gameId, string? installPath)
    {
        string? path = installPath ?? AppConfig.GetGameInstallPath(gameId.GameBiz);
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return false;
        }
        // 只删「看起来是游戏目录」的路径（含 config.ini 或可执行文件），避免误删。
        if (!File.Exists(Path.Combine(path, "config.ini")))
        {
            return false;
        }
        try
        {
            await Task.Run(() => Directory.Delete(path, recursive: true));
            AppConfig.SetGameInstallPath(gameId.GameBiz, null);
            return true;
        }
        catch
        {
            return false;
        }
    }


    public Task PauseTaskAsync(GameInstallContext context)
    {
        _paused[context.GameId] = true;
        context.State = GameInstallState.Paused;
        return Task.CompletedTask;
    }


    public Task ContinueTaskAsync(GameInstallContext context)
    {
        _paused[context.GameId] = false;
        context.State = GameInstallState.Downloading;
        return Task.CompletedTask;
    }


    public Task StopTaskAsync(GameInstallContext context)
    {
        if (_cancellations.TryRemove(context.GameId, out CancellationTokenSource? cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
        _paused[context.GameId] = false;
        context.State = GameInstallState.Stop;
        return Task.CompletedTask;
    }


    private async Task<GameInstallContext?> StartAsync(GameId gameId, string installPath, AudioLanguage audio, GameInstallOperation operation, bool preDownload)
    {
        if (string.IsNullOrEmpty(installPath))
        {
            return null;
        }
        if (_tasks.TryGetValue(gameId, out GameInstallContext? existing)
            && existing.State is GameInstallState.Downloading or GameInstallState.Verifying or GameInstallState.Waiting)
        {
            return existing;
        }
        var context = new GameInstallContext
        {
            GameId = gameId,
            InstallPath = installPath,
            Operation = operation,
            AudioLanguage = audio,
            State = GameInstallState.Waiting,
            DownloadMode = GameInstallDownloadMode.Chunk,
            Timestamp = DateTimeOffset.Now.ToUnixTimeSeconds(),
        };
        var cts = new CancellationTokenSource();
        // 原子占位：同一游戏同时只允许一个任务，避免预下载/更新互相踩（D39）。
        if (!_cancellations.TryAdd(gameId, cts))
        {
            cts.Dispose();
            Serilog.Log.Information("Game install skipped: {game} already has a running task", gameId.GameBiz);
            return _tasks.TryGetValue(gameId, out GameInstallContext? running) ? running : null;
        }
        _tasks[gameId] = context;
        _paused[gameId] = false;
        _ = Task.Run(() => RunAsync(context, preDownload, cts.Token));
        await Task.CompletedTask;
        return context;
    }


    private async Task RunAsync(GameInstallContext context, bool preDownload, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        long lastBytes = 0;
        var lastTick = DateTime.UtcNow;
        try
        {
            context.State = GameInstallState.Waiting;
            Directory.CreateDirectory(context.InstallPath);
            GameId gameId = context.GameId;
            var hoYoPlayClient = AppConfig.GetService<HoYoPlayClient>();
            string launcherId = LauncherId.FromGameId(gameId)!;
            GameBranch? branch = await hoYoPlayClient.GetGameBranchAsync(launcherId, "zh-cn", gameId, cancellationToken);
            GameBranchPackage? package = preDownload ? branch?.PreDownload : branch?.Main;
            if (package is null)
            {
                context.State = GameInstallState.Error;
                context.ErrorMessage = Starward.Language.Lang.Lunarward_Error_NoVersionInfo;
                return;
            }

            var fields = new List<string> { "game" };
            List<string> audioFields = SophonClient.DetectInstalledAudioFields(context.InstallPath);
            if (context.Operation is GameInstallOperation.Install && audioFields.Count == 0)
            {
                // 全新安装：按所选语音装一个包（音频清单文件本身在下载内容里，不在这里造）。
                string? mapped = SophonClient.MapAudioField(context.AudioLanguage.ToString());
                if (!string.IsNullOrEmpty(mapped))
                {
                    audioFields = new List<string> { mapped };
                }
            }
            fields.AddRange(audioFields);

            // 进度回调：两条路径（整文件下载 / chunk 级复用）共用同一份上报逻辑。
            void Report(InstallProgress progress)
            {
                context.Progress_DownloadTotalBytes = progress.TotalBytes;
                context.Progress_DownloadFinishBytes = progress.FinishedBytes;
                // 我们是边下边写，写入量与下载量同步；上游「更新 + Chunk 模式」的进度环读的是写入量，
                // 不填会一直是 0/0 → UI 显示 NaN%。见 L-65。
                context.Progress_WriteTotalBytes = progress.TotalBytes;
                context.Progress_WriteFinishBytes = progress.FinishedBytes;
                context.Progress_Percent = progress.TotalBytes > 0 ? Math.Min(1d, (double) progress.FinishedBytes / progress.TotalBytes) : 0d;
                DateTime now = DateTime.UtcNow;
                double seconds = (now - lastTick).TotalSeconds;
                if (seconds >= 0.5)
                {
                    long delta = progress.FinishedBytes - lastBytes;
                    context.NetworkDownloadSpeed = (long) (delta / seconds);
                    long remain = context.NetworkDownloadSpeed > 0
                                  ? (progress.TotalBytes - progress.FinishedBytes) / context.NetworkDownloadSpeed
                                  : 0;
                    context.RemainTimeSeconds = remain;
                    lastBytes = progress.FinishedBytes;
                    lastTick = now;
                }
            }

            // 开发期限额：Debug 构建下可用环境变量把一次安装/更新限制在 N MB，
            // 便于在真机上验证进度、暂停、断点续传而不必真下 20 GB（D35：Release 不含此入口）。
            long devLimitBytes = long.MaxValue;
#if DEBUG
            if (long.TryParse(Environment.GetEnvironmentVariable("LUNARWARD_DEV_INSTALL_LIMIT_MB"), out long devLimitMb) && devLimitMb > 0)
            {
                devLimitBytes = devLimitMb * 1024 * 1024;
                Serilog.Log.Information("Game install dev limit: {mb} MB", devLimitMb);
            }
#endif

            context.State = GameInstallState.Downloading;

            // 更新/预下载优先走「补丁 + chunk 复用」的组合编排：官方能只下几 GB 就是这两件事叠加。
            // 拿不到本地版本、清单不可用或整体失败，就落回整文件下载，不打断安装流程。
            InstallPipelineResult? reused = null;

            // 修复：不和最新版本比，而是按「本地版本自己的清单」逐个文件校验（长度 + MD5），
            // 只有不对的才重下——这才是「校验文件」，否则修复会变成顺手更新。
            if (context.Operation is GameInstallOperation.Repair && branch is not null)
            {
                string? repairVersion = SophonClient.TryReadLocalVersion(context.InstallPath);
                if (string.IsNullOrEmpty(repairVersion))
                {
                    Serilog.Log.Warning("Repair without local version, falling back to full download: {game}", gameId.GameBiz);
                }
                else
                {
                    try
                    {
                        Serilog.Log.Information("Game repair verifies version {version}: {game}", repairVersion, gameId.GameBiz);
                        SophonBuildInfo? repairBuild = await new SophonClient(hoYoPlayClient)
                            .GetBuildInfoAsync(gameId, preDownload: false, fields, cancellationToken, repairVersion);
                        if (repairBuild is null)
                        {
                            Serilog.Log.Warning("Repair manifest unavailable for {version}, falling back", repairVersion);
                        }
                        else
                        {
                            reused = await InstallPipeline.RunResumableAsync(
                                _httpClient, repairBuild, fields, context.InstallPath, devLimitBytes, null, Report, Paused, cancellationToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "Repair verify failed, falling back to full download: {game}", gameId.GameBiz);
                        reused = null;
                    }
                }
            }

            if (reused is null && context.Operation is GameInstallOperation.Update or GameInstallOperation.Predownload && branch is not null)
            {
                string? localVersion = SophonClient.TryReadLocalVersion(context.InstallPath);
                if (!string.IsNullOrEmpty(localVersion))
                {
                    try
                    {
                        Serilog.Log.Information("Game install uses patch+reuse: {game} from {version}", gameId.GameBiz, localVersion);
                        UpdateRunResult? orchestrated = await UpdateOrchestrator.RunAsync(
                            _httpClient,
                            branch,
                            package,
                            fields,
                            context.InstallPath,
                            preDownload,
                            localVersion,
                            onProgress: Report,
                            isPaused: () => _paused.TryGetValue(context.GameId, out bool paused) && paused,
                            limitBytes: devLimitBytes,
                            cancellationToken: cancellationToken);
                        if (orchestrated is not null && orchestrated.FailedFiles == 0)
                        {
                            Serilog.Log.Information("Patch+reuse finished: patched={patched} reuse={reuse} download={downloaded} reused={reused}",
                                                    orchestrated.PatchedFiles, orchestrated.ReuseFiles,
                                                    orchestrated.DownloadedBytes, orchestrated.ReusedBytes);
                            reused = new InstallPipelineResult
                            {
                                Downloaded = orchestrated.PatchedFiles + orchestrated.ReuseFiles,
                                Skipped = 0,
                                Failed = orchestrated.FailedFiles,
                                TotalBytes = orchestrated.TotalDownloadBytes,
                                Errors = orchestrated.Errors,
                                LimitReached = orchestrated.LimitReached,
                            };
                        }
                        else if (orchestrated is not null)
                        {
                            // 有零散失败：剩下的交给整文件下载补齐（会跳过已正确的文件）。
                            Serilog.Log.Warning("Patch+reuse left {failed} failed files, completing with full download", orchestrated.FailedFiles);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "Patch+reuse failed, falling back to full download: {game}", gameId.GameBiz);
                        reused = null;
                    }
                }
            }

            bool Paused() => _paused.TryGetValue(context.GameId, out bool p) && p;
            InstallPipelineResult? result = reused;
            if (result is null)
            {
                // 兜底也尽量可续传：中断后重跑从临时文件已完成的块继续，而不是整个文件重下。
                try
                {
                    SophonBuildInfo? build = await new SophonClient(hoYoPlayClient).GetBuildInfoAsync(gameId, preDownload, fields, cancellationToken);
                    if (build is not null)
                    {
                        result = await InstallPipeline.RunResumableAsync(
                            _httpClient, build, fields, context.InstallPath, devLimitBytes, null, Report, Paused, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "Resumable download unavailable, falling back to asset download: {game}", gameId.GameBiz);
                }
            }
            result ??= await InstallPipeline.RunAsync(
                _httpClient,
                package,
                fields,
                context.InstallPath,
                preDownload,
                limitBytes: devLimitBytes,
                parallel: 4,
                onlyFile: null,
                onProgress: Report,
                isPaused: Paused,
                cancellationToken: cancellationToken);

            if (result.Failed > 0)
            {
                context.State = GameInstallState.Error;
                context.ErrorMessage = string.Join(Environment.NewLine, result.Errors);
                return;
            }

            if (result.LimitReached)
            {
                // 开发期限量：只下了这么多，绝不能当成装完（否则会写版本标记）。
                context.State = GameInstallState.Paused;
                context.NetworkDownloadSpeed = 0;
                context.RemainTimeSeconds = 0;
                Serilog.Log.Information("Game install dev limit reached: {game} {op} files={files} bytes={bytes}",
                                        context.GameId.GameBiz, context.Operation, result.Downloaded, result.TotalBytes);
                return;
            }

            // 版本标记：预下载不写（否则启动器会以为已经更新完）；修复保持原版本不动；更新/安装才写。
            if (!preDownload && context.Operation is not GameInstallOperation.Repair)
            {
                GameVersionMarker.WriteAll(context.InstallPath, package.Tag);
            }
            // 收尾对齐：修复不改版本，但进度条也要走满。
            context.Progress_DownloadFinishBytes = context.Progress_DownloadTotalBytes;
            context.Progress_WriteFinishBytes = context.Progress_WriteTotalBytes;
            context.Progress_Percent = 1d;
            context.State = GameInstallState.Finish;
            Serilog.Log.Information("Game install finished: {game} {op} files={downloaded} skipped={skipped} bytes={bytes} in {ms} ms",
                                    context.GameId.GameBiz, context.Operation, result.Downloaded, result.Skipped, result.TotalBytes, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException)
        {
            context.State = GameInstallState.Stop;
        }
        catch (Exception ex)
        {
            context.State = GameInstallState.Error;
            context.ErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            Serilog.Log.Error(ex, "Game install failed: {game}", context.GameId.GameBiz);
        }
        finally
        {
            _paused.TryRemove(context.GameId, out _);
            if (_cancellations.TryRemove(context.GameId, out CancellationTokenSource? cts))
            {
                cts.Dispose();
            }
        }
    }

}
