# 24 依赖盘点：外部进程与手写实现，哪些该换成库

> 状态：**盘点基线 + 执行记录**（2026-11-14 初版，2026-11-19 补 P5 结论，2026-11-20 文档整理）。
> 最后核对：2026-11-26（0.19.0 基线，HEAD `f3ea6f87`，D135–D138 之后；口径行复测：`Process.Start(` 字面量 **49**、其中死代码 **5**、参与编译 **44**、非字面量 **2** → **存活 46**）。按用户口径「鼓励第三方依赖，能用的都可以考虑」做一次全仓清点，逐条给出「换 / 不换 / 待定」和理由。
> 判定原则（三条，缺一条就不换）：
> 1. **仍在活跃维护**（近一年有发布；本项目不做长期无人维护的依赖）。
> 2. **行为等价或更好**（换完功能不减、报错不更差）。「更好」通常指：可取消、不占 UI 线程、少一条发行版依赖。
> 3. **不为了换而换**（体积/许可证/多平台行为要算账；能靠已引入的库解决的优先，不新增）。

## 1 外部进程清单（存活 46 处）

统计口径（2026-11-26 复测，HEAD `f3ea6f87`）：

| 口径 | 数量 | 怎么算 |
| --- | ---: | --- |
| `Process.Start(` 字面量 | **49** | `grep -rn "Process\.Start(" src/Lunarward --include=*.cs \| wc -l` |
| 其中死代码 | **5** | 文件被 `<Compile Remove>` 排除（`grep -c "<Compile Remove" src/Lunarward/Lunarward.csproj` = 46 条，命中 4 个文件共 5 行） |
| 参与编译的字面量 | **44** | 49 − 5 |
| 非字面量、但仍是外部进程 | 2 | `new Process{…}` + `process.Start()`：`Features/PlayTime/PlayTimeSession.Linux.cs:73`（跑 `wineserver -w`）、`Features/Runtime/ComponentHealth.cs:98`（跑 `wine --version`） |
| **存活合计** | **46** | 44 + 2 |

死代码 5 处（D137/D138 把截图二代与批量转换窗口整批删掉后从 9 降下来）：`Features/RPC/RpcService.cs:51`、`Features/Screenshot/ScreenshotHelper.cs:61/80`、`Features/Update/SetupService.cs:133`、`Features/Update/UpdateWindow.xaml.cs:418`。

下表按用途归类，未逐行列全 46 处（44 处参与编译的字面量，加 2 处非字面量）；跑上面的 grep 可复现全量位置。

### 1.1 必须保留（外部进程就是功能本身）

| 位置 | 命令 | 用途 | 判定 |
| --- | --- | --- | --- |
| `Features/GameLauncher/GameLauncherService.cs:479` | `wine <exe>` | 启动游戏本体 | **必须**（换库=重写 Wine） |
| `Features/Runtime/PrefixManager.cs:404/491` | `cp -a --reflink=auto` / `wineboot -u` | 克隆前缀 / 建前缀 | **必须**（`wineboot` 属于 Wine；`cp` 的 reflink 语义要的就是内核行为） |
| `Features/GameAccount/WineRegistry.cs:339` | `wine reg query/import` | 游戏注册表（账号、画质） | **必须** |
| `Program.Linux.cs:365/385` | `wine --version` / `wine cmd /c echo` | `--runner-check` 自检（CLI） | **必须**（被测对象就是 Wine） |
| `Features/Runtime/ProfileShellService.cs` | `$SHELL` | 调试 Shell | **必须**（用户自己的 shell） |
| `Features/Runtime/RunOnceService.cs` | 用户指定程序 | 「在该环境下运行程序」 | **必须** |
| `Features/Companion/CompanionRunner.cs:74` | 用户指定程序 | 后启动程序 | **必须** |
| `Features/PlayTime/PlayTimeRecordService.cs:378/421` | `wineserver -w` 等 | 游玩时长判定 | **必须** |
| `Features/GameSetting/GameSettingPage.xaml.cs:800`、`RuntimeEnvironmentSetting.xaml.cs:2448`、`AdvancedSetting.xaml.cs:333`、`ScreenshotPage.Linux.cs:230/328`、`ScreenshotViewerWindow.xaml.cs:133`、`OpenUrl.cs:118`、`FileManageSetting.xaml.cs:77/134` | `xdg-open` / 自身 exe | 打开目录/文件、重启自身 | **保留**（见「执行说明」：portal 优先，但保留兜底） |

### 1.2 已换（原「该换」3 项，2026-11-14 全部完成）

| 位置 | 现在 | 换成 | 收益 |
| --- | --- | --- | --- |
| `Features/Runtime/ComponentImporter.cs:169-171`（`ExtractAsync`） | ✅ 已不调系统命令：全部委托 `ArchiveExtractor`（BCL `TarReader` + `GZipStream` + `ZstdSharp` + `SharpCompress` 读 xz + `ZipFile`）；文件头 `:6-7` 注明 2026-11-14 起不再 fork `tar`/`xz` | `System.Formats.Tar`（BCL）+ `GZipStream`（BCL）+ `ZstdSharp.Port`（已有）+ `SharpCompress`（1.0.0，读 xz） | **可取消**（不再 `Kill` 进程树）；不占 UI 线程（D72 不必再 `Task.Run` 包一层）；解压进度可上报；报错原文更完整；少依赖 `tar`/`xz` 两条外部命令（服务 D73 的「自洽」） |
| `Helpers/MagickCodec.cs` | ✅ 已换（D75）：`Magick.NET-Q8-AnyCPU` 15.1.0，`MagickCodec.cs:4` 注明带 libheif/libjxl delegate；Skia 不解 AVIF/JXL，统一走 `Helpers/ImageDecode`。原 `avifenc`/`cjxl`/`avifdec`/`djxl` 那几处（`ImageBatchConvertWindow.xaml.cs:542/562/593/613`、`ScreenshotHelper.cs:61/80`）**本身就在 `<Compile Remove>` 里**，早已不参与编译 | `Magick.NET-Q8-AnyCPU`（ImageMagick 维护方 dlemstra 在维护） | 去掉 4 个外部二进制（AVIF/JXL 编解码）；不再要求用户装 libavif/libjxl 工具。**代价**：体积大（实测见「执行记录」P4 行），publish 只留本机 RID |
| `Helpers/DesktopNotification.cs` | ✅ 已换（2026-11-14）：`Tmds.DBus` 直连 `org.freedesktop.Notifications` 为主（`:65`）；`notify-send` / `gdbus` 只在 D-Bus 失败时兜底（`:109`/`:133`），且硬规则 10 禁止 UI 线程 fork | `Tmds.DBus`（已有） | 复用已有依赖，少两条外部命令；可返回通知 id、可撤回 |

### 1.3 有取舍的项（现状：多数已做，剩余仍待定）

| 位置 | 现在 | 候选 | 取舍 |
| --- | --- | --- | --- |
| `Helpers/FileDialogHelper.Linux.cs:29/186` | ✅ portal 优先（`org.freedesktop.portal.FileChooser`，含 filters / multiple / directory / 保存建议名），`kdialog` → `zenity` 兜底 | XDG Desktop Portal `FileChooser`（`Tmds.DBus`） | ✅ 2026-11-14 已完成（P3）：portal 在精简桌面可能没实现，所以保留 kdialog/zenity 兜底（D31 降级链） |
| `Helpers/ClipboardHelper.Linux.cs:13/72/129` | 文本已走 Uno `Clipboard`（`DataPackage.SetText` + `SetContent`，非 UI 线程经 `DispatcherQueue` 排队）；PNG 与 `text/uri-list` 仍调 `wl-copy` / `xclip` | Uno `Clipboard` API（已在用） | ✅ 2026-11-27 **已定**（D140）：Uno Skia 后端只实现文本（官方文档：SetContent/GetContent 可用，但「currently support textual data on all platforms」），文本通道已用外部 Xlib 客户端读回验证；`SetBitmap`/`SetStorageItems` 在 Linux 无实现 → 图片/文件列表只能外部命令，`wl-clipboard`/`xclip` 登记为 optdepends（`xsel` 不能带 MIME，已删除） |
| `Helpers/OpenUrl.cs:57/118` | ✅ `org.freedesktop.portal.OpenURI` 优先，`xdg-open` → `gio open` 兜底 | Portal `OpenURI` | ✅ 2026-11-14 已完成（P2）：sandbox/flatpak 场景 portal 更稳，兜底保留 |
| `Features/Screenshot/ScreenCapture.Linux.cs:148` | `spectacle` / `grim` / `gnome-screenshot` 优先，portal `Screenshot` 兜底（**刻意保留**外部优先） | Portal `ScreenCast`（PipeWire）/ `Screenshot` | **部分已做**：portal 兜底已落地（P4）；PipeWire 路线没做——收益最大（Wayland 原生、不绑桌面），改动也最大（会话协商、流、编码），**应单独立项** |
| `Features/GameRecord/WebLoginService.cs:105/129` | ✅ 登录窗 = 本程序子进程 + WebKitGTK 6.0（`--webkit-login`，D76）；`python3` 助手只作旧系统兜底（`:129`） | `GirCore`（WebKitGTK 绑定）进程内打开登录窗 | ✅ 2026-11-14 已完成 A 方案（D76）：不再要求系统有 `python3`（仅兜底）。B 方案（同进程）实测能跑，但 GTK3/GTK4 并存、标题栏观感不统一，不采用 |
| `Helpers/ProcessQuery.cs:119` | ✅ 已改读 `/proc/<pid>/fd` 的符号链接，不再 fork `fuser` / `lsof` | 直接扫 `/proc/*/fd` 与 `/proc/*/maps` | ✅ 2026-11-14 已完成：不依赖 `psmisc`/`lsof` 安装，只读、可取消 |
| `Helpers/X11KeyState.cs:266/287` | ✅ 首选进程内 libxcb（`X11Window.Activate`），`xdotool` 仅兜底 | 已有的 `Helpers/X11Window.cs`（libxcb） | ✅ 2026-11-14 已完成：**内部去重**，不新增依赖 |

## 2 手写实现盘点（非进程类）

| 现在手写的 | 已有/可选库 | 判定 |
| --- | --- | --- |
| JSON 读写、原子写 | `System.Text.Json`（BCL）+ 自研 `Helpers/JsonFile.cs`（.tmp→.bak→改名） | **保留**：库不管「原子写」语义，这层必须自己写 |
| 哈希（SHA-256/MD5）、hex、BOM | BCL `System.Security.Cryptography` / `System.IO.Hashing`（已引） | 保留（已在用库） |
| zip 备份 | `System.IO.Compression`（BCL） | 保留（已在用） |
| zstd 解压（Sophon / 组件） | `ZstdSharp.Port`（已引） | 保留 |
| SQLite | `Microsoft.Data.Sqlite` + `Dapper`（已引） | 保留 |
| ini 解析 | `Microsoft.Extensions.Configuration.Ini`（已引） | 保留 |
| 版本比较（wine/DXVK 版本、AUR） | `NuGet.Versioning`（已引） | 保留 |
| HTTP（Sophon、组件下载、API） | `HttpClient` + `Microsoft.Extensions.Http`（已引） | 保留 |
| 注册表文本解析（`RegistryFormat`） | 无对应库（`reg query` 输出格式是 Wine/Windows 私有） | 保留（自研是唯一选择，已有单测） |
| 图片解码/缩放（截图、壁纸、图库） | `SkiaSharp`（Uno 传递依赖） | 保留 |
| 视频解码 + vmem 出图 | `LibVLCSharp`（Uno 传递依赖） | 保留 |
| 托盘（StatusNotifierItem） | `Tmds.DBus`（已引，自研 SNI 协议层） | 保留：没有维护中的 SNI 客户端库能等价替代 |
| D-Bus 调用（通知、媒体控制） | `Tmds.DBus`（已引） | 保留 |
| X11 热键轮询、窗口 map/unmap | `libxcb` P/Invoke（自研 `Helpers/X11Window.cs`） | 保留：Wayland 下本就不可用，没有库能做「X11 全局热键」 |
| 时间格式化、日志、i18n | BCL / `Serilog` / 自研 `Lang` | 保留 |
| AVIF/JXL 编码（截图转格式） | `Magick.NET`（见 §1.2） | **已换（D75）**，入口 `Helpers/MagickCodec.cs` |

## 3 新增依赖清单（均已落地；版本以 `src/Lunarward/Lunarward.csproj` 实钉为准）

| 包 | 版本（csproj 实钉，2026-11-24 核） | 许可证（nuspec） | 用途 | 体积影响 |
| --- | --- | --- | --- | --- |
| `System.Formats.Tar` | BCL（.NET 11 自带） | MIT | 进程内解 tar | 0（框架内） |
| `SharpCompress` | 1.0.0 | MIT | 读 `.tar.xz` | 小（~1 MB） |
| `Magick.NET-Q8-AnyCPU` | 15.1.0 | Apache-2.0（底层 ImageMagick 自身是 ImageMagick License） | AVIF/JXL/HEIC 编解码 | **大**（8 个 RID native 约 237 MB，见执行记录） |
| `GirCore.Gtk-4.0` / `GirCore.WebKit-6.0` | 0.8.1 | **MIT** | WebKitGTK 登录窗（spike；实际落地是 `--webkit-login` 子进程，见 D76） | 中（需运行时 GTK4/WebKitGTK） |

> 「活跃维护」的核对方式：`nuget.azure.cn` 的 flat container 版本列表（`sharpcompress` 共 162 个版本、`magick.net-q8-anycpu` 共 224 个版本，均有近期发布）。GirCore 已实钉 0.8.1，两包 nuspec 均为 `<license type="expression">MIT</license>`。

## 4 建议批次（P1–P4 已全部完成，保留作回溯；现状见「执行记录」）

1. **P1（先做）**：tar/xz → `System.Formats.Tar` + `GZipStream` + `ZstdSharp.Port` + `SharpCompress`。理由：正好和「下载入口 + 进度 + 可取消 + 空间预检」同一批，解压可取消了「取消下载」才是真的取消。
2. **P2**：桌面通知 → `Tmds.DBus`；剪贴板/打开链接的 portal 化（要看 Uno 现状再定）。
3. **P3**：文件对话框 portal 化（保留 kdialog/zenity 兜底）。
4. **P4（单独立项）**：截图 portal 化、登录窗 GirCore 化、AVIF/JXL 换 Magick.NET（体积问题要单独讨论）。
5. 顺手做：`xdotool` 两处改用已有 `Helpers/X11Window.cs`；`ProcessQuery` 改扫 `/proc`。

## 5 明确不换

- Wine 系一切（`wine` / `wineboot` / `wine reg` / `wineserver`）：外部进程就是被测对象。
- `cp --reflink=auto`：要的就是文件系统语义。
- 用户自己的程序（调试 Shell、一次性运行、后启动程序、游戏本体）。
- 原子写、注册表文本解析、SNI 托盘协议、X11 热键轮询：没有等价且维护中的库。

---

## 执行记录

| 批次 | 状态 | 说明 |
| --- | --- | --- |
| P1 tar/xz 换库 | ✅ 2026-11-14 | 新增 `ArchiveExtractor`（BCL `TarReader` + `GZipStream` + `ZstdSharp` + `SharpCompress` for xz + `ZipFile`），删除 `ComponentImporter` 里的 `tar` / `xz` 子进程与 `TryKill`；解压现在可取消、有进度、拦路径逃逸（23 号 §13.8） |
| P1 下载入口/进度/取消/空间预检 | ✅ 2026-11-14 | 页面「下载组件…」/「导入归档…」；取消=暂停（`.part.N` 续传）；GitHub 限流时给可操作提示 + 支持 `LUNARWARD_GITHUB_TOKEN` |
| P2 桌面通知 / 打开链接 portal 化 | ✅ 2026-11-14 | `DesktopNotification` 不再 fork `notify-send`，直接 D-Bus `org.freedesktop.Notifications`（失败才退外部命令）；`OpenUrl` 先走 `org.freedesktop.portal.OpenURI`，不可用才 `xdg-open` → `gio open` |
| P3 文件对话框 portal 化 | ✅ 2026-11-14 | `FileDialogHelper` 先走 `org.freedesktop.portal.FileChooser`（含 filters / multiple / directory / 保存建议名），portal 不可用才 kdialog → zenity |
| P4 截图 portal 化 | ✅ 2026-11-14 | `ScreenCapture.Linux` 在 spectacle / grim / gnome-screenshot 全失败后走 `org.freedesktop.portal.Screenshot`；**外部工具优先**的理由见下 |
| P4 AVIF / JXL 编解码（Magick.NET） | ✅ 2026-11-14 | 新增 `Helpers/MagickCodec.cs` + `Helpers/ImageDecode.cs`（Skia 优先、AVIF/JXL/HEIC 走 Magick）；`ComponentDownloader` 之外的四个解码点统一走 `ImageDecode`；`--image-info` 可自检。**体积代价**：`Magick.NET-Q8-AnyCPU` 15.1.0 解包后 **8 个 RID** 的 native 合计约 **237 MB**（linux-x64 37 MB、linux-musl-x64 40 MB、win-x64 24 MB…）；Debug 输出是全拷，本机 `src/Lunarward/bin/Debug/net11.0-desktop/runtimes` 实测 **725 MB / 25 个 RID 目录**（2026-11-14 实测）；publish 单 RID 只留一个，linux-x64/native 约 **52 MB** = Magick.Native 38,834,992 B + libSkiaSharp 约 11 MB + libHarfBuzzSharp 约 2.4 MB + libe_sqlite3 约 1.4 MB |
| P4 登录窗 WebKitGTK 6.0 化 | ✅ 2026-11-14（D76） | 先前的「不改」结论**已撤回**：当时只查了本机有没有装，没查仓库——`extra/webkitgtk-6.0 2.54.2-1` 一直有，用户装上后 spike 通过。落地方式是 **A 方案**：登录窗仍是独立进程，但实现换成「本程序 + WebKitGTK 6.0」（`--webkit-login`），去掉 python3 依赖；B 方案（同进程）实测也能跑，但 Uno 自带 GtkSharp 3.24（GTK3）与 GTK4 并存、且 GTK 窗口标题栏跟系统配色走，观感不统一，故不采用 |
| 顺手：`xdotool` → 进程内 xcb | ✅ 2026-11-14 | 新增 `X11Window.Activate`（EWMH `_NET_ACTIVE_WINDOW`），`X11KeyState.ActivateWindow` 先走它，`xdotool` 仅兜底 |
| 顺手：`ProcessQuery` 改扫 `/proc` | ✅ 2026-11-14 | 不再 fork `fuser` / `lsof`：读 `/proc/*/fd` 的符号链接按路径前缀匹配（跳过无权限进程，可取消） |

| P5 ValveKeyValue（Steam 自定义库） | ✅ 2026-11-15（D78） | 新增 `Features/Runtime/SteamLibrary`：解析 `steamapps/libraryfolders.vdf`（新旧两种格式）+ 每个库的 `steamapps/compatibilitytools.d` 与 `steamapps/common`；`ComponentStore.SteamCompatRoots()` 从硬编码 4 条路径改成走它。**这是真功能缺口**：Steam 装在自定义库时一个 Proton 都扫不到 |
| P5 PeNet（预检架构项） | ✅ 2026-11-15（D78） | 新增 `Features/Runtime/PeArchitecture`：PE machine → `X86/X64/Arm64`，预检「游戏架构」项比对前缀架构（x64+win32 → Failed；x86+win64 → WoW64 Warning；ARM64 → Failed） |
| P5 SharpHook（换 X11 热键轮询） | ❌ 2026-11-15（D78） | spike 实测否决：KDE Wayland 下 `TaskPoolGlobalHook` enabled 但 12 s 内 0 事件，XTEST 与强制 X11 后端都收不到；对照 `XQueryKeymap` 能看到。**保留 `X11KeyState`**；Wayland 正解是 `org.freedesktop.portal.GlobalShortcuts`（本机已暴露，列为后续项） |
| P5 Tmds.DBus → Tmds.DBus.Protocol | ❌ 2026-11-15（D78） | 921 行、4 个 `[DBusInterface]`（含 FD 与请求句柄），Protocol 是源生成器模型（无 `CreateProxy`），托盘/通知/portal 三条链路无自动化覆盖 → 收益（性能/AOT）小于回归风险。**前置条件**：先有这三条的集成断言 |
| P5 System.CommandLine（重构 CLI） | ⚠️ 改成自研声明表 2026-11-15（D79） | 换库目的（帮助不漂移 + 校验 + 拼错提示）用 `Helpers/CliSpec.cs` 一张表达成；System.CommandLine 3.0 的扁平 help 会丢掉现有分组可读性，重写 1903 行分发风险不成正比。包已移除；表就是「声明层」，将来可机械转换 |

### 执行说明：为什么 portal 优先，但截图不优先

- **打开链接 / 选文件：portal 优先**。理由：沙盒（flatpak、纯 Wayland）里 fork 出去的 `xdg-open` 看不到宿主文件系统，`kdialog` 也不一定有；portal 是这些场景唯一的正规通道，而原生安装下 portal 也几乎必然存在（KDE/GNOME 都自带 `xdg-desktop-portal`）。
- **截图：外部工具优先，portal 兜底**。理由：`spectacle -b -n -f` 是静默全屏，按一次热键就出图；portal 的 `Screenshot` 在部分桌面上会弹授权或选区，把「静默截图」变成「每次都要点一下」。只有外部工具都不在（沙盒 / 精简系统）时才用 portal。
- 两者都保留兜底路径，任何一侧失败都不会静默：失败会写日志并在返回值上体现。

---

## 6　2026-11-27 复审：还有哪些「有依赖却手写」

用户口径：**鼓励第三方依赖**，只排除「上游自己造的那些」（`src/Starward/` 里的手写实现属于上游，不动）。

方法：全仓 `DllImport` 清点（仅 19 处）+ 逐个读 Linux 专属 helper + 抽查 Features 里的解析/并发/哈希/版本比较，逐条问「已引入的库或 BCL 里有没有等价体」。

| 位置 | 现在 | 现成轮子 | 判定 |
| --- | --- | --- | --- |
| `Helpers/PosixIdentity.cs:15-32` | ✅ 已换（D140）：`geteuid()` P/Invoke 与用户名兜底删除，改 `Environment.IsPrivilegedProcess` | .NET 8+ `Environment.IsPrivilegedProcess`（语义就是 euid==0；实测 False） | ✅ 2026-11-27：一行替代，Windows 上先排除（该属性在 Windows 表示管理员提权，与 wine 的 root 限制无关） |
| `Helpers/DataFolder.Linux.cs:19-23` | ✅ 已换（D140）：`DataHome` 直接取 `SpecialFolder.LocalApplicationData` | `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)`（.NET 在 Unix 上本身就读 `XDG_DATA_HOME`；实测默认 `~/.local/share`，`XDG_DATA_HOME=/tmp/xdgtest` 时返回该值） | ✅ 2026-11-27：删掉手写分支 |
| `Helpers/PosixIdentity.cs:45-88` | `stat` P/Invoke 读 `(st_dev, st_ino)` 做文件身份键 | 无 BCL 等价（`File.GetUnixFileMode` 不含 inode） | 保留（B27 已登记；偏移 x86_64/aarch64 一致的前提要留注释） |
| `Helpers/JsonFile.cs:135-160` | libc `open`/`fsync`/`close` 对**目录** fsync | BCL 无目录 fsync（`FileStream.Flush(true)` 只刷文件） | 保留 |
| `Helpers/X11KeyState.cs`（13 处）/ `Helpers/X11Window.cs`（xcb 15 处） | libX11 / libxcb P/Invoke | 无维护中的托管绑定（SharpHook 已实测否决，见 P5） | 保留 |
| `Helpers/DriveHelper.Linux.cs` | `/proc/mounts` + `statvfs` | `DriveInfo` 在 Linux 不给「可移动/网络」语义 | 保留 |
| `Helpers/XdgFolders.cs` | 解析 `user-dirs.dirs` 的 `XDG_DOWNLOAD_DIR` | 无（.NET 不读 user-dirs.dirs） | 保留 |
| `Helpers/AppIcon.cs` | SkiaSharp 解 PNG → `_NET_WM_ICON` | SkiaSharp（已在用） | 保留 |
| `Helpers/FileHashHelper.cs` | BCL `SHA256` | 已用 BCL | 保留 |
| `Helpers/FileCache.cs` | 磁盘缓存但**内部已用 FusionCache** | FusionCache（已在用） | 保留 |
| `Features/Background/VideoThumbnailer.cs` | LibVLCSharp `vmem` 回调抽帧 | LibVLCSharp（已在用） | 保留 |
| 并发/哈希/版本 | 无自研线程池；`System.IO.Hashing`（XxHash64/Crc32）、`NuGetVersion`、`File.SetUnixFileMode`、`Process.Kill(entireProcessTree)` 均按现代 API 用 | — | 无重复 |

结论：上面两条 BCL 替换已于 2026-11-27 落地（D140），此外没有「已引入依赖却在重造」的实例；`xdotool` 仅剩 `X11KeyState` 的兜底用途（libxcb 失败时），保留并在 PKGBUILD 里登记。
