using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Starward.Core.DeviceFp;
using Starward.Core.Gacha.Genshin;
using Starward.Core.Gacha.StarRail;
using Starward.Core.Gacha.ZZZ;
using Starward.Core.GameNotice;
<<<<<<< /tmp/_ours
using Starward.Core.DeviceFp;
using Starward.Core.GameRecord;
using Starward.Core.GameRecord.BH3;
using Starward.Core.GameRecord.Genshin;
using Starward.Core.GameRecord.StarRail;
using Starward.Core.GameRecord.ZZZ;
||||||| /tmp/_base
using Starward.Core.GameRecord;
=======
using Starward.Core.GameRecord.BH3;
using Starward.Core.GameRecord.Genshin;
using Starward.Core.GameRecord.StarRail;
using Starward.Core.GameRecord.ZZZ;
>>>>>>> /tmp/_theirs
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
using ZiggyCreatures.Caching.Fusion;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.Serialization.SystemTextJson;

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

<<<<<<< /tmp/_ours
            var sc = new ServiceCollection();
            sc.AddMemoryCache();
            sc.AddLogging(c => c.AddSerilog(Log.Logger));
            sc.AddHttpClient().ConfigureHttpClientDefaults(ConfigDefaultHttpClient);

            // 上游 0.19.0 把 HoYoPlayService 搬进了 Starward.GameInstall（net11.0-windows + FusionCache），
            // 移植侧不能引用，所以这里保留 src/Lunarward/Features/HoYoPlay 里的 Linux 版实现。
            sc.AddSingleton<HoYoPlayService>();
            sc.AddSingleton<HoYoPlayClient>();
            // LUNARWARD-DEVIATION: 上游 0.19.0 的 HoYoPlayClient 改为「一个启动器配置一个实例」，
            // 移植侧先用默认配置单例，多启动器（B服/第三方）随 D115 之后单独对齐。
            sc.AddSingleton<HoYoPlayBetaClient>();
            sc.AddSingleton<GameNoticeClient>();
            sc.AddSingleton<BackgroundService>();
            sc.AddSingleton<GameLauncherService>();
            sc.AddSingleton<GamePackageService>();
            sc.AddSingleton<Starward.Features.Companion.CompanionRunner>();
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
||||||| /tmp/_base
            var sc = new ServiceCollection();
            sc.AddMemoryCache();
            sc.AddLogging(c => c.AddSerilog(Log.Logger));
            sc.AddHttpClient().ConfigureHttpClientDefaults(ConfigDefaultHttpClient);

            sc.AddSingleton<HoYoPlayClient>();
            sc.AddSingleton<GameNoticeClient>();
            sc.AddSingleton<HoYoPlayService>();
            sc.AddSingleton<BackgroundService>();
            sc.AddSingleton<GameLauncherService>();
            sc.AddSingleton<GamePackageService>();
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
=======
>>>>>>> /tmp/_theirs

<<<<<<< /tmp/_ours
            sc.AddSingleton<DeviceFpClient>();
            sc.AddSingleton<HyperionGenshinClient>();
            sc.AddSingleton<HoyolabGenshinClient>();
            sc.AddSingleton<HyperionStarRailClient>();
            sc.AddSingleton<HoyolabStarRailClient>();
            sc.AddSingleton<HyperionZZZClient>();
            sc.AddSingleton<HoyolabZZZClient>();
            sc.AddSingleton<HyperionBH3Client>();
            sc.AddSingleton<HoyolabBH3Client>();
            sc.AddSingleton<PassportClient>();
            sc.AddSingleton<GameRecordService>();
||||||| /tmp/_base
            sc.AddSingleton<HoyolabClient>();
            sc.AddSingleton<HyperionClient>();
            sc.AddSingleton<GameRecordService>();
=======
            var sc = new ServiceCollection();
>>>>>>> /tmp/_theirs

            ConfigServiceCollection(sc);

            _serviceProvider = sc.BuildServiceProvider();
        }
    }

<<<<<<< /tmp/_ours
            sc.AddSingleton<RpcService>();
            // Lunarward: 真实安装流程依赖 Starward.RPC（gRPC + 命名管道，D6 待换 Unix socket）；
            // 这里注册的是 Features/GameInstall/GameInstallShims.cs 里的最小实现。
            sc.AddSingleton<Starward.RPC.GameInstall.GameInstallService>();
||||||| /tmp/_base
            sc.AddSingleton<RpcService>();
            sc.AddSingleton<GameInstallService>();
=======
>>>>>>> /tmp/_theirs

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

        sc.AddSingleton<HoYoPlayService>();
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

        sc.AddSingleton<GameInstallService>();
        sc.AddSingleton<GameResourceService>();
        sc.AddSingleton<GameConfigService>();
        sc.AddSingleton<GenshinDispatchClient>();
        sc.AddSingleton<GenshinHotUpdateService>();

        sc.AddSingleton<GameAccountService>();

        sc.AddSingleton<ScreenCaptureService>();

        sc.AddHttpClient<LogUploadClient>().ConfigStarwardHttpClient();
    }

<<<<<<< /tmp/_ours
            // Lunarward: ScreenCaptureService 依赖 Windows.Graphics.Capture + Win2D，M2 重写后再注册
||||||| /tmp/_base
            sc.AddSingleton<ScreenCaptureService>();
=======
>>>>>>> /tmp/_theirs




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
            client.DefaultRequestHeaders.Add("User-Agent", $"Lunarward/{AppVersion}");
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
            client.DefaultRequestHeaders.Add("User-Agent", $"Lunarward/{AppVersion}");
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