// Lunarward：统一的图像解码入口（24 号 P4）。
//
// Linux 上图像解码有两条路：
//   1. SkiaSharp —— PNG / JPEG / WebP / GIF / BMP / ICO，快，项目本来就有；
//   2. Magick.NET —— AVIF / JPEG XL / HEIC，Skia 没有这些 delegate 时唯一可用的路。
// 调用方不用关心是哪条：先 Skia，Skia 解不开且扩展名属于「Skia 不支持」时才找 Magick。
using SkiaSharp;
using System;
using System.IO;

namespace Starward.Helpers;


internal static class ImageDecode
{

    /// <summary>
    /// 解码成 <see cref="SKBitmap"/>；两种后端都失败返回 null。
    /// <paramref name="maxEdge"/> 大于 0 时把长边缩进该范围（AVIF 原图动辄 4K，缩略图没必要解全量）。
    /// </summary>
    public static SKBitmap? Load(string path, int maxEdge = 0)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }
        SKBitmap? bitmap = TrySkia(path);
        if (bitmap is not null)
        {
            return maxEdge > 0 ? Downscale(bitmap, maxEdge) : bitmap;
        }
        if (MagickCodec.NeedsMagick(path) || MagickCodec.IsAvailable)
        {
            return MagickCodec.TryDecode(path, maxEdge);
        }
        return null;
    }


    /// <summary>已经拿到字节时走这个（<paramref name="path"/> 只用来判断格式与兜底）。</summary>
    public static SKBitmap? Load(byte[] data, string? path = null, int maxEdge = 0)
    {
        if (data is null || data.Length == 0)
        {
            return null;
        }
        try
        {
            SKBitmap? bitmap = SKBitmap.Decode(data);
            if (bitmap is not null)
            {
                return maxEdge > 0 ? Downscale(bitmap, maxEdge) : bitmap;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[decode] Skia 解码失败");
        }
        if (path is not null && maxEdge == 0)
        {
            return MagickCodec.TryDecode(path);
        }
        return null;
    }


    private static SKBitmap? TrySkia(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using SKCodec? codec = SKCodec.Create(stream);
            if (codec is null)
            {
                return null;
            }
            return SKBitmap.Decode(codec);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "[decode] Skia 解码失败：{Path}", path);
            return null;
        }
    }


    private static SKBitmap Downscale(SKBitmap source, int maxEdge)
    {
        int longest = Math.Max(source.Width, source.Height);
        if (longest <= maxEdge)
        {
            return source;
        }
        double scale = (double)maxEdge / longest;
        int width = Math.Max(1, (int)Math.Round(source.Width * scale));
        int height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var result = new SKBitmap(info);
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(source);
        using var paint = new SKPaint { FilterQuality = SKFilterQuality.High };
        canvas.DrawImage(image, new SKRect(0, 0, width, height), paint);
        source.Dispose();
        return result;
    }

}
