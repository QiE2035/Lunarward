// Lunarward：分块下载的可复用管线（CLI 与安装服务共用）。
//
// 与 UI 无关：只负责「按清单把文件下到目标目录、校验 MD5、报告进度」，
// 进度通过回调抛出，调用方决定怎么展示（CLI 打印 / 写入 GameInstallContext）。

using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using Starward.Core;
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameInstall.Sophon;


public sealed class InstallProgress
{
    public long TotalBytes { get; set; }

    public long FinishedBytes { get; set; }

    public int TotalFiles { get; set; }

    public int FinishedFiles { get; set; }

    public int SkippedFiles { get; set; }

    public int FailedFiles { get; set; }

    public string CurrentFile { get; set; } = "";
}


public sealed class InstallPipelineResult
{
    public int Downloaded { get; init; }

    public int Skipped { get; init; }

    public int Failed { get; init; }

    public long TotalBytes { get; init; }

    public List<string> Errors { get; init; } = new();

    /// <summary>是否因为字节上限提前停下（开发期限量联调；正式安装恒为 false）。</summary>
    public bool LimitReached { get; init; }
}


public static class InstallPipeline
{

    /// <summary>
    /// 下载一个版本（或预下载）的全部文件。
    /// </summary>
    /// <param name="assetsOverride">测试用：只处理这些资产；null 表示按清单全量。</param>
    public static async Task<InstallPipelineResult> RunAsync(
        HttpClient httpClient,
        GameBranchPackage package,
        IEnumerable<string> fields,
        string outputRoot,
        bool preDownload,
        long limitBytes,
        int parallel,
        string? onlyFile,
        Action<InstallProgress>? onProgress,
        Func<bool>? isPaused,
        CancellationToken cancellationToken = default)
    {
        long planned = 0;
        bool limitHit = false;
        var assets = new List<SophonAsset>();
        foreach (string field in fields)
        {
            SophonChunkManifestInfoPair pair = await SophonLibrary.GetChunkPairAsync(httpClient, package, field, preDownload, cancellationToken);
            if (!pair.IsFound)
            {
                // 预下载清单不是每个版本都有；库在清单缺失时会抛 NRE，这里提前给出可读错误（L-59）。
                throw new InvalidOperationException($"{Starward.Language.Lang.Lunarward_Error_ManifestUnavailable}field={field}, branch={package.Branch}, tag={package.Tag}, code={pair.ReturnCode}");
            }
            await foreach (SophonAsset asset in SophonLibrary.EnumerateAssetsAsync(httpClient, pair, cancellationToken))
            {
                if (onlyFile is not null && !string.Equals(asset.AssetName, onlyFile, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (onlyFile is null && planned + asset.AssetSize > limitBytes)
                {
                    // 额度用尽：剩下的文件不再排队，并且要把「没装完」如实反映给调用方（L-66）。
                    limitHit = true;
                    continue;
                }
                assets.Add(asset);
                planned += asset.AssetSize;
            }
        }

        if (limitBytes == long.MaxValue)
        {
            // 只有「真装」才拦空间；限量下载是调试入口，让它自己撞墙更有用。
            UpdateOrchestrator.EnsureEnoughSpace(outputRoot, planned);
        }
        var progress = new InstallProgress { TotalBytes = planned, TotalFiles = assets.Count };
        onProgress?.Invoke(progress);

        int ok = 0, skipped = 0, failed = 0;
        long finishedBytes = 0;
        var errors = new List<string>();
        using var semaphore = new SemaphoreSlim(Math.Max(1, parallel));
        var tasks = new List<Task>();
        foreach (SophonAsset asset in assets)
        {
            await semaphore.WaitAsync(cancellationToken);
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // 暂停：每个文件开始前检查一次，暂停时下载中的文件会跑完，但不会再开新文件。
                    while (isPaused?.Invoke() == true)
                    {
                        await Task.Delay(200, cancellationToken);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    string target = Path.Combine(outputRoot, asset.AssetName);
                    if (File.Exists(target) && new FileInfo(target).Length == asset.AssetSize)
                    {
                        string localMd5 = await InstallPlanner.ComputeMD5Async(target, cancellationToken);
                        if (string.Equals(localMd5, asset.AssetHash, StringComparison.OrdinalIgnoreCase))
                        {
                            Interlocked.Increment(ref skipped);
                            Interlocked.Add(ref finishedBytes, asset.AssetSize);
                            lock (progress)
                            {
                                progress.SkippedFiles++;
                                progress.FinishedFiles++;
                                progress.FinishedBytes = Interlocked.Read(ref finishedBytes);
                            }
                            onProgress?.Invoke(progress);
                            return;
                        }
                    }
                    lock (progress)
                    {
                        progress.CurrentFile = asset.AssetName;
                    }
                    // 库回调里的 downloaded 是「本资产的累计下载量」，直接 += 会把同一批字节反复计入，
                    // 进度条提前冲到 100%（L-65）。这里按增量折算到全局已完成字节。
                    object reportGate = new();
                    long reported = 0;
                    await SophonLibrary.DownloadAssetAsync(httpClient, asset, outputRoot, (downloaded, _) =>
                    {
                        long delta;
                        lock (reportGate)
                        {
                            delta = downloaded - reported;
                            if (delta <= 0)
                            {
                                return;
                            }
                            reported = downloaded;
                        }
                        Interlocked.Add(ref finishedBytes, delta);
                        lock (progress)
                        {
                            progress.FinishedBytes = Interlocked.Read(ref finishedBytes);
                        }
                        onProgress?.Invoke(progress);
                    }, cancellationToken);
                    string doneMd5 = await InstallPlanner.ComputeMD5Async(target, cancellationToken);
                    if (!string.Equals(doneMd5, asset.AssetHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"MD5 不匹配（期望 {asset.AssetHash}，实际 {doneMd5}）");
                    }
                    Interlocked.Increment(ref ok);
                    lock (progress)
                    {
                        progress.FinishedFiles++;
                    }
                    onProgress?.Invoke(progress);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failed);
                    lock (errors)
                    {
                        if (errors.Count < 10)
                        {
                            errors.Add($"{asset.AssetName}: {ex.GetType().Name}: {ex.Message}");
                        }
                    }
                    lock (progress)
                    {
                        progress.FailedFiles++;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }
        await Task.WhenAll(tasks);
        return new InstallPipelineResult { Downloaded = ok, Skipped = skipped, Failed = failed, TotalBytes = planned, Errors = errors, LimitReached = limitHit };
    }

    /// <summary>
    /// 可断点续传的整包下载（安装 / 兜底）：所有文件都走 chunk 下载器，
    /// 中断后重跑会从临时文件已完成的块继续；已正确落盘的文件按 MD5 跳过。
    /// </summary>
    public static async Task<InstallPipelineResult> RunResumableAsync(
        HttpClient httpClient,
        SophonBuildInfo build,
        IEnumerable<string> fields,
        string outputRoot,
        long limitBytes,
        string? onlyFile,
        Action<InstallProgress>? onProgress,
        Func<bool>? isPaused,
        CancellationToken cancellationToken = default)
    {
        var perField = new Dictionary<string, List<SophonChunkFile>>(StringComparer.OrdinalIgnoreCase);
        long planned = 0;
        int plannedFiles = 0, skipped = 0, mismatchLogged = 0;
        bool limitHit = false;
        foreach (string field in fields)
        {
            if (!build.Manifests.TryGetValue(field, out List<SophonChunkFile>? files))
            {
                continue;
            }
            var need = new List<SophonChunkFile>();
            foreach (SophonChunkFile f in files)
            {
                if (f.IsFolder || f.Chunks is null || f.Chunks.Count == 0)
                {
                    continue;
                }
                if (onlyFile is not null && !string.Equals(f.File, onlyFile, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                // 已经下对的文件跳过（先比长度再比 MD5，避免每次启动都全量哈希）。
                string target = Path.Combine(outputRoot, f.File.Replace('\\', '/'));
                if (File.Exists(target) && new FileInfo(target).Length == f.Size)
                {
                    string md5 = await InstallPlanner.ComputeMD5Async(target, cancellationToken);
                    if (string.Equals(md5, f.MD5, StringComparison.OrdinalIgnoreCase))
                    {
                        skipped++;
                        continue;
                    }
                    if (mismatchLogged < 10)
                    {
                        mismatchLogged++;
                        Serilog.Log.Information("verify mismatch: {file} ({actual} != {expected})", f.File, md5, f.MD5);
                    }
                }
                else if (mismatchLogged < 10)
                {
                    mismatchLogged++;
                    Serilog.Log.Information("verify missing/size: {file} (exists={exists})", f.File, File.Exists(target));
                }
                if (onlyFile is null && planned + f.Size > limitBytes)
                {
                    // 额度用完就停在这里，并把「没装完」明确告诉调用方（否则会被当成安装成功）。
                    limitHit = true;
                    break;
                }
                need.Add(f);
                planned += f.Size;
            }
            if (need.Count > 0)
            {
                perField[field] = need;
                plannedFiles += need.Count;
            }
            if (limitHit)
            {
                break;
            }
        }

        if (limitBytes == long.MaxValue)
        {
            UpdateOrchestrator.EnsureEnoughSpace(outputRoot, planned);
        }
        var progress = new InstallProgress { TotalBytes = planned, TotalFiles = plannedFiles, SkippedFiles = skipped };
        onProgress?.Invoke(progress);
        long downloaded = 0, reused = 0;
        int ok = 0, failed = 0;
        var errors = new List<string>();
        var noReuse = new Dictionary<string, (string File, long Offset, long Size)>(StringComparer.Ordinal);
        foreach (var (field, files) in perField)
        {
            if (!build.ChunkUrlPrefixes.TryGetValue(field, out string? prefix) || string.IsNullOrEmpty(prefix))
            {
                errors.Add($"{field}: 没有 chunk 下载前缀");
                continue;
            }
            // 把剩余额度交给下载器：文件级预算挡不住单个大文件冲破额度（L-62/L-66）。
            long budgetLeft = limitBytes == long.MaxValue ? long.MaxValue : Math.Max(0, limitBytes - (downloaded + reused));
            ChunkReuseResult part = await ChunkReuseDownloader.RunAsync(
                httpClient, files, noReuse, outputRoot, outputRoot, prefix, budgetLeft,
                (chunkBytes, chunkTotal) =>
                {
                    lock (progress)
                    {
                        progress.FinishedBytes = downloaded + reused + chunkBytes;
                    }
                    onProgress?.Invoke(progress);
                    return Task.CompletedTask;
                },
                cancellationToken, isPaused);
            downloaded += part.DownloadedBytes;
            reused += part.ReusedBytes + part.ResumedBytes;
            if (part.LimitReached)
            {
                limitHit = true;
            }
            ok += part.Files;
            failed += part.FailedFiles;
            lock (progress)
            {
                progress.FinishedFiles += part.Files;
                progress.FailedFiles += part.FailedFiles;
            }
            foreach (var (file, error) in part.Errors)
            {
                errors.Add($"{file}: {error}");
            }
            onProgress?.Invoke(progress);
            if (limitHit)
            {
                break;
            }
        }
        lock (progress)
        {
            progress.FinishedBytes = downloaded + reused;
        }
        onProgress?.Invoke(progress);
        return new InstallPipelineResult { Downloaded = ok, Skipped = skipped, Failed = failed, TotalBytes = planned, Errors = errors, LimitReached = limitHit };
    }


    /// <summary>
    /// chunk 级增量更新：本地已有相同内容的块直接复用，只下载新增/变化的块。
    /// 与 <see cref="RunAsync"/> 的区别是数据源不同（这里用 chunk 清单而不是 SophonAsset），
    /// 因此单独一个入口；调用方按「有补丁用补丁、没补丁用本方法」的顺序编排。
    /// </summary>
    public static async Task<InstallPipelineResult> RunChunkReuseAsync(
        HttpClient httpClient,
        SophonBuildInfo newBuild,
        SophonBuildInfo oldBuild,
        string outputRoot,
        long limitBytes,
        Action<InstallProgress>? onProgress,
        Func<bool>? isPaused,
        CancellationToken cancellationToken = default)
    {
        var reuseIndex = ChunkReusePlanner.BuildIndex(oldBuild.Manifests.Values.SelectMany(x => x));
        var oldHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (SophonChunkFile f in oldBuild.Manifests.Values.SelectMany(x => x))
        {
            if (!f.IsFolder)
            {
                oldHashes[f.File] = f.MD5;
            }
        }

        // 只处理内容有变化的文件：整份没变的文件不参与，也不计入进度分母。
        var perField = new Dictionary<string, List<SophonChunkFile>>(StringComparer.OrdinalIgnoreCase);
        long plannedDownload = 0;
        int plannedFiles = 0;
        foreach (var (field, files) in newBuild.Manifests)
        {
            var need = new List<SophonChunkFile>();
            foreach (SophonChunkFile f in files)
            {
                if (f.IsFolder || (oldHashes.TryGetValue(f.File, out string? oldHash) && string.Equals(oldHash, f.MD5, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }
                need.Add(f);
            }
            if (need.Count == 0)
            {
                continue;
            }
            perField[field] = need;
            plannedFiles += need.Count;
            ChunkReuseStats stats = ChunkReusePlanner.Measure(reuseIndex, need, outputRoot);
            plannedDownload += stats.DownloadBytes;
        }

        var progress = new InstallProgress { TotalBytes = plannedDownload, TotalFiles = plannedFiles };
        onProgress?.Invoke(progress);
        long downloaded = 0, reused = 0;
        int ok = 0, failed = 0;
        var errors = new List<string>();
        foreach (var (field, files) in perField)
        {
            if (!newBuild.ChunkUrlPrefixes.TryGetValue(field, out string? prefix) || string.IsNullOrEmpty(prefix))
            {
                errors.Add($"{field}: 没有 chunk 下载前缀");
                continue;
            }
            ChunkReuseResult part = await ChunkReuseDownloader.RunAsync(
                httpClient, files, reuseIndex, outputRoot, outputRoot, prefix, limitBytes,
                (chunkBytes, chunkTotal) =>
                {
                    lock (progress)
                    {
                        progress.FinishedBytes = downloaded + reused + chunkBytes;
                    }
                    onProgress?.Invoke(progress);
                    return Task.CompletedTask;
                },
                cancellationToken, isPaused);
            downloaded += part.DownloadedBytes;
            reused += part.ReusedBytes;
            ok += part.Files;
            failed += part.FailedFiles;
            lock (progress)
            {
                progress.FinishedFiles += part.Files;
                progress.FailedFiles += part.FailedFiles;
            }
            foreach (var (file, error) in part.Errors)
            {
                errors.Add($"{file}: {error}");
            }
            onProgress?.Invoke(progress);
        }
        lock (progress)
        {
            progress.FinishedBytes = downloaded + reused;
        }
        onProgress?.Invoke(progress);
        return new InstallPipelineResult { Downloaded = ok, Skipped = 0, Failed = failed, TotalBytes = plannedDownload, Errors = errors };
    }

}
