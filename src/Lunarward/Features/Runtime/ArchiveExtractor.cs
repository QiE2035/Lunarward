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

    /// <summary>R60：统一上限——条目数、单文件、总解压体积，所有格式一致。</summary>
    private const int MaxEntries = 500_000;

    private const long MaxSingleFileBytes = 4L << 30;

    private const long AbsoluteSizeCap = 8L << 30;

    private const long SlackBytes = 512L << 20;


    /// <summary>
    /// R60：按归档体积估算解压上限：至少「归档 + 512 MB」，至多 8 GiB，
    /// 中间按 25 倍压缩比放宽（xz 对二进制通常 5–10 倍）。未压缩 .tar 不会被误杀。
    /// </summary>
    private static long SizeLimitFor(string archivePath)
    {
        long length = 0;
        try
        {
            length = new FileInfo(archivePath).Length;
        }
        catch
        {
            // 拿不到大小就按 2 GiB 兜底
        }
        if (length <= 0)
        {
            return 2L << 30;
        }
        long scaled = length > (long.MaxValue / 25) ? long.MaxValue : length * 25;
        return Math.Max(length + SlackBytes, Math.Min(AbsoluteSizeCap, scaled));
    }


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
        long sizeLimit = SizeLimitFor(archivePath);
        if (lower.EndsWith(".zip", StringComparison.Ordinal))
        {
            await ExtractZipAsync(archivePath, destination, state, sizeLimit, progress, logger, ct).ConfigureAwait(false);
            return;
        }
        if (lower.EndsWith(".tar.xz", StringComparison.Ordinal))
        {
            await ExtractTarXzAsync(archivePath, destination, state, sizeLimit, progress, logger, ct).ConfigureAwait(false);
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
            await ExtractTarStreamAsync(body, destination, state, sizeLimit, progress, logger, ct).ConfigureAwait(false);
        }
    }


    private static async Task ExtractTarStreamAsync(Stream stream, string destination, Counter state,
        long sizeLimit, IProgress<ArchiveProgress>? progress, ILogger? logger, CancellationToken ct)
    {
        using var reader = new TarReader(stream, leaveOpen: true);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (state.Entries > MaxEntries)
            {
                throw new InvalidDataException($"条目数超过上限（{MaxEntries}），疑似压缩炸弹");
            }
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
                await CopyAsync(entry.DataStream, output, state, progress, ct, sizeLimit).ConfigureAwait(false);
            }
            TrySetMode(target, entry.Mode);
            state.Entries++;
            Report(state, progress);
        }
    }


    private static async Task ExtractTarXzAsync(string archivePath, string destination, Counter state,
        long sizeLimit, IProgress<ArchiveProgress>? progress, ILogger? logger, CancellationToken ct)
    {
        await using FileStream file = File.OpenRead(archivePath);
        // SharpCompress 自己识别外层压缩（xz）并给出 tar 条目；逐条写盘，方便取消与计时。
        using IReader reader = ReaderFactory.Open(file, new ReaderOptions { LeaveStreamOpen = false });
        while (reader.MoveToNextEntry())
        {
            ct.ThrowIfCancellationRequested();
            if (state.Entries > MaxEntries)
            {
                throw new InvalidDataException($"条目数超过上限（{MaxEntries}），疑似压缩炸弹");
            }
            if (reader.Entry.IsDirectory)
            {
                Directory.CreateDirectory(ResolveTarget(destination, reader.Entry.Key));
                continue;
            }
            string target = ResolveTarget(destination, reader.Entry.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using FileStream output = new(target, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize);
            await using Stream input = reader.OpenEntryStream();
            await CopyAsync(input, output, state, progress, ct, sizeLimit).ConfigureAwait(false);
            // R43：SharpCompress 的 tar 条目不带 Mode（只有 BCL TarReader 有），解压后按 ELF 文件头兜底补执行位。
            state.Entries++;
            Report(state, progress);
        }
        EnsureExecutableBits(destination, logger);
    }


    private static async Task ExtractZipAsync(string archivePath, string destination, Counter state,
        long sizeLimit, IProgress<ArchiveProgress>? progress, ILogger? logger, CancellationToken ct)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            ct.ThrowIfCancellationRequested();
            if (state.Entries > MaxEntries)
            {
                throw new InvalidDataException($"条目数超过上限（{MaxEntries}），疑似压缩炸弹");
            }
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
            await CopyAsync(input, output, state, progress, ct, sizeLimit).ConfigureAwait(false);
            // R43：zip 的 Unix 权限位在外层属性高 16 位（由 zip 工具写入）；没有就不设，最后统一兜底。
            int unixMode = (entry.ExternalAttributes >> 16) & 0xFFF;
            if (unixMode != 0)
            {
                TrySetMode(target, (UnixFileMode)unixMode);
            }
            state.Entries++;
            Report(state, progress);
        }
        EnsureExecutableBits(destination, logger);
    }


    private static async Task CopyAsync(Stream input, Stream output, Counter state,
        IProgress<ArchiveProgress>? progress, CancellationToken ct, long sizeLimit = 0)
    {
        byte[] buffer = new byte[BufferSize];
        int read;
        long written = 0;
        while ((read = await input.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
            state.Bytes += read;
            written += read;
            if (written > MaxSingleFileBytes)
            {
                throw new InvalidDataException($"单个文件解压超过上限（{MaxSingleFileBytes / 1024 / 1024} MB），疑似压缩炸弹");
            }
            if (sizeLimit > 0 && state.Bytes > sizeLimit)
            {
                throw new InvalidDataException($"解压体积超过上限（{sizeLimit / 1024 / 1024} MB），疑似压缩炸弹");
            }
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
        string raw = name.Replace('\\', '/');
        // R32：归档里的绝对路径不是「相对根目录的路径」，以前 TrimStart('/') 会把它悄悄改写进目标目录，
        // 注释写着拒绝、实际接受。Windows 盘符（C:/...）同样拒绝。
        if (raw.StartsWith('/')
            || raw.StartsWith("//", StringComparison.Ordinal)
            || (raw.Length >= 2 && char.IsAsciiLetter(raw[0]) && raw[1] == ':'))
        {
            throw new InvalidDataException($"归档条目是绝对路径，拒绝解压：{name}");
        }
        string normalized = raw;
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
        // R32：和 ResolveTarget 一样必须带分隔符，否则 /tmp/extract-evil 会被当成 /tmp/extract 的后代。
        string rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(rootWithSeparator, StringComparison.Ordinal))
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


    /// <summary>
    /// R43：把「解压出来的 ELF 可执行文件」补上 u+x。
    /// tar.xz（SharpCompress）拿不到条目 Mode，zip 也常不带权限位，而 <c>ComponentStore</c> 用执行位判断组件是否可用；
    /// 只加位、不夺权，脚本/文本文件不动（wine 的 bin 全是 ELF）。
    /// </summary>
    private static void EnsureExecutableBits(string destination, ILogger? logger)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            int fixedCount = 0;
            foreach (string path in Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories))
            {
                FileInfo info = new(path);
                if (info.LinkTarget is not null || info.Length < 4 || !IsElfFile(path))
                {
                    continue;
                }
                UnixFileMode mode = File.GetUnixFileMode(path);
                if (mode.HasFlag(UnixFileMode.UserExecute))
                {
                    continue;
                }
                File.SetUnixFileMode(path, mode | UnixFileMode.UserExecute);
                fixedCount++;
            }
            if (fixedCount > 0)
            {
                logger?.LogInformation("[components] 补齐 {Count} 个 ELF 文件的执行位：{Destination}", fixedCount, destination);
            }
        }
        catch (Exception ex)
        {
            // 补位失败不致命：组件行会显示「没有可执行权限」，用户还能手动 chmod。
            logger?.LogWarning(ex, "[components] 补齐执行位失败：{Destination}", destination);
        }
    }


    private static bool IsElfFile(string path)
    {
        try
        {
            Span<byte> header = stackalloc byte[4];
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return stream.Read(header) == 4
                && header[0] == 0x7F && header[1] == (byte)'E' && header[2] == (byte)'L' && header[3] == (byte)'F';
        }
        catch
        {
            return false;
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
