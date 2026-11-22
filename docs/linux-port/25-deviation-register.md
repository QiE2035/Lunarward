# 25 · 偏离点总账（Deviation Register）

> 生成日期：2026-11-21　基线：上游 `src/Starward/` = `df8ae07`；移植 `src/Lunarward/` = `b5b75a5`（D94 修复后）。
> 方法：9 组逐文件 `diff -u` + 主代理抽样读码核实（见 `26-misalignment-audit.md`）。2026-11-22 对**全部 265 行的必要性判断逐条复核**，57 处改判，见 §1.1。
> 本文取代 `18-upstream-table.md` 的关键词粗分类；原则见 `18-upstream-deviations.md`，实现状态见 `12-shims-and-gaps.md`。

## 0 · 口径

- **偏离** = 上游存在的文件在移植侧行为/结构不同。三种来源：①同名文件被修改（86 个）；②上游文件被 `Lunarward.csproj` 的 `<Compile Remove>` 排除、由同目录 `*.Linux.cs` 重写（36 个）；③纯新增文件（§4，不计入偏离，但会改变同名文件的调用路径）。
- **必要性**：`must` 不这么写程序就不可用（平台 API 不存在、引擎不支持）；`reasonable` 平台不同但语义等价，或为满足 Linux 用户预期；`avoidable` 与平台无关，可以照上游写却写了别的（可能是顺手重构/记忆偏差，也可能是修上游缺陷但没留痕）。
- **可否回流**：这条改动是否值得、也可能提交给上游（多数是平台专属，回不去）。
- 每条都带行号或成员名；`*.Linux.cs` 的对照对象是去掉 `.Linux` 的同名上游文件。

## 1 · 统计

| 维度 | 数量 |
|---|---|
| 偏离点 | **265** |
| 涉及文件 | 114（同名修改 86 + `*.Linux.cs` 36，有交叉） |
| 类别 | linux 专属实现 68、api 平台 API 46、cut 裁剪 46、engine 引擎能力 43、cosmetic 外观文案 32、eng 工程性 21、bugfix 修上游缺陷 9 |
| 必要性 | reasonable 合理换法 118、must 必需 87、avoidable 可避免 60（2026-11-22 全量复核后） |
| 可回流上游 | 12 |

按模块：

| 模块 | 偏离点 | 疑点(bug 候选) |
|---|---|---|
| launcher | 30 | 10 |
| install | 37 | 11 |
| gamesetting | 27 | 10 |
| shell | 43 | 4 |
| screenshot | 26 | 9 |
| gacha-record | 21 | 8 |
| background-codec | 25 | 6 |
| settings-ui | 24 | 8 |
| services-helpers | 32 | 10 |


### 1.1 必要性复核更正（2026-11-22，57 处）

全量 265 行逐条复核（读两侧真实代码），57 行改判（第一批 120 行 23 处，第二批 145 行 34 处）。下表是全部改判项；条目正文的类别已同步更新，决策以新类别为准。

| # | 文件 · 位置 | 原判 → 新判 | 复核依据（摘要） |
|---|---|---|---|
| 0 | `Features/GameLauncher/GameLauncherSettingDialog.xaml` · 根 Grid Background（第 12 行） | must → **reasonable** | 上游 src/Starward/Features/GameLauncher/GameLauncherSettingDialog.xaml:12 用 CustomAcrylicBrush，移植 :12 改 LunarwardSurfaceBrush。理由中「上游 |
| 1 | `Features/GameLauncher/GameLauncherSettingDialog.xaml` · 第 64 行（图标模板 Glyph） | must → **reasonable** | 上游 :64 Glyph="&#xF012;"（MDL2 私有区），移植 :64 改 "&#xE896;"，两侧同在该处用 <FontIcon>。Uno 拦截 "Segoe Fluent Icons"/"Segoe MDL2 Assets" 族名并用内置 Sy |
| 6 | `Features/GameLauncher/GameLauncherSettingDialog.xaml.cs` · 第 929 行（自定义背景文件传入） | must → **reasonable** | 上游 BackgroundService.cs:233 静态 ChangeCustomBackgroundFileAsync(StorageFile file)，移植 BackgroundService.cs:231 改 (string file)、:237  |
| 24 | `Features/GameLauncher/GameLauncherService.cs` · 第 381-384 行（对应上游第 379-382 行） | must → **avoidable** | 原判必须的两条理由都不成立。(1) rows.json 称 HDR「走注册表写 DWM 覆盖项，Linux 无意义」——事实错误：上游 src/Starward/Features/GameSetting/GameSettingService.cs:249-25 |
| 37 | `Features/GameInstall/GameInstallService.Linux.cs` · L41–42 StartInstallAsync 的 mode 形参；L157 cont | reasonable → **avoidable** | src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs:41 形参 `GameInstallDownloadMode mode = GameInstallDownloadMode.Undef |
| 38 | `Features/GameInstall/GameInstallService.Linux.cs` · L45 StartUpdateAsync、L48 StartPredownloadAsy | reasonable → **avoidable** | 移植侧全部调用点都传非空 installPath：src/Lunarward/Features/GameInstall/InstallGameDialog.Linux.cs:95、PreDownloadDialog.Linux.cs:190、src/Lunar |
| 41 | `Features/GameInstall/GameInstallService.Linux.cs` · L177–215 CountLocalMismatchesAsync | reasonable → **avoidable** | src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs:177 `private static async Task<int> CountLocalMismatchesAsync(...)` |
| 42 | `Features/GameInstall/GameInstallService.Linux.cs` · L218–254 EnsureInstallPathLooksSane | reasonable → **avoidable** | src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs:239-243 `bool hasAnyEntry = Directory.EnumerateFileSystemEntries(pa |
| 43 | `Features/GameInstall/GameInstallService.Linux.cs` · L340–374 Repair 分支 | must → **reasonable** | src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs:342 `string? repairVersion = SophonClient.TryReadLocalVersion(conte |
| 45 | `Features/GameInstall/GameInstallService.Linux.cs` · L484–498 版本标记 | must → **reasonable** | 『must：Linux 无注册表』不成立——上游根本不写注册表，而是 RPC 侧 SetGameConfigIniAsync 写 <InstallPath>/config.ini 的 game_version（src/Starward.RPC/GameInst |
| 47 | `Features/GameInstall/GameInstallService.Linux.cs` · L320–327 #if DEBUG 的下载量上限 | reasonable → **avoidable** | 与平台无关的调试插桩：LUNARWARD_DEV_INSTALL_LIMIT_MB 只在 #if DEBUG 内读取（src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs:321-327） |
| 50 | `Features/GameInstall/GameInstallService.Linux.cs` · 整文件（RPC 宿主） | reasonable → **must** | 上游 RPC 宿主在 Linux 上根本不能构建/运行：src/Starward.RPC/Starward.RPC.csproj:4 <TargetFramework>net10.0-windows</TargetFramework>，并引用 Vanara.P |
| 56 | `Features/GameInstall/InstallGameDialog.Linux.cs` · L114–120 TextBlock_IsTextTrimmedChanged | must → **reasonable** | 上游 InstallGameDialog.xaml.cs:425-431 与移植 L114-120 逐行等价（if (sender.FontSize > 12) sender.FontSize--;），没有任何平台 API 替换；且 Uno 6.7.359 已 |
| 57 | `Features/GameInstall/PreDownloadDialog.Linux.cs` · L107–167 LoadSizeInfoAsync | must → **avoidable** | 上游 LoadSizeInfo 所需 API 在 Linux 侧全部已编译：Features/HoYoPlay/HoYoPlayService.cs:181 GetGamePackageAsync、:253 GetGameBranchAsync、:271 Ge |
| 62 | `Features/GameInstall/PreDownloadDialog.Linux.cs` · L21–77（显式 INotifyPropertyChanged + 手写 setter | reasonable → **avoidable** | 登记给的理由「源生成器在 Uno/Skia 下曾出过刷新问题」与仓库自身事实相反，且手写 setter 与平台无关：同一移植工程里 [INotifyPropertyChanged] / [ObservableProperty] 生成器被大量编译并投入使用——s |
| 67 | `Features/GameSetting/GameSettingPage.xaml` · L2 | must → **reasonable** | 加 NavigationCacheMode 是性能适配，不是「不这么写就不可用」：docs/linux-port/00-decisions.md:562-586（D60）实测切页 Navigate→Navigated 1729 ms，同时明确把根因拆成两半，并 |
| 71 | `Features/GameSetting/GameSettingPage.xaml` · L115-117 | must → **reasonable** | 上游 GameSettingPage.xaml:91 <StackPanel Grid.Row="1" Grid.Column="1" …>，但上游 Grid（L19-23）只有 ColumnDefinitions、无 RowDefinitions（隐式单行） |
| 87 | `Features/GameSetting/GameSettingService.cs` · L279-319 | avoidable → **must** | avoidable 不成立：avoidable 的定义是「与平台无关、可照上游写」，而这段的核心偏离是前缀解析，正是平台强制。Lunarward Features/GameSetting/GameSettingService.cs:291 `string? p |
| 88 | `Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs` · L1-54（整文件；上游同目录 .xaml.cs 343 行 + .xaml 202 行 | reasonable → **must** | reasonable 不成立：被裁掉的能力没有 Linux 等价物，属引擎/平台 API 缺口。上游 src/Starward/Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.cs:46 ` |
| 94 | `App.xaml` · :58 / :63 ContentDialog 与 FlyoutPresenter St | must → **reasonable** | src/Lunarward/App.xaml:44-51 仍然定义了 CustomAcrylicBrush/CustomOverlayAcrylicBrush，:57-64 的 ContentDialog/FlyoutPresenter Style 只是把 B |
| 97 | `AppConfig.Configuration.cs` · :36 新增 CacheDirectory；:50 LinuxCacheHome；:62 | must → **reasonable** | 不是 API 缺失：上游 src/Starward/AppConfig.Configuration.cs:87/:92 走 Environment.GetFolderPath(SpecialFolder.LocalApplicationData) + "Sta |
| 104 | `AppConfig.ServiceProvider.cs` · :97 ScreenCaptureService 未注册；:63 新增 Companio | reasonable → **must** | src/Lunarward/Lunarward.csproj:114 `<Compile Remove="Features/Screenshot/ScreenCaptureService.cs" />`：该类型在 Linux 构建里根本不存在，且其实现依赖 M |
| 116 | `Features/ViewHost/MainWindow.xaml.cs` · :229-276 AppWindow_Closing | must → **reasonable** | 应为 reasonable（平台差异下的 Linux 预期行为），不是 must：托盘缺失并不等于不可唤回——单实例唤起路径 App.xaml.cs:152-158 onShow → :329-334 EnsureMainWindow() 里显式调 m_Mai |
| 125 | `Features/ViewHost/MainView.xaml.cs` · :427 NavigateTo 的页类型放行、:446-459 UpdateNaviga | must → **reasonable** | 上游 src/Starward/Features/ViewHost/MainView.xaml.cs:205-211 的全局页豁免只有一页：else if (page.Name is not nameof(SettingPage) && !CurrentGam |
| 127 | `Features/ViewHost/MainView.xaml.cs` · :566-573 CheckSystemProxy | reasonable → **avoidable** | 平台无关，可照上游写：上游 src/Starward/Features/ViewHost/MainView.xaml.cs:324 的 `if (proxy is not null)` 在 Linux 直接编译运行（HttpClient.DefaultProx |
| 129 | `Features/ViewHost/WelcomeWindow.xaml.cs` · :45 RectInt32 构造；:103-125 InitializeDefaultU | reasonable → **must** | 引擎缺 API（实测）：Uno 的 Windows.Graphics.RectInt32 没有 4 参构造函数 —— 用 csc 引用 src/Lunarward/bin/Debug/net10.0-desktop 的 Uno.dll 编译 `new Rect |
| 142 | `Features/Screenshot/ScreenshotPage.Linux.cs` · L75-111 LoadImages | reasonable → **avoidable** | 上游 src/Starward/Features/Screenshot/ScreenshotPage.xaml.cs:128-192 枚举 backup/in-game/external 三类目录、用 ScreenshotHelper.IsSupportedE |
| 144 | `Features/Screenshot/ScreenshotPage.Linux.cs` · L149-184 打开查看器 | avoidable → **reasonable** | 上游查看器 src/Starward/Features/Screenshot/ImageViewWindow2.xaml.cs 依赖 Win2D 与 Win32 窗口栈（:27-33 using Windows.Storage/Windows.System/W |
| 145 | `Features/Screenshot/ScreenshotPage.Linux.cs` · L188-203 BatchConvert | avoidable → **reasonable** | 上游批量窗口 src/Starward/Features/Screenshot/ImageBatchConvertWindow.xaml.cs 依赖 Win2D CanvasBitmap 与 Windows.Graphics.Imaging/Windows.S |
| 152 | `Features/Screenshot/ImageViewWindow.xaml.cs` · L300 | must → **avoidable** | 这一行改动根本不参与构建：src/Lunarward/Lunarward.csproj:108 `<Compile Remove="Features/Screenshot/ImageViewWindow.xaml.cs" />`（无条件 ItemGroup，t |
| 155 | `Features/Setting/ScreenshotSetting.Linux.cs` · L45-52 OnNavigatedFrom | reasonable → **avoidable** | 上游 src/Starward/Features/Setting/ScreenshotSetting.xaml.cs:209-275 五个 setter 各自即时写 AppConfig（ScreenshotForamt→ScreenCaptureSavedFo |
| 159 | `Features/Setting/ScreenshotSetting.Linux.cs` · L118-129 HotkeyInput_Hotkey* | must → **avoidable** | 上游 src/Starward/Features/Setting/ScreenshotSetting.xaml.cs:59-136 三个处理器只做平台无关的胶水：调 HotkeyManager 的 UnregisterHotkey/DeleteHotkey/R |
| 161 | `Features/Screenshot/ScreenCaptureService.cs` · L405 | must → **avoidable** | 这一行根本不在构建里：src/Lunarward/Lunarward.csproj:114 `<Compile Remove="Features/Screenshot/ScreenCaptureService.cs" />`，上游 src/Starward/F |
| 172 | `Features/GameRecord/GameRecordPage.xaml` · 根 Grid 末尾新增空态层（port:526-541） | reasonable → **avoidable** | 上游 src/Starward/Features/GameRecord/GameRecordPage.xaml:16-18 根 Grid 只有 NavigationView；移植 src/Lunarward/Features/GameRecord/GameRe |
| 173 | `Features/GameRecord/GameRecordPage.xaml.cs` · CurrentRole setter（port:271）、GameRoleList 改显 | reasonable → **avoidable** | 上游 src/Starward/Features/GameRecord/GameRecordPage.xaml.cs:263 `public GameRecordRole? CurrentRole`（仅 AvatarUrl 通知）、:276 `public L |
| 177 | `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml` · 5 处小图标码位（port:299/370/627/995/1197 一带，SegoeF | reasonable → **must** | 上游 src/Starward/Features/GameRecord/ZZZ/ShiyuDefensePage.xaml:299/370/627/995/1197 为 FontFamily="{ThemeResource SegoeFluentIcons}" |
| 179 | `Features/GameRecord/DailyNote/DailyNoteButton.xaml` · 「禁止获取便笺」提示图标（port:310） | reasonable → **must** | 上游 src/Starward/Features/GameRecord/DailyNote/DailyNoteButton.xaml:310 `<FontIcon ... Glyph="&#xE733;" Visibility="{x:Bind IsForbi |
| 184 | `Features/Background/BackgroundViewPage.xaml` · :135-155（ListView 声明 + :145-150 ItemContaine | reasonable → **must** | 引擎硬证据：反射 Uno.WinUI 6.7.359（src/Lunarward/obj/project.assets.json 解析到的版本；dll 在 ~/.nuget/packages/uno.winui/6.7.359/lib/net10.0/Uno. |
| 187 | `Features/Background/BackgroundViewPage.xaml.cs` · :216、:278、:347、:400、:422、:445（六处 OpenUrl.Ope | reasonable → **must** | Windows.System.Launcher 是 WinRT 专有 API，Linux 无后端、调用静默失败：src/Lunarward/Helpers/OpenUrl.cs:1-4 注释『上游到处用 Windows.System.Launcher.Laun |
| 192 | `Features/Background/AppBackground.Linux.cs` · :55-60 字段、:88-140 ctor（Content 手工搭 Grid） | must → **avoidable** | 应改判 avoidable：行内理由『ImageEx 在 Linux 无实现』与仓库事实相反，丢掉的视觉效果也与平台无关。① 移植 src/Lunarward/Features/Background/AppBackground.Linux.cs:88-140  |
| 199 | `Features/Background/AppBackground.Linux.cs` · :70-80 Current 惰性 getter、:377 _disposed、:880 | reasonable → **avoidable** | 上游 Current 是普通自动属性：src/Starward/Features/Background/AppBackground.xaml.cs:35 `public static AppBackground Current { get; private s |
| 204 | `Controls/CachedImage.Linux.cs` · :16-30 类与新 Source DP、:32-62 CornerRadius/Upd | must → **reasonable** | 平台缺口是真的（故不是 avoidable）：上游 src/Starward/Controls/CachedImage.cs:15 `public sealed partial class CachedImage : ImageEx`，Scighost.Win |
| 206 | `Controls/CachedImage.Linux.cs` · :18 静态 HttpClient、:160-169 http 分支、:121-128  | reasonable → **avoidable** | 理由『FileCache 依赖 StorageFile/ApplicationData』与代码不符，网络图的磁盘缓存是平台无关的实现选择：移植侧 src/Lunarward/Helpers/FileCache.cs 与上游 src/Starward/Helpe |
| 208 | `Controls/CachedImage.Linux.cs` · :131-172 LoadAsync 的 value 分支、:87-95 新增 Deco | reasonable → **avoidable** | 『只认 string/Uri、其余 ImageSource 静默变空白』这一限制没有平台约束：上游 src/Starward/Controls/CachedImage.cs:41 `protected override async Task<ImageSour |
| 210 | `Features/Setting/SettingPage.xaml` · :19 根 Grid | must → **reasonable** | 上游根容器确实无底：src/Starward/Features/Setting/SettingPage.xaml:17 `<Grid>`（页面靠 AppBackground 透出壁纸/丙烯）；移植 src/Lunarward/Features/Setting/ |
| 211 | `Features/Setting/SettingPage.xaml` · :21 NavigationView | must → **reasonable** | x:Name 是标准 XAML 能力，Uno 完整支持：上游 src/Starward/Features/Setting/SettingPage.xaml:19 无 x:Name 照常工作。移植 src/Lunarward/Features/Setting/S |
| 212 | `Features/Setting/SettingPage.xaml` · :86 手柄菜单图标 | must → **reasonable** | 纯视觉码位替换，页面功能不受影响，构不成「不这么写 Linux 上不可用」：E9CD 确实存在于 XboxFluent2.ttf（fontTools cmap 校验，family 名 'Xbox Fluent v2'），E7FC 是内置 Symbols 的手柄 |
| 215 | `Features/Setting/SettingPage.xaml.cs` · :36-41 OnLoaded | must → **reasonable** | 这是缓存页（src/Lunarward/Features/Setting/SettingPage.xaml:3 NavigationCacheMode="Required"）的配套修复，不是平台 API 缺失/引擎不支持：上游 src/Starward/Fea |
| 223 | `Features/Setting/DownloadSetting.xaml.cs` · :173-184 SetRateLimiterAsync | reasonable → **avoidable** | 上游 src/Starward/Features/Setting/DownloadSetting.xaml.cs:177-182 `if (RpcService.CheckRpcServerRunning()) { var client = RpcServic |
| 224 | `Features/Setting/FileManageSetting.xaml` · :55 用户数据目录 TextBlock | reasonable → **avoidable** | 上游 src/Starward/Features/Setting/FileManageSetting.xaml:57 `IsTextTrimmedChanged="TextBlock_IsTextTrimmedChanged"`（:58 MaxLines=2） |
| 226 | `Features/Setting/FileManageSetting.xaml.cs` · :204-210 手动备份 | reasonable → **must** | 上游 src/Starward/Features/Setting/FileManageSetting.xaml.cs:207 `new SharpSevenZipCompressor().CompressFiles(archive, file)` 依赖 Sta |
| 231 | `Features/Setting/AboutSetting.xaml` · :22 Logo / :26 名称 / :28 署名 | must → **reasonable** | src/Lunarward/Features/Setting/AboutSetting.xaml:22-28 换成 ms-appx:///Assets/lunarward.png、"Lunarward"、"Lunarward · 基于 Starward（MIT |
| 237 | `Features/PlayTime/PlayTimeButton.xaml.cs` · L63-67 `RefreshPlayTime(GameBiz)` | must → **reasonable** | 应改为 reasonable：这不是平台 API 缺失/引擎不支持，而是 D94 页面缓存（docs/linux-port/00-decisions.md:1359，源自 D60 性能决策 :562）的配套修复。上游 src/Starward/Features |
| 250 | `Helpers/PointerCursor.cs` · L8 类声明 | reasonable → **must** | 实测反证：把 src/Lunarward/Helpers/PointerCursor.cs:8 的 `public partial class PointerCursor` 改回上游写法 `public class PointerCursor`，`dotnet |
| 257 | `Helpers/DataFolder.Linux.cs` · 全文件（L13-76）：`AppFolderName="lunarward"`/`Dat | must → **reasonable** | 原判 must 过强，应为 reasonable（平台差异 + Linux 用户预期，且失败不影响可用性）。判据一：这里没有「平台 API 缺失」。移植 Helpers/DataFolder.Linux.cs:19-30 的 DataHome 只是 Envir |
| 258 | `Helpers/WindowWaitAsync.Linux.cs` · 全文件（L1-30）：`WindowWaitAsyncExtension.WaitAsy | must → **avoidable** | 原判「上游用 Scighost.WinUI 的 Window.WaitAsync() 扩展，Linux 没有该包」不成立，该文件是无调用点的死代码，应判 avoidable。证据一（上游根本没有这个扩展）：上游 src/Starward/Starward.cs |
| 263 | `Features/RPC/RpcService.Linux.cs` · 全文件（L1-43），另含 `RpcServerInfo` 类型 | reasonable → **must** | reclass reasonable→must：上游写法在 Linux 目标编译不过、运行不了，不是「平台差异下语义等价」。证据一（依赖的工程不可能出现在 Linux 构建里）：上游 src/Starward/Features/RPC/RpcService.c |

## 2 · 结论（需要拍板的三件事）

1. **must 87 条**是移植的骨架（X11/Wayland、Uno/Skia 引擎、wine 前缀、进程与会话、文件对话框、托盘/热键），去掉任何一条都会直接失去对应功能，不应回退。
2. **avoidable 60 条**与平台无关，属于「本可以按上游写」。清单见附录 B；建议逐条二选一：改回上游语义，或在本文登记为例外并写明原因。
3. **还可以回流上游的 12 条**多半是上游自身的缺陷修正（清单见附录 C），值得单独提 PR。

## 3 · 逐模块偏离点

### 3.1 启动器与游戏入口（GameLauncher）

移植新增：安装任务状态条、启动预检弹窗、输出面板、游玩时长按钮、背景轮播；并把页面设为缓存（D60/D62/D94）。这是偏离最密集的区域，也是错位 bug 最多的地方（`26` §1 大半在此）。

#### `Features/GameLauncher/GameLauncherSettingDialog.xaml`

- **根 Grid Background（第 12 行）**｜engine 引擎能力｜reasonable 合理换法
  - 上游：Background="{ThemeResource CustomAcrylicBrush}"
  - 移植：Background="{ThemeResource LunarwardSurfaceBrush}"
  - 理由：Uno/Skia 没有 DWM 丙烯，用半透明画刷模拟面板底色；上游画刷键在移植里不存在
- **第 64 行（图标模板 Glyph）**｜cosmetic 外观文案｜reasonable 合理换法
  - 上游：Glyph="&#xF012;"（Segoe MDL2 私有区）
  - 移植：Glyph="&#xE896;"（Segoe Fluent/Symbols 标准码位）
  - 理由：Uno 自带 Symbols 字体会覆盖私有区码位（F012 渲染成空白），换成标准 Fluent 图标
- **第 104-107 行 ColumnDefinitions 200/*；第 110 行 NavigationView(Grid.Column=0, IsPaneOpen=True, PaneDisplayMode=Left, OpenPaneLength=200, IsPaneToggleButtonVisible=False)；第 161-165 行 FlipView_Settings 移到 Grid.Column=1** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：NavigationView 内嵌 FlipView（第 144 行），默认 PaneDisplayMode=Auto，内容是 NavigationView.Content
  - 移植：两列 Grid：左列常驻展开的 NavigationView 当侧栏，右列独立 FlipView
  - 理由：Uno 的 NavigationView 在 Auto/紧凑模式下内容区高度与触发按钮不可控，改为固定 200px 侧栏后导航与内容分离；连带丢掉 FlipView 的导航按钮布局
- **上游第 148-152 行 FlipView.ItemsPanel(VirtualizingStackPanel Orientation=Vertical) 被删除** ｜ engine 引擎能力 ｜ avoidable 可避免
  - 上游：<FlipView.ItemsPanel><ItemsPanelTemplate><VirtualizingStackPanel Orientation="Vertical"/></ItemsPanelTemplate></FlipView.ItemsPanel>
  - 移植：无（默认横向 ItemsPanel，内容超高时无法纵向滚动）
  - 理由：Uno 的 FlipView 纵向排列时翻页手势/选中索引同步失效，改横向；代价是内容超过 480 高的设置页底部不可达
- **第 271、472 行附近（上游）三处 IsTextTrimmedChanged="TextBlock_IsTextTrimmedChanged" 被删除** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：IsTextTrimmedChanged="TextBlock_IsTextTrimmedChanged"（处理函数在 GameLauncherSettingDialog.xaml.cs:1223，按需缩字号）
  - 移植：无绑定（处理函数成为死代码）
  - 理由：Uno 的 IsTextTrimmedChanged 不触发，绑着也无效果；但长路径/长名称现在直接省略号截断，不再自动缩字号

#### `Features/GameLauncher/GameLauncherSettingDialog.xaml.cs`

- **第 343 行（打开安装目录）、第 830 行（打开自定义背景目录）** ｜ api 平台 API ｜ must 必需
  - 上游：await Launcher.LaunchUriAsync(new Uri(path))
  - 移植：Starward.Helpers.OpenUrl.Open(path)
  - 理由：WindowsRuntime Launcher 在 Linux 上无实现/静默失败（L-74），改走 xdg-open 封装
- **第 929 行（自定义背景文件传入）**｜api 平台 API｜reasonable 合理换法
  - 上游：BackgroundService.ChangeCustomBackgroundFileAsync(file) // StorageFile
  - 移植：BackgroundService.ChangeCustomBackgroundFileAsync(file.Path) // string 路径
  - 理由：移植侧 BackgroundService.Linux.cs 的文件选择/存储改用路径字符串，不再有 StorageFile

#### `Features/GameLauncher/GameLauncherPage.xaml`

- **第 3 行 sf:PageBase 属性** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：x:Class=...（无 NavigationCacheMode，每次导航新建页面）
  - 移植：NavigationCacheMode="Required"
  - 理由：移植侧要在切页后复用页面状态（播放时长/背景图请求较贵），改由 OnGameChanged/LoadGameContent 手工清理跨游戏残留（D94）
- **上游 PipsPager_SwitchBackgroundImage（Style=CustomPipsPagerStyle）→ 移植 StackPanel_BackgroundDots（第 22-31 行的 PipsPager 资源键保留但不再使用）** ｜ engine 引擎能力 ｜ must 必需
  - 上游：<PipsPager x:Name="PipsPager_SwitchBackgroundImage" Style="{StaticResource CustomPipsPagerStyle}" SelectedIndex="{x:Bind CurrentBackgroundImageIndex, Mode=TwoWay}"/>
  - 移植：<StackPanel x:Name="StackPanel_BackgroundDots" Orientation="Horizontal" Spacing="2"/>（代码里动态生成 Button）
  - 理由：Uno 的 PipsPager 模板不响应点击/选中（L-69），只能自己用 Button+FontIcon 重建圆点
- **新增 StackPanel_InstallTask（Margin 60,0,16,92 / MaxWidth 460 / Visibility=Collapsed）+ TextBlock_InstallTask + ProgressBar_InstallTask(Maximum=1 Minimum=0)** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：无（安装进度只在 StartGameButton 的进度环里显示）
  - 移植：页面左下角新增安装任务条（文字状态 + 3px 进度条）
  - 理由：Linux 安装/更新在后台长时间运行（wine 下载解压），移植侧要把进度显示在启动器页；Progress_Percent 是 0..1 的 double，故 Maximum=1

#### `Features/GameLauncher/GameLauncherPage.xaml.cs`

- **第 68-90 行 OnLoaded/OnGameChanged；第 99 行起 LoadGameContent；第 80 行 CurrentGameChangedMessage 注册；第 137-150 行 OnCurrentGameChanged** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：OnLoaded 直接做初始化；换游戏靠新建页面实例
  - 移植：OnLoaded + OnGameChanged 都调 LoadGameContent()（清空 GameInstallPath/GameServers/_gameInstallTask/BackgroundImages 等 13 项后重跑上游初始化），并用 CurrentGameChangedMessage 兜底
  - 理由：页面被缓存复用后旧游戏的状态必须手工清干净，否则换游戏会带着上一款游戏的安装路径/服务器/背景图
- **第 653 行 catch LaunchPreflightException；667 ShowPreflightDialogAsync、725 GetPreflightActionText、739 RunPreflightActionAsync、890 ResolveCurrentPrefixAsync** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：StartGameAsync：catch 里只弹一条 toast
  - 移植：预检失败弹 ContentDialog，逐项列 ✓/!/✕ 并给「修复」动作（打开游戏/运行环境设置、打开目录、重建前缀、安装图形组件、查看占用进程）
  - 理由：Linux 启动前要检查 wine/前缀/DXVK 三件套，失败原因必须可自修复，否则用户只看到一个 toast（D73/D84）
- **第 1294 行 PauseIcon** ｜ cosmetic 外观文案 ｜ must 必需
  - 上游：private const string PauseIcon = "\uE62E";
  - 移植：private const string PauseIcon = "\uE769";
  - 理由：Uno Symbols 字体映射差异（E62E 在该字体里缺字形），换成 Fluent 的 Pause 码位
- **第 1305-1400 行 CurrentBackgroundImageIndex(1305，先 Math.Clamp 再 ChangeBackgroundImageIndex+UpdateBackgroundDots)、RebuildBackgroundDots(1327)、UpdateBackgroundDots(1360)** ｜ engine 引擎能力 ｜ must 必需
  - 上游：PipsPager.SelectedIndex 双向绑定 + 手动 _pipsPager.SelectedIndex 赋值
  - 移植：自己维护 Button 列表，点击回写 CurrentBackgroundImageIndex；setter 先 Math.Clamp(value,0,Count-1) 再 ChangeBackgroundImageIndex+UpdateBackgroundDots（前景色取 AccentTextFillColorPrimaryBrush / TextFillColorSecondaryBrush）
  - 理由：替换 PipsPager 后的等价实现，顺带修掉 BackgroundImages 为空/切换中索引越界
- **第 1411-1420 行（DEV 钩子）LUNARWARD_DEV_BG_CYCLE** ｜ eng 工程性 ｜ avoidable 可避免
  - 上游：无
  - 移植：#if DEBUG 下按环境变量自动轮播背景图
  - 理由：调试自动化入口，已在 #if DEBUG 内（硬规则 6），但仍属于可删的插桩，Release 无副作用

#### `Features/GameLauncher/StartGameButton.xaml`

- **第 2 处/第 3 处 Glyph（上游 F012、E917）** ｜ cosmetic 外观文案 ｜ must 必需
  - 上游：Glyph="&#xF012;"、Glyph="&#xE917;"（×2）
  - 移植：Glyph="&#xE896;"、Glyph="&#xE823;"（×2）
  - 理由：同 Symbols 字体码位问题：私有区/旧码位在 Uno 自带字体里缺字形，换标准 Fluent 码位

#### `Features/GameLauncher/StartGameButton.xaml.cs`

- **第 264、271-275、280 行（Downloading 进度计算）** ｜ bugfix 修上游缺陷 ｜ reasonable 合理换法
  - 上游：double progress = (double)finish / total; ProgressPercentText = $"{progress:P1}";
  - 移植：total>0 ? (double)finish/total : 0；chunk 分支写进度取 Progress_WriteTotalBytes>0?...:0；ProgressRingValue=Math.Clamp(...,0,100)；ProgressPercentText=total>0?$...:""
  - 理由：总量尚未知时上游会算出 NaN%/越界进度环（L-65），移植侧加零除与钳制；属于可回流上游的健壮性修复（可回流上游）

#### `Features/GameLauncher/GameBannerAndPost.xaml`

- **第 35-38 行 Grid_BannerContainer 增加 Tapped；第 46-52 行 Image_Banner 增加 DecodePixelWidth=760/Loaded，去掉 Tapped** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：<Grid Name="Grid_BannerContainer" PointerEntered/Exited ...>、<sc:CachedImage Name="Image_Banner" Tapped="Image_Banner_Tapped"/>
  - 移植：点击区域上移到容器并补 Grid_BannerContainer_Tapped、Image_Banner_Loaded(handledEventsToo) 与 DecodePixelWidth=760
  - 理由：Uno 里图片外围的透明留白不命中 Tapped（点 banner 两侧无反应），且 CachedImage 不设 DecodePixelWidth 会按原图解码吃内存
- **第 74 行 Pivot_Posts；第 126-135 行活动行 Grid(MaxWidth=340/Tapped=PostRow_Tapped/Background=Transparent) 与标题 TextBlock** ｜ api 平台 API ｜ must 必需
  - 上游：<Pivot x:Name="Pivot_Posts" .../>、<TextBlock Text="{x:Bind Title}"><Hyperlink NavigateUri="{x:Bind Link}">
  - 移植：Pivot 不变；活动行改成整行可点（MaxWidth=340）的 TextBlock(MaxLines=1/TextTrimming/NoWrap)+PostRow_Tapped
  - 理由：Hyperlink.NavigateUri 在 Uno 走 Launcher.LaunchUriAsync，Linux 静默失败（L-74）；带 Hyperlink 的 TextBlock 也不按宽度省略、会顶出卡片

#### `Features/GameLauncher/GameBannerAndPost.xaml.cs`

- **第 31 行 _contentGameId；第 50-75 行 CurrentGameId 支持字段属性 + ReloadForCurrentGameAsync；第 95-120 行 Loaded/Unloaded** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：public GameId CurrentGameId { get; set; }（换游戏只能重建控件）；Loaded 时无条件 UpdateGameContentAsync
  - 移植：Setter 在 IsLoaded 时触发 ReloadForCurrentGameAsync；Loaded 仅在 _contentGameId != CurrentGameId 或列表为 null 时重取；Unloaded 刻意不清 Banners/PostGroups
  - 理由：页面被缓存后控件不再重建，必须由属性变更驱动重取；Unloaded 清空会让 Uno 的 Pivot 重建容器时抛异常
- **第 204-237 行 UpdateGameContentAsync 的 Pivot 重置（ItemsSource=null → SelectedIndex=-1 → 赋值 → Delay(60) → 再赋值）** ｜ engine 引擎能力 ｜ must 必需
  - 上游：PostGroups = ...; （直接绑定，不重置 Pivot）
  - 移植：先清空 ItemsSource 并把 SelectedIndex 置 -1，等一个布局周期再赋新列表，最后 SelectedIndex=0
  - 理由：Uno Pivot 换 ItemsSource 后复用旧条目容器（换游戏后 tab 内容还是上一款），且 SynchronizeSelectedItem 会拿旧 SelectedIndex 索引子元素抛 ArgumentOutOfRangeException（L-126）
- **第 285-345 行 Image_Banner_Loaded(285)/PostRow_Tapped(296)/Image_Banner_Tapped(313)/Grid_BannerContainer_Tapped(329)；第 420-440 行 OpenGameNoticeWindowAsync** ｜ cut 裁剪 ｜ must 必需
  - 上游：Image_Banner_Tapped 用 Launcher.LaunchUriAsync；OpenGameNoticeWindow 激活 GameNoticeWindow 窗口
  - 移植：统一 OpenUrl.Open(...) 打开系统浏览器（失败弹 warning toast），并取消 async void 事件处理器
  - 理由：公告窗 GameNoticeWindow 在移植里已删除（12-shims-and-gaps 记录为「改为浏览器打开」），且 WindowsRuntime Launcher 在 Linux 不可用（L-74）
- **第 100-120 行 Loaded/Unloaded（GameNoticeWindowClosedMessage 注册/注销被删除）、OnGameNoticeWindowClosed 方法删除；第 376-379 行 DEV 钩子 LUNARWARD_DEV_BANNER_AUTOLINK** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：Loaded 注册 GameNoticeWindowClosedMessage，窗口关闭后刷新公告红点；Unloaded 注销
  - 移植：不再注册该消息（无公告窗可关闭），红点只在 UpdateGameNoticeAlertAsync 时刷新；另有 DEBUG-only 环境变量钩子
  - 理由：公告改为浏览器打开后没有「窗口关闭」事件可听；但红点刷新时机也随之退化（见 Bug）

#### `Features/GameLauncher/GameNoticeService.cs`

- **第 36-59 行 GetGameNoticeUrl→GetGameNoticeUrlAsync、GetLastUid→GetLastUidAsync、GetUidFromRegistry→GetUidFromRegistryAsync** ｜ api 平台 API ｜ must 必需
  - 上游：using Microsoft.Win32; Registry.GetValue(...) 同步读 HKCU\Software\miHoYo\<game>\LastUid
  - 移植：using Starward.Features.GameAccount; WineRegistry.GetValueAsync(key, name, null, ct)（前缀注册表异步读）
  - 理由：Linux 没有注册表 API，UID 在 wine 前缀里；WineRegistry 只有异步重载（Helpers 层同步包装会阻塞 UI 线程，L-127）；nap 直接返回 0 不读注册表（与原逻辑一致）

#### `Features/GameLauncher/GameLauncherService.cs`

- **第 381-384 行（对应上游第 379-382 行）**｜linux 专属实现｜avoidable 可避免
  - 上游：if (gameId.GameBiz.Game is GameBiz.hk4e) GameSettingService.SetGenshinEnableHDR(gameId.GameBiz, AppConfig.EnableGenshinHDR);
  - 移植：整段删除（留注释说明）；StartGameWithCMD 分支加 OperatingSystem.IsWindows() 条件
  - 理由：HDR 走注册表写 DWM 覆盖项，Linux 无意义；且该调用要在游戏进程之前起 wine（冷启动 4 秒卡 UI，L-62/L-127）。cmd 包装是 Windows 提权技巧，Linux 无效
- **第 394-441 行（非 Windows 预检/配方/启动报告）与第 442-464 行（AttachSessionCleanup、LaunchHistoryStore、LastKnownGoodStore、OutputCapture）** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：Process.Start 前无预检；启动后直接转 PlayTimeRecordService
  - 移植：ProfileBinding.EnsureFor→LaunchPreflight.Check(Task.Run)→失败抛 LaunchPreflightException(带 preflight 结果)；成功记录 LaunchReport/LaunchHistoryStore 并挂会话清理与输出捕获
  - 理由：Linux 启动路径依赖 wine/前缀/DXVK，需要可回滚（LastKnownGood）与失败原因留档；预检必须离开 UI 线程（硬规则 10）

#### `Features/GameLauncher/GameLauncherService.Linux.cs`

- **第 34-106 行 PrefixRoot(34)/ResolveWine(46)/ResolvePrefix(83)** ｜ linux 专属实现 ｜ must 必需
  - 上游：无对应实现（Windows 直接跑 exe）
  - 移植：LUNARWARD_WINE → Steam 的 Proton Experimental/9.0/Hotfix files/bin/wine → PATH；LUNARWARD_WINEPREFIX → ~/.wine → 每游戏前缀；前缀根 XDG_DATA_HOME/lunarward/prefix
  - 理由：Linux 上游戏 exe 只能由 wine/Proton 运行，探测顺序要让用户能覆盖（环境变量优先）
- **第 108-140 行 TryLockPrefix(108)/ReleasePrefixLock(128)/RunExitCompanions(141)；第 160-187 行 AttachSessionCleanup(160)** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：无（PlayTimeRecordService 自己按进程名盯）
  - 移植：<prefix>/.lunarward/lock 用 FileShare.None 独占标记 + PlayTimeSession.ActivePrefix；会话结束后释放锁并跑 on_game_exit 伴随启动
  - 理由：wine 下无法可靠按 exe 名找到游戏进程，改用「启动的 wine 进程 + 前缀锁」判定会话存活；伴随启动要在游戏退出后执行
- **第 189-346 行 CreateGameProcessStartInfo(189)、LaunchRecipeBuilder 应用(320)；第 348-391 行 ApplyWineRuntimeEnvironment(348)** ｜ linux 专属实现 ｜ must 必需
  - 上游：ProcessStartInfo 直接用 exe + arg；环境变量只设工作目录
  - 移植：wine/proton 可执行 + WINEPREFIX/WINEDLLPATH/LD_LIBRARY_PATH/STEAM_COMPAT_*/PROTONPATH，配方的 Env 与增强（DXVK/HUD/gamescope）合并后应用，参数交给 LaunchRecipe 组装
  - 理由：这是 Linux 启动的全部实质实现：Proton 需要 STEAM_COMPAT_* 与库路径，配方/增强要能在启动时注入并报告覆盖冲突
- **第 393-486 行 DescribeLaunchPlan(393)/SplitArguments(460)** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：无
  - 移植：为 CLI --launch-plan 生成可复制的命令行（含引号），SplitArguments 处理 ' 与 " 的拆分
  - 理由：Linux 排障需要「不启动游戏也能看到最终命令行」，供 12-shims 文档与用户报障使用

### 3.2 安装 / 更新 / 预下载（GameInstall）

Linux 没有官方启动器提供的安装器，整条 Sophon 管线（清单、chunk、差分、续传、修复）是移植侧新写的；`InstallGameDialog` 由上游 600+ 行缩成 x:Bind 桩，语义丢失集中在这里。

#### `Features/GameInstall/InstallGameDialog.xaml`

- **L1（UTF-8 BOM）；L49–58（TextBlock_InstallationPath 属性换序）** ｜ cosmetic 外观文案 ｜ avoidable 可避免
  - 上游：文件无 BOM；属性顺序为 Text / TextTrimming / TextWrapping / MaxLines / IsTextTrimmedChanged
  - 移植：文件带 BOM；MaxLines 提到 IsTextTrimmedChanged 之前，属性值未改
  - 理由：仅编码与属性书写顺序不同，模板、绑定与命名元素完全一致，渲染与行为无差异

#### `Features/GameInstall/PreDownloadButton.xaml`

- **L157 Glyph；L181 Glyph** ｜ engine 引擎能力 ｜ must 必需
  - 上游：Glyph="&#xF012;"（下载中图标）、Glyph="&#xE917;"（剩余时间图标）
  - 移植：Glyph="&#xE896;"、Glyph="&#xE823;"
  - 理由：Uno 自带 Symbols 字体覆盖了上游私有码位，F012/E917 在 Linux 上渲染为空白/方框；改用字体中确实存在的 E896（下载）/E823（时钟）

#### `Features/GameInstall/PreDownloadDialog.xaml`

- **L48、L57、L66（PackageSizeText / UnzipSpaceText / AvailableSpaceText 的 Run.Text）** ｜ cosmetic 外观文案 ｜ avoidable 可避免
  - 上游：{x:Bind PackageSizeText} 等，依赖根元素 x:DefaultBindMode="OneWay"
  - 移植：{x:Bind PackageSizeText, Mode=OneWay} 等显式写 Mode
  - 理由：根元素 L12 已有 x:DefaultBindMode="OneWay"，三处显式 Mode 属冗余保险；XAML 层面无行为差异（真正的刷新问题在 code-behind 缺 INPC，见 bugs）

#### `Features/GameInstall/PreDownloadButton.xaml.cs`

- **L60（GetButtonIcon 暂停图标）** ｜ engine 引擎能力 ｜ must 必需
  - 上游：case PointerOver && Downloading → return "\uE62E";
  - 移植：return "\uE769";
  - 理由：与 PreDownloadButton.xaml 同因：Uno Symbols 字体缺 E62E 码位，暂停图标改 E769 才能显示
- **L184–185（UpdateGameInstallTask 里的百分比文案）** ｜ bugfix 修上游缺陷 ｜ reasonable 合理换法
  - 上游：ProgressPercentText = $"{(double)finish / total:P1}";（total=0 时为 NaN%）
  - 移植：ProgressPercentText = total > 0 ? $"{(double)finish / total:P1}" : "";
  - 理由：等待/估算阶段 total 尚未算出，上游直接相除得到 NaN 并显示「NaN%」；移植加 total>0 守卫，纯健壮性改动，建议回流上游（可回流上游）

#### `Features/GameInstall/GameInstallService.Linux.cs`

- **L1–6 注释、L22 namespace、L28–34 字段、L139–173 StartAsync（整文件）** ｜ api 平台 API ｜ must 必需
  - 上游：所有安装/更新/修复/卸载/进度都经 gRPC 客户端 GameInstallerClient 调 RPC 子进程（GameInstallService.cs:60–132、239–269）
  - 移植：主进程内直接跑 Sophon 管线：_tasks/_cancellations/_paused 三张表 + Task.Run(RunAsync)
  - 理由：RPC 子进程宿主属 M4 暂缓（见下方 cut 行），Linux 侧必须在主进程内实现；因此类名保留、命名空间改为 Starward.RPC.GameInstall，方法集与上游同名但不走 RPC
- **L102–107 PauseTaskAsync、L110–115 ContinueTaskAsync、L118–136 StopTaskAsync** ｜ api 平台 API ｜ must 必需
  - 上游：PauseTaskAsync/ContinueTaskAsync/StopTaskAsync(GameId) → RPC 后 AddOrUpdateTask 刷新上下文（GameInstallService.cs:273–326）
  - 移植：入参改为 GameInstallContext，只翻 _paused 标志与 context.State；Stop 仅 cts.Cancel()，均返回 Task.CompletedTask
  - 理由：无 RPC 可调，状态机只能在内存里维护；同时导致「取消 vs 失败」的语义位移（见 bugs）
- **L41–42 StartInstallAsync 的 mode 形参；L157 context.DownloadMode = Chunk**｜cut 裁剪｜avoidable 可避免
  - 上游：GameInstallRequest.DownloadMode 由调用方指定（LDIFF/chunk 二选一），RPC 按模式下载
  - 移植：额外接受 GameInstallDownloadMode mode = Undefined 但忽略；一律 GameInstallDownloadMode.Chunk
  - 理由：只接了 chunk 下载，LDIFF 增量未实现；保留形参仅为对齐上游调用点（GameLauncherPage.ResumeDownloadAsync 等），调用方不会因签名不同而编译失败
- **L45 StartUpdateAsync、L48 StartPredownloadAsync、L52 StartRepairAsync**｜api 平台 API｜avoidable 可避免
  - 上游：StartInstallAsync/StartUpdateAsync 等只收 GameId，安装路径由 GameInstallRequest.InstallPath 显式给
  - 移植：三个方法增加 string? installPath，为空时回落 AppConfig.GetGameInstallPath(gameId.GameBiz)
  - 理由：移植侧调用点（设置对话框、CLI、预下载按钮）常常只拿得到 GameId；回落避免调用方重复拼路径，语义等价于上游把 InstallPath 塞进请求
- **L56–99 StartUninstallAsync** ｜ api 平台 API ｜ reasonable 合理换法
  - 上游：发 UninstallGameRequest{InstallPath, UserDataFolder, ScreenshotFolder, GameExeName} 给 RPC（GameInstallService.cs:330–362），由服务端结束进程并按设置决定删哪些目录
  - 移植：Directory.Exists + config.ini 双重守卫，CancellationTokenSource.Cancel，Task.Run(Directory.Delete(recursive))，最后 AppConfig.SetGameInstallPath(null)
  - 理由：Linux 侧没有注册表用户数据目录/截图目录约定，也没有 RPC 端可托付；改为直接删整个安装目录，并加 config.ini 守卫避免把非游戏目录误删（L-66）
- **L294–316 Report()** ｜ bugfix 修上游缺陷 ｜ reasonable 合理换法
  - 上游：下载进度与磁盘写入进度由 RPC 分别上报（Progress_Download* / Progress_Write* 各自独立）
  - 移植：同一份管线进度同时写 Progress_Download* 与 Progress_Write*（百分比、速度均镜像）
  - 理由：移植侧 PreDownloadButton / 启动器页读的是 Write 系列字段，而 InstallPipeline 只产出下载进度，不镜像就显示 0%（L-65）
- **L177–215 CountLocalMismatchesAsync**｜eng 工程性｜avoidable 可避免
  - 上游：无对应检查（RPC 侧按 manifest 校验）
  - 移植：安装结束按 manifest 逐文件比长度，不一致只写日志（Errors 不进 UI）
  - 理由：自研管线的兜底自检：只比文件大小，故意不做 MD5（代价高），仅作诊断线索，不改变状态机
- **L218–254 EnsureInstallPathLooksSane**｜linux 专属实现｜avoidable 可避免
  - 上游：无等价校验：安装目标目录是否已有别的内容由用户/RPC 侧自行承担
  - 移植：Install 且目标目录非空、又缺 config.ini 时抛 InvalidOperationException 并置 Error
  - 理由：L-66 的教训：误把游戏装进已有数据目录会覆盖用户文件；Linux 侧路径由用户手选，风险比 Windows 的默认路径高
- **L340–374 Repair 分支**｜api 平台 API｜reasonable 合理换法
  - 上游：修复请求交给 RPC，由服务端决定按本地版本还是最新版本核验
  - 移植：TryReadLocalVersion → GetBuildInfoAsync(tag: repairVersion) → InstallPipeline.RunResumableAsync；取不到本地版本时只记日志再走兜底
  - 理由：D42 的决定：Linux 侧修复以「本地已装版本」的 manifest 为唯一基准，避免把玩家改过的文件按最新版本重下
- **L376–427 Update/Predownload 的 patch+reuse 分支** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：由 RPC 的 Sophon 流程内部决定是否走补丁/复用
  - 移植：本地存在版本时先跑 UpdateOrchestrator.RunAsync 做补丁合并 + 已存在文件复用，成功则直接转成 InstallPipelineResult，失败再回退整包
  - 理由：移植侧自研的补丁复用编排（docs/linux-port/12-shims-and-gaps.md:57 实测 7.1.0→7.2.0 下载 23.81 GB，省 47%）；上游实现不公开，无法照搬
- **L484–498 版本标记**｜linux 专属实现｜reasonable 合理换法
  - 上游：版本号写入注册表 / GameConfig，由 RPC 服务端维护
  - 移植：GameVersionMarker.WriteAll(installPath, package.Tag)，且预下载与修复不写
  - 理由：Linux 无注册表，改用以安装目录为锚的标记文件；不写预下载/修复是与上游状态语义对齐（这两种操作都不代表版本已就位）
- **L500–525 完成与失败分支** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：上游无桌面通知（RPC 只回状态）
  - 移植：State=Finish 或 catch 时 Helpers.DesktopNotification.Show(安装完成/预下载完成/修复完成/更新完成/任务失败)
  - 理由：Linux 侧没有任务栏进度，长任务结束需要可见反馈；属通用增强，可回流上游（可回流上游）
- **L320–327 #if DEBUG 的下载量上限**｜linux 专属实现｜avoidable 可避免
  - 上游：无对应逻辑
  - 移植：读环境变量 LUNARWARD_DEV_INSTALL_LIMIT_MB 作为 limitBytes，仅 Debug 编译生效
  - 理由：冒烟/离线测试用（硬规则 6：Release 构建里不存在），避免测试真下几十 GB
- **L330 Housekeeping.PruneStaleTempFiles(installPath)** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：临时目录清理由 RPC 端负责
  - 移植：每次任务开始前清理安装目录下的陈旧临时文件
  - 理由：中断的任务会留下 .tmp/chunk 残片，积累几十 GB；启动前扫一遍成本低
- **L526–543 finally 与 _tasks 生命周期（含 GetGameInstallTask 轮询接口）** ｜ api 平台 API ｜ reasonable 合理换法
  - 上游：任务表由 RPC 流式推送 + AddOrUpdateTask/Remove 维护（GameInstallService.cs:190–207）
  - 移植：任务表在 finally 里按 ReferenceEquals 守卫移除；对外只暴露 GetGameInstallTask(gameId) 供 UI 轮询
  - 理由：无推送通道可订阅，UI 改为 1s 轮询（GameLauncherPage.InstallTask.cs:21–56）；引用相等守卫防同一 gameId 的新任务被旧任务清掉（L-65）
- **整文件（RPC 宿主）**｜cut 裁剪｜must 必需
  - 上游：安装/卸载/进度全部运行在 Starward.RPC 子进程里
  - 移植：主进程内联实现，RPC 宿主整体不启用
  - 理由：M4 占位：RPC 子进程与跨进程宿主明确暂不做（与 Overlay/Gamepad/BBS/云游戏/战绩窗同批）；本条只记这一次，能力细节已并入上面的 api 行

#### `Features/GameInstall/InstallGameDialog.Linux.cs`

- **L23 _audioLanguage、L51–57 构造函数** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：[INotifyPropertyChanged]（InstallGameDialog.xaml.cs:25）；Loaded 里 SetDefaultInstallationPath() + GetGamePackageAsync()（L60–62）
  - 移植：普通 C# 类，ctor 只 InitializeComponent + 三个命令；无 Loaded/Unloaded 订阅
  - 理由：只保留了 x:Bind 需要的那一层成员（docs/linux-port/12-shims-and-gaps.md:89 明确记为「只保留 x:Bind 成员」）；默认路径、包体/空间计算、按钮使能校验全部缺失，造成功能异常，见 bugs
- **L23、L43–47** ｜ api 平台 API ｜ avoidable 可避免
  - 上游：[INotifyPropertyChanged] + SetProperty：InstallationPath/ErrorMessage/AvailableSpaceText/UnzipSpaceText/AutomaticallyCreateSubfolderForInstall 全部可通知
  - 移植：普通自动属性（get; set;），类未实现 INotifyPropertyChanged
  - 理由：与 XAML 根元素的 x:DefaultBindMode="OneWay"（InstallGameDialog.xaml:12）直接冲突：OneWay 绑定在无 INPC 时永不刷新，属实现错位而非平台限制
- **L60–81 ChangePathAsync** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：ChangeInstallationPathAsync（L326–344）→ SetInstallationPath（L347–359）里同步 AvailableSpaceBytes = DriveHelper.GetDriveAvailableSpace(path) 并 CheckCanStartInstallation()
  - 移植：只拼子目录、赋 InstallationPath、清 ErrorMessage；不算空间、不做任何校验
  - 理由：上游选目录后的空间提示与可点性判断在 Linux 版整段消失，用户看不到「够不够装」也得不到即时反馈
- **L84–109 StartInstallationAsync** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：成功后 GameLauncherService.ChangeGameInstallPath（L395）→ 发 GameInstallTaskStartedMessage（L396）→ AppConfig.DefaultGameInstallationPath（L397–400）→ Close()
  - 移植：Directory.CreateDirectory + StartInstallAsync(gameId, path, _audioLanguage)，失败置 ErrorMessage，成功弹 toast 后 Hide()
  - 理由：安装路径从不落盘、也不广播任务开始（见 bugs B1/B8）；卸载/更新/启动全都依赖这条路径，属最严重的语义缺口
- **Segmented_SelectLanguage（InstallGameDialog.LinuxAuto.cs 空壳）与 L23 _audioLanguage** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：Segmented_SelectLanguage_SelectionChanged（L366–388）按 zh-cn/en-us/ja-jp/ko-kr 映射 AudioLanguage 标志并重算包体
  - 移植：处理函数是自动生成的空方法体；_audioLanguage 恒为 AudioLanguage.Chinese；XAML 里 Segmented_SelectLanguage Visibility="Collapsed"
  - 理由：M1 阶段只跑中文语音包，选择区藏起来避免误导；但服务端 L282–291 会按 context.AudioLanguage 映射音频字段，因此该常量直接决定下载哪套语音
- **L114–120 TextBlock_IsTextTrimmedChanged**｜cosmetic 外观文案｜reasonable 合理换法
  - 上游：上游同名处理：文本被 Trim 时把 FontSize 从 14 逐级降到 12（上游 L349 附近的 D46 复核结论 1）
  - 移植：同样的 FontSize 递减，阈值/步进一致
  - 理由：Uno 的 TextBlock 在窄对话框里更容易被 Trim，这段是从上游原样搬来并保留的必要适配

#### `Features/GameInstall/PreDownloadDialog.Linux.cs`

- **L107–167 LoadSizeInfoAsync**｜api 平台 API｜avoidable 可避免
  - 上游：GetGamePackageAsync（L95–320 区间）经 GamePackageService/HoYoPlay 取 PreDownload 补丁与 chunk manifest，并用 GetIgnoreMatchingFields 删掉 ResCategoryDir 里 is_delete 的类别
  - 移植：SophonClient.GetBuildInfoAsync(preDownload:true) + TryReadLocalVersion + GetPatchDownloadBytesAsync 取增量大小，退化时用 TotalCompressedBytes/TotalUncompressedBytes；DriveInfo.AvailableFreeSpace 取可用空间
  - 理由：移植侧不引 GamePackageService/HoYoPlay，直接用 Sophon 公开接口按「同一接口 branch=predownload、tag 留空」算大小（13-lessons L-59/L-61：整包 136 GB、补丁 8.34 GB）；代价是忽略列表（is_delete 类别）没有实现
- **L95–104 ResolveInstallPath** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：安装路径为空或本地版本取不到时显示「预下载不可用」并 return，绝不自造路径
  - 移植：InstallationPath → AppConfig.GetGameInstallPath → 兜底 <UserDataFolder>/Games/<GameBiz.Value>，并回写 InstallationPath
  - 理由：兜底把「无安装」当成「可预下载到默认目录」，改变了可预下载的前提条件（见 bugs B5）
- **L127、L184–204（CheckCanPreDownload 缺失）** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：CheckCanPreDownload（L318–336）在包/chunk/patch build、全限定路径、本地版本、包体>0、解压量>0 全部满足时才 Button_StartPredownload.IsEnabled = true
  - 移植：只有 build 为 null 时把按钮置 false；StartPredownloadAsync 前不再做任何校验
  - 理由：上游唯一的使能点被整段裁掉，按钮可点性因此不受任何条件约束（叠加 XAML 的 IsEnabled="False"，行为取决于 Uno ButtonBase 是否按 CanExecute 回填）
- **L32 _audioLanguage；L86–92 ctor 的 Opened 订阅；L184–204 StartPredownloadAsync** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：StartPredownloadAsync 发 GameInstallTaskStartedMessage（L350）后 Close()；语音取自游戏配置 GetAudioLanguageAsync；窗口用 Loaded 触发加载
  - 移植：固定 AudioLanguage.Chinese；发成功/失败 InAppToast 后 Hide()；不发消息；用 Opened 触发 LoadSizeInfoAsync
  - 理由：上游通知 + Loaded 的组合在移植里退化成「toast + 轮询」；语音固定 Chinese 与服务端 MapAudioField 叠加后同样只能下中文语音
- **L170–181 FormatSize** ｜ cosmetic 外观文案 ｜ avoidable 可避免
  - 上游：文本为 {x/GB:F2} GB 之类固定单位
  - 移植：自适应单位 0.## B/KB/MB/GB/TB
  - 理由：单位选择不同，数值口径一致；对 136 GB 的显示反而更省位数，不影响判断
- **L21–77（显式 INotifyPropertyChanged + 手写 setter）**｜api 平台 API｜avoidable 可避免
  - 上游：[INotifyPropertyChanged] 源生成器 + SetProperty
  - 移植：手写 event PropertyChanged、OnPropertyChanged，三个属性 setter 里各调一次
  - 理由：同一工程里源生成器版本的行为在 Uno/Skia 下曾出过刷新问题，手写更可控；对外契约与上游一致（XAML 仍 OneWay 绑定）

#### `Features/GameInstall/GameInstallScanner.Linux.cs`

- **整文件（L38–141 ScanAndApply、L271–305 FindGameDirectory）** ｜ linux 专属实现 ｜ must 必需
  - 上游：无对应文件：安装路径来自注册表 HKCU\Software\miHoYo\HYP\1_1\{biz} 与 HKCU\Software\Cognosphere\HYP\1_0\{biz}（GameSelector.xaml.cs:893–940）
  - 移植：按候选根目录扫盘（深度≤2）+ GameLauncherService.GetGameExeName 命中 + AppConfig.SetGameInstallPath，结果记入 KnownGames
  - 理由：Linux 无注册表，安装路径只能靠目录扫描获得；这是移植新增模块，上游没有可对照实现
- **L145–158 DefaultScanRoots、L162–192 ScanRoots/SetScanRoots、L196–223 24h 缓存、L238–248 LUNARWARD_GAME_ROOTS** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：无等价逻辑（注册表读取是常数开销）
  - 移植：默认扫 $HOME/games、$HOME/Games、$HOME/Games/miHoYo、/opt 与 3 个 Steam 目录；AppConfig.GameScanRoots 可配；24h 内命中缓存直接跳过；环境变量 LUNARWARD_GAME_ROOTS 可加根
  - 理由：扫盘比读注册表贵得多，必须缓存与限根；环境变量是给测试/非常规布局用的（Release 也生效，属用户可用的逃生口）
- **L257–268 NameMatches** ｜ linux 专属实现 ｜ avoidable 可避免
  - 上游：无等价逻辑（注册表路径自带 gameBiz，无需匹配目录名）
  - 移植：目录名小写后需 name == biz 值，或同时 Contains(游戏名) && Contains(服名)
  - 理由：为压误报而设的严格匹配，但「Genshin Impact」「Star Rail」这类最常见目录名不含服名，天然命中不了（见 bugs B9）
- **L101–137 重复占用清理** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：无等价逻辑
  - 移植：同一目录被多个 gameBiz 认领时保留一个，清掉其余 AppConfig 记录
  - 理由：扫描是启发式的，必须自洽；否则多个启动项指向同一 exe，更新/启动互相打架

### 3.3 游戏设置与运行环境（GameSetting / HoYoPlay）

分辨率/语音/FPS/HDR 的读写对象从 Windows 注册表换成 wine 前缀与 `config.ini`；图形信息从 WMI 换成 `/sys/class/drm`。

#### `Features/GameSetting/GameSettingPage.xaml`

- **L2**｜linux 专属实现｜reasonable 合理换法
  - 上游：<sf:PageBase x:Class="Starward.Features.GameSetting.GameSettingPage"
  - 移植：NavigationCacheMode="Required"
  - 理由：Uno 的 Frame 每次 Navigate 都重建页面（D60 实测切换 1729 ms），缓存页面后靠 OnLoaded 重取游戏相关数据；Windows 版没有这个需要。
- **L14** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：<Grid>
  - 移植：<Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">
  - 理由：Linux 无 DWM 丙烯（D61），页面透明会让游戏背景的大字透到表单文字后面，给实底规避。
- **L22-49** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：<ScrollViewer> 内直接是 <Grid Margin="24,20,24,24" ColumnSpacing="24">，无 RowDefinitions
  - 移植：<ScrollViewer x:Name="ScrollViewer_Main"><StackPanel> + <Grid x:Name="Grid_Content"> 新增 4 个 RowDefinition Height="Auto"
  - 理由：为新增 InfoBar 与「运行配置/后启动程序/诊断」三个分区提供行位；第 4 行注释说明诊断区必须独占一行，否则与第 0 行「分辨率」重叠（L3 真机用例 T-UI-31）。
- **L24-33** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：（无）
  - 移植：<InfoBar IsOpen="{x:Bind IsProfileBindingMissing}" Title="运行环境绑定失效"><InfoBar.ActionButton><Button Click="Button_RebindProfile_Click" Content="重新选择运行环境" />
  - 理由：D61：绑定指向已删除的运行方案时必须提示用户而不是只写日志；运行方案是 Linux 专属概念。
- **L115-117**｜cosmetic 外观文案｜reasonable 合理换法
  - 上游：<StackPanel Grid.Row="1" Grid.Column="1" Spacing="12" Visibility="{x:Bind IsGraphicsSettingEnable}">
  - 移植：<StackPanel Grid.Column="1" Spacing="12" Visibility="{x:Bind IsGraphicsSettingEnable}">
  - 理由：上游 Grid 只有一行，Grid.Row=1 被夹回第 0 行；移植加了 RowDefinitions 后不改这一处，图形设置就会和「后启动程序」分区重叠。
- **L170-187** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：（无）
  - 移植：「运行配置」分区：<ComboBox x:Name="ComboBox_Profile" DisplayMemberPath="Name" SelectionChanged="ComboBox_Profile_SelectionChanged" />
  - 理由：按游戏绑定 Wine/Proton 运行方案（D73 / M2），上游没有运行环境与「按游戏绑定」的概念。
- **L189-213** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：（无）
  - 移植：「后启动程序」分区：Button_AddCompanion_Click / Button_OpenCompanionFolder_Click / TextBlock_CompanionEmpty / Panel_Companions
  - 理由：伴随启动（D24/D28）是 Linux 新增模块（游戏退出后跑 wine 原生程序），Windows 版没有对等能力。
- **L214-227** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：（无）
  - 移植：「诊断」分区 + <Button x:Name="Button_Diagnose" Click="Button_Diagnose_Click" Content="运行启动检查" />
  - 理由：D63 §6：把启动预检摊到设置页，用户可手动跑一次；上游预检只在启动流程内部触发。

#### `Features/GameSetting/GameSettingPage.xaml.cs`

- **L33-74** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：（无）
  - 移植：Button_Diagnose_Click：GetGameInstallPath + GetGameExeNameAsync + ProfileBinding.Resolve + LaunchPreflight.Check(...)，用 ContentDialog 逐条列出 PreflightItem
  - 理由：配套「诊断」区的预检入口（D59/D63），上游无运行器/前缀概念，预检项也不同。
- **L90** ｜ cosmetic 外观文案 ｜ avoidable 可避免
  - 上游：（无）
  - 移植：var swNav = System.Diagnostics.Stopwatch.StartNew();（之后从未使用）
  - 理由：导航耗时打点的残留，未输出也未 Stop，属临时代码（硬规则 7），应删除。
- **L92-100、L127-130** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：OnNavigatedTo 直接设置 Emoji 后做 bh3 重映射（L44-62），OnLoaded 直接初始化
  - 移植：前置 if (string.IsNullOrEmpty(CurrentGameBiz.Value)) { Image_Emoji.Source = null; Toast 警告; return; }
  - 理由：干净环境/首启时本页仍会被导航到（L1.5 --selftest-ui），给明确空态；GameBiz 是 record struct（GameBiz.cs:5，Value => _value ?? ""），空值不会 NRE，但下游 ToGame() 无意义。
- **L109-110** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：（无）
  - 移植：RefreshProfileSelector(); RefreshCompanions();
  - 理由：页面被缓存后构造函数只跑一次，运行方案下拉与后启动程序列表必须在每次导航时显式刷新（D60 的副作用）。
- **L131-138** ｜ engine 引擎能力 ｜ must 必需
  - 上游：InitializeResolutionItem();（L68）
  - 移植：try { InitializeResolutionItem(); } catch (Exception ex) { }
  - 理由：Uno 没有 XamlRoot.ContentIslandEnvironment，GetFromWindowId 必抛（D60 决策文档已记录）；代价是分辨率下拉在 Linux 上恒空，用户只能用自定义宽高填分辨率。
- **L286-389（重点 L337-340、L349）** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：三处同步 Registry 读取（GetGameResolutionSetting/GetStarRailFPSIndex/GetGameVoiceLanguageSetting）直接赋属性（L230-268）
  - 移植：await Task.Run(() => new GameSettingSnapshot(Resolution: ..., StarRailFpsIndex: ..., VoiceLanguageIndex: ...))，record 定义 L386-389；之后 DispatcherQueue.TryEnqueue(() => Grid_Content.InvalidateMeasure())
  - 理由：同步 WineRegistry 每次 fork wine reg query（冷 1.1 s），在 UI 线程会把整窗冻住（L-127 / D60 实测 1729→216 ms）；InvalidateMeasure 补 Uno 行高不随子元素变化重算的问题。
- **L314-333、L561-586** ｜ engine 引擎能力 ｜ must 必需
  - 上游：_displayInformation = DisplayInformation.CreateForWindowId(this.XamlRoot.GetAppWindow().Id); AdvancedColorInfoChanged += ...; UpdateHdrState(_displayInformation)（L237-239）
  - 移植：try { XamlRoot.GetAppWindow() → DisplayInformation.CreateForWindowId } catch (Exception) { HDRNotSupported = true; }；UpdateHdrState 内层再 catch { }
  - 理由：Microsoft.Graphics.Display 在 Linux 上不可用，只能按「显示器不支持 HDR」兜底；副作用是原神设置页常亮「当前显示器不支持 HDR」警告条，对用户偏误导（HDR 预览缺能力 ≠ 显示器不支持）。
- **L510-557** ｜ linux 专属实现 ｜ must 必需
  - 上游：ApplySetting 同步调用 SetGameResolutionSetting / SetGameVoiceLanguageSetting / SetStarRailFPSIndex / SetGenshinEnableHDR
  - 移植：ApplySettingAsync：await SetGameResolutionSettingAsync / SetGameVoiceLanguageSettingAsync / SetStarRailFPSIndexAsync / SetGenshinEnableHDRAsync（L529/534/540/545）
  - 理由：写注册表是一次 wine fork，UI 线程同步调用会冻窗（L-127），必须异步。
- **L591-911** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：（无）
  - 移植：后启动程序 #region：RefreshBindingWarning / Button_RebindProfile_Click / RefreshProfileSelector / ComboBox_Profile_SelectionChanged / RefreshCompanions / CreateCompanionRow / ShowCompanionEditorAsync / Button_OpenCompanionFolder_Click(xdg-open)
  - 理由：Linux 新增的绑定 + 伴随启动管理界面；删除条目没有二次确认、xdg-open 在 UI 线程直接 Process.Start（秒回，可接受）属可打磨点。
- **L140-144** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：（无）
  - 移植：if (DevSwitches.Scroll is int offset && offset > 0) ScrollViewer_Main.ChangeView(null, offset, null, true);
  - 理由：Wayland 下 UI 自动化点不到控件（L-36），改用 --scroll 直接定位截图；受 DevSwitches.IsEnabled 控制（Release 需 LUNARWARD_DEV=1），但仍属开发设施（应登记/最终移除）。

#### `Features/GameSetting/GameSettingService.cs`

- **L42-79、L91-131、L133-207、L212-278、L284-319、L321-379（全文件）** ｜ api 平台 API ｜ must 必需
  - 上游：Registry.GetValue / Registry.SetValue（Microsoft.Win32，L39-306 全文件）
  - 移植：WineRegistry.GetValue / GetKeyValuesAsync / SetValueAsync / SetValuesAsync(key, values, [prefix], ct)（写时生成 UTF-16LE BOM 的 .reg 再 wine reg import）
  - 理由：Linux 上没有 Windows 注册表，米哈游的分辨率/语音/FPS/HDR 设置实际存在 wine 前缀的 user.reg 里，只能经 wine reg 或直接解析 user.reg 读写（WineRegistry.cs / WineRegistryFiles.cs）；这是平台 API 缺口，无法回流。
- **L91-131、L171-206、L238-277** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：每个值一次 Registry.SetValue（SetGameResolutionSetting 写 3–4 次、SetStarRailFPSIndex 写 2 次）
  - 移植：拆出 Async 版本，并用一次 reg import 写整批：BuildResolutionSettingValues（L98-131）返回 Dictionary；SetStarRailFPSIndexAsync 把 GraphicsQuality=0 与 Model 合并（L269-277）
  - 理由：每次 wine reg 都是一次 fork（冷 1.1 s / 热 0.13–0.2 s，L-127），合并成一次 import 把 3–4 次 fork 降到 1 次，UI 路径才能用。
- **L279-319**｜linux 专属实现｜must 必需
  - 上游：SetGenshinEnableHDR(biz, enableHDR)：if (biz.Game is GameBiz.hk4e) Registry.SetValue(key, WINDOWS_HDR_ON_h3132281285, enableHDR ? 1 : 0)（L249-255）
  - 移植：SetGenshinEnableHDRAsync(biz, enableHDR, string? winePrefix = null)：prefix = winePrefix ?? ResolveGamePrefix(biz)，ResolveGamePrefix = ProfileBinding.Resolve(biz) + PrefixManager.TryGetExistingPrefix(profile, biz.Value)；取不到前缀就 Log.Information 后静默 return
  - 理由：必须写到游戏真正使用的前缀（上游在 Windows 上无歧义）这点是对的，但「找不到前缀就静默跳过」会让用户以为 HDR 已生效（应回落到显式提示/排队到启动时写），因此不算必须的偏离。

#### `Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs`

- **L1-54（整文件；上游同目录 .xaml.cs 343 行 + .xaml 202 行）**｜cut 裁剪｜must 必需
  - 上游：Win2D HDR 预览：CanvasBitmap 加载 Scene.jxr/Sign.png/UI.jxr、CanvasSwapChain、LinearTransferEffect，DisplayInformation.CreateForWindowId + AdvancedColorInfoChanged 驱动 DrawMora/DrawScene，AutoAdjust 读 MaxLuminanceInNits/SdrWhiteLevelInNits（L30-330）
  - 移植：54 行：保留 CurrentGameBiz/DisplayInfomation/HDRNotSupported/HDRNotEnabled/HDREnabled/MoraVisibility/MaxLuminance/UILuminance/SceneLuminance 与 SaveAsync/Reset/AutoAdjust/Close；XAML 改名 GenshinHDRLuminanceSettingDialog.xaml.disabled
  - 理由：Uno/Skia 无 HDR surface 也无可用的 Win2D 合成（D21/D31 已知缺口），HDR 实时预览无法移植；裁剪本身合理，但 Reset/AutoAdjust 的默认值随之变成 1000/200/400（上游 1000/300/350），且对话框没有 XAML → 成了空壳（见 bugs）。

#### `Features/HoYoPlay/HoYoPlayService.cs`

- **L311-380（GetGPUInfos）、L298-310（调用点）** ｜ api 平台 API ｜ must 必需
  - 上游：Vanara.PInvoke SetupAPI：SetupDiGetClassDevs/SetupDiEnumDeviceInfo/SetupDiGetDeviceRegistryProperty 枚举显示适配器，GetDeviceName 取 DriverDesc、GetDeviceProperty 取 DriverVersion（删除的 2 个私有方法）
  - 移植：读 /sys/class/drm：Directory.EnumerateDirectories(drmRoot, "card[0-9]*") → device/uevent 的 DRIVER=/PCI_ID=，版本读 /sys/module/<driver>/version，Name = $"{driver} ({pciId})"，DriverVersion = version
  - 理由：Linux 没有 SetupAPI；这是给米哈游接口上报设备指纹用的，只能取 sysfs 里的可用信息。设备名从「NVIDIA GeForce RTX ...」变成「amdgpu (0000:03:00.0)」这类驱动+PCI ID，与上游上报值不同（只影响接口按 GPU 返回的 DX 配置）。
- **L168-173** ｜ bugfix 修上游缺陷 ｜ reasonable 合理换法
  - 上游：content = await _client.GetGameContentAsync(...); _memoryCache.Set($"{nameof(GameContent)}_{content.GameId.Id}", content, ...);（L167-169，content 为 null 时直接 NRE）
  - 移植：if (content is null) throw new InvalidOperationException($"米哈游接口没有返回游戏内容（game=..., launcher=..., lang=...）");
  - 理由：接口偶尔不返回 content 节点时上游是 NullReferenceException、日志看不出是哪个请求；换成可读异常，调用方本来就 catch 降级，纯收益，可回流上游。（可回流上游）

#### `Features/GameAccount/GameAccountService.cs`

- **L29-92、L106-137、L174-203** ｜ api 平台 API ｜ must 必需
  - 上游：GetGameAccountFromRegistry/ChangeGameAccount 用 Registry.GetValue/SetValue 逐值读写（L28-73、L137-159）
  - 移植：GetGameAccountFromRegistryAsync（一次 GetKeyValuesAsync 取回整键 + is int / is byte[] 模式匹配）、GetGameAccountsAsync + MergeAccounts、ChangeGameAccountAsync（SetValuesAsync 一次 reg import），同步版本保留为 GetAwaiter().GetResult() 包装
  - 理由：同上：Linux 只能经 wine 前缀注册表读写；AND 票据与 Uid 在同一键，一次读回 + 一次 import 才能避免 UI 线程上 2–4 次 fork。
- **L196-201** ｜ linux 专属实现 ｜ avoidable 可避免
  - 上游：逐条 Registry.SetValue，失败直接抛异常（L146-157）
  - 移植：bool ok = await WineRegistry.SetValuesAsync(...); if (!ok) { _logger.LogWarning("Change account {name} ({biz}) 写注册表失败"); return; }
  - 理由：WineRegistry.SetValuesAsync 内部 catch 掉异常只返回 bool，这里又只记警告不回传给调用方，导致切换账号失败时上层仍按成功处理（无任何用户反馈）；应把失败上抛或返回结果。

#### `Features/GameAccount/GameAccountSwitcher.xaml.cs`

- **L36-38、L73-84、L88-104、L145-155** ｜ linux 专属实现 ｜ must 必需
  - 上游：GameAccountSwitcher_Loaded → UpdateGameAccount()；ChangeGameAccount() / DeleteGameAccount() 都是同步 void（L36-38、L73-99、L145-155）
  - 移植：Loaded 改 async void → await UpdateGameAccountAsync()；三个方法改 async Task（UpdateGameAccountAsync / ChangeGameAccountAsync / DeleteGameAccountAsync），命令名不变（ChangeGameAccountCommand 等由 Async 后缀剥离生成）
  - 理由：GetGameAccounts/ChangeGameAccount 现在会 fork wine reg，同步调用会冻 UI（L-127）；XAML 绑定名与上游一致，无需改 XAML。

### 3.4 外壳：应用、窗口、导航（App / Program / Frameworks / ViewHost）

窗口 chrome 与显隐用 libxcb 重写；托盘、全局热键、单实例、深链、CLI 是移植侧新增的进程级设施。

#### `App.xaml`

- **:58 / :63 ContentDialog 与 FlyoutPresenter Style 的 Background；:67 MenuFlyoutPresenterBackground；:68 MenuFlyoutSeparatorBackground；:88 / :101 ThemeDictionaries 新增 LunarwardSurfaceBrush**｜engine 引擎能力｜reasonable 合理换法
  - 上游：{ThemeResource CustomAcrylicBrush}（丙烯材质，跟随壁纸）
  - 移植：{ThemeResource LunarwardSurfaceBrush}（Light #FFF3F3F3 / Dark #FF202020 SolidColorBrush）；两个 MenuFlyout 键用 <StaticResource ResourceKey="LunarwardSurfaceBrush"/> 强制不透明底
  - 理由：Uno 的 AcrylicBrush 只实现 FallbackColor（约 96% 不透明的深色实底），会把壁纸整体盖住；浮层半透明时下层文字透叠读不清，故用实底近似并在菜单浮层强制不透明。

#### `AppConfig.Common.cs`

- **:26 DeviceId 计算；:31 新增 ReadFirstLine(string path)；:44 新增 ReadMachineId()** ｜ api 平台 API ｜ must 必需
  - 上游：读注册表 HKLM\HARDWARE\DESCRIPTION\System 的 SystemBiosVersion + SOFTWARE\Microsoft\Cryptography 的 MachineGuid，与 MachineName 一起 MD5
  - 移植：MD5.HashData(UTF8($"{ReadFirstLine("/sys/class/dmi/id/bios_version")}{ReadMachineId()}{Environment.MachineName}"))；ReadMachineId 先 /etc/machine-id，退回 UserDataFolder/machine_id 文件，再退 MachineName
  - 理由：Linux 无注册表与 MachineGuid；DMI bios_version + machine-id 是同一语义的稳定指纹，仅用于服务端 X-Sw-Device-Id。

#### `AppConfig.Configuration.cs`

- **:89-95 AppVersion 兜底与 IsAppInRemovableStorage** ｜ api 平台 API ｜ reasonable 合理换法
  - 上游：AppVersion 取程序集 informational version；IsAppInRemovableStorage = DriveHelper.IsDeviceRemovableOrOnUSB(...)
  - 移植：AppVersion 为空时 Assembly.GetName().Version?.ToString() ?? "0.0.0"；IsAppInRemovableStorage 恒 false（注释 M2 用 /proc/mounts 细化）
  - 理由：AppVersion 为空会让 User-Agent 变成非法 header 直接炸 HttpClient（L-26）；可移动介质检测在 Linux 尚未实现，先按固定值处理。（可回流上游）
- **:36 新增 CacheDirectory；:50 LinuxCacheHome；:62 LinuxDataHome；:124 / :129 CacheFolder 取值**｜linux 专属实现｜reasonable 合理换法
  - 上游：CacheFolder = %LocalAppData%\Starward（可移动盘另有分支）；无独立缓存目录，缓存与数据同处 LocalAppData
  - 移植：CacheFolder = Path.Combine(LinuxDataHome, "lunarward")（IsPortable 同样）；新增 CacheDirectory = $XDG_CACHE_HOME/lunarward；LinuxDataHome = XDG_DATA_HOME|~/.local/share，LinuxCacheHome = XDG_CACHE_HOME|~/.cache
  - 理由：Windows 已知文件夹在 Linux 无意义，按 XDG 规范拆分用户数据与纯缓存（D57），缓存可被安全清理。
- **:172-176 首次运行无数据目录分支** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：await new WelcomeWindow(...).WaitAsync() 让用户选择数据目录后再建库
  - 移植：直接用 Path.Combine(LinuxDataHome, "lunarward") + Directory.CreateDirectory + DatabaseService.SetDatabase + LoadConfiguration()
  - 理由：D22：Linux 首次运行不弹向导（WelcomeWindow 在无窗口管理器的裸 X 下容易卡死），默认目录即 XDG 数据目录；代价是 WelcomeWindow 成死代码。
- **:242 IsAdmin** ｜ api 平台 API ｜ must 必需
  - 上游：WindowsIdentity.GetCurrent() + WindowsPrincipal.IsInRole(Administrator)
  - 移植：Environment.IsPrivilegedProcess
  - 理由：.NET 跨平台等价语义（uid 0 / euid 提升判定），无 Windows 身份对象可用，可直接回流上游。（可回流上游）
- **:246 LoadConfigurationFromRegistry→LoadConfigurationFromStore；:272 调用点；:295 SaveConfigurationToRegistry→SaveConfigurationToStore** ｜ api 平台 API ｜ must 必需
  - 上游：读写注册表 HKCU\Software\Starward（Language / UserDataFolder / EnableLoginAuthTicket "1"/"0" / stoken / mid），保存时把路径压成相对 ConfigPath 父目录
  - 移植：LinuxConfig.Get/Set/Save 持久化同名键；保存时不再做路径压缩
  - 理由：Linux 无注册表，改为 JSON 配置存储；不存在「exe 所在目录」语义，路径压缩无意义。
- **:224-225 SetLanguage** ｜ bugfix 修上游缺陷 ｜ reasonable 合理换法
  - 上游：只设置 CultureInfo.CurrentUICulture 与 DefaultThreadCurrentUICulture
  - 移植：额外设置 Starward.Language.Lang.Culture = culture; Starward.Core.Localization.CoreLang.Culture = culture; 并写 Serilog 的 UI language 日志
  - 理由：Uno 下界面线程与非 UI 线程已创建，仅改线程文化时 x:Bind/本地化文案仍用旧文化，需同步设置静态 Culture。（可回流上游）
- **:187-190 CheckEnviromentAsync 的 catch 分支** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：User32.MessageBox 弹窗告知初始化失败后退出
  - 移植：System.Diagnostics.Debug.WriteLine 记录后 Environment.Exit(0)
  - 理由：无 Win32 MessageBox 可用；但静默 Exit(0) 让用户完全看不出失败原因，改用 stderr 或 InAppToast 更合适（当前属可避免的体验缺口）。

#### `AppConfig.ServiceProvider.cs`

- **:92 GameInstallService 注册** ｜ api 平台 API ｜ must 必需
  - 上游：sc.AddSingleton<GameInstallService>()（走注册表/COM 的上游实现）
  - 移植：sc.AddSingleton<Starward.RPC.GameInstall.GameInstallService>()（Features/GameInstall/GameInstallShims.cs 的最小实现）
  - 理由：上游安装服务依赖 Windows 注册表与 COM 卸载信息；移植改注册 shim，真正的安装/更新走 Sophon 新管线。
- **:97 ScreenCaptureService 未注册；:63 新增 CompanionRunner**｜cut 裁剪｜must 必需
  - 上游：sc.AddSingleton<ScreenCaptureService>()（Win2D + Windows.Graphics.Capture 实现）
  - 移植：注释「M2 重写后再注册」，改为注册 CompanionRunner
  - 理由：Win2D / 图形捕获在 Linux 不可用，截图已改走外部工具（ScreenCaptureLinux）；伴随启动是 Linux 新模块。
- **:50 启动日志；:131/:133 与 :154/:156 HttpClient User-Agent** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：日志 "Welcome to Starward v…"；DEBUG UA = Starward.Debug/{AppVersion}
  - 移植：日志 "Welcome to Lunarward v…"；DEBUG UA = Lunarward/{AppVersion}，Release 仍 Starward/{AppVersion}
  - 理由：日志中区分移植版便于排错；Release 保留 Starward UA，避免服务端拒绝未知客户端。

#### `Program.cs`

- **:41 日志目录拼接（App.xaml.cs:43 同改）** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：Path.Combine(Environment.GetFolderPath(LocalApplicationData), "Starward", "log")
  - 移植：Path.Combine(Environment.GetFolderPath(LocalApplicationData), Starward.Helpers.DataFolder.AppFolderName, "log")
  - 理由：目录名统一由 DataFolder.AppFolderName（Linux 为 "lunarward"）提供，避免两处字面量漂移。（可回流上游）

#### `Frameworks/PageBase.cs`

- **:20 新增 ContentGameId 属性；:38 PageEx_Loaded；:44 PageEx_Unloaded；:65-70 OnNavigatedTo；:88 新增 OnGameChanged()** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：每次导航新建页面实例：Loaded 里一次性初始化，Unloaded 里 Loaded -= PageEx_Loaded; Unloaded -= PageEx_Unloaded 解除订阅
  - 移植：新增 protected string? ContentGameId；Loaded 先记 ContentGameId = CurrentGameId?.Id 再 OnLoaded()；Unloaded 不再解除订阅；OnNavigatedTo 里比对 gameId，变化则重设 ContentGameId 并调 OnGameChanged()；新增 protected virtual void OnGameChanged() { }
  - 理由：D94 页面缓存（NavigationCacheMode=Required）后同一实例反复 Loaded/Unloaded，解除订阅会导致第二次进入不再初始化；换游戏也不重建页面，需 OnGameChanged 钩子。

#### `Frameworks/WindowEx.Linux.cs`

- **:13 WindowHandle；:15 UIScale；:18 MainWindowId** ｜ api 平台 API ｜ must 必需
  - 上游：WindowHandle 取 Win32 HWND（WindowNative.GetWindowHandle）；UIScale 取 DPI/96；MainWindowId 取 AppWindow.Id
  - 移植：WindowHandle => IntPtr.Zero；UIScale => Content?.XamlRoot?.RasterizationScale ?? 1d；MainWindowId 沿用基类静态字段
  - 理由：Linux 无 HWND（Uno 的 AppWindow.Id 恒 0x1），窗口显隐/置顶改走 libxcb 的 Helpers.X11Window。
- **:24-64 Show()** ｜ linux 专属实现 ｜ must 必需
  - 上游：base.Show()（内部 SetWindowPos/ShowWindow + SetForegroundWindow）
  - 移植：先 AppWindow.TitleBar.ExtendsContentIntoTitleBar = false；Helpers.X11Window.Show(Title)，失败回退 AppWindow.Show()；Activate()；ClampToScreenInBackground()；map 成功后补发 MainWindowStateChangedMessage{Activate=true}
  - 理由：Uno 的 AppWindow.Show() 只在第一次 map 生效，唤不回已 unmapped 的窗口，也不触发 Activated —— 视频壁纸与激活逻辑都不会恢复（L-84 / D47）。
- **:71-112 ClampToScreenAsync；:115 Hide()；:124 Minimize()；:135 ShowFromMinimized()；:147 CenterInScreen()** ｜ linux 专属实现 ｜ must 必需
  - 上游：ShowWindow(SW_HIDE / SW_MINIMIZE / SW_RESTORE) 与 SetWindowPos 居中；CLAMP 后 SetWindowPos 回屏内
  - 移植：Hide 用 Helpers.X11Window.Hide(Title) 回退 AppWindow.Hide()；Minimize/ShowFromMinimized 走 OverlappedPresenter；CenterInScreen 只 Resize 不摆位，350ms 后按屏幕 90% 夹取并居中，末尾 Helpers.AppIcon.Apply(Title) 补 _NET_WM_ICON
  - 理由：Uno X11 后端的 Hide()/MoveAndResize 是空实现、也不设置窗口图标；需要 libxcb 兜底与延迟夹取（首次 show 前拿不到工作区尺寸）。
- **:160 SetIcon；:162 SetDragRectangles；:168 ShouldAppsUseDarkMode / :172 强调色相关** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：SetIcon 走 WM_SETICON；SetDragRectangles 调 AppWindow.TitleBar.SetDragRectangles；ShouldAppsUseDarkMode 经 SystemUIHelper 读注册表
  - 移植：全部改为 no-op 或恒返回 true
  - 理由：Uno 未实现这些 API 且移植已交还系统标题栏；图标改在窗口 map 后由 AppIcon 补，深色主题恒为 true（Uno X11 只有深色标题栏）。

#### `Features/ViewHost/MainWindow.xaml.cs`

- **:107-112 InitializeMainWindow** ｜ linux 专属实现 ｜ must 必需
  - 上游：Title 取自程序集信息；ExtendsContentIntoTitleBar = true 并以 SetDragRectangles 自绘标题栏
  - 移植：Title = "Lunarward"；AppWindow.TitleBar.ExtendsContentIntoTitleBar = false；IconShowOptions = ShowIconAndSystemMenu、PreferredHeightOption = Tall
  - 理由：Uno 没有自绘标题栏实现（拖拽区与自绘按钮点不动），只能交还 WM 标题栏（D49）；标题同时区分移植版。
- **:34-46 构造函数；:130-147 OnFirstActivated** ｜ linux 专属实现 ｜ must 必需
  - 上游：构造里 CenterInScreen(1200, 676) + SetDragRectangles + WTSRegisterSessionNotification(WindowHandle, 0)
  - 移植：构造保留 CenterInScreen(1200, 676) 与 App.Current.EnsureSystemTray()、SetIcon，删 WTS 注册；首次 Activated 时再按 Content?.XamlRoot?.RasterizationScale 执行 AppWindow.Resize(1200*scale, 676*scale)
  - 理由：首次激活前拿不到 RasterizationScale（L-35），不补一次 Resize 时 1.25/1.5 缩放下窗口偏小；会话通知 API 在 Linux 不存在。
- **:151-197 RunDebugWindowAction** ｜ eng 工程性 ｜ avoidable 可避免
  - 上游：无（窗口动作全靠人手操作）
  - 移植：DevSwitches.WindowAction（minimize / close / gamesetting / backgroundview）经 3000ms 延迟后执行
  - 理由：XWayland 下无法可靠点击应用（L-36），窗口动作只能用启动开关验证；已由 DevSwitches.IsEnabled 门控（Release 需 LUNARWARD_DEV=1，D35）。
- **:205-209 ShowByGamepad 的 User32.SetCursorPos** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：把光标移到窗口中心：User32.SetCursorPos(AppWindow.Position.X + width/2, ...)
  - 移植：保留 CenterInScreen(1200, 676) + base.Show()，删除 SetCursorPos
  - 理由：Linux 下光标归合成器管理，没有等价的 SetCursorPos（D30）；手柄唤起路径本身也是 M4 缺口。
- **:229-276 AppWindow_Closing**｜linux 专属实现｜reasonable 合理换法
  - 上游：先 args.Cancel = true；按 MainWindowCloseOption 走 Hide，或 Close + UnregisterKey + 30s 上限的 AutoBackup 后 App.Current.Exit()
  - 移植：与上游逐行一致，但在 args.Cancel 之前插入：if (option is Hide && !Features.Tray.StatusNotifierTray.Available) { Log.Warning(...); return; }（不拦截关闭 → 随窗口退出）
  - 理由：Linux 托盘可能根本不存在（无 StatusNotifierWatcher），隐藏后没有任何入口可唤回（C1）；宁可退出也不留隐形进程。
- **:280-300 Content_KeyDown 的 Esc 处理** ｜ bugfix 修上游缺陷 ｜ reasonable 合理换法
  - 上游：Esc 直接 Hide() 隐藏窗口
  - 移植：Esc 先 MainContentHost?.TryCloseGameSelector()，返回 true 则只收掉选择层并置 Handled，否则才 Hide()
  - 理由：打开游戏选择层时按 Esc，上游会连带把整个窗口藏起来，而用户意图只是退出选择层；该修复对上流同样成立。（可回流上游）
- **:324-360 OnWindowActivated（替代上游 WindowSubclassProc）** ｜ api 平台 API ｜ must 必需
  - 上游：WindowSubclassProc 处理 WM_ACTIVATE、WM_SYSCOMMAND（屏蔽双击标题栏最大化）、WM_WTSSESSION_CHANGE（锁屏暂停视频）、WM_DEVICECHANGE（可移动盘重扫）、WM_HOTKEY（44444 overlay 或 Show / 44445 截图）
  - 移植：改为 Activated 事件：启动后 3s 内只记时间，之后按 15s 冷却记录激活并执行 playtime/背景暂停等逻辑；无窗口消息子类化
  - 理由：Linux/Uno 无 Win32 窗口子类化；热键改 XQueryKeymap 轮询（Helpers/HotkeyManager.Linux.cs），锁屏与设备插拔暂无等价事件源。

#### `Features/ViewHost/MainView.xaml`

- **:94 抽卡记录入口 FontIcon** ｜ cosmetic 外观文案 ｜ must 必需
  - 上游：<FontIcon Glyph="&#xF4A5;" />
  - 移植：<FontIcon Glyph="&#xE734;" />
  - 理由：Uno 自带 Symbols 字体会覆盖部分码位，F4A5 在 Linux 上显示为空白方块；换一个 Uno 字体实际包含的图标码位。
- **:143-156 FooterMenuItems** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：无（导航项全部属于当前游戏域）
  - 移植：新增 NavigationViewItem_RuntimeEnv（Tag=RuntimeEnvironmentSetting，Glyph E7F8，文案 Lang.MainView_RuntimeEnvironment）放在 footer 齿轮上方
  - 理由：运行环境是 Linux 专属、跨游戏的页面（D80），不属于 SupportedPages 的当前游戏域白名单。

#### `Features/ViewHost/MainView.xaml.cs`

- **:88 新增 CurrentPageType；:95-98 NavigateToGameSettings；:100-126 TryCloseGameSelector；:128-138 NavigateToSettings / NavigateToRuntimeEnvironment** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：无公开的现页类型与外部导航/收起浮层 API（由窗口与子控件直接持有引用）
  - 移植：新增 CurrentPageType / CurrentPageContent、NavigateToGameSettings()、TryCloseGameSelector()（VisualTreeHelper.GetOpenPopupsForXamlRoot 找 popup，调 GameSelector.FullBackgroundVisible/CloseLayer）、NavigateToSettings()、NavigateToRuntimeEnvironment()
  - 理由：托盘菜单、Esc 收层与 L1.5 自检需要从窗口外部驱动导航，上游用不到所以没有这些入口。
- **:151-268 DevSwitches.Page 直达与引脚布局重放** ｜ eng 工程性 ｜ avoidable 可避免
  - 上游：无
  - 移植：按 --page / LUNARWARD_PAGE（home、setting、runtimeenv 等十余种）直接 Frame.Navigate，1500ms 后重放导航；installprogress / predownload / install 另弹对话框
  - 理由：截图与排错靠启动直达页面（L-36，无法稳定点击 Uno 窗口）；全部经 DevSwitches.IsEnabled 门控，Release 需显式 LUNARWARD_DEV=1（D35）。
- **:270-282 自动扫描提示；:141-149 ShowHotkeyUnavailableHintOnce** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：无（Windows 靠注册表发现游戏、靠 RegisterHotKey 注册热键）
  - 移植：非 Windows：KnownGames.Count > 0 且 OneShotHints.TryTake("auto-scan") 时 toast「已自动识别 N 个已安装游戏」；!X11KeyState.Available 且首次时 toast 提示用系统快捷键绑 lunarward --show
  - 理由：扫盘发现游戏与 X11 键盘轮询是 Linux 新增机制（D25），能力受限时必须一次性告知用户（D86 / L-45）。
- **:310-313 GameSelector_CurrentGameChanged** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：只更新 CurrentGameId / CurrentGameFeatureConfig 并调 UpdateNavigationView
  - 移植：追加 WeakReferenceMessenger.Default.Send(new CurrentGameChangedMessage(CurrentGameId))
  - 理由：页面缓存后切换游戏不会重建页面，必须广播让当前页自行刷新（D62 / D94）；上游每次导航新建页面，用不到该消息。
- **:427 NavigateTo 的页类型放行、:446-459 UpdateNavigationView 白名单**｜linux 专属实现｜reasonable 合理换法
  - 上游：SupportedPages 只对 SettingPage 放行，导航栏选中项按页面映射
  - 移植：RuntimeEnvironmentSetting 同样绕过 SupportedPages 放行，并从 footer 的 NavigationViewItem_RuntimeEnv 同步选中项
  - 理由：不放行的话从 footer 进入运行环境页会被静默换回启动器（L-152）；该页是 Linux 新增页。
- **:512-522 CheckUpdateOrShowRecentUpdateContentAsync** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：版本号变化时显示 UpdateWindow 的「更新内容」页
  - 移植：非 Windows：只写 LastAppVersion 后直接 return
  - 理由：D5 下用 AUR/包管理器更新，没有内置更新内容窗（空窗口只剩标题），跳过展示。
- **:566-573 CheckSystemProxy**｜cosmetic 外观文案｜avoidable 可避免
  - 上游：每次启动发现系统代理都弹一次提示
  - 移植：加 Helpers.OneShotHints.TryTake("system-proxy")，只提醒一次
  - 理由：每次开机重复提示同一件已知信息（D86），改为一次性。（可回流上游）

#### `Features/ViewHost/SystemTrayWindow.xaml.cs`

- **:52-55 去边框与 DwmSetWindowAttribute；:65-67 LoadIcon；:77 激活状态判断；:86-104 Show() 摆位** ｜ cut 裁剪 ｜ must 必需
  - 上游：GetWindowLongPtr/SetWindowLong 去边框 + DwmSetWindowAttribute + 从 exe 资源 LoadIcon + 按 Win32 托盘图标位置摆出菜单
  - 移植：前三处改成注释说明的 no-op；trayIcon.ToolTipText = "Lunarward"；args.WindowActivationState.ToString() == "Deactivated"；Show() 用 DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary) 摆到右上角
  - 理由：Linux 托盘改 StatusNotifierItem（Features/Tray/StatusNotifierTray），该类在工作区只剩 App.xaml.cs:174 的 Windows 分支构造点，Linux 上实际是死代码。

#### `Features/ViewHost/WelcomeWindow.xaml.cs`

- **:45 RectInt32 构造；:103-125 InitializeDefaultUserDataFolder**｜cut 裁剪｜must 必需
  - 上游：new RectInt32(0, 0, 100000, (int)(48 * UIScale))；默认目录 %LocalAppData%\Starward（DEBUG）/ MyDocuments\Starward（Release）
  - 移植：RectInt32 对象初始化器写法；DEBUG 走 LocalApplicationData/lunarward，Release 分支仍是 MyDocuments/Starward
  - 理由：D22 之后 WelcomeWindow 全仓无引用（首次运行直接建默认数据目录），保留但不可达；Release 分支的目录名漂移同样是死代码。

#### `Program.Linux.cs`

- **:18-1820 入口重写** ｜ engine 引擎能力 ｜ must 必需
  - 上游：Program.Main（#if DISABLE_XAML_GENERATED_MAIN 包住的 WinUI Main；:24 AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException，:36 把异常与 Data 追加写 AppConfig.LogFile）
  - 移植：新增 internal class LunarwardProgram + [STAThread] public static void Main(string[] args)，尾部 UnoPlatformHostBuilder.Create().App(() => new Starward.App()).UseX11().UseLinuxFrameBuffer().UseWin32().Build(); host.Run();
  - 理由：WinUI 的 Main 由 XAML 生成，Uno 下不存在（上游 Main 被 #if 排除），入口必须自建 HostBuilder 才能装 X11/帧缓冲后端。
- **全文件：--webkit-login / --shell-plan / --launch-plan / --profile-ensure / --screenshot / --update-check / --output-tail / --convert-screenshots / --url-plan / --companion-plan / --companion-run / --register / --unregister / --protocol-status / --preflight / --prefix-template / --prefix-ensure / --graphics-list / --database-backup / --diagnose 等子命令** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：无 CLI（GUI 参数只在 HandleStartupAsync 里被解析）
  - 移植：大量 headless 子命令；无 DISPLAY 且无 WAYLAND_DISPLAY 时 stderr 提示后 exit 3；未知 '-' 首参提示「未知命令或不完整」exit 1；GUI 开关白名单 --hide/--show/--new-instance/--selftest-ui/--page/--subpage/--game/--scroll/--viewer/--profile-editor/--window-action
  - 理由：L2 CLI 冒烟（cli-smoke/cli-extra/lint-cli-help）与无图形环境排错的基础设施（D79 / D30）；上游 Windows 版没有 headless 需求。

#### `App.xaml.cs`

- **:69 HandleStartupAsync 调用顺序；:244 函数体；:290 ExpandUrlArgument；:250 Housekeeping.Run；:266 rpc 分支；:416 FindInstanceForKey** ｜ api 平台 API ｜ must 必需
  - 上游：先处理 starward://test/ 深链（TestUrlProtocolWindow），再 ConfigurationBuilder；starward://startgame/<biz>?install_path= 与 starward://playtime/<biz>?pid= 由 UrlProtocolService.HandleUrlProtocolAsync 处理；rpc 分支 RpcRunner.Run()；FindInstanceForKey 遍历 AppInstance 比对 Key
  - 移植：去掉 test 分支与 UrlProtocolService 调用；ExpandUrlArgument 只认 lunarward://startgame?biz=（只解析 query biz=，返回 ["startgame","--biz",biz]），其它 host 一律原样返回；新增 Helpers.Housekeeping.Run(CacheFolder ?? ~/.local/share/lunarward)；rpc 分支改注释 + Environment.Exit(0)；FindInstanceForKey 恒返回 null；playtime/startgame 保留 LogPlayTimeAsync / StartGameAsync(gameId)
  - 理由：Uno 无 AppInstance/深链激活通道，单实例改自建 Unix socket + 文件锁；install_path 与 playtime 深链没有 Linux 产生者，故未实现；housekeeping 是 Linux 长驻托盘场景的清理需求。
- **:108-116 单实例判定、:385 SingleInstanceLockPath、:389 TryAcquireSingleInstanceLock、:395 TryReleaseSingleInstanceLock、:420 Exit()** ｜ api 平台 API ｜ must 必需
  - 上游：AppInstance.GetCurrent() + FindOrRegisterForKey("main") + RedirectActivationToAsync + AppInstance.Activated 事件；FindInstanceForKey(key) 真实现
  - 移植：bool newInstance = args.Contains("--new-instance"); 否则 TryAcquireSingleInstanceLock()（~/.local/state/lunarward/lunarward.lock 的独占 FileStream），失败则 SingleInstancePipe.NotifyPrimary 成功即 Exit(0)，失败判定陈旧锁 → RemoveStaleSocket + TryRelease + 重取，仍失败 Exit(2)；FindInstanceForKey => null
  - 理由：Uno 的 AppInstance 是 NotImplemented 桩（实测 GetCurrent() 抛 System.NotImplementedException），重定向到主实例的能力只能自建（Unix domain socket + 文件锁）。
- **:169-191 --hide 分支；:229-241 HideAfterShownAsync** ｜ linux 专属实现 ｜ must 必需
  - 上游：if (args.Contains("--hide")) { m_SystemTrayWindow = new SystemTrayWindow(); }（Windows 托盘常驻，主窗口干脆不建）
  - 移植：非 Windows：m_MainWindow = new MainWindow(); Activate(); Task ignoredHide = HideAfterShownAsync(m_MainWindow)（800ms 后才 window.Hide()）；Windows 分支仍建 SystemTrayWindow
  - 理由：Linux 走 StatusNotifierItem 而不是隐藏窗口作为托盘宿主，窗口必须先 map 才能被托盘/--show 正常唤出，故延迟隐藏。
- **:81 ApplyAcrylicFallback（:432 实现）；:89 GameInstallScanner.ScanAndApply；:96 DevSwitches.Game；:63 删 _gcTimer；:110-120 DEBUG ApplyDevAccentAsync** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：无丙烯回退（Windows 真丙烯）；开局不扫盘（靠 GameInstallService 读注册表）；无 DevSwitches.Game；每 60s 触发一次 GC.Collect
  - 移植：非 Windows 时把 CustomAcrylicBrush=#D01C1C20、CustomOverlayAcrylicBrush=#A8101012 换成 SolidColorBrush；调用 GameInstallScanner.ScanAndApply() 扫盘并日志；DevSwitches.Game 可覆盖 SelectedGameBizs/CurrentGameBiz；删除 _gcTimer 与 GC.Collect；DEBUG 下 LUNARWARD_DEV_ACCENT=#RRGGBB 延迟 1.5s 改强调色
  - 理由：Uno 无丙烯与注册表游戏清单，扫盘是 Linux 的替代发现机制（D25）；手工 GC 在 Linux/Skia 下反而造成卡顿（删掉）；DevSwitches 属开发开关（D35）。
- **:364-383 EnsureSystemTray；:343-360 StartTrayAsync** ｜ linux 专属实现 ｜ must 必需
  - 上游：EnsureSystemTray 里 m_SystemTrayWindow ??= new SystemTrayWindow()
  - 移植：非 Windows：_ = StartTrayAsync()（Features.Tray.StatusNotifierTray.StartAsync），失败时 40×250ms 等 MainWindow.Current 后用 InAppToast 警告「当前桌面没有系统托盘，关闭窗口会直接退出应用。」；ProcessExit 时 StatusNotifierTray.Stop()
  - 理由：Linux 托盘是 StatusNotifierItem，需异步注册到 DBus 并处理没有 watcher 的情况；不能像 Windows 那样同步 new 一个窗口。

### 3.5 截图与图库（Screenshot / Codec）

抓屏改走 X11/portal；图库改用 `ItemsControl + WrapPanel`（Uno 无 `ItemsWrapGrid` 等价物），多选与查看器行为随之改变。

#### `Features/Screenshot/ScreenshotPage.xaml`

- **L50-96（GridView → ItemsControl + WrapPanel）** ｜ engine 引擎能力 ｜ must 必需
  - 上游：GridView（IsItemClickEnabled/SelectionChanged/DragItemsStarting，upstream ScreenshotPage.xaml:50-59）+ GroupStyle 按 TimeMonthDay 分组 + ItemsWrapGrid 218×128 + hover 缩放 Storyboard（:216-246）
  - 移植：ItemsControl + tk:WrapPanel 扁平列表，每项 Border Width=218 + 206×116 缩略图，无分组/拖拽/悬停缩放
  - 理由：Uno 6.7 下分组 GridView 不渲染条目（L-48），只能换 ItemsControl；分组信息随之丢失
- **L122-207（ContextFlyout 整块删除）** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：条目 ContextFlyout：信息项 + Open / CopyFile / CopyAsJPG / OpenInExplorer / OpenWithDefault / OpenWith / Delete（upstream ScreenshotPage.xaml:122-207，handler 在 .xaml.cs:463-680）
  - 移植：条目上没有任何 ContextFlyout，只剩 CheckBox + 文件名 TextBlock
  - 理由：打开/删除在 Linux 有等价实现（xdg-open、File.Delete），剪贴板/资源管理器项不适用；M3 未补，回看时可加
- **L2 NavigationCacheMode** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：未设置（默认 Disabled，每次导航新建页面实例，OnLoaded 每次都跑）
  - 移植：NavigationCacheMode="Required"，页面实例跨导航复用
  - 理由：D94 的页面缓存做法；但本页没覆盖 OnLoaded 重载数据，缓存反而让图库变陈旧（见 bug）
- **L81-85（多选 CheckBox）** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：靠 GridView.SelectionMode 切换：MutliSelect 时 Multiple，否则 None（upstream ScreenshotPage.xaml.cs:90-98）
  - 移植：每个条目常驻 CheckBox IsChecked="{x:Bind IsSelected, Mode=TwoWay}"，与 MutliSelect 无关
  - 理由：ItemsControl 没有 SelectionMode；代价是页面上的「多选」开关彻底失去作用（见 bug）

#### `Features/Screenshot/ScreenshotPage.xaml.cs`

- **L221-228** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：string? folder = AppConfig.ScreenshotFolder; if (!Directory.Exists(folder)) folder = Path.Join(AppConfig.UserDataFolder, "Screenshots");
  - 移植：string? folder = ScreenCaptureLinux.DefaultFolder; if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) → 仍回落 UserDataFolder/Screenshots
  - 理由：默认目录与 Linux 真实落盘（~/Pictures/Lunarward）对齐；该文件已被 <Compile Remove>，此处只说明契约，生效实现在 ScreenshotPage.Linux.cs

#### `Features/Screenshot/ScreenshotPage.Linux.cs`

- **L75-111 LoadImages**｜cut 裁剪｜avoidable 可避免
  - 上游：Initialize() 枚举「默认 + 游戏内 + 外部」三类目录，ScreenshotHelper.IsSupportedExtension 认 jpg/png/jxr/webp/heic/avif/jxl，按 CreationTime 倒序（upstream ScreenshotPage.xaml.cs:116-190、ScreenshotHelper.cs:15-22）
  - 移植：只枚举 ScreenCaptureLinux.DefaultFolder 一层，只认 png/jpg/jpeg，按 LastWriteTime 倒序
  - 理由：游戏内截图目录依赖 GameInstall 路径映射；扩展名白名单也少了 4 种（见 bug）
- **L16-33 构造器** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：OnLoaded → await Task.Delay(16); Initialize()（upstream ScreenshotPage.xaml.cs:60-64），每次挂载都重新枚举
  - 移植：在构造函数里 LoadImages()，此后没有任何重载入口（未 override OnLoaded）
  - 理由：PageBase.cs:37-40 每次 Loaded 都调 OnLoaded()，只要 override 就能与上游等价；现在漏了（见 bug）
- **L149-184 打开查看器**｜cut 裁剪｜reasonable 合理换法
  - 上游：GridView IsItemClickEnabled + ItemClick → new ImageViewWindow2(...)（upstream ScreenshotPage.xaml.cs:380）
  - 移植：改为 new ScreenshotViewerWindow(items, index)；但 XAML 里 ItemsControl 没有 IsItemClickEnabled/ItemClick，方法永不触发，仅 DevSwitches.ViewerIndex 路径可达
  - 理由：查看器换成 SkiaSharp 版是 M3 决策；接线漏了（见 bug）
- **L188-203 BatchConvert**｜cut 裁剪｜reasonable 合理换法
  - 上游：OpenImageBatchConvertWindow：MutliSelect ? SelectedItems : []，再 new ImageBatchConvertWindow().Activate(list)（upstream ScreenshotPage.xaml.cs:340-351），窗口里可选格式/命名/覆盖策略并有预览
  - 移植：直接同步调用 ScreenshotConverter.ConvertToJpeg（固定 quality 92、同名 .jpg、不删原图），未选中时把全部截图当目标
  - 理由：批量窗口依赖 ImageSharp/Win2D 已排除；但「未选中=全部」与上游空列表语义相反（见 bug），且同步跑在 UI 线程
- **L126-142 ManageScreenshotFolderCommand** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：new ScreenshotFolderManageDialog { Folders = _folders, CurrentGameId }.ShowAsync()（upstream ScreenshotPage.xaml.cs:355-370），管外部目录/游戏内目录/备份目录
  - 移植：Directory.CreateDirectory(DefaultFolder) + xdg-open 打开该目录；ScreenshotFolderManageDialog 无任何入口
  - 理由：外部截图目录（AppConfig.GetExternalScreenshotFolder）与备份目录概念未移植；对话框保留但成孤儿
- **L19-31 空态** ｜ cosmetic 外观文案 ｜ avoidable 可避免
  - 上游：OnNavigatedTo 按 CurrentGameBiz 设 Image_Emoji（派蒙/帕姆/爱酱/邦布，upstream ScreenshotPage.xaml.cs:45-57），文案走 Lang.ScreenshotPage_ScreenshotFolderDoesNotExist
  - 移植：空态文本在 code-behind 里写死中文「还没有截图。按 Alt+D …」，覆盖了 XAML 上的 Lang 绑定；Image_Emoji 不设图
  - 理由：多语言回退；顺带丢了按游戏换 emoji 的观感

#### `Features/Screenshot/ScreenCapture.Linux.cs`

- **L48-112 CaptureAsync** ｜ engine 引擎能力 ｜ must 必需
  - 上游：ScreenCaptureService.CaptureInternal：只取 RunningGameService.GetLatestActiveGame() 的窗口，Windows.Graphics.Capture + Win2D，HDR/色彩信息一并取（upstream ScreenCaptureService.cs:78-111）
  - 移植：依次尝试 spectacle -b -n -f -o / grim / gnome-screenshot 全屏截图，都失败再退 xdg-desktop-portal；任意时刻、任意前台内容都能截
  - 理由：Linux 无 Windows.Graphics.Capture，也没有「游戏窗口归属」信息，全屏外部工具是唯一通用路径
- **L60 文件命名** ｜ linux 专属实现 ｜ avoidable 可避免
  - 上游：$"{runningGame.Process.ProcessName}_{captureItem.FrameTime:yyyyMMdd_HHmmssff}.{extension}"（upstream ScreenCaptureService.cs:316）
  - 移植：$"Lunarward_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"
  - 理由：新命名不被 ScreenshotItem.TryParseCreationTime 识别（它认 yyyyMMdd_HHmmssff 与 yyyy-MM-dd-HH-mm-ss），CreationTime 退回文件系统时间，排序/时间显示只能靠 mtime
- **L60-108 格式 / 质量 / 剪贴板** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：扩展名由 (HDR, ScreenCaptureSavedFormat) 决定 png/avif/jxl，编码质量由 ScreenCaptureEncodeQuality 映射（upstream ScreenCaptureService.cs:309-360）；AutoCopyScreenshotToClipboard 为真才写剪贴板（:420-430）
  - 移植：恒 PNG；不读 ScreenCaptureSavedFormat/EncodeQuality；从不写剪贴板
  - 理由：外部工具只出 PNG，要别的格式得再编码一次；HDR 元数据在 Wayland 会话下拿不到。剪贴板属于可补项
- **L74-108 反馈** ｜ engine 引擎能力 ｜ must 必需
  - 上游：ScreenCaptureInfoWindow 覆盖层展示缩略图/成功/失败，失败给可读原因（upstream ScreenCaptureService.cs:96-108）
  - 移植：DesktopNotification.Show + 调用方 InAppToast（测试截图时两者同时出现）
  - 理由：覆盖层依赖 Win2D 抓到的帧；Linux 侧只有文件路径可展示

#### `Features/Screenshot/ImageViewWindow.xaml.cs`

- **L300**｜api 平台 API｜avoidable 可避免
  - 上游：await Launcher.LaunchUriAsync(new Uri(CurrentImage.FilePath));
  - 移植：Starward.Helpers.OpenUrl.Open(CurrentImage.FilePath);
  - 理由：Uno 的 Launcher.LaunchUriAsync 在 X11 后端是空实现；该文件已 <Compile Remove>，改动不参与构建（生效实现是 ImageViewWindow.Linux.cs 的空壳）

#### `Features/Screenshot/ImageViewWindow.Linux.cs`

- **L16-43** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：ImageViewWindow.xaml.cs 完整查看器：CurrentImage/ImageCollection 绑定、缩放/旋转/复制/另存/全屏/键盘翻页（~550 行，:125-550）
  - 移植：ImageFile 容器（FilePath/Width/Height）+ 8 个空 RelayCommand（Close/CopyImage/FullScreen/OpenFile/Rotate/ZoomIn/ZoomOut），CurrentImage 只存路径
  - 理由：M1 只补 x:Bind 契约；M3 的看图能力改由 ScreenshotViewerWindow 承担，这个窗口在 Linux 上无人实例化

#### `Features/Screenshot/ImageConvertItem.Linux.cs`

- **L6-13** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：ImageConvertItem：SourceFilePath/SourceFileName/SourceExtension/SourceFileSize/SourceFileSizeText/SourceFileTime/Converting/ConvertSuccess/ConvertError/Output*/FileDelta*/ErrorMessage + ctor(string filePath)（ImageBatchConvertWindow.xaml.cs:853-909）
  - 移植：只剩 FileName / FilePath / IsSelected 三个自动属性，无构造函数、无实例化点
  - 理由：唯一消费者 ImageBatchConvertWindow.xaml.cs 已 <Compile Remove>，该文件是死代码，可以整份删掉

#### `Features/Setting/ScreenshotSetting.Linux.cs`

- **L45-52 OnNavigatedFrom**｜cut 裁剪｜avoidable 可避免
  - 上游：每个属性 setter 内即时写 AppConfig（ScreenshotForamt/ScreenshotQuality/AutoConvertScreenshotToSDR/AutoCopyScreenshotToClipboard/EnableScreenshotColorManagement，upstream ScreenshotSetting.xaml.cs:209-270）
  - 移植：只在离开页面时写 ScreenshotFolder 与 AutoCopyScreenshotToClipboard；格式/质量/SDR/色彩管理不落盘
  - 理由：格式/质量只有 DXGI 捕获路径消费（D31），SDR/色彩管理在 Linux 无实现；但 XAML 仍展示这些项，用户改完等于没改
- **L28 ScreenshotQualityVisibility** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：public Visibility ScreenshotQualityVisibility => ScreenshotForamt > 0 ? Visibility.Visible : Visibility.Collapsed;（upstream ScreenshotSetting.xaml.cs:236）
  - 移植：public bool ScreenshotQualityVisibility => true;（恒显示）
  - 理由：格式不生效，质量档位隐藏与否没有实际意义；但 PNG 档下仍显示「质量」与上游不一致
- **L84-100 ClearThumbnailCache** ｜ linux 专属实现 ｜ avoidable 可避免
  - 上游：ImageThumbnail.ClearThumbnailCache()（upstream ScreenshotSetting.xaml.cs:431-441 → ImageThumbnail.cs:292，目录 <CacheFolder>/thumb）
  - 移植：删 Path.Combine(AppConfig.CacheFolder ?? TempPath, "screenshot-thumb")，然后无条件 toast「已清理」
  - 理由：D57 后缓存迁到 AppConfig.CacheDirectory、缩略图缓存叫 thumb，两处名字与根目录都对不上（见 bug）
- **L33-37 构造 / L40-42 EffectiveFolder** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：InitializeScreenshotFolder()：AppConfig.ScreenshotFolder 不存在时回填 <UserData>/Screenshots 并创建（upstream ScreenshotSetting.xaml.cs:150-167）
  - 移植：构造只读 AppConfig，未配置时 ScreenshotFolder 留空（XAML 显示空路径），实际目录靠 EffectiveFolder 兜底到 ~/Pictures/Lunarward
  - 理由：默认目录改为与 ScreenCaptureLinux 一致；页面上看不到默认路径是个小回退
- **L118-129 HotkeyInput_Hotkey***｜api 平台 API｜avoidable 可避免
  - 上游：真正 Unregister/Register/Delete 全局热键并回写 HotkeyInput 状态（upstream ScreenshotSetting.xaml.cs:88-140）
  - 移植：三个空处理，热键改由 HotkeyManager.Linux 的 X11 轮询注册，控件只做展示
  - 理由：RegisterHotKey 是 Win32 API，Wayland 下也不可用（X11 轮询方案见 D61）

#### `Features/Screenshot/ScreenshotFolderManageDialog.xaml`

- **L100** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：Glyph="&#xF413;"
  - 移植：Glyph="&#xE74E;"
  - 理由：Uno 自带 Symbols 字体会覆盖部分码位，F413 显示为空；且该对话框在 Linux 上没有入口

#### `Features/Screenshot/ScreenCaptureService.cs`

- **L405**｜api 平台 API｜avoidable 可避免
  - 上游：(_, outputMaxLuminance, _) = GameSettingService.GetGenshinHDRLuminance(runningGame.GameBiz);
  - 移植：(_, outputMaxLuminance, _) = await GameSettingService.GetGenshinHDRLuminanceAsync(runningGame.GameBiz).ConfigureAwait(false);
  - 理由：把同步 GameSetting 读取换成现成的 Async 版（避免捕获线程上的同步等待）；但该文件已 <Compile Remove>，在当前 Linux 构建里不生效（可回流上游）

#### `Features/Codec/ImageThumbnail.cs`

- **L31** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：private static readonly string CacheFolder = Path.Combine(AppConfig.CacheFolder, "thumb");
  - 移植：private static string CacheFolder => Path.Combine(AppConfig.CacheDirectory, "thumb");
  - 理由：D57 把缓存与数据目录分开；改成惰性属性避免静态构造早于配置加载。文件已 <Compile Remove>，缩略图管线当前不参与构建

### 3.6 抽卡 / 战绩 / 自助查询（Gacha / GameRecord / SelfQuery）

网络层与文件搬运是平台无关的复制，但页面里的 `x:Load`/`Visibility` 开关与上游写法不同，跨游戏切换时出现残留（见 `26`）。

#### `Features/Gacha/GachaLogPage.xaml`

- **page 根元素（第 3 行，唯一 hunk）** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：<Page ...> 无任何缓存属性（全仓 NavigationCacheMode 0 处）
  - 移植：NavigationCacheMode="Required"
  - 理由：Uno 的 Frame.Navigate 默认不缓存页面，D60 给 8 个重页加缓存把启动器页 ctor 289–425ms 压到 24–46ms；代价是页面实例常驻，上游「每次导航新建实例 ⇒ OnNavigatedTo 里的置位就是干净初始值」这一隐含前提失效（见 bugs 第 1/2/3 条）。

#### `Features/Gacha/GachaLogPage.xaml.cs`

- **UpdateGachaLogMessage 处理（port:146 / upstream:146，唯一 hunk）** ｜ api 平台 API ｜ must 必需
  - 上游：User32.SetForegroundWindow((nint)this.XamlRoot.ContentIslandEnvironment.AppWindowId.Value);
  - 移植：// Lunarward：SetForegroundWindow 在 Linux 无对应物，窗口激活交给合成器（D30）。
  - 理由：Linux 无 SetForegroundWindow，也没有 AppWindowId 语义；把窗口前置交给合成器（D30）。残留的 `using Vanara.PInvoke;`（:26）已无引用，属清理项，不计偏离。

#### `Features/Gacha/GachaLogExportFile.cs`

- **ExportApp 初值（port:39 / upstream:39）** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：ExportApp = "Starward"
  - 移植：ExportApp = "Lunarward"
  - 理由：[JsonPropertyName("export_app")] 是 UIGF 导出文件的宿主标识，写移植名避免用户把导出的 json 误当成 Windows 版产出；纯标识差异，不影响导入导出语义。

#### `Features/Gacha/UIGF/UIGF3File.cs`

- **ExportApp 初值（port:39）** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：ExportApp = "Starward"
  - 移植：ExportApp = "Lunarward"
  - 理由：同 GachaLogExportFile：UIGF3 的 export_app 标识改为移植名。

#### `Features/Gacha/UIGF/UIGF4File.cs`

- **UIGF4FileInfo 构造函数内（port:77）** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：ExportApp = "Starward"
  - 移植：ExportApp = "Lunarward"
  - 理由：UIGF4 的 export_app 标识改为移植名。

#### `Features/Gacha/ZZZGachaService.cs`

- **嵌套导出模型属性（port:550）** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：public string export_app { get; set; } = "Starward"
  - 移植：public string export_app { get; set; } = "Lunarward"
  - 理由：绝区零导出文件的 export_app 标识改为移植名。

#### `Features/Gacha/GenshinBeyondGachaPage.xaml`

- **page 根元素（第 3 行，唯一 hunk）** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：无 NavigationCacheMode
  - 移植：NavigationCacheMode="Required"
  - 理由：同 D60 的缓存策略（该页 .xaml.cs 与上游 0 差异）；实例常驻后 hk4e_cn↔hk4e_global 切区服不会再走「新建实例 + OnLoaded 重新 Initialize」，见 bugs 第 6 条。

#### `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs`

- **打开抽卡记录保存目录（port:463 / upstream:463）** ｜ api 平台 API ｜ must 必需
  - 上游：await Launcher.LaunchUriAsync(new Uri(folder));
  - 移植：Starward.Helpers.OpenUrl.Open(folder);
  - 理由：Uno Linux 上 Launcher.LaunchUriAsync 打开本地目录不可靠（且没有 Windows 的 ShellExecute），统一走 Helpers/OpenUrl.cs：portal → xdg-open → gio open 三级回退；返回的 bool 被丢弃，与上游一样不产生用户可见差异。

#### `Features/GameRecord/GameRecordPage.xaml`

- **page 根元素（第 3 行）** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：<Page ...> 无缓存属性
  - 移植：NavigationCacheMode="Required"
  - 理由：D60 缓存策略的一部分（战绩页重、含 NavigationView + 内嵌 Frame）；副作用是 OnLoaded 注册的消息与游戏相关状态不再随实例销毁复位，见 bugs 第 4 条。
- **根 Grid 末尾新增空态层（port:526-541）**｜cosmetic 外观文案｜avoidable 可避免
  - 上游：根 Grid 只有 <NavigationView Name="NavigationView_Toolbox"> 一个子元素
  - 移植：<Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}" Visibility="{x:Bind ShowRecordEmptyState}"> 内含 FontIcon E9D9 + Lunarward_Record_Empty_Title/Description + Button Command="{x:Bind WebLoginCommand}"
  - 理由：D61 #7：上游未登录时内容区只有一片透背景的白，看起来像坏页；移植补空态（图标 + 说明 + 登录按钮）。空态层与 NavigationView 同级且无行列约束，铺满整页（含左侧工具箱导航），见 bugs 第 8 条。

#### `Features/GameRecord/GameRecordPage.xaml.cs`

- **CurrentRole setter（port:271）、GameRoleList 改显式 get/set（port:277-285）、新增 ShowRecordEmptyState（port:289）**｜cosmetic 外观文案｜avoidable 可避免
  - 上游：public List<GameRecordRole> GameRoleList { get; set => SetProperty(ref field, value); }（upstream:276），无 ShowRecordEmptyState
  - 移植：OnPropertyChanged(nameof(ShowRecordEmptyState)); … public bool ShowRecordEmptyState => CurrentRole is null || GameRoleList is null || GameRoleList.Count == 0;
  - 理由：空态层的唯一数据源；L-110 实测本项目 x:Bind bool→Visibility 在 OnPropertyChanged 后生效（该页 x:DefaultBindMode="OneWay"，:14），因此必须在两个源 setter 里额外通知派生属性。除这三处外与上游 0 差异。

#### `Features/GameRecord/LoginPage.xaml`

- **Grid.Row=1 的内容层（port:98-125）** ｜ linux 专属实现 ｜ must 必需
  - 上游：<Border Grid.Row="1"><WebView2 Name="webview" DefaultBackgroundColor="#303030"> 内嵌浏览器承载登录页
  - 移植：<Grid Grid.Row="1"> + WebView2 Visibility="Collapsed" + 叠加 StackPanel（Lunarward_Login_Title / 硬编码中文说明 :118 / Button_OpenLoginWindow）
  - 理由：D29/D32：Uno Linux 的 WebView2 后端没有 CoreWebView2.CookieManager（拿不到 cookie 就调不了 GameRecordClient），登录改由独立 WebKitGTK 窗口完成；页面里保留 WebView2 仅供 XAML 结构兼容（Collapsed，不使用）。说明文案未入 Lang（只有标题/按钮两个键），属未本地化的遗留小项。

#### `Features/GameRecord/LoginPage.Linux.cs`

- **整文件重写（103 行）vs upstream LoginPage.xaml.cs（268 行，csproj:100 已 <Compile Remove>）** ｜ linux 专属实现 ｜ must 必需
  - 上游：OnLoaded→InitializeCoreWebView()：EnsureCoreWebView2Async + CookieManager.GetCookiesAsync/DeleteCookie 清旧态 + Navigate + 注入 JS 自动点开登录弹窗；Button_Finish_Click 用 GetCookiesAsync(GetGameBizUrl()) 按 x.Name/x.Value 拼 cookie；URL_CN="https://www.miyoushe.com/"、URL_OS="https://www.hoyolab.com/home"，URL 按 GameBiz 拼 bh3|ys|sr|zzz
  - 移植：Button_OpenLoginWindow_Click：await WebLoginService.LoginAsync(GetGameBizUrl(), 5min) → Current.Cookies.Cookies 拼 "k=v;k=v" → AddRecordUserAsync → AddGameRolesAsync → Send(GameRecordRoleChangedMessage)，状态写 TextBlock_LoginStatus；GetGameBizUrl() 只按 IsChinaServer 返回 "https://user.miyoushe.com/" 或 "https://www.hoyolab.com/"
  - 理由：WebView2 脚本桥在 Linux 不可用，改为「外部 WebKitGTK 助手窗取 cookie → 回填」，D29/D76（WebKitLoginHost，无 webkitgtk-6.0 时退回 python3 助手）；页面内的 GoBack/GoForward/Refresh/Uri 输入与「完成」按钮退化为提示刷新（LINT-ALLOW 空实现），因为移植版没有页内浏览器。代价：不再按游戏跳对应社区页、也没有 JS 自动点开登录弹窗，改由用户中心统一登录；Button_Finish_Click 只刷新提示文本（上游在这里 LoadGameRolesAsync + 提示已添加角色数）。

#### `Features/GameRecord/StarRail/SimulatedUniversePage.xaml`

- **模拟宇宙 buff 列表（port:367-405，第二处 hunk）** ｜ engine 引擎能力 ｜ must 必需
  - 上游：<ItemsRepeater ItemsSource="{x:Bind Items}"><ItemsRepeater.Layout><cc:WrapLayout HorizontalSpacing="8" Orientation="Horizontal" VerticalSpacing="4"/>
  - 移植：<ItemsControl ItemsSource="{x:Bind Items}"><ItemsPanelTemplate><cc:WrapPanel HorizontalSpacing="8" Orientation="Horizontal" VerticalSpacing="4"/>
  - 理由：Uno 的 ItemsRepeater 不调用自定义（非虚拟化）Layout 的 Measure/Arrange，WrapLayout 不生效；换成 ItemsControl + WrapPanel 得到同样的换行排版（12-shims-and-gaps.md 有登记）。项模板内容与 x:DataType 未改，属引擎软差异。

#### `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml`

- **5 处小图标码位（port:299/370/627/995/1197 一带，SegoeFluentIcons FontSize=10）**｜engine 引擎能力｜must 必需
  - 上游：Glyph="&#xF08F;"
  - 移植：Glyph="&#xE768;"
  - 理由：推断：本项目字体回退链下 F08F 渲染为空白/错字形（Uno 自带 Symbols 字体会覆盖部分码位），换成同为「禁止/锁定」语义的 E768；纯视觉，不影响 ElementWeaknessVisibility 判定。
- **10 处 x:Bind 实参（port:439/442/445/448/451 与 696/699/702/705/708 一带）** ｜ engine 引擎能力 ｜ must 必需
  - 上游：local:ShiyuDefensePage.ElementWeaknessVisibility(<X>Weakness, -1)
  - 移植：local:ShiyuDefensePage.ElementWeaknessVisibility(<X>Weakness, local:ElementWeaknessConstants.None)
  - 理由：Uno 的 x:Bind 解析器不接受负数实参（`f(x, -1)` → UXAML0001 "Invalid identifier character -"），新增 const int None = -1 替代；函数体两侧一致（`value == weakOrResist ? Visible : Collapsed`），语义等价。

#### `Features/GameRecord/DailyNote/DailyNoteButton.xaml`

- **「禁止获取便笺」提示图标（port:310）**｜engine 引擎能力｜must 必需
  - 上游：Glyph="&#xE733;"
  - 移植：Glyph="&#xF140;"
  - 理由：推断：E733 在本项目字体回退下不显示（Uno Symbols 字体覆盖），换成同为「禁止」形状的 F140；只在 IsForbid 时出现的小图标，无功能影响。
- **满体力时间格式（port:406）** ｜ bugfix 修上游缺陷 ｜ avoidable 可避免
  - 上游：ToString('HH\:mm', x:Null)
  - 移植：ToString('HH:mm', x:Null)
  - 理由：上游用转义冒号（字面 ':'），移植去掉反斜杠后 ':' 变成区域性 TimeSeparator，在 fi-FI 等分隔符不是冒号的区域会显示成 12.34；属无意削弱，见 bugs 第 7 条（两处 {@0} 文案：Lang.DailyNoteButton_StarRail_FullTime / ..._Genshin_FullTime）。

#### `Features/SelfQuery/SelfQueryPage.xaml`

- **page 根元素（第 3 行，唯一 hunk）** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：无 NavigationCacheMode
  - 移植：NavigationCacheMode="Required"
  - 理由：D60 缓存策略；该页 .xaml.cs 与上游 0 差异，但缓存实例让 OnNavigatedTo 里「按游戏设 ListView.Visibility=Visible」的置位永不回落，见 bugs 第 5 条。

#### `Features/GameRecord/BBSWebBridge.Linux.cs`

- **整文件（24 行）vs upstream BBSWebBridge.xaml.cs（574 行，csproj:98 <Compile Remove>）** ｜ cut 裁剪 ｜ must 必需
  - 上游：完整 WebView2 脚本桥：文档标题/截图裁剪与保存/复制、Cookie 注入、页面内导航等 570+ 行
  - 移植：只有 GameBiz / IsHoyolab / IsLoggedIn => WebLoginService.Current.HasCookies / DocumentTitle 与 3 个空 RelayCommand（CloseScreenshotGrid/CopyScreenshot/SaveScreenshot），截图为空实现
  - 理由：M4 占位：WebView2 脚本桥（BBS 工具箱截图/复制）明确暂不做，只保留不崩的最小契约（12-shims-and-gaps.md:96 ⬜ M4）。

#### `Features/GameRecord/BattleChronicleWindow.Linux.cs`

- **整文件 vs upstream BattleChronicleWindow.xaml.cs（csproj:99 <Compile Remove>）** ｜ cut 裁剪 ｜ must 必需
  - 上游：完整战绩窗：标题栏、角色切换、内嵌 BattleChroniclePage、窗口尺寸/位置持久化
  - 移植：空窗口占位：Title="战绩"、Content = new Grid()，ShowBattleChronicle(owner, role) 只 Activate（BattleChronicleWindow.LinuxAuto.cs 的 RootGrid_Loaded 为空实现）
  - 理由：M4 占位：「战绩窗」整窗明确暂不做，保留入口不崩（12-shims-and-gaps.md:97 ⬜ M4）。

### 3.7 背景与图片加载（Background / Codec / Controls）

视频壁纸在 Skia 下用 `MediaPlayerElement` + 自绘圆点替换 `PipsPager`；`CachedImage`/`ImageLoader` 改成 Skia 可用的解码与缩略图路径。

#### `Features/Background/BackgroundViewPage.xaml`

- **:135-155（ListView 声明 + :145-150 ItemContainerStyle + :151-155 ItemsPanel）**｜engine 引擎能力｜must 必需
  - 上游：<GridView x:Name="GridView_Images" ...> + Style BasedOn DefaultGridViewItemStyle + ItemsWrapGrid ItemWidth="228" ItemHeight="136"
  - 移植：<ListView x:Name="GridView_Images" ...> + Style BasedOn DefaultListViewItemStyle + <tk:WrapPanel HorizontalSpacing="12" VerticalSpacing="12"/>
  - 理由：Uno/Skia 下没有可用的 ItemsWrapGrid：Toolkit 的 VirtualizingLayout 在 Uno 不调 MeasureOverride/ArrangeOverride（Controls/ToolkitControls.cs:116-124 注释），只能用自研 Panel shim。代价是容器失去虚拟化，图库一次性实例化所有条目（几百个视频时会明显变慢/吃内存）。
- **:161-171（条目里的 CachedImage）** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：Width="216" Height="122" HorizontalAlignment="Center" VerticalAlignment="Center" Source="{x:Bind FilePath}"
  - 移植：HorizontalAlignment/VerticalAlignment="Stretch" DecodePixelWidth="400" Source="{x:Bind DisplayPath, Mode=OneWay}"（IsThumbnail/PngThumbnail 属性保留）
  - 理由：自研 WrapPanel 不按 ItemWidth/ItemHeight 约束子元素，尺寸改由 code-behind UpdateTileSize 下发，故用 Stretch 填满容器；Source 改吃 DisplayPath，让视频条目显示 VideoThumbnailer 生成的首帧 png（上游靠 IsThumbnail 走 WIC 视频缩略图）。

#### `Features/Background/BackgroundViewPage.xaml.cs`

- **:34 SizeChanged 订阅、:64-80 GenerateVideoThumbnailsAsync、:89-106 OnPageSizeChanged、:108-147 UpdateTileSize、:191-192 LoadBackgroundItems 末尾** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：无对应代码（尺寸由 ItemsWrapGrid ItemWidth=228/ItemHeight=136 静态给出，视频缩略图由 CachedImage.IsThumbnail→ImageThumbnail.GetVideoThumbnailAsync 出）
  - 移植：逐个 item 调 VideoThumbnailer.EnsureAsync(item.FilePath) 生成首帧 png 并回 UI 线程写 ThumbnailPath；UpdateTileSize 用 `avail=ActualWidth-Padding.L-R`、`gap=24`、`minTile=216` 算列数与 tile 宽，再写 panel.ItemWidth/ItemHeight + InvalidateMeasure
  - 理由：WrapPanel shim 既没有固定格尺寸也没有 shell 缩略图能力，格宽/列数/视频首帧都得手补。副作用：布局依赖 ActualWidth，每格宽度随窗口变化（上游是写死 216×122），且条目宽度不再是 216 的整数语义。
- **:216、:278、:347、:400、:422、:445（六处 OpenUrl.Open）**｜api 平台 API｜must 必需
  - 上游：Launcher.LaunchFolderPathAsync / Launcher.LaunchUriAsync / Launcher.LaunchFileAsync + FolderLauncherOptions{ItemsToSelect} + LauncherOptions{DisplayApplicationPicker=true}
  - 移植：Starward.Helpers.OpenUrl.Open(folder | file)（xdg-open）
  - 理由：WinRT Launcher 在 Linux 不存在，统一走 xdg-open。代价是两条交互丢失：『在文件管理器中定位』不再自动选中文件（只打开目录），『打开方式』选择器消失（只能默认程序打开）。

#### `Features/Background/BackgroundService.cs`

- **:185-196 GetFallbackBackgroundImage** ｜ linux 专属实现 ｜ must 必需
  - 上游：Path.Combine(baseFolder, @"Assets\Image\UI_CutScene_1130320101A.png")
  - 移植：Path.Combine(baseFolder, "Assets", "Image", "UI_CutScene_1130320101A.png")
  - 理由：上游把 Windows 反斜杠写进了字面量，Linux 上 File.Exists 永远 false，兜底背景会静默消失；必须改成分段拼接。
- **:205-222 实例重载、:231-244 静态重载、:252-267 PickBackgroundFileAsync、:276-291 CheckBackgroundFileAvailableAsync** ｜ api 平台 API ｜ reasonable 合理换法
  - 上游：static ChangeCustomBackgroundFileAsync(StorageFile file)：先用 MediaSource.OpenAsync/BitmapDecoder.CreateAsync 试解码，坏文件抛异常（对话框显示 CannotDecodeFile），校验通过才复制
  - 移植：改为 string 路径 + FileDialogHelper.PickSingleFileAsync（bmp/jpg/png/webp/avif/jxl + mp4/mkv/webm），校验只剩 File.Exists + FileInfo.Length>0
  - 理由：StorageFile/FileSavePicker 不可用，只能用 System.IO + 文件对话框。代价是『这张图能不能解码』不再校验：坏图/无解码器的格式会被复制进缓存并写进 AppConfig（见 bug）。

#### `Features/Background/BackgroundViewWindow.xaml.cs`

- **:29-44 InitializeWindow、:48-52 CenterInScreen、:56 DefaultWindowSizeInDips、:60-65 Show** ｜ linux 专属实现 ｜ must 必需
  - 上游：User32.GetCursorPos + DisplayArea.GetFromPoint + AppWindow.MoveAndResize 居中；Show() 里 AppWindow.MoveAndResize 复制 MainWindow 的 Position/Size；AppWindow.TitleBar.ExtendsContentIntoTitleBar=true
  - 移植：base.CenterInScreen(width ?? 1200, height ?? 676)（位置交给 WM）；Show() 只 base.Show()；AppWindow.Title="Starward - Background Viewer"；新增 DefaultWindowSizeInDips=(1200,676)
  - 理由：Uno X11 下 AppWindow.MoveAndResize 是空操作（L-35），居中/复制主窗几何都无意义；ExtendsContentIntoTitleBar 无对应实现，改用 AppWindow.Title 设标题。可见差异：背景查看窗不再与主窗同位置同尺寸，固定 1200×676 DIP（受 WM 约束）。

#### `Features/Background/BackgroundFileItem.cs`

- **:7 类声明、:22-33 ThumbnailPath、:35 DisplayPath** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：普通 class：FilePath/FileInfo/FileSize/CreationTime/IsVideo 只读属性，无通知
  - 移植：: ObservableObject，新增 ThumbnailPath（set 里 OnPropertyChanged(nameof(DisplayPath))）与 DisplayPath => ThumbnailPath ?? FilePath
  - 理由：视频首帧是在后台生成、再回 UI 线程补进来的，必须有属性通知才能让 x:Bind 刷新；DisplayPath 作为兜底值同时承担『缩略图未就绪』的语义，而它等于 .mp4 原路径（见 bug）。

#### `Features/Background/AppBackground.Linux.cs`

- **:55-60 字段、:88-140 ctor（Content 手工搭 Grid）**｜engine 引擎能力｜avoidable 可避免
  - 上游：AppBackground.xaml：swi:ImageEx（PlaceholderSource 交叉淡入 + AppBackgroundImageExStyle）+ 三块渐变 Rectangle 遮罩（右上/右下 400×400、右侧 200 宽）+ 底部 ProgressBar(IsIndeterminate/Visibility=IsUpdateBackgroundRunning)
  - 移植：Content = new Grid{ Background=#FF16161A, _image(Image), _videoCanvas(SKXamlCanvas) }；不调 InitializeComponent；_image.Opacity=1.0；BackgroundImageSource setter 直接赋 _image.Source，PlacehoderImageSource 只 Raise
  - 理由：ImageEx（Scighost.WinUI.ImageEx）在 Linux 无实现，只能手搭视觉树。丢失三件事：换图交叉淡入（原来 null→PlacehoderImageSource 承载旧图）、三块角落渐变遮罩（壁纸压暗，D54 决定不再压暗整体）、背景更新中的底部进度条（用户看不到『正在取背景』）。
- **:201-244 LoadBackgroundImageAsync、:246-... ComposeBackgroundPng、:907-928 ToBitmap** ｜ engine 引擎能力 ｜ must 必需
  - 上游：ChangeBackgroundImageAsync：BitmapDecoder 解码，按窗口 ActualWidth*scale 判断是否需要缩放（SoftwareBitmap + BitmapTransform），出 WriteableBitmap/SoftwareBitmapSource；主题图只在本分支叠（CanvasBitmap 合成）
  - 移植：ToBitmap = new BitmapImage(new Uri(Path.GetFullPath(path)))（全尺寸惰性解码）；有 Theme 时用 SkiaSharp 合成底图+主题层 → Encode(Png,100) → BitmapImage.SetSourceAsync；所有静态背景（非 CUSTOM）都叠主题层
  - 理由：BitmapDecoder/SoftwareBitmapSource/BitmapTransform 在 Linux 不可用。两点代价：丢『按窗口尺寸降采样』（4K 壁纸按原分辨率进内存，且换图时同一张图被 Skia 再解一次、再 PNG 重编码一次）；主题文字层从『仅视频型背景分支』扩大到『所有静态背景』（视觉更接近上游截图，但比上游多做一次网络下载 + 合成）。
- **:600-653 TryStartVideoBackgroundAsync、:657-752 OnVideoFrameArrived/VideoCanvas_PaintSurface、:880-900 StopVideoBackground、:29-36 帧率开关** ｜ engine 引擎能力 ｜ must 必需
  - 上游：MediaPlayer(IsVideoFrameServerEnabled) + VideoFrameAvailable→CopyFrameToVideoSurface(CanvasRenderTarget) + CanvasImageSource 绘制 + overlay CanvasBitmap；SemaphoreSlim 节流；VP9Helper.IsVP9DecoderInstalled/RegisterVP9Decoder/RegisterVorbisDecoder/SuggestToInstallVP9Decoder
  - 移植：LibVLCSharp VideoBackgroundPlayer → 原始 byte[] 帧回调 → 双 SKBitmap(front/back) + SKXamlCanvas.Invalidate/PaintSurface 绘制；按 LUNARWARD_VIDEO_FPS（默认 30）合并帧；vp9/vorbis 注册与『装 VP9 扩展』提示全部删除
  - 理由：Uno/Skia 没有 MediaPlayer 与 Win2D；libvlc 自带 vp8/vp9/vorbis 解码。等价能力靠 vmem 回调 + Skia 重绘，代价是每帧一次 Marshal.Copy(约 8MB)+一次 Invalidish/Paint，以及解码器安装提示这条 UX 完全消失（Linux 上不需要）。
- **:251-300 GetSuggestedBackgroundCachedAsync、:412-418 下载 3s 超时、:264 LUNARWARD_BG_TIMEOUT_MS** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：for (int i = 0; i < 2; i++) 两轮：API 超时 1s（new CancellationTokenSource(1000)），下载超时 3s
  - 移植：建议背景结果按 gameId 缓存（成功 5min / 失败 30s），API 超时 4000ms 起、失败再翻倍重试一次（共 2 次）；下载仍 3s
  - 理由：L-71：Linux/TLS 首包比 Windows 慢，1s 必然超时把背景打成兜底图；把『重试』从事后循环搬到缓存层并放宽到 4s+8s，同时避免每次切页都等一轮超时。副作用：同一游戏 5 分钟内不会重新请求建议背景。
- **:394-400 早退守卫、:401-421 预填、:437-500 失败保画面、:515-540 应用顺序** ｜ eng 工程性 ｜ reasonable 合理换法
  - 上游：每次 UpdateBackgroundAsync 都跑完整流程：DisposeVideoResource → BackgroundImageSource=null → 重新下载/解码/取色
  - 移植：同游戏且已有画面（_appliedGameId == gameId && (BackgroundImageSource != null || _videoActive)）直接 return；换图期间先预填缓存/兜底图，失败时保留当前画面，不把 BackgroundImageSource 置 null
  - 理由：避免无意义重复下载与『先黑一下再出来』（上游每次切页都会闪一下）。副作用是整条链路必须靠 _appliedGameId/_lastBackgroundFile 手工失效，漏失效就会『改了配置不刷新』（见 bug）。
- **:61 _lastAccentFile、:528-532、:760-781 ApplyAccentFromPosterAsync** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：静态分支用已解码的像素取色（ChangeBackgroundImageAsync 里 GetAccentColor(PixelBuffer/IMemoryBuffer)）+ 写 AppConfig.AccentColor；视频型分支用 CanvasBitmap 像素取色
  - 移植：静态分支 Starward.Helpers.AccentColorHelper.ApplyFromFile(filePath)（Skia 再从文件取一次色，_lastAccentFile 去重）；视频型分支改用海报图 background.Background.Url 取色；AppConfig.AccentColor 不再写
  - 理由：取不到解码后的像素 buffer，只能从文件再取一次（每换一张壁纸多一次全图解码 + 降采样统计）；视频型没有合成图可取样，退而用海报图（比上游『合成帧取色』更稳定的色调）；AppConfig.AccentColor 不再持久化，重启后先显示默认强调色直到壁纸解码完成。
- **:29 LUNARWARD_VIDEO_FPS、:36 LUNARWARD_VIDEO_NOPAINT、:264 LUNARWARD_BG_TIMEOUT_MS、:426 LUNARWARD_VIDEO_BG、:569-570 LUNARWARD_VIDEO_OVERLAY** ｜ linux 专属实现 ｜ avoidable 可避免
  - 上游：无（上游没有任何环境变量开关）
  - 移植：Environment.GetEnvironmentVariable(...) 直接读取，未加 #if DEBUG 也未收进 DevSwitches
  - 理由：违反仓库硬规则 6『调试/开发入口一律 #if DEBUG 或 DevSwitches，Release 里不得残留副作用命令』：LUNARWARD_VIDEO_BG 能把任意本地视频塞成壁纸、LUNARWARD_VIDEO_NOPAINT 会直接把视频画面丢掉（只留声音+静止帧），Release 用户误设就会表现成『视频壁纸不出画面』。
- **:70-80 Current 惰性 getter、:377 _disposed、:880-900 无 Unloaded 清理、:932-949 ChangeCustomBackgroundAsync**｜eng 工程性｜avoidable 可避免
  - 上游：Current 是 ctor 里赋值的普通自动属性；Unloaded → DisposeVideoResource() + UnregisterAll()；ChangeCustomBackgroundAsync 从缓存目录取全路径后再转 BitmapImage
  - 移植：Current 用 `_current ??= new AppBackground()`（可能造出不在可视树的实例）；_disposed 声明后从未置 true，也没有 Unloaded 处理器；ChangeCustomBackgroundAsync 把 BackgroundService 返回的『纯文件名』直接喂给 ToBitmap（Path.GetFullPath 按进程 CWD 解析）——该方法当前无任何调用点，属死代码
  - 理由：Uno 的 UserControl 生命周期与 WinUI 不同（页面缓存 + 单例宿主），但没补卸载清理意味着视频播放器/消息注册不会释放；惰性 getter 让『在读 Current 的地方先创建一个空实例』成为可能（构造函数会启动 UiWatchdog、注册消息）。ChangeCustomBackgroundAsync 是照抄上游签名但未适配路径语义的死代码，一旦被调用必然报『这张图片读不了』。

#### `Helpers/AccentColorHelper.Linux.cs`

- **:20-100 ApplyFromFile/PickColor、:200-240 Hue/ToHsv/FromHsv** ｜ engine 引擎能力 ｜ must 必需
  - 上游：Helpers/AccentColorHelper.GetAccentColor(byte[]/IBuffer/nint)：隔点采样求平均 RGB → ToHsv，返回 (FromHsv(H,0.6,V), FromHsv(平均H,0.9,0.9))；常量 0.6 饱和度、0.9/0.9 前景色
  - 移植：SKBitmap.GetPixel 逐点统计 36 个色相桶（权重 = 饱和度×明度，跳过 alpha<200、max<0.15、delta<0.12），合并相邻桶取最大，再 clamp S∈[0.30,0.55]、V∈[0.65,0.90] 生成单一颜色
  - 理由：IBuffer/IMemoryBufferByteAccess 的 COM 接口与 WinRT 像素访问在 Linux 不存在，只能用 SkiaSharp 读图。算法也换了（主色直方图 vs 平均色），同一张壁纸算出的强调色与上游不同——这是可见的配色偏差，不是实现细节。
- **:160-190 ChangeAppAccentColor** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：Features/Background/AccentColorHelper.cs:109-132：只写 Application.Current.Resources["SystemAccentColor"/Light1-3/Dark1-3]，Light1..Dark3 用 ColorMix(白/黑, 0.8/0.6/0.4)（移植端 Mix 权重同）
  - 移植：同一套 Key 写 Application.Current.Resources 之外，再遍历 Resources.ThemeDictionaries 的 Light/Dark/Default/HighContrast 各写一遍（额外 Send AccentColorChangedMessage，与上游一致）
  - 理由：L-89：本移植的 App.xaml 把强调色定义在 ThemeDictionaries 里，只写应用级 Resources 对 {ThemeResource SystemAccentColor} 不生效（实测按钮颜色不变），所以必须双写；幂等，但每次换壁纸多写 7×5 个资源项。

#### `Features/Codec/ImageLoader.Linux.cs`

- **:11-28 GetImagePixelSizeAsync（全文件 30 行）** ｜ engine 引擎能力 ｜ must 必需
  - 上游：Features/Codec/ImageLoader.cs:345-364：BitmapDecoder.CreateAsync + .avif→avifDecoderLite/.jxl→JxlDecoderLite（系统无解码器时），367 行文件
  - 移植：File.OpenRead + SKCodec.Create → (codec.Info.Width, codec.Info.Height)；失败再 MagickCodec.TryGetPixelSize；都不行返回 (0u,0u)
  - 理由：Win2D/WIC/Starward.Codec 不可移植，改用 Skia（Uno 本来就带）+ Magick 兜 AVIF/JXL。语义差异：上游遇到解不了的文件是抛异常（调用方 catch 吞掉），移植返回 (0,0)，ScreenshotItem.cs:139 已判 width>0 才显示，不会崩但也不显示尺寸。
- **全文件：LoadImageAsync / LoadPixelBytes / ImageInfo 等成员整体未移植** ｜ cut 裁剪 ｜ must 必需
  - 上游：LoadImageAsync(string, CancellationToken) → ImageInfo、GetPixelBytes 等（截图查看/批量转换用）
  - 移植：无对应成员（只有 GetImagePixelSizeAsync）
  - 理由：唯一调用方 ImageBatchConvertWindow.xaml.cs:670 与 ImageViewWindow2.xaml.cs:632/679 在本移植里被 <Compile Remove>（M4 截图窗），没有调用者；保留等于死代码，且要重写整套 Win2D 像素管线。

#### `Controls/CachedImage.Linux.cs`

- **:16-30 类与新 Source DP、:32-62 CornerRadius/UpdateClip、:97-98/233-240 事件**｜engine 引擎能力｜reasonable 合理换法
  - 上游：Controls/CachedImage.cs:15 继承 Scighost.WinUI.ImageEx：占位图/交叉淡入/PlaceholderSource/Stretch 缓存/ImageOpened 语义都在基类
  - 移植：继承 Image，自建 new object Source DP + CornerRadius DP + ImageOpened/ImageExOpened/ImageExFailed，圆角用 Clip 近似
  - 理由：ImageEx 是 Windows-only 包（依赖 Win2D/合成器）。视觉与行为差异：无占位图与淡入、Source 变成 object 型 DP（只认 string/Uri，见下）、圆角退化成矩形裁剪。
- **:64-80 IsThumbnail/PngThumbnail 声明（全文件无任何读取）、:131-227 LoadAsync/ReadFileAsync/DownscaleToStream** ｜ cut 裁剪 ｜ must 必需
  - 上游：ProvideCachedResourceAsync：file+IsThumbnail → ImageThumbnail.GetVideoThumbnailAsync(ffmpeg 首帧) / GetImageThumbnailAsync(PngThumbnail)（WIC 缩略图 + 磁盘缓存），否则 new BitmapImage(uri)
  - 移植：IsThumbnail/PngThumbnail 只声明不使用；一律 File.ReadAllBytesAsync 整文件读入 + 可选 Skia 降采样（DecodePixelWidth）后 SetSourceAsync
  - 理由：WIC/Windows shell 缩略图在 Linux 不存在，缩略图缓存层整体删除，视频首帧改由 BackgroundViewPage 用 ffmpeg 单干。代价：整文件进内存 + 每次重新解码/降采样，没有任何磁盘缓存（见 bug）。
- **:18 静态 HttpClient、:160-169 http 分支、:121-128 失败分支**｜engine 引擎能力｜avoidable 可避免
  - 上游：FileCache.GetFromCacheAsync(uri, false, token) 落盘复用；BitmapImage_ImageFailed 里 FileCache.DeleteCacheFile(uriSource) 清掉坏缓存
  - 移植：静态 HttpClient(30s) 直接 GetByteArrayAsync → MemoryStream → BitmapImage.SetSourceAsync；失败只写 Debug.WriteLine + RaiseImageExFailed
  - 理由：FileCache 依赖 StorageFile/ApplicationData；但代价是网络图（公告/抽卡物品图标）没有磁盘缓存、也没有坏缓存清理，每次进页面都重新下载，弱网下明显慢且更吃流量。
- **:50-62 UpdateClip** ｜ cosmetic 外观文案 ｜ avoidable 可避免
  - 上游：ImageEx 的圆角交给模板/合成器，无 Clip 覆盖
  - 移植：SizeChanged/CornerRadius 变化时 `Clip = new RectangleGeometry{ Rect = new Rect(0,0,ActualWidth,ActualHeight) }`，CornerRadius 的值被忽略
  - 理由：注释自称『圆角在渲染层由合成器处理（M3）』，实际没有任何地方补圆角：上游设 CornerRadius 的地方（图库条目 ItemContainerStyle=4、GameBannerAndPost 等）在移植里都是直角，属可见的视觉降级；且 Clip 覆盖会让注释里的『M3 再补』无处可补。
- **:131-172 LoadAsync 的 value 分支、:87-95 新增 DecodePixelWidth**｜api 平台 API｜avoidable 可避免
  - 上游：Source 是 ImageSource 类型：任何 ImageSource（BitmapImage/WriteableBitmap）直接显示；降采样由 ImageThumbnail 在网络/解码头做
  - 移植：只接受 string/Uri（裸路径 / file / ms-appx / ms-appdata / http），其它类型走 `_ => null` → base.Source=null（空白）；新增 DecodePixelWidth 在 SetSourceAsync 前用 Skia 降采样
  - 理由：自建加载管线要自己解析 scheme（Uno 下 file:// 与相对 Uri 都不可靠，L-74）。当前仓库里没有把 ImageSource 直接赋给 CachedImage.Source 的调用点，属潜在回归面：一旦有人像用 Image 那样赋 BitmapImage，图片会静默消失。

### 3.8 设置界面（Setting）

字体（XboxFluent2 → 系统字体）、图标码位、热键控件、托盘行为是主要差异；设置页本身被缓存（D60）。

#### `Features/Setting/SettingPage.xaml`

- **:2-3 sf:PageBase 属性** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：无 NavigationCacheMode（默认 Disabled，每次导航重建页面）
  - 移植：NavigationCacheMode="Required" x:Class="Starward.Features.Setting.SettingPage"
  - 理由：Uno 下重建整页会丢掉语言消息订阅与子页状态；缓存后 ctor 只跑一次，配套把服务注册搬到 OnLoaded（D94）。上游 Windows 重建开销小，无此需求。
- **:19 根 Grid**｜engine 引擎能力｜reasonable 合理换法
  - 上游：<Grid>（无背景）
  - 移植：<Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">
  - 理由：缺 DWM 丙烯，透明根容器会让壁纸/背景文字透到设置页造成叠字；补不透明底（D61 软差异处理）。
- **:21 NavigationView**｜api 平台 API｜reasonable 合理换法
  - 上游：<NavigationView ...>（无 x:Name）
  - 移植：x:Name="NavigationView_Setting"
  - 理由：code-behind 要在初始化/调试直达时同步 SelectedItem，只能靠具名引用。
- **:86 手柄菜单图标**｜engine 引擎能力｜reasonable 合理换法
  - 上游：<FontIcon FontFamily="{ThemeResource XboxFluent2}" Glyph="&#xE9CD;" />
  - 移植：<FontIcon Glyph="&#xE7FC;" />（注释：XboxFluent2 在 Uno/Skia 下不加载）
  - 理由：XboxFluent2 字体未注册，原图标渲染为空白；退化为 Symbols 里存在的手柄码位。
- **:88-89 菜单项尾部注释** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：上游没有「运行环境」设置项（MenuItems 只有 About/General/Download/FileManage/Screenshot/Hotkey/Gamepad/Advanced/Toolbox）
  - 移植：注释说明运行环境入口移到 MainView 页脚
  - 理由：运行环境是 Linux 专属模块，入口放在主页脚而非设置页；两边菜单项数量一致，仅注释差别。

#### `Features/Setting/SettingPage.xaml.cs`

- **:20-31 ctor** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：:20 Frame_Setting.Navigate(typeof(AboutSetting));
  - 移植：:22-23 Type initialPage = ResolveInitialPage(); Frame_Setting.Navigate(initialPage); 另 :25-31 遍历 MenuItems 同步 SelectedItem
  - 理由：支持「直达某子页」（预检跳转/调试截图）。因页面被 Required 缓存，ctor 只执行一次，重定向实际只在首次构造生效（见 bug）。
- **:36-41 OnLoaded**｜bugfix 修上游缺陷｜reasonable 合理换法
  - 上游：:21 WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, ...) 在 ctor 内注册
  - 移植：protected override void OnLoaded() 内注册；:102-105 OnUnloaded 里 UnregisterAll(this)
  - 理由：页面缓存后 ctor 只跑一次，若仍在 ctor 注册，卸载时 UnregisterAll 会把语言切换绑定永久丢掉；搬到 OnLoaded 与上游「每页新建即注册」等价（D94）。
- **:45-68 ResolveInitialPage；:86 ItemInvoked 新增分支** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：:30-40 args.InvokedItemContainer?.Tag switch { ... }（无 RuntimeEnvironmentSetting）
  - 移植：:86 nameof(RuntimeEnvironmentSetting) => typeof(RuntimeEnvironmentSetting)；ResolveInitialPage 依次读 SettingNavigation.TakeTarget()、DevSwitches.SubPage，兜底 AboutSetting
  - 理由：Linux 专属运行环境页需要导航分支；DevSwitches.SubPage 走 LUNARWARD_SUBPAGE 环境变量（符合规则 6）。SettingNavigation 目前全仓无调用方，为潜伏钩子。

#### `Features/Setting/AdvancedSetting.xaml / .cs`

- **xaml:46-52 InfoBar；.cs:110-233 RPC 状态/启停，含 :170 与 :216 的 catch 类型替换** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：过程内 RpcService.CheckRpcServerRunning()/CreateRpcClient<...>() 真实拉起 Starward.RPC（Grpc.Core.RpcException、StatusCode.DeadlineExceeded）
  - 移植：InfoBar「Linux 版暂未实现 RPC 服务，下面的开关与按钮不会生效。」；catch (OperationCanceledException) / catch (Exception)；RpcService.Linux.cs 恒 false/NotRunning，CreateRpcClient 抛 NotSupportedException
  - 理由：M4 明确暂不做 RPC（云游戏/战绩/BBS 同样依赖它）；保留整块 UI 与状态刷新骨架而非删段，避免排版塌陷，用 InfoBar 告知不可用。
- **xaml:129-151「诊断」区；.cs Button_CreateDiagnose_Click / Button_OpenDiagnoseFolder_Click** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：该位置无对应区块（移植新增；注释称替代 Windows 的提权类入口）
  - 移植：Button「生成诊断包」(FontIcon F412) 调 Helpers.DiagnosticBundle.CreateAsync(logger:_logger) 并 toast；Button「打开下载目录」mkdir ~/Downloads 后 Helpers.OpenUrl.Open
  - 理由：Linux 排障需要日志/前缀/环境快照；DiagnosticBundle 的输出目录与「打开」按钮目标一致，无 UI 线程 fork。
- **xaml:153-186「运行输出」区；.cs Button_ViewOutput_Click / BuildOutputText / Button_OpenOutputFolder_Click** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：上游无此区块（输出走 NLog 文件，不提供查看器）
  - 移植：ContentDialog 展示 Features.Runtime.OutputCapture.Recent(500)，主按钮「刷新」递归重开、次按钮「打开日志文件夹」xdg-open ~/.local/share/lunarward/logs/output
  - 理由：wine/预检输出对 Linux 用户是关键排障信息（D27）；resx 键 Lunarward_Output_Title/View/OpenFolder 已本地化。

#### `Features/Setting/GeneralSetting.xaml`

- **:46 RadioButton_CloseWindowOption_Hide** ｜ cosmetic 外观文案 ｜ avoidable 可避免
  - 上游：Content="{x:Bind lang:Lang.ExperienceSettingPage_MinimizeToSystemTray}"
  - 移植：Content="隐藏到系统托盘"（硬编码中文）
  - 理由：该页其它文案仍走 lang；硬编码后切到英文/日文时不跟随，与相邻项不一致。语种问题属可避免的偏离。
- **:62 透明/动画效果区块** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：MenuFlyoutSeparator + 两个 Hyperlink（Click=Hyperlink_VisualEffects_Click, NavigateUri=ms-settings:easeofaccess-visualeffects）与说明文本
  - 移植：整段删除，仅留注释「Linux：透明/动画效果是 Windows 系统设置项…整段移除（D61）」
  - 理由：ms-settings: URI 在 Linux 无处理程序，点了没反应；对应 Windows 视觉特效开关不存在。GeneralSetting.xaml.cs 未改，Hyperlink_VisualEffects_Click 成为孤儿方法（无害）。

#### `Features/Setting/DownloadSetting.xaml.cs`

- **:102 打开安装目录** ｜ api 平台 API ｜ must 必需
  - 上游：await Launcher.LaunchUriAsync(new Uri(DefaultInstallPath));
  - 移植：Starward.Helpers.OpenUrl.Open(DefaultInstallPath);
  - 理由：Launcher.LaunchUriAsync 对 file/本地目录路径在 Linux 无效，改走 xdg-open 封装（Helpers/OpenUrl）。
- **:173-184 SetRateLimiterAsync**｜cut 裁剪｜avoidable 可避免
  - 上游：:177-182 using var client = RpcService.CreateRpcClient<GameInstaller.GameInstallerClient>(); await client.SetRateLimiterAsync(new RateLimiterMessage { BytesPerSecond = Math.Clamp(value * 1024, 0, int.MaxValue) });
  - 移植：RPC 未实现，方法体只剩注释 + await Task.CompletedTask；SpeedLimit 仍写 AppConfig.SpeedLimitKBPerSecond（:165）
  - 理由：限速由 Windows RPC 安装服务执行；移植的 Sophon 下载链路（ChunkReuseDownloader/InstallDownloadRunner/SophonClient）从不读 SpeedLimitKBPerSecond，滑杆可调但完全无效，且该页没有「不支持」提示（高级页有），用户无从得知。

#### `Features/Setting/FileManageSetting.xaml`

- **:55 用户数据目录 TextBlock**｜cosmetic 外观文案｜avoidable 可避免
  - 上游：含 IsTextTrimmedChanged="TextBlock_IsTextTrimmedChanged"（.cs:147-153 把过长路径字号缩到 12）
  - 移植：该属性被删除，仅剩 MaxLines=2 + TextTrimming=CharacterEllipsis；处理器仍存在于 code-behind
  - 理由：长路径由「缩字号完整显示」变成「第二行省略号」。未实测 Uno 下该事件是否可靠触发，若是引擎限制则合理，否则属可避免的显示退化。
- **:83 备份按钮图标** ｜ engine 引擎能力 ｜ must 必需
  - 上游：<FontIcon Glyph="&#xF413;" />
  - 移植：<FontIcon Glyph="&#xE74E;" />
  - 理由：F413 不在 Uno 自带 Symbols 字体覆盖范围（显示豆腐/空白），换成存在的 E74E（保存）。

#### `Features/Setting/FileManageSetting.xaml.cs`

- **:204-210 手动备份**｜api 平台 API｜must 必需
  - 上游：string archive = Path.Combine(folder, $"{name}.7z"); new SharpSevenZipCompressor().CompressFiles(archive, file);
  - 移植：扩展名改 .zip，改用 DatabaseService.CreateArchive(archive, file)（注释：只打包单个 .db，不打包 DatabaseBackup 整个目录）
  - 理由：SharpSevenZip 带原生 7z 依赖，增加打包/发布负担；移植侧 DatabaseService 自动备份本就走 zip（DatabaseService.cs:109），手动备份与之统一。副作用：备份文件与上游 Windows 版不互通；恢复路径 OpenLastBackupDatabaseAsync 用 Launcher.LaunchFolderAsync 打开目录，仍能读 .zip。

#### `Features/Setting/GamepadControlSetting.xaml`

- **:22-29 InfoBar + :35 IsEnabled="False"** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：无 InfoBar；EnableGamepadController ToggleSwitch 可用，页面内各控件按 {x:Bind EnableGamepadController} 联动
  - 移植：新增 InfoBar（lang:Lunarward_Gamepad_NotSupported_Title/Message），开关锁死 IsEnabled="False"
  - 理由：M4 手柄映射占位（GamepadController.Linux.cs 空实现）；保留页面结构以便后续实现，先明确告知不可用并锁死开关，避免用户开了无效。
- **:101/109/113/128/159 XboxFluent2 图标** ｜ cosmetic 外观文案 ｜ avoidable 可避免
  - 上游：FontFamily="{ThemeResource XboxFluent2}"（Windows 下正常渲染）
  - 移植：同页 5 处仍保留 XboxFluent2 FontFamily 与字形
  - 理由：SettingPage 菜单的手柄图标已因字体缺失换成 Symbols 码位，本页漏改；Uno/Skia 下这 5 处图标渲染为空白。页面整体已被置灰，影响有限但属不一致。

#### `Features/Setting/HotkeySetting.xaml`

- **:22 单键快捷键提示** ｜ linux 专属实现 ｜ reasonable 合理换法
  - 上游：Text="{x:Bind lang:Lang.HotkeySetting_SingleKeyShortcutsTips}"（提示需管理员权限运行 Starward）
  - 移植：Text="{x:Bind lang:Lang.Lunarward_Hotkey_Description}"（新增 resx 键，含 zh/en）
  - 理由：Linux 下热键是 X11 轮询、不需要管理员权限；替换文案并把 Wayland 限制写清。:19 另加同义注释。

#### `Features/Setting/ScreenshotSetting.xaml`

- **:81-84 截图热键提示** ｜ cosmetic 外观文案 ｜ avoidable 可避免
  - 上游：Text="{x:Bind lang:Lang.HotkeySetting_SingleKeyShortcutsTips}"
  - 移植：Text="Linux 下截图热键通过轮询 X11 键盘状态实现，不需要管理员权限。纯 Wayland 会话不可用，可以把系统快捷键绑定到 lunarward --screenshot。"（硬编码中文）
  - 理由：同一批改动的 HotkeySetting 页用了新增 resx 键，这里却内联中文；非中文界面会出现混排。:61-64 只是 IsTextTrimmedChanged 属性换位（纯格式，不记）。

#### `Features/Setting/AboutSetting.xaml`

- **:22 Logo / :26 名称 / :28 署名**｜cosmetic 外观文案｜reasonable 合理换法
  - 上游：Source="ms-appx:///Assets/logo.png"；Text="Starward"；版权署名行
  - 移植：Source="ms-appx:///Assets/lunarward.png"（46 KB，logo.png 在移植树中不存在）；Text="Lunarward"；"Lunarward · 基于 Starward（MIT）的 Linux 移植"
  - 理由：移植分叉必须换品牌与 Logo（否则继承上游图标且图片资源缺失会加载失败），并明确 MIT 出处。

#### `Helpers/HotkeyManager.Linux.cs`

- **整文件（285 行，namespace Starward.Helpers）对应上游 Features/Setting/HotkeyManager.cs（184 行，namespace Starward.Features.Setting）** ｜ api 平台 API ｜ must 必需
  - 上游：User32.RegisterHotKey(hwnd, id, modifiers|MOD_NOREPEAT, key) + Kernel32.GetLastError() 注册/注销，失败返回 ERROR_HOTKEY_ALREADY_REGISTERED 等
  - 移植：X11KeyState 定时器 25ms XQueryKeymap 轮询，边沿触发（_pressed HashSet）后自派发：44444 恢复并 EWMH 显示主窗、44445 调 ScreenCaptureLinux.CaptureAsync()；hwnd 参数被忽略（GetWindowHandle() 恒 0）
  - 理由：无 Win32 RegisterHotKey 等价物；XGrabKey 要么抢键、要么在 Wayland 不可用，故选轮询（D61/文档已知缺口）。代价：纯 Wayland 完全不可用、按键映射只覆盖字母数字与 F 键、25ms 粒度。

### 3.9 服务与帮助器（PlayTime / Database / Selector / UrlProtocol / Helpers / Update / 占位）

磁盘、剪贴板、文件对话框、更新检查、进程与会话全部换平台实现；Overlay/RPC/Gamepad 为 M4 占位裁剪。

#### `Features/PlayTime/PlayTimeRecordService.cs`

- **L26-28 `_recordingPids`/`IsRecording`；L48 TryAdd；L321/L371 IsRecording 判断** ｜ api 平台 API ｜ reasonable 合理换法
  - 上游：`var instance = AppInstance.FindOrRegisterForKey($"playtime_{pid}"); if (!instance.IsCurrent) { LogWarning(...); return; }` / 扫描进程时 `App.FindInstanceForKey($"playtime_{pid}")`（机器级跨进程唯一）
  - 移植：`static readonly ConcurrentDictionary<int,byte> _recordingPids`；`if (!_recordingPids.TryAdd(pid, 0))` + finally TryRemove；扫描时 `if (IsRecording(process.Id)) continue`
  - 理由：AppLifecycle/AppInstance 是 WinRT 专属，Linux 无实现，只能退化为进程内去重。正常启动路径每次只 spawn 一个 recorder，且 PlayTimeStats 有 UNIQUE(GameBiz,StartTime,Pid) 兜底（DatabaseService.cs:1055），只有并发第二次 playtime 调用且 StartTime 走了回退分支才会重复计。
- **L78-107 Linux 分支（WaitForSessionEndAsync + 20s WhenAny + 30s 心跳 + Interruption=false 收尾）** ｜ linux 专属实现 ｜ must 必需
  - 上游：`while (true) { await Task.Delay(Random.Shared.Next(800, 1200)); if (process.HasExited) { Log(Stop); SavePlayTimeStats(biz,pid,process.StartTime,now); break; } if (sw.ElapsedMilliseconds - last > 30000) Log(Play); }`
  - 移植：`Task session = PlayTimeSession.WaitForSessionEndAsync(pid, prefix, cts.Token); while (!session.IsCompleted) { await Task.WhenAny(session, Task.Delay(20000)); ... if (sw.ElapsedMilliseconds - last > 30000) { Log(Play); SavePlayTimeStats(..., interruption: true); } }` 结束后 cts.Cancel() 再写 Stop/Interruption=false
  - 理由：Linux 的 PID 是 wine loader，HasExited 会过早为真（Proton 变体先起 steam.exe 存根），必须换成「前缀里的 wineserver 何时退出」。心跳写入 Interruption=true 的行让启动器被杀也不丢整段时长——Interruption 列上游本来就有，这条可回流。（可回流上游）
- **L58-77 ArgumentException 分支；L144-160 `GetLastStartTime`** ｜ bugfix 修上游缺陷 ｜ reasonable 合理换法
  - 上游：`var process = Process.GetProcessById(pid); LogStartState(biz, process); ... SavePlayTimeStats(biz, pid, process.StartTime, now)` —— 拿不到 PID 时靠 catch 记 Error，整段时长丢失
  - 移植：`catch (ArgumentException) { DateTime? lastStart = GetLastStartTime(biz, pid); if (lastStart - now > 12h) lastStart = null; startTime = lastStart ?? DateTime.Now; Log(Start, ...); }`（`SELECT * FROM PlayTimeItem WHERE GameBiz=@biz AND Pid=@pid ORDER BY TimeStamp DESC LIMIT 1`）
  - 理由：wine/Proton 脚本 exec 后 loader 进程可能秒退，子 recorder 起来时 PID 已不可查；拿最近一条 Start 当起点，避免时长记成 0 或整段丢失。代价是 pid 复用时起点可能属于上一次会话（仅用 12h 兜底）。
- **L261-290 `CreatePlaytimeStartInfo`；L357-375 进程名校验** ｜ linux 专属实现 ｜ must 必需
  - 上游：`new ProcessStartInfo { FileName = AppConfig.StarwardExecutePath, Arguments = $"playtime --biz {biz} --pid {pid}", CreateNoWindow = true }`；`if (process.ProcessName != name) { LogWarning("Not the expected process"); return null; }`
  - 移植：同一 StartInfo 额外写 `Environment["LUNARWARD_WINEPREFIX"]/["LUNARWARD_WINE"]/["LUNARWARD_WINESERVER"]`（prefix = `PlayTimeSession.ActivePrefix ?? GameLauncherService.ResolvePrefix(biz.Value)`）；Linux 下 `process.ProcessName is not ("wine" or "wine64" or "wine-preloader")` 只 LogWarning 不返回
  - 理由：recorder 子进程必须继承「这次启动真正用的前缀与 wine」，否则它自己 ResolvePrefix 会按 env→~/.wine→prefix/<game> 顺序猜错前缀；Linux 的 comm 是 wine 而不是游戏名，按上游原名判定会直接放弃记录。

#### `Features/PlayTime/PlayTimeButton.xaml.cs`

- **L63-67 `RefreshPlayTime(GameBiz)`**｜linux 专属实现｜reasonable 合理换法
  - 上游：无此方法；总时长只在 `UserControl_Loaded` 里读 `DatabaseService.GetValue<TimeSpan>($"playtime_total_{gameBiz}")`（上游页面每次导航都重建控件）
  - 移植：`public void RefreshPlayTime(GameBiz gameBiz) { CurrentGameBiz = gameBiz; InitializePlayTime(); }`；调用点 Features/GameLauncher/GameLauncherPage.xaml.cs:118
  - 理由：D94 页面缓存（不复建控件）后 Loaded 不再触发，切换游戏必须显式重读总时长，否则按钮上一直显示上一个游戏的时间。

#### `Features/PlayTime/PlayTimeSession.Linux.cs`

- **全文件（L1-287）：`WaitForSessionEndAsync` / `WaitForWineProcessAsync` / `HasWineProcessForPrefix` / `ResolveWineserver`** ｜ linux 专属实现 ｜ must 必需
  - 上游：无对应文件；会话结束只看 `process.HasExited`（see Features/PlayTime/PlayTimeRecordService.cs:52-59）
  - 移植：先 `WaitForWineProcessAsync(prefix,pid,30s)`（读 /proc/*/environ 找 `WINEPREFIX=<prefix>`）确认会话真起来了，再跑 `wineserver -w`（env WINEPREFIX=prefix、WINEDEBUG=-all）阻塞等待；拿不到 wineserver 时退回 /proc 进程树轮询 `HasLiveWineProcess`
  - 理由：wine 会话的生命周期由 wineserver 决定，被 exec 的 PID 退出不等于会话结束（实测绝区零：存根退出、游戏是孙进程，直接盯 PID 会判定「0 秒游玩」并提前释放前缀锁）。GameLauncherService.Linux.cs:172 的前缀锁释放与 RunExitCompanions 也复用同一判定。

#### `Features/Database/DatabaseService.cs`

- **L9 `using System.IO.Compression`；L102-123 `AutoBackupToAppDataLocal`；L1075-1102 新增 `BackupToArchive` / `CreateArchive`** ｜ api 平台 API ｜ must 必需
  - 上游：`new SharpSevenZipCompressor().CompressFiles(archive_tmp, file);`，备份扩展名 `.7z`，`Directory.GetFiles(folder, "StarwardDatabase_AutoBackup_*.7z")`
  - 移植：`ZipFile.Open(archivePath, ZipArchiveMode.Create)` + `zip.CreateEntryFromFile(sourceFile, Path.GetFileName(sourceFile), CompressionLevel.SmallestSize)`，扩展名 `.zip`；新增 `BackupToArchive(archivePath)`（临时目录 `.lwdb_<guid>` + `LunarwardDatabase_{yyyyMMdd_HHmmss}.db`，归档后 File.Move 覆盖，finally 清理）供 CLI `--database-backup`
  - 理由：SharpSevenZip 依赖 Windows 7z.dll；BCL ZipArchive 足够，且归档内仍只有一个 .db 条目（早期把整个备份目录打进去造成套娃，已在注释里留档）。7 天轮换与 BackupDatabase(VACUUM+Backup) 逻辑保持不变。

#### `Features/GameSelector/GameBizDisplay.cs`

- **L8 `public partial class GameBizDisplay : ObservableObject`；L18-31 CardWidth/CardHeight** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：`public class GameBizDisplay`；卡片尺寸写死在 XAML（Width=192 Height=108）
  - 移植：`public double CardWidth { get => field; set => SetProperty(ref field, value); }` 默认 192，CardHeight 默认 108，同写法可通知
  - 理由：Uno/Skia 没有横向滚动条，卡片必须按可用宽度均分，尺寸得可绑定、可通知（D56）。

#### `Features/GameSelector/GameSelector.xaml`

- **L105 `<ScrollViewer x:Name="ScrollViewer_GameBizs" SizeChanged="ScrollViewer_GameBizs_SizeChanged">`；L118-119 `Width="{x:Bind CardWidth, Mode=OneWay}" Height="{x:Bind CardHeight, Mode=OneWay}"`** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：`<ScrollViewer>`（匿名）；卡片 Grid `Width="192" Height="108"`
  - 移植：命名并挂 SizeChanged；卡片宽高改为 x:Bind 到 GameBizDisplay.CardWidth/CardHeight
  - 理由：同上：Uno 下横向滚动条缺失，只能由父容器宽度反算卡片尺寸；绑定而非代码 set 属性是为了复用 XAML 模板。

#### `Features/GameSelector/GameSelector.xaml.cs`

- **L470-505 `ScrollViewer_GameBizs_SizeChanged`/`UpdateGameCardSize`；L701 `DispatcherQueue?.TryEnqueue(DispatcherQueuePriority.Low, UpdateGameCardSize)`** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：无（尺寸固定 192x108，不需要重算）
  - 移植：`double avail = ScrollViewer_GameBizs.ActualWidth - 12; cardWidth = Math.Clamp(Math.Floor((avail - 16*(count-1))/count), 140, 192); cardHeight = Math.Round(cardWidth*108.0/192.0);` 异常安全（catch 后 Serilog.Log.Debug）
  - 理由：把写死的卡片尺寸按窗口宽度均分并保持 16:9；下限 140 防止 4 个以上游戏时卡片被压成不可点。
- **L930-960 硬链接去重键（`var key = $\"{file.Length}_{file.LastWriteTimeUtc.Ticks}_{file.FullName.GetHashCode():X8}\"`，L953）** ｜ linux 专属实现 ｜ avoidable 可避免
  - 上游：`using var handle = File.OpenHandle(...); var idInfo = Kernel32.GetFileInformationByHandleEx<Kernel32.FILE_ID_INFO>(handle, ...); dic[Convert.ToHexString(idInfoBytes)] = file.Length;`（按卷号+128bit FileId 去重）
  - 移植：改成长度+修改时间+路径哈希作为键，`dic[key] = file.Length`
  - 理由：FILE_ID_INFO 是 Win32 API，但 Linux 有等价的 inode（Helpers/PosixIdentity.cs 已能 stat），完全可以按 (st_dev, st_ino) 去重；换成路径哈希后不同路径的硬链接不再合并，统计会偏大。
- **L1005-1008 `if (!OperatingSystem.IsWindows()) { continue; }`（AutoSearchInstalledGames 注册表分支前）** ｜ api 平台 API ｜ must 必需
  - 上游：读 HKLM/HKCU 下的米哈游安装信息与 dialog 注册表项来定位 install path
  - 移植：非 Windows 直接跳过整个注册表分支；安装位置改由 `GameInstallScanner.ScanAndApply()`（Features/GameInstall/GameInstallScanner.Linux.cs，App.xaml.cs:89 启动时调用）扫描目录得到
  - 理由：Linux 无 Win32 注册表；盘符/安装路径来源换成目录扫描，能覆盖「一键导入已安装游戏」，但不再能自动发现装在任意位置的本体。
- **L241-252 `CurrentGameChanged += ... HideSelectGameTip()`；L381-390 `#if DEBUG DevSelectGame`；L590 `CloseLayer()`** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：TeachingTip 打开后由框架自行关闭；无 DevSelectGame/CloseLayer
  - 移植：切换游戏时显式 `TeachTip_SelectGame.IsOpen = false`；DEBUG 下 `DevSelectGame(GameBiz)` 直接切游戏（打 `[dev] switched game to {biz}`）；`CloseLayer() => HideFullBackground()` 供 Esc 用
  - 理由：Uno 的 TeachingTip 不会因为底层内容变化自动收起。DevSelectGame 是 L-36 的验证钩子（LUNARWARD_DEV_SELECTOR=1 自动展开选择层），只在 DEBUG 编入，不违反 D35。

#### `Features/UrlProtocol/UrlProtocolService.Linux.cs`

- **L33-131 `RegisterProtocol` / `UnregisterProtocol` / `IsProtocolRegistered` / `RunQuiet`** ｜ api 平台 API ｜ must 必需
  - 上游：`Registry.SetValue(@"HKEY_CURRENT_USER\Software\Classes\Starward", ...)`，scheme 为 `Starward`，DefaultIcon `Starward.exe,1`，Command = `"{exe}" "%1"`
  - 移植：写 `$XDG_DATA_HOME/applications/lunarward.desktop`（`MimeType=x-scheme-handler/lunarward`、`Exec="{ExecutablePath}" %u`、Icon 指向 Assets/lunarward.png），再 `update-desktop-database` + `xdg-mime default lunarward.desktop x-scheme-handler/lunarward`；`IsProtocolRegistered()` 用 `xdg-mime query default` 的输出判定
  - 理由：Linux 注册协议走 .desktop + xdg-mime，且只写用户级目录、不请求 root；scheme 名随之从 starward:// 变为 lunarward://，判定「是否已注册」也从看文件改成问默认处理器。
- **全文件：`HandleUrlProtocolAsync` 未移植；深链改在 App.xaml.cs:246-320 `ExpandUrlArgument` + Features/App/SingleInstancePipe.cs:33-50** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：`HandleUrlProtocolAsync(string url)` 支持 host=`startgame`（含 `install_path` 查询参数）、`playtime`（pid）、`test`，并从 AppInstance.Activated 收到 URL
  - 移植：`ExpandUrlArgument` 只认 `lunarward://startgame?biz=…`，其余 host 原样返回当普通 argv；`SingleInstancePipe.NotifyPrimary` 也只转发 show/exit/startgame
  - 理由：Linux 的激活入口变成 argv/xdg-mime，只补了用户实际会点的 startgame；playtime/test 深链与 install_path 参数被丢弃（详见 bugs）。

#### `Features/UrlProtocol/TestUrlProtocolWindow.xaml`

- **L12 Image Source** ｜ cosmetic 外观文案 ｜ reasonable 合理换法
  - 上游：`ms-appx:///Assets/logo.png`
  - 移植：`ms-appx:///Assets/lunarward.png`
  - 理由：品牌资源改名（Assets/logo.png 在移植树里已被 lunarward.png 取代）。

#### `Features/CloudGame/CloudGameButton.xaml.cs`

- **L178** ｜ api 平台 API ｜ must 必需
  - 上游：`await Launcher.LaunchUriAsync(new Uri(url));`
  - 移植：`Starward.Helpers.OpenUrl.Open(url);`
  - 理由：`Launcher.LaunchUriAsync` 是 WinRT API；Linux 走 OpenUrl（xdg-open/portal 回退）。云游戏本体仍是 M4 占位。

#### `Helpers/PointerCursor.cs`

- **L8 类声明**｜cosmetic 外观文案｜must 必需
  - 上游：`public class PointerCursor : DependencyObject`
  - 移植：`public partial class PointerCursor : DependencyObject`
  - 理由：加 partial 给分部声明/生成代码留位，无语义变化（注释称去掉会 CS0260，实际当前树只此一处声明）。

#### `Helpers/FileDialogHelper.Linux.cs`

- **全文件（L17-204）：四个入口 + `DetectTool`/`AppendFilters`/`RunAsync`** ｜ api 平台 API ｜ must 必需
  - 上游：`FileOpenPicker`/`FileSavePicker`（WinRT）+ COM `IFileOpenDialog` 兜底；parentWindow 由 `xamlRoot.ContentIslandEnvironment.AppWindowId.Value` 取；取消返回 null
  - 移植：xdg-desktop-portal 优先（`Portal.PickFilesAsync(...)`，Handled 即返回），否则 kdialog（`--getopenfilename/--getsavefilename/--getexistingdirectory`）或 zenity（`--file-selection [--save|--directory]`）；parentWindow 参数被忽略；`p.ExitCode == 0 ? stdout : null` 把取消统一成 null
  - 理由：Linux 没有 WinRT picker；portal 优先是为了 flatpak/纯 Wayland 下也能看见宿主文件（kdialog/zenity 在沙盒里常看不见），全都不存在时返回 null 由上层按「用户取消」处理。

#### `Helpers/ClipboardHelper.Linux.cs`

- **L20-59 `SetText`** ｜ api 平台 API ｜ must 必需
  - 上游：`data.SetText(value); Clipboard.SetContent(data);`（WinRT 同步内存操作，无外部进程）
  - 移植：依次尝试 `wl-copy --` → `xclip -selection clipboard` → `xsel --clipboard --input`：Process.Start + 写 stdin + `p.WaitForExit(2000)`，全部失败 Debug.WriteLine
  - 理由：Linux 无 WinRT 剪贴板；Wayland 优先、X11 兜底（三者都没有就静默失败）。同步 WaitForExit 的代价见 bugs。
- **L11-18 `SetStorageItems`；缺 `SetBitmap(3 重载)` 与 `GetTextAsync`** ｜ cut 裁剪 ｜ avoidable 可避免
  - 上游：`data.SetStorageItems(items); Clipboard.SetContent(data);` 另有 `SetBitmap(IRandomAccessStream/IStorageFile/Uri)`、`GetTextAsync()`
  - 移植：`var paths = items.Select(x => x.Path) ...; SetText(string.Join("\n", paths));`；SetBitmap*/GetTextAsync 在移植树里不存在（调用点已一并删除）
  - 理由：文件剪贴板要写成 text/uri-list 才算数（见 bugs）；贴图给 BBS 与读剪贴板文本的功能在 Linux 侧没有保留。

#### `Helpers/DriveHelper.Linux.cs`

- **全文件（L1-110）：`IsDeviceRemovableOrOnUSB`/`GetDriveType`/`GetDriveAvailableSpace`/`GetDriveFormat`** ｜ api 平台 API ｜ must 必需
  - 上游：Vanara Kernel32：`CreateFile`+`DeviceIoControl(STORAGE_DEVICE_DESCRIPTOR)` 判 `BusTypeUsb`、`GetDriveType`、`GetDiskFreeSpaceEx`、`GetVolumeInformation`
  - 移植：/proc/mounts 解析 + `DriveInfo`：可移动性按挂载点前缀（/media、/run/media、/mnt）+ 最长挂载匹配判断；`GetDriveType` 按 fs 名映射（nfs/cifs/smb3/sshfs→Network、tmpfs/devtmpfs→Ram、其余 Fixed，**从不返回 Removable**）；`GetDriveFormat` 恒返回 ""；`GetDriveAvailableSpace` 用 `new DriveInfo(Path.GetPathRoot(...))`
  - 理由：P/Invoke 全是 Win32；fs 名映射足以支撑「网络盘」「内存盘」两处判断，NTFS 硬链接优化在 Linux 上本就在 GameInstallService 里被 `GetDriveFormat is "NTFS"` 短路掉（安全跳过）。可用空间取根文件系统的问题见 bugs。

#### `Helpers/XamlRootExtension.Linux.cs`

- **L12-38 `GetAppWindow` / `GetWindowHandle` / `SetWindowDragRectangles`** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：`AppWindow.GetFromWindowId(xamlRoot.ContentIslandEnvironment.AppWindowId.Value)`；`GetWindowHandle() => AppWindow.Id.Value`；`SetWindowDragRectangles` 按 `RasterizationScale` 换算 Rect
  - 移植：`GetAppWindow` 先试 `xamlRoot.ContentIslandEnvironment.AppWindowId`（Uno 抛 NotImplemented）再回落 `MainWindow.Current?.AppWindow`，失败返回 null；`GetWindowHandle() => 0`；`SetWindowDragRectangles` no-op；缺 `GetUIScaleFactor()` 与 RectInt32 重载
  - 理由：Uno 没有 ContentIsland/AppWindow.Id（恒 0x1），窗口拖动区由原生窗口装饰负责；保留的调用点是热键（传 0 不影响）与自绘标题栏拖动（Linux 不需要）。GetUIScaleFactor 的调用点都在被 <Compile Remove> 的 AppBackground.xaml.cs 里。

#### `Helpers/SystemBackdropHelper.Linux.cs`

- **L16-105：`TrySetMica(bool isDark = true)`/`TrySetAcrylic(bool isDark = true)`/`Apply`；`SystemBackdropProperty`（去 required）与 `MicaAltDefault.TintOpacityLight` 0.5→0** ｜ engine 引擎能力 ｜ reasonable 合理换法
  - 上游：MicaController/DesktopAcrylicController + SystemBackdropConfiguration，`TrySetMica(bool useMicaAlt = false, bool fallbackToAcrylic = false, bool alwaysActive = false)`/`TrySetAcrylic(bool alwaysActive = false)` 返回 bool，TintColor/TintOpacity/LuminosityOpacity 真正生效
  - 移植：只用 `_isDark ? FallbackColorDark : FallbackColorLight` 给 `_window.Content` 铺一层 SolidColorBrush，完全忽略 Tint*；两个 Try* 返回 `this`（fluent）；`ColorExtensions.ToColor(this uint)` 顶替上游 UInt32ToColorHelper
  - 理由：Skia/Uno 下没有 Mica 与丙烯合成器（无 DWM），只能纯色兜底。参数语义从 alwaysActive 变成 isDark，唯一调用点 Features/ViewHost/SystemTrayWindow.xaml.cs:39 传 true 恰好两种语义都合理。

#### `Helpers/DataFolder.Linux.cs`

- **全文件（L13-76）：`AppFolderName="lunarward"`/`DataHome`/`Unify`**｜linux 专属实现｜reasonable 合理换法
  - 上游：无此文件（Windows 上只有一个 %LocalAppData%\Starward）
  - 移植：XDG_DATA_HOME（否则 ~/.local/share）下的 `lunarward` 为准；启动早期把 Uno 按程序集名建的大写 `Lunarward` 目录内的文件搬进小写目录，再把大写目录替换成指向小写的符号链接（已是链接则直接返回）
  - 理由：Uno 的 ApplicationData 目录名取自程序集名（大写 L），与 XDG 小写目录分裂成两份；Unify 合并一次后 Uno 的写入经符号链接落回同一目录，避免「日志/设置在别的目录里」类问题。

#### `Helpers/WindowWaitAsync.Linux.cs`

- **全文件（L1-30）：`WindowWaitAsyncExtension.WaitAsync(this Window)`**｜api 平台 API｜avoidable 可避免
  - 上游：无此文件；用 Scighost.WinUI 包的 `Window.WaitAsync()` 扩展
  - 移植：`TaskCompletionSource<bool>` + `window.Closed += (_, _) => tcs.TrySetResult(true)`，返回 `Task<bool>`
  - 理由：Scighost.WinUI 是 Windows 包，Linux 构建里没有该扩展。调用点是 AppConfig.Configuration.cs:107/166 `await new NoPermissionWindow(...).WaitAsync()` 这类「等用户关窗」的模态流程。未预检「窗口已关」，但那两个调用点都是先 new 再等。

#### `Features/Update/UpdateService.Linux.cs`

- **L16-103 全文件** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：533 行完整自更新：Polly 重试 + ReleaseClient + ReleaseManifest + NuGet.Versioning 比较 + Sha256 校验 + zstd 解压 + HPatch 增量 + Backup/RestoreVersionIni + DeleteOldFiles；`CheckUpdateAsync(bool disableIgnore)` 会跳过 AppConfig.IgnoreVersion
  - 移植：只查 AUR RPC `https://aur.archlinux.org/rpc/v5/info?arg[]=lunarward`（HttpClient Timeout=8s），`Version.Split('-')[0]` 去掉 pkgrel，`Version.TryParse`+`IsNewer` 比较后返回只填 Version/BuildTime 的 ReleaseInfoDetail；`CheckUpdateAsync(disableIgnore)` 直接丢弃该参数；`DownloadSetupAsync` 仅 Debug.WriteLine
  - 理由：D5：Linux 不做自我替换升级，交给 AUR/AppImage/包管理器；网络异常一律当「无更新」（D33 不把失败伪装成成功）。

#### `Features/Update/SetupService.Linux.cs`

- **全文件（L9-17）** ｜ cut 裁剪 ｜ must 必需
  - 上游：147 行：ReleaseClient 取 detail、FileHashHelper 校验、Range 断点续传 3 次、`Process.Start(setupPath, Verb="runas", Arguments="update --InstallFolder ... --pid ...")`
  - 移植：构造签名 `(ILogger<SetupService>, HttpClient, ReleaseClient)` 保留但空体，只有 `SetupTotalBytes`/`SetupDownloadBytes` 两个属性
  - 理由：Starward.Setup 是 Windows 安装器（runas 提权 + 自替换 + 安装目录登记）；Linux 无对应模型，保留类型只为 DI 与页面可编译。

#### `Features/Update/UpdateWindow.Linux.cs`

- **L14-94 全文件；配套 Features/Update/UpdateWindow.LinuxAuto.cs:14-17 四个空事件处理器** ｜ cut 裁剪 ｜ reasonable 合理换法
  - 上游：776 行：RootGrid_Loaded 里 LoadUpdateContentAsync（WebView2 渲染 changelog）、下载/安装进度、IgnoreVersion 落盘、HyperlinkButton_Click 打开发布页/完整包、Finish 后自动重启
  - 移植：只保留展示字段（NewVersion/ChannelText/BuildTime）与 RemindMeLatter/IgnoreThisVersion/Retry/Restart 命令；`IsUpdateNowEnabled => false`；RootGrid_Loaded/HyperlinkButton_Click/PointerEntered 全是 M1 编译清零时生成的空实现
  - 理由：没有 WebView2（引擎缺口）也没有自更新管线；窗口只在 MainView 发现 AUR 版本更新时打开，承担「有新版本，去包管理器升级」的告知。

#### `Features/Overlay/RunningGameService.Linux.cs`

- **全文件（L1-79），含内联的 `RunningGame` 类（上游独立文件 Features/Overlay/RunningGame.cs 已被 <Compile Remove>）** ｜ cut 裁剪 ｜ must 必需
  - 上游：`User32.SetWinEventHook(EVENT_SYSTEM_FOREGROUND, ...)` 追踪最近活跃游戏 + `System.Timers.Timer` 清退 + `OpenOverlayWindow()` 弹 OverlayWindow + GamepadController 指南键接管/恢复
  - 移植：进程表 + `HasExited` 判存活（`_runningGames.RemoveAll(x => x.HasExited)`），`GetLatestActiveGame()` 返回最后加入者，`OpenOverlayWindow() => false`，不再调 GamepadController
  - 理由：M4 占位：悬浮窗依赖 Win32 窗口钩子/DWM 与「最近前台游戏」概念，Linux 侧只保留「有几个游戏在跑」的计数供 UI 使用。

#### `Features/RPC/RpcService.Linux.cs`

- **全文件（L1-43），另含 `RpcServerInfo` 类型**｜cut 裁剪｜must 必需
  - 上游：命名管道 + gRPC（Starward.RPC，net10.0-windows）：Env/Game 服务、提权启动 `rpc <magic> <pid>` 子进程、SetEnviroment（限速）、KeepRunningOnExited、StopRpcServer
  - 移植：`CheckRpcServerRunning() => false`；`GetRpcServerInfoAsync => null`；`EnsureRpcServerRunningAsync => false`；`KeepRunningOnExited` 空；`StopRpcServerAsync => Task.CompletedTask`；`CreateRpcClient<T>()` 抛 NotSupportedException
  - 理由：M4 占位（D6 计划换 Unix domain socket）。保留签名让 DI 与调用点可编译，RPC 相关功能视为不可用；App.xaml.cs:255 的 `rpc` 子命令也因此直接 Exit(0)。

#### `Features/GamepadControl/GamepadController.Linux.cs`

- **全文件（L1-56）** ｜ cut 裁剪 ｜ must 必需
  - 上游：SharpGameInput + DispatcherQueue（1008 行）：指南键/分享键映射、模拟输入、GameBar 接管、Redist 安装检测
  - 移植：全静态空实现；`NeedInstallGameInputRedist => false`、`GameInputRedistInstalled => true`、`GameInputRedistOutdate => false`；`SetGamepadGuideButtonMapKeys/SetGamepadShareButtonMapKeys` 返回 false 且 `keysTextOrErrorKey = "Gamepad_NotSupportedOnLinux"`
  - 理由：M4 占位：Windows GameInput 在 Linux 不存在，计划改走 evdev/uinput；先让设置页能显示「不支持」而不是崩溃或假装成功。

## 4 · 纯新增文件（不是偏离，但同名文件的行为由它们兜底）

移植树里上游没有的文件，按目录统计（不含 `*.Linux.cs`，那是 §3 的替代实现）：

| 目录 | 新增 .cs | 例子 |
|---|---|---|
| `Features/Runtime` | 26 | `ProfileShellService.cs`、`OutputCapture.cs`、`LastKnownGoodStore.cs` |
| `Features/GameInstall` | 14 | `GameInstallShims.cs`、`InstallGameDialog.LinuxAuto.cs`、`ProtoReader.cs` |
| `Features/Screenshot` | 6 | `ImageViewWindow.LinuxAuto.cs`、`ScreenshotPage.LinuxAuto.cs`、`ScreenshotWindowsDisabled.cs` |
| `obj/Release` | 6 | `uno.reload.cookie.g.cs`、`.NETCoreApp,Version=v10.0.AssemblyAttributes.cs`、`uno.reload.cookie.g.cs` |
| `Features/GameRecord` | 5 | `WebLoginService.cs`、`BattleChronicleWindow.LinuxAuto.cs`、`LoginPage.LinuxAuto.cs` |
| `Features/Setting` | 5 | `SettingNavigation.cs`、`RuntimeEnvironmentSetting.Components.cs`、`RuntimeEnvironmentSetting.Enhancements.cs` |
| `Features/Companion` | 4 | `CompanionEntry.cs`、`CompanionStore.cs`、`CompanionAutoStart.cs` |
| `obj/Debug` | 4 | `uno.reload.cookie.g.cs`、`.NETCoreApp,Version=v10.0.AssemblyAttributes.cs`、`HotReloadInfo.Attribute.g.cs` |
| `Features/GameAccount` | 3 | `RegistryFormat.cs`、`WineRegistryFiles.cs`、`WineRegistry.cs` |
| `Features/GameLauncher` | 3 | `LaunchReport.cs`、`GameLauncherPage.InstallTask.cs`、`LaunchPreflight.cs` |
| `Features/Background` | 2 | `VideoThumbnailer.cs`、`VideoBackgroundPlayer.cs` |
| `Features/App` | 2 | `SingleInstancePipe.cs`、`DevSwitches.cs` |
| `LinuxConfig.cs` | 1 | `LinuxConfig.cs` |
| `Controls/ToolkitCompat.cs` | 1 | `ToolkitCompat.cs` |
| `Controls/InteractivityCompat.cs` | 1 | `InteractivityCompat.cs` |
| `Controls/ImageExCompat.cs` | 1 | `ImageExCompat.cs` |
| `Controls/Win2DCompat.cs` | 1 | `Win2DCompat.cs` |
| `Controls/ToolkitControls.cs` | 1 | `ToolkitControls.cs` |
| `Features/Overlay` | 1 | `OverlayWindow.LinuxAuto.cs` |
| `Features/Update` | 1 | `UpdateWindow.LinuxAuto.cs` |
| `Features/ViewHost` | 1 | `CurrentGameChangedMessage.cs` |
| `Features/Tray` | 1 | `StatusNotifierTray.cs` |
| `Features/Dev` | 1 | `UiSelfTest.cs` |
| `Helpers/X11KeyState.cs` | 1 | `X11KeyState.cs` |
| `Helpers/JsonFile.cs` | 1 | `JsonFile.cs` |
| `Helpers/Housekeeping.cs` | 1 | `Housekeeping.cs` |
| `Helpers/X11Window.cs` | 1 | `X11Window.cs` |
| `Helpers/OpenUrl.cs` | 1 | `OpenUrl.cs` |
| `Helpers/DiagnosticBundle.cs` | 1 | `DiagnosticBundle.cs` |
| `Helpers/AppIcon.cs` | 1 | `AppIcon.cs` |
| `Helpers/UiWatchdog.cs` | 1 | `UiWatchdog.cs` |
| `Helpers/DesktopNotification.cs` | 1 | `DesktopNotification.cs` |
| `Helpers/ProcessQuery.cs` | 1 | `ProcessQuery.cs` |
| `Helpers/Portal.cs` | 1 | `Portal.cs` |
| `Helpers/MagickCodec.cs` | 1 | `MagickCodec.cs` |
| `Helpers/ImageDecode.cs` | 1 | `ImageDecode.cs` |
| `Helpers/PosixIdentity.cs` | 1 | `PosixIdentity.cs` |
| `Helpers/CliSpec.cs` | 1 | `CliSpec.cs` |
| `Helpers/OneShotHints.cs` | 1 | `OneShotHints.cs` |
| `Properties/AssemblyVersion.cs` | 1 | `AssemblyVersion.cs` |
| `Compat/VanaraHotkeyCompat.cs` | 1 | `VanaraHotkeyCompat.cs` |
| `Compat/NotifyIcon` | 1 | `TaskbarIconCompat.cs` |

合计 **110** 个新增 `.cs`。运行环境（`Features/Runtime/`）、伴随启动（`Features/Companion/`）、托盘与窗口底座、Dev CLI 是新增的主体。

## 附录 A · `<Compile Remove>` 排除清单（53 条）

| 上游文件 | 移植侧替代 | 说明 |
|---|---|---|
| `Controls/CachedImage.cs` | `CachedImage.Linux.cs` | 同名能力由 `Controls/CachedImage.Linux.cs` 重写（平台 API 不同） |
| `Features/Background/AccentColorHelper.cs` | `AccentColorHelper.Linux.cs` | 同名能力由 `Helpers/AccentColorHelper.Linux.cs` 重写（平台 API 不同） |
| `Features/Background/AppBackground.xaml.cs` | — | Windows 桌面 API（DWM/系统 UI/透明背景/注册表）在 Linux 无对应 |
| `Features/Codec/HDR10GammaEffect.cs` | — | 图像/视频编解码改用 Skia、Magick.NET、SharpCompress（`ImageLoader.Linux.cs`、`CachedImage.Linux.cs`） |
| `Features/Codec/ImageInfo.cs` | — | 图像/视频编解码改用 Skia、Magick.NET、SharpCompress（`ImageLoader.Linux.cs`、`CachedImage.Linux.cs`） |
| `Features/Codec/ImageLoader.cs` | `ImageLoader.Linux.cs` | 同名能力由 `Features/Codec/ImageLoader.Linux.cs` 重写（平台 API 不同） |
| `Features/Codec/ImageSaver.cs` | — | 图像/视频编解码改用 Skia、Magick.NET、SharpCompress（`ImageLoader.Linux.cs`、`CachedImage.Linux.cs`） |
| `Features/Codec/ImageThumbnail.cs` | — | 图像/视频编解码改用 Skia、Magick.NET、SharpCompress（`ImageLoader.Linux.cs`、`CachedImage.Linux.cs`） |
| `Features/Codec/SrgbGammaEffect.cs` | — | 图像/视频编解码改用 Skia、Magick.NET、SharpCompress（`ImageLoader.Linux.cs`、`CachedImage.Linux.cs`） |
| `Features/Codec/UhdrGainmapEffect.cs` | — | 图像/视频编解码改用 Skia、Magick.NET、SharpCompress（`ImageLoader.Linux.cs`、`CachedImage.Linux.cs`） |
| `Features/Codec/VP9Helper.cs` | — | 图像/视频编解码改用 Skia、Magick.NET、SharpCompress（`ImageLoader.Linux.cs`、`CachedImage.Linux.cs`） |
| `Features/GameInstall/GameInstallService.cs` | `GameInstallService.Linux.cs` | 同名能力由 `Features/GameInstall/GameInstallService.Linux.cs` 重写（平台 API 不同） |
| `Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.cs` | — | 平台专属实现被删除，能力在 `*.Linux.cs` 或新模块中重做 |
| `Features/GameInstall/GameInstallTaskStartedMessage.cs` | — | 安装对话框与任务模型由 `InstallGameDialog.Linux.cs`、`PreDownloadDialog.Linux.cs`、`GameInstallService.Linux.cs` 重写 |
| `Features/GameInstall/InstallGameDialog.xaml.cs` | — | 安装对话框与任务模型由 `InstallGameDialog.Linux.cs`、`PreDownloadDialog.Linux.cs`、`GameInstallService.Linux.cs` 重写 |
| `Features/GameInstall/PreDownloadDialog.xaml.cs` | — | 安装对话框与任务模型由 `InstallGameDialog.Linux.cs`、`PreDownloadDialog.Linux.cs`、`GameInstallService.Linux.cs` 重写 |
| `Features/GameRecord/BBSWebBridge.xaml.cs` | — | M4 占位：明确不做（AGENTS §8） |
| `Features/GameRecord/BattleChronicleWindow.xaml.cs` | — | M4 占位：明确不做（AGENTS §8） |
| `Features/GameRecord/LoginPage.xaml.cs` | — | 战绩窗口/登录页依赖 WebView2 与米游社桥，未移植 |
| `Features/GamepadControl/GamepadController.cs` | `GamepadController.Linux.cs` | 同名能力由 `Features/GamepadControl/GamepadController.Linux.cs` 重写（平台 API 不同） |
| `Features/GamepadControl/SimulateInputTipsWindow.xaml.cs` | — | M4 占位：明确不做（AGENTS §8） |
| `Features/Overlay/OverlayWindow.xaml.cs` | — | M4 占位：明确不做（AGENTS §8） |
| `Features/Overlay/RunningGame.cs` | — | M4 占位：明确不做（AGENTS §8） |
| `Features/Overlay/RunningGameService.cs` | `RunningGameService.Linux.cs` | 同名能力由 `Features/Overlay/RunningGameService.Linux.cs` 重写（平台 API 不同） |
| `Features/RPC/RpcService.cs` | `RpcService.Linux.cs` | 同名能力由 `Features/RPC/RpcService.Linux.cs` 重写（平台 API 不同） |
| `Features/Screenshot/ImageBatchConvertWindow.xaml.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ImageViewWindow.xaml.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ImageViewWindow2.xaml.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ScreenCaptureContext.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ScreenCaptureHelper.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ScreenCaptureInfoWindow.xaml.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ScreenCaptureItem.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ScreenCaptureService.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ScreenshotHelper.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ScreenshotPage.xaml.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ScreenshotPage2.xaml.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Screenshot/ScrollViewerExtensions.cs` | — | 抓屏/图库整体换实现（`ScreenCapture.Linux.cs`、`ScreenshotPage.Linux.cs`、`ImageViewWindow.Linux.cs`） |
| `Features/Setting/HotkeyManager.cs` | `HotkeyManager.Linux.cs` | 同名能力由 `Helpers/HotkeyManager.Linux.cs` 重写（平台 API 不同） |
| `Features/Setting/ScreenshotSetting.xaml.cs` | — | 设置页由 `SettingPage.Linux.cs` 等重写；热键改 X11 轮询（`HotkeyManager.Linux.cs`） |
| `Features/UrlProtocol/TestUrlProtocolWindow.xaml.cs` | — | 协议测试窗口无对应 UI |
| `Features/UrlProtocol/UrlProtocolService.cs` | `UrlProtocolService.Linux.cs` | 同名能力由 `Features/UrlProtocol/UrlProtocolService.Linux.cs` 重写（平台 API 不同） |
| `Frameworks/WindowEx.cs` | `WindowEx.Linux.cs` | 同名能力由 `Frameworks/WindowEx.Linux.cs` 重写（平台 API 不同） |
| `Helpers/AccentColorHelper.cs` | `AccentColorHelper.Linux.cs` | 同名能力由 `Helpers/AccentColorHelper.Linux.cs` 重写（平台 API 不同） |
| `Helpers/ClipboardHelper.cs` | `ClipboardHelper.Linux.cs` | 同名能力由 `Helpers/ClipboardHelper.Linux.cs` 重写（平台 API 不同） |
| `Helpers/DriveHelper.cs` | `DriveHelper.Linux.cs` | 同名能力由 `Helpers/DriveHelper.Linux.cs` 重写（平台 API 不同） |
| `Helpers/FileDialogHelper.cs` | `FileDialogHelper.Linux.cs` | 同名能力由 `Helpers/FileDialogHelper.Linux.cs` 重写（平台 API 不同） |
| `Helpers/SystemUIHelper.cs` | — | 平台专属实现被删除，能力在 `*.Linux.cs` 或新模块中重做 |
| `Helpers/TransparentBackdrop.cs` | — | 平台专属实现被删除，能力在 `*.Linux.cs` 或新模块中重做 |
| `Helpers/XamlRootExtension.cs` | `XamlRootExtension.Linux.cs` | 同名能力由 `Helpers/XamlRootExtension.Linux.cs` 重写（平台 API 不同） |
| `Features/Update/SetupService.cs` | `SetupService.Linux.cs` | 同名能力由 `Features/Update/SetupService.Linux.cs` 重写（平台 API 不同） |
| `Helpers/SystemBackdropHelper.cs` | `SystemBackdropHelper.Linux.cs` | 同名能力由 `Helpers/SystemBackdropHelper.Linux.cs` 重写（平台 API 不同） |
| `Features/Update/UpdateService.cs` | `UpdateService.Linux.cs` | 同名能力由 `Features/Update/UpdateService.Linux.cs` 重写（平台 API 不同） |
| `Features/Update/UpdateWindow.xaml.cs` | — | 更新改为包管理器/自更新脚本路径 |

> 为什么整份排除：Windows 专属 API 或 M4 占位功能。逐项实现状态见 `12-shims-and-gaps.md` §6。

## 附录 B · `avoidable` 清单（60 条，需要逐条决定）

> 2026-11-22 全量复核后按新类别重新生成。

| 文件 | 位置 | 一句话 | 类别 |
|---|---|---|---|

| `Features/GameLauncher/GameLauncherSettingDialog.xaml` | 上游第 148-152 行 FlipView.ItemsPanel(VirtualizingStackPanel Orientation=Vertical) 被删除 | Uno 的 FlipView 纵向排列时翻页手势/选中索引同步失效，改横向；代价是内容超过 480 高的设置页底部不可达 | engine |
| `Features/GameLauncher/GameLauncherSettingDialog.xaml` | 第 271、472 行附近（上游）三处 IsTextTrimmedChanged="TextBlock_IsTextTrimmedChanged" 被删除 | Uno 的 IsTextTrimmedChanged 不触发，绑着也无效果；但长路径/长名称现在直接省略号截断，不再自动缩字号 | cut |
| `Features/GameLauncher/GameLauncherPage.xaml.cs` | 第 1411-1420 行（DEV 钩子）LUNARWARD_DEV_BG_CYCLE | 调试自动化入口，已在 #if DEBUG 内（硬规则 6），但仍属于可删的插桩，Release 无副作用 | eng |
| `Features/GameLauncher/GameLauncherService.cs` | 第 381-384 行（对应上游第 379-382 行） | HDR 走注册表写 DWM 覆盖项，Linux 无意义；且该调用要在游戏进程之前起 wine（冷启动 4 秒卡 UI，L-62/L-127）。cmd 包装是 Windows 提权技巧，Linux 无效 | linux |
| `Features/GameInstall/InstallGameDialog.xaml` | L1（UTF-8 BOM）；L49–58（TextBlock_InstallationPath 属性换序） | 仅编码与属性书写顺序不同，模板、绑定与命名元素完全一致，渲染与行为无差异 | cosmetic |
| `Features/GameInstall/PreDownloadDialog.xaml` | L48、L57、L66（PackageSizeText / UnzipSpaceText / AvailableSpaceText 的 Run.Text） | 根元素 L12 已有 x:DefaultBindMode="OneWay"，三处显式 Mode 属冗余保险；XAML 层面无行为差异（真正的刷新问题在 code-behind 缺 INPC，见 bugs） | cosmetic |
| `Features/GameInstall/GameInstallService.Linux.cs` | L41–42 StartInstallAsync 的 mode 形参；L157 context.DownloadMode = Chunk | 只接了 chunk 下载，LDIFF 增量未实现；保留形参仅为对齐上游调用点（GameLauncherPage.ResumeDownloadAsync 等），调用方不会因签名不同而编译失败 | cut |
| `Features/GameInstall/GameInstallService.Linux.cs` | L45 StartUpdateAsync、L48 StartPredownloadAsync、L52 StartRepairAsync | 移植侧调用点（设置对话框、CLI、预下载按钮）常常只拿得到 GameId；回落避免调用方重复拼路径，语义等价于上游把 InstallPath 塞进请求 | api |
| `Features/GameInstall/GameInstallService.Linux.cs` | L177–215 CountLocalMismatchesAsync | 自研管线的兜底自检：只比文件大小，故意不做 MD5（代价高），仅作诊断线索，不改变状态机 | eng |
| `Features/GameInstall/GameInstallService.Linux.cs` | L218–254 EnsureInstallPathLooksSane | L-66 的教训：误把游戏装进已有数据目录会覆盖用户文件；Linux 侧路径由用户手选，风险比 Windows 的默认路径高 | linux |
| `Features/GameInstall/GameInstallService.Linux.cs` | L320–327 #if DEBUG 的下载量上限 | 冒烟/离线测试用（硬规则 6：Release 构建里不存在），避免测试真下几十 GB | linux |
| `Features/GameInstall/InstallGameDialog.Linux.cs` | L23 _audioLanguage、L51–57 构造函数 | 只保留了 x:Bind 需要的那一层成员（docs/linux-port/12-shims-and-gaps.md:89 明确记为「只保留 x:Bind 成员」）；默认路径、包体/空间计算、按钮使能校验全部缺失，造成功能异常，见 bugs | cut |
| `Features/GameInstall/InstallGameDialog.Linux.cs` | L23、L43–47 | 与 XAML 根元素的 x:DefaultBindMode="OneWay"（InstallGameDialog.xaml:12）直接冲突：OneWay 绑定在无 INPC 时永不刷新，属实现错位而非平台限制 | api |
| `Features/GameInstall/InstallGameDialog.Linux.cs` | L60–81 ChangePathAsync | 上游选目录后的空间提示与可点性判断在 Linux 版整段消失，用户看不到「够不够装」也得不到即时反馈 | cut |
| `Features/GameInstall/InstallGameDialog.Linux.cs` | L84–109 StartInstallationAsync | 安装路径从不落盘、也不广播任务开始（见 bugs B1/B8）；卸载/更新/启动全都依赖这条路径，属最严重的语义缺口 | cut |
| `Features/GameInstall/InstallGameDialog.Linux.cs` | Segmented_SelectLanguage（InstallGameDialog.LinuxAuto.cs 空壳）与 L23 _audioLanguage | M1 阶段只跑中文语音包，选择区藏起来避免误导；但服务端 L282–291 会按 context.AudioLanguage 映射音频字段，因此该常量直接决定下载哪套语音 | cut |
| `Features/GameInstall/PreDownloadDialog.Linux.cs` | L107–167 LoadSizeInfoAsync | 移植侧不引 GamePackageService/HoYoPlay，直接用 Sophon 公开接口按「同一接口 branch=predownload、tag 留空」算大小（13-lessons L-59/L-61：整包 136 GB、补丁 8.34 GB）；代价是忽略列表（is_delete 类别）… | api |
| `Features/GameInstall/PreDownloadDialog.Linux.cs` | L95–104 ResolveInstallPath | 兜底把「无安装」当成「可预下载到默认目录」，改变了可预下载的前提条件（见 bugs B5） | cut |
| `Features/GameInstall/PreDownloadDialog.Linux.cs` | L127、L184–204（CheckCanPreDownload 缺失） | 上游唯一的使能点被整段裁掉，按钮可点性因此不受任何条件约束（叠加 XAML 的 IsEnabled="False"，行为取决于 Uno ButtonBase 是否按 CanExecute 回填） | cut |
| `Features/GameInstall/PreDownloadDialog.Linux.cs` | L32 _audioLanguage；L86–92 ctor 的 Opened 订阅；L184–204 StartPredownloadAsync | 上游通知 + Loaded 的组合在移植里退化成「toast + 轮询」；语音固定 Chinese 与服务端 MapAudioField 叠加后同样只能下中文语音 | cut |
| `Features/GameInstall/PreDownloadDialog.Linux.cs` | L170–181 FormatSize | 单位选择不同，数值口径一致；对 136 GB 的显示反而更省位数，不影响判断 | cosmetic |
| `Features/GameInstall/PreDownloadDialog.Linux.cs` | L21–77（显式 INotifyPropertyChanged + 手写 setter） | 同一工程里源生成器版本的行为在 Uno/Skia 下曾出过刷新问题，手写更可控；对外契约与上游一致（XAML 仍 OneWay 绑定） | api |
| `Features/GameInstall/GameInstallScanner.Linux.cs` | L257–268 NameMatches | 为压误报而设的严格匹配，但「Genshin Impact」「Star Rail」这类最常见目录名不含服名，天然命中不了（见 bugs B9） | linux |
| `Features/GameSetting/GameSettingPage.xaml.cs` | L90 | 导航耗时打点的残留，未输出也未 Stop，属临时代码（硬规则 7），应删除 | cosmetic |
| `Features/GameAccount/GameAccountService.cs` | L196-201 | WineRegistry.SetValuesAsync 内部 catch 掉异常只返回 bool，这里又只记警告不回传给调用方，导致切换账号失败时上层仍按成功处理（无任何用户反馈）；应把失败上抛或返回结果 | linux |
| `AppConfig.Configuration.cs` | :187-190 CheckEnviromentAsync 的 catch 分支 | 无 Win32 MessageBox 可用；但静默 Exit(0) 让用户完全看不出失败原因，改用 stderr 或 InAppToast 更合适（当前属可避免的体验缺口） | cut |
| `Features/ViewHost/MainWindow.xaml.cs` | :151-197 RunDebugWindowAction | XWayland 下无法可靠点击应用（L-36），窗口动作只能用启动开关验证；已由 DevSwitches.IsEnabled 门控（Release 需 LUNARWARD_DEV=1，D35） | eng |
| `Features/ViewHost/MainView.xaml.cs` | :151-268 DevSwitches.Page 直达与引脚布局重放 | 截图与排错靠启动直达页面（L-36，无法稳定点击 Uno 窗口）；全部经 DevSwitches.IsEnabled 门控，Release 需显式 LUNARWARD_DEV=1（D35） | eng |
| `Features/ViewHost/MainView.xaml.cs` | :566-573 CheckSystemProxy | 每次开机重复提示同一件已知信息（D86），改为一次性。（可回流上游） | cosmetic |
| `Features/Screenshot/ScreenshotPage.xaml` | L122-207（ContextFlyout 整块删除） | 打开/删除在 Linux 有等价实现（xdg-open、File.Delete），剪贴板/资源管理器项不适用；M3 未补，回看时可加 | cut |
| `Features/Screenshot/ScreenshotPage.Linux.cs` | L75-111 LoadImages | 游戏内截图目录依赖 GameInstall 路径映射；扩展名白名单也少了 4 种（见 bug） | cut |
| `Features/Screenshot/ScreenshotPage.Linux.cs` | L16-33 构造器 | PageBase.cs:37-40 每次 Loaded 都调 OnLoaded()，只要 override 就能与上游等价；现在漏了（见 bug） | cut |
| `Features/Screenshot/ScreenshotPage.Linux.cs` | L126-142 ManageScreenshotFolderCommand | 外部截图目录（AppConfig.GetExternalScreenshotFolder）与备份目录概念未移植；对话框保留但成孤儿 | cut |
| `Features/Screenshot/ScreenshotPage.Linux.cs` | L19-31 空态 | 多语言回退；顺带丢了按游戏换 emoji 的观感 | cosmetic |
| `Features/Screenshot/ScreenCapture.Linux.cs` | L60 文件命名 | 新命名不被 ScreenshotItem.TryParseCreationTime 识别（它认 yyyyMMdd_HHmmssff 与 yyyy-MM-dd-HH-mm-ss），CreationTime 退回文件系统时间，排序/时间显示只能靠 mtime | linux |
| `Features/Screenshot/ImageViewWindow.xaml.cs` | L300 | Uno 的 Launcher.LaunchUriAsync 在 X11 后端是空实现；该文件已 <Compile Remove>，改动不参与构建（生效实现是 ImageViewWindow.Linux.cs 的空壳） | api |
| `Features/Screenshot/ImageConvertItem.Linux.cs` | L6-13 | 唯一消费者 ImageBatchConvertWindow.xaml.cs 已 <Compile Remove>，该文件是死代码，可以整份删掉 | cut |
| `Features/Setting/ScreenshotSetting.Linux.cs` | L45-52 OnNavigatedFrom | 格式/质量只有 DXGI 捕获路径消费（D31），SDR/色彩管理在 Linux 无实现；但 XAML 仍展示这些项，用户改完等于没改 | cut |
| `Features/Setting/ScreenshotSetting.Linux.cs` | L28 ScreenshotQualityVisibility | 格式不生效，质量档位隐藏与否没有实际意义；但 PNG 档下仍显示「质量」与上游不一致 | cut |
| `Features/Setting/ScreenshotSetting.Linux.cs` | L84-100 ClearThumbnailCache | D57 后缓存迁到 AppConfig.CacheDirectory、缩略图缓存叫 thumb，两处名字与根目录都对不上（见 bug） | linux |
| `Features/Setting/ScreenshotSetting.Linux.cs` | L118-129 HotkeyInput_Hotkey* | RegisterHotKey 是 Win32 API，Wayland 下也不可用（X11 轮询方案见 D61） | api |
| `Features/Screenshot/ScreenCaptureService.cs` | L405 | 把同步 GameSetting 读取换成现成的 Async 版（避免捕获线程上的同步等待）；但该文件已 <Compile Remove>，在当前 Linux 构建里不生效（可回流上游） | api |
| `Features/GameRecord/GameRecordPage.xaml` | 根 Grid 末尾新增空态层（port:526-541） | D61 #7：上游未登录时内容区只有一片透背景的白，看起来像坏页；移植补空态（图标 + 说明 + 登录按钮）。空态层与 NavigationView 同级且无行列约束，铺满整页（含左侧工具箱导航），见 bugs 第 8 条 | cosmetic |
| `Features/GameRecord/GameRecordPage.xaml.cs` | CurrentRole setter（port:271）、GameRoleList 改显式 get/set（port:277-285）、新增 ShowRecordEmptyStat | 空态层的唯一数据源；L-110 实测本项目 x:Bind bool→Visibility 在 OnPropertyChanged 后生效（该页 x:DefaultBindMode="OneWay"，:14），因此必须在两个源 setter 里额外通知派生属性。除这三处外与上游 0 差异 | cosmetic |
| `Features/GameRecord/DailyNote/DailyNoteButton.xaml` | 满体力时间格式（port:406） | 上游用转义冒号（字面 ':'），移植去掉反斜杠后 ':' 变成区域性 TimeSeparator，在 fi-FI 等分隔符不是冒号的区域会显示成 12.34；属无意削弱，见 bugs 第 7 条（两处 {@0} 文案：Lang.DailyNoteButton_StarRail_FullTime / … | bugfix |
| `Features/Background/AppBackground.Linux.cs` | :55-60 字段、:88-140 ctor（Content 手工搭 Grid） | ImageEx（Scighost.WinUI.ImageEx）在 Linux 无实现，只能手搭视觉树。丢失三件事：换图交叉淡入（原来 null→PlacehoderImageSource 承载旧图）、三块角落渐变遮罩（壁纸压暗，D54 决定不再压暗整体）、背景更新中的底部进度条（用户看不到『正在取背… | engine |
| `Features/Background/AppBackground.Linux.cs` | :29 LUNARWARD_VIDEO_FPS、:36 LUNARWARD_VIDEO_NOPAINT、:264 LUNARWARD_BG_TIMEOUT_MS、:426 LUNA | 违反仓库硬规则 6『调试/开发入口一律 #if DEBUG 或 DevSwitches，Release 里不得残留副作用命令』：LUNARWARD_VIDEO_BG 能把任意本地视频塞成壁纸、LUNARWARD_VIDEO_NOPAINT 会直接把视频画面丢掉（只留声音+静止帧），Release 用… | linux |
| `Features/Background/AppBackground.Linux.cs` | :70-80 Current 惰性 getter、:377 _disposed、:880-900 无 Unloaded 清理、:932-949 ChangeCustomBackgr | Uno 的 UserControl 生命周期与 WinUI 不同（页面缓存 + 单例宿主），但没补卸载清理意味着视频播放器/消息注册不会释放；惰性 getter 让『在读 Current 的地方先创建一个空实例』成为可能（构造函数会启动 UiWatchdog、注册消息）。ChangeCustomBa… | eng |
| `Controls/CachedImage.Linux.cs` | :18 静态 HttpClient、:160-169 http 分支、:121-128 失败分支 | FileCache 依赖 StorageFile/ApplicationData；但代价是网络图（公告/抽卡物品图标）没有磁盘缓存、也没有坏缓存清理，每次进页面都重新下载，弱网下明显慢且更吃流量 | engine |
| `Controls/CachedImage.Linux.cs` | :50-62 UpdateClip | 注释自称『圆角在渲染层由合成器处理（M3）』，实际没有任何地方补圆角：上游设 CornerRadius 的地方（图库条目 ItemContainerStyle=4、GameBannerAndPost 等）在移植里都是直角，属可见的视觉降级；且 Clip 覆盖会让注释里的『M3 再补』无处可补 | cosmetic |
| `Controls/CachedImage.Linux.cs` | :131-172 LoadAsync 的 value 分支、:87-95 新增 DecodePixelWidth | 自建加载管线要自己解析 scheme（Uno 下 file:// 与相对 Uri 都不可靠，L-74）。当前仓库里没有把 ImageSource 直接赋给 CachedImage.Source 的调用点，属潜在回归面：一旦有人像用 Image 那样赋 BitmapImage，图片会静默消失 | api |
| `Features/Setting/GeneralSetting.xaml` | :46 RadioButton_CloseWindowOption_Hide | 该页其它文案仍走 lang；硬编码后切到英文/日文时不跟随，与相邻项不一致。语种问题属可避免的偏离 | cosmetic |
| `Features/Setting/DownloadSetting.xaml.cs` | :173-184 SetRateLimiterAsync | 限速由 Windows RPC 安装服务执行；移植的 Sophon 下载链路（ChunkReuseDownloader/InstallDownloadRunner/SophonClient）从不读 SpeedLimitKBPerSecond，滑杆可调但完全无效，且该页没有「不支持」提示（高级页有），… | cut |
| `Features/Setting/FileManageSetting.xaml` | :55 用户数据目录 TextBlock | 长路径由「缩字号完整显示」变成「第二行省略号」。未实测 Uno 下该事件是否可靠触发，若是引擎限制则合理，否则属可避免的显示退化 | cosmetic |
| `Features/Setting/GamepadControlSetting.xaml` | :101/109/113/128/159 XboxFluent2 图标 | SettingPage 菜单的手柄图标已因字体缺失换成 Symbols 码位，本页漏改；Uno/Skia 下这 5 处图标渲染为空白。页面整体已被置灰，影响有限但属不一致 | cosmetic |
| `Features/Setting/ScreenshotSetting.xaml` | :81-84 截图热键提示 | 同一批改动的 HotkeySetting 页用了新增 resx 键，这里却内联中文；非中文界面会出现混排。:61-64 只是 IsTextTrimmedChanged 属性换位（纯格式，不记） | cosmetic |
| `Features/GameSelector/GameSelector.xaml.cs` | L930-960 硬链接去重键（`var key = $\"{file.Length}_{file.LastWriteTimeUtc.Ticks}_{file.FullName.G | FILE_ID_INFO 是 Win32 API，但 Linux 有等价的 inode（Helpers/PosixIdentity.cs 已能 stat），完全可以按 (st_dev, st_ino) 去重；换成路径哈希后不同路径的硬链接不再合并，统计会偏大 | linux |
| `Features/UrlProtocol/UrlProtocolService.Linux.cs` | 全文件：`HandleUrlProtocolAsync` 未移植；深链改在 App.xaml.cs:246-320 `ExpandUrlArgument` + Features/A | Linux 的激活入口变成 argv/xdg-mime，只补了用户实际会点的 startgame；playtime/test 深链与 install_path 参数被丢弃（详见 bugs） | cut |
| `Helpers/ClipboardHelper.Linux.cs` | L11-18 `SetStorageItems`；缺 `SetBitmap(3 重载)` 与 `GetTextAsync` | 文件剪贴板要写成 text/uri-list 才算数（见 bugs）；贴图给 BBS 与读剪贴板文本的功能在 Linux 侧没有保留 | cut |
| `Helpers/WindowWaitAsync.Linux.cs` | 全文件（L1-30）：`WindowWaitAsyncExtension.WaitAsync(this Window)` | Scighost.WinUI 是 Windows 包，Linux 构建里没有该扩展。调用点是 AppConfig.Configuration.cs:107/166 `await new NoPermissionWindow(...).WaitAsync()` 这类「等用户关窗」的模态流程。未预检「窗… | api |

## 附录 C · 可回流上游（12 条）

| 文件 | 位置 | 理由 |
|---|---|---|
| `Features/GameLauncher/StartGameButton.xaml.cs` | 第 264、271-275、280 行（Downloading 进度计算） | 总量尚未知时上游会算出 NaN%/越界进度环（L-65），移植侧加零除与钳制；属于可回流上游的健壮性修复 |
| `Features/GameInstall/GameInstallService.Linux.cs` | L500–525 完成与失败分支 | Linux 侧没有任务栏进度，长任务结束需要可见反馈；属通用增强，可回流上游 |
| `Features/GameInstall/PreDownloadButton.xaml.cs` | L184–185（UpdateGameInstallTask 里的百分比文案） | 等待/估算阶段 total 尚未算出，上游直接相除得到 NaN 并显示「NaN%」；移植加 total>0 守卫，纯健壮性改动，建议回流上游 |
| `Features/HoYoPlay/HoYoPlayService.cs` | L168-173 | 接口偶尔不返回 content 节点时上游是 NullReferenceException、日志看不出是哪个请求；换成可读异常，调用方本来就 catch 降级，纯收益，可回流上游。 |
| `AppConfig.Configuration.cs` | :224-225 SetLanguage | Uno 下界面线程与非 UI 线程已创建，仅改线程文化时 x:Bind/本地化文案仍用旧文化，需同步设置静态 Culture。 |
| `AppConfig.Configuration.cs` | :242 IsAdmin | .NET 跨平台等价语义（uid 0 / euid 提升判定），无 Windows 身份对象可用，可直接回流上游。 |
| `AppConfig.Configuration.cs` | :89-95 AppVersion 兜底与 IsAppInRemovableStorage | AppVersion 为空会让 User-Agent 变成非法 header 直接炸 HttpClient（L-26）；可移动介质检测在 Linux 尚未实现，先按固定值处理。 |
| `Features/ViewHost/MainView.xaml.cs` | :566-573 CheckSystemProxy | 每次开机重复提示同一件已知信息（D86），改为一次性。 |
| `Features/ViewHost/MainWindow.xaml.cs` | :280-300 Content_KeyDown 的 Esc 处理 | 打开游戏选择层时按 Esc，上游会连带把整个窗口藏起来，而用户意图只是退出选择层；该修复对上流同样成立。 |
| `Program.cs` | :41 日志目录拼接（App.xaml.cs:43 同改） | 目录名统一由 DataFolder.AppFolderName（Linux 为 "lunarward"）提供，避免两处字面量漂移。 |
| `Features/Screenshot/ScreenCaptureService.cs` | L405 | 把同步 GameSetting 读取换成现成的 Async 版（避免捕获线程上的同步等待）；但该文件已 <Compile Remove>，在当前 Linux 构建里不生效 |
| `Features/PlayTime/PlayTimeRecordService.cs` | L78-107 Linux 分支（WaitForSessionEndAsync + 20s WhenAny + 30s 心跳 + Interruption=false 收尾） | Linux 的 PID 是 wine loader，HasExited 会过早为真（Proton 变体先起 steam.exe 存根），必须换成「前缀里的 wineserver 何时退出」。心跳写入 Interruption=true 的行 |

