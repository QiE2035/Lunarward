// Lunarward：L1.5「进程内 UI 自检」（D65 里的 --selftest-ui）。
//
// 为什么需要它：Uno 的 Skia/X11 后端不发布 AT-SPI 树，外部工具（Uno.UITest 也不支持 Skia）看不到控件；
// 但 Uno 自家的 RuntimeTests 在 SkiaX11 上跑的是「进程内断言」——直接读视觉树、DependencyProperty 和布局结果。
// 这里沿用同一思路：进程起来、窗口显示后，在 UI 线程上按清单核对关键不变量，输出 PASS/FAIL 并给退出码。
//
// 它不注入任何系统输入，所以 `xvfb-run -a ./Lunarward --selftest-ui` 可以进 CI；但它也只能覆盖
// 「结构/资源/导航」这类可断言的东西，交互与观感仍要靠 L3（人工 + 截图）与 L2（CLI）。
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Features.GameLauncher;
using Starward.Features.GameSetting;
using Starward.Features.Setting;
using Starward.Features.ViewHost;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Starward.Features.Dev;


internal static class UiSelfTest
{

    private sealed record Check(string Name, Func<Task<(bool Ok, string Detail)>> Run);


    /// <summary>
    /// 跑完整套自检；返回失败条数（0 = 全通过）。
    /// </summary>
    public static async Task<int> RunAsync(MainWindow window, TimeSpan settle)
    {
        // 等首帧稳定：窗口 map、GameLauncherPage 首次 OnLoaded、背景层快速图加载都在这个窗口里完成
        await Task.Delay(settle);

        var checks = new List<Check>
        {
            new("窗口已显示且有尺寸", () =>
            {
                bool ok = window.Bounds.Width > 0 && window.Bounds.Height > 0;
                return Task.FromResult((ok, $"bounds {window.Bounds.Width:0}x{window.Bounds.Height:0}"));
            }),

            new("主内容区是 MainView", () =>
            {
                bool ok = window.RootView is not null;
                return Task.FromResult((ok, window.RootView?.GetType().Name ?? "(null)"));
            }),

            new("启动器页已加载", () =>
            {
                MainView? view = window.RootView;
                Type? page = view?.CurrentPageType;
                bool ok = page == typeof(GameLauncherPage);
                return Task.FromResult((ok, page?.Name ?? "(null)"));
            }),

            new("导航到游戏设置页后内容非空", async () =>
            {
                window.OpenGameSettings();
                await Task.Delay(400);
                MainView? view = window.RootView;
                Type? page = view?.CurrentPageType;
                bool ok = page == typeof(GameSettingPage) && view?.CurrentPageContent is not null;
                return (ok, page?.Name ?? "(null)");
            }),

            new("导航到设置页后内容非空", async () =>
            {
                window.OpenSettings();
                await Task.Delay(400);
                MainView? view = window.RootView;
                Type? page = view?.CurrentPageType;
                bool ok = page == typeof(SettingPage) && view?.CurrentPageContent is not null;
                return (ok, page?.Name ?? "(null)");
            }),

            new("关键画刷资源存在", () =>
            {
                var resources = Application.Current.Resources;
                bool ok = resources.TryGetValue("ApplicationPageBackgroundThemeBrush", out object? brush)
                          && brush is not null
                          && resources.TryGetValue("SystemAccentColor", out object? accent)
                          && accent is not null;
                return Task.FromResult((ok, ok ? "背景/强调色已定义" : "缺 ApplicationPageBackgroundThemeBrush 或 SystemAccentColor"));
            }),

            new("运行时方案能被解析", () =>
            {
                // 只读取，不创建：确认 ProfileBinding 在无绑定/损坏绑定下也能给出一个可用方案（D46 §3 的可观测性）
                Starward.Features.Runtime.RuntimeProfile profile =
                    Starward.Features.Runtime.ProfileBinding.Resolve(null);
                bool ok = !string.IsNullOrEmpty(profile.Id) && !string.IsNullOrEmpty(profile.Name);
                return Task.FromResult((ok, $"{profile.Name} ({profile.RunnerType})"));
            }),

            new("主窗口标题正确", () =>
            {
                bool ok = string.Equals(window.Title, "Lunarward", StringComparison.Ordinal)
                          || string.Equals(window.AppWindow?.Title, "Lunarward", StringComparison.Ordinal);
                return Task.FromResult((ok, window.AppWindow?.Title ?? window.Title ?? "(null)"));
            }),
        };

        int failed = 0;
        foreach (Check check in checks)
        {
            bool ok;
            string detail;
            try
            {
                (ok, detail) = await check.Run();
            }
            catch (Exception ex)
            {
                ok = false;
                detail = ex.GetType().Name + ": " + ex.Message;
            }
            if (!ok)
            {
                failed++;
            }
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {check.Name}    {detail}");
        }
        Console.WriteLine($"──────── selftest-ui 汇总 ────────");
        Console.WriteLine($"通过 {checks.Count - failed} / 失败 {failed}");
        return failed;
    }

}
