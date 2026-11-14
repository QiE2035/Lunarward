// Lunarward：截图批量转换（M3）。
// 解码走 Helpers.ImageDecode（Skia 优先，AVIF / JXL 由 Magick.NET 兜底，24 号 P4），
// 编码统一用 SkiaSharp 出 JPEG。转换只做「解码 → JPEG 编码 → 同名 .jpg」，不删除原文件。
using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

namespace Starward.Features.Screenshot;

internal static class ScreenshotConverter
{
    public static (int Ok, int Skip, int Failed) ConvertToJpeg(IEnumerable<string> files, int quality = 92)
    {
        int ok = 0, skip = 0, failed = 0;
        foreach (string file in files)
        {
            if (!File.Exists(file))
            {
                failed++;
                continue;
            }
            if (file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                skip++;
                continue;
            }
            try
            {
                string target = Path.ChangeExtension(file, ".jpg");
                using SKBitmap? bitmap = Starward.Helpers.ImageDecode.Load(file);
                if (bitmap is null)
                {
                    Serilog.Log.Warning("convert screenshot: 解不开 {file}（Skia 与 Magick 都不支持该格式）", file);
                    failed++;
                    continue;
                }
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
                if (data is null)
                {
                    failed++;
                    continue;
                }
                using (var output = File.Create(target))
                {
                    data.SaveTo(output);
                }
                ok++;
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "convert screenshot failed: {file}", file);
                failed++;
            }
        }
        return (ok, skip, failed);
    }
}
