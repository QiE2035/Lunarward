// Lunarward：「谁占着这个前缀」——预检失败时给用户可执行的答案。
//
// 只用系统已有的只读工具，找不到就如实说「没查到」，不报错。
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Starward.Helpers;


public static class ProcessQuery
{

    /// <summary>列出占用了指定目录的进程（fuser -v），失败或为空时返回空字符串。</summary>
    public static async Task<string> FindUsersAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }
        string output = await RunAsync("fuser", new[] { "-v", path });
        if (!string.IsNullOrWhiteSpace(output))
        {
            return output.Trim();
        }
        // fuser 不在时退回 lsof（只列 PID，输出更短）
        output = await RunAsync("lsof", new[] { "-t", "+D", path });
        return output.Trim();
    }


    private static async Task<string> RunAsync(string file, string[] args)
    {
        try
        {
            var info = new ProcessStartInfo
            {
                FileName = file,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (string arg in args)
            {
                info.ArgumentList.Add(arg);
            }
            using var process = Process.Start(info);
            if (process is null)
            {
                return "";
            }
            Task<string> stdout = process.StandardOutput.ReadToEndAsync();
            Task<string> stderr = process.StandardError.ReadToEndAsync();
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                return "";
            }
            var builder = new StringBuilder();
            builder.Append(await stdout);
            string err = await stderr;
            if (builder.Length == 0 && !string.IsNullOrWhiteSpace(err))
            {
                builder.Append(err);
            }
            return builder.ToString();
        }
        catch (Exception)
        {
            return "";
        }
    }

}
