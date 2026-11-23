// Lunarward：受管组件的「下载」与「导入归档」入口（D73 §2.3 / §13.8）。
//
// 单开一个 partial 文件的原因：这两个入口都是「弹窗 + 长任务 + 进度」，跟页面本身的
// 卡片/编辑器逻辑没关系，混在 RuntimeEnvironmentSetting.xaml.cs 里会长到读不动。
//
// 取消语义（与设计 §2.3 一致）：取消 = 暂停。ComponentDownloader 的分片是 .part.N 落盘的，
// 取消后不清，下次同一版本再点下载会接着下（Resumed）。界面必须把这点说清楚，
// 不要做成语义含糊的「取消」。
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Starward.Features.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Setting;


public sealed partial class RuntimeEnvironmentSetting
{
    /// <summary>下载源列表（默认源 + 用户覆盖，按 Rank 排序）。</summary>
    private static IReadOnlyList<ComponentSourceEntry> LoadSourceEntries() => ComponentSources.Load();


    /// <summary>把源的 kind 归一成 ComponentImporter 认的三种。</summary>
    private static string ImportKindOf(ComponentSourceEntry source) => source.Kind switch
    {
        "graphics" or "dxvk" or "vkd3d" => "graphics",
        "proton" => "wine",
        _ => "wine",
    };


    /// <summary>
    /// 「下载组件…」：选源 → 列版本 → 下载（进度/取消）→ 解压导入 → 刷新列表。
    /// </summary>
    private bool _downloadDialogOpen;

    /// <summary>下载弹窗里的一行（D110：勾选 + 行内进度 + 失败重试）。</summary>
    private sealed class DownloadRow
    {
        public required ComponentReleaseAsset Asset { get; init; }
        public required CheckBox Check { get; init; }
        public required ProgressBar Progress { get; init; }
        public required TextBlock RowStatus { get; init; }
        public required Button Retry { get; init; }
        public required StackPanel Holder { get; init; }
    }

    private async void Button_DownloadComponent_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadDialogOpen)
        {
            return;
        }
        _downloadDialogOpen = true;
        try
        {
            IReadOnlyList<ComponentSourceEntry> sources = LoadSourceEntries().Where(x => x.Enabled).ToList();
            if (sources.Count == 0)
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_NoEnabledSource);
                return;
            }

            var sourceBox = new ComboBox
            {
                ItemsSource = sources.Select(x => x.DisplayName).ToList(),
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var manageSources = new HyperlinkButton
            {
                Content = Lang.Lunarward_RuntimeEnv_ManageSources,
                Margin = new Thickness(12, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
            };
            // D110：源 + 管理入口竖排，避免窄弹窗里「管理下载源」被裁成「管理」。
            manageSources.HorizontalAlignment = HorizontalAlignment.Left;
            manageSources.Margin = new Thickness(0, 2, 0, 0);
            manageSources.Padding = new Thickness(0);
            var sourceRow = new StackPanel { Spacing = 2 };
            sourceRow.Children.Add(sourceBox);
            sourceRow.Children.Add(manageSources);

            var status = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.75,
                Text = Lang.Lunarward_RuntimeEnv_DownloadHint,
            };
            var listHeader = new TextBlock { Text = Lang.Lunarward_RuntimeEnv_Versions, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
            // D109 教训：ContentDialog 里的 ListBox 在 Uno/Skia 上点不中，改用 CheckBox 行自绘。
            var assetPanel = new StackPanel { Spacing = 2 };
            var assetScroll = new ScrollViewer
            {
                Content = assetPanel,
                Height = 280,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            var selectionText = new TextBlock { FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap };
            var pauseButton = new Button { Content = Lang.Lunarward_RuntimeEnv_Pause };
            var installButton = new Button { Content = Lang.Lunarward_RuntimeEnv_InstallSelected };
            var closeButton = new Button { Content = Lang.Lunarward_RuntimeEnv_Close };
            var buttonsRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            buttonsRow.Children.Add(pauseButton);
            buttonsRow.Children.Add(installButton);
            buttonsRow.Children.Add(closeButton);

            var panel = new StackPanel { Spacing = 8, MinWidth = 460 };
            panel.Children.Add(new TextBlock { Text = Lang.Lunarward_RuntimeEnv_Source, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            panel.Children.Add(sourceRow);
            panel.Children.Add(listHeader);
            panel.Children.Add(assetScroll);
            panel.Children.Add(selectionText);
            panel.Children.Add(status);
            panel.Children.Add(buttonsRow);

            var dialog = new ContentDialog
            {
                Title = Lang.Lunarward_RuntimeEnv_AddComponents,
                Content = new ScrollViewer { Content = panel, MaxHeight = 560 },
                DefaultButton = ContentDialogButton.None,
                XamlRoot = XamlRoot,
            };

            IReadOnlyList<ComponentReleaseAsset> assets = [];
            List<DownloadRow> rows = [];
            HashSet<string> installed = LoadInstalledVersions();
            CancellationTokenSource? cts = null;
            bool busy = false;

            void UpdateSummary()
            {
                int count = rows.Count(x => x.Check.IsChecked == true);
                long total = rows.Where(x => x.Check.IsChecked == true).Sum(x => x.Asset.Size);
                long free = 0;
                try
                {
                    free = Helpers.DriveHelper.GetDriveAvailableSpace(ComponentStore.ComponentsFolder);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "read free space failed");
                }
                if (count == 0)
                {
                    selectionText.Text = "";
                    installButton.IsEnabled = false;
                    return;
                }
                long need = total * 2 + 64L * 1024 * 1024;
                string text = string.Format(
                    Lang.Lunarward_RuntimeEnv_SummaryFormat,
                    count,
                    Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(total),
                    Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(free));
                if (free > 0 && free < need)
                {
                    text += "  ·  " + string.Format(Lang.Lunarward_RuntimeEnv_SummaryNeedMore, Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(need - free));
                    installButton.IsEnabled = false;
                }
                else
                {
                    installButton.IsEnabled = !busy;
                }
                selectionText.Text = text;
            }

            void ClearAssets()
            {
                assets = [];
                rows.Clear();
                assetPanel.Children.Clear();
                selectionText.Text = "";
                installButton.IsEnabled = false;
            }

            StackPanel CreateAssetRow(int index, ComponentReleaseAsset asset)
            {
                bool isInstalled = IsInstalled(asset, installed);
                var title = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(asset.Version) ? asset.Name : $"{asset.Name}    {asset.Version}",
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 470,
                };
                var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                titleRow.Children.Add(title);
                if (isInstalled)
                {
                    var badge = new Border
                    {
                        Background = (Brush)Application.Current.Resources["SystemFillColorSuccessBackgroundBrush"],
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(6, 1, 6, 1),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = Lang.Lunarward_RuntimeEnv_Installed,
                            FontSize = 11,
                            Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"],
                        },
                    };
                    titleRow.Children.Add(badge);
                }
                var meta = new TextBlock
                {
                    Text = $"{asset.ReleaseTag}  ·  {Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(asset.Size)}",
                    FontSize = 12,
                    Opacity = 0.7,
                };
                var content = new StackPanel { Spacing = 2 };
                content.Children.Add(titleRow);
                content.Children.Add(meta);

                var check = new CheckBox { Tag = index, Content = content, MinWidth = 500, HorizontalAlignment = HorizontalAlignment.Stretch };
                var progress = new ProgressBar { Visibility = Visibility.Collapsed, Minimum = 0, Maximum = 100, Value = 0, Height = 3 };
                var rowStatus = new TextBlock { FontSize = 12, Opacity = 0.75, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
                var retry = new Button { Content = Lang.Lunarward_RuntimeEnv_Retry, Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Left };
                var holder = new StackPanel { Spacing = 4 };
                holder.Children.Add(check);
                holder.Children.Add(progress);
                holder.Children.Add(rowStatus);
                holder.Children.Add(retry);

                var row = new DownloadRow
                {
                    Asset = asset,
                    Check = check,
                    Progress = progress,
                    RowStatus = rowStatus,
                    Retry = retry,
                    Holder = holder,
                };
                check.Checked += (_, _) => UpdateSummary();
                check.Unchecked += (_, _) => UpdateSummary();
                retry.Click += async (_, _) => await RunQueueAsync([index]);
                if (isInstalled)
                {
                    retry.Content = Lang.Lunarward_RuntimeEnv_Reinstall;
                }
                return holder;
            }

            async Task LoadAssetsAsync()
            {
                if (busy || sources.Count == 0)
                {
                    return;
                }
                int index = Math.Max(0, sourceBox.SelectedIndex);
                ComponentSourceEntry source = sources[index];
                ClearAssets();
                sourceBox.IsEnabled = false;
                installButton.IsEnabled = false;
                status.Text = string.Format(Lang.Lunarward_RuntimeEnv_ReadingVersions, source.DisplayName);
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                    assets = await ComponentSources.ListAssetsAsync(http, source, 10, _logger);
                    for (int i = 0; i < assets.Count; i++)
                    {
                        assetPanel.Children.Add(CreateAssetRow(i, assets[i]));
                    }
                    installed = LoadInstalledVersions();
                    if (assets.Count == 0)
                    {
                        status.Text = Lang.Lunarward_RuntimeEnv_SourceNoAssets;
                        installButton.IsEnabled = false;
                    }
                    else
                    {
                        status.Text = string.Format(Lang.Lunarward_RuntimeEnv_ArchivesFound, assets.Count);
                    }
                    UpdateSummary();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "list component assets failed");
                    status.Text = string.Format(Lang.Lunarward_RuntimeEnv_ReadFailed, ex.Message);
                }
                finally
                {
                    sourceBox.IsEnabled = true;
                }
            }

            async Task RunQueueAsync(IReadOnlyList<int> requested)
            {
                if (busy || assets.Count == 0)
                {
                    return;
                }
                List<int> queue = requested.Where(x => x >= 0 && x < assets.Count).Distinct().ToList();
                if (queue.Count == 0)
                {
                    return;
                }
                ComponentSourceEntry source = sources[sourceBox.SelectedIndex < 0 ? 0 : sourceBox.SelectedIndex];
                string kind = ImportKindOf(source);

                busy = true;
                cts = new CancellationTokenSource();
                CancellationToken token = cts.Token;
                pauseButton.IsEnabled = true;
                installButton.IsEnabled = false;
                sourceBox.IsEnabled = false;
                string downloadDir = Path.Combine(ComponentStore.ComponentsFolder, ".download");
                int okCount = 0;
                int done = 0;
                List<string> failed = new();
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                    foreach (int index in queue)
                    {
                        if (token.IsCancellationRequested)
                        {
                            break;
                        }
                        DownloadRow row = rows.First(x => ReferenceEquals(x.Asset, assets[index]));
                        row.Progress.Visibility = Visibility.Visible;
                        row.Progress.Value = 0;
                        row.RowStatus.Visibility = Visibility.Visible;
                        row.RowStatus.Text = queue.Count > 1 ? string.Format(Lang.Lunarward_RuntimeEnv_QueuePrefix2, ++done, queue.Count) : "";
                        row.Check.IsEnabled = false;
                        Directory.CreateDirectory(downloadDir);
                        try
                        {
                            var downloadProgress = new Progress<ComponentDownloadProgress>(p =>
                            {
                                string total = p.Total is > 0 ? Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(p.Total.Value) : Lang.Lunarward_RuntimeEnv_Unknown2;
                                row.RowStatus.Text = string.Format(
                                    Lang.Lunarward_RuntimeEnv_Downloading2,
                                    Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(p.Received),
                                    total,
                                    p.ActiveConnections,
                                    (p.BytesPerSecond / 1024.0 / 1024.0).ToString("F1"));
                                if (p.Total is > 0)
                                {
                                    row.Progress.Value = Math.Clamp(p.Received * 100.0 / p.Total.Value, 0, 100);
                                }
                            });
                            ComponentDownloadResult result = await ComponentDownloader.DownloadAsync(
                                http, new Uri(row.Asset.Url), downloadDir, row.Asset.Name, row.Asset.Sha256,
                                mirrorPrefix: source.MirrorPrefix, connections: 4,
                                logger: _logger, progress: downloadProgress, ct: token);

                            row.Progress.Value = 100;
                            row.RowStatus.Text = result.Resumed ? Lang.Lunarward_RuntimeEnv_DownloadedResumed : Lang.Lunarward_RuntimeEnv_Downloaded;
                            var extractProgress = new Progress<ArchiveProgress>(p =>
                            {
                                row.RowStatus.Text = string.Format(Lang.Lunarward_RuntimeEnv_Extracting2, p.Entries, Starward.Features.GameInstall.Sophon.InstallPlanner.FormatSize(p.Bytes));
                            });
                            ComponentManifest manifest = await ComponentImporter.ImportAsync(
                                result.FilePath, kind, sha256: result.Sha256, expectedSha256: row.Asset.Sha256,
                                sourceUrl: row.Asset.Url,
                                mirrorPrefix: source.MirrorPrefix, logger: _logger, ct: token,
                                progress: extractProgress);
                            try
                            {
                                File.Delete(result.FilePath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "delete downloaded archive failed: {Path}", result.FilePath);
                            }
                            ComponentStore.Invalidate();
                            LoadComponents();
                            okCount++;
                            row.RowStatus.Text = string.Format(Lang.Lunarward_RuntimeEnv_StageDone2, manifest.Name, manifest.Version);
                            row.Progress.Visibility = Visibility.Collapsed;
                            row.RowStatus.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_InstalledToast, manifest.Name, manifest.Version), 4000);
                        }
                        catch (OperationCanceledException)
                        {
                            row.RowStatus.Text = Lang.Lunarward_RuntimeEnv_PausedKeepParts;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "download component failed: {Name}", row.Asset.Name);
                            failed.Add(row.Asset.Name);
                            row.Progress.Visibility = Visibility.Collapsed;
                            row.RowStatus.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                            row.RowStatus.Text = FormatFriendlyError(ex);
                            row.Retry.Visibility = Visibility.Visible;
                        }
                        finally
                        {
                            row.Check.IsEnabled = true;
                        }
                    }
                    installed = LoadInstalledVersions();
                    string summary = okCount == queue.Count
                        ? string.Format(Lang.Lunarward_RuntimeEnv_QueueAllOk2, okCount, queue.Count)
                        : string.Format(Lang.Lunarward_RuntimeEnv_QueuePartial2, okCount, queue.Count);
                    status.Text = summary;
                }
                catch (OperationCanceledException)
                {
                    status.Text = Lang.Lunarward_RuntimeEnv_PausedKeepParts;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "download queue failed");
                    status.Text = FormatFriendlyError(ex);
                }
                finally
                {
                    busy = false;
                    pauseButton.IsEnabled = false;
                    sourceBox.IsEnabled = true;
                    cts?.Dispose();
                    cts = null;
                    UpdateSummary();
                }
            }

            sourceBox.SelectionChanged += async (_, _) =>
            {
                if (sourceBox.SelectedIndex >= 0 && !busy)
                {
                    await LoadAssetsAsync();
                }
            };
            manageSources.Click += async (_, _) =>
            {
                dialog.Hide();
                await ShowSourcesDialogAsync();
            };
            pauseButton.IsEnabled = false;
            pauseButton.Click += (_, _) => cts?.Cancel();
            closeButton.Click += (_, _) =>
            {
                cts?.Cancel();
                dialog.Hide();
            };
            installButton.Click += async (_, _) =>
            {
                List<int> queue = rows.Where(x => x.Check.IsChecked == true).Select(x => (int)x.Check.Tag!).ToList();
                if (queue.Count == 0)
                {
                    status.Text = Lang.Lunarward_RuntimeEnv_SelectVersionFirst;
                    return;
                }
                await RunQueueAsync(queue);
            };

            dialog.Opened += async (_, _) => await LoadAssetsAsync();
            dialog.Closing += (_, _) => cts?.Cancel();
            await dialog.ShowAsync();
            cts?.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "open download dialog failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_OpenDownloadWindowFailed);
        }
        finally
        {
            _downloadDialogOpen = false;
        }
    }

    private static HashSet<string> LoadInstalledVersions()
    {
        HashSet<string> set = new(StringComparer.OrdinalIgnoreCase);
        foreach (Component component in ComponentStore.GetGraphics())
        {
            if (!string.IsNullOrWhiteSpace(component.Version))
            {
                set.Add(component.Version.Trim());
            }
        }
        foreach (Component component in ComponentStore.GetRunners())
        {
            if (!string.IsNullOrWhiteSpace(component.Version))
            {
                set.Add(component.Version.Trim());
            }
        }
        return set;
    }

    private static bool IsInstalled(ComponentReleaseAsset asset, HashSet<string> installed)
    {
        if (string.IsNullOrWhiteSpace(asset.Version))
        {
            return false;
        }
        string version = asset.Version.Trim();
        foreach (string candidate in installed)
        {
            if (candidate.Contains(version, StringComparison.OrdinalIgnoreCase)
                || version.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// 「导入归档…」：选一个本地归档（.tar / .tar.gz / .tar.zst / .tar.xz / .zip），
    /// 问一下是运行器还是图形层，然后解压导入（可取消、有进度）。
    /// </summary>
    private async void Button_ImportArchive_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string? archive = await Helpers.FileDialogHelper.PickSingleFileAsync(XamlRoot);
            if (string.IsNullOrWhiteSpace(archive))
            {
                return;
            }
            if (!ArchiveExtractor.IsSupported(archive))
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null,
                    string.Format(Lang.Lunarward_RuntimeEnv_UnsupportedArchive, Path.GetFileName(archive), string.Join("、", ArchiveExtractor.SupportedExtensions)), 6000);
                return;
            }

            var kindBox = new ComboBox
            {
                ItemsSource = new List<string> { Lang.Lunarward_RuntimeEnv_RunnerSectionTitle, Lang.Lunarward_RuntimeEnv_GraphicsSectionTitle },
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var dialog = new ContentDialog
            {
                Title = string.Format(Lang.Lunarward_RuntimeEnv_ImportTitle, Path.GetFileName(archive)),
                Content = kindBox,
                PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Import,
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            string kind = kindBox.SelectedIndex == 1 ? "graphics" : "wine";

            Helpers.InAppToast.MainWindow?.Information((string?)null, Lang.Lunarward_RuntimeEnv_Extracting, 8000);
            var progress = new Progress<ArchiveProgress>(p =>
                _logger.LogDebug("[components] 解压进度 {Entries} 项 / {Bytes} 字节", p.Entries, p.Bytes));
            ComponentManifest manifest = await ComponentImporter.ImportAsync(
                archive, kind, logger: _logger, progress: progress);
            ComponentStore.Invalidate();
            LoadComponents();
            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_ImportedToast, manifest.Name, manifest.Version), 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "import archive failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_ImportArchiveFailed);
        }
    }


    // ─────────────────────────────────────────────────────────────────────────
    // P3 体验项：组件源编辑 / 镜像测速 / 下载暂存 / 前缀占用汇总
    // 都是「看得见的状态」，不改任何运行期行为；每个入口只做一件事。
    // ─────────────────────────────────────────────────────────────────────────


    /// <summary>
    /// 「组件源…」：编辑 components-sources.json 里用户覆盖的那一份（镜像前缀为主）。
    /// 只写用户覆盖，不动内置默认——默认源跟着程序版本走，用户改动独立成文件（D73 §2.3）。
    /// </summary>
    private sealed class SourceEditRow
    {
        public required ComponentSourceEntry Source { get; init; }
        public required CheckBox Enabled { get; init; }
        public required TextBox Mirror { get; init; }
        public required TextBlock MirrorError { get; init; }
        public required TextBlock ProbeText { get; init; }
        public required TextBlock CheckText { get; init; }
    }

    private async void Button_Sources_Click(object sender, RoutedEventArgs e) => await ShowSourcesDialogAsync();

    private async void Button_ManageSources_Click(object sender, RoutedEventArgs e) => await ShowSourcesDialogAsync();

    /// <summary>
    /// D110「管理下载源」：启用/停用、排序、镜像前缀校验、单行测速、源自检、恢复默认。
    /// 源文件仍然是 components-sources.json，这里只做可视化编辑（Rank 按当前顺序重排）。
    /// </summary>
    private async Task ShowSourcesDialogAsync()
    {
        try
        {
            List<SourceEditRow> rows = [];
            var list = new StackPanel { Spacing = 12 };
            var hint = new TextBlock
            {
                Text = string.Format(Lang.Lunarward_RuntimeEnv_SourcesHint, ComponentSources.FilePath),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.75,
            };
            var resetButton = new Button { Content = Lang.Lunarward_RuntimeEnv_ResetSources };
            var probeAllButton = new Button { Content = Lang.Lunarward_RuntimeEnv_ProbeAll };
            var actions = new Grid { ColumnSpacing = 8 };
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            actions.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(resetButton, 0);
            Grid.SetColumn(probeAllButton, 2);
            actions.Children.Add(resetButton);
            actions.Children.Add(probeAllButton);

            void Rebuild()
            {
                list.Children.Clear();
                for (int i = 0; i < rows.Count; i++)
                {
                    SourceEditRow entry = rows[i];
                    int index = i;

                    var enabled = entry.Enabled;
                    var name = new TextBlock
                    {
                        Text = entry.Source.DisplayName,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };
                    var kind = new TextBlock
                    {
                        Text = entry.Source.Kind == "wine" ? Lang.Lunarward_RuntimeEnv_Runner : Lang.Lunarward_RuntimeEnv_Graphics,
                        FontSize = 11,
                        Opacity = 0.7,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var up = new Button { Content = "▲", IsEnabled = index > 0, Padding = new Thickness(8, 2, 8, 2) };
                    var down = new Button { Content = "▼", IsEnabled = index < rows.Count - 1, Padding = new Thickness(8, 2, 8, 2) };
                    var probe = new Button { Content = Lang.Lunarward_RuntimeEnv_Probe, Padding = new Thickness(8, 2, 8, 2) };
                    var check = new Button { Content = Lang.Lunarward_RuntimeEnv_SelfCheck, Padding = new Thickness(8, 2, 8, 2) };

                    // D110：三行布局（标题 / 镜像 / 状态），窄弹窗里不再把测速与自检按钮挤出可视区。
                    var titleLine = new Grid { ColumnSpacing = 8 };
                    titleLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    titleLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    titleLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    titleLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    titleLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    Grid.SetColumn(enabled, 0);
                    Grid.SetColumn(name, 1);
                    Grid.SetColumn(kind, 2);
                    Grid.SetColumn(up, 3);
                    Grid.SetColumn(down, 4);
                    titleLine.Children.Add(enabled);
                    titleLine.Children.Add(name);
                    titleLine.Children.Add(kind);
                    titleLine.Children.Add(up);
                    titleLine.Children.Add(down);

                    var toolLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    toolLine.Children.Add(probe);
                    toolLine.Children.Add(check);
                    toolLine.Children.Add(entry.ProbeText);
                    toolLine.Children.Add(entry.CheckText);

                    var mirrorLine = new Grid { ColumnSpacing = 8 };
                    mirrorLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    mirrorLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    Grid.SetColumn(entry.Mirror, 0);
                    Grid.SetColumn(entry.MirrorError, 1);
                    mirrorLine.Children.Add(entry.Mirror);
                    mirrorLine.Children.Add(entry.MirrorError);

                    var block = new StackPanel { Spacing = 4 };
                    block.Children.Add(titleLine);
                    block.Children.Add(mirrorLine);
                    block.Children.Add(toolLine);
                    list.Children.Add(block);

                    up.Click += (_, _) =>
                    {
                        (rows[index], rows[index - 1]) = (rows[index - 1], rows[index]);
                        Rebuild();
                    };
                    down.Click += (_, _) =>
                    {
                        (rows[index], rows[index + 1]) = (rows[index + 1], rows[index]);
                        Rebuild();
                    };
                    probe.Click += async (_, _) =>
                    {
                        probe.IsEnabled = false;
                        entry.ProbeText.Text = "…";
                        try
                        {
                            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                            ComponentSourceEntry probeSource = ToEntry(entry, rows.IndexOf(entry));
                            ComponentSourceProbe result = await ComponentSources.ProbeAsync(http, probeSource);
                            entry.ProbeText.Text = result.Text;
                            entry.ProbeText.Foreground = (Brush)Application.Current.Resources[
                                result.Ok ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush"];
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "probe source failed");
                            entry.ProbeText.Text = Lang.Lunarward_RuntimeEnv_ProbeFailedShort;
                        }
                        finally
                        {
                            probe.IsEnabled = true;
                        }
                    };
                    check.Click += async (_, _) =>
                    {
                        check.IsEnabled = false;
                        entry.CheckText.Text = "…";
                        try
                        {
                            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
                            IReadOnlyList<ComponentReleaseAsset> found = await ComponentSources.ListAssetsAsync(
                                http, ToEntry(entry, rows.IndexOf(entry)), 3, _logger);
                            entry.CheckText.Text = found.Count == 0
                                ? Lang.Lunarward_RuntimeEnv_SelfCheckEmpty
                                : string.Format(Lang.Lunarward_RuntimeEnv_SelfCheckOk, found.Count);
                            entry.CheckText.Foreground = (Brush)Application.Current.Resources[
                                found.Count == 0 ? "SystemFillColorCautionBrush" : "SystemFillColorSuccessBrush"];
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "self check source failed");
                            entry.CheckText.Text = FormatFriendlyError(ex);
                            entry.CheckText.Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
                        }
                        finally
                        {
                            check.IsEnabled = true;
                        }
                    };
                }
            }

            ComponentSourceEntry ToEntry(SourceEditRow row, int index) => new()
            {
                Id = row.Source.Id,
                DisplayName = row.Source.DisplayName,
                Kind = row.Source.Kind,
                Owner = row.Source.Owner,
                Repo = row.Source.Repo,
                AssetPattern = row.Source.AssetPattern,
                MirrorPrefix = row.Mirror.Text?.Trim() ?? "",
                Rank = (index + 1) * 10,
                Enabled = row.Enabled.IsChecked == true,
            };

            void LoadRows()
            {
            rows.Clear();
            foreach (ComponentSourceEntry source in LoadSourceEntries())
            {
                var mirror = new TextBox
                {
                    Text = source.MirrorPrefix,
                    PlaceholderText = Lang.Lunarward_RuntimeEnv_MirrorPlaceholder,
                };
                var mirrorError = new TextBlock
                {
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                    VerticalAlignment = VerticalAlignment.Center,
                    IsHitTestVisible = false,
                };
                var probeText = new TextBlock { FontSize = 12, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center };
                var checkText = new TextBlock { FontSize = 12, Opacity = 0.75, VerticalAlignment = VerticalAlignment.Center };
                var enabled = new CheckBox { IsChecked = source.Enabled, VerticalAlignment = VerticalAlignment.Center };
                var row = new SourceEditRow
                {
                    Source = source,
                    Enabled = enabled,
                    Mirror = mirror,
                    MirrorError = mirrorError,
                    ProbeText = probeText,
                    CheckText = checkText,
                };
                mirror.TextChanged += (_, _) =>
                {
                    bool valid = ComponentSources.IsValidMirrorPrefix(mirror.Text);
                    mirrorError.Text = valid ? "" : Lang.Lunarward_RuntimeEnv_MirrorInvalid;
                };
                rows.Add(row);
            }
            }

            probeAllButton.Click += async (_, _) =>
            {
                probeAllButton.IsEnabled = false;
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                    foreach (SourceEditRow row in rows)
                    {
                        row.ProbeText.Text = "…";
                        try
                        {
                            ComponentSourceProbe result = await ComponentSources.ProbeAsync(http, ToEntry(row, rows.IndexOf(row)));
                            row.ProbeText.Text = result.Text;
                            row.ProbeText.Foreground = (Brush)Application.Current.Resources[
                                result.Ok ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush"];
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "probe source failed");
                            row.ProbeText.Text = Lang.Lunarward_RuntimeEnv_ProbeFailedShort;
                        }
                    }
                }
                finally
                {
                    probeAllButton.IsEnabled = true;
                }
            };
            resetButton.Click += async (_, _) =>
            {
                ContentDialogResult confirm = await new ContentDialog
                {
                    Title = Lang.Lunarward_RuntimeEnv_ResetSources,
                    Content = Lang.Lunarward_RuntimeEnv_ResetSourcesHint,
                    PrimaryButtonText = Lang.Lunarward_RuntimeEnv_ResetSources,
                    CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot,
                }.ShowAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    ComponentSources.ResetUserEntries();
                    Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_SourcesResetDone, 4000);
                    LoadRows();
                    Rebuild();
                }
            };

            // D110：固定宽度，避免长源名/长镜像把行内按钮挤出可视区。
            var page = new StackPanel { Spacing = 12, Width = 470 };
            page.Children.Add(hint);
            page.Children.Add(actions);
            page.Children.Add(list);

            var dialog = new ContentDialog
            {
                Title = Lang.Lunarward_RuntimeEnv_ManageSources,
                Content = new ScrollViewer { Content = page, MaxHeight = 560 },
                PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Save,
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };

            LoadRows();
            Rebuild();
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            List<ComponentSourceEntry> result = rows.Select((row, index) => ToEntry(row, index)).ToList();
            if (result.Any(x => !ComponentSources.IsValidMirrorPrefix(x.MirrorPrefix)))
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_MirrorInvalid);
                return;
            }
            ComponentSources.SaveUserEntries(result);
            Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_SourcesSaved, 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "edit component sources failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_SourcesSaveFailed);
        }
    }


    /// <summary>
    /// 「镜像测速」：每个源发一个 HEAD，报首字节耗时。只给数字，不替用户下结论——
    /// 「哪个源快」随网络和时间变化，程序排序反而会误导（P3）。
    /// </summary>
    private async void Button_SourceSpeed_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Helpers.InAppToast.MainWindow?.Information((string?)null, Lang.Lunarward_RuntimeEnv_Probing, 6000);
            IReadOnlyList<ComponentSourceProbe> probes;
            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) })
            {
                probes = await ComponentSources.ProbeAllAsync(http);
            }
            var panel = new StackPanel { Spacing = 8, MinWidth = 540 };
            foreach (ComponentSourceProbe probe in probes)
            {
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock
                {
                    Text = probe.DisplayName,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                };
                ToolTipService.SetToolTip(name, probe.Url);
                row.Children.Add(name);
                var value = new TextBlock
                {
                    Text = probe.Text,
                    FontSize = 12,
                    Opacity = probe.Ok ? 1 : 0.7,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                if (probe.Ok)
                {
                    value.Foreground = (Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
                }
                Grid.SetColumn(value, 1);
                row.Children.Add(value);
                panel.Children.Add(row);
            }
            panel.Children.Add(new TextBlock
            {
                Text = Lang.Lunarward_RuntimeEnv_MirrorNotice,
                FontSize = 12,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
            });
            await new ContentDialog
            {
                Title = Lang.Lunarward_RuntimeEnv_ProbeTitle,
                Content = new ScrollViewer { Content = panel, MaxHeight = 480 },
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Close2,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "probe component sources failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_ProbeFailed);
        }
    }


    /// <summary>
    /// 「下载暂存」：列出 <c>.download/</c> 里已下的分片（取消 = 暂停，分片留着续传），
    /// 可以按归档整组删掉。这里只读文件系统，不看进度状态（P3）。
    /// </summary>
    private async void Button_Staging_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string dir = Path.Combine(ComponentStore.ComponentsFolder, ".download");
            List<(string Key, long Bytes, int Parts, string[] Files)> groups = await Task.Run(() => LoadStagingGroups(dir));
            var panel = new StackPanel { Spacing = 8, MinWidth = 560 };
            if (groups.Count == 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = Lang.Lunarward_RuntimeEnv_NoStagingFiles,
                    TextWrapping = TextWrapping.Wrap,
                    Opacity = 0.75,
                });
            }
            else
            {
                long total = groups.Sum(x => x.Bytes);
                panel.Children.Add(new TextBlock
                {
                    Text = string.Format(Lang.Lunarward_RuntimeEnv_StagingSummary, groups.Count, FormatSize(total), dir),
                    FontSize = 12,
                    Opacity = 0.7,
                    TextWrapping = TextWrapping.Wrap,
                });
                foreach ((string key, long bytes, int parts, string[] files) in groups)
                {
                    var row = new Grid { ColumnSpacing = 12 };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var name = new TextBlock
                    {
                        Text = key,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    row.Children.Add(name);
                    var size = new TextBlock
                    {
                        Text = string.Format(Lang.Lunarward_RuntimeEnv_StagingPart, FormatSize(bytes), parts),
                        FontSize = 12,
                        Opacity = 0.7,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    Grid.SetColumn(size, 1);
                    row.Children.Add(size);
                    var delete = new Button { Content = Lang.Lunarward_RuntimeEnv_Delete, Tag = files, VerticalAlignment = VerticalAlignment.Center };
                    delete.Click += async (_, _) =>
                    {
                        try
                        {
                            await Task.Run(() =>
                            {
                                foreach (string file in files)
                                {
                                    try
                                    {
                                        File.Delete(file);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogWarning(ex, "delete staging file failed: {Path}", file);
                                    }
                                }
                            });
                            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_DeletedStaging, key), 3000);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "delete staging files failed");
                            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_DeleteStagingFailed);
                        }
                    };
                    Grid.SetColumn(delete, 2);
                    row.Children.Add(delete);
                    panel.Children.Add(row);
                }
            }
            await new ContentDialog
            {
                Title = Lang.Lunarward_RuntimeEnv_StagingTitle,
                Content = new ScrollViewer { Content = panel, MaxHeight = 480 },
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Close2,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "list staging files failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_ReadStagingFailed);
        }
    }


    private static List<(string Key, long Bytes, int Parts, string[] Files)> LoadStagingGroups(string dir)
    {
        List<(string Key, long Bytes, int Parts, string[] Files)> groups = new();
        if (!Directory.Exists(dir))
        {
            return groups;
        }
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(dir))
        {
            string name = Path.GetFileName(file);
            int index = name.LastIndexOf(".part.", StringComparison.Ordinal);
            string key = index > 0 ? name[..index] : name;
            if (!map.TryGetValue(key, out List<string>? list))
            {
                list = new List<string>();
                map[key] = list;
            }
            list.Add(file);
        }
        foreach ((string key, List<string> files) in map)
        {
            long bytes = 0;
            foreach (string file in files)
            {
                try
                {
                    bytes += new FileInfo(file).Length;
                }
                catch (Exception)
                {
                    // 刚被删掉/正在写的文件：跳过，不影响汇总
                }
            }
            groups.Add((key, bytes, files.Count, files.ToArray()));
        }
        return groups.OrderByDescending(x => x.Bytes).ToList();
    }


    /// <summary>
    /// 「占用汇总」：所有已知前缀的体积合计（含外部前缀），按大小排序。
    /// 体积计算会扫盘，放后台线程跑；PrefixManager 自带 10 分钟缓存（L-62 的代价换来的）。
    /// </summary>
    private async void Button_PrefixUsage_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Helpers.InAppToast.MainWindow?.Information((string?)null, Lang.Lunarward_RuntimeEnv_CollectingUsage, 6000);
            List<Features.Runtime.PrefixManager.PrefixInspection> items = await Task.Run(() =>
                Features.Runtime.PrefixManager
                    .EnumerateKnownPrefixes(Features.Runtime.RuntimeProfileStore.LoadAll(_logger), computeSize: true)
                    .ToList());
            var panel = new StackPanel { Spacing = 6, MinWidth = 560 };
            long total = items.Sum(x => Math.Max(0, x.SizeBytes));
            panel.Children.Add(new TextBlock
            {
                Text = string.Format(Lang.Lunarward_RuntimeEnv_PrefixSummaryChip, items.Count, FormatSize(total)),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });
            foreach (Features.Runtime.PrefixManager.PrefixInspection item in items.OrderByDescending(x => x.SizeBytes))
            {
                var row = new Grid { ColumnSpacing = 12 };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var path = new TextBlock
                {
                    Text = item.Path,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                ToolTipService.SetToolTip(path, item.Path);
                row.Children.Add(path);
                var size = new TextBlock
                {
                    Text = item.Exists ? FormatSize(item.SizeBytes) : Lang.Lunarward_RuntimeEnv_Missing,
                    FontSize = 12,
                    Opacity = 0.7,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(size, 1);
                row.Children.Add(size);
                panel.Children.Add(row);
            }
            panel.Children.Add(new TextBlock
            {
                Text = Lang.Lunarward_RuntimeEnv_UsageNotice,
                FontSize = 12,
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
            });
            await new ContentDialog
            {
                Title = Lang.Lunarward_RuntimeEnv_UsageTitle,
                Content = new ScrollViewer { Content = panel, MaxHeight = 480 },
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Close2,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            }.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "summarize prefix usage failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_StatsFailPrefix);
        }
    }

    /// <summary>D110：错误文案不把 components-sources.json / AssetPattern 这类内部术语甩给用户。</summary>
    private static string FormatFriendlyError(Exception ex)
    {
        string message = ex.Message;
        if (message.Contains("AssetPattern", StringComparison.OrdinalIgnoreCase)
            || message.Contains("components-sources", StringComparison.OrdinalIgnoreCase))
        {
            return Lang.Lunarward_RuntimeEnv_SourceNoAssets;
        }
        return message;
    }
}
