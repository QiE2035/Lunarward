# 13 · 移植过程中的经验与坑

> 这里只记「踩过一次就不该再踩」的东西。每条都给出证据位置与处理方式。
> 目的：让后来者（或几个月后的我们）不用重新发现一遍。

- 最后更新：2026-11-02

---

## A · 编译错误分诊（最高频）

### L-01 先分清「找不到类型」还是「找不到成员」

| 错误形态 | 真实原因 | 处理方式 |
|---|---|---|
| `The type '{using:X}Foo' could not be found`（UXAML0001） | `Foo.xaml.cs` 被 `<Compile Remove>` 排除了 | 纳入编译，或写 `Foo.Linux.cs` 分部类补契约 |
| `Unable to find member 'Bar' on type 'PageBase'`（UXAML0001） | 同名页面的 code-behind 没编译，`x:Bind` 找不到成员 | 同上 |
| `Property 'P' does not exist on 'C'`（UXAML0001） | 自定义控件（如 `CachedImage`）缺少上游依赖的属性/事件 | 在控件上补属性，**不要**改 XAML |

> 教训：Uno 报「成员找不到」时，**第一反应不该是写新控件**，而是检查那一页的 code-behind 是否参与编译。

### L-02 `Invalid identifier character -` 且没有文件名

根因：Uno 的 `x:Bind` 解析器不接受**函数实参里的负字面量**。

```xml
<!-- 触发 20 条无文件名错误 -->
{x:Bind local:ShiyuDefensePage.ElementWeaknessVisibility(IceWeakness, -1)}
```

解法：抽成常量 `ElementWeaknessConstants.None` 再传入。

定位手法（当 Uno 不给文件名时）：

```bash
dotnet build -v:n 2>&1 | grep -B4 "Invalid identifier"
```

级联错误里相邻的上下文行会暴露它属于哪个文件。

### L-03 `ControlTemplate is not supported`

上游 `ImageEx` 是 **Control**（有 `DefaultStyleKey` 和模板）。当它被实现成 `Image` 的子类时，
XAML 里的 `ControlTemplate` 无处可挂。重写为 `ImageEx : Control` 并在 `OnApplyTemplate` 里
拿 `Image`/`PlaceholderImage`、用 `VisualStateManager.GoToState` 切状态就解决了。

### L-04 分部类基类必须与 XAML 根元素一致

`GameNoticeWindow.xaml` 派生自 `WindowEx`，而 Linux 分部类写成 `Window` → `CS0263`。
规则：`*.Linux.cs` 里的基类必须逐字照抄 `*.xaml.cs` 的基类。

### L-05 `x:Load` 的元素必须有 `x:Name`

否则 `UXAML0001`。这是 Uno 的硬约束（G0-9 实测）。

---

## B · 契约对齐

### L-06 契约要照抄上游真实代码，不能凭感觉

`GameInstallState` 我第一版按「看起来够用」写了 7 个值，实际上游是 10 个
（`Stop/Waiting/Downloading/Decompressing/Merging/Verifying/Paused/Finish/Error/Queueing`），
`GameInstallDownloadMode` 也不是我猜的 `Auto/MultiThread/SingleThread`，而是
`Undefined/SingleFile/CompressedPackage/Chunk/Patch`（位标志）。结果多返工两轮。

**做法**：写 shim 前先 `cat` 一遍 `src/Starward.RPC/GameInstall/*.cs`，逐字段抄。
类型已存在于别的程序集时（如 `AudioLanguage` 在 `Starward.Core`），**不要重复定义**，否则 `CS0104` 撞名。

### L-07 名字兼容层的边界：补名字可以，补行为不行

`AdvancedSetting`/`HotkeySetting` 用 `Vanara.PInvoke` 的热键常量，补 3 个类型即可，页面源码不动；
但 `ScreenshotSetting` 整页围绕 DXGI 捕获 + HDR 色彩管理，**补名字救不了**，只能回退为排除、M3 重做。

判据：如果缺失的“不只是类型而是一整套管线”，就不要开兼容层。

---

## C · 环境与工具

### L-08 `pkill -f` 会杀掉自己

`pkill -f "login.py"` 匹配到了自己的 shell 命令行（因为命令行里就含这个字符串），把自己杀了。
用 `pkill -x <进程名>` 或先记 PID 再 `kill`。

### L-09 仓库根的 `global.json` 会拦截所有 dotnet 命令

仓库根 `global.json` 钉死 SDK `10.0.400`（`rollForward: latestMajor` 也不够），本机只有 10.0.302，
在仓库根执行任何 `dotnet` 命令都报：

```
A compatible .NET SDK was not found. Requested SDK version: 10.0.400
```

解法：在子目录（`src/Lunarward/`、`poc/`）放各自的 `global.json` 覆盖。**不要改上游那份**。

### L-10 api.nuget.org 在本机下大包会 ~1 MB 处 EOF

`Received an unexpected EOF or 0 bytes from the transport stream`。改用华为云镜像
（`https://repo.huaweicloud.com/repository/nuget/v3/index.json`，实测 12 MB/s），
并且 `NuGet.config` 里 `<clear/>` 后**只留一个源**——多源时 nuget.org 一失败就拖垮整个还原。

### L-11 `Tmds.DBus` 必须 ≥ 0.95.x

≤ 0.21.x 有高危漏洞 `GHSA-xrw6-gwf8-vvr9`，且 API 不同（0.95.2 的 `Connection` 只有
`ConnectAsync`/`Dispose`）。

### L-12 Uno 的 WebView2 在 Linux 需要两个环境变量

```bash
GDK_BACKEND=x11 WEBKIT_DISABLE_DMABUF_RENDERER=1
```

否则刷 `Failed to create GBM buffer of size WxH` 且窗口黑屏（G0-3b）。

---

## D · 设计相关

### L-13 reflink 必须同卷

`cp -a --reflink=auto` 跨文件系统会退化成真拷贝：650 MB 前缀在同一 btrfs 上 0.168 s / 实际增量 4.5 MB，
拷到 tmpfs 的 `/tmp` 则吃掉 385 MB。产品必须在克隆前校验「模板与实例同卷」（W-NO-REFKLINK）。

### L-14 `du` 对 reflink 文件会撒谎

克隆后 `du -sh` 显示 654 MB，`df` 只增加 4.5 MB。校验脚本里要注明，否则会误判为「克隆没生效」。

### L-15 Uno 会拦截 `Segoe Fluent Icons` / `Segoe MDL2 Assets` 这两个族名

无论怎么用 `ms-appx:///` 指定，都会被替换成 Uno 内置字体（G0-7）。自定义图标字体必须改内部
nameID（1/4/6/16/17）成唯一族名。另外 `FontIcon` 控件会忽略 `FontFamily`，隐式 `<Style TargetType="FontIcon">` 也不生效 → D26 改为「替代控件 + 脚本替换」。

---

## 变更记录

| 日期 | 变更 |
|---|---|
| 2026-11-02 | 建立，收录 L-01..L-15 |

## L-16 · `x:Bind` 里的 `\:` 会生成非法 C#

`DailyNoteButton.xaml` 有 `{x:Bind ...ToString('HH\:mm', x:Null)}`。WinUI 能接受，Uno 的生成器直接把它写成 C# 字符串 `"HH\:mm"`，触发 `error CS1009: 无法识别的转义序列`——而且报错指向生成文件，不给 XAML 行号。

- **判定**：生成代码报 `CS1009` 时，去 XAML 里搜反斜杠与 `ToString('...')`。
- **修法**：`.NET` 自定义格式串里 `:` 本来就是时间分隔符，`HH:mm` 与 `HH\:mm` 输出一致，直接去掉反斜杠。
- **普查**：全仓 `ToString('` 共 100 处，只有这一处带转义，其余（`yyyy-MM-dd HH:mm:ss` 等）无风险。

## L-17 · `<Page Remove>` 在 Uno SDK 里不生效

想把某个 XAML 从编译里摘掉时，外层 `<ItemGroup>` 的 `<Page Remove="..."/>` 无效——Uno SDK 在 targets 阶段才收集 `Page` 项，Remove 跑在 Include 之前。

- **可用做法**：把文件改名为 `*.xaml.disabled`（本项目对 `GenshinHDRLuminanceSettingDialog.xaml` 采用此法）。
- **代价**：与上游的差异要登记（见 `12` §4）。

## L-18 · 关了默认 XAML 项之后，App.xaml 要显式声明

本项目为了精确控制「哪些 XAML 参与编译」，设了 `EnableDefaultXamlItems=false` / `EnableDefaultPageItems=false`。副作用是 `App.xaml` 不再自动成为 `ApplicationDefinition`，于是 `App.g.cs` 不生成，报 `CS1061: App 未包含 InitializeComponent`（还会连累 `CS5001: 没有 Main`）。

- **修法**：显式 `<ApplicationDefinition Include="App.xaml" />`。
- **判定**：`App.xaml.cs` 报 `InitializeComponent` 找不到时，先确认 `ApplicationDefinition` 是否存在。

## L-19 · Uno 桌面需要自己写入口点

上游 `Program.cs` 的 `Main` 包在 `#if DISABLE_XAML_GENERATED_MAIN` 里，而且是 WinUI 的 `Application.Start` 路径，在 Uno 上不适用；同时 Uno 也不会替单项目自动生成 Main。

- **修法**：照 Uno 单项目模板写 `Program.cs`（本项目为 `Program.Linux.cs`）：

```csharp
var host = UnoPlatformHostBuilder.Create()
    .App(() => new App())
    .UseX11().UseLinuxFrameBuffer().UseWin32()
    .Build();
host.Run();
```

- **注意**：`OutputType` 用 `Exe`（`WinExe` 在 Linux 上没有意义，且部分工具链不认）。

## L-20 · 增量构建会留下坏状态

出现过一次「无文件名的 `The input is not a valid Base-64 string`」，`-t:Rebuild` 不报、只有增量构建报；删掉 `obj/` `bin/` 后彻底消失。

- **判定**：错误只在增量构建出现、且指向生成文件或干脆没有文件名 → 先 `rm -rf obj bin` 再判定，别去改源码。

## L-21 · 先用上游签名反查真契约，再改自己的 stub

本想把 `UpdateService.GetLatestVersionAsync` 简化成返回 `ReleaseInfo?`，结果 `AboutSetting`、`MainView`、`UpdateWindow` 三处立刻报「无法把 ReleaseInfo 转成 ReleaseInfoDetail」——上游真实签名返回的是 `ReleaseInfoDetail`（按 `{架构}-{安装类型}` 从 `Releases` 里挑出来的明细）。

- **做法**：stub 的签名一律照抄上游；语义可以退化，类型不能。
- **反例代价**：改类型 → 连锁三处；照抄 → 0 处。

## L-22 · `“X”未包含“Y”` 先分清 X 是本页面还是引用的类型

生成代码的 `CS1061` 有两种来源：① `X` 是本页面类 → 本页 code-behind 没编译或成员缺失；② `X` 是别的类型（如 `ReleaseInfo`）→ 是**那个类型**缺成员，或绑定路径原本就指向别的类型。看错误里被引号括起来的类名，比看行号快。

## L-23 · Uno 的 ItemsRepeater 不支持自定义 Layout

`VirtualizingLayout` 在 Uno 里不会调用子类的 `MeasureOverride`/`ArrangeOverride`（实测：派生类编译通过，但布局完全不生效；`NonVirtualizingLayout` 更是连可覆写的方法都没有，编译器报 CS0115）。想覆写 `MeasureOverride(Size, VirtualizingLayoutContext)` 会得到「没有找到适合的方法来重写」。

- **结论**：需要「换行排布」时用 `ItemsControl` + `ItemsPanelTemplate` 里的自写 `WrapPanel`（`: Panel`，覆写 Measure/Arrange 正常生效），不要用 `ItemsRepeater.Layout`。
- **代价**：`SimulatedUniversePage.xaml` 的标签云从 `ItemsRepeater+WrapLayout` 改成 `ItemsControl+WrapPanel`，视觉等价、无损虚拟化（该处数据量小）。

## L-24 · `IsTextTrimmedChanged` 这类 toolkit 附加事件不要硬撑

`CommunityToolkit.WinUI` 只有 net8.0-windows。上游在 9 个 XAML 里用 `IsTextTrimmedChanged`（作用仅是把 ToolTip 设成全文）。我试过自造同名 `IsTextTrimmedChangedEventArgs`：能过类型检查，但生成代码的委托是 `TypedEventHandler<TextBlock, IsTextTrimmedChangedEventArgs>`，与自造类型不是同一个类型 → 一片 CS0123/CS1503。

- **做法**：直接删掉这 9 处 XAML 属性（附注释），ToolTip 退化为「不一定有」，M2 要用再自己实现一个附加事件。
- **原则**：UI 附属品（tooltip/阴影/动画）不值得为它伪造第三方契约；核心契约（`UpdateService`、`GameInstallState`）才必须照抄。

## L-25 · 事件处理器的参数类型要按「委托」而不是「直觉」写

同一批错误里出现了三种：`RoutedEventArgs`、`EventArgs`、`HotkeyInputEventArg`，还有一个 `MenuFlyoutItem.Loading` 的委托是 `TypedEventHandler<FrameworkElement, object>`——第二个参数是 `object`，写成 `RoutedEventArgs` 就报「object 无法转换为 RoutedEventArgs」。

- **做法**：看 CS1503 的**转换方向**——「从 A 到 B」说明委托要 B、你给的是 A。

---

### L-26 · 窗口「黑屏」先查 Map State，再查绘制

- **现象**：`MainWindow` 创建成功、`MainView` 的 ctor/Loaded 都跑到了，截图却全黑；`xdotool windowmap` 手动映射后变成「有窗口但全黑」，误判为渲染问题。
- **实测**：`xwininfo -id <window> | grep "Map State"` 显示 `IsUnMapped` —— 窗口从来没被映射，屏幕上的黑是别的窗口/根窗口。
- **根因**：`MainWindow` 构造函数里 `App.Current.EnsureSystemTray()` → `new SystemTrayWindow()` 卡死（`TaskbarIcon` 还是占位控件），构造函数不返回 → `Activate()` 永远走不到。
- **教训**：
  1. 「黑」分三种：**未映射**（Map State）、**映射了但没画**（抓窗口内容）、**画了但是黑的**（采样像素值区分 #000000 / #202020）。
  2. `Map State: IsViewable` 是判断「窗口到底有没有出来」的第一手证据，比截图可靠。
  3. 副窗口（托盘/悬浮窗）在 M1 不该进入主窗口构造路径，否则一个占位控件就能让主界面永远不显示。

### L-27 · 缺版本号会炸在 HttpClient，而不是编译期

- **现象**：`System.FormatException: The format of value 'Starward.Debug/' is invalid.`，栈底是 `GameSelector..ctor()` → `AppConfig.GetService<T>()`。
- **根因**：`AppConfig.AppVersion` 取自 `AssemblyInformationalVersionAttribute`，新工程没定义版本 → 空串 → `User-Agent: Starward.Debug/` 是非法 header，`HttpClient` 构造即抛。
- **教训**：
  1. 移植新建 csproj 时，**版本号是运行期依赖**，不只是打包元数据。
  2. 这类「构造期炸」的表现是目标页面空白，栈里却看不到目标页面——先看最内层异常类型，再倒推调用链。
  3. 兜底写法：`AppVersion` 为空时回退 `Assembly.GetName().Version`，避免任何环境都炸。

### L-28 · `AttachedDropShadow` 资源套到 `Shadow` 附加属性上会在运行时抛 TypeConverter 异常

- **现象**：`GameLauncherPage` 打开后内容区全空，无未处理异常记录，`MainView_Frame.Navigate` 也不抛。加 try/catch 包住 `Frame.Navigate` 后拿到真因：
  `System.NotSupportedException: TypeConverter cannot convert from CommunityToolkit.WinUI.AttachedDropShadow`，位置在 `PreDownloadButton.InitializeComponent()`。
- **原因**：上游 XAML 用 `<cu:AttachedDropShadow x:Key="TextShadow" .../>` 作为资源，再通过 `cu:Effects.Shadow="{StaticResource TextShadow}"` 套到 TextBlock 上。我方 compat 把 `Effects.ShadowProperty` 的类型声明为 `Microsoft.UI.Xaml.Media.Shadow`，Uno 在应用资源时发现值不是该类型，走 TypeConverter 转换并失败。
- **修法**：兼容层属性类型放宽到 `object`（`RegisterAttached("Shadow", typeof(object), ...)`），XAML 里存什么都可以，不做转换。
- **教训**：写 toolkit 兼容层时，**附加属性的类型要按上游「实际会传进来的对象」放宽**，不要按语义猜一个更具体的类型——Uno 会在资源解析期做类型转换，猜错就炸在页面构造里，且异常被 Frame 吞掉、只表现为「页面空白」。

### L-29 · 游戏页面的两个「当前游戏」不是一回事：`CurrentGameBiz` 决定选中，`SelectedGameBizs` 只是安装列表

- **现象**：设置了 `AppConfig.SelectedGameBizs = "hk4e_cn"` 后，导航栏左上出现游戏图标，但主内容区始终是 `BlankPage`（空）。
- **原因**：`GameSelector` 的恢复逻辑（`GameSelector.xaml.cs:186` 起）用 `AppConfig.SelectedGameBizs` 构建图标列表，但**决定「当前选中」的是 `AppConfig.CurrentGameBiz`**（`GameBiz lastSelectedGameBiz = AppConfig.CurrentGameBiz;`）。只设前者，选择不会被恢复，`CurrentGameChanged` 不触发。
- **修法**：两者都设（`SelectedGameBizs` + `CurrentGameBiz`）。
- **教训**：移植时若「某处状态没生效」，先分清是**列表/持久化字段**还是**当前选择字段**，别只按名字猜。

### L-30 · `Microsoft.Win32.Registry` 在 Linux 上是 `PlatformNotSupportedException`，且异常会被页面吞掉

- **现象**：游戏设置页只剩底部「应用」，主体空白，日志无痕。
- **真因**：`GameSettingService` 直接调 `Microsoft.Win32.Registry.GetValue(...)` → `PlatformNotSupportedException`，被 `InitializeGameSettingAsync` 的 `catch` 记进 Serilog 就结束了。
- **修法**：全部 `Registry.GetValue/SetValue` → `WineRegistry.GetValue/SetValue`（走 `wine reg query/add`）。
- **教训**：上游代码里凡是 `Registry.` 前缀的调用，在 Linux 上都是**运行期炸弹**，编译期发现不了；移植时用 `grep -c "Registry\."` 逐个清点（本项目主工程共 98 处）。

### L-31 · Uno 未实现 `XamlRoot.ContentIslandEnvironment` 与 `DisplayInformation.CreateForWindowId`，调用即抛

- **现象**：`XamlRootExtension.GetAppWindow()` 抛 `NotImplementedException`；HDR 区块抛 `CreateForWindowId(WindowId) is not implemented`。
- **修法**：`GetAppWindow()` 兜底到 `MainWindow.Current.AppWindow`；HDR 区块 try/catch 后置 `HDRNotSupported = true`（界面显示「当前显示器不支持 HDR」，符合 D33「占位要诚实」）。
- **教训**：Uno 的 `NotImplementedException` 消息里带 `aka.platform.uno/notimplemented` 链接，是判断「官方明确没做」而不是「用法错了」的可靠信号。

## L-32 外部登录助手的两个坑

1. **助手参数协议必须和调用方对齐**：`login.py` 最初只认 `sys.argv[1]` 当 URL，而 `WebLoginService` 传的是 `--url/--output`，结果 WebKit 把字符串 `--url` 当地址加载，界面显示「该 URL 无法显示」——看起来像网络问题，其实是参数没解析。改造后统一用 `argparse`。
2. **WebKitGTK 在部分环境要关 DMABUF 渲染**：不设 `WEBKIT_DISABLE_DMABUF_RENDERER=1` 会刷 `Failed to create GBM buffer of size 1100x760` 并黑屏；Wayland 会话下再加 `GDK_BACKEND=x11`（L-12 同源问题）。`WebLoginService` 在拉起助手时统一注入这两个环境变量。

## L-33 行为（Behavior）兼容层必须真的 Attach，否则整类功能静默失效

- 现象：`InAppToast` 从不出现、`InAppToast.MainWindow` 永远是 null，日志里没有任何异常。受影响的不是一处 UI，而是**全应用的应用内提示**（成功/警告/错误都在用）。
- 根因：`Microsoft.Xaml.Behaviors.WinUI.Managed` 只有 `net8.0-windows`，M1 自写了 `Controls/InteractivityCompat.cs` 的 `Interaction`；但集合只在列表里存对象，**没人调用 `Behavior.Attach`**，于是 `OnAttached` 从不执行，`Tag == nameof(MainWindow)` 的分支也就不会注册静态实例。
- 修法：`Interaction.Get/SetBehaviors` 返回带宿主的 `BehaviorCollection`，`Add/Insert` 时 `Attach(owner)`、`Remove/Clear` 时 `Detach()`；`SetBehaviors` 收到普通列表时转换并逐项 Attach（XAML 集合语法可能先构造列表再整体赋值）。
- 教训：写控件/行为兼容层时，**「编译通过 + 不报错」不等于「接线了」**。凡是回调式扩展点（Behavior、AttachedProperty、事件订阅），必须用一条可见效果验证，而不是看构建结果。

## L-34 星号行里的固定高度块会把列表压成一行

- 现象：设置 → 运行环境 的 Profile 卡片只显示标题，摘要与按钮不可见；同一页的 `InfoBar` 正常。
- 原因：`Grid` 的行高是 `Auto/Auto/*`，末尾 `Auto` 的 `InfoBar` 占去了整块剩余高度，`*` 行只剩约 44px，卡片被裁成一行。
- 修法：把占空间的 `InfoBar` 从布局里去掉，改成列表内的一行说明文字；卡片改单列纵向排布（标题 → 摘要 → 按钮），不再用「星号列 + 右侧按钮」的多列 Grid——Uno 在窄容器里会把星号列压成 0，按钮直接消失。
- 教训：调试「内容不见」时先怀疑**容器尺寸**而不是内容本身；`Width` 固定值可以快速证伪「星号列塌陷」。

## L-35 `AppWindow.MoveAndResize` 在 Uno 的 X11 后端是空操作

- 现象：主窗口一直是 1024x640，`CenterInScreen(1200, 676)` 明明执行了；`AppWindow.Size` 也仍然报 1024x640。
- 原因：Uno（6.7.135，`net10.0-desktop` + X11）里 `MoveAndResize` 不落到 X11 窗口，`Resize` 才真正生效；`AppWindow.Size` 的读回同样不刷新。
- 修法：`WindowEx.Linux.CenterInScreen` 先 `AppWindow.Resize(new SizeInt32 { Width = w, Height = h })`（注意 `SizeInt32` 没有两参构造），再调 `MoveAndResize` 定位。
- 验证：`xdotool getwindowgeometry` 才是真相，别信 `AppWindow.Size`（它滞后一拍）。
- **尺寸是物理像素，不是 DIP**：本机 `RasterizationScale = 1.25`，`AppWindow.Resize(1200, 676)` 得到的是 1200x676 物理像素，内容区只有 960x540 DIP，于是所有内容看起来「大了 25%、很挤」。正确做法：`Resize(1200*scale, 676*scale)`，内容区才是 1200x676 DIP（=Starward 的设计尺寸）。
- 时序陷阱：构造函数里 `Content?.XamlRoot?.RasterizationScale` 还是 null（拿到 1），必须等窗口**首次 Activated** 后再换算，否则又会退回 1.0。

## L-36 XWayland 下 `xdotool` 的点击到不了应用窗口

- 现象：`xdotool mousemove X Y click 1` 对准按钮点，控件毫无反应；`windowactivate --sync`、`windowraise`、`ydotool` 也无效。
- 结论：本机（KDE Wayland + XWayland）无法用 xdotool 验证点击类交互，别把「点了没反应」当成代码 bug。
- 替代：给关键动作加环境变量钩子（如 `LUNARWARD_WINDOW_ACTION=minimize`）在启动后自动执行一次，用 `xwininfo -id <id> | grep "Map State"`（`IsUnMapped` = 最小化成功）来判定。

## L-37 上游 `Grid.Row="1"` 在无行定义的 Grid 里是「被夹到第 0 行」的

- 现象：给 `GameSettingPage` 的根 Grid 补了 `RowDefinitions` 之后，右侧「图形设置」列与新增的「后启动程序」分区叠在一起。
- 原因：上游 XAML 里右列写的是 `Grid.Row="1"`，但那个 Grid 只有隐式一行，WinUI/Uno 会把越界的行号夹到第 0 行，所以一直"看着正常"。一旦补了第二行，它就真的跑到第 1 行去了。
- 修法：给该处显式去掉 `Grid.Row`（或写 `Grid.Row="0"`），并在 XAML 里留注释说明原因；新增分区用 `Grid.Row="1"` 才安全。
- 教训：给上游布局加行列定义前，先 grep 该 Grid 里所有 `Grid.Row`/`Grid.Column`，越界值会在地基改变后暴露出来。

## L-38 同一份组件经常以两个路径出现（符号链接），去重要用真实路径

- 现象：组件列表里 `Proton - Experimental` 出现两次，一条 `~/.local/share/Steam/...`、一条 `~/.steam/steam/...`。
- 原因：`~/.steam/steam` 是符号链接，`Path.GetFullPath` 不会解析链接，两条字符串自然不相等。
- 修法：`File.ResolveLinkTarget(path, returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path)` 作为去重键（`ComponentStore.RealPath`）。
- 教训：凡是把「扫描到的文件」当身份的地方（去重、缓存键、写入记录），都用解析后的真实路径。

## L-39 wine 拒绝在 `/tmp` 下建前缀：前缀必须在「自己拥有的目录」里

- 现象：`wineboot -u` 报 `wine: '/tmp' is not owned by you, refusing to create a configuration directory there`，退出码 1。
- 原因：wine 检查前缀**父目录**的所有者与权限，`/tmp`（sticky、root 所有）不满足。
- 规矩：前缀一律放在 `$XDG_DATA_HOME/lunarward/prefix/` 或用户拥有的路径下；测试前缀也别用 `/tmp`，用 `~/...` 下的临时目录。
- 另一个数字：`wineboot -u` 首次初始化约 **18 s**、落盘 **630 MB**（Proton Experimental 的 wine），所以 dedicated 模式的首次启动要有等待提示；template 克隆只要 ~100 ms。

## L-40 「能启动」的判据要能被命令行复现

- 现象：`StartGameAsync` 里的预检依赖 `AppConfig`（SQLite 里的安装路径），CLI 直接调时拿到空值，预检误报「找不到 exe」。
- 做法：`--preflight <gameKey> [exePath]` 允许显式传 exe；CLI 先用 `AppConfig.LoadConfiguration()`，仍拿不到安装路径时按 `~/games/<biz>` 猜一个只用于排错。
- 教训：任何「启动前判断」都要有不开图形界面的复现入口，否则真出问题时只能靠读日志猜。

## L-41 上游 XAML 没被 InitializeComponent 时，控件是「空壳」

- 现象：`AppBackground` 的壁纸始终不显示，日志也看不到异常；排查发现我们的 `AppBackground.Linux.cs` 构造函数从没调 `InitializeComponent()`（上游代码隐藏被排除），所以 `UserControl.Content` 是 `null`。
- 连带坑：即便调了 `InitializeComponent()`，XAML 里的 `ImageEx` 也是**没有 ControlTemplate 的 Control**（`Microsoft.Xaml.Behaviors`/`CommunityToolkit` 那类只能靠自建模板），照样不显示。
- 做法：不硬套上游 XAML，直接在代码里给 `Content` 一个 `Image`，把 `BackgroundImageSource` 映射到 `Image.Source`；在 `12-shims-and-gaps.md` 登记「视频壁纸/切换手势未接」。
- 教训：**「控件在、属性在、编译过」不等于「有视觉树」**。碰到某块区域全空，先确认 `InitializeComponent()` 是否被调用过。

## L-42 用日志验证状态，不要用截图猜

- 现象：截图里「前缀模式」下拉看着停在 `template — 模板克隆（默认）`，我据此以为 Uno 丢失了 `SelectedIndex` 并加了一段补丁。
- 真相：加临时日志后发现 `dialog.Opened` 时 `SelectedIndex` 本来就是 3（`external`），图层里显示的字段是「前缀路径」而不是「模板路径」——**是我把截图读错了**。
- 做法：任何「控件状态不对」的判断，先在事件里把关键值写进 `/tmp/*.log` 再动手；补丁若无法证明修复了什么，就删掉（免得留下没人能解释的代码）。

## L-43 「壁纸显示出来了」不等于「能看」

- 现象：接上静态壁纸后截图里文字几乎不可读——默认兜底图 `UI_CutScene_1130320101A.png` 是一张明亮的过场 CG，而 Windows 上它会被系统亚克力模糊+压暗。
- 处理：没有模糊就别硬抄观感。壁纸层做成「深色底 + `Image.Opacity=0.42`」，导航栏用半透明亚克力画刷；可读性优先于「壁纸原样可见」。
- 教训：跨平台移植视觉时，**保真对象是信息层次**（哪块暗、哪块亮、文字在哪），不是某个具体特效有没有实现。

## L-44 两个「静默降级」各自埋了一颗雷

**一、Profile 指定的运行器不存在时不要回退。**
预检原先写的是「Profile 有 RunnerPath 且文件存在就用它，否则 `ResolveWine()`」，于是把 Profile 指到一个不存在的路径时，预检照样通过、实际用了自动探测到的 Proton。用户以为在验证 A，其实跑的是 B。
修法：Profile 显式指定了运行器就只认它，不存在直接判 Failed（`该配置指定的运行器不存在`），并在修法里告诉用户「清空路径即回到自动探测」。CLI 复现：`lunarward --preflight <gameKey> <exe>`。

**二、直接调 Proton 的 `files/bin/wine` 必须自带 Proton 环境。**
Proton 的启动器（`proton` python 脚本）会构造 `WINEDLLPATH`、`LD_LIBRARY_PATH`、`STEAM_COMPAT_CLIENT_INSTALL_PATH` 等环境；绕过它直接 exec `files/bin/wine` 时这些是空的，wine 找不到 Proton 自带的 builtin dll 与库，游戏表现常常是「启动了、几秒后没了」，且日志里什么都看不出来。
修法：`GameLauncherService.Linux.ApplyWineRuntimeEnvironment(info, winePath)` 从路径反推并补齐（`files/lib/wine` 或旧布局 `files/lib64/wine`；`files/lib/{x86_64,i386}-linux-gnu`；`STEAM_COMPAT_CLIENT_INSTALL_PATH`=steam 根；`PROTONPATH`）。只补环境变量，不引入 Proton 启动器，保住「游戏是 wine 首个进程」。
自检：`lunarward --runner-check <winePath>` —— 跑 `wine --version` 与 `wine cmd /c echo`，不启动游戏。实测 Proton Experimental：`wine-11.0` + `lunarward-ok`（`ntsync: up and running`）。

## L-45 · Xlib 不能随便在别的线程里用；窗口恢复交给 xdotool

**现象**：给全局热键加「唤回主窗口」时，先试 `OverlappedPresenter.Restore()` / `AppWindow.Show()` / `AppWindow.Hide()`——全部是空操作（Uno 6.7.135 X11 后端，窗口 `Map State` 一直是 `IsUnMapped`）；再试在计时器线程里直接调 Xlib（`XQueryTree` + `XFetchName` + `XMapRaised` 或 `XSendEvent(_NET_ACTIVE_WINDOW)`），进程直接 `free(): invalid pointer` / `X Error: 9 Bad Drawable` 而 abort。

**原因**：Xlib 不是线程安全的。我们自己 `XOpenDisplay` 的连接在计时器线程上跑，但 Xlib 的全局状态（display 列表、错误处理器）与 GTK 共用；`XQueryKeymap` 这种只读、不分配内存的调用侥幸没事，一旦涉及 `XQueryTree`/`XFetchName`（会 malloc/free）就撞堆。装 `XSetErrorHandler` 更糟——它是**进程级全局**的，改掉等于把 GTK 的错误处理也换了。

**做法**：
1. 窗口恢复改用**子进程** `xdotool search --name ^Starward$` + `xdotool windowactivate --sync <id>`（`X11KeyState.ActivateWindowWithXdotool`），xdotool 自己维护连接，崩溃隔离在子进程；没有 xdotool 时返回 false，调用方退回 `Window.Activate()`。
2. 热键检测保留 `XQueryKeymap` 轮询（25ms），只读不分配。
3. 已知限制：`xdotool key alt+s`（约 12ms 的合成按压）抓不到，人类按键通常 >100ms 所以可接受；要更灵敏需改 `XGrabKey` + 事件循环。
4. `AppWindow.Id`/`WindowHandle` 在 Uno Linux 上分别是 `1`/`0`，拿不到真实 X 窗口 id。

**教训**：跨线程碰 Xlib 前先问「这段代码会不会 malloc」；能不碰就不碰，用子进程或官方 API；错误处理器这类全局钩子在任何 GUI 框架里都不要动。

## L-46 · 游戏时长统计：Linux 上被 `ProcessName` 挡住

**现象**：真机启动日志出现 `Game process ("hk4e_cn", 323281) is not the expected process ("wine")`，之后游玩时长一条都没记。

**原因**：`PlayTimeRecordService.StartProcessToLogAsync` 会拿 `process.ProcessName` 和游戏 exe 名（`YuanShen` 等）比对，防止误记录。Linux 上我们启动的是 `wine`，进程名自然是 `wine`，于是被自己挡掉、直接 `return`。

**做法**：Windows 保持严格校验；Linux 接受 `wine`/`wine64`/`wine-preloader`（真正跑游戏的子进程由 wine 管理，按启动器进程计时即可），其它名字只记警告不返回。

**验证**：`Lunarward playtime --biz hk4e_cn --pid <pid>` 对着一个 `sleep 10` 跑，进程退出后日志出现
`Save play time stats: GameBiz "hk4e_cn", Pid …, StartTime …, EndTime …, Interruption False`。

**教训**：移植时凡是「名称/路径/进程名比对」的分支都要重新问一次「这条在 Linux 还成立吗」——它们是隐式的平台假设，编译器不会报错。

## L-47 · 重定向了 stdout 就必须立刻有人读

**现象**：给「游戏/后启动程序输出捕获」打开 `RedirectStandardOutput` 后，CLI 排错路径里始终拿不到任何输出。

**原因**：`RedirectStandardOutput = true` 只是把管道接出来，没有任何人读的话，写满 64KB 后子进程会被**阻塞**（游戏卡死）。另外，命令行工具启动完子进程就 `return`，进程退出会连带杀掉异步读取线程，输出丢失。

**做法**：① `Process.Start` 之后**立刻** `OutputCapture.Attach(...)`（`BeginOutputReadLine`），不要等业务逻辑；② 排错 CLI 在启动后 `WaitForExit(3000)` 再退出；③ 捕获同时落盘，`--output-tail` 读文件而不是读内存队列——跨进程才看得见。

**教训**：开关一个 `Redirect*` 就等于给自己加了一个「必须持续消费」的义务；不做消费的重定向比不重定向更危险。

## L-48 · Uno 的分组 GridView 在图库场景下不渲染条目

**目标**：把 `~/Pictures/Lunarward` 里的截图列成上游那种「按日期分组」的网格。

**试过的三种写法**（都基于上游 `ScreenshotPage.xaml` 原封不动的模板）：
1. `CollectionViewSource { IsSourceGrouped = true, Source = List<ScreenshotItemGroup> }` → 区域空白，无异常、无绑定错误；
2. `IsSourceGrouped = false` + 扁平 `List<ScreenshotItem>` → 仍然空白；
3. 顺便把 `CachedImage` 的 `file://` 改为「自己读字节再 `SetSourceAsync`」——`GridView` 依旧不显示。

**结论**：不是数据问题（构造函数日志确认 `groups=2`，条目对象齐全），而是 Uno 在「`GridView` + `GroupStyle` + `ItemsWrapGrid`」这条组合上不工作。要做出图库，需要**给 Linux 版单独写一个轻量列表控件**（例如 `ItemsRepeater` + `WrapLayout` 或 `ItemsControl` + `WrapPanel`），这会偏离上游 XAML 结构，属于「需要先确认」的改动，不是顺手能做的。

**处理（用户拍板「改这一个页面的列表实现」后）**：把该页的 `GridView`+`GroupStyle`+`ItemsWrapGrid` 整段换成 `ScrollViewer` + `ItemsControl` + `tk:WrapPanel`（`ScreenshotPage.xaml` 内替换，条目模板仍是缩略图 + 文件名的上游观感），数据源改成扁平 `List<ScreenshotItem>`。**验证通过**：两张截图并排显示、文件名在缩略图下方、整页与上游观感一致。代价：日期分组标题、多选与批量转换暂时没有（登记为 M3 后续）。

**教训**：Uno 对 WinUI 的「分组/虚拟化容器」支持是有洞的；先用最小数据验证控件本身能不能渲染，再投入数据层——否则会在数据与绑定上白花时间。分组渲染不行时，「换容器」比「修数据」便宜得多。

**教训**：Uno 对 WinUI 的「分组/虚拟化容器」支持是有洞的；先用最小数据验证控件本身能不能渲染，再投入数据层——否则会在数据与绑定上白花时间。

## L-49 · 用 ItemsControl 做多选列表时，选中状态要挂在条目模型上

`ScreenshotPage` 换成 `ItemsControl` + `WrapPanel` 后就没有 `SelectionMode` 了（那是 `ListView`/`GridView` 的能力）。多选的可行做法：
1. 条目模型加 `IsSelected`（`ScreenshotItem` 是 `partial`，用 `Features/Screenshot/ScreenshotItem.Selection.cs` 扩展，不动上游文件）；
2. 模板里放 `CheckBox IsChecked="{x:Bind IsSelected, Mode=TwoWay}"`；
3. 计数文案（「已选 M / N 张」）在 `LoadImages` 时给每个条目挂 `PropertyChanged`，变化时重算——比在 UI 线程轮询简单，也不需要把页面变成严格 MVVM。

批量转换本身是纯逻辑，抽成 `Features/Screenshot/ScreenshotConverter.ConvertToJpeg(IEnumerable<string>, quality)`，页面与 CLI 共用；这样「点按钮」和「命令行验证」走同一条代码路径，不会出现只有 UI 能用、CI 验不了的情况。

## L-50 · Uno.Sdk 不生成版本属性，`AppConfig.AppVersion` 会读到 0.0.0.0

**现象**：「关于」页显示 `0.1.0-linux` 之前是 `0.0.0.0`；日志 `Welcome to Starward v0.0.0.0`。`Lunarward.csproj` 里明明写了 `<Version>` 与 `<InformationalVersion>`。

**原因**：Uno.Sdk 项目下自动生成的 `obj/Debug/net10.0-desktop/Lunarward.AssemblyInfo.cs` **只有** `TargetPlatform`/`SupportedOSPlatform` 两条特性，没有 `AssemblyVersion`/`AssemblyInformationalVersion`——所以 `GetCustomAttribute<AssemblyInformationalVersionAttribute>()` 返回 null，代码落到兜底 `Assembly.GetName().Version`（也是 0.0.0.0）。

**做法**：新增 `Properties/AssemblyVersion.cs` 显式声明三条：
```csharp
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0-linux")]
```
验证：日志变 `Welcome to Starward v0.1.0-linux`，关于页显示「当前版本：0.1.0-linux」（截图 `docs/linux-port/screenshots/m2-about.png`）。

**教训**：`<Version>` 之类的 MSBuild 属性只在默认的 `GenerateAssemblyInfo` 流程里生效；Uno/特殊 SDK 可能接管这步，出问题先看 `obj/**/AssemblyInfo.cs` 里到底生成了什么。

## L-51 · LibVLCSharp 的 vmem 回调：地址要写进 planes[0]，不是返回值

**目标**：把视频帧拿到托管侧做壁纸。

**坑 1（段错误）**：`SetVideoCallbacks(lock, unlock, display)` 的 `lock` 签名是 `void lock(void* opaque, void** planes)`——libvlc 给的是一个**指针数组**，回调必须把缓冲区地址写进 `planes[0]`。我一开始按 `IntPtr lock(...)` 返回地址，libvlc 照样往 `planes[0]`（null）写，直接 SIGSEGV。

**坑 2（跨边界异常）**：`display` 回调里 `new SKImageInfo(..., SKColorSpace.CreateSrgb())` 抛异常 → 异常穿过原生栈帧 → `core dumped`。回调里必须整段 `try/catch`。

**坑 3（生命周期）**：固定缓冲区在播放期间只能做一次；结束要先 `player.Stop()` 再 `GCHandle.Free()`，否则 libvlc 的线程还在写。

**可用组合**：`vlc 3.0.24` + `LibVLCSharp 3.9.4` + `SkiaSharp 3.119.0` + `SkiaSharp.NativeAssets.Linux.NoDependencies`（缺这个包时 `SKColorSpace` 的类型初始化会炸）。

## L-52 · Uno 已经传递依赖 LibVLCSharp，别再自己引一遍

`Uno.WinUI.Runtime.Skia.X11 6.7.135` 的依赖里就有 `LibVLCSharp (>= 3.9.7.1)`（`MediaPlayerElement` 用）。我一开始在项目里显式加 `LibVLCSharp 3.9.4`，直接 `NU1605` 包降级失败。正确做法：**不引**，直接用传递依赖的版本；原生库 `libvlc` 由系统提供（Arch 装 `vlc`），缺失时 `LibVLC.Core.Initialize()` 抛异常，按 D33 诚实降级为静态壁纸。

另外：`Core.Initialize()` 会被 `Starward.Core` 命名空间遮住，必须写 `LibVLCSharp.Shared.Core.Initialize()`。


## L-53 · 上传/发布类资源常被 `.gitignore` 的宽规则误伤

- **现象**：`pkexec`/`makepkg` 从 git 克隆构建时报 `MSB3030 无法复制 Assets/lunarward.png`，而本地 `dotnet build` 一直是好的。
- **根因**：上游 `.gitignore` 第 220 行有一条 `Assets/`，会把**任何**名为 `Assets` 的目录整体忽略，包括我们新建的 `src/Lunarward/Assets/`（logo、登录助手脚本）。本地文件在磁盘上所以构建正常，但从未进过版本库，克隆出来的树里就没有。
- **判定**：`git ls-tree -r HEAD --name-only <目录>` 为空、`git check-ignore -v <文件>` 指向该规则，两条一起看即可确认。
- **做法**：`git add -f` 强制跟踪这两个文件（不改上游 `.gitignore`，减少后续 cherry-pick 冲突）；新增资源时先确认 `git ls-files` 里能看到。
- **延伸**：`makepkg` 构建要能过，意味着「从干净克隆能否构建」必须作为提交前的检查项，而不是只测当前工作区。


## L-54 · `BitmapImage.SetSourceAsync` 的续体不在 UI 线程

- **现象**：自研截图查看器里图片区域全白，`Image.Source` 看起来也设了，日志无异常。
- **根因**：`SetSourceAsync` 的 `await` 续体跑在线程池上，此时给 `Image.Source` 赋值不会更新合成树（Uno 不报错，也不渲染）。
- **做法**：`DispatcherQueue.TryEnqueue(() => Image_Preview.Source = bitmap);`，把赋值放回 UI 线程。
- **附带教训**：「看起来是白图」也可能真的是白图——测试素材要选有内容的，否则会把素材问题误判成渲染问题（本次先浪费了两轮截图）。

## L-55 · Linux 上「游戏进程」不等于我们拿到的 PID

`ProcessStartInfo("wine", ["game.exe"])` 返回的子进程是 **wine loader**，它在启动游戏后很快 re-exec 掉自己：
`Process.GetProcessById(pid)` 一秒后就抛 `ArgumentException: Process with an Id of … is not running`。

- 会话结束的正确判据是 **`wineserver -w`**（等该前缀的 wineserver 退出）；没有 wineserver 才回退到「PID + /proc 子孙」轮询。
- 调 wineserver 时必须显式传 `WINEPREFIX`；CLI 子命令里 `PlayTimeSession.ActivePrefix` 可能为空，要用 `ResolvePrefix(gameKey)` 兜底，否则会立刻判定「会话已结束」。
- 记录开始时间时不能只靠 `process.StartTime`：进程已消失就回退 `PlayTimeItem` 里最近一次 `Start` 事件。
- 启动器可能被杀，因此每 30 秒写一次 `Interruption=true` 的心跳行；正常退出时用 `Interruption=false` 覆盖。

## L-56 · Sophon 安装链路：清单是「zstd 压缩的 protobuf」

- 清单不是 JSON：`manifest_download.url_prefix/<manifest.id>` 拿到的是 **zstd 压缩的 protobuf**，先解压再解析，最后用 `checksum` 校验**解压后**的 MD5。
- 只解析我们需要的 schema（`Sophon.proto`）就够了，手写一个两百行的 protobuf 读取器比引 Grpc.Tools（protoc + gRPC 运行时）更划算；遇到未知字段必须跳过而不是抛异常，否则米哈游加字段就装不了。
- 下载单元是 chunk：`chunk_download.url_prefix/<chunk.id>`，内容是 **zstd 压缩**的，解压后按 `offset` 写进文件；chunk 与整文件都要校验 MD5。
- 判断「要不要下」先用**文件大小**，MD5 只在用户显式要求或修复时才算：一个完整原神是 130+ GB，规划时全量 MD5 会让界面卡几分钟。
- 同国服/国际服共用 exe 名一样，清单里的 `matching_field` 也有 `game` 与语音包（`zh-cn`/`en-us`/`ja-jp`/`ko-kr`）之分；只比本地 `Audio_*_pkg_version` 存在的语音包，否则会凭空多下几十 GB。
- CLI 验证路径：`--install-plan <biz>`（只算）→ `--install-fetch <biz> <file>`（单文件，带备份）→ `--install-run <biz> --target <dir> --limit-mb N`（受控批量，下载到指定目录，不碰真实安装）。

## L-57 · Sophon 下载别手写，用 `Hi3Helper.Sophon`

- **背景**：我先把 Sophon 的清单解析（手写 protobuf 读取器）、分块下载、`hpatchz` 子进程差分都自己写了一遍并跑通；随后对比了 `Hi3Helper.Sophon`（Collapse Launcher 的生产依赖）——它把三件事都做了，而且是纯托管（`SharpHDiffPatch.Core` + `Hi3Helper.ZstdNet`），不需要用户装 `hdiffpatch-bin`。
- **结论**：`Starward.Core` 只负责「launcherId / package / password / 版本」，清单、下载、差分全部交给 `Hi3Helper.Sophon 1.7.1`。手写的 `SophonDownloader` / `SophonPatcher` / 补丁清单解析器已删除。
- **交叉验证**：库枚举出的 `game` 清单与手写解析完全一致（原神 2744 文件 / 122.92 GB）；`getPatchBuild` 按本地 7.0.0 选出、并经 `EnsureOnlyGetDedupPatchAssets` 去重后是 **90 条目 / 9.32 GB**，与官方 `stats` 里的 9.32 GB 对上。
- **坑 1（必须处理）**：`Hi3Helper.ZstdNet` 不按常规 `DllImport` 探测，而是固定去 `<程序目录>/Lib/linux-x64/libzstd{,-bmi2}.so` 找库；用自己的 `DllImportResolver` 去拦会撞上它自带的解析器（`A resolver is already set for the assembly`）。做法：构建/发布时建这两个符号链接指向 `/usr/lib/libzstd.so.1`（csproj 里 `LunarwardZstdShim` 目标），打包声明依赖 `zstd`。
- **坑 2**：`SophonAsset.WriteToStreamAsync` 会在写完后回读流做分块校验，输出流必须 `FileAccess.ReadWrite`，否则 `NotSupportedException: Output stream must be readable!`。
- **坑 3**：`SophonPatch.EnumerateUpdateAsync` 返回的是**全部**新旧条目，必须再走 `SophonPatch.EnsureOnlyGetDedupPatchAssets(...)`，否则会把 79 GB 当成本次要下载的量。
- **未验证**：真正的差分**应用**需要「旧版本文件 + 新版本哈希」，本机两个游戏都已是最新版，只能在下次实际更新时验证；在此之前 `--patch-run` 默认只读，必须显式 `--apply-to-install` 才会写安装目录。

## L-58 · `Audio_*_pkg_version` 不是版本号，是文件清单

- **踩到的坑**：我按名字以为 `~/games/hk4e_cn/Audio_Chinese_pkg_version` 存的是「语音包版本号」，在版本标记里准备把它写成目标版本；实际打开是 **JSON lines 清单**（173 行，每行 `{"remoteName": ..., "md5": ..., "hash": ..., "fileSize": ...}`），它本身就是随更新下发的数据文件。
- **正确的版本标记只有一处**：`config.ini` 的 `game_version=`（原神 `config.ini:4`）。写入时只替换这一行、其余键（`channel`/`cps`/`sub_channel`）原样保留，避免把渠道信息写坏。
- **顺手修掉的隐患**：`--install-run` 只在「文件全部下载成功 **且** 输出目录就是安装目录」时才写版本标记；失败或写到临时目录一律不写，免得出现「标记说最新、文件却是旧的」。

## L-59 · 预下载不是「换 branch」，是同一个 API 换 package

- **失败的写法**：我以为预下载就是 `getBuild?branch=pre_download`，于是自己拼 `branch=pre_download` + `tag=<预下载版本>`，库返回 `code=-202`（清单无效），随后 `SophonManifest.EnumerateAsync` 直接抛 `NullReferenceException`，看不出是清单没取到。
- **正确写法**（照上游 `GamePackageService`）：`branch` 取 `package.Branch`（预下载时它自己就是 `predownload`），`tag` **留空**表示「该 branch 的最新版本」。即 `getBuild?branch=predownload&package_id=…&password=…&tag=`。
- **加了一层防御**：`InstallPipeline` 在枚举前检查 `pair.IsFound`，不可用时抛可读异常（带 field/branch/tag/code），不再让库的 NRE 冒上来。
- **实测**：`--install-task hk4e_cn <dir> --predownload` → 状态 `Waiting → Downloading`，`进度 0.2% / 已下 252 MB / 126.51 GB / 60 MB/s / 剩余 2199s`，8 秒后主动 Stop 正常。
- **教训**：库的错误语义依赖上游调用约定，别自己发明参数；先抄一遍上游的真实调用再改。

### L-61 · `getBuild` 的响应包了一层 `data`

- **症状**：预下载对话框三行大小永远为空，`--predownload-size` 打印「没有预下载清单」，但直接 `curl` 同一个 URL 能看到 `manifests`。
- **根因**：`https://downloader-api.mihoyo.com/downloader/sophon_chunk/api/getBuild?...` 返回 `{"retcode":0,"message":"OK","data":{"build_id":…,"manifests":[…]}}`。上游 `HoYoPlayClient` 走 `CommonGetAsync(url, "data")` 自动拆层；我后来为了绕开预下载 `-202` 改成自己拼 URL，却仍把**整个响应**反序列化成 `GameSophonChunkBuild` → `Manifests` 为 null → 过滤后 `manifests.Count == 0` → 静默返回 null，看起来像「官方没开预下载」。
- **判据**：解析失败要**抛出可读异常**，不要返回 null；「清单为空」和「没有这个分支」是两回事。
- **附带 1**：预下载的 `tag` 必须留空（`branch=predownload&tag=`），带上版本号会返回 `retcode=-202`。两件事叠加起来，才会出现「用户说有预下载、程序说没有」。
- **附带 2（体积）**：`getBuild` 返回的是**完整游戏**清单（hk4e_cn 7.2.0 = 136 GB），把它当「预下载大小」会吓人。真实预下载量在 `getPatchBuild`（**必须 POST**，GET 返回 405）的 `manifests[].stats[本地版本].compressed_size`：7.1.0 → 7.2.0 是 game 7.90 GB + zh-cn 0.44 GB ≈ **8.34 GB**。先看 stats，没有再退回整包体积。

### L-62 · chunk 复用的三个坑：解压尺寸、原位覆盖、句柄开销

- **解压尺寸**：`ZstdSharp.Decompressor.Unwrap(src, dstSize)` 会把 `dstSize` 当上限。清单里个别条目的 `uncompressed_size` 与实际不符（实测 `APMConfig.json` 声明 105 字节、实际 1024），带上限直接 `ZstdException: Decompressed content size 1024 is greater than maxDecompressedSize 105`。改成不限长解压、解压后再比长度 + MD5。
- **原位覆盖**：更新是往旧安装目录里写；被替换过的文件块布局变了，只按 offset 从旧文件读会拿到**新内容的字节**。所以复用的每一块都要校验 `uncompressed_md5`，不符就当「没有这块」回退下载——这条是敢在原位目录测试的前提。
- **句柄开销**：一个文件几千块，每块 `new FileStream` 打开/关闭会让 60 个文件的测试从 60 s 涨到 169 s。按文件路径缓存句柄（容量 32）后恢复。
- **并发**：chunk 下载串行只有 ~5 MB/s（每个块一个小 GET）。滑动窗口 8 并发 + 顺序写盘，同样数据 60 s 跑完（~14 MB/s），MD5 依然逐文件校验。

### L-63 · 「限速/限量」要把上一阶段已下的字节算进去

- **症状**：`--update-run --limit-mb 200` 打完 274 MB 补丁后，chunk 复用仍继续下，因为剩余额度只减了 `downloaded + reused`，漏掉了 `patchedBytes`。
- **修法**：预算判断统一用「本阶段累计下载量」——补丁、复用各自累加后一起参与比较；给下载器的额度也用 `limit - 已下`，而不是恒等 `long.MaxValue`。
- **教训**：分阶段流水线里，任何「总量/剩余量」都要有个单一累计点，别每段各算一套。

### L-64 · 顺序写盘是断点续传最便宜的「日志」

- **做法**：chunk 下载本来就是按块顺序写临时文件，于是**文件长度天然等于「完成了几个块」**，不需要额外的 journal 文件。重跑时按清单里前 k 块的累计长度匹配现有长度即可定位续传点。
- **必须处理的边界**：长度可能落在「块中间」（崩溃时写了半块）。这时把临时文件截断到上一个块边界，重写那半块。
- **必须有的兜底**：续传前缀毕竟来自「上次可能崩在写盘中途」的文件，整文件 MD5 不符时丢掉临时文件从头再来一次；不要相信「上次写的一定对」。
- **清单变了怎么办**：块边界对不上 → 匹配到的 k=0，自动退化为整文件重下，不会写坏文件。

### L-65 · 「更新 + Chunk 模式」的进度环读的是写入量

- **症状**：界面上更新进度环（`StartGameButton` 里的圆环）一直是 0，百分比标签显示 `NaN%`。
- **根因**：上游 `StartGameButton.xaml.cs` / `PreDownloadButton.xaml.cs` 在「更新 + Chunk」模式下用 `Progress_WriteFinishBytes / Progress_WriteTotalBytes` 算进度环；Linux 侧只填了 `Progress_DownloadTotalBytes / Progress_DownloadFinishBytes`，写入量恒为 0 → 0/0 得 NaN。
- **修法**：我们是边下边写，`Report(...)` 里把下载量与写入量一起填（`Progress_WriteTotalBytes = progress.TotalBytes`、`Progress_WriteFinishBytes = progress.FinishedBytes`）；另外两处 UI 计算都加 `total > 0` 兜底，避免任何情况下再出现 NaN%。
- **教训**：移植时「字段名对得上」不等于「语义对得上」。凡是 UI 直接读的进度字段，都要在 Linux 侧找到一个明确的赋値点。

### L-66 · 开发期限量跑完不能当「装完」

- **症状**：`--dev-install-probe --limit-mb 120` 跑了 2 个补丁文件、129 MB 就停，但任务状态变成 `Finish`，并且往 `config.ini` 写了 `game_version=7.2.0`——安装目录里其实还是 7.1.0 的内容。
- **根因**：`GameInstallService.Linux` 的成功分支无条件 `Progress_DownloadFinishBytes = Progress_DownloadTotalBytes` + 写版本标记；下游只知道「没抛异常」，不知道「因为额度提前停」。
- **修法**：`UpdateRunResult.LimitReached` → `InstallPipelineResult.LimitReached` → 服务里判定为「未完成」，状态置 `Paused`、速度归零、**不写版本标记**，日志 `Game install dev limit reached`。
- **教训**：「提前停下」必须是显式结果，不能靠调用方猜。版本标记只能在真正完整安装后写。

### L-67 · 速度与剩余时间要记住上次采样点

- **症状**：速度停在第一次算出来的值（如 73.96 MB/s）不动，剩余时间也不变。
- **根因**：`Report(...)` 里算了 `delta / seconds`，但 `lastBytes`、`lastTick` 从未更新。
- **修法**：算完速度后把 `lastBytes = progress.FinishedBytes; lastTick = now;` 写回；总量为 0 或已超总量时把速度/剩余置 0。
- **教训**：任何「速率」都必须有显式的采样基准，否则第一帧的偶然值会被永远显示。


### L-68 · 「修复」拿错清单 + 写版本标记 = 顺手升级 + 谎报版本

- **症状**：在 7.1.0 的目录上点修复，跑完 `config.ini` 变成 `game_version=7.2.0`，但文件还是 7.1.0 的；再次修复时程序按 7.2.0 清单校验，于是把上千个文件判成「不一致」。
- **根因**：修复复用了「更新」的取清单逻辑（`branch.Main` = 最新 7.2.0），收尾又复用了更新的写版本标记。
- **修法**：修复必须 `tagOverride = 本地版本` 取清单；修复不写版本标记；`RunResumableAsync` 遇到字节上限时置 `LimitReached` 并停下（原来只是 `continue` 跳过剩余文件，结果「只修了一部分」被当成成功）。
- **教训**：四个操作（安装/更新/预下载/修复）在「取哪份清单」「是否改版本标记」上各不相同，必须显式分开，不能靠一个通用分支兜住。


### L-69 · Uno 的 PipsPager（模板里套 ItemsRepeater）既不画也点不动

- **症状**：启动页底部的背景切换器点圆点没有任何反应，`CurrentBackgroundImageIndex` 不变。
- **根因**：切换器的 `CustomPipsPagerStyle` 模板里用 `<ItemsRepeater ItemsSource="{Binding RelativeSource={RelativeSource TemplatedParent}, Path=TemplateSettings.PipsPagerItems}">` 手画圆点。Uno 对 `PipsPager.TemplateSettings` + `ItemsRepeater` 的实现不全，点不产生 `SelectedPageIndex` 更新，双向绑定也就没东西可回写（与 L-23 同族）。
- **修法**：换成代码生成的圆点按钮（`StackPanel_BackgroundDots` + `Button{Content=FontIcon}`，`Click` 直接 `CurrentBackgroundImageIndex = i`），选中态用 `AccentTextFillColorPrimaryBrush` / `TextFillColorSecondaryBrush` 区分；同时给 setter 加 clamp。
- **教训**：凡是「模板里塞 ItemsRepeater/自定义面板」的 WinUI 控件，在 Uno 上都要按「可能完全不工作」来验证，别只信 XAML 能编译。

### L-70 · 背景切换没反应：漏注册消息 + 把海报当视频

- **症状一**：切换器改了 index、发了 `BackgroundChangedMessage`，背景图一动不动。
  - **根因**：`AppBackground.Linux.cs` 重写时只保留了「按建议背景刷新」，没注册 `WeakReferenceMessenger.Default.Register<BackgroundChangedMessage>(...)`；上游 `AppBackground.xaml.cs` 里有的这条，在 Linux 分身里漏了。
  - **修法**：构造里注册，`UpdateBackgroundAsync(GameBackground? requested = null)` 支持「用户显式选中的背景」直接应用。
- **症状二**：选择了视频背景，永远是静态图。
  - **根因**：`TryStartVideoBackgroundAsync` 把 `background.Background.Url`（webp 海报）当视频文件喂给 libvlc，必然失败 → 静默退回静态图；视频其实在 `background.Video.Url`（webm）。
  - **修法**：视频取 `Video.Url`，叠加图取 `Theme.Url`；并尊重 `background.StopVideo`（用户点过暂停就不自动播）。
- **教训**：模型里的 `Background`/`Theme`/`Video`/`Icon` 是四种不同东西，写播放器前先确认字段语义，别望文生义。

### L-71 · WriteableBitmap 重复赋同一个实例不会重绘；1 秒 API 超时在 Linux 上必超时

- **症状**：视频帧回调在跑（帧计数稳定 30fps），屏幕上的背景却完全静止；另外启动时背景永远是兜底 CG，只有手动加长超时才拿到官方视频背景。
- **根因一**：`_image.Source = _videoBitmap` 每帧复用**同一个实例**，属性值没变，Uno/Skia 不重新采样。
  - **修法**：先 `_image.Source = null;` 再赋回同一个 `WriteableBitmap`，强制触发一次 source 重设（视觉上无闪烁）。
- **根因二**：上游用 `CancellationTokenSource(TimeSpan.FromSeconds(1))` 拿「建议背景」；Linux 首包要 TLS 握手，1 秒几乎必然 `OperationCanceledException` → 每台机器都退回静态兜底。
  - **修法**：默认 6 秒（`LUNARWARD_BG_TIMEOUT_MS` 可覆盖），超时时写一条 Information 日志说明「用了兜底图」，别再静默。
- **教训**：跨平台移植里「超时」和「PropertyChanged 式刷新」这两类隐含假设最容易原样搬过来，而它们在 Linux 上都不成立。

### L-72 · 单实例陈旧锁 + 缺 DISPLAY = 「进程在跑但没有窗口」

- **症状**：从 Rider 启动，进程起来了但没有窗口，控制台也没说为什么。
- **根因**：①`TryAcquireSingleInstanceLock()` 失败就 `NotifyPrimary(...)` 然后 `Environment.Exit(0)`，而 `NotifyPrimary` 把「没人接」的异常吞成 Debug 日志——上次崩溃留下的锁 + 已消失的 socket 就让新实例静默自杀；②`UnoPlatformHostBuilder.Build()` 在没有 `DISPLAY`/`WAYLAND_DISPLAY` 时直接抛异常，堆栈里只有 Uno 的路径，看不出是人话。
- **修法**：`NotifyPrimary` 返回 bool；没人接就判定为陈旧锁，删 socket、释放并重试接管，仍失败则 `stderr` 明确报错并 `exit 2`；`Program.Linux.Main` 在建 host 之前检查 `DISPLAY`/`WAYLAND_DISPLAY`，缺失时打印人话并 `exit 3`；新增 `--new-instance` 跳过单实例检查（IDE 多开/排查用）。
- **教训**：「第二个实例」的默认动作是**静默退出**，所以任何让它静默退出的分支都必须留下可读原因，否则用户只会看到「没反应」。

### L-73 · 自绘标题栏在 Uno X11 上既拖不动也点不动

- **症状**：窗口拖不动，右上角的最小化/关闭按钮点了没反应。
- **根因**：`WindowEx.SetDragRectangles` 在 Linux 是空实现，`AppWindow.TitleBar.SetTitleBar(...)` 也不生效；上游那套「`ExtendsContentIntoTitleBar=true` + 自绘拖动条 + 自绘按钮」完全建立在 Windows 的 DWM 行为上。另外 `MainView.xaml` 里的自绘按钮走 `AppWindow.Minimize/Close`，Uno 下没有对应实现。
- **修法**：改成 `ExtendsContentIntoTitleBar = false`，删掉自绘拖动条与两个按钮，窗口管理器的系统标题栏接管拖动/最小化/关闭（`_NET_FRAME_EXTENTS` 里能看到 35px 标题栏）。
- **教训**：凡是「替代系统 chrome」的 UI，都要先确认后端有没有实现对应的非客户区协议；没有就老实交还 WM，别硬画。

### L-74 · `Launcher.LaunchUriAsync` 在 Linux 静默失败

- **症状**：点左下角轮播图、公告里的链接、「打开文件夹」之类，全都没反应。
- **根因**：`Windows.System.Launcher.LaunchUriAsync` 在 Uno/Linux 上没有后端（翻遍 Uno dll 没有 `xdg-open`/`gio` 痕迹），调用不抛异常也不做事；上游还把调用包在 `catch { }` 里，失败完全不可见。
- **修法**：新增 `Helpers/OpenUrl.cs`（`xdg-open` → 失败退 `gio open`，`UseShellExecute=false` + `ArgumentList`，Debug 日志 + 返回 bool），替换 8 类调用点；`AppBackground` 里 3 处 apps.microsoft.com 保留原样（Windows 专属提示，Linux 上本来就没意义）。
- **教训**：WinRT 里「打开外部程序」的 API 在非 Windows 后端基本都是空实现，必须逐个换成本机命令，并且**别用空 catch 把它盖住**。


### L-75 · 用户数据用 `File.WriteAllText` 写 = 断电即丢配置

- **症状**：设置保存后 UI 显示成功，重启后回到默认；Profile 偶尔整个消失，目录里没有任何线索。
- **根因**：`LinuxConfig.Save`、`RuntimeProfileStore.Save`、`ProfileBinding`、`CompanionStore`、前缀元数据全是 `File.WriteAllText`——进程被杀/磁盘满会在目标文件上留半截 JSON；读取侧 `catch { LogWarning }` 把它当「没有配置」，损坏文件既不备份也不改名。
- **修法**：`Helpers/JsonFile.WriteAtomic(string, string)`（临时文件 → `Flush(flushToDisk:true)` → 覆盖前留 `.bak` → `File.Move(overwrite)`）+ 泛型重载；损坏读取改名 `*.corrupt-时间` 并记 Warning。
- **教训**：凡是「用户的设置/绑定/条目」，写入都要原子化；**读失败也必须留下证据**，否则用户只能得出「这软件会自己丢配置」的结论。

### L-76 · 预检有副作用：点一下「预检」就造前缀

- **症状**：只是点「预检」看能不能启动，`~/.wine` 里多出 `.lunarward/`，dedicated/template 模式甚至直接开始 clone/`wineboot`。
- **根因**：`LaunchPreflight` 直接调 `PrefixManager.EnsurePrefix`（本来就带创建语义），锁检查还 `Directory.CreateDirectory(prefix/.lunarward)` 后建锁文件。
- **修法**：预检改用 `TryGetExistingPrefix`（只解析已存在前缀），未准备就报告「启动时按配置创建/克隆」；占用检查改用 `PrefixManager.IsPrefixLocked`（只读打开）。
- **教训**：**预检/试运行必须只读**。任何「顺便建一下」的调用都会在用户只想看结果时改变系统状态。

### L-77 · 预检 key 与启动 key 不一致，预检的和你用的不是同一个前缀

- **症状**：预检报告前缀 A 一切正常，真启动却用了前缀 B（template/ephemeral 下尤其明显）。
- **根因**：`LaunchPreflight` 用 `biz.Value`（如 `hk4e_cn`）当 gameKey，`GameLauncherService.Linux` 用 exe 文件名（如 `yuanshen`）。
- **修法**：预检也走 `Path.GetFileNameWithoutExtension(exePath).ToLowerInvariant()`，与启动完全同源。
- **教训**：同一份「key → 资源」映射必须只有一个计算入口，任何复制的推导都会漂移。

### L-78 · 前缀锁/临时前缀不能挂 wine loader 的 `Exited`

- **症状**：游戏还在跑，第二个启动器实例已经能拿同一前缀；ephemeral 模式甚至会在游戏中途把前缀删掉。
- **根因**：`process.Exited` 监听的是 `wine` 启动器进程，它在游戏真正退出前常常先退出（L-55 同一现象）。
- **修法**：`AttachEphemeralPrefixCleanup` 改为后台 `Task` 里 `await PlayTimeSession.WaitForSessionEndAsync(pid, ActivePrefix)`，之后才 `DiscardEphemeralPrefix` / `ReleasePrefixLock` / `RunExitCompanions`。
- **教训**：Linux 上「wine 会话结束」才是游戏结束，任何资源回收都要以 `wineserver -w` 为准。

### L-79 · 一把永不释放的全局互斥量 = 第二个游戏时长恒为 0

- **症状**：打完一个游戏再开另一个，游玩时长一直不涨。
- **根因**：`PlayTimeRecordService` 用 `Mutex("lunarward_playtime").WaitOne(0)` 去重，且**从不 `ReleaseMutex`**；第一个会话结束后锁仍然挂着，第二个 pid 直接 `return`。
- **修法**：删掉 Mutex，改用已有的 `_recordingPids.TryAdd(pid)` + `finally TryRemove`；pid 复用时若「上一次 Start」超过 12 小时则视为过期，用当前时间当起点。
- **教训**：进程内去重优先用集合而不是系统级具名锁；命名锁一旦泄漏就只有重启能救。

### L-80 · 空桩页面 = 按钮点了没反应，和「bug」无法区分

- **症状**：截图设置页「更改文件夹/打开文件夹/测试截图/清缓存」全都没反应；改了截图目录也不生效。
- **根因**：`ScreenshotSetting.Linux.cs` 是 M1 时期的空桩（四个 `[RelayCommand] private void X() { }`），而 `ScreenCaptureLinux.DefaultFolder` 写死 `~/Pictures/Lunarward`。
- **修法**：设置页接 `FileDialogHelper.PickFolderAsync`、`OpenUrl`、`ScreenCaptureLinux.CaptureAsync`、缓存目录清理；`DefaultFolder` 优先读 `AppConfig.ScreenshotFolder`，`MyPictures` 为空时退 `~/Pictures`。
- **教训**：移植期留的空桩要在 `12-shims-and-gaps.md` 里能被搜到，否则用户只会当成又一个「Linux 版不好用」。

### L-81 · CLI 失败返回 0，脚本以为成功

- **症状**：`lunarward --preflight hk4e_cn || echo fail` 永远不触发 fail；`--install-run` 抓不到清单也「成功」退出。
- **根因**：`Program.Linux.cs` 的错误分支只 `Console.WriteLine` 后 `return`，只有陈旧锁/DISPLAY 两处 `Environment.Exit`。
- **修法**：所有「未知游戏/找不到条目/读不到本地版本/拿不到清单/branch 不可用/清单不可用/拒绝/规划失败/探针失败/初始化环境失败」分支统一置 `Environment.ExitCode = 1`。
- **教训**：命令行工具的错误路径要么抛、要么置退出码；只打印等于把「失败」伪装成「成功」。

### L-82 · CI 拿 Debug 的 assets 编 Release，撞上 Uno DevServer 门禁

- **症状**：本地/CI 执行 `dotnet build -c Release --no-restore`（前面只跑过不带配置的 `dotnet restore`）报
  `error UNOB0019: The DevServer package is intended for development builds only and should not be used with optimized/release builds.`
- **根因**：`Uno.Implicit.Packages.ProjectSystem.targets` 按 `$(Optimize)` 决定是否把 `Uno.WinUI.DevServer` 完整引入
  （`Optimize != 'true'` 时完整引用，`== 'true'` 时 `Exclude="all" IncludeAssets="None"`）。不带 `-p:Configuration=Release`
  的 `dotnet restore` 生成的是 Debug 口径的 `project.assets.json`，Release 编译时 DevServer 的 `buildTransitive` 目标照样被导入，
  于是它自己的「优化构建不许用 DevServer」校验直接报错。
- **修法**：Release 前先 `dotnet restore -p:Configuration=Release`，再 `dotnet build -c Release --no-restore`。
- **教训**：Uno.Sdk 的隐式包集合依赖 `Optimize`，**restore 的配置必须和 build 的配置一致**；`--no-restore` 只有在这个前提下才安全。

### L-83 · 用「可见」推断「属性没生效」是错的；间距要用像素测

- **背景**：真机反馈「左上角三个游戏图标挤在一起」。我当时的推理是「图标条在 y≈0 可见 → 它本该被 `Translation=(0,-100,0)` 移出视野 → 所以 Uno 不应用 `Translation`」。
- **错在哪**：用户设置 `IsGameBizSelectorPinned=True`，`GameSelector.xaml.cs` 的 `Pin()` 在固定态把 `GameIconsAreaVisible` 置真，setter 里就是 `Translation = Vector3.Zero`（`:263-268`）。也就是说「可见」是固定态的正常结果，无法推出任何关于 `Translation` 支持的结论。结论已撤回，规则见 D46。
- **正确的证据获取方式**（本次实测，可复用）：`spectacle -b` 截全屏 → 按窗口原点 `(360, 220)`（含 35px 系统标题栏）裁出内容区 → 对图标条做逐列采样，再用自相关找「条目间距」：
  ```python
  # 每列取图标带内的像素签名，找使两列最相似的 lag 即条目节距
  sig[x] = [sum(px(x, y)) for y in range(oy+4, oy+44)]
  for lag in range(30, 80): 计算 sum((sig[x]-sig[x+lag])**2)
  ```
  结果：最相似 lag = **50 物理像素**（= 40 DIP × 1.25）。而容器样式 `GameSelector.xaml:441` 写的是 `Padding="6,8,6,8"`（`ListViewItemPresenter.ContentMargin="{TemplateBinding Padding}"`），如果生效，节距应是 52 DIP ≈ 65 物理像素。实测节距 = 图标宽度本身 → **`ListViewItem` 的 `Padding/ContentMargin` 在这条 Uno 渲染路径上没生效**，所以三张 40px 图标是紧贴的（放大 8 倍的截图也能直接看到边缘相接、徽标压到隔壁）。
- **修法（已实施）**：不依赖 `ListViewItemPresenter.ContentMargin`，在条目根元素 `Grid_GameIcon` 上加 `Margin="0,0,8,0"`。改后用同一套像素方法复测：节距 50 → **60 物理像素**，图标之间出现 **9–10 像素纯背景列**（存证 `screenshots/m3-game-icons-spacing.png`）。
- **教训**：涉及「间距/尺寸/对齐/颜色」的结论，先截图再做像素测量，用数字说话；不要用某个像素现象去反推某个 API 是否被实现，除非有对照实验（例如同一属性在另一处生效/失效的对比）。

### L-84 · Uno X11 后端没有「隐藏」，而「唤回」要发 `_NET_ACTIVE_WINDOW`

- **症状**：点窗口管理器标题栏的 X 没有任何反应；用 `xdotool windowclose` 验证时窗口直接消失、进程还活着，看起来像「事件进不来」。
- **实测（三条，都有对照）**：
  1. `Window.Hide()`、`AppWindow.Hide()` 在 Uno 6.7.135 的 X11 后端上是空操作：调用后 `xwininfo` 仍是 `Map State: IsViewable`。
     对照：`OverlappedPresenter.Minimize()` 有效（立刻 `IsUnMapped`）。
  2. 真正的 `WM_DELETE_WINDOW` 客户端消息**能**进托管代码（`AppWindow.Closing` 触发）。`xdotool windowclose` 走的是 `_NET_CLOSE_WINDOW`，
     KDE 下窗口被直接销毁、托管层收不到任何事件——**不能用它验证关闭逻辑**。
  3. 最小化后 `presenter.Restore()` 回不来（`Map State` 仍 `IsUnMapped`）；`AppWindow.Show()` 也回不来。
     上游源码：`NativeWindowWrapperBase.Show(bool)` 只在 `!WasShown` 时 `ShowCore()`，之后只 `Activate()`；
     `X11WindowWrapper.Activate()` 只发 `XRaiseWindow`，`_NET_ACTIVE_WINDOW` 那段被注释掉了。
- **修法**：进程内 `libxcb`（`Helpers/X11Window.cs`，见 D47）：`xcb_unmap_window` 隐藏、`xcb_map_window` + `_NET_ACTIVE_WINDOW` 唤回。
- **三个容易再踩的坑**：
  1. 窗口 unmap 后会被窗口管理器从 `_NET_CLIENT_LIST` 摘掉 → **必须缓存窗口 id**，否则「隐藏成功、唤回失败」。
  2. Uno 的顶层窗口在 KDE 下**没有** `_NET_WM_PID` → 匹配窗口要按 `WM_NAME`/`WM_CLASS` 兜底。
  3. 手写 xcb 结构体偏移：`xcb_setup_t` 的 `vendor_len=24`、`roots_len=28`、`pixmap_formats_len=29`（header 40 字节），
     `xcb_get_property_reply_t` 的 `value_len` 在偏移 **16**（不是 24）。这两处错一次，症状都是「读不到 root / 列表为空」，与 X 服务器无关。

### L-85 · StatusNotifierItem 只实现 `Properties.Get` 会让 KDE 同时丢掉菜单和图标

- **现象**：KDE 上托盘图标显示成占位、右键没有菜单；代码里 DBusMenu 数据本身是对的（`GetLayout` 手工调用能拿到 4 项）。
- **证据链**：`gdbus … Properties.GetAll` 报 `UnknownMethod` → 抓总线：plasmashell 只发一次 `Properties.GetAll`，失败后不再访问 `/MenuBar`
  → plasmashell 日志 `Could not find DBusMenu interface, falling back to calling ContextMenu()`。
- **原因**：tmds.dbus 用「接口方法名 = D-Bus 方法名（去 `Async`）」的约定，`GetAsync` 恰好对上 `Properties.Get`，
  但 `Properties.GetAll` 没有对应方法就没人实现；KDE 的属性获取走 `GetAll` 单次批量。
- **修法**：接口里补 `GetAllAsync()`（返回 `IDictionary<string, object>`），SNI 与 `com.canonical.dbusmenu` 各实现一份；
  顺手把 `ContextMenu` 回调从空 lambda 改成「唤回主窗口」，宿主实在拿不到 dbusmenu 时也有兜底。
- **教训**：D-Bus 的「可选接口」不是真可选——宿主用哪个方法取属性，决定了你必须实现哪几个方法；验证要看**宿主的行为**（总线抓包 + 宿主日志），不要只看自己对象返回得对不对。

### L-86 · Uno X11：把已存在的窗口重新 map 不会触发 `Window.Activated`

- **现象**：隐藏 → `--show`/托盘唤回后窗口回来了，但背景视频不恢复（一直暂停），截图差异 0.00%。
- **证据**：`[bg-diag]` 日志里只有 `activate=False hide=True`，唤回后没有 `activate=True`；`Window.Activated` 不触发。
- **原因**：Uno 的 `Activated` 只在窗口获得焦点时发；`xcb_map_window` + `_NET_ACTIVE_WINDOW` 让 kwin 把窗口重新显示，
  但走的是「map 已存在窗口」路径，托管层不再产生 `Activated`。
- **修法**：`WindowEx.Show()` 在 `X11Window.Show()` 成功后自己发一条 `MainWindowStateChangedMessage{Activate=true}`（与 `MainWindow.OnWindowActivated` 同语义）。
- **教训**：自己接管框架缺失的能力（D47）时，也要接管**框架本该因此触发的事件**，否则「窗口回来了、状态没回来」。

### L-87 · 米哈游背景的 `Theme` 图是纯透明文字层，静态背景必须合成

- **现象**：暂停/静态背景时左上角没有标题文字，用户以为是「海报文字丢了」。
- **证据**：下载的 `0eefc26b…webp` 2560×1440，**99.1% 像素 alpha=0**，只有标题文字不透明；上游静态分支 `DrawImage(底图)` + `DrawImage(Theme)` 两次合成。
- **修法**：`AppBackground.Linux.LoadBackgroundImageAsync` 下主题图（6 秒超时）→ SkiaSharp 合成 → PNG → `BitmapImage`；失败退回底图。
- **教训**：上游把「底图」和「文字层」拆成两张资源，只看其中一张会以为「素材缺了文字」；移植时先对齐上游的**绘制顺序**，再谈差异。

### L-88 · 图标「豆腐块」的真凶是 Uno 自带 Symbols 字体缺码位，随包字体在这套后端上不可靠

- **现象**：视频播放/暂停按钮等少数图标显示成方框或串字形，而网络/齿轮等多数图标正常。
- **取证**：`strace -f -e trace=openat ./Lunarward` → 全程没有 `Assets/Font/*`；`Uno.Fonts.Fluent/Fonts/uno-fluentui-assets.ttf`（family `Symbols`，1394 字形）被加载；
  用 fontTools 比对项目 104 个码位，`E62E E733 F012 F08F F413 F4A5` 不在该字体里。
- **为什么不能直接用上游字体**：`ms-appx:///Assets/Font/Segoe Fluent Icons.ttf`（文件名带空格）与 `%20` 编码、以及复制成无空格别名 `FluentIcons.ttf`，
  三种写法都从未触发 openat；而 `PhotosIcons.ttf`（无空格、显式 FontFamily）能正常加载 → 结论只能落在「改用 Uno 自带字体 + 替换码位」。
- **修法**：`E62E→E769`、`E917→E823`、`E733→F140`、`F012→E896`、`F08F→E768`、`F413→E74E`、`F4A5→E734`。
- **教训**：①「图标显示错」先别猜主题/字体族，直接 strace 看**实际加载了哪个 ttf**；②fontTools 可以离线渲染任意码位确认形状，比在 UI 里逐个撞快得多；
  ③Uno 的字体加载对含空格路径静默失败，没有任何日志——这类失败只能靠系统调用层取证。


### L-89 · 强调色写在 ThemeDictionary 里，只改 `Application.Resources` 不生效

- **现象**：切换游戏后界面配色不变，右下主按钮一直是 `#CBAD8E`；日志显示取色成功、写资源也没报错。
- **证据**：`App.xaml` 的 `SystemAccentColor*` 放在 `<ResourceDictionary.ThemeDictionaries>` 的 `Light`/`Dark` 里；
  主题字典的查找优先级高于应用级 `Resources`，所以 `Application.Current.Resources["SystemAccentColor"] = color` 对任何
  `{ThemeResource SystemAccentColor}` 都不起作用。
- **修法**：同时写应用级 `Resources` 与四个主题字典（`Light`/`Dark`/`Default`/`HighContrast`），再发消息让主窗口翻转 `RequestedTheme` 重刷。
- **附带坑**：取色跑在线程池，但写资源必须在 UI 线程；在 `Task.Run` 里调 `DispatcherQueue.GetForCurrentThread()` 得到的是 null，
  结果在后台线程改 Resources——资源变了、界面不重绘。正确做法是**在调用方（UI 线程）先取好 dispatcher**再传进去。
- **教训**：Uno/WinUI 的 `ThemeResource` 解析有明确的优先级链，改资源前先确认「这个名字到底在哪一层定义」。

### L-90 · 语言设置不生效：`CurrentUICulture` 改不到已经建好的界面线程

- **现象**：`config.json` 里 `Language=de-DE` 重启后界面仍是中文；把系统 `LANG` 改成 `de_DE.UTF-8` 启动却是德文。
- **原因**：`AppConfig.SetLanguage` 只设了 `CultureInfo.CurrentUICulture` 与 `DefaultThreadCurrentUICulture`；
  `DefaultThreadCurrentUICulture` 只影响**之后新建**的线程，而 Uno 的界面线程早已创建。
  `Lang` 的属性调 `ResourceManager.GetString(name, resourceCulture)`，`resourceCulture` 为 null 时读的是当前线程的 culture。
- **修法**：给 `Starward.Language.Lang.Culture` 与 `Starward.Core.Localization.CoreLang.Culture` 显式赋值（跨线程生效），
  两个资源类都要设，否则界面文案变了、`CoreLang` 里的活动/公告/资讯标题还是旧语言。
- **教训**：i18n 的两条链路（UI 资源类 + Core 资源类）都要覆盖；验证时用「改配置 + 系统语言保持 zh_CN」这种最贴近用户的组合，
  只测 `LANG=de_DE` 会把问题掩盖掉。

### L-91 · 托盘不可用时「关闭 = 隐藏」= 关不掉的窗口

- **现象**：没有 StatusNotifierHost 的桌面上，点 X 窗口消失、进程还在，用户只能去任务管理器杀。
- **原因**：`CloseWindowOption` 默认 `Hide`，而 `StatusNotifierTray.StartAsync` 的失败被 `_ =` 丢弃，没人知道托盘没起来。
- **修法**：`StartAsync` 返回 bool + `Available` 静态属性；启动失败弹一次提示；`AppWindow_Closing` 在「要隐藏但托盘不可用」时
  不拦截关闭，让进程跟着窗口退出。
- **教训**：任何「隐藏到托盘」的语义都必须有「托盘不可用」的降级路径，否则在精简桌面（无托盘 host）上就是死锁体验。

## L-92 · `WINEPREFIX` 带尾斜杠会让某些 wine 构建直接退出（2026-11-06）

**症状**：绝区零「完全启动不了」——点开始游戏后 1–2 秒内进程退出，没有窗口，日志里只有一行
`wine: launching from steam.exe stub!`。

**取证**：同一套命令，唯一差别是 `WINEPREFIX` 是否带尾斜杠：

```bash
WINEPREFIX=/home/…/prefix/ wine ./ZenlessZoneZero.exe   # 打印 stub 消息，立即退出
WINEPREFIX=/home/…/prefix  wine ./ZenlessZoneZero.exe   # 正常起窗口
```

spritz-wine（sleepy-launcher 的运行器，和部分 Proton 变体同源）把 `WINEPREFIX` 当字符串做前缀比较，
尾斜杠让它认为「不是同一个前缀」，于是走 stub 分支退出。**修法**：所有前缀路径统一过
`PrefixManager.ExpandPath`（顺带展开 `~`/`$HOME` 并去掉尾分隔符），profile 存盘时也规范化。

## L-93 · 游玩时长子进程盯错前缀（2026-11-06）

**症状**：游戏窗口还在，游玩时长 0–30 秒就结束，前缀锁也被提前释放。

**根因两层**：
1. `playtime` 记录子进程是新进程，`PlayTimeSession.ActivePrefix` 是空的，`ResolvePrefix` 回落到 `~/.wine`；
2. 子进程里 `ResolveWine()` 走 PATH/Proton 回落，拿到的 `wineserver` 与自定义运行器的不是同一个，
   `wineserver -w` 立刻返回。

**修法**：启动记录子进程时用环境变量把「本次真正的前缀 / wine / wineserver」传下去
（`LUNARWARD_WINEPREFIX` / `LUNARWARD_WINE` / `LUNARWARD_WINESERVER`），`ResolveWineserver` 优先读显式值。

同一个链路上还有一处竞态：运行器先起 `steam.exe` 存根、真正的游戏是孙进程，此刻 `wineserver -w`
因为「前缀里还没有 wine 进程」会瞬间返回。修法是先等 `/proc/*/environ` 里出现 `WINEPREFIX=<prefix>`
的进程（最多 30 秒，启动进程已退出则再宽限 3 秒），再进入阻塞等待。

## L-94 · MSBuild 只在「调用目录」的 global.json 里找 `msbuild-sdks`（2026-11-05）

从仓库根构建时 `Uno.Sdk` 解析失败（`MSB4236` + `The NuGetSdkResolver did not resolve this SDK because there was no version specified in the project or global.json`），
而进 `src/Lunarward` 单独构建正常——因为 `msbuild-sdks` 钉版本只写在子目录的 `global.json` 里。
**结论**：要给某个 SDK 钉版本，就写在「你会在哪里敲 dotnet」的那层 `global.json`；子目录留一份给单独构建也合理。
另外 `.slnx` 不含 `Configurations` 时 `-c Release` 依然生效，不必手写平台矩阵。

## L-95 · `du` 大的目录不一定是构建产物（2026-11-10）

清理 `poc/uno-smoke/LunarwardPoc`（170 MB）时差点把 65 个**源文件**一起删掉——它的 `bin/obj` 占了大头，
但目录本身是受版本控制的 PoC 项目。删任何"看起来像产物"的目录前，先 `git ls-files <dir> | wc -l` 核对；
确认是产物就只删 `bin/`、`obj/`、发布输出这类子目录。删错了 `git checkout -- <dir>` 能立刻恢复。

## L-96 · 壁纸缩略图别整张解码（2026-11-10）

`bg/` 里 16 张背景全是 2560×1440，图库条目只显示 216×122 DIP。`BitmapImage.SetSourceAsync` 会把整张图解成
位图（2560×1440×4 ≈ 14.7 MB/张），16 张 ≈ 236 MB，而且都在 UI 线程排队 → 打开图库明显顿。
**做法**：解码前先用 SkiaSharp 缩到显示尺寸（`SKFilterQuality.High` = 三次卷积 + mipmap，缩 10 倍也不糊），
再 `SetSourceAsync`。别用最近邻（锯齿）或纯线性（摩尔纹）。

## L-97 · 程序化放大窗口后 WrapPanel 不会自己换行（2026-11-10）

窗口先按 1200 DIP 创建、再由 `ClampToScreenAsync` 撑大，`ListView` 里的 `tk:WrapPanel` 仍是首次测量的列数，
右侧留出几百 DIP 空白——**看起来像布局 bug，其实是没重新测量**。修法是订阅 `SizeChanged` 后
`ItemsPanelRoot.InvalidateMeasure()` + `InvalidateMeasure()`。通用教训：凡是「运行时改窗口尺寸」的窗口，
都要考虑布局面板是否需要显式失效。

### L-98 视频条目的缩略图在 Linux 上是黑的

`Controls/CachedImage.Linux.cs` 里 `IsThumbnail`/`PngThumbnail` 只注册了依赖属性、没有任何实现（是给上游 API 留的形状），所以背景图库里的 `.webm` 条目走的是「直接解码 webm」路径：`SKBitmap.Decode` 抛异常 → 退回 `BitmapImage.SetSourceAsync` 也解不了 → 只剩容器底色 + 右下播放角标。修法（已实现，`Features/Background/VideoThumbnailer.cs`）：复用 libVLC 的 `MediaPlayer.TakeSnapshot` 抽首帧（`:start-time=1`、480×270），落成 `~/.local/share/lunarward/bg-thumbs/<hash>.png` 缓存，`BackgroundFileItem.ThumbnailPath` 填好后 `DisplayPath` 切到 png；信号量限制同时 2 个，`PropertyChanged` 回 UI 线程发。实测 16 张背景里的视频条目从黑块变成首帧画面。

### L-99 删掉卡片的显式 Width/Height 不会「自适应」，而是被内容撑开

选择层为了「按宽度均分」把 `Grid` 的 `Width="192" Height="108"` 去掉，结果卡片宽度变成背景图原始尺寸（2560 px）——`CachedImage` 是 `Image`，`Stretch` 只影响绘制，测量时仍返回图片固有尺寸。正确做法是把尺寸做成**数据**：给条目模型加 `CardWidth/CardHeight`（`ObservableObject`），模板 `x:Bind` 到它们，由容器按可用宽度写值。

### L-100 名字叫 Cache 的字段不一定是缓存

`AppConfig.CacheFolder` 是从上游继承来的名字，实际指向用户数据目录（日志、SQLite 数据库、背景下载都在里面）。往它下面加「缩略图」这种可再生物时，先分清「删了会不会丢用户数据」：纯缓存应放 `AppConfig.CacheDirectory`（`~/.cache/lunarward`），并交给 `Housekeeping` 定期清理，否则数据目录会越来越杂、备份也会把几百 MB 缩略图一起打包。

### L-101 ScrollViewer 里「星号列 + TextTrimming」压不住宽度

运行组件列表的每一行原本是「横向 StackPanel + 两个文字块 + 右侧徽标」：`StackPanel` 给子项无限宽度，`TextTrimming` 不生效，路径文字直接顶到徽标上、甚至超出窗口右边界（截图为证）。改成 `Grid`（`Auto / * / Auto / Auto`）后**仍然**顶出去——Uno 的 `ScrollViewer` 在 `HorizontalScrollMode=Disabled` 时也会用无限宽度测量内容，星号列拿不到上界。最终做法是不依赖测量：按可用像素估算可容纳字符数（12px ≈ 6.8 px/字符），用「中间省略」的字符串喂给 `TextBlock`（保留头尾，路径的尾部文件名最重要）。能用测量解决当然更好，但这条链在 Uno 上试了三种写法都不收敛，不要再花时间。

### L-102 「上次可用」要记在启动成功之后

记录「上次能用的组合」放在 `Process.Start` 之后、`process != null` 分支里，而不是预检通过时：预检只证明「看起来能跑」，真正的证据是进程起来了。这个顺序也保证「记录的一定是能用的」，用户按它切回去不会掉进另一个坏组合。
