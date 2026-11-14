// Lunarward：Linux 下没有 Windows 注册表。米哈游的账号票据（MIHOYOSDK_ADL_*）实际保存在
// wine 前缀的注册表里，这里通过 `wine reg` 读写对应前缀，语义与上游 Registry.GetValue/SetValue 对齐。
//
// 性能注意（L-127）：每次 `wine reg` 都是一次 fork + wineserver 通信，实测冷 1.1 s、热 0.13–0.2 s，
// 在 UI 线程上同步调用时会整窗卡住（曾实测 4.1 s）。因此：
//   1. 读取按「整个键」缓存，一次 `reg query <key>` 取回所有值，N 个值只 fork 一次；
//   2. UI 路径一律走 *Async 版本，绝不阻塞 UI 线程；
//   3. 写入后立刻失效该键缓存，5 分钟 TTL 兜底（空结果 60 s）。
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

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
                return Features.Runtime.PrefixManager.ExpandPath(_scopedPrefix) ?? _scopedPrefix!;
            }
            // 统一走 ExpandPath 去掉尾斜杠：带尾斜杠的 WINEPREFIX 会让 wine 直接退出（实测绝区零起不来）。
            string? env = Features.Runtime.PrefixManager.ExpandPath(Environment.GetEnvironmentVariable("LUNARWARD_WINEPREFIX"));
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env!;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".wine");
        }
    }

    /// <summary>wine 可执行文件。后续由运行环境（Profile 的 runner）注入。</summary>
    public static string WineExecutable =>
        Starward.Features.GameLauncher.GameLauncherService.ResolveWine() ?? "wine";


    #region 缓存

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    /// <summary>键不存在时只缓存 60 s，避免游戏刚写入票据后读不到。</summary>
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromSeconds(60);

    private const int SlowCallMs = 800;

    private sealed class CacheEntry
    {
        public DateTime TimestampUtc { get; init; }
        public TimeSpan Ttl { get; init; }
        public Dictionary<string, object?> Values { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.Ordinal);


    /// <summary>清空缓存；传 key 只清该键，传 prefix 只清该前缀。</summary>
    public static void InvalidateCache(string? key = null, string? prefix = null)
    {
        if (key is null && prefix is null)
        {
            _cache.Clear();
            return;
        }
        foreach (var pair in _cache)
        {
            if (key is not null && !pair.Key.EndsWith("|" + NormalizeKey(key), StringComparison.Ordinal))
            {
                continue;
            }
            if (prefix is not null && !pair.Key.StartsWith(prefix + "|", StringComparison.Ordinal))
            {
                continue;
            }
            _cache.TryRemove(pair.Key, out _);
        }
    }

    #endregion


    #region 读取

    /// <summary>同步读取单个值。<b>不要在 UI 线程调用</b>，UI 路径请用 <see cref="GetValueAsync"/>。</summary>
    public static object? GetValue(string key, string name, object? defaultValue = null)
    {
        try
        {
            var values = GetKeyValues(key);
            return values.TryGetValue(name, out object? value) ? value : defaultValue;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[WineRegistry] GetValue failed: {Key}\\{Name}", key, name);
            return defaultValue;
        }
    }


    public static Task<object?> GetValueAsync(string key, string name, object? defaultValue = null, string? winePrefix = null, CancellationToken cancellationToken = default)
        => GetValueCoreAsync(key, name, defaultValue, winePrefix, cancellationToken);


    public static Task<object?> GetValueAsync(string key, string name, object? defaultValue, CancellationToken cancellationToken)
        => GetValueCoreAsync(key, name, defaultValue, null, cancellationToken);


    private static async Task<object?> GetValueCoreAsync(string key, string name, object? defaultValue, string? winePrefix, CancellationToken cancellationToken)
    {
        try
        {
            var values = await GetKeyValuesAsync(key, winePrefix, cancellationToken).ConfigureAwait(false);
            return values.TryGetValue(name, out object? value) ? value : defaultValue;
        }
        catch (OperationCanceledException)
        {
            return defaultValue;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[WineRegistry] GetValueAsync failed: {Key}\\{Name}", key, name);
            return defaultValue;
        }
    }


    /// <summary>一次 `reg query` 取回整键所有值（含缓存）。</summary>
    public static Dictionary<string, object?> GetKeyValues(string key)
        => GetKeyValuesAsync(key, null, CancellationToken.None).GetAwaiter().GetResult();


    public static Task<Dictionary<string, object?>> GetKeyValuesAsync(string key, CancellationToken cancellationToken)
        => GetKeyValuesAsync(key, null, cancellationToken);


    public static async Task<Dictionary<string, object?>> GetKeyValuesAsync(string key, string? winePrefix = null, CancellationToken cancellationToken = default)
    {
        // 前缀必须在任何 await 之前取好：UsePrefix 是 [ThreadStatic]，异步续体可能换线程（L-127）。
        string prefix = string.IsNullOrWhiteSpace(winePrefix) ? WinePrefix : winePrefix!;
        string normalizedKey = NormalizeKey(key);
        string cacheKey = prefix + "|" + normalizedKey;

        if (_cache.TryGetValue(cacheKey, out CacheEntry? cached) && DateTime.UtcNow - cached.TimestampUtc < cached.Ttl)
        {
            return cached.Values;
        }

        var stopwatch = Stopwatch.StartNew();
        string? output = await RunAsync(prefix, cancellationToken, "reg", "query", normalizedKey).ConfigureAwait(false);
        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds >= SlowCallMs)
        {
            Log.Warning("[WineRegistry] wine reg query {Key} 耗时 {Elapsed} ms（每次都是一次 fork，UI 路径应改异步）", normalizedKey, stopwatch.ElapsedMilliseconds);
        }

        var values = output is null ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) : ParseValues(output);
        bool missing = output is null || values.Count == 0;
        _cache[cacheKey] = new CacheEntry
        {
            TimestampUtc = DateTime.UtcNow,
            Ttl = missing ? NegativeCacheTtl : CacheTtl,
            Values = values,
        };
        return values;
    }

    #endregion


    #region 写入

    /// <summary>同步写入单个值。<b>不要在 UI 线程调用</b>。</summary>
    public static void SetValue(string key, string name, object value)
        => SetValueAsync(key, name, value).GetAwaiter().GetResult();


    public static Task SetValueAsync(string key, string name, object value, CancellationToken cancellationToken)
        => SetValueAsync(key, name, value, null, cancellationToken);


    public static async Task SetValueAsync(string key, string name, object value, string? winePrefix = null, CancellationToken cancellationToken = default)
    {
        try
        {
            string[] args = BuildAddArgs(key, name, value);
            await RunAsync(winePrefix ?? WinePrefix, cancellationToken, args).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[WineRegistry] SetValue failed: {Key}\\{Name}", key, name);
        }
        finally
        {
            InvalidateCache(key);
        }
    }


    /// <summary>
    /// 一次写入多个值：生成临时 .reg 文件后用 `wine reg import` 一次导入，N 次 fork 变 1 次。
    /// 键必须能写成 .reg 的完整路径（HKEY_CURRENT_USER\... 或 HKEY_LOCAL_MACHINE\...）。
    /// </summary>
    public static Task<bool> SetValuesAsync(string key, IReadOnlyDictionary<string, object> values, CancellationToken cancellationToken)
        => SetValuesAsync(key, values, null, cancellationToken);


    public static async Task<bool> SetValuesAsync(string key, IReadOnlyDictionary<string, object> values, string? winePrefix = null, CancellationToken cancellationToken = default)
    {
        if (values.Count == 0)
        {
            return true;
        }
        string? tempFile = null;
        try
        {
            tempFile = Path.Combine(Path.GetTempPath(), $"lunarward-{Guid.NewGuid():N}.reg");
            // 必须带 UTF-16LE BOM：wine 的 reg import 靠 BOM 判定编码，没有 BOM 会当成 ANSI 并「导入成功但一个值都没写」（实测）。
            byte[] regBytes = RegistryFormat.BuildRegFileBytes(key, values);
            await File.WriteAllBytesAsync(tempFile, regBytes, cancellationToken).ConfigureAwait(false);
            string? output = await RunAsync(winePrefix ?? WinePrefix, cancellationToken, "reg", "import", tempFile).ConfigureAwait(false);
            return output is not null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "[WineRegistry] 批量写入注册表失败：{Key}", key);
            return false;
        }
        finally
        {
            InvalidateCache(key);
            if (tempFile is not null)
            {
                try { File.Delete(tempFile); } catch { }
            }
        }
    }

    #endregion


    private static string[] BuildAddArgs(string key, string name, object value)
    {
        switch (value)
        {
            case byte[] bytes:
                return ["reg", "add", NormalizeKey(key), "/v", name, "/t", "REG_BINARY", "/d", ToHex(bytes), "/f"];
            case int or long or uint:
                var dword = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
                return ["reg", "add", NormalizeKey(key), "/v", name, "/t", "REG_DWORD", "/d", $"0x{dword:x}", "/f"];
            default:
                return ["reg", "add", NormalizeKey(key), "/v", name, "/t", "REG_SZ", "/d", value?.ToString() ?? "", "/f"];
        }
    }

    // 下面是 RegistryFormat 的薄包装：纯函数放在 Features/GameAccount/RegistryFormat.cs，便于单元测试单独链接。
    private static string NormalizeKey(string key) => RegistryFormat.NormalizeKey(key);


    private static string ToHex(byte[] bytes) => RegistryFormat.ToHex(bytes);


    internal static Dictionary<string, object?> ParseValues(string output) => RegistryFormat.ParseValues(output);


    internal static string BuildRegFile(string key, IReadOnlyDictionary<string, object> values) => RegistryFormat.BuildRegFile(key, values);


    private static async Task<string?> RunAsync(string prefix, CancellationToken cancellationToken, params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = WineExecutable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }
        psi.Environment["WINEPREFIX"] = prefix;

        using var process = Process.Start(psi);
        if (process is null)
        {
            return null;
        }
        // stderr 必须一起排空：wine 出错时会写不少内容，管道满了子进程就卡住写、读端又等退出，两边互等。
        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch { }
            Log.Warning("[WineRegistry] wine reg 超过 15 秒未返回：{Args}", string.Join(' ', args));
            return null;
        }
        string stdout = await stdoutTask.ConfigureAwait(false);
        string stderr = await stderrTask.ConfigureAwait(false);
        if (process.ExitCode != 0)
        {
            Log.Debug("[WineRegistry] wine reg 退出码 {Code}：{Args} {Error}", process.ExitCode, string.Join(' ', args), stderr.Trim());
            return null;
        }
        return stdout;
    }
}
