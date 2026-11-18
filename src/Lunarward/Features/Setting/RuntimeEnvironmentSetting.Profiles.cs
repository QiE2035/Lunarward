// Lunarward：运行环境页的「方案」页（D85）——左列清单 + 右列详情 / 编辑 / 增强三态。
// 设计前提：列表用代码构建（Uno 的 ListView 在星号行 + ScrollViewer 里会抽风，D58 v2）；
// 详情里的每段都是手写 Border 卡片，没有 Uno Toolkit（本工程 <UnoFeatures> 不含 Toolkit，没有 SettingsCard）。
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Starward.Core;
using Starward.Features.Runtime;
using Starward.Language;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Starward.Features.Setting;


public sealed partial class RuntimeEnvironmentSetting
{

    /// <summary>当前方案列表（最近一次 LoadProfiles 的结果）。</summary>
    private IReadOnlyList<RuntimeProfile> _profiles = Array.Empty<RuntimeProfile>();


    /// <summary>右栏正在看哪一份（删掉/改名之后按 Id 校验）。</summary>
    private string? _selectedProfileId;


    // ───────────────────────────── 左列清单 ─────────────────────────────


    private UIElement CreateProfileListRow(RuntimeProfile profile)
    {
        PrefixManager.PrefixInspection inspection = PrefixManager.Inspect(EnvironmentResolver.EffectivePrefixPath(profile));
        (string dot, string text) = DescribeHealth(profile, inspection);
        int bindings = ProfileBinding.Usages(profile.Id).Count;

        var grid = new Grid { ColumnSpacing = 8 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var statusDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            Margin = new Thickness(0, 7, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Fill = BrushOf(dot),
        };
        Grid.SetColumn(statusDot, 0);
        grid.Children.Add(statusDot);

        var text2 = new StackPanel { Spacing = 2 };
        text2.Children.Add(new TextBlock
        {
            Text = profile.Name,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        var subtitle = new TextBlock
        {
            Text = bindings > 0 ? $"{text} · {string.Format(Lang.Lunarward_RuntimeEnv_BindingsCount, bindings)}" : text,
            FontSize = 12,
            Opacity = 0.7,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        text2.Children.Add(subtitle);
        Grid.SetColumn(text2, 1);
        grid.Children.Add(text2);

        var button = new Button
        {
            Tag = profile,
            Content = grid,
            Style = (Style)Resources["ProfileListItemStyle"],
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        button.Click += Button_ProfileList_Click;
        ToolTipService.SetToolTip(button, $"{profile.Name}\n{text}");
        return button;
    }


    /// <summary>点左列某一行：切右栏，列表选中态跟着走。</summary>
    private void Button_ProfileList_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: RuntimeProfile profile })
        {
            return;
        }
        _selectedProfileId = profile.Id;
        UpdateProfileListSelection();
        ShowProfileDetail(profile);
        ShowDetailPane();
    }


    /// <summary>左列选中态：整行底色（Uno 的 Button 不给 ListView 那种选中视觉，自己画）。</summary>
    private void UpdateProfileListSelection()
    {
        Brush selected = BrushOf("Secondary");
        Brush normal = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        foreach (UIElement child in StackPanel_ProfileList.Children)
        {
            if (child is Button { Tag: RuntimeProfile profile } button)
            {
                button.Background = profile.Id == _selectedProfileId ? selected : normal;
            }
        }
    }


    /// <summary>
    /// 把健康灯的 <c>#RRGGBB</c> 转成 Brush。选中底色用主题画刷，
    /// 传 <c>Secondary</c> 表示「次要填充」（和页签选中同一档视觉）。
    /// </summary>
    private static Brush BrushOf(string colorOrKeyword)
    {
        if (colorOrKeyword == "Secondary")
        {
            return Application.Current.Resources["ControlFillColorSecondaryBrush"] as Brush
                ?? new SolidColorBrush(Microsoft.UI.Colors.DimGray);
        }
        try
        {
            string hex = colorOrKeyword.TrimStart('#');
            byte r = Convert.ToByte(hex[..2], 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, r, g, b));
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "brush from {color} failed", colorOrKeyword);
            return new SolidColorBrush(Microsoft.UI.Colors.Gray);
        }
    }


    // ───────────────────────────── 右栏三态 ─────────────────────────────


    /// <summary>详情（默认）。</summary>
    private void ShowDetailPane()
    {
        ScrollViewer_ProfileDetail.Visibility = Visibility.Visible;
        ScrollViewer_ProfileEditor.Visibility = Visibility.Collapsed;
        ScrollViewer_ProfileEnhance.Visibility = Visibility.Collapsed;
        UpdateContentWidth();
    }


    /// <summary>编辑（就地，不整页覆盖：左列还在，能看到「改成谁」）。</summary>
    private void ShowEditorPane()
    {
        ScrollViewer_ProfileDetail.Visibility = Visibility.Collapsed;
        ScrollViewer_ProfileEditor.Visibility = Visibility.Visible;
        ScrollViewer_ProfileEnhance.Visibility = Visibility.Collapsed;
        UpdateContentWidth();
    }


    /// <summary>增强（原「增强」页签，现在从方案详情点进来）。</summary>
    private void ShowEnhancePane()
    {
        ScrollViewer_ProfileDetail.Visibility = Visibility.Collapsed;
        ScrollViewer_ProfileEditor.Visibility = Visibility.Collapsed;
        ScrollViewer_ProfileEnhance.Visibility = Visibility.Visible;
        UpdateContentWidth();
    }


    private void Button_EditorBack_Click(object sender, RoutedEventArgs e) => CloseEditor();


    private void Button_EnhanceBack_Click(object sender, RoutedEventArgs e)
    {
        ShowDetailPane();
        ShowProfileDetail(SelectedProfileOrNull());
    }


    private RuntimeProfile? SelectedProfileOrNull()
    {
        return _profiles.FirstOrDefault(x => x.Id == _selectedProfileId);
    }


    // ───────────────────────────── 右栏详情 ─────────────────────────────


    /// <summary>右栏详情：五段卡片（概览 / 运行器 / 图形层 / 前缀 / 增强 / 最近启动）。</summary>
    private void ShowProfileDetail(RuntimeProfile? profile)
    {
        StackPanel_ProfileDetail.Children.Clear();
        if (profile is null)
        {
            StackPanel_ProfileDetail.Children.Add(Hint(Lang.Lunarward_Profiles_Empty));
            return;
        }
        string prefixPath = EnvironmentResolver.EffectivePrefixPath(profile);
        PrefixManager.PrefixInspection inspection = PrefixManager.Inspect(prefixPath);
        IReadOnlyList<GameBiz> bindings = ProfileBinding.Usages(profile.Id);

        StackPanel_ProfileDetail.Children.Add(CreateProfileHeader(profile, inspection, bindings, prefixPath));
        StackPanel_ProfileDetail.Children.Add(CreateRunnerSection(profile));
        StackPanel_ProfileDetail.Children.Add(CreateGraphicsSection(profile, inspection, prefixPath));
        StackPanel_ProfileDetail.Children.Add(CreatePrefixSection(profile, inspection, prefixPath, bindings));
        StackPanel_ProfileDetail.Children.Add(CreateEnhanceSection(profile));
        StackPanel_ProfileDetail.Children.Add(CreateHistorySection(profile));
    }


    /// <summary>顶部卡片：名字 + 健康灯 + 摘要 + 主次按钮（主：编辑并启动；次：⋯菜单）。</summary>
    private Border CreateProfileHeader(RuntimeProfile profile, PrefixManager.PrefixInspection inspection, IReadOnlyList<GameBiz> bindings, string prefixPath)
    {
        (string dot, string healthText) = DescribeHealth(profile, inspection);

        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(new TextBlock
        {
            Text = profile.Name,
            FontSize = 18,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
        });

        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        statusRow.Children.Add(new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = BrushOf(dot),
        });
        statusRow.Children.Add(new TextBlock { Text = healthText, FontSize = 13, Opacity = 0.85 });
        body.Children.Add(statusRow);

        string extras = DescribeLaunchExtras(profile);
        if (!string.IsNullOrWhiteSpace(extras))
        {
            body.Children.Add(new TextBlock
            {
                Text = extras,
                FontSize = 12,
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap,
            });
        }
        body.Children.Add(new TextBlock
        {
            Text = bindings.Count == 0
                ? Lang.Lunarward_RuntimeEnv_UsedByNone
                : string.Join("、", bindings.Select(x => x.ToGameName())),
            FontSize = 12,
            Opacity = 0.7,
            TextWrapping = TextWrapping.Wrap,
        });

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        var runButton = new Button
        {
            Content = Lang.Lunarward_RuntimeEnv_RunOnce,
            Tag = profile,
            Style = (Style)Application.Current.Resources["AccentButtonStyle"],
        };
        runButton.Click += Button_RunOnce_Click;
        buttons.Children.Add(runButton);

        var editButton = new Button { Content = Lang.Lunarward_RuntimeEnv_EditProfile, Tag = profile };
        editButton.Click += Button_Edit_Click;
        buttons.Children.Add(editButton);

        // 低频动作进 ⋯ 菜单：一屏里六个同权重按钮谁都不突出（D85 诊断第 2 条）
        var moreButton = new Button { Content = "\uE712", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 14 };
        ToolTipService.SetToolTip(moreButton, Lang.Lunarward_RuntimeEnv_More);
        var moreFlyout = new MenuFlyout();
        moreFlyout.Items.Add(MenuItem(Lang.Lunarward_RuntimeEnv_Clone, profile, Button_Clone_Click));
        moreFlyout.Items.Add(MenuItem(Lang.Lunarward_RuntimeEnv_TextView, profile, Button_TextView_Click));
        moreFlyout.Items.Add(MenuItem(Lang.Lunarward_RuntimeEnv_OpenShell, profile, Button_OpenShell_Click));
        moreFlyout.Items.Add(new MenuFlyoutSeparator());
        moreFlyout.Items.Add(MenuItem(Lang.Lunarward_RuntimeEnv_Delete, profile, Button_Delete_Click));
        moreButton.Flyout = moreFlyout;
        buttons.Children.Add(moreButton);

        body.Children.Add(buttons);
        return Card(body);
    }


    private static MenuFlyoutItem MenuItem(string text, object? tag, RoutedEventHandler handler)
    {
        var item = new MenuFlyoutItem { Text = text, Tag = tag };
        item.Click += handler;
        return item;
    }


    private Border CreateRunnerSection(RuntimeProfile profile)
    {
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(profile.RunnerName) ? Lang.Lunarward_RuntimeEnv_NoUsableRunner : profile.RunnerName!,
            TextWrapping = TextWrapping.Wrap,
        });
        if (!string.IsNullOrWhiteSpace(profile.RunnerPath))
        {
            body.Children.Add(Hint(profile.RunnerPath!));
        }
        var editButton = new Button { Content = Lang.Common_Change, Tag = profile, VerticalAlignment = VerticalAlignment.Top };
        editButton.Click += Button_Edit_Click;
        return Section(Lang.Lunarward_RuntimeEnv_RunnerSectionTitle, body, editButton);
    }


    private Border CreateGraphicsSection(RuntimeProfile profile, PrefixManager.PrefixInspection inspection, string prefixPath)
    {
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock { Text = DescribeGraphics(profile), TextWrapping = TextWrapping.Wrap });

        (string syncText, _) = DescribeGraphicsSync(profile, inspection);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Top };
        if (!string.IsNullOrWhiteSpace(syncText))
        {
            body.Children.Add(new TextBlock
            {
                Text = syncText,
                FontSize = 12,
                Foreground = BrushOf("9D5D00"),
                TextWrapping = TextWrapping.Wrap,
            });
            var syncButton = new Button { Content = Lang.Lunarward_RuntimeEnv_SyncGraphics, Tag = profile };
            syncButton.Click += Button_SyncGraphics_Click;
            actions.Children.Add(syncButton);
        }
        var editButton = new Button { Content = Lang.Lunarward_RuntimeEnv_EditProfile, Tag = profile };
        editButton.Click += Button_Edit_Click;
        actions.Children.Add(editButton);
        return Section(Lang.Lunarward_RuntimeEnv_GraphicsSectionTitle, body, actions);
    }


    private Border CreatePrefixSection(RuntimeProfile profile, PrefixManager.PrefixInspection inspection, string prefixPath, IReadOnlyList<GameBiz> bindings)
    {
        var body = new StackPanel { Spacing = 4 };
        var pathText = new TextBlock
        {
            Text = prefixPath,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };
        body.Children.Add(pathText);

        var sizeText = new TextBlock { FontSize = 12, Opacity = 0.7, Text = Lang.Lunarward_RuntimeEnv_PrefixUsageDash };
        body.Children.Add(sizeText);
        if (inspection.Exists)
        {
            _ = FillPrefixSizeAsync(sizeText, prefixPath);
        }

        if (inspection.Locked)
        {
            body.Children.Add(Hint(Lang.Lunarward_RuntimeEnv_Locked));
        }
        if (!inspection.Exists)
        {
            body.Children.Add(Hint(Lang.Lunarward_RuntimeEnv_PrefixMissing));
        }
        else if (string.IsNullOrWhiteSpace(profile.PrefixPath))
        {
            body.Children.Add(Hint(Lang.Lunarward_RuntimeEnv_PrefixDefaultDir));
        }

        var openButton = new Button { Content = Lang.Lunarward_RuntimeEnv_OpenFolder };
        openButton.Click += (_, _) => Helpers.OpenUrl.Open(prefixPath);
        return Section(Lang.Lunarward_RuntimeEnv_Prefix, body, openButton);
    }


    private Border CreateEnhanceSection(RuntimeProfile profile)
    {
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(new TextBlock
        {
            Text = Enhancements.Describe(profile, Environment.GetEnvironmentVariable("PATH")),
            TextWrapping = TextWrapping.Wrap,
        });
        var configureButton = new Button { Content = Lang.Lunarward_RuntimeEnv_Configure, Tag = profile };
        configureButton.Click += Button_ConfigureEnhance_Click;
        return Section(Lang.Lunarward_RuntimeEnv_Enhancements, body, configureButton);
    }


    private Border CreateHistorySection(RuntimeProfile profile)
    {
        var body = new StackPanel { Spacing = 2 };
        IReadOnlyList<LaunchAttempt> attempts = LaunchHistoryStore.Recent(profile.Id, 3);
        if (attempts.Count == 0)
        {
            body.Children.Add(Hint(Lang.Lunarward_RuntimeEnv_LastLaunchNone));
        }
        else
        {
            foreach (LaunchAttempt attempt in attempts)
            {
                body.Children.Add(new TextBlock
                {
                    Text = "· " + attempt.Describe(),
                    FontSize = 12,
                    Opacity = attempt.Ok ? 0.8 : 0.95,
                    TextWrapping = TextWrapping.Wrap,
                });
            }
        }
        return Section(Lang.Lunarward_RuntimeEnv_LastLaunch, body);
    }


    /// <summary>一张卡片：标题 + 右侧动作 + 内容。</summary>
    private static Border Section(string title, UIElement content, params UIElement[] actions)
    {
        var header = new Grid { ColumnSpacing = 8 };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleText = new TextBlock { Text = title, FontSize = 13, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center };
        header.Children.Add(titleText);

        var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (UIElement action in actions)
        {
            actionPanel.Children.Add(action);
        }
        Grid.SetColumn(actionPanel, 1);
        header.Children.Add(actionPanel);

        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(header);
        body.Children.Add(content);
        return Card(body);
    }


    private static Border Card(UIElement content)
    {
        return new Border
        {
            Padding = new Thickness(14, 12, 14, 12),
            CornerRadius = new CornerRadius(8),
            Background = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush,
            Child = content,
        };
    }


    // ───────────────────────────── 顶部 chip 条 ─────────────────────────────


    /// <summary>
    /// chip 条：一眼看完规模，每个 chip 都能点进对应页签（D85）。
    /// 统计要在后台算（组件扫描会遍历 PATH 与 Steam 库，D72）。
    /// </summary>
    private async Task RefreshChipsAsync()
    {
        IReadOnlyList<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll(_logger);
        if (StackPanel_Chips is null)
        {
            return;
        }
        (IReadOnlyList<Component> runners, IReadOnlyList<Component> graphics) = await Task.Run(() =>
            (ComponentStore.GetRunners(false, _logger), ComponentStore.GetGraphics(false, _logger)));
        int usableRunners = runners.Count(x => x.Usable != false);

        List<PrefixManager.PrefixInspection> prefixes = await Task.Run(() =>
            PrefixManager.EnumerateKnownPrefixes(profiles, computeSize: true).ToList());
        long totalBytes = 0;
        foreach (PrefixManager.PrefixInspection prefix in prefixes)
        {
            if (prefix.SizeBytes > 0)
            {
                totalBytes += prefix.SizeBytes;
            }
        }

        StackPanel_Chips.Children.Clear();
        StackPanel_Chips.Children.Add(CreateChip(string.Format(Lang.Lunarward_RuntimeEnv_ChipProfiles, profiles.Count), 0, Lang.Lunarward_RuntimeEnv_Profiles));
        StackPanel_Chips.Children.Add(CreateChip(string.Format(Lang.Lunarward_RuntimeEnv_ChipBindings, ProfileBinding.BindingCount()), 0, Lang.Lunarward_RuntimeEnv_Profiles));
        StackPanel_Chips.Children.Add(CreateChip(string.Format(Lang.Lunarward_RuntimeEnv_ChipRunners, usableRunners, runners.Count), 1, Lang.Lunarward_RuntimeEnv_Runner));
        StackPanel_Chips.Children.Add(CreateChip(string.Format(Lang.Lunarward_RuntimeEnv_ChipGraphics, graphics.Count), 2, Lang.Lunarward_RuntimeEnv_Components_2));
        StackPanel_Chips.Children.Add(CreateChip($"{string.Format(Lang.Lunarward_RuntimeEnv_ChipPrefix, prefixes.Count)} · {FormatSize(totalBytes)}", 3, Lang.Lunarward_RuntimeEnv_Prefix));

        RefreshBindingNotice(profiles);
    }


    private Button CreateChip(string text, int tab, string tooltip)
    {
        var chip = new Button
        {
            Content = new TextBlock { Text = text, FontSize = 12 },
            Tag = tab,
            Padding = new Thickness(10, 4, 10, 4),
            CornerRadius = new CornerRadius(12),
            Background = BrushOf("Secondary"),
            BorderThickness = new Thickness(0),
        };
        chip.Click += Button_Chip_Click;
        ToolTipService.SetToolTip(chip, $"{tooltip} · 点这里看");
        return chip;
    }


    private void Button_Chip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: int tab })
        {
            ShowTab(tab);
        }
    }


    /// <summary>失效绑定提示（D80）：只影响启动时的回落，放方案页顶部不打断列表。</summary>
    private void RefreshBindingNotice(IReadOnlyList<RuntimeProfile> profiles)
    {
        StackPanel_BindingNotice.Children.Clear();
        if (profiles.Count == 0)
        {
            return;
        }
        IReadOnlyList<(GameBiz Biz, string ProfileId)> dangling = ProfileBinding.Dangling(profiles.Select(x => x.Id).ToList());
        if (dangling.Count == 0)
        {
            return;
        }
        RuntimeProfile fallback = profiles[0];
        var body = new StackPanel { Spacing = 6 };
        body.Children.Add(new TextBlock
        {
            Text = string.Format(Lang.Lunarward_RuntimeEnv_BindingNotice, dangling.Count),
            TextWrapping = TextWrapping.Wrap,
        });
        body.Children.Add(new TextBlock
        {
            Text = string.Join("、", dangling.Select(x => x.Biz.ToGameName())) + " —— " +
                   string.Format(Lang.Lunarward_RuntimeEnv_BindingFallback, fallback.Name),
            FontSize = 12,
            Opacity = 0.8,
            TextWrapping = TextWrapping.Wrap,
        });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var repair = new Button { Content = string.Format(Lang.Lunarward_RuntimeEnv_BindToFallback, fallback.Name), Tag = fallback.Id };
        repair.Click += Button_RepairBindings_Click;
        buttons.Children.Add(repair);
        var clear = new Button { Content = Lang.Lunarward_RuntimeEnv_ClearBindings };
        clear.Click += Button_ClearBindings_Click;
        buttons.Children.Add(clear);
        body.Children.Add(buttons);

        StackPanel_BindingNotice.Children.Add(new Border
        {
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(8),
            Background = Application.Current.Resources["SystemFillColorCautionBackgroundBrush"] as Brush,
            Child = body,
        });
    }


    // ───────────────────────────── 增强（从详情进入） ─────────────────────────────


    private void Button_ConfigureEnhance_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        OpenEnhance(profile);
    }


    // ───────────────────────────── 组件筛选 / 默认运行器 ─────────────────────────────


    /// <summary>组件页的筛选：0 全部、1 只有 DXVK、2 只有 VKD3D-Proton。</summary>
    private int _componentFilter;


    /// <summary>初始化筛选下拉（只在第一次建页面时跑）。</summary>
    private void InitializeComponentFilter()
    {
        if (Combo_ComponentFilter.Items.Count > 0)
        {
            return;
        }
        Combo_ComponentFilter.Items.Add(Lang.Lunarward_RuntimeEnv_FilterAll);
        Combo_ComponentFilter.Items.Add("DXVK");
        Combo_ComponentFilter.Items.Add("VKD3D-Proton");
        Combo_ComponentFilter.SelectedIndex = 0;
    }


    private void Combo_ComponentFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedIndex: >= 0 } combo || _profiles is null)
        {
            return;
        }
        _componentFilter = combo.SelectedIndex;
        LoadComponents();
    }


    /// <summary>
    /// 「设为新方案默认」：只影响之后新建的方案（已有方案各自那份不动）。
    /// 用一个下拉选，比给每行加按钮省地方，也不会让人以为「点了就改了现有方案」。
    /// </summary>
    private async void Button_SetDefaultRunner_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyList<Component> runners = await Task.Run(() => ComponentStore.GetRunners(false, _logger));
        if (runners.Count == 0)
        {
            Helpers.InAppToast.MainWindow?.Warning((string?)null, Lang.Lunarward_RuntimeEnv_NoUsableRunner, 5000);
            return;
        }
        var combo = new ComboBox { MinWidth = 420, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (Component runner in runners)
        {
            combo.Items.Add($"{runner.Name} · {runner.Version} · {runner.SourceText}");
        }
        string? current = RuntimeDefaults.DefaultRunnerId;
        int index = 0;
        for (int i = 0; i < runners.Count; i++)
        {
            if (runners[i].Id == current)
            {
                index = i;
                break;
            }
        }
        combo.SelectedIndex = index;

        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(new TextBlock { Text = Lang.Lunarward_RuntimeEnv_SetDefaultRunner, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(combo);

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = Lang.Lunarward_RuntimeEnv_SetDefaultRunner,
            PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Save,
            CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            Content = body,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary || combo.SelectedIndex < 0)
        {
            return;
        }
        RuntimeDefaults.DefaultRunnerId = runners[combo.SelectedIndex].Id;
        Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_SetDefaultRunnerDone, 4000);
    }


}
