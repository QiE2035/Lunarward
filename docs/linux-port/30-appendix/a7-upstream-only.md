# 仅上游存在的 16 个文件：能否移植

> 只读评估。基线：`src/Starward/` @ HEAD `2b1e7b46`（上游 0.19.0 = `747ed10b` 是 HEAD 祖先，已 `git merge-base --is-ancestor` 确认）；移植侧 `src/Lunarward/`。
> 口径：下文行号均为 `src/Starward/...` 或 `src/Lunarward/...` 的当前工作树行号。
> **重要事实更正**：`docs/linux-port/12-shims-and-gaps.md:178` 记「D134 删掉上游已移除的截图二代/批量转换/图片查看与 `TestUrlProtocolWindow`」——这个前提**不成立**。这 16 个文件在 0.19.0 全部存在（`git cat-file -s 747ed10b:src/Starward/Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml` = 9718 字节，其余同理），D134 的清账理由要回改。

---

## Features/GameLauncher/GameNoticeWindow.xaml

- **平台依赖**：`WebView2 Name="webview"`（第 24–30 行，`DefaultBackgroundColor="Transparent"`，主题画刷资源 `BrushForThemeBackgroundColor`）；其余是 `ProgressRing` 加载遮罩。
- **上游引用点**：`Features/GameLauncher/GameBannerAndPost.xaml:90`（`Command="{x:Bind OpenGameNoticeWindowCommand}"`）；`Features/GameLauncher/GameBannerAndPost.xaml.cs:306-310`（`new GameNoticeWindow { CurrentGameBiz, ParentWindowHandle = (nint)this.XamlRoot.ContentIslandEnvironment.AppWindowId.Value }.Activate()`）。
- **移植侧现状**：`src/Lunarward/Features/GameLauncher/GameBannerAndPost.xaml.cs:435-455` `OpenGameNoticeWindowAsync()` → `GameNoticeService.GetGameNoticeUrlAsync(CurrentGameId.GameBiz)` → `Helpers.OpenUrl.Open(url)`，并把 `IsGameNoticesAlert = false`（B61：改浏览器后无关闭事件，打开即已读）。`12-shims-and-gaps.md:116`、`:255` 已登记「公告窗改系统浏览器」。
- **结论**：**不需要**（已被等价实现取代）
- **依据与改动面**：D64/提交 `5cd4862b` 主动删除；公告页是米哈游网页（需登录态 + 官方 JS SDK），Uno 的 WebView2 在本机只出黑底（真机截图证据在 `GameBannerAndPost.xaml.cs:439-441` 注释里），换 WebKitGTK 也要重做登录/回调，收益为负。无改动面。

## Features/GameLauncher/GameNoticeWindow.xaml.cs

- **平台依赖**：`Vanara.PInvoke.User32`——`InputSiteSubclassProc` 接 `WM_KEYDOWN` 判 Esc 关闭（第 103–117 行）、`SetWindowLong(GWL_STYLE, … & ~WS_DLGFRAME)`（第 71 行）、`SetWindowLong(GWL_HWNDPARENT, ParentWindowHandle)`（第 94 行，把窗口挂成父窗口的子窗）；`AppWindow.GetFromWindowId(new WindowId((ulong)ParentWindowHandle)).MoveAndResize(...)`（第 84–93 行，贴合父窗口几何）；`WebView2.CoreWebView2`（`EnsureCoreWebView2Async` 146、`ExecuteScriptAsync` 223、`WebMessageReceived` 236、`DOMContentLoaded` 169）；`Helpers/TransparentBackdrop`；`Timer(10000)` 10 秒自动关（第 129–132 行）。
- **上游引用点**：`Features/GameLauncher/GameBannerAndPost.xaml.cs:306-310`；自身 `Closed` → `WeakReferenceMessenger.Default.Send(new GameNoticeWindowClosedMessage())`（第 49 行）。
- **移植侧现状**：同上一节（浏览器打开）。
- **结论**：**不需要**
- **依据与改动面**：整窗的存在理由是「在游戏窗口上方内嵌显示公告网页」，Win32 子窗挂靠 + WebView2 两个前提在 Linux 都无对应物。即便日后做 WebKitGTK 内嵌窗，也应新写（`Features/GameRecord/WebLoginService.cs` 已有 Gtk4+WebKitGTK 6.0 的绑定可借），不是移植这个文件。

## Features/GameLauncher/GameNoticeWindowClosedMessage.cs

- **平台依赖**：无。全文 6 行：`internal class GameNoticeWindowClosedMessage { }`（CommunityToolkit.Mvvm 消息类型）。
- **上游引用点**：`Features/GameLauncher/GameBannerAndPost.xaml.cs:62`（`Register<GameNoticeWindowClosedMessage>`）、`:98`（`OnGameNoticeWindowClosed`）；`Features/GameLauncher/GameNoticeWindow.xaml.cs:49`（发送）。
- **移植侧现状**：无此类型；`GameBannerAndPost.xaml.cs:435-455` 里直接 `IsGameNoticesAlert = false`，不需要消息回路。
- **结论**：**不需要**
- **依据与改动面**：0 平台依赖，但它是「窗口关掉 → 熄灭红点」这条回路的载体；窗口不进移植，消息也就没有生产者。补它等于留一个永不触发的死类型。

## Features/Screenshot/ImageBatchConvertWindow.xaml

- **平台依赖**：XAML 层无 Win2D / WebView2。控件：`ListView`（拖放导入，第 82–83 行事件接线）、两个 `ComboBox`（输出格式 JPEG/PNG/AVIF/JPEG XL、已存在时的 Skip/Overwrite/Rename）、`Slider`（质量 0–100）、`ProgressRing`、`TextBlock` 体量对比，全部是 Uno 有等价物的控件。
- **上游引用点**：`Features/Screenshot/ScreenshotPage.xaml:267`（`Command="{x:Bind OpenImageBatchConvertWindowCommand}"`，页面右上「批量转换」按钮）；`Features/Screenshot/ScreenshotPage.xaml.cs:338-343`（`new ImageBatchConvertWindow().Activate(list)`）。
- **移植侧现状**：`src/Lunarward/Features/Screenshot/ScreenshotPage.xaml:115` 按钮仍在，绑到 `ScreenshotPage.Linux.cs:19,274` 的 `OpenImageBatchConvertWindowCommand` → `BatchConvertAsync()`（`ScreenshotPage.Linux.cs:581-607`）：**只做「转 JPEG」**，且要求先切多选并勾选，否则提示「请先切换多选并勾选要转换的截图」。转换实体在 `src/Lunarward/Features/Screenshot/ScreenshotConverter.cs`（`ConvertToJpeg(IEnumerable<string>, int quality=92)`，Skia 解码+编码，注释写明「解码走 Helpers/ImageDecode，AVIF/JXL 由 Magick.NET 兜底」）。`12-shims-and-gaps.md:257` 记「功能由 `ScreenshotConverter` + 设置页批量转 JPG 覆盖」。
- **结论**：**可选**
- **依据与改动面**：主链路（看图→转一份 JPG）已经能用，但用户可感知的差距明确：**没有输出格式选择（PNG/AVIF/JXL）、没有质量与覆盖策略、没有输出目录、没有逐文件体量对比**。Uno 侧控件齐备（`ComboBox`/`Slider`/`ListView`/`ProgressRing` 都在用）。

## Features/Screenshot/ImageBatchConvertWindow.xaml.cs

- **平台依赖**：`Microsoft.Graphics.Canvas`（`CanvasBitmap`、`ImageLoader.LoadImageAsync` 返回的 `ImageInfo.CanvasBitmap`，第 3、673、682、691、756 行）；`Windows.Graphics.Imaging`（`BitmapDecoder.CreateAsync` / `BitmapEncoder.CreateAsync`，第 642–651 行，JPEG 质量走 `ImageQuality`+`JpegYCrCbSubsampling` typed value）；`Windows.Storage`（`StorageFile.GetFileFromPathAsync` 第 323、340 行，拖放里 `IStorageFolder` 递归）；`Windows.ApplicationModel.DataTransfer`（DragOver/Drop）；`FileDialogHelper.PickMultipleFilesAsync` / `PickFolderAsync`（第 149、174 行）；**外部进程** `avifdec` / `djxl`（第 590–628 行 `Process.Start`）；AVIF/JXL 编码器 `avifEncoderLite` / `JxlEncoder` 来自 `Starward.Codec.AVIF` / `Starward.Codec.JpegXL`——源码在 `vendor/upstream/large-resource/Starward.Codec/`，**移植侧 `Lunarward.csproj` 没有引用这个工程**，所以这两个编码器当前不可用。
- **上游引用点**：`Features/Screenshot/ScreenshotPage.xaml:267` + `Features/Screenshot/ScreenshotPage.xaml.cs:338-343`（唯一调用点）。
- **移植侧现状**：见上条——`ScreenshotPage.Linux.cs` 内联实现，`ScreenshotConverter.cs` 用 SkiaSharp；`12-shims-and-gaps.md:257` 已把这个 `.xaml` 标成待 `.xaml.disabled`（但实物在 D134 已被删掉，与文档不符）。
- **结论**：**可选**
- **依据与改动面**：
  - 解码：`src/Lunarward/Helpers/ImageDecode.cs` `Load(path, maxEdge)`（Skia → Magick 兜底，覆盖 AVIF/JXL/HEIC）；
  - 编码：`SKImage.Encode(SKEncodedImageFormat.Jpeg|Png, q)` 直接可用；AVIF/JXL 走 `Helpers/MagickCodec.cs` `TryEncode(byte[] bgra, int width, int height, bool jxl, int quality)`（第 129 行）——**不必引 `Starward.Codec` 工程**，这也是移植侧既定的取舍（`12-shims-and-gaps.md:183` 把 `Features/Codec/*` 记成 M4 接 `Starward.Codec`）；
  - 文件对话框：`Helpers/FileDialogHelper.Linux.cs` 已有；
  - 拖放导入：Uno 的 `DragEventArgs.DataView` 能拿到 `StorageItems`，但 Linux 下从文件管理器拖入的 MIME 需实测，建议先保留「导入按钮」这一条路；
  - 估算：重写 4 条转换分支（JPEG/PNG 一条、AVIF、JXL、以及「不转格式只转质量」）+ 逐项体量对比，约 400–500 行、1.5–2 天；`ScreenshotConverter.ConvertToJpeg` 可保留作 CLI/热键路径。

## Features/Screenshot/ImageViewWindow.xaml

- **平台依赖**：`sc:CachedImage`（自定义控件，移植侧 `Controls/CachedImage.Linux.cs` 已存在）+ `ScrollViewer ZoomMode="Enabled"`（缩放/拖拽平铺）+ 底部 `GridView` 缩略图条；**无 Win2D**，是纯 XAML 方案。
- **上游引用点**：**无引用，疑似遗留**——`grep -rn "new ImageViewWindow[^2]"` 只命中它自己的构造函数 `Features/Screenshot/ImageViewWindow.xaml.cs:33`；`ScreenshotPage.xaml.cs` 里两处打开看图窗都是 `new ImageViewWindow2()`（第 384、467 行）。git 上它随 2025-01-05 `e6adf9c3 screenshot page & image viewer window` 引入，2025-07-08 `0b62f429 new image viewer` 加了 v2 后就没再被接线。
- **移植侧现状**：无；移植侧看图窗是自研 `src/Lunarward/Features/Screenshot/ScreenshotViewerWindow.xaml(.cs)`。
- **结论**：**不需要**
- **依据与改动面**：上游自己也不 `new` 它。移植一个死窗口只会增加 Uno 差异面。

## Features/Screenshot/ImageViewWindow.xaml.cs

- **平台依赖**：`Vanara.PInvoke.User32.ShowWindow((nint)AppWindow.Id.Value, SW_SHOWMAXIMIZED)`（第 59 行）；`AppWindow.SetPresenter(FullScreen/Overlapped)`（第 551–558 行）；`ClipboardHelper.SetStorageItems(DataPackageOperation.Copy, file)`（第 275 行）；`Windows.Storage`；`CachedImage` 的 `ImageExOpened`/`RotationTransition`。
- **上游引用点**：**无引用，疑似遗留**（`new ImageViewWindow(` 零命中）。
- **移植侧现状**：无。
- **结论**：**不需要**
- **依据与改动面**：功能面（缩放/旋转/复制/全屏/翻页）已被 v2 完全覆盖，而 v2 才是被调用的那个。

## Features/Screenshot/ImageViewWindow2.xaml

- **平台依赖**：`mgcux:CanvasSwapChainPanel x:Name="CanvasSwapChainPanel_Image"`（第 241 行，`xmlns:mgcux="using:Microsoft.Graphics.Canvas.UI.Xaml"`）；`cwc:Segmented`（CommunityToolkit.WinUI `Segmented`，第 645–650 行，Uno 无此控件）；`FontFamily="{ThemeResource PhotoFluentIcons}"`（第 68 行，Uno 会拦截 Segoe 图标族名，见 L-15/L-88）；`GridView` 缩略图画廊；`ScrollViewer ZoomMode="Enabled" MaxZoomFactor="5"`。
- **上游引用点**：
  - `Features/Screenshot/ScreenshotPage.xaml.cs:384`（双击条目 → `new ImageViewWindow2().ShowWindowAsync(AppWindowId, item, _screenshots)`）、`:467`；
  - `Features/Screenshot/ScreenshotPage2.xaml.cs:496`（死页里的同一调用）；
  - `Features/Setting/ToolboxSetting.xaml.cs:41`（`nameof(ImageViewWindow2)` 工具箱项）、`:85-87`（`new ImageViewWindow2().ShowWindow(...)`）；
  - `Features/Screenshot/ScreenCaptureInfoWindow.xaml.cs:103`（字段 `_imageViewWindow2`，截图 OSD 里点缩略图打开它）。
- **移植侧现状**：`src/Lunarward/Features/Screenshot/ScreenshotWindowsDisabled.cs:12-24` 是个空壳 `ImageViewWindow2 : WindowBase`（正文只有一句「图片查看器将在 M3 用 SkiaSharp 重写。」），唯一目的是让工具箱入口能编译；真正在用的是 `ScreenshotViewerWindow.xaml(.cs)`（顶栏文件名 + 上一张/下一张/在文件夹中显示/关闭 + 单个 `Image` + 「Esc 关闭 · ← → 切换」）。`12-shims-and-gaps.md:58` 明写现状：「查看器缺上游的缩放/旋转/复制/另存（`ScreenshotViewerWindow` 只做浏览）」；`18-upstream`/`12-shims:258` 记 `ImageViewWindow2.xaml` 改 `.xaml.disabled`（实物已被 D134 删）。
- **结论**：**必须移植**（能力层面；原文件不可原样移植，须以 Skia 重写）
- **依据与改动面**：它是上游**唯一在用的看图窗**——截图页双击、工具箱「图片查看」、截图 OSD 缩略图三个入口都指向它；移植侧现在只有一个「翻页浏览」的替代品，缩放/适应窗口/全屏/画廊/复制（文件·路径·图像）/删除/另存/拖放打开/图像信息全部缺失，是用户一眼可见的缺口。改动面：`ContentDialog` 不需要，`FlipView`/`Image`/`MenuFlyout`/`GridView` 都有 Uno 等价物；画布元素 `CanvasSwapChainPanel` 必须换掉——用 `ScrollViewer ZoomMode` + `Image`（Skia 已解码的位图源）即可覆盖缩放/拖拽；位图解码直接用 `Helpers/ImageDecode.cs`，缩略图用 `Controls/CachedImage`（含 `DecodePixelWidth`），HDR/色彩管理部分（`DisplayInformation`、`Segmented_DisplayMode`、`SDRLuminance` 滑杆）按 D31 归 M4。

## Features/Screenshot/ImageViewWindow2.xaml.cs

- **平台依赖**（1572 行里最重的一档）：
  - Win2D / DirectX：`CanvasSwapChain` + `CanvasSwapChainPanel_Image.SwapChain`（第 570–575 行）、`_canvasSwapChain.ResizeBuffers((float)width, (float)height, User32.GetDpiForWindow(WindowHandle))`（第 742 行）、`CanvasBitmap`、`Microsoft.Graphics.Canvas.Effects`（色彩映射），`DrawImage()`/`LoadImageAsync()`（第 619–763 行）；
  - `Microsoft.Graphics.Display.DisplayInformation.CreateForWindowId(AppWindow.Id)`（第 570 行）+ `AdvancedColorInfo`（第 589 行，HDR 亮度/色域）；
  - Vanara `User32`：`GetDpiForWindow`（第 45、739 行）、`ShowWindow(SW_SHOWMAXIMIZED/SW_MINIMIZE)`（第 918、958、978、1210 行）、`LoadIcon(hInstance, "#32512")`（第 1506 行）；
  - `[LibraryImport("Shcore.dll")]`（第 1553 行，DPI）；
  - `Windows.Storage`（`IStorageFile`、`StorageFile.GetFileFromPathAsync`、`FolderLauncherOptions`）、`Windows.System.Launcher.LaunchFolderAsync`；
  - `ClipboardHelper.SetStorageItems/SetText/SetBitmap`（第 1092、1113、1135 行）；
  - `AppWindow` 全套：`SetPresenter(FullScreen)`（1236–1247）、`MoveAndResize` 贴合父窗（912–917、974–978）、`SetIcon`（1501–1511）、标题栏拖拽区（`SetDragRectangles`，第 89 行）。
- **上游引用点**：同上一节的 4 处（ScreenshotPage ×2、ToolboxSetting ×2、ScreenCaptureInfoWindow、ScreenshotPage2）。
- **移植侧现状**：空壳（`ScreenshotWindowsDisabled.cs:12`）；真实替代 `ScreenshotViewerWindow.xaml.cs`（约 150 行，含 `ShowCurrentAsync`/`Button_Previous_Click`/`Button_Next_Click`/`Button_OpenFolder_Click`/`RootGrid.KeyDown`）。
- **结论**：**必须移植**（重写，不是搬运）
- **依据与改动面**：见上一节结论；额外要写清的三件事：①`ShowWindowAsync(Microsoft.UI.WindowId, …)` 有 3 个重载（第 908、929、969 行），移植侧 `WindowBase.Linux.cs:13` 的 `WindowHandle => IntPtr.Zero`、`MainWindowId` 恒等值，签名要降级成无 `AppWindowId` 参数的版本；②全屏用 `WindowBase.Linux.cs` 已有能力 + `AppWindow.SetPresenter` 在 Uno/Skia 下的行为需实测（`12-shims` 已知 `Window.Hide()` 原生为空、已用 libxcb 补齐）；③`SetIcon`/`AdaptTitleBarButtonColorToAcuallTheme` 两个 `public` 方法被 `ScreenCaptureInfoWindow` 等外部引用，改写时要么保留同名方法，要么同步改调用方。估算：≈600–800 行（缩放/适应/全屏 + 右键菜单 5 项 + 删除 + 另存 + 画廊 + 拖放），HDR 那部分留 M4。

## Features/Screenshot/ScreenCaptureInfoWindow.xaml

- **平台依赖**：XAML 自身是 `Grid` + `Image`（72×72 缩略图）+ `ProgressRing`/`FontIcon` + 「打开」按钮 + 失败态插画 `ms-appx:///Assets/Image/UI_EmotionIcon5.png`；**无 Win2D 引用**，但窗口形态依赖 Win32（见 .cs）：`RootGrid` 上有 `Translation="320,0,0"`（滑入动画起点）。
- **上游引用点**：`Features/Screenshot/ScreenCaptureService.cs:38`（字段）、`:92`/`:110`（`new ScreenCaptureInfoWindow()`，两条捕获路径：按窗口 hwnd / 按显示器 displayId）；`Features/Setting/ScreenshotSetting.xaml.cs:41`（设置页「测试截图」也用同一个 OSD）、`:292`、`:421`（错误文案）。
- **移植侧现状**：无此窗口。`src/Lunarward/Features/Screenshot/ScreenCapture.Linux.cs` 走外部工具链（`spectacle -b -n -f -o` → `grim` → `gnome-screenshot` → portal 兜底，第 55–105 行），反馈用 `Helpers/DesktopNotification.Show("截图已保存", Path.GetFileName(file), "lunarward", 4000)`（第 81、103、112 行）；设置页测试截图在 `Features/Setting/ScreenshotSetting.Linux.cs:175`（`TestCaptureAsync`）。
- **结论**：**可选**
- **依据与改动面**：主链路不阻塞——「截到了」这件事已经有桌面通知。差距是观感：上游 OSD 在屏幕右上（高度 25% 处）滑入，带 72×72 缩略图、`已保存/处理中`、连拍计数 `n/m`、「打开」按钮、失败态给「打开日志」；移植侧只有一行纯文本通知。要做需要：置顶无边框窗口（`Features/WindowBase.Linux.cs` 目前只有 `Hide/Minimize/ShowFromMinimized/CenterInScreen`，缺 `_NET_WM_STATE_ABOVE`，得在 `Helpers/X11Window` 层补）+ Skia 生成方形缩略图（`ImageDecode.Load(path, maxEdge)` 加裁剪）+ 不抢焦点的显示（X11 下 `override-redirect` 或 `WM_HINTS.input = False`）。约 250–350 行，且属「引擎软差异」范畴，建议排在必须项之后。

## Features/Screenshot/ScreenCaptureInfoWindow.xaml.cs

- **平台依赖**：Win2D（`CanvasBitmap` 入参、`CropEffect`/`ScaleEffect` 做 72×72 居中裁剪，第 213–240 行左右）；`Vanara`：`SetWindowLong(GWL_STYLE/EXSTYLE)` 去 `WS_DLGFRAME`、加 `WS_EX_TOPMOST`（第 64–69 行），`User32.GetDpiForWindow(hwnd)`（第 118、155 行）；`[LibraryImport("Shcore.dll")] GetDpiForMonitor`（第 197 行）；`AppWindow.MoveAndResize(rect)` + `AppWindow.Show(false)`（第 429–430 行，**Show(activate:false)** 不抢焦点）+ `AppWindow.Hide()`（第 437 行向后）；`ElementCompositionPreview.GetElementVisual` + `CreateVector3KeyFrameAnimation`（第 446–470 行，滑入/滑出 0.6s）；`DisplayArea.GetFromDisplayId(displayId)` 定位。
- **上游引用点**：同上一节（`ScreenCaptureService.cs:38/92/110`、`ScreenshotSetting.xaml.cs:41/292/421`）。
- **移植侧现状**：无（`ScreenCapture.Linux.cs` 用桌面通知替代）。
- **结论**：**可选**
- **依据与改动面**：同上。补充：`CaptureStart/CaptureSuccess` 的 4 个重载都以 `CanvasBitmap` + `hwnd`/`DisplayId` 为参数，移植侧捕获是「外部工具写文件」，天然拿不到 bitmap，接口得改成「传文件路径」，缩略图用 `ImageDecode` 现解——这也是它只能算优化的原因。

## Features/Screenshot/ScreenshotPage2.xaml

- **平台依赖**：XAML 层无 Win32/Win2D；但用了 WinUI 1.7 新控件 **`ItemsView`**（第 169–282 行）+ **`AnnotatedScrollBar`**（第 285–299 行，含自绘 `ControlTemplate`，注释里贴了 microsoft-ui-xaml 源码链接），Uno 侧这两个控件没有可用模板（`PipsPager` 模板不响应是同类的先例）。其余是 `BitmapImage`、`Image`。
- **上游引用点**：**无引用，上游自己也不用**——`grep -rn "ScreenshotPage2"` 在整个 `src/Starward/` 只命中它自己的 4 行；导航表是 `Features/ViewHost/MainView.xaml.cs:267`（`nameof(ScreenshotPage) => typeof(ScreenshotPage)`）、`MainView.xaml:83`（`Tag="ScreenshotPage"`）、`GameFeatureConfig.cs:80/93/106/123/140/156/171/186/202/218/233`（`nameof(ScreenshotPage)`）。`ScreenshotPage` 才是被 `Navigate` 的那个（2025-01-05 `e6adf9c3` 引入），`ScreenshotPage2` 是 2025-08-19 `a2862351 change style of screenshot page` 的**未接线改版尝试**。
- **移植侧现状**：无；移植侧图库页是 `ScreenshotPage.xaml` + `ScreenshotPage.Linux.cs`（`ItemsControl` + `tk:WrapPanel`，因 Uno 分组容器不渲染，L-48）。
- **结论**：**不需要**
- **依据与改动面**：上游死页，且它赌的 `ItemsView`/`AnnotatedScrollBar` 恰是 Uno 最不稳的一类控件。若将来要「按日期分组的滚动条 + 详情标签」，应基于 `ScreenshotPage.xaml` 自绘，不是捡这个文件。

## Features/Screenshot/ScreenshotPage2.xaml.cs

- **平台依赖**：无 Win32/Win2D（`using` 清单第 1–22 行只有 CommunityToolkit.Mvvm/WinUI、`Microsoft.UI.Xaml.*`、`Windows.ApplicationModel.DataTransfer`、`Windows.Storage`、`Windows.System`）；逻辑面：`FileSystemWatcher` 多文件夹监听、`ItemsView.ScrollView`/`IScrollController`、`AnnotatedScrollBar` 详情标签、`HoYoPlayService` 取游戏截图路径；601 行。
- **上游引用点**：**无引用，上游自己也不用**（同上）。
- **移植侧现状**：无；`ScreenshotPage.Linux.cs` 已用 `FileSystemWatcher` 实现同一套监听（`CreateFileSystemWatcher`），能力等价且已跑通。
- **结论**：**不需要**
- **依据与改动面**：它相对 `ScreenshotPage` 的增量只有「AnnotatedScrollBar 分组导航 + ItemsView 虚拟化」，两者在 Uno 上都无可靠模板；移植侧现有 `ItemsControl + WrapPanel` 是全量渲染，条目多时会慢（已知），但那是 `ScreenshotPage` 的性能议题，与本文件无关。

## Features/UrlProtocol/TestUrlProtocolWindow.xaml

- **平台依赖**：仅 `sf:WindowBase` + `Image Source="ms-appx:///Assets/logo.png"` + 两个 `TextBlock`（可执行路径、启动参数）。**零 Win32、零 WebView2、零 Win2D**。
- **上游引用点**：**无引用，不可达**——`git grep -n "TestUrlProtocol" 747ed10b` 只命中它自己的 `.xaml:2,9` 与 `.xaml.cs:12,15`；`Features/UrlProtocol/UrlProtocolService.cs` 里没有任何 `new …Window` / `Activate()`（`grep -rn "new .*Window\|Activate()"` 零命中），`Program.cs:86` 只调 `UrlProtocolService.HandleUrlProtocolAsync(args[0])`。2025-03-16 `bd64959d handle url protocol and startup arguments` 引入时是开发者自检窗，接线后来被撤掉。
- **移植侧现状**：无；`12-shims-and-gaps.md:245` 记过它曾在移植侧存在（只改了 logo 换成 `lunarward.png`，+1/−1 行），D134 被删。
- **结论**：**不需要**
- **依据与改动面**：上游不可达的死窗口，移植它等于给发行版多一些永不出现的代码。若确实想要「协议/参数自检」诊断入口，20–50 行就能复活（`Process.GetCurrentProcess().MainModule?.FileName` + `Environment.GetCommandLineArgs().Skip(1)`），按硬规则 6 应当走 `#if DEBUG`/`DevSwitches`，而不是恢复这个窗口。

## Features/UrlProtocol/TestUrlProtocolWindow.xaml.cs

- **平台依赖**：`AppWindow.TitleBar.ExtendsContentIntoTitleBar`、`CenterInScreen(480, 400)`、`OverlappedPresenter`（禁最大化/缩放）、`MicaController.IsSupported()`/`DesktopAcrylicController.IsSupported()`（Mica/Acrylic 背板）、`ShouldAppsUseDarkMode()`；`Windows` 侧没有 P/Invoke。
- **上游引用点**：**无引用，不可达**（同上）。
- **移植侧现状**：无。
- **结论**：**不需要**
- **依据与改动面**：同上。移植侧 `WindowBase.Linux.cs`/`SystemBackdropHelper.Linux.cs` 已能提供同等的窗口装饰能力，属实没必要为死窗口开工。

## Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml

- **平台依赖**：整页 Win2D 实时预览——`xmlns:canvas="using:Microsoft.Graphics.Canvas.UI.Xaml"`（第 4 行）、1200×680 的 HDR 对比画面、`ContentDialog` 资源覆写成全屏透明（`ContentDialogMaxWidth=10000` 等，第 12–20 行）、`CustomOverlayAcrylicBrush` 背板；code-behind 里还有 `DisplayInformation` 的 `MaxLuminanceInNits`/`SdrWhiteLevelInNits` 读数。
- **上游引用点**：`Features/GameSetting/GameSettingPage.xaml.cs:386`（`await new GenshinHDRLuminanceSettingDialog { XamlRoot = this.XamlRoot, CurrentGameBiz = this.CurrentGameBiz }.ShowAsync()`）。
- **移植侧现状**：**已有等价实现**——改名 `src/Lunarward/Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml.disabled`，新增 `GenshinHDRLuminanceSettingDialog.Linux.cs` 用代码重建同一个 `ContentDialog`：3 个 `Slider`（Max 300–2000、Scene 100–500、UI 150–550，默认 1000/300/350）+ 数值文本 + 「恢复默认」按钮 + 顶部一句「预览不可用」说明；读写走 `GameSettingService.Get/SetGenshinHDRLuminanceAsync`（真写游戏前缀，D95/B11、B70）。调用点 `src/Lunarward/Features/GameSetting/GameSettingPage.xaml.cs:519-524`。`12-shims-and-gaps.md:99`、`:194`、`:256` 均已登记。
- **结论**：**不需要**
- **依据与改动面**：功能面（三个亮度值可读可写）已 1:1 覆盖，缺的只有 Win2D 的实时 HDR 对比画面——Linux 没有等价的 HDR 交换链，属引擎软差异（D31）。0 改动面。

---

## 汇总

### 必须移植（2）

| 文件 | 为什么必须 | 形态 |
| --- | --- | --- |
| `Features/Screenshot/ImageViewWindow2.xaml` | 上游唯一在用的看图窗，3 个入口（截图页双击 / 工具箱 / 截图 OSD 缩略图）都指向它；移植侧 `ScreenshotViewerWindow` 只有翻页 | 以 Skia + Uno 控件重写 |
| `Features/Screenshot/ImageViewWindow2.xaml.cs` | 同上；缩放·适应·全屏·画廊·复制（文件/路径/图像）·删除·另存·拖放·图像信息全缺 | 重写 ≈600–800 行，HDR 部分留 M4 |

### 可选（4）

| 文件 | 现状 | 价值 |
| --- | --- | --- |
| `Features/Screenshot/ImageBatchConvertWindow.xaml` | `ScreenshotPage.Linux.cs:581` 内联「批量转 JPG」 | 格式（PNG/AVIF/JXL）、质量、覆盖策略、输出目录、体量对比 |
| `Features/Screenshot/ImageBatchConvertWindow.xaml.cs` | Skia+Magick 已就位（`ImageDecode` / `MagickCodec.TryEncode` / `FileDialogHelper.Linux`） | 同上，≈400–500 行 |
| `Features/Screenshot/ScreenCaptureInfoWindow.xaml` | 桌面通知 `DesktopNotification.Show("截图已保存", ...)` | 对齐上游的滑入 OSD 缩略图 |
| `Features/Screenshot/ScreenCaptureInfoWindow.xaml.cs` | 同上 | 同上，≈250–350 行，需 X11 置顶 + 不抢焦点 |

### 不需要（10）

| 文件 | 理由 |
| --- | --- |
| `Features/GameLauncher/GameNoticeWindow.xaml` | D22/D64：公告改系统浏览器打开 |
| `Features/GameLauncher/GameNoticeWindow.xaml.cs` | 同上；Win32 子窗挂靠 + WebView2 双重无解 |
| `Features/GameLauncher/GameNoticeWindowClosedMessage.cs` | 随窗口消失；红点熄灭已内联（B61） |
| `Features/Screenshot/ImageViewWindow.xaml` | 上游零引用（v2 已取代） |
| `Features/Screenshot/ImageViewWindow.xaml.cs` | 上游零引用 |
| `Features/Screenshot/ScreenshotPage2.xaml` | 上游零引用（未接线改版），且依赖 `ItemsView`/`AnnotatedScrollBar` |
| `Features/Screenshot/ScreenshotPage2.xaml.cs` | 同上；监听/画廊能力移植侧已有 |
| `Features/UrlProtocol/TestUrlProtocolWindow.xaml` | 上游不可达（0.19.0 亦无接线） |
| `Features/UrlProtocol/TestUrlProtocolWindow.xaml.cs` | 同上；要诊断入口应走 `#if DEBUG` 重写 |
| `Features/GameSetting/GenshinHDRLuminanceSettingDialog.xaml` | 已由 `GenshinHDRLuminanceSettingDialog.Linux.cs` 代码重建（D31/D95） |

### 不可移植（0，按「原文件原样搬运」口径）

所有 Win2D / DirectX / Win32 子窗挂靠的项都已在上面被「重写」（必须/可选）或「等价替代」（不需要）吸收：`CanvasSwapChainPanel`、`CanvasBitmap`/`CropEffect`/`ScaleEffect`、`DisplayInformation.AdvancedColorInfo`、`WebView2.CoreWebView2`、`GetDpiForMonitor(Shcore)`、`SetWindowLong(GWL_HWNDPARENT)`、`AppWindow.Show(false)` 在 Linux 均无同名对应。**逐个文件的搬运工作量为 0，重写才有意义。**

### 建议移植顺序

1. **`ImageViewWindow2` → 扩写 `ScreenshotViewerWindow`**（唯一的「必须」）：先缩放/适应窗口/全屏 + 键盘，再右键菜单（复制文件/路径/图像、在文件夹中显示、用默认应用打开、删除），最后另存与拖放打开；HDR 显示模式、显示器亮度信息按 D31 挂 M4。验收：截图页双击、工具箱「图片查看」、`ScreenshotPage.Linux.cs` 的 `OpenItem` 三条路径都落到新窗。
2. **`ImageBatchConvertWindow` → 新 `ImageBatchConvertWindow.Linux`**：`ComboBox`（JPEG/PNG/AVIF/JXL）× `Slider`（质量）× 覆盖策略（跳过/覆盖/重命名）× 输出目录，转换体调 `ImageDecode` + Skia/Magick；把 `ScreenshotPage.xaml:115` 的按钮从「转 JPG」升格为开会话窗；`ScreenshotConverter.ConvertToJpeg` 保留给 CLI/热键。
3. **`ScreenCaptureInfoWindow` → X11 OSD**（可选）：先在 `Helpers/X11Window` 补 `_NET_WM_STATE_ABOVE` 与不抢焦点显示，再用 Skia 出 72×72 缩略图 + 2s 自动隐藏；桌面通知保留作降级。
4. **其余 10 个不动**，但必须同步台账：
   - `docs/linux-port/12-shims-and-gaps.md:178` 的「D134 删掉上游已移除的……」口径要改成「上游 0.19.0 仍存在，其中 X 项判为不需要（零引用/已替代）」；
   - `:257`/`:258`/`:259`/`:260`/`:245` 关于 `ImageBatchConvertWindow`/`ImageViewWindow2`/`ScreenCaptureInfoWindow`/`ScreenshotPage2`/`TestUrlProtocolWindow` 的 `.xaml.disabled` 记载与实物不符（实物已随 D134 删除），要么补回 `.xaml.disabled` 留档，要么把记载改成「已删除（判定：不需要/可选）」；
   - `00-decisions.md` 追加一条 D 记录本次定性，`18-upstream-deviations.md` 同步文件条目。
