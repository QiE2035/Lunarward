// Lunarward：米游社 / HoYoLAB 登录态获取。
//
// 为什么不是 WebView2：Uno Linux 的 WebView2 后端（WebKitGTK）没有实现
// CoreWebView2.CookieManager（G0-3b 实测抛 NotImplementedException），
// 拿不到 cookie 就无法调用 GameRecordClient。
//
// M1 做法（临时、可替换）：启动独立 WebKitGTK 登录窗（复用 M0 验证过的
// poc/login-spike/login.py），登录完成后把 cookie 写成 JSON 返回。
// M2 会用 GirCore（Gtk4/WebKit6 的 .NET 绑定）把这一步换成进程内实现，
// 接口不变（见 docs/linux-port/09「登录态」一节与 D29）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Starward.Features.GameRecord;

public class LoginCookies
{
    public Dictionary<string, string> Cookies { get; set; } = new();

    public DateTime Time { get; set; }

    public string? Uid { get; set; }
}

public static class WebLoginService
{
    public static WebLoginServiceState Current { get; } = new();

    /// <summary>登录助手脚本位置：构建产物里在 Assets/login-helper/ 下，安装后可能在 login-helper/ 下。</summary>
    private static string? ResolveHelper()
    {
        string[] candidates =
        [
            Path.Combine(AppContext.BaseDirectory, "Assets", "login-helper", "login.py"),
            Path.Combine(AppContext.BaseDirectory, "login-helper", "login.py"),
        ];
        return candidates.FirstOrDefault(File.Exists);
    }


    public static string CookiesFile => Path.Combine(AppConfig.CacheFolder ?? Path.GetTempPath(), "login-cookies.json");


    /// <summary>会话 cookie 只给自己读：默认 umask 下 0644 同机别的用户也能读（L-62）。</summary>
    private static void RestrictCookiesFile()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }
        try
        {
            if (File.Exists(CookiesFile))
            {
                File.SetUnixFileMode(CookiesFile, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebLoginService] chmod cookies failed: {ex.Message}");
        }
    }

    /// <summary>打开登录窗并等待 cookie 落盘。返回 false 表示用户取消或超时。</summary>
    public static async Task<bool> LoginAsync(string url, TimeSpan timeout)
    {
        string? helper = ResolveHelper();
        if (helper is null)
        {
            System.Diagnostics.Debug.WriteLine("[WebLoginService] login-helper 缺失（Assets/login-helper/login.py）");
            return false;
        }
        try
        {
            File.Delete(CookiesFile);
        }
        catch { }

        var psi = new ProcessStartInfo("python3")
        {
            ArgumentList = { helper, "--url", url, "--output", CookiesFile },
            UseShellExecute = false,
        };
        // WebKitGTK 在部分驱动/合成器下需要关掉 DMABUF 渲染，Wayland 会话还要强制 X11（L-12）
        psi.Environment["WEBKIT_DISABLE_DMABUF_RENDERER"] = "1";
        if (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") == "wayland")
        {
            psi.Environment["GDK_BACKEND"] = "x11";
        }
        using var process = Process.Start(psi);
        if (process is null)
        {
            return false;
        }
        // 助手在检测到登录 cookie 后会自行退出；这里也轮询输出文件，
        // 这样用户即使不关登录窗口，也能在登录成功当刻拿到登录态。
        using var cts = new System.Threading.CancellationTokenSource(timeout);
        try
        {
            while (!cts.IsCancellationRequested)
            {
                if (process.HasExited)
                {
                    break;
                }
                if (await HasCookiesFileAsync())
                {
                    try { process.Kill(entireProcessTree: true); } catch { }
                    break;
                }
                await Task.Delay(500, cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
        }
        finally
        {
            RestrictCookiesFile();
        }
        return await LoadAsync();
    }

    private static async Task<bool> HasCookiesFileAsync()
    {
        try
        {
            if (!File.Exists(CookiesFile))
            {
                return false;
            }
            string json = await File.ReadAllTextAsync(CookiesFile);
            var cookies = JsonSerializer.Deserialize<LoginCookies>(json);
            return cookies?.Cookies is { Count: > 0 };
        }
        catch
        {
            return false;
        }
    }


    public static async Task<bool> LoadAsync()
    {
        try
        {
            if (!File.Exists(CookiesFile))
            {
                return false;
            }
            var json = await File.ReadAllTextAsync(CookiesFile);
            var cookies = JsonSerializer.Deserialize<LoginCookies>(json);
            if (cookies?.Cookies is null || cookies.Cookies.Count == 0)
            {
                return false;
            }
            Current.SetCookies(cookies);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[WebLoginService] load failed: {ex.Message}");
            return false;
        }
    }
}

public class WebLoginServiceState
{
    public LoginCookies? Cookies { get; private set; }

    public bool HasCookies => Cookies is { Cookies.Count: > 0 };

    public void SetCookies(LoginCookies cookies) => Cookies = cookies;

    public void Clear() => Cookies = null;

    public string? GetCookie(string name) => Cookies is not null && Cookies.Cookies.TryGetValue(name, out var v) ? v : null;
}
