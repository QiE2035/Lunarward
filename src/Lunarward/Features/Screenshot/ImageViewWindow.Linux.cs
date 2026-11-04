using System;
using Microsoft.UI.Xaml;
// Lunarward：旧版看图窗口（上游已被 ImageViewWindow2 取代，但仍被截图流程引用）。
// Win2D 画布 → M3 用 SkiaSharp 重画；这里先补齐 x:Bind 契约与窗口壳。
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Frameworks;

namespace Starward.Features.Screenshot;

/// <summary>极简图片信息容器：上游用 Win2D 的 CanvasBitmap，Linux 侧只需要路径与尺寸。</summary>
public sealed class ImageFile
{
    public string? FilePath { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }
}

public sealed partial class ImageViewWindow : WindowEx
{
    public ImageViewWindow()
    {
        InitializeComponent();
    }

    /// <summary>当前显示的图片；由调用方设置。</summary>
    public ImageFile? CurrentImage { get; set; }

    public CommunityToolkit.Mvvm.Input.IRelayCommand CloseCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
    public CommunityToolkit.Mvvm.Input.IRelayCommand CopyImageCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
    public string? FilePath { get; set; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand FullScreenCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
    public string? ImageCollection { get; set; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenFileCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
    public CommunityToolkit.Mvvm.Input.IRelayCommand RotateCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
    public CommunityToolkit.Mvvm.Input.IRelayCommand ZoomInCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
    public CommunityToolkit.Mvvm.Input.IRelayCommand ZoomOutCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
    private void _Image_ImageOpened(object sender, EventArgs e) { }
}
