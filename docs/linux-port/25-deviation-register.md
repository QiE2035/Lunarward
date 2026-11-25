# 25 · 偏离点总账（Deviation Register）

> 生成日期：2026-11-25　基线：上游 `src/Starward/` = 0.19.0（远端 `48b0cd90`，与本仓库 merge-base `747ed10b`）；移植 `src/Lunarward/` = 当前工作区（`974a0c27` 之后 + 当日未提交修复）。
> 方法：按 9 个模块（3.1 启动器 … 3.9 服务与帮助器）逐文件 `diff -u` 重算，每组独立读过两侧源码后判定类别与必要性；本文**全量取代** 2026-11-21 初版与 2026-11-22 的 57 条改判表。
> 原则见 `18-upstream-deviations.md`，实现状态见 `12-shims-and-gaps.md`，同名文件清单见 `18-upstream-table.md`（由 `scripts/list-deviations.sh` 生成，只列举不分类）。

## 0 · 口径

- **偏离** = 上游存在的文件在移植侧行为/结构不同。三种来源：①同名文件被修改（76 条逐点记录，另有 4 条只属格式或单行改动，见 §3.10）；②上游文件被 `src/Lunarward/Lunarward.csproj` 的 `<Compile Remove>` 排除、由同目录 `*.Linux.cs` 重写（31 条，排除清单见附录 A）；③移植侧独有的新文件（5 条，如 `Helpers/Portal.cs`）。
- **必要性**：`must` 不这么写程序就不可用（平台 API 不存在、引擎不支持）；`reasonable` 平台不同但语义等价，或为满足 Linux 用户预期；`avoidable` 与平台无关，可以照上游写却写了别的（顺手重构、记忆偏差，或修上游缺陷但没留痕）。
- **可否回流**：这条改动是否值得、也可能提交给上游（多数是平台专属，回不去；判为「修上游缺陷」的见附录 C）。
- 每条都带移植侧当前行号或成员名；`*.Linux.cs` 的对照对象是去掉 `.Linux` 的同名上游文件。
- 一个文件可能出现在两个模块的小节里（例如 `Features/ViewHost/MainView.xaml.cs` 在 3.4 与 3.9 各一次），因为偏离分属不同子系统；按小节读，别按文件数加总。

## 1 · 统计

| 维度 | 数量 |
|---|---|
| 偏离点 | **423** |
| 文件条目 | **113**（`MainView.xaml.cs` 跨模块重复 1 个 → 实际 112 个文件；`*.Linux.cs` 31 + 同名文件 76 + 移植侧独有 5；另有 4 个只属格式/单行的见 §3.10） |
| 类别 | linux 专属实现 100、eng 工程性 95、api 平台 API 65、engine 引擎能力 57、cut 裁剪 51、cosmetic 外观文案 43、bugfix 修上游缺陷 12 |
| 必要性 | reasonable 合理取舍 166、must 必需 158、avoidable 可避免 99 |
| 可回流上游 | 12（附录 C） |

按模块：

| 模块 | 偏离点 | must | reasonable | avoidable | 文件条目 |
|---|---|---|---|---|---|
| 3.1 launcher 启动器与游戏入口 | 46 | 21 | 18 | 7 | 7 |
| 3.2 install 安装 / 更新 / 预下载 | 18 | 7 | 8 | 3 | 5 |
| 3.3 gamesetting 游戏设置 / 运行环境 / 账号 | 40 | 11 | 21 | 8 | 6 |
| 3.4 shell 外壳 | 72 | 48 | 11 | 13 | 16 |
| 3.5 screenshot 截图与图库 | 37 | 8 | 16 | 13 | 9 |
| 3.6 gacha-record 抽卡 / 战绩 | 47 | 19 | 18 | 10 | 19 |
| 3.7 background-codec 背景与图片加载 | 43 | 16 | 17 | 10 | 10 |
| 3.8 settings-ui 设置界面 | 51 | 9 | 27 | 15 | 14 |
| 3.9 services-helpers 服务与帮助器 | 69 | 19 | 30 | 20 | 27 |
| **合计** | **423** | **158** | **166** | **99** | **113** |

> 与 `scripts/list-deviations.sh` 的关系：脚本 2026-11-25 重跑得到 **114 个同名不同内容的文件**（含 28 个 `.xaml`），而本文同名条目 76 条（另有 4 个只属格式或单行的见 §3.10） —— 差额来自三处：①脚本把 `*.Linux.cs` 之外的同名差异一网打尽（含纯格式差异，本文不记）；②本文按「行为/结构不同」合并（如 `AdvancedSetting.xaml` 与 `.cs` 并成一条）；③0.19.0 合并后部分文件只是上游自己改了、移植侧逐字节相同（如 `GameLauncherSettingDialog`、`StartGameButton` 四个文件在本次重算中归零）。要文件清单看脚本，要判定看本文。

## 2 · 结论（需拍板的三件事）

1. **must 160 条** = 移植骨架，去掉任一条就直接失去对应功能，不应回退：X11/Wayland 窗口与托盘（3.4）、Uno/Skia 引擎缺口（3.5/3.7 的图像解码与视频帧）、wine 前缀与注册表（3.3/3.9）、进程与会话观察（3.9）、文件对话框与 portal（3.9）。
2. **avoidable 97 条** = 与平台无关、本可照上游写；清单见附录 B。建议逐条二选一：改回上游语义，或在 §3 对应条目里写明「有意保留」的理由。2026-11-25 已按此原则顺手修掉 5 条可复现缺陷（见 §2.1），并按「优先对齐上游」批量回改了一批（D134，见 §2.3）；其中 2 条经 CI 的 `dotnet publish -r linux-x64` 实测**不是**可避免的（`RectInt32` 4 参构造器、`WindowActivationState` 同名双类型），已改判 must。
3. **可回流上游 12 条**（附录 C）全是「上游自己也有这个毛病」的修正，值得单独提 PR；风险最低的是 `Helpers/PointerCursor.cs`、`Features/Setting/FileManageSetting.xaml.cs` 手工备份、`Features/GameInstall` 的图标码位替换。

### 2.1 本次重算顺带修掉的（已在工作区/已提交）

| 模块 | 问题 | 处理 |
|---|---|---|
| 3.4 | `AppConfig.Configuration.cs` 漏 `GameFileDatabase.SetDatabase` → 打开启动器多配置面板必崩 | 两分支补齐（上游 `AppConfig.Configuration.cs:114` 有） |
| 3.4 | `InitializePassportUser` 被塞进「检测到系统代理」分支 → 无代理用户看不到 Hyp 账号列表 | 恢复上游语义，一次性提示守卫移回 `CheckSystemProxy` |
| 3.7 | 5 处调试环境变量裸读，Release 也生效（违反 D35） | 收进 `DevSwitches.DevEnv` |
| 3.8 | `ScreenshotSetting.Linux.cs` 漏 `InitializeComponent()` → 设置页进入该页一片空白 | 补调用 + 新增 `--selftest-ui` 断言 |
| 3.4 | `/tmp/lw-trace.txt` 调试残留 | 删除 |

### 2.2 高优先修复候选（用户能直接感知，尚未修）

| 模块 | 现象 | 位置 |
|---|---|---|
| 3.9 | 浅色主题下托盘菜单是深灰底（把 `TrySetAcrylic(true)` 当成了「深色」） | `Helpers/SystemBackdropHelper.Linux.cs` |
| 3.9 | 更新窗点「重试」没有任何反馈（缺 `INotifyPropertyChanged`） | `Features/Update/UpdateWindow.Linux.cs` |
| 3.9 | AppImage 版 `lunarward://` 深链静默无效（打包未刷新 desktop 缓存） | `Features/UrlProtocol/UrlProtocolService.Linux.cs` |
| 3.9 | `GameSelector` 三份 1800+ 行死代码仍在编译，其迁移逻辑让老用户当前游戏回落默认 | `Features/GameSelector/*` |
| 3.5 | 图库缩略图漏 `IsThumbnail="True"` → 一屏 30 张全尺寸解码（内存与首屏时间翻倍） | `Features/Screenshot/ScreenshotPage.xaml:78-81` |
| 3.5 | 自己截的图时间戳解析不中 → 排序/分组成谜 | `Features/Screenshot/ScreenCapture.Linux.cs:60` 对 `ScreenshotItem.cs:52-95` |
| 3.5 | 单选一张图点「批量转换」会把整库都转成 .jpg | `ScreenshotPage.LinuxAuto.cs` → `BatchConvertAsync:308-321` |
| 3.5 | 截图右键菜单七项（复制路径/打开所在目录/重命名…）整块未搬，七个 handler 全是空的 | `Features/Screenshot/ScreenshotPage.LinuxAuto.cs:14-22` |
| 3.3 | 开 HDR 写注册表失败只记 Debug 日志，界面照样显示成功 | `Features/GameSetting/GameSettingService.cs:327` → `WineRegistry.cs:237-252` |
| 3.3 | 切游戏时旧 `DisplayInformation` 不释放、不退订 → 累积泄漏 | `Features/GameSetting/GameSettingPage.xaml.cs:343-345` |
| 3.7 | 视频壁纸音量滑块无效（播放器恒 `--no-audio`） | `Features/Background/AppBackground.Linux.cs:479-497` |
| 3.6 | BBS 战绩页退化为说明页（WebView2 桥裁到 26 行） | `Features/GameRecord/BattleChronicleWindow.Linux.cs:64` |
| 3.6 | 登录成功后战绩页不刷新（`GameRecordRoleChangedMessage` 无人订阅） | `Features/GameRecord/GameRecordPage.xaml.cs:56` |

### 2.3 D134：可避免偏离的批量回改（2026-11-25）

按「优先对齐上游」逐模块回改，本节只记结论；逐条的「上游 / 移植 / 理由」仍看 §3 对应条目。

| 模块 | 回改条目 | 处置 |
| --- | --- | --- |
| 3.1 启动器 | 同名遮蔽的 `GameConfigIni`、`SetGenshinEnableHDR` 死方法、公告同步包装、`#if DEBUG` 自动链接钩子、预检动作文案 | 删死代码 / 改走 `Lang` |
| 3.2 安装 | 扫描根 `LUNARWARD_GAME_ROOTS`、任务表不清理、finally 缺 1 秒退避 | 删环境变量 / 补清理与退避 |
| 3.3 游戏设置 | 读注册表回落 `~/.wine`、HDR 写入静默失败、分辨率同步等待、HDR 对话框文案 | 走游戏前缀 / 抛错 / 改异步 / 走 `Lang` |
| 3.4 外壳 | `App.HandleStartupAsync`、`ApplyAcrylicFallback`、缺 `playtime`/`rpc` 子命令 | 删死代码 / 补子命令（托盘激活与拖拽区两点还原后编不过，不改） |
| 3.5 截图 | 右键菜单、缩略图 `IsThumbnail`、截图文件名、批量转换整库、两个死处理器 | 补菜单（`RightTapped` 现造）/ 补属性 / 改格式 / 要求先勾选 / 删 |
| 视图底座 | `PageBase` 在 `Unloaded` 退订 `Loaded`（缓存页面再进入时 `OnLoaded` 不执行） | 不再退订 |

**仍保留的 avoidable 项（下一轮）**：`GameLauncherPage` 内联 `BackgroundSwitcher`（约 270 行，需截图验证）、截图外部目录管理与「格式/质量」设置开关、`ImageViewWindow` 死窗口三件套、`ImageConvertItem` 孤立模型、`LoginPage` 残留（上游 0.19.0 已删）、BBS 桥裁剪、托盘主题/更新窗重试/深链等 3.8/3.9 条目。

## 3 · 逐模块偏离点

### 3.1 启动器与游戏入口（GameLauncher）

#### `Features/GameLauncher/GameBannerAndPost.xaml`

> 对照上游 `src/Starward/Features/GameLauncher/GameBannerAndPost.xaml`（149 行 / 移植 158 行）。

- **第 38 行（根 Grid `Grid_BannerContainer` 新增 `Tapped`，第 48 行 `Image_Banner` 同时去掉 `Tapped`）**｜engine 引擎能力｜must
  - 上游：`<Grid x:Name="Grid_BannerContainer">` 不挂事件，横幅点击由 `DataTemplate` 里的 `<Image x:Name="Image_Banner" Tapped="Image_Banner_Tapped">` 接收。
  - 移植：命中区上移到覆盖整个横幅的根 Grid（`Tapped="Grid_BannerContainer_Tapped"`），`Image_Banner` 只留 `Loaded`。
  - 理由：Uno/Skia 下 `FlipView` 在内部把指针事件标记为已处理，`DataTemplate` 内元素收不到 `Tapped`；点在 `UniformToFill` 图片外围的留白上也不命中。把命中区放到 `FlipView` 之外的 Grid 是唯一稳定的落点，属引擎行为差异，Windows 上无此问题。

- **第 46 行（`Image_Banner` 新增 `DecodePixelWidth="760"` 与 `Loaded="Image_Banner_Loaded"`）**｜eng 工程性｜reasonable
  - 上游：`<Image Source="{x:Bind ...}" Stretch="UniformToFill" />`，不设解码尺寸。
  - 移植：`DecodePixelWidth="760"`，配合 `Loaded` 里补挂事件（见 `.xaml.cs` 第 291 行）。
  - 理由：启动页横幅原图是 1920×1080 级海报，不设 `DecodePixelWidth` 会按原图解码并常驻；横幅实际显示宽度约 380 DIP，760 覆盖 2x 屏。纯内存优化，与平台无关，不需要平台能力。

- **第 74 行（`Pivot_Posts` 新增 `x:Name`）**｜engine 引擎能力｜reasonable
  - 上游：`<Pivot ItemsSource="{x:Bind PostGroups}">` 匿名，靠绑定与页面重建维持状态。
  - 移植：`<Pivot x:Name="Pivot_Posts" ...>`，供 code-behind 手工重置 `ItemsSource`/`SelectedIndex`。
  - 理由：Uno 的 `Pivot` 换 `ItemsSource` 且旧选中项越界时抛 `ArgumentOutOfRangeException`（L-126），必须拿到控件实例做「置空 → 重建 → 复位选中项」序列；匿名 Pivot 无法从 code-behind 触达。

- **第 111-135 行（公告列表：`ScrollViewer Margin="0,0,-12,0"` → `Margin="0"`；`ItemsRepeater` 加 `HorizontalAlignment="Stretch"`；行 Grid 加 `MaxWidth="340"`、`Tapped`、`Background="Transparent"`）**｜engine 引擎能力｜must
  - 上游：`Margin="0,0,-12,0"`（用负边距抵消 `ScrollViewer` 内边距），行 Grid 不定宽。
  - 移植：负边距归零、`ItemsRepeater` 拉满、行 Grid `MaxWidth="340"` 并给透明背景保证命中。
  - 理由：Uno 的 `ItemsRepeater` 在 `ScrollViewer` 里按无限宽度测量，上游「自动宽度 + 负边距」的写法在移植上会让右侧日期列被挤出卡片；340 = 卡片固定 380 DIP − 左右各 12 − 右侧 16（改法注释里写了推导）。`Background="Transparent"` 是因为无背景的 Grid 透明区域不参与命中测试。

- **第 126-130 行（公告标题由 `Hyperlink` 改为普通 `TextBlock` + `MaxLines="1"` / `TextTrimming="CharacterEllipsis"`）**｜api 平台 API｜reasonable
  - 上游：`<TextBlock><Hyperlink NavigateUri="{x:Bind Link}"><Run Text="{x:Bind Title}" /></Hyperlink></TextBlock>`。
  - 移植：`<TextBlock Text="{x:Bind Title}" MaxLines="1" TextTrimming="CharacterEllipsis" TextWrapping="NoWrap" />`，点击交给行 Grid 的 `Tapped` → `Helpers.OpenUrl`。
  - 理由：`Hyperlink.NavigateUri` 内部走 `Launcher.LaunchUriAsync`，上游把它包在 `catch { }` 里，失败无任何反馈；改用 `Helpers/OpenUrl`（`xdg-open` → 失败退 `gio open`，返回 bool）能判定失败并回 toast。注意旧总账写的理由「`Launcher` 在 Linux 上静默失败（L-74）」**已不成立**：L-74 现在的结论是 Linux 上**有**实现，只有缺 handler 的 scheme 才失败——换成 `OpenUrl` 仍合理，属可判定 + 可回显，但不是「平台 API 不存在」。

#### `Features/GameLauncher/GameBannerAndPost.xaml.cs`

> 对照上游同名文件（331 行 / 移植 460 行）。上游 0.19.0 仍保留 `GameNoticeWindow`、`GameNoticeWindowClosedMessage` 的用法，移植侧两者都已删除。

- **第 32、47-63、65-75 行（新增 `_contentGameId` 字段 + `CurrentGameId` 字段属性 + `ReloadForCurrentGameAsync`）**｜linux 专属实现｜reasonable
  - 上游：`[ObservableProperty] public partial GameId CurrentGameId { get; set; }`，内容刷新靠「每次导航重建页面」。
  - 移植：`CurrentGameId` 是手写字段属性，setter 里 `if (IsLoaded) _ = ReloadForCurrentGameAsync();`；`ReloadForCurrentGameAsync` 只做 `_contentGameId = CurrentGameId` + `await UpdateGameContentAsync()`。
  - 理由：页面缓存（D94，`GameLauncherPage.xaml` 第 3 行 `NavigationCacheMode="Required"`）之后页面不再重建，切游戏必须由属性变更驱动刷新，否则横幅/公告停在上一款游戏。属于缓存决策的连带改动。

- **第 97-108 行（`Loaded` 改为条件刷新，并删掉 `GameNoticeWindowClosedMessage` 注册）**｜linux 专属实现｜reasonable
  - 上游：`Loaded` 里无条件 `_ = UpdateGameContentAsync();`，并注册 `GameNoticeWindowClosedMessage`（公告窗关闭后重拉红点状态）。
  - 移植：`if (_contentGameId != CurrentGameId || Banners is null || PostGroups is null)` 才刷新；消息注册整段删除，红点改在打开公告时置 `IsGameNoticesAlert = false`（第 434 行）。
  - 理由：缓存页每次切回都会 `Loaded`，无条件重拉会重复请求公告接口；公告窗控件在移植侧已删除（见文末「上游存在但移植侧删除的文件」），对应消息类型也随之消失，注册处只能删。

- **第 110-116 行（`Unloaded` 不再把 `Banners` / `PostGroups` 置 `null`）**｜engine 引擎能力｜must
  - 上游：`Unloaded` 里 `Banners = null; PostGroups = null;`，下次 `Loaded` 重建。
  - 移植：保留数据，只在换游戏时刷新；注释写明「不要置 null，否则 Uno `Pivot` 重新赋值列表时不重建条目，公告区只剩空卡片（真机复现）」。
  - 理由：Uno 7 的 `Pivot` 在重新赋 `ItemsSource` 时不重建条目容器，置 null 清空后缓存页再回来就是一片空白；Windows 上无此现象。

- **第 207-233 行（`UpdateGameContentAsync`：竞态守卫 + `Pivot` 重置序列）**｜engine 引擎能力｜must
  - 上游：直接 `Pivot_Posts.SelectedIndex = 0;` 之类的一句赋值。
  - 移植：第 211 行 `if (_contentGameId != CurrentGameId) return;`（切游戏期间旧请求作废，B62）；随后 `Pivot_Posts.ItemsSource = null;` → `SelectedIndex = -1` → 赋 `PostGroups` → `await Task.Delay(60)` → `SelectedIndex = 0`。
  - 理由：Uno 6.7.45 的 `Pivot.SynchronizeSelectedItem`（`Pivot.cs:295-301`）在换 `ItemsSource` 且旧 `SelectedIndex` 越界时抛 `ArgumentOutOfRangeException`（L-126），先把选中项压回 -1 再赋值即可绕开；60 ms 是等模板同步完成的实测值。

- **第 291-300 行（`Image_Banner_Loaded` 里用 `AddHandler(..., handledEventsToo: true)` 补挂 `Tapped`）**｜engine 引擎能力｜must
  - 上游：XAML 里 `Tapped="Image_Banner_Tapped"` 直接挂。
  - 移植：XAML 不挂，改为 `element.AddHandler(UIElement.TappedEvent, new TappedEventHandler(Image_Banner_Tapped), true);`。
  - 理由：`FlipView` 内部已把指针事件标记为 handled，普通 XAML 订阅收不到；只有 `handledEventsToo: true` 才能收到，且必须在元素 `Loaded` 之后挂到实际实例上。

- **第 302-333 行（三个 `Tapped` 处理函数增加 `FlipView_Banner?.SelectedItem` 兜底）**｜engine 引擎能力｜reasonable
  - 上游：`(sender as FrameworkElement).DataContext as GameBanner` 直接取。
  - 移植：保留 `DataContext` 路径，取不到时退到 `FlipView_Banner.SelectedItem`。
  - 理由：横幅 `DataTemplate` 根是 `Border`/`Grid` 时，Uno 不一定回填 `DataContext`，命中区又没有承载绑定数据；从 `FlipView` 当前项兜底最稳。（具体哪些模板根不回填未逐一核实，只知道 `DataContext` 路径在本移植上会拿到 null。）

- **第 377-405 行（`#if DEBUG` + `LUNARWARD_DEV_BANNER_AUTOLINK` 钩子）**｜eng 工程性｜avoidable
  - 上游：无。
  - 移植：环境变量存在时启动页自动打开第一条横幅链接，用于无 GUI 自动化时验证「点击 → 打开」链路。
  - 理由：与平台无关的自测插桩，仅 `#if DEBUG` 编译；按仓库硬规则 7（不留临时插桩）应在用完后删除或降级，属可避免。

- **第 426-450 行（`OpenGameNoticeWindowAsync` 改为「取 URL → `OpenUrl.Open` → 置已读」，整窗实现删除）**｜cut 裁剪｜must
  - 上游：`new GameNoticeWindow { CurrentGameId = ... }.Activate();` + Vanara `User32.SetForegroundWindow(hwnd)` 把公告窗提到前台；关闭后由 `GameNoticeWindowClosedMessage` 触发重拉。
  - 移植：`service.GetGameNoticeUrlAsync(CurrentGameId.GameBiz)` → `IsGameNoticesAlert = false`（B61：打开即视为已读）→ `Helpers.OpenUrl.Open(url)`，失败弹 warning toast。
  - 理由：上游公告窗是 `WindowBase` + `WebView2` 宿主（`src/Starward/Features/GameLauncher/GameNoticeWindow.xaml`）。移植侧曾尝试同一路径，但 Uno/Skia 的 `WebView2` 在真机上只显示黑底、窗口里给不出网页内容（`GameBannerAndPost.xaml.cs:430-432` 的注释留档），`User32.SetForegroundWindow` 又是 Win32 API、Linux 无对应物。功能降级为系统浏览器打开，用户可见差异 = 不再有应用内公告窗（既定决策，D40 延续）。

#### `Features/GameLauncher/GameLauncherPage.xaml`

> 对照上游同名文件（223 行 / 移植 326 行）。上游 0.19.0 把背景切换条整块抽成了 `sfb:BackgroundSwitcher` 控件，本移植页把旧内联块保留在页面里。

- **第 3 行（`sf:PageBase` 上新增 `NavigationCacheMode="Required"`）**｜linux 专属实现｜reasonable
  - 上游：`<sf:PageBase x:Class="...GameLauncherPage" ...>` 不设缓存模式（默认 `Disabled`，每次导航重建）。
  - 移植：`NavigationCacheMode="Required"`。
  - 理由：D94 页面缓存决策——启动页重建一次要重拉公告/资讯/服务器列表/背景，Linux 上明显卡顿；缓存后由 `CurrentGameChangedMessage` + 手工重置维持正确性（见 `.xaml.cs` 第 68-160 行）。这是与上游最大的结构性差异，后续多条偏离都由它派生。

- **第 57-141 行（上游一行 `sfb:BackgroundSwitcher` → 页面内联一份约 85 行的切换条：渐变底 `#00000000→#88000000`、`Height="60"`、`Opacity="0"` + `OpacityTransition`、`PointerEntered/Exited`、`PointerWheelChanged`、图库/复制/另存为/播放暂停按钮）**｜eng 工程性｜avoidable
  - 上游：三行 XAML —— `<sfb:BackgroundSwitcher HorizontalAlignment="Stretch" VerticalAlignment="Bottom" CurrentGameId="{x:Bind CurrentGameId}" />`（`src/Starward/Features/GameLauncher/GameLauncherPage.xaml:56-58`，控件本体见 `src/Starward/Features/Background/BackgroundSwitcher.xaml`）。
  - 移植：把旧版内联块原样留在页里（含同一套交互），且 `xmlns:sfb="using:Starward.Features.Background"`（第 13 行）仍在但已无任何使用；与此同时 `src/Lunarward/Features/Background/BackgroundSwitcher.xaml(.cs)` 与上游**逐字节相同**却全仓无引用（死代码）。
  - 理由：真正的引擎缺口只在控件内部的 `PipsPager` 圆点（下一条），改控件一处即可；整块内联属于上游重构后没跟上的重复实现，与平台无关、本可照上游用控件。现状副作用：同一交互有两份实现、修一处漏一处。

- **第 74 行（`StackPanel_BackgroundDots` 取代控件里的 `PipsPager`）**｜engine 引擎能力｜must
  - 上游：`<PipsPager x:Name="PipsPager_SwitchBackgroundImage" Style="{ThemeResource CustomPipsPagerStyle}" />`，`CustomPipsPagerStyle` 的模板里用 `<ItemsRepeater ItemsSource="{Binding RelativeSource={RelativeSource TemplatedParent}, Path=TemplateSettings.PipsPagerItems}">` 手画圆点。
  - 移植：`<StackPanel x:Name="StackPanel_BackgroundDots" Spacing="2" />`，圆点由 code-behind 生成 `Button`（`.xaml.cs` 第 1389-1420 行）。
  - 理由：Uno 对 `PipsPager.TemplateSettings` + `ItemsRepeater` 的实现不全——既不显示圆点也不产生 `SelectedPageIndex` 更新（L-69，实测点圆点无任何反应），双向绑定没有可回写的东西。属引擎不支持，只能换实现。

- **第 153-169 行（新增 `StackPanel_InstallTask`：`Margin="60,0,16,92"` / `MaxWidth="460"` / `TextBlock_InstallTask`(FontSize 12) / `ProgressBar_InstallTask`(Height 3, `Maximum=1`,`Minimum=0`)）**｜eng 工程性｜reasonable
  - 上游：无此元素，安装/预下载进度只画在 `StartGameButton` 内（经 RPC 任务消息驱动）。
  - 移植：页面底部独立一行「安装任务」状态条 + 细进度条，逻辑在同名 partial `Features/GameLauncher/GameLauncherPage.InstallTask.cs`（移植新增文件，上游无对应物；`StartInstallTaskWatcher` 每秒读一次进程内 `GameInstallService.GetGameInstallTask`）。
  - 理由：D128 之后安装走进程内 `Starward.GameInstall.GameInstallRunner` facade，没有 RPC 消息推送，且页面缓存后 `StartGameButton` 未必处于可显示进度的状态，需要一处常驻读出的进度；纯新增功能，与平台无关。

#### `Features/GameLauncher/GameLauncherPage.xaml.cs`

> 对照上游同名文件（990 行 / 移植 1645 行）。差异集中在三块：页面缓存派生的重载逻辑、预检/修复动作、被内联的背景切换条（约 280 行，上游在 `BackgroundSwitcher.xaml.cs` 里）。

- **第 65、68-93 行（`_gameChangedHandled` 双初始化守卫 + `Loaded` 里重接 `_dispatchTimer.Tick` 与 4 个消息注册）**｜linux 专属实现｜reasonable
  - 上游：`OnLoaded` 只做 `LoadGameContent()` + 注册消息，`DispatcherTimer` 在构造函数里挂一次即长期有效。
  - 移植：`OnLoaded` 增 `_gameChangedHandled` 判断（缓存页切游戏时 `OnGameChanged` 已重载过内容，紧接着的 `Loaded` 不能再跑一遍，B30）；调 `StartInstallTaskWatcher()`；显式 `_dispatchTimer.Tick -= UpdateGameInstallTaskProgress;` 再 `+=`（D94：缓存页第一次 `Unloaded` 会把 Tick 摘掉，重新挂载必须接回，否则安装进度不再更新）；消息注册处补 `CurrentGameChangedMessage`（第 88 行）。
  - 理由：全部由「页面不再重建」派生；上游每次导航新建页面，`Loaded` 与新实例天然对齐，不存在重复初始化与 Tick 丢失。

- **第 96-142 行（`OnGameChanged` → `LoadGameContent`：手工清 13 项状态 + 重新初始化各子模块）**｜linux 专属实现｜reasonable
  - 上游：无 `OnGameChanged`/`LoadGameContent`，状态靠新页面实例天然归零。
  - 移植：`LoadGameContent` 逐项清 `GameInstallPath`、`IsInstallPathRemovableTipEnabled`、`IsPredownloadButtonEnabled`、`IsPredownloadFinished`、`IsDX12OptionVisible`、`GameServers`、`SelectedGameServer`、`_gameInstallTask`、`DXConfig`、`Button_CloudGame.Visibility`、`BackgroundImages`、`currentBackgroundImageIndex`/`CurrentBackgroundImageIndex`，再 `Button_PlayTime.RefreshPlayTime(CurrentGameBiz)`、`GameBannerAndPost.CurrentGameId = CurrentGameId`、以及 `InitializeGameFeature` / `CheckGameVersion` / `UpdateGameInstallTask` / `CheckCloudGame` / `InitializeGameServerAsync` / `InitializeBackgameImageSwitcherAsync`。
  - 理由：等价于「上游重建页面」的语义；漏清任何一项都会把上一款游戏的数据带过来（例如 DX12 选项、云游戏按钮可见性、背景图库）。这是缓存决策下最脆的一处，新增字段时必须同步补进这张清单。

- **第 145-160 行（`OnCurrentGameChanged(CurrentGameChangedMessage)` 兜底刷新）**｜linux 专属实现｜reasonable
  - 上游：无此消息处理路径（导航参数直接构造新页面）。
  - 移植：收到 `CurrentGameChangedMessage` 时若与当前 `CurrentGameId` 不一致则走 `OnGameChanged`，与 `Loaded` 里的 `_gameChangedHandled` 配合避免重复执行。
  - 理由：缓存页可能在「未 `Loaded`」时收到消息（切游戏发生在别的页面），需要一条不依赖可视树的刷新入口。

- **第 720-942 行（新增启动预检对话框 + 分派修复动作：`ShowPreflightDialogAsync` / `GetPreflightActionText` / `RunPreflightActionAsync`）**｜eng 工程性｜reasonable
  - 上游：启动失败只弹 `InAppToast.MainWindow?.Error(ex)`，无预检概念。
  - 移植：`LaunchPreflightException` 触发 `ContentDialog`（逐项 ✓ / ! / ✕ + `ScrollViewer MaxHeight="440"`），动作包括 `OpenRuntimeSettings`（带 `ProfileBinding.Resolve()?.Id`，R26）、`RebuildPrefix`（校验前缀必须在 `PrefixManager.PrefixRoot` 下、确认框、`Task.Run(() => PrefixManager.EnsurePrefix(...))`）、`InstallGraphics`（`ComponentStore.GetGraphics` + `GraphicsInstaller.EnsureInstalled`）、`ShowPrefixProcesses`（`Helpers.ProcessQuery.FindUsersAsync(prefix)` 弹等宽字体对话框）。
  - 理由：Linux 上启动失败最常见的原因是缺 wine / 前缀坏 / 缺 DXVK / 前缀被别的实例占用，这些在 Windows 上由系统与官方启动器兜住；没有预检就只能给一句原始异常。属运行环境模块（`Features/Runtime/`）的入口，是移植新增能力。

- **第 778-790 行（`GetPreflightActionText` 返回硬编码中文：打开游戏设置 / 打开运行环境 / 打开前缀目录 / 重建前缀 / 安装图形组件 / 查看占用进程 / 打开游戏目录）**｜cosmetic 外观文案｜avoidable
  - 上游：无对应文案。
  - 移植：`switch` 直接返回中文字面量，未走 `Lang.*`。
  - 理由：同页面其它新增文案都用了 `Lang.Lunarward_Preflight_Title` / `Lang.Lunarward_Preflight_OK` / `Lang.Lunarward_Launch_Failed`，只有这 7 个按钮标签漏了，非中文环境下会是中文 + 英文混排。与平台无关，本可照同文件的做法写资源。

- **第 1301 行（自定义背景改为传 `file.Path` 而不是 `StorageFile`）**｜api 平台 API｜must
  - 上游：`await BackgroundService.ChangeCustomBackgroundFileAsync(file)`，`file` 是 `StorageFile`。
  - 移植：`await BackgroundService.ChangeCustomBackgroundFileAsync(file.Path)`，签名收 `string`。
  - 理由：Uno/Skia 没有 `Windows.Storage.StorageFile` 的可用实现（文件选择器返回的是路径，拿不到 `StorageFile`），`AppBackground.Linux.cs` 侧只认路径。行号取值处（`file.Path`）即偏离点。

- **第 1353-1355 行（`PlayIcon = "\uF5B0"` / `PauseIcon = "\uE769"` 换成 Uno Symbols 码位）**｜cosmetic 外观文案｜must
  - 上游：用 `Segoe Fluent Icons` / `Segoe MDL2 Assets` 的码位（`\uF5B0`、`\uE769` 属该字体私有区）。
  - 移植：常量改成 Uno Symbols 字体里有形的码位。
  - 理由：Uno 拦截 `Segoe Fluent Icons` / `Segoe MDL2 Assets` 族名，随包字体不加载（L-15），沿用上游码位会显示成豆腐块；这是字体族名被引擎改写导致的必须换码位。

- **第 1350-1618 行（`#region Switch Background Image`，被内联的切换条代码：`BackgroundImages` / `CurrentBackgroundImageIndex`（先 `Math.Clamp`）/ `RebuildBackgroundDots` / `UpdateBackgroundDots` / `UpdateVideoButtonState` / `InitializeBackgameImageSwitcherAsync` / `ChangeBackgroundImageIndex` / `PointerEntered`·`Exited`·`WheelChanged` / `OpenBackgroundViewWindow` / 复制·另存为·暂停，以及第 1484-1505 行 `#if DEBUG` 的 `LUNARWARD_DEV_BG_CYCLE`）**｜eng 工程性｜avoidable
  - 上游：这些逻辑在 `src/Starward/Features/Background/BackgroundSwitcher.xaml.cs` 里（控件内部），页面侧没有；无 `LUNARWARD_DEV_BG_CYCLE`。
  - 移植：整份搬进页面 code-behind（约 160 行），列表与选中项改从 `AppBackground.Current` 读（`AppBackground.Linux.cs` 重写过），并保留 6 秒自动切图的 DEV 钩子；`Copy`/`StartOrStopVideoBackground` 里还用 `current?.Type is 1` 这种魔法数判断视频。
  - 理由：与 XAML 那条同源——重复实现了上游控件已提供的功能，与平台无关、本可照上游用控件；其中「圆点由 Button 生成」的部分才是 must 级引擎规避（已单列为 `GameLauncherPage.xaml` 第 74 行）。DEV 钩子按硬规则 7 属应清理项。

#### `Features/GameLauncher/GameLauncherService.cs`

> 对照上游同名文件（669 行 / 移植 804 行）。

- **第 290-292 行（`GameConfigIni` 使用全限定名 `Starward.GameInstall.Model.GameConfigIni`）**｜eng 工程性｜avoidable
  - 上游：`using Starward.GameInstall.Model;` 后用短名 `GameConfigIni`。
  - 移植：`Starward.GameInstall.Model.GameConfigIni? configIni = await GameConfigService.GetGameConfigIniAsync(...)`，并留 `LUNARWARD-DEVIATION` 注释说明原因。
  - 理由：移植侧 `Features/GameLauncher/GameConfigIni.cs` 还留着 Windows 时代手写的同名类（`namespace Starward.Features.GameLauncher`），与共享工程里的 `Starward.GameInstall.Model.GameConfigIni` 同名会遮蔽，只能全限定绕开（`Program.Linux.cs:784/879` 同样在绕）。上游 0.19.0 已把该类整体移入 `Starward.GameInstall.Model`，删掉这份残留即可恢复正常写法——与平台无关，属可避免。

- **第 409-431 行（`GetGameProcessAsync` 增加 Linux 分支：按 `/proc` 命令行找进程）**｜api 平台 API｜must
  - 上游：`Process.GetProcessesByName(name).Where(x => x.SessionId == currentSessionId && !IsProcessPending(x))`。
  - 移植：`if (!OperatingSystem.IsWindows())` → `GetGameExeNameAsync(...)` → `PlayTimeSession.FindProcessByCommandLine(exeName)` → `Process.GetProcessById`。
  - 理由：wine 下进程名是 `wine`/`wine64`，按 exe 名永远找不到（B34）；`Process.SessionId` 在 Linux 上无意义，会话隔离只能靠命令行里的 exe 路径判定。`/proc/<pid>/cmdline` 是 Linux 专有来源。

- **第 515-530 行（启动前重放 HDR 开关：`GameSettingService.SetGenshinEnableHDRAsync(gameId.GameBiz, AppConfig.EnableGenshinHDR)`）**｜api 平台 API｜must
  - 上游：无这段（HDR 由 `GameSettingService` 在「改设置」时写注册表，启动流程不重放）。
  - 移植：`if (gameId.GameBiz.Game is GameBiz.hk4e)` 时启动前按 `AppConfig.EnableGenshinHDR` 重放一次，失败仅 `LogWarning`；注释写明两层约束：「游戏必须是 wine 的第一个进程」铁律（代码注释引作 L-62；但 `docs/linux-port/13-lessons.md:576` 的 L-62 是“chunk 复用”，编号疑误，约束本身见 `docs/user/faq.md:59`）+ D95/B41「按 `AppConfig` 重放，保证与界面一致」。
  - 理由：Windows 侧 HDR 走注册表写 DWM 覆盖项，Linux 没有注册表 API 与 DWM，只能通过 `WineRegistry` 异步写 wine 前缀里的同名键；且前缀可能被重建、或用户在拿到前缀之前就改过设置，注册表里会是旧值，所以每次启动重放。**注意：这条与旧总账相反**——旧文写「HDR 整段删除，理由：Linux 无意义」，现已被 D95/B41 推翻；另外第 517-521 行还留着一条「HDR 改由设置页保存时写入目标前缀」的旧注释，与下面的代码自相矛盾，应清理。

- **第 533-546 行（`StartGameWithCMD` 在 Linux 上强制忽略）**｜api 平台 API｜must
  - 上游：`AppConfig.StartGameWithCMD` 为真时用 `cmd.exe /c` 包一层启动（Windows 提权/兼容技巧）。
  - 移植：`if (!thirdPartyTool && AppConfig.StartGameWithCMD && OperatingSystem.IsWindows())`，注释「保留判断以免上游字段消失时编译失败」。
  - 理由：`cmd.exe` / `UseShellExecute` 的提权行为在 Linux 不存在，套 shell 还会破坏「游戏是 wine 的第一个进程」这条铁律，影响命令行识别（`FindProcessByCommandLine`）与游玩时长归属。

- **第 548-600 行（启动前新增：`ProfileBinding.EnsureFor` → `LaunchPreflight.Check` → 失败记 `LaunchHistoryStore` 并 `throw LaunchPreflightException`）**｜eng 工程性｜reasonable
  - 上游：直接 `CreateGameProcessStartInfo` 后 `Process.Start`，失败抛原始异常。
  - 移植：先确保绑定到运行环境（无则创建并 toast），再 `Task.Run(() => LaunchPreflight.Check(...))`；预检失败时 `LaunchHistoryStore.Record(Outcome = PreflightFailed)` 后抛 `LaunchPreflightException(preflight)` 交给页面弹对话框。
  - 理由：Linux 上「缺 wine / 前缀坏了 / 缺图形组件」占失败原因的绝大多数，预检把这些变成可读项与可点修复动作；`LaunchHistoryStore` 让失败也有记录可查（上游只有成功路径）。

- **第 601-612 行（`Process.Start` 失败时按 R05 释放前缀锁，再抛出）**｜eng 工程性｜reasonable
  - 上游：无前缀锁概念。
  - 移植：`catch { ReleasePrefixLock(launchPrefix); throw; }`，并把 `LaunchReport.Write(...)` 放在启动后。
  - 理由：前缀锁（`GameLauncherService.Linux.cs` 第 115-152 行）由 `CreateGameProcessStartInfo` 取得，若 `Process.Start` 抛异常（wine 路径错、权限不足）不释放会把该前缀永久锁住，下次启动报「前缀正被另一个实例使用」——这是移植侧自己的缺陷修补（R05），与平台无关但由锁机制派生。

- **第 613-638 行（启动成功后挂 `AttachSessionCleanup` + `LastKnownGoodStore.Remember` + `OutputCapture.Attach`）**｜linux 专属实现｜reasonable
  - 上游：`Process.WaitForExit` 后写游玩时长。
  - 移植：`AttachSessionCleanup(process, gameId.GameBiz, launchPrefix)`（后台等会话结束，`finally` 里释放锁 + 跑退出伴随程序）；`LastKnownGoodStore.Remember`（R111 记**实际使用**的前缀而不是绑定值）；`OutputCapture.Attach`（D27，捕获 wine stderr 供诊断）。
  - 理由：wine loader 常先于游戏本体退出，不能靠 `process.Exited` 判结束（`GameLauncherService.Linux.cs` 第 208-236 行注释），必须走 `PlayTimeSession.WaitForSessionEndAsync` 的补位逻辑；前缀/输出捕获都是运行环境模块的配套。

- **第 738-745 行（遗留的无调用点 `private void SetGenshinEnableHDR(GameId gameId)`）**｜eng 工程性｜avoidable
  - 上游：无。
  - 移植：方法体仍在（同步版写注册表），全文件无调用点；第 526 行的注释已经说明改用 `SetGenshinEnableHDRAsync` 重放。
  - 理由：0.19.0 同步改造后忘记删除的死代码，留着会误导后来者以为有两条 HDR 路径，也与注释自相矛盾；纯清理项。

#### `Features/GameLauncher/GameLauncherService.Linux.cs`

> 上游**无同名文件**：这是移植侧把 `GameLauncherService` 拆成 partial 后新增的平台实现（804 行），上游对应逻辑都在 `GameLauncherService.cs` 里（`Process.Start` + `ProcessStartInfo`）。下列条目的「上游」写作「上游无对应实现」或指上游 `GameLauncherService.cs` 中的最近似代码。

- **第 46-81 行（`ResolveWine`：`LUNARWARD_WINE` → Steam Proton 的 `files/bin/wine` → `PATH`）**｜linux 专属实现｜must
  - 上游：`Process.Start(new ProcessStartInfo { FileName = exePath, ... })`，可执行文件本身就是游戏 exe。
  - 移植：按 `LUNARWARD_WINE` 环境变量 → `Proton Experimental`/`9.0`/`Hotfix` 的 `files/bin/wine`（不过 `proton` 脚本）→ `PATH` 里的 `wine` 顺序解析，缺 wine 抛 `FileNotFoundException` 并提示设置 `LUNARWARD_WINE`。
  - 理由：Linux 上 exe 不能直接执行，必须显式挑一个 wine；不套 Proton 脚本是因为脚本会自行决定进程树与前缀，破坏「游戏是 wine 的第一个进程」这条铁律。平台 API 层面：无 `CreateProcess`，只能 `Process.Start("wine", ...)`。

- **第 34-44、83-101 行（`PrefixRoot` / `ResolvePrefix`：`LUNARWARD_WINEPREFIX` → `~/.wine` → `~/.local/share/lunarward/prefix/<game>`）**｜linux 专属实现｜must
  - 上游：无前缀概念，`Process.Start(exe)` 由游戏自己处理。
  - 移植：`PrefixRoot` 取 `XDG_DATA_HOME`（缺省 `~/.local/share`）+ `lunarward/prefix`；`ResolvePrefix` 按上述优先级返回，路径展开走 `PrefixManager.ExpandPath`。
  - 理由：wine 的 `WINEPREFIX` 是 Linux 专有环境约定；XDG 目录规范也没有 Windows 对应物。默认目录选在数据目录下而不是 `~/.wine`，避免污染用户已有前缀。

- **第 104-188 行（`_prefixLocks` 静态字典 + `NormalizePrefixKey` / `TryLockPrefix` / `ReleasePrefixLock` / `AcquirePrefixLock` 的 `<prefix>/.lunarward/lock` 独占文件锁）**｜linux 专属实现｜must
  - 上游：无，两个实例同时启动同一游戏时由游戏自己报错。
  - 移植：`FileStream(lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)`，`IOException` 视为占用；失败抛「前缀正被另一个实例使用：{prefix}…」（R05/R46），并顺带设 `PlayTimeSession.ActivePrefix`。
  - 理由：同一个 wine 前缀被两个进程并发使用会互相破坏注册表与 `dosdevices`；Windows 上有多用户/会话隔离，Linux 前缀是普通目录，只能自己加跨进程锁。`NormalizePrefixKey` 先 `GetFullPath` 再 `TrimEnd` 分隔符，避免 `/a/b` 与 `/a/b/` 两把锁。

- **第 190-236 行（`RunExitCompanions` + `AttachSessionCleanup`：等会话结束再释放锁、跑 `on_game_exit` 伴随程序）**｜linux 专属实现｜must
  - 上游：`process.WaitForExit()` 后写时长，无伴随程序概念。
  - 移植：`Task.Run` 里 `PlayTimeSession.WaitForSessionEndAsync(process.Id, prefix, gameBiz)`，`finally` 中 `ReleasePrefixLock` + `CompanionRunner.StartCompanionsAsync(biz, "on_game_exit")`；注释「不能用 `process.Exited`：wine loader 常先退出」。
  - 理由：wine 的进程树里 loader 先退、游戏本体后死，直接等 `process.Exited` 会把游玩时长截断并把前缀锁提前放掉；伴随启动（`Features/Companion/`）是移植新增的 Linux 专属模块。

- **第 238-436 行（`CreateGameProcessStartInfo`：profile 解析 → 锁前缀 → `EnsurePrefix` → 图形组件 → `RuntimeEnvironment.Assemble` + `LaunchRecipeBuilder.Build` → `WINEPREFIX`/`WINEDEBUG=-all`/UTF-8 管道）**｜linux 专属实现｜must
  - 上游：`new ProcessStartInfo(exePath) { WorkingDirectory = installPath, UseShellExecute = false, ... }`。
  - 移植：`ProfileBinding.Resolve(gameBiz)`（B31）取运行环境；`ProcessStartInfo` 指向 `RunnerPath ?? ResolveWine()`；`RedirectStandardOutput/Error = true` + UTF-8 编码（R107）；**先** `AcquirePrefixLock` **再** `PrefixManager.EnsurePrefix`（R46）；DXVK/VKD3D 走 `ComponentStore.GetGraphics()` + `GraphicsInstaller.EnsureInstalled`（失败仅告警）；环境里写 `WINEPREFIX`、`WINEDEBUG=-all`，再 `RuntimeEnvironment.Assemble(profile, key, prefix)` 与 `LaunchRecipeBuilder.Build(..., wrapper => Enhancements.FindInPath(wrapper) is not null)`（R19/R02/R18/R01：整份 recipe 环境下发、缺 wrapper 提示、overrides 记日志）。
  - 理由：`WINEPREFIX`/`WINEDEBUG`/Proton 前置变量都只存在于 Linux wine；`FileShare`/`UseShellExecute=false` 的组合在 Linux 上不做 shell 解析。这段是 Linux 启动路径的核心，任何一条缺失都直接表现为「点开始游戏没反应」。

- **第 437-479 行（`ApplyWineRuntimeEnvironment`：只补 `WINEDLLPATH`、`PrependLibraryPath`、`STEAM_COMPAT_*`/`PROTONPATH`）**｜eng 工程性｜reasonable
  - 上游：无。
  - 移植：独立方法，与 `RuntimeEnvironment.Assemble` 共用 `PrependLibraryPath`（R108），补齐 Proton 需要的 `STEAM_COMPAT_CLIENT_INSTALL_PATH` / `STEAM_COMPAT_INSTALL_PATH` / `PROTONPATH` 与 `WINEDLLPATH`。
  - 理由：主启动路径已改由 `RuntimeEnvironment.Assemble` 一次性下发整份环境，本方法现在**只在 CLI 路径被调用**（`Program.Linux.cs:332`、`:358`），属为 `--launch-plan`/CLI 保留的兼容层；若确认 CLI 也走 `Assemble` 即可删掉，避免两处拼环境导致漂移（R108 的教训）。调用点已核对，非「未核实」。

- **第 482-550 行（`DescribeLaunchPlan`：CLI `--launch-plan` 输出 exe / 配方 / 运行器 / wine / 前缀 / 命令 / env / 覆盖）**｜eng 工程性｜reasonable
  - 上游：无（Windows 版没有对应诊断输出）。
  - 移植：走同一套 `ProfileBinding` → `EnvironmentResolver` → `RuntimeEnvironment` → `LaunchRecipeBuilder`，把最终要执行的命令与环境逐项打印（`Program.Linux.cs:143` 触发）。
  - 理由：Linux 上启动涉及 wine/前缀/图形组件/命令行增强四层拼装，肉眼无法判断最终 argv；`--launch-plan` 是排查「参数被谁吃了」的唯一入口，属移植新增的诊断能力而非平台限制。

- **第 552 行起（`SplitArguments`：支持双引号与单引号的参数切分）**｜eng 工程性｜reasonable
  - 上游：增强参数直接拼进 `Arguments` 字符串。
  - 移植：自写切分器把配方参数还原成 argv 列表（`ProcessStartInfo.ArgumentList`），同时被 `Features/Companion/CompanionRunner.cs:109` 复用。
  - 理由：Linux 上 `wine` 的 argv 由我们自己拼，必须避免 shell 二次解析与引号丢失（游戏路径常含空格）；`ArgumentList` 而非 `Arguments` 字符串是防注入/防拆错的关键。与引擎无关，但属平台侧必要工程。

#### `Features/GameLauncher/GameNoticeService.cs`

> 对照上游同名文件（103 行 / 移植 111 行）。上游用 `Microsoft.Win32.Registry` 同步读注册表，移植改用 `Helpers/WineRegistry` 异步读 wine 前缀。

- **第 33-34 行（新增同步包装 `GetGameNoticeUrl(GameBiz)`）**｜eng 工程性｜avoidable
  - 上游：只有同步的 `GetGameNoticeUrl`。
  - 移植：保留同步名 `GetGameNoticeUrl` 作为 `GetGameNoticeUrlAsync(biz).GetAwaiter().GetResult()` 的包装，另加异步真身。
  - 理由：全仓唯一调用点是 `GameBannerAndPost.xaml.cs:434` 的异步版本，这个同步包装**没有任何调用点**；一旦被 UI 线程调用就会走「等外部进程」的死路（L-127）。上游 API 形状已由异步版承接，包装属可删的兼容残留。

- **第 39-43 行（`GetGameNoticeUrlAsync(biz, ct)` 成为 UI 唯一入口）**｜api 平台 API｜must
  - 上游：`public string GetGameNoticeUrl(GameBiz biz)` 内同步读注册表取 uid 再拼 URL。
  - 移植：`public async Task<string> GetGameNoticeUrlAsync(GameBiz biz, CancellationToken ct = default)`，注释「UI 路径必须用这个：uid 可能要读 wine 注册表（一次 fork 0.1–4 s），不能阻塞 UI 线程（L-127）」。
  - 理由：Linux 上读注册表 = 起一个 `wine reg` 进程（0.1–4 s），同步读会把整个窗口冻住；这是平台实现方式导致的必须异步化，不是风格选择。

- **第 48-56 行（`IsNoticeAlertAsync` 依赖异步 uid）**｜api 平台 API｜must
  - 上游：`public bool IsNoticeAlert(GameBiz biz)` 同步。
  - 移植：`public async Task<bool> IsNoticeAlertAsync(GameBiz biz, CancellationToken ct = default)`，内部 `await GetLastUidAsync(biz, ct)` 后与上次记录比较。
  - 理由：同一个 uid 来源（wine 注册表）在 Linux 上只能异步取，同步签名无法实现；调用方（`GameBannerAndPost.xaml.cs`）相应地改成 `await`。

- **第 61-69 行（`GetLastUidAsync`：先查 `GameRecordRoleService`，再退注册表）**｜api 平台 API｜must
  - 上游：`GetLastUid` 直接 `Registry.GetValue(key, name, null)`。
  - 移植：先 `GameRecordRoleService.GetLastSelectGameRecordRoleOrTheFirstOne`（本地数据库里已有 uid 就不 fork wine），取不到才 `await GetUidFromRegistryAsync(...)`。
  - 理由：每条公告红点都要判 uid，若每次都 fork 一次 `wine reg`，启动页会连续卡顿数秒；先用手上已有的记录是 Linux 下的必要短路。`Microsoft.Win32.Registry` 在 Linux 上不存在直接决定了后半段必须换实现。

- **第 74-104 行（`GetUidFromRegistryAsync` 三次 `WineRegistry.GetValueAsync` + nap 直接返回 0）**｜api 平台 API｜must
  - 上游：`using Microsoft.Win32;` + `Registry.GetValue(key, name, null)`（hk4e 分支再把 `byte[]` 转 UTF8 再 `long.Parse`）。
  - 移植：三处 `await WineRegistry.GetValueAsync(key, "<value>", null, ct)`——`bh3` 读 `GENERAL_DATA_V2_LastLoginUserId_h47158221`、`hk4e` 读 `__LastUid___h2153286551`、`hkrpg` 读 `App_LastUserID_h2841727341`；`nap` 没有 uid 概念直接 `return 0`；注释「每个游戏读同一个键，`GetKeyValuesAsync` 一次 fork 取回全部值」。
  - 理由：Linux 没有注册表 API，uid 只能从 wine 前缀的 `system.reg`/`user.reg` 里读，而 `WineRegistry` 只提供异步重载（内部起进程）；键名与 Windows 版本完全一致，保证同一前缀在双系统下读到同一个 uid。

---

**上游存在但移植侧删除的文件（本组）**

- `src/Starward/Features/GameLauncher/GameNoticeWindow.xaml`、`GameNoticeWindow.xaml.cs`、`GameNoticeWindowClosedMessage.cs`：上游 0.19.0 仍存在（公告窗是 `WindowBase` + `WebView2` 宿主 + Vanara `User32.SetForegroundWindow`）。移植侧三个文件全部删除，公告改为 `Helpers.OpenUrl` 打开浏览器（详见 `GameBannerAndPost.xaml.cs` 第 426-450 行条目），`GameNoticeWindowClosedMessage` 的订阅点随之删除。属既定决策，不为它们写 `####` 条目。
- 另：移植侧独有的 `Features/GameLauncher/GameAuthLoginButton.xaml(.cs)` 是纯新增控件（登录按钮，无上游对应物），不计入偏离；它改变了 `GameLauncherPage` 的按钮排布调用路径，但不构成对上游实现的替换。

---

**旧总账 §3.1 中理由已不成立的条目**

1. **`GameLauncherService.cs` 旧条第 1 项「HDR 整段删除（上游 379-382）」+「理由：HDR 走注册表写 DWM 覆盖项，Linux 无意义」**——不成立。现移植侧第 515-530 行**重新加回** `SetGenshinEnableHDRAsync` 重放（注释引 D95/B41「按 `AppConfig` 重放，保证与界面一致」），且铁律要求启动前完成注册表写入。方向与旧文相反；更麻烦的是 `docs/linux-port/00-decisions.md:336` 仍写着「`WineRegistry` 的 HDR 写入改为『设置页保存时写入』，启动期不再跑 `wine reg`」，与代码和 D95/B41 直接冲突，需一并改文档。应改写为「Linux 用 `WineRegistry` 异步写前缀，故每次启动重放」。同条里的 `StartGameWithCMD` 判断仍然成立。
2. **`GameBannerAndPost.xaml` 旧条第 2 项「理由：`Hyperlink.NavigateUri` 走 `Launcher.LaunchUriAsync`，Linux 静默失败（L-74）」**——引用已失效。L-74 现标题即「`Launcher.LaunchUriAsync` 在 Linux 上**有**实现，失败只发生在个别 scheme/缺 handler」，正文明确「『恒失败、返回 false』是错误定性」。改用 `Helpers.OpenUrl` 仍合理，但理由须改为「失败可判定 + 明确走浏览器 + 可回 toast」，必要性由 must 降为 reasonable。
3. **`GameLauncherSettingDialog.xaml` / `.xaml.cs` / `StartGameButton.xaml` / `.xaml.cs` 的全部旧条目（画刷 `CustomAcrylicBrush`→`LunarwardSurfaceBrush`、Glyph F012→E896 / E917→E823、NavigationView 两列改造、删 `FlipView` Vertical ItemsPanel、删 3 处 `IsTextTrimmedChanged`、`StartGameButton.xaml.cs` 264-280 行 NaN%/越界进度环修复）**——四个文件现已与上游**逐字节相同**（`cmp` 无输出：SettingDialog.xaml 两侧均 37897 B、StartGameButton.xaml 均 18098 B、StartGameButton.xaml.cs 均 14960 B），偏离本身不复存在：① 移植 `App.xaml:44` 已定义 `<AcrylicBrush x:Key="CustomAcrylicBrush">`，「上游画刷键在移植里不存在」不再成立；② 上游 0.19.0 自己把码位改成 E713/E917/E945/E8B7/E71B，旧文里的 F012 对不上当前上游；③ NaN 修复已回流上游——`StartGameButton.xaml.cs:298-300` 现在是 `ProgressRingValue = ringValue;` + `ProgressPercentText = hasPercent ? GameInstallProgressDisplay.ToPercentText(percent) : null;`。这几条应从总账移除，而不是改理由。
4. **`GameLauncherPage.xaml` 旧条第 2 项「`PipsPager_SwitchBackgroundImage` → `StackPanel_BackgroundDots`，理由：Uno 的 PipsPager 模板不响应点击（L-69）」**——理由成立，但描述已过时：上游 0.19.0 把这一整块抽成 `sfb:BackgroundSwitcher` 控件，移植页现在是「整块内联替代控件」（可避免，见 `GameLauncherPage.xaml` 第 57-141 行条目），`PipsPager` 缺口只剩控件内部一处。应拆成「控件级 must（L-69 仍有效）+ 页面级 avoidable（重复实现）」两条。
5. **`GameLauncherService.Linux.cs` 旧条第 3 项把 `ApplyWineRuntimeEnvironment`(348) 记为主启动路径的一部分**——已不成立。主启动路径改由 `RuntimeEnvironment.Assemble` + `LaunchRecipeBuilder.Build` 下发整份环境（`GameLauncherService.Linux.cs` 第 238-436 行），`ApplyWineRuntimeEnvironment` 现在只被 `Program.Linux.cs:332`、`:358` 调用（CLI 路径）；旧文里的行号（34/46/83/108/128/141/160/189/320/348/393/460）也全部漂移。
6. 补充（不属失效项，供合并时留意）：`GameNoticeService` 旧文引「`WineRegistry` 只有异步重载」——仍成立；但移植侧第 33-34 行新加的同步包装 `GetGameNoticeUrl` **无任何调用点**，不要把它当成对外 API 契约写进总账。

### 3.2 安装 / 更新 / 预下载（GameInstall）

#### `Features/GameInstall/GameInstallScanner.Linux.cs`

- **第 1–4 行（文件头注释）、第 71–176 行（ScanAndApply）；调用点 App.xaml.cs:109 与 GameSelector.xaml.cs:1132**｜linux 专属实现｜must
  - 上游：没有扫盘逻辑。安装路径只来自注册表——`FindGamesDialog.xaml.cs:55–110` 读 `HKCU\Software\miHoYo\HYP\1_1\{gameBiz}` 与 `HKCU\Software\Cognosphere\HYP\1_0\{gameBiz}` 的 `GameInstallPath`；而且 0.19.0 上游把主选择器 `GameSelector.xaml(.cs)` 整个删了（`src/Starward/Features/GameSelector/` 下已无此文件），注册表读取只剩这个手动对话框。
  - 移植：启动时 `Task.Run(() => GameInstallScanner.ScanAndApply())` 后台扫盘；遍历 9 个 gameBiz，按 `GameLauncherService.GetGameExeName(biz)` 在候选根目录 BFS 找目录，命中即写 GameEntry 表。
  - 理由：Linux 既没有注册表、也没有启动器写好的安装记录，路径只能靠目录启发式获得；上游又把自动识别删掉后，移植这条反而比上游多一份能力（上游只能在 FindGamesDialog 里手动找）。

- **第 41–69 行（GetInstallPath / SetInstallPath）**｜api 平台 API｜reasonable
  - 上游：路径真源是注册表，key 名里自带 launcher（`jGHBHlcOq1` = 国服官方、`VYTpXlbWo8` = 国际服），不需要推断 launcherId。
  - 移植：经 `GameLauncherService.GetGameInstallPath(GameId)` / `GetRelativePathIfInRemovableStorage` 读，经 `GameEntryService.SetInstallPath(gameId, launcherId, relativePath, removable)` 写；`LauncherId.FromGameBiz(biz)` 取不到时回落 `LauncherId.ChinaOfficial`。
  - 理由：存储层已对齐上游 0.19.0（`AppConfig.GetGameInstallPath` 在上游已 `[Obsolete(..., true)]`），可移动存储写相对路径与上游同口径；唯一新增判定是 launcherId 回落——扫盘只扫得到 biz，拿不到「玩家当初用哪个启动器装的」，对同 biz 多启动器（国服官方 / B 服）会记错渠道。

- **第 73–96 行（缓存命中提前返回）、第 241–266 行（IsCacheFresh / TouchCache）**｜linux 专属实现｜reasonable
  - 上游：无对应逻辑（读注册表是常数开销，不需要缓存）。
  - 移植：`AppConfig` 里存 `GameScanLastUtc`，24 h 内且所有已知路径仍 `Directory.Exists` 就跳过扫盘；`MarkDirty()` 清空时间戳强制重扫。
  - 理由：扫盘要遍历 7 个候选根、每个最多 3 层，比读注册表贵几个数量级，不缓存会让每次启动多几百毫秒到数秒。代价：24 h 内用户把游戏挪走而旧路径仍存在时不会自动纠正，得手动「重新扫描」。

- **第 178–191 行（DefaultScanRoots）**｜linux 专属实现｜reasonable
  - 上游：无对应逻辑（注册表里有什么就是什么）。
  - 移植：`~/games`、`~/Games`、`~/Games/miHoYo`、`/opt`，加三个 Steam 库：`~/.local/share/Steam/steamapps/common`、`~/.steam/steam/steamapps/common`、`~/.var/app/com.valvesoftware.Steam/...`（Flatpak）。
  - 理由：Linux 侧没有统一安装位置约定，只能猜常见布局；Steam 三条覆盖原生、旧布局与 Flatpak 发行版。

- **第 193–238 行（ScanRoots / SetScanRoots / MarkDirty）、第 271–281 行（GetCandidateRoots）、第 274 行（LUNARWARD_GAME_ROOTS）**｜linux 专属实现｜avoidable
  - 上游：无扫描根概念，也没有对应设置项。
  - 移植：`AppConfig.GetValue<string>(null, "GameScanRoots")` 读自定义根（`:` 或换行分隔，`Directory.Exists` 过滤），设置页写回；另有环境变量 `LUNARWARD_GAME_ROOTS` 追加候选根，Release 也生效。
  - 理由：装在 `/data/games` 这类位置的用户必须有办法加根，否则永远认不到；但环境变量这条逃生口绕过了设置页持久化，用户自己都看不见多设了哪些根，而且 `GetCandidateRoots` 里没有任何日志，排查「为什么这个目录被认到了」时无线索。另：`AppConfig.GetValue<T>` 本身是移植侧新增的重载（上游 `src/Starward/AppConfig.Setting.cs` 无此方法），本模块只依赖、未核实其实现。

- **第 286–355 行（GameAliases / ServerMarkers / HasGlobalMarker / NameMatches）**｜linux 专属实现｜reasonable
  - 上游：不需要匹配——注册表键名本身就是 gameBiz，服区由键所在分支（miHoYo / Cognosphere）决定，零歧义。
  - 移植：目录名归一化后先比 `biz.Value`，再比别名表（hk4e → genshin / yuanshen / 原神；nap → zenless / 绝区零 …），再看区服标记；目录名不带标记时，国服直接命中，国际服额外要求 `GenshinImpact_Data` / `GenshinImpact.exe` 佐证。
  - 理由：扫盘最大的风险是国服目录被记成国际服（两侧 exe 同名），只能靠目录名与数据目录判别；别名表是 D95 的修正——旧实现要求「目录名同时含游戏名与服名」，`Genshin Impact`、`Star Rail` 这类真实目录名永远命中不了。残留风险：`HasGlobalMarker` 只认 hk4e 的数据目录名，hkrpg / nap / bh3 的国际服目录若不带 global 字样会被判成国服（真实目录命名未核实）。

- **第 360–410 行（FindGameDirectory）**｜linux 专属实现｜reasonable
  - 上游：无对应逻辑。
  - 移植：BFS，深度上限 3（`if (depth >= 3) continue;`），命中条件 `File.Exists(Path.Combine(child, exeName)) && NameMatches(child, biz)`，`FileAttributes.ReparsePoint` 目录直接跳过，`EnumerateDirectories` 抛异常就跳过该层。
  - 理由：深度 3 才够到 `~/Games/miHoYo/Genshin Impact` 这种布局（旧实现只看两层）；不跟随符号链接是防环路，代价是游戏装在软链挂载点（Steam 库常见）时会漏检，属已知取舍。

- **第 134–170 行（重复占用清理）**｜eng 工程性｜reasonable
  - 上游：无对应逻辑（注册表按 gameBiz 存，天然一个 biz 一条路径）。
  - 移植：扫完统计「同一路径被多个 biz 认领」，保留 `NameMatches` 命中的那个，其余 `GameEntryService.SetInstallPathNull`；整段失败只写 `LogWarning`，不让本次扫描失败。
  - 理由：启发式扫描必须自洽，否则两个启动项指向同一个 exe，更新/启动/卸载互相踩；失败降级为 Warning 而不是 Error 也是对的，去重失败不该丢掉已经扫到的结果。

#### `Features/GameInstall/GameInstallService.cs`

- **第 1–3 行（文件头 LUNARWARD-DEVIATION）、第 19 行（字段）、第 29–35 行（构造函数）**｜api 平台 API｜must
  - 上游：`private readonly GameInstaller.GameInstallerClient _gameInstallerClient = RpcService.CreateRpcClient<...>()`；安装/更新/卸载/进度全部经 gRPC 发给独立的 `Starward.RPC` 进程（`GameInstall.proto`）。
  - 移植：注入 `GameInstallRunner`（`Starward.GameInstall`，进程内），直接调 `SetRateLimiter / CreateContextAsync / StartOrContinueTask / PauseTask / StopTask / UninstallGameAsync`；csproj 同时 `<Compile Remove>` 掉 `GameInstallController.cs`（gRPC 服务端）、`RpcRunner.cs`、`NamedPipesConnectionFactory.cs`。
  - 理由：跨进程宿主属移植侧明确不做的 M4 范围，而 0.19.0 上游自己已把安装逻辑抽成类库，进程内直调即可——旧总账里「Unix socket 替代命名管道」的方案（D6）随之一并作废（D128）。

- **第 47–70 行（StartUpdateTaskProgress 主体）**｜api 平台 API｜reasonable
  - 上游：`using var call = _gameInstallerClient.GetProgress(new EmptyMessage()); await foreach (GameInstallProgressList? list in call.ResponseStream.ReadAllAsync()) { foreach (...) AddOrUpdateTask(item); }`——长连服务端流推送。
  - 移植：`while (true) { foreach (GameInstallProgressDTO dto in _gameInstallRunner.GetAllProgress()) AddOrUpdateTask(dto); if (_tasks.IsEmpty) break; await Task.Delay(1000); }`。
  - 理由：进程内没有可订阅的流，只能定时拉；`GetAllProgress()` 顺带负责释放已结束 context 的 `ServiceScope`（Runner 侧实现）。代价：进度最坏晚 1 s 刷新，且任务在跑期间每秒唤醒一次（实际 CPU 影响未核实）。

- **第 76–83 行（finally 分支）**｜eng 工程性｜avoidable
  - 上游：`if (RpcService.CheckRpcServerRunning()) { await Task.Delay(1000); StartUpdateTaskProgress(); } else { foreach (task) { State = Error; ErrorMessage = Lang.RPCServiceExitedUnexpectedly; NetworkDownloadSpeed = 0; RemainTimeSeconds = 0; } }`
  - 移植：`_getProgressSemaphore.Release(); if (!_tasks.IsEmpty) StartUpdateTaskProgress();`
  - 理由：进程内没有「RPC 进程退出」这一事件，删掉置 Error 分支本身合理；但重启前连上游那 1 s 退避也一起丢了——`GetAllProgress()` 若持续抛异常（例如某个 context 的 `ServiceScope` 已 Dispose），catch → finally → 立即重启会退化成热循环，而任务会一直停在 Running、UI 上没有任何错误提示。补一行 `await Task.Delay(1000)` 即可。

- **第 190–222 行（StartOrContinueTaskAsync）**｜api 平台 API｜must
  - 上游：组装 `GameInstallRequestDTO`（`GameIdDTO` + beta 时 `PassportUserInfoDTO` + `request.CutsceneBlockList.AddRange`），`if (await RpcService.EnsureRpcServerRunningAsync())` 才继续，先 `SetRateLimiterAsync`，再 `CreateContextAsync`，再 `StartOrContinueTaskAsync(request, deadline: DateTime.UtcNow.AddSeconds(5))`；RPC 没起来就记日志 `return null`。
  - 移植：直接构造 `GameInstallRequest`（`GameId` / `LauncherConfig` 传模型，`PassportUserInfo`、`CutsceneBlockList` 直接赋值），`runner.SetRateLimiter(limit > 0, limit)` → `await runner.CreateContextAsync(request, cancellation)` → 同步 `runner.StartOrContinueTask(request)`；没有 EnsureRpcServerRunning、没有 5 s deadline、没有 null 分支（返回类型仍是 `GameInstallProgress?`）。
  - 理由：DTO 两层投影是为跨 gRPC 边界而存在，进程内直接传模型；deadline 是 gRPC 超时概念，进程内调用同步完成。附带效果：上游「RPC 没起来就静默不装、界面无反馈」的半失败路径在移植侧不存在了。

- **第 228–247 行（AddOrUpdateProgress）**｜eng 工程性｜avoidable
  - 上游：没有这个方法；Pause / Continue / Stop 都走 `AddOrUpdateTask(dto)`，该函数在 `task.State is RunnerState.Stop or RunnerState.Finish` 时 `_tasks.TryRemove(gameId, out _)`。
  - 移植：新写 `AddOrUpdateProgress`，只合并 `State / ErrorMessage / Step / CurrentStep / TotalStep / PercentProgress / ProgressDisplay`，**没有** Stop/Finish 的移除分支（`AddOrUpdateTask` 第 120–123 行才保留着）。
  - 理由：`StopTaskAsync` 走到这里时 Runner 已把 context 摘掉（`GameInstallRunner.StopTask` 里 `_tasks.TryRemove` + `ServiceScope.Dispose`），轮询再也拿不到 Stop 快照，于是 `_tasks` 里永久留一条 `State=Stop` 的记录——第 65 行 `if (_tasks.IsEmpty) break` 永不成立，后台变成常驻 1 Hz 轮询；`GetGameInstallTask()` 也会长期返回一个「已停止」的僵尸任务给 UI。按上游语义补 Stop/Finish 分支即可（UI 是否另有兜底清理未核实）。

- **第 250–288 行（PauseTaskAsync / ContinueTaskAsync / StopTaskAsync / ToRequest）**｜api 平台 API｜reasonable
  - 上游：三个方法都先 `CheckRpcServerRunning()`；调不通就把 `task.State = RunnerState.Stop; task.ErrorMessage = Lang.RPCServiceExitedUnexpectedly`。请求走 `ToRequestDTO(task)`，beta 通道会附 `Passport`（Aid / Mid / Stoken）。
  - 移植：无条件调 `_gameInstallRunner.PauseTask / StartOrContinueTask / StopTask(ToRequest(task))`；`ToRequest` 不带 `PassportUserInfo`，也不带 `CutsceneBlockList`。
  - 理由：无 RPC 可失联，兜底分支失去意义；Passport 在 `StartOrContinueTaskAsync` 已随 `GameInstallRequest` 注进 Runner 的 `HoYoPlayService`（`PrepareContextAsync` 里 `hoYoPlayService.PassportUserInfo = request.PassportUserInfo`），暂停/继续/停止只翻状态机不需要再传。风险面：同进程共享 `HoYoPlayService`，beta 任务若被别的任务覆盖了 Passport，这三个方法不再自我纠正（未核实是否真会跨任务覆盖）。

#### `Features/GameInstall/InstallGameDialog.xaml`

- **第 123–128 行（GridView_CategoryScenario 的 ItemContainerTransitions）**｜engine 引擎能力｜must
  - 上游：`<GridView ... ItemContainerTransitions="{x:Null}" SelectedIndex="0" ...>`（显式清空容器过渡，WinUI 合法）。
  - 移植：整个属性删掉，原位留一行 `<!-- LUNARWARD-DEVIATION: Uno 的 GridView 不支持 TransitionCollection，{x:Null} 会让 XAML 编译器崩（UXAML0001），D128 -->`。
  - 理由：Uno 的 XAML 编译器不接受 `ItemContainerTransitions` 的 `{x:Null}`，编译期即 UXAML0001（依据移植侧注释 D128，本轮只读审计未构建核实）。行为差异只是「资源选择网格首次出现时没有容器淡入动画」，不影响选中与绑定。
  - 备注：本文件其余部分与上游逐字节一致，唯一其它差异是第 61–63 行 `MaxLines` / `IsTextTrimmedChanged` 的属性书写顺序（纯格式，不记）。

#### `Features/GameInstall/PreDownloadButton.xaml`

- **第 155–157 行（下载量图标的 FontIcon.Glyph）**｜engine 引擎能力｜must
  - 上游：`Glyph="&#xF012;"`（`Segoe MDL2 Assets` 私有区的「下载」字形）。
  - 移植：`Glyph="&#xE896;"`（Symbols 字体内的下载箭头）。
  - 理由：Uno 拦截 `Segoe Fluent Icons` / `Segoe MDL2 Assets` 两个族名并换用内置 Symbols 字体（1395 字形），fontTools 比对已确认 `F012` 不在其中，真机渲染为空白/方框（L-88 有 strace + fontTools 取证）；`E896` 是 Symbols 内语义相同的字形（D49 决策同批替换）。

- **第 178–181 行（剩余时间图标的 FontIcon.Glyph）**｜engine 引擎能力｜must
  - 上游：`Glyph="&#xE917;"`（MDL2 秒表）。
  - 移植：`Glyph="&#xE823;"`（Symbols 时钟）。
  - 理由：同因，`E917` 缺码位（L-88 同一批取证）；换成 Symbols 内的时钟字形，语义从「秒表」变成「时钟」，对「预计剩余 N 分钟」的提示无歧义。

#### `Features/GameInstall/PreDownloadButton.xaml.cs`

- **第 64–68 行（GetButtonIcon 的暂停分支：`PointerOver && State is RunnerState.Running`）**｜engine 引擎能力｜must
  - 上游：`return "\uE62E";`
  - 移植：`return "\uE769";`
  - 理由：与上面两个 XAML 图标同因——`E62E` 不在 Uno 内置 Symbols 字体里（L-88 用 fontTools 逐码位比对过），暂停图标会显示成方框；`E769` 是 Symbols 的双竖条暂停字形，与 `GameLauncherPage.xaml.cs:1355` 的 `PauseIcon` 取值一致。
  - 备注：本文件相对上游只有两处 diff——上面这条，以及第 1 行去掉 UTF-8 BOM、第 6 行多一个未被使用的 `using System;`（纯格式/冗余，不记）。

---

##### 附 · 与旧总账 §3.2 的对照（旧 37 条 / 8 文件 → 现 18 条 / 5 文件）

| 旧条目所在文件 | 旧条数 | 现状 | 结算 |
| --- | --- | --- | --- |
| `InstallGameDialog.xaml` | 1 | BOM 与属性换序已与上游一致；**新发现**唯一的实质偏离是 GridView 过渡（旧条目没记到） | 1 条作废 / 1 条新增 |
| `InstallGameDialog.Linux.cs` | 6 | 文件已删除，上游 `InstallGameDialog.xaml.cs` 逐字节照用（默认路径、包体/空间计算、按钮使能、语音选择全部回归上游） | 6 条全作废 |
| `GameInstallService.Linux.cs` | 16 | 文件改名重写为 `GameInstallService.cs`，自研 Sophon 管线整体删除，只剩 facade | chun/差分/续传/修复基准/patch 复用/版本标记/临时文件清理/DEBUG 限额/进度镜像/本地不一致自检/目录守卫/额外 mode 形参/installPath 回落 **等 13 条作废**；「RPC→进程内」「任务表维护」2 条**仍成立但形态已变**（见上）；另新增 3 条（轮询抖动、退避丢失、僵尸任务）与 1 条（Passport 缺省） |
| `PreDownloadDialog.xaml` + `PreDownloadDialog.Linux.cs` | 7 | 两文件现与上游逐字节一致（不在本组清单，仅作对照） | 7 条全作废 |
| `PreDownloadButton.xaml` | 1 | 图标码位替换仍在，行号 157/181 未变 | 仍成立（拆成 2 条） |
| `PreDownloadButton.xaml.cs` | 2 | 暂停图标 1 条仍成立；「NaN% 守卫」已作废——上游 0.19.0 改用 `GameInstallProgressDisplay.TryGetPercent/ToPercentText`，两侧逻辑同源 | 1 条仍成立 / 1 条作废 |
| `GameInstallScanner.Linux.cs` | 4 | 4 条框架仍成立；路径落盘已改 `GameEntryService`（对齐上游 0.19.0），严格匹配的 bug 已由 D95 别名表修掉 | 4 条仍成立（内容已更新） |

未核实项汇总：① Uno 对 `ItemContainerTransitions="{x:Null}"` 的编译错误仅有移植侧注释为据，未构建复现；② 轮询 1 Hz 的实际 CPU/耗电未测；③ hkrpg/nap/bh3 国际服目录的真实命名未核实（影响 `HasGlobalMarker` 的覆盖面）。

### 3.3 游戏设置 / 运行环境 / 账号（GameSetting / Runtime / GameAccount）

##### 附 · 3.3 游戏设置与运行环境（GameSetting / HoYoPlay / GameAccount）
#### `Features/GameAccount/GameAccountService.cs`

- **第 38-89 行（GetGameAccountFromRegistryAsync 主体）**｜api 平台 API｜must
  - 上游：`GetGameAccountFromRegistry` 用 `Microsoft.Win32.Registry.GetValue(keyPath, keyName, null) as byte[]` 取 ADL 票据，再按分游戏多次 `GetValue` 取 Uid（上游 L28-70）
  - 移植：`WineRegistry.GetKeyValuesAsync(keyPath, cancellationToken)` 一次取回整键字典，再 `TryGetValue` + `is byte[]` / `is int` 模式匹配（L120-135 同时抽出 `MergeAccounts`）
  - 理由：Linux 没有 Win32 注册表 API，值实际在游戏所用 wine 前缀的 `user.reg` 里，只能解析该文件或走 `wine reg query`；整键一次读回把 3-4 次 fork 降到 1 次。类型判断成立（`WineRegistryFiles` 把 dword 解析成 `int`，见其 L212-215），不存在 Uid 读成 `long` 而丢失的问题。

- **第 47 行（同上，读路径的注销）**｜linux 专属实现｜avoidable
  - 上游：读写同一个 `HKCU\Software\miHoYo\...` 键，天然一致
  - 移植：读用 2 参 `GetKeyValuesAsync(key, ct)`，`winePrefix = null` → `WineRegistry` L180 回落到 `WinePrefix`（默认 `~/.wine` 或 `LUNARWARD_WINEPREFIX`）；而写入侧用 `GameSettingService.GetGameWinePrefix(account.GameBiz)` 解析出的**游戏前缀**（L197）
  - 理由：读默认前缀、写游戏前缀。游戏绑定了 profile 前缀时，账号列表会显示 `~/.wine` 里的旧账号（或空），切号却写进游戏前缀。带前缀的 `GetKeyValuesAsync(key, prefix, ct)` 已存在，`WineRegistry.UsePrefix`（L33）也已实现但**全仓无调用点**——不是能力缺口，属可避免的实现错位；旧账「读写 `~/.wine` 而非游戏实际前缀」在写侧已修，读侧仍成立。

- **第 174-209 行（ChangeGameAccountAsync）**｜linux 专属实现｜reasonable
  - 上游：逐条 `Registry.SetValue` 写 ADL 票据与 Uid（上游 L137-160，2-4 次调用）
  - 移植：先聚合成 `Dictionary<string, object>`（票据必写 + 按分游戏写 Uid），再 `WineRegistry.SetValuesAsync(keyPath, values, prefix, ct)` 一次 `wine reg import`
  - 理由：每次注册表写入在 Linux 上是 fork 一次 wine（冷 ~1.1 s），合并成一次 import 才不冻窗口；REG_DWORD / REG_BINARY 的值类型经 `RegistryFormat` 生成与上游逐值写等价。旧账判 must 在这里不变（平台强制），只是手段从「逐值」变「批量」。

- **第 196-206 行（写入失败与前缀缺失的处理）**｜linux 专属实现｜must
  - 上游：`Registry.SetValue` 失败直接抛异常（用户看得到）
  - 移植：前缀为空抛 `Lang.Lunarward_Error_PrefixNotFound`；`SetValuesAsync` 返回 false 时 `LogWarning` 后抛 `Lang_Lunarward_Error_SetGameSettingFailed`，由 `GameAccountSwitcher` 弹 `InAppToast`（B40）
  - 理由：必须写进游戏真正的前缀，写不进去还得让用户知道——旧账 L196-201 判 `avoidable` 的依据是「只 `LogWarning` 后静默 return」，现已改为上抛，该判定**不再成立**。

- **第 29-30、106-107、168-169 行（三个同步包装）**｜eng 工程性｜reasonable
  - 上游：`GetGameAccountFromRegistry` / `GetGameAccounts` / `ChangeGameAccount` 本身就是同步签名
  - 移植：三个同步方法保留为 `Async(...).GetAwaiter().GetResult()`，另出 Async 版供 UI 用
  - 理由：保持上游调用签名（`GameAccountSwitcher.xaml` 里命令名与外部调用点不变）；实际 UI 路径已改 async。读侧有 `WineRegistryFiles` 的文件快路径兜底（毫秒级、不 fork），写侧同步版仍是真 fork，只在非 UI 线程可用。

#### `Features/GameAccount/GameAccountSwitcher.xaml.cs`

（同目录 `GameAccountSwitcher.xaml` 与上游逐字节相同，未改；`[RelayCommand]` 去掉 `Async` 后缀后生成的命令名仍是 `ChangeGameAccountCommand` / `DeleteGameAccountCommand`。）

- **第 36-39、73-85 行（Loaded 与 UpdateGameAccountAsync）**｜linux 专属实现｜must
  - 上游：`GameAccountSwitcher_Loaded` 调同步 `UpdateGameAccount()`（上游 L36-39、L73-85）
  - 移植：Loaded 改 `async void` 内 `await UpdateGameAccountAsync()`，方法改返回 `Task`
  - 理由：`GetGameAccounts` 现在要读 wine 前缀注册表（可能 fork），同步调用会把整个窗口冻住（L-127）；UI 线程不许同步等外部进程。

- **第 88-105 行（ChangeGameAccountAsync 的失败反馈）**｜bugfix 修上游缺陷｜reasonable
  - 上游：`ChangeGameAccount()` 同步 void，`catch` 里只 `_logger.LogError`，界面看起来像成功
  - 移植：改 `async Task`，`catch` 里 `LogError` + `InAppToast.MainWindow?.Error(ex.Message, null, 6000)`（B40）
  - 理由：Linux 侧新增了「前缀不存在」「reg import 失败」两种失败源，必须让用户看见；`[RelayCommand]` 方法改名不影响 XAML 绑定名。

- **第 147-162 行（DeleteGameAccountAsync）**｜linux 专属实现｜reasonable
  - 上游：`DeleteGameAccount()` 同步 void，删完调同步 `UpdateGameAccount()`（上游 L145-159）
  - 移植：改 `async Task`，删完 `await UpdateGameAccountAsync()`
  - 理由：删除本身只碰 SQLite，但紧接着的刷新已异步化，保持整条链在后台线程；命令名 `DeleteGameAccountCommand` 不变。（`SaveGameAccountAsync` 上游本来就是 async，内容一致，不记偏离。）

#### `Features/GameSetting/GameSettingPage.xaml`

- **第 2 行（根元素属性）**｜linux 专属实现｜reasonable
  - 上游：`<sf:PageBase x:Class="..." ...>` 无缓存设置（0.19.0 基类仍叫 `PageBase`，`WindowEx→WindowBase` 的改名不涉及本页）
  - 移植：`NavigationCacheMode="Required"`
  - 理由：Uno 的 `Frame` 每次导航都重建页面（实测切到本页 1729 ms，其中 ctor 132 ms），不缓存则每次进设置页都要重读注册表；缓存后靠 `OnLoaded` / `OnGameChanged` 重取数据（D60/D94）。代价是必须手工刷新，见 `.xaml.cs` 对应条目。

- **第 14 行（根 Grid 背景）**｜cosmetic 外观文案｜reasonable
  - 上游：`<Grid>`
  - 移植：`<Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">`
  - 理由：Linux 无 DWM 丙烯/云母，透明区域会透出背景壁纸的大图，表单文字可读性下降；给不透明实底规避（与上游 Setting 页同款处理）。

- **第 20-21、34-47 行（ScrollViewer 内层结构）**｜cosmetic 外观文案｜reasonable
  - 上游：`<ScrollViewer>` 里直接是 `<Grid Margin="24,20,24,24" ColumnSpacing="24">`，无 `RowDefinitions`
  - 移植：`<ScrollViewer x:Name="ScrollViewer_Main"><StackPanel>` 包一层，内层新增 `<Grid x:Name="Grid_Content">` 与 4 个 `RowDefinition Height="Auto"`（L45 注释：诊断区独占一行，否则与第 0 行分辨率重叠，T-UI-31）
  - 理由：给新增的「运行配置 / 后启动程序 / 诊断」三个分区提供行位。行高全 `Auto` 时 Uno 不随子项后置可见而重算行高，所以 `.xaml.cs` L373 显式 `Grid_Content.InvalidateMeasure()` 补一刀。

- **第 23-33 行（ScrollViewer 顶部 InfoBar）**｜linux 专属实现｜reasonable
  - 上游：（无）
  - 移植：`<InfoBar IsOpen="{x:Bind IsProfileBindingMissing}" Title="运行环境绑定失效" Message="{x:Bind MissingProfileBindingMessage}">` + `Button_RebindProfile_Click`
  - 理由：游戏→运行方案（Profile）的绑定是 Linux 专属概念，绑定指向已删除方案时必须提示用户而不是只写日志（D61）。文案是硬编码中文、未走 `Lang`，属 i18n 欠账（与全库文案策略不一致，未核实是否有意为之）。

- **第 117 行（图形设置 StackPanel 的附加属性）**｜cosmetic 外观文案｜reasonable
  - 上游：`<StackPanel Grid.Row="1" Grid.Column="1" ...>`（原 Grid 只有一行，`Grid.Row="1"` 被夹回第 0 行）
  - 移植：`<StackPanel Grid.Column="1" ...>`（去掉 `Grid.Row="1"`）
  - 理由：移植给 `Grid_Content` 加了 4 行定义，照抄 `Grid.Row="1"` 会让图形设置与第 1 行的「运行配置」分区重叠；显式落回第 0 行才与上游视觉一致。

- **第 170-186 行（「运行配置」分区）**｜linux 专属实现｜reasonable
  - 上游：（无）
  - 移植：`<ComboBox x:Name="ComboBox_Profile" DisplayMemberPath="Name" SelectionChanged="ComboBox_Profile_SelectionChanged"/>` 与「按游戏绑定 Wine/Proton 运行方案」
  - 理由：Linux 才有运行器与前缀，按游戏绑定方案是 D73/M2 的核心交互；上游无对应概念。

- **第 188-212 行（「后启动程序」分区）**｜linux 专属实现｜reasonable
  - 上游：（无）
  - 移植：`Button_AddCompanion` / `Button_OpenCompanionFolder` / `TextBlock_CompanionEmpty` / `Panel_Companions` 一组控件
  - 理由：伴随启动（D24/D28）是 Linux 新增模块（游戏启动后/退出后跑 wine 原生程序或脚本），上游无对等能力。条目删除没有二次确认（`.xaml.cs` L764-771），属可打磨的交互点。

- **第 215-225 行（「诊断」分区与运行启动检查按钮）**｜linux 专属实现｜reasonable
  - 上游：（无；预检只在启动流程内部触发，用户看不到明细）
  - 移植：`<Button x:Name="Button_Diagnose" Click="Button_Diagnose_Click" Content="运行启动检查"/>`
  - 理由：Linux 启动链（wine 版本 / 前缀 / DXVK / 游戏文件 / 运行方案）失败原因多，必须能自查（D59/D63）；WIN 侧没有运行器概念，不需要该入口。

#### `Features/GameSetting/GameSettingPage.xaml.cs`

- **第 33-75 行（Button_Diagnose_Click）**｜linux 专属实现｜reasonable
  - 上游：（无）
  - 移植：`GetGameInstallPath` + `GetGameExeNameAsync` + `ProfileBinding.Resolve` + `LaunchPreflight.Check`，再用代码构建的 `ContentDialog` 逐条列 `PreflightItem`
  - 理由：预检六项的展示入口（D59/D63），上游预检项集合本身不同（无前缀/DXVK/运行器），无法照搬 XAML；对话框内容全代码构建，未落 XAML（与 `DiagnosticBundle` 同思路）。

- **第 91 行、第 142-145 行（OnNavigatedTo 的 Stopwatch、OnLoaded 的滚动钩子）**｜eng 工程性｜avoidable
  - 上游：`OnNavigatedTo` 无计时段；`OnLoaded` 无滚动钩子（上游 L44-62）
  - 移植：`var swNav = System.Diagnostics.Stopwatch.StartNew();` 定义后**从未读取也未 Stop**；`if (DevSwitches.Scroll is int offset && offset > 0) ScrollViewer_Main.ChangeView(...)`
  - 理由：两者都是开发期插桩（硬规则 7）：`swNav` 是导航耗时打点的残留；`--scroll` 只为 Wayland 下截图定位（L-36），发布版需 `LUNARWARD_DEV=1` 才生效。旧账「L90 swNav / L140-144 DevSwitches.Scroll 判 avoidable」**仍成立**，只是行号变为 91 / 142-145。

- **第 95-101、110-111、150-157 行（免费/缓存页的生命周期补丁）**｜linux 专属实现｜reasonable
  - 上游：`OnNavigatedTo` 直接按 `CurrentGameBiz` 设 Emoji、做 bh3 区服重映射；页面每次导航都新建，没有「切游戏重载」概念
  - 移植：空 `GameBiz` 提前 return 并给 toast 空态；每次导航 `RefreshProfileSelector()` + `RefreshCompanions()`；新增 `protected override async void OnGameChanged()`
  - 理由：`NavigationCacheMode="Required"` 把生命周期从「每次 new」变成「一次 ctor + 多次导航」，方案下拉与伴随启动列表必须每次刷新，否则切游戏后仍显示上一个游戏的绑定。`PageBase.OnGameChanged` 是移植侧新增虚方法（`src/Lunarward/Features/PageBase.cs:77`，上游没有，标了 `LUNARWARD-DEVIATION: D94`），是本组的共享依赖，不是本文件私货。

- **第 126-139 行（OnLoaded 里的 InitializeResolutionItem）**｜engine 引擎能力｜must
  - 上游：`OnLoaded` 直接 `InitializeResolutionItem();`，方法内用 `DisplayArea.GetFromWindowId(this.XamlRoot.ContentIslandEnvironment.AppWindowId, ...)`（上游 L284）
  - 移植：整段包 `try/catch`（catch 里只留注释，`ex` 未用）
  - 理由：Uno 没有 `XamlRoot.ContentIslandEnvironment`（Windows App SDK 的 ContentIsland 概念），`DisplayArea.GetFromWindowId` 必抛，只能吞掉让页面活着；代价是分辨率下拉在 Linux 恒空，用户只能用自定义宽高输入。旧账判 `engine/must` **仍成立**，且这是玩家可见的功能缺口（不是纯代码洁癖）。

- **第 336-355 行（InitializeGameSettingAsync 的 hk4e 分支，HDR 状态）**｜engine 引擎能力｜must
  - 上游：`_displayInformation = DisplayInformation.CreateForWindowId(this.XamlRoot.GetAppWindow().Id); AdvancedColorInfoChanged += ...; UpdateHdrState(...)`（上游 L236-238）
  - 移植：`this.XamlRoot.GetAppWindow()` 可能为 null → 直接 `HDRNotSupported = true`；`CreateForWindowId` 与 `UpdateHdrState` 再各包一层 try/catch（`UpdateHdrState` L594-611 的内层是空 catch）
  - 理由：`Microsoft.Graphics.Display` 的 HDR 能力在 Linux/Uno 无实现，只能按「显示器不支持 HDR」兜底；副作用是原神设置页常亮「当前显示器不支持 HDR」的警告，与真实原因（引擎没有 HDR 查询能力）不符，文案上对用户偏误导。同一处 `_displayInformation` 在 `OnGameChanged` 重入时被**直接重新赋值，旧实例没有 Dispose、`AdvancedColorInfoChanged` 也没有退订**（L343-345），每次切游戏泄漏一个实例与一个事件订阅。

- **第 361-373 行（读注册表段与 InvalidateMeasure）**｜linux 专属实现｜reasonable
  - 上游：三处同步 `Registry` 读取直接赋属性（`GetGameResolutionSetting` / `GetStarRailFPSIndex` / `GetGameVoiceLanguageSetting`，上游 L229/L242/L263）
  - 移植：`await Task.Run(() => new GameSettingSnapshot(...))`（record 定义 L410-413）把读取挪到后台线程，取回后一次性赋属性；结尾 `DispatcherQueue.TryEnqueue(() => Grid_Content.InvalidateMeasure())`
  - 理由：读一次 wine 前缀注册表在冷路径上是一次 fork（~1.1 s），UI 线程同步读会整窗冻住（L-127）；`InvalidateMeasure` 补 Uno「行高 Auto 不随子项后置可见重算」的缺口。

- **第 554 行（ApplySettingAsync 里的分辨率写入）**｜eng 工程性｜avoidable
  - 上游：`ApplySetting` 同步顺序调四个写入方法（`SetGameResolutionSetting` / `SetGameVoiceLanguageSetting` / `SetStarRailFPSIndex` / `SetGenshinEnableHDR`，上游 L419/424/430/435）
  - 移植：方法已整体改 `ApplySettingAsync`，语音 / 星铁 FPS / HDR 三处都 `await ...Async`（L559/565/570），**唯独分辨率仍调同步包装** `GameSettingService.SetGameResolutionSetting(CurrentGameBiz, model)` → 内部 `GetAwaiter().GetResult()`（`GameSettingService.cs:86-87`）
  - 理由：同一个方法里其余三处已经异步化，说明「平台必须异步」已被承认，唯独这里漏改：点「应用」时会在 UI 线程上等一次 `wine reg import`。异步版 `SetGameResolutionSettingAsync` 就在同文件 L92 且已导出，属可避免的冻结点（不是平台限制）。

- **第 591-936 行（#region 后启动程序）**｜linux 专属实现｜reasonable
  - 上游：（无）
  - 移植：`RefreshBindingWarning` / `Button_RebindProfile_Click` / `RefreshProfileSelector` / `ComboBox_Profile_SelectionChanged` / `RefreshCompanions` / `CreateCompanionRow` / `ShowCompanionEditorAsync` / `Button_OpenCompanionFolder_Click`（`xdg-open`）/ `Button_AddCompanion_Click`
  - 理由：绑定管理与伴随启动的整套编辑界面（D24/D28/D61），上游无对应功能，只能新写。两处可打磨：删除条目直接 `CompanionStore.Remove` 无二次确认（L764-771）；`xdg-open` 在 UI 线程直接 `Process.Start`（参数已是白名单路径、毫秒级返回，可接受，但严格按硬规则 10 也属外部进程调用）。

#### `Features/GameSetting/GameSettingService.cs`

（上游 0.19.0 同名文件 321 行，**未变**：仍是 `Microsoft.Win32.Registry.GetValue/SetValue` 全量实现，`SetGenshinEnableHDR` 仍在 L249-256 直写 `WINDOWS_HDR_ON_h3132281285`，`GetGenshinHDRLuminance` 默认值仍 1000/300/350。旧账对上游的说法不需要修订。）

- **第 42-82、148-179、228-251、353-382 行（全部读写入口）**｜api 平台 API｜must
  - 上游：`Registry.GetValue` / `Registry.SetValue` 直读写 `HKCU\Software\Cognosphere\...`、`miHoYo\...`
  - 移植：`WineRegistry.GetValue(..., prefix)` / `GetKeyValuesAsync(key, prefix, ct)` / `SetValueAsync` / `SetValuesAsync`；写时生成 UTF-16LE BOM 的 `.reg` 再 `wine reg import`，读时先走 `WineRegistryFiles` 直接解析 `user.reg`（毫秒级、不 fork），结论不足才 `wine reg query`
  - 理由：Linux 没有 Win32 注册表 API，分辨率/语音/星铁 FPS/HDR 这些值实际落在游戏所用 wine 前缀的 `user.reg` 里。平台 API 缺口，无法回流。

- **第 45、54、63-75、151-164、233、364 行（读路径取前缀）**｜linux 专属实现｜avoidable
  - 上游：读取目标无歧义（同一 `HKCU` 键）
  - 移植：`string? prefix = ResolveGamePrefix(biz)` 后传给 `WineRegistry`；`ResolveGamePrefix`（L335-347）= `ProfileBinding.Resolve` + `PrefixManager.TryGetExistingPrefix`，任一失败 `catch` 后返回 null，`WineRegistry` 收到 null 即回落到默认 `~/.wine`
  - 理由：写路径在同样条件下会抛「前缀不存在」，读路径却静默去 `~/.wine` 取值。真实命中场景：`ProfileBinding.Resolve` 在「一个运行方案都没有」时返回 null（`src/Lunarward/Features/Runtime/ProfileBinding.cs:320`），`TryGetExistingPrefix` 在前缀目录还不存在时返回 null（`PrefixManager.cs:368`）——恰好就是「首次启动游戏前先设分辨率/语音」这条路径（本页 XAML L97 的说明「首次启动游戏前设置此值可以避免下载部分语音资源」就是为它写的），用户会看到默认前缀里的旧值或读不到值。可照上游语义改为「前缀不可用就返回 null 并提示」。

- **第 73 行（GetGameResolutionSetting 的 nap 分支全屏标志）**｜linux 专属实现｜avoidable
  - 上游：`(int)(Registry.GetValue(keyPath, Screenmanager_Fullscreen_mode_h3630240806, 0) ?? 0) != 3`（无前缀概念）
  - 移植：`WineRegistry.GetValue(keyPath, Screenmanager_Fullscreen_mode_h3630240806, 0)`——**漏传 prefix**；紧邻的宽高（L74-75）都传了 prefix
  - 理由：同一分支三个值里两个读游戏前缀、全屏标志读默认前缀，绝区零的全屏勾选可能显示成别的前缀的值。纯笔误，补一个参数即可，不是平台限制。

- **第 92-110、113-143、187-224、259-304 行（写入路径批量化）**｜linux 专属实现｜reasonable
  - 上游：每个值一次 `Registry.SetValue`（分辨率 3-4 次、星铁 FPS 2 次、语音 1-2 次）
  - 移植：拆出 Async 版 + 一次 import 写整批：`BuildResolutionSettingValues` 返回 `Dictionary<string, object>`（L113-143）；`SetStarRailFPSIndexAsync` 把 `GraphicsQuality=0` 与 `GraphicsQuality2_Model_...` 合并（L295-299）
  - 理由：每次注册表写 = fork 一次 wine（冷 1.1 s / 热 0.13-0.2 s，L-127），合并后 3-4 次降到 1 次才不冻窗口；生成的 `REG_DWORD`/`REG_BINARY` 与上游逐值写等价。

- **第 100-108、191-194、274-277、321-325、402-405 行（前缀缺失与写失败检查）**｜linux 专属实现｜must
  - 上游：（无对应概念；写失败由 `Registry.SetValue` 抛 Win32Exception）
  - 移植：`prefix` 为空抛 `Lunarward_Error_PrefixNotFound`；`SetValuesAsync` 返回 false 抛 `Lunarward_Error_SetGameSettingFailed`（B10/B43）
  - 理由：必须写到游戏真正使用的前缀，否则设置对游戏无效；上游键路径唯一无歧义，这里的前缀解析是平台强制的。旧账把这段判 `avoidable`（理由「取不到前缀静默 return」）**不再成立**——但下一行说明 HDR 的实际写入仍会吞错。

- **第 327 行（SetGenshinEnableHDRAsync 的写入）**｜linux 专属实现｜avoidable
  - 上游：`Registry.SetValue(key, WINDOWS_HDR_ON_h3132281285, enableHDR ? 1 : 0);`（失败抛异常，上游 L249-256）
  - 移植：`await WineRegistry.SetValueAsync(key, WINDOWS_HDR_ON_h3132281285, enableHDR ? 1 : 0, prefix, ct)`——`SetValueAsync` 内部 `catch (Exception ex) { Log.Debug(...); }` 无返回值（`WineRegistry.cs:237-252`）
  - 理由：分辨率/语音/星铁 FPS 都走能返回 bool 的 `SetValuesAsync` 并抛错，只有 HDR 走单值版、写失败被 `Log.Debug` 吞掉；调用方（本页 L569-570）已先写 `AppConfig.EnableGenshinHDR`，界面仍显示「应用成功」，下次启动游戏 HDR 却没开。同文件就有可报错的批量写，不是平台限制，属该补齐的一处静默失败——旧账「静默跳过」在前缀缺失的一半已修，在写入失败的一半**仍成立**。

- **第 86-87、182-183、254-255、308-309、353-354、386-387 行（同步包装）**｜eng 工程性｜reasonable
  - 上游：只有同步 API（依赖它们的调用点是上游原样的 `GameLauncher` 代码）
  - 移植：每个写方法保留同名同步版，实现为 `Async(...).GetAwaiter().GetResult()`
  - 理由：让未改写的上游调用点（`GameLauncherService.cs:742`、`GameLauncherSettingDialog.xaml.cs:763/853`）继续编译且签名不变；代价是同步版被 UI 线程调用就是同步等待，只能靠调用点自觉（`GameSettingPage.xaml.cs:554` 正是反例）。

- **第 286-292 行（SetStarRailFPSIndexAsync 缺键时的兜底值）**｜eng 工程性｜avoidable
  - 上游：键不存在时只写 `{"FPS":{{fps}}}`（上游 L233-243）
  - 移植：键不存在时写整套 17 字段模型（`{"FPS":...,"EnableVSync":false,"RenderScale":1.0,"ResolutionQuality":3,...,"AAmode":1,"DlssQuality":0}`）
  - 理由：兜底时凭空生成其余画质字段，等于用移植侧硬编码值覆盖用户/游戏的画质设置（`RenderScale=1.0`、各 `Quality=3` 未必等于该版本游戏默认）。是否因为「只写 FPS 会让星铁读不出模型」而故意为之，未核实；若成立应在决策文档登记，否则建议回到上游写法或从现有键读默认。

#### `Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs`

（上游 0.19.0 侧 `.xaml`（202 行，含 `canvas:CanvasSwapChainPanel_Mora/Scene`）与 `.xaml.cs`（343 行）都还在，**未删除**。移植侧把 `.xaml` 改名为 `GenshinHDRLuminanceSettingDialog.xaml.disabled`（内容与上游**逐字节相同**），`.xaml.cs` 与上游也逐字节相同但被 `<Compile Remove>`（`src/Lunarward/Lunarward.csproj:110`）；csproj **没有任何 `Page Remove`**，XAML 不入构建纯粹因为扩展名不再是 `*.xaml`，无任何编译中的 XAML 引用该对话框。所以对话框**不是「整体不可用」，而是手写 UI**：本文件在 ctor 里 `Content = BuildContent()`。）

- **第 1-45、83-101 行（整类：Win2D HDR 预览整块删除，改为代码构建 3 个滑块）**｜engine 引擎能力｜must
  - 上游：`GenshinHDRLuminanceSettingDialog.xaml`（202 行）提供 1200×680 的 HDR 预览面板：两个 `canvas:CanvasSwapChainPanel`（Mora 角色 / 场景）由 `.xaml.cs` 的 `DrawMora` / `DrawScene`（L151-234）用 Win2D 画（`R16G16B16A16Float` swapchain、`LinearTransferEffect`、`CanvasBitmap.LoadAsync` 载 `Scene.jxr`/`UI.jxr`/`Sign.png`）
  - 移植：`Content = BuildContent()` 用 `StackPanel` + 3 个 `Slider`（最大亮度 / 场景亮度 / UI 亮度）+ 数值 `TextBlock` + 「恢复默认」`Button`，预览面板整体不存在
  - 理由：Uno/Skia 下没有 `CanvasSwapChainPanel`/Win2D（`Microsoft.Graphics.Canvas`），HDR 预览需要交换链与浮点渲染目标，平台无该能力。保留预览只能整块重写成 Skia 端口，超出「软差异」边界（AGENTS §8 的引擎差异处置）。因此把可用的调节能力（读/写/恢复默认）留下，预览删掉。

- **第 20-21、33-45 行（XAML 摘出构建 + 手写 Content）**｜cut 裁剪｜reasonable
  - 上游：`GenshinHDRLuminanceSettingDialog.xaml` 由 SDK 的 `Page` glob 编译出 `InitializeComponent()`，`.xaml.cs` 里 `this.InitializeComponent()`（L40 附近）加载
  - 移植：`.xaml` 改名 `.disabled`（不入 `Page` glob）、`.xaml.cs` 显式 `Compile Remove`，本文件成唯一的 partial 实现，`Title`/按钮文案在 ctor 里设、内容全代码构建
  - 理由：保留上游 XAML 就要保留两个不存在的 `CanvasSwapChainPanel` 与全部 Win2D 绑定，得大改布局；改成代码构建后只留三个滑块，改动集中、无 XAML 资源依赖。属裁剪手段而非平台强制（也可以只删画布节点留 XAML），故判 `reasonable` 而非 `must`。

- **第 22-31、104-137 行（对话框状态与命令面裁剪）**｜cut 裁剪｜reasonable
  - 上游：`.xaml.cs` 暴露 `HDRNotSupported` / `HDRNotEnabled` / `HDREnabled` / `DisplayInfomation` / `MoraVisibility`（L101-109）与 `MaxLuminance` / `SceneLuminance` / `UILuminance` 双向绑定、两个 InfoBar「显示器不支持 HDR」「Windows HDR 未开启」（xaml L70/L75）
  - 移植：这些属性与其绑定一起消失；本文件只保留 3 个 `Slider` + 数值显示，`OnOpened` 时 `await GameSettingService.GetGenshinHDRLuminanceAsync(CurrentGameBiz)` 回填
  - 理由：这些属性全部服务于被删掉的预览/告警 UI，XAML 不编译后留着就是死代码；`DisplayInfomation` 的数据源（`DisplayInformation.CreateForWindowId`）在 Linux 也不存在。裁剪边界合理，但用户失去了「Windows HDR 未开启」这类前置提示。

- **第 104-115 行（OnOpened 异步读注册表）**｜linux 专属实现｜must
  - 上游：`.xaml.cs` L52 在 `Loaded` 里同步 `GetGenshinHDRLuminance(CurrentGameBiz)` 赋初值
  - 移植：`async void OnOpened` 内 `await GameSettingService.GetGenshinHDRLuminanceAsync(CurrentGameBiz)`，取不到值沿用默认 1000/300/350
  - 理由：读值要访问 wine 前缀注册表（可能 fork 一次 wine），同步读会卡住对话框（L-127）；`async void` 事件处理器是 WinUI 的惯用写法。默认值已与上游一致（旧的 1000/200/400 已修）。

- **第 118-135 行（OnPrimaryButtonClick 的 deferral 与失败反馈）**｜linux 专属实现｜must
  - 上游：`.xaml.cs` L283-296 `Save` 同步 `SetGenshinHDRLuminance(...)` 后 `Hide()`，失败只 `Debug.WriteLine`
  - 移植：`PrimaryButtonClick` 里 `args.GetDeferral()` → `await SetGenshinHDRLuminanceAsync(...)` → 失败弹 toast 并 `args.Cancel = true`（不关对话框）→ `deferral.Complete()`
  - 理由：写注册表是 fork wine 的异步操作，对话框不能先关；且「前缀不存在 / reg import 失败」必须让用户看到，否则亮度设置静默丢失（B40 同类缺陷）。上游的同步 + 吞错在这里不可复用。

- **第 140-146 行（恢复默认）**｜linux 专属实现｜reasonable
  - 上游：`.xaml.cs` L300-313 `Reset` 把三个值设回 1000/300/350 并重新 `Save`
  - 移植：`Reset` 把三个滑块设回 1000/300/350，**不立即写注册表**，仍需点「保存」才落盘
  - 理由：写盘是异步 + 可能失败，若 Reset 直接写就得在命令里塞 toast/异常分支；改成只改 UI 值把「恢复默认」降级为纯视图操作，语义与上游略不同（上游点了就写），属交互取舍——顺手修掉了「点恢复默认立刻写盘但看不见失败」的问题。

- **第 83-101 行（硬编码中文提示与借用的标题）**｜cosmetic 外观文案｜avoidable
  - 上游：正文全走 `lang:Lang.*`（`GenshinHDRLuminanceSettingDialog_MonitorInformation` 只是对话框内部小节标题，见 xaml L46）；对话框 `Title` 由宿主页面设置
  - 移植：`Title = Lang.GenshinHDRLuminanceSettingDialog_MonitorInformation`（把内部小节标题借来当对话框标题），内容里 L86-90 有硬编码中文「Linux：HDR 预览暂不可用，只能调整写入游戏的亮度参数。」
  - 理由：该字符串未走 `Lang`，切英文界面时这句仍是中文（同页 XAML 的 InfoBar 文案也是硬编码中文，全库 i18n 未收口）；标题借用「显示器信息」语义也偏了（此对话框现在调的是亮度，不是显示器信息）。低风险、纯文案，故 `avoidable`。

- **第 148 行（AutoAdjust 自动调节命令删除）**｜engine 引擎能力｜must
  - 上游：`.xaml.cs` L317-333 `AutoAdjust` 从 `DisplayInformation` 的 `MaxLuminanceInNits`（clamp 300-2000）与 `SdrWhiteLevelInNits + 20`（clamp 100-500）推推荐值，UI = 场景 + 50；xaml L64 有按钮
  - 移植：（无）
  - 理由：自动调节的唯一数据源是 `Microsoft.Graphics.Display.DisplayInformation` 的 HDR 亮度信息，Linux/Uno 无该 API（与本组 GameSettingPage 的 HDR 条目同源）；没有数据源就只能删按钮，否则是假按钮。

### 3.4 外壳：应用 / 窗口 / 导航（App / WindowBase / MainView）

##### 附 · 3.4 外壳：应用 / 窗口 / 导航（App / Program / AppConfig / Frameworks→Features / ViewHost）
#### `App.xaml`

> 上游对照：`src/Starward/App.xaml`（102 行）

- **第 58、63 行（ContentDialogBackground / FlyoutPresenterBackground 两个 Setter）**｜engine 引擎能力｜must
  - 上游：`{ThemeResource CustomAcrylicBrush}`（Windows 上 DWM 真亚克力，透出背后壁纸）
  - 移植：改成 `{ThemeResource LunarwardSurfaceBrush}`，即 `:88`（Light `#FFF3F3F3`）/ `:101`（Dark `#FF202020`）两枚**不透明** SolidColorBrush
  - 理由：Uno/Skia 只把 AcrylicBrush 实现成 `FallbackColor`（约 96% 不透明的深色平涂），弹窗底完全盖住壁纸且与内容层不协调；换成同色系不透明画刷至少保证可读。真模糊（壁纸截屏 + 高斯）属 M3，当前引擎无此能力。
- **第 67、68 行（MenuFlyoutPresenterBackground / MenuFlyoutSeparatorBackground）**｜engine 引擎能力｜reasonable
  - 上游：无这两行，菜单底走 Fluent 默认（亚克力 / 主题画刷）
  - 移植：`<StaticResource x:Key="MenuFlyoutPresenterBackground" ResourceKey="LunarwardSurfaceBrush"/>`、分隔线底色同样转发到该 key
  - 理由：与上一条同一个降级思路（无亚克力 → 不透明底），但用 StaticResource 转发而不是逐处改模板，换画刷只需改一处；未核实 Uno 下 Popup 是否会在主题切换后重新解析 ThemeResource（若会，则菜单在切换主题后颜色不跟随）。

#### `App.xaml.cs`

> 上游对照：`src/Starward/App.xaml.cs`（189 行）。上游的启动参数分派已迁到 `src/Starward/Program.cs:48`。

- **第 128-190 行（OnLaunched 里的单实例判定）**｜api 平台 API｜must
  - 上游：`AppInstance.FindOrRegisterForKey("Starward.Main")` + `RedirectActivationToAsync`，`AppInstance_Activated` 里用 `Shell32.CommandLineToArgvW` 解析 `starward-hyp-login://`；`:488` `FindInstanceForKey` 返回 `AppInstance?`；`:505` 退出时 `AppInstance.GetCurrent().UnregisterKey()`
  - 移植：文件锁（`~/.local/state/lunarward/lunarward.lock`，`FileShare.None`）+ `Features/App/SingleInstancePipe`（Unix socket）转发参数；`:488` 的 `FindInstanceForKey` 直接返回 `object? null`，`:505` 的 `UnregisterKey` 改成注释
  - 理由：`Microsoft.Windows.AppLifecycle.AppInstance` 在 Uno 上无实现（Windows 才有的 WinRT 激活重定向）。移植侧还补了上游没有的两件事：残留锁接管（`:139` `RemoveStaleSocket` + 重取锁，否则「启动没反应也没窗口」，L-72）与 `--new-instance` 跳过（调试器里多实例）。
- **第 108、110 行（首跑自动识别游戏的分支）**｜eng 工程性｜avoidable
  - 上游：无
  - 移植：`System.IO.File.AppendAllText("/tmp/lw-trace.txt", "C scan begin\n");` … `"D scan done\n"`
  - 理由：这是调试期的启动打点残留，**没有 `#if DEBUG`、没有环境变量开关**，发行版每次启动都会往 `/tmp/lw-trace.txt` 追加两行（违反硬规则 7；`/tmp` 是 tmpfs 还多一次无谓写）。功能上无害但应删。未核实是否是本轮合并时才带进来的。
- **第 97-121 行（首跑扫描 + 伴随启动注册）**｜linux 专属实现｜must
  - 上游：无（Windows 靠注册表判断已安装游戏；伴随启动由上游自己的机制处理）
  - 移植：`Features.Companion.CompanionAutoStart.Register(...)`；`OperatingSystem.IsWindows()` 为假时 `Task.Run(() => GameInstallScanner.ScanAndApply())`（放后台线程，B65），并按 `DevSwitches.Game` 直接选中游戏
  - 理由：Linux 没有注册表，已安装游戏只能扫目录（D25）；扫描是磁盘 IO，必须离开 UI 线程（L-127 同类问题）。
- **第 209-218 行（`--hide` 的落地）**｜linux 专属实现｜must
  - 上游：`--hide` 时只 `new SystemTrayWindow()`，不建主窗口
  - 移植：Linux 仍建主窗口并 `Activate()`，再走 `HideAfterShownAsync()`（`:264`）等 800ms 后隐藏；且先判 `Features.Tray.StatusNotifierTray.Available`，托盘不可用就**保持可见**
  - 理由：Linux 的托盘与全局热键都挂在主窗口上（托盘在 MainWindow 构造里起、热键在 MainView.Loaded 里装），只建托盘不建窗口会得到「进程在跑、没有任何入口」的隐形进程（L-75 / B12）。
- **第 285-329 行（HandleStartupAsync / ExpandUrlArgument）**｜eng 工程性｜avoidable
  - 上游：`src/Starward/Program.cs:48` 的 `HandleStartupAsync` 处理 `rpc` / `playtime` / `startgame` / `starward://` / `starward-hyp-login://`
  - 移植：同名方法留在 `App.xaml.cs`，**全仓没有任何调用点**（`grep` 只有定义）；其 `ExpandUrlArgument`（`:331`，`lunarward://startgame|playtime|test` 展开）同样无人调用
  - 理由：上游 0.19.0 把这段搬去 `Program.cs` 后，移植侧既没删旧副本也没在新入口接上，形成两份都不生效的实现；`args[0]` 还被无条件取下标（`:292` 起，`args.Length == 0` 会抛）。实际生效的只有 `Features/UrlProtocol/UrlProtocolService.cs:70`（也只在不可达的 `Program.cs:86` 被调用）与 `SingleInstancePipe` 的 `startgame` 转发。属于合并遗留，应删或接到 `Program.Linux.cs`。
- **第 413-448 行（EnsureSystemTray / StartTrayAsync）**｜linux 专属实现｜must
  - 上游：`m_SystemTrayWindow ??= new SystemTrayWindow();`（H.NotifyIcon）
  - 移植：Linux 走 `Features/Tray/StatusNotifierTray.StartAsync`（org.kde.StatusNotifierItem），`ProcessExit` 时 `Stop()` 摘注册；启动失败时轮询等主窗口出现，弹一次「没有托盘，关闭窗口会直接退出」的提示
  - 理由：`H.NotifyIcon` 在 Linux 是占位控件且会卡住主窗口初始化（12-shims 已登记）；不告知用户的话，「隐藏到托盘」等于把窗口关进笼子（C1 / L-62）。
- **第 517-528 行（ApplyAcrylicFallback）**｜eng 工程性｜avoidable
  - 上游：无
  - 移植：把 `CustomAcrylicBrush` / `CustomOverlayAcrylicBrush` 覆盖成两枚半透明 SolidColorBrush
  - 理由：**死代码**——全仓只有定义没有调用（`grep ApplyAcrylicFallback` 仅此一处），实际生效的是 `App.xaml` 的 `LunarwardSurfaceBrush`。留着会让人以为亚克力降级还有第二条路径；要么接上，要么删。

#### `AppConfig.Common.cs`

> 上游对照：`src/Starward/AppConfig.Common.cs`（64 行）

- **第 26-27 行（DeviceId 计算）**｜linux 专属实现｜must
  - 上游：`new(MD5.HashData(UTF8($"{Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\BIOS", "SystemBiosVersion", "")}{Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", "")}{Environment.MachineName}")))`（`:1` 还 `using Microsoft.Win32;`）
  - 移植：`new(MD5.HashData(UTF8($"{ReadFirstLine("/sys/class/dmi/id/bios_version")}{ReadMachineId()}{Environment.MachineName}")))`，`using Microsoft.Win32` 一并删掉
  - 理由：Linux 无注册表；对应物是 DMI 的 `bios_version` 与 `/etc/machine-id`（systemd 生成的稳定机器标识），三元组结构保持不变，上报给 MiHoYo 的 X-Sw-Device-Id 语义与 Windows 侧对齐。
- **第 66-96 行（新增 partial class AppConfig：ReadFirstLine / ReadMachineId）**｜linux 专属实现｜must
  - 上游：无（直接 `Registry.GetValue`）
  - 移植：`ReadFirstLine(path)` 用 `File.Exists` + `StreamReader.ReadLine()` + `Trim()`，异常吞掉返回空串；`ReadMachineId()` 先读 `/etc/machine-id`，为空再读 `/var/lib/dbus/machine-id`
  - 理由：`/etc/machine-id` 在部分镜像/容器里是空文件，`/var/lib/dbus/machine-id` 是传统位置，两个都试一次；读不到就只剩 `Environment.MachineName`（见下条）。
- **第 26 行（DeviceId 的兜底链）**｜eng 工程性｜reasonable
  - 上游：注册表读不到时自然退化成空串 + MachineName
  - 移植：只到 `/var/lib/dbus/machine-id` 为止，**没有**旧总账记载的「退回 `UserDataFolder/machine_id` 文件、再退 MachineName」那条额外持久化兜底
  - 理由：实测（本机）`/etc/machine-id` 存在，兜底链很少触发；但没有持久化兜底意味着 machine-id 缺失时 DeviceId 只由 bios_version + 主机名决定，主机名一改设备 ID 就变（影响多设备同步/登录态）。未核实上游 Windows 侧对「MachineGuid 缺失」是否也有额外兜底。

#### `AppConfig.Configuration.cs`

> 上游对照：`src/Starward/AppConfig.Configuration.cs`（304 行）。注：`CheckEnviroment()` → `CheckEnviromentAsync()`（`new NoPermissionWindow(folder).WaitAsync()` 取代 `User32.MessageBox`）是**上游 0.19.0 自己的改动**，不是移植偏离。

- **第 36、50-71 行（LinuxCacheHome / LinuxDataHome）**｜linux 专属实现｜must
  - 上游：`%LocalAppData%\Starward` / `Environment.SpecialFolder.LocalApplicationData`
  - 移植：`LinuxCacheHome` = `XDG_CACHE_HOME` 否则 `~/.cache`；`LinuxDataHome` = `XDG_DATA_HOME` 否则 `~/.local/share`；新增 `CacheDirectory` 属性（`:36`）
  - 理由：Linux 的 XDG 目录规范，缓存与数据分家（缓存可被 `--clear-cache` 安全删除）；`SpecialFolder.LocalApplicationData` 在 .NET/Linux 上映射到 `~/.local/share`，不能表达「缓存另放」。
- **第 124-139 行（CacheFolder 赋值与建目录）**｜linux 专属实现｜must
  - 上游：`CacheFolder = Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "Starward")`
  - 移植：`CacheFolder = Path.Combine(LinuxDataHome, "lunarward")`，随后 `CreateDirectory(CacheDirectory)`（`:132-139`）
  - 理由：目录名必须是 `lunarward`（与 `.desktop`、单实例锁、托盘名一致）；`CacheFolder` 名不副实（实际是数据目录），`App.xaml.cs:54`、`Helpers.Housekeeping` 都拿它当日志/清理根，改名会牵动一片，暂不动只加注释。
- **第 144、272、295-313 行（UserDataFolder 与配置的持久化后端）**｜linux 专属实现｜must
  - 上游：读注册表 `HKEY_CURRENT_USER\Software\Starward[.Debug]` 的 `UserDataFolder`；`LoadConfigurationFromStore` / `SaveConfigurationToStore` 用 `DatabaseService`（注册表 + 文件）
  - 移植：`userDataFolder = LinuxConfig.Get("UserDataFolder")`（`Helpers/LinuxConfig`，XDG config 下的 ini/json），`SaveConfigurationToStore` 用 `LinuxConfig.Set(...) + Save()`，且**不再做上游那段路径压缩**（`%LocalAppData%` → `LocalAppData`）
  - 理由：Linux 无注册表；也没有 `%LocalAppData%` 这种需要压缩的可移植路径语法，所以压缩逻辑一并去掉。
- **第 156-177 行（首次运行分支 + 缺 GameFileDatabase.SetDatabase）**｜eng 工程性｜avoidable
  - 上游：`GameFileDatabase.SetDatabase(userDataFolder)` + `DatabaseService.SetDatabase(userDataFolder)`，UserDataFolder 为空时弹 `WelcomeWindow`
  - 移植：两个分支（目录已存在 `:156-169`、首次运行 `:170-177`）都**只调了 `DatabaseService.SetDatabase`，删掉了 `GameFileDatabase.SetDatabase`**；首跑不再弹 WelcomeWindow，直接落到 `LinuxDataHome/lunarward`
  - 理由：可疑回归。`src/Starward.GameInstall/GameFileDatabase.cs:20-31` 在未初始化时直接 `throw new InvalidOperationException("Database is not initialized. Call SetDatabase first.")`，而移植侧全仓 `SetDatabase` 的另一个调用点只剩 `Features/ViewHost/WelcomeWindow.xaml.cs:261`（D22 起 WelcomeWindow 在 Linux 是死代码），使用方 `Features/GameSelector/LauncherConfigView.xaml.cs:283`、`:397` 仍会 `CreateConnection()`，入口在 `MainView.xaml.cs:94`（`ShowLauncherConfigView`）。是否真被点到未核实（需要启动器配置面板被打开），但这是「合并后少了一行上游代码」的典型形态。首跑不弹 WelcomeWindow 本身是有意（D22，移植改用自建目录）。
- **第 181-186 行（环境检查失败的处理）**｜linux 专属实现｜reasonable
  - 上游：`User32.MessageBoxW(...)` 提示 + `ClipboardHelper.SetText` 复制路径 + `Environment.Exit(0)`
  - 移植：`System.Diagnostics.Debug.WriteLine` 后 `Environment.Exit(0)`
  - 理由：`User32.MessageBoxW` 在 Uno/Linux 不存在。但注意：上游 0.19.0 已把提示换成跨平台 `NoPermissionWindow`（`:107`/`:166`），这条 MessageBox 只在「catch 到异常」时用；移植退化成 Debug 输出意味着**GUI 启动阶段异常退出时用户看不到任何原因**（只能去日志），可考虑复用 `NoPermissionWindow`。
- **第 204-229 行（SetLanguage）**｜linux 专属实现｜must
  - 上游：`CultureInfo.CurrentUICulture = culture;`，并把 `Lang.Culture` 设好（其余靠线程默认继承）
  - 移植：先算 `culture`（含 `Lang.Culture` 名称的反查），再统一设 `CurrentUICulture` / `DefaultThreadCurrentUICulture` / `CurrentCulture`，并显式 `Starward.Language.Lang.Culture = culture; Starward.Core.Localization.CoreLang.Culture = culture;`，最后 Serilog 记一行 UI language
  - 理由：Uno 上后台线程（托盘、单实例 socket、扫描）不继承 UI 线程的 culture，不设 `DefaultThreadCurrentUICulture` 就会出现「部分提示中文、部分英文」；`CoreLang` 是上游 0.19.0 新拆出来的第二套资源，必须同步设。
- **第 242 行（IsAdmin）**｜api 平台 API｜must
  - 上游：`WindowsIdentity.GetCurrent()` + `WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator)`
  - 移植：`Environment.IsPrivilegedProcess`
  - 理由：`WindowsIdentity` / `WindowsPrincipal` 只在 Windows 有效；`Environment.IsPrivilegedProcess` 是 .NET 跨平台属性，Linux 上等价于 euid==0。
- **第 95 行（IsAppInRemovableStorage / GameInstallEnv.CacheFolder）**｜cut 裁剪｜must
  - 上游：`IsAppInRemovableStorage = DriveHelper.IsDeviceRemovableOrOnUSB(AppContext.BaseDirectory);` 并在同一处 `GameInstallEnv.CacheFolder = CacheFolder;`
  - 移植：`IsAppInRemovableStorage = false`，并删掉 `GameInstallEnv.CacheFolder` 赋值
  - 理由：`DriveHelper` 依赖 Win32 卷/设备 API；「程序是否在可移动盘」对 Linux 的 AppImage/包管理器安装没有意义。`GameInstallEnv.CacheFolder` 的赋值移到 `Starward.GameInstall` 扩展/CLI 路径里（未核实是否还有等价设置点，若无则安装器会退到自己默认缓存目录）。

#### `AppConfig.ServiceProvider.cs`

> 上游对照：`src/Starward/AppConfig.ServiceProvider.cs`（222 行）

- **第 140-143 行（GameInstall 相关注册）**｜eng 工程性｜reasonable
  - 上游：`:93` 起逐个 `AddSingleton<HoYoPlayService>()` / `GameResourceService` / `GameConfigService` / `GenshinDispatchClient` / `GenshinHotUpdateService`，`:138` `AddSingleton<GameInstallService>()`
  - 移植：一行 `sc.AddStarwardGameInstall();`（扩展方法在 `src/Starward.GameInstall/GameInstallServiceCollectionExtensions.cs:9-20`，注册同一组 + `HttpService` / `DownloadService` / `GameInstallRunner`），`:143` 保留 `GameInstallService`
  - 理由：上游 0.19.0 把安装器抽成独立项目，移植侧直接复用它的注册入口，避免手工列表与上游漂移；显式注册的 `GameInstallService` 仍是移植侧 `Features/GameInstall/GameInstallService.cs` 的 facade。
- **第 144-145 行（ScreenCaptureService 不注册）**｜cut 裁剪｜must
  - 上游：`:146` `sc.AddSingleton<ScreenCaptureService>();`
  - 移植：整行注释掉，注明「依赖 Windows.Graphics.Capture + Win2D，M2 重写后再注册」
  - 理由：`Windows.Graphics.Capture` 与 Win2D 在 Uno/Linux 都没有实现；保留注册会让 DI 构造时就抛。代价是截图模块（含上游热键 44445）整体不可用，属明确的 M4 占位缺口。
- **第 149 行（CompanionRunner）**｜linux 专属实现｜must
  - 上游：无
  - 移植：`sc.AddSingleton<Starward.Features.Companion.CompanionRunner>();`
  - 理由：伴随启动（游戏起来后再拉起后启动程序）是 Linux 新模块（D24/D28），`App.xaml.cs:97` 的 `CompanionAutoStart.Register` 从容器取它。
- **第 151、153 行（GameRecordService / GameAuthLoginService）**｜linux 专属实现｜must
  - 上游：这两个类型都不存在（上游只有 `Features/GameRecord/GameRecordServiceBase.cs`；登录态靠 `PassportService`）
  - 移植：`AddSingleton<Starward.Features.GameRecord.GameRecordService>()`、`AddHttpClient<Starward.Features.GameLauncher.GameAuthLoginService>()`（注释称 0.19.0 合并时丢了注册）
  - 理由：Linux 侧的登录回填（WebKitGTK 抓 ticket）与战绩读取是独立实现（D76），必须自带注册；注释表明这是合并后补回来的一行，未核实是否还有其它同类注册在合并中丢失。
- **第 65 行（启动日志文案）**｜cosmetic 外观文案｜reasonable
  - 上游：`"Welcome to Starward v{AppVersion}"`
  - 移植：`"Welcome to Lunarward v{AppVersion}"`
  - 理由：产品名不同，日志是用户可见的品牌点。
- **第 187、189、210、212 行（User-Agent）**｜cosmetic 外观文案｜avoidable
  - 上游：`Starward.Debug/{AppVersion}`（Debug）/ `Starward/{AppVersion}`
  - 移植：**与上游逐字相同**，仍是 `Starward`，没有改成 `Lunarward`
  - 理由：旧总账 §3.4 记载「UA 改 Lunarward」已失效——实况没改。是否该改取决于 MiHoYo 服务端是否校验 UA（改错会被风控/被判定为第三方客户端）；在核实前保持与上游一致是安全的，但这也说明「品牌改名」在 HTTP 层没有做全（`X-Sw-App-Version` 仍是 Starward 版本号，见 `:214-217`）。

#### `Features/Passport/WindowsDeviceFpService.cs`

- **整文件（387 行差异；上游同名文件约 60 行）**｜linux 专属实现｜must 必需
  - 上游：设备指纹取自 `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`、WMI/`SystemInformation` 的机型信息与 `DisplayInformation` 的屏幕尺寸。
  - 移植：改用 `/etc/machine-id`、`/sys/class/dmi/id/*`、`/proc/cpuinfo`、`/sys/class/drm`、`/proc/meminfo`，`GetScreenSize()` 返回 `unknown`；类名与公开成员沿用上游（`WindowsDeviceFpService`），调用方无需分支。
  - 理由：Linux 没有这些注册表/WMI 来源，只能就地重算；影响面仅限「设备指纹上报」，账号与会话不受影响。D130 已登记。

#### `Features/PageBase.cs`

> 上游对照：`src/Starward/Features/PageBase.cs`（80 行，旧路径 `src/Starward/Frameworks/PageBase.cs`）

- **第 33-40 行（PageEx_Loaded 里快照 ContentGameId）**｜engine 引擎能力｜must
  - 上游：`PageEx_Loaded` 只调 `OnLoaded()`，没有游戏 ID 快照
  - 移植：加载时先 `ContentGameId = CurrentGameId?.Id;` 再 `OnLoaded()`，并新增 `protected string? ContentGameId;`（带 `LUNARWARD-DEVIATION: D94` 注释）
  - 理由：Uno 的 `Frame` 缓存页面（`NavigationCacheMode=Enabled`）在同一个实例上反复导航，页面里的 `CurrentGameId` 在切换游戏后已变成「新游戏」，页面自己却还是旧游戏的内容；快照一个只读的 `ContentGameId` 才能判断「我这个实例画的是哪个游戏」。
- **第 63-80 行（OnNavigatedTo 里的 OnGameChanged 管道）**｜engine 引擎能力｜must
  - 上游：`OnNavigatedTo` 只处理 `e.Parameter` 与 `OnNavigatedTo()` 虚方法
  - 移植：记录上次 gameId，导航结束时若变化则调 `protected virtual void OnGameChanged()`（`:73-80`，空体，注释说明「缓存页面导航到另一个游戏时调用」）
  - 理由：缓存页面不会重新 `Loaded`，`OnLoaded` 里的一次性初始化不会重跑；没有这个钩子，切游戏后所有缓存页都停在旧数据上（D94）。**注意**：旧总账记的「移植侧不再解除 Loaded/Unloaded 订阅」已失效——`:43-48` 仍是 `Loaded -= PageEx_Loaded; Unloaded -= PageEx_Unloaded;`，与上游一致。

#### `Features/ViewHost/MainView.xaml`

> 上游对照：`src/Starward/Features/ViewHost/MainView.xaml`（384 行）

- **第 356-369 行（新增 FooterMenuItems：运行环境入口）**｜linux 专属实现｜must
  - 上游：`NavigationView.PaneFooter` 里只有「设置」齿轮；主菜单六项全在当前游戏域（按 `SupportedPages` 显隐）
  - 移植：footer 里、齿轮上方新增 `NavigationViewItem_RuntimeEnv`（Tag=`RuntimeEnvironmentSetting`，Glyph `&#xE7F8;`）
  - 理由：运行环境（wine/Proton/前缀）是**跨游戏**的（一个前缀可被多个游戏共用、一个游戏绑一个方案），放进游戏域菜单会随游戏切走而被隐藏；放 footer 与设置同级，D80 起它是一级页。
- **第 359、361、365 行（图标与文案绑定）**｜cosmetic 外观文案｜reasonable
  - 上游：无对应控件
  - 移植：`FontIcon Glyph="&#xE7F8;"` + `ToolTipService.ToolTip="{x:Bind lang:Lang.Lunarward_RuntimeEnv_Title}"`，内容用 `<Border Height="40"><TextBlock .../></Border>` 包一层
  - 理由：Uno 下 `SymbolIcon` 的枚举覆盖不全（`Lunarward_` 前缀的 key 是移植自建文案，需走 `x:Bind` 到 `Lang`），`Border Height=40` 是为与上游其它菜单项的 40px 行高对齐、避免 footer 项比主菜单项矮一截。

#### `Features/ViewHost/MainView.xaml.cs`

> 上游对照：`src/Starward/Features/ViewHost/MainView.xaml.cs`（615 行）

- **第 104-108 行（CurrentPageType / CurrentPageContent）**｜linux 专属实现｜must
  - 上游：无
  - 移植：`public Type? CurrentPageType => MainView_Frame?.SourcePageType;`、`public object? CurrentPageContent => MainView_Frame?.Content;`
  - 理由：L1.5 进程内 UI 自检（`--selftest-ui`）要断言「现在确实停在某页」。Uno/Skia 不发布 AT-SPI 树（L-36），外部工具查不到页面，只能在进程内读 Frame 状态。
- **第 121-135 行（TryCloseGameSelector，Esc 第一优先级）**｜bugfix 修上游缺陷｜reasonable
  - 上游：无（Esc 直接隐藏到托盘）
  - 移植：先 `VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot)` 关掉打开的 Popup/选择层，返回 `true` 时调用方（`MainWindow.xaml.cs:311-315`）不再隐藏窗口
  - 理由：修的是上游同一个 Esc 键承担两件事的老问题——用户只想关选择层，窗口却整个消失到托盘。属移植主动改的行为，须与「以上游为准」权衡。
- **第 152-160 行（ShowHotkeyUnavailableHintOnce）**｜linux 专属实现｜must
  - 上游：全局热键由 `RegisterHotKey` 提供，失败无提示
  - 移植：`Helpers.X11KeyState.Available` 为假（Wayland 原生会话）时，用 `OneShotHints.TryTake("hotkey-wayland")` 只提示一次，并给出替代做法（系统快捷键绑 `lunarward --show`）
  - 理由：Wayland 下拿不到全局键盘状态（L-45），热键静默失效比报错更糟；一次性提示避免每次启动都弹。
- **第 166-256 行（DevSwitches 页面直达 + 调试下载/安装对话框）**｜eng 工程性｜avoidable
  - 上游：无
  - 移植：`DevSwitches.Page` / `SubPage` 的 switch 直达 13 个页面（含 `runtimeenv`、`installprogress`、`predownload`、`install`），其中 `installprogress` 会在启动后真的发起一次 45 秒预下载到 `~/games/lw-ui-progress`
  - 理由：GUI 自动化在 Uno 上不可靠（无 a11y 树），开发期只能靠环境变量直达页面。但体积很大（一个 `MainView_Loaded` 里塞了近百行调试分支），`installprogress` 会真的写盘下载；仅有部分包在 `#if DEBUG`，`--page/--subpage/--game` 的开关本身（`DevSwitches`）在 Release 依然存在，违反硬规则 6 的精神。
- **第 285-292 行（自动识别游戏的提示）**｜linux 专属实现｜must
  - 上游：无（Windows 靠注册表，用户不需要知道「扫到了几个游戏」）
  - 移植：`!OperatingSystem.IsWindows()` 时读 `GameInstallScanner.KnownGames`，`OneShotHints.TryTake("auto-scan")` 只提示一次（D86），文案硬编码中文
  - 理由：Linux 靠扫目录发现游戏，用户需要知道「我装的游戏被认出来了没」；一次性提示是 D86 的决定（原来每次启动都弹）。文案未进 `Lang` 资源（12-shims 已登记）。
- **第 344、510-525 行（切游戏后的页面刷新与全局页白名单）**｜cut 裁剪｜must
  - 上游：`HideLauncherConfigView(); UpdateDragableArea();`；`NavigateTo` 里只有 `SettingPage` 在白名单外
  - 移植：改成 `WeakReferenceMessenger.Default.Send(new CurrentGameChangedMessage(CurrentGameId));`（D62）；白名单加 `RuntimeEnvironmentSetting`（`:513`），`SelectedItem` 也要跟着设（`:523`）
  - 理由：`LauncherConfigView` 的显隐与 `UpdateDragableArea`（拖拽区，依赖 `SetDragRectangles`）在 Uno 上都没有对应实现，只能删；页面缓存导致切游戏后内容不刷新，改成广播消息让当前页自己刷（`PageBase` 的 `OnGameChanged` 是接收方）。运行环境页若不进白名单会被 `SupportedPages` 过滤后静默换回启动器页（L-152）。
- **第 598-604 行（更新后不弹「更新内容」窗）**｜cut 裁剪｜must
  - 上游：版本变化时 `new UpdateWindow().Activate()`
  - 移植：`!OperatingSystem.IsWindows()` 时只把 `LastAppVersion` 记为当前版本并跳过窗口
  - 理由：Linux 走包管理器/AppImage 更新（D5），没有「发布页更新内容」可展示，弹出来只有标题和空白字段。
- **第 663-688 行（InitializePassportUser 被系统代理检测逻辑覆盖）**｜eng 工程性｜avoidable
  - 上游：`CurrentPassportUser = null; PassportUsers = null; UpdateHypFlyoutLayout();` 然后 `if (CurrentLauncherConfig?.CanLogin ?? false)` 就初始化 `PassportUsers` 与设备指纹
  - 移植：方法体变成 `await Task.Delay(1500); Uri? proxy = HttpClient.DefaultProxy.GetProxy(...); if (proxy is not null && Helpers.OneShotHints.TryTake("system-proxy")) { ...初始化 PassportUsers... }`
  - 理由：疑似编辑事故——上半段（清空 + `CanLogin` 判断）被删，Passport 初始化被塞进「有系统代理」分支，于是**没配代理的用户永远看不到 Hyp 账号列表**，配了代理的用户也只有第一次启动才有（`OneShotHints` 的 key 只取一次）。同时 `CheckSystemProxy`（`:309-321`）仍是每次启动都弹提示，旧总账记的「加了一次性提示」实际落在了错误的方法里（D86 的意图没实现）。运行时影响未核实（需要登录态与浏览器环境），但两处结构明显错位。

#### `Features/ViewHost/MainWindow.xaml.cs`

> 上游对照：`src/Starward/Features/ViewHost/MainWindow.xaml.cs`（259 行）

- **第 118-141 行（InitializeMainWindow：标题栏策略）**｜linux 专属实现｜must
  - 上游：`Title = "Starward"`、`AppWindow.TitleBar.ExtendsContentIntoTitleBar = true`、`SetDragRectangles(new RectInt32(0, 0, 100000, (int)(48 * UIScale)))`、`SetIcon()`、`WTSRegisterSessionNotification(WindowHandle, 0)`
  - 移植：`Title = "Lunarward"`、`ExtendsContentIntoTitleBar = false`，删掉 `SetDragRectangles` 与 `WTSRegisterSessionNotification`
  - 理由：Uno 的 X11 后端没实现 `TitleBar.SetDragRectangles`（`WindowBase.SetDragRectangles` 只能是空实现），扩展内容区后就是「窗口拖不动 + 自绘最小化/关闭按钮点不动」；改成由窗口管理器画真标题栏，拖拽/最小化/关闭全部免费且可靠。session 通知是 `wtsapi32.dll` 专有。
- **第 34、397-417 行（OnWindowActivated 取代 WindowSubclassProc）**｜api 平台 API｜must
  - 上游：`WindowSubclassProc` 处理 `WM_ACTIVATE`/`WM_POINTERACTIVATE`（激活时间）、`WM_SYSCOMMAND 0xF030`（挡双击最大化）、`WM_WTSSESSION_CHANGE`（锁屏暂停视频背景）、`WM_DEVICECHANGE`（U 盘插拔）、`WM_HOTKEY` 44444/44445（悬浮窗/截图）
  - 移植：用 Uno 的 `Window.Activated` 只保留「激活时间 → `MainWindowStateChangedMessage{Activate=true, LastActivatedTime=...}`」这一条，其余全部去掉
  - 理由：Win32 消息钩子在 Linux 没有对应物。去掉的部分都有替代或明确不做：双击最大化由合成器处理；锁屏/会话切换理论上可接 `org.freedesktop.login1`（注释写「M2 接入」，目前没接，视频背景在锁屏后仍继续解码）；U 盘插拔的 `RemovableStorageDeviceChangedMessage` 没了，安装路径检测不再自动刷新（未核实是否影响「移动硬盘上的游戏」场景）。
- **第 36-37、107-116、143-162 行（OnFirstActivated / OnIconActivatedOnce）**｜api 平台 API｜must
  - 上游：构造时 `CenterInScreen(1200, 676)` 直接按 DPI 定尺寸，图标由 `SetIcon()` 从 exe 资源取
  - 移植：`:143-162` 首次激活时按 `Content.XamlRoot.RasterizationScale` 重新 `AppWindow.Resize(1200*scale, 676*scale)`；`:107-116` 首次激活后延迟 300ms 调 `Helpers.AppIcon.Apply(Title)` 补 `_NET_WM_ICON`
  - 理由：Uno 里 `XamlRoot.RasterizationScale` 要到窗口激活后才有值，构造时算尺寸会得到 1.0（L-35，HiDPI 下窗口只有一半大）；X11 后端不设 `_NET_WM_ICON`，不补的话任务栏/Alt-Tab 里是通用方块。
- **第 38-56 行（`--selftest-ui` 的 Activate 兜底定时器）**｜linux 专属实现｜must
  - 上游：无
  - 移植：`LUNARWARD_SELFTEST=1` 时挂 `OnSelfTestActivated`，并用 `DispatcherQueue.CreateTimer()` 8 秒后无条件跑一次 `UiSelfTest.RunAsync`，跑完 `Environment.Exit(failed == 0 ? 0 : 1)`
  - 理由：CI 的 `xvfb-run` 没有窗口管理器，`Activated` 永远不来，自检会挂到超时（L-185）；有终态的批处理模式才能进 CI。
- **第 58、164-210 行（RunDebugWindowAction）**｜eng 工程性｜avoidable
  - 上游：无
  - 移植：读 `DevSwitches.WindowAction`，延迟 3 秒后可 `minimize` / `close` / 打开 `gamesetting` 对话框 / 打开 `backgroundview` 窗口
  - 理由：无人值守验证窗口动作（Wayland 合成点击不可靠，L-36）。属开发入口，须留在 `DevSwitches`（硬规则 6）；`close` 分支会真的触发 `AppWindow_Closing` 的关闭策略，调试时容易把进程关掉。
- **第 213-230 行（Show / ShowByGamepad）**｜api 平台 API｜must
  - 上游：`ShowByGamepad` 里 `User32.SetCursorPos(AppWindow.Position.X + Width/2, ...)` 把光标挪到窗口中心
  - 移植：删掉 `SetCursorPos`（注释「Linux 无 SetCursorPos，光标交给合成器」），只保留 `CenterInScreen(1200, 676)` + `base.Show()`
  - 理由：`User32.SetCursorPos` 在 Wayland 下无对应协议（合成器不允许客户端移动全局光标），X11 下虽可 `XWarpPointer` 但 Uno 层没有暴露；手柄唤起时窗口会盖住光标位置，属可接受的行为差异。
- **第 249-262 行（AppWindow_Closing：托盘不可用则不拦关闭）**｜bugfix 修上游缺陷｜must
  - 上游：函数第一行就是 `args.Cancel = true;`，然后按 `CloseWindowOption` 分支
  - 移植：先判 `option is Hide && !Features.Tray.StatusNotifierTray.Available` → 直接 `return`（不取消关闭，进程随之退出），之后才 `args.Cancel = true`
  - 理由：上游假设「隐藏到托盘」一定有托盘；Linux 上有些会话（纯净 Wayland 合成器、未装 StatusNotifierWatcher）没有托盘宿主，照上游逻辑隐藏后只能靠 `lunarward --show` 捞回来。这是移植补的上游缺陷。
- **第 307-318 行（Content_KeyDown 的 Esc）**｜bugfix 修上游缺陷｜reasonable
  - 上游：`if (e.Key is VirtualKey.Escape) { Hide(); }`
  - 移植：先 `MainContentHost?.TryCloseGameSelector()`，收掉了 Popup 就 `e.Handled = true` 并返回，没东西可收才 `Hide()`
  - 理由：同 `MainView.xaml.cs:121-135`，Esc 一键两用导致「想关选择层，窗口却消失了」。属主动行为改动，与「以上游为准」有张力，须在决策文档留痕。

#### `Features/ViewHost/SystemTrayWindow.xaml`

> 上游对照：`src/Starward/Features/ViewHost/SystemTrayWindow.xaml`（53 行，逐字对比只差 1 处）

- **第 13 行（Closed 事件处理器名）**｜cosmetic 外观文案｜must
  - 上游：`Closed="WindowEx_Closed"`
  - 移植：`Closed="WindowBase_Closed"`
  - 理由：上游 0.19.0 把基类 `WindowEx` 改名 `WindowBase`，XAML 里的事件名必须跟着改，否则编译失败；纯改名，无行为差异。**注意**：该窗口在 Linux 上不会被创建（`App.xaml.cs:209` 只在 `OperatingSystem.IsWindows()` 分支 new 它），属「保留但不可达」的上游文件。
- **第 11 行（Title）**｜cosmetic 外观文案｜avoidable
  - 上游：`Title="StarwardTrayMenu"`
  - 移植：未改，仍是 `Title="StarwardTrayMenu"`
  - 理由：与 `App.xaml.cs`/`MainWindow` 把品牌改成 Lunarward 的做法不一致；因为窗口不可达，用户看不到，影响为零，属遗漏而非设计。

#### `Features/ViewHost/SystemTrayWindow.xaml.cs`

> 上游对照：`src/Starward/Features/ViewHost/SystemTrayWindow.xaml.cs`（134 行）

- **第 51-56 行（去掉标题栏边框与 DWM 圆角）**｜api 平台 API｜must
  - 上游：`User32.GetWindowLongPtr/SetWindowLong` 清掉 `WS_CAPTION`、`WS_BORDER`，再 `DwmApi.DwmSetWindowAttribute(DWMWA_WINDOW_CORNER_PREFERENCE, DWMWCP_ROUND)` 设圆角
  - 移植：整段删成一行注释，靠 `WindowBase` 的无边框窗口 + 合成器圆角
  - 理由：`WS_*` 样式位与 `DwmSetWindowAttribute` 都是 Win32/DWM 概念，Uno/Linux 没有窗口样式位可改；圆角由合成器（KWin/Mutter 的 decoration 协议）负责。代价：托盘菜单在部分合成器上会带阴影/边框（未核实具体表现）。
- **第 64-66 行（托盘图标）**｜api 平台 API｜must
  - 上游：`Kernel32.GetModuleHandle(null)` + `User32.LoadIcon(hInstance, "#32512")` → `System.Drawing.Icon.FromHandle` 赋给 `trayIcon.Icon`
  - 移植：删掉图标加载，只设 `trayIcon.ToolTipText = "Lunarward";`
  - 理由：`System.Drawing.Icon.FromHandle` 依赖 GDI+ 的 Win32 图标句柄；Linux 托盘走 `org.kde.StatusNotifierItem`，图标由 `IconPixmap`/桌面主题提供（M2），`H.NotifyIcon` 这条路径整体不可用。
- **第 76 行（激活状态判断）**｜eng 工程性｜must 必需
  - 上游：`if (args.WindowActivationState is WindowActivationState.Deactivated)`
  - 移植：`if (args.WindowActivationState.ToString() == "Deactivated")`
  - 理由：**不是可避免的**——2026-11-25 实测：还原成 `is WindowActivationState.Deactivated` 后，本地 Debug/Release 构建通过，但 CI 的 `dotnet publish -r linux-x64 --self-contained` 报 `error CS0266: Cannot implicitly convert type 'Microsoft.UI.Xaml.WindowActivationState' to 'Windows.UI.Core.CoreWindowActivationState'`。同一个属性在不同 target/projection 下解析成两套同名枚举，字符串比较是唯一两边都成立写法（D134）。
- **第 88-101 行（托盘菜单定位）**｜api 平台 API｜must
  - 上游：`User32.GetCursorPos` + `User32.CalculatePopupWindowPosition(point, size, TPM_RIGHTALIGN|TPM_BOTTOMALIGN|TPM_WORKAREA, ...)` + `User32.MoveWindow`
  - 移植：`RootGrid.Measure(...)` 算尺寸后，用 `DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea` 把窗口贴到**工作区右上角**
  - 理由：`CalculatePopupWindowPosition` 是 Win32 弹出菜单定位（含工作区裁剪），Linux 下菜单该出现在托盘图标旁边，坐标只能由 StatusNotifierItem 的 `Activate(x, y)` 给出；M2 换协议前退化为右上角，多显示器时可能弹在主屏而非托盘所在屏（未核实）。

#### `Features/ViewHost/WelcomeWindow.xaml.cs`

> 上游对照：`src/Starward/Features/ViewHost/WelcomeWindow.xaml.cs`（274 行，逐行几乎一致）

- **第 120 行（DEBUG 下的默认 UserDataFolder）**｜eng 工程性｜reasonable
  - 上游：`#if DEBUG` → `%LocalAppData%\Starward`；`#else` → `MyDocuments\Starward`
  - 移植：`#if DEBUG` → `%LocalAppData%\lunarward`（目录名改品牌）；`#else` 分支未改，仍是 `MyDocuments\Starward`（`:122`）
  - 理由：与 `AppConfig.Configuration.cs` 的 `LinuxDataHome/lunarward` 保持一致。但 Release 分支没跟着改，一旦这个窗口重新启用，Release 下会建出 `~/Documents/Starward`，与真正的数据目录 `~/.local/share/lunarward` 并存（旧总账没有记这条）。**整体前提**：D22 起 Linux 首跑不再弹 WelcomeWindow（`AppConfig.Configuration.cs:170-177`），本文件在 Linux 上是死代码——`GameFileDatabase.SetDatabase` 的唯一残留调用点（`:261-262`）也因此不可达。
- **第 46 行（SetDragRectangles 调用写法）**｜cosmetic 外观文案｜must 必需
  - 上游：`SetDragRectangles(new RectInt32(0, 0, 100000, (int)(48 * UIScale)))`
  - 移植：`SetDragRectangles(new RectInt32 { X = 0, Y = 0, Width = 100000, Height = (int)(48 * UIScale) })`
  - 理由：2026-11-25 实测：4 参构造器只在 Windows 目标下存在，CI 的 `dotnet publish -r linux-x64` 报 `error CS1729: RectInt32 does not contain a constructor that takes 4 arguments`；对象初始化器两边都能编（D134）。原判据：纯语法替换（构造器 → 对象初始化器），行为完全相同；大概是 Uno 下 `RectInt32` 构造器重载解析有问题时的临时改法，属纯格式差异，可还原。

#### `Features/WindowBase.Linux.cs`

> 上游对照：`src/Starward/Features/WindowBase.cs`（216 行，旧路径 `src/Starward/Frameworks/WindowEx.cs`，类名 `WindowEx` → `WindowBase`，namespace `Starward.Features`）。上游**没有** `ShowFromMinimized`、`ClampToScreen`、`DefaultWindowSizeInDips`、`FindAppWindow`。

- **第 13、15 行（WindowHandle / UIScale）**｜api 平台 API｜must
  - 上游：`WindowHandle = (IntPtr)AppWindow.Id.Value`（Win32 下就是 HWND）；`UIScale = User32.GetDpiForWindow(WindowHandle) / 96d`
  - 移植：`WindowHandle => IntPtr.Zero`；`UIScale => Content?.XamlRoot?.RasterizationScale ?? 1d`
  - 理由：Linux 没有 HWND，`AppWindow.Id` 在 Uno 上恒为 `0x1`（不是可用的 X11 window id）；缩放只能取 `XamlRoot.RasterizationScale`。所有下游拿 `WindowHandle` 当 HWND 用的代码（子类化、`MoveWindow`、`SetWindowLong`）因此全部需要改写或退化为空实现。
- **第 19-23、197-227 行（构造与 XamlRoot→AppWindow 登记表）**｜api 平台 API｜must
  - 上游：构造函数里两次 `ComCtl32.SetWindowSubclass`（`windowSubclassProc` / `inputSiteSubclassProc`），并保留 `WindowSubclassProc` / `InputSiteSubclassProc` 两个虚方法给子类重写
  - 移植：删掉子类化，改成 `Register(this)`（静态 `WeakReference` 表，XamlRoot → AppWindow）与 `AppWindow.Closing += (_, _) => { };`；新增 `internal static AppWindow? FindAppWindow(XamlRoot?)`（找不到返回 null，不猜主窗口）
  - 理由：`SetWindowSubclass` 是 comctl32 专有；Uno 的事件模型里子类要改用 `Window.Activated` 等（见 `MainWindow.xaml.cs:34`）。`FindAppWindow` 用来在多窗口场景（托盘菜单、欢迎页、对话框）反查某一个 `XamlRoot` 对应的 `AppWindow`——上游靠 `WindowHandle` 就够了，Linux 没有这个锚点。构造函数里那个空 `Closing` 订阅看不出用途（未核实，可能是为了强制 Uno 生成关闭路径）。
- **第 25-59 行（Show）**｜api 平台 API｜must
  - 上游：`AppWindow.Show(true)` + `MoveInZOrderAtTop()` + `ShowWindow(SW_SHOWNORMAL)` + `SetForegroundWindow`
  - 移植：先 `AppWindow.TitleBar.ExtendsContentIntoTitleBar = false`（try/catch 记 debug），再 `Helpers.X11Window.Show(Title)`（libxcb：unmap/map + 置顶 + 抢焦点）；失败回退 `AppWindow.Show()` + `Activate()`；随后后台 `ClampToScreenInBackground()`，并在显示后发 `MainWindowStateChangedMessage{Activate=true}`
  - 理由：`Window.Hide()` 在 Uno 原生是**空实现**（Uno 的 X11 后端不 unmap），所以显隐必须自己用 libxcb 按窗口标题找 X 窗口再操作；`ShowWindow`/`SetForegroundWindow` 也不存在。抢焦点用 EWMH `_NET_ACTIVE_WINDOW`，这条路径对没有窗口管理器的会话（xvfb）会失败并回退（L-183 一带的实测记录）。
- **第 75-113 行（DefaultWindowSizeInDips + ClampToScreenAsync）**｜linux 专属实现｜must
  - 上游：无（上游把窗口尺寸钉在 `CenterInScreen(1200, 676)`，从不检查是否超出屏幕）
  - 移植：`DefaultWindowSizeInDips` 虚属性（默认 null）→ `Delay(350)` 等窗口 map → `X11Window.TryGetScreenSize` → 按 `size × UIScale` Resize → `Delay(120)` → `AppIcon.Apply` → 若窗口超过屏幕 90%（下限 400×300）则再 Resize 并 `X11Window.SetBounds` 居中
  - 理由：Uno/X11 首次 map 时窗口尺寸经常是错的（HiDPI 与 clamp 到 640×480 两种都见过，L-35），而 `DisplayArea` 在 Uno 上拿不到真实的每屏工作区，只能直接问 X server。延时是为了等 Uno 完成首次布局，属平台限制下的实测妥协。
- **第 116-146 行（Hide / Minimize / ShowFromMinimized）**｜api 平台 API｜must
  - 上游：`Hide() = AppWindow.Hide()`；`Minimize() = ShowWindow(WindowHandle, SW_MINIMIZE)`；无 `ShowFromMinimized`
  - 移植：`Hide()` 走 `Helpers.X11Window.Hide(Title)`，失败回退 `AppWindow.Hide()`；`Minimize()` 用 `OverlappedPresenter.Minimize()`（注释说明 `Restore()` 无效）；`:136-146` 新增 `ShowFromMinimized() → Show()`（注释称上游有，实际**上游没有**，全仓也无调用者）
  - 理由：`AppWindow.Hide()` 在 Uno 是空实现（AGENTS.md 8 节已列），必须 libxcb；`OverlappedPresenter.Minimize()` 是 Uno 唯一可用的最小化。`ShowFromMinimized` 是死代码（方法存在但没人调），旧总账把它当成上游 API 迁移，属误记。
- **第 148-159 行（CenterInScreen）**｜api 平台 API｜reasonable
  - 上游：`DisplayArea.GetFromWindowId(MainWindowId, DisplayAreaFallback.Nearest)` 算工作区后 `MoveAndResize` 到正中
  - 移植：只做 `Resize(width*UIScale, height*UIScale)` + `MoveAndResize` 到**当前位置**，真正居中交给 `ClampToScreenAsync` 的 X11 分支
  - 理由：Uno 的 `DisplayArea` 返回值在 X11 下不可靠（多屏/缩放场景常返回主屏），居中改由 X server 的屏幕尺寸推。副作用：非主屏、非 X11（Wayland 原生）时窗口可能不居中。
- **第 166-195 行（SetIcon / SetDragRectangles / AdaptTitleBarButtonColor / 主题判据）**｜api 平台 API｜must
  - 上游：`SetIcon()` 用 `LoadIcon(hInstance, "#32512")` 或 `AppWindow.SetIcon(path)`；`SetDragRectangles` 写 `AppWindow.TitleBar.SetDragRectangles`；`AdaptTitleBarButtonColorToActuallTheme` 改标题栏按钮颜色；`ShouldAppsUseDarkMode`/`ShouldSystemUseDarkMode` 是 `uxtheme.dll` 的 `#132`/`#138`
  - 移植：`SetIcon` → `Helpers.AppIcon.Apply(Title)`；`SetDragRectangles`、`AdaptTitleBarButtonColorToAcuallTheme` 空实现；两个 `Should*` → `Helpers.SystemThemePreference.IsDark`（注释引 L-183：以前恒 `true`）
  - 理由：`uxtheme.dll` 与 `TitleBar.*` 的颜色/拖拽区 API 在 Uno/X11 都没有；图标只能自己发 `_NET_WM_ICON`；深色判据改读桌面主题（`gsettings`/portal），否则「跟随系统」永远是深色。空实现的代价：`SetDragRectangles` 空 → 自绘标题栏不可用（连带 `MainWindow` 改成不扩展内容区）。
- **第 184 行（ChangeAccentColor）**｜engine 引擎能力｜reasonable
  - 上游：翻转 `FrameworkElement.RequestedTheme`（Dark↔Light）再设回 `Default`，强制 Uno/WinUI 重新求值一遍 `ThemeResource`，从而让强调色改变后所有依赖强调色的控件刷新
  - 移植：空实现 `{ }`
  - 理由：Linux 上强调色改的是 `ColorPaletteOverride`/`Application.Resources`，但没有「翻转主题强制重算」这个 WinUI 私有 trick（Uno 上没有 `RequestedTheme` 的重入刷新保证，未核实）。后果：换壁纸导致的强调色变化要等重启/切页才生效，`AccentColorChangedMessage` 的接收方 `MainView.OnAccentColorChanged` 只重新应用了本地资源。

#### `Program.Linux.cs`

> 上游对照：上游无此文件。上游入口是 `src/Starward/Program.cs`（`#if DISABLE_XAML_GENERATED_MAIN` 包着的 WinUI `Application.Start` 入口）。移植侧用 `LunarwardProgram.Main` 取代它，文件 1623 行，绝大部分是 Linux 专属 CLI。

- **第 10、1581-1597 行（GUI 入口）**｜linux 专属实现｜must
  - 上游：`WinRT.ComWrappersSupport.InitializeComWrappers()` + `AppConfig.CheckEnviroment()` + `Microsoft.UI.Xaml.Application.Start(...)`
  - 移植：`UnoPlatformHostBuilder.Create().App(() => new Starward.App()).UseX11().UseLinuxFrameBuffer().UseWin32().Build(); host.Run();`，并在 `host.Run()` 之前显式 `InitEnviroment()`（`:89` 的局部函数 = `AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult()`）
  - 理由：Uno 的启动器不是 `Application.Start`。`InitEnviroment()` 这一步是 0.19.0 合并后**补回来**的：`CacheFolder`/`UserDataFolder`/config 都在 `CheckEnviromentAsync` 里初始化，漏掉它 `App.OnLaunched` 会看到 `UserDataFolder == null` 去弹欢迎向导，之后拼日志目录就是 null（源码注释已写明「合并后漏掉了这次调用」）。
- **第 50-72 行（--version / --help / 未知开关）**｜linux 专属实现｜must
  - 上游：无（Windows 版没有 CLI）
  - 移植：`--version`/`-v` 打印 `Lunarward x.y.z`；`--help`/`-h` 走 `Helpers/CliSpec.cs` 的 `RenderHelp`（唯一帮助来源，`lint-cli-help.sh` 卡住「源码开关 ⊆ 这张表」）；`CliSpec.TryRejectUnknown` 对首 token 是未知 `-` 开关时 exit 2 并给候选（D79）
  - 理由：无 DISPLAY 环境下也要能 `--version`/`--help`（这两个分支在环境检查之前 return，不需要 X）；位置参数留给游戏自己的参数，不能一律拦。
- **第 704-763、1478-1543 行（安装任务与前缀/组件运维子命令）**｜linux 专属实现｜must
  - 上游：无
  - 移植：`--install-task <gameKey> <目录> [--predownload] [--run-seconds N]` 走 `Starward.GameInstall.GameInstallRunner`（与 UI 同一路径）；另有 `--prefix-ensure`、`--graphics-install/verify/remove`、`--graphics-list`、`--component-scan/list/health/import/download`、`--preflight`、`--prefix-template`
  - 理由：Linux 的运行环境（wine/Proton/DXVK/前缀）没有 GUI 就绪前的可诊断入口，脚本与 CI 必须能只读地拿到「前缀建在哪、装了哪个 DXVK」。0.19.0 把自写的 Sophon 管线整体删掉（D128），安装/更新改由上游 `GameInstallRunner` 承担，CLI 只剩一层 facade。
- **第 98-109 行（--webkit-login 登录助手）**｜linux 专属实现｜must
  - 上游：登录窗走 WebView2
  - 移植：`--webkit-login` 起独立进程 + WebKitGTK 6.0（`Features/GameRecord/WebKitLoginHost`），缺失时提示「Arch 上装 webkitgtk-6.0」并 exit 1
  - 理由：Uno/Linux 没有 WebView2；登录回填要把 cookie/ticket 抓回主进程，只能做成独立子进程（D76）。注意它同样在 DISPLAY 检查之前，属于「需要图形会话但没有窗口」的例外。
- **第 1539-1580 行（GUI 开关白名单 + 无 DISPLAY 退出码 3）**｜linux 专属实现｜must
  - 上游：无
  - 移植：首参数以 `-` 开头但不在 `{--hide,--show,--new-instance,--selftest-ui,--page,--subpage,--game,--scroll,--viewer,--profile-editor,--window-action}` 里 → 打印「未知命令或不完整」并 exit 1；`DISPLAY` 与 `WAYLAND_DISPLAY` 都为空 → 打印人话并 `exit 3`
  - 理由：Uno 的 X11 后端在没有 DISPLAY 时会在 `HostBuilder.Build()` 里炸（比 `App.OnLaunched` 更早），报错信息无法理解（L-72）；而「参数像命令却没分支接住」若不拦，会变成「命令没反应反而弹窗」。`DevSwitches` 的调试开关必须放行，否则 `lunarward --subpage RuntimeEnvironmentSetting` 会被当成未知命令（2026-11-14 回归）。
- **第 18-48 行（Main 入口与未处理异常落盘）**｜linux 专属实现｜must
  - 上游：`Program.CurrentDomain_UnhandledException`（`src/Starward/Program.cs:112`）把崩溃写进 `AppConfig.LogFile`
  - 移植：在 `Main` 最前面注册 `AppDomain.CurrentDomain.UnhandledException`，自己拼时间戳写 `AppConfig.LogFile` 并回落到 `Console.Error`（B44）
  - 理由：上游那个处理器整块被 `#if DISABLE_XAML_GENERATED_MAIN` 包着（Windows 才编译），Uno 上等于没有；后台线程（扫描、socket、托盘）崩溃时没有它就只有一句 `Process terminated`。
- **第 18-72 行（没有任何 `rpc` / `playtime` / `startgame` 子命令分支）**｜eng 工程性｜avoidable
  - 上游：`src/Starward/Program.cs:56-82` 处理 `rpc`（`RpcRunner.Run`）、`playtime`（`LogPlayTimeAsync(biz, pid)`）、`startgame`（`GameLauncherService.StartGameAsync`），未知则以 GUI 启动
  - 移植：这三个词不在任何分支里；`playtime --biz ... --pid ...` 由 `Features/CloudGame/CloudGameButton.xaml.cs:143` 真实拉起，落到这里会被当位置参数放行 → 走 GUI/单实例流程
  - 理由：疑似合并后失联。`App.xaml.cs:285` 那份 `HandleStartupAsync` 有这三个分支但**无人调用**，`Program.cs` 那份被 `#if` 排除；结果是 `playtime` 子进程要么被单实例转发（`SingleInstancePipe` 只认 `startgame`）后丢弃，要么直接开一个 GUI 窗口。游玩时长是否真的丢记录未核实（需要跑一次真实游戏或手动调 CLI），但这条链路在源码层面是断的。

#### `Program.cs`

> 上游对照：`src/Starward/Program.cs`（139 行）。**整份文件都在 `#if DISABLE_XAML_GENERATED_MAIN` 里，移植侧没有定义该常量**（`Lunarward.csproj` 无 `DefineConstants`、无 `StartupObject`；入口是 `Program.Linux.cs` 的 `LunarwardProgram.Main`），所以本文件编译结果为空——**无实质偏离**。

- **第 117 行（崩溃日志目录）**｜eng 工程性｜avoidable
  - 上游：`Path.Combine(Environment.GetFolderPath(SpecialFolder.LocalApplicationData), "Starward", "log")`
  - 移植：`Path.Combine(..., Starward.Helpers.DataFolder.AppFolderName, "log")`
  - 理由：这行**永远不会执行**（文件被 `#if` 排除）。旧总账把它记成「Program.cs 只改日志目录名」，属于把死代码当生效改动；真正的崩溃日志目录由 `App.xaml.cs:54` 的同一处替换决定。要么删掉这份上游副本，要么把 `DISABLE_XAML_GENERATED_MAIN` 的定义与入口关系理清，否则它是「改错地方」的典型陷阱。
- **第 48-107 行（HandleStartupAsync）**｜cut 裁剪｜must
  - 上游：`rpc` / `playtime` / `startgame` / `starward://`（`UrlProtocolService.HandleUrlProtocolAsync`）/ `starward-hyp-login://`（`AppInstance` 重定向）五个分支
  - 移植：整段随文件一起被排除，`rpc` 项目（`src/Starward.RPC`）也已删除
  - 理由：`starward://` 协议在 Linux 换成 `lunarward://` + `.desktop`（`Features/UrlProtocol/UrlProtocolService.Linux.cs`），`starward-hyp-login://` 的 `AppInstance` 重定向不存在。但除 `rpc`（M4 明确不做）之外，其余四个分支在移植侧都**没有生效的替代实现**（见 `Program.Linux.cs` 最后一条与 `App.xaml.cs:285` 那条），属真正的功能缺口而非有意的裁剪。

### 3.5 截图与图库（Screenshot）

##### 附 · 3.5 截图与图库（Screenshot / Codec）
#### `Features/Screenshot/ScreenshotPage.xaml`

> 上游对照：`src/Starward/Features/Screenshot/ScreenshotPage.xaml`（274 行）；移植 120 行。

- **第 1-2 行（根元素 `sf:PageBase` 的 `NavigationCacheMode`）**｜eng 工程性｜reasonable
  - 上游：无该属性（默认 `Disabled`，每次导航重建页面、`OnLoaded` 里 `await Task.Delay(16); Initialize();`）
  - 移植：`NavigationCacheMode="Required"`，页面常驻，靠 `ScreenshotPage.Linux.cs:167-172` 的 `OnLoaded` override 重新枚举目录
  - 理由：D60 的全仓页面缓存约定（切页不闪、不重解析 XAML）。语义差异已被 `OnLoaded` 重载抵消，但缓存页在**导航回来时不会再走构造器**，所以任何只写在构造器里的状态（`ScreenshotPage.Linux.cs:17-34` 里的空态显隐与文案）不会重算——目前靠 `LoadImages()` 里的 `HasImages` 赋值 + XAML 绑定绕开，属可接受。
- **第 54-98 行（图库容器：`ScrollViewer` + `ItemsControl` + `tk:WrapPanel`）**｜engine 引擎能力｜must
  - 上游：`GridView_Images`（`:49-252`）+ `GroupStyle`（`:60-86`，`HeaderTemplate` 绑 `ScreenshotItemGroup.Header`，即日期分组头）+ `ItemsWrapGrid`（`:93-100`，`AreStickyGroupHeadersEnabled="True"`，`ItemWidth=218/ItemHeight=128`）+ `ItemContainerStyle`（`:87-92`）
  - 移植：`ItemsControl` + `CommunityToolkit.WinUI.Controls.WrapPanel`（`HorizontalSpacing/VerticalSpacing=12`），扁平列表，无分组
  - 理由：Uno 下「分组 GridView + ItemsWrapGrid」不渲染任何条目（L-48），只能换扁平网格。代价是**日期分组头与粘性头整体消失**（上游 `:188-190` 按 `TimeMonthDay` 分组），条目排序也失去「按日期分块」的视觉线索。
- **第 82-87 行（条目左上角 `CheckBox`）**｜linux 专属实现｜reasonable
  - 上游：条目里没有勾选框，多选靠 `GridView.SelectionMode` 切换（上游 `.xaml.cs:89-96` 的 `MutliSelect` setter 在 `Multiple`/`None` 之间切），选中态由 `GridViewItem` 容器画
  - 移植：条目模板内固定放一个 `CheckBox`，`Visibility` 绑 `CheckBoxVisibility`（`ScreenshotPage.Linux.cs:62`，仅多选模式 `Visible`），`IsChecked` 双向绑 `ScreenshotItem.IsSelected`
  - 理由：没有 `GridView` 容器就借不到容器选中态，只能自己拿复选框表达「这条被勾了」；旧总账记的「常驻 CheckBox（B06）」已修掉，现在只在多选模式出现。
- **第 51-53 行注释 / 整块缺失的右键菜单（上游 `:121-213` 的 `Grid.ContextFlyout`）**｜cut 裁剪｜avoidable
  - 上游：条目上挂 7 项右键菜单——`MenuFlyoutItem_ScreenshotInfo`（`:130-140`，`FileInfo` 文本，`Loading` 时算像素尺寸）、`Common_Open`（`:143-150`）、`CopyFile`（`:152-159`）、`CopyAsJPG`（`:161-168`）、`OpenInExplorer`（`:171-178`）、`OpenWithDefault`（`:180-187`）、`OpenWith`（`:189-196`）、`Delete`（`:199-211`，红色前景），handler 在上游 `.xaml.cs:445-690`
  - 移植：**一项都没有**；`ScreenshotPage.Linux.cs:335-338` 只留一个空的 `MenuFlyoutItem_ScreenshotInfo_Loading`，`ScreenshotPage.LinuxAuto.cs:14-22` 里 7 个 `MenuFlyoutItem_*_Click` 全为空方法且 XAML 里没有对应绑定（纯死代码）
  - 理由：注释写「悬停菜单保持上游观感」，实际是整块没做。逐项都可在 Linux 落地：`xdg-open`（打开/默认应用）、`File.Delete`（删除，配合确认框）、剪贴板写文件（复制文件）、`ScreenshotConverter.ConvertToJpeg`（复制为 JPG）。属**功能回退**而非平台限制，应优先补「打开/在文件夹中显示/删除」三项。
- **第 68-81 行（条目外观：卡片底色、悬停放大、手型光标）**｜cosmetic 外观文案｜reasonable
  - 上游：条目根 `Grid` 只有 `sh:PointerCursor.CursorShape="Hand"`（`:104`）、`ui:UIElementExtensions.ClipToBounds`（`:105`）；缩略图 `CachedImage` 自带 `Background="{ThemeResource ControlAltFillColorTertiaryBrush}"`（`:112`）、`RenderTransformOrigin` + `ScaleTransform`（`:114-119`）、`ui:VisualExtensions.NormalizedCenterPoint="0.5"`（`:111`），`PointerEntered/Exited` 触发 `GridZoomInStoryboard`（1.15 / 0.3s）/`GridZoomOutStoryboard`（1.0 / 0.6s）（`:214-248`）；容器是 `GridViewItem`（`CornerRadius=4`、`Margin=0,0,12,12`，`:87-92`）
  - 移植：条目换成 `Border`（`Width=218`、`Padding=6`、`CornerRadius=6`、`Background=CardBackgroundFillColorDefaultBrush`，`:68-73`），缩略图区是内层 `Grid`（`Height=116`、同底色、`CornerRadius=4`，`:75-77`），`CachedImage` 只剩 `Width/Height/Stretch/Source`（`:78-81`）；手型光标、`ClipToBounds`、`NormalizedCenterPoint`、两个 Storyboard 与 `mxi:EventTriggerBehavior` 全部没有
  - 理由：换成 `ItemsControl` 后容器模板不再由 `GridViewItem` 提供，卡片化是最省事的等价观感（也是全仓其他列表的既有做法）。代价是悬停放大 1.15 的「呼吸感」和手型光标没了，图片圆角外溢也没裁（`ClipToBounds` 缺省 false）。补这两项不需要平台能力，属观感欠账。
- **第 78-81 行（`CachedImage` 少了 `IsThumbnail="True"`）**｜eng 工程性｜avoidable
  - 上游：`:113` `IsThumbnail="True"`（上游 CachedImage 走缩略图通道，400px 目标宽）
  - 移植：没设 `IsThumbnail`，于是走 `Controls/CachedImage.Linux.cs:252-298` 的 `ReadFileAsync` 普通分支；缩略图分支在 `:255`（`IsThumbnail` 为真时才 `DecodeScaledPng(path, DecodePixelWidth > 0 ? DecodePixelWidth : 400)`，`:268`）
  - 理由：游戏截图动辄 2560×1440，一屏 30 张就要把 30 张原图整段读进内存再解码（`DecodeScaledPng` 用 `SKCodec` 按目标宽度解码，正是为这个场景写的），这是**纯粹的漏标属性**，改一行即可。旧总账没有这条，是 0.19.0 之后仍存在的性能偏离。附带记：`Background` 从 `CachedImage` 挪到外层 `Grid`（`:75-77`），因为移植 `CachedImage : Image` 的 `Background` DP 只存值不绘制（`CachedImage.Linux.cs:21-32`，L-183），占位底色实际由内层 `Grid` 提供——视觉上等价，但**图片底部若透明或解码失败**时内层 `Grid` 的底色会比上游多铺一层。
- **第 89-92 行（条目下方新增文件名 `TextBlock`）**｜cosmetic 外观文案｜reasonable
  - 上游：条目模板里没有文件名文本（`:101-250`），文件名只出现在右键菜单的 `FileInfo` 项（`:130-140`，打开菜单时才填充）
  - 移植：`TextBlock FontSize=12 MaxLines=1 TextTrimming=CharacterEllipsis Text="{x:Bind Name}"`
  - 理由：右键菜单整块没移植（见上一条），文件名只能常显，否则用户分不清同一天的截图。等菜单补上后这条可留可去（留则条目更高、每行更少图），属设计取舍。
- **第 59-65 行（没有 `CanDragItems` / `DragItemsStarting`）**｜cut 裁剪｜reasonable
  - 上游：`:53-54` `CanDragItems="{x:Bind s:AppConfig.IsAdmin, Converter={StaticResource BoolReversedConverter}}"` + `DragItemsStarting="GridView_Images_DragItemsStarting"`（上游 `.xaml.cs:395-418`：非管理员时把文件塞进 `DataPackage`，可直接拖到资源管理器/别的应用）
  - 移植：无；`ScreenshotPage.Linux.cs:239-242` 只留空方法 `GridView_Images_DragItemsStarting`（XAML 未绑定，死代码）
  - 理由：拖出进程需要 `StorageItems`/`DataPackage`，Uno 在 X11/Wayland 下没有实现（未核实 Uno 7 是否已补齐 `DragStarting` 的 URI 载荷），属引擎能力缺口。用户仍可通过右键菜单「打开文件夹」拿到文件，故记 reasonable 而非 must。

#### `Features/Screenshot/ScreenshotPage.xaml.cs`

> 上游对照：`src/Starward/Features/Screenshot/ScreenshotPage.xaml.cs`（693 行）；移植 695 行。**整文件 `<Compile Remove>`，不参与构建**（`Lunarward.csproj:129`），生效实现在 `ScreenshotPage.Linux.cs`。

- **第 221-228 行（`Button_ChangeFolder_Click` 里的默认目录）**｜linux 专属实现｜reasonable
  - 上游：`:219-223` `string? folder = AppConfig.ScreenshotFolder; if (!Directory.Exists(folder))`
  - 移植：`string? folder = ScreenCaptureLinux.DefaultFolder; if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))`，注释写「与真实落盘位置一致」
  - 理由：Linux 的落盘目录由 `ScreenCapture.Linux.cs:22-45` 决定（`AppConfig.ScreenshotFolder` 为空时退 `~/Pictures/Lunarward`），这里必须跟着它，否则「更改文件夹」按钮会指向上游默认的 `<UserData>/Screenshots`。0.19.0 把上游同文件的外部目录参数改成 `CurrentGameId`（`:146`/`:364`）——移植这份也同步了，但两处都不进构建，真正生效的是 `ScreenshotPage.Linux.cs:220-236` 的 `xdg-open`。
- **第 1-693 行（整文件不参与构建）**｜eng 工程性｜must
  - 上游：该文件是截图页的全部逻辑（`Initialize` 三类目录 `:127-190`、对话框 `:352-373`、`ImageBatchConvertWindow` `:337-349`、7 个菜单 handler `:445-690`、`StorageFile.DeleteAsync` `:660-690`、`FileSystemWatcher` `:246-333`）
  - 移植：`<Compile Remove>`，改为 `ScreenshotPage.Linux.cs`（345 行）重写；本文件仅作为「上游改动跟踪样本」留在树里
  - 理由：文件直接依赖 `Windows.Storage.StorageFile`、`ImageViewWindow2`、`this.XamlRoot.ContentIslandEnvironment.AppWindowId`、`CollectionViewSource.IsSourceGrouped`，在 Uno 下编不过，必须排除。副作用是**「上游这份文件里还有什么没搬」很难发现**：本次核对下来，未搬的实质功能是外部截图目录（`GetExternalScreenshotFolder`）、文件夹管理对话框、批量转换窗口、右键菜单 7 项。

#### `Features/Screenshot/ScreenshotPage.Linux.cs`

> 上游对照：`src/Starward/Features/Screenshot/ScreenshotPage.xaml.cs`（693 行）。移植 345 行，是本页唯一生效实现。

- **第 26-34 行（空态文案写死中文、不设 `Image_Emoji`）**｜cosmetic 外观文案｜reasonable
  - 上游：XAML 用 `Lang.ScreenshotPage_ScreenshotFolderDoesNotExist`（`ScreenshotPage.xaml:44`），`OnNavigatedTo`（上游 `.xaml.cs:44-55`）按 `CurrentGameBiz` 给 `Image_Emoji.Source` 换成派蒙/帕姆/AI/邦布表情
  - 移植：构造器里 `FindName("StackPanel_Emoji")` 后把第二个子元素（`TextBlock`）的 `Text` 直接改成 `$"还没有截图。按 Alt+D 可以截取当前屏幕，图片会保存到 {ScreenCaptureLinux.DefaultFolder}。"`，`Image_Emoji.Source` 从不赋值
  - 理由：Linux 版没有「当前游戏」的概念（默认目录与游戏无关），表情图也就无从选择；文案里带上真实落盘路径对 Linux 用户更有用（`~/Pictures/Lunarward` 不是显而易见的默认位置）。但**硬编码中文绕过了 `Lang` 资源**，英文界面下会出现中英混排，属该进 `Lang` 的欠账（12-shims 已登记同类问题）。
- **第 95-130 行（`LoadImages` 只枚举单一默认目录，排序与分组也换了）**｜cut 裁剪｜avoidable
  - 上游：`:127-190` 三类目录——`GetGameScreenshotPathAsync()` 得到的默认截图目录（`:127-137`，标记 `Default`）+ 游戏安装目录内的 `ScreenShot` / `StarRail_Data\ScreenShots`（`:138-147`，标记 `InGame`）+ `AppConfig.GetExternalScreenshotFolder(CurrentGameId)` 分号分隔的外部目录（`:149-166`）；按文件名去重（`:174-185`）、`OrderByDescending(x => x.CreationTime)`（`:188`）、`GroupBy(x => x.TimeMonthDay)` 生成 `ScreenshotItemGroup`（`:189-190`）
  - 移植：只读 `ScreenCaptureLinux.DefaultFolder` 一层、`Directory.EnumerateFiles`（不递归）、`OrderByDescending(x => x.LastWriteTime)`、`new ScreenshotItem(x.FullName)` 平铺
  - 理由：外部目录管理本身**完全可移植**（`AppConfig.GetExternalScreenshotFolder` 是纯配置读写，`ScreenshotFolderManageDialog` 也已编译通过），丢的是用户已经在设置里配过的目录；游戏内目录依赖「安装路径 → 截图子目录」映射（上游靠 `GameLauncherService.GetGameExeName` + HoYoPlay 配置），可留到 M4。排序用 `LastWriteTime` 而不是 `CreationTime` 是次要差异，但和 `ScreenshotItem` 的命名解析（见 `ScreenCapture.Linux.cs` 第 4 条）叠加后，新截的图有时会插到列表中间。
- **第 167-208 行（`OnLoaded` 全量重建 + `FileSystemWatcher` 全量重载）**｜eng 工程性｜reasonable
  - 上游：`OnLoaded` 先 `await Task.Delay(16)` 再 `Initialize()`（`:59-63`）；`OnUnloaded`（`:66-86`）逐项 `Dispose` watcher 并把 `_folders`/`_screenshotDict`/`_screenshotItems`/`ScreenshotGroups`/`ScreenshotViewSource.Source` 全置空；watcher 用 `ScreenshotHelper.WatcherFilters` 的 7 个扩展名建过滤器（`:246-258`），只在 `Created`/`Deleted` 上做事，`Created` 里先 `WaitForFileReleaseAsync` 再**增量** `Insert(0, item)` 到分组（`:261-300`）
  - 移植：`OnLoaded` 里 `LoadImages(); EnsureWatcher();`，没有 `OnUnloaded` override（watcher 只在目录变化时重建，`_watcher?.Dispose()` 仅出现在 `EnsureWatcher` 开头 `:184`）；watcher 的 `NotifyFilter = FileName | LastWrite | Size`，`Created/Deleted/Renamed/Changed` 四个事件统一 `DispatcherQueue.TryEnqueue(LoadImages)`（`:196-200`），每次全量重枚举
  - 理由：扁平列表每次重建 30 个 `ScreenshotItem` 并重挂 `PropertyChanged`（`:115-124`）代价可接受，代码也短得多。两个真问题：(1) **`Changed` 也触发全量重载**，截图工具写盘过程中会连打好几次（无 `WaitForFileRelease`），列表抖动；(2) 没有 `OnUnloaded`，页面被回收后 watcher 仍挂着并继续往 `DispatcherQueue` 排 `LoadImages`（目前页面是 `NavigationCacheMode=Required` 不会真卸载，所以只在极端情况暴露）。
- **第 220-236 行（`ManageScreenshotFolderCommand` = `xdg-open` 目录）**｜cut 裁剪｜avoidable
  - 上游：`:352-373` `ManageScreenshotFolderAsync` 弹 `ScreenshotFolderManageDialog { Folders = _folders, CurrentGameId, XamlRoot }`，`FolderChanged` 后 `AppConfig.SetExternalScreenshotFolder(CurrentGameId, ...)` + `Initialize()` 重扫；对话框本身支持逐目录「打开 / 移除 / 备份到游戏目录」（`ScreenshotFolderManageDialog.xaml:62-101`）
  - 移植：`RelayCommand(OpenScreenshotFolder)` → `Directory.CreateDirectory(folder)` + `Process.Start("xdg-open", folder)`；`ScreenshotFolderManageDialog` **全仓零实例化点**（grep 只剩它自己的构造器），成了编译通过但用户永远看不到的孤儿对话框
  - 理由：只读单目录的前提下没有「管理」可言，弹空对话框更糟。但这也意味着**用户配过的外部截图目录在 Linux 版永远不生效**，且对话框维护成本（158 行 XAML + 一个 `CanRemove`/`InGame` 模板）已经白付。要么补回「添加外部目录」的最小入口，要么把对话框一起删掉。
- **第 239-242、261-267 行（两个 XAML 里已无绑定的死事件处理器）**｜eng 工程性｜avoidable
  - 上游：`GridView_Images_DragItemsStarting`（`.xaml.cs:395-418`）与 `GridView_Images_ItemClick`（`:376-384`）都由 `ScreenshotPage.xaml:54/56` 绑定
  - 移植：两个方法还在，但 XAML 里没有 `DragItemsStarting`/`ItemClick` 绑定（拖拽走 `Item_Tapped`，点击走 `Item_Tapped`），方法体一个是 `// LINT-ALLOW` 注释、一个是 `OpenItem(e.ClickedItem)`，永不被调用
  - 理由：遗留自「照抄上游契约让 XAML 编过」的阶段，现在既没绑定也没有行为，留着容易让人误以为拖拽/点击还有别的入口（`GridView_Images_ItemClick` 若被误绑会与 `Item_Tapped` 双开查看器）。可整段删。
- **第 271-284 行（`Item_Tapped` 替代 `ItemClick`）**｜linux 专属实现｜reasonable
  - 上游：`GridView` 的 `ItemClick`（`:55-56` 绑定，`:376-384` 处理），多选模式下点条目由 `GridViewItem` 切选中态
  - 移植：条目 `Border` 上 `Tapped="Item_Tapped"`（XAML `:70`），多选时 `item.IsSelected = !item.IsSelected`，非多选时 `OpenItem(item)`
  - 理由：`ItemsControl` 没有 `ItemClick`（B07），只能条目根元素自己接 `Tapped`；行为与上游等价（多选点一下勾/取消，非多选点一下开图）。注意 `e.Handled = true`（`:277`）是为了不让 `Border` 内的 `CheckBox` 双击判定串到打开查看器。
- **第 287-301 行（`OpenItem` → `ScreenshotViewerWindow`）**｜cut 裁剪｜reasonable
  - 上游：`:376-384` `new ImageViewWindow2().ShowWindowAsync(this.XamlRoot.ContentIslandEnvironment.AppWindowId, item, _screenshotItems)`（Win2D 画布查看器：缩放/旋转/拖拽/键盘翻页/另存/复制/全屏，上游 `ImageViewWindow2.xaml.cs` 1400+ 行）
  - 移植：`new ScreenshotViewerWindow(items, index).Activate()`（D31/M3 的 SkiaSharp 查看器，`Features/Screenshot/ScreenshotViewerWindow.xaml.cs`），只有「上一张 / 下一张 / 打开文件夹 / 关闭 + 预览图 + 状态行」；异常时退 `xdg-open`（`:296-300`）
  - 理由：`ImageViewWindow2` 走 Win2D + `ContentIsland`，Uno 无对应实现，必须换实现（不是换写法）。查看器本身是移植侧新增文件不计偏离，但**缩放/旋转/全屏/另存这几项能力是真丢失**，属 M3 未完成而非设计选择。
- **第 308-321 行（`BatchConvertAsync`：语义与格式设置都变了）**｜cut 裁剪｜avoidable
  - 上游：`:337-349` `var list = MutliSelect ? GridView_Images.SelectedItems.Cast<ScreenshotItem>().ToList() : []; new ImageBatchConvertWindow().Activate(list);`——非多选模式传**空列表**，由转换窗口自己选文件；窗口里可选输出格式（JPG/PNG/AVIF/JXL）、画质、是否删原图、重命名（上游 `ImageBatchConvertWindow.xaml.cs`）
  - 移植：`targets = MutliSelect ? items.Where(x => x.IsSelected).ToList() : items;`——**非多选模式 = 整库转换**；`quality = AppConfig.ScreenCaptureEncodeQuality switch { 0 => 80, 1 => 90, _ => 100 }`；`await Task.Run(() => ScreenshotConverter.ConvertToJpeg(...))` 后 toast「成功 X，跳过 Y，失败 Z」再 `LoadImages()`
  - 理由：没有转换窗口，只能就地转 JPG，语义必须重新定义。但「没开多选、点一下批量转换 = 把图库所有图各写一份 .jpg」和上游「先选文件」相反，属于**破坏性默认值**（`ScreenshotConverter.ConvertToJpeg` 是 `Path.ChangeExtension` 同名输出，原图不动，但会在图库里凭空多出一倍文件）。建议非多选模式禁用该按钮或先弹目录选择。另：转换质量借用了「截图编码质量」这个设置（原本是给采集用的三段式 80/90/100），混杂了两个语义。

#### `Features/Screenshot/ScreenCapture.Linux.cs`

> 上游对照：`src/Starward/Features/Screenshot/ScreenCaptureService.cs`（555 行，Windows.Graphics.Capture + Win2D 进程内抓帧）。移植 185 行，是**新增的 Linux 专属文件**，本目录无同名上游文件。

- **第 48-114 行（`CaptureAsync` 的三条外部工具 + portal 兜底）**｜engine 引擎能力｜must
  - 上游：`Windows.Graphics.Capture` 抓窗口帧（`ScreenCaptureService.cs:118-130` `CaptureAndProceedImageAsync`，`GraphicsCaptureItem` + Direct3D11 帧），进程内编码
  - 移植：候选数组 `("spectacle", ["-b","-n","-f","-o", file])` / `("grim", [file])` / `("gnome-screenshot", ["-f", file])`（`:63-68`），逐个 `TryRunAsync` 并校验 `File.Exists && Length > 0`（`:71`），失败 `TryDelete`（`:82`）；三条都不行再 `Helpers.Portal.ScreenshotAsync(interactive: false, ...)`（`:88`）并 `File.Move` 到截图目录（`:93`，搬不动就直接返回 portal 原件路径 `:102`）
  - 理由：Linux 无 `Windows.Graphics.Capture` 等价物，进程内抓帧要么绑 X11/XWayland（还要处理 DMA-BUF、HDR、合成器私有协议），要么走 portal；外部工具 + portal 是 D31 的既定路线，也是唯一「不改上游 XAML 就能出图」的路。旧总账记的「spectacle/grim/gnome-screenshot + portal」与本轮代码一致，未漂移。**未核实**：`File.Move` 后 portal 原件是否已被删除（KDE 的 portal 截图通常落在 `~/Pictures` 之外的临时目录，这里只搬不删，可能残留）。
- **第 48-114 行（整屏捕获，没有「抓的是哪个窗口」的判定）**｜engine 引擎能力｜must
  - 上游：`CaptureInternal`（`:71-114`）先 `RunningGameService.GetLatestActiveGame()`，**没有运行中的游戏就直接返回**（`:73-77`），窗口最小化也拒抓（`:78-85`），只抓该游戏窗口的客户区；HDR 时按显示器高级色彩信息选 `R16G16B16A16Float`（`:126-128`），并可选另存 SDR（`:99-103`）
  - 移植：不查游戏、不判断最小化，`spectacle -f` / `grim` / `gnome-screenshot -f` 抓的是**整块屏幕**；也没有 HDR / 色彩管理分支（恒 8bit SDR PNG）
  - 理由：外部工具的全屏模式拿不到「窗口归属」；要按窗口抓得用 portal 的 `parent_window` 或 `grim -g <geometry>`，两者都要先解析游戏窗口（移植侧 `Helpers/X11Window` 已有窗口枚举能力，但未接）。旧总账未单列这条，属**语义级缩小**：热键在任何时候（哪怕没开游戏）都会截一张桌面图。**未核实** portal 路径是否传 `parent_window`（若传了，沙盒里的行为会比外部工具更接近上游）。
- **第 22-45 行（`DefaultFolder` 的目录规则）**｜linux 专属实现｜reasonable
  - 上游：`Path.Join(AppConfig.UserDataFolder, "Screenshots", runningGame.Process.ProcessName)`（`ScreenCaptureService.cs:305`），按游戏进程名分子目录
  - 移植：优先 `AppConfig.ScreenshotFolder`（try/catch 吞异常，`:27-33`），否则 `Environment.SpecialFolder.MyPictures`/`~/Pictures` + `"Lunarward"`（`:38-43`）
  - 理由：`~/Pictures` 是 Linux 桌面环境里图片的约定位置，比藏在 `~/.local/share/lunarward/Screenshots` 更容易被文件管理器和图片工具发现；不按进程名分子目录是因为捕获不看游戏（上一条）。注释提到的 L-62（设置里改目录不生效）已修：`Features/Setting/ScreenshotSetting.Linux.cs` 写 `AppConfig.ScreenshotFolder` 后这里能读到。
- **第 60 行（文件名 `Lunarward_yyyy-MM-dd_HH-mm-ss.png`）**｜cut 裁剪｜avoidable
  - 上游：`$"{runningGame.Process.ProcessName}_{captureItem.FrameTime:yyyyMMdd_HHmmssff}.{extension}"`（`:316`，如 `GenshinImpact_20261125_12345678.png`），并写 XMP `CreatorTool=Starward Launcher` + `CreateDate`（`:318`/`:378-384`）
  - 移植：`$"Lunarward_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png"`，无 XMP、无颜色配置
  - 理由：格式本身无害，但它**命不中 `ScreenshotItem.TryParseCreationTime`**（`Features/Screenshot/ScreenshotItem.cs:52-95` 只认 `yyyyMMdd_HHmmssff`、`yyyyMMddHHmmss`、`StarRail_Image_<unix>`、`yyyy-MM-dd-HH-mm-ss`、`GenshinlmpactPhoto ...`、nvidia、xbox；移植这套是全短横线，不是本文件的「日期_时间」混排），于是每条新截图都退到 `FileInfo.CreationTime`（`ScreenshotItem.cs:39-45`），`TimeMonthDay` 与排序全交给文件系统时间。改成 `Lunarward_{DateTime.Now:yyyyMMdd_HHmmssff}.png` 即可命中第一条，**无任何平台障碍**，纯属没对齐。**未核实**：ext4/btrfs 上 .NET 的 `File.CreationTime` 是否稳定拿到 statx btime（拿到则影响仅剩命名风格）。
- **第 53-68 行（忽略「保存格式 / 编码质量 / HDR→SDR」三个设置）**｜cut 裁剪｜avoidable
  - 上游：扩展名按 `(captureItem.HDR, AppConfig.ScreenCaptureSavedFormat)` 选 png/avif/jxl（`:309-315`），质量按 `AppConfig.ScreenCaptureEncodeQuality` 映射（avif 80/90/100，jxl distance 2/1/0，`:334-358`），HDR 可选另存 SDR（`:398-416`，原神还读游戏内 HDR 亮度）；`ImageSaver` 写 ICC/XMP
  - 移植：恒 PNG、无质量参数、无色彩配置文件；而 `Features/Setting/ScreenshotSetting.Linux.cs:19-58` 仍在给用户展示「格式（PNG/AVIF/JXL）+ 质量」下拉框，且 `ScreenshotQualityVisibility` 会在选了 AVIF/JXL 时把质量下拉显示出来
  - 理由：采集端不产生 avif/jxl，设置项就成了**无声失效的开关**（用户选 AVIF 仍得到 PNG）。AVIF/JXL 编码在移植侧有现成通道（`Helpers/ImageDecode.cs` 已接 Magick.NET，`ScreenshotConverter` 用 SkiaSharp 出 JPEG），缺的是采集后转码这一步。属功能缺口而非平台限制。
- **第 73-79、104-110 行（反馈：桌面通知 + 调用方 toast 双份）**｜cosmetic 外观文案｜reasonable
  - 上游：`ScreenCaptureInfoWindow` 覆盖层——先 `CaptureStart` 显示抓到的缩略图，保存成功后 `CaptureSuccess(hwnd, bitmap, filePath, maxCLL)` 就地显示「已保存 + 缩略图」并可点开查看器（`ScreenCaptureService.cs:86-104`），失败 `CaptureError`
  - 移植：`Helpers.DesktopNotification.Show("截图已保存", 文件名, "lunarward", 4000)`（`:78`/`:100`/`:108`）；同时调用方各自再弹一次 toast——`Helpers/HotkeyManager.Linux.cs:273-295`（成功「截图已保存：{file}」/失败「没有可用的截图工具」）与 `Features/Setting/ScreenshotSetting.Linux.cs:138-149`（「已保存到 {file}」）
  - 理由：没有覆盖层窗口，通知是唯一能在「游戏全屏盖住窗口」时确认截到的反馈（C4）。但**热键一次截图会同时出现桌面通知和应用内 toast**，两者文案还不一致；正确做法是 `notify` 由调用方统一控制（热键走通知、设置页测试走 toast），现在两条路径都是双份。缩略图预览能力整体缺失（通知里只有文件名）。
- **第 118-136 行（剪贴板：按设置写图片文件）**｜api 平台 API｜must
  - 上游：`CopyToClipboardAsync`（`:420-430`）在 `AppConfig.AutoCopyScreenshotToClipboard` 为真时 `ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, file)`（Windows 剪贴板，放的是**文件对象**）
  - 移植：同样读该设置，改为 `Task.Run(() => Helpers.ClipboardHelper.SetImageFile(file))`（`wl-copy` / `xclip`，放的是**图片数据**），失败只记 Warning（`:127-130`）
  - 理由：`Windows.ApplicationModel.DataTransfer.Clipboard` 在 Uno/X11 无实现，只能借 `wl-copy`/`xclip`；且 X11 剪贴板没有「文件对象」这种统一载荷，放图片位图是跨桌面环境最兼容的做法（代价：粘贴到文件管理器里得到的是位图而不是文件）。旧总账记的「从不写剪贴板」已过时，现在受设置控制且真的会写。
- **第 139-171 行（`TryRunAsync`：子进程调用与超时）**｜eng 工程性｜reasonable
  - 上游：无外部截图进程（`ScreenCaptureHelper`/`ScreenCaptureContext` 全在进程内）；同类的外部进程调用在 `ScreenshotHelper.ConvertToJpgAsync`（`avifdec`/`djxl`，`ScreenshotHelper.cs:48-108`），无超时
  - 移植：`ProcessStartInfo { UseShellExecute = false, RedirectStandardError/Output }` + `ArgumentList` 逐参数传（避免路径空格/引号问题），链接 `CancellationToken` + 8 秒 `CancelAfter`，超时 `process.Kill(entireProcessTree: true)`，`ExitCode == 0` 才算成功，异常只 `Log.Debug`
  - 理由：外部工具在沙盒/精简系统会挂住（等授权、等选区），必须设超时；用 `ArgumentList` 而不是拼字符串是因为截图目录可能含空格（`~/Pictures` 一般没有，但用户可自定义）。UI 线程安全：调用方 `HotkeyManager` 用后台 `ContinueWith`，设置页用 `await`，`Program.Linux.cs:177` 的 `.GetAwaiter().GetResult()` 在 CLI（headless、非 UI 线程）路径上，不违反硬规则 10。

#### `Features/Screenshot/ScreenCaptureService.cs`

> 上游对照：`src/Starward/Features/Screenshot/ScreenCaptureService.cs`（555 行，同名同路径）。移植 555 行，**整文件 `<Compile Remove>`，不参与构建**（`Lunarward.csproj:127`）。

- **第 405 行（`SaveAsSdrAsync` 里取原神 HDR 亮度）**｜eng 工程性｜avoidable
  - 上游：`(_, outputMaxLuminance, _) = GameSettingService.GetGenshinHDRLuminance(runningGame.GameBiz);`（同步）
  - 移植：`(_, outputMaxLuminance, _) = await GameSettingService.GetGenshinHDRLuminanceAsync(runningGame.GameBiz).ConfigureAwait(false);`
  - 理由：意图是避免 UI 线程同步等待（硬规则 10 的同类问题），但**这条改动在当前构建里既没编译也没有收益**——移植侧 `Features/GameSetting/GameSettingService.cs:353-355` 仍保留同步包装 `GetGenshinHDRLuminance => GetGenshinHDRLuminanceAsync(biz).GetAwaiter().GetResult();`，而调用这个同步版本的 `GenshinHDRLuminanceSettingDialog.xaml.cs:52` 仍走 `.GetResult()`。要么把这条改动连同上游一起回流（上游 0.19.0 仍是同步调用），要么删掉避免误导。
- **第 1-555 行（整文件不参与构建）**｜eng 工程性｜must
  - 上游：本文件即「截图采集 + 编码 + 落盘 + 剪贴板 + 覆盖层」全流程，依赖 `Windows.Graphics.Capture`、`Win2D`（`CanvasBitmap`）、`Vanara.PInvoke.User32`、`DisplayInformation`、`ImageSaver`/`ImageThumbnail`（`Features/Codec`，同样全被排除）
  - 移植：`<Compile Remove>`，能力由 `ScreenCapture.Linux.cs` 重写；本目录只剩这一个「上游逻辑存档」
  - 理由：这些 API 在 Uno 下都没有，编译不过，排除是必须的。需要记的是**它带走了哪些行为**：HDR 抓帧/Uhdr 另存、按显示器选像素格式、`ScreenCaptureInfoWindow` 覆盖层、XMP/ICC 写作、avif/jxl 编码、`_encodeSlim` 串行化编码。这些在 `ScreenCapture.Linux.cs` 里都没有对应物（见该文件各条），是「截图子系统整体降级」而不是逐点偏离。

#### `Features/Screenshot/ImageViewWindow.xaml.cs`

> 上游对照：`src/Starward/Features/Screenshot/ImageViewWindow.xaml.cs`（564 行，同名同路径）。移植 564 行，**整文件 `<Compile Remove>`，不参与构建**（`Lunarward.csproj:121`）；生效实现是 `ImageViewWindow.Linux.cs`（43 行）。

- **第 299 行（`Button_OpenFile` 的「用默认应用打开」）**｜api 平台 API｜reasonable
  - 上游：`await Launcher.LaunchUriAsync(new Uri(CurrentImage.FilePath));`
  - 移植：`Starward.Helpers.OpenUrl.Open(CurrentImage.FilePath);`
  - 理由：`Windows.System.Launcher` 在 Uno/X11 下是空实现（不抛错也不打开），移植侧统一改走 `Helpers/OpenUrl`（内部 `xdg-open`）。同类替换在本组还有 `ScreenshotPage.Linux.cs:328` 的 `OpenWithDefaultApp`。注意本文件不参与构建，所以这条改动**当前没有任何运行时效果**，只是给「将来回流上游」留的样本。
- **第 1-564 行（整文件不参与构建）**｜eng 工程性｜must
  - 上游：完整看图窗口（`CanvasBitmap`/Win2D 渲染，缩放 `:125-300`、旋转、复制、另存、全屏、键盘翻页 `:300-550`），且上游 0.19.0 里**它已被 `ImageViewWindow2` 取代**——截图页与工具箱都调 `ImageViewWindow2`（`ScreenshotPage.xaml.cs:384`/`:467`、`ToolboxSetting.xaml.cs:87`），本文件在上游只剩「旧窗口」身份
  - 移植：排除；`ImageViewWindow.xaml`（190 行）仍编入并与 `ImageViewWindow.Linux.cs` 的空壳配套
  - 理由：`CanvasBitmap`/`Win2D`/`ContentIslandEnvironment` 在 Uno 下不可用，必须排除。**上游已迁移**：0.19.0 的活跃查看器是 `ImageViewWindow2`（移植侧对应 `ScreenshotViewerWindow`），本文件属上游历史包袱，跟着排除即可，不必再投人力。

#### `Features/Screenshot/ImageViewWindow.Linux.cs`

> 上游对照：`src/Starward/Features/Screenshot/ImageViewWindow.xaml.cs`（564 行，已被上游 `ImageViewWindow2` 取代）。移植 43 行，是**新增的 Linux 专属实现**，配合仍编入的 `ImageViewWindow.xaml`（190 行）。

- **第 11-18 行（`ImageFile { FilePath, Width, Height }`）**｜engine 引擎能力｜must
  - 上游：`CurrentImage` 是包装 `CanvasBitmap` 的对象（`ImageViewWindow.xaml.cs` 里持有 Win2D 位图与 `MaxCLL`/颜色信息，供缩放与另存使用）
  - 移植：极简 POCO，只有路径与宽高；文件头注释写明「上游用 Win2D 的 CanvasBitmap，Linux 侧只需要路径与尺寸」
  - 理由：Win2D 在 Uno 无实现，位图得靠 `SKBitmap`/`BitmapImage`，两者都不能直接当 `CanvasBitmap` 用；先只保留 XAML `x:Bind` 需要的三个字段。
- **第 20-42 行（窗口壳 + 8 个空命令）**｜cut 裁剪｜reasonable
  - 上游：564 行完整查看器——缩放（滚轮/按钮/双击）、旋转、复制图片、另存、全屏、删除、上一张/下一张、文件名与像素尺寸状态行
  - 移植：`CloseCommand`/`CopyImageCommand`/`FullScreenCommand`/`OpenFileCommand`/`RotateCommand`/`ZoomInCommand`/`ZoomOutCommand` 全是 `new RelayCommand(() => { })`（`:30-38`），`_Image_ImageOpened` 是空方法（`:39-42`），`FilePath`/`ImageCollection` 是裸属性
  - 理由：这是「先让 XAML 编过」的壳（M1 契约阶段）。因为上游 0.19.0 自己已经切到 `ImageViewWindow2`，移植侧把真实能力放在 `ScreenshotViewerWindow`（M3），这个壳没有升级必要——但见下一条：它连壳都还是坏的。
- **第 3 行注释与实际引用不符（死窗口，`ImageViewWindow.xaml` 却仍编入）**｜eng 工程性｜avoidable
  - 上游：该窗口在 0.19.0 已无调用点（引用都指向 `ImageViewWindow2`）
  - 移植：注释说「上游已被 ImageViewWindow2 取代，但仍被截图流程引用」——**这是错的**，全仓 `new ImageViewWindow(` 只出现在它自己的两个构造器里；而 `ImageViewWindow.xaml:114-187` 的工具栏按钮（`ZoomOut/ZoomIn/Rotate/FullScreen/CopyImage/OpenFile/Close`）绑的正是这 8 个空命令，`:68` `ItemsSource="{x:Bind ImageCollection}"` 绑的是一个 `string?`，`:88` 还留着 `IsThumbnail="True"`
  - 理由：三份文件（`ImageViewWindow.xaml`/`.xaml.cs`/`.Linux.cs`）合起来是一个**任何入口都到不了、按钮全无效**的窗口，还占着 `Lunarward.csproj` 的排除清单与 XAML 编译时间。要么删掉三份（查看器已由 `ScreenshotViewerWindow` 承担），要么至少修掉误导性注释。

#### `Features/Screenshot/ImageConvertItem.Linux.cs`

> 上游对照：`ImageConvertItem` 定义在上游 `src/Starward/Features/Screenshot/ImageBatchConvertWindow.xaml.cs:853-909`（该文件移植侧 `<Compile Remove>`）。移植 13 行，是**新增的占位实现**。

- **第 6-13 行（属性从 14 个缩到 3 个）**｜cut 裁剪｜avoidable
  - 上游：`SourceFilePath`/`SourceFileName`/`SourceExtension`/`SourceFileSize`/`SourceFileSizeText`/`SourceFileTime`/`Converting`/`ConvertSuccess`/`ConvertError`/`OutputFilePath`/`OutputFileName`/`OutputFileSize`/`FileDelta*`/`ErrorMessage` + `ctor(string filePath)`（`ImageBatchConvertWindow.xaml.cs:853-909`）
  - 移植：只剩 `FileName`/`FilePath`/`IsSelected` 三个自动属性 + `ObservableObject`
  - 理由：转换窗口没有移植（`ImageBatchConvertWindow.xaml.disabled`），条目模型只需要「文件 + 勾选」来喂 `ScreenshotConverter`。等真要补转换窗口（格式/画质/删原图/重命名）时，这份模型要整体重写，现在这个 13 行文件没有复用价值。
- **第 1-13 行（唯一消费者不参与构建 → 死代码）**｜eng 工程性｜avoidable
  - 上游：唯一消费者是 `ImageBatchConvertWindow.xaml.cs`（`ImageConvertItem` 的 `ObservableCollection` + `ConvertItem` 命令）
  - 移植：`Lunarward.csproj:120` 排除了 `ImageBatchConvertWindow.xaml.cs`，`ScreenshotPage.Linux.cs:308-321` 直接调 `ScreenshotConverter.ConvertToJpeg`，**没有任何代码引用 `ImageConvertItem`**（编译警告级别的孤立类型）
  - 理由：为「让被排除的 XAML 编过」而留的占位，但现在连引用都不存在。可删；若保留，建议加注释说明「与 `ImageBatchConvertWindow.xaml.disabled` 配套，勿当真实现用」，否则后来者容易以为批量转换有 UI。

#### `Features/Screenshot/ScreenshotFolderManageDialog.xaml`

> 上游对照：`src/Starward/Features/Screenshot/ScreenshotFolderManageDialog.xaml`（158 行，同名同路径）。移植 158 行，**参与构建**（`.xaml.cs` 未被排除），但全仓无实例化点。

- **第 100 行（「备份文件夹」按钮的图标码位）**｜cosmetic 外观文案｜reasonable
  - 上游：`<FontIcon FontSize="16" Foreground="{ThemeResource TextFillColorSecondaryBrush}" Glyph="&#xF413;" />`（同一按钮：`Button_BackupFolder`，`:89-101`，`Visibility="{x:Bind InGame}"`，`ToolTipService.ToolTip="{x:Bind lang:Lang.ScreenshotPage_BackupScreenshots}"`）
  - 移植：`Glyph="&#xE74E;"`
  - 理由：`F413` 属于 `Segoe MDL2 Assets`/`Segoe Fluent Icons` 段的私有码位，而 Uno 会拦截这两个族名导致随包字体不加载（L-15/L-88），码位会显示成豆腐块；`E74E` 在本仓库其余 6 处 XAML（`BackgroundSwitcher.xaml:429`、`GameAccountSwitcher.xaml:120`、`GameLauncherPage.xaml:108`、`BBSWebBridge.xaml:30`、`LauncherConfigSettingDialog.xaml:329`、`FileManageSetting.xaml:83`）都用作「保存/备份」语义，替换后至少语义一致。属于「换字体下的图标对齐」，非行为改变。
- **第 1-158 行（整个对话框没有任何入口）**｜eng 工程性｜avoidable
  - 上游：由 `ScreenshotPage.xaml.cs:352-373` 的 `ManageScreenshotFolderAsync` 弹出，用户可逐目录「打开 / 移除 / 备份到游戏目录」，`FolderChanged` 后写 `AppConfig.SetExternalScreenshotFolder`
  - 移植：唯一调用点在被 `<Compile Remove>` 的 `ScreenshotPage.xaml.cs:359`（和同样被排除的 `ScreenshotPage2.xaml.cs:445`），生效实现 `ScreenshotPage.Linux.cs:220-236` 直接用 `xdg-open` 打开目录——**对话框编译通过但用户永远看不到**
  - 理由：与 `ScreenshotPage.Linux.cs` 第 4 条同一个问题的两半：要么恢复「管理截图文件夹」入口（对话框本身逻辑完整，只差一个调用点 + 外部目录读写），要么连对话框一起删掉（含 `.xaml.cs`）。现在这种「编着不用」的状态最费维护：L-15 那类图标问题、`Lang` 键缺失、样式漂移都会继续被它牵动。

### 上游存在但移植侧缺失的文件

以下 4 个上游文件在 `src/Lunarward/Features/Screenshot/` 下**没有同名可编译文件**，只留 `.disabled` 后缀的 XAML 备份（时间戳均 2025-11-01 00:18）与 `Lunarward.csproj:120-133` 的 `Compile Remove` 条目。逐一核实结论：

| 上游文件 | 上游新增时间（是否 0.19.0 新增） | 上游引用点 | 移植侧替代 |
| --- | --- | --- | --- |
| `ImageViewWindow2.xaml` | **否**，2025-07-08 提交 `0b62f429` "new image viewer"（早于 0.19.0） | `ScreenshotPage.xaml.cs:384`/`:467`、`ScreenshotPage2.xaml.cs:496`、`ScreenCaptureInfoWindow.xaml.cs:103`、`Features/Setting/ToolboxSetting.xaml.cs:41/85/87`（工具箱可单独开查看器） | `ScreenshotViewerWindow.xaml(.cs)`（移植独有，`ScreenshotPage.Linux.cs:251`/`:294` 打开）：只有上一张/下一张/打开文件夹/关闭 + 预览图，缩放/旋转/全屏/另存/复制未实现 |
| `ScreenCaptureInfoWindow.xaml` | **否**，2025-06-22 提交 `dd6d0618` "screen capture framework" | `ScreenCaptureService.cs:38/92/110`、`Features/Setting/ScreenshotSetting.xaml.cs:41/292` | 无窗口；用 `Helpers.DesktopNotification` + `Helpers.InAppToast` 双份提示（`ScreenCapture.Linux.cs:78`/`:108`，调用方 `HotkeyManager.Linux.cs:273-295`、`ScreenshotSetting.Linux.cs:138-149`），无缩略图预览 |
| `ScreenshotPage2.xaml` | **否**，2025-08-19 提交 `a2862351` "change style of screenshot page" | **上游全仓零引用**（无实例化、无导航注册），是未启用的改版页 | 无需替代：移植直接用第一代 `ScreenshotPage.xaml` 的扁平改版 |
| `ImageBatchConvertWindow.xaml` | **否**，2025-09-04 提交 `1247555c` "image batch convert" | `ScreenshotPage.xaml.cs:338-349`（`new ImageBatchConvertWindow().Activate(list)`） | 无窗口；`ScreenshotConverter.ConvertToJpeg` 就地转同名 `.jpg`（`ScreenshotPage.Linux.cs:308-321`），无格式/画质/删原图/重命名选项 |

结论：这 4 个文件都是**上游 0.19.0 之前就存在的第一代/第二代界面演进遗留**，不是本轮合并带来的新缺口；其中 `ScreenshotPage2.xaml` 上游自己也没用上。移植侧的处理方式（`.disabled` 留档 + 排除编译）本身合理，但要在总账里记清：真正影响用户的是 `ImageViewWindow2`（查看器能力缩水）与 `ImageBatchConvertWindow`（批量转换语义变化），`ScreenCaptureInfoWindow` 的缺失表现为「截图反馈从覆盖层降级为通知」，`ScreenshotPage2` 无影响。

### 3.6 抽卡 / 战绩 / 自助查询（GachaLog / GameRecord）

#### `Features/Gacha/GachaLogExportFile.cs`

- **第 39 行（`UIGF3FileInfo.ExportApp` 初始化值）**｜cosmetic 外观文案｜reasonable
  - 上游：`public string ExportApp { get; set; } = "Starward";`（`[JsonPropertyName("export_app")]`，第 38 行）
  - 移植：`public string ExportApp { get; set; } = "Lunarward";`
  - 理由：`export_app` 只用于导出文件的溯源展示（UIGF 校验器按名字识别来源），无功能影响；写移植名避免把 Lunarward 的产物标成上游 Starward。本文件其余 56 行与上游逐行一致。

#### `Features/Gacha/GachaLogPage.xaml`

- **第 3 行（`sf:PageBase` 开标签上的属性）**｜eng 工程性｜reasonable
  - 上游：`<sf:PageBase x:Class="Starward.Features.Gacha.GachaLogPage" …>`，无 `NavigationCacheMode`（上游从不缓存页面）
  - 移植：`NavigationCacheMode="Required" x:Class="…"`
  - 理由：D60 为消除页面切换卡顿给 8 个重页开缓存，代价是该 XAML 只连接一次事件、`OnLoaded` 不再重跑，必须由同名 `.xaml.cs` 里的 B18/B19/B48 三处补偿兜住。文件另两处差异（根元素换行重排、BOM 去除）为纯格式，不记。

#### `Features/Gacha/GachaLogPage.xaml.cs`

- **第 58–67 行（`OnNavigatedTo` 开头新增的复位段）**｜eng 工程性｜must
  - 上游：`OnNavigatedTo` 只有 `base.OnNavigatedTo(e);`，随后直接 `GachaTypeText = GachaLogService.GetGachaLogText(CurrentGameBiz);`
  - 移植：先把 `EnableGenshinGachaItemStats`/`EnableStarRailGachaItemStats`/`EnableZZZGachaItemStats`/`IsZZZGachaStatsCardVisible` 置 `false`、`MenuFlyoutItem_SyncFromMiyoushe(.All)`/`SyncFromHoYoLAB(.All)` 置 `Collapsed`、`MenuFlyoutItem_CloudGame` 置 `Visible`，再按当前游戏重新置位（注释「B18/B19」）
  - 理由：缓存页复用后，上一款游戏的统计开关与同步菜单项会残留（星铁页里出现原神统计、国际服项残留等串味现象）。上游每次导航新建实例，天然没有这个问题——本条是 D60 的直接代价，不是可避免的重构。

- **第 153–155 行（`UpdateGachaLogMessage` 处理体内的窗口激活）**｜api 平台 API｜must
  - 上游：`User32.SetForegroundWindow((nint)this.XamlRoot.ContentIslandEnvironment.AppWindowId.Value);`（`Vanara.PInvoke`）
  - 移植：该行改为注释「`SetForegroundWindow` 在 Linux 无对应物，窗口激活交给合成器（D30）」，只保留 `_ = UpdateGachaLogInternalAsync(m.Url);`
  - 理由：X11 的 `SetForegroundWindow` 受焦点窃取策略限制、Wayland 下无对等 API（D30 已定「激活由合成器决定」）。降级后果：抽卡链接同步完窗口不会自己跳到前面，需要用户自己切回来——可接受但不完全等价。附：`using Vanara.PInvoke;`（第 26 行）已无任何引用，属清理项。

- **第 166–181 行（新增 `override OnGameChanged()`）**｜eng 工程性｜must
  - 上游：无此方法（上游 `PageBase` 里也没有该虚方法）
  - 移植：`DispatcherQueue.TryEnqueue(async () => { Initialize(); await UpdateWikiDataAsync(); })`，异常记 `"Reload gacha log page after game changed"`
  - 理由：`PageBase.OnNavigatedTo`（`Features/PageBase.cs:63-69`）检测到「渲染的游戏身份变了」就调 `OnGameChanged()`；缓存页切游戏时若只靠 `OnLoaded`，UID 列表、卡池选择、统计卡片会整片停在上一款游戏的数据上。注意：本方法**不重跑 `OnLoaded`**，因此 `OnLoaded` 里注册的消息订阅在第二次挂载后不会恢复（见文末未核实项）。

- **第 26 行（`using Vanara.PInvoke;`）**｜eng 工程性｜avoidable
  - 上游：同位置有该 using，且文件里确有 `User32.*` 调用
  - 移植：调用已删、using 残留，`grep -n "User32\|Vanara"` 只命中第 26 行本身
  - 理由：零成本的 diff 噪声，删掉或保留都不影响行为；建议删掉以免下次同步时误判「这个文件还依赖 Vanara」。

#### `Features/Gacha/GenshinBeyondGachaPage.xaml`

- **第 3 行（`sf:PageBase` 开标签上的属性）**｜eng 工程性｜reasonable
  - 上游：无 `NavigationCacheMode`（页面每次导航新建）
  - 移植：`NavigationCacheMode="Required"`
  - 理由：同 D60 的 8 页缓存策略。该页 code-behind（`GenshinBeyondGachaPage.xaml.cs`）与上游 **0 差异**（`diff -q` 一致），所以这里没有 `OnGameChanged` 补偿——但页面本身按游戏区分内容，切游戏后靠 `OnNavigatedTo`/绑定刷新是否完整，未核运行时表现。除该属性与 BOM 外本文件无其它差异。

#### `Features/Gacha/UIGF/UIGF3File.cs`

- **第 39 行（`ExportApp` 初始化值）**｜cosmetic 外观文案｜reasonable
  - 上游：`public string ExportApp { get; set; } = "Starward";`
  - 移植：`public string ExportApp { get; set; } = "Lunarward";`
  - 理由：同 `GachaLogExportFile.cs`，UIGF v3.0 导出头里的 `export_app` 溯源字段；本文件其余行与上游逐行一致。

#### `Features/Gacha/UIGF/UIGF4File.cs`

- **第 77 行（`UIGF4FileInfo` 构造函数里的 `ExportApp` 赋值）**｜cosmetic 外观文案｜reasonable
  - 上游：`public UIGF4FileInfo() { ExportTimestamp = …; ExportApp = "Starward"; ExportAppVersion = AppConfig.AppVersion; }`
  - 移植：同结构，`ExportApp = "Lunarward";`
  - 理由：UIGF v4.2 的 `export_app` 字段。注意这里是**构造函数**赋值（不是属性初始化器，第 63 行的属性本身无初值），所以只影响新建导出对象，读取旧文件时不受影响。

#### `Features/Gacha/UIGF/UIGFGachaService.cs`

- **第 12 行附近（`using Vanara.PInvoke;` 被删除）**｜eng 工程性｜reasonable
  - 上游：文件头有 `using Vanara.PInvoke;`
  - 移植：该 using 已删；全文件其余部分（UIGF 读写、`GachaLogService` 子类实现）与上游逐行一致
  - 理由：Linux 侧不再有 `User32`/P/Invoke 调用，删掉无用 using 属零行为差异清理；保留亦可（缩小 diff）。本文件功能上**无偏离**。

#### `Features/Gacha/ZZZGachaService.cs`

- **第 550 行（`UIGFInfo.export_app` 初始化值）**｜cosmetic 外观文案｜reasonable
  - 上游：`public string export_app { get; set; } = "Starward";`
  - 移植：`= "Lunarward";`
  - 理由：绝区零导出沿用 UIGF 结构（没有官方的 ZZZ 抽卡标准），同一套溯源字段；本文件其余行与上游一致。

#### `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs`

- **第 462 行（`ExportToFolderAsync` 末尾「打开导出目录」）**｜api 平台 API｜reasonable
  - 上游：`await Launcher.LaunchUriAsync(new Uri(folder));`（`Windows.System.Launcher`，Uno 下对目录 URI 无实现）
  - 移植：`Starward.Helpers.OpenUrl.Open(folder);`
  - 理由：`OpenUrl.Open`（`Helpers/OpenUrl.cs:21`，`public static bool Open(string? target)`）走 portal `OpenURI` → `xdg-open` → `gio open` 三级回退，是 Linux 侧统一的「用系统默认程序打开」入口。两点小瑕疵：返回值 `bool` 被丢弃（打开失败时界面无任何反馈）、且不再 `await`（不阻塞 UI，但也无法再捕获异常）。

#### `Features/GameRecord/BBSWebBridge.Linux.cs`

- **第 11–26 行（整个 `BBSWebBridge` 类体）**｜cut 裁剪｜must
  - 上游：`BBSWebBridge.xaml.cs` ~700 行——`CurrentGameBiz`、`GameRecordRole`（`[ObservableProperty]` + `OnGameRecordRoleChanged → LoadPageAsync(true)`）、`WebPageClosed` 事件、`InitializeWebViewAsync`/`EnsureCoreWebView2Async`、`CookieManager` 注入 cookie、`ExecuteScriptAsync(miHoYoJSInterface/HideScrollBarScript)`、`CallDevToolsProtocolMethodAsync("Page.captureScreenshot")` 截图、`HandleJsMessageAsync`/`HandleShareAsync` 消息桥、`CanGoBack`/`GoBack`/`Navigate`
  - 移植：26 行壳，只有 `GameBiz`、`IsHoyolab`、`IsLoggedIn`、3 个空 `RelayCommand`、`DocumentTitle` 自动属性；上游 `.xaml.cs` 由 `Lunarward.csproj:111` 排除
  - 理由：Uno Linux 的 WebView2 后端（WebKitGTK）没有 `CookieManager` 与 `ExecuteScriptAsync`（G0-3b 实测，D29），整座桥进 M4 占位。这是本组最大的功能缺口：米游社 BBS / 战绩详情页在移植侧**完全不可用**，且不是「排版差一点」而是「打不开」。

- **第 13 行（`GameBiz` 属性）**｜cut 裁剪｜reasonable
  - 上游：`public GameBiz CurrentGameBiz { get; set; }`（对端是 `BattleChronicleWindow.xaml:30` 的 `CurrentGameBiz="{x:Bind CurrentRole.GameBiz}"`）
  - 移植：改名成 `public GameBiz GameBiz { get; set; }`，且全仓无赋值/读取（`grep` 仅命中声明）
  - 理由：改名后与上游 XAML 的对端彻底断开；两者目前都只是占位骨架。将来启用 BBS 页时必须恢复成 `CurrentGameBiz`（或同步改 XAML），否则 `x:Bind` 目标缺失。

- **第 18 行（`IsLoggedIn`）**｜linux 专属实现｜reasonable
  - 上游：无此成员；登录态活在 WebView2 的 cookie 容器里，由 `EnsureCoreWebView2Async` 之后的导航结果隐式决定
  - 移植：`public bool IsLoggedIn => WebLoginService.Current.HasCookies;`（独立 WebKitGTK 登录窗写出的 cookie 文件）
  - 理由：登录态从「页面内 cookie 容器」迁到「独立登录窗 + cookie 文件」（D29/D32）。语义差别：这里判的是**全局有没有票**，不区分 miyoushe / hoyolab 哪个站、也不区分账号，多区/多账号切换时可能「有票但不对站」——当前无调用方，暂不构成现实缺陷（未核实际表现）。

- **第 20–22 行（3 个截图命令）**｜cut 裁剪｜must
  - 上游：`CloseScreenshotGridCommand` / `CopyScreenshotCommand` / `SaveScreenshotCommand` → `CallDevToolsProtocolMethodAsync("Page.captureScreenshot")` 拿 base64 → `HandleShareAsync` 落盘/复剪贴板
  - 移植：三个 `new RelayCommand(() => { })` 空实现，XAML 上的按钮仍在
  - 理由：截图依赖 DevTools 协议，Uno 后端没有；M4 前无替代。注意这不是「隐藏不可用功能」而是「按钮可点但无反应」，对用户来说是坏体验——建议连同 XAML 一起吃掉（或置灰）。

- **第 25 行（`DocumentTitle`）**｜cut 裁剪｜must
  - 上游：`public string DocumentTitle { get; set => SetProperty(ref field, value); }`，页面 `NavigationCompleted`/`DocumentTitleChanged` 时同步标题
  - 移植：纯自动属性 `public string? DocumentTitle { get; set; }`，全仓无人赋值
  - 理由：没有 WebView 就没有标题来源。连带后果：`BattleChronicleWindow.xaml:9/:25` 两处 `x:Bind bbsWebBridge.DocumentTitle` 永远为空（窗口标题栏与标题栏文本都空），且移植版窗口根本没调 `InitializeComponent`，这条绑定连初始化都不会跑。

#### `Features/GameRecord/BattleChronicleWindow.Linux.cs`

- **第 11–62 行（构造函数整体）**｜cut 裁剪｜must
  - 上游：`this.InitializeComponent(); InitializeWindow();`，其中 `InitializeWindow` 设 `AppWindow.TitleBar.ExtendsContentIntoTitleBar = true`、`Title = Lang.HoyolabToolboxPage_BattleChronicle`、`RootGrid.RequestedTheme = …`、`AdaptTitleBarButtonColorToAcuallTheme()`、`SetIcon()`、`CenterInScreen()`（`DisplayArea` 取工作区 95% 高、按 16:9 算宽）
  - 移植：不调 `InitializeComponent`，代码里手搓 `Grid(Padding=24)` + `StackPanel(Spacing=8, MaxWidth=420)` 的说明页；无标题栏扩展、无图标、无 16:9 居中、无主题同步
  - 理由：BBS 页面不可用（上一条），窗口没有内容可承载。改为「讲清楚为什么没有」是 L-183 的补救（以前是 `Content = new Grid();` 一片全白）。代价：窗口几何由 Uno/合成器默认决定，与上游的「大窗居中」观感不同。

- **第 22 行（`Title = "战绩"`）**｜cosmetic 外观文案｜avoidable
  - 上游：`Title = Lang.HoyolabToolboxPage_BattleChronicle;`（随界面语言切换）
  - 移植：硬编码简体中文 `"战绩"`
  - 理由：同目录其他文案都走 `lang:Lang`（`GameRecordPage`、`DailyNote`、`Shiyu` 全是 `x:Bind lang:Lang.*`），这一处是漏网；英文/繁中界面下窗口标题仍是简中。可避免，改一行的事。

- **第 23–56 行（占位说明文案）**｜cosmetic 外观文案｜reasonable
  - 上游：无（窗口里直接是 BBS 页面）
  - 移植：`FontIcon E9D9` + 标题 `"Linux 版暂时不能打开战绩页面"` + 两段说明（`"上游用 WebView2 承载米游社页面，Linux 版要等独立的 WebKitGTK 窗口（里程碑 M2）…"`）+ 角色行 `_roleText`
  - 理由：把「坏了」变成「已知限制」是合理补救（L-183）。两处该收拾：文案硬编码简中、未入 `Lang`；「里程碑 M2」是内部术语，不该出现在用户可见文本里。

- **第 64–72 行（`ShowBattleChronicle(Window owner, GameRecordRole? role)`）**｜eng 工程性｜avoidable
  - 上游：无此方法；`CurrentRole` 是 `[ObservableProperty]`，由 `BattleChronicleWindow.xaml:30-31` 的 `x:Bind` 推给 `BBSWebBridge`
  - 移植：方法体设 `OwnerWindow`/`CurrentRole`/`_roleText.Text` 后 `Activate()`；但全仓 `grep ShowBattleChronicle` **零调用者**——`GameRecordPage.xaml.cs:344-359 ShowBattleChronicleWindow()` 是 `new BattleChronicleWindow { CurrentRole = CurrentRole }` + `_battleChronicleWindow.Activate()`
  - 理由：死代码。连带后果：`_roleText` 永远不被赋值（第 68-70 行的角色行渲染逻辑走不到），占位窗口里「当前角色：…」那行**始终为空**；`OwnerWindow` 同样无人使用。要么让 `GameRecordPage` 改调本方法，要么删掉方法并把这行文本接到 `CurrentRole` 的 setter 上。

- **第 1–2 行（头注释揭示的整体处理方式）**｜eng 工程性｜avoidable
  - 上游：`BattleChronicleWindow.xaml`（35 行，含 `RootGrid_Loaded` 处理、`CurrentGameBiz`/`GameRecordRole` 两个 `x:Bind`）+ `.xaml.cs` 共同构成该窗口
  - 移植：`.xaml.cs` 被 `Lunarward.csproj:112` 排除，但 `.xaml` 仍留在 `src/Lunarward/Features/GameRecord/` 里，**工程内没有任何 `Page Remove`**（只有 53 条 `Compile Remove`，且 `EnableDefaultPageItems=false`）；同时 `BBSWebBridge.xaml:28-31` 引用的 `CurrentGameBiz`/`GameRecordRole`/`RootGrid_Loaded` 在 26 行 shim 里都不存在
  - 理由：两种可能都不健康——(a) 这几个 XAML 参与编译的话，`x:Bind` 目标缺失会直接编译失败；(b) 不参与编译的话，它们就是纯死文件（上游 3 个 XAML：`BBSWebBridge.xaml`、`BattleChronicleWindow.xaml`、`LoginPage.xaml`）。本组不构建，**未核实**属于哪种；无论哪种都应在 `12-shims-and-gaps.md` 里钉死状态，避免下一个人以为「窗口还在、接上就能用」。

#### `Features/GameRecord/DailyNote/DailyNoteButton.xaml`

- **第 310 行（「禁止获取便笺」提示图标 `Glyph`，第 311 行 `Visibility="{x:Bind IsForbid}"`）**｜engine 引擎能力｜must
  - 上游：`Glyph="&#xE733;"`
  - 移植：`Glyph="&#xF140;"`
  - 理由：L-15/L-88 的字体回退问题——`E733` 不在本项目可加载的字体覆盖码位内（`13-lessons.md:823` 用 fontTools 比对 104 个码位，`E62E E733 F012 F08F F413 F4A5` 均缺失；`:829` 给出替换表 `E733→F140`，`00-decisions.md:446/450` 同源）。这是**引擎能力缺口**，不是设计选择，删不掉。

- **第 406 行（满体力时间的 `x:Bind` 格式串）**｜engine 引擎能力｜must
  - 上游：`{x:Bind StarRailDailyNote.StaminaFullTime.LocalDateTime.ToString('HH\:mm', x:Null)}`
  - 移植：`ToString('HH\\:mm', x:Null)`（双反斜杠）
  - 理由：Uno 的 XAML 生成器把 `\:` 原样写成 C# 字符串，编译期 `error CS1009: 无法识别的转义序列`——而且报错指向生成文件、不给 XAML 行号（`13-lessons.md:160`，`00-decisions.md:1510/1515` 记为 D95/B49 实测）。**旧总账把这条记成 `bugfix / avoidable`（当作可避免的编码失误）是错的**：单反斜杠在 Uno 下根本编不过，只能是双反斜杠，且代价是时间格式不再跟随区域分隔符。

#### `Features/GameRecord/GameRecordPage.xaml`

- **第 3 行（`sf:PageBase` 开标签上的属性）**｜eng 工程性｜reasonable
  - 上游：无 `NavigationCacheMode`
  - 移植：`NavigationCacheMode="Required"`（页面第 14 行另有 `x:DefaultBindMode="OneWay"`，上游同）
  - 理由：D60 缓存策略；本页是「缓存补偿」最密集的一页（`OnGameChanged`/`OnUnloaded`/页签可见性三处），见同名 `.xaml.cs`。

- **第 490–506 行（新增空态层）**｜cosmetic 外观文案｜avoidable
  - 上游：根 `Grid` 里只有 `NavigationView`，没有角色时内容区就是空白（背景图透出来，看起来像坏页）
  - 移植：`<Grid Margin="248,0,0,0" Background="{ThemeResource ApplicationPageBackgroundThemeBrush}" Visibility="{x:Bind ShowRecordEmptyState}">`，内含 `FontIcon E9D9` + `Lang.Lunarward_Record_Empty_Title/_Description` + `Button Command="{x:Bind WebLoginCommand}"`（注释标 D61/B50）
  - 理由：这是**移植自造的 UI**（上游 0.19.0 没有空态），不是平台必需，所以记 `avoidable`。B50 已把「铺满整页盖住左侧工具箱」修成只盖内容区（`Margin="248,0,0,0"` 对应 `OpenPaneLength=248`），现在不会挡点击。若目标是「还原上游观感」，删掉这一层即回到空白；若要保留，文案已进 `Lang`（比 `BattleChronicleWindow` 的硬编码规范）。

#### `Features/GameRecord/GameRecordPage.xaml.cs`

- **第 70–100 行（新增 `override OnGameChanged()`）**｜eng 工程性｜must
  - 上游：无（切游戏＝重新导航＝新实例，`OnLoaded` 跑完整初始化）
  - 移植：`DispatcherQueue.TryEnqueue` 内依次 `InitializeNavigationViewItemVisibility()` → `CurrentRole = null` → `GameRoleList = null!` → `NavigateTo(typeof(BlankPage))` → `if (await CheckAgreementAsync()) { LoadGameRoles(); await UpdateDeviceInfoAsync(); await RefreshGameRoleHeadIconSilentlyAsync(); }`，异常记 `"Reload game record page after game changed"`（注释 B51）
  - 理由：缓存页切游戏必须把「上一款游戏的角色/页签/设备信息」整套换掉，否则会显示别的游戏的 UID 与战绩（比抽卡页串味严重得多）。`NavigateTo(BlankPage)` 是清内容区的必要动作（`ContentFrame` 不重建时旧页还在）。

- **第 105–160 行（新增 `CheckAgreementAsync()`）**｜cosmetic 外观文案｜avoidable
  - 上游：0.19.0 无免责声明对话框（`AppConfig.Setting.cs:115` 的 `AcceptHoyolabToolboxAgreement` 字段两侧都还在，但上游页面已不再读它）；接口侧也不再要求先接受
  - 移植：`AcceptHoyolabToolboxAgreement` 为假时弹 `ContentDialog`（`Lang.Common_Disclaimer` + `HoyolabToolboxPage_DisclaimerContent` + 「接受 (5s)」倒计时、`DefaultButton=Secondary`、拒绝则 `Send(MainViewNavigateMessage(typeof(GameLauncherPage)))` 并返回 `false`），注释标 `LUNARWARD-DEVIATION`/D61
  - 理由：上游已经不需要这道声明，移植自己补回来＝自加摩擦，可避免。另有一处行为不一致值得记：全文件唯一调用点是 `OnGameChanged`（第 85 行），**首次进入本页不会触发**，只有「切游戏」时才弹——新用户反而看不到声明，老用户每次切游戏都要看一次（直到点接受）。

- **第 164–172 行（`OnUnloaded` 不再退订 XAML 事件）**｜eng 工程性｜must
  - 上游：`OnUnloaded` 里 `NavigationViewItem_BattleChronicle.Tapped -= NavigationViewItem_BattleChronicle_Tapped;` 与 `NavigationViewItem_UpdateDeviceInfo.Tapped -= NavigationViewItem_UpdateDeviceInfo_Tapped;`（上游 `GameRecordPage.xaml.cs:70` 一带）
  - 移植：两条退订删除，只留 `WeakReferenceMessenger.Default.UnregisterAll(this)` + 清 `CurrentRole`/`GameRoleList`/`_battleChronicleWindow`，并以注释说明「XAML 只连接一次，退订了就再也接不回来」（D94）
  - 理由：页面缓存后 `Unloaded` 会反复发生，而上游写法基于「实例只活一次」；照抄会导致第二次进入本页后「战绩」「更新设备指纹」两个按钮永久失效（这条是 D94 期间真实修掉的 bug）。

- **第 216–244 行（`AllToolboxItemNames` + `InitializeNavigationViewItemVisibility` 的「先全收起」）**｜eng 工程性｜must
  - 上游：`InitializeNavigationViewItemVisibility()` 直接按游戏逐项 `Visibility = Visible`，从不预置 `Collapsed`
  - 移植：先 `foreach (name in AllToolboxItemNames) if (FindName(name) is NavigationViewItem item) item.Visibility = Collapsed;`（14 个 `nameof`），再按当前游戏打开
  - 理由：缓存页面上，只「打开本游戏的项」不会关掉上一款游戏的项 → 切游戏后页签残留（原神页里出现绝区零的「式舆防卫战」）。上游每次新建实例所以看不出来。

- **第 245 行（`NavigationViewItem_UpdateDeviceInfo.Visibility` 按区服赋值）**｜bugfix 修上游缺陷｜reasonable
  - 上游：XAML 里该项 `Visibility="Collapsed"`（`GameRecordPage.xaml:475`），全仓再无任何地方把它置回 `Visible` → 上游这个「更新设备指纹」入口是**死入口**（`Tapped` 处理器与 `Lang.HoyolabToolboxPage_UpdateDeviceFingerprint` 文案都还在，就是点不到）
  - 移植：`NavigationViewItem_UpdateDeviceInfo.Visibility = CurrentGameBiz.IsGlobalServer() ? Collapsed : Visible;`
  - 理由：把上游的死入口救活，并限定在国服（HoYoLAB 侧不需要设备指纹）。注意这是**故意与上游不一致**——若哪天上游把它做成「永不可见」的废弃入口，移植要跟着改。

- **第 376–397、503–506 行（空态数据源 + 空态登录入口）**｜cosmetic 外观文案｜avoidable
  - 上游：无 `ShowRecordEmptyState`、无 `WebLogin` 命令
  - 移植：`CurrentRole` setter 与 `GameRoleList` setter 各补 `OnPropertyChanged(nameof(ShowRecordEmptyState))`；`public bool ShowRecordEmptyState => CurrentRole is null || GameRoleList is null || GameRoleList.Count == 0;`；`[RelayCommand] WebLogin() => NavigateTo(typeof(LoginPage), CurrentGameBiz);`
  - 理由：整套空态是移植自造（配合上一条 XAML）。其中 `WebLogin` 把用户导到**上游已删除的 `LoginPage`**（见本组 `LoginPage.Linux.cs`）——上游此时走的是 `LoginCommand → MainView.Current.HypLoginAsync`（第 540-556 行移植侧仍保留），同一个功能有两条路径，且空态这条走的是自制页。可避免：删空态，或让空态按钮直接调 `HypLoginAsync`。

- **第 594–620 行（`UpdateDeviceInfoAsync(bool forceUpdate)` 补回）**｜eng 工程性｜reasonable
  - 上游：`NavigationViewItem_UpdateDeviceInfo_Tapped` 只有 `GameRecordServiceBase.ResetDeviceInfo(); InAppToast.MainWindow?.Success(Lang.HoyolabToolboxPage_TheDeviceFingerprintIsAlreadyUpdated);`——即 0.19.0 起「只丢缓存 + 提示」，不再发请求
  - 移植：`ServerRegion region = CurrentGameBiz.IsGlobalServer() ? HoYoverse : miHoYo` → `AppConfig.GetService<WindowsDeviceFpService>()` → `forceUpdate` 时 `ResetDeviceInfo()` + `await InitializeDeviceFpAsync(region)` → `_logger.LogDebug("Device fp: {deviceFp}", GetDeviceFp())` → `await _gameRecordService.GetDeviceInfoAsync(region)`（返回值丢弃，只做预热/写回 `AppConfig.AndroidDeviceInfo`）→ 成功/失败各自 `InAppToast`
  - 理由：语义上比上游多做一次「先把指纹备好、再生成 payload」，失败时还能弹错误（上游点下去永远报成功）。两点要注意：① `WindowsDeviceFpService.InitializeDeviceFpAsync` 在 `MIHOYOSDK_DEVICE_FP` 已存在时直接 return（`Features/Passport/WindowsDeviceFpService.cs:44-46`），所以这一步**不会换新指纹**，也不会失败——所谓「更新设备指纹」两侧其实都只是重建 payload；② `GetDeviceInfoAsync` 的返回值被忽略，若接口抖动这里会多弹一次错误（上游没有这条失败路径）。

- **第 635–650 行（新增 `RefreshGameRoleHeadIconSilentlyAsync()`）**｜eng 工程性｜reasonable
  - 上游：`UpdateSelectedGameRoleAsync` 那条路径仍在（用户手动切角色时刷新），但没有「切游戏后静默刷新当前角色」的动作
  - 移植：`CurrentRole` 非空时 `await _gameRecordService.UpdateGameRoleAsync(CurrentRole)`，异常只记 `"Update game role head icon silently ({gameBiz}, {uid})."`
  - 理由：缓存页切回某游戏时，角色头像可能是很久以前缓存的（`HeadIcon` 是带时效的 CDN 链接）；静默刷新避免显示裂图/过期图。失败不打扰用户是合理取舍，但「静默」也意味着接口长期失败时无人察觉（仅有 Debug 日志）。

#### `Features/GameRecord/LoginPage.Linux.cs`

> 前提：**上游 0.19.0 已删除整个 `LoginPage`**（`src/Starward/Features/GameRecord/` 下无 `LoginPage.xaml(.cs)`，`grep -r LoginPage src/Starward` 0 命中；上游登录统一走 `Features/Passport/HypLoginWindow`）。移植侧仍保留该页（`LoginPage.xaml` 129 行 + 本文件 103 行），属于**移植自造的页面**。

- **第 1–8 行（文件头注释）**｜cosmetic 外观文案｜avoidable
  - 上游：无对端（页已删）
  - 移植：注释仍写「上游在本页内嵌 WebView2…」「`WebLoginService（拉起 python3 助手）`」「M2 换成 GirCore 进程内实现后本页 UI 不变」
  - 理由：三处都已过期——上游没有这个页了；`WebLoginService.LoginAsync` 现在优先 `TryStartNativeHelper` 起本程序的 `--webkit-login`（WebKitGTK 6.0 子进程，D76），`python3 + login.py` 只是兜底；GirCore 方案已不在路线图上（D32 之后改走 WebKitGTK helper 进程）。注释是审计的锚点，留着过期注释会让人按错模型改代码。

- **第 19 行（`public sealed partial class LoginPage : PageBase`）**｜linux 专属实现｜must
  - 上游：0.19.0 无此类（df8ae07 时代有，WebView2 + `CookieManager` 取票）
  - 移植：保留页面并在 `GameRecordPage` 空态里作为登录入口（`GameRecordPage.xaml.cs:503-506`）
  - 理由：Uno 的 WebView2 没有 `CookieManager`，取票只能靠外部登录窗（D29/D32），所以「页面里放一个按钮拉起登录窗」这个形态本身是平台必需。但**页面是否该存在**是另一回事：上游已把登录统一到 `HypLoginWindow`，移植侧有两条并行登录路径（`HypLoginAsync` 与 `LoginPage`）。另：同目录 `LoginPage.xaml.cs`（268 行旧 Windows 实现，含 `EnsureCoreWebView2Async`/`CookieManager`/按 `GameBiz` 拼 `bh3|ys|sr|zzz`）仍在仓库内、被 `Lunarward.csproj:113` 排除，与 `LoginPage.Linux.cs` 双份并存——建议删旧文件，否则下一个同步的人会以为它还是活的。

- **第 25–27 行（`URL_CN` / `URL_OS` 常量）**｜linux 专属实现｜reasonable
  - 上游（旧版，见本目录未编译的 `LoginPage.xaml.cs:20/48-52`）：`URL_CN = "https://www.miyoushe.com/"`，再按 `GameBiz` 拼成 `bh3/ys/sr/zzz` 社区页
  - 移植：`URL_CN = "https://user.miyoushe.com/"`、`URL_OS = "https://www.hoyolab.com/"`（`GetGameBizUrl()` 只按 `IsChinaServer()` 二选一）
  - 理由：独立 WebKitGTK 登录窗只需要「取票」，落用户中心比落某个游戏社区页更稳（社区页会随版本改路径）。代价：本页不再有「进对应游戏社区」的能力——不过上游 0.19.0 已无此页，没有可回归的对端。

- **第 42–71 行（`Button_OpenLoginWindow_Click`：取票全流程）**｜linux 专属实现｜must
  - 上游：页面内 `EnsureCoreWebView2Async` → `CookieManager` 注入/读取 → 把 cookie 交给 `GameRecordService`
  - 移植：`await WebLoginService.LoginAsync(GetGameBizUrl(), TimeSpan.FromMinutes(5))` → `WebLoginService.Current.Cookies!.Cookies` 拼成 `k=v;k=v` → `await _gameRecordService.AddRecordUserAsync(cookie)` → `var roles = await _gameRecordService.AddGameRolesAsync(cookie)` → `Send(new GameRecordRoleChangedMessage(...))`，状态写 `TextBlock_LoginStatus`
  - 理由：唯一可行的取票路径（G0-3b：无 `CookieManager`）；`AddRecordUserAsync`/`AddGameRolesAsync` 是上游 0.19.0 的现成接口（`GameRecordService.cs:278/329`），这里复用得干净。观察：全程在 UI 事件处理器里 `await`（不阻塞 UI，正确）；5 分钟硬超时是写死的。

- **第 60 行（`Send(new GameRecordRoleChangedMessage(...))`）**｜eng 工程性｜avoidable
  - 上游：0.19.0 已无 `GameRecordRoleChangedMessage` 类型（`grep -r "GameRecordRoleChangedMessage" src/Starward` 0 命中）
  - 移植：保留该类型（`Features/GameRecord/GameRecordRoleChangedMessage.cs`）并在此发送；但全仓只有「定义 + 两处 `Send`」（另一处在未编译的旧 `LoginPage.xaml.cs:240`），**没有任何 `Register<GameRecordRoleChangedMessage>`**
  - 理由：这条消息目前空放——登录成功后不会刷新战绩页的角色、也没有任何别的订阅者。要么补注册方（参照上游 passport 的刷新链路），要么删掉类型与发送，避免「看起来登录完成会通知页面」的错觉。

- **第 74–80 行（`Button_Finish_Click`）**｜cut 裁剪｜reasonable
  - 上游（旧版）：从 WebView 的 cookie 完成收尾、关闭页面/回填
  - 移植：只把 `TextBlock_LoginStatus.Text` 换成「登录态已存在，可直接返回战绩页」/「请先点击「打开登录窗口」完成登录」
  - 理由：取票已经在打开登录窗时完成，这个按钮退化为状态检查；行为不丢。顺带：`LoginPage.xaml:99-105` 里那个 `<WebView2 Name="webview" Visibility="Collapsed">` 是旧实现的遗骸，本文件一次都没用到 `webview` 字段（未核 Uno 是否会为此实例化一个 WebKit 进程——若会，删掉它能省一份后端资源）。

- **第 83–101 行（`Button_GoBack_Click` / `GoForward` / `RefreshOrCancel` / `TextBox_Uri_KeyDown`）**｜cut 裁剪｜must
  - 上游：四个处理器都操作页内 WebView2（`CanGoBack`/`GoBack`/`GoForward`/`Reload`/`Navigate`/地址栏回车）
  - 移植：四个全部空实现，逐个带 `// LINT-ALLOW:` 说明（导航由助手窗自己的工具栏承担 / 页面内没有刷新取消这一步 / 地址栏只做展示）
  - 理由：页内没有浏览器了，这些动作无处执行。`LINT-ALLOW` 标注说明是**故意留空**而非漏写（`scripts/test/lint-ui-thread.sh` 会查空 stub），处理方式规范。

- **第 48、52、57、61、65 行（面向用户的状态文案）**｜cosmetic 外观文案｜avoidable
  - 上游：文案全走 `Lang`（旧版 `LoginPage.xaml` 里是 `{x:Bind lang:Lang.LoginPage_*}`）
  - 移植：五处硬编码简体中文（「登录窗口已打开，请在新窗口中完成登录（最长等待 5 分钟）…」「未取得登录态：窗口被关闭或等待超时，可重试。」「已取得登录态，正在获取角色信息…」「登录成功，已添加 {roles.Count} 个角色。」「登录失败：{ex.GetType().Name}: {ex.Message}」）
  - 理由：本页是移植自造页，但同一模块其他页（`GameRecordPage`/`DailyNote`/`LoginPage.xaml`）都已经 `Lang` 化，这里漏了；并且最后一条把**异常类型名与原始 message 直接抛给用户**（多为英文技术文本），既不可读也泄露实现细节。可避免。

#### `Features/GameRecord/StarRail/SimulatedUniversePage.xaml`

- **第 370–405 行（效果/buff 标签云的容器）**｜engine 引擎能力｜must
  - 上游：`<ItemsRepeater ItemsSource="{x:Bind Items}"><ItemsRepeater.Layout><cc:WrapLayout … /></ItemsRepeater.Layout>` + `ItemTemplate`
  - 移植：`<ItemsControl ItemsSource="{x:Bind Items}"><ItemsControl.ItemsPanel><ItemsPanelTemplate><cc:WrapPanel … /></ItemsPanelTemplate></ItemsControl.ItemsPanel>` + **同一份** `ItemTemplate`（`x:DataType` 未改）
  - 理由：`13-lessons.md:213-224`（L-23）——本项目里 `ItemsRepeater` 不调用自写 `VirtualizingLayout` 的 `Measure/Arrange`，标签云会挤成一行；`20 现行做法` 明确要求换行排布改用 `ItemsControl + ItemsPanelTemplate`。代价是失去虚拟化（这一屏标签数量少，可接受）；`Controls/ToolkitControls.cs` 里还留着一个空 `WrapLayout` 只为兼容残留 XAML。本文件其余 434 行与上游一致。

#### `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml`

- **第 299、370、627、994、1196 行（`SegoeFluentIcons` 的小字号图标，多为 `FontSize="10"`）**｜engine 引擎能力｜must
  - 上游：`Text="&#xF08F;"`
  - 移植：`Text="&#xE768;"`
  - 理由：同 `DailyNoteButton` 的字体缺失问题——`F08F` 不在可加载字体覆盖内，`13-lessons.md:829` 的替换表给出 `F08F→E768`（`00-decisions.md:446/450` 同源），是字号/字形上最接近的等价符号。5 处全改，无功能影响但视觉略有差别（同一字形不同风格）。

- **第 439–451、696–708 行（元素抗性图标的 `Visibility` 绑定，共 10 处）**｜engine 引擎能力｜must
  - 上游：`Visibility="{x:Bind local:ShiyuDefensePage.ElementWeaknessVisibility(IceWeakness, -1)}"`（`-1` 是「无该元素弱点」的哨兵值）
  - 移植：`…, local:ElementWeaknessConstants.None)`，常量定义在 `Features/GameRecord/ZZZ/ElementWeaknessConstants.cs`：`public const int None = -1;`
  - 理由：Uno 的 `x:Bind` 解析器不接受负数实参（`f(x, -1)` → `UXAML0001 Invalid identifier character -`），只能提成常量；`ElementWeaknessVisibility(int value, int weakOrResist)`（`ShiyuDefensePage.xaml.cs:169`）两侧签名与实现完全一致，语义零变化。这两条都是编译期硬约束，不是风格选择。

#### `Features/SelfQuery/SelfQueryPage.xaml`

- **第 2 行（`sf:PageBase` 开标签上的属性）**｜eng 工程性｜reasonable
  - 上游：无 `NavigationCacheMode`
  - 移植：`NavigationCacheMode="Required"`
  - 理由：D60 缓存策略；该页的补偿只做了一半（见下一条 `.xaml.cs`），因为本页三份列表是**同屏并存**而不是切主题切换，缓存后需要显式互斥。文件其余 265 行与上游一致。

#### `Features/SelfQuery/SelfQueryPage.xaml.cs`

- **第 55–58 行（`OnNavigatedTo` 开头新增的「三个列表先全收起」）**｜eng 工程性｜must
  - 上游：`OnNavigatedTo` 里只有三个 `if (CurrentGameBiz.Game == …) ListView_QueryItems_*.Visibility = Visible;`，从不预置 `Collapsed`
  - 移植：先 `ListView_QueryItems_Genshin/_StarRail/_ZZZ.Visibility = Visibility.Collapsed;` 三行，再按当前游戏打开对应一个（注释 B20）
  - 理由：缓存页复用后，只「打开当前游戏的列表」不会关掉上一款游戏的列表 → 三个自助查询列表叠在同一行（原神 uid 查询、星铁、绝区零挤在一起）。上游每次新建实例所以没这个问题，属 D60 的必需补偿。

---

##### 附 · 旧总账（`25-deviation-register.md` §3.6，第 939–1080 行）逐条结算

| 旧条目 | 现状 | 结算 |
| --- | --- | --- |
| `GachaLogPage.xaml` / `GenshinBeyondGachaPage.xaml` / `GameRecordPage.xaml` / `SelfQueryPage.xaml`：只记 `NavigationCacheMode`（eng/reasonable） | 4 处全部仍在，行号 3 / 3 / 3 / 2 | 4 条仍成立（判据未变） |
| `GachaLogPage.xaml.cs`：只记 `SetForegroundWindow` 移除 | 仍成立；**新增 3 条**（第 58-67 复位段、第 166-181 `OnGameChanged`、【清理项】第 26 行 Vanara using 残留） | 1 条仍成立 / 3 条新增 |
| `GameRecordPage.xaml`：空态层记 cosmetic/avoidable，当时「铺满整页盖住工具箱」 | B50 已修（`Margin="248,0,0,0"` 只盖内容区），**仍判 avoidable**（上游无空态） | 仍成立，但缺陷已修 |
| `GameRecordPage.xaml.cs`：旧账未记（当时只有 `SetForegroundWindow` 之类的零星条目） | 0.19.0 后上游删掉/简化了三块能力，移植补回：`OnGameChanged`(B51)、`CheckAgreementAsync`、`UpdateDeviceInfoAsync`、`RefreshGameRoleHeadIconSilentlyAsync`；另有 D94 的两处（`OnUnloaded` 不退订、页签先全收起）与救活上游死入口（第 245 行） | 8 条新增 |
| `LoginPage.*`：记 linux/must，且「`LoginPage.xaml` 隐藏 WebView2」当作必要妥协 | **上游已删除整页**，移植侧属自造页；隐藏的 `WebView2` 已无任何 C# 引用；同目录还留着被排除的 268 行旧实现 | 需重估：linux/must 仍成立（取票只能靠外部登录窗），但「保留本页」本身变成可避免（上游已统一到 `HypLoginWindow`） |
| `BBSWebBridge.*` / `BattleChronicleWindow.*`：cut/must（M4 占位） | 仍成立。但 `BattleChronicleWindow` 已从「全白」改成带说明的占位页（L-183），且 `ShowBattleChronicle` 是死代码、角色行永远为空 | 仍成立 + 2 条新增（死代码、占位文案/硬编码标题） |
| `SimulatedUniversePage.xaml`：engine/must（ItemsRepeater→ItemsControl） | 仍成立，L-23 已是定论 | 仍成立 |
| `ShiyuDefensePage.xaml`：glyph + 负数 `x:Bind`，engine/must | 仍成立（5 处 glyph、10 处 `ElementWeaknessConstants.None`） | 仍成立 |
| `DailyNoteButton.xaml`：glyph `E733→F140` engine/must；`'HH\:mm'` 记 **bugfix/avoidable** | glyph 仍成立且**证据升级**（fontTools 实测码位缺失，不再是「推断」）；时间格式**必须改判**：`\:` 在 Uno 下编不过（CS1009 实测），应为 `engine/must`，不是可避免 | 1 条仍成立（证据升级）/ 1 条改判 |
| `ZZZGachaInfoWindow.xaml.cs`：`Launcher.LaunchUriAsync` → `OpenUrl.Open` | 仍成立（api/reasonable），返回值被丢弃 | 仍成立 |
| 导出 `export_app`（`GachaLogExportFile` / `UIGF3File` / `UIGF4File` / `ZZZGachaService`） | 4 处全部仍在（39 / 39 / 77 / 550 行） | 4 条仍成立 |
| （旧账未记）`UIGFGachaService.cs` 的 `using Vanara.PInvoke` 删除 | 新发现，零行为差异 | 1 条新增 |

##### 附 · 未核实项汇总

1. **`PageBase` 与 D94 第 1 条不一致**（`Features/PageBase.cs:43-48` 仍在首次 `Unloaded` 摘掉 `Loaded` 钩子，`OnLoaded()` 只跑一次）。按代码推断：本组两个缓存页（`GachaLogPage`、`GameRecordPage`）在离开过一次之后，`OnLoaded` 里的 `WeakReferenceMessenger` 注册不会再恢复（`GameRecordPage.xaml.cs:56` 的 `HypThirdPartyLoginMessage` 就在其中）——**未实机验证**。`OnGameChanged` 由 `OnNavigatedTo` 驱动，不受影响，这也是为什么 B48/B51 的补偿能生效。此条跨组（`PageBase` 不属 3.6），建议并表时升为模块级遗留问题。
2. `BBSWebBridge.xaml` / `BattleChronicleWindow.xaml` / `LoginPage.xaml` 这三个上游 XAML 在本工程里到底是否参与编译：工程设了 `EnableDefaultPageItems=false` 且没有 `Page Remove`，而它们的 code-behind 已被排除、XAML 里又引用了 shim 不存在的成员（`CurrentGameBiz`/`GameRecordRole`/`RootGrid_Loaded`）。本组不构建，**未核实**编译侧如何配对；若参与编译则该页必编不过，因此更可能是「XAML 随 code-behind 一起被排除」的隐式规则。
3. `LoginPage.xaml` 里 `Visibility="Collapsed"` 的 `<WebView2 Name="webview">` 是否仍会实例化 WebKit 后端（占内存/进程）：未测。
4. `BBSWebBridge.IsLoggedIn` 的「全局 HasCookies」判定在多区/多账号下是否误报：无调用方，暂不影响行为，未核实。
5. `GenshinBeyondGachaPage`（code-behind 与上游 0 差异）在缓存复用后切游戏，内容是否完整刷新：未实机验证。

### 3.7 背景与图片加载（Background / Codec / CachedImage）

#### `Controls/CachedImage.Linux.cs`

- **第 16 行（类声明 `public sealed partial class CachedImage : Image`）**｜engine 引擎能力｜reasonable
  - 上游：`src/Starward/Controls/CachedImage.cs:15` `public sealed partial class CachedImage : ImageEx`（Scighost.WinUI.ImageEx），加载逻辑写在 `ProvideCachedResourceAsync(Uri, CancellationToken)`（:41-97）里，由 ImageEx 负责过渡淡入、`PlaceholderSource`、失败重试。
  - 移植：继承 `Image`，自己实现 `OnSourceChanged`（第 114-143 行）：取消上一个 CTS → 异步加载 → 成功后直接 `base.Source = image` 并触发 `ImageOpened`/`ImageExOpened`。
  - 理由：ImageEx 依赖 WinUI Composition / `CompositionBrush`，Uno 的 Skia 后端没有这套实现；换基类后**没有交叉淡入与占位图**（纯观感损失，不影响功能）。

- **第 25-32 行（新增 `Background` 依赖属性）**｜engine 引擎能力｜reasonable
  - 上游：`ImageEx` 继承自 `Control`，自带 `Control.Background`；`src/Starward/Features/Background/BackgroundViewPage.xaml:169` 用 `Background="{ThemeResource ControlAltFillColorTertiaryBrush}"` 给还没加载出来的缩略图占位块上色。
  - 移植：`Image` 没有 `Background`，这里补一个同名 DP，`get/set` 只走 `GetValue/SetValue`，**从不绘制**（注释自述 L-183）。
  - 理由：0.19.0 上游新增了这个用法，不补 DP 会 XAML 编译失败；补了但不渲染 → 缩略图未加载出来的那一瞬是透明而不是灰底。要真画出来得改成 `Grid{Image, Border}` 组合，或给 `Image` 叠一层 `Background` 画刷。

- **第 149-152 行（裸绝对路径优先分支）**｜api 平台 API｜must
  - 上游：`Source` 一律当 `Uri` 处理（`ProvideCachedResourceAsync(Uri imageUri, ...)`，`:45-82` 分 ms-appx / file / http 三支）。
  - 移植：先判 `value is string direct && Path.IsPathRooted(direct) && File.Exists(direct)` → 直接读文件系统字节；不是才退回 Uri 分支。
  - 理由：`{x:Bind FilePath}` 传进来的是 `/home/x/a.png` 这种裸路径，Uno 下 `new Uri` 出来既不是 `ms-appx` 也不是 `file` scheme，`BitmapImage` 静默不显示（L-74）。

- **第 18 行 + 第 174-183 行（http 分支）**｜engine 引擎能力｜avoidable
  - 上游：`:69` `var file = await FileCache.GetFromCacheAsync(imageUri.ToString(), false, token);` —— 命中磁盘缓存就不下载，解码异常时 `FileCache.DeleteCacheFile`（:94 与 `BitmapImage_ImageFailed` :117）。
  - 移植：`_http.GetByteArrayAsync(uri, token)` 每次全量下到内存；静态 `HttpClient`（第 18 行，30s 超时）无缓存目录、无失败清理、无重试。
  - 理由：`Helpers/FileCache.cs` 在移植侧**仍然存在且未被排除编译**（`GetFromCacheAsync` 在第 73 行），完全可以直接复用；现在的写法每进一次页面就重下网络图，断网即白块。

- **第 64-76 行（`UpdateClip` 只做矩形裁剪）**｜cosmetic 外观文案｜avoidable
  - 上游：`ImageEx` + `ui:UIElementExtensions.ClipToBounds` / `CornerRadius` 由合成器做圆角裁剪。
  - 移植：`var cr = CornerRadius;` 取到之后**没有任何使用**，`Clip = new RectangleGeometry{ Rect = (0,0,ActualWidth,ActualHeight) }` 是直角矩形。
  - 理由：注释自述「Uno 的 RectangleGeometry 不支持圆角，圆角在渲染层由合成器处理（M3）」——目前既没有合成器圆角，也没退化方案（例如外层 `Border` + `CornerRadius` 承载），所以图库里所有缩略图的圆角被切成直角。

- **第 255-278 行（`IsThumbnail` 缩略图通道）**｜engine 引擎能力｜reasonable
  - 上游：`:51-60` 视频走 `ImageThumbnail.GetVideoThumbnailAsync`（MediaPlayer + `SoftwareBitmap` 取首帧），图片走 `ImageThumbnail.GetImageThumbnailAsync`（`BitmapDecoder` 缩略图 + 磁盘 png 缓存）。
  - 移植：扩展名命中视频（`.mp4/.mkv/.webm/.mov/.avi`）→ `Starward.Features.Background.VideoThumbnailer.EnsureAsync(path, token)`（libvlc `--vout=vmem` 抽首帧）；图片 → `DecodeScaledPng`（第 189-216 行，`SKCodec.GetScaledDimensions` 定点缩放解码后重编码 PNG(80)）。
  - 理由：WinRT `BitmapDecoder` / `MediaPlayer` 在 Uno/Skia 下不可用。**旧总账「`IsThumbnail`/`PngThumbnail` 只是声明、并不生效」那条自此作废**——现在两条属性都真的接了实现。

- **第 101-109 行（新增 `DecodePixelWidth` 依赖属性）**｜engine 引擎能力｜reasonable
  - 上游：无此属性，缩略图尺寸由 `ImageThumbnail` 内部固定，XAML 只传 `IsThumbnail/PngThumbnail`。
  - 移植：新增 `int` DP，`BackgroundViewPage.xaml:162` 传 400；`PropertyMetadata` 回调里重新触发 `OnSourceChanged(Source)`（改值即重新解码）；非缩略图分支用 `DownscaleToStream`（第 220-250 行，`SKCubicResampler.Mitchell` + mipmap）。
  - 理由：上游「一个控件一种缩略图尺寸」，移植要同时服务图库（400 DIP）和预览（原图按需），所以把解码宽度提到 XAML 层（D56 / B21）。

- **第 300-302 行（空的 `private static class StreamExtensions { }`）**｜eng 工程性｜avoidable
  - 上游：无。
  - 移植：空类，没有任何方法、没有任何引用（`AsRandomAccessStream` 已经直接用 `System.IO` 的扩展）。
  - 理由：遗留占位，应删除；留着会让静态规则与阅读都以为这里还有扩展方法。

---

#### `Controls/CachedImage.cs`

**无偏离。** 该文件在移植侧与 `src/Starward/Controls/CachedImage.cs` **逐字节相同**（120 行），且被 `src/Lunarward/Lunarward.csproj:98` `<Compile Remove="Controls/CachedImage.cs" />` 排除编译，实际参与编译的是同目录的 `Controls/CachedImage.Linux.cs`。保留副本仅为与上游对照（也方便上游改动时看出 API 漂移）。逐点偏离全部记在上面的 `.Linux.cs` 小节里。

---

#### `Features/Background/AppBackground.Linux.cs`

- **第 148-171 行（ctor 里 `Content = new Grid{...}`，不调 `InitializeComponent`）**｜engine 引擎能力｜must
  - 上游：`src/Starward/Features/Background/AppBackground.xaml.cs:46-52` 构造里 `this.InitializeComponent()`；视觉树来自 `AppBackground.xaml:17-78`——两个 `swi:ImageEx`（`BackgroundImageSource` / `ThemeImageUrl`）+ 三块渐变 Rectangle + ProgressBar。
  - 移植：手搭 `Content = new Grid{ Background = SolidColorBrush(#FF16161A), Children = { _image, _videoImage } }`（:166-170），两个子元素是 `new Image{ Stretch = UniformToFill }`（:101-119）。
  - 理由：`ImageEx`（Scighost.WinUI）与 Win2D 在 Uno 7 下无可用资产；主题文字层改成 Skia 预合成（`ComposeBackgroundPng`，:354-373）成一张 PNG 再交给 `Image`，所以视觉树必须自己搭。

- **第 166-170 行（手搭树里没有渐变遮罩，也没有 ProgressBar）**｜cosmetic 外观文案｜avoidable
  - 上游：`AppBackground.xaml:34-68` 三块 `LinearGradientBrush`（右上 400×400 `0→#50000000`、右下 400×400、右侧 200 宽 `0→#30000000`）给右侧文字区做压暗；`:71-75` `ProgressBar` 绑 `IsUpdateBackgroundRunning`。
  - 移植：只有纯色底 + 两张 `Image`；`IsUpdateBackgroundRunning`（:413-423）仍然在维护并参与守卫，但**没有任何 UI 消费它**。
  - 理由：这两块都是纯 XAML，与引擎能力无关，属可补回的外观差异。现在：浅色壁纸时右侧侧栏文字对比度偏低；换游戏/取背景期间没有任何等待提示（用户以为卡住）。

- **第 34 / 52 / 311 / 582 / 991-992 行（五个 `LUNARWARD_*` 环境变量）**｜linux 专属实现｜avoidable
  - 上游：无对应开关（帧率、超时、视频源都是常量或接口返回值）。
  - 移植：`LUNARWARD_VIDEO_FPS`（:34）、`LUNARWARD_VIDEO_NOPAINT`（:52）、`LUNARWARD_BG_TIMEOUT_MS`（:311）、`LUNARWARD_VIDEO_BG`（:582 与 :991）、`LUNARWARD_VIDEO_OVERLAY`（:992）全部用 `Environment.GetEnvironmentVariable` **直读**，没有 `#if DEBUG` 也没有 `DevSwitches` 判断。
  - 理由：硬规则 6 / D35 要求调试入口不得进发行版。其中 `LUNARWARD_VIDEO_BG` 能让 Release 版去播任意本地视频文件（还能和 `LUNARWARD_VIDEO_OVERLAY` 叠加任意图片），等于给发行版留了行为注入点。另见 `:151` `LUNARWARD_UI_WATCHDOG`（看门狗，同样裸读）。

- **第 58-62 行（`Current` 惰性 getter）**｜eng 工程性｜avoidable
  - 上游：`AppBackground.xaml.cs:38` `public static AppBackground Current { get; private set; }`——只在 ctor 里 `Current = this` 赋一次。
  - 移植：`get => _current ??= new AppBackground();`——任何消费者第一次访问都会**顺手造一个实例**（`BackgroundSwitcher.xaml.cs:30` 订阅 `PropertyChanged` 就是这条路）。
  - 理由：`new AppBackground()` 会执行 `UiWatchdog.Start`（:152）、注册两类 Messenger 消息（:155/:158）、创建 `_videoImage`；如果发生在窗口尚未创建/非 UI 线程，就是一个永远挂不进视觉树、也不会 Unloaded 的泄漏实例。正常路径下 `MainView.xaml:19` 已先实例化，所以是「埋雷」而非现网可见故障（未核实非 UI 线程路径是否会抛）。

- **第 298-352 行（`GetSuggestedBackgroundCachedAsync`：5min/30s 缓存 + 4s/8s 两轮退避重试）**｜linux 专属实现｜reasonable
  - 上游：`AppBackground.xaml.cs:184` `new CancellationTokenSource(1000)` 硬超时；超时后 :206 先 `GetLastAppBackgroundFile` 铺图，再 :210 用完整 token 重试一次。
  - 移植：`SuggestedBackgroundTtl = 5min` / `SuggestedBackgroundFailureTtl = 30s`（:72-75）缓存「候选列表 + 选中项」；单次超时默认 4000ms（`LUNARWARD_BG_TIMEOUT_MS` 可改），按 `attempts = {t, 2t}` 退避重试。
  - 理由：Linux 首包（TLS 握手 + DNS）常 5–6 秒（L-71），1 秒必然超时，用户每次进启动器都会看到兜底图跳变；缓存还避免 `GameLauncherPage` 每次 Loaded 都重拉接口导致的「主题强调色闪一下」。代价：换游戏后最多 5 分钟内仍用旧列表（`BackgroundChangedMessage` 会清缓存，见 :192-213）。

- **第 540-546 行（早退守卫）+ 第 549-566 行（先用磁盘图铺画面）**｜eng 工程性｜reasonable
  - 上游：每次 `UpdateBackgroundAsync` 都先 `GameBackgrounds = null; CurrentGameBackground = null; ThemeImageUrl = null;`（:172-174）再从头拉接口——切页时画面先黑一下、正在播的视频被停掉。
  - 移植：`requested is null && !_backgroundDirty && !customBgChanged && gameId == _appliedGameId && (BackgroundImageSource is not null || _videoActive)` 直接 return；首次进入时先 `GetLastAppBackgroundFile` / `GetFallbackBackgroundImage` 铺图（:549-566）再走网络。
  - 理由：切页/换窗口不应该把正在播的视频壁纸停掉重来；`_backgroundDirty`（B22）保证换自定义背景能穿透守卫。代价：上游「每次清空列表再拉」的语义没了，接口失败时会保留旧列表（切换器显示上一个游戏的背景），需在失败路径上补提示。

- **第 981-1266 行（视频壁纸全链路：`TryStartVideoBackgroundAsync` → `MeasureFrameInterval` → `OnVideoFrameArrived` → `PresentVideoFrame`）**｜engine 引擎能力｜must
  - 上游：`MediaPlayer` + `CanvasImageSource/CanvasRenderTarget`（Win2D）；`MediaPlayer_VideoFrameAvailable`（:559）里 `CopyFrameToVideoSurface`；`_videoSemaphore`（:431）串行化拷帧；音量直接用 `_mediaPlayer.Volume`；起不来时 `MediaPlayer_MediaFailed`（:522）弹 toast，并在 :771 建议安装 VP9 扩展（`_needToInstallVp9VideoExtension`，:519）。
  - 移植：`VideoBackgroundPlayer`（libvlc，`--vout=vmem` 回调）→ `SKBitmap` 前后台双缓冲 → UI 线程 `WriteableBitmap` A/B 交替赋给 `_videoImage.Source`（:1220-1266）；帧率自适应 `MeasureFrameInterval`（:1080-1123，16 帧环形窗口均值×0.8，夹在 10–240fps）；`_paintQueued` + `Interlocked.Exchange` 合并重绘（:1163-1175）；`VideoBgVolume`（:479-497）只写 `AppConfig.VideoBgVolume`，播放器恒 `--no-audio`；起播失败或 5 秒无帧只记 Warning（:1354-1373），没有 VP9/解码器诊断。
  - 理由：Uno 7 不再提供 SkiaSharp.Views 运行时程序集（UNOB0023，L-184），SKXamlCanvas 直画路线被推翻后回到 `WriteableBitmap` 双缓冲——**旧总账里「SKXamlCanvas + SKPaint 画帧」的描述已作废**。MediaPlayer/Win2D 在 Linux 无实现，只能 libvlc。音量不生效是已知裁剪（设置页有音量滑块，需与文档对齐）。

- **第 939-963 行（`SaveCurrentBackgroundAsync`）**｜api 平台 API｜reasonable
  - 上游：`:733-757` `FileDialogHelper.OpenSaveFileDialogAsync` → `File.Copy` → `StorageFile.GetFileFromPathAsync` + `FolderLauncherOptions.ItemsToSelect` + `Launcher.LaunchFolderAsync`（保存完在文件管理器里**选中**刚保存的文件）。
  - 移植：同样的保存对话框 + `File.Copy`，之后只 `InAppToast.MainWindow?.Information(savePath)` 报一下路径。
  - 理由：Linux 没有「打开文件夹并选中某个文件」的通用接口（只能 `xdg-open <dir>`）。

---

#### `Features/Background/BackgroundFileItem.cs`

- **第 7 行（`public partial class BackgroundFileItem : ObservableObject`）**｜eng 工程性｜reasonable
  - 上游：`src/Starward/Features/Background/BackgroundFileItem.cs:6` `public class BackgroundFileItem`（普通类，无通知）。
  - 移植：继承 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject`，成为 `partial`（配合 `field` 关键字 + `SetProperty`）。
  - 理由：视频首帧是**异步**回填的，必须能通知 UI；上游靠 `CachedImage` 内部异步加载，所以不需要模型通知。

- **第 21-31 行（新增 `ThumbnailPath`，用 `field` 关键字）**｜linux 专属实现｜reasonable
  - 上游：无此属性（`FilePath/FileInfo/FileSize/CreationTime/IsVideo` 五个属性 + ctor，:`9-28`）。
  - 移植：`public string? ThumbnailPath { get => field; set { if (SetProperty(ref field, value)) OnPropertyChanged(nameof(DisplayPath)); } }`。
  - 理由：视频文件不能直接当图片源（L-98），首帧 png 由 `VideoThumbnailer` 异步抽出来落盘后再回填（`BackgroundViewPage.xaml.cs:70-93`），需要属性级通知才能刷新绑定。`field` 关键字需 C# 13 / .NET 11，属工程前提而非平台差异。

- **第 35 行（`public string DisplayPath => ThumbnailPath ?? FilePath;`）**｜engine 引擎能力｜reasonable
  - 上游：无；XAML 直接 `Source="{x:Bind FilePath}"`（`BackgroundViewPage.xaml:173`）。
  - 移植：`BackgroundViewPage.xaml:170` 改绑 `Source="{x:Bind DisplayPath, Mode=OneWay}"`，视频条目在有首帧时加载 png、没有时退回原视频路径（再由 `CachedImage` 的 `IsThumbnail` 通道兜底）。
  - 理由：一条绑定同时覆盖「图片条目」「已抽帧的视频条目」「未抽帧的视频条目」，避免在 XAML 里堆 `DataTrigger`。

---

#### `Features/Background/BackgroundService.cs`

- **第 203-210 行（`GetFallbackBackgroundImage` 用 `Path.Combine`）**｜linux 专属实现｜must
  - 上游：`src/Starward/Features/Background/BackgroundService.cs:209` `Path.Combine(baseFolder, @"Assets\Image\UI_CutScene_1130320101A.png")`（字面量反斜杠）。
  - 移植：`Path.Combine(baseFolder, "Assets", "Image", "UI_CutScene_1130320101A.png")`。
  - 理由：Linux 上 `\` 不是分隔符，上游写法 `File.Exists` 恒 false，兜底壁纸永远加载不出来（这句是移植注释原文，实测成立）。

- **第 248-266 行（`ChangeCustomBackgroundFileAsync(string? file)` 取代上游的 `(StorageFile file)` 重载）**｜api 平台 API｜reasonable
  - 上游：`:249-283` `public static async Task<string?> ChangeCustomBackgroundFileAsync(StorageFile file)`——用 `MediaSource.CreateFromStorageFile` / `BitmapDecoder` 校验后 `File.OpenWrite` + `stream.AsStream().CopyToAsync` 落盘。
  - 移植：收本地路径 `string?`（调用方 `GameLauncherPage.xaml.cs:1301` 传 `file.Path`），`File.Copy(file, path, true)` 落盘。
  - 理由：Uno/Skia 没有可用的 WinRT `StorageFile` 打开流（拖拽拿到的 `StorageFile` 只有 Path 有意义）。`string` 重载与上游 `(XamlRoot)` 重载并存，功能对齐。

- **第 298-322 行（`CheckBackgroundFileAvailableAsync` 整套重写）**｜engine 引擎能力｜must
  - 上游：`:305-321` 视频 `MediaSource.CreateFromUri(new Uri(file)).OpenAsync()`，图片 `BitmapDecoder.CreateAsync(File.OpenRead(file).AsRandomAccessStream())`——**不做存在性/大小检查**，靠解码器抛。
  - 移植：`Task.Run` 里 `File.Exists`（否则 `FileNotFoundException`）、`FileInfo.Length <= 0`（否则 `InvalidDataException("background file is empty")`），图片再 `ImageLoader.GetImagePixelSizeAsync` 取宽高，`width == 0 || height == 0` 抛 `InvalidDataException("background image cannot be decoded")`（注释标 B54）。
  - 理由：`BitmapDecoder` / `MediaSource` 不可用，只能用 Skia 解；额外补的存在性与空文件检查顺带修掉了上游「0 字节文件被认为合法、壁纸变黑」的问题（可视为 bugfix，但主语是平台替换）。

- **第 312-320 行（视频分支只按扩展名放行，不再真正打开）**｜cut 裁剪｜avoidable
  - 上游：视频也是真校验——`MediaSource.OpenAsync()` 打不开就抛（注释里写着 0xC00D36C4 这个具体错误码）。
  - 移植：`extension is ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi"` 直接跳过校验，坏视频会被照常复制进 `bg/` 并持久化。
  - 理由：libvlc 打开一个文件等于起一个播放器实例，代价与线程都不适合放在「选文件」路径上；但可以至少做容器头魔数/probe 校验（`libvlc_media_parse` 有同步解析接口），所以记为可避免。

- **第 315 行（`GetImagePixelSizeAsync(file).GetAwaiter().GetResult()` 嵌套同步等待）**｜eng 工程性｜avoidable
  - 上游：`await BitmapDecoder.CreateAsync(...)`——纯异步。
  - 移植：外层已经是 `Task.Run`（:300），内层又 `ImageLoader.GetImagePixelSizeAsync` 再 `Task.Run` 一次（`ImageLoader.Linux.cs:13`），然后用 `GetAwaiter().GetResult()` 同步等。
  - 理由：一次选背景要占两个线程池线程，且同步等待会吃掉外层线程；把它写成普通同步方法（直接 `SKCodec.Create` + `MagickCodec.TryGetPixelSize`）就能去掉一层。

- **第 256-259 行（校验放在「是否已在 bg 目录」判断之前）**｜eng 工程性｜avoidable
  - 上游：`:250-254` 先 `if (Path.GetDirectoryName(file.Path) != bg)`，只有需要复制时才走 `MediaSource`/`BitmapDecoder` 校验；已在 `bg/` 里的文件直接返回文件名。
  - 移植：先在 :258 `await CheckBackgroundFileAvailableAsync(file)`，再在 :259 比较目录。
  - 理由：用户把 `bg/` 里已经存在的（损坏/空的）文件重新拖进来，上游会静默成功，移植会抛异常（还原路径可以避免，但会重新引入上游那个「坏文件被接受」的问题——属于取舍，需明示）。另外 :259 用 `Path.GetFullPath` 两端比较，比上游裸字符串比较更稳（无害）。

---

#### `Features/Background/BackgroundViewPage.xaml`

- **第 135-155 行（`GridView` → `ListView` + `tk:WrapPanel`）**｜engine 引擎能力｜must
  - 上游：`src/Starward/Features/Background/BackgroundViewPage.xaml:134` `<GridView x:Name="GridView_Images" ...>`，面板是 `:152-155` `<ItemsWrapGrid AreStickyGroupHeadersEnabled="False" ItemHeight="136" ItemWidth="228" Orientation="Horizontal" />`，容器样式 `DefaultGridViewItemStyle`（:145-149）。
  - 移植：`<ListView x:Name="GridView_Images" ...>`（控件名保留，code-behind 不改名）+ `:145-150` `DefaultListViewItemStyle` + `:151-155` `<tk:WrapPanel HorizontalSpacing="12" VerticalSpacing="12" />`。
  - 理由：Uno 的 `GridView`/`ItemsWrapGrid` 不走真实排版面板（旧总账已记），只能换 ListView + CommunityToolkit WrapPanel。代价有两处留痕：①item 尺寸没了 `ItemHeight/ItemWidth`，改由 code-behind 算（`BackgroundViewPage.xaml.cs:144-176`）；②`AreStickyGroupHeadersEnabled` 随 GridView 一起消失。

- **第 161-171 行（缩略图 `CachedImage`：尺寸与数据源都改了）**｜engine 引擎能力｜reasonable
  - 上游：`:164-165` 固定 `Width="216" Height="122"`、`HorizontalAlignment="Center" VerticalAlignment="Center"`，`:173` `Source="{x:Bind FilePath}"`，没有 `DecodePixelWidth`。
  - 移植：去掉固定宽高，改 `HorizontalAlignment/VerticalAlignment="Stretch"`、`DecodePixelWidth="400"`、`Source="{x:Bind DisplayPath, Mode=OneWay}"`；实际宽高由 `ApplyTileSize`（:98-111）写到 `ListViewItem` 上，`WrapPanel` 负责换行。
  - 理由：WrapPanel 没有 ItemWidth/ItemHeight（L-179），只能由容器承载尺寸；216 DIP 固定宽度在 1200 DIP 窗口右侧永远空 204 DIP（D56）。**副作用**：`CachedImage` 自身不再有确定尺寸，若某处漏掉 `ApplyTileSize`，`Image` 会按字数/自然尺寸撑开，需要用截图确认（未核实）。

- **第 166 行（`Background="{ThemeResource ControlAltFillColorTertiaryBrush}"`）**｜engine 引擎能力｜reasonable
  - 上游：`:169` 同一行，`CachedImage : ImageEx` 继承自 `Control`，`Background` 天然存在。
  - 移植：XAML 原样保留，但 `CachedImage : Image` 没有 `Background`，靠 `Controls/CachedImage.Linux.cs:25-32` 补的 DP 吃掉这个值（只存不画）。
  - 理由：0.19.0 上游新增的在 `CachedImage` 上设底色的用法；不补 DP 就编译不过。见上面 `CachedImage.Linux.cs` 第 2 条。

---

#### `Features/Background/BackgroundViewPage.xaml.cs`

- **第 30-40 行挂接 + 第 98-176 行（`ApplyTileSize` / `OnImageContainerChanging` / `OnPageSizeChanged` / `UpdateTileSize`）**｜engine 引擎能力｜reasonable
  - 上游：无这套代码（`:30-33` 构造里只有 `InitializeComponent()`），条目尺寸由 `ItemsWrapGrid` 的 `ItemWidth=228/ItemHeight=136` 决定。
  - 移植：ctor 里 `SizeChanged += OnPageSizeChanged`（:37）、`GridView_Images.ContainerContentChanging += OnImageContainerChanging`（:40）；`UpdateTileSize`（:144-176）按可用宽度 `(avail + gap) / (minTile + gap)` 反推列数、`tile = (avail - gap*(cols-1))/cols`，高度按 `122/216` 保持比例，写到已实体化容器（:98-111），新实体化条目在 `ContainerContentChanging` 里补（:115-122）。
  - 理由：CommunityToolkit（Uno 版）`WrapPanel` 只有 `Spacing`，没有 `ItemWidth/ItemHeight`（L-179）；写容器属性是实现「网格铺满」的唯一途径。列数不变时不触发重排（:163-166 的 0.5 DIP 阈值），否则滚动会抖。

- **第 70-93 行（`GenerateVideoThumbnailsAsync`）**｜engine 引擎能力｜reasonable
  - 上游：无；视频缩略图在 `CachedImage`→`ImageThumbnail.GetVideoThumbnailAsync` 内部按需生成。
  - 移植：`LoadBackgroundItems` 拿到列表后 `_ = GenerateVideoThumbnailsAsync(BackgroundImages)`（:226），逐个 `VideoThumbnailer.EnsureAsync(item.FilePath)`，成功后在 `DispatcherQueue.TryEnqueue` 里回填 `item.ThumbnailPath`。
  - 理由：`MediaPlayer` 不可用，改用 libvlc 抽帧；抽帧是异步且要落盘（`VideoThumbnailer` 内部信号量限并发），所以放到列表加载之后批量跑，而不是绑定时逐个触发（绑定时触发会把 20 个视频同时塞进播放器）。注意 `foreach` 是串行 `await`，列表大时首屏会分批出现缩略图（无进度提示，见 `AppBackground.Linux.cs` 第 2 条）。

- **第 250-255 行 / 第 313 行 / 第 382 行（三处 `Launcher` → `Helpers.OpenUrl.Open`）**｜api 平台 API｜must
  - 上游：`:127` `await Launcher.LaunchFolderPathAsync(folder)`；`:186` `await Launcher.LaunchUriAsync(new(item.FilePath))`（单击）；`:255` 同样的 `LaunchUriAsync`（双击打开文件）。
  - 移植：`if (!Helpers.OpenUrl.Open(folder)) _logger.LogWarning(...)`（:251）与 `Starward.Helpers.OpenUrl.Open(item.FilePath)`（:313、:382）。
  - 理由：Uno 的 `Launcher` 在 Linux 上是空实现（L-74），必须走 `xdg-open`；顺带补了「没有处理程序」的告警。

- **第 429-440 行（打开所在文件夹并选中文件 → 只打开目录）**｜api 平台 API｜must
  - 上游：`:306-309` `StorageFile.GetFileFromPathAsync` + `new FolderLauncherOptions()` + `options.ItemsToSelect.Add(file)` + `Launcher.LaunchFolderAsync(await file.GetParentAsync(), options)`。
  - 移植：`Path.GetDirectoryName(item.FilePath)` + `Helpers.OpenUrl.Open(folder)`，失败记 `LogWarning`。
  - 理由：Linux 文件管理器没有「打开目录并选中某文件」的通用接口（部分 DE 有 `org.freedesktop.FileManager1.ShowItems`，移植未接，见「未核实」）。

- **第 453-461 行 / 第 475-484 行（默认打开 / 选择打开方式）**｜api 平台 API｜must
  - 上游：`:328-329` `StorageFile.GetFileFromPathAsync` + `Launcher.LaunchFileAsync(file)`；`:348-350` `new LauncherOptions { DisplayApplicationPicker = true }` + `LaunchFileAsync(file, options)`（弹出「用什么打开」）。
  - 移植：两处都退化成 `Helpers.OpenUrl.Open(item.FilePath)`，「选择打开方式」语义丢失。
  - 理由：Uno 无 `LauncherOptions.DisplayApplicationPicker`；Linux 下「选择打开方式」由文件管理器/桌面环境负责。可考虑 `.desktop` 的 MimeType 关联设置页，但工作量远大于本页（记为可避免的交互缺口）。

---

#### `Features/Background/BackgroundViewWindow.xaml.cs`

- **第 30-31 行（`Title` + `AppWindow.Title` 字面量，不再 `ExtendsContentIntoTitleBar`）**｜linux 专属实现｜must
  - 上游：`:32-33` `Title = "Starward - Background Viewer";` + `AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;`（自绘标题栏，XAML 里有 `Grid` 第一行当拖拽区）。
  - 移植：`Title` 与 `AppWindow.Title` 都设同一个字符串（X11 的 WM caption 走 `AppWindow.Title`），不设 `ExtendsContentIntoTitleBar`；此外 `WindowBase.Show()`（`Features/WindowBase.Linux.cs:35`）还会**主动**把它设回 `false`。
  - 理由：Uno 的 X11 后端没有「内容区当标题栏 + 拖拽区」实现，开了之后既没有 WM 装饰也没有按钮 → 窗口拖不动、关不掉（L-84 / D49）。XAML 里那行标题栏 Grid 于是成了空白条（外观差异）。

- **第 47-51 行（覆盖 `CenterInScreen`，只设尺寸、不定位置）**｜linux 专属实现｜must
  - 上游：`:49-61` `User32.GetCursorPos` 取光标 → `DisplayArea.GetFromPoint` 找所在显示器 → 按 `UIScale` 换算 → `AppWindow.MoveAndResize(x, y, w, h)` 精确居中。
  - 移植：`base.CenterInScreen(width ?? 1200, height ?? 676)`；基类（`WindowBase.Linux.cs:148-160`）只做 `Resize`（DIP×UIScale）+ 原地 `MoveAndResize`，位置交给 WM。
  - 理由：Uno X11 下 `MoveAndResize` 是空操作（L-35），也没有跨显示器的坐标系 API；能做的只有设尺寸、然后靠 WM 的 placement。

- **第 55 行（`DefaultWindowSizeInDips => (1200, 676)`）**｜linux 专属实现｜reasonable
  - 上游：无此属性；窗口尺寸由 `Show()` 从主窗口复制或由 `CenterInScreen(1200, 676)` 设。
  - 移植：新增该属性，`WindowBase.ClampToScreenAsync`（`WindowBase.Linux.cs:88`）在窗口 map 出来之后按 `DIP × UIScale` 补一次 `AppWindow.Resize`，并把超屏尺寸夹回 90% 屏幕。
  - 理由：Uno 在 map 之后才应用默认尺寸，直接 `Resize` 会被覆盖（L-35）；这个钩子同时也是「大窗口在小屏上会被夹回」的唯一实现点。

- **第 59-64 行（`Show()` 只调 `base.Show()`）**｜linux 专属实现｜must
  - 上游：`:65-77` `AppWindow.GetFromWindowId(MainWindowId)` 拿到主窗口 → `MoveAndResize` 到与主窗口完全相同的 `Position/Size`（背景查看器跟随主窗口），拿不到才 `CenterInScreen(1200, 676)`。
  - 移植：不复制主窗口几何（无法 `MoveAndResize`），全部交给 `base.Show()`；基类会做 X11 `map`（`Helpers.X11Window.Show`）、`Activate`、`ClampToScreenInBackground`，并在 map 成功后补发 `MainWindowStateChangedMessage{ Activate = true }`。
  - 理由：X11 下位置由 WM 决定（同上）；补发 Activate 消息是必需的——否则打开查看器时视频壁纸不会被当作「窗口重新激活」而恢复播放（旧总账只记了居中/尺寸，这条副作用是新增的）。

---

#### `Features/Codec/ImageLoader.Linux.cs`

- **整文件（仅 30 行，只剩一个成员）**｜cut 裁剪｜must
  - 上游：`src/Starward/Features/Codec/ImageLoader.cs`（`public static partial class ImageLoader`）还有 `DecodeImageAsync`、`SaveBitmapAsync`、HDR/AVIF/JXL 相关入口、`GetImagePixelSizeAsync` 等多个成员，全部基于 Win2D / `BitmapDecoder` / `Starward.Codec`。
  - 移植：`public static partial class ImageLoader` 里**只有** `GetImagePixelSizeAsync`（第 11 行），其余成员一个都没移植；上游那份 `ImageLoader.cs` 仍在 `src/Lunarward/Features/Codec/` 目录下，但被 `Lunarward.csproj:104` 排除编译。
  - 理由：Win2D 在 Uno/Skia 下无资产；而且移植侧真正用到的只有「取像素尺寸」这一处（`BackgroundService.cs:315` 用来判断「能不能解码」）。**注意**：如果以后有页面用到 `ImageLoader` 的其它成员，会直接编译失败——正好当作缺口哨兵。

- **第 11-29 行（`GetImagePixelSizeAsync` 用 `SKCodec`，失败再退 `MagickCodec`）**｜engine 引擎能力｜reasonable
  - 上游：同名方法内部走 `BitmapDecoder` + `GetPixelDataAsync`（拿当前帧的像素尺寸）。
  - 移植：`Task.Run` 里 `File.OpenRead` + `SKCodec.Create(stream)` 取 `codec.Info`；`codec is null` 时走 `Starward.Helpers.MagickCodec.TryGetPixelSize(filePath, out w, out h)`（AVIF/JXL/HEIC），都不行返回 `(0u, 0u)`。
  - 理由：Skia 不带 AVIF/JXL delegate，Magick.NET 带（24 号 P4）。返回值 `(0,0)` 被 `BackgroundService` 当作「解不开」处理，所以「未识别格式」与「坏文件」在这条链路上无法区分（调用方拿到的异常文案都是 `background image cannot be decoded`）。

---

#### `Features/Codec/ImageThumbnail.cs`

- **第 26 行（整文件 `internal static class ImageThumbnail` 被排除编译）**｜cut 裁剪｜must
  - 上游：`src/Starward/Features/Codec/ImageThumbnail.cs` 是缩略图能力的唯一入口（`GetImageThumbnailAsync`、`GetVideoThumbnailAsync`、`LoadAvifImageAsync`、`LoadJxlImageAsync`、`ClearThumbnailCache`），调用方是上游 `Controls/CachedImage.cs:55/59`、`ImageLoader.cs:349/354`、`ScreenshotSetting.xaml.cs:433`。
  - 移植：`Lunarward.csproj:106` `<Compile Remove="Features/Codec/ImageThumbnail.cs" />`，文件留在仓库里但不参与编译；缩略图能力**分流到三处**：视频首帧 → 新增的 `Features/Background/VideoThumbnailer.cs`（`EnsureAsync`，落 `AppConfig.CacheDirectory/bg-thumbs`）、图片缩放 → `Controls/CachedImage.Linux.cs` 的 `DecodeScaledPng`、AVIF/JXL → `Helpers/MagickCodec.cs`。全仓已无活跃调用点（剩下三处调用方自己也被排除编译）。
  - 理由：`BitmapDecoder.CreateAsync` / `SoftwareBitmapSource` / `IMemoryBufferByteAccess` / MediaPlayer 在 Uno 下都不存在，整条链路无法编译。**旧总账「IsThumbnail 只是声明、没有缩略图实现」的结论需要重写**：能力没丢，只是搬到了别处。

- **第 31 行（唯一的实质改动：`CacheFolder` 改 `AppConfig.CacheDirectory` 并改成只读属性）**｜eng 工程性｜avoidable
  - 上游：`:31`（`src/Starward/Features/Codec/ImageThumbnail.cs`）`private static readonly string CacheFolder = Path.Combine(AppConfig.CacheFolder, "thumb");`（静态字段，值在类型初始化时算，写在数据目录下的 `thumb/`）。
  - 移植：`private static string CacheFolder => Path.Combine(AppConfig.CacheDirectory, "thumb");`（计算属性，改到纯缓存目录，跟 D57 的「缓存可清」原则一致）。
  - 理由：这是该文件与上游**唯一**的差异（整文件 400 行，其余逐字节相同）。但因为整文件不参与编译，这处改动实际是死代码——若打算长期保留该文件作参考，应把改动撤回上游原样（避免下次合并冲突），若打算恢复它，需要先补 `BitmapDecoder` 等价实现。**未核实**：是否有人有意把它当「备用实现」保留。

- **第 34-41 行 + 第 122-212 行（AVIF/JXL 的 base64 解码器探测与 `LoadAvifImageAsync`/`LoadJxlImageAsync`）**｜cut 裁剪｜must
  - 上游：`:34/:36` `AvifDecoderSupported`/`JxlDecoderSupported` 静态字段，在静态构造里用一段 base64 内嵌样本 + `BitmapDecoder` 试解码来探测系统是否装了 AVIF/JXL 扩展；`:84-90` 命中 `.avif`/`.jxl` 且系统不支持时走 `LoadAvifImageAsync`/`LoadJxlImageAsync`（手动解容器 + `IMemoryBufferByteAccess` 拷像素，`:142/:185`）。
  - 移植：整段随文件一起被排除编译；AVIF/JXL 改由 `Helpers/MagickCodec.cs`（Magick.NET + libheif/libjxl，注释称「已实测往返」）统一处理，`MagickCodec.IsAvailable` 为 false 时调用方退回 Skia 并提示不支持。
  - 理由：`BitmapDecoder` 与 `IMemoryBufferByteAccess` 不可用；改为「进程内代理解码」比「探测系统扩展再手写容器解析」更简单可靠。副作用：`ImageThumbnail` 里那两个 `*DecoderSupported` 语义（「系统装没装扩展」）在移植侧不存在，取而代之的是「Magick 原生库有没有加载成功」，用户在提示文案上看到的解释会不一样（`BackgroundService` 报的是 `background image cannot be decoded`）。

- **第 281-288 行（`IMemoryBufferByteAccess` 的 COM 声明）**｜api 平台 API｜must
  - 上游：`:282-289` `[ComImport]` + `[Guid("5B0D3235-…")]` + `public unsafe interface IMemoryBufferByteAccess { void GetBuffer([Out] out nint buffer, [Out] out uint capacity); }` —— WinRT `IBufferByteAccess` 的 C# 投影（与移植侧这段完全相同），配合 `refer.As<IMemoryBufferByteAccess>().GetBuffer(...)`（:142、:185）零拷贝拿像素。
  - 移植：随文件排除编译；像素访问改用 `SkiaSharp`（`SKBitmap.GetPixels()` / `SKPixmap.GetPixelSpan()`）与 `Controls/CachedImage.Linux.cs:279` 的 `File.ReadAllBytesAsync`。
  - 理由：`Windows.Graphics.Imaging` 的 COM 互操作在 Uno/Skia 下没有实现。

---

### 附：清单外、但与本组强相关、建议补登总账的文件

> 这两个文件不在 `/tmp/lw-docs/groups/*.txt` 的任何分组里，属于清单缺口（旧清单按 0.19.0 之前的结构生成）。

- `Features/Background/VideoThumbnailer.cs`（**移植新增**，224 行）
  - 定位：`internal static class VideoThumbnailer`（:24），`ThumbnailFolder => Path.Combine(AppConfig.CacheDirectory, "bg-thumbs")`（:44），`EnsureAsync(videoPath, token)`（:55）用 `new LibVLC("--no-audio", "--quiet", "--no-video-title-show", "--no-osd", "--vout=vmem")`（:140）抽首帧，`_gate = new SemaphoreSlim(2, 2)`（:33）限并发，`frameReady.Wait(TimeSpan.FromSeconds(6))`（:163）等帧，`FailedRetention = 10min`（:41）缓存失败结果，`ClearCache()`（:208）。
  - 建议记 1 条：`engine 引擎能力｜must 必需`——上游对应能力是 `Features/Codec/ImageThumbnail.cs` 的 `GetVideoThumbnailAsync`（MediaPlayer + SoftwareBitmap），移植换成 libvlc vmem 抽帧并落盘缓存。
- `Features/Background/GameBackgroundModel.cs`（**上游 0.19.0 新增**，与 `src/Starward/` 逐字节相同）
  - 无需记为偏离；但它是「Background 模型重做」的载体（`GameBackground` 变纯 DTO，选中态/`Type`/`Last` 都在 `GameBackgroundModel` 上），本组多条条目（`AppBackground.Linux.cs` 的 `CurrentGameBackground`、`BackgroundService.GetGameBackgroundsAsync`）都以它为准，总账的对照口径要跟着改。

### 3.8 设置界面（Setting）

#### `Features/Setting/AboutSetting.xaml`

- **第 22 行（页面顶部 logo）**｜cosmetic 外观文案｜must
  - 上游：`Source="ms-appx:///Assets/logo.png"`
  - 移植：`Source="ms-appx:///Assets/lunarward.png"`
  - 理由：`src/Lunarward/Assets/` 下没有 `logo.png`（只剩 `login-helper/` 与 `lunarward.png`），照抄上游会得到一个加载失败的空白图。

- **第 26 行（版本号下方标题）**｜cosmetic 外观文案｜must
  - 上游：`Text="Starward"`
  - 移植：`Text="Lunarward"`
  - 理由：应用已分叉，标题保留上游名会让用户把问题报到 Scighost/Starward。

- **第 28 行（署名行）**｜cosmetic 外观文案｜reasonable
  - 上游：`Developed by Scighost, all rights reserved.`
  - 移植：`Lunarward · 基于 Starward（MIT）的 Linux 移植`
  - 理由：需保留上游 MIT 归属同时说明移植身份；措辞硬编码中文（同页其余文案也是这种混合风格，未做 i18n）。

> 未替换（与上游一致，不构成偏离）：第 59–125 行的相关链接仍全部指向上游域名（`starward.scighost.com`、`github.com/Scighost/Starward/issues|releases|blob/main/docs/Privacy.md`、`donate.scighost.com`）。点击「问题反馈」实际会把用户带到上游仓库；`AboutSetting.xaml.cs` 两侧逐字节相同，更新检测走 `Features/Update/UpdateService.Linux.cs`，XAML 层面无更新块差异。

#### `Features/Setting/AdvancedSetting.xaml`

- **第 18–40 行（页面首个区块）**｜linux 专属实现｜reasonable
  - 上游：页面被清空，只有 `<StackPanel />`（0.19.0 随 RPC 一起删除全部内容）
  - 移植：新增「诊断（D61）」区：标题 `Text="诊断"` + 说明 + 「生成诊断包」（Glyph F412）+「打开下载目录」（Glyph E838）
  - 理由：上游该页原本承载 RPC 状态/URL Protocol 开关，Windows 专属入口没了之后，移植侧把这页改成 Linux 排障入口（诊断包是 Linux 特有的报障载体）。

- **第 42–63 行（页面第二个区块）**｜linux 专属实现｜reasonable
  - 上游：无
  - 移植：「运行输出（Lunarward / D27）」区：`lang:Lang.Lunarward_Output_Title` + 说明 +「查看」（Glyph E9D9，`Lunarward_Output_View`）+「打开日志文件夹」（Glyph E838，`Lunarward_Output_OpenFolder`）
  - 理由：Linux 侧无调试器附着的常规路径（D27 的 OutputCapture 进程内环形缓冲），需要一个入口把最近输出吐给用户。

- **第 19、23–25、31、37 行（诊断区的标题/说明/两个按钮文案）**｜cosmetic 外观文案｜avoidable
  - 上游：无此区块
  - 移植：`Text="诊断"`、硬编码中文说明、「生成诊断包」「打开下载目录」
  - 理由：同一页的运行输出区（第 43、55、61 行）已经全部走 `Lang.Lunarward_Output_*` 资源键，诊断区却硬编码，导致这个页面无法翻译；补 4 个 resx 键即可消除。

- **第 30 行（「生成诊断包」按钮图标）**｜engine 引擎能力｜reasonable
  - 上游：无（新代码）
  - 移植：`<FontIcon FontSize="16" Glyph="&#xF412;" />`
  - 理由：F412 全仓仅此一处使用，L-88 的图标码位替换清单里没有它；该码位属于 Fluent 私有区，是否落在 Uno 自带 Symbols 字体的覆盖范围内**未核实**（若不在，此处渲染为方框）。同页其余图标（E838/E9D9）都在已核对范围内。

#### `Features/Setting/AdvancedSetting.xaml.cs`

- **第 15–27 行（类头注释）**｜cut 裁剪｜must
  - 上游：0.19.0 已删除 RPC 服务与 URL Protocol 页内开关（`AppConfig.KeepRpcServerRunningInBackground` 变 `[Obsolete(..., true)]`，相关页面段落一并消失）
  - 移植：`LUNARWARD-DEVIATION` 注释说明上游删除，能力迁到 `Program.Linux.cs` 的 `--register-protocol` / `--unregister-protocol` / `--protocol-status` 三个 CLI 入口
  - 理由：上游把「图形界面里开关协议注册」整段拿掉了，移植侧不重建 GUI，改用不依赖注册表的 CLI（Linux 的 xdg-mime 注册本身就是用户级操作）；这是一次「上游删除后的重新落位」，不是移植自造偏离。

- **第 35–73 行（`Button_ViewOutput_Click`）**｜linux 专属实现｜reasonable
  - 上游：无
  - 移植：点「查看」时新建 `ContentDialog`，用 `OutputCapture.Recent(500)` 渲染最近 500 行并支持刷新
  - 理由：提供不依赖终端的运行输出查看；`BuildOutputText`（第 75–90 行）用 `HH:mm:ss.fff` + Scope + IsError 拼行。

- **第 47–52 行（上述对话框的标题与三个按钮）**｜cosmetic 外观文案｜avoidable
  - 上游：无
  - 移植：`Title = "最近运行输出"`、主按钮「刷新」、次按钮「打开日志文件夹」、关闭「关闭」
  - 理由：纯硬编码中文，同一功能区块内 XAML 已用 resx 键，代码侧不一致，非中文环境看到中文。

- **第 92–104 行（`Button_CreateDiagnose_Click`）**｜linux 专属实现｜reasonable
  - 上游：无
  - 移植：`await Helpers.DiagnosticBundle.CreateAsync(logger: _logger)`，成功/失败分别 `InAppToast.MainWindow.Success/Error`
  - 理由：诊断包是 Linux 侧新增能力（进程/环境/前缀信息汇总），无上游对应物。

- **第 107–115 行（`Button_OpenDiagnoseFolder_Click`）**｜api 平台 API｜avoidable
  - 上游：无
  - 移植：`Path.Combine(GetFolderPath(SpecialFolder.UserProfile), "Downloads")` + `Directory.CreateDirectory` + `Helpers.OpenUrl.Open`
  - 理由：写死 `~/Downloads`，忽略 `XDG_DOWNLOAD_DIR`（全仓 grep 无 `xdg-user-dir` 用法，`Helpers/DiagnosticBundle.cs:33-34` 同样写死）。在中文桌面（`~/下载`）上「打开下载目录」会新造一个空的 `~/Downloads`。

- **第 125–135 行（`OpenOutputFolder`）**｜api 平台 API｜avoidable
  - 上游：无
  - 移植：`Path.Combine(UserProfile, ".local", "share", "lunarward", "logs", "output")` + 直接 `Process.Start(new ProcessStartInfo("xdg-open", folder))`
  - 理由：两处不一致——①`OutputCapture.cs:83` 写入的是 `Path.Combine(DataHome(), "lunarward", "logs", "output")`，而 `DataHome()`（`OutputCapture.cs:72-75`）优先 `XDG_DATA_HOME`，注释明写「R107：以前写死 ~/.local/share，忽略 XDG_DATA_HOME」；这里又把写死路径放回来了，设了 `XDG_DATA_HOME` 的用户点开的是空目录。②绕过 `Helpers.OpenUrl` 的 portal→xdg-open→gio 链路，在无 `xdg-open` 的精简环境里静默失败（`Process.Start` 抛异常被 catch 成 `Log.Warning`）。

#### `Features/Setting/DownloadSetting.xaml.cs`

- **第 50–61 行（`SetRateLimiterAsync`）**｜cut 裁剪｜reasonable
  - 上游：`RpcService.CheckRpcServerRunning()` → `CreateRpcClient<GameInstaller.GameInstallerClient>()` → `SetRateLimiterAsync(new RateLimiterMessage { Enable = limit > 0, BytesPerSecond = limit }, deadline: +3s)`
  - 移植：`await Task.CompletedTask;`（注释「限速由 RPC 安装器执行；D25 下 M1 无下载流水线」）
  - 理由：RPC 子进程被裁掉，限速改为「只写配置」；语义并未失效——`Features/GameInstall/GameInstallService.cs:215` 读 `AppConfig.SpeedLimitKBPerSecond` 并 `Math.Clamp(×1024)` 后交给上游安装器（`DownloadSetting.xaml.cs:42/:46`、`AppConfig.Setting.cs:139`）。行为差异：上游是滑动即生效（下发到正在跑的任务），移植是下次开始任务才生效。

- **第 5、11、53–60 行（残留脚手架）**｜eng 工程性｜avoidable
  - 上游：`using Starward.Features.RPC; using Windows.System;` 有实际用途
  - 移植：两个 using 已无引用，`SetRateLimiterAsync` 的 `try { await Task.CompletedTask; } catch (Exception ex) { _logger.LogError(...) }` 整块是死骨架
  - 理由：裁剪时只删了主体、没删外围；留着会让人误以为限速还有一条执行路径，也挡不住「RPC 依赖已清空」的静态检查。

#### `Features/Setting/FileManageSetting.xaml`

- **第 55–61 行（备份/数据库区的长路径 `TextBlock`）**｜cut 裁剪｜avoidable
  - 上游：`IsTextTrimmedChanged="TextBlock_IsTextTrimmedChanged"` + `MaxLines="2"` + `CharacterEllipsis`
  - 移植：删掉 `IsTextTrimmedChanged`，只留 `MaxLines="2"` + `CharacterEllipsis`
  - 理由：该事件处理器在移植树里不存在（全仓 grep 只命中 XAML 侧，`FileManageSetting.xaml.cs` 没有实现），保留会是悬空引用。需要注明：上游 XAML 同样只有属性、`.xaml.cs` 里也没有同名方法（**未核实**上游 XAML 编译期如何消解），所以这一条实际是把「上游本来就没接上的属性」删干净，视觉行为无差；与 `ScreenshotSetting` 页不同，那边移植侧反而在 `ScreenshotSetting.Linux.cs:166` 补回了处理器。

- **第 83 行（「备份数据库」按钮图标）**｜engine 引擎能力｜reasonable
  - 上游：`<FontIcon Glyph="&#xF413;" />`
  - 移植：`<FontIcon Glyph="&#xE74E;" />`
  - 理由：F413 属 Fluent 私有区，Uno 内置 Symbols 字体不一定覆盖（L-88 的码位替换口径），换成语义相近的 E74E（Save）。

#### `Features/Setting/FileManageSetting.xaml.cs`

- **第 192–196 行（`BackupDatabaseAsync`）**｜api 平台 API｜must
  - 上游：`Path.ChangeExtension(file, ".7z")` + `new SharpSevenZipCompressor().CompressFiles(archive, file)`
  - 移植：`Path.ChangeExtension(file, ".zip")` + `DatabaseService.CreateArchive(archive, file)`
  - 理由：`SharpSevenZip` 带原生 7z 库（Windows x64 二进制），Linux 没有对应物；改用仓库已有的单文件 zip（`DatabaseService.cs:119-129` 自动备份本就走 `CreateArchive`）。注释（第 194–196 行）明确只压当次 `.db`、不打包整个 `DatabaseBackup` 目录——那是 D46 复核发现的上游真 bug（会把历次备份套娃进新包）。

- **第 192 行（备份包扩展名）**｜eng 工程性｜reasonable
  - 上游：备份包为 `.7z`
  - 移植：备份包为 `.zip`
  - 理由：副作用是两平台备份不互通：Linux 生成的 `.zip` 拿到 Windows 版「恢复数据库」不认（上游按 `.7z` 解），反之亦然。属于压缩库替换的直接后果，可接受但需在用户文档里说明。

- **第 75、132、438–448 行（`UnregisterInstanceKey`）**｜bugfix 修上游缺陷｜must
  - 上游：`AppInstance.GetCurrent().UnregisterKey()`（退出前释放单实例键）
  - 移植：新增 `private static void UnregisterInstanceKey()`，内部 try/catch 包裹同一调用并 `Serilog.Log.Debug(ex, "AppInstance.UnregisterKey is not available on this platform")`（注释「B13：Uno 的 `AppInstance` 是未实现的投影桩」）
  - 理由：Uno 的 `AppInstance` 是不完整投影，直接调会抛；这是为引擎缺口加的保护层，不是逻辑变更。

> 未替换（与上游一致，不构成偏离）：第 99 行 `Launcher.LaunchUriAsync(new Uri(AppConfig.UserDataFolder))`、第 228 行 `Launcher.LaunchFolderAsync(folder, options)`（带 `FolderLauncherOptions.ItemsToSelect`）、第 271/275 行 `Launcher.LaunchFolderPathAsync(...)` 全部保留 WinRT launcher。L-74 记载 Uno Skia 有 launcher 实现但个别 scheme 静默失败，`docs/linux-port/12-shims-and-gaps.md:51` 称已替换 8 类 `Launcher.LaunchUriAsync` 调用——**本页三处不在替换范围内**，「打开数据目录」「定位最近备份」在无 xdg-open 的环境里可能点了没反应（未核实 Uno launcher 的 Linux 回落）。

#### `Features/Setting/GamepadControlSetting.xaml`

- **第 21–27 行（页面顶部）**｜cut 裁剪｜reasonable
  - 上游：无 InfoBar
  - 移植：`InfoBar IsClosable="False" IsOpen="True" Severity="Informational"`，`Title="{x:Bind lang:Lang.Lunarward_Gamepad_NotSupported_Title}"`、`Message="{x:Bind lang:Lang.Lunarward_Gamepad_NotSupported_Message}"`
  - 理由：手柄映射是 M4 占位（已知缺口），整页仍保留排版但明确告知不可用，避免用户以为开关坏了。文案走 resx，是本组里少数连提示都做了 i18n 的地方。

- **第 34–38 行（「手柄增强」`ToggleSwitch`）**｜cut 裁剪｜reasonable
  - 上游：可交互
  - 移植：加 `IsEnabled="False"`（`IsOn` 仍 TwoWay 绑定到 `EnableGamepadController`）
  - 理由：与上面的 InfoBar 配套，禁用而非删除，保留上游排版与未来接线点。

- **第 14、101、109、113、128、159 行（`XboxFluent2` 字体资源与 5 处图标）**｜engine 引擎能力｜avoidable
  - 上游：`<FontFamily x:Key="XboxFluent2">ms-appx:///Assets/Font/XboxFluent2.ttf#Xbox Fluent v2</FontFamily>`，5 处 `<FontIcon FontFamily="{ThemeResource XboxFluent2}" Glyph="E9A1/E9A4/E9C8/E9A1/E7D4"/>`
  - 移植：资源声明与 5 处引用原样保留（码位也未替换）
  - 理由：`src/Lunarward/Assets/` 下已无 `Font/` 目录（只有 `login-helper/`、`lunarward.png`），L-88 亦记「`src/Lunarward/Assets/Font/` 现已不存在（只剩 `bin/` 输出里的副本）」。这 5 个图标现在渲染为空/方框，属于「页内让位已做、图标没跟着换」的遗漏：同目录 `SettingPage.xaml:82-88` 的手柄菜单图标已经改成了 Uno Symbols 码位 E7FC，这里却没改。

#### `Features/Setting/GeneralSetting.xaml`

- **第 46 行（「关闭窗口时」`RadioButtons` 的「隐藏到系统托盘」项）**｜cosmetic 外观文案｜avoidable
  - 上游：`Content="{x:Bind lang:Lang.ExperienceSettingPage_MinimizeToSystemTray}"`
  - 移植：`Content="隐藏到系统托盘"`
  - 理由：同页其余两项（关闭/最小化）仍走 `lang`，只有这一项硬编码中文，非中文界面出现中英混排。

- **第 56–79 行（「透明效果 / 动画效果」整段）**｜cut 裁剪｜reasonable
  - 上游：`MenuFlyoutSeparator` + 两条 `Hyperlink Click="Hyperlink_VisualEffects_Click" NavigateUri="ms-settings:easeofaccess-visualeffects"` + 说明文本
  - 移植：整段删除，只剩注释「Linux：透明/动画效果是 Windows 系统设置项…整段移除（D61）」
  - 理由：`ms-settings:` 是 Windows 设置页 URI，Linux 无对应目标；这两项只是跳转 Windows 辅助功能设置，不是本应用自己的开关，删掉不损失功能。

> 未替换（与上游一致，不构成偏离）：第 31 行「帮助翻译」`Hyperlink NavigateUri="https://github.com/Scighost/Starward/blob/main/docs/Localization.md"` 仍指上游文档。语言 `ComboBox`（第 20–27 行）与上游完全一致，切换逻辑在 `GeneralSetting.xaml.cs`，未改。

#### `Features/Setting/GeneralSetting.xaml.cs`

- **第 10 行（using 区）**｜eng 工程性｜must
  - 上游：无 `using Starward.Features;`
  - 移植：新增该 using
  - 理由：0.19.0 把 `PageBase` 从根命名空间迁到 `Starward.Features`，基类不再能靠 `Starward` 命名空间解析；纯同步上游改名的编译性改动。

- **第 186–198 行（`Hyperlink_VisualEffects_Click`）**｜cut 裁剪｜avoidable
  - 上游：`private async void Hyperlink_VisualEffects_Click(...)` → `await Launcher.LaunchUriAsync(new Uri("ms-settings:easeofaccess-visualeffects"))`，由 XAML 两条 `Hyperlink` 调用
  - 移植：方法原样保留，但 XAML 里已无任何引用（对应区块在第 56–79 行被删）
  - 理由：删 XAML 没删 handler，留下孤儿方法与一个 `ms-settings:` URI；现在既不报错也不会被触发，属可清理残留（同页 `GeneralSetting.xaml` 的注释已说明删除理由，代码侧没跟上）。

#### `Features/Setting/HotkeySetting.xaml`

- **第 19–22 行（单键快捷键说明文本）**｜cosmetic 外观文案｜reasonable
  - 上游：注释「需要管理员权限」+ `Text="{x:Bind lang:Lang.HotkeySetting_SingleKeyShortcutsTips}"`
  - 移植：注释改为「Linux 口径（D61）：热键走 X11 轮询，不需要管理员权限；纯 Wayland 会话不可用。」，`Text="{x:Bind lang:Lang.Lunarward_Hotkey_Description}"`
  - 理由：上游单键热键靠 `RegisterHotKey`/管理员权限，Linux 侧改为 X11 键盘状态轮询，权限前提与失败模式（Wayland 不可用）完全不同，必须换文案而不是沿用。新键在 `Lang.resx:3080` 及 de-DE/es-ES/it-IT/ja-JP/ko-KR/ru-RU/th-TH/vi-VN/zh-CN 共 10 份 resx 中都有；旧键 `HotkeySetting_SingleKeyShortcutsTips` 在本页与 `ScreenshotSetting` 页被同时替换后已无引用。

- **第 32–38、48–54 行（两个 `HotkeyInput` 的 `WindowHandle` / `HotkeyId`）**｜api 平台 API｜reasonable
  - 上游：`WindowHandle="{x:Bind WindowHandle}"`（`=> XamlRoot.GetWindowHandle()`）+ `HotkeyId="44444"/"44445"`，`HotkeyManager.RegisterHotkey(hwnd, id, ...)` 调 Win32 `RegisterHotKey`
  - 移植：XAML 一个字未改（`HotkeySetting.xaml.cs` 也与上游逐字节相同），但 `Helpers/HotkeyManager.Linux.cs`（代替被 `<Compile Remove>` 的上游 `Features/Setting/HotkeyManager.cs`）同签名重写为 X11 轮询，`hwnd` 被忽略，id 退化成固定派发 id（44444 = 显示主窗口 Alt+S，44445 = 截图 Alt+D）
  - 理由：Win32 热键 API 在 Linux 不存在，只能换成键态轮询；因为签名保持一致，XAML 与 code-behind 都不用动——这正是 L-185 里「同签名替身」的收益面，代价是 id 的语义从「系统热键句柄」变成了「程序内常量」，重复注册不再有 OS 层报错。

#### `Features/Setting/ScreenshotSetting.Linux.cs`

> 上游对照为 `src/Starward/Features/Setting/ScreenshotSetting.xaml.cs`（444 行），该文件在 `src/Lunarward/Lunarward.csproj:133` 被 `<Compile Remove="Features/Setting/ScreenshotSetting.xaml.cs" />` 排除，由本文件（173 行）整体替代。

- **第 137–150 行（`TestCaptureAsync`）**｜engine 引擎能力｜must
  - 上游：`User32.MonitorFromWindow` + `DisplayInformation` + `Direct3D11CaptureFrame` + `CanvasBitmap` + HDR（maxCLL/SdrWhiteLevel）+ `ImageSaver.SaveAsPng/Avif/Jxl` + 弹出 `ScreenCaptureInfoWindow`
  - 移植：`ScreenCaptureLinux.CaptureAsync(EffectiveFolder)`，失败提示「截图失败：没有找到可用的截图工具（spectacle / grim / gnome-screenshot）」
  - 理由：`Windows.Graphics.Capture` 与 Win2D 在 Linux 无实现，改为调外部工具（`ScreenCapture.Linux.cs` 依序试 `spectacle -b -n -f -o`、`grim`、`gnome-screenshot -f`）。能力缺口：无窗口级捕获、无 HDR 色调映射、只有 PNG（`Lunarward_{yyyy-MM-dd_HH-mm-ss}.png`）。

- **第 60、152–163 行（`WindowHandle` 属性与三个 `HotkeyInput` 事件处理器）**｜api 平台 API｜must
  - 上游：`public nint WindowHandle => XamlRoot.GetWindowHandle();` + `HotkeyInput_HotkeyEditing/HotkeyEditFinished/HotkeyDeleted` 调 `HotkeyManager.InitializeHotkeyInput` 做实时注册/预览/注销
  - 移植：`public string? WindowHandle { get; set; }`（从未赋值，恒 null）；三个处理器方法体为空，各带 `// LINT-ALLOW: 热键由 HotkeyManager 统一接管（X11 轮询）…` 注释
  - 理由：X11 键态轮询没有「占用式注册」，也没有可交给 OS 的窗口句柄，预览/注销无意义；处理器不能删（`ScreenshotSetting.xaml:88-96` 已引用），只能留空并登记。两处遗留需注意：①属性的类型从 `nint` 变成 `string?`，而 `HotkeyInput.WindowHandle` 是 `nint`（`Features/Setting/HotkeyInput.xaml.cs:28`），XAML 仍是 `WindowHandle="{x:Bind WindowHandle}"`，这个 `string?`→`nint` 的绑定为何能编译通过**未核实**；②`Helpers/XamlRootExtension.Linux.cs:9` 其实有 `GetWindowHandle(this XamlRoot?) => 0` 替身，保留上游写法也能编过，改成可写属性并非必需。

- **第 63–67 行（构造函数）**｜eng 工程性｜avoidable
  - 上游：ctor 依次 `InitializeComponent()` → `InitializeHotkeyInput()` → `InitializeScreenshotFolder()`
  - 移植：ctor 只有两句赋值（`ScreenshotFolder`、`AutoCopyScreenshotToClipboard`），全文无 `InitializeComponent()`（同一目录 `AboutSetting`/`AdvancedSetting`/`DownloadSetting`/`FileManageSetting`/`GamepadControlSetting`/`GeneralSetting`/`HotkeySetting`/`RuntimeEnvironmentSetting` 的 ctor 都调了）
  - 理由：整文件替代被 `<Compile Remove>` 的 `.xaml.cs` 时，若 Uno 不给 `x:Class` 页面自动初始化，则本页 `Content` 为空、`x:Bind` 全部不生效（`Bindings.Update()` 也会落在空树上）。**未核实**：本轮不允许构建/运行，无法确认 Uno 7 是否有兜底；`docs/linux-port/12-shims-and-gaps.md:107` 仍把该页记为「🟡 可用」，与代码现状矛盾，建议优先实测这一页能否加载。

- **第 52–56、75–83 行（`AutoConvertScreenshotToSDR` / `EnableScreenshotColorManagement` / `AutoCopyScreenshotToClipboard`）**｜cut 裁剪｜avoidable
  - 上游：三个 setter 都直接写 `AppConfig`（`AutoConvertScreenshotToSDR`、`EnableScreenshotColorManagement`、`AutoCopyScreenshotToClipboard`）
  - 移植：三个纯自动属性（第 52/54/56 行），`OnNavigatedFrom`（第 75–83 行）只回写 `ScreenshotFolder` 与 `AutoCopyScreenshotToClipboard`
  - 理由：注释理由是「Uno 下逐属性 setter 落盘会写太多次文件」。后果是 **SDR 转换与色彩管理两个开关勾了不落盘、离开页面即回弹，且全仓无消费者**（上游这两个值服务 HDR 截图管线，Linux 外部工具链路不做色调映射）。页面留着一个看起来能用、实际什么都不做的开关，属于应该置灰或加说明的项。

- **第 15、86–95 行（`ScreenshotFolder` 与 `ChangeScreenshotFolderAsync`）**｜eng 工程性｜reasonable
  - 上游：`public string? ScreenshotFolder { get => field; set => SetProperty(ref field, value); }`（INPC），改目录后 XAML 自动刷新
  - 移植：普通自动属性 + 第 94 行手动 `Bindings.Update()`（注释称「XAML 里是 OneTime 绑定」，但 `ScreenshotSetting.xaml:11` 根节点是 `x:DefaultBindMode="OneWay"`，实际原因是缺 INPC）
  - 理由：为了让 4 个属性共用「离开页面统一落盘」的写法而放弃 `SetProperty`，代价是任何非本方法修改 `ScreenshotFolder` 的路径都不会刷新 UI（例如后续从 CLI/托盘改目录），且 `Bindings.Update()` 是整页重刷。

- **第 63–73 行（构造函数与 `EffectiveFolder`）**｜api 平台 API｜reasonable
  - 上游：ctor 在 `AppConfig.ScreenshotFolder` 为空时回填 `UserDataFolder/Screenshots` 并 `Directory.CreateDirectory`
  - 移植：ctor 只赋 `ScreenshotFolder = AppConfig.ScreenshotFolder`（可为 null），实际目录由 `EffectiveFolder` 兜底到 `ScreenCaptureLinux.DefaultFolder` = `~/Pictures/Lunarward`（`ScreenCapture.Linux.cs:22-45`）
  - 理由：默认目录从「应用数据目录下」改成 XDG 的 `~/Pictures`，更符合 Linux 用户预期；但 ctor 不再回填，导致「截图文件夹」输入框在未配置时显示为空，用户看到的是空白而非实际生效路径。

- **第 114–135 行（`ClearThumbnailCache`）**｜cut 裁剪｜reasonable
  - 上游：`ImageThumbnail.ClearThumbnailCache()`（内部走 Windows 缩略图缓存）
  - 移植：手工 `Directory.Delete(Path.Combine(AppConfig.CacheDirectory, "thumb"), true)`，结果提示 InAppToast 硬编码中文「已清理缩略图缓存」/「没有可清理的缩略图缓存」
  - 理由：`ImageThumbnail` 的 Windows 实现不可用，改为直接删自建缓存目录；顺带把提示文案写死了（同页其它提示也有硬编码，未做 i18n）。

- **第 166–172 行（`TextBlock_IsTextTrimmedChanged`）**｜eng 工程性｜reasonable
  - 上游：同一方法在被 `<Compile Remove>` 的 `ScreenshotSetting.xaml.cs` 里（XAML 第 62 行引用）
  - 移植：在 `.Linux.cs` 里重新实现（截断一次缩字号，最小 12，注释「D46 复核结论 1」）
  - 理由：整文件重写时容易漏掉这类纯 UI 胶水方法，这里补回了；对照 `FileManageSetting.xaml` 那边直接删属性的做法，两个页面对同一事件的处理口径不一致。

#### `Features/Setting/ScreenshotSetting.xaml`

- **第 77–85 行（截图快捷键说明文本）**｜cosmetic 外观文案｜avoidable
  - 上游：`Text="{x:Bind lang:Lang.HotkeySetting_SingleKeyShortcutsTips}"`
  - 移植：硬编码中文「Linux 下截图热键通过轮询 X11 键盘状态实现，不需要管理员权限。纯 Wayland 会话不可用，可以把系统快捷键绑定到 lunarward --screenshot。」
  - 理由：内容是 Linux 专属的，确实需要重写；但同一轮改动里 `HotkeySetting.xaml:22` 已经为此新建了 `Lunarward_Hotkey_Description` 资源键（10 份 resx），这里却写成内联字符串，非中文界面出现中文。

- **第 119 行（`Segmented` 的可见性绑定）**｜eng 工程性｜avoidable
  - 上游：`Visibility="{x:Bind ScreenshotQualityVisibility}"`
  - 移植：`Visibility="{x:Bind ScreenshotQualityVisibility, Mode=OneWay}"`
  - 理由：根节点第 11 行已有 `x:DefaultBindMode="OneWay"`，显式 `Mode` 完全冗余、行为无差；疑似为绕开 `cwc:Segmented` 手写替身（`Controls/ToolkitControls.cs:19` `Segmented : ListView`）的绑定问题而试加后未清理。

- **第 166–169 行（`TextBlock_CaptureError`）**｜cut 裁剪｜avoidable
  - 上游：`TestCaptureAsync` 失败时在 code-behind 里写 `TextBlock_CaptureError.Text` 与 `Visibility`
  - 移植：XAML 元素保留，`ScreenshotSetting.Linux.cs` 全文从不引用它（失败改走 InAppToast 提示）
  - 理由：错误提示渠道换了，但页面元素没删，成为一个永远不可见的死节点；同页第 62–65 行的文件夹文本块则仍在用（`TextBlock_IsTextTrimmedChanged` 已补回）。

> 关于 L-185：本页第 109–123 行使用 `cwc:Segmented`/`cwc:SegmentedItem`（`xmlns:cwc="using:CommunityToolkit.WinUI.Controls"`），对应 `src/Lunarward/Controls/ToolkitControls.cs` 的手写替身（第 12 行 `SegmentedItem : ListViewItem`、第 19 行 `Segmented : ListView`，第 29–32 行注释说明 Uno 7 的 `ItemsPanelTemplate` 构造不一致故不替换 `ItemsPanel`，改在 `OnApplyTemplate` 里设 `ItemsStackPanel.Orientation=Horizontal`）。实测该页 XAML 与上游一致、**未被追改**（仅多一个冗余 `Mode=OneWay`）；本组其余 7 个设置页 XAML 完全不引用 toolkit 替身，所以 L-185 在这组里没有造成 XAML 改写，只留下了第 119 行那处痕迹。

#### `Features/Setting/SettingPage.xaml`

- **第 3 行（页面根节点）**｜engine 引擎能力｜reasonable
  - 上游：`<sf:PageBase x:Class="Starward.Features.Setting.SettingPage" ...>`（无 `NavigationCacheMode`）
  - 移植：加 `NavigationCacheMode="Required"`
  - 理由：Uno/Skia 下每次导航重建页面代价明显（设置页含大量 `x:Bind` 与图片资源），缓存后返回不再重跑 ctor；代价见 `SettingPage.xaml.cs` 那组（直达参数只在首次构造生效，需要在 `OnLoaded` 里补一次）。

- **第 14–16 行（`Page.Resources`）**｜eng 工程性｜avoidable
  - 上游：同（`XboxFluent2` 字体键）
  - 移植：`<FontFamily x:Key="XboxFluent2">ms-appx:///Assets/Font/XboxFluent2.ttf#Xbox Fluent v2</FontFamily>` 保留，但页内已无任何 `{ThemeResource XboxFluent2}` 引用（第 82–88 行的手柄图标已改成 Uno Symbols 码位）
  - 理由：字体文件已不存在（见 `GamepadControlSetting.xaml` 那条），这行现在是无引用、无资源的死声明；删掉即可，留着误导后来人以为该字体可用。

- **第 18–19 行（内容 `Grid`）**｜engine 引擎能力｜reasonable
  - 上游：`<Grid>`
  - 移植：`<Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">`
  - 理由：Linux 无 DWM 丙烯/透明层（`docs/linux-port` 已知引擎缺口），不铺不透明底色时设置页会透出壁纸，文字可读性崩（D61）。

- **第 21 行（`NavigationView`）**｜eng 工程性｜reasonable
  - 上游：`<NavigationView ...>`（无 `x:Name`）
  - 移植：`x:Name="NavigationView_Setting"`
  - 理由：新代码要在 ctor 里遍历 `MenuItems` 按 `Tag` 同步 `SelectedItem`（配合「按 subpage 参数直达某设置页」），必须拿到实例。

- **第 82–88 行（「手柄控制」菜单图标）**｜engine 引擎能力｜must
  - 上游：`<FontIcon FontFamily="{ThemeResource XboxFluent2}" Glyph="&#xE9CD;"/>`
  - 移植：`<FontIcon Glyph="&#xE7FC;"/>` + `LUNARWARD-DEVIATION` 注释
  - 理由：同上，`XboxFluent2` 字体资产已移除，不换码位会渲染成空白；E7FC 属 Uno 内置 Symbols 覆盖范围。

- **第 89–90 行（「运行环境」入口说明）**｜linux 专属实现｜reasonable
  - 上游：无
  - 移植：两行注释「运行环境已提升为一级页（D80），入口在 MainView 的 footer、齿轮正上方；这里保留 SettingPage 里的 `Tag` 映射只为兼容旧的 subpage 参数，不再有可见条目」
  - 理由：运行环境是 Linux 新增模块，提升为一级页后在设置页留了一条「只认参数、不出条目」的兼容路径；`Tag`→`Type` 映射仍在 `SettingPage.xaml.cs:106` 的 `ItemInvoked` 里。

> 两侧第 92–98 行「高级」菜单项都带 `Visibility="Collapsed"`（上游 :88 同样隐藏），不是移植差异，不记。

#### `Features/Setting/SettingPage.xaml.cs`

- **第 19–31 行（构造函数）**｜linux 专属实现｜reasonable
  - 上游：`Frame_Setting.Navigate(typeof(AboutSetting));`
  - 移植：`Type initialPage = ResolveInitialPage(); Frame_Setting.Navigate(initialPage);`，随后遍历 `NavigationView_Setting.MenuItems` 按 `Equals(nvi.Tag, initialPage.Name)` 同步 `SelectedItem`
  - 理由：`--setting <name>` / `LUNARWARD_SUBPAGE` 直达某设置页是 Linux 调试与 CLI 能力（`Helpers/CliSpec.cs` 那套），需要按名字解析页面；页面缓存后导航栏选中项不会自动跟手，故手动同步。

- **第 35–45 行（`OnLoaded`）**｜bugfix 修上游缺陷｜reasonable
  - 上游：ctor 里 `WeakReferenceMessenger.Default.Register<LanguageChangedMessage>(this, ...)`；无 `OnLoaded`
  - 移植：`protected override void OnLoaded()` 内先 `ApplyRequestedPage()`，再注册 `LanguageChangedMessage`（`Bindings.Update()`）
  - 理由：①`NavigationCacheMode="Required"` 下 ctor 只跑一次，把语言消息注册留在 ctor 会在 `OnUnloaded` 的 `UnregisterAll` 之后彻底失联，必须在每次挂载时重注册（D94）；②缓存页重新挂载时也要重新消费一次直达请求。

- **第 46–60 行（`ApplyRequestedPage`）**｜bugfix 修上游缺陷｜reasonable
  - 上游：无
  - 移植：取 `SettingNavigation.TakeTarget()`，与 `Frame_Setting.Content?.GetType()` 相同则直接返回，否则 `Navigate` + 同步选中项（注释「B74：缓存页 ctor 只跑一次」）
  - 理由：与上一条同源——缓存模式下「导航到 X 设置页」的请求只有在挂载点才能可靠兑现；这是对 Uno 缓存语义的补偿，不是新功能。

- **第 62–92 行（`ResolveInitialPage`）**｜linux 专属实现｜reasonable
  - 上游：无（固定 `AboutSetting`）
  - 移植：先 `SettingNavigation.TakeTarget()`，再 `switch (Starward.Features.Dev.DevSwitches.SubPage)`（9 个 `nameof` 分支，含 `RuntimeEnvironmentSetting`），兜底 `typeof(AboutSetting)`
  - 理由：开发/CLI 直达入口；`DevSwitches.SubPage` 定义在 `Features/App/DevSwitches.cs:67`（`Read("subpage", "LUNARWARD_SUBPAGE")`），`#if DEBUG`/`DevSwitches` 门禁满足硬规则 6 的「危险入口不进发行版」。

- **第 106 行（`ItemInvoked` 的 `switch`）**｜linux 专属实现｜reasonable
  - 上游：无 `RuntimeEnvironmentSetting` 分支
  - 移植：新增 `nameof(RuntimeEnvironmentSetting) => typeof(RuntimeEnvironmentSetting)`
  - 理由：运行环境页是一级页但仍接受设置导航参数（D80），需要在这里认名字。

> 已知副作用（未列入偏离，供后续修）：`NavigationCacheMode="Required"` + ctor 内 `ResolveInitialPage()` 叠加，使 `--setting`/`LUNARWARD_SUBPAGE` 的直达参数**只在首次构造生效**；后续挂载靠 `OnLoaded` 的 `ApplyRequestedPage` 覆盖，而 `SettingNavigation.TakeTarget()` 在 ctor 里已被消费一次，首次进入时 `ApplyRequestedPage` 实际是空转。第 122–125 行 `OnUnloaded` 调 `UnregisterAll(this)` 且与上游一样不调 `base.OnUnloaded()`（非移植差异）。

### 3.9 服务与帮助器（PlayTime / Database / Selector / UrlProtocol / Helpers / Update / 占位）

#### `Features/PlayTime/PlayTimeRecordService.cs`

- **第 27-29 行、第 49 行、第 142-144 行（_recordingPids/IsRecording 取代 AppInstance 跨进程键）**｜api 平台 API｜reasonable
  - 上游：`AppInstance.FindOrRegisterForKey($"Starward.PlayTime.{pid}")`，`!instance.IsCurrent` 即认为别的进程已在记（上游第 43-47 行）。
  - 移植：进程内 `ConcurrentDictionary<int, byte> _recordingPids`，`TryAdd` 失败即跳过，`finally` 里 `TryRemove`。
  - 理由：Linux 无 `AppInstance`；启动器单实例 + 记录子进程按 pid 唯一，进程内字典等价。旧账写的 key `playtime_{pid}` 已过时——上游 0.19.0 已改名，条目文字需同步。
- **第 86-111 行（非 Windows 的会话结束判定：WaitForSessionEndAsync + 20s WhenAny + 30s 心跳）**｜linux 专属实现｜must
  - 上游：`while (!process.HasExited) await Task.Delay(...)`，靠 Windows 进程句柄判存活。
  - 移植：`PlayTimeSession.WaitForSessionEndAsync(pid, prefix, ct, biz)`；循环里 `Task.WhenAny(session, Task.Delay(20000))`，每超 30s `Log(Play)` + `SavePlayTimeStats(..., interruption: true)`，结束后 `cts.Cancel()` + `interruption: false` 收尾。
  - 理由：wine 游戏宿主进程秒退、真正游戏进程是它 fork 出来的 wine 子进程，Windows 语义的「进程退出即下课」在 Linux 不成立（L-62/ B58）。心跳是必须的：崩溃/断电时最后一次原子写不能停在上一局。
- **第 60-80 行（ArgumentException 回退）、第 152 行（GetProcessStartTime）、第 189 行起（GetLastStartTime）**｜bugfix 修上游缺陷｜reasonable
  - 上游：`Process.GetProcessById(pid)` 直接取，无 try/catch；pid 已消失时整个记录流程抛异常。
  - 移植：捕获后从库里的 Start 事件回推起点，超过 12h 视为 stale，再用 `/proc/pid/stat` 的 starttime + btime 反算进程启动时刻（B76）排除 pid 复用。
  - 理由：wine loader 退出后 game pid 立即消失，不回推就会漏记整局时长；pid 复用会让空闲时间被算成游玩。
- **第 309-330 行（CreatePlaytimeStartInfo）、第 407-414 行（进程名校验）**｜linux 专属实现｜must
  - 上游：`ProcessName == 游戏 exe 名` 才算命中，直接以当前启动器身份写库。
  - 移植：另起 `playtime --biz {biz} --pid {pid}` 子进程，env 注入 `LUNARWARD_WINEPREFIX`（`ActivePrefix ?? ResolvePrefix(biz)`）、`LUNARWARD_WINE`、`LUNARWARD_WINESERVER`；只对 `wine` / `wine64` / `wine-preloader` 放行，其它进程名仅 LogWarning 后继续。
  - 理由：游戏在 wine 下跑，`/proc/<pid>/comm` 是 wine 而不是游戏名；前缀/wineserver 路径必须显式传给子进程，否则子进程用默认 `~/.wine` 会盯错前缀（子进程侧由 `GameLauncherService.ResolvePrefix/ResolveWine` 与 `PlayTimeSession.ResolveWineserver` 消费）。
- **第 294-302 行（SavePlayTimeStats 两条日志的 interruption 实参恒为 false）**｜eng 工程性｜avoidable
  - 上游：无 `interruption` 参数，日志模板里的 `Interruption` 后面本就没有真实值，硬写 false 无所谓。
  - 移植：方法签名新增 `bool interruption = false`、库里确实会写 `Interruption=1` 的心跳行，但两条日志（信息与错误）仍然把字面量 `false` 传给 `{interruption}` 占位。
  - 理由：移植侧唯一的 `interruption=true` 写入路径（第 103 行心跳）在日志里永远显示 False，排查「时长翻倍/时长丢失」时会直接看错方向。改成传 `interruption` 即可。

#### `Features/PlayTime/PlayTimeButton.xaml.cs`

- **第 63-68 行（RefreshPlayTime(GameBiz) 显式切目标游戏重读时长）**｜linux 专属实现｜reasonable
  - 上游：每次导航重建页面，控件 Loaded 时自然读到新的 `CurrentGameBiz`。
  - 移植：`CurrentGameBiz = gameBiz; InitializePlayTime();`（D94），让被缓存的页面能换目标。
  - 理由：页面缓存是移植侧的既定策略（D94），不补这个方法切游戏后时长卡在上一款游戏。唯一调用点 `Features/GameLauncher/GameLauncherPage.xaml.cs:126`（旧账写 118，漂移 8 行）。

#### `Features/PlayTime/PlayTimeSession.Linux.cs`

- **第 34-92 行（WaitForSessionEndAsync 的四级判定：wine 宿主 → 游戏 exe → wineserver -w → 进程树）**｜linux 专属实现｜must
  - 上游：无对应文件，靠 `process.HasExited`。
  - 移植：`ResolveWineserver()` 之后按「prefix 目录存在 → `WaitForWineProcessAsync(30s)`；B58 游戏 exe 名命中 → `WaitForNamedProcessEndAsync`；否则 `wineserver -w`（WINEDEBUG=-all，`WaitForExitAsync(ct)`）；异常回落 `WaitForProcessTreeEndAsync`」逐级降级。
  - 理由：`wineserver -w` 在「别的前缀还开着游戏」或 wineserver 版本不匹配时会提前返回（L-92），单一策略必然误判整局结束；分级 + exe 名兜底是把误判压到最低的唯一办法。
- **第 94-160 行（WaitForWineProcessAsync / GameExeNames / WaitForNamedProcessAsync / WaitForNamedProcessEndAsync）**｜linux 专属实现｜reasonable
  - 上游：无。
  - 移植：500ms 轮询 wine 进程；hk4e→`YuanShen.exe`/`GenshinImpact.exe`、hkrpg→`StarRail.exe`、nap→`ZenlessZoneZero.exe`、bh3→`BH3.exe`；`WaitForNamedProcessEndAsync` 要求连续 2 次扫空才结束（1s 轮询）。
  - 理由：B58 之前「wine 宿主还在 ≠ 游戏还在」（启动器/更新器也挂在同一 prefix）。代价是整个对局期间每秒遍历 `/proc/*/cmdline`，低配机上属可接受的固定开销，但要清楚它不是零成本。
- **第 375-409 行（ResolveWineserver：LUNARWARD_WINESERVER → wine 同目录 → PATH）**｜linux 专属实现｜must
  - 上游：无。
  - 移植：优先读 env `LUNARWARD_WINESERVER`，其次 `GameLauncherService.ResolveWine()` 同目录下的 `wineserver`，再扫 PATH。
  - 理由：`wineserver -w` 只认和自己同版本的那个 server（L-92）；不解析出具体路径就会连到系统 wine 的 server 上，判定立刻返回。
- **第 192-227 行（HasNamedProcessForPrefix 里的内层 `catch {}`）**｜eng 工程性｜avoidable
  - 上游：无。
  - 移植：遍历 `/proc/*/environ` 时对单个进程的读取失败整段静默丢弃（外层只 catch 目录枚举）。
  - 理由：权限不足（root 所属进程）导致的失败与「进程不存在」无法区分，会话结束时可能少算一次命中；至少该在 Debug 级留一行，避免现场只能靠猜。

#### `Features/Database/DatabaseService.cs`

- **第 9 行、第 100-135 行、第 1236-1264 行（ZipFile 取代 SharpSevenZip 做备份/归档）**｜api 平台 API｜reasonable
  - 上游：`new SharpSevenZipCompressor().CompressFiles(...)`（第 119 行）+ 7z 库。
  - 移植：`CreateArchive` 用 `ZipFile.Open(..., ZipArchiveMode.Create)` + `CreateEntryFromFile(..., CompressionLevel.SmallestSize)`；`BackupToArchive` 先把库文件拷进临时目录 `.lwdb_<guid>` 再打包，条目名 `LunarwardDatabase_{yyyyMMdd_HHmmss}.db`，`finally` 清理。
  - 理由：7z 原生库在 Linux 要额外依赖，zip 够用；`BackupToArchive` 是 CLI `--backup`（`Program.Linux.cs:1386`）需要的新入口。
- **第 113-115 行（产物是 ZIP，扩展名与清理通配符仍写 .7z）**｜cosmetic 外观文案｜avoidable
  - 上游：7z 产物，`.7z` 名副其实。
  - 移植：`archive = Path.ChangeExtension(file, ".7z")`、`Directory.GetFiles(folder, $"StarwardDatabase_AutoBackup_{hash}_*.7z")` 都还是 `.7z`，第 119/128 行却已改为 `CreateArchive`（zip）。
  - 理由：用户看到的自动备份文件名叫 `..._xxxx.7z`，实际是 ZIP；用 7z 工具以外的归档器打开会报格式不符，反过来升级/回滚时清理旧备份的逻辑也按 `.7z` 找。改为 `.zip`（并兼容读旧 `.7z`）即可，注意 CLI 帮助文案已经是 `<输出.zip>`，此处是唯一不自洽处。
- 旧账失效：`L1075-1102 新增 BackupToArchive/CreateArchive｜api｜must（旧账称"扩展名随之改为 .zip"）` —— 两个方法实际在 `1236-1264`（范围漂移），且扩展名并没有改，仍是 `.7z`（事实错误，重算后见上两条）。

#### `Features/GameSelector/GameBizDisplay.cs`

- **第 1-30 行（整份类型上游已删、移植侧仍编译）**｜eng 工程性｜avoidable
  - 上游：0.19.0 起 `Features/GameSelector/` 下没有 `GameBizDisplay.cs`（改为 `PinnedGameIcon` + `GameSwitcher` 横排图标条）。
  - 移植：`public partial class GameBizDisplay : ObservableObject`，`CardWidth`（默认 192）/`CardHeight`（108）带通知，仅被 `GameSelector.xaml` 的 `x:DataType` 引用。
  - 理由：随 `GameSelector.xaml(.cs)` 一起成为死代码，三份合计 1800+ 行仍进 Release 包；删掉需同时删另两份。
- 旧账失效：`L8 partial class｜L18-31 CardWidth/CardHeight（默认 192/108，可通知）｜engine｜reasonable` —— 组件被上游 0.19.0 整份删除，移植侧无实例化点，条目所描述的「引擎缺口导致卡片尺寸需自行均分」已不存在。

#### `Features/GameSelector/GameSelector.xaml`

- **第 1-574 行（整份 XAML 上游已删，移植侧仍编译）**｜eng 工程性｜avoidable
  - 上游：0.19.0 用 `GameSwitcher.xaml`（82 行横排图标 ListView，`MainView.xaml:389` 已接入）。
  - 移植：`GameSelector.xaml` 仍在，`ScrollViewer_GameBizs`（第 105 行，注释引 D56）、卡片 `Width/Height` 绑 `CardWidth/CardHeight`（第 118/119 行）、半透明底 `#40000000`。
  - 理由：`grep -rn GameSelector src/Lunarward` 除自身与注释外无实例化点；`strings Lunarward.dll | grep -c GameSelector` = 128，说明它确实被编译进产物却永不显示。属于「0.19.0 合并后该删没删」。
- 旧账失效：`L105 ScrollViewer_GameBizs SizeChanged｜engine｜reasonable` 与 `L118-119 卡片 Width/Height x:Bind CardWidth/CardHeight｜engine｜reasonable` —— 上游已删除该组件，两条描述的引擎缺口已随组件消失。

#### `Features/GameSelector/GameSelector.xaml.cs`

- **第 1-1202 行（整份 code-behind 上游已删，移植侧仍编译）**｜eng 工程性｜avoidable
  - 上游：0.19.0 无此文件；选择层状态由 `GameSwitcher` + `GameEntryService` + `AppConfig.CurrentGameId` 维护。
  - 移植：1202 行仍在（`CurrentGameChanged` 第 40 行、`CloseLayer()` 第 687 行、`#if DEBUG DevSelectGame` 第 477-491 行、`LUNARWARD_DEV_SELECTOR` 钩子第 799 行、`ScrollViewer_GameBizs_SizeChanged` 第 567 行）。
  - 理由：整份无人实例化；其中 `#if DEBUG` 调试入口与 dev 钩子都在死代码里，等于无效开关。
- **第 75-112 行（0.19.0 旧键迁移：SelectedGameBizs / CachedGameInfo / IsGameBizSelectorPinned / CurrentGameBiz → CurrentGameId）**｜eng 工程性｜avoidable
  - 上游：这些属性在 0.19.0 已标 `[Obsolete(..., true)]`，当前游戏只记 `AppConfig.CurrentGameId`。
  - 移植：为让「升级后原有用户的选择不丢」，在本文件里写了键名兜底读取（`LoadCurrentGameBiz()`）。
  - 理由：迁移代码住在从未实例化的组件里 → 从 0.19.0 之前的移植版本升级上来、旧库只有 `CurrentGameBiz` 的用户，`CurrentGameId` 仍是空，界面回落到默认游戏。要么把这段迁到 `App.xaml.cs`/`GameEntryService` 的一次性迁移，要么承认放弃迁移。
- 旧账失效：`L470-505 ScrollViewer_GameBizs_SizeChanged/UpdateGameCardSize + L701 DispatcherQueuePriority.Low｜engine｜reasonable`、`L930-960 硬链接去重键（自拼 Length_LastWriteTimeUtc_GetHashCode）｜linux 专属实现｜avoidable`、`L1005-1008 if (!OperatingSystem.IsWindows()) continue 注册表分支｜api｜must`、`L241-252 CurrentGameChanged+=HideSelectGameTip / L381-390 #if DEBUG DevSelectGame / L590 CloseLayer｜engine｜reasonable` —— 组件被上游 0.19.0 删除；且去重键那一条即便活着也已不成立（第 1079 行改用 `PosixIdentity.TryGetFileIdentity` 的 st_dev/st_ino，B27）。

#### `Features/UrlProtocol/UrlProtocolService.Linux.cs`

- **第 33-64 行（RegisterProtocol：写 .desktop + update-desktop-database + xdg-mime default）**｜api 平台 API｜must
  - 上游：注册表 `HKCU\Software\Classes\starward` 写 `URL Protocol`，靠 Windows Shell 分发。
  - 移植：写 `$XDG_DATA_HOME/applications/lunarward.desktop`（`Exec="{ExecutablePath}" %u`、`MimeType=x-scheme-handler/lunarward`、`StartupWMClass=Lunarward`），再跑 `update-desktop-database` 与 `xdg-mime default`。
  - 理由：Linux 上 scheme handler 只有 XDG desktop entry 一条路，两个外部命令缺一不可（前者刷 MimeType 缓存、后者设默认）。
- **第 97-144 行（IsProtocolRegistered + RunQuiet）**｜api 平台 API｜must
  - 上游：读注册表判断。
  - 移植：文件存在 + `xdg-mime query default x-scheme-handler/lunarward` 输出等于 `lunarward.desktop`；`RunQuiet` 带 `WaitForExit(5000)`，失败只 LogDebug。
  - 理由：`--protocol-status`（`Program.Linux.cs:296-310`）是排障唯一入口，判定必须问桌面环境的真实默认值而不是只看文件。
- **第 71-77 行（RegisterHypLoginProtocol / UnregisterHypLoginProtocol 共用同一份 .desktop）**｜api 平台 API｜reasonable
  - 上游：`hyp-mihoyo`/`hyp-hoyoverse` 是独立 scheme，注册表可写多值。
  - 移植：`RegisterHypLoginProtocol() => RegisterProtocol();`，`UnregisterHypLoginProtocol()` 空方法体（有意）。
  - 理由：移植侧深链只用一个 `lunarward` scheme 承载全部 action；退出登录时若真按上游撤销注册，会把主 scheme 一起弄掉。空实现是对的，但值得留注释（现有一行注释已说明）。
- **第 80-94 行（UnregisterProtocol 摘掉 .desktop 但不撤销 xdg-mime 默认值）**｜eng 工程性｜avoidable
  - 上游：删注册表键即彻底解除关联。
  - 移植：删文件 + `update-desktop-database`，没有 `xdg-mime default ""` / 还原原默认项。
  - 理由：卸载深链后 `xdg-mime query default` 仍可能返回已删除的 `lunarward.desktop`，`IsProtocolRegistered` 于是返回 false 而系统仍尝试把它当 handler，用户点链接得到的是「什么都没发生」而不是「没有程序处理」。
- **第 14-145 行（HandleUrlProtocolAsync 未移植，深链改走 App.ExpandUrlArgument + SingleInstancePipe）**｜cut 裁剪｜reasonable
  - 上游：`HandleUrlProtocolAsync(string url)` 统一解析 action 并分发。
  - 移植：无该方法；`App.xaml.cs:329` 的 `ExpandUrlArgument` 只识别 `lunarward://startgame?...&biz=` 与 `pid`，其余 action 无处理；第二实例经 `Features/App/SingleInstancePipe.cs:37` 转发给已运行实例。
  - 理由：移植侧只对外承诺「网页启动游戏」这一种深链，其余 action（登录回填等）本身没做。旧账判 `avoidable` 偏严——能力缺口是真实的，但补 `HandleUrlProtocolAsync` 的前提是先把那些 action 实现出来；改判为合理裁剪。
- **打包侧（`packaging/build-appimage.sh:28-31` 只 install .desktop，未跑 update-desktop-database / 无集成分发步骤）**｜eng 工程性｜avoidable
  - 上游：由安装器在装包时注册。
  - 移植：AppImage 只把 `lunarward.desktop` 拷进 `usr/share/applications`，AUR 侧靠 pacman hook / `desktop-file-utils` 刷缓存，AppImage 侧没有。
  - 理由：AppImage 用户点网站上的 `lunarward://` 深链很可能完全没反应（缓存里没有该 MimeType），而 `--register-protocol` 也得用户手动跑。修法是在启动时（或首次运行）检测并补注册，或打包脚本加 `update-desktop-database`。

#### `Features/UrlProtocol/TestUrlProtocolWindow.xaml`

- **第 16 行（Image Source 指向 lunarward.png）**｜cosmetic 外观文案｜reasonable
  - 上游：`ms-appx:///Assets/logo.png`。
  - 移植：`ms-appx:///Assets/lunarward.png`。
  - 理由：移植侧没有 `logo.png`，不改会显示空白图。与上游 diff 仅此一行（旧账写 L12，漂移 4 行）。
- **`TestUrlProtocolWindow.xaml.cs` 被 `Lunarward.csproj:134` 排除、无 `.Linux.cs` 取代（第 1-41 行 XAML 仍参与编译）**｜eng 工程性｜avoidable
  - 上游：code-behind 声明窗口与「测试一次深链」的按钮逻辑。
  - 移植：只剩 XAML 生成的空壳类（dll 内 `strings | grep -c TestUrlProtocolWindow` = 13），无构造函数之外的任何行为。
  - 理由：上游与移植侧都没有实例化点（上游同样只有自身引用），所以用户无感、纯死代码；但「窗口能编译出来却点不动」是下次误用的坑，删 XAML 或补 code-behind 二选一。

#### `Features/CloudGame/CloudGameButton.xaml.cs`

- **第 178 行（Launcher.LaunchUriAsync → OpenUrl.Open）**｜api 平台 API｜must
  - 上游：`await Launcher.LaunchUriAsync(new Uri(url));`
  - 移植：`Starward.Helpers.OpenUrl.Open(url);`（`xdg-open` 系）。
  - 理由：`Launcher` 在 Uno/桌面端不可用；云游戏按钮点开的是浏览器页面，行为等价。与上游 diff 仅此一行。

#### `Helpers/PointerCursor.cs`

- **第 8 行（`public class` → `public partial class`）**｜cosmetic 外观文案｜must
  - 上游：`public class PointerCursor : DependencyObject`。
  - 移植：`public partial class`，上方留了一行移植注释说明必需。
  - 理由：同一编译单元里另有一处 `partial` 声明，去掉就 CS0260 编译失败；属编译必需而非风格。

#### `Helpers/FileDialogHelper.Linux.cs`

- **第 47-130 行（四组入口 × nint/XamlRoot 双重重载：PickSingleFileAsync / PickMultipleFilesAsync / OpenSaveFileDialogAsync / PickFolderAsync）**｜api 平台 API｜must
  - 上游：`IFileDialog` COM（`FileDialogNative.cs`）+ Win32 owner hwnd。
  - 移植：先试 `Helpers.Portal.PickFilesAsync`（xdg-desktop-portal FileChooser，`Portal.cs` 361 行），未处理则退回 `kdialog`/`zenity`，`ToGlobs`（第 132-148 行）把 `(名称, .ext)` 转 glob。
  - 理由：Linux 没有 `IFileDialog`；portal 优先是为了在沙箱/Flatpak 下也能弹窗，KDE/GNOME 之外的老桌面才落到 kdialog/zenity。
- **第 116 行（缺 `PickFolderAsync(nint)` 重载）**｜api 平台 API｜reasonable
  - 上游：`PickFolderAsync(nint)` 与 `PickFolderAsync(XamlRoot)` 两个（第 251/301 行）。
  - 移植：只有 `XamlRoot?` 版本；全仓 `PickFolderAsync` 调用点（`ZZZGachaInfoWindow`、`InstallGameDialog`、`GameLauncherPage`、`GameLauncherSettingDialog`、`FindGamesDialog` 等）全部传 `XamlRoot`。
  - 理由：nint 重载在移植侧无消费者，Linux 上也没有需要传给对话框的 hwnd 语义；缺它不影响任何调用点。
- **第 21-45 行（DetectTool 探测失败不缓存）**｜eng 工程性｜avoidable
  - 上游：无此概念（COM 必存在）。
  - 移植：`_dialogTool ??= DetectTool()`，而 `DetectTool` 找不到工具时返回 null → 下次调用又从头 `kdialog --version` + `zenity --version`（各 `WaitForExit(2000)`）。
  - 理由：在既无 portal 又无 kdialog/zenity 的机器上，每次点「选择文件」白等最多 4 秒的两轮进程探测；应缓存「都没有」这个结果。
- **第 167-172 行（既无 portal 也无 kdialog/zenity 时只 Debug.WriteLine 后返回 null）**｜eng 工程性｜avoidable
  - 上游：对话框必弹。
  - 移植：`Debug.WriteLine("[FileDialogHelper] neither kdialog nor zenity is available")`，调用方拿到 null 后当场 `return`。
  - 理由：用户点「选择文件夹/另存为」屏幕上什么都不发生，Release 构建里连日志都没有；应在 UI 上给一次「缺 kdialog/zenity」提示（或退化成让用户手输路径）。

#### `Helpers/ClipboardHelper.Linux.cs`

- **第 12-30 行（SetStorageItems 改写成 text/uri-list，含 ToFileUri）**｜api 平台 API｜must
  - 上游：`DataPackage.SetStorageItems(...)`，由 Windows 剪贴板承载 CF_HDROP。
  - 移植：`string.Join("\r\n", paths.Select(ToFileUri))` + `TryWriteClipboard(uriList, "text/uri-list")`（B29），在 `Task.Run` 里执行。
  - 理由：Linux 文件管理器只认 `text/uri-list`；`file:///路径` 的百分号转义必须自己做，否则含空格/中文的路径粘到别的程序里会断。
- **第 33-68 行（TryWriteClipboard：wl-copy → xclip → xsel 三后端）**｜api 平台 API｜must
  - 上游：`Clipboard.SetContent`。
  - 移植：`wl-copy --type <mime>`（Wayland）→ `xclip -selection clipboard -t <mime>` → `xsel`，统一 `WaitForExit(2000)`，全失败才 `Debug.WriteLine("[ClipboardHelper] no clipboard tool available")`。
  - 理由：Wayland/X11 两套协议没有统一 API；三后端是覆盖 KDE/GNOME/X11 老桌面的最低成本方案。
- **第 71-121 行（SetImageFile：wl-copy 二进制喂流 / xclip -i）**｜api 平台 API｜reasonable
  - 上游：`SetBitmap(IRandomAccessStream / IStorageFile / Uri)` 三个重载。
  - 移植：只做「给一个文件路径，塞进剪贴板的 image/png」；截图复制走 `Features/Screenshot/ScreenCapture.Linux.cs:126`。
  - 理由：移植侧的图片来源全部是磁盘文件，没必要复刻 stream/Uri 三份重载；牺牲的是「内存里的图直接贴」这种上游用法。
- **第 125-133 行（SetText 改为 `Task.Run(() => TryWriteClipboard(value))`）**｜bugfix 修上游缺陷｜must
  - 上游：同步 `Clipboard.SetContent`，Windows 上不阻塞。
  - 移植：进程内 `WaitForExit(2000)` 若留在 UI 线程会卡窗（B60 / L-127）。
  - 理由：wl-copy 在部分合成器上会等剪贴板被接管，同步调用等于把界面冻住最多 2 秒。
- **全文件（缺 `SetBitmap` 3 重载与 `GetTextAsync`）**｜cut 裁剪｜avoidable
  - 上游：`SetBitmap`（第 26/38/50 行）与 `GetTextAsync`（第 75 行）。
  - 移植：无这两个 API；`SetBitmap` 的调用点全在被 `<Compile Remove>` 排除的文件里（`BBSWebBridge.xaml.cs:439`、`ImageViewWindow2.xaml.cs:1135`），`GetTextAsync` 全仓无调用点。
  - 理由：截图贴图已由 `SetImageFile` 覆盖，缺口只剩「BBS 图床贴图」与「读剪贴板文本」两条，而它们所属的整份功能在移植侧本来就是挖空的；旧账判 `avoidable` 成立但优先级极低——补 API 之前得先有调用者。

#### `Helpers/DriveHelper.Linux.cs`

- **第 11-37 行（IsDeviceRemovableOrOnUSB：/proc/mounts + /media、/run/media、/mnt 前缀启发式）**｜api 平台 API｜reasonable
  - 上游：`GetDriveType` + `IOCTL_STORAGE_QUERY_PROPERTY` 问设备总线类型。
  - 移植：按挂载点前缀猜「可移动/USB」。
  - 理由：Linux 没有稳定的用户态「这根盘是不是 USB」接口；前缀法会把 `/mnt/data`（内置盘）误判成可移动。活调用点 `GameLauncherService.cs:158`、`GameLauncherSettingDialog.xaml.cs:282` 只用来提示「装在移动盘上可能变慢/路径会变」，误判后果可接受。
- **第 38-70 行（GetDriveType 从文件系统名映射，永不返回 Removable）**｜api 平台 API｜reasonable
  - 上游：`DriveType.Removable/Fixed/Network/Ram`。
  - 移植：`nfs/cifs/smb` → Network、`tmpfs/ramfs` → Ram、其余 Fixed；从不返回 Removable。
  - 理由：活调用点只有 `InstallGameDialog.xaml.cs:350` 判 `is DriveType.Network`（提示网络盘安装风险），Removable 无消费者；要真判可移动得遍历 `/sys/block/*/removable`，超出现有收益。
- **第 91 行（GetDriveFormat 恒返回空串）**｜api 平台 API｜reasonable
  - 上游：`GetVolumeInformation` 返回 NTFS/FAT32/exFAT。
  - 移植：`=> ""`。
  - 理由：全仓**无任何调用点**（旧账说「NTFS 硬链接优化被短路」，该理由已随 0.19.0 的硬链接判定改写而失效）；保留只为满足 API 面。
- **第 93-108 行（GetDriveAvailableSpace 走 GetMountPoint + DriveInfo）**｜bugfix 修上游缺陷｜must
  - 上游：`DriveInfo(path).AvailableFreeSpace`（Windows 上即卷）。
  - 移植：先 `GetMountPoint(path)` 取最长匹配挂载点再 `DriveInfo(mount).AvailableFreeSpace`（D95 / B09）。
  - 理由：`DriveInfo("/home/x/games")` 在 Linux 上对非挂载路径会退化成根文件系统，空间判断会算成整盘（B09）。活调用点 `InstallGameDialog.xaml.cs:328`、`PreDownloadDialog.xaml.cs:196`、`RuntimeEnvironmentSetting.Components.cs:177` 都靠这个数字决定「空间是否够」。
- **第 143-224 行（IsDeviceSeekPenalty：/sys/block/<dev>/queue/rotational，dm 看 slaves）**｜api 平台 API｜reasonable
  - 上游：`IOCTL_STORAGE_QUERY_PROPERTY` 的 `SeekPenalty`。
  - 移植：`/proc/self/mounts` 最长匹配 → `/sys/block/<dev>/queue/rotational`；device-mapper 走 `slaves/` 逐个子设备。
  - 理由：只影响 `InstallGameDialog.xaml.cs:330` 的「固态/机械」提示文案，判不准也不影响功能；dm/LVM 那一层不处理会把机械盘上的 LVM 卷全报成固态。
- 旧账失效：`全文件 L1-110 四方法（… GetDriveAvailableSpace 用 Path.GetPathRoot 有 bug）｜api｜must` —— 文件已扩到 224 行、方法集不同；且「Path.GetPathRoot 有 bug」这条已修（第 93-108 行改 `GetMountPoint`，D95 / B09）。

#### `Helpers/XamlRootExtension.Linux.cs`

- **第 9 行（GetWindowHandle 恒返回 0）**｜api 平台 API｜reasonable
  - 上游：`WindowNative.GetWindowHandle` 返回 HWND。
  - 移植：`=> 0`。
  - 理由：活调用点 `MainView.xaml.cs:296`（热键初始化）、`Setting/ScreenshotSetting.xaml.cs:29` 只需一个非空宿主标识，移植侧热键走 X11 轮询、不依赖 hwnd。
- **第 12-40 行（GetAppWindow：ContentIslandEnvironment → WindowBase.FindAppWindow → MainWindow.Current 三级回落）**｜bugfix 修上游缺陷｜must
  - 上游：`WindowId.FromWindowHandle` 一步到位。
  - 移植：先问 `xamlRoot.ContentIslandEnvironment.AppWindowId`，异常时用 `Features.WindowBase.FindAppWindow(xamlRoot)` 反查所属窗口，最后才回落主窗口。
  - 理由：L-183 之前无条件回落主窗口，子窗口/对话框（`GameLauncherSettingDialog`、`GenshinHDRLuminanceSettingDialog`）拿到的都是主窗口 AppWindow → `DisplayInformation.CreateForWindowId(...)`（`GameLauncherSettingDialog.xaml.cs:755`）按主窗口算 DPI，多屏不同缩放时字号错乱。
- **第 43 行（SetWindowDragRectangles 空实现）**｜engine 引擎能力｜reasonable
  - 上游：两个 `params RectInt32[]` / `params Rect[]` 重载，把可拖拽区交给 `AppWindowTitleBar`。
  - 移植：`(this XamlRoot?, Rect[]?) {}`，只保留一个签名。
  - 理由：Uno/Skia 下没有 `AppWindowTitleBar.SetDragRectangles`，拖动区由 `Features/WindowBase.Linux.cs` 的 X11 侧逻辑负责；调用点 `MainView.xaml.cs:421` 传的矩形在 Linux 上无意义。留空方法比删调用点改动面小。
- **全文件（缺 `GetUIScaleFactor`）**｜cut 裁剪｜avoidable
  - 上游：`GetUIScaleFactor(this XamlRoot) => xamlRoot.RasterizationScale`（第 47 行）。
  - 移植：无。
  - 理由：唯一调用点在主仓里已不存在（旧账说是在被排除的 `AppBackground.xaml.cs`，复核成立），全仓无活调用点；将来若有人按上游代码搬过来会直接编译失败。

#### `Helpers/SystemBackdropHelper.Linux.cs`

- **第 10-56 行（SystemBackdropProperty 去掉 `required`、固化为 AcrylicDefault/MicaDefault/MicaAltDefault 三个静态预设）**｜engine 引擎能力｜reasonable
  - 上游：属性带 `required`，由 `SetControllerProperties` 按主题（Light/Dark 两套 Tint/Fallback/Luminosity）动态填。
  - 移植：`record` + `init`，三个静态预设（`MicaAltDefault.TintOpacityLight = 0`）。
  - 理由：移植侧只用到托盘一处，`with { }` 改写预设比复刻整套 controller 属性便宜。
- **第 72-84 行（TrySetAcrylic/TrySetMica 的参数语义从 alwaysActive/useMicaAlt 变成 isDark，且返回实例支持链式）**｜engine 引擎能力｜reasonable
  - 上游：`bool TrySetMica(bool useMicaAlt = false, bool fallbackToAcrylic = false, bool alwaysActive = false)`、`bool TrySetAcrylic(bool alwaysActive = false)`。
  - 移植：`TrySetAcrylic(bool isDark = true)` / `TrySetMica(bool isDark = true)`，返回 `SystemBackdropHelper`；`ResetBackdrop()` / `SetBackdropProperty()` 未提供。
  - 理由：Mica/Acrylic 本身是 DWM 能力，Linux 上只剩「铺一个底色」，参数只能退化为「用深色还是浅色底色」。风险是调用点按上游语义传 `true`。
- **第 86-106 行（Apply 只取 FallbackColor，按 Content 是 Panel/Control 分别设 Background）**｜engine 引擎能力｜reasonable
  - 上游：`SystemBackdrop = MicaController/AcrylicBrush` 交给合成器。
  - 移植：`new SolidColorBrush(_isDark ? FallbackColorDark : FallbackColorLight)` 直接赋给 `_window.Content` 的 `Background`（L-184）。
  - 理由：没有 DWM 就只能不透明底色；这是「壁纸透字」类问题的根因，但确实无解。
- **`Features/ViewHost/SystemTrayWindow.xaml.cs:34-38`（唯一的活调用点传 `true`，被当成 isDark，浅色主题下托盘恒为深灰底）**｜cosmetic 外观文案｜avoidable
  - 上游：同一行 `TrySetAcrylic(true)`，参数是 `alwaysActive`，深浅由 controller 按主题自己选。
  - 移植：`new SystemBackdropHelper(this, AcrylicDefault with { TintColorLight = 0xFFE7E7E7, TintColorDark = 0xFF404040 }).TrySetAcrylic(true)` → `_isDark = true`；而 `SystemTrayWindow.Show()`（同文件第 87 行）会按 `ShouldSystemUseDarkMode()` 设 `RootGrid.RequestedTheme`。
  - 理由：浅色主题下托盘菜单是深灰底（0xFF545454）+ 浅色文字，与系统主题相反；深色主题下反而正好。修法：把调用点改成 `TrySetAcrylic(ShouldSystemUseDarkMode())`，或在 `Show()` 里重设。旧账「唯一调用点传 true 恰好两种语义都合理」被此推翻。

#### `Helpers/DataFolder.Linux.cs`

- **第 13-76 行（AppFolderName = "lunarward" + DataHome + Unify 统一大写目录）**｜linux 专属实现｜reasonable
  - 上游：无此文件；数据目录由 `ApplicationData.Current.LocalFolder` 给出。
  - 移植：`XDG_DATA_HOME`（默认 `~/.local/share`）下用小写 `lunarward`；`Unify()` 在大写 `Lunarward` 不存在时建符号链接，已存在真实目录时递归搬文件再建链，异常只 `Serilog.Log.Warning(ex, "data folder: unify failed")`。
  - 理由：Uno 的 `ApplicationData` 目录名取程序集名（大写 L），而移植侧早先自用的是小写目录（D56）；不统一就会出现两份配置、用户看着像「设置丢了」。`Unify` 失败只记日志是可以的（数据不会丢，只是分裂），但搬家过程没有进度提示，大目录（日志/截图）首次启动会明显卡一下。

#### `Helpers/WindowWaitAsync.Linux.cs`

- **第 8-17 行（自实现 Window.WaitAsync 扩展：等 Closed 事件返回 Task<bool>）**｜api 平台 API｜must
  - 上游：用 `Scighost.WinUI 0.1.0` 提供的 `Window.WaitAsync()`（上游 `Starward.csproj:52` 引用该包）。
  - 移植：`WindowWaitAsyncExtension.WaitAsync(this Window)` 自行挂 `Closed` → `TrySetResult(true)`。
  - 理由：移植侧不引 `Scighost.WinUI`（Windows App SDK 依赖），缺这个扩展 `App.xaml.cs:108/171`（`NoPermissionWindow`）与 `App.xaml.cs:86`（`WelcomeWindow`，该窗口自带同名方法）都要改调用方式。旧账判 `avoidable` 偏严：API 来源整体缺失，属必需替代，改判 `must`。

#### `Features/Update/UpdateService.Linux.cs`

- **第 16-60 行（CheckUpdateAsync/GetLatestVersionAsync 走 AUR RPC，且丢弃 disableIgnore 参数）**｜cut 裁剪｜reasonable
  - 上游：查 GitHub Releases，按 `disableIgnore` 决定是否跳过用户忽略的版本。
  - 移植：查 AUR 的 `lunarward-bin` 版本号；`disableIgnore` 参数接收后不用（忽略版本列表在 Linux 上无意义——更新由包管理器完成）。
  - 理由：D5 定了「更新交给包管理器」，提示新版本只为告诉用户 `yay -Syu`；忽略列表不参与。
- **第 62-103 行（DownloadSetupAsync 只写 Debug 日志）**｜cut 裁剪｜reasonable
  - 上游：下载安装器 + 校验 + 启动。
  - 移植：`Debug.WriteLine` 后返回。
  - 理由：无安装器可下；但注意 `UpdateWindow` 里的「立即更新」按钮在 Linux 上是靠 `IsUpdateNowEnabled => true` + 手动下载提示（B59）兜的，两处语义要一起看。

#### `Features/Update/SetupService.Linux.cs`

- **第 9-18 行（SetupService 空实现：ctor 不启动任何东西，SetupTotalBytes/SetupDownloadBytes 恒 0）**｜cut 裁剪｜must
  - 上游：147 行，真实下载 `Starward.Setup`、校验签名、`Process.Start` 安装器。
  - 移植：文件头注释「D5 决定不移植 Starward.Setup」；只有构造与两个恒 0 属性。
  - 理由：`Starward.Setup` 是 Windows 更新器；类型必须存在（`UpdateService` 与 DI 图仍引用），行为必须为空。属「必须有、必须不做事」。

#### `Features/Update/UpdateWindow.Linux.cs`

- **第 15-155 行（整窗裁剪：上游 773 行 → 155 行，去掉自动下载/安装/进度联动）**｜cut 裁剪｜reasonable
  - 上游：完整更新流程（进度、取消、自动重启）。
  - 移植：只留「有新版本 + 手动下载提示 + 跳到发布页（B28 按 Tag 分 release/package）+ 忽略此版本（B08）」。
  - 理由：D5；窗口仍要存在是因为 `MainView.xaml.cs:624` 与 `AboutSetting.xaml.cs:69` 会在发现新版本时弹它。
- **第 15-47 行（类上没有 `[INotifyPropertyChanged]`，属性全是普通自动属性）**｜engine 引擎能力｜avoidable
  - 上游：`[INotifyPropertyChanged] public sealed partial class UpdateWindow`，属性用 `SetProperty(ref field, value)`。
  - 移植：普通 `{ get; set; }`。
  - 理由：XAML 原封不动用 `x:Bind`（`UpdateWindow.xaml:141/153/175`），OneWay 绑定失去了通知源 → 运行时改属性界面不动，只有 `Activate()` 之前设好的值生效。
- **第 107-115 行（SetErrorMessage 用 `FindName("TextBlock_ErrorMessage")` 直接改控件）**｜eng 工程性｜avoidable
  - 上游：改 `ErrorMessage` 属性，绑定自己刷新。
  - 移植：绕开绑定硬找控件改 `Text`/`Visibility`（L-183 的补丁）。
  - 理由：这是对上一行缺陷的局部补丁，只在 `UpdateNow()` 这一条路径生效，且控件名变更（改 XAML）会静默失效；修好通知就该删掉它。
- **第 117-134 行（Retry()/CheckUpdateAsync 直接赋 `ErrorMessage`，失败文案永不显示）**｜eng 工程性｜avoidable
  - 上游：赋值即刷新。
  - 移植：`Retry() => _ = CheckUpdateAsync();`，`CheckUpdateAsync` 内部 `ErrorMessage = ex.Message`（第 128/132 行）走普通属性。
  - 理由：用户点「重试」时界面毫无变化（第 153 行的错误区仍不可见），只能凭窗口不关猜「没成功」。这是本节点最直接的静默失败。
- **`UpdateWindow.LinuxAuto.cs` 第 14-15 行（Button_UpdateRemindLatter_PointerEntered/PointerExited 空方法体）**｜eng 工程性｜reasonable
  - 上游：code-behind 里同名事件处理（悬停改前景色）。
  - 移植：空体，仅为满足 XAML 的 `x:Bind` 事件签名（`UpdateWindow.xaml.cs` 被 `Lunarward.csproj:147` 排除）。
  - 理由：悬停换色是纯视觉，Uno 下按钮自带指针态；留空可接受，但空事件处理器容易被当成「漏实现」。

#### `Features/Overlay/RunningGameService.Linux.cs`

- **第 12-38 行（内联 RunningGame 类）、第 40-68 行（AddRuninngGame / GetRunningGameCount / GetLatestActiveGame 的进程表实现）**｜linux 专属实现｜must
  - 上游：`RunningGameService.cs` + 独立 `RunningGame.cs`，靠 `Process` 句柄与 `WindowHandle`。
  - 移植：一个文件内联 `internal class RunningGame`（GameBiz/Process/StartTime/WindowHandle/IsActive/HasExited）；按 pid 去重登记，`HasExited` 捕获异常返回 true。
  - 理由：旧账把它整份判成 `cut 裁剪` 是错的——它有活调用点：`CloudGameButton.xaml.cs:135`、`GameLauncherPage.xaml.cs:623/1632` 登记，`ScreenCaptureService.cs:73` 读取。是「保留上游 API、重写实现」，应改判为 linux 专属实现。
- **第 78 行（OpenOverlayWindow 恒返回 false）**｜cut 裁剪｜reasonable
  - 上游：`DeviceLostHelper` + DirectComposition 叠一层透明覆盖窗显示 FPS/延迟。
  - 移植：`=> false`。
  - 理由：悬浮窗在 AGENTS.md 里明确列为 M4 占位（Linux 侧没有可叠加的透明层方案）；返回 false 让调用方安静跳过，不抛异常。
- **第 12-38 行（RunningGame.WindowHandle 恒 0：移植侧无任何赋值点）**｜cut 裁剪｜reasonable
  - 上游：登记时填 HWND，供截图/覆盖窗定位。
  - 移植：`nint WindowHandle { get; set; }` 从未被赋非零值；`ScreenCaptureService.cs:73-88` 拿它调 `User32.IsIconic/GetWindowText` 并据此截窗口。
  - 理由：窗口截图（`Ctrl+PrtSc` 截游戏窗口）在 Linux 上不可用，属 M4 缺口；但调用链上没有任何 null 检查，`WindowHandle=0` 只会静默走「没最小化 → 开始截」然后失败在 GraphicsCapture。要么在这里显式拒绝（返回 null 让上层提示不支持），要么记成已知缺口。

#### `Features/RPC/RpcService.Linux.cs`

- **第 17-43 行（RpcService 占位：CheckRpcServerRunning 恒 false、EnsureRpcServerRunningAsync 恒 false、TrySetEnviromentAsync 只 LogDebug、CreateRpcClient 抛 NotSupportedException）**｜cut 裁剪｜reasonable
  - 上游：命名管道 + `Authorization` 头 + `ClientBase<T>` 的 Discord RPC 服务端。
  - 移植：全部是空实现，`CreateRpcClient<T>()` 直接 `throw new NotSupportedException("RPC is not available on Linux yet (see D6)")`。
  - 理由：移植侧无任何活调用点（`RpcService.cs`/`RpcRunner.cs` 都被 `<Compile Remove>` 排除，DI 里也没注册），占位只是为了让引用它的代码编译通过；旧账判 `must` 过严，改判 `reasonable`——真正 must 的是「不要静默假装在跑」，而抛异常满足这一点。
- **`App.xaml.cs:290-296`（`rpc` 子命令直接 `Environment.Exit(0)`）**｜cut 裁剪｜reasonable
  - 上游：`rpc` 子命令跑 `RpcServerHost`。
  - 移植：命中 `rpc` 参数就退出，不提示。
  - 理由：上游的 `PlayStation`/Discord 状态同步在 Linux 上没落地；用户手动敲 `lunarward rpc` 只会看到进程立刻退出，没有任何说明——建议至少打一行「RPC 未移植（D6）」。

#### `Features/GamepadControl/GamepadController.Linux.cs`

- **第 11-37 行（整份占位：Initialize/Start/Stop 空体，Initialized 恒 false，NeedInstallGameInputRedist 恒 false，GameInputRedistInstalled 恒 true）**｜cut 裁剪｜reasonable
  - 上游：~1000 行，`GameInput` API + 手柄到键鼠/虚拟键映射 + GameBar 钩子。
  - 移植：全部空实现。
  - 理由：手柄映射在 AGENTS.md 里列为 M4 占位；`GameInputRedistInstalled => true` 让设置页不弹「请安装 GameInput」。
- **第 21-31 行（键位映射属性从 `VirtualKeyCode[]` 改成 `(string[] Modifiers, string[] Keys)`）**｜api 平台 API｜reasonable
  - 上游：`VirtualKeyCode[]`（Vanara/Windows 虚拟键枚举）。
  - 移植：字符串数组，`Get…Text()` 恒空、`KeyNameToVirtualKey` 空字典。
  - 理由：Windows 虚拟键码在 Linux 无对应物；改成字符串是为了让设置页的 XAML/绑定能编译。
- **第 39-49 行（Set…MapKeys 返回 `Gamepad_NotSupportedOnLinux` 作为错误键）**｜cosmetic 外观文案｜avoidable
  - 上游：返回 `Lang.Gamepad_xxx` 真实错误文案。
  - 移植：`Gamepad_NotSupportedOnLinux` 不是任何 Lang 资源键（全仓只在第 41/47 行出现），`GamepadControlSetting.xaml.cs:111` 把它拼进 `GamepadGuideButtonKeyTextEditError`。
  - 理由：用户在手柄设置里改键会看到「未识别的按键: Gamepad_NotSupportedOnLinux」（英文键名直出）。要么加资源键，要么整页隐藏改键入口。

#### `Helpers/Portal.cs`（补登：`FileDialogHelper.Linux.cs` 的 portal 后端）

- **第 1-361 行（xdg-desktop-portal FileChooser 客户端：PickFilesAsync + PortalPickResult）**｜linux 专属实现｜must
  - 上游：无（`IFileDialog`）。
  - 移植：经 D-Bus 调 `org.freedesktop.portal.FileChooser`，返回 `Handled` 标志区分「portal 处理了」与「没人处理、回落 kdialog/zenity」。
  - 理由：Flatpak/沙箱与 GNOME 上唯一合法弹窗方式；`Handled` 语义是回落逻辑的前提，不能省。

#### `Helpers/PosixIdentity.cs`（补登：文件身份与 root 防护）

- **第 1-92 行（IsRoot / ThrowIfRoot / TryGetFileIdentity(st_dev, st_ino)）**｜linux 专属实现｜must
  - 上游：`GetFileInformationByHandleEx(FILE_ID_INFO)` 做硬链接去重，管理员判断用 token。
  - 移植：`st_dev + st_ino` 拼身份键（B27）；`ThrowIfRoot` 保护前缀目录写入（`PrefixManager.cs:306/430`）、`IsRoot` 用于预检提示（`LaunchPreflight.cs:86`）。
  - 理由：跨文件系统/硬链接/符号链接下 `(长度, 修改时间, 路径哈希)` 会误判同一文件；root 跑 wine 会把前缀写成 root 所有，之后普通用户启动全失败。

#### `Features/ViewHost/MainView.xaml.cs`（补登：GameSelector 残留引用）

- **第 118-140 行（TryCloseGameSelector 及第 272/276 行注释仍指向已不存在的 GameSelector）**｜eng 工程性｜avoidable
  - 上游：Esc 关闭选择层（0.19.0 里是 `GameSwitcher` 的下拉/弹出）。
  - 移植：函数体只剩「关掉第一个打开的 Popup」，XML 注释仍写「先把『游戏选择层』…收掉」；第 272/276 行注释称「正常导航仍由 GameSelector 负责」「GameSelector 恢复选中会再导航一次」。
  - 理由：行为本身没问题（关 Popup 仍然有用），但三处注释指向死组件，下一个人会去改不存在的代码。随 GameSelector 三份文件一起清理。

### 3.10 脚本命中但只属格式或单行的同名文件（4 个，不逐点计数）

`scripts/list-deviations.sh` 命中 80 个同名差异文件，其中 76 个已在 §3.1–§3.9 逐条记录，剩下 4 个差异太小、不值得单独列偏离点，记在这里备查：

| 文件 | 差异 | 判定 |
|---|---|---|
| `Features/Passport/HypLoginWindow.xaml.cs` | `Activate()` 里 `User32.SetWindowLong(WindowHandle, GWL_HWNDPARENT, ParentWindowHandle)` 包进 `OperatingSystem.IsWindows()`（9 行，含 `LUNARWARD-DEVIATION` 注释） | api 平台 API / must：X11 下窗口父子关系由 `Helpers/X11Window` 管，Win32 那段在 Linux 不该执行 |
| `Features/Update/UpdateWindow.xaml` | 错误文案 `TextBlock` 加 `x:Name="TextBlock_ErrorMessage"`（5 行） | eng 工程性 / reasonable：`UpdateWindow.Linux.cs` 的 `SetErrorMessage` 需要引用它，上游没有对应成员 |
| `Features/UrlProtocol/UrlProtocolService.cs` | `User32.MessageBox(HWND.NULL, ex.Message, "Starward")` → `"Lunarward"`（4 行） | cosmetic 外观文案 / must：分叉后错误对话框不该再标上游名 |
| `Features/PlayTime/PlayTimeStatsDialog.xaml.cs` | 去掉文件头 UTF-8 BOM（4 行） | 纯格式，不算偏离 |

## 4 · 纯新增文件（不是偏离，但同名文件的行为由它们兜底）

移植树里上游没有的文件（不含 `*.Linux.cs`，那是 §3 的替代实现），共 **110** 个，按目录统计：

| 目录 | 新增文件数 | 例子 |
|---|---|---|
| `Features/Runtime` | 27 | `ArchiveExtractor.cs`、`ComponentDownloadMath.cs`、`ComponentDownloader.cs` |
| `Helpers` | 20 | `AppIcon.cs`、`CliSpec.cs`、`DesktopNotification.cs` |
| `Features/GameRecord` | 8 | `BattleChronicleWindow.LinuxAuto.cs`、`GameRecordRoleChangedMessage.cs`、`GameRecordService.cs` |
| `Features/GameLauncher` | 7 | `GameAuthLoginButton.xaml`、`GameAuthLoginButton.xaml.cs`、`GameAuthLoginService.cs` |
| `Features/Screenshot` | 7 | `ImageViewWindow.LinuxAuto.cs`、`ScreenshotConverter.cs`、`ScreenshotItem.Selection.cs` |
| `Features/Setting` | 6 | `RuntimeEnvironmentSetting.Components.cs`、`RuntimeEnvironmentSetting.Enhancements.cs`、`RuntimeEnvironmentSetting.Profiles.cs` |
| `Controls` | 5 | `ImageExCompat.cs`、`InteractivityCompat.cs`、`ToolkitCompat.cs` |
| `Features/Background` | 4 | `BackgroundChangedMessage.cs`、`VideoBackgroundPlayer.cs`、`VideoBgVolumeChangedMessage.cs` |
| `Features/Companion` | 4 | `CompanionAutoStart.cs`、`CompanionEntry.cs`、`CompanionRunner.cs` |
| `Features/GameSelector` | 4 | `GameBizDisplay.cs`、`GameBizIcon.cs`、`GameSelector.xaml` |
| `Features/GameAccount` | 3 | `RegistryFormat.cs`、`WineRegistry.cs`、`WineRegistryFiles.cs` |
| `Features/ViewHost` | 3 | `CurrentGameChangedMessage.cs`、`NoPermissionWindow.xaml`、`NoPermissionWindow.xaml.cs` |
| `Features/App` | 2 | `DevSwitches.cs`、`SingleInstancePipe.cs` |
| `.` | 1 | `LinuxConfig.cs` |
| `Compat` | 1 | `VanaraHotkeyCompat.cs` |
| `Compat/NotifyIcon` | 1 | `TaskbarIconCompat.cs` |
| `Features/Dev` | 1 | `UiSelfTest.cs` |
| `Features/GameInstall` | 1 | `GamePackageService.cs` |
| `Features/GameRecord/ZZZ` | 1 | `ElementWeaknessConstants.cs` |
| `Features/Overlay` | 1 | `OverlayWindow.LinuxAuto.cs` |
| `Features/Tray` | 1 | `StatusNotifierTray.cs` |
| `Features/Update` | 1 | `UpdateWindow.LinuxAuto.cs` |
| `Properties` | 1 | `AssemblyVersion.cs` |

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

## 附录 B · `avoidable` 清单（97 条，需要逐条决定）

> 2026-11-25 全量重算后按新类别重新生成。规则：这些与平台无关，本可以照上游写。

| 模块 | 文件 | 位置 | 类别 |
|---|---|---|---|
| 启动器与游戏入口 | `Features/GameLauncher/GameBannerAndPost.xaml.cs` | **第 377-405 行（`#if DEBUG` + `LUNARWARD_DEV_BANNER_AUTOLINK` 钩子）** | eng |
| 启动器与游戏入口 | `Features/GameLauncher/GameLauncherPage.xaml` | **第 57-141 行（上游一行 `sfb:BackgroundSwitcher` → 页面内联一份约 85 行的切换条：渐变底 `#00000000→#88000000`、`Height="60"`、`Opacity="0"` + `OpacityTransition`、`PointerEntered/Exited`、`PointerWheelChanged`、图库/复制/另存为/播放暂停按钮）** | eng |
| 启动器与游戏入口 | `Features/GameLauncher/GameLauncherPage.xaml.cs` | **第 778-790 行（`GetPreflightActionText` 返回硬编码中文：打开游戏设置 / 打开运行环境 / 打开前缀目录 / 重建前缀 / 安装图形组件 / 查看占用进程 / 打开游戏目录）** | cosmetic |
| 启动器与游戏入口 | `Features/GameLauncher/GameLauncherPage.xaml.cs` | **第 1350-1618 行（`#region Switch Background Image`，被内联的切换条代码：`BackgroundImages` / `CurrentBackgroundImageIndex`（先 `Math.Clamp`）/ `RebuildBackgroundDots` / `UpdateBackgroundDots` / `UpdateVideoButtonState` / `InitializeBackgameImageSwitcherAsync` / `ChangeBackgroundImageIndex` / `PointerEntered`·`Exited`·`WheelChanged` / `OpenBackgroundViewWindow` / 复制·另存为·暂停，以及第 1484-1505 行 `#if DEBUG` 的 `LUNARWARD_DEV_BG_CYCLE`）** | eng |
| 启动器与游戏入口 | `Features/GameLauncher/GameLauncherService.cs` | **第 290-292 行（`GameConfigIni` 使用全限定名 `Starward.GameInstall.Model.GameConfigIni`）** | eng |
| 启动器与游戏入口 | `Features/GameLauncher/GameLauncherService.cs` | **第 738-745 行（遗留的无调用点 `private void SetGenshinEnableHDR(GameId gameId)`）** | eng |
| 启动器与游戏入口 | `Features/GameLauncher/GameNoticeService.cs` | **第 33-34 行（新增同步包装 `GetGameNoticeUrl(GameBiz)`）** | eng |
| 安装/更新/预下载 | `Features/GameInstall/GameInstallScanner.Linux.cs` | **第 193–238 行（ScanRoots / SetScanRoots / MarkDirty）、第 271–281 行（GetCandidateRoots）、第 274 行（LUNARWARD_GAME_ROOTS）** | linux |
| 安装/更新/预下载 | `Features/GameInstall/GameInstallService.cs` | **第 76–83 行（finally 分支）** | eng |
| 安装/更新/预下载 | `Features/GameInstall/GameInstallService.cs` | **第 228–247 行（AddOrUpdateProgress）** | eng |
| 游戏设置/运行环境/账号 | `Features/GameAccount/GameAccountService.cs` | **第 47 行（同上，读路径的注销）** | linux |
| 游戏设置/运行环境/账号 | `Features/GameSetting/GameSettingPage.xaml.cs` | **第 91 行、第 142-145 行（OnNavigatedTo 的 Stopwatch、OnLoaded 的滚动钩子）** | eng |
| 游戏设置/运行环境/账号 | `Features/GameSetting/GameSettingPage.xaml.cs` | **第 554 行（ApplySettingAsync 里的分辨率写入）** | eng |
| 游戏设置/运行环境/账号 | `Features/GameSetting/GameSettingService.cs` | **第 45、54、63-75、151-164、233、364 行（读路径取前缀）** | linux |
| 游戏设置/运行环境/账号 | `Features/GameSetting/GameSettingService.cs` | **第 73 行（GetGameResolutionSetting 的 nap 分支全屏标志）** | linux |
| 游戏设置/运行环境/账号 | `Features/GameSetting/GameSettingService.cs` | **第 327 行（SetGenshinEnableHDRAsync 的写入）** | linux |
| 游戏设置/运行环境/账号 | `Features/GameSetting/GameSettingService.cs` | **第 286-292 行（SetStarRailFPSIndexAsync 缺键时的兜底值）** | eng |
| 游戏设置/运行环境/账号 | `Features/GameSetting/GenshinHDRLuminanceSettingDialog.Linux.cs` | **第 83-101 行（硬编码中文提示与借用的标题）** | cosmetic |
| 外壳 | `App.xaml.cs` | **第 108、110 行（首跑自动识别游戏的分支）** | eng |
| 外壳 | `App.xaml.cs` | **第 285-329 行（HandleStartupAsync / ExpandUrlArgument）** | eng |
| 外壳 | `App.xaml.cs` | **第 517-528 行（ApplyAcrylicFallback）** | eng |
| 外壳 | `AppConfig.Configuration.cs` | **第 156-177 行（首次运行分支 + 缺 GameFileDatabase.SetDatabase）** | eng |
| 外壳 | `AppConfig.ServiceProvider.cs` | **第 187、189、210、212 行（User-Agent）** | cosmetic |
| 外壳 | `Features/ViewHost/MainView.xaml.cs` | **第 166-256 行（DevSwitches 页面直达 + 调试下载/安装对话框）** | eng |
| 外壳 | `Features/ViewHost/MainView.xaml.cs` | **第 663-688 行（InitializePassportUser 被系统代理检测逻辑覆盖）** | eng |
| 外壳 | `Features/ViewHost/MainWindow.xaml.cs` | **第 58、164-210 行（RunDebugWindowAction）** | eng |
| 外壳 | `Features/ViewHost/SystemTrayWindow.xaml` | **第 11 行（Title）** | cosmetic |
| 外壳 | `Features/ViewHost/SystemTrayWindow.xaml.cs` | **第 76 行（激活状态判断）** | eng |
| 外壳 | `Features/ViewHost/WelcomeWindow.xaml.cs` | **第 46 行（SetDragRectangles 调用写法）** | cosmetic |
| 外壳 | `Program.Linux.cs` | **第 18-72 行（没有任何 `rpc` / `playtime` / `startgame` 子命令分支）** | eng |
| 外壳 | `Program.cs` | **第 117 行（崩溃日志目录）** | eng |
| 截图与图库 | `Features/Screenshot/ScreenshotPage.xaml` | **第 51-53 行注释 / 整块缺失的右键菜单（上游 `:121-213` 的 `Grid.ContextFlyout`）** | cut |
| 截图与图库 | `Features/Screenshot/ScreenshotPage.xaml` | **第 78-81 行（`CachedImage` 少了 `IsThumbnail="True"`）** | eng |
| 截图与图库 | `Features/Screenshot/ScreenshotPage.Linux.cs` | **第 95-130 行（`LoadImages` 只枚举单一默认目录，排序与分组也换了）** | cut |
| 截图与图库 | `Features/Screenshot/ScreenshotPage.Linux.cs` | **第 220-236 行（`ManageScreenshotFolderCommand` = `xdg-open` 目录）** | cut |
| 截图与图库 | `Features/Screenshot/ScreenshotPage.Linux.cs` | **第 239-242、261-267 行（两个 XAML 里已无绑定的死事件处理器）** | eng |
| 截图与图库 | `Features/Screenshot/ScreenshotPage.Linux.cs` | **第 308-321 行（`BatchConvertAsync`：语义与格式设置都变了）** | cut |
| 截图与图库 | `Features/Screenshot/ScreenCapture.Linux.cs` | **第 60 行（文件名 `Lunarward_yyyy-MM-dd_HH-mm-ss.png`）** | cut |
| 截图与图库 | `Features/Screenshot/ScreenCapture.Linux.cs` | **第 53-68 行（忽略「保存格式 / 编码质量 / HDR→SDR」三个设置）** | cut |
| 截图与图库 | `Features/Screenshot/ScreenCaptureService.cs` | **第 405 行（`SaveAsSdrAsync` 里取原神 HDR 亮度）** | eng |
| 截图与图库 | `Features/Screenshot/ImageViewWindow.Linux.cs` | **第 3 行注释与实际引用不符（死窗口，`ImageViewWindow.xaml` 却仍编入）** | eng |
| 截图与图库 | `Features/Screenshot/ImageConvertItem.Linux.cs` | **第 6-13 行（属性从 14 个缩到 3 个）** | cut |
| 截图与图库 | `Features/Screenshot/ImageConvertItem.Linux.cs` | **第 1-13 行（唯一消费者不参与构建 → 死代码）** | eng |
| 截图与图库 | `Features/Screenshot/ScreenshotFolderManageDialog.xaml` | **第 1-158 行（整个对话框没有任何入口）** | eng |
| 抽卡/战绩 | `Features/Gacha/GachaLogPage.xaml.cs` | **第 26 行（`using Vanara.PInvoke;`）** | eng |
| 抽卡/战绩 | `Features/GameRecord/BattleChronicleWindow.Linux.cs` | **第 22 行（`Title = "战绩"`）** | cosmetic |
| 抽卡/战绩 | `Features/GameRecord/BattleChronicleWindow.Linux.cs` | **第 64–72 行（`ShowBattleChronicle(Window owner, GameRecordRole? role)`）** | eng |
| 抽卡/战绩 | `Features/GameRecord/BattleChronicleWindow.Linux.cs` | **第 1–2 行（头注释揭示的整体处理方式）** | eng |
| 抽卡/战绩 | `Features/GameRecord/GameRecordPage.xaml` | **第 490–506 行（新增空态层）** | cosmetic |
| 抽卡/战绩 | `Features/GameRecord/GameRecordPage.xaml.cs` | **第 105–160 行（新增 `CheckAgreementAsync()`）** | cosmetic |
| 抽卡/战绩 | `Features/GameRecord/GameRecordPage.xaml.cs` | **第 376–397、503–506 行（空态数据源 + 空态登录入口）** | cosmetic |
| 抽卡/战绩 | `Features/GameRecord/LoginPage.Linux.cs` | **第 1–8 行（文件头注释）** | cosmetic |
| 抽卡/战绩 | `Features/GameRecord/LoginPage.Linux.cs` | **第 60 行（`Send(new GameRecordRoleChangedMessage(...))`）** | eng |
| 抽卡/战绩 | `Features/GameRecord/LoginPage.Linux.cs` | **第 48、52、57、61、65 行（面向用户的状态文案）** | cosmetic |
| 背景与图片加载 | `Controls/CachedImage.Linux.cs` | **第 18 行 + 第 174-183 行（http 分支）** | engine |
| 背景与图片加载 | `Controls/CachedImage.Linux.cs` | **第 64-76 行（`UpdateClip` 只做矩形裁剪）** | cosmetic |
| 背景与图片加载 | `Controls/CachedImage.Linux.cs` | **第 300-302 行（空的 `private static class StreamExtensions { }`）** | eng |
| 背景与图片加载 | `Features/Background/AppBackground.Linux.cs` | **第 166-170 行（手搭树里没有渐变遮罩，也没有 ProgressBar）** | cosmetic |
| 背景与图片加载 | `Features/Background/AppBackground.Linux.cs` | **第 34 / 52 / 311 / 582 / 991-992 行（五个 `LUNARWARD_*` 环境变量）** | linux |
| 背景与图片加载 | `Features/Background/AppBackground.Linux.cs` | **第 58-62 行（`Current` 惰性 getter）** | eng |
| 背景与图片加载 | `Features/Background/BackgroundService.cs` | **第 312-320 行（视频分支只按扩展名放行，不再真正打开）** | cut |
| 背景与图片加载 | `Features/Background/BackgroundService.cs` | **第 315 行（`GetImagePixelSizeAsync(file).GetAwaiter().GetResult()` 嵌套同步等待）** | eng |
| 背景与图片加载 | `Features/Background/BackgroundService.cs` | **第 256-259 行（校验放在「是否已在 bg 目录」判断之前）** | eng |
| 背景与图片加载 | `Features/Codec/ImageThumbnail.cs` | **第 31 行（唯一的实质改动：`CacheFolder` 改 `AppConfig.CacheDirectory` 并改成只读属性）** | eng |
| 设置界面 | `Features/Setting/AdvancedSetting.xaml` | **第 19、23–25、31、37 行（诊断区的标题/说明/两个按钮文案）** | cosmetic |
| 设置界面 | `Features/Setting/AdvancedSetting.xaml.cs` | **第 47–52 行（上述对话框的标题与三个按钮）** | cosmetic |
| 设置界面 | `Features/Setting/AdvancedSetting.xaml.cs` | **第 107–115 行（`Button_OpenDiagnoseFolder_Click`）** | api |
| 设置界面 | `Features/Setting/AdvancedSetting.xaml.cs` | **第 125–135 行（`OpenOutputFolder`）** | api |
| 设置界面 | `Features/Setting/DownloadSetting.xaml.cs` | **第 5、11、53–60 行（残留脚手架）** | eng |
| 设置界面 | `Features/Setting/FileManageSetting.xaml` | **第 55–61 行（备份/数据库区的长路径 `TextBlock`）** | cut |
| 设置界面 | `Features/Setting/GamepadControlSetting.xaml` | **第 14、101、109、113、128、159 行（`XboxFluent2` 字体资源与 5 处图标）** | engine |
| 设置界面 | `Features/Setting/GeneralSetting.xaml` | **第 46 行（「关闭窗口时」`RadioButtons` 的「隐藏到系统托盘」项）** | cosmetic |
| 设置界面 | `Features/Setting/GeneralSetting.xaml.cs` | **第 186–198 行（`Hyperlink_VisualEffects_Click`）** | cut |
| 设置界面 | `Features/Setting/ScreenshotSetting.Linux.cs` | **第 63–67 行（构造函数）** | eng |
| 设置界面 | `Features/Setting/ScreenshotSetting.Linux.cs` | **第 52–56、75–83 行（`AutoConvertScreenshotToSDR` / `EnableScreenshotColorManagement` / `AutoCopyScreenshotToClipboard`）** | cut |
| 设置界面 | `Features/Setting/ScreenshotSetting.xaml` | **第 77–85 行（截图快捷键说明文本）** | cosmetic |
| 设置界面 | `Features/Setting/ScreenshotSetting.xaml` | **第 119 行（`Segmented` 的可见性绑定）** | eng |
| 设置界面 | `Features/Setting/ScreenshotSetting.xaml` | **第 166–169 行（`TextBlock_CaptureError`）** | cut |
| 设置界面 | `Features/Setting/SettingPage.xaml` | **第 14–16 行（`Page.Resources`）** | eng |
| 服务与帮助器 | `Features/PlayTime/PlayTimeRecordService.cs` | **第 294-302 行（SavePlayTimeStats 两条日志的 interruption 实参恒为 false）** | eng |
| 服务与帮助器 | `Features/PlayTime/PlayTimeSession.Linux.cs` | **第 192-227 行（HasNamedProcessForPrefix 里的内层 `catch {}`）** | eng |
| 服务与帮助器 | `Features/Database/DatabaseService.cs` | **第 113-115 行（产物是 ZIP，扩展名与清理通配符仍写 .7z）** | cosmetic |
| 服务与帮助器 | `Features/GameSelector/GameBizDisplay.cs` | **第 1-30 行（整份类型上游已删、移植侧仍编译）** | eng |
| 服务与帮助器 | `Features/GameSelector/GameSelector.xaml` | **第 1-574 行（整份 XAML 上游已删，移植侧仍编译）** | eng |
| 服务与帮助器 | `Features/GameSelector/GameSelector.xaml.cs` | **第 1-1202 行（整份 code-behind 上游已删，移植侧仍编译）** | eng |
| 服务与帮助器 | `Features/GameSelector/GameSelector.xaml.cs` | **第 75-112 行（0.19.0 旧键迁移：SelectedGameBizs / CachedGameInfo / IsGameBizSelectorPinned / CurrentGameBiz → CurrentGameId）** | eng |
| 服务与帮助器 | `Features/UrlProtocol/UrlProtocolService.Linux.cs` | **第 80-94 行（UnregisterProtocol 摘掉 .desktop 但不撤销 xdg-mime 默认值）** | eng |
| 服务与帮助器 | `Features/UrlProtocol/UrlProtocolService.Linux.cs` | **打包侧（`packaging/build-appimage.sh:28-31` 只 install .desktop，未跑 update-desktop-database / 无集成分发步骤）** | eng |
| 服务与帮助器 | `Features/UrlProtocol/TestUrlProtocolWindow.xaml` | **`TestUrlProtocolWindow.xaml.cs` 被 `Lunarward.csproj:134` 排除、无 `.Linux.cs` 取代（第 1-41 行 XAML 仍参与编译）** | eng |
| 服务与帮助器 | `Helpers/FileDialogHelper.Linux.cs` | **第 21-45 行（DetectTool 探测失败不缓存）** | eng |
| 服务与帮助器 | `Helpers/FileDialogHelper.Linux.cs` | **第 167-172 行（既无 portal 也无 kdialog/zenity 时只 Debug.WriteLine 后返回 null）** | eng |
| 服务与帮助器 | `Helpers/ClipboardHelper.Linux.cs` | **全文件（缺 `SetBitmap` 3 重载与 `GetTextAsync`）** | cut |
| 服务与帮助器 | `Helpers/XamlRootExtension.Linux.cs` | **全文件（缺 `GetUIScaleFactor`）** | cut |
| 服务与帮助器 | `Helpers/SystemBackdropHelper.Linux.cs` | **`Features/ViewHost/SystemTrayWindow.xaml.cs:34-38`（唯一的活调用点传 `true`，被当成 isDark，浅色主题下托盘恒为深灰底）** | cosmetic |
| 服务与帮助器 | `Features/Update/UpdateWindow.Linux.cs` | **第 15-47 行（类上没有 `[INotifyPropertyChanged]`，属性全是普通自动属性）** | engine |
| 服务与帮助器 | `Features/Update/UpdateWindow.Linux.cs` | **第 107-115 行（SetErrorMessage 用 `FindName("TextBlock_ErrorMessage")` 直接改控件）** | eng |
| 服务与帮助器 | `Features/Update/UpdateWindow.Linux.cs` | **第 117-134 行（Retry()/CheckUpdateAsync 直接赋 `ErrorMessage`，失败文案永不显示）** | eng |
| 服务与帮助器 | `Features/GamepadControl/GamepadController.Linux.cs` | **第 39-49 行（Set…MapKeys 返回 `Gamepad_NotSupportedOnLinux` 作为错误键）** | cosmetic |
| 服务与帮助器 | `Features/ViewHost/MainView.xaml.cs` | **第 118-140 行（TryCloseGameSelector 及第 272/276 行注释仍指向已不存在的 GameSelector）** | eng |

## 附录 C · 可回流上游的修正（12 条）

> 2026-11-25 重算：类别判为「修上游缺陷」的条目，即上游自己也有这个问题，值得单独提 PR。

| 模块 | 文件 | 位置 | 说明 |
|---|---|---|---|
| 游戏设置/运行环境/账号 | `Features/GameAccount/GameAccountSwitcher.xaml.cs` | **第 88-105 行（ChangeGameAccountAsync 的失败反馈）** | reasonable 合理换法 |
| 外壳 | `Features/ViewHost/MainView.xaml.cs` | **第 121-135 行（TryCloseGameSelector，Esc 第一优先级）** | reasonable 合理换法 |
| 外壳 | `Features/ViewHost/MainWindow.xaml.cs` | **第 249-262 行（AppWindow_Closing：托盘不可用则不拦关闭）** | must 必需 |
| 外壳 | `Features/ViewHost/MainWindow.xaml.cs` | **第 307-318 行（Content_KeyDown 的 Esc）** | reasonable 合理换法 |
| 抽卡/战绩 | `Features/GameRecord/GameRecordPage.xaml.cs` | **第 245 行（`NavigationViewItem_UpdateDeviceInfo.Visibility` 按区服赋值）** | reasonable 合理换法 |
| 设置界面 | `Features/Setting/FileManageSetting.xaml.cs` | **第 75、132、438–448 行（`UnregisterInstanceKey`）** | must 必需 |
| 设置界面 | `Features/Setting/SettingPage.xaml.cs` | **第 35–45 行（`OnLoaded`）** | reasonable 合理换法 |
| 设置界面 | `Features/Setting/SettingPage.xaml.cs` | **第 46–60 行（`ApplyRequestedPage`）** | reasonable 合理换法 |
| 服务与帮助器 | `Features/PlayTime/PlayTimeRecordService.cs` | **第 60-80 行（ArgumentException 回退）、第 152 行（GetProcessStartTime）、第 189 行起（GetLastStartTime）** | reasonable 合理取舍 |
| 服务与帮助器 | `Helpers/ClipboardHelper.Linux.cs` | **第 125-133 行（SetText 改为 `Task.Run(() => TryWriteClipboard(value))`）** | must 必需 |
| 服务与帮助器 | `Helpers/DriveHelper.Linux.cs` | **第 93-108 行（GetDriveAvailableSpace 走 GetMountPoint + DriveInfo）** | must 必需 |
| 服务与帮助器 | `Helpers/XamlRootExtension.Linux.cs` | **第 12-40 行（GetAppWindow：ContentIslandEnvironment → WindowBase.FindAppWindow → MainWindow.Current 三级回落）** | must 必需 |

