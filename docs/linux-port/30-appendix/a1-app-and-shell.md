# s1a 审计（App 底座与外壳）

审计对象：`src/Starward/`（上游，HEAD=2b1e7b46 已合入 0.19.0）↔ `src/Lunarward/`（Linux 移植），16 个同名文件，相对路径一一对应。
方法：两侧逐行读全文（大文件分段读全），差异行号全部用 `grep -n` / `sed -n` 复核。**只读审计，未修改仓库任何文件。**

> **基线提醒（重要）**：审计期间工作区**不是** HEAD 的干净状态，有人在并发改动（本审计只读，未参与）：
> `git status --porcelain` 显示 `M src/Lunarward/App.xaml.cs`、`M src/Lunarward/AppConfig.Configuration.cs`、`M src/Lunarward/Features/GameSetting/GameSettingPage.xaml.cs`，以及新增未跟踪的 `src/Lunarward/Helpers/SessionLockWatcher.Linux.cs`。
> 三处改动都是 D135 相关：`AppConfig.Configuration.cs:136` 补回 `GameInstallEnv.CacheFolder = CacheFolder;`、`App.xaml.cs:401` 补 `Helpers.SessionLockWatcher.Start(...)`、`GameSettingPage.xaml.cs`（不在本清单内）。
> 因此 **下文行号与结论对应「审计时刻的工作区」**；HEAD 里没有这两处 D135 补丁。凡涉及这两处的条目，我已注明「工作区已修 / HEAD 未修」。

## 编译状态前提（16 个文件共同背景）

- `src/Lunarward/Lunarward.csproj` **没有任何 `<Page Remove>`**；`EnableDefaultXamlItems=false`、`EnableDefaultPageItems=false`、`EnableDefaultUnoItems=true`；`<ApplicationDefinition Include="App.xaml" />` 在第 43 行。
- Compile Remove 清单在 csproj 33-36 行与 98-141 行：与本清单**唯一相关**的是 `Features/Update/UpdateWindow.xaml.cs`（本清单不含该 .cs，但它的 XAML 在清单里）。
- 除 UpdateWindow.xaml 外，其余 15 个文件均**参与编译**，且**没有同名 `*.Linux.cs` 覆盖**。`Program.cs` 旁边有 `Program.Linux.cs`，但那是并列的第二入口，不是 partial 覆盖。

## 汇总

| 文件 | 上游/移植行数 | 差异数 |
| --- | --- | --- |
| App.xaml | 102 / 108 | 4 |
| App.xaml.cs | 189 / 475 | 14 |
| AppConfig.Common.cs | 64 / 96 | 3 |
| AppConfig.Configuration.cs | 304 / 369 | 16（+2 处文档缺陷） |
| AppConfig.ServiceProvider.cs | 223 / 230 | 6 |
| Program.cs | 139 / 139 | 1（整份死代码） |
| Features/PageBase.cs | 80 / 105 | 4 |
| Features/Update/UpdateWindow.xaml | 299 / 300 | 1 |
| Features/ViewHost/MainWindow.xaml.cs | 259 / 382 | 16 |
| Features/ViewHost/MainView.xaml | 384 / 400 | 1 |
| Features/ViewHost/MainView.xaml.cs | 615 / 858 | 13 |
| Features/ViewHost/SystemTrayWindow.xaml | 53 / 53 | 1 |
| Features/ViewHost/SystemTrayWindow.xaml.cs | 134 / 140 | 6（整份运行时死代码） |
| Features/ViewHost/WelcomeWindow.xaml.cs | 274 / 277 | 2（整份运行时死代码） |
| Features/Setting/SettingPage.xaml | 110 / 116 | 4 |
| Features/Setting/SettingPage.xaml.cs | 62 / 129 | 4 |

高优先级结论（可行动）：

1. **`App.xaml.cs` 的托盘 / 热键 / 可移动存储消息链在 Linux 上没有生产者**（上游 `WindowSubclassProc` 172-249 整块缺失 → `RemovableStorageDeviceChangedMessage`、`WM_HOTKEY 44444/44445` 永不触发）。锁屏消息例外：工作区新增未跟踪的 `src/Lunarward/Helpers/SessionLockWatcher.Linux.cs`（147 行，按 logind `Session.Lock/Unlock` → `MainWindowStateChangedMessage{SessionLock}`，见其 137-142 行）已接上，但**该文件未提交，HEAD 里没有**。
2. **截图服务未注册**（ServiceProvider 144-145 注释掉 + MainWindow 热键分支删除）→ 截图功能在 Linux 上完全没有实现。
3. **初始化异常彻底静默**（`AppConfig.Configuration.cs:190` 只 `Debug.WriteLine` + `Exit(0)`）→ Release 下双击无反应、无任何日志外提示。
4. **`App.xaml` 把 `MenuFlyoutSeparatorBackground` 刷成面板同色**（LW:68）→ 右键菜单分隔线不可见，是可直接看到的外观缺陷。
5. **单实例机制替换掉了 `starward-hyp-login://` 深链**（上游 130-149 整块无替代）→ Hyp 第三方登录回跳在 Linux 无入口。
6. 两个文件整份运行时死代码：`SystemTrayWindow.*`（Linux 从不实例化）、`WelcomeWindow.xaml.cs`（`UserDataFolder` 恒非 null）。

---

### App.xaml

- 编译状态：参与编译（csproj:43 `ApplicationDefinition`；无 Compile Remove，无 `*.Linux.cs` 覆盖）。纯资源字典。
- 逐行结论：差异 4 处（另登记 1 处「同源保留带来的视觉不一致」）
- 差异：
  1. 上游 58 vs 移植 58 ｜平台适配｜`ContentDialog` 样式 Background = `{ThemeResource CustomAcrylicBrush}`｜`{ThemeResource LunarwardSurfaceBrush}`｜可见：对话框从（假）亚克力变成不透明实色｜建议：保留（Uno 的 `AcrylicBrush` 只渲染 FallbackColor）
  2. 上游 63 vs 移植 63 ｜平台适配｜`FlyoutPresenter` Background = `CustomAcrylicBrush`｜`LunarwardSurfaceBrush`｜可见：浮出层实色｜建议：保留
  3. 上游 54 之后无 MenuFlyout 键 vs 移植 66-68 ｜**行为错位**｜—｜新增 `<StaticResource x:Key="MenuFlyoutPresenterBackground" ResourceKey="LunarwardSurfaceBrush"/>` 与 `<StaticResource x:Key="MenuFlyoutSeparatorBackground" ResourceKey="LunarwardSurfaceBrush"/>`｜**可见缺陷：分隔线与面板同色 → MenuFlyout 里的分组分隔线完全看不见**｜建议：删掉 `MenuFlyoutSeparatorBackground` 这一条（回落系统 ThemeResource），`MenuFlyoutPresenterBackground` 保留
  4. 上游无 vs 移植 88（Light `#FFF3F3F3`）/ 101（Dark `#FF202020`）｜平台适配｜—｜新增 `LunarwardSurfaceBrush` SolidColorBrush｜不可见（自身）｜建议：保留（1/2/3 依赖它）
- 额外：
  - 上游 44-51 的 `CustomAcrylicBrush` / `CustomOverlayAcrylicBrush` 在移植侧 44-51 原样保留，且 `NavigationViewDefaultPaneBackground`（移植 54）仍指向 `CustomAcrylicBrush` → **左侧导航栏仍是（假）亚克力、会随壁纸偏色，而设置页是实底（D61）**，同一窗口两种底色。属于已知软差异，此处只登记不判缺陷。
  - 移植侧无多出的可调用能力（纯资源）。

### App.xaml.cs

- 编译状态：参与编译（无 Compile Remove，无 `*.Linux.cs` 覆盖）。
- 逐行结论：差异 14 处
- 差异：
  1. 上游 26 vs 移植 32 ｜行为错位（性能）｜`_gcTimer = new(TimeSpan.FromSeconds(60))`｜`FromMinutes(10)`｜不可见；强制 GC 由每分钟一次降为十分钟一次（视频壁纸/大图缓存下更稳）｜建议：保留
  2. 上游 47 vs 移植 54 ｜工程性｜崩溃日志目录 `LocalApplicationData/Starward/log`｜`LocalApplicationData/{DataFolder.AppFolderName}/log`（文件名 `Starward_{date}.log` 未变）｜不可见｜建议：保留
  3. 上游 72-81 vs 移植 78-94 ｜平台适配 + 调试入口｜`if (await new WelcomeWindow().WaitAsync()) LoadConfiguration(); else Exit(0);`｜同结构，但先在 82-85 判 `LUNARWARD_SELFTEST is "1"` 走 `LoadConfiguration()`（L-185）｜不可见（需环境变量）｜建议：保留
  4. 上游 84-91 + 130-149 vs 移植 126-201 ｜**行为错位（机制整体替换）**｜`AppInstance.FindOrRegisterForKey("Starward.Main")` + `RedirectActivationToAsync` + `Activated += AppInstance_Activated`；`AppInstance_Activated` 里解析 `starward-hyp-login://` → `HypThirdPartyLoginMessage`｜文件锁 `~/.local/state/lunarward/lunarward.lock`（`FileShare.None`，含陈旧锁接管 133-145）+ Unix socket `SingleInstancePipe.StartListening(onShow/onExit/onStartGame)`（171-201，`OnUi` 147-170 做线程切换，onStartGame 走 `GameLauncherService.StartGameAsync` 失败 toast）｜可见：重复启动能把已有窗口叫出来（等价上游）；**可见缺失：`starward-hyp-login://` 深链无任何替代**｜建议：保留机制，单独登记「Hyp 登录回跳 Linux 不支持」
  5. 上游 93-96 vs 移植 202-218 ｜平台适配｜`--hide` 只 `new SystemTrayWindow()`｜非 Windows 下仍 `new MainWindow` + `HideAfterShownAsync`（262-280，托盘不可用则保持可见）｜可见：`--hide` 会闪一下主窗口｜建议：保留
  6. 上游 123-126 vs 移植 392-406 ｜平台适配｜`m_SystemTrayWindow ??= new SystemTrayWindow();`｜非 Windows 直接 `_ = StartTrayAsync();` + `Helpers.SessionLockWatcher.Start(...)`（**工作区未提交新增，HEAD 无此行**）+ `ProcessExit += StatusNotifierTray.Stop`（D135）｜不可见｜建议：保留
  7. 上游 153-163 vs 移植 446 ｜功能缺失（对外契约降级）｜遍历 `AppInstance.GetInstances()` 返回 `AppInstance?`｜恒 `null`（签名也降为 `object?`）｜**当前无调用者**（上游 `PlayTimeRecordService.cs:206/256` 在移植版已改写；RPC 两处调用方被 Compile Remove）→ 不影响运行｜建议：保留，但注明「返回恒 null」是契约，接 RPC 时必须重做
  8. 上游 179 vs 移植 463｜平台适配｜`AppInstance.GetCurrent().UnregisterKey();`｜删除（注释说明 Uno 无实现，D128）｜不可见｜建议：保留
  9. 上游无 vs 移植 97｜新增能力｜—｜`CompanionAutoStart.Register(GetService<CompanionRunner>(), GetLogger<App>())`｜被调用一次（启动路径）｜建议：保留
  10. 上游无 vs 移植 103-121｜新增能力｜—｜非 Windows 时 `await Task.Run(() => GameInstallScanner.ScanAndApply())`（108）+ KnownGames 计数 + `DevSwitches.Game` 覆盖 `AppConfig.CurrentGameId`（115-120）｜可见：自动发现已安装游戏｜建议：保留
  11. 上游无 vs 移植 225-229 / 234-258｜调试入口｜—｜`#if DEBUG` + `LUNARWARD_DEV_ACCENT` 切强调色｜不可见（Release 无）｜建议：保留
  12. 上游无 vs 移植 287-348｜新增能力｜—｜`internal static string[] ExpandUrlArgument(string[] args)`：`lunarward:// startgame|playtime|test` 参数展开｜被 `Features/App/SingleInstancePipe.cs:37` 与 `Program.Linux.cs:275（--url-plan）` 调用｜建议：保留（这是上游已死 Program.cs 里 `starward://` 分支的替身）
  13. 上游无 vs 移植 369-389｜新增能力｜—｜`StartTrayAsync()`（StatusNotifierTray 初始化，含异常兜底）｜被移植 399 调用｜建议：保留
  14. 上游无 vs 移植 98-101｜工程性（注释错位）｜—｜三条注释，第一条讲 AcrylicBrush 替换，紧邻代码却是单实例锁｜不可见｜建议：删掉第一条错位注释
- 额外：
  - `string[] args`：上游 69 与移植 76 都声明且都未使用 → **不是差异**，两侧同为死变量。
  - 上游 `AppInstance_Activated` 的 UI 线程投递（`_uiDispatcherQueue.TryEnqueue`）在移植侧由 `OnUi`（147-170）等价实现，这部分是对齐的。
  - 上游有而移植没有：`HypThirdPartyLoginMessage` 深链转发、`AppInstance` 重定向（被文件锁替代）。

### AppConfig.Common.cs

- 编译状态：参与编译（无覆盖）。
- 逐行结论：差异 3 处
- 差异：
  1. 上游 26-28 vs 移植 24-29｜平台适配｜`Registry.GetValue(HKLM\HARDWARE\DESCRIPTION\System, "SystemBiosVersion")` + `MachineGuid` + MachineName → MD5 → `DeviceId`｜`ReadFirstLine("/sys/class/dmi/id/bios_version")` + `ReadMachineId()`（`/etc/machine-id`，回落 `/var/lib/dbus/machine-id`）+ MachineName → MD5｜不可见（同一账号在 Windows/Linux 得到不同 DeviceId，只影响设备指纹/风控上报）｜建议：保留
  2. 上游无 vs 移植 66-96｜新增能力（内部辅助）｜—｜第二个 `public static partial class AppConfig`：`ReadFirstLine`(71-83)、`ReadMachineId`(86-94)｜被移植 27 调用｜建议：保留
  3. 上游 1 `using Microsoft.Win32;` vs 移植 3 `using System.Linq;`｜工程性｜—｜移植侧不再需要 Microsoft.Win32，但多了一个**未使用的 `System.Linq`**（文件名同目录下无 Linq 调用）｜不可见｜建议：删掉该 using
- 额外：`StartGCTimer`（上游 36-44 / 移植 35-43）逐字一致（60 秒）；上游文件带 UTF-8 BOM、移植无 BOM，无影响，不记差异。

### AppConfig.Configuration.cs

- 编译状态：参与编译（无覆盖）。
- 逐行结论：差异 16 处（另有 2 处文档缺陷）
- 差异：
  1. 上游 45 vs 移植 84｜平台适配｜`public static void CheckEnviroment()`｜`public static async Task CheckEnviromentAsync()`｜不可见（唯一调用 `src/Lunarward/Program.Linux.cs:89 static void InitEnviroment() => AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult();`）｜建议：保留
  2. 上游 47 vs 移植 88-95｜健壮性新增｜`AppVersion = ...InformationalVersion ?? ""`｜空值回落 `Assembly.GetName().Version`（注释引 L-26）｜不可见（避免 User-Agent 非法）｜建议：保留
  3. 上游 50 vs 移植 96｜功能缺失（平台限制）｜`IsAppInRemovableStorage = DriveHelper.IsDeviceRemovableOrOnUSB(AppContext.BaseDirectory)`｜恒 `false`｜**可见（边界）**：上游 67-72 / 移植 113-122 的 U 盘分支在 Linux 永不成立 → 把程序放在移动盘上跑，缓存仍写 `~/.local/share/lunarward`｜建议：保留（M2 用 /proc/mounts 细化），但把移植 113-122 标为死分支
  4. 上游 62 vs 移植 108｜平台适配｜`ShowNoPermissionMessageBox(parentFolder)`｜`await new NoPermissionWindow(parentFolder).WaitAsync()`｜可见：权限不足提示改为自绘窗口｜建议：保留
  5. 上游 119 vs 移植 174｜同上（`userDataFolder`）｜建议：保留
  6. 上游 80 / 84 vs 移植 125 / 130｜平台适配｜`CacheFolder = LocalApplicationData/Starward`｜`LinuxDataHome/lunarward`（`XDG_DATA_HOME` 或 `~/.local/share`）｜不可见（设计如此）｜建议：保留
  7. 上游无 vs 移植 31-38 / 51-72 / 133-143｜新增能力｜—｜`CacheDirectory`（`XDG_CACHE_HOME/lunarward`，D57）+ `LinuxCacheHome` / `LinuxDataHome` + 启动时 `Directory.CreateDirectory(CacheDirectory)`｜不可见｜建议：保留。**但移植 49-50 两条 `<summary>` 写反了**（`LinuxCacheHome` 上方写「XDG 数据根目录（~/.local/share）」，`LinuxDataHome` 上方写「XDG 缓存根目录（~/.cache）」）→ 建议对调
  8. 上游 86 vs 移植 136｜**无差异（注释自相矛盾）**｜`GameInstallEnv.CacheFolder = CacheFolder;`｜同（**工作区刚补上，HEAD 里没有这一行**：`git diff` 显示 136 行是未提交新增）｜—｜建议：**改掉移植 134-135 的注释**——它用「移植侧漏了这一行」的现在时描述一个已经修好的缺口，而正确代码就在 136 行。后续审计（含本轮）会据此误报假缺陷；建议改成「D135：已补回上游 :86 的交接」这类过去时表述
  9. 上游 88-102 vs 移植 145-148｜平台适配｜注册表 `Software\Starward[.Debug]` 读 `UserDataFolder`｜`LinuxConfig.Get("UserDataFolder")`｜不可见｜建议：保留
  10. 上游 107-123（只有 `Directory.Exists` 分支）vs 移植 177-186｜行为错位（有意的设计取舍）｜首次运行由 WelcomeWindow 选目录｜直接 `UserDataFolder = ~/.local/share/lunarward` + `Directory.CreateDirectory` + `SetDatabase` ×2 + `LoadConfiguration()`（D22）｜可见：首次启动不进欢迎向导；**副作用：WelcomeWindow 整份成为死代码**｜建议：保留（有意偏离，需在 18/25 登记）
  11. 上游 126-127 vs 移植 190-191｜功能缺失（可诊断性）｜`User32.MessageBox(..., "Starward", MB_OK)` + `ClipboardHelper.SetText(ex.ToString())`｜`System.Diagnostics.Debug.WriteLine($"{Lang.AppConfig_AnUnknownIssueOccurredDuringInitialization}\n{ex.Message}")` + `Environment.Exit(0)`｜**可见：Release 下初始化异常＝双击毫无反应、无窗口、无日志**｜建议：至少写 `AppConfig.LogFile`／stderr（Debug.WriteLine 在 Release 直接丢弃）
  12. 上游 149-152 vs 移植（删除）｜死代码清理｜`ShowNoPermissionMessageBox(string folder)`｜方法删除（`Lang.NoPermissionWindow_*` 仍被 NoPermissionWindow 使用）｜不可见｜建议：保留删除
  13. 上游 155-174 vs 移植 213-237｜行为错位（必要）｜`SetLanguage` 只设 `CultureInfo.CurrentUICulture` / `DefaultThreadCurrentUICulture`｜抽出 `culture` 变量并额外设 `Starward.Language.Lang.Culture`、`Starward.Core.Localization.CoreLang.Culture`，另加一条 Serilog 日志｜**可见（正向）**：config.json 里改语言能生效（上游写法在 Uno 下不生效，注释已记录实测）｜建议：保留
  14. 上游 186-188 vs 移植 251｜平台适配｜`WindowsIdentity.GetCurrent()` + `WindowsPrincipal.IsInRole(Administrator)`｜`Environment.IsPrivilegedProcess`｜不可见｜建议：保留
  15. 上游 192 / 203-215 / 239-273 vs 移植 255 / 266-289 / 304-322｜平台适配｜注册表读写（`LoadConfigurationFromRegistry` / `SaveConfigurationToRegistry`；含 UserDataFolder 相对化、`DeleteValue` 清理语义）｜`LinuxConfig` 读写（`LoadConfigurationFromStore` / `SaveConfigurationToStore` + `LinuxConfig.Save()`）｜不可见（配置位置变化）；**边界差异**：上游写 `UserDataFolder` 时会相对化到 ConfigPath 父目录、清空时 `DeleteValue`；移植直接写绝对路径、清空时写空值（不 Remove）→ 配置里会残留空键｜建议：保留机制，给 LinuxConfig 补 Remove 语义
  16. 上游无 vs 移植 75-79 / 272-273 / 284-288 / 313-320 / 346-356｜新增能力｜—｜`EnableLoginAuthTicket` / `stoken` / `mid` 三个跨启动持久化的登录态（LinuxConfig + config.ini，**config.ini 里是明文**）｜被 `Features/GameLauncher/GameAuthLoginButton.xaml.cs:42-45,66`、`GameAuthLoginService.cs:50-108` 使用｜建议：保留；`config.ini` 明文写入 token 这一点建议在文档里显式登记（便携模式下配置文件可能随目录共享）
- 额外：
  - 移植 263/281/304/325 四个方法从 `private` 改 `public`（LoadConfigurationFromConfigFile / LoadConfigurationFromStore / SaveConfigurationToStore / SaveConfigurationToConfigFile）→ 供 CLI/自检调用，属工程性放宽。
  - 上游有而移植没有：Windows MessageBox + 剪贴板异常提示、注册表存储、U 盘识别、欢迎向导驱动的首次目录选择。
  - 移植多出且**被调用**：`CacheDirectory`（被周边缓存代码使用）、`LinuxConfig` 存储、登录态三项。

### AppConfig.ServiceProvider.cs

- 编译状态：参与编译（无覆盖）。
- 逐行结论：差异 6 处
- 差异：
  1. 上游 93 vs 移植（删除）｜工程性（去重）｜`sc.AddSingleton<HoYoPlayService>()`｜删除，改由 `AddStarwardGameInstall()` 提供｜不可见｜建议：保留
  2. 上游 139-142 vs 移植 140-142｜**平台适配（生命周期语义变了）**｜`GameResourceService` / `GameConfigService` / `GenshinDispatchClient` / `GenshinHotUpdateService` 四个 Singleton｜`sc.AddStarwardGameInstall();` → `src/Starward.GameInstall/GameInstallServiceCollectionExtensions.cs:11-18` 把这 5 个（含 HoYoPlayService）注册为 **Scoped**，另 HttpService/DownloadService/GameInstallRunner 为 Singleton｜不可见（当前）；**风险**：`GameInstallService`（移植 143，Singleton，构造注入 scoped HoYoPlayService）从根容器解析＝根作用域捕获；移植 72 `sc.BuildServiceProvider()` **没开 `ValidateScopes`** 所以不抛异常，一旦开启或改用 `CreateScope` 就会炸｜建议：把这几个注册改成 Singleton，或开作用域校验后按 scope 取 `GameInstallService`
  3. 上游 146 vs 移植 144-145｜**功能缺失（未实现）**｜`sc.AddSingleton<ScreenCaptureService>()`｜注释掉（注释：依赖 Windows.Graphics.Capture + Win2D）｜**可见：截图功能在 Linux 完全没有**（与 MainWindow 热键 44445 分支同时删除相印证）｜建议：保留（M2 重写），但必须在 12-shims / 25 登记「截图不可用」
  4. 上游无 vs 移植 149｜新增能力｜—｜`AddSingleton<Starward.Features.Companion.CompanionRunner>()`｜被 `App.xaml.cs:97` 解析｜建议：保留
  5. 上游无 vs 移植 151 / 153｜新增能力｜—｜`AddSingleton<Features.GameRecord.GameRecordService>()`（移植独有类型，`src/Lunarward/Features/GameRecord/GameRecordService.cs:40`）、`AddHttpClient<Features.GameLauncher.GameAuthLoginService>()`｜均被页面/服务使用｜建议：保留
  6. 上游 63 vs 移植 65｜文案｜日志横幅 `"Welcome to Starward v..."`｜`"Welcome to Lunarward v..."`｜仅日志｜建议：保留
- 额外：上游 `AddStarwardGameInstall` 只在 `src/Starward/Features/RPC/RpcRunner.cs:117` 被调用（该文件在移植侧被 Compile Remove），所以移植侧这次替换是唯一入口，不构成重复注册。服务类型唯一、无同名歧义（均在 `src/Starward.GameInstall/`）。

### Program.cs

- 编译状态：**参与编译但整份是死代码**——全文被 `#if DISABLE_XAML_GENERATED_MAIN`（移植 18）…`#endif`（移植 137）包裹，该符号在 `Lunarward.csproj` 与 `Program.Linux.cs` 里都没有定义；Linux 的实际入口是 `Program.Linux.cs`（该文件第 9 行注释说明本文件被弃用）。
- 逐行结论：差异 1 处
- 差异：
  1. 上游 114 vs 移植 117｜工程性｜`"Starward"`（AppInstance 注册键/日志名）｜`Starward.Helpers.DataFolder.AppFolderName`｜不可见（死代码）｜建议：保留（若将来恢复该入口，这处替换方向是对的）
- 额外：上游这份文件承载的 `rpc` / `playtime` / `startgame` / `starward://` 深链 / `AppInstance` 重定向分支在 Linux 上全部失效；对应能力由 `Program.Linux.cs`（CLI）与 `App.ExpandUrlArgument`、`SingleInstancePipe` 提供。**登记为「同文件不同实现」，不算移植缺陷。**

### Features/PageBase.cs

- 编译状态：参与编译（无覆盖）。
- 逐行结论：差异 4 处
- 差异：
  1. 上游 31-35 vs 移植 31-35｜行为错位（必要）｜`PageEx_Loaded` 只调 `OnLoaded()`｜先 `ContentGameId = CurrentGameId?.Id;`（移植 33）再 `OnLoaded()`｜不可见｜建议：保留
  2. 上游无 vs 移植 38-40｜新增能力（契约字段）｜—｜`protected string? ContentGameId;`（注释 D94）｜建议：保留
  3. 上游 39-40 vs 移植 43-50｜行为错位（必要）｜`Unloaded` 里先 `Loaded -= PageEx_Loaded; Unloaded -= PageEx_Unloaded;` 再 `OnUnloaded()`｜**只调 `OnUnloaded()`，不退订**（注释说明：缓存页退订后再进入收不到 Loaded）｜可见：页面缓存后重复进入仍能收到 Loaded/Unloaded；代价是退订责任下放到各页面｜建议：保留（与 SettingPage 的 OnLoaded 注册 / OnUnloaded UnregisterAll 成对）
  4. 上游 45-57 vs 移植 53-72 + 75-82｜新增能力（契约）｜`OnNavigatedTo` 只解析参数｜末尾比较 `ContentGameId` 与新 `gameId`，不等则 `OnGameChanged()`（条件含 `ContentGameId is not null`，首次导航不触发）；新增 `protected virtual void OnGameChanged()`｜可见：切游戏时页面自动重载（等价上游「每次新建页面」）｜建议：保留
- 额外：
  - 4 个实现者：`GachaLogPage.xaml.cs:165`、`GameLauncherPage.xaml.cs:95`、`GameRecordPage.xaml.cs:77`、`GameSettingPage.xaml.cs:144`。
  - 上游与移植的 `OnNavigatedTo` **都没有调 base** → 不是差异，不作为缺陷。
  - 契约风险：新页面若忘记实现 `OnGameChanged`，表现为「切游戏后内容不换」（缓存页不会重建）。建议在文档里写死这条契约。

### Features/Update/UpdateWindow.xaml

- 编译状态：**XAML 参与编译（无 Page Remove）**；代码后置 `Features/Update/UpdateWindow.xaml.cs` 在 csproj 内被 `<Compile Remove>`，由 `Features/Update/UpdateWindow.Linux.cs`（`partial UpdateWindow : WindowBase`）+ `UpdateWindow.LinuxAuto.cs`（空壳事件）提供 → **本文件的差异只影响一致性，不影响行为**。
- 逐行结论：差异 1 处
- 差异：
  1. 上游 147 vs 移植 147｜死代码｜该 TextBlock 无 `x:Name`｜新增 `x:Name="TextBlock_ErrorMessage"`｜不可见（全仓库 .cs/.xaml 再无第二处引用；`UpdateWindow.Linux.cs:115-121` 的 SetErrorMessage 已明确改走 `Bindings.Update()`）｜建议：删掉（无害，但会诱导人以为错误文本是 FindName 改的）
- 额外：确认 XAML 上引用的处理器与命令都能解析到 `.Linux.cs`：`RootGrid_Loaded`（UpdateWindow.Linux.cs:102）、`HyperlinkButton_Click`（:78）、命令 `UpdateNow`(:57) / `RemindMeLatter`(:66) / `IgnoreThisVersion`(:69) / `Retry`(:124) / `Restart`(:127) 由 `[RelayCommand]` 生成；`UpdateWindow.LinuxAuto.cs:14-15` 只补 `Button_UpdateRemindLatter_PointerEntered/Exited` 空实现。其余 XAML（WebView2 `webview:` 288、`SplitButton`、ProgressBar 143）与上游逐字一致。

### Features/ViewHost/MainWindow.xaml.cs

- 编译状态：参与编译（无覆盖）。
- 逐行结论：差异 16 处
- 差异：
  1. 上游 42 vs 移植 120｜文案｜`Title = "Starward"`｜`"Lunarward"`｜可见（窗口标题/任务栏）｜建议：保留
  2. 上游 43 vs 移植 121-125｜平台适配｜`AppWindow.TitleBar.ExtendsContentIntoTitleBar = true`｜`false`（注释：Uno X11 无 `SetDragRectangles` 实现 → 拖不动 + 自绘按钮点不动）｜可见：改用系统标题栏｜建议：保留
  3. 上游 50 vs 移植（删除）｜平台适配（同上原因）｜`SetDragRectangles(new RectInt32(0, 0, 100000, (int)(48 * UIScale)))`｜删除｜不可见（既然不接管标题栏）｜建议：保留
  4. 上游 52 + 256 vs 移植（删除，注释 357）｜**功能缺失（平台缺失，已有替代）**｜`WTSRegisterSessionNotification(WindowHandle, 0)` + `[LibraryImport("wtsapi32.dll")]`｜删除，注释说明 WTS 由 compositor 处理；**实际替代品是工作区新增的 `Helpers/SessionLockWatcher.Linux.cs`（未跟踪，D135）**，它按 logind 的 `Session.Lock/Unlock` 发同语义的 `MainWindowStateChangedMessage{SessionLock}`（该文件 137-142 行）｜可见缺失已消除（工作区状态）；HEAD 里仍无替代 → 锁屏消息无人发｜建议：保留缺失，把 SessionLockWatcher 提交并登记进 12-shims「win32 WTS → logind」
  5. 上游 77 vs 移植 228｜平台适配｜`User32.SetCursorPos(...)`（ShowByGamepad 把光标移到窗口中心）｜删除（注释：光标交给合成器）｜仅边界（手柄唤出时鼠标不跟随）｜建议：保留
  6. 上游 102 vs 移植 256-261｜行为错位（必要）｜`args.Cancel = true` 无条件放最前｜先判 `option is Hide && !Features.Tray.StatusNotifierTray.Available` → 直接 `return`（不 Cancel＝真关闭退出），再 `args.Cancel = true`（261）｜可见：无托盘时点 X 会真正退出，不会留一个看不见的进程｜建议：保留
  7. 上游 164 vs 移植 324-328｜行为错位（性能）｜`Hide()` 里 `GC.Collect()`｜删除｜不可见（隐藏不再强制 GC）｜建议：保留
  8. 上游 172-249 vs 移植 339-353｜**功能缺失（整块替换）**｜`WindowSubclassProc`：WM_ACTIVATE / WM_POINTERACTIVATE → 发 `Activate` 消息；WM_SYSCOMMAND 0xF030 → 拦双击最大化；WM_WTSSESSION_CHANGE → 锁屏消息；WM_DEVICECHANGE → `RemovableStorageDeviceChangedMessage`；WM_HOTKEY 44444 → `RunningGameService.OpenOverlayWindow()`（失败则 `Show()`）、44445 → `ScreenCaptureService.Capture()`｜整块删除，改用 `Activated` 事件 → `OnWindowActivated`（341 用 `e.WindowActivationState.ToString()=="Deactivated"` 判反激活，D134；346-352 发 `MainWindowStateChangedMessage{Activate, CurrentTime, LastActivatedTime}`）｜**可见缺失**：① 全局热键（悬浮窗/截图）在 Linux 无响应；② U 盘插拔消息在 Linux 无人发；③ 锁屏消息：HEAD 无人发，工作区新增 `Helpers/SessionLockWatcher.Linux.cs`（未提交）已补上同语义消息；④ 双击标题栏最大化拦截（因不接管标题栏已无意义）｜建议：保留主体（平台 API 缺失），把 ①② 逐条登记到 12-shims / 18，而不是让它静默缺失
  9. 上游无 vs 移植 34 / 36 / 37｜新增能力｜—｜三个 `Activated` 订阅（OnWindowActivated / OnFirstActivated / OnIconActivatedOnce）｜建议：保留
  10. 上游无 vs 移植 38-56 + 71-97｜调试入口｜—｜`LUNARWARD_SELFTEST=1` → `OnSelfTestActivated` + 8s `DispatcherQueueTimer` 兜底（L-185）+ `UiSelfTest.RunAsync(this, 1s)` + `Environment.Exit(failed == 0 ? 0 : 1)`（96）｜建议：保留；注意 `Environment.Exit` 在 Release 也存在，靠环境变量守门（可接受）
  11. 上游无 vs 移植 100-115｜新增能力｜—｜`_iconApplied` / `OnIconActivatedOnce` → `Helpers.AppIcon.Apply(Title)`｜不可见｜建议：保留
  12. 上游无 vs 移植 142-160｜平台适配｜—｜`OnFirstActivated` 按 `Content?.XamlRoot?.RasterizationScale` 重设 1200×676 物理尺寸，异常记 Serilog（L-35）｜可见：高 DPI 下窗口尺寸正确｜建议：保留
  13. 上游无 vs 移植 164-210｜调试入口｜—｜`RunDebugWindowAction`（`DevSwitches.WindowAction`：minimize / close / gamesetting / backgroundview，其中 gamesetting 走 `GameEntryService.GetAllEntries().FirstOrDefault` + `GameLauncherSettingDialog`）｜不可见（需显式开 DevSwitches）｜建议：保留
  14. 上游无 vs 移植 308-320｜新增能力｜—｜`Content_KeyDown` 先判 `MainContentHost?.TryCloseGameSelector() == true` 则吃掉 Esc，否则 `Hide()`｜可见：Esc 先关游戏选择层，不再直接隐藏窗口｜建议：保留
  15. 上游无 vs 移植 361-380｜新增能力｜—｜`RootView`（363）、`OpenSettings()`（366）、`OpenRuntimeSettings(string?, bool)`（372-376）、`OpenGameSettings()`（380）｜被托盘菜单、MainView、调试入口调用｜建议：保留。**移植 361-362 两条 `<summary>` 堆叠**（「托盘菜单用：把主视图导航到设置页。」实际属于 `OpenSettings`，却压在 `RootView` 头上）→ 建议整理
  16. 上游无 vs 移植 9 / 12 / 15｜工程性｜—｜多 `using System.Linq;`、`System.Threading.Tasks`；`using Starward.Features.Overlay;` / `Starward.Features.Screenshot` 因整块删除变成无用 using｜不可见｜建议：清理
- 额外：
  - 上游有而移植没有：Win32 子类化带来的四类系统事件（见差异 8）、自绘标题栏、`GC.Collect()`、`SetCursorPos`。
  - 移植多出且被调用：自检入口、图标应用、首次激活换算、调试窗口动作、Esc 分层关闭、四个公开导航方法。
  - `AppWindow_Closing` 的对话框与写回逻辑（移植 262-279）与上游 103-130 等价，只是 Cancel 位置调整。

### Features/ViewHost/MainView.xaml

- 编译状态：参与编译（无覆盖）。
- 逐行结论：差异 1 处
- 差异：
  1. 上游 351-353 之后无 footer vs 移植 356-369｜新增能力｜—｜新增 `<NavigationView.FooterMenuItems>` 单项 `NavigationViewItem_RuntimeEnv`（`Tag="RuntimeEnvironmentSetting"`、FontIcon `E7F8`、文本 `{x:Bind lang:Lang.Lunarward_RuntimeEnv_Title}`、注释标 D80：运行环境升为一级页、放在齿轮上方）｜可见：左侧导航新增「运行环境」一级入口｜建议：保留
- 额外：`Lang.Lunarward_RuntimeEnv_Title` 是移植侧新增的资源键（`src/Lunarward/Language/` 目录不存在，键在生成的 Lang 类里）；能编译说明键已定义，但**若键缺失会变成 x:Bind 编译错误**，改资源时注意。

### Features/ViewHost/MainView.xaml.cs

- 编译状态：参与编译（无覆盖）。
- 逐行结论：差异 13 处
- 差异：
  1. 上游 106 vs 移植 303｜平台适配｜`var queue = Content.DispatcherQueue;`｜`(Content as Microsoft.UI.Xaml.FrameworkElement)?.DispatcherQueue`｜边界：为 null 时 `GamepadController` 收到 null queue（应先判空跳过初始化）｜建议：保留写法，补判空
  2. 上游无 vs 移植 101-160｜新增能力｜—｜`CurrentPageType`(104)、`CurrentPageContent`(108)、`NavigateToGameSettings`(111-114)、`TryCloseGameSelector`(121-136)、`NavigateToSettings`(139-142)、`NavigateToRuntimeEnvironment`(146-149)、`ShowHotkeyUnavailableHintOnce`(152-160)｜建议：保留
  3. 上游 97-109 vs 移植 163-306｜调试入口 + 新增｜—｜`MainView_Loaded` 增补：DEBUG 直达（`DevSwitches.Page`/`--subpage`、installprogress / predownload 夹具、1.5s 二次 `DebugNavigate`）+ 非 Windows 的 KnownGames toast（287-292，`OneShotHints "auto-scan"`）｜建议：保留（DEBUG 段用 `#if DEBUG` 包住）
  4. 上游 118 vs 移植 316｜行为错位（改进）｜`if (proxy is not null)` 每次启动都提示系统代理｜`proxy is not null && Helpers.OneShotHints.TryTake("system-proxy")`｜可见：重复提示消失（D86）｜建议：保留
  5. 上游 145-147 vs 移植 343-345｜**行为错位**｜`UpdateNavigationView(); HideLauncherConfigView(); UpdateDragableArea();`｜只有 `UpdateNavigationView();` + `WeakReferenceMessenger.Default.Send(new CurrentGameChangedMessage(CurrentGameId));`（D62）｜**可见（边界）**：在启动器配置面板（`LauncherConfigView`，MainView.xaml:378）打开时切游戏，面板不会自动收起（上游会）；`UpdateDragableArea()` 在 Linux 因不接管标题栏本就无效，可忽略｜建议：补回 `HideLauncherConfigView();`（现有实现仍在移植 399）
  6. 上游无 vs 移植 349-371｜调试入口｜—｜`DevSwitchGameIfRequestedAsync`（`LUNARWARD_DEV_SWITCH_GAME`，延时 6s）｜建议：保留
  7. 上游 264-273 vs 移植 487-497｜新增能力｜—｜`NavigationView_ItemInvoked` 增加 `nameof(Features.Setting.RuntimeEnvironmentSetting)` 分支｜建议：保留
  8. 上游 290 vs 移植 514-515 / 524-527｜新增能力（与 7 配套）｜—｜"不缓存导航"判定从 `SettingPage` 扩为 `SettingPage or RuntimeEnvironmentSetting`；命中时选中 `NavigationViewItem_RuntimeEnv`｜建议：保留
  9. 上游 365-377 vs 移植 595-604｜平台适配｜版本变化后 `new UpdateWindow().Activate()` 展示更新内容｜非 Windows 时只把 `AppConfig.LastAppVersion = AppConfig.AppVersion`、`_lastCheckUpdateTime = now - 55min` 然后 return（D5 走包管理器）｜可见：Linux 不再弹只有标题的空更新窗｜建议：保留
  10. 上游 426 vs 移植 664 + 668｜平台适配｜`private void InitializePassportUser()`（同步）｜`private async void` + `await Task.Delay(1500)`｜可见：启动 1.5s 后才回填账号信息（错开首页加载）｜建议：保留
  11. 移植 27 与 30 重复 `using System.Linq;`｜工程性｜—｜同一文件两条相同 using → 编译警告 CS0105｜不可见｜建议：删一行
  12. 移植 101-103 三条残留 `<summary>`（「托盘菜单「打开设置」用。」「导航到当前游戏的设置页」「当前页类型」）全堆在 `CurrentPageType` 上方｜工程性（文档注释错位）｜—｜前两条实际对应 `OpenSettings` 之类的方法，这里没有对应成员｜不可见｜建议：整理或删除
  13. 移植 34 `using Starward.Features.RPC;`｜工程性（无用 using）｜—｜全文件再无任何 `Rpc` 符号引用（grep 仅命中此行）｜不可见｜建议：删除
- 额外：
  - 上游有而移植没有：`Content.DispatcherQueue` 直取、切游戏时隐藏配置面板、`UpdateDragableArea()` 调用（Linux 无效果）。
  - 移植多出且被调用：`TryCloseGameSelector`（MainWindow 308-320 调用）、`NavigateTo*` 三件套（MainWindow 366-380、托盘菜单调用）、`ShowHotkeyUnavailableHintOnce`（297 调用）。

### Features/ViewHost/SystemTrayWindow.xaml

- 编译状态：参与编译（无 Compile Remove、无覆盖）；但**运行时从不实例化**（见下方「额外」）。
- 逐行结论：差异 1 处
- 差异：
  1. 上游 13 vs 移植 13｜工程性（跟随基类改名）｜`Closed="WindowEx_Closed"`｜`Closed="WindowBase_Closed"`｜不可见（同文件 .cs 131-137 已同步改名，处理器存在）｜建议：保留
- 额外：
  - `Title="StarwardTrayMenu"`、`ToolTipText="Starward"` 两侧相同 → 不算差异；移植侧 `ToolTipText` 在 `.cs:67` 运行时被改成 `"Lunarward"`，`Title` 仍是 Starward（窗口不可见，无影响）。
  - **运行时事实**：`App.xaml.cs:392-406` 的 `EnsureSystemTray()` 在非 Windows 直接走 `StartTrayAsync()` + `SessionLockWatcher` 并 return，`m_SystemTrayWindow` 永不为非 null；`--hide` 分支（App.xaml.cs:205-207）也只在 Windows 才 `new SystemTrayWindow()`。→ 本文件在 Linux 上编译但从不实例化。

### Features/ViewHost/SystemTrayWindow.xaml.cs

- 编译状态：参与编译（无覆盖）；**整份运行时死代码**（同上一节结论）。
- 逐行结论：差异 6 处
- 差异：
  1. 上游 51-56 vs 移植 51-52｜平台适配｜`GetWindowLongPtr`/`SetWindowLong` 去掉 `WS_CAPTION|WS_BORDER` + `DwmSetWindowAttribute` 圆角｜只剩注释｜不可见｜建议：保留
  2. 上游 64-73 vs 移植 60-69｜功能缺失｜`trayIcon.Icon = ...`（`Kernel32.GetModuleHandle` + `User32.LoadIcon("#32512")`）｜只设 `trayIcon.ToolTipText = "Lunarward"`，不设图标｜不可见（窗口不实例化）；注意 `trayIcon` 是移植侧占位控件 `src/Lunarward/Compat/NotifyIcon/TaskbarIconCompat.cs`（namespace `H.NotifyIcon`，`Icon`/`Dispose` 空实现）｜建议：保留（托盘图标由 `Features/Tray/StatusNotifierTray` 提供）
  3. 上游 74-84 vs 移植 74-84｜平台适配｜`args.WindowActivationState == WindowActivationState.Deactivated`（强类型）｜`args.WindowActivationState.ToString() == "Deactivated"`（D134：publish 与本地构建的枚举投影不同，避免 CS0266）｜不可见｜建议：保留，待 Uno 修好投影后改回强类型比较
  4. 上游 89-102 vs 移植 88-108｜行为错位｜`GetCursorPos` + `CalculatePopupWindowPosition(TPM_RIGHTALIGN|TPM_BOTTOMALIGN|TPM_WORKAREA)` + `MoveWindow` → 菜单弹在鼠标处｜`DisplayArea.GetFromWindowId` 把窗口钉在工作区**右上角**｜可见（若启用）：菜单不再跟随托盘图标位置｜建议：保留（M2 换托盘协议）；**移植 97 行 `height` 计算后未使用**，删掉
  5. 上游无 vs 移植 131-137 `WindowBase_Closed`（原 `WindowEx_Closed`，内容仍 `trayIcon?.Dispose()`）｜工程性｜跟随基类改名｜建议：保留
  6. 上游 8 `using Vanara.PInvoke;`（被使用）vs 移植 8（已无用）｜工程性｜—｜删掉 Win32 调用后 using 变成噪音｜不可见｜建议：删除
- 额外：
  - 移植保留的 `AppWindow.IsShownInSwitchers = false`、`Closing += (_, e) => e.Cancel = true`、`Show(); Hide();`（移植 54-55）与上游一致。
  - 上游有而移植没有：托盘图标设置、按光标定位弹窗、窗口样式剥离 + DWM 圆角。
  - 结论：本文件所有差异对 Linux 用户**不可见**，可维持现状；真正在用的是 `Features/Tray/StatusNotifierTray`。

### Features/ViewHost/WelcomeWindow.xaml.cs

- 编译状态：参与编译（无覆盖）；**整份运行时死代码**。
- 逐行结论：差异 2 处
- 差异：
  1. 上游 45 vs 移植 46-48｜工程性（编译必需）｜`new RectInt32(0, 0, 100000, (int)(48 * UIScale))`｜对象初始化器 `new RectInt32 { X = 0, Y = 0, Width = 100000, Height = (int)(48 * UIScale) }`（D134：4 参构造器只在不带 RID 的 projection 存在，linux-x64 publish 报 CS1729）｜不可见｜建议：保留
  2. 上游 117 vs 移植 122｜文案/平台适配｜DEBUG 默认目录 `LocalApplicationData/Starward`｜`LocalApplicationData/lunarward`（Release 分支移植 124 仍是 `MyDocuments/Starward`）｜不可见｜建议：Release 分支也改成 `lunarward`，否则将来真启用向导会写进 `~/Documents/Starward` 这个不存在于其他任何地方的目录
- 额外：
  - **死代码证据**：`App.xaml.cs:78 if (AppConfig.UserDataFolder is null)` 才构造 WelcomeWindow；而 `AppConfig.CheckEnviromentAsync` 的已有目录分支（移植 164）与首次运行分支（移植 181）都会给 `UserDataFolder` 赋值 → Linux 上 `WelcomeWindow` 永不显示（D22 不做欢迎向导）。
  - 其余方法（`Grid_Loaded` 93-100、`InitializeDefaultUserDataFolder` 106-132、`CheckWritePermission` 136-177、`ChangeUserDataFolderAsync` 181-197、`CheckWebView2Support` 202-212、`CheckWebpDecoderSupportAsync` 217-232、`Hyperlink_Click` 236-247、`Start` 251-273）与上游逐行一致。
  - 移植 13 多一个 `using Starward.Features;`（工程性）。

### Features/Setting/SettingPage.xaml

- 编译状态：参与编译（无覆盖）。
- 逐行结论：差异 4 处
- 差异：
  1. 上游 2 vs 移植 3｜行为错位（必要）｜`<sf:PageBase x:Class=...>` 无缓存｜`NavigationCacheMode="Required"`（D94）｜可见：设置页状态（滚动位置/选中项）保留；副作用见 `.cs` 差异 2/3｜建议：保留
  2. 上游 17 vs 移植 19｜平台适配｜`<Grid>`｜`<Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">`（D61）｜可见：设置页不透明，壁纸/游戏背景大字不再透到表单后面｜建议：保留
  3. 上游 19 vs 移植 21｜工程性（必要）｜`<NavigationView IsBackButtonVisible=...>`（无 x:Name）｜新增 `x:Name="NavigationView_Setting"`｜不可见（code-behind 用它同步 SelectedItem）｜建议：保留
  4. 上游 82 vs 移植 85-86｜平台适配｜`<FontIcon FontFamily="{ThemeResource XboxFluent2}" Glyph="&#xE9CD;"/>`｜`<FontIcon Glyph="&#xE7FC;"/>`｜可见：手柄增强图标能显示（不再是方框，L-88）｜建议：保留
- 额外：移植 89-90 新增两行注释（说明「运行环境已提升为一级页（D80），SettingPage 里只留 Tag 映射，不再有可见条目」），非功能差异；`MenuItems` 其余内容与上游一致，**没有** RuntimeEnvironmentSetting 的可见条目（入口在 MainView footer）。

### Features/Setting/SettingPage.xaml.cs

- 编译状态：参与编译（无覆盖）。
- 逐行结论：差异 4 处
- 差异：
  1. 上游 19 vs 移植 20-30｜行为错位（必要）｜ctor 固定 `Frame_Setting.Navigate(typeof(AboutSetting));`｜`Type initialPage = ResolveInitialPage();` + `Frame_Setting.Navigate(initialPage);` + 按 Tag 同步 `NavigationView_Setting.SelectedItem`｜可见：`--subpage` / 直达请求能落到对应子页｜建议：保留
  2. 上游 20（ctor 里注册 `LanguageChangedMessage`）vs 移植 35-42｜行为错位（必要）｜ctor 注册 + 依赖「每次导航新建页面」｜改到 `OnLoaded()` 注册（B74/D94：缓存页 ctor 只跑一次），配合移植 122-125 `OnUnloaded` 的 `UnregisterAll(this)`｜可见：切语言后设置页文字能刷新｜建议：保留
  3. 上游无 vs 移植 46-62 + 65-88｜新增能力｜—｜`ApplyRequestedPage()`（`SettingNavigation.TakeTarget()` → 导航 + 同步选中）、`ResolveInitialPage()`（先 TakeTarget，再 `DevSwitches.SubPage` 映射 9 项 + `RuntimeEnvironmentSetting`，兜底 AboutSetting）｜建议：保留。**边界风险**：`ApplyRequestedPage` 只在 `OnLoaded` 跑；若用户正停在设置页（已缓存）时再请求直达子页，是否重新触发 `Loaded` 取决于调用方是否执行 `Navigate` —— 需真机验证一次（这是页面缓存引入的新失效模式）
  4. 上游 29-41 vs 移植 106｜新增能力｜—｜`NavigationView_ItemInvoked` 增加 `nameof(RuntimeEnvironmentSetting) => typeof(RuntimeEnvironmentSetting)`（保留旧 subpage 参数的兼容映射）｜建议：保留
- 额外：上游有而移植没有：ctor 里的固定初始页与 ctor 级消息注册；移植多出且被调用：`ApplyRequestedPage`/`ResolveInitialPage`/RuntimeEnv 映射。

---

## 覆盖清单（16 行，齐全）

| # | 文件 | 状态 |
| --- | --- | --- |
| 1 | App.xaml | 已读全（102/108）｜差异 4 处 |
| 2 | App.xaml.cs | 已读全（189/475）｜差异 14 处 |
| 3 | AppConfig.Common.cs | 已读全（64/96）｜差异 3 处 |
| 4 | AppConfig.Configuration.cs | 已读全（304/369）｜差异 16 处（+2 文档缺陷） |
| 5 | AppConfig.ServiceProvider.cs | 已读全（223/230）｜差异 6 处 |
| 6 | Program.cs | 已读全（139/139）｜差异 1 处（整份死代码） |
| 7 | Features/PageBase.cs | 已读全（80/105）｜差异 4 处 |
| 8 | Features/Update/UpdateWindow.xaml | 已读全（299/300）｜差异 1 处（代码后置被 Compile Remove，影响仅一致性） |
| 9 | Features/ViewHost/MainWindow.xaml.cs | 已读全（259/382）｜差异 16 处 |
| 10 | Features/ViewHost/MainView.xaml | 已读全（384/400）｜差异 1 处 |
| 11 | Features/ViewHost/MainView.xaml.cs | 已读全（615/858）｜差异 13 处 |
| 12 | Features/ViewHost/SystemTrayWindow.xaml | 已读全（53/53）｜差异 1 处（运行时死代码） |
| 13 | Features/ViewHost/SystemTrayWindow.xaml.cs | 已读全（134/140）｜差异 6 处（运行时死代码） |
| 14 | Features/ViewHost/WelcomeWindow.xaml.cs | 已读全（274/277）｜差异 2 处（运行时死代码） |
| 15 | Features/Setting/SettingPage.xaml | 已读全（110/116）｜差异 4 处 |
| 16 | Features/Setting/SettingPage.xaml.cs | 已读全（62/129）｜差异 4 处 |
