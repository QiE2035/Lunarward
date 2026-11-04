// Lunarward：安装 / 预下载进度显示。
//
// 上游把进度画在 StartGameButton 里（通过 RPC 任务消息）；Linux 侧 M3 的任务在主进程内，
// 进度在 GameInstallContext 上，这里每秒读一次并画在页面底部的一行状态条里。

using Microsoft.UI.Xaml;
using Starward.RPC.GameInstall;
using System;

namespace Starward.Features.GameLauncher;


public sealed partial class GameLauncherPage
{

    private DispatcherTimer? _installTaskTimer;

    private GameInstallState _lastInstallState = GameInstallState.Stop;


    private void StartInstallTaskWatcher()
    {
        _installTaskTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _installTaskTimer.Tick -= InstallTaskTimer_Tick;
        _installTaskTimer.Tick += InstallTaskTimer_Tick;
        _installTaskTimer.Start();
        UpdateInstallTask();
    }


    private void StopInstallTaskWatcher()
    {
        _installTaskTimer?.Stop();
    }


    private void InstallTaskTimer_Tick(object? sender, object e)
    {
        UpdateInstallTask();
    }


    private void UpdateInstallTask()
    {
        try
        {
            var service = AppConfig.GetService<GameInstallService>();
            GameInstallContext? task = service.GetGameInstallTask(CurrentGameId);
            if (task is null)
            {
                StackPanel_InstallTask.Visibility = Visibility.Collapsed;
                return;
            }
            bool active = task.State is GameInstallState.Waiting or GameInstallState.Downloading
                          or GameInstallState.Verifying or GameInstallState.Merging or GameInstallState.Decompressing;
            if (!active)
            {
                if (_lastInstallState != task.State)
                {
                    _lastInstallState = task.State;
                    if (task.State is GameInstallState.Finish)
                    {
                        Helpers.InAppToast.MainWindow?.Success(null, "下载完成", 4000);
                    }
                    else if (task.State is GameInstallState.Error)
                    {
                        Helpers.InAppToast.MainWindow?.Error((string?)null, $"{Starward.Language.Lang.Lunarward_Error_InstallFailed}{task.ErrorMessage}", 8000);
                    }
                }
                StackPanel_InstallTask.Visibility = Visibility.Collapsed;
                return;
            }

            _lastInstallState = task.State;
            StackPanel_InstallTask.Visibility = Visibility.Visible;
            ProgressBar_InstallTask.Value = task.Progress_Percent;
            TextBlock_InstallTask.Text = DescribeTask(task);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "update install task failed");
            StackPanel_InstallTask.Visibility = Visibility.Collapsed;
        }
    }


    private static string DescribeTask(GameInstallContext task)
    {
        string action = task.Operation switch
        {
            GameInstallOperation.Predownload => "预下载",
            GameInstallOperation.Install => "安装",
            GameInstallOperation.Repair => "修复",
            _ => "更新",
        };
        if (task.State is GameInstallState.Waiting)
        {
            return $"{action}：正在准备…";
        }
        string percent = $"{task.Progress_Percent:P1}";
        if (task.NetworkDownloadSpeed > 0)
        {
            string speed = FormatSize(task.NetworkDownloadSpeed) + "/s";
            if (task.RemainTimeSeconds > 0)
            {
                return $"{action}中 {percent} · {FormatSize(task.Progress_DownloadFinishBytes)} / {FormatSize(task.Progress_DownloadTotalBytes)} · {speed} · 剩余 {FormatRemain(task.RemainTimeSeconds)}";
            }
            return $"{action}中 {percent} · {FormatSize(task.Progress_DownloadFinishBytes)} / {FormatSize(task.Progress_DownloadTotalBytes)} · {speed}";
        }
        return $"{action}中 {percent}";
    }


    private static string FormatRemain(long seconds)
    {
        if (seconds >= 3600)
        {
            return $"{seconds / 3600} 小时 {seconds % 3600 / 60} 分";
        }
        if (seconds >= 60)
        {
            return $"{seconds / 60} 分 {seconds % 60} 秒";
        }
        return $"{seconds} 秒";
    }


    private static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }
        return unit >= 3 ? $"{value:0.00} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }

}
