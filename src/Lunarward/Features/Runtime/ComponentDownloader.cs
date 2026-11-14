// Lunarward：受管组件的下载（D73 §2.3）。
//
// 串行单流是这类启动器的实际短板，所以单文件默认分片并行：
//   HEAD 探 Accept-Ranges → 支持就切 8 MiB 片、默认 4 连接、每片写 .part.<n>；
//   不支持就退回单流并提示；源端拒绝（429/403）连续两次则自动降为单连接。
// 断点续传：.part.<n> 大小已达标即视为该片完成，重跑直接跳过。
// 校验：合并后整体 SHA-256 一次，与应用内 digest 比对，不一致删掉重来。
//
// 注意：这里所有操作都必须在后台线程（D72：UI 线程不许做阻塞 IO / fork 外部进程）。
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Runtime;


public sealed record ComponentDownloadProgress(long Received, long? Total, int ActiveConnections, double BytesPerSecond)
{
    public double? Percent => Total is > 0 ? (double)Received / Total.Value * 100 : null;
}


/// <summary>源端不接受 Range 请求（连续两次被拒）时抛出，调用方降级为单连接。</summary>
internal sealed class RangeRejectedException : Exception
{
    public RangeRejectedException(string message) : base(message) { }
}


public sealed class ComponentDownloadResult
{
    public string FilePath { get; init; } = "";

    public long Bytes { get; init; }

    public string Sha256 { get; init; } = "";

    public int Connections { get; init; }

    public bool Resumed { get; init; }

    /// <summary>true = 因源端拒绝而降为单连接。</summary>
    public bool DegradedToSingle { get; init; }

    public bool DigestMatched { get; init; }
}


public static class ComponentDownloader
{
    public const int DefaultConnections = 4;

    public const int DefaultChunkSize = 8 * 1024 * 1024;

    /// <summary>连续多少个片被源端拒绝后降为单连接。</summary>
    private const int RejectThreshold = 2;


    /// <summary>分片计划：第 index 片的起点与长度（纯函数，便于单测）。</summary>
    public static (long Start, long Length) PlanChunk(long total, int index, int chunkSize)
        => ComponentDownloadMath.PlanChunk(total, index, chunkSize);


    public static int ChunkCount(long total, int chunkSize)
        => ComponentDownloadMath.ChunkCount(total, chunkSize);


    /// <summary>
    /// 把 <paramref name="url"/> 下载到 <paramref name="targetDirectory"/>/<paramref name="fileName"/>。
    /// 中间文件放在 <c>.download/</c> 下，成功后才原子改名，失败不污染目标文件。
    /// </summary>
    public static async Task<ComponentDownloadResult> DownloadAsync(
        HttpClient http,
        Uri url,
        string targetDirectory,
        string fileName,
        string? expectedSha256 = null,
        int connections = DefaultConnections,
        int chunkSize = DefaultChunkSize,
        ILogger? logger = null,
        IProgress<ComponentDownloadProgress>? progress = null,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(targetDirectory);
        string target = Path.Combine(targetDirectory, fileName);
        string workDir = Path.Combine(targetDirectory, ".download");
        Directory.CreateDirectory(workDir);
        string actual = ApplyMirror(url, null);

        (long? total, bool ranges) = await ProbeAsync(http, new Uri(actual), ct).ConfigureAwait(false);
        int useConnections = ranges ? Math.Clamp(connections, 1, 16) : 1;
        bool degraded = false;
        bool resumed = false;

        if (useConnections > 1 && total is > 0)
        {
            try
            {
                resumed = await DownloadChunkedAsync(http, actual, workDir, fileName, total.Value,
                    useConnections, chunkSize, logger, progress, ct).ConfigureAwait(false);
            }
            catch (RangeRejectedException ex)
            {
                logger?.LogWarning("[components] 源端拒绝分片（{Reason}），降为单连接下载：{Url}", ex.Message, actual);
                degraded = true;
                useConnections = 1;
                CleanupParts(workDir, fileName);
            }
        }

        if (degraded || useConnections == 1)
        {
            bool single = await DownloadSingleAsync(http, actual, workDir, fileName, total, logger, progress, ct)
                .ConfigureAwait(false);
            resumed |= single;
        }

        // 合并（分片）或直接用单流文件
        string assembled = Path.Combine(workDir, fileName);
        if (useConnections > 1)
        {
            MergeParts(workDir, fileName, assembled, total!.Value);
            CleanupParts(workDir, fileName);
        }

        if (!File.Exists(assembled))
        {
            throw new InvalidOperationException($"下载结果缺失：{assembled}");
        }

        string sha = await ComputeSha256Async(assembled, ct).ConfigureAwait(false);
        bool matched = string.IsNullOrWhiteSpace(expectedSha256)
            || string.Equals(sha, expectedSha256, StringComparison.OrdinalIgnoreCase);
        if (!matched)
        {
            // 不一致就删掉，让下一次重来（D73 §2.3）
            try
            {
                File.Delete(assembled);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[components] 删除校验失败的文件失败：{Path}", assembled);
            }
            throw new InvalidDataException($"SHA-256 不一致：期望 {expectedSha256}，实际 {sha}");
        }

        long bytes = new FileInfo(assembled).Length;
        File.Move(assembled, target, overwrite: true);
        logger?.LogInformation("[components] 下载完成 {File}（{Size} 字节，连接 {Connections}{Degraded}）",
            target, bytes, useConnections, degraded ? "，已降级" : "");

        return new ComponentDownloadResult
        {
            FilePath = target,
            Bytes = bytes,
            Sha256 = sha,
            Connections = useConnections,
            Resumed = resumed,
            DegradedToSingle = degraded,
            DigestMatched = matched,
        };
    }


    /// <summary>镜像前缀：<c>https://mirror/</c> + 原 URL 去掉协议。</summary>
    public static string ApplyMirror(Uri url, string? mirrorPrefix)
        => ComponentDownloadMath.ApplyMirror(url, mirrorPrefix);


    /// <summary>
    /// HEAD 探测：能拿到长度吗、支持 Range 吗。拿不到长度时按「不支持分片」处理。
    /// </summary>
    private static async Task<(long? Total, bool Ranges)> ProbeAsync(HttpClient http, Uri url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return (null, false);
            }
            long? length = response.Content.Headers.ContentLength;
            string? acceptRanges = response.Headers.TryGetValues("Accept-Ranges", out IEnumerable<string>? values)
                ? values.FirstOrDefault()
                : null;
            bool ranges = acceptRanges?.Contains("bytes", StringComparison.OrdinalIgnoreCase) == true;
            return (length, ranges && length is > 0);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return (null, false);
        }
    }


    private static async Task<bool> DownloadChunkedAsync(
        HttpClient http, string url, string workDir, string fileName, long total,
        int connections, int chunkSize, ILogger? logger,
        IProgress<ComponentDownloadProgress>? progress, CancellationToken ct)
    {
        int count = ChunkCount(total, chunkSize);
        bool resumed = false;
        long completed = 0;
        for (int i = 0; i < count; i++)
        {
            (long start, long length) = PlanChunk(total, i, chunkSize);
            string part = PartPath(workDir, fileName, i);
            if (File.Exists(part) && new FileInfo(part).Length == length)
            {
                completed += length;
                resumed = true;
            }
        }
        progress?.Report(new ComponentDownloadProgress(completed, total, 0, 0));

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var gate = new SemaphoreSlim(connections, connections);
        int rejects = 0;
        long received = Interlocked.Read(ref completed);
        var clock = Stopwatch.StartNew();
        long lastReported = 0;
        var tasks = new Task[count];
        for (int i = 0; i < count; i++)
        {
            int index = i;
            (long start, long length) = PlanChunk(total, index, chunkSize);
            string part = PartPath(workDir, fileName, index);
            if (File.Exists(part) && new FileInfo(part).Length == length)
            {
                tasks[index] = Task.CompletedTask;
                continue;
            }
            tasks[index] = Task.Run(async () =>
            {
                await gate.WaitAsync(linked.Token).ConfigureAwait(false);
                try
                {
                    if (linked.IsCancellationRequested)
                    {
                        return;
                    }
                    await DownloadRangeAsync(http, url, part, start, length, linked.Token).ConfigureAwait(false);
                }
                catch (RangeRejectedException)
                {
                    if (Interlocked.Increment(ref rejects) >= RejectThreshold)
                    {
                        linked.Cancel();
                        throw;
                    }
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException)
                {
                    if (Interlocked.Increment(ref rejects) >= RejectThreshold)
                    {
                        linked.Cancel();
                        throw new RangeRejectedException(ex.Message);
                    }
                }
                finally
                {
                    gate.Release();
                }
            }, linked.Token);
        }

        // 进度采样：任务进行中每 250 ms 报一次
        var sampler = Task.Run(async () =>
        {
            while (!linked.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(250, linked.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                long now = Interlocked.Read(ref received);
                double elapsed = clock.Elapsed.TotalSeconds;
                long delta = now - lastReported;
                lastReported = now;
                double speed = elapsed > 0 ? now / elapsed : 0;
                progress?.Report(new ComponentDownloadProgress(now, total, connections, speed));
                _ = delta;
            }
        }, linked.Token);

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // 由降级路径触发的取消
        }
        finally
        {
            linked.Cancel();
            try
            {
                await sampler.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 预期
            }
        }

        if (ct.IsCancellationRequested)
        {
            ct.ThrowIfCancellationRequested();
        }

        if (rejects >= RejectThreshold)
        {
            throw new RangeRejectedException("连续分片请求被拒绝");
        }

        _ = logger;
        return resumed;

        async Task DownloadRangeAsync(HttpClient client, string targetUrl, string partPath, long start, long length,
            CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
            request.Headers.Range = new RangeHeaderValue(start, start + length - 1);
            using HttpResponseMessage response = await client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.StatusCode is HttpStatusCode.RequestedRangeNotSatisfiable)
            {
                throw new RangeRejectedException("416 分片不可用");
            }
            if (response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.Forbidden)
            {
                throw new RangeRejectedException($"HTTP {(int)response.StatusCode}");
            }
            response.EnsureSuccessStatusCode();
            await using Stream source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await using var target = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024,
                useAsync: true);
            byte[] buffer = new byte[64 * 1024];
            long written = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                written += read;
                Interlocked.Add(ref received, read);
            }
            if (written != length)
            {
                // 半截片：删掉，下次重来
                target.Close();
                try
                {
                    File.Delete(partPath);
                }
                catch
                {
                    // 删不掉也不影响下次重下（会被长度校验拦下）
                }
                throw new IOException($"分片长度不符：期望 {length}，实际 {written}");
            }
        }
    }


    /// <summary>返回 true 表示复用了上一轮的半成品。</summary>
    private static async Task<bool> DownloadSingleAsync(
        HttpClient http, string url, string workDir, string fileName, long? total, ILogger? logger,
        IProgress<ComponentDownloadProgress>? progress, CancellationToken ct)
    {
        string single = Path.Combine(workDir, fileName);
        long have = File.Exists(single) ? new FileInfo(single).Length : 0;
        bool resumed = have > 0 && total is > 0 && have < total.Value;
        if (total is > 0 && have == total.Value)
        {
            return true;
        }
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (resumed)
        {
            request.Headers.Range = new RangeHeaderValue(have, null);
        }
        using HttpResponseMessage response = await http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using Stream source = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var target = new FileStream(single, have > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write,
            FileShare.None, 64 * 1024, useAsync: true);
        byte[] buffer = new byte[64 * 1024];
        long received = have;
        int read;
        while ((read = await source.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            received += read;
            progress?.Report(new ComponentDownloadProgress(received, total, 1, 0));
        }
        logger?.LogInformation("[components] 单流下载完成 {Bytes} 字节（续传：{Resumed}）", received, resumed);
        return resumed;
    }


    private static string PartPath(string workDir, string fileName, int index)
        => Path.Combine(workDir, $"{fileName}.part.{index}");


    private static void CleanupParts(string workDir, string fileName)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(workDir, $"{fileName}.part.*"))
            {
                File.Delete(file);
            }
        }
        catch
        {
            // 清理失败不影响结果
        }
    }


    private static void MergeParts(string workDir, string fileName, string target, long total)
    {
        int count = Directory.EnumerateFiles(workDir, $"{fileName}.part.*").Count();
        using var output = new FileStream(target, FileMode.Create, FileAccess.Write, FileShare.None, 256 * 1024);
        for (int i = 0; i < count; i++)
        {
            string part = PartPath(workDir, fileName, i);
            if (!File.Exists(part))
            {
                continue;
            }
            using var input = File.OpenRead(part);
            input.CopyTo(output);
        }
        output.Flush(flushToDisk: true);
        if (output.Length != total)
        {
            throw new InvalidDataException($"合并后长度 {output.Length} 与 Content-Length {total} 不一致");
        }
    }


    public static async Task<string> ComputeSha256Async(string path, CancellationToken ct = default)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 256 * 1024,
            useAsync: true);
        byte[] hash = await SHA256.HashDataAsync(stream, ct).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
