// Lunarward：设置 → 运行环境（Profile 列表）。D28：Profile 是全局的，条目每游戏独立（后启动列表在游戏设置页）。
// 说明：列表用代码构建而非 DataTemplate —— Uno 的 ListView/ItemsControl 在星号行 + ScrollViewer 里
// 存在测量问题（行高被压成一行、内容被裁），M1 先用确定性代码路径；M2 换回模板并复测（见 12-shims-and-gaps）。
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using Starward.Core;
using Starward.Features.Runtime;
using Starward.Language;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;


namespace Starward.Features.Setting;


public sealed partial class RuntimeEnvironmentSetting : PageBase
{

    private readonly ILogger<RuntimeEnvironmentSetting> _logger = AppConfig.GetLogger<RuntimeEnvironmentSetting>();

    /// <summary>R23：ContentDialog 同一时间只允许一个；连点检查类按钮时第二次 ShowAsync 会直接抛。</summary>
    private bool _dialogOpen;
    private bool _shortcutsAttached;


    public RuntimeEnvironmentSetting()
    {
        this.InitializeComponent();
        RegisterShortcuts();
        // Uno 的 ScrollViewer 在「禁用横向滚动」时仍会给内容无限宽度，横向 StackPanel/Grid 的星号列就没有约束，
        // 表现为文字不省略、右侧徽标压到文字上甚至超出窗口（D58 v2 截图实测）。这里显式把列表宽度钉到可视区。
        SizeChanged += (_, _) => UpdateContentWidth();
        // F：自绘页签要自己补键盘导航（L-153：Pivot 换掉之后左右键也跟着没了）
        TabStrip.KeyDown += TabStrip_KeyDown;
        LoadProfiles();
        LoadComponents();
        LoadPrefixes();
        InitializeEnhance();
        _ = RefreshChipsAsync();
    }


    /// <summary>R22/R29：路径比较统一口径（GetFullPath + 去尾部分隔符），避免 `..` 或尾斜杠导致「没人在用」的误判。</summary>
    private static string NormalizePrefix(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }


    private static bool SamePrefix(string? a, string? b) => string.Equals(NormalizePrefix(a), NormalizePrefix(b), StringComparison.Ordinal);


    /// <summary>R29：手动填的前缀路径在保存时就校验，不要等到启动时 EnsurePrefix 才炸。</summary>
    private static string? ValidatePrefixPathInput(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        if (!Path.IsPathFullyQualified(path))
        {
            return Lang.Lunarward_RuntimeEnv_PrefixMustBeAbsolute;
        }
        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full) ?? "/";
        if (full.TrimEnd(Path.DirectorySeparatorChar) == root.TrimEnd(Path.DirectorySeparatorChar))
        {
            return Lang.Lunarward_RuntimeEnv_PrefixMustNotBeRoot;
        }
        return null;
    }


    /// <summary>R26：预检「去设置」带过来的目标方案/分组，在页面挂载后消费一次。</summary>
    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        (string? ProfileId, bool Enhance)? target = SettingNavigation.TakeRuntimeTarget();
        if (target is null || _profiles is null || _profiles.Count == 0)
        {
            return;
        }
        RuntimeProfile? profile = target.Value.ProfileId is { Length: > 0 } id
            ? _profiles.FirstOrDefault(x => x.Id == id)
            : null;
        profile ??= _profiles[0];
        _selectedProfileId = profile.Id;
        UpdateProfileListSelection();
        ShowProfileDetail(profile);
        if (target.Value.Enhance)
        {
            OpenEnhance(profile);
        }
        else
        {
            ShowDetailPane();
        }
    }


    /// <summary>D113：两个按钮的作用域容易误解，补 tooltip（加路径是全局清单，设默认只改默认方案）。</summary>
    private void InitializeRunnerButtonTips()
    {
        ToolTipService.SetToolTip(Button_AddRunner, Lang.Lunarward_RuntimeEnv_AddPathTip);
        ToolTipService.SetToolTip(Button_SetDefaultRunner, Lang.Lunarward_RuntimeEnv_SetDefaultRunnerTip);
        ToolTipService.SetToolTip(Button_RunnerMore, Lang.Lunarward_RuntimeEnv_SetDefaultRunnerTip);
        ToolTipService.SetToolTip(Button_More, Lang.Lunarward_RuntimeEnv_More);
    }


    private void LoadProfiles()
    {
        // D85：方案页是「左清单 + 右详情」。左列只放名字、状态点与绑定数，
        // 详细内容全部进右栏，避免一屏里既有清单又有 8 行日志。
        _profiles = RuntimeProfileStore.LoadAll(_logger);
        StackPanel_ProfileList.Children.Clear();
        if (_profiles.Count == 0)
        {
            _selectedProfileId = null;
            StackPanel_ProfileList.Children.Add(new TextBlock
            {
                Text = Lang.Lunarward_Profiles_Empty,
                Opacity = 0.7,
                Margin = new Thickness(8, 8, 8, 8),
                TextWrapping = TextWrapping.Wrap,
            });
            ShowProfileDetail(null);
            return;
        }
        if (_profiles.All(x => x.Id != _selectedProfileId))
        {
            _selectedProfileId = _profiles[0].Id;
        }
        foreach (RuntimeProfile profile in _profiles)
        {
            StackPanel_ProfileList.Children.Add(CreateProfileListRow(profile));
        }
        ShowProfileDetail(_profiles.First(x => x.Id == _selectedProfileId));
        UpdateProfileListSelection();
    }




    /// <summary>红黄绿灯：一眼看出这份配方现在能不能启动（D73 §7.1）。</summary>
    private (string Dot, string Text) DescribeHealth(RuntimeProfile profile, PrefixManager.PrefixInspection inspection)
    {
        Component? runner = EnvironmentResolver.ResolveRunner(profile);
        if (runner is null)
        {
            return ("Critical", Lang.Lunarward_RuntimeEnv_NoUsableRunner);
        }
        if (runner.Usable != true)
        {
            return ("Critical", string.Format(Lang.Lunarward_RuntimeEnv_RunnerUnavailable, runner.UsabilityReason));
        }
        if (inspection.Exists && !inspection.HasDriveC)
        {
            return ("Critical", Lang.Lunarward_RuntimeEnv_PrefixNotWine);
        }
        if (inspection.Exists && inspection.Locked)
        {
            return ("Caution", Lang.Lunarward_RuntimeEnv_PrefixLocked);
        }
        if (!inspection.Exists)
        {
            return ("Caution", Lang.Lunarward_RuntimeEnv_PrefixMissing);
        }
        return ("Success", Lang.Lunarward_RuntimeEnv_Usable);
    }


    /// <summary>启动参数与命令包装只在这两栏非空时才显示。</summary>
    private static string DescribeLaunchExtras(RuntimeProfile profile)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(profile.Args))
        {
            parts.Add(string.Format(Lang.Lunarward_RuntimeEnv_LaunchArgsPrefix, profile.Args));
        }
        if (profile.Wrappers.Count > 0)
        {
            parts.Add(string.Format(Lang.Lunarward_RuntimeEnv_WrappersPrefix, string.Join(" ", profile.Wrappers)));
        }
        return string.Join("   ", parts);
    }


    /// <summary>D108：布局标记（bin/system/prefix）翻成人话，别把裸标记直接上屏。</summary>
    private static string DescribeLayout(string? layout) => layout switch
    {
        null or "" => "",
        "bin" => Lang.Lunarward_RuntimeEnv_LayoutBin,
        "system" => Lang.Lunarward_RuntimeEnv_LayoutSystem,
        "prefix" => Lang.Lunarward_RuntimeEnv_LayoutWinePrefix,
        _ => layout,
    };


    /// <summary>D108：键盘快捷键——Ctrl+N 新建方案、Ctrl+S 保存编辑、Delete 删除当前选中的方案（详情页时）。
    /// 注在窗口根元素上：页面本身不可聚焦，按键事件不会落到 Page，只有挂在 XamlRoot.Content 才能收到。</summary>
    private void RegisterShortcuts()
    {
        Loaded -= OnPageLoadedAttachShortcuts;
        Loaded += OnPageLoadedAttachShortcuts;
        if (IsLoaded)
        {
            AttachShortcutsToWindow();
        }
    }


    private void OnPageLoadedAttachShortcuts(object sender, RoutedEventArgs e)
    {
        AttachShortcutsToWindow();
        // 按键事件从「当前有焦点的元素」开始冒泡；页面刚导航进来时焦点可能不在页面里，
        // 所以把焦点放到当前页签按钮上，快捷键才收得到（真机实测：不点任何控件按 Ctrl+N 无反应）。
        FocusCurrentTab();
    }


    private void FocusCurrentTab()
    {
        Button[] tabs = [Tab_Profiles, Tab_Components, Tab_Prefixes];
        int index = Math.Clamp(_currentTab, 0, tabs.Length - 1);
        _ = tabs[index].Focus(FocusState.Programmatic);
    }


    private void AttachShortcutsToWindow()
    {
        if (_shortcutsAttached || XamlRoot?.Content is not UIElement root)
        {
            return;
        }
        _shortcutsAttached = true;
        root.AddHandler(KeyDownEvent, new KeyEventHandler(OnShortcutKeyDown), true);
    }


    /// <summary>R81：页面被 Frame 换掉后 XamlRoot.Content 仍强引用处理器——卸载时解挂，避免旧实例继续收快捷键。</summary>
    protected override void OnUnloaded()
    {
        base.OnUnloaded();
        if (_shortcutsAttached && XamlRoot?.Content is UIElement root)
        {
            root.RemoveHandler(KeyDownEvent, new KeyEventHandler(OnShortcutKeyDown));
        }
        _shortcutsAttached = false;
    }


    private void OnShortcutKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // R81：页面自身的 Visibility 从来不会被赋值（只有子面板会被切），所以改成判断「还在可视树里」。
        if (Visibility != Visibility.Visible || !IsLoaded || XamlRoot is null)
        {
            return;
        }
        bool editorOpen = ShowEditorPaneVisible;
        bool ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
                     & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down;
        if (e.Key == Windows.System.VirtualKey.N && ctrl)
        {
            e.Handled = true;
            Button_New_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Windows.System.VirtualKey.S && ctrl && editorOpen)
        {
            e.Handled = true;
            Button_EditorSave_Click(this, new RoutedEventArgs());
        }
        else if (e.Key == Windows.System.VirtualKey.Delete && !editorOpen && _selectedProfileId is { } id
                 && _profiles.FirstOrDefault(x => x.Id == id) is { } profile)
        {
            e.Handled = true;
            var fake = new Grid { Tag = profile };
            Button_Delete_Click(fake, new RoutedEventArgs());
        }
    }


    /// <summary>D108：体积一律走同一个格式化函数，避免 chip 与卡片出现 370.7 / 370.71 两种写法。</summary>
    private static string FormatSize(long bytes)
        => Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(bytes);


    private void Button_RepairBindings_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string profileId })
        {
            return;
        }
        IReadOnlyList<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll(_logger);
        IReadOnlyList<(GameBiz Biz, string ProfileId)> dangling = ProfileBinding.Dangling(profiles.Select(x => x.Id));
        int failures = 0;
        foreach ((GameBiz biz, _) in dangling)
        {
            if (!ProfileBinding.Set(biz, profileId, _logger))
            {
                failures++;
            }
        }
        LoadProfiles();
        _ = RefreshChipsAsync();
        if (failures > 0)
        {
            Helpers.InAppToast.MainWindow?.Warning((string?)null,
                string.Format(Lang.Lunarward_RuntimeEnv_BindingsSaveFailed, failures), 8000);
            return;
        }
        Helpers.InAppToast.MainWindow?.Success(string.Format(Lang.Lunarward_RuntimeEnv_ReboundGames, dangling.Count), null, 4000);
    }


    private void Button_ClearBindings_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll(_logger);
        IReadOnlyList<(GameBiz Biz, string ProfileId)> dangling = ProfileBinding.Dangling(profiles.Select(x => x.Id));
        int failures = 0;
        foreach ((GameBiz biz, _) in dangling)
        {
            if (!ProfileBinding.Set(biz, null, _logger))
            {
                failures++;
            }
        }
        LoadProfiles();
        _ = RefreshChipsAsync();
        if (failures > 0)
        {
            Helpers.InAppToast.MainWindow?.Warning((string?)null,
                string.Format(Lang.Lunarward_RuntimeEnv_BindingsSaveFailed, failures), 8000);
            return;
        }
        Helpers.InAppToast.MainWindow?.Success(string.Format(Lang.Lunarward_RuntimeEnv_ClearedDangling, dangling.Count), null, 4000);
    }


    /// <summary>当前页签序号（0 概览 / 1 运行器 / 2 组件 / 3 前缀 / 4 增强）。</summary>
    private int _currentTab = -1;

    /// <summary>D108：编辑器打开时的字段快照，用于判断是否有未保存改动。</summary>
    private string _editorSnapshot = string.Empty;


    private async void Tab_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is FrameworkElement element && element.Tag is string tag && int.TryParse(tag, out int index))
            {
                if (ShowEditorPaneVisible)
                {
                    if (!await ConfirmLeaveEditorAsync())
                    {
                        return;
                    }
                    // 放弃修改后必须先把右栏从编辑器切回详情，否则 ShowTab 只切页签可见性，编辑器会留在原地。
                    CloseEditor();
                }
                ShowTab(index);
            }
        }
        catch (Exception ex)
        {
            // R93：`async void` 处理器抛出会直接打到同步上下文（进程崩），统一兜住。
            _logger.LogError(ex, "switch tab failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_Failed);
        }
    }


    /// <summary>
    /// 切换页签。L-153：Uno Skia 6.7 的 PivotHeader 收不到鼠标点击（方向键可以），
    /// 所以页签用普通 Button 自绘，内容用 Visibility 互斥切换。
    /// </summary>
    private void ShowTab(int index)
    {
        // D85：页签只剩四个，「增强」并进方案详情（它是方案级开关，不该要求先在下拉里挑方案）。
        UIElement[] pages = [Page_Profiles, Page_Components, ScrollViewer_Prefixes];
        Button[] tabs = [Tab_Profiles, Tab_Components, Tab_Prefixes];
        index = Math.Clamp(index, 0, pages.Length - 1);
        _currentTab = index;
        for (int i = 0; i < pages.Length; i++)
        {
            pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
            tabs[i].Style = (Style)Resources[i == index ? "TabButtonSelectedStyle" : "TabButtonStyle"];
            // D108：读屏要能听出当前选中哪个页签（自绘 Button 没有 SelectionItem 语义）。
            AutomationProperties.SetItemStatus(tabs[i], i == index ? "Selected" : "");
        }
        // 切到组件页时布局还没跑完，等这一帧结束再按新视口宽度钉列表（否则拿到上一个页签的宽度）
        if (index == 1)
        {
            DispatcherQueue.TryEnqueue(UpdateContentWidth);
        }
        UpdateContentWidth();
        if (!ShowEditorPaneVisible)
        {
            FocusCurrentTab();
        }
    }


    /// <summary>宽度钉死用当前可见页的可视宽度（分页后每个 PivotItem 各有自己的 ScrollViewer）。</summary>
    private double CurrentViewportWidth()
    {
        // 只有组件页自己可见时才有意义：别的页签的视口宽度更窄，钉过去会让图形层列表缩成一窄条（真机实测）。
        if (Page_Components.Visibility == Visibility.Visible)
        {
            if (Page_Components.ViewportWidth > 0)
            {
                return Page_Components.ViewportWidth;
            }
            if (Page_Components.ActualWidth > 0)
            {
                return Page_Components.ActualWidth - 32;
            }
        }
        return 0;
    }


    /// <summary>前缀体积要遍历整个目录，放后台算（D72：UI 线程不做阻塞 IO），算完再回填文案。</summary>
    private async Task FillPrefixSizeAsync(TextBlock target, string prefix)
    {
        try
        {
            PrefixManager.PrefixInspection? computed = await Task.Run(() => PrefixManager.Inspect(prefix));
            if (computed is { Exists: true, SizeBytes: > 0 })
            {
                target.Text = string.Format(Lang.Lunarward_RuntimeEnv_PrefixUsage, FormatSize(computed.SizeBytes));
            }
            else
            {
                target.Text = Lang.Lunarward_RuntimeEnv_PrefixUsageDash;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "compute prefix size failed");
            target.Text = Lang.Lunarward_RuntimeEnv_PrefixUsageDash;
        }
    }


    /// <summary>只读文本视图：显示这份配方的完整 JSON，可复制、可导出（D73 §7.4）。</summary>
    private async void Button_TextView_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: RuntimeProfile profile })
        {
            return;
        }
        string json = System.Text.Json.JsonSerializer.Serialize(profile, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        });
        // 只读展示用 ScrollViewer + TextBlock：不用 TextBox（Uno/Skia 下多行只画出第一行，实测只显示 "{"），
        // 也不指定 FontFamily（"monospace" 在本机不是可用族名，会导致整段文字不渲染）
        var box = new ScrollViewer
        {
            Height = 320,
            MinWidth = 620,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Enabled,
            Content = new TextBlock
            {
                Text = json,
                IsTextSelectionEnabled = true,
                FontSize = 12,
                TextWrapping = TextWrapping.NoWrap,
            },
        };
        var dialog = new ContentDialog
        {
            Title = string.Format(Lang.Lunarward_RuntimeEnv_ConfigTextTitle, profile.Name),
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = string.Format(Lang.Lunarward_RuntimeEnv_ConfigTextFile, Path.Combine(RuntimeProfileStore.EnvironmentsFolder, profile.Id + ".json")),
                        FontSize = 12,
                        Opacity = 0.65,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    box,
                },
            },
            PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Copy,
            SecondaryButtonText = Lang.Lunarward_RuntimeEnv_Export,
            CloseButtonText = Lang.Lunarward_RuntimeEnv_Close,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (_dialogOpen)
        {
            return;
        }
        ContentDialogResult result;
        _dialogOpen = true;
        try
        {
            result = await dialog.ShowAsync();
        }
        finally
        {
            _dialogOpen = false;
        }
        if (result == ContentDialogResult.Primary)
        {
            try
            {
                var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
                package.SetText(json);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
                Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_Copied, 3000);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "copy profile json failed");
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_CopyFailed);
            }
        }
        else if (result == ContentDialogResult.Secondary)
        {
            try
            {
                string? target = await Helpers.FileDialogHelper.OpenSaveFileDialogAsync(XamlRoot, profile.Name + ".json", ("JSON", ".json"));
                if (!string.IsNullOrWhiteSpace(target))
                {
                    await File.WriteAllTextAsync(target!, json);
                    Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_Exported, 3000);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "export profile json failed");
                Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_ExportFailed);
            }
        }
    }


    /// <summary>这个配置最终会用哪些图形组件、现在装没装（M2 组件层的状态展示，D83 两类各说一句）。</summary>
    private static string DescribeGraphics(RuntimeProfile profile)
    {
        if (!profile.HasGraphics)
        {
            return Lang.Lunarward_RuntimeEnv_GraphicsInherit;
        }
        string prefix = profile.PrefixPath;
        bool prefixKnown = !string.IsNullOrWhiteSpace(prefix) && Directory.Exists(prefix);
        List<string> parts = new();
        foreach ((string label, string? id, ComponentKind kind) in new[]
                 {
                     ("DXVK", profile.DxvkId, ComponentKind.Dxvk),
                     ("VKD3D", profile.Vkd3dId, ComponentKind.Vkd3dProton),
                 })
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }
            if (!prefixKnown)
            {
                parts.Add(string.Format(Lang.Lunarward_RuntimeEnv_GraphicsWillInstall, label, id));
                continue;
            }
            try
            {
                InstalledGraphics? installed = GraphicsInstaller.GetInstalled(prefix, kind);
                if (installed is not null && installed.Id == id)
                {
                    parts.Add(string.Format(Lang.Lunarward_RuntimeEnv_GraphicsInstalledAt, label, installed.Name, installed.InstalledAt.ToString("yyyy-MM-dd")));
                }
                else
                {
                    parts.Add(installed is null
                        ? string.Format(Lang.Lunarward_RuntimeEnv_GraphicsNotInstalled, label, id)
                        : string.Format(Lang.Lunarward_RuntimeEnv_GraphicsWillReplace, label, id, installed.Name));
                }
            }
            catch
            {
                parts.Add($"{label}：{id}");
            }
        }
        return string.Join(" · ", parts);
    }


    /// <summary>选一个程序 + 可选参数，在方案环境里跑一次（D64）。</summary>
    private async void Button_RunOnce_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: RuntimeProfile profile })
            {
                return;
            }
            await RunOnceAsync(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "run once failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_Failed);
        }
    }


    /// <summary>D111：「保存并运行」与详情页「运行一次」共用这一条路径。</summary>
    private async Task RunOnceAsync(RuntimeProfile profile)
    {
        try
        {
            string? file = await Helpers.FileDialogHelper.PickSingleFileAsync(XamlRoot);
            if (string.IsNullOrWhiteSpace(file))
            {
                return;
            }
            var argsBox = new TextBox { PlaceholderText = Lang.Lunarward_RuntimeEnv_ArgsPlaceholder, MinWidth = 420 };
            var dialog = new ContentDialog
            {
                Title = Lang.Lunarward_RuntimeEnv_RunOnce,
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = file, FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = string.Format(Lang.Lunarward_RuntimeEnv_EnvironmentLabel, profile.Name), FontSize = 12, Opacity = 0.7 },
                        argsBox,
                    },
                },
                PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Run,
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            var process = Starward.Features.Runtime.RunOnceService.Start(profile, file, argsBox.Text, _logger, AppConfig.CurrentGameBiz.Value);
            if (process is null)
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_Failed);
                return;
            }
            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_StartedPid, process.Id), 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "run once failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_RunFailed);
        }
    }


    private void LoadComponents()
    {
        InitializeComponentFilter();
        // D72：首次扫描要遍历 PATH、所有 Steam 库与 compatibilitytools.d，不能在 UI 线程上做
        _ = LoadComponentsAsync();
    }


    /// <summary>R83：每次加载的代号；并发触发时旧结果直接丢弃，不再出现「重复行 + 闪烁」。</summary>
    private int _componentsLoadGeneration;


    private async Task LoadComponentsAsync()
    {
        int generation = ++_componentsLoadGeneration;
        try
        {
            UpdateSourcesSummary();
        (IReadOnlyList<Component> runners, IReadOnlyList<Component> allGraphics) = await Task.Run(() =>
            (ComponentStore.GetRunners(false, _logger), ComponentStore.GetGraphics(false, _logger)));
        if (generation != _componentsLoadGeneration)
        {
            return; // R83：已经有更新的一次加载在跑，丢掉这轮结果（否则两份列表交替 Clear/Add 会闪）。
        }
        // D85：组件页的筛选只作用于图形层（运行器是另一页的事）
        IReadOnlyList<Component> graphics = _componentFilter switch
        {
            1 => allGraphics.Where(x => x.Kind == ComponentKind.Dxvk).ToList(),
            2 => allGraphics.Where(x => x.Kind == ComponentKind.Vkd3dProton).ToList(),
            _ => allGraphics,
        };

        Text_RunnersCount.Text = string.Format(Lang.Lunarward_RuntimeEnv_SectionCount, runners.Count);
        Text_GraphicsCount.Text = string.Format(Lang.Lunarward_RuntimeEnv_SectionCount, graphics.Count);

        // D63 三段式：运行器进第一段、图形组件留在最后的组件区（P3 再收敛到前缀行）
        StackPanel_Runners.Children.Clear();
        StackPanel_Components.Children.Clear();

        if (runners.Count == 0)
        {
            StackPanel_Runners.Children.Add(new TextBlock
            {
                Text = Lang.Lunarward_RuntimeEnv_WineNotFound,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        else
        {
            // D73 §7.2：受管组件（本程序下载/导入）与外部组件（系统 / Steam / 其它启动器）分两段，
            // 因为前者归我们管、后者只做发现，混在一起会让人以为「删了也会自动补回来」。
            List<Component> managedRunners = runners.Where(x => x.Managed).ToList();
            List<Component> externalRunners = runners.Where(x => !x.Managed).ToList();
            if (managedRunners.Count > 0)
            {
                StackPanel_Runners.Children.Add(GroupTitle(string.Format(Lang.Lunarward_RuntimeEnv_ManagedRunners, managedRunners.Count)));
                foreach (Component runner in managedRunners)
                {
                    StackPanel_Runners.Children.Add(CreateComponentRow(runner, CountProfilesUsingRunner(runner)));
                }
            }
            if (externalRunners.Count > 0)
            {
                StackPanel_Runners.Children.Add(GroupTitle(string.Format(Lang.Lunarward_RuntimeEnv_ExternalRunners, externalRunners.Count)));
                foreach (Component runner in externalRunners)
                {
                    StackPanel_Runners.Children.Add(CreateComponentRow(runner, CountProfilesUsingRunner(runner)));
                }
            }
        }

        if (graphics.Count == 0)
        {
            StackPanel_Components.Children.Add(new TextBlock { Text = Lang.Lunarward_Components_Empty, Opacity = 0.7 });
        }
        else
        {
            // 同内容的多个副本折叠成一行（本机实测 3.1.1 有 3 份），避免看起来像 5 个不同版本
            List<List<Component>> groups = Starward.Features.Runtime.ComponentStore.GroupGraphics(graphics).ToList();
            List<List<Component>> managedGroups = groups.Where(g => g[0].Managed).ToList();
            List<List<Component>> externalGroups = groups.Where(g => !g[0].Managed).ToList();
            if (managedGroups.Count > 0)
            {
                StackPanel_Components.Children.Add(GroupTitle(string.Format(Lang.Lunarward_RuntimeEnv_ManagedGraphicsGroups, managedGroups.Count)));
                foreach (List<Component> group in managedGroups)
                {
                    StackPanel_Components.Children.Add(CreateComponentRow(group[0], 0, group.Count, group));
                }
            }
            if (externalGroups.Count > 0)
            {
                StackPanel_Components.Children.Add(GroupTitle(string.Format(Lang.Lunarward_RuntimeEnv_ExternalGraphicsGroups, externalGroups.Count)));
                foreach (List<Component> group in externalGroups)
                {
                    StackPanel_Components.Children.Add(CreateComponentRow(group[0], 0, group.Count, group));
                }
            }
        }
        // 运行器列表就绪：详情页那个名字要换成带版本的显示名，和编辑器下拉保持一致（D87）
        if (_detailRunnerName is not null && SelectedProfileOrNull() is { } selectedProfile)
        {
            _detailRunnerName.Text = ResolveRunnerDisplay(selectedProfile);
        }
        }
        catch (Exception ex)
        {
            // R83：以前是 `_ = LoadComponentsAsync()` 即发即忘，异常被整个吞掉，页面停在旧内容上。
            _logger.LogError(ex, "load components failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_Failed);
        }
    }


    /// <summary>有几个方案正指着这个运行器（D63：让「这行有没有人在用」可见）。</summary>
    private int CountProfilesUsingRunner(Component runner)
    {
        int count = 0;
        foreach (RuntimeProfile profile in RuntimeProfileStore.LoadAll(_logger))
        {
            string? path = Starward.Features.Runtime.PrefixManager.ExpandPath(profile.RunnerPath);
            if (!string.IsNullOrWhiteSpace(path)
                && string.Equals(Path.GetFullPath(path!), runner.Path, StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }


    /// <summary>前缀段：来源、大小、被几个方案共用、状态（D63）。</summary>
    private void LoadPrefixes()
    {
        _ = LoadPrefixesAsync();
    }


    /// <summary>R20：前缀体积是递归全量 stat，必须在后台算；这是页面上最后一条「扫盘落在 UI 线程」的路径。</summary>
    private async Task LoadPrefixesAsync()
    {
        IReadOnlyList<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll(_logger);
        IReadOnlyList<Starward.Features.Runtime.PrefixManager.PrefixInspection> prefixes = await Task.Run(
            () => Starward.Features.Runtime.PrefixManager.EnumerateKnownPrefixes(profiles)).ConfigureAwait(true);
        StackPanel_Prefixes.Children.Clear();
        if (prefixes.Count == 0)
        {
            StackPanel_Prefixes.Children.Add(new TextBlock
            {
                Text = Lang.Lunarward_RuntimeEnv_NoPrefixes,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        // D85：占用条要有个参照，取最大那份前缀的体积
        long maxBytes = prefixes.Max(x => x.SizeBytes);
        foreach (Starward.Features.Runtime.PrefixManager.PrefixInspection prefix in prefixes)
        {
            StackPanel_Prefixes.Children.Add(CreatePrefixRow(prefix, profiles, maxBytes));
        }
    }


    private UIElement CreatePrefixRow(Starward.Features.Runtime.PrefixManager.PrefixInspection prefix, IReadOnlyList<RuntimeProfile> profiles, long maxBytes = 0)
    {
        // D85：方案可以不写前缀路径（用默认目录），所以要走 EffectivePrefixPath，不然「谁在用」永远算不到它
        int used = profiles.Count(x =>
            string.Equals(EnvironmentResolver.EffectivePrefixPath(x), prefix.Path, StringComparison.Ordinal));

        string state = !prefix.Exists ? Lang.Lunarward_RuntimeEnv_Missing
            : !prefix.HasDriveC ? Lang.Lunarward_RuntimeEnv_NoDriveC
            : prefix.Locked ? Lang.Lunarward_RuntimeEnv_Locked
            : Lang.Lunarward_RuntimeEnv_Healthy;

        string sizeText = prefix.Exists && prefix.SizeBytes > 0
            ? Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(prefix.SizeBytes)
            : "—";

        List<string> owners = profiles
            .Where(x => string.Equals(EnvironmentResolver.EffectivePrefixPath(x), prefix.Path, StringComparison.Ordinal))
            .Select(x => x.Name)
            .ToList();

        // D108：标题说人话——「谁在用 + 目录名」，不再直接拼 environment · hk4e_cn 这类内部标识。
        string titleText = owners.Count switch
        {
            0 when !prefix.Managed => Lang.Lunarward_RuntimeEnv_ExternalPrefix,
            0 => Lang.Lunarward_RuntimeEnv_Unused,
            1 => owners[0],
            _ => $"{owners[0]} {string.Format(Lang.Lunarward_RuntimeEnv_PrefixOwnersSuffix, owners.Count)}",
        };
        var title = new TextBlock
        {
            Text = titleText,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        string infoText = $"{sizeText} · {string.Format(Lang.Lunarward_RuntimeEnv_PrefixUsedBy, used)} · {state}";
        FrameworkElement info;
        if (owners.Count > 0)
        {
            var link = new HyperlinkButton
            {
                Content = new TextBlock { Text = infoText, FontSize = 12, Opacity = 0.6, TextWrapping = TextWrapping.Wrap },
                Padding = new Thickness(0),
                Tag = (prefix, owners),
            };
            link.Click += Button_PrefixUsage_Click;
            info = link;
        }
        else
        {
            info = new TextBlock { Text = infoText, Opacity = 0.6, FontSize = 12, TextWrapping = TextWrapping.Wrap };
        }
        var pathText = new TextBlock
        {
            Text = ShortenPath(prefix.Path, 72),
            Opacity = 0.5,
            FontSize = 12,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap,
        };
        ToolTipService.SetToolTip(pathText, prefix.Path);

        // 前缀里的图形层：状态 + 安装/重装/移除/校验（D63 §5 第 3、4 条）
        Starward.Features.Runtime.GraphicsInstaller.GraphicsStatus status =
            prefix.Exists ? Starward.Features.Runtime.GraphicsInstaller.Verify(prefix.Path) : new();
        string installedId = status.RecordOf(ComponentKind.Dxvk)?.Id ?? "";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var open = new Button { Content = Lang.Lunarward_RuntimeEnv_OpenFolder, Tag = prefix.Path };
        open.Click += (_, _) => Helpers.OpenUrl.Open(prefix.Path);
        buttons.Children.Add(open);

        if (prefix.Exists && !prefix.Locked)
        {
            var graphicsButton = new Button
            {
                Content = status.AnyInstalled ? Lang.Lunarward_RuntimeEnv_Reinstall : Lang.Lunarward_RuntimeEnv_InstallGraphics,
                Tag = (prefix.Path, installedId),
            };
            graphicsButton.Click += Button_InstallGraphicsToPrefix_Click;
            buttons.Children.Add(graphicsButton);

            if (status.AnyInstalled)
            {
                var removeGraphics = new Button { Content = Lang.Lunarward_RuntimeEnv_RemoveGraphics, Tag = prefix.Path };
                removeGraphics.Click += Button_RemoveGraphicsFromPrefix_Click;
                buttons.Children.Add(removeGraphics);

                var verify = new Button { Content = Lang.Lunarward_RuntimeEnv_Verify, Tag = prefix.Path };
                verify.Click += Button_VerifyGraphics_Click;
                buttons.Children.Add(verify);
            }
        }
        if (prefix.Managed && used == 0 && !prefix.Locked)
        {
            var clean = new Button { Content = Lang.Lunarward_RuntimeEnv_Cleanup, Tag = prefix.Path };
            clean.Click += Button_CleanupPrefix_Click;
            buttons.Children.Add(clean);
        }

        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(title);
        content.Children.Add(info);
        content.Children.Add(pathText);

        // D112 后：去掉「占最大前缀」进度条——它没有容量含义，容易被读成磁盘占用。
        if (owners.Count > 1)
        {
            content.Children.Add(new TextBlock
            {
                Text = string.Join("、", owners),
                FontSize = 12,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        else if (prefix.Exists)
        {
            content.Children.Add(new TextBlock
            {
                Text = Lang.Lunarward_RuntimeEnv_Unused,
                FontSize = 12,
                Opacity = 0.6,
            });
        }

        content.Children.Add(new TextBlock
        {
            Text = string.Format(Lang.Lunarward_RuntimeEnv_GraphicsStatus, status.Text),
            FontSize = 12,
            Opacity = status.IsGuardBroken ? 1 : 0.7,
            Foreground = status.IsGuardBroken
                ? (Brush)Application.Current.Resources["SystemFillColorCautionBrush"]
                : null,
            TextWrapping = TextWrapping.Wrap,
        });
        content.Children.Add(buttons);

        return new Border
        {
            Child = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12, 10, 12, 10),
            CornerRadius = new CornerRadius(4),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        };
    }


    /// <summary>手动添加运行器：选一个 wine 可执行文件，探测 `--version` 后记进 manual-runners.json（D63）。</summary>
    private async void Button_AddRunner_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? file = await PickWineExecutableAsync();
            if (string.IsNullOrWhiteSpace(file))
            {
                return;
            }
            if (!File.Exists(file))
            {
                return;
            }
            // R77：`ReadRunnerVersion` 是同步壳（内部 GetResult），wine 挂住时最坏冻 4 秒，挪到线程池。
            string? version = await Task.Run(() => Starward.Features.Runtime.ComponentStore.ReadRunnerVersion(file));
            if (string.IsNullOrWhiteSpace(version))
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_WineVersionFailed);
                return;
            }
            Starward.Features.Runtime.ManualRunners.Add(file);
            Starward.Features.Runtime.ComponentStore.Invalidate();
            LoadComponents();
            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_RunnerAdded, version), 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Add manual runner failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_AddFailed);
        }
    }


    /// <summary>挑一个可执行文件：优先走系统文件对话框，退回 kdialog/zenity。</summary>
    private async Task<string?> PickWineExecutableAsync()
    {
        try
        {
            // 不给扩展名过滤：wine 可执行文件没有后缀
            return await Helpers.FileDialogHelper.PickSingleFileAsync(XamlRoot);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pick executable failed");
            return null;
        }
    }


    /// <summary>把方案选定的图形组件（DXVK 与 VKD3D 各自独立）装进这个前缀。</summary>
    private async void Button_InstallGraphicsToPrefix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ValueTuple<string, string> tag })
        {
            return;
        }
        (string prefixPath, string installedId) = tag;
        try
        {
            IReadOnlyList<Component> graphics = Starward.Features.Runtime.ComponentStore.GetGraphics(false, _logger);
            if (graphics.Count == 0)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, Lang.Lunarward_RuntimeEnv_GraphicsNotFound, 6000);
                return;
            }
            // 优先用这个前缀所属方案的选择；方案没选就退回「前缀里已有的那一类」，最后才用第一个可用组件。
            RuntimeProfile? owner = RuntimeProfileStore.LoadAll(_logger).FirstOrDefault(x => string.Equals(
                Starward.Features.Runtime.PrefixManager.ExpandPath(x.PrefixPath), prefixPath, StringComparison.Ordinal));
            List<Component> chosen = new();
            foreach ((string? wanted, ComponentKind kind) in new[]
                     {
                         (owner?.DxvkId ?? installedId, ComponentKind.Dxvk),
                         (owner?.Vkd3dId ?? "", ComponentKind.Vkd3dProton),
                     })
            {
                Component? component = graphics.FirstOrDefault(x => x.Id == wanted)
                    ?? graphics.FirstOrDefault(x => string.Equals(x.Id, wanted, StringComparison.Ordinal));
                if (component is null && owner is null && kind == ComponentKind.Dxvk)
                {
                    component = graphics.FirstOrDefault(x => x.Kind == kind) ?? graphics[0];
                }
                if (component is not null)
                {
                    chosen.Add(component);
                }
            }
            if (chosen.Count == 0)
            {
                chosen.Add(graphics[0]);
            }
            bool installed = await Task.Run(() =>
                Starward.Features.Runtime.GraphicsInstaller.EnsureInstalled(prefixPath, chosen, _logger));
            LoadPrefixes();
            Helpers.InAppToast.MainWindow?.Success((string?)null,
                installed ? string.Format(Lang.Lunarward_RuntimeEnv_GraphicsInstalledNow, string.Join(" · ", chosen.Select(x => x.Name))) : Lang.Lunarward_RuntimeEnv_GraphicsAlreadyInstalled, 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "install graphics to prefix failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_InstallGraphicsFailed);
        }
    }


    /// <summary>从前缀里移除图形层（按记录删文件）；动别人的前缀要二次确认。</summary>
    private async void Button_RemoveGraphicsFromPrefix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string prefixPath })
        {
            return;
        }
        bool external = !Starward.Features.Runtime.PrefixManager.Inspect(prefixPath, computeSize: false).Managed;
        ContentDialogResult result = await new ContentDialog
        {
            Title = external ? Lang.Lunarward_RuntimeEnv_ConfirmRemoveExternal : Lang.Lunarward_RuntimeEnv_ConfirmRemoveGraphics,
            Content = Lang.Lunarward_RuntimeEnv_GraphicsMoveToPrefixBody + "\n" + prefixPath,
            PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Remove,
            CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        }.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }
        try
        {
            await Task.Run(() => Starward.Features.Runtime.GraphicsInstaller.Remove(prefixPath, kind: null, _logger));
            LoadPrefixes();
            Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_RemovedGraphics, 3000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "remove graphics failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_RemoveFailed);
        }
    }


    /// <summary>核对前缀里的图形层文件，把结论说清楚。</summary>
    private void Button_VerifyGraphics_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string prefixPath })
        {
            return;
        }
        Starward.Features.Runtime.GraphicsInstaller.GraphicsStatus status =
            Starward.Features.Runtime.GraphicsInstaller.Verify(prefixPath);
        Helpers.InAppToast.MainWindow?.Information((string?)null,
            string.Format(Lang.Lunarward_RuntimeEnv_VerifyResult, status.Text, status.TotalFiles - status.MissingFiles, status.TotalFiles), 6000);
    }


    // ── 方案编辑（D63 §4）：整页视图，四段：运行器 / 图形层 / 前缀 / 高级 ──

    private RuntimeProfile? _editingProfile;
    private bool _saveThenRun;

    private TextBox? _editorName;
    private ComboBox? _editorRunner;
    private ComboBox? _editorDxvk;
    private ComboBox? _editorVkd3d;

    private TextBox? _editorPrefixPath;
    private TextBox? _editorEnv;
    private StackPanel? _editorEnvHost;
    private readonly List<(TextBox Key, TextBox Value, Grid Row)> _editorEnvRows = new();
    private StackPanel? _editorWrapperHost;
    private TextBox? _editorWrapperInput;
    private readonly List<string> _editorWrapperList = new();
    private TextBox? _editorArgs;
    private TextBox? _editorWrappers;
    private TextBox? _editorNotes;
    private TextBox? _editorShell;
    private TextBlock? _editorPrefixInfo;
    private TextBlock? _editorPreview;
    private TextBlock? _editorEnvInfo;
    private Button? _editorBrowse;
    private List<Component> _editorRunners = new();
    private List<Component> _editorGraphics = new();


    private void OpenEditor(RuntimeProfile? profile)
    {
        _ = OpenEditorAsync(profile);
    }


    /// <summary>
    /// R78：新建 / 修改编辑器以前在 UI 线程同步 `GetRunners()` + `GetGraphics()`（遍历 PATH、Steam 库、
    /// compatibilitytools.d，冷启动约 0.7 秒且随组件数增长）。现在先在线程池扫描，回到 UI 线程再建控件。
    /// </summary>
    private async Task OpenEditorAsync(RuntimeProfile? profile)
    {
        try
        {
            _editingProfile = profile;
            (_editorRunners, _editorGraphics) = await Task.Run(() =>
                (ComponentStore.GetRunners(false, _logger).ToList(), ComponentStore.GetGraphics(false, _logger).ToList()));
            BuildEditor(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "open profile editor failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_Failed);
        }
    }


    private void BuildEditor(RuntimeProfile? profile)
    {
        StackPanel_ProfileEditor.Children.Clear();

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        header.Children.Add(new TextBlock
        {
            Text = profile is null ? Lang.Lunarward_RuntimeEnv_NewProfile : Lang.Lunarward_RuntimeEnv_EditProfile,
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center,
        });
        StackPanel_ProfileEditor.Children.Add(header);

        // 0) 名称（D111：统一 EditorField 栅格，标签列固定、控件拉伸）
        _editorName = new TextBox
        {
            Text = profile?.Name ?? Lang.Lunarward_RuntimeEnv_NewProfileDefaultName,
        };
        StackPanel_ProfileEditor.Children.Add(Field(Lang.Lunarward_RuntimeEnv_Name, _editorName));

        // 1) 运行器
        _editorRunner = new ComboBox();
        foreach (Component runner in _editorRunners)
        {
            _editorRunner.Items.Add($"{runner.Name} {runner.Version} · {DescribeLayout(runner.LayoutText)}");
        }
        int runnerIndex = _editorRunners.FindIndex(x => MatchesComponent(x, profile?.RunnerPath, profile?.RunnerId));
        _editorRunner.SelectedIndex = runnerIndex >= 0 ? runnerIndex : (_editorRunners.Count > 0 ? 0 : -1);
        StackPanel_ProfileEditor.Children.Add(Field(Lang.Lunarward_RuntimeEnv_Runner, _editorRunner,
            profile is not null && !string.IsNullOrWhiteSpace(profile.RunnerPath) && runnerIndex < 0
                ? string.Format(Lang.Lunarward_RuntimeEnv_RunnerMissing, profile.RunnerPath)
                : null));

        // 2) 图形层（D83：DXVK 与 VKD3D-Proton 是两个独立的层，可以同时选）
        StackPanel_ProfileEditor.Children.Add(SectionTitle(Lang.Lunarward_RuntimeEnv_Graphics));
        _editorDxvk = GraphicsCombo([ComponentKind.Dxvk], profile?.DxvkId);
        _editorVkd3d = GraphicsCombo([ComponentKind.Vkd3dProton], profile?.Vkd3dId);
        StackPanel_ProfileEditor.Children.Add(Field("DXVK", _editorDxvk));
        StackPanel_ProfileEditor.Children.Add(Field("VKD3D-Proton", _editorVkd3d));

        // 3) 前缀（D73 §3.1：只是路径；新建/指向/复制是动作，不是模式）
        _editorPrefixPath = new TextBox
        {
            Text = profile?.PrefixPath ?? "",
            PlaceholderText = Lang.Lunarward_RuntimeEnv_PrefixPlaceholderShort,
        };
        _editorBrowse = new Button { Content = Lang.Lunarward_RuntimeEnv_PickFolder };
        _editorBrowse.Click += Button_EditorBrowsePrefix_Click;
        var copyPrefix = new Button { Content = Lang.Lunarward_RuntimeEnv_ClonePrefix };
        copyPrefix.Click += Button_EditorCopyPrefix_Click;
        // D111：路径框独占整行，按钮另起一行——三个按钮挤在一行会把路径吃掉一半。
        var prefixButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        prefixButtons.Children.Add(_editorBrowse);
        prefixButtons.Children.Add(copyPrefix);
        var pathRow = new StackPanel { Spacing = 4 };
        pathRow.Children.Add(_editorPrefixPath);
        pathRow.Children.Add(prefixButtons);
        StackPanel_ProfileEditor.Children.Add(Field(Lang.Lunarward_RuntimeEnv_Prefix, pathRow));
        _editorPrefixInfo = new TextBlock { FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap };
        StackPanel_ProfileEditor.Children.Add(_editorPrefixInfo);

        // 4) 启动预览：和真启动同一套解析（D73 §4），改哪一项都能立刻看到结果
        StackPanel_ProfileEditor.Children.Add(SectionTitle(Lang.Lunarward_RuntimeEnv_LaunchPreview));
        _editorPreview = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            IsTextSelectionEnabled = true,
            Opacity = 0.9,
        };
        StackPanel_ProfileEditor.Children.Add(_editorPreview);
        _editorEnvInfo = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
        };
        StackPanel_ProfileEditor.Children.Add(_editorEnvInfo);

        // 5) 高级（D110：按语义分组；环境变量改键值表格并校验；包装器改 chips 并检测 PATH）
        var expander = new Expander { Header = Lang.Lunarward_RuntimeEnv_Advanced, IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        var advanced = new StackPanel { Spacing = 6 };

        advanced.Children.Add(SubTitle(Lang.Lunarward_RuntimeEnv_AdvancedLaunch));
        _editorShell = new TextBox { Text = profile?.Shell ?? "", PlaceholderText = "/bin/bash" };
        advanced.Children.Add(Field("Shell", _editorShell, Lang.Lunarward_RuntimeEnv_ShellHint));
        _editorArgs = new TextBox { Text = profile?.Args ?? "", PlaceholderText = "-windowed -nologo" };
        advanced.Children.Add(Field(Lang.Lunarward_RuntimeEnv_AdvancedArgs, _editorArgs, Lang.Lunarward_RuntimeEnv_ArgsHint));

        advanced.Children.Add(SubTitle(Lang.Lunarward_RuntimeEnv_Wrappers));
        _editorWrapperHost = new StackPanel { Spacing = 6 };
        advanced.Children.Add(_editorWrapperHost);
        var wrapperRow = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = GridLength.Auto } },
            ColumnSpacing = 8,
        };
        _editorWrapperInput = new TextBox { PlaceholderText = "gamemoderun" };
        var addWrapper = new Button { Content = Lang.Lunarward_RuntimeEnv_Add, Padding = new Thickness(10, 4, 10, 4) };
        addWrapper.Click += (_, _) =>
        {
            AddWrapperChip(_editorWrapperInput?.Text);
            UpdateEditorPreview();
        };
        _editorWrapperInput.KeyDown += (_, e) =>
        {
            if (e.Key is Windows.System.VirtualKey.Enter)
            {
                AddWrapperChip(_editorWrapperInput?.Text);
                e.Handled = true;
                UpdateEditorPreview();
            }
        };
        wrapperRow.Children.Add(_editorWrapperInput);
        Grid.SetColumn(addWrapper, 1);
        wrapperRow.Children.Add(addWrapper);
        advanced.Children.Add(Field(Lang.Lunarward_RuntimeEnv_Wrappers, wrapperRow, Lang.Lunarward_RuntimeEnv_WrappersHelp));

        advanced.Children.Add(SubTitle(Lang.Lunarward_RuntimeEnv_EnvTitle));
        _editorEnvHost = new StackPanel { Spacing = 6 };
        advanced.Children.Add(_editorEnvHost);
        var envActions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var addEnvRow = new Button { Content = Lang.Lunarward_RuntimeEnv_AddVariable, Padding = new Thickness(10, 4, 10, 4) };
        addEnvRow.Click += (_, _) => AddEnvRow("", "");
        envActions.Children.Add(addEnvRow);
        var envPresets = new ComboBox
        {
            PlaceholderText = Lang.Lunarward_RuntimeEnv_EnvPresets,
            MinWidth = 300,
            VerticalAlignment = VerticalAlignment.Center,
        };
        foreach (string preset in EnvPresets)
        {
            envPresets.Items.Add(preset);
        }
        envPresets.SelectionChanged += (_, _) =>
        {
            if (envPresets.SelectedItem is not string preset)
            {
                return;
            }
            string key = preset.Split('=')[0];
            string value = preset[(preset.IndexOf('=') + 1)..];
            bool exists = _editorEnvRows.Any(r => string.Equals(r.Key.Text.Trim(), key, StringComparison.OrdinalIgnoreCase));
            if (!exists)
            {
                AddEnvRow(key, value);
                UpdateEditorPreview();
            }
            envPresets.SelectedIndex = -1;
        };
        envActions.Children.Add(envPresets);
        advanced.Children.Add(envActions);
        advanced.Children.Add(_editorEnvInfo);
        advanced.Children.Add(Hint(Lang.Lunarward_RuntimeEnv_EnvHint2));

        advanced.Children.Add(SubTitle(Lang.Lunarward_RuntimeEnv_Notes));
        _editorNotes = new TextBox
        {
            Text = profile?.Notes ?? "",
            PlaceholderText = Lang.Lunarward_RuntimeEnv_NotesPlaceholder,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 64,
        };
        advanced.Children.Add(Field(Lang.Lunarward_RuntimeEnv_Notes, _editorNotes));

        expander.Content = advanced;
        StackPanel_ProfileEditor.Children.Add(expander);

        if (profile is not null)
        {
            foreach (KeyValuePair<string, string> pair in profile.Env)
            {
                AddEnvRow(pair.Key, pair.Value);
            }
            foreach (string wrapper in profile.Wrappers)
            {
                AddWrapperChip(wrapper);
            }
        }

        // 保存 / 取消：D108 放到编辑器底栏，680 高窗口下不用滚到底才能保存
        StackPanel_EditorFooter.Children.Clear();
        var save = new Button { Content = Lang.Lunarward_RuntimeEnv_Save, Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        save.Click += Button_EditorSave_Click;
        var saveAndRun = new Button { Content = Lang.Lunarward_RuntimeEnv_SaveAndRun };
        saveAndRun.Click += (_, _) =>
        {
            _saveThenRun = true;
            Button_EditorSave_Click(this, new RoutedEventArgs());
        };
        var cancel = new Button { Content = Lang.Lunarward_RuntimeEnv_Cancel };
        cancel.Click += (_, _) => CloseEditor();
        StackPanel_EditorFooter.Children.Add(save);
        StackPanel_EditorFooter.Children.Add(saveAndRun);
        StackPanel_EditorFooter.Children.Add(cancel);

        if (_editorName is not null)
        {
            // R27：新建方案时名字会决定默认前缀目录，改名要立刻反映到预览与「前缀将建在」那一行。
            _editorName.TextChanged += (_, _) =>
            {
                UpdateEditorPrefixRow();
                UpdateEditorPreview();
            };
        }
        _editorRunner.SelectionChanged += (_, _) => UpdateEditorPreview();
        _editorPrefixPath.TextChanged += (_, _) => UpdateEditorPreview();
        _editorArgs.TextChanged += (_, _) => UpdateEditorPreview();
        if (_editorName is not null)
        {
            // 名字决定默认前缀目录：改名要立刻反映到预览与「前缀将建在」那一行（D108/R27）。
        }

        UpdateEditorPrefixRow();
        UpdateEditorPreview();
        _editorSnapshot = SnapshotEditor();
        ShowEditorPane();
    }


    /// <summary>D108：编辑器快照，用来判断「有没有未保存的改动」。</summary>
    private string SnapshotEditor() => string.Join('\u0001',
        _editorName?.Text,
        _editorRunner?.SelectedIndex,
        _editorDxvk?.SelectedIndex,
        _editorVkd3d?.SelectedIndex,
        _editorPrefixPath?.Text,
        SerializeEnv(),
        _editorArgs?.Text,
        string.Join('\n', _editorWrapperList),
        _editorNotes?.Text,
        _editorShell?.Text);


    private bool EditorDirty => ShowEditorPaneVisible && _editorSnapshot != SnapshotEditor();

    private bool ShowEditorPaneVisible => Grid_ProfileEditor.Visibility == Visibility.Visible;


    /// <summary>D108：离开编辑器前确认，避免切页签/切方案时静默丢草稿。</summary>
    private async Task<bool> ConfirmLeaveEditorAsync()
    {
        if (!EditorDirty)
        {
            return true;
        }
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Lang.Lunarward_RuntimeEnv_DiscardEditTitle,
            Content = Lang.Lunarward_RuntimeEnv_DiscardEditBody,
            PrimaryButtonText = Lang.Lunarward_RuntimeEnv_DiscardEditConfirm,
            CloseButtonText = Lang.Lunarward_RuntimeEnv_DiscardEditKeep,
            DefaultButton = ContentDialogButton.Close,
        };
        ContentDialogResult result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }


    private void CloseEditor()
    {
        _editorSnapshot = string.Empty;
        StackPanel_ProfileEditor.Children.Clear();
        _editingProfile = null;
        ShowDetailPane();
        ShowProfileDetail(SelectedProfileOrNull());
    }


    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        Margin = new Thickness(0, 12, 0, 0),
    };


    private static TextBlock GroupTitle(string text) => new()
    {
        Text = text,
        FontWeight = FontWeights.SemiBold,
        FontSize = 12,
        Margin = new Thickness(0, 10, 0, 2),
        Opacity = 0.8,
        TextWrapping = TextWrapping.Wrap,
    };


    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Opacity = 0.7,
        TextWrapping = TextWrapping.Wrap,
    };


    /// <summary>D111：统一的编辑字段栅格——标签列固定 110px，控件拉伸，提示落在控件列下方。</summary>
    private static Grid Field(string label, FrameworkElement control, string? hint = null)
    {
        var grid = new Grid { ColumnSpacing = 12, RowSpacing = 4 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 13,
            Opacity = 0.8,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
        };
        grid.Children.Add(labelText);

        control.HorizontalAlignment = HorizontalAlignment.Stretch;
        Grid.SetColumn(control, 1);
        grid.Children.Add(control);

        if (!string.IsNullOrWhiteSpace(hint))
        {
            var hintText = new TextBlock
            {
                Text = hint,
                FontSize = 12,
                Opacity = 0.65,
                TextWrapping = TextWrapping.Wrap,
            };
            Grid.SetColumn(hintText, 1);
            Grid.SetRow(hintText, 1);
            grid.Children.Add(hintText);
        }
        return grid;
    }


    private static StackPanel Row(string label, UIElement control)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, MinWidth = 64, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(control);
        return row;
    }


    /// <summary>
    /// 组件的「身份」可能是绝对路径、wine 可执行文件路径或数据目录名，三处来源不一致时也要能对上。
    /// </summary>
    private static bool MatchesComponent(Component component, string? pathOrId, string? id)
    {
        if (string.IsNullOrWhiteSpace(pathOrId) && string.IsNullOrWhiteSpace(id))
        {
            return false;
        }
        if (!string.IsNullOrWhiteSpace(id) && string.Equals(component.Id, id, StringComparison.Ordinal))
        {
            return true;
        }
        string? expanded = string.IsNullOrWhiteSpace(pathOrId)
            ? null
            : Starward.Features.Runtime.PrefixManager.ExpandPath(pathOrId);
        if (expanded is null)
        {
            return false;
        }
        if (string.Equals(component.Id, expanded, StringComparison.Ordinal)
            || string.Equals(component.Path, expanded, StringComparison.Ordinal))
        {
            return true;
        }
        // 运行器的 Id 常是安装目录、Path 是 bin/wine（或反过来）：目录前缀也算命中
        string dir = component.Path.TrimEnd('/');
        if (dir.Length > 0 && expanded.StartsWith(dir + "/", StringComparison.Ordinal))
        {
            return true;
        }
        string tail = Tail(expanded);
        return !string.IsNullOrEmpty(tail)
            && (string.Equals(Tail(component.Id), tail, StringComparison.Ordinal)
                || string.Equals(Tail(component.Path), tail, StringComparison.Ordinal));
    }

    private static string Tail(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }
        string[] parts = path.TrimEnd('/').Split('/');
        int take = Math.Min(2, parts.Length);
        return string.Join('/', parts[^take..]);
    }

    private ComboBox GraphicsCombo(ComponentKind[] kinds, string? selectedId)
    {
        var combo = new ComboBox { MinWidth = 320, HorizontalAlignment = HorizontalAlignment.Left };
        combo.Items.Add(Lang.Lunarward_RuntimeEnv_NoneSelected);
        int index = 0;
        List<Component> items = _editorGraphics.Where(x => kinds.Contains(x.Kind)).ToList();
        for (int i = 0; i < items.Count; i++)
        {
            combo.Items.Add($"{items[i].Name} · {items[i].Version} · {items[i].KindText}");
            if (MatchesComponent(items[i], selectedId, null))
            {
                index = i + 1;
            }
        }
        combo.SelectedIndex = index;
        combo.Tag = items;
        return combo;
    }


    /// <summary>按当前编辑内容算一遍启动命令（与真启动同源），并标出会被配方覆盖的自动环境变量。</summary>
    private void UpdateEditorPreview()
    {
        try
        {
            if (_editorPreview is null || _editorEnvInfo is null)
            {
                return;
            }
            Component? runner = _editorRunner is { SelectedIndex: >= 0 } && _editorRunner.SelectedIndex < _editorRunners.Count
                ? _editorRunners[_editorRunner.SelectedIndex]
                : null;
            var draft = new RuntimeProfile
            {
                Name = string.IsNullOrWhiteSpace(_editorName?.Text) ? Lang.Lunarward_RuntimeEnv_NewProfileDefaultName : _editorName!.Text,
                Args = _editorArgs?.Text ?? "",
                Wrappers = _editorWrapperList.ToList(),
                Env = ReadEnv(out _),
                // R27：增强项不在编辑器里，但属于同一份方案——预览必须带上，否则看不到 gamemode/mangohud。
                Hud = _editingProfile?.Hud ?? "off",
                HudOptions = _editingProfile?.HudOptions ?? "",
                GameMode = _editingProfile?.GameMode ?? false,
                GameScope = _editingProfile?.GameScope ?? false,
                GameScopeArgs = _editingProfile?.GameScopeArgs ?? "",
            };
            string prefix = Starward.Features.Runtime.PrefixManager.ExpandPath(_editorPrefixPath?.Text)
                ?? EnvironmentResolver.EffectivePrefixPath(draft);
            if (runner is null)
            {
                _editorPreview.Text = Lang.Lunarward_RuntimeEnv_PreviewNoRunner;
                _editorEnvInfo.Text = "";
                return;
            }
            var auto = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["WINEPREFIX"] = prefix,
                ["WINEDEBUG"] = "-all",
            };
            LaunchRecipe recipe = LaunchRecipeBuilder.Build(draft, runner.Path, prefix, Lang.Lunarward_RuntimeEnv_PreviewGameExe, null, auto);
            _editorPreview.Text = recipe.CommandLine;
            _editorEnvInfo.Text = recipe.Overrides.Count == 0
                ? ""
                : Lang.Lunarward_RuntimeEnv_OverriddenByRecipe + string.Join("；", recipe.Overrides.Select(x => string.Format(Lang.Lunarward_RuntimeEnv_OverriddenByValue, x.Key, x.AutoValue, x.RecipeValue)));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "update launch preview failed");
            if (_editorPreview is not null)
            {
                _editorPreview.Text = Lang.Lunarward_RuntimeEnv_PreviewFailed;
            }
        }
    }


    private void UpdateEditorPrefixRow()
    {
        if (_editorPrefixInfo is null || _editorPrefixPath is null)
        {
            return;
        }
        string? path = Starward.Features.Runtime.PrefixManager.ExpandPath(_editorPrefixPath.Text);
        if (string.IsNullOrWhiteSpace(path))
        {
            _editorPrefixInfo.Text = Lang.Lunarward_RuntimeEnv_PrefixPlaceholderLong;
            return;
        }
        Starward.Features.Runtime.PrefixManager.PrefixInspection info =
            Starward.Features.Runtime.PrefixManager.Inspect(path, computeSize: false);
        string state = !info.Exists ? Lang.Lunarward_RuntimeEnv_PrefixMissingEditor : !info.HasDriveC ? Lang.Lunarward_RuntimeEnv_NoDriveCEditor : info.Locked ? Lang.Lunarward_RuntimeEnv_Locked : Lang.Lunarward_RuntimeEnv_Healthy;
        _editorPrefixInfo.Text = $"{state} · {path}";
    }


    /// <summary>把一份现有前缀复制到编辑框里的路径（reflink，同卷几乎瞬时）。</summary>
    private async void Button_EditorCopyPrefix_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? source = await Helpers.FileDialogHelper.PickFolderAsync(XamlRoot);
            if (string.IsNullOrWhiteSpace(source))
            {
                return;
            }
            string target = Starward.Features.Runtime.PrefixManager.ExpandPath(_editorPrefixPath?.Text)
                ?? Starward.Features.Runtime.EnvironmentResolver.EffectivePrefixPath(_editingProfile);
            if (_editorPrefixPath is not null && string.IsNullOrWhiteSpace(_editorPrefixPath.Text))
            {
                _editorPrefixPath.Text = target;
            }
            await Task.Run(() => Starward.Features.Runtime.PrefixManager.ClonePrefix(source, target, _logger));
            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_CopiedTo, target), 4000);
            UpdateEditorPrefixRow();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "copy prefix failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_ClonePrefixFailed);
        }
    }


    private async void Button_EditorBrowsePrefix_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? folder = await Helpers.FileDialogHelper.PickFolderAsync(XamlRoot);
            if (!string.IsNullOrWhiteSpace(folder) && _editorPrefixPath is not null)
            {
                _editorPrefixPath.Text = folder;
                UpdateEditorPrefixRow();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "pick prefix folder failed");
        }
    }


    private async void Button_EditorSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_editorRunner is null || _editorRunner.SelectedIndex < 0 || _editorRunner.SelectedIndex >= _editorRunners.Count)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, Lang.Lunarward_RuntimeEnv_SelectRunnerFirst, 4000);
                return;
            }
            Component runner = _editorRunners[_editorRunner.SelectedIndex];
            if (runner.Usable == false)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_RunnerUnusable, runner.UsabilityReason), 6000);
                return;
            }
            string prefixPath = _editorPrefixPath?.Text?.Trim() ?? "";
            string? prefixError = ValidatePrefixPathInput(prefixPath);
            if (prefixError is not null)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, prefixError, 5000);
                return;
            }

            List<RuntimeProfile> all = RuntimeProfileStore.LoadAll(_logger).ToList();
            RuntimeProfile profile = _editingProfile is null
                ? new RuntimeProfile { Id = Guid.NewGuid().ToString("N")[..8] }
                : all.FirstOrDefault(x => x.Id == _editingProfile.Id) ?? _editingProfile;
            profile.Name = string.IsNullOrWhiteSpace(_editorName?.Text) ? (profile.Name.Length > 0 ? profile.Name : Lang.Lunarward_RuntimeEnv_NewProfileDefaultName) : _editorName.Text.Trim();
            profile.RunnerId = runner.Id;
            profile.RunnerPath = runner.Path;
            profile.DxvkId = SelectedGraphicsId(_editorDxvk);
            profile.Vkd3dId = SelectedGraphicsId(_editorVkd3d);
            profile.PrefixPath = string.IsNullOrWhiteSpace(prefixPath) ? null : prefixPath;
            Dictionary<string, string> env = ReadEnv(out string? envError);
            if (envError is not null)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, envError, 6000);
                return;
            }
            profile.Env = env;
            profile.Args = _editorArgs?.Text?.Trim() ?? "";
            profile.Wrappers = _editorWrapperList.ToList();
            profile.Notes = string.IsNullOrWhiteSpace(_editorNotes?.Text) ? null : _editorNotes.Text.Trim();
            profile.Shell = _editorShell?.Text?.Trim() ?? "";

            if (!RuntimeProfileStore.Save(profile, _logger))
            {
                // R07：保存失败不能再弹「已保存」，否则用户以为改好了，重启就没了。
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_SaveFailed);
                return;
            }
            ComponentStore.Invalidate();
            CloseEditor();
            LoadProfiles();
            LoadPrefixes();
            _ = RefreshChipsAsync();
            Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_Saved, 3000);
            if (_saveThenRun)
            {
                _saveThenRun = false;
                await RunOnceAsync(profile);
            }
            // P3：第一次选用这个运行器时顺手自检一次（后台跑，不挡保存；结果只提示一句）
            if (runner.Usable is null)
            {
                AutoCheckRunnerAsync(runner);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "save profile failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_SaveFailed);
        }
    }


    private static List<string> ParseWrappers(string? text)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return list;
        }
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length > 0)
            {
                list.Add(trimmed);
            }
        }
        return list;
    }


    private static Dictionary<string, string> ParseEnv(string? text)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text))
        {
            return result;
        }
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }
            int index = trimmed.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }
            result[trimmed[..index].Trim()] = trimmed[(index + 1)..].Trim();
        }
        return result;
    }


    private static string SelectedGraphicsId(ComboBox? combo)
    {
        if (combo is null || combo.SelectedIndex <= 0 || combo.Tag is not List<Component> items)
        {
            return "";
        }
        int index = combo.SelectedIndex - 1;
        return index < items.Count ? items[index].Id : "";
    }


    /// <summary>清理我们创建、且没有任何方案在用的前缀。</summary>
    private async void Button_CleanupPrefix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path })
        {
            return;
        }
        ContentDialogResult result = await new ContentDialog
        {
            Title = Lang.Lunarward_RuntimeEnv_ConfirmCleanupPrefix,
            Content = string.Format(Lang.Lunarward_RuntimeEnv_DeletePrefixBody, path),
            PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Delete,
            CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        }.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }
        try
        {
            // R82：前缀动辄几百 MB，和同文件 2863 行的删除组件保持同一套「后台删」实现。
            await Task.Run(() => Directory.Delete(path, recursive: true));
            LoadPrefixes();
            Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_CleanedPrefix, 3000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup prefix failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_CleanupFailed);
        }
    }


    /// <summary>一行组件信息的布局信息：原始文案 + 这一行除文字外还要占多少像素。</summary>
    private sealed class ComponentRowInfo
    {
        public ComponentRowInfo(TextBlock name, TextBlock version, string nameRaw, string versionRaw, double reservedWidth)
        {
            Name = name;
            Version = version;
            NameRaw = nameRaw;
            VersionRaw = versionRaw;
            ReservedWidth = reservedWidth;
        }

        public TextBlock Name { get; }

        public TextBlock Version { get; }

        public string NameRaw { get; }

        public string VersionRaw { get; }

        public double ReservedWidth { get; }
    }


    /// <summary>D108：路径按目录段截断（根 + 末尾两段），不要把目录名从中间切碎；完整路径放 ToolTip。</summary>
    private static string ShortenPath(string path, int maxChars)
    {
        if (string.IsNullOrEmpty(path) || path.Length <= maxChars)
        {
            return path;
        }
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
        {
            return path;
        }
        string tail = string.Join('/', parts[^2..]);
        string withHead = $"/{parts[0]}/…/{tail}";
        return withHead.Length <= maxChars ? withHead : $"…/{tail}";
    }


    /// <summary>中间省略：保留头尾、掐掉中间（路径类文本最需要尾部的文件名）。</summary>
    private static string Ellipsize(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars || maxChars < 8)
        {
            return value;
        }
        int head = maxChars / 2;
        int tail = maxChars - head - 1;
        return string.Concat(value.AsSpan(0, head), "…", value.AsSpan(value.Length - tail));
    }


    private void UpdateContentWidth()
    {
        double width = CurrentViewportWidth();
        if (width > 0)
        {
            StackPanel_Components.Width = width;
            StackPanel_Components.HorizontalAlignment = HorizontalAlignment.Left;
        }
        // Uno 的测量链在这里不认「星号列 + TextTrimming」：文字仍会顶出可视区（截图实测）。
        // 因此按可用像素估算可容纳的字符数，直接用「中间省略」的字符串，布局就不再依赖测量行为。
        foreach (UIElement child in StackPanel_Components.Children)
        {
            if (child is Border { Tag: ComponentRowInfo info })
            {
                int chars = (int)Math.Max(24, (width - info.ReservedWidth) / 6.8);
                info.Name.Text = Ellipsize(info.NameRaw, chars);
                info.Version.Text = Ellipsize(info.VersionRaw, chars);
            }
        }
    }


    private UIElement CreateComponentRow(Component component, int usedByProfiles, int groupCount = 1, IReadOnlyList<Component>? group = null)
    {
        // 来源单独一行放在最左侧：塞进路径行会被省略号吃掉（截图实测）
        var kindPanel = new StackPanel { MinWidth = 76, Spacing = 2 };
        kindPanel.Children.Add(new TextBlock
        {
            Text = component.KindText,
            FontSize = 12,
            Opacity = 0.6,
        });
        if (!string.IsNullOrWhiteSpace(component.SourceText))
        {
            kindPanel.Children.Add(new TextBlock
            {
                Text = component.SourceText,
                FontSize = 10,
                Opacity = 0.5,
            });
        }
        var name = new TextBlock
        {
            Text = component.Name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        string shortPath = ShortenPath(component.Path, 52);
        var version = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(component.Version) ? shortPath : $"{component.Version}  ·  {shortPath}",
            FontSize = 12,
            Opacity = 0.6,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        ToolTipService.SetToolTip(version, component.Path);
        var text = new StackPanel { Spacing = 2 };
        text.Children.Add(name);
        text.Children.Add(version);

        // 用 Grid 而不是横向 StackPanel：StackPanel 给子项无限宽度，TextBlock 不会省略，
        // 右边的可用性徽标就会压在路径文字上（实测重叠，见 D58 v2 的截图验证）。
        Grid row = new() { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(kindPanel, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(kindPanel);
        row.Children.Add(text);

        // 右侧：状态徽标 + 「检查」（组件健康，D73 §2.6 的 UI 落点）
        var right = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };

        // D58：扫描阶段就要能看出「能不能用 + 为什么」，别让用户选了才发现跑不起来
        if (component.Kind is ComponentKind.Wine or ComponentKind.Proton)
        {
            var badge = new TextBlock
            {
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Text = component.Usable switch
                {
                    true => Lang.Lunarward_RuntimeEnv_Usable,
                    false => component.UsabilityReason,
                    null => Lang.Lunarward_RuntimeEnv_NotChecked,
                },
                Opacity = component.Usable == true ? 1 : 0.7,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 320,
            };
            if (component.Usable == true)
            {
                badge.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                // D63：一眼看出这行有没有人在用，避免删掉/换掉还在用的运行器
                if (usedByProfiles > 0)
                {
                    badge.Text = string.Format(Lang.Lunarward_RuntimeEnv_BadgeUsedBy, badge.Text, usedByProfiles);
                }
            }
            right.Children.Add(badge);
        }

        if (component.Kind is ComponentKind.Dxvk or ComponentKind.Vkd3dProton)
        {
            if (groupCount > 1)
            {
                var copies = new TextBlock
                {
                    Text = string.Format(Lang.Lunarward_RuntimeEnv_CopiesCount, groupCount),
                    FontSize = 12,
                    Opacity = 0.6,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                right.Children.Add(copies);
            }
            // 图形组件要「装进某个前缀」才有意义：这里直接挑一个前缀目录装进去（D31 的按前缀安装）
            var installButton = new Button
            {
                Content = Lang.Lunarward_RuntimeEnv_InstallToPrefix2,
                Tag = component,
                VerticalAlignment = VerticalAlignment.Center,
            };
            installButton.Click += Button_InstallGraphics_Click;
            Grid.SetColumn(installButton, 3);
            row.Children.Add(installButton);
        }

        // D114：每行一个实心「检查」按钮像一列噪音，降级成低强调文字。
        var healthButton = new Button
        {
            Content = Lang.Lunarward_RuntimeEnv_Check,
            Tag = component,
            VerticalAlignment = VerticalAlignment.Center,
            Background = null,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        };
        ToolTipService.SetToolTip(healthButton, Lang.Lunarward_RuntimeEnv_CheckHint);
        healthButton.Click += Button_ComponentHealth_Click;
        right.Children.Add(healthButton);

        // A6：受管组件才给删除入口；外部组件只是引用（系统目录/Steam/手动路径），按钮不出现
        string? managedRoot = ComponentStore.ManagedRootOf(component);
        if (managedRoot is not null)
        {
            var deleteButton = new Button
            {
                Content = Lang.Lunarward_RuntimeEnv_Delete,
                Tag = component,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ToolTipService.SetToolTip(deleteButton, string.Format(Lang.Lunarward_RuntimeEnv_DeleteComponentTip, managedRoot));
            deleteButton.Click += Button_ComponentDelete_Click;
            right.Children.Add(deleteButton);
        }
        Grid.SetColumn(right, 2);
        row.Children.Add(right);

        return new Border
        {
            Child = row,
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(4),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            // 记录原始文案与固定占位，窗口尺寸变化时据此重算「中间省略」后的字符串（见 UpdateContentWidth）
            Tag = new ComponentRowInfo(name, version, name.Text, version.Text, 72 + 24 + 24 + (row.Children.Count > 2 ? 180 : 0)),
        };
    }


    /// <summary>
    /// 删除受管组件（A6）：先看有没有配方在用（运行器 / 图形层），有就列出来二次确认，
    /// 绝不静默删掉正在用的组件；外部组件连按钮都不出现，这里再挡一次。
    /// </summary>
    private async void Button_ComponentDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Component component })
        {
            return;
        }
        try
        {
            string? root = ComponentStore.ManagedRootOf(component);
            if (root is null)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, Lang.Lunarward_RuntimeEnv_ExternalComponentInfo, 5000);
                return;
            }
            List<string> users = new();
            foreach (RuntimeProfile profile in RuntimeProfileStore.LoadAll(_logger))
            {
                if (string.Equals(profile.RunnerId, component.Id, StringComparison.Ordinal)
                    || string.Equals(profile.DxvkId, component.Id, StringComparison.Ordinal)
                    || string.Equals(profile.Vkd3dId, component.Id, StringComparison.Ordinal)
                    || (!string.IsNullOrWhiteSpace(profile.RunnerPath)
                        && string.Equals(profile.RunnerPath, component.Path, StringComparison.Ordinal)))
                {
                    users.Add(profile.Name);
                }
            }
            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(new TextBlock
            {
                Text = users.Count == 0
                    ? string.Format(Lang.Lunarward_RuntimeEnv_DeleteGraphicsBody, root)
                    : string.Format(Lang.Lunarward_RuntimeEnv_DeleteGraphicsSharedBody, users.Count, root),
                TextWrapping = TextWrapping.Wrap,
            });
            foreach (string name in users)
            {
                body.Children.Add(new TextBlock { Text = "· " + name, FontSize = 12, Opacity = 0.8 });
            }
            ContentDialogResult confirm = await new ContentDialog
            {
                Title = users.Count == 0 ? Lang.Lunarward_RuntimeEnv_ConfirmDeleteComponent : Lang.Lunarward_RuntimeEnv_ConfirmDeleteComponentInUse,
                Content = body,
                PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Delete,
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            }.ShowAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }
            // 删目录可能几百 MB：一律后台跑（L-127）
            (string Message, bool Ok) outcome = await Task.Run(() =>
            {
                bool done = ComponentStore.TryDeleteManaged(component, out string message, _logger);
                return (message, done);
            });
            LoadComponents();
            if (outcome.Ok)
            {
                Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_DeletedComponent, component.Name), 4000);
            }
            else
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_DeleteFailedColon + outcome.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "delete managed component failed: {name}", component.Name);
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_DeleteComponentFailed);
        }
    }


    /// <summary>首次选用组件时的自动自检（P3）：后台跑一次，结果只提示一句，不改任何设置。</summary>
    private async void AutoCheckRunnerAsync(Component runner)
    {
        try
        {
            ComponentHealthResult result = await Starward.Features.Runtime.ComponentHealth
                .CheckAsync(runner, _logger)
                .ConfigureAwait(true);
            Helpers.InAppToast.MainWindow?.Information(
                string.Format(Lang.Lunarward_RuntimeEnv_SelfCheckTitle, runner.Name, result.Summary), null,
                result.Ok ? 5000 : 8000);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "auto health check failed: {name}", runner.Name);
        }
    }


    /// <summary>组件健康（D73 §2.6）：一句概括 + 等宽只读原文，不做发行版包名映射。</summary>
    private async void Button_ComponentHealth_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: Component component })
        {
            return;
        }
        ComponentHealthResult result;
        try
        {
            result = await Starward.Features.Runtime.ComponentHealth
                .CheckAsync(component, _logger)
                .ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "component health check failed: {name}", component.Name);
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_HealthCheckFailed);
            return;
        }

        var summary = new TextBlock
        {
            Text = result.Summary,
            TextWrapping = TextWrapping.Wrap,
        };
        var meta = new TextBlock
        {
            Text = string.Format(Lang.Lunarward_RuntimeEnv_SelfCheckDetail, result.ExitCode, result.Elapsed.TotalMilliseconds.ToString("0"), component.Path),
            FontSize = 12,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
        };
        var output = new ScrollViewer
        {
            Height = 220,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollMode = ScrollMode.Enabled,
            VerticalScrollMode = ScrollMode.Enabled,
            Content = new TextBlock
            {
                // TextBlock 而不是多行 TextBox（L-141：代码 new 出来的多行 TextBox 只画第一行）
                Text = string.IsNullOrWhiteSpace(result.Output) ? Lang.Lunarward_RuntimeEnv_NoOutput : result.Output,
                IsTextSelectionEnabled = true,
                FontSize = 12,
                TextWrapping = TextWrapping.NoWrap,
            },
        };

        var dialog = new ContentDialog
        {
            Title = string.Format(Lang.Lunarward_RuntimeEnv_HealthCheckTitle, component.Name),
            Content = new StackPanel
            {
                Spacing = 8,
                Children = { summary, meta, output },
            },
            PrimaryButtonText = Lang.Lunarward_RuntimeEnv_CopyRaw,
            CloseButtonText = Lang.Lunarward_RuntimeEnv_Close,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (_dialogOpen)
        {
            return;
        }
        ContentDialogResult choice;
        _dialogOpen = true;
        try
        {
            choice = await dialog.ShowAsync();
        }
        finally
        {
            _dialogOpen = false;
        }
        if (choice == ContentDialogResult.Primary)
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(string.IsNullOrWhiteSpace(result.Output) ? result.Summary : result.Output);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        }
    }


    private async void Button_ImportGraphics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? dir = await Helpers.FileDialogHelper.PickFolderAsync(XamlRoot);
            if (string.IsNullOrWhiteSpace(dir))
            {
                return;
            }
            bool isDxvk = File.Exists(Path.Combine(dir, "x64", "d3d11.dll"));
            bool isVkd3d = !isDxvk && File.Exists(Path.Combine(dir, "x64", "d3d12.dll"));
            if (!isDxvk && !isVkd3d)
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_GraphicsDirEmpty);
                return;
            }
            string kind = isDxvk ? "dxvk" : "vkd3d-proton";
            string name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
            string target = Path.Combine(ComponentStore.ComponentsFolder, kind, name);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await Task.Run(() => CopyDirectory(dir, target));
            ComponentStore.Invalidate();
            LoadComponents();
            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_ImportedComponent, kind, name), 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import graphics component failed.");
            Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_ImportGraphicsFailed);
        }
    }


    private static void CopyDirectory(string source, string target)
    {
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file);
            string dest = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            if (!File.Exists(dest))
            {
                File.Copy(file, dest);
            }
        }
    }


    private async void Button_InstallGraphics_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Component component)
        {
            return;
        }
        try
        {
            string? prefix = await Helpers.FileDialogHelper.PickFolderAsync(XamlRoot);
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return;
            }
            if (!Directory.Exists(Path.Combine(prefix, "drive_c")))
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_DirNotWinePrefix);
                return;
            }
            bool ok = await Task.Run(() =>
                Features.Runtime.GraphicsInstaller.EnsureInstalled(prefix, component, _logger));
            if (ok)
            {
                Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_InstalledToPrefix, component.Name), 4000);
            }
            else
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_InstallGraphicsComponentFailed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install graphics component failed.");
            Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_InstallGraphicsComponentFailed);
        }
    }


    private void Button_Refresh_Click(object sender, RoutedEventArgs e)
    {
        ComponentStore.Invalidate();
        LoadProfiles();
        LoadComponents();
        LoadPrefixes();
        _ = RefreshChipsAsync();
    }


    private void Button_OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(RuntimeProfileStore.ProfilesFolder);
            Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { RuntimeProfileStore.ProfilesFolder }, UseShellExecute = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Open profile folder failed.");
            Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_OpenConfigFolderFailed);
        }
    }


    private void Button_OpenShell_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        if (ProfileShellService.TryOpenShell(profile, _logger, AppConfig.CurrentGameBiz.Value))
        {
            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_Shell_Opened, profile.Name), 3000);
        }
        else
        {
            Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_Shell_NoTerminal);
        }
    }


    /// <summary>D114 修订 3：组件页顶部的下载源摘要（启用数 / 总数）。</summary>
    private void UpdateSourcesSummary()
    {
        IReadOnlyList<ComponentSourceEntry> sources = ComponentSources.Load();
        int enabled = sources.Count(x => x.Enabled && !x.Removed);
        Text_SourcesSummary.Text = string.Format(Lang.Lunarward_RuntimeEnv_SourcesSummaryFormat, enabled, sources.Count);
    }


    /// <summary>D114 修订 3：检查全部启用的源是否可达（只发 HEAD，不改配置）。</summary>
    private async void Button_CheckSources_Click(object sender, RoutedEventArgs e)
    {
        Button_CheckSources.IsEnabled = false;
        Text_SourcesSummary.Text = Lang.Lunarward_RuntimeEnv_CheckAllSourcesRunning;
        try
        {
            HttpClient http = Starward.Helpers.HttpClients.Probe; // R85：共享探测客户端，勿 Dispose
            IReadOnlyList<ComponentSourceEntry> sources = ComponentSources.Load()
                .Where(x => x.Enabled && !x.Removed).ToList();
            if (sources.Count == 0)
            {
                Text_SourcesSummary.Text = string.Format(Lang.Lunarward_RuntimeEnv_SourcesSummaryFormat, 0, ComponentSources.Load().Count);
                Helpers.InAppToast.MainWindow?.Warning((string?)null, Lang.Lunarward_RuntimeEnv_NoEnabledSource, 5000);
                return;
            }
            // R85：限并发（4 路），与 ComponentSources.ProbeAllAsync 同一口径。
            ComponentSourceProbe[] results = new ComponentSourceProbe[sources.Count];
            using SemaphoreSlim gate = new(4);
            await Task.WhenAll(sources.Select(async (source, index) =>
            {
                await gate.WaitAsync();
                try
                {
                    results[index] = await ComponentSources.ProbeAsync(http, source);
                }
                finally
                {
                    gate.Release();
                }
            }));
            int ok = results.Count(x => x.Ok);
            string summary = string.Format(Lang.Lunarward_RuntimeEnv_SourcesCheckResult, ok, results.Length);
            Text_SourcesSummary.Text = summary;
            if (ok == results.Length)
            {
                Helpers.InAppToast.MainWindow?.Success((string?)null, summary, 3000);
            }
            else
            {
                string detail = string.Join("; ", results.Where(x => !x.Ok).Select(x => $"{x.DisplayName}: {x.Detail}"));
                Helpers.InAppToast.MainWindow?.Warning((string?)null, $"{summary} ({detail})", 8000);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "check sources failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_CheckAllSourcesFailed);
            UpdateSourcesSummary();
        }
        finally
        {
            Button_CheckSources.IsEnabled = true;
        }
    }


    private void Button_New_Click(object sender, RoutedEventArgs e)
    {
        OpenEditor(null);
    }


    private void Button_Edit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((sender as FrameworkElement)?.Tag is RuntimeProfile profile)
            {
                OpenEditor(profile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "open profile editor failed");
        }
    }


    /// <summary>常用环境变量预设（D）：点了往高级里的环境变量框追加一行。</summary>
    private static readonly string[] EnvPresets =
    [
        "DXVK_HUD=fps,frametimes,gpuload",
        "MANGOHUD=1",
        "WINEDLLOVERRIDES=d3d11,d3d12=n,b",
        "PROTON_ENABLE_NVAPI=1",
        "mesa_glthread=true",
        "WINEESYNC=1",
        "WINEFSYNC=1",
        "DXVK_STATE_CACHE=1",
    ];


    /// <summary>复制一份方案（克隆 / 为某个游戏拆出去都用它）。</summary>
    private static RuntimeProfile CopyProfile(RuntimeProfile source, string name, bool clearPrefix = false)
    {
        return new RuntimeProfile
        {
            Name = name,
            RunnerId = source.RunnerId,
            RunnerPath = source.RunnerPath,
            DxvkId = source.DxvkId,
            Vkd3dId = source.Vkd3dId,
            PrefixPath = clearPrefix ? "" : source.PrefixPath,
            Env = new Dictionary<string, string>(source.Env),
            Args = source.Args,
            Wrappers = new List<string>(source.Wrappers),
            Notes = source.Notes,
            PrefixArch = source.PrefixArch,
            Shell = source.Shell,
            GameOutput = source.GameOutput,
            Hud = source.Hud,
            HudOptions = source.HudOptions,
            GameMode = source.GameMode,
            GameScope = source.GameScope,
            GameScopeArgs = source.GameScopeArgs,
        };
    }


    /// <summary>A：方案里选的图形层与前缀里真正装的东西比对（对不上就提示 + 一键同步）。</summary>
    private (string Text, bool NeedsInstall) DescribeGraphicsSync(RuntimeProfile profile, PrefixManager.PrefixInspection inspection)
    {
        if (!profile.HasGraphics)
        {
            return ("", false);
        }
        if (!inspection.Exists)
        {
            return (Lang.Lunarward_RuntimeEnv_SyncNoPrefix, false);
        }
        GraphicsInstaller.GraphicsStatus status = GraphicsInstaller.Verify(inspection.Path);
        // R79：详情区建行的调用方是 UI 线程——只读缓存，冷缓存时退化成 Id，别在这里触发全盘扫描。
        IReadOnlyList<Component> available = ComponentStore.CachedGraphics ?? Array.Empty<Component>();
        List<string> problems = [];
        foreach ((ComponentKind kind, string? id) in new (ComponentKind, string?)[] { (ComponentKind.Dxvk, profile.DxvkId), (ComponentKind.Vkd3dProton, profile.Vkd3dId) })
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }
            string label = GraphicsInstaller.KindTextOf(kind);
            // 组件不在当前扫描结果里时（被删 / 自定义路径）退化成末段目录名，别把整条路径糊到界面上
            string name = available.FirstOrDefault(x => x.Id == id)?.Name ?? Path.GetFileName(id!.TrimEnd('/'));
            InstalledGraphics? record = status.RecordOf(kind);
            if (record is null)
            {
                problems.Add(string.Format(Lang.Lunarward_RuntimeEnv_PrefixMissingSelected, label, name));
            }
            else if (record.Id != id)
            {
                problems.Add(string.Format(Lang.Lunarward_RuntimeEnv_PrefixDifferentSelected, label, record.Name, name));
            }
            else if (!status.IsHealthy(kind))
            {
                problems.Add(string.Format(Lang.Lunarward_RuntimeEnv_PrefixIncomplete, label, record.Name));
            }
        }
        return problems.Count == 0
            ? ("", false)
            : (string.Format(Lang.Lunarward_RuntimeEnv_SyncMismatch, string.Join("；", problems)), true);
    }


    /// <summary>A：把方案选定的图形层重新装进前缀。</summary>
    private async void Button_SyncGraphics_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        string prefixPath = EnvironmentResolver.EffectivePrefixPath(profile);
        List<Component> components = ComponentStore.GetGraphics()
            .Where(x => x.Id == profile.DxvkId || x.Id == profile.Vkd3dId)
            .ToList();
        if (components.Count == 0)
        {
            Helpers.InAppToast.MainWindow?.Information((string?)null, Lang.Lunarward_RuntimeEnv_GraphicsHint, 5000);
            return;
        }
        if (!Directory.Exists(prefixPath))
        {
            Helpers.InAppToast.MainWindow?.Information((string?)null, Lang.Lunarward_RuntimeEnv_SyncNoPrefix, 4000);
            return;
        }
        try
        {
            await Task.Run(() => GraphicsInstaller.EnsureInstalled(prefixPath, components, _logger));
            LoadProfiles();
            LoadPrefixes();
            await RefreshChipsAsync();
            Helpers.InAppToast.MainWindow?.Success((string?)null,
                string.Format(Lang.Lunarward_RuntimeEnv_SyncedToPrefix, string.Join(" · ", components.Select(x => x.Name))), 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sync graphics to prefix failed");
            Helpers.InAppToast.MainWindow?.Error((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_SyncFailed, ex.Message));
        }
    }


    /// <summary>C：把某个游戏从这个共用方案里拆出去（新方案 + 自己的前缀）。</summary>
    private async void Button_CloneForGame_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        IReadOnlyList<GameBiz> usages = ProfileBinding.Usages(profile.Id);
        if (usages.Count == 0)
        {
            return;
        }
        var combo = new ComboBox
        {
            MinWidth = 280,
            ItemsSource = usages.Select(x => x.ToGameName()).ToList(),
            SelectedIndex = 0,
        };
        var dialog = new ContentDialog
        {
            Title = Lang.Lunarward_RuntimeEnv_CloneForGame,
            Content = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock
                    {
                        Text = string.Format(Lang.Lunarward_RuntimeEnv_CloneBody, profile.Name),
                        TextWrapping = TextWrapping.Wrap,
                    },
                    combo,
                },
            },
            PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Clone,
            CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }
        GameBiz biz = usages[Math.Max(0, combo.SelectedIndex)];
        RuntimeProfile clone = CopyProfile(profile, $"{profile.Name} · {biz.ToGameName()}", clearPrefix: true);
        if (!RuntimeProfileStore.Save(clone, _logger))
        {
            Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_SaveFailed);
            return;
        }
        if (!ProfileBinding.Set(biz, clone.Id, _logger))
        {
            Helpers.InAppToast.MainWindow?.Warning((string?)null,
                "方案已创建，但写入绑定失败（绑定文件可能损坏）；请在游戏设置里重新选择", 8000);
        }
        LoadProfiles();
        await RefreshChipsAsync();
        Helpers.InAppToast.MainWindow?.Success((string?)null,
            string.Format(Lang.Lunarward_RuntimeEnv_CloneDone, biz.ToGameName(), clone.Name), 4000);
    }


    /// <summary>B：清理受管目录里没有任何方案引用的组件。</summary>
    private async void Button_CleanupComponents_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<Component> all = ComponentStore.GetRunners().Concat(ComponentStore.GetGraphics()).ToList();
        IReadOnlyList<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll(_logger);
        List<Component> unused = all
            .Where(c => ComponentStore.ManagedRootOf(c) is not null)
            .Where(c => !profiles.Any(p => p.RunnerId == c.Id || p.DxvkId == c.Id || p.Vkd3dId == c.Id
                || (!string.IsNullOrWhiteSpace(p.RunnerPath) && p.RunnerPath == c.Path)))
            .ToList();
        if (unused.Count == 0)
        {
            Helpers.InAppToast.MainWindow?.Information((string?)null,
                Lang.Lunarward_RuntimeEnv_NothingToClean, 4000);
            return;
        }
        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(new TextBlock
        {
            Text = string.Format(Lang.Lunarward_RuntimeEnv_CleanConfirmHeader, unused.Count),
            TextWrapping = TextWrapping.Wrap,
        });
        foreach (Component component in unused)
        {
            body.Children.Add(new TextBlock
            {
                Text = $"· {component.DisplayName} · {component.KindText}",
                FontSize = 12,
                Opacity = 0.8,
            });
        }
        body.Children.Add(new TextBlock
        {
            Text = Lang.Lunarward_RuntimeEnv_ExternalDirsNotManaged,
            FontSize = 12,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
        });
        ContentDialogResult confirm = await new ContentDialog
        {
            Title = Lang.Lunarward_RuntimeEnv_CleanupUnused,
            Content = body,
            PrimaryButtonText = string.Format(Lang.Lunarward_RuntimeEnv_CleanConfirmPrimary, unused.Count),
            CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        }.ShowAsync();
        if (confirm != ContentDialogResult.Primary)
        {
            return;
        }
        int deleted = 0;
        List<string> failed = [];
        await Task.Run(() =>
        {
            foreach (Component component in unused)
            {
                if (ComponentStore.TryDeleteManaged(component, out _, _logger))
                {
                    deleted++;
                }
                else
                {
                    failed.Add(component.Name);
                }
            }
        });
        ComponentStore.Invalidate();
        LoadComponents();
        await RefreshChipsAsync();
        Helpers.InAppToast.MainWindow?.Success((string?)null,
            failed.Count == 0 ? string.Format(Lang.Lunarward_RuntimeEnv_CleanResult, deleted) : string.Format(Lang.Lunarward_RuntimeEnv_CleanResultFailed, deleted, string.Join("、", failed)), 4000);
    }


    /// <summary>F：页签键盘导航（Left/Right/Home/End）。</summary>
    private void TabStrip_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        int target = e.Key switch
        {
            Windows.System.VirtualKey.Left => _currentTab - 1,
            Windows.System.VirtualKey.Right => _currentTab + 1,
            Windows.System.VirtualKey.Home => 0,
            Windows.System.VirtualKey.End => 3,
            _ => -1,
        };
        if (target < 0)
        {
            return;
        }
        e.Handled = true;
        ShowTab(target);
        Button[] tabs = [Tab_Profiles, Tab_Components, Tab_Prefixes];
        tabs[Math.Clamp(_currentTab, 0, tabs.Length - 1)].Focus(FocusState.Programmatic);
    }


    private void Button_Clone_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        RuntimeProfile clone = CopyProfile(profile, profile.Name + Lang.Lunarward_RuntimeEnv_CopySuffix);
        if (!RuntimeProfileStore.Save(clone, _logger))
        {
            Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_SaveFailed);
            return;
        }
        LoadProfiles();
        Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_Clone_Result, clone.Name), 3000);
    }


    private async void Button_Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        try
        {
            // R71：绑定文件坏了时 Usages 只能返回空，「没人在用」这个结论不成立；先停，别删方案。
            if (!Features.Runtime.ProfileBinding.IsReadable(out string? bindingsError))
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null,
                    $"运行环境绑定文件无法读取（{bindingsError}），暂时不能判断哪些游戏在用这份方案；"
                    + "请先修复或删除 profile-bindings.json 再删方案", 9000);
                return;
            }
            // §4.2：删之前先列清楚哪些游戏在用，并说明绑定会一起清掉。
            // 以前这里直接 File.Delete，留下的悬空绑定只能靠「游戏设置」里的红条兜底（L-62 的反面）。
            IReadOnlyList<GameBiz> usages = Features.Runtime.ProfileBinding.Usages(profile.Id);
            var body = new StackPanel { Spacing = 8 };
            body.Children.Add(new TextBlock
            {
                Text = usages.Count == 0
                    ? string.Format(Lang.Lunarward_RuntimeEnv_DeleteProfileUnused, profile.Name)
                    : string.Format(Lang.Lunarward_RuntimeEnv_DeleteProfileInUse, profile.Name, usages.Count),
                TextWrapping = TextWrapping.Wrap,
            });
            foreach (GameBiz biz in usages)
            {
                body.Children.Add(new TextBlock
                {
                    Text = "· " + biz.ToGameName(),
                    FontSize = 12,
                    Opacity = 0.8,
                });
            }
            string prefixPath = EnvironmentResolver.EffectivePrefixPath(profile);
            bool prefixExists = !string.IsNullOrWhiteSpace(prefixPath) && Directory.Exists(prefixPath);
            if (!string.IsNullOrWhiteSpace(profile.PrefixPath))
            {
                body.Children.Add(new TextBlock
                {
                    Text = Lang.Lunarward_RuntimeEnv_PrefixUntouched + profile.PrefixPath,
                    FontSize = 12,
                    Opacity = 0.6,
                    TextWrapping = TextWrapping.Wrap,
                });
            }
            // H：默认不删；勾上才连带删目录（前缀动辄几百 MB，用户有时就是要清掉）
            var deletePrefixBox = new CheckBox
            {
                Content = string.IsNullOrWhiteSpace(profile.PrefixPath)
                    ? Lang.Lunarward_RuntimeEnv_DeletePrefixToo
                    : $"{Lang.Lunarward_RuntimeEnv_DeletePrefixToo}（{prefixPath}）",
                IsChecked = false,
                IsEnabled = prefixExists,
                Margin = new Thickness(0, 8, 0, 0),
                IsThreeState = false,
            };
            body.Children.Add(deletePrefixBox);
            ContentDialogResult confirm = await new ContentDialog
            {
                Title = Lang.Lunarward_RuntimeEnv_ConfirmDeleteProfile,
                Content = body,
                PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Delete,
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            }.ShowAsync();
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }
            // R09/R10：删除也走 Store（Id 校验 + 统一的错误处理），失败要明确提示。
            if (!RuntimeProfileStore.Delete(profile, _logger))
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_SaveFailed);
                return;
            }
            int cleared = Features.Runtime.ProfileBinding.ClearFor(profile.Id);
            if (cleared < 0)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null,
                    "方案已删除，但绑定文件损坏，无法清理指向它的绑定；下次启动会回落到默认方案", 8000);
            }
            if (deletePrefixBox.IsChecked == true && prefixExists)
            {
                // R22：克隆出来的方案默认共用同一个前缀目录；删之前必须确认没别人在用。
                List<string> sharers = RuntimeProfileStore.LoadAll(_logger)
                    .Where(x => x.Id != profile.Id && SamePrefix(EnvironmentResolver.EffectivePrefixPath(x), prefixPath))
                    .Select(x => x.Name)
                    .ToList();
                if (sharers.Count > 0)
                {
                    Helpers.InAppToast.MainWindow?.Warning((string?)null,
                        string.Format(Lang.Lunarward_RuntimeEnv_PrefixSharedSkip, string.Join("、", sharers)), 6000);
                }
                else
                {
                try
                {
                    await Task.Run(() => Directory.Delete(prefixPath, recursive: true));
                    _logger.LogInformation("Deleted prefix folder: {path}", prefixPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Delete prefix folder failed");
                    Helpers.InAppToast.MainWindow?.Error((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_PrefixCleanFailed, ex.Message));
                }
                }
            }
            LoadProfiles();
            LoadPrefixes();
            _ = RefreshChipsAsync();
            Helpers.InAppToast.MainWindow?.Success((string?)null,
                cleared > 0
                    ? string.Format(Lang.Lunarward_RuntimeEnv_DeletedProfileCleared, string.Format(Lang.Lunarward_Delete_Result, profile.Name), cleared)
                    : string.Format(Lang.Lunarward_Delete_Result, profile.Name), 3000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete profile failed");
            Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_DeleteFailedColon + ex.Message);
        }
    }

    private static TextBlock SubTitle(string text) => new()
    {
        Text = text,
        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        FontSize = 13,
        Margin = new Thickness(0, 10, 0, 0),
    };


    /// <summary>D110：环境变量一行 = 键 + 值 + 删除；键必须是合法环境变量名，重复键会被拒绝保存。</summary>
    private void AddEnvRow(string key, string value)
    {
        if (_editorEnvHost is null)
        {
            return;
        }
        var keyBox = new TextBox { Text = key, PlaceholderText = "KEY", MinWidth = 200 };
        var valueBox = new TextBox { Text = value, PlaceholderText = "VALUE", MinWidth = 300 };
        var remove = new Button { Content = "\u2715", Padding = new Thickness(8, 2, 8, 2) };
        var row = new Grid { ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(keyBox, 0);
        Grid.SetColumn(valueBox, 1);
        Grid.SetColumn(remove, 2);
        row.Children.Add(keyBox);
        row.Children.Add(valueBox);
        row.Children.Add(remove);
        _editorEnvRows.Add((keyBox, valueBox, row));
        _editorEnvHost.Children.Add(row);
        keyBox.TextChanged += (_, _) => UpdateEditorPreview();
        valueBox.TextChanged += (_, _) => UpdateEditorPreview();
        remove.Click += (_, _) =>
        {
            _editorEnvRows.RemoveAll(x => ReferenceEquals(x.Row, row));
            _editorEnvHost.Children.Remove(row);
            UpdateEditorPreview();
        };
    }


    private string SerializeEnv() => string.Join('\n', _editorEnvRows
        .Where(x => !string.IsNullOrWhiteSpace(x.Key.Text))
        .Select(x => $"{x.Key.Text.Trim()}={x.Value.Text}"));


    /// <summary>D110：读环境变量表格并校验键名/重复；错误返回给调用方提示，不静默丢。</summary>
    private Dictionary<string, string> ReadEnv(out string? error)
    {
        Dictionary<string, string> env = new(StringComparer.Ordinal);
        error = null;
        for (int i = 0; i < _editorEnvRows.Count; i++)
        {
            string key = _editorEnvRows[i].Key.Text.Trim();
            if (key.Length == 0)
            {
                continue;
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(key, "^[A-Za-z_][A-Za-z0-9_]*$"))
            {
                error = string.Format(Lang.Lunarward_RuntimeEnv_EnvInvalid, i + 1, key);
                return env;
            }
            if (!env.TryAdd(key, _editorEnvRows[i].Value.Text))
            {
                error = string.Format(Lang.Lunarward_RuntimeEnv_EnvDuplicate, key);
                return env;
            }
        }
        return env;
    }


    private void AddWrapperChip(string? text)
    {
        string value = (text ?? "").Trim();
        if (value.Length == 0 || _editorWrapperList.Any(x => string.Equals(x, value, StringComparison.Ordinal)))
        {
            return;
        }
        _editorWrapperList.Add(value);
        RebuildWrapperChips();
        if (_editorWrapperInput is not null)
        {
            _editorWrapperInput.Text = "";
        }
    }


    /// <summary>D110：包装器用 chips 展示；不在 PATH 里的命令标红，提示先装再存。</summary>
    private void RebuildWrapperChips()
    {
        if (_editorWrapperHost is null)
        {
            return;
        }
        _editorWrapperHost.Children.Clear();
        foreach (string wrapper in _editorWrapperList.ToList())
        {
            string command = wrapper.Split(' ', 2)[0];
            bool found = Enhancements.FindInPath(command) is not null;
            var chipText = new TextBlock
            {
                Text = wrapper,
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                // D108 教训：不再用硬编码 hex；缺失就用 Caution token + 提示行说明。
            };
            var remove = new Button { Content = "\u2715", Padding = new Thickness(6, 0, 6, 0), FontSize = 11 };
            var chipContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            chipContent.Children.Add(chipText);
            chipContent.Children.Add(remove);
            var chip = new Border
            {
                Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 4, 3),
                Child = chipContent,
            };
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            line.Children.Add(chip);
            if (!found)
            {
                line.Children.Add(new TextBlock
                {
                    Text = string.Format(Lang.Lunarward_RuntimeEnv_WrapperMissing, command),
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["SystemFillColorCautionBrush"],
                    VerticalAlignment = VerticalAlignment.Center,
                });
            }
            _editorWrapperHost.Children.Add(line);
            remove.Click += (_, _) =>
            {
                _editorWrapperList.Remove(wrapper);
                RebuildWrapperChips();
                UpdateEditorPreview();
            };
        }
    }


}
