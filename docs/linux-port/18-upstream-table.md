# 上游同名差异文件表（脚本生成）

> 本表由 `uv run --no-project python scripts/gen-upstream-table.py --write` 生成，不要手改。
> 来源：`bash scripts/list-deviations.sh` 的同名差异文件清单（**74 个**）；「变更行」= 两侧内容做 `difflib.unified_diff` 后 `+`/`-` 行数之和（去掉文件头），增删都计入，整段重排/挪位会让数字偏大，只作量级参考。
> 「分类」＝该文件在 `25-deviation-register.md` §3 里的主导类别（取前两位）；§3 未收录说明差异只是格式或轻微，标 `仅格式或轻微`。逐点判定与必要性见 `25`，偏差原则见 `18-upstream-deviations.md`。
> 实测：50 个 ≤40 行，14 个 ≥174 行。

| 变更行 | 文件 | 分类 |
|---:|---|---|
| 540 | `Features/Passport/WindowsDeviceFpService.cs` | linux |
| 503 | `Features/GameSetting/GameSettingPage.xaml.cs` | linux / engine |
| 413 | `App.xaml.cs` | linux / eng |
| 403 | `Features/GameInstall/GameInstallService.cs` | api / eng |
| 395 | `Features/GameLauncher/GameLauncherPage.xaml.cs` | linux / cosmetic |
| 384 | `Features/GameSetting/GameSettingService.cs` | linux / eng |
| 309 | `Features/ViewHost/MainWindow.xaml.cs` | api / linux |
| 263 | `Features/Screenshot/ScreenshotPage.xaml` | cut / cosmetic |
| 261 | `Features/ViewHost/MainView.xaml.cs` | linux / cut |
| 257 | `AppConfig.Configuration.cs` | linux / api |
| 256 | `Features/PlayTime/PlayTimeRecordService.cs` | linux / api |
| 224 | `Features/GameRecord/GameRecordPage.xaml.cs` | eng / cosmetic |
| 206 | `Features/GameLauncher/GameBannerAndPost.xaml.cs` | engine / linux |
| 184 | `Features/GameLauncher/GameLauncherService.cs` | eng / api |
| 157 | `Features/Background/BackgroundViewPage.xaml.cs` | api / engine |
| 129 | `Features/Background/BackgroundService.cs` | eng / linux |
| 123 | `Features/Setting/AdvancedSetting.xaml.cs` | linux / api |
| 93 | `Features/GameSetting/GameSettingPage.xaml` | linux / cosmetic |
| 83 | `Features/GameAccount/GameAccountService.cs` | linux / api |
| 69 | `Features/Setting/SettingPage.xaml.cs` | linux / bugfix |
| 61 | `Features/Database/DatabaseService.cs` | api / cosmetic |
| 52 | `Features/Setting/FileManageSetting.xaml.cs` | api / eng |
| 52 | `Features/Setting/AdvancedSetting.xaml` | linux / engine |
| 42 | `Features/ViewHost/SystemTrayWindow.xaml.cs` | api / eng |
| 40 | `AppConfig.Common.cs` | linux / eng |
| 36 | `Features/Gacha/GachaLogPage.xaml.cs` | eng / api |
| 35 | `Features/GameLauncher/GameBannerAndPost.xaml` | engine / api |
| 35 | `Features/Background/BackgroundViewPage.xaml` | engine |
| 31 | `Features/Background/BackgroundViewWindow.xaml.cs` | linux |
| 30 | `Features/GameRecord/ZZZ/ShiyuDefensePage.xaml` | engine |
| 29 | `Features/PageBase.cs` | engine |
| 27 | `Features/GameRecord/StarRail/SimulatedUniversePage.xaml` | engine |
| 26 | `Features/GameAccount/GameAccountSwitcher.xaml.cs` | linux / bugfix |
| 25 | `Features/GameLauncher/GameLauncherPage.xaml` | eng / linux |
| 25 | `AppConfig.ServiceProvider.cs` | linux / cosmetic |
| 23 | `Features/GameLauncher/GameNoticeService.cs` | api / eng |
| 22 | `Features/Setting/GeneralSetting.xaml` | cut / cosmetic |
| 22 | `Features/GameRecord/GameRecordPage.xaml` | cosmetic / eng |
| 20 | `Features/Background/BackgroundFileItem.cs` | linux / engine |
| 19 | `Features/Passport/HypLoginWindow.xaml.cs` | 仅格式或轻微 |
| 18 | `Features/Setting/DownloadSetting.xaml.cs` | cut / eng |
| 17 | `Features/Setting/GeneralSetting.xaml.cs` | cut / eng |
| 16 | `Features/ViewHost/MainView.xaml` | linux / cosmetic |
| 14 | `Features/Setting/SettingPage.xaml` | engine / eng |
| 12 | `Features/Setting/ScreenshotSetting.xaml` | cut / cosmetic |
| 12 | `Features/PlayTime/PlayTimeButton.xaml.cs` | linux |
| 10 | `App.xaml` | engine |
| 9 | `Features/Setting/GamepadControlSetting.xaml` | cut / engine |
| 7 | `Features/ViewHost/WelcomeWindow.xaml.cs` | cosmetic / eng |
| 6 | `Features/Setting/AboutSetting.xaml` | cosmetic |
| 6 | `Features/Screenshot/ScreenshotPage.xaml.cs` | linux / eng |
| 4 | `Features/Setting/HotkeySetting.xaml` | api / cosmetic |
| 4 | `Features/SelfQuery/SelfQueryPage.xaml.cs` | eng |
| 4 | `Features/GameRecord/DailyNote/DailyNoteButton.xaml` | engine |
| 4 | `Features/GameInstall/PreDownloadButton.xaml` | engine |
| 4 | `Features/GameInstall/InstallGameDialog.xaml` | engine |
| 3 | `Program.cs` | cut / eng |
| 3 | `Helpers/PointerCursor.cs` | cosmetic |
| 3 | `Features/Update/UpdateWindow.xaml` | 仅格式或轻微 |
| 3 | `Features/SelfQuery/SelfQueryPage.xaml` | eng |
| 3 | `Features/GameInstall/PreDownloadButton.xaml.cs` | engine |
| 3 | `Features/Gacha/GenshinBeyondGachaPage.xaml` | eng |
| 3 | `Features/Gacha/GachaLogPage.xaml` | eng |
| 2 | `Features/ViewHost/SystemTrayWindow.xaml` | cosmetic |
| 2 | `Features/UrlProtocol/UrlProtocolService.cs` | 仅格式或轻微 |
| 2 | `Features/Setting/FileManageSetting.xaml` | cut / engine |
| 2 | `Features/Screenshot/ScreenshotFolderManageDialog.xaml` | cosmetic / eng |
| 2 | `Features/Screenshot/ScreenCaptureService.cs` | eng |
| 2 | `Features/Gacha/ZZZGachaToolbox/ZZZGachaInfoWindow.xaml.cs` | api |
| 2 | `Features/Gacha/UIGF/UIGFGachaService.cs` | eng |
| 2 | `Features/Codec/ImageThumbnail.cs` | cut / api |
| 2 | `Features/CloudGame/CloudGameButton.xaml.cs` | api |
| 0 | `Features/PlayTime/PlayTimeStatsDialog.xaml.cs` | 仅格式或轻微 |
| 0 | `Controls/CachedImage.cs` | 仅格式或轻微 |
