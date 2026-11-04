// Lunarward：上游用 GetDriveTypeW 等 Win32 接口判断盘符；Linux 侧改读 /proc/mounts 与 statvfs。
using System;
using System.IO;
using System.Linq;

namespace Starward.Helpers;

public static class DriveHelper
{
    /// <summary>Linux 上「可移动存储」判定：挂载点位于 /media、/run/media 或 /mnt 下。</summary>
    public static bool IsDeviceRemovableOrOnUSB(string path)
    {
        try
        {
            var full = Path.GetFullPath(path);
            foreach (var line in File.ReadLines("/proc/mounts"))
            {
                var parts = line.Split(' ');
                if (parts.Length < 2)
                {
                    continue;
                }
                var mount = parts[1].Replace("\\040", " ");
                if (full.StartsWith(mount, StringComparison.Ordinal) &&
                    (mount.StartsWith("/media") || mount.StartsWith("/run/media") || mount.StartsWith("/mnt")))
                {
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DriveHelper] {ex.Message}");
        }
        return false;
    }

    public static System.IO.DriveType GetDriveType(string path)
    {
        try
        {
            var mount = "/";
            var full = Path.GetFullPath(path);
            foreach (var line in File.ReadLines("/proc/mounts"))
            {
                var parts = line.Split(' ');
                if (parts.Length < 2)
                {
                    continue;
                }
                var m = parts[1].Replace("\\040", " ");
                if (full.StartsWith(m, StringComparison.Ordinal) && m.Length > mount.Length)
                {
                    mount = m;
                }
            }
            var fs = ReadFileSystemOf(mount);
            return fs switch
            {
                "nfs" or "nfs4" or "cifs" or "smb3" or "sshfs" or "fuse.sshfs" => System.IO.DriveType.Network,
                "tmpfs" or "devtmpfs" => System.IO.DriveType.Ram,
                _ => System.IO.DriveType.Fixed,
            };
        }
        catch
        {
            return System.IO.DriveType.Fixed;
        }
    }

    private static string ReadFileSystemOf(string mount)
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/mounts"))
            {
                var parts = line.Split(' ');
                if (parts.Length >= 3 && parts[1].Replace("\\040", " ") == mount)
                {
                    return parts[2];
                }
            }
        }
        catch
        {
            // ignore
        }
        return "";
    }

    public static string GetDriveFormat(string path) => "";

    public static long GetDriveAvailableSpace(string path)
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)) ?? "/");
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }
}
