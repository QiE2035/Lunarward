// Lunarward：游戏公告窗口。
//
// 上游在 WebView2 里注入一段 JS（去掉内嵌页背景、改字体源、接 miHoYoGameJSSDK 的关闭/打开浏览器回调），
// 并依赖 CoreWebView2.ExecuteScriptAsync 与 WebMessageReceived。Uno 6.7 的 WebView2 后端：
//   - 渲染 https 页面：可用（G0-3b 实测）
//   - ExecuteScriptAsync / CookieManager / WebMessageReceived：不可用或直接崩进程
// 因此这里保留「嵌入显示公告页」的主体验，去掉 JS 注入与消息回调；
// 「打开系统浏览器」由用户在页面内自行点击链接完成（我们的 WebView2 会把它交给 WebKit 处理）。
using System;
using System.Threading.Tasks;
using System.Timers;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Core;
using Starward.Frameworks;

namespace Starward.Features.GameLauncher;

public sealed partial class GameNoticeWindow : WindowEx
{
    private readonly GameNoticeService _gameNoticeService = AppConfig.GetService<GameNoticeService>();

    private Timer? _timer;

    private string? _noticeUrl;

    private bool _noticeRendered;

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
        // Uno X11 的 Window.Activate() 只做 raise，不会把尚未 map 的窗口显示出来（实测点小喇叭无任何窗口），
        // 所以这里先显式 AppWindow.Show()，再 activate 拿焦点。
        try
        {
            AppWindow.Show();
            // Uno X11 不会把 XAML 里设的 Title 写到 WM_NAME，窗口尺寸也停在默认 1024x640（L-35 同族）。
            // Uno 的 X11 后端写 WM_NAME 时把 UTF-8 当 Latin-1，中文标题会显示成「æ...」乱码（真机截图）；
            // 串行化窗口标题先用 ASCII，中文标题等能直接写 _NET_WM_NAME 再补。
            AppWindow.Title = "Game Notice";
            double scale = Content?.XamlRoot?.RasterizationScale ?? 1d;
            var target = new Windows.Graphics.SizeInt32 { Width = (int)(900 * scale), Height = (int)(640 * scale) };
            AppWindow.Resize(target);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "show notice window failed");
        }
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
            // Uno 的 WebView2 在本机（Skia + WebKitGTK）实测只显示黑底转圈：页面既不渲染也不报错。
            // 10 秒还没成功导航就把「不可用」说清楚，避免用户对着转圈干等。
            _ = DispatcherQueue.TryEnqueue(async () =>
            {
                await Task.Delay(10000);
                try
                {
                    if (!_noticeRendered)
                    {
                        TextBlock_Fallback.Text = "内置浏览器没能加载这条公告（当前 Linux 构建的 WebView 后端限制）。\n请点击右下角「在浏览器中打开」查看。";
                        TextBlock_Fallback.Visibility = Visibility.Visible;
                        webview.Visibility = Visibility.Collapsed;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "notice window fallback failed");
                }
            });
            webview.NavigationCompleted += (_, args) =>
            {
                if (args.IsSuccess)
                {
                    _noticeRendered = true;
                }
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
