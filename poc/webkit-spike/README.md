# webkit-spike（M5 前置验证，2026-11-14）

目的：验证能不能用 **GirCore（Gtk-4.0 / WebKit-6.0）在本机进程内跑 WebKitGTK 6.0**，
为「登录窗从 python3 + WebKitGTK 4.1 助手换成 C# 进程内/进程内助手」提供基线。

## 结论（实测）

- `extra/webkitgtk-6.0 2.54.2-1` 可用，提供 `libwebkitgtk-6.0.so.4` / `libjavascriptcoregtk-6.0.so.1`。
- `GirCore.Gtk-4.0 0.8.1` + `GirCore.WebKit-6.0 0.8.1` 在 `net10.0` 上可直接用，`Gtk.Application` →
  `ApplicationWindow` → `WebKit.WebView` 全链路正常，`WebKit.Functions.GetMajorVersion()` = `2.54.2`。
- **必须给 native 库做名字映射**：GirCore 的 `DllImport` 名是 `WebKit` / `JavaScriptCore`，
  glibc 会去找 `libWebKit.so`。做法：在输出目录 `ln -sf /usr/lib/libwebkitgtk-6.0.so.4 libWebKit.so`
  （`JavaScriptCore` 同理），或者自己注册 `DllImportResolver`。与 L-57 的 libzstd 是同一类坑。
- 实测窗口截图：`about:blank` + `LoadHtml` 渲染正常，UA `AppleWebKit/605.1.15 (KHTML, like Gecko) Version/60.5 Safari/605.1.15`，
  `cookieEnabled: true`，`webView.GetNetworkSession()` 非空。

## API 备忘（GirCore 0.8.1）

- 构造函数都是 `ConstructArgument[]`，要用静态工厂：`Gtk.Application.New(appId, flags)`、
  `Gtk.ApplicationWindow.New(application)`；`Gio.ApplicationFlags` 没有 `None`，传 `0`。
- 全局函数在 `GLib.Functions` / `WebKit.Functions`（如 `TimeoutAddSeconds`、`GetMajorVersion`）。
- `WebView` 上没有 `UserAgent` 属性（0.8.1），UA 从 JS 侧读或走 `NetworkSession`。

## 未验证

- **与 Uno 的 X11 主机同进程共存**（两套 UI 工具包一个进程）：本轮只验证了独立进程。
- WebKitGTK 6.0 在 Wayland 下的合成路径（当前 Gtk4 默认走 Wayland，Uno 走 X11）。

## B 方案（同进程）验证 —— 2026-11-14

在 `poc/uno-smoke`（真 Uno 应用）里加 `GtkWebProbe.cs`，用 `--gtkweb` 触发，同一进程再开一个 Gtk4 + WebKit 窗口。

### 跑通的部分

- **同进程两个窗口都在**：`cu_windows` 显示 pid 相同（802061）的两个 toplevel ——
  `io.lunarward.gtkwebprobe 760×588 @ (388,118)` 与 `com.companyname.lunarwardpoc 819×540 @ (358,142)`，
  Uno 主循环继续跑，日志里 0 个 `Unhandled/FATAL/SIGSEGV`。
- WebView 真的渲染：GTK 窗口里画出了 `https://example.com` 的完整页面。
- `NetworkSession.GetCookieManager()` + `CookieManager` 在绑定里齐全，cookie 不用再读 SQLite。

### 必须满足的条件

1. `GDK_BACKEND=x11`（不设时 Gtk 走 Wayland，窗口在截图里是空的，且与 Uno 的 X11 宿主不共享焦点模型）。
2. `DllImportResolver` 把 `WebKit` → `/usr/lib/libwebkitgtk-6.0.so.4`、`JavaScriptCore` → `/usr/lib/libjavascriptcoregtk-6.0.so.1`。
3. **Uno 自己带 GtkSharp 3.24（GTK3）**，与 GirCore 的 GTK4 类型同名冲突（`Application`/`WebView`），
   必须给包加 `Aliases="gir"` + `extern alias gir;` 才能编译。也就是**一个进程里同时存在 GTK3 和 GTK4**。

### 没解决的两点（B 的硬伤）

- **窗口 chrome 不受我们控制**：GTK 窗口走 KWin 的服务端装饰，标题栏颜色跟 KDE 配色（本机浅色）；
  `GTK_THEME=Breeze-Dark` 只影响窗口**内部**，标题栏依旧浅色 → 要统一观感只能自己做 CSD 标题栏（`SetTitlebar` + 深色 CSS）。
- **模态/父子只能靠 X 属性硬设**：GTK4 没有「对到外部窗口 transient」的 API，只能手工写
  `WM_TRANSIENT_FOR`（实测 `xprop` 写入成功：`WM_TRANSIENT_FOR(CARDINAL) = 18874370`），
  但 KWin 是否稳定按子窗口处理（最小化父窗联动、永远在父窗之上）**未能确定性验证**。
