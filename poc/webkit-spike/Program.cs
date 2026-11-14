using System;
using GLib;
using Gtk;
using WebKit;

// 最小 spike：进程内起 Gtk4 窗口 + WebKitGTK 6.0 的 WebView。
// 目的：①GirCore 的 Gtk4 / WebKit-6.0 绑定在本机能不能跑；②给「能否与 Uno 的 X11 主机同进程共存」提供基线。
int seconds = args.Length > 0 && int.TryParse(args[0], out int s) ? s : 15;

var app = Application.New("io.lunarward.webkitspike", 0);
app.OnActivate += (sender, _) =>
{
    var application = (Application)sender;
    var window = ApplicationWindow.New(application);
    window.Title = "Lunarward WebKitGTK 6.0 spike";
    window.SetDefaultSize(900, 640);

    var webView = new WebView();
    webView.LoadHtml(
        "<html><body style='font-family:sans-serif;padding:24px'>" +
        "<h2>WebKitGTK 6.0 in-process ✅</h2>" +
        "<p id='v'></p><p id='c'></p>" +
        "<script>" +
        "document.getElementById('v').textContent = 'UA: ' + navigator.userAgent;" +
        "document.getElementById('c').textContent = 'cookieEnabled: ' + navigator.cookieEnabled;" +
        "</script></body></html>",
        "about:blank");

    window.SetChild(webView);
    window.Present();

    Console.WriteLine($"[spike] window presented");
    Console.WriteLine($"[spike] webkit: {WebKit.Functions.GetMajorVersion()}.{WebKit.Functions.GetMinorVersion()}.{WebKit.Functions.GetMicroVersion()}");
    Console.WriteLine($"[spike] network-session present: {webView.GetNetworkSession() is not null}");

    GLib.Functions.TimeoutAddSeconds(0, (uint)seconds, () => { application.Quit(); return false; });
};

return app.Run(args);
