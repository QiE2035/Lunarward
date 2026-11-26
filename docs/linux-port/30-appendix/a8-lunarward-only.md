# s3 审计（仅移植侧存在的 149 个文件）

- 仓库：`/home/qie2035/projects/migrate/Starward`，HEAD `2b1e7b46`（refactor(d134) 可避免偏离第四轮回改）
- 上游 `src/Starward/`，移植 `src/Lunarward/`；清单来源 `/tmp/lw-audit2/lunarward_only.txt`（149 条，逐条对账，无抽样、无按目录整体结论）
- 判据一（是否参与编译）：`dotnet msbuild src/Lunarward/Lunarward.csproj -getItem:Compile -getItem:Page -nologo`（Compile 332 项、Page 84 项）+ csproj 的 `<Compile Remove>` 清单（33-36、98-141 行）。`EnableDefaultPageItems=false` 但 Page 项由 Uno.Sdk 默认项注入，因此 XAML 是否编译一律以 MSBuild item 为准
- 判据二（引用）：`grep -rn <类名/文件名主干> src/Lunarward --include=*.cs --include=*.xaml`，并区分引用方自身是否在 Compile/Page 集合内；XML 命名空间式引用（`xmlns:xx="using:A.B"`）与双后缀文件（`*.xaml.cs`、`*.InstallTask.cs`、`*.Selection.cs`）另做人工核对，工具计数为 0 不代表无引用
- 判据三（是否上游同名覆盖）：`ls src/Starward/<同路径去掉 .Linux/.LinuxAuto>`；上游无同名基文件的一律按「移植新增」而非「平台覆盖」计
- 只读审计：本报告未修改仓库任何文件

## 逐文件判定（149/149）

| # | 文件 | 判定 | 证据（编译 / 引用） | 处置建议 |
|---|------|------|----------------------|----------|
| 1 | `.idea/.idea.Lunarward.dir/.idea/.gitignore` | **DEAD** | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证）；全仓零引用；.gitignore:402 已忽略 `.idea/` | 直接删（IDE 本地状态，本不该入库） |
| 2 | `.idea/.idea.Lunarward.dir/.idea/indexLayout.xml` | **DEAD** | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证）；全仓零引用；.gitignore:402 已忽略 `.idea/` | 直接删（IDE 本地状态，本不该入库） |
| 3 | `.idea/.idea.Lunarward.dir/.idea/projectSettingsUpdater.xml` | **DEAD** | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证）；全仓零引用；.gitignore:402 已忽略 `.idea/` | 直接删（IDE 本地状态，本不该入库） |
| 4 | `.idea/.idea.Lunarward.dir/.idea/vcs.xml` | **DEAD** | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证）；全仓零引用；.gitignore:402 已忽略 `.idea/` | 直接删（IDE 本地状态，本不该入库） |
| 5 | `.idea/.idea.Lunarward.dir/.idea/workspace.xml` | **DEAD** | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证）；全仓零引用；.gitignore:402 已忽略 `.idea/` | 直接删（IDE 本地状态，本不该入库） |
| 6 | `.junie/mcp/mcp.json` | **DEAD** | 非编译项、零引用；`.junie/` 已被 .gitignore:410 忽略，但 `git ls-files` 仍跟踪（历史误提交），其中一份还是 .bak 备份 | `git rm -r --cached src/Lunarward/.junie` 后从工作区删除 |
| 7 | `.junie/mcp/mcp.json.bak` | **DEAD** | 非编译项、零引用；`.junie/` 已被 .gitignore:410 忽略，但 `git ls-files` 仍跟踪（历史误提交），其中一份还是 .bak 备份 | `git rm -r --cached src/Lunarward/.junie` 后从工作区删除 |
| 8 | `Assets/login-helper/login.py` | **KEEP-NEW** | **真被代码调用**：WebLoginService.cs:39-40 拼出 `Assets/login-helper/login.py` 并用 python3 子进程拉起，:149 有「login helper missing」分支；WebKitLoginHost.cs:29 `IsAvailable()` 用同一判据；csproj Content 负责把目录复制到输出 | 保留，但调用链入口只有 LoginPage（见 §登录决策组） |
| 9 | `Assets/lunarward.png` | **KEEP-NEW** | csproj Content 复制到输出；AboutSetting.xaml、Features/Tray/StatusNotifierTray.cs、Features/UrlProtocol/UrlProtocolService.Linux.cs、Helpers/AppIcon.cs 引用该图 | 保留 |
| 10 | `Compat/NotifyIcon/TaskbarIconCompat.cs` | **DEAD** | H.NotifyIcon 包未引用，本文件纯占位 `TaskbarIcon`；唯一 XAML 使用者 SystemTrayWindow.xaml:17 `hn:TaskbarIcon`；而 App.xaml.cs:204-208 明确 `if (OperatingSystem.IsWindows())` 才构造 SystemTrayWindow，Linux 托盘走 Features/Tray/StatusNotifierTray.cs（App.xaml.cs 引用）→ 该控件在 Linux 永不实例化 | 与 SystemTrayWindow.xaml/.cs 一并从 Page/Compile 中排除后删除（一次原子改动） |
| 11 | `Compat/VanaraHotkeyCompat.cs` | **KEEP-NEW** | Vanara 包未引用，靠本文件提供 `Vanara.PInvoke.User32.{VK, HotKeyModifiers, WindowLongFlags, SetWindowLong}`；存活调用点 Features/Setting/HotkeySetting.xaml.cs:69、Features/Passport/HypLoginWindow.xaml.cs:77、Helpers/HotkeyManager.Linux.cs | 保留（热键 + 登录窗父窗设置都依赖） |
| 12 | `Controls/CachedImage.Linux.cs` | **KEEP-LINUX** | 上游 Controls/CachedImage.cs 在 csproj `<Compile Remove>`；本文件 31 处存活引用（VideoThumbnailer.cs、BackgroundViewPage.xaml、GachaLogPage.xaml 等） | 保留（唯一来取代实现：DecodePixelWidth「Lunarward」，同名类替换上游） |
| 13 | `Controls/ImageExCompat.cs` | **KEEP-NEW** | CommunityToolkit.WinUI.Controls 的 ImageEx 未引包，本文件提供 `namespace Scighost.WinUI.ImageEx`；XAML 以 `xmlns:swi="using:Scighost.WinUI.ImageEx"` 使用：AppBackground.xaml:9、AppBackgroundImageExStyle.xaml:5、GameSelector/NoAnimationImageExStyle.xaml:5（编译器按 xmlns 解析，文本 grep 查不到，故工具计数为 0） | 保留（删则这 3 个 XAML 编译失败） |
| 14 | `Controls/InteractivityCompat.cs` | **KEEP-NEW** | Microsoft.Xaml.Behaviors 未引包，本文件提供 `Microsoft.Xaml.Interactivity.{Interaction, EventTriggerBehavior, ControlStoryboardAction}`；XAML xmlns 使用：MainWindow.xaml:8、ScreenshotPage.xaml:10、Background/BackgroundViewPage.xaml:9。文件头自承「事件触发与动画动作暂未真正接线」——只是让 XAML 编译通过的替身 | 保留；若以后把这三处 XAML 行为删干净，本文件可整簇删 |
| 15 | `Controls/ToolkitCompat.cs` | **KEEP-NEW** | CommunityToolkit.WinUI 包未引用，本文件提供 `UIElementExtensions.ClipToBounds` / `VisualExtensions.NormalizedCenterPoint`；XAML 实际使用：BackgroundViewPage.xaml:160/165（ui:）、DeadlyAssaultPage.xaml:410/719、ShiyuDefensePage.xaml:555/812/1121/1351、LauncherConfigView.xaml:353（cw:） | 保留（大量 ZZZ 战绩页依赖） |
| 16 | `Controls/ToolkitControls.cs` | **KEEP-NEW** | 本文件提供 `CommunityToolkit.WinUI.Controls.{Segmented, SegmentedItem, WrapPanel}`；`xmlns:...="using:CommunityToolkit.WinUI.Controls"` 命中 BackgroundViewPage.xaml:14、Gacha/GachaStatsCard.xaml:5、GenshinBeyondGachaPage.xaml:6、ZZZ/GachaStatsCard.xaml:5、InstallGameDialog.xaml:5、DX12IntroDialog.xaml:5、GameLauncherSettingDialog.xaml:5、DailyNote/DailyNoteButton.xaml:5 等 | 保留 |
| 17 | `Controls/Win2DCompat.cs` | **DEAD** | 提供 `CanvasSwapChainPanel` 占位；唯一使用者是 Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.cs:82-197，而该文件已在 csproj `<Compile Remove>`；全部 XAML 与其余编译码零命中 | 删（连带 GenshinHDR 的 Win2D 渲染路径已由 .Linux.cs 程序化重建） |
| 18 | `Features/App/DevSwitches.cs` | **KEEP-NEW** | 9 处存活引用：App.xaml.cs、AppBackground.Linux.cs、ScreenshotPage.Linux.cs、设置页等（开发开关集中处，D35） | 保留 |
| 19 | `Features/App/SingleInstancePipe.cs` | **KEEP-NEW** | App.xaml.cs 单实例管道（Linux 侧替代命名管道/互斥体） | 保留 |
| 20 | `Features/Background/AppBackground.Linux.cs` | **KEEP-LINUX** | 上游 Features/Background/AppBackground.xaml.cs 在 `<Compile Remove>`，本文件是同一 partial 类；6 处存活引用（AppBackground.xaml、BackgroundSwitcher.xaml.cs、GameLauncherPage.xaml.cs…） | 保留 |
| 21 | `Features/Background/BackgroundChangedMessage.cs` | **KEEP-NEW** | AppBackground.Linux.cs:156 `WeakReferenceMessenger.Register<..., BackgroundChangedMessage>` + :193 `OnBackgroundChangedMessage`（上游有、移植侧曾漏，:154 注释） | 保留 |
| 22 | `Features/Background/VideoBackgroundPlayer.cs` | **KEEP-NEW** | AppBackground.Linux.cs、VideoThumbnailer.cs 引用（LibVLCSharp 视频背景） | 保留 |
| 23 | `Features/Background/VideoBgVolumeChangedMessage.cs` | **DEAD** | 全仓只有类定义，无任何 `Send(`/`Register<`；上游 0.19.0 已无对应消息类型 | 删 |
| 24 | `Features/Background/VideoThumbnailer.cs` | **KEEP-NEW** | CachedImage.Linux.cs、BackgroundFileItem.cs、BackgroundViewPage.xaml.cs | 保留 |
| 25 | `Features/Codec/ImageLoader.Linux.cs` | **KEEP-LINUX** | 上游 Features/Codec/ImageLoader.cs 在 `<Compile Remove>`；被 BackgroundService.cs、ScreenshotItem.cs 使用（Skia/Magick 解码） | 保留 |
| 26 | `Features/Companion/CompanionAutoStart.cs` | **KEEP-NEW** | App.xaml.cs 调用（伴随启动自启注册） | 保留 |
| 27 | `Features/Companion/CompanionEntry.cs` | **KEEP-NEW** | 4 处：CompanionAutoStart.cs、CompanionRunner.cs、CompanionStore.cs | 保留 |
| 28 | `Features/Companion/CompanionRunner.cs` | **KEEP-NEW** | App.xaml.cs + AppConfig.ServiceProvider.cs:149 `AddSingleton<CompanionRunner>()` + CompanionAutoStart.cs | 保留 |
| 29 | `Features/Companion/CompanionStore.cs` | **KEEP-NEW** | 4 处：CompanionAutoStart.cs、CompanionRunner.cs、GameSettingPage.xaml.cs | 保留 |
| 30 | `Features/Dev/UiSelfTest.cs` | **KEEP-NEW** | Program.Linux.cs 的 `--selftest-ui` 分派 + MainWindow.xaml.cs；AGENTS §5 L1.5（12 条断言，CI 内 xvfb 跑） | 保留（环境变量门控，不进 Release 副作用） |
| 31 | `Features/GameAccount/RegistryFormat.cs` | **KEEP-NEW** | WineRegistry.cs、WineRegistryFiles.cs 引用（Wine registry 文本格式） | 保留 |
| 32 | `Features/GameAccount/WineRegistry.cs` | **KEEP-NEW** | 4 处：GameAccountService.cs、WineRegistryFiles.cs、GameNoticeService.cs、GameSettingService.cs | 保留 |
| 33 | `Features/GameAccount/WineRegistryFiles.cs` | **KEEP-NEW** | WineRegistry.cs 引用 | 保留 |
| 34 | `Features/GameInstall/GameInstallScanner.Linux.cs` | **KEEP-NEW** | 上游 Features/GameInstall/ 下无同名基文件（只有 GameInstallService/GameInstallController…），所以不是「覆盖」而是移植新增；App.xaml.cs:108 `Task.Run(ScanAndApply)`、MainView.xaml.cs:287 `KnownGames` | 保留（`.Linux` 只是命名习惯，可改名 GameInstallScanner.cs） |
| 35 | `Features/GameInstall/GamePackageService.cs` | **DEAD** | 181 行旧 Sophon 安装管线（Hi3Helper.Sophon 直连、自研 chunk 下载）；全仓 grep 只在自身命中，未 DI 注册、零调用点；当前安装走上游 `Starward.GameInstall.GameInstallRunner` 进程内 facade（AGENTS §3 注 + D128 已退役自研 Sophon 管线） | 删（与 D128 一致） |
| 36 | `Features/GameLauncher/GameAuthLoginButton.xaml` | **DEAD** | 作为 Page item 参与编译，但全仓没有任何 XAML 放置该控件（`GameAuthLoginButton` 只命中自身与 AppConfig.ServiceProvider.cs:152 的注释）；没有任何导航/路由指向它 | 删（连同下面两个文件 + AppConfig.ServiceProvider.cs:152-153 注册行） |
| 37 | `Features/GameLauncher/GameAuthLoginButton.xaml.cs` | **DEAD** | 只被自身 XAML 与 GameAuthLoginService 引用；控件从未实例化 | 同上 |
| 38 | `Features/GameLauncher/GameAuthLoginService.cs` | **DEAD** | 唯一引用 AppConfig.ServiceProvider.cs:153 `sc.AddHttpClient<GameAuthLoginService>()`（注释称「GameAuthLoginButton 会解析它」）；服务本身提供 `GetHyperionAidAsync`/`VerifyStokenAsync`/`CreateAuthTicketByGameBiz`（hyp 换 auth ticket），但无任何调用点、按钮也从未被放置 → 未接线功能 | 同上（连注册行一起删） |
| 39 | `Features/GameLauncher/GameLauncherPage.InstallTask.cs` | **KEEP-NEW** | GameLauncherPage 的第三个 partial；StartInstallTaskWatcher/StopInstallTaskWatcher 由 GameLauncherPage.xaml.cs:79 与 :164 调用（工具 grep 因双后缀漏匹配） | 保留 |
| 40 | `Features/GameLauncher/GameLauncherService.Linux.cs` | **KEEP-LINUX** | 上游与移植两侧的 GameLauncherService.cs 都是 `internal partial class`（上游 :25、移植 :27），本文件是第三个 partial，承载 Linux 启动路径（wine 首进程约束、组件/前缀解析）；25 处存活引用 | 保留（Linux 启动核心） |
| 41 | `Features/GameLauncher/LaunchPreflight.cs` | **KEEP-NEW** | 4 处：GameLauncherService.cs、GameSettingPage.xaml.cs、Helpers/DiagnosticBundle.cs、Runtime 侧 | 保留 |
| 42 | `Features/GameLauncher/LaunchReport.cs` | **KEEP-NEW** | GameLauncherService.cs 引用（启动诊断报告） | 保留 |
| 43 | `Features/GameRecord/BBSWebBridge.Linux.cs` | **KEEP-LINUX** | 上游 BBSWebBridge.xaml.cs 在 `<Compile Remove>`（Page 仍在编）；BattleChronicleWindow.xaml:28 实例化该 UserControl | 保留 |
| 44 | `Features/GameRecord/BattleChronicleWindow.Linux.cs` | **KEEP-LINUX** | 上游 BattleChronicleWindow.xaml.cs 在 `<Compile Remove>`；GameRecordPage.xaml.cs:345/357 `new BattleChronicleWindow` | 保留 |
| 45 | `Features/GameRecord/BattleChronicleWindow.LinuxAuto.cs` | **KEEP-LINUX** | 同上（同页 partial：XAML 事件/命名控件接线） | 保留 |
| 46 | `Features/GameRecord/GameRecordRoleChangedMessage.cs` | **DEAD** | 唯一 `Send` 在 LoginPage.Linux.cs:61（另一处 LoginPage.xaml.cs:240 已不编译）；唯一订阅 GameRecordPage.xaml.cs:66，是专为 LoginPage 补的旁路消息 | 随 LoginPage 一起删；同时撤掉 GameRecordPage.xaml.cs:64-70 的注册 |
| 47 | `Features/GameRecord/GameRecordService.cs` | **REDUNDANT** | 移植自造门面：全仓唯一消费者 LoginPage.Linux.cs:23 `GetService<GameRecordService>()`（+ AppConfig.ServiceProvider.cs:151 注册）；上游 0.19.0 没有这个类，战绩页直接走 GameRecordRoleService + 各游戏 GameRecord*Service（GameRecordPage.xaml.cs:32/418）；文件内 :50 注释「23 处调用方不受影响」与实况不符，属过时断言 | 随 LoginPage 退役一并删（顺带删 :151 注册） |
| 48 | `Features/GameRecord/LoginPage.Linux.cs` | **REDUNDANT** | 上游 0.19.0 已删整页登录页，登录统一走 Passport/HypLoginWindow；移植侧本页唯一入口是 GameRecordPage.xaml.cs:512 `NavigateTo(typeof(LoginPage))`（GameRecordPage.xaml:504 空态按钮 `WebLoginCommand`）与 MainView.xaml.cs:188 的 `"login"` 调试路由，而上游等价路径 GameRecordPage.xaml:93/154 `LoginCommand`→`HypLoginAsync()` 在移植侧同样可用；docs/linux-port/25-deviation-register.md:95/1502-1503/1602 已把保留本页登记为「可避免」 | 先真机验证 HypLogin 回填，再把 GameRecordPage.xaml:504 改成 LoginCommand，然后本页整簇删 |
| 49 | `Features/GameRecord/LoginPage.LinuxAuto.cs` | **REDUNDANT** | 同上；x:Class partial（事件/控件接线） | 同上 |
| 50 | `Features/GameRecord/LoginPage.xaml` | **REDUNDANT** | MSBuild `-getItem:Page` 命中（确实参与编译，回答了 25-deviation-register.md:1614 的悬案）；.xaml.cs 被 Compile Remove，代码由 Linux/LinuxAuto partial 提供；可达性同 LoginPage.Linux.cs | 同上 |
| 51 | `Features/GameRecord/LoginPage.xaml.cs` | **DEAD** | csproj:113 `<Compile Remove="Features/GameRecord/LoginPage.xaml.cs" />`；268 行旧 Windows 实现（WebView2 CookieManager），上游已无此页 | 删 |
| 52 | `Features/GameRecord/WebKitLoginHost.cs` | **KEEP-NEW** | Program.Linux.cs:136/142 暴露子命令；WebLoginService.cs:75 `IsAvailable()`；由 login.py 承载 WebKitGTK 登录窗口 | 保留（但入口只有 LoginPage，见 §登录决策组） |
| 53 | `Features/GameRecord/WebLoginService.cs` | **KEEP-NEW** | BBSWebBridge.Linux.cs:18 `IsLoggedIn => WebLoginService.Current.HasCookies`、LoginPage.Linux.cs:50 `LoginAsync`、WebKitLoginHost 拉起 login.py | 保留（同上，BBS cookie 唯一来源） |
| 54 | `Features/GameRecord/ZZZ/ElementWeaknessConstants.cs` | **KEEP-NEW** | ShiyuDefensePage.xaml:439-451 `local:ElementWeaknessConstants.None` | 保留 |
| 55 | `Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs` | **KEEP-LINUX** | 上游 GenshinHDRLuminanceSettingDialog.xaml.cs 在 `<Compile Remove>`；GameSettingPage.xaml.cs:514 `new GenshinHDRLuminanceSettingDialog{...}.ShowAsync()` | 保留（程序化构建对话框，替代被停用的 .xaml.disabled） |
| 56 | `Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.disabled` | **DEAD** | 后缀是 `.disabled`，不进 Compile/Page；页面改由 .Linux.cs 程序化构建，全仓无引用 | 删（历史停用留档，git 历史里已有） |
| 57 | `Features/GamepadControl/GamepadController.Linux.cs` | **KEEP-LINUX** | 上游 Features/GamepadControl/GamepadController.cs 在 `<Compile Remove>`；App.xaml.cs、GamepadControlSetting.xaml.cs、MainView.xaml.cs 使用 | 保留 |
| 58 | `Features/Overlay/OverlayWindow.LinuxAuto.cs` | **KEEP-LINUX** | 上游 OverlayWindow.xaml.cs 在 `<Compile Remove>`，本 partial 是 OverlayWindow.xaml（Page item）唯一代码提供者；但 `OpenOverlayWindow()` 全仓零调用、RunningGameService.Linux.cs:78 恒 `false` → 运行期不可达（AGENTS §8：悬浮窗 M4 占位） | 保留（M4 占位）；真要瘦身就 Page Remove OverlayWindow.xaml + 本文件一起删 |
| 59 | `Features/Overlay/RunningGameService.Linux.cs` | **KEEP-LINUX** | 上游 RunningGameService.cs 在 `<Compile Remove>`；CloudGameButton.xaml.cs、GameLauncherPage.xaml.cs 调用（:78 `OpenOverlayWindow() => false`） | 保留 |
| 60 | `Features/PlayTime/PlayTimeSession.Linux.cs` | **KEEP-NEW** | 上游 Features/PlayTime/ 下无 PlayTimeSession.cs（只有 PlayTimeItem/State/RecordService…），故属移植新增而非覆盖；GameLauncherService.Linux.cs、GameLauncherService.cs、PlayTimeRecordService.cs 引用 | 保留（可去掉 `.Linux` 后缀） |
| 61 | `Features/RPC/RpcService.Linux.cs` | **DEAD** | 引用者只有 Features/RPC/RpcRunner.cs 与 RpcService.cs，两者都在 `<Compile Remove>`；文件里声明的 `RpcServerInfo` 也全仓零引用；AppConfig.ServiceProvider 无任何 RPC 注册 | 删 |
| 62 | `Features/Runtime/ArchiveExtractor.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；3 处存活引用：Features/Runtime/ComponentImporter.cs、Features/Runtime/ComponentSources.cs、Features/Setting/RuntimeEnvironmentSetting.Components.cs | 保留（运行环境核心） |
| 63 | `Features/Runtime/ComponentDownloadMath.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；1 处存活引用：Features/Runtime/ComponentDownloader.cs | 保留（运行环境核心） |
| 64 | `Features/Runtime/ComponentDownloader.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；4 处存活引用：Features/Runtime/ComponentImporter.cs、Features/Runtime/ComponentSources.cs、Features/Setting/RuntimeEnvironmentSetting.Components.cs | 保留（运行环境核心） |
| 65 | `Features/Runtime/ComponentHealth.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；2 处存活引用：Features/Setting/RuntimeEnvironmentSetting.xaml.cs、Program.Linux.cs | 保留（运行环境核心） |
| 66 | `Features/Runtime/ComponentImporter.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；2 处存活引用：Features/Setting/RuntimeEnvironmentSetting.Components.cs、Program.Linux.cs | 保留（运行环境核心） |
| 67 | `Features/Runtime/ComponentManifest.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；4 处存活引用：Features/Runtime/ComponentImporter.cs、Features/Runtime/ComponentStore.cs、Features/Setting/RuntimeEnvironmentSetting.Components.cs | 保留（运行环境核心） |
| 68 | `Features/Runtime/ComponentSources.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；4 处存活引用：Features/Runtime/ComponentImporter.cs、Features/Setting/RuntimeEnvironmentSetting.Components.cs、Features/Setting/RuntimeEnvironmentSetting.xaml.cs | 保留（运行环境核心） |
| 69 | `Features/Runtime/ComponentStore.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；16 处存活引用：Features/GameLauncher/GameLauncherPage.xaml.cs、Features/GameLauncher/GameLauncherService.Linux.cs、Features/GameLauncher/LaunchPreflight.cs | 保留（运行环境核心） |
| 70 | `Features/Runtime/Enhancements.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；8 处存活引用：Features/GameLauncher/GameLauncherService.Linux.cs、Features/GameLauncher/LaunchPreflight.cs、Features/Runtime/LaunchRecipe.cs | 保留（运行环境核心） |
| 71 | `Features/Runtime/EnvironmentResolver.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；9 处存活引用：Features/GameLauncher/GameLauncherPage.xaml.cs、Features/GameLauncher/GameLauncherService.Linux.cs、Features/GameLauncher/LaunchPreflight.cs | 保留（运行环境核心） |
| 72 | `Features/Runtime/GraphicsInstaller.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；4 处存活引用：Features/GameLauncher/GameLauncherPage.xaml.cs、Features/GameLauncher/GameLauncherService.Linux.cs、Features/Setting/RuntimeEnvironmentSetting.xaml.cs | 保留（运行环境核心） |
| 73 | `Features/Runtime/LastKnownGoodStore.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；3 处存活引用：Features/GameLauncher/GameLauncherService.cs、Features/Setting/RuntimeEnvironmentSetting.Profiles.cs、Helpers/DiagnosticBundle.cs | 保留（运行环境核心） |
| 74 | `Features/Runtime/LaunchHistoryStore.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；2 处存活引用：Features/GameLauncher/GameLauncherService.cs、Features/Setting/RuntimeEnvironmentSetting.Profiles.cs | 保留（运行环境核心） |
| 75 | `Features/Runtime/LaunchRecipe.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；3 处存活引用：Features/GameLauncher/GameLauncherService.Linux.cs、Features/Setting/RuntimeEnvironmentSetting.Profiles.cs、Features/Setting/RuntimeEnvironmentSetting.xaml.cs | 保留（运行环境核心） |
| 76 | `Features/Runtime/OutputCapture.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；3 处存活引用：Features/Companion/CompanionRunner.cs、Features/GameLauncher/GameLauncherService.cs、Features/Setting/AdvancedSetting.xaml.cs | 保留（运行环境核心） |
| 77 | `Features/Runtime/PeArchitecture.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；2 处存活引用：Features/GameLauncher/LaunchPreflight.cs、Features/Runtime/PrefixManager.cs | 保留（运行环境核心） |
| 78 | `Features/Runtime/PrefixManager.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；16 处存活引用：Features/Companion/CompanionRunner.cs、Features/GameAccount/WineRegistry.cs、Features/GameLauncher/GameLauncherPage.xaml.cs | 保留（运行环境核心） |
| 79 | `Features/Runtime/PrefixPath.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；15 处存活引用：Features/GameLauncher/LaunchPreflight.cs、Features/GameLauncher/LaunchReport.cs、Features/Runtime/EnvironmentResolver.cs | 保留（运行环境核心） |
| 80 | `Features/Runtime/ProfileBinding.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；11 处存活引用：Features/Dev/UiSelfTest.cs、Features/GameLauncher/GameLauncherPage.xaml.cs、Features/GameLauncher/GameLauncherService.Linux.cs | 保留（运行环境核心） |
| 81 | `Features/Runtime/ProfileShellService.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；4 处存活引用：Features/Companion/CompanionRunner.cs、Features/Runtime/RunOnceService.cs、Features/Setting/RuntimeEnvironmentSetting.xaml.cs | 保留（运行环境核心） |
| 82 | `Features/Runtime/RunOnceService.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；2 处存活引用：Features/Setting/RuntimeEnvironmentSetting.xaml.cs、Program.Linux.cs | 保留（运行环境核心） |
| 83 | `Features/Runtime/RunnerNaming.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；1 处存活引用：Features/Runtime/ComponentStore.cs | 保留（运行环境核心） |
| 84 | `Features/Runtime/RuntimeDefaults.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；3 处存活引用：Features/Runtime/ProfileBinding.cs、Features/Runtime/RuntimeProfileStore.cs、Features/Setting/RuntimeEnvironmentSetting.Profiles.cs | 保留（运行环境核心） |
| 85 | `Features/Runtime/RuntimeEnvironment.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；4 处存活引用：Features/GameLauncher/GameLauncherService.Linux.cs、Features/Runtime/PrefixManager.cs、Features/Runtime/ProfileShellService.cs | 保留（运行环境核心） |
| 86 | `Features/Runtime/RuntimeProfile.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；24 处存活引用：Features/Companion/CompanionRunner.cs、Features/Dev/UiSelfTest.cs、Features/GameLauncher/GameLauncherPage.xaml.cs | 保留（运行环境核心） |
| 87 | `Features/Runtime/RuntimeProfileStore.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；11 处存活引用：Features/Companion/CompanionRunner.cs、Features/GameLauncher/GameLauncherService.Linux.cs、Features/GameSetting/GameSettingPage.xaml.cs | 保留（运行环境核心） |
| 88 | `Features/Runtime/SteamLibrary.cs` | **KEEP-NEW** | 移植新增（上游无对应文件）；1 处存活引用：Features/Runtime/ComponentStore.cs | 保留（运行环境核心） |
| 89 | `Features/Screenshot/ScreenCapture.Linux.cs` | **KEEP-LINUX** | 上游 ScreenCaptureHelper/ScreenCaptureItem/ScreenCaptureService/ScreenCaptureContext 全在 `<Compile Remove>`；本文件声明类 `ScreenCaptureLinux`（工具按主干匹配不到，故显示 0），实际被 ScreenshotPage.Linux.cs:32/102/204/281/470、ScreenshotSetting.Linux.cs:109/177、Program.Linux.cs:213/263 使用 | 保留（截图核心） |
| 90 | `Features/Screenshot/ScreenshotConverter.cs` | **KEEP-NEW** | ScreenshotPage.Linux.cs:594、Program.Linux.cs:267（批量转 JPEG，M3 新增） | 保留 |
| 91 | `Features/Screenshot/ScreenshotItem.Selection.cs` | **KEEP-NEW** | 给上游 partial `ScreenshotItem` 加 `IsSelected`/`CheckBoxVisibility`；ScreenshotPage.xaml:85/89 `x:Bind`、ScreenshotPage.Linux.cs:144/173/186 使用（工具因双后缀漏匹配） | 保留 |
| 92 | `Features/Screenshot/ScreenshotPage.Linux.cs` | **KEEP-LINUX** | 上游 ScreenshotPage.xaml.cs 在 `<Compile Remove>`；5 处存活引用（ScreenshotPage.xaml、GameFeatureConfig.cs、LinuxAuto） | 保留 |
| 93 | `Features/Screenshot/ScreenshotPage.LinuxAuto.cs` | **KEEP-LINUX** | 同上（同页 partial：命名控件/事件接线） | 保留 |
| 94 | `Features/Screenshot/ScreenshotViewerWindow.xaml` | **KEEP-NEW** | Page item 参与编译；ScreenshotPage.Linux.cs 实例化，替代被删的 ImageViewWindow/ImageViewWindow2 链 | 保留 |
| 95 | `Features/Screenshot/ScreenshotViewerWindow.xaml.cs` | **KEEP-NEW** | 同上 | 保留 |
| 96 | `Features/Screenshot/ScreenshotWindowsDisabled.cs` | **DEAD** | 只声明占位类 `ImageViewWindow2`；唯一使用者 ScreenshotPage.xaml.cs:386/469 已在 `<Compile Remove>`；ScreenshotPage.Linux.cs:562 只在注释里提它，真实查看器是 ScreenshotViewerWindow | 删 |
| 97 | `Features/Setting/RuntimeEnvironmentSetting.Components.cs` | **KEEP-NEW** | `RuntimeEnvironmentSetting` 同一 partial 类（.xaml.cs 声明 partial）；页面由 MainView/DevSwitches 可达（10 处），文件内含组件管理与探测逻辑（R85 共享 HttpClient 等） | 保留 |
| 98 | `Features/Setting/RuntimeEnvironmentSetting.Enhancements.cs` | **KEEP-NEW** | 同一 partial（增强项开关） | 保留 |
| 99 | `Features/Setting/RuntimeEnvironmentSetting.Profiles.cs` | **KEEP-NEW** | 同一 partial（配置档） | 保留 |
| 100 | `Features/Setting/RuntimeEnvironmentSetting.xaml` | **KEEP-NEW** | Page item，10 处存活引用（DevSwitches.cs、UiSelfTest.cs、SettingPage…） | 保留 |
| 101 | `Features/Setting/RuntimeEnvironmentSetting.xaml.cs` | **KEEP-NEW** | 同上（页面代码） | 保留 |
| 102 | `Features/Setting/ScreenshotSetting.Linux.cs` | **KEEP-LINUX** | 上游 Features/Setting/ScreenshotSetting.xaml.cs 在 `<Compile Remove>`；Dev/UiSelfTest.cs、ScreenshotSetting.xaml、SettingPage.xaml 引用 | 保留 |
| 103 | `Features/Setting/SettingNavigation.cs` | **KEEP-NEW** | SettingPage.xaml.cs:48/68 `TakeTarget()`、RuntimeEnvironmentSetting.xaml.cs:118 `TakeRuntimeTarget()`、MainWindow.xaml.cs:374 `SetRuntimeTarget`（跨页跳运行环境） | 保留 |
| 104 | `Features/Tray/StatusNotifierTray.cs` | **KEEP-NEW** | App.xaml.cs、MainWindow.xaml.cs（Linux 托盘；正是它取代了永不构造的 SystemTrayWindow） | 保留 |
| 105 | `Features/Update/SetupService.Linux.cs` | **DEAD** | 18 行空壳（构造函数空体 + 两个恒 0 属性），存在意义只是让 AppConfig.ServiceProvider.cs:103 `AddSingleton<SetupService>()` 的类型成立；唯一真实消费者 UpdateWindow.xaml.cs:41 已 Compile Remove；全仓无 `GetService<SetupService>` | 删，同时删 AppConfig.ServiceProvider.cs:103 注册行 |
| 106 | `Features/Update/UpdateService.Linux.cs` | **KEEP-LINUX** | 上游 UpdateService.cs 在 `<Compile Remove>`；AboutSetting.xaml.cs:64、MainView.xaml.cs:620、UpdateWindow.Linux.cs:133、Program.Linux.cs:226 使用（查 AUR 版本，D5） | 保留 |
| 107 | `Features/Update/UpdateWindow.Linux.cs` | **KEEP-LINUX** | 上游 UpdateWindow.xaml.cs 在 `<Compile Remove>`；AboutSetting.xaml.cs + UpdateWindow.xaml（Page）引用 | 保留 |
| 108 | `Features/Update/UpdateWindow.LinuxAuto.cs` | **KEEP-LINUX** | 同上（同页 partial） | 保留 |
| 109 | `Features/UrlProtocol/UrlProtocolService.Linux.cs` | **KEEP-LINUX** | 上游 UrlProtocolService.cs 在 `<Compile Remove>`；HypLoginWindow.xaml.cs、Program.Linux.cs、上游 Program.cs 引用（deep link 注册 + hyp:// 回调） | 保留 |
| 110 | `Features/ViewHost/CurrentGameChangedMessage.cs` | **KEEP-NEW** | MainView.xaml.cs:345 `Send`、GameLauncherPage.xaml.cs:87 `Register`、GameSettingPage.xaml.cs | 保留 |
| 111 | `Features/ViewHost/NoPermissionWindow.xaml` | **KEEP-NEW** | Page item；AppConfig.Configuration.cs:108/174 `new NoPermissionWindow(folder)` | 保留 |
| 112 | `Features/ViewHost/NoPermissionWindow.xaml.cs` | **KEEP-NEW** | 同上；提供自带实例方法 `WaitAsync()`——这也是 WindowWaitAsync 扩展没人用的原因 | 保留 |
| 113 | `Features/WindowBase.Linux.cs` | **KEEP-LINUX** | 上游 Features/WindowBase.cs 在 `<Compile Remove>`；30 处存活引用（全部窗口页、BackgroundViewWindow、CloudGameGachaWindow…），承载 X11 chrome/显隐/尺寸 | 保留（平台底座） |
| 114 | `Helpers/AccentColorHelper.Linux.cs` | **KEEP-LINUX** | 上游 Helpers/AccentColorHelper.cs 在 `<Compile Remove>`；App.xaml.cs、AppBackground.Linux.cs 使用 | 保留 |
| 115 | `Helpers/AppIcon.cs` | **KEEP-NEW** | MainWindow.xaml.cs、WindowBase.Linux.cs（窗口图标） | 保留 |
| 116 | `Helpers/CliSpec.cs` | **KEEP-NEW** | Program.Linux.cs 唯一帮助来源（D79）；scripts/test/lint-cli-help.sh 校验 | 保留 |
| 117 | `Helpers/ClipboardHelper.Linux.cs` | **KEEP-LINUX** | 上游 Helpers/ClipboardHelper.cs 在 `<Compile Remove>`；7 处存活引用 | 保留 |
| 118 | `Helpers/DataFolder.Linux.cs` | **KEEP-NEW** | 上游 Helpers/ 下无 DataFolder.cs（移植新增）；App.xaml.cs、Features/Runtime/OutputCapture.cs、Program.Linux.cs | 保留（可去 `.Linux` 后缀） |
| 119 | `Helpers/DesktopNotification.cs` | **KEEP-NEW** | ScreenCapture.Linux.cs:81/103/112 `DesktopNotification.Show(...)` | 保留 |
| 120 | `Helpers/DiagnosticBundle.cs` | **KEEP-NEW** | AdvancedSetting.xaml.cs、Program.Linux.cs（诊断包子命令） | 保留 |
| 121 | `Helpers/DialogGate.cs` | **KEEP-NEW** | 4 处：ScreenshotPage.Linux.cs、RuntimeEnvironmentSetting.Components.cs/.Profiles.cs 等 | 保留 |
| 122 | `Helpers/DriveHelper.Linux.cs` | **KEEP-LINUX** | 上游 Helpers/DriveHelper.cs 在 `<Compile Remove>`；7 处（InstallGameDialog.xaml.cs、PreDownloadDialog.xaml.cs、GameLauncherPage.xaml.cs…） | 保留 |
| 123 | `Helpers/FileDialogHelper.Linux.cs` | **KEEP-LINUX** | 上游 Helpers/FileDialogHelper.cs 在 `<Compile Remove>`；16 处存活引用（走 Portal / XDG portal 文件选择） | 保留 |
| 124 | `Helpers/GameIdHelper.cs` | **KEEP-NEW** | CompanionAutoStart.cs、GameLauncherService.Linux.cs、Program.Linux.cs | 保留 |
| 125 | `Helpers/HotkeyManager.Linux.cs` | **KEEP-NEW** | 上游 **没有** Helpers/HotkeyManager.cs（csproj:127 Remove 的是 Features/Setting/HotkeyManager.cs），故属移植新增；被 Features/Setting/HotkeySetting.xaml.cs、ScreenshotSetting.Linux.cs、Compat/VanaraHotkeyCompat.cs 等 5 处使用（X11 轮询热键） | 保留（可去 `.Linux` 后缀） |
| 126 | `Helpers/Housekeeping.cs` | **DEAD** | `Run()` 全仓零调用；无 HostedService/启动钩子注册它 | 删 |
| 127 | `Helpers/HttpClients.cs` | **KEEP-NEW** | RuntimeEnvironmentSetting.Components.cs:831/1014、RuntimeEnvironmentSetting.xaml.cs:2522（共享 Probe 客户端，R85） | 保留 |
| 128 | `Helpers/ImageDecode.cs` | **KEEP-NEW** | 7 处：CachedImage.Linux.cs、AppBackground.Linux.cs、ScreenCapture.Linux.cs、ScreenshotConverter… | 保留 |
| 129 | `Helpers/JsonFile.cs` | **KEEP-NEW** | 12 处（CompanionStore.cs、ComponentManifest.cs、ComponentSources.cs…原子写） | 保留 |
| 130 | `Helpers/MagickCodec.cs` | **KEEP-NEW** | ImageLoader.Linux.cs、ScreenCapture.Linux.cs、ImageDecode.cs、Program.Linux.cs（Magick.NET 兜底 AVIF/JXL） | 保留 |
| 131 | `Helpers/OneShotHints.cs` | **KEEP-NEW** | MainView.xaml.cs:154/288/316、Runtime/ProfileBinding.cs:337 `TryTake(...)` | 保留 |
| 132 | `Helpers/OpenUrl.cs` | **KEEP-NEW** | 11 处（BackgroundViewPage.xaml.cs、CloudGameButton.xaml.cs、ZZZ 抽卡工具…） | 保留 |
| 133 | `Helpers/Portal.cs` | **KEEP-NEW** | ScreenCapture.Linux.cs、FileDialogHelper.Linux.cs、OpenUrl.cs（XDG Desktop Portal） | 保留 |
| 134 | `Helpers/PosixIdentity.cs` | **KEEP-NEW** | LaunchPreflight.cs、Runtime/PrefixManager.cs（UID/属主判断） | 保留 |
| 135 | `Helpers/ProcessQuery.cs` | **KEEP-NEW** | GameLauncherPage.xaml.cs:908 `FindUsersAsync(prefix)` | 保留 |
| 136 | `Helpers/SystemBackdropHelper.Linux.cs` | **DEAD** | 上游 Helpers/SystemBackdropHelper.cs 在 `<Compile Remove>`，但本文件唯一存活引用是 Features/ViewHost/SystemTrayWindow.xaml.cs，而 SystemTrayWindow 在 Linux 永不构造（App.xaml.cs:204 `if (OperatingSystem.IsWindows())`）；WindowBase.Linux.cs 不经过它 | 与 TaskbarIconCompat + SystemTrayWindow 一起「排除编译 + 删除」（同一原子改动） |
| 137 | `Helpers/SystemThemePreference.cs` | **KEEP-NEW** | WindowBase.Linux.cs（主题跟随）+ SystemBackdropHelper.Linux.cs | 保留 |
| 138 | `Helpers/UiWatchdog.cs` | **KEEP-NEW** | AppBackground.Linux.cs:153 `UiWatchdog.Start(DispatcherQueue)` | 保留 |
| 139 | `Helpers/WindowWaitAsync.Linux.cs` | **DEAD** | 提供 `WaitAsync(this Window)` 扩展，全仓零调用；AppConfig.Configuration.cs:108/174 与 App.xaml.cs:86 命中的是 NoPermissionWindow/WelcomeWindow 自带的实例方法 `WaitAsync()` | 删 |
| 140 | `Helpers/X11KeyState.cs` | **KEEP-NEW** | MainView.xaml.cs:154、Helpers/HotkeyManager.Linux.cs（Wayland 下热键可用性判定） | 保留 |
| 141 | `Helpers/X11Window.cs` | **KEEP-NEW** | 6 处：HypLoginWindow.xaml.cs、StatusNotifierTray.cs、WindowBase.Linux.cs…（libxcb 窗口操作） | 保留 |
| 142 | `Helpers/XamlRootExtension.Linux.cs` | **KEEP-LINUX** | 上游 Helpers/XamlRootExtension.cs 在 `<Compile Remove>`；WindowBase.Linux.cs 使用 | 保留 |
| 143 | `LinuxConfig.cs` | **KEEP-NEW** | AppConfig.Configuration.cs 引用（配置根/路径解析） | 保留 |
| 144 | `Lunarward.csproj` | **KEEP-NEW** | 移植侧唯一工程文件：Uno.Sdk 入口、`<Compile Remove>` 清单（33-36、98-141 行）、Content（`Assets/login-helper/**`、`Assets/lunarward.png`）都在这里 | 保留（构建清单） |
| 145 | `Lunarward.csproj.user` | **DEAD** | IDE 本地状态文件；`git ls-files` 未跟踪；Compile/Page 项都不含它 | 直接删（应进 .gitignore） |
| 146 | `NuGet.config` | **KEEP-NEW** | 华为云单源镜像（含 `<clear/>`），AGENTS §3 记录 api.nuget.org 大包在本机 EOF | 保留 |
| 147 | `Program.Linux.cs` | **KEEP-NEW** | CLI 入口（60+ 开关/子命令），与上游 Program.cs 并存编译；Helpers/CliSpec.cs 与 Program.cs 引用它 | 保留（headless 能力的唯一入口） |
| 148 | `Properties/AssemblyVersion.cs` | **KEEP-NEW** | csproj 设 `GenerateAssemblyInfo=false`，程序集特性（AssemblyVersion 0.1.0.0 等）只有这一处提供；文件头注明是 L-50 的修复 | 保留（删则 Debug 版 AppVersion 退化 0.0.0.0） |
| 149 | `global.json` | **KEEP-NEW** | 钉 SDK 11.0.100 + Uno.Sdk 7.0.22；AGENTS 环境坑表依赖它 | 保留 |

## 可疑点专项结论

### `src/Lunarward/Features/GameInstall/GamePackageService.cs`

**DEAD**。181 行自研 Sophon 安装管线（Hi3Helper.Sophon 直连 + chunk 下载），全仓 grep 只在自身命中，无 DI 注册、无调用点；当前安装走上游 `Starward.GameInstall.GameInstallRunner` 进程内 facade（AGENTS §3 注、D128 已退役自研管线）。删除无连带改动。

### `Controls/ImageExCompat.cs` / `ToolkitCompat.cs` / `ToolkitControls.cs` / `InteractivityCompat.cs`

**四个都还在用（KEEP-NEW）**，但用法是「XAML 命名空间替身」而非代码调用，所以纯文本 grep 会误报 0 引用：ImageExCompat → `xmlns:swi="using:Scighost.WinUI.ImageEx"`（AppBackground.xaml:9、AppBackgroundImageExStyle.xaml:5、GameSelector/NoAnimationImageExStyle.xaml:5）；ToolkitCompat → `ui:`/`cw:` 前缀的 `UIElementExtensions.ClipToBounds`、`VisualExtensions.NormalizedCenterPoint`（BackgroundViewPage.xaml:160/165、DeadlyAssaultPage.xaml:410/719、ShiyuDefensePage.xaml:555/812/1121/1351、LauncherConfigView.xaml:353）；ToolkitControls → `xmlns:...="using:CommunityToolkit.WinUI.Controls"`（Segmented/WrapPanel，9 个 XAML）；InteractivityCompat → `xmlns:mxi="using:Microsoft.Xaml.Interactivity"`（MainWindow.xaml:8、ScreenshotPage.xaml:10、BackgroundViewPage.xaml:9，仅编译期接线，文件头自承未真正实现行为）。删任何一个都会让对应 XAML 编译失败。

### `Compat/NotifyIcon/TaskbarIconCompat.cs`

**DEAD**。为 H.NotifyIcon 的 `TaskbarIcon` 做占位，唯一 XAML 使用者 `SystemTrayWindow.xaml:17`；而 `App.xaml.cs:204-208` 是 `if (OperatingSystem.IsWindows())` 才构造 SystemTrayWindow，Linux 托盘走 `Features/Tray/StatusNotifierTray.cs` → 该控件在 Linux 永不实例化。删除必须一次做完：csproj 加 `<Compile Remove="Features/ViewHost/SystemTrayWindow.xaml.cs" />` 与 Page 排除，再删本文件（否则编译断）。

### `Compat/VanaraHotkeyCompat.cs`

**KEEP-NEW**。Vanara 包并未引用，热键/窗口父窗能力全靠本文件提供 `Vanara.PInvoke.User32.{VK, HotKeyModifiers, WindowLongFlags, SetWindowLong}`；存活调用点：`Features/Setting/HotkeySetting.xaml.cs:69`、`Features/Passport/HypLoginWindow.xaml.cs:77`、`Helpers/HotkeyManager.Linux.cs`。

### `Features/GameRecord/LoginPage.xaml` 与 `LoginPage.Linux.cs`

**REDUNDANT（整簇 4 个文件）**。上游 0.19.0 已删登录页，统一走 Passport/HypLoginWindow；移植侧本页唯一入口是 `GameRecordPage.xaml:504` 空态按钮（`WebLoginCommand`）→ `GameRecordPage.xaml.cs:512` `NavigateTo(typeof(LoginPage))`，外加 `MainView.xaml.cs:188` 的 `"login"` 调试路由。上游等价路径 `GameRecordPage.xaml:93/154` 的 `LoginCommand`→`HypLoginAsync()` 在移植侧可用，`docs/linux-port/25-deviation-register.md:95/1502-1503/1602` 也已把保留本页登记为「可避免」。处置：先真机验证 HypLogin 回填 → 把 :504 改为 `LoginCommand` → 删 LoginPage.xaml/.Linux.cs/.LinuxAuto.cs/.xaml.cs，连带 `GameRecordService.cs`（唯一消费者就是 LoginPage）、`GameRecordRoleChangedMessage.cs`（唯一 Send 在 LoginPage.Linux.cs:61）与 AppConfig.ServiceProvider.cs:151。附：`LoginPage.xaml` 确实是 Page item（`-getItem:Page` 命中），解决了 25-deviation-register.md:1614 的悬案。

### `Features/GameSelector/` 目录

**不存在问题**。上游 `src/Starward/Features/GameSelector/` 与移植侧逐文件名一致（GameSwitcher.xaml/.cs、NoAnimationImageExStyle.xaml 等），移植侧没有任何「仅移植侧存在」的残留文件进入本清单，无需处置。

### `Features/Codec/ImageLoader.Linux.cs`、`Features/Runtime/*`

**全部 KEEP**。ImageLoader.Linux.cs 是上游 `Features/Codec/ImageLoader.cs`（已 Compile Remove）的平台覆盖，被 `BackgroundService.cs`、`ScreenshotItem.cs` 使用。Features/Runtime 共 27 个文件全部参与编译且都有存活调用链（引用计数见表格行，最高的 RuntimeProfile 24 处、ComponentStore/PrefixManager 各 16、PrefixPath 15、ProfileBinding/RuntimeProfileStore 各 11，最低的 RunnerNaming/SteamLibrary 各 1 处但都在 `ComponentStore.cs` 内存活），是运行环境模块本体，无孤儿。

### `src/Lunarward/Assets/login-helper/login.py`

**被代码真实调用（KEEP-NEW）**，不是摆设：`WebLoginService.cs:39-40` 拼出 `Assets/login-helper/login.py` 并以 python3 子进程拉起，`:149` 有「login helper missing」错误分支；`WebKitLoginHost.cs:29` 的 `IsAvailable()` 用同一路径判据；csproj 的 `Content Include="Assets/login-helper/**"` 负责把它复制到输出目录。**但它的调用链入口只有 LoginPage**（`WebLoginService.LoginAsync` 的唯一调用点是 `LoginPage.Linux.cs:50`），因此文件保留与否取决于上面的登录决策：若 LoginPage 退役，则 `login.py` + `WebLoginService` + `WebKitLoginHost` + Program.Linux.cs:136-142 子命令 + csproj Content 条目应作为一批处理（`BBSWebBridge.Linux.cs:18` 的 `IsLoggedIn` 是这批的最后一个消费者，需同时决定 BBS 页在无 cookie 时的降级表现）。

## 统计

| 判定 | 数量 |
|------|------|
| KEEP-LINUX | 25 |
| KEEP-NEW | 96 |
| DEAD | 24 |
| REDUNDANT | 4 |
| REVIEW | 0 |
| **合计** | **149** |

分类说明：REVIEW 为 0——149 个文件全部拿到了确定性证据。24 个 DEAD 中，10 个根本不参与编译或不属于代码（`.idea/` 5、`.junie/` 2、`Lunarward.csproj.user`、`.xaml.disabled`、已被 `<Compile Remove>` 的 `LoginPage.xaml.cs`），属仓库卫生；另外 14 个是**参与编译但零存活调用点**的死代码（见下「建议立刻删除」）。4 个 REDUNDANT 全部集中在登录页那一簇。

## 建议立刻删除（A 组：无任何连带改动，一次提交即可）

前 8 个与「是否参与编译」无关（`.idea/` 5、`.junie/` 2、`Lunarward.csproj.user`），纯仓库卫生；其余 9 个是参与编译或已排除编译但零存活引用的死代码，删除后 `dotnet build Starward.slnx -c Debug` 与全部 lint 脚本不受影响：

- `.idea/.idea.Lunarward.dir/.idea/.gitignore`
- `.idea/.idea.Lunarward.dir/.idea/indexLayout.xml`
- `.idea/.idea.Lunarward.dir/.idea/projectSettingsUpdater.xml`
- `.idea/.idea.Lunarward.dir/.idea/vcs.xml`
- `.idea/.idea.Lunarward.dir/.idea/workspace.xml`
- `.junie/mcp/mcp.json`
- `.junie/mcp/mcp.json.bak`
- `Lunarward.csproj.user`
- `Controls/Win2DCompat.cs`
- `Features/Background/VideoBgVolumeChangedMessage.cs`
- `Features/GameInstall/GamePackageService.cs`
- `Features/GameRecord/LoginPage.xaml.cs`
- `Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.disabled`
- `Features/RPC/RpcService.Linux.cs`
- `Features/Screenshot/ScreenshotWindowsDisabled.cs`
- `Helpers/Housekeeping.cs`
- `Helpers/WindowWaitAsync.Linux.cs`

## 建议删除（B 组：需连带改一行到两行）

| 待删文件 | 连带改动 |
|----------|----------|
| `Features/Update/SetupService.Linux.cs` | 删 `AppConfig.ServiceProvider.cs:103` `sc.AddSingleton<SetupService>();` |
| `Features/GameLauncher/GameAuthLoginButton.xaml` / `.xaml.cs` / `GameAuthLoginService.cs` | 删 `AppConfig.ServiceProvider.cs:152-153`（注释 + `AddHttpClient<GameAuthLoginService>()`）；Page 项由 SDK 默认项生成，csproj 无需改 |
| `Features/GameRecord/GameRecordRoleChangedMessage.cs` | 删 `GameRecordPage.xaml.cs:64-70` 的 `Register<GameRecordRoleChangedMessage>` 块（该块是专为 LoginPage 补的） |
| `Helpers/SystemBackdropHelper.Linux.cs` + `Compat/NotifyIcon/TaskbarIconCompat.cs` | csproj 加 `<Compile Remove="Features/ViewHost/SystemTrayWindow.xaml.cs" />` 并排除 `SystemTrayWindow.xaml` 的 Page 项（该窗在 Linux 永不构造） |

## 建议删除（C 组：登录页簇，需先真机验证再动）

| 待删文件 | 前置条件 / 连带改动 |
|----------|---------------------|
| `Features/GameRecord/LoginPage.xaml` / `.Linux.cs` / `.LinuxAuto.cs` | 先把 `GameRecordPage.xaml:504` 的 `WebLoginCommand` 改成 `LoginCommand`（走 HypLogin），并删 `GameRecordPage.xaml.cs:512` 导航与 `MainView.xaml.cs:188` 的 `"login"` 调试路由；上线前须真机确认 hyp:// 回调登录能回填战绩 |
| `Features/GameRecord/GameRecordService.cs` | 唯一消费者是 LoginPage，删除时同时删 `AppConfig.ServiceProvider.cs:151` |
| `Features/GameRecord/WebLoginService.cs` + `WebKitLoginHost.cs` + `Assets/login-helper/login.py` | 同一决定的下游：入口只有 `WebLoginService.LoginAsync`（唯一调用点 LoginPage.Linux.cs:50）；删则同时删 `Program.Linux.cs:136-142` 子命令与 csproj 的 `Content Include="Assets/login-helper/**"`，并决定 `BBSWebBridge.Linux.cs:18 IsLoggedIn` 在无 cookie 时的降级表现 |

## 需要跟进的仓库卫生

- `src/Lunarward/.junie/mcp/mcp.json` 与 `mcp.json.bak` 虽被 `.gitignore:410` 忽略，但仍在 git 索引里：`git rm -r --cached src/Lunarward/.junie` 再删工作区文件。
- `.idea/` 与 `Lunarward.csproj.user` 未跟踪，直接从工作区删即可（前者已被 `.gitignore:402` 覆盖，后者建议补进 .gitignore）。
- 三处过时注释建议同步修正：`GameRecordService.cs:50`「23 处调用方不受影响」（实际唯一消费者是 LoginPage）、`SetupService.Linux.cs:1-2`「保留类型以维持 DI 与页面引用」（页面引用那部分已 Compile Remove）、`GameRecordPage.xaml.cs:64` 对 LoginPage 旁路的说明。
