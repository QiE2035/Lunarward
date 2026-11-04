// Lunarward：Windows 上通过注册表注册 starward:// 协议；Linux 走 .desktop + xdg-mime。
//
// 需要三样东西才能真的「点网页里的 lunarward:// 就唤起启动器」：
//   1. ~/.local/share/applications/lunarward.desktop（带 MimeType=x-scheme-handler/lunarward 与 Exec）
//   2. 该 .desktop 的可执行位（部分桌面环境要求）
//   3. updatedb（`update-desktop-database ~/.local/share/applications`）刷新缓存
// 这里只做用户级注册，不碰系统目录、不请求 root。
using System;
using System.Diagnostics;
using System.IO;

namespace Starward.Features.UrlProtocol;

internal static class UrlProtocolService
{
    private const string Scheme = "lunarward";

    private static string DesktopFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "applications", "lunarward.desktop");

    private static string ExecutablePath => Environment.ProcessPath ?? "lunarward";

    public static void RegisterProtocol()
    {
        try
        {
            string? folder = Path.GetDirectoryName(DesktopFilePath);
            if (folder is null)
            {
                return;
            }
            Directory.CreateDirectory(folder);
            string icon = Path.Combine(AppContext.BaseDirectory, "Assets", "lunarward.png");
            File.WriteAllText(DesktopFilePath, $"""
                [Desktop Entry]
                Type=Application
                Name=Lunarward
                Comment=米哈游游戏启动器（Linux）
                Exec="{ExecutablePath}" %u
                Icon={(File.Exists(icon) ? icon : "lunarward")}
                Terminal=false
                Categories=Game;
                MimeType=x-scheme-handler/{Scheme};
                StartupWMClass=Lunarward

                """);
            RunQuiet("update-desktop-database", Path.GetDirectoryName(DesktopFilePath)!);
            RunQuiet("xdg-mime", "default", "lunarward.desktop", $"x-scheme-handler/{Scheme}");
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Register protocol failed");
        }
    }

    public static void UnregisterProtocol()
    {
        try
        {
            if (File.Exists(DesktopFilePath))
            {
                File.Delete(DesktopFilePath);
                RunQuiet("update-desktop-database", Path.GetDirectoryName(DesktopFilePath)!);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Unregister protocol failed");
        }
    }

    /// <summary>以「当前用户默认处理器是不是我们自己」判断，而不是看文件在不在。</summary>
    public static bool IsProtocolRegistered()
    {
        try
        {
            if (!File.Exists(DesktopFilePath))
            {
                return false;
            }
            using var proc = Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-mime",
                ArgumentList = { "query", "default", $"x-scheme-handler/{Scheme}" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            });
            if (proc is null)
            {
                return false;
            }
            string output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);
            return output.Equals("lunarward.desktop", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Query protocol failed");
            return false;
        }
    }

    private static void RunQuiet(string fileName, params string[] arguments)
    {
        try
        {
            var info = new ProcessStartInfo { FileName = fileName, UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }
            using var proc = Process.Start(info);
            proc?.WaitForExit(5000);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Run {file} failed", fileName);
        }
    }
}
