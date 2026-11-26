using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Starward.Core.DeviceFp;
using Starward.Core.Gacha.Genshin;
using Starward.Core.Gacha.StarRail;
using Starward.Core.Gacha.ZZZ;
using Starward.Core.GameNotice;
using Starward.Core.GameRecord.BH3;
using Starward.Core.GameRecord.Genshin;
using Starward.Core.GameRecord.StarRail;
using Starward.Core.GameRecord.ZZZ;
using Starward.Core.HoYoPlay;
using Starward.Core.Passport;
using Starward.Core.SelfQuery;
using Starward.Features.Background;
using Starward.Features.Database;
using Starward.Features.Gacha;
using Starward.Features.Gacha.UIGF;
using Starward.Features.GameAccount;
using Starward.Features.GameInstall;
using Starward.Features.GameLauncher;
using Starward.Features.GameRecord;
using Starward.Features.GameRecord.BH3;
using Starward.Features.GameRecord.Genshin;
using Starward.Features.GameRecord.StarRail;
using Starward.Features.GameRecord.ZZZ;
using Starward.Features.Passport;
using Starward.Features.PlayTime;
using Starward.Features.Screenshot;
using Starward.Features.SelfQuery;
using Starward.Features.Update;
using Starward.GameInstall;
using Starward.GameInstall.HotUpdate;
using Starward.Setup.Core;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;
using Starward.Core.GameRecord;

namespace Starward;

public static partial class AppConfig
{

    private static IServiceProvider _serviceProvider;


    private static void BuildServiceProvider()
    {
        if (_serviceProvider == null)
        {
            var logFolder = Path.Combine(CacheFolder, "log");
            Directory.CreateDirectory(logFolder);
            LogFile = Path.Combine(logFolder, $"Starward_{DateTime.Now:yyMMdd}.log");
            Log.Logger = new LoggerConfiguration().WriteTo.File(path: LogFile, shared: true, outputTemplate: $$"""[{Timestamp:HH:mm:ss.fff}] [{Level:u4}] [{{Path.GetFileName(Environment.ProcessPath)}} ({{Environment.ProcessId}})] {SourceContext} {ExtraContext}{NewLine}{Message}{NewLine}{Exception}{NewLine}""")
                                                  .Enrich.FromLogContext()
                                                  .MinimumLevel.Override("ZiggyCreatures.Caching.Fusion", Serilog.Events.LogEventLevel.Error)
                                                  .CreateLogger();
            Log.Information($"Welcome to Lunarward v{AppVersion}\r\nSystem: {Environment.OSVersion}\r\nCommand Line: {Environment.CommandLine}");


            var sc = new ServiceCollection();

            ConfigServiceCollection(sc);

            _serviceProvider = sc.BuildServiceProvider();
        }
    }


    public static void ConfigServiceCollection(IServiceCollection sc)
    {
        sc.AddLogging(c => c.AddSerilog(Log.Logger));
        sc.AddHttpClient().ConfigureHttpClientDefaults(ConfigDefaultHttpClient);

        sc.AddMemoryCache();
        sc.AddFusionCache().WithDistributedCache(new SqliteCache(), new FusionCacheSystemTextJsonSerializer())
                           .WithDefaultEntryOptions(op =>
                           {
                               op.Duration = TimeSpan.FromMinutes(5);
                               op.EagerRefreshThreshold = 0.1f;
                               op.IsFailSafeEnabled = true;
                               op.FailSafeMaxDuration = TimeSpan.FromDays(90);
                               op.FailSafeThrottleDuration = TimeSpan.FromSeconds(10);
                               op.AllowStaleOnReadOnly = true;
                               op.FactorySoftTimeout = TimeSpan.FromSeconds(5);
                           });

        sc.AddSingleton<HoYoPlayClient>();
        sc.AddSingleton<HoYoPlayBetaClient>();
        sc.AddSingleton<GameNoticeClient>();
        sc.AddSingleton<BackgroundService>();
        sc.AddSingleton<GameLauncherService>();
        sc.AddSingleton<PlayTimeRecordService>();
        sc.AddSingleton<PlayTimeStatsService>();
        sc.AddSingleton<GameNoticeService>();
        sc.AddSingleton<SetupService>();

        sc.AddSingleton<GenshinGachaClient>();
        sc.AddSingleton<StarRailGachaClient>();
        sc.AddSingleton<ZZZGachaClient>();
        sc.AddSingleton<GenshinGachaService>();
        sc.AddSingleton<StarRailGachaService>();
        sc.AddSingleton<ZZZGachaService>();
        sc.AddSingleton<UIGFGachaService>();
        sc.AddSingleton<GenshinBeyondGachaClient>();
        sc.AddSingleton<GenshinBeyondGachaService>();

        sc.AddSingleton<DeviceFpClient>();
        sc.AddSingleton<HyperionBH3Client>();
        sc.AddSingleton<HoyolabBH3Client>();
        sc.AddSingleton<HyperionGenshinClient>();
        sc.AddSingleton<HoyolabGenshinClient>();
        sc.AddSingleton<HyperionStarRailClient>();
        sc.AddSingleton<HoyolabStarRailClient>();
        sc.AddSingleton<HyperionZZZClient>();
        sc.AddSingleton<HoyolabZZZClient>();
        sc.AddSingleton<GameRecordRoleService>();
        sc.AddSingleton<GameRecordBH3Service>();
        sc.AddSingleton<GameRecordGenshinService>();
        sc.AddSingleton<GameRecordStarRailService>();
        sc.AddSingleton<GameRecordZZZService>();
        sc.AddSingleton<WindowsDeviceFpService>();
        sc.AddSingleton<PassportClient>();
        sc.AddSingleton<PassportService>();

        sc.AddSingleton<SelfQueryClient>();
        sc.AddSingleton<SelfQueryService>();

        sc.AddHttpClient<ReleaseClient>().ConfigStarwardHttpClient();
        sc.AddTransient<UpdateService>();

        // Lunarward: 上游 0.19.0 把安装/热更新/服务抽进 Starward.GameInstall，移植侧同样引用该库（D128）。
        // 生命周期按上游 AppConfig.ServiceProvider.cs:118-142 注册 —— 那 5 个是 Singleton。
        // 库自带的 AddStarwardGameInstall() 是给上游 RPC 子进程（RpcRunner）用的，里面全是 Scoped；
        // 移植侧没有 RPC 子进程、全在根容器里解析，照抄会产生 12 处 captive dependency，
        // 且与 GameInstallRunner 自建 scope 解析出两个实例（D135）。
        // 库自带的注册里 HttpService/DownloadService 是 internal（只能由库自己注册），
        // 所以先调 AddStarwardGameInstall() 拿到那几个基础设施，再把 5 个业务服务
        // 按上游的生命周期覆写成 Singleton —— 后注册的赢，从根解析不再产生 captive dependency。
        sc.AddStarwardGameInstall();
        sc.AddSingleton<HoYoPlayService>();
        sc.AddSingleton<GameConfigService>();
        sc.AddSingleton<GameResourceService>();
        sc.AddSingleton<GenshinDispatchClient>();
        sc.AddSingleton<GenshinHotUpdateService>();
        sc.AddSingleton<GameInstallService>();
        // Lunarward: ScreenCaptureService 依赖 Windows.Graphics.Capture + Win2D，M2 重写后再注册
        // sc.AddSingleton<ScreenCaptureService>();

        sc.AddSingleton<GameAccountService>();
        // Lunarward：伴随启动的运行器（移植侧自建，上游没有），缺注册会让 App.OnLaunched 解析时抛异常。
        sc.AddSingleton<Starward.Features.Companion.CompanionRunner>();
        // Lunarward：BBS 登录页 / 战绩页要用的门面（0.19.0 合并时同样丢了注册）。
        sc.AddSingleton<Starward.Features.GameRecord.GameRecordService>();

        sc.AddHttpClient<LogUploadClient>().ConfigStarwardHttpClient();
    }





    public static T GetService<T>() where T : class
    {
        BuildServiceProvider();
        return _serviceProvider.GetRequiredService<T>();
    }

    public static ILogger<T> GetLogger<T>() where T : class
    {
        BuildServiceProvider();
        return _serviceProvider.GetRequiredService<ILogger<T>>();
    }

    public static SqliteConnection CreateDatabaseConnection()
    {
        return DatabaseService.CreateConnection();
    }


    private static void ConfigDefaultHttpClient(this IHttpClientBuilder builder)
    {
        builder.RemoveAllLoggers();
        builder.ConfigureHttpClient(client =>
        {
            client.DefaultRequestHeaders.Clear();
#if DEBUG
            client.DefaultRequestHeaders.Add("User-Agent", $"Starward.Debug/{AppVersion}");
#else
            client.DefaultRequestHeaders.Add("User-Agent", $"Starward/{AppVersion}");
#endif
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        });
        builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,
            EnableMultipleHttp3Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        });
    }


    private static void ConfigStarwardHttpClient(this IHttpClientBuilder builder)
    {
        builder.RemoveAllLoggers();
        builder.ConfigureHttpClient(client =>
        {
            client.DefaultRequestHeaders.Clear();
#if DEBUG
            client.DefaultRequestHeaders.Add("User-Agent", $"Starward.Debug/{AppVersion}");
#else
            client.DefaultRequestHeaders.Add("User-Agent", $"Starward/{AppVersion}");
#endif
            client.DefaultRequestHeaders.Add("X-Sw-Device-Id", DeviceId.ToString());
            client.DefaultRequestHeaders.Add("X-Sw-Session-Id", SessionId.ToString());
            client.DefaultRequestHeaders.Add("X-Sw-App-Version", AppVersion);
            client.DefaultRequestHeaders.Add("X-Sw-App-Type", "Desktop");
            client.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher;
        });
        builder.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,
            EnableMultipleHttp3Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
        });
    }


}