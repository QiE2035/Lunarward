# 24 依赖盘点：外部进程与手写实现，哪些该换成库

> 状态：**只读盘点，未改任何代码**（2026-11-14）。按用户口径「鼓励第三方依赖，能用的都可以考虑」做一次全仓清点，逐条给出「换 / 不换 / 待定」和理由。
> 判定原则（三条，缺一条就不换）：
> 1. **仍在活跃维护**（近一年有发布；本项目不做长期无人维护的依赖）。
> 2. **行为等价或更好**（换完功能不减、报错不更差）。「更好」通常指：可取消、不占 UI 线程、少一条发行版依赖。
> 3. **不为了换而换**（体积/许可证/多平台行为要算账；能靠已引入的库解决的优先，不新增）。

## 1 外部进程清单（48 处 `Process.Start`）

`grep -rn "Process\.Start(" src/Lunarward --include=*.cs | wc -l` = **48**。

### 1.1 必须保留（外部进程就是功能本身）

| 位置 | 命令 | 用途 | 判定 |
| --- | --- | --- | --- |
| `Features/GameLauncher/GameLauncherService.cs:421` | `wine <exe>` | 启动游戏本体 | **必须**（换库=重写 Wine） |
| `Features/Runtime/PrefixManager.cs:431/465` | `cp -a --reflink=auto` / `wineboot -u` | 克隆前缀 / 建前缀 | **必须**（`wineboot` 属于 Wine；`cp` 的 reflink 语义要的就是内核行为） |
| `Features/GameAccount/WineRegistry.cs:312` | `wine reg query/import` | 游戏注册表（账号、画质） | **必须** |
| `Features/Runtime/ProfileShellService.cs` | `$SHELL` | 调试 Shell | **必须**（用户自己的 shell） |
| `Features/Runtime/RunOnceService.cs` | 用户指定程序 | 「在该环境下运行程序」 | **必须** |
| `Features/Companion/CompanionRunner.cs:74` | 用户指定程序 | 后启动程序 | **必须** |
| `Features/PlayTime/PlayTimeRecordService.cs:334/377` | `wineserver -w` 等 | 游玩时长判定 | **必须** |
| `Features/GameSetting/GameSettingPage.xaml.cs:771`、`RuntimeEnvironmentSetting.xaml.cs:1705`、`AdvancedSetting.xaml.cs:332`、`ScreenshotPage.Linux.cs:136/210`、`ScreenshotViewerWindow.xaml.cs:133`、`OpenUrl.cs:90`、`FileManageSetting.xaml.cs:77/134`、`Program.Linux.cs:343/363` | `xdg-open` / 自身 exe | 打开目录/文件、重启自身 | **保留**（见 §3.4，portal 有兜底成本） |

### 1.2 该换（换成库，去掉一条外部依赖）

| 位置 | 现在 | 换成 | 收益 |
| --- | --- | --- | --- |
| `Features/Runtime/ComponentImporter.cs`（`ExtractAsync`） | `tar -xf` / `tar --zstd -xf` + `xz -d -T0` | `System.Formats.Tar`（BCL）+ `GZipStream`（BCL）+ `ZstdSharp.Port`（已有）+ `SharpCompress`（新增，读 xz） | **可取消**（不再 `Kill` 进程树）；不占 UI 线程（D72 不必再 `Task.Run` 包一层）；解压进度可上报；报错原文更完整；少依赖 `tar`/`xz` 两条外部命令（服务 D73 的「自洽」） |
| `Features/Screenshot/ImageBatchConvertWindow.xaml.cs:542/562/593/613`、`ScreenshotHelper.cs:61/80` | `avifenc` / `cjxl` / `avifdec` / `djxl` | `Magick.NET-Q8-AnyCPU`（15.1.0，ImageMagick 维护方 dlemstra 在维护） | 去掉 4 个外部二进制（AVIF/JXL 编解码）；不再要求用户装 libavif/libjxl 工具。**代价**：体积大（几十 MB），需评估是否只在需要时打包 |
| `Helpers/DesktopNotification.cs:45/70` | `notify-send` / `gdbus call` | `Tmds.DBus`（已有）直连 `org.freedesktop.Notifications` | 复用已有依赖，少两条外部命令；可返回通知 id、可撤回 |

### 1.3 待定（有收益，但有取舍，需单独决策）

| 位置 | 现在 | 候选 | 取舍 |
| --- | --- | --- | --- |
| `Helpers/FileDialogHelper.Linux.cs:25/137` | `kdialog` / `zenity` | XDG Desktop Portal `FileChooser`（`Tmds.DBus`） | portal 在精简桌面可能没实现，要保留 kdialog/zenity 兜底 → 变成两条路径；现有降级链（D31）已能工作，收益不确定 |
| `Helpers/ClipboardHelper.Linux.cs:40` | `xclip` / `wl-copy` | Uno `Clipboard` API（已在用）/ portal | 记录：`Windows.ApplicationModel.DataTransfer.Clipboard` 在本项目里已用于「配置文本」对话框，说明 Uno 侧可用；此 helper 是更早期为 X11 兜底写的，可先看 Uno 是否已稳定再删 |
| `Helpers/OpenUrl.cs:49/90` | `xdg-mime query` / `xdg-open` | Portal `OpenURI` | 同上（sandbox/flatpak 场景 portal 更稳），但需保留兜底 |
| `Features/Screenshot/ScreenCapture.Linux.cs:97` | `spectacle` / `grim` / `gnome-screenshot` | Portal `ScreenCast`（PipeWire）/ `Screenshot` | 收益最大（Wayland 原生、不绑桌面），改动也最大（会话协商、流、编码）；**应单独立项**，不塞进这一批 |
| `Features/GameRecord/WebLoginService.cs:103/155` | `python3` + WebKitGTK 脚本 | `GirCore`（WebKitGTK 绑定）进程内打开登录窗 | 已在 D32 里规划（M2 换 GirCore）；换掉后不再要求系统有 `python3` |
| `Helpers/ProcessQuery.cs:49` | `fuser -v` / `lsof -t +D` | 直接扫 `/proc/*/fd` 与 `/proc/*/maps` | 无库可借，自己扫 `/proc` 反而更可靠（不依赖 `psmisc`/`lsof` 安装）；**倾向自研**（这是「少一条外部依赖」，不是「重复造轮子」） |
| `Helpers/X11KeyState.cs:216/237` | `xdotool search/activate` | 已有的 `Helpers/X11Window.cs`（libxcb） | **内部去重**，不新增依赖：窗口激活已经有 libxcb 实现，`xdotool` 这一处属于历史遗留 |

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
| AVIF/JXL 编码（截图转格式） | `Magick.NET`（见 §1.2） | **换** |

## 3 新增依赖清单（只在需要时加）

| 包 | 最新版本（NuGet，2026-11-14 查） | 许可证 | 用途 | 体积影响 |
| --- | --- | --- | --- | --- |
| `System.Formats.Tar` | BCL（.NET 10 自带） | MIT | 进程内解 tar | 0（框架内） |
| `SharpCompress` | 1.0.0 | MIT | 读 `.tar.xz` | 小（~1 MB） |
| `Magick.NET-Q8-AnyCPU` | 15.1.0 | ImageMagick License（Apache-2.0 兼容） | AVIF/JXL 编解码 | **大**（数十 MB，需评估） |
| `GirCore`（`GirCore.Gtk-4.0` 等） | 见 NuGet（`GirCore` 系列） | LGPL-2.1 | WebKitGTK 登录窗 | 中（需运行时 GTK4/WebKitGTK） |

> 「活跃维护」的核对方式：`nuget.azure.cn` 的 flat container 版本列表（`sharpcompress` 共 162 个版本、`magick.net-q8-anycpu` 共 224 个版本，均有近期发布）。GirCore 的版本号未在此次查询里确认，落地前再核。

## 4 建议批次

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
