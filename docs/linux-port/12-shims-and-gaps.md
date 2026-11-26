# 12 · Linux 替身、缺口与状态登记表

> 这份文档回答一个问题：**Linux 版里，哪些地方是「真的做了」，哪些只是「先让它编译过」？**
>
> **最后核对**：2026-11-24（HEAD `2b86682`，D123）。本文只回答「实现了没有、还是只是替身」。
>
> **数据来源**（2026-11-24 全部重跑，勿凭记忆填数）：`bash scripts/list-deviations.sh`、`grep -c '<Compile Remove' src/Lunarward/Lunarward.csproj`、`find src/Lunarward -name '*.Linux.cs'`。
>
> **D94–D109 期间状态有变化，以对应审查文档为准**：`26`（76 条实现错位，70 条已修）、`28`（运行环境 130 条：首轮 42 + 复审 88，全修）、`29`（运行环境 UI 21 条，全修）。受影响的替身/实现：`InstallGameDialog`、`PreDownloadDialog`、`GameInstallService`、`GameRecordPage`、`ScreenshotPage`/`ScreenshotSetting`、`CachedImage` 缩略图、`RuntimeEnvironmentSetting.*`、`BattleChronicleWindow`（仍是 M2 占位）。
>
> 维护规则（强制）：每新增一个 `*.Linux.cs`、每新增一处 `Compat/` 兼容层、每把一个上游文件加入
> `<Compile Remove>`，都必须同步更新本表。没有登记的替身视为缺陷（D33）。
>
> **本表是唯一登记处。** 其它文档只描述机制，不描述状态。
>
> 逐点偏离（265 条，含必要性判定）见 `25-deviation-register.md`；实现错位 bug 候选（76 条，已全部复核）见 `26-misalignment-audit.md`。

- 最后更新：2026-11-24（全表复核：§3 补登 4 个漏登文件并纠正 2 处路径、§6 按 csproj 现算 53 条、§7 用 diff 脚本重算 XAML 差异；2026-11-20 的文档整理记录：删 17 份过时文档、D91 打包本地源、D90 CI 产物、D89 SDK 下限、D88 打包依赖单一来源、D87 运行器显示名、D86 注册表改读文件）
- 对应代码：`src/Lunarward/`（`main` 分支；`git rev-list --count HEAD` = 1485 提交，其中 109 条动过 `src/Lunarward/`；`linux-port` 已于 2026-11-13 停用）
- 最后核对：2026-11-25（HEAD = 合并 upstream 0.19.0 + net11 分支工作区）
- 规模：**31 个** `*.Linux.cs`、5 个 `*.LinuxAuto.cs`、`Compat/` 兼容层 2 个文件（`VanaraHotkeyCompat.cs`、`NotifyIcon/TaskbarIconCompat.cs`）、47 条 `<Compile Remove>`（2026-11-25 `find` / `grep` 实测；对齐上游 0.19.0 后 `GameInstallShims.cs`、`GameInstallService.Linux.cs`、`InstallGameDialog.Linux.cs`、`PreDownloadDialog.Linux.cs` 四个文件已删除，`WindowEx.Linux.cs` 随基类改名 `WindowBase.Linux.cs`）

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
| 运行环境（Profile） | ✅ | **一级页**（`NavigationView` footer、齿轮正上方，D80）：五页签（概览/运行器/组件/前缀/增强）+ 整页编辑器 + 启动预览 + 失效绑定提示；页签自绘（`Pivot` 页签头在 Skia 下点不动，L-153）；首次使用自动建/改名环境并绑定（`EnsureFor`，D77）；删环境列引用者并清绑定；图形层拆成 DXVK 与 VKD3D-Proton 两个字段、可同时装（D83）；概览卡片补「选的和前缀里不一致」提示 + 一键同步、共用前缀提示 + 为游戏克隆、最近三次启动历史，组件页补「清理未使用」，编辑器补常用变量与 HUD/分辨率预设（D84） | D63 D74 D77 D80 D82 D83 D84 |
| 前缀生命周期 | ✅ | 前缀只是路径：空=默认目录（首次启动创建）/ 有值=存在就复用、不存在就创建；元数据在 `.lunarward/prefix.json`，互斥靠 `.lunarward/lock`；root 下拒绝创建（D77） | — |
| 组件管理 | ✅ | 扫描系统 / Steam（含 `libraryfolders.vdf` 里的自定义库与 Proton 目录，D78）/ 自带组件；受管组件下载（队列/暂停续传/镜像测速/暂存列表/组件源编辑）、导入归档、健康检查、受管目录删除（只覆盖 `components/<分类>/<目录>`，D77）；DXVK·VKD3D 装入前缀；扫描阶段零副作用；`--list-runners [--json]` | 组件市场 ⛔ 设计选择（D58 v2） |
| 启动前预检 + 启动报告 | ✅ | 11 项只读检查（运行身份 / 可执行文件 / 运行器 / 前缀 / 前缀结构 / 可写 / 占用 / 图形组件 / 游戏自带 dll / 游戏架构 / 增强开关）、失败弹窗、JSON 报告、前缀互斥锁 | D81 |
| 后启动程序（Companion） | ✅ | 条目 CRUD、三种触发时机、伴随启动、`--companion-plan/run`；`WINEPREFIX` 按游戏 key + 前缀模式解析（不再只认 `PrefixPath`） | 注入型只编排不实现（D24） |
| 输出与诊断 | 🟡 | 输出捕获/落盘/脱敏、面板、「运行输出」对话框；**只有沉默/面板两级**（`terminal` 级按 D37 降级为 M4，`RuntimeProfile.GameOutput` 目前是未消费字段） | M4（terminal 级） |
| 游玩时长 | ✅ | `wineserver -w` 判定会话结束，进程早退时回退最近 Start 事件；30s 心跳写 `Interruption=true`，启动器被杀也能留下时长 | — |
| 启动器主界面 | ✅ | 1200×676 DIP、**系统标题栏**（交给窗口管理器，L-73）、真实公告/活动 | — |
| 外部链接 | ✅ | `Helpers/OpenUrl.cs`：**portal 优先**（`Portal.TryOpenUri`，xdg-desktop-portal）→ `xdg-open` → `gio open`，替换 8 类 `Launcher.LaunchUriAsync` 调用（后者在 Linux 静默失败，L-74）；沙箱内走 portal | `AppBackground` 里 3 处 apps.microsoft.com 保留（Windows 专属提示） |
| 背景切换 / 视频壁纸 | ✅ | 圆点切换器由代码生成（Uno PipsPager 不响应点击，L-69）；视频走 libvlc `--vout=vmem` + Skia 合成（`Video.Url`，非海报），媒体选项带 `:avcodec-hw=vaapi` 硬解（D123，`LUNARWARD_VIDEO_HWDEC=0` 可关）；隐藏/锁屏暂停、唤回恢复（D48/L-86）；静态背景底图 + `Theme` 文字层合成（D48/L-87）；暂停静态图预加载（D49）；整机 CPU **112% → 74%**（D123 实测，1920×1080 VP9；禁硬解 100%）；强调色按底图/海报取色（D50） | 无 vlc 或无视频文件时退回静态兜底图；`PropertiesChanged` 不需要（D51）；NVIDIA 的 VAAPI 是 NVDEC 解码后**下载回内存**，省的是解码 CPU，不是带宽 |
| 背景图库窗口 | ✅ | `BackgroundViewWindow` code-behind 重新纳入编译；缩略图 `ListView + tk:WrapPanel`；本地绝对路径由 `CachedImage.Linux` 读字节解码；窗口交还系统标题栏并按 `DefaultWindowSizeInDips` 设 1200×676、夹到 90% 屏幕内（D49） | 无 |
| 单实例 / 陈旧锁 | ✅ | 文件锁 + UNIX socket；无人接时判定陈旧锁并接管；无 `DISPLAY`/`WAYLAND_DISPLAY` 明确报错退出（L-72）；`--new-instance` 跳过检查 | 无 |
| 设置页 9 项 | ✅ | 上游 9 项；「运行环境」已提为 footer 一级页（D80） | D80 |
| 登录 / 账号 | 🟡 | WebKitGTK 独立窗取 cookie；**未真实登录验证** | 待用户 |
| 抽卡记录 | 🟡 | 页面与导出可用；真实数据未验证 | 待用户 |
| 截图 | 🟡 | Alt+D 捕获、图库、多选、批量转 JPG、查看器（显示/翻页/在文件夹中显示/Esc） | 查看器缺上游的缩放/旋转/复制/另存（`ScreenshotViewerWindow` 只做浏览）；HDR 色彩管理 ⬜ M4 |
| 壁纸 | ✅ | 静态图 + 视频（LibVLC，缺库自动回退静态） | — |
| 托盘 | ✅ | StatusNotifierItem 注册、`Properties.GetAll`、DBusMenu 菜单（用户已确认右键可用）、点击唤回、托盘不可用时关闭=退出（D48/D50、L-85/L-91） | 托盘属性不发 `PropertiesChanged` |
| 全局热键 | 🟡 | X11 轮询（25ms）；唤回走进程内 libxcb（D47，不再依赖 xdotool）；Wayland 会话下给「绑定 `lunarward --show`」提示（D51） | 平台限制：纯 Wayland 无全局热键标准 |
| URL 协议 / 单实例 | ✅ | `lunarward://` + socket 转发（show/exit/startgame） | — |
| 自更新 | ✅ | 查 AUR RPC 版本并报告；不下载替换、网络失败当无更新（D5/D33；D51 定论） | — |
| 游戏安装 / 预下载 / 增量更新 / 修复 | 🟡 | `Features/GameInstall/GameInstallService.cs` 是**进程内 facade**，直接调官方 `Starward.GameInstall.GameInstallRunner`（上游 UI 侧是 gRPC 客户端、真正干活的 `GameInstallRunner` 跑在 `Starward.RPC` 子进程里；上游 0.19.0 已删掉整个 RPC 项目，D128）。公开 API 与上游逐字一致：安装/预下载/更新/修复/插件更新/热更新/过场清理/卸载 + Pause/Continue/Stop，进度来自 `GameInstallProgress`（PercentProgress 0–1、网络/磁盘字节、速度、剩余时间、RunnerState）。移植侧自写的 Sophon 管线（`InstallPipeline`/`UpdateOrchestrator`/`ChunkReuseDownloader`/`ChunkResume`，2663 行）随 D128 一并删除；CLI 的 `--install-run/-fetch/-chunk-plan/-chunk-run`/`--update-run`/`--patch-run`/`--sophon-probe` 同时退役（`--install-plan`/`--patch-plan`/`--update-plan`/`--predownload-size`/`--version-status`/`--version-write`/`--install-task` 改走官方 API） | 差分子进程隔离没有了（主进程崩溃会带走安装任务）；离线安装链路的自动化测试（假 Sophon 服务 + `chunk-smoke.sh`）已退役，改为真实网络人工验收 |
| 悬浮窗（时钟/帧率） | ⬜ | `OpenOverlayWindow()` 恒 false | M4 |
| 手柄控制 | ⬜ | 空实现 | M4 |
| RPC 服务 | ⬜ | 恒「未运行」 | M4 |
| 云游戏 / 战绩 / BBS 桥 | ⬜ | 占位 | M4 |
| 打包分发 | ✅ / 🟡 | `PKGBUILD` 出包并跑通；AppImage 脚本就绪缺 `appimagetool` | — |
| i18n | ✅ | 11 个语言文件与 `Lang.resx` 的 965 键完全一致（86 条 Lunarward 文案已译 de/es/it/ru/th/vi）；语言设置现真正生效（D50/L-90） | CLI 输出固定中文（诊断接口，D51 定论） |

---

## 3 · Linux 实现文件（32 个 `*.Linux.cs` + 3 个非 `*.Linux.cs` 替身）

| 文件 | 替代的 Windows 机制 | 状态 | 里程碑 | 说明 |
|---|---|---|---|---|
| `Program.Linux.cs` | WinUI 生成入口 | ✅ | M1 | Uno `UnoPlatformHostBuilder`；CLI 子命令（见 §8） |
| `Features/WindowBase.Linux.cs` | `WindowBase` 标题栏/PInvoke（上游 0.19.0 把 `Frameworks/WindowEx.cs` 改名 `Features/WindowBase.cs`） | ✅ | M1 | 基于 `Window.AppWindow`；`CenterInScreen` 先 `Resize` 再 `MoveAndResize`（后者在 X11 空操作）；`SetDragRectangles`/`AdaptTitleBarButtonColorToActuallTheme`/`ChangeAccentColor` 空实现 → Windows 上「自绘标题栏」方案在 Linux 不可用，改用系统标题栏。`Hide()/Show()/ShowFromMinimized()` 走 `Helpers/X11Window`（libxcb unmap/map + `_NET_ACTIVE_WINDOW`），因为 Uno X11 既没有 `HideCore`、`Activate()` 也只发 `XRaiseWindow`（D47、L-84）；`Show()` 统一关掉 `ExtendsContentIntoTitleBar` 并按 `DefaultWindowSizeInDips` + 屏幕钳制摆位（D49）；`SetIcon()` 复用 `Helpers/AppIcon` 的 `_NET_WM_ICON`（每个窗口都调，L-183）；`ShouldAppsUseDarkMode/ShouldSystemUseDarkMode` 读 GTK `settings.ini`/`kdeglobals` 推断（以前恒 true，浅色桌面的子窗口被强制深色，L-183）；另维护窗口登记表给 `Helpers/XamlRootExtension.GetAppWindow` 按 `XamlRoot` 反查真正的 `AppWindow`（以前一律回落到主窗口） | X11 下图标/标题栏配色只能尽力而为；纯 Wayland 无 `_NET_WM_ICON` 写入通道 |
| `Controls/CachedImage.Linux.cs` | `Scighost.WinUI.CachedImage` | 🟡 | M1 | ms-appx/网络/本地（本地读字节 + `SetSourceAsync`）；`CornerRadius` 为矩形裁剪 |
| `Helpers/FileDialogHelper.Linux.cs` | `FileOpenPicker`/`FileSavePicker` | 🟡 | M1 | **portal 优先**（`Portal.PickFilesAsync`，xdg-desktop-portal）→ 失败才 `kdialog` → `zenity`；沙箱内走 portal，可用（D75） |
| `Helpers/ClipboardHelper.Linux.cs` | WinRT Clipboard | 🟡 | M1 | wl-copy → xclip → xsel |
| `Helpers/DriveHelper.Linux.cs` | `DriveInfo`/`GetDriveType` | ✅ | M1 | 解析 `/proc/mounts` |
| `Helpers/AccentColorHelper.Linux.cs` | `Helpers/AccentColorHelper.cs`（WinRT/WIC 读像素取色） | 🟡 | M3 | 改 SkiaSharp 从背景图/海报取主色，异步取色后**回 UI 线程**写 `App.xaml` 主题字典（`DispatcherQueue` 在 `Task.Run` 里取到 null，L-89/D50）；Windows 上还染标题栏与边框（DWM），Linux 交给 WM（D47） |
| `Helpers/DataFolder.Linux.cs` | 无同名上游文件（Linux 新增） | ✅ | M1 | 应用数据目录统一：把 `~/.local/share/lunarward` 与 `~/.local/share/Lunarward` 合并成小写一个、大写目录换成符号链接（D56）；`Program.Linux.cs:1272` 启动即 `Unify()` |
| `Helpers/SessionLockWatcher.Linux.cs` | `WM_WTSSESSION_CHANGE` 会话锁通知 | ✅ | M3 | 订阅 `org.freedesktop.ScreenSaver.ActiveChanged` 与 logind `Session.Lock/Unlock`，发 `MainWindowStateChangedMessage{SessionLock}`（以前这个消息在 Linux 上没有任何发送方，视频壁纸锁屏后还在解码，D135） |
| `Helpers/HotkeyManager.Linux.cs` | `RegisterHotKey` + 消息循环 | 🟡 | M2 | `XQueryKeymap` 25ms 轮询；Alt+S 唤回、Alt+D 截图；唤回走 `Helpers/X11Window.Show`（原先按标题 `"Starward"` 查找、而实际标题是 `Lunarward`，唤回永远失败，已修） |
| `Helpers/X11KeyState.cs` | 无 | 🟡 | M2 | 只做只读查询与 keysym→keycode；窗口操作走子进程，本进程不碰 Xlib 分配/错误处理器（L-45） |
| `Helpers/XamlRootExtension.Linux.cs` | `GetWindowHandle`/拖动区 | 🟡 | M1 | 句柄恒 0；`GetAppWindow()` 经 `ContentIslandEnvironment` 反查并兜底 `MainWindow.Current`（L-31） |
| `Helpers/SystemBackdropHelper.Linux.cs` | Mica/Acrylic | 🟡 | M3 | 只把 `Content.Background` 铺成**不透明底色**（`FallbackColor`：Mica `0xFF202020`/`0xFFF3F3F3`、Acrylic `0xFF545454`/`0xFFD3D3D3`），无叠层、无模糊 |
| `Helpers/WindowWaitAsync.Linux.cs` | 窗口句柄等待 | ✅ | M1 | 改用窗口生命周期事件 |
| `Properties/AssemblyVersion.cs` | MSBuild 生成版本特性 | ✅ | M2 | Uno.Sdk 不生成版本特性；配合 `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`（L-50） |
| `Features/Background/AppBackground.Linux.cs` | Win2D + MediaPlayer 动态壁纸 | ✅ | M3 | 静态图**原样显示**（`_image.Opacity = 1.0`，底色 `#16161A`；0.42 的半透明近似已弃用——观感明显偏暗）与视频（`VideoBackgroundPlayer`，LibVLC 为 Uno 传递依赖）双路；无 libvlc 回退静态（L-41/L-43/L-52） |
| `Features/Codec/ImageLoader.Linux.cs` | Win2D `CanvasBitmap` | ✅ | M1 | 尺寸走 SkiaSharp `SKCodec`；解码 PNG/JPG/WebP 走 Skia，**AVIF/JXL/HEIC 走 Magick.NET**（`Helpers/MagickCodec`，`NeedsMagick` 覆盖这 4 个扩展名；原生库缺失时提示不支持，D75） |
| `Features/Overlay/RunningGameService.Linux.cs` | `SetWinEventHook` | 🟡 | M1 | `Process.HasExited` 判定存活，不感知窗口焦点；`OpenOverlayWindow()` 恒 false |
| `Features/RPC/RpcService.Linux.cs` | 命名管道 + gRPC 子进程 | ⬜ | M4 | 恒「未运行」；`CreateRpcClient` 抛 NotSupported（D6） |
| `Features/GameInstall/GameInstallService.cs`（**非** `*.Linux.cs`） | `Starward.RPC.GameInstall` 的 gRPC 客户端包装 | ✅ | M3 | 移植侧改成**进程内 facade**：同名同命名空间（`Starward.Features.GameInstall`），内部直接 `CreateContextAsync` + `StartOrContinueTaskAsync`，不再起子进程（上游 0.19.0 已删 RPC 项目，D128）。`RunOnce`/限速/任务进度订阅与上游一致 | 少了子进程隔离；`GameInstallController`（gRPC 服务端）在 Linux 上不编译 |
| `Features/GameInstall/GameInstallScanner.Linux.cs` | 注册表枚举已装游戏 | ✅ | M1 | 扫 `~/games`、`/opt`、Steam `common` 与设置页配置的扫描根（D134 删掉 `$LUNARWARD_GAME_ROOTS`），`NameMatches` 防国服/B 服同名 exe 误认 |
| `Features/GameLauncher/GameLauncherService.Linux.cs` | `ProcessStartInfo{Verb="runas"}` + `cmd /c start` | ✅ | M1–M2 | 直 `exec wine`；补 Proton 的 `WINEDLLPATH`/`LD_LIBRARY_PATH`/`PROTONPATH`（L-44）；前缀锁、报告、退出钩子都在这里 |
| `Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs` | Win2D HDR 预览 | 🟡 | M3 | `.xaml` 已改名 `*.xaml.disabled`（整页 Win2D 实时预览，D31）；对话框由本文件**代码重建**：3 个 Slider + 数值文本 + 恢复默认 + `Opened` 读 `GameSettingService.GetGenshinHDRLuminanceAsync` / 关闭写回，只能调写入游戏的亮度参数，无预览 |
| `Features/GamepadControl/GamepadController.Linux.cs` | `SharpGameInput` + `InputSimulator` | ⬜ | M4 | 整体未实现 |
| `Features/GameRecord/LoginPage.Linux.cs` | WebView2 内嵌登录 | 🟡 | M1–M2 | 主路径是**本程序 `--webkit-login` 子进程**（WebKitGTK 6.0 + GirCore，D76），直接读 WebKit 的 cookie 库；`WebKitLoginHost.IsAvailable()` 为假才回落 `python3` + `python-gobject` 跑 `login.py`（D29/D32） |
| `Features/GameRecord/BBSWebBridge.Linux.cs` | WebView2 脚本桥 | ⬜ | M4 | 仅有 `GameBiz`/`IsHoyolab`/`IsLoggedIn` |
| `Features/GameRecord/BattleChronicleWindow.Linux.cs` | 战绩窗口 | ⬜ | M4 | 占位 |
| `Features/PlayTime/PlayTimeSession.Linux.cs` | 无同名上游文件（上游靠进程退出即判定会话结束） | ✅ | M2 | `wineserver -w` 等该前缀的 wine 全部退出（wine 会 fork，只盯启动拿到的 pid 会早退）；拿不到 wineserver 时退回「进程 + 子孙进程」轮询；30s 心跳写 `Interruption`（L-55） |
| `Features/Screenshot/ScreenCapture.Linux.cs` | `Windows.Graphics.Capture` + DXGI | ✅ | M3 | `spectacle -b -n -f -o` → `grim` → `gnome-screenshot` 降级链，三者都不行再走 portal `ScreenshotAsync`；目录优先取设置里的文件夹，否则 `SpecialFolder.MyPictures`（为空回落 `~/Pictures`）下的 `Lunarward/` |
| `Features/Screenshot/ScreenshotPage.Linux.cs` | DXGI 图库 | ✅ | M3 | 扁平 `ItemsControl` + `WrapPanel`（L-48）；多选、批量转 JPG、打开查看器 |
| `Features/Setting/ScreenshotSetting.Linux.cs` | DirectX 色彩管理设置 | 🟡 | M3 | 文件夹/格式/质量/自动复制剪贴板/批量转换/测试截图可用；`EnableScreenshotColorManagement`、`AutoConvertScreenshotToSDR` 只是恒关属性，HDR 转换未接（D31） |
| `Features/Update/UpdateService.Linux.cs` | GitHub Release | 🟡 | M2 | 查 AUR RPC 版本；网络失败按「无更新」 |
| `Features/Update/UpdateWindow.Linux.cs` | Windows 自更新 UI | 🟡 | M2 | 检查/忽略/提醒可用；「立即更新」按钮**可点**，点了给出「请用包管理器（AUR/AppImage）升级」的提示（走 `SetErrorMessage` 才会刷新，L-183；以前按钮恒禁用且无解释） |
| `Features/Update/SetupService.Linux.cs` | Windows 安装器 | ⛔ | — | Linux 不做（D5） |
| `Features/UrlProtocol/UrlProtocolService.Linux.cs` | 注册表注册 `starward://` | ✅ | M2 | 写 `~/.local/share/applications/lunarward.desktop` + `update-desktop-database` + `xdg-mime` |

> `Features/GamepadControl/*`、`Features/Overlay/*`、`Features/RPC/*` 的 Windows 原版在 §6 中排除；
> 这些子系统的 Linux 方案尚未定型，M4 再决定「真做还是不做」。
>
> 公告/资讯**没有独立窗口**：`Features/GameLauncher/GameBannerAndPost.xaml.cs` 直接把链接交给系统浏览器（`Helpers.OpenUrl.Open`，D40/D64）；`GameNoticeWindow.xaml`/`GameNoticeWindow.xaml.cs` 已于 D64 删除（提交 `5cd4862b`），该行曾登记为 `GameNoticeWindow.Linux.cs`，2026-11-24 核对时作废。

---

## 4 · 名字兼容层（`Compat/` 2 个文件 + `Controls/` 5 个文件 + 2 项无同名文件，共 8 行）

原则（D30）：**补名字可以，补行为不行**。只在上游大面积使用、替换成本远超收益时开。

| 文件 | 覆盖的类型 | 状态 | 说明 |
|---|---|---|---|
| `Compat/VanaraHotkeyCompat.cs` | `Win32Error` / `HotKeyModifiers` / `VK` / `User32.*` | 🟡 | 补常量与 `Succeeded/Failed`，另含 `WindowLongFlags` + `SetWindowLong`（x64 → `SetWindowLongPtrW`）真 P/Invoke —— **Linux 上调用会 `DllNotFoundException`**，现有调用点都包在 `OperatingSystem.IsWindows()` 里或本身不编译；真正注册热键在 `HotkeyManager.Linux.cs` |
| 桌面通知 | `Helpers/DesktopNotification.cs`（无同名上游文件） | ✅ | **D-Bus 直连优先**（Tmds.DBus 调 `org.freedesktop.Notifications.Notify`）→ 失败才 `notify-send` → `gdbus Notify` 兜底；安装/预下载/修复/更新、截图保存各发一条（D50/D75）；无通知服务时静默跳过 |
| 强调色 | `Helpers/AccentColorHelper.Linux.cs`（无同名上游文件） | 🟡 | 按背景/海报取主色写主题字典并刷新（D50/L-89）；Windows 上还染标题栏/边框（DWM），Linux 交给 WM（D47） |
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
| `Features/GameLauncher/LaunchPreflight.cs` | 11 项预检与 `LaunchPreflightException` |
| `Features/GameLauncher/LaunchReport.cs` | 每次启动写 JSON 到 `logs/launch/` |
| `Features/Companion/*.cs` | 条目模型/存储/执行器/自动启动（三时机） |
| `Features/Tray/StatusNotifierTray.cs` | SNI 托盘 + DBusMenu + 点击唤回 |
| `Features/App/SingleInstancePipe.cs` | Unix socket 单实例转发（show/exit/startgame） |
| `Features/Screenshot/ScreenshotViewerWindow.xaml(.cs)` | 自研看图窗（翻页/方向键/在文件夹中显示） |
| `Features/Background/VideoBackgroundPlayer.cs` | LibVLC vmem → Skia 合成 → `WriteableBitmap`；媒体选项带 `:avcodec-hw=vaapi`（D123，`LUNARWARD_VIDEO_HWDEC=0` 关） |
| `Features/App/DevSwitches.cs` | 开发直达开关集中处（§8） |
| `LinuxConfig.cs` / `AppConfig.*.cs` 适配 | XDG 路径、`WineRegistry`、DeviceId、权限判定 |
| `Features/GameAccount/WineRegistryFiles.cs` | 直接解析 `<prefix>/user.reg` / `system.reg`（`\xHHHH` 解码 + 折行拼接），命中就不 fork；给不出结论才回落 `wine reg query`（D86/L-174） |
| `Helpers/OneShotHints.cs` | 一次性提示 | ✅ | M1 | SQLite `Setting` 表的 `hint_<name>`，替代进程级 `static bool`（D86） |
| `Features/Runtime/RunnerNaming.cs` | 运行器显示名 | ✅ | M1 | PATH 里的 wine 只有装在 `/usr`、`/bin`、`/sbin` 才叫「系统 Wine」，其余用目录名；重名才加来源后缀（D87/L-161） |

### 5.1 · 上游库 `src/Starward.GameInstall/` 里的偏差（D128 / D131 / D132）

> 这个库不是 UI 代码，但 0.19.0 把安装/热更新整体搬进来了，Linux 侧必须能编译能跑。改动全部带 `// LUNARWARD-DEVIATION:` 注释。

| 文件 | 偏差 | 原因 | 决策 |
|---|---|---|---|
| `Starward.GameInstall.csproj` | TFM `net11.0-windows` → `net11.0` | Linux 构建平台中立 | D128 |
| `Service/HardLinkCompat.cs`（新增） | 跨平台 `IsSupported` / `TryCreate` | 上游只认 NTFS/ReFS（`DriveInfo.DriveFormat`），Linux 的 ext4/btrfs/xfs 同样支持；`.NET 11` 已内建 `File.CreateHardLink` | D131 |
| `Service/HardLinkService.cs` | 3 处改用上面的兼容层；文件 ID 的 `Kernel32` 调用只留 Windows 分支 | 文件 ID 两列在库里只建索引、无人读取，Linux 填 0 即可 | D131 |
| `GameInstallRunner.cs` | 截图备份的同卷判断改成「文件系统是否支持硬链接」，跨卷交给 `TryCreate` 失败回退复制 | Linux 上所有路径的根都是 `/`，「根相同」恒真 | D131 |
| `HotUpdate/GenshinHotUpdateService.cs` | 11 处 `@"Persistent\X"` / `@"StreamingAssets\X"` 拆成多段 `Path.Combine`；`GetServerName` 双平台（Windows 读注册表，Linux 读 wine 前缀 `user.reg`）；`GetDispatchRegionInfoAsync` 多一个 `installPath` 参数；不再按平台关闭热更新 | 反斜杠在 Linux 上是普通字符（L-188）；Linux 没有注册表，但 wine 把值落在 `<prefix>/user.reg`；`!OperatingSystem.IsWindows()` 门禁按 D132 解除 | D131 / D132 |
| `Service/WinePrefixRegistry.cs`（新增） | 从安装目录回溯找 wine 前缀，流式解析 `user.reg` 里的 REG_BINARY（折行 + `\xNNNN` 转义） | 库不能反向依赖 Lunarward 的 `WineRegistryFiles`（internal + 命名空间），自带窄读取器 | D132 |
| `HotUpdate/GenshinHotUpdateStep.cs` | `Kernel32.CreateHardLink` 改 `HardLinkCompat.TryCreate`，删掉唯一一处 `using Vanara.PInvoke` | Linux 上没有 Kernel32；失败仍回退 `File.Copy` | D132 |

---

## 6 · 排除编译的上游文件（47 条）

> 口径：`grep -c '<Compile Remove' src/Lunarward/Lunarward.csproj` = **47**（2026-11-25 重跑；D134 删掉上游已移除的截图二代/批量转换/图片查看与 `TestUrlProtocolWindow` 后从 53 降到 47）。逐条文件清单见 `25-deviation-register.md` 附录 A（与 csproj 逐条一致，脚本比对无差异）。

| 子系统 | 条数 | 代表文件 | 排除原因 | 归属 |
|---|---|---|---|---|
| `Features/Screenshot/*` | 7 | `ScreenshotPage.xaml.cs`、`ScreenCaptureService.cs`、`ScreenshotHelper.cs` | DXGI/Win2D 画布与捕获 | M3 已用 Linux 实现替代，剩余 Warp 见 §2 |
| `Features/Codec/*` | 8 | `ImageLoader.cs`、`HDR10GammaEffect.cs` | Win2D/ComputeSharp 特效 | M4（接 `Starward.Codec` + 系统原生库） |
| `Helpers/*`（Windows 原版） | 8 | `FileDialogHelper.cs`、`HotkeyManager.cs`、`DriveHelper.cs`、`AccentColorHelper.cs` 等 | 已有 `.Linux.cs` | M1 |
| `Features/GameInstall/*` | 4 | `GameInstallService.cs`、`GameInstallTaskStartedMessage.cs`、`InstallGameDialog.xaml.cs` | gRPC/RPC + Windows 路径 | M3 已替代：流程搬进主进程 |
| `Features/GameRecord/*` | 3 | `LoginPage.xaml.cs`、`BBSWebBridge.xaml.cs`、`BattleChronicleWindow.xaml.cs` | WebView2 脚本桥 | M2/M4 |
| `Features/Overlay/*` | 3 | `OverlayWindow.xaml.cs`、`RunningGameService.cs`、`RunningGame.cs` | Win32 钩子 | M4 |
| `Features/Update/*` | 3 | `SetupService.cs`、`UpdateService.cs`、`UpdateWindow.xaml.cs` | Windows 安装器 | M2 已替代 |
| `Features/Background/*` | 2 | `AppBackground.xaml.cs`、`AccentColorHelper.cs` | Win2D + MediaPlayer / WinRT 取色 | M3 已替代；`BackgroundViewWindow.xaml.cs` **没有被排除**，仍在编译 |
| `Features/GamepadControl/*` | 2 | `GamepadController.cs`、`SimulateInputTipsWindow.xaml.cs` | GameInput/InputSimulator | M4 |
| `Features/Setting/*` | 2 | `HotkeyManager.cs`、`ScreenshotSetting.xaml.cs` | RegisterHotKey / DXGI | M2/M3 已替代 |
| `Features/UrlProtocol/*` | 1 | `UrlProtocolService.cs` | 注册表 | M2 已替代 |
| `Controls/*` | 1 | `CachedImage.cs` | 依赖 WinUI 控件库 | M1 已替代（`Controls/CachedImage.Linux.cs`） |
| `Features/GameSetting/*` | 1 | `GenshinHDRLuminanceSettingDialog.xaml.cs` | Win2D 实时预览 | M3 已用代码重建对话框 |
| `Features/RPC/*` | 1 | `RpcService.cs` | 命名管道 | M4 |
| `Features/WindowBase.cs` | 1 | 上游 Windows 实现（Vanara User32 子类化） | PInvoke 标题栏 | 用 `Features/WindowBase.Linux.cs` 替代，拖拽/最小化交给 WM；上游那份不编译 |

按目录汇总（脚本输出，合计 47）：`Screenshot` 7、`Codec` 8、`Helpers` 8、`GameInstall` 4、`GameRecord` 3、`Overlay` 3、`Update` 3、`Background` 2、`GamepadControl` 2、`Setting` 2、`UrlProtocol` 1、`Controls` 1、`GameSetting` 1、`RPC` 1、`Frameworks` 1。

```bash
# 复算：按目录聚合（2026-11-24 用过）
grep -o '<Compile Remove="[^"]*"' src/Lunarward/Lunarward.csproj | sed 's/<Compile Remove="//;s/"$//' \
  | awk -F/ '{ if ($1=="Features") print $1"/"$2; else print $1 }' | sort | uniq -c | sort -rn
```

---

## 7 · 与上游 XAML 的差异（37 个字节不同：32 个实质差异 + 5 个仅编码差异；另有 6 个缺失）

> 口径（2026-11-24 重算，脚本见本节末）：`src/Starward` 共 82 个 `.xaml`，其中 39 个与移植侧逐字节相同；37 个字节不同——32 个文本有实质差异、5 个只差 BOM/换行；另有 6 个移植侧没有同名文件（5 个改名 `*.xaml.disabled` + 已删除的 `GameNoticeWindow.xaml`）。
>
> 原则不变：**能不改就不改**，每一处都要能说清「为什么非改不可」。差异行数 = `diff -u` 的新增 + 删除行数。

| # | 文件 | 差异行（+新增/−删除） | 一句话原因 |
|---:|---|---:|---|
| 1 | `Features/GameLauncher/GameLauncherSettingDialog.xaml` | 1077（+542/−535） | `NavigationView` 左栏 + 纵向 `FlipView` 改成 `200/*` 两列网格、删掉整段 `FlipView_Settings`（Uno 纵向 `FlipView` 翻页手势与选中索引不同步、内容超高时底部不可达）；主题 `CustomAcrylicBrush`→`LunarwardSurfaceBrush`，Glyph `F012`→`E896` |
| 2 | `Features/Screenshot/ScreenshotPage.xaml` | 258（+52/−206） | `GridView`+`GroupStyle` → `ItemsControl`+`WrapPanel`（Uno 分组容器不渲染，L-48，用户拍板） |
| 3 | `Features/GameSetting/GameSettingPage.xaml` | 93（+88/−5） | 加「运行配置」「后启动程序」分区（D28）、修上游 `Grid.Row` 越界一处（L-37） |
| 4 | `Features/Setting/AdvancedSetting.xaml` | 56（+56/−0） | 加「RPC 服务未实现」InfoBar（D61）、诊断包与「运行输出」按钮 |
| 5 | `Features/GameLauncher/GameBannerAndPost.xaml` | 35（+22/−13） | banner 加 `DecodePixelWidth`/`Tapped`、`Pivot` 加 `x:Name`；帖子行 `MaxWidth=340`、`Hyperlink`→`Tapped` 走 `OpenUrl`（L-74） |
| 6 | `Features/Background/BackgroundViewPage.xaml` | 35（+16/−19） | `GridView`→`ListView`+`tk:WrapPanel`；`CachedImage` 改 `DecodePixelWidth`/`DisplayPath`（D49/L-48） |
| 7 | `Features/GameLauncher/GameLauncherPage.xaml` | 33（+24/−9） | 加 `NavigationCacheMode`；`PipsPager` 改代码生成圆点（L-69）；加安装任务状态条 |
| 8 | `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml` | 30（+15/−15） | 图标码位替换（`F08F`→`E768`）；`ElementWeaknessVisibility(...,-1)` → `ElementWeaknessConstants.None` |
| 9 | `Features/GameRecord/LoginPage.xaml` | 29（+26/−3） | 隐藏 `WebView2`，叠「在独立窗口中登录」面板（D29/D32） |
| 10 | `Features/GameRecord/StarRail/SimulatedUniversePage.xaml` | 27（+16/−11） | `ItemsRepeater`+`cc:WrapLayout` → `ItemsControl`+`cc:WrapPanel`（Uno 不调自定义 `Layout`） |
| 11 | `Features/Setting/GeneralSetting.xaml` | 24（+2/−22） | 删「透明/动画效果」Windows 系统设置引导段（Linux 无对应开关，D61）；托盘文案去 `x:Bind` |
| 12 | `Features/GameRecord/GameRecordPage.xaml` | 22（+21/−1） | 未登录空态（D61/B50），只盖内容区、保留左侧工具箱可点 |
| 13 | `Features/ViewHost/MainView.xaml` | 21（+18/−3） | 「运行环境」进 footer 一级页（D80）；图标码位替换 |
| 14 | `Features/Setting/SettingPage.xaml` | 14（+10/−4） | 移除「运行环境」子导航项（D80）；页面给实底（D61）；`XboxFluent2` 手柄图标改内置 `Symbols`（`E7FC`） |
| 15 | `App.xaml` | 10（+8/−2） | 弹层/菜单/下拉改实底 `LunarwardSurfaceBrush`（半透明时底层文字透过来，D61） |
| 16 | `Features/Setting/GamepadControlSetting.xaml` | 9（+9/−0） | 整页置灰 + 「M4 未实现」InfoBar（D61） |
| 17 | `Features/Setting/ScreenshotSetting.xaml` | 8（+4/−4） | 单键热键的「管理员权限」提示改 Linux 口径（X11 轮询；Wayland 用系统快捷键）；`Visibility` 补 `Mode=OneWay` |
| 18 | `Features/GameSelector/GameSelector.xaml` | 8（+5/−3） | 卡片宽高由代码按可用宽度均分（D56），不依赖横向滚动 |
| 19 | `Features/Setting/AboutSetting.xaml` | 6（+3/−3） | 品牌改 Lunarward（logo `lunarward.png`、版权行） |
| 20 | `Features/GameLauncher/StartGameButton.xaml` | 6（+3/−3） | 图标码位替换（`E917`→`E823`、`F012`→`E896`，L-88） |
| 21 | `Features/GameInstall/PreDownloadDialog.xaml` | 6（+3/−3） | 三处 `Run.Text` 补 `Mode=OneWay` |
| 22 | `Features/Setting/HotkeySetting.xaml` | 4（+2/−2） | 「管理员权限」提示改 Linux 口径（D61） |
| 23 | `Features/GameRecord/DailyNote/DailyNoteButton.xaml` | 4（+2/−2） | `ToString('HH\:mm')` → `'HH\\:mm'`（Uno 把 `\:` 当非法转义，L-16）+ 图标码位 |
| 24 | `Features/GameInstall/PreDownloadButton.xaml` | 4（+2/−2） | 图标码位替换 |
| 25 | `Features/Update/UpdateWindow.xaml` | 3（+2/−1） | 错误文案 `TextBlock` 加 `x:Name` 供代码控制可见性 |
| 26 | `Features/Setting/FileManageSetting.xaml` | 3（+1/−2） | 去掉 `IsTextTrimmedChanged`（Uno 不触发，L-132）+ 图标码位 |
| 27 | `Features/SelfQuery/SelfQueryPage.xaml` | 3（+2/−1） | 加 `NavigationCacheMode="Required"` |
| 28 | `Features/Gacha/GenshinBeyondGachaPage.xaml` | 3（+2/−1） | 同上 |
| 29 | `Features/Gacha/GachaLogPage.xaml` | 3（+2/−1） | 同上 |
| 30 | `Features/UrlProtocol/TestUrlProtocolWindow.xaml` | 2（+1/−1） | logo 换 `lunarward.png` |
| 31 | `Features/Screenshot/ScreenshotFolderManageDialog.xaml` | 2（+1/−1） | 图标码位替换 |
| 32 | `Features/GameInstall/InstallGameDialog.xaml` | 2（+1/−1） | `MaxLines` 属性换序 |

**仅编码差异（内容一致，5 个）**：`Features/Gacha/GachaStatsCard.xaml`、`Features/Gacha/ZZZGachaStatsCard.xaml`、`Features/GameRecord/StarRail/ApocalypticShadowPage.xaml`、`Features/GameRecord/StarRail/ForgottenHallPage.xaml`、`Features/GameRecord/StarRail/PureFictionPage.xaml` —— 只差 BOM / 换行，渲染与行为完全一致。

**缺失（移植侧没有同名文件，6 个）**：

| 文件 | 现状 | 原因 |
|---|---|---|
| `Features/GameLauncher/GameNoticeWindow.xaml` | 已删除（提交 `5cd4862b`，D64） | 公告/资讯改系统浏览器打开（D40/D64） |
| `Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml` | 改名 `*.xaml.disabled` | 整页 Win2D 实时预览 + DirectX 色彩管理（D31）；`<Page Remove>` 在 Uno SDK 不生效（L-17） |
| `Features/Screenshot/ImageBatchConvertWindow.xaml` | `.xaml.disabled` | Win2D 批量转换窗，功能由 `ScreenshotConverter` + 设置页「批量转 JPG」覆盖 |
| `Features/Screenshot/ImageViewWindow2.xaml` | `.xaml.disabled` | Win2D 看图窗，改用自研 `ScreenshotViewerWindow` |
| `Features/Screenshot/ScreenCaptureInfoWindow.xaml` | `.xaml.disabled` | DXGI 捕获提示窗（`.xaml.cs` 一并排除），Linux 侧失败走通知 + 日志 |
| `Features/Screenshot/ScreenshotPage2.xaml` | `.xaml.disabled` | 旧版图库页，已被重写的 `ScreenshotPage` 取代 |

```bash
# 生成上面两张表（2026-11-24 用过；仓库根执行）
python3 - <<'EOF'
import os, difflib
up, lo = 'src/Starward', 'src/Lunarward'
rows, byteonly, missing = [], [], []
for root, _, files in os.walk(up):
    for name in files:
        if not name.endswith('.xaml'):
            continue
        rel = os.path.relpath(os.path.join(root, name), up)
        a_path, b_path = os.path.join(up, rel), os.path.join(lo, rel)
        if not os.path.exists(b_path):
            missing.append(rel); continue
        a, b = open(a_path, 'rb').read(), open(b_path, 'rb').read()
        if a == b:
            continue
        ta = a.decode('utf-8-sig').replace('\r\n', '\n').splitlines()
        tb = b.decode('utf-8-sig').replace('\r\n', '\n').splitlines()
        if ta == tb:
            byteonly.append(rel); continue          # 只有 BOM / 换行差异
        d = list(difflib.unified_diff(ta, tb, lineterm='', n=0))
        add = sum(1 for l in d if l.startswith('+') and not l.startswith('+++'))
        rem = sum(1 for l in d if l.startswith('-') and not l.startswith('---'))
        rows.append((add + rem, add, rem, rel))
for total, add, rem, rel in sorted(rows, reverse=True):
    print(f'{total:5d}  +{add:<5d}-{rem:<5d}{rel}')
print(f'--- 实质差异 {len(rows)} / 仅 BOM·换行 {len(byteonly)} / 缺失 {len(missing)}')
for rel in sorted(byteonly): print('  BOM ', rel)
for rel in sorted(missing):  print('  MISS', rel)
EOF
```

---

## 8 · 开发开关与 CLI（正式版收尾时处理）

命令行优先，环境变量回落（`Features/App/DevSwitches.cs`）：

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
| 2026-11-14 | 24 号 P1：解压改进程内（`ArchiveExtractor`：BCL `TarReader` + `GZipStream` + `ZstdSharp` + `SharpCompress` 处理 `.tar.xz` + `ZipFile`，可取消/有进度/拦路径逃逸），删掉 `tar`/`xz` 子进程；运行环境页加「下载组件…」（选源→列版本→下载，取消=暂停可续传，下载前空间预检）与「导入归档…」；GitHub 限流 403 改为可操作提示并支持 `LUNARWARD_GITHUB_TOKEN`；新增 `--component-list` |
| 2026-11-14 | 运行环境阶段 2/3（D74）：取消 `PrefixMode` 全部枚举，前缀只是路径（空=默认，首次启动创建）；配置迁到 `~/.config/lunarward/env/<id>.json` 并自动搬移旧 `profiles/`；`LaunchRecipeBuilder` 成为 argv/env 唯一生成处（启动/预览/`--launch-plan` 共用）；运行环境页加状态灯、占用、文本视图与启动预览；`--launch-plan` 支持 `--profile` |
| 2026-11-14 | 24 号 P2/P3/P4：portal 优先（打开链接 / 选文件 / 通知）、截图 portal 兜底、Magick.NET 解 AVIF/JXL、弹层实底、Esc 只退一层、`xdotool`→xcb、`ProcessQuery`→`/proc`（D75、L-145～L-148） |

- 2026-11-06：`#1` 图标码位替换（E62E→E769 等 7 处，L-88）、`#2` 暂停静态图预加载、`#3` 窗口底座统一（D49）；图库窗口 1500×845 且有系统标题栏（真机几何 + `_NET_FRAME_EXTENTS` 验证）。
| 2026-11-15 | 依赖批次（D78/D79）：`SteamLibrary` 解析 `libraryfolders.vdf` 补全自定义库 Proton 扫描；`PeArchitecture` + 预检「游戏架构」项；SharpHook spike 否决（KDE Wayland 收不到 XTEST）、Tmds.DBus.Protocol 不迁（无集成测试）；CLI 帮助改由 `Helpers/CliSpec.cs` 单一来源生成 + 未知开关 exit 2 + `lint-cli-help.sh`；单测 200、cli-smoke 40 / cli-extra 32 |
| 2026-11-15 | 运行环境收尾（D77）：删环境列引用+清绑定、受管组件删除（分类目录判定在先）、root 拒跑（预检+建前缀双道）、游戏自带 dll 提示、首次使用自动建环境（`EnsureFor` 四级）、保存后自动自检、下载队列/镜像测速/暂存列表/组件源编辑/前缀占用汇总；CLI 新增 `--profile-ensure`；单测 145、L2 cli-smoke 40 / cli-extra 28 
| 2026-11-16 | 运行环境 UI 重做（D80/D81/D82）：footer 一级页 + 五页签自绘（`Pivot` 页签头点不动，L-153）+ 概览统计与失效绑定提示 + 增强页（HUD/gamemode/gamescope，schema 3）+ 图形层可选 VKD3D-Proton + gamescope 参数体检；预检 10 → 11 项；`SettingPage` 少一个子导航项 ||
| 2026-11-16 | 运行环境可用性打磨（D84）：一致性比对 + 同步到前缀、组件「清理未使用」、共用前缀提示 + 为游戏克隆、启动历史（`logs/launch-history.jsonl`）、编辑器常用变量、页签键盘切换、删方案可选删前缀、HUD/分辨率预设与 HDR/VRR；单测 241 ||
| 2026-11-18 | D88 Arch 打包：三份 PKGBUILD 的 `depends` 统一为 6 项（新增 `libxcb`、`xdg-utils`，AUR 两份补 `webkitgtk-6.0`；删 `gtk3`/`zstd`），`source` 分支从 `linux-port` 改 `main`、`aur/lunarward` 改 `#tag=v$pkgver`，`packaging/.gitignore` 的 `lunarward/` 改 `/lunarward/`（此前整份 AUR 包定义被忽略、从未进仓库），新增 `scripts/test/lint-pkgbuild.sh`（L-175） |
| 2026-11-18 | D87 运行器显示名单一来源：`/opt` 下的定制 wine 不再叫「系统 Wine」，重名运行器补来源后缀（`RunnerNaming`），详情页优先显示组件名（`DisplayName`）与下拉对齐（L-161） |
| 2026-11-18 | D86 卡顿与「每次重启都重来」：注册表读取改走文件（`WineRegistryFiles`，同一键 **95 ms** vs `reg query` 热 1843 ms / 冷 4128–5411 ms，顺带修好长 `REG_BINARY` 被首行截断与中文键名乱码）；三条提示改持久化一次性（`OneShotHints`）；失效绑定 warn 只一次；删 `[dbg]` 插桩、「识别到 N 个」改用 `KnownGames.Count`（L-174） |
| 2026-11-19 | D89 SDK 下限：5 处 `global.json` 的 `sdk.version` 从 `10.0.300`/`10.0.302` 统一降到 `10.0.100`（`rollForward: latestMinor`），并在根与 `src/Lunarward` 两份里加注释说明「别改回 10.0.3xx」，否则 Arch 打包环境的低版本 SDK 会直接报 `A compatible .NET SDK was not found`；`lint-pkgbuild.sh` 加 D 节（`sdk.version` 末段必须是 `100` + 必须有 `rollForward` + `makedepends` 必须有 `dotnet-sdk`）（L-163） |
| 2026-11-24 | **全表复核（HEAD `2b86682`，D123）**：按脚本重算 36 个 `*.Linux.cs` / 6 个 `*.LinuxAuto.cs` / 53 条 `<Compile Remove>` / XAML 37 差 6 缺；纠正 12 处历史断言（`GameNoticeWindow` 已删、`FileDialogHelper` 走 portal、`ImageLoader` 走 Magick.NET、`SystemBackdropHelper` 只铺不透明底色、`AppBackground` 已弃 0.42 近似、登录走进程内 WebKitGTK 6.0、`ScreenshotSetting` 真实路径、截图目录取 `MyPictures`、通知走 D-Bus 直连、视频壁纸 VAAPI 硬解 112%→74%、外部链接 portal 优先、`SingleInstancePipe`/`DevSwitches` 在 `Features/App/`）；补登 4 个漏记的 `*.Linux.cs`（`GameInstallService`、`PlayTimeSession`、`AccentColorHelper`、`DataFolder`） |

### 9.1　共享文件里的 `LUNARWARD-DEVIATION`（D134 新增两条）

| 文件 : 行 | 原因 |
| --- | --- |
| `Features/ViewHost/SystemTrayWindow.xaml.cs:76` | `WindowActivatedEventArgs.WindowActivationState` 在本地 `net11.0-desktop` 构建里是 `Microsoft.UI.Xaml.WindowActivationState`，CI 的 `dotnet publish -r linux-x64` 里却是 `Windows.UI.Core.CoreWindowActivationState`；写枚举模式匹配会在其中一侧 `CS0266`，改用 `ToString() == "Deactivated"`。 |
| `Features/ViewHost/WelcomeWindow.xaml.cs:46` | `RectInt32` 的 4 参构造器只在 Windows 目标下存在，`linux-x64` publish 报 `CS1729`；改用对象初始化器。 |

