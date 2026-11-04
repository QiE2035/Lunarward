// Lunarward：登录页（Linux）。
//
// 上游在本页内嵌 WebView2 打开米游社/HoYoLAB，登录完成后用
// CoreWebView2.CookieManager 取 cookie 再交给 GameRecordService。
// Uno Linux 的 WebView2 后端没有实现 CookieManager（G0-3b 实测），
// 因此改为「独立 WebKitGTK 登录窗 + 读回 cookie」（D29 / D32）：
//   LoginPage（本页）→ WebLoginService（拉起 python3 助手）→ cookie JSON → GameRecordService
// 接口与上游一致，M2 换成 GirCore 进程内实现后本页 UI 不变。
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Xaml;
using Starward.Core;
using Starward.Frameworks;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord;

public sealed partial class LoginPage : PageBase
{

    private readonly GameRecordService _gameRecordService = AppConfig.GetService<GameRecordService>();


    private const string URL_CN = "https://user.miyoushe.com/";

    private const string URL_OS = "https://www.hoyolab.com/";


    public LoginPage()
    {
        this.InitializeComponent();
    }


    private string GetGameBizUrl()
    {
        return CurrentGameBiz.IsChinaServer() ? URL_CN : URL_OS;
    }


    private async void Button_OpenLoginWindow_Click(object sender, RoutedEventArgs e)
    {
        var button = (Microsoft.UI.Xaml.Controls.Button)sender;
        button.IsEnabled = false;
        try
        {
            TextBlock_LoginStatus.Text = "登录窗口已打开，请在新窗口中完成登录（最长等待 5 分钟）…";
            bool ok = await WebLoginService.LoginAsync(GetGameBizUrl(), TimeSpan.FromMinutes(5));
            if (!ok)
            {
                TextBlock_LoginStatus.Text = "未取得登录态：窗口被关闭或等待超时，可重试。";
                return;
            }
            var cookies = WebLoginService.Current.Cookies!.Cookies;
            string cookieString = string.Join(";", cookies.Select(x => $"{x.Key}={x.Value}"));
            TextBlock_LoginStatus.Text = "已取得登录态，正在获取角色信息…";
            await _gameRecordService.AddRecordUserAsync(cookieString);
            var roles = await _gameRecordService.AddGameRolesAsync(cookieString);
            WeakReferenceMessenger.Default.Send(new GameRecordRoleChangedMessage(roles.FirstOrDefault(x => x.GameBiz == CurrentGameBiz.ToString())));
            TextBlock_LoginStatus.Text = $"登录成功，已添加 {roles.Count} 个角色。";
        }
        catch (Exception ex)
        {
            TextBlock_LoginStatus.Text = $"登录失败：{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            button.IsEnabled = true;
        }
    }


    private void Button_Finish_Click(object sender, RoutedEventArgs e)
    {
        // 上游用 WebView 的 cookie 完成；Linux 在打开登录窗时已自动完成，这里只刷新提示
        TextBlock_LoginStatus.Text = WebLoginService.Current.HasCookies
            ? "登录态已存在，可直接返回战绩页。"
            : "请先点击「打开登录窗口」完成登录。";
    }


    private void Button_GoBack_Click(object sender, RoutedEventArgs e) { }

    private void Button_GoForward_Click(object sender, RoutedEventArgs e) { }

    private void Button_RefreshOrCancel_Click(object sender, RoutedEventArgs e) { }

    private void TextBox_Uri_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) { }

}
