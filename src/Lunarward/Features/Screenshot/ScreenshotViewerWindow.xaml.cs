// Lunarward：上游的 ImageViewWindow/ImageViewWindow2 建立在 Win2D + DirectX 色彩管理上（D31 已排除），
// 这里用 Uno 自带的 BitmapImage 做一个够用的查看器：翻页、在文件夹中显示、Esc/方向键操作。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Features;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;


namespace Starward.Features.Screenshot;

public sealed partial class ScreenshotViewerWindow : WindowBase
{

    private List<ScreenshotItem> _items = new();

    private int _index;

    // D135：上游 ImageViewWindow2 支持缩放/适应/全屏/复制/删除/另存/外部打开（1572 行，含 Win2D 色彩管理）。
    // 移植侧用「按窗口尺寸比例缩放 Image」实现同一批操作，Win2D 预览与 HDR 留 M4。
    private double _zoom = 1;


    public ScreenshotViewerWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
    }


    public ScreenshotViewerWindow(IEnumerable<ScreenshotItem> items, int index) : this()
    {
        _items = new List<ScreenshotItem>(items);
        _index = Math.Clamp(index, 0, Math.Max(0, _items.Count - 1));
        _ = ShowCurrentAsync();
    }


    private void InitializeWindow()
    {
        try
        {
            AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            AdaptTitleBarButtonColorToActuallTheme();
            CenterInScreen(1080, 720);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Initialize screenshot viewer window");
        }
        // Uno 的 Window 没有 KeyDown 事件，键事件挂在内容根上；
        // 窗口没焦点时它不会触发，所以快捷键只作加分项，主路径始终是按钮。
        RootGrid.KeyDown += ScreenshotViewerWindow_KeyDown;
        RootGrid.Loaded += (_, _) => RootGrid.Focus(FocusState.Programmatic);
    }


    private void ScreenshotViewerWindow_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        // Wayland/XWayland 下窗口键事件并不总是送到这里，按钮始终可用，快捷键只是加分项。
        switch (e.Key)
        {
            case Windows.System.VirtualKey.Escape:
                Close();
                break;
            case Windows.System.VirtualKey.Left:
                _ = MoveAsync(-1);
                break;
            case Windows.System.VirtualKey.Right:
                _ = MoveAsync(1);
                break;
        }
    }


    private async Task MoveAsync(int delta)
    {
        if (_items.Count == 0)
        {
            return;
        }
        _index = (_index + delta + _items.Count) % _items.Count;
        await ShowCurrentAsync();
    }


    private async Task ShowCurrentAsync()
    {
        if (_items.Count == 0)
        {
            TextBlock_FileName.Text = "没有截图";
            TextBlock_Status.Text = "截图文件夹是空的";
            Button_Previous.IsEnabled = false;
            Button_Next.IsEnabled = false;
            return;
        }
        ScreenshotItem item = _items[_index];
        TextBlock_FileName.Text = Path.GetFileName(item.FilePath);
        UpdateStatus();
        Button_Previous.IsEnabled = _items.Count > 1;
        Button_Next.IsEnabled = _items.Count > 1;
        try
        {
            byte[] bytes = await File.ReadAllBytesAsync(item.FilePath);
            BitmapImage bitmap = new();
            using (MemoryStream stream = new(bytes))
            {
                await bitmap.SetSourceAsync(stream.AsRandomAccessStream());
            }
            // 赋值必须回到 UI 线程：SetSourceAsync 的续体在线程池上，直接赋 Source 会静默不生效。
            DispatcherQueue.TryEnqueue(() => Image_Preview.Source = bitmap);
        }
        catch (Exception ex)
        {
            Image_Preview.Source = null;
            TextBlock_Status.Text = $"无法打开这张图片：{ex.Message}";
            Serilog.Log.Warning(ex, "Open screenshot {path}", item.FilePath);
        }
    }


    private void UpdateStatus()
    {
        TextBlock_Status.Text = $"{_index + 1} / {_items.Count} · 缩放 {_zoom * 100:0}% · Esc 关闭 · ← → 切换";
    }


    private void ApplyZoom()
    {
        if (Image_Preview.Source is not BitmapImage bitmap || double.IsNaN(Image_Preview.Width))
        {
            return;
        }
        Image_Preview.Width = Math.Max(1, _naturalWidth * _zoom);
        Image_Preview.Height = Math.Max(1, _naturalHeight * _zoom);
        UpdateStatus();
    }


    private double _naturalWidth;

    private double _naturalHeight;


    private void CaptureNaturalSize()
    {
        // BitmapImage.PixelWidth/Height 拿得到原始像素尺寸；首次显示时把它当作 100%。
        if (Image_Preview.Source is BitmapImage bitmap && bitmap.PixelWidth > 0)
        {
            _naturalWidth = bitmap.PixelWidth;
            _naturalHeight = bitmap.PixelHeight;
        }
    }


    private void Button_ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        CaptureNaturalSize();
        _zoom = Math.Min(_zoom * 1.25, 8);
        ApplyZoom();
    }


    private void Button_ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        CaptureNaturalSize();
        _zoom = Math.Max(_zoom / 1.25, 0.1);
        ApplyZoom();
    }


    private void Button_ZoomReset_Click(object sender, RoutedEventArgs e)
    {
        Image_Preview.Width = double.NaN;
        Image_Preview.Height = double.NaN;
        _zoom = 1;
        UpdateStatus();
    }


    private void Button_Copy_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            return;
        }
        try
        {
            Helpers.ClipboardHelper.SetImageFile(_items[_index].FilePath);
            Helpers.InAppToast.MainWindow?.Success((string?)null, "已复制图片", 2000);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Copy screenshot to clipboard");
        }
    }


    private async void Button_SaveAs_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            return;
        }
        string source = _items[_index].FilePath;
        try
        {
            string? target = await Helpers.FileDialogHelper.OpenSaveFileDialogAsync(RootGrid.XamlRoot, Path.GetFileName(source));
            if (string.IsNullOrWhiteSpace(target))
            {
                return;
            }
            await Task.Run(() => File.Copy(source, target, true));
            Helpers.InAppToast.MainWindow?.Success((string?)null, "已另存", 2000);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Save screenshot as");
            Helpers.InAppToast.MainWindow?.Warning((string?)null, $"另存失败：{ex.Message}", 4000);
        }
    }


    private void Button_OpenWith_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            return;
        }
        try
        {
            // 用默认应用打开这一张（不是只开目录）：xdg-open 交给桌面环境挑关联程序。
            Helpers.OpenUrl.Open(_items[_index].FilePath);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Open screenshot with default app");
        }
    }


    private async void Button_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_items.Count == 0)
        {
            return;
        }
        string path = _items[_index].FilePath;
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            _items.RemoveAt(_index);
            if (_items.Count == 0)
            {
                Close();
                return;
            }
            _index = Math.Clamp(_index, 0, _items.Count - 1);
            await ShowCurrentAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Delete screenshot");
            Helpers.InAppToast.MainWindow?.Warning((string?)null, $"删除失败：{ex.Message}", 4000);
        }
    }


    private async void Button_Previous_Click(object sender, RoutedEventArgs e) => await MoveAsync(-1);


    private async void Button_Next_Click(object sender, RoutedEventArgs e) => await MoveAsync(1);


    private void Button_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_items.Count > 0)
            {
                System.Diagnostics.Process.Start("xdg-open", Path.GetDirectoryName(_items[_index].FilePath)!);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Open screenshot folder");
        }
    }


    private void Button_Close_Click(object sender, RoutedEventArgs e) => Close();

}
