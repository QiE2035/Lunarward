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
            && profile.PrefixMode is "external" or "dedicated"
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

        // 「在这个方案下运行」的第二种形态：不启动游戏，用同一套环境跑一次工具（D64）
        var runButton = new Button { Content = "运行程序…", Tag = profile };
        runButton.Click += Button_RunOnce_Click;
        buttons.Children.Add(runButton);

        var editButton = new Button { Content = Lang.Common_Edit, Tag = profile };
        editButton.Click += Button_Edit_Click;
        var cloneButton = new Button { Content = Lang.Lunarward_RuntimeEnv_Clone, Tag = profile };
        cloneButton.Click += Button_Clone_Click;
        var deleteButton = new Button { Content = Lang.Common_Delete, Tag = profile };
        deleteButton.Click += Button_Delete_Click;
        buttons.Children.Add(editButton);
        buttons.Children.Add(cloneButton);
        buttons.Children.Add(deleteButton);

        Starward.Features.Runtime.LastGoodEntry? lastGood = Starward.Features.Runtime.LastKnownGoodStore.Get(profile.Id);
        var lastLine = new TextBlock
        {
            Text = lastGood is null
                ? "上次启动：还没有成功启动记录"
                : $"上次启动：{lastGood.At.LocalDateTime:MM-dd HH:mm} 成功" +
                  (string.IsNullOrWhiteSpace(lastGood.RunnerVersion) ? "" : $" · {lastGood.RunnerVersion}"),
            FontSize = 12,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
        };

        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(name);
        content.Children.Add(summary);
        content.Children.Add(lastLine);
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


    /// <summary>选一个程序 + 可选参数，在方案环境里跑一次（D64）。</summary>
    private async void Button_RunOnce_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: RuntimeProfile profile })
        {
            return;
        }
        try
        {
            string? file = await Helpers.FileDialogHelper.PickSingleFileAsync(XamlRoot);
            if (string.IsNullOrWhiteSpace(file))
            {
                return;
            }
            var argsBox = new TextBox { PlaceholderText = "参数（可选），例如 /S 或 -o out.bin", MinWidth = 420 };
            var dialog = new ContentDialog
            {
                Title = "运行一次",
                Content = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Text = file, FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = $"环境：{profile.Name}", FontSize = 12, Opacity = 0.7 },
                        argsBox,
                    },
                },
                PrimaryButtonText = "运行",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            var process = Starward.Features.Runtime.RunOnceService.Start(profile, file, argsBox.Text, _logger);
            if (process is null)
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, "没能启动：文件不存在，或这个方案没有可用的 wine。");
                return;
            }
            Helpers.InAppToast.MainWindow?.Success((string?)null, $"已启动（PID {process.Id}）", 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "run once failed");
            Helpers.InAppToast.MainWindow?.Error(ex, "运行失败");
        }
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
            // 同内容的多个副本折叠成一行（本机实测 3.1.1 有 3 份），避免看起来像 5 个不同版本
            foreach (List<Component> group in Starward.Features.Runtime.ComponentStore.GroupGraphics(graphics))
            {
                StackPanel_Components.Children.Add(CreateComponentRow(group[0], 0, group.Count, group));
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

        // 前缀里的图形层：状态 + 安装/重装/移除/校验（D63 §5 第 3、4 条）
        Starward.Features.Runtime.GraphicsInstaller.GraphicsStatus status =
            prefix.Exists ? Starward.Features.Runtime.GraphicsInstaller.Verify(prefix.Path) : new();
        string installedId = status.Record?.Id ?? "";

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var open = new Button { Content = "打开目录", Tag = prefix.Path };
        open.Click += (_, _) => Helpers.OpenUrl.Open(prefix.Path);
        buttons.Children.Add(open);

        if (prefix.Exists && !prefix.Locked)
        {
            var graphicsButton = new Button
            {
                Content = status.Record is null ? "安装图形层" : "重装",
                Tag = (prefix.Path, installedId),
            };
            graphicsButton.Click += Button_InstallGraphicsToPrefix_Click;
            buttons.Children.Add(graphicsButton);

            if (status.Record is not null)
            {
                var removeGraphics = new Button { Content = "移除图形层", Tag = prefix.Path };
                removeGraphics.Click += Button_RemoveGraphicsFromPrefix_Click;
                buttons.Children.Add(removeGraphics);

                var verify = new Button { Content = "校验", Tag = prefix.Path };
                verify.Click += Button_VerifyGraphics_Click;
                buttons.Children.Add(verify);
            }
        }
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
        content.Children.Add(new TextBlock
        {
            Text = status.Text,
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


    /// <summary>把图形组件装进这个前缀；未装过时按该前缀绑定方案的 GraphicsId 挑一个。</summary>
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
                Helpers.InAppToast.MainWindow?.Warning((string?)null, "没有检测到 DXVK / VKD3D。把组件放进 ~/.local/share/lunarward/components 或点「导入图形组件…」。", 6000);
                return;
            }
            string wanted = installedId;
            if (string.IsNullOrEmpty(wanted))
            {
                IReadOnlyList<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll(_logger);
                wanted = profiles.FirstOrDefault(x => string.Equals(
                    Starward.Features.Runtime.PrefixManager.ExpandPath(x.PrefixPath), prefixPath, StringComparison.Ordinal))?.GraphicsId ?? "";
            }
            Component? chosen = graphics.FirstOrDefault(x => x.Id == wanted) ?? graphics[0];
            bool installed = Starward.Features.Runtime.GraphicsInstaller.EnsureInstalled(prefixPath, chosen, _logger);
            LoadPrefixes();
            Helpers.InAppToast.MainWindow?.Success((string?)null,
                installed ? $"已装 {chosen.Name}" : "记录与文件都齐全，已跳过", 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "install graphics to prefix failed");
            Helpers.InAppToast.MainWindow?.Error(ex, "安装图形层失败");
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
            Title = external ? "这是外部前缀，确认移除？" : "移除图形层？",
            Content = "会按记录删掉装进 system32 / syswow64 的那几个 dll，并删除记录（保留 .bak）。游戏会回到 wine 自带的 d3d 实现。\n" + prefixPath,
            PrimaryButtonText = "移除",
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
            Starward.Features.Runtime.GraphicsInstaller.Remove(prefixPath, _logger);
            LoadPrefixes();
            Helpers.InAppToast.MainWindow?.Success((string?)null, "已移除图形层", 3000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "remove graphics failed");
            Helpers.InAppToast.MainWindow?.Error(ex, "移除失败");
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
            $"{status.Text}（{status.TotalFiles - status.MissingFiles}/{status.TotalFiles} 个文件在位）", 6000);
    }


    // ── 方案编辑（D63 §4）：整页视图，四段：运行器 / 图形层 / 前缀 / 高级 ──

    private RuntimeProfile? _editingProfile;

    private TextBox? _editorName;
    private ComboBox? _editorRunner;
    private ComboBox? _editorDxvk;

    private RadioButton? _editorPrefixNew;
    private RadioButton? _editorPrefixCopy;
    private RadioButton? _editorPrefixExternal;
    private RadioButton? _editorPrefixEphemeral;
    private TextBox? _editorPrefixPath;
    private TextBox? _editorEnv;
    private TextBox? _editorShell;
    private TextBlock? _editorPrefixInfo;
    private Button? _editorBrowse;
    private List<Component> _editorRunners = new();
    private List<Component> _editorGraphics = new();


    private void OpenEditor(RuntimeProfile? profile)
    {
        _editingProfile = profile;
        _editorRunners = ComponentStore.GetRunners(false, _logger).ToList();
        _editorGraphics = ComponentStore.GetGraphics(false, _logger).ToList();
        StackPanel_Editor.Children.Clear();

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var back = new Button { Content = "← 返回" };
        back.Click += (_, _) => CloseEditor();
        header.Children.Add(back);
        header.Children.Add(new TextBlock
        {
            Text = profile is null ? "新建方案" : "编辑方案",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            VerticalAlignment = VerticalAlignment.Center,
        });
        StackPanel_Editor.Children.Add(header);

        // 0) 名称
        StackPanel_Editor.Children.Add(SectionTitle("名称"));
        _editorName = new TextBox
        {
            Text = profile?.Name ?? "新方案",
            MinWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        StackPanel_Editor.Children.Add(_editorName);

        // 1) 运行器
        StackPanel_Editor.Children.Add(SectionTitle("运行器"));
        _editorRunner = new ComboBox { MinWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
        foreach (Component runner in _editorRunners)
        {
            _editorRunner.Items.Add($"{runner.Name} {runner.Version} · {runner.LayoutText}");
        }
        int runnerIndex = _editorRunners.FindIndex(x => string.Equals(x.Path,
            Starward.Features.Runtime.PrefixManager.ExpandPath(profile?.RunnerPath), StringComparison.Ordinal));
        _editorRunner.SelectedIndex = runnerIndex >= 0 ? runnerIndex : (_editorRunners.Count > 0 ? 0 : -1);
        StackPanel_Editor.Children.Add(_editorRunner);
        if (profile is not null
            && !string.IsNullOrWhiteSpace(profile.RunnerPath)
            && runnerIndex < 0)
        {
            StackPanel_Editor.Children.Add(Hint($"原运行器已不在检测结果里：{profile.RunnerPath}"));
        }

        // 2) 图形层
        StackPanel_Editor.Children.Add(SectionTitle("图形层"));
        _editorDxvk = GraphicsCombo(ComponentKind.Dxvk, profile?.GraphicsId);
        StackPanel_Editor.Children.Add(Row("DXVK", _editorDxvk));
        StackPanel_Editor.Children.Add(Hint("图形层要装进前缀后才生效：保存后到「前缀」段点安装/重装/移除。"));

        // 3) 前缀
        StackPanel_Editor.Children.Add(SectionTitle("前缀"));
        _editorPrefixNew = new RadioButton { Content = "新建一个（推荐）", GroupName = "prefix" };
        _editorPrefixCopy = new RadioButton { Content = "复制自现有前缀…", GroupName = "prefix" };
        _editorPrefixExternal = new RadioButton { Content = "使用已有目录", GroupName = "prefix" };
        _editorPrefixEphemeral = new RadioButton { Content = "临时（退出即删，调试组合用）", GroupName = "prefix" };
        string mode = profile?.PrefixMode ?? "dedicated";
        switch (mode)
        {
            case "external": _editorPrefixExternal.IsChecked = true; break;
            case "template": _editorPrefixCopy.IsChecked = true; break;
            case "ephemeral": _editorPrefixEphemeral.IsChecked = true; break;
            default: _editorPrefixNew.IsChecked = true; break;
        }
        // 「临时」属于调试用途，放进高级折叠，避免和「新建/复制自现有」并列时选错（D63 §2 精简）
        foreach (RadioButton radio in new[] { _editorPrefixNew, _editorPrefixCopy, _editorPrefixExternal })
        {
            radio.Checked += (_, _) => UpdateEditorPrefixRow();
            StackPanel_Editor.Children.Add(radio);
        }
        var pathRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        _editorPrefixPath = new TextBox
        {
            MinWidth = 420,
            Text = profile?.PrefixPath ?? "",
            PlaceholderText = "留空 = 默认位置",
        };
        _editorBrowse = new Button { Content = "浏览…" };
        _editorBrowse.Click += Button_EditorBrowsePrefix_Click;
        pathRow.Children.Add(_editorPrefixPath);
        pathRow.Children.Add(_editorBrowse);
        StackPanel_Editor.Children.Add(pathRow);
        _editorPrefixInfo = new TextBlock { FontSize = 12, Opacity = 0.7, TextWrapping = TextWrapping.Wrap };
        StackPanel_Editor.Children.Add(_editorPrefixInfo);
        StackPanel_Editor.Children.Add(Hint("换运行器后，现有前缀会在下次启动时走一次 wine 更新（wineboot -u）。"));

        // 4) 高级
        var expander = new Expander { Header = "高级", IsExpanded = false, HorizontalAlignment = HorizontalAlignment.Stretch };
        var advanced = new StackPanel { Spacing = 6 };
        _editorPrefixEphemeral.Checked += (_, _) => UpdateEditorPrefixRow();
        advanced.Children.Add(new TextBlock { Text = "前缀（调试用）", FontSize = 12, Opacity = 0.7 });
        advanced.Children.Add(_editorPrefixEphemeral);
        advanced.Children.Add(new TextBlock { Text = "环境变量（每行 KEY=VALUE）", FontSize = 12, Opacity = 0.7 });
        _editorEnv = new TextBox
        {
            AcceptsReturn = true,
            Height = 96,
            TextWrapping = TextWrapping.Wrap,
            Text = profile is null ? "" : string.Join(Environment.NewLine, profile.Environment.Select(x => $"{x.Key}={x.Value}")),
        };
        advanced.Children.Add(_editorEnv);
        advanced.Children.Add(new TextBlock { Text = "调试 Shell（留空 = 跟随系统）", FontSize = 12, Opacity = 0.7 });
        _editorShell = new TextBox { Text = profile?.Shell ?? "", PlaceholderText = "/bin/bash" };
        advanced.Children.Add(_editorShell);
        expander.Content = advanced;
        StackPanel_Editor.Children.Add(expander);

        // 保存 / 取消
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        var save = new Button { Content = "保存", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        save.Click += Button_EditorSave_Click;
        var cancel = new Button { Content = "取消" };
        cancel.Click += (_, _) => CloseEditor();
        actions.Children.Add(save);
        actions.Children.Add(cancel);
        StackPanel_Editor.Children.Add(actions);

        UpdateEditorPrefixRow();
        ScrollViewer_Content.Visibility = Visibility.Collapsed;
        ScrollViewer_Editor.Visibility = Visibility.Visible;
    }


    private void CloseEditor()
    {
        ScrollViewer_Editor.Visibility = Visibility.Collapsed;
        ScrollViewer_Content.Visibility = Visibility.Visible;
        StackPanel_Editor.Children.Clear();
        _editingProfile = null;
    }


    private static TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        Margin = new Thickness(0, 12, 0, 0),
    };


    private static TextBlock Hint(string text) => new()
    {
        Text = text,
        FontSize = 12,
        Opacity = 0.7,
        TextWrapping = TextWrapping.Wrap,
    };


    private static StackPanel Row(string label, UIElement control)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        row.Children.Add(new TextBlock { Text = label, MinWidth = 64, VerticalAlignment = VerticalAlignment.Center });
        row.Children.Add(control);
        return row;
    }


    private ComboBox GraphicsCombo(ComponentKind kind, string? selectedId)
    {
        var combo = new ComboBox { MinWidth = 320, HorizontalAlignment = HorizontalAlignment.Left };
        combo.Items.Add("（不选）");
        int index = 0;
        List<Component> items = _editorGraphics.Where(x => x.Kind == kind).ToList();
        for (int i = 0; i < items.Count; i++)
        {
            combo.Items.Add($"{items[i].Name} · {items[i].Version}");
            if (string.Equals(items[i].Id, selectedId, StringComparison.Ordinal))
            {
                index = i + 1;
            }
        }
        combo.SelectedIndex = index;
        combo.Tag = items;
        return combo;
    }


    private void UpdateEditorPrefixRow()
    {
        if (_editorPrefixInfo is null || _editorPrefixPath is null || _editorBrowse is null)
        {
            return;
        }
        bool external = _editorPrefixExternal?.IsChecked == true;
        bool ephemeral = _editorPrefixEphemeral?.IsChecked == true;
        _editorPrefixPath.IsEnabled = !ephemeral;
        _editorBrowse.IsEnabled = !ephemeral;
        string? path = Starward.Features.Runtime.PrefixManager.ExpandPath(_editorPrefixPath.Text);
        if (string.IsNullOrWhiteSpace(path))
        {
            _editorPrefixInfo.Text = ephemeral
                ? "临时前缀：从复制源克隆一份，游戏退出后删除。"
                : "留空则使用默认位置：~/.local/share/lunarward/prefix/<游戏>";
            return;
        }
        Starward.Features.Runtime.PrefixManager.PrefixInspection info =
            Starward.Features.Runtime.PrefixManager.Inspect(path, computeSize: false);
        string state = !info.Exists ? "不存在" : !info.HasDriveC ? "缺少 drive_c" : info.Locked ? "正被占用" : "健康";
        _editorPrefixInfo.Text = $"{state}{(external ? "（外部目录，我们不会修改它的内容）" : "")} · {path}";
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


    private void Button_EditorSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_editorRunner is null || _editorRunner.SelectedIndex < 0 || _editorRunner.SelectedIndex >= _editorRunners.Count)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, "先选一个运行器。", 4000);
                return;
            }
            Component runner = _editorRunners[_editorRunner.SelectedIndex];
            if (runner.Usable == false)
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, $"这个运行器不可用：{runner.UsabilityReason}", 6000);
                return;
            }
            string mode = _editorPrefixExternal?.IsChecked == true ? "external"
                : _editorPrefixCopy?.IsChecked == true ? "template"
                : _editorPrefixEphemeral?.IsChecked == true ? "ephemeral"
                : "dedicated";
            string prefixPath = _editorPrefixPath?.Text?.Trim() ?? "";
            if (mode is "external" or "template" && string.IsNullOrWhiteSpace(prefixPath))
            {
                Helpers.InAppToast.MainWindow?.Warning((string?)null, "这个前缀来源需要填一个目录。", 4000);
                return;
            }

            List<RuntimeProfile> all = RuntimeProfileStore.LoadAll(_logger).ToList();
            RuntimeProfile profile = _editingProfile is null
                ? new RuntimeProfile { Id = Guid.NewGuid().ToString("N")[..8] }
                : all.FirstOrDefault(x => x.Id == _editingProfile.Id) ?? _editingProfile;
            profile.Name = string.IsNullOrWhiteSpace(_editorName?.Text) ? (profile.Name.Length > 0 ? profile.Name : "新方案") : _editorName.Text.Trim();
            profile.RunnerType = runner.Kind == ComponentKind.Proton ? "proton" : "wine";
            profile.RunnerPath = runner.Path;
            profile.GraphicsId = SelectedGraphicsId(_editorDxvk);
            profile.PrefixMode = mode;
            profile.PrefixPath = prefixPath;
            profile.Environment = ParseEnv(_editorEnv?.Text);
            profile.Shell = _editorShell?.Text?.Trim() ?? "";

            RuntimeProfileStore.Save(profile, _logger);
            ComponentStore.Invalidate();
            CloseEditor();
            LoadProfiles();
            LoadPrefixes();
            Helpers.InAppToast.MainWindow?.Success((string?)null, "已保存", 3000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "save profile failed");
            Helpers.InAppToast.MainWindow?.Error(ex, "保存失败");
        }
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
        Grid.SetColumn(kindPanel, 0);
        Grid.SetColumn(text, 1);
        row.Children.Add(kindPanel);
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
            if (groupCount > 1)
            {
                var copies = new TextBlock
                {
                    Text = $"共 {groupCount} 份",
                    FontSize = 12,
                    Opacity = 0.6,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(copies, 2);
                row.Children.Add(copies);
            }
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
}
