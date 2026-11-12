// Lunarward：设置 → 运行环境（Profile 列表）。D28：Profile 是全局的，条目每游戏独立（后启动列表在游戏设置页）。
// 说明：列表用代码构建而非 DataTemplate —— Uno 的 ListView/ItemsControl 在星号行 + ScrollViewer 里
// 存在测量问题（行高被压成一行、内容被裁），M1 先用确定性代码路径；M2 换回模板并复测（见 12-shims-and-gaps）。
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Text;
using Starward.Features.Runtime;
using Starward.Language;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Linq;


namespace Starward.Features.Setting;


public sealed partial class RuntimeEnvironmentSetting : PageBase
{

    private readonly ILogger<RuntimeEnvironmentSetting> _logger = AppConfig.GetLogger<RuntimeEnvironmentSetting>();


    public RuntimeEnvironmentSetting()
    {
        this.InitializeComponent();
        // Uno 的 ScrollViewer 在「禁用横向滚动」时仍会给内容无限宽度，横向 StackPanel/Grid 的星号列就没有约束，
        // 表现为文字不省略、右侧徽标压到文字上甚至超出窗口（D58 v2 截图实测）。这里显式把列表宽度钉到可视区。
        SizeChanged += (_, _) => UpdateContentWidth();
        LoadProfiles();
        LoadComponents();
        LoadPrefixes();
        // Lunarward：截图/排错用（LUNARWARD_PROFILE_EDITOR=1 打开第一项的编辑对话框），正式版移除
        if (Starward.Features.Dev.DevSwitches.ProfileEditor)
        {
            Loaded += (_, _) =>
            {
                List<RuntimeProfile> all = RuntimeProfileStore.LoadAll(_logger);
                if (all.Count > 0)
                {
                    _ = ShowProfileEditorAsync(all[0]);
                }
            };
        }
    }


    private void LoadProfiles()
    {
        StackPanel_Profiles.Children.Clear();
        IReadOnlyList<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll(_logger);
        if (profiles.Count == 0)
        {
            StackPanel_Profiles.Children.Add(new TextBlock { Text = Lang.Lunarward_Profiles_Empty, Opacity = 0.7 });
            return;
        }
        foreach (RuntimeProfile profile in profiles)
        {
            StackPanel_Profiles.Children.Add(CreateProfileRow(profile));
        }
        StackPanel_Profiles.Children.Add(new TextBlock
        {
            Text = "组件只做「发现 + 选择」，不安装也不修改系统里的 wine / DXVK；换运行器后现有前缀会在下次启动时走一次 wine 更新。「伴随启动」在游戏设置页。",
            Opacity = 0.6,
            FontSize = 12,
            Margin = new Thickness(12, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
    }




    /// <summary>这个配置最终会用哪个图形组件、现在装没装（M2 组件层的状态展示）。</summary>
    private static string DescribeGraphics(RuntimeProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.GraphicsId))
        {
            return "图形组件：沿用前缀里已有的";
        }
        string prefix = profile.PrefixPath;
        bool prefixKnown = !string.IsNullOrWhiteSpace(prefix)
            && profile.PrefixMode is "external" or "dedicated" or "shared"
            && Directory.Exists(prefix);
        if (!prefixKnown)
        {
            return $"图形组件：{profile.GraphicsId}（启动前安装到前缀）";
        }
        try
        {
            InstalledGraphics? installed = GraphicsInstaller.GetInstalled(prefix);
            if (installed is not null && installed.Id == profile.GraphicsId)
            {
                return $"图形组件：{installed.Name}（已安装，{installed.InstalledAt:yyyy-MM-dd}）";
            }
            return installed is null
                ? $"图形组件：{profile.GraphicsId}（前缀中尚未安装，启动前会装上）"
                : $"图形组件：{profile.GraphicsId}（前缀里现在是 {installed.Name}，启动前会替换）";
        }
        catch
        {
            return $"图形组件：{profile.GraphicsId}";
        }
    }


    private UIElement CreateProfileRow(RuntimeProfile profile)
    {
        var name = new TextBlock
        {
            Text = profile.Name,
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        var summary = new TextBlock
        {
            Text = profile.Summary,
            Opacity = 0.65,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        var graphicsText = new TextBlock
        {
            Text = DescribeGraphics(profile),
            Opacity = 0.6,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };

        var shellButton = new Button
        {
            Content = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new FontIcon { Glyph = "\uE756", FontSize = 16 },
                    new TextBlock { Text = Lang.Lunarward_RuntimeEnv_OpenShell },
                },
            },
            Tag = profile,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        ToolTipService.SetToolTip(shellButton, Lang.Lunarward_RuntimeEnv_OpenShell);
        shellButton.Click += Button_OpenShell_Click;

        // 单列纵向排布：Uno 下多列 Grid 在窄容器里容易把右侧按钮挤掉（L-34）
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(shellButton);

        var editButton = new Button { Content = Lang.Common_Edit, Tag = profile };
        editButton.Click += Button_Edit_Click;
        var cloneButton = new Button { Content = Lang.Lunarward_RuntimeEnv_Clone, Tag = profile };
        cloneButton.Click += Button_Clone_Click;
        var deleteButton = new Button { Content = Lang.Common_Delete, Tag = profile };
        deleteButton.Click += Button_Delete_Click;
        buttons.Children.Add(editButton);
        buttons.Children.Add(cloneButton);
        buttons.Children.Add(deleteButton);

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(name);
        content.Children.Add(summary);
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


    private void LoadComponents()
    {
        // D63 三段式：运行器进第一段、图形组件留在最后的组件区（P3 再收敛到前缀行）
        StackPanel_Runners.Children.Clear();
        StackPanel_Components.Children.Clear();
        IReadOnlyList<Component> runners = ComponentStore.GetRunners(false, _logger);
        IReadOnlyList<Component> graphics = ComponentStore.GetGraphics(false, _logger);

        if (runners.Count == 0)
        {
            StackPanel_Runners.Children.Add(new TextBlock
            {
                Text = "没有找到 wine。可以点「添加路径…」指一个 wine 可执行文件。",
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        else
        {
            foreach (Component runner in runners)
            {
                StackPanel_Runners.Children.Add(CreateComponentRow(runner, CountProfilesUsingRunner(runner)));
            }
        }

        if (graphics.Count == 0)
        {
            StackPanel_Components.Children.Add(new TextBlock { Text = Lang.Lunarward_Components_Empty, Opacity = 0.7 });
        }
        else
        {
            foreach (Component graphic in graphics)
            {
                StackPanel_Components.Children.Add(CreateComponentRow(graphic, 0));
            }
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
        StackPanel_Prefixes.Children.Clear();
        IReadOnlyList<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll(_logger);
        IReadOnlyList<Starward.Features.Runtime.PrefixManager.PrefixInspection> prefixes =
            Starward.Features.Runtime.PrefixManager.EnumerateKnownPrefixes(profiles);
        if (prefixes.Count == 0)
        {
            StackPanel_Prefixes.Children.Add(new TextBlock
            {
                Text = "还没有任何前缀。新建方案时创建，或在方案里指向已有目录。",
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        foreach (Starward.Features.Runtime.PrefixManager.PrefixInspection prefix in prefixes)
        {
            StackPanel_Prefixes.Children.Add(CreatePrefixRow(prefix, profiles));
        }
    }


    private UIElement CreatePrefixRow(Starward.Features.Runtime.PrefixManager.PrefixInspection prefix, IReadOnlyList<RuntimeProfile> profiles)
    {
        int used = profiles.Count(x =>
            string.Equals(Starward.Features.Runtime.PrefixManager.ExpandPath(x.PrefixPath), prefix.Path, StringComparison.Ordinal));

        string state = !prefix.Exists ? "不存在"
            : !prefix.HasDriveC ? "缺少 drive_c"
            : prefix.Locked ? "正被占用"
            : "健康";

        string sizeText = prefix.Exists && prefix.SizeBytes > 0
            ? Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(prefix.SizeBytes)
            : "—";

        var title = new TextBlock
        {
            Text = (prefix.Mode, prefix.GameKey, prefix.Managed) switch
            {
                (null, null, false) => "外部前缀",
                (null, null, true) => Path.GetFileName(prefix.Path),
                (null, _, _) => prefix.GameKey!,
                (_, null, _) => $"{prefix.Mode} · {Path.GetFileName(prefix.Path)}",
                _ => $"{prefix.Mode} · {prefix.GameKey}",
            },
            FontWeight = FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap,
        };
        var info = new TextBlock
        {
            Text = $"{sizeText} · {used} 个方案使用 · {state}",
            Opacity = 0.6,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        };
        var pathText = new TextBlock
        {
            Text = Ellipsize(prefix.Path, 90),
            Opacity = 0.5,
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var open = new Button { Content = "打开目录", Tag = prefix.Path };
        open.Click += (_, _) => Helpers.OpenUrl.Open(prefix.Path);
        buttons.Children.Add(open);
        if (prefix.Managed && used == 0 && !prefix.Locked)
        {
            var clean = new Button { Content = "清理", Tag = prefix.Path };
            clean.Click += Button_CleanupPrefix_Click;
            buttons.Children.Add(clean);
        }

        var content = new StackPanel { Spacing = 6 };
        content.Children.Add(title);
        content.Children.Add(info);
        content.Children.Add(pathText);
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
            string? version = Starward.Features.Runtime.ComponentStore.ReadRunnerVersion(file);
            if (string.IsNullOrWhiteSpace(version))
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, "这个文件跑不出 `wine --version`，可能不是 wine 或缺少依赖库。");
                return;
            }
            Starward.Features.Runtime.ManualRunners.Add(file);
            Starward.Features.Runtime.ComponentStore.Invalidate();
            LoadComponents();
            Helpers.InAppToast.MainWindow?.Success((string?)null, $"已添加：{version}", 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Add manual runner failed");
            Helpers.InAppToast.MainWindow?.Error(ex, "添加失败");
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


    /// <summary>清理我们创建、且没有任何方案在用的前缀。</summary>
    private async void Button_CleanupPrefix_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string path })
        {
            return;
        }
        ContentDialogResult result = await new ContentDialog
        {
            Title = "清理这个前缀？",
            Content = "会删除目录及其内容，不可恢复：\n" + path + "\n\n如果是你还想保留的 wine 环境，请点「取消」。",
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        }.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }
        try
        {
            Directory.Delete(path, recursive: true);
            LoadPrefixes();
            Helpers.InAppToast.MainWindow?.Success((string?)null, "已清理前缀", 3000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cleanup prefix failed");
            Helpers.InAppToast.MainWindow?.Error(ex, "清理失败");
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
        double width = ScrollViewer_Content.ViewportWidth > 0 ? ScrollViewer_Content.ViewportWidth : RootGrid.ActualWidth - 64;
        if (width > 0)
        {
            StackPanel_Components.Width = width;
            StackPanel_Profiles.Width = width;
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


    private UIElement CreateComponentRow(Component component, int usedByProfiles)
    {
        var kind = new TextBlock
        {
            Text = component.KindText,
            FontSize = 12,
            Opacity = 0.6,
            MinWidth = 72,
        };
        var name = new TextBlock
        {
            Text = component.Name,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var version = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(component.Version) ? component.Path : $"{component.Version}  ·  {component.Path}",
            FontSize = 12,
            Opacity = 0.6,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
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
        Grid.SetColumn(kind, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(kind);
        row.Children.Add(text);

        // D58：扫描阶段就要能看出「能不能用 + 为什么」，别让用户选了才发现跑不起来
        if (component.Kind is ComponentKind.Wine or ComponentKind.Proton)
        {
            var badge = new TextBlock
            {
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Text = component.Usable switch
                {
                    true => "可用",
                    false => component.UsabilityReason,
                    null => "未检测",
                },
                Opacity = component.Usable == true ? 1 : 0.7,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 320,
            };
            if (component.Usable == true)
            {
                badge.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                if (!string.IsNullOrWhiteSpace(component.LayoutText))
                {
                    badge.Text = $"可用 · {component.LayoutText}";
                }
                // D63：一眼看出这行有没有人在用，避免删掉/换掉还在用的运行器
                if (usedByProfiles > 0)
                {
                    badge.Text = $"{badge.Text} · {usedByProfiles} 个方案在用";
                }
            }
            else if (!string.IsNullOrWhiteSpace(component.LayoutText))
            {
                badge.Text = $"{badge.Text}（{component.LayoutText}）";
            }
            Grid.SetColumn(badge, 2);
            row.Children.Add(badge);
        }

        if (component.Kind is ComponentKind.Dxvk or ComponentKind.Vkd3dProton)
        {
            // 图形组件要「装进某个前缀」才有意义：这里直接挑一个前缀目录装进去（D31 的按前缀安装）
            var installButton = new Button
            {
                Content = "安装到前缀…",
                Tag = component,
                VerticalAlignment = VerticalAlignment.Center,
            };
            installButton.Click += Button_InstallGraphics_Click;
            Grid.SetColumn(installButton, 3);
            row.Children.Add(installButton);
        }

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
                Helpers.InAppToast.MainWindow?.Error((string?)null, "这个目录里没有 x64/d3d11.dll（DXVK）或 x64/d3d12.dll（VKD3D）");
                return;
            }
            string kind = isDxvk ? "dxvk" : "vkd3d-proton";
            string name = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar));
            string target = Path.Combine(ComponentStore.ComponentsFolder, kind, name);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            CopyDirectory(dir, target);
            ComponentStore.Invalidate();
            LoadComponents();
            Helpers.InAppToast.MainWindow?.Success((string?)null, $"已导入 {kind}「{name}」", 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import graphics component failed.");
            Helpers.InAppToast.MainWindow?.Error((string?)null, "导入图形组件失败，详见日志");
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
                Helpers.InAppToast.MainWindow?.Error((string?)null, "这个目录不像 Wine 前缀（缺少 drive_c）");
                return;
            }
            bool ok = Features.Runtime.GraphicsInstaller.EnsureInstalled(prefix, component, _logger);
            if (ok)
            {
                Helpers.InAppToast.MainWindow?.Success((string?)null, $"已把「{component.Name}」安装到该前缀", 4000);
            }
            else
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, "安装图形组件失败，详见日志");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install graphics component failed.");
            Helpers.InAppToast.MainWindow?.Error((string?)null, "安装图形组件失败，详见日志");
        }
    }


    private void Button_Refresh_Click(object sender, RoutedEventArgs e)
    {
        ComponentStore.Invalidate();
        LoadProfiles();
        LoadComponents();
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
            Helpers.InAppToast.MainWindow?.Error((string?)null, "无法打开配置文件夹");
        }
    }


    private void Button_OpenShell_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        if (ProfileShellService.TryOpenShell(profile, _logger))
        {
            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_Shell_Opened, profile.Name), 3000);
        }
        else
        {
            Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_Shell_NoTerminal);
        }
    }


    private void Button_New_Click(object sender, RoutedEventArgs e)
    {
        _ = ShowProfileEditorAsync(new RuntimeProfile { Name = "新配置" }, isNew: true);
    }


    private void Button_Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is RuntimeProfile profile)
        {
            _ = ShowProfileEditorAsync(profile);
        }
    }


    private void Button_Clone_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        RuntimeProfile clone = new()
        {
            Name = profile.Name + " 副本",
            RunnerType = profile.RunnerType,
            RunnerPath = profile.RunnerPath,
            PrefixMode = profile.PrefixMode,
            PrefixPath = profile.PrefixPath,
            Environment = new Dictionary<string, string>(profile.Environment),
            Shell = profile.Shell,
        };
        RuntimeProfileStore.Save(clone, _logger);
        LoadProfiles();
        Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_Clone_Result, clone.Name), 3000);
    }


    private void Button_Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        try
        {
            string file = Path.Combine(RuntimeProfileStore.ProfilesFolder, profile.Id + ".json");
            if (File.Exists(file))
            {
                File.Delete(file);
            }
            LoadProfiles();
            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_Delete_Result, profile.Name), 3000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete profile failed");
            Helpers.InAppToast.MainWindow?.Error((string?)null, "删除失败：" + ex.Message);
        }
    }


    private async Task ShowProfileEditorAsync(RuntimeProfile profile, bool isNew = false)
    {
        TextBox nameBox = new() { Header = "名称", Text = profile.Name };

        ComboBox runnerTypeBox = new() { Header = "运行器类型" };
        foreach (string type in new[] { "wine", "proton", "umu", "custom" })
        {
            runnerTypeBox.Items.Add(type);
        }
        runnerTypeBox.SelectedIndex = Math.Max(0, runnerTypeBox.Items.IndexOf(profile.RunnerType));

        // 已检测到的运行器：选中即回填路径与类型（仍然保留手填入口）
        var detectedRunners = ComponentStore.GetRunners(false, _logger);
        ComboBox runnerPickBox = new() { Header = Lang.Lunarward_Editor_UseDetectedRunner };
        runnerPickBox.Items.Add("（下面手动填写）");
        foreach (var runner in detectedRunners)
        {
            runnerPickBox.Items.Add(string.IsNullOrWhiteSpace(runner.Version) ? runner.Name : $"{runner.Name}  ({runner.Version})");
        }
        runnerPickBox.SelectedIndex = 0;

        TextBox runnerPathBox = new() { Header = "运行器路径（wine/proton 的 wine 可执行文件）", Text = profile.RunnerPath ?? "" };
        runnerPickBox.SelectionChanged += (_, _) =>
        {
            int index = runnerPickBox.SelectedIndex - 1;
            if (index < 0 || index >= detectedRunners.Count)
            {
                return;
            }
            var runner = detectedRunners[index];
            runnerPathBox.Text = runner.Path;
            string type = runner.Kind == ComponentKind.Proton ? "proton" : "wine";
            if (runnerTypeBox.Items.Contains(type))
            {
                runnerTypeBox.SelectedItem = type;
            }
        };
        int currentRunnerIndex = detectedRunners.ToList().FindIndex(x => string.Equals(x.Path, profile.RunnerPath, StringComparison.Ordinal));
        if (currentRunnerIndex >= 0)
        {
            runnerPickBox.SelectedIndex = currentRunnerIndex + 1;
        }
        Button browseRunner = new() { Content = "浏览…", VerticalAlignment = VerticalAlignment.Bottom, MinWidth = 84 };
        browseRunner.Click += async (_, _) =>
        {
            string? file = await Helpers.FileDialogHelper.PickSingleFileAsync(XamlRoot, (Lang.Lunarward_Filter_AllFiles, "*"));
            if (!string.IsNullOrWhiteSpace(file))
            {
                runnerPathBox.Text = file;
            }
        };
        Grid runnerGrid = new() { ColumnSpacing = 8 };
        runnerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        runnerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(browseRunner, 1);
        runnerGrid.Children.Add(runnerPathBox);
        runnerGrid.Children.Add(browseRunner);
        StackPanel runnerPanel = new() { Spacing = 8 };
        runnerPanel.Children.Add(runnerPickBox);
        runnerPanel.Children.Add(runnerGrid);
        runnerPanel.Children.Add(new TextBlock
        {
            Text = "换运行器后，现有前缀会在下次启动时走一次 wine 更新（wineboot -u），里面的东西一般会保留。",
            FontSize = 12,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
        });
        // D58：给一条退路——上次成功启动用的组合，出问题一键切回
        Features.Runtime.LastGoodEntry? lastGood = isNew ? null : Features.Runtime.LastKnownGoodStore.Get(profile.Id);
        if (lastGood is not null && !string.IsNullOrWhiteSpace(lastGood.RunnerPath))
        {
            var lastGoodRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            lastGoodRow.Children.Add(new TextBlock
            {
                Text = "上次可用：" + Features.Runtime.LastKnownGoodStore.Describe(lastGood),
                FontSize = 12,
                Opacity = 0.7,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            });
            var useLastGood = new Button
            {
                Content = "切回",
                MinWidth = 72,
                IsEnabled = !string.Equals(lastGood.RunnerPath, profile.RunnerPath, StringComparison.Ordinal),
            };
            ToolTipService.SetToolTip(useLastGood, "把运行器路径改回上次成功启动时用的那个");
            useLastGood.Click += (_, _) => runnerPathBox.Text = lastGood.RunnerPath!;
            lastGoodRow.Children.Add(useLastGood);
            runnerPanel.Children.Add(lastGoodRow);
        }

        // D58 v3：对用户只说「前缀从哪来」，不再暴露 template/shared 这些内部名字。
        // shared 只对旧配置保留，新建时不再提供（共用通过「多个游戏绑定同一个方案」表达）。
        ComboBox prefixModeBox = new() { Header = "前缀来源" };
        var modeEntries = new List<(string Mode, string Text)>
        {
            ("dedicated", "新建一个专属前缀（默认）"),
            ("template", "复制自现有前缀"),
            ("external", "使用已有目录（就地使用，不复制）"),
            ("ephemeral", "临时前缀（退出后丢弃）"),
        };
        if (string.Equals(profile.PrefixMode, "shared", StringComparison.OrdinalIgnoreCase))
        {
            modeEntries.Add(("shared", "共享前缀（旧配置）"));
        }
        string[] modes = modeEntries.Select(x => x.Mode).ToArray();
        for (int i = 0; i < modeEntries.Count; i++)
        {
            prefixModeBox.Items.Add(modeEntries[i].Text);
            if (string.Equals(modeEntries[i].Mode, profile.PrefixMode, StringComparison.OrdinalIgnoreCase))
            {
                prefixModeBox.SelectedIndex = i;
            }
        }
        if (prefixModeBox.SelectedIndex < 0)
        {
            prefixModeBox.SelectedIndex = 0;
        }

        TextBox prefixPathBox = new() { Header = "前缀路径", Text = profile.PrefixPath ?? "" };
        Button browsePrefix = new() { Content = "选择目录…", VerticalAlignment = VerticalAlignment.Bottom, MinWidth = 104 };
        browsePrefix.Click += async (_, _) =>
        {
            string? dir = await Helpers.FileDialogHelper.PickFolderAsync(XamlRoot);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                prefixPathBox.Text = dir;
            }
        };
        Grid prefixGrid = new() { ColumnSpacing = 8 };
        prefixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        prefixGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(browsePrefix, 1);
        prefixGrid.Children.Add(prefixPathBox);
        prefixGrid.Children.Add(browsePrefix);

        // 从现有前缀复制一份（reflink 克隆），用于「复制自现有前缀」这个来源
        Button makeTemplate = new() { Content = "从现有前缀复制…", HorizontalAlignment = HorizontalAlignment.Left };
        ToolTipService.SetToolTip(makeTemplate, "选一个已有的 Wine 前缀目录，reflink 复制到一个新目录并填入上面的路径；之后这个方案就从它起步");
        makeTemplate.Click += async (_, _) =>
        {
            string? source = await Helpers.FileDialogHelper.PickFolderAsync(XamlRoot);
            if (string.IsNullOrWhiteSpace(source))
            {
                return;
            }
            if (!Directory.Exists(Path.Combine(source, "drive_c")))
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, "这个目录不像 Wine 前缀（缺少 drive_c）");
                return;
            }
            try
            {
                string name = $"{Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar))}-{DateTime.Now:MMddHHmm}";
                string destination = Path.Combine(RuntimeProfileStore.TemplatesFolder, name);
                PrefixManager.ClonePrefix(source, destination, _logger);
                prefixPathBox.Text = destination;
                prefixModeBox.SelectedIndex = Array.IndexOf(modes, "template");
                Helpers.InAppToast.MainWindow?.Success((string?)null, $"已复制到：{destination}", 6000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create prefix template failed: {source}", source);
                Helpers.InAppToast.MainWindow?.Error((string?)null, "复制前缀失败，详见日志");
            }
        };

        TextBox shellBox = new() { Header = "调试 Shell（留空跟随 $SHELL）", Text = profile.Shell, PlaceholderText = "/bin/fish" };

        TextBox envBox = new()
        {
            Header = "额外环境变量（每行 KEY=VALUE）",
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
            Height = 96,
            Text = string.Join("\n", profile.Environment.Select(x => $"{x.Key}={x.Value}")),
        };

        // 图形组件：DXVK / VKD3D（来自 ComponentStore 的扫描结果；空=沿用前缀里已有）
        IReadOnlyList<Component> graphicsComponents = ComponentStore.GetGraphics(false, _logger);
        ComboBox graphicsBox = new()
        {
            Header = "图形组件（DXVK / VKD3D）",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            PlaceholderText = Lang.Lunarward_Editor_KeepPrefixGraphics,
        };
        graphicsBox.Items.Add(new ComboBoxItem { Content = Lang.Lunarward_Editor_KeepPrefixGraphics, Tag = null });
        foreach (Component component in graphicsComponents)
        {
            graphicsBox.Items.Add(new ComboBoxItem { Content = $"{component.Name}（{component.KindText}）", Tag = component.Id });
        }
        ComboBoxItem? selectedGraphics = graphicsBox.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(i => (i.Tag as string) == profile.GraphicsId);
        graphicsBox.SelectedItem = selectedGraphics ?? graphicsBox.Items[0];
        ToggleSwitch ensureGraphicsSwitch = new()
        {
            Header = Lang.Lunarward_Editor_EnsureGraphics,
            IsOn = profile.EnsureGraphics,
            OnContent = "开",
            OffContent = "关",
        };

        StackPanel content = new() { Spacing = 12, MinWidth = 520 };
        content.Children.Add(nameBox);
        content.Children.Add(runnerTypeBox);
        content.Children.Add(runnerPanel);
        content.Children.Add(prefixModeBox);
        content.Children.Add(prefixGrid);
        content.Children.Add(makeTemplate);
        content.Children.Add(graphicsBox);
        content.Children.Add(ensureGraphicsSwitch);
        content.Children.Add(shellBox);
        content.Children.Add(envBox);
        content.Children.Add(new TextBlock
        {
            Text = Lang.Lunarward_RuntimeEnv_PrefixModeNote,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.65,
        });

        // Uno 的坑：ComboBox 还在可视树外时设的 SelectedIndex 会被重置成 0（表现为「明明是外部前缀，却显示模板克隆」）。
        // 内容拼好、进树之前补一次。
        int restoredModeIndex = Array.IndexOf(modes, profile.PrefixMode);
        if (restoredModeIndex < 0)
        {
            restoredModeIndex = 3;
        }
        prefixModeBox.SelectedIndex = restoredModeIndex;


        ContentDialog dialog = new()
        {
            Title = isNew ? "新建运行配置" : "编辑运行配置",
            Content = new ScrollViewer { Content = content, MaxHeight = 520 },
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() is not ContentDialogResult.Primary)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(nameBox.Text))
        {
            Helpers.InAppToast.MainWindow?.Warning((string?)null, "请填写名称");
            return;
        }
        profile.Name = nameBox.Text.Trim();
        profile.RunnerType = runnerTypeBox.SelectedItem as string ?? "wine";
        profile.RunnerPath = string.IsNullOrWhiteSpace(runnerPathBox.Text) ? null : runnerPathBox.Text.Trim();
        profile.PrefixMode = modes[Math.Max(0, prefixModeBox.SelectedIndex)];
        profile.PrefixPath = string.IsNullOrWhiteSpace(prefixPathBox.Text) ? null : prefixPathBox.Text.Trim();
        profile.Shell = shellBox.Text?.Trim() ?? "";
        profile.GraphicsId = (graphicsBox.SelectedItem as ComboBoxItem)?.Tag as string;
        profile.EnsureGraphics = ensureGraphicsSwitch.IsOn;
        Dictionary<string, string> env = new();
        foreach (string line in (envBox.Text ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            int eq = line.IndexOf('=');
            if (eq > 0)
            {
                env[line[..eq].Trim()] = line[(eq + 1)..].Trim();
            }
        }
        profile.Environment = env;
        RuntimeProfileStore.Save(profile, _logger);
        LoadProfiles();
    }

}
