using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Frameworks;
using Starward.Helpers;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;


namespace Starward.Features.Background;

public sealed partial class BackgroundViewPage : PageBase
{


    private readonly ILogger<BackgroundViewPage> _logger = AppConfig.GetLogger<BackgroundViewPage>();


    public BackgroundViewPage()
    {
        this.InitializeComponent();
        SizeChanged += OnPageSizeChanged;
    }


    public ObservableCollection<BackgroundFileItem> BackgroundImages { get; set => SetProperty(ref field, value); }


    public string SelectCountText { get; set => SetProperty(ref field, value); }


    public string DeleteInfoText { get; set => SetProperty(ref field, value); }


    public bool MutliSelect
    {
        get; set
        {
            field = value;
            GridView_Images.SelectionMode = value ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
            UpdateSelectCountText();
        }
    }




    /// <summary>
    /// 窗口尺寸变化后重算条目宽度并让 WrapPanel 重新换行（D56 / L-97）。
    /// </summary>
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateTileSize();
        try
        {
            GridView_Images?.ItemsPanelRoot?.InvalidateMeasure();
            GridView_Images?.InvalidateMeasure();
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "background view reflow failed");
        }
    }


    /// <summary>
    /// 按可用宽度均分列宽（D56）：固定 216 DIP 的条目会让 1200 DIP 窗口右侧永远空 204 DIP。
    /// 这里按「能放几列」反推每条的实际宽度，让网格铺满；列数不变时也不会抖动。
    /// </summary>
    private void UpdateTileSize()
    {
        try
        {
            if (GridView_Images?.ItemsPanelRoot is not CommunityToolkit.WinUI.Controls.WrapPanel panel)
            {
                return;
            }
            double avail = GridView_Images.ActualWidth - GridView_Images.Padding.Left - GridView_Images.Padding.Right;
            if (avail <= 0)
            {
                return;
            }
            const double gap = 24;      // 容器 Margin 12 + WrapPanel 间距 12
            const double minTile = 216; // 低于这个宽度不再加列，避免条目缩得难看
            int cols = Math.Max(1, (int)Math.Floor((avail + gap) / (minTile + gap)));
            double tile = Math.Floor((avail - (gap * (cols - 1))) / cols);
            tile = Math.Max(120, tile);
            double height = Math.Round(tile * 122.0 / 216.0);
            double currentWidth = panel.ItemWidth ?? 0;
            double currentHeight = panel.ItemHeight ?? 0;
            if (Math.Abs(currentWidth - tile) < 0.5 && Math.Abs(currentHeight - height) < 0.5)
            {
                return;
            }
            panel.ItemWidth = tile;
            panel.ItemHeight = height;
            panel.InvalidateMeasure();
            Serilog.Log.Debug("background view tiles: avail={avail} cols={cols} tile={tile}x{height}", avail, cols, tile, height);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "background view tile size failed");
        }
    }

    protected override void OnLoaded()
    {
        LoadBackgroundItems();
    }


    protected override void OnUnloaded()
    {
        try
        {
            BackgroundImages?.Clear();
            BackgroundImages = null!;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }


    private void LoadBackgroundItems()
    {
        try
        {
            string folder = Path.Join(AppConfig.CacheFolder, "bg");
            if (Directory.Exists(folder))
            {
                string[] files = Directory.GetFiles(folder);
                var result = files.Select(x => new BackgroundFileItem(x));
                if (RadioMenuFlyoutItem_Filter_Image.IsChecked)
                {
                    result = result.Where(x => !x.IsVideo);
                }
                if (RadioMenuFlyoutItem_Filter_Video.IsChecked)
                {
                    result = result.Where(x => x.IsVideo);
                }
                if (RadioMenuFlyoutItem_Sort_Time.IsChecked)
                {
                    result = result.OrderByDescending(x => x.CreationTime);
                }
                if (RadioMenuFlyoutItem_Sort_FileSize.IsChecked)
                {
                    result = result.OrderByDescending(x => x.FileSize);
                }
                BackgroundImages = new(result);
                UpdateSelectCountText();
                DispatcherQueue?.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, UpdateTileSize);
            }
            else
            {
                StackPanel_NoFolder.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Load background items.");
        }
    }



    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        try
        {
            string folder = Path.Join(AppConfig.CacheFolder, "bg");
            if (Directory.Exists(folder))
            {
                // Lunarward：Uno 的 Launcher 在 Linux 上是空实现（L-74），统一走 xdg-open。
                if (!Helpers.OpenUrl.Open(folder))
                {
                    _logger.LogWarning("No handler to open folder {Folder}", folder);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open background folder.");
        }
    }


    [RelayCommand]
    private async Task DeleteDuplicateBgAsync()
    {
        try
        {
            string folder = Path.Join(AppConfig.CacheFolder, "bg");
            if (Directory.Exists(folder))
            {
                int count = 0;
                string[] files = Directory.GetFiles(folder);
                ConcurrentDictionary<string, bool> dict = new();
                await Parallel.ForEachAsync(files, async (file, _) =>
                {
                    using FileStream fs = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    string hash = Convert.ToHexString(await MD5.HashDataAsync(fs));
                    if (dict.TryAdd(hash, true))
                    {
                        return;
                    }
                    fs.Dispose();
                    File.Delete(file);
                    Interlocked.Increment(ref count);
                });
                DeleteInfoText = string.Format(Lang.BackgroundViewPage_0DuplicateFileSHasBeenDeleted, count);
                LoadBackgroundItems();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete duplicate background files.");
        }
    }


    private void RadioMenuFlyoutItem_FilterSort_Click(object sender, RoutedEventArgs e)
    {
        LoadBackgroundItems();
    }



    private async void GridView_Images_ItemClick(object sender, ItemClickEventArgs e)
    {
        try
        {
            if (GridView_Images.SelectionMode is ListViewSelectionMode.None)
            {
                if (e.ClickedItem is BackgroundFileItem item)
                {
                    Starward.Helpers.OpenUrl.Open(item.FilePath);
                }
            }
        }
        catch { }
    }


    private async void GridView_Images_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        try
        {
            var list = new List<StorageFile>();
            foreach (var dragItem in e.Items)
            {
                if (dragItem is BackgroundFileItem item)
                {
                    if (File.Exists(item.FilePath))
                    {
                        var file = await StorageFile.GetFileFromPathAsync(item.FilePath);
                        list.Add(file);
                    }
                }
            }
            if (list.Count > 0)
            {
                e.Data.RequestedOperation = DataPackageOperation.Copy;
                e.Data.SetStorageItems(list, true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Drag image starting");
        }
    }



    private void GridView_Images_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateSelectCountText();
    }



    private void UpdateSelectCountText()
    {
        try
        {
            if (MutliSelect)
            {
                SelectCountText = $"{GridView_Images.SelectedItems.Count}/{BackgroundImages?.Count ?? 0}";
            }
            else
            {
                SelectCountText = $"{BackgroundImages?.Count ?? 0}";
            }
        }
        catch { }
    }



    private async void MenuFlyoutItem_Open_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement { DataContext: BackgroundFileItem item })
            {
                Starward.Helpers.OpenUrl.Open(item.FilePath);
            }
        }
        catch { }
    }


    private async void MenuFlyoutItem_CopyFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (GridView_Images.SelectionMode is ListViewSelectionMode.Multiple && GridView_Images.SelectedItems.Count > 0)
            {
                var list = new List<StorageFile>();
                foreach (BackgroundFileItem item in GridView_Images.SelectedItems.Cast<BackgroundFileItem>())
                {
                    if (File.Exists(item.FilePath))
                    {
                        var file = await StorageFile.GetFileFromPathAsync(item.FilePath);
                        list.Add(file);
                    }
                }
                if (list.Count > 0)
                {
                    ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, list.ToArray());
                }
            }
            else if (sender is FrameworkElement fe && fe.DataContext is BackgroundFileItem item)
            {
                if (File.Exists(item.FilePath))
                {
                    var file = await StorageFile.GetFileFromPathAsync(item.FilePath);
                    ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy file to clipboard");
        }
    }


    private async void MenuFlyoutItem_OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.DataContext is BackgroundFileItem item)
            {
                if (File.Exists(item.FilePath))
                {
                    // Linux 没有「打开文件夹并选中某个文件」的通用接口，退一步打开所在目录。
                    string? folder = Path.GetDirectoryName(item.FilePath);
                    if (string.IsNullOrEmpty(folder) || !Helpers.OpenUrl.Open(folder))
                    {
                        _logger.LogWarning("No handler to open folder of {File}", item.FilePath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file in explorer");
        }
    }


    private async void MenuFlyoutItem_OpenWithDefault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.DataContext is BackgroundFileItem item)
            {
                if (File.Exists(item.FilePath))
                {
                    if (!Helpers.OpenUrl.Open(item.FilePath))
                    {
                        _logger.LogWarning("No handler to open file {File}", item.FilePath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file with default application");
        }
    }


    private async void MenuFlyoutItem_OpenWith_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement fe && fe.DataContext is BackgroundFileItem item)
            {
                if (File.Exists(item.FilePath))
                {
                    // 「选择打开方式」的对话框在 Linux 上由文件管理器/桌面环境负责，这里只做默认打开。
                    if (!Helpers.OpenUrl.Open(item.FilePath))
                    {
                        _logger.LogWarning("No handler to open file {File}", item.FilePath);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file with application picker");
        }
    }


    private async void MenuFlyoutItem_Delete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (GridView_Images.SelectionMode is ListViewSelectionMode.Multiple && GridView_Images.SelectedItems.Count > 0)
            {
                var list = GridView_Images.SelectedItems.Cast<BackgroundFileItem>().ToList();
                foreach (BackgroundFileItem item in list)
                {
                    if (File.Exists(item.FilePath))
                    {
                        var file = await StorageFile.GetFileFromPathAsync(item.FilePath);
                        await file.DeleteAsync();
                    }
                    BackgroundImages?.Remove(item);
                }
            }
            else if (sender is FrameworkElement fe && fe.DataContext is BackgroundFileItem item)
            {
                if (File.Exists(item.FilePath))
                {
                    var file = await StorageFile.GetFileFromPathAsync(item.FilePath);
                    await file.DeleteAsync();
                }
                BackgroundImages?.Remove(item);
            }
            UpdateSelectCountText();
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Failed to delete image file");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete image file");
        }
    }


}
