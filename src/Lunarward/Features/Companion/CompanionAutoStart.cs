// Lunarward：游戏启动后按列表顺序拉起「伴随启动」的条目（D24/D28）。
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Features.GameLauncher;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Starward.Features.Companion;

public static class CompanionAutoStart
{
    private static CompanionRunner? _runner;

    private static ILogger? _logger;

    /// <summary>在 App 启动时调用一次。</summary>
    public static void Register(CompanionRunner runner, ILogger? logger = null)
    {
        _runner = runner;
        _logger = logger;
        WeakReferenceMessenger.Default.Register<GameStartedMessage>(typeof(CompanionAutoStart), (_, _) => OnGameStarted());
    }

    private static async void OnGameStarted()
    {
        try
        {
            GameBiz biz = AppConfig.CurrentGameBiz;
            if (_runner is null)
            {
                return;
            }
            // 游戏启动后这一轮只处理 on_game_started / after_delay；
            // on_game_exit 由启动器在进程退出时单独触发（见 GameLauncherService.Linux）。
            IReadOnlyList<CompanionEntry> entries = CompanionStore.Load(biz);
            int count = entries.Count(x => x.Enabled && x.Companion
                && (x.Trigger is "on_game_started" or "after_delay"));
            if (count == 0)
            {
                return;
            }
            _logger?.LogInformation("Starting {count} companion(s) for {biz}", count, biz.Value);
            Helpers.InAppToast.MainWindow?.Information(null, $"正在启动 {count} 个伴随程序", 3000);
            foreach (string trigger in new[] { "on_game_started", "after_delay" })
            {
                await _runner.StartCompanionsAsync(biz, trigger);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Companion auto start failed");
        }
    }
}
