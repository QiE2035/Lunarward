// Lunarward：Sophon 清单获取（Chunk 下载模式）。
//
// 复用 `Starward.Core` 的 `HoYoPlayClient` 拿 build 信息；清单文件本身是 zstd 压缩的 protobuf，
// 这里负责下载、校验、解压、解析，并缓存到 `~/.cache/lunarward/sophon`。

using Microsoft.Extensions.Logging;
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ZstdSharp;

namespace Starward.Features.GameInstall.Sophon;


public sealed class SophonBuildInfo
{
    public required GameBranch Branch { get; init; }

    public required GameBranchPackage Package { get; init; }

    public required GameSophonChunkBuild Build { get; init; }

    /// <summary>清单类别（matching_field）→ 文件列表。</summary>
    public required Dictionary<string, List<SophonChunkFile>> Manifests { get; init; }

    /// <summary>清单类别 → chunk 下载前缀。</summary>
    public required Dictionary<string, string> ChunkUrlPrefixes { get; init; }

    public long TotalUncompressedBytes => Manifests.Values.SelectMany(x => x).Where(x => !x.IsFolder).Sum(x => x.Size);

    /// <summary>
    /// 需要下载的字节数（按 chunk 去重后的压缩体积）。
    /// </summary>
    public long TotalCompressedBytes => Manifests.Values
                                                 .SelectMany(x => x)
                                                 .Where(x => !x.IsFolder)
                                                 .SelectMany(x => x.Chunks)
                                                 .GroupBy(x => x.Id)
                                                 .Sum(g => g.First().CompressedSize);
}


public sealed class SophonClient
{

    private readonly HoYoPlayClient _client;

    private readonly ILogger? _logger;

    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromMinutes(10) };


    public SophonClient(HoYoPlayClient client, ILogger? logger = null)
    {
        _client = client;
        _logger = logger;
    }


    public static string CacheFolder => Path.Combine(
        Environment.GetEnvironmentVariable("XDG_CACHE_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache"),
        "lunarward", "sophon");


    /// <summary>
    /// 取当前版本（或预下载）的完整清单。
    /// </summary>
    /// <param name="gameId">游戏</param>
    /// <param name="preDownload">是否取预下载分支</param>
    /// <param name="audioFields">要包含的语音包类别（如 zh-cn、en-us）</param>
    public async Task<SophonBuildInfo?> GetBuildInfoAsync(GameId gameId, bool preDownload, IEnumerable<string> audioFields, CancellationToken cancellationToken = default, string? tagOverride = null)
    {
        string launcherId = LauncherId.FromGameId(gameId) ?? throw new ArgumentException($"无法映射 launcherId：{gameId.GameBiz}");
        GameBranch? branch = await _client.GetGameBranchAsync(launcherId, "zh-cn", gameId, cancellationToken);
        if (branch is null)
        {
            _logger?.LogWarning("Sophon: no branch for {game}", gameId.GameBiz);
            return null;
        }
        GameBranchPackage? package = preDownload ? branch.PreDownload : branch.Main;
        if (package is null)
        {
            _logger?.LogInformation("Sophon: {game} has no {mode} package", gameId.GameBiz, preDownload ? "predownload" : "main");
            return null;
        }
        return await GetBuildInfoForPackageAsync(branch, package, preDownload, audioFields, cancellationToken, tagOverride);
    }


    /// <summary>
    /// 用已经拿到的 branch/package 取清单（组合编排里补丁与复用要保证同一个 package）。
    /// </summary>
    public async Task<SophonBuildInfo?> GetBuildInfoForPackageAsync(GameBranch branch, GameBranchPackage package, bool preDownload, IEnumerable<string> audioFields, CancellationToken cancellationToken = default, string? tagOverride = null)
    {

        // 用我们自己的 URL（与 SophonLibrary 一致）：上游 GetGameSophonChunkBuildAsync 在
        // 预下载 package 上会返回 -202，尚未查明原因（L-59 补充）。
        string buildUrl = SophonLibrary.BuildUrl(package, preDownload, tagOverride);
        string buildJson = await _httpClient.GetStringAsync(buildUrl, cancellationToken);
        GameSophonChunkBuild build;
        // getBuild 的响应外面包了一层 `{ "retcode": 0, "message": "OK", "data": { "build_id": … } }`；
        // 上游 HoYoPlayClient 用 CommonGetAsync 取 `data` 节点，我们这里要自己拆（L-61）。
        using (System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(buildJson))
        {
            if (!document.RootElement.TryGetProperty("data", out System.Text.Json.JsonElement dataElement))
            {
                throw new InvalidOperationException($"清单响应缺少 data 节点：{buildUrl}");
            }
            build = System.Text.Json.JsonSerializer.Deserialize<GameSophonChunkBuild>(dataElement.GetRawText(), new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                    ?? throw new InvalidOperationException($"清单响应无法解析：{buildUrl}");
        }
        var manifests = new Dictionary<string, List<SophonChunkFile>>(StringComparer.OrdinalIgnoreCase);
        var prefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var wanted = new HashSet<string>(audioFields, StringComparer.OrdinalIgnoreCase) { "game" };
        foreach (GameSophonChunkManifest manifest in build.Manifests ?? new List<GameSophonChunkManifest>())
        {
            if (!wanted.Contains(manifest.MatchingField))
            {
                continue;
            }
            List<SophonChunkFile> files = await GetManifestFilesAsync(manifest, cancellationToken);
            manifests[manifest.MatchingField] = files;
            prefixes[manifest.MatchingField] = manifest.ChunkDownload.UrlPrefix;
            _logger?.LogInformation("Sophon: manifest {field} = {count} files", manifest.MatchingField, files.Count);
        }
        if (manifests.Count == 0)
        {
            return null;
        }
        return new SophonBuildInfo { Branch = branch, Package = package, Build = build, Manifests = manifests, ChunkUrlPrefixes = prefixes };
    }


    /// <summary>
    /// 预下载的实际下载体积：用 getPatchBuild 的 stats（key = 本地版本）。
    /// 注意 getBuild 返回的是「完整游戏」清单（136 GB 量级），预下载只差几十 MB 到几 GB，
    /// 直接拿它当下载量会让用户以为要重下整包。
    /// </summary>
    public async Task<long?> GetPatchDownloadBytesAsync(GameId gameId, string localVersion, IEnumerable<string> audioFields, bool preDownload = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(localVersion))
        {
            return null;
        }
        string launcherId = LauncherId.FromGameId(gameId) ?? throw new ArgumentException($"无法映射 launcherId：{gameId.GameBiz}");
        GameBranch? branch = await _client.GetGameBranchAsync(launcherId, "zh-cn", gameId, cancellationToken);
        GameBranchPackage? package = preDownload ? branch?.PreDownload : branch?.Main;
        if (package is null)
        {
            return null;
        }
        string url = SophonLibrary.PatchUrl(package, preDownload);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, cancellationToken);
        string json = await response.Content.ReadAsStringAsync(cancellationToken);
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out System.Text.Json.JsonElement data)
            || data.ValueKind is System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined)
        {
            return null;
        }
        var wanted = new HashSet<string>(audioFields, StringComparer.OrdinalIgnoreCase) { "game" };
        long total = 0;
        bool found = false;
        foreach (System.Text.Json.JsonElement manifest in data.GetProperty("manifests").EnumerateArray())
        {
            string field = manifest.GetProperty("matching_field").GetString() ?? "";
            if (!wanted.Contains(field) || !manifest.TryGetProperty("stats", out System.Text.Json.JsonElement stats))
            {
                continue;
            }
            if (stats.TryGetProperty(localVersion, out System.Text.Json.JsonElement stat)
                && stat.TryGetProperty("compressed_size", out System.Text.Json.JsonElement size)
                && long.TryParse(size.GetString(), out long bytes))
            {
                total += bytes;
                found = true;
            }
        }
        _logger?.LogInformation("Sophon: patch from {version} = {bytes} bytes (found={found})", localVersion, total, found);
        return found ? total : null;
    }


    /// <summary>下载、解压、解析一个类别的清单。</summary>
    private async Task<List<SophonChunkFile>> GetManifestFilesAsync(GameSophonChunkManifest manifest, CancellationToken cancellationToken)
    {
        byte[] compressed = await DownloadManifestAsync(manifest, cancellationToken);
        byte[] raw;
        using (var input = new MemoryStream(compressed))
        using (var decompressor = new DecompressionStream(input))
        using (var output = new MemoryStream())
        {
            await decompressor.CopyToAsync(output, cancellationToken);
            raw = output.ToArray();
        }

        string md5 = Convert.ToHexString(MD5.HashData(raw));
        if (!string.IsNullOrEmpty(manifest.Manifest.Checksum)
            && !string.Equals(md5, manifest.Manifest.Checksum, StringComparison.OrdinalIgnoreCase))
        {
            // 校验失败就丢掉缓存，下次重新下载，避免一直用坏文件。
            TryDelete(CachePathFor(manifest.Manifest.Id));
            throw new InvalidDataException($"Sophon 清单校验失败（{manifest.MatchingField}）：期望 {manifest.Manifest.Checksum}，实际 {md5}");
        }
        return SophonChunkManifestParser.Parse(raw);
    }


    private async Task<byte[]> DownloadManifestAsync(GameSophonChunkManifest manifest, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(CacheFolder);
        string cachePath = CachePathFor(manifest.Manifest.Id);
        if (File.Exists(cachePath))
        {
            var info = new FileInfo(cachePath);
            if (info.Length == manifest.Manifest.CompressedSize)
            {
                return await File.ReadAllBytesAsync(cachePath, cancellationToken);
            }
            TryDelete(cachePath);
        }

        string url = $"{manifest.ManifestDownload.UrlPrefix.TrimEnd('/')}/{manifest.Manifest.Id}";
        _logger?.LogInformation("Sophon: download manifest {url} ({size} B)", url, manifest.Manifest.CompressedSize);
        byte[] bytes = await _httpClient.GetByteArrayAsync(url, cancellationToken);
        if (bytes.Length != manifest.Manifest.CompressedSize)
        {
            _logger?.LogWarning("Sophon: manifest size mismatch, expect {expect}, got {actual}", manifest.Manifest.CompressedSize, bytes.Length);
        }
        await File.WriteAllBytesAsync(cachePath, bytes, cancellationToken);
        return bytes;
    }


    private static string CachePathFor(string id) => Path.Combine(CacheFolder, id);


    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // 缓存删不掉不影响结果
        }
    }


    /// <summary>
    /// 本地语音包目录名（Chinese/English/Japanese/Korean）→ Sophon 清单的 matching_field。
    /// </summary>
    public static string? MapAudioField(string audioName)
    {
        return audioName.ToLowerInvariant() switch
        {
            "chinese" or "zh-cn" or "zh_cn" => "zh-cn",
            "english" or "en-us" or "en_us" => "en-us",
            "japanese" or "ja-jp" or "ja_jp" => "ja-jp",
            "korean" or "ko-kr" or "ko_kr" => "ko-kr",
            _ => null,
        };
    }


    /// <summary>
    /// 从本地安装目录推断已安装的语音包（`Audio_{lang}_pkg_version`），用于只比对对应类别。
    /// </summary>
    public static List<string> DetectInstalledAudioFields(string installPath)
    {
        var result = new List<string>();
        try
        {
            foreach (string file in Directory.EnumerateFiles(installPath, "Audio_*_pkg_version"))
            {
                string name = Path.GetFileNameWithoutExtension(file); // Audio_zh-cn_pkg_version -> Audio_zh-cn
                if (name.StartsWith("Audio_", StringComparison.OrdinalIgnoreCase))
                {
                    string lang = name["Audio_".Length..];
                    if (lang.EndsWith("_pkg_version", StringComparison.OrdinalIgnoreCase))
                    {
                        lang = lang[..^"_pkg_version".Length];
                    }
                    if (MapAudioField(lang) is string field)
                    {
                        result.Add(field);
                    }
                }
            }
        }
        catch
        {
            // 目录不可读时当作没有语音包
        }
        return result;
    }


    /// <summary>
    /// 读本地版本（`config.ini` 的 `game_version=`，与启动器判定一致）。
    /// </summary>
    public static string? TryReadLocalVersion(string installPath)
    {
        string config = Path.Combine(installPath, "config.ini");
        if (!File.Exists(config))
        {
            return null;
        }
        string? version = null;
        foreach (string line in File.ReadLines(config))
        {
            if (line.StartsWith("game_version=", StringComparison.Ordinal))
            {
                version = line["game_version=".Length..].Trim();
            }
        }
        return string.IsNullOrEmpty(version) ? null : version;
    }
}
