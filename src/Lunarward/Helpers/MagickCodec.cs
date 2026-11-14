// Lunarward：AVIF / JXL / HEIC 编解码（24 号 P4，用 Magick.NET）。
//
// 为什么需要：SkiaSharp（Uno 在 Linux 上的渲染后端）默认不带 AVIF / JPEG XL 解码器，
// 而米哈游的背景图与部分用户素材是这两种格式；原来这些图「打开是空白」。
// Magick.NET 带 libheif（AVIF/HEIC）与 libjxl（JXL）delegate，读写都支持（已实测往返）。
//
// 设计：只在「扩展名命中」时才走 Magick，PNG/JPG/WebP 仍走 Skia（更快、无额外内存）。
using ImageMagick;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Starward.Helpers;


internal static class MagickCodec
{

    private static readonly object Gate = new();
    private static bool _probed;
    private static bool _available;


    /// <summary>Magick.NET 是否可用（原生库加载失败时为 false，调用方退回 Skia 并提示不支持）。</summary>
    public static bool IsAvailable
    {
        get
        {
            if (Volatile.Read(ref _probed))
            {
                return _available;
            }
            lock (Gate)
            {
                if (_probed)
                {
                    return _available;
                }
                try
                {
                    _available = MagickNET.SupportedFormats.Any(x => x.Format == MagickFormat.Avif);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "[magick] 原生库不可用，AVIF/JXL 将不支持");
                    _available = false;
                }
                Volatile.Write(ref _probed, true);
                return _available;
            }
        }
    }


    /// <summary>这些扩展名必须靠 Magick 才解得开（Skia 不支持）。</summary>
    public static bool NeedsMagick(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".avif" or ".jxl" or ".heic" or ".heif";
    }


    /// <summary>读尺寸；失败返回 false。</summary>
    public static bool TryGetPixelSize(string path, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (!IsAvailable)
        {
            return false;
        }
        try
        {
            var info = new MagickImageInfo(path);
            if (info.Width == 0 || info.Height == 0)
            {
                return false;
            }
            width = (int)info.Width;
            height = (int)info.Height;
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[magick] 读尺寸失败：{Path}", path);
            return false;
        }
    }


    /// <summary>解码成 SKBitmap（BGRA8888）；<paramref name="maxEdge"/> 大于 0 时等比缩到该边长内。</summary>
    public static SKBitmap? TryDecode(string path, int maxEdge = 0)
    {
        if (!IsAvailable)
        {
            return null;
        }
        try
        {
            using var image = new MagickImage(path);
            image.Format = MagickFormat.Bgra;
            if (maxEdge > 0 && Math.Max(image.Width, image.Height) > maxEdge)
            {
                uint w = (uint)maxEdge;
                image.Resize(new MagickGeometry(w, w) { IgnoreAspectRatio = false });
            }
            int width = (int)image.Width;
            int height = (int)image.Height;
            byte[] pixels = image.GetPixels().ToByteArray(PixelMapping.BGRA)
                ?? throw new InvalidOperationException("GetPixels 返回空");
            var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
            return bitmap;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[magick] 解码失败：{Path}", path);
            return null;
        }
    }


    /// <summary>
    /// 把 BGRA 像素编码成 AVIF / JXL。质量 0–100（AVIF 默认 80，JXL 默认 90）。
    /// 返回编码后的字节；失败返回 null。
    /// </summary>
    public static byte[]? TryEncode(byte[] bgra, int width, int height, bool jxl, int quality = 0)
    {
        if (!IsAvailable)
        {
            return null;
        }
        try
        {
            var settings = new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.BGRA);
            using var image = new MagickImage(bgra, settings);
            image.Format = jxl ? MagickFormat.Jxl : MagickFormat.Avif;
            image.Quality = (uint)(quality <= 0 ? (jxl ? 90 : 80) : Math.Clamp(quality, 1, 100));
            return image.ToByteArray();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[magick] 编码 {Format} 失败", jxl ? "JXL" : "AVIF");
            return null;
        }
    }

}
