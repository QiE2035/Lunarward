# 第三方依赖清单与准入标准

- **状态**：v0.1（版本为 2026-11-01 快照）
- **最后更新**：2026-11-01
- **关联**：要求2（R-12）、D9、D16

---

## 1. 准入标准（必须全部满足才可引入）

| 编号 | 标准 | 判据 |
|---|---|---|
| A1 | **活跃维护** | 近 12 个月有提交；或有明确的「稳定/完成态」声明且无未修高危 issue |
| A2 | **许可证兼容** | 允许 MIT/Apache-2.0/BSD/MPL-2.0；LGPL 需动态链接且可替换；**GPL/AGPL 只能进程边界调用**（D16） |
| A3 | **Linux 一等公民** | 官方支持 Linux 或纯托管无平台依赖；不接受「Windows 专用 + 社区补丁」 |
| A4 | **无原生依赖地狱** | 不要求用户额外安装非包管理器可得的运行时；若有，必须在 AUR/仓库中存在 |
| A5 | **可替换性** | 必须能被替换；不允许依赖内部实现细节 |
| A6 | **体积合理** | 单个依赖引入的间接依赖 ≤ 10 个，或收益显著 |
| A7 | **NuGet 可信** | 官方包或有明确发布者签名；不接受不明来源的二进制包 |

**反例（禁止）**：为「解析 PE 文件」自写解析器、为「读 INI」自写解析器、为「下载」自写断点续传、为「D-Bus」自写协议编解码。

---

## 2. 保留的上游依赖（已在用，确认可继续）

| 包 | 版本 | 许可证 | Linux 可用 | 用途 |
|---|---|---|---|---|
| `CommunityToolkit.Mvvm` | 8.4.2 | MIT | ✅ | MVVM 源生成器 |
| `Dapper` | — | Apache-2.0 | ✅ | Sqlite 访问 |
| `Microsoft.Data.Sqlite` | — | MIT | ✅ | 抽卡/战绩本地库 |
| `MiniExcel` | — | Apache-2.0 | ✅ | 表格导入导出 |
| `Serilog` + sinks | — | Apache-2.0 | ✅ | 日志 |
| `Microsoft.Extensions.*`（DI/Config/Logging） | — | MIT | ✅ | 基础设施 |
| `FluentAssertions`/`xunit` 等测试栈 | — | MIT/Apache | ✅ | 测试 |
| `Google.Protobuf` | — | BSD-3 | ✅ | RPC 协议 |
| `Snap.HPatch` | — | MIT | ⚠️ 仅 Setup | 增量补丁，随 Setup 裁剪 |

**作者自研包**（源码位于上游 `large-resource` 分支 / `Scighost/Win2D` / `TheNathannator/SharpGameInput`，**可 fork 取源码自建**，见 D21）：

| 包 | 版本 | 处置 | 替代 |
|---|---|---|---|
| `Starward.Assets` | 0.6.5 | 保留（纯资源，跨平台） | — |
| `Starward.Codec` | 0.5.2 | **移植**：取 C# 源码建 `src/Lunarward.Codec`（`net10.0`），只删 Windows MFT 的 `VP9Decoder` | 系统/AUR 原生库 `libavif`/`libjxl`/`libultrahdr`/`lcms2`/`brotli`/`libyuv`，DllImport 名称用 `NativeLibrary.SetDllImportResolver` 映射 |
| `Starward.NativeLib` | 0.3.1 | 拆分：`Snap.HPatch` 裁剪（仅 Setup）；`SharpSevenZip` 不可移植 | `SharpCompress`（托管）优先，退化时用 `7z`/`bsdtar` CLI |
| `Starward.GameInput` | 0.3.1 | 不可移植 → 裁剪（M4 另写） | evdev + uinput（先查 `Silk.NET.Input` / `SDL2` 绑定是否够用） |
| `Starward.Win2D` | 1.3.2-release.1 | 不可移植（D2D 投影） → 按接口替换 | SkiaSharp（3 个 D2D1 效果需用 SkSL 或 CPU 重写） |

**需要替换的上游依赖**（见 `04-porting-matrix.md`）：

| 包 | 替换为 | 里程碑 |
|---|---|---|
| `Microsoft.WindowsAppSDK` | Uno Platform 6.6 | M0 |
| `CommunityToolkit.WinUI.*` | Uno 内建 + `CommunityToolkit.Maui` 中可复用部分 / 自绘 | M1 |
| `H.NotifyIcon.WinUI` | 自写 `ITrayService`（StatusNotifierItem），优先复用 `TrayIcon` 类库 | M1 |
| `H.InputSimulator` | 无（裁剪，M4 用 uinput） | M4 |
| `Vanara.PInvoke.*` | 无（接口替换） | M1 |
| `Microsoft.Graphics.Win2D` | SkiaSharp | M1/M3 |
| `ComputeSharp.D2D1` | 无（裁剪） | M3 |
| `Microsoft.Web.WebView2` | Uno 内置 WebView2（`UnoFeatures=WebView`）+ WebKitGTK 4.1；Linux 需 `libwebkit2gtk-4.1` 与 GTK3，Wayland 下必须 `GDK_BACKEND=x11`；部分机器需 `WEBKIT_DISABLE_DMABUF_RENDERER=1` 规避 GBM 黑屏；`CookieManager`/`ExecuteScriptAsync` 未实现（G0-3b） | M1 |
| `SharpGameInput` | evdev（`Silk.NET.Input` 或自写） | M4 |

---

## 3. 拟新增依赖

### 3.1 UI / 平台

| 包 | 候选版本 | 许可证 | 活跃度 | 用途 | 评估 |
|---|---|---|---|---|---|
| `Uno.Sdk` / `Uno.WinUI` | 6.6.166 | Apache-2.0 | 高（月度发版） | WinUI XAML 跨平台 | **采用（D1）** |
| `SkiaSharp` | 3.x | MIT | 高 | 图像解码/缩放/编码（替换 Win2D） | 采用（M3） |
| 系统原生图像库 | libavif 1.4.2 / libjxl 0.12.0 / lcms2 2.19.1 / brotli 1.2.1 / libyuv r2921 | BSD-3 / libjxl BSD-3 / MIT / MIT / BSD-3 | 高 | 供 `Lunarward.Codec` P/Invoke（D21 修订） | **采用**（G0-8 已验证 `libavif.so.16`、`libjxl.so.0.12` 可直连解码） |
| 原生图像库（`libavif` / `libjxl` / `libultrahdr` / `lcms2` / `brotli` / `libyuv`） | 随发行版 | BSD-2/BSD-3/Apache-2.0/MIT | 高（上游活跃） | `Lunarward.Codec` 的 P/Invoke 目标；优先级：系统包 > AUR > vendored 预编译 | **采用**（M3，需先确认 `libultrahdr` 在 Arch/AUR 的可得性，见 Q-033） |
| `SharpCompress` | 0.39+ | MIT | 高 | 替代 `SharpSevenZip`（纯托管解压 7z/zip） | 采用（M2） |
| `ImageSharp` | 3.x | Apache-2.0（部分 Six Labors Split License，商用需注意） | 高 | 备选图像库；本项目非商用，可用 | 备选 |
| `Tmds.DBus` | **0.95.2**（M0 实测版本） | MIT | 中 | D-Bus：通知、托盘、logind、portal | **采用**（G0-6 已验证；**禁用 ≤0.21.x**：GHSA-xrw6-gwf8-vvr9 高危） |
| `Hi3Helper.Sophon` | **1.7.1**（2026-11 最新） | MIT | 高 | Sophon 清单解析 + 分块下载 + 差分更新（内部用 `SharpHDiffPatch.Core` 与 `Hi3Helper.ZstdNet`） | **采用**（Collapse Launcher 生产依赖；实测清单与原神 2744 文件/122.92 GB 完全一致；L-57） |
| `LibVLCSharp` + `libvlc` | 3.x | LGPL-2.1（动态链接） | 高 | 背景视频播放（M2） | 采用（可选功能，A2 满足） |
| `GirCore`（GTK4 绑定） | 0.6+ | MIT | 中 | 仅作 GTK4 头变体验证（Q-005） | 备选 |
| `System.CommandLine` | 2.0 | MIT | 高 | `lunarward-cli` 参数解析 | 采用 |
| `ProcessX` / 自写 `posix_spawn` 封装 | — | MIT | 中 | 进程启动与 `/proc` 观测 | 优先自写薄封装（POSIX 语义简单，A5 反例不适用） |

### 3.2 运行环境 / 组件

| 方案 | 许可证 | 形态 | 用途 | 决策 |
|---|---|---|---|---|
| `winetricks` | LGPL-2.1 | 外部命令 | 前缀初始化、常见依赖安装 | **进程调用**（不链接） |
| `umu-launcher`（`umu-run`） | GPL-3.0 | 外部命令 | 可选的 `runner.type=umu` | 进程调用（D16） |
| `protontricks` | GPL-3.0 | 外部命令 | 探测/辅助（可选） | 进程调用，非必需 |
| `dxvk` / `vkd3d-proton` / `dxvk-nvapi` | zlib / LGPL-2.1 / MIT | 二进制产物 | 图形组件 | 直接分发/下载产物（非代码链接） |
| `anime-game-core`（Rust） | GPL-3.0 | 参考实现 | 组件索引格式、dxvk 安装逻辑参考 | **仅阅读参考，不调用** |
| `wincompatlib`（Rust） | MIT | 参考实现 | wine/dxvk API 设计参考 | **仅阅读参考** |

> 说明：`umu-launcher` 与 `an-anime-team` 系为 GPL-3.0。以「外部可执行文件 + 标准 CLI 参数」方式调用属于聚合（aggregation），不构成衍生作品，符合 D16。**禁止**把其源码复制进本项目。

### 3.3 系统集成

| 能力 | 方案 | 说明 |
|---|---|---|
| 托盘 | `org.kde.StatusNotifierItem` + `com.canonical.dbusmenu`（Tmds.DBus 实现） | KDE/GNOME 扩展/waybar 均支持 |
| 通知 | `org.freedesktop.Notifications` | 与托盘共用 D-Bus 栈 |
| 文件对话框 | `org.freedesktop.portal.FileChooser` | 沙箱/Flatpak 兼容 |
| 屏幕捕获（M4） | `org.freedesktop.portal.ScreenCast` + PipeWire | 仅 M4 评估 |
| 休眠抑制 | `org.freedesktop.login1.Manager.Inhibit` | |
| 开机自启 | `~/.config/autostart/*.desktop` | 无需依赖 |
| 单实例 | `flock` + `UnixDomainSocketEndPoint` | 无需依赖 |
| 全局热键 | X11：`XGrabKey`（`Silk.NET.Input` 或 `X11` bindings）；Wayland：降级 | D11 |

### 3.4 构建 / 打包

| 项 | 方案 |
|---|---|
| SDK | .NET 10（`net10.0` 单 TFM） |
| 构建 | `Uno.Sdk` + `dotnet publish -r linux-x64`（NativeAOT 可选，M2 评估） |
| 打包 | `makepkg`（AUR）+ tarball 自更新 |
| CI | GitHub Actions Linux runner（`dotnet test` + `publish` + 冒烟脚本） |

---

## 4. 明确不引入

| 候选 | 理由 |
|---|---|
| Avalonia | 路线 B（D1 未采纳）；仅在 M0 失败时重新评估 |
| Electron / Tauri | 与「复用 Starward XAML」目标冲突，等于重写 |
| Proton 官方脚本作为唯一运行器 | 会额外 fork，违反 R-06 |
| `wine-staging` 强绑定 | 用户可自选，组件库不锁版本（D9） |
| 任何 Windows-only 原生库 | 违反 A3 |
| 任何未维护的 DXVK 安装脚本 | 逻辑简单但版本矩阵复杂，改用组件库 + 指纹 |

---

## 5. 版本与锁定策略

1. **锁定**：`Directory.Packages.props` 统一版本，禁止随意浮动；
2. **升级节奏**：Uno 跟随 minor（每 2 周检查）；SkiaSharp/Tmds.DBus 跟随 patch；
3. **升级前置**：升级后必须跑 `07-roadmap.md` 的回归清单（含两款真机游戏）；
4. **安全**：`dotnet list package --vulnerable` 纳入 CI；
5. **许可证清单**：`THIRD-PARTY-NOTICES.md` 由 CI 生成（`nuget-license` 工具），随发布附带。

---

## 6. 依赖风险登记（与 `06-risks.md` 联动）

| 依赖 | 风险 | 缓解 |
|---|---|---|
| Uno 6.6 Linux 后端 | 最大单点风险：XAML 兼容、性能、稳定性 | M0 门禁；备选 GTK4 头；最终备选 Avalonia |
| Tmds.DBus | 托盘协议兼容性（不同桌面/合成器） | 分桌面验证矩阵；失败降级为「无托盘 + 窗口常驻」 |
| SkiaSharp | 原生库版本与 musl/glibc | 官方 NuGet 提供 Linux-x64 native，纳入 CI 冒烟 |
| LibVLCSharp | 需要 `vlc` 运行时（AUR 可得） | 特性开关，缺失时禁用视频背景 |
| `umu-run` | GPL 边界、参数演进 | 仅实验性 runner；参数以 CLI 文档为准，失败不阻断 wine 路径 |
