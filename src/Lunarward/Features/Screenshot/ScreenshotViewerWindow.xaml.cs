// Lunarward：上游的 ImageViewWindow/ImageViewWindow2 建立在 Win2D + DirectX 色彩管理上（D31 已排除），
// 这里用 Uno 自带的 BitmapImage 做一个够用的查看器：翻页、在文件夹中显示、Esc/方向键操作。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;


namespace Starward.Features.Screenshot;

public sealed partial class ScreenshotViewerWindow : WindowEx
{

    private List<ScreenshotItem> _items = new();

    private int _index;


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
        TextBlock_Status.Text = $"{_index + 1} / {_items.Count} · Esc 关闭 · ← → 切换";
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
