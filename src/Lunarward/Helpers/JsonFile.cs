// Lunarward：用户数据（配置、Profile、绑定、后启动条目）的落盘工具。
//
// 直接 File.WriteAllText 会在「进程被杀 / 磁盘满」时留下半截 JSON，
// 下次启动就变成「设置全部丢失」而且没有任何提示（L-62）。这里统一走
// 「写临时文件 → fsync → 原子改名」，并顺手留一份 .bak 便于人工找回。

using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Starward.Helpers;

internal static class JsonFile
{

    /// <summary>原子写入：先写 <c>path.tmp</c>，再改名覆盖目标。</summary>
    public static void WriteAtomic(string path, string content)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
        string temp = path + ".tmp";
        using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
        {
            writer.Write(content);
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
        if (File.Exists(path))
        {
            try
            {
                File.Copy(path, path + ".bak", overwrite: true);
            }
            catch
            {
                // 备份是尽力而为，不能因为它失败就不保存
            }
        }
        File.Move(temp, path, overwrite: true);
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

}
