// Lunarward：X11 上没有「应用是否深色」的编程接口，上游那些 User32/注册表判断在移植侧没有对应物（L-183）。
// 这里用一次性的启发式探测：优先看 GTK 主题（多数桌面都会同步 / 写入这些文件），其次看 KDE 配色名。
// 只读若干配置文件的头几行，不 fork 进程（硬规则 10），失败一律当浅色（Uno 默认是浅色）。
using System;
using System.IO;

namespace Starward.Helpers;

internal static class SystemThemePreference
{
    private static readonly Lazy<bool> _isDark = new(Detect, isThreadSafe: true);

    /// <summary>当前桌面是否为深色主题。</summary>
    public static bool IsDark => _isDark.Value;

    private static bool Detect()
    {
        try
        {
            // 显式设置的环境变量优先：GTK_THEME=Adwaita:dark / Breeze-Dark
            string? env = Environment.GetEnvironmentVariable("GTK_THEME");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return ContainsDark(env);
            }

            string? home = Environment.GetEnvironmentVariable("HOME");
            if (string.IsNullOrWhiteSpace(home))
            {
                return false;
            }

            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(home, ".config");

            // GTK 3 / GTK 4 的 settings.ini：gtk-application-prefer-dark-theme=1 或 gtk-theme-name=...-Dark
            bool? gtkDark = ReadGtkSettings(Path.Combine(configHome, "gtk-4.0", "settings.ini"))
                ?? ReadGtkSettings(Path.Combine(configHome, "gtk-3.0", "settings.ini"));
            if (gtkDark is bool gtk)
            {
                return gtk;
            }

            // KDE：kdeglobals 的 [General] ColorScheme / [Colors:Window] BackgroundNormal 亮度
            if (ReadKdeSettings(Path.Combine(configHome, "kdeglobals")) is bool kde)
            {
                return kde;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "detect system theme failed");
        }
        return false;
    }

    private static bool? ReadGtkSettings(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        bool? preferDark = null;
        foreach (string line in ReadHead(path, 200))
        {
            if (line.StartsWith("gtk-application-prefer-dark-theme", StringComparison.OrdinalIgnoreCase))
            {
                int index = line.IndexOf('=');
                if (index > 0)
                {
                    string value = line[(index + 1)..].Trim();
                    preferDark = value is "1" or "true" or "True" or "TRUE";
                }
            }
            else if (line.StartsWith("gtk-theme-name", StringComparison.OrdinalIgnoreCase))
            {
                if (ContainsDark(line))
                {
                    return true;
                }
            }
        }
        return preferDark;
    }

    private static bool? ReadKdeSettings(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        foreach (string line in ReadHead(path, 400))
        {
            // ColorScheme=BreezeDark / BreezeLight
            if (line.StartsWith("ColorScheme", StringComparison.OrdinalIgnoreCase))
            {
                int index = line.IndexOf('=');
                if (index > 0)
                {
                    return ContainsDark(line[(index + 1)..]);
                }
            }
            // BackgroundNormal=46,52,54 → 取三个通道的平均亮度，< 128 视为深色
            if (line.StartsWith("BackgroundNormal", StringComparison.OrdinalIgnoreCase))
            {
                int index = line.IndexOf('=');
                if (index > 0 && TryAverageBrightness(line[(index + 1)..], out double brightness))
                {
                    return brightness < 128;
                }
            }
        }
        return null;
    }

    private static bool TryAverageBrightness(string value, out double brightness)
    {
        brightness = 0;
        string[] parts = value.Trim().Split(',');
        if (parts.Length < 3)
        {
            return false;
        }
        int sum = 0;
        for (int i = 0; i < 3; i++)
        {
            if (!int.TryParse(parts[i].Trim(), out int channel))
            {
                return false;
            }
            sum += channel;
        }
        brightness = sum / 3d;
        return true;
    }

    private static bool ContainsDark(string value)
    {
        return value.Contains("dark", StringComparison.OrdinalIgnoreCase);
    }

    private static string[] ReadHead(string path, int maxLines)
    {
        try
        {
            string[] lines = File.ReadAllLines(path);
            return lines.Length <= maxLines ? lines : lines[..maxLines];
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "read theme file failed: {path}", path);
            return Array.Empty<string>();
        }
    }
}
