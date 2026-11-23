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
using System.Text.RegularExpressions;
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
                // D111：空态就说清楚下一步能做什么，别只弹一条 toast。
                ContentDialogResult empty = await new ContentDialog
                {
                    Title = Lang.Lunarward_RuntimeEnv_AddComponents,
                    Content = Lang.Lunarward_RuntimeEnv_NoEnabledSource,
                    PrimaryButtonText = Lang.Lunarward_RuntimeEnv_ManageSources,
                    CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot,
                }.ShowAsync();
                if (empty == ContentDialogResult.Primary)
                {
                    await ShowSourcesDialogAsync();
                }
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
                    ToolTipService.SetToolTip(badge, Lang.Lunarward_RuntimeEnv_InstalledBadgeTip);
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
    /// D111「管理下载源」：真正的增删改查。
    /// 自定义源可新增/删除；内置源删除时写墓碑（Removed），避免下次 Load 又从 Defaults 合并回来。
    /// </summary>
    private sealed class SourceDraft
    {
        public string Id { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Kind { get; set; } = "wine";
        public string Owner { get; set; } = "";
        public string Repo { get; set; } = "";
        public string Pattern { get; set; } = "";
        public string Mirror { get; set; } = "";
        public bool Enabled { get; set; } = true;
    }


    private async void Button_Sources_Click(object sender, RoutedEventArgs e) => await ShowSourcesDialogAsync();

    private async void Button_ManageSources_Click(object sender, RoutedEventArgs e)
    {
        await ShowSourcesDialogAsync();
        // D114 修订 3：源管理里可能增删/启停源，回来把顶部摘要刷新一下。
        UpdateSourcesSummary();
    }


    private async Task ShowSourcesDialogAsync()
    {
        try
        {
            List<SourceDraft> drafts = LoadSourceEntries().Select(ToDraft).ToList();
            var probeState = new Dictionary<string, (string Text, bool Ok)>();
            var checkState = new Dictionary<string, (string Text, bool Ok)>();
            var list = new StackPanel { Spacing = 10 };
            var summary = new TextBlock { FontSize = 12, Opacity = 0.7 };

            var search = new TextBox
            {
                PlaceholderText = Lang.Lunarward_RuntimeEnv_SourceSearch,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var addButton = new Button { Content = Lang.Lunarward_RuntimeEnv_AddSource, VerticalAlignment = VerticalAlignment.Center };
            var probeAll = new Button { Content = Lang.Lunarward_RuntimeEnv_ProbeAll, VerticalAlignment = VerticalAlignment.Center };
            var reset = new Button { Content = Lang.Lunarward_RuntimeEnv_ResetSources, VerticalAlignment = VerticalAlignment.Center };

            // D111：搜索框独占一行，三个按钮另起一行——挤在一行时右侧按钮会被弹窗裁掉。
            var toolbarButtons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            toolbarButtons.Children.Add(probeAll);
            toolbarButtons.Children.Add(reset);
            toolbarButtons.Children.Add(addButton);
            var toolbar = new StackPanel { Spacing = 8 };
            toolbar.Children.Add(search);
            toolbar.Children.Add(toolbarButtons);

            var hint = new TextBlock
            {
                Text = string.Format(Lang.Lunarward_RuntimeEnv_SourcesHint, ComponentSources.FilePath),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.75,
            };
            var tokenHint = new TextBlock
            {
                Text = Lang.Lunarward_RuntimeEnv_SourcesTokenHint,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.65,
            };

            Brush Res(string key) => (Brush)Application.Current.Resources[key];

            IEnumerable<SourceDraft> Filtered()
            {
                string q = (search.Text ?? "").Trim();
                if (q.Length == 0)
                {
                    return drafts;
                }
                return drafts.Where(x =>
                    x.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Owner.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Repo.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || x.Kind.Contains(q, StringComparison.OrdinalIgnoreCase));
            }

            void Rebuild()
            {
                list.Children.Clear();
                List<SourceDraft> visible = Filtered().ToList();
                summary.Text = string.Format(Lang.Lunarward_RuntimeEnv_SourcesCount, drafts.Count);
                if (visible.Count == 0)
                {
                    list.Children.Add(new TextBlock
                    {
                        Text = Lang.Lunarward_RuntimeEnv_SourcesFilterEmpty,
                        Opacity = 0.7,
                        FontSize = 12,
                        Margin = new Thickness(4, 8, 0, 0),
                    });
                    return;
                }

                foreach (SourceDraft draft in visible)
                {
                    SourceDraft current = draft;
                    string id = draft.Id;
                    // WinUI CheckBox 默认 MinWidth=120，会把名称推到半个弹窗之外（用户报「左边一大块空白」）。
                    var enabled = new CheckBox { IsChecked = draft.Enabled, VerticalAlignment = VerticalAlignment.Center, MinWidth = 0 };
                    var name = new TextBlock
                    {
                        Text = draft.DisplayName,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    var meta = new TextBlock
                    {
                        Text = $"{draft.Owner}/{draft.Repo} · " + (draft.Kind == "wine" ? Lang.Lunarward_RuntimeEnv_Runner : Lang.Lunarward_RuntimeEnv_Graphics),
                        FontSize = 11,
                        Opacity = 0.65,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    };
                    var edit = new Button { Content = Lang.Lunarward_RuntimeEnv_EditSource, Padding = new Thickness(8, 2, 8, 2) };
                    var remove = new Button { Content = Lang.Lunarward_RuntimeEnv_DeleteSource, Padding = new Thickness(8, 2, 8, 2) };
                    var probe = new Button { Content = Lang.Lunarward_RuntimeEnv_Probe, Padding = new Thickness(8, 2, 8, 2) };
                    var check = new Button { Content = Lang.Lunarward_RuntimeEnv_SelfCheck, Padding = new Thickness(8, 2, 8, 2) };

                    // 名称一行、操作一行：四个按钮放同一行会把「删除」挤出弹窗（实测裁掉）。
                    var titleLine = new Grid { ColumnSpacing = 8 };
                    titleLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    titleLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    Grid.SetColumn(enabled, 0);
                    Grid.SetColumn(name, 1);
                    titleLine.Children.Add(enabled);
                    titleLine.Children.Add(name);

                    var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(28, 0, 0, 0) };
                    actions.Children.Add(probe);
                    actions.Children.Add(check);
                    actions.Children.Add(edit);
                    actions.Children.Add(remove);

                    var statuses = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    if (probeState.TryGetValue(id, out var p))
                    {
                        statuses.Children.Add(new TextBlock
                        {
                            Text = p.Text,
                            FontSize = 12,
                            Foreground = Res(p.Ok ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush"),
                        });
                    }
                    if (checkState.TryGetValue(id, out var c))
                    {
                        statuses.Children.Add(new TextBlock
                        {
                            Text = c.Text,
                            FontSize = 12,
                            Foreground = Res(c.Ok ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush"),
                        });
                    }

                    var block = new StackPanel { Spacing = 3 };
                    block.Children.Add(titleLine);
                    block.Children.Add(meta);
                    block.Children.Add(actions);
                    block.Children.Add(statuses);
                    list.Children.Add(block);

                    enabled.Click += (_, _) => current.Enabled = enabled.IsChecked == true;
                    probe.Click += async (_, _) =>
                    {
                        probe.IsEnabled = false;
                        try
                        {
                            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                            ComponentSourceProbe result = await ComponentSources.ProbeAsync(http, ToEntry(current, drafts.IndexOf(current)));
                            probeState[id] = (result.Text, result.Ok);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "probe source failed");
                            probeState[id] = (Lang.Lunarward_RuntimeEnv_ProbeFailedShort, false);
                        }
                        finally
                        {
                            probe.IsEnabled = true;
                        }
                        Rebuild();
                    };
                    check.Click += async (_, _) =>
                    {
                        check.IsEnabled = false;
                        try
                        {
                            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(1) };
                            IReadOnlyList<ComponentReleaseAsset> found = await ComponentSources.ListAssetsAsync(
                                http, ToEntry(current, drafts.IndexOf(current)), 3, _logger);
                            checkState[id] = found.Count == 0
                                ? (Lang.Lunarward_RuntimeEnv_SelfCheckEmpty, false)
                                : (string.Format(Lang.Lunarward_RuntimeEnv_SelfCheckOk, found.Count), true);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "self check source failed");
                            checkState[id] = (FormatFriendlyError(ex), false);
                        }
                        finally
                        {
                            check.IsEnabled = true;
                        }
                        Rebuild();
                    };
                    edit.Click += async (_, _) => await OpenSourceEditorAsync(current, isNew: false);
                    remove.Click += async (_, _) => await RemoveSourceAsync(current);
                }
            }

            async Task RemoveSourceAsync(SourceDraft draft)
            {
                ContentDialogResult confirm = await new ContentDialog
                {
                    Title = Lang.Lunarward_RuntimeEnv_DeleteSource,
                    Content = string.Format(Lang.Lunarward_RuntimeEnv_DeleteSourceConfirm, draft.DisplayName),
                    PrimaryButtonText = Lang.Lunarward_RuntimeEnv_DeleteSource,
                    CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot,
                }.ShowAsync();
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
                drafts.Remove(draft);
                probeState.Remove(draft.Id);
                checkState.Remove(draft.Id);
                Rebuild();
            }

            async Task OpenSourceEditorAsync(SourceDraft draft, bool isNew)
            {
                var nameBox = new TextBox { Text = draft.DisplayName };
                var kindBox = new ComboBox();
                kindBox.Items.Add(Lang.Lunarward_RuntimeEnv_Runner);
                kindBox.Items.Add(Lang.Lunarward_RuntimeEnv_Graphics);
                kindBox.SelectedIndex = draft.Kind == "graphics" ? 1 : 0;
                var ownerBox = new TextBox { Text = draft.Owner, PlaceholderText = "Kron4ek" };
                var repoBox = new TextBox { Text = draft.Repo, PlaceholderText = "Wine-Builds" };
                var patternBox = new TextBox { Text = draft.Pattern, PlaceholderText = @"wine-[\d.]+-(amd64|x86_64)\.tar\.xz$" };
                var mirrorBox = new TextBox { Text = draft.Mirror, PlaceholderText = Lang.Lunarward_RuntimeEnv_MirrorPlaceholder };
                var enabledBox = new CheckBox { Content = Lang.Lunarward_RuntimeEnv_Enabled, IsChecked = draft.Enabled };
                var error = new TextBlock
                {
                    FontSize = 12,
                    Foreground = Res("SystemFillColorCriticalBrush"),
                    TextWrapping = TextWrapping.Wrap,
                };

                static Grid FormRow(string label, FrameworkElement control)
                {
                    var grid = new Grid { ColumnSpacing = 10 };
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(96) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    var text = new TextBlock { Text = label, FontSize = 12, Opacity = 0.8, VerticalAlignment = VerticalAlignment.Center };
                    control.HorizontalAlignment = HorizontalAlignment.Stretch;
                    Grid.SetColumn(control, 1);
                    grid.Children.Add(text);
                    grid.Children.Add(control);
                    return grid;
                }

                var form = new StackPanel { Spacing = 8 };
                form.Children.Add(FormRow(Lang.Lunarward_RuntimeEnv_Name, nameBox));
                form.Children.Add(FormRow(Lang.Lunarward_RuntimeEnv_SourceKindLabel, kindBox));
                form.Children.Add(FormRow(Lang.Lunarward_RuntimeEnv_SourceOwnerLabel, ownerBox));
                form.Children.Add(FormRow(Lang.Lunarward_RuntimeEnv_SourceRepoLabel, repoBox));
                form.Children.Add(FormRow(Lang.Lunarward_RuntimeEnv_SourcePatternLabel, patternBox));
                form.Children.Add(FormRow("Mirror", mirrorBox));
                form.Children.Add(enabledBox);
                form.Children.Add(error);

                var dialog = new ContentDialog
                {
                    Title = isNew ? Lang.Lunarward_RuntimeEnv_AddSource : Lang.Lunarward_RuntimeEnv_EditSource,
                    Content = form,
                    PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Save,
                    CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = XamlRoot,
                };
                dialog.PrimaryButtonClick += (_, args) =>
                {
                    string name = (nameBox.Text ?? "").Trim();
                    string owner = (ownerBox.Text ?? "").Trim();
                    string repo = (repoBox.Text ?? "").Trim();
                    string pattern = (patternBox.Text ?? "").Trim();
                    string mirror = (mirrorBox.Text ?? "").Trim();
                    string? problem = null;
                    if (name.Length == 0)
                    {
                        problem = Lang.Lunarward_RuntimeEnv_SourceNameRequired;
                    }
                    else if (drafts.Any(x => !ReferenceEquals(x, draft) && string.Equals(x.DisplayName, name, StringComparison.OrdinalIgnoreCase)))
                    {
                        problem = Lang.Lunarward_RuntimeEnv_SourceNameDuplicate;
                    }
                    else if (!ComponentSources.IsValidRepoPart(owner) || !ComponentSources.IsValidRepoPart(repo))
                    {
                        problem = Lang.Lunarward_RuntimeEnv_SourceRepoInvalid;
                    }
                    else if (!ComponentSources.IsValidMirrorPrefix(mirror))
                    {
                        problem = Lang.Lunarward_RuntimeEnv_MirrorInvalid;
                    }
                    else if (pattern.Length > 0)
                    {
                        try
                        {
                            Regex.IsMatch(string.Empty, pattern);
                        }
                        catch (ArgumentException)
                        {
                            problem = Lang.Lunarward_RuntimeEnv_SourcePatternInvalid;
                        }
                    }
                    if (problem is not null)
                    {
                        args.Cancel = true;
                        error.Text = problem;
                    }
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }
                draft.DisplayName = (nameBox.Text ?? "").Trim();
                draft.Kind = kindBox.SelectedIndex == 1 ? "graphics" : "wine";
                draft.Owner = (ownerBox.Text ?? "").Trim();
                draft.Repo = (repoBox.Text ?? "").Trim();
                draft.Pattern = (patternBox.Text ?? "").Trim();
                draft.Mirror = (mirrorBox.Text ?? "").Trim();
                draft.Enabled = enabledBox.IsChecked == true;
                if (isNew)
                {
                    draft.Id = ComponentSources.NewCustomId();
                    drafts.Add(draft);
                }
                Rebuild();
            }

            search.TextChanged += (_, _) => Rebuild();
            addButton.Click += async (_, _) => await OpenSourceEditorAsync(new SourceDraft { Enabled = true }, isNew: true);
            probeAll.Click += async (_, _) =>
            {
                probeAll.IsEnabled = false;
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
                    foreach (SourceDraft draft in drafts.ToList())
                    {
                        try
                        {
                            ComponentSourceProbe result = await ComponentSources.ProbeAsync(http, ToEntry(draft, drafts.IndexOf(draft)));
                            probeState[draft.Id] = (result.Text, result.Ok);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "probe source failed");
                            probeState[draft.Id] = (Lang.Lunarward_RuntimeEnv_ProbeFailedShort, false);
                        }
                    }
                }
                finally
                {
                    probeAll.IsEnabled = true;
                }
                Rebuild();
            };
            reset.Click += async (_, _) =>
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
                if (confirm != ContentDialogResult.Primary)
                {
                    return;
                }
                ComponentSources.ResetUserEntries();
                drafts = LoadSourceEntries().Select(ToDraft).ToList();
                probeState.Clear();
                checkState.Clear();
                Helpers.InAppToast.MainWindow?.Success((string?)null, Lang.Lunarward_RuntimeEnv_SourcesResetDone, 4000);
                Rebuild();
            };

            var page = new StackPanel { Spacing = 12, Width = 560 };
            page.Children.Add(hint);
            page.Children.Add(tokenHint);
            page.Children.Add(toolbar);
            page.Children.Add(summary);
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

            Rebuild();
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            if (drafts.Any(x => !ComponentSources.IsValidMirrorPrefix(x.Mirror)))
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_MirrorInvalid);
                return;
            }
            List<ComponentSourceEntry> result = drafts.Select((draft, index) => ToEntry(draft, index)).ToList();
            foreach (ComponentSourceEntry def in ComponentSources.Defaults)
            {
                if (drafts.All(x => x.Id != def.Id))
                {
                    result.Add(new ComponentSourceEntry { Id = def.Id, Removed = true });
                }
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


    private static SourceDraft ToDraft(ComponentSourceEntry entry) => new()
    {
        Id = entry.Id,
        DisplayName = entry.DisplayName,
        Kind = entry.Kind,
        Owner = entry.Owner,
        Repo = entry.Repo,
        Pattern = entry.AssetPattern,
        Mirror = entry.MirrorPrefix,
        Enabled = entry.Enabled,
    };


    private static ComponentSourceEntry ToEntry(SourceDraft draft, int index) => new()
    {
        Id = draft.Id,
        DisplayName = draft.DisplayName,
        Kind = draft.Kind,
        Owner = draft.Owner,
        Repo = draft.Repo,
        AssetPattern = draft.Pattern,
        MirrorPrefix = draft.Mirror,
        Rank = (index + 1) * 10,
        Enabled = draft.Enabled,
    };


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
