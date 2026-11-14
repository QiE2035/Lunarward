# 12 · Linux 替身、缺口与状态登记表

> 这份文档回答一个问题：**Linux 版里，哪些地方是「真的做了」，哪些只是「先让它编译过」？**
>
> **最后校准**：2026-11-13（D70 + L-125）。UI 层的逐页复核与缺陷记录在 `21-ui-audit.md` / `22-port-verification.md`；本文只回答「实现了没有、还是只是替身」。
>
> 维护规则（强制）：每新增一个 `*.Linux.cs`、每新增一处 `Compat/` 兼容层、每把一个上游文件加入
> `<Compile Remove>`，都必须同步更新本表。没有登记的替身视为缺陷（D33）。
>
> **本表是唯一登记处。** 其它文档只描述机制，不描述状态。

- 最后更新：2026-11-13（视频看门狗阈值、背景记账、catch 收口）
- 对应代码：`src/Lunarward/`（`linux-port` 分支，76 提交）
- 规模：33 个 `*.Linux.cs`、8 个 `*.LinuxAuto.cs`、6 个 `Compat/` 兼容层、55 条 `<Compile Remove>`

---

## 1 · 状态图例

| 标记 | 含义 | 允许上线的阶段 |
|---|---|---|
| ✅ | 真的实现了，功能等价或可接受替代 | 任意阶段 |
| 🟡 | 能用，但有明确缺口（写在该行） | 满足「缺口不影响主流程」时可上线 |
| ⬜ | 占位：能编译、不报错、**运行时不工作** | 只有显式标注「M1 允许带占位交付」的里程碑 |
| ⛔ | 决定不做（有书面理由） | 不做 |

---

## 2 · 总览（按功能域）

先看这张表。后面各节是明细。

| 功能域 | 状态 | 一句话说明 | 补齐时机 |
|---|---|---|---|
| 启动游戏 | ✅ | `exec wine <exe> <args>`，不套 shell，保住「游戏是 wine 首个进程」 | — |
| 运行环境（Profile） | 🟡 | 现状：运行器带布局与可用性（D58 v2）、启动成功记「上次可用」、前缀五种来源；待办：D63 三段式重排（运行器/方案/前缀）与方案独立编辑页 | D63 |
| 前缀生命周期 | ✅ | external / dedicated / shared / template / ephemeral 五模式 | — |
| 组件管理 | ✅ | 扫描系统 / Steam / 自带组件；DXVK·VKD3D 导入并装入前缀；扫描阶段零副作用（不建前缀）；`--list-runners [--json]` | 下载渠道 ⛔ 设计选择（D58 v2 不做市场） |
| 启动前预检 + 启动报告 | ✅ | 6 项检查（只读，不建前缀）、失败弹窗、JSON 报告、前缀互斥锁 | — |
| 后启动程序（Companion） | ✅ | 条目 CRUD、三种触发时机、伴随启动、`--companion-plan/run`；`WINEPREFIX` 按游戏 key + 前缀模式解析（不再只认 `PrefixPath`） | 注入型只编排不实现（D24） |
| 输出与诊断 | 🟡 | 输出捕获/落盘/脱敏、面板、「运行输出」对话框；**只有沉默/面板两级**（`terminal` 级按 D37 降级为 M4，`RuntimeProfile.GameOutput` 目前是未消费字段） | M4（terminal 级） |
| 游玩时长 | ✅ | `wineserver -w` 判定会话结束，进程早退时回退最近 Start 事件；30s 心跳写 `Interruption=true`，启动器被杀也能留下时长 | — |
| 启动器主界面 | ✅ | 1200×676 DIP、**系统标题栏**（交给窗口管理器，L-73）、真实公告/活动 | — |
| 外部链接 | ✅ | `Helpers/OpenUrl.cs`：`xdg-open` → `gio open`，替换 8 类 `Launcher.LaunchUriAsync` 调用（后者在 Linux 静默失败，L-74） | `AppBackground` 里 3 处 apps.microsoft.com 保留（Windows 专属提示） |
| 背景切换 / 视频壁纸 | ✅ | 圆点切换器由代码生成（Uno PipsPager 不响应点击，L-69）；视频走 libvlc `--vout=vmem` + Skia 合成（`Video.Url`，非海报）；隐藏/锁屏暂停、唤回恢复（D48/L-86）；静态背景底图 + `Theme` 文字层合成（D48/L-87）；暂停静态图预加载（D49）；CPU 1.66 → ~0.95 核；强调色按底图/海报取色（D50） | 无 vlc 或无视频文件时退回静态兜底图；`PropertiesChanged` 不需要（D51） |
| 背景图库窗口 | ✅ | `BackgroundViewWindow` code-behind 重新纳入编译；缩略图 `ListView + tk:WrapPanel`；本地绝对路径由 `CachedImage.Linux` 读字节解码；窗口交还系统标题栏并按 `DefaultWindowSizeInDips` 设 1200×676、夹到 90% 屏幕内（D49） | 无 |
| 单实例 / 陈旧锁 | ✅ | 文件锁 + UNIX socket；无人接时判定陈旧锁并接管；无 `DISPLAY`/`WAYLAND_DISPLAY` 明确报错退出（L-72）；`--new-instance` 跳过检查 | 无 |
| 设置页 10 项 | ✅ | 上游 9 项 + 「运行环境」 | — |
| 登录 / 账号 | 🟡 | WebKitGTK 独立窗取 cookie；**未真实登录验证** | 待用户 |
| 抽卡记录 | 🟡 | 页面与导出可用；真实数据未验证 | 待用户 |
| 截图 | ✅ | Alt+D 捕获、图库、多选、批量转 JPG、查看器 | HDR 色彩管理 ⬜ M4 |
| 壁纸 | ✅ | 静态图 + 视频（LibVLC，缺库自动回退静态） | — |
| 托盘 | ✅ | StatusNotifierItem 注册、`Properties.GetAll`、DBusMenu 菜单（用户已确认右键可用）、点击唤回、托盘不可用时关闭=退出（D48/D50、L-85/L-91） | 托盘属性不发 `PropertiesChanged` |
| 全局热键 | 🟡 | X11 轮询（25ms）；唤回走进程内 libxcb（D47，不再依赖 xdotool）；Wayland 会话下给「绑定 `lunarward --show`」提示（D51） | 平台限制：纯 Wayland 无全局热键标准 |
| URL 协议 / 单实例 | ✅ | `lunarward://` + socket 转发（show/exit/startgame） | — |
| 自更新 | ✅ | 查 AUR RPC 版本并报告；不下载替换、网络失败当无更新（D5/D33；D51 定论） | — |
| 游戏安装 / 预下载 / 增量更新 / 修复 | 🟡 | 真实流程（`GameInstallService.Linux.cs`）：Waiting→Downloading→Finish/Error，Pause/Continue/Stop/Uninstall，进度写入 `GameInstallContext`（含 `Progress_Write*`，见 L-65）。增量 = 补丁 + chunk 复用组合编排（`UpdateOrchestrator`，实测 7.1.0→7.2.0 估算 23.81 GB / 省 47%，差分 101 文件全应用且逐文件 MD5 一致）；块级断点续传（`ChunkResume`，临时文件长度=块进度）；修复按**本地版本**清单逐文件校验（D42，实测破坏一个 dll 后被修回、版本不变）。CLI：`--install-plan/-fetch/-run`、`--patch-plan/-run`、`--install-chunk-plan/-run`、`--update-plan/-run`、`--version-status/-write`，Debug 另有 `--dev-install-probe`。**已知偏差**：官方 `stats` 8.34 GB 只是 101 个补丁文件体积，我们 23.81 GB = 补丁 + 其余 1110 文件块下载，语义不同非算错（D51）；补丁阶段无块内续传（`Hi3Helper.Sophon` 无 Range API，D51 接受）；修复要全量读 140 GB（约 210 s） | — |
| 悬浮窗（时钟/帧率） | ⬜ | `OpenOverlayWindow()` 恒 false | M4 |
| 手柄控制 | ⬜ | 空实现 | M4 |
| RPC 服务 | ⬜ | 恒「未运行」 | M4 |
| 云游戏 / 战绩 / BBS 桥 | ⬜ | 占位 | M4 |
| 打包分发 | ✅ / 🟡 | `PKGBUILD` 出包并跑通；AppImage 脚本就绪缺 `appimagetool` | — |
| i18n | ✅ | 11 个语言文件与 `Lang.resx` 的 965 键完全一致（86 条 Lunarward 文案已译 de/es/it/ru/th/vi）；语言设置现真正生效（D50/L-90） | CLI 输出固定中文（诊断接口，D51 定论） |

---

## 3 · Linux 实现文件（`*.Linux.cs`，33 个）

| 文件 | 替代的 Windows 机制 | 状态 | 里程碑 | 说明 |
|---|---|---|---|---|
| `Program.Linux.cs` | WinUI 生成入口 | ✅ | M1 | Uno `UnoPlatformHostBuilder`；CLI 子命令（见 §8） |
| `Frameworks/WindowEx.Linux.cs` | `WindowEx` 标题栏/PInvoke | ✅ | M1 | 基于 `Window.AppWindow`；`CenterInScreen` 先 `Resize` 再 `MoveAndResize`（后者在 X11 空操作）；`SetDragRectangles` 空实现 → Windows 上「自绘标题栏」方案在 Linux 不可用，改用系统标题栏。`Hide()/Show()/ShowFromMinimized()` 走 `Helpers/X11Window`（libxcb unmap/map + `_NET_ACTIVE_WINDOW`），因为 Uno X11 既没有 `HideCore`、`Activate()` 也只发 `XRaiseWindow`（D47、L-84）；`Show()` 统一关掉 `ExtendsContentIntoTitleBar` 并按 `DefaultWindowSizeInDips` + 屏幕钳制摆位（D49） |
| `Controls/CachedImage.Linux.cs` | `Scighost.WinUI.CachedImage` | 🟡 | M1 | ms-appx/网络/本地（本地读字节 + `SetSourceAsync`）；`CornerRadius` 为矩形裁剪 |
| `Helpers/FileDialogHelper.Linux.cs` | `FileOpenPicker`/`FileSavePicker` | 🟡 | M1 | kdialog → zenity；未接 portal，沙箱内不可用 |
| `Helpers/ClipboardHelper.Linux.cs` | WinRT Clipboard | 🟡 | M1 | wl-copy → xclip → xsel |
| `Helpers/DriveHelper.Linux.cs` | `DriveInfo`/`GetDriveType` | ✅ | M1 | 解析 `/proc/mounts` |
| `Helpers/HotkeyManager.Linux.cs` | `RegisterHotKey` + 消息循环 | 🟡 | M2 | `XQueryKeymap` 25ms 轮询；Alt+S 唤回、Alt+D 截图；唤回走 `Helpers/X11Window.Show`（原先按标题 `"Starward"` 查找、而实际标题是 `Lunarward`，唤回永远失败，已修） |
| `Helpers/X11KeyState.cs` | 无 | 🟡 | M2 | 只做只读查询与 keysym→keycode；窗口操作走子进程，本进程不碰 Xlib 分配/错误处理器（L-45） |
| `Helpers/XamlRootExtension.Linux.cs` | `GetWindowHandle`/拖动区 | 🟡 | M1 | 句柄恒 0；`GetAppWindow()` 经 `ContentIslandEnvironment` 反查并兜底 `MainWindow.Current`（L-31） |
| `Helpers/SystemBackdropHelper.Linux.cs` | Mica/Acrylic | 🟡 | M3 | 半透明画刷近似（`#D01C1C20`/`#A8101012`），无真模糊 |
| `Helpers/WindowWaitAsync.Linux.cs` | 窗口句柄等待 | ✅ | M1 | 改用窗口生命周期事件 |
| `Properties/AssemblyVersion.cs` | MSBuild 生成版本特性 | ✅ | M2 | Uno.Sdk 不生成版本特性；配合 `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`（L-50） |
| `Features/Background/AppBackground.Linux.cs` | Win2D + MediaPlayer 动态壁纸 | ✅ | M3 | 静态图（`#16161A` + `Image Opacity=0.42`）与视频（`VideoBackgroundPlayer`，LibVLC 为 Uno 传递依赖）双路；无 libvlc 回退静态（L-41/L-43/L-52） |
| `Features/Codec/ImageLoader.Linux.cs` | Win2D `CanvasBitmap` | ✅ | M1 | SkiaSharp `SKCodec` 取尺寸；AVIF/JXL 解码未接（M4） |
| `Features/Overlay/RunningGameService.Linux.cs` | `SetWinEventHook` | 🟡 | M1 | `Process.HasExited` 判定存活，不感知窗口焦点；`OpenOverlayWindow()` 恒 false |
| `Features/RPC/RpcService.Linux.cs` | 命名管道 + gRPC 子进程 | ⬜ | M4 | 恒「未运行」；`CreateRpcClient` 抛 NotSupported（D6） |
| `Features/GameInstall/GameInstallShims.cs` | `Starward.RPC.GameInstall` 契约 | ✅ | M3 | DTO/枚举按上游真实代码复制；`GameInstallService.Linux.cs` 提供 Linux 实现，`Start*Async` 返回真实任务上下文 |
| `Features/GameInstall/GameInstallScanner.Linux.cs` | 注册表枚举已装游戏 | ✅ | M1 | 扫 `~/games`、Steam `common`、`$LUNARWARD_GAME_ROOTS`，`NameMatches` 防国服/B 服同名 exe 误认 |
| `Features/GameInstall/InstallGameDialog.Linux.cs` | 安装对话框 code-behind | ✅ | M3 | 只保留 `x:Bind` 成员；安装/预下载/更新/修复都从真实服务起任务 |
| `Features/GameInstall/PreDownloadDialog.Linux.cs` | 预下载对话框（体积/空间） | ✅ | 体积取 `getPatchBuild` 的 stats[本地版本]（增量），无预下载时显示提示并禁用开始；可用空间读安装分区 | M3 |
| `Features/GameLauncher/GameLauncherService.Linux.cs` | `ProcessStartInfo{Verb="runas"}` + `cmd /c start` | ✅ | M1–M2 | 直 `exec wine`；补 Proton 的 `WINEDLLPATH`/`LD_LIBRARY_PATH`/`PROTONPATH`（L-44）；前缀锁、报告、退出钩子都在这里 |
| `Features/GameLauncher/GameNoticeWindow.Linux.cs` | WebView2 正文 + JS 注入 | 🟡 | M2 | 内嵌 Uno `WebView2`；JS 注入全部删除；正文需带 authkey 的真机 URL 验证 |
| `Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs` | Win2D HDR 预览 | 🟡 | M3 | 结构/绑定/持久化保留，预览区无画面 |
| `Features/GamepadControl/GamepadController.Linux.cs` | `SharpGameInput` + `InputSimulator` | ⬜ | M4 | 整体未实现 |
| `Features/GameRecord/LoginPage.Linux.cs` | WebView2 内嵌登录 | 🟡 | M1–M2 | 独立 WebKitGTK 窗取 cookie（D29/D32），`python3` + `python-gobject` |
| `Features/GameRecord/BBSWebBridge.Linux.cs` | WebView2 脚本桥 | ⬜ | M4 | 仅有 `GameBiz`/`IsHoyolab`/`IsLoggedIn` |
| `Features/GameRecord/BattleChronicleWindow.Linux.cs` | 战绩窗口 | ⬜ | M4 | 占位 |
| `Features/Screenshot/ScreenCapture.Linux.cs` | `Windows.Graphics.Capture` + DXGI | ✅ | M3 | `spectacle` → `grim` → `gnome-screenshot` 降级链；默认 `~/图片/Lunarward` |
| `Features/Screenshot/ScreenshotPage.Linux.cs` | DXGI 图库 | ✅ | M3 | 扁平 `ItemsControl` + `WrapPanel`（L-48）；多选、批量转 JPG、打开查看器 |
| `Features/Screenshot/ScreenshotSetting.Linux.cs` | DirectX 色彩管理设置 | 🟡 | M3 | 结构保留，HDR 相关项无实际作用 |
| `Features/Screenshot/ImageViewWindow.Linux.cs` | Win2D 看图窗 | 🟡 | M3 | 窗口壳；实际看图走 `ScreenshotViewerWindow` |
| `Features/Screenshot/ImageConvertItem.Linux.cs` | Win2D 编码 | 🟡 | M3 | 批量转换走 `ScreenshotConverter`（SkiaSharp，JPEG 92） |
| `Features/Update/UpdateService.Linux.cs` | GitHub Release | 🟡 | M2 | 查 AUR RPC 版本；网络失败按「无更新」 |
| `Features/Update/UpdateWindow.Linux.cs` | Windows 自更新 UI | 🟡 | M2 | 检查/忽略/提醒可用；「立即更新」提示走包管理器（D5） |
| `Features/Update/SetupService.Linux.cs` | Windows 安装器 | ⛔ | — | Linux 不做（D5） |
| `Features/UrlProtocol/UrlProtocolService.Linux.cs` | 注册表注册 `starward://` | ✅ | M2 | 写 `~/.local/share/applications/lunarward.desktop` + `update-desktop-database` + `xdg-mime` |

> `Features/GamepadControl/*`、`Features/Overlay/*`、`Features/RPC/*` 的 Windows 原版在 §6 中排除；
> 这些子系统的 Linux 方案尚未定型，M4 再决定「真做还是不做」。

---

## 4 · 名字兼容层（`Compat/`，6 个）

原则（D30）：**补名字可以，补行为不行**。只在上游大面积使用、替换成本远超收益时开。

| 文件 | 覆盖的类型 | 状态 | 说明 |
|---|---|---|---|
| `Compat/VanaraHotkeyCompat.cs` | `Win32Error` / `HotKeyModifiers` / `VK` | 🟡 | 只补常量与 `Succeeded/Failed`；真正注册在 `HotkeyManager.Linux.cs` |
| 桌面通知 | ✅ | `Helpers/DesktopNotification.cs`（`notify-send` → `gdbus Notify` 兜底）；安装/预下载/修复/更新、截图保存各发一条（D50） | 无通知服务时静默跳过 |
| 强调色 | 🟡 | 按背景/海报取主色写主题字典并刷新（D50/L-89） | Windows 上还染标题栏/边框（DWM），Linux 交给 WM（D47） |
| `Compat/NotifyIcon/TaskbarIconCompat.cs` | `H.NotifyIcon.TaskbarIcon` | ⬜ | 占位；真实托盘是 `Features/Tray/StatusNotifierTray.cs` |
| `Controls/ImageExCompat.cs` | `Scighost.WinUI.ImageEx.ImageEx` | 🟡 | 写成 `Control`（上游有 ControlTemplate），按状态切 `Loading/Loaded/Failed/Unloaded` |
| `Controls/Win2DCompat.cs` | `CanvasSwapChainPanel` | ⬜ | 空 `Panel`，不画任何内容 |
| `Controls/ToolkitControls.cs` | `Segmented`/`WrapPanel` | 🟡 | `Segmented` 用横向 ListView；`WrapPanel` 自写 Measure/Arrange；`WrapLayout` 空实现（L-23） |
| `Controls/ToolkitCompat.cs` + `InteractivityCompat.cs` | toolkit 附加属性 / `Microsoft.Xaml.Behaviors` | 🟡 | `Interaction.GetBehaviors` 返回会 `Attach` 的集合（L-33）；`Effects.Shadow` 注册为 `object`（L-28） |

---

## 5 · 新增模块（不是替身，是 Linux 特有）

| 模块 | 作用 |
|---|---|
| `Features/Runtime/RuntimeProfile*.cs` | Profile 模型、存储（`~/.config/lunarward/profiles/*.json`）、`ProfileBinding`（游戏→Profile） |
| `Features/Runtime/ComponentStore.cs` | 扫描运行器/图形组件（目录链符号链接逐段解析，L-38） |
| `Features/Runtime/GraphicsInstaller.cs` | 把 DXVK/VKD3D dll 装进前缀，写 `.lunarward/graphics.json` |
| `Features/Runtime/PrefixManager.cs` | 五种前缀模式的创建/克隆（`cp -a --reflink=auto`）/清理/`.lunarward/prefix.json` |
| `Features/Runtime/ProfileShellService.cs` | 终端探测（konsole/kitty/…）、`BuildEnvironment`、`exec` 直连 |
| `Features/Runtime/OutputCapture.cs` | 输出捕获、环形缓冲 5000、落盘、脱敏（D27） |
| `Features/GameLauncher/LaunchPreflight.cs` | 6 项预检与 `LaunchPreflightException` |
| `Features/GameLauncher/LaunchReport.cs` | 每次启动写 JSON 到 `logs/launch/` |
| `Features/Companion/*.cs` | 条目模型/存储/执行器/自动启动（三时机） |
| `Features/Tray/StatusNotifierTray.cs` | SNI 托盘 + DBusMenu + 点击唤回 |
| `Features/SingleInstance/SingleInstancePipe.cs` | Unix socket 单实例转发（show/exit/startgame） |
| `Features/Screenshot/ScreenshotViewerWindow.xaml(.cs)` | 自研看图窗（翻页/方向键/在文件夹中显示） |
| `Features/Screenshot/VideoBackgroundPlayer.cs`（在 `Features/Background/`） | LibVLC vmem → Skia 合成 → `WriteableBitmap` |
| `Features/Dev/DevSwitches.cs` | 开发直达开关集中处（§8） |
| `LinuxConfig.cs` / `AppConfig.*.cs` 适配 | XDG 路径、`WineRegistry`、DeviceId、权限判定 |

---

## 6 · 排除编译的上游文件（55 条）

| 子系统 | 条数 | 代表文件 | 排除原因 | 归属 |
|---|---|---|---|---|
| `Features/Screenshot/*` | 12 | `ScreenshotPage.xaml.cs`、`ScreenCaptureService.cs`、`ImageViewWindow2.xaml.cs` | DXGI/Win2D 画布与捕获 | M3 已用 Linux 实现替代，剩余 Warp 见 §2 |
| `Features/Codec/*` | 8 | `ImageLoader.cs`、`HDR10GammaEffect.cs` | Win2D/ComputeSharp 特效 | M4（接 `Starward.Codec` + 系统原生库） |
| `Features/GameInstall/*` | 4 | `GameInstallService.cs`、`InstallGameDialog.xaml.cs` | gRPC/RPC + Windows 路径 | **进行中（本优先级 3）** |
| `Features/Background/*` | 3 | `AppBackground.xaml.cs`、`BackgroundViewWindow.xaml.cs` | Win2D + MediaPlayer | M3 已替代 |
| `Features/Update/*` | 3 | `SetupService.cs`、`UpdateService.cs`、`UpdateWindow.xaml.cs` | Windows 安装器 | M2 已替代 |
| `Features/Overlay/*` | 3 | `OverlayWindow.xaml.cs`、`RunningGameService.cs` | Win32 钩子 | M4 |
| `Features/GameRecord/*` | 3 | `LoginPage.xaml.cs`、`BBSWebBridge.xaml.cs`、`BattleChronicleWindow.xaml.cs` | WebView2 脚本桥 | M2/M4 |
| `Features/GamepadControl/*` | 2 | `GamepadController.cs`、`SimulateInputTipsWindow.xaml.cs` | GameInput/InputSimulator | M4 |
| `Features/UrlProtocol/*` | 2 | `UrlProtocolService.cs`、`TestUrlProtocolWindow.xaml.cs` | 注册表 | M2 已替代 |
| `Features/Setting/*` | 2 | `HotkeyManager.cs`、`ScreenshotSetting.xaml.cs` | RegisterHotKey / DXGI | M2/M3 已替代 |
| `Features/RPC/*` | 1 | `RpcService.cs` | 命名管道 | M4 |
| `Helpers/*`（Windows 原版） | 8 | `FileDialogHelper.cs`、`HotkeyManager.cs`、`DriveHelper.cs` 等 | 已有 `.Linux.cs` | M1 |
| `Frameworks/*` | 1 | `WindowEx.cs` | PInvoke 标题栏 | M1 部分替代：拖拽/最小化交给 WM |

---

## 7 · 与上游 XAML 的差异（必须保持最小，共 7 处）

| 文件 | 改动 | 原因 |
|---|---|---|
| `Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml` | 改名 `*.xaml.disabled` | 整页 Win2D 实时预览 + DirectX 色彩管理（D31），`<Page Remove>` 在 Uno SDK 不生效（L-17） |
| `Features/GameRecord/DailyNote/DailyNoteButton.xaml` | `ToString('HH\:mm')` → `ToString('HH:mm')` | Uno 把 `\:` 写成非法 C# 转义（L-16） |
| `Features/Screenshot/ScreenshotPage.xaml` | `GridView`+`GroupStyle` → `ItemsControl`+`WrapPanel` | Uno 分组容器不渲染（L-48，用户拍板） |
| `Features/GameRecord/LoginPage.xaml` | 隐藏 `WebView2`，叠加「在独立窗口中登录」面板 | D29/D32 |
| `Features/ViewHost/MainView.xaml` | 自绘标题栏拖拽区与最小化/关闭按钮已**移除** | Uno X11 无 `SetDragRectangles`，自绘标题栏既拖不动也点不动（L-73，交还 WM） |
| `Features/GameSetting/GameSettingPage.xaml` | 加「运行配置」「后启动程序」分区；修正上游 `Grid.Row` 越界的那一处 | D28；L-37 |
| `Features/Setting/SettingPage.xaml` | 加「运行环境」导航项 | D28 |

---

## 8 · 开发开关与 CLI（正式版收尾时处理）

命令行优先，环境变量回落（`Features/Dev/DevSwitches.cs`）：

| 命令行 | 环境变量 | 作用 |
|---|---|---|
| `--page <launcher\|gamesetting\|screenshot\|gacha\|record\|selfquery\|beyond\|setting\|login\|notice>` | `LUNARWARD_PAGE` | 启动直达页面 |
| `--subpage <Setting 子页>` | `LUNARWARD_SUBPAGE` | 直达设置子页 |
| `--game <biz>` | `LUNARWARD_GAME` | 选定游戏（同时设 `SelectedGameBizs` 与 `CurrentGameBiz`） |
| `--scroll <px>` | `LUNARWARD_SCROLL` | 页面滚动位置（截图用） |
| `--profile-editor` | `LUNARWARD_PROFILE_EDITOR` | 自动打开第一个 Profile 编辑框 |
| `--viewer <index>` | `LUNARWARD_VIEWER` | 自动打开第 N 张截图的查看器 |
| `--window-action <minimize\|close>` | `LUNARWARD_WINDOW_ACTION` | 窗口动作自测 |
| `--preflight <gameKey> [exe]` | — | 只跑预检不启动 |
| `--launch-plan <exe> [args]` | — | 只打印启动计划 |
| `--url-plan <url>` | — | 只打印深链展开 |
| `--companion-plan <gameKey>` / `--companion-run <gameKey> <id>` | — | 后启动程序计划 / 实跑一条 |
| `--shell-plan` | — | 打印各 Profile 的调试 shell 命令 |
| `--prefix-ensure <gameKey> [profileId]` / `--prefix-template <dir> [name]` | — | 前缀创建 / 做成模板 |
| `--graphics-list` / `--graphics-import <dir>` / `--graphics-install <prefix> <id>` | — | 图形组件 |
| `--output-tail [n]` | — | 读输出落盘日志 |
| `--capture-test` / `--convert-screenshots [folder]` | — | 截图捕获 / 批量转 JPG |
| `--update-check` / `--register-protocol` / `--protocol-status` | — | 自更新 / 协议 |
| `--runner-check <wine>` | — | 运行器体检 |

`--page/--subpage/--game/--scroll/--viewer/--window-action/--profile-editor` 属**测试设施**，正式发布前要么移除，要么只在 Debug 构建编译。

---

## 9 · 变更记录

| 日期 | 变更 |
|---|---|
| 2026-11-02 | 建立本表；登记 28 个 Linux 实现、6 个兼容层、62 条排除 |
| 2026-11-03 | 安装器：Sophon 清单 + chunk 下载打通（`--install-plan` / `--install-fetch` / `--install-run`） |
| 2026-11-03 | 下载/差分改用 `Hi3Helper.Sophon`（删除手写下载器与 `hpatchz` 子进程）；新增 `--patch-plan`（L-57） |
| 2026-11-03 | 恢复 `--install-run`/`--install-fetch`（库版）+ 文件级续传 + 版本标记（`--version-status`/`--version-write`，L-58） |
| 2026-11-03 | 安装服务接真实流程（`InstallPipeline` + `GameInstallService.Linux.cs`），修正预下载参数语义（L-59） |
| 2026-11-03 | 修 `getBuild` 响应未拆 `data` 导致「预下载清单为空」（L-61）；预下载大小改用 `getPatchBuild` 的 stats[本地版本]（7.1.0→7.2.0 ≈ 8.34 GB），不再显示整包 136 GB |
| 2026-11-03 | 小问题批：无预下载时给出明确提示并禁用开始按钮、托盘菜单补「打开设置 / 打开截图文件夹」、`--predownload-size` 诊断命令、公告窗「在浏览器中打开」兜底 |
| 2026-11-03 | 游玩时长改成 `wineserver -w` 会话判定 + 心跳落盘（`PlayTimeSession.Linux.cs`，L-55） |
| 2026-11-03 | 新增单元测试工程 `tests/Lunarward.Tests`（链接纯逻辑源码，10 个用例：规划分类/MD5 判定/FormatSize/版本标记往返） | 
| 2026-11-03 | i18n：新增 `Lang.Lunarward_*` 共 74 键（en/zh-CN/zh-TW/zh-HK/ja/ko），运行环境页、后启动程序、预检弹窗、输出面板、截图页、查看器、登录页已接线；`Lang.Designer.cs` 改 `partial` + `Lang.Lunarward.cs` |
| 2026-11-03 | **全表校准**：按代码实测重写（33 个 `*.Linux.cs`、8 个 `*.LinuxAuto.cs`、55 条排除）；修正过时状态（视频壁纸、URL 协议、截图图库/查看器、更新检查、AssemblyVersion）；新增总览表、新增模块表、XAML 差异表（7 处）、CLI 清单；去掉重复的「更新：…」附录，合并到对应功能域 |
| 2026-11-05 | D44 加固：原子写（`JsonFile`）、预检只读 + key 统一、安装目录/空间校验、前缀清理挂 wine 会话、单实例回调回 UI 线程、playtime 按 pid 去重、CLI 失败退出码、日志与临时文件保留策略、截图设置页实装 |
| 2026-11-05 | D45 CI 只验证 Linux 侧：删除上游 6 个 Windows 工作流，新增 `.github/workflows/linux.yml`（Release 需先 `restore -p:Configuration=Release`，见 L-82） |
| 2026-11-06 | D50：托盘不可用时关闭=退出、桌面通知、6 语言补齐与语言设置生效、强调色按背景取色 | 缺口清单第一批（m13555） |
| 2026-11-06 | D51 非 M4 收尾：14 项缺口逐条定论（补丁体积语义、CLI 中文、托盘信号、HDR、AVIF/JXL、自更新、登录依赖前置检查、热键/Wayland、NoticeWindow、CI 出包、上游同步、CHANGELOG、D36 结项、真机待验） |
| 2026-11-05 | D48：托盘补 `Properties.GetAll`（KDE 菜单+图标）、视频随窗口隐藏暂停/唤回恢复、静态背景合成 `Theme` 文字层 |

| 2026-11-10 | 安装链路：chunk 级复用（`ChunkReusePlanner`/`ChunkReuseDownloader`）+ `UpdateOrchestrator` 组合编排（补丁 7.76 GB + chunk 16.04 GB，省 47%）+ 可续传（`RunResumableAsync`）；Repair 改用本地版本清单且不写版本标记（D42/L-68） |
| 2026-11-11 | GitHub 上线与工程化：`ATTRIBUTIONS.md`、单提交历史、`.github/workflows/linux.yml` 替代上游 6 个 Windows 工作流；`slnx` 收敛为 Linux-only 四工程（D45/D53） |
| 2026-11-12 | 运行环境 D63（三段页 + 整页编辑器 + 图形层校验/移除 + 上次启动徽标）；D64 冗余清理（删 `GameNoticeWindow`、取消 `shared` 前缀、`EnsureGraphics` 归一、来源标签、`RunOnceService`） |
| 2026-11-12 | 测试分层 D65：L1 49 条 + L1.5 `--selftest-ui` 8 条 + L2 47 条脚本 + L3 真机；覆盖率 90.39% 行 / 77.95% 分支（`tests/coverlet.runsettings`） |
| 2026-11-13 | 背景链路定案（D69）：补齐上游 `SetBg`/`SetGameBackgroundIds` 记账、4 处静默 `return false` 带原因、起播失败不再覆盖已有画面；D70 `*.Linux.cs` 无痕 catch 收口为 2 处 |
| 2026-11-13 | 视频看门狗 2 s → 5 s（L-125）：VP9 1080p 首帧实测 2.0–3.0 s，旧阈值把正常起播报成故障，真机三次复测帧差 39.70%–48.13% |
| 2026-11-14 | 切游戏卡 4 秒修掉（D72/L-127）：`wine reg query` 同步 fork 在 UI 线程（冷启动 ~4 s）→ 注册表访问全异步 + 一次取整键 + `reg import` 批量写 + 缓存；前缀创建与启动预检挪出 UI 线程；`.reg` 必须带 UTF-16LE BOM（L-128） |
| 2026-11-14 | `IsTextTrimmedChanged` 7 处回加（L-24 已过期，L-132）+ 自检第 9 条证明 Skia 会触发；三处硬编码中文入 `Lang`（6 键 × 12 语言）；启动组成实测后决定不做 `x:Load`（可延迟部分仅 85 ms） |
| 2026-11-14 | 26 个上游偏差逐个复核完（A19/B4/C3）：手动备份 zip 目录 bug、`0.0%` 帧被吞、悬空分隔线、无用 `x:Name` 已修；`PointerCursor.partial` 为必需（L-130）；冷启动与游戏设置对话框首次打开的分层耗时入基线 |
| 2026-11-14 | 运行环境阶段 1（D73）落地：受管组件 `manifest.json` + 布局归一化、4 连接分片下载（可续传/自动降级）、`tar` 导入与摊平、健康检查只给原文；CLI `--component-scan/health/import/download`；顺带修掉图形层符号链接去重与大小写匹配 |

- 2026-11-06：`#1` 图标码位替换（E62E→E769 等 7 处，L-88）、`#2` 暂停静态图预加载、`#3` 窗口底座统一（D49）；图库窗口 1500×845 且有系统标题栏（真机几何 + `_NET_FRAME_EXTENTS` 验证）。

