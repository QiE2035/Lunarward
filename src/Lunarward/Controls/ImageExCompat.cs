// Lunarward：上游用 Scighost.WinUI 的 ImageEx（Control，带 ControlTemplate）。
// 这里提供同命名空间、同模板契约的等价控件：暴露 Source/PlaceholderSource/
// PlaceholderStretch/NineGrid/Stretch，并按 Loading/Loaded/Failed/Unloaded 切视觉状态。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Scighost.WinUI.ImageEx;

public class ImageExOpenedEventArgs : EventArgs
{
}

public partial class ImageEx : Control
{
    private Image? _image;
    private Image? _placeholderImage;
    private CancellationTokenSource? _cts;

    public ImageEx()
    {
        DefaultStyleKey = typeof(ImageEx);
    }

    public event EventHandler<ImageExOpenedEventArgs>? ImageExOpened;

    public event EventHandler<ImageExFailedEventArgs>? ImageExFailed;

    public object? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public static readonly DependencyProperty SourceProperty =
        DependencyProperty.Register(nameof(Source), typeof(object), typeof(ImageEx),
            new PropertyMetadata(null, (d, e) => _ = ((ImageEx)d).LoadAsync()));

    public object? PlaceholderSource
    {
        get => GetValue(PlaceholderSourceProperty);
        set => SetValue(PlaceholderSourceProperty, value);
    }

    public static readonly DependencyProperty PlaceholderSourceProperty =
        DependencyProperty.Register(nameof(PlaceholderSource), typeof(object), typeof(ImageEx),
            new PropertyMetadata(null, (d, e) => ((ImageEx)d).ApplyPlaceholder()));

    public Stretch PlaceholderStretch
    {
        get => (Stretch)GetValue(PlaceholderStretchProperty);
        set => SetValue(PlaceholderStretchProperty, value);
    }

    public static readonly DependencyProperty PlaceholderStretchProperty =
        DependencyProperty.Register(nameof(PlaceholderStretch), typeof(Stretch), typeof(ImageEx),
            new PropertyMetadata(Stretch.None));

    public Stretch Stretch
    {
        get => (Stretch)GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public static readonly DependencyProperty StretchProperty =
        DependencyProperty.Register(nameof(Stretch), typeof(Stretch), typeof(ImageEx),
            new PropertyMetadata(Stretch.Uniform));

    public Thickness NineGrid
    {
        get => (Thickness)GetValue(NineGridProperty);
        set => SetValue(NineGridProperty, value);
    }

    public static readonly DependencyProperty NineGridProperty =
        DependencyProperty.Register(nameof(NineGrid), typeof(Thickness), typeof(ImageEx),
            new PropertyMetadata(default(Thickness)));

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _image = GetTemplateChild("Image") as Image;
        _placeholderImage = GetTemplateChild("PlaceholderImage") as Image;
        if (_image is not null)
        {
            _image.Stretch = Stretch;
        }
        ApplyPlaceholder();
        _ = LoadAsync();
    }

    private void ApplyPlaceholder()
    {
        if (_placeholderImage is null)
        {
            return;
        }
        _placeholderImage.Stretch = PlaceholderStretch;
        _placeholderImage.Source = ToImageSource(PlaceholderSource);
    }

    protected virtual Task<ImageSource?> ProvideCachedResourceAsync(Uri uri, CancellationToken token)
        => Task.FromResult<ImageSource?>(new BitmapImage(uri));

    private async Task LoadAsync()
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        try
        {
            GoToState("Loading");
            ImageSource? image = Source switch
            {
                null => null,
                ImageSource src => src,
                Uri uri => await ProvideCachedResourceAsync(uri, cts.Token),
                string s => await ProvideCachedResourceAsync(new Uri(s, UriKind.RelativeOrAbsolute), cts.Token),
                _ => null,
            };
            if (cts.IsCancellationRequested)
            {
                return;
            }
            if (_image is not null)
            {
                _image.Source = image;
                _image.Stretch = Stretch;
            }
            GoToState(image is null ? "Failed" : "Loaded");
            ImageExOpened?.Invoke(this, new ImageExOpenedEventArgs());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageEx] load failed: {Source} {ex.Message}");
            GoToState("Failed");
            ImageExFailed?.Invoke(this, new ImageExFailedEventArgs(ex));
        }
    }

    private void GoToState(string state)
    {
        VisualStateManager.GoToState(this, state, false);
    }

    internal static ImageSource? ToImageSource(object? value) => value switch
    {
        ImageSource src => src,
        Uri uri => new BitmapImage(uri),
        string s when Uri.TryCreate(s, UriKind.RelativeOrAbsolute, out var uri) => new BitmapImage(uri),
        _ => null,
    };
}

public class ImageExFailedEventArgs : EventArgs
{
    public ImageExFailedEventArgs(Exception exception) => Exception = exception;

    public Exception Exception { get; }
}

/// <summary>Muxc 命名空间下个别页面用的别名。</summary>
public class ImageExFailedException : Exception
{
    public ImageExFailedException(string message) : base(message) { }
}
