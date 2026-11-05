// Lunarward：chunk 级增量下载。
//
// 目标：变了但大部分 chunk 没变的文件（例如 400 MB 的 pck）不再整文件重下，
// 而是把本地旧文件里「内容相同的块」直接搬过来，只下载新增/变化的块。
// 这也是官方启动器能把大版本预下载压到几 GB 的机制。
//
// 断点续传：临时文件按块顺序写，所以「临时文件长度」= 「已经完成到第几块」。
// 进程被杀/暂停后重跑，从块边界继续，块内的半截数据截掉重写；整文件 MD5 不符则丢掉重来一次。
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ZstdSharp;

namespace Starward.Features.GameInstall.Sophon;


public sealed class ChunkReuseResult
{
    public int Files { get; set; }
    public long DownloadedBytes { get; set; }
    public long ReusedBytes { get; set; }
    public long ResumedBytes { get; set; }
    public int FailedFiles { get; set; }
    /// <summary>因额度用完而停下（不是失败，也不是装完）。</summary>
    public bool LimitReached { get; set; }
    public List<(string File, string Error)> Errors { get; } = new();
}


public static class ChunkReuseDownloader
{
    public static async Task<ChunkReuseResult> RunAsync(
        HttpClient httpClient,
        IEnumerable<SophonChunkFile> files,
        IReadOnlyDictionary<string, (string File, long Offset, long Size)> reuseIndex,
        string oldInstallPath,
        string outputRoot,
        string chunkUrlPrefix,
        long limitBytes,
        Func<long, long, Task>? onProgress,
        CancellationToken cancellationToken = default,
        Func<bool>? isPaused = null)
    {
        var result = new ChunkReuseResult();
        long total = 0;
        // 复用要按块从旧文件里读。一个文件动辄几千块，每块都 open/close 会慢到不可接受，
        // 所以按路径缓存句柄（上限 32 个，够覆盖同类文件连续处理的场景）。
        using var oldFiles = new OldFileCache(oldInstallPath, 32);
        foreach (SophonChunkFile file in files)
        {
            // 暂停：每个文件开始前等一次，已开始的文件跑完（与 InstallPipeline 的语义一致）。
            while (isPaused?.Invoke() == true)
            {
                await Task.Delay(200, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (file.IsFolder || file.Chunks is null || file.Chunks.Count == 0)
            {
                continue;
            }
            if (limitBytes > 0 && total >= limitBytes)
            {
                result.LimitReached = true;
                break;
            }
            // 清单里的路径来自服务器，先规范化再确认没跑出安装目录（防 ../ 逃逸写坏别处）。
            string target = Path.GetFullPath(Path.Combine(outputRoot, file.File.Replace('\\', '/')));
            string rootFull = Path.GetFullPath(outputRoot);
            if (!target.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                result.FailedFiles++;
                if (result.Errors.Count < 10)
                {
                    result.Errors.Add((file.File, "清单路径超出安装目录"));
                }
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            string temp = target + ".lunarward-tmp";
            try
            {
                bool done = false;
                bool limitHit = false;
                for (int attempt = 0; attempt < 2 && !done; attempt++)
                {
                    // 第一次优先续传；MD5 不过说明前缀不可信，第二次从零开始。
                    var chunkSizes = file.Chunks.Select(c => c.UncompressedSize).ToList();
                    int startIndex = attempt == 0 && File.Exists(temp)
                                         ? ChunkResume.CountCompletedChunks(new FileInfo(temp).Length, chunkSizes)
                                         : 0;
                    long resumeLength = startIndex > 0 ? ChunkResume.LengthOfChunks(chunkSizes, startIndex) : 0;
                    if (startIndex > 0)
                    {
                        using (FileStream pre = new(temp, FileMode.Open, FileAccess.Write, FileShare.None))
                        {
                            pre.SetLength(resumeLength);
                        }
                        result.ResumedBytes += resumeLength;
                    }
                    else
                    {
                        TryDelete(temp);
                    }
                    long reusedHere = 0, downloadedHere = 0;
                    bool limitStopped = false;
                    try
                    {
                        await using (var output = startIndex > 0
                                           ? new FileStream(temp, FileMode.Append, FileAccess.Write, FileShare.None, 1 << 20, true)
                                           : new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, true))
                        {
                            // 按滑动窗口并发下载：顺序写盘保证 md5 正确，但网络请求最多 8 个同时在飞，
                            // 否则 2 万多个 chunk 串行下会慢到没法用（实测串行只有 5 MB/s）。
                            const int window = 8;
                            using var gate = new SemaphoreSlim(window);
                            var tasks = new System.Collections.Concurrent.ConcurrentDictionary<int, Task<byte[]>>();
                            int nextStart = startIndex;
                            int n = file.Chunks.Count;
                            for (int i = startIndex; i < n; i++)
                            {
                                while (nextStart < n && nextStart < i + window)
                                {
                                    int index = nextStart++;
                                    SophonChunk c = file.Chunks[index];
                                    byte[]? reused = oldFiles.TryRead(c, reuseIndex);
                                    if (reused is not null)
                                    {
                                        reusedHere += c.UncompressedSize;
                                        tasks[index] = Task.FromResult(reused);
                                    }
                                    else
                                    {
                                        downloadedHere += c.CompressedSize;
                                        total += c.CompressedSize;
                                        tasks[index] = DownloadWithGateAsync(gate, httpClient, chunkUrlPrefix, c, cancellationToken);
                                    }
                                }
                                byte[] data = await tasks[i];
                                tasks.TryRemove(i, out _);
                                // 额度按块生效：文件粒度判断会让单个大文件轻易冲过额度（L-62 的教训）。
                                if (limitBytes > 0 && total > limitBytes)
                                {
                                    limitStopped = true;
                                    break;
                                }
                                output.Write(data);
                            }
                        }
                        if (limitStopped)
                        {
                            // 额度用完：临时文件留在原地，下次续传接着写；既不算失败也不算完成。
                            result.LimitReached = true;
                            limitHit = true;
                            break;
                        }
                        result.ReusedBytes += reusedHere;
                        result.DownloadedBytes += downloadedHere;
                        string actual;
                        using (FileStream verifyStream = new(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true))
                        {
                            actual = Convert.ToHexString(await MD5.HashDataAsync(verifyStream, cancellationToken));
                        }
                        if (!string.IsNullOrEmpty(file.MD5) && !string.Equals(actual, file.MD5, StringComparison.OrdinalIgnoreCase))
                        {
                            if (startIndex > 0)
                            {
                                // 续传前缀不可信：回滚统计，丢掉临时文件，下一轮从头来。
                                Serilog.Log.Warning("Resumed file failed MD5, restarting from scratch: {file}", file.File);
                                result.ResumedBytes -= resumeLength;
                                result.ReusedBytes -= reusedHere;
                                result.DownloadedBytes -= downloadedHere;
                                total -= downloadedHere;
                                TryDelete(temp);
                                continue;
                            }
                            throw new InvalidDataException($"整文件 MD5 不符：{file.File}");
                        }
                        File.Move(temp, target, true);
                        result.Files++;
                        done = true;
                        if (onProgress is not null)
                        {
                            await onProgress(total, file.Chunks.Count);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                }
                if (limitHit)
                {
                    break;
                }
                if (!done)
                {
                    throw new InvalidDataException($"整文件 MD5 反复不符：{file.File}");
                }
            }
            catch (Exception ex)
            {
                result.FailedFiles++;
                if (result.Errors.Count < 10)
                {
                    result.Errors.Add((file.File, $"{ex.GetType().Name}: {ex.Message}"));
                }
                TryDelete(temp);
            }
        }
        return result;
    }


    /// <summary>旧安装目录里按 chunk id 取字节，句柄按文件缓存。</summary>
    private sealed class OldFileCache : IDisposable
    {
        private readonly string _root;
        private readonly int _capacity;
        private readonly Dictionary<string, FileStream> _open = new(StringComparer.Ordinal);

        public OldFileCache(string root, int capacity)
        {
            _root = root;
            _capacity = capacity;
        }

        public byte[]? TryRead(SophonChunk chunk, IReadOnlyDictionary<string, (string File, long Offset, long Size)> index)
        {
            if (!index.TryGetValue(chunk.Id, out var old) || chunk.UncompressedSize <= 0 || chunk.UncompressedSize > int.MaxValue)
            {
                return null;
            }
            try
            {
                FileStream? handle = GetOrOpen(Path.Combine(_root, old.File.Replace('\\', '/')));
                if (handle is null)
                {
                    return null;
                }
                handle.Position = old.Offset;
                byte[] buffer = new byte[chunk.UncompressedSize];
                int read = handle.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false);
                if (read != buffer.Length)
                {
                    return null;
                }
                // 原位更新时，同一目录里的文件可能已经被替换成新内容（块布局变了），
                // 只按 offset 读会拿到错字节；这里逐块校验，不符就当没有、回退下载。
                if (!string.IsNullOrEmpty(chunk.UncompressedMD5))
                {
                    string md5 = Convert.ToHexString(MD5.HashData(buffer));
                    if (!string.Equals(md5, chunk.UncompressedMD5, StringComparison.OrdinalIgnoreCase))
                    {
                        return null;
                    }
                }
                return buffer;
            }
            catch (Exception)
            {
                // 句柄坏了就丢掉，下次重新打开（按路径淘汰）。
                EvictAll();
                return null;
            }
        }

        private FileStream? GetOrOpen(string path)
        {
            if (_open.TryGetValue(path, out FileStream? cached))
            {
                return cached;
            }
            if (!File.Exists(path))
            {
                return null;
            }
            if (_open.Count >= _capacity)
            {
                // 简单淘汰：字典里最早插入的那个（Dictionary 不保证顺序，这里只求别无限增长）。
                string? victimPath = null;
                foreach (string key in _open.Keys)
                {
                    victimPath = key;
                    break;
                }
                if (victimPath is not null)
                {
                    Evict(victimPath);
                }
            }
            var handle = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1);
            _open[path] = handle;
            return handle;
        }

        private void Evict(string path)
        {
            if (_open.Remove(path, out FileStream? handle))
            {
                handle.Dispose();
            }
        }

        private void EvictAll()
        {
            foreach (FileStream handle in _open.Values)
            {
                handle.Dispose();
            }
            _open.Clear();
        }

        public void Dispose()
        {
            EvictAll();
        }
    }


    private static async Task<byte[]> DownloadWithGateAsync(SemaphoreSlim gate, HttpClient httpClient, string prefix, SophonChunk chunk, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            using var decompressor = new Decompressor();
            return await DownloadChunkAsync(httpClient, prefix, chunk, decompressor, cancellationToken);
        }
        finally
        {
            gate.Release();
        }
    }


    private static async Task<byte[]> DownloadChunkAsync(HttpClient httpClient, string prefix, SophonChunk chunk, Decompressor decompressor, CancellationToken cancellationToken)
    {
        // 单块重试：几万个块里总有几个会被网络抖掉，整文件失败重来的代价太大。
        string url = $"{prefix.TrimEnd('/')}/{chunk.Id}";
        byte[] compressed = null!;
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                compressed = await httpClient.GetByteArrayAsync(url, cancellationToken);
                break;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (attempt < 3)
            {
                Serilog.Log.Warning(ex, "Chunk download failed (attempt {attempt}): {id}", attempt, chunk.Id);
                await Task.Delay(400 * attempt, cancellationToken);
            }
        }
        if (!string.IsNullOrEmpty(chunk.CompressedMD5))
        {
            string md5 = Convert.ToHexString(MD5.HashData(compressed));
            if (!string.Equals(md5, chunk.CompressedMD5, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"chunk 压缩数据 MD5 不符：{chunk.Id}");
            }
        }
        // 不能用 Unwrap(src, dstSize)：个别条目的 uncompressed_size 与实际不符（实测 APMConfig.json
        // 声明 105 字节、实际 1024），带上限会直接抛 ZstdException。不限长度解压后用 MD5 兜底校验。
        byte[] data = decompressor.Unwrap(compressed).ToArray();
        if (chunk.UncompressedSize > 0 && data.Length != chunk.UncompressedSize)
        {
            throw new InvalidDataException($"chunk 解压长度不符：{chunk.Id} 期望 {chunk.UncompressedSize} 实际 {data.Length}");
        }
        if (!string.IsNullOrEmpty(chunk.UncompressedMD5))
        {
            string md5 = Convert.ToHexString(MD5.HashData(data));
            if (!string.Equals(md5, chunk.UncompressedMD5, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"chunk 解压后 MD5 不符：{chunk.Id}");
            }
        }
        return data;
    }


    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
        }
    }
}
