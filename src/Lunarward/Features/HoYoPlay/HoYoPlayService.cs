using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.HoYoPlay;

public class HoYoPlayService
{


    private readonly ILogger<HoYoPlayService> _logger;

    private readonly HttpClient _httpClient;

    // 上游 0.19.0（D128）把 HoYoPlayClient 改成「一个启动器配置一个实例」：launcher_id、
    // channel、region、语言都挂在实例上，接口方法不再收这些参数。这里按 launcherId + 语言缓存。
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, HoYoPlayClient> _clients = new();

    private readonly IMemoryCache _memoryCache;


    public HoYoPlayService(ILogger<HoYoPlayService> logger, HttpClient httpClient, IMemoryCache memoryCache)
    {
        _logger = logger;
        _httpClient = httpClient;
        _memoryCache = memoryCache;
    }


    private HoYoPlayClient Client(string launcherId, string language)
    {
        return _clients.GetOrAdd($"{launcherId}|{language}", _ => new HoYoPlayClient(_httpClient)
        {
            Language = language,
            LauncherConfig = LauncherConfigOf(launcherId),
        });
    }


    private static LauncherConfig LauncherConfigOf(string launcherId)
    {
        return launcherId switch
        {
            LauncherId.ChinaOfficial => new LauncherConfig(launcherId, 1, 1, ServerRegion.miHoYo),
            LauncherId.GlobalOfficial => new LauncherConfig(launcherId, 1, 0, ServerRegion.HoYoverse),
            _ => new LauncherConfig(launcherId, 14, 0, ServerRegion.miHoYo),
        };
    }





    public async Task<GameInfo> GetGameInfoAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameInfo)}_{gameId.Id}", out GameInfo? info))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await Client(LauncherId.FromGameId(gameId)!, lang).GetGameInfoAsync(cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameInfo)}_{item.Id}", item, TimeSpan.FromMinutes(10));
            }
            info = list.FirstOrDefault(x => x == gameId);
        }
        return info!;
    }



    public async Task<List<GameInfo>> UpdateGameInfoListAsync(CancellationToken cancellationToken = default)
    {
        if (_memoryCache is MemoryCache cache)
        {
            cache.Clear();
        }
        List<GameInfo> infos = new List<GameInfo>();
        string lang = CultureInfo.CurrentUICulture.Name;
        if (LanguageUtil.FilterLanguage(lang) is "zh-cn")
        {
            infos.AddRange(await Client(LauncherId.ChinaOfficial, lang).GetGameInfoAsync(cancellationToken));
            infos.AddRange(await Client(LauncherId.GlobalOfficial, lang).GetGameInfoAsync(cancellationToken));
        }
        else
        {
            infos.AddRange(await Client(LauncherId.GlobalOfficial, lang).GetGameInfoAsync(cancellationToken));
            infos.AddRange(await Client(LauncherId.ChinaOfficial, lang).GetGameInfoAsync(cancellationToken));
        }
        foreach ((GameBiz _, string launcherId) in LauncherId.GetBilibiliLaunchers())
        {
            infos.AddRange(await Client(launcherId, lang).GetGameInfoAsync(cancellationToken));
        }
        foreach (var item in infos)
        {
            _memoryCache.Set($"{nameof(GameInfo)}_{item.Id}", item, TimeSpan.FromMinutes(10));
        }
        string json = JsonSerializer.Serialize(infos);
        AppConfig.CachedGameInfo = json;
        _ = DownloadGameVersionPosterAsync(infos);
        return infos;
    }



    private async Task DownloadGameVersionPosterAsync(List<GameInfo> infos)
    {
        try
        {
            List<string> urls = new();
            foreach (var info in infos)
            {
                if (!string.IsNullOrWhiteSpace(info.Display.Background?.Url))
                {
                    urls.Add(info.Display.Background.Url);
                }
            }
            if (AppConfig.CacheFolder is not null)
            {
                string bg = Path.Combine(AppConfig.CacheFolder, "bg");
                Directory.CreateDirectory(bg);
                await Parallel.ForEachAsync(infos, async (info, _) =>
                {
                    if (string.IsNullOrWhiteSpace(info.Display.Background?.Url))
                    {
                        return;
                    }
                    string url = info.Display.Background.Url;
                    try
                    {
                        string name = Path.GetFileName(url);
                        string path = Path.Combine(bg, name);
                        if (!File.Exists(path))
                        {
                            byte[] bytes = await _httpClient.GetByteArrayAsync(url);
                            await File.WriteAllBytesAsync(path, bytes);
                        }
                        AppConfig.SetVersionPoster(info.GameBiz, name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Download image: {url}", url);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, nameof(DownloadGameVersionPosterAsync));
        }
    }



    public async Task<GameBackgroundInfo> GetGameBackgroundAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameBackgroundInfo)}_{gameId.Id}", out GameBackgroundInfo? background))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await Client(LauncherId.FromGameId(gameId)!, lang).GetGameBackgroundAsync(cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameBackgroundInfo)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            background = list.FirstOrDefault(x => x.GameId == gameId);
        }
        return background!;
    }



    public async Task<GameContent> GetGameContentAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameContent)}_{gameId.Id}", out GameContent? content))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            content = await Client(LauncherId.FromGameId(gameId)!, lang).GetGameContentAsync(gameId, cancellationToken);
            if (content is null)
            {
                // 上游这里直接解引用，接口偶尔不返回 content 节点时会变成 NullReferenceException，
                // 日志里看不出是哪个请求。这里换成能读懂的错误，调用方本来就会 catch 并降级。
                throw new InvalidOperationException($"米哈游接口没有返回游戏内容（game={gameId.Id}, launcher={LauncherId.FromGameId(gameId)}, lang={lang}）");
            }
            _memoryCache.Set($"{nameof(GameContent)}_{content.GameId.Id}", content, TimeSpan.FromMinutes(1));
        }
        return content!;
    }



    public async Task<GamePackage> GetGamePackageAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GamePackage)}_{gameId.Id}", out GamePackage? package))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await Client(LauncherId.FromGameId(gameId)!, lang).GetGamePackageAsync([gameId], cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GamePackage)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            package = list.FirstOrDefault(x => x.GameId == gameId);
        }
        return package!;
    }



    public async Task<GameConfig?> GetGameConfigAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameConfig)}_{gameId.Id}", out GameConfig? config))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await Client(LauncherId.FromGameId(gameId)!, lang).GetGameConfigAsync([gameId], cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameConfig)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            config = list.FirstOrDefault(x => x.GameId == gameId);
        }
        // 仅星穹铁道强制使用 Chunk 作为默认下载模式
        if (config is not null && config.GameId.GameBiz.Value is GameBiz.hkrpg)
        {
            config.DefaultDownloadMode = DownloadMode.DOWNLOAD_MODE_CHUNK;
        }
        return config;
    }



    public async Task<List<GameDeprecatedFile>> GetGameDeprecatedFilesAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        var launcherId = LauncherId.FromGameId(gameId);
        if (launcherId is not null)
        {
            var configs = await Client(launcherId, "en-us").GetGameDeprecatedFileConfigAsync([gameId], cancellationToken);
            if (configs?.FirstOrDefault(x => x.GameId == gameId) is { } fileConfig)
            {
                return fileConfig.DeprecatedFiles;
            }
        }
        return [];
    }



    public async Task<GameChannelSDK?> GetGameChannelSDKAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameChannelSDK)}_{gameId.Id}", out GameChannelSDK? sdk))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await Client(LauncherId.FromGameId(gameId)!, lang).GetGameChannelSDKAsync([gameId], cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameChannelSDK)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            sdk = list.FirstOrDefault(x => x.GameId == gameId);
        }
        return sdk;
    }



    public async Task<GameBranch?> GetGameBranchAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameBranch)}_{gameId.Id}", out GameBranch? branch))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var list = await Client(LauncherId.FromGameId(gameId)!, lang).GetGameBranchAsync([gameId], cancellationToken);
            foreach (var item in list)
            {
                _memoryCache.Set($"{nameof(GameBranch)}_{item.GameId.Id}", item, TimeSpan.FromMinutes(1));
            }
            branch = list.FirstOrDefault(x => x.GameId == gameId);
        }
        return branch;
    }




    public async Task<GameSophonChunkBuild?> GetGameSophonChunkBuildAsync(GameBranch gameBranch, GameBranchPackage gameBranchPackage, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameSophonChunkBuild)}_{gameBranchPackage.PackageId}", out GameSophonChunkBuild? build))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            build = await Client(LauncherId.FromGameId(gameBranch.GameId)!, lang).GetGameSophonChunkBuildAsync(gameBranchPackage, gameBranchPackage.Tag, cancellationToken);
            _memoryCache.Set($"{nameof(GameSophonChunkBuild)}_{gameBranchPackage.PackageId}", build, TimeSpan.FromMinutes(1));
        }
        return build;
    }




    public async Task<GameSophonPatchBuild?> GetGameSophonPatchBuildAsync(GameBranch gameBranch, GameBranchPackage gameBranchPackage, CancellationToken cancellationToken = default)
    {
        if (!_memoryCache.TryGetValue($"{nameof(GameSophonPatchBuild)}_{gameBranchPackage.PackageId}", out GameSophonPatchBuild? build))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            build = await Client(LauncherId.FromGameId(gameBranch.GameId)!, lang).GetGameSophonPatchBuildAsync(gameBranchPackage, cancellationToken);
            _memoryCache.Set($"{nameof(GameSophonPatchBuild)}_{gameBranchPackage.PackageId}", build, TimeSpan.FromMinutes(1));
        }
        return build;
    }



    public async Task<List<GameDXConfig>> GetGameDXConfigsAsync(IEnumerable<GameId> gameIds, CancellationToken cancellationToken = default)
    {
        string key = $"{nameof(GameDXConfig)}_{string.Join(',', gameIds.Select(x => x.Id))}";
        if (!_memoryCache.TryGetValue(key, out List<GameDXConfig>? dxConfigs))
        {
            string lang = CultureInfo.CurrentUICulture.Name;
            var launcherId = LauncherId.FromGameId(gameIds.First())!;
            var gpuInfos = GetGPUInfos();
            dxConfigs = await Client(launcherId, lang).GetDXConfigsAsync(gpuInfos, gameIds, cancellationToken);
            _memoryCache.Set(key, dxConfigs, TimeSpan.FromMinutes(5));
        }
        return dxConfigs!;
    }


    /// <summary>
    /// Lunarward：Linux 下改用 DRM sysfs 读取显卡信息（替代 Windows SetupAPI 枚举）。
    /// 结果只用于向米哈游接口上报设备指纹，因此名称/版本取可用值即可。
    /// </summary>
    private static List<GPUInfo> GetGPUInfos()
    {
        var gpuInfos = new List<GPUInfo>();
        try
        {
            const string drmRoot = "/sys/class/drm";
            if (!Directory.Exists(drmRoot))
            {
                return gpuInfos;
            }

            // B05：.NET 的 searchPattern 只支持 * 和 ?，"[0-9]" 会被当成字面量，永远匹配不到；
            // 这里枚举后自己用正则筛 card0/card1…（不含 card0-DP-1 这类连接器目录）。
            foreach (var cardDir in Directory.EnumerateDirectories(drmRoot))
            {
                if (!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(cardDir), "^card\\d+$"))
                {
                    continue;
                }
                var ueventPath = Path.Combine(cardDir, "device", "uevent");
                if (!File.Exists(ueventPath))
                {
                    continue;
                }

                string? driver = null;
                string? pciId = null;
                foreach (var line in File.ReadLines(ueventPath))
                {
                    if (line.StartsWith("DRIVER=", StringComparison.Ordinal))
                    {
                        driver = line["DRIVER=".Length..].Trim();
                    }
                    else if (line.StartsWith("PCI_ID=", StringComparison.Ordinal))
                    {
                        pciId = line["PCI_ID=".Length..].Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(driver))
                {
                    continue;
                }

                string version = "";
                var moduleVersion = $"/sys/module/{driver}/version";
                if (File.Exists(moduleVersion))
                {
                    try
                    {
                        version = File.ReadAllText(moduleVersion).Trim();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                }

                gpuInfos.Add(new GPUInfo
                {
                    Name = string.IsNullOrEmpty(pciId) ? driver : $"{driver} ({pciId})",
                    DriverVersion = version,
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        return gpuInfos;
    }


}
