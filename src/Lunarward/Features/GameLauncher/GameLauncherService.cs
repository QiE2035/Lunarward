using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.HoYoPlay;
using Starward.Features.GameSelector;
using Starward.Features.GameSetting;
using Starward.Features.Passport;
using Starward.Features.PlayTime;
using Starward.GameInstall;
using Starward.GameInstall.HotUpdate;
using Starward.GameInstall.Model;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Starward.Features.HoYoPlay;
using System.Text.RegularExpressions;

namespace Starward.Features.GameLauncher;

internal partial class GameLauncherService
{
    private readonly ILogger<GameLauncherService> _logger;

    private readonly HoYoPlayService _hoYoPlayService;

    private readonly GameConfigService _gameConfigService;

    private readonly PassportService _passportService;

    private readonly PlayTimeRecordService _playTimeRecorderService;

    private readonly GenshinHotUpdateService _genshinHotUpdateService;


    public GameLauncherService(ILogger<GameLauncherService> logger,
                               HoYoPlayService hoYoPlayService,
                               PassportService passportService,
                               GameConfigService gameConfigService,
                               PlayTimeRecordService playTimeRecorderService,
                               GenshinHotUpdateService genshinHotUpdateService)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
        _gameConfigService = gameConfigService;
        _passportService = passportService;
        _playTimeRecorderService = playTimeRecorderService;
        _genshinHotUpdateService = genshinHotUpdateService;
    }


    #region Install Path


    /// <summary>
    /// 游戏安装目录，为空时未找到
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetGameInstallPath(GameId gameId)
    {
        GameEntry? entry = GameEntryService.GetFromId(gameId);
        if (string.IsNullOrWhiteSpace(entry?.InstallPath))
        {
            return null;
        }
        string path = GetFullPathIfRelativePath(entry.InstallPath);
        if (Directory.Exists(path))
        {
            return Path.GetFullPath(path);
        }
        else if (entry.Removable)
        {
            return path;
        }
        else
        {
            GameEntryService.SetInstallPathNull(gameId);
            return null;
        }
    }


    /// <summary>
    /// 游戏安装目录，为空时未找到
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="storageRemoved">可移动存储设备已移除</param>
    /// <returns></returns>
    public static string? GetGameInstallPath(GameId gameId, out bool storageRemoved)
    {
        storageRemoved = false;
        GameEntry? entry = GameEntryService.GetFromId(gameId);
        if (string.IsNullOrWhiteSpace(entry?.InstallPath))
        {
            return null;
        }
        string path = GetFullPathIfRelativePath(entry.InstallPath);
        if (Directory.Exists(path))
        {
            return path;
        }
        else if (entry.Removable)
        {
            storageRemoved = true;
            return path;
        }
        else
        {
            GameEntryService.SetInstallPathNull(gameId);
            return null;
        }
    }

    /// <summary>
    /// 修改游戏安装目录
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public string? ChangeGameInstallPath(GameId gameId, string? path)
    {
        LauncherConfig? model = _hoYoPlayService.GetLauncherConfig(gameId);
        if (model is null)
        {
            throw new ArgumentNullException($"Cannot get LauncherConfig from GameId ({gameId}).");
        }
        if (Directory.Exists(path))
        {
            path = Path.GetFullPath(path);
            string relativePath = GetRelativePathIfInRemovableStorage(path, out bool removable);
            GameEntryService.SetInstallPath(gameId, model.Id, relativePath, removable);
            WeakReferenceMessenger.Default.Send(new PinGameIconMessage(gameId, model, true));
        }
        else
        {
            path = null;
            GameEntryService.SetInstallPath(gameId, model.Id, null, false);
        }
        return path;
    }


    /// <summary>
    /// 如果安装在可移动存储设备中，获取相对路径
    /// </summary>
    /// <param name="path"></param>
    /// <param name="removableStorage"></param>
    /// <returns></returns>
    public static string GetRelativePathIfInRemovableStorage(string path, out bool removableStorage)
    {
        removableStorage = DriveHelper.IsDeviceRemovableOrOnUSB(path);
        if (removableStorage && Path.GetPathRoot(AppConfig.StarwardExecutePath) == Path.GetPathRoot(path))
        {
            path = Path.GetRelativePath(Path.GetDirectoryName(AppConfig.ConfigPath)!, path);
        }
        return path;
    }


    /// <summary>
    /// 如果安装在可移动存储设备中，获取完整路径
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string GetFullPathIfRelativePath(string path)
    {
        if (Path.IsPathFullyQualified(path))
        {
            return Path.GetFullPath(path);
        }
        else
        {
            return Path.GetFullPath(path, Path.GetDirectoryName(AppConfig.ConfigPath)!);
        }
    }


    #endregion



    #region Game Version


    /// <summary>
    /// 本地游戏版本
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="installPath"></param>
    /// <returns></returns>
    public async Task<Version?> GetLocalGameVersionAsync(GameId gameId, string? installPath = null, CancellationToken cancellation = default)
    {
        installPath ??= GetGameInstallPath(gameId);
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }
        return (await GameConfigService.GetGameConfigIniAsync(installPath, cancellation))?.GameVersion;
    }


    /// <summary>
    /// 最新游戏版本
    /// </summary>
    /// <param name="gameBiz"></param>
    /// <returns></returns>
    public async Task<(Version? Latest, Version? Predownload)> GetLatestGameVersionAsync(GameId gameId, CancellationToken cancellation)
    {
        LauncherConfig? launcher = _hoYoPlayService.GetLauncherConfig(gameId);
        GameConfig? config = await _hoYoPlayService.GetGameConfigAsync(gameId, cancellation);
        if (config is null)
        {
            throw new ArgumentOutOfRangeException($"Game config is null ({gameId.Id}, {gameId.GameBiz}).");
        }
        if (config.DefaultDownloadMode is DownloadMode.DOWNLOAD_MODE_FILE)
        {
            GamePackage? package = await _hoYoPlayService.GetGamePackageAsync(gameId, cancellation);
            _ = Version.TryParse(package?.Main.Major?.Version, out Version? latestVersion);
            _ = Version.TryParse(package?.PreDownload.Major?.Version, out Version? predownloadVersion);
            return (latestVersion, predownloadVersion);
        }
        else
        {
            GameBranch? gameBranch = await _hoYoPlayService.GetGameBranchAsync(gameId, cancellation);
            if (gameBranch is null)
            {
                throw new ArgumentOutOfRangeException($"Game branch is null ({gameId.Id}, {gameId.GameBiz}).");
            }
            if (launcher?.IsBeta is true)
            {
                await _hoYoPlayService.GetGameSophonChunkBuildAsync(gameId, gameBranch.Main, null, cancellation);
            }
            _ = Version.TryParse(gameBranch.Main.Tag, out Version? latestVersion);
            _ = Version.TryParse(gameBranch.PreDownload?.Tag, out Version? predownloadVersion);
            return (latestVersion, predownloadVersion);
        }
    }


    /// <summary>
    /// 检查预下载是否完成
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="installPath"></param>
    /// <returns></returns>
    public async Task<bool> CheckPreDownloadFinishedAsync(GameId gameId, string? installPath = null, CancellationToken cancellation = default)
    {
        installPath ??= GetGameInstallPath(gameId);
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return false;
        }
        var config = await GameConfigService.GetGameConfigIniAsync(installPath, cancellation);
        if (config is null)
        {
            return false;
        }
        (_, Version? pre) = await GetLatestGameVersionAsync(gameId, cancellation);
        AudioLanguage language = await _gameConfigService.GetAudioLanguageAsync(gameId, installPath, cancellation);
        return config.Settings.GetValueOrDefault("predownload") == $"{config.GameVersion},{pre},{language}";
    }


    /// <summary>
    /// 检查插件更新
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="installPath"></param>
    /// <param name="cancellation"></param>
    /// <returns></returns>
    public async Task<bool> CheckPluginUpdateAsync(GameId gameId, string? installPath = null, CancellationToken cancellation = default)
    {
        installPath ??= GetGameInstallPath(gameId);
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return false;
        }
        GamePluginRelease? pluginRelease = await _hoYoPlayService.GetGamePluginAsync(gameId, cancellation);
        if (pluginRelease is null)
        {
            return false;
        }
        GameConfigIni? configIni = await GameConfigService.GetGameConfigIniAsync(installPath, cancellation);
        foreach (var item in pluginRelease.Plugins)
        {
            if (item.PluginPackage.Url.Contains("DXSETUP", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            string key = $"plugin_{item.PluginId}_version";
            if (configIni?.Settings.GetValueOrDefault(key) == item.Version)
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(item.PluginPackage.Validation))
            {
                return true;
            }
            else if (!await GameResourceService.VerifyPluginAsync(installPath, item.PluginPackage, cancellation))
            {
                return true;
            }
        }
        return false;
    }



    public async Task<bool> CheckGenshinHotUpdateAsync(GameId gameId, string? installPath = null, CancellationToken cancellation = default)
    {
        if (gameId.GameBiz.Game is not GameBiz.hk4e || !AppConfig.GetEnableCutsceneManage(gameId))
        {
            return false;
        }
        var launcher = _hoYoPlayService.GetLauncherConfig(gameId);
        if (launcher is null || launcher.IsBeta)
        {
            return false;
        }
        installPath ??= GetGameInstallPath(gameId);
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return false;
        }
        return await _genshinHotUpdateService.CheckUpdateAsync(gameId, installPath, cancellation);
    }



    #endregion



    #region Start Game


    /// <summary>
    /// 游戏进程名，带 .exe 扩展名
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public async Task<string> GetGameExeNameAsync(GameId gameId, CancellationToken cancellation)
    {
        string? name = GetGameExeName(gameId.GameBiz);
        if (string.IsNullOrWhiteSpace(name))
        {
            var config = await _hoYoPlayService.GetGameConfigAsync(gameId, cancellation);
            name = config?.ExeFileName;
        }
        return name ?? throw new ArgumentOutOfRangeException($"Unknown game ({gameId.Id}, {gameId.GameBiz}).");
    }


    /// <summary>
    /// 游戏进程名，带 .exe 扩展名
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetGameExeName(GameBiz gameBiz)
    {
        string? name = gameBiz.Value switch
        {
            GameBiz.hk4e_cn or GameBiz.hk4e_bilibili => "YuanShen.exe",
            GameBiz.hk4e_global => "GenshinImpact.exe",
            _ => gameBiz.Game switch
            {
                GameBiz.hkrpg => "StarRail.exe",
                GameBiz.bh3 => "BH3.exe",
                GameBiz.nap => "ZenlessZoneZero.exe",
                _ => null,
            },
        };
        return name;
    }


    /// <summary>
    /// 游戏进程文件是否存在
    /// </summary>
    /// <param name="biz"></param>
    /// <param name="installPath"></param>
    /// <returns></returns>
    public async Task<bool> IsGameExeExistsAsync(GameId gameId, string? installPath = null, CancellationToken cancellation = default)
    {
        installPath ??= GetGameInstallPath(gameId);
        if (!string.IsNullOrWhiteSpace(installPath))
        {
            var exe = Path.Join(installPath, await GetGameExeNameAsync(gameId, cancellation));
            return File.Exists(exe);
        }
        return false;
    }


    /// <summary>
    /// 获取游戏进程
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public async Task<Process?> GetGameProcessAsync(GameId gameId, CancellationToken cancellation)
    {
        if (!OperatingSystem.IsWindows())
        {
            // B34：wine 的进程名是 wine/wine64，按 exe 名永远找不到；改为扫 /proc 的 cmdline。
            string exeName = await GetGameExeNameAsync(gameId);
            int? pid = Features.PlayTime.PlayTimeSession.FindProcessByCommandLine(exeName);
            if (pid is int found)
            {
                try
                {
                    return Process.GetProcessById(found);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            }
            return null;
        }
        int currentSessionId = Process.GetCurrentProcess().SessionId;
        var name = (await GetGameExeNameAsync(gameId, cancellation)).Replace(".exe", "");
        return Process.GetProcessesByName(name).Where(x => x.SessionId == currentSessionId && !IsProcessPending(x)).FirstOrDefault();
    }


    /// <summary>
    /// 检测进程挂起
    /// </summary>
    /// <param name="process"></param>
    /// <returns></returns>
    public static bool IsProcessPending(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }
            foreach (ProcessThread thread in process.Threads)
            {
                if (thread.ThreadState is not System.Diagnostics.ThreadState.Wait)
                {
                    return false;
                }
                else if (thread.WaitReason is not ThreadWaitReason.Suspended)
                {
                    return false;
                }
            }
            return true;
        }
        catch { }
        return true;
    }


    /// <summary>
    /// 启动游戏
    /// </summary>
    /// <returns></returns>
    public async Task<Process?> StartGameAsync(GameId gameId, string? installPath = null, CancellationToken cancellation = default)
    {
        const int ERROR_CANCELLED = 0x000004C7;
        try
        {
            if (await GetGameProcessAsync(gameId, cancellation) is Process existingProcess)
            {
                throw new Exception($"Game is running: {existingProcess.ProcessName}.exe ({existingProcess.Id}).");
            }
            string? exe = null, verb = null;
            if (Directory.Exists(installPath))
            {
                var e = Path.Join(installPath, await GetGameExeNameAsync(gameId, cancellation));
                if (File.Exists(e))
                {
                    exe = e;
                }
            }
            bool thirdPartyTool = false;
            if (string.IsNullOrWhiteSpace(exe) && AppConfig.GetEnableThirdPartyTool(gameId))
            {
                exe = GetThirdPartyToolPath(gameId);
                if (File.Exists(exe))
                {
                    thirdPartyTool = true;
                    verb = Path.GetExtension(exe) is ".exe" or ".bat" ? "runas" : "";
                }
                else
                {
                    exe = null;
                    SetThirdPartyToolPath(gameId, null);
                    _logger.LogWarning("Third party tool not found: {path}", exe);
                }
            }
            if (string.IsNullOrWhiteSpace(exe))
            {
                var folder = GetGameInstallPath(gameId);
                var name = await GetGameExeNameAsync(gameId, cancellation);
                exe = Path.Join(folder, name);
                verb = "runas";
                if (!File.Exists(exe))
                {
                    _logger.LogWarning("Game exe not found: {path}", exe);
                    throw new FileNotFoundException("Game exe not found", name);
                }
            }

            // Lunarward：上游在启动前用 winreg 写 HDR 开关，在 Linux 上等于「先跑一次 wine」，
            // 会破坏「游戏必须是 wine 的第一个进程」这条铁律（L-62）。HDR 改由设置页保存时写入目标前缀。
            // Lunarward：上游在启动前用 winreg 写 HDR 开关（Windows 上游戏与设置共用同一份注册表）。
            // Linux 上设置页写的是「已存在的前缀」，如果用户是在建前缀之前改的 HDR，或者换过前缀，
            // 注册表里就会是旧值——启动前按 AppConfig 重放一次，保证游戏读到的与界面一致（D95/B41）。
            if (gameId.GameBiz.Game is GameBiz.hk4e)
            {
                try
                {
                    await GameSettingService.SetGenshinEnableHDRAsync(gameId.GameBiz, AppConfig.EnableGenshinHDR).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Replay Genshin HDR setting before launch failed");
                }
            }
            // Lunarward：`StartGameWithCMD` 是 Windows 上「经 cmd.exe 拉起以便提权」的技巧，
            // Linux 没有对应概念，且套一层 shell 会破坏「游戏是 wine 第一个进程」的前提，
            // 因此该开关在 Linux 上忽略（保留判断以免上游字段消失时编译失败）。
            if (!thirdPartyTool && AppConfig.StartGameWithCMD && OperatingSystem.IsWindows())
            {
                arg = $"""/c start "" /d "{Path.GetDirectoryName(exe)}" "{exe}" {arg}""";
                exe = "cmd.exe";
            }
            _logger.LogInformation("Start game ({biz})\r\npath: {exe}\r\narg: {arg}", gameId, exe, arg);
            // Lunarward：Linux 启动前预检（失败即中止，并把每条原因写进异常信息与启动报告）
            Features.Runtime.RuntimeProfile? launchProfile = null;
            PreflightResult? preflight = null;
            System.Diagnostics.Stopwatch launchWatch = System.Diagnostics.Stopwatch.StartNew();
            if (!OperatingSystem.IsWindows())
            {
                // §5：首次使用自动建同名环境并绑定（只有一份没人用的配方时就地改名，不制造第二份）
                (Features.Runtime.RuntimeProfile? ensured, bool createdProfile, _) = Features.Runtime.ProfileBinding.EnsureFor(gameId.GameBiz, _logger);
                launchProfile = ensured;
                if (createdProfile && ensured is not null)
                {
                    Helpers.InAppToast.MainWindow?.Information($"已创建运行环境「{ensured.Name}」", "可在「设置 → 运行环境」里调整运行器、图形层与参数。", 6000);
                }
                // 预检要探测前缀/占用（会 spawn fuser、wine），创建前缀更可能要 wineboot 十几秒（L-127）
                preflight = await Task.Run(() => LaunchPreflight.Check(gameId.GameBiz, exe, launchProfile, _logger));
                if (!preflight.CanLaunch)
                {
                    // 预检失败也记一笔：这类「根本没起来」的尝试不会写启动报告，用户最想看的正是它
                    Features.Runtime.LaunchHistoryStore.Record(new Features.Runtime.LaunchAttempt
                    {
                        At = DateTimeOffset.Now,
                        ProfileId = launchProfile?.Id,
                        ProfileName = launchProfile?.Name,
                        GameBiz = gameId.GameBiz.Value,
                        Outcome = Features.Runtime.LaunchOutcome.PreflightFailed,
                        Detail = string.Join("；", preflight.Items
                            .Where(x => x.Status == PreflightStatus.Failed)
                            .Select(x => $"{x.Name}：{x.Detail}")),
                        DurationMs = launchWatch.ElapsedMilliseconds,
                    }, _logger);
                    throw new LaunchPreflightException(preflight);
                }
            }
            // Lunarward：Windows 用 ShellExecute + runas 提权启动；Linux 改为直接 exec wine，
            // 保证「游戏是 wine 的第一个进程」（见 docs/linux-port/03-runtime-model.md）。
            (ProcessStartInfo Info, bool ViaWine) launch = await Task.Run(() =>
            {
                var psi = CreateGameProcessStartInfo(gameId.GameBiz, exe, arg, out bool vw);
                return (psi, vw);
            });
            var info = launch.Info;
            bool viaWine = launch.ViaWine;
            _logger.LogInformation("Linux launch: {file} {args}", info.FileName, string.Join(" ", info.ArgumentList));
            if (!OperatingSystem.IsWindows())
            {
                string? reportPath = LaunchReport.Write(gameId.GameBiz, exe, arg, launchProfile, preflight!, info, viaWine);
                if (reportPath is not null)
                {
                    _logger.LogInformation("Launch report: {path}", reportPath);
                }
            }
            // R05：前缀锁在 CreateGameProcessStartInfo 里已经拿到；Start 抛异常必须在这里释放，
            // 否则一次 ENOENT/EACCES 之后同名游戏在整个进程生命周期里都起不来。
            string? launchPrefix = info.Environment.TryGetValue("WINEPREFIX", out string? envPrefix) ? envPrefix : null;
            Process? process;
            try
            {
                process = Process.Start(info);
            }
            catch
            {
                ReleasePrefixLock(launchPrefix);
                throw;
            }
            AttachSessionCleanup(process, gameId.GameBiz, launchPrefix);
            if (process != null)
            {
                if (!OperatingSystem.IsWindows())
                {
                    Features.Runtime.LaunchHistoryStore.Record(new Features.Runtime.LaunchAttempt
                    {
                        At = DateTimeOffset.Now,
                        ProfileId = launchProfile?.Id,
                        ProfileName = launchProfile?.Name,
                        GameBiz = gameId.GameBiz.Value,
                        Outcome = Features.Runtime.LaunchOutcome.Started,
                        Detail = launchProfile?.RunnerName,
                        DurationMs = launchWatch.ElapsedMilliseconds,
                    }, _logger);
                }
                // D58：进程真的起来了才记「上次能用的组合」——之后换运行器坏了可以一键切回
                if (!OperatingSystem.IsWindows() && launchProfile is not null)
                {
                    // R111：把启动链真正用的前缀记下来（默认前缀方案以前记 null，回滚时不知道回哪个）
                    Features.Runtime.LastKnownGoodStore.Remember(launchProfile, launchPrefix);
                }
                // D27：重定向了 stdout/stderr 就必须立刻有人读，否则管道写满会把游戏卡住。
                if (!OperatingSystem.IsWindows() && info.RedirectStandardOutput)
                {
                    Features.Runtime.OutputCapture.Attach($"game:{gameId.GameBiz}", process);
                }
                // B35：Linux 上 wine 进程名不是游戏 exe，按进程名轮询必然 30 秒超时；直接按 pid 记时长。
                if ((thirdPartyTool || AppConfig.StartGameWithCMD) && OperatingSystem.IsWindows())
                {
                    return await _playTimeRecorderService.StartProcessToLogAsync(gameId);
                }
                else
                {
                    await _playTimeRecorderService.StartProcessToLogAsync(gameId, process.Id);
                    return process;
                }
            }
            else if (!OperatingSystem.IsWindows())
            {
                Features.Runtime.LaunchHistoryStore.Record(new Features.Runtime.LaunchAttempt
                {
                    At = DateTimeOffset.Now,
                    ProfileId = launchProfile?.Id,
                    ProfileName = launchProfile?.Name,
                    GameBiz = gameId.GameBiz.Value,
                    Outcome = Features.Runtime.LaunchOutcome.StartFailed,
                    Detail = "进程没有起来（Process.Start 返回 null）",
                    DurationMs = launchWatch.ElapsedMilliseconds,
                }, _logger);
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == ERROR_CANCELLED)
        {
            // Operation canceled
            _logger.LogInformation("Start game operation canceled.");
        }
        return null;
    }


    private async Task AppendDX12Async(StringBuilder sb, GameId gameId, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return;
        }
        try
        {
            var gameConfig = await _hoYoPlayService.GetGameConfigAsync(gameId, cancellation);
            if (gameConfig?.EnableDxSwitch is true)
            {
                var dxConfig = await _hoYoPlayService.GetDXConfigAsync(gameId, GPUInfoHelper.GetGPUInfos(), cancellation);
                if (dxConfig?.EnableDXSwitch is true)
                {
                    sb.Append(' ');
                    bool? enable = AppConfig.GetEnableDX12(gameId);
                    if (dxConfig.AutoMode && enable.HasValue)
                    {
                        sb.Append(enable.Value ? dxConfig.CmdArgs : dxConfig.DX11CmdArgs);
                    }
                    else if (enable.HasValue)
                    {
                        sb.Append(enable.Value ? dxConfig.CmdArgs : dxConfig.DX11CmdArgs);
                    }
                    else
                    {
                        sb.Append(dxConfig.UseDX12ByDefault ? dxConfig.CmdArgs : dxConfig.DX11CmdArgs);
                    }
                }
            }
        }
        catch { }
    }


    private async Task AppendLoginAuthTicketAsync(StringBuilder sb, GameId gameId, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return;
        }
        try
        {
            if (AppConfig.AuthLoginGameByPassportAccount)
            {
                string? ticket = await _passportService.CreateAuthTicketAsync(gameId, cancellation);
                if (!string.IsNullOrWhiteSpace(ticket))
                {
                    sb.Append($" login_auth_ticket={ticket}");
                    if (AppConfig.IsAppInRemovableStorage)
                    {
                        sb.Append(" login_storage_type=session");
                    }
                    else
                    {
                        sb.Append(" login_storage_type=local");
                    }
                }
            }
        }
        catch { }
    }


    private void SetGenshinEnableHDR(GameId gameId)
    {
        if (gameId.GameBiz.Game is GameBiz.hk4e)
        {
            GameSettingService.SetGenshinEnableHDR(gameId.GameBiz, AppConfig.EnableGenshinHDR);
        }
    }


    #endregion



    #region Third Party Tool


    /// <summary>
    /// 获取第三方工具路径
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetThirdPartyToolPath(GameId gameId)
    {
        string? path = AppConfig.GetThirdPartyToolPath(gameId);
        if (!string.IsNullOrWhiteSpace(path))
        {
            path = GetFullPathIfRelativePath(path);
        }
        if (File.Exists(path))
        {
            return path;
        }
        else
        {
            AppConfig.SetThirdPartyToolPath(gameId, null);
            return null;
        }
    }


    /// <summary>
    /// 设置第三方工具路径
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static string? SetThirdPartyToolPath(GameId gameId, string? path)
    {
        if (File.Exists(path))
        {
            path = Path.GetFullPath(path);
            string relativePath = GetRelativePathIfInRemovableStorage(path, out bool removable);
            AppConfig.SetThirdPartyToolPath(gameId, relativePath);
        }
        else
        {
            path = null;
            AppConfig.SetThirdPartyToolPath(gameId, null);
        }
        return path;
    }


    #endregion


}
