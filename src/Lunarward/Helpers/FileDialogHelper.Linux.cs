// Lunarward：Linux 下的文件对话框。
//
// 顺序（24 号 P3）：xdg-desktop-portal 的 FileChooser → kdialog → zenity。
// portal 优先是因为沙盒（flatpak / 纯 Wayland）里 kdialog 系工具可能根本看不见宿主文件；
// portal 不可用时才用 kdialog/zenity 这类「有就用」的兜底。
// 全都不存在返回 null（上层按「用户取消」处理）。
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starward.Helpers;

public static partial class FileDialogHelper
{
    private static string? _dialogTool;

    private static string? DialogTool => _dialogTool ??= DetectTool();

    private static string? DetectTool()
    {
        foreach (var candidate in new[] { "kdialog", "zenity" })
        {
            try
            {
                using var p = Process.Start(new ProcessStartInfo(candidate, "--version") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
                if (p is not null)
                {
                    p.WaitForExit(2000);
                    if (p.ExitCode == 0)
                    {
                        return candidate;
                    }
                }
            }
            catch
            {
                // 忽略，尝试下一个
            }
        }
        return null;
    }

    public static Task<string?> PickSingleFileAsync(nint parentWindow, params (string Name, string Extension)[] fileTypeFilter)
        => PickSingleFileAsync(fileTypeFilter);

    public static Task<string?> PickSingleFileAsync(XamlRoot? xamlRoot, params (string Name, string Extension)[] fileTypeFilter)
        => PickSingleFileAsync(fileTypeFilter);

    private static async Task<string?> PickSingleFileAsync((string Name, string Extension)[] filter)
    {
        PortalPickResult picked = await Portal.PickFilesAsync("打开文件", multiple: false, save: false,
            globs: ToGlobs(filter), acceptLabel: filter.Length > 0 ? filter[0].Name : null).ConfigureAwait(false);
        if (picked.Handled)
        {
            return picked.Paths.Length > 0 ? picked.Paths[0] : null;
        }
        var args = DialogTool == "kdialog"
            ? new List<string> { "--getopenfilename", AppConfig.UserDataFolder ?? "HOME" }
            : new List<string> { "--file-selection" };
        AppendFilters(args, filter);
        var output = await RunAsync(args);
        return FirstLine(output);
    }

    public static Task<List<string>> PickMultipleFilesAsync(nint parentWindow, params (string Name, string Extension)[] fileTypeFilter)
        => PickMultipleFilesAsync(fileTypeFilter);

    public static Task<List<string>> PickMultipleFilesAsync(XamlRoot? xamlRoot, params (string Name, string Extension)[] fileTypeFilter)
        => PickMultipleFilesAsync(fileTypeFilter);

    private static async Task<List<string>> PickMultipleFilesAsync((string Name, string Extension)[] filter)
    {
        PortalPickResult picked = await Portal.PickFilesAsync("选择文件", multiple: true, save: false,
            globs: ToGlobs(filter), acceptLabel: filter.Length > 0 ? filter[0].Name : null).ConfigureAwait(false);
        if (picked.Handled)
        {
            return picked.Paths.ToList();
        }
        var args = DialogTool == "kdialog"
            ? new List<string> { "--getopenfilename", AppConfig.UserDataFolder ?? "HOME", "--multiple", "--separate-output" }
            : new List<string> { "--file-selection", "--multiple", "--separator=\\n" };
        AppendFilters(args, filter);
        var output = await RunAsync(args);
        return output?.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList() ?? new List<string>();
    }

    public static Task<string?> OpenSaveFileDialogAsync(nint parentWindow, string? fileName = null, params (string Name, string Extension)[] fileTypeFilter)
        => OpenSaveFileDialogAsync(fileName, fileTypeFilter);

    public static Task<string?> OpenSaveFileDialogAsync(XamlRoot? xamlRoot, string? fileName = null, params (string Name, string Extension)[] fileTypeFilter)
        => OpenSaveFileDialogAsync(fileName, fileTypeFilter);

    private static async Task<string?> OpenSaveFileDialogAsync(string? fileName, (string Name, string Extension)[] filter)
    {
        PortalPickResult picked = await Portal.PickFilesAsync("保存文件", multiple: false, save: true,
            suggestedName: fileName, globs: ToGlobs(filter), acceptLabel: filter.Length > 0 ? filter[0].Name : null).ConfigureAwait(false);
        if (picked.Handled)
        {
            return picked.Paths.Length > 0 ? picked.Paths[0] : null;
        }
        var start = string.IsNullOrWhiteSpace(fileName)
            ? AppConfig.UserDataFolder ?? "HOME"
            : System.IO.Path.Combine(AppConfig.UserDataFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), fileName);
        var args = DialogTool == "kdialog"
            ? new List<string> { "--getsavefilename", start }
            : new List<string> { "--file-selection", "--save", $"--filename={start}" };
        AppendFilters(args, filter);
        var output = await RunAsync(args);
        return FirstLine(output);
    }

    public static async Task<string?> PickFolderAsync(XamlRoot? xamlRoot)
    {
        PortalPickResult picked = await Portal.PickFilesAsync("选择目录", multiple: false, save: false, directory: true).ConfigureAwait(false);
        if (picked.Handled)
        {
            return picked.Paths.Length > 0 ? picked.Paths[0] : null;
        }
        var args = DialogTool == "kdialog"
            ? new List<string> { "--getexistingdirectory", AppConfig.UserDataFolder ?? "HOME" }
            : new List<string> { "--file-selection", "--directory" };
        var output = await RunAsync(args).ConfigureAwait(false);
        return FirstLine(output);
    }


    /// <summary>`(".png")` → `"*.png"`；portal 的 filters 用 glob。</summary>
    private static string[] ToGlobs((string Name, string Extension)[] filter)
    {
        if (filter.Length == 0)
        {
            return Array.Empty<string>();
        }
        var globs = new List<string>(filter.Length);
        foreach (var (_, ext) in filter)
        {
            if (string.IsNullOrWhiteSpace(ext))
            {
                continue;
            }
            globs.Add(ext.Contains('*') ? ext : $"*{ext}");
        }
        return globs.ToArray();
    }


    private static void AppendFilters(List<string> args, (string Name, string Extension)[] filter)
    {
        foreach (var (name, ext) in filter)
        {
            var pattern = $"*{ext}";
            if (DialogTool == "kdialog")
            {
                args.Add($"{name} ({pattern})");
            }
            else
            {
                args.Add($"--file-filter={name} | {pattern}");
            }
        }
    }

    private static async Task<string?> RunAsync(List<string> args)
    {
        if (DialogTool is null)
        {
            Debug.WriteLine("[FileDialogHelper] neither kdialog nor zenity is available");
            return null;
        }
        try
        {
            var psi = new ProcessStartInfo(DialogTool)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (var a in args)
            {
                psi.ArgumentList.Add(a);
            }
            using var p = Process.Start(psi);
            if (p is null)
            {
                return null;
            }
            var stdout = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return p.ExitCode == 0 ? stdout : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileDialogHelper] dialog failed: {ex.Message}");
            return null;
        }
    }

    private static string? FirstLine(string? text)
        => string.IsNullOrWhiteSpace(text) ? null : text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).FirstOrDefault();
}
