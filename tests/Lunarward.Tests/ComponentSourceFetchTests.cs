// Lunarward：源列表拉取（D73 §2.3 / R105）。
//
// 以前这一层只钉纯函数（DescribeFailure / IsArchive / GuessVersion），「真去要一次列表」的
// 路径——JSON 解析、10 分钟缓存、5xx 退避重试、限流头、单源探测——全没有用例，CI 的
// L1 行覆盖率门限（85%）就是这么被一批批新代码压穿的。
//
// 这里用假的 HttpMessageHandler 把这些路径钉住：不联网、不走真实 GitHub，跑得也快。
using Starward.Features.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Lunarward.Tests;


[Collection("env-serial")]
public class ComponentSourceFetchTests : IDisposable
{
    private readonly string _configRoot;

    private readonly string? _previousConfigRoot;

    private readonly StubHandler _handler = new();

    private readonly HttpClient _http;

    /// <summary>每个用例用不同 owner，避免静态缓存（同一个进程内）跨用例命中。</summary>
    private readonly string _owner = "owner" + Guid.NewGuid().ToString("N")[..10];


    public ComponentSourceFetchTests()
    {
        _configRoot = Path.Combine(Path.GetTempPath(), "lw-fetch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_configRoot);
        _previousConfigRoot = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _configRoot);
        _http = new HttpClient(_handler);
    }


    public void Dispose()
    {
        _http.Dispose();
        Environment.SetEnvironmentVariable("XDG_CONFIG_HOME", _previousConfigRoot);
        try
        {
            Directory.Delete(_configRoot, recursive: true);
        }
        catch
        {
            // 清理失败不影响结论
        }
    }


    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = new();

        public Func<int, HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = (_, _) => Json("[]");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Respond(Requests.Count, request));
        }
    }


    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };


    private ComponentSourceEntry Source(string repo = "wine-builds", string pattern = "", string kind = "wine") => new()
    {
        Id = "src-" + repo,
        DisplayName = repo,
        Kind = kind,
        Owner = _owner,
        Repo = repo,
        AssetPattern = pattern,
    };


    private const string ReleasesJson = """
        [
          {
            "tag_name": "v10.0",
            "assets": [
              {
                "name": "wine-10.0.tar.xz",
                "browser_download_url": "https://example.invalid/wine-10.0.tar.xz",
                "size": 104857600,
                "digest": "sha256:abc123"
              },
              {
                "name": "notes.txt",
                "browser_download_url": "https://example.invalid/notes.txt",
                "size": 12
              }
            ]
          },
          { "tag_name": "v9.0" }
        ]
        """;


    [Fact]
    public async Task ListAssets_KeepsArchivesAndDropsEverythingElse()
    {
        _handler.Respond = (_, _) => Json(ReleasesJson);

        IReadOnlyList<ComponentReleaseAsset> assets = await ComponentSources.ListAssetsAsync(_http, Source());

        ComponentReleaseAsset asset = Assert.Single(assets);
        Assert.Equal("src-wine-builds", asset.SourceId);
        Assert.Equal("v10.0", asset.ReleaseTag);
        Assert.Equal("wine-10.0.tar.xz", asset.Name);
        Assert.Equal("https://example.invalid/wine-10.0.tar.xz", asset.Url);
        Assert.Equal(104857600, asset.Size);
        Assert.Equal("abc123", asset.Sha256);
        Assert.Equal("10.0", asset.Version);
    }


    [Fact]
    public async Task ListAssets_AssetPatternOverridesExtensionFilter()
    {
        _handler.Respond = (_, _) => Json(ReleasesJson);

        IReadOnlyList<ComponentReleaseAsset> assets = await ComponentSources.ListAssetsAsync(
            _http, Source(pattern: @"^notes\.txt$"));

        Assert.Equal("notes.txt", Assert.Single(assets).Name);
    }


    [Fact]
    public async Task ListAssets_UnsupportedDigest_IsTreatedAsMissing()
    {
        const string json = """
            [ { "tag_name": "v1", "assets": [
                { "name": "dxvk-1.0.tar.gz", "browser_download_url": "https://example.invalid/d.tar.gz",
                  "size": 1, "digest": "blake3:deadbeef" } ] } ]
            """;
        _handler.Respond = (_, _) => Json(json);

        ComponentReleaseAsset asset = Assert.Single(await ComponentSources.ListAssetsAsync(_http, Source(kind: "graphics")));

        Assert.Null(asset.Sha256);
    }


    [Fact]
    public async Task ListAssets_NonArrayRoot_ReturnsEmpty()
    {
        _handler.Respond = (_, _) => Json("""{"message":"Not Found"}""");

        Assert.Empty(await ComponentSources.ListAssetsAsync(_http, Source()));
    }


    [Fact]
    public async Task ListAssets_MissingOwner_DoesNotTouchNetwork()
    {
        ComponentSourceEntry source = Source();
        source.Owner = "";

        Assert.Empty(await ComponentSources.ListAssetsAsync(_http, source));
        Assert.Empty(_handler.Requests);
    }


    [Fact]
    public async Task ListAssets_SecondCallIsServedFromCache()
    {
        _handler.Respond = (_, _) => Json(ReleasesJson);

        await ComponentSources.ListAssetsAsync(_http, Source());
        await ComponentSources.ListAssetsAsync(_http, Source());

        Assert.Single(_handler.Requests);
    }


    [Fact]
    public async Task ListAssets_SendsUserAgentAndTokenHeader()
    {
        _handler.Respond = (_, _) => Json("[]");
        string? previous = Environment.GetEnvironmentVariable("LUNARWARD_GITHUB_TOKEN");
        Environment.SetEnvironmentVariable("LUNARWARD_GITHUB_TOKEN", "ghp_test");
        try
        {
            await ComponentSources.ListAssetsAsync(_http, Source());
        }
        finally
        {
            Environment.SetEnvironmentVariable("LUNARWARD_GITHUB_TOKEN", previous);
        }

        HttpRequestMessage request = Assert.Single(_handler.Requests);
        Assert.Contains("Lunarward", request.Headers.UserAgent.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("ghp_test", request.Headers.Authorization.Parameter);
    }


    [Fact]
    public async Task ListAssets_RetriesOnceAfterServerError()
    {
        _handler.Respond = (attempt, _) => attempt == 1
            ? Json("""{"message":"boom"}""", HttpStatusCode.ServiceUnavailable)
            : Json(ReleasesJson);

        IReadOnlyList<ComponentReleaseAsset> assets = await ComponentSources.ListAssetsAsync(_http, Source());

        Assert.Equal(2, _handler.Requests.Count);
        Assert.Equal("wine-10.0.tar.xz", Assert.Single(assets).Name);
    }


    [Fact]
    public async Task ListAssets_TwoServerErrors_SurfaceTheStatus()
    {
        _handler.Respond = (_, _) => Json("""{"message":"boom"}""", HttpStatusCode.ServiceUnavailable);

        ComponentSourceException ex = await Assert.ThrowsAsync<ComponentSourceException>(
            () => ComponentSources.ListAssetsAsync(_http, Source()));

        Assert.Contains("503", ex.Message);
        Assert.Equal(2, _handler.Requests.Count);
    }


    [Fact]
    public async Task ListAssets_NotFound_PointsAtRepoConfig()
    {
        _handler.Respond = (_, _) => Json("Not Found", HttpStatusCode.NotFound);

        ComponentSourceException ex = await Assert.ThrowsAsync<ComponentSourceException>(
            () => ComponentSources.ListAssetsAsync(_http, Source()));

        Assert.Contains("404", ex.Message);
        Assert.Contains("Owner", ex.Message);
        Assert.Single(_handler.Requests);
    }


    [Fact]
    public async Task ListAssets_RateLimitHint_DoesNotFailTheCall()
    {
        _handler.Respond = (_, _) =>
        {
            HttpResponseMessage response = Json(ReleasesJson);
            response.Headers.TryAddWithoutValidation("X-RateLimit-Remaining", "0");
            response.Headers.TryAddWithoutValidation("X-RateLimit-Reset", "1795497894");
            return response;
        };

        Assert.Single(await ComponentSources.ListAssetsAsync(_http, Source()));
    }


    [Fact]
    public async Task ListAssets_NetworkFailure_IsTranslated()
    {
        _handler.Respond = (_, _) => throw new HttpRequestException("connection refused");

        ComponentSourceException ex = await Assert.ThrowsAsync<ComponentSourceException>(
            () => ComponentSources.ListAssetsAsync(_http, Source()));

        Assert.Contains("连不上源", ex.Message);
    }


    [Fact]
    public async Task ListAssets_Cancellation_IsNotWrapped()
    {
        _handler.Respond = (_, _) => throw new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => ComponentSources.ListAssetsAsync(_http, Source()));
    }


    [Fact]
    public async Task ListAssets_CacheStopsGrowing_AndDropsTheOldest()
    {
        _handler.Respond = (_, _) => Json("[]");

        // 上限 64：塞 70 个不同的仓库，最早那 6 个会被按时间淘汰。
        for (int i = 0; i < 70; i++)
        {
            await ComponentSources.ListAssetsAsync(_http, Source(repo: "repo" + i));
        }

        int before = _handler.Requests.Count;
        await ComponentSources.ListAssetsAsync(_http, Source(repo: "repo0"));
        Assert.Equal(before + 1, _handler.Requests.Count);

        await ComponentSources.ListAssetsAsync(_http, Source(repo: "repo69"));
        Assert.Equal(before + 1, _handler.Requests.Count);
    }


    [Fact]
    public async Task Probe_ReportsStatusAndLatency()
    {
        _handler.Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);

        ComponentSourceProbe probe = await ComponentSources.ProbeAsync(_http, Source());

        Assert.True(probe.Ok);
        Assert.Equal("200", probe.Detail);
        Assert.Equal("src-wine-builds", probe.SourceId);
        Assert.Contains(" ms", probe.Text);
        Assert.Equal(HttpMethod.Head, Assert.Single(_handler.Requests).Method);
    }


    [Fact]
    public async Task Probe_NonSuccess_ExplainsFailure()
    {
        _handler.Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.Forbidden);

        ComponentSourceProbe probe = await ComponentSources.ProbeAsync(_http, Source());

        Assert.False(probe.Ok);
        Assert.Contains("403", probe.Detail);
        Assert.Contains("失败", probe.Text);
    }


    [Fact]
    public async Task Probe_NetworkError_StaysFailNotThrow()
    {
        _handler.Respond = (_, _) => throw new HttpRequestException("no route");

        ComponentSourceProbe probe = await ComponentSources.ProbeAsync(_http, Source());

        Assert.False(probe.Ok);
        Assert.Contains("no route", probe.Detail);
    }


    [Fact]
    public async Task ProbeAll_KeepsSourceOrder_AndIncludesDisabledSources()
    {
        _handler.Respond = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);
        ComponentSources.SaveUserEntries(new[]
        {
            new ComponentSourceEntry { Id = "cut-off", DisplayName = "被禁用的源", Kind = "wine", Owner = _owner, Repo = "hidden", Enabled = false, Rank = 90 },
        });

        IReadOnlyList<ComponentSourceProbe> probes = await ComponentSources.ProbeAllAsync(_http);

        // 源管理页要能看见被禁用的源（方便重新勾上），所以这里按 Load() 的顺序原样探测。
        Assert.Equal(ComponentSources.Load().Select(x => x.Id), probes.Select(x => x.SourceId));
        Assert.Contains(probes, x => x.SourceId == "cut-off");
        Assert.All(probes, x => Assert.True(x.Ok));
    }


    [Fact]
    public void SameAsDefault_ComparesEveryPersistedField()
    {
        ComponentSourceEntry def = ComponentSources.Defaults[0];
        Assert.True(ComponentSources.SameAsDefault(new ComponentSourceEntry
        {
            Id = def.Id,
            DisplayName = def.DisplayName,
            Kind = def.Kind,
            Owner = def.Owner,
            Repo = def.Repo,
            AssetPattern = def.AssetPattern,
            MirrorPrefix = def.MirrorPrefix,
            Rank = def.Rank,
            Enabled = def.Enabled,
            Removed = def.Removed,
        }));

        ComponentSourceEntry changed = new()
        {
            Id = def.Id,
            DisplayName = def.DisplayName,
            Kind = def.Kind,
            Owner = def.Owner,
            Repo = def.Repo,
            AssetPattern = def.AssetPattern,
            MirrorPrefix = "https://mirror.example/",
            Rank = def.Rank,
            Enabled = def.Enabled,
            Removed = def.Removed,
        };
        Assert.False(ComponentSources.SameAsDefault(changed));
        Assert.False(ComponentSources.SameAsDefault(new ComponentSourceEntry { Id = "not-a-default" }));
    }


    [Fact]
    public void SaveUserEntries_PersistsOnlyDifferencesFromDefaults()
    {
        ComponentSourceEntry def = ComponentSources.Defaults[0];
        ComponentSourceEntry untouched = new()
        {
            Id = def.Id,
            DisplayName = def.DisplayName,
            Kind = def.Kind,
            Owner = def.Owner,
            Repo = def.Repo,
            AssetPattern = def.AssetPattern,
            MirrorPrefix = def.MirrorPrefix,
            Rank = def.Rank,
            Enabled = def.Enabled,
            Removed = def.Removed,
        };
        ComponentSourceEntry custom = new() { Id = "mine", DisplayName = "自建", Kind = "wine", Owner = "me", Repo = "wine", Rank = 99 };

        ComponentSources.SaveUserEntries(new[] { untouched, custom });

        string saved = File.ReadAllText(ComponentSources.FilePath);
        Assert.Contains("mine", saved);
        Assert.DoesNotContain(def.Id + "\"", saved);
    }


    [Fact]
    public void ResetUserEntries_RemovesTheOverrideFile()
    {
        ComponentSources.SaveUserEntries(new[]
        {
            new ComponentSourceEntry { Id = "mine", DisplayName = "自建", Kind = "wine", Owner = "me", Repo = "wine", Rank = 99 },
        });
        Assert.True(File.Exists(ComponentSources.FilePath));

        ComponentSources.ResetUserEntries();

        Assert.False(File.Exists(ComponentSources.FilePath));
        Assert.Equal(ComponentSources.Defaults.Count, ComponentSources.Load().Count);
    }


    [Fact]
    public void RemovedDefault_StaysDeleted()
    {
        ComponentSourceEntry def = ComponentSources.Defaults[0];
        ComponentSources.SaveUserEntries(new[] { new ComponentSourceEntry { Id = def.Id, Removed = true } });

        Assert.DoesNotContain(ComponentSources.Load(), x => x.Id == def.Id);
    }


    [Fact]
    public void LoadEnabled_DropsDisabledEntries()
    {
        Assert.All(ComponentSources.LoadEnabled(), x => Assert.True(x.Enabled));
    }


    [Fact]
    public void Validators_CatchConfigTypos()
    {
        Assert.False(ComponentSources.IsValidRepoPart(""));
        Assert.False(ComponentSources.IsValidRepoPart("a b"));
        Assert.False(ComponentSources.IsValidRepoPart("a/b"));
        Assert.True(ComponentSources.IsValidRepoPart("wine-builds"));

        Assert.True(ComponentSources.IsValidMirrorPrefix(""));
        Assert.True(ComponentSources.IsValidMirrorPrefix("https://mirror.example/gh/"));
        Assert.False(ComponentSources.IsValidMirrorPrefix("https://mirror.example/gh"));
        Assert.False(ComponentSources.IsValidMirrorPrefix("ftp://mirror.example/"));

        Assert.StartsWith("user-", ComponentSources.NewCustomId());
        Assert.True(ComponentSources.IsDefault(ComponentSources.Defaults[0].Id));
        Assert.False(ComponentSources.IsDefault("user-1234"));
    }
}
