using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Media;

namespace LunarwardPoc;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        this.InitializeComponent();

        GachaList.ItemsSource = new List<GachaRow>
        {
            new("阿蕾奇诺", "第 74 抽", "#E05C6E"),
            new("芙宁娜", "第 12 抽", "#5CA8E0"),
            new("那维莱特", "第 88 抽", "#C46FE0"),
            new("绮良良", "第 3 抽", "#7FD98A"),
            new("香菱", "第 21 抽", "#E0A05C"),
            new("班尼特", "第 37 抽", "#E0A05C"),
        };

        // PoC 专用：允许用 --page=xxx 指定初始页，便于逐页截图（不进最终产品）
        var arg = System.Environment.GetCommandLineArgs()
            .FirstOrDefault(a => a.StartsWith("--page="));
        if (arg is not null)
        {
            var tag = arg["--page=".Length..];
            SelectByTag(tag);
            Loaded += (_, _) => { if (tag is "webview" or "compat")
            {
                ApplyPage(tag);
                if (tag == "webview") ProbeSetup();
                var sc = System.Environment.GetCommandLineArgs().FirstOrDefault(a => a.StartsWith("--scroll="));
                if (sc is not null) PageCompat.ScrollTo(double.Parse(sc["--scroll=".Length..]));
                if (System.Environment.GetCommandLineArgs().Contains("--dialog")) PageCompat.ShowProbeDialog();
            } };
        }
    }

    private void SelectByTag(string tag)
    {
        foreach (var item in Nav.MenuItems)
        {
            if (item is NavigationViewItem nvi && (nvi.Tag as string) == tag)
            {
                Nav.SelectedItem = nvi;
                ApplyPage(tag);
                return;
            }
        }
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        ApplyPage((args.SelectedItemContainer?.Tag as string) ?? "launcher");
    }

    // PoC 尖峰：Uno WebView2（Linux = WebKitGTK）能力探针。
    // --page=webview        只渲染登录页（可用）
    // --page=webview --js-probe 额外调用 ExecuteScriptAsync（实测会让进程退出，见 poc/GATES.md G0-3）
    private async void ProbeSetup()
    {
        await Probe.EnsureCoreWebView2Async();
        System.Console.Error.WriteLine("[poc] EnsureCoreWebView2Async ok");
        System.Console.Error.WriteLine("[poc] DocumentTitle=" + Probe.CoreWebView2.DocumentTitle);

        if (System.Environment.GetCommandLineArgs().Contains("--js-probe"))
        {
            System.Console.Error.WriteLine("[poc] calling ExecuteScriptAsync ...");
            var r = await Probe.ExecuteScriptAsync("1+1");
            System.Console.Error.WriteLine("[poc] js=" + r);
        }
    }

    private void ApplyPage(string tag)
    {
        System.Console.Error.WriteLine($"[poc] ApplyPage {tag}");
        PageLauncher.Visibility   = tag == "launcher" ? Visibility.Visible : Visibility.Collapsed;
        PageGacha.Visibility      = tag == "gacha"    ? Visibility.Visible : Visibility.Collapsed;
        PageShot.Visibility       = tag == "shot"     ? Visibility.Visible : Visibility.Collapsed;
        PageGSetting.Visibility   = tag == "gsetting" ? Visibility.Visible : Visibility.Collapsed;
        PageControl.Visibility    = tag == "control"  ? Visibility.Visible : Visibility.Collapsed;
        PageSetting.Visibility    = tag == "setting"  ? Visibility.Visible : Visibility.Collapsed;
        WebHost.Visibility        = tag == "webview"  ? Visibility.Visible : Visibility.Collapsed;
        PageCompat.Visibility     = tag == "compat"   ? Visibility.Visible : Visibility.Collapsed;
    }

    public sealed record GachaRow(string Name, string Pulls, string Accent);
}
