// Lunarward：截图捕获（D31）。
//
// 上游用 Windows.Graphics.Capture + Win2D 直接在进程内抓帧；Linux 侧 M3 先走「外部工具」：
//   KDE    → spectacle -b -n -f -o <file>
//   wlroots→ grim <file>
//   GNOME  → gnome-screenshot -f <file>
// 全屏捕获不做窗口白名单（游戏全屏时抓的就是它）；失败一律返回 null，绝不返回「假成功」的空文件。
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Screenshot;

public static class ScreenCaptureLinux
{
    /// <summary>
    /// 保存目录：优先用设置页里配置的文件夹，否则 ~/Pictures/Lunarward。
    /// 以前这里写死、设置页的「更改文件夹」是空桩，用户改了等于没改（L-62）。
    /// </summary>
    public static string DefaultFolder
    {
        get
        {
            string? configured = null;
            try
            {
                configured = AppConfig.ScreenshotFolder;
            }
            catch
            {
            }
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured!;
            }
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrWhiteSpace(pictures))
            {
                pictures = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Pictures");
            }
            return Path.Combine(pictures, "Lunarward");
        }
    }


    public static async Task<string?> CaptureAsync(string? folder = null, bool notify = true, CancellationToken cancellation = default)
    {
        folder = string.IsNullOrWhiteSpace(folder) ? DefaultFolder : folder;
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "创建截图目录失败：{folder}", folder);
            return null;
        }
        // 文件名要能被 ScreenshotItem.TryParseCreationTime 认出来（它认 yyyyMMdd_HHmmssff 等格式，
        // 不认「日期_时间」这种混排），否则每条新截图都退到 FileInfo.CreationTime（D134）。
        string file = Path.Combine(folder, $"Lunarward_{DateTime.Now:yyyyMMdd_HHmmssff}.png");

        // 顺序按「本机最可能可用的」来：KDE 的 spectacle 支持无交互全屏，grim 次之。
        (string exe, string[] args)[] candidates =
        [
            ("spectacle", ["-b", "-n", "-f", "-o", file]),
            ("grim", [file]),
            ("gnome-screenshot", ["-f", file]),
        ];
        foreach ((string exe, string[] args) in candidates)
        {
            if (await TryRunAsync(exe, args, cancellation) && File.Exists(file) && new FileInfo(file).Length > 0)
            {
                Serilog.Log.Information("截图完成：{file}", file);
                await TryCopyToClipboardAsync(file).ConfigureAwait(false);
                if (notify)
                {
                    // C4：热键截图时窗口通常被游戏盖住，桌面通知是唯一能确认「截到了」的反馈。
                    Helpers.DesktopNotification.Show("截图已保存", Path.GetFileName(file), "lunarward", 4000);
                }
                return file;
            }
            TryDelete(file);
        }
        // 24 号 P4：外部工具都不可用（沙盒 / 精简系统）时走 xdg-desktop-portal。
        // 顺序是「外部工具优先」而不是 portal 优先，理由是观感：
        // spectacle -b -n -f 是静默全屏，portal 的 Screenshot 在 KDE 上可能弹一次授权/选区。
        // 沙盒里的进程看不到宿主的 spectacle/grim，自然就落到 portal。
        string? viaPortal = await Helpers.Portal.ScreenshotAsync(interactive: false, cancellation).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(viaPortal) && File.Exists(viaPortal))
        {
            try
            {
                File.Move(viaPortal, file, overwrite: true);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "把 portal 截图搬到截图目录失败：{source} → {file}", viaPortal, file);
                if (notify)
                {
                    Helpers.DesktopNotification.Show("截图已保存", Path.GetFileName(viaPortal), "lunarward", 4000);
                }
                return viaPortal;
            }
            Serilog.Log.Information("截图完成（portal）：{file}", file);
            await TryCopyToClipboardAsync(file).ConfigureAwait(false);
            if (notify)
            {
                Helpers.DesktopNotification.Show("截图已保存", Path.GetFileName(file), "lunarward", 4000);
            }
            return file;
        }
        Serilog.Log.Warning("截图失败：spectacle / grim / gnome-screenshot / portal 都不可用或被拒绝");
        return null;
    }


    /// <summary>B14：设置里「自动复制截图到剪贴板」在 Linux 侧也要真的生效。</summary>
    private static async Task TryCopyToClipboardAsync(string file)
    {
        try
        {
            if (!AppConfig.AutoCopyScreenshotToClipboard || !File.Exists(file))
            {
                return;
            }
            bool ok = await Task.Run(() => Helpers.ClipboardHelper.SetImageFile(file)).ConfigureAwait(false);
            if (!ok)
            {
                Serilog.Log.Warning("复制截图到剪贴板失败（wl-copy / xclip 都不可用或拒绝）");
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "复制截图到剪贴板失败");
        }
    }


    private static async Task<bool> TryRunAsync(string exe, string[] args, CancellationToken cancellation)
    {
        try
        {
            var info = new ProcessStartInfo(exe) { UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (string arg in args)
            {
                info.ArgumentList.Add(arg);
            }
            using Process? process = Process.Start(info);
            if (process is null)
            {
                return false;
            }
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return false;
            }
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "截图工具不可用：{exe}", exe);
            return false;
        }
    }


    private static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch { }
    }
}
