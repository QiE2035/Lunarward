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
        JsonFile.WriteAtomic(FilePath, entries.ToList(), JsonOptions);
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
            using var request = new HttpRequestMessage(HttpMethod.Get, api);
            if (!string.IsNullOrEmpty(cachedEtag))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", cachedEtag);
            }
            request.Headers.UserAgent.ParseAdd("Lunarward/0.1 (+https://github.com/QiE2035/Lunarward)");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            // 匿名请求每 IP 每小时 60 次，共享出口（代理/NAT）很容易被限流；
            // 有 token 就用上，没有就明说，别让用户对着「没有可用归档」猜。
            string? token = Environment.GetEnvironmentVariable("LUNARWARD_GITHUB_TOKEN")
                ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN")
                ?? Environment.GetEnvironmentVariable("GH_TOKEN");
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
            using HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.NotModified && AssetCache.TryGetValue(cacheKey, out var notModified))
            {
                AssetCache[cacheKey] = (DateTimeOffset.UtcNow, cachedEtag, notModified.Items);
                return notModified.Items;
            }
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
        AssetCache[cacheKey] = (DateTimeOffset.UtcNow, cachedEtag, result);
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
        Task<ComponentSourceProbe>[] tasks = sources.Select(x => ProbeAsync(http, x, ct)).ToArray();
        ComponentSourceProbe[] results = await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }


    public static bool IsArchive(string name)
        => name.EndsWith(".tar.xz", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);


    public static bool PrefersZstd(string name)
        => name.EndsWith(".tar.zst", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)
           || name.EndsWith(".tgz", StringComparison.OrdinalIgnoreCase);


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
