// Lunarward：归档解压在进程内完成（D73 §2.4，2026-11-14 改成库）。
//
// 以前用系统 tar/xz 外部进程，三个问题：取消不了（只能 Kill 进程树）、
// 违反「UI 线程不许 fork 外部进程」（D72，处处要 Task.Run）、依赖发行版是否装了 tar/xz。
// 现在：.tar / .tar.gz / .tar.zst 走 BCL 的 System.Formats.Tar + GZipStream + ZstdSharp，
// .tar.xz / .zip 走 SharpCompress（xz）与 BCL ZipFile（zip），全部可取消、可上报进度。
//
// 安全：所有条目都做路径逃逸检查（绝对路径、.. 越界一律拒绝），符号链接只在目标落在
// 目标目录内时才创建——归档来自网络，不能当成可信输入。
using Microsoft.Extensions.Logging;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Runtime;


/// <summary>解压进度：已完成的条目数与已写字节数。</summary>
public readonly record struct ArchiveProgress(int Entries, long Bytes);


/// <summary>把支持的归档解压到目标目录（进程内、可取消、有进度）。</summary>
public static class ArchiveExtractor
{
    /// <summary>单次读取的缓冲大小：1 MiB，足够让取消及时生效。</summary>
    private const int BufferSize = 1024 * 1024;


    /// <summary>支持的扩展名（用于界面提示与提前拒绝）。</summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } =
        new[] { ".tar", ".tar.gz", ".tgz", ".tar.zst", ".tar.xz", ".zip" };


    public static bool IsSupported(string path)
    {
        string lower = path.ToLowerInvariant();
        foreach (string ext in SupportedExtensions)
        {
            if (lower.EndsWith(ext, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }


    /// <summary>
    /// 解压归档。<paramref name="destination"/> 不存在时会创建。
    /// 不支持的格式抛 <see cref="NotSupportedException"/>，条目路径逃逸抛 <see cref="InvalidDataException"/>。
    /// </summary>
    public static async Task ExtractAsync(string archivePath, string destination,
        IProgress<ArchiveProgress>? progress = null, ILogger? logger = null, CancellationToken ct = default)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException($"找不到归档：{archivePath}", archivePath);
        }
        string lower = archivePath.ToLowerInvariant();
        Directory.CreateDirectory(destination);
        var state = new Counter();
        if (lower.EndsWith(".zip", StringComparison.Ordinal))
        {
            await ExtractZipAsync(archivePath, destination, state, progress, logger, ct).ConfigureAwait(false);
            return;
        }
        if (lower.EndsWith(".tar.xz", StringComparison.Ordinal))
        {
            await ExtractTarXzAsync(archivePath, destination, state, progress, logger, ct).ConfigureAwait(false);
            return;
        }
        if (!lower.EndsWith(".tar", StringComparison.Ordinal)
            && !lower.EndsWith(".tar.gz", StringComparison.Ordinal)
            && !lower.EndsWith(".tgz", StringComparison.Ordinal)
            && !lower.EndsWith(".tar.zst", StringComparison.Ordinal))
        {
            throw new NotSupportedException(
                $"不支持的归档格式：{Path.GetFileName(archivePath)}（支持 {string.Join("、", SupportedExtensions)}）");
        }

        await using FileStream file = File.OpenRead(archivePath);
        Stream body;
        if (lower.EndsWith(".tar.gz", StringComparison.Ordinal) || lower.EndsWith(".tgz", StringComparison.Ordinal))
        {
            body = new GZipStream(file, CompressionMode.Decompress, leaveOpen: false);
        }
        else if (lower.EndsWith(".tar.zst", StringComparison.Ordinal))
        {
            body = new ZstdSharp.DecompressionStream(file, leaveOpen: false);
        }
        else
        {
            body = file;
        }
        await using (body)
        {
            await ExtractTarStreamAsync(body, destination, state, progress, logger, ct).ConfigureAwait(false);
        }
    }


    private static async Task ExtractTarStreamAsync(Stream stream, string destination, Counter state,
        IProgress<ArchiveProgress>? progress, ILogger? logger, CancellationToken ct)
    {
        using var reader = new TarReader(stream, leaveOpen: true);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            string? name = entry.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }
            string target = ResolveTarget(destination, name);
            if (entry.EntryType is TarEntryType.Directory)
            {
                Directory.CreateDirectory(target);
                continue;
            }
            if (entry.EntryType is TarEntryType.SymbolicLink)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                TryCreateSymlink(target, entry.LinkName, destination, logger);
                state.Entries++;
                Report(state, progress);
                continue;
            }
            if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile
                or TarEntryType.ContiguousFile))
            {
                // 硬链接 / 设备节点 / PAX 元数据条目：跳过，不猜
                logger?.LogDebug("[components] 跳过条目 {Name}（{Type}）", name, entry.EntryType);
                continue;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using FileStream output = new(target, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
            if (entry.DataStream is not null)
            {
                await CopyAsync(entry.DataStream, output, state, progress, ct).ConfigureAwait(false);
            }
            TrySetMode(target, entry.Mode);
            state.Entries++;
            Report(state, progress);
        }
    }


    private static async Task ExtractTarXzAsync(string archivePath, string destination, Counter state,
        IProgress<ArchiveProgress>? progress, ILogger? logger, CancellationToken ct)
    {
        await using FileStream file = File.OpenRead(archivePath);
        // SharpCompress 自己识别外层压缩（xz）并给出 tar 条目；逐条写盘，方便取消与计时。
        using IReader reader = ReaderFactory.Open(file, new ReaderOptions { LeaveStreamOpen = false });
        while (reader.MoveToNextEntry())
        {
            ct.ThrowIfCancellationRequested();
            if (reader.Entry.IsDirectory)
            {
                Directory.CreateDirectory(ResolveTarget(destination, reader.Entry.Key));
                continue;
            }
            string target = ResolveTarget(destination, reader.Entry.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using FileStream output = new(target, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
            reader.WriteEntryTo(output);
            state.Entries++;
            state.Bytes += output.Length;
            Report(state, progress);
        }
    }


    private static async Task ExtractZipAsync(string archivePath, string destination, Counter state,
        IProgress<ArchiveProgress>? progress, ILogger? logger, CancellationToken ct)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                // 目录条目
                Directory.CreateDirectory(ResolveTarget(destination, entry.FullName));
                continue;
            }
            string target = ResolveTarget(destination, entry.FullName);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using Stream input = entry.Open();
            await using FileStream output = new(target, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
            await CopyAsync(input, output, state, progress, ct).ConfigureAwait(false);
            state.Entries++;
            Report(state, progress);
        }
    }


    private static async Task CopyAsync(Stream input, Stream output, Counter state,
        IProgress<ArchiveProgress>? progress, CancellationToken ct)
    {
        byte[] buffer = new byte[BufferSize];
        int read;
        while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            state.Bytes += read;
            Report(state, progress);
        }
    }


    private static void Report(Counter state, IProgress<ArchiveProgress>? progress)
    {
        progress?.Report(new ArchiveProgress(state.Entries, state.Bytes));
    }


    /// <summary>
    /// 把条目名解析成目标目录内的绝对路径；绝对路径或越界（..）一律拒绝。
    /// </summary>
    internal static string ResolveTarget(string destination, string name)
    {
        string normalized = name.Replace('\\', '/').TrimStart('/');
        string root = Path.GetFullPath(destination);
        string target = Path.GetFullPath(Path.Combine(root, normalized));
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!target.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"归档条目试图写到目标目录之外：{name}");
        }
        return target;
    }


    private static void TryCreateSymlink(string target, string? linkTarget, string destination, ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(linkTarget))
        {
            return;
        }
        // 目标也必须在解压目录内，否则拒绝（避免做出指向 /etc 的链接）
        string resolved;
        try
        {
            resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(target)!, linkTarget));
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[components] 符号链接目标无法解析：{Target} -> {Link}", target, linkTarget);
            return;
        }
        string root = Path.GetFullPath(destination);
        if (!resolved.StartsWith(root, StringComparison.Ordinal))
        {
            logger?.LogWarning("[components] 跳过可疑符号链接：{Target} -> {Link}", target, linkTarget);
            return;
        }
        try
        {
            if (File.Exists(target) || Directory.Exists(target))
            {
                File.Delete(target);
            }
            File.CreateSymbolicLink(target, linkTarget);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[components] 创建符号链接失败：{Target} -> {Link}", target, linkTarget);
        }
    }


    private static void TrySetMode(string path, UnixFileMode mode)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            if (mode != UnixFileMode.None && File.Exists(path))
            {
                File.SetUnixFileMode(path, mode);
            }
        }
        catch
        {
            // 权限位设置失败不影响解压结果（发行版里 tar 也会忽略）
        }
    }


    private sealed class Counter
    {
        public int Entries;
        public long Bytes;
    }
}
