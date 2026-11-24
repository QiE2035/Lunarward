// Lunarward：受管组件的下载源（D73 §2.3）。
//
// 源是「可配置的清单」，不写死任何私有 API：默认三条走 GitHub Releases，
// 用户可以在 components-sources.json 里加自己的仓库或填镜像前缀。
// 这里只负责「列出版本 / 给出 asset URL / 给出期望 digest」，下载在 ComponentDownloader。
using Microsoft.Extensions.Logging;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Runtime;


public sealed class ComponentSourceEntry
{
    /// <summary>在配置里唯一。</summary>
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>wine / proton / graphics</summary>
    public string Kind { get; set; } = "wine";

    public string Owner { get; set; } = "";

    public string Repo { get; set; } = "";

    /// <summary>asset 文件名的正则；不写则按扩展名过滤。</summary>
    public string AssetPattern { get; set; } = "";

    /// <summary>镜像前缀，默认空 = 直连。</summary>
    public string MirrorPrefix { get; set; } = "";

    /// <summary>界面上的推荐顺序，越小越前（wine-staging 默认第一）。</summary>
    public int Rank { get; set; } = 50;

    /// <summary>D110：勾掉就不出现在「添加组件」的源列表里；源管理页仍显示。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>D111：内置源被删除时留墓碑，防止下次 Load 又从 Defaults 合并回来。</summary>
    public bool Removed { get; set; }
}


public sealed class ComponentReleaseAsset
{
    public string SourceId { get; init; } = "";

    public string ReleaseTag { get; init; } = "";

    public string Name { get; init; } = "";

    public string Url { get; init; } = "";

    public long Size { get; init; }

    /// <summary>源端给的 sha256（有的仓库没有）。</summary>
    public string? Sha256 { get; init; }

    /// <summary>从文件名 / tag 猜出来的版本串，用于展示与目录名。</summary>
    public string Version { get; init; } = "";

    public string Display => string.IsNullOrWhiteSpace(Version) ? Name : $"{Version}  ({Size / 1024 / 1024} MB)";
}


public static class ComponentSources
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };


    /// <summary>默认源：wine-staging 排第一（用户定的一键默认项），wine-tkg 第二，GE-Proton 第三。</summary>
    public static IReadOnlyList<ComponentSourceEntry> Defaults { get; } = new List<ComponentSourceEntry>
    {
        new()
        {
            Id = "wine-staging",
            DisplayName = "wine-staging（预编译）",
            Kind = "wine",
            Owner = "Kron4ek",
            Repo = "Wine-Builds",
            AssetPattern = @"wine-staging-[\d.]+-(amd64|x86_64)\.tar\.(xz|zst|gz)$",
            Rank = 10,
        },
        new()
        {
            Id = "wine-tkg",
            DisplayName = "wine-tkg（预编译）",
            Kind = "wine",
            Owner = "Kron4ek",
            Repo = "Wine-Builds",
            AssetPattern = @"wine-tkg-[\d.]+-(amd64|x86_64)\.tar\.(xz|zst|gz)$",
            Rank = 20,
        },
        new()
        {
            Id = "ge-proton",
            DisplayName = "GE-Proton（需要 Steam 或 umu）",
            Kind = "proton",
            Owner = "GloriousEggroll",
            Repo = "proton-ge-custom",
            AssetPattern = @"GE-Proton[\d-]+\.tar\.(xz|gz|zst)$",
            Rank = 30,
        },
        new()
        {
            Id = "dxvk",
            DisplayName = "DXVK",
            Kind = "graphics",
            Owner = "doitsujin",
            Repo = "dxvk",
            AssetPattern = @"dxvk-[\d.]+\.tar\.(gz|xz|zst)$",
            Rank = 40,
        },
        new()
        {
            Id = "vkd3d-proton",
            DisplayName = "VKD3D-Proton",
            Kind = "graphics",
            Owner = "HansKristian-Work",
            Repo = "vkd3d-proton",
            AssetPattern = @"vkd3d-proton-[\d.]+\.tar\.(zst|xz|gz)$",
            Rank = 50,
        },
    };


    /// <summary>$XDG_CONFIG_HOME|~/.config/lunarward/components-sources.json</summary>
    public static string FilePath
    {
        get
        {
            string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(config, "lunarward", "components-sources.json");
        }
    }


    /// <summary>默认 + 用户覆盖（按 Id 覆盖、可追加）。</summary>
    public static IReadOnlyList<ComponentSourceEntry> Load()
    {
        List<ComponentSourceEntry> list = Defaults.Select(Clone).ToList();
        if (!File.Exists(FilePath))
        {
            return list.OrderBy(x => x.Rank).ToList();
        }
        List<ComponentSourceEntry>? user;
        try
        {
            user = JsonFile.ReadOrRecover<List<ComponentSourceEntry>>(FilePath, JsonOptions);
        }
        catch
        {
            user = null;
        }
        if (user is null)
        {
            return list.OrderBy(x => x.Rank).ToList();
        }
        foreach (ComponentSourceEntry entry in user)
        {
            int index = list.FindIndex(x => x.Id == entry.Id);
            if (index >= 0)
            {
                list[index] = entry;
            }
            else if (!entry.Removed)
            {
                list.Add(entry);
            }
        }
        // D111：墓碑（Removed）只写不显示；内置源被删后不会再从 Defaults 冒出来。
        return list.Where(x => !x.Removed).OrderBy(x => x.Rank).ToList();
    }


    /// <summary>D110：只给下载弹窗用的列表（按 Rank 排好、去掉被禁用的源）。</summary>
    public static IReadOnlyList<ComponentSourceEntry> LoadEnabled() => Load().Where(x => x.Enabled).ToList();


    /// <summary>D110：删掉用户覆盖，回到内置默认。</summary>
    public static void ResetUserEntries()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
        catch (IOException)
        {
            // 删不掉就保持原状，下次保存再试。
        }
    }


    /// <summary>D111：这个 Id 是不是内置源（删除内置源要留墓碑）。</summary>
    public static bool IsDefault(string? id) => id is not null && Defaults.Any(x => x.Id == id);


    /// <summary>D111：新增自定义源的 Id。</summary>
    public static string NewCustomId() => "user-" + Guid.NewGuid().ToString("N")[..8];


    /// <summary>D111：仓库写法的粗校验（owner/repo 分开，不能含空白与斜杠）。</summary>
    public static bool IsValidRepoPart(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsWhiteSpace) && !value.Contains('/');


    /// <summary>D110：镜像前缀要么空（直连），要么是 http(s) 且以 / 结尾的基地址。</summary>
    public static bool IsValidMirrorPrefix(string? mirrorPrefix)
    {
        if (string.IsNullOrWhiteSpace(mirrorPrefix))
        {
            return true;
        }
        return Uri.TryCreate(mirrorPrefix, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            && mirrorPrefix.EndsWith('/');
    }


    public static void SaveUserEntries(IEnumerable<ComponentSourceEntry> entries)
    {
        // R103：与内置默认逐字段相同的条目不落盘。以前保存一次就把「当时的默认值」固化成用户副本，
        // 以后升级默认源（换仓库 / 换镜像 / 改过滤）对老用户永远不生效。
        List<ComponentSourceEntry> diff = entries.Where(entry => !SameAsDefault(entry)).ToList();
        JsonFile.WriteAtomic(FilePath, diff, JsonOptions);
    }


    /// <summary>R103：这个条目和同 Id 的内置默认是不是完全一样（用于「只持久化差异」）。</summary>
    public static bool SameAsDefault(ComponentSourceEntry entry)
    {
        ComponentSourceEntry? def = Defaults.FirstOrDefault(x => x.Id == entry.Id);
        if (def is null)
        {
            return false;
        }
        return string.Equals(entry.DisplayName, def.DisplayName, StringComparison.Ordinal)
            && string.Equals(entry.Kind, def.Kind, StringComparison.Ordinal)
            && string.Equals(entry.Owner, def.Owner, StringComparison.Ordinal)
            && string.Equals(entry.Repo, def.Repo, StringComparison.Ordinal)
            && string.Equals(entry.AssetPattern, def.AssetPattern, StringComparison.Ordinal)
            && string.Equals(entry.MirrorPrefix, def.MirrorPrefix, StringComparison.Ordinal)
            && entry.Rank == def.Rank
            && entry.Enabled == def.Enabled
            && entry.Removed == def.Removed;
    }


    private static ComponentSourceEntry Clone(ComponentSourceEntry source) => new()
    {
        Id = source.Id,
        DisplayName = source.DisplayName,
        Kind = source.Kind,
        Owner = source.Owner,
        Repo = source.Repo,
        AssetPattern = source.AssetPattern,
        MirrorPrefix = source.MirrorPrefix,
        Rank = source.Rank,
        Enabled = source.Enabled,
        Removed = source.Removed,
    };


    /// <summary>
    /// 列出某个源的候选 asset（新的在前）。走 GitHub Releases API，不爬网页。
    /// 单个仓库失败只影响它自己，不抛给调用方。
    /// </summary>
    /// <summary>
    /// 把 GitHub 的错误响应翻译成一句人能看懂的话（不猜、不改写原文，只补上下文）。
    /// 纯函数，单独可测。
    /// </summary>
    public static string DescribeFailure(int statusCode, string? body)
    {
        string trimmed = (body ?? "").Trim();
        if (trimmed.Length > 200)
        {
            trimmed = trimmed[..200] + "…";
        }
        if (statusCode is 403 or 429)
        {
            return $"GitHub 拒绝了这个请求（HTTP {statusCode}）。匿名访问每小时只有 60 次，共享出口（代理 / NAT）很容易被算满；"
                + "可以设置环境变量 LUNARWARD_GITHUB_TOKEN，或在 components-sources.json 里给该源配镜像前缀后重试。"
                + (trimmed.Length > 0 ? $"\n原文：{trimmed}" : "");
        }
        if (statusCode == 404)
        {
            return $"这个仓库或 API 地址不存在（HTTP 404）：核对源配置里的 Owner / Repo。" + (trimmed.Length > 0 ? $"\n原文：{trimmed}" : "");
        }
        return $"列版本失败（HTTP {statusCode}）。" + (trimmed.Length > 0 ? $"原文：{trimmed}" : "");
    }


    /// <summary>D112：发布列表短期缓存（10 分钟）+ ETag，重复打开弹窗不再打 GitHub。</summary>
    private static readonly TimeSpan AssetCacheTtl = TimeSpan.FromMinutes(10);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (DateTimeOffset At, string? ETag, IReadOnlyList<ComponentReleaseAsset> Items)> AssetCache = new();

    /// <summary>R105：缓存上限。以前只增不减，切镜像/改模式久了会把过期条目全留在内存里。</summary>
    private const int AssetCacheLimit = 64;


    /// <summary>R105：清掉过期条目；还超上限就按时间淘汰最旧的。调用方拿到的是已收敛的缓存。</summary>
    private static void TrimAssetCache()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (var pair in AssetCache)
        {
            if (now - pair.Value.At >= AssetCacheTtl)
            {
                AssetCache.TryRemove(pair.Key, out _);
            }
        }
        int overflow = AssetCache.Count - AssetCacheLimit;
        if (overflow <= 0)
        {
            return;
        }
        foreach (string key in AssetCache.OrderBy(x => x.Value.At).Take(overflow).Select(x => x.Key))
        {
            AssetCache.TryRemove(key, out _);
        }
    }


    /// <summary>
    /// R105：列表请求的一次退避重试——只对 5xx 与网络异常重试（4xx 是配置问题，重试只会多打一次限流）。
    /// </summary>
    private static async Task<HttpResponseMessage> SendAssetRequestAsync(
        HttpClient http, Func<HttpRequestMessage> create, ILogger? logger, CancellationToken ct)
    {
        HttpResponseMessage response = await SendOnceAsync(http, create, logger, ct).ConfigureAwait(false);
        if ((int)response.StatusCode < 500)
        {
            return response;
        }
        response.Dispose();
        await Task.Delay(600, ct).ConfigureAwait(false);
        logger?.LogWarning("[components] 源返回 5xx，600 ms 后重试一次");
        return await SendOnceAsync(http, create, logger, ct).ConfigureAwait(false);
    }


    private static async Task<HttpResponseMessage> SendOnceAsync(
        HttpClient http, Func<HttpRequestMessage> create, ILogger? logger, CancellationToken ct)
    {
        try
        {
            using HttpRequestMessage request = create();
            HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false);
            // R105：把限流余量说清楚，别让用户对着「没有可用归档」猜。
            if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
                && remaining.FirstOrDefault() == "0")
            {
                string reset = response.Headers.TryGetValues("X-RateLimit-Reset", out var resetValues)
                    ? resetValues.FirstOrDefault() ?? ""
                    : "";
                logger?.LogWarning("[components] GitHub 限流已用完（reset={Reset}）", reset);
            }
            return response;
        }
        catch (HttpRequestException ex)
        {
            throw new ComponentSourceException($"连不上源：{ex.Message}");
        }
    }


    public static async Task<IReadOnlyList<ComponentReleaseAsset>> ListAssetsAsync(
        HttpClient http, ComponentSourceEntry source, int releases = 8, ILogger? logger = null,
        CancellationToken ct = default)
    {
        var result = new List<ComponentReleaseAsset>();
        if (string.IsNullOrWhiteSpace(source.Owner) || string.IsNullOrWhiteSpace(source.Repo))
        {
            return result;
        }
        string api = $"https://api.github.com/repos/{source.Owner}/{source.Repo}/releases?per_page={releases}";
        string cacheKey = $"{source.Owner}/{source.Repo}|{releases}|{source.AssetPattern}|{source.Kind}";
        string? cachedEtag = null;
        string? responseEtag = null;
        if (AssetCache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTimeOffset.UtcNow - cached.At < AssetCacheTtl)
            {
                return cached.Items;
            }
            cachedEtag = cached.ETag;
        }
        try
        {
            // 匿名请求每 IP 每小时 60 次，共享出口（代理/NAT）很容易被限流；
            // 有 token 就用上，没有就明说，别让用户对着「没有可用归档」猜。
            string? token = Environment.GetEnvironmentVariable("LUNARWARD_GITHUB_TOKEN")
                ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN")
                ?? Environment.GetEnvironmentVariable("GH_TOKEN");
            using HttpResponseMessage response = await SendAssetRequestAsync(http, () =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, api);
                if (!string.IsNullOrEmpty(cachedEtag))
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", cachedEtag);
                }
                request.Headers.UserAgent.ParseAdd("Lunarward/0.1 (+https://github.com/QiE2035/Lunarward)");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                if (!string.IsNullOrWhiteSpace(token))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                return request;
            }, logger, ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && AssetCache.TryGetValue(cacheKey, out var notModified))
            {
                AssetCache[cacheKey] = (DateTimeOffset.UtcNow, cachedEtag, notModified.Items);
                return notModified.Items;
            }
            // D112 的本意是「缓存 + ETag」：以前这里存的还是进函数时的 cachedEtag（首次必为 null），
            // 响应头里的 ETag 从来没被记下来，于是 If-None-Match 永远发不出去、304 分支是死代码。
            responseEtag = response.Headers.ETag?.ToString() ?? cachedEtag;
            if (!response.IsSuccessStatusCode)
            {
                int code = (int)response.StatusCode;
                string body = "";
                try
                {
                    body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger?.LogDebug(ex, "[components] 读取失败响应体失败");
                }
                logger?.LogWarning("[components] 列源 {Source} 失败：HTTP {Code} {Hint}", source.Id, code, body);
                throw new ComponentSourceException(DescribeFailure(code, body));
            }
            await using Stream stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: ct)
                .ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            var regex = string.IsNullOrWhiteSpace(source.AssetPattern)
                ? null
                : new Regex(source.AssetPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            foreach (JsonElement release in document.RootElement.EnumerateArray())
            {
                string tag = release.TryGetProperty("tag_name", out JsonElement tagNode) ? tagNode.GetString() ?? "" : "";
                if (!release.TryGetProperty("assets", out JsonElement assets) || assets.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }
                foreach (JsonElement asset in assets.EnumerateArray())
                {
                    string name = asset.TryGetProperty("name", out JsonElement nameNode) ? nameNode.GetString() ?? "" : "";
                    string url = asset.TryGetProperty("browser_download_url", out JsonElement urlNode)
                        ? urlNode.GetString() ?? ""
                        : "";
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(url))
                    {
                        continue;
                    }
                    if (regex is not null ? !regex.IsMatch(name) : !IsArchive(name))
                    {
                        continue;
                    }
                    long size = asset.TryGetProperty("size", out JsonElement sizeNode) && sizeNode.TryGetInt64(out long parsed)
                        ? parsed
                        : 0;
                    string? sha = asset.TryGetProperty("digest", out JsonElement digestNode)
                        ? digestNode.GetString()
                        : null;
                    // R61：只接受 sha256: 前缀；其它算法（sha512: 等）明确置空，
                    // 不能把原串当 sha256 去比，否则下载 100% 完成后必报「SHA-256 不一致」。
                    if (!string.IsNullOrEmpty(sha))
                    {
                        if (sha.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        {
                            sha = sha["sha256:".Length..];
                        }
                        else
                        {
                            logger?.LogWarning("[components] 源 {Source} 的摘要算法不受支持（{Digest}），按无源端摘要处理",
                                source.Id, sha);
                            sha = null;
                        }
                    }
                    result.Add(new ComponentReleaseAsset
                    {
                        SourceId = source.Id,
                        ReleaseTag = tag,
                        Name = name,
                        Url = url,
                        Size = size,
                        Sha256 = string.IsNullOrWhiteSpace(sha) ? null : sha,
                        Version = GuessVersion(name, tag),
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ComponentSourceException)
        {
            throw; // 已经是一句给用户看的话，别在外面再裹一层
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[components] 列源 {Source} 异常", source.Id);
            throw new ComponentSourceException($"列版本失败：{ex.Message}");
        }
        TrimAssetCache();
        AssetCache[cacheKey] = (DateTimeOffset.UtcNow, responseEtag ?? cachedEtag, result);
        return result;
    }


    /// <summary>
    /// 测速用的地址：配了镜像就打镜像根，否则打仓库页。
    /// </summary>
    public static string ProbeUrl(ComponentSourceEntry source)
    {
        string mirror = (source.MirrorPrefix ?? "").Trim();
        if (mirror.Length > 0)
        {
            return mirror.EndsWith('/') ? mirror : mirror + "/";
        }
        if (!string.IsNullOrWhiteSpace(source.Owner) && !string.IsNullOrWhiteSpace(source.Repo))
        {
            return $"https://github.com/{source.Owner}/{source.Repo}";
        }
        return "https://github.com";
    }


    /// <summary>
    /// 一个 HEAD 请求的首字节耗时（P3）：慢是慢、不通是不通，都如实写进结果，
    /// 由用户决定换哪个源——程序不替人猜「哪个更快」。
    /// </summary>
    public static async Task<ComponentSourceProbe> ProbeAsync(HttpClient http, ComponentSourceEntry source, CancellationToken ct = default)
    {
        string url = ProbeUrl(source);
        Stopwatch sw = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using HttpResponseMessage response = await http
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            sw.Stop();
            return new ComponentSourceProbe
            {
                SourceId = source.Id,
                DisplayName = source.DisplayName,
                Url = url,
                Milliseconds = (int)sw.ElapsedMilliseconds,
                Ok = response.IsSuccessStatusCode,
                Detail = response.IsSuccessStatusCode ? $"{(int)response.StatusCode}" : DescribeFailure((int)response.StatusCode, null),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ComponentSourceProbe
            {
                SourceId = source.Id,
                DisplayName = source.DisplayName,
                Url = url,
                Milliseconds = (int)sw.ElapsedMilliseconds,
                Ok = false,
                Detail = ex.GetBaseException().Message,
            };
        }
    }


    /// <summary>并发测所有源（默认源 + 用户覆盖）。单个源失败不影响其它。</summary>
    public static async Task<IReadOnlyList<ComponentSourceProbe>> ProbeAllAsync(HttpClient http, CancellationToken ct = default)
    {
        IReadOnlyList<ComponentSourceEntry> sources = Load();
        // R85：最多同时 4 个。以前是 Task.WhenAll 全量并发，源一多就瞬时打满出口，
        // 反而更容易撞上 GitHub 的 403 限流（共享出口实测）。
        ComponentSourceProbe[] results = new ComponentSourceProbe[sources.Count];
        using SemaphoreSlim gate = new(4);
        await Task.WhenAll(sources.Select(async (source, index) =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                results[index] = await ProbeAsync(http, source, ct).ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        })).ConfigureAwait(false);
        return results;
    }


    /// <summary>R104：支持哪些归档交给 <see cref="ArchiveExtractor"/> 一份口径（以前这里漏了 .zip）。</summary>
    public static bool IsArchive(string name) => ArchiveExtractor.IsSupported(name);


    /// <summary>R104：只有 `.tar.zst` 才需要 zstd 解压；`.tar.gz`/`.tgz` 走 tar 直接解开。</summary>
    public static bool PrefersZstd(string name) => name.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase);


    /// <summary>从文件名或 tag 里抠版本串；抠不到就退回 tag。</summary>
    public static string GuessVersion(string fileName, string tag)
    {
        Match match = Regex.Match(fileName, @"(\d+(?:\.\d+)+)", RegexOptions.CultureInvariant);
        if (match.Success)
        {
            return match.Groups[1].Value;
        }
        return string.IsNullOrWhiteSpace(tag) ? "" : tag.TrimStart('v');
    }
}


/// <summary>列版本 / 下载准备工作失败时抛这个，消息是给用户看的一句话。</summary>
/// <summary>单个源的连通性/延迟结果（P3「镜像自动测速」）。只报数字，不替用户排序。</summary>
public sealed class ComponentSourceProbe
{
    public string SourceId { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public string Url { get; init; } = "";

    public int Milliseconds { get; init; }

    public bool Ok { get; init; }

    public string Detail { get; init; } = "";

    public string Text => Ok ? $"{Milliseconds} ms" : $"失败 · {Detail}";
}


public sealed class ComponentSourceException(string message) : Exception(message);
