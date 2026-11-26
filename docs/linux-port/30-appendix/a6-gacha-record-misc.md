# E-2 组：抽卡/战绩/杂项（22 文件）

- 审计对象：`/home/qie2035/projects/migrate/Starward`，HEAD `2b1e7b46`
- 对照：上游 `src/Starward/<相对路径>` × 移植 `src/Lunarward/<相对路径>`（逐行 `diff -u`，非抽查）；`/tmp/lw-audit2/diffs/*.u0` 与实测 diff 一致（抽验 ZZZGachaService.cs、UIGFGachaService.cs 逐字节相同）
- 只读审计：未修改仓库、未执行构建。所有「编译状态」是静态核对结论（引用/签名/资源键一致性 + 与上游同构），不是 `dotnet build` 结果
- 行号均以 `grep -n` / `sed -n` 复核（移植侧行号，除标注“上游”者）

---

## 抽卡组（10 文件）

### Features/Gacha/GachaLogPage.xaml
- 变更行数：3（删除 1 / 新增 2）
- 编译状态：静态一致。`NavigationCacheMode` 是 `Microsoft.UI.Xaml.Controls.Page` 属性，Uno 支持（D60 已在 8 个页面启用并有 13-lessons 实测数据）

#### 差异清单
- **[移植 第 1-3 行 · 文件头]** 文案外观（BOM 移除 + 属性换行）｜功能损失：否｜建议：保留｜上游：`﻿<?xml…?>` 带 BOM，第 2 行 `<sf:PageBase x:Class=…>`｜移植：无 BOM，第 2 行 `<sf:PageBase`，第 3 行 `NavigationCacheMode="Required" x:Class=…`｜理由：BOM 差异无行为；`Required` 是 D60「页面缓存」决策，13-lessons.md:964 实测启动器页 ctor 289–425 ms → 24–46 ms
- **[移植 第 3 行 · 页面缓存]** 移植新增｜功能损失：否（性能收益）｜建议：保留｜上游：无任何 `NavigationCacheMode`（页面每次导航重建）｜移植：`Required`｜理由：收益明确；代价是「实例复用」，必须由 `OnNavigatedTo` 重置 + `OnGameChanged()` 补刷新，本文件配套改动见下一个文件（GachaLogPage.xaml.cs:57-66、:165-180），**成对成立，缺一即错**

### Features/Gacha/GachaLogPage.xaml.cs
- 变更行数：35（删除 3 / 新增 32）；上游 1116 行 → 移植 1145 行
- 编译状态：静态自洽。删除 `using Vanara.PInvoke;` 的同时移除了唯一的 `User32.SetForegroundWindow` 调用点；`using Starward.Features;` 为 `PageBase` 所在命名空间；`OnGameChanged` 与 `PageBase.OnGameChanged`（`src/Lunarward/Features/PageBase.cs:79` 声明（空实现）、`:70` 调用）签名一致

#### 差异清单
- **[移植 第 1 行 · 文件头]** 文案外观｜功能损失：否｜建议：保留｜上游：带 BOM｜移植：无 BOM｜理由：无行为
- **[移植 第 25-26 行 · using 区]** 平台替换｜功能损失：否｜建议：保留｜上游：`using Vanara.PInvoke;`｜移植：删除，新增 `using Starward.Features;`｜理由：Vanara 是 Windows P/Invoke 封装，Linux 无对应库；该 using 在上游此文件只服务 `User32.SetForegroundWindow`
- **[移植 第 57-66 行 · OnNavigatedTo]** 移植新增（缓存连带）｜功能损失：否｜建议：保留｜上游：`OnNavigatedTo` 只调 `base` 再按游戏置位｜移植：进入分支前先把 `EnableGenshinGachaItemStats/EnableStarRailGachaItemStats/EnableZZZGachaItemStats/IsZZZGachaStatsCardVisible` 全部置 false、4 个 `MenuFlyoutItem_Sync*` 置 Collapsed、`MenuFlyoutItem_CloudGame` 置 Visible｜理由：页面实例被缓存后，上一款游戏的置位会残留（B18/B19）。已核对后续 `:78-109` 分支对 hk4e/hkrpg/nap 全覆盖、global 再收 `CloudGame`，重置后不会出现「该隐藏的仍可见」
- **[移植 第 146-161 行 · OnLoaded / UpdateGachaLogMessage 回调]** 平台缺失（窗口激活）｜功能损失：是（轻微，可感知）｜建议：保留 + 在 12-shims 记一条「外部窗口不抢焦点」｜上游：回调里先 `User32.SetForegroundWindow((nint)this.XamlRoot.ContentIslandEnvironment.AppWindowId.Value)` 把主窗提到前台，再拉记录｜移植：`:153` 只剩注释「SetForegroundWindow 在 Linux 无对应物，窗口激活交给合成器（D30）」｜理由：Linux 无 Win32 前台权；从浏览器/云游戏页面跳回时窗口不会被提到最前，用户可能以为「点了没反应」（记录确实已开始拉取）
- **[移植 第 163-180 行 · OnGameChanged]** 移植新增（缓存连带）｜功能损失：否｜建议：保留｜上游：无此方法（每次导航新建页面，`OnLoaded` 即完整初始化）｜移植：`DispatcherQueue.TryEnqueue` → `Initialize()` + `await UpdateWikiDataAsync()`，catch 记 "Reload gacha log page after game changed"｜理由：B48；不重载就会停在上一款游戏的 UID/卡池/统计
- **[移植 第 183-190 行 · OnUnloaded]** 行为保持｜功能损失：否｜建议：保留｜上游：`UnregisterAll(this)`（另有 `Grid_GachaStats.PointerWheelChanged -=`）｜移植：同样 `UnregisterAll`，`PageEx_Unloaded` 不再退订事件（`PageBase.cs:66-73`），但 `OnLoaded` 每次都会重新 `+=`｜理由：与 `OnLoaded` 的 `Register` 对称（`Register` 同 key 覆盖，不会叠加）
- **[移植 第 483-517 行 · UpdateGachaLogAsync（数据来源①：本机 webcache 文件）]** 平台替换（实现相同、可用性待真机）｜功能损失：否（判定为可用）｜建议：保留 + 真机验收｜上游：`GameLauncherService.GetGameInstallPath` → `_gachaLogService.GetGachaLogUrlFromWebCache(CurrentGameBiz, path)`｜移植：`:511` 同一行未改；`GachaLogService.cs` 与上游 `diff -q` 完全相同；实现落在 `src/Starward.Core/Gacha/GachaLogClient.cs:113` `GetGachaUrlFromWebCache` → `:125 GetWebCachesFolderPath`（`YuanShen_Data\webCaches` 等）+ `:140-175 GetGachaCacheFilePath`（扫 `<webCaches>/Cache/Cache_Data/data_2` 取最新写时间），纯文件 IO｜理由：这条链在 wine 前缀内是普通文件路径，**不依赖 WebView2**，是本组唯一「原样可用」的抽卡取 URL 路径；但它只能读到「游戏里打开过抽卡记录页」留下的 cache，故提示语（`GachaLogPage_CannotFindURL` / 删缓存目录按钮）仍是唯一出路
- **[移植 第 624-700 行 · SyncFromMiyousheAsync（数据来源②：米游社/HoYoLAB 接口）]** 平台替换｜功能损失：否｜建议：保留｜上游：同一实现，取 `GameRecordRoleService.GetGameRoles(biz)` 的 `role.Cookie` 调 `zzzGachaService.GetGachaLogByGameRecordAsync`｜移植：与上游 1:1（无 diff）｜理由：纯 HTTP + DB 里的 cookie，不经过 WebView2；仅绝区零（nap_cn/nap_bilibili 用 nap_cn 角色）可用，`role.Cookie` 由 `LoginPage.Linux.cs`（独立 WebKitGTK 窗口）+ `WebLoginService` 落库，D29/D32 已成立
- **[移植 第 831 行 · OpenCloudGameWindow]** 平台缺失（云游戏窗口整条链）｜功能损失：是｜建议：**待定（先隐藏入口，见汇总 M1）**｜上游：`new CloudGameGachaWindow { GameBiz }`，窗口内 WebView2 打开云游戏页面、`Button_Click` 用 `ExecuteScriptAsync("document.documentElement.outerHTML")` 抓抽卡链接｜移植：`:831` 未改，`Features/Gacha/CloudGameGachaWindow.xaml.cs:53-54` 设 `webview.Source`、`:71 await webview.EnsureCoreWebView2Async()`、`:72 ExecuteScriptAsync(...)`｜理由：`docs/linux-port/00-decisions.md:112`、`:195` 明确记录 Uno/WebKitGTK 下 `ExecuteScriptAsync` **会让进程直接退出**（G0-3b 实测）；而 `:66` 又把 `MenuFlyoutItem_CloudGame` 置为 Visible（除 global 与 nap），用户点下去有进程退出风险 —— 这不是「功能缺失」，是「可触发崩溃」
- **[移植 第 207-247 行 · Initialize]** 行为保持｜功能损失：否｜建议：保留｜上游：同结构（卡池 banner/UID 列表/上次 UID/空态 emoji）｜移植：一致｜理由：无差异

### Features/Gacha/GachaLogExportFile.cs
- 变更行数：2（删除 1 / 新增 1）
- 编译状态：静态一致（仅字符串常量变化）

#### 差异清单
- **[移植 第 39 行 · ExportApp 默认值]** 平台替换（品牌）｜功能损失：是（互操作，轻微）｜建议：**改回上游**（或至少保留可配置）｜上游：`public string ExportApp { get; set; } = "Starward";`｜移植：`= "Lunarward";`｜理由：`export_app` 是 UIGF/SRGF 的**互操作字段**，第三方统计站（如 UIGF 生态的导入器）常按 `export_app` 分派/白名单，未知值会被拒或归类为「其他」；导出内容本身不受影响，风险在「别人读你的导出」

### Features/Gacha/ZZZGachaService.cs
- 变更行数：2（删除 1 / 新增 1）
- 编译状态：静态一致（u0 与实测 diff 一致，其余 566 行 1:1）

#### 差异清单
- **[移植 第 550 行 · 绝区零导出构造（export_app）]** 平台替换（品牌）｜功能损失：是（互操作，同上一节）｜建议：改回上游｜上游：`export_app = "Starward"`｜移植：`"Lunarward"`｜理由：同上；`:552 export_app_version = AppConfig.AppVersion ?? ""` 未改，字段完整性不受影响。**注意此处与 UIGF3File/UIGF4File 是同一决策的三处落地，要改就四处一起改**（见汇总 M10）

### Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs
- 变更行数：2（删除 1 / 新增 1）；上游 475 行 → 移植 475 行
- 编译状态：静态一致（`OpenUrl.Open` 存在：`src/Lunarward/Helpers/OpenUrl.cs:21 public static bool Open(string? target)`，返回 bool 被丢弃，与上游 `await Launcher.LaunchUriAsync` 同样不检查返回值）

#### 差异清单
- **[移植 第 462 行 · 导出完成 → 打开文件夹]** 平台替换｜功能损失：否｜建议：保留｜上游：`await Launcher.LaunchUriAsync(new Uri(folder))`｜移植：`Starward.Helpers.OpenUrl.Open(folder)`｜理由：`Launcher` 在移植侧**没有任何 shim**（全仓无 `class Launcher`，`Windows.System.Launcher` 在 Uno 属未实现面），换成 `OpenUrl`（portal → xdg-open → gio 三级回退）是必要替换；建议顺手用返回值判失败并提示（当前静默）
- **[移植 第 68-72 行 · InitializeWebViewAsync]** 平台缺失｜功能损失：是（窗口主功能整块死）｜建议：**待定（M4）+ 在窗口内给说明，别让用户以为是抽卡数据坏了**｜上游：`await webview2.EnsureCoreWebView2Async()` → `coreWebView2 = webview2.CoreWebView2` → `Profile.PreferredColorScheme = Dark` → `WebResourceResponseReceived += …`｜移植：与上游逐行相同（diff 只有 462 行那一处）｜理由：`:151 CoreWebView2_WebResourceResponseReceived` → `:195 AddIconInfoAndGetItemListAsync` 是**唯一的数据入口**（抓网页响应里的物品列表/图标）；WebKitGTK 后端没有 `CookieManager`/`Profile`/`ExecuteScriptAsync`（00-decisions.md:112，G0-3b），`WebResourceResponseReceived` 属同一族未实现面 → 导出得到的 `ZZZGachaInfo.*.json` 很可能是空列表。1 行的 diff 掩盖了「整个窗口在 Linux 上无事可做」的事实

### Features/Gacha/UIGF/UIGFGachaService.cs
- 变更行数：2（删除 1 / 新增 1）
- 编译状态：静态一致（该 `using` 在文件内无任何 `Vanara` 类型引用，删除安全）

#### 差异清单
- **[移植 第 16 行 · using 区]** 平台替换｜功能损失：否｜建议：保留｜上游：`using Vanara.PInvoke;`（残留、未使用）｜移植：删除（留空行）｜理由：Vanara 是 Windows-only 包；上游此处本就是无用 using，删掉等于顺手清理，无功能影响

### Features/Gacha/UIGF/UIGF3File.cs
- 变更行数：2（删除 1 / 新增 1）
- 编译状态：静态一致

#### 差异清单
- **[移植 第 39 行 · ExportApp 默认值]** 平台替换（品牌）｜功能损失：是（互操作，轻微）｜建议：改回上游｜上游：`= "Starward"`｜移植：`= "Lunarward"`｜理由：同 GachaLogExportFile.cs:39

### Features/Gacha/UIGF/UIGF4File.cs
- 变更行数：2（删除 1 / 新增 1）
- 编译状态：静态一致

#### 差异清单
- **[移植 第 77 行 · ExportApp 默认值]** 平台替换（品牌）｜功能损失：是（互操作，轻微）｜建议：改回上游｜上游：`= "Starward"`｜移植：`= "Lunarward"`｜理由：同 GachaLogExportFile.cs:39

### Features/Gacha/GenshinBeyondGachaPage.xaml
- 变更行数：5（删除 2 / 新增 3）；上游 645 行 → 移植 646 行
- 编译状态：静态一致（配套 code-behind `GenshinBeyondGachaPage.xaml.cs` 与上游 `diff` **完全为空**）

#### 差异清单
- **[移植 第 1-3 行 · 文件头]** 文案外观 + 移植新增（缓存）｜功能损失：否｜建议：保留｜上游：带 BOM 的 `<sf:PageBase x:Class=…>`｜移植：无 BOM、第 3 行 `NavigationCacheMode="Required"`｜理由：与 GachaLogPage.xaml 同一决策；此页只有原神一种内容，code-behind 的 `OnLoaded`（`:55`）在每次 `Loaded` 都会跑，不存在「切游戏残留」问题，因此**没有**加 `OnGameChanged` 也自洽
- **[移植 第 55-58 行 · OnLoaded → Initialize]** 行为保持｜功能损失：否｜建议：保留｜上游：同｜移植：同（无 diff）｜理由：缓存页面每次显示仍触发 `Loaded`，初始化照跑

### Features/CloudGame/CloudGameButton.xaml.cs
- 变更行数：2（删除 1 / 新增 1）；上游 188 行 → 移植 188 行
- 编译状态：静态一致

#### 差异清单
- **[移植 第 178 行 · InstallGameAsync（打开云游戏官网）]** 平台替换｜功能损失：否｜建议：保留｜上游：`await Launcher.LaunchUriAsync(new Uri(url))`（`ys.mihoyo.com/cloud/#/download`、`cloudgenshin.hoyoverse.com`、`zzz.mihoyo.com/cloud-feat/`）｜移植：`Starward.Helpers.OpenUrl.Open(url)`｜理由：同 ZZZGachaInfoWindow:462，`Launcher` 无 shim；`OpenUrl` 三级回退更稳
- **[移植 第 130-149 行 · 启动云游戏后的时长记录]** 平台替换（未改动，但语义变化）｜功能损失：待定｜建议：真机验收｜上游：`Process.Start(StarwardExecutePath, $"playtime --biz … --pid {p.Id}")`｜移植：同代码；但 Linux 上 `p` 是 **wine 进程**，`AppConfig.StarwardExecutePath` 是 Linux 原生可执行｜理由：`PlayTimeSession.WaitForSessionEndAsync` 已支持「pid 是 wine 加载器」的判定（前缀 + 游戏 exe 名 + `wineserver -w`），所以这里能工作；但云游戏客户端不是 `YuanShen.exe` 这类，会落到「进程树轮询」分支 —— 时长是否准，只能真机验

---

## 战绩组（5 文件）

### Features/GameRecord/GameRecordPage.xaml
- 变更行数：24（删除 2 / 新增 22）；上游 493 行 → 移植 513 行
- 编译状态：静态一致。`ShowRecordEmptyState` 是 `public bool`（`GameRecordPage.xaml.cs:404`），Uno 支持 bool→Visibility 隐式绑定；`WebLoginCommand` 来自 `:510 private void WebLogin()` 的 `[RelayCommand]` 生成；`Lunarward_Record_Empty_Title/Description/Login` 在 `src/Starward.Language/Lang.Lunarward.cs:166-168` 与 `Lang.resx:3164-3170`、`Lang.zh-CN.resx:3164-3170` 均有键

#### 差异清单
- **[移植 第 1-3 行 · 文件头]** 文案外观 + 移植新增（缓存）｜功能损失：否｜建议：保留｜上游：带 BOM、`<sf:PageBase x:Class=…>`｜移植：无 BOM、`NavigationCacheMode="Required"`｜理由：D60/D94，配套 `OnGameChanged`（见 .xaml.cs:77-106）
- **[移植 第 491-508 行 · 空态 Grid（B50/D61）]** 移植新增｜功能损失：否（补兜底）｜建议：保留｜上游：无（未登录/无角色时内容区一片空白，只有背景图）｜移植：`<Grid Margin="248,0,0,0" Background="{ThemeResource ApplicationPageBackgroundThemeBrush}" Visibility="{x:Bind ShowRecordEmptyState}">` + 图标 + 标题/说明 + `AccentButtonStyle` 的登录按钮｜理由：`:492` 注释自己写了「空态只盖内容区，左侧工具箱要能点（OpenPaneLength=248）」；缺陷是 `Margin=248` 硬编码——若将来改 `NavigationView.OpenPaneLength`，遮罩会错位（低危，建议改成绑定或 `Resources` 常量）
- **[移植 第 229 / 477 行 · `Tapped=` 处理器]** 行为保持｜功能损失：否｜建议：保留｜上游：XAML 已声明 `Tapped="NavigationViewItem_BattleChronicle_Tapped"`（上游 `GameRecordPage.xaml:228`）与 `Tapped="NavigationViewItem_UpdateDeviceInfo_Tapped"`（`:476`）｜移植：同样两处（`:229`/`:477`，行号+1 因为文件头多一行）｜理由：这是判断「上游 `OnUnloaded` 里两行 `-=` 是死代码」的关键证据 —— 见 .xaml.cs:171-181

### Features/GameRecord/GameRecordPage.xaml.cs
- 变更行数：219（删除 5 / 新增 214）；上游 452 行 → 移植 661 行
- 编译状态：静态自洽。新增成员都有 XAML 或内部调用方（`ShowRecordEmptyState`←GameRecordPage.xaml:495；`WebLoginCommand`←:504；`OnGameChanged`←`PageBase.cs:79` 虚方法（`:70` 调用））；`GameRecordRoleChangedMessage` 定义在 `src/Lunarward/Features/GameRecord/GameRecordRoleChangedMessage.cs`，发送方 `LoginPage.Linux.cs`

#### 差异清单
- **[移植 第 63-67 行 · OnLoaded 追加订阅]** 移植新增（D134）｜功能损失：否（修 bug）｜建议：保留｜上游：只订阅 `HypThirdPartyLoginMessage`｜移植：追加 `WeakReferenceMessenger.Default.Register<GameRecordRoleChangedMessage>(this, … InitializeGameRoleAsync())`｜理由：Linux 登录走独立 WebKitGTK 窗口，`LoginPage.Linux.cs` 发的是 `GameRecordRoleChangedMessage`，上游没有任何订阅者 → 「登录成功回到战绩页仍是空的」。已核实 `HypThirdPartyLoginMessage` 仍由 `HypLoginWindow.xaml.cs:51/:535` 活跃发送（不是死订阅）
- **[移植 第 171-181 行 · OnUnloaded]** 平台替换（缓存连带）｜功能损失：否（修 bug）｜建议：保留｜上游：`UnregisterAll` + `NavigationViewItem_BattleChronicle.Tapped -= …` + `NavigationViewItem_UpdateDeviceInfo.Tapped -= …` + 三个字段置空｜移植：保留 `UnregisterAll` 与三个置空，删掉两行 `-=` 并留注释｜理由：两处 `Tapped` 本来就在 XAML 里声明（见上节），上游 `OnLoaded` 从不 `+=`；那两行 `-=` 在「页面不缓存」时无害（新实例重新 XAML 接线），在缓存实例上却是**把 XAML 接线摘掉**，导致第二次进入后「战绩」「更新设备信息」点了没反应。移植删得对
- **[移植 第 77-106 行 · OnGameChanged]** 移植新增（B51）｜功能损失：否｜建议：保留｜上游：无｜移植：`DispatcherQueue.TryEnqueue` → `InitializeNavigationViewItemVisibility()` → `CurrentRole = null; GameRoleList = null!;` → `NavigateTo(BlankPage)` → `if (await CheckAgreementAsync())` → `LoadGameRoles()` + `UpdateDeviceInfoAsync()` + `RefreshGameRoleHeadIconSilentlyAsync()`｜理由：缓存页面切游戏必须整套重载；三个置空/导航与 `OnUnloaded` 一致，不会残留上一款游戏的页签
- **[移植 第 112-165 行 · CheckAgreementAsync]** 移植新增（与上游错位）｜功能损失：否，但**行为不一致**｜建议：**改回上游（删除）**或至少让首屏也走同一逻辑｜上游：0.19.0 起战绩工具不再弹免责声明（`AcceptHoyolabToolboxAgreement` 在 `src/Starward/AppConfig.Setting.cs:115` 仍存在但无人读；上游 `GameRecordPage.xaml.cs` 全文无此方法）｜移植：`:108-111` 注释自述「上游不再弹，但移植侧保留 D61 的 5 秒倒计时」；只在 `OnGameChanged`（`:92`）调用｜理由：`OnLoaded`（`:49-72`）不调用它 → **同一会话第一次进入不弹、切一次游戏才弹**，这个交互是半吊子；而唯一的读取点在这里，`AcceptHoyolabToolboxAgreement` 事实上仍是死配置（只在 `:116/:152` 被本方法读写）。上游已判定「接口侧不再要求」，建议整块删掉；若产品坚持保留，则必须同时在 `OnLoaded` 之前调用，否则等于把免责声明变成只在特定路径出现的弹窗
- **[移植 第 223-240 / 244-253 行 · AllToolboxItemNames + foreach 折叠]** 移植新增（待定）｜功能损失：否｜建议：保留｜上游：`InitializeNavigationViewItemVisibility` 里逐个 `Visibility = Collapsed`｜移植：抽成 `static readonly string[]` + `nameof(...)` 遍历 + 反射/FindName 置 Collapsed｜理由：重构等价，减少 20+ 行重复；`nameof` 与 XAML 元素名一致（`:225-238` 与 XAML 中 `NavigationViewItem_*` 匹配），改名时编译器会报错，比上游安全
- **[移植 第 386 / 398 / 403-404 行 · ShowRecordEmptyState + setter 通知]** 移植新增（配套空态）｜功能损失：否｜建议：保留｜上游：无｜移植：`CurrentRole` / `GameRoleList` setter 里 `OnPropertyChanged(nameof(ShowRecordEmptyState))`；`public bool ShowRecordEmptyState => CurrentRole is null || GameRoleList is null || GameRoleList.Count == 0;`｜理由：与 XAML 空态集成，缺 `OnPropertyChanged` 则空态不会消失（这是常见坑，这里做对了）
- **[移植 第 510 行 · WebLogin]** 移植新增｜功能损失：否｜建议：保留｜上游：无（无空态按钮）｜移植：`[RelayCommand] private void WebLogin()`，供空态按钮拉起登录｜理由：配套 `GameRecordPage.xaml:504`
- **[移植 第 599-640 行 · UpdateDeviceInfoAsync + Tapped]** 移植新增（恢复旧语义）｜功能损失：否（反而是修上游退化）｜建议：保留 + 登记到 18-upstream-deviations.md｜上游：`NavigationViewItem_UpdateDeviceInfo_Tapped` 只做 `GameRecordServiceBase.ResetDeviceInfo();` + 直接弹成功提示（`Lang.HoyolabToolboxPage_TheDeviceFingerprintIsAlreadyUpdated`）—— 0.19.0 已退化为「清缓存 + 假成功」｜移植：`await UpdateDeviceInfoAsync(true)` → `ResetDeviceInfo()` + `deviceFpService.InitializeDeviceFpAsync(region)` + `_gameRecordService.GetDeviceInfoAsync(region)` + 成功/失败提示｜理由：依赖 `WindowsDeviceFpService` 的 Linux 实现（`src/Lunarward/Features/Passport/WindowsDeviceFpService.cs`，读 `/proc`、`/sys`、`/etc/machine-id`，SDK 值存 `AppConfig.CacheFolder/device-sdk.json`，非注册表），已核实存在且不改类名；这是「比上游更完整」的一处，需在偏差表里说明「上游 0.19.0 退化，移植保留旧语义」
- **[移植 第 642-655 行 · RefreshGameRoleHeadIconSilentlyAsync]** 移植新增｜功能损失：否｜建议：保留｜上游：无｜移植：`await _gameRecordService.UpdateGameRoleAsync(CurrentRole)`，失败只记 Debug/LogError｜理由：切游戏后静默刷新头像，失败不打扰用户，属于合理增益
- **[移植 第 47-72 行 · OnLoaded 其余部分]** 行为保持｜功能损失：否｜建议：保留｜上游：`HoyolabToolboxPaneOpen` 开关面板 → `Task.Delay(16)` → `NavigateTo(BlankPage)` → `InitializeGameRoleAsync()`｜移植：同 + 上述追加订阅｜理由：无其余差异

### Features/GameRecord/StarRail/SimulatedUniversePage.xaml
- 变更行数：27（删除 11 / 新增 16）；上游 439 行 → 移植 444 行
- 编译状态：静态自洽。`cc:WrapPanel` 是移植侧自写控件：`src/Lunarward/Controls/ToolkitControls.cs:40 public partial class WrapPanel : Panel`，命名空间 `CommunityToolkit.WinUI.Controls`（与 XAML `xmlns:cc="using:CommunityToolkit.WinUI.Controls"` 对齐），带 `MeasureOverride/ArrangeOverride` + `Orientation/HorizontalSpacing/VerticalSpacing/ItemWidth/ItemHeight`

#### 差异清单
- **[移植 第 370-403 行 · 祝福（Buffs）列表容器]** 平台缺失（引擎限制）｜功能损失：是（轻微：失去虚拟化）｜建议：保留 + 已在 12-shims 登记的话就补全行号｜上游：`<ItemsRepeater …><ItemsRepeater.Layout><cc:WrapLayout …/></ItemsRepeater.Layout></ItemsRepeater>`｜移植：`<ItemsControl …><ItemsControl.ItemsPanel><ItemsPanelTemplate><cc:WrapPanel HorizontalSpacing="8" VerticalSpacing="4"/></ItemsPanelTemplate></ItemsControl.ItemsPanel>`，注释引用「Uno 的 ItemsRepeater 不支持自定义 Layout（VirtualizingLayout 的 Measure/Arrange 不会被调用）」｜理由：Uno 的 `ItemsRepeater` 只认内置 `StackLayout/UniformGridLayout`，自定义 `VirtualizingLayout` 不生效（会全挤在一行/不换行）；换 `ItemsControl` 是正确的兼容做法，代价是条目不虚拟化（祝福条目数量级几十，可接受）。同文件下方 Miracles 仍用 `ItemsRepeater + UniformGridLayout`（内置布局可用）→ 同文件内标准一致，不矛盾
- **[移植 第 40 行 · ToolkitControls.cs 的命名空间占用]** 移植新增（风险提示）｜功能损失：否｜建议：待定（长期建议挪到独立命名空间）｜上游：无此文件｜移植：自写 `WrapPanel/Segmented/SegmentedItem` 冒充 `CommunityToolkit.WinUI.Controls`｜理由：官方包 TFM 是 `net8.0-windows`，Linux 用不了，冒充命名空间让 XAML 不改；代价是将来若引入官方包会**类型冲突**且难排查

### Features/GameRecord/ZZZ/ShiyuDefensePage.xaml
- 变更行数：30（删除 15 / 新增 15）；上游 1397 行 → 移植 1397 行（纯等量替换）
- 编译状态：静态自洽。新增文件 `src/Lunarward/Features/GameRecord/ZZZ/ElementWeaknessConstants.cs`（`public const int None = -1;`）；`ElementWeaknessVisibility(int value, int weakOrResist)` 两侧签名相同（`ShiyuDefensePage.xaml.cs:169`）

#### 差异清单
- **[移植 第 299 / 370 / 627 / 994 / 1196 行 · FontIcon Glyph]** 平台替换（字体）｜功能损失：否（图标语义需真机核对）｜建议：保留｜上游：`Glyph="&#xF08F;"`（Segoe Fluent Icons 私有区）｜移植：`&#xE768;`（Uno Symbols 码位）｜理由：Uno 拦截 `Segoe Fluent Icons` 族名，随包字体不加载（L-15/L-88），只能换码位；`E768` 与 `F08F` 是否**同一语义**需要肉眼看一眼（现状是「能显示」）
- **[移植 第 439/442/445/448/451/696/699/702/705/708 行 · ElementWeaknessVisibility 第二实参]** 平台替换（编译器限制）｜功能损失：否（语义等价）｜建议：保留｜上游：`ElementWeaknessVisibility(IceWeakness, -1)` 等 10 处「字面 -1」｜移植：`ElementWeaknessVisibility(IceWeakness, local:ElementWeaknessConstants.None)`｜理由：Uno 的 x:Bind 解析器不接受负数实参（`UXAML0001 "Invalid identifier character -"`，注释写在 ElementWeaknessConstants.cs 头部）；提成 `const int None = -1` 后 XAML 侧传的是常量，方法体判定仍是 `weakOrResist == -1`，`Visibility` 结果不变

### Features/GameRecord/DailyNote/DailyNoteButton.xaml
- 变更行数：4（删除 2 / 新增 2）；上游 722 行 → 移植 722 行
- 编译状态：静态自洽（`ConverterParameter` 里的字符串只是 `ToString` 格式串实参）

#### 差异清单
- **[移植 第 307 行 · FontIcon Glyph]** 平台替换（字体）｜功能损失：否｜建议：保留｜上游：`Glyph="&#xE733;"`｜移植：`&#xF140;`｜理由：同上（字体族被 Uno 拦截）；`F140` 属 Uno Symbols 里的「日历/时钟」族，与同文件其它图标风格一致
- **[移植 第 406 行 · StarRail 体力回满时间格式]** 平台替换（XAML 解析器）｜功能损失：否（格式串语义不变）｜建议：保留｜上游：`{x:Bind StarRailDailyNote.StaminaFullTime.LocalDateTime.ToString('HH\:mm', x:Null)}`｜移植：`'HH\\:mm'`｜理由：Uno 的 x:Bind 先做 C# 字符串字面量转义，`\\` → `\`，送进 `ToString` 的仍是 `HH\:mm`（`\:` 是「字面冒号」，输出 `HH:mm`）；两种写法结果一致，只是 Uno 需要多一层转义

---

## 杂项组（7 文件）

### Features/PlayTime/PlayTimeRecordService.cs
- 变更行数：234（删除 40 / 新增 194）；上游 302 行 → 移植 456 行
- 编译状态：静态自洽。`PlayTimeSession`（`Features/PlayTime/PlayTimeSession.Linux.cs`，412 行）提供 `ActivePrefix/ActiveWine/WaitForSessionEndAsync`；`PlayTimeItemStruct` 有 `State`（`PlayTimeItem.cs:22-33`），`PlayTimeState.Start = 1`（`PlayTimeState.cs:8`）

#### 差异清单
- **[移植 第 27 / 29 / 49 / 144 行 · _recordingPids 去重]** 平台替换｜功能损失：是（跨进程保护丢失，低概率）｜建议：保留，但补一把文件锁（`flock` on `$XDG_RUNTIME_DIR/lunarward/playtime-<pid>.lock`）替代上游的跨进程命名对象｜上游：`AppInstance.FindOrRegisterForKey($"Starward.PlayTime.{pid}")`（跨进程命名互斥，注释自述「从不释放」→ 第二个游戏恒 0）｜移植：`private static readonly ConcurrentDictionary<int, byte> _recordingPids`；`:49 if (!_recordingPids.TryAdd(pid, 0)) return;`；`:144 TryRemove`｜理由：上游那套是**真 bug**（键永不释放），改成进程内字典后正常运行路径（启动器 → `playtime` 子进程）只有唯一记录者，不会双记；但用户若手动再跑一次 `lunarward playtime --biz … --pid X`（CLI 有该子命令）就会和主进程各记一份 → 时长翻倍。概率低，属可接受折衷，建议加锁或至少在 CLI 帮助里提示
- **[移植 第 60-84 行 · 起点判定（进程已退出时回退历史起点）]** 平台替换｜功能损失：否｜建议：保留｜上游：`process = Process.GetProcessById(pid); startTime = process.StartTime;` 直接抛 `ArgumentException` 就整段失败（wine 加载器秒退时丢记录）｜移植：catch 后取 `GetLastStartTime(biz,pid)`，>12h 判 stale，再做 pid 复用判定，最后 `startTime = lastStart ?? DateTime.Now`｜理由：wine 的加载器进程常常在游戏起来后立刻退出，上游在这里会直接失败；移植的兜底是必要的
- **[移植 第 75-78 行 · pid 复用判定（B76）]** 行为丢失（缺陷）｜功能损失：是｜建议：**改（必须）**｜上游：无此逻辑｜移植：`if (lastStart is { } candidate && GetProcessStartTime(pid) is { } processStart && processStart > candidate.AddSeconds(2)) lastStart = null;`｜理由：`GetProcessStartTime`（`:152-186`）返回 `DateTimeOffset.FromUnixTimeSeconds(btime).UtcDateTime.AddSeconds(...)` → **Kind=Utc 的 UTC 墙钟**；`GetLastStartTime`（`:189-208`）返回 `DateTimeOffset.FromUnixTimeMilliseconds(ts).DateTime` → **本地墙钟**。`DateTime` 比较忽略 `Kind`，直接比数值：在东八区 UTC 比本地小 8 小时 → `processStart > candidate+2s` 几乎恒假 → pid 复用保护**从未生效**（旧起点被当成新一局起点，把空闲时间算成游玩）；在西半球（UTC-x）则恒真 → 真实起点被丢弃、起点退化成 `DateTime.Now`（时长偏短）。修法：两边统一 `DateTimeOffset`（或都用 UTC），例如给 `GetLastStartTime` 加 `.ToUniversalTime()` 再比
- **[移植 第 86-134 行 · Linux 会话判定 + 心跳]** 平台替换｜功能损失：否｜建议：保留｜上游：`while (!process.HasExited) { await Task.Delay(800/1200); Log(Play) }`（按进程存在轮询）｜移植：`prefix = PlayTimeSession.ActivePrefix ?? GameLauncherService.ResolvePrefix(biz.ToString())`；`Task session = PlayTimeSession.WaitForSessionEndAsync(pid, prefix, cts.Token, biz)`；`while (!session.IsCompleted) { WhenAny(session, Delay(20000)); if (退出即 break); 每 30s → SavePlayTimeStats(..., interruption:true) }`；结束 `cts.Cancel(); Log(Stop); SavePlayTimeStats(..., interruption:false)`｜理由：Linux 上真正的「游戏还在跑」不等价于「pid 还活着」（wine 加载器会退出、游戏进程在 wineserver 下改名），所以要走 `wineserver -w` / 命名进程 / 进程树三级判定（`PlayTimeSession.Linux.cs` 里：`WaitForWineProcessAsync(prefix,pid,30s)` → `WaitForNamedProcessAsync(prefix, GetGameExeNames(biz), 120s)` → `WaitForProcessTreeEndAsync`；都失败则 `wineserver -w`）；30s 心跳 + `Interruption=true` 是「启动器被杀」时保住已游玩时长的必要设计（`INSERT OR REPLACE` + 唯一索引 `IX_PlayTimeStats_GameBiz_StartTime_Pid`（`DatabaseService.cs:1084`）保证不重复计时）
- **[移植 第 141-145 行 · finally 清理]** 平台替换｜功能损失：否｜建议：保留｜上游：`finally { cts.Cancel(); … }`｜移植：`finally { _recordingPids.TryRemove(pid, out _); }`｜理由：与新去重结构配套
- **[移植 第 150-151 行 · 两个连续 `/// <summary>`]** 文档缺陷｜功能损失：否｜建议：删除第一行（或把第一行改成 `//` 注释）｜上游：无｜移植：`:150 /// <summary>取该 PID 最近一次的 Start 事件时间；没有就返回 null。</summary>` 紧跟 `:151 /// <summary>B76：读 /proc/<pid>/stat 的启动时刻（boot ticks + btime）。</summary>`，两条 summary 连在一起被编译器合并/丢弃前一条｜理由：`GetProcessStartTime` 实际只描述 boot ticks，前一条本属 `GetLastStartTime`（`:189`），属编辑事故；会误导后来的维护者
- **[移植 第 152-186 行 · GetProcessStartTime]** 移植新增｜功能损失：否（假定成立）｜建议：保留 + 加注释说明 USER_HZ 前提｜上游：无｜移植：读 `/proc/{pid}/stat`，`LastIndexOf(')')` 后 `Split(' ')` 取下标 19（starttime，单位 jiffies），`/proc/stat` 的 `btime`，`FromUnixTimeSeconds(btime).UtcDateTime.AddSeconds(ticks / 100.0)`｜理由：`stat` 第二字段是 `(comm)` 可能含空格与括号，先按最后一个 `)` 切是正确的写法；`ticks/100.0` 假定 `USER_HZ=100`（x86/x86_64/arm64 默认都是 100，非常规内核才不同），可接受
- **[移植 第 189-208 行 · GetLastStartTime]** 移植新增（与注释不符）｜功能损失：是（低）｜建议：**改（小修）**：SQL 加 `AND State = 1`（`PlayTimeState.Start`）｜上游：无｜移植：`SELECT * FROM PlayTimeItem WHERE GameBiz = @biz AND Pid = @pid ORDER BY TimeStamp DESC LIMIT 1;`｜理由：注释说「最近一次的 Start 事件」，但取的是**任意 State 的最新一条**。回退路径触发时（进程已没了），最新一条可能是 `Stop`(5)/`Error` 行 → 用「上一局的结束时刻」当「本局起点」，时长≈0；也可能是一条 `Play` 心跳 → 起点晚于真实起点若干十秒。加 `State = 1` 后语义与注释一致（顺带解决上一条 UTC/Local 的一半问题）
- **[移植 第 277-300 行 · SavePlayTimeStats(…, bool interruption)]** 移植新增｜功能损失：否｜建议：保留｜上游：只有 `SavePlayTimeStats(biz,pid,startTime,endTime)`（结束时落一条）｜移植：多一个 `Interruption` 列 + `INSERT OR REPLACE INTO PlayTimeStats (GameBiz,Pid,StartTime,EndTime,Interruption,Type)`，同事务 `DELETE FROM PlayTimeItem … BETWEEN StartTime AND EndTime`｜理由：配合 30s 心跳；幂等性由唯一索引保证（同一 `StartTime` 被 REPLACE），不会因心跳重复累加
- **[移植 第 305-337 行 · CreatePlaytimeStartInfo + 环境变量注入]** 移植新增｜功能损失：否｜建议：保留｜上游：三处内联 `new ProcessStartInfo { FileName = AppConfig.StarwardExecutePath, Arguments = $"playtime --biz … --pid …", CreateNoWindow = true }`｜移植：抽成方法，额外注入 `LUNARWARD_WINEPREFIX` / `LUNARWARD_WINE` / `LUNARWARD_WINESERVER`（由 `PlayTimeSession.ActivePrefix/ActiveWine` 或 `ResolvePrefix/ResolveWine` 推导；wineserver 要求与 wine 同目录且存在）｜理由：子进程需要知道自己属于哪个前缀，否则 `wineserver -w` 会等错对象；`:366/:379/:422` 三处调用点均已替换
- **[移植 第 405-420 行 · 进程名校验放宽]** 平台替换｜功能损失：是（弱化校验，影响面小）｜建议：保留｜上游：`if (process.ProcessName != name) return;`（严格等于游戏 exe 名）｜移植：`if (OperatingSystem.IsWindows() && process.ProcessName != name) return;` + Linux 分支 `if (!OperatingSystem.IsWindows() && process.ProcessName is not ("wine" or "wine64" or "wine-preloader")) { 记 warning }`（不 return），去重改用 `IsRecording(process.Id)`｜理由：Linux 上进程名永远是 `wine*`，严格比较会让 Linux 分支**永远不记录**（上游写法在 Linux 上就是死的）；放宽后靠 `IsRecording` + 前缀/命名进程判定兜住误领。代价是「同名 wine 进程」理论上会被误认领，实践中由 `WaitForSessionEndAsync` 的命名进程判定收敛
- **[移植 第 42-58 行 · LogPlayTimeAsync 入口]** 平台替换｜功能损失：否｜建议：保留｜上游：`AppInstance.FindOrRegisterForKey(...)` → 若已有实例则 return｜移植：`_recordingPids.TryAdd(pid,0)` → 已存在则 return｜理由：见第一条

### Features/PlayTime/PlayTimeButton.xaml.cs
- 变更行数：12（删除 0 / 新增 12）；上游 79 行 → 移植 91 行
- 编译状态：静态自洽（唯一调用方 `Features/GameLauncher/GameLauncherPage.xaml.cs:123 Button_PlayTime.RefreshPlayTime(CurrentGameBiz)`，`Button_PlayTime` 在 GameLauncherPage.xaml 中存在）

#### 差异清单
- **[移植 第 59-70 行 · RefreshPlayTime(GameBiz)]** 移植新增（缓存连带，D94）｜功能损失：否（修 bug）｜建议：保留｜上游：无（`CurrentGameBiz` 由 XAML 绑定，控件随页面重建）｜移植：`public void RefreshPlayTime(GameBiz gameBiz) { CurrentGameBiz = gameBiz; InitializePlayTime(); }` + 5 行注释｜理由：页面缓存后控件不重建，`CurrentGameBiz` 保持旧值 → 启动器「游玩时长」停在上一个游戏；显式换目标再读一次是必要补丁
- **[移植 第 45-57 行 · InitializePlayTime]** 行为保持｜功能损失：否｜建议：保留｜上游：`gameBiz = CurrentGameBiz.IsBilibili() ? $"{CurrentGameBiz.Game}_cn" : CurrentGameBiz; PlayTimeTotal = DatabaseService.GetValue<TimeSpan>($"playtime_total_{gameBiz}", out _)`｜移植：同｜理由：无差异（`TimeSpanToString` 的 `Math.Round` + `{h}h {m}m` 也与上游一致，格式化时区无关）

### Features/PlayTime/PlayTimeStatsDialog.xaml.cs
- 变更行数：2（删除 1 / 新增 1）；上游 522 行 → 移植 522 行
- 编译状态：静态一致

#### 差异清单
- **[移植 第 1 行 · 文件头]** 文案外观（BOM）｜功能损失：否｜建议：保留｜上游：`﻿using CommunityToolkit.Mvvm.ComponentModel;`｜移植：无 BOM｜理由：无行为差异。**时区/格式化结论**：该对话框正文的起止时间格式化逻辑与上游逐字相同（无第二个 hunk），因此显示侧不存在「移植引入的时区错位」；本组所有时区风险集中在 `PlayTimeRecordService.cs:75-78`（写入/回退路径），显示层是受害者不是元凶

### Features/SelfQuery/SelfQueryPage.xaml
- 变更行数：3（删除 1 / 新增 2）；上游 266 行 → 移植 267 行
- 编译状态：静态一致

#### 差异清单
- **[移植 第 1-2 行 · 文件头]** 移植新增（缓存）｜功能损失：否｜建议：保留｜上游：`<sf:PageBase x:Class=…>`（无 BOM）｜移植：`<sf:PageBase` + 第 2 行 `NavigationCacheMode="Required" x:Class=…`｜理由：D60 页面缓存；配套 `.xaml.cs:55-58` 的显隐重置（否则三个游戏的查询列表会叠在一起）

### Features/SelfQuery/SelfQueryPage.xaml.cs
- 变更行数：4（删除 0 / 新增 4）；上游 790 行 → 移植 794 行
- 编译状态：静态自洽（四个 `ListView_*` 名称与 XAML 一致）

#### 差异清单
- **[移植 第 55-58 行 · OnNavigatedTo]** 移植新增（缓存连带，B20）｜功能损失：否（修 bug）｜建议：保留｜上游：`OnNavigatedTo` 只按 `CurrentGameBiz.Game` 把对应游戏的列表置 `Visible`｜移植：先三行 `Visibility = Collapsed`（Genshin/StarRail/ZZZ）再走原逻辑｜理由：缓存实例复用后，上一个游戏选中的列表不会自动收起 → 三个列表叠在一行（注释原文：「避免三个列表叠在一行」）

### Features/UrlProtocol/UrlProtocolService.cs
- 变更行数：2（删除 1 / 新增 1）；上游 123 行 → 移植 123 行
- 编译状态：**本文件被排除编译**（`src/Lunarward/Lunarward.csproj:129 <Compile Remove="Features/UrlProtocol/UrlProtocolService.cs" />`），活实现是同目录 `UrlProtocolService.Linux.cs`（194 行）。因此此文件虽“有差异”，对产物零影响

#### 差异清单
- **[移植 第 116 行 · HandleUrlProtocolAsync 异常分支]** 死代码｜功能损失：否｜建议：**删除该文件**（或至少注明「仅作上游参考、未编译」）｜上游：`User32.MessageBox(HWND.NULL, ex.Message, "Starward");`｜移植：同一行只把标题改成 `"Lunarward"`｜理由：它整体被 `<Compile Remove>` 排除，改标题不改变任何行为；留在树里会让上游同步与下一个维护者误以为「Windows 实现还在用」。注意它里面的 `User32`/`HWND`（Vanara）在 Linux 无法编译，保留文件的唯一价值是「对照上游协议语义」
- **[`UrlProtocolService.Linux.cs:78-82` · UnregisterHypLoginProtocol]** 平台替换（注册/注销不对称）｜功能损失：否（实际清理仍发生）｜建议：保留，但把方法体改成交给 `UnregisterProtocol()` 或加显式校验｜上游：`HypLogin` 协议是**独立**注册表键 `Software\Classes\starward-hyp-login`，注册/注销成对（注销走 `DeleteSubKeyTree`）｜移植：`RegisterHypLoginProtocol() => RegisterProtocol()`；`UnregisterHypLoginProtocol()` **空方法体**（注释：与主协议共用同一个 .desktop，交给 `UnregisterProtocol`）｜理由：因为只有一份 `lunarward.desktop`，HypLogin「注销」不清任何东西也不会残留脏数据；但对称性只存在于注释里 —— 若将来有人给 HypLogin 单独建 .desktop，这个空实现会漏清。风险低，属可读性问题
- **[`UrlProtocolService.Linux.cs:49-84` · RegisterProtocol]** 平台替换｜功能损失：否（但注释与实现不符）｜建议：改注释（或补 `File.SetUnixFileMode`）｜上游：注册表写 `Software\Classes\starward`（`URL Protocol`、`shell\open\command` 等）｜移植：写 `$XDG_DATA_HOME|~/.local/share/applications/lunarward.desktop`（`Exec="{Exe}" %u`、`MimeType=x-scheme-handler/lunarward`、`StartupWMClass=Lunarward`），再 `update-desktop-database` + `xdg-mime default`｜理由：方法头注释提到需要「可执行位」，但代码**从未 chmod**；实际 `Exec` 指向 `$APPIMAGE` 或 `Environment.ProcessPath`（本身已可执行），所以功能不受影响，属于注释误导
- **[`UrlProtocolService.Linux.cs:150-183` · IsProtocolRegistered]** 平台替换（潜在卡顿）｜功能损失：否｜建议：**改**：`ReadToEndAsync` + `WaitForExitAsync` 配 `CancellationTokenSource(3s)`｜上游：读注册表值比较｜移植：`xdg-mime query default x-scheme-handler/lunarward` → **先 `StandardOutput.ReadToEnd()` 再 `WaitForExit(3000)`**，Trim 后与 `lunarward.desktop` 比较｜理由：`ReadToEnd()` 是同步阻塞且**没有超时**，`WaitForExit(3000)` 在它之后才生效 —— 一旦 `xdg-mime` 卡住（缺 `$DISPLAY`、D-Bus 挂死），这个「查询状态」会把调用线程一起拖住；CLI `--protocol-status` 与设置页都会用到
- **[`UrlProtocolService.Linux.cs:185-194` · RunQuiet]** 平台替换（违反硬规则）｜功能损失：否｜建议：**改**：调用方一律后台线程；或把 `WaitForExit(5000)` 换成异步 + 超时｜上游：无（注册表写入是内存操作，不 fork）｜移植：`Process.Start` + `proc?.WaitForExit(5000)`；`RegisterProtocol` 内连调两个 RunQuiet｜理由：AGENTS.md 硬规则 10「UI 线程不许 fork 外部进程 + WaitForExit」。调用点核查：`Program.Linux.cs:1628-1640` 已用 `Task.Run`（注释明确「扔后台，硬规则 10」）✅；但 `HypLoginWindow.xaml.cs:65 RegisterHypLoginProtocol()` / `:129 UnregisterHypLoginProtocol()` 是 **GUI 事件里同步调用**，最坏阻塞 UI 线程 2×5 s ❌（本组必须修项 M7）
- **[`UrlProtocolService.Linux.cs:107-140` · RemoveMimeAssociation]** 平台替换｜功能损失：否｜建议：保留｜上游：删注册表键即完事｜移植：读 `$XDG_CONFIG_HOME|~/.config/mimeapps.list`，剔除 `x-scheme-handler/lunarward=` 开头的行，`File.WriteAllLines` 回写（仅 `changed` 时写）｜理由：清 `mimeapps.list` 是 Linux 注销的标准动作，做对了；小瑕疵是 `WriteAllLines` 会统一换行/补末尾换行（对 `mimeapps.list` 无害），且不处理 `[Added Associations]`/`[Default Applications]` 分节差异 —— 用「按前缀整行剔除」反而规避了分节解析，可接受
- **[`App.xaml.cs:290 ExpandUrlArgument` · 深链参数]** 平台替换（功能缺失）｜功能损失：是｜建议：**补齐**（见 M9）｜上游：`UriProtocolService.HandleUrlProtocolAsync` 支持 `Uri.AbsolutePath.Trim('/')` 形式的 game_biz，且 `startgame` 支持 `install_path` 查询参数（`kvs["install_path"]`）｜移植：只认 `lunarward://<host>?biz=&pid=`，host ∈ {`startgame`,`playtime`,`test`}；无 `install_path`、无路径式 biz；错误路径静默返回原 args（上游弹 `User32.MessageBox`）｜理由：`SingleInstancePipe.cs:23/36-41` 只把 `lunarward://startgame?biz=` 展开成 `startgame --biz`，说明协议格式是自定义过的；丢失 `install_path` 会让「从外部一键带路径启动」失效（用户场景窄，但属上游已有能力）

### Features/Database/DatabaseService.cs
- 变更行数：57（删除 5 / 新增 52）；上游 1226 行 → 移植 1273 行
- 编译状态：静态自洽（`System.IO.Compression` 是 BCL；`ZipFile/ZipArchive/CreateEntryFromFile/CompressionLevel.SmallestSize` 在 net11 可用；`SharpSevenZip` 仅用于压缩，删除用点后无残留引用）

#### 差异清单
- **[移植 第 3 / 9 行 · using 区]** 平台替换｜功能损失：否｜建议：保留｜上游：`using SharpSevenZip;`｜移植：删掉，新增 `using System.IO.Compression;`｜理由：`7z.dll` 是 Windows 原生库（且 `SharpSevenZip` 在 Linux 无可用 native），换 BCL zip 是唯一选择
- **[移植 第 108 行 · AutoBackupToAppDataLocal 备份目录]** 平台缺失（Windows 语义残留）｜功能损失：是（用户找不到文件）｜建议：**改**为 `AppConfig.UserDataFolder/DatabaseBackup`（`~/.local/share/lunarward/DatabaseBackup`）并登记偏差｜上游：`Path.Join(Environment.GetFolderPath(SpecialFolder.ApplicationData), @"Starward\DatabaseBackup")`｜移植：**原样保留**同一行 → Linux 上解析成 `~/.config/Starward/DatabaseBackup`（`Starward` 是上游产品名）｜理由：移植侧数据目录是 `Path.Combine(LinuxDataHome, "lunarward")`（`AppConfig.Configuration.cs:180`），手动备份在 `AppConfig.UserDataFolder/DatabaseBackup`（`FileManageSetting.xaml.cs:186`）→ **同一功能两个目录**，且自动备份那个还带 `Starward` 字样。`#if DEBUG return;` 继承上游（Debug 不自动备份），所以日常开发看不到，容易漏
- **[移植 第 113-117 / 121 / 130 行 · 压缩方式与扩展名]** 平台替换｜功能损失：否（并有修 bug）｜建议：保留｜上游：`string archive = Path.ChangeExtension(file, ".7z"); … new SharpSevenZipCompressor().CompressFiles(archive, file);`（逻辑上「ZIP 内容挂 .7z 扩展名」的隐患由移植注释指出）｜移植：`.zip` + `CreateArchive(archive, file)`；轮转通配符改 `*.zip`｜理由：扩展名与真实格式一致，7z/ark 打开不再报错
- **[移植 第 145-150 行 · 清理旧的 *.7z]** 移植新增（迁移）｜功能损失：否｜建议：保留｜上游：无｜移植：`Directory.GetFiles(folder, $"StarwardDatabase_AutoBackup_{hash}_*.7z")` → 逐个删（try/catch IOException）｜理由：旧版本留下的「其实是 ZIP 的 .7z」不会再被轮转逻辑选中，会永久堆在磁盘；清掉是对的
- **[移植 第 1244-1268 行 · BackupToArchive(string)]** 移植新增｜功能损失：否（新能力）｜建议：保留｜上游：无（上游尾部到 :1229，只有 `.7z` 自动备份与 `BackupDatabase`）｜移植：临时目录 `.<guid>` → `BackupDatabase(tempDb)`（SQLite backup API，`:89-96 con.BackupDatabase(backupCon)`）→ `CreateArchive(tempZip, tempDb)` → `File.Move(..., true)`，finally 清临时文件｜理由：供 CLI `--database-backup <输出.zip>`（`Helpers/CliSpec.cs:101`）与设置页导出；用 SQLite 在线备份 API 而不是拷文件，`-wal` 场景安全
- **[移植 第 1270-1273 行 · CreateArchive(string,string)]** 移植新增｜功能损失：否｜建议：保留｜上游：无｜移植：`ZipFile.Open(archivePath, ZipArchiveMode.Create)` + `zip.CreateEntryFromFile(sourceFile, Path.GetFileName(sourceFile), CompressionLevel.SmallestSize)`｜理由：只压**当次 .db 单文件**；`FileManageSetting.xaml.cs:194-196` 注释记着「上游那套会把整个备份目录套进去」的老 bug（D46 复核），这里从设计上避开了
- **[移植 第 1070-1090 行 · Sql_v21（PlayTimeStats 建表 + 唯一索引）]** 行为保持｜功能损失：否｜建议：保留｜上游：同（无 diff）｜移植：同 + `:1084 CREATE UNIQUE INDEX IF NOT EXISTS IX_PlayTimeStats_GameBiz_StartTime_Pid`｜理由：迁移逻辑与 SQLite 路径（`AppConfig.UserDataFolder`、`StarwardDatabase.db`）与上游完全一致（本文件所有 hunk 都不涉及 schema/路径）；`IX_*` 是移植 30s 心跳幂等的前提，属跨文件依赖，不能单独回退
- **[交叉引用 · `FileManageSetting.xaml.cs:229 Launcher.LaunchFolderAsync`]** 平台缺失（不在本清单文件内，但由本文件能力引出）｜功能损失：是（低）｜建议：改 `OpenUrl.Open(目录)`（`OpenUrl.cs:21` 支持目录）｜上游：`Launcher.LaunchFolderAsync(folder, options)` + `ItemsToSelect` 高亮｜移植：原样保留该调用，但全仓**没有 `class Launcher` 的 shim**（唯一命中都是业务类名 `LauncherConfig*`）→ 在 Uno Linux 上要么抛 `NotImplementedException`（被 catch 记 "Open last backup database" 日志），要么静默无事发生｜理由：「打开最近备份」是备份功能的最后一公里，用 `OpenUrl` 打开目录（牺牲“选中该文件”高亮）比现在强

---

## 汇总

### 必须修（按严重度）

| ID | 问题 | 位置 | 理由/修法 |
| --- | --- | --- | --- |
| **M1** | **点「云游戏」抽卡会把进程搞死**：菜单项在 hk4e/hkrpg 下仍 `Visible`，窗口内部调 `ExecuteScriptAsync` | `Features/Gacha/GachaLogPage.xaml.cs:66`；`Features/Gacha/CloudGameGachaWindow.xaml.cs:71-72`；证据 `docs/linux-port/00-decisions.md:112`、`:195`（G0-3b 实测「会让进程直接退出」） | 最小修：Linux 下把 `MenuFlyoutItem_CloudGame` 恒置 `Collapsed`（或点了给 InfoBar 说明）；根治要等 M4 的独立 WebKitGTK 取链接方案 |
| **M2** | **米游社 BBS / 战绩详情整条桥是空壳，但入口仍像能用**：`BBSWebBridge.xaml.cs`（705 行）被排除，替换为 26 行 stub；`BattleChronicleWindow` 退化成说明页 | `src/Lunarward/Lunarward.csproj:111-113`；`Features/GameRecord/BBSWebBridge.Linux.cs`（26 行）；`Features/GameRecord/BattleChronicleWindow.Linux.cs:1-60`（正文「Linux 版暂时不能打开战绩页面」）；入口 `GameRecordPage.xaml.cs:343` | 说明页已经存在（L-183 修过“一片空白”），但工具箱里「战绩」页签仍高亮可点 → 建议在页签上加“暂不可用”标识或禁用，避免用户以为是登录/账号问题。**这是本组最大功能缺口，已在 25-deviation-register.md:1398-1400 登记，不算新发现，但要做到“入口与能力一致”** |
| **M3** | **绝区零抽卡物品窗口在 Linux 上取不到数据**（`WebResourceResponseReceived` 属未实现面），1 行 diff 掩盖了整窗失效 | `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs:68-72`、`:151`、`:195` | 数据入口只有这一个事件；建议窗口内加降级提示（同 M2 的说明页做法），并在 25-deviation-register 补一条“不是 API 替换，是功能不可用” |
| **M4** | **游玩时长 pid 复用保护时区错位**（UTC 墙钟 vs 本地墙钟比较） | `Features/PlayTime/PlayTimeRecordService.cs:75-78`（对比 `:152-186` 返回 `UtcDateTime` 与 `:189-208` 返回本地 `DateTime`） | 东八区：保护恒失效（旧起点被当新起点，空闲算游玩）；负时区：恒成立（起点退化 `Now`，时长偏短）。改法：两值统一 `DateTimeOffset`/UTC 后比较 |
| **M5** | `GetLastStartTime` 取「任意 State 的最新一条」，与注释「最近一次 Start」不符 | `Features/PlayTime/PlayTimeRecordService.cs:189-208` | 回退路径可能拿 `Stop`/`Error` 行当起点 → 时长≈0。修法：`AND State = 1`（`PlayTimeState.Start`，`PlayTimeState.cs:8`） |
| **M6** | 自动备份写到 `~/.config/Starward/DatabaseBackup`，与数据目录/手动备份目录不一致（产品名还是 `Starward`） | `Features/Database/DatabaseService.cs:108` vs `AppConfig.Configuration.cs:180`、`FileManageSetting.xaml.cs:186` | 用户找不到自动备份；同功能两处目录。改成 `AppConfig.UserDataFolder` 并在 18-upstream-deviations 登记（这是有意偏离） |
| **M7** | 登录窗口在 GUI 线程同步 fork 并 `WaitForExit(5000)`×2，违反硬规则 10 | `Features/UrlProtocol/UrlProtocolService.cs` 的活实现 `UrlProtocolService.Linux.cs:49-84`（内部 `RunQuiet` `:185-194`）；调用点 `Features/GameRecord/HypLoginWindow.xaml.cs:65`、`:129` | 「登录/退出登录」会冻窗最多 10 s；改成 `Task.Run` 或异步 + 超时（`Program.Linux.cs:1628-1640` 已是正确写法，可直接照抄） |
| **M8** | `IsProtocolRegistered` 先 `ReadToEnd()`（无超时）再 `WaitForExit(3000)`，超时形同虚设 | `UrlProtocolService.Linux.cs:150-183` | `xdg-mime` 在无 `$DISPLAY`/D-Bus 挂死时会把调用线程拖住；改异步 + `CancellationTokenSource(3s)` |
| **M9** | 深链丢掉上游已有的两件事：`startgame` 的 `install_path` 查询参数、路径形式的 game_biz；错误路径静默 | `src/Lunarward/App.xaml.cs:290 ExpandUrlArgument` vs 上游 `UriProtocolService.HandleUrlProtocolAsync` | 外部调用「带路径启动」失效；建议补齐解析并保留一条日志（上游是 `MessageBox`，Linux 可换成 `InAppToast`/stderr） |
| **M10** | `export_app` 由 `"Starward"` 改成 `"Lunarward"`（四处），第三方导入器可能不认 | `GachaLogExportFile.cs:39`、`ZZZGachaService.cs:550`、`UIGF3File.cs:39`、`UIGF4File.cs:77` | 属互操作字段；建议保持上游值（或先确认目标站点的白名单），四处必须同时决策 |
| **M11** | 免责声明只在「切游戏」时弹，首次进入不弹；且上游 0.19.0 已删除该交互 | `GameRecordPage.xaml.cs:92`（调用点）vs `:49-72`（OnLoaded 不调用）、`:112-165`（实现） | 要么整块删掉（推荐，与上游一致），要么首屏也走同一逻辑；现状是半吊子，且 `AcceptHoyolabToolboxAgreement` 仍是死配置 |
| **M12** | 文档/卫生小瑕：双 `/// <summary>`；被排除编译的 `UrlProtocolService.cs` 仍留在树里改了一行标题 | `PlayTimeRecordService.cs:150-151`；`Features/UrlProtocol/UrlProtocolService.cs:116`（csproj:129 排除） | 前者会误导维护者（`GetProcessStartTime` 的注释被前一条覆盖）；后者让人误判“Windows 实现还活着”，建议删文件或加 `// 未编译` 头注释 |

### 明确保留（有决策/实测支撑，不要“顺手回退”）

- 页面缓存 `NavigationCacheMode="Required"`（`GachaLogPage.xaml:3`、`GenshinBeyondGachaPage.xaml:3`、`SelfQueryPage.xaml:2`、`GameRecordPage.xaml:3`）+ 配套的 `OnNavigatedTo` 重置 / `OnGameChanged` / `RefreshPlayTime`（130 ms→30 ms 级收益，13-lessons.md:964）
- `OnUnloaded` 去掉两行 `Tapped -=`（上游那两行是死代码，且会摘掉 XAML 接线）
- `ItemsRepeater+WrapLayout` → `ItemsControl+WrapPanel`（Uno 不支持自定义 `VirtualizingLayout`）
- 负数字面量提常量、5 处图标码位替换、`HH\\:mm` 转义（Uno 编译器/字体限制）
- `_recordingPids` 取代 `AppInstance`（上游键永不释放的真 bug）、`PlayTimeSession` 三级会话判定、30 s 心跳 + `Interruption`、`CreatePlaytimeStartInfo` 注入前缀环境变量
- BCL zip 备份管线（`BackupToArchive`/`CreateArchive`），以及 `.7z` 残留清理
- `UpdateDeviceInfoAsync` 恢复真实取指纹（上游 0.19.0 退化成“清缓存 + 假提示”，`WindowsDeviceFpService` 的 Linux 实现已核实存在）
- `GameRecordRoleChangedMessage` 订阅（Linux 独立登录窗口的唯一回填通道）

### 数据来源结论（本组两个重点问题的直接回答）

1. **抽卡**：Linux 上与 WebView2 无关的两条链是活的 —— ① 扫 wine 前缀里 `<Game>_Data/webCaches/**/Cache_Data/data_2`（`GachaLogClient.cs:113-175`，纯文件 IO）；② 米游社/HoYoLAB 接口 + DB 里 `GameRecordRole.Cookie`（`GameRecordPage` 登录链，`LoginPage.Linux.cs` + `WebLoginService`），仅绝区零。被裁掉的是**云游戏取链接**（`CloudGameGachaWindow`，会崩）和 **ZZZ 物品信息窗口**（`WebResourceResponseReceived`，静默无数据）。
2. **战绩**：列表/深渊/虚构/末日等**页面数据本身走 HTTP + cookie，可用**；只有「米游社 BBS 页面（战绩详情/战绩页签）」整块不可用，`BattleChronicleWindow.Linux.cs` 退化成说明页（文案与 L-183 修复都在）。
3. **游玩时长**：起止判定走 `wineserver -w` + 命名进程 + 进程树三级（`PlayTimeSession.Linux.cs`），跨进程去重退化为进程内（`_recordingPids`），wineserver 退出由子进程 `wineserver -w` 与主进程 30 s 心跳共同兜住；时区是**唯一真 bug**（M4），格式化层（`PlayTimeStatsDialog`、`PlayTimeButton.TimeSpanToString`）与上游逐字相同、无问题。
4. **URL 协议**：注册写 `.desktop` + `xdg-mime default`、注销删 `.desktop` + 清 `mimeapps.list` 行，**清理链路是对称的**；不对称的只有 `UnregisterHypLoginProtocol` 的空方法体（共用同一 `.desktop`，实际无害）。真正要修的是**同步阻塞**（M7/M8）与**深链参数缺失**（M9）。
5. **数据库**：SQLite 路径、文件名、全部迁移（含 `Sql_v21`）与上游一致；差异只在备份格式（`.7z`→`.zip`，并新增 `BackupToArchive`）与**自动备份目录仍是 `%APPDATA%\Starward`**（M6）。
