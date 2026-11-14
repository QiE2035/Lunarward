// Lunarward：受管组件的下载源（D73 §2.3）。
//
// 源是「可配置的清单」，不写死任何私有 API：默认三条走 GitHub Releases，
// 用户可以在 components-sources.json 里加自己的仓库或填镜像前缀。
// 这里只负责「列出版本 / 给出 asset URL / 给出期望 digest」，下载在 ComponentDownloader。
using Microsoft.Extensions.Logging;
using Starward.Helpers;
using System;
using System.Collections.Generic;
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
            else
            {
                list.Add(entry);
            }
        }
        return list.OrderBy(x => x.Rank).ToList();
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
    };


    /// <summary>
    /// 列出某个源的候选 asset（新的在前）。走 GitHub Releases API，不爬网页。
    /// 单个仓库失败只影响它自己，不抛给调用方。
    /// </summary>
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
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, api);
            request.Headers.UserAgent.ParseAdd("Lunarward/0.1 (+https://github.com/QiE2035/Lunarward)");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using HttpResponseMessage response = await http.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                logger?.LogWarning("[components] 列源 {Source} 失败：HTTP {Code}", source.Id, (int)response.StatusCode);
                return result;
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
                    if (!string.IsNullOrEmpty(sha) && sha.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    {
                        sha = sha["sha256:".Length..];
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
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[components] 列源 {Source} 异常（按无候选处理）", source.Id);
        }
        return result;
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
