# 移植矩阵（Windows → Linux）

- **状态**：v0.1（基于 `df8ae07` 源码实测量）
- **最后更新**：2026-11-01
- **统计口径**：`grep -rl` 文件数（同文件多次出现只计 1）
- **标记含义**：`直通` 无需改动｜`适配` 小改｜`替换` 换实现｜`重写` 新写｜`裁剪` 不移植

---

## 1. Windows API 面总表

| 依赖 | 文件数 | 主要落点 | 处理 | Linux 对应 | 里程碑 | 风险 |
|---|---|---|---|---|---|---|
| `Microsoft.UI.Xaml`（WinUI 3） | 126 | 全部页面与控件 | **直通/适配** | Uno Platform 6.6 的 WinUI XAML 兼容层 | M1 | **高** |
| `Microsoft.UI.Xaml.*` 控件（NavigationView/TabView/InfoBar 等） | — | XAML + code-behind | 适配 | Uno 已实现大部分；缺项走 `CommunityToolkit` 或自绘 | M1 | 中 |
| `Microsoft.UI.Windowing` | 19 | 窗口尺寸/标题栏/全屏 | 替换 | `IPlatformService.WindowService`（Uno `Window` + AppWindow 抽象） | M1 | 中 |
| `Microsoft.UI.Dispatching` | 7 | `DispatcherQueue` | 直通 | Uno 提供同名类型 | M1 | 低 |
| `Microsoft.UI.Composition` | 6 | 动画/阴影/模糊 | 适配 | Uno 的 Composition 子集；不支持处降级为 Storyboard | M2 | 中 |
| `Windows.UI`（Color/Colors/Text/ViewManagement） | 23 | 主题色/状态栏 | 适配 | Uno 提供 `Windows.UI.Color` 等；`ViewManagement` 需替换 | M1 | 低 |
| `Windows.Storage` | 21 | `StorageFile`/`ApplicationData` | 替换 | `IStorageService` + `System.IO` | M1 | 低 |
| `Windows.Foundation` | 18 | `Uri`/`Size`/`Point`/`IAsyncOperation` | 直通 | Uno 提供 | M0 | 低 |
| `Windows.System` | 35 | `VirtualKey`、`Launcher`、`DispatcherQueue` | 适配 | `VirtualKey` 直通；`Launcher`→`xdg-open` | M1 | 低 |
| `Windows.ApplicationModel.*` | 10 | 数据传递/包信息/激活 | 替换 | 自实现（单实例 + socket） | M1 | 中 |
| `Microsoft.Windows.AppLifecycle` | 4 | 单实例/激活转发 | 替换 | `ISingleInstanceService`（flock + socket） | M1 | 低 |
| `Windows.Graphics`（Capture/Imaging/DirectX） | 28 | 截图、图像、GPU | **替换/裁剪** | portal+PipeWire（M4）；图像用 SkiaSharp/ImageSharp | M1/M4 | 高 |
| `Microsoft.Graphics.Canvas`（Win2D） | 20 | HDR 处理、缩略图、截图标注、背景视频 | **替换/裁剪** | SkiaSharp（`SKBitmap`/`SKImage`/`SKColorFilter`）；M1 先降级 SDR | M1/M3 | 高 |
| `ComputeSharp.D2D1` | 3 | HDR10/uHDR gainmap 像素着色器 | 裁剪 | 无；M3 用 SkSL 或 CPU 实现 | M3 | 中 |
| `Microsoft.Web.WebView2` | 6 | 登录、公告、云游戏、更新页 | 部分替换 | `UnoFeatures=WebView`（Linux = WebKitGTK）：渲染/导航可用，cookie/JS/settings 未实现 → 取值另走独立 WebKit 窗口或 passport API | M1 | **中** |
| `Windows.Media.*` | 3 | 背景视频播放 | 替换 | LibVLCSharp 或 `GStreamer`（M2，可先裁剪） | M2 | 中 |
| `H.NotifyIcon.WinUI` | 1 | 托盘 | 替换 | `ITrayService`（StatusNotifierItem over D-Bus） | M1 | 中 |
| `H.InputSimulator` / `SharpGameInput` | 3（GamepadControl） | 手柄读取 + 虚拟输入 | 替换 | evdev 读取 + uinput（M4） | M4 | 中 |
| `Vanara.PInvoke.*` | 38 | 系统对话框、Shell、DWM、SetupAPI | **替换** | `IFileDialogService`（portal）+ `IBackdropService` | M1 | 中 |
| `Microsoft.Win32.Registry`（含 `Registry` API） | 16（约 98 处调用） | 游戏注册表、配置、卸载信息 | 替换 | `IRegistryService` → 前缀内 `reg` 命令 / `system.reg` 解析 | M2 | 中 |
| `System.Runtime.InteropServices`（P/Invoke） | 36 | 多为 Vanara 附带；裸 P/Invoke 仅：`Snap.HPatch`×6、`uxtheme.dll`×2、`Shcore.dll`×2、`wtsapi32.dll`×1、`CoreMessaging.dll`×1 | 裁剪/替换 | 前二者随 Setup 裁剪；`Shcore`（DPI）→ Uno 自动；`wtsapi32`（会话锁）→ logind | M1 | 低 |
| `Microsoft.Windows.AppNotifications` | 少 | 通知 | 替换 | `INotificationService`（D-Bus） | M1 | 低 |
| 自研包 `Starward.Codec` | 8（`Features/Codec`）+ 调用点 | 图像解码/编码/HDR | **未采用**（原计划移植） | 实际改用 `SkiaSharp`（解码/尺寸）+ `Hi3Helper.Sophon`（安装包）；AVIF/JXL/HDR gainmap 记为 M4 再评估（D21 二次修订） | — | 低 |
| 自研包 `Starward.NativeLib` | RPC/Setup | 7z 解压、HPatch 增量 | **未采用** | 7z 解压用 `ZipArchive`；增量安装用 `Hi3Helper.Sophon` 的分块/差分，不再需要 `SharpSevenZip` / `Snap.HPatch` | — | 低 |
| 自研包 `Starward.GameInput` | 3 | 手柄 | 裁剪 | evdev + uinput | M4 | 中 |
| 自研包 `Starward.Win2D` | 20 | 同 Win2D（D2D 投影） | **不可移植** | 按接口换 SkiaSharp（3 个 D2D1 效果需重写） | M1/M3 | 高 |
| 自研包 `Starward.Assets` | 全 UI | 图标字体、图片 | **直通** | — | M0 | 低（需验证字体加载） |
| `WindowsAppSDK`（Mica/Backdrop/主题） | 19+ | `SystemBackdropHelper`、`TransparentBackdrop` | 替换 | 合成器模糊探测 + 降级 | M2 | 低 |
| `Windows.Gaming.Input` / XInput | 0 | — | — | — | — | — |

> 关键结论：**UI 面（126 文件）依赖 Uno 的兼容层，非 UI 面基本上都能在 1–3 个接口后面隔离**。真正的高风险缩到两处：XAML 兼容性、Win2D/截图；WebView2 经 M0 实测降为「渲染可用、取值需绕行」（G0-3b）。

---

## 2. 按功能目录的移植矩阵

| 目录（`src/Starward/Features`） | LOC | 文件 | 可直通 | 需适配 | 需替换 | 需裁剪 | 主要风险点 |
|---|---:|---:|---|---|---|---|---|
| `GameRecord`（战绩/登录） | 15724 | 47 | 数据层 | 页面 | WebView2 登录 | — | 登录流程、cookie 持久化 |
| `Gacha`（抽卡记录） | 8796 | 40 | 几乎全部 | 少量 | — | — | Sqlite 路径、代理设置 |
| `Screenshot`（截图/图库） | 8393 | 23 | 图库 UI | 文件监听 | Win2D 绘制、屏幕捕获 | 捕获（M4） | 图像解码走 Codec 移植；截取需 portal（M4） |
| `GameLauncher`（启动） | 7091 | 26 | 页面/进度 | 进程启动 | `Process.Start`/时长统计 | — | 与 Runtime 对接是核心工作量 |
| `Setting`（设置） | 4334 | 25 | 大部分 | 路径/主题 | 注册表、热键、自启 | — | 平台项需逐项替换 |
| `GameInstall`（安装更新） | 2417 | 9 | 下载/校验逻辑 | — | 解压/权限 | — | 与 RPC 的 socket 改造 |
| `PlayTime`（游玩时长） | 2389 | 14 | 全部 | — | 进程监控 | — | 依赖 agent |
| `Background`（背景） | 2232 | 13 | 图片背景 | 视频背景 | Win2D 帧 | 视频（可选） | 内存/性能 |
| `Codec`（图像编解码） | 2207 | 8 | 大部分 | `CanvasBitmap` 绘制 | D2D1 效果（3 文件） | `VP9Decoder` | **未采用**：解码走 `SkiaSharp`（`ImageLoader.Linux.cs`）；AVIF/JXL 支援见 08 Q-033 |
| `Update`（自更新） | 1988 | 8 | 版本检查 | — | 安装流程 | — | 走 AUR/自更新（D10） |
| `ViewHost`（主窗口/导航） | 1851 | 20 | 布局 | 标题栏 | 窗口服务 | — | 托盘/单实例 |
| `GameSelector`（游戏选择） | 1766 | 6 | 全部 | — | — | — | 低 |
| `GameSetting`（游戏设置） | 1651 | 7 | 大部分 | 图标/路径 | HDR 对话框 | HDR | D13 |
| `SelfQuery`（账号查询） | 1493 | 4 | 全部 | — | — | — | 低 |
| `GamepadControl` | 1431 | 3 | — | — | 全部 | M4 前隐藏 | 纯 Windows |
| `Database`（Sqlite 层） | 1185 | 2 | 全部 | 路径 | — | — | XDG 路径 |
| `GameAccount` | 625 | 5 | 全部 | — | — | — | 低 |
| `HoYoPlay` | 378 | 1 | 全部 | — | — | — | 低 |
| `Overlay`（游戏内覆盖） | 360 | 4 | — | — | 全部 | M4 | portal/PipeWire |
| `CloudGame`（云游戏） | 313 | 3 | — | — | WebView2 | 可选 | D12 |
| `UrlProtocol` | 206 | 3 | 解析逻辑 | — | 注册机制 | — | `.desktop` |
| `RPC` | 168 | 1 | protobuf 逻辑 | 传输层 | 命名管道 | — | Unix socket |

**合计**：可直接复用约 **45%**（按 LOC），需适配 **30%**，需替换/重写 **25%**。

---

## 3. 项目级处理

| 项目 | 处理 | 说明 |
|---|---|---|
| `Starward.Core` | **直通**（≈95%） | `net10.0` 纯托管。需处理：`GameRegistry.cs`（注册表）、`Environment.SpecialFolder`（XDG）、UA 字符串、`HttpClient` 代理 |
| `Starward.Language` | **直通**（100%） | 仅改 TFM |
| `Starward` 主工程 | **适配为主** | 见上表；XAML 尽量不动 |
| `Starward.RPC` | 适配 | 命名管道 → Unix domain socket；`Process.Start` 拉起方式保留 |
| `Starward.Setup` / `Starward.Setup.Core` | **裁剪** | Windows 安装器；Linux 走 AUR + 自更新（D10） |
| `Starward.Launcher`（C++） | **裁剪** | 单实例/DPI/协议注册由 `ISingleInstanceService` 接管 |

### 3.1 作者自研 NuGet 包（源码位于上游 `large-resource` 分支，已补全核查）

实测环境：`~/.nuget/packages`（包）+ `github.com/Scighost/Starward@large-resource`（源码，2026-11-01 拉取，提交 `3028327`）。该分支顶层只有 `Starward.Assets/`、`Starward.Codec/`、`Starward.NativeLib/`、`VP9DecoderMFT/`、`img/`、`logo.ai`。

源码已镜像到本仓库 **`vendor/upstream/large-resource/`**（只读，不参与构建）：`Codec` 195 文件、`NativeLib` 35 文件、`Assets` 166 文件，共约 13 MB；含 `MANIFEST.sha256.txt`；Windows 原生 DLL 与 `VP9Decoder` 刻意排除（说明见 `vendor/upstream/README.md`）。

| 包 | 版本 | 目标框架 / 资产 | 实质 | 处置 |
|---|---|---|---|---|
| `Starward.Assets` | 0.6.5 | `contentFiles/any/any/Assets`，166 个文件（CSS、Font、Image） | **纯资源**：`Segoe Fluent Icons.ttf`、`PhotosIcons.ttf`、`XboxFluent2.ttf` + 图标库 | **保留**（跨平台可用）；M0 必须验证 Uno Linux 能加载图标字体（G0-7） |
| `Starward.Codec` | 0.5.2 | `lib/net10.0-windows` + `runtimes/win-{x64,x86,arm64}/native`（`avif.dll`、`jxl.dll`、`uhdr.dll`、`lcms2.dll`、`brotli*.dll`、`libyuv.dll`、`VP9DecoderMFT.dll`） | **C# P/Invoke 包装层**：libavif 1.3.0、libjxl 0.11.1、libultrahdr 1.4.0、Little-CMS 2.17、Brotli、libyuv、PNG chunk 解析；外加 Windows MFT 的 VP9 解码器 | **可移植**（改判）：从 `large-resource` 取源码建 `src/Lunarward.Codec`，`net10.0`；用 `NativeLibrary.SetDllImportResolver` 把 `avif.dll`→`libavif.so.16` 等映射到系统/AUR 原生库，不改源码；**仅删 `VP9Decoder`**（M3） |
| `Starward.NativeLib` | 0.3.1 | `lib/net9.0-windows` + 原生 `7z.dll`、`Snap.HPatch.dll` | `SharpSevenZip`（fork of JeremyAnsel/SharpSevenZip，靠 `kernel32!LoadLibrary` + COM 加载 7z.dll）+ `Snap.HPatch`（纯 `LibraryImport` 调 libhpatch，跨平台） | **混合**：`Snap.HPatch` 可移植（需 `libhpatch.so`，仅 Setup 用 → 裁剪）；`SharpSevenZip` **不可移植** → 换 `SharpCompress`（纯托管）或 `7z` CLI（M2） |
| `Starward.GameInput` | 0.3.1 | `lib/net9.0`（`SharpGameInput.dll`，无 runtimes） | 上游 `TheNathannator/SharpGameInput` 的托管封装（"C# interop bindings for the GameInput API"，topic=windows） | **不可移植** → 裁剪，M4 用 evdev/uinput 另写（不影响 M0/M1） |
| `Starward.Win2D` | 1.3.2-release.1 | `lib/net45`/`net6.0-windows10.0.19041.0`/`uap10.0` + `runtimes/win-*` | `microsoft/Win2D` 的 fork（源码在 `Scighost/Win2D@nuget/Starward.Win2D`），Direct2D + WinRT 的托管投影 | **不可移植**（D2D 是 Windows 图形栈）→ 按接口替换为 SkiaSharp |

**重要含义**（据补全源码后的实测修订）：

1. 五个包的源码都在上游 `large-resource` 分支（`Starward.Assets`/`Codec`/`NativeLib`）或作者 fork（`Win2D`）与上游第三方（`SharpGameInput`），**可以取源码自建，不需要从零重写**；
2. **`Codec` 是跨平台原生库的 C# 包装层**（libavif/libjxl/libultrahdr/lcms2/brotli/libyuv + 自写 PNG chunk 解析），`net10.0-windows` 只是打包限制 → 目标是**移植**（复制 C# 源码 + 原生库换 Linux 构建 + DllImport 名称映射），而不是用 SkiaSharp 重写；收益：HDR gainmap / AVIF / JPEG XL / ICC 的画质与元数据能力**全部保留**，M3 工作量从 4–6 周降到 2–3 周；
3. 真正需要替换为 SkiaSharp 的只有 `Starward.Win2D` 提供的**绘制原语**（`CanvasBitmap`/`CanvasDevice`/像素着色器效果），共 20 个文件；其中 `HDR10GammaEffect`/`SrgbGammaEffect`/`UhdrGainmapEffect` 三个 D2D1 效果需用 SkSL 或 CPU 路径重写（HDR 显示在 M1 仍先降级 SDR）；
4. `VP9Decoder`（Windows MFT）与背景视频解码一起处理：改用 LibVLCSharp/GStreamer，不阻塞主线；
5. `NativeLib` 的 `SharpSevenZip` 依赖 `kernel32!LoadLibrary` + COM 加载 `7z.dll`，**只能换实现**（`SharpCompress` 纯托管，或调用 `7z`/`bsdtar` CLI）；`Snap.HPatch` 本身跨平台，但只被 `Starward.Setup` 使用，随 Setup 一起不移植；
6. 许可：`Codec`/`NativeLib` 源码随主仓为 MIT（该分支无独立 LICENSE 文件，需按 Q-032 确认后再纳入）；原生库各自的许可（libavif BSD-2、libjxl BSD-3、libultrahdr Apache-2.0、lcms2 MIT、brotli MIT、libyuv BSD-3）需写入 `THIRD-PARTY-NOTICES`；
7. `Starward.Assets` 内 `Segoe Fluent Icons.ttf` 的再分发许可仍需确认（Q-029）。

---

## 4. 「保留但不编译」清单（D2）

以下内容留在仓库中以降低上游同步冲突，但**不进入 Linux 构建**：

```xml
<!-- src/Starward/Starward.csproj（示意） -->
<ItemGroup>
  <Compile Remove="Platform/Windows/**" />
  <Compile Remove="Frameworks/**" />            <!-- WindowEx / TransparentBackdrop 等 -->
  <Compile Remove="Features/Overlay/**" />      <!-- M4 前 -->
  <Page    Remove="Features/Overlay/**" />
  <Compile Remove="Features/GamepadControl/**" />
  <Page    Remove="Features/GamepadControl/**" />
</ItemGroup>
```

原则：
1. 先**排除**，不删文件；
2. 若某文件的 80% 逻辑可复用（如 `GamepadControl` 之外的 `PlayTime`），则改为「抽接口 + 条件注册」，仍保留原文件；
3. 删除动作统一推迟到 M3 之后，单独立项（避免把上游 diff 变成不可合并）。

---

## 5. 界面保真度清单（硬对齐项）

| 页面 | XAML 是否改动 | 允许的 Linux 差异 |
|---|---|---|
| `ViewHost/MainWindow.xaml` | 最小 | 无系统标题栏时自绘标题区；窗口按钮位置 |
| `ViewHost/MainView.xaml` | 不改 | 无 |
| `GameLauncher/GameLauncherPage.xaml` | 不改 | 无 |
| `GameLauncher/StartGameButton.xaml` | 不改 | 无 |
| `GameLauncher/GameLauncherSettingDialog.xaml` | 不改 | 新增「运行环境」页签见 R-03（新增页面，不改既有页） |
| `Setting/*` | 小改 | 平台相关项替换文案与控件 |
| `GameRecord/*` | 小改 | WebView 容器替换 |
| `Screenshot/*` | 中改 | 图库逻辑保留，图像处理替换 |
| `ViewHost/WelcomeWindow.xaml` | 小改 | 首次运行引导（Linux 特有步骤可新增页） |

**新增页面**（不改既有页面信息架构）：

1. 运行环境管理（列表/编辑器/导入导出）
2. 组件库（扫描结果/版本/来源/引用关系）
3. 启动计划预览 + 调试终端
4. 伴随启动配置
5. 启动报告查看器
6. 迁移导入向导

---

## 6. 实测校准（2026-11-02，M1-0）

真实编译 `src/Lunarward` 后修正了纸面估计：

| 层 | 纸面估计 | 实测 |
|---|---|---|
| C# 文件 | "非 UI 面基本可隔离" | **146/232 零错误**，剩余 8 个类型 / 52 条错误 |
| XAML | "82 个页面逐个修" | **616 条错误，63% 由 6 个共享控件/包引起** |
| 最大单点 | WebView2 | **`CachedImage`（280 条）**，WebView2 已降级 |
| 第三方包 | 未知 | `Microsoft.Xaml.Behaviors`、`CommunityToolkit.WinUI.Controls.*` 均只有 `net8.0-windows`，必须在 Linux 侧替换 |

## 6. 工作量基线（粗估，单人等效）

| 模块 | 乐观 | 保守 | 说明 |
|---|---:|---:|---|
| M0 PoC | 1 周 | 2 周 | 门禁验证 |
| 平台层（20 接口） | 3 周 | 5 周 | 托盘/通知/对话框/单实例是大头 |
| Uno 主工程跑通 | 3 周 | 6 周 | XAML 兼容问题不可预估 |
| Runtime 引擎 | 4 周 | 7 周 | 核心资产 |
| 启动对接 + 报告 | 2 周 | 3 周 | |
| WebView 登录 | 2 周 | 5 周 | 高风险，可能改走系统浏览器 |
| 图像（Codec 移植 + 原生库） | 1–2 周 | 3 周 | `Lunarward.Codec` + DllImport 映射；比整目录重写省 2–4 周 |
| 截图/捕获 | 1 周 | 2 周 | M4，可裁剪 |
| 迁移导入 | 1 周 | 2 周 | |
| 打磨/回归 | 3 周 | 6 周 | |

**结论**：达到「原神 + 绝区零可玩且观感接近」约 **14–22 周**（不含上游同步维护）。
