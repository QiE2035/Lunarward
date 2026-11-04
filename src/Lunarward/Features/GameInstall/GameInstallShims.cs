// Lunarward：D5/D25 —— M1 只识别已有安装，完整的下载/解压/修复流程依赖 Starward.RPC
// （gRPC + 命名管道，D6 计划换成 Unix domain socket）。
// 本文件按上游公开契约提供最小实现，让启动器页面与设置对话框原样编译；
// 枚举值、字段语义与 src/Starward.RPC/GameInstall/* 保持一致，后续替换实现时上层不用改。
using Starward.Core;
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Starward.RPC.GameInstall;

public enum GameInstallState
{
    Stop = 0,
    Waiting = 1,
    Downloading = 2,
    Decompressing = 3,
    Merging = 4,
    Verifying = 5,
    Paused = 6,
    Finish = 7,
    Error = 8,
    Queueing = 9,
}

public enum GameInstallOperation
{
    None = 0,
    Install = 1,
    Predownload = 2,
    Update = 3,
    Repair = 4,
    Uninstall = 5,
}

public enum GameInstallDownloadMode
{
    Undefined = 0,
    SingleFile = 1,
    CompressedPackage = 2,
    Chunk = 4,
    Patch = 8,
}

public class GameInstallContext
{
    public GameId GameId { get; init; } = null!;

    public string InstallPath { get; init; } = "";

    public GameInstallOperation Operation { get; set; }

    public AudioLanguage AudioLanguage { get; init; }

    public string? HardLinkPath { get; init; }

    public long Timestamp { get; set; }

    public GameInstallState State { get; set; }

    public string? ErrorMessage { get; set; }

    public GameInstallDownloadMode DownloadMode { get; set; }

    public long Progress_DownloadTotalBytes { get; set; }

    public long Progress_DownloadFinishBytes { get; set; }

    public long Progress_ReadTotalBytes { get; set; }

    public long Progress_ReadFinishBytes { get; set; }

    public long Progress_WriteTotalBytes { get; set; }

    public long Progress_WriteFinishBytes { get; set; }

    public double Progress_Percent { get; set; }

    public long NetworkDownloadSpeed { get; set; }

    public long StorageReadSpeed { get; set; }

    public long StorageWriteSpeed { get; set; }

    public long RemainTimeSeconds { get; set; }
}

public class GameInstallTaskStartedMessage
{
    public GameInstallTaskStartedMessage(GameInstallContext installTask) => InstallTask = installTask;

    public GameInstallContext InstallTask { get; }
}

/// <summary>实现见 <see cref="Starward.RPC.GameInstall.GameInstallService"/> 的 Linux 分部类。</summary>
public partial class GameInstallService
{
}
