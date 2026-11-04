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

    private static async Task<ImageSource?> LoadAsync(object value, CancellationToken token)
    {
        // 纯路径（无 scheme）是最常见的用法：x:Bind 出来的绝对路径在 Linux 上不是绝对 URI，
        // 走 new BitmapImage(相对 uri) 会静默失败，所以这里先按文件系统路径处理。
        if (value is string direct && Path.IsPathRooted(direct) && File.Exists(direct))
        {
            byte[] directData = await File.ReadAllBytesAsync(direct, token);
            token.ThrowIfCancellationRequested();
            using var directStream = new MemoryStream(directData);
            using var directRas = directStream.AsRandomAccessStream();
            var directBitmap = new BitmapImage();
            await directBitmap.SetSourceAsync(directRas);
            return directBitmap;
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
            byte[] data = await File.ReadAllBytesAsync(uri.LocalPath, token);
            token.ThrowIfCancellationRequested();
            using var fs = new MemoryStream(data);
            using var fsStream = fs.AsRandomAccessStream();
            var local = new BitmapImage();
            await local.SetSourceAsync(fsStream);
            return local;
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

    private static class StreamExtensions
    {
    }

    /// <summary>上游 ImageEx 的图片加载完成事件；这里在加载结束（成功或失败）时触发一次。</summary>
    public event EventHandler? ImageExOpened;

    public event EventHandler? ImageExFailed;

    private void RaiseImageExOpened() => ImageExOpened?.Invoke(this, EventArgs.Empty);

    private void RaiseImageExFailed() => ImageExFailed?.Invoke(this, EventArgs.Empty);
}
