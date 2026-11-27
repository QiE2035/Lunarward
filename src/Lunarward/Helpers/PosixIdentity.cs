// Lunarward：进程身份查询（D77 §3.3）。
//
// wine 不允许以 root 运行：它会建出属主错乱的前缀，之后普通用户既读不到也删不掉，
// 而且 wine 自己只在启动后几行 stderr 里嘟囔一句就退出。这里在**动手之前**就判掉，
// 并把原因说人话（谁在跑、为什么不行、该怎么跑）。
using System;
using System.Runtime.InteropServices;

namespace Starward.Helpers;


public static class PosixIdentity
{

    /// <summary>
    /// 当前有效 UID 是否 0（root）。用 BCL 的 <see cref="Environment.IsPrivilegedProcess"/>（.NET 8+，
    /// Unix 上语义就是 euid == 0），不再自己 P/Invoke geteuid。
    /// Windows 上该属性表示「管理员提权」，而 wine 的 root 限制只对 Unix 有意义，所以先排除 Windows。
    /// </summary>
    public static bool IsRoot
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return false;
            }
            return Environment.IsPrivilegedProcess;
        }
    }


    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int Stat(string path, IntPtr buf);


    /// <summary>
    /// B27：取 (st_dev, st_ino) 作为文件身份，硬链接在不同路径下也得到同一个键。
    /// glibc 的 struct stat 在 Linux 上 st_dev 在偏移 0、st_ino 在偏移 8（x86_64 / aarch64 一致）。
    /// </summary>
    public static bool TryGetFileIdentity(string path, out string key)
    {
        key = "";
        if (OperatingSystem.IsWindows())
        {
            return false;
        }
        IntPtr buffer = Marshal.AllocHGlobal(256);
        try
        {
            if (Stat(path, buffer) != 0)
            {
                return false;
            }
            long dev = Marshal.ReadInt64(buffer, 0);
            long ino = Marshal.ReadInt64(buffer, 8);
            key = $"{dev}:{ino}";
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }


    /// <summary>root 下直接拒绝：抛出的消息要能独立读懂（会进预检清单和 CLI 输出）。</summary>
    public static void ThrowIfRoot(string action)
    {
        if (IsRoot)
        {
            throw new InvalidOperationException(
                $"不能以 root 运行{action}：wine 会建出属主错乱的前缀，之后自己也改不动。"
                + "请用普通用户启动 Lunarward（需要写系统目录时再单独提权）。");
        }
    }

}
