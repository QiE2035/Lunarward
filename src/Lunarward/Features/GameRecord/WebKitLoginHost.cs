// Lunarward：登录助手（D76，A 方案）。
//
// 为什么是独立进程：不想把 Gtk4 塞进 Uno 进程。B 方案（同进程）实测能跑，但
//   ① Uno 自带 GtkSharp 3.24（GTK3），同进程同时有 GTK3 + GTK4，属不受推荐组合（`poc/uno-smoke` 的 `GtkWebProbe.cs`）；
//   ② GTK 窗口的标题栏由 WM 装饰，颜色跟系统配色走，观感上「像另一个程序」。
// 所以登录窗仍是独立进程，只是实现从「python3 + WebKitGTK 4.1」换成「本进程 + WebKitGTK 6.0」：
// 去掉 python3 依赖，cookie 也不再需要读 `moz_cookies`（直接读 WebKit 的持久化 cookie 库）。
//
// 调用方式（由 WebLoginService 拉起）：
//   lunarward --webkit-login --url <登录页> --output <cookies.json> [--timeout 300]
//
// 输出格式与 python 助手保持一致：{ "Cookies": { name: value }, "Time": "..." }。
extern alias gir;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using Gtk4 = gir::Gtk;
using WebKit6 = gir::WebKit;

namespace Starward.Features.GameRecord;

internal static class WebKitLoginHost
{
    /// <summary>登录成功的判据：与 python 助手（Assets/login-helper/login.py）保持一致。</summary>
    private static readonly string[] LoginMarkers =
    [
        "ltuid_v2", "ltmid_v2", "ltoken_v2", "cookie_token_v2", "account_id", "cookie_token", "ltuid",
    ];

    private static bool _resolverInstalled;


    /// <summary>WebKitGTK 6.0 在不在。只做文件探测 —— 绝不在主进程里把 GTK4 加载进来。</summary>
    public static bool IsAvailable()
    {
        string[] names = ["libwebkitgtk-6.0.so.4", "libwebkitgtk-6.0.so"];
        foreach (string name in names)
        {
            if (NativeLibrary.TryLoad(name, out IntPtr handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }
        }
        return false;
    }


    /// <summary>GirCore 的 DllImport 名是 WebKit / JavaScriptCore，映射到 6.0 的 soname（同 L-57 的 libzstd）。</summary>
    private static void InstallResolver()
    {
        if (_resolverInstalled)
        {
            return;
        }
        NativeLibrary.SetDllImportResolver(typeof(WebKit6.WebView).Assembly, Resolve);
        _resolverInstalled = true;
    }


    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        string[] candidates = libraryName switch
        {
            "WebKit" => ["libwebkitgtk-6.0.so.4", "libwebkitgtk-6.0.so", "libwebkitgtk-6.0.so.4.19.5"],
            "JavaScriptCore" => ["libjavascriptcoregtk-6.0.so.1", "libjavascriptcoregtk-6.0.so"],
            _ => [],
        };
        foreach (string candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, out IntPtr handle))
            {
                return handle;
            }
        }
        return IntPtr.Zero;
    }


    /// <summary>`lunarward --webkit-login …` 的入口。返回进程退出码。</summary>
    public static int Run(string[] argv)
    {
        ParseArgs(argv, out string? url, out string? output, out int timeoutSeconds);
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(output))
        {
            Console.Error.WriteLine("用法：lunarward --webkit-login --url <登录页> --output <cookies.json> [--timeout 秒]");
            return 1;
        }

        string fullOutput = Path.GetFullPath(output);
        string? directory = Path.GetDirectoryName(fullOutput);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        string cookieDb = fullOutput + ".db";

        InstallResolver();

        var state = new HostState(url, fullOutput);

        var app = Gtk4.Application.New("io.lunarward.login", 0);
        app.OnActivate += (sender, _) =>
        {
            var application = (Gtk4.Application)sender;
            var window = Gtk4.ApplicationWindow.New(application);
            window.Title = "Lunarward 登录";
            window.SetDefaultSize(1100, 760);

            WebKit6.CookieManager cookieManager = WebKit6.NetworkSession.GetDefault().GetCookieManager();
            cookieManager.SetPersistentStorage(cookieDb, WebKit6.CookiePersistentStorage.Sqlite);

            var webView = new WebKit6.WebView();
            webView.LoadUri(url);
            window.SetChild(webView);

            // 关窗即导出（用户主动取消时也把已拿到的 cookie 交回去）。
            window.OnCloseRequest += (_, _) =>
            {
                state.Export(cookieManager, quiet: false);
                application.Quit();
                return false;
            };

            // 每 2 秒看一次：登录成功就立刻落盘，不必等用户关窗。
            GLib.Functions.TimeoutAddSeconds(0, 2, () =>
            {
                if (state.ExportIfLoggedIn(cookieManager))
                {
                    window.Close();
                    return false;
                }
                return true;
            });

            // 兜底超时：到点导出当前 cookie 并退出（python 助手同语义）。
            GLib.Functions.TimeoutAddSeconds(0, (uint)Math.Max(30, timeoutSeconds), () =>
            {
                state.Export(cookieManager, quiet: true);
                application.Quit();
                return false;
            });

            window.Present();
            Console.WriteLine($"[login] 登录窗已打开：{url}");
        };

        int code = app.Run([]);
        Console.WriteLine(state.Wrote ? $"[login] 已导出 cookie → {fullOutput}" : "[login] 没有导出 cookie（未登录或已取消）");
        return code;
    }


    private static void ParseArgs(string[] argv, out string? url, out string? output, out int timeoutSeconds)
    {
        url = null;
        output = null;
        timeoutSeconds = 300;
        for (int i = 0; i + 1 < argv.Length; i++)
        {
            switch (argv[i])
            {
                case "--url": url = argv[++i]; break;
                case "--output": output = argv[++i]; break;
                case "--timeout" when int.TryParse(argv[i + 1], out int seconds): timeoutSeconds = seconds; i++; break;
            }
        }
    }


    /// <summary>一次登录会话的状态：写文件只做一次，避免关窗与轮询重复写。</summary>
    private sealed class HostState(string url, string outputFile)
    {
        public bool Wrote { get; private set; }

        public bool ExportIfLoggedIn(WebKit6.CookieManager cookieManager)
        {
            Dictionary<string, string> cookies = ReadCookiesFromStore(outputFile + ".db");
            if (!cookies.Keys.Any(name => LoginMarkers.Contains(name)))
            {
                return false;
            }
            Write(cookies);
            return true;
        }


        public void Export(WebKit6.CookieManager cookieManager, bool quiet)
        {
            Dictionary<string, string> cookies = ReadCookiesFromStore(outputFile + ".db");
            if (cookies.Count == 0 && quiet)
            {
                return;
            }
            Write(cookies);
        }


        private void Write(Dictionary<string, string> cookies)
        {
            if (Wrote)
            {
                return;
            }
            Wrote = true;
            var payload = new
            {
                Cookies = cookies,
                Time = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            };
            File.WriteAllText(outputFile, System.Text.Json.JsonSerializer.Serialize(payload));
            TryRestrict(outputFile);
            Console.WriteLine($"[login] 已导出 {cookies.Count} 条 cookie");
        }


        private static void TryRestrict(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            try
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch
            {
                // 权限收紧失败不影响登录，忽略。
            }
        }
    }


    /// <summary>
    /// 直接从 WebKit 的持久化 cookie 库读（表名/列名与 WebKit 的 SQLite 后端一致）。
    /// 之所以不用 `CookieManager.GetCookies`：GirCore 0.8.1 只生成了 `*Finish`，没有异步入口。
    /// </summary>
    internal static Dictionary<string, string> ReadCookiesFromStore(string databasePath)
    {
        var cookies = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(databasePath))
        {
            return cookies;
        }
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared,
        };
        try
        {
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name, value FROM moz_cookies";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                string name = reader.GetString(0);
                string value = reader.IsDBNull(1) ? "" : reader.GetString(1);
                cookies[name] = value;
            }
        }
        catch (SqliteException ex)
        {
            // WebKit 正在写库时可能短暂锁库：这一轮读不到就等下一轮。
            Console.Error.WriteLine($"[login] 读 cookie 库失败（下轮再试）：{ex.Message}");
        }
        return cookies;
    }
}
