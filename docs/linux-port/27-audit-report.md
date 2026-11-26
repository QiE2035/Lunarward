# 27 · 实现错位审计报告（可读版）

> ⚠️ **历史快照**：本文记录的是 2026-11-21 当时的状态，之后又被 D128–D138 的对齐改动推进过；**当前总体对齐情况看 `30-full-alignment-audit.md`，逐点偏离判定看 `25-deviation-register.md`**，本文只用于回看当时的问题与论据。

> 生成日期：2026-11-22　基线：上游 `df8ae07` ↔ 移植 `b5b75a5`（D94 后）。
> 数据来源：[25 逐点偏离总账](25-deviation-register.md) / [26 候选清单](26-misalignment-audit.md)。本文件是给人读的版本：按模块讲「哪里错了、上游怎么写、用户会看到什么」，编号与 26 一致。

> **快照说明**：本文是 2026-11-22「修复前」的可读版，用于理解问题本身；修复进度与逐条证据以 [`26-misalignment-audit.md`](26-misalignment-audit.md) 为准（当前 70/76 已修，6 条不成立）。后续新增的审查（`28` 运行环境、`29` 运行环境 UI、D109 页面审查）不在本文范围。

复核结论：✅ 成立 **51** / ◐ 部分成立 **19** / ❌ 不成立 **6**，共 76 条。

修复状态（2026-11-22）：**已修复 70 条**（成立 51 + 部分成立 19，见 26 号文档每条的 `🛠 修复` 行，决策 D95–D103）；**6 条不成立不修**。


## 安装/更新/预下载（11 条）

### B01 · 安装成功后不记录游戏安装路径

`Features/GameInstall/InstallGameDialog.Linux.cs:101`　置信度 high　**✅ 成立**

- **现状**：装完回到启动器页，按钮仍是「安装」而不是「启动」；游戏无法启动/更新。重启应用后只能靠 24h 目录扫描找回，而扫描的目录名匹配又很严格（见另一条 bug），多数情况下识别不回，用户必须手动「定位游戏路径」。

- **上游/对照**：`Features/GameInstall/InstallGameDialog.xaml.cs:395` — 上游 L395 成功后第一件事是 GameLauncherService.ChangeGameInstallPath(CurrentGameId, InstallationPath)（内部写 AppConfig.SetGameInstallPath 并发 GameInstallPathChangedMessage），随后才发任务消息并 Close。移植版 L94 只 Directory.CreateDirectory、L95 起任务、L101 弹 toast、L102 Hide()，全程不写路径。全仓 grep：Linux 侧写安装路径的地方只有 GameInstallScanner.Linux.cs:84/128、设置对话框、手动「定位游戏路径」，GUI 安装流程自身永不落盘。

- **复核证据**：src/Lunarward/Features/GameInstall/InstallGameDialog.Linux.cs:94-102 流程只有 Directory.CreateDirectory(path) → _installService.StartInstallAsync → InAppToast → Hide()，没有上游 src/Starward/Features/GameInstall/InstallGameDialog.xaml.cs:395-400 的 GameLauncherService.ChangeGameInstallPath + GameInstallTaskStartedMessage；StartAsync 本体 src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs:150-172 也只填 context，全文件唯…

### B02 · 语音包语言硬编码中文，选择区被隐藏且处理函数是空壳

`Features/GameInstall/InstallGameDialog.Linux.cs:23`　置信度 high　**◐ 部分成立**

- **现状**：只能安装/修复中文语音包：想要英/日/韩语音或「中配+日配」组合无从选择；对已装多语音的客户端做修复时也只补中文一套，可能与其他语音文件版本不一致。

- **上游/对照**：`Features/GameInstall/InstallGameDialog.xaml.cs:366` — 移植 L23 是 `private readonly AudioLanguage _audioLanguage = AudioLanguage.Chinese;`，XAML 里 Segmented_SelectLanguage Visibility="Collapsed"，SelectionChanged 处理器落在 InstallGameDialog.LinuxAuto.cs 的空方法体。上游 L366–388 按 zh-cn/en-us/ja-jp/ko-kr 四个 tag 组装 AudioLanguage 标志。服务端 L282–291 在 fields 缺音频字段时用 SophonClient.MapAudioField(context.AudioLanguage.ToString()) 决定拉哪套语音。

- **复核证据**：src/Lunarward/Features/GameInstall/InstallGameDialog.Linux.cs:23 `private readonly AudioLanguage _audioLanguage = AudioLanguage.Chinese;`；src/Lunarward/Features/GameInstall/InstallGameDialog.xaml:79-83 Segmented_SelectLanguage Visibility="Collapsed"；src/Lunarward/Features/GameInstall/InstallGameDialog.LinuxAuto.cs:14 `private void Segmented_SelectLanguage_SelectionChanged(object sender, SelectionChangedEventArg…

### B03 · 对话框属性没有变更通知，OneWay 绑定永不刷新

`Features/GameInstall/InstallGameDialog.Linux.cs:28`　置信度 high　**✅ 成立**

- **现状**：选完目录后对话框里的路径文本不变（ChangePathAsync 改了 InstallationPath 但界面不动）；点开始时的「请先选择安装目录」「安装目录无效」错误提示永远不显示；可用/解压空间文本即使被赋值也保持空白。用户看不到自己选了什么，误以为选目录失败。

- **上游/对照**：`Features/GameInstall/InstallGameDialog.xaml.cs:25` — 上游类标注 [INotifyPropertyChanged]，InstallationPath/ErrorMessage/AvailableSpaceText/UnzipSpaceText 全走 SetProperty。移植类是普通类，四个属性都是自动属性；而 XAML 根元素设了 x:DefaultBindMode="OneWay"（InstallGameDialog.xaml:12），所有 x:Bind 只在首次渲染取值。

- **复核证据**：src/Lunarward/Features/GameInstall/InstallGameDialog.Linux.cs:13 `public sealed partial class InstallGameDialog : ContentDialog` 未实现 INotifyPropertyChanged，:29/:33/:37/:41 的 InstallationPath/AvailableSpaceText/UnzipSpaceText/ErrorMessage 全是自动属性；赋值点仅 :78 `InstallationPath = folder;` 与 :89/:98/:107 ErrorMessage，无 PropertyChanged。src/Lunarward/Features/GameInstall/InstallGameDialog.xaml:12 `x:DefaultBindMode="OneW…

### B04 · 打开对话框时既不预填默认安装路径，也不计算安装体积

`Features/GameInstall/InstallGameDialog.Linux.cs:51`　置信度 high　**✅ 成立**

- **现状**：对话框一打开路径是空的，必须手动点「选择目录」；「需要空间 / 解压后大小」两行与 AvailableSpaceText 永远是空串；也没有上游的「装到盘符根目录/网络盘」提示。开箱即用的默认流程退化成一个必须手选的对话框。

- **上游/对照**：`Features/GameInstall/InstallGameDialog.xaml.cs:62` — 上游 Loaded（L60–62）调 SetDefaultInstallationPath()（AppConfig.DefaultGameInstallationPath → … → ProgramFiles/miHoYo 兜底，L73–117）与 GetGamePackageAsync()（算压缩包体与解压后大小，L122–288）。移植 ctor（L51–57）只 InitializeComponent + 建三个命令，没有 Loaded 订阅，也没有任何体积计算入口。

- **复核证据**：src/Lunarward/Features/GameInstall/InstallGameDialog.Linux.cs:52-57 构造函数只 InitializeComponent + 建三个命令，无 `Loaded +=`；活动实现里没有 SetDefaultInstallationPath/GetGamePackageAsync/ComputePackageSize（这些只在上游副本 src/Lunarward/Features/GameInstall/InstallGameDialog.xaml.cs:42/:60-61 出现，而该文件被 src/Lunarward/Lunarward.csproj:96 `<Compile Remove>`）。ChangePathAsync（.Linux.cs:60-81）只 PickFolder→拼子目录→赋值；调用点 src/Lunarward/Features/G…

### B36 · 自动识别对常见目录名失效，且「自动搜索安装的游戏」在 Linux 上是空操作

`Features/GameInstall/GameInstallScanner.Linux.cs:257`　置信度 medium　**✅ 成立**

- **现状**：重装/迁移后自动识别多半认不回游戏，启动器页显示「安装」；在游戏选择页点「自动搜索」没有任何反应（按钮点了等于没点），用户必须逐个手动「定位游戏路径」。

- **上游/对照**：`Features/GameInstall/GameSelector.xaml.cs:893` — NameMatches 要求目录名 == biz 值，或同时包含游戏名与服名（如 genshin + cn）。玩家最常见的目录名是「Genshin Impact」「Star Rail」，不含 cn/os，永远命中不了；FindGameDirectory 只下探两层。另外 GameSelector.AutoSearchInstalledGames 的移植副本在 L1005 加了 `if (!OperatingSystem.IsWindows()) { continue; }`，注册表分支被跳过后既不写路径也不调用扫描器。

- **复核证据**：src/Lunarward/Features/GameInstall/GameInstallScanner.Linux.cs:257-268 NameMatches 只接受 `name == key`（如 hk4e_cn）或 `name.Contains(game) && name.Contains(server)`（game 为代码名 hk4e/hkrpg，server 为 cn/global），真实目录 Genshin Impact / Star Rail / ZenlessZoneZero 均不命中；:271-305 FindGameDirectory 只枚举 root、一级、二级子目录且仍要 NameMatches。自动搜索按钮：GameSelector.xaml.cs:1005-1008 `if (!OperatingSystem.IsWindows()) { continue; }`（在注册表分支前，既不…

### B37 · 「停止」在竞态下被记录成「失败」，并弹出错误通知

`Features/GameInstall/GameInstallService.Linux.cs:118`　置信度 medium　**◐ 部分成立**

- **现状**：点「停止」后进度面板偶发变红，提示 OperationCanceledException 与「任务失败」桌面通知；用户以为出错了，实际是自己停的。也可能残留「已下载 N 个文件」的错误明细。

- **上游/对照**：`Features/GameInstall/GameInstallService.cs:309` — StopTaskAsync 只做 cts.Cancel() + _paused=false + context.State=Stop。InstallPipeline 每个文件任务的 catch(Exception)（InstallPipeline.cs:187–203）会把 OperationCanceledException 计入 failed 并写进 errors；只有生产者循环里的 semaphore.WaitAsync(cancellationToken) 抛出的 OCE 才冒泡到 RunAsync 的 catch(OperationCanceledException) → State=Stop（L515–518）。取消时若正在文件任务内，会走到 L465–470：State=Error、ErrorMessage 拼成「xxx: OperationCanceledException: 一个任务被取消」。

- **复核证据**：src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs:122-134 StopTaskAsync 只 `cts.Cancel()` + `context.State = GameInstallState.Stop`；InstallPipeline.cs:119 `await semaphore.WaitAsync(cancellationToken)` 在 try 外，:187-201 文件任务 `catch (Exception ex) { Interlocked.Increment(ref failed); ... errors.Add($"{asset.AssetName}: {ex.GetType().Name}: {ex.Message}"); }` 把 OperationCanceledException 吞成失败；GameInsta…

### B38 · 卸载直接递归删除整个安装目录，不区分用户数据/截图、也不先结束游戏进程

`Features/GameInstall/GameInstallService.Linux.cs:92`　置信度 medium　**◐ 部分成立**

- **现状**：游戏正在运行时点卸载：文件被占用，删除到一半抛异常 → 返回 false，留下半删目录（有的文件没了、配置还在），后续安装/修复都会撞上残骸；用户数据与本地截图目录被无提示连带删除，上游的「保留用户数据」设置在 Linux 侧形同虚设。

- **上游/对照**：`Features/GameInstall/GameInstallService.cs:346` — 上游把 UninstallGameRequest{GameBiz, GameId, InstallPath, UserDataFolder=AppConfig.UserDataFolder, ScreenshotFolder=AppConfig.ScreenshotFolder, GameExeName=GameLauncherService.GetGameExeName(...)} （L330–362）交给 RPC，由服务端按设置决定删哪些目录并先结束游戏。移植 L85–95 直接 Directory.Delete(path, recursive:true)，随后 AppConfig.SetGameInstallPath(null)。

- **复核证据**：src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs:89-98 `await Task.Run(() => Directory.Delete(path, recursive: true)); AppConfig.SetGameInstallPath(gameId.GameBiz, null); return true; ... catch { return false; }`，全流程没有截图备份；上游 src/Starward.RPC/GameInstall/GameUninstallService.cs:60-62 `BackupScreenshot(request, gameConfig); _logger.LogInformation("Deleting folder …"); Directory.Delete(installPath, …

### B39 · 无本地安装也能发起预下载，并把数据写进自造的默认目录

`Features/GameInstall/PreDownloadDialog.Linux.cs:190`　置信度 medium　**◐ 部分成立**

- **现状**：对一个根本没装的游戏点「预下载」，进度条真的开始拉整包（上百 GB）到 ~/.local/share/lunarward/Games/<biz>；磁盘被占满，且该目录之后不会被安装流程复用或校验，等于白下。

- **上游/对照**：`Features/GameInstall/PreDownloadDialog.xaml.cs:95` — 上游 GetGamePackageAsync 在 GetGameInstallPath 为空或 GetLocalGameVersionAsync 为空时点亮 TextBlock_PredownloadUnavailable 并 return（L95–101），CheckCanPreDownload 也不会使能按钮。移植 LoadSizeInfoAsync 只在 Sophon build 取不到时才置 Unavailable + 禁用；本地版本缺失只是把体积退化成整包；ResolveInstallPath（L95–104）还会拼出 <UserDataFolder>/Games/<biz> 并回写 InstallationPath，StartPredownloadAsync 里 Directory.CreateDirectory 后直接开任务。

- **复核证据**：src/Lunarward/Features/GameInstall/PreDownloadDialog.Linux.cs:95-104 无 AppConfig 路径时 `InstallationPath = AppConfig.GetGameInstallPath(CurrentGameId.GameBiz) ?? Path.Combine(AppConfig.UserDataFolder ?? …, "Games", CurrentGameId.GameBiz.Value)`；:134-148 `localVersion = SophonClient.TryReadLocalVersion(InstallationPath)` 为 null 时退回整包体积、不禁用；:188-190 `string path = ResolveInstallPath(); Directory.CreateDirectory(pat…

### B65 · 扫盘在启动路径上同步执行，缓存过期时拖慢冷启动

`Features/GameInstall/GameInstallScanner.Linux.cs:38`　置信度 low　**◐ 部分成立**

- **现状**：首次启动、缓存超过 24h、或用户把游戏放在大目录（如 /opt 下成千上万个文件夹）时，主窗口出现明显延迟；点击「自动搜索」也会卡住界面。

- **上游/对照**：`Features/GameInstall/GameSelector.xaml.cs:940` — ScanAndApply 是同步方法（L38–141），由 App.xaml.cs:89 在 OnLaunched 里直接调用；一次未命中要遍历 $HOME/games、$HOME/Games/miHoYo、/opt 与 3 个 Steam 根，每个根做 2 层目录枚举并对每个候选取 exe 名，全程 UI 线程。上游注册表读取是常数级，且发生在命令内部（异步）。

- **复核证据**：src/Lunarward/App.xaml.cs:89 `Features.GameInstall.GameInstallScanner.ScanAndApply();` 是同步调用，早于 :181 `m_MainWindow = new MainWindow();`；GameInstallScanner.Linux.cs:38 ScanAndApply 同步执行，:238-251 根目录含 $HOME/games 等 + /opt + 3 个 Steam common，:279/:291 两层 Directory.EnumerateDirectories + File.Exists，并对 9 个 gameBiz 重复遍历。但 GameSelector.xaml.cs:981 AutoSearchInstalledGames 在 :1005 `if (!OperatingSystem.IsWindows()) { …

### B66 · 开始按钮的可点性没有任何代码路径保障

`Features/GameInstall/InstallGameDialog.Linux.cs:51`　置信度 low　**✅ 成立**

- **现状**：视 Uno 版本而定：可能「开始安装 / 开始预下载」按钮全程灰掉，安装入口彻底不可用；也可能按钮可点但完全不反映路径/空间/语音是否就绪（用户能在非法状态下点下去）。

- **上游/对照**：`Features/GameInstall/InstallGameDialog.xaml.cs:312` — 两个对话框的 XAML 都把 Button 写死 IsEnabled="False"；全仓唯一的使能点是已被 <Compile Remove> 排除的上游 code-behind（InstallGameDialog.xaml.cs:312、PreDownloadDialog.xaml.cs:326）。移植侧 InstallGameDialog.Linux.cs 从不解禁，PreDownloadDialog.Linux.cs:127 只会置 false。至于按钮最终是否可点，取决于 Uno 的 ButtonBase 是否用 Command.CanExecute 覆盖本地 IsEnabled 值（Uno.UI 里存在 UpdateCanExecute/OnCanExecuteUpdated 符号，倾向会覆盖）。

- **复核证据**：src/Lunarward/Features/GameInstall/InstallGameDialog.xaml:137-143 `Button_StartInstallation` 带 Command="{x:Bind StartInstallationCommand}" 且 IsEnabled="False"；PreDownloadDialog.xaml:97-104 同样 IsEnabled="False"。唯一解禁代码 InstallGameDialog.xaml.cs:312 / PreDownloadDialog.xaml.cs:326 所在文件被 src/Lunarward/Lunarward.csproj:96-97 <Compile Remove> 排除；编译中的 InstallGameDialog.Linux.cs、PreDownloadDialog.Linux.cs:184-204 从不解禁，…

### B67 · 安装/预下载开始时不广播任务消息，页面靠 1s 轮询才看到任务

`Features/GameInstall/InstallGameDialog.Linux.cs:95`　置信度 low　**◐ 部分成立**

- **现状**：点开始后进度区最多延迟约 1 秒才出现；这段时间对话框已 Hide，页面上仍是「安装」可点，用户可能重复点击（重复点击由 StartAsync 的去重与 _cancellations.TryAdd 兜住，不会双开，但观感上是「没反应」）。

- **上游/对照**：`Features/GameInstall/InstallGameDialog.xaml.cs:396` — 上游成功后 WeakReferenceMessenger.Send(new GameInstallTaskStartedMessage(task))（InstallGameDialog.xaml.cs:396、PreDownloadDialog.xaml.cs:350），启动器页 OnGameInstallTaskStarted 立即展开进度区。移植两个对话框都不发消息（消息类型本身由 GameInstallShims.cs:89–94 提供），页面只能靠 GameLauncherPage.InstallTask.cs:21–56 的 1s DispatcherTimer 轮询 GetGameInstallTask。

- **复核证据**：InstallGameDialog.Linux.cs:84-109 StartInstallationAsync：:95 await StartInstallAsync、:101 toast、:102 Hide()，不发 GameInstallTaskStartedMessage；PreDownloadDialog.Linux.cs 的 StartPredownloadAsync 同样只 toast+Hide。编译树里唯一 Send 在 GameLauncherSettingDialog.xaml.cs:472（修复路径）；InstallGameDialog.xaml.cs:399 / PreDownloadDialog.xaml.cs:350 已被排除。页面靠 GameLauncherPage.InstallTask.cs:21-28 的 1s DispatcherTimer 轮询 GetGameInstallTa…


## 游戏设置与运行环境（10 条）

### B05 · GetGPUInfos 用 "card[0-9]*" 作 EnumerateDirectories 的 searchPattern，恒匹配 0 个目录 → GPU 列表永远为空

`Features/HoYoPlay/HoYoPlayService.cs:L328`　置信度 high　**✅ 成立**

- **现状**：上报给 HoYoPlay 接口的 GPU 列表恒为空，接口按「未知设备」返回 DX 配置/画质建议；日志里看不出任何异常。

- **上游/对照**：`Features/HoYoPlay/HoYoPlayService.cs:L308-330` — Directory.EnumerateDirectories 的 searchPattern 只支持 * 和 ?，不支持 [] 字符类（与 Windows 通配/正则习惯不同）。实测（/tmp 下 .NET 控制台工程，同一台机器）：EnumerateDirectories("/sys/class/drm", "card[0-9]*") 返回 0 项，而 "card*" 返回 5 项（card1、card1-DP-1、card1-DP-2、card1-eDP-1、card1-HDMI-A-1）。因此 gpuInfos 恒为空 List，被 GetDXConfigsAsync(launcherId, lang, gameIds, gpuInfos, ct)（L305-306）原样上报给米哈游接口。顺带：即使改成 card*，连接器目录（card1-DP-1 等）也会被当成独立显卡重复计入，应按 /sys/class/drm/card[0-9]+ 精确过滤（枚举后自行 EndsWith 判断或用 Directory.GetDirectories + 正则）。

- **复核证据**：src/Lunarward/Features/HoYoPlay/HoYoPlayService.cs:328 `foreach (var cardDir in Directory.EnumerateDirectories(drmRoot, "card[0-9]*"))`（drmRoot="/sys/class/drm"，:322）。本机 net10.0 实测同一模式：`card[0-9]*` 返回 0 项，`card*` 返回 5 项（card1、card1-HDMI-A-1、card1-DP-2、card1-eDP-1、card1-DP-1）；.NET searchPattern 仅支持 * 与 ?。gpuInfos 在 :305-306 原样传给 GetDXConfigsAsync。

### B10 · 设置页的分辨率/语音/FPS/HDR 亮度全部读写 ~/.wine，而不是游戏实际运行的 profile 前缀

`Features/GameSetting/GameSettingService.cs:L42-79、L91-131、L171-206、L238-277、L359-379`　置信度 high　**✅ 成立**

- **现状**：游戏设置页「套用」后游戏内分辨率/语音/帧率不变；重新进设置页读到的永远是默认值（分辨率 0、语音 null、FPS 默认）；HDR 开关却因为单独解析前缀而可能生效，出现「HDR 生效、其他设置都不生效」的割裂现象。

- **上游/对照**：`Features/GameSetting/GameSettingService.cs:L39-306` — 除 SetGenshinEnableHDRAsync 外，所有 WineRegistry 调用都走默认前缀：WineRegistry.WinePrefix（WineRegistry.cs:45-61）= _scopedPrefix → 环境变量 LUNARWARD_WINEPREFIX → ~/.wine。但 WineRegistry.UsePrefix（WineRegistry.cs:32）在全仓库 0 个调用点，应用进程也从不为自己设置 LUNARWARD_WINEPREFIX（只有 PlayTimeRecordService.cs:275 给子进程设）。而游戏启动用的是 profile 前缀：GameLauncherService.Linux.cs:216-263 ProfileBinding.Resolve(AppConfig.CurrentGameBiz) → EnvironmentResolver.EffectivePrefixPath(profile)（EnvironmentResolver.cs:93-102：PrefixPath 为空时落到 PrefixManager.PrefixRoot/<profile 名>）→ PrefixManager.EnsurePrefix(...) → info.Environment["WINEPREFIX"] = prefix（L307）。

- **复核证据**：src/Lunarward/Features/GameAccount/WineRegistry.cs:45-61 WinePrefix = _scopedPrefix → 环境变量 LUNARWARD_WINEPREFIX → ~/.wine；`UsePrefix`(:32) 全仓 0 个调用点（仅注释 :168 提到）。GameSettingService 除 HDR 外全传 null：分辨率 :53-64/:94、语音 :138/:148/:177/:186/:200、FPS :212/:252/:274、HDR 亮度 :332/:369/:380。HDR 单独 :291-298 `string? prefix = winePrefix ?? ResolveGamePrefix(biz);` + `WineRegistry.SetValueAsync(key, WINDOWS_HDR_ON_h3132281285…

### B11 · 原神「HDR 亮度设置」对话框在移植版里是空壳：没有 XAML、没有 Content、没有按钮

`Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs:L13-54`　置信度 high　**✅ 成立**

- **现状**：点「HDR 亮度设置」后要么弹出空白对话框、要么异常被 OpenGenshinHDRLumianceSettingWindow 的 catch(Debug.WriteLine) 吞掉而毫无反应；用户无法调整 HDR 亮度，且没有可点的关闭按钮（只能等 Esc/外部关闭）。

- **上游/对照**：`Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.cs:L30-60` — Lunarward.csproj:94 用 <Compile Remove> 排除了上游 code-behind，XAML 也被改名为 GenshinHDRLuminanceSettingDialog.xaml.disabled（202 行，含滑杆/预览/确定按钮），所以 GenshinHDRLuminanceSettingDialog.Linux.cs 这个 partial class 没有 InitializeComponent、没有构造函数，也没有任何 Content/Title/PrimaryButton 设置。GameSettingPage.xaml.cs:496 仍然 `new GenshinHDRLuminanceSettingDialog { XamlRoot = ..., CurrentGameBiz = ... }.ShowAsync()`，弹出来的是一个内容为空、没有确定/取消按钮的 ContentDialog（MaxLuminance/UILuminance/SceneLuminance 三个属性没有任何 UI 能改）。另外 .Linux.cs 也没有调用 GameSettingService.GetGenshinHDRLuminanceAsync 回读已保存值（上游在 Loaded 里 L52 回读）。

- **复核证据**：src/Lunarward/Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs:13-53 只有 MaxLuminance/SceneLuminance/UILuminance 属性与 :34-39 SaveAsync（调 GameSettingService.SetGenshinHDRLuminanceAsync），无构造函数、无 InitializeComponent、无 Content/Title/PrimaryButtonText；`ls src/Lunarward/Features/GameSetting/` 只有 GenshinHDRLuminanceSettingDialog.xaml.disabled（无 .xaml），src/Lunarward/Lunarward.csproj:94 `<Compile Remove="Fea…

### B40 · 切换账号写注册表失败只记 warning 就 return，上层按成功继续（静默失败）

`Features/GameAccount/GameAccountService.cs:L196-201`　置信度 medium　**✅ 成立**

- **现状**：账号切换失败时界面没有任何错误提示（看起来像切换成功），游戏启动后仍是旧账号；只能翻日志才能发现。

- **上游/对照**：`Features/GameAccount/GameAccountService.cs:L137-159` — ChangeGameAccountAsync 拿 WineRegistry.SetValuesAsync 的 bool，失败时 _logger.LogWarning 后 return（不抛）；GameAccountSwitcher.xaml.cs:92-100 的 ChangeGameAccountAsync 只是把 CanChangeGameAccount 置 false，既不刷新列表也不提示。上游 Registry.SetValue 失败会抛异常，被 GameAccountSwitcher 的 catch 记 error（至少日志里有异常栈）。

- **复核证据**：src/Lunarward/Features/GameAccount/GameAccountService.cs:196-201 `bool ok = await WineRegistry.SetValuesAsync(key, values, cancellationToken).ConfigureAwait(false); if (!ok) { _logger.LogWarning("Change account {name} ({biz}) 写注册表失败", account.Name, account.GameBiz); return; }`；WineRegistry.cs:265-272 `string? output = await RunAsync(…, "reg", "import", tempFile); return output is not null;` / `catch (Exception …

### B41 · 上游「每次启动游戏前把 AppConfig.EnableGenshinHDR 重写进注册表」的行为在移植版启动器里丢失

`Features/GameLauncher/GameLauncherService.Linux.cs:L189（CreateGameProcessStartInfo，整个方法无 HDR 相关调用）`　置信度 medium　**✅ 成立**

- **现状**：用户在设置页改过 HDR 后在游戏内又关掉，下次从 Lunarward 启动游戏时 HDR 不会按设置页的值恢复；首次启动前就勾上 HDR 的情况，值只写进注册表一次，之后不再同步。

- **上游/对照**：`Features/GameLauncher/GameLauncherService.cs:L379-382` — 上游构造启动参数时执行 if (gameId.GameBiz.Game is GameBiz.hk4e) GameSettingService.SetGenshinEnableHDR(gameId.GameBiz, AppConfig.EnableGenshinHDR);（L379-382）。移植侧重写的 GameLauncherService.Linux.cs 对 SetGenshinEnableHDR / EnableGenshinHDR 零引用（grep 整个 Features/GameLauncher 无命中），只有在 GameSettingPage.xaml.cs:544-545 点「套用」时写一次。此外上游这个调用是「每次启动强制对齐」的语义，保证用户在游戏内改了 HDR 也会被拉回 AppConfig 的设定。

- **复核证据**：上游 src/Starward/Features/GameLauncher/GameLauncherService.cs:379-382 `if (gameId.GameBiz.Game is GameBiz.hk4e) { GameSettingService.SetGenshinEnableHDR(gameId.GameBiz, AppConfig.EnableGenshinHDR); }`；移植侧同一方法 src/Lunarward/Features/GameLauncher/GameLauncherService.cs:379-380 把这段换成了注释（「上游在启动前用 winreg 写 HDR 开关…… HDR 改由设置页保存时写入目标前缀。」），src/Lunarward/Features/GameLauncher/GameLauncherService.Linux.cs 全文无 EnableGenshi…

### B42 · 页面被 NavigationCacheMode=Required 缓存后，切换游戏时 IsLanguageSettingEnable/IsGraphicsSettingEnable/StackPanel_StarRailFPS/StackPanel_GenshinHDR/StackPanel_Emoji 全部不重置，上一个游戏的分区残留

`Features/GameSetting/GameSettingPage.xaml.cs:L296-312（另 L293）`　置信度 medium　**✅ 成立**

- **现状**：先看原神设置页，再切到绝区零/星穹铁道：语音语言下拉、HDR 勾选与「HDR 亮度设置」按钮、星铁帧率下拉等上一个游戏的分区仍然可见可点；「游戏未安装」的 emoji 面板在装好游戏后也不会消失，会盖住设置表单。

- **上游/对照**：`Features/GameSetting/GameSettingPage.xaml.cs:L218-240` — InitializeGameSettingAsync 只会把标志与面板设成 true/Visible（L296 IsBaseSettingEnable=true、L299/L306/L311 各分支、L293 StackPanel_Emoji.Visibility=Visible），从来没有对应的「置 false / Collapsed」。上游每次导航都新建页面，默认值天然是 false，所以不需要重置；移植版给本页加了 NavigationCacheMode="Required"（D60），同一个实例会在「原神 → 星穹铁道 → 绝区零」之间复用，且本页没有 override OnGameChanged 做清理（对比 GameLauncherPage 的 GameBannerAndPost 就是这样修的，D60 追加记录）。

- **复核证据**：src/Lunarward/Features/GameSetting/GameSettingPage.xaml:2 `NavigationCacheMode="Required"`（上游 src/Starward/Features/GameSetting/GameSettingPage.xaml 无该属性，每次导航新建实例）；src/Lunarward/Features/GameSetting/GameSettingPage.xaml.cs:286-315 InitializeGameSettingAsync 只有 `StackPanel_Emoji.Visibility = Visibility.Visible;`（:293）、`IsBaseSettingEnable = true;`（:296）、`IsLanguageSettingEnable = true;`（:299）、`IsGraphicsSettingE…

### B43 · 写注册表失败被吞掉：WineRegistry.SetValuesAsync 返回 false 但 GameSettingService 不检查，页面照样提示「设置已应用」

`Features/GameSetting/GameSettingService.cs:L91-96、L269-277、L375-379`　置信度 medium　**✅ 成立**

- **现状**：wine reg import 失败（前缀锁、注册表损坏、wineserver 没起来等）时用户看到「设置已应用」，但游戏里没有变化；只有日志里有一行 [WineRegistry] 批量写入注册表失败。

- **上游/对照**：`Features/GameSetting/GameSettingService.cs:L82-116、L212-247、L289-306` — WineRegistry.SetValuesAsync（WineRegistry.cs:252-281）内部 catch 所有异常、失败只 Log.Warning 并返回 false；SetGameResolutionSettingAsync / SetStarRailFPSIndexAsync / SetGenshinHDRLuminanceAsync 忽略这个返回值（L94 直接把 Task 返回给调用方，L277/L379 await 后丢弃 bool）。上游 Registry.SetValue 失败会抛异常，GameSettingPage.ApplySetting 的 catch 会把 ex.Message 显示到 ErrorMessage。移植后 GameSettingPage.xaml.cs:549 依旧无条件 ErrorMessage = Lang.GameSettingPage_SettingNotEffect，等于把失败报成成功。

- **复核证据**：src/Lunarward/Features/GameAccount/WineRegistry.cs:252-281 `public static async Task<bool> SetValuesAsync(...)`：RunAsync 在 `process.ExitCode != 0` 时返回 null（:355-358）、15s 超时也返回 null（:350-351）→ :271 `return output is not null;`；catch 里 :274-278 `Log.Warning(ex, "[WineRegistry] 批量写入注册表失败：{Key}", key); return false;`。调用方丢返回值：src/Lunarward/Features/GameSetting/GameSettingService.cs:92-96 `=> values.Count == 0 ? Task…

### B68 · ApplySetting 先改 AppConfig.EnableGenshinHDR 再写注册表，写失败后内存与游戏内状态不一致

`Features/GameSetting/GameSettingPage.xaml.cs:L544-545`　置信度 low　**❌ 不成立**

- **现状**：HDR 写失败/被跳过时，设置页仍显示勾选为「已开启」，AppConfig 也认为开启；下次进页面 EnableGenshinHDR 从 AppConfig 读出 true，用户无从发现上次没写进去。

- **上游/对照**：`Features/GameSetting/GameSettingPage.xaml.cs:L435-436` — 上游顺序是 GameSettingService.SetGenshinEnableHDR(...) 成功后再 AppConfig.EnableGenshinHDR = EnableGenshinHDR（L435-436）；移植版把赋值提到 await 之前（L544-545）。如果 SetGenshinEnableHDRAsync 抛异常（或在无前缀时静默 return），AppConfig 已经被改成新值，而注册表没变，之后启动时（若上游的启动写入被恢复）会以新值覆盖，用户看到的勾选状态与游戏内实际不符。

- **复核证据**：上游 src/Starward/Features/GameSetting/GameSettingPage.xaml.cs:435-436 同样是先 `AppConfig.EnableGenshinHDR = EnableGenshinHDR;` 再 `GameSettingService.SetGenshinEnableHDR(CurrentGameBiz, EnableGenshinHDR);`；移植 src/Lunarward/Features/GameSetting/GameSettingPage.xaml.cs:544-545 为先 AppConfig 再 `await GameSettingService.SetGenshinEnableHDRAsync(...)`，顺序一致，仅异步化。；「上游 service 成功后才赋值」与代码不符，不是实现错位。真正缺失的是上游启动时 GameLauncherServ…

### B69 · HDR 开关在「前缀还不存在」时被静默丢弃（只写 Information 日志）

`Features/GameSetting/GameSettingService.cs:L284-301`　置信度 low　**✅ 成立**

- **现状**：全新安装的用户先勾 HDR 再启动游戏，游戏内 HDR 仍是关闭的，且没有任何提示；必须再回设置页点一次「套用」才生效。

- **上游/对照**：`Features/GameSetting/GameSettingService.cs:L249-260` — SetGenshinEnableHDRAsync 用 PrefixManager.TryGetExistingPrefix(profile, biz.Value) 解析「已经存在」的前缀，拿不到就 Serilog.Log.Information("SetGenshinEnableHDR: 找不到游戏前缀，跳过") 后 return；上游无条件写入。这个场景恰好是「首次启动前先在设置页勾 HDR」：前缀要等第一次启动（EnsurePrefix）才创建，此刻用户的操作被丢掉，页面上却照常显示「设置已应用」。

- **复核证据**：src/Lunarward/Features/GameSetting/GameSettingService.cs:284-301：:291 `string? prefix = winePrefix ?? ResolveGamePrefix(biz);`，为空则 :294 记日志并 `return;`，不写注册表；ResolveGamePrefix(:303-314) 经 ProfileBinding.Resolve + PrefixManager.TryGetExistingPrefix（PrefixManager.cs:335-339 `Directory.Exists(path) ? path : null`），游戏首启前前缀目录不存在即丢弃。上游 src/Starward/Features/GameSetting/GameSettingService.cs:249-260 无条件 `Registry.SetVa…

### B70 · HDR 亮度对话框既不回读已保存值，Reset/AutoAdjust 的默认值也与上游不一致（1000/200/400 vs 1000/300/350）

`Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs:L27-31、L42-47`　置信度 low　**◐ 部分成立**

- **现状**：（补 UI 后）打开对话框显示的不是当前已保存的亮度；点「重置」得到 1000/200/400 而不是上游/游戏的 1000/300/350；直接点保存会覆盖用户原来的值。

- **上游/对照**：`Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.cs:L52、L300-330` — 上游 Loaded 里 (MaxLuminance, SceneLuminance, UILuminance) = GameSettingService.GetGenshinHDRLuminance(CurrentGameBiz)（L52），Reset 设 1000/300/350（L300-307），AutoAdjust 用显示器 MaxLuminanceInNits 与 SdrWhiteLevelInNits clamp 后算 Scene=UI-50（L317-330）。移植版属性初值直接写成 Max=1000/UI=200/Scene=400（L27-31），Reset 也写同样三个值（L42-47），AutoAdjust => Reset()，且从不调用 GetGenshinHDRLuminanceAsync。当前因为对话框没有 UI（见另一条 bug）实际不可达，但一旦补上 XAML，会把用户已调好的亮度覆盖成默认值，且默认值与游戏内默认（300/350）不一致。

- **复核证据**：src/Lunarward/Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs:27/29/31 默认值 1000/200/400、:42-46 Reset 同值、:50 `private void AutoAdjust() => Reset();`，无 GetGenshinHDRLuminanceAsync 回读。上游 GenshinHDRLuminanceSettingDialog.xaml.cs:52 Loaded 回读、:122/:134/:146 默认 1000/300/350、:300-307 Reset 1000/300/350、:317-324 AutoAdjust 按显示器 info clamp。当前不可达：XAML 为 .xaml.disabled，.xaml.cs 被 src/Lunarward/Lunarward.csp…


## 截图与图库（9 条）

### B06 · 「多选」开关是空开关：MutliSelect 不再影响任何行为

`Features/Screenshot/ScreenshotPage.xaml:107-109`　置信度 high　**✅ 成立**

- **现状**：点「多选」按钮没有任何变化：复选框一直显示，勾选行为与批量转换的目标集合都不受开关控制。

- **上游/对照**：`Features/Screenshot/ScreenshotPage.xaml.cs:90-98` — 上游 MutliSelect setter 会切 GridView_Images.SelectionMode（Multiple/None），并决定批量转换传入哪些项。移植版里 MutliSelect 只有一个带 PropertyChanged 的自动属性（Linux.cs:39-51），XAML 中唯一的引用是 ToggleButton.IsChecked（:107-109），CheckBox 常驻显示、BatchConvert 也完全不看 MutliSelect，所以开关无论开还是关，交互完全一样。

- **复核证据**：src/Lunarward/Features/Screenshot/ScreenshotPage.Linux.cs:39-51 MutliSelect 只在 setter 内自赋值并发 PropertyChanged，全文件无读取点；src/Lunarward/Features/Screenshot/ScreenshotPage.xaml:108-109 唯一引用是 `IsChecked="{x:Bind MutliSelect, Mode=TwoWay}"`；条目模板 :80-84 的 CheckBox 无条件常显；BatchConvert()（ScreenshotPage.Linux.cs:188-207）只按 `x.IsSelected` 收集目标，从不读 MutliSelect。

### B07 · 缩略图点击不打开查看器：ItemClick 没有接线

`Features/Screenshot/ScreenshotPage.xaml:59-60`　置信度 high　**✅ 成立**

- **现状**：在图库里单击/双击任意缩略图都没有反应，看图窗口打不开，用户只能去文件管理器看截图。

- **上游/对照**：`Features/Screenshot/ScreenshotPage.xaml:52-58` — 上游用 GridView + IsItemClickEnabled="True" + ItemClick="GridView_Images_ItemClick"（handler 见 upstream .xaml.cs:380）。移植版换成 ItemsControl，XAML 里没有任何 ItemClick/IsItemClickEnabled/Tapped，而 ItemsControl 本身也没有 ItemClick 事件，所以 ScreenshotPage.Linux.cs:167 的 GridView_Images_ItemClick 永远不会被调用；ScreenshotViewerWindow 只剩 DevSwitches.ViewerIndex 这条开发路径能打开。

- **复核证据**：src/Lunarward/Features/Screenshot/ScreenshotPage.xaml:59-95 为 `<ItemsControl x:Name="ItemsControl_Images" ItemsSource="{x:Bind ScreenshotViewSource.View}">`，全文无 ItemClick/IsItemClickEnabled/Tapped/DoubleTapped，模板内只有 Border/StackPanel/CachedImage/CheckBox/TextBlock；ScreenshotViewerWindow 仅在 ScreenshotPage.Linux.cs:157（DevSwitches.ViewerIndex）与 :167-176 GridView_Images_ItemClick 中打开，后者无 XAML 接线，是死代码。

### B14 · 「自动复制截图文件」开关在 Linux 上完全不生效

`Features/Screenshot/ScreenCapture.Linux.cs:48-112`　置信度 high　**✅ 成立**

- **现状**：开着「自动复制截图文件」时截图不会进剪贴板，粘贴出来是旧内容；关闭它也没有任何区别。

- **上游/对照**：`Features/Screenshot/ScreenCaptureService.cs:420-430` — 该设置的唯一消费点是 ScreenCaptureService.CopyToClipboardAsync（:420-430，`if (AppConfig.AutoCopyScreenshotToClipboard)` 时 SetStorageItems）。ScreenCaptureLinux.CaptureAsync 只写文件 + 发通知，从不碰剪贴板；而设置页仍然展示并持久化这个开关（ScreenshotSetting.Linux.cs:22/36/50）。默认值还是 true（AppConfig.Setting.cs:348），所以用户默认就处于「承诺会复制、实际不复制」的状态。

- **复核证据**：Linux 捕获 src/Lunarward/Features/Screenshot/ScreenCapture.Linux.cs:48-112 CaptureAsync 只写文件 + DesktopNotification，无剪贴板。唯一消费点 src/Lunarward/Features/Screenshot/ScreenCaptureService.cs:420-430 `if (AppConfig.AutoCopyScreenshotToClipboard)` → `ClipboardHelper.SetStorageItems(...)`，但 CopyToClipboardAsync 只在 Windows 捕获路径 :97/:102 被调用（CaptureInternal，依赖 RunningGameService + Windows.Graphics.Capture）。Linux 三个真实入口都走 Scr…

### B15 · 未选中任何截图时「批量转换」会把全部截图转成 JPEG

`Features/Screenshot/ScreenshotPage.Linux.cs:188-203`　置信度 high　**✅ 成立**

- **现状**：图库里什么都没勾就点「批量转换」，会把整个截图目录的 PNG 全部额外写出一份 .jpg，一次点击即产生几十上百个文件，且无法撤销。

- **上游/对照**：`Features/Screenshot/ScreenshotPage.xaml.cs:340-351` — 上游 OpenImageBatchConvertWindow 传的是 MutliSelect ? SelectedItems : []，即没选就传空列表，由 ImageBatchConvertWindow 让用户自己加文件、选格式/命名/覆盖策略后再开始。移植版在 targets.Count == 0 时改成 targets = items（全部截图），直接同步跑 ScreenshotConverter.ConvertToJpeg（固定 quality 92、同名 .jpg、不删原图），没有任何确认或预览。

- **复核证据**：src/Lunarward/Features/Screenshot/ScreenshotPage.Linux.cs:188-203 BatchConvert：:191 `var targets = items.Where(x => x.IsSelected).ToList();`、:194 `targets = items;`（无选中直接全部）、:201 `var (ok, skip, fail) = ScreenshotConverter.ConvertToJpeg(targets.Select(x => x.FilePath));`，全程无确认、无预览、无选项；命令接线 :18 `OpenImageBatchConvertWindowCommand = new AsyncRelayCommand(BatchConvert)`、:127 触发，按钮 src/Lunarward/Features/Screenshot…

### B16 · 图库只在构造时加载一次，Alt+D 截图或切换目录后不刷新

`Features/Screenshot/ScreenshotPage.Linux.cs:20`　置信度 high　**✅ 成立**

- **现状**：按 Alt+D 截完图回到图库看不到新图；在设置页改了截图文件夹再回来，列表仍是旧目录的内容；必须重启应用才更新。

- **上游/对照**：`Features/Screenshot/ScreenshotPage.xaml.cs:60-64` — 上游 override OnLoaded → Task.Delay(16) → Initialize()，并且每个目录挂 FileSystemWatcher（:116-263），新建/删除文件都会即时增删条目。移植版在构造函数里调一次 LoadImages()（L20→L75-111），之后既不 override OnLoaded 也没有 watcher；XAML 又设了 NavigationCacheMode="Required" 复用页面实例，所以再次导航回来连构造器都不会跑。PageBase.cs:37-40 每次 Loaded 都会调 OnLoaded()，本来只要补一个 override 就能对齐上游。

- **复核证据**：src/Lunarward/Features/Screenshot/ScreenshotPage.Linux.cs:20 构造函数内 `LoadImages();`，LoadImages 定义 :75-114（每次读 ScreenCaptureLinux.DefaultFolder），全类仅此一次调用；无 OnLoaded/OnGameChanged override，类内无 FileSystemWatcher（watcher 只出现在已从编译排除的 ScreenshotPage2.xaml.cs:102/:201，src/Lunarward/Lunarward.csproj:117 `<Compile Remove="Features/Screenshot/ScreenshotPage2.xaml.cs" />` 对应）。src/Lunarward/Features/Screenshot/ScreenshotPag…

### B17 · ScreenshotQuality=95 被绑到三段式 SelectedIndex（值域错位）

`Features/Setting/ScreenshotSetting.Linux.cs:20`　置信度 high　**✅ 成立**

- **现状**：「图像格式和质量」里的质量三档初始没有任何一段处于选中态；选完离开页面也不会保存，下次进来又回到未选中。

- **上游/对照**：`Features/Setting/ScreenshotSetting.xaml.cs:223-234` — 上游 ScreenshotQuality 初值是 AppConfig.ScreenCaptureEncodeQuality（0=Middle/1=High/2=Lossless），并且就是作为 Segmented.SelectedIndex 用的（screen XAML 三个 SegmentedItem，ScreenshotSetting.xaml:116-121）；ScreenCaptureService.cs:334-340 也按 0/1/2 映射到 80/90/100。移植版把 ScreenshotQuality 硬编码成 95（一个 JPEG 质量值），同时仍在 XAML 里绑 SelectedIndex，越界值会被 Selector 钳成 -1（或直接不选中任何一段）。此外该字段在 OnNavigatedFrom 里也不落盘。

- **复核证据**：src/Lunarward/Features/Setting/ScreenshotSetting.Linux.cs:20 `public int ScreenshotQuality { get; set; } = 95;`，被 src/Lunarward/Features/Setting/ScreenshotSetting.xaml:116-123 `SelectedIndex="{x:Bind ScreenshotQuality, Mode=TwoWay}"` 当索引用（三个 SegmentedItem：Medium/High/Lossless），95 越界 → 无段选中；同文件 :45-55 OnNavigatedFrom 只写 ScreenshotFolder 与 AutoCopyScreenshotToClipboard，注释写明「格式/质量只有 Windows 的 DXGI 捕获路径会用到…先不落盘」，故从…

### B45 · 批量转换同步跑在 UI 线程，截图多时界面卡死

`Features/Screenshot/ScreenshotPage.Linux.cs:201`　置信度 medium　**✅ 成立**

- **现状**：点「批量转换」后窗口无响应（标题栏「未响应」），期间没有任何进度提示，只能等。

- **上游/对照**：`Features/Screenshot/ImageBatchConvertWindow.xaml.cs:853-909` — ScreenshotConverter.ConvertToJpeg 是同步方法（逐张 SkiaSharp 解码 + JPEG 编码，ScreenshotConverter.cs:13-57），移植版在按钮命令里直接调用（L201），没有任何 await/Task.Run 或进度反馈；上游在 ImageBatchConvertWindow 里是异步逐项转换并刷新进度。按 2560×1440 PNG 估算，每张解码+编码数百毫秒，几十张就会冻结数秒到数十秒。

- **复核证据**：src/Lunarward/Features/Screenshot/ScreenshotPage.Linux.cs:18 `OpenImageBatchConvertWindowCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(BatchConvert);`，BatchConvert 在 :201 直接 `var (ok, skip, fail) = ScreenshotConverter.ConvertToJpeg(targets.Select(x => x.FilePath));` 后弹 toast，没有 await / Task.Run / 进度；src/Lunarward/Features/Screenshot/ScreenshotConverter.cs:13-57 是同步 foreach（`Starward.Helpers.ImageDecode…

### B46 · 图库扩展名白名单缩到 3 种，avif/jxl/webp/heic/jxr 截图不显示

`Features/Screenshot/ScreenshotPage.Linux.cs:84-86`　置信度 medium　**✅ 成立**

- **现状**：截图目录里明明有 .avif/.jxl/.webp 文件，图库却是空的（并显示「还没有截图」），只能去文件管理器找。

- **上游/对照**：`Features/Screenshot/ScreenshotHelper.cs:15-22` — 上游 IsSupportedExtension/WatcherFilters 认 .jpg/.png/.jxr/.webp/.heic/.avif/.jxl 七种，枚举时逐个判断。移植版只用 EndsWith(".png"/".jpg"/".jpeg") 过滤（L84-86），其余格式即使存在于截图目录也不会成为条目；这批文件包含移植版自己「批量转换」可能产生/上游历史遗留的格式。

- **复核证据**：src/Lunarward/Features/Screenshot/ScreenshotPage.Linux.cs:84-86 `.Where(x => x.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || x.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))`，空列表时 :120-121 `ScreenshotViewSource.View = items; HasImages = items.Count > 0;`（空态文案「还没有截图」）；上游 src/Starward/Features/Screenshot/ScreenshotHelper.cs:15-22 `return …

### B47 · 「清除缩略图缓存」删的是不存在的目录，却提示已清理

`Features/Setting/ScreenshotSetting.Linux.cs:88`　置信度 medium　**◐ 部分成立**

- **现状**：点「清除缩略图缓存」永远显示成功，但 <cache>/thumb 下的缩略图一张没删，磁盘占用不变。

- **上游/对照**：`Features/Setting/ScreenshotSetting.xaml.cs:431-441` — 上游调用 ImageThumbnail.ClearThumbnailCache()，缓存目录是 <CacheFolder>/thumb（ImageThumbnail.cs:31 上游；移植版同一行改成了 AppConfig.CacheDirectory/thumb）。移植版的按钮却拼 <AppConfig.CacheFolder>/screenshot-thumb，两个路径（根目录与子目录名）都与真实缓存不一致，Directory.Exists 为 false 时直接跳过删除，然后无条件 toast「已清理」。

- **复核证据**：src/Lunarward/Features/Setting/ScreenshotSetting.Linux.cs:88 `string cache = System.IO.Path.Combine(AppConfig.CacheFolder ?? System.IO.Path.GetTempPath(), "screenshot-thumb");`、:89-92 `if (System.IO.Directory.Exists(cache)) { System.IO.Directory.Delete(cache, true); }`，之后 :93 无条件 `Helpers.InAppToast.MainWindow?.Success("已清理");`；上游 src/Starward/Features/Setting/ScreenshotSetting.xaml.cs:435 `ImageThumbnail.Clear…


## 服务与帮助器（10 条）

### B08 · 「忽略此版本」不落盘，同一版本会反复弹更新窗

`Features/Update/UpdateWindow.Linux.cs:54`　置信度 high　**✅ 成立**

- **现状**：点「忽略此版本」关窗后，最多 6 小时（跨日后 MainView 的 `now - _lastShowUpdateTime > 6h && now.Date != _lastShowUpdateTime.Date` 条件再次满足）又会弹出同一个版本的更新窗，用户无法屏蔽。

- **上游/对照**：`Features/Update/UpdateWindow.xaml.cs:455` — 上游 `IgnoreThisVersion()` 写 `AppConfig.IgnoreVersion = NewVersion.Version;`（NewVersion 为 null 时写 `AppConfig.LastAppVersion = AppConfig.AppVersion`），MainView 的 `CheckUpdateAsync(false)` 据此过滤；移植只有 `private void IgnoreThisVersion() => Close();`，而且 Features/Update/UpdateService.Linux.cs:25 `CheckUpdateAsync(bool disableIgnore = false) => await GetLatestVersionAsync();` 把参数丢掉、全程无人读 IgnoreVersion。

- **复核证据**：src/Lunarward/Features/Update/UpdateWindow.Linux.cs:54 `private void IgnoreThisVersion() => Close();`；src/Lunarward/Features/Update/UpdateService.Linux.cs:25-26 `public async Task<ReleaseInfoDetail?> CheckUpdateAsync(bool disableIgnore = false) => await GetLatestVersionAsync();` 丢弃参数且不看 IgnoreVersion。命中的 UpdateWindow.xaml.cs:455 / UpdateService.cs:80 均在 src/Lunarward/Lunarward.csproj:133-134 `<Compile Remove>` …

### B09 · 可用空间永远取根文件系统的，不是目标路径所在卷

`Helpers/DriveHelper.Linux.cs:97`　置信度 high　**◐ 部分成立**

- **现状**：游戏装在 /mnt/games 等非根挂载时：安装页/预下载页显示的「可用空间」是根分区的；Features/GameInstall/Sophon/UpdateOrchestrator.cs:310 `EnsureEnoughSpace` 会拿这个数字比较，根分区剩余不足而目标盘充足时直接抛 IOException「磁盘空间不足…」中止安装/更新，反过来目标盘已满时又放行、下到一半失败。

- **上游/对照**：`Helpers/DriveHelper.cs:89` — 上游 `Kernel32.GetDiskFreeSpaceEx(root, ...)`，root 由 `GetPathRoot(path)` 归一化成盘符根；移植改成 `var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)) ?? "/"); return drive.AvailableFreeSpace;`。Linux 上 Path.GetPathRoot 对任何绝对路径都返回 "/"，于是 / 的可用空间被当成目标卷的可用空间（同文件 GetDriveType 则正确地做了最长挂载匹配，两条路径不一致正是证据）。

- **复核证据**：src/Lunarward/Helpers/DriveHelper.Linux.cs:97-98 `var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path)) ?? "/"); return drive.AvailableFreeSpace;`；本机实测 GetPathRoot("/run/lunarward-test/Game") = "/"，取到根卷 536498950144 B，而 `new DriveInfo("/run")` 为 5004386304 B。src/Lunarward/Features/GameInstall/Sophon/UpdateOrchestrator.cs:310-316 EnsureEnoughSpace 用该值比较并抛 IOException（调用点 InstallPipeline.cs:107/:291、…

### B27 · 硬链接去重键退化成「路径哈希」，多路径硬链接不再合并

`Features/GameSelector/GameSelector.xaml.cs:953`　置信度 high　**✅ 成立**

- **现状**：游戏管理页的「实际占用 / 已节省空间」失真：米哈游多个游戏共用资源（硬链接）时实际占用偏大、节省空间显示 0。可用 Helpers/PosixIdentity.cs 的 stat 拿 (st_dev, st_ino) 等价修复。

- **上游/对照**：`Features/GameSelector/GameSelector.xaml.cs:863-866` — 上游打开文件句柄取 `FILE_ID_INFO`（卷序列号 + 128bit FileId），因此同一 inode 被多个游戏目录硬链接引用时 dic 只算一份；移植改成 `var key = $"{file.Length}_{file.LastWriteTimeUtc.Ticks}_{file.FullName.GetHashCode():X8}";`，硬链接在不同路径下键必然不同，dic 不再合并。注释写的是「大小+修改时间+inode 推断」，实际没有任何 inode 来源。

- **复核证据**：GameSelector.xaml.cs:953 `var key = $"{file.Length}_{file.LastWriteTimeUtc.Ticks}_{file.FullName.GetHashCode():X8}";`、:954 `dic[key] = file.Length;`（:952 注释自称含 inode，实际没有 inode 来源）；上游 Starward/Features/GameSelector/GameSelector.xaml.cs:863-866 用 FILE_ID_INFO（卷序列号 + 128bit FileId）作 key，硬链接只算一份。

### B28 · 更新窗里的「发布页面 / 完整包」超链接点了没反应

`Features/Update/UpdateWindow.LinuxAuto.cs:16`　置信度 high　**✅ 成立**

- **现状**：UpdateWindow 中「发布页面」「完整包下载」两个 HyperlinkButton 点击无任何反应（无报错、无日志）。

- **上游/对照**：`Features/Update/UpdateWindow.xaml.cs:162-180` — 上游 `HyperlinkButton_Click` 按 `fe.Tag`（"release" → GitHub release tag 页，"package" → NewVersion.PackageUrl）拼 URL 后 `Launcher.LaunchUriAsync`；移植树里这个 handler 是 M1 编译清零阶段自动生成的空实现（文件头写明「自动生成，勿手工编辑」，与本批的 UpdateWindow.Linux.cs 分离），从未改成 Helpers/OpenUrl.Open。XAML 侧 `Click="HyperlinkButton_Click"` 与 `Visibility="{x:Bind local:UpdateWindow.StringToVisibility(NewVersion.PackageUrl)}"` 都还在，按钮可见但无效。

- **复核证据**：src/Lunarward/Features/Update/UpdateWindow.LinuxAuto.cs:16 `private void HyperlinkButton_Click(object sender, RoutedEventArgs e) { }` 为空；UpdateWindow.xaml:97 与 :101 两个 HyperlinkButton 的 Click 仍指向它；真正的上游实现在 UpdateWindow.xaml.cs:162-181（fe.Tag 为 release 拼 GitHub tag 页、package 用 NewVersion.PackageUrl，再 Launcher.LaunchUriAsync），但该文件被 Lunarward.csproj:134 排除编译，生效实现是 UpdateWindow.Linux.cs + LinuxAuto.cs。

### B29 · 复制文件到剪贴板写的是纯路径文本，不是 text/uri-list

`Helpers/ClipboardHelper.Linux.cs:16`　置信度 high　**✅ 成立**

- **现状**：壁纸页/启动器页右键「复制文件」后到 Nautilus/Dolphin 里 Ctrl+V 没有任何文件（以文本粘贴时得到一串裸路径）。

- **上游/对照**：`Helpers/ClipboardHelper.cs:63-70` — `SetStorageItems` 注释写「把若干文件的本地路径以 URI 列表形式放进剪贴板（文件管理器可识别）」，实现却是 `SetText(string.Join("\n", paths))`，交给 wl-copy/xclip 的都是 text/plain；文件管理器要求 `text/uri-list`（且元素是 `file://` URI）才认作文件。上游用 `data.SetStorageItems(items)` 走结构化剪贴板。

- **复核证据**：src/Lunarward/Helpers/ClipboardHelper.Linux.cs:11-18 SetStorageItems 只取 `x.Path` 后 `SetText(string.Join("\n", paths))`；SetText :20-59 依次尝试 wl-copy -- / xclip -selection clipboard / xsel --clipboard --input，写入的 MIME 是 text/plain，没有 text/uri-list、也没有 file:// URI；上游 Starward/Helpers/ClipboardHelper.cs:63-71 `data.SetStorageItems(items); Clipboard.SetContent(data);` 走结构化剪贴板。

### B57 · 深链不再处理 playtime/test，`lunarward://playtime/<biz>?pid=N` 静默无操作

`App.xaml.cs:306`　置信度 medium　**✅ 成立**

- **现状**：外部程序或快捷方式执行 `lunarward://playtime/hk4e_cn?pid=1234` 时只把启动器窗口叫出来，不记录游玩时长；`startgame?...&install_path=...` 的安装路径参数同样被忽略（多实例启动时）。

- **上游/对照**：`Features/UrlProtocol/UrlProtocolService.cs:80-95` — 上游 `HandleUrlProtocolAsync` 支持三个 host：startgame（含 `install_path` 查询参数）、playtime（取 pid 后 `StartProcessToLogAsync(gameId, pid)`，没有 pid 时走名字轮询版）、test。移植把深链搬到 `App.ExpandUrlArgument`，`if (host is not "startgame") return args;`，未知 host 原样作为 argv[0] 返回；`HandleStartupAsync` 只在 args[0] 是字面量 rpc/playtime/startgame 时分支，于是 URL 落进普通启动流程，`SingleInstancePipe.NotifyPrimary` 也只会发 `show`。

- **复核证据**：src/Lunarward/App.xaml.cs:290-306 `internal static string[] ExpandUrlArgument(string[] args)` 中 `if (host is not "startgame") { return args; }`，且只解析 biz：`return biz is null ? ["startgame"] : ["startgame", "--biz", biz];`；:248 `args = ExpandUrlArgument(args);`，:250-272 只对字面量 `args[0].ToLower() is "rpc"/"playtime"/"startgame"` 分支，playtime 分支只读 `--pid`/`--biz`；SingleInstancePipe.cs:37-44 `args = App.ExpandUrlArgum…

### B58 · 会话结束按「整个前缀的 wineserver」判定，同前缀还有别的 wine 程序时时长继续增长

`Features/PlayTime/PlayTimeSession.Linux.cs:44-72`　置信度 medium　**✅ 成立**

- **现状**：同一 WINEPREFIX 下还有别的 wine 程序在跑（米游社/其它启动器/手工开的 exe）时，退出游戏后游玩时长会一直记到那个程序退出；伴随启动（RunExitCompanions）与前缀解锁同样被拖后。

- **上游/对照**：`Features/PlayTime/PlayTimeRecordService.cs:52-59` — `WaitForWineProcessAsync` 用 `HasWineProcessForPrefix(prefix)`（扫 /proc/*/environ 里 `WINEPREFIX=<prefix>` 的任意进程）判断会话已启动，随后 `wineserver -w`（env WINEPREFIX=prefix）等的是该前缀的 wineserver 退出，而不是本次启动的游戏进程树；上游等价条件是 `process.HasExited`，只与这一局游戏有关。前缀锁（GameLauncherService.Linux.cs:172 的 ReleasePrefixLock + RunExitCompanions）复用了同一判定。

- **复核证据**：src/Lunarward/Features/PlayTime/PlayTimeSession.Linux.cs:41-76 构造 `wineserver -w` 时 :68 `process.StartInfo.Environment["WINEPREFIX"] = prefix;`，:71 `await process.WaitForExitAsync(cancellationToken);`（等整个前缀的 wineserver 退出）；PlayTimeRecordService.cs:86-100 `Task session = PlayTimeSession.WaitForSessionEndAsync(pid, prefix, cts.Token); while (!session.IsCompleted) {...}` 以其完成作为会话终点并落 Stop；GameLauncherService.Linux…

### B59 · 更新窗打开时不加载更新说明，也不显示「需手动下载」提示

`Features/Update/UpdateWindow.LinuxAuto.cs:17`　置信度 medium　**✅ 成立**

- **现状**：打开更新窗只有版本号/构建时间，更新内容区域永远空白；上游标记为「必须手动下载」的版本也不会给出任何提示（WebView2 缺失是引擎缺口，但 DisableAutoUpdate 分支与错误信息本可以照搬）。

- **上游/对照**：`Features/Update/UpdateWindow.xaml.cs:123-135` — 上游 `RootGrid_Loaded` 做三件事：`NewVersion?.DisableAutoUpdate` 时置 `IsUpdateNowEnabled = false` 并给 `ErrorMessage = Lang.UpdatePage_YouNeedToManuallyDownloadTheNewVersionPackage`、`UpdateService.UpdateFinished` 时走 Finish、`_ = LoadUpdateContentAsync()` 渲染 changelog。移植是空 handler；同时 UpdateWindow.Linux.cs 的 `IsUpdateNowEnabled => false` 恒假、ErrorMessage 是无通知的普通属性（类上没有 INotifyPropertyChanged），即使赋值也不会刷新。

- **复核证据**：src/Lunarward/Features/Update/UpdateWindow.LinuxAuto.cs:17 `private void RootGrid_Loaded(object sender, RoutedEventArgs e) { }`（空实现）；UpdateWindow.Linux.cs:23 `public string? ErrorMessage { get; set; }`、:33 `public bool IsUpdateNowEnabled => false;`，类上无 INotifyPropertyChanged；UpdateWindow.xaml:14 `Loaded="RootGrid_Loaded"`，右侧 changelog 区只有 `<ProgressRing ... IsActive="True" />` 与 Collapsed 的 WebView2；Lunarward.c…

### B60 · 复制文本在 UI 线程同步拉起外部进程并阻塞最多 2s

`Helpers/ClipboardHelper.Linux.cs:47`　置信度 medium　**✅ 成立**

- **现状**：点「复制」后界面最多卡 2~6s（常见于无剪贴板守护、或 wl-copy 在不带 Wayland socket 的会话里挂住时）。

- **上游/对照**：`Helpers/ClipboardHelper.cs:12-21` — `SetText` 由按钮点击直接在 UI 线程调用，内部 `Process.Start(psi)` + `p.StandardInput.Write(text)` + `p.WaitForExit(2000)` 全同步；每个候选（wl-copy → xclip → xsel）最多等 2s，最坏情况三个候选依次超时才返回。上游 `Clipboard.SetContent` 是同步内存操作（微秒级）。`grep -rn "ClipboardHelper.SetText"` 的调用点（GachaLogPage.xaml.cs:850、GameRecordPage.xaml.cs:384、GameLauncherSettingDialog.xaml.cs:1121/1130 等）都在点击处理器里。

- **复核证据**：src/Lunarward/Helpers/ClipboardHelper.Linux.cs:26-51 依次尝试 wl-copy/xclip/xsel：:40 `using var p = Process.Start(psi);` → :45-46 `p.StandardInput.Write(text); p.StandardInput.Close();` → :47 `p.WaitForExit(2000);`，全同步；调用点 GameRecordPage.xaml.cs:384、GachaLogPage.xaml.cs:850、GameLauncherSettingDialog.xaml.cs:1121/:1130 都直接在点击/菜单处理器里。上游 src/Starward/Helpers/ClipboardHelper.cs:12-21 走 `Clipboard.SetContent(...)`，是同步内存操作。

### B76 · 进程已退出时用「最近一次 Start 事件」当起点，pid 复用会把旧会话算进本次

`Features/PlayTime/PlayTimeRecordService.cs:67-77`　置信度 low　**◐ 部分成立**

- **现状**：启动器被强杀后重新记录同一游戏时，单次游玩时长可能虚高（上限 12 小时），并写进 PlayTimeStats（同一 StartTime/Pid 有唯一索引，不会重复行，但值是错的）。

- **上游/对照**：`Features/PlayTime/PlayTimeRecordService.cs:48-49` — `catch (ArgumentException)` 分支调 `GetLastStartTime(biz, pid)`，SQL 只按 `GameBiz=@biz AND Pid=@pid ORDER BY TimeStamp DESC LIMIT 1` 取最近一条 Start，唯一的合理性检查是「>12 小时视为 stale」。wine/Proton 的 loader 进程短命时必然走这条分支，而 Linux 的 pid 复用很常见，取到的 Start 可能属于上一次会话（甚至在 12 小时窗口内属于别的进程）。

- **复核证据**：src/Lunarward/Features/PlayTime/PlayTimeRecordService.cs:55-76：`catch (ArgumentException)` 分支 `DateTime? lastStart = GetLastStartTime(biz, pid);`，唯一的合理性检查是 :69-72 `DateTime.Now - stale > TimeSpan.FromHours(12)`，然后 :73-75 `startTime = lastStart ?? DateTime.Now;` 并按该时间 Log(Start)；GetLastStartTime（:144-160）SQL 为 `SELECT * FROM PlayTimeItem WHERE GameBiz = @biz AND Pid = @pid ORDER BY TimeStamp DESC LIMIT 1;`，不筛 St…


## 外壳与应用生命周期（4 条）

### B12 · --hide 自启时无条件隐藏窗口，不检查托盘是否可用，无托盘桌面会变成没有入口的隐形进程

`App.xaml.cs:183`　置信度 high　**✅ 成立**

- **现状**：在没有 StatusNotifierWatcher 的会话（部分 Wayland 合成器、纯 X11 无托盘）用 --hide 自启（scripts/test/perf.sh:91 即如此调用）时，进程在跑但没有任何窗口或托盘入口，只能 lunarward --show 捞回；而同一会话里点关闭按钮反而会直接退出，两种行为不一致。

- **上游/对照**：`App.xaml.cs:94` — 上游 --hide 只创建 SystemTrayWindow（Windows 托盘必然存在），主窗口不建。移植改成创建 MainWindow 后 Task ignoredHide = HideAfterShownAsync(m_MainWindow)（App.xaml.cs:183），而 HideAfterShownAsync（:229-241）只有 await Task.Delay(800); window.Hide();，完全不检查 Features.Tray.StatusNotifierTray.Available。而同文件 AppWindow_Closing（:236）与 StartTrayAsync（:343-360）都已按「托盘不可用 → 不要隐藏 / 直接退出」处理，两处策略自相矛盾。

- **复核证据**：src/Lunarward/App.xaml.cs:169 `if (Environment.GetCommandLineArgs().Contains("--hide"))`；:181-183 `m_MainWindow = new MainWindow(); m_MainWindow.Activate(); System.Threading.Tasks.Task ignoredHide = HideAfterShownAsync(m_MainWindow);`；HideAfterShownAsync :229-241 函数体只有 :233 `await Task.Delay(800);` 与 :234 `window.Hide();`，不检查 Features.Tray.StatusNotifierTray.Available。对照 src/Lunarward/Features/ViewHost/MainWind…

### B13 · 退出分支调用 AppInstance.UnregisterKey()：Uno 下必抛 NotImplementedException，被 catch 吞掉后备份与退出都不执行

`Features/ViewHost/MainWindow.xaml.cs:267`　置信度 high　**✅ 成立**

- **现状**：关窗选「退出」时只关掉窗口：数据库自动备份与 App.Current.Exit() 被跳过，退出路径退化为普通关窗；设置页「更改数据目录 / 删除所有设置」点击后旧进程不退出，新起的进程被单实例锁驳回，表现为点了没反应或重启失败。

- **上游/对照**：`Features/ViewHost/MainWindow.xaml.cs:133` — MainWindow.xaml.cs:267 在「关闭窗口 → 退出」分支里调用 AppInstance.GetCurrent().UnregisterKey()。Uno 的 Microsoft.Windows.AppLifecycle 只是投影桩：实测直接引用 uno.winrt 6.7.135 的 Uno.dll 调用 GetCurrent() 抛 System.NotImplementedException: The member GetCurrent() is not implemented（https://aka.platform.uno/notimplemented#...AppInstance.GetCurrent%28%29）。该调用位于外层 try/catch{ } 内，异常被吞后同一块里的 Task.Run(DatabaseService.AutoBackupToAppDataLocal) 与 App.Current.Exit() 都不再执行。同样的 UnregisterKey 模式还在 src/Lunarward/Features/Setting/FileManageSetting.xaml.cs:76（更改数据目录）与 :133（删除所有设置），两处本应在之后 Process.Start(StarwardExecutePath) + App.Current.Exit() 完成重启。

- **复核证据**：src/Lunarward/Features/ViewHost/MainWindow.xaml.cs:262-268 退出分支 `Close(); AppInstance.GetCurrent().UnregisterKey(); Task backupTask = Task.Run(DatabaseService.AutoBackupToAppDataLocal); … await Task.WhenAny(backupTask, timeTask); App.Current.Exit();`，整段在 :229 起的 try/catch{} 里，UnregisterKey 抛异常后备份与 Exit 都被跳过。同模式 src/Lunarward/Features/Setting/FileManageSetting.xaml.cs:76-78（ChangeUserDataFolderAsync：先 `AppConfig…

### B44 · 新入口没有注册 AppDomain.UnhandledException，崩溃时不再落盘 crash 日志

`Program.Linux.cs:18`　置信度 medium　**✅ 成立**

- **现状**：非 UI 线程或启动早期崩溃时只剩 .NET 默认 stderr / 进程退出码，DiagnosticBundle 与用户反馈拿不到异常堆栈，排错信息出现缺口。

- **上游/对照**：`Program.cs:24` — 上游 Program.cs:24 注册 AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException，:36 把 e.Exception 与 Exception.Data 追加写入 AppConfig.LogFile。Program.Linux.cs 全文没有 UnhandledException 处理，只剩 App.xaml.cs:35 的 XAML 线程 App_UnhandledException，因此 CLI 子命令、后台线程、以及 host.Run() 之前的启动早期崩溃都没有日志落盘。

- **复核证据**：入口 src/Lunarward/Program.Linux.cs:18 `public static void Main(string[] args)`（整个类无 UnhandledException 处理），末尾 :1820-1827 `var host = UnoPlatformHostBuilder.Create().App(() => new Starward.App()).UseX11().UseLinuxFrameBuffer().UseWin32().Build(); host.Run();`；上游处理器 src/Lunarward/Program.cs:24 `AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;`、:36-58 把 e.Exception 写进 AppConfig.LogFil…

### B71 · 单实例监听早于主窗口创建，窗口期内的第二实例回调会先建一个窗口，随后又被 OnLaunched 覆盖，形成两个主窗口

`App.xaml.cs:152`　置信度 low　**❌ 不成立**

- **现状**：双击图标快速连点启动时偶发两个主窗口同时存在；关闭其中一个后应用可能未退出（另一个仍在），任务栏/托盘出现重复图标。

- **上游/对照**：`App.xaml.cs:94` — App.xaml.cs:152 先 SingleInstancePipe.StartListening(onShow: EnsureMainWindow + Activate, ...)，:169-191 才创建 m_MainWindow（:181 / :188）。EnsureMainWindow 使用 m_MainWindow ??= new MainWindow()。若第二个实例恰在这个间隙连上（连点两次启动图标即可），onShow 回调先建窗口 A 并赋值给字段，随后本实例的 OnLaunched 又执行 m_MainWindow = new MainWindow()，字段被窗口 B 覆盖，窗口 A 失去引用。两个 X11 窗口、两次 EnsureSystemTray/托盘注册，而 Exit()（:420 起的 m_MainWindow?.Close()）只关得掉 B。

- **复核证据**：src/Lunarward/App.xaml.cs:130 `DispatcherQueue uiDispatcher = ...GetForCurrentThread();`，:131-150 OnUi() 在非 UI 线程一律走 `uiDispatcher.TryEnqueue(...)`；:152-168 StartListening 的 onShow 是 `() => OnUi(() => { EnsureMainWindow(); m_MainWindow?.Activate(); })`，而 :168-190 中间没有任何 await，UI 线程一直停在 OnLaunched 里，排队项只能在 :188 `m_MainWindow = new MainWindow();` 之后执行，:331 `m_MainWindow ??= new MainWindow();` 因此永远看到非空字段，不会产生窗口 A；…


## 抽卡/战绩/自助查询（8 条）

### B18 · 抽卡页按游戏置位的 x:Load 开关只置位不复位，统计卡/物品统计面板跨游戏残留

`Features/Gacha/GachaLogPage.xaml.cs:72`　置信度 high　**✅ 成立**

- **现状**：先打开原神抽卡页、再切到星穹铁道/绝区零：同一 Grid 单元格里的 GridView_GachaItemStats_Genshin / _StarRail / _ZZZ 会同时处于加载状态，抽卡物品统计面板出现两套稀有度底色/两列卡片叠在一起；回到原神页还带着绝区零的统计卡（IsZZZGachaStatsCardVisible 恒 true）。

- **上游/对照**：`Features/Gacha/GachaLogPage.xaml.cs:72` — OnNavigatedTo（:59-105）只在命中当前游戏时把 EnableGenshinGachaItemStats / EnableStarRailGachaItemStats / EnableZZZGachaItemStats / IsZZZGachaStatsCardVisible 置 true（:72、:78、:84-85），XAML 用 `x:Load="{x:Bind ...}"`（GachaLogPage.xaml:326/342/365/393/426，页面 x:DefaultBindMode="OneWay"），全仓没有任何代码把这些属性复位成 false（grep 计数 0；属性 setter 只在 :253-257）。上游不需要复位——每次导航都新建页面实例，XAML 默认值即干净初始态；移植页加了 NavigationCacheMode="Required"（D60），实例常驻，于是置位会累积。

- **复核证据**：src/Lunarward/Features/Gacha/GachaLogPage.xaml.cs:71-72 `EnableGenshinGachaItemStats = true;`、:77-78 `EnableStarRailGachaItemStats = true;`、:83-85 `EnableZZZGachaItemStats = true; IsZZZGachaStatsCardVisible = true;`，属性定义 :253-257 仅 SetProperty，全仓无任何复位为 false 的写入。XAML src/Lunarward/Features/Gacha/GachaLogPage.xaml:2 `NavigationCacheMode="Required"`，:326/:342/:365/:393/:426 `x:Load="{x:Bind …}"`（页 x:DefaultBindMo…

### B19 · 服务器分支的菜单项 Visibility 只置 Visible 不复位，国际服残留米游社同步入口

`Features/Gacha/GachaLogPage.xaml.cs:91`　置信度 high　**✅ 成立**

- **现状**：从国服游戏切到国际服游戏后，抽卡页菜单里同时出现「从米游社同步」和「从 HoYoLAB 同步」两组入口；反向切换时云游戏入口仍然消失（Collapsed 残留）。

- **上游/对照**：`Features/Gacha/GachaLogPage.xaml.cs:59` — OnNavigatedTo 按 CurrentGameBiz 分支置 Visible：nap_cn/nap_bilibili → MenuFlyoutItem_SyncFromMiyoushe(:91) / SyncFromMiyousheAll(:92)；nap_global → SyncFromHoYoLAB(:95) / SyncFromHoYoLABAll(:96)；IsGlobalServer 时 MenuFlyoutItem_CloudGame 置 Collapsed（:88，仅 nap 分支内）。没有任何分支把上一次的置位收回（全仓无复位代码）。缓存实例（D60）使这些 Visibility 跨导航保留；上游每次新建实例即回收。

- **复核证据**：src/Lunarward/Features/Gacha/GachaLogPage.xaml.cs:88 `MenuFlyoutItem_CloudGame.Visibility = Visibility.Collapsed;`（nap 分支内）、:91-92 `MenuFlyoutItem_SyncFromMiyoushe.Visibility = Visibility.Visible; MenuFlyoutItem_SyncFromMiyousheAll.Visibility = Visibility.Visible;`（nap_cn/nap_bilibili）、:96-97 `MenuFlyoutItem_SyncFromHoYoLAB.Visibility = …Visible;`（nap_global）、:100-102 全局服再次把 CloudGame 置 Collapsed；grep 该文件无任何把已置…

### B20 · 自助查询页三个游戏的查询列表只置 Visible 不置 Collapsed，多个列表在同一行重叠显示

`Features/SelfQuery/SelfQueryPage.xaml.cs:53`　置信度 high　**◐ 部分成立**

- **现状**：依次访问原神、星穹铁道、绝区零的自助查询页后，查询项列表同时出现原神+星铁+绝区零三套条目，互相压在同一区域、点击命中错乱（可见行混排）。

- **上游/对照**：`Features/SelfQuery/SelfQueryPage.xaml.cs:53` — OnNavigatedTo（:53-68）按 CurrentGameBiz.Game 命中一个，分别在 :58/:62/:66 置 Visible，把 ListView_QueryItems_Genshin / _StarRail / _ZZZ 的 Visibility 置 Visible，从不把另外两个置回 Collapsed；XAML 三个 ListView 默认 Visibility="Collapsed"、同在 Grid.Row="1" 相互重叠（ItemsSource 分别是 GenshinQueryItemList(:173) / StarRailQueryItemList(:201) / ZZZQueryItemList(:233)）。上游靠「每次导航新建实例，XAML 默认 Collapsed」实现单显；移植页 NavigationCacheMode="Required"，置位在同一个实例上累积。

- **复核证据**：代码事实成立：src/Lunarward/Features/SelfQuery/SelfQueryPage.xaml.cs:53-68 OnNavigatedTo 只置 Visible（:58 ListView_QueryItems_Genshin、:62 ListView_QueryItems_StarRail、:66 ListView_QueryItems_ZZZ），无 Collapsed 分支；XAML src/Lunarward/Features/SelfQuery/SelfQueryPage.xaml:2 `NavigationCacheMode="Required"`，三个 ListView 默认 `Visibility="Collapsed"`（:170-175/:198-203/:230-235）且都是 `Grid.Row="1"` 同一单元格。但同文件 :163-172 `protected ove…

### B48 · 缓存页未实现 D94 的 OnGameChanged，切游戏后抽卡记录/UID/横幅仍是上一个游戏

`Features/Gacha/GachaLogPage.xaml.cs:139`　置信度 medium　**✅ 成立**

- **现状**：在原神抽卡页切到星穹铁道：页头（跃迁记录/卡池说明/emoji）已变成星铁文案，但 UID 下拉、卡池横幅、祈愿记录列表仍是原神数据（或加载后空白），点击同步又会把上一个游戏的 UID 拉一遍。

- **上游/对照**：`Features/Gacha/GachaLogPage.xaml.cs:139` — 数据加载全在 OnLoaded（:139-154：注册 UpdateGachaLogMessage、Initialize() 里的 InitializeGachaBanners / SelectUid / UidList = GetUids、UpdateWikiDataAsync），OnNavigatedTo（:59-105）只更新标题类状态（GachaTypeText/LegacyFormatName/LegacyImportText/Image_Emoji）与开关位；OnUnloaded（:158-177）才把 DisplayGachaTypeStatsCollection/GachaItemStats/GachaBanners 清空——实例复用（不卸载）时这条路径不跑。D94 已在 Frameworks/PageBase.cs 提供 OnGameChanged 钩子（OnNavigatedTo 里 ContentGameId 与导航参数不一致时调用），全仓只有 GameLauncherPage.xaml.cs:88 重写；本页未重写。Uno 对已缓存实例的同类型导航不保证重跑 Loaded/OnLoaded，正是 D94 要兜的场景。

- **复核证据**：src/Lunarward/Features/Gacha/GachaLogPage.xaml:3 `NavigationCacheMode="Required"`；数据加载全在 src/Lunarward/Features/Gacha/GachaLogPage.xaml.cs:139-154 OnLoaded（:186 `InitializeGachaBanners();`、:188 `UidList = new(_gachaLogService.GetUids());`、:192/196 选 SelectUid、:189-190 读 AppConfig 的上次 UID、:192 后 UpdateWikiDataAsync），OnNavigatedTo（:57-105）只更新 GachaTypeText / LegacyFormatName / LegacyImportText / emoji 与开关位；本页没有 o…

### B49 · 满体力时间格式丢转义冒号，跟随区域分隔符显示

`Features/GameRecord/DailyNote/DailyNoteButton.xaml:406`　置信度 medium　**✅ 成立**

- **现状**：在 TimeSeparator 不是冒号的区域（如 fi-FI 为 '.'、部分区域为空格）满体力时间显示成「12.34」这类非预期格式；zh-CN 下不可见。

- **上游/对照**：`Features/GameRecord/DailyNote/DailyNoteButton.xaml:406` — 上游 `ToString('HH\:mm', x:Null)` 用转义冒号固定字面 ':'；移植写成 `ToString('HH:mm', x:Null)`，'HH'/'mm' 之间的 ':' 变成当前区域（CurrentCulture）的 TimeSeparator。全仓只剩这一处把 `\:` 写成 `:`。

- **复核证据**：移植 src/Lunarward/Features/GameRecord/DailyNote/DailyNoteButton.xaml:406 `<Run Text="{x:Bind StarRailDailyNote.StaminaFullTime.LocalDateTime.ToString('HH:mm', x:Null)}" />`；上游 src/Starward/Features/GameRecord/DailyNote/DailyNoteButton.xaml:406 为 `ToString('HH\:mm', x:Null)`（转义冒号固定字面 ':'）。移植侧全仓没有固定 CurrentCulture / TimeSeparator（grep CurrentCulture 只命中 RegistryFormat.cs、WineRegistryFiles.cs 等处的 InvariantCulture 与…

### B50 · 未登录空态层铺满整页，连左侧工具箱导航一起盖住

`Features/GameRecord/GameRecordPage.xaml:526`　置信度 medium　**✅ 成立**

- **现状**：未登录/无角色时战绩页只剩「还没有登录或没有绑定角色 + 登录按钮」，左侧工具箱（战绩/每日便签/其他子页入口）被完全遮住、无法切换，必须先登录才能看到工具箱；与上游可浏览工具箱的行为不一致（BBS 子项当前是 M4 占位，实际损失有限）。

- **上游/对照**：`Features/GameRecord/GameRecordPage.xaml:18` — 空态 Grid 是根 Grid 的最后一个子元素（:525-541），与 <NavigationView Name="NavigationView_Toolbox"> 同级、没有 Grid.Row/Column 约束也没有 Margin/IsHitTestVisible="False"，因此覆盖整个页面（含左侧工具箱菜单与更新设备指纹入口）；上游此处在未登录时只是内容区空白，菜单仍可点。

- **复核证据**：src/Lunarward/Features/GameRecord/GameRecordPage.xaml:17 根 `<Grid>`（无 RowDefinitions），:19 第一个子元素是 `<NavigationView Name="NavigationView_Toolbox" ... PaneDisplayMode="Left">`（含 PaneFooter 的「更新设备指纹」），:524-539 是根 Grid 的最后一个子元素 `<Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}" Visibility="{x:Bind ShowRecordEmptyState}">`，既无 Grid.Row/Column 也无 Margin、无 IsHitTestVisible；上游 src/Starward/Features/…

### B51 · 战绩页缓存实例未实现 OnGameChanged，切游戏后角色列表/子页仍是上一个游戏

`Features/GameRecord/GameRecordPage.xaml.cs:67`　置信度 medium　**✅ 成立**

- **现状**：从原神战绩切到星穹铁道战绩：左侧工具箱内容区仍是原神角色（昵称/UID/头像）与深渊等子页数据，而 _gameRecordService 已切到 hkrpg（IsHoyolab/Language 已变），后续请求/刷新可能把两个游戏的账号状态混用；空态层（ShowRecordEmptyState）也不会随切换刷新。

- **上游/对照**：`Features/GameRecord/GameRecordPage.xaml.cs:67` — 角色加载在 OnLoaded（:67-95：注册 GameRecordRoleChangedMessage / GameRecordVerifyAccountMessage、CheckAgreementAsync → LoadGameRoles() / UpdateDeviceInfoAsync() / RefreshGameRoleHeadIconSilentlyAsync()），OnNavigatedTo（:45-64）只更新 CurrentGameBiz 派生的 _gameRecordService.IsHoyolab/Language 与导航项可见性。页面 NavigationCacheMode="Required"，未重写 D94 的 OnGameChanged（grep 0），因此实例复用时刻不会重新 LoadGameRoles。

- **复核证据**：src/Lunarward/Features/GameRecord/GameRecordPage.xaml:3 `NavigationCacheMode="Required"`（上游 src/Starward/Features/GameRecord/GameRecordPage.xaml 无此属性）；src/Lunarward/Frameworks/PageBase.cs:70 `OnGameChanged();` 只在 OnNavigatedTo 比较 ContentGameId 后调用，:93 `protected virtual void OnGameChanged() { }`，全仓仅 GameLauncherPage.xaml.cs:88 重写；GameRecordPage.xaml.cs:48-62 OnNavigatedTo 只更新 CurrentGameBiz / `_gameRecordServic…

### B72 · 集录祈愿页缓存后 hk4e_cn↔hk4e_global 切区服不重新加载

`Features/Gacha/GenshinBeyondGachaPage.xaml:3`　置信度 low　**❌ 不成立**

- **现状**：在原神国服集录祈愿页切到国际服再回来：列表/UID 仍是上一次区服的数据（两个区服的抽卡记录本来就不同），需要离开再回来或重启才能刷新。

- **上游/对照**：`Features/Gacha/GenshinBeyondGachaPage.xaml:1` — 该页 .xaml.cs 与上游 0 差异，数据在 OnLoaded 里 Initialize/加载（按 CurrentGameBiz 选 UID 与记录），NavigationCacheMode="Required" 让实例常驻且未重写 D94 的 OnGameChanged；上游每次导航新建实例、OnLoaded 必然重跑。

- **复核证据**：src/Lunarward/Features/Gacha/GenshinBeyondGachaPage.xaml:3 确有 `NavigationCacheMode="Required"`，且该页未重写 OnGameChanged；但页面内容与区服无关：src/Lunarward/Features/Gacha/GenshinBeyondGachaService.cs:43 `SELECT DISTINCT Uid FROM {GachaTableName}`（无 GameBiz/Region 过滤），GenshinBeyondGachaPage.xaml.cs:73-97 Initialize 只用 UidList + `AppConfig.GetLastUidInGachaLogPage("hk4eugc")`，CurrentGameBiz 仅出现在 :185/:382/:407 的「从游戏获取」命令里。切区服时 …


## 背景与图片加载（6 条）

### B21 · 图库视频条目把整个视频文件读进内存当图片解码（IsThumbnail 语义丢失 + DisplayPath 兜底到 .mp4）

`Controls/CachedImage.Linux.cs:206-208（ReadFileAsync）、64-80（IsThumbnail/PngThumbnail 未被读取）`　置信度 high　**✅ 成立**

- **现状**：进入背景图库时，每个视频格子先触发一次整段视频读盘+解码失败（几百 MB 的视频就是几百 MB 内存峰值与长时间黑块），缩略图生成失败或 ffmpeg 不可用时永远黑块；磁盘/网络盘上尤其明显。

- **上游/对照**：`Controls/CachedImage.cs:49-65（IsThumbnail → ImageThumbnail）` — 上游 file 分支里 `if (IsThumbnail)` 会分流到 ImageThumbnail.GetVideoThumbnailAsync/GetImageThumbnailAsync（WIC 小图 + 磁盘缓存）。移植版 IsThumbnail/PngThumbnail 只声明不读取，ReadFileAsync 无条件 `await File.ReadAllBytesAsync(path)` 把整文件塞进内存，只有 DecodePixelWidth>0 时才在**整文件已读入之后**用 Skia 降采样。BackgroundViewPage.xaml:168-170 仍传 IsThumbnail=True/PngThumbnail=True 且 Source 绑 DisplayPath，而 BackgroundFileItem.cs:35 `DisplayPath => ThumbnailPath ?? FilePath`：VideoThumbnailer 出图之前（以及生成失败时）它就是 .mp4/.mkv 原路径。

- **复核证据**：src/Lunarward/Controls/CachedImage.Linux.cs:208 `byte[] data = await File.ReadAllBytesAsync(path, token);` 无条件整文件读入；同文件:64-80 只注册 IsThumbnail/PngThumbnail 两个 DependencyProperty，全文件无读取点；BackgroundViewPage.xaml:168-170 仍传 IsThumbnail=True/PngThumbnail=True 且 Source={x:Bind DisplayPath, Mode=OneWay}，BackgroundFileItem.cs:35 `DisplayPath => ThumbnailPath ?? FilePath`。上游 src/Starward/Controls/CachedImage.cs:49-64 `i…

### B22 · 换自定义壁纸/开关自定义背景后背景不刷新（早退守卫 + 5 分钟建议背景缓存）

`Features/Background/AppBackground.Linux.cs:394`　置信度 high　**✅ 成立**

- **现状**：在游戏设置里换一张自定义壁纸（或打开/关闭『启用自定义背景』）后，主窗口背景纹丝不动，只有切换游戏或重启应用才生效；日志里连请求都没有。

- **上游/对照**：`Features/Background/AppBackground.xaml.cs:180-196（UpdateBackgroundAsync 主体，无早退守卫）` — 移植版在 UpdateBackgroundCoreAsync 开头加了守卫：`if (requested is null && gameId is not null && gameId == _appliedGameId && (BackgroundImageSource is not null || _videoActive)) return;`（:394）。而 UI 侧『改自定义背景』『切换启用自定义背景』等操作发的是无参消息：GameLauncherSettingDialog.xaml.cs:760/802/846/938、GameLauncherPage.xaml.cs:1242 都是 `Send(new BackgroundChangedMessage())`，GameBackground==null → requested==null → 命中守卫直接 return。上游没有这个守卫，每次都重跑 GetSuggestedGameBackgroundAsync + ChangeBackgroundImageAsync。第二重保险也会挡住：GetSuggestedBackgroundCachedAsync 成功结果缓存 5 分钟（SuggestedBackgroundTtl），自定义背景路径变了也还在用旧的 GameBackground。

- **复核证据**：src/Lunarward/Features/Background/AppBackground.Linux.cs:394 `if (requested is null && gameId is not null && gameId == _appliedGameId && (BackgroundImageSource is not null || _videoActive)) { return; }`；UI 侧 GameLauncherSettingDialog.xaml.cs:760/802/846/938 与 GameLauncherPage.xaml.cs:1242 都发无参消息（BackgroundChangedMessage.cs:8-12 GameBackground 默认 null），:157-166 OnBackgroundChangedMessage 原样传 message.GameBackgrou…

### B52 · 视频帧回调线程直接改 UI 并释放播放器

`Features/Background/AppBackground.Linux.cs:712-717（catch 里 StopVideoBackground）、880-900（StopVideoBackground 改 UI）`　置信度 medium　**✅ 成立**

- **现状**：偶发（某一帧呈现失败/画布尺寸异常时）抛跨线程访问异常或直接崩溃退出；即使不崩，也可能在 VLC 线程里 Dispose 播放器导致死锁（界面卡住、视频壁纸再也不出来，点击无响应）。

- **上游/对照**：`Features/Background/AppBackground.xaml.cs:472-505（MediaPlayer_VideoFrameAvailable，异常只在 try/catch 内吞掉，UI 操作都在 DispatcherQueue 里）` — OnVideoFrameArrived 是 LibVLCSharp 帧回调线程上的回调；catch 分支调用 StopVideoBackground()，后者直接 `_videoCanvas.Visibility = Collapsed; _image.Visibility = Visible; _image.Opacity = 1.0;`（:897-899）并 `_videoPlayer?.Dispose()`（:882）——既跨线程改 UI 元素，又在 VLC 自己的回调线程里销毁正在回调的播放器。上游对应位置的所有 UI 操作都在 `DispatcherQueue.TryEnqueue` 内，异常只 `catch { }` 吞掉并 Release 信号量，从不在回调线程碰 XAML。

- **复核证据**：src/Lunarward/Features/Background/AppBackground.Linux.cs:712-717 `catch (Exception ex) { Interlocked.Exchange(ref _paintQueued, 0); Serilog.Log.Warning(ex, "[background] present video frame failed"); StopVideoBackground(); }`；OnVideoFrameArrived(:657) 由 VideoBackgroundPlayer.cs:211 `FrameReady?.Invoke(_pixels, VideoWidth, VideoHeight);` 在 libVLC Display 回调线程触发（:112 `_player.SetVideoCallbacks(Lock, Unlock, Displ…

### B53 · 绘制线程用可能已被释放的 SKBitmap（与 StopVideoBackground 竞态）

`Features/Background/AppBackground.Linux.cs:721-751（VideoCanvas_PaintSurface 取 _videoFront 后出锁使用）`　置信度 medium　**◐ 部分成立**

- **现状**：停止/切换视频壁纸的瞬间偶发原生崩溃（SIGSEGV，托管栈看不到）或 SkiaSharp ObjectDisposedException 刷屏；崩溃点无规律，与机器速度相关。

- **上游/对照**：`Features/Background/AppBackground.xaml.cs:472-505` — VideoCanvas_PaintSurface 在 `lock (_frameLock)` 里只把引用取到局部变量 `bitmap = _videoFront` 就出锁，随后在锁外用 `canvas.DrawBitmap(bitmap, ...)`（:751）；而 StopVideoBackground 在同一把锁里把 _videoFront/_videoBack Dispose 并置 null（:886-895）。UI 线程停视频（切游戏/暂停/异常）恰好落在『取引用』与『DrawBitmap』之间时，就是对已释放 SKBitmap 的原生绘制。上游的等价路径是把帧 Copy 到 CanvasRenderTarget 后由合成器持有，不存在这种 use-after-free。

- **复核证据**：src/Lunarward/Features/Background/AppBackground.Linux.cs:721-733 `lock (_frameLock) { if (!_hasVideoFrame) return; bitmap = _videoFront; }` 后出锁，:751 `canvas.DrawBitmap(bitmap, new SkiaSharp.SKRect(0, 0, bitmap.Width, bitmap.Height), destination, paint);`；StopVideoBackground :886-889 在 lock 内 `_videoFront?.Dispose(); ... _videoFront = null;`；OnVideoFrameArrived 尺寸变化时同样在 lock 内 Dispose（:670-671），运行在 VLC 回调线程。；「取引…

### B54 · 自定义背景不再做解码校验：坏图被接受、落盘、写进配置，界面无任何报错

`Features/Background/BackgroundService.cs:276-291（CheckBackgroundFileAvailableAsync）`　置信度 medium　**✅ 成立**

- **现状**：选了一张解不了的图后壁纸直接变成纯 #FF16161A 底色、没有任何提示，而且已持久化——重启/换游戏回来还是黑的，用户只能再手动选一张；上游此时会弹『无法解码该文件』并保留原壁纸。

- **上游/对照**：`Features/Background/BackgroundService.cs:289-303（MediaSource.OpenAsync / BitmapDecoder.CreateAsync 试解码）` — 上游 CheckBackgroundFileAvailableAsync 会用 MediaSource.OpenAsync + BitmapDecoder.CreateAsync 真正解一次，解不开就抛（对话框 catch COMException 显示 GameLauncherSettingDialog_CannotDecodeFile，且不会把文件复制进缓存）。移植版只做 File.Exists + FileInfo.Length>0，于是：解不了的 png/avif/jxl（Skia+Magick 都覆盖不到的编码）照旧被复制进 CacheFolder/bg、写进 AppConfig.SetCustomBg，随后 AppBackground 的 ToBitmap（:907-928）只查存在性就直接 `new BitmapImage(new Uri(full))`，返回非 null，所以 :941 的『这张图片读不了』提示永远走不到。

- **复核证据**：src/Lunarward/Features/Background/BackgroundService.cs:276-291 `private static async Task CheckBackgroundFileAvailableAsync(string file) { await Task.Run(() => { if (!File.Exists(file)) throw new FileNotFoundException(...); var info = new FileInfo(file); if (info.Length <= 0) throw new InvalidDataException("background file is empty"); }); }`（无解码）；:212-221 校验通过即 `File.Copy(file, path, true)` 并返回 name；上游 src/Star…

### B73 · 首次进图库时格子尺寸可能一直是默认值（UpdateTileSize 静默失败且无重试）

`Features/Background/BackgroundViewPage.xaml.cs:108-118（UpdateTileSize 里 ItemsPanelRoot 不是 WrapPanel 就 return）`　置信度 low　**❌ 不成立**

- **现状**：第一次打开背景图库时格子可能排成单列/宽度异常，必须手动拖动窗口触发 SizeChanged 才恢复正常布局；不同机器（首帧耗时不同）表现不一致。

- **上游/对照**：`Features/Background/BackgroundViewPage.xaml:150-157（ItemsWrapGrid 自带 ItemWidth/ItemHeight，无需代码参与）` — UpdateTileSize 开头 `if (GridView_Images?.ItemsPanelRoot is not CommunityToolkit.WinUI.Controls.WrapPanel panel) return;`，没有任何重试；调用点只有 OnPageSizeChanged 和 LoadBackgroundItems 末尾的一次 Low 优先级 DispatcherQueue 排队（:191-192）。若这次排队早于 ItemsPanel 创建（或页面被缓存复用时 SizeChanged 不再触发），panel.ItemWidth/ItemHeight 就一直是 null，WrapPanel shim 便按子元素自身 DesiredSize 排列（CachedImage 是 Stretch，没有固定宽高）。

- **复核证据**：src/Lunarward/Features/Background/BackgroundViewPage.xaml.cs:112-115 的 `if (GridView_Images?.ItemsPanelRoot is not CommunityToolkit.WinUI.Controls.WrapPanel panel) { return; }` 确实无重试，但存在两处补偿：:34 构造函数 `SizeChanged += OnPageSizeChanged;`，:89-90 处理器第一行就 `UpdateTileSize();`——页面首次 Arrange（ActualSize 由 0 变 1200）必然触发 SizeChanged，而 ListView 的 ItemsPanel（BackgroundViewPage.xaml:152-155 `<tk:WrapPanel .../>`）在 Measure 阶段…


## 设置界面（8 条）

### B23 · InitializeHotkeyInput 丢失 State 赋值，注册失败在设置页不可见

`Helpers/HotkeyManager.Linux.cs:134-140`　置信度 high　**◐ 部分成立**

- **现状**：纯 Wayland 会话或 libX11 缺失时，设置页热键项显示为已设置且无任何警告，用户以为热键可用（实际 PollOnce 永不触发）。

- **上游/对照**：`Features/Setting/HotkeyManager.cs:129-136` — 上游 :133-134 为 SetHotkey(...) 后紧跟 hotkeyInput.State = info.Error.Succeeded ? HoykeyInputState.None : HoykeyInputState.Warning;。移植 :134-140 只调 SetHotkey，没有 State；而 :96 的注释仍写着「界面会显示「注册失败」」。RegisterHotkey 在 X11KeyState.Available==false 时把 info.Error 设为 Win32Error(1409)，该 Error 只被 HotkeySetting.xaml.cs 的编辑完成回调（info?.Error.Failed）读取，页面首次加载路径（InitializeHotkeyInput）完全不提示。

- **复核证据**：src/Lunarward/Helpers/HotkeyManager.Linux.cs:134-140 InitializeHotkeyInput 只调用 hotkeyInput.SetHotkey((uint)info.Modifiers, (uint)(int)info.Key)，缺上游 HotkeyManager.cs:134 `hotkeyInput.State = info.Error.Succeeded ? HoykeyInputState.None : HoykeyInputState.Warning;`。但移植 :28-32 在 `!X11KeyState.EnsureStarted()` 时直接 return（_hotkeys 不填、Error 不会 Failed）；X11 可用时 RegisterHotkey :94-101 只可能返回 `info.Error = default` 成功。；缺 …

### B24 · 无 X11 时 _hotkeys 从未填充，设置页显示为空而非已保存组合

`Helpers/HotkeyManager.Linux.cs:28-32、171`　置信度 high　**✅ 成立**

- **现状**：Wayland 会话下打开热键设置页，两行快捷键显示为「点击设置快捷键」空白；用户改一次设置即把 AppConfig 里的值重写为新组合（原值不可见）。

- **上游/对照**：`Features/Setting/HotkeyManager.cs:20-64、163-171` — 上游 GetHotkeyInfo 用 id switch 恒返回非 null 的静态实例，配置值总能在页面上显示。移植 :28-32 在 X11KeyState.EnsureStarted() 失败时直接 return，跳过 :24-60 的 _hotkeys 填充；随后 :171 GetHotkeyInfo 走 _hotkeys.TryGetValue 返回 null，InitializeHotkeyInput (:134) 的 if 不成立。

- **复核证据**：HotkeyManager.Linux.cs:28-32 `if (!X11KeyState.EnsureStarted()) { Serilog...; return; }` 早于 :33-58 的 `_hotkeys[item.Id] = item;` 填充循环；:17 `_hotkeys = new()`；:171 `GetHotkeyInfo(int id) => _hotkeys.TryGetValue(id, out var info) ? info : null`。上游 HotkeyManager.cs:163-171 用 id switch 恒返回静态 ShowMainWindow/ScreenshotCapture。HotkeySetting.xaml.cs:29-37 页面加载调 InitializeHotkeyInput；HotkeyInput.xaml.cs:45-63 State=None …

### B25 · 热键注册失败不再弹 InAppToast，用户看不到「快捷键被占用/注册失败」

`Helpers/HotkeyManager.Linux.cs:52-57`　置信度 high　**❌ 不成立**

- **现状**：Wayland 会话或注册确有失败时（例如 X11 下与桌面环境的键冲突不会被发现），用户以为全局热键已生效，实际按键无反应，日志里只有一行 Information。

- **上游/对照**：`Features/Setting/HotkeyManager.cs:45-57` — 上游 InitializeHotkey 在 RegisterHotkey 返回 error.Failed 后必弹提示：ERROR_HOTKEY_ALREADY_REGISTERED 用 Lang.HotkeyManager_TheShortcutKeys0IsAlreadyInUsePleaseModifyItInSettingsPage，其它失败用 Lang.HotkeyManager_FailedToRegisterTheShortcutKeys0PleaseRetryInSettingsPage，均经 InAppToast.MainWindow?.Warning(...)。移植 :54-57 只有 Serilog.Log.Information("Hotkey {id} not available")，把用户可见提示整段删除；而 InAppToast 在本移植中可用（AdvancedSetting 等处仍在用），并非平台限制。叠加 :89 无条件 IsRegistered=true，界面上完全没有任何失败信号。

- **复核证据**：HotkeyManager.Linux.cs:52-57 在 error.Failed 时只有 :56 `Serilog.Log.Information("Hotkey {id} not available", item.Id);`，无 InAppToast（上游 HotkeyManager.cs:45-57 有两条失败提示）；但该分支不可达：InitializeHotkey :28-32 在 EnsureStarted 失败时已 return，而 RegisterHotkey :94-99 唯一失败条件是 `!X11KeyState.Available`，X11KeyState.cs:34 `Available => _display != 0`、:36-58 EnsureStarted 成功后恒为 true。；删掉的 toast 只存在于注册循环里一个永远不会失败的分支，对用户可见行为零影响；真实缺口是 B24（…

### B26 · RegisterHotkey 无条件返回成功，但轮询层只认得部分键码，多数合法组合永不触发

`Helpers/HotkeyManager.Linux.cs:84-101`　置信度 high　**✅ 成立**

- **现状**：用户设置 Alt+↑ / Ctrl+Home / Pad0 等组合时界面显示注册成功，实际按下去毫无反应，也没有任何错误提示。

- **上游/对照**：`Features/Setting/HotkeyManager.cs:68-97` — :84-101 只要 X11KeyState.Available 就 info.IsRegistered = true 并返回 success；真正的键码映射在 src/Lunarward/Helpers/X11KeyState.cs 的 KeycodeFor（约 :118-134），只覆盖 VK_F1-F12、VK_0-9、VK_A-Z、VK_SNAPSHOT、VK_PAUSE、VK_SPACE，其余返回 null→keycode 0。而上游委托 RegisterHotKey 由系统判定，冲突/非法会立刻失败并提示。HotkeyInput.AvaliableKeyDict（Features/Setting/HotkeyInput.xaml.cs:425-520）允许方向键、Home/End/PageUp/PageDown、小键盘、标点等。

- **复核证据**：HotkeyManager.Linux.cs:89 `info.IsRegistered = true;`、:100-101 `info.Error = default; return default;`（只要 X11KeyState.Available 即报成功）；X11KeyState.cs:117-135 KeycodeFor 仅映射 VK_F1-F12/VK_0-9/VK_A-Z/VK_SNAPSHOT/VK_PAUSE/VK_SPACE，其余 `_ => null`，:95-99 `if (!IsKeyDown(KeycodeFor(key))) return false;`、:101-104 keycode==0 返回 false；HotkeyInput.xaml.cs:425-520 AvaliableKeyDict 却允许 Home/End/PageUp/PageDown/方向键/小键盘/标点。上游 …

### B55 · ParseHotkey 不认「0」这一删除标记，已删除的热键会被默认值复活并被写回配置

`Helpers/HotkeyManager.Linux.cs:63-77、41-48`　置信度 medium　**✅ 成立**

- **现状**：若配置文件里是上游格式的 "0"（用户曾删除该热键），升级/换机后 Alt+S 突然生效并写回 1+83；反之移植写的 "0+0" 在语义上与 "0" 等价，只是格式不同。

- **上游/对照**：`Features/Setting/HotkeyManager.cs:139-158、77-80` — 上游 GetModifiersKey :145-148 明确 if (value.Trim() is "0") return (0,0);，配合 RegisterHotkey :77-80 的 (0,0) 提前返回成功，实现「已删除且不落盘」。移植 ParseHotkey :63-77 要求恰好两段数字，"0" 返回 null → :38-48 落到 DefaultModifiers/DefaultKey；同时 :93 SaveHotkey 无条件写入，于是 "0" 被覆盖成 "1+83"。DeleteHotkey :118-131 写的是 FormatHotkey(0,0)="0+0"，与上游写的 "0" 也不一致。

- **复核证据**：src/Lunarward/Helpers/HotkeyManager.Linux.cs:63-77 `string[] parts = text.Split('+'); if (parts.Length != 2 || !int.TryParse(parts[0], out int modifiers) || !int.TryParse(parts[1], out int key)) { return null; }`（"0" 返回 null）；:36-48 解析结果为 null 时保留 `modifiers = item.DefaultModifiers; key = item.DefaultKey;`（ShowMainWindow = MOD_ALT/VK_S，:13），随后 RegisterHotkey→:93 `SaveHotkey(info);` 写回 `FormatHotkey(...)`；Delete…

### B56 · SaveHotkey 无条件落盘，丢失上游「注册成功且值变动才写」的守卫

`Helpers/HotkeyManager.Linux.cs:93`　置信度 medium　**◐ 部分成立**

- **现状**：Wayland 会话首次启动即把默认值写进配置；失败状态无法通过「不写配置」自愈，也是上一条复活问题的放大器。

- **上游/对照**：`Features/Setting/HotkeyManager.cs:83-86` — 上游 :83-86 只在 error.Succeeded && (info.Modifiers != modifiers || info.Key != key) 时 AppConfig.SetValue；移植 :84-101 每次都调 SaveHotkey（:155-169），包括 X11KeyState.Available==false、注册事实上失败的路径。

- **复核证据**：src/Lunarward/Helpers/HotkeyManager.Linux.cs:84-99 RegisterHotkey 先 `info.IsRegistered = true; _hotkeys[id] = info; _pressed.Remove(id); SaveHotkey(info);`（:90-93）之后才 `if (!X11KeyState.Available) { info.Error = new Win32Error(1409); return info.Error; }`；SaveHotkey :155-169 无条件 `AppConfig.SetValue(FormatHotkey(info.Modifiers, info.Key), info.ConfigSetting);`；InitializeHotkey :28-33 `if (!X11KeyState.EnsureStart…

### B74 · SettingNavigation 直达入口与页面缓存冲突，只有首次构造生效

`Features/Setting/SettingPage.xaml.cs:20-31、45-68`　置信度 low　**◐ 部分成立**

- **现状**：调试/预检若真去调 SettingNavigation.SetTarget，从第二次起点了没反应，页面仍停留在上次的子页（潜伏；当前无调用方，无可见现象）。

- **上游/对照**：`Features/Setting/SettingPage.xaml.cs:17-22` — ctor :22 调 ResolveInitialPage() 消费 SettingNavigation.TakeTarget()（Features/Setting/SettingNavigation.cs，TakeTarget 取出后清空 _target），而 XAML :3 把页面设为 NavigationCacheMode="Required"，ctor 整个会话只跑一次。之后任何 SetTarget 都不会被消费（且当前全仓无 SetTarget 调用方，钩子为空转）。

- **复核证据**：src/Lunarward/Features/Setting/SettingPage.xaml.cs:17 ctor 里（Navigate 在 :22） `Frame_Setting.Navigate(initialPage);`，initialPage 来自 :45-49 `SettingNavigation.TakeTarget()`（src/Lunarward/Features/Setting/SettingNavigation.cs:18-23 取出后置 null）；src/Lunarward/Features/Setting/SettingPage.xaml:2-3 `NavigationCacheMode="Required"`，页面实例常驻、ctor 整个会话只跑一次，:24-31 的左侧选中项同步同样只在 ctor。全仓 grep `SettingNavigation` 只有定义、:48 消费点与 s…

### B75 · 未知热键 id 被静默当成截图热键

`Helpers/HotkeyManager.Linux.cs:143-152`　置信度 low　**◐ 部分成立**

- **现状**：目前页面只传 44444/44445，暂无现象；一旦新增热键或传错 id，会静默覆盖截图热键的配置（潜伏）。

- **上游/对照**：`Features/Setting/HotkeyManager.cs:93-95、163-171` — 上游 GetHotkeyInfo 对非 44444/44445 返回 null，RegisterHotkey 因此走 :93-95 返回 ERROR_BAD_ARGUMENTS。移植 GetOrCreateHotkeyInfo 在 _hotkeys 里找不到 id 时回退到 ScreenshotCapture 并继续注册/保存，等于把任意 id 的调用都记到截图热键头上。

- **复核证据**：src/Lunarward/Helpers/HotkeyManager.Linux.cs:143-151 `GetOrCreateHotkeyInfo`：`HotkeyInfo known = id == ShowMainWindow.Id ? ShowMainWindow : ScreenshotCapture; _hotkeys[id] = known;`——未知 id 拿到的是 ScreenshotCapture 同一个实例（其 Id 仍是 44445、ConfigSetting 仍是 ScreenshotCaptureHotkey）；:84-93 RegisterHotkey 紧接着改写 `info.Modifiers/Key/IsRegistered` 并 `SaveHotkey(info)`（:155-169 写到 info.ConfigSetting），等于把未知 id 的组合键落盘到截图热键。上游 s…


## 启动器与游戏入口（10 条）

### B30 · 缓存页切换游戏时 LoadGameContent 被调用两次（OnGameChanged + OnLoaded），初始化并发

`Features/GameLauncher/GameLauncherPage.xaml.cs:74`　置信度 medium　**◐ 部分成立**

- **现状**：换游戏时版本检查与背景图/服务器列表请求各发两遍（首次进入游戏页也会重复初始化），日志里成对出现；极端时序下先完成的请求把后清空的状态写回，出现背景图/服务器列表错位或闪烁。

- **上游/对照**：`Features/GameLauncher/GameLauncherPage.xaml.cs:63` — PageBase 在 PageEx_Loaded 里先设 ContentGameId，OnNavigatedTo 发现切换就调 OnGameChanged→LoadGameContent（第 88-90 行），随后 Loaded 事件再走 OnLoaded→LoadGameContent（第 68-74 行）。两次都会 CheckGameVersion/InitializeGameServerAsync/InitializeBackgameImageSwitcherAsync/UpdateGameInstallTask，且第二次开始时把 BackgroundImages 置 null、_gameInstallTask 置 null。

- **复核证据**：GameLauncherPage.xaml.cs:67-75 OnLoaded 调 LoadGameContent()，:85-89 OnGameChanged 也调 LoadGameContent()，LoadGameContent(:95-134) 无幂等守卫且开头把 BackgroundImages/_gameInstallTask/GameServers 等清空；Frameworks/PageBase.cs:53-72 在缓存实例切游戏时先调 OnGameChanged，页面重新挂载时 Loaded→OnLoaded 再跑一次；页面 NavigationCacheMode="Required"（GameLauncherPage.xaml:3）。；两条入口都无守卫的代码事实成立，切页回来后可能连跑两次（OnGameChanged + OnLoaded），CheckGameVersion/InitializeGa…

### B31 · 运行时配方/前缀/伴随启动按 AppConfig.CurrentGameBiz 解析，而不是本次启动的 gameId

`Features/GameLauncher/GameLauncherService.Linux.cs:220`　置信度 medium　**✅ 成立**

- **现状**：从 deep link `lunarward://startgame/<biz>`（UrlProtocolService.cs:72）、第二实例管道（App.xaml.cs:166）或 CLI startgame（App.xaml.cs:275）启动「非当前选中」的游戏时，会用另一款游戏的配方/runner/前缀启动（甚至用另一款游戏的前缀 + 它的 Args），退出时跑的也是另一款游戏的伴随程序；首次自动创建的配方（EnsureFor 用的是正确 biz）不会被用到。

- **上游/对照**：`Features/GameLauncher/GameLauncherService.cs:315` — CreateGameProcessStartInfo 用 `Features.Runtime.ProfileBinding.Resolve(AppConfig.CurrentGameBiz)`（第 220 行，DescribeLaunchPlan 第 413 行同）选运行环境，RunExitCompanions 用 `GameBiz biz = AppConfig.CurrentGameBiz;`（第 145 行）；而上游 StartGameAsync(GameId gameId) 全程只用入参 gameId。GameLauncherService.cs:402 的预检用的却是 `gameId.GameBiz`。

- **复核证据**：src/Lunarward/Features/GameLauncher/GameLauncherService.cs:397 `(…, _) = Features.Runtime.ProfileBinding.EnsureFor(gameId.GameBiz, _logger)`、:404 `LaunchPreflight.Check(gameId.GameBiz, exe, launchProfile, _logger)`、:427 `var psi = CreateGameProcessStartInfo(exe, arg, out bool vw);`；而 GameLauncherService.Linux.cs:220 `profile = Features.Runtime.ProfileBinding.Resolve(AppConfig.CurrentGameBiz);`（:413 DescribeLaun…

### B32 · 配方 Args 非空时游戏自带参数被整体替换（登录票据/DX12/弹窗参数丢失）

`Features/GameLauncher/GameLauncherService.Linux.cs:320`　置信度 medium　**✅ 成立**

- **现状**：只要给某个游戏配了「启动参数」，登录回填（login_auth_ticket）就失效，DX12 开关与窗口化参数也一起失效，表现为启动后仍要求扫码登录、设置里的 DX12 选项不生效；参数被静默吞掉且无提示。

- **上游/对照**：`Features/GameLauncher/GameLauncherService.cs:367` — 第 320 行 `string effectiveArgs = string.IsNullOrWhiteSpace(profile?.Args) ? (arg ?? "") : profile!.Args;`，并且 LaunchRecipe.Build（Features/Runtime/LaunchRecipe.cs:201）同样在 profile.Args 非空时用 profile.Args 替代 gameArgs。而上游的 arg 里包含 `login_auth_ticket=...`（上游 367 行）、`-popupwindow`（372）、`-use-d3d12`（376）以及用户设置的启动参数。

- **复核证据**：src/Lunarward/Features/GameLauncher/GameLauncherService.Linux.cs:320 `string effectiveArgs = string.IsNullOrWhiteSpace(profile?.Args) ? (arg ?? "") : profile!.Args;`；src/Lunarward/Features/Runtime/LaunchRecipe.cs:201 `Argv = BuildArgv(Enhancements.Wrappers(profile), runnerPath, exePath, string.IsNullOrWhiteSpace(profile?.Args) ? gameArgs : profile!.Args);`。被丢弃的 arg 由 GameLauncherService.cs:361 `arg = AppConfig.…

### B33 · ApplyWineRuntimeEnvironment 对 full.IndexOf("/files/") 未判空，自定义 Proton 路径会抛异常

`Features/GameLauncher/GameLauncherService.Linux.cs:358`　置信度 medium　**◐ 部分成立**

- **现状**：用户用自建/重打包的 Proton（目录结构不是 files/bin/wine）时，启动游戏直接异常失败；异常在 CreateGameProcessStartInfo 内被 Task.Run 捕获后转成启动失败 toast，用户看不到具体原因。

- **上游/对照**：`Features/GameLauncher/GameLauncherService.Linux.cs:0` — 第 348 行只用 `full.Contains("/steamapps/common/")` 做门禁，第 358 行直接 `string toolRoot = full[..full.IndexOf("/files/", StringComparison.Ordinal)];`。若 runner 路径形如 ~/.local/share/Steam/steamapps/common/GE-Proton9-20/proton（没有 /files/ 段），IndexOf 返回 -1，`full[..-1]` 抛 ArgumentOutOfRangeException。

- **复核证据**：src/Lunarward/Features/GameLauncher/GameLauncherService.Linux.cs:351 `int idx = full.IndexOf("/steamapps/common/", StringComparison.Ordinal); if (idx < 0) { return; }`，:358 `string toolRoot = full[..full.IndexOf("/files/", StringComparison.Ordinal)];` —— 路径含 /steamapps/common/ 却不含 /files/ 时 `full[..-1]` 抛 ArgumentOutOfRangeException；调用点 :312 `ApplyWineRuntimeEnvironment(info, wine);` 无 try。但异常经 GameLauncherServ…

### B34 · GetGameProcessAsync 仍按 exe 名找进程，Linux 上永远找不到游戏（wine 进程名是 wine/wine64）

`Features/GameLauncher/GameLauncherService.cs:274`　置信度 medium　**◐ 部分成立**

- **现状**：CheckGameRunningAsync/启动前的重复启动检查（GameLauncherPage.xaml.cs:587、GameLauncherSettingDialog.xaml.cs:258/541、StartGameAsync:320）在 Linux 恒为 false：游戏已在运行时启动器仍显示「开始游戏」，用户可以重复点、起第二个 wine 实例。

- **上游/对照**：`Features/GameLauncher/GameLauncherService.cs:274` — 移植原样保留上游 `Process.GetProcessesByName(name).Where(x => x.SessionId == currentSessionId && !IsProcessPending(x))`，name 来自 GetGameExeNameAsync(...).Replace(".exe","")。移植侧启动的是 wine，真正跑游戏的进程名是 wine64/wine-preloader，游戏 exe 名不会出现在进程表里；同文件其它地方（PlayTimeRecordService 的 pid 重载）已经专门加了 Linux 分支，这里没有。

- **复核证据**：src/Lunarward/Features/GameLauncher/GameLauncherService.cs:273-274 `var name = (await GetGameExeNameAsync(gameId)).Replace(".exe", ""); return Process.GetProcessesByName(name).Where(x => x.SessionId == currentSessionId && !IsProcessPending(x)).FirstOrDefault();`；调用点 GameLauncherPage.xaml.cs:587 `GameProcess = await _gameLauncherService.GetGameProcessAsync(CurrentGameId);`、GameLauncherSettingDialog.xaml.cs:541 卸…

### B35 · thirdPartyTool/StartGameWithCMD 仍走「按进程名轮询」的时长记录分支，Linux 上必然超时

`Features/GameLauncher/GameLauncherService.cs:468`　置信度 medium　**✅ 成立**

- **现状**：用户开着「通过 CMD 启动」或用第三方工具启动时：游戏正常启动，但游玩时长不记录，且 StartGameAsync 返回 null → 页面停留在「开始游戏」状态（IsGameRunning 不为真）。

- **上游/对照**：`Features/GameLauncher/GameLauncherService.cs:400` — 移植只把 cmd 包装的条件改成 `&& OperatingSystem.IsWindows()`（第 384 行），但第 468-470 行仍是 `if (thirdPartyTool || AppConfig.StartGameWithCMD) return await _playTimeRecorderService.StartProcessToLogAsync(gameId);`，而该重载（PlayTimeRecordService.cs:300-340）按 exe 名轮询 15×2 秒。Linux 上 wine 拉起后找不到该进程名，30 秒后返回 null。设置页的 StartGameWithCMD 开关（GameLauncherSettingDialog.xaml:387）仍然暴露给用户。

- **复核证据**：src/Lunarward/Features/GameLauncher/GameLauncherService.cs:468 `if (thirdPartyTool || AppConfig.StartGameWithCMD) { return await _playTimeRecorderService.StartProcessToLogAsync(gameId); }`；src/Lunarward/Features/PlayTime/PlayTimeRecordService.cs:305-317 `string name = await GetGameExeNameWithoutExtensionAsync(gameId); for (int i = 0; i < 15; i++) { await Task.Delay(2000); var processes = Process.GetProcessesByN…

### B61 · 公告改为浏览器打开后红点不再刷新（GameNoticeWindowClosedMessage 链路被删）

`Features/GameLauncher/GameBannerAndPost.xaml.cs:259`　置信度 low　**✅ 成立**

- **现状**：点开公告（浏览器）后回到启动器，公告红点仍然亮着，直到重新进入页面/换游戏触发 UpdateGameNoticeAlertAsync 才消失；用户会以为还有未读公告。

- **上游/对照**：`Features/GameLauncher/GameBannerAndPost.xaml.cs:212` — 上游 OpenGameNoticeWindow 打开 GameNoticeWindow，Loaded 里注册 GameNoticeWindowClosedMessage，窗口关闭后重新 UpdateGameNoticeAlertAsync 把 IsGameNoticesAlert 置回 false。移植删掉了消息注册与 OnGameNoticeWindowClosed，OpenGameNoticeWindowAsync 只 `OpenUrl.Open(url)`，浏览器何时关闭无从得知，也没有其它地方重置 IsGameNoticesAlert。

- **复核证据**：src/Lunarward/Features/GameLauncher/GameBannerAndPost.xaml.cs:98-99 的 Loaded 只注册 MainWindowStateChangedMessage 与 GameAnnouncementSettingChangedMessage；:420 OpenGameNoticeWindowAsync 只取 URL 后 :429 Helpers.OpenUrl.Open(url)；IsGameNoticesAlert 仅在 :149/:255/:259 赋值，无浏览器返回后的回写。上游同名文件 :62 注册 GameNoticeWindowClosedMessage、:98-102 在其回调里 UpdateGameNoticeAlertAsync；移植树 grep 不到 GameNoticeWindowClosedMessage 类型。红点数据源 GameN…

### B62 · ReloadForCurrentGameAsync 先记 _contentGameId 再 await，无并发/后置校验，快速换游戏会显示上一款的内容

`Features/GameLauncher/GameBannerAndPost.xaml.cs:64`　置信度 low　**✅ 成立**

- **现状**：在游戏选择器里连续切换两款游戏时，banner/活动 Pivot 可能停留在前一款游戏的活动，公告红点也是前一款的；再切一次才恢复。

- **上游/对照**：`Features/GameLauncher/GameBannerAndPost.xaml.cs:170` — 第 68 行 `_contentGameId = CurrentGameId;` 之后才 `await UpdateGameContentAsync()+UpdateGameNoticeAlertAsync()`；UpdateGameContentAsync（第 204 行）在 `await _hoYoPlayService.GetGameContentAsync(CurrentGameId)` 返回后不复核 CurrentGameId 是否已变，直接 Banners=…/PostGroups=…/ShowBannerAndPost=true。两次快速切换时后完成的旧请求会覆盖新内容（Upstream 每次换游戏重建控件，不存在此竞态）。

- **复核证据**：GameBannerAndPost.xaml.cs:46-61 CurrentGameId setter 变化即 :58 `_ = ReloadForCurrentGameAsync();`；:64-76 ReloadForCurrentGameAsync 在 :68 `_contentGameId = CurrentGameId;` 后 await 内容与红点，无取消或版本复核。:204 UpdateGameContentAsync 在 :208 `var content = await _hoYoPlayService.GetGameContentAsync(CurrentGameId);` 返回后直接 :214 `Banners = content.Banners;`、:222 `PostGroups = GamePostGroup.FromGameContent(content);`，从不比较 _conten…

### B63 · 设置对话框去掉 FlipView 的纵向 ItemsPanel 后，超过 480 高的设置页底部不可达

`Features/GameLauncher/GameLauncherSettingDialog.xaml:161`　置信度 low　**✅ 成立**

- **现状**：游戏设置里内容较长的一页（如启动参数/第三方工具/运行环境项较多的页面）底部控件被裁掉，无法滚动查看或点击。

- **上游/对照**：`Features/GameLauncher/GameLauncherSettingDialog.xaml:148` — 上游用 `<FlipView.ItemsPanel><VirtualizingStackPanel Orientation="Vertical"/></FlipView.ItemsPanel>`，纵向翻转时内容可在 FlipView 的 ScrollViewer 里纵向滚动；移植删掉该 ItemsPanel（默认横向），而 FlipView 模板在横向模式下不提供纵向滚动，页面本身也没有外层 ScrollViewer。对话框固定 Height=480、Padding 底部 0。

- **复核证据**：src/Lunarward/Features/GameLauncher/GameLauncherSettingDialog.xaml:102 Height="480"、:103 Padding="24,24,24,0"；:161 FlipView_Settings 已无 ItemsPanel（上游 src/Starward/Features/GameLauncher/GameLauncherSettingDialog.xaml:148-152 是 VirtualizingStackPanel Orientation=Vertical）；全文仅 :659/:681 两处 ScrollViewer，都在游戏包体页内部，无外层滚动。code-behind :93 `scrollViewer.PointerWheelChanged += (_, e) => e.Handled = true;` 把滚轮事件全部吃掉。

### B64 · chunk 更新分支的百分比文字用下载总量做门禁，与进度环（写盘进度）不同步

`Features/GameLauncher/StartGameButton.xaml.cs:275`　置信度 low　**❌ 不成立**

- **现状**：分块更新时进度环与「xx.x%」文字不同步，或环已转起来而百分比空白。

- **上游/对照**：`Features/GameLauncher/StartGameButton.xaml.cs:272` — 第 271-275 行 chunk 分支里 progress 改按 Progress_WriteTotalBytes 计算，ProgressRingValue 直接用它，但 `ProgressPercentText = total > 0 ? $"{progress:P1}" : ""` 的 total 仍是 Progress_DownloadTotalBytes。下载总量为 0（chunk 预下载/更新刚开始）时环有值、文字为空；反之两者分母不同也可能显示不一致。

- **复核证据**：StartGameButton.xaml.cs:261 `long total = task.Progress_DownloadTotalBytes;`、:271-272 用 Progress_WriteTotalBytes 算 progress、:275 `ProgressPercentText = total > 0 ? $"{progress:P1}" : "";`；但 Linux 全部进度都经 src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs:294-301 Report 写入，其中 DownloadTotal=WriteTotal=progress.TotalBytes、DownloadFinish=WriteFinish=progress.FinishedBytes，二者恒等；:500-501 收尾同样对齐。GameInstall…
