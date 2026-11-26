# s1b 审计（启动器/游戏设置/安装）

审计范围：`src/Starward/Features/{GameLauncher,GameSetting,GameInstall}` ↔ `src/Lunarward/Features/{GameLauncher,GameSetting,GameInstall}`，HEAD = 上游 0.19.0 已合并。
方法：两侧文件**逐个全文读**（大文件分段读全），行号全部用 `grep -n` / `sed -n` 复核；只读，未改动仓库。
快照时间：2026-11-26 16:17（CST）。期间另一个 agent 并发改了 `GameSettingPage.xaml.cs`（见该条目的「注意」），其余 12 个文件未被改动、行号可直接对照。

编译状态基线（命令输出摘要）：

```
$ ls src/Starward/Features/GameLauncher src/Lunarward/Features/GameLauncher ...
# 上游 GameLauncher 有 3 个移植侧不存在的文件：
#   GameNoticeWindow.xaml / GameNoticeWindow.xaml.cs / GameNoticeWindowClosedMessage.cs
# 移植侧额外：GameLauncherPage.InstallTask.cs(156 行)、GameLauncherService.Linux.cs(578 行)、
#             LaunchPreflight.cs(502 行)、LaunchReport.cs、GameAuthLoginButton/Service、GameState.cs

$ grep -n "Compile Remove" src/Lunarward/Lunarward.csproj
33:  Features/WindowBase.cs                      (被 WindowBase.Linux.cs 覆盖)
34:  Features/GameInstall/GameInstallController.cs   ← RPC 控制器整份排除
36:  Features/RpcRunner.cs
110: Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.cs  ← 被 .Linux.cs 覆盖
120-126: Features/Screenshot/*（若干）
127: Features/Setting/HotkeyManager.cs           ← 被 Helpers/HotkeyManager.Linux.cs 覆盖
128: Features/Setting/ScreenshotSetting.xaml.cs   ← 被 Setting/ScreenshotSetting.Linux.cs 覆盖
```

**本组 13 个文件里没有任何一个被 `Compile Remove`，也没有任何同名 `*.Linux.cs` 覆盖**——两侧同名文件都参与编译。
唯一的「同名双份」是 `GameLauncherService`：`GameLauncherService.cs` 与 `GameLauncherService.Linux.cs` 是**同一个 `internal partial class GameLauncherService` 的两个 partial**（csproj 注释亦如此说明），编译后合并；`GameInstallController.cs`（上游 RPC 控制器）在移植侧被整份排除，因为安装改走进程内 `Starward.GameInstall.GameInstallRunner`（D128）。

行数对照（up / port / diff）：

| 文件 | 上游 | 移植 | diff 行 |
| --- | --- | --- | --- |
| GameLauncherPage.xaml | 223 | 243 | 24 |
| GameLauncherPage.xaml.cs | 990 | 1369 | 393 |
| GameLauncherService.cs | 669 | 793 | 164 |
| GameBannerAndPost.xaml | 149 | 158 | 35 |
| GameBannerAndPost.xaml.cs | 331 | 440 | 157 |
| GameNoticeService.cs | 103 | 106 | 25 |
| GameSettingPage.xaml | 199 | 282 | 93 |
| GameSettingPage.xaml.cs | 480 | 943 | 499 |
| GameSettingService.cs | 321 | 456 | 327 |
| GameInstallService.cs | 388 | 342 | 292 |
| PreDownloadButton.xaml | 204 | 204 | 4 |
| PreDownloadButton.xaml.cs | 205 | 206 | 5 |
| InstallGameDialog.xaml | 282 | 282 | 4 |
| GameLauncherPage.InstallTask.cs | — | 156 | 新增 |

---

### Features/GameLauncher/GameLauncherPage.xaml

- **编译状态**：参与编译（无 Compile Remove，无 `*.Linux.cs` 覆盖）。
- **逐行结论**：差异 3 处。整体结构（拖放根 Grid、背景切换、Banner、右下角四个按钮、崩三区服下拉、云游戏/每日）逐行对应。

- 差异：
  - [上游 2–3] vs [移植 2–3] ｜平台适配｜`<sf:PageBase ...>` 无缓存声明｜第 3 行加 `NavigationCacheMode="Required"`｜页面被缓存复用，切页不再重建（配合 D62/D94 的状态重载）｜**保留**（Uno 下重建成本远高于 WPF；但要靠 code-behind 手动清状态，见下条文件的 5 处改动）
  - [上游 56] vs [移植 57] ｜工程性｜`<sfb:BackgroundSwitcher HorizontalAlignment="Stretch"`（匿名）｜加 `x:Name="BackgroundSwitcher"`｜无用户可见影响｜**保留**（`LoadGameContent()` 需要给它换 `CurrentGameId`）
  - [上游 无] vs [移植 69–85] ｜功能多出｜无｜新增 `StackPanel_InstallTask`（`TextBlock_InstallTask` FontSize 12 + `ProgressBar_InstallTask` Height 3 / Minimum 0 / **Maximum 1**）｜底部多一条安装/预下载状态条（上游只在 `StartGameButton` / `PreDownloadButton` 内部显示）｜**保留，但必须修**：`GameLauncherPage.InstallTask.cs:84` 写 `Value = task.PercentProgress * 100`，而这里 `Maximum="1"`，进度条恒为满格（详细见该文件条目）

---

### Features/GameLauncher/GameLauncherPage.xaml.cs

- **编译状态**：参与编译。
- **逐行结论**：差异 10 处。上游 990 行的业务逻辑（版本检查、区服、DX12、定位游戏、启动、安装/更新六个入口、任务进度、拖放背景、设置对话框、云游戏）在移植侧**逐段存在且顺序一致**，新增内容集中在「缓存页切游戏必须重载」和「Linux 启动预检」。

- 差异：
  - [上游 57–68] vs [移植 65–99] ｜平台适配｜`OnLoaded()` 里本事无状态、直接 `_dispatchTimer.Tick += ...` + 4 条 Register｜重写成：`_gameChangedHandled` 判定（B30，避免 OnGameChanged 后紧跟的 Loaded 二次初始化）→ `LoadGameContent()` → `StartInstallTaskWatcher()` → Tick 先 `-=` 再 `+=`（D94，缓存页 Unloaded 会摘掉 Tick）→ 同 4 条 Register + 第 5 条 `CurrentGameChangedMessage`｜保证缓存页重新挂载后进度仍更新｜**保留**
  - [上游 无] vs [移植 93–99] ｜平台适配｜无｜`protected override void OnGameChanged() { _gameChangedHandled = true; LoadGameContent(); }`｜切换游戏自动换内容｜**保留**
  - [上游 无] vs [移植 107–136] ｜平台适配｜无（上游每次导航新建页面，字段天然是默认值）｜`LoadGameContent()` 手工把 9 个页面级字段清零 + `Button_CloudGame.Visibility=Collapsed` + 给下挂控件换游戏（`BackgroundSwitcher.CurrentGameId` / `Button_PlayTime.RefreshPlayTime` / `GameBannerAndPost.CurrentGameId`）+ 重跑 `InitializeGameFeature/CheckGameVersion/UpdateGameInstallTask/CheckCloudGame/InitializeGameServerAsync`，整体 try/catch 记 `LogError`｜这是缓存页的正确代价，行为等价于上游「重建页面」｜**保留**
  - [上游 无] vs [移植 141–159] ｜平台适配｜无｜`OnCurrentGameChanged(CurrentGameChangedMessage)`：同 id 直接 return，否则改 `CurrentGameId/CurrentGameBiz/ContentGameId` 后 `LoadGameContent()`｜保留
  - [上游 72–77] vs [移植 162–168] ｜平台适配｜`UnregisterAll(this)` + `Tick -=` + `Stop()`｜多一句 `StopInstallTaskWatcher()`｜保留
  - [上游 590–613] vs [移植 681–712] ｜行为错位（轻）｜`catch (Exception ex) { InAppToast.MainWindow?.Error(ex); }`｜第 699–703 先单独 `catch (LaunchPreflightException)` → `ShowPreflightDialogAsync`，第 707–710 再用 `if (!OperatingSystem.IsWindows()) { Error((string?)null, Lang.Lunarward_Launch_Failed + " " + ex.Message, 6000); }`｜Linux 用户能看到原因，Windows 分支**任何提示都没有**（编译器不会报错，因为上游的 `Error(ex)` 被整个替换掉了）｜**建议改回无条件 `Error(ex)`，Linux 分支文案保留**（当前 Windows 静默是纯粹的行为退化；虽然 Windows 不发行，但这属于把上游语义改错，不是平台必需）
  - [上游 无] vs [移植 699–703、716–990] ｜功能多出｜无｜`ShowPreflightDialogAsync(PreflightResult)` 现场拼 StackPanel（逐项 ✓/!/✕ + 修复按钮）+ `ContentDialog`；`GetPreflightActionText`；`RunPreflightActionAsync` 支持 OpenGameSettings/OpenRuntimeSettings/OpenGameFolder/OpenPrefixFolder/RebuildPrefix（含删前缀二次确认 + `Task.Run(PrefixManager.EnsurePrefix)`）/InstallGraphics/ShowPrefixProcesses；另有 `ResolveCurrentPrefixAsync` 940、`ResolveCurrentExePathAsync` 953、`TryEnsureForGraphicsAsync` 974｜Linux 专属，上游无对应物｜**保留**（这是移植侧最需要的失败可见性）
  - [上游 902–947] vs [移植 1279–1323] ｜平台适配｜`await BackgroundService.ChangeCustomBackgroundFileAsync(file)`（`StorageFile` 重载）｜第 1297 行 `ChangeCustomBackgroundFileAsync(file.Path)`（字符串路径）｜拖放换背景仍可用｜**保留**（Uno 的 `DragEventArgs.DataView.GetStorageItemsAsync()` 拿不到可用的 `StorageFile`）
  - [上游 无] vs [移植 18 与 33 行] ｜工程性｜—｜`using Starward.GameInstall;` 重复两次｜无｜**建议删一处**
  - [上游 无] vs [移植 79 / 162] ｜工程性（功能重叠）｜无｜`StartInstallTaskWatcher()` 每 1 s 画底部状态条，同时上游那套 `_dispatchTimer`（100 ms）→ `UpdateGameInstallTaskProgress`（移植 1239 行，原样保留）仍在把同一个任务推给 `Button_StartGame` / `Button_PreDownload`｜同一任务在**两处 UI** 各画一遍；两者状态机各自独立（`_lastInstallState` vs `GameState`），一旦一边先 Stop，另一边仍显示旧态｜**建议二选一**：要么删 `InstallTask.cs` 的定时器只留上游按钮内进度，要么保留状态条但把 `UpdateGameInstallTaskProgress` 降级为只驱动按钮

- 额外：
  - 移植侧多出的能力：缓存页状态重载（被 `OnLoaded`/`OnGameChanged`/`OnCurrentGameChanged` 调用，**有调用**）、启动预检对话框（被 `StartGameAsync` 的 `LaunchPreflightException` 分支调用，**有调用**）、底部安装状态条（**有调用**）、`InstallTask` 定时器（**有调用**）。
  - 上游有而移植没有：**无**（上游 990 行的每个方法都能在移植侧找到对应体；`RunningGameService.AddRuninngGame` 699/1356 仍在调用，编译用的是 `Features/Overlay/RunningGameService.Linux.cs`，因为 `Features/Overlay/RunningGameService.cs` 被 Compile Remove）。

---

### Features/GameLauncher/GameLauncherService.cs

- **编译状态**：参与编译；与 `GameLauncherService.Linux.cs`（578 行）是同一 partial 类的两半，**真正干活的启动路径大部分在 `.Linux.cs`**。
- **逐行结论**：差异 10 处。Install Path 区 58–185、Game Version 区 189–337、`GetGameExeName*` 349–381、`IsGameExeExistsAsync` 390、`IsProcessPending` 438–461、`AppendDX12Async` 672–704、`AppendLoginAuthTicketAsync` 707–733、Third Party Tool 区 740–787 与上游**一字不差**。

重点怀疑项逐条结案：

**(1) 启动参数构造** —— 与上游同构：`sb.Append(AppConfig.GetStartArgument(gameId)?.Trim())` → `AppendDX12Async` → `AppendLoginAuthTicketAsync` → `string arg = sb.ToString()`（移植 531–536 / 上游 496–501）；`AppendDX12Async`、`AppendLoginAuthTicketAsync` 两函数**逐字符相同**（含 ` login_auth_ticket=`、` login_storage_type=local|session`、`--` 之外的引号规则）。参数语义一致。

**(2) wine 解析** —— 上游无对应（`UseShellExecute=true` + `runas`）。移植在 `.Linux.cs`：`ResolveWine()` 46–80 顺序为 `LUNARWARD_WINE` → Proton Experimental / Proton 9.0 / Proton Hotfix 的 `files/bin/wine` → PATH；`ResolvePrefix()` 83–97 为 `LUNARWARD_WINEPREFIX` → `~/.wine` → `<XDG_DATA_HOME>/lunarward/prefix/<game>`。失败时抛「未找到 wine。请设置环境变量 LUNARWARD_WINE…」，可见。

**(3) 环境变量装配** —— `CreateGameProcessStartInfo` 238–430：`WINEPREFIX`、`PlayTimeSession.ActiveWine`、`WINEDEBUG`（默认 `-all`）先手写，随后 `RuntimeEnvironment.Assemble` → `LaunchRecipeBuilder.Build` → **用 `recipe.Environment` 整体覆盖**（不是合并），再把 `info.FileName = recipe.Argv[0]` 并重填 `ArgumentList`；`recipe.MissingWrappers` 走 toast、`recipe.Overrides` 记日志。`ApplyWineRuntimeEnvironment`（437–476，补 `WINEDLLPATH`/库路径/`STEAM_COMPAT_*`/`PROTONPATH`）**只有 `Program.Linux.cs:368` 与 `:394` 两个调用方**，游戏启动路径不调用它——因为 `RuntimeEnvironment` 已覆盖同一职责。这是「两套装配逻辑并存但只有一套在启动路径上」，可接受，但要注意 `LaunchRecipeBuilder` 若返回空 `Environment`，`WINEDEBUG=-all` 会被覆盖掉。

**(4) 预检调用顺序** —— 移植 547–591：`Stopwatch` 起 → `ProfileBinding.EnsureFor`（可能新建并 toast）→ `Task.Run(LaunchPreflight.Check)`（不阻塞 UI 线程，合规）→ 不通过则记 `LaunchHistoryStore` 并 `throw LaunchPreflightException` → `Task.Run(CreateGameProcessStartInfo)`（内部才 `AcquirePrefixLock` + `EnsurePrefix` + `GraphicsInstaller.EnsureInstalled`）→ `Process.Start`。**预检在前、建前缀在后**是对的：`LaunchPreflight.cs:149–174` 明确「预检只看不建」，前缀不存在时给 `Ok` + `尚未准备，启动时创建`，不会误判失败。

**(5) 启动后 HDR 重放** —— 见差异第 2 条。

**(6) 游玩时长挂钩** —— 移植 639–647：`if ((thirdPartyTool || AppConfig.StartGameWithCMD) && OperatingSystem.IsWindows()) StartProcessToLogAsync(gameId); else StartProcessToLogAsync(gameId, process.Id);`；上游 519–525 同结构但无 `&& IsWindows()`（Android/Linux 上 `StartGameWithCMD` 分支本就不会进，见差异第 3 条）。**语义等价，且 Linux 侧按 pid 记录是必需的**（B35：wine 进程名不是游戏 exe）。

**(7) 截图快捷键** —— `grep -rn "Screenshot|Hotkey" src/Lunarward/Features/GameLauncher src/Starward/Features/GameLauncher` **两侧零命中**：上游 0.19.0 的 `GameLauncherService` 里根本不存在截图/热键代码，本文件与截图快捷键无关。移植侧截图热键的实体是 `src/Lunarward/Helpers/HotkeyManager.Linux.cs:11` + `src/Lunarward/Features/Setting/ScreenshotSetting.Linux.cs`（`Features/Setting/HotkeyManager.cs`、`Setting/ScreenshotSetting.xaml.cs` 均被 Compile Remove，`Setting/HotkeySetting.xaml.cs` 里那些 `HotkeyManager.*` 调用走的是 Linux 版）。

- 差异：
  - [上游 405–410] vs [移植 407–430] ｜平台适配｜`Process.GetProcessesByName(name.Replace(".exe",""))` 按进程名找、`SessionId` 过滤｜`if (!OperatingSystem.IsWindows())` 分支改走 `Features.PlayTime.PlayTimeSession.FindProcessByCommandLine(exeName)` 扫 `/proc/*/cmdline`，`Process.GetProcessById` 包 `ArgumentException` 返回 null；Windows 分支原样保留｜Linux 上「游戏已在运行」提示与单实例拦截才有效（B34）｜**保留**
  - [上游 500] vs [移植 515–530] ｜平台适配（+ 死注释）｜`SetGenshinEnableHDR(gameId)`：同步写 HKCU，位置在两条 append 之后、`arg` 取值之前｜删掉该私有方法，改为 `if (...Game is GameBiz.hk4e) { try { await GameSettingService.SetGenshinEnableHDRAsync(biz, AppConfig.EnableGenshinHDR).ConfigureAwait(false); } catch { LogWarning } }`，位置在两条 append **之前**；并且第 515–516 与 517–519 是**两条互相打架的注释**（先说「HDR 改由设置页保存时写入目标前缀」，紧接又说「启动前按 AppConfig 重放一次」）｜Linux 上每次启动 hk4e 都要先跑一次 `wine reg import`（0.1–4 s），启动变慢；写失败只 LogWarning，用户看不到，游戏读到旧值｜**保留功能，删掉第 515–516 那条已作废的注释**；「位置从 append 之后挪到之前」无实际影响（HDR 不碰 `sb`），但建议挪回 append 之后以贴合上游读码习惯
  - [上游 503–507] vs [移植 541] ｜平台适配｜`if (!thirdPartyTool && AppConfig.StartGameWithCMD)` 就套 `cmd.exe /c start`｜加 `&& OperatingSystem.IsWindows()`｜Linux 不再套 shell，保证「游戏是 wine 的第一个进程」｜**保留**
  - [上游 无] vs [移植 546] ｜工程性｜无｜`_logger.LogInformation("Start game ({biz})\r\npath: {exe}\r\narg: {arg}", ...)`（**含完整参数行，其中有 `login_auth_ticket=`**）｜日志里会出现登录票据（明文），任何附带日志的诊断包都会带走它｜**建议脱敏**（票据只留前后 4 位）
  - [上游 508–516] vs [移植 547–591] ｜功能多出｜`ProcessStartInfo{UseShellExecute=true, Verb=verb}` + `Process.Start`｜Linux 专属：`ProfileBinding.EnsureFor` + `LaunchPreflight.Check` + `LaunchHistoryStore.Record(PreflightFailed)` + `LaunchReport.Write` + `Task.Run(CreateGameProcessStartInfo)`｜首次启动体验、失败可见性大幅改善｜**保留**
  - [上游 516] vs [移植 600–611] ｜平台适配｜裸 `Process.Start(info)`｜先把 `info.Environment["WINEPREFIX"]` 取成 `launchPrefix`，`Process.Start` 包 try/catch，失败 `ReleasePrefixLock(launchPrefix)` 后 rethrow（R05）｜避免一次 `ENOENT` 让同名游戏整个进程生命周期都起不来｜**保留**
  - [上游 无] vs [移植 614–637] ｜功能多出｜无｜`LaunchHistoryStore.Record(Started)`、`LastKnownGoodStore.Remember(launchProfile, launchPrefix)`（R111）、`info.RedirectStandardOutput` 时 `Runtime.OutputCapture.Attach($"game:{biz}", process)`（D27，否则管道写满卡死游戏）｜保留
  - [上游 519–525] vs [移植 639–647] ｜平台适配｜见重点怀疑 (6)｜同｜保留
  - [上游 无] vs [移植 649–661] ｜功能多出｜无｜`Process.Start` 返回 null 时记 `LaunchOutcome.StartFailed`｜保留
  - [上游 485–515 的 `verb`] vs [移植 485、500、511] ｜死代码｜`verb` 最终赋进 `psi.Verb`｜`verb` 仍被赋值（`"runas"` / `""`），但 `CreateGameProcessStartInfo` 不使用它，**Linux 路径上 `verb` 是纯死变量**｜无｜**建议删**（或至少在赋值处加注释说明「仅 Windows 语义」）
  - [上游 616–666] vs [移植 740–787] ｜—｜`GetThirdPartyToolPath`/`SetThirdPartyToolPath`｜一字不差｜无｜一致

- 额外：
  - 移植侧多出能力（14 项）：`Handle`/前缀锁（`TryLockPrefix`/`AcquirePrefixLock`/`ReleasePrefixLock`，被 `CreateGameProcessStartInfo` 与 `StartGameAsync` 调用）、`CreateGameProcessStartInfo`、`AttachSessionCleanup`、`RunExitCompanions`、`DescribeLaunchPlan`（CLI `--launch-plan` 专用）、`SplitArguments`（被 `.Linux.cs:257` 与 `CompanionRunner.cs:109` 调用）、`ApplyWineRuntimeEnvironment`（仅 `Program.Linux.cs:368/394`）、`ResolveWine`/`ResolvePrefix`/`PrefixRoot`。—— 全部**有调用方**。
  - 上游有而移植没有：`SetGenshinEnableHDR(GameId)` 私有同步方法（被内联替换）、`psi.Verb`/`UseShellExecute` 语义、`cmd.exe` 提权启动。—— 均为 Windows 平台必需物，Linux 无等价概念。
  - 附注：`RunOnceService.cs:88` 另有一份与 `SplitArguments` 同名的实现（重复代码），`CompanionRunner.cs:109` 明确用的是 `GameLauncherService.SplitArguments`。

---

### Features/GameLauncher/GameBannerAndPost.xaml

- **编译状态**：参与编译。
- **逐行结论**：差异 7 处。骨架（380×304 容器、`Pivot` 两个 Header、`Button_InGameNotices` + `alert.png`、行高 176/124、日期列）与上游一致。

- 差异：
  - [上游 35–37] vs [移植 35–38] ｜平台适配｜`Grid Name="Grid_BannerContainer"` 只挂 PointerEntered/PointerExited｜加 `Tapped="Grid_BannerContainer_Tapped"`｜容器级兜底点击（见 `.cs` 第 10 条），但也是「一次点击可能开两次」的一半原因｜**保留但需修**（`.cs` 里补 `e.Handled`）
  - [上游 46 区域] vs [移植 46–48] ｜平台适配｜`<sc:CachedImage>` 无解码宽度、`Tapped="Image_Banner_Tapped"` 写在 XAML｜加 `DecodePixelWidth="760"`、改 `Loaded="Image_Banner_Loaded"`（事件改由 code-behind `AddHandler(..., handledEventsToo: true)` 挂）｜大图内存/首帧时间下降；点击改为穿透订阅｜**保留**
  - [上游 无] vs [移植 74] ｜工程性｜`<Pivot ItemsSource="{x:Bind PostGroups}">`（匿名）｜加 `x:Name="Pivot_Posts"`｜无｜**保留**（`UpdateGameContentAsync` 要在切游戏时复位 Pivot，见 `.cs` 第 6 条）
  - [上游 111] vs [移植 114] ｜平台适配｜`<ScrollViewer Margin="0,0,-12,0"`（负边距吃掉滚动条宽度）｜`Margin="0"`｜资讯列表右侧少 12 DIP 负偏移，滚动条区域看起来略宽｜**保留**（Uno 下负边距会把最后一行裁掉），但**应补一行注释**，当前无说明
  - [上游 115] vs [移植 117] ｜平台适配｜`<ItemsRepeater>` 无 HorizontalAlignment｜加 `HorizontalAlignment="Stretch"`｜配合 `MaxWidth` 修「Title 顶出卡片」｜**保留**
  - [上游 122–133] vs [移植 121–142] ｜平台适配｜行模板 `<Grid ColumnSpacing="16">` + 标题用 `<Hyperlink NavigateUri="{x:Bind Link}">` + `TextTrimming="WordEllipsis"`｜`<Grid ColumnSpacing="16" MaxWidth="340" Tapped="PostRow_Tapped" Background="Transparent">` + 标题改普通 `TextBlock`（`MaxLines=1` / `CharacterEllipsis` / `NoWrap` / `TextFillColorPrimaryBrush`），并带两条注释说明（Uno `ItemsRepeater` 在 `ScrollViewer` 里按无限宽测量；`Hyperlink.NavigateUri` 走 `LaunchUriAsync` 在 Linux 静默失败，L-74）｜点击打开资讯改用 `OpenUrl.Open`，都能点；`CharacterEllipsis` 比 `WordEllipsis` 更早截断｜**保留**
  - [上游 127 之后] vs [移植 143] ｜文案/视觉｜Hyperlink 有下划线抑制属性｜普通 TextBlock 无悬停手型光标｜可点区域不再有 hover 反馈（仅靠 `Tapped`）｜**建议**给行加 `ProtectedCursor` 或在 PointerEntered 里换光标

- 额外：移植多出 `Pivot_Posts` 具名引用与行级 `Tapped`；上游的 `Hyperlink` 能力（右键/新窗口语义）在移植侧丢失——Uno 无对应，属软差异。

---

### Features/GameLauncher/GameBannerAndPost.xaml.cs

- **编译状态**：参与编译。
- **逐行结论**：差异 12 处。消息订阅骨架、`ShowBannerAndPost`、`UpdateGameNoticeAlertAsync`、`_bannerTimer_Tick`、`FlipView_Banner_Loaded`、`Grid_BannerContainer_PointerEntered/Exited`、`AddOne(int)` 与上游一致。

- 差异：
  - [上游 12–20] vs [移植 11–18] ｜工程性｜using 列表｜多 `using System.Collections.ObjectModel;`（未使用）、`using Starward.GameInstall;` 重复两次、去掉 `Vanara.PInvoke`｜无｜**建议清理未使用 using**
  - [上游 43–52] vs [移植 44–62] ｜平台适配｜`public GameId? CurrentGameId { get; set; }` 自动属性｜改手写属性，setter 里 `if (IsLoaded) _ = ReloadForCurrentGameAsync();`（D62）｜父级在页面缓存下改游戏时能自刷新｜**保留**
  - [上游 无] vs [移植 32、65–77] ｜平台适配｜无｜`_contentGameId` 字段 + `ReloadForCurrentGameAsync()`｜与 `_Loaded` 里的「内容 id 不同才拉」配合，避免重复请求｜保留
  - [上游 59–66] vs [移植 97–107] ｜平台适配｜`_Loaded` 无条件 `UpdateGameContentAsync(); UpdateGameNoticeAlertAsync();`，并注册 `GameNoticeWindowClosedMessage`｜改条件式：`_contentGameId != CurrentGameId || Banners is null || PostGroups is null` 才拉内容，再刷红点；**不再注册** `GameNoticeWindowClosedMessage`（公告窗已改浏览器）｜上游「关窗后刷红点」的触发点消失——移植用「打开即置 `IsGameNoticesAlert=false`」替代（第 11 条）｜**保留**（但见第 11 条的取舍说明）
  - [上游 69–76] vs [移植 110–118] ｜平台适配｜`_Unloaded` 里 `Banners = null; PostGroups = null;`｜**不再置空**，改为注释说明「Uno Pivot 复用容器，置空后只剩空卡片」｜缓存页反复进出后不会出现空白卡片｜**保留**（上游靠新建页面天然回到 null）
  - [上游 170–188] vs [移植 205–241] ｜平台适配｜`UpdateGameContentAsync` 直赋两次集合后 `ShowBannerAndPost = true`｜增 `_contentGameId != CurrentGameId` 保险（B62）+ `Pivot_Posts.ItemsSource = null; SelectedIndex = -1;` → `await Task.Delay(60)` → 重新赋 `ItemsSource` → `SelectedIndex = 0`（L-126 的 Uno `Pivot` `ArgumentOutOfRangeException`）｜修切游戏时 Pivot 崩；代价是每次拉内容多 60 ms 延迟｜**保留**
  - [上游 238–248] vs [移植 291–299] ｜平台适配｜XAML 直挂 `Tapped`｜新增 `Image_Banner_Loaded`：`element.AddHandler(UIElement.TappedEvent, new TappedEventHandler(Image_Banner_Tapped), true)`（handledEventsToo）｜`FlipView` 内部把 Tapped 标 handled，普通挂法收不到｜**保留**
  - [上游 无] vs [移植 302–316] ｜功能多出｜无｜`PostRow_Tapped`：`OpenUrl.Open(post.Link)` + `e.Handled = true`｜资讯可点｜保留
  - [上游 238–248] vs [移植 319–332] ｜平台适配｜`Launcher.LaunchUriAsync(banner.Image.Link)`｜`OpenUrl.Open(...)`，`DataContext` 取不到时回退 `FlipView_Banner.SelectedItem`｜Linux 下真能打开（`LaunchUriAsync` 静默失败，L-74）｜**保留**
  - [上游 无] vs [移植 335–347] ｜平台适配｜无｜`Grid_BannerContainer_Tapped`：同样从 `SelectedItem` 取，失败只 `Serilog.Information`｜容器级兜底｜**保留**
  - [上游 305–317] vs [移植 405–426] ｜平台适配｜`OpenGameNoticeWindow()` 新建 `GameNoticeWindow{ ParentWindowHandle = (nint)XamlRoot.ContentIslandEnvironment.AppWindowId.Value }.Activate()`｜`OpenGameNoticeWindowAsync()`：取 URL → `IsGameNoticesAlert = false` → `Helpers.OpenUrl.Open(url)`，失败 toast「没能打开公告：系统里没有可用的浏览器？」｜公告改浏览器打开（已知缺口，`GameNoticeWindow` 三文件已删）｜**保留**。副作用：`IsGameNoticesAlert = false` 是「先当作已读再尝试打开」，浏览器没起来红点也灭了（B61 的取舍）
  - [上游 无] vs [移植 319–332 与 335–347 都无 `e.Handled = true`] ｜行为错位（推断）｜上游只有一处入口（`Image_Banner_Tapped`）｜移植侧同一张 banner 同时挂了容器 `Tapped` 和图片的 `AddHandler(handledEventsToo:true)`，两处都不设 `e.Handled`｜**同一次点击有可能 `OpenUrl` 打开两次**（浏览器开两个标签）。是否复现取决于 `FlipView` 是否已把事件标 handled——**未真机验证，按推断记录**｜**建议**在 `Image_Banner_Tapped` 末尾补 `e.Handled = true`

- 额外：
  - 移植多出：`_contentGameId` 缓存、`ReloadForCurrentGameAsync`、`OpenGameNoticeWindowAsync`、`PostRow_Tapped`、`Image_Banner_Loaded`、`Grid_BannerContainer_Tapped` —— 全部有调用方（XAML 事件 / setter）。
  - 上游有而移植没有：公告窗（`GameNoticeWindow`）与 `GameNoticeWindowClosedMessage` 订阅、`_Unloaded` 的清空集合行为、`Launcher.LaunchUriAsync` 的「用默认浏览器打开」语义（移植用 `OpenUrl` 等价替代，但少了 `Launcher` 的协议兜底）。

---

### Features/GameLauncher/GameNoticeService.cs

- **编译状态**：参与编译。
- **逐行结论**：差异 2 处。注册表键名、三种 uid 的解析方式（bh3 int / hk4e byte[]→UTF8→Trim→long / hkrpg int，nap 返回 0）、`IsNoticeAlertAsync` 逻辑均一一对应。

- 差异：
  - [上游 33–37] vs [移植 34] ｜平台适配｜`public string GetGameNoticeUrl(GameBiz biz)`｜`public async Task<string> GetGameNoticeUrlAsync(GameBiz biz, CancellationToken cancellationToken = default)`｜调用方全部改 await｜**保留**（UI 路径读 wine 注册表一次 fork 0.1–4 s，不能阻塞 UI，L-127）
  - [上游 55–97] vs [移植 56–100] ｜平台适配｜`GetUidFromRegistry` + `Microsoft.Win32.Registry.GetValue`｜`GetUidFromRegistryAsync` + `WineRegistry.GetValueAsync(key, name, null, ct)`，nap 直接返回 0（注释：所以切绝区零秒切）｜读的是「当前前缀」而不是 `~/.wine`｜**保留**

- 额外：移植多出异步重载（有调用方）；上游 `GetGameNoticeUrl` 唯一调用方是 `GameNoticeWindow.xaml.cs:143`（该文件移植侧已删除），移植侧改由 `GameBannerAndPost.xaml.cs:405` 起的 `OpenGameNoticeWindowAsync` 调用——**没有孤儿**。

---

### Features/GameSetting/GameSettingPage.xaml

- **编译状态**：参与编译。
- **逐行结论**：差异 9 处。分辨率/全屏/无边框/自定义分辨率/语音、星铁 FPS、原神 HDR 两个 InfoBar、未安装 emoji、底部应用条逐行一致。

- 差异：
  - [上游 2–3] vs [移植 2–3] ｜平台适配｜`<sf:PageBase>`｜加 `NavigationCacheMode="Required"`｜同上｜保留
  - [上游 13–14] vs [移植 13–14] ｜平台适配｜外层 Grid 无背景｜加 `Background="{ThemeResource ApplicationPageBackgroundThemeBrush}"`（D61）｜Linux 无丙烯，铺实底避免壁纸透字｜保留（已知软差异）
  - [上游 20] vs [移植 20] ｜工程性｜`<ScrollViewer>` 匿名｜加 `x:Name="ScrollViewer_Main"`｜无｜保留
  - [上游 无] vs [移植 22–33] ｜功能多出｜无｜「运行环境绑定失效」InfoBar（`IsProfileBindingMissing` / `MissingProfileBindingMessage` / `Button_RebindProfile_Click`）｜绑定丢了能就地重绑｜保留
  - [上游 34] vs [移植 34–47] ｜平台适配｜`<Grid x:Name="Grid_Content">` 无 RowDefinitions｜加 4 条 RowDefinition（诊断单独一行；注释引 L3/T-UI-31）｜为下面三段新内容腾行｜保留
  - [上游 91] vs [移植 115–119] ｜平台适配｜图形设置 `StackPanel Grid.Row="1" Grid.Column="1"`（上游 Grid 无行定义 → 实际落在第 0 行）｜**去掉** `Grid.Row="1"`，显式归 0（注释说明）｜排版与上游「看起来一样」｜**保留**（注释已交代，属正确的等价改写）
  - [上游 无] vs [移植 170–186] ｜功能多出｜无｜「运行配置」区（`ComboBox_Profile`）｜选运行环境方案｜保留
  - [上游 无] vs [移植 188–212] ｜功能多出｜无｜「后启动程序」区（`Button_AddCompanion` / `Panel_Companions` / `TextBlock_CompanionEmpty`）｜伴随启动管理｜保留
  - [上游 无] vs [移植 214–225] ｜功能多出｜无｜「诊断」区（`Button_Diagnose`）｜手动预检｜保留

- 额外：移植多出三块设置区 + 绑定失效提示（均有 code-behind 调用）；上游该 XAML 无内容缺失。

---

### Features/GameSetting/GameSettingPage.xaml.cs

- **编译状态**：参与编译（对应 XAML 同名 code-behind）。
- **注意**：本文件在我审计期间（2026-11-26 16:10:36）被**其他 agent 并发修改**（另一组在干活）：删掉文件头 BOM，并在 `XamlRoot.GetAppWindow()` 分支里加了 `_displayInformation` 退订+Dispose（注释标 D135），文件从 935 行变 943 行，第 337 行之后的行号整体 +8。下表行号已按**当前工作区**核准。
- **逐行结论**：差异 11 处。属性区、`InitializeResolutionItem`、`ComboBox_Resolution_SelectionChanged`、`UpdateResolutionComboBoxSelection`、`UpdateHdrState`、`OpenGenshinHDRLumianceSettingWindow` 的骨架与上游对应；差异集中在「同步读注册表 → 后台异步读」和移植新增的三块功能。

- 差异：
  - [上游 40–62] vs [移植 89–124] ｜平台适配 + **行为错位**｜`OnNavigatedTo` 只做 emoji 与 bh3_global 区服映射｜先加空态早退（`CurrentGameBiz.Value` 为空 → 清 emoji + toast + return），再在第 **109–110** 行调 `RefreshProfileSelector(); RefreshCompanions();`，**然后**才是 111–124 的 bh3_global → bh3_jp/kr/os/asia 映射｜**bh3_global 页上「运行配置」下拉与「后启动程序」列表按 `bh3_global` 展示，而 `OnLoaded`→`InitializeGameSettingAsync`（139 行）用的是映射后的 `bh3_jp/kr/os/asia`**——两处 biz 不一致，用户看到的绑定不是设置真正读写的那份。这是本组最明确的一处实现错位｜**改回上游**：把 109–110 两行移到 bh3_global 映射之后（或改成 `OnLoaded` 里调）
  - [上游 65–69] vs [移植 125–140] ｜平台适配｜`InitializeResolutionItem(); await InitializeGameSettingAsync();`｜`InitializeResolutionItem()` 包 try/catch（Linux `DisplayArea.GetFromWindowId` 不可用 → 分辨率列表为空，不抛）｜设置页能进，但分辨率下拉**永远为空**（自定义分辨率仍可勾）｜**保留**（引擎能力缺失，建议在 12-shims 里登记为已知缺口）
  - [上游 无] vs [移植 144–151] ｜平台适配｜无｜`protected override async void OnGameChanged()` → `await InitializeGameSettingAsync()`｜缓存页切游戏会重读｜保留
  - [上游 210–278] vs [移植 292–400] ｜平台适配｜同步调 `GetStarRailFPSIndex` / `GetGameResolutionSetting` / `GetGameVoiceLanguageSetting`（各一次 fork，实测切页 1729 ms）｜先清空可见性并 **`ErrorMessage = null`（305）**，再把三条读放进 `Task.Run(() => new GameSettingSnapshot(...))`，回 UI 线程后赋属性，并 `DispatcherQueue.TryEnqueue(() => Grid_Content.InvalidateMeasure())`｜UI 不再冻｜**保留异步化，但 `ErrorMessage = null` 建议改回上游默认文案**：上游进页面就显示「设置无法生效」的常驻提示，移植侧清掉后用户以为能生效，只有点应用失败才看到错误
  - [上游 282–306] vs [移植 244–290] ｜平台适配｜`DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary)` + `Resolutions`｜同机保留，外层 try/catch（见上条）｜分辨率列表为空｜保留
  - [上游 380–396] vs [移植 325–350] ｜平台适配｜`OpenGenshinHDRLumianceSettingWindow` 里 catch 只 `Debug.WriteLine`｜catch 记日志 + `XamlRoot.GetAppWindow()` 为空则置 `HDRNotSupported = true`｜HDR 对话框打不开时有可见降级｜保留
  - [上游 400–447] vs [移植 538–576] ｜平台适配｜`[RelayCommand] private void ApplySetting()` 同步写四项，catch → `ErrorMessage = ex.Message`｜`[RelayCommand] private async Task ApplySettingAsync()`，逐项 await 四个 `*Async`（XAML 仍绑 `ApplySettingCommand`，CommunityToolkit 自动去 `Async` 后缀，绑定未坏）｜UI 不冻；wine 写失败时 `ex.Message` 可能是英文 wine 输出（直接进 UI）｜**保留**，建议把 wine 异常包一层中文提示再赋值 `ErrorMessage`
  - [上游 无] vs [移植 33–75] ｜功能多出｜无｜`Button_Diagnose_Click` 复用手动预检对话框｜保留
  - [上游 无] vs [移植 339–347] ｜平台适配（并发新增）｜无（上游每次导航新建页面，`DisplayInformation` 只创建一次）｜切游戏/重进本页时先 `_displayInformation.AdvancedColorInfoChanged -= …` + `Dispose()` 再重建（注释标 D135）｜修「事件越挂越多」的泄漏；与上游语义等价｜**保留**
  - [上游 无] vs [移植 610–940] ｜功能多出｜无｜「运行配置」「后启动程序」整套（`RefreshCompanions` 709、`CreateCompanionRow`、`ShowCompanionEditorAsync`、`Button_AddCompanion_Click` 795、`Button_OpenCompanionFolder_Click` 里的 `Process.Start(new ProcessStartInfo("xdg-open") …)` 805）｜保留；**但大量硬编码中文**（会话内文案不走 `Lang`），与上游 i18n 口径不一致
  - [上游 无] vs [移植 770–771] ｜死代码｜无｜删除按钮回调里多调了一次 `RefreshProfileSelector()`（与本功能无关），且下一行缩进错乱｜无功能影响，读码误导｜**建议删**

- 额外：
  - 移植多出：空态早退、`OnGameChanged`、`GameSettingSnapshot` 后台读、`InvalidateMeasure` 修正、运行配置/后启动程序/诊断三块 UI 逻辑。—— 均有调用。
  - **上游有而移植没有**：`ErrorMessage` 的初始默认文案（被 305 行 `= null` 清掉）、`ApplySetting` 的同步语义（改为 async，语义等价）。
  - **UI 线程等待**：`ApplySettingAsync`/`InitializeGameSettingAsync` 已全部 async；但 `GameSettingService` 仍保留 6 个同步 wrapper（见下一文件），任何新代码误用即回到「UI 线程 fork」的老坑。

---

### Features/GameSetting/GameSettingService.cs

- **编译状态**：参与编译（与 `GenshinHDRLuminanceSettingDialog.Linux.cs` 配合；`GenshinHDRLuminanceSettingDialog.xaml.cs` 被 Compile Remove，**真正编译的是 `.Linux.cs`**）。
- **逐行结论**：差异 6 类。**注册表键名两侧完全相同**（含 `AudioSettings_*` 四个与 `LanguageSettings_LocalTextLanguage_*` 这类两侧都没用的死常量）；四个游戏分支的读写值域、JSON 结构、Clamp 区间全部一致。读写**对称性成立**（读什么键、写什么键、什么类型，一一对应）。

- 差异：
  - [上游 39–113] vs [移植 42–113] ｜平台适配｜`Microsoft.Win32.Registry.GetValue/SetValue(keyPath)`，keyPath = `biz.GetGameRegistryKey()`（= 本机 HKCU 游戏键）｜`WineRegistry.GetValue/GetKeyValuesAsync/SetValueAsync/SetValuesAsync(keyPath, ..., prefix)`，prefix 由 `ResolveGamePrefix(biz)`（372–384）= `ProfileBinding.Resolve` → `PrefixManager.TryGetExistingPrefix` 得出｜**读写路径从「本机注册表」变成「游戏真正用的 wine 前缀」**（L-62：写到 `~/.wine` 游戏读不到）｜**保留**（这是移植的核心正确性点）
  - [上游 82–113] vs [移植 89–146] ｜工程性｜四个独立 `if` 逐键 `SetValue`（4 次写）｜抽 `BuildResolutionSettingValues` + 一次 `SetValuesAsync`（单次 `reg import`）｜写更快、失败可抛（`Lang.Lunarward_Error_SetGameSettingFailed` / `Lang.Lunarward_Error_PrefixNotFound`）｜保留
  - [上游 39/118/186/262] vs [移植 358–369 `TryGetReadPrefix`] ｜平台适配｜直接读｜前缀解析不出来就**跳过读**、不回落 `~/.wine`｜页面上显示 0/默认值（而不是别的前缀的旧值）｜保留（D134）
  - [上游 249–256] vs [移植 318–347] ｜平台适配｜仅 hk4e 写 `WINDOWS_HDR_ON_h3132281285`，无返回值｜`winePrefix ?? ResolveGamePrefix`，前缀为空**抛**（B69），走 `SetValuesAsync` 让失败可见｜HDR 写失败会提示而不是静默｜保留
  - [上游 289–310] vs [移植 423–455] ｜**不一致（写失败被吞）**｜`Registry.SetValue`（抛异常即冒泡）｜先读整键 → 改三字段 → 第 **454** 行单值 `SetValueAsync(...)`｜同文件其它三个写路径都用 `SetValuesAsync` + 抛，**只有这里写失败会被 `WineRegistry` 内部吞掉**，用户点了保存却什么也没发生｜**建议统一为 `SetValuesAsync` + 抛**（与 D134 的其余三处一致）
  - 死代码（6 个同步 wrapper）：移植 90 / 189 / 265 / 319 / 391 / 424 行分别保留 `SetGameResolutionSetting`、`SetGameVoiceLanguageSetting`、`SetStarRailFPSIndex`、`SetGenshinEnableHDR`、`GetGenshinHDRLuminance`、`SetGenshinHDRLuminance` 的 `GetAwaiter().GetResult()` 包装。**编译范围内零调用方**（唯一调用点在 `GenshinHDRLuminanceSettingDialog.xaml.cs:52/288`，该文件被 Compile Remove；`tests/` 里 `GameSettingService.` 命中 0 次）｜无用户影响，但违反「UI 线程不许同步等」的区域规范，误用即回到 1729 ms 卡顿｜**建议删除**（或 `.Linux.cs` 之外不留同步面）

- 额外：
  - 移植多出：`ResolveGamePrefix`、`TryGetReadPrefix`、`BuildResolutionSettingValues`、全套 `*Async`。全部有调用方（`GameSettingPage` / `GameLauncherService` / `ScreenCaptureService.cs:405`）。
  - 上游有而移植没有：无读取逻辑缺失；仅「同步 API 面」被异步替换。
  - 读写对称性结论：分辨率（4 键或 JSON）、语音（JSON / 单键）、星铁 FPS（预设 0 + Model JSON）、原神 HDR（单键）、HDR 亮度（3 键）**读与写使用的键、类型、值域完全对称**，唯一不对称是上面那条写失败可见性。

---

### Features/GameSetting/GameSettingPage 的「UI 线程等待」专项结论

- `OnLoaded` → `InitializeGameSettingAsync` 内核已 `Task.Run`；`ApplySettingAsync` 四个写操作全 await；`OpenGenshinHDRLumianceSettingWindow` 走 `.Linux.cs` 的 await 版本（`GenshinHDRLuminanceSettingDialog.Linux.cs:108/125`）。
- 残留风险：`GameSettingService` 的 6 个同步 wrapper（死代码）、`GameLauncherSettingDialog.xaml.cs:763`（`ComboBox_StarRailFPS.SelectedIndex = GameSettingService.GetStarRailFPSIndex(...)`，**这是编译内的同步读**，赋值时 fork wine —— 该文件不在本次清单，但它是唯一活跃的同步读路径，建议一并登记）。

---

### Features/GameInstall/GameInstallService.cs

- **编译状态**：参与编译。上游的 `GameInstallController.cs`（gRPC 控制器）**被 Compile Remove**，真正干活的是 `Starward.GameInstall.GameInstallRunner`（进程内，`src/Starward.GameInstall/`，D128）。
- **逐行结论**：差异 6 处。公开 API 面（`GetGameInstallTask`、6 个 `Start*Async`、`PauseTaskAsync`/`ContinueTaskAsync`/`StopTaskAsync`、`StartUninstallAsync`）与上游**完全对齐**；`AddOrUpdateTask` 的逐字段拷贝与上游**一字不差**。

- 差异：
  - [上游 47–95] vs [移植 50–87] ｜平台适配｜订阅 `client.GetProgress(...)` gRPC 流，RPC 掉线把所有任务置 `RunnerState.Error` + `Lang.RPCServiceExitedUnexpectedly`，再由 `finally` 重启 | `public async void` 里 `while(true)` 每 1 s `_gameInstallRunner.GetAllProgress()` 拉一遍，`_tasks.IsEmpty` 时 break，`finally` 里 `Task.Delay(1000)` 后自重启（D134 防热循环），信号量 `_getProgressSemaphore` 防并发多开｜进程内无「RPC 掉线」概念，失败语义退化是可接受的；1 s 轮询取代流式订阅，进度条刷新粒度从「即时」变成「1 s」｜**保留**
  - [上游 235–285] vs [移植 193–225] ｜平台适配｜`SetRateLimiterAsync` → `CreateContextAsync` → `StartOrContinueTaskAsync(deadline 5 s)` → `StartUpdateTaskProgress()`；每步都有「返回 null 就重试一次」的兜底 | 直接构造 `GameInstallRequest` → `runner.SetRateLimiter(limit>0, limit)`（同步）→ `await runner.CreateContextAsync(request, ct)` → `runner.StartOrContinueTask(request)`（同步）→ `StartUpdateTaskProgress()`；**5 s deadline 与 `EnsureRpcServerRunningAsync` 分支删除**｜进程内不会「服务没起来」，等价可接受｜保留
  - [上游 98–136] vs [移植 90–128] ｜平台适配｜`GameInstallProgress.FromDTO(dto)` 后逐字段拷贝（21 个字段）｜同样的 21 个字段逐字段拷贝，`task = value` 分支也一致｜一致｜—
  - [上游 无] vs [移植 231–256] ｜工程性（潜在降级）｜无（上游每步都走 `AddOrUpdateTask`）｜`AddOrUpdateProgress(GameInstallProgress)` **只合并 7 个字段**（State/ErrorMessage/Step/CurrentStep/TotalStep/PercentProgress/ProgressDisplay），少了 `NetworkTotalBytes`/`NetworkFinishedBytes`/`StorageTotalBytes`/`StorageFinishedBytes`/`HardLinkBytes`/`NetworkDownloadSpeed`/`RemainTimeSeconds`/`Timestamp`/`LauncherConfig`/`InstallPath`/`Operation`/`AudioLanguage`/`CategoryScenario`｜`Start*`/`Pause`/`Continue`/`Stop` 的**返回值**里下载速度、已下载字节、剩余时间是旧值（0 或上一 tick），页面若直接拿返回值渲染（页面确实把返回值赋给 `_gameInstallTask`），会看到一瞬间「0 B/s / 无剩余时间」｜**建议**让 `AddOrUpdateProgress` 复用 `AddOrUpdateTask` 的完整拷贝（把 `FromDTO` 换成直接拷贝 21 个字段）
  - [上游 286–338] vs [移植 259–283] ｜工程性｜`PauseTaskAsync`/`ContinueTaskAsync`/`StopTaskAsync` 都真正 `await` gRPC｜三个方法声明 `async Task` 但**体内无任何 await**（`runner.PauseTask/StartOrContinueTask/StopTask` 全是同步），等于同步执行在调用线程上｜页面在 UI 线程 `await` 这些方法 → 实际同步跑在 UI 线程；暂停/停止若涉及文件 I/O 会掉帧（规则 10）｜**建议**包一层 `Task.Run` 或改回同步签名（现在是 CS1998 告警级隐患）
  - [上游 139–160 `ToRequestDTO`] vs [移植 286–298 `ToRequest`] ｜行为错位（轻）｜`ToRequestDTO` 显式带 `Passport = task.LauncherConfig.IsBeta ? ToPassportDTO(_hoYoPlayService.PassportUserInfo) : null`｜`ToRequest(task)` 只带 6 个字段，**没有 `PassportUserInfo`，也没有 `CutsceneBlockList`**｜beta 渠道游戏在「暂停后继续」时丢掉登录票据（Runner 侧可能重新校验失败）；过场动画下载续传丢 blockList（会重下或漏下）｜**建议改回**：`PassportUserInfo = task.LauncherConfig.IsBeta ? _hoYoPlayService.PassportUserInfo : null`，并补 `CutsceneBlockList = blockList ?? new()`

- 额外：
  - 移植多出：`AddOrUpdateProgress`（有调用，4 处）、`_getProgressSemaphore`（有调用）。
  - 上游有而移植没有：RPC 失联兜底、`EnsureRpcServerRunningAsync`、`deadline`、`ToPassportDTO`（移植在 `StartOrContinueTaskAsync` 里内联了 `PassportUserInfo`，但 `ToRequest` 路径仍缺）。—— 与上游 `GameInstallRunner` 的 API 差异仅在于「失败语义」，其余签名一一对应，页面侧无需改调用。

---

### Features/GameInstall/PreDownloadButton.xaml

- **编译状态**：参与编译。
- **逐行结论**：语义一致；差异 2 处，均为字形码位替换（Uno 自带 `Symbols` 字体覆盖 `Segoe MDL2` 码位，L-88），布局/绑定/状态机零变化。

- 差异：
  - [上游 157] vs [移植 157] ｜平台适配｜`Glyph="&#xF012;"`｜`Glyph="&#xE896;"`（下载字节图标）｜图标形状略有差别（E896 是 Download 系实心箭头）｜**保留**（F012 在 Uno Symbols 里不渲染）
  - [上游 181] vs [移植 181] ｜平台适配｜`Glyph="&#xE917;"`｜`Glyph="&#xE823;"`（时钟/剩余时间）｜同上｜**保留**

- 额外：无；上游内容无缺失。

### Features/GameInstall/PreDownloadButton.xaml.cs

- **编译状态**：参与编译。
- **逐行结论**：语义一致；差异 2 处。

- 差异：
  - [上游 1] vs [移植 1] ｜工程性｜带 UTF-8 BOM｜去 BOM、`using System;` 挪到末尾｜无｜可忽略
  - [上游 66] vs [移植 67] ｜平台适配｜`return "\uE62E"`（暂停）｜`return "\uE769"`｜暂停图标形状不同（E769 是两竖线暂停）｜**保留**

- 额外：无。

### Features/GameInstall/InstallGameDialog.xaml

- **编译状态**：参与编译。
- **逐行结论**：语义一致；差异 2 处。

- 差异：
  - [上游 63] vs [移植 62] ｜工程性｜`IsTextTrimmedChanged` 与 `MaxLines="2"` 的书写顺序｜两属性互换｜无｜可忽略
  - [上游 125] vs [移植 123] ｜平台适配｜`<GridView ... ItemContainerTransitions="{x:Null}">`｜删掉该属性，加注释「Uno 的 GridView 不支持 TransitionCollection，{x:Null} 会让 XAML 编译器崩（UXAML0001），D128」｜安装目录列表少一层容器过渡动画；不删则整个对话框编译失败｜**保留**

- 额外：无。

### Features/GameLauncher/GameLauncherPage.InstallTask.cs

- **编译状态**：参与编译；`partial class GameLauncherPage`（与主文件同一个类，非覆盖关系）。
- **上游是否存在**：**不存在**（上游把进度画在 `StartGameButton` 里）。本文件是移植侧新增能力，必须单独列出，不能当成「差异 N 处」。
- 逐行结论：新文件，无上游对照。内容与问题：

- 差异/问题：
  - [上游 无] vs [移植 1–10] ｜功能多出｜无｜文件头注释说明「上游把进度画在 StartGameButton 里（通过 RPC 任务消息）；移植侧不拆进程（D128），每秒读一次画在底部状态条」｜—｜保留（能力本身合理）
  - [上游 无] vs [移植 22–43] ｜平台适配｜无｜`_installTaskTimer`（`DispatcherTimer` 1 s）+ `StartInstallTaskWatcher`/`StopInstallTaskWatcher`/`InstallTaskTimer_Tick`；`Tick -=` 先摘再挂，防缓存页重复订阅｜—｜保留
  - [上游 无] vs [移植 46–92 `UpdateInstallTask`] ｜**行为错位（确认的 bug）**｜—｜第 **84** 行 `ProgressBar_InstallTask.Value = task.PercentProgress * 100;`，而 `GameLauncherPage.xaml:81–85` 的 `ProgressBar_InstallTask` 是 `Maximum="1" Minimum="0"`｜**进度条恒为满格**（值 0–100 永远 ≥ Max 1），用户看到「条满着但百分比/文字还在变」｜**改回上游口径（值域 0–1）**：要么 XAML 改 `Maximum="100"`，要么代码改 `Value = task.PercentProgress`——二者必须一致
  - [上游 无] vs [移植 95–126 `DescribeTask`] ｜文案/工程性｜—｜硬编码中文「预下载/安装/修复/过场动画/插件/热更新/更新」+ `FormatRemain` + `FormatSize`，不经 `Lang`｜与上游 i18n 机制不一致（切英文界面这段仍是中文）｜**建议**迁到 `Lang.*`
  - [上游 无] vs [移植 67–91 的 Stop/Finish/Error 分支] ｜行为错位（轻）｜—｜Finish → toast「下载完成」；Error → toast `Lang.Lunarward_Error_InstallFailed + task.ErrorMessage`｜与上游 `UpdateGameInstallTaskProgress` 的按钮态展示**并存**，同一次完成会「toast + 按钮闪一下」双份反馈；`_lastInstallState` 与页面 `GameState` 两套状态机互相独立｜**建议**只留一套（见 GameLauncherPage.xaml.cs 差异第 10 条）

- 额外：本文件的 4 个入口全部有调用方（`OnLoaded` 79、`OnUnloaded` 163、`Tick`）。

---

## 重点怀疑对象结论汇总

| # | 怀疑项 | 结论 |
| --- | --- | --- |
| 1a | `GameLauncherService` 启动参数构造 | **一致**（`AppConfig.GetStartArgument` → DX12 → login_auth_ticket，两函数逐字符相同） |
| 1b | wine 解析 | **移植新增**（`ResolveWine`/`ResolvePrefix`，`LUNARWARD_*` → Proton → PATH → `~/.wine` → 私有前缀目录），无上游对应，行为合理 |
| 1c | 环境变量装配 | **移植新增**（`RuntimeEnvironment.Assemble` + `LaunchRecipeBuilder` 整体覆盖 `WINEPREFIX`/`WINEDEBUG`/DLL 路径）；`ApplyWineRuntimeEnvironment` 不走启动路径（仅 CLI 预览），属两套并存但无冲突 |
| 1d | 预检调用顺序 | **合理**：预检「只看不建」（`LaunchPreflight.cs:149–174` 前缀缺失判 `Ok`），建前缀在 `CreateGameProcessStartInfo` 内、锁先于建 |
| 1e | 启动后 HDR 重放 | **差异在位置与可见性**：由同步改 await、由「append 之后」挪到「append 之前」，失败只 LogWarning；注释自相矛盾（515–516 vs 517–519）→ 建议删旧注释，功能保留 |
| 1f | 游玩时长挂钩 | **一致**（改按 pid 记，B35，Linux 必需） |
| 1g | 截图快捷键 | **本组文件两侧均无相关代码**；实体在 `Helpers/HotkeyManager.Linux.cs` + `Setting/ScreenshotSetting.Linux.cs`（两个上游同名文件被 Compile Remove） |
| 2a | `GameSettingService` 注册表路径 | **写出「游戏真正使用的前缀」而不是 `~/.wine`**（`ResolveGamePrefix` → `ProfileBinding.Resolve` → `TryGetExistingPrefix`），读同路径，语义正确 |
| 2b | 分辨率/语音/FPS/HDR 读写对称性 | **对称**（键名、类型、值域、Clamp 全一致）；唯一不对称：`SetGenshinHDRLuminance` 写失败被 `WineRegistry` 吞（454 行），其余三处写会抛 |
| 2c | 异常与提示 | 分辨率/语音/FPS/HDR 写失败有 toast/`ErrorMessage`；HDR 亮度保存失败**无提示**；`InitializeGameSettingAsync` 把 `ErrorMessage` 清成 null，丢了上游常驻提示 |
| 2d | UI 线程等待 | 页面侧已全异步；残留 `GameSettingService` 6 个同步 wrapper（编译内无调用方=死代码）+ `GameLauncherSettingDialog.xaml.cs:763` 同步读 |
| 3a | `GameInstallService` facade vs 上游 `GameInstallRunner` API | **公开面完全对齐**，差异只在失败语义（RPC 掉线兜底删除，进程内等价） |
| 3b | 进度任务表 | 轮询路径字段拷贝完整；**`AddOrUpdateProgress` 只拷 7 字段** → Start/Pause/Continue/Stop 的返回值速度/字节/剩余时间为旧值 |
| 3c | 取消与收尾 | `Stop/Finish` 从 `_tasks` 移除（与上游同）；`UninstallGameAsync` 传 `cancellation` 到 Runner；`Pause/Continue/Stop` 声明 async 但同步执行（UI 线程风险） |
| 4a | `GameLauncherPage` UI 结构 | **一致 + 3 处增补**（缓存模式、背景切换器具名、底部状态条） |
| 4b | 命令绑定 | 全部保留且绑定名未变（`ApplySettingAsync` → `ApplySettingCommand` 由 toolkit 生成，XAML 绑定未坏） |
| 4c | 消息订阅 | 上游 4 条全在，新增第 5 条 `CurrentGameChangedMessage`；`GameNoticeWindowClosedMessage` 随公告窗删除（只影响 `GameBannerAndPost`） |
| 4d | 死代码 | `GameLauncherService.verb`（Linux 无用途）、`GameSettingService` 6 个同步 wrapper、`GameSettingPage` 762 行多余 `RefreshProfileSelector()`、重复 using、`InstallTask` 与上游进度双轨并存 |

## 需要优先处理的问题（按严重度）

1. **`ProgressBar_InstallTask` 值域不一致**：`GameLauncherPage.xaml:81–85` 是 `Maximum="1"`，`GameLauncherPage.InstallTask.cs:84` 写 `Percent*100` → 进度条恒满。（确认）
2. **`GameSettingPage.xaml.cs:109–110` 早于 bh3_global 区服映射**：运行配置/后启动程序按 `bh3_global` 展示，设置按 `bh3_jp/kr/os/asia` 读写。（确认）
3. **`GameInstallService.ToRequest` 丢 `PassportUserInfo` / `CutsceneBlockList`**：beta 渠道暂停后继续、过场动画续传受影响。（确认）
4. **`AddOrUpdateProgress` 只合并 7 字段**：Start/Pause/Continue/Stop 的即时返回值里速度/字节/剩余时间是旧值。（确认）
5. **`GameLauncherPage.xaml.cs:707` 把上游 `Error(ex)` 包进 `!IsWindows()`**：非 Linux 平台彻底静默。（确认）
6. **`GameBannerAndPost` 一次点击可能开两次**（容器 Tapped + 图片 handledEventsToo 都不设 `e.Handled`）。（推断，需真机验证）
7. **`StartGameAsync` 日志含 `login_auth_ticket` 明文**（`:546`）。（确认）
8. **`SetGenshinHDRLuminance` 写失败被吞**（`:454`）。（确认）
9. **`Pause/Continue/Stop` 伪 async 在 UI 线程同步执行**。（确认，危害取决于 Runner 内部 IO 量）
10. **`GameLauncherService.cs:515–516` 作废注释 + `verb` 死变量 + 6 个同步 wrapper 死代码**。（确认）

---

## 覆盖清单

| # | 文件 | 上游 | 移植 | 编译状态 | 审计结论 |
| --- | --- | --- | --- | --- | --- |
| 1 | Features/GameLauncher/GameLauncherPage.xaml | 223 行 | 243 行 | 参与编译（无 Remove / 无 `.Linux.cs` 覆盖） | 差异 3 处 |
| 2 | Features/GameLauncher/GameLauncherPage.xaml.cs | 990 行 | 1369 行 | 参与编译 | 差异 10 处 |
| 3 | Features/GameLauncher/GameLauncherService.cs | 669 行 | 793 行 | 参与编译，与 `GameLauncherService.Linux.cs`（578 行）同一 partial | 差异 10 处 |
| 4 | Features/GameLauncher/GameBannerAndPost.xaml | 149 行 | 158 行 | 参与编译 | 差异 7 处 |
| 5 | Features/GameLauncher/GameBannerAndPost.xaml.cs | 331 行 | 440 行 | 参与编译 | 差异 12 处 |
| 6 | Features/GameLauncher/GameNoticeService.cs | 103 行 | 106 行 | 参与编译 | 差异 2 处 |
| 7 | Features/GameSetting/GameSettingPage.xaml | 199 行 | 282 行 | 参与编译 | 差异 9 处 |
| 8 | Features/GameSetting/GameSettingPage.xaml.cs | 480 行 | 943 行（审计期间被并发改动，原 935） | 参与编译 | 差异 11 处 |
| 9 | Features/GameSetting/GameSettingService.cs | 321 行 | 456 行 | 参与编译 | 差异 6 类 |
| 10 | Features/GameInstall/GameInstallService.cs | 388 行 | 342 行 | 参与编译；上游 `GameInstallController.cs` 被 Compile Remove | 差异 6 处 |
| 11 | Features/GameInstall/PreDownloadButton.xaml | 204 行 | 204 行 | 参与编译 | 语义一致（2 处字形） |
| 12 | Features/GameInstall/PreDownloadButton.xaml.cs | 205 行 | 206 行 | 参与编译 | 语义一致（2 处） |
| 13 | Features/GameInstall/InstallGameDialog.xaml | 282 行 | 282 行 | 参与编译 | 语义一致（2 处） |
| 14 | Features/GameLauncher/GameLauncherPage.InstallTask.cs | **不存在** | 156 行 | 参与编译（partial 追加，非覆盖） | 新增文件，4 个问题 |

被覆盖/被排除相关（不在本 13 个文件内，但影响本组结论）：

| 文件 | 状态 | 真正编译的是 |
| --- | --- | --- |
| Features/GameInstall/GameInstallController.cs | 上游有，移植侧 `Compile Remove`（csproj:34） | 由 `GameInstallService.cs` + `Starward.GameInstall.GameInstallRunner` 取代（D128） |
| Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.cs | 上游有，移植侧 `Compile Remove`（csproj:110） | `GenshinHDRLuminanceSettingDialog.Linux.cs` |
| Features/GameLauncher/GameNoticeWindow.xaml(.cs) | **上游有，移植侧文件不存在** | 无（改为浏览器打开，`GameBannerAndPost.OpenGameNoticeWindowAsync`） |
| Features/Overlay/RunningGameService.cs | 移植侧 `Compile Remove` | `Features/Overlay/RunningGameService.Linux.cs`（`GameLauncherPage:619/1356`、`CloudGameButton:135` 调用的就是它） |
| Features/Setting/HotkeyManager.cs、Setting/ScreenshotSetting.xaml.cs | 移植侧 `Compile Remove` | `Helpers/HotkeyManager.Linux.cs`、`Setting/ScreenshotSetting.Linux.cs` |
| Features/WindowBase.cs | 移植侧 `Compile Remove` | `Features/WindowBase.Linux.cs` |
| Helpers/DriveHelper.cs、FileDialogHelper.cs、ClipboardHelper.cs | 移植侧 `Compile Remove` | 各自 `*.Linux.cs` |

> 说明：第 6 项清单里的 `GameLauncherPage.InstallTask.cs`「若两侧都有」——**上游没有**，已按「新增文件」标注。清单中「若下面还有就一并做」的 4 个额外文件未在消息里给出文件名，本报告已覆盖 GameLauncher/GameSetting/GameInstall 三个目录下**全部**同名文件（含 InstallTask.cs），无遗漏；目录内其余文件（`LaunchPreflight.cs`/`LaunchReport.cs`/`GameState.cs`/`GameAuthLogin*.cs`/`GameInstallScanner.Linux.cs`/`GamePackageService.cs`/`CutsceneResourceWindow.*`/`PreDownloadDialog.*`）为移植侧独有或属其他分组，不在本次副本比对口径内。
