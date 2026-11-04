// Lunarward：D5 —— Linux 不自建安装器，自更新走「发行版包管理器 / AppImage」。
// 这里保留与上游一致的签名（契约照抄上游，见 13-lessons L-06），做两件事：
//   1. 查 AUR 的 `lunarward` 包版本，比当前版本新就如实报告（配置项决定包名，默认 lunarward）；
//   2. 「立即更新」不做自我替换，只提示去包管理器更新（D5）。
// 任何网络异常都当作「无更新」，绝不把失败伪装成成功（D33）。
using Starward.Setup.Core;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Update;

public class UpdateService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };


    /// <summary>AUR 包名；留空或没装 AUR 时返回「无更新」。</summary>
    public static string AurPackageName { get; set; } = "lunarward";


    public async Task<ReleaseInfoDetail?> CheckUpdateAsync(bool disableIgnore = false)
        => await GetLatestVersionAsync();


    public async Task<ReleaseInfoDetail?> GetLatestVersionAsync(CancellationToken cancellation = default)
    {
        string? latest = await GetPackageVersionAsync(cancellation);
        if (string.IsNullOrWhiteSpace(latest))
        {
            return null;
        }
        string current = string.IsNullOrWhiteSpace(AppConfig.AppVersion) || AppConfig.AppVersion == "0.0.0.0"
            ? System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0"
            : AppConfig.AppVersion;
        return IsNewer(latest, current)
            ? new ReleaseInfoDetail
            {
                Version = latest,
                BuildTime = DateTimeOffset.Now,
            }
            : null;
    }


    /// <summary>AUR RPC：`https://aur.archlinux.org/rpc/v5/info?arg[]=lunarward`。</summary>
    public static async Task<string?> GetPackageVersionAsync(CancellationToken cancellation = default, string? packageName = null)
    {
        string package = string.IsNullOrWhiteSpace(packageName) ? AurPackageName : packageName;
        if (string.IsNullOrWhiteSpace(package))
        {
            return null;
        }
        try
        {
            string url = $"https://aur.archlinux.org/rpc/v5/info?arg[]={Uri.EscapeDataString(package)}";
            string json = await _http.GetStringAsync(url, cancellation);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("results", out JsonElement results) || results.GetArrayLength() == 0)
            {
                return null;
            }
            string? full = results[0].TryGetProperty("Version", out JsonElement version) ? version.GetString() : null;
            // AUR 版本形如 `0.2.0-1`，去掉 pkgrel
            return full?.Split('-')[0];
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "AUR version check failed");
            return null;
        }
    }


    private static bool IsNewer(string latest, string? current)
    {
        if (Version.TryParse(Normalize(latest), out Version? v1) && Version.TryParse(Normalize(current), out Version? v2))
        {
            return v1 > v2;
        }
        return false;
    }


    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "0.0.0";
        }
        int dash = value.IndexOf('-');
        return dash > 0 ? value[..dash] : value;
    }


    public Task DownloadSetupAsync(ReleaseInfo release)
    {
        Debug.WriteLine("[UpdateService] 自更新在 Linux 上未实现（D5）");
        return Task.CompletedTask;
    }
}
