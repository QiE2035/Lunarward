# 上游同名差异文件表（快照 2026-11-25，上游 0.19.0）

最后核对：2026-11-25（上游基线 `48b0cd90`，merge-base `747ed10b`）。

> **本表由脚本生成，不再人工分类。** 来源：`bash scripts/list-deviations.sh` 的同名差异文件清单（**80 个**）；「变更行」= `diff -u src/Starward/<路径> src/Lunarward/<路径> | grep -c '^[+-]'` 再减 2（去掉 `+++`/`---` 文件头），增删都计入，整段重排/挪位会让数字偏大，只作量级参考。
> 「分类」＝该文件在 `25-deviation-register.md` §3 里的主导类别（取前两位，`linux 专属实现`/`api 平台 API`/`cut 裁剪`/`engine 引擎能力`/`cosmetic 外观文案`/`eng 工程性`/`bugfix 修上游缺陷`）；§3 未收录的文件说明差异只是格式或轻微，标 `仅格式或轻微`。逐点判定与必要性见 `25`，偏差原则见 `18-upstream-deviations.md`。
> 2026-11-25 实测：80 个文件里 57 个 ≤40 行，13 个 ≥174 行。上一版（2026-11-24）表内列了 88 个文件；本次降到 80 个，是上游 0.19.0 合并的结果——部分文件上游自己重写后与移植侧逐字节相同（如 `GameLauncherSettingDialog.xaml/.cs`、`StartGameButton.xaml/.cs`），部分被上游整份删除（`GameSelector`、`LoginPage`）。

| 变更行 | 文件 | 分类 |
|---:|---|---|
| 2 | `Controls/CachedImage.cs` | 仅格式或轻微 |
| 2 | `Features/CloudGame/CloudGameButton.xaml.cs` | api |
| 2 | `Features/Codec/ImageThumbnail.cs` | cut / eng |
| 2 | `Features/Gacha/GachaLogExportFile.cs` | cosmetic |
| 2 | `Features/Gacha/UIGF/UIGF3File.cs` | cosmetic |
| 2 | `Features/Gacha/UIGF/UIGF4File.cs` | cosmetic |
| 2 | `Features/Gacha/UIGF/UIGFGachaService.cs` | eng |
| 2 | `Features/Gacha/ZZZGachaService.cs` | cosmetic |
| 2 | `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs` | api |
| 2 | `Features/PlayTime/PlayTimeStatsDialog.xaml.cs` | 仅格式或轻微 |
| 2 | `Features/Screenshot/ImageViewWindow.xaml.cs` | api / eng |
| 2 | `Features/Screenshot/ScreenCaptureService.cs` | eng |
| 2 | `Features/Screenshot/ScreenshotFolderManageDialog.xaml` | cosmetic / eng |
| 2 | `Features/UrlProtocol/TestUrlProtocolWindow.xaml` | cosmetic / eng |
| 2 | `Features/UrlProtocol/UrlProtocolService.cs` | 仅格式或轻微 |
| 2 | `Features/ViewHost/SystemTrayWindow.xaml` | cosmetic |
| 2 | `Program.cs` | eng / cut |
| 3 | `Features/Gacha/GachaLogPage.xaml` | eng |
| 3 | `Features/SelfQuery/SelfQueryPage.xaml` | eng |
| 3 | `Features/Setting/FileManageSetting.xaml` | cut / engine |
| 3 | `Features/Setting/GeneralSetting.xaml.cs` | eng / cut |
| 3 | `Features/Update/UpdateWindow.xaml` | 仅格式或轻微 |
| 3 | `Helpers/PointerCursor.cs` | cosmetic |
| 4 | `Features/GameInstall/InstallGameDialog.xaml` | engine |
| 4 | `Features/GameInstall/PreDownloadButton.xaml` | engine |
| 4 | `Features/GameRecord/DailyNote/DailyNoteButton.xaml` | engine |
| 4 | `Features/SelfQuery/SelfQueryPage.xaml.cs` | eng |
| 4 | `Features/Setting/HotkeySetting.xaml` | cosmetic / api |
| 5 | `Features/Gacha/GenshinBeyondGachaPage.xaml` | eng |
| 5 | `Features/GameInstall/PreDownloadButton.xaml.cs` | engine |
| 6 | `Features/Screenshot/ScreenshotPage.xaml.cs` | linux / eng |
| 6 | `Features/Setting/AboutSetting.xaml` | cosmetic |
| 7 | `Features/Passport/HypLoginWindow.xaml.cs` | 仅格式或轻微 |
| 7 | `Features/ViewHost/WelcomeWindow.xaml.cs` | eng / cosmetic |
| 8 | `Features/Setting/ScreenshotSetting.xaml` | cosmetic / eng |
| 9 | `Features/Setting/GamepadControlSetting.xaml` | cut / engine |
| 10 | `App.xaml` | engine |
| 12 | `Features/PlayTime/PlayTimeButton.xaml.cs` | linux |
| 15 | `Features/Setting/DownloadSetting.xaml.cs` | cut / eng |
| 16 | `Features/Setting/SettingPage.xaml` | engine / eng |
| 16 | `Features/ViewHost/MainView.xaml` | linux / cosmetic |
| 18 | `Features/GameAccount/GameAccountSwitcher.xaml.cs` | linux / bugfix |
| 20 | `Features/Background/BackgroundFileItem.cs` | eng / linux |
| 23 | `AppConfig.ServiceProvider.cs` | linux / cosmetic |
| 23 | `Features/PageBase.cs` | engine |
| 24 | `Features/GameRecord/GameRecordPage.xaml` | eng / cosmetic |
| 24 | `Features/Setting/GeneralSetting.xaml` | cosmetic / cut |
| 25 | `Features/Setting/FileManageSetting.xaml.cs` | api / eng |
| 27 | `Features/GameRecord/StarRail/SimulatedUniversePage.xaml` | engine |
| 28 | `Features/GameLauncher/GameNoticeService.cs` | api / eng |
| 30 | `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml` | engine |
| 33 | `Features/Background/BackgroundViewWindow.xaml.cs` | linux |
| 34 | `Features/Gacha/GachaLogPage.xaml.cs` | eng / api |
| 35 | `Features/Background/BackgroundViewPage.xaml` | engine |
| 35 | `Features/GameLauncher/GameBannerAndPost.xaml` | engine / eng |
| 40 | `AppConfig.Common.cs` | linux / eng |
| 40 | `Features/ViewHost/SystemTrayWindow.xaml.cs` | api / eng |
| 45 | `Features/Database/DatabaseService.cs` | api / cosmetic |
| 53 | `Features/Setting/AdvancedSetting.xaml` | linux / cosmetic |
| 69 | `Features/Setting/SettingPage.xaml.cs` | linux / bugfix |
| 73 | `Features/GameAccount/GameAccountService.cs` | linux / api |
| 75 | `Features/Background/BackgroundService.cs` | eng / linux |
| 93 | `Features/GameSetting/GameSettingPage.xaml` | linux / cosmetic |
| 111 | `Features/GameLauncher/GameLauncherPage.xaml` | eng / linux |
| 125 | `Features/Setting/AdvancedSetting.xaml.cs` | linux / api |
| 157 | `Features/Background/BackgroundViewPage.xaml.cs` | api / engine |
| 159 | `Features/GameLauncher/GameLauncherService.cs` | eng / api |
| 177 | `Features/GameLauncher/GameBannerAndPost.xaml.cs` | engine / linux |
| 197 | `AppConfig.Configuration.cs` | linux / eng |
| 212 | `Features/GameRecord/GameRecordPage.xaml.cs` | eng / cosmetic |
| 230 | `Features/PlayTime/PlayTimeRecordService.cs` | linux / api |
| 255 | `Features/ViewHost/MainView.xaml.cs` | linux / eng |
| 258 | `Features/Screenshot/ScreenshotPage.xaml` | eng / cut |
| 285 | `Features/GameInstall/GameInstallService.cs` | api / eng |
| 288 | `Features/GameSetting/GameSettingService.cs` | linux / eng |
| 291 | `Features/ViewHost/MainWindow.xaml.cs` | api / linux |
| 387 | `Features/Passport/WindowsDeviceFpService.cs` | linux |
| 413 | `App.xaml.cs` | eng / linux |
| 495 | `Features/GameSetting/GameSettingPage.xaml.cs` | linux / eng |
| 669 | `Features/GameLauncher/GameLauncherPage.xaml.cs` | linux / eng |
