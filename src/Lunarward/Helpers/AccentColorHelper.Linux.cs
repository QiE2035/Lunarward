// Lunarward：上游 Helpers/AccentColorHelper.cs 与 Features/Background/AccentColorHelper.cs 都依赖
// WinRT/WIC 的像素读取，已被 csproj 排除编译，因此 Linux 侧没有任何强调色实现
// （AppBackground.Linux.cs 里也就从未调用过 ChangeAppAccentColor）——表现为「切换游戏后配色不变」。
// 这里用 SkiaSharp 读图取主色，再把结果写进 App.xaml 的主题字典（D50、L-89）。
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.UI;

namespace Starward.Helpers;

internal static class AccentColorHelper
{


    /// <summary>从背景图取主色并应用到界面强调色（异步取色，回 UI 线程写入资源）。</summary>
    public static void ApplyFromFile(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return;
        }
        // 必须在 UI 线程上取 DispatcherQueue：在 Task.Run 里取到的是 null，
        // 结果会在后台线程改 Resources，ThemeResource 不重算（实测按钮颜色不变，L-89）。
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _ = Task.Run(() =>
        {
            try
            {
                Color? color = PickColor(imagePath);
                if (color is null)
                {
                    return;
                }
                if (dispatcher is null || dispatcher.HasThreadAccess)
                {
                    ChangeAppAccentColor(color);
                }
                else
                {
                    dispatcher.TryEnqueue(() => ChangeAppAccentColor(color));
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "pick accent color from {path}", imagePath);
            }
        });
    }



    /// <summary>按「饱和度×明度」加权统计色相直方图，取占比最高的色相作为主色。</summary>
    public static Color? PickColor(string imagePath)
    {
        using SKBitmap? bitmap = ImageDecode.Load(imagePath);
        if (bitmap is null)
        {
            return null;
        }
        // 缩小到最长边 128，避免 4K 壁纸逐像素统计拖慢启动。
        int scale = Math.Max(1, Math.Max(bitmap.Width, bitmap.Height) / 128);
        var hueWeight = new double[36];
        var hueR = new double[36];
        var hueG = new double[36];
        var hueB = new double[36];
        for (int y = 0; y < bitmap.Height; y += scale)
        {
            for (int x = 0; x < bitmap.Width; x += scale)
            {
                SKColor pixel = bitmap.GetPixel(x, y);
                if (pixel.Alpha < 200)
                {
                    continue;
                }
                double r = pixel.Red / 255d, g = pixel.Green / 255d, b = pixel.Blue / 255d;
                double max = Math.Max(r, Math.Max(g, b));
                double min = Math.Min(r, Math.Min(g, b));
                double delta = max - min;
                if (max < 0.15 || delta < 0.12)
                {
                    continue; // 太暗或接近灰的像素不参与取色
                }
                double hue;
                if (max == r)
                {
                    hue = 60 * (((g - b) / delta) % 6);
                }
                else if (max == g)
                {
                    hue = 60 * (((b - r) / delta) + 2);
                }
                else
                {
                    hue = 60 * (((r - g) / delta) + 4);
                }
                if (hue < 0)
                {
                    hue += 360;
                }
                int bucket = (int)(hue / 10) % 36;
                double weight = delta * max;
                hueWeight[bucket] += weight;
                hueR[bucket] += r * weight;
                hueG[bucket] += g * weight;
                hueB[bucket] += b * weight;
            }
        }
        int best = -1;
        double bestWeight = 0;
        for (int i = 0; i < hueWeight.Length; i++)
        {
            // 相邻桶合并，避免同一色相被切成两半。
            double merged = hueWeight[i] + hueWeight[(i + 1) % 36];
            if (merged > bestWeight)
            {
                bestWeight = merged;
                best = i;
            }
        }
        if (best < 0 || bestWeight <= 0)
        {
            return null;
        }
        double sum = hueWeight[best] + hueWeight[(best + 1) % 36];
        if (sum <= 0)
        {
            return null;
        }
        double avgR = (hueR[best] + hueR[(best + 1) % 36]) / sum;
        double avgG = (hueG[best] + hueG[(best + 1) % 36]) / sum;
        double avgB = (hueB[best] + hueB[(best + 1) % 36]) / sum;
        var (_, s, v) = ToHsv(avgR, avgG, avgB);
        // 取到的原始色往往过深或过艳，收敛到适合做 UI 强调色的区间。
        (double cr, double cg, double cb) = FromHsv(Hue(avgR, avgG, avgB), Math.Clamp(s, 0.30, 0.55), Math.Clamp(v, 0.65, 0.90));
        return Color.FromArgb(255, (byte)(cr * 255), (byte)(cg * 255), (byte)(cb * 255));
    }



    public static void ChangeAppAccentColor(Color? color)
    {
        if (color is null)
        {
            return;
        }
        Color value = color.Value;
        Color light1 = Mix(value, Microsoft.UI.Colors.White, 0.8);
        Color light2 = Mix(value, Microsoft.UI.Colors.White, 0.6);
        Color light3 = Mix(value, Microsoft.UI.Colors.White, 0.4);
        Color dark1 = Mix(value, Microsoft.UI.Colors.Black, 0.8);
        Color dark2 = Mix(value, Microsoft.UI.Colors.Black, 0.6);
        Color dark3 = Mix(value, Microsoft.UI.Colors.Black, 0.4);
        var palette = new (string Key, object Value)[]
        {
            ("SystemAccentColor", value),
            ("SystemAccentColorLight1", light1),
            ("SystemAccentColorLight2", light2),
            ("SystemAccentColorLight3", light3),
            ("SystemAccentColorDark1", dark1),
            ("SystemAccentColorDark2", dark2),
            ("SystemAccentColorDark3", dark3),
        };
        var appResources = Microsoft.UI.Xaml.Application.Current.Resources;
        foreach (var (key, entry) in palette)
        {
            appResources[key] = entry;
        }
        // App.xaml 的强调色放在 ThemeDictionaries（Light/Dark）里，主题字典优先级高于应用级 Resources，
        // 只写 Resources[key] 对任何 {ThemeResource SystemAccentColor} 都不生效（L-89）。
        var themeDictionaries = appResources.ThemeDictionaries;
        foreach (string themeKey in new[] { "Light", "Dark", "Default", "HighContrast" })
        {
            if (themeDictionaries.TryGetValue(themeKey, out object? dictionary) &&
                dictionary is Microsoft.UI.Xaml.ResourceDictionary themeResources)
            {
                foreach (var (key, entry) in palette)
                {
                    themeResources[key] = entry;
                }
            }
        }
        Serilog.Log.Information("Accent color applied: #{R:X2}{G:X2}{B:X2}", value.R, value.G, value.B);
        WeakReferenceMessenger.Default.Send(new Starward.Features.Background.AccentColorChangedMessage());
    }



    private static Color Mix(Color input, Color blend, double weight)
    {
        return Color.FromArgb(255,
                              (byte)(input.R * weight + blend.R * (1 - weight)),
                              (byte)(input.G * weight + blend.G * (1 - weight)),
                              (byte)(input.B * weight + blend.B * (1 - weight)));
    }


    private static double Hue(double r, double g, double b)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        if (delta <= 0)
        {
            return 0;
        }
        double hue;
        if (max == r)
        {
            hue = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            hue = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            hue = 60 * (((r - g) / delta) + 4);
        }
        return hue < 0 ? hue + 360 : hue;
    }


    private static (double Hue, double S, double V) ToHsv(double r, double g, double b)
    {
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double s = max <= 0 ? 0 : delta / max;
        return (Hue(r, g, b), s, max);
    }


    private static (double R, double G, double B) FromHsv(double h, double s, double v)
    {
        double c = v * s;
        double x = c * (1 - Math.Abs(((h / 60) % 2) - 1));
        double m = v - c;
        (double r, double g, double b) = (h % 360) switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return (r + m, g + m, b + m);
    }


}
