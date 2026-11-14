// Lunarward：截图页（图库 + 批量管理）。上游依赖 Win2D 缩略图与 Windows.Graphics.Capture。
// M1 只补 x:Bind 契约；M3 接 portal/grim 捕获 + SkiaSharp 缩略图后重写。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Starward.Features.Screenshot;

public sealed partial class ScreenshotPage : PageBase, System.ComponentModel.INotifyPropertyChanged
{
    public ScreenshotPage()
    {
        OpenImageBatchConvertWindowCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(BatchConvert);
        // x:Bind 默认 OneTime：数据要在 InitializeComponent 之前准备好。
        LoadImages();

        this.InitializeComponent();
        // 诚实降级（D31/D33/D34）：M1 未移植截图子系统，明确告诉用户而不是留白
        ScrollViewer_Images.Visibility = HasImages ? Visibility.Visible : Visibility.Collapsed;
        if (this.FindName("StackPanel_Emoji") is StackPanel panel)
        {
            panel.Visibility = HasImages ? Visibility.Collapsed : Visibility.Visible;
            if (panel.Children.Count > 1 && panel.Children[1] is TextBlock block)
            {
                block.Text = $"还没有截图。按 Alt+D 可以截取当前屏幕，图片会保存到 {ScreenCaptureLinux.DefaultFolder}。";
            }
        }
    }

    private bool _mutliSelect;

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public bool MutliSelect
    {
        get => _mutliSelect;
        set
        {
            if (_mutliSelect == value)
            {
                return;
            }
            _mutliSelect = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(MutliSelect)));
        }
    }

    private string? _selectCountText;

    public string? SelectCountText
    {
        get => _selectCountText;
        private set
        {
            if (_selectCountText == value)
            {
                return;
            }
            _selectCountText = value;
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SelectCountText)));
        }
    }

    public ScreenshotViewSourceModel ScreenshotViewSource { get; } = new();


    /// <summary>
    /// 图库数据源：扁平列表（上游按日期分组，但 Uno 的分组 GridView 不渲染条目，见 L-48）。
    /// </summary>
    private void LoadImages()
    {
        var items = new List<ScreenshotItem>();
        try
        {
            string folder = ScreenCaptureLinux.DefaultFolder;
            if (Directory.Exists(folder))
            {
                items = Directory.EnumerateFiles(folder)
                    .Where(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                             || x.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                             || x.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    .Select(x => new FileInfo(x))
                    .OrderByDescending(x => x.LastWriteTime)
                    .Select(x => new ScreenshotItem(x.FullName))
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "读取截图目录失败");
        }
        foreach (ScreenshotItem item in items)
        {
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ScreenshotItem.IsSelected))
                {
                    UpdateSelectCount();
                }
            };
        }
        ScreenshotViewSource.View = items;
        HasImages = items.Count > 0;
        OpenViewerIfRequested(items);
        UpdateSelectCount();
    }


    public bool HasImages { get; private set; }


    private void UpdateSelectCount()
    {
        var items = (ScreenshotViewSource.View as IEnumerable<ScreenshotItem>)?.ToList() ?? new List<ScreenshotItem>();
        int selected = items.Count(x => x.IsSelected);
        SelectCountText = selected > 0 ? string.Format(Lang.Lunarward_Screenshot_SelectedCount, selected, items.Count) : string.Format(Lang.Lunarward_Screenshot_Count, items.Count);
    }

    public BitmapImage? Header { get; set; }

    public CommunityToolkit.Mvvm.Input.IRelayCommand ManageScreenshotFolderCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(OpenScreenshotFolder);
    public CommunityToolkit.Mvvm.Input.IRelayCommand OpenImageBatchConvertWindowCommand { get; }

    /// <summary>没有内置文件管理器，交给桌面：`xdg-open <folder>`。</summary>
    private static void OpenScreenshotFolder()
    {
        try
        {
            string folder = ScreenCaptureLinux.DefaultFolder;
            Directory.CreateDirectory(folder);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", folder) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "打开截图文件夹失败");
        }
    }


    private void GridView_Images_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        // LINT-ALLOW: Uno 没有拖拽重排，改为多选 + 批量命令（L-48 家族）
    }
    private void OpenViewerIfRequested(List<ScreenshotItem> items)
    {
        // 验证用：--viewer N 直接打开查看器（L-36：UI 点击在 XWayland 下不可靠）
        int index = Starward.Features.Dev.DevSwitches.ViewerIndex;
        if (index >= 0 && items.Count > 0)
        {
            try
            {
                new ScreenshotViewerWindow(items, Math.Min(index, items.Count - 1)).Activate();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Open screenshot viewer (dev switch)");
            }
        }
    }


    private void GridView_Images_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ScreenshotItem item)
        {
            // Lunarward：M3 起用自己的查看器（上游 ImageViewWindow2 走 Win2D，D31 排除）
            try
            {
                var items = (ScreenshotViewSource.View as IEnumerable<ScreenshotItem>)?.ToList() ?? new List<ScreenshotItem>();
                int index = items.IndexOf(item);
                new ScreenshotViewerWindow(items, index < 0 ? 0 : index).Activate();
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Open screenshot viewer");
                OpenWithDefaultApp(item.FilePath);
            }
        }
    }


    /// <summary>批量转换：把选中的（没选就是全部）PNG 转成 JPEG，写在同一目录。</summary>
    private void BatchConvert()
    {
        var items = (ScreenshotViewSource.View as IEnumerable<ScreenshotItem>)?.ToList() ?? new List<ScreenshotItem>();
        var targets = items.Where(x => x.IsSelected).ToList();
        if (targets.Count == 0)
        {
            targets = items;
        }
        if (targets.Count == 0)
        {
            Helpers.InAppToast.MainWindow?.Information("没有可转换的截图", null, 3000);
            return;
        }
        var (ok, skip, fail) = ScreenshotConverter.ConvertToJpeg(targets.Select(x => x.FilePath));
        Helpers.InAppToast.MainWindow?.Success((string?)null, $"批量转换完成：成功 {ok}，跳过 {skip}，失败 {fail}", 5000);
    }


    private static void OpenWithDefaultApp(string file)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", file) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "打开截图失败");
        }
    }
    private void MenuFlyoutItem_ScreenshotInfo_Loading(Microsoft.UI.Xaml.FrameworkElement sender, object e)
    {
        // LINT-ALLOW: 信息项在 Linux 版打开菜单前就同步填好了，不需要延迟加载回调
    }

    /// <summary>上游是 CollectionViewSource；x:Bind 走 .View 取视图集合。</summary>
    public sealed class ScreenshotViewSourceModel
    {
        public object? View { get; set; }
    }
}
