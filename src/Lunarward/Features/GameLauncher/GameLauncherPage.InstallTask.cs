// Lunarward：安装 / 预下载进度显示。
//
// 上游把进度画在 StartGameButton 里（通过 RPC 任务消息）；移植侧不拆进程（D128），
// 任务进度在 GameInstallProgress 上，这里每秒读一次并画在页面底部的一行状态条里。
// D128：上游 0.19.0 用 RunnerState/GameInstallProgress 取代了移植侧的 GameInstallContext/GameInstallState。

using Starward.Core.HoYoPlay;
using Starward.GameInstall;

using Microsoft.UI.Xaml;
using System;

namespace Starward.Features.GameLauncher;


public sealed partial class GameLauncherPage
{

    private DispatcherTimer? _installTaskTimer;

    private RunnerState _lastInstallState = RunnerState.Stop;


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
            var service = AppConfig.GetService<Features.GameInstall.GameInstallService>();
            GameId? gameId = CurrentGameId;
            if (gameId is null)
            {
                StackPanel_InstallTask.Visibility = Visibility.Collapsed;
                return;
            }
            GameInstallProgress? task = service.GetGameInstallTask(gameId);
            if (task is null)
            {
                StackPanel_InstallTask.Visibility = Visibility.Collapsed;
                return;
            }
            bool active = task.State is RunnerState.Running or RunnerState.Paused or RunnerState.Queue;
            if (!active)
            {
                if (_lastInstallState != task.State)
                {
                    _lastInstallState = task.State;
                    if (task.State is RunnerState.Finish)
                    {
                        Helpers.InAppToast.MainWindow?.Success(null, "下载完成", 4000);
                    }
                    else if (task.State is RunnerState.Error)
                    {
                        Helpers.InAppToast.MainWindow?.Error((string?)null, $"{Starward.Language.Lang.Lunarward_Error_InstallFailed}{task.ErrorMessage}", 8000);
                    }
                }
                StackPanel_InstallTask.Visibility = Visibility.Collapsed;
                return;
            }

            _lastInstallState = task.State;
            StackPanel_InstallTask.Visibility = Visibility.Visible;
            ProgressBar_InstallTask.Value = task.PercentProgress * 100;
            TextBlock_InstallTask.Text = DescribeTask(task);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "update install task failed");
            StackPanel_InstallTask.Visibility = Visibility.Collapsed;
        }
    }


    private string DescribeTask(GameInstallProgress task)
    {
        string action = task.Operation switch
        {
            GameInstallOperation.Predownload => "预下载",
            GameInstallOperation.Install => "安装",
            GameInstallOperation.Repair => "修复",
            GameInstallOperation.UpdateCutscene => "过场动画",
            GameInstallOperation.UpdatePlugin => "插件",
            GameInstallOperation.HotUpdate => "热更新",
            _ => "更新",
        };
        if (task.State is RunnerState.Queue)
        {
            return $"{action}：正在准备…";
        }
        if (task.State is RunnerState.Paused)
        {
            return $"{action}已暂停 · {task.PercentProgress:P1}";
        }
        string percent = $"{task.PercentProgress:P1}";
        if (task.NetworkDownloadSpeed > 0)
        {
            string speed = FormatSize(task.NetworkDownloadSpeed) + "/s";
            if (task.RemainTimeSeconds > 0)
            {
                return $"{action}中 {percent} · {FormatSize(task.NetworkFinishedBytes)} / {FormatSize(task.NetworkTotalBytes)} · {speed} · 剩余 {FormatRemain(task.RemainTimeSeconds)}";
            }
            return $"{action}中 {percent} · {FormatSize(task.NetworkFinishedBytes)} / {FormatSize(task.NetworkTotalBytes)} · {speed}";
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
