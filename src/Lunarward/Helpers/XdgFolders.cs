using Serilog;
using System;
using System.IO;

namespace Starward.Helpers;

/// <summary>
/// XDG 用户目录。Linux 上没有「Downloads 一定叫 Downloads」这回事：
/// 中文桌面是 <c>~/下载</c>，由 <c>~/.config/user-dirs.dirs</c> 的 <c>XDG_DOWNLOAD_DIR</c> 指定。
/// 上游用 Windows 的 <c>KnownFolders</c>，移植侧统一走这里（D134/D135）。
/// </summary>
internal static class XdgFolders
{
    /// <summary>
    /// 下载目录：读 <c>XDG_DOWNLOAD_DIR</c>，没配置就回落到 <c>$HOME</c>
    /// （不主动造一个空的 <c>~/Downloads</c>）。
    /// </summary>
    public static string DownloadFolder()
    {
        try
        {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            string file = Path.Combine(configHome, "user-dirs.dirs");
            if (File.Exists(file))
            {
                foreach (string line in File.ReadAllLines(file))
                {
                    if (!line.StartsWith("XDG_DOWNLOAD_DIR=", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string value = line["XDG_DOWNLOAD_DIR=".Length..].Trim().Trim('"');
                    value = value.Replace("$HOME", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.Ordinal);
                    if (value.Length > 0)
                    {
                        return value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "read XDG_DOWNLOAD_DIR failed");
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
