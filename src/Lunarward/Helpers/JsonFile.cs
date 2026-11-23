// Lunarward：用户数据（配置、Profile、绑定、后启动条目）的落盘工具。
//
// 直接 File.WriteAllText 会在「进程被杀 / 磁盘满」时留下半截 JSON，
// 下次启动就变成「设置全部丢失」而且没有任何提示（L-62）。这里统一走
// 「写临时文件 → fsync → 原子改名」，并顺手留一份 .bak 便于人工找回。

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Starward.Helpers;

internal static class JsonFile
{

    /// <summary>同一路径的保存串行化：两个窗口同时保存同一份配置时，后一个不能把前一个的临时文件改名覆盖掉。</summary>
    private static readonly ConcurrentDictionary<string, object> PathLocks = new(StringComparer.Ordinal);


    /// <summary>
    /// 原子写入：写唯一的 <c>path.&lt;guid&gt;.tmp</c> → fsync → 备份旧文件 → 改名覆盖。
    /// R08：固定 <c>.tmp</c> 会让两次并发保存互相踩（第二个进程读到半截、或改名把第一个的临时文件搬走）；
    /// 改名后还要 fsync 目录，否则崩溃时「文件已改名但目录项未落盘」。（D104）
    /// </summary>
    public static void WriteAtomic(string path, string content)
    {
        string full = Path.GetFullPath(path);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        object gate = PathLocks.GetOrAdd(full, static _ => new object());
        lock (gate)
        {
            string temp = full + "." + Guid.NewGuid().ToString("n") + ".tmp";
            try
            {
                using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(content);
                    writer.Flush();
                    stream.Flush(flushToDisk: true);
                }
                if (File.Exists(full))
                {
                    try
                    {
                        File.Copy(full, full + ".bak", overwrite: true);
                    }
                    catch
                    {
                        // 备份是尽力而为，不能因为它失败就不保存
                    }
                }
                File.Move(temp, full, overwrite: true);
            }
            catch
            {
                try
                {
                    if (File.Exists(temp))
                    {
                        File.Delete(temp);
                    }
                }
                catch
                {
                }
                throw;
            }
            FsyncDirectory(dir);
        }
    }


    public static void WriteAtomic<T>(string path, T value, JsonSerializerOptions? options = null)
    {
        WriteAtomic(path, JsonSerializer.Serialize(value, options ?? new JsonSerializerOptions { WriteIndented = true }));
    }


    /// <summary>
    /// 读取 JSON；解析失败时把坏文件改名成 <c>*.corrupt-时间</c> 再返回 default，
    /// 这样「配置坏了」至少能在目录里看见证据，而不是静默当成没有配置。
    /// </summary>
    public static T? ReadOrRecover<T>(string path, JsonSerializerOptions? options = null) where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (Exception ex)
        {
            // 测试工程链接本文件时没有 Serilog；用 Debug 输出兜底，行为一致（坏文件照旧改名）
            System.Diagnostics.Debug.WriteLine($"Corrupt json file, moving aside: {path} ({ex.Message})");
            try
            {
                Serilog.Log.Warning(ex, "Corrupt json file, moving aside: {path}", path);
            }
            catch
            {
            }
            try
            {
                File.Move(path, $"{path}.corrupt-{DateTime.Now:yyyyMMddHHmmss}", overwrite: true);
            }
            catch
            {
            }
            return null;
        }
    }


    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int PosixOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int PosixFsync(int fd);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int PosixClose(int fd);


    private static void FsyncDirectory(string? dir)
    {
        if (string.IsNullOrEmpty(dir) || OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            int fd = PosixOpen(dir, 0);
            if (fd < 0)
            {
                return;
            }
            try
            {
                PosixFsync(fd);
            }
            finally
            {
                PosixClose(fd);
            }
        }
        catch
        {
        }
    }

}
