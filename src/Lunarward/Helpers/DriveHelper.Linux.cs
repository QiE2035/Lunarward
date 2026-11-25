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
            // D95：按「路径实际落在哪个挂载点」取空间。以前用 Path.GetPathRoot 一律落到 /，
            // 跨分区安装时可用空间永远显示根文件系统的数字（B09）。
            string mount = GetMountPoint(path);
            return new DriveInfo(mount).AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }


    /// <summary>路径所在的最长挂载点；找不到时退回根目录。</summary>
    public static string GetMountPoint(string path)
    {
        var mount = "/";
        var full = Path.GetFullPath(path);
        try
        {
            foreach (var line in File.ReadLines("/proc/mounts"))
            {
                var parts = line.Split(' ');
                if (parts.Length < 2)
                {
                    continue;
                }
                var m = parts[1].Replace("\\040", " ");
                if (m.Length > mount.Length && full.StartsWith(m, StringComparison.Ordinal))
                {
                    mount = m;
                }
            }
        }
        catch
        {
            // /proc 不可读时用根目录
        }
        return mount;
    }


    /// <summary>
    /// 判断目标路径所在磁盘是否有寻道开销：true = 机械盘（HDD），false = 固态（SSD），null = 判断不出来。
    /// 不 fork 外部命令（硬规则 10）：读 <c>/proc/self/mounts</c> 找最长匹配挂载点，
    /// 再从 <c>/sys/block/&lt;设备&gt;/queue/rotational</c> 取 0/1；dm 设备尽力往下看一层 slaves。
    /// </summary>
    public static bool? IsDeviceSeekPenalty(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            string? device = null;
            int best = -1;
            foreach (string line in File.ReadLines("/proc/self/mounts"))
            {
                string[] parts = line.Split(' ', 4);
                if (parts.Length < 2 || !parts[1].StartsWith('/'))
                {
                    continue;
                }
                string mountPoint = parts[1].Replace("\\040", " ");
                if (full.Equals(mountPoint, StringComparison.Ordinal)
                    || (full.StartsWith(mountPoint, StringComparison.Ordinal) && mountPoint.EndsWith('/'))
                    || (mountPoint == "/" && full.StartsWith('/')))
                {
                    if (mountPoint.Length > best)
                    {
                        best = mountPoint.Length;
                        device = parts[0];
                    }
                }
            }
            if (device is null || !device.StartsWith("/dev/"))
            {
                return null;
            }
            string name = Path.GetFileName(device);
            bool? result = ReadRotational($"/sys/block/{name}/queue/rotational");
            if (result is not null)
            {
                return result;
            }
            // dm/LVM/md：看第一层子设备
            string slaves = $"/sys/block/{name}/slaves";
            if (Directory.Exists(slaves))
            {
                foreach (string slave in Directory.EnumerateFileSystemEntries(slaves))
                {
                    result = ReadRotational($"/sys/block/{Path.GetFileName(slave)}/queue/rotational");
                    if (result is not null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "IsDeviceSeekPenalty failed: {path}", path);
            return null;
        }
    }


    private static bool? ReadRotational(string file)
    {
        try
        {
            if (!File.Exists(file))
            {
                return null;
            }
            string text = File.ReadAllText(file).Trim();
            return text switch
            {
                "1" => true,
                "0" => false,
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

}
