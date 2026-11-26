// Lunarward：截图页（图库 + 批量管理）。上游依赖 Win2D 缩略图与 Windows.Graphics.Capture。
// M1 只补 x:Bind 契约；M3 接 portal/grim 捕获 + SkiaSharp 缩略图后重写。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Starward.Features;
using Starward.Features.GameLauncher;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.Screenshot;

public sealed partial class ScreenshotPage : PageBase, System.ComponentModel.INotifyPropertyChanged
{
    public ScreenshotPage()
    {
        OpenImageBatchConvertWindowCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(BatchConvertAsync);
        ManageScreenshotFolderCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(ManageScreenshotFolderAsync);
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
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(CheckBoxVisibility)));
            if (!value)
            {
                ClearSelection();
            }
            ApplyCheckBoxVisibility();
            UpdateSelectCount();
        }
    }

    /// <summary>B06：只有多选模式才显示条目上的勾选框，否则整个条目就是「点开看图」。</summary>
    public Microsoft.UI.Xaml.Visibility CheckBoxVisibility => _mutliSelect ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

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
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".jxr", ".webp", ".heic", ".avif", ".jxl"];

    private static bool IsSupportedFile(string path)
    {
        string extension = Path.GetExtension(path);
        return Array.Exists(SupportedExtensions, x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase));
    }


    /// <summary>
    /// D134：上游支持「默认目录 + 每游戏的外部截图目录」（设置页/管理对话框可加）。
    /// 移植侧以前只读默认目录，管理对话框也就成了摆设；这里按 AppConfig 里登记的列表逐个读。
    /// </summary>
    private IReadOnlyList<string> ScreenshotFolders()
    {
        var folders = new List<string> { ScreenCaptureLinux.DefaultFolder };
        // 上游 ScreenshotPage.xaml.cs:199-248：除了默认目录与外部目录，还要读
        // ①「本程序备份目录」= <ScreenshotFolder 或 UserData/Screenshots>/<exe 名>
        // ②「游戏内截图目录」= <安装路径>/<各游戏自己的截图子目录>
        // 移植侧以前只读默认目录，游戏里按 F12 存的图和备份目录里的都看不见（D135）。
        try
        {
            string? exeName = GameLauncherService.GetGameExeName(CurrentGameBiz)?.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
            string? relativePath = CurrentGameBiz.Game switch
            {
                Starward.Core.GameBiz.hk4e => "ScreenShot",
                Starward.Core.GameBiz.hkrpg => Path.Combine("StarRail_Data", "ScreenShots"),
                Starward.Core.GameBiz.bh3 => "ScreenShot",
                Starward.Core.GameBiz.nap => "ScreenShot",
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(exeName))
            {
                string backupRoot = Directory.Exists(AppConfig.ScreenshotFolder)
                    ? AppConfig.ScreenshotFolder
                    : Path.Combine(AppConfig.UserDataFolder, "Screenshots");
                string backup = Path.Combine(backupRoot, exeName);
                if (Directory.Exists(backup))
                {
                    folders.Add(backup);
                }
            }
            if (!string.IsNullOrWhiteSpace(exeName) && !string.IsNullOrWhiteSpace(relativePath))
            {
                string? installPath = GameLauncherService.GetGameInstallPath(CurrentGameId);
                if (!string.IsNullOrWhiteSpace(installPath))
                {
                    string inGame = Path.Combine(installPath, relativePath);
                    if (Directory.Exists(inGame))
                    {
                        folders.Add(inGame);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "解析游戏截图目录失败");
        }
        string? external = AppConfig.GetExternalScreenshotFolder(CurrentGameId);
        if (!string.IsNullOrWhiteSpace(external))
        {
            foreach (string part in external.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!folders.Contains(part, StringComparer.Ordinal))
                {
                    folders.Add(part);
                }
            }
        }
        return folders;
    }


    private void LoadImages()
    {
        var items = new List<ScreenshotItem>();
        foreach (string folder in ScreenshotFolders())
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }
                items.AddRange(Directory.EnumerateFiles(folder)
                    .Where(IsSupportedFile)
                    .Select(x => new FileInfo(x))
                    .Select(x => new ScreenshotItem(x.FullName)));
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "读取截图目录失败：{folder}", folder);
            }
        }
        items = items.OrderByDescending(x => x.CreationTime).ToList();
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
        ApplyCheckBoxVisibility();
        HasImages = items.Count > 0;
        OpenViewerIfRequested(items);
        UpdateSelectCount();
    }


    public bool HasImages { get; private set; }

    // 上游用 _watchers 列表（每个目录一个）；移植侧以前只有单个 _watcher 且从不释放，
    // 页面 NavigationCacheMode=Required 会一直存活，换游戏/换目录后旧 watcher 仍在跑（D135）。
    private readonly List<FileSystemWatcher> _watchers = [];

    private string _watcherKey = "";

    private void ApplyCheckBoxVisibility()
    {
        if (ScreenshotViewSource.View is not IEnumerable<ScreenshotItem> items)
        {
            return;
        }
        Visibility visibility = CheckBoxVisibility;
        foreach (ScreenshotItem item in items)
        {
            item.CheckBoxVisibility = visibility;
        }
    }


    private void ClearSelection()
    {
        if (ScreenshotViewSource.View is not IEnumerable<ScreenshotItem> items)
        {
            return;
        }
        foreach (ScreenshotItem item in items)
        {
            item.IsSelected = false;
        }
    }


    /// <summary>B16：缓存页面只会在第一次构造时加载，挂载时重新枚举，并挂目录监视器。</summary>
    protected override void OnLoaded()
    {
        base.OnLoaded();
        LoadImages();
        EnsureWatcher();
    }


    private void EnsureWatcher()
    {
        try
        {
            List<string> folders = ScreenshotFolders().Where(Directory.Exists).ToList();
            string key = string.Join('|', folders);
            if (string.Equals(_watcherKey, key, StringComparison.Ordinal) && _watchers.Count > 0)
            {
                return;
            }
            DisposeWatchers();
            foreach (string folder in folders)
            {
                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                };
                void OnChange(object? _, FileSystemEventArgs e) => DispatcherQueue.TryEnqueue(LoadImages);
                watcher.Created += OnChange;
                watcher.Deleted += OnChange;
                watcher.Renamed += (_, e) => DispatcherQueue.TryEnqueue(LoadImages);
                watcher.Changed += OnChange;
                _watchers.Add(watcher);
            }
            _watcherKey = key;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "监视截图目录失败");
        }
    }


    private void DisposeWatchers()
    {
        foreach (FileSystemWatcher watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch (Exception ex)
            {
                Serilog.Log.Debug(ex, "释放截图目录监视器失败");
            }
        }
        _watchers.Clear();
        _watcherKey = "";
    }


    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        DisposeWatchers();
    }


    private void UpdateSelectCount()
    {
        var items = (ScreenshotViewSource.View as IEnumerable<ScreenshotItem>)?.ToList() ?? new List<ScreenshotItem>();
        int selected = items.Count(x => x.IsSelected);
        SelectCountText = selected > 0 ? string.Format(Lang.Lunarward_Screenshot_SelectedCount, selected, items.Count) : string.Format(Lang.Lunarward_Screenshot_Count, items.Count);
    }

    public BitmapImage? Header { get; set; }

    public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand ManageScreenshotFolderCommand { get; }

    /// <summary>与上游一致：打开「管理截图文件夹」对话框（默认目录 + 可增删的外部目录）。</summary>
    private async Task ManageScreenshotFolderAsync()
    {
        try
        {
            var folders = ScreenshotFolders()
                .Select((path, i) => new Starward.Features.Screenshot.ScreenshotFolder(path) { Default = i == 0 })
                .ToList();
            var dialog = new Starward.Features.Screenshot.ScreenshotFolderManageDialog
            {
                Folders = folders,
                CurrentGameId = CurrentGameId,
                XamlRoot = this.XamlRoot,
            };
            await Helpers.DialogGate.Show(dialog);
            if (dialog.FolderChanged)
            {
                string value = string.Join(';', dialog.Folders.Where(x => x.CanRemove).Select(x => x.Folder));
                AppConfig.SetExternalScreenshotFolder(CurrentGameId, value);
                LoadImages();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "管理截图文件夹失败");
        }
    }
    public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand OpenImageBatchConvertWindowCommand { get; }

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


    /// <summary>菜单项的 Tag 是它所属的条目（Uno 的 MenuFlyoutItem 不回传 DataContext）。</summary>
    private static ScreenshotItem? ItemOf(object sender) => (sender as FrameworkElement)?.Tag as ScreenshotItem;


    /// <summary>
    /// 右键菜单：上游写在 XAML 的 `Grid.ContextFlyout` 里。
    /// 2026-11-26 更正（E2）：以前这里写「Uno 的 Grid 没有这个属性（CS0117）」——那是搞错了 API，
    /// `ContextFlyout` 是 `UIElement` 上的属性，Grid 继承得到；代码里要设得走 `FlyoutBase.SetAttachedFlyout`，
    /// 直接 `grid.ContextFlyout = menu` 才编译不过。这里保留 RightTapped 现造菜单的做法，原因是菜单项
    /// 依赖当前条目（文件信息、勾选状态），写在 code-behind 里比 XAML 里绑一组命令更短。
    /// 项与上游 ScreenshotPage.xaml:121-213 一一对应，少了「打开方式」（Windows 专有对话框）——
    /// 「用默认应用打开」已覆盖同一需求。D134/D138。
    /// </summary>
    private void Item_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not ScreenshotItem item)
        {
            return;
        }
        var menu = new MenuFlyout();
        var info = new MenuFlyoutItem
        {
            Text = item.FileInfo,
            IsEnabled = false,
            MinWidth = 208,
            FontSize = 12,
        };
        menu.Items.Add(info);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateMenuItem(Lang.Common_Open, "\uE8A7", item, MenuFlyoutItem_Open_Click));
        menu.Items.Add(CreateMenuItem(Lang.ImageViewWindow2_CopyFile, "\uE8C8", item, MenuFlyoutItem_CopyFile_Click));
        menu.Items.Add(CreateMenuItem(Lang.ImageViewWindow2_CopyAsJPG, "\uE74E", item, MenuFlyoutItem_CopyAsJPG_Click));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(CreateMenuItem(Lang.ImageViewWindow2_OpenInExplorer, "\uEC50", item, MenuFlyoutItem_OpenInExplorer_Click));
        menu.Items.Add(CreateMenuItem(Lang.ImageViewWindow2_OpenWithDefaultApp, "\uE91B", item, MenuFlyoutItem_OpenWithDefault_Click));
        menu.Items.Add(new MenuFlyoutSeparator());
        MenuFlyoutItem delete = CreateMenuItem(Lang.Common_Delete, "\uE74D", item, MenuFlyoutItem_Delete_Click);
        delete.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        menu.Items.Add(delete);

        item.UpdatePixelSize();
        menu.ShowAt(element, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions { Position = e.GetPosition(element) });
    }


    private static MenuFlyoutItem CreateMenuItem(string text, string glyph, ScreenshotItem item, RoutedEventHandler handler)
    {
        var menuItem = new MenuFlyoutItem
        {
            Text = text,
            Tag = item,
            Icon = new FontIcon { Glyph = glyph },
        };
        menuItem.Click += handler;
        return menuItem;
    }


    // ── 右键菜单（对齐上游 ScreenshotPage.xaml.cs:445-700 的六个处理器）────────────────────
    // 上游还有「打开方式」（Windows 的 OpenWith 对话框）与「复制为 JPG 后再粘图片」，
    // Linux 侧前者没有对应物（xdg-open 已覆盖「用默认应用打开」），后者的语义由
    // CopyAsJPG 承担（转 JPG 后把文件放剪贴板）。D134。

    private void MenuFlyoutItem_ScreenshotInfo_Loading(FrameworkElement sender, object args)
    {
        // 上游在这里补「像素大小」——FileInfo 初值是「格式 + 体积」，Loading 时才去读尺寸。
        if (sender.DataContext is ScreenshotItem item)
        {
            item.UpdatePixelSize();
        }
    }


    private void MenuFlyoutItem_Open_Click(object sender, RoutedEventArgs e)
    {
        if (ItemOf(sender) is ScreenshotItem item)
        {
            OpenItem(item);
        }
    }


    /// <summary>
    /// 上游 ScreenshotPage.xaml.cs:392-418：把选中的截图拖到文件管理器。
    /// 移植侧用的是 ItemsControl（Uno 的 GridView 分组不渲染条目，L-48），没有 DragItemsStarting，
    /// 所以改成条目根元素的 DragStarting（一次只带当前这一张，多选拖出上游也只走单张）。
    /// </summary>
    private async void Item_DragStarting(UIElement sender, DragStartingEventArgs e)
    {
        try
        {
            if (ItemOf(sender) is not ScreenshotItem item || !File.Exists(item.FilePath))
            {
                return;
            }
            e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(item.FilePath);
            e.Data.SetStorageItems([file], true);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "拖出截图失败");
        }
    }


    private async void MenuFlyoutItem_CopyFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            List<string> files = SelectedOrSingle(sender);
            if (files.Count == 0)
            {
                return;
            }
            var storageFiles = new List<Windows.Storage.IStorageItem>();
            foreach (string path in files)
            {
                if (File.Exists(path))
                {
                    storageFiles.Add(await Windows.Storage.StorageFile.GetFileFromPathAsync(path));
                }
            }
            if (storageFiles.Count == 0)
            {
                return;
            }
            Helpers.ClipboardHelper.SetStorageItems(Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy, storageFiles.ToArray());
            Helpers.InAppToast.MainWindow?.Success((string?)null, $"已复制 {storageFiles.Count} 个文件", 3000);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "复制截图文件失败");
            Helpers.InAppToast.MainWindow?.Warning((string?)null, "复制文件失败（剪贴板不可用）", 4000);
        }
    }


    private async void MenuFlyoutItem_CopyAsJPG_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            List<string> files = SelectedOrSingle(sender);
            if (files.Count == 0)
            {
                return;
            }
            string temp = Path.Combine(Path.GetTempPath(), $"lunarward-copy-{Guid.NewGuid():N}.jpg");
            bool ok = await Task.Run(() => ConvertFirstToJpeg(files[0], temp));
            if (!ok)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, "转换 JPG 失败", 4000);
                return;
            }
            Helpers.ClipboardHelper.SetImageFile(temp);
            Helpers.InAppToast.MainWindow?.Success((string?)null, "已复制为 JPG 图片", 3000);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "复制为 JPG 失败");
        }
    }


    private static bool ConvertFirstToJpeg(string source, string target)
    {
        using SkiaSharp.SKBitmap? bitmap = Helpers.ImageDecode.Load(source);
        if (bitmap is null)
        {
            return false;
        }
        using SkiaSharp.SKImage image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using SkiaSharp.SKData data = image.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 92);
        using FileStream stream = File.Create(target);
        data.SaveTo(stream);
        return true;
    }


    private void MenuFlyoutItem_OpenInExplorer_Click(object sender, RoutedEventArgs e)
    {
        // Linux 没有资源管理器：打开文件所在目录（xdg-open 会交给文件管理器）。
        try
        {
            List<string> files = SelectedOrSingle(sender);
            string? folder = files.Count > 0 ? Path.GetDirectoryName(files[0]) : null;
            if (string.IsNullOrEmpty(folder))
            {
                folder = ScreenCaptureLinux.DefaultFolder;
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", folder!) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "打开截图目录失败");
        }
    }


    private void MenuFlyoutItem_OpenWithDefault_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            List<string> files = SelectedOrSingle(sender);
            if (files.Count > 0)
            {
                OpenWithDefaultApp(files[0]);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "用默认应用打开截图失败");
        }
    }


    private void MenuFlyoutItem_Delete_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            List<string> files = SelectedOrSingle(sender);
            int deleted = 0;
            foreach (string path in files)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    deleted++;
                }
            }
            if (deleted > 0)
            {
                Helpers.InAppToast.MainWindow?.Success((string?)null, $"已删除 {deleted} 张截图", 3000);
                LoadImages();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            Serilog.Log.Warning(ex, "删除截图失败（权限或占用）");
            Helpers.InAppToast.MainWindow?.Warning((string?)null, "删除失败：没有权限或文件被占用", 5000);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "删除截图失败");
        }
    }


    /// <summary>多选时有勾选就按勾选，否则退回右键那一张（上游同样先看 SelectedItems）。</summary>
    private List<string> SelectedOrSingle(object sender)
    {
        var items = (ScreenshotViewSource.View as IEnumerable<ScreenshotItem>)?.ToList() ?? new List<ScreenshotItem>();
        List<ScreenshotItem> selected = MutliSelect ? items.Where(x => x.IsSelected).ToList() : new List<ScreenshotItem>();
        if (selected.Count == 0 && ItemOf(sender) is ScreenshotItem single)
        {
            selected.Add(single);
        }
        return selected.Select(x => x.FilePath).ToList();
    }


    /// <summary>B07：ItemsControl 没有 ItemClick，条目根元素自己接 Tapped。</summary>
    private void Item_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if ((sender as Microsoft.UI.Xaml.FrameworkElement)?.DataContext is not ScreenshotItem item)
        {
            return;
        }
        e.Handled = true;
        if (MutliSelect)
        {
            item.IsSelected = !item.IsSelected;
            return;
        }
        OpenItem(item);
    }


    private void OpenItem(ScreenshotItem item)
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


    /// <summary>
    /// 批量转换：多选模式只转勾选的（B15：没勾选就不动，不再默认全库）；非多选模式整库转换。
    /// 转换放到线程池（B45），避免几十张图在 UI 线程上逐张解码+编码导致窗口卡死。
    /// </summary>
    private async Task BatchConvertAsync()
    {
        var items = (ScreenshotViewSource.View as IEnumerable<ScreenshotItem>)?.ToList() ?? new List<ScreenshotItem>();
        // 上游是「打开转换窗口 → 选文件 → 选格式」，移植侧没有那个窗口（ImageBatchConvertWindow
        // 被 Compile Remove）。这里只保留「批量转 JPG」，所以必须要求先勾选：
        // 非多选模式以前会拿整个图库当输入，点一下就在库里凭空多出一倍 .jpg（D134）。
        List<ScreenshotItem> targets = MutliSelect ? items.Where(x => x.IsSelected).ToList() : new List<ScreenshotItem>();
        if (targets.Count == 0)
        {
            Helpers.InAppToast.MainWindow?.Information((string?)null, "请先切换多选并勾选要转换的截图", 3000);
            return;
        }
        int quality = AppConfig.ScreenCaptureEncodeQuality switch { 0 => 80, 1 => 90, _ => 100 };
        var (ok, skip, fail) = await Task.Run(() => ScreenshotConverter.ConvertToJpeg(targets.Select(x => x.FilePath), quality));
        Helpers.InAppToast.MainWindow?.Success((string?)null, $"批量转换完成：成功 {ok}，跳过 {skip}，失败 {fail}", 5000);
        LoadImages();
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
    /// <summary>上游是 CollectionViewSource；x:Bind 走 .View 取视图集合。</summary>
    public sealed class ScreenshotViewSourceModel
    {
        public object? View { get; set; }
    }
}
