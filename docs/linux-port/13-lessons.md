# 13 · 移植过程中的经验与坑

> 这里只记「踩过一次就不该再踩」的东西。每条都给出证据位置与处理方式。
> 目的：让后来者（或几个月后的我们）不用重新发现一遍。
>
> 条目 **L-01…L-175**（最后更新 2026-11-24；L-166 VLC 抽帧窗口、L-167 wineboot 卡 wine-mono；编号勘误见文末）。

- 最后更新：2026-11-19（L-163）

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

### L-03 `ControlTemplate is not supported`：自身实现错位，不是 Uno 不支持

Uno 的 `Control` / `ControlTemplate` 正常可用。这里的报错来自**我们自己的实现错位**：
上游 `ImageEx` 是 **Control**（有 `DefaultStyleKey` 和模板），移植时把它写成了 `Image` 的子类，
XAML 里的 `ControlTemplate` 自然无处可挂。改回 `ImageEx : Control` 并在 `OnApplyTemplate` 里
拿 `Image`/`PlaceholderImage`、用 `VisualStateManager.GoToState` 切状态即解决
（当前实现：`Controls/ImageExCompat.cs:18`）。

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

### L-09 仓库根 `global.json` 的 SDK 下限：钉太高会拦下所有 dotnet 命令

仓库根 `global.json` 由本仓库自己维护（D89 / L-163）：`sdk.version = 10.0.100`、
`rollForward = latestMinor`，并带 `"msbuild-sdks": { "Uno.Sdk": "6.7.45" }`。
本机 SDK 10.0.404，在仓库根构建/测试正常。

历史教训：曾把 `version` 钉成 `10.0.400`（`rollForward: latestMajor` 也不够），当时本机只有 10.0.302，
于是仓库根的任何 `dotnet` 命令都报：

```
A compatible .NET SDK was not found. Requested SDK version: 10.0.400
```

正确做法：**下限只写 `x.y.100`**，别钉具体 feature band；`src/Lunarward/`、`tests/`、`poc/`
各自的 `global.json` 与根保持同一口径（它们是给「单独在该目录构建」用的，
不是用来覆盖上游文件的——本项目已不再保留上游那份 Windows 构建配置）。

### L-10 api.nuget.org 在本机下大包会 ~1 MB 处 EOF

`Received an unexpected EOF or 0 bytes from the transport stream`。改用华为云镜像
（`https://repo.huaweicloud.com/repository/nuget/v3/index.json`，实测 12 MB/s），
并且 `NuGet.config` 里 `<clear/>` 后**只留一个源**——多源时 nuget.org 一失败就拖垮整个还原。

### L-11 `Tmds.DBus` 必须 ≥ 0.95.x

≤ 0.21.x 有高危漏洞 `GHSA-xrw6-gwf8-vvr9`，且 API 不同（0.95.2 的 `Connection` 只有
`ConnectAsync`/`Dispose`）。

### L-12 WebKitGTK 在部分环境要关 DMABUF 渲染（只对子进程助手注入，不是全局必需）

```bash
WEBKIT_DISABLE_DMABUF_RENDERER=1
# Wayland 会话再加：
GDK_BACKEND=x11
```

不设时可能刷 `Failed to create GBM buffer of size WxH` 并黑屏（G0-3b）。注意适用范围：
这两条只在**拉起子进程登录助手**时注入（`Features/GameRecord/WebLoginService.cs:99-126`，
Python 兜底路径 `:123` 同样注入）；D76 之后的主路径是本进程内的 `--webkit-login` 宿主
（`Features/GameRecord/WebKitLoginHost.cs`），那里**没有**设置这两个变量。
所以别把「必须设置」写成全局结论——进程内路径遇到 GBM/黑屏时，才需要在启动环境里补。

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

## L-23 · ItemsRepeater + 自写 `WrapLayout` 这套组合在本项目不工作

当时的实测：`VirtualizingLayout` 派生类编译通过，但 `MeasureOverride`/`ArrangeOverride` 不被调用，
布局完全不生效；`NonVirtualizingLayout` 连可覆写的方法都没有（CS0115）。这只能说明
**当时那套 ItemsRepeater + `WrapLayout` 组合**不可用，不能推广成「Uno 的 ItemsRepeater
不支持自定义 Layout」——该结论已收窄。

- **现行做法**：需要「换行排布」时用 `ItemsControl` + `ItemsPanelTemplate` 里的自写 `WrapPanel`
  （`: Panel`，覆写 Measure/Arrange 正常生效），不要用 `ItemsRepeater.Layout`。
  `Controls/ToolkitControls.cs:117-122` 里仍留着一个空的 `WrapLayout : VirtualizingLayout`，
  只为让可能残留的上游 XAML 能编译；XAML 侧已全部改用内置 `StackLayout`/自写 `WrapPanel`。
- **代价**：`SimulatedUniversePage.xaml` 的标签云从 `ItemsRepeater+WrapLayout` 改成
  `ItemsControl+WrapPanel`，视觉等价、无损虚拟化（该处数据量小）。

## L-24 · `IsTextTrimmedChanged` 是 Uno 自带的——此条旧结论已撤回

原结论「Uno toolkit 没有这个附加事件，不要硬撑」**已被证伪**。Uno 6.7.359 的 `Uno.UI.dll`
含 `add_IsTextTrimmedChanged`（`strings src/Lunarward/bin/Debug/net10.0-desktop/Uno.UI.dll |
grep add_IsTextTrimmedChanged` 有命中），本仓库现有 **7 处** XAML 正常挂载并生效：
`Features/Gacha/GachaStatsCard.xaml:59`、`Features/Gacha/ZZZGachaStatsCard.xaml:59`、
`Features/GameInstall/InstallGameDialog.xaml:56`、`Features/GameRecord/StarRail/ApocalypticShadowPage.xaml:206`、
`ForgottenHallPage.xaml:203`、`PureFictionPage.xaml:204`、`Features/Setting/ScreenshotSetting.xaml:62`；
处理器形如 `GachaStatsCard.xaml.cs:105` 的
`TextBlock_GachaTypeText_IsTextTrimmedChanged(TextBlock sender, IsTextTrimmedChangedEventArgs args)`。

- **现行做法**：直接用该事件，不要再按旧结论把它删掉。
- **当时的失败**：自造了同名 `IsTextTrimmedChangedEventArgs`，生成代码里的委托是
  `TypedEventHandler<TextBlock, IsTextTrimmedChangedEventArgs>`，与自造类型不是同一个 → CS0123/CS1503。
  这是**类型没引对**，不是事件不存在；复核见 L-132。
- **原则**：判「平台不支持某能力」前先查程序集/源码；撤回的结论要留档（参见 L-132）。

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
- **教训**：上游代码里凡是 `Microsoft.Win32.Registry` 的调用，在 Linux 上都是**运行期炸弹**，编译期发现不了。
  实测口径（2026-11-24）：`grep -rn "Registry\." src/Lunarward --include=*.cs | wc -l` 只有 **51 行**，
  其中还混着本项目自己的 `WineRegistry.` / `GameRegistry.`；真正属于 `Microsoft.Win32.Registry` 的 **15 行**：
  10 行在 3 个被 `<Compile Remove>` 排除的文件里（`Codec/VP9Helper.cs:98`、
  `GamepadControl/GamepadController.cs:924/934/960/966`、`UrlProtocol/UrlProtocolService.cs:35-45`），
  另 5 行虽被编译但 Linux 走不到（`GameSelector.xaml.cs:1028` 位于 `!OperatingSystem.IsWindows()` 分支之后；
  `CloudGame/CloudGameButton.xaml.cs:57/74/75/90` 是云游戏 M4 占位页）。所以清点时先分清
  「真 `Registry.`」与「自己的 `WineRegistry.`」，再看它是否在编译/可达路径上——不是「98 处全是炸弹」。

### L-31 · Uno 未实现 `XamlRoot.ContentIslandEnvironment` 与 `DisplayInformation.CreateForWindowId`，调用即抛

- **现象**：`XamlRootExtension.GetAppWindow()` 抛 `NotImplementedException`；HDR 区块抛 `CreateForWindowId(WindowId) is not implemented`。
- **修法**：`GetAppWindow()` 兜底到 `MainWindow.Current.AppWindow`；HDR 区块 try/catch 后置 `HDRNotSupported = true`（界面显示「当前显示器不支持 HDR」，符合 D33「占位要诚实」）。
- **教训**：Uno 的 `NotImplementedException` 消息里带 `aka.platform.uno/notimplemented` 链接，是判断「官方明确没做」而不是「用法错了」的可靠信号。

## L-32 外部登录助手的两个坑

1. **助手参数协议必须和调用方对齐**：`login.py` 最初只认 `sys.argv[1]` 当 URL，而 `WebLoginService` 传的是 `--url/--output`，结果 WebKit 把字符串 `--url` 当地址加载，界面显示「该 URL 无法显示」——看起来像网络问题，其实是参数没解析。改造后统一用 `argparse`。
2. **WebKitGTK 在部分环境要关 DMABUF 渲染**：不设 `WEBKIT_DISABLE_DMABUF_RENDERER=1` 会刷 `Failed to create GBM buffer of size 1100x760` 并黑屏；Wayland 会话下再加 `GDK_BACKEND=x11`（L-12 同源问题）。注入范围仅限**子进程登录助手**：`WebLoginService` 拉起自进程 `--webkit-login` 子进程与 Python 兜底助手时会设（`Features/GameRecord/WebLoginService.cs:99-126`）；D76 之后主路径的进程内 `WebKitLoginHost` 没有设这两个变量。

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

## L-35 `AppWindow.MoveAndResize` 在 Uno 的 X11 后端是空操作（`Move`/`Resize` 不是）

- 现象：主窗口一直是 1024x640，`CenterInScreen(1200, 676)` 明明执行了；`AppWindow.Size` 也仍然报 1024x640。
- 原因（Uno 6.7.359 复核）：`X11WindowWrapper` 已经实现了 `Move(PointInt32)`
  （`src/Uno.UI.Runtime.Skia.X11/UI/Xaml/Window/X11WindowWrapper.cs:137`）与 `Resize(SizeInt32)`（`:152`），
  两者都真的作用到 X11 窗口；仍是生成桩的是 `AppWindow.MoveAndResize(RectInt32)` 及其重载——
  `src/Uno.UWP/Generated/3.0.0.0/Microsoft.UI.Windowing/AppWindow.cs:90-100` 只调 `TryRaiseNotImplemented`。
  所以「Resize 生效、MoveAndResize 不生效」成立，但**不能推出「Move 也不行」**。`AppWindow.Size` 的读回同样滞后一拍。
- 修法：`WindowEx.Linux.CenterInScreen` 先 `AppWindow.Resize(new SizeInt32 { Width = w, Height = h })`
  （注意 `SizeInt32` 没有两参构造）拿到正确尺寸，再用 `AppWindow.Move(new PointInt32 { X = x, Y = y })`
  真正移动；`MoveAndResize` 可以留着当「语义占位」，但要清楚它当前是空实现，别指望它定位。
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

## L-45 · Xlib 不能随便在别的线程里用；窗口恢复主路径是 libxcb，xdotool 只兜底

**现象**：给全局热键加「唤回主窗口」时，先试 `OverlappedPresenter.Restore()` / `AppWindow.Show()` / `AppWindow.Hide()`——全部是空操作（Uno 6.7.359 X11 后端，窗口 `Map State` 一直是 `IsUnMapped`）；再试在计时器线程里直接调 Xlib（`XQueryTree` + `XFetchName` + `XMapRaised` 或 `XSendEvent(_NET_ACTIVE_WINDOW)`），进程直接 `free(): invalid pointer` / `X Error: 9 Bad Drawable` 而 abort。

**原因**：Xlib 不是线程安全的。我们自己 `XOpenDisplay` 的连接在计时器线程上跑，但 Xlib 的全局状态（display 列表、错误处理器）与 GTK 共用；`XQueryKeymap` 这种只读、不分配内存的调用侥幸没事，一旦涉及 `XQueryTree`/`XFetchName`（会 malloc/free）就撞堆。装 `XSetErrorHandler` 更糟——它是**进程级全局**的，改掉等于把 GTK 的错误处理也换了。

**做法**：
1. 窗口恢复主路径是**进程内 libxcb**：`X11Window.Show` / `X11Window.Activate` 发 EWMH `_NET_ACTIVE_WINDOW`（`Helpers/X11Window.cs:106`、`:139`，消息发送在 `:592`），不碰 Xlib。
2. 子进程 `xdotool search --name ^Starward$` + `xdotool windowactivate --sync <id>`（`X11KeyState.ActivateWindowWithXdotool`）只作为 **libxcb 不可用时的兜底**（`Helpers/X11KeyState.cs:240-257` 注释自述），好处是 xdotool 自己维护连接、崩溃隔离在子进程；没有 xdotool 时返回 false，调用方退回 `Window.Activate()`。
3. 热键检测保留 `XQueryKeymap` 轮询（25ms），只读不分配。
4. 已知限制：`xdotool key alt+s`（约 12ms 的合成按压）抓不到，人类按键通常 >100ms 所以可接受；要更灵敏需改 `XGrabKey` + 事件循环。
5. `AppWindow.Id`/`WindowHandle` 在 Uno Linux 上分别是 `1`/`0`，拿不到真实 X 窗口 id。

**教训**：跨线程碰 Xlib 前先问「这段代码会不会 malloc」；能不碰就不碰，优先用进程内 libxcb 或官方 API，子进程是最后兜底；错误处理器这类全局钩子在任何 GUI 框架里都不要动。

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

## L-50 · `GenerateAssemblyInfo=false` 之后版本属性要自己补

**现象**：「关于」页显示 `0.1.0-linux` 之前是 `0.0.0.0`；日志 `Welcome to Starward v0.0.0.0`。`Lunarward.csproj` 里明明写了 `<Version>` 与 `<InformationalVersion>`。

**原因**：本项目在 `src/Lunarward/Lunarward.csproj:9` 显式设了
`<GenerateAssemblyInfo>false</GenerateAssemblyInfo>`（第 8/10 行另有 `<Version>` / `<InformationalVersion>`），
SDK 因此不再生成 `AssemblyVersion` / `AssemblyInformationalVersion` 等特性——自动生成的
`obj/Debug/net10.0-desktop/Lunarward.AssemblyInfo.cs` **只剩** `TargetPlatform`/`SupportedOSPlatform` 两条，
所以 `GetCustomAttribute<AssemblyInformationalVersionAttribute>()` 返回 null，代码落到兜底
`Assembly.GetName().Version`（0.0.0.0）。**不是 Uno.Sdk 不生成**，是我们自己把开关关了。

**做法**：新增 `Properties/AssemblyVersion.cs` 显式声明三条：
```csharp
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0-linux")]
```
验证：日志变 `Welcome to Starward v0.1.0-linux`，关于页显示「当前版本：0.1.0-linux」（截图 `docs/linux-port/screenshots/m2-about.png`）。

**教训**：`<Version>` 之类的 MSBuild 属性只在默认的 `GenerateAssemblyInfo` 流程里生效；
谁关掉 `GenerateAssemblyInfo`，谁就要自己补程序集特性。出问题先看 `obj/**/AssemblyInfo.cs` 里到底生成了什么，
别先怪 SDK。

## L-51 · LibVLCSharp 的 vmem 回调：地址要写进 planes[0]，不是返回值

**目标**：把视频帧拿到托管侧做壁纸。

**坑 1（段错误）**：`SetVideoCallbacks(lock, unlock, display)` 的 `lock` 签名是 `void lock(void* opaque, void** planes)`——libvlc 给的是一个**指针数组**，回调必须把缓冲区地址写进 `planes[0]`。我一开始按 `IntPtr lock(...)` 返回地址，libvlc 照样往 `planes[0]`（null）写，直接 SIGSEGV。

**坑 2（跨边界异常）**：`display` 回调里 `new SKImageInfo(..., SKColorSpace.CreateSrgb())` 抛异常 → 异常穿过原生栈帧 → `core dumped`。回调里必须整段 `try/catch`。

**坑 3（生命周期）**：固定缓冲区在播放期间只能做一次；结束要先 `player.Stop()` 再 `GCHandle.Free()`，否则 libvlc 的线程还在写。

**可用组合**（`src/Lunarward/obj/project.assets.json` 的实际解析结果）：`vlc 3.0.24` + `LibVLCSharp 3.9.7.1` + `SkiaSharp 3.119.2` + `SkiaSharp.NativeAssets.Linux.NoDependencies`（缺这个包时 `SKColorSpace` 的类型初始化会炸）。

## L-52 · Uno 已经传递依赖 LibVLCSharp，别再自己引一遍

`Uno.WinUI.Runtime.Skia.X11 6.7.359` 的依赖里就有 `LibVLCSharp (>= 3.9.7.1)`（`MediaPlayerElement` 用，实际解析为 `LibVLCSharp 3.9.7.1`）。我一开始在项目里显式加 `LibVLCSharp 3.9.4`，直接 `NU1605` 包降级失败。正确做法：**不引**，直接用传递依赖的版本；原生库 `libvlc` 由系统提供（Arch 装 `vlc`），缺失时 `LibVLC.Core.Initialize()` 抛异常，按 D33 诚实降级为静态壁纸。

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
  - **修法**：默认 **4 秒**（`LUNARWARD_BG_TIMEOUT_MS` 可覆盖），并做 1x/2x 退避重试：
    `Features/Background/AppBackground.Linux.cs:275` 的 `: 4000`、`:279` 的
    `int[] attempts = { apiTimeoutMs, apiTimeoutMs * 2 }`；超时时写一条 Information 日志说明「用了兜底图」，别再静默。
    6 秒只属于主题图/合成路径（`:222`、`:598`、`:605`）与 `Features/Background/VideoThumbnailer.cs:163`
    的 `frameReady.Wait(TimeSpan.FromSeconds(6))`，不要和「背景 API 超时」混为一谈。
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

### L-74 · `Launcher.LaunchUriAsync` 在 Linux 上有实现，失败只发生在个别 scheme/缺 handler

- **症状**：点左下角轮播图、公告里的链接、「打开文件夹」之类，有的没反应。
- **根因（复核）**：Uno 6.7.359 的 Skia/X11 后端**有** launcher 实现：`src/Uno.UWP/System/Launcher.skia.cs`
  与 `src/Uno.UWP/System/LinuxLauncherExtension.skia.cs:17-31` 用
  `ProcessStartInfo { UseShellExecute = true, Verb = "open" }` 交给系统，注册点在
  `src/Uno.UI.Runtime.Skia.X11/Hosting/X11ApplicationHost.cs:99`；.NET 10 再把 `UseShellExecute`
  落到 xdg-open/gio（`Process.Unix.cs:379-445`）。所以「恒失败、返回 false」是错误定性。
  真正会失败的是：没有对应 handler 的 scheme（如 `ms-settings:`）、PATH 里既无 xdg-open 也无 gio、
  或上游把调用包在 `catch { }` 里把失败吞掉。
- **修法**：保留 `Helpers/OpenUrl.cs`（`xdg-open` → 失败退 `gio open`，`UseShellExecute=false` +
  `ArgumentList`，Debug 日志 + 返回 bool）作为可控路径；主程序里仍有 **10 处** `Launcher.LaunchUriAsync`
  被编译进去，其中 3 处是 `AppBackground.xaml.cs:266/462/631` 的 apps.microsoft.com（Windows 专属提示，
  Linux 上本来就没意义，保留原样），其余 7 处是 `Features/Setting/FileManageSetting.xaml.cs:100`、
  `Features/ViewHost/NoPermissionWindow.xaml.cs:106`、`Features/Setting/GeneralSetting.xaml.cs:211`（`ms-settings:`）、
  `AdvancedSetting.xaml.cs:101`、`Features/Update/UpdateWindow.xaml.cs:177`、`:726`、
  `Features/ViewHost/WelcomeWindow.xaml.cs:241`——不必因为「恒失败」去逐个改写，但要按 scheme 判定预期。
- **教训**：判「平台 API 没实现」要查到注册点/源码，不能只看 dll 里有没有 `xdg-open` 字符串——
  实现可能在别的基础库，或靠 `UseShellExecute` 间接调用。


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
- **错在哪**：用户设置 `IsGameBizSelectorPinned=True`，`GameSelector.xaml.cs:258-271` 的 `Pin()` 在固定态把 `GameIconsAreaVisible` 置真，setter 里就是 `Translation = Vector3.Zero`（赋值见 `:265` 与 `:269`）。也就是说「可见」是固定态的正常结果，无法推出任何关于 `Translation` 支持的结论。结论已撤回，规则见 D46。
- **正确的证据获取方式**（本次实测，可复用）：`spectacle -b` 截全屏 → 按窗口原点 `(360, 220)`（含 35px 系统标题栏）裁出内容区 → 对图标条做逐列采样，再用自相关找「条目间距」：
  ```python
  # 每列取图标带内的像素签名，找使两列最相似的 lag 即条目节距
  sig[x] = [sum(px(x, y)) for y in range(oy+4, oy+44)]
  for lag in range(30, 80): 计算 sum((sig[x]-sig[x+lag])**2)
  ```
  结果：最相似 lag = **50 物理像素**（= 40 DIP × 1.25）。而容器样式 `GameSelector.xaml:440` 写的是 `Padding="6,8,6,8"`（`ListViewItemPresenter.ContentMargin="{TemplateBinding Padding}"`），如果生效，节距应是 52 DIP ≈ 65 物理像素。实测节距 = 图标宽度本身 → **`ListViewItem` 的 `Padding/ContentMargin` 在这条 Uno 渲染路径上没生效**，所以三张 40px 图标是紧贴的（放大 8 倍的截图也能直接看到边缘相接、徽标压到隔壁）。
- **修法（已实施）**：不依赖 `ListViewItemPresenter.ContentMargin`，在条目根元素 `Grid_GameIcon` 上加 `Margin="0,0,8,0"`。改后用同一套像素方法复测：节距 50 → **60 物理像素**，图标之间出现 **9–10 像素纯背景列**（存证 `screenshots/m3-game-icons-spacing.png`）。
- **教训**：涉及「间距/尺寸/对齐/颜色」的结论，先截图再做像素测量，用数字说话；不要用某个像素现象去反推某个 API 是否被实现，除非有对照实验（例如同一属性在另一处生效/失效的对比）。

### L-84 · Uno X11 后端没有「隐藏」，而「唤回」要发 `_NET_ACTIVE_WINDOW`

- **症状**：点窗口管理器标题栏的 X 没有任何反应；用 `xdotool windowclose` 验证时窗口直接消失、进程还活着，看起来像「事件进不来」。
- **实测（三条，都有对照）**：
  1. `Window.Hide()`、`AppWindow.Hide()` 在 Uno 6.7.359 的 X11 后端上仍是空操作：调用后 `xwininfo` 仍是 `Map State: IsViewable`。
     源码对照：`X11WindowWrapper` 只覆写 `ShowCore`（`src/Uno.UI.Runtime.Skia.X11/UI/Xaml/Window/X11WindowWrapper.cs:107`），没有 `HideCore`；
     `AppWindow.Hide` 也还是生成桩（`src/Uno.UWP/Generated/3.0.0.0/Microsoft.UI.Windowing/AppWindow.cs:84` 只调 `TryRaiseNotImplemented`）。
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

### L-86 · Uno X11：已存在窗口重新 map 时的「激活」机制与原文不同，补发消息待复测

- **现象**：隐藏 → `--show`/托盘唤回后窗口回来了，但背景视频不恢复（一直暂停），截图差异 0.00%。
- **旧证据（当时环境）**：`[bg-diag]` 日志里只有 `activate=False hide=True`，唤回后没有 `activate=True`。
- **机制（Uno 6.7.359 源码复核）**：宿主把 X11 事件映射成托管状态——`FocusIn`/`FocusOut` → `OnNativeActivated(true/false)`，
  `VisibilityNotify` → `OnNativeVisibilityChanged(state != 2)`（`src/Uno.UI.Runtime.Skia.X11/Hosting/X11XamlRootHost.x11events.cs:202-209`）；
  `X11WindowWrapper` 再把它们落到 `ActivationState`/`IsVisible`
  （`src/Uno.UI.Runtime.Skia.X11/UI/Xaml/Window/X11WindowWrapper.cs:28`、`:103`、`:105`）。
  也就是说「重新 map 已存在窗口」**并非没有激活路径**，而是走 `VisibilityNotify`/焦点事件、语义与 `Window.Activated` 不完全等同。
  当初「不触发 `Window.Activated`」的观察可能仍然成立，但机制描述不对。
- **修法（待复测）**：`WindowEx.Show()` 在 `X11Window.Show()` 后仍自己发一条
  `MainWindowStateChangedMessage{ Activate = true }`（`Frameworks/WindowEx.Linux.cs:41-58`，与 `MainWindow.OnWindowActivated` 同语义）。
  这条补发消息**可能已经冗余**——按源码，焦点/可见性事件会带来激活；需要在 6.7.359 上重跑「隐藏→唤回」并看 `Window.Activated`
  是否自然触发，再决定删留。
- **教训**：自己接管框架缺失的能力（D47）时，也要接管**框架本该因此触发的事件**；但引擎升级后要按新源码复核旧结论，别把补丁当永久事实。

### L-87 · 米哈游背景的 `Theme` 图是纯透明文字层，静态背景必须合成

- **现象**：暂停/静态背景时左上角没有标题文字，用户以为是「海报文字丢了」。
- **证据**：下载的 `0eefc26b…webp` 2560×1440，**99.1% 像素 alpha=0**，只有标题文字不透明；上游静态分支 `DrawImage(底图)` + `DrawImage(Theme)` 两次合成。
- **修法**：`AppBackground.Linux.LoadBackgroundImageAsync` 下主题图（6 秒超时）→ SkiaSharp 合成 → PNG → `BitmapImage`；失败退回底图。
- **教训**：上游把「底图」和「文字层」拆成两张资源，只看其中一张会以为「素材缺了文字」；移植时先对齐上游的**绘制顺序**，再谈差异。

### L-88 · 图标「豆腐块」的真凶是 Uno 自带 Symbols 字体缺码位，随包字体在这套后端上不可靠

- **现象**：视频播放/暂停按钮等少数图标显示成方框或串字形，而网络/齿轮等多数图标正常。
- **取证**：`strace -f -e trace=openat ./Lunarward` → 全程没有 `Assets/Font/*`；`Uno.Fonts.Fluent/Fonts/uno-fluentui-assets.ttf`（family `Symbols`，**1395** 字形）被加载；
  用 fontTools 比对项目 104 个码位，`E62E E733 F012 F08F F413 F4A5` 不在该字体里。
- **为什么不能直接用上游字体（当时环境，现已不可复现）**：`ms-appx:///Assets/Font/Segoe Fluent Icons.ttf`（文件名带空格）与 `%20` 编码、以及复制成无空格别名 `FluentIcons.ttf`，
  三种写法都从未触发 openat；只有 `PhotosIcons.ttf`（无空格、显式 FontFamily）当时能正常加载。后来的复核（L-15）把真因定在
  **Uno 拦截 `Segoe Fluent Icons` / `Segoe MDL2 Assets` 这两个族名**：族名先被替换成内置字体，文件因此根本没被打开，
  与路径里有没有空格无关——「含空格路径加载失败」这个归因是错的。
  **注意**：`src/Lunarward/Assets/Font/` 现已不存在（只剩 `bin/` 输出里的副本），这条 PhotosIcons 对照实验今天无法按原样重做，保留作历史证据。
- **修法**：`E62E→E769`、`E917→E823`、`E733→F140`、`F012→E896`、`F08F→E768`、`F413→E74E`、`F4A5→E734`。
- **教训**：①「图标显示错」先别猜主题/字体族，直接 strace 看**实际加载了哪个 ttf**；②fontTools 可以离线渲染任意码位确认形状，比在 UI 里逐个撞快得多；
  ③字体没加载时的两个检查：strace 看有没有 openat（没有 = 路径压根没被用到），再看 FontFamily 是不是 `Segoe Fluent Icons` / `Segoe MDL2 Assets` 这类被 Uno 接管的族名——能立刻区分「引擎接管」与「路径写错」。


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
而进 `src/Lunarward` 单独构建正常——当时 `msbuild-sdks` 钉版本只写在子目录的 `global.json` 里，根 `global.json` 只有 SDK 版本、没带 `msbuild-sdks`。

**现状（2026-11-24 复核）**：根 `global.json` 现在也带 `"msbuild-sdks": { "Uno.Sdk": "6.7.45" }`，根构建正常；`src/Lunarward/global.json` 仍保留一份供单独构建。

**结论（不变）**：要给某个 SDK 钉版本，就写在「你会在哪里敲 dotnet」的那层 `global.json`；只写子目录、却在根敲 dotnet，就会 MSB4236。
另外 `.slnx` 不含 `Configurations` 时 `-c Release` 依然生效，不必手写平台矩阵。

## L-95 · `du` 大的目录不一定是构建产物（2026-11-10）

清理 `poc/uno-smoke/LunarwardPoc`（170 MB）时差点把 65 个**源文件**一起删掉——它的 `bin/obj` 占了大头，
但目录本身是受版本控制的 PoC 项目。删任何"看起来像产物"的目录前，先 `git ls-files <dir> | wc -l` 核对；
确认是产物就只删 `bin/`、`obj/`、发布输出这类子目录。删错了 `git checkout -- <dir>` 能立刻恢复。

## L-96 · 壁纸缩略图别整张解码（2026-11-10）

图库里的静态背景（webp）都是 2560×1440，图库条目只显示 216×122 DIP。`BitmapImage.SetSourceAsync` 会把整张图解成
位图（2560×1440×4 ≈ 14.7 MB/张），几十张同时在 UI 线程排队 → 打开图库明显顿。

**实测规模（2026-11-24）**：运行时图库 `~/.local/share/lunarward/bg` 共 **34 个文件** = **29 张 webp** + **5 个 webm**，
`du -sh` ≈ **150 MB**（旧文「16 张 / 约 236 MB」已过时）。视频不走 `BitmapImage`，不会按整帧位图解码，见抽帧（L-166）。

**做法**：解码前先用 SkiaSharp 缩到显示尺寸（`SKFilterQuality.High` = 三次卷积 + mipmap，缩 10 倍也不糊），
再 `SetSourceAsync`。别用最近邻（锯齿）或纯线性（摩尔纹）。

## L-97 · 程序化放大窗口后 WrapPanel 不会自己换行（2026-11-10）

窗口先按 1200 DIP 创建、再由 `ClampToScreenAsync` 撑大，`ListView` 里的 `tk:WrapPanel` 仍是首次测量的列数，
右侧留出几百 DIP 空白——**看起来像布局 bug，其实是没重新测量**。修法是订阅 `SizeChanged` 后
`ItemsPanelRoot.InvalidateMeasure()` + `InvalidateMeasure()`。通用教训：凡是「运行时改窗口尺寸」的窗口，
都要考虑布局面板是否需要显式失效。

### L-98 视频条目的缩略图在 Linux 上是黑的

历史现象：背景图库里的 `.webm` 条目只有容器底色 + 右下播放角标。当时的成因是 `Controls/CachedImage.Linux.cs` 里 `IsThumbnail`/`PngThumbnail` 只注册了依赖属性、没有实现，于是落到「直接解码 webm」路径：`SKBitmap.Decode` 抛异常 → 退回 `BitmapImage.SetSourceAsync` 也解不了。

现状（B21 已实现，本条不再描述缺陷，只留做法）：`CachedImage.Linux.cs:239-263` 的 `ReadFileAsync` 一进来就判 `IsThumbnail`——视频扩展名（`.mp4/.mkv/.webm/.mov/.avi`）走 `VideoThumbnailer.EnsureAsync`，图片走 `DecodeScaledPng`（按 `DecodePixelWidth` 缩解码，不读整张原图）。抽帧**不是** `TakeSnapshot`，而是复用 libVLC 的 vmem 回调：`player.SetVideoFormat("RV32", 480, 270, 480*4)` + `SetVideoCallbacks`（`Features/Background/VideoThumbnailer.cs:140-143`，尺寸常量 `:27-30`），锁一个帧就写 PNG。缓存落在 `AppConfig.CacheDirectory/bg-thumbs`，即 `~/.cache/lunarward/bg-thumbs`（`VideoThumbnailer.cs:44`），与 L-166 一致——不要写成 `~/.local/share`。并发用 `SemaphoreSlim(2)`，同文件任务用 `_inFlight` 去重，失败结果留 10 分钟。

### L-99 删掉卡片的显式 Width/Height 不会「自适应」，而是被内容撑开

选择层为了「按宽度均分」把 `Grid` 的 `Width="192" Height="108"` 去掉，结果卡片宽度变成背景图原始尺寸（2560 px）——`CachedImage` 是 `Image`，`Stretch` 只影响绘制，测量时仍返回图片固有尺寸。正确做法是把尺寸做成**数据**：给条目模型加 `CardWidth/CardHeight`（`ObservableObject`），模板 `x:Bind` 到它们，由容器按可用宽度写值。

### L-100 名字叫 Cache 的字段不一定是缓存

`AppConfig.CacheFolder` 是从上游继承来的名字，实际指向用户数据目录（日志、SQLite 数据库、背景下载都在里面）。往它下面加「缩略图」这种可再生物时，先分清「删了会不会丢用户数据」：纯缓存应放 `AppConfig.CacheDirectory`（`~/.cache/lunarward`），并交给 `Housekeeping` 定期清理，否则数据目录会越来越杂、备份也会把几百 MB 缩略图一起打包。

### L-101 ScrollViewer 里「星号列 + TextTrimming」压不住宽度

运行组件列表的每一行原本是「横向 StackPanel + 两个文字块 + 右侧徽标」：`StackPanel` 给子项无限宽度，`TextTrimming` 不生效，路径文字直接顶到徽标上、甚至超出窗口右边界（截图为证）。改成 `Grid`（`Auto / * / Auto / Auto`）后**仍然**顶出去——Uno 的 `ScrollViewer` 在 `HorizontalScrollMode=Disabled` 时也会用无限宽度测量内容，星号列拿不到上界。最终做法是不依赖测量：按可用像素估算可容纳字符数（12px ≈ 6.8 px/字符），用「中间省略」的字符串喂给 `TextBlock`（保留头尾，路径的尾部文件名最重要）。能用测量解决当然更好，但这条链在 Uno 上试了三种写法都不收敛，不要再花时间。

### L-102 「上次可用」要记在启动成功之后

记录「上次能用的组合」放在 `Process.Start` 之后、`process != null` 分支里，而不是预检通过时：预检只证明「看起来能跑」，真正的证据是进程起来了。这个顺序也保证「记录的一定是能用的」，用户按它切回去不会掉进另一个坏组合。

## L-103 X11 上「窗口图标」与「窗口查找」的两个坑

- Uno 的 X11 后端不写 `_NET_WM_ICON`，任务栏/Alt-Tab 只有占位方块；要自己用 `xcb_change_property` 写，数据格式 `[width, height, ...0xAARRGGBB]`、`type=XA_CARDINAL`、`format=32`。
- 写图标的时机：`Show()` 里立刻写常常无效（窗口还没 map/还没有 WM 属性），放到首次 `Activated` 之后 300ms 更稳。
- 找窗口不要只查 `_NET_CLIENT_LIST`：它只列「已被 WM 接管」的窗口，刚 map 的顶层窗口可能不在里面（本机实测：列表里只有 1 个窗口且 pid 不匹配）。兜底用 `xcb_query_tree(root)` 遍历子窗口，按 `WM_CLASS` 或 `WM_NAME` 匹配。

## L-104 前缀目录默认 0755 是个安全洞

wine 前缀里存着登录 token、cookie、注册表；新建目录默认 0755，同机其他用户可以读。创建/克隆后立刻 `chmod 0700`（`File.SetUnixFileMode`）。另外 `WINEPREFIX` 结尾多一个 `/` 会让某些 wine 构建走进 `launching from steam.exe stub!` 然后静默退出（L-92），所以路径规范化要 `TrimEnd('/')` 且保留根 `/`。

## L-105 页面「卡很久」通常是两件事叠加：每次重建 + 在 UI 线程起 wine

- Uno 的 `Frame.Navigate` 默认不缓存页面，每次切换都重新膨胀 XAML（本机实测启动器页 ctor 289–425 ms）。给页面加 `NavigationCacheMode="Required"`，再进该页就降到 24–46 ms。
- `WineRegistry.GetValue` 每次都会 spawn 一次 `wine reg query`（首次触碰前缀时还会等 wine 初始化），同步调用会把 UI 线程卡住 1 s 以上。页面初始化里的注册表读取必须整段 `await Task.Run(...)`，回来再赋属性。
- 判断「卡在布局还是卡在 IO」的办法：分别给 ctor、`OnLoaded`、`Frame.Navigated` 打时间戳。本例 ctor 132 ms 而导航 1729 ms，立刻就能排除「页面构造慢」。

## L-106 页面缓存之后，不能在 `Unloaded` 里清空 `ItemsSource`

给页面加 `NavigationCacheMode="Required"`（D60）之后，页面里的控件实例会被复用：`Unloaded → Loaded` 会再次发生，但**同一个控件**还在。`GameBannerAndPost` 原来在 `Unloaded` 里 `Banners = null; PostGroups = null;`（上游写法，页面不缓存时无害），复用时等于先把 `Pivot.ItemsSource` 清空再赋新列表，Uno 的 `Pivot` 在这种情况下不重建条目——数据侧日志明明 `banners=5 groups=3`，界面上只剩一张空卡片（真机复现，切页两次必现）。正确做法：`Unloaded` 只解注册与停计时器；数据留在内存里，靠「当前游戏 id 变了」判断是否重新拉取。

## L-107 「建议背景」失败也要缓存，否则每次切页都要等一轮超时

`AppBackground.UpdateBackgroundAsync` 每次进启动器页都会被调用（页面 `Loaded` → 设 `CurrentGameId`）。它要请求「建议背景」接口，Linux 首包 + 弱网常 5–6 秒超时，失败后走 `GetFallbackBackgroundImage` + 重算强调色。不缓存就有两个后果：切一次页等 6 秒的超时链路重新跑一遍；每跑一次都重放「换背景 + 换强调色」，而 `ChangeAppAccentColor` 会触发整棵树主题重解析 → 用户看到的是「切回启动器时闪一下、卡一下」。处理：成功与失败结果都按游戏缓存 5 分钟；强调色按「文件路径」去重，同一张图只应用一次。

## L-108 设置类页面必须给实底，否则游戏背景会毁掉可读性

启动器页的壁纸层是全窗口最底层，页面根容器只要透明，壁纸上的大字（`万序衍盈 7.2`）就会直接叠在导航文字和表单标签后面。真机看是「字压字」，不是设计。移植时凡是「表单 / 设置 / 列表」类页面，根容器都应给 `ApplicationPageBackgroundThemeBrush` 实底；只有启动器这种「展示型」页面保留背景。

## L-109 移植时要把 Windows 语义的文案和开关逐条挖出来

上游文案里混着大量 Windows 专有语义：UAC 提权（「需要以管理员权限运行」）、`ms-settings:` 深链、服务/RPC、托盘行为。它们在 Linux 上要么无效要么误导。做法：按「用户可见文案 + Hyperlink + 开关」三类扫一遍，逐条判断「保留 / 改写 / 删除」，并在代码注释里写明为什么（本仓库用 `D61` 标记）。注意 XAML 注释里的旧说明也可能被误读成功能，别拿注释当证据。

## L-110 `x:Bind` 的 bool → Visibility 在本项目可用，空态层不必写转换器

给「空态覆盖层」绑 `Visibility="{x:Bind ShowRecordEmptyState}"`（bool）直接编译通过并生效（Uno 的隐式转换）。比引入 `BoolToVisibilityConverter` 干净，但要求属性变更时 `OnPropertyChanged` 通知到该属性本身——只通知数据源属性是不够的，本例在 `CurrentRole` 的 setter 里额外 `OnPropertyChanged(nameof(ShowRecordEmptyState))`。

### L-111 页面缓存之后，「跨游戏刷新」必须靠消息，不能靠加载时机
`NavigationCacheMode` 之后 `OnNavigatedTo`/`Loaded` 只在第一次跑；改 `MainView.CurrentGameId` 不会传到页面与下挂控件。要新增一条 `CurrentGameChangedMessage`，由页面在处理里改自己的 `CurrentGameId`，下挂控件再靠属性 setter 触发重载。判断「有没有刷新」别只用日志，要截图看内容文案。

### L-112 Uno 的 Pivot：换 ItemsSource 不会重建 tab 内容
同一个 Pivot 重新赋一个全新的 `List<T>`，条目容器被复用，里面的文案还是旧的（banner 这种单值控件会更新，容易误判「已经刷新了」）。有效做法是 `ItemsSource = null` → 让出一轮布局（`await Task.Delay(60)`）→ 再赋新列表。仅 `Clear()`+`Add()` 或 `Collapsed/Visible` 切换都试过，无效。

### L-113 背景层不要每次 Loaded 都重做
`StopVideoBackground()` + 取建议背景 + 下载 + Skia 合成在页面来回切时被重复触发，实测 8–10 秒。加「同游戏守卫」（记录 `_appliedGameId`，视频在播时记 `_videoActive`）后直接返回；同时进来先用本地缓存图/兜底图填画面（2 ms），把网络放到后面。排查时用 `Stopwatch` 打在 `UpdateBackgroundAsync` 的 finally 里，能一眼看出是页面还是背景层慢。

## L-114 运行环境页：前缀大小要缓存、组件路径比较要用 `GetFullPath`

- `PrefixManager.Inspect` 对大前缀 `du` 一次要几百毫秒（`.wine` 370 MB ≈ 0.2 s，百 GB 前缀更久），进页必须缓存（10 分钟），并把「这次是缓存值」也暴露出来，否则用户会以为数字是刚算的。
- 判断「这个运行器有几个方案在用」不能只比字符串：`profile.RunnerPath` 可能带 `~`、可能带尾斜杠，必须是 `PrefixManager.ExpandPath` → `Path.GetFullPath` → 全等比较；早期直接 `==` 得到恒定 0。
- `Directory.EnumerateFiles` 要带 `IgnoreInaccessible = true` 与 `AttributesToSkip = ReparsePoint`，否则 winetricks 建出来的符号链接会把枚举拖死。

## L-115 编辑器要「页内整页」而不是再套一层对话框

- 方案编辑塞进 `ContentDialog` 时，Uno 的对话框宽度固定、ScrollViewer 高度受限，四段表单要来回滚；改成同一个页面里两块互斥的 `ScrollViewer`（列表 ↔ 编辑），体验与「独立页」一致，也不用处理导航参数与返回栈。
- `RuntimeProfile.Environment` 是 `Dictionary<string,string>`：直接绑到 TextBox 会 `CS0029`，按 `KEY=VALUE` 每行一条解析/序列化最省事。
- 图形层的「校验」必须同时比存在性与长度：只比存在时，被其它工具覆盖过的 dll 会显示「一致」，用户会以为装的是同一个版本。

## L-116 两套同义表单必然会分叉

- 方案编辑同时存在「对话框」与「页内整页」两套实现时，新字段只加在其中一套（`_editorName` 先漏、`EnsureGraphics` 只留在旧对话框），用户看到的行为取决于从哪个按钮进来。
- 死代码不占运行时间，但它占**排查时间**：`GameNoticeWindow` 四个文件加 csproj 排除项，每次改公告相关逻辑都要先确认「这坨还有没有用」。

## L-117 状态类文字不要拼到会被省略的路径行末尾

- 把「· 系统包」拼在「版本 · 路径」后面，宽窗口下看不出来，窄一点就被 `TextTrimming` 切掉——用户以为来源没显示。来源、徽标这类短标签要单独占一列（`Auto` 宽），或独立一行。
- 同义开关（`EnsureGraphics` + `GraphicsId`）一定会分叉：两个都能表达「要不要装」，结果就是一处关了一处没关。保留一个字段，旧字段只做迁移读取。


## L-118：UI 自检要拿 `MainView` 得走公开属性，`x:Name` 字段跨类不可见

写 `--selftest-ui` 时先按 `window.Content is MainView` 断言，实测 `Content` 是外层 `Grid`（MainWindow.xaml 用 Grid 包
`MainView` 做背景），而 `MainView` 实例来自 `x:Name="MainContentHost"` 生成字段——别的类访问不到。第一版 8 条断言里
4 条因此失败，看着像「页面没加载」，其实是取值路径错了。

做法：`MainWindow` 暴露 `public MainView? RootView => MainContentHost;`，自检只走公开属性。
**教训**：进程内 UI 自检断言失败时，先怀疑「我取错了对象」，再怀疑「代码坏了」。

## L-119 符号链接的 `FileInfo.Length` 是链接长度，不是目标大小

判「本地文件是否与清单一致」时写过 `info.Length == file.Size`，本机测试通过，换成
`--target` 目录（里面是 `File.CreateSymbolicLink` 产出的链接）就永远不相等：
`new FileInfo("/tmp/x/Game.exe").Length` 返回 **24**（链接路径字符串长度），目标文件是 100 字节。

正确写法：`File.ResolveLinkTarget(path, returnFinalTarget: true) is FileInfo target ? target.Length : info.Length`。

出现方式很隐蔽——功能本身没问题，只有「重复执行同一操作」时才会暴露（L2 的 T-IN-23 就是幂等用例）。

## L-173 · `Auto` 行不随「异步变可见」重算，表现为两块内容重叠（原编号 L-119，重号已改）

- **现象**：游戏设置页滚动到底，「运行配置」标题与 HDR 段落画在同一坐标；改窗口尺寸、最小化恢复都不消失（排除重绘残留）。
- **根因**：行高 `Auto` 先按「子项都 Collapsed」测出，随后 `Visibility`/绑定在异步初始化里变可见，Uno 未重新测量行高。
- **判据**：用两种「强制整体重排」的动作（改尺寸、最小化恢复）对比——仍重叠就是布局，不是重绘。
- **修法**：初始化结束 `DispatcherQueue.TryEnqueue(() => grid.InvalidateMeasure())`。
- **教训**：先测像素再定方向。本次先后怀疑「横向无限测量」与「丙烯半透明透出」，都被截图/逐像素比较否掉（`Clip` 加了以后逐像素相同）。

## L-120 · 「画面静止」先分级，再怀疑渲染

**症状**：视频壁纸不动。

**三级判据（先测，不要先猜）**

1. `ls -l /proc/<pid>/fd | grep webm`：没有句柄 → 播放器根本没起（或已被停）。
2. `/proc/<pid>/stat` 的 utime+stime 5 秒增量：≈0 核 → 没在解码；≈1 核 → 在解码但画布没显示。
3. 两张间隔 1.5 s 的截图逐像素比较：0 差异 + 上面两条 → 静态图在显示，不是「帧不重绘」。

**本例的真相**：`BackgroundService.GetSuggestedGameBackgroundAsync` 在没有新背景时把上次背景复用并置 `StopVideo = true`（上游「看过的背景不再播视频」），所以「静止」是设计行为；真正的实现缺陷只有「调试入口不记状态导致重复起播」。

## L-121 判断「某项功能坏了」之前，先核对它依赖的前提状态

F-21 的两次误判都栽在同一处：没有先确认「当前是哪个游戏、它的背景记录是什么、上游在这种状态下期望什么表现」，就按「我上次看到的是动态 → 现在不动 → 移植弄坏了」下结论。

可复用的检查顺序：①当前选中的游戏/配置（本机是 SQLite `Setting` 的 `CurrentGameBiz`）→ ②相关持久化状态（`bg_<biz>`、`game_background_ids_<biz>`）→ ③上游同名逻辑在该状态下会怎么走（读 `src/Starward` 对应文件，而不是凭印象）→ ④才轮到「移植实现有问题」。四步里任何一步没做，结论都只能写成假设。

## L-122 我们自己的代码里不该有「无痕的 catch」

全仓搜 `catch {}` / `catch (Exception) {}`（不含 `obj/`、`bin/`）共 **361 处**：`src/Lunarward` **185 处**、`src/Starward`（上游共享文件，按零偏差铁律不动）**176 处**；`src/Lunarward` 里 `*.Linux.cs` 只有 **2 处**（`Features/Screenshot/ScreenCapture.Linux.cs:161`、`:183`，都是清理动作的兜底，保持无痕合理）。旧文「187 处 / 183 在上游 / `*.Linux.cs` 4 处」是过期数字。

另 2 处是真问题，且已修：`GameInstallScanner.Linux.cs:136` 的「重复安装路径去重」原先整段被吞（会出现「明明装了却认不到、日志里一点线索都没有」），现在 `LogWarning` 且不影响扫描结果；`GameInstallService.Linux.cs:76` 与 `:169` 的 `cts.Cancel()` 原先 catch-all，现在只吞 `ObjectDisposedException`、其余记 Warning。

可复用的判断：**自己能容忍失败的动作才允许无痕**（清理、兜底渲染）；**影响结论的动作必须留痕**（扫描、探测、状态判定）。

## L-123 在 kwin-mcp 隔离会话里跑 X11-only 应用的方法

本应用是 Uno X11 主机（`Program.Linux.cs` 里 `.UseX11()`，Uno 6.7 没有 Wayland 后端），而 `kwin-mcp` 的 `session_start` 起的是**纯 Wayland** 隔离会话，直接 `launch_app` 会报 `No platform host could be selected`。正确做法：

1. `session_start`（隔离会话，屏幕 1600×900 即可）；
2. 在会话内起一个 X 服务器：`launch_app("Xwayland :5 -noreset -decorate")`，等 `/tmp/.X11-unix/X5` 出现（`/tmp/.X1-lock` 之类残留会报 `Server is already active`，换号或删锁）；
3. 再把 `DISPLAY=:5` 通过 `env` 传给 `launch_app` 启动应用。

这样窗口、点击、截图全在隔离会话内部，不会出现在用户桌面上，也不抢用户键鼠。判据：`launch_app` 返回的 app log 无 `No platform host could be selected`，且 `screenshot` 里能看到窗口。

## L-124 kwin-mcp 隔离会话是嵌套合成器，代价是 ~35% CPU

`kwin_wayland --virtual --width 1920 --height 1080` 实测 ≈28% CPU，会话内 `Xwayland` ≈8%，且画面要被用户主合成器再合成一次（等效双重合成）。用户能直接感知为「桌面每 10 秒卡 1–2 秒」，停止会话后立刻消失。

所以：GUI 自动化是**有成本的诊断手段**，不是默认手段。能用应用内日志（`[tempdiag]` + 跑完删除）量到的，就不要开合成会话；非开不可时按「短测 → 立即 `session_stop`」执行，并避开用户玩游戏/看视频的时段。

## L-125 超时阈值要按「最慢的正常路径」定，不要按平均值

视频看门狗原定 2 秒无帧即告警，实测首帧要 2.0–3.0 秒（VLC 缓冲 1033 ms + VP9 1080p 解码 + 8 MB 整帧拷贝），于是正常起播每次都触发 `video started but no frame in 2000 ms`。我据此写下「视频回归（F-21）」的结论，并把两次 0.00% 帧差当成证据——其实截图分别在切换后 2.5 s / 4.5 s 取的，都在首帧之前。

真机复测（`cu_*`，1200×676）：启动即播 48.13%、会话中切换 44.79%、干净构建重启 39.70%，三次都正常。

教训：①阈值取「正常路径最慢值 × 1.5」而不是平均；②「没帧」这类否定证据必须配合「等了足够久」的条件，否则量的是自己的采样太早；③判定回归前先验证同一环境下的正例（这次是 `poc/video-spike` 直接跑出 `frames=12`）。


## L-126 · `Pivot` 换 `ItemsSource` 抛 `ArgumentOutOfRangeException`（Uno 顺序缺陷）

**现象**：切游戏时日志稳定出现

```
System.ArgumentOutOfRangeException: Index was out of range ... (Parameter 'index')
  at Microsoft.UI.Xaml.Controls.Pivot.SynchronizeSelectedItem() ... Pivot.cs:line 301
  at Microsoft.UI.Xaml.Controls.Pivot.UpdateItems(...)
  at Microsoft.UI.Xaml.Controls.Pivot.OnItemsSourceChanged(...)
  at Starward.Features.GameLauncher.GameBannerAndPost.UpdateGameContentAsync()
```

**根因**：Uno 的 `SynchronizeSelectedItem` 守卫是 `SelectedIndex != -1 && HasItems`，然后直接索引 `_staticHeader.Children[selectedIndex]`；换数据源瞬间 `HasItems` 已真、header 容器还没重建。

**后果**：异常从 `PostGroups` 赋值处抛出，后面 `ShowBannerAndPost = true` 执行不到，横幅/资讯区可见性停留在上一个游戏；日志每次刷 ~45 行堆栈。

**解法**：赋值前后把 `SelectedIndex` 设成 `-1`（改完再设回 0）。`-1` 命中守卫第一项直接返回。

**验证方法**：`grep -c ArgumentOutOfRangeException ~/.local/share/lunarward/log/Starward_*.log` 在切游戏前后计数，修复后不再增长。


## L-127 · 在 UI 线程上 fork 外部进程 = 整窗静止（wine 冷启动 4 秒）

**现象**：切到原神/星穹/崩坏3 时整窗静止 4 秒（鼠标能动、点什么都没反应），切绝区零秒切。

**根因**：`WineRegistry.GetValue` 同步 `Process.Start("wine","reg","query",…)` + `WaitForExit()`；`wine` 在 `wineserver` 未起时要 4 秒，而它在 UI 线程上跑。绝区零不卡是因为那条代码路径对 `nap` 直接返回 0。

**教训**：
- 判断「卡顿」不要看日志停不停（日志停只说明没有新事件），用看门狗（`queue.TryEnqueue` + 计时）测 UI 线程真实占用。
- 任何 `Process.Start` + `WaitForExit` 只能出现在后台线程；UI 路径必须 `WaitForExitAsync`。
- 捕获式 API 要提供异步重载并且**调用方一起改**，只加异步版不换调用方等于没改。
- `[ThreadStatic]` 的隐式上下文（这里是 `UsePrefix`）在 `async` 里会失效，必须在第一个 `await` 之前取出来用参数传。


## L-128 · `wine reg import` 不吃没有 BOM 的 UTF-16LE 文件（而且不报错）

**现象**：手写的 `.reg`（UTF-16LE 无 BOM）`wine reg import` 返回 0，随后 `reg query` 说「找不到指定的注册表项或值」。

**原因**：`reg.exe` 靠 BOM 判断「这是 UTF-16」，没有 BOM 就按 ANSI 解析，整份文件被当成无效内容跳过，退出码仍是 0。

**做法**：写 `.reg` 时显式拼 `Encoding.Unicode.GetPreamble() + Encoding.Unicode.GetBytes(text)` 落盘（`File.WriteAllBytesAsync`），不要指望默认行为；写完用一次 `reg query` 回读确认。


## L-129 · 「该异步没异步」要按类清，不要只修发现的那一处

**现象**：修完 `WineRegistry` 的 4 秒卡顿后，同类问题还有三处（导入图形组件同步拷 100 MB、`EnsureInstalled` 拷 DLL、`ComponentStore` 首次扫 PATH/Steam 库），都属于「一次点击卡一下」。

**做法**：按六类逐类搜，别凭印象——
1. 同步等异步：`.Result` / `.Wait()` / `.GetAwaiter().GetResult()`；
2. 同步子进程：`Process.Start` + `WaitForExit()`（只搜 `WaitForExit()` 就够，`Process.Start` 多数是 fire-and-forget）；
3. 同步目录/文件 IO：`Directory.GetFiles(...AllDirectories)`、大文件 `File.Copy`；
4. 同步网络；5. 静态构造里的阻塞；6. `[ThreadStatic]` 跨 `await`。

**判断标准**（本次结论）：CLI 主线程、退出路径、`<Compile Remove>` 掉的死文件、先判 `IsCompletedSuccessfully` 的取结果、异步续体里的 `.Result`、解析 1 px 图的静态构造——都可以留着；只有「UI 线程 + 可能上百毫秒」才必须改。

**改法固定**：调用点包 `await Task.Run(...)`；页面初始化改成 `private void X() { _ = XAsync(); }` + `XAsync` 里先 `Task.Run` 取数据、`await` 之后再碰控件。

### L-130 · 复核结论也要自己验证；`partial` 不一定是装饰

- 复核结论来自 diff + 静态阅读时，「这行没有作用」这类判断必须跑一次反向验证（去掉再编译）。
- 实例：`src/Lunarward/Helpers/PointerCursor.cs:7` 的 `partial` 被复核判为「无伙伴文件、纯装饰，建议回退」，实际去掉后编译报 `CS0260 类型缺少 partial 修饰符；存在此类型的其他分部声明`。同一编译单元里另有一处分部声明，来源尚未查明（`Compile` 项只列出本文件一份），因此这一行是必需的。
- 推论：把 `partial`、`using`、`#pragma` 这类「看起来没作用」的行当作可清理的噪声很危险，尤其在本项目这种「上游源码 + 移植目录同名单文件并存」的结构下。

### L-131 · 一次性成本要分层打点，别拿单个看门狗数字当根因

- 冷启动 1.04 s、游戏设置对话框首次 170 ms，前者由 XAML 通胀 + 首帧布局/光栅两段构成，后者是「通胀 81 ms + 首帧 89 ms」；只有分层打点（ctor / InitializeComponent done / 首次 `LayoutUpdated`）才能说清钱花在哪。
- 看门狗只回答「UI 线程被占住了多久」，不回答「谁占的」；`dotnet-stack` 只回答「采样那一瞬间在谁身上」。三者要给同一条时间线对齐才有结论。
- 反例教训：`AppBackground.xaml.cs` 里的看门狗初始化写了却没日志——因为该文件在 `<Compile Remove>` 里根本不编译（L-41 家族），这种「什么都没发生」最容易被误读成「功能不生效」。

### L-132 · 旧结论会过期：判定「平台不支持某能力」要重新查程序集

- L-24 当年记「Uno 没有 `IsTextTrimmedChanged`」，据此把 7 处 XAML 的事件删除。本轮复核发现 Uno 6.7.359 的程序集里**已有** `add_IsTextTrimmedChanged` / `IsTextTrimmedChangedEventArgs` / `get_IsTextTrimmed`，且自检实测 Skia 后端**会触发**该事件 → 7 处全部回加（`ScreenshotSetting` / `GachaStatsCard` / `ZZZGachaStatsCard` / `InstallGameDialog` / 三个 StarRail 战绩页），恢复「长文案被截断时缩字号」的自适应。
- 推论：lessons 里的「框架不支持 X」要带版本号，并在升级 Uno 时复查；判定能力是否存在用 `strings`/`ildasm` 查程序集，比查旧结论可靠。
- 附带一条：上游实现所在 `.xaml.cs` 被 `<Compile Remove>` 时，事件处理器要在 `*.Linux.cs` 里补**真实逻辑**而不是空 stub，否则事件会触发但什么也不做（`ScreenshotSetting.Linux.cs` 与 `InstallGameDialog.Linux.cs` 本轮各补一处）。

### L-133 · Uno `Pivot` 换 `ItemsSource` 的两种崩法：越界与 NRE

- 真机触发：切游戏时 `GameBannerAndPost.UpdateGameContentAsync` 换 `Pivot_Posts.ItemsSource`，`Pivot.SynchronizeSelectedItem()`（6.7 的 `Pivot.cs:295-301`）在 `SelectedIndex >= 0` 时用夹取后的索引去取 `_staticHeader.Children[...]`：容器已重建→`ArgumentOutOfRangeException`，容器尚未重建→`NullReferenceException`（同一方法，`Pivot.cs:301`）。
- 最小复现（`--selftest-ui` 里现成可跑）：
  ```csharp
  pivot.ItemsSource = new List<string> { "a", "b", "c" };
  pivot.SelectedIndex = 2;
  pivot.ItemsSource = new List<string> { "x" };   // ← 崩
  ```
- 约定顺序（`GameBannerAndPost` 与自检第 10 条都按这个写）：
  `SelectedIndex = -1` → `ItemsSource = null` → 赋新集合 → `SelectedIndex = 0`。
- 教训：框架方法里的 `HasItems` 这类守卫不等于安全，真正的崩溃点是它后面那行取头；这类问题要用**最小复现**定稿，再把它钉成 L1.5 断言（RG-01），否则下次重构又会把 `-1` 那两行当冗余删掉。

## L-134 自检断言不要依赖「可选事件是否触发」

- 现象：`IsTextTrimmedChanged` 的自检在真机（Debug、1536×864、Skia）稳定 PASS，在 CI（Release、xvfb 1280×800）稳定失败 —— `1.5 s 内没有触发`，把一个**功能可用**的提交判成红。
- 判断依据：这个事件不是平台契约，只有「文字真的被截断」时才发；xvfb 的布局与字体度量不同，可能整段都能放下，于是永不触发。功能本身（字号 -1）是渐进增强，缺失只是不变小，不影响正确性。
- 做法：自检只断言**可控部分**——挂事件、强制 `UpdateLayout()`、改宽度再改回来、600 ms 内不许抛异常；「触发 / 未触发」写进 detail 供人工核对。
- 通用规则：L1.5 断言要么钉「顺序 / 不抛异常 / 内容存在」，要么钉「数值区间」；凡是「某个平台事件必须发生」的，只能进 L3 人工项。

## L-135 基线文件的格式漂移会让「比较」变成永真

- 现象：`scripts/test/perf.sh` 的 `--update` 写的是 `{"envs": {"<uname -sr>": {...}}}`，而仓库里的 `perf-baseline.json` 还是早期的扁平结构 `{"list-runners-hot":413,...}`。比较分支只查 `envs[host]`，取不到就打印「本次只记录，不判定」并 `exit 0` —— **CI 里那条 Perf smoke 其实从来没红过**，灯是绿的但没在测。
- 另一层：脚本依赖 `/usr/bin/time -v` 取峰值 RSS，Arch 默认不装 `time` 包 → 每次都 `self.time` 不存在、`awk` 报错、`peak_rss_kb` 恒为空（同样静默）。
- 修法：①比较分支兼容扁平基线（`if base is None and "cli_median_ms" in data: base = {...}`）；②`/usr/bin/time` 不存在时只测耗时、RSS 记 n/a；③新增 `startup_stall_ms` 并配**绝对上限 1500 ms**（UX 硬指标不该跟某台机器的基线比）；④基线重新生成，不带 null 的空指标。
- **残余机制（2026-11-24 修，见 D125）**：`scripts/test/perf.sh` 原用 `$(uname -sr)` 当 key（当时基线键 `Linux 7.3.3-1-cachyos-bore-lto`，机器已升到 `Linux 7.3.5-2-cachyos-bore-lto`），取不到就打印「本次只记录，不判定」并 `exit 0`——**内核小版本一升，Perf smoke 就静默空转**。修法：①key 改成 `Linux 7.3 x86_64`（`uname -s` + 内核主次版本 + 架构），比较时先精确匹配、再按旧 key、最后按「同名内核主次版本」兜底，命中会打印用的是哪个 key；②`--update` 写新 key 时顺手清掉同系列旧 key，基线不会一台机器两条记录；③`perf-baseline.json` 已按新 key 重建（`kernel` 字段留原始完整版本）；④**失败路径实测过**：把基线 `cli_median_ms` 改成 0.5 再跑，输出 `cli_median_ms 0.5 → 2 +300% REGRESSION` 且退出码 1。
- 通用规则：任何「有基线就比、没基线就过」的脚本，必须在**第一次落地时**确认它真的会红——制造一次回归（临时把阈值调到 1）验证失败路径，否则绿灯没有意义。


## L-136 链接源码的测试工程，覆盖率分母只看「链接进来的文件」

- 现象：新增 `ComponentDownloader.cs`（现 **650 行**，含 HEAD 探测、分片并发、单流降级、合并校验）后被链接进测试工程，覆盖率从 90.59% 掉到 **74.42%**（1164/1564），CI 的 85% 门槛直接红；而这些行里真正无法离线覆盖的只有「http 请求 + 文件搬运」。
- 修法：把**纯算术/纯字符串**抽成 `src/Lunarward/Features/Runtime/ComponentDownloadMath.cs`（`ChunkCount`/`PlanChunk`/`PartPath`/`ApplyMirror`），测试工程改为链接 `ComponentDownloadMath.cs` + `ComponentSources.cs`（以及其余纯逻辑文件），**不再链接 `ComponentDownloader.cs`**；网络编排由手工实测（`--component-download dxvk` → 4 连接 / 18,041,512 字节 / digest 一致）+ `cli-extra` 兜。
- 通用规则：①覆盖率只统计链接集，链接边界就是覆盖边界——把 IO 编排挡在链接集外、把判定逻辑抽出来，比给网络时序写 mock 划算；②一旦把某个文件移出链接集，必须在文档里写明「这部分靠什么验证」，否则等于悄悄降低门槛；③`coverage.sh` 报告要能区分「行覆盖」和「分支覆盖」，当时一轮是 87.44% / 66.16%（34 个类）。**现行数字（D122 新口径，2026-11-24）**：整体 line-rate **85.30%**，门限 85%（`scripts/test/coverage.sh 85`），只比门限高 0.3 个点；分母 = 链接进测试工程的 src 文件，不含未链接的主程序 UI/服务代码。


## L-137 改代码前先确认「要替换的那段区间里还有什么」

- 现象：用一段 `python` 脚本按「从 `ApplyMirror` 到 `DownloadChunkedAsync`」整段替换时，夹在中间的 `ProbeAsync`（HEAD 探测）和 `RangeRejectedException` 定义一起被删，编译报 `CS0103 ProbeAsync` / `CS0246 RangeRejectedException`，而下载器逻辑本身看不出问题。
- 修法：先 `grep -n` 打印两端锚点之间的成员清单再替换；替换后立即 `dotnet build`（不要攒着改完再编）。
- 通用规则：脚本化改代码时，锚点只负责定位，落盘的内容必须以「该区间原有全部成员」为基线做增删，而不是只写想加的那部分。


## L-138 「自动注入的 env」不是冲突

- 现象：`LaunchRecipeBuilder.Build` 一开始把「自动注入的 `WINEPREFIX`」也算进覆盖清单，于是每次启动都打一行「环境变量 WINEPREFIX 被覆盖」，而用户什么都没配。
- 修法：只在**配方自己显式写了**同名键且值不同时才记 `EnvOverride`；自动层与配方层值相同也不记。
- 通用规则：三层 env（自动注入 → 配方 → 进程）里，「来源不同」不等于「冲突」，只有「同一键在不同层有不同值」才是冲突。判断冲突要比值，不要比来源。

## L-139 迁移没清干净 = 每次启动都写回旧字段

- 现象：迁移后 `profiles/<id>.json` 里仍一直出现 `EnsureGraphics: true` 和 `Environment: {}`，看起来像迁移没生效。
- 根因：`LegacyEnsureGraphics` 是 `bool`，默认 `false`，但旧文件里是 `true`，所以每次序列化都写回；`LegacyEnvironment` 是空字典同理。
- 修法：`LegacyEnsureGraphics` 改 `bool?`（`null` = 已迁移，配 `JsonIgnoreCondition.WhenWritingNull` 不落盘）；`LegacyEnvironment` 迁移后置 `null`，空字典不能留。
- 通用规则：做「旧字段 → 新字段」迁移时，迁移完成后必须把旧字段变成「不存在」而不是「默认值」，否则序列化会把它写回去，形成永久残留。

## L-140 预览与实际启动必须共用同一个构造函数

- 现象：编辑器里的「启动预览」和真正启动是两段代码时，改了一处另处不改，用户看到「预览说 A、启动做 B」。
- 修法：`LaunchRecipeBuilder.Build` 是唯一入口，启动、预览、`--launch-plan` 三处都调它；冲突检测也在里面做。
- 通用规则：任何「给用户看的预测」都必须来自「真正执行的那份数据」，不要复制一份生成逻辑。

## L-141 代码里 new 出来的多行 TextBox 在 Skia 后端只画第一行

- 现象：「配置文本（只读）」对话框里的 JSON 只显示了一个 `{`，其余行全空；点「复制」拿到的却是完整 JSON → 说明数据正确，是渲染问题。
- 试过并排除：给 `TextBox` 补 `Height=320` 无效；去掉 `FontFamily("monospace")` 也无效（`fc-match monospace` 实际解析到 `Maple Mono Normal NF CN`，族名是有效的，所以这条排除与字体无关，别误记成「本机没有 monospace」）。
- 做法：只读展示改用 `ScrollViewer` + `TextBlock`（`IsTextSelectionEnabled=true`、`TextWrapping=NoWrap`、横竖滚动条 Auto），高度给在 ScrollViewer 上 → 14 行 JSON 完整显示。
- 通用规则：Uno/Skia 下「代码创建 + `AcceptsReturn` 的多行 `TextBox`」不可靠，展示类场景直接用 `TextBlock`；要可编辑再考虑 XAML 里声明（XAML 声明的多行 TextBox 目前正常）。

## L-142 截图坐标在不同后端之间不通用，滚动/键盘行为也不一样

- 现象：`cu_screenshot`（spectacle 后端）返回的是**物理像素**（1500×880，`frame` 是 1200×704 逻辑像素，scale 1.25），直接拿截图像素当全局坐标点会偏；另外同一个 ScrollViewer 上「鼠标滚轮」时灵时不灵，而 `Page_Down` / `Tab` 触发 focus-into-view 一定有效。
- 做法：全局坐标 = `frame.origin + 截图像素 / scale`；滚动优先用 `Page_Down`/`Page_Up`/`End`，滚轮只在必要时用，且每次先重新取图确认位置。
- 通用规则：用截图定位点击前，先确认「截图尺寸是否等于窗口逻辑尺寸」，不等就必须按 scale 换算；页面滚动优先键盘，不要假设滚轮一次一定生效。

### L-143 GitHub API 匿名限流会把「网络能用」伪装成「源里没东西」

- 现象：`--component-list dxvk` 与页面「加载版本列表」都返回空，界面说「这个源没有可用归档」；实际是 `HTTP 403 API rate limit exceeded for <共享出口 IP>`。匿名限额每 IP 每小时 60 次，用代理/NAT 的用户极容易踩满，而 `curl` 直接打同一个仓库也一样 403。
- 处理：①`ListAssetsAsync` 不再把非 2xx 吞成「空列表」，而是抛 `ComponentSourceException`（消息是 `DescribeFailure` 生成的人话）；②`403/429` 的文案直接说清「限流 + 两条出路（`LUNARWARD_GITHUB_TOKEN` / 镜像前缀）+ 原文」；③`404` 提示核对 `Owner/Repo`；④其余状态码保留原文前 200 字。
- 教训：**「没有候选」和「没拿到候选」是两件事**，前者是数据问题、后者是环境问题，界面别用一个说法糊过去。打不通时要让人知道「打不通」以及「怎么办」。

### L-144 解压可以进程内做，别为图省事 fork `tar`

- 需求：下载必须能取消，而「取消」若解压仍由外部 `tar`/`xz` 子进程完成，就只是取消了一半（子进程不跟 `CancellationToken`，还得杀进程树，且违反硬规则 10）。
- 事实（实测，2026-11-14）：BCL `System.Formats.Tar.TarReader` 只处理未压缩 tar 流，`.tar.gz` 套 `GZipStream` 即可，`.tar.zst` 套 `ZstdSharp.DecompressionStream`（项目已有）即可，**`.tar.xz` 没有 BCL 方案** → 用 `SharpCompress` 的 `ReaderFactory.Open(...)` + `MoveToNextEntry`/`WriteEntryTo`（1.0.0，MIT，活跃）。`.zip` 走 `System.IO.Compression.ZipFile`。
- 路径安全必须在解压**之前**判定：`ResolveTarget` 对绝对路径与含 `..` 的条目直接抛 `InvalidDataException`，不要指望「解完再查」。
- 结果：三种格式（`.tar.gz` / `.tar.xz` / `.tar.zst`）真机导入均成功，且都能被同一个 `IProgress<ArchiveProgress>` 报进度、被同一个 token 打断。

## L-145 「portal 优先」不是教条，静默性优先于沙盒纯度

- 现象：24 号 P2/P3 把 `xdg-open` / `kdialog` 换成 xdg-desktop-portal 之后，要不要连截图也一起 portal 化？
- 判定：**分两类对待**。
  - 打开链接、选文件 → portal 优先。沙盒里 fork 的外部命令看不到宿主文件系统；原生安装下 portal 也几乎必然存在，成本只是几十毫秒 D-Bus 往返。
  - 截图 → 外部工具优先、portal 兜底。`spectacle -b -n -f` 是静默全屏，热键按一次就出图；portal 的 `Screenshot` 在部分桌面会弹授权/选区，把「静默」变成「每次点一下」。沙盒里天然没有 spectacle，会自动落到 portal。
- 教训：换依赖时先问「这个调用的关键体验是什么」。截图的关键体验是「按下就出图」，不是「走官方通道」。

## L-146 Skia 没有 AVIF/JXL delegate；Magick.NET 的 native 是按 RID 打包的

- `SKCodec.Create` 对 `.avif` / `.jxl` 返回 null（SkiaSharp 默认不带这两个解码器），表现为「图片打开是空白」而不是报错。
- Magick.NET 的 `MagickNET.SupportedFormats` 里格式名是 `Avif` / `Jxl`（不是 `AVIF`），用 `Contains("AVIF")` 过滤会得到空列表 → 会误判成「不支持」。
- `Magick.NET-Q8-AnyCPU` 的 nupkg 里带 linux-x64 / linux-musl-x64 / linux-arm64 / linux-musl-arm64 四份 native（输出目录约 146 MB），`publish -r linux-x64` 只保留一份（约 40 MB）。选 AnyCPU 是为了让 AUR 在 aarch64 上源码构建也能跑，体积代价记在 24 号。
- 用法：`ImageDecode.Load(path)` 统一入口 —— 先 Skia，解不开且扩展名属于 `.avif/.jxl/.heic` 才走 Magick；纯扩展名判断放在 `MagickCodec.NeedsMagick`，便于单测。

## L-147 Esc 的语义是「退一层」，不是「关窗口」

- 现象：游戏选择层展开时按 Esc，窗口直接消失到托盘（用户以为程序崩了）。
- 修法：`Content_KeyDown` 里先问 `MainContentHost.TryCloseGameSelector()`（先关打开的下拉/浮出 Popup，再收选择层），都没有才 `Hide()`；`GameSelector.CloseLayer()` 复用原来的 `HideFullBackground()`。
- 教训：桌面应用里 Esc 是「退一层」；把 Esc 直接绑到窗口隐藏，等于把「返回」和「退出」混成一个键。

## L-148 半透明弹层在 Skia 上是「脏」而不是「毛玻璃」

- 现象：`AcrylicBrush` 在 Windows 上靠系统模糊，在 Skia 后端只是把 `TintColor` 按 `TintOpacity` 叠上去 —— 背景壁纸的文字直接透到对话框里，浅色壁纸区灰白字几乎读不清。
- 修法：给弹层/对话框单独一套**不透明表面** `LunarwardSurfaceBrush`（Dark `#FF202020` / Light `#FFF3F3F3`），`ContentDialog`、`FlyoutPresenter`、`MenuFlyoutPresenterBackground`、`GameLauncherSettingDialog` 都换成它；页面里叠在应用背景上的面板仍保留原来的半透明观感（那里没有「文字压在别的文字上」的问题）。
- 教训：判断标准不是「像不像 Windows」，而是「文字背后有没有别的内容」。有就必须实底。

## L-149 用 .NET 绑定 GTK4/WebKitGTK 6.0 的三个硬点

- **库名对不上**：GirCore 的 `DllImport` 名是 `WebKit` / `JavaScriptCore`，glibc 会去找 `libWebKit.so` → `System.DllNotFoundException`。要么在输出目录建 `libWebKit.so` 符号链接，要么 `NativeLibrary.SetDllImportResolver(typeof(WebKit.WebView).Assembly, Resolve)` 映射到 `libwebkitgtk-6.0.so.4` / `libjavascriptcoregtk-6.0.so.1`（与 L-57 的 libzstd 同类）。
- **cookie 没有异步入口**：GirCore 0.8.1 的 `CookieManager` 只生成了 `GetCookiesFinish` / `GetAllCookiesFinish`（返回非泛型 `List`），能用的路径是 `SetPersistentStorage(path, CookiePersistentStorage.Sqlite)` + 自己读 `moz_cookies`（`SELECT name, value`）。读库要容忍 `SqliteException`（WebKit 正在写时短暂锁库），下一轮再试即可。
- **与 Uno 撞名**：Uno 自带 GtkSharp 3.24（GTK3），同进程引 GTK4 必然 `CS0433`（`Gtk.Application`、`WebView` 等）；跨程序集要用 `extern alias gir;` + `using Gtk4 = gir::Gtk;`。所以「登录窗放独立进程」不只是观感问题，也是依赖隔离问题。
- 附带：GirCore 里所有构造函数都是 `ConstructArgument[]`，一律走静态工厂（`Gtk.Application.New(appId, flags)`、`ApplicationWindow.New(app)`）；全局函数在 `GLib.Functions` / `WebKit.Functions`；`Gio.ApplicationFlags` 没有 `None`，传 `0`。

## L-150 运行环境收尾三坑：可删范围、绑定残留、解析不一致

- **「可删」必须显式定义范围**：`components/wine`、`components/dxvk` 这类分类目录与组件目录是**同一种形状**（都是 `ComponentsFolder` 下一层）。只判断「在管理根之下」会把整个运行器库判成可删。定法：受管根 = `components/<分类>/<目录>` 这一层，且目录名本身不能等于分类名（`wine`/`dxvk`/`vkd3d-proton`/`graphics` 一律 false），上溯最多 4 层。
- **删环境必须连带清绑定**：`ProfileBinding.Resolve` 在未绑定/指向已删环境时**不是静默**：会用 `Helpers.OneShotHints.TryTake("profile-binding-dangling")` 只提示一次并写 Serilog Warning（`Features/Runtime/ProfileBinding.cs:337`）；未绑定时优先取 `RuntimeDefaults.DefaultProfileId`（R68，`:344`），都没有才退回第一份（L-62 的兜底）。所以「删了环境但绑定还在」只表现为一次 warning + 界面照常，直到启动游戏用了另一套参数。删文件后仍必须 `ClearFor(id)`，并把被清的条数写进 toast/日志。
- **诊断命令要和真启动走同一条解析路径**：`--launch-plan` 走的是 `ProfileBinding.Resolve`，真启动本轮改成了 `EnsureFor`（会建环境）。两者不一致时，「计划里好好的、真启动多出一份环境」。修法：解析差异只允许在「写」这一侧（`EnsureFor` 需要 `ILogger`、会落盘），并给读侧一个可观测入口 `--profile-ensure` 让脚本盯住三种分支。
- 附带：xunit 工程用 `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>` + 逐个 `<Compile Include>` 链接源文件（D37），`GameBiz`/`ProfileBinding`/`RuntimeProfileStore` 这条链拉不进单测——这类文件系统行为走 L2 CLI 夹具（`T-CL-45..47`），别硬塞进 xunit。

## L-151 依赖批次的四个坑：帮助两处手写、VDF 的 Children 形态、PeNet 的 IsPeFile、libuiohook 收不到 XTEST

- **帮助文本和分支表分开写必然漂移**：`--help` 是一段手写字符串，`Program.Linux.cs` 现有 **48 个** `args[0] is "--x"`，两边靠人记。一批里踩两次：`--profile-ensure` 加完帮助里没有；`--preflight` 描述停在「六项」（实际九项，现在十项）。修法不是「认真点」，而是让「帮助」从声明表生成（`Helpers/CliSpec.cs`，D79），再用 lint 卡「源码开关 ⊆ 表」。**凡是同一份信息要写两遍，第二遍就会过期。**
- **`KVObject.Children` 是 `IEnumerable<KeyValuePair<string, KVObject>>`**（ValveKeyValue 0.80）：`foreach (var child in root)` 拿到的是 `KVObject`，要 `kvp.Value`；`foreach (var child in root.Children)` 才是键值对。另外旧版 `libraryfolders.vdf` 的节点是 `"1" "/path"`（key 是数字、值是字符串），新版是 `"1" { "path" "/path" }`——两种都要兼容，判断依据是 key 是否全数字，取字符串值靠 `(string)obj` 隐式转换（`op_Implicit(string)` 存在，但压缩过的二进制 VDF 不能这么读）。
- **`PeFile.IsPeFile` 是带参方法不是属性**（PeNet 6.1.2）：写成 `pe.IsPeFile` 会编译不过或语义错（`IsPeFile(int)` 之类），判断是否 PE 直接 `new PeFile(path)` 抛不抛 `PeNetParsingException` 更省事。machine 走 `pe.ImageNtHeaders?.FileHeader.Machine`（`I386` / `Amd64` / `Arm64`）。
- **SharpHook 在 KDE Wayland 上收不到 XTEST 事件**（实测，2026-11-15）：`TaskPoolGlobalHook` 打印 enabled，12 秒内 0 个事件；`xdotool key`（XTEST）与 `env -u WAYLAND_DISPLAY` 强制 X11 后端都一样。对照：自写 `XQueryKeymap` 轮询能看到 `xdotool keydown shift` → `keys=1`。所以「X 客户端看得见的事件」不等于「libuiohook/XRecord 拿得到」，**换库前必须用对照实验定位到哪一层断了**，否则会把「这库不行」和「我的用法不行」混在一起。Wayland 正解是 `org.freedesktop.portal.GlobalShortcuts`（本机 KDE 已暴露，`busctl --user introspect` 可证）。
- 附带：测试工程 `ImplicitUsings` 关闭，链接进来的源文件要自己写 `using System;/Collections.Generic;/IO;/Linq;`；`ParseLibraries(null)` 这种重载歧义要写 `(string?) null`；最小 PE 夹具可以用 `$$"""` 拼字节表，注意 `{{x}}` 才是插值。

## L-152 页面白名单会「静默换页」——新增一级页时必查 `MainView.NavigateTo`

- **现象**：把运行环境从设置页挪成一级页后，从 footer 点进去仍然显示启动器页，没有任何报错。
- **原因**：`Features/ViewHost/MainView.xaml.cs` 的 `NavigateTo` 里有一句白名单判断（原本 `page.Name is not nameof(SettingPage)` 就把目标换成 `GameLauncherPage`），目的是「防止打开不存在的页面」。新页面没进白名单 → 被当成非法页。
- **处理**：白名单改成 `page.Name is not (nameof(SettingPage) or nameof(Features.Setting.RuntimeEnvironmentSetting))`；`--page` 映射、Tag 分发、`UiSelfTest` 断言同步补上。
- **教训**：这条兜底**不报错**，所以「点击没反应」要先去查它，而不是怀疑事件没绑上或布局没命中。

## L-153 Uno Skia 6.7 的 `Pivot` 页签头收不到鼠标点击

- **现象**：运行环境页的 `Pivot` 五个页签，鼠标单击/双击/长按都不切换；同一窗口里的普通 `Button`（「配置文本」）能正常弹窗，`Right` 方向键也能切页。
- **结论**：不是注入问题（KWin EIS 与 `xdotool` XTEST 两条路都一样），是 `PivotHeaderItem` 的命中区在 Skia 后端不工作（本仓 `Features/Gacha` 等处的 `Pivot` 只用于展示，没有点击切换需求，所以一直没暴露）。
- **处理**：改成自绘页签（`StackPanel` + 5 个 `Button` + 两个 `Style`），内容用 `Visibility` 互斥，`ShowTab(int)` 一处收口（D82）。
- **教训**：**Uno 控件「看着能用」不等于「能点」**。凡是导航类控件（Pivot/TabView/SelectorBar），落地前先在真机上点一次；不通过就换普通 `Button` 自绘，比追 Uno 内部模板便宜。
- 附带：`ScrollViewer` 的滚轮在本机注入路径下也滚不动（`cu_mouse scroll` 与 `xdotool click 5` 都无效，页面右侧能看见滚动条轨道），但这是**注入限制**，尚未证伪真实鼠标——验收时请用真鼠标滚一次运行环境页。

## L-154 引号会被 `SplitArgs` 吃掉——校验要看原始串

- **现象**：给 gamescope 参数加「不要写 shell 引号」的检查，写完单测直接红：`"-W 2560 -H 1440"` 这类输入里引号没了。
- **原因**：`LaunchRecipeBuilder.SplitArgs` 是「按 shell 规则切 token」，它天生会把引号解析掉——所以 token 里永远查不到 `"`。
- **处理**：引号检查改成查**原始串**（`args.Contains('"')`），元字符（`; | & > < \` $`）继续按 token 查；单测覆盖两组。
- **教训**：凡是「先解析再校验」，要先想清楚**信息在哪一层还在**。解析器吃掉的东西（引号、空白、转义）只能在解析前看。

## L-155 「两个独立的东西」被塞进一个字段，UI 只能退化成单选

- **现象**：`RuntimeProfile` 只有一个 `GraphicsId`，于是 DXVK 和 VKD3D-Proton 只能选一个；想两个都装只能手改 JSON。用户一眼看出不对：「这俩是不是可以同时装？那单选用下拉框就不合理」。
- **根因**：字段是数据的形状，UI 是字段的投影。字段合并 ⇒ UI 一定选不出「都要」。当时把「图形层」当成一个东西，是因为它们都从同一个按钮装进去——安装动作相同不代表数据是一个。
- **处理**：拆 `DxvkId` + `Vkd3dId`（D83），`graphics.json` 同步改成按 kind 的数组。
- **教训**：**判据是「这两者能不能同时为真」**。能同时为真就是两个字段/两个开关；不能同时为真才是一个枚举。别因为「装的时候走同一条代码」就合成一个。

## L-156 改 schema 前先查测试：迁移用例会跟着一起塌

- **现象**：删掉 `RuntimeProfile.MigrateFromLegacy()` 后，`dotnet build` 在测试工程报了 18 条错——`LaunchRecipeTests.cs` 里 5 个 `MigrateFromLegacy_*` 用例直接引用被删的字段。
- **处理**：连同用例一起删（用户 m01589：开发阶段不做数据迁移），并在 D83 里写明「删了哪 5 个用例」。
- **教训**：删公开成员前先 `grep -rn "成员名" tests/`。源码编译过不代表测试编译过——`dotnet build Starward.slnx` 会把测试工程一起带进来，所以报错一定会出现，但**先看清楚是「新代码错」还是「旧测试错」**，别去给废弃 API 造兼容壳。

## L-157 边遍历边 `Remove`：`--graphics-remove` 直接 SIGABRT

- **现象**：L2 的 `T-RT-13`（`--graphics-remove` 按记录清干净）退出码 **134**，`Unhandled exception: Collection was modified; enumeration operation may not execute.`（`GraphicsInstaller.Remove` 里 `foreach (InstalledGraphics record in targets)` 内 `records.Remove(record)`）。
- **根因**：`targets` 在 `kind is null` 时**就是 `records` 本身**（同一个 List 引用），遍历它再往里删元素必然炸；`kind` 有值时才是 `.ToList()` 出来的副本。
- **处理**：`targets` 一律 `.ToList()`，删完文件后用 `records = records.Where(x => !targets.Contains(x)).ToList();` 过滤。
- **教训**：「过滤一个集合并删掉其中元素」时，**先把要被遍历的那一份拷出来**。这类 bug 只在 `kind == null`（全删）这条路径上出现，单看代码很容易以为两种分支一样。

## L-158 界面上别糊绝对路径：组件名查不到时回落文件名

- **现象**：一致性提示里出现「前缀里还没装（选的是 `/home/qie2035/.local/share/sleepy-launcher/dxvk/dxvk-gplasync-v3.0-1`）」，一条提示占满两行，还比正文更显眼。
- **根因**：`DescribeGraphicsSync` 用 `ComponentStore.GetGraphics()` 里 `Id == id` 查组件名，查不到就退回整个 `Id`——而 `Id` 本身就是绝对路径（`Component.Id` 即路径）。组件被删、或路径大小写/尾部斜杠不一致时必然查不到。
- **处理**：回落改成 `Path.GetFileName(id.TrimEnd('/'))`，界面显示 `dxvk-gplasync-v3.0-1`；「前缀里是 X」这类文案同样走名字。
- **教训**：**`Id` 是路径不代表它适合当标签**。凡是要拼进界面的标识，先想「查不到名字时显示什么」，默认答案永远不是完整路径。顺带：`Interlocked` 忘了 `using System.Threading` 会让编译停在「找不到类型」——小工具函数别为了省一行 using 去换写法。

## L-159 组件「身份」不止一个：按路径匹配会静默把选择改掉

**现象**：D85 编辑器里给方案选了 `DxvkId = /usr/share/dxvk`，重新打开编辑器时下拉显示「（不选）」，保存一次就把选择清掉了。

**原因**：`ComponentStore` 里运行器的 `Id` 是安装目录（`/opt/spritz-wine-tkg`）、`Path` 是 `bin/wine`；图形组件的 `Id` 与 `Path` 相同但都会被 `RealPath` 解析过。编辑器原先只比 `Id`（图形）或只比 `Path`（运行器），任一侧不一致就落回索引 0，界面上看起来只是「没选中」。

**改法**：`MatchesComponent(component, pathOrId, id)` 四路比对 —— `Id == id`、`Id == expanded`、`Path == expanded`、`Path` 是 `expanded` 的目录前缀、末两段相同。四种来源（配置文件、组件清单、`realpath`、用户手填）都能对上。

**顺带两条**：

- XAML 里已经放了返回按钮，代码里又拼了一个 header 返回键 → 截图里出现两个「← 返回」。同一段 UI 只在一处生成。
- 页签/按钮「点了没反应」先量坐标：`logical = origin + px * (frame 宽 / 截图宽)`，本次是把 900px 宽的截图当成 1240 逻辑宽算，点到了空白处（差 150px）。

---

## L-161 「一个文件两个名字」必然来自两处各自计算

**现象**：同一个 `/opt/spritz-wine-tkg/bin/wine`，方案详情页显示 `wine`，编辑器下拉显示「系统 Wine wine-11.19-1-g78e32ba1d3d (Staging) …」。用户的第一反应是「我是不是选错了」。

**根因**：显示名有两处独立来源——`RuntimeProfile.RunnerName` 从路径猜（`…/bin/wine` → 取文件名 → `wine`），`ComponentStore` 的 PATH 扫描分支硬编码 `"系统 Wine"`。两处算法不同、输入不同，**必然会漂移**，不是偶发。

**规则**：

1. 同一个东西的显示名只留**一个来源**。能拿到组件对象的地方优先用组件的名字；配置里存的旧名字只当回落。
2. 配置里存的是「用户当初选的那条路径」，不等于「现在扫出来的那条」；用 `MatchesComponent` 的四路比对（`Id` / `Path` / 目录前缀 / 末两段）把两者接上。
3. 列表里出现**两行一样的字**，说明命名没消歧。目录名天然相同（两个第三方启动器各带一份 `spritz-wine-*`）时用来源区分；**不重名就不要动**——否则「系统 Wine」会变成「系统 Wine（系统包）」这种噪音。
4. `Component.Name` 原本是 `init`，想在后处理阶段改必须先把属性改成 `set`。

**顺带的坑**：`DisplayName` 已经是 `Name (Version)` 拼好的，CLI 再单独打一列版本就重复两遍。打印时名字用 `Name`、版本用 `Version`。

**怎么进单测**：`ComponentStore.cs` 太大（依赖 Serilog / `ILogger` / 文件系统），没链接进测试工程（D37）。纯字符串逻辑（命名、消歧）拆到 `Features/Runtime/RunnerNaming.cs` 才能测——和 `ComponentDownloadMath.cs`、`PrefixPath.cs` 是同一个模式。

---

## L-174 wine 注册表文件是「第二份真相」，格式有三个坑（原编号 L-160，错序已改）

**背景**：为了让「查注册表」不再 fork wine（D86），改成直接解析 `<prefix>/user.reg`。三个格式细节不看真实文件绝对想不到：

1. **非 ASCII 键名是 `\xHHHH` 转义**：原神在文件里写作 `[Software\\miHoYo\\\x539f\x795e]`（`\x539f`=原、`\x795e`=神）。只做 `\\` → `\` 的替换会匹配不上，必须按 4 位十六进制再解一次。
2. **超长 `hex:` 值折行**：行尾是 `,` 后跟一个反斜杠，下一行缩进两个空格继续。不拼接就会把 54 KB 的 `SDK_LANG_FILE` 截成首行 —— 而 `reg query` 的输出解析（`RegistryFormat.ParseLine`）恰好就是这个毛病。
3. **`reg query` 的中文输出是乱码**：同一台机器上 `wine reg query 'HKCU\Software\miHoYo\原神'` 打印成 `ԭ��`（本地代码页）；文件是 UTF-8，解码正确。

**另一条（更普遍）**：`private static bool _xxxShown` **不是「一次性」**。桌面应用的进程寿命就是一次启动，进程级 static 等于「每次启动都重来」。要一次性就落到持久化（本仓库用 SQLite `Setting` 表的 `hint_<name>`，见 `Helpers/OneShotHints.TryTake`）。同类缺陷在本仓库共 3 处（自动识别、Wayland 热键、系统代理），全都表现为「每次开都弹一遍」。

**验证手法（值得复用）**：纯函数（`WineRegistryFiles`）不依赖 wine/前缀/日志，可以 `cp --reflink=auto` 复制到 `/tmp` 的 console 工程里，直接对**真实数据**跑并计时，再和 `wine reg query` 对照。比在应用里点 UI 可靠得多（本次点了「HDR 亮度设置」按钮毫无反应，白等 6 秒）。

**顺带**：C# 的 verbatim 字符串 `@"...\"..."` 里 `\"` **不是转义**，双引号会提前结束字符串 —— 写测试里带引号的样例时要改用普通字符串 `"a\\nb\\0c\\rd\\\"e"`。

---

## L-163 SDK 版本钉太高 = 换台机器就编译不过；CI 绿不代表打包环境绿

**背景**：用户 m03131 报「global.json 导致 aur 编译不过，好像是 dotnet sdk 版本不匹配」。

1. **`rollForward` 只向上滚，不向下兼容**。`global.json` 写 `"version": "10.0.302", "rollForward": "latestMinor"` 时：装 10.0.404 的机器能编译（向上滚），装 10.0.1xx 的机器**直接失败**（`A compatible .NET SDK was not found. Requested SDK version: 10.0.302`）。**规则**：对外发布的下限只钉到该 major 的**最低 feature band**（`x.y.100`），让 rollForward 去挑实际装的版本。
2. **`dotnet-version: '10.0.x'` 的 CI 会永远掩盖这个问题**：setup-dotnet 装的是最新 10.0.x，永远 >= 我们写的下限。只有「版本由别人决定」的环境（Arch 的 `dotnet-sdk`、系统 LTS 渠道）才会暴露。**规则**：凡是给外部环境用的版本下限，按「最旧的可能值」写，并在打包文档里写明为什么，别只在开发机上验证。
3. **复现手法**：造个临时目录放 `global.json` 钉一个不存在的高版本，跑 `dotnet --version` —— 立刻得到完整报错（请求版本 + global.json 路径 + 已装 SDK 列表），比等打包机快得多。实测 `/tmp/gjtest` 里钉 `10.0.999` 就复现了。
4. **`global.json` 支持 `//` 注释**（JSONC，.NET 自己解析）：把「为什么是这个版本」写在旁边，比写在文档里更容易被下一个改它的人看见（实测加注释后三处 `dotnet --version` 仍正常）。
5. **防回归靠 lint，不靠记性**：`scripts/test/lint-pkgbuild.sh` 的 D 节检查「每个 `global.json` 的 `sdk.version` 最后一段必须是 `100`」，谁改回 `10.0.3xx` 就 FAIL。

**验证**（2026-11-19）：改后 `dotnet --version` 在仓库根 / `src/Lunarward` / `tests` 都解析到 10.0.404；Debug/Release 均 0 错误；单测 264/0；lint-pkgbuild 四节全 PASS。

---

## L-175 打包清单与源码各写一遍，必然漂移；`gitignore` 的 `dir/` 会吃掉同名包目录（原编号 L-162，错序已改）

**背景**：用户 m02959「arch打包是不是还是有问题」。核对三份 PKGBUILD 时挖出三个不同层次的坑：

1. **依赖清单写三遍 = 一定漏**。`webkitgtk-6.0` 在 `d095922`（D76）只补进了 `packaging/PKGBUILD`，`packaging/aur/` 下两份从 11-11 起就没再动过 —— 同一个仓库里，同一份依赖，三种状态。而且三份的 `depends` 都缺 `libxcb`（`X11Window.cs:17` 的 `libxcb.so.1` 是运行时 P/Invoke，`libx11` 不覆盖它）与 `xdg-utils`（`GameSettingPage.xaml.cs:771` 的 `xdg-open`）。**修法**：`scripts/test/lint-pkgbuild.sh` 从源码反查（扫 `DllImport`/`lib*.so*` 常量与 `ProcessStartInfo("cmd")`），再校验三份 `depends` 一致 —— 清单不再靠人记。

2. **`.gitignore` 里的 `dir/`（无前导斜杠）匹配任意层级**，`packaging/.gitignore` 写了 `lunarward/`（本意是 makepkg 的构建目录），把 `packaging/aur/lunarward/` 整份目录也忽略了。后果：`git ls-files packaging/` 里没有它，这份包定义**从来没进过仓库**，「改一处忘两处」根本没人看得见。**规则**：想只忽略某一层，就写 `/dir/`；写完用 `git check-ignore -v <path>` 验一遍。

3. **`pkgver` 不能直接喂 `-p:Version`**。git 包的 `pkgver()` 产出 `0.1.0.r123.gabc`，而 `AssemblyVersion` 只认 `major.minor[.build[.rev]]`，带字母就构建失败。**修法**：`-p:Version="${pkgver%%.r*}"`。

4. **`source` 里的分支/标签指向必须能和 `git` 对上**：`#branch=linux-port` 指向一个 11-13 就停用的分支（构建出五天前的代码）；`archive/v0.1.0.tar.gz` 指向一个不存在的 tag（`git tag -l` 为空 → 404）。lint 的 C 段专门查这个：git 源才允许 `sha256sums=('SKIP')`，`#tag=` 源要求 tag 真实存在。

**验证手法**：`makepkg --printsrcinfo -p PKGBUILD` 不联网也不构建，能立刻验出依赖表达式与字段错误；三份的输出直接 `diff` 就能看出漂移。

---


## L-164 `makepkg` 的 source 可以什么都不取；`grep '^source='` 会漏掉缩进行

**背景**：用户 m03533 指出 `packaging/PKGBUILD`「会重复源码和构建」。实测一次本地打包在 `packaging/` 下留了 **235 MB**（`src/lunarward` 克隆）+ **140 MB**（`src/publish`），而工作区本来就有这份源码和它的 `obj/`。

1. **`source=()` 是合法的**：`makepkg` 不会报错，也不克隆任何东西；源码可以在 `prepare()` 里用 `ln -sfn "$startdir/.." "$srcdir/$pkgname"` 软链进来。实测 `makepkg --printsrcinfo` 正常、`makepkg -f` 打包成功、`pkgver()` 还能在软链里读 `.git` 拿到提交数。
2. **软链 ≠ 复制**，所以 D91 里「`source=()` + `prepare()` 复制工作区会把 `bin/obj` 一起拷进去」的顾虑不成立 —— 软链只是让 `dotnet publish` 在原位置跑，增量编译直接复用已有 `obj/`。
3. **`grep -E '^source='` 会漏掉 if/else 里缩进的 `source=`**。把 source 从「顶层变量 + 单行赋值」改成「if/else 两块」之后，lint 的 `src` 变量变成空串，`git+` 与 `SKIP` 都匹配不到，于是走进「tarball 源有真实哈希」分支 —— **打印 OK，但理由是错的**。修法：`^[[:space:]]*source=`。同理，任何「扫源码里的某行」的 lint 规则，都要容忍缩进。
4. **`cp -a` 之后不会自己消失**：`package()` 里 `cp -a publish/. pkgdir/...` 会让 publish 输出留两份（srcdir 一份、pkgdir 一份，各 140 MB）。改成 `mv` 即可；跨文件系统时 `mv` 自动退化成「复制 + 删除」，也不会更差。
5. **本地软链模式打进包的是工作区当前文件，包括未提交改动** —— 这是特性（打包验证本来就该验证当前代码），但和 D91「只含已提交内容」正好相反，注释与 README 必须写清，否则会误以为包内容等于 HEAD。
6. `-p:DebugType=none` 与 CI 的 publish 保持一致：`Lunarward.pdb` 不进包。

**验证手法**：`du -sh packaging/src`（打包后应是几 KB，只剩空目录）、`makepkg -f` 第二次的耗时（应是个位数秒）、`pacman -Qlp <pkg> | grep -c '\.pdb$'`（应为 0）、解包后 `Lunarward --version` 能跑。

---

## L-165 缓存页面 × `OnLoaded` 一次性 × `x:Bind` 默认 OneTime = 状态永远陈旧

**背景**：用户报「切回已安装的游戏，按钮还是『安装游戏』、定位提示还在、时长停在上一个游戏」。上游 Starward 的页面**从不缓存**，所以它的 `OnLoaded` 就是「每次显示都完整初始化」；移植侧为了减卡顿给 8 个页面加了 `NavigationCacheMode="Required"`（D60/D62），把这条隐含前提打破了两次。

1. **`PageBase` 在第一次 `Unloaded` 就退订**（`Loaded -= PageEx_Loaded`）—— 上游写法本身没问题（实例用完即弃），但缓存实例第二次挂载时 `Loaded` 事件不会再订阅回去，`OnLoaded` 从此不再执行。症状是「离开过一次启动器页，本会话内它再也不刷新」：消息不注册、安装任务看门狗停摆、`CheckGameVersion()` 不跑。修法：`Loaded/Unloaded` 常驻，`OnLoaded` 按挂载次数跑；挂载时记录 `ContentGameId`，导航参数指向别的游戏就调 `OnGameChanged()`（D94）。
2. **先导航、后广播 = 守卫必然吃掉消息**。`MainView` 切游戏是 `UpdateNavigationView()`（内含 `NavigateTo(SourcePageType, newId)`）再 `Send(CurrentGameChangedMessage)`。因为 `GameInfo : GameId`，`OnNavigatedTo` 的 `is GameId` 分支已经把 `CurrentGameId` 换成新值，消息处理里的 `if (CurrentGameId == message.GameId) return;` 就等于「永远 return」。**教训**：`OnNavigatedTo` 会改的字段，不能用来做「是否需要刷新」的判据；要单独记一个「已渲染的游戏 id」（`ContentGameId`）来比。
3. **`x:Bind` 默认 OneTime 的属性能不能更新，取决于控件是否重建**。缓存页不重建控件，`PlayTimeButton.CurrentGameBiz` 这类「随游戏变化」的属性必须显式刷新（本次补 `RefreshPlayTime(GameBiz)`）。实测不刷时切到原神显示 `0h 0m`，正确值 `0h 9m` —— 这种差一眼看不出是 bug，会被当成「没玩过这个游戏」。
4. **ctor 里的消息注册 + `OnUnloaded` 的 `UnregisterAll` 配对**：在上游是安全的（每次导航新实例），缓存后 ctor 只跑一次、`UnregisterAll` 却每离开一次就执行一次 —— 第一次离开设置页之后，`LanguageChangedMessage` 永久丢失，切语言不再生效。凡是「ctor 注册、OnUnloaded 统一注销」的页面，缓存后都要把注册搬到 `OnLoaded`。

**排查手法**（本次有效）：临时在页面 `OnNavigatedTo / OnLoaded / OnUnloaded / 消息处理` 打 `[lw-diag]` 日志，跑一次复现路径，靠「有 `OnNavigatedTo` 而**没有** `OnLoaded`」「消息到了但立刻 `skipped (same id)`」两类缺失直接定位；日志用完 `git checkout -- <file>`，`bin/` 里的诊断产物要重建。

**顺带发现的导航坑**：`MainView.NavigationView_ItemInvoked` 开头 `if (args.InvokedItemContainer?.IsSelected ?? false) return;` —— 停在设置页（不是导航项）时，导航栏「启动器」仍是选中态，点它不会返回；要先点别的项再点回来。GUI 自动化做往返验证时必须注意，否则会误判为「点击无效」。

---

## L-166 LibVLC 不指定 `--vout` 会开真窗口：抽帧/缩略图必须走 vmem 回调

**背景**：用户报「打开背景图库会闪过好多 vlc 窗口」。图库视频缩略图由 `Features/Background/VideoThumbnailer.cs` 用 LibVLC 抽首帧生成，早期实现是：

```csharp
using var libvlc = new LibVLC("--no-audio", "--quiet", "--no-video-title-show");
using var player = new MediaPlayer(libvlc);
player.Play(media);                       // 没有 --vout=vmem，也没 SetXWindow
bool ok = player.TakeSnapshot(0, path);   // 依赖 vout 的渲染面
```

1. **`--no-video-title-show` 只关标题字幕，不关窗口**。没调 `SetXWindow/SetHwnd` 时，libvlc 的 vout 自己创建顶层窗口来渲染，抽完帧销毁 —— 窗口生命周期只有 1~8 秒，所以表现为「闪一下」。
2. **放大效应**：图库打开时对每个无缓存缩略图的视频都抽一次（`BackgroundViewPage.GenerateVideoThumbnailsAsync`），`CachedImage` 的 `IsThumbnail` 通道也会请求同一个文件；`SemaphoreSlim(2)` 只限并发数，不限总次数 → 几十个视频时「两个两个地弹」，像满屏 VLC 窗口。
3. **正确做法**：与视频壁纸播放器一致，`new LibVLC(..., "--vout=vmem")` + `SetVideoFormat("RV32", w, h, rowBytes)` + `SetVideoCallbacks(Lock, Unlock, Display)`，等第一个 `Display` 回调拿到 BGRA 缓冲，再用 Skia `SKImage.FromPixelCopy(...).Encode(Png)` 落盘。全程没有窗口，也不依赖 `TakeSnapshot` 的内部渲染面。
4. **顺手要做的两件事**：①同一路径的抽帧任务用 `ConcurrentDictionary<string, Task>` 去重（页面与 `CachedImage` 会同时请求）；②失败结果短期缓存（本次 10 分钟），否则坏文件每次打开图库都要重新解码一遍、再失败一遍。

**验证手法**：清掉 `~/.cache/lunarward/bg-thumbs`，`Lunarward --window-action backgroundview`（Debug 专用直达入口）打开图库，同时轮询 `wmctrl -l | grep -i vlc`（或 `cu_windows`）——修复前抽帧期间会出现 `vlc` 窗口，修复后始终为 0，缩略图仍按 5 张全部生成。

## L-167 wineboot 卡在 wine-mono 对话框 / WINEARCH 在 wow64 版 wine 上直接失败

- 现象：`lunarward --prefix-ensure …` 挂住不动，屏幕上出现 wine-mono 安装询问窗口；在真机验证里表现为「启动器卡在初始化前缀」。
- 原因：`wineboot -u` 初始化前缀时会提示安装 mono/gecko（`mscoree`/`mshtml`）；没有 `WINEDLLOVERRIDES=mscoree,mshtml=` 就等用户点选；用 `Process.WaitForExit()` 无超时等待会永久挂起。
- 正确做法：启动 wineboot 时同时设 `WINEDLLOVERRIDES`（取配方里的值，否则默认 `mscoree,mshtml=`），并给 `WaitForExit` 上限（本项目 3 分钟），超时 `Kill(entireProcessTree: true)` 后抛明确错误。
- 另一个坑：wow64 版 wine（本项目测试机 `/opt/spritz-wine-tkg`）对 `WINEARCH=win32` 直接报 `WINEARCH is set to 'win32' but this is not supported in wow64 mode`——建 32 位前缀必须用 32 位 wine。预检给「以实际前缀为准」的警告，不要在配置层假装 win32 生效。

## L-168 列 Grid 里没有显式星号行：内部 ScrollViewer 不滚动、内容被整块裁掉

- 现象：运行环境页右栏换成「详情 / 编辑器 / 增强」三态 Grid 后，编辑器下方的前缀、启动预览、高级全都看不到，滚轮和 PageDown 都没反应，窗口里只到「VKD3D-Proton」两行为止。
- 原因：右栏写的是 `<Grid Grid.Column="1">` 同时放两个 `ScrollViewer`，但没写 `Grid.RowDefinitions`；Uno 把这一行按内容高度量（等价 Auto），内部 `ScrollViewer` 拿到的高度就是内容高度，于是既不产生滚动条、也不滚动，超出的部分由父级直接裁掉。
- 正确做法：给这类「同格多视图」的 Grid 显式补一行 `<RowDefinition Height="*" />`（或直接把 `ScrollViewer` 的父级设成 `Grid` 的星号行），让内部 `ScrollViewer` 拿到有限视口高度。
- 排查手法：`xdotool mousemove X Y click 5`（滚轮）与 `PageDown` 都不动 → 先怀疑视口没被约束；`Window` 放大后能露出的内容更多，也说明是父级裁剪而不是内容缺失。

## L-169 `wl-copy` 守护进程继承 stdout：`out=$(…)` 永远等不到 EOF，CLI 用例挂死

- 现象：`scripts/test/cli-smoke.sh` 本机跑 26 分钟无输出，进程树只剩 `bash` + `tail`，没有 Lunarward 子进程；`/proc/<pid>/wchan` 是 `anon_pipe_read`。
- 原因：剪贴板复制用例里 `ClipboardHelper` 启动 `wl-copy` 时只重定向了 stdin，`stdout`/`stderr` 继承自调用方。`wl-copy` 写完剪贴板会 fork 一个守护进程继续持有 selection，这个守护进程同时握着继承来的 stdout 写端 → 脚本里的 `out=$(lunarward …)` 等不到管道 EOF，永久阻塞。CI 走 xvfb、没有 `wl-copy`，所以只在 Wayland 本机必现。
- 正确做法：任何「启动后可能常驻」的外部进程都要 `RedirectStandardOutput = true, RedirectStandardError = true`（不是只有 `RedirectStandardInput`）；需要看输出就异步读，不需要就丢弃。`ClipboardHelper.Linux.cs` 的两个分支（通用 `wl-copy` 与 `SetImageFile`）都已补上。
- 排查手法：`ps -o pid,ppid,stat,cmd -p <hung>` 看是不是 `bash` 在读管道；再 `ls -l /proc/<hung>/fd` 找谁持有写端 —— 若是一个被 `systemd --user` 收养的 `wl-copy`，就是这个坑。

## L-170 `ContentDialog`：同一实例不能二次 Show、连点会抛、嵌套必须先 Hide

- 现象：运行环境页连点「清理前缀 / 删除组件 / 移除图形层」时偶发进程直接消失（`async void` 处理器里抛出无人接）；「管理下载源」弹窗开着时点「编辑」，要么报 `Only a single ContentDialog can be open`，要么新弹窗压在下面收不到输入。
- 原因（三条独立坑）：①Uno/WinUI 的 `ContentDialog` **一个实例只能 `ShowAsync()` 一次**，第二次抛；②同一页面里 20+ 处各自裸 `ShowAsync()`，只有 2 处有 `_dialogOpen` 守卫，`async void` 里抛出就是同步上下文未捕获异常；③嵌套流程（管理窗 → 确认框）没有先 `Hide()` 外层，第二个弹窗与第一个并存。
- 正确做法：抽一个进程级入口 `Helpers/DialogGate.Show(dialog)`——`Interlocked` 抢锁，抢不到直接按「用户取消」返回，`try/finally` 复位；每次都 `new ContentDialog { … }`；确实要嵌套时先 `Hide()` 外层，动作完成后再重建/重开（本仓库 `RunNestedAsync` 就是这个形状）。
- 排查手法：日志里搜 `Only a single ContentDialog can be open`；`grep -rn "\.ShowAsync()" src/Lunarward` 数裸调用点，比逐个点界面快。
- 代价：锁是进程级的，真正需要两个对话框并存（非模态）的设计会被这门锁挡住，届时要把「被拒」和「用户取消」区分开（`ContentDialogResult.None` 目前两者同义）。

## L-171 Linux 上不存在的字体族会让整段文字不渲染

- 现象：运行环境页的前缀路径、启动预览、「⋯」按钮在 Linux 上是**空白**（不是豆腐块，是完全没有像素），Windows 上正常。
- 原因：`FontFamily = "Consolas"` / `"Consolas, monospace"` / `"Segoe MDL2 Assets"` 在 Linux 上没有对应族名。Skia 找不到族时不会回退到默认字体，而是拿不到字形——整段 TextBlock 渲染为空；`Segoe MDL2 的 `\uE712` 同理画不出省略号。
- 正确做法：跨平台代码里**不写 Windows 专属族名**。等宽只是视觉诉求时用主题自带的等宽资源（或干脆不指定），图标优先 `FontIcon`/文本字符，不要靠 `Segoe MDL2` 的私有码位。
- 排查手法：Grep `FontFamily` 找 `Consolas`/`Segoe MDL2`/`Segoe UI`；`fc-match Consolas` 在 Linux 上返回的是替代族，不代表原文能渲染。
- 代价：等宽对齐（比如启动预览的命令行）在非等宽主题样式下会略微不对齐，可读性优先于像素对齐，值得。

## L-172 libvlc 的 `avcodec-hw` 只能当「媒体选项」传，实例参数会被静默忽略

- 现象：背景视频（1080p VP9 30fps）常驻 1.2 核。早先给 `new LibVLC(...)` 加过 `--avcodec-hw=vaapi`，注释里记的是「CPU 没降（1.05 → 1.05 核）」，于是结论成了「VLC 3 + vmem 拿不到硬件帧」。
- 真正原因：`avcodec-hw` 是**输入级**配置，作为 libvlc 实例参数传进去不生效，而且没有任何报错。打开 `--verbose=2` 才看得到：`looking for hw decoder module matching "none"` → `no hw decoder modules matched`（值根本没到 avcodec）。
- 正确做法：在 `Media` 上加选项——`media.AddOption(":avcodec-hw=vaapi")`。日志随即变成 `looking for hw decoder module matching "vaapi"` → `using hw decoder module "vaapi_drm"`。没有 VAAPI 的机器 VLC 自己回退软解，不需要应用侧探测；但 `:avcodec-hw=any` 在本机**不会**选中 vaapi，要显式写 `vaapi`。
- 排查手法：`LibVLC` 上挂 `Log += (_, e) => Console.WriteLine(e.Message)` 并加 `--verbose=2`，搜 `hw decoder module`；只看 CPU 容易被「其实没开」骗过去。
- 实测（1920×1080 VP9、vmem 输出 RV32、20 秒窗口）：纯解码 0.28 → 0.13 核；含 Skia 叠图与整帧拷贝 0.44 → 0.29 核；真机整体 112% → 74%（同时去掉一次整帧拷贝）。
- 代价：NVIDIA 的 VAAPI 是 NVDEC 解码后**下载回内存**，省不掉后面的合成与上屏；个别驱动会花屏，所以留了 `LUNARWARD_VIDEO_HWDEC=0` 让用户退回软解。

---

## 编号勘误（2026-11-24）

本轮校准时发现编号有重号与错序，按「保留先出现的编号、后来者改用后续未占用编号」处理，避免外部引用整段失效：

| 原编号 | 现编号 | 位置/内容 | 原因 |
|---|---|---|---|
| L-119（第二处） | **L-173** | `Auto` 行不随「异步变可见」重算 | 同一文件已有 `L-119`（符号链接 `FileInfo.Length`，保留） |
| L-160 | **L-174** | wine 注册表文件是「第二份真相」 | 原文排在 `L-161` 之后，错序 |
| L-162 | **L-175** | 打包清单与源码各写一遍 | 原文排在 `L-163` 之后，错序 |

**外部引用已同步（D124）**：`00-decisions.md:292`、`CHANGELOG.md:823` 的 L-119 → L-173；`README.md`、`12-shims-and-gaps.md:155/:266` 的 L-160 → L-174；`12-shims-and-gaps.md:264` 的 L-162 → L-175。`19-test-plan.md:502` 的 L-119 指符号链接条目（保留的编号），无需改。`scripts/test/lint-doc-status.sh` 现在会把「L-* 重号」直接判红，同类问题不会再靠人眼发现。

## L-176 libvlc 在 Linux 上拿不到「源帧率」，只能量帧到达节奏

- 现象：想按视频自己的帧率限合成（而不是写死 30 fps），三条路都不通：①`MediaPlayer.Fps` 在 vmem 输出下**恒为 0**（3.10.2 实测，起播 5 秒后仍是 0）；②`Media.Parse(ParseLocal)` 后 `media.Tracks` 有视频轨，但 VP9/WebM 的 `VideoTrack.FrameRateNum / FrameRateDen` **都是 0**（容器里写的是时间基，不是帧率）；③`ffprobe` 能拿到 30 fps，但要 fork 外部进程，UI 线程不许（硬规则 10）。
- 修法：在 vmem 回调里量帧到达时刻（`Environment.TickCount64`，环形缓冲 16 帧），样本 ≥8 帧后取「跨度 / (n-1)」当每帧周期，打 8 折当合成间隔；异常值用 10–240 fps 上下限挡掉。实测一条标称 30 fps 的 VP9 壁纸量出 `30.0 ms/帧`（libvlc 的 vmem 送帧粒度约 1 ms，比标称 33.3 ms 略快），按实测走反而更贴合真实送帧节奏。
- 通用规则：**拿不到可靠的元数据时，量实际行为，别退回硬编码常量**——常量在 24 fps 与 60 fps 素材上都会错（一个被无谓限制，一个被砍半）。

### L-177 · 视频壁纸：NV12 直出比 RV32 更贵，且缓冲要留填充行

- 现象：想省掉 libvlc 的 YUV→RV32 转换，改成让它直出 NV12、我们转 BGRA。隔离 POC 里确实赢（RV32 18.4% 核 → NV12 6.1%），接进应用后反而输（Release 同机：RV32 70% → NV12 92%）。
- 原因：POC 只量「解码 + 拷进缓冲」，没量「我们自己的转换」。VLC 的 RV32 转换是 SIMD 汇编，我们的 NV12→BGRA 是标量 C#（1080p 一帧约 7–10 ms，单核），省下的不够付出去的。
- 第二个坑：按 `1.5 × W × H` 分配 NV12 缓冲会**稳定段错误**。libvlc 的 NV12 输出会在 UV 平面后多写几行（1080p 实测写到 3,121,920 字节，比 3,110,400 多 11,520 = 6 行）。给 32 行填充余量后立刻正常。POC 里没崩只是因为裸控制台没有别的线程在冲那块内存。
- 还有个只在应用里出现的坑：`Lock` 回调里用 `Parallel.For` 铺开转换会稳定崩（改成串行就好），而 `Display` 回调里做同样的行并行没问题——vmem 回调线程的栈空间/信号状态不保证，别在里面起并行任务。
- 教训：**POC 的收益必须把「我们自己那一半」算进去**，否则测的是「换了个更便宜的搬运工」，不是「总成本更低」。

### L-178 · SkiaSharp 能把外部 GL 纹理包成 SKImage（零拷贝的 Skia 侧是可行的）

- 结论：只要线程上有当前 GL 上下文，`GRGlInterface.CreateOpenGl(getProcAddress)` → `GRContext.CreateGl` → `new GRBackendTexture(w, h, mipmapped, new GRGlTextureInfo{ Target, Id, Format })` → `SKImage.FromTexture(gr, btex, GRSurfaceOrigin.TopLeft, SKColorType.Bgra8888, SKAlphaType.Premul)` 就能得到一个 `IsTextureBacked == true` 的图像，直接 `canvas.DrawImage` 画在 GPU 表面上，全程不经 CPU。
- 上下文怎么来（无窗口场景）：`eglGetPlatformDisplay(EGL_PLATFORM_SURFACELESS_MESA, 0, 0)` → `eglInitialize` → `eglBindAPI(EGL_OPENGL_API)` → `eglCreateContext(dpy, EGL_NO_CONFIG_KHR, EGL_NO_CONTEXT, …)` → `eglMakeCurrent(dpy, 0, 0, ctx)`。**不要**先 `eglChooseConfig`：surfaceless 平台上一个窗口配置也不匹配时它会返回 false 但 `eglGetError() == EGL_SUCCESS`，看起来像「成功但没配置」，走 `EGL_NO_CONFIG_KHR` 才干净。
- `GRGlGetProcedureAddressDelegate` 只有一个 `string name` 参数，返回 `IntPtr`；先试 `eglGetProcAddress`，返回 0 再用 `NativeLibrary.TryGetExport(libGL, name, out addr)` 兜 GL 1.1 的核心函数（那些不受 EGL 管）。
- 实测环境：NVIDIA 硬件 EGL（`GRContext.Backend=OpenGL`、`MaxTextureSize=32768`）与 Mesa 软件 EGL（16384）都通过，回读像素与上传值逐值吻合。
- 边界：这条链只解决「纹理 → Skia」。libvlc 3 的 `vmem` 只给 CPU 内存、Uno 渲染循环没有注入外部图像的公开入口，端到端零拷贝仍不成立（见 D127）。

### L-179 · Uno 版 CommunityToolkit `WrapPanel` 没有 `ItemWidth` / `ItemHeight`

- 现象：从手写替身切到真实 `CommunityToolkit.WinUI.Controls.Primitives`（toolkit 11 / 8.2.251219 的 Uno 构建）后，`BackgroundViewPage.xaml.cs` 的自适应列宽（D56）编译不过：`CS1061 "WrapPanel" 未包含 "ItemWidth" 的定义`，`ItemHeight` 同样。
- 实测该 dll 的属性只有 `Orientation`、`HorizontalSpacing`、`VerticalSpacing`（`strings` 里连 `ItemWidth` 都没有）。Windows Community Toolkit 的 `WrapPanel` 文档里那些 `ItemWidth/ItemHeight` 在这个 Uno 构建里不存在，别照文档写。
- 修法：把算好的尺寸写到**每个已实体化的容器**上（`panel.Children` 里的 `ListViewItem` 的 `Width/Height`），再挂 `GridView.ContainerContentChanging` 给滚动中新实体化的项补同样的尺寸；`InvalidateMeasure()` 照旧。
- 教训：升级依赖后，「替身删掉 → 编译过」不等于「行为等价」。替身提供过的属性要逐个核对，缺失的自己补在容器层。
- 顺带：手写替身（同名的 `Controls/ToolkitControls.cs`、`InteractivityCompat.cs`、`ToolkitCompat.cs`）会**遮蔽真实包的类型**，报出来的是「属性不存在」这种像 XAML 错的消息（`UXAML0001: Property 'HorizontalSpacing' does not exist on 'WrapLayout'`）。真实包加进来时先把替身改名 `.disabled`，再逐个补回真正缺的能力。

### L-180 · 上游 0.19.0 自带 `ContentDialogBase`，`DialogGate` 与它并存

- `src/Starward/Features/ContentDialogBase.cs` 是 `public abstract partial class ContentDialogBase : ContentDialog`，构造里挂 `Loaded`，把「同一时间只允许一个 ContentDialog」这类公共行为收进基类；`GameLauncherSettingDialog`、`InstallGameDialog`、`PreDownloadDialog` 都继承它。
- 我们 D120 的 `Helpers/DialogGate.cs`（进程级单锁 + `WaitClosedAsync`）**不要删**：它还管嵌套重开与非 ContentDialog 弹窗；但新建弹窗时要记得继承 `ContentDialogBase`（上游语义），否则行为与上游不一致。
- 踩过的相关坑：Uno 的 XAML 编译器遇到 `ItemContainerTransitions="{x:Null}"` 会直接崩（`UXAML0001: Processing failed for an unknown reason (BuildLiteralProperties@6614)` + `The type {http://schemas.microsoft.com/winfx/2006/xaml}NullExtension could not be found`），删掉整行即可（`GridView` 在 Uno 上不支持 `TransitionCollection`，语义等价）。
- XAML 被批量合并拼坏时，别手写标签栈脚本（属性值里有 `>` 会误判）；用 `python3 -c "import xml.etree.ElementTree as ET; ET.parse('...')"`，它直接给出 `mismatched tag: line N, column M`。

### L-181 · 批量三方合并的固定套路（0.19.0 实战）

- 分类驱动：对 `git diff <base>..<merge> -- src/Starward` 的每个文件先判 A/D/M + 「移植侧是否偏差」，再分别处理：
  - **A（上游新增）**：直接拷进 `src/Lunarward`，然后处理依赖（Windows-only、gRPC、WinRT）。
  - **DEL（上游删除）**：**不要直接删**本地同名文件。上游删的往往是我们 `*.Linux.cs` 的对照文件（`GameNoticeWindow.xaml`、`ImageViewWindow2.xaml`），本地那份可能是移植资产。先 `git log --oneline -1 -- <path>` 与 `git show <base>:<path>` 确认归属。
  - **M（改动）**：`git merge-file --diff3` 三方合并；冲突块按「ours==base 取 theirs / theirs==base 取 ours / ours 含 `Lunarward|X11|Skia|wine|Linux|D\d\d|L-\d` 取 ours」启发式处理，剩下的人工过。启发式对 `AppConfig.ServiceProvider.cs` 这类「两边都在同一区加注册」的文件会拼坏，这种文件直接以上游为底重写更快。
- 提交粒度：批量移植、冲突解决、编译收口分开提交，出问题能单独回退。
- 坑：`git checkout <csproj>` 会把未提交的 net11 / `ProjectReference` 改动一起冲掉（曾有 `/tmp/csproj.bak` 才救回来）；改过的工程文件回滚前先备份。

### L-182 · net11 迁移：Uno 6.7 只有 net10 资产，必须一起升 Uno 7

- 事实：`Uno.WinUI 6.7.359` 的 `lib/` 只有 `net9.0`/`net10.0`，没有 `net11.0`；`Uno.WinUI 7.0.65`（对应 `Uno.Sdk 7.0.22`）才有 `net11.0` + `net11.0-windows10.0.19041.0`。所以「只把 TFM 改成 net11」一定在引用 Uno 时报不兼容。
- 连带：`Microsoft.Extensions.*` 要同步升 `11.0.0`，否则 NuGet 报 `NU1605 包降级`（Uno 7 的依赖已经到 11）。
- 好消息：Uno 7 默认 `SkiaSharpVersion 4.156.0`，但我们的工程实际仍解析到 `SkiaSharp 3.119.2`（没有连带报错）；Uno.Toolkit / Composition 也无需改代码。
- 判定「某个包只发 net11 资产」的方法：`find ~/.nuget/packages/<id>/<ver>/lib -maxdepth 1 -type d`；只发 net11 资产的包在 net10 下会报 `CS1705 System.Runtime 11.0.0.0 高于 10.0.0.0`（`Starward.HPatch` 就是这样）。
- AUR：`dotnet-core-bin`（SDK 11.0.100 / runtime 11.0.0，2026-11-12）与 repo 的 `dotnet-*-bin` 同装到 `/usr/share/dotnet`；只升 SDK 不升 host 会报 `You must install or update .NET to run this application. App: /usr/share/dotnet/sdk/11.0.100/dotnet.dll`。

## L-183　「手写替身」审计：空实现看起来像实现

- 现象（2026-11-25 全量复核 33 个 `*.Linux.cs` + 6 个替身）：27 个有实质实现、10 个是空壳，其中 4 处用户真的会撞上——
  1. `Features/GameRecord/BattleChronicleWindow.Linux.cs` 只 `Content = new Grid()`：点「战绩」弹一个**纯白窗口**，没有任何说明；
  2. `Features/WindowBase.Linux.cs` 的 `SetIcon()` 空 → 除主窗口外任务栏/Alt-Tab 都是通用方块；`ShouldAppsUseDarkMode()/ShouldSystemUseDarkMode()` 恒 `true` → 浅色桌面的 Gacha/更新/过场子窗口被强制深色；
  3. `Features/Update/UpdateWindow.Linux.cs` 的 `IsUpdateNowEnabled => false` → 「立即更新」灰掉且无解释；`UpdateNow()` 只赋 `ErrorMessage` 不走 `SetErrorMessage()`，`x:Bind` 根本不刷新；
  4. `Helpers/XamlRootExtension.Linux.cs` 的 `GetAppWindow()` 兜底 `MainWindow.Current?.AppWindow` → 子窗口/对话框拿到的是**主窗口**的 id（`DisplayInformation.CreateForWindowId` 会按主窗口算 DPI）。
- 教训：`*.Linux.cs` 里「方法签名补齐、函数体空着」编译得过、跑起来不报错，代码和文档都容易当成已完成。审计时按「有没有调用方 → 用户能不能触达 → 触达后看到什么」三问过一遍，比读实现更有效。
- 修法（D129）：能真做就真做——`SetIcon` 复用 `Helpers/AppIcon` 的 `_NET_WM_ICON`；深浅色按 GTK `settings.ini`（`gtk-application-prefer-dark-theme`/`gtk-theme-name`）/ KDE `kdeglobals`（`ColorScheme`/`BackgroundNormal` 亮度）推断，写成 `Helpers/SystemThemePreference`（只读文件、不 fork，硬规则 10）；空窗口改成带说明的占位页；「立即更新」改成可点 + 明确文案；`GetAppWindow` 改为查 `WindowBase` 的窗口登记表（按 `XamlRoot` 引用相等反查），查不到才退主窗口。
- 反面：`Compat/VanaraHotkeyCompat.cs` 里保留着 `[DllImport("user32.dll")] SetWindowLongPtr64/SetWindowLong32`，Linux 上一调就 `DllNotFoundException`。现有调用点都包在 `OperatingSystem.IsWindows()` 里，但这类 P/Invoke 留在移植侧等于埋雷——要么删，要么写清「只有 Windows 语义」。
- 复核手段：`bash scripts/test/lint-doc-status.sh` 里已有的「12-shims 登记集合 == `find` 实际集合」能挡住「文件改名/删除后文档没跟」，挡不住「文件在、函数体空」——后者只能人工读。
## L-184　Uno 7 之后没有可用的 SkiaSharp.Views，视频帧改用双缓冲 WriteableBitmap

**现象**：升级到 Uno.Sdk 7.0.22 / net11 后，`SKXamlCanvas` 所在的 `SkiaSharp.Views.Windows` 类型全部找不到（`CS0234 SkiaSharp.Views`），加回 `SkiaSharp.Views.Uno.WinUI` 又报 `UNOB0023: The runtime-enabled package(s) [SkiaSharp.Views.Uno.WinUI] did not provide any runtime assembly for net11.0-desktop … must be updated to a version built for 7.0`。

**原因（实测包内容）**：
- `SkiaSharp.Views.Uno.WinUI 3.119.2` 有 `uno-runtime/net8.0/skia/SkiaSharp.Views.Windows.dll`，但 Uno 7 只认「为 7.0 构建」的 runtime 资产 → 拒收。
- `SkiaSharp.Views.Uno.WinUI 4.156.0` 的 nupkg 里**完全没有** `uno-runtime/`，只有 `lib/net10.0/SkiaSharp.Views.Windows.dll` 与 `lib/net10.0-windows10.0.19041.0/_._`（从 4.150 起改了布局，Uno 侧资产没了）。
- Uno 6 时代我们从来没在 csproj 里引用过它：它是 `Uno.WinUI 6.7.359` 的传递依赖（旧 `bin/Debug/net10.0-desktop/Lunarward.deps.json` 里能看到 `LibVLCSharp 3.9.7.1` 与 `SkiaSharp.Views.Uno.WinUI 3.119.2` 两条），Uno 7 不再带。

**做法**：删掉 SkiaSharp.Views 依赖，视频壁纸的呈现改成「`WriteableBitmap` 双缓冲交替贴给 `Image`」：
1. `Start()` 里建 A/B 两张 `WriteableBitmap(VideoWidth, VideoHeight)`；
2. 每帧在锁内拷贝 `byte[]` 到「非当前显示」的那张（`bitmap.PixelBuffer.AsStream().Write(...)`），再 `_videoImage.Source = target`；
3. 两张轮换 → 每次赋给 `Image` 的都是**新实例**，绕开 L-71 的「同一实例不重新采样」。

**SkiaSharp 4 的 API 迁移**：`SKFilterQuality` 枚举与 `SKPaint.FilterQuality` 都被删，改用 `SKSamplingOptions`：
```csharp
canvas.DrawImage(image, destRect, new SKSamplingOptions(SKCubicResampler.Mitchell), paint);
bitmap.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell));
```
涉及 `Controls/CachedImage.Linux.cs`、`Helpers/AppIcon.cs`、`Helpers/ImageDecode.cs`、`Features/Tray/StatusNotifierTray.cs`。

**教训**：别把「Uno 的传递依赖」当自己的依赖假设（它随 Uno 大版本消失）；判断某个渲染类包能不能用，先看 nupkg 里有没有 `uno-runtime/<tfm>/`，光看版本号会被 `UNOB0023` 打回来。

## L-185　CommunityToolkit.WinUI.* 在 Uno 工程里会选到 Windows 资产，手写替身反而是必需的

**现象**：按上游 `Starward.csproj` 加了 `CommunityToolkit.WinUI.{Animations,Behaviors,Controls.Primitives,Controls.Segmented,Helpers} 8.2.251219` 后，冒出 41×`CS0012 类型 CoreDispatcher / Color / RadialControllerMenuKnownIcon 在未引用的程序集 Uno, Version=255.255.255.255 中定义`、8×`CS1503 无法从 Microsoft.Xaml.Interactivity.ControlStoryboardAction 转换为 DependencyObject`、`AttachedDropShadow` / `InAppToast` 找不到。

**原因**：这些包在 Uno 项目里解析到 WindowsAppSDK 版本的程序集，于是「Windows 的 DependencyObject」和「Uno 的 DependencyObject」成了两套类型。

**做法**：把这些包从移植侧 csproj 移除，恢复我们自己的替身（`Controls/ToolkitControls.cs`、`Controls/ToolkitCompat.cs`、`Controls/InteractivityCompat.cs`），并按 Uno 7 的实际 API 调整：
- `WrapPanel`/`WrapLayout`：Uno 版只有 `Orientation` + `HorizontalSpacing`/`VerticalSpacing`，**没有 `ItemWidth`/`ItemHeight`** → 自适应列宽改成给每个 `ListViewItem` 设尺寸 + 挂 `ContainerContentChanging`（L-179）。
- `Segmented`：不要设 `ItemsPanel`（Uno 7 的 `ItemsPanelTemplate` 只有 `(object, NewFrameworkTemplateBuilder)` 一种构造，Debug/Release 参考程序集还不一致，会 `CS1729`/`CS0234`）→ 在 `OnApplyTemplate` 里把 `ItemsStackPanel.Orientation` 设成 Horizontal。
- 补 `DependencyObjectExtensions.FindDescendant<T>()`（上游 `LauncherConfigView.xaml.cs` 用它从 GridView 里找 ScrollViewer），注意本项目没开 `Nullable`，泛型返回别写 `T?`（`CS0403`），用 `where T : class`。

**教训**：「上游引了这个包」不等于「移植侧也要引」——先看它在 Uno 下解析到哪套资产。空壳替身比错误引用更安全，但替身必须**真的实现**（L-183）。

## L-186　Uno/Skia 应用起不来时的两处静默失败：漏调 InitEnviroment、漏注册服务

**现象**：`--selftest-ui` 在 xvfb 下 `exit=124`、stdout/stderr 全空、没有任何窗口；日志里只有一行 `Welcome to Lunarward v0.1.0-linux`。

**两个独立原因**（都实测确认）：
1. **GUI 路径没做环境初始化**。移植侧 `Program.Linux.cs` 的 `InitEnviroment()`（`AppConfig.CheckEnviromentAsync().GetAwaiter().GetResult()`）只在 CLI 子命令分支里调用，`host.Run()` 之前没调 → `AppConfig.CacheFolder`/`UserDataFolder` 为 null → `App.OnLaunched` 判定「首次运行」弹 `WelcomeWindow`（标题也是 `Starward`，容易误以为是主窗口），之后 `BuildServiceProvider()` 里 `Path.Combine(CacheFolder, "log")` 拿到 null 就挂。修法：`host.Run()` 前调用一次 `InitEnviroment()`。
2. **DI 里漏注册移植侧自建服务**。`CompanionRunner` 没注册（0.19.0 合并把 provider 文件按上游重写时丢了），`App.OnLaunched` 里 `AppConfig.GetService<CompanionRunner>()` 抛 `InvalidOperationException`，但 `OnLaunched` 是 `async void`，异常被 Uno 的 `UnhandledException` 吞掉 → **没有窗口、没有报错、进程挂在消息循环里**。同类还有 `GameRecordService`、`GameAuthLoginService`。

**排查手法**（Console 在这里不可靠，要落文件）：
- 在关键点 `File.AppendAllText("/tmp/lw-trace.txt", "...")` 打点（GUI 下重定向的 stderr 可能一句都看不到）；
- `dotnet-stack report -p <pid>` 看主线程是否只在 `X11ApplicationHost.RunLoop()` 的 `Thread.Sleep` 里（说明 UI 已空闲 → OnLaunched 早就异常退出了）；
- 逐段隔离：先解析一个已知能用的服务（`PlayTimeRecordService`）打点，确认真的是「某个类型解析失败」而不是 DI 整体挂掉。

**教训**：Uno 的 `async void` 生命周期方法里抛异常**不一定会让进程退出**，表现是「启动没反应」；启动链上的每一步都要么能打点、要么能报错，否则排查成本极高（这次花了 1 小时+）。

## L-187　0.19.0 起安装路径进了 SQLite，CLI 子命令必须先做环境初始化

**现象**：`lunarward --preflight hk4e_cn` 抛 `Microsoft.Data.Sqlite.SqliteException: SQLite Error 1: 'no such table: GameEntry'`（`T-CL-07b` 因此失败，输出里没有「结论」行）。

**原因**：0.19.0 把「游戏装在哪」从配置迁移到 SQLite 的 `GameEntry` 表（`GameEntryService.GetFromId` / `GameLauncherService.GetGameInstallPath`）。`--preflight` 分支当时只调了 `AppConfig.LoadConfiguration()`，它只读配置、**不建库建表**；建库在 `AppConfig.CheckEnviromentAsync()` → `DatabaseService.SetDatabase()` → `InitializeDatabase()` 里。

**做法**：所有会碰数据库的 CLI 分支统一先 `InitEnviroment()`（`Program.Linux.cs` 本来就定义了它，只是 branch 里漏调）。

**教训**：`LoadConfiguration()` 和 `CheckEnviromentAsync()` 不是同一件事——前者只读 `config.ini`，后者才有「定位数据目录 + 建库 + 迁移表 + 读配置」。CLI 分支要读任何持久化状态，都走后者。

## L-188　上游把子目录写成单个字面量（`@"Persistent\X"`），Linux 上会变成文件名的一部分

**现象**：热更新检查 `CheckChannelNameAndScriptVersionAsync` 在 Linux 上恒定返回 false——`Path.Combine(installPath, "YuanShen_Data", @"Persistent\ChannelName")` 得到的是 `…/YuanShen_Data/Persistent\ChannelName`（反斜杠是文件名里的普通字符），`File.Exists` 永远为 false。

**原因**：Windows 的文件 API 把 `\` 和 `/` 都当分隔符，所以上游在单个字符串里写 `@"Persistent\ChannelName"` 能工作；Linux 只认 `/`。

**做法**：拆成多段——`Path.Combine(installPath, folder, "Persistent", "ChannelName")`。同一个文件里还有 `StreamingAssets\res_versions_streaming` 等 11 处，一次性全改（`GenshinHotUpdateService.cs`）。排查这类问题用一条 grep 就够：`grep -rn '@"[A-Za-z0-9_ .]*\\\\' src/`。

**教训**：从 Windows 移植过来的路径拼接，要按「**字面量里有没有反斜杠**」扫一遍，不能只看 `Path.Combine` 用得对不对——上游那一行看起来完全正常。反过来说，也只在这类地方能安全地改：路径语义本来就是平台相对的，改完 Windows 行为不变。

**注意**：路径修对了 ≠ 功能能用了。同一处热更新还依赖注册表（服务器名）与硬链接（打补丁），所以 D131 里给 `CheckUpdateAsync` 加了 `!OperatingSystem.IsWindows()` 的整体关闭——半可用状态会真的触发一次「按错误区域下载」的热更新。
