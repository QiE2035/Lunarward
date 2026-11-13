# 上游偏差分类（自动生成 2026-11-13）

来源：`scripts/list-deviations.sh` 的同名差异文件 + `diff` 关键词粗分类。

共 81 个文件。其中 55 个能被 diff 关键词直接归类；剩下 26 个是「小改动」集合（≤40 行），已抽样核对 6 个，均为有意修改（`ExportApp` 品牌名、`lunarward.png` 资源替换、去掉 WinUI 专有的 `IsTextTrimmedChanged` / `XboxFluent2` 依赖、BOM），其余待逐个复核（`18-upstream-deviations.md` 有复核流程）。

| 变更行 | 文件 | 分类 |
|---:|---|---|
| 2 | `Features/CloudGame/CloudGameButton.xaml.cs` | 外链改 OpenUrl |
| 2 | `Features/Codec/ImageThumbnail.cs` | XDG 目录 |
| 2 | `Features/Gacha/GachaLogExportFile.cs` | 品牌名 |
| 2 | `Features/Gacha/UIGF/UIGF3File.cs` | 品牌名 |
| 2 | `Features/Gacha/UIGF/UIGF4File.cs` | 品牌名 |
| 2 | `Features/Gacha/UIGF/UIGFGachaService.cs` | 去 Windows API |
| 2 | `Features/Gacha/ZZZGachaService.cs` | **待人工分类** |
| 2 | `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs` | 外链改 OpenUrl |
| 2 | `Features/Screenshot/ImageViewWindow.xaml.cs` | 外链改 OpenUrl |
| 2 | `Features/Screenshot/ScreenshotFolderManageDialog.xaml` | 图标码位 |
| 2 | `Features/UrlProtocol/TestUrlProtocolWindow.xaml` | 资源替换 |
| 2 | `Features/UrlProtocol/UrlProtocolService.cs` | 去 Windows API |
| 2 | `Helpers/PointerCursor.cs` | **待人工分类** |
| 2 | `Program.cs` | XDG 目录 |
| 3 | `Features/Gacha/GachaLogPage.xaml` | 页面缓存 |
| 3 | `Features/Gacha/GachaLogPage.xaml.cs` | 去 Windows API |
| 3 | `Features/Gacha/GachaStatsCard.xaml` | **待人工分类** |
| 3 | `Features/Gacha/GenshinBeyondGachaPage.xaml` | 页面缓存 |
| 3 | `Features/Gacha/ZZZGachaStatsCard.xaml` | **待人工分类** |
| 3 | `Features/GameInstall/InstallGameDialog.xaml` | **待人工分类** |
| 3 | `Features/GameRecord/StarRail/ApocalypticShadowPage.xaml` | **待人工分类** |
| 3 | `Features/GameRecord/StarRail/ForgottenHallPage.xaml` | **待人工分类** |
| 3 | `Features/GameRecord/StarRail/PureFictionPage.xaml` | **待人工分类** |
| 3 | `Features/SelfQuery/SelfQueryPage.xaml` | 页面缓存 |
| 4 | `Features/GameInstall/PreDownloadButton.xaml` | 图标码位 |
| 4 | `Features/GameRecord/DailyNote/DailyNoteButton.xaml` | 图标码位 |
| 4 | `Features/Setting/HotkeySetting.xaml` | **待人工分类** |
| 4 | `Features/ViewHost/MainView.xaml` | 图标码位 |
| 4 | `Features/ViewHost/WelcomeWindow.xaml.cs` | XDG 目录 |
| 5 | `Features/GameInstall/PreDownloadButton.xaml.cs` | **待人工分类** |
| 5 | `Features/Setting/FileManageSetting.xaml` | XDG 目录；图标码位 |
| 6 | `Features/GameInstall/PreDownloadDialog.xaml` | **待人工分类** |
| 6 | `Features/GameLauncher/GameLauncherSettingDialog.xaml.cs` | 外链改 OpenUrl；XDG 目录 |
| 6 | `Features/GameLauncher/StartGameButton.xaml` | 图标码位 |
| 6 | `Features/Screenshot/ScreenshotPage.xaml.cs` | XDG 目录 |
| 6 | `Features/Setting/AboutSetting.xaml` | 资源替换 |
| 7 | `Features/Setting/ScreenshotSetting.xaml` | **待人工分类** |
| 8 | `Features/GameLauncher/GameNoticeService.cs` | wine/前缀；去 Windows API |
| 9 | `Features/Setting/GamepadControlSetting.xaml` | **待人工分类** |
| 10 | `Features/GameSelector/GameSelector.xaml` | **待人工分类** |
| 10 | `Features/Setting/DownloadSetting.xaml.cs` | 外链改 OpenUrl |
| 10 | `Features/Setting/FileManageSetting.xaml.cs` | **待人工分类** |
| 13 | `AppConfig.ServiceProvider.cs` | **待人工分类** |
| 15 | `Features/GameLauncher/StartGameButton.xaml.cs` | **待人工分类** |
| 15 | `Features/GameRecord/GameRecordPage.xaml.cs` | **待人工分类** |
| 17 | `Features/GameAccount/GameAccountService.cs` | wine/前缀；去 Windows API |
| 19 | `Features/GameSelector/GameBizDisplay.cs` | **待人工分类** |
| 19 | `Features/Setting/SettingPage.xaml` | 图标码位；页面缓存；字体族；实底/外观 |
| 20 | `Features/Background/BackgroundFileItem.cs` | **待人工分类** |
| 20 | `Features/Database/DatabaseService.cs` | **待人工分类** |
| 20 | `Features/GameRecord/GameRecordPage.xaml` | 图标码位；页面缓存；实底/外观 |
| 22 | `Features/Setting/GeneralSetting.xaml` | **待人工分类** |
| 29 | `Features/GameRecord/LoginPage.xaml` | **待人工分类** |
| 29 | `Features/GameRecord/StarRail/SimulatedUniversePage.xaml` | **待人工分类** |
| 30 | `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml` | 图标码位；字体族 |
| 31 | `Features/Background/BackgroundViewWindow.xaml.cs` | 去 Windows API |
| 33 | `Features/GameLauncher/GameLauncherPage.xaml` | 页面缓存 |
| 35 | `Features/Background/BackgroundViewPage.xaml` | **待人工分类** |
| 35 | `Features/GameLauncher/GameBannerAndPost.xaml` | 外链改 OpenUrl |
| 36 | `Features/ViewHost/SystemTrayWindow.xaml.cs` | 去 Windows API |
| 40 | `Features/Setting/SettingPage.xaml.cs` | **待人工分类** |
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
