// Lunarward：截图批量转换（M3）。
// 上游走 Win2D + ImageMagick 风格的重编码，这里用 SkiaSharp——项目里已经有它（ImageLoader/壁纸缩略图都用），
// 不额外引入图像库。转换只做「解码 → JPEG 编码 → 同名 .jpg」，不删除原文件。
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
                using var input = File.OpenRead(file);
                using SKCodec? codec = SKCodec.Create(input);
                if (codec is null)
                {
                    failed++;
                    continue;
                }
                using var bitmap = SKBitmap.Decode(codec);
                if (bitmap is null)
                {
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
