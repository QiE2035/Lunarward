// Lunarward：用系统默认程序打开 URL / 文件 / 目录。
//
// 上游到处用 `Windows.System.Launcher.LaunchUriAsync`，Uno 的 X11 后端没有实现它
// （运行时里搜不到任何 xdg-open / gio 调用痕迹，调用会静默失败），所以在 Linux 上统一走这里。
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
        // xdg-open 是 freedesktop 标准入口，KDE/GNOME/XFCE 都装了；失败再退 gio（部分精简环境只留 gio）。
        if (TryStart("xdg-open", target))
        {
            return true;
        }
        return TryStart("gio", "open", target);
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
