// Lunarward：把 Hi3Helper.Sophon 接到我们的 GameId / 下载目录模型上。
//
// 为什么要用它：它把 Sophon 的三件事都实现了——清单拉取、分块下载（含限速与断点缓存）、
// 以及真正的差分更新（HDiffPatch 的纯托管实现，见 SharpHDiffPatch.Core）。
// 我们只保留「从 Starward.Core 拿 launcherId / package / password」这一段，其余交给它。

using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Helper;
using Hi3Helper.Sophon.Structs;
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameInstall.Sophon;


public static class SophonLibrary
{

    public static string BuildUrl(GameBranchPackage package, bool preDownload, string? tag = null)
    {
        // 预下载不是「换个 branch」，而是同一个 branch 上用 package.Tag 指向预下载版本；
        // 官方 getBuild 的 branch 参数就是 package.branch（L-59）。
        string branch = package.Branch ?? "main";
        // tag 留空 = 该 branch 的最新版本（上游也是这么调的：`branch=predownload&tag=`）。
        string tagValue = string.IsNullOrEmpty(tag) ? "" : tag;
        // tag 为空 = 该 branch 最新；指定 tag 可以取历史版本的清单（chunk 复用要靠它算「本地已有块」）。
        return $"https://downloader-api.mihoyo.com/downloader/sophon_chunk/api/getBuild?branch={branch}&package_id={package.PackageId}&password={package.Password}&tag={tagValue}";
    }


    public static string PatchUrl(GameBranchPackage package, bool preDownload)
    {
        // 预下载不是「换个 branch」，而是同一个 branch 上用 package.Tag 指向预下载版本；
        // 官方 getBuild 的 branch 参数就是 package.branch（L-59）。
        string branch = package.Branch ?? "main";
        // getPatchBuild 不带 tag：一次返回所有本地版本可用的补丁。
        return $"https://downloader-api.mihoyo.com/downloader/sophon_chunk/api/getPatchBuild?branch={branch}&package_id={package.PackageId}&password={package.Password}";
    }


    public static async Task<SophonChunkManifestInfoPair> GetChunkPairAsync(HttpClient httpClient, GameBranchPackage package, string matchingField, bool preDownload, CancellationToken cancellationToken)
    {
        return await SophonManifest.CreateSophonChunkManifestInfoPair(httpClient, BuildUrl(package, preDownload), matchingField, cancellationToken);
    }


    public static async Task<SophonChunkManifestInfoPair> GetPatchPairAsync(HttpClient httpClient, GameBranchPackage package, string matchingField, string versionUpdateFrom, CancellationToken cancellationToken)
    {
        return await SophonPatch.CreateSophonChunkManifestInfoPair(httpClient, PatchUrl(package, false), versionUpdateFrom, matchingField, cancellationToken);
    }


    /// <summary>
    /// 枚举清单里的所有文件条目（不含目录）。
    /// </summary>
    public static async IAsyncEnumerable<SophonAsset> EnumerateAssetsAsync(HttpClient httpClient, SophonChunkManifestInfoPair pair, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (SophonAsset asset in SophonManifest.EnumerateAsync(httpClient, pair, null, cancellationToken))
        {
            if (asset is not null && !asset.IsDirectory)
            {
                yield return asset;
            }
        }
    }


    /// <summary>
    /// 下载单个文件到 <paramref name="targetRoot"/>（保持相对路径）。
    /// </summary>
    public static async Task DownloadAssetAsync(HttpClient httpClient, SophonAsset asset, string targetRoot, Action<long, long>? progress = null, CancellationToken cancellationToken = default)
    {
        string target = Path.Combine(targetRoot, asset.AssetName);
        string? folder = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }
        // 库会在写完后回读流做分块校验，所以必须可读可写（踩过：FileAccess.Write 会抛 Output stream must be readable）。
        await using FileStream output = new(target, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20, useAsync: true);
        DelegateWriteDownloadInfo onDownload = (downloaded, written) => progress?.Invoke(downloaded, written);
        await asset.WriteToStreamAsync(httpClient, output, null, onDownload, null, cancellationToken);
        await output.FlushAsync(cancellationToken);
    }


    /// <summary>
    /// 应用一条差分补丁：旧文件在 <paramref name="inputDir"/>，产物写到 <paramref name="outputDir"/>。
    /// </summary>
    public static async Task ApplyPatchAssetAsync(HttpClient httpClient, SophonPatchAsset asset, string inputDir, string outputDir, Action<long>? progress = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDir);
        await asset.ApplyPatchUpdateAsync(httpClient, inputDir, outputDir, false, bytes => progress?.Invoke(bytes), null, null, cancellationToken);
    }

}
