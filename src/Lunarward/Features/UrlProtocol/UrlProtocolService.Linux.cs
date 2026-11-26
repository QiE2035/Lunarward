// Lunarward：Windows 上通过注册表注册 starward:// 协议；Linux 走 .desktop + xdg-mime。
//
// 需要三样东西才能真的「点网页里的 lunarward:// 就唤起启动器」：
//   1. ~/.local/share/applications/lunarward.desktop（带 MimeType=x-scheme-handler/lunarward 与 Exec）
//   2. 该 .desktop 的可执行位（部分桌面环境要求）
//   3. updatedb（`update-desktop-database ~/.local/share/applications`）刷新缓存
// 这里只做用户级注册，不碰系统目录、不请求 root。
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;

namespace Starward.Features.UrlProtocol;

internal static class UrlProtocolService
{
    private const string Scheme = "lunarward";

    /// <summary>XDG_DATA_HOME/applications（未设置时回落 ~/.local/share/applications），便于测试与自定义数据目录。</summary>
    private static string DesktopFilePath
    {
        get
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            string root = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : xdg;
            return Path.Combine(root, "applications", "lunarward.desktop");
        }
    }

    /// <summary>
    /// D134：AppImage 运行时进程路径是挂载点里的临时副本（/tmp/.mount_XXXX/...），
    /// 写进 .desktop 后 AppImage 一退出就成了死路径，深链永远唤不起来。
    /// appimagetool 会导出 `$APPIMAGE`，指向磁盘上那个真正可执行的 .AppImage，优先用它。
    /// </summary>
    private static string ExecutablePath =>
        Environment.GetEnvironmentVariable("APPIMAGE") is { Length: > 0 } appImage
            ? appImage
            : Environment.ProcessPath ?? "lunarward";

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

    /// <summary>
    /// Lunarward：上游 0.19.0 的 Hyperion 登录回调协议（Windows 写注册表，Linux 走同一个 lunarward.desktop）。
    /// 移植侧的登录回调已经由 `lunarward://hyperion-login/` 走单实例管道处理，因此这里复用同一个注册项：
    /// 注册协议时一个 .desktop 就涵盖全部 scheme，注销同理（D128）。
    /// </summary>
    public static void RegisterHypLoginProtocol() => RegisterProtocol();


    public static void UnregisterHypLoginProtocol()
    {
        // 与 RegisterProtocol 共用 .desktop，注销交给 UnregisterProtocol，避免把主协议一起删掉。
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
            // D134：只删 .desktop 不够——~/.config/mimeapps.list 里还留着
            // `x-scheme-handler/lunarward=lunarward.desktop`，桌面环境会把请求发给一个不存在的处理器，
            // 结果就是「点了没反应也不报错」。把指向我们这条的清掉（别动其它应用的关联）。
            RemoveMimeAssociation();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Unregister protocol failed");
        }
    }

    private static void RemoveMimeAssociation()
    {
        try
        {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            string file = Path.Combine(configHome, "mimeapps.list");
            if (!File.Exists(file))
            {
                return;
            }
            string[] lines = File.ReadAllLines(file);
            var kept = new List<string>(lines.Length);
            bool changed = false;
            foreach (string line in lines)
            {
                if (line.StartsWith($"x-scheme-handler/{Scheme}=", StringComparison.Ordinal))
                {
                    changed = true;
                    continue;
                }
                kept.Add(line);
            }
            if (changed)
            {
                File.WriteAllLines(file, kept);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "清理 mimeapps.list 关联失败");
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
            // D135：先 ReadToEnd 再 WaitForExit，一旦 xdg-mime 卡住就会一直阻塞调用线程；
            // 反过来先等（超时就直接判否），读到的输出才是完整的。
            if (!proc.WaitForExit(3000))
            {
                Serilog.Log.Debug("xdg-mime query x-scheme-handler timed out");
                return false;
            }
            string output = proc.StandardOutput.ReadToEnd().Trim();
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
