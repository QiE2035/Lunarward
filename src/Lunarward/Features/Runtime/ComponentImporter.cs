// Lunarward：把压缩包变成受管组件（D73 §2.4）。
//
// 与「手动路径」的区别只有一点：这里由我们解压、归一化布局、写 manifest，
// 所以后续能校验、能删除、能算占用。下载完的归档也走同一条路。
//
// 解压用系统 tar（后台线程），不用托管解压库：xz 解压是 CPU 密集的，
// 托管实现普遍单线程且慢，而且多一个依赖。
using Microsoft.Extensions.Logging;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Runtime;


public static class ComponentImporter
{
    /// <summary>解压超时：几百 MB 的 xz 在慢盘上也要几分钟。</summary>
    private static readonly TimeSpan ExtractTimeout = TimeSpan.FromMinutes(15);


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
        string? sourceUrl = null,
        string? mirrorPrefix = null,
        ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(archivePath))
        {
            throw new FileNotFoundException($"找不到归档：{archivePath}", archivePath);
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
            await ExtractAsync(archivePath, temp, logger, ct).ConfigureAwait(false);

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
                if (!HasGraphicsDlls(extracted))
                {
                    throw new InvalidDataException("归档里没有找到 d3d11.dll / dxgi.dll / d3d12.dll，不像图形层");
                }
            }

            // 搬成正式组件目录（搬解压出来的那一层，不是外层临时目录）
            Directory.Move(extracted, target);

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
                    : new ComponentManifestDigest { Value = sha256, FromApi = !string.IsNullOrEmpty(sourceUrl) },
            };
            ComponentManifestStore.Write(target, manifest);
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
    /// 用系统 tar 解压。先试自动识别（bsdtar 能认 zstd），失败再显式 <c>--zstd</c>，
    /// 再失败把 tar 的 stderr 原样抛出去——不猜、不翻译（D73 §2.5）。
    /// </summary>
    public static async Task ExtractAsync(string archivePath, string destination, ILogger? logger,
        CancellationToken ct = default)
    {
        string[] attempts =
        {
            "-xf",
            "--zstd -xf",
        };
        string lastError = "";
        foreach (string mode in attempts)
        {
            string[] parts = mode.Split(' ');
            var startInfo = new ProcessStartInfo("tar")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            foreach (string part in parts)
            {
                startInfo.ArgumentList.Add(part);
            }
            startInfo.ArgumentList.Add(archivePath);
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(destination);

            using var process = new Process { StartInfo = startInfo };
            process.Start();
            Task<string> stderr = process.StandardError.ReadToEndAsync(CancellationToken.None);
            Task<string> stdout = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(ExtractTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                throw new TimeoutException($"解压超时：{archivePath}");
            }
            string err = await stderr.ConfigureAwait(false);
            _ = await stdout.ConfigureAwait(false);
            if (process.ExitCode == 0)
            {
                return;
            }
            lastError = string.IsNullOrWhiteSpace(err) ? $"tar 退出码 {process.ExitCode}" : err.Trim();
            logger?.LogWarning("[components] tar {Mode} 失败（{Code}）：{Error}", mode, process.ExitCode, lastError);
        }
        throw new InvalidOperationException($"解压失败：{archivePath}\n{lastError}");
    }


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


    private static bool HasGraphicsDlls(string directory)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*.dll", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file).ToLowerInvariant();
                if (name is "d3d11.dll" or "dxgi.dll" or "d3d12.dll" or "d3d10core.dll" or "d3d9.dll")
                {
                    return true;
                }
            }
        }
        catch
        {
            return false;
        }
        return false;
    }


    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // 已经退出或没权限，忽略
        }
    }
}
