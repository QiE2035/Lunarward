# Lunarward PoC（M0 门禁验证）

本目录只用于**验证可行性**，不属于产品代码；`src/` 未做任何改动。

## 目录

```
poc/
├── global.json          # 独立固定 SDK 版本，避免与 src/Starward/global.json(10.0.400) 冲突
├── NuGet.config         # 仅华为云镜像（api.nuget.org 在本机会大包断流）
├── uno-smoke/           # Uno.Templates 生成的最小工程（Skia 桌面头）
│   └── LunarwardPoc/
│       ├── App.xaml(.cs)        # 主题、图标字体、全局资源
│       ├── MainPage.xaml(.cs)   # 六页原型：启动器/抽卡/截图/游戏设置/控件验收/设置
│       └── Assets/Fonts/        # 上游 Segoe Fluent Icons + 改名副本 lunar-icons.ttf
├── codec-spike/         # G0-8：net10.0 直连系统 libavif/libjxl 解码 AVIF+JXL
├── tray-spike/          # G0-6：StatusNotifierItem 托盘 + org.freedesktop.Notifications
├── login-spike/         # G0-3：WebKitGTK 承载米游社登录页 + cookie 落盘/导出
├── screenshots/         # 页面实机截图（1024x640）+ 登录页全屏截图
└── GATES.md             # 门禁结果与实测结论
```

## 运行

```bash
cd poc/uno-smoke
dotnet build -c Release
DISPLAY=:0 ./LunarwardPoc/bin/Release/net10.0-desktop/LunarwardPoc
# PoC 专用：直接打开指定页（便于逐页截图，不进产品）
DISPLAY=:0 ./LunarwardPoc/bin/Release/net10.0-desktop/LunarwardPoc --page=gacha
```

`--page=` 取值：`launcher` `gacha` `shot` `gsetting` `control` `setting` `webview` `compat`。

`compat` 页是 XAML 兼容性探针（上游高频写法 11 项），另支持 `--scroll=<像素>` 与 `--dialog`；结论见 `GATES.md` G0-9。

`webview` 页验证 Uno 内置 WebView2（Linux = WebKitGTK）。运行需 `GDK_BACKEND=x11`，本机还需 `WEBKIT_DISABLE_DMABUF_RENDERER=1`：

```bash
GDK_BACKEND=x11 WEBKIT_DISABLE_DMABUF_RENDERER=1 DISPLAY=:0 ./LunarwardPoc --page=webview
```
加 `--js-probe` 可复现 `ExecuteScriptAsync` 导致的进程退出（见 `GATES.md` G0-3b）。

## 已知 PoC-only 设定（不会带进产品）

- 页面用硬编码假数据（游戏名、抽卡记录、截图占位块）。
- 图标字体用改名副本 `lunar-icons.ttf`（原因见 `GATES.md` G0-7）。
- `MainPage` 用可见性切换模拟导航（产品里是 `NavigationView` + 真实页面）。

## 其余三个尖峰工程

```bash
# G0-8 解码（无 NuGet 依赖，直连系统库）
cd poc/codec-spike && dotnet run -c Release -- .
#   → AVIF 1024x640 / JXL 1024x640 RGBA，末行打印 G0-8: PASS

# G0-6 托盘 + 通知（Tmds.DBus 0.95.2）
cd poc/tray-spike && dotnet run -c Release
#   → 打印 watcher 回读列表与通知 id，末行打印 G0-6(KDE): PASS
#   注意：必须在图形会话内运行（DBUS_SESSION_BUS_ADDRESS 已设置）

# G0-3 登录页（Python + PyGObject + WebKitGTK）
cd poc/login-spike && KEEP_MS=6000 python3 login.py
#   可选参数：URL（默认 https://user.miyoushe.com/）
#   产物：cookies.db（SQLite）、cookies.json（导出列表）
```

三个尖峰都**不参与产品构建**，不引用 `src/` 任何代码；结论已汇总进 `GATES.md`。
