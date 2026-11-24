// Lunarward：运行环境页的「方案」页（D85）——左列清单 + 右列详情 / 编辑 / 增强三态。
// 设计前提：列表用代码构建（Uno 的 ListView 在星号行 + ScrollViewer 里会抽风，D58 v2）；
// 详情里的每段都是手写 Border 卡片，没有 Uno Toolkit（本工程 <UnoFeatures> 不含 Toolkit，没有 SettingsCard）。
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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

    /// <summary>详情页「运行器」卡片里那个名字的 TextBlock：组件扫描完成后要就地更新（D87）。</summary>
    private TextBlock? _detailRunnerName;


    // ───────────────────────────── 左列清单 ─────────────────────────────


    private UIElement CreateProfileListRow(RuntimeProfile profile)
    {
        // R20：列表每行都递归算前缀体积会拖死 UI；体积只在「前缀」页后台算。
        PrefixManager.PrefixInspection inspection = PrefixManager.Inspect(EnvironmentResolver.EffectivePrefixPath(profile), computeSize: false);
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
        // R130：8px 的圆点对读屏是一片空白，把健康状态挂到它身上（旁边那行文字是视觉冗余）。
        AutomationProperties.SetName(statusDot, text);
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
    private async void Button_ProfileList_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: RuntimeProfile profile })
            {
                return;
            }
            // R91：切页签和点「返回」都会确认，唯独点左列别的方案不会——编到一半点一下就静默丢草稿。
            if (!await ConfirmLeaveEditorAsync())
            {
                return;
            }
            _selectedProfileId = profile.Id;
            UpdateProfileListSelection();
            ShowProfileDetail(profile);
            ShowDetailPane();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "switch profile failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_Failed);
        }
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
    private static Brush BrushOf(string token) => token switch
    {
        // D108：状态色一律走主题画刷，别再用 #RRGGBB（深色主题对比度差，且违反「禁止硬编码颜色」）。
        "Critical" => Application.Current.Resources["SystemFillColorCriticalBrush"] as Brush
                      ?? new SolidColorBrush(Microsoft.UI.Colors.IndianRed),
        "Caution" => Application.Current.Resources["SystemFillColorCautionBrush"] as Brush
                     ?? new SolidColorBrush(Microsoft.UI.Colors.DarkOrange),
        "Success" => Application.Current.Resources["SystemFillColorSuccessBrush"] as Brush
                     ?? new SolidColorBrush(Microsoft.UI.Colors.SeaGreen),
        _ => Application.Current.Resources["ControlFillColorSecondaryBrush"] as Brush
             ?? new SolidColorBrush(Microsoft.UI.Colors.DimGray),
    };


    // ───────────────────────────── 右栏三态 ─────────────────────────────


    /// <summary>详情（默认）。</summary>
    private void ShowDetailPane()
    {
        ScrollViewer_ProfileDetail.Visibility = Visibility.Visible;
        Grid_ProfileEditor.Visibility = Visibility.Collapsed;
        Grid_ProfileEnhance.Visibility = Visibility.Collapsed;
        UpdateContentWidth();
    }


    /// <summary>编辑（就地，不整页覆盖：左列还在，能看到「改成谁」）。</summary>
    private void ShowEditorPane()
    {
        ScrollViewer_ProfileDetail.Visibility = Visibility.Collapsed;
        Grid_ProfileEditor.Visibility = Visibility.Visible;
        Grid_ProfileEnhance.Visibility = Visibility.Collapsed;
        UpdateContentWidth();
    }


    /// <summary>增强（原「增强」页签，现在从方案详情点进来）。</summary>
    private void ShowEnhancePane()
    {
        ScrollViewer_ProfileDetail.Visibility = Visibility.Collapsed;
        Grid_ProfileEditor.Visibility = Visibility.Collapsed;
        Grid_ProfileEnhance.Visibility = Visibility.Visible;
        UpdateContentWidth();
    }


    private async void Button_EditorBack_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (await ConfirmLeaveEditorAsync())
            {
                CloseEditor();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "leave editor failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_Failed);
        }
    }


    private async void Button_EnhanceBack_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // R91：增强页也有未保存改动，返回同样要确认。
            if (!await ConfirmLeaveEditorAsync())
            {
                return;
            }
            ShowDetailPane();
            ShowProfileDetail(SelectedProfileOrNull());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "leave enhancements failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_Failed);
        }
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
        // R20：详情只关心「存在/健康/被谁用」，体积交给前缀页后台统计。
        PrefixManager.PrefixInspection inspection = PrefixManager.Inspect(prefixPath, computeSize: false);
        IReadOnlyList<GameBiz> bindings = ProfileBinding.Usages(profile.Id);

        StackPanel_ProfileDetail.Children.Add(CreateProfileHeader(profile, inspection, bindings, prefixPath));
        _detailRunnerName = null;
        StackPanel_ProfileDetail.Children.Add(CreateRunnerSection(profile));
        StackPanel_ProfileDetail.Children.Add(CreateGraphicsSection(profile, inspection, prefixPath));
        StackPanel_ProfileDetail.Children.Add(CreatePrefixSection(profile, inspection, prefixPath, bindings));
        StackPanel_ProfileDetail.Children.Add(CreateEnhanceSection(profile));
        StackPanel_ProfileDetail.Children.Add(CreatePreviewSection(profile));
        StackPanel_ProfileDetail.Children.Add(CreateHistorySection(profile));
    }


    /// <summary>顶部卡片：名字 + 健康灯 + 摘要 + 主次按钮（主：编辑并启动；次：⋯菜单）。</summary>
    private Border CreateProfileHeader(RuntimeProfile profile, PrefixManager.PrefixInspection inspection, IReadOnlyList<GameBiz> bindings, string prefixPath)
    {
        (string dot, string healthText) = DescribeHealth(profile, inspection);

        var body = new StackPanel { Spacing = 6 };
        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        titleRow.Children.Add(new TextBlock
        {
            Text = profile.Name,
            FontSize = 18,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
            TextWrapping = TextWrapping.Wrap,
        });
        if (string.Equals(RuntimeDefaults.DefaultProfileId, profile.Id, StringComparison.Ordinal))
        {
            // R68：默认方案必须可见——未绑定的游戏（新游戏、双击 exe）都会落到它身上。
            titleRow.Children.Add(new Border
            {
                Background = (Brush)Application.Current.Resources["ControlFillColorSecondaryBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(6, 1, 6, 1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = Lang.Lunarward_RuntimeEnv_DefaultProfileBadge, FontSize = 11 },
            });
        }
        body.Children.Add(titleRow);

        var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        var detailDot = new Ellipse
        {
            Width = 8,
            Height = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = BrushOf(dot),
        };
        AutomationProperties.SetName(detailDot, healthText);
        statusRow.Children.Add(detailDot);
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
                : $"{Lang.Lunarward_RuntimeEnv_BindingsLabel}：{string.Join("、", bindings.Select(x => x.ToGameName()))}",
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

        var editButton = new Button { Content = Lang.Lunarward_RuntimeEnv_Modify, Tag = profile };
        editButton.Click += Button_Edit_Click;
        buttons.Children.Add(editButton);

        // 低频动作进 ⋯ 菜单：一屏里六个同权重按钮谁都不突出（D85 诊断第 2 条）
        // R129：以前用 Segoe MDL2 的 \uE712 画「⋯」，Linux 上没有这个字体，Uno 只会画一个方框；
        // 直接用文本省略号，跟上游的视觉差异可忽略。
        var moreButton = new Button { Content = "⋯", FontSize = 14 };
        ToolTipService.SetToolTip(moreButton, Lang.Lunarward_RuntimeEnv_More);
        AutomationProperties.SetName(moreButton, Lang.Lunarward_RuntimeEnv_More);
        var moreFlyout = new MenuFlyout();
        moreFlyout.Items.Add(MenuItem(Lang.Lunarward_RuntimeEnv_SetDefaultProfile, profile, Button_SetDefaultProfile_Click));
        moreFlyout.Items.Add(MenuItem(Lang.Lunarward_RuntimeEnv_Clone, profile, Button_Clone_Click));
        moreFlyout.Items.Add(MenuItem(Lang.Lunarward_RuntimeEnv_TextView, profile, Button_TextView_Click));
        // D108：调试 Shell 只给开发构建（AGENTS 硬规则 6：危险入口不进发行版）；
        // 文本视图与克隆是常用动作，中间加分隔线，删除单独一组并标红。
        if (Starward.Features.Dev.DevSwitches.IsEnabled)
        {
            moreFlyout.Items.Add(MenuItem(Lang.Lunarward_RuntimeEnv_OpenShell, profile, Button_OpenShell_Click));
            moreFlyout.Items.Add(MenuDivider());
        }
        MenuFlyoutItem deleteItem = MenuItem(Lang.Lunarward_RuntimeEnv_Delete, profile, Button_Delete_Click);
        deleteItem.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        moreFlyout.Items.Add(deleteItem);
        moreButton.Flyout = moreFlyout;
        buttons.Children.Add(moreButton);

        body.Children.Add(buttons);
        return Card(body);
    }


    private void Button_SetDefaultProfile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not RuntimeProfile profile)
        {
            return;
        }
        RuntimeDefaults.DefaultProfileId = profile.Id;
        LoadProfiles();
        Helpers.InAppToast.MainWindow?.Success(string.Format(Lang.Lunarward_RuntimeEnv_DefaultProfileSet, profile.Name), null, 3000);
    }


    /// <summary>D111：深色主题下 MenuFlyoutSeparator 几乎看不见，用一条禁用的点线当分隔。</summary>
    private static MenuFlyoutItem MenuDivider()
    {
        var divider = new MenuFlyoutItem
        {
            Text = "·  ·  ·",
            IsEnabled = false,
            IsTabStop = false,
            FontSize = 11,
            Opacity = 0.45,
            Padding = new Thickness(12, 0, 12, 0),
            MinHeight = 12,
        };
        // R130：这条「点线」只是视觉分隔，读屏不该把它当菜单项念一遍「点 点 点」。
        // Uno 没有 IsInAccessibleTree 这个附加属性，改用空名字 + 不可聚焦（读屏会跳过无名项）。
        AutomationProperties.SetName(divider, string.Empty);
        return divider;
    }


    private static MenuFlyoutItem MenuItem(string text, object? tag, RoutedEventHandler handler)
    {
        var item = new MenuFlyoutItem { Text = text, Tag = tag };
        item.Click += handler;
        return item;
    }


    /// <summary>
    /// 详情页的运行器名字：优先用组件扫描结果里的 <see cref="Component.DisplayName"/>（带版本，
    /// 和编辑器下拉里那一行一字不差）；还没扫过或找不到（运行器被删 / 配置指向已不存在的路径）
    /// 才回落到配置里的旧名字。原来直接用 <c>profile.RunnerName</c>，于是
    /// `/opt/spritz-wine-tkg/bin/wine` 在详情页显示 `wine`、在下拉里显示「系统 Wine …」，
    /// 同一个文件两个名字（2026-11-18 实测，D87）。
    /// 只用缓存，不在这里触发扫描——扫描要 fork `wine --version`，不能在 UI 线程上做（L-127）。
    /// </summary>
    private static string ResolveRunnerDisplay(RuntimeProfile profile)
    {
        string fallback = string.IsNullOrWhiteSpace(profile.RunnerName)
            ? Lang.Lunarward_RuntimeEnv_NoUsableRunner
            : profile.RunnerName!;
        if (ComponentStore.TryGetCachedRunners(out IReadOnlyList<Component> runners))
        {
            Component? match = runners.FirstOrDefault(x => MatchesComponent(x, profile.RunnerPath, profile.RunnerId));
            if (match is not null)
            {
                return match.DisplayName;
            }
        }
        return fallback;
    }


    private Border CreateRunnerSection(RuntimeProfile profile)
    {
        var body = new StackPanel { Spacing = 4 };
        _detailRunnerName = new TextBlock
        {
            Text = ResolveRunnerDisplay(profile),
            TextWrapping = TextWrapping.Wrap,
        };
        body.Children.Add(_detailRunnerName);
        if (!string.IsNullOrWhiteSpace(profile.RunnerPath))
        {
            body.Children.Add(Hint(profile.RunnerPath!));
        }
        return Section(Lang.Lunarward_RuntimeEnv_RunnerSectionTitle, body);
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
                Foreground = BrushOf("Caution"),
                TextWrapping = TextWrapping.Wrap,
            });
            var syncButton = new Button { Content = Lang.Lunarward_RuntimeEnv_SyncGraphics, Tag = profile };
            syncButton.Click += Button_SyncGraphics_Click;
            actions.Children.Add(syncButton);
        }
        return actions.Children.Count > 0
            ? Section(Lang.Lunarward_RuntimeEnv_GraphicsSectionTitle, body, actions)
            : Section(Lang.Lunarward_RuntimeEnv_GraphicsSectionTitle, body);
    }


    private Border CreatePrefixSection(RuntimeProfile profile, PrefixManager.PrefixInspection inspection, string prefixPath, IReadOnlyList<GameBiz> bindings)
    {
        var body = new StackPanel { Spacing = 4 };
        var pathText = new TextBlock
        {
            Text = prefixPath,
            // R129：Linux 上没有 Consolas（`monospace` 也不是可用族名，见下 480 行附近的说明），
            // 指定不存在的族名在 Uno/Skia 下会让整段文字不渲染。等宽靠文本本身，不再点名族。

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
        // R25：上次成功启动的组合只写不读等于没记；这里给出时间并允许一键恢复。
        LastGoodEntry? lastGood = LastKnownGoodStore.Get(profile.Id);
        if (lastGood is not null)
        {
            body.Children.Add(new TextBlock
            {
                Text = string.Format(Lang.Lunarward_RuntimeEnv_LastGoodAt, lastGood.At.ToLocalTime().ToString("MM-dd HH:mm")),
                FontSize = 12,
                Opacity = 0.85,
                TextWrapping = TextWrapping.Wrap,
            });
            var restore = new Button { Content = Lang.Lunarward_RuntimeEnv_RestoreLastGood, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 4) };
            restore.Click += async (_, _) =>
            {
                try
                {
                    RuntimeProfile? fresh = RuntimeProfileStore.LoadAll(_logger).FirstOrDefault(x => x.Id == profile.Id);
                    if (fresh is null)
                    {
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(lastGood.RunnerId)) fresh.RunnerId = lastGood.RunnerId!;
                    if (!string.IsNullOrWhiteSpace(lastGood.DxvkId)) fresh.DxvkId = lastGood.DxvkId;
                    if (!string.IsNullOrWhiteSpace(lastGood.Vkd3dId)) fresh.Vkd3dId = lastGood.Vkd3dId;
                    if (!string.IsNullOrWhiteSpace(lastGood.RunnerPath)) fresh.RunnerPath = lastGood.RunnerPath;
                    if (RuntimeProfileStore.Save(fresh, _logger))
                    {
                        LoadProfiles();
                        ShowProfileDetail(fresh);
                        Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_RestoredLastGood, 3000);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Restore last good failed");
                }
                await Task.CompletedTask;
            };
            body.Children.Add(restore);
        }
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
        StackPanel_Chips.Children.Add(CreateChip(string.Format(Lang.Lunarward_RuntimeEnv_ChipBindings, ProfileBinding.BindingCount()), BindingsChipTag, Lang.Lunarward_RuntimeEnv_Profiles));
        StackPanel_Chips.Children.Add(CreateChip(string.Format(Lang.Lunarward_RuntimeEnv_ChipRunners, usableRunners, runners.Count), 1, Lang.Lunarward_RuntimeEnv_Runner));
        StackPanel_Chips.Children.Add(CreateChip(string.Format(Lang.Lunarward_RuntimeEnv_ChipGraphics, graphics.Count), 1, Lang.Lunarward_RuntimeEnv_Components_2));
        StackPanel_Chips.Children.Add(CreateChip($"{string.Format(Lang.Lunarward_RuntimeEnv_ChipPrefix, prefixes.Count)} · {FormatSize(totalBytes)}", 2, Lang.Lunarward_RuntimeEnv_Prefix));

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
        ToolTipService.SetToolTip(chip, string.Format(Lang.Lunarward_RuntimeEnv_ChipTip, tooltip));
        return chip;
    }


    /// <summary>D108：「已绑定 N 个游戏」点开后除了切到方案页，还要选中当前游戏绑定的那份方案，否则看起来没反应。</summary>
    private const int BindingsChipTag = 10;


    private async void Button_Chip_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is not FrameworkElement { Tag: int tag })
            {
                return;
            }
            if (tag == BindingsChipTag)
            {
                await ShowBindingsDialogAsync();
                return;
            }
            ShowTab(tag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "chip click failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_Failed);
        }
    }


    /// <summary>D111：「已绑定 N 个游戏」点开就是绑定表，不再是「看起来没反应」。</summary>
    private async Task ShowBindingsDialogAsync()
    {
        List<RuntimeProfile> profiles = RuntimeProfileStore.LoadAll(_logger).ToList();
        if (profiles.Count == 0)
        {
            return;
        }
        var body = new StackPanel { Spacing = 8 };
        body.Children.Add(new TextBlock { Text = Lang.Lunarward_RuntimeEnv_BindingsHint, FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap });
        var rows = new List<(GameBiz Biz, ComboBox Combo)>();
        foreach (GameBiz biz in GameBiz.AllGameBizs)
        {
            var combo = new ComboBox { MinWidth = 240 };
            combo.Items.Add(Lang.Lunarward_RuntimeEnv_BindingsUnbound);
            foreach (RuntimeProfile profile in profiles)
            {
                combo.Items.Add(profile.Name);
            }
            string? bound = ProfileBinding.Get(biz);
            int index = bound is null ? -1 : profiles.FindIndex(x => x.Id == bound);
            combo.SelectedIndex = index >= 0 ? index + 1 : 0;
            var label = new TextBlock
            {
                Text = biz.ToGameName(),
                MinWidth = 130,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
            };
            var line = new Grid { ColumnSpacing = 10 };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(combo, 1);
            line.Children.Add(label);
            line.Children.Add(combo);
            body.Children.Add(line);
            rows.Add((biz, combo));
        }
        var dialog = new ContentDialog
        {
            Title = Lang.Lunarward_RuntimeEnv_BindingsTitle,
            Content = new ScrollViewer { Content = body, MaxHeight = 520 },
            PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Save,
            CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };
        if (await Starward.Helpers.DialogGate.Show(dialog) != ContentDialogResult.Primary)
        {
            return;
        }
        int failures = 0;
        foreach ((GameBiz biz, ComboBox combo) in rows)
        {
            int index = combo.SelectedIndex;
            if (!ProfileBinding.Set(biz, index <= 0 ? null : profiles[index - 1].Id, _logger))
            {
                failures++;
            }
        }
        if (failures > 0)
        {
            Helpers.InAppToast.MainWindow?.Warning((string?)null,
                string.Format(Lang.Lunarward_RuntimeEnv_BindingsSaveFailed, failures), 8000);
        }
        _ = RefreshChipsAsync();
        LoadProfiles();
        Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_BindingsSaved, 3000);
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
      try
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
        if (await Starward.Helpers.DialogGate.Show(dialog) != ContentDialogResult.Primary || combo.SelectedIndex < 0)
        {
            return;
        }
        RuntimeDefaults.DefaultRunnerId = runners[combo.SelectedIndex].Id;
        Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_SetDefaultRunnerDone, 4000);
      }
      catch (Exception ex)
      {
          _logger.LogError(ex, "set default runner failed");
          Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_Failed);
      }
    }



    /// <summary>D110：详情页给只读「启动预览」——和真实启动同一套解析，配错了不必等启动才发现。</summary>
    private Border CreatePreviewSection(RuntimeProfile profile)
    {
        var body = new StackPanel { Spacing = 4 };
        var command = new TextBlock
        {
            FontSize = 12,
            TextWrapping = TextWrapping.NoWrap,
            IsTextSelectionEnabled = true,
            Opacity = 0.9,
        };
        // D111：长命令行不再靠换行，横向滚动 + 一键复制。
        var scroll = new ScrollViewer
        {
            Content = command,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Top,
        };
        body.Children.Add(scroll);
        body.Children.Add(new TextBlock
        {
            Text = Lang.Lunarward_RuntimeEnv_PreviewNote,
            FontSize = 12,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
        });
        try
        {
            // R79：详情区在 UI 线程上拼预览——只读缓存，冷缓存时用 profile 里的路径兜底。
            Component? runner = (ComponentStore.CachedRunners ?? Array.Empty<Component>())
                .FirstOrDefault(x => x.Id == profile.RunnerId);
            string runnerPath = !string.IsNullOrWhiteSpace(runner?.Path) ? runner!.Path : profile.RunnerPath;
            if (string.IsNullOrWhiteSpace(runnerPath))
            {
                command.Text = Lang.Lunarward_RuntimeEnv_PreviewNoRunner;
            }
            else
            {
                string prefix = EnvironmentResolver.EffectivePrefixPath(profile);
                var auto = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["WINEPREFIX"] = prefix,
                    ["WINEDEBUG"] = "-all",
                };
                LaunchRecipe recipe = LaunchRecipeBuilder.Build(profile, runnerPath, prefix, Lang.Lunarward_RuntimeEnv_PreviewGameExe, null, auto);
                var text = new System.Text.StringBuilder();
                text.Append(recipe.CommandLine);
                foreach (EnvOverride item in recipe.Overrides)
                {
                    text.Append('\n').Append(item.Key).Append('=').Append(string.IsNullOrEmpty(item.RecipeValue) ? item.AutoValue : item.RecipeValue);
                }
                command.Text = text.ToString();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "build launch preview failed");
            command.Text = ex.Message;
        }
        var copy = new Button { Content = Lang.Lunarward_RuntimeEnv_Copy };
        copy.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(command.Text))
            {
                Helpers.ClipboardHelper.SetText(command.Text);
            }
        };
        return Section(Lang.Lunarward_RuntimeEnv_LaunchPreview, body, copy);
    }

}
