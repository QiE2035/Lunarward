# 18 · 与上游 Starward 的偏差清单

> 状态：当前（2026-11-13 生成，2026-11-24 重新核对）
> 最后核对：2026-11-24（HEAD `2b86682`）；本文数字以该日 `bash scripts/list-deviations.sh` 的输出为准，之后代码变动会让它漂移，刷新请重跑脚本。
> 生成方式：`bash scripts/list-deviations.sh`（同名文件用 `cmp -s` 比较）与 `grep -rn LUNARWARD-DEVIATION src/Lunarward --include=*.cs --include=*.xaml`
> 用途：同步上游前先看这份清单；本文件只回答「哪些文件偏离了上游」，实现状态看 12 号表，逐点必要性看 25 号表。

## 1. 原则

1. `src/Starward/` 是上游代码的镜像，**零改动**，只做参考与同步来源。
2. 需要 Linux 行为差异时，优先在 `src/Lunarward/` 里**新建** `*.Linux.cs` 分部文件；同名文件**优先只改最小必要行**、不改结构——这是**目标**，不是现状。约束的本质是「上游 `src/Starward/` 零改动 + 同步时冲突可控」，不是「必须一行以内」；按上游语义重写实现的文件（见 1.1）允许大改，但要登记原因。
3. 任何「行为层面」的偏差都要在代码里留 `// LUNARWARD-DEVIATION: <原因>` 标记，并在下面第 3 节列明。
4. 只是排版/文案/图标码位这类**观感**差异，不强制标记，但会出现在第 2 节的自动清单里。

### 1.1 已知的大偏差（同名文件里改动量最大的 10 个）

「增删行」口径：`diff -u src/Starward/<路径> src/Lunarward/<路径> | grep -c '^[+-]'` 再减 2（去掉 `+++`/`---` 两行头）；**增删都计入**，所以整段重排/挪位也会让数字偏大，只作量级参考，不等于「必须改这么多行」。2026-11-24（HEAD `2b86682`）实测：

| 文件 | 增删行 | 类别 | 为什么躲不开 |
| --- | ---: | --- | --- |
| `Features/GameLauncher/GameLauncherSettingDialog.xaml` | 1043 | 重排 + 平台替代 | `FlipView` 单列设置改 `NavigationView` 双栏（窄窗口一屏放不下）；丙烯→实底、图标码位 |
| `Features/GameSetting/GameSettingPage.xaml.cs` | 493 | 重实现 | 异步初始化 + 启动预检/诊断入口；文案改走 `Lang` |
| `Features/GameLauncher/GameLauncherPage.xaml.cs` | 474 | 重实现 + 平台替代 | 外链走 `OpenUrl`、字体族、导航缓存、Wine 进程/占用来回查 |
| `App.xaml.cs` | 368 | 平台替代 | 单实例管道、托盘、XDG 目录、语言初始化 |
| `Features/GameSetting/GameSettingService.cs` | 288 | 平台替代 | `Microsoft.Win32.Registry` → `WineRegistry`（`wine reg`），文案走 `Lang` |
| `Features/ViewHost/MainWindow.xaml.cs` | 278 | 平台替代 | `AppWindow` / 标题栏交还 WM（X11）、激活与托盘时序、`--selftest-ui` 入口 |
| `Features/ViewHost/MainView.xaml.cs` | 258 | 平台替代 | 导航异步化、热键初始化、标题栏/窗口控制 |
| `Features/Screenshot/ScreenshotPage.xaml` | 258 | 重排 | `GridView` 分组 → `ListView` + `NavigationCacheMode`（页面缓存）、图标码位 |
| `Features/PlayTime/PlayTimeRecordService.cs` | 227 | 平台替代 | `wineserver -w` 判游玩时长（Windows 侧是进程/句柄语义） |
| `Features/GameLauncher/GameBannerAndPost.xaml.cs` | 174 | 重实现 | 公告窗改浏览器打开、页面缓存、外链 `OpenUrl` |

这几处是「按上游语义重写实现」，不是顺手改；同步上游时按第 4 节「保留本仓 + 人工搬新逻辑」处理。完整逐点清单见 `25-deviation-register.md`。

## 2. 自动清单（74 个同名文件与上游不同，2026-11-25 快照；上游已同步到 0.19.0）

这些文件大多含「Windows API → Linux 等价物」的分支（例如 `Microsoft.Win32.Registry` → `WineRegistry`、`Windows.Storage` → `File`），
以及排版/文案修正。**条目数以脚本输出为准**：本文的 80 是 2026-11-25（合并 upstream 0.19.0 后、HEAD 为 net11 分支工作区）快照，脚本用 `cmp -s` 比同名文件，行尾/BOM 差异也会计入，所以数字会随代码变动漂移；刷新方式：

```bash
bash scripts/list-deviations.sh
```

<details>
<summary>展开完整清单</summary>

```
AppConfig.Common.cs
AppConfig.Configuration.cs
AppConfig.ServiceProvider.cs
App.xaml
App.xaml.cs
Features/Background/BackgroundFileItem.cs
Features/Background/BackgroundService.cs
Features/Background/BackgroundViewPage.xaml
Features/Background/BackgroundViewPage.xaml.cs
Features/Background/BackgroundViewWindow.xaml.cs
Features/CloudGame/CloudGameButton.xaml.cs
Features/Codec/ImageThumbnail.cs
Features/Database/DatabaseService.cs
Features/Gacha/GachaLogExportFile.cs
Features/Gacha/GachaLogPage.xaml
Features/Gacha/GachaLogPage.xaml.cs
Features/Gacha/GachaStatsCard.xaml
Features/Gacha/GenshinBeyondGachaPage.xaml
Features/Gacha/UIGF/UIGF3File.cs
Features/Gacha/UIGF/UIGF4File.cs
Features/Gacha/UIGF/UIGFGachaService.cs
Features/Gacha/ZZZGachaService.cs
Features/Gacha/ZZZGachaStatsCard.xaml
Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs
Features/GameAccount/GameAccountService.cs
Features/GameAccount/GameAccountSwitcher.xaml.cs
Features/GameInstall/InstallGameDialog.xaml
Features/GameInstall/PreDownloadButton.xaml
Features/GameInstall/PreDownloadButton.xaml.cs
Features/GameInstall/PreDownloadDialog.xaml
Features/GameLauncher/GameBannerAndPost.xaml
Features/GameLauncher/GameBannerAndPost.xaml.cs
Features/GameLauncher/GameLauncherPage.xaml
Features/GameLauncher/GameLauncherPage.xaml.cs
Features/GameLauncher/GameLauncherService.cs
Features/GameLauncher/GameLauncherSettingDialog.xaml
Features/GameLauncher/GameLauncherSettingDialog.xaml.cs
Features/GameLauncher/GameNoticeService.cs
Features/GameLauncher/StartGameButton.xaml
Features/GameLauncher/StartGameButton.xaml.cs
Features/GameRecord/DailyNote/DailyNoteButton.xaml
Features/GameRecord/GameRecordPage.xaml
Features/GameRecord/GameRecordPage.xaml.cs
Features/GameRecord/LoginPage.xaml
Features/GameRecord/StarRail/ApocalypticShadowPage.xaml
Features/GameRecord/StarRail/ForgottenHallPage.xaml
Features/GameRecord/StarRail/PureFictionPage.xaml
Features/GameRecord/StarRail/SimulatedUniversePage.xaml
Features/GameRecord/ZZZ/ShiyuDefensePage.xaml
Features/GameSelector/GameBizDisplay.cs
Features/GameSelector/GameSelector.xaml
Features/GameSelector/GameSelector.xaml.cs
Features/GameSetting/GameSettingPage.xaml
Features/GameSetting/GameSettingPage.xaml.cs
Features/GameSetting/GameSettingService.cs
Features/HoYoPlay/HoYoPlayService.cs
Features/PlayTime/PlayTimeButton.xaml.cs
Features/PlayTime/PlayTimeRecordService.cs
Features/Screenshot/ImageViewWindow.xaml.cs
Features/Screenshot/ScreenCaptureService.cs
Features/Screenshot/ScreenshotFolderManageDialog.xaml
Features/Screenshot/ScreenshotPage.xaml
Features/Screenshot/ScreenshotPage.xaml.cs
Features/SelfQuery/SelfQueryPage.xaml
Features/SelfQuery/SelfQueryPage.xaml.cs
Features/Setting/AboutSetting.xaml
Features/Setting/AdvancedSetting.xaml
Features/Setting/AdvancedSetting.xaml.cs
Features/Setting/DownloadSetting.xaml.cs
Features/Setting/FileManageSetting.xaml
Features/Setting/FileManageSetting.xaml.cs
Features/Setting/GamepadControlSetting.xaml
Features/Setting/GeneralSetting.xaml
Features/Setting/HotkeySetting.xaml
Features/Setting/ScreenshotSetting.xaml
Features/Setting/SettingPage.xaml
Features/Setting/SettingPage.xaml.cs
Features/Update/UpdateWindow.xaml
Features/UrlProtocol/TestUrlProtocolWindow.xaml
Features/UrlProtocol/UrlProtocolService.cs
Features/ViewHost/MainView.xaml
Features/ViewHost/MainView.xaml.cs
Features/ViewHost/MainWindow.xaml.cs
Features/ViewHost/SystemTrayWindow.xaml.cs
Features/ViewHost/WelcomeWindow.xaml.cs
Frameworks/PageBase.cs
Helpers/PointerCursor.cs
Program.cs
```

</details>

## 3. 已登记的偏差标记

1 处显式标记（`grep -rn LUNARWARD-DEVIATION src/Lunarward --include=*.cs --include=*.xaml`，2026-11-24 实测全仓就这一处）：

```
src/Lunarward/Features/Setting/SettingPage.xaml:85:  <!-- LUNARWARD-DEVIATION: XboxFluent2 字体在 Uno/Skia 下不加载（手柄图标显示为方框），改用内置 Symbols 里有字形的 E7FC -->
```

`list-deviations.sh` 会把同一处渲染成 `src/Lunarward/Features/Setting/SettingPage.xaml  →: XboxFluent2 … E7FC -->`（脚本截断了行内空白），标记本身没被脚本吞掉。

行为层面的偏差都该在这里登记；新增偏差时**必须**同时补标记与本表。**现状是欠账**：同名差异文件里只有 1 处带标记，补标记属于后续工作，不是本表漏登记。

## 4. 同步流程

```bash
bash scripts/sync-upstream.sh --dry-run   # 只看上游新增提交与偏差清单
bash scripts/sync-upstream.sh            # 合并；冲突里「已偏差文件」默认保留本仓版本
```

冲突处理口径：

| 冲突位置 | 处理 |
| --- | --- |
| 第 2 节清单里的同名文件 | 保留本仓（`git checkout --ours`），再人工把上游**新增逻辑**搬进来 |
| `src/Starward/` 之外的新文件（上游新增功能） | 直接接受上游版本 |
| `*.Linux.cs`（本仓独有） | 不可能冲突；若上游也新增同名文件，先看语义再决定命名 |
| `Starward.slnx` / `global.json` / `.github/workflows/*` | 保留本仓（D45/D53 已改） |

合并后必须：`dotnet build Starward.slnx -c Debug` + `dotnet test` + `bash scripts/test/cli-smoke.sh`。

## 5. 已知会反复冲突的文件

`GameSelector.xaml(.cs)`（卡片尺寸/横向滚动）、`GameBannerAndPost.*`（公告→浏览器、Pivot 重建）、
`MainView.xaml`（标题栏交还 WM）、`Setting/*.xaml`（实底与 Linux 文案）、`GameSettingPage.*`（异步初始化后重算布局）、
`GameLauncherPage.xaml(.cs)`（背景圆点、导航缓存）、`Features/GameInstall/*`（Sophon 链路重写）。

## 偏差分类表

`18-upstream-table.md` 是**一次性快照 + 人工归类**，不是脚本生成：文件清单取自 `scripts/list-deviations.sh` 输出，每行的「分类」由人工读 diff 归纳（仓库里没有分类生成脚本）。筛选时用的关键词集合沿用首版：外链 OpenUrl / 窗口 X11 / XDG 目录 / 图标码位 / wine 前缀 / 去 Windows API / 页面缓存 / 字体族 / 实底 / 品牌名 / 资源替换 / 去 WinUI 事件。口径与核对日期写在该文件顶部，**逐点判定的权威版本是 `25-deviation-register.md`**（其第 5 行明确取代本文的关键词归类）。
