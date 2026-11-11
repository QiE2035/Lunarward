// Lunarward：把应用图标喂给窗口管理器。
//
// Uno 的 X11 后端不设置 _NET_WM_ICON，导致任务栏/Alt-Tab 里只有通用占位方块。
// 这里用 SkiaSharp 解一次 PNG，转成 X11 需要的 0xAARRGGBB 数组，交给 X11Window.SetIcon。
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System;
using System.IO;

namespace Starward.Helpers;


public static class AppIcon
{

    private static uint[]? _argb;
    private static int _width;
    private static int _height;
    private static bool _loaded;


    /// <summary>设置窗口图标；失败只记日志（缺图标不影响使用）。</summary>
    public static void Apply(string? windowTitle)
    {
        try
        {
            if (!Load())
            {
                return;
            }
            SetOnWindow(windowTitle, _argb!, _width, _height);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "AppIcon.Apply failed");
        }
    }


    /// <summary>直接给某个窗口设置同一张图标（等窗口真正 map 之后调用更可靠）。</summary>
    public static void SetOnWindow(string? windowTitle, uint[] argb, int width, int height)
    {
        X11Window.SetIcon(windowTitle, width, height, argb);
    }


    public static bool TryGet(out uint[] argb, out int width, out int height)
    {
        if (Load())
        {
            argb = _argb!;
            width = _width;
            height = _height;
            return true;
        }
        argb = Array.Empty<uint>();
        width = height = 0;
        return false;
    }


    private static bool Load()
    {
        if (_loaded)
        {
            return _argb is not null;
        }
        _loaded = true;
        try
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "lunarward.png");
            if (!File.Exists(path))
            {
                Serilog.Log.Debug("AppIcon: 找不到 {Path}", path);
                return false;
            }
            using SKBitmap? bitmap = SKBitmap.Decode(path);
            if (bitmap is null)
            {
                return false;
            }
            // X11 图标有大小限制，过大的图会让 WM 拒绝；64 足够清晰
            const int Target = 64;
            using var surface = SKSurface.Create(new SKImageInfo(Target, Target, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            using (var paint = new SKPaint { IsAntialias = true, FilterQuality = SKFilterQuality.High })
            {
                float scale = Math.Min((float)Target / bitmap.Width, (float)Target / bitmap.Height);
                float w = bitmap.Width * scale;
                float h = bitmap.Height * scale;
                surface.Canvas.DrawImage(SKImage.FromBitmap(bitmap), new SKRect((Target - w) / 2, (Target - h) / 2, (Target + w) / 2, (Target + h) / 2), paint);
            }
            using SKImage image = surface.Snapshot();
            using SKBitmap? scaled = SKBitmap.FromImage(image);
            if (scaled is null)
            {
                return false;
            }
            uint[] argb = new uint[scaled.Width * scaled.Height];
            for (int y = 0; y < scaled.Height; y++)
            {
                for (int x = 0; x < scaled.Width; x++)
                {
                    SKColor color = scaled.GetPixel(x, y);
                    argb[y * scaled.Width + x] = ((uint)color.Alpha << 24) | ((uint)color.Red << 16) | ((uint)color.Green << 8) | color.Blue;
                }
            }
            _argb = argb;
            _width = scaled.Width;
            _height = scaled.Height;
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "AppIcon.Load failed");
            return false;
        }
    }

}
