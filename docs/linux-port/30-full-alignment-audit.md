# 30 · 全量文件级对齐复核（2026-11-26）

> 最后核对：2026-11-26（移植 HEAD `2b1e7b46`，上游 `48b0cd90` / 0.19.0 已合入，merge-base `747ed10b`）
> 口径：`src/Starward` 与 `src/Lunarward` 同名文件逐行对读 + 差异全文判读 + 两侧独有文件逐个定性，**无抽样**
> 状态：复核完成，修复计划待拍板（本批只读，未改代码）

---

## 0. 这份文档是什么

2026-11-25 的文档全量重算（D133）与可避免偏离回改（D134）之后，用户要求「再次逐文件检查、严禁抽查、暂不考虑旧兼容性、允许破坏式更新」。本轮按六个模块 + 三组专项做了**穷举式复核**，产出两类东西：

1. **判定**：78 个同名差异文件逐条判读（差异 5512 行，其中删除 1294 / 新增 4218），16 个「仅上游存在」与 149 个「仅移植存在」逐个定性；
2. **修复计划**：分 7 批、约 60 个条目，每条给出位置、现象、修法与风险。

同时**推翻了上一轮六条结论**（详见 §3）——其中两条直接改变了「哪些文件该存在」的判断。

## 1. 方法与可复现命令

| 步骤 | 做法 | 命令/产物 |
| --- | --- | --- |
| 1 | 两侧文件全量枚举 | `find src/Starward -type f`、`find src/Lunarward -type f` → `inventory/upstream.txt`、`inventory/port.txt` |
| 2 | 同名文件分类 | `cmp -s` 判相同/不同 → `identical.txt`（257）、`differing.txt`（78） |
| 3 | 差异全文导出 | `diff -U0 <up> <port>` 只留 `@@` 与 `+`/`-` 行 → `diffs/*.u0`（6113 行） |
| 4 | 逐条判读 | 1294 条删除行分批读完（删除侧是「上游行为丢失」的锚点），新增行判「平台替换/移植新功能/无谓改写」 |
| 5 | 独有文件定性 | 16 个逐个判「必须移植/可选/不需要/不可移植」，149 个判「KEEP-NEW/KEEP-LINUX/DEAD/REDUNDANT」 |
| 6 | 构建归属核对 | `<Compile Remove>` 47 条 + `dotnet msbuild -getItem:Page`，确认「谁在真正编译」——很多同名文件是死代码 |
| 7 | 交叉验证 | 关键结论用 `grep -n` / `sed -n` / `git log -1` 复核，脚本可复跑（§附录 E） |

**基线事实**：`<Compile Remove>` 47 条；`*.Linux.cs` 31 个；`*.LinuxAuto.cs` 5 个；`Lang.resx` 1393 键；`dotnet test` 365 条；`--selftest-ui` 12 条断言。

## 2. 清点结果

| 项 | 数量 | 说明 |
| --- | ---: | --- |
| 上游 `src/Starward` | 351 文件 | 0.19.0 已合入 |
| 移植 `src/Lunarward` | 484 文件 | 独有 149 |
| 同名文件 | 335 | 相同 257 / 不同 78 |
| 仅上游存在 | 16 | 详见附录 B |
| 仅移植存在 | 149 | 详见附录 C |
| 差异文件的变更行 | 5512 | 删 1294 / 增 4218 |
| 判为「功能损失 = 是」的条目 | 40 | 分布在 78 文件中的约 30 个文件 |
| 本轮审计报告 | 8 份 / 2368 行 | 原文见 `30-appendix/` |

变更行数前 10：`GameSettingPage.xaml.cs` 491、`GameLauncherPage.xaml.cs` 393、`WindowsDeviceFpService.cs` 387、`App.xaml.cs` 358、`GameSettingService.cs` 327、`GameInstallService.cs` 292、`MainWindow.xaml.cs` 291、`ScreenshotPage.xaml` 260、`MainView.xaml.cs` 255、`PlayTimeRecordService.cs` 234。

## 3. 必须更正的六条旧结论

| # | 旧文档写的 | 事实 | 需改的文档 |
| --- | --- | --- | --- |
| E1 | 「上游 0.19.0 删掉了截图二代/批量转换/看图窗/公告窗/HDR 对话框」（D134 依据） | 这 16 个文件在上游 `747ed10b 0.19.0` **全部仍在**（`git log -1` 显示被 0.19.0 改过）。移植侧按错误前提删了 15 个；只有 `GameNoticeWindow` 是 D22 主动删 | `12-shims-and-gaps.md:178`、`:245/:257-260`、`00-decisions.md` D134 段、`18-upstream-deviations.md` |
| E2 | 「`Grid` 没有 `ContextFlyout`（CS0117）」 | `BackgroundViewPage.xaml:188` 的 `<Grid.ContextFlyout>` 编译正常。真错因是我调错了 API（`SetContextFlyout` 不是 `ContextFlyout` 的附加属性方法） | `ScreenshotPage.Linux.cs:315` 注释、`25-deviation-register.md` 截图条目 |
| E3 | 「`PointerCursor` 去掉 `partial` 会 CS0260」 | 全仓无第二处声明，上游那份不参与编译，没有依据 | `Helpers/PointerCursor.cs:7` 注释 |
| E4 | `25:2557`「X11 下窗口父子关系由 Helpers/X11Window 管」 | `Helpers/X11Window.cs` 与 `Features/WindowBase.Linux.cs` 搜 `Owner`/`Transient`/`WM_TRANSIENT`/`_NET_WM_STATE` **0 命中**，`WindowHandle` 恒 `IntPtr.Zero` → 登录窗真的没有父子/模态 | `25-deviation-register.md:2557`、`18-upstream-table.md:43`（由「仅格式或轻微」改为「行为丢失」） |
| E5 | `25:519-560`「`GameAccountService` 读路径回落 `~/.wine` 属 avoidable」 | D134（`602475c5`）已改为传游戏前缀，只剩 `null` 回落（＝F-11） | `25-deviation-register.md:519-560`、`:869-876` |
| E6 | 「`ScreenshotSetting` 整页空白是因为 XAML 没加载」以外，还有一处把 `IsTextTrimmedChanged` 删除说成「清理悬空处理器」 | 它是 `PageBase` 的 protected 方法，删掉后长路径不再自动缩字号 | `FileManageSetting.xaml:55-61` 恢复该属性 |

## 4. 修复批次

七个批次，建议执行顺序 **B1 → B3 → B4 → B2 → B5 → B6 → B7**（B1 是已在工作区、只差验证的一批；B2 工作量最大且有产品取舍，放后面）。

### B1 · 工作区已改、待验证（12 项）

`git status` 显示 14 个文件（13 改 + 1 新），`dotnet build` Debug 0 错，但最后一次编辑后**没跑测试与自检**。

| 项 | 位置 | 现象 | 修法 |
| --- | --- | --- | --- |
| B1-01 | 新增 `Helpers/SessionLockWatcher.Linux.cs` + `App.xaml.cs:401` | 锁屏后视频壁纸**继续解码**（`MainWindowStateChangedMessage.SessionLock` 在 Linux 上无发送方） | 订阅 `org.freedesktop.ScreenSaver.ActiveChanged` 与 logind `Session.Lock/Unlock`，发同一条消息 |
| B1-02 | `AppConfig.Configuration.cs` | `GameInstallEnv.CacheFolder` 未赋值 → 清单缓存写 `~/.local/share/Starward/game/…` | 补 `GameInstallEnv.CacheFolder = CacheFolder;` |
| B1-03 | `AppConfig.Configuration.cs` 初始化 catch | 只 `Debug.WriteLine` + `Exit(0)` → Release 双击无反应 | stderr + `Serilog.Log.Fatal` 再退出 |
| B1-04 | `GameSettingService.SetGenshinHDRLuminanceAsync` | 写入失败被 `WineRegistry` 吞 → 界面成功、游戏没变 | 改 `SetValuesAsync`，false 抛 `SetGameSettingFailed` |
| B1-05 | `GameSettingPage.xaml.cs` | `DisplayInformation` 切游戏重复新建，旧实例不退订不 Dispose | 退订 + `Dispose()` |
| B1-06 | `GameSettingPage.xaml.cs` | 方案/伴随刷新跑在 bh3_global 映射**之前**，国际服 BH3 取错方案 | 移到映射之后 |
| B1-07 | `GameLauncherPage.xaml` | 安装进度条 `Maximum="1"`，后台写 `PercentProgress*100` → 恒满 | 改 `"100"` |
| B1-08 | `GameInstallService.AddOrUpdateProgress` | 只合并 7 字段，暂停/继续后速度、字节、剩余时间全是旧值 | 按上游拷全 21 字段 |
| B1-09 | `GameInstallService.ToRequest` | 丢 `PassportUserInfo`（beta 通道）与 `CutsceneBlockList` | 改实例方法，补两字段（新增 `_cutsceneBlockLists`） |
| B1-10 | `GameLauncherService` 启动日志 | 打印完整参数行，含 `login_auth_ticket` 明文 | `RedactLaunchArgument` 打码 |
| B1-11 | `GameBannerAndPost.xaml.cs` | 容器 `Tapped` + 图片 `AddHandler(handledEventsToo)` 都不 `e.Handled` → 开两次 | 同链接 400 ms 内只开一次 |
| B1-12 | `App.xaml` | `MenuFlyoutSeparatorBackground` 与面板同色 → 分隔线不可见 | 删该覆盖 |

**遗留**：`GameLauncherService` 的 `verb` 死变量、`:515-519` 两段互相矛盾的 HDR 注释还没清（补丁脚本语法错未落地）。

### B2 · 16 个「仅上游存在」文件的处置

| 判定 | 文件 | 说明 |
| --- | --- | --- |
| **必须移植（2）** | `Features/Screenshot/ImageViewWindow2.xaml` / `.xaml.cs` | 上游唯一在用的看图窗，三入口（截图页双击 `ScreenshotPage.xaml.cs:384/467`、工具箱 `ToolboxSetting.xaml.cs:41/85-87`、截图 OSD `ScreenCaptureInfoWindow.xaml.cs:103`）。移植侧只有 `ScreenshotViewerWindow` 约 150 行，缺缩放/适应/全屏/画廊/复制（文件·路径·图像）/删除/另存/拖放/图像信息。`CanvasSwapChainPanel`、`CanvasSwapChain`、`DisplayInformation.AdvancedColorInfo`、`User32.GetDpiForWindow`、`Shcore`、`cwc:Segmented` 均不能搬 → 用 `ScrollViewer ZoomMode` + `Image` + `Helpers/ImageDecode` 重写，**≈600–800 行**，HDR 部分留 M4 |
| 可选（4） | `ImageBatchConvertWindow.xaml(.cs)` | 上游 `ScreenshotPage.xaml:267` 在用；移植侧「复制为 JPG」已能单张转。做则 ≈400–500 行（格式 PNG/AVIF/JXL × 质量 × 覆盖策略 × 输出目录） |
| 可选（4） | `ScreenCaptureInfoWindow.xaml(.cs)` | 上游是滑入 OSD 缩略图；移植是桌面通知。做则 ≈250–350 行，需给 `Helpers/X11Window` 补 `_NET_WM_STATE_ABOVE` + 不抢焦点 |
| 不需要（9） | `GameNoticeWindow.xaml/.cs` + `GameNoticeWindowClosedMessage.cs` | D22/D64：公告改系统浏览器打开 |
| 不需要（9） | `ImageViewWindow.xaml(.cs)`、`ScreenshotPage2.xaml(.cs)`、`TestUrlProtocolWindow.xaml(.cs)` | 上游零引用或不可达；`ScreenshotPage2` 还依赖 `ItemsView`（Uno 无模板） |
| 已替代（1） | `GenshinHDRLuminanceSettingDialog.xaml` | 由 `.Linux.cs` 程序化重建 |

**验收**：三条入口都落到新窗（截图页双击、工具箱「图片查看」、`OpenItem`）。

### B3 · 功能缺失与缺陷（35 项）

分五组，编号 F-xx 便于台账引用。**除注明外均为「上游有、移植丢了」，不是平台能力问题。**

#### B3-a 截图（8）

| 编号 | 位置 | 现象 | 修法 |
| --- | --- | --- | --- |
| F-01 | `ScreenshotPage.xaml:59-60` | 拖出到文件管理器整体丢失（无 `DragItemsStarting`） | 补 `DragItemsStarting` → `DataPackage.SetStorageItems` |
| F-02 | `ScreenshotPage.Linux.cs:100-115` | 不认游戏内截图目录（上游覆盖 hk4e `ScreenShot`、hkrpg `StarRail_Data\ScreenShots`、bh3/nap `ScreenShot`）→ 游戏内 F12 截图不进图库 | 移植 `GetGameScreenshotPathAsync` 的四个分支 |
| F-03 | `ScreenshotPage.Linux.cs:160/200-233` | `FileSystemWatcher` 永不释放（`NavigationCacheMode="Required"` 且无 `OnUnloaded`） | `OnUnloaded`/`OnNavigatedFrom` 释放并置空 |
| F-04 | `ScreenshotPage.Linux.cs:337` | 「复制为 JPG」图标 `\uF413`（豆腐块），对话框已是 `E74E` | 统一 `E74E` |
| F-05 | `ScreenshotPage.xaml:68-78` | 卡片圆角只声明不裁剪 | 加 `ClipToBounds` 或裁剪层 |
| F-06 | `ScreenshotPage.xaml:76-83` | 条目无手型指针 | 补 `PointerCursor.CursorShape="Hand"` |
| F-07 | `ScreenshotPage.Linux.cs:315` | 注释称「Uno 的 Grid 没有 ContextFlyout」，并因此改用 code-behind 现造面板 | 改注释；若愿意，右建菜单可回归 XAML `ContextFlyout`（E2） |
| F-08 | `BackgroundService.cs:313-322` | 视频背景改魔数嗅探后，坏视频可被持久化 → 黑屏 | 选完补一次「首帧能否解出」的异步校验 |

#### B3-b 设置页（11）

| 编号 | 位置 | 现象 | 修法 |
| --- | --- | --- | --- |
| F-09 | `ScreenshotSetting.Linux.cs:189-200` | **截图热键三个事件处理器全空** → 改热键不注册、不落盘、失败无提示 | 照上游 `ScreenshotSetting.xaml.cs:68-110` 补（`HotkeyManager` 的 Linux 实现 API 同名齐备） |
| F-10 | `ScreenshotSetting.Linux.cs:98-105` | ctor 缺 `HotkeyManager.InitializeHotkeyInput(...)` → 页面显示「点击设置快捷键」而 `Alt+D` 实际生效 | 补一行 |
| F-11 | `ScreenshotSetting.xaml:134-149` + `ScreenCapture.Linux.cs` | 颜色管理/SDR 两个开关只写不读，用户开了没效果 | 要么实现 SDR 转换，要么置灰并说明 |
| F-12 | `FileManageSetting.xaml.cs:99/222-228/268-275` | 仍用 WinRT `Launcher.LaunchUriAsync/LaunchFolderAsync` 与 `StorageFile.GetFileFromPathAsync`，Linux 静默无反应 | 改 `Helpers.OpenUrl.Open` + `Process.Start("xdg-open")` |
| F-13 | `AboutSetting.xaml:100/107` | 「报告问题 / 发布历史」指向 `Scighost/Starward` | 改 `QiE2035/Lunarward` |
| F-14 | `FileManageSetting.xaml:55-61` | 误删 `IsTextTrimmedChanged`（`PageBase` 的 protected 方法）→ 长路径不再缩字号 | 恢复属性 |
| F-15 | `DiagnosticBundle.cs:33-35` | 诊断包写死 `~/Downloads`，而「打开文件夹」用 `XDG_DOWNLOAD_DIR` → 生成后看不到 | 统一走 `XDG_DOWNLOAD_DIR` |
| F-16 | `ScreenshotSetting.Linux.cs:95` | `WindowHandle` 是 `string?` 且恒 null，上游是 `nint` | 改用 `XamlRoot.GetWindowHandle()` |
| F-17 | `ScreenshotSetting.Linux.cs:203-209` | 与 `PageBase` 重复的私有方法 | 删 |
| F-18 | `AdvancedSetting.xaml:46`、`AdvancedSetting.xaml.cs:80`、`ScreenshotSetting.xaml:84`、`AboutSetting.xaml:28` | 4 处硬编码中文（其中两处 Lang 键已存在） | 走 `Lang` |
| F-19 | `DownloadSetting.xaml.cs:2-10` 等 | 7 个无用 using、2 个零引用 `x:Name` | 清理 |

#### B3-c 账号 / 登录（8）

| 编号 | 位置 | 现象 | 修法 |
| --- | --- | --- | --- |
| F-20 | `HypLoginWindow.xaml.cs:71-78` | `GWL_HWNDPARENT` 被 `IsWindows()` 守掉且无替代 → 登录窗不随主窗关、可能被遮、无模态（**行为丢失**，E4） | 给 `X11Window` 补 `WM_TRANSIENT_FOR`（父窗 id 用 `ParentWindowHandle`），或至少 `_NET_WM_STATE_MODAL` |
| F-21 | `WindowsDeviceFpService.cs:411-417` | `GetScreenSize()` 恒 `"unknown"`（注释理由「xrandr 会 fork」不成立） | 用 `Helpers/X11Window.TryGetScreenSize` |
| F-22 | `WindowsDeviceFpService.cs:289-318` | NVIDIA 分支显存读 `/sys/class/drm/card0/device/mem_info_vram_total`（那是 amdgpu 节点）；`long.Parse` 一抛异常连已取到的型号一起丢 | 分开取、逐项 try、`long.TryParse` |
| F-23 | `HypLoginWindow.xaml.cs:65/129` → `UrlProtocolService.Linux.cs:49-84/185-194` | GUI 线程同步 fork + `WaitForExit(5000)` 两次（违反硬规则 10） | 照 `Program.Linux.cs:1628-1640` 的 `Task.Run` 写法 |
| F-24 | `UrlProtocolService.Linux.cs:150-183` | `IsProtocolRegistered` 先 `ReadToEnd()` 无超时再 `WaitForExit(3000)` → 超时形同虚设 | 先 `WaitForExitAsync` 或 `ReadToEndAsync` + CTS |
| F-25 | `App.xaml.cs:290` | 深链丢 `install_path` 与路径式 biz，错误路径静默 | 对齐上游 `HandleUrlProtocolAsync` |
| F-26 | `GachaLogExportFile.cs:39`、`ZZZGachaService.cs:550`、`UIGF3File.cs:39`、`UIGF4File.cs:77` | `export_app` 由 `"Starward"` 改 `"Lunarward"` → 第三方导入器白名单风险 | **需产品决策**（见 §6） |
| F-27 | `DatabaseService.cs:108` | 自动备份目录仍是 `%APPDATA%\Starward\DatabaseBackup`（= `~/.config/Starward/`），与 `~/.local/share/lunarward` 及手动备份分裂 | 统一到 `AppConfig.UserDataFolder` 下 |

#### B3-d 游玩时长 / 数据库（4）

| 编号 | 位置 | 现象 | 修法 |
| --- | --- | --- | --- |
| F-28 | `PlayTimeRecordService.cs:75-78` vs `:152-186` | pid 复用保护**时区错位**：`GetProcessStartTime` 返回 `UtcDateTime`，`GetLastStartTime` 返回本地 `DateTime`，直接比较 → 东八区保护恒失效（空闲算游玩），负时区恒成立 | 统一 `DateTimeOffset` 比较 |
| F-29 | `:189-208` | 取「任意 State 最新一条」而非注释所称 Start → 回退路径可能拿 Stop/Error 当起点，时长≈0 | 加 `AND State = 1` |
| F-30 | `:150-151` | 双 `/// <summary>` | 删一条 |
| F-31 | `playtime` 子命令 | 已在 GUI 记录时手动再跑会双记 | 记录 `_recordingPids` 已有，跨进程需文件锁或 DB 判重 |

#### B3-e 抽卡 / 战绩（4）

| 编号 | 位置 | 现象 | 修法 |
| --- | --- | --- | --- |
| F-32 | `GachaLogPage.xaml.cs:66` → `CloudGameGachaWindow.xaml.cs:71-72` | **云游戏抽卡菜单无条件可见，点了会让进程直接退出**（`ExecuteScriptAsync` 在 Uno/WebKitGTK 下 G0-3b 实测崩溃） | Linux 下恒 `Collapsed`（紧急，属可触发崩溃） |
| F-33 | `BBSWebBridge.Linux.cs`（26 行 stub）+ `GameRecordPage` | BBS 战绩详情整座桥是空壳，但页签仍高亮可点 | Linux 下禁用该页签并给说明（引擎能力，见 §5） |
| F-34 | `ZZZGachaInfoWindow.xaml.cs:68-72/151/195` | 唯一数据入口是未实现的 `WebResourceResponseReceived`，1 行 diff 掩盖整窗失效 | Linux 下隐藏该入口或改走 HTTP |
| F-35 | `GameRecordPage.xaml.cs:92` + `:112-165` | `CheckAgreementAsync` 只在切游戏时弹，首屏不弹；上游 0.19.0 已删该交互 | 整块删 |

### B4 · DI 生命周期对齐（1 项）

上游把 `HoYoPlayService`/`GameResourceService`/`GameConfigService`/`GenshinDispatchClient`/`GenshinHotUpdateService` 注册为 **Singleton**；移植改成 `sc.AddStarwardGameInstall()`（**Scoped**），且 `BuildServiceProvider()` 没开 `ValidateScopes`：

- 12 处 `AppConfig.GetService<HoYoPlayService>()` 从根解析 = captive dependency；
- 与 `GameInstallRunner.cs:162` 自建 scope 解析出的实例**不是同一个** → 缓存/登录态可能分裂。

**修法**：这些服务在上游是 Singleton 的，注册也改回 Singleton（`GameInstallServiceCollectionExtensions.cs` 里保留 Scoped 的只有真正短命的 Download/Http 类）。

**风险**：改变 `GameInstallRunner` 的行为，需连带跑 `scripts/test/chunk-smoke.sh`（离线安装链路）。

### B5 · 死代码清理（28 项）

24 DEAD + 4 REDUNDANT，明细见附录 C。分三组删除：

1. **仓库卫生（10）**：`src/Lunarward/.idea/**`（5）、`.junie/mcp/mcp.json(+.bak)`、`Lunarward.csproj.user`、`GenshinHDRLuminanceSettingDialog.xaml.disabled`、`LoginPage.xaml.cs`（已被 Compile Remove）；
2. **零调用死代码（14）**：`GamePackageService.cs`(181)、`GameAuthLoginButton.xaml(.cs)`+`GameAuthLoginService.cs`、`RpcService.Linux.cs`、`ScreenshotWindowsDisabled.cs`、`SetupService.Linux.cs`、`Housekeeping.cs`、`WindowWaitAsync.Linux.cs`、`Win2DCompat.cs`、`TaskbarIconCompat.cs`+`SystemBackdropHelper.Linux.cs`+`SystemTrayWindow.*`（Windows-only 死链，需一次原子改动）、`VideoBgVolumeChangedMessage.cs`；
3. **登录页簇（4）**：`LoginPage.xaml/.Linux.cs/.LinuxAuto.cs/.xaml.cs` + `GameRecordService.cs` + `GameRecordRoleChangedMessage.cs`（`GameRecordPage.xaml:504` 改 `LoginCommand` 后整簇退役）——**删前需真机验证一次 `hyp://` 回填**。

### B6 · 文档校准（6 组）

1. **§3 的六条更正**：`12-shims-and-gaps.md`、`25-deviation-register.md`、`18-upstream-table.md`、`00-decisions.md` D134 段；
2. **D135 决策**：本批复核的定性（16 个文件的处置、DI 生命周期、死代码、六个更正）与代价边界；
3. **新增 L-191…**：`Compile Remove` 的文件上留着「已适配」改动会误导同步（D9）、Serilog 缓冲导致「日志判据」失效、`ContextFlyout` 用法；
4. **统计行**：`25` 的 78 个同名文件 / 16 / 149 / DEAD 24 等新数字，以及 `12` 的 47 条排除（D134 后未变）；
5. **库工程两处卫生**：`Starward.Core.csproj` 与 `Starward.GameInstall.csproj` 各留了一个**空 `<ItemGroup>`**（见附录 D），`Starward.HPatch` 的包顺序也被无意义搬动；
6. **CHANGELOG** 与 `AGENTS.md`/`README.md` 的版本行。

### B7 · 验证与提交

| 层 | 命令 | 期望 |
| --- | --- | --- |
| 构建 | `dotnet build Starward.slnx -c Debug` / `-c Release` | 0 错 |
| 单测 | `dotnet test Starward.slnx -c Debug` | 365 + 新增 |
| 自检 | `Xvfb :9x` + `LUNARWARD_SELFTEST=1 … --selftest-ui` | 12/12 |
| lint | `lint-ui-thread`、`lint-doc-status --strict`、`lint-cli-help`、`lint-pkgbuild` | 全绿 |
| CLI | `cli-smoke`（40+1）、`cli-extra`（28+4） | 全绿 |
| 安装链路 | `scripts/test/chunk-smoke.sh` | 全绿（B4 必跑） |
| publish 敏感 | `dotnet publish src/Lunarward/Lunarward.csproj -c Release -r linux-x64 --self-contained true -p:DebugType=none` | 0 错 |

**教训（D134 踩过）**：本地 `net11.0-desktop` 构建过 ≠ CI 能过。改了 `RectInt32` 构造、`WindowActivationState` 这类跨 projection 类型，必须先跑上面那条 publish。

## 5. 明确保留、不要回退

这些看着像偏离，但有依据，**下一轮不要顺手改回去**：

| 项 | 理由 |
| --- | --- |
| 4 处 `NavigationCacheMode` 及配套 `OnGameChanged`/重置 | 页面实例被缓存，不复位会串游戏数据（B18/B19/B48） |
| `PageBase.PageEx_Unloaded` 不再退订 Loaded/Unloaded | 退订后缓存页面再进来 `OnLoaded` 不执行（D135） |
| `OnUnloaded` 删两行 `Tapped -=` | 上游那两行会摘掉 XAML 接线，是真 bug |
| `ItemsControl+WrapPanel` 替 `ItemsRepeater+WrapLayout` | Uno 的 `WrapLayout` 不工作 |
| 负数常量/图标码位/`HH\\:mm` 格式串 | Uno 生成器与图标字体要求 |
| BCL zip 备份（`.zip` 替 `.7z`） | 不引入 7z 依赖 |
| `UpdateDeviceInfoAsync` 恢复真实取指纹 | 上游 0.19.0 退化成假提示 |
| BBS 桥、HDR 预览、客户区截图、只抓游戏窗口、HDR→SDR 编码 | 引擎能力缺口（软差异），已在 `12-shims-and-gaps.md` 登记 |

## 6. 需要拍板的四件事

| # | 事项 | 选项 | 我的建议 |
| --- | --- | --- | --- |
| 1 | B1 现在跑全量验证（不提交）？ | 是 / 先攒着一起验 | **是**：改到了安装进度与 HDR 写入，越早发现越好 |
| 2 | `ImageViewWindow2` 重写（600–800 行）本轮做？ | 全做 / 只做缩放+全屏+右键菜单 / 推到下轮 | **本轮全做**：双击截图看图是主路径，现在只有翻页 |
| 3 | DI 改回 Singleton 现在动？ | 现在 / 与 B3 合并 / 推到下轮 | **与 B3 之后单独一笔**，连带 `chunk-smoke` |
| 4 | DEAD 24 + REDUNDANT 4 一起删？ | 全删 / 只删零风险组（仓库卫生 + Windows-only 死链）| **先删零风险两组**；登录页簇等真机验证 `hyp://` 后再删 |

另有两项产品取舍，我倾向但不替你定：**F-26 `export_app` 是否保留 `Starward`**（影响第三方导入器兼容）、**F-33 BBS 页签是否隐藏**（隐藏=少一个假入口，保留=占位说明）。

## 附录 A · 78 个同名差异文件索引

按变更行数降序；每行指向逐条判读的原始报告。

| 文件（相对 `src/Lunarward/`） | 变更行 | 复核报告 |
| --- | ---: | --- |
| `Features/GameSetting/GameSettingPage.xaml.cs` | 491 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/GameLauncher/GameLauncherPage.xaml.cs` | 393 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/Passport/WindowsDeviceFpService.cs` | 387 | [a3-account-passport.md](30-appendix/a3-account-passport.md) |
| `App.xaml.cs` | 358 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/GameSetting/GameSettingService.cs` | 327 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/GameInstall/GameInstallService.cs` | 292 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/ViewHost/MainWindow.xaml.cs` | 291 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/Screenshot/ScreenshotPage.xaml` | 260 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/ViewHost/MainView.xaml.cs` | 255 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/PlayTime/PlayTimeRecordService.cs` | 234 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/GameRecord/GameRecordPage.xaml.cs` | 219 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `AppConfig.Configuration.cs` | 197 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/GameLauncher/GameLauncherService.cs` | 164 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/Setting/AdvancedSetting.xaml.cs` | 162 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/GameLauncher/GameBannerAndPost.xaml.cs` | 157 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/Background/BackgroundViewPage.xaml.cs` | 157 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/Background/BackgroundService.cs` | 116 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/GameSetting/GameSettingPage.xaml` | 93 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/GameAccount/GameAccountService.cs` | 76 | [a3-account-passport.md](30-appendix/a3-account-passport.md) |
| `Features/Setting/SettingPage.xaml.cs` | 69 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/Database/DatabaseService.cs` | 57 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/Setting/AdvancedSetting.xaml` | 52 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/ViewHost/SystemTrayWindow.xaml.cs` | 44 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `AppConfig.Common.cs` | 40 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/GameLauncher/GameBannerAndPost.xaml` | 35 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/Background/BackgroundViewPage.xaml` | 35 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/Gacha/GachaLogPage.xaml.cs` | 35 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/Background/BackgroundViewWindow.xaml.cs` | 33 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml` | 30 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/PageBase.cs` | 29 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/GameRecord/StarRail/SimulatedUniversePage.xaml` | 27 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/GameLauncher/GameNoticeService.cs` | 25 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/Setting/FileManageSetting.xaml.cs` | 25 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/GameLauncher/GameLauncherPage.xaml` | 24 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/GameRecord/GameRecordPage.xaml` | 24 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `AppConfig.ServiceProvider.cs` | 23 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/Setting/GeneralSetting.xaml` | 22 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/Background/BackgroundFileItem.cs` | 20 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/Setting/GeneralSetting.xaml.cs` | 19 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/Setting/DownloadSetting.xaml.cs` | 19 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/GameAccount/GameAccountSwitcher.xaml.cs` | 18 | [a3-account-passport.md](30-appendix/a3-account-passport.md) |
| `Features/ViewHost/MainView.xaml` | 16 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/Setting/SettingPage.xaml` | 16 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/Setting/ScreenshotSetting.xaml` | 12 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/PlayTime/PlayTimeButton.xaml.cs` | 12 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `App.xaml` | 10 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/ViewHost/WelcomeWindow.xaml.cs` | 9 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/Setting/GamepadControlSetting.xaml` | 9 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/Passport/HypLoginWindow.xaml.cs` | 7 | [a3-account-passport.md](30-appendix/a3-account-passport.md) |
| `Features/Screenshot/ScreenshotPage.xaml.cs` | 6 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/Setting/AboutSetting.xaml` | 6 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/GameInstall/PreDownloadButton.xaml.cs` | 5 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/Gacha/GenshinBeyondGachaPage.xaml` | 5 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/GameInstall/PreDownloadButton.xaml` | 4 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/GameInstall/InstallGameDialog.xaml` | 4 | [a2-launch-install-setting.md](30-appendix/a2-launch-install-setting.md) |
| `Features/Setting/HotkeySetting.xaml` | 4 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/GameRecord/DailyNote/DailyNoteButton.xaml` | 4 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/SelfQuery/SelfQueryPage.xaml.cs` | 4 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/Update/UpdateWindow.xaml` | 3 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Helpers/PointerCursor.cs` | 3 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/Setting/FileManageSetting.xaml` | 3 | [a5-settings.md](30-appendix/a5-settings.md) |
| `Features/Gacha/GachaLogPage.xaml` | 3 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/SelfQuery/SelfQueryPage.xaml` | 3 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Program.cs` | 2 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/ViewHost/SystemTrayWindow.xaml` | 2 | [a1-app-and-shell.md](30-appendix/a1-app-and-shell.md) |
| `Features/Screenshot/ScreenCaptureService.cs` | 2 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/Screenshot/ScreenshotFolderManageDialog.xaml` | 2 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/Codec/ImageThumbnail.cs` | 2 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Controls/CachedImage.cs` | 2 | [a4-screenshot-background.md](30-appendix/a4-screenshot-background.md) |
| `Features/Gacha/GachaLogExportFile.cs` | 2 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/Gacha/ZZZGachaService.cs` | 2 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs` | 2 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/Gacha/UIGF/UIGFGachaService.cs` | 2 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/Gacha/UIGF/UIGF3File.cs` | 2 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/Gacha/UIGF/UIGF4File.cs` | 2 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/CloudGame/CloudGameButton.xaml.cs` | 2 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/PlayTime/PlayTimeStatsDialog.xaml.cs` | 2 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |
| `Features/UrlProtocol/UrlProtocolService.cs` | 2 | [a6-gacha-record-misc.md](30-appendix/a6-gacha-record-misc.md) |

## 附录 B · 16 个「仅上游存在」文件

见附录 A7 `a7-upstream-only.md` 的完整表；结论见 §B2。要点：**必须移植 2 个文件**（`ImageViewWindow2.xaml/.cs`），可选 4 个，不需要 9 个，已替代 1 个，不可移植 0 个。

## 附录 C · 149 个「仅移植存在」文件

`a8-lunarward-only.md` 给出 149/149 的逐个定性（KEEP-NEW 96 / KEEP-LINUX 25 / DEAD 24 / REDUNDANT 4 / REVIEW 0）。下表是需删除的 28 个。

### C.1 DEAD（参与编译或入库但零存活调用点）

| 文件 | 判据 | 处置 |
| --- | --- | --- |
| `.idea/.idea.Lunarward.dir/.idea/.gitignore` | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证） | 直接删（IDE 本地状态，本不该入库） |
| `.idea/.idea.Lunarward.dir/.idea/indexLayout.xml` | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证） | 直接删（IDE 本地状态，本不该入库） |
| `.idea/.idea.Lunarward.dir/.idea/projectSettingsUpdater.xml` | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证） | 直接删（IDE 本地状态，本不该入库） |
| `.idea/.idea.Lunarward.dir/.idea/vcs.xml` | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证） | 直接删（IDE 本地状态，本不该入库） |
| `.idea/.idea.Lunarward.dir/.idea/workspace.xml` | 非 .cs/.xaml，MSBuild 的 Compile/Page item 里都没有它（-getItem 查证） | 直接删（IDE 本地状态，本不该入库） |
| `.junie/mcp/mcp.json` | 非编译项、零引用 | `git rm -r --cached src/Lunarward/.junie` 后从工作区删除 |
| `.junie/mcp/mcp.json.bak` | 非编译项、零引用 | `git rm -r --cached src/Lunarward/.junie` 后从工作区删除 |
| `Compat/NotifyIcon/TaskbarIconCompat.cs` | H.NotifyIcon 包未引用，本文件纯占位 `TaskbarIcon` | 与 SystemTrayWindow.xaml/.cs 一并从 Page/Compile 中排除后删除（一次原子改动） |
| `Controls/Win2DCompat.cs` | 提供 `CanvasSwapChainPanel` 占位 | 删（连带 GenshinHDR 的 Win2D 渲染路径已由 .Linux.cs 程序化重建） |
| `Features/Background/VideoBgVolumeChangedMessage.cs` | 全仓只有类定义，无任何 `Send(`/`Register<` | 删 |
| `Features/GameInstall/GamePackageService.cs` | 181 行旧 Sophon 安装管线（Hi3Helper.Sophon 直连、自研 chunk 下载） | 删（与 D128 一致） |
| `Features/GameLauncher/GameAuthLoginButton.xaml` | 作为 Page item 参与编译，但全仓没有任何 XAML 放置该控件（`GameAuthLoginButton` 只命中自身与 AppConfig.ServiceProvider.cs:152 的注释） | 删（连同下面两个文件 + AppConfig.ServiceProvider.cs:152-153 注册行） |
| `Features/GameLauncher/GameAuthLoginButton.xaml.cs` | 只被自身 XAML 与 GameAuthLoginService 引用 | 同上 |
| `Features/GameLauncher/GameAuthLoginService.cs` | 唯一引用 AppConfig.ServiceProvider.cs:153 `sc.AddHttpClient<GameAuthLoginService>()`（注释称「GameAuthLoginButton 会解析它」 | 同上（连注册行一起删） |
| `Features/GameRecord/GameRecordRoleChangedMessage.cs` | 唯一 `Send` 在 LoginPage.Linux.cs:61（另一处 LoginPage.xaml.cs:240 已不编译） | 随 LoginPage 一起删；同时撤掉 GameRecordPage.xaml.cs:64-70 的注册 |
| `Features/GameRecord/LoginPage.xaml.cs` | csproj:113 `<Compile Remove="Features/GameRecord/LoginPage.xaml.cs" />` | 删 |
| `Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.disabled` | 后缀是 `.disabled`，不进 Compile/Page | 删（历史停用留档，git 历史里已有） |
| `Features/RPC/RpcService.Linux.cs` | 引用者只有 Features/RPC/RpcRunner.cs 与 RpcService.cs，两者都在 `<Compile Remove>` | 删 |
| `Features/Screenshot/ScreenshotWindowsDisabled.cs` | 只声明占位类 `ImageViewWindow2` | 删 |
| `Features/Update/SetupService.Linux.cs` | 18 行空壳（构造函数空体 + 两个恒 0 属性），存在意义只是让 AppConfig.ServiceProvider.cs:103 `AddSingleton<SetupService>()` 的类型成立 | 删，同时删 AppConfig.ServiceProvider.cs:103 注册行 |
| `Helpers/Housekeeping.cs` | `Run()` 全仓零调用 | 删 |
| `Helpers/SystemBackdropHelper.Linux.cs` | 上游 Helpers/SystemBackdropHelper.cs 在 `<Compile Remove>`，但本文件唯一存活引用是 Features/ViewHost/SystemTrayWindow.xaml.cs | 与 TaskbarIconCompat + SystemTrayWindow 一起「排除编译 + 删除」（同一原子改动） |
| `Helpers/WindowWaitAsync.Linux.cs` | 提供 `WaitAsync(this Window)` 扩展，全仓零调用 | 删 |
| `Lunarward.csproj.user` | IDE 本地状态文件 | 直接删（应进 .gitignore） |

### C.2 REDUNDANT（上游已删、移植自造但仍可路由）

| 文件 | 判据 | 处置 |
| --- | --- | --- |
| `Features/GameRecord/GameRecordService.cs` | 移植自造门面：全仓唯一消费者 LoginPage.Linux.cs:23 `GetService<GameRecordService>()`（+ AppConfig.ServiceProvider.cs:151 注册） | 随 LoginPage 退役一并删（顺带删 :151 注册） |
| `Features/GameRecord/LoginPage.Linux.cs` | 上游 0.19.0 已删整页登录页，登录统一走 Passport/HypLoginWindow | 先真机验证 HypLogin 回填，再把 GameRecordPage.xaml:504 改成 LoginCommand，然后本页整簇删 |
| `Features/GameRecord/LoginPage.LinuxAuto.cs` | 同上 | 同上 |
| `Features/GameRecord/LoginPage.xaml` | MSBuild `-getItem:Page` 命中（确实参与编译，回答了 25-deviation-register.md:1614 的悬案） | 同上 |

## 附录 D · 上游库工程内的移植改动（8 处）

| 文件 | 改动 | 评价 |
| --- | --- | --- |
| `src/Starward.Core/Starward.Core.csproj` | 删掉 `<Using Include="System.Net.Http.Json" />`（net11 隐式已含） | **留下一个空 `<ItemGroup>`，应删** |
| `src/Starward.GameInstall/Starward.GameInstall.csproj` | TFM `net11.0-windows`→`net11.0`（带 `LUNARWARD-DEVIATION`） | 必要；但 `Starward.HPatch` 被搬到新 `<ItemGroup>` 且**又留一个空组**，建议还原顺序并删空组 |
| `src/Starward.Language/Lang.Designer.cs` | `public class Lang` → `public partial class Lang` | 必要（`Lang.Lunarward.cs` 必需） |
| `HotUpdate/GenshinHotUpdateStep.cs` | 去 `Vanara`、`HardLinkCompat.TryCreate`、`GetDispatchRegionInfoAsync` 多 `installPath` | 已登记 D132 |
| `Service/HardLinkService.cs` | `HardLinkCompat.IsSupported`、`Kernel32` 调用包 `IsWindows()` | 已登记 D131 |
| `Service/HardLinkCompat.cs`（新增） | 文件系统能力判定 + `File.CreateHardLink` | 已登记 D131 |
| `Service/WinePrefixRegistry.cs`（新增） | 读 wine `user.reg` 的 REG_BINARY | 已登记 D132 |
| `HotUpdate/GenshinHotUpdateService.cs` | 11 处 `@"Persistent\X"` 拆 `Path.Combine`、`GetServerName` 双平台 | 已登记 D132/L-188 |

## 附录 E · 原始审计报告与复现

| 报告 | 覆盖 | 行数 |
| --- | --- | ---: |
| `30-appendix/a1-app-and-shell.md` | App 底座与外壳 16 文件 | 302 |
| `30-appendix/a2-launch-install-setting.md` | 启动器/游戏设置/安装 13 文件 | 393 |
| `30-appendix/a3-account-passport.md` | 账号与 Passport 4 文件 | 114 |
| `30-appendix/a4-screenshot-background.md` | 截图/背景/控件 12 文件 | 295 |
| `30-appendix/a5-settings.md` | 设置页 11 文件 | 244 |
| `30-appendix/a6-gacha-record-misc.md` | 抽卡/战绩/杂项 22 文件 | 264 |
| `30-appendix/a7-upstream-only.md` | 16 个仅上游文件 | 200 |
| `30-appendix/a8-lunarward-only.md` | 149 个仅移植文件 | 254 |

复现命令：

```bash
# 同名/独有分类
comm -12 <(cd src/Starward && find . -type f | sort) <(cd src/Lunarward && find . -type f | sort)
# 差异全文（只看增删行）
diff -U0 src/Starward/<路径> src/Lunarward/<路径> | grep -E '^(@@|[+-])'
# 谁在真正编译
grep -n "Compile Remove" src/Lunarward/Lunarward.csproj
dotnet msbuild src/Lunarward/Lunarward.csproj -getItem:Page | grep -c 'Identity'
```
