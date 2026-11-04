// Lunarward：游戏公告窗口。
//
// 上游在 WebView2 里注入一段 JS（去掉内嵌页背景、改字体源、接 miHoYoGameJSSDK 的关闭/打开浏览器回调），
// 并依赖 CoreWebView2.ExecuteScriptAsync 与 WebMessageReceived。Uno 6.7 的 WebView2 后端：
//   - 渲染 https 页面：可用（G0-3b 实测）
//   - ExecuteScriptAsync / CookieManager / WebMessageReceived：不可用或直接崩进程
// 因此这里保留「嵌入显示公告页」的主体验，去掉 JS 注入与消息回调；
// 「打开系统浏览器」由用户在页面内自行点击链接完成（我们的 WebView2 会把它交给 WebKit 处理）。
using System;
using System.Timers;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Starward.Core;
using Starward.Frameworks;

namespace Starward.Features.GameLauncher;

public sealed partial class GameNoticeWindow : WindowEx
{
    private readonly GameNoticeService _gameNoticeService = AppConfig.GetService<GameNoticeService>();

    private Timer? _timer;

    private string? _noticeUrl;

    public GameBiz CurrentGameBiz { get; set; }

    public nint ParentWindowHandle { get; set; }

    public GameNoticeWindow()
    {
        this.InitializeComponent();
        Closed += (_, _) => WeakReferenceMessenger_Closed();
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "open notice in browser failed");
        }
    }


    private void WeakReferenceMessenger_Closed()
    {
        _timer?.Stop();
        _timer?.Dispose();
        WeakReferenceMessenger.Default.Send(new GameNoticeWindowClosedMessage());
    }

    private void Button_OpenInBrowser_Click(object sender, RoutedEventArgs e)
    {
        // Uno 的 WebView2 不能注入 JS、也拿不到页面内链接的点击，给用户一个明确的出口。
        if (!string.IsNullOrEmpty(_noticeUrl))
        {
            OpenInBrowser(_noticeUrl);
        }
    }


    public new void Activate()
    {
        // 上游按父窗口把公告窗内嵌成一条横幅（Windows 才能拿 HWND）；Linux 先独立弹出，位置交给合成器。
        base.Activate();
    }

    private async void Grid_Loaded(object sender, RoutedEventArgs e)
    {
        // 上游要求父窗口句柄，拿不到就关；Linux 没有句柄，改成独立窗口照常显示。
        _timer = new Timer(30000) { AutoReset = false };
        _timer.Elapsed += (_, _) => DispatcherQueue.TryEnqueue(Close);
        _timer.Start();

        try
        {
            string url = _gameNoticeService.GetGameNoticeUrl(CurrentGameBiz);
            _noticeUrl = url;
            Serilog.Log.Information("notice window loading {url}", url);
            webview.Source = new Uri(url);
            webview.Visibility = Visibility.Visible;
            // Uno 的 WebView2 事件支持不全（G0-3b）；这里只做「失败就交给浏览器」的兜底，
            // 真正的公告正文是否可见要等真机启动游戏后带 authkey 的 URL 才能验证。
            webview.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    Serilog.Log.Warning("notice webview navigation failed, fallback to browser");
                    OpenInBrowser(url);
                }
            };
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "initialize game notice failed");
            Close();
        }
    }
}
