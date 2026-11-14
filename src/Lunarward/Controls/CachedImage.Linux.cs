// Lunarward：CachedImage 的 Linux 实现（替代上游依赖 Scighost.WinUI.ImageEx 的版本）。
// 公开 API 与上游保持一致：Source（ms-appx / file / http）、IsThumbnail、PngThumbnail。
// 上游 Controls/CachedImage.cs 保留在仓库中，但排除编译。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Controls;

public sealed partial class CachedImage : Image
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private CancellationTokenSource? _cts;

    public new object? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly new DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(object), typeof(CachedImage),
            new PropertyMetadata(null, (d, e) => ((CachedImage)d).OnSourceChanged(e.NewValue)));

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(nameof(CornerRadius), typeof(CornerRadius), typeof(CachedImage),
            new PropertyMetadata(new CornerRadius(0), (d, _) => ((CachedImage)d).UpdateClip()));

    /// <summary>等价于 CommunityToolkit 的 ClipToBounds，供页面以附加属性方式开关。</summary>
    public bool ClipToBounds { get; set; }

    public CachedImage()
    {
        SizeChanged += (_, _) => UpdateClip();
    }

    private void UpdateClip()
    {
        var cr = CornerRadius;
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            return;
        }
        // Uno 的 RectangleGeometry 不支持圆角，这里先做矩形裁剪；圆角在渲染层由合成器处理（M3）。
        Clip = new RectangleGeometry
        {
            Rect = new Windows.Foundation.Rect(0, 0, ActualWidth, ActualHeight),
        };
    }

    public bool IsThumbnail
    {
        get => (bool)GetValue(IsThumbnailProperty);
        set => SetValue(IsThumbnailProperty, value);
    }

    public static readonly DependencyProperty IsThumbnailProperty =
        DependencyProperty.Register(nameof(IsThumbnail), typeof(bool), typeof(CachedImage), new PropertyMetadata(false));

    public bool PngThumbnail
    {
        get => (bool)GetValue(PngThumbnailProperty);
        set => SetValue(PngThumbnailProperty, value);
    }

    public static readonly DependencyProperty PngThumbnailProperty =
        DependencyProperty.Register(nameof(PngThumbnail), typeof(bool), typeof(CachedImage), new PropertyMetadata(false));

    /// <summary>
    /// 解码宽度上限（DIP 无关，单位=像素）。大于 0 时先用 SkiaSharp 按高质量插值缩到该宽度再交给
    /// BitmapImage，避免「2560×1440 的壁纸按 216 DIP 显示」把整张位图解进内存（L-96）。
    /// 用 Mitchell 三次卷积 + mipmap：缩 10 倍以上也不会出现最近邻锯齿或线性采样的摩尔纹。
    /// </summary>
    public int DecodePixelWidth
    {
        get => (int)GetValue(DecodePixelWidthProperty);
        set => SetValue(DecodePixelWidthProperty, value);
    }

    public static readonly DependencyProperty DecodePixelWidthProperty =
        DependencyProperty.Register(nameof(DecodePixelWidth), typeof(int), typeof(CachedImage),
            new PropertyMetadata(0, (d, e) => ((CachedImage)d).OnSourceChanged(((CachedImage)d).Source)));

    /// <summary>图片加载完成后触发，供上游 ImageViewWindow 等使用。</summary>
    public event EventHandler? ImageOpened;

    private async void OnSourceChanged(object? value)
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;

        if (value is null)
        {
            base.Source = null;
            return;
        }

        try
        {
            var image = await LoadAsync(value, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                base.Source = image;
                ImageOpened?.Invoke(this, EventArgs.Empty);
                RaiseImageExOpened();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CachedImage] load failed: {value} {ex.Message}");
            base.Source = null;
            RaiseImageExFailed();
        }
    }

    private async Task<ImageSource?> LoadAsync(object value, CancellationToken token)
    {
        // 纯路径（无 scheme）是最常见的用法：x:Bind 出来的绝对路径在 Linux 上不是绝对 URI，
        // 走 new BitmapImage(相对 uri) 会静默失败，所以这里先按文件系统路径处理。
        if (value is string direct && Path.IsPathRooted(direct) && File.Exists(direct))
        {
            return await ReadFileAsync(direct, token);
        }

        var uri = value switch
        {
            Uri u => u,
            string s when !string.IsNullOrWhiteSpace(s) => new Uri(s, UriKind.RelativeOrAbsolute),
            _ => null,
        };
        if (uri is null) return null;

        // 本地文件：Uno 的 file:// 支持不稳（实测绝对路径不显示），自己读字节最稳。
        if (uri.IsAbsoluteUri && uri.Scheme is "file" && File.Exists(uri.LocalPath))
        {
            return await ReadFileAsync(uri.LocalPath, token);
        }

        // 应用内资源
        if (!uri.IsAbsoluteUri || uri.Scheme is "ms-appx" or "ms-appdata")
        {
            return new BitmapImage(uri);
        }

        if (uri.Scheme is "http" or "https")
        {
            var bytes = await _http.GetByteArrayAsync(uri, token);
            token.ThrowIfCancellationRequested();
            using var ms = new MemoryStream(bytes);
            using var ras = ms.AsRandomAccessStream();
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(ras);
            return bitmap;
        }

        return new BitmapImage(uri);
    }

    /// <summary>按 DecodePixelWidth 做高质量降采样；失败时返回 null（调用方退回原图）。</summary>
    private static Stream? DownscaleToStream(byte[] data, int targetWidth, string? path = null)
    {
        try
        {
            using var src = Starward.Helpers.ImageDecode.Load(data, path);
            if (src is null || src.Width <= targetWidth)
            {
                return null;
            }
            int targetHeight = Math.Max(1, (int)Math.Round((double)src.Height * targetWidth / src.Width));
            var info = new SkiaSharp.SKImageInfo(targetWidth, targetHeight, SkiaSharp.SKColorType.Bgra8888,
                                                 SkiaSharp.SKAlphaType.Premul);
            using var surface = SkiaSharp.SKSurface.Create(info);
            using var image = SkiaSharp.SKImage.FromBitmap(src);
            // SKFilterQuality.High = 三次卷积 + mipmap：缩 10 倍也不会出现最近邻锯齿或线性采样的摩尔纹。
            using var paint = new SkiaSharp.SKPaint { FilterQuality = SkiaSharp.SKFilterQuality.High };
            surface.Canvas.DrawImage(image, new SkiaSharp.SKRect(0, 0, targetWidth, targetHeight), paint);
            using var snapshot = surface.Snapshot();
            using var encoded = snapshot.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            var ms = new MemoryStream();
            encoded.SaveTo(ms);
            ms.Position = 0;
            return ms;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "downscale failed: target {Width}", targetWidth);
            return null;
        }
    }

    private async Task<ImageSource?> ReadFileAsync(string path, CancellationToken token)
    {
        byte[] data = await File.ReadAllBytesAsync(path, token);
        token.ThrowIfCancellationRequested();
        int target = DecodePixelWidth;
        if (target > 0)
        {
            using Stream? scaled = DownscaleToStream(data, target, path);
            if (scaled is not null)
            {
                using var scaledRas = scaled.AsRandomAccessStream();
                var scaledBitmap = new BitmapImage();
                await scaledBitmap.SetSourceAsync(scaledRas);
                return scaledBitmap;
            }
        }
        using var fs = new MemoryStream(data);
        using var fsStream = fs.AsRandomAccessStream();
        var bitmap = new BitmapImage();
        await bitmap.SetSourceAsync(fsStream);
        return bitmap;
    }

    private static class StreamExtensions
    {
    }

    /// <summary>上游 ImageEx 的图片加载完成事件；这里在加载结束（成功或失败）时触发一次。</summary>
    public event EventHandler? ImageExOpened;

    public event EventHandler? ImageExFailed;

    private void RaiseImageExOpened() => ImageExOpened?.Invoke(this, EventArgs.Empty);

    private void RaiseImageExFailed() => ImageExFailed?.Invoke(this, EventArgs.Empty);
}
