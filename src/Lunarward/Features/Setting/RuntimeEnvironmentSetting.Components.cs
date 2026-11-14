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
                Helpers.InAppToast.MainWindow?.Error((string?)null, "没有可用的下载源（检查 components-sources.json）");
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
                Text = "先选源，再点「加载版本列表」。",
            };
            var assetList = new ListBox { Height = 190 };
            var loadButton = new Button { Content = "加载版本列表" };
            var startButton = new Button { Content = "开始下载", IsEnabled = false };
            var cancelButton = new Button { Content = "取消", IsEnabled = false };

            var panel = new StackPanel { Spacing = 8, MinWidth = 520 };
            panel.Children.Add(new TextBlock { Text = "下载源", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            panel.Children.Add(sourceBox);
            panel.Children.Add(loadButton);
            panel.Children.Add(assetList);
            panel.Children.Add(startButton);
            panel.Children.Add(cancelButton);
            panel.Children.Add(status);

            var dialog = new ContentDialog
            {
                Title = "下载组件",
                Content = new ScrollViewer { Content = panel, MaxHeight = 520 },
                CloseButtonText = "关闭",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };

            IReadOnlyList<ComponentReleaseAsset>? assets = null;
            CancellationTokenSource? cts = null;

            loadButton.Click += async (_, _) =>
            {
                int index = Math.Max(0, sourceBox.SelectedIndex);
                ComponentSourceEntry source = sources[index];
                loadButton.IsEnabled = false;
                startButton.IsEnabled = false;
                status.Text = $"正在读取 {source.DisplayName} 的版本列表…";
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                    assets = await ComponentSources.ListAssetsAsync(http, source, 10, _logger);
                    assetList.ItemsSource = assets.Select(x =>
                        $"{x.Name}    {x.Size / 1048576} MB{(string.IsNullOrWhiteSpace(x.Version) ? "" : "    " + x.Version)}").ToList();
                    if (assets.Count == 0)
                    {
                        status.Text = "这个源没有可用归档（没有匹配的资产，检查 components-sources.json 的 AssetPattern）。";
                    }
                    else
                    {
                        assetList.SelectedIndex = 0;
                        startButton.IsEnabled = true;
                        status.Text = $"共 {assets.Count} 个归档，选择一项后点「开始下载」。";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "list component assets failed");
                    status.Text = $"读取失败：{ex.Message}";
                }
                finally
                {
                    loadButton.IsEnabled = true;
                }
            };

            startButton.Click += async (_, _) =>
            {
                if (assets is null || assetList.SelectedIndex < 0 || assetList.SelectedIndex >= assets.Count)
                {
                    return;
                }
                ComponentReleaseAsset asset = assets[assetList.SelectedIndex];
                ComponentSourceEntry source = sources[Math.Max(0, sourceBox.SelectedIndex)];
                string kind = ImportKindOf(source);

                // 空间预检：归档本身 + 解压后（保守按 2 倍估）
                long need = asset.Size * 2 + 64L * 1024 * 1024;
                string root = Path.GetPathRoot(ComponentStore.ComponentsFolder) ?? "/";
                try
                {
                    long free = new DriveInfo(root).AvailableFreeSpace;
                    if (free < need)
                    {
                        status.Text = $"空间不足：需要约 {need / 1048576} MB，{root} 只剩 {free / 1048576} MB。";
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "check free space failed, continue anyway");
                }

                startButton.IsEnabled = false;
                loadButton.IsEnabled = false;
                cancelButton.IsEnabled = true;
                cts = new CancellationTokenSource();
                string downloadDir = Path.Combine(ComponentStore.ComponentsFolder, ".download");
                Directory.CreateDirectory(downloadDir);
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
                    var downloadProgress = new Progress<ComponentDownloadProgress>(p =>
                    {
                        string total = p.Total is > 0 ? $"{(p.Total.Value / 1048576)} MB" : "未知";
                        status.Text = $"下载中：{p.Received / 1048576} MB / {total}（{p.ActiveConnections} 连接，{p.BytesPerSecond / 1024 / 1024:F1} MB/s）";
                    });
                    ComponentDownloadResult result = await ComponentDownloader.DownloadAsync(
                        http, new Uri(asset.Url), downloadDir, asset.Name, asset.Sha256,
                        connections: 4, logger: _logger, progress: downloadProgress, ct: cts.Token);

                    status.Text = result.Resumed ? "下载完成（续传），开始解压…" : "下载完成，开始解压…";
                    var extractProgress = new Progress<ArchiveProgress>(p =>
                    {
                        status.Text = $"解压中：{p.Entries} 项 / {p.Bytes / 1048576} MB";
                    });
                    ComponentManifest manifest = await ComponentImporter.ImportAsync(
                        result.FilePath, kind, sha256: result.Sha256, sourceUrl: asset.Url,
                        mirrorPrefix: source.MirrorPrefix, logger: _logger, ct: cts.Token,
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
                    status.Text = $"完成：{manifest.Id}（{manifest.Name} {manifest.Version}）";
                    Helpers.InAppToast.MainWindow?.Success((string?)null, $"已安装 {manifest.Name} {manifest.Version}", 4000);
                }
                catch (OperationCanceledException)
                {
                    status.Text = "已暂停：已下载的分片保留，下次对同一版本再点下载会接着下。";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "download component failed");
                    status.Text = $"失败：{ex.Message}";
                }
                finally
                {
                    cancelButton.IsEnabled = false;
                    loadButton.IsEnabled = true;
                    startButton.IsEnabled = assets.Count > 0;
                    cts?.Dispose();
                    cts = null;
                }
            };

            cancelButton.Click += (_, _) => cts?.Cancel();

            await dialog.ShowAsync();
            cts?.Cancel();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "open download dialog failed");
            Helpers.InAppToast.MainWindow?.Error(ex, "打开下载窗口失败");
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
                    $"不支持的归档：{Path.GetFileName(archive)}（支持 {string.Join("、", ArchiveExtractor.SupportedExtensions)}）", 6000);
                return;
            }

            var kindBox = new ComboBox
            {
                ItemsSource = new List<string> { "运行器（wine / proton）", "图形层（DXVK / VKD3D）" },
                SelectedIndex = 0,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var dialog = new ContentDialog
            {
                Title = $"导入 {Path.GetFileName(archive)}",
                Content = kindBox,
                PrimaryButtonText = "导入",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot,
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }
            string kind = kindBox.SelectedIndex == 1 ? "graphics" : "wine";

            Helpers.InAppToast.MainWindow?.Information((string?)null, "正在解压，稍等…", 8000);
            var progress = new Progress<ArchiveProgress>(p =>
                _logger.LogDebug("[components] 解压进度 {Entries} 项 / {Bytes} 字节", p.Entries, p.Bytes));
            ComponentManifest manifest = await ComponentImporter.ImportAsync(
                archive, kind, logger: _logger, progress: progress);
            ComponentStore.Invalidate();
            LoadComponents();
            Helpers.InAppToast.MainWindow?.Success((string?)null, $"已导入 {manifest.Name} {manifest.Version}", 4000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "import archive failed");
            Helpers.InAppToast.MainWindow?.Error(ex, "导入归档失败（详见日志）");
        }
    }
}
