// Lunarward：上游 BBSWebBridge 内嵌 WebView2 加载米游社/HoYoLAB 页面，
// 通过 CoreWebView2.CookieManager 注入/提取登录态并执行脚本桥接。
// Uno Linux 的 WebView2 后端是 WebKitGTK，但 CookieManager / ExecuteScriptAsync
// 均未实现（G0-3b 实测），因此这里改用「独立 WebKitGTK 登录窗 + cookie 文件」的
// 方案（D29）：WebView2 只负责渲染页面内容，登录态由 WebLoginService 提供。
using Microsoft.UI.Xaml.Controls;
using Starward.Core;

namespace Starward.Features.GameRecord;

public sealed partial class BBSWebBridge : UserControl
{
    public GameBiz GameBiz { get; set; }

    public bool IsHoyolab => GameBiz.ToGame() == GameBiz.hk4e_global;

    /// <summary>是否已具备可用的登录态（由 WebLoginService 判定）。</summary>
    public bool IsLoggedIn => WebLoginService.Current.HasCookies;

    public CommunityToolkit.Mvvm.Input.IRelayCommand CloseScreenshotGridCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
    public CommunityToolkit.Mvvm.Input.IRelayCommand CopyScreenshotCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
    public CommunityToolkit.Mvvm.Input.IRelayCommand SaveScreenshotCommand { get; } = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });

    /// <summary>网页标题（上游从 WebView2 的 DocumentTitle 同步；Linux 走独立登录窗口，暂无标题来源）。</summary>
    public string? DocumentTitle { get; set; }
}
