// Lunarward：把压缩包变成受管组件（D73 §2.4）。
//
// 与「手动路径」的区别只有一点：这里由我们解压、归一化布局、写 manifest，
// 所以后续能校验、能删除、能算占用。下载完的归档也走同一条路。
//
// 解压走 ArchiveExtractor（进程内、可取消、有进度）：.tar / .tar.gz / .tar.zst / .tar.xz / .zip。
// 2026-11-14 起不再调用系统 tar/xz 外部进程（见 24 号依赖盘点 P1）。
using Microsoft.Extensions.Logging;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Runtime;


public static class ComponentImporter
{
    /// <summary>
    /// 导入一个归档到受管目录，返回写好的 manifest。
    /// <paramref name="kind"/>：wine / proton / graphics。
    /// </summary>
    public static async Task<ComponentManifest> ImportAsync(
        string archivePath,
        string kind,
        string? name = null,
        string? version = null,
        string? sha256 = null,
        string? expectedSha256 = null,
        string? sourceUrl = null,
        string? mirrorPrefix = null,
        ILogger? logger = null,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException($"找不到归档：{archivePath}", archivePath);
        }
        // R34：有源端摘要就必须核对；不一致直接拒绝导入（而不是照样装、照样标「已校验」）。
        if (!string.IsNullOrWhiteSpace(expectedSha256))
        {
            string actual = sha256 ?? await ComponentDownloader.ComputeSha256Async(archivePath, ct).ConfigureAwait(false);
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"归档 SHA-256 与源端不一致：期望 {expectedSha256}，实际 {actual}");
            }
            sha256 = actual;
        }
        string fileName = Path.GetFileName(archivePath);
        name ??= ComponentNaming.SuggestName(fileName, kind);
        version ??= ComponentSources.GuessVersion(fileName, "");
        string dirName = ComponentNaming.Sanitize($"{name}-{version}".Trim('-'));
        string kindFolder = Path.Combine(ComponentStore.ComponentsFolder, FolderFor(kind));
        Directory.CreateDirectory(kindFolder);
        string target = Path.Combine(kindFolder, dirName);
        if (Directory.Exists(target))
        {
            throw new IOException($"组件目录已存在：{target}");
        }

        string temp = Path.Combine(ComponentStore.ComponentsFolder, $".import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            await ExtractAsync(archivePath, temp, logger, ct, progress).ConfigureAwait(false);

            // 大多数归档把内容包在一层同名目录里（dxvk-3.1.1/、wine-staging-9.22/）：
            // 只有一层且是目录时，把它当作组件根，避免变成 components/graphics/dxvk-3.1.1/dxvk-3.1.1/。
            string extracted = temp;
            List<string> topLevel = Directory.EnumerateFileSystemEntries(temp).ToList();
            if (topLevel.Count == 1 && Directory.Exists(topLevel[0]))
            {
                extracted = topLevel[0];
            }

            string root = ".";
            string layout = "";
            if (kind is "wine" or "proton")
            {
                (layout, string foundRoot, string winePath) = ComponentLayouts.FindRunnerRoot(extracted);
                if (string.IsNullOrEmpty(winePath))
                {
                    throw new InvalidDataException("归档里没有找到 bin/wine、files/bin/wine 或 wine 可执行文件");
                }
                root = RelativeTo(extracted, foundRoot);
            }
            else
            {
                // R59：与 GetGraphics 共用同一套「图形层根」判定，并把根归一。
                // 归档顶层常混着 README / LICENSE（顶层不是唯一目录），以前只做递归存在性检查，
                // 结果导入判定通过、manifest 也写了，但 GetGraphics 按固定路径永远看不到。
                string? graphicsRoot = GraphicsLayouts.FindRoot(extracted);
                if (graphicsRoot is null)
                {
                    throw new InvalidDataException("归档里没有找到 x64/d3d11.dll（或 d3d12.dll），不像图形层");
                }
                root = RelativeTo(extracted, graphicsRoot);
                extracted = graphicsRoot;
            }

            // R33：先把 manifest 写进临时目录、再整体改名，避免「目录已经在正式位置、manifest 没写成」
            // 的半成品（这种目录既不能再导入，也不会被组件列表认出来）。

            var manifest = new ComponentManifest
            {
                Id = dirName,
                Kind = kind,
                Name = name,
                Version = version,
                Root = root,
                Layout = layout,
                Origin = "managed",
                LayoutNormalized = true,
                Source = new ComponentManifestSource
                {
                    Url = sourceUrl ?? archivePath,
                    MirrorPrefix = mirrorPrefix ?? "",
                    DownloadedAt = DateTimeOffset.Now,
                },
                Digest = string.IsNullOrWhiteSpace(sha256)
                    ? null
                    : new ComponentManifestDigest { Value = sha256, FromApi = !string.IsNullOrEmpty(expectedSha256) },
            };
            ComponentManifestStore.Write(extracted, manifest);
            Directory.Move(extracted, target);
            if (kind == "graphics" && GraphicsLayouts.FindRoot(target) is null)
            {
                // R59：落盘后复核一次，失败就回滚，不留「目录在、列表里看不到」的半成品。
                try
                {
                    Directory.Delete(target, recursive: true);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "[components] 回滚无效图形层目录失败：{Path}", target);
                }
                throw new InvalidDataException($"导入后仍找不到图形层标记：{dirName}");
            }
            ComponentStore.Invalidate();
            logger?.LogInformation("[components] 导入完成 {Id}（kind={Kind} layout={Layout} root={Root}）",
                manifest.Id, kind, layout, root);
            return manifest;
        }
        finally
        {
            try
            {
                if (Directory.Exists(temp))
                {
                    Directory.Delete(temp, recursive: true);
                }
            }
            catch
            {
                // 清理失败不影响结果
            }
        }
    }


    /// <summary>
    /// 解压归档到 <paramref name="destination"/>（进程内完成，可取消、有进度）。
    /// 不支持的格式会抛 <see cref="NotSupportedException"/>，条目路径逃逸会抛 <see cref="InvalidDataException"/>。
    /// </summary>
    public static Task ExtractAsync(string archivePath, string destination, ILogger? logger,
        CancellationToken ct = default, IProgress<ArchiveProgress>? progress = null)
        => ArchiveExtractor.ExtractAsync(archivePath, destination, progress, logger, ct);


    public static string FolderFor(string kind) => kind == "graphics" ? "graphics" : "wine";


    private static string RelativeTo(string parent, string child)
    {
        if (string.Equals(parent, child, StringComparison.Ordinal))
        {
            return ".";
        }
        string relative = Path.GetRelativePath(parent, child);
        return string.IsNullOrEmpty(relative) ? "." : relative;
    }




}
