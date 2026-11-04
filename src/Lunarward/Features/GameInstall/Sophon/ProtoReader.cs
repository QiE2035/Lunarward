// Lunarward：极简 protobuf 读取器。
//
// 只服务一件事——解析米哈游 Sophon 清单（`Sophon.proto`）。
// 引 Grpc.Tools 会往构建里塞 protoc 与整套 gRPC 运行时，而这里需要的能力只有
// 「读 varint / 读长度前缀字段」两种，手写一个两百行的读取器更划算，也让 Linux 构建零额外工具链。
//
// 只支持清单里出现的 wire type：0（varint）与 2（length-delimited）。
// 遇到其它类型直接跳过字段（按 wire type 的规则），不抛异常——清单里新增未知字段不应该让安装失败。

using System;

namespace Starward.Features.GameInstall.Sophon;

internal ref struct ProtoReader
{

    private ReadOnlySpan<byte> _data;

    private int _position;


    public ProtoReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _position = 0;
    }


    public bool End => _position >= _data.Length;


    /// <summary>读取下一个字段的 tag（field number + wire type）。</summary>
    public bool TryReadTag(out int fieldNumber, out int wireType)
    {
        fieldNumber = 0;
        wireType = 0;
        if (!TryReadVarint(out ulong tag) || tag == 0)
        {
            return false;
        }
        fieldNumber = (int) (tag >> 3);
        wireType = (int) (tag & 0x7);
        return true;
    }


    public bool TryReadVarint(out ulong value)
    {
        value = 0;
        int shift = 0;
        while (_position < _data.Length)
        {
            byte b = _data[_position++];
            value |= (ulong) (b & 0x7F) << shift;
            if ((b & 0x80) == 0)
            {
                return true;
            }
            shift += 7;
            if (shift >= 64)
            {
                return false;
            }
        }
        return false;
    }


    public long ReadInt64()
    {
        return TryReadVarint(out ulong value) ? (long) value : 0;
    }


    public bool ReadBool()
    {
        return TryReadVarint(out ulong value) && value != 0;
    }


    public string ReadString()
    {
        if (!TryReadLength(out int length) || length < 0 || _position + length > _data.Length)
        {
            return string.Empty;
        }
        string value = System.Text.Encoding.UTF8.GetString(_data.Slice(_position, length));
        _position += length;
        return value;
    }


    /// <summary>读取嵌套消息（length-delimited）的字节范围。</summary>
    public ReadOnlySpan<byte> ReadBytes()
    {
        if (!TryReadLength(out int length) || length < 0 || _position + length > _data.Length)
        {
            return ReadOnlySpan<byte>.Empty;
        }
        ReadOnlySpan<byte> value = _data.Slice(_position, length);
        _position += length;
        return value;
    }


    private bool TryReadLength(out int length)
    {
        length = 0;
        if (!TryReadVarint(out ulong value) || value > int.MaxValue)
        {
            return false;
        }
        length = (int) value;
        return true;
    }


    /// <summary>跳过当前字段（用于不认识的字段）。</summary>
    public bool SkipField(int wireType)
    {
        switch (wireType)
        {
            case 0:
                return TryReadVarint(out _);
            case 1:
                _position += 8;
                return _position <= _data.Length;
            case 2:
                return TryReadLength(out int length) && (_position += length) <= _data.Length;
            case 5:
                _position += 4;
                return _position <= _data.Length;
            default:
                return false;
        }
    }

}
