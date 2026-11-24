// LUNARWARD-DEVIATION: D128。上游 0.19.0 编在 .NET 11 上，用到了 .NET 11 才有的 BCL 类型，
// 移植侧整体钉在 net10.0（Uno 6.7.45 只支持 net10），这里按同名同签名补上，
// 让上游源码一个字都不用改。
using System.IO.Compression;

namespace System.IO.Compression;

/// <summary>.NET 11 的 <c>ZstandardStream</c> 在 net10.0 上的等价实现，基于 ZstdSharp.Port。</summary>
internal sealed class ZstandardStream : Stream
{
    private readonly ZstdSharp.DecompressionStream? _decompression;
    private readonly ZstdSharp.CompressionStream? _compression;
    private readonly Stream _inner;
    private readonly Stream _baseStream = null!;
    private readonly bool _leaveOpen;

    public ZstandardStream(Stream baseStream, CompressionMode mode, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(baseStream);
        if (mode == CompressionMode.Compress)
        {
            _compression = new ZstdSharp.CompressionStream(baseStream, 3);
            _leaveOpen = leaveOpen;
            _inner = _compression;
        }
        else
        {
            _decompression = new ZstdSharp.DecompressionStream(baseStream);
            _leaveOpen = leaveOpen;
            _inner = _decompression;
        }
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => _inner.Flush();
    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
    public override int Read(Span<byte> buffer) => _inner.Read(buffer);
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => _inner.ReadAsync(buffer, cancellationToken);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
    public override void Write(ReadOnlySpan<byte> buffer) => _inner.Write(buffer);
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_compression is not null) { _compression.Dispose(); }
            if (_decompression is not null) { _decompression.Dispose(); }
            if (!_leaveOpen && _baseStream is not null) { _baseStream.Dispose(); }
        }
        base.Dispose(disposing);
    }
}
