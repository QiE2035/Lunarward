# 上游偏差分类（自动生成 2026-11-13）

来源：`scripts/list-deviations.sh` 的同名差异文件 + `diff` 关键词粗分类。

共 81 个文件。其中 55 个能被 diff 关键词直接归类；剩下 26 个「小改动」集合（≤40 行）已在 2026-11-14 全部逐个复核完毕，结论见文末「26 个文件复核结论」。

| 变更行 | 文件 | 分类 |
|---:|---|---|
| 2 | `Features/CloudGame/CloudGameButton.xaml.cs` | 外链改 OpenUrl |
| 2 | `Features/Codec/ImageThumbnail.cs` | XDG 目录 |
| 2 | `Features/Gacha/GachaLogExportFile.cs` | 品牌名 |
| 2 | `Features/Gacha/UIGF/UIGF3File.cs` | 品牌名 |
| 2 | `Features/Gacha/UIGF/UIGF4File.cs` | 品牌名 |
| 2 | `Features/Gacha/UIGF/UIGFGachaService.cs` | 去 Windows API |
| 2 | `Features/Gacha/ZZZGachaService.cs` | 品牌名（`export_app="Lunarward"`，只写不读） |
| 2 | `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs` | 外链改 OpenUrl |
| 2 | `Features/Screenshot/ImageViewWindow.xaml.cs` | 外链改 OpenUrl |
| 2 | `Features/Screenshot/ScreenshotFolderManageDialog.xaml` | 图标码位 |
| 2 | `Features/UrlProtocol/TestUrlProtocolWindow.xaml` | 资源替换 |
| 2 | `Features/UrlProtocol/UrlProtocolService.cs` | 去 Windows API |
| 2 | `Helpers/PointerCursor.cs` | 保留（`partial` 必需，去掉报 CS0260） |
| 2 | `Program.cs` | XDG 目录 |
| 3 | `Features/Gacha/GachaLogPage.xaml` | 页面缓存 |
| 3 | `Features/Gacha/GachaLogPage.xaml.cs` | 去 Windows API |
| 3 | `Features/Gacha/GachaStatsCard.xaml` | 删 `IsTextTrimmedChanged` + BOM |
| 3 | `Features/Gacha/GenshinBeyondGachaPage.xaml` | 页面缓存 |
| 3 | `Features/Gacha/ZZZGachaStatsCard.xaml` | 删 `IsTextTrimmedChanged` + BOM |
| 3 | `Features/GameInstall/InstallGameDialog.xaml` | 删 `IsTextTrimmedChanged`（该 code-behind 未编译） |
| 3 | `Features/GameRecord/StarRail/ApocalypticShadowPage.xaml` | 删 `IsTextTrimmedChanged` + BOM |
| 3 | `Features/GameRecord/StarRail/ForgottenHallPage.xaml` | 删 `IsTextTrimmedChanged` + BOM |
| 3 | `Features/GameRecord/StarRail/PureFictionPage.xaml` | 删 `IsTextTrimmedChanged` + BOM |
| 3 | `Features/SelfQuery/SelfQueryPage.xaml` | 页面缓存 |
| 4 | `Features/GameInstall/PreDownloadButton.xaml` | 图标码位 |
| 4 | `Features/GameRecord/DailyNote/DailyNoteButton.xaml` | 图标码位 |
| 4 | `Features/Setting/HotkeySetting.xaml` | 去 Windows 专有说明文案（D61 #1） |
| 4 | `Features/ViewHost/MainView.xaml` | 图标码位 |
| 4 | `Features/ViewHost/WelcomeWindow.xaml.cs` | XDG 目录 |
| 5 | `Features/GameInstall/PreDownloadButton.xaml.cs` | 图标码位 D49 + NaN 兜底 L-65 |
| 5 | `Features/Setting/FileManageSetting.xaml` | XDG 目录；图标码位 |
| 6 | `Features/GameInstall/PreDownloadDialog.xaml` | 手写 INPC 需显式 `Mode=OneWay` |
| 6 | `Features/GameLauncher/GameLauncherSettingDialog.xaml.cs` | 外链改 OpenUrl；XDG 目录 |
| 6 | `Features/GameLauncher/StartGameButton.xaml` | 图标码位 |
| 6 | `Features/Screenshot/ScreenshotPage.xaml.cs` | XDG 目录 |
| 6 | `Features/Setting/AboutSetting.xaml` | 资源替换 |
| 7 | `Features/Setting/ScreenshotSetting.xaml` | 文案 + 删 `IsTextTrimmedChanged`（待回加试验） |
| 8 | `Features/GameLauncher/GameNoticeService.cs` | wine/前缀；去 Windows API |
| 9 | `Features/Setting/GamepadControlSetting.xaml` | M4 占位（D61 #9） |
| 10 | `Features/GameSelector/GameSelector.xaml` | 无横向滚动 → 按宽均分（D56/L-691） |
| 10 | `Features/Setting/DownloadSetting.xaml.cs` | 外链改 OpenUrl |
| 10 | `Features/Setting/FileManageSetting.xaml.cs` | 7z→zip（已改为单文件 `CreateArchive`） |
| 13 | `AppConfig.ServiceProvider.cs` | 服务注册换 Linux 实现（D21）+ 品牌名 |
| 15 | `Features/GameLauncher/StartGameButton.xaml.cs` | NaN 兜底（已统一 `total > 0`） |
| 15 | `Features/GameRecord/GameRecordPage.xaml.cs` | 未登录空态（D61 #7） |
| 17 | `Features/GameAccount/GameAccountService.cs` | wine/前缀；去 Windows API |
| 19 | `Features/GameSelector/GameBizDisplay.cs` | 卡片尺寸按宽度均分（D56） |
| 19 | `Features/Setting/SettingPage.xaml` | 图标码位；页面缓存；字体族；实底/外观 |
| 20 | `Features/Background/BackgroundFileItem.cs` | 视频首帧缩略图（L-98） |
| 20 | `Features/Database/DatabaseService.cs` | 7z→zip + `CreateArchive` |
| 20 | `Features/GameRecord/GameRecordPage.xaml` | 图标码位；页面缓存；实底/外观 |
| 22 | `Features/Setting/GeneralSetting.xaml` | 删 Windows 系统设置段（D61） |
| 29 | `Features/GameRecord/LoginPage.xaml` | 登录助手入口（D29/D32） |
| 29 | `Features/GameRecord/StarRail/SimulatedUniversePage.xaml` | `ItemsRepeater`→`ItemsControl`（L-23） |
| 30 | `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml` | 图标码位；字体族 |
| 31 | `Features/Background/BackgroundViewWindow.xaml.cs` | 去 Windows API |
| 33 | `Features/GameLauncher/GameLauncherPage.xaml` | 页面缓存 |
| 35 | `Features/Background/BackgroundViewPage.xaml` | `GridView`→`ListView`（L-48/D55） |
| 35 | `Features/GameLauncher/GameBannerAndPost.xaml` | 外链改 OpenUrl |
| 36 | `Features/ViewHost/SystemTrayWindow.xaml.cs` | 去 Windows API |
| 40 | `Features/Setting/SettingPage.xaml.cs` | 初始页解析（D63） |
| 47 | `AppConfig.Common.cs` | XDG 目录；去 Windows API |
| 52 | `Features/GameLauncher/GameLauncherService.cs` | wine/前缀 |
| 56 | `Features/Setting/AdvancedSetting.xaml` | 图标码位 |
| 58 | `Features/Background/BackgroundService.cs` | XDG 目录 |
| 93 | `Features/GameSetting/GameSettingPage.xaml` | 图标码位；wine/前缀；页面缓存；实底/外观 |
| 99 | `Features/GameSelector/GameSelector.xaml.cs` | 去 Windows API |
| 102 | `Features/HoYoPlay/HoYoPlayService.cs` | 去 Windows API |
| 103 | `Features/GameSetting/GameSettingService.cs` | wine/前缀；去 Windows API |
| 115 | `Features/Setting/AdvancedSetting.xaml.cs` | 外链改 OpenUrl；字体族 |
| 121 | `Features/Background/BackgroundViewPage.xaml.cs` | 外链改 OpenUrl；XDG 目录 |
| 157 | `Features/GameLauncher/GameBannerAndPost.xaml.cs` | 外链改 OpenUrl；去 Windows API；页面缓存 |
| 162 | `AppConfig.Configuration.cs` | XDG 目录；去 Windows API |
| 183 | `Features/PlayTime/PlayTimeRecordService.cs` | wine/前缀 |
| 213 | `Features/ViewHost/MainView.xaml.cs` | XDG 目录 |
| 256 | `Features/Screenshot/ScreenshotPage.xaml` | 图标码位；页面缓存 |
| 259 | `Features/ViewHost/MainWindow.xaml.cs` | 去 Windows API |
| 312 | `App.xaml.cs` | XDG 目录 |
| 372 | `Features/GameLauncher/GameLauncherPage.xaml.cs` | 外链改 OpenUrl；wine/前缀；字体族 |
| 451 | `Features/GameSetting/GameSettingPage.xaml.cs` | wine/前缀；去 Windows API |
| 1035 | `Features/GameLauncher/GameLauncherSettingDialog.xaml` | 图标码位；实底/外观 |


---

## 26 个文件复核结论（2026-11-14）

方法：`diff -u src/Starward/<路径> src/Lunarward/<路径>` 取真实差异，再核对 `src/Lunarward/Lunarward.csproj` 的 `<Compile Remove>`，按 **A 必要 / B 可接受 / C 可疑 / D 死文件** 分类；每一条都要能指到具体行。三组并行复核（每组 8–9 个文件），全程只读。

分类结果：**A×19、B×4、C×3**，死文件 0 个。

- 26 个文件本身**没有一个**被 `<Compile Remove>` 排除；被排除的都是同目录 code-behind（`Lunarward.csproj:88/89/92/112`），对应 XAML 仍参与编译。
- 因此「改上游的 `.xaml.cs` 却不生效」这件事要按 D 处理：`PreDownloadDialog.xaml.cs`、`ScreenshotSetting.xaml.cs`、`InstallGameDialog.xaml.cs`、`GamepadController.cs`、`GameInstallService.cs` 这 5 个上游实现**已是死代码**，能力由对应的 `*.Linux.cs` 提供。

### 复核后当场修掉的 6 处

| 文件 | 问题 | 处理 |
|---|---|---|
| `Features/Setting/FileManageSetting.xaml.cs:204-210` | 手动备份用 `ZipFile.CreateFromDirectory(数据库备份目录)`，把历次 `.zip` 一起套进去（自我膨胀、`.db` 多一层） | 改用 `DatabaseService.CreateArchive(archive, file)` 单文件压缩（`CreateArchive` 由 `private` 改 `internal`） |
| `Features/GameLauncher/StartGameButton.xaml.cs:275/280` | `progress > 0` 吞掉 `0.0%` 那一帧，与 `PreDownloadButton.xaml.cs:185` 口径不一致 | 统一为 `total > 0` |
| `Features/Setting/GeneralSetting.xaml:62` | 整段 Windows 设置被删除后留下悬空的 `MenuFlyoutSeparator`（多一条分隔线 + 底部 20 DIP 空白） | 删掉该行 |
| `Features/GameSelector/GameSelector.xaml:109` | 新增的 `x:Name="ItemsControl_GameBizs"` 全仓无人使用 | 去掉名字 |
| `Helpers/PointerCursor.cs:7` | 复核建议回退 `partial` | **复核结论有误，已保留**：去掉后编译报 `CS0260 存在此类型的其他分部声明`，`partial` 是必需的（具体来源未查明，见 L-130） |
| `Features/Gacha/ZZZGachaService.cs:550` | `export_app = "Starward"` 品牌名 | 保留（只写不读，改品牌反而破坏与上游数据的互认） |

### 仍需决策的 2 项

1. **`IsTextTrimmedChanged` 能否回加**：Uno 6.7.359 的程序集里**已有** `add_IsTextTrimmedChanged` / `IsTextTrimmedChangedEventArgs` / `get_IsTextTrimmed`（`strings` 8 处命中），L-24 当年「Uno 没有这个事件」的阻塞条件可能已消解。涉及 `ScreenshotSetting.xaml:61`、`GachaStatsCard.xaml:59`、`ZZZGachaStatsCard.xaml:59`、`InstallGameDialog.xaml:55`、三个 StarRail 战绩页（`:206/:203/:204`）。回加能恢复「长文案被截断时两行回流」的自适应；但 Skia 后端是否真的触发该事件需要实测。
2. **硬编码中文文案的 i18n 欠账**：`HotkeySetting.xaml:22`、`GamepadControlSetting.xaml:22-27`、`GameRecordPage.xaml:527` 三处按 D61 用了中文字面量，等统一入 `Lang.resx`。
