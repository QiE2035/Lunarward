// Lunarward：Sophon Chunk 清单（`src/Starward.RPC/GameInstall/Sophon.proto`）的模型与解析。
//
// 字段编号与上游 proto 保持一一对应，改动前请先对照那份 proto：
//   SophonChunkManifest { repeated SophonChunkFile chuncks = 1 }
//   SophonChunkFile     { file=1, repeated SophonChunk chunks=2, is_folder=3, size=4, md5=5 }
//   SophonChunk         { id=1, uncompressed_md5=2, offset=3, compressed_size=4, uncompressed_size=5, unknown=6, compressed_md5=7 }

using System;
using System.Collections.Generic;

namespace Starward.Features.GameInstall.Sophon;


/// <summary>清单里的一个文件。</summary>
public sealed class SophonChunkFile
{
    public string File { get; init; } = "";

    public List<SophonChunk> Chunks { get; init; } = new();

    public bool IsFolder { get; init; }

    public long Size { get; init; }

    public string MD5 { get; init; } = "";
}


/// <summary>文件里的一块（按 offset 落位）。</summary>
public sealed class SophonChunk
{
    public string Id { get; init; } = "";

    public string UncompressedMD5 { get; init; } = "";

    public long Offset { get; init; }

    public long CompressedSize { get; init; }

    public long UncompressedSize { get; init; }

    public string CompressedMD5 { get; init; } = "";
}


public static class SophonChunkManifestParser
{

    /// <summary>解析清单（已解压的 protobuf 字节）。</summary>
    public static List<SophonChunkFile> Parse(ReadOnlySpan<byte> data)
    {
        var files = new List<SophonChunkFile>();
        var reader = new ProtoReader(data);
        while (!reader.End && reader.TryReadTag(out int field, out int wireType))
        {
            if (field == 1 && wireType == 2)
            {
                ReadOnlySpan<byte> payload = reader.ReadBytes();
                if (!payload.IsEmpty)
                {
                    files.Add(ParseFile(payload));
                }
            }
            else if (!reader.SkipField(wireType))
            {
                break;
            }
        }
        return files;
    }


    private static SophonChunkFile ParseFile(ReadOnlySpan<byte> data)
    {
        string file = "";
        var chunks = new List<SophonChunk>();
        bool isFolder = false;
        long size = 0;
        string md5 = "";
        var reader = new ProtoReader(data);
        while (!reader.End && reader.TryReadTag(out int field, out int wireType))
        {
            switch (field, wireType)
            {
                case (1, 2):
                    file = reader.ReadString();
                    break;
                case (2, 2):
                    ReadOnlySpan<byte> payload = reader.ReadBytes();
                    if (!payload.IsEmpty)
                    {
                        chunks.Add(ParseChunk(payload));
                    }
                    break;
                case (3, 0):
                    isFolder = reader.ReadBool();
                    break;
                case (4, 0):
                    size = reader.ReadInt64();
                    break;
                case (5, 2):
                    md5 = reader.ReadString();
                    break;
                default:
                    if (!reader.SkipField(wireType))
                    {
                        return new SophonChunkFile { File = file, Chunks = chunks, IsFolder = isFolder, Size = size, MD5 = md5 };
                    }
                    break;
            }
        }
        return new SophonChunkFile { File = file, Chunks = chunks, IsFolder = isFolder, Size = size, MD5 = md5 };
    }


    private static SophonChunk ParseChunk(ReadOnlySpan<byte> data)
    {
        string id = "";
        string uncompressedMD5 = "";
        long offset = 0;
        long compressedSize = 0;
        long uncompressedSize = 0;
        string compressedMD5 = "";
        var reader = new ProtoReader(data);
        while (!reader.End && reader.TryReadTag(out int field, out int wireType))
        {
            switch (field, wireType)
            {
                case (1, 2):
                    id = reader.ReadString();
                    break;
                case (2, 2):
                    uncompressedMD5 = reader.ReadString();
                    break;
                case (3, 0):
                    offset = reader.ReadInt64();
                    break;
                case (4, 0):
                    compressedSize = reader.ReadInt64();
                    break;
                case (5, 0):
                    uncompressedSize = reader.ReadInt64();
                    break;
                case (7, 2):
                    compressedMD5 = reader.ReadString();
                    break;
                default:
                    if (!reader.SkipField(wireType))
                    {
                        return new SophonChunk { Id = id, UncompressedMD5 = uncompressedMD5, Offset = offset, CompressedSize = compressedSize, UncompressedSize = uncompressedSize, CompressedMD5 = compressedMD5 };
                    }
                    break;
            }
        }
        return new SophonChunk { Id = id, UncompressedMD5 = uncompressedMD5, Offset = offset, CompressedSize = compressedSize, UncompressedSize = uncompressedSize, CompressedMD5 = compressedMD5 };
    }

}
