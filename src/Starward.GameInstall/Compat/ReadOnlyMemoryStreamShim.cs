// LUNARWARD-DEVIATION: D128。上游 0.19.0 编在 .NET 11 上，用到了 .NET 11 才有的 BCL 类型，
// 移植侧整体钉在 net10.0（Uno 6.7.45 只支持 net10），这里按同名同签名补上，
// 让上游源码一个字都不用改。
namespace System.IO;

/// <summary>.NET 11 的 <c>ReadOnlyMemoryStream</c> 在 net10.0 上的等价实现。</summary>
internal sealed class ReadOnlyMemoryStream : Stream
{
    private readonly ReadOnlyMemory<byte> _memory;
    private int _position;

    public ReadOnlyMemoryStream(ReadOnlyMemory<byte> memory) => _memory = memory;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => false;
    public override long Length => _memory.Length;
    public override long Position { get => _position; set => _position = checked((int)value); }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int available = _memory.Length - _position;
        if (available <= 0) { return 0; }
        int n = Math.Min(available, count);
        _memory.Span.Slice(_position, n).CopyTo(buffer.AsSpan(offset, n));
        _position += n;
        return n;
    }

    public override int Read(Span<byte> buffer)
    {
        int available = _memory.Length - _position;
        if (available <= 0) { return 0; }
        int n = Math.Min(available, buffer.Length);
        _memory.Span.Slice(_position, n).CopyTo(buffer);
        _position += n;
        return n;
    }

    public override int ReadByte()
    {
        if (_position >= _memory.Length) { return -1; }
        return _memory.Span[_position++];
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        long target = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _memory.Length + offset,
            _ => throw new ArgumentOutOfRangeException(nameof(origin)),
        };
        if (target < 0 || target > _memory.Length) { throw new IOException("尝试定位到流范围之外。"); }
        _position = (int)target;
        return _position;
    }

    public override void Flush() { }
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
