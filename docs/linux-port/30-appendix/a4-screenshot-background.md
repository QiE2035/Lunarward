# D 组：截图/背景/控件（12 文件）

审计方式：只读。逐条读 `/tmp/lw-audit2/diffs/*.u0`（每文件 `wc -l` 记变更行数），逐行读两侧同名文件，再读同目录 `*.Linux.cs` 与 `src/Lunarward/Lunarward.csproj` 判定「谁在真正编译」。行号全部用 `nl -ba` 复核过。

编译总表（`src/Lunarward/Lunarward.csproj`）：

| 文件 | 编译状态 |
| --- | --- |
| Features/Screenshot/ScreenshotPage.xaml | 参与编译（唯一在编的截图页面 XAML） |
| Features/Screenshot/ScreenshotPage.xaml.cs | `<Compile Remove>`（csproj:125）→ 死代码；由 `ScreenshotPage.Linux.cs`（616 行，partial）+ `ScreenshotPage.LinuxAuto.cs`（12 行空实现）提供 |
| Features/Screenshot/ScreenCaptureService.cs | `<Compile Remove>`（csproj:123）→ 死代码；由 `ScreenCapture.Linux.cs`（`ScreenCaptureLinux`，240 行）提供 |
| Features/Screenshot/ScreenshotFolderManageDialog.xaml | 参与编译 |
| Features/Codec/ImageThumbnail.cs | `<Compile Remove>`（csproj:106）→ 死代码，全仓无存活引用 |
| Controls/CachedImage.cs | `<Compile Remove>`（csproj:98）→ 死代码；由 `Controls/CachedImage.Linux.cs`（328 行）提供 |
| Features/Background/BackgroundService.cs | 参与编译 |
| Features/Background/BackgroundViewPage.xaml | 参与编译 |
| Features/Background/BackgroundViewPage.xaml.cs | 参与编译 |
| Features/Background/BackgroundViewWindow.xaml.cs | 参与编译 |
| Features/Background/BackgroundFileItem.cs | 参与编译 |
| Helpers/PointerCursor.cs | 参与编译 |

---

## Features/Screenshot/ScreenshotPage.xaml

- 变更行数：264（删除 206 / 新增 54，4 个 hunk）
- 编译状态：参与编译

### 差异清单

- **[移植 第 1-2 行 · `sf:PageBase` 根元素]** 移植新增｜功能损失：否｜建议：保留｜上游：无 `NavigationCacheMode`（默认 Disabled）｜移植：`NavigationCacheMode="Required"`｜理由：页面被永久缓存后才能靠 `OnLoaded`（`ScreenshotPage.Linux.cs:192-197`）重新枚举 + 重挂 watcher 来补齐「缓存页面」语义（B16 注释）。代价见本文件结论第 2 条：因为没有 `OnUnloaded`，watcher 会常驻。
- **[移植 第 8 行 · `xmlns:tk`]** 移植新增｜功能损失：否｜建议：保留｜上游：无此命名空间｜移植：`xmlns:tk="using:CommunityToolkit.WinUI.Controls"`，供第 63 行 `tk:WrapPanel` 使用｜理由：`Uno` 下 `ItemsWrapGrid` 不可用后的替代面板来源；缩进与相邻行不一致（纯外观）。
- **[移植 第 51-53 行 · 大块替换前的注释]** 文案外观｜功能损失：否｜建议：保留但要更正｜上游：无｜移植：注释断言「Uno 6.7 下 GridView+GroupStyle+ItemsWrapGrid 不渲染条目（L-48）」｜理由：同组 `BackgroundViewPage.xaml:135` 在本轮仍用 `ListView`+`WrapPanel`，没有反证；但注释把「不渲染」写成了引擎定论，而 `BackgroundViewPage` 的 ListView 能渲染说明问题出在 `GridView+GroupStyle` 组合与 `ItemsWrapGrid`，建议把范围写清，避免下次误判整个 GridView 不可用。
- **[移植 第 54-58 行 · `ScrollViewer_Images`]** 平台替换｜功能损失：是（分组缺失）｜建议：保留｜上游：条目容器是 `GridView_Images`（上游 xaml:49）直接铺在页面 Grid 里，不需要外层 ScrollViewer｜移植：`ScrollViewer(Visibility="Collapsed")` 包住 ItemsControl，可见性由 code-behind 切换｜理由：`ItemsControl` 无虚拟化滚动，必须自包 ScrollViewer；但连带丢掉了上游按日期分组（`CollectionViewSource.IsSourceGrouped=true` + `GroupStyle` + `AreStickyGroupHeadersEnabled=True`），Linux 用户看到的是一整片扁平网格、没有「日期」分组头，滚到几百张时找不到「今天的」一批。
- **[移植 第 59-60 行 · `ItemsControl_Images` + `ItemsSource`]** 平台替换｜功能损失：是（点击/拖拽/多选语义全部改写）｜建议：保留（但需补拖出）｜上游：`GridView` + `IsItemClickEnabled/ItemClick` + `SelectionChanged` + `SelectionMode` + `CanDragItems/DragItemsStarting`｜移植：`ItemsControl`，条目自己接 `Tapped/RightTapped`｜理由：`ItemsControl` 没有 `ItemClick`（`ScreenshotPage.Linux.cs:543` 的 B07 注释已说明），只能靠条目根元素；但**上游的「把截图拖出到文件管理器」能力整体消失**——移植侧 XAML 与 code-behind 都没有 `DragItemsStarting`/`DataPackage`（`grep DragItems` 在 `ScreenshotPage.Linux.cs` 无命中）。触发：用户想把截图拖到聊天窗口，现在完全没反应。
- **[移植 第 61-65 行 · `tk:WrapPanel` 面板]** 平台替换｜功能损失：是（排版/尺寸）｜建议：保留｜上游：`ItemsWrapGrid ItemWidth=218 ItemHeight=128 Orientation=Horizontal`（上游 xaml:150-156）｜移植：`tk:WrapPanel HorizontalSpacing=12 VerticalSpacing=12`（无条目尺寸，尺寸写死在第 74 行 `Width="218"`）｜理由：`WrapPanel` 不提供 `ItemWidth/ItemHeight`；后果是列数固定按 218+12 推算，窗口缩放时列数会突然跳变（背景组的 `UpdateTileSize` 已在做自适应，截图页没做——两页行为不一致）。
- **[移植 第 66-97 行 · `ItemTemplate` 整体]** 平台替换｜功能损失：是（多条）｜建议：保留但有 3 处待补｜上游：`GridViewItem` 容器样式（`DefaultGridViewItemStyle` + `CornerRadius=4` + `Margin=0,0,12,12`）、`Grid` 上 `sh:PointerCursor.CursorShape="Hand"` 与 `ui:UIElementExtensions.ClipToBounds="True"`、`CachedImage 206x116` 带 `RenderTransform ScaleTransform` + 两个 `Storyboard`（悬停放大 1.15）、`Grid.ContextFlyout` 完整右键菜单、缩略图 + 文件名 | 移植：`Border`（Width=218、Padding=6、CornerRadius=6、`Tapped/RightTapped`、`CardBackgroundFillColorDefaultBrush`）> `StackPanel` > `Grid Height=116 CornerRadius=4`（内 `CachedImage 206x116 IsThumbnail=True UniformToFill` + 左上 `CheckBox`）> `TextBlock`（FontSize=12、MaxLines=1、CharacterEllipsis、`x:Bind Name`）｜理由明细见下面四条。补充实证：上游 `ScreenshotPage.xaml:104-105` 有 `sh:PointerCursor.CursorShape="Hand"` + `ui:UIElementExtensions.ClipToBounds="True"`，`216-247` 是两个 `Storyboard`（`ScaleX/ScaleY→1.15`）配 `mxi:ControlStoryboardAction`（`Microsoft.Xaml.Interactivity` 行为），移植侧 `xmlns:mxi` 还留在第 10 行但已无任何 `mxi:` 使用（无用命名空间）。
- **[移植 第 68-74 行 · 卡片 `Border`]** 移植新增｜功能损失：否｜建议：保留｜上游：无卡片容器（条目直接是 GridViewItem + 图）｜移植：218 宽卡片，`Padding=6`，圆角 6，`CornerRadius` 写在 Border 上但没有 `ClipToBounds`｜理由：观感比上游「裸图」更像 0.19.0 的卡片风；但 `Border.CornerRadius` 不会裁剪子元素（Uno 与 WinUI 一致），`CachedImage` 的 4 个直角会盖住圆角——视觉细节损失（圆角/遮罩要求项：**圆角声明了、实际不生效**）。对照 `BackgroundViewPage.xaml:160` 用了 `ui:UIElementExtensions.ClipToBounds="True"`，截图页漏了。
- **[移植 第 76-90 行 · `Grid` + `CachedImage` + `CheckBox`]** 移植新增｜功能损失：是（悬停放大/手型指针丢失）｜建议：保留 CheckBox，补回手型指针｜上游：条目根 `Grid` 有 `sh:PointerCursor.CursorShape="Hand"`，`CachedImage` 有 `ScaleTransform` + 悬停 `Storyboard` 放大到 1.15｜移植：无线，也无 `PointerCursor`｜理由：手型指针丢失后，Linux 用户把鼠标移到截图上是箭头，看不出「可点」（这是本组「指针」相关的唯一实证：截图页与背景页取值不一致——`BackgroundViewPage.xaml:159` 仍保留了 `PointerCursor.CursorShape="Hand"`）。悬停缩放属视觉细节，可接受。
- **[移植 第 91-94 行 · 文件名 `TextBlock`]** 移植新增｜功能损失：否｜建议：保留｜上游：无文件名（只有 `ToolTip`/右键菜单里的 `FileInfo`）｜移植：单行 12 号字、`CharacterEllipsis` 显示 `x:Bind Name`｜理由：`ItemsControl` 没有 `GridViewItem` 的 `ToolTip` 与分组头，文件名是唯一可辨识线索，属合理补偿。
- **[移植 第 110 行 · `TextBlock_SelectCount`]** 文案外观｜功能损失：否｜建议：保留｜上游：`Text="{x:Bind SelectCountText}"`（靠 `x:DefaultBindMode="OneWay"`，上游 xaml:14）｜移植：显式 `Mode=OneWay`｜理由：等价重复；同文件第 16 行已声明 `x:DefaultBindMode="OneWay"`，显式写法只是冗余。

### 本文件结论

1. 这一页的实际编译者是「本 XAML + `ScreenshotPage.Linux.cs`」；XAML 里被替换掉的 `GridView` 交互面（分组、`ItemClick`、`SelectionChanged`、拖出）必须由 code-behind 补齐，目前**只补了 Tapped/RightTapped/CheckBox 多选**，拖出与日期分组是真实功能损失。
2. 圆角/遮罩只声明未生效（`Border` 与 `Grid` 的 `CornerRadius` 都没有配 `ClipToBounds`）；手型指针缺失。
3. 页面注释（第 51-53 行）把「GridView+GroupStyle+ItemsWrapGrid 不渲染」写成定论，但同项目 `BackgroundViewPage.xaml:135` 的 `ListView` 渲染正常，建议收窄表述。

---

## Features/Screenshot/ScreenshotPage.xaml.cs

- 变更行数：7（删除 2 / 新增 4，1 个 hunk）
- 编译状态：被 Compile Remove（csproj:125）→ **整文件不参与编译**，真实现是同目录 `ScreenshotPage.Linux.cs`

### 差异清单

- **[移植 第 222-225 行 · `Initialize()` 里的目录解析]** 平台替换｜功能损失：否（对本文件而言是死代码）｜建议：保留（作对照）｜上游：`string? folder = AppConfig.ScreenshotFolder; if (!Directory.Exists(folder)) { folder = Path.Join(AppConfig.UserDataFolder, "Screenshots"); }`（上游 xaml.cs:222-227）｜移植：`string? folder = ScreenCaptureLinux.DefaultFolder; if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))` + 两行 `Lunarward` 注释（`~/图片/Lunarward` 兜底）｜理由：这个改动本身方向正确（Linux 无 `AppConfig.ScreenshotFolder` 时要有兜底目录），但**它所在的文件根本没编译**；真实现走 `ScreenshotPage.Linux.cs:100-115` 的 `ScreenshotFolders()`。留着重写版会被后续同步误当成「已生效逻辑」。

### 本文件结论

1. 该文件唯一改动是死代码，且与 `ScreenshotPage.Linux.cs` 的逻辑已经分叉（真实现多了 `AppConfig.GetExternalScreenshotFolder` 外部目录，见 `ScreenshotPage.Linux.cs:103`）。
2. **连带确认的行为丢失（真实现里没有，上游有）**：上游 `GetGameScreenshotPathAsync()`（上游 xaml.cs:199-241）会把「游戏安装目录内的截图目录」也加进监控——`hk4e→"ScreenShot"`、`hkrpg→@"StarRail_Data\ScreenShots"`、`bh3/nap→"ScreenShot"`，并通过 `HoYoPlayService.GetGameConfigAsync` 取 `GameScreenshotDir`。移植侧 `ScreenshotFolders()` 只认「`ScreenCaptureLinux.DefaultFolder` + 用户手工加的外部目录」，**不认游戏内截图目录**。触发：用户在游戏里按 F12 出的官方截图不会出现在「截图」页，除非手工在「管理截图文件夹」里加一次（前提是他知道路径）。
3. 上游 `OnUnloaded`（释放 `_watchers`、清空字典与分组）在移植侧不存在；`ScreenshotPage.Linux.cs` 全文无 `OnUnloaded`/`Dispose`，`_watcher`（第 160、209-227 行）在页面被缓存后一直活着 → 离开页面后仍在为隐藏页面触发 `LoadImages()`。

---

## Features/Screenshot/ScreenCaptureService.cs

- 变更行数：3（删除 1 / 新增 1，1 个 hunk）
- 编译状态：被 Compile Remove（csproj:123）→ 死代码；真链路是 `Features/Screenshot/ScreenCapture.Linux.cs`（`static class ScreenCaptureLinux`）

### 差异清单

- **[移植 第 405 行 · `SaveAsSdrAsync()`]** 平台替换｜功能损失：否（死代码）｜建议：保留｜上游：`(_, outputMaxLuminance, _) = GameSettingService.GetGenshinHDRLuminance(runningGame.GameBiz);`｜移植：`... = await GameSettingService.GetGenshinHDRLuminanceAsync(runningGame.GameBiz).ConfigureAwait(false);`｜理由：同步→异步 API 改名/改签名（HDR→SDR 时的输出亮度）。整文件不编译，所以这行**在 Linux 上不产生任何效果**；而它代表的 HDR 链路在 `ScreenCapture.Linux.cs` 中整体不存在。

**截图链路逐项对账（本组重点，以上游 `ScreenCaptureService.cs` 为准）**

| 环节 | 上游（`src/Starward/Features/Screenshot/ScreenCaptureService.cs`） | 移植（`src/Lunarward/Features/Screenshot/ScreenCapture.Linux.cs`） | 结论 |
| --- | --- | --- | --- |
| 捕获方式 | 进程内 `Windows.Graphics.Capture` + Direct3D11 帧 + Win2D（`CaptureAndProceedImageAsync` 118-158） | 外部工具子进程，按顺序 `spectacle -b -n -f -o <file>` → `grim <file>` → `gnome-screenshot -f <file>`（第 65-70 行），每个 8 秒超时 + `Kill(entireProcessTree:true)`（194-226）；全失败才走 `Helpers.Portal.ScreenshotAsync(interactive:false)`（第 91 行） | 平台替换 |
| 「只抓游戏窗口」 | 有：`RunningGameService.GetLatestActiveGame()` 取最近活跃游戏 → 抓该 hwnd；`User32.IsIconic` 拒绝最小化窗口（71-93 行） | **不存在**：`CaptureAsync` 不带任何目标窗口参数，抓的是整个屏幕；文件头注释也写明「全屏捕获不做窗口白名单」 | 平台缺失（Linux 用户截到的是整屏，含副屏当前桌面、其它窗口；副屏/多显示器时内容与 Windows 不同） |
| 客户区裁剪 | `TryClipClient(hwnd, contentSize, out clipRect)`（440-472）：`GetClientRect` + `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` + `ClientToScreen` 去掉标题栏与边框 | **不存在** | 平台缺失（仅在全屏抓屏下不影响结果，见上） |
| 每游戏子目录 | `ScreenshotFolder/<ProcessName>/`，无配置时 `UserDataFolder/Screenshots/<ProcessName>/`（SaveImageAsync 296-308） | 单一目录 `ScreenCaptureLinux.DefaultFolder`（第 50 行），文件名前缀 `Lunarward_`（第 62 行） | 平台替换（多游戏截图混在一个目录） |
| 文件名/时间戳 | `<ProcessName>_yyyyMMdd_HHmmssff.<ext>` | `Lunarward_yyyyMMdd_HHmmssff.png`（第 62 行，注释解释是为了让 `ScreenshotItem.TryParseCreationTime` 认） | 平台替换（可接受） |
| HDR→SDR | 有：HDR 时 `pixelFormat=R16G16B16A16Float`；`HDR = maxCLL > SdrWhiteLevelInNits + 5`（128、152 行 `GetMaxCLL` 474-482 降采样求最大亮度）；`HDR && AutoConvertScreenshotToSDR` → `SaveAsSdrAsync`（398-408）走 `ImageSaver.SaveAsUhdrAsync` + `GetGenshinHDRLuminance(Async)` | **整条链不存在**：无 `MaxCLL`、无 SdrWhiteLevel、无 UHDR 编码；`Capture.Linux.cs` 只认一个 PNG 文件 | 平台缺失（Linux 无 GraphicsCapture 高精度帧；HDR 屏截出来是 SDR 后的成品，亮度裁剪由外部工具决定，且「自动另存 SDR」开关在 Linux 上永不触发） |
| 保存格式 | `(HDR, AppConfig.ScreenCaptureSavedFormat) switch { (_,1)=>"avif", (_,2)=>"jxl", (false,_)=>"png", (true,_)=>"avif" }`（309-314） | 捕获固定为 PNG；再由 `FinalizeCaptureAsync`（126-169）在 `ScreenCaptureSavedFormat` 为 1/2 时转 `.avif`/`.jxl`（`MagickCodec.TryEncode`，质量 2→100/1→92/0→75），失败保留 PNG | 平台替换（结果格式齐了；差别是 HDR 会强制 avif 的分支不存在、JPEG XL 依赖 Magick.NET） |
| 色彩管理/XMP | `EnableScreenshotColorManagement` + `BuildXMPMetadata(FrameTime)` + `ColorPrimaries` 写入（319-372） | **不存在** | 平台缺失（截图没有 XMP 元数据，色彩配置由外部工具决定） |
| 剪贴板 | `CopyToClipboardAsync(filePath)`（420-427）→ `ClipboardHelper.SetStorageItems(Copy, file)`；HDR 时再复制 SDR 版本 | `TryCopyToClipboardAsync`（173-191）：`AppConfig.AutoCopyScreenshotToClipboard` 且 `ClipboardHelper.SetImageFile(file)` 成功才复制（wl-copy/xclip），失败只记 warning | 平台替换（Linux 剪贴板只能放图片/路径；`ClipboardHelper.Linux.cs` 提供） |
| 成功/失败反馈 | `ScreenCaptureInfoWindow`：`CaptureStart`/`CaptureSuccess`（带缩略图与文件路径）/`CaptureError`（94-116 行） | 桌面通知 `DesktopNotification.Show("截图已保存", 文件名, "lunarward", 4000)`（第 81、112 行），热键路径 `notify:true`；`--screenshot` 用 `notify:false`（`Program.Linux.cs:213`） | 平台替换（无预览窗；移植侧连 `ScreenCaptureInfoWindow.xaml(.cs)` 文件都不存在） |
| 触发入口 | `ScreenCaptureService.Capture()`（热键/手柄） | `Helpers/HotkeyManager.Linux.cs:277` → `ScreenCaptureLinux.CaptureAsync()`；`Features/Setting/ScreenshotSetting.Linux.cs:177`（设置页测试）；`Program.Linux.cs:213/263`（CLI）；`Features/Tray/StatusNotifierTray.cs:295`（打开目录）。`GamepadControl/GamepadController.cs:616,647` 仍调 `ScreenCaptureService.Capture`，但该文件被 `<Compile Remove>`（csproj:131），手柄截图在本组不生效 | 平台替换 |

### 本文件结论

1. 该文件是**死代码**，唯一改动没有任何运行影响；把它当作「截图服务」来审计会全部看错。
2. 真实链路在 `ScreenCapture.Linux.cs`，与上游相比缺失的是：客户区判定、只抓游戏窗口、HDR/UHDR 与 SDR 另存、XMP/色彩管理、`ScreenCaptureInfoWindow` 预览；补齐的是：外部工具回退链 + portal 兜底、格式转换、剪贴板、桌面通知。
3. 移植侧 `Features/Screenshot/` 里 `ScreenCaptureInfoWindow`、`ImageViewWindow2`、`ImageBatchConvertWindow`、`ScreenshotPage2` 四组文件已被删除（上游存在），属「文件级删除」偏离，不在本次 12 文件 diff 内，但解释了上表多行「不存在」。

---

## Features/Screenshot/ScreenshotFolderManageDialog.xaml

- 变更行数：3（删除 1 / 新增 1，1 个 hunk）
- 编译状态：参与编译

### 差异清单

- **[移植 第 98-100 行 · `Button_BackupFolder` 内 `FontIcon.Glyph`]** 文案外观｜功能损失：否｜建议：保留｜上游：`Glyph="&#xF413;"`（上游 xaml:100）｜移植：`Glyph="&#xE74E;"`｜理由：`F413` 是 Segoe Fluent Icons 私有区码位，Uno 的 `Symbols`/随包字体不含该码位（L-88 已登记同类问题），保留上游会在按钮上出现豆腐块。**但**同一语义（`Lang.ImageViewWindow2_CopyAsJPG`）在 `ScreenshotPage.Linux.cs:337` 仍写 `"\uF413"`——同一图标两处取值不一致，右键菜单里的「复制为 JPG」还是豆腐块，属漏改。

### 本文件结论

改对了，但只改了这一处；`ScreenshotPage.Linux.cs:337` 需同步成 `E74E`（或另一个确实存在的码位）。

---

## Features/Codec/ImageThumbnail.cs

- 变更行数：3（删除 1 / 新增 1，1 个 hunk）
- 编译状态：被 Compile Remove（csproj:106）→ 死代码

### 差异清单

- **[移植 第 31 行 · `CacheFolder` 字段]** 平台替换｜功能损失：否（死代码）｜建议：删除｜上游：`private static readonly string CacheFolder = Path.Combine(AppConfig.CacheFolder, "thumb");`｜移植：`private static string CacheFolder => Path.Combine(AppConfig.CacheDirectory, "thumb");`｜理由：`AppConfig.CacheDirectory` 在 `src/Lunarward/AppConfig.Configuration.cs:37` 定义、`:132` 设为 `LinuxCacheHome/lunarward`，写法本身没问题；但整文件不编译，且**全仓已无存活引用**——引用它的三个文件全部同时被 `Compile Remove`（`Controls/CachedImage.cs:55,59`、`Features/Codec/ImageLoader.cs:349,354`、`Features/Setting/ScreenshotSetting.xaml.cs:433`）。400 行的缩略图缓存体系（`GetImageThumbnailAsync`/`GetVideoThumbnailAsync`/`ClearThumbnailCache`/AVIF、JXL 解码器探测）在 Linux 上完全不存在：`CachedImage.Linux.cs` 用 `SKCodec`（`DecodeScaledPng` 209-236）+ `VideoThumbnailer` 自己实现缩略图，`ImageLoader.Linux.cs:11` 提供 `GetImagePixelSizeAsync`。存活路径里没有任何「清理缩略图缓存」入口（上游设置页的「清除缩略图缓存」按钮在移植侧无效，属行为丢失，但触发面小）。

### 本文件结论

死代码。建议整文件删除或明确标注「未移植」，不要为了一个 `CacheFolder→CacheDirectory` 的适配让读者误以为缩略图缓存已工作；同时确认设置页「清理缩略图缓存」入口在移植侧是真的不存在（而不是点了没反应）。

---

## Controls/CachedImage.cs

- 变更行数：3（删除 1 / 新增 1，1 个 hunk）
- 编译状态：被 Compile Remove（csproj:98）→ 死代码；真实现 `Controls/CachedImage.Linux.cs`（328 行，`public sealed partial class CachedImage : Image`）

### 差异清单

- **[移植 第 1 行 · 文件头]** 文案外观｜功能损失：否｜建议：保留（可不管）｜上游：UTF-8 BOM（`﻿﻿using`）｜移植：无 BOM（`using`）｜理由：`diff -U0` 的唯一命中就是 BOM 差异，122 行内容与上游逐字节相同。它被 `Compile Remove` 后由 `CachedImage.Linux.cs` 顶替，所以这处差异纯粹是「保留对照副本」留下的字节差，没有运行影响。

### 本文件结论

1. 死代码，唯一差异是 BOM。真正要审计的是 `CachedImage.Linux.cs`，与上游 `CachedImage : ImageEx` 的差别如下（供本组「IsThumbnail 缩放解码」结论用）：
   - `IsThumbnail` **真被调用**：`CachedImage.Linux.cs:275` `if (IsThumbnail)` → 视频扩展名走 `VideoThumbnailer.EnsureAsync`（278-286），图片走 `DecodeScaledPng(path, DecodePixelWidth > 0 ? DecodePixelWidth : 400)`（288），即 `SKCodec.GetScaledDimensions` 按目标宽度直接解码，不解整张原图（209-236）。
   - `PngThumbnail`（84-91）与 `ClipToBounds`（54）是**只存不用**的占位属性；`Background`（22-29）只存不画（占位底色丢失）。
   - `CornerRadius`（43-73）走 `UpdateClip`，只做 `RectangleGeometry` 矩形裁剪，注释自认「Uno 的 RectangleGeometry 不支持圆角」→ 圆角在 `CachedImage` 层不生效（与截图页/背景页容器圆角不裁剪是同一个视觉缺口）。
2. `ImageExOpened/ImageExFailed`（320-327）仍会触发，`ImageOpened`（109）也在，事件面与上游兼容。

---

## Features/Background/BackgroundService.cs

- 变更行数：127（删除 32 / 新增 84，11 个 hunk）
- 编译状态：参与编译

### 差异清单

- **[移植 第 1 行 · 文件头]** 文案外观｜功能损失：否｜建议：保留｜上游：带 BOM 的 `﻿using Microsoft.Extensions.Logging;`｜移植：无 BOM｜理由：纯字节差。
- **[移植 第 15 行附近（删除上游 16-19 行）· using 段]** 平台替换｜功能损失：否｜建议：保留｜上游：`using Windows.Graphics.Imaging; using Windows.Media.Core; using Windows.Storage;`｜移植：删除｜理由：WinRT 媒体/存储栈在 Uno/Skia 下不存在，删掉是编译前提；对应能力在下面几条逐个替换。
- **[移植 第 203-210 行 · `GetFallbackBackgroundImage()`]** 平台替换｜功能损失：是（修复型）｜建议：保留｜上游：`Path.Combine(baseFolder, @"Assets\Image\UI_CutScene_1130320101A.png")`（反斜杠写死）｜移植：`Path.Combine(baseFolder, "Assets", "Image", "UI_CutScene_1130320101A.png")` + 2 行 `LUNARWARD-DEVIATION` 注释｜理由：Linux 下反斜杠不是分隔符，上游写法 `File.Exists` 恒 false → 兜底壁纸永远拿不到（壁纸空白）。移植写法正确，属必修的跨平台修正；**注意这是「上游 bug 在 Linux 上才暴露」**，不能当偏离删掉。
- **[移植 第 248-266 行 · `ChangeCustomBackgroundFileAsync(string? file)`]** 平台替换｜功能损失：是（校验强度）｜建议：保留（已是最优折中）｜上游：`ChangeCustomBackgroundFileAsync(StorageFile file)`，视频用 `MediaSource.CreateFromStorageFile(file).OpenAsync()` 真打不开就抛（注释标 0xC00D36C4），图片用 `BitmapDecoder.CreateAsync` 真解码（0x88982F8B）｜移植：收本地路径；空/NULL 早退（250-253）；`CheckBackgroundFileAvailableAsync`（258）后 `Path.GetDirectoryName(GetFullPath(file)) != GetFullPath(bg)` 才复制（259-264）｜理由：签名改成路径是必需的（Uno 无 `StorageFile` 写入路径），调用点已同步（`GameLauncherPage.xaml.cs:1295` 传 `file.Path`、`AppBackground.Linux.cs:1432`、`GameLauncherSettingDialog.xaml.cs:639`）。校验强度差异见下一条。
- **[移植 第 298-331 行 · `CheckBackgroundFileAvailableAsync(string)`]** 平台替换｜功能损失：是（弱校验，属已知取舍）｜建议：保留｜上游：`if (FileIsSupportedVideo(file.Name)) { using var source = MediaSource.CreateFromUri(new Uri(file)); await source.OpenAsync(); } else { using var fs = File.OpenRead(file); var decoder = await BitmapDecoder.CreateAsync(fs.AsRandomAccessStream()); }`（上游 307-315）｜移植：存在性检查（300-303）→ `Length <= 0` 抛（304-308，注释「B54：图片必须真的解得开才允许落盘」）→ 视频扩展名（`.mp4/.mkv/.webm/.mov/.avi`）只验容器魔数（313-322）→ 其它用 `ImageLoader.GetImagePixelSizeAsync`（325，实现在 `Features/Codec/ImageLoader.Linux.cs:11`，SKCodec 解码）｜理由：图片侧与上游等价（真解码）；**视频侧从「真打开」退化为「看 12 字节魔数」**，注释（315-317）已自述理由（libvlc 实例代价高）。行为差异：截断的 mp4（`ftyp` 完好、`moov` 缺失）会被接受并持久化，之后壁纸黑屏——上游能挡住。触发：用户选一个下载不全的 webm/mp4。
- **[移植 第 337-371 行 · `LooksLikeVideoContainer(string)`]** 移植新增｜功能损失：否｜建议：保留｜上游：无（用 `MediaSource`）｜移植：EBML `1A 45 DF A3` / `ftyp`（第 4 字节起）/ `RIFF....AVI ` 三组魔数，异常时 `Serilog.Log.Warning` 返回 false｜理由：上面的折中实现，逻辑本身正确（`.webm`/`.mkv` 走 EBML、`.mov`/`.mp4` 走 ftyp、`.avi` 走 RIFF）；局限：不认 `.flv`/`.ts`，但 `PickBackgroundFileAsync`（276-287）的过滤器也只放 `.mp4/.mkv/.webm`，一致。

### 本文件结论

1. 方向正确：三个 WinRT 依赖（`MediaSource`/`BitmapDecoder`/`StorageFile`）全部替换掉，且把上游「反斜杠路径」这个 Windows-only 隐患在 Linux 侧修好了（第 203-210 行，必须保留）。
2. 唯一实质退化是视频校验（第 313-322 行）从真解码降级为魔数嗅探，已在注释里登记，属可接受取舍。
3. `PickBackgroundFileAsync`（274-289）依赖 `Helpers/FileDialogHelper.Linux.cs`（上游 `Helpers/FileDialogHelper.cs` 被 csproj:135 Remove），过滤器列表与上游一致（bmp/jpg/png/webp/avif/jxl/mp4/mkv/webm）。

---

## Features/Background/BackgroundViewPage.xaml

- 变更行数：45（删除 19 / 新增 16，10 个 hunk）
- 编译状态：参与编译

### 差异清单

- **[移植 第 14 行 · `xmlns:tk`]** 移植新增｜功能损失：否｜建议：保留｜上游：无｜移植：`xmlns:tk="using:CommunityToolkit.WinUI.Controls"`（缩进与相邻 `xmlns:` 行不一致，纯外观）｜理由：第 153 行 `tk:WrapPanel` 需要。
- **[移植 第 135 行 · `GridView_Images` 控件类型]** 平台替换｜功能损失：是（容器语义）｜建议：保留｜上游：`<GridView x:Name="GridView_Images" ...>`（上游 xaml:134）｜移植：`<ListView x:Name="GridView_Images" ...>`（名字没改，控件换了）｜理由：`GridView`+`ItemsWrapGrid` 在本项目不可用；换成 `ListView` 后 `IsItemClickEnabled/ItemClick/SelectionChanged/CanDragItems/DragItemsStarting` 这些属性与事件全部保留并且**真的被 code-behind 接住**（`BackgroundViewPage.xaml.cs:305`、`:321`、`:351`），比截图页处理得更完整。
- **[移植 第 145-150 行 · `ListView.ItemContainerStyle`]** 平台替换｜功能损失：否｜建议：保留｜上游：`Style BasedOn="{StaticResource DefaultGridViewItemStyle}" TargetType="GridViewItem"`，`Margin=0,0,12,12`、`CornerRadius=4`（上游 144-149）｜移植：`BasedOn="{StaticResource DefaultListViewItemStyle}" TargetType="ListViewItem"`，同样两个 Setter（146-148）｜理由：基样式随控件类型换掉，`CornerRadius=4` 与 `Margin` 原样保留（容器圆角是否真渲染取决于 Uno 主题模板，属引擎软差异）。
- **[移植 第 151-155 行 · `ItemsPanel`]** 平台替换｜功能损失：是（不吃 ItemWidth/ItemHeight）｜建议：保留｜上游：`ItemsWrapGrid AreStickyGroupHeadersEnabled="False" ItemHeight="136" ItemWidth="228" Orientation="Horizontal"`｜移植：`tk:WrapPanel HorizontalSpacing="12" VerticalSpacing="12"`｜理由：面板没有条目尺寸属性（注释 `BackgroundViewPage.xaml.cs:38-39` 记 L-179），只能靠 code-behind 把尺寸写到容器（`ApplyTileSize` 98-112 + `OnImageContainerChanging` 115-121 + `UpdateTileSize` 144-175）。这条替换**没有丢功能**：列宽按可用宽度反推，反而修掉了上游「1200 DIP 窗口右侧永远空 204 DIP」（D56 注释）。
- **[移植 第 158-171 行 · 条目 `Grid_ImageItem` + `CachedImage`]** 平台替换｜功能损失：部分｜建议：保留｜上游：`CachedImage Width="216" Height="122" HorizontalAlignment="Center" VerticalAlignment="Center"`、`Source="{x:Bind FilePath}"`、无 `DecodePixelWidth`（上游 163-174）｜移植：`DecodePixelWidth="400"`、`HorizontalAlignment/VerticalAlignment="Stretch"`、`Source="{x:Bind DisplayPath, Mode=OneWay}"`、保留 `IsThumbnail="True" PngThumbnail="True" Stretch="UniformToFill"`、保留 `sh:PointerCursor.CursorShape="Hand"`（159）与 `ui:UIElementExtensions.ClipToBounds="True"`（160）｜理由：`DisplayPath`（`BackgroundFileItem.cs:35`）让视频条目能显示 `VideoThumbnailer` 抽出的首帧（上游在 Linux 上视频条目是黑块，因为 `CachedImage` 无法解码 `.webm`）；`DecodePixelWidth=400` 让 2560×1440 的壁纸按 400 px 解码（B21/L-96）。`ClipToBounds`+容器圆角在这一页是齐的，**遮罩真的生效**（与截图页形成对照）。唯一损失：尺寸从「216×122 居中」变成「拉伸填充容器」，条目宽高由 `UpdateTileSize` 决定，不再固定。
- **[移植 第 172-175 行 · `RenderTransform`/`ScaleTransform`]** 平台替换（位置前移）｜功能损失：是（悬停放大）｜建议：待定｜上游：`RenderTransform` 在 `RenderTransformOrigin` 之后、`Source` 之前；并配有悬停放大的 `Storyboard`（本 hunk 只调换了属性顺序）｜移植：`RenderTransform` 块顺序提前到 `Source` 之前｜理由：属性顺序变化本身无影响；悬停放大只在截图页上游存在（本页上游无 `Storyboard`，diff 里也没有对应删除），所以这条纯属排布差异，可接受。
- **[移植 第 188 行 · `Grid.ContextFlyout` 右键菜单]** 平台替换｜功能损失：否｜建议：保留｜上游：同一份 XAML 菜单（`MenuFlyoutItem_ScreenshotInfo` 带 `Loading` 事件、`Open/OpenWith/CopyFile/Delete/...`）｜移植：整块原样保留（`MenuFlyoutItem_ScreenshotInfo_Loading`、`MenuFlyoutItem_Open_Click`、`MenuFlyoutItem_OpenWith_Click`、`MenuFlyoutItem_CopyFile_Click`、`MenuFlyoutItem_Delete_Click` 等都在 code-behind 里）｜理由：**这条是本次审计的关键反证**：同一项目里 `Grid.ContextFlyout` 能编过并工作，说明 `ScreenshotPage.Linux.cs:315` 注释「Uno 的 Grid 没有这个属性（CS0117）」的前提不成立（或至少是过时结论）。
- **[移植 第 297-298 行 · `</ListView>` 收尾]** 平台替换｜功能损失：否｜建议：保留｜上游：`</GridView.ItemTemplate></GridView>`｜移植：`</ListView.ItemTemplate></ListView>`｜理由：与第 135 行同一处替换的闭合标签。

### 本文件结论

1. 背景图库页是本组**移植质量最高**的文件：控件替换后事件面（点击/拖拽/多选/右键菜单）全部保留并在 code-behind 中接住，圆角 + `ClipToBounds` 齐备，条目尺寸还能自适应。
2. 唯一可见退化：条目不再固定 216×122 居中、没有悬停放大；均属外观。
3. 顺带否定 `ScreenshotPage.Linux.cs:315` 的「Grid 无 ContextFlyout」结论。

---

## Features/Background/BackgroundViewPage.xaml.cs

- 变更行数：167（删除 12 / 新增 145，10 个 hunk）
- 编译状态：参与编译

### 差异清单

- **[移植 第 30-31 行 · 字段 `_tileWidth`/`_tileHeight`]** 移植新增｜功能损失：否｜建议：保留｜上游：无（`ItemsWrapGrid` 自带 `ItemWidth/ItemHeight`）｜移植：缓存当前条目尺寸｜理由：`tk:WrapPanel` 没有条目尺寸，只能自己记（L-179）。
- **[移植 第 37-40 行 · 构造函数新增订阅]** 移植新增｜功能损失：否｜建议：保留｜上游：只有 `InitializeComponent()`｜移植：`SizeChanged += OnPageSizeChanged;` + `GridView_Images.ContainerContentChanging += OnImageContainerChanging;`｜理由：前者触发重新算列，后者给「滚动中新实体化的容器」补尺寸，缺任一个都会出现「第一行铺满、往下滚的条目宽度错误」。
- **[移植 第 66-89 行 · `GenerateVideoThumbnailsAsync(items)`]** 移植新增｜功能损失：否（补功能）｜建议：保留｜上游：无｜移植：遍历 `IsVideo && ThumbnailPath is null` → `await VideoThumbnailer.EnsureAsync(item.FilePath)`，成功后在 `DispatcherQueue.TryEnqueue` 里写 `item.ThumbnailPath`（强制 UI 线程发 PropertyChanged）｜理由：上游视频条目靠 `ImageEx` 解 `.webm` 首帧，Linux 侧必然黑块（L-98）。实测调用链成立：`LoadBackgroundItems` 第 226 行 `_ = GenerateVideoThumbnailsAsync(BackgroundImages);`；`VideoThumbnailer.EnsureAsync` 签名 `(string videoPath, CancellationToken = default)`（`VideoThumbnailer.cs:55`），单参调用合法；内部 `_inFlight` 去重（36 行）保证与 `CachedImage` 的 `IsThumbnail` 视频分支（`CachedImage.Linux.cs:280`）不会重复抽帧。
- **[移植 第 92-121 行 · `ApplyTileSize` / `OnImageContainerChanging`]** 移植新增｜功能损失：否｜建议：保留｜上游：无｜移植：给 `ListViewItem` 直接写 `Width/Height`（98-112），新容器在 `ContainerContentChanging` 里补（115-121）｜理由：见上；注意第 92-97 行有两段相邻的 `<summary>`（注释重复，纯外观）。
- **[移植 第 125-175 行 · `OnPageSizeChanged` / `UpdateTileSize`]** 移植新增｜功能损失：否（修 D56「右侧空 204 DIP」）｜建议：保留｜上游：无｜移植：`avail = ActualWidth - Padding`；`gap=24`、`minTile=216`、`cols=floor((avail+gap)/(minTile+gap))`、`tile=floor((avail-gap*(cols-1))/cols)`、`height=round(tile*122/216)`；变化 < 0.5 时早退防抖；最后 `InvalidateMeasure` + `Serilog.Log.Debug`（145-175）｜理由：把上游「固定 216 宽 + 12 间距」换算成均分列宽，逻辑自洽（列数不变时不抖动）。第 127 行会连带调用 `ItemsPanelRoot?.InvalidateMeasure()`。
- **[移植 第 226-227 行 · `LoadBackgroundItems` 尾部]** 移植新增｜功能损失：否｜建议：保留｜上游：只有 `BackgroundImages = new(result); UpdateSelectCountText();`｜移植：追加 `_ = GenerateVideoThumbnailsAsync(BackgroundImages);` 与低优先级 `UpdateTileSize`｜理由：`UpdateTileSize` 必须等面板实体化后才有 `ItemsPanelRoot`，所以用 `DispatcherQueuePriority.Low` 排队；若首次进页面仍拿到 null，`UpdateTileSize` 会早退（第 148-151 行），而 `SizeChanged` 会再触发一次——存在「首帧尺寸为 0 时列宽不更新」的窄窗，属可接受。
- **[移植 第 250-254 行 · `OpenFolderAsync`]** 平台替换｜功能损失：否｜建议：保留｜上游：`await Launcher.LaunchFolderPathAsync(folder);`（上游 xaml.cs:127）｜移植：`Helpers.OpenUrl.Open(folder)`，失败 `_logger.LogWarning`｜理由：`Launcher` 在 Linux 上是空实现（L-74），必须走 `xdg-open`。
- **[移植 第 313 行 · `GridView_Images_ItemClick`]** 平台替换｜功能损失：否｜建议：保留｜上游：`await Launcher.LaunchUriAsync(new(item.FilePath));`（上游 xaml.cs:186）｜移植：`Starward.Helpers.OpenUrl.Open(item.FilePath);`｜理由：同上，`OpenUrl` 内部走 `xdg-open`；点击条目打开图片/视频的行为保留。
- **[移植 第 382 行 · `MenuFlyoutItem_Open_Click`]** 平台替换｜功能损失：否｜建议：保留｜上游：`await Launcher.LaunchUriAsync(new(item.FilePath));`（上游 xaml.cs:255）｜移植：`Starward.Helpers.OpenUrl.Open(item.FilePath);`｜理由：同上。注意方法签名仍是 `async void` 但已无 `await`（1138 警告级，不影响运行）。
- **[移植 第 433-439 行 · `MenuFlyoutItem_OpenInExplorer_Click`]** 平台替换｜功能损失：是（「选中该文件」丢失）｜建议：保留｜上游：`StorageFile.GetFileFromPathAsync` + `FolderLauncherOptions.ItemsToSelect.Add(file)` + `Launcher.LaunchFolderAsync(parent, options)`（上游 xaml.cs:306-309）｜移植：`Path.GetDirectoryName(item.FilePath)` 后 `OpenUrl.Open(folder)`，只有打开目录｜理由：Linux 没有「打开文件管理器并选中某文件」的跨桌面通用接口（`xdg-open dir` 只能到目录）。用户看到的是「打开了 bg 目录，还得自己找那个文件」。
- **[移植 第 457-461 行 · `MenuFlyoutItem_OpenWithDefault_Click`]** 平台替换｜功能损失：否｜建议：保留｜上游：`StorageFile.GetFileFromPathAsync` + `Launcher.LaunchFileAsync(file)`（上游 xaml.cs:328-329）｜移植：`OpenUrl.Open(file)`｜理由：等价（`xdg-open` 走默认关联）。
- **[移植 第 479-484 行 · `MenuFlyoutItem_OpenWith_Click`]** 平台替换｜功能损失：是（无「选择打开方式」对话框）｜建议：保留｜上游：`new LauncherOptions { DisplayApplicationPicker = true }` + `LaunchFileAsync`（上游 xaml.cs:348-350）｜移植：`OpenUrl.Open(file)`，注释说明选择器交给桌面环境｜理由：Linux 无跨桌面「打开方式」选择器；GTK/KDE 下 `xdg-open` 不给选择。菜单项与「用默认应用打开」此时行为相同（重复项），属观感/功能双降级但无更好做法。

### 本文件结论

1. 本文件是「平台缺失 → 自己补实现」的正面样本：视频缩略图、条目尺寸自适应、新增的 `VideoThumbnailer` 调用**都真的被调用**（`LoadBackgroundItems` 第 226 行），缩略图通过 `DisplayPath` 回填到 XAML（`BackgroundViewPage.xaml:170`）。
2. 唯一实质功能损失是右键菜单的「在文件夹中显示（并选中文件）」与「打开方式选择器」两处降级。
3. 遗留小瑕疵：第 92-97 行重复 `<summary>`；`MenuFlyoutItem_Open_Click`（376）与另外三个 handler 仍标 `async void` 但无 `await`。

---

## Features/Background/BackgroundViewWindow.xaml.cs

- 变更行数：39（删除 23 / 新增 10，6 个 hunk）
- 编译状态：参与编译

### 差异清单

- **[移植 第 1 行 · 文件头]** 文案外观｜功能损失：否｜建议：保留｜上游：带 BOM 的 `﻿using CommunityToolkit.Mvvm.ComponentModel;`｜移植：无 BOM｜理由：纯字节差。
- **[移植 第 8-9 行（删除上游 9-10 行）· using 段]** 平台替换｜功能损失：否｜建议：保留｜上游：`using Vanara.PInvoke; using Windows.Graphics;`｜移植：删除两行｜理由：Vanara（Win32 P/Invoke）与 WinRT `Windows.Graphics` 在 Linux 不存在；删掉后第 49-51 行改用 `WindowBase` 的居中实现。
- **[移植 第 31 行 · `InitializeWindow()`]** 平台替换｜功能损失：是（外观）｜建议：保留｜上游：`Title = "Starward - Background Viewer"; AppWindow.TitleBar.ExtendsContentIntoTitleBar = true; AppWindow.TitleBar.IconShowOptions = ...`（上游 32-34）｜移植：`Title = ...; AppWindow.Title = "Starward - Background Viewer";`（31-32），`ExtendsContentIntoTitleBar` 删除，`IconShowOptions` 保留（32）｜理由：Uno 无 `ExtendsContentIntoTitleBar`，改用 `AppWindow.Title` 让 X11 标题可见。后果：标题栏不再「内容延伸」，背景查看窗顶部多一条系统标题栏（XAML 里第 15 行 `RowDefinition Height="32"` 的留白仍在，视觉上变成 32 DIP 的空条 + 系统标题栏）。
- **[移植 第 47-51 行 · `CenterInScreen`]** 平台替换｜功能损失：是（多显示器居中）｜建议：保留｜上游：`User32.GetCursorPos` + `DisplayArea.GetFromPoint(...Nearest)` + `MoveAndResize` 居中到「光标所在显示器」的工作区（上游 49-61，含 `UIScale` 换算）｜移植：`base.CenterInScreen(width ?? 1200, height ?? 676)`（注释：Uno X11 下 `MoveAndResize` 是空操作，位置交给窗口管理器，只按 DIP 设尺寸，L-35）｜理由：`WindowBase.Linux.cs:148` 的 `CenterInScreen(int?,int?)` 已实现「按 DIP 设尺寸 + 由 WM 放置」。损失：多显示器时窗口不再保证出现在光标所在屏（由 WM 策略决定），且尺寸参数语义从「物理像素缩放后」变成「DIP 直传」（`WindowBase.Linux.cs:154-155` 注释里仍按 `UIScale` 放大，注意与 `DefaultWindowSizeInDips` 路径分工）。
- **[移植 第 55 行 · `DefaultWindowSizeInDips`]** 移植新增｜功能损失：否｜建议：保留｜上游：无此成员（尺寸靠 `CenterInScreen`/`Show` 里的 `MoveAndResize`）｜移植：`protected override (int Width, int Height)? DefaultWindowSizeInDips => (1200, 676);`｜理由：`WindowBase.Linux.cs:72-90` 的模板钩子，`base.Show()` 用它设置初始尺寸，是上面那条替换的配套。
- **[移植 第 59-64 行 · `Show()`]** 平台替换｜功能损失：是（不再与主窗对齐）｜建议：保留｜上游：`AppWindow.GetFromWindowId(MainWindowId)`；拿不到就 `CenterInScreen(1200,676)`，拿到就 `MoveAndResize` 到主窗的位置与尺寸（上游 65-77）｜移植：`base.Show();` + 3 行注释（避免两次 Resize 打架 D49）｜理由：`AppWindow.GetFromWindowId` 在 Uno 恒返回同一实例（`AppWindow.Id` 恒 `0x1`，AGENTS.md 已登记），上游逻辑在 Linux 无意义。损失：背景查看窗不再「叠在主窗上、同尺寸」，而是独立尺寸 1200×676 由 WM 摆放；XAML 与页面逻辑不依赖该几何，属可接受偏离。

### 本文件结论

1. 全部改动围绕「Win32 窗口几何不可用」，无业务逻辑变化；标题、图标、`DesktopAcrylicBackdrop`、Esc 关闭、主题切换、关闭时解绑 Messenger 全部保留（第 33-37、68-103 行）。
2. 可见退化：标题栏变成「32 DIP 空白条 + 系统标题栏」、多显示器居中失效、窗口不再与主窗同尺寸。三项都建议保留现状（D49/L-35 已登记），但第 15 行的 32 DIP 空行值得单独确认是否需要删。

---

## Features/Background/BackgroundFileItem.cs

- 变更行数：23（删除 1 / 新增 19，3 个 hunk）
- 编译状态：参与编译

### 差异清单

- **[移植 第 1 行 · using **]** 移植新增｜功能损失：否｜建议：保留｜上游：无 `using CommunityToolkit.Mvvm.ComponentModel;`｜移植：新增该 using｜理由：下面把类改成 `ObservableObject` 的前提。
- **[移植 第 7 行 · 类型声明]** 移植新增｜功能损失：否｜建议：保留｜上游：`public class BackgroundFileItem`（上游 6）｜移植：`public partial class BackgroundFileItem : ObservableObject`｜理由：只为承载 `ThumbnailPath` 的属性变更通知；`ObservableObject` 来自 `CommunityToolkit.Mvvm`（已在包引用里）。注意 `partial` 目前没有第二处声明（`grep "partial class BackgroundFileItem"` 仅此一处），是 `ObservableObject` 源生成器习惯写法，无害。
- **[移植 第 21-31 行 · `ThumbnailPath`]** 移植新增｜功能损失：否（补功能）｜建议：保留｜上游：无｜移植：`field` 关键字自动属性 + `SetProperty` 成功后 `OnPropertyChanged(nameof(DisplayPath))`｜理由：视频首帧回填的唯一通道（`BackgroundViewPage.xaml.cs:81` 在 UI 线程写它）。
- **[移植 第 34-35 行 · `DisplayPath`]** 移植新增｜功能损失：否（补功能）｜建议：保留｜上游：无（XAML 直接绑 `FilePath`）｜移植：`public string DisplayPath => ThumbnailPath ?? FilePath;`｜理由：`BackgroundViewPage.xaml:170` 绑的就是它；这条 + `ThumbnailPath` 让「视频条目显示真实首帧」在 Linux 上成立。属**真正被调用**的移植新增。

### 本文件结论

新增的两个成员都是为了让视频条目有画面，链路完整（`LoadBackgroundItems` → `GenerateVideoThumbnailsAsync` → `ThumbnailPath` → `DisplayPath` → XAML）。无功能损失；`partial` 无第二处声明，可去可留。

---

## Helpers/PointerCursor.cs

- 变更行数：4（删除 1 / 新增 2，1 个 hunk）
- 编译状态：参与编译

### 差异清单

- **[移植 第 7-8 行 · 类声明与注释]** 移植新增｜功能损失：否｜建议：改回上游（去掉 `partial` 与注释）｜上游：`public class PointerCursor : DependencyObject`（上游 7）｜移植：第 7 行注释「partial 是必需的，不是装饰——去掉会 CS0260（同一编译单元里另有一处分部声明，来源未查明，见 18-upstream-table.md 复核记录）」+ 第 8 行 `public partial class PointerCursor : DependencyObject`｜理由：`grep -rn "class PointerCursor" --include=*.cs .` 全仓只有 `src/Starward/Helpers/PointerCursor.cs:7` 与 `src/Lunarward/Helpers/PointerCursor.cs:8` 两处，而 `src/Starward/**` 不参与 `Lunarward.csproj` 编译（项目里没有链接上游源码，靠 Remove + `*.Linux.cs` 替身）→ **注释声称的 CS0260 无法复现**，属过时/错误结论。保留 `partial` 无害（不产生第二个分部），但注释会误导后来者；建议改回上游并删注释（若真要留 `partial`，把理由改成中性的一句）。

### 本文件结论

1. 语义零变化，唯一问题是那条无法复现的注释（事实性问题，非功能问题）。
2. 与本组「指针」相关的实证：`PointerCursor.CursorShape` 在 9 处 XAML 使用（`BackgroundViewPage.xaml:159`、`GameLauncher/GameBannerAndPost.xaml:51`、`GameRecord/Genshin/StygianOnslaughtPage.xaml:275`、`GameRecordPage.xaml:49,69`、`GameSelector/GameSwitcher.xaml:52`、`LauncherConfigView.xaml:117,620`、`ViewHost/MainView.xaml:53`），实现走 `UnsafeAccessor` 取 `UIElement.set_ProtectedCursor`；该成员在 Uno 程序集里确实存在（`~/.nuget/packages/uno.winui/6.7.135/lib/net10.0/Uno.UI.dll` 含 `get_ProtectedCursor`/`set_ProtectedCursor`/`UpdateProtectedCursor`）→ 手型光标有真实落点（推断，未做 GUI 实测：本任务禁止构建/运行）。「指针隐藏」（全屏看图时隐藏鼠标）两侧都不存在，属平台未做，不是本次 diff 造成的损失。
3. `ScreenshotPage.xaml` 丢掉了 `PointerCursor.CursorShape="Hand"`（本文件对照见上），同一能力在同组两页里一有一无。

---

## 汇总

必须修项（按严重度）：

- **D1**｜截图页「拖出到文件管理器」整体丢失（上游 `CanDragItems/DragItemsStarting` → `StorageFile` 拖出），`ScreenshotPage.xaml:59-60` + `ScreenshotPage.Linux.cs`（无 `DragItemsStarting`）。
- **D2**｜截图页不再显示「游戏内截图目录」（上游 `GetGameScreenshotPathAsync` 覆盖 `hk4e ScreenShot` / `hkrpg StarRail_Data\ScreenShots` / `bh3/nap ScreenShot`），`ScreenshotPage.Linux.cs:100-115`；用户游戏内 F12 截图不进图库。
- **D3**｜截图页离开后 `FileSystemWatcher` 不释放（`NavigationCacheMode="Required"` + 无 `OnUnloaded`），`ScreenshotPage.Linux.cs:160,200-233`（上游 `ScreenshotPage.xaml.cs:OnUnloaded` 有释放）。
- **D4**｜同一「复制为 JPG」图标两处取值不一致：`ScreenshotFolderManageDialog.xaml:100` 已改 `E74E`，`ScreenshotPage.Linux.cs:337` 仍是 `\uF413`（豆腐块）。
- **D5**｜截图页卡片圆角只声明不裁剪（`Border`/`Grid` 的 `CornerRadius` 无 `ClipToBounds`），`ScreenshotPage.xaml:68-78`；对照 `BackgroundViewPage.xaml:160` 有 `ClipToBounds`。
- **D6**｜截图条目缺手型指针（上游 `sh:PointerCursor.CursorShape="Hand"` 未移植），`ScreenshotPage.xaml:76-83`。
- **D7**｜右键菜单「在文件夹中显示」只剩打开目录、不选中文件；「打开方式」退化为默认打开（`BackgroundViewPage.xaml.cs:433-439`、`479-484`）——无跨桌面接口，需产品层面认账或引入 `dbus` 文件管理器调用。
- **D8**｜视频背景校验从「真打开」退化为容器魔数嗅探，坏视频可被持久化后黑屏，`BackgroundService.cs:313-322`（已在注释登记，建议至少在选完后补一次「首帧能否解出」的异步校验）。
- **D9**｜`ImageThumbnail.cs`（400 行）与 `ScreenCaptureService.cs` 等死代码仍在仓库里带「已适配」改动（`ImageThumbnail.cs:31`、`ScreenCaptureService.cs:405`、`ScreenshotPage.xaml.cs:222-225`），会误导后续同步；建议标注或删除。
- **D10**｜`ScreenshotPage.Linux.cs:315` 注释称「Uno 的 Grid 没有 ContextFlyout（CS0117）」与同项目 `BackgroundViewPage.xaml:188`（`Grid.ContextFlyout` 正常编译）矛盾；`PointerCursor.cs:7` 注释称去掉 `partial` 会 CS0260，全仓无第二处声明。两条注释需更正（事实性问题）。
- **D11**｜截图链路平台缺口（记录，不必修）：客户区裁剪、只抓游戏窗口、HDR/UHDR + 自动 SDR、XMP/色彩管理、`ScreenCaptureInfoWindow` 预览均不存在，见 `ScreenCapture.Linux.cs:48-118` 与本文对照表。
- **D12**｜`BackgroundViewWindow.xaml.cs:31` 换掉 `ExtendsContentIntoTitleBar` 后，XAML 里 `RowDefinition Height="32"`（`BackgroundViewWindow.xaml:15`）留下一条 32 DIP 空带，建议实测后决定是否删。
