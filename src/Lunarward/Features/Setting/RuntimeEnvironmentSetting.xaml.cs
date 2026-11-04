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
        LoadProfiles();
        LoadComponents();
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
        AppendTemplatesSection(StackPanel_Profiles);
        StackPanel_Profiles.Children.Add(new TextBlock
        {
            Text = "组件的下载与版本管理（DXVK/VKD3D/运行器）、Profile 的完整校验在 M2 补齐；「伴随启动」在游戏设置页。",
            Opacity = 0.6,
            FontSize = 12,
            Margin = new Thickness(12, 8, 0, 0),
            TextWrapping = TextWrapping.Wrap,
        });
    }


    /// <summary>前缀模板库：列出 prefix-templates 下的目录，可直接用来新建配置或删除（M2）。</summary>
    private void AppendTemplatesSection(Panel host)
    {
        host.Children.Add(new TextBlock
        {
            Text = Lang.Lunarward_RuntimeEnv_Templates_Title,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(12, 16, 0, 4),
        });
        string folder = RuntimeProfileStore.TemplatesFolder;
        string[] templates;
        try
        {
            templates = Directory.Exists(folder)
                ? Directory.GetDirectories(folder).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()
                : Array.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "List prefix templates failed");
            templates = Array.Empty<string>();
        }
        if (templates.Length == 0)
        {
            host.Children.Add(new TextBlock
            {
                Text = Lang.Lunarward_RuntimeEnv_TemplatesNote,
                Opacity = 0.65,
                FontSize = 12,
                Margin = new Thickness(12, 0, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });
            return;
        }
        foreach (string template in templates)
        {
            host.Children.Add(CreateTemplateRow(template));
        }
    }


    private UIElement CreateTemplateRow(string templatePath)
    {
        var info = new DirectoryInfo(templatePath);
        long size = 0;
        try
        {
            size = info.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch
        {
            // 权限或链接问题不该让整页崩掉。
        }
        var text = new TextBlock
        {
            Text = $"{info.Name}\n{size / 1024.0 / 1024.0:0} MB · 修改于 {info.LastWriteTime:yyyy-MM-dd HH:mm}",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Opacity = 0.75,
        };
        ToolTipService.SetToolTip(text, templatePath);

        var useButton = new Button { Content = Lang.Lunarward_Template_Use, Tag = templatePath };
        useButton.Click += Button_UseTemplate_Click;
        var deleteButton = new Button { Content = Lang.Common_Delete, Tag = templatePath };
        deleteButton.Click += Button_DeleteTemplate_Click;

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(useButton);
        buttons.Children.Add(deleteButton);

        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(text);
        panel.Children.Add(buttons);

        return new Border
        {
            Margin = new Thickness(12, 0, 12, 8),
            Padding = new Thickness(12),
            CornerRadius = new CornerRadius(4),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = panel,
        };
    }


    private async void Button_UseTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string templatePath })
        {
            return;
        }
        var profile = new RuntimeProfile
        {
            Name = Path.GetFileName(templatePath) + " 配置",
            RunnerType = "wine",
            RunnerPath = Features.GameLauncher.GameLauncherService.ResolveWine() ?? "",
            PrefixMode = "template",
            PrefixPath = templatePath,
        };
        await ShowProfileEditorAsync(profile, isNew: true);
    }


    private async void Button_DeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string templatePath })
        {
            return;
        }
        var dialog = new ContentDialog
        {
            Title = "删除模板？",
            Content = new TextBlock
            {
                Text = $"会删除目录：\n{templatePath}\n\n这不会影响任何已存在的游戏前缀，只删模板本身。",
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = "删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }
        try
        {
            Directory.Delete(templatePath, recursive: true);
            Helpers.InAppToast.MainWindow?.Success((string?)null, "模板已删除", 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete template failed");
            Helpers.InAppToast.MainWindow?.Error((string?)null, $"删除失败：{ex.Message}", 6000);
        }
        LoadProfiles();
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
        StackPanel_Components.Children.Clear();
        IReadOnlyList<Component> runners = ComponentStore.GetRunners(false, _logger);
        IReadOnlyList<Component> graphics = ComponentStore.GetGraphics(false, _logger);
        if (runners.Count == 0 && graphics.Count == 0)
        {
            StackPanel_Components.Children.Add(new TextBlock { Text = Lang.Lunarward_Components_Empty, Opacity = 0.7 });
            return;
        }
        foreach (Component runner in runners)
        {
            StackPanel_Components.Children.Add(CreateComponentRow(runner));
        }
        foreach (Component graphic in graphics)
        {
            StackPanel_Components.Children.Add(CreateComponentRow(graphic));
        }
    }


    private UIElement CreateComponentRow(Component component)
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
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        row.Children.Add(kind);
        row.Children.Add(text);
        if (component.Kind is ComponentKind.Dxvk or ComponentKind.Vkd3dProton)
        {
            // 图形组件要「装进某个前缀」才有意义：这里直接挑一个前缀目录装进去（D31 的按前缀安装）
            var installButton = new Button
            {
                Content = "安装到前缀…",
                Tag = component,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            installButton.Click += Button_InstallGraphics_Click;
            row.Children.Add(installButton);
        }
        return new Border
        {
            Child = row,
            Padding = new Thickness(12, 8, 12, 8),
            CornerRadius = new CornerRadius(4),
            Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
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

        ComboBox prefixModeBox = new() { Header = "前缀模式" };
        string[] modes = { "template", "dedicated", "shared", "external", "ephemeral" };
        string[] modeTexts = { "模板克隆（默认）", "独占", "共享", "外部（就地使用）", "临时（每次重建）" };
        for (int i = 0; i < modes.Length; i++)
        {
            prefixModeBox.Items.Add($"{modes[i]} — {modeTexts[i]}");
            if (modes[i] == profile.PrefixMode)
            {
                prefixModeBox.SelectedIndex = i;
            }
        }
        if (prefixModeBox.SelectedIndex < 0)
        {
            prefixModeBox.SelectedIndex = 3;
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

        // 一键把现有前缀（如 ~/.wine 或某个启动器的 prefix）变成可复用的模板
        Button makeTemplate = new() { Content = Lang.Lunarward_Template_Create, HorizontalAlignment = HorizontalAlignment.Left };
        ToolTipService.SetToolTip(makeTemplate, "选择已有的 Wine 前缀目录，reflink 复制成模板；之后可用「模板克隆」模式秒级克隆出独立实例");
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
                Helpers.InAppToast.MainWindow?.Success((string?)null, $"模板已创建：{destination}", 6000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Create prefix template failed: {source}", source);
                Helpers.InAppToast.MainWindow?.Error((string?)null, "创建模板失败，详见日志");
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
