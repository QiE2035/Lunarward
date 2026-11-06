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
        string file = Path.Combine(folder, $"Lunarward_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");

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
                if (notify)
                {
                    // C4：热键截图时窗口通常被游戏盖住，桌面通知是唯一能确认「截到了」的反馈。
                    Helpers.DesktopNotification.Show("截图已保存", Path.GetFileName(file), "lunarward", 4000);
                }
                return file;
            }
            TryDelete(file);
        }
        Serilog.Log.Warning("截图失败：spectacle / grim / gnome-screenshot 都不可用或无输出");
        return null;
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
