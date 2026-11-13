# 18 · 与上游 Starward 的偏差清单

> 状态：当前（2026-11-13 生成）
> 生成方式：`bash scripts/list-deviations.sh`（同名文件比较）与 `grep LUNARWARD-DEVIATION`
> 用途：同步上游前先看这份清单；本文件只回答「哪些文件偏离了上游」，实现状态看 12 号表。

## 1. 原则

1. `src/Starward/` 是上游代码的镜像，**零改动**，只做参考与同步来源。
2. 需要 Linux 行为差异时，优先在 `src/Lunarward/` 里**新建** `*.Linux.cs` 分部文件；只有在同名文件里改一行以内、且不改结构时才原地改。
3. 任何「行为层面」的偏差都要在代码里留 `// LUNARWARD-DEVIATION: <原因>` 标记，并在下面第 3 节列明。
4. 只是排版/文案/图标码位这类**观感**差异，不强制标记，但会出现在第 2 节的自动清单里。

## 2. 自动清单（81 个同名文件与上游不同）

这些文件大多含「Windows API → Linux 等价物」的分支（例如 `Microsoft.Win32.Registry` → `WineRegistry`、`Windows.Storage` → `File`），
以及排版/文案修正。刷新方式：

```bash
bash scripts/list-deviations.sh
```

<details>
<summary>展开完整清单</summary>

```
AppConfig.Common.cs
AppConfig.Configuration.cs
AppConfig.ServiceProvider.cs
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
Features/PlayTime/PlayTimeRecordService.cs
Features/Screenshot/ImageViewWindow.xaml.cs
Features/Screenshot/ScreenshotFolderManageDialog.xaml
Features/Screenshot/ScreenshotPage.xaml
Features/Screenshot/ScreenshotPage.xaml.cs
Features/SelfQuery/SelfQueryPage.xaml
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
Features/UrlProtocol/TestUrlProtocolWindow.xaml
Features/UrlProtocol/UrlProtocolService.cs
Features/ViewHost/MainView.xaml
Features/ViewHost/MainView.xaml.cs
Features/ViewHost/MainWindow.xaml.cs
Features/ViewHost/SystemTrayWindow.xaml.cs
Features/ViewHost/WelcomeWindow.xaml.cs
Helpers/PointerCursor.cs
Program.cs
```

</details>

## 3. 已登记的偏差标记

1 处显式标记（`grep -rn LUNARWARD-DEVIATION src/Lunarward`）：

```
# 代码里登记的偏差标记（LUNARWARD-DEVIATION）
```

行为层面的偏差都在这里；新增偏差时**必须**同时补标记与本表。

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
