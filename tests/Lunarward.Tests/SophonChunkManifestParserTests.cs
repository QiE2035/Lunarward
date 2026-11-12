using Starward.Features.GameInstall.Sophon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace Lunarward.Tests;

/// <summary>
/// Sophon 清单解析（手写 protobuf）的测试。
/// 字段编号来自 <c>src/Starward.RPC/GameInstall/Sophon.proto</c>：
///   SophonChunkManifest { repeated SophonChunkFile chuncks = 1 }
///   SophonChunkFile     { file=1, chunks=2, is_folder=3, size=4, md5=5 }
///   SophonChunk         { id=1, uncompressed_md5=2, offset=3, compressed_size=4, uncompressed_size=5, unknown=6, compressed_md5=7 }
/// 这里手写编码，避免为了测试引入 protobuf 生成器（L-56）。
/// </summary>
public class SophonChunkManifestParserTests
{

    private static void WriteVarint(Stream s, ulong value)
    {
        while (value >= 0x80)
        {
            s.WriteByte((byte)(value | 0x80));
            value >>= 7;
        }
        s.WriteByte((byte)value);
    }


    private static void WriteTag(Stream s, int field, int wireType) => WriteVarint(s, (ulong)((field << 3) | wireType));


    private static void WriteString(Stream s, int field, string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        WriteTag(s, field, 2);
        WriteVarint(s, (ulong)bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }


    private static void WriteLong(Stream s, int field, long value)
    {
        WriteTag(s, field, 0);
        WriteVarint(s, (ulong)value);
    }


    private static void WriteMessage(Stream s, int field, Action<Stream> body)
    {
        using var inner = new MemoryStream();
        body(inner);
        WriteTag(s, field, 2);
        WriteVarint(s, (ulong)inner.Length);
        inner.Position = 0;
        inner.CopyTo(s);
    }


    private static byte[] Chunk(string id, long offset, long compressed, long uncompressed, string? compressedMd5 = null, string? uncompressedMd5 = null)
    {
        using var ms = new MemoryStream();
        WriteString(ms, 1, id);
        if (uncompressedMd5 is not null) WriteString(ms, 2, uncompressedMd5);
        WriteLong(ms, 3, offset);
        WriteLong(ms, 4, compressed);
        WriteLong(ms, 5, uncompressed);
        if (compressedMd5 is not null) WriteString(ms, 7, compressedMd5);
        return ms.ToArray();
    }


    private static void WriteChunkFile(Stream s, string file, bool isFolder, long size, string? md5, params byte[][] chunks)
    {
        WriteMessage(s, 1, inner =>
        {
            WriteString(inner, 1, file);
            foreach (byte[] chunk in chunks)
            {
                WriteTag(inner, 2, 2);
                WriteVarint(inner, (ulong)chunk.Length);
                inner.Write(chunk, 0, chunk.Length);
            }
            if (isFolder) WriteLong(inner, 3, 1);
            WriteLong(inner, 4, size);
            if (md5 is not null) WriteString(inner, 5, md5);
        });
    }


    private static byte[] Manifest(params Action<Stream>[] files)
    {
        using var ms = new MemoryStream();
        foreach (Action<Stream> file in files) file(ms);
        return ms.ToArray();
    }


    [Fact]
    public void Parse_Reads_File_With_Chunks()
    {
        byte[] data = Manifest(s => WriteChunkFile(s, "YuanShen_Data/a.pck", false, 300, "filemd5",
            Chunk("chunkA", 0, 10, 100, "c1md5", "u1md5"),
            Chunk("chunkB", 100, 20, 200)));

        List<SophonChunkFile> files = SophonChunkManifestParser.Parse(data);

        SophonChunkFile file = Assert.Single(files);
        Assert.Equal("YuanShen_Data/a.pck", file.File);
        Assert.False(file.IsFolder);
        Assert.Equal(300, file.Size);
        Assert.Equal("filemd5", file.MD5);
        Assert.Equal(2, file.Chunks.Count);
        Assert.Equal("chunkA", file.Chunks[0].Id);
        Assert.Equal(0, file.Chunks[0].Offset);
        Assert.Equal(10, file.Chunks[0].CompressedSize);
        Assert.Equal(100, file.Chunks[0].UncompressedSize);
        Assert.Equal("c1md5", file.Chunks[0].CompressedMD5);
        Assert.Equal("u1md5", file.Chunks[0].UncompressedMD5);
        Assert.Equal(100, file.Chunks[1].Offset);
        Assert.Equal(200, file.Chunks[1].UncompressedSize);
        // 缺省字段应按默认值填充，而不是抛异常或 null
        Assert.Equal("", file.Chunks[1].CompressedMD5);
        Assert.Equal("", file.Chunks[1].UncompressedMD5);
    }


    [Fact]
    public void Parse_Reads_Folder_Entry()
    {
        byte[] data = Manifest(s => WriteChunkFile(s, "YuanShen_Data", true, 0, null));

        List<SophonChunkFile> files = SophonChunkManifestParser.Parse(data);

        SophonChunkFile file = Assert.Single(files);
        Assert.True(file.IsFolder);
        Assert.Empty(file.Chunks);
    }


    [Fact]
    public void Parse_Handles_Multiple_Files_And_Order()
    {
        byte[] data = Manifest(
            s => WriteChunkFile(s, "b.bin", false, 10, null, Chunk("c2", 0, 1, 10)),
            s => WriteChunkFile(s, "a.bin", false, 20, null, Chunk("c1", 0, 2, 20)));

        List<SophonChunkFile> files = SophonChunkManifestParser.Parse(data);

        Assert.Equal(new[] { "b.bin", "a.bin" }, files.Select(x => x.File));
    }


    [Fact]
    public void Parse_Empty_Input_Returns_Empty_List()
    {
        Assert.Empty(SophonChunkManifestParser.Parse(ReadOnlySpan<byte>.Empty));
    }


    [Fact]
    public void Parse_Skips_Unknown_Fields()
    {
        using var ms = new MemoryStream();
        // 顶层未知字段（编号 9，varint）
        WriteTag(ms, 9, 0);
        WriteVarint(ms, 12345);
        WriteChunkFile(ms, "keep.bin", false, 1, null, Chunk("c", 0, 1, 1));

        List<SophonChunkFile> files = SophonChunkManifestParser.Parse(ms.ToArray());

        Assert.Equal("keep.bin", Assert.Single(files).File);
    }


    [Fact]
    public void Parse_Truncated_Input_Does_Not_Hang_Or_Throw_Unexpectedly()
    {
        byte[] full = Manifest(s => WriteChunkFile(s, "x.bin", false, 100, null, Chunk("c", 0, 5, 100)));
        byte[] truncated = full.AsSpan(0, full.Length / 2).ToArray();

        // 允许抛异常（截断的 protobuf 无法解析），关键是「快速失败、不挂死、不是越界崩溃」
        Exception? ex = Record.Exception(() => SophonChunkManifestParser.Parse(truncated));
        Assert.True(ex is null || ex is InvalidOperationException || ex is ArgumentOutOfRangeException,
            ex?.GetType().FullName ?? "unexpected");
    }

}
