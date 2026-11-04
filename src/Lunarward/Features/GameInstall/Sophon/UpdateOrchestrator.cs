// Lunarward：更新/预下载的组合编排。
//
// 官方启动器能把大版本更新压到几 GB，靠的是两件事叠加：
//   1) 差分包（HDiffPatch）：能差分的文件只下补丁；
//   2) chunk 级复用：其余变更文件只下新增的块，没变的块直接从本地搬。
// 这里把两者串成一条流水线，任何一步失败都退到下一步，最后由调用方兜底整文件下载：
//   补丁（失败→不记入已处理）→ chunk 复用（覆盖剩余变更文件）→ 整文件下载（调用方的 RunAsync）。
//
// 2026-11 调整：先一次性算出「这轮要下多少」（补丁体积 + 每个类别要下的块），再按这个总量上报进度。
// 否则进度条会先按补丁总量跑到 100%，再因为加入块下载掉回 30%，用户看到的是「进度倒退」。
// 代价是多一次清单枚举（实测 2.3 s / 20 GB 级更新，可接受）。
using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameInstall.Sophon;


public sealed class UpdateRunResult
{
    public int PatchedFiles { get; set; }

    public long PatchedBytes { get; set; }

    public int ReuseFiles { get; set; }

    public long DownloadedBytes { get; set; }

    public long ReusedBytes { get; set; }

    public int FailedFiles { get; set; }

    /// <summary>有没有因为调用方给的字节上限提前停下（开发期限量联调用）。</summary>
    public bool LimitReached { get; set; }

    public List<string> Errors { get; } = new();

    public long TotalDownloadBytes => PatchedBytes + DownloadedBytes;
}


public sealed class UpdatePlan
{
    public int PatchFiles { get; set; }

    public long PatchBytes { get; set; }

    public int ChangedFiles { get; set; }

    public long FullFileBytes { get; set; }

    public int ReuseFiles { get; set; }

    public long ReuseDownloadBytes { get; set; }

    public long ReuseReusedBytes { get; set; }

    public long TotalDownloadBytes => PatchBytes + ReuseDownloadBytes;
}


/// <summary>一个类别（game / zh-cn / …）在本轮要处理的文件与预估下载量。</summary>
internal sealed class ReuseFieldWork
{
    public required string Field { get; init; }

    public required string UrlPrefix { get; init; }

    public required List<SophonChunkFile> Files { get; init; }

    public required ChunkReuseStats Stats { get; init; }
}


/// <summary>「这一轮要做什么」的完整描述：补丁清单 + 每个类别的复用计划。</summary>
internal sealed class UpdateWork
{
    public required List<SophonPatchAsset> PatchAssets { get; init; }

    public required long PatchBytes { get; init; }

    public required Dictionary<string, (string File, long Offset, long Size)> ReuseIndex { get; init; }

    public required List<ReuseFieldWork> Fields { get; init; }

    public long TotalDownloadBytes => PatchBytes + Fields.Sum(x => x.Stats.DownloadBytes);

    public int TotalFiles => PatchAssets.Count + Fields.Sum(x => x.Files.Count);
}


public static class UpdateOrchestrator
{

    /// <summary>
    /// 执行「补丁 + chunk 复用」。返回 null 表示清单不可用，调用方应退回整文件下载。
    /// </summary>
    public static async Task<UpdateRunResult?> RunAsync(
        HttpClient httpClient,
        GameBranch branch,
        GameBranchPackage package,
        IEnumerable<string> fields,
        string installPath,
        bool preDownload,
        string localVersion,
        Action<InstallProgress>? onProgress,
        Func<bool>? isPaused,
        long limitBytes = long.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var fieldList = fields.ToList();
        var result = new UpdateRunResult();

        UpdateWork? work = await BuildWorkAsync(httpClient, branch, package, fieldList, installPath, preDownload, localVersion, cancellationToken);
        if (work is null)
        {
            return null;
        }
        Serilog.Log.Information("Update orchestrator: patches={patches} ({patchBytes} B), fields={fields}, planned={total} B",
                                work.PatchAssets.Count, work.PatchBytes, work.Fields.Count, work.TotalDownloadBytes);

        // 一次性把「补丁 + 块下载」的完整总量交给 UI，进度从 0 涨到 100%，不会倒退。
        var progress = new InstallProgress { TotalBytes = work.TotalDownloadBytes, TotalFiles = work.TotalFiles };
        onProgress?.Invoke(progress);

        // ---- 第一步：差分补丁 ----
        var patchedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long patchedBytes = 0;
        string patchWorkDir = Path.Combine(installPath, ".lunarward", "patch");
        foreach (SophonPatchAsset asset in work.PatchAssets)
        {
            if (patchedBytes >= limitBytes)
            {
                result.LimitReached = true;
                break;
            }
            await WaitWhilePausedAsync(isPaused, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string targetFile = asset.TargetFilePath;
            try
            {
                Directory.CreateDirectory(patchWorkDir);
                await asset.DownloadPatchAsync(httpClient, patchWorkDir, true, null, null, cancellationToken);
                await asset.ApplyPatchUpdateAsync(httpClient, installPath, patchWorkDir, false, null, null, null, cancellationToken);
                string full = Path.Combine(installPath, targetFile);
                string expected = asset.MainAssetInfo?.AssetHash ?? "";
                if (!string.IsNullOrEmpty(expected))
                {
                    string actual = File.Exists(full) ? Convert.ToHexString(await MD5.HashDataAsync(File.OpenRead(full), cancellationToken)) : "";
                    if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"补丁应用后 MD5 不符：{targetFile}");
                    }
                }
                patchedFiles.Add(targetFile.Replace('\\', '/'));
                result.PatchedFiles++;
                patchedBytes += asset.PatchSize;
                lock (progress)
                {
                    progress.FinishedFiles++;
                    progress.FinishedBytes = patchedBytes;
                }
                onProgress?.Invoke(progress);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 这条补丁失败不致命：没进 patchedFiles，chunk 复用会把整个文件重写一遍。
                Serilog.Log.Warning(ex, "Patch failed, will fall back to chunk reuse: {file}", targetFile);
                lock (result.Errors)
                {
                    if (result.Errors.Count < 10)
                    {
                        result.Errors.Add($"{targetFile}: {ex.GetType().Name}: {ex.Message}");
                    }
                }
                // 这条补丁的大小本来算在总量里，实际没下：扣掉，免得进度条永远差一截。
                lock (progress)
                {
                    progress.TotalBytes = Math.Max(0, progress.TotalBytes - asset.PatchSize);
                }
                onProgress?.Invoke(progress);
            }
        }
        TryDeleteDirectory(patchWorkDir);
        result.PatchedBytes = patchedBytes;

        // ---- 第二步：chunk 级复用（覆盖补丁没处理到的变更文件）----
        long downloaded = 0, reused = 0;
        foreach (ReuseFieldWork fieldWork in work.Fields)
        {
            // 补丁已经处理过的文件从本类别里剔掉（总文件数、总量同步扣减）。
            List<SophonChunkFile> need = fieldWork.Files
                .Where(f => !patchedFiles.Contains(f.File.Replace('\\', '/')))
                .ToList();
            if (need.Count == 0)
            {
                continue;
            }
            if (patchedBytes + downloaded + reused >= limitBytes)
            {
                result.LimitReached = true;
                break;
            }
            long remaining = limitBytes == long.MaxValue ? long.MaxValue : Math.Max(0, limitBytes - patchedBytes - downloaded - reused);
            if (remaining <= 0)
            {
                break;
            }
            ChunkReuseResult part = await ChunkReuseDownloader.RunAsync(
                httpClient, need, work.ReuseIndex, installPath, installPath, fieldWork.UrlPrefix, remaining,
                (chunkBytes, chunkTotal) =>
                {
                    lock (progress)
                    {
                        progress.FinishedBytes = patchedBytes + downloaded + reused + chunkBytes;
                    }
                    onProgress?.Invoke(progress);
                    return Task.CompletedTask;
                },
                cancellationToken, isPaused);
            downloaded += part.DownloadedBytes;
            reused += part.ReusedBytes;
            result.ReuseFiles += part.Files;
            result.DownloadedBytes += part.DownloadedBytes;
            result.ReusedBytes += part.ReusedBytes;
            result.FailedFiles += part.FailedFiles;
            lock (progress)
            {
                progress.FinishedFiles += part.Files;
                progress.FailedFiles += part.FailedFiles;
                progress.FinishedBytes = patchedBytes + downloaded + reused;
            }
            foreach (var (file, error) in part.Errors)
            {
                result.Errors.Add($"{file}: {error}");
            }
            onProgress?.Invoke(progress);
        }
        return result;
    }


    /// <summary>
    /// 只算不下的方案：补丁覆盖了多少文件、剩下的变更文件要下多少块、相比整文件省多少。
    /// </summary>
    public static async Task<UpdatePlan?> PlanAsync(
        HttpClient httpClient,
        GameBranch branch,
        GameBranchPackage package,
        IEnumerable<string> fields,
        string installPath,
        bool preDownload,
        string localVersion,
        CancellationToken cancellationToken = default)
    {
        var fieldList = fields.ToList();
        UpdateWork? work = await BuildWorkAsync(httpClient, branch, package, fieldList, installPath, preDownload, localVersion, cancellationToken);
        if (work is null)
        {
            return null;
        }

        var plan = new UpdatePlan
        {
            PatchFiles = work.PatchAssets.Count,
            PatchBytes = work.PatchBytes,
            // 补丁覆盖的文件也算「变更」，只是不需要块下载。
            ChangedFiles = work.PatchAssets.Count + work.Fields.Sum(x => x.Files.Count),
            FullFileBytes = work.PatchAssets.Sum(x => x.TargetFileSize) + work.Fields.Sum(x => x.Files.Sum(f => f.Size)),
            ReuseFiles = work.Fields.Sum(x => x.Files.Count),
            ReuseDownloadBytes = work.Fields.Sum(x => x.Stats.DownloadBytes),
            ReuseReusedBytes = work.Fields.Sum(x => x.Stats.ReusableBytes),
        };
        return plan;
    }


    /// <summary>一次性把补丁清单与每个类别的复用计划算出来，供执行和估算共用。</summary>
    private static async Task<UpdateWork?> BuildWorkAsync(
        HttpClient httpClient,
        GameBranch branch,
        GameBranchPackage package,
        List<string> fieldList,
        string installPath,
        bool preDownload,
        string localVersion,
        CancellationToken cancellationToken)
    {
        (List<SophonPatchAsset> patchAssets, long patchTotal) = await EnumeratePatchAssetsAsync(httpClient, package, fieldList, preDownload, localVersion, cancellationToken);

        var sophon = new SophonClient(AppConfig.GetService<HoYoPlayClient>());
        SophonBuildInfo? newBuild = await sophon.GetBuildInfoForPackageAsync(branch, package, preDownload, fieldList, cancellationToken);
        SophonBuildInfo? oldBuild = await sophon.GetBuildInfoForPackageAsync(branch, package, false, fieldList, cancellationToken, localVersion);
        if (newBuild is null || oldBuild is null)
        {
            return null;
        }

        var oldFiles = oldBuild.Manifests.Values.SelectMany(x => x).ToList();
        var reuseIndex = ChunkReusePlanner.BuildIndex(oldFiles);
        var oldHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (SophonChunkFile f in oldFiles)
        {
            if (!f.IsFolder)
            {
                oldHashes[f.File] = f.MD5;
            }
        }

        var patchedFiles = new HashSet<string>(patchAssets.Select(x => x.TargetFilePath.Replace('\\', '/')), StringComparer.OrdinalIgnoreCase);
        var fieldWork = new List<ReuseFieldWork>();
        foreach (var (field, files) in newBuild.Manifests)
        {
            if (!newBuild.ChunkUrlPrefixes.TryGetValue(field, out string? prefix) || string.IsNullOrEmpty(prefix))
            {
                continue;
            }
            var need = new List<SophonChunkFile>();
            foreach (SophonChunkFile f in files)
            {
                if (f.IsFolder)
                {
                    continue;
                }
                // 补丁会整文件重写的，不再走块下载。
                if (patchedFiles.Contains(f.File.Replace('\\', '/')))
                {
                    continue;
                }
                if (oldHashes.TryGetValue(f.File, out string? oldHash) && string.Equals(oldHash, f.MD5, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                need.Add(f);
            }
            if (need.Count == 0)
            {
                continue;
            }
            fieldWork.Add(new ReuseFieldWork
            {
                Field = field,
                UrlPrefix = prefix,
                Files = need,
                Stats = ChunkReusePlanner.Measure(reuseIndex, need, installPath),
            });
        }

        return new UpdateWork
        {
            PatchAssets = patchAssets,
            PatchBytes = patchTotal,
            ReuseIndex = reuseIndex,
            Fields = fieldWork,
        };
    }


    private static async Task WaitWhilePausedAsync(Func<bool>? isPaused, CancellationToken cancellationToken)
    {
        while (isPaused?.Invoke() == true)
        {
            await Task.Delay(200, cancellationToken);
        }
    }


    public static async Task<(List<SophonPatchAsset> Assets, long Bytes)> EnumeratePatchAssetsAsync(
        HttpClient httpClient,
        GameBranchPackage package,
        List<string> fieldList,
        bool preDownload,
        string localVersion,
        CancellationToken cancellationToken)
    {
        var patchAssets = new List<SophonPatchAsset>();
        foreach (string field in fieldList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                SophonChunkManifestInfoPair patchPair = await SophonLibrary.GetPatchPairAsync(httpClient, package, field, localVersion, cancellationToken);
                if (!patchPair.IsFound || patchPair.ChunksInfo.FilesCount <= 0)
                {
                    continue;
                }
                SophonChunkManifestInfoPair mainPair = await SophonLibrary.GetChunkPairAsync(httpClient, package, field, preDownload, cancellationToken);
                if (!mainPair.IsFound)
                {
                    continue;
                }
                await foreach (SophonPatchAsset asset in SophonPatch.EnumerateUpdateAsync(httpClient, patchPair, mainPair, localVersion, null, cancellationToken))
                {
                    if (asset is not null)
                    {
                        patchAssets.Add(asset);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Patch manifest unavailable: package={package} field={field}", package.PackageId, field);
            }
        }
        patchAssets = SophonPatch.EnsureOnlyGetDedupPatchAssets(patchAssets).ToList();
        return (patchAssets, patchAssets.Sum(x => x.PatchSize));
    }


    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (Exception)
        {
        }
    }

}
