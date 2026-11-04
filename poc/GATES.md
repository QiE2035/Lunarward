# M0 门禁 PoC 结果（Lunarward）

执行时间：2026-11-01 09:15–09:45（CachyOS / KDE / Wayland + XWayland / .NET SDK 10.0.302）
工程：`poc/uno-smoke/LunarwardPoc`（Uno.Templates 6.7.30，`net10.0-desktop`，Skia）
证据：`poc/screenshots/1..6-*.png`

## 结论汇总

| 门禁 | 内容 | 结果 | 依据 / 备注 |
|---|---|---|---|
| G0-1 | 无 WinAppSDK / WebView2 / Win2D / WinUI Toolkit 残留 | **PASS** | 产物 `LunarwardPoc.deps.json` 中相关包命中数 0 |
| G0-2 | 6 页渲染成功率 ≥80% | **PASS（6/6）** | 启动器 / 抽卡记录 / 截图 / 游戏设置 / 控件验收 / 设置；截图见 `poc/screenshots` |
| G0-3 | 登录（WebView → 米游社/HoYoLAB）走通 | **PASS（渲染）/ 部分（取值）** | ①独立 WebKitGTK 2.54：渲染 + cookie 落盘 + 导出 5 条；②**Uno 内置 WebView2 也能在 XAML 里渲染同一登录页**（截图 `8-webview-uno.png`），但 `CoreWebView2.CookieManager` 未实现、`ExecuteScriptAsync` 会让进程退出。见下两节 |
| G0-4 | hk4e_cn 进入主界面且父进程为 wine | **部分验证** | 已证明 `Proton - Experimental/files/bin/wine` 在既有 `~/.wine` 前缀下可直接跑 Windows 进程（`wine cmd.exe /c echo` → `hello-from-wine`，ntsync 正常）；真机进游戏待授权 |
| G0-5 | 前缀克隆 <2s | **PASS** | 同盘 btrfs reflink：650 MB / **0.168 s** / df 真实增量 **4.5 MB**（可复现脚本 `poc/prefix-reflink/clone.sh`）|
| G0-6 | 托盘 + 通知 | **PASS（KDE）** | Tmds.DBus 0.95.2：SNI 成功注册进 `org.kde.StatusNotifierWatcher`（回读列表命中本服务）；通知 `Notify` 返回 id=66。Hyprland 未测 |
| G0-7 | Segoe Fluent Icons 在 Uno Linux 正常渲染 | **部分 PASS（有坑）** | 见下节 |
| G0-9 | 上游高频 XAML 写法在 Uno 下能否编译并渲染 | **PASS（11/11）** | `CompatPage` 一页 11 项全通过；唯一编译期要求是 `x:Load` 元素必须带 `x:Name`。见下节 |
| G0-8 | `net10.0` 下 AVIF + JXL 解码 | **PASS** | `poc/codec-spike` 直接 P/Invoke 系统 `libavif.so.16`(1.4.2) / `libjxl.so.0.12`(0.12.0)：AVIF 1024x640 depth=8 平面非空；JXL 1024x640 RGBA 2621440B 已解出。见下节 |

## G0-7 图标字体：实测细节（重要）

1. 上游 `Segoe Fluent Icons.ttf` 内部家族名 = `Segoe Fluent Icons`（nameID 1/4），nameID 6 = `SegoeFluentIcons`。
2. 以 `ms-appx:///Assets/Fonts/xxx.ttf#Segoe Fluent Icons` 引用时，**无论文件是否存在、路径对错、家族名对错，渲染结果完全一致**——都是 Uno 内置的描边风格图标。
   → 结论：**Uno 会拦截 `Segoe Fluent Icons` / `Segoe MDL2 Assets` 这两个家族名**，替换为自带 Fluent 图标字体（描边风格），ms-appx 字体根本没被选中。
3. 用 fontTools 把 nameID 1/4/6/16/17 全改成唯一名（`LunarIconsTest`）后，`TextBlock` 用该 FontFamily **成功渲染出实心图标**（与 Windows 上 Segoe Fluent Icons 一致）。
   → 自定义 TTF 在 Uno Skia 上**可用**，前提是家族名唯一、不与 Uno 内置名冲突。
4. **`FontIcon` 控件会忽略 `FontFamily`**：同一字体，`<TextBlock FontFamily=...>` 渲染为实心，`<FontIcon FontFamily=...>` 仍是描边（回退到默认字体）。隐式 `<Style TargetType="FontIcon">` 也不生效。
   → 这是移植面里影响最大的单点：上游 82 个 XAML 里大量 `<FontIcon Glyph="..."/>`，不改写法就拿不到 Windows 上的观感。
   候选解法（M1 决策）：① 自定义 `FontIcon` 子类/替代控件（内部用 TextBlock 渲染）；② XAML 批量把 `FontIcon` 替换为该替代控件（脚本化，风险可控）；③ 接受 Uno 内置描边图标作为「软差异」（与 D3 的「允许局部更好」一致，但图标风格差异肉眼可见）。
5. 附带发现：App 未设 `RequestedTheme="Dark"` 时，InfoBar 等控件会用浅色主题资源（深色页面上出现亮灰底），设 `RequestedTheme="Dark"` 后正常。

## G0-5 补充：跨文件系统会退化为真拷贝

`cp -a --reflink=auto` 只在**同一支持 reflink 的文件系统**内共享数据块。本次 `/tmp` 是 tmpfs：
- 克隆到 `/tmp`：0.33 s，但实际新增磁盘占用 **385 MB**（等于整份拷贝，等于没省）。
- 克隆到 `~`（btrfs）：0.15 s，实际新增 **1 MB**。

→ 实现约束（需写进 03-runtime-model.md 的校验项）：**prefix 模板与克隆目标必须在同一卷**；跨卷时给出 `W-NO-REFKLINK` 警告并估算真实占用与耗时（386 MB 级别约 1–3 s，可接受但要提示）。

## 视觉/控件观察（待 M1 复核）

- `ProgressBar`（`Value=0.62/0.65`）在 Skia 下渲染为极细的一小段，与 WinUI 观感不符 → 需确认是否动画未完成或 Height 约束问题。
- `NavigationView` 紧凑模式、`InfoBar`、`ToggleSwitch`、`Slider`、`ProgressRing`、`Expander`、`ComboBox`、`GridView`、`ItemsControl+DataTemplate` 均正常。
- 中文字体渲染正常（未配置字体替换，系统字体回退可用）。
- 窗口 1024×640、深色主题下整体信息层次与 Starward 的 GameLauncherPage 接近。

## 环境坑（已解决，写入 README）

- `src/Starward/global.json` 要求 SDK 10.0.400，本机 10.0.302 → `poc/global.json` 单独声明，不改上游。
- `api.nuget.org` 大包在 ~1 MB 处 EOF 断开 → 用华为云镜像（`poc/NuGet.config`，`<clear/>` 后仅保留镜像源）。
- GitHub `git clone` 该分支 300 s 超时 → 改走 trees API + raw 拉取（见 `vendor/upstream/README.md`）。

## 用户决策（PoC 审阅后，2026-11-01）

| 事项 | 决策 |
|---|---|
| 图标字体（G0-7） | **接受 Uno 内置描边图标**，零改动；不替换 `FontIcon`、不改上游 XAML。记入 `00-decisions.md` D26，按 D3 归为「视觉软差异」 |
| G0-4 真机跑原神 | **暂不执行**，等用户安排时间 |
| M0 后续门禁 | G0-3（含内嵌 WebView2 复测）/ G0-6 / G0-8 已完成；G0-4 仍待用户安排真机时间 |

→ 当前 PoC 停留在「可运行、六页可渲染、演示效果已交付」状态，未做任何 `src/` 改动，未提交 git。

## G0-3b Uno 内嵌 WebView2 实测（Uno 6.7.30 · Linux Skia · WebKitGTK 2.54）

工程改动：`LunarwardPoc.csproj` 加 `UnoFeatures=WebView`，`MainPage.xaml` 加 `<WebView2 x:Name="Probe" Source="https://user.miyoushe.com/" />`（`--page=webview` 显示）。运行环境必须 `GDK_BACKEND=x11`（Uno 文档明确 Wayland 需回退 X11）；本机另需 `WEBKIT_DISABLE_DMABUF_RENDERER=1`，否则日志刷 `Failed to create GBM buffer of size 1280x800` 且窗口黑屏。

**结论：渲染与导航可用，取值类 API 大多未实现。**

| 能力 | 结果 |
|---|---|
| `WebView2` 控件 + `Source` 渲染真实登录页 | **OK**（截图 `screenshots/8-webview-uno.png`，与独立 WebKitGTK 一致） |
| `EnsureCoreWebView2Async()` | **OK** |
| `CoreWebView2.DocumentTitle` | OK（返回空串，不抛） |
| `CoreWebView2.AddWebResourceRequestedFilter(...)` | OK（不抛，是否真拦截未验证） |
| `CoreWebView2.Navigate(uri)` | OK（不抛） |
| `CoreWebView2.CookieManager` | **NotImplementedException** |
| `CoreWebView2.Settings.*`（如 `IsScriptEnabled`） | **NotImplementedException** |
| `CoreWebView2.Profile` | **NotImplementedException** |
| `ExecuteScriptAsync("1+1")` | **进程直接退出**（无异常、无日志，`pgrep` 归零）—— 比未实现更糟，属崩溃 |

含义（直接影响 M1 登录方案）：
1. **不需要自建 GTK 宿主**也能把网页嵌进 Uno 界面 —— R-02 的"嵌入"部分被证伪为低风险。
2. 但 Starward 登录的核心是**登录后从 WebView 取 cookie** 交给 `Starward.Core`；Uno 的 `CookieManager` 与 `ExecuteScriptAsync` 都不可用 → 内嵌 WebView 拿不到 cookie。
3. 可行路线（M1 需拍板，见 Q-039）：① 独立 WebKitGTK 登录窗口（已证可用，cookie 落 SQLite，登录完回传）+ 内嵌 WebView 只做展示（公告/BBS）；② 不走 WebView，直接调米哈游 passport 的扫码/密码 API 换 `ltoken`；③ 给 Uno 提 PR 实现 `CookieManager`（Uno 是开源项目，但排期不受我们控制）。

**复现**：`GDK_BACKEND=x11 WEBKIT_DISABLE_DMABUF_RENDERER=1 DISPLAY=:0 ./LunarwardPoc --page=webview`；加 `--js-probe` 可复现崩溃。

## G0-3 登录尖峰：边界与结论

工程：`poc/login-spike/login.py`（Python + PyGObject + WebKitGTK 2.54 + GTK3），截图 `poc/screenshots/7-login-webkit.png`。

1. **渲染通过**：`https://user.miyoushe.com/` 正常加载，页面弹出「米哈游通行证」短信/密码登录框，样式与 Windows 版 WebView2 中一致。
2. **cookie 落盘**：`WebKit2.WebContext.get_default().get_cookie_manager().set_persistent_storage(...)` 指定 SQLite 路径后，`moz_cookies` 表持久化 4 行；跨进程重启仍在。
3. **cookie 可导出**：`get_cookies(uri)` 回调拿到 5 条（`DEVICEFP`、`DEVICEFP_SEED_ID`、`DEVICEFP_SEED_TIME`、`_MHYUUID`、`MIHOYO_LOGIN_PLATFORM_LIFECYCLE_ID`），已写 `cookies.json`，可直接喂给 `Starward.Core` 的 Hyperion/HoYoLAB 客户端。
4. **未做**：真实账号登录（无凭据，也不该由自动化代填）；Uno Skia 桌面端**内嵌** WebView（本次是独立 GTK 窗口）。M1 需定嵌入方案：GtkSharp/GirCore 原生宿主 or 独立 WebKit 进程 + IPC（对应 `06-risks.md` R-02）。

## G0-6 托盘/通知尖峰：边界与结论

工程：`poc/tray-spike`（Tmds.DBus 0.95.2）。

1. 导出 `org.kde.StatusNotifierItem`（`/StatusNotifierItem`）并注册 `org.kde.StatusNotifierItem-<pid>-1`，watcher 回读 6 条中命中本服务 → **KDE 托盘可见**。
2. `org.freedesktop.Notifications.Notify` 返回 id=66 → **通知通路可用**（测试通知正常弹出）。
3. 边界：用主题图标 `IconName=applications-games`；自定义图标需要 `IconPixmap`（`(iiay)` ARGB 数组），M2 补。Hyprland 无会话可测，记为未验证。
4. **依赖安全**：`Tmds.DBus 0.21.2` 命中高危公告 GHSA-xrw6-gwf8-vvr9，已升级 0.95.2；`05-dependencies.md` 同步记录。

## G0-9 XAML 兼容性探针：11 项实测

工程：`poc/uno-smoke/LunarwardPoc/CompatPage.xaml`（`--page=compat`，可加 `--scroll=430`、`--dialog`）。

| # | 构造 | 结果 |
|---|---|---|
| ① | `{x:Bind Describe(Count)}` 函数绑定 | 渲染正确 |
| ② | `x:Bind` + `IValueConverter` | 渲染正确 |
| ③ | `Mode=TwoWay` + `UpdateSourceTrigger=PropertyChanged` | 编译通过 |
| ④ | `x:Load`（需 `x:Name`，否则 `UXAML0001`） | 正确显隐 |
| ⑤ | `AcrylicBrush`（TintOpacity 0.6） | 半透明渲染 |
| ⑥ | `ThemeShadow` + `Translation="0,0,32"` | 阴影偏淡 |
| ⑦ | `ItemsRepeater` + `StackLayout` | 正确渲染 3 条 |
| ⑧ | `PipsPager NumberOfPages=5` | 5 个 pip |
| ⑨ | `InfoBar` | 深色配色正确 |
| ⑩ | `ContentDialog` + `XamlRoot` | 弹层正常 |
| ⑪ | `TeachingTip` | 可构造 |

截图：`screenshots/9-compat.png`、`10-compat-2.png`、`11-compat-3.png`、`12-dialog.png`。
统计面（82 个 XAML）：`x:Bind` 72、`FontIcon` 55、`AcrylicBrush` 39、`ThemeShadow` 23、`ItemsRepeater` 14、`ContentDialog` 10、`x:Load` 8、`WebView2` 7、`PipsPager` 2、`AnimatedIcon` 3、`MicaBackdrop` 1。详见 `docs/linux-port/10-xaml-compat.md`。

## G0-8 解码尖峰：细节

工程：`poc/codec-spike`（纯 `net10.0`，无 NuGet 依赖，`DllImport` 直连）。

1. AVIF：`avifImageCreateEmpty` + `avifDecoderReadFile`，从 `avifImage` 前部读 `width@0 / height@4 / depth@8`、`yuvPlanes[0]@24`（布局取自 `/usr/include/avif/avif.h:777`）→ 1024x640、depth 8、Y/U 平面非空。
2. JXL：`JxlDecoderCreate` → `SubscribeEvents(BASIC_INFO|FULL_IMAGE)` → `SetInput`+`CloseInput` → `ProcessInput` 循环；`GetBasicInfo` 读 `xsize@4 / ysize@8`；`ImageOutBufferSize`+`SetImageOutBuffer` 解出 RGBA8 2621440 字节，非零字节 2621128。
3. 样本由 `avifenc -q 60` / `cjxl -q 80` 从 PoC 截图生成，随工程提交（17.5 KB / 28.9 KB）。
4. **结论**：D21 修订中「`avif.dll` → `libavif.so.16`」的映射假设成立；本机 libavif 1.4.2 / libjxl 0.12.0 与上游封装的 1.3.0 / 0.11.1 同代，API 兼容，`SetDllImportResolver` 方案可行。

## G0-5 复现脚本

`poc/prefix-reflink/clone.sh [源前缀] [目标前缀]`，默认 `~/.wine` → `~/.local/share/lunarward-poc/clone-test`。

- 耗时 **0.168 s**（650 MB，btrfs 同卷）
- `du` 会显示 654 MB（reflink 副本仍按块统计），**真实占用要看 `df` 增量：4.5 MB**
- 跨文件系统（如 `/tmp` 是 tmpfs）会退化为真拷贝，耗时更长且吃满空间 —— 产品里必须校验「prefix 模板与实例同卷」，否则提示用户

## Uno 控件缺口清单（M1 输入）

| 控件 / 能力 | 现象 | 处理建议 |
|---|---|---|
| `FontIcon` + `FontFamily` | 忽略 `FontFamily`，强制用默认字体（描边） | D26：替代控件 + 脚本替换 `FontIcon` |
| 隐式 `<Style TargetType="FontIcon">` | 不生效 | 同上，避免依赖隐式样式 |
| `FontFamily` 名 `Segoe Fluent Icons` / `Segoe MDL2 Assets` | 被 Uno 拦截替换为内置字体 | 字体改唯一族名后随包分发 |
| `ProgressBar` | 细条渲染（Value=0.62 只显示一小段） | M1 复核模板/高度，必要时自定义 `ControlTemplate` |
| `InfoBar` 等主题资源 | 未设 `RequestedTheme="Dark"` 时用浅色资源 | 应用根统一设 `RequestedTheme="Dark"` |
| `WebView2` | 有内置实现（`UnoFeatures=WebView`），渲染 OK；cookie/JS/settings 未实现 | G0-3b 已实测；登录取值需绕行（Q-039） |
| 托盘 | 无内置 | G0-6 已验证 Tmds.DBus SNI；需自建 `IconPixmap` 支持 |
