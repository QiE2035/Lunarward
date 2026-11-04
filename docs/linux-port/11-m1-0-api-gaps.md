# 11 · M1-0 实测：把上游代码接到 Uno(Linux) 上到底缺什么

> 目的：不再靠纸面矩阵推断，而是**真的建一个 Linux 头工程编译一遍**，把缺口变成具体数字。
> 产物：`src/Lunarward/`（复制自 `src/Starward/`，**上游目录一个字节没改**）。
> 复现：`cd src/Lunarward && dotnet build Lunarward.csproj -c Debug`（工程自带 `global.json`/`NuGet.config`，用华为云镜像）。

## 1. 工程配置（M1-0 版本）

| 项 | 值 |
|---|---|
| SDK | `Uno.Sdk` 6.7.30 |
| TFM | `net10.0-desktop`（Skia） |
| UnoFeatures | `SkiaRenderer`（暂不含 WebView/其它） |
| 项目引用 | `Starward.Core`、`Starward.Language`（均为 `net10.0`，直接可用） |
| 不含 | `Starward.RPC`（`net10.0-windows`）、`Starward.Setup*`、`Microsoft.WindowsAppSDK`、Win2D/ComputeSharp/Vanara/H.NotifyIcon/Scighost.WinUI/SharpGameInput |
| 排除编译 | 86 个 `.cs`（保留源码，不删除） |

## 2. C# 层结果：146 / 232 个文件直接编译通过

- 232 个 `.cs` 中 **146 个**与 Windows API 无关，按原样编译**零错误**（`Starward.Core`、`Starward.Language` 全量可用）。
- 剩下 **52 条错误，集中在 11 个文件、8 个缺失类型**——全部是"实现了但被排除的 Windows 服务"或"被排除的 Windows 独有 UI 基类"，不是未知的 API 差异：

| 缺失类型 | 错误数 | 性质 | 处理方向 |
|---|---|---|---|
| `HoYoPlayService` / `ReleaseClient` / `ReleaseInfoDetail` | 18 | `Features/HoYoPlay` 因 `Windows.Storage` 被排除 | 换 `System.IO` 直读，属**低难度**替换 |
| `Starward.Features.RPC` | 16 | `Starward.RPC` 是命名管道实现 | 换 Unix domain socket（D6） |
| `SetupService` / `Starward.Setup` | 14 | Windows 安装器 | 不移植（D5） |
| `GameAccountService` | 4 | 依赖 `Windows.Storage` 文件锁 | 换 `FileStream` + flock |
| `PlayTimeRecordService` | 4 | 依赖注册表/进程查询 | 换 `/proc` |
| `WindowEx` / `BattleChronicleWindow` | 4 | 继承 `Microsoft.UI.Windowing` 基类 | 需 Linux 窗口抽象（02 的 `IWindowService`）|
| `PointerCursor` | 2 | 分部类另一半被排除 | 随窗口抽象一起处理 |

**结论**：C# 层没有"深坑"。真正的移植工作 = 实现 5 个 Linux 服务 + 2 个窗口抽象。

## 3. XAML 层结果：616 条错误，但 63% 来自 6 个共享项

把 XAML 编译打开（`EnableDefaultUnoItems=true`）后全量尝试，聚合如下：

| 缺失项 | 错误数 | 位置 | 处理方向 |
|---|---|---|---|
| `Starward.Controls.CachedImage` | **280** | 自家控件，用了 Win2D | 用 `Image`+`BitmapImage` 重写（M1-1） |
| `Starward.Frameworks.WindowEx` | 34 | `Microsoft.UI.Windowing` | 抽象成 `IWindowService` |
| `CommunityToolkit.WinUI.Controls.Segmented` | 30 | 第三方包，无 Linux TFM | 自实现或换 `RadioButtons` |
| `CommunityToolkit.WinUI.Controls.WrapPanel` | 18 | 同上 | Uno Toolkit 有 `AutoLayout`/自实现 |
| `Microsoft.Xaml.Interactivity.Interaction` | 12 | 官方 Behaviors 包只支持 `net8.0-windows` | 换 `Uno.Microsoft.Xaml.Behaviors.Interactivity.WinUI` |
| `Microsoft.Graphics.Canvas…CanvasSwapChainPanel` | 6 | Win2D | 截图/背景子系统重写 |
| `Scighost.WinUI.ImageEx` | 6 | 第三方控件 | 用 `Image` 替代 |
| `CommunityToolkit.WinUI.AttachedDropShadow` | 4 | 包 | 用 `ThemeShadow` |

其余为 `PageBase` 缺成员（`MutliSelect`/`SelectUid`/`GachaLanguage`/`MaxLuminance` 等，各 2–4 条）和 `x:Bind` 对已排除类型的引用。

**结论**：82 个页面不是 82 份独立工作。**先补 6 个共享控件/包，616 条错误里约 390 条（63%）会直接消失**。

## 4. 依赖层的两个硬发现

1. **`Microsoft.Xaml.Behaviors.WinUI.Managed` 无法用在 Linux**：它只提供 `net8.0-windows10.0.17763`，装到 `net10.0-desktop` 会报 `NU1202`。上游 6 个 XAML 用它。Uno 生态的替代是 `Uno.Microsoft.Xaml.Behaviors.Interactivity.WinUI`（官方文档"Using XAML behaviors to execute a command"一节提到）。
2. **`CommunityToolkit.WinUI.Controls.*` 整包不可用**（Segmented/WrapPanel/AttachedDropShadow），因为其 TFM 是 `net8.0-windows`。需要 `Uno.Toolkit.WinUI` 的等价控件或自实现。
3. 已确认可用并补进工程：`Microsoft.Extensions.Http`（`IHttpClientBuilder`）、`ZiggyCreatures.FusionCache`（`FileCache`）。

## 5. 对 M1 计划的修正

| 原计划 | 实测后 |
|---|---|
| "M1 先接 XAML 再逐个修 API" | **倒过来**：先补 6 个共享控件 + 5 个服务，再一次性开 XAML |
| "WebView2 是最大风险" | 已降级（G0-3b/D29），现在最大的是 `CachedImage`（280 条） |
| "M1 需要 2 周" | 缩短：C# 层只剩 8 个类型；主要成本在 `CachedImage` 与 `WindowEx` 两个自研控件的 Linux 实现 |

## 6. 下一步（M1-1，待授权）

1. `CachedImage` 的 Linux 实现（`Image` + 解码交给 `Lunarward.Codec`/SkiaSharp），目标：280 条错误清零；
2. `WindowEx` → `IWindowService`（Linux 用 GtkSharp 或 Uno 的 `Window`）；
3. 换 Behaviors 包、自实现 `Segmented`/`WrapPanel`；
4. 再全量打开 XAML，跑一次完整编译，得到真正的"页面级剩余缺口"。

## 7. M1-1 进展（2026-11-02 当天）

在 `src/Lunarward/` 内实现共享件，**XAML 编译错误从 616 降到 238**：

| 项 | 实现位置 | 效果 |
|---|---|---|
| `CachedImage`（Linux 版） | `Controls/CachedImage.Linux.cs` | **280 条错误清零**；支持 `ms-appx`/`file`/`http`，带 `CornerRadius` 圆角裁剪 |
| `CommunityToolkit.WinUI.Controls.Segmented` / `SegmentedItem` | `Controls/ToolkitControls.cs` | 30 条清零（基于 `ListView`，横向多选） |
| `CommunityToolkit.WinUI.Controls.WrapPanel` | 同上 | 18 条清零（自行实现 Measure/Arrange） |
| `AttachedDropShadow` / `ClipToBounds` / `Normalize` / `NormalizedCenterPoint` | `Controls/ToolkitCompat.cs` | 28 条清零（占位 + 真实裁剪） |
| `Microsoft.Xaml.Interactivity.Interaction` / `Behavior` / `EventTriggerBehavior` / `ControlStoryboardAction` | `Controls/InteractivityCompat.cs` | 12 条清零（编译可用；事件接线待补） |
| `WindowEx`（Linux 版） | `Frameworks/WindowEx.Linux.cs` | 34 条类型缺失清零；基于 Uno 的 `Window.AppWindow`，Windows 专有的图标/拖拽区降级为 no-op |

**当前状态**：XAML 238 条、C# 50 条（全部为待移植服务）。

**关键意外收获**：Uno 6.7.30 的 `Window.AppWindow`、`AppWindow.TitleBar`、`AppWindow.Size/Presenter/Id` **都存在且可用**——这比预期好很多，窗口层不需要自建抽象就能先跑起来。

剩余 238 条的结构：

| 类别 | 数量 | 说明 |
|---|---|---|
| 级联（页面 code-behind 被排除） | ~136 | x:Bind 找不到成员，等对应 `.cs` 移植后自然消失 |
| XAML 处理失败 | 52 | 多为上游 Win2D/背景子系统 |
| `ImageConvertItem` / `BGSWebBridge` 等被排除类型 | 22 | 截图/战绩子系统 |
| Win2D `CanvasSwapChainPanel` | 6 | 需重写 |
| `Scighost.WinUI.ImageEx` | 6 | 背景子系统，计划整块重写 |

## 8. M1-1 第二批：平台服务替换（2026-11-02 下午）

| 上游实现 | Windows 依赖 | Linux 替代 | 说明 |
|---|---|---|---|
| `HoYoPlayService.GetGPUInfos()` | `Vanara.SetupAPI` 枚举显卡 | 读 `/sys/class/drm/card*/device/uevent` + `/sys/module/<driver>/version` | 结果仅用于上报设备指纹 |
| `PlayTimeRecordService` | `Microsoft.Windows.AppLifecycle.AppInstance`（进程互斥） | `Mutex` 命名互斥 | 保证同一游戏进程只记账一次 |
| `GameAccountService` | `Microsoft.Win32.Registry` 读写 `MIHOYOSDK_ADL_*` | 新增 `Features/GameAccount/WineRegistry.cs`，通过 `wine reg query/add` 操作当前前缀（`LUNARWARD_WINEPREFIX`，默认 `~/.wine`） | **这是 Linux 下账号切换的核心**：票据本来就在 wine 前缀注册表里 |
| `DatabaseService` 备份 | `SharpSevenZip`（7z.dll + COM） | `System.IO.Compression.ZipArchive`，备份扩展名 `.7z` → `.zip` | 偏离已记录 |
| `ImageLoader.GetImagePixelSizeAsync` | Win2D / `Starward.Codec` | SkiaSharp `SKCodec` | 截图/缩略图链路的最小实现 |
| `SetupService` | `Starward.Setup`（Windows 安装器） | 空实现占位（D5） | 后续由 AUR/AppImage 自更新替代 |
| `RpcService` | 命名管道 + gRPC | 同签名空实现（D6） | M2 换 Unix domain socket |
| `GamepadController` | SharpGameInput | 空实现（M4 换 evdev） | |
| `BattleChronicleWindow` | WebView2 | 空窗口占位（D29） | 战绩页后续走独立 WebKitGTK |

同时把 `Helpers/InAppToast.cs` 重新纳入编译（它只依赖 Behaviors），**这一项消掉了 1500+ 条级联错误**——原因是 `InAppToast` 缺失导致 `App`、`AppConfig` 全线失效。

当前状态：**XAML 236 条**、C# 剩余错误集中在 `AppConfig.Configuration.cs`（配置/路径/版本）与 82 个仍被排除的文件。

## 9. 下一个阻塞点（已定位）

`AppConfig.Configuration.cs`（353 行）是 60% 级联的根：它定义了 `AppVersion`/`CacheFolder`/`UserDataFolder`/`LogFile`/`ConfigPath`，但用了 `Microsoft.Win32.Registry`、`Vanara.PInvoke`、`WindowsPrincipal`。移植方案已明确：

1. 配置持久化：注册表 → `~/.config/lunarward/config.json`（D22 的 XDG 目录）；
2. 便携模式探测：`DriveHelper`（USB/可移动盘）→ `/proc/mounts` 判断；
3. 管理员判定：`WindowsPrincipal` → `Environment.IsPrivilegedProcess` 或直接 false；
4. `Vanara` 调用（图标/DPI 等）逐个降级为 no-op。

## 10. M1-1 第三批：配置层落地（2026-11-02 傍晚）

`AppConfig.Configuration.cs`（353 行，60% 级联错误的根）已移植：

| 上游 | Linux |
|---|---|
| 配置存注册表 `HKCU\Software\Starward` | 新增 `LinuxConfig`：`~/.config/lunarward/config.json`（键名保持一致：`Language`/`UserDataFolder`/`EnableLoginAuthTicket`/`stoken`/`mid`） |
| `Environment.SpecialFolder.LocalApplicationData\Starward` | `~/.local/share/lunarward`（XDG_DATA_HOME 优先） |
| `WindowsIdentity`/`WindowsPrincipal` 判管理员 | `Environment.IsPrivilegedProcess` |
| `Registry` 读 BIOS/MachineGuid 生成 `DeviceId` | `/sys/class/dmi/id/bios_version` + `/etc/machine-id`（回退到自建 `machine_id`） |
| `DriveHelper` 判可移动盘 | 暂固定 false（M2 用 `/proc/mounts` 细化） |
| `User32.MessageBox` 错误弹窗 | 写 Debug 输出 |

同时修正 `Behavior<T>`：`AssociatedObject` 暴露为 `T`，否则上游 `InAppToast` 的 `AssociatedObject.Children` 编译失败。

当前：XAML 236 条；源码级 C# 错误 118 条（obj 生成代码的级联另有 1200+，随源码修完自然消失）。

剩余源码错误的构成（下一步清单）：

| 类型 | 条数 | 说明 |
|---|---|---|
| 上游扩展方法（`Window.As()`、`WaitAsync`、`NoPermissionWindow(parent)`） | ~14 | 在 `Helpers/`/`Frameworks/` 中被排除的文件里 |
| 未移植服务（`UpdateService`/`UIGFGachaService`/`ScreenCaptureService`/`GameNoticeService`） | ~10 | AppConfig DI 引用；按页面依赖顺序逐个移植或 stub |
| `GamepadController` 缺属性（`GamepadShareButtonMode` 等） | ~12 | stub 补字段即可 |
| 其余 | ~80 | 都是上述三类的引用方 |

## 11. M1-1 第四批：入口与长尾（2026-11-02 夜）

| 项 | 处理 |
|---|---|
| `App.xaml.cs` 单实例（`AppInstance`） | 改文件锁 `~/.local/state/lunarward/lunarward.lock`（D6 之后换 Unix socket 以转发参数） |
| URL 协议（`starward://`） | 暂移除分支，等 `.desktop` + scheme 注册（M2） |
| `Program.cs` WinUI 专用 Main | 去掉 `DISABLE_XAML_GENERATED_MAIN`，交给 Uno 生成入口 |
| 首次运行欢迎向导（`WelcomeWindow` + WebView2） | **Linux 默认直接使用 `~/.local/share/lunarward`**，不弹向导（WebView2 取值不可用，D29） |
| `UpdateService`（Polly/Snap.HPatch/RPC） | stub：返回「无更新」，Linux 自更新走 AUR/AppImage（D5） |
| `UIGFGachaService` / `NoPermissionWindow` | 去掉未用的 Vanara using 后直接纳入编译 |
| `GamepadController` | 扩展 stub：镜像上游公开成员（模式、映射键、状态），设置页可打开 |
| `WineRegistry` | 修正 `line[Index + Length..]` 的运算符优先级 bug |
| `PlayTimeRecordService` | 两处 `App.FindInstanceForKey` 改为进程内记录表（启动器已单实例） |

**进展**：源码级 C# 错误 118 → **54**；XAML 仍 236。剩余集中在 `SystemBackdropHelper`（WinRT interop `As<ISystemBackdropInterop>`）、`BackgroundService`（Win2D 背景）与这两者的引用方。

## 12. M1-1 第五批：背景/窗口/对话框（2026-11-02 深夜）

| 子系统 | Linux 实现 |
|---|---|
| `App.xaml.cs` | 单实例=文件锁；去掉 AppLifecycle/URL 协议分支；纳入编译 |
| `BackgroundService` | `Windows.Storage`/`MediaSource`/`BitmapDecoder` → `System.IO` + 存在性校验；`StorageFile` 参数改 `string` 路径 |
| `SystemBackdropHelper` | WinRT 互操作 → 兼容实现：读 `FallbackColor` 给窗口内容刷背景（真正模糊留 M3） |
| `ImageEx`（Scighost.WinUI） | 新建同命名空间 `Scighost.WinUI.ImageEx.ImageEx` 控件（`Source` + `ImageExOpened`） |
| `CanvasSwapChainPanel`（Win2D） | 同名占位 `Panel`，使 XAML 可编译（渲染 M2 用 SkiaSharp 重写） |
| `FileDialogHelper` | 注册表/Windows Picker → `kdialog`/`zenity`（自动探测，支持单选/多选/保存/目录） |
| `ClipboardHelper` | `wl-copy` → `xclip` → `xsel` 兜底 |
| `DriveHelper` | `GetDriveTypeW` → `/proc/mounts` 判定可移动盘 + `DriveInfo` 可用空间 |
| `HotkeyManager` | 空实现（M2 用 portal GlobalShortcuts 或 XGrabKey） |
| `XamlRootExtension` | `GetWindowHandle()` 返回 0；`SetWindowDragRectangles` no-op |
| `GameNoticeService` | `Registry` → `WineRegistry` |
| `GameSelector` | 去掉未用 Vanara；文件去重从 `FILE_ID_INFO` 改为「大小+时间+路径哈希」 |

**进展**：XAML 236 → **166**；`ImageConvertItem`/`CanvasSwapChainPanel`/`ImageEx` 三类缺失清零。

剩余 166 条的结构：级联失败 22、`Invalid identifier character -` 20（未定位）、`ControlTemplate` 标记扩展 4、`PageBase` 成员 12、HDR 对话框成员 8，其余为零散成员绑定。

## 13. M1-1 第六批：XAML 解析器的三个坑（2026-11-02）

XAML 错误 166 → **120**。

| 现象 | 根因 | 处理 |
|---|---|---|
| 20 条 `UXAML0001: Invalid identifier character -`（**无文件名**，最折磨人） | Uno 的 `x:Bind` 解析器不接受函数实参里的**负数实参**：`{x:Bind local:Page.Fn(IceWeakness, -1)}` | 新增 `Features/GameRecord/ZZZ/ElementWeaknessConstants.cs`（`public const int None = -1`），XAML 里改成 `local:ElementWeaknessConstants.None`；语义不变，10 处 |
| 4 条 `MarkupExtension 'ControlTemplate' is not supported` | 上游 `ImageEx` 是 **Control**（有 ControlTemplate），我最初实现成 `Image` 子类，模板无处可挂 | 重写 `Controls/ImageExCompat.cs`：`ImageEx : Control`，暴露 `Source/PlaceholderSource/PlaceholderStretch/NineGrid/Stretch`，`OnApplyTemplate` 里取 `Image`/`PlaceholderImage` 并切 `Loading/Loaded/Failed/Unloaded` 四个视觉状态 |
| `Property 'PlaceholderStretch' does not exist` | 同上 | 同上 |

**定位手法（可复用）**：`dotnet build -v:n 2>&1 | grep -B4 "Invalid identifier"` —— 无文件名的错误会在相邻行暴露它其实是哪个文件的级联产物。

剩余 120 条 = 约 60 个「成员找不到」，全部集中在**code-behind 尚未纳入编译**的页面上（`MutliSelect`/`SelectUid`/`GachaLanguage` → 抽卡页；`MaxLuminance`/`SceneLuminance`/`VideoBgVolume` → HDR/背景对话框）。修法只有一条：逐页把 code-behind 捞回来。
