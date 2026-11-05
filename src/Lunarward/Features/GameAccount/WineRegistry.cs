// Lunarward：Linux 下没有 Windows 注册表。米哈游的账号票据（MIHOYOSDK_ADL_*）实际保存在
// wine 前缀的注册表里，这里通过 `wine reg` 读写对应前缀，语义与上游 Registry.GetValue/SetValue 对齐。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Starward.Features.GameAccount;

internal static class WineRegistry
{
    /// <summary>作用域内的前缀覆盖：让「写设置页」这类调用能明确指定目标前缀，而不是永远落到 ~/.wine（L-62）。</summary>
    [ThreadStatic]
    private static string? _scopedPrefix;

    /// <summary>临时把读写指向指定前缀；传 null 表示不覆盖。</summary>
    public static IDisposable UsePrefix(string? prefix)
    {
        string? previous = _scopedPrefix;
        _scopedPrefix = string.IsNullOrWhiteSpace(prefix) ? null : prefix;
        return new PrefixScope(previous);
    }

    private sealed class PrefixScope(string? previous) : IDisposable
    {
        public void Dispose() => _scopedPrefix = previous;
    }

    /// <summary>当前 wine 前缀。作用域覆盖 &gt; 环境变量 &gt; 默认 ~/.wine。</summary>
    public static string WinePrefix
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_scopedPrefix))
            {
                return _scopedPrefix!;
            }
            var env = Environment.GetEnvironmentVariable("LUNARWARD_WINEPREFIX");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wine");
        }
    }

    /// <summary>wine 可执行文件。后续由运行环境（Profile 的 runner）注入。</summary>
    public static string WineExecutable =>
        Starward.Features.GameLauncher.GameLauncherService.ResolveWine() ?? "wine";

    public static object? GetValue(string key, string name, object? defaultValue = null)
    {
        try
        {
            var output = Run("reg", "query", NormalizeKey(key), "/v", name);
            if (output is null)
            {
                return defaultValue;
            }
            return ParseValue(output, name, defaultValue);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WineRegistry] GetValue failed: {key}\\{name}: {ex.Message}");
            return defaultValue;
        }
    }

    public static void SetValue(string key, string name, object value)
    {
        try
        {
            switch (value)
            {
                case byte[] bytes:
                    Run("reg", "add", NormalizeKey(key), "/v", name, "/t", "REG_BINARY", "/d", ToHex(bytes), "/f");
                    break;
                case int or long or uint:
                    var dword = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
                    Run("reg", "add", NormalizeKey(key), "/v", name, "/t", "REG_DWORD", "/d", $"0x{dword:x}", "/f");
                    break;
                default:
                    Run("reg", "add", NormalizeKey(key), "/v", name, "/t", "REG_SZ", "/d", value?.ToString() ?? "", "/f");
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WineRegistry] SetValue failed: {key}\\{name}: {ex.Message}");
        }
    }

    private static string NormalizeKey(string key)
    {
        // HKEY_CURRENT_USER\... -> HKCU\...
        if (key.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase))
        {
            return "HKCU\\" + key["HKEY_CURRENT_USER\\".Length..];
        }
        if (key.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase))
        {
            return "HKLM\\" + key["HKEY_LOCAL_MACHINE\\".Length..];
        }
        return key;
    }

    private static string ToHex(byte[] bytes) => string.Join(",", bytes.Select(b => b.ToString("x2")));

    private static object? ParseValue(string output, string name, object? defaultValue)
    {
        // 形如：  MIHOYOSDK_ADL_PROD_CN_h3123967166    REG_BINARY    01,02,03
        //        App_LastUserID_h2841727341          REG_DWORD     0x64
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.Contains("REG_", StringComparison.Ordinal))
            {
                continue;
            }
            var parts = line.Split(new[] { "    " }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            }
            if (parts.Length < 3)
            {
                continue;
            }
            var valueName = parts[0].Trim();
            var type = parts[1].Trim();
            var data = line[(line.IndexOf(type, StringComparison.Ordinal) + type.Length)..].Trim();

            switch (type)
            {
                case "REG_BINARY":
                    var bytes = data.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => byte.Parse(x.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                        .ToArray();
                    return bytes;
                case "REG_DWORD":
                    var hex = data.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? data[2..] : data;
                    if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var dw))
                    {
                        return (int)dw;
                    }
                    return defaultValue;
                default:
                    return data.Trim('"');
            }
        }
        return defaultValue;
    }

    private static string? Run(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = WineExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }
        psi.Environment["WINEPREFIX"] = WinePrefix;

        using var process = Process.Start(psi);
        if (process is null)
        {
            return null;
        }
        // stderr 必须一起排空：wine 出错时会写不少内容，管道满了子进程就卡住写、读端又等退出，两边互等。
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(15000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }
            Debug.WriteLine($"[WineRegistry] wine reg timed out after 15s: {string.Join(' ', args)}");
            return null;
        }
        string stdout = stdoutTask.GetAwaiter().GetResult();
        _ = stderrTask.GetAwaiter().GetResult();
        return process.ExitCode == 0 ? stdout : null;
    }
}
