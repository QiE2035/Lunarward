# E-1 组：设置页（11 文件）

审计对象：`/home/qie2035/projects/migrate/Starward`，移植侧 `src/Lunarward/Features/Setting/*`，上游 `src/Starward/Features/Setting/*`。
方法：逐行读 U0 diff（`/tmp/lw-audit2/diffs/Features__Setting__<文件>.u0`）+ 逐行读两边实体 + 读 `*.Linux.cs` 覆盖 + 用 `grep -n` 复核每个行号与成员名。**未修改仓库、未构建**（只做静态读取与 grep）。

编译状态判定依据（全组共用）：

- `src/Lunarward/Lunarward.csproj:17-18`：`EnableDefaultUnoItems=true`、`EnableDefaultXamlItems=false`、`EnableDefaultPageItems=false`；11 个 XAML 仍由 Uno 的 `Uno.DefaultItems.targets` 作为 **Page item**（`IsDefaultItem=true`）加入编译 → **本组没有"XAML 没被加载"的情况**。
- `Lunarward.csproj:127-128`：`<Compile Remove="Features/Setting/HotkeyManager.cs" />`、`<Compile Remove="Features/Setting/ScreenshotSetting.xaml.cs" />`；`:137` 移除 `Helpers/XamlRootExtension.cs`。替代品分别是 `src/Lunarward/Helpers/HotkeyManager.Linux.cs`、`src/Lunarward/Features/Setting/ScreenshotSetting.Linux.cs`、`src/Lunarward/Helpers/XamlRootExtension.Linux.cs`。
- 其余 10 个 code-behind 均在 `compile.txt` 的 `Features/Setting/` 段（246-263 行），全部参与编译。每个 code-behind 都显式调了 `InitializeComponent()`（AdvancedSetting:30、AboutSetting:20、DownloadSetting:24、FileManageSetting:26、GeneralSetting、GamepadControlSetting:18、HotkeySetting:22、ScreenshotSetting.Linux.cs:102）。
- 新鲜度旁证：`src/Lunarward/bin/Debug/net11.0-desktop/Lunarward.dll` mtime `2026-11-26 16:27:31` 晚于全部源文件（15:21）→ 当前源码（含 XAML x:Bind）通过过一次真实编译。

落盘机制（判"是否真落盘"的基准）：`AppConfig.SetValue<T>` 直接 `INSERT OR REPLACE INTO Setting (Key, GameId, Value)` 写 SQLite（`AppConfig.Setting.cs:810/816`），getter 走 `GetValue<T>`（同文件 `:760`，`[CallerMemberName]`）；`AppConfig.SaveConfiguration()`（`AppConfig.Configuration.cs:300`）只写 `Language / UserDataFolder / EnableLoginAuthTicket / stoken / mid` 到 `LinuxConfig/config.ini`，**与 Setting 表无关**；两侧 `App.xaml.cs` 都没有退出时统一保存。

---

## Features/Setting/AdvancedSetting.xaml

- 变更行数：53（删除 1 / 新增 47，1 个 hunk）
- 编译状态：参与编译（Page item）；`x:Class=Starward.Features.Setting.AdvancedSetting`，code-behind `AdvancedSetting.xaml.cs` 在编译清单内。

### 差异清单

- **[移植 第 18-39 行 · 「诊断（D61）」区 · Lunarward_Diagnose_Title/Description + Button_CreateDiagnose_Click + Button_OpenDiagnoseFolder_Click]** 移植新增｜功能损失：否｜建议：**保留**｜上游：整个 XAML 只有 `<ScrollViewer><StackPanel /></ScrollViewer>`（0.19.0 已删掉 URL Protocol 与 RPC 两块）｜移植：新增标题/说明 + 两个按钮（F412「生成诊断包」、E838「打开文件夹」）｜理由：上游此页在 Linux 上是白页；移植用"诊断包"补上等价可用动作（D61），`Lunarward_Diagnose_*` 键在 `Lang.resx` 各存在 1 处（已逐个 grep），`Button_CreateDiagnose_Click` 实现见 `.xaml.cs:92`。
- **[移植 第 41-63 行 · 「运行输出（D27）」区 · Lunarward_Output_Title/View/OpenFolder]** 移植新增｜功能损失：否｜建议：**保留**｜上游：无｜移植：标题 + 说明文字 + `Button_ViewOutput_Click`（E9D9）/ `Button_OpenOutputFolder_Click`（E838）｜理由：Linux 侧 `Features/Runtime/OutputCapture` 捕获 stdout/stderr，需要入口；两个处理器在 `.xaml.cs:35-72` 与 `:159-175` 有实体实现。
- **[移植 第 46 行 · 运行输出区说明 TextBlock]** 文案外观（i18n 缺口）｜功能损失：否｜建议：**待定 → 建议补 Lang 键**｜上游：无此段｜移植：中文硬编码「游戏本体与「后启动程序」的 stdout/stderr 会一直捕获并落盘…这里只控制「怎么看」。」｜理由：同页其余文字都走 `lang:`，唯独这句写死；非中文语种界面会出现中文（`docs/linux-port/25-deviation-register.md:93` 声称"诊断区文案全部走 Lang（9 个键）"，与此行不符，属登记与实际不一致）。

---

## Features/Setting/AdvancedSetting.xaml.cs

- 变更行数：165（删除 1 / 新增 145，3 个 hunk）
- 编译状态：参与编译。

### 差异清单

- **[移植 第 1-10 行 · using 块（去掉 BOM，新增 Microsoft.UI.Xaml / .Controls / Serilog / Starward.Features / System / System.Collections.Generic / System.Diagnostics / System.IO / System.Text）]** 移植新增｜功能损失：否｜建议：保留｜上游：只有 RPC/URL Protocol 需要的少量 using｜移植：新增 ContentDialog、Serilog、OutputCapture 等所需引用｜理由：新增功能所需的依赖，全部实际用到（`ContentDialog`、`StringBuilder`、`OutputCapture`）。
- **[移植 第 5-15 行 · LUNARWARD-DEVIATION 注释块（6 行）]** 平台缺失说明｜功能损失：否｜建议：保留｜上游：此页有「URL Protocol」注册/卸载按钮与「RPC 服务」开关｜移植：注释说明上游 0.19.0 已删除这两块、RPC 子进程项目整体移除、`AppConfig.KeepRpcServerRunningInBackground` 变 `[Obsolete(..., true)]`、URL Protocol 改由安装器注册且移植另有 `--register-protocol / --unregister-protocol / --protocol-status` CLI｜理由：符合硬规则 4（偏离要有原因），且说明与代码一致（`Program.Linux.cs` 有这三个开关）。
- **[移植 第 30 行 · ctor]** 与上游一致｜功能损失：否｜建议：保留｜上游/移植：均 `InitializeComponent()`｜理由：页面能正常构造（无"缺 InitializeComponent 导致空白页"问题）。
- **[移植 第 35-72 行 · Button_ViewOutput_Click]** 移植新增｜功能损失：否｜建议：保留（但见下一条）｜上游：无｜移植：`ContentDialog` 展示 `Features.Runtime.OutputCapture.Recent(500)`，主按钮「刷新」/次按钮「打开日志文件夹」/关闭「关闭」，主按钮时递归重开对话框｜理由：Linux 侧唯一查看运行输出的入口；`ContentDialog` 在 Uno Skia 可用（本页与其它页均使用）。
- **[移植 第 40-44 行 · 复用 Diagnose 系列文案键（Lunarward_Diagnose_Refresh / Lunarward_Diagnose_OpenLogFolder / Lunarward_Diagnose_Close / Lunarward_Diagnose_OutputTitle）]** 文案外观｜功能损失：否｜建议：待定（`Lunarward_Diagnose_OutputTitle` 与 `Lunarward_Output_Title` 语义重复）｜上游：无｜移植：输出对话框借用诊断区键｜理由：键都存在（`Lang.resx` 各 1 处），只是命名语义混用；若日后改文案会互相牵连。
- **[移植 第 46 行 · `_ = ShowOutputDialogAsync();` 自递归重开]** 移植新增｜功能损失：否｜建议：保留（低风险）｜上游：无｜移植：主按钮点击后再次调用自身重新打开对话框｜理由：可接受写法，但递归深度受用户点击次数约束，建议改成 `while` 循环（非必须）。
- **[移植 第 75-88 行 · BuildOutputText]** 移植新增｜功能损失：否｜建议：保留并修一行｜上游：无｜移植：拼接最近 500 行 `OutputLine`（`HH:mm:ss.fff [scope] text`）｜理由：实现正确。
- **[移植 第 80 行 · BuildOutputText 空列表返回值]** 文案外观（i18n 缺口）｜功能损失：否｜建议：**改用已存在的 `Lang.Lunarward_Output_Empty`**｜上游：无｜移植：硬编码中文「（还没有捕获到输出。…）」｜理由：`Lunarward_Output_Empty` 键已在 `Lang.resx` 定义（grep 1 处）却没被引用，等于白建一个键。
- **[移植 第 92-104 行 · Button_CreateDiagnose_Click]** 移植新增｜功能损失：否｜建议：保留（见下条落点一致性）｜上游：无｜移植：`Helpers.DiagnosticBundle.CreateAsync(logger: _logger)` + `InAppToast`｜理由：实现可用。
- **[移植 第 107-117 行 · Button_OpenDiagnoseFolder_Click + DownloadFolder()]** 行为丢失（与本页另一个按钮口径不一致）｜功能损失：是（轻微）｜建议：**统一落点**｜上游：无｜移植：打开 XDG `XDG_DOWNLOAD_DIR`（读 `XDG_CONFIG_HOME/user-dirs.dirs`，回落 `$HOME`，见 `DownloadFolder()` 与注释 D134）｜理由：**诊断包实际落点是硬编码 `~/Downloads`**——`src/Lunarward/Helpers/DiagnosticBundle.cs:33-35` `Path.Combine(Environment.GetFolderPath(SpecialFolder.UserProfile), "Downloads")` 并 `Directory.CreateDirectory` 造目录。当 `XDG_DOWNLOAD_DIR` ≠ `~/Downloads`（如 `~/下载`）时，用户点「打开文件夹」看到的是空目录，刚生成的 `lunarward-diagnose-*.zip` 在别处。建议 `CreateAsync` 接受同一个 `DownloadFolder()` 结果。
- **[移植 第 159-175 行 · Button_OpenOutputFolder_Click / OpenOutputFolder()]** 移植新增｜功能损失：否｜建议：保留｜上游：无｜移植：`AppConfig.LinuxDataHome/lunarward/logs/output` + `Process.Start("xdg-open")`｜理由：与移植侧日志落点一致；`xdg-open` 缺失时靠 try/catch 吞掉（已有 `_logger.LogWarning`）。

---

## Features/Setting/GeneralSetting.xaml

- 变更行数：21（删除 20 / 新增 1，1 个 hunk）
- 编译状态：参与编译（Page item）。

### 差异清单

- **[移植 第 56 行 · 原「透明/动画效果」整段（上游 56-76 行）替换为一行注释]** 平台缺失｜功能损失：否｜建议：保留｜上游：`MenuFlyoutSeparator` + 提示 `Lang.SettingPage_ForABetterExperiencePleaseEnableTheFollowingFeaturesInSystemSettings` + 两个 `Hyperlink`（`Click="Hyperlink_VisualEffects_Click"`、`NavigateUri="ms-settings:easeofaccess-visualeffects"`，文本 `AppearanceSettingPage_TransparencyEffects` / `AppearanceSettingPage_AnimationEffects`）｜移植：注释「Linux：透明/动画效果是 Windows 系统设置项，Linux 下无对应开关，整段移除（D61）」｜理由：`ms-settings:` 是 Windows URI scheme，Linux 无对应系统设置页；`25-deviation-register.md:93` 也把该处理器列为"删掉的死处理器"。
- **[移植 第 20-27 行 · 语言 ComboBox（SelectionChanged="ComboBox_Language_SelectionChanged"）]** 与上游一致｜功能损失：否｜建议：保留｜上游/移植：同为 `Header="{x:Bind lang:Lang.SettingPage_Language}"` + 13 个 `ComboBoxItem`｜理由：加密核对一致。
- **[移植 第 44-53 行 · 关闭窗口行为两个 RadioButton（Checked="RadioButton_CloseWindowOption_Checked"，Tag 绑 `sfv:MainWindowCloseOption.Hide/Exit`）]** 与上游一致｜功能损失：否｜建议：保留｜理由：`GroupName`、`IsChecked` 差值绑定均与上游逐字相同。

---

## Features/Setting/GeneralSetting.xaml.cs

- 变更行数：23（删除 12 / 新增 2，4 个 hunk；其中 1 个 hunk 是 BOM）
- 编译状态：参与编译。

### 差异清单

- **[移植 第 1 行 · 文件 BOM 去除]** 文案外观（无功能影响）｜功能损失：否｜建议：保留｜理由：全项目多数新增文件无 BOM，不影响编译。
- **[移植 第 10 行 · 新增 `using Starward.Features;`]** 移植新增｜功能损失：否｜建议：保留｜理由：`LanguageChangedMessage`（`Features/Setting/LanguageChangedMessage.cs`）与 `WindowBase` 相关类型需要；实际有引用。
- **[移植 第 182 行 · 删除 `#region 系统视觉效果` 注释]** 死代码清理｜功能损失：否｜建议：保留｜理由：区域内容已整体删除。
- **[移植 第 183 行起 · 删除 `Hyperlink_VisualEffects_Click` 处理器（UP 15 行）]** 平台缺失｜功能损失：否｜建议：保留｜上游：处理器调 `Launcher.LaunchUriAsync(new Uri("ms-settings:easeofaccess-visualeffects"))`｜移植：无｜理由：XAML 里对应 Hyperlink 已删，处理器留着就是死代码。
- **[保留部分复核 · ComboBox_Language_SelectionChanged（移植 88-114 行）]** 与上游一致｜功能损失：否｜建议：保留｜移植：`SetLanguage` + `_hoYoPlayService.ResetUILanguage()` + `Bindings.Update()` + `Send(LanguageChangedMessage)` + `AppConfig.SaveConfiguration()`｜理由：语言切换既刷新界面消息、也写 `config.ini`，落盘链路完整（`SaveConfiguration` 调用点含本文件 102 行）。
- **[保留部分复核 · RadioButton_CloseWindowOption_Checked（移植 158-175 行）]** 与上游一致｜功能损失：否｜建议：保留｜移植：写 `AppConfig.CloseWindowOption`（`SetValue` → SQLite `Setting` 表，立即落盘，无需 `SaveConfiguration`）｜理由：读取点在 `App.xaml.cs`/`WindowBase.Linux.cs` 关闭路径上，写读闭环成立。

---

## Features/Setting/DownloadSetting.xaml.cs

- 变更行数：19（删除 7 / 新增 12，3 个 hunk）
- 编译状态：参与编译。对应 `DownloadSetting.xaml` 与上游 **逐字相同**（不在差异清单内）。

### 差异清单

- **[移植 第 1 行 · BOM 去除]** 文案外观｜功能损失：否｜建议：保留。
- **[移植 第 6-10 行 · 新增 5 个 using（CommunityToolkit.Mvvm.Input / Starward.Features / Starward.Helpers / System.IO / Windows.System）]** 死代码｜功能损失：否｜建议：**删除**｜移植：全文无 `RelayCommand`、无 `Starward.Features.*`/`Starward.Helpers.*` 类型、无 `File/Path/Directory`、无 `Windows.System` 类型（已逐行核对 72 行全文）｜理由：`NoWarn` 掩盖掉警告；顺带 `using Starward.Features.RPC;`（第 2 行）与 `using Starward.GameInstall;`（第 3 行）也已无用（第 57 行用的是全限定 `Starward.GameInstall.GameInstallRunner`）→ 共 7 个死 using。
- **[移植 第 50-65 行 · SetRateLimiterAsync 取代上游 `RpcService.CheckRpcServerRunning()` + gRPC `GameInstallerClient.SetRateLimiterAsync`]** 平台替换｜功能损失：否｜建议：保留｜上游：`if (RpcService.CheckRpcServerRunning()) { ... gRPC 调安装器 ... }`｜移植：`Math.Clamp(value * 1024, 0, int.MaxValue)` → `AppConfig.GetService<Starward.GameInstall.GameInstallRunner>().SetRateLimiter(limit > 0, limit)`｜理由：`GameInstallRunner.SetRateLimiter(bool,int)` 存在于 `src/Starward.GameInstall/GameInstallRunner.cs:49`，`sc.AddStarwardGameInstall()`（`GameInstallServiceCollectionExtensions.cs:18`）注册了它 → 运行时成立；文件内注释（D134）如实记录"以前是空壳、改滑块要等下次安装才生效"。
- **[落盘闭环复核 · SpeedLimit 属性（36-46 行）]** 与上游一致｜功能损失：否｜建议：保留｜理由：setter `AppConfig.SpeedLimitKBPerSecond = value`（42 行，立即写 SQLite）+ 即时下发 runner（43 行）；消费者存在：`Features/GameInstall/GameInstallService.cs:233` `int limit = Math.Clamp(AppConfig.SpeedLimitKBPerSecond * 1024, 0, int.MaxValue);` → 下次安装开始时会读到落盘值。
- **[未变更但存疑 · DownloadSetting.xaml 第 31 行 `<Button Content="{x:Bind lang:Lang.Common_Confirm}" />`（无 Click、无 Command）]** 死代码（两侧同源）｜功能损失：否（无影响，只是无效控件）｜建议：待定（要删得同步改上游，会新增偏离）｜理由：上游同样存在这个无事件按钮，属上游遗留；移植保持一致，不单独修。

---

## Features/Setting/FileManageSetting.xaml

- 变更行数：5（删除 2 / 新增 1，2 个 hunk）
- 编译状态：参与编译（Page item）。

### 差异清单

- **[移植 第 55-61 行 · 当前数据文件夹 TextBlock（上游 56-61 行）删掉 `IsTextTrimmedChanged="TextBlock_IsTextTrimmedChanged"`]** 行为丢失｜功能损失：是（轻微）｜建议：**改回上游（加回该属性）**｜上游：`MaxLines="2" TextTrimming="CharacterEllipsis" TextWrapping="Wrap"` + `IsTextTrimmedChanged="TextBlock_IsTextTrimmedChanged"`｜移植：其余属性一致，独缺事件｜理由：**该处理器不是悬空的**，它是基类 `PageBase` 的 protected 方法——`src/Starward/Features/PageBase.cs:72` 与 `src/Lunarward/Features/PageBase.cs:97` 都有同一实现 `if (sender.FontSize > 12) sender.FontSize -= 1;`。删掉后长路径（`/home/<user>/...` 两行仍放不下时）不再自动缩字号，只会被 `CharacterEllipsis` 硬切。同目录 `ScreenshotSetting.xaml:62` 仍保留该属性，处置不一致。
- **[移植 第 83 行 · 备份按钮 `FontIcon Glyph="&#xE74E;"`（上游 84 行 `&#xF413;`）]** 平台替换｜功能损失：否｜建议：**保留**｜上游：F413（Segoe Fluent Icons 的 Save）｜移植：E74E（Uno Symbols 的 Save）｜理由：L-15/L-88 已确认 Uno 拦截 `Segoe Fluent Icons` / `Segoe MDL2 Assets` 族名，随包字体不加载；SettingPage 等页面同样做过码位替换。
- **[逐条复核 · 第 34/48/79/94/115/134/181 行各 Command 绑定、第 89-96 行 ProgressRing/Visibility、第 163-171 行五个缓存大小、第 198 行 `ClearLauncherBackground` CheckBox]** 与上游一致｜功能损失：否｜建议：保留｜理由：全部逐行对比过，无缺键（本页所有 `lang:` 键均为上游既有键，无 `Lunarward_*`）。

---

## Features/Setting/FileManageSetting.xaml.cs

- 变更行数：23（删除 5 / 新增 18，6 个 hunk）
- 编译状态：参与编译。

### 差异清单

- **[移植 第 1-4 行 · 删除 `using SharpSevenZip;`]** 平台替换｜功能损失：否｜建议：保留｜上游：用 `SharpSevenZipCompressor` + 原生 `7z.dll` 压数据库备份｜移植：改用 `DatabaseService.CreateArchive`｜理由：`SharpSevenZip` 的 Linux 侧依赖 `7z.dll`（Windows-only），`docs/linux-port/24-dependency-audit.md` 已把它列为不可用依赖。
- **[移植 第 192-197 行 · 备份产物 `.7z` → `.zip`，压缩实现换 `DatabaseService.CreateArchive(archive, file)`]** 平台替换｜功能损失：否｜建议：保留｜上游：`Path.ChangeExtension(file, ".7z")` + `new SharpSevenZipCompressor().CompressFiles(archive, file)`｜移植：`.zip` + `DatabaseService.CreateArchive(archive, file)` + 3 行注释（只压当次 `.db`，不含 WAL/SHM）｜理由：`DatabaseService.CreateArchive` 是移植已有 API（`DatabaseService.cs:121/130/1258` 在用），产物 ZIP；`DatabaseService.cs:113-115` 注释写明「D134：以前产物是 ZIP、名字是 .7z」——即命名与格式现已自洽。**注意向后兼容**：历史备份文件后缀是 `.7z`（内容其实是 ZIP），`GetLastBackupTime` 只读 `Setting` 表里的路径，不影响展示。
- **[移植 第 75 行 / 第 132 行 · `AppInstance.GetCurrent().UnregisterKey()` → `UnregisterInstanceKey()`]** 平台替换｜功能损失：否｜建议：保留｜上游：直接调 WinRT `AppInstance.UnregisterKey()`｜移植：包一层 try/catch static 方法（第 437-448 行，注释 B13）｜理由：Uno 下 `AppInstance` 可能抛（单实例键非 Windows App SDK 实现），包 try/catch 避免退出路径崩。
- **[移植 第 437-448 行 · 新增 `UnregisterInstanceKey()`]** 移植新增｜功能损失：否｜建议：保留｜理由：见上；`Serilog Debug` 记录失败，不影响主流程。
- **[移植 第 99 行 · `await Launcher.LaunchUriAsync(new Uri(AppConfig.UserDataFolder))`（「打开数据文件夹」）]** 平台缺失（未替换的 WinRT launcher）｜功能损失：待定（可能点了没反应）｜建议：**待定 → 建议改 `Helpers.OpenUrl.Open(AppConfig.UserDataFolder)`**｜上游：同一行｜移植：同上，未替换｜理由：`docs/linux-port/25-deviation-register.md:2004` 已登记这条（"未替换（与上游一致，不构成偏离）…在无 xdg-open 的环境里可能点了没反应（未核实 Uno launcher 的 Linux 回落）"）；移植已有标准替代 `Helpers/OpenUrl.cs`（portal → xdg-open → gio），`ScreenshotSetting.Linux.cs:137-147`、`AdvancedSetting.xaml.cs:113` 都用它，本页三处是例外。
- **[移植 第 222-228 行 · `StorageFile.GetFileFromPathAsync` + `FolderLauncherOptions.ItemsToSelect` + `Launcher.LaunchFolderAsync(folder, options)`（「打开上次备份」= 在文件管理器里选中文件）]** 平台缺失｜功能损失：待定（"选中文件"在 Linux 无等价能力）｜建议：**待定 → 退化为 `OpenUrl.Open(DatabaseBackup目录)`**｜上游：同一段逻辑｜理由：`AppBackground.Linux.cs:949` 注释已明说"Linux 没有等价能力"；本页仍是上游写法，`25:2004` 同样登记为未替换。最坏表现是点了没反应（Linux 上是静默失败而非报错）。
- **[移植 第 268-275 行 · `StorageFile.GetFileFromPathAsync` + `Launcher.LaunchFolderPathAsync(...)`（「打开日志文件夹」两处）]** 平台缺失｜功能损失：待定｜建议：**待定 → 改 `Helpers.OpenUrl.Open(目录)`**｜上游：同一段｜理由：同上；`StorageFile.GetFileFromPathAsync` 在 Uno 的 Linux 后端是"能编译、不保证有实现"的 WinRT API，与 `OpenUrl` 相比没有优势。
- **[逐条复核 · ChangeUserDataFolderAsync(49-84)/DeleteAllSettingAsync(114-142)/GetLastBackupTime(157-175)/BackupDatabaseAsync(180-208)/UpdateCacheSizeAsync(311-326)/ClearCacheAsync(356-375)/ClearUpLauncherBackgroundFileAsync(397-429)]** 与上游一致（除上面列出的 7z→zip）｜功能损失：否｜建议：保留｜理由：`ChangeUserDataFolderAsync` 的 `AppConfig.SaveConfiguration()`（74 行）+ `UnregisterInstanceKey`（75）+ `Process.Start`（76）+ `App.Current.Exit(true)`（77）链路完整，用户数据目录切换后会重启生效。

---

## Features/Setting/ScreenshotSetting.xaml

- 变更行数：11（删除 8 / 新增 3，5 个 hunk）
- 编译状态：参与编译（Page item）。**其 code-behind 被 `<Compile Remove>`，由 `ScreenshotSetting.Linux.cs` 提供。**

### 差异清单

- **[移植 第 61-62 行 · `IsTextTrimmedChanged` 位置调整（上游 61 行在 Foreground 前，移植 62 行在 Foreground 后）]** 文案外观（纯属性顺序）｜功能损失：否｜建议：保留（无需动）｜理由：属性集合完全相同，XAML 语义无差别。
- **[移植 第 81 行 · 注释改「Linux 口径（D61）…」]** 文案外观｜功能损失：否｜建议：保留｜上游：「截图保存路径支持环境变量，如 %USERPROFILE%」之类注释｜移植：改为 X11 轮询说明｜理由：仅注释。
- **[移植 第 84 行 · 热键提示文本 `{x:Bind lang:Lang.HotkeySetting_SingleKeyShortcutsTips}` → 硬编码中文]** 文案外观（i18n 缺口）｜功能损失：否｜建议：**待定 → 建议抽成 `Lunarward_Hotkey_Description`（HotkeySetting 页已有同名键可直接复用）**｜上游：走 Lang 键｜移植：中文写死「Linux 下截图热键通过轮询 X11 键盘状态实现，不需要管理员权限。纯 Wayland 会话不可用，可以把系统快捷键绑定到 lunarward --screenshot。」｜理由：`src/Starward.Language/Lang.Lunarward.cs:163` / `Lang.resx:3080` 已有 `Lunarward_Hotkey_Description`，本页却另写一份硬编码；两页同一句话两种来源，日后改一处会不一致。
- **[移植 第 160-163 行（原 164-169 行区）· 删除 `TextBlock_CaptureError`（x:Name，`Foreground="{ThemeResource SystemFillColorCautionBrush}"`，`Visibility="Collapsed"`）]** 死代码清理 / 连带行为丢失｜功能损失：是（轻微：失败原因不再显示在页面内）｜建议：**待定**｜上游：`ScreenshotSetting.xaml.cs:415-422` 会写 `TextBlock_CaptureError.Text` 并置 `Visibility=Visible`（测试截图失败原因）｜移植：XAML 块删除，`ScreenshotSetting.Linux.cs:174-186 TestCaptureAsync` 改用 `InAppToast` 弹提示｜理由：删除与移植实现是配套的（toast 替代内联错误行），可接受；但 toast 是瞬时提示，用户切走就看不到原因，属弱化。
- **[逐条复核 · 第 88-96 行 `HotkeyInput_ScreenshotCapture`（HotkeyId=44445、三个事件、`WindowHandle="{x:Bind WindowHandle}"`）]** 与上游一致｜功能损失：**是**（处理器为空 + 未初始化显示，见下个文件）｜建议：见 `ScreenshotSetting.Linux.cs` 条目｜理由：XAML 侧无问题，问题在 code-behind。
- **[死代码 · 第 109 行 `x:Name="Segmented_ScreenshotFormat"`、第 116 行 `x:Name="Segmented_ScreenshotQuality"`]** 死代码｜功能损失：否｜建议：**删除 x:Name**（控件本身要留，`SelectedIndex` 绑 `ScreenshotForamt`/`ScreenshotQuality`）｜理由：`ScreenshotSetting.Linux.cs` 全文 grep 无这两名字的引用。

---

## Features/Setting/ScreenshotSetting.Linux.cs（替代被移除的 `ScreenshotSetting.xaml.cs`）

- 变更行数：上游 444 行 code-behind 整体换成 210 行新实现（不在 U0 diff 内，属 `<Compile Remove>` 的替代文件；按"同名差异"计入本组）
- 编译状态：**唯一参与编译的截图设置 code-behind**。

### 差异清单

- **[移植 第 98-105 行 · ctor 只调 `InitializeComponent()` + 初始化 `ScreenshotFolder` / `AutoCopyScreenshotToClipboard`，缺 `HotkeyManager.InitializeHotkeyInput(HotkeyInput_ScreenshotCapture)`]** 行为丢失｜功能损失：**是**｜建议：**补上该调用**｜上游：`ScreenshotSetting.xaml.cs:34-39` ctor 调 `InitializeHotkeyInput()`，其 `:57-66` 对 `HotkeyInput_ScreenshotCapture` 调 `HotkeyManager.InitializeHotkeyInput`｜移植：无｜理由：`HotkeyInput.State=None` 分支在 `HotkeyText` 为空时显示「点击设置快捷键」（`HotkeyInput.xaml.cs:44-58`），而只有 `SetHotkey(...)` 才会填 `HotkeyText`。结果是：**用户看到"截图热键未设置"，实际 `Alt+D` 正在生效**（`HotkeyManager.Linux.cs:15` 默认 `Alt+D` 且 `MainView.xaml.cs:294` 启动时已注册）。这是一处直接误导。
- **[移植 第 189-200 行 · `HotkeyInput_HotkeyEditing` / `HotkeyInput_HotkeyDeleted` / `HotkeyInput_HotkeyEditFinished` 三个空处理器（带 `// LINT-ALLOW`）]** 行为丢失（S1）｜功能损失：**是**｜建议：**照 `HotkeySetting.xaml.cs` 补实现**｜上游：三个处理器分别 `HotkeyManager.UnregisterHotkey` / `DeleteHotkey` / `RegisterHotkey` + 失败 toast（`HotkeyManager_TheShortcutKeys0IsAlreadyInUse` / `FailedToRegisterTheShortcutKeys0`）+ 设置 `((HotkeyInput)sender).State`｜移植：全空｜理由：`HotkeyInput` 只负责改控件文本并抛事件，**注册与落盘都在页面的 `HotkeyEditFinished` 里**（`HotkeySetting.xaml.cs:60-105` 是现成的正确范本）。因此本页改截图热键：不注册、不写 `ScreenshotCaptureHotkey`、失败也没有任何提示——用户在设置页里"改了个寂寞"。同一功能在 `HotkeySetting` 页可正常改，说明这是移植遗漏而非平台限制。（`HotkeyManager.Linux.cs` 的 `hwnd` 参数一律忽略，所以传入 0 也能工作。）
- **[移植 第 95 行 · `public string? WindowHandle { get; set; }`（vs 上游 `public nint WindowHandle => XamlRoot.GetWindowHandle();`）]** 平台替换 / 类型错位｜功能损失：否（`hwnd` 被忽略）｜建议：**改回 `nint`**（`XamlRoot.GetWindowHandle()` 在 Linux 返回 0）｜移植：`string?` 且从未赋值，`x:Bind` 到 `HotkeyInput.WindowHandle`（该属性是 `nint`，`HotkeyInput.xaml.cs:28`）｜理由：类型不一致却能编译（x:Bind 走了隐式转换/或绑定被降级），运行期恒 `null`/0；统一成 `nint` 可与 `HotkeySetting:16` 及上游语义对齐，避免误导后续维护者。
- **[移植 第 203-209 行 · 私有 `TextBlock_IsTextTrimmedChanged`]** 死代码/重复｜功能损失：否｜建议：**删除**（基类已提供）｜理由：`PageBase`（`src/Lunarward/Features/PageBase.cs:97`）已有同签名 protected 方法且实现完全相同（`if (sender.FontSize > 12) sender.FontSize -= 1;`），XAML 里的事件绑定对两种都能解析；重复定义只会让人误以为这是页面特例。
- **[移植 第 108-110 / 113-119 行 · `EffectiveFolder` + `OnNavigatedFrom`]** 移植新增｜功能损失：否｜建议：保留｜理由：Linux 用 `AppConfig.ScreenshotFolder` 单一目录 + 离开页面时统一落盘，替代上游的多目录管理（`ScreenshotFolderManageDialog` 已接入，见 `25-deviation-register.md:93`）。
- **[移植 第 122-147 行 · `ChangeScreenshotFolderAsync` / `OpenScreenshotFolder`]** 平台替换｜功能损失：否｜建议：保留｜上游：`FolderPicker` + `Launcher.LaunchFolderAsync`｜移植：`Helpers.FileDialogHelper.PickFolderAsync` + `Helpers.OpenUrl.Open`｜理由：Linux 无 WinRT 文件夹选择器，走 zenity/kdialog portal 包装；与 `FileDialogHelper` 的既有处理一致。
- **[移植 第 150-171 行 · `ClearThumbnailCache`]** 移植新增｜功能损失：否｜建议：保留｜理由：清 `AppConfig.CacheDirectory/thumb`，与 XAML 第 162 行的按钮对应。
- **[移植 第 174-186 行 · `TestCaptureAsync` → `ScreenCaptureLinux.CaptureAsync()` + toast]** 平台替换｜功能损失：否｜建议：保留｜上游：`User32.MonitorFromWindow` + `ScreenCaptureService`（全被 `<Compile Remove>`）｜理由：Linux 侧截图走外部工具（spectacle/grim/gnome-screenshot），实现见 `Features/Screenshot/ScreenCapture.Linux.cs`。
- **[未绑定读端 · `EnableScreenshotColorManagement`（80-91 行）/`AutoConvertScreenshotToSDR`（67-78 行）两个属性 → XAML 第 134-149 行两个 ToggleSwitch]** 写了没人读｜功能损失：**是**（用户改了没效果）｜建议：**置灰或加说明（S2）**｜理由：`src/Lunarward/Features/Screenshot/ScreenCapture.Linux.cs` 只读 `ScreenCaptureSavedFormat`（:128）、`ScreenCaptureEncodeQuality`（:145）、`AutoCopyScreenshotToClipboard`（:177），全文 grep **没有** `EnableScreenshotColorManagement` / `AutoConvertScreenshotToSDR`；这两个键的唯一读端在被排除的 `ScreenCaptureService.cs:99/139/319/327/341/355/422`。Linux 无 HDR/色彩管理，所以能力缺失本身合理，但 UI 让用户以为能开关 → 应按 `GamepadControlSetting` 的做法 `IsEnabled="False"` 或加 InfoBar 说明。

---

## Features/Setting/AboutSetting.xaml

- 变更行数：6（删除 3 / 新增 3，2 个 hunk）
- 编译状态：参与编译（Page item），code-behind `AboutSetting.xaml.cs` 也在编译清单。

### 差异清单

- **[移植 第 22 行 · Logo `Source="ms-appx:///Assets/lunarward.png"`（上游 `Assets/logo.png`）]** 移植新增（品牌替换）｜功能损失：否｜建议：**保留**｜理由：资产存在且有打包规则——`src/Lunarward/Assets/lunarward.png`（46318 字节）+ `Lunarward.csproj:93` `<Content Include="Assets/lunarward.png" CopyToOutputDirectory="PreserveNewest" />`；上游 `Assets/**` 由 `csproj:38` 的 vendor 镜像 `Content Include="../../vendor/upstream/large-resource/Starward.Assets/Assets/**/*"` 提供，`logo.png` 仍在但不该用于 Lunarward。
- **[移植 第 27 行 · `Text="Lunarward"`（上游 `Text="Starward"`）]** 文案外观（品牌）｜功能损失：否｜建议：保留｜理由：品牌名必须改。
- **[移植 第 28 行 · `Text="Lunarward · 基于 Starward（MIT）的 Linux 移植"`（上游 `Developed by Scighost, all rights reserved.`）]** 文案外观（i18n 缺口）｜功能损失：否｜建议：**待定 → 抽 Lang 键（如 `Lunarward_About_Description`）**｜理由：中文写死在 XAML，非中文语种用户看到中文；同页其余文本都走 `lang:` 键。
- **[未变更但存疑 · 第 93-125 行五个 Hyperlink 全部指向上游 Starward]** 移植新增缺失（信息链未改）｜功能损失：**是**（轻微）｜建议：**把「报告问题」与「发布历史」改指 `QiE2035/Lunarward`**（至少这两条）｜上游：`https://starward.scighost.com`、`https://github.com/Scighost/Starward/issues`、`https://github.com/Scighost/Starward/releases`、`https://github.com/Scighost/Starward/blob/main/docs/Privacy.md`、`https://donate.scighost.com`｜移植：与上游逐字相同，未改｜理由：Lunarward 用户点「报告问题」会到 Windows 版 Starward 的 issue 列表、点「发布历史」下载到 Windows 安装包；`docs/linux-port/25-deviation-register.md` 对 AboutSetting 只有 logo/文案两条，未覆盖链接。隐私政策/捐赠保留上游尚可接受（移植确实基于其代码），issues/releases 不改会直接把用户引错地方。
- **[逐条复核 · 第 38 行 `Text="{x:Bind s:AppConfig.AppVersion}"`]** 与上游一致｜功能损失：否｜建议：保留｜理由：`AppConfig.Configuration.cs:89` 取 `AssemblyInformationalVersion`、回落 `AssemblyName.Version` → 显示的是 Lunarward 自己的版本号，与 `Lunarward.csproj` 的版本一致。
- **[逐条复核 · 检查更新（code-behind 57-81 行）]** 平台替换（未在 diff 内）｜功能损失：否｜建议：保留｜理由：`UpdateService.Linux.cs` 查 AUR（`https://aur.archlinux.org/rpc/v5/info?arg[]=lunarward`）而非 GitHub Release；有新版本时 `new UpdateWindow { NewVersion = release }.Activate()`（`UpdateWindow.Linux.cs` 参与编译），无更新时显示"已是最新版本"。

---

## Features/Setting/HotkeySetting.xaml

- 变更行数：4（删除 2 / 新增 2，2 个 hunk）
- 编译状态：参与编译（Page item）；code-behind `HotkeySetting.xaml.cs` 与上游**逐字相同**且在编译清单内。

### 差异清单

- **[移植 第 19 行 · 注释「Linux 口径（D61）：热键走 X11 轮询，不需要管理员权限；纯 Wayland 会话不可用。」（上游：「如果注册单键快捷键 (F1-F12)，需要以管理员权限运行 Starward…」）]** 文案外观（注释）｜功能损失：否｜建议：保留｜理由：仅注释，实际口径与 `Helpers/HotkeyManager.Linux.cs`（X11 轮询 + `X11KeyState`）一致。
- **[移植 第 22 行 · 提示文本 `{x:Bind lang:Lang.HotkeySetting_SingleKeyShortcutsTips}` → `{x:Bind lang:Lang.Lunarward_Hotkey_Description}`]** 文案外观（平台口径）｜功能损失：否｜建议：保留｜理由：键存在——`src/Starward.Language/Lang.Lunarward.cs:163`、`Lang.resx:3080`（zh-CN 等有值）；新文案讲的是 Linux 的 X11 轮询，比上游的"管理员权限"准确。
- **[逐条复核 · 功能闭环（本页是"能正常工作"的对照组）]** 与上游一致｜功能损失：否｜建议：保留｜理由：`HotkeySetting.xaml.cs:16` `WindowHandle => XamlRoot.GetWindowHandle()`（`Helpers/XamlRootExtension.Linux.cs:9` 恒返回 0，`HotkeyManager` 忽略 hwnd）；`InitializeHotkeyInput`（31-41）→ 控件显示已保存组合键；`HotkeyInput_HotkeyEditFinished`（60-105）→ `RegisterHotkey`（`HotkeyManager.Linux.cs:100-141`）→ 成功且组合键变化时 `SaveHotkey` → `AppConfig.SetValue(..., "ShowMainWindowHotkey"/"ScreenshotCaptureHotkey")` → 写 SQLite `Setting` 表；`HotkeyDeleted` → 写 `"0"` 表示禁用；读回在 `InitializeHotkey`（25-70）。配合 `MainView.xaml.cs:294` 启动注册 + `X11KeyState` 25ms 轮询（`X11KeyState.cs:18/34/50`）→ 全局热键链路完整。

---

## Features/Setting/GamepadControlSetting.xaml

- 变更行数：8（删除 0 / 新增 8，2 个 hunk）
- 编译状态：参与编译（Page item）；code-behind `GamepadControlSetting.xaml.cs` 与上游一致且参与编译，依赖被替换为 `Features/GamepadControl/GamepadController.Linux.cs`（桩）。

### 差异清单

- **[移植 第 21-28 行 · 新增 InfoBar（Title=`Lunarward_Gamepad_NotSupported_Title`，Message=`Lunarward_Gamepad_NotSupported_Message`，Severity=Informational，IsOpen=True）]** 移植新增｜功能损失：否｜建议：保留｜理由：键都存在（`Lang.Lunarward.cs:164-165`、`Lang.resx:3071/3074`）；上游此页在 Linux 上完全是"看起来能用但全无效"的状态，加 InfoBar 是正确处置（M4 占位，`AGENTS.md:8` 明确悬浮窗/手柄映射/RPC 不做）。
- **[移植 第 35 行 · 「手柄增强」ToggleSwitch 新增 `IsEnabled="False"`]** 平台缺失｜功能损失：否（有意禁用）｜建议：保留（但见下条）｜上游：可自由开关，`EnableGamepadController` setter 调 `GamepadController.Initialize()`｜移植：主开关禁用 → `EnableGamepadController` 恒为 DB 里旧值（默认 false）｜理由：`GamepadController.Linux.cs:11` `Initialize` 是空方法、`Initialized=false`、`NeedInstallGameInputRedist=false` → 开了也毫无作用；禁用主开关是诚实的做法。
- **[依赖间接传导 · 第 87/132/140/163/171 行 `IsEnabled="{x:Bind EnableGamepadController}"`]"整页置灰"的实现方式]** 平台缺失（间接）｜功能损失：否｜建议：**待定（建议显式化，例如整页外套 `IsEnabled` 或在每个控件上直接 `IsEnabled="False"`）**｜理由：下游控件的可用性完全依赖"主开关恒关"，一旦 DB 里 `EnableGamepadController=true`（例如从 Windows 拷来的数据目录），ComboBox/TextBox 会变成可编辑，而桩会把 `Lunarward_Gamepad_NotSupported_Message` 塞进 `GamepadGuideButtonKeyTextEditError`（`GamepadControlSetting.xaml.cs:105-120` + `GamepadController.Linux.cs:39-51`），出现看不懂的错误行。属边角，不必急修。
- **[写了没人读 · `EnableGamepadController` / `EnableGamepadSimulateInput` / `GamepadGuideButtonMode` / `GamepadShareButtonMode`（第 36/88/133/164 行绑定）]** 平台缺失（M4 占位）｜功能损失：否（控件已禁用）｜建议：保留｜理由：`GamepadController.Linux.cs` 不消费这些 `AppConfig` 值（`Initialize` 空、无 Start/轮询），Linux 侧也没有读端；因 UI 已置灰，用户改不了，属明示不可用。
- **[未变更但存疑 · 第 13-15 行 `XboxFluent2` 字体资源 + 第 101/109/113/128/159 行 `FontFamily="{ThemeResource XboxFluent2}"` 图标]** 平台缺失（未验证）｜功能损失：待定（图标可能显示成方框）｜建议：**待定**（低优先）｜理由：L-15/L-88 已确认 Uno 拦截 `Segoe Fluent Icons` / `Segoe MDL2 Assets` 族名导致随包字体不加载；`XboxFluent2` 是另一族名（`ms-appx:///Assets/Font/XboxFluent2.ttf#Xbox Fluent v2`），未实测是否被拦截。旁证：`SettingPage.xaml` 里 GamepadControlSetting 的导航图标已从该字体改成 `Glyph="&#xE7FC;"`（带 LUNARWARD-DEVIATION 注释），说明该字体在移植侧被认为不可靠 → 这几个手柄图标有显示成占位方块的风险，需真机确认。

---

## 汇总

### S1（功能坏了 / 用户可见的真实损失，必须修）

1. `src/Lunarward/Features/Setting/ScreenshotSetting.Linux.cs:189-200` —— 截图热键三个事件处理器是空实现（`HotkeyInput_HotkeyEditing` / `HotkeyInput_HotkeyDeleted` / `HotkeyInput_HotkeyEditFinished`）。在截图设置页改热键：不 `RegisterHotkey`、不写 `AppConfig.ScreenshotCaptureHotkey`、注册失败无提示。修法：照 `Features/Setting/HotkeySetting.xaml.cs:60-105` / `:44-59` 补实现（`HotkeyManager.RegisterHotkey / DeleteHotkey / UnregisterHotkey` + `InAppToast` 失败提示 + 设置 `((HotkeyInput)sender).State`）。
2. `src/Lunarward/Features/Setting/ScreenshotSetting.Linux.cs:98-105` —— ctor 缺 `HotkeyManager.InitializeHotkeyInput(HotkeyInput_ScreenshotCapture)`（上游 `ScreenshotSetting.xaml.cs:34-39` + `:57-66`）。后果：页面显示"点击设置快捷键"，而 `Alt+D` 实际生效（`Helpers/HotkeyManager.Linux.cs:15` + `Features/ViewHost/MainView.xaml.cs:294`）→ 直接误导。随 1 一起修。

### S2（明确的能力/行为缺口，应修或应显式置灰）

3. `src/Lunarward/Features/Setting/ScreenshotSetting.xaml:134-149`（属性读写端：`ScreenshotSetting.Linux.cs:67-91`）—— `EnableScreenshotColorManagement` / `AutoConvertScreenshotToSDR` 两个 ToggleSwitch 在 Linux 上**只写不读**（`Features/Screenshot/ScreenCapture.Linux.cs` 全文只读 `ScreenCaptureSavedFormat`/`ScreenCaptureEncodeQuality`/`AutoCopyScreenshotToClipboard`；唯一读端在被 `<Compile Remove>` 的 `ScreenCaptureService.cs:99/139/319/327/341/355/422`）。按 `GamepadControlSetting` 的处置加 `IsEnabled="False"` 或 InfoBar 说明。
4. `src/Lunarward/Features/Setting/FileManageSetting.xaml.cs:99`（打开数据目录）、`:222-228`（打开上次备份并选中）、`:268-275`（打开日志文件夹 ×2）—— 仍是 WinRT `Launcher.*` / `StorageFile.GetFileFromPathAsync`，Linux 下很可能静默无反应（`docs/linux-port/25-deviation-register.md:2004` 已登记"未核实"）。改用项目自有 `Helpers.OpenUrl.Open(path)`（`ScreenshotSetting.Linux.cs:137-147`、`AdvancedSetting.xaml.cs:113` 即是范本）。
5. `src/Lunarward/Features/Setting/AboutSetting.xaml:100`（报告问题）、`:107`（发布历史）—— 指向上游 `Scighost/Starward` 的 issues / releases，Lunarward 用户会被引到 Windows 版仓库与安装包。至少这两条改指 `QiE2035/Lunarward`（隐私政策 `:114`、捐赠 `:121` 可保留）。

### S3（轻微 / 一致性 / i18n，可批量收）

6. `src/Lunarward/Features/Setting/FileManageSetting.xaml:55-61` —— 删掉了 `IsTextTrimmedChanged="TextBlock_IsTextTrimmedChanged"`，而该处理器是基类 `PageBase`（`src/Lunarward/Features/PageBase.cs:97` / 上游 `src/Starward/Features/PageBase.cs:72`）的 protected 方法，**并非悬空**。长数据目录路径不再自动缩字号，只会被省略号硬切。建议加回（同目录 `ScreenshotSetting.xaml:62` 就保留着）。
7. `src/Lunarward/Helpers/DiagnosticBundle.cs:33-35` vs `src/Lunarward/Features/Setting/AdvancedSetting.xaml.cs:107-117` —— 诊断包落点写死 `~/Downloads`，按钮却打开 `XDG_DOWNLOAD_DIR`；两者不同时用户"打开文件夹"看不到刚生成的 zip。建议 `CreateAsync` 接受 `DownloadFolder()` 的结果。
8. `src/Lunarward/Features/Setting/ScreenshotSetting.Linux.cs:95` —— `public string? WindowHandle` 与上游 `public nint WindowHandle => XamlRoot.GetWindowHandle();` 及 `HotkeyInput.WindowHandle`（`HotkeyInput.xaml.cs:28`，`nint`）类型不一致，运行期恒 null。改回 `nint` 表达式属性。
9. `src/Lunarward/Features/Setting/ScreenshotSetting.Linux.cs:203-209` —— 与基类 `PageBase.TextBlock_IsTextTrimmedChanged` 完全重复的私有处理器，删掉；同时把 `ScreenshotSetting.xaml:62` 留给基类解析。
10. 硬编码中文（均应抽 `Lang` 键）：
    - `AdvancedSetting.xaml:46`（运行输出区说明整段）—— 且与 `docs/linux-port/25-deviation-register.md:93`「诊断区文案全部走 Lang（9 个键）」的登记不符；
    - `AdvancedSetting.xaml.cs:80`（空输出提示）—— 应用已存在但未使用的 `Lang.Lunarward_Output_Empty`；
    - `ScreenshotSetting.xaml:84`（热键提示）—— 应复用 `Lang.Lunarward_Hotkey_Description`（`Lang.Lunarward.cs:163`）；
    - `AboutSetting.xaml:28`（副标题）。
11. 死代码：
    - `DownloadSetting.xaml.cs:2,3,6,7,8,9,10` —— 7 个未使用 using（`Starward.Features.RPC`、`Starward.GameInstall`、`CommunityToolkit.Mvvm.Input`、`Starward.Features`、`Starward.Helpers`、`System.IO`、`Windows.System`），第 57 行用的是全限定名；
    - `ScreenshotSetting.xaml:109,116` —— `x:Name="Segmented_ScreenshotFormat"` / `"Segmented_ScreenshotQuality"` 在 `ScreenshotSetting.Linux.cs` 中零引用（控件保留、删 `x:Name`）；
    - `DownloadSetting.xaml:31` —— 无 `Click`/`Command` 的 `Common_Confirm` 按钮（上游同源，删则新增偏离，建议待定）。

### 明确"无需修"的偏离（避免下轮重复审）

- `AdvancedSetting.xaml:18-63` 诊断区 + 运行输出区（移植新增，D61/D27，键齐全）。
- `GeneralSetting.xaml:56` + `GeneralSetting.xaml.cs:183` 删掉「透明/动画效果」（`ms-settings:` Windows-only）。
- `DownloadSetting.xaml.cs:50-65` 限速改直下发 `GameInstallRunner.SetRateLimiter`（`GameInstallRunner.cs:49`；落盘值另由 `GameInstallService.cs:233` 读取）。
- `FileManageSetting.xaml.cs:192-197` 备份 `.7z`→`.zip` + `DatabaseService.CreateArchive`；`:437-448` `UnregisterInstanceKey` 兜底。
- `FileManageSetting.xaml:83` 字形 `F413`→`E74E`（L-15/L-88）。
- `HotkeySetting.xaml:19,22` 注释与提示键（`Lunarward_Hotkey_Description` 存在）。
- `GamepadControlSetting.xaml:21-28,35` InfoBar + 主开关禁用（M4 占位，明示不可用）。
- `AboutSetting.xaml:22,27` 品牌/Logo（`Assets/lunarward.png` 已由 `Lunarward.csproj:93` 打包）。
- 全组**没有**绑定不存在的 `Lang` 键（逐键 grep 过 `Lang.resx`）；**没有**因缺 `InitializeComponent()` 导致 XAML 不加载的情况（10 个 code-behind 逐个核对过）。
