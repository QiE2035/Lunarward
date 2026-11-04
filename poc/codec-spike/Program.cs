// G0-8 尖峰：验证 net10.0 下通过 P/Invoke 调用系统 libavif / libjxl 完成真实解码。
// 只验证「映射通路 + 解码成功」，不追求 API 完整（完整封装见 M3 的 Lunarward.Codec）。
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Lunarward.CodecSpike;

internal static class AvifNative
{
    private const string Lib = "libavif.so.16";

    [DllImport(Lib)] internal static extern IntPtr avifDecoderCreate();
    [DllImport(Lib)] internal static extern void avifDecoderDestroy(IntPtr decoder);
    [DllImport(Lib)] internal static extern IntPtr avifImageCreateEmpty();
    [DllImport(Lib)] internal static extern void avifImageDestroy(IntPtr image);
    [DllImport(Lib)] internal static extern int avifDecoderReadFile(IntPtr decoder, IntPtr image, string filename);
    [DllImport(Lib)] internal static extern string avifResultToString(int result);
}

internal static class JxlNative
{
    private const string Lib = "libjxl.so.0.12";

    internal const int JXL_DEC_SUCCESS = 0;
    internal const int JXL_DEC_NEED_MORE_INPUT = 2;
    internal const int JXL_DEC_BASIC_INFO = 0x40;
    internal const int JXL_DEC_FULL_IMAGE = 0x1000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct JxlPixelFormat
    {
        public uint num_channels;   // 4
        public int data_type;       // JXL_TYPE_UINT8 = 2
        public int endianness;      // JXL_NATIVE_ENDIAN = 0
        public UIntPtr align;       // 0
    }

    [DllImport(Lib)] internal static extern IntPtr JxlDecoderCreate(IntPtr memoryManager);
    [DllImport(Lib)] internal static extern void JxlDecoderDestroy(IntPtr decoder);
    [DllImport(Lib)] internal static extern int JxlDecoderSubscribeEvents(IntPtr decoder, int events);
    [DllImport(Lib)] internal static extern int JxlDecoderSetInput(IntPtr decoder, byte[] data, UIntPtr size);
    [DllImport(Lib)] internal static extern void JxlDecoderCloseInput(IntPtr decoder);
    [DllImport(Lib)] internal static extern int JxlDecoderProcessInput(IntPtr decoder);
    [DllImport(Lib)] internal static extern int JxlDecoderGetBasicInfo(IntPtr decoder, IntPtr basicInfo);
    [DllImport(Lib)] internal static extern int JxlDecoderImageOutBufferSize(IntPtr decoder, ref JxlPixelFormat format, out UIntPtr size);
    [DllImport(Lib)] internal static extern int JxlDecoderSetImageOutBuffer(IntPtr decoder, ref JxlPixelFormat format, IntPtr buffer, UIntPtr size);
}

internal static class Program
{
    private static unsafe uint ReadU32(IntPtr p, int offset) => *(uint*)((byte*)p + offset);
    private static unsafe IntPtr ReadPtr(IntPtr p, int offset) => *(IntPtr*)((byte*)p + offset);

    private static int Main(string[] args)
    {
        var dir = args.Length > 0 ? args[0] : ".";
        var avifPath = Path.Combine(dir, "sample.avif");
        var jxlPath = Path.Combine(dir, "sample.jxl");

        var ok = DecodeAvif(avifPath) & DecodeJxl(jxlPath);
        Console.WriteLine(ok ? "\nG0-8: PASS" : "\nG0-8: FAIL");
        return ok ? 0 : 1;
    }

    private static bool DecodeAvif(string path)
    {
        Console.WriteLine($"[AVIF] {path} ({new FileInfo(path).Length} bytes)");
        if (!File.Exists(path)) return false;

        var decoder = AvifNative.avifDecoderCreate();
        var image = AvifNative.avifImageCreateEmpty();
        if (decoder == IntPtr.Zero || image == IntPtr.Zero) { Console.WriteLine("  创建 decoder/image 失败"); return false; }
        try
        {
            var res = AvifNative.avifDecoderReadFile(decoder, image, path);
            if (res != 0)
            {
                Console.WriteLine($"  avifDecoderReadFile 失败: {AvifNative.avifResultToString(res)} ({res})");
                return false;
            }
            // avifImage 起始字段布局（见 /usr/include/avif/avif.h:777）
            // width@0, height@4, depth@8, yuvFormat@12, yuvRange@16, chromaSamplePos@20,
            // yuvPlanes[3]@24/32/40, yuvRowBytes[3]@48/52/56
            var width = ReadU32(image, 0);
            var height = ReadU32(image, 4);
            var depth = ReadU32(image, 8);
            var yPlane = ReadPtr(image, 24);
            var uPlane = ReadPtr(image, 32);
            var rowBytes = ReadU32(image, 48);
            Console.WriteLine($"  解码成功: {width}x{height} depth={depth} yPlane={(yPlane != IntPtr.Zero ? "有" : "空")} uPlane={(uPlane != IntPtr.Zero ? "有" : "空")} rowBytes={rowBytes}");
            return width > 0 && height > 0 && yPlane != IntPtr.Zero;
        }
        finally
        {
            AvifNative.avifImageDestroy(image);
            AvifNative.avifDecoderDestroy(decoder);
        }
    }

    private static bool DecodeJxl(string path)
    {
        Console.WriteLine($"[JXL] {path} ({new FileInfo(path).Length} bytes)");
        if (!File.Exists(path)) return false;

        var data = File.ReadAllBytes(path);
        var decoder = JxlNative.JxlDecoderCreate(IntPtr.Zero);
        if (decoder == IntPtr.Zero) { Console.WriteLine("  JxlDecoderCreate 失败"); return false; }

        var info = Marshal.AllocHGlobal(1024);
        var pixels = IntPtr.Zero;
        try
        {
            var events = JxlNative.JXL_DEC_BASIC_INFO | JxlNative.JXL_DEC_FULL_IMAGE;
            if (JxlNative.JxlDecoderSubscribeEvents(decoder, events) != JxlNative.JXL_DEC_SUCCESS) { Console.WriteLine("  SubscribeEvents 失败"); return false; }
            if (JxlNative.JxlDecoderSetInput(decoder, data, (UIntPtr)data.Length) != JxlNative.JXL_DEC_SUCCESS) { Console.WriteLine("  SetInput 失败"); return false; }
            JxlNative.JxlDecoderCloseInput(decoder);

            var fmt = new JxlNative.JxlPixelFormat { num_channels = 4, data_type = 2, endianness = 0, align = UIntPtr.Zero };
            UIntPtr bufSize = UIntPtr.Zero;
            uint width = 0, height = 0;
            var outBufferSet = false;

            for (var i = 0; i < 64; i++)
            {
                var status = JxlNative.JxlDecoderProcessInput(decoder);
                if (status == JxlNative.JXL_DEC_BASIC_INFO)
                {
                    if (JxlNative.JxlDecoderGetBasicInfo(decoder, info) != JxlNative.JXL_DEC_SUCCESS) { Console.WriteLine("  GetBasicInfo 失败"); return false; }
                    // JxlBasicInfo: have_container@0, xsize@4, ysize@8
                    width = ReadU32(info, 4);
                    height = ReadU32(info, 8);
                    if (JxlNative.JxlDecoderImageOutBufferSize(decoder, ref fmt, out bufSize) != JxlNative.JXL_DEC_SUCCESS) { Console.WriteLine("  ImageOutBufferSize 失败"); return false; }
                    pixels = Marshal.AllocHGlobal((int)bufSize);
                    if (JxlNative.JxlDecoderSetImageOutBuffer(decoder, ref fmt, pixels, bufSize) != JxlNative.JXL_DEC_SUCCESS) { Console.WriteLine("  SetImageOutBuffer 失败"); return false; }
                    outBufferSet = true;
                    continue;
                }
                if (status == JxlNative.JXL_DEC_FULL_IMAGE)
                {
                    if (!outBufferSet) { Console.WriteLine("  FULL_IMAGE 但未设置输出缓冲"); return false; }
                    var bytes = new byte[(int)bufSize];
                    Marshal.Copy(pixels, bytes, 0, bytes.Length);
                    var nonZero = bytes.Count(b => b != 0);
                    var hash = Convert.ToHexString(SHA256.HashData(bytes))[..16];
                    Console.WriteLine($"  解码成功: {width}x{height} rgba={bufSize} 非零字节={nonZero} sha256[:16]={hash}");
                    return width > 0 && height > 0 && nonZero > bytes.Length / 2;
                }
                if (status == JxlNative.JXL_DEC_NEED_MORE_INPUT) { Console.WriteLine("  NEED_MORE_INPUT（数据不足）"); return false; }
                if (status == JxlNative.JXL_DEC_SUCCESS) { Console.WriteLine("  流结束但未拿到 FULL_IMAGE"); return false; }
                if (status < 0 || status == 1) { Console.WriteLine($"  ProcessInput 失败: {status}"); return false; }
            }
            Console.WriteLine("  循环上限内未完成解码");
            return false;
        }
        finally
        {
            if (pixels != IntPtr.Zero) Marshal.FreeHGlobal(pixels);
            Marshal.FreeHGlobal(info);
            JxlNative.JxlDecoderDestroy(decoder);
        }
    }
}
