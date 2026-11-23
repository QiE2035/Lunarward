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
    private async void Button_DownloadComponent_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<ComponentSourceEntry> sources = LoadSourceEntries();
            if (sources.Count == 0)
            {
                Helpers.InAppToast.MainWindow?.Error((string?)null, Lang.Lunarward_RuntimeEnv_NoDownloadSource);
                return;
            }

            var sourceBox = new ComboBox
            {
                ItemsSource = sources.Select(x => x.DisplayName).ToList(),
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var status = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.75,
                Text = Lang.Lunarward_RuntimeEnv_DownloadHint,
            };
            // 多选 = 队列（P3「下载队列」）：勾几个版本就依次下载并导入。
            // 用 CheckBox 自绘而不是 ListBox：ContentDialog 里的 ListBox 在 Uno/Skia 上点击不改变选中，
            // 鼠标也看不出选没选（用户报的「下载组件弹窗无法选择」）。
            var assetPanel = new StackPanel { Spacing = 2 };
            var assetScroll = new ScrollViewer
            {
                Content = assetPanel,
                Height = 190,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            };
            var selectionText = new TextBlock { FontSize = 12, Opacity = 0.75 };
            var loadButton = new Button { Content = Lang.Lunarward_RuntimeEnv_LoadVersions };
            var startButton = new Button { Content = Lang.Lunarward_RuntimeEnv_StartDownload, IsEnabled = false };
            var cancelButton = new Button { Content = Lang.Lunarward_RuntimeEnv_Cancel, IsEnabled = false };
            List<CheckBox> assetChecks = new();

            var panel = new StackPanel { Spacing = 8, MinWidth = 520 };
            panel.Children.Add(new TextBlock { Text = Lang.Lunarward_RuntimeEnv_Source, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            panel.Children.Add(sourceBox);
            panel.Children.Add(loadButton);
            panel.Children.Add(assetScroll);
            panel.Children.Add(selectionText);
            panel.Children.Add(startButton);
            panel.Children.Add(cancelButton);
            panel.Children.Add(status);

            var dialog = new ContentDialog
            {
                Title = Lang.Lunarward_RuntimeEnv_DownloadComponent,
                Content = new ScrollViewer { Content = panel, MaxHeight = 520 },
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Close2,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            IReadOnlyList<ComponentReleaseAsset>? assets = null;
            CancellationTokenSource? cts = null;

            void UpdateSelection()
            {
                int count = assetChecks.Count(x => x.IsChecked == true);
                selectionText.Text = count == 0 ? "" : string.Format(Lang.Lunarward_RuntimeEnv_SelectedVersions, count);
                startButton.IsEnabled = count > 0;
            }

            void ClearAssets()
            {
                assets = null;
                assetChecks.Clear();
                assetPanel.Children.Clear();
                selectionText.Text = "";
                startButton.IsEnabled = false;
            }

            // 切下载源必须清空上一份版本列表：否则列表还显示上一个源的归档，
            // 而「开始下载」按新源的类型/镜像去装（玩家会看到「DXVK 的列表里下 wine」这种事）。
            sourceBox.SelectionChanged += (_, _) =>
            {
                if (sourceBox.SelectedIndex >= 0)
                {
                    ClearAssets();
                    status.Text = Lang.Lunarward_RuntimeEnv_DownloadHint;
                }
            };

            loadButton.Click += async (_, _) =>
            {
                int index = Math.Max(0, sourceBox.SelectedIndex);
                ComponentSourceEntry source = sources[index];
                loadButton.IsEnabled = false;
                startButton.IsEnabled = false;
                status.Text = string.Format(Lang.Lunarward_RuntimeEnv_ReadingVersions, source.DisplayName);
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                    assets = await ComponentSources.ListAssetsAsync(http, source, 10, _logger);
                    assetPanel.Children.Clear();
                    assetChecks.Clear();
                    for (int i = 0; i < assets.Count; i++)
                    {
                        ComponentReleaseAsset asset = assets[i];
                        string label = $"{asset.Name}    {asset.Size / 1048576} MB{(string.IsNullOrWhiteSpace(asset.Version) ? "" : "    " + asset.Version)}";
                        var check = new CheckBox
                        {
                            Content = label,
                            Tag = i,
                            MinWidth = 480,
                        };
                        check.Checked += (_, _) => UpdateSelection();
                        check.Unchecked += (_, _) => UpdateSelection();
                        assetChecks.Add(check);
                        assetPanel.Children.Add(check);
                    }
                    if (assets.Count == 0)
                    {
                        status.Text = Lang.Lunarward_RuntimeEnv_NoAssetsForSource;
                    }
                    else
                    {
                        status.Text = string.Format(Lang.Lunarward_RuntimeEnv_ArchivesFound, assets.Count);
                    }
                    UpdateSelection();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "list component assets failed");
                    status.Text = string.Format(Lang.Lunarward_RuntimeEnv_ReadFailed, ex.Message);
                }
                finally
                {
                    loadButton.IsEnabled = true;
                }
            };

            startButton.Click += async (_, _) =>
            {
                if (assets is null)
                {
                    return;
                }
                // 队列：多选即排队，逐个「下载 → 导入」；一个失败不影响后面的（P3「下载队列」）
                List<int> queue = assetChecks
                    .Where(x => x.IsChecked == true && x.Tag is int index)
                    .Select(x => (int)x.Tag)
                    .Distinct()
                    .ToList();
                if (queue.Count == 0)
                {
                    status.Text = Lang.Lunarward_RuntimeEnv_SelectVersionFirst;
                    return;
                }
                queue.RemoveAll(x => x >= assets.Count);
                if (queue.Count == 0)
                {
                    status.Text = Lang.Lunarward_RuntimeEnv_SelectionExpired;
                    return;
                }
                ComponentSourceEntry source = sources[sourceBox.SelectedIndex < 0 ? 0 : sourceBox.SelectedIndex];
                string kind = ImportKindOf(source);

                startButton.IsEnabled = false;
                loadButton.IsEnabled = false;
                cancelButton.IsEnabled = true;
                cts = new CancellationTokenSource();
                CancellationToken token = cts.Token;
                string downloadDir = Path.Combine(ComponentStore.ComponentsFolder, ".download");
                int okCount = 0;
                List<string> failed = new();
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                    for (int i = 0; i < queue.Count; i++)
                    {
                        ComponentReleaseAsset asset = assets[queue[i]];
                        string prefix = queue.Count > 1 ? string.Format(Lang.Lunarward_RuntimeEnv_QueuePrefix, i + 1, queue.Count) : "";

                        // 空间预检：归档本身 + 解压后（保守按 2 倍估）
                        long need = asset.Size * 2 + 64L * 1024 * 1024;
                        try
                        {
                            long free = Helpers.DriveHelper.GetDriveAvailableSpace(ComponentStore.ComponentsFolder);
                            if (free < need)
                            {
                                failed.Add(string.Format(Lang.Lunarward_RuntimeEnv_NotEnoughSpace, asset.Name, need / 1048576, free / 1048576));
                                continue;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "check free space failed, continue anyway");
                        }

                        Directory.CreateDirectory(downloadDir);
                        try
                        {
                            var downloadProgress = new Progress<ComponentDownloadProgress>(p =>
                            {
                                string total = p.Total is > 0 ? $"{(p.Total.Value / 1048576)} MB" : Lang.Lunarward_RuntimeEnv_Unknown2;
                                status.Text = string.Format(Lang.Lunarward_RuntimeEnv_Downloading, prefix, p.Received / 1048576, total, p.ActiveConnections, (p.BytesPerSecond / 1024.0 / 1024.0).ToString("F1"));
                            });
                            ComponentDownloadResult result = await ComponentDownloader.DownloadAsync(
                                http, new Uri(asset.Url), downloadDir, asset.Name, asset.Sha256,
                                mirrorPrefix: source.MirrorPrefix, connections: 4,
                                logger: _logger, progress: downloadProgress, ct: token);

                            status.Text = prefix + (result.Resumed ? Lang.Lunarward_RuntimeEnv_DownloadedResumed : Lang.Lunarward_RuntimeEnv_Downloaded);
                            var extractProgress = new Progress<ArchiveProgress>(p =>
                            {
                                status.Text = string.Format(Lang.Lunarward_RuntimeEnv_Extracting, prefix, p.Entries, p.Bytes / 1048576);
                            });
                            ComponentManifest manifest = await ComponentImporter.ImportAsync(
                                result.FilePath, kind, sha256: result.Sha256, expectedSha256: asset.Sha256,
                                sourceUrl: asset.Url,
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
                            status.Text = string.Format(Lang.Lunarward_RuntimeEnv_StageDone, prefix, manifest.Name, manifest.Version);
                            Helpers.InAppToast.MainWindow?.Success((string?)null, string.Format(Lang.Lunarward_RuntimeEnv_InstalledToast, manifest.Name, manifest.Version), 4000);
                        }
                        catch (OperationCanceledException)
                        {
                            status.Text = Lang.Lunarward_RuntimeEnv_PausedKeepParts;
                            break;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "download component failed: {Name}", asset.Name);
                            failed.Add($"{asset.Name}（{ex.Message}）");
                        }
                    }
                    if (queue.Count > 1)
                    {
                        status.Text = failed.Count == 0
                            ? string.Format(Lang.Lunarward_RuntimeEnv_QueueAllOk, okCount, queue.Count)
                            : string.Format(Lang.Lunarward_RuntimeEnv_QueuePartial, okCount, queue.Count, string.Join("；", failed));
                    }
                    else if (failed.Count > 0)
                    {
                        status.Text = Lang.Lunarward_RuntimeEnv_FailedColon + string.Join("；", failed);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "download queue failed");
                    status.Text = string.Format(Lang.Lunarward_RuntimeEnv_FailedPrefix, ex.Message);
                }
                finally
                {
                    cancelButton.IsEnabled = false;
                    loadButton.IsEnabled = true;
                    startButton.IsEnabled = assets.Count > 0;
                    cts?.Dispose();
                    cts = null;
                    UpdateSelection();
                }
            };

            cancelButton.Click += (_, _) => cts?.Cancel();

            await dialog.ShowAsync();
            cts?.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "open download dialog failed");
            Helpers.InAppToast.MainWindow?.Error(ex, Lang.Lunarward_RuntimeEnv_OpenDownloadWindowFailed);
        }
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
    private async void Button_Sources_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IReadOnlyList<ComponentSourceEntry> entries = LoadSourceEntries();
            var panel = new StackPanel { Spacing = 10, MinWidth = 560 };
            panel.Children.Add(new TextBlock
            {
                Text = string.Format(Lang.Lunarward_RuntimeEnv_SourcesHint, ComponentSources.FilePath),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Opacity = 0.75,
            });
            List<(ComponentSourceEntry Source, TextBox Mirror)> fields = new();
            foreach (ComponentSourceEntry source in entries)
            {
                var box = new TextBox
                {
                    Text = source.MirrorPrefix,
                    PlaceholderText = Lang.Lunarward_RuntimeEnv_MirrorPlaceholder,
                };
                var block = new StackPanel { Spacing = 2 };
                block.Children.Add(new TextBlock
                {
                    Text = $"{source.DisplayName}（{source.Kind} · {source.Owner}/{source.Repo}）",
                    FontSize = 12,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                });
                block.Children.Add(box);
                panel.Children.Add(block);
                fields.Add((source, box));
            }
            var dialog = new ContentDialog
            {
                Title = Lang.Lunarward_RuntimeEnv_SourcesTitle,
                Content = new ScrollViewer { Content = panel, MaxHeight = 520 },
                PrimaryButtonText = Lang.Lunarward_RuntimeEnv_Save,
                CloseButtonText = Lang.Lunarward_RuntimeEnv_Cancel,
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            List<ComponentSourceEntry> result = fields.Select(x => new ComponentSourceEntry
            {
                Id = x.Source.Id,
                DisplayName = x.Source.DisplayName,
                Kind = x.Source.Kind,
                Owner = x.Source.Owner,
                Repo = x.Source.Repo,
                AssetPattern = x.Source.AssetPattern,
                MirrorPrefix = x.Mirror?.Text?.Trim() ?? "",
                Rank = x.Source.Rank,
            }).ToList();
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
}
