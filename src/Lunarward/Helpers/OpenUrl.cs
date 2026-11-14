// Lunarward：用系统默认程序打开 URL / 文件 / 目录。
//
// 上游到处用 `Windows.System.Launcher.LaunchUriAsync`，Uno 的 X11 后端没有实现它
// （运行时里搜不到任何 xdg-open / gio 调用痕迹，调用会静默失败），所以在 Linux 上统一走这里。
//
// 24 号 P2：优先 xdg-desktop-portal 的 OpenURI（沙盒 / 纯 Wayland 下唯一正规通道），
// portal 不可用才退回 `xdg-open` → `gio open`。
using System;
using System.Diagnostics;

namespace Starward.Helpers;

internal static class OpenUrl
{

    /// <summary>
    /// 用桌面默认程序打开 <paramref name="target"/>（http(s) 链接、文件路径、目录都行）。
    /// 返回 false 表示没找到可用的打开方式（调用方自行决定是否提示）。
    /// </summary>
    public static bool Open(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }
        // portal 优先：沙盒里 fork 出去的 xdg-open 没有宿主文件系统视图，会什么都不开
        if (Portal.TryOpenUri(ToUri(target)))
        {
            return true;
        }
        WarnIfDefaultHandlerMissing(target);
        // xdg-open 是 freedesktop 标准入口，KDE/GNOME/XFCE 都装了；失败再退 gio（部分精简环境只留 gio）。
        if (TryStart("xdg-open", target))
        {
            return true;
        }
        if (TryStart("gio", "open", target))
        {
            return true;
        }
        Serilog.Log.Warning("open {Target} failed: neither xdg-open nor gio is usable", target);
        return false;
    }


    /// <summary>
    /// `xdg-open` 在「默认程序指向一个不存在的 .desktop」时依然返回 0、什么都不打开，
    /// 用户看到的就是「点了没反应」。这里提前把这种环境下最可疑的情况记进日志，便于排查。
    /// </summary>
    private static void WarnIfDefaultHandlerMissing(string target)
    {
        try
        {
            string scheme = target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                ? "x-scheme-handler/https"
                : "inode/directory";
            using var query = Process.Start(new ProcessStartInfo("xdg-mime") { UseShellExecute = false, ArgumentList = { "query", "default", scheme }, RedirectStandardOutput = true });
            string? desktop = query?.StandardOutput.ReadToEnd().Trim();
            if (query is not null && !query.WaitForExit(3000))
            {
                query.Kill(entireProcessTree: true);
            }
            if (string.IsNullOrEmpty(desktop))
            {
                Serilog.Log.Warning("no default handler for {Scheme}; xdg-open will do nothing ({Target})", scheme, target);
                return;
            }
            string? home = Environment.GetEnvironmentVariable("HOME");
            string[] probes =
            {
                $"/usr/share/applications/{desktop}",
                $"/usr/local/share/applications/{desktop}",
                System.IO.Path.Combine(home ?? "", ".local/share/applications", desktop),
                System.IO.Path.Combine(home ?? "", ".local/share/flatpak/exports/share/applications", desktop),
                $"/var/lib/flatpak/exports/share/applications/{desktop}",
            };
            if (!Array.Exists(probes, System.IO.File.Exists))
            {
                Serilog.Log.Warning("default handler {Desktop} for {Scheme} does not exist on disk; xdg-open will do nothing ({Target})", desktop, scheme, target);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "probe default handler failed");
        }
    }


    /// <summary>本地路径补成 file:// URI（portal 只接受 URI），已是 URI 的原样返回。</summary>
    private static string ToUri(string target)
    {
        if (Uri.TryCreate(target, UriKind.Absolute, out Uri? parsed)
            && (parsed.Scheme is "http" or "https" or "file" or "mailto" or "lunarward" or "steam"))
        {
            return target;
        }
        try
        {
            return new Uri(System.IO.Path.GetFullPath(target)).AbsoluteUri;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "ToUri failed: {Target}", target);
            return target;
        }
    }


    private static bool TryStart(string file, params string[] args)
    {
        try
        {
            var info = new ProcessStartInfo(file) { UseShellExecute = false };
            foreach (string arg in args)
            {
                info.ArgumentList.Add(arg);
            }
            using Process? process = Process.Start(info);
            return process is not null;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "open {Target} via {File} failed", args.Length > 0 ? args[^1] : "", file);
            return false;
        }
    }

}
