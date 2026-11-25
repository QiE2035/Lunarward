// LUNARWARD-DEVIATION: 上游只在 NTFS/ReFS 上尝试硬链接（`DriveInfo.DriveFormat is "NTFS" or "ReFS"`），
// Linux 的 ext4/btrfs/xfs 同样支持，这里把「能不能硬链接」和「建硬链接」抽成一处判断，
// 供 HardLinkService 与 GameInstallRunner 共用（D131）。
using System;
using System.Collections.Generic;
using System.IO;

namespace Starward.GameInstall.Service;

/// <summary>
/// 跨平台的硬链接能力判断与创建。
/// </summary>
public static class HardLinkCompat
{
    /// <summary>
    /// Unix 上不支持硬链接的文件系统（FAT/exFAT 无 inode 概念，NTFS 走 fuse 或 ntfs3 时不保证，网络文件系统不可靠）。
    /// </summary>
    private static readonly HashSet<string> UnsupportedUnixFileSystems = new(StringComparer.OrdinalIgnoreCase)
    {
        "vfat", "fat", "fat32", "msdos", "umsdos", "exfat", "ntfs", "ntfs3", "fuseblk",
        "cifs", "smb3", "smbfs", "nfs", "nfs4", "9p", "afs", "ceph", "glusterfs", "fuse.sshfs", "sshfs",
    };

    /// <summary>
    /// 该路径所在的文件系统是否支持硬链接。
    /// Windows 只认 NTFS/ReFS（与上游一致），Unix 用黑名单排除不支持的卷。
    /// </summary>
    public static bool IsSupported(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        try
        {
            string format = new DriveInfo(path).DriveFormat;
            if (OperatingSystem.IsWindows())
            {
                return format is "NTFS" or "ReFS";
            }
            return !string.IsNullOrWhiteSpace(format) && !UnsupportedUnixFileSystems.Contains(format);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// 在 <paramref name="linkPath"/> 上建立指向 <paramref name="targetPath"/> 的硬链接。
    /// 跨卷（EXDEV）、链接已存在、文件系统不支持等情况返回 false，调用方自行回退。
    /// </summary>
    public static bool TryCreate(string linkPath, string targetPath)
    {
        try
        {
            File.CreateHardLink(linkPath, targetPath);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
