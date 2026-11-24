# 决策记录（ADR）

- **状态**：生效中（D0–D123）
- **最后更新**：2026-11-24
- **规则**：本文件是唯一施工依据。新增决策只能追加；变更必须记录「变更原因 + 影响范围 + 受影响文档」。
- **历史文档引用**：正文里出现的 `01`–`17`、`20`–`22` 号文档已于 2026-11-20 从仓库删除（已完成使命，或被 `12`/`13`/`23`/`24` 取代）。引用处保留原编号便于回溯；原文用 `git log --diff-filter=D -- docs/linux-port/` 找到删除提交后 `git show <commit>^:<路径>` 取回。
- **确认方式**：
  - `明确` = 用户在对话中直接选定（附消息引用）
  - `按推荐` = 用户指示「其余规划先按推荐」，默认生效，逐条复核时再转「明确」

---

## 决策汇总

| ID | 主题 | 结论 | 状态 | 确认方式 | 日期 |
|---|---|---|---|---|---|
| D0 | 「体验一致」的判据 | 结构/流程/文案/交互节奏为硬对齐；窗口装饰、模糊、托盘、系统对话框、字体渲染为软差异；允许在 Linux 上局部做得更好，但不得改动既有页面信息架构 | 已确认 | 按推荐 | 2026-11-01 |
| D1 | 技术路线 | **Uno Platform 6.6 单头 Linux**（复用 Starward 的 XAML 与业务代码）；Windows 头不维护；M0 门禁 PoC 不通过则重新立项评估 Avalonia 重写 | 已确认 | 按推荐 | 2026-11-01 |
| D2 | 仓库与上游同步 | fork Starward；平台层与运行环境子系统独立目录/项目；上游 Windows 专属代码**保留但不参与构建**；对上游做单向 cherry-pick | 已确认 | 按推荐 | 2026-11-01 |
| D3 | 平台抽象方式 | 接口 + DI + Linux 单实现；删除 Windows 实现；禁止大面积 `#if WINDOWS`（只允许文件级排除） | 已确认 | 按推荐 | 2026-11-01 |
| D4 | 术语 | 组件/运行器/前缀/运行环境/游戏条目/启动计划/启动会话/伴随启动/监控代理/调试终端（见 README 术语表） | 已确认 | 按推荐 | 2026-11-01 |
| D5 | 前缀隔离默认策略 | 默认 `template`：基础前缀模板 + `cp -a --reflink=auto` 克隆；模式集合 `template/dedicated/shared/external/ephemeral` | 已确认 | 按推荐 | 2026-11-01 |
| D6 | 图形组件注入 | `ensure-at-launch`：启动前按运行环境声明把 DXVK/VKD3D 组件就位（内容寻址库 + 按需替换），**不重建前缀** | 已确认 | 按推荐 | 2026-11-01 |
| D7 | 伴随启动 | 触发集合：`on_game_started` / `after_delay` / `on_window_mapped` / `on_log_match` / `on_game_exit`；条目分「独立进程」与「注入型工具」两类 | 已确认 | 按推荐 | 2026-11-01 |
| D8 | 调试终端范围 | 环境一致 shell + 单次命令执行 + 前缀工具（winecfg/regedit/winefile）+ 日志跟随 | 已确认 | 按推荐 | 2026-11-01 |
| D9 | 组件来源 | 以本地登记（系统/Steam/Lutris/Bottles/Heroic/自定义）为主；内置 GitHub 下载为可选增强；不依赖 umu-database 作为唯一来源 | 已确认 | 按推荐 | 2026-11-01 |
| D10 | 分发 | AUR 优先 + tarball 自更新；首发不做 Flatpak；AppImage 作为后续可选 | 已确认 | 按推荐 | 2026-11-01 |
| D11 | 桌面集成 | 托盘（StatusNotifierItem）、通知（D-Bus）、`.desktop`、`starward://` URL scheme 全做；全局热键在 Wayland 下降级为应用内快捷键 | 已确认 | 按推荐 | 2026-11-01 |
| D12 | 登录与内嵌网页 | 优先 Uno 的 WebView2 兼容层；M0 验证失败则切「系统浏览器 + cookie 导入」 | 已确认 | 按推荐 | 2026-11-01 |
| D13 | HDR / Win2D 功能 | M1 先降级为 SDR（不做 HDR 增益图处理）；M3 用 SkiaSharp/ImageSharp 重写 | 已确认 | 按推荐 | 2026-11-01 |
| D14 | 游戏内 Overlay / 游戏截图 | M1 只做「读取游戏自带截图目录」；portal + PipeWire 方案推迟到 M4 评估 | 已确认 | 按推荐 | 2026-11-01 |
| D15 | 真机验收范围 | **原神 `hk4e_cn` + 绝区零 `nap_cn`** 必须跑通 | 已确认 | **明确**（m00119） | 2026-11-01 |
| D16 | 许可证边界 | 本项目沿用 MIT 并保留上游版权声明；GPL-3.0 组件（umu-launcher、an-anime-team 系）**只允许进程边界调用**，不得链接或复制代码 | 已确认 | 按推荐 | 2026-11-01 |
| D17 | 配置与数据路径 | 全面 XDG：`~/.config/starward`、`~/.local/share/starward`、`~/.cache/starward`、`~/.local/state/starward`、`$XDG_RUNTIME_DIR/starward` | 已确认 | 按推荐 | 2026-11-01 |
| D18 | 视觉基线 | 建立 `golden screenshots` 截图回归集，以上游版本截图 + 源码为规格基线（不构建 Windows 版） | 已确认 | 按推荐 | 2026-11-01 |
| D19 | 上游跟进节奏 | 每 2 周检查一次上游 release；每个 minor 版本评估是否 cherry-pick | 已确认 | 按推荐 | 2026-11-01 |
| D20 | 构建与 CI | 单 TFM `net10.0`（去掉 `-windows`）；Uno.Sdk；CI 仅 Linux；不使用 WinAppSDK/MSIX/代码签名 | 已确认 | 按推荐 | 2026-11-01 |
| D21 | 上游闭源自研包（**已补全源码后修订**） | 源码位于上游 `large-resource` 分支，已镜像到本仓 `vendor/upstream/large-resource/`（只读、不参与构建）。判定：`Starward.Codec` **可移植**（C# 包装层 + 跨平台原生库 libavif/libjxl/libultrahdr/lcms2/brotli/libyuv，仅 `VP9Decoder` 为 Windows MFT 需去除）；`Starward.NativeLib` 中 `Snap.HPatch` 可移植、`SharpSevenZip`（7z.dll COM + kernel32）**不可移植**，改 `SharpCompress`/`7z` CLI；`Starward.Win2D`（microsoft/Win2D fork，D2D/WinRT）与 `SharpGameInput`（GameInput API 绑定）**不可移植**，替换/裁剪；`Starward.Assets` 保留 | 已确认 | **明确授权**（m00210 第 5 条：先补全源码再重新决策） | 2026-11-01 |
| D22 | 项目命名 | **Lunarward**：产品名与显示名 `Lunarward`；二进制 `lunarward`、CLI `lunarward-cli`；仓库 `Lunarward`；AUR 包 `lunarward`（如冲突则 `lunarward-bin`/`lunarward-git`）；配置目录 `~/.config/lunarward`；数据目录 `~/.local/share/lunarward`；URL scheme `lunarward://`；.desktop `lunarward.desktop` | 已确认 | **明确**（m00210 第 1 条） | 2026-11-01 |
| D23 | M0 PoC 环境授权 | 允许创建 `poc/` 目录、安装 Uno 模板与开发依赖、建临时工程；**仍不得改动 `src/` 与上游文件** | 已确认 | **明确**（m00210 第 2 条） | 2026-11-01 |
| D24 | 伴随启动默认值与边界 | 全局默认**关闭**；单个条目默认 `companion=false`（仅手动后启动）；启用「伴随」需二次确认风险提示；**只做编排，不提供任何注入/内存修改实现**；注入类条目在 UI 上标注高风险 | 已确认 | **明确**（m00210 第 3 条：按建议） | 2026-11-01 |
| D25 | 安装器范围 | M1 只做「识别已有安装 + 修复校验」；完整下载/安装/更新放 M2 之后，单独立项 | 已确认 | **明确**（m00210 第 4 条：按建议） | 2026-11-01 |
| D26 | 图标字体策略（**已改判**） | **做替代控件 + 脚本批量替换 `<FontIcon>`**，让 Linux 下呈现实心 Segoe Fluent 观感。实现：①用 fontTools 把上游 `Segoe Fluent Icons.ttf` 的 nameID 1/4/6/16/17 改为唯一族名（`LunarIcons`）随应用分发，规避 Uno 对 `Segoe Fluent Icons`/`Segoe MDL2 Assets` 的家族名拦截；②新增内部控件（`TextBlock` + 自定义字体渲染字形）替代 `FontIcon`；③脚本把上游 82 个 XAML 里的 `<FontIcon Glyph=…/>` 机械替换为新控件，产物与替换规则纳入回归。原「接受 Uno 内置描边图标」结论作废——它与「成品与 Starward 观感一致」直接冲突（图标语言是全局性视觉差异，不属于 D3 允许的软差异）。成本估 1–2 天，可脚本化、可回滚 | 已确认 | **明确**（m00676：都按你推荐的来） | 2026-11-01 |
| D27 | 输出可见性模型 | **三级可见性 × 三作用域**。级别：`silent`（仅写日志文件）/ `panel`（应用内实时日志面板）/ `terminal`（外部终端窗口 `exec` 目标程序）；**捕获永远开启**，级别只决定显示方式。作用域：①游戏本体=`Profile.gameOutput`，默认 `silent`；②后启动条目=`Companion.output`，默认 `follow`（跟随全局默认）；③启动器本体=开发者模式 / `--verbose`，默认 `silent`。全局默认值在「设置 → 运行环境」，`Ctrl+Shift+L` 调出日志面板；面板环形缓冲 5000 行，默认对 `(?i)(token|secret|cookie|auth)` 打码。外部终端窗口在子进程退出后**保留**并标注 `[已退出 code=N]`。游戏进程放独立 session 并忽略 SIGHUP，避免关终端连带杀游戏 | 已确认 | **明确**（m00679：按你的建议来） | 2026-11-01 |
| D28 | 两个 CRUD 列表的位置与模型 | **Profile 列表全局**（设置 → 运行环境），承载跨游戏复用；**后启动程序列表按游戏**（游戏设置页内的分区，不新增主导航项）。调试 shell 双入口：A = Profile 行「打开调试 shell」按钮（不启动游戏，直接在指定 prefix/组件环境开 bash/zsh/fish）；B = 启动器页 `⋯` →「在游戏 profile 中打开 shell」。取消方案 C（`mode=shell` 的条目）——shell 是交互式会话，塞进「启动工具」模型会污染伴随语义。伴随启动**串行**、可配间隔（默认 1s）、列表可拖拽排序；游戏运行中手动启动需过 prefix flock 检查，冲突时只放行不写 prefix 的命令或让用户选「等游戏退出」 | 已确认 | **明确**（m00676：都按你推荐的来） | 2026-11-01 |
| D29 | **登录取值形态：独立 WebKitGTK 登录窗口**（Q-039 结案）。内嵌 `WebView2` 只做展示（公告/BBS），不承担 cookie 取值；登录由独立 WebKitGTK 窗口完成并回传 cookie | 已确认 | **明确**（m01372：按你的建议来） | 2026-11-02 |

---

## 关键决策详述

### D0 · 一致性判据

**背景**：用户要求「移植后和移植前用户体验（包括功能、界面观感、操作逻辑）要确保尽可能完全一致」（m00001），但同时 Linux 是唯一硬目标（m00115）。

**结论**：把「一致」拆成可验收的三层：

| 层 | 内容 | 判据 |
|---|---|---|
| 硬一致 | 页面结构、控件选型与排布、导航层级、信息层级、文案、关键操作路径 | 与上游同页逐项对照，允许像素级差异，不允许结构差异 |
| 软一致 | 窗口装饰、标题栏、模糊/亚克力、托盘与通知形态、系统对话框、字体渲染、快捷键具体键位 | 语义一致即可，允许 Linux 原生化 |
| 允许更好 | 上游没有的能力（运行环境管理、启动计划预览、调试终端、伴随启动） | 新增页面需遵循 Starward 设计语言，不得改写既有页面信息架构 |

**影响**：`07-roadmap.md` M0 门禁据此判定通过与否。

### D1 · 技术路线

**背景**：三条候选路线（Uno 双头移植 / Avalonia 重写外壳 / 先 PoC 再定），见 m00001 第 3 条要求。

**结论**：以 Uno 单头为目标，先做 M0 门禁 PoC。理由：

1. 只有 Uno 能在不重写 82 个 XAML 的前提下把 WinUI 界面搬到 Linux；
2. Avalonia 路线要翻译全部 XAML 并重接 123 个 code-behind 的 UI 胶水，属于「重做/仿造」，直接违反 m00001 第 3 条；
3. Uno 6.6（2026-07-30）已具备 Linux NativeAOT、X11 Skia + Vulkan 后端、WinAppSDK XAML 预处理、WebView2 `CoreWebView2` 类型生成等能力，正好对准本场景。

**风险与回退**：Uno Linux 后端是本项目最大单点风险（见 `06-risks.md` R-01）。M0 不通过 → 停止投入，重新立项评估 Avalonia（届时需重新评估工作量与保真度目标）。

### D2 · 仓库与上游同步

**结论**：fork 后保持「上游文件在场」，通过 csproj 排除 Windows 专属目录，而不是立刻删除。理由：

- 上游仍在活跃发版（0.18.x），保留文件让 cherry-pick 时的冲突面最小；
- 删除容易、恢复难；先排除、后清理（清理动作推迟到 M3 之后，单独决策）。

**约束**：任何位于上游目录内的改动，必须做「最小侵入 + 可回溯」，并在 `04-porting-matrix.md` 登记。

### D22 · 命名约定（补充说明）

> 2026-11-03 更新：用户确认「界面里也别再叫 Starward 了」。已改的用户可见品牌位：关于页标题（`Lunarward`）+ 说明行（`Lunarward · 基于 Starward（MIT）的 Linux 移植`）、主窗口/公告窗口标题、启动日志 `Welcome to Lunarward`、`User-Agent: Lunarward/{version}`、UIGF/抽卡导出 `export_app`、欢迎窗口默认目录、URL 协议提示框标题。**另**：`src/Starward.Language/Lang*.resx` 里 value 段的 `Starward` 已批量改为 `Lunarward`（12 个语言文件，只改 value、不动 `data name=` 键名），因此 UI 文案（欢迎语、URL 协议说明、手柄提示等）不再出现旧名。**未改**（避免破坏数据与文档索引）：程序集名/命名空间 `Starward.*`、SQLite 文件名 `StarwardDatabase.db`、日志文件名 `Starward_yyMMdd.log`——这些属于「内部标识」，改名要配合迁移逻辑，等发布前统一处理。

- **产品名 / 显示名**：`Lunarward`。
- **用户可见二进制**：`lunarward`（GUI）、`lunarward-cli`（CLI/agent）。
- **仓库名**：`Lunarward`；AUR 包名 `lunarward`（冲突时依次 `lunarward-bin`、`lunarward-git`）。
- **XDG**：`~/.config/lunarward/`、`~/.local/share/lunarward/`（图片缓存、日志、启动报告）、`~/.cache/lunarward/`。
- **URL scheme**：`lunarward://`（对应 `lunarward.desktop` 的 `MimeType=x-scheme-handler/lunarward`）。
- **内部项目名**：上游既有项目**保留原名**（`src/Starward`、`Starward.Core`、`Starward.Language`、`Starward.RPC`），以把上游 diff 压到最小；**新增**项目统一 `Lunarward.*`（`Lunarward.Platform.Linux`、`Lunarward.Runtime`、`Lunarward.Cli`、`Lunarward.Codec`）。程序集名与产品名不一致是刻意的，见 D2 的上游同步策略。

### D15 · 真机验收范围

**结论**（用户明确指定，m00119）：

- 必过：**原神**（`~/games/hk4e_cn`，`GenshinImpact.exe` 系）
- 必过：**绝区零**（`~/games/nap_cn`，`ZenlessZoneZero.exe`，含 ACE 反作弊）
- 星穹铁道（`hkrpg_cn`）作为回归参考，不作为门禁。

**理由**：原神代表「最复杂的既有生态 + 反作弊 + 高负载」，绝区零代表「最新 ACE 反作弊 + 高帧率」，两者覆盖了 Linux 上最可能翻车的两端。

---

### D29 · 登录取值走「独立 WebKitGTK 登录窗口」（Q-039 结案）

- **背景**：M0 实测（`poc/GATES.md` G0-3b）——Uno 内置 `WebView2` 在 Linux（WebKitGTK）能渲染、能导航，但 `CoreWebView2.CookieManager`、`Settings`、`Profile` 均为 `NotImplementedException`，`ExecuteScriptAsync` 会让进程直接退出。Starward 登录的命门是「登录后取 cookie 交给 `Starward.Core`」，内嵌控件这条取值路走不通。
- **决策**：登录走**独立 WebKitGTK 窗口**（`poc/login-spike/login.py` 已验证：渲染登录页 → cookie 落 SQLite `moz_cookies` → 导出 5 条交给 Core）；成功取到 cookie 后关闭窗口、回到主界面。内嵌 `WebView2` 仅承担**展示**用途（公告、BBS 页面、云游戏入口），不承担取值。
- **备选（保留）**：① 直调米哈游 passport 扫码/密码 API 换 `ltoken`（不依赖 WebView，但需跟随 API 变更）；② 给 Uno 提 PR 实现 `CookieManager`（排期不可控）。两者都不是 M1 阻塞项。
- **影响**：R-02 降为 B（见 `06-risks.md`）；`04-porting-matrix.md` 中 `Microsoft.Web.WebView2` 由「替换/高风险」改为「部分替换/中风险」；登录窗口的进程内/跨进程形态在 M1 定（倾向子进程，天然隔离 WebKit 崩溃）。

### D30 · 名字兼容层的边界：「补名字可以，补行为不行」

- **背景**：M1 移植中反复遇到 `Vanara.PInvoke`、`H.NotifyIcon`、`CommunityToolkit.WinUI.Controls` 这类 Windows 生态类型。若为它们铺一整套名字兼容层，会把上游最脏的 Windows 耦合永久固化进 Linux 版。
- **决策**：允许写**最小类型集合**的兼容层（`src/Lunarward/Compat/`），条件是：① 上游源码大面积使用、② 替换成本远超收益、③ 只补「名字与常量」，不补「行为」。凡是缺的是一整条管线（如 DXGI 捕获、Win2D 特效），一律**排除编译**并在 `12-shims-and-gaps.md` 登记，不做假实现。
- **判据**：如果替换只需要「让编译器认识这个名字」，可以开兼容层；如果需要「让功能真的跑起来」，就不是兼容层的事。
- **落地**：`Compat/VanaraHotkeyCompat.cs`（仅热键 3 类型）、`Compat/NotifyIcon/TaskbarIconCompat.cs`（占位）、`Controls/ImageExCompat.cs`、`Controls/Win2DCompat.cs`。全部登记在 `12`。

### D31 · 截图与看图：用 portal / 外部工具替代 DXGI 管线

- **背景**：上游截图子系统（`Features/Screenshot/` 12 个文件、8393 行）建立在 `Windows.Graphics.Capture` + Win2D + `Direct3D11CaptureFrame` 之上。Linux 没有等价 API。
- **决策**：
  - 截图**捕获**走 `org.freedesktop.portal.Screenshot`（Wayland 正路）或 `grim`（wlroots）/ `spectacle -b`（KDE），作为外部进程调用；不在进程内碰 PipeWire。
  - 截图**查看/编辑**（`ImageViewWindow2`、`ImageBatchConvertWindow`）M3 用 SkiaSharp 重画，不引入第二套渲染栈。
  - `ScreenshotSetting`（HDR 色彩管理）整页在 M1 排除编译，M3 随捕获管线一起重做。
- **理由**：portal/外部工具由桌面环境维护，稳定性与权限模型都比自绘捕获好；Starward 的截图价值主要在「游戏内热键 + 归档管理」，捕获实现可替换。
- **影响**：`06-risks.md` 新增 R-16；`07-roadmap.md` M3 增截图重做项；`12-shims-and-gaps.md` 登记 12 个文件为 ⛔。

### D32 · 登录助手的 M1 形态：独立进程（python + WebKitGTK），M2 换进程内

- **背景**：D29 定了「独立 WebKitGTK 登录窗取 cookie」，但用 C# 直接调 WebKitGTK 需要 GtkSharp/GirCore，M1 阶段引入成本过高。
- **决策**：M1 用 M0 已验证的 `poc/login-spike/login.py`（PyGObject + WebKitGTK 2.54）作为外部助手进程，命令行约定 `--url <login-url> --output <cookies.json>`；`WebLoginService.LoginAsync()` 负责拉起、等待、读回。助手脚本随包分发（`Assets/login-helper/login.py`）。
- **约束**：① 这是**临时方案**，接口 `WebLoginService` 不变，M2 换成 GirCore 进程内实现；② 助手缺失时 `LoginAsync` 返回 false 并在 UI 明确提示，不静默失败；③ 用户可见文档中说明需要 `python3` + `python-gobject` + `webkit2gtk`。
- **待确认**：是否接受「运行时依赖 python3」作为 M1 过渡（Q-040）。

### D33 · 「编译通过」不等于「完成」——占位必须登记

- **背景**：M1 的目标是让上游 XAML 全部编译，过程中必然出现大量占位实现（空方法、恒 false、返回 null）。若不登记，团队会误判进度。
- **决策**：
  - 所有占位必须登记在 `12-shims-and-gaps.md`，逐项标注状态（✅/🟡/⬜/⛔）与里程碑。
  - 占位在 UI 上的表现必须诚实：不可用时显示「不可用/失败」，**不得静默假成功**。
  - 里程碑判定以「XAML 报错清零 **且** 主流程真机可运行」为准，不以编译通过为准。
- **落地**：`12` 为唯一登记处；`07` 各里程碑的完成判据引用本条。

### D34 · M1 的「编译清零」口径：截图子系统 XAML 整页排除

- **背景**：M1 目标是把上游全部 XAML 编译通过。截图子系统的 4 个页面（`ImageBatchConvertWindow`、`ImageViewWindow2`、`ScreenCaptureInfoWindow`、`ScreenshotPage2`）建立在 Win2D `CanvasSwapChainPanel` 之上，逐成员补桩会产出大量 M3 必然删除的死代码。
- **决策**：这 4 个页面的 XAML 改名为 `*.xaml.disabled`，不参与编译；`ImageViewWindow2` 保留一个最小窗口类型，供工具箱入口编译通过。`ScreenshotPage`/`ScreenshotSetting` 保留（导航需要），但功能停用、界面明示「M3 实现」。
- **理由**：与 D33 一致——占位要有登记、要诚实，不要把「能编译」伪装成「能用」。
- **影响**：`12` 登记 ⛔；`07` M3 增加「截图子系统重写」；真机验收不包含截图功能。

### D35 · 发行版不留「有副作用」的开发入口

- **问题**：M1–M3 期间为了绕开 XWayland 下点不动控件的问题，加了一批启动即生效的开关；其中 `--page installprogress` / `predownload` / `install` 会真的启动下载任务、`--window-action` 会真的最小化/关闭窗口。这些代码一度原样编进了 Release。
- **决策**：
  - `DevSwitches.IsEnabled`：Debug 构建默认 true，Release 构建必须 `LUNARWARD_DEV=1` 才生效；
  - 有副作用的入口（安装/预下载相关、窗口动作）**用 `#if DEBUG` 直接编掉**，Release 二进制里不存在；
  - 只读类开关（`--page`/`--subpage`/`--scroll`）保留，但仍受 `IsEnabled` 约束。
- **配套**：`--version-write` 增加三道校验（目录必须有 `config.ini`、版本号必须 `x.y.z`、写前备份 `config.ini.bak`）。
- **理由**：发行版里的调试后门=用户机器上的未授权写操作/下载，比"截图方便"重要得多。

### D36 · 图标字体：改用可再分发的开源字体

- **背景**：M0 实测 Uno 会拦截 `Segoe Fluent Icons`/`Segoe MDL2 Assets` 家族名并替换为自带描边字体；`FontIcon` 控件忽略 `FontFamily`。要实心图标只能「自定义控件内部用 TextBlock + 唯一字体名」。
- **决策**：**不随包分发 Segoe Fluent Icons**（微软字体，再分发无授权）。改用开源等价字体（Fluent UI System Icons / Material Symbols，MIT / Apache-2.0），逐图标与上游对照并列表记录差异；批量替换 55 处 `<FontIcon>` 为自定义控件。
- **理由**：AUR 分发对许可敏感；观感一致性以「信息层次一致」为准，个别字形差异可接受并记录。
- **落地**：`12-shims-and-gaps.md` 记录字体来源与许可；`docs/linux-port/16-icon-mapping.md` 存 55 行对照表。

### D37 · terminal 级输出降级为 M4

- **背景**：D27 定义了 silent/panel/terminal 三级；`terminal`（外部终端 + 直 exec + PID 反查）一直没有消费者，`RuntimeProfile.GameOutput` 是死字段。
- **决策**：M3 只保证 silent/panel 两级（捕获、日志落盘、面板查看已实现）；`terminal` 明确记为 M4 需求，文档不再宣称三级。
- **理由**：没有真实场景在催；先把「捕获+面板」做扎实，避免半成品字段误导。

### D38 · 发布链路推迟

- **决策**：CI / AUR 发布 / AppImage 产线暂不做，等真机验证（启动、登录、托盘）通过后再上。
- **保留现状**：`packaging/PKGBUILD` 与 `build-appimage.sh` 作为可复现脚本保留，不承诺可用性。

### D39 · 并发与占用：同一游戏只跑一个任务，跨进程看前缀锁

- **问题**：`GameInstallService` 先查状态再启动，两个请求同时到达会各起一套下载（预下载与更新互踩）；「后启动程序」的手动启动完全不看前缀占用，可能在同一 prefix 里起第二个 wine 会话。
- **决策**：
  - 安装/更新/预下载/修复：用 `ConcurrentDictionary.TryAdd(gameId, cts)` 做**原子占位**，占不到就返回正在跑的那个任务（不排队、不重复启动）。
  - 手动启动伴随程序前，先读该条目最终 `WINEPREFIX`，用与启动游戏相同的 `~/…/.lunarward/lock` 判断是否被占用；占用则抛可读错误（`Lunarward_Error_PrefixLocked`）。
- **未做（明确记录）**：跨游戏共享同一 prefix 的排队/串行化，M4 再考虑。

### D40 · 公告窗不做 JS 注入，改为「在浏览器中打开」兜底

- **背景**：上游在 WebView2 里注入 JS（去内嵌页背景、换字体源、接 `miHoYoGameJSSDK` 的关闭/打开浏览器回调），依赖 `ExecuteScriptAsync` 与 `WebMessageReceived`。Uno 6.7 的 WebView2 后端这两项不可用（`ExecuteScriptAsync` 实测会让进程直接退出）。
- **决策**：不做注入，也不做「伪装成上游观感」的半成品；公告窗保留内嵌浏览 + 右下角「在浏览器中打开」按钮，作为明确的出口。正文依赖 game 的 authkey，仍待真机验证。
- **代价**：公告页会带上它自己的白底/字体，观感与上游不一致——这条已记在 `12-shims-and-gaps.md` 的 🟡 里，不藏着。

### D41 · 增量更新走「chunk 级复用」，与差分包互补

- **背景**：7.1.0 → 7.2.0 实测，差分包只覆盖 101 个文件（7.76 GiB）；剩余约 980 个变更文件没有差分条目，整文件下载要 40.87 GB，与官方启动器「大版本只下几 GB」的量级对不上。
- **机制**：Sophon 清单按 chunk 描述文件，chunk id 就是块内容的哈希。更新时读本地旧安装的 chunk 布局（旧版本清单，`getBuild` 带 `tag=本地版本`），新版本文件里：块 id 能在旧目录找到就复用本地字节，找不到才 GET `{chunk_prefix}/{chunk.Id}`（zstd，校验 `compressed_md5` + `uncompressed_md5`）。整文件 MD5 仍逐文件校验，不符即失败。
- **实测**：hk4e_cn 7.1.0 → 7.2.0 预下载，1176+35 个变更文件；索引块 125508，可复用 108469 块 / 117.18 GB，**需下载 20.7 GB**（vs 整文件 40.87 GB）。CLI 抽样 60 文件：下载 845 MB、复用 551 MB、0 失败，60 s（串行时 169 s → 滑动窗口 8 并发）。
- **安全点**：原位更新时被替换过的文件布局会变，所以逐块校验 `uncompressed_md5`，不符就当「没有这个块」回退下载；临时文件写 `.lunarward-tmp`，整文件校验通过后 `File.Move` 原子替换。
- **编排**：更新/预下载先试 chunk 复用（`InstallPipeline.RunChunkReuseAsync`），拿不到旧清单或异常则落回整文件下载；差分包（`ChunkPatch`/CLI）与其互补，最终目标是「有补丁用补丁 → 其余文件 chunk 复用 → 再兜底整文件」。
- **组合编排（`UpdateOrchestrator`）**：补丁 → chunk 复用 → 调用方兜底整文件下载；补丁失败的条目不会被记为已处理，交给复用整文件重写。
- **组合实测（`--update-plan`，只算不下）**：hk4e_cn 7.1.0 → 7.2.0 预下载，变更 1211 文件 / 整文件 44.53 GB；补丁覆盖 101 文件 7.76 GB；其余 1110 文件块下载 16.04 GB + 块复用 18.2 GB；**合计 23.81 GB**（比整文件省 47%）。
- **组合实跑**：`--update-run` 在新鲜克隆上跑了 1169 个复用文件（下载 17.42 GB、复用 19.8 GB、0 失败，每个文件整文件 MD5 校验），限量测试 505.99 MB / 0 失败。
- **断点续传（D41 补充）**：临时文件按块顺序写，「临时文件长度」即「已完成到第几块」。重跑时找到长度正好落在块边界的位置继续，块内半截数据截掉；整文件 MD5 不符则丢弃临时文件从零重来一次（防止崩溃时写坏的半块污染结果）。安装/兜底路径（`InstallPipeline.RunResumableAsync`）全部走这条通道，已下对的文件按「长度 + MD5」跳过。
- **实测**：`--install-chunk-run` 打断后重跑同一条目 → `续传 10.04 MB / 下载 38.6 MB / 0 失败`；`--install-fetch` 264 MB 的 pck 打断（临时文件 265 MB）后重跑 → 2.1 秒补完剩余 10.16 MB、整文件 MD5 通过；已完成的文件第二次运行直接「跳过 1」。
- **已知代价**：兜底路径启动时会按「长度 + MD5」逐个校验已存在的文件，140 GB 的安装全量校验较慢（与官方启动器「校验文件」等价），但只在重跑/中断恢复时发生。
- **UI 联调（D41 补充）**：一次性在阶段开始就把「补丁 + 块下载」的总量交给 UI（`UpdateWork`），进度从 0 单调涨到 100%，不再出现「补丁跑到 100% 再掉回 30%」；`Report(...)` 同时填 `Progress_WriteTotalBytes/Progress_WriteFinishBytes`（上游「更新 + Chunk」进度环读的是写入量，不填就是 0/0 → NaN%）；所有百分比计算加 `total > 0` 兜底。见 L-65。
- **开发期限量（D41 补充，仅 Debug）**：`LUNARWARD_DEV_INSTALL_LIMIT_MB` + `--dev-install-probe/--dev-set-install-path`，CN 用 `LimitReached` 显式标记「额度用完」，此时状态置 `Paused`、速度归零、**不写版本标记**（否则会把半装目录标成新版本，见 L-66）。这些入口都在 `#if DEBUG` 内，发行版不含（D35）。
- **未做**：跨文件跨类别共享索引（目前 `BuildIndex` 已把各类别清单合并建索引，但没有跨 `package` 复用）、补丁阶段的块内续传（`Hi3Helper.Sophon` 的 `DownloadPatchAsync` 不接受 Range，自研传输层风险大于收益，暂缓）；以及「为什么官方统计 8.34 GB、我们算 23.81 GB」的差异还没查清（怀疑官方把 101 个补丁文件之外的语音包也走了服务端差分）。

### D42 · 「修复」按本地版本清单校验，不是顺手更新

- **语义**：修复 = 用 `config.ini` 里的本地版本去取**同一版本**的 Sophon 清单（`tagOverride = 本地版本`），逐个文件比「长度 + MD5」，只重下不对的文件；绝不用最新版本清单，否则点修复等于升级。
- **实现**：`GameInstallService.Linux.RunAsync` 在 `Repair` 分支直接走 `InstallPipeline.RunResumableAsync`（内部就是「校验 + 缺失/不符才下」），跳过补丁编排。
- **不写版本标记**：修复成功不改 `game_version`（原来会写成 branch main 的 tag，实测把 7.1.0 的目录标成 7.2.0，见 L-68）。
- **实测**（`--dev-install-probe hk4e_cn --op-repair`，reflink 克隆）：破坏 `YuanShen_Data/Plugins/HYPass.dll` 中间一个字节 → 修复日志 `verify mismatch: ... ("0588F023..." != "1061184c...")` → 重下 2 个文件 5.9 MB → MD5 恢复为 `1061184c7bc47acf1c4c0e3540522d40`，`game_version` 仍为 `7.1.0`。全量校验 2919 文件 / 140 GB 约 210 s（NVMe，单线程 MD5）。
- **进度**：修复成功也把 `Progress_Percent` 走满 100%，状态 `Finish`。

## D43 · 主窗口 chrome 与背景切换的 Linux 形态

- **背景**：用户 m09515 报 7 个真机问题，其中 #1（窗口拖不动/按钮点不动）、#3（外链没反应）、#5（背景切换器不生效、没有视频背景）都源于「Windows 上的系统行为在 Uno/X11 上不存在」。
- **标题栏**：放弃「扩展内容区 + 自绘标题栏」，改 `ExtendsContentIntoTitleBar = false`，拖动/最小化/关闭交给窗口管理器。理由：`SetDragRectangles` 在 Linux 是空实现，自绘方案只会得到「拖不动 + 点不动」（L-73）。
- **外部链接**：`Launcher.LaunchUriAsync` 在 Linux 静默失败（L-74），统一走 `Helpers/OpenUrl.cs`（`xdg-open` → `gio open`）。
- **背景切换**：Uno 的 `PipsPager`（模板里套 `ItemsRepeater`）不响应点击（L-69），改为代码生成圆点按钮；`AppBackground.Linux` 补注册 `BackgroundChangedMessage`，`UpdateBackgroundAsync(GameBackground? requested)` 支持「用户显式选中的背景」。
- **视频壁纸**：取 `GameBackground.Video.Url`（webm）而不是 `Background.Url`（海报 webp），并尊重 `StopVideo`；`WriteableBitmap` 赋值前先置 `null` 强制重采样（L-71）。「建议背景」API 超时从 1s 放宽到 6s（`LUNARWARD_BG_TIMEOUT_MS` 可覆盖）。
- **代价**：主窗口失去 Windows 那套「无边框沉浸」观感，顶部多一条系统标题栏——这是 D3「硬/软差异」里明确接受的软差异；换来的是窗口可拖动、可最小化、可关闭。

## D50 · 缺口第一批（托盘退路 / 桌面通知 / 语言 / 强调色）（2026-11-06）

**背景**：用户 m13555「按你的建议推进吧」，批准按 C1 托盘退路 → C4 桌面通知 → B7 六语言 → B3 强调色 的顺序补齐缺口清单。

1. **C1 托盘不可用时「关闭 = 隐藏」没有退路**
   - 现象：`CloseWindowOption` 默认 `Hide`，托盘注册失败（无 StatusNotifierHost）时点 X 只会把窗口藏起来，进程留在后台、没有任何入口唤回。
   - 修法：`StatusNotifierTray.StartAsync` 返回 bool 并暴露 `Available`；`App.StartTrayAsync` 失败时启动提示「当前桌面没有系统托盘，关闭窗口会直接退出应用」；`MainWindow.AppWindow_Closing` 在 `Hide` + 托盘不可用时**不拦截关闭**（进程随窗口退出）。
   - 验证：`DBUS_SESSION_BUS_ADDRESS=unix:path=/tmp/nonexistent-bus` 启动 → 日志两条警告；`WM_DELETE_WINDOW` 关窗 → 进程退出。正常会话里同样操作 → 窗口 `IsUnMapped`、进程存活（其实是隐藏）。
2. **C4 桌面通知**：新增 `Helpers/DesktopNotification.cs`（`notify-send` → `gdbus Notify` 兜底，PATH 自行扫描，不经过 shell）。安装/预下载/修复/更新完成与失败、截图保存各发一条。验证：`dbus-monitor` 抓到 `Notify` 调用，参数为「Lunarward / 截图已保存 / 文件名」。
3. **B7 六语言**：`src/Starward.Language` 的 de-DE / es-ES / it-IT / ru-RU / th-TH / vi-VN 原本各缺 86 条 `Lunarward_*` 文案，已全部补齐（含占位符），并补上 es-ES 缺的上游键 `PlayTimeStatsDialog_Total`；现 11 个语言文件与 `Lang.resx` 的 965 键完全一致。
4. **B7 附带修掉的真 bug：语言设置不生效**
   - 现象：`config.json` 写 `Language=de-DE`、重启后界面仍是中文；改用 `LANG=de_DE.UTF-8` 启动才是德文。
   - 根因：`AppConfig.SetLanguage` 只改 `CultureInfo.CurrentUICulture` / `DefaultThreadCurrentUICulture`，而 Uno 的界面线程在它之前就建好了；`Lang` 的属性用的是 `ResourceManager.GetString(name, resourceCulture)`，`resourceCulture` 为 null 时读的是**当前线程**的 culture → 界面线程仍是 zh-CN。
   - 修法：`SetLanguage` 同时给 `Starward.Language.Lang.Culture` 与 `Starward.Core.Localization.CoreLang.Culture` 赋同一个 culture（跨线程生效），并记一条 `UI language: …` 日志。
   - 验证：`config.json` 里 `Language=de-DE` + 系统 `LANG=zh_CN` 启动 → 顶栏与活动/公告/资讯变德文（`Ereignisse / Mitteilungen / Informationen`、`Spiel aktualisieren`）。
5. **B3 强调色（此前完全没实现）**
   - 现象：切换游戏后配色不变，右下按钮恒为 `#CBAD8E`。
   - 根因有两层：①上游 `AccentColorHelper` 的两个文件都被 csproj 排除编译，`AppBackground.Linux.cs` 从未调用 `ChangeAppAccentColor`；②即便写了 `Application.Current.Resources["SystemAccentColor"]` 也不生效——App.xaml 把强调色放在 `ThemeDictionaries`（Light/Dark）里，主题字典查找优先级高于应用级 Resources。
   - 修法：新增 `Helpers/AccentColorHelper.Linux.cs`（SkiaSharp 读图，按「饱和度×明度」加权统计 10° 色相桶取主色，再收敛到 S∈[0.30,0.55]、V∈[0.65,0.90]），同时写入应用级 Resources 与四个主题字典，然后发 `AccentColorChangedMessage` 让主窗口重刷；静态背景按底图取色、视频背景按海报取色。取色跑在线程池，写资源回 UI 线程（必须先取 `DispatcherQueue`，在 `Task.Run` 里取到的是 null）。
   - 验证：`LUNARWARD_DEV_ACCENT=#3B82F6` → 按钮变蓝；无覆盖、真实壁纸时日志 `Accent color applied: #4A78A5`，按钮变成对应的蓝灰调。
   - 未做：Windows 上强调色还染标题栏/边框（DWM），Linux 交给 WM（D47），因此只影响应用内控件。
6. **仍属中文硬编码**：`--preflight` 等 CLI 输出没有走 `Lang`，目前只有中文；已登记为后续 i18n 尾巴。

**代价/未做**：桌面通知依赖 `notify-send` 或会话总线上的 `org.freedesktop.Notifications`，两者都没有时静默跳过；主题字典里 `AccentTextFillColor*Brush` 是引用 `ThemeResource` 的 SolidColorBrush，颜色刷新依赖主窗口的 `RequestedTheme` 翻转，个别页面可能要等下一次重绘。

### D56 按宽度均分 + 特征探测扫描 + 数据目录归一

**问题**：①图库右侧固定空白（1200 DIP 下 4 列只占 960 DIP）；②选择层 6 张 192 DIP 卡片需要 1232 DIP，超出窗口且 Uno 后端没有横向滚动条；③组件扫描按厂商目录硬编码，漏掉 `/opt/spritz-wine-tkg`、`~/.local/share/sleepy-launcher`、`/usr/share/steam/compatibilitytools.d`；④每次启动同步扫盘、扫描根不可配；⑤`~/.local/share` 下同时有 `lunarward` 与 `Lunarward`。

**做法**：
1. 图库：`WrapPanel.ItemWidth/ItemHeight` 按「可用宽度 / 能放几列」反推（列数 = `floor((avail+gap)/(minTile+gap))`，minTile=216、gap=24），条目 `CachedImage` 改为 `Stretch` 填充容器；缩略图 `DecodePixelWidth=400`。
2. 选择层：卡片尺寸从固定 192×108 改成 `GameBizDisplay.CardWidth/CardHeight`（`ObservableObject`），`GameSelector.UpdateGameCardSize()` 在 `ScrollViewer_GameBizs.SizeChanged` 时按「可用宽度 / 卡片数」均分（夹在 140–192 DIP）；撤掉无效的 `HorizontalScrollBarVisibility`。
3. 组件：`ComponentStore.ScanRoots`（`/opt`、`/usr/share/steam`、`/usr/lib`、`~/.local/share`、Steam、Flatpak，可用 `AppConfig["ComponentScanRoots"]` 覆盖）+ `DiscoverRunners()`/`DiscoverGraphicsDirs()` 特征探测（`bin/wine`、`bin/wine64`、`files/bin/wine`、`proton` 脚本、`x64/d3d11.dll`），深度 ≤4、跳过 `drive_c`/`dosdevices`/`node_modules`，访问上限 20000 个目录；新增 `--list-components` 只读诊断。
4. 游戏扫描：`GameInstallScanner.ScanRoots`（可配）+ 24 小时缓存（`GameScanLastUtc`；已知安装仍在则跳过扫盘）+ `MarkDirty()` 强制重扫。
5. 数据目录：`Helpers/DataFolder.Unify()` 以小写 `~/.local/share/lunarward` 为准，把 Uno 的 `Lunarward`（`RemoteControlClient` 的 `Settings/Local.dat`）合并进来后换成符号链接；`WelcomeWindow` 的 DEBUG 默认值改小写。

**实测**：图库 1200 DIP 下由 4×216 变为 4×273（铺满至窗口右缘）；选择层 6 张卡片一排放下、无裁切；`--list-components` 扫出 `/opt/spritz-wine-tkg`、`compatibilitytools.d/proton-cachyos-slr`、sleepy-launcher / anime-game-launcher 的 runners 与 dxvk 目录；`~/.local/share/Lunarward` 已变为指向小写目录的符号链接。

**视频缩略图**：`Features/Background/VideoThumbnailer.cs` 用 libVLC `TakeSnapshot` 抽首帧（480×270、`start-time=1`）落 `bg-thumbs/<hash>.png` 缓存，`BackgroundFileItem.DisplayPath` 对视频切到该 png（L-98）。

**代价/未做**：特征探测在第一次扫描会多几百次 stat（已加 24h 缓存与上限）；图库仍未虚拟化；缩略图缓存目前不会自动清理（与 `ImageThumbnail` 的缓存分开管理）。

### D57 纯缓存与数据分离（XDG_CACHE_HOME）

**问题**：`AppConfig.CacheFolder` 名字叫 Cache，实际是「应用数据目录」（`~/.local/share/lunarward`：日志、数据库、背景下载都在这儿），与 D22 写的「缓存放 `~/.cache/lunarward`」对不上；新加的 `bg-thumbs`、以及上游的 `thumb`（截图缩略图）都属于「删掉也能重新生成」的纯缓存，混在数据目录里既不便于清理，也不便于备份时排除。

**做法**：新增 `AppConfig.CacheDirectory`（`$XDG_CACHE_HOME/lunarward`，默认 `~/.cache/lunarward`，启动时创建）；把 `VideoThumbnailer`（`bg-thumbs/`）与 `ImageThumbnail`（`thumb/`）迁到该目录；`Helpers/Housekeeping.Run` 增加对这两个目录的 14 天清理（纯缓存可安全删除）。`CacheFolder` 保留原名与位置（日志、数据库、`bg/` 用户下载都依赖它），只补注释说明它是「数据」。

**迁移**：旧位置的 `bg-thumbs/`、`thumb/` 直接删除（都是可再生缓存），不做搬迁；下次打开图库/截图页会自动在新的缓存目录里重建。

**实测**：删除旧目录后打开图库，`~/.cache/lunarward/bg-thumbs/` 重新生成 png，`~/.local/share/lunarward/` 下不再出现 `bg-thumbs`。

**代价**：备份工具默认会包含 `~/.local/share`、排除 `~/.cache`，因此缩略图不再随数据一起备份——这正是想要的（可重新生成）。

### D67 · 游戏设置页的行高重算（2026-11-13）

- **问题**：真机滚动到底时，「运行配置」与 HDR 分区重叠（`22` 的 P0-2）。原始假设「右列横向溢出」被像素测量否掉（1200 宽最右文字 x=1149，1000 宽 x=807）。
- **根因**：内容 Grid 行高是 `Auto`；HDR 分区靠 `Visibility`/`HDRNotSupported` 在异步初始化后变可见，Uno 不重算行高 → 后出现的块画到下一行位置。
- **做法**：内容 Grid 命名 `Grid_Content`，`InitializeGameSettingAsync` 末尾 `DispatcherQueue.TryEnqueue(() => Grid_Content.InvalidateMeasure())`。
- **代价**：多一次布局；`Auto` 行 + 异步可见性组合在其他页面同样可能踩（见 L-119）。
- **未做 / 撤回**：底部按钮条改实底（与重叠无关）、`ScrollViewer.Clip`（Uno 不应用，逐像素相同）。

## 变更记录

| 日期 | 变更 | 原因 | 影响文档 |
|---|---|---|---|
| 2026-11-01 | 建立 D0–D20 | 首轮方案讨论（m00001 / m00115 / m00119） | 全部 |
| 2026-11-01 | D15 从「星铁 + 绝区零」改为「原神 + 绝区零」 | 用户明确指定（m00119） | `01`、`07` |
| 2026-11-01 | 目标从「双平台移植」改为「Linux 单头」 | 用户补充 m00115：Windows 可用性不做强要求 | D1/D2/D3/D13/D20、`02`、`04`、`07` |
| 2026-11-01 | 新增 D21 | 只读核查发现 5 个 `Starward.*` 为闭源 NuGet 包，其中 4 个 Windows-only | `04` §3.1、`05`、`06` R-15 |
| 2026-11-01 | **D21 修订**（Codec 改判「可移植」）、D22 命名 Lunarward、D23 PoC 授权、D24 伴随启动、D25 安装器范围 | 用户明确答复（m00210）；并补全 `large-resource` 分支源码后重新评估 | `00` D21、`02`、`04` §3.1/§3.2、`05`、`06` R-15、`07` M0/M3、`08` |
| 2026-11-01 | 新增 D26 图标字体策略；确认 G0-4 暂不真机运行、M0 暂停待用户审阅截图 | M0 PoC 实测（`poc/GATES.md` G0-7）+ 用户答复 | `00` D26、`poc/GATES.md` |
| 2026-11-03 | 新增 D39 并发与占用（安装任务原子占位、伴随程序前缀锁） | 复核「仍存在的问题」 | `12`、`src/Lunarward/Features/GameInstall/GameInstallService.Linux.cs` |
| 2026-11-03 | 新增 D35 发行版开发入口门禁、D36 图标字体选型、D37 terminal 降级、D38 发布链路推迟 | 用户复核「仍然存在的问题」后拍板 | `12`、`04`、`07`、`16` |
| 2026-11-02 | 新增 D34（截图子系统 XAML 整页排除） | M1 清零口径 | `12`、`07` |
| 2026-11-02 | 新增 D30 名字兼容层边界、D31 截图策略、D32 登录助手形态、D33 占位登记制度 | M1 移植实践（`12`/`13` 沉淀）+ 用户要求「提到的都要落实文档」 | `12`、`13`、`04`、`06` R-16、`07`、`08` |
| 2026-11-01 | **D26 改判**（描边图标 → 替代控件 + 脚本替换）、新增 D27 输出可见性、D28 两个列表位置与字段 | 用户答复 m00676 / m00679 | `00` D26/D27/D28、`09`、`02` §接口、`03` §字段 |
| 2026-11-03 | D41 扩展：组合编排（补丁+复用）落地 | `UpdateOrchestrator`、实测 23.81 GB / 0 失败 |
| 2026-11-04 | 新增 D42「修复 = 按本地版本校验」 | 实测修复会把目录标成新版本、且拿最新清单等于顺手升级 | `00` D42、`13` L-68 |
| 2026-11-04 | D41 扩展：断点续传 + UI 联调 + 开发期限量 | `ChunkResume`、`InstallPipelineResult.LimitReached`、`--dev-install-probe`；修 NaN% 进度环与进度倒退（L-65/L-66/L-67） |
| 2026-11-06 | 新增 D50（托盘退路/桌面通知/语言生效/强调色） | 缺口清单第一批，用户 m13555「按你的建议推进吧」 | `00` D50、`13` L-89–L-91、`12` |
| 2026-11-03 | 新增 D41 chunk 级复用（增量更新主力机制） | 实测差分只覆盖 101 文件，需把剩余变更压到官方量级（m08787「要」） | `00` D41、`src/Lunarward/Features/GameInstall/Sophon/ChunkReuse*.cs`、`InstallPipeline.RunChunkReuseAsync` |
| 2026-11-05 | 新增 D44 全量审查后的健壮性加固（原子写、只读预检、空间/目录校验、前缀清理挂 wine 会话、单实例回调回 UI 线程、CLI 失败退出码、日志保留） | 用户 m10479「能修的就都修了吧」；审查清单见 m10478 汇报 | `00` D44、`13` L-75–L-81、`12` |
| 2026-11-04 | 新增 D43 主窗口 chrome 与背景切换的 Linux 形态 | 用户真机反馈 #1/#3/#5，Uno/X11 无对应系统行为 | `00` D43、`13` L-69–L-74、`12` |
| 2026-11-05 | 新增 D45 CI 只验证 Linux 侧（移除上游 Windows 流水线） | 用户问「github ci 报错，为什么」，定位为上游 `build.yml` 构建 `src/Starward` 且上游该提交本身红；用户选方案 A | `.github/workflows/linux.yml`、`00` D45、`13` L-82、`12` |
| 2026-11-05 | 新增 D46「结论必须有论据」（禁止臆测）+ 协作约定复核 | 用户 m11348：「严禁再臆测，你任何的结论都要有论据，这条计入文档规则」；当次触犯项见 `13` L-83 | `00` D46、`13` L-83 |
| 2026-11-05 | D46 补第 6 条「批准范围不得扩大」；回滚 `ffadf28` 越权的窗口显隐改动 | 用户 m11675：「你怎么又没经过我审批方案就擅自执行了」 | `00` D46、提交 `000304e` |
| 2026-11-05 | 新增 D47：窗口显隐用进程内 libxcb 补齐（点 X 隐藏/唤回可用） | 用户报「X 关闭不了窗口」；实测 Uno X11 无 `HideCore`、`Activate()` 只发 `XRaiseWindow` | `00` D47、`13` L-84、`Helpers/X11Window.cs` |


## D44 · 全量审查后的健壮性加固（2026-11-05）

用户 m10479 授权「能修的就都修了吧」后，按 39 条审查清单（P0/P1/P2，文件:行号见 m10478 汇报）逐条处理。这一轮不改功能形态，只把「会悄悄损坏用户数据/静默失败」的地方补上，并统一下面几条原则：

1. **用户数据一律原子写**：`Helpers/JsonFile.WriteAtomic`（临时文件 + fsync + 原子改名 + `.bak`）。配置文件、Profile、ProfileBinding、后启动条目、前缀元数据、图形组件记录全部改用；读坏了的 JSON 改名成 `*.corrupt-时间` 留证据，不再静默忽略。
2. **预检只读**：`--preflight`/启动前检查不再 `EnsurePrefix`（会 wineboot/克隆前缀），也不再往 external 前缀里写 `.lunarward/lock`；前缀 key 统一用「exe 文件名」与真实启动一致。
3. **动手前先验环境**：安装目录必须「空」或「含 config.ini」；下载前用 `DriveHelper.GetDriveAvailableSpace` 检查空间，不足直接给可读错误而不是下到 99% 才 ENOSPC。
4. **前缀生命周期挂 wine 会话**：前缀锁释放、ephemeral 前缀删除、`on_game_exit` 后启动程序都在 `wineserver -w` 结束后执行，不跟 wine loader 进程的 `Exited` 走。
5. **跨线程/跨进程边界显式化**：单实例 socket 回调统一 `DispatcherQueue` 回 UI 线程；playtime 去重按 pid（原来一把永不释放的全局 Mutex 导致第二个游戏时长为 0）。
6. **CLI 失败要有退出码**：所有「未知游戏/拿不到清单/清单不可用/拒绝」等错误分支置 `Environment.ExitCode = 1`，脚本可以 `cmd && next`。
7. **退出码之外的可见性**：ScreenshotSetting 从空桩改为可用（选/开文件夹、测试截图、清缓存）；截图目录读 `AppConfig.ScreenshotFolder`；托盘在进程退出时摘除 D-Bus 注册。
8. **磁盘回收**：`Helpers/Housekeeping` 启动时清理 14 天前的日志/输出/启动报告，安装前清理 3 天前的 `*.lunarward-tmp`；移除两处 60 秒一次的 `GC.Collect()`。

代价/未做：补丁阶段仍无块内续传（库无 API，见 D41 未做项）；`_tasks` 保留期之外的重复 toast 已修，但安装任务列表没有历史视图；`WineRegistry` 的 HDR 写入改为「设置页保存时写入」，启动期不再跑 `wine reg`（保住「游戏是 wine 第一个进程」）。

## D45 · CI 只验证 Linux 侧（2026-11-05）

**背景**：用户问「github ci 报错，为什么」。定位结论——失败的是继承自上游的 `.github/workflows/build.yml`，它在 `windows-2025-vs2026` 上 `dotnet publish src/Starward`，而移植基线提交 `df8ae07 filecache powered by fusioncache` 里 `src/Starward/Helpers/FileCache.cs` 已经 `using ZiggyCreatures.Caching.Fusion`，但 `src/Starward/Starward.csproj` 没有对应 `PackageReference`。上游同提交的 CI（Scighost/Starward `38165058728`）报的是同一批 `error CS0246`，即**上游自己就红**，与移植无关；后续 `WMC0001/WMC9999` 全是连锁反应。

**决策**：本仓不构建 Windows 侧（D1/D2 单头 Linux），也不该为了让它变绿去改 `src/Starward/`（0 改动铁律）。因此：

1. 删除上游 6 个 Windows 工作流（`build.yml`、`publish_preview*.yml`、`publish_res_signpath.yml`、`convert_preview_to_stable.yml`）。发布链路本来就按 D38 推迟，这些文件保留下来只会每次 push 都红。
2. 新增 `.github/workflows/linux.yml`：`ubuntu-latest` + `setup-dotnet`（`global-json-file: src/Lunarward/global.json`）→ `dotnet restore -p:Configuration=Release` → `dotnet build -c Release --no-restore` → `dotnet test tests/Lunarward.Tests`。只验证「能编译 + 纯逻辑单测绿」，不跑游戏、不下载资源、不产出发行物。
3. 所有 dotnet 命令都在子目录（`src/Lunarward`、`tests/Lunarward.Tests`）执行：仓库根 `global.json` 钉的是上游 Windows SDK 10.0.400，本机与 runner 都没有。

**代价**：Windows 侧从此没有任何自动验证（本来也没跑过）；Release 构建依赖「先 `-p:Configuration=Release` 还原」这一约束（Uno.Sdk 按 `Optimize` 决定是否引用 `Uno.WinUI.DevServer`，拿 Debug 的 assets 编 Release 会命中 `UNOB0019`），已在工作流注释与 L-82 记录。

**未做**：不加 `act` 级别的本地回归；不缓存 NuGet（仓库内 `src/Lunarward/NuGet.config` 固定华为云镜像，CI 沿用同一份配置）；不做 AOT/裁剪验证。

## D46 · 结论必须有论据（禁止臆测）（2026-11-05）

**背景**：用户 m11348：「严禁再臆测，你任何的结论都要有论据，这条计入文档规则」。触发原因是我在汇报「左上角三个图标挤在一起」时，把「图标条在 y≈0 可见」当成「Uno 不读取 `Translation`」的证据；实际上用户在固定态（`IsGameBizSelectorPinned=True`），`Pin()` 本来就把 `Translation` 设回 `Vector3.Zero`，这条观察无法推出该结论。我也没有单独验证过 `Translation` 在 Uno Skia 上的支持情况，属于把推断当结论。

**规则**（对本项目的一切分析与回复生效）：

1. **结论必须可复现**：给出代码位置（`文件:行号`）、命令与输出、日志行、截图或像素/数值测量。拿不出证据时只能说「假设」，并写明验证方法；不得用「应该/显然/大概」代替证据。
2. **区分实测与推断**：回复中把「我实测到的」和「我推测的」分开写；推断必须先验证再作为结论使用。
3. **用户描述优先**：用户报告的现象是一手数据。被纠正时先复核自己的前提，而不是换一套说法继续解释。
4. **可视化问题量化**：间距/尺寸/颜色/对齐一律用截图 + 脚本给数字（本项实测见 `13` L-83 的像素方法），不靠肉眼判断。
5. **猜错的要撤回**：明确写出「这条结论作废」，并在 `13` 记录教训，避免同一错误反复出现。
6. **批准范围不得扩大**：用户批准哪几步就只做哪几步。需要额外改动时先停下来说明「还差什么、为什么必须要」，等再次确认；
   不得以「顺手一起修」「反正要改同一个文件」为由自行扩张（本条来自 2026-11-05 的实际教训：只批准了加 Margin 与临时日志，
   却把窗口显隐方案实现、提交并推送了，已按用户要求回滚 `ffadf28` 的窗口部分）。

## D47 · 窗口显隐用进程内 libxcb 补齐（2026-11-05）

**背景**：用户报「右上角的 X 关闭不了窗口，只能任务管理器强制关闭」。按 D46 逐条实测：

1. `Window.Hide()` / `AppWindow.Hide()` 在 Uno 6.7.135 的 X11 后端上都是**空操作**（调用后 `xwininfo` 仍 `Map State: IsViewable`）。
   上游源码 `src/Uno.UI.Runtime.X11/UI/Xaml/Window/X11WindowWrapper.cs` 只实现了 `ShowCore()`，没有 `HideCore`。
2. 关闭链路本身是通的：真正的 `WM_DELETE_WINDOW` 客户端消息能触发 `AppWindow.Closing`（用自编 `XSendEvent` 程序验证）；
   而 `xdotool windowclose` 走 `_NET_CLOSE_WINDOW`，KDE 下窗口被直接销毁、托管层收不到事件——**它不能用来验证关闭逻辑**。
3. 最小化后 `presenter.Restore()` 也回不来（实测 `Map State` 仍 `IsUnMapped`）。原因同样在上游：`NativeWindowWrapperBase.Show(bool)`
   只在首次调用时 map（`if (!WasShown) ShowCore()`），之后只 `Activate()`；而 `X11WindowWrapper.Activate()` 只发 `XRaiseWindow`，
   源码里那段 `_NET_ACTIVE_WINDOW` 恰好被注释掉，KDE 恢复最小化窗口靠的正是这条 EWMH 消息。

**方案对比**（用户否掉了外部命令方案，理由：新增 `xdotool` 运行时依赖、每操作一次子进程、失败只能吞、Wayland 原生会话下全部作废）：

| 方案 | 结论 |
|---|---|
| 外部命令 `xdotool`（曾实现并回滚，`ffadf28`→`000304e`） | 否掉，理由同上 |
| 只做 `presenter.Minimize()` | 隐藏可用，但托盘/热键/再次启动的唤回全部失效，等于半残 |
| **进程内 `libxcb`（采用）** | 无外部进程、无新依赖（`libxcb.so.1` 是 X11 桌面必备） |

**决策**：新增 `src/Lunarward/Helpers/X11Window.cs`，P/Invoke `libxcb`：

1. 自建连接（`xcb_connect`）、从 `xcb_setup_t` 里取 root；`xcb_intern_atom` 拿 `_NET_ACTIVE_WINDOW` / `_NET_CLIENT_LIST` / `_NET_WM_PID`。
2. 找窗口：遍历 root 的 `_NET_CLIENT_LIST`，先按 `_NET_WM_PID == 本进程` 匹配；Uno 的顶层窗口在 KDE 下**没有**这个属性，
   再按 `WM_NAME` / `WM_CLASS` 兜底。**找到后必须缓存窗口 id**——窗口被 unmap 后会被窗口管理器从 `_NET_CLIENT_LIST` 摘掉，
   下次再扫就找不到了（这是实测踩到的第二个坑）。
3. `Hide()` = `xcb_unmap_window`；`Show()` = `xcb_map_window` + 向 root 发 `_NET_ACTIVE_WINDOW`（source=2 pager、timestamp=CurrentTime，
   mask = `SubstructureRedirect|SubstructureNotify`）。
4. 选 xcb 而不是 Xlib：xcb 的错误按连接异步返回，不需要装进程级 `XSetErrorHandler`（Xlib 默认处理器会直接退出进程），
   也避开 L-45 的多线程 `Display` 问题——连接只在窗口显隐这一处用，所有调用加锁串行。
5. 接入点：`WindowEx.Hide()/Show()/ShowFromMinimized()`、托盘菜单「显示主窗口」、全局热键「显示主窗口」；失败时回退 Uno 的原实现并记日志。

**验证**（两条链路都跑过）：正常启动 → 发 `WM_DELETE_WINDOW` → `IsUnMapped` → 再次启动应用 → `IsViewable` 且成为活动窗口；
`--hide` 启动 → `IsUnMapped` → `--show` → `IsViewable`。

**代价**：多一层手写 XCB 互操作（结构体偏移容易错，踩过 `xcb_setup_t` 的 `vendor_len/roots_len/pixmap_formats_len` 偏移与
`xcb_get_property_reply_t` 的 `value_len` 偏移）；Wayland 原生会话下仍走 XWayland。

**未做**：托盘图标在 KDE 上显示为占位（用户指示暂缓）；没有做隐藏后的托盘气泡提示。

## D48 · 托盘菜单、背景视频与静态主题图的三处真实缺陷（2026-11-05）

**背景**：用户报四个问题（m11814）：视频背景「静止」且暂停后缺左上角标题文字、托盘右键没菜单、轮播点击无效、左下角资讯文字溢出。
按 D46 逐条取证后，前三项里有两项被证实是**同一个缺陷**的两个症状。

1. **托盘右键无菜单 + 托盘图标占位（同一根因）**
   - 证据：`gdbus … Properties.GetAll org.kde.StatusNotifierItem` → `org.freedesktop.DBus.Error.UnknownMethod`；
     抓会话总线可见 plasmashell 启动时只调**一次** `Properties.GetAll`，失败后不再访问 `/MenuBar`，
     日志 `Could not find DBusMenu interface, falling back to calling ContextMenu()`。
   - 原因：tmds.dbus 的属性访问靠接口方法命名约定（`GetAsync`→`Get`），我们只实现了 `GetAsync`，没实现 `GetAllAsync`；
     KDE 恰好只用 `GetAll` 一次性取 `Menu`/`IconName`/`IconPixmap` → `m_menu` 为 null（菜单没了）、图标名解析不到（显示占位）。
   - 修法：`IStatusNotifierItem` / `IDBusMenu` 各补 `Task<IDictionary<string, object>> GetAllAsync()`；
     `SniObject` 返回 10 个属性、`TrayMenu` 返回 `Version/TextDirection/Status/IconThemePath`；
     另外把 `SniObject` 的 ContextMenu 回调从空 lambda 改成 `ShowMainWindow`，宿主真拿不到 dbusmenu 时至少能唤回窗口。
   - 验证：`Properties.GetAll` 返回完整字典；总线抓包显示 plasmashell 随后调用了 `com.canonical.dbusmenu GetLayout`（DBusMenuImporter 建起来了）。
2. **视频壁纸不随窗口状态暂停/恢复**
   - 证据：`MainWindow.Hide()` 会发 `MainWindowStateChangedMessage{Hide=true}`，上游 `AppBackground.xaml.cs:591-608` 对视频 `Pause()/Play()`；
     Linux 版从未注册这条消息 → 隐藏/锁屏时视频继续跑（耗电）。
   - 修法：Linux 侧注册消息 → `VideoBackgroundPlayer.Pause()/Resume()`（`MediaPlayer.SetPause`）；
     并且发现「`--show`/托盘唤回」后 `Window.Activated` 不触发（窗口是重新 map 而不是新获得焦点），
     于是 `WindowEx.Show()` 在 map 成功后自行补发一条 `Activate` 消息（与 `MainWindow.OnWindowActivated` 语义一致）。
   - 验证：隐藏→唤回后日志出现 `activate=True`，两次截图差异 5.03%（此前 0.00%）。
3. **静态背景缺左上角标题文字**
   - 证据：主题图（`Theme.Url`）实测 2560×1440、**99.1% 像素透明**，是纯文字层；上游静态分支是底图 + 主题图两次 `DrawImage` 合成
     （`AppBackground.xaml.cs:419-437`），Linux 静态分支只贴底图。
   - 修法：`LoadBackgroundImageAsync` → 6 秒超时下载主题图 → `ComposeBackgroundPng` 用 SkiaSharp 铺底图并整幅叠主题图 → PNG → `BitmapImage`；
     失败退回单张底图（不黑屏）。自定义背景按上游语义不叠主题图。

**代价**：静态背景多一次主题图下载与一次 Skia 合成（毫秒级）；`Properties.GetAll` 与 `GetAsync` 两套实现要一起维护。

**未做**：托盘属性不发 `PropertiesChanged`（KDE 只在建项时取一次）；Wayland 原生会话托盘仍依赖 XWayland/StatusNotifier 宿主。

## D49 · 图标码位、暂停静态图与窗口底座（2026-11-06）

**背景**：用户 m13072 报三个问题——「有些图标不正确（如视频背景的播放/暂停）」「暂停视频背景要等几秒才出静态图」「背景图库窗口会变大到超出屏幕、不能拖动也没有关闭按钮」，并明确要求先给原因与方案。用户 m13110：「按你建议的来」，批准 #1 走「替换码位」、#2 预加载、#3 统一窗口底座。

1. **图标豆腐块（根因与 Windows 假设完全不同）**
   - 取证：`strace -f -e trace=openat` 跑应用，启动全程**没有任何** `Assets/Font/*` 打开；Uno 实际加载的是自带的
     `Uno.Fonts.Fluent/Fonts/uno-fluentui-assets.ttf`（family `Symbols`，1394 字形）。用它做 cmap 比对：项目 104 个图标码位里
     `E62E E733 F012 F08F F413 F4A5` 缺失（`E62E` 正是暂停），另有 11 个是手柄页且显式用 `XboxFluent2`（可正常加载）。
   - 为什么没走上游字体：`ms-appx:///Assets/Font/Segoe Fluent Icons.ttf`（含空格）无论原名还是 `%20` 编码都从未被打开；
     复制成无空格别名 `FluentIcons.ttf` 后仍不加载，而同样机制下 `PhotosIcons.ttf`（无空格、显式 `FontFamily`）能加载
     → 判定「随包字体这条路在 Uno 6.7 上不可靠」，改用 Uno 自带 Symbols 字体。
   - 修法：把 6 个缺失码位替换成 Symbols 内的等价字形——`E62E→E769`（暂停两竖条）、`E917→E823`（时钟）、`E733→F140`（禁止符）、
     `F012→E896`（下载箭头）、`F08F→E768`（描边播放）、`F413→E74E`（保存）、`F4A5→E734`（星形），共 10 个文件。
   - 验证（D46）：用 fontTools 按 110px 渲染候选字形逐个目视确认形状语义，再对照 Symbols 的 cmap 确认全部存在；改后导航栏「抽卡记录」已显示星形而非方框。
2. **暂停视频背景要等几秒**
   - 原因：暂停只是发 `BackgroundChangedMessage`，Linux 侧 `UpdateBackgroundAsync` 先 `StopVideoBackground()` 再走静态分支：
     海报下载（≤3s）+ 主题图下载（≤6s）+ Skia 合成，于是「等几秒才出图」。
   - 修法：视频起播后 `PreloadPausedBackgroundAsync` 在后台预取并合成「暂停后要显示的那张图」，写入隐藏的 `_image` 与 `_lastBackgroundFile`；
     暂停时静态分支命中 `filePath == _lastBackgroundFile` 直接返回 → 立即显示。
3. **窗口底座（图库超屏/不能拖动/无关闭按钮）**
   - 原因：上游 18 个窗口都开 `ExtendsContentIntoTitleBar = true` 靠自绘标题栏，Uno X11 不实现这套（L-73），结果既无 WM 装饰也无自绘按钮；
     同时 `MoveAndResize` 是空操作，resize 后没人重新摆位。
   - 修法：`WindowEx.Show()` 统一 `ExtendsContentIntoTitleBar = false`（交还系统标题栏）；新增 `protected virtual (int,int)? DefaultWindowSizeInDips`，
     map 后 350ms 按 `RasterizationScale` 应用默认尺寸，再夹到「≤90% 屏幕」并居中（`X11Window.SetBounds`）；图库窗口声明 `(1200, 676)` 并删掉自带的第二次 Resize。
   - 验证：图库窗口 `1500x845+420+185`、`_NET_FRAME_EXTENTS = 0,0,35,0`（有系统标题栏）；日志 `clamp: size 1500x845 fits screen 1920x1080`。

**代价**：顶部多一条 35px 系统标题栏（D3 软差异）；图标字形与 Windows 版存在细微差别（D36 的开源字体方案仍是正式解）。

**未做**：其余窗口的默认尺寸尚未逐个声明（目前只有图库）；`_NET_CLIENT_LIST` 里找不到窗口时 `X11Window.Show` 会退回 `AppWindow.Show()`（首帧创建阶段）。

## D51 · 非 M4 收尾：缺口逐条定论（2026-11-06）

用户要求「M4 之前的全部做干净」。M4 占位功能（悬浮窗、手柄、RPC、云游戏/战绩/BBS 桥）按指示**不做**；其余每一项都必须有结论：已实现 / 已论证不适用 / 待用户真机验证。本决议是逐条结论表，后续新增缺口先加一行再动手。

| 缺口 | 结论 | 证据 / 理由 | 后续 |
| --- | --- | --- | --- |
| 官方补丁 8.34 GB vs 我们 23.81 GB | 语义不同，不是算错 | `UpdateOrchestrator.BuildWorkAsync` 的 1211 个「变更文件」是 **7.1.0 清单 vs 7.2.0 清单**的 MD5 差异；官方 `getPatchBuild` 的 8.34 GB 只是 101 个补丁文件的压缩体积，其余 1110 个变更文件官方同样要下。我们的 23.81 GB = 补丁 7.76 GB + 剩余文件块下载 16.04 GB（复用 18.2 GB 已扣除） | 不额外改显示；界面已按「补丁增量」与「预计总量」分开显示 |
| CLI 输出中文硬编码（`--preflight` 等 142 处） | 接受，不参与 i18n | CLI 是诊断/脚本接口，输出稳定比跟随界面语言更重要；跟随语言会让脚本解析随设置漂移 | 需要时再加 `--json`（结构化输出优先于翻译） |
| 托盘属性不发 `PropertiesChanged` | 不需要 | KDE 在注册时就调 `Properties.GetAll` 一次取全量（D48 已验证）；我们的图标/菜单/状态在运行期不变，没有变化可通知 | 若将来加动态图标（如下载进度角标）再补信号 |
| HDR 预览 / 截图色彩管理 | 不适用 | Linux 侧无 HDR 合成通路（X11 + Skia 8-bit）；但**游戏内 HDR 开关已支持**——`GameSettingService.SetGenshinEnableHDR` 按 `GamePrefixResolver` 定位前缀后写 `WINDOWS_HDR_ON_h3132281285` 注册表项 | 无 |
| AVIF / JXL 解码 | 受限，登记 | 上游靠 Windows WIC；SkiaSharp 3 的编解码器里没有 AVIF/JXL。影响：把 avif/jxl 当自定义背景或截图查看器打开会失败 | 需要时接 libavif/libjxl 或先转码 |
| 自更新只查 AUR | 按 D5 实现 | `Features/Update/UpdateService.Linux.cs:25` 查 `https://aur.archlinux.org/rpc/v5/info`，只报告不自我替换；网络失败当「无更新」（D33） | 随 AUR 包发布即可 |
| 登录依赖 python3 + PyGObject + WebKitGTK | 已实装前置检查 | 新增 `WebLoginService.CheckHelperDependenciesAsync`：先跑 `python3 -c "import gi; gi.require_version('Gtk','3.0'); … WebKit2 '4.1'"`，失败时 `InAppToast` 给可操作提示（Arch: `python-gobject webkit2gtk-4.1`），不再静默失败 | M2 换 GirCore 后可去掉 python 依赖 |
| 热键仅 X11 | 已说明并给替代 | `X11KeyState` 25ms 轮询；Wayland 会话下 `X11KeyState.Available == false` 时启动提示「把 `lunarward --show` 绑到系统快捷键」（`ShowHotkeyUnavailableHintOnce`） | 无（纯 Wayland 无全局热键标准） |
| 纯 Wayland 原生会话 | 不支持，已给明确错误 | Uno 的 Linux 后端是 X11/XWayland；`Program.Linux.cs` 在无 `DISPLAY` 时打印「没有 DISPLAY / WAYLAND_DISPLAY，无法创建窗口…」并 `exit 3` | 等 Uno 支持后重评 |
| `GameNoticeWindow` 是否死代码 | 保留（仅 DEBUG 入口） | `MainView.xaml.cs:198` 的 `--page notice` 调试入口可达；正常路径走浏览器（D40）。保留是为了将来换 WebKitGTK 时直接复用 XAML | M4 之后再决定删除或启用 |
| CI 不出安装包 | 按 D38 推迟 | `.github/workflows/linux.yml` 只构建 + 测试；发布链路（AppImage/AUR 自动发布）单独立项 | D38 |
| 上游同步流程未定（Q-003） | 已定（D93） | `upstream` 远程指向 Scighost/Starward，`upstream-base` 记录移植基线；2026-11-21 起 `main` 与上游历史已连通（合并提交 `542458e`，merge base = `df8ae07`），`git fetch upstream && git merge upstream/main` 是普通三方合并（上游改动只在 `src/Starward/`，冲突也只会落在这里；`src/Lunarward/` 不受影响），随后按 `12-shims-and-gaps.md` 复核 55 条排除与 7 处 XAML 差异 | 每次上游发版时执行 |
| 无 CHANGELOG | 已补 | 仓库根新增 `CHANGELOG.md`，按日期记录 M1–M3 的关键变更 | 随提交维护 |
| D36 图标字体（开源字体方案） | 结项为「码位替换」 | D49 把缺字形的码位换成 Uno 自带 Symbols 字体里存在的等价字形，`strace` 证明随包 Segoe 字体在本后端不被加载（L-88）；不再需要打包字体 | 若将来要视觉完全一致，再评估打包 Fluent System Icons |
| 真机启动 / 真实登录 / 游玩时长 / 真实目录更新 / 从零安装 | 待用户验证 | 需要用户账号与真机操作（m05423/m05843 明确推迟） | 用户择期，我按结果修 |

## D52 · 启动链路三处缺陷：尾斜杠前缀、游玩时长盯错前缀、wineserver 竞态（2026-11-06）

用户真机启动绝区零「完全启动不了」。三处独立缺陷叠加，全部已修并实测：

| # | 缺陷 | 证据 | 修法 |
| --- | --- | --- | --- |
| 1 | `WINEPREFIX` 带尾斜杠 → spritz-wine 打印 `wine: launching from steam.exe stub!` 并立即退出（无窗口） | 同一命令只差尾斜杠：带斜杠 1 秒内退出，不带斜杠正常起窗口（L-92） | `PrefixManager.ExpandPath` 统一去尾分隔符；profile 存盘规范化；`WineRegistry`/`ResolvePrefix`/后启动程序都走它 |
| 2 | 游玩时长子进程回落到 `~/.wine`，且 `wineserver` 用错版本 → 会话秒结束、前缀锁提前释放 | 日志 `Start 19:04:48 → End 19:05:29`，而游戏窗口仍在（L-93） | 记录子进程经环境变量继承 `LUNARWARD_WINEPREFIX` / `LUNARWARD_WINE` / `LUNARWARD_WINESERVER`；`ResolveWineserver` 优先显式值 |
| 3 | `wineserver -w` 在 wine 进程还没挂到前缀时瞬间返回 | 运行器先起 `steam.exe` 存根、游戏是孙进程 | 先等 `/proc/*/environ` 出现该前缀的 wine 进程（≤30s，启动进程已退出再宽限 3s），再阻塞等待 |

**验证**：经 `lunarward://startgame?biz=nap_cn` 启动 → 出现「绝区零」窗口；游玩时长从 19:06:47 连续记录到
19:07:59（游戏被我手动结束后才 End）；前缀锁文件在游戏期间保持存在。另新增预检项「前缀结构」：
目录缺 `drive_c`/`system.reg` 时提示「选的不是真正的前缀目录」（external 模式很容易指到前缀根目录）。

**代价**：external 前缀若与其他启动器共用，`wineserver -w` 仍会等其他 wine 进程退出（已知限制，沿用 D7）。

## D53 · 根解决方案改为 Linux-only（2026-11-05 起，2026-11-10 落地）

**问题**：仓库根原来只有上游的 `Starward.slnx`，里面含 `src/Starward/`（WinUI，`net10.0-windows`）与
`src/Starward.Launcher/*.vcxproj`（C++/Win32）。在 Linux 上打开或 `dotnet build` 它会直接失败：

```
error NETSDK1100: 若要在此操作系统上生成面向 Windows 的项目，请将 EnableWindowsTargeting 属性设置为 true
warning NU1503: 正在跳过项目 Starward.Launcher.vcxproj 的还原操作
```

**做法**：把根 `Starward.slnx` 的内容替换为 Linux 侧四个项目（`Starward.Core`、`Starward.Language`、
`Lunarward`、`Lunarward.Tests`）+ 文档文件夹。文件名保持不变，这样 IDE 自动发现与 `dotnet build` 都不用额外参数；
上游 Windows 解决方案的内容留在上游仓库与本仓 git 历史里。

**配套**：根 `global.json` 补 `"msbuild-sdks": { "Uno.Sdk": "6.7.30" }`。MSBuild 解析 `Uno.Sdk` 只读**调用目录**
向上找到的 `global.json`，之前只有 `src/Lunarward/global.json` 里有钉版本，从仓库根构建就报
`MSB4236 找不到指定的 SDK "Uno.Sdk"`。SDK 版本同时从 `10.0.400/latestMajor` 放宽成 `10.0.300/latestMinor`，
让 10.0.302 与 10.0.403 都能用。

**代价**：上游若改动 `Starward.slnx`，同步时会在这一处冲突；保留本仓版本即可（上游的 Windows 解决方案对本项目无意义）。
CI 同步改为从根构建解决方案，少一层子目录 cd。

## D54 · 背景壁纸不再压暗（2026-11-10）

**问题**：用户反馈「背景有点暗」。根因是 `AppBackground.Linux.cs` 用 `_image.Opacity = 0.42`、
视频画布 `Opacity = 0.6` 近似 Windows 的「亚克力模糊 + 压暗」观感——Linux 侧没有模糊，
只剩压暗，壁纸整体发灰。

**做法**：两者都改回不透明（`1.0`），壁纸原样显示；文字可读性交给上层面板自身的半透明背景
与上游那三层四角/右边渐变。实测背景区亮度 luma 从压暗态升到 **154.7**（截图
`screenshots/m3-launcher-no-dim.png`），主页文字与按钮仍清晰。

**代价**：遇到极亮的过场 CG 时，左上角游戏名与版本号的对比度会比 Windows 侧略低（Windows 有模糊兜底）。
若后续觉得个别壁纸糊，再加「壁纸压暗强度」设置项（0–100%），而不是回到写死的 0.42。

## D55 · 图库重排、缩略图高质量降采样、游戏选择层横向滚动（2026-11-10）

**图库右侧空白**：窗口先按 1200 DIP 创建、随后被 `WindowEx.ClampToScreenAsync` 撑到 1500 物理（1200 DIP→1500px），
`ListView` 的 `tk:WrapPanel` 只在首次测量时算过列数，放大后不重新 reflow → 右侧留约 296 DIP 空白。
修法：`BackgroundViewPage` 订阅 `SizeChanged`，对 `GridView_Images.ItemsPanelRoot` 调 `InvalidateMeasure()`。
实测列数从 4 列（末列结束 x≈1282）变为 5 列（末列结束 x≈1500），空白消失。

**缩略图降采样**：`bg/` 里 16 张全是 2560×1440，条目只有 216×122 DIP，`CachedImage` 之前把整张图解进内存
（≈236 MB 位图 + UI 线程 `SetSourceAsync`），打开图库就是一次可感知的卡顿。新增 `DecodePixelWidth`
依赖属性：走 SkiaSharp 解码 + `SKFilterQuality.High`（三次卷积 + mipmap）缩到目标宽度再交给 `BitmapImage`。
图库条目设 `DecodePixelWidth="272"`（216 DIP × 1.25 缩放）。**不降帧、不牺牲清晰度**：3 倍放大目视无锯齿/糊；
缩 10 倍以上时三次卷积比线性采样的摩尔纹明显更少。

**游戏选择层横向滚动**：卡片 192 DIP + 间距 16，6 个游戏 = 1232 DIP > 可用宽度，默认 `ScrollViewer`
横向禁用 → 最后一张被裁且滚不到。改为 `HorizontalScrollMode/BarVisibility=Enabled/Auto`，纵向禁用（页面本身够高）。
代价：出现横向滚动条（用户选择 A 方案，不缩小卡片）。

**未做**：图库虚拟化（`ItemsRepeater + UniformGridLayout`）——先靠降采样把解码量降两个数量级，实测已无明显卡顿；
视频降帧被用户否掉（保持 30fps），如需省电可后续加「视频转 H.264 缓存」开关。

## D60 页面切换卡顿：缓存页面 + 注册表读取挪出 UI 线程

**现象**：在「启动器 ↔ 游戏设置」之间切换，界面冻结 1–2 秒。

**取证**（临时 `[nav-diag]` 日志，实测后已删）

- 切到游戏设置：`Navigate` → `Frame.Navigated` 共 **1729 ms**，其中页面 ctor 只有 132 ms → 大头是布局/首次加载，而 `OnLoaded` 里的 `GameSettingService.GetGameResolutionSetting` / `GetStarRailFPSIndex` / `GetGameVoiceLanguageSetting` 都同步调 `WineRegistry`，每次 spawn 一个 `wine reg query`（首次前缀初始化时更久）。
- 切回启动器：ctor **425 ms**（XAML 重新膨胀），因为 Uno 的 `Frame` 默认每次 `Navigate` 都重建页面（全项目没有任何 `NavigationCacheMode`）。

**做法**

1. 给 `GameLauncherPage`、`GameSettingPage`、`ScreenshotPage`、`GachaLogPage`、`GenshinBeyondGachaPage`、`SelfQueryPage`、`GameRecordPage`、`SettingPage` 加 `NavigationCacheMode="Required"`。
2. `GameSettingPage.InitializeGameSettingAsync` 里三处注册表读取合并成一次 `await Task.Run(...)`（返回 `GameSettingSnapshot` record），回到 UI 线程只赋属性。

**效果**（同一台机器，`LUNARWARD_NAV_LOOP` 连续切换 4 次实测）

| 场景 | 改前 | 改后 |
|---|---|---|
| 首次进游戏设置 | 1729 ms | 216 ms |
| 再进游戏设置 | 1729 ms | **33 ms** |
| 回到启动器 | 425 ms+ | **24–46 ms** |

**代价**

- 缓存页面后 `OnLoaded/OnUnloaded` 仍每次触发，但**构造只做一次**；因此页面必须在 `OnLoaded` 里刷新游戏相关数据（现有代码已是这个结构）。
- 注册表读取改为后台线程后，页面先显示默认值再填真实值（几十毫秒内完成，用户不可见）。

**未做**：`InitializeResolutionItem` 仍会抛异常（Uno 无 `ContentIslandEnvironment`），靠 try/catch 顶上；页面 XAML 本身的体积没有精简（首次进页面仍有 ~200 ms 布局成本）。




> 追加（页面缓存暴露出的两处副作用）：
> 1. `GameBannerAndPost` 在 `Unloaded` 里清空了 `Banners/PostGroups`，页面被缓存后控件复用，Uno 的 `Pivot` 不会因为 `ItemsSource` 清空→重设而重建条目，左下角公告区退化成空卡片（真机复现）。现在 `Unloaded` 只解注册与停计时器，数据按 `_contentGameId` 判断是否重新拉取（L-106）；banner 图加 `DecodePixelWidth=760` 降低首次解码成本。
> 2. 每次进启动器页都会重新请求「建议背景」，Linux 上常 5–6 秒超时并回退 + 重算强调色（触发整树主题重解析）。现在成功/失败都按游戏缓存 5 分钟，强调色按文件路径去重（L-107）。
> 效果：切「启动器 ↔ 游戏设置」双向都在 150 ms 内完成（真手点击 + 连续截图确认，dsh-cu 注入）。

## D59 可观测性与打包跟随代码

**背景**：用户要求「预检只报告不给动作」「出问题要十条追问」「跑的是旧二进制」。三件事都在「用户能不能自己解决」这条线上。

**做法**

1. **预检项可点修复**：`PreflightItem` 增加 `Action`（打开游戏设置 / 打开运行环境 / 打开前缀目录 / 重建前缀 / 安装图形组件 / 查看占用进程），失败项在对话框里直接给按钮。`重建前缀` 会先确认，`external` 模式拒绝删除（那是别人的目录）。
2. **诊断包**：`lunarward --diagnose --out DIR [--game BIZ]` 生成 zip（环境、运行器清单与可用性、方案与绑定、上次可用、每个方案的前缀状态、最近启动报告、日志尾部、预检结果）。敏感环境变量按名字打码。不含存档、账号、cookie。
3. **窗口图标**：Uno 的 X11 后端不设 `_NET_WM_ICON`，任务栏只有占位方块。`Helpers/AppIcon` 读 `Assets/lunarward.png`、Skia 缩到 64×64、通过 libxcb `xcb_change_property` 写入；首次激活 300ms 后应用（此时窗口才存在）。
4. **前缀权限**：新建/克隆的前缀 `chmod 0700`（默认 0755 时同机其他用户可读前缀里的 token 与注册表）。
5. **打包跟随代码**：`packaging/aur/lunarward-git`（HEAD 出包）、`packaging/aur/lunarward`（tag 出包）、`.github/workflows/appimage.yml`（tag 或手动触发，产出 AppImage 附件）。
6. **单测**：抽出 `PrefixPath`（路径展开与规范化）与 `JsonFile`（原子写）进入测试工程，24 → 36 个用例。

**代价**

- 预检按钮要能触达设置页：新增 `SettingNavigation` 一次性目标 + `MainWindow.OpenRuntimeSettings/OpenGameSettings`，属于「补名字不补行为」之外的新导航入口。
- 诊断包会包含路径与版本信息，分享前应确认（README/用户文档已写明）。
- `_NET_WM_ICON` 每次只写 64×64 一档，高 DPI 托盘仍可能略糊（KDE 托盘另有 `IconPixmap` 通道，已单独处理）。

**未做**：崩溃自动上报（要服务器）、诊断包自动脱敏路径（只做了环境变量）。



## D58 · 运行组件模块（v2 极简，2026-11-11 定稿实施）

**背景**：v1 设计（依赖解析 + 锁文件 + 内容寻址仓 + 可达性 GC + 组件市场）按「频繁试组合」优化，与真实使用方式不符——用户明确说明组合一旦确定就长期不动，变动来源是 pacman/AUR 升级；用户两次追问后判定「过于复杂、绕晕」，要求从头重述。

**决定（v2，用户 m15086 批准「先按这个来吧，先做出来体验一下」）**：见 `16-runtime-components.md`。

- **只回答三问**：有哪些能用的运行器 / 这个游戏用的是哪个 / 出问题怎么换回来。凡这三问用不上的不做。
- **管清单与选择，不管安装**：wine 的安装归 pacman/AUR；启动器负责发现、说明可用性、让用户选、并能换回来。
- **数据三层**：运行器（扫描得出，含 `Layout`/`Usable`/`Reason`）→ 方案 Profile（引用运行器 + 前缀 + env）→ 前缀（属于方案）。
- **扫描阶段零副作用**：只做「文件存在 / 有执行位 / `wine --version` 有输出 / 有同套 wineserver」四项判定；不建前缀（`wine cmd /c exit` 会顺手建前缀，故不做）。
- **回退机制**：每次启动成功写 `~/.config/lunarward/last-good.json`（原子写），方案编辑器显示「上次可用」并可一键切回；换运行器时界面明示会触发一次 `wineboot -u`。
- **界面三处**：运行环境→组件、运行环境→方案、游戏设置→运行。
- **不做**：锁文件、漂移检测、哈希校验、组件市场、多 provider、内容寻址仓、可达性 GC（v1 全撤回）。
- **CLI**：`--list-runners [--json]`。

**代价**：不记录组件内容哈希，无法证明「两次启动用的是同一份二进制」；外部组件被 pacman 升级后若出现异常，只能靠「上次可用」回退，没有更强的复现手段——按用户口径（漂移＝预期行为）接受。

**未做（后续）**：`WINEDLLPATH` 叠加（DXVK 不入前缀）、游戏设置页的运行器/预检展示、「固定版本」动作（把系统 wine 复制进 store）、前缀健康检查。

---

<details>
<summary>v1 设计稿（已撤回，保留存档）</summary>

## D58（v1）· 运行组件与运行环境：目标形态（设计稿 v1，2026-11-11）

**背景**：现有 `RuntimeProfile` 把 runner / graphics / prefix / env 四件事合成一个对象，直接存最终值；
`ComponentStore` 只扫路径不给身份；`GraphicsInstaller` 靠往前缀里复制 dll；没有版本锁定、没有引用计数，
也没有下载来源。讨论中用户提出三条修正，直接改变了设计：

1. **反对「复制一份固定」**：能复用就不要重复存，且**不能假定用户是 btrfs**（无 reflink）。
2. **下载市场要做**：否则用户得自己找包、解压、挑架构；且应支持多个自定义来源。
3. **反对用 LRU 做回收**：「最近不用」不等于「没用」，「最近在用」不等于「还在用」，只有引用计数才代表真实性。

**决定（目标形态，见 `16-runtime-components.md`）**：

- **范围（v1.1 校准）**：**只管自己造的**。系统包管理器 / Steam 管的组件由它们管，启动器不接管、不校验、不保留、不迁移其版本；**外部漂移＝预期行为**，只展示不拦截。受管组件（启动器下载/复制/手工放入 store 的）才有强身份与全套能力。
- **身份分强弱**：受管 = `(kind, id, version, sha256, source, capabilities)`（哈希只算关键文件，惰性 L0→L1→L2）；外部 = `(path, 读到的版本字符串)`，够用就行。
- **发现层与仓层分离**：扫描结果只是「可用的候选」，不进仓、不算哈希；**不提供「把外部组件固定下来」**。想长期稳定就用启动器自己下载一份受管组件。
- **存储**：受管组件内容寻址存一份、多方案共享（删到最后一个才释放，pnpm 同构）；`hardlink`（`cp -al`，同分区零空间，不依赖 CoW）与 `copy`（`cp -a --reflink=auto`）只是**创建受管条目**的两种物理方式，不是「版本管理」。
- **方案 = 声明 + 锁**：`profile.json`（可跟随的意图）+ `profile.lock.json`（钉死到哈希的事实 + `explain` 解释 + `verified_at`）。解析规则：显式 > 继承 > 默认；能力校验；冲突检测。
- **前缀可重建**：`base` + `manifest(operations)` + 生命周期 `create/adopt/upgrade/snapshot/restore/clone/verify/discard`；快照三档降级 btrfs 子卷 → 硬链接树 → `tar.zst`（**都不假定 CoW**）。
- **注入默认 overlay**：`WINEDLLPATH` + `WINEDLLOVERRIDES`，换 DXVK 不动前缀；`inprefix` 显式选择并记 manifest。
- **预检 = 锁 vs 现实**：`missing` / `drift` / `version-mismatch` / `modified` / `locked` / `no-space` 六类结果统一呈现并写 `LaunchReport`。
- **回收 = 可达性 GC（pnpm 式标记-清扫，不是计数器）**：根四类——方案锁、前缀 manifest、运行租约（lease 文件带 pid）、显式 `keep`（= Nix 的 gcroot）；**每次 GC 从根遍历，引用关系不落盘**（避免计数器漂移）；**可达或带 lease/keep → 永不自动删除**，删除只由 `--gc` 显式触发并需确认（默认不自动清理，学 `pnpm store prune`）；候选排序按「引用数 + 占用空间」，**不按时间**；LRU 仅用于纯缓存（缩略图/下载临时文件/合成壁纸）。补 `--verify-store`（对应 `pnpm store status`）与 `broken` 状态；空间报告按 `st_dev+st_ino` 去重、`st_nlink` 判断共享，区分「名义 / 实际 / 可回收」。
- **`missing` 是一等状态**（不是异常）：启动前展示「被哪些方案引用 / 上次可用时间 / 配方重取 / 换用系统版本 / 解除引用」。
- **市场 = provider + 标准索引**：`providers/*.json`（official + 多个自定义：https / 本地目录 / `file://`）；索引条目强制 `sha256`，多来源去重，`http` 警告，记录 provenance，支持离线镜像。

**明确不做**：沙箱/容器化运行时、游戏版本管理、Steam 库管理、「自动修好一切」的魔法。

**实施状态**：**未实施**。分期 P0（区分受管/外部 + 身份）→ P1（锁 + 解析 + 受管校验）→ P2（受管存储 + `missing`/`broken`）→ P3（前缀 manifest 与生命周期）→ P4（overlay + `--print-env`）→ P5（provider/下载）→ P6（UI/空间报告/`--gc`）。P0+P1 是分水岭。

**范围校准（2026-11-11，用户原话）**：「由系统包管理器或者 Steam 等管理的组件，就应该由对应的部分去管理，我们的启动器就不要再去额外做这部分的工作了……至于外部漂移，可以认为预期行为。」→ 据此**砍掉**：外部组件的哈希/校验/漂移告警/旧版本保留/配方/GC 参与、`missing` 状态（对外部降级为预检一句「找不到，请另选一个」）；**保留**：外部组件的发现、版本展示、启动时存在性与「是否满足方案最低版本要求」。相应地把 v1 的「三档保留策略」收紧为「受管/外部两类来源」。

**代价**：P1 起启动路径多一次解析（毫秒级，仅受管组件校验）；受管组件被外部清理工具删掉时会出现 `missing`，需要 UI 正面呈现；硬链接快照若被原地改写会污染（只用于回滚，回滚前 `verify`）。外部组件不做校验 → 系统升级导致的行为变化**不会被告知**，这是用户明确接受的取舍。

**待拍板**：Q1 受管组件默认存放位置（建议数据目录，可改到其它盘）、Q2 游戏页是否允许临时覆盖（建议允许并标红）、Q3 `overlay` 默认（建议是）、Q4 provider 配置位置（建议独立 `providers/`）、Q5 `--print-env` 是否脱敏（建议默认脱敏）。见 `16-runtime-components.md` §14。

**措辞校准（2026-11-11，用户指出）**：用户原话是「类似 pnpm 那种感觉」，不是想表达「维护一个 refcount 计数器」。据此把本节与 16 号文档 §8 的口径统一为 **可达性 GC（mark-and-sweep）**：引用关系是**图上的边**，由 GC 时从根遍历得出，而不是写入时加减的计数器；`refcount` 一词只留在 UI 展示（「被 3 个方案引用」）。同时补三处与 pnpm 对齐的细节：完整性校验 `--verify-store`（↔ `pnpm store status`）、删除必须显式触发（↔ `pnpm store prune`）、共享语义「删到最后一个 inode 才释放空间」。

---

</details>

### D62 缓存页面后的跨页/跨游戏刷新，与「切回启动器卡几秒」

**现象**：①从其它页切回「启动器」要卡几秒；②切换游戏后，左下角公告/资讯仍是上一个游戏的内容。

**根因（三处，全部实测）**
1. 页面加了 `NavigationCacheMode`（D60）后不再重建，切换游戏只改了 `MainView.CurrentGameId`，`GameBannerAndPost.CurrentGameId`（导航参数赋的值）保持旧值，`Loaded` 里的 `_contentGameId != CurrentGameId` 永远为假 → 不刷新。
2. 即使重新取到了内容，`Pivot.ItemsSource` 换成新列表也不重建 tab 条目（Uno 复用条目容器）→ banner 变了、三个 tab 里还是旧活动的文案。
3. 背景层 `UpdateBackgroundCoreAsync` 每次进来先无条件 `StopVideoBackground()`，之后取「建议背景」（接口在本机必超时）→ 下载 → Skia 合成，累计 8–10 秒；这段虽在后台，但会挡住首帧画面。

**修法**
- `Features/ViewHost/CurrentGameChangedMessage.cs` + `MainView.GameSelector_CurrentGameChanged` 广播；`GameLauncherPage` 收到后更新 `CurrentGameId`/`CurrentGameBiz` 并重跑 `InitializeGameFeature`/`CheckGameVersion`/`UpdateGameInstallTask`/`InitializeGameServerAsync`；`GameBannerAndPost.CurrentGameId` 改成带 setter 的属性，值变化且 `IsLoaded` 时重载内容。
- `UpdateGameContentAsync` 里先把 `Pivot_Posts.ItemsSource = null`，赋新列表后等一次布局周期再挂回去（`await Task.Delay(60)`），强制重建 tab 内容。
- 背景层：加 `_appliedGameId` + `_videoActive` 守卫，同游戏且已有画面（静态图或视频在播）时直接返回，不再停视频重下；进来先用 `BackgroundService.GetCachedBackgroundFile` 或兜底图把画面填上（实测 2 ms），网络那段慢也看不见空白；「建议背景」超时 6 s → 1.5 s（可用 `LUNARWARD_BG_TIMEOUT_MS` 覆盖）。
- 开发入口（`#if DEBUG`，D35）：`LUNARWARD_DEV_SWITCH_GAME=<biz>` 启动后 6 秒用 `GameSelector.DevSelectGame` 走真实选服路径切游戏，便于无人值守复现。

**实测**
- `quick background 2 ms`、`launcher OnLoaded sync 25–34 ms`、`nav GameLauncherPage sync 266–342 ms`。
- 用 dev 入口在 `hkrpg_cn ↔ hk4e_cn` 之间切换：banner 与三个 tab 的条目都换成了新游戏的内容（截图 `/tmp/dev_switch.jpg`、`/tmp/dev_genshin.jpg`）。
- `dotnet test` 36 通过。

**代价 / 未做**
- 换游戏的瞬间先显示「上一张/兜底图」，几百毫秒后才换成新游戏的背景；这是有意的（首帧优先）。
- 建议背景接口在本机仍会超时（TLS 首包），只是不再挡画面；没有做「预取 + 落盘缓存」。

### D63 运行环境页重排：三段式 + 方案独立编辑页

**背景**：用户 2026-11-12 决定重新讨论运行环境的逻辑与 UI，并在四条方向上确认「按你倾向来」：方案编辑改独立页、前缀单独成段、运行器只读+手动添加、游戏页只加「诊断」。

**设计**：见 `docs/linux-port/17-runtime-environment-ui.md`（现状 → 目标形态 → 方案编辑页 → 数据落点 → 分期 P1–P4 → 不做 → 5 条默认取舍）。

**关键取舍**
- 运行环境页按「运行器 → 方案 → 前缀」三段纵向排列，把「前缀」从方案的附属提升为可见实体（因为多个方案共用前缀是常态，且前缀会坏、会占几十 G、会被占用）。
- 方案编辑从 `ContentDialog` 表单改为独立页，页内直接显示运行器状态、图形层是否已装进前缀、前缀的绝对路径/大小/占用/共用方案数；保存前做轻校验，不弹二次确认。
- 运行器保持只读清单 + 「添加路径…」（探测 `wine --version` 后记入 `manual-runners.json`），不下载、不复制系统组件（延续 2026-11-11 的边界）。
- 游戏设置页保持下拉，只加「诊断」（复用现有可点修复的预检对话框）。

**代价 / 未做**
- P1 先只做信息可见（方案仍走旧弹窗），P2 才换独立编辑页——中间态会短暂存在两套编辑入口。
- 前缀大小统计要扫目录，默认进页算一次并缓存 10 分钟，可能显示旧值。
- 方案「上次启动」徽标依赖 `LastKnownGoodStore` 扩展字段，旧文件缺字段时显示「未知」。

**状态**：设计稿 v2 已落盘（含 §5 图形层细则），代码未改动。v2 相比 v1 补充：运行器/方案/前缀三段的状态字段与文案表；DXVK/VKD3D 按 SHA-256 分组去重、组件删除边界、前缀侧「安装/重装/移除/校验」、换版本先移除旧文件并留 `graphics.json.bak`；明确 `WINEDLLPATH` 叠加未验证、不进实施；分期 P1–P4 各带验收标准。

## 协作约定复核（2026-11-05，来源：聊天记录）

用户要求「顺便整理一下聊天记录看看还有哪些需要计入文档的」。以下是从 m00001 起到 m11348 为止所有明确要求，标注当前落点（✅ 已入文档 / 🆕 本次补入）：

| # | 约定 | 来源 | 落点 |
|---|---|---|---|
| 1 | 未明确说明前不得动手，默认一切是沟通 | m00001 | ✅ `01`、D30；D46 第 3 条 |
| 2 | 任何细节都要沟通并落实到文档 | m00001 | ✅ 本目录 00–15 |
| 3 | 积极引入仍在维护的三方依赖，不手搓 | m00001 | ✅ D5、`05-dependencies.md` |
| 4 | 移植不是重做：不改写上游代码，体验尽量一致 | m00001 | ✅ D3、D30 |
| 5 | Linux 必须可用，Windows 可用性不做强要求 | m00115 | ✅ D1/D2 |
| 6 | 真机验收用原神与绝区零 | m00119 | ✅ D15 |
| 7 | 在独立分支提交、随进度拆分提交 | m02240 | ✅ `linux-port` 分支，提交链见 git log |
| 8 | 验证推迟期间继续推进无需用户操作的部分 | m05423 / m05843 | 🆕 本表：真机启动/登录/托盘目视待用户，其余照常推进 |
| 9 | 图标字体方案推迟，先处理小问题 | m08435 | ✅ D36（推迟中） |
| 10 | 测试克隆用 btrfs reflink 放 `~/games`，禁止往 `/tmp` 塞大文件 | m09344 / m09356 / m09389 | 🆕 本表：`/tmp` 仅放 KB–MB 级日志与截图 |
| 11 | 危险/调试入口不得进发行版 | D35 | ✅ D35、`#if DEBUG` 门控 |
| 12 | `src/Starward/` 保持 0 改动（上游可同步） | D1 / D45 | ✅ D1、D45 |
| 13 | 修复/更新/预下载/安装四者的清单与版本标记语义必须分开 | m09496 | ✅ D42 |
| 14 | 结论必须有论据，禁止臆测 | m11348 | 🆕 D46（本文） |

**未入表的其余口头要求**：均已在对应决议里体现（例如「偏差如实记录」→ D3、「占位登记」→ D33、「发行版不留副作用入口」→ D35），不重复列出。


---

## D61 UI 复审后的 9 处修复（设置页可读性、Windows 语义清理、空态与提示）

用 dsh-cu / mcp-kwin 注入真实点击，把「除启动游戏外」的所有页面过了一遍（启动器页、游戏设置、截图、抽卡记录、战绩、设置 8 个子页、图库）。下面 9 条是用户确认后修掉的，全部有真机截图或日志证据。

| # | 现象 | 根因 | 修法 |
|---|------|------|------|
| 1 | 键盘快捷键页写「需要以管理员权限运行」 | 移植时照搬了 Windows UAC 文案 | 换成 Linux 口径：「X11 轮询实现，不需要管理员权限；Wayland 请把 `lunarward --show` 绑到系统快捷键」 |
| 2 | 高级页原「获得管理员权限」入口在 Linux 无意义 | 上游功能在 Linux 无对应物 | 该页没有这个按钮（复审时确认），改为新增「诊断」区：一键生成诊断包 + 打开下载目录；顺手给 RPC 区加「暂未实现」提示 |
| 3 | 常规页「透明效果 / 动画效果」点了没反应 | 那是 Windows 系统设置页的 `ms-settings:` 链接 | 整段移除 |
| 4 | 运行环境页残留「M2 补齐」「前缀模板库」 | 设计迭代后文案未跟上 | 删掉模板库区块与过时注释；文案改成「只做发现 + 选择，不安装也不修改系统组件」；前缀模式标签改成「前缀来源」四种（新建 / 复制自现有前缀 / 使用已有目录 / 临时），`shared` 只对旧配置保留 |
| 5 | 关闭窗口选项写「最小化到系统托盘」 | 实际实现是 unmap 隐藏 | 文案改「隐藏到系统托盘」 |
| 6 | 设置类页面背景透出游戏大字，表单难读 | 页面根容器透明，背景层在最底下 | 设置页、游戏设置页根容器加 `ApplicationPageBackgroundThemeBrush` 实底（启动器页保持背景） |
| 7 | 战绩页未登录时内容区一片空白 | 没有空态分支 | 加空态层：图标 + 「还没有登录或没有绑定角色」+ 说明 + 登录按钮（`ShowRecordEmptyState` 由 `CurrentRole`/`GameRoleList` 推导） |
| 8 | 方案被删除后绑定悬空，只写日志 | `ProfileBinding.Resolve` 静默回退 | 游戏设置页顶部 InfoBar：写明原绑定 id 与回退到哪个方案，附「重新选择运行环境」按钮；选完自动消失 |
| 9 | 手柄增强页整页可点但 Linux 无实现 | M4 占位 | 页面顶部 InfoBar + 主开关置灰 |

代价与未做：
- 这些 Linux 专属文案先用中文字面量（不新增 11 个 resx 的键），与 D51「CLI 固定中文」同一口径；后续统一入 `Lang.resx` 时再抽。
- 空态只覆盖「未登录 / 无角色」；战绩页里各子页（深渊、幻想真境剧诗等）的空态沿用上游。
- `--screenshot` 之前只有 `--capture-test` 的名字，这次补了正式名字（同一个分支，旧名保留）。


**P1 落地（2026-11-12，提交 `e234877`）**：三段落成且真机核对通过。

- 数据层：`PrefixManager.PrefixInspection` + `Inspect(path)`（`Exists`/`HasDriveC`/`HasRegistry`/`Locked`/`SizeBytes`/`Managed`/`GameKey`/`ProfileId`/`Mode`）+ `EnumerateKnownPrefixes(profiles)`；目录大小带 10 分钟缓存（`ConcurrentDictionary<string,(long,DateTime)>`），`IgnoreInaccessible` + 跳过 reparse point，单个文件读不到不影响整页。
- 运行器：`ComponentStore.ManualRunners`（`~/.config/lunarward/manual-runners.json`）作为第五段扫描来源，`[添加路径…]` 选文件 → `ReadRunnerVersion` 探测 → 落盘 → 刷新；行徽标追加「N 个方案在用」（现算 `profile.RunnerPath` 与组件路径全等比较）。
- 方案：沿用现有行 + 旧编辑对话框（P2 再改独立页）。
- 前缀：单独成段，行显示 `模式 · 游戏` / `大小 · N 个方案使用 · 状态` / 绝对路径 / `[打开目录]`，仅「我们的 + 无人引用 + 未被占用」才出现 `[清理]`（二次确认）；外部前缀只读。
- 未做（按分期）：方案独立编辑页（P2）、图形层分组与安装/移除/校验（P3）、`lastLogin` 徽标（P4）、SHA 去重（P3）。
- 代价：前缀大小首次进页会遍历一次（本机 `.wine` 370 MB 约 0.2 s）；新文案仍是中文字面量（沿用 D61 口径，i18n 时统一收）。

**P2–P4 落地（2026-11-12 同日）**

- P2 方案编辑：改成**页内整页视图**（`ScrollViewer_Editor` 与列表互斥显示，带「← 返回」）。四段：名称 / 运行器（下拉 + 原运行器不可用提示）/ 图形层（DXVK 下拉，写明「装进前缀后生效」）/ 前缀（新建 / 复制自现有 / 使用已有 / 临时 + 路径 + 浏览 + 状态提示）/ 高级折叠（`KEY=VALUE` 多行环境变量、调试 Shell）。保存做轻校验：运行器不可用拦下、`external`/`template` 必须填目录。`RuntimeProfile` 的 `Environment` 是 `Dictionary<string,string>`，保存时按行解析，不整体重写 `RuntimeProfileStore`。
- P2 游戏页诊断：`GameSettingPage` 底部新增「诊断 → 运行启动检查」，跑一次 `LaunchPreflight.Check` 并以对话框摊开六项（结论行 + 每项状态/说明）。
- P3 图形层：`ComponentStore.GroupGraphics` 按 `x64/d3d11.dll`（VKD3D 取 `d3d12.dll`）的 SHA-256 前 12 位分组，同内容折叠并显示「共 N 份」；`GraphicsInstaller` 新增 `Verify(prefix)`（存在 + 长度比对 → `已装 xxx · 与记录一致 / 不一致（N 个缺失）/ 未安装`）、`Remove(prefix)`（按记录删文件再删记录）、写记录前留 `graphics.json.bak`，记录里增 `Sizes`。前缀行据此显示状态并给 `安装图形层 / 重装 / 移除图形层 / 校验`；外部前缀的移除要二次确认。
- P4 `lastLogin`：方案行显示 `上次启动：MM-dd HH:mm 成功 · <runner 版本>`，数据来自 `last-good.json`（`LastKnownGoodStore.Get`），无记录显示「还没有成功启动记录」。
- 新增 CLI（便于脚本与排错）：`--graphics-install <prefix> <id|名称|路径>`、`--graphics-verify <prefix>`、`--graphics-remove <prefix>`。
- 实测（临时前缀 `~/games/lw-prefix-test`，测完已删）：`--graphics-verify` → `未安装（0/0）`；装 → `已装 dxvk · 与记录一致（5/5）`；删掉一个 `d3d11.dll` → `不一致（1 个缺失）（4/5）`；重装 → 恢复一致；移除 → `未安装（0/0）`，目录里留下 `graphics.json.bak`。
- 未做：图形组件的「删除」（仅允许删我们自己导入的那份；UI 未接）、`WINEDLLPATH` 叠加、SHA 参与 `Verify`（只在分组时用）。

**冗余清理（2026-11-12 同日，按「移植保真 + 组件管理够用」两条判）**

- 删除 `GameNoticeWindow`（4 个文件 + `GameNoticeWindowClosedMessage` + `--page notice` 调试入口 + `GameBannerAndPost` 里的注册/处理 + csproj 的 `Compile Remove`）。理由：D40/D50 已定「公告走浏览器」，内嵌窗口是死代码，留着还要维护 XAML 与排除项。
- 取消 `shared` 前缀模式：`PrefixManager` 的两处分支删掉，`RuntimeProfileStore.MigrateLegacy` 把旧配置的 `shared` 归一成 `dedicated`（并保留原 `PrefixPath`，不让前缀换地方）。「共用」改由「多个游戏绑同一个方案」表达。
- 删除旧的方案编辑对话框 `ShowProfileEditorAsync`（约 270 行）+ `DevSwitches.ProfileEditor` 逃生口。理由：P2 已改成页内整页编辑，旧对话框只剩一个 DEBUG 入口在用，两套表单会让字段慢慢分叉（`EnsureGraphics` 就只在旧对话框里）。
- 删除 `--capture-test` 旧别名，只留 `--screenshot`。

**D64 冗余清理第二批：来源标签、`EnsureGraphics` 归一、一次性运行**

- **来源标签**：`Component` 加 `Source`（`managed` / `system` / `launcher` / `steam` / `manual`），`ComponentSource.Guess` 按路径判定（本程序目录 → Steam → `~/.local/share/{sleepy-launcher,anime-game-launcher,honkers-launcher,lutris,bottles,heroic,umu}` → 其余算系统包），手动添加的显式标 `manual`。UI 上单独一行放在最左列——塞进路径行会被省略号吃掉（首版实测截图 `/tmp/r5a.png`）。来源只用于显示与排序，不参与「能不能用」。
- **`EnsureGraphics` 归一**：`RuntimeProfile.GraphicsId` 非空即代表「启动前装图形层」，`EnsureGraphics` 布尔是冗余开关（也只在已删的旧对话框里能改）。字段改成 `LegacyEnsureGraphics`（`[JsonPropertyName("EnsureGraphics")]`），读旧配置时 `false` → 清空 `GraphicsId`，随后删掉两处使用（启动装配、预检）。
- **一次性运行**（「在这个方案下运行」的第二形态）：新增 `Features/Runtime/RunOnceService.cs`，与游戏启动/伴随启动共用 `ProfileShellService.BuildEnvironment`；`IsWindowsExecutable` 判 `.exe/.bat/.cmd/.msi` 走方案里的 wine，其余直接执行；`SplitArguments` 支持引号包裹但不做 shell 解释。入口：运行环境页方案行的「运行程序…」（选文件 + 可选参数）、CLI `--run-once <方案 id|名字> <程序> [参数…]`（等 5 分钟并回传退出码）。
- 实测：`--run-once "默认（opt）" /usr/bin/env` 输出 `LUNARWARD_WINE=/opt/spritz-wine-tkg/bin/wine`、`WINEPREFIX=/home/qie2035/.wine`、`LUNARWARD_PROFILE=默认（opt）`、`LUNARWARD_PROFILE_ID=8b3d42fa`，退出码 0 —— 与游戏启动的环境一致。
- 又删的冗余：图形组件区标题改成「图形层（DXVK / VKD3D）」并合并进同一段（原来「运行器」与「已检测到的组件」两处都列运行器）；「临时（退出即删）」前缀选项移进编辑器「高级」折叠，避免与「新建/复制自现有」并列时选错。
- 未做：图形组件「删除」入口（只删我们自己导入的那份）、`WINEDLLPATH` 叠加、方案导入导出。

**D65 测试分层与门禁**

- 四层：L1 单元（xunit，CI 必过）、L2 CLI/无头集成（CI，临时 XDG + 假前缀 + 假 Sophon）、L3 GUI 端到端（本机 X11，`cu_*` 注入 + 日志断言 + 截图判据）、L4 人工验收（真实账号/游戏，用户在场）。
- 隔离：L2/L3 一律把 `XDG_CONFIG_HOME`/`XDG_DATA_HOME`/`XDG_CACHE_HOME`/`XDG_STATE_HOME` 指向 `mktemp -d`，不碰用户真实配置与游戏目录。
- 用例登记：`19-test-plan.md` §4 为唯一清单，ID 形如 `T-RT-04`；修完的提交信息必须带 ID；S1/S2 缺陷先补用例再修。
- 门禁：push 跑 L1 + L2 冒烟；每晚跑 L2 全量 + L3 关键 10 条；发布前 S1/S2 清零 + L3 关键全过。
- 代价：GUI 自动化会抢焦点、需要真实会话；假 Sophon 服务要自己维护；L4 无法自动化的部分依赖用户时间。
- 最终版（`19-test-plan.md`）：196 条用例，14 个模块分册（RT/LA/IN/CF/PL/UI/AC/CL/RC/SEC/LP/SO/CP/I18N）；新增性能预算表（11 项指标 + 基线 JSON + 20% 回归阈值）、故障注入（kill -9 时点、磁盘满、断网、外部命令卡死）、浸泡（2 小时 / 200 次切页 / 100 次截图）、安全用例（路径穿越、符号链接写穿、命令注入、敏感文件权限）、以及「Uno.UITest 不采用」的证据说明与 L1.5 进程内断言层。

## D68 · 建议背景的超时语义与视频帧看门狗（2026-11-13）

**背景**：F-21 排查发现「视频壁纸有时不动」有三类原因，过去混在一起：

| 类型 | 判据 | 处理 |
| --- | --- | --- |
| 上游语义：看过的背景不自动播视频 | `BackgroundService.GetSuggestedGameBackgroundAsync` 在无新背景时把上次背景 `StopVideo = true` | **不改**（对齐上游）。想要「选过就一直播」属于 UX 偏离，见「未做」 |
| 请求失败：超时/异常把画面换成兜底图 | 1200px 截图前缀为兜底 CG、日志 `using fallback image` | 已改：见下 |
| 真正没解码 | 无 `.webm` 句柄 + CPU 空闲 | 新增看门狗日志，见下 |

**改动**（`Features/Background/AppBackground.Linux.cs`）

1. 建议背景超时默认 1500 ms → **4000 ms**，并在超时后**再试一次**（`apiTimeoutMs`、`2×apiTimeoutMs`）；两次都失败才算「拿不到」。
2. 「拿不到」与「拿不到新背景」都不再覆盖已有画面：`BackgroundImageSource is not null || _videoActive` 时记 `keeping current picture` 并返回。
3. 失败结果只缓存 **30 秒**（成功仍缓存 5 分钟），避免一次抖动影响数分钟。
4. 调试入口 `LUNARWARD_VIDEO_BG` 起播后同样写 `_appliedGameId/_videoActive`，消除重复起播。
5. 新增帧看门狗：起播后 2 秒仍 `_videoFrameCount == 0` → `[background] video started but no frame in 2000 ms`（实测用 `/tmp/fake.webm` 触发）。

**代价**：最坏情况（两次超时）多等约 12 秒后台请求，但界面在此之前已用本地图填好，不影响可交互性。

**未做（需单独拍板）**：让「用户点选过的视频背景」在启动时继续播放（偏离上游 `StopVideo` 复用语义）。

### D69 视频壁纸的失败可见性与背景记账（2026-11-13）

**背景**：F-21 一度被定性为「视频壁纸回归」，复核后确认判断前提错误（当时当前游戏是 `abc_cn`，其记录的背景是静态海报，按上游规则本就不播视频）。但沿这条线查出三处真偏差。

**改动**

1. `TryStartVideoBackgroundAsync` 的 4 条失败路径补日志：`video skipped: libvlc unavailable` / `cancelled` / `no video file (download failed or url missing)` / `file not found: <path>`（原来全是静默 `return false`）。
2. 起播失败但已有画面（`BackgroundImageSource is not null || _videoActive`）时记 `video unavailable, keeping current picture` 并直接返回，不再用兜底图覆盖当前画面；只有完全没画面才用兜底图。
3. 补上游记账：`RecordAppliedBackgroundAsync(background, filePath, token)` → `AppConfig.SetBg(biz, 文件名)` + `AppConfig.SetGameBackgroundIds(biz, ids)`，静态应用与视频起播各调一次，跳过 `CUSTOM`；对应上游 `AppBackground.xaml.cs:249-251`。

**代价**：`RecordAppliedBackgroundAsync` 每次应用背景会多一次 `GetGameBackgroundsAsync`（命中缓存，实测 <1 ms 的字典查询 + 首次网络）；日志量增加 1–3 行/次切换。

**未做**：没有为「视频起播失败」做自动重试；没有把「上次背景是静态 → 这次也静态」做成用户可关闭的开关（保持与上游一致）。

### D70 自有代码里「无痕 catch」的收口（2026-11-13）

全仓 `catch {}` 共 187 处，183 处属上游共享文件（不动）。`*.Linux.cs` 的 4 处里，`ScreenCapture.Linux.cs` 两处（Kill 失败、删临时文件失败）保持无痕；另两处改为留痕：

- `GameInstallScanner.Linux.cs`：重复安装路径去重失败 → `logger.LogWarning("清理重复安装路径记录失败（不影响本次扫描结果）")`，返回结果不变。
- `GameInstallService.Linux.cs`：`cts.Cancel()` 只吞 `ObjectDisposedException`（正常竞态），其余记 `Serilog.Log.Warning("停止下载任务时取消令牌失败：{game}", biz)`。

**代价**：扫描/停止路径各多一条日志，无行为变化。**未做**：没有把这条规则扩到上游共享文件（那会破坏零偏差）。


## D71 · Uno Pivot 换 ItemsSource 的越界异常：切换前把 SelectedIndex 置 -1

**问题**：每次切游戏时 `GameBannerAndPost.UpdateGameContentAsync` 给 `PostGroups` 赋新列表，日志稳定抛 `ArgumentOutOfRangeException`（`UIElementCollection.get_Item`），落在 `Pivot.SynchronizeSelectedItem()`。

**根因（对过 Uno 6.7.45 `Pivot.cs:295-301`）**：该方法的守卫是 `SelectedIndex != -1 && HasItems`，随后直接 `_staticHeader.Children[selectedIndex]`。换 `ItemsSource` 时 `HasItems` 立刻为真，而 `_staticHeader.Children` 要等 `SynchronizeItems` 之后才重建，于是拿旧索引去索引空集合；`selectedIndex` 又被 `Math.Max(...,0)` 夹成 ≥0，必然越界。上游 WinUI 在 Windows 上不会踩到（推断，未实测）。

**决定**：在 Lunarward 自己那份 `GameBannerAndPost.xaml.cs`（D62 已存在，非上游文件）里，赋值 `PostGroups` 之前先 `Pivot_Posts.SelectedIndex = -1`，赋值完成后再设回 0。`-1` 让 Uno 的第一道守卫直接返回，不进入索引。

**代价**：每次切换多两次属性赋值；标签页选中项回到第一个（上游行为也是每次换游戏默认第一个）。

**验证**：真机切 4 次游戏，`Starward_261113.log` 里 pid 87809 的 `EROR` 行数 = 0（修复前同一操作 20 次异常）。


## D72 · 切游戏卡 4 秒：UI 线程不得 fork wine（注册表访问改异步 + 整键批量 + 缓存）

**现象**（用户口径）：切到绝区零秒切，切到原神/星穹/崩坏3 整窗静止几秒后恢复。

**证据链**：
- `LUNARWARD_UI_WATCHDOG=1` 看门狗：启动 1024 + 4224 ms，切原神 4047 ms，切星穹 4346 ms，切绝区零 **0 次**。
- 临时打点抓到元凶：`[tempdiag] wine "reg query HKCU\Software\miHoYo\崩坏：星穹铁道 /v App_LastUserID_h2841727341" took 4159 ms (rc=1)`，紧随 `[watchdog] UI 线程卡顿 4212 ms`。
- 调用链：`GameBannerAndPost.UpdateGameNoticeAlertAsync()` → `GameNoticeService.IsNoticeAlertAsync()` → `GetUidFromRegistry()` → `WineRegistry.GetValue()` → `Process.Start("wine reg query") + WaitForExit()`（同步）。
- **为什么绝区零不卡**：`GameNoticeService.GetUidFromRegistry` 只有 `bh3/hk4e/hkrpg` 三个分支，`nap` 直接 `return 0`，根本不碰注册表。对照改 `CurrentGameBiz` 重启各一次：`hkrpg_cn` 1 次 wine 调用 4159 ms + 卡 4212 ms；`nap_cn` 0 次调用、0 卡顿。
- wine 冷启动本身就要 ~4 s（`wineserver` 未起时），shell 里同样 4.199 s；这不是我们的开销，但绝不能放在 UI 线程。

**决定**：
1. `WineRegistry` 全量改异步：`GetKeyValuesAsync/GetValueAsync/SetValueAsync/SetValuesAsync`，`RunAsync` 用 `WaitForExitAsync` + 15 s 超时 + 并行排空 stdout/stderr；同步 API 保留为 `.GetAwaiter().GetResult()` 包装（CLI 用）。
2. **一次 `reg query` 取整键**（原来一个值一次 fork），`N 个值只 fork 一次`。
3. 结果按 `prefix|归一化键` 缓存：命中 5 分钟，键不存在缓存 60 s（游戏刚写入票据能读到）；写操作后失效对应键。
4. 写多值改用 `wine reg import` + 临时 `.reg`（UTF-16LE **带 BOM**），一次 fork 写完整键。
5. `UsePrefix` 是 `[ThreadStatic]`，异步续体可能换线程 → 前缀必须在任何 `await` 之前取出，并沿参数传递；`SetGenshinEnableHDRAsync` 显式传 prefix。
6. 调用方全部改真异步：`GameNoticeService`、`GameAccountService`、`GameAccountSwitcher`、`GameBannerAndPost`、`GameSettingService`（写设置六处）、`GameSettingPage.ApplySettingAsync`、`GenshinHDRLuminanceSettingDialog.SaveAsync`、`ScreenCaptureService`（HDR 亮度）。
7. 顺带把「创建/检查前缀」挪出 UI 线程：`GameLauncherPage` 两处 `PrefixManager.EnsurePrefix` 与 `GameLauncherService.StartGameAsync` 的 `LaunchPreflight.Check` + `CreateGameProcessStartInfo` 包进 `Task.Run`（wineboot 十几秒）。

**验证**：
- 看门狗重启后：启动只剩 1 次 1019–1266 ms 的平台渲染卡顿，切原神/星穹不再出现 4 秒卡顿，切绝区零仍 0 次（行为不变）。
- 日志里 `wine reg query … 耗时 4235/4506 ms` 仍在，但**不再伴随 `[watchdog] UI 线程卡顿`** —— 说明它们在后台线程。
- `tests/Lunarward.Tests/RegistryFormatTests.cs` 12 条覆盖 `ParseValues/ParseLine/BuildRegFile/NormalizeKey`（`(Default)` 行必须跳过，长 hex 行可解析）。
- 真机 `reg import` 端到端：用与代码完全相同的字节组装（BOM + UTF-16LE）导入 `HKCU\Software\LunarwardSelfTest`，rc=0 且回读到 `Flag REG_DWORD 0x1`、`Name REG_SZ 自检`；400 字节的 hex 行也能一次导入。

**代价**：注册表值最多滞后 5 分钟（游戏内改的账号要在 5 分钟后才反映，或重启启动器）；写操作会失效缓存，所以启动器自己写的立刻可见。

**2026-11-14 补充（同类收口）**：按同一原则清掉三处「一次点击卡一下」的同步重活——
1. `RuntimeEnvironmentSetting.Button_ImportGraphics_Click` 的 `CopyDirectory`（导入 DXVK 组件，几百文件 / ~100 MB）包进 `Task.Run`；
2. `GraphicsInstaller.EnsureInstalled` 的三个 UI 调用点（`GameLauncherPage` 预检修复、`RuntimeEnvironmentSetting` 前缀行与组件行）包进 `Task.Run`（拷 5 个 DLL + SHA-256）；
3. `RuntimeEnvironmentSetting.LoadComponents` 改为 `LoadComponentsAsync`：`ComponentStore.GetRunners/GetGraphics` 的首次扫描（PATH + 所有 Steam 库 + `compatibilitytools.d`）在后台跑，扫完再回 UI 线程建行。

验证：`--page setting --subpage RuntimeEnvironmentSetting` 冷启动看门狗只剩启动那 1 次平台渲染卡顿（1244 ms），页面组件行正常渲染（截图 `RuntimeEnvironmentSetting` 6 个运行器、来源标签齐全）；`dotnet test` 61 通过。

## D73 · 运行环境模块 v3：三概念 + 自洽硬目标（阶段 1 组件层已落地）

**背景**：用户 m21954「先搁置（实现），我们讨论下 Linux 补充部分如何设计，例如这个"运行环境"模块」；m21958「不要过度封装/傻瓜式，不要局限在我说的原始痛点」；m21961「配置文件只在极特殊状况手改」；m21966「实际不会有那么多环境配置，多数是一个游戏一份，甚至多个游戏共用一份」；m21973「倾向于带（wine 下载），至少确保启动器自洽」；m21984「你的建议都不错，按你的建议来吧」。

**决定**（全部写进 `23-runtime-env-design.md`，取代表 16/17 两份 v2 文档）：

1. **只留三个概念**：组件 / 环境（配方）/ 绑定。前缀**不是**概念，是环境里的一个路径字段 + 一段只读检查。
2. **自洽是硬性目标**：干净 Arch 上装完本程序，不依赖 Steam、不依赖别的启动器、不依赖发行版 wine，仅靠程序内操作就能跑起来。三样必需物全由本程序提供：运行器（受管下载）+ 图形层（受管下载）+ 前缀（`wineboot`）。**边界**：32 位库/Vulkan loader/驱动在系统里，不代装、不假装能装。
3. **组件三类 `kind`**：`wine` / `proton` / `graphics`；来源 `managed`/`system`/`steam`/`umu`/`manual`。非 managed 只读引用（不复制、不修改、不删除、不算占用）。
4. **受管目录 + manifest**：`~/.local/share/lunarward/components/{wine,graphics}/<name>-<version>/` 旁边 `manifest.json`（记录 url/时间/SHA-256/layout/root）；**布局归一化**（`bin-wine` / `files-bin-wine` / `bare-exe`）是必须步骤。
5. **下载源可配置不写死**：默认 wine-staging（一键项）→ wine-tkg → GE-Proton，走 GitHub Releases API；条目带 `mirrorPrefix`；有 API `digest` 用它，否则 TOFU 记 SHA-256；可取消、解压前 `df` 校验空间、失败打真实 URL。**不自动更新运行器**，只标「有新版本」。
6. **报错给原文，不映射发行版包名**（用户 m21977 明确要求）：健康状态只放一句概括，点开是等宽、只读、可全选复制的原始 stderr + 复制按钮。
7. **前缀去枚举化**：删 `template/dedicated/shared/external/ephemeral`，只剩三个动作（新建 / 指向已有 / 从…复制）。`WINEARCH` 新建时选一次，之后只读。root 运行拒绝。占用沿用 `FileShare.None` 锁 + 界面表达「正在被 X 使用」。
8. **环境八个字段**：name / runnerId / graphicsId / prefixPath / env / args / wrappers / notes。`env` 三层优先级（自动注入 → 配方 → 进程），**配方覆盖自动注入且冲突要标红说明**；`WINEDLLOVERRIDES=mscoree,mshtml=` 是可见默认值。`wrappers` 是有序命令前缀列表，拼在 `wine` 前，`--launch-plan` 为唯一真相。
9. **绑定按 id**（重命名不影响），多对一是正常用法；新游戏首次启动**自动建同名环境**并提示。
10. **检查四层归并成一张清单**（组件/前缀/图形/启动），每项带动作（复用 `PreflightAction`）；额外检测游戏目录自带 `d3d11/dxgi/d3d12.dll` 并提示（不自动删）。`last-good` 只存**单条**记录 + 「用上次能跑的组合重新启动」。
11. **持久化两条硬规则**：保存即全量（不做增量合并、不顺手修正未触碰字段）；没有隐藏字段（界面可影响运行的项必须能在导出 JSON 里看到，反之亦然）。派生数据全放 `~/.cache/lunarward/`，可随时删。
12. **明确不做**：`extends` 覆盖层、矩阵试跑、Run 历史、导入 CLI、自动更新运行器、包名映射、向导、前缀进左栏、模板库、`ephemeral` 模式。

**13. 组件下载必须并行**（用户 m22018）：`HEAD` 探 `Accept-Ranges` → 支持则分片并行（默认 4 连接、8 MiB/片、`.part.<n>` 落盘、可续传）→ 不支持退回单流并提示；多组件排队、一次只下一样；被限速（连续两片 429/403）自动降为 1 连接并记录；解压优先 `.tar.zst`，只有 `.tar.xz` 时在后台线程用 `xz -d -T0` + `tar -xf` 外部命令，不用托管解压库。理由：串行单流是 an-anime-team 那类启动器的实际短板，安装链路 5 并发实测已到 14 MB/s，4 连接足以打满常见宽带。

**验证**：`23-runtime-env-design.md` §10 给出 A1–A8 / B1–B5 / C1–C5 / D1–D4 / E1–E3 验收项。

**代价**：磁盘（wine 构建 400–700 MB、GE-Proton ~1 GB；换版本时提示保留/删除）；组件更新需手动；Proton 在自洽模式下仍需 Steam 或 umu（界面必须写明）。

**状态**：阶段 1/2/3 均已落地并推上 `main`（实施记录见 `23-runtime-env-design.md` §12、§13；§11 的 5 条待定细节已在实施中定案，见 §13.1–§13.7）。

---

## D74 前缀只是路径：取消 template / ephemeral / shared 三种「模式」

**背景**：D63 引入的 `PrefixMode` 把「前缀从哪来」编码成枚举（`template` / `dedicated` / `shared` / `external` / `ephemeral`），实际使用中它带来三类问题：①同一个前缀在不同模式下行为不同（例如 `shared` 与 `dedicated` 的差别只剩文案）；②`template` 需要额外维护模板库，而「从现有前缀复制」已经覆盖同样需求；③`ephemeral`（退出即删）与「路径存在与否」没有本质区别，却要额外记一份状态。

**决定**：

1. `RuntimeProfile.PrefixPath` 是唯一事实：**空** = 用默认路径 `PrefixRoot/<名字>`（首次启动时创建）；**有值** = 用这个路径，存在就复用、不存在就创建。
2. 取消 `PrefixMode` 与 `EnsureGraphics`，删除 `template` 模板库、`ephemeral` 退出清理、`shared` 语义（D64 已先合并 `shared`）。
3. 共享前缀 = 两份环境填同一个路径；隔离前缀 = 两份环境填不同路径。界面不再出现「模式」这种词。
4. 迁移：`LoadAll` 时读旧 `profiles/*.json`，`dedicated` / `external` / `shared` 保留 `PrefixPath`；`template` / `ephemeral` 清空 `PrefixPath`（回到默认路径）并把原值写进 `Notes`，旧目录整体改名为 `profiles.migrated-<时间戳>`。
5. 前缀被谁占用、占地多少、是不是 `drive_c` 齐全，一律现查（`PrefixManager.Inspect`）而不是存状态。

**代价**：①「临时环境」不再有一键语义，需要临时前缀时手动填一个路径、用完自己删（或建完复制一份）；②旧 JSON 的 `PrefixMode` 只读不写；③模板复用要靠「从现有前缀复制」，复制 650 MB 大约 0.2 秒（btrfs reflink）。

**验证**：`--launch-plan` 在迁移后的真实配置上打印出正确的前缀与命令行；5 条 `MigrateFromLegacy` 单测覆盖上述规则。

## D76 登录窗改用「本程序 + WebKitGTK 6.0」（A 方案）

- **背景**：D32/D75 当初判定「登录窗不改 GirCore」，论据是「本机没有 `libwebkitgtk-6.0`」。该论据不成立——`extra/webkitgtk-6.0 2.54.2-1` 一直在官方仓库里，只是本机没装（D46：结论要有论据，不能拿「我这台机器没有」当「目标环境没有」）。
- **两种方案都实测过**（spike 记录在 `poc/webkit-spike/README.md`）：
  - **A（独立进程）**：`poc/webkit-spike` 用 GirCore 0.8.1 + GTK4 + WebKitGTK 6.0 开窗并渲染页面成功。
  - **B（同进程）**：`poc/uno-smoke` 里 `GtkWebProbe.cs` 证明 Uno 进程内也能开 GTK4 窗口且 Uno 主循环继续跑。
- **决定**：采用 **A**。理由：①进程内 GTK3（Uno 自带 GtkSharp 3.24）+ GTK4 并存属不受推荐组合（必须 `extern alias` 隔离，`CS0433` 是常态）；②GTK 窗口标题栏由 WM 装饰，颜色跟系统配色走，`GTK_THEME` 只影响窗口内部，观感上「像另一个程序」，要统一得自绘 CSD。
- **实现**：`lunarward --webkit-login --url <登录页> --output <cookies.json> [--timeout 秒]`（`Features/GameRecord/WebKitLoginHost.cs`），由 `WebLoginService.LoginAsync` 用 `Environment.ProcessPath` 拉起自己；**没有 `libwebkitgtk-6.0` 时自动退回 python3 助手（D32 保留）**，两者输出格式一致（`{Cookies:{...}, Time:"yyyy-MM-ddTHH:mm:ss"}`）。
- **cookie 怎么拿**：`CookieManager.SetPersistentStorage(<output>.db, Sqlite)` + 每 2 秒读 `moz_cookies`（`name`/`value`）。之所以不调 `GetCookies`：GirCore 0.8.1 只生成了 `*Finish`，没有 `GetCookiesAsync` 这类异步入。命中标记（同 python 的 `LOGIN_MARKERS`）即导出并关窗；关窗也导出；兜底超时导出。
- **代价**：①新增 `GirCore.Gtk-4.0` + `GirCore.WebKit-6.0`（0.8.1）两个包引用与 `Aliases="gir"`；②运行时要求 `webkitgtk-6.0`（Arch 包名同名），缺失时退回 python 助手（功能不退化，只是又需要 python3 + pygobject）；③AUR 包 `packaging/PKGBUILD` 已把 `webkitgtk-6.0` 并入 `depends`（pkgrel 2），原 `webkit2gtk-4.1` / `python-gobject` 降为 `optdepends` 并注明「仅在没有 webkitgtk-6.0 时需要」。
- **验证**（实测，2026-11-14）：本地起 `Set-Cookie: ltuid_v2 / cookie_token_v2` 的 http 服务，`--webkit-login` 开窗后 2 秒内即输出 `{"Cookies":{"ltuid_v2":"123456789","cookie_token_v2":"tok_abc"},"Time":"2026-11-14T21:16:50"}`，退出码 0；`moz_cookies` 里两条与页面一致。**待验收**：真实米游社登录（需用户操作）。

## D92 `packaging/PKGBUILD` 本地模式零复制：软链工作区 + `mv` 代替 `cp`

**背景**：用户 m03533「感觉你这个PKGBUILD写的不好，会重复源码和构建，有别的好写法吗？」。实测一次本地打包的磁盘占用：`$srcdir/lunarward` 克隆 **235 MB**（含 `.git`）、`$srcdir/publish` **140 MB**（全新 restore + publish，`obj/` 与 `bin/` 全部重来）、`package()` 里 `cp -a` 又产出第三份 —— 这三份里有两份是工作区**已经有的东西**。D91 的代价里写过「`source=()` + `prepare()` 复制工作区会把 `bin/obj` 一起拷进去，得不偿失」，但那是**复制**；改成**软链**就不存在这个问题。

**决定**：

1. 本地模式（默认）用 **`source=()` + `sha256sums=()`**，`prepare()` 里 `rm -rf "$srcdir/$pkgname"` 后 `ln -sfn "$startdir/.." "$srcdir/$pkgname"` —— 源码零复制，`pkgver()` 与 `dotnet publish` 都直接走工作区，复用 `obj/` 增量编译。
2. `package()` 里 `cp -a "$srcdir/publish/." "$pkgdir/usr/lib/lunarward/"` 改成 **`mv "$srcdir/publish" "$pkgdir/usr/lib/lunarward"`**（`install -d` 相应从 `$pkgdir/usr/lib/lunarward` 降到 `$pkgdir/usr/lib`）—— 省掉 140 MB 中间副本。
3. 远端模式（`LUNARWARD_SOURCE=remote`）保持 `git+https://…#branch=main`；AUR 两份不动。
4. `build()` 的 `dotnet publish` 加 **`-p:DebugType=none`**，与 CI 的 publish 一致（`Lunarward.pdb` 不进包）。
5. `lint-pkgbuild.sh` C 节两处修正：行首改 `^[[:space:]]*source=`（source 现在缩进在 if/else 里，原 `^source=` 匹配不到任何行 → 误报「tarball 源有真实哈希」）；新增 `source=()` 空源分支。

**代价**：①本地模式打进包的是**工作区当前文件，包含未提交改动** —— 与 D91「只含已提交内容」相反，这是刻意的（打包验证本来就该验证当前代码），但 README 与 PKGBUILD 注释必须写清，否则会误以为包等于 HEAD；②`$startdir` 在 chroot / 沙箱构建里不可靠，那种场景要用 `LUNARWARD_SOURCE=remote`；③软链让 `dotnet publish` 在工作区写 `obj/Release/net10.0-desktop/linux-x64/`（本来开发构建也写这里）。

**验证**（实测，2026-11-21）：

- `makepkg --printsrcinfo`：除 `pkgbase` / `pkgname` 外**无 `source` 行**，depends 与 D88 一致。
- `makepkg -f` 首次 **34 s**、第二次 **10 s**（增量），产出 `lunarward 0.1.0.r116.gc33e1b1-1`（`pkgver()` 读工作区 `.git`）。
- `du -sh packaging/src` 打包后 **4 KB**（只剩空目录）；`packaging/pkg/lunarward` **149 MB**（只有 pkgdir 这一份）。
- 包内 **391 个文件**：`/usr/lib/lunarward/Lunarward`、`/usr/bin/lunarward`（sh 包装）、desktop、图标、LICENSE；`pacman -Qlp` 无 `.pdb`。
- 解包后 `./usr/lib/lunarward/Lunarward --version` → `Lunarward 0.1.0.0`，退出码 0。
- `bash scripts/test/lint-pkgbuild.sh` 四节全 PASS（1 条预期提醒：AUR 稳定版的 tag 尚不存在）。

## D91 `packaging/PKGBUILD` 默认打本地仓库

**变更记录**：第 1 条的 `git+file://` 已被 **D92** 改成 `source=()` + 软链工作区（零复制、含未提交改动）；第 3 条「只含已提交内容」在本地模式下不再成立。

**背景**：用户 m03385「@packaging/PKGBUILD 默认走本地」。D88 把 source 从 `#branch=linux-port` 改成 `git+https://…#branch=main`（远端），于是想验证本地改动必须先 push —— 而「打包验证」恰恰是 push **之前**最需要做的事，顺序反了。

**决定**：

1. `packaging/PKGBUILD` 的 source 默认是 **`git+file://${startdir}/..#branch=main`**（克隆本地仓库的 HEAD）；`LUNARWARD_SOURCE=remote` 时切回 `git+https://github.com/QiE2035/Lunarward.git#branch=main`。
2. AUR 两份**不变**（仍走远端）：它们的 source 必须能被 AUR 的构建机取到，指本地路径没有意义。
3. 注释与 `packaging/README.md` 都写明「git 源只包含**已提交**的内容」—— `git+file://` 克隆的是 HEAD，不是工作区快照，有未提交改动要先 `git commit`。
4. `lint-pkgbuild.sh` 的 C 节扩展到「source 引用变量」的写法：`git+` 出现在 `_xxx_source="git+…"` 赋值行时同样按 VCS 源判定，否则会被误判成「tarball 源 + SKIP」而报错。

**代价**：①`git+file://` 会克隆整个 `.git`（当前 55 MB / 114 提交），每次几秒 —— 换来 `pkgver()` 里的提交数可用；若仓库涨到数百 MB 再考虑 `source=()` + `prepare()` 复制工作区，但那会把 `bin/obj` 一起拷进去，得不偿失；②本地源打包出来的是**上一个提交**，改完忘 commit 会静默打出旧代码，只能靠 `RUN` 时的 `git log` 反查。

**验证**（实测）：

- `makepkg --printsrcinfo`（默认）→ `source = lunarward::git+file:///home/qie2035/projects/migrate/Starward/packaging/..#branch=main`；`LUNARWARD_SOURCE=remote makepkg --printsrcinfo` → `git+https://github.com/QiE2035/Lunarward.git#branch=main`。
- `makepkg --verifysource` 真克隆成功（`克隆到纯仓库 '…/packaging/lunarward'`）；克隆体里 `rev-list --count main` = **114**、`rev-parse --short main` = **`4937610`**，与本地仓库完全一致 → `pkgver()` 会得到 `0.1.0.r114.g4937610`。
- `bash scripts/test/lint-pkgbuild.sh` 通过（1 条提醒：`aur/lunarward` 的 tag 尚不存在，预期内）。

## D90 CI 上传可运行产物：self-contained tar.gz，保留 7 天

**背景**：用户 m03282「CI 应该上传编译产物」+ m03284「可运行的」。此前 `.github/workflows/linux.yml` 只跑 `dotnet build -c Release` 与各层测试，**没有任何可运行产物**（只有覆盖率的 xml、失败时的 L2 日志）；想试跑必须自己 clone、装 SDK、build。

**决定**：

1. 在全部测试通过之后，追加 `dotnet publish src/Lunarward/Lunarward.csproj -c Release -r linux-x64 --self-contained true -p:DebugType=none` → 打成 `lunarward-linux-x64-<sha7>.tar.gz` → `actions/upload-artifact@v4`（名 `lunarward-linux-x64`，保留 7 天，`compression-level: 1`）。
2. **选 self-contained，不选 framework-dependent**：产物 226 MB、tar.gz **92 MB**，解压即跑，不要求下载方装 `dotnet-runtime`。正式发布形态（AUR / AppImage）仍是 framework-dependent（`depends=('dotnet-runtime>=10.0')`，D88）；两者不冲突——CI 产物是「给我一个立刻能试的东西」，AUR/AppImage 是「给用户的正式件」。
3. 产物根目录附 `RUN.txt`：解压方式、`./Lunarward`、所需系统库（`fontconfig libx11 libxcb xdg-utils webkitgtk-6.0`）、提交 sha、版本，以及「这是 CI 临时产物，正式发行请走 AppImage/AUR」。
4. **PR 不上传**（`if: github.event_name != 'pull_request'`），只在 `push main` 与 `workflow_dispatch` 产出，避免吃 artifact 配额。
5. 上传单个 tar.gz 而不是目录：`tar` 保留可执行位，也避免 artifact 的 zip 打包把 Unix 权限位弄丢。

**代价**：①CI 单次多约 40 秒（publish 25 s + 打包 7 s + 上传）；②每 push 存一份 92 MB artifact、留 7 天；③self-contained 产物用 `ubuntu-latest` 的 glibc 构建，`glibc < 2.39` 的老发行版跑不起来——这类用户应当用 AUR 包或 AppImage。

**验证**（实测）：

- 本机 `dotnet publish … --self-contained true` 后 `./Lunarward --version` → `Lunarward 0.1.0.0`（exit 0）、`--help` 正常输出分组帮助；publish 目录 226 MB，`tar -czf` 后 92 MB（7 s）。
- 对照：framework-dependent publish 目录 147 MB、tar.gz 59 MB（同样实测可运行）。
- CI 端到端（run 44499063793，commit `a3f286a`）：artifact `lunarward-linux-x64` **81 MB**（zip 里含 92 MB tar.gz）→ 解压 215 MB → `./Lunarward --version` → `Lunarward 0.1.0.0`（exit 0）、`--help` 正常、`--profile-ensure` 正确读出已绑定方案；包内有 `libcoreclr.so` / `libhostfxr.so`，确认是 self-contained；`RUN.txt` 里的 commit 与版本正确。

## D89 SDK 下限：`global.json` 只钉到最低 feature band

**背景**：用户 m03131「global.json 导致 aur 编译不过，好像是 dotnet sdk 版本不匹配」。核查（2026-11-19）：

- `src/Lunarward/global.json` 的 `sdk.version` 是 **`10.0.302`**，根 `global.json` 是 `10.0.300`，`tests/`、`poc/`、`poc/video-spike/` 也都是 `10.0.302`，`rollForward` 一律 `latestMinor`。
- **`rollForward` 只向上滚，不向下兼容**：本机装的是 **10.0.404**（`dotnet --list-sdks` 只有这一条），10.0.302 能解析；但如果打包环境装的是 10.0.1xx / 10.0.2xx（低于 302），就直接失败。
- 复现（`/tmp` 里造一个 `global.json` 钉 `10.0.999`，本机只有 10.0.404）：

  ```
  A compatible .NET SDK was not found.
  Requested SDK version: 10.0.999
  global.json file: /tmp/gjtest/global.json
  ```

- **CI 一直绿，掩盖了这个问题**：`.github/workflows/linux.yml` 用 `actions/setup-dotnet@v5` + `dotnet-version: '10.0.x'`，装的是最新 10.0.x（现在 4xx，高于 302）；而 Arch 的 `dotnet-sdk` 版本由 Arch 仓库决定，不跟随我们。

**决定**：

1. 5 处 `global.json` 的 `sdk.version` 一律改成 **`10.0.100`**（.NET 10 首发 feature band），`rollForward` 保持 `latestMinor` —— 任何 10.0.x SDK 都能解析，并由 rollForward 向上滚到实际安装的版本。
2. 根 `global.json` 与 `src/Lunarward/global.json` 就地加注释写明原因与「别改回去」——`global.json` 是 JSONC，`.NET` 接受 `//` 注释（实测三处 `dotnet --version` 都能正常解析到 10.0.404）。
3. `scripts/test/lint-pkgbuild.sh` 新增 **D 节**（防回归）：①所有 `global.json` 的 `sdk.version` 最后一段必须是 `100`，否则 FAIL；②写了 `sdk.version` 就必须有 `rollForward`；③三份 PKGBUILD 的 `makedepends` 必须有 `dotnet-sdk`。
4. 三份 PKGBUILD 的 `build()` 注释与 `packaging/README.md` 改成新说法（不再写「仓库根的 global.json 钉的是 SDK 10.0.300」）。

**代价**：

1. 下限放到 10.0.100 后，如果将来 `Uno.Sdk 6.7.45` 真的用到了只有 10.0.3xx+ 才有的 SDK 行为，问题会以**编译错误**（而不是「找不到 SDK」）的形式暴露。届时正确做法是给 `makedepends` 写死版本要求（`dotnet-sdk>=10.0.3xx`），**而不是**把 `global.json` 改回去。
2. 本机只有 10.0.404，**无法实测** 10.0.1xx 环境能否编译 —— 这一条只能等 Arch 打包时验证（用户验收）。

**验证**（2026-11-19 实测）：`dotnet --version` 在仓库根 / `src/Lunarward` / `tests` 三处都解析到 `10.0.404`（注释不影响解析）；`dotnet build Starward.slnx -c Debug` 与 `-c Release` 均 **0 错误**；`dotnet test` **264 通过 / 0 失败**；`bash scripts/test/lint-pkgbuild.sh` 四节全 PASS（D 节覆盖 5 个 `global.json` + 3 份 `makedepends`）。

## D88 Arch 打包：依赖清单单一来源，三份 PKGBUILD 对齐

**背景**：用户 m02959「arch打包是不是还是有问题」。逐份核对三份 PKGBUILD（2026-11-18 实测）：

- `packaging/PKGBUILD` 的 `source` 写着 `git+file://${startdir}/..#branch=linux-port` —— **`linux-port` 分支 2026-11-13 就停用了**（停在 `5d31bcd`），构建出来是五天前的代码，D78–D87 全都不在里面；而且 `file://` 本地路径别人无法复现。`url` 还指向上游 `Scighost/Starward`。
- `packaging/aur/lunarward/PKGBUILD` 的 `source` 指向 `archive/v0.1.0.tar.gz`，而 `git tag -l` 的结果是 **0 个 tag** —— 必然 404，`makepkg` 直接失败。它注释写「跟随 main 最新提交」，与 source 自相矛盾。
- 三个包的 `depends` 都缺 `libxcb`（`src/Lunarward/Helpers/X11Window.cs:17` `LibXcb = "libxcb.so.1"` 是运行时 P/Invoke）；两个 AUR 包还缺 `webkitgtk-6.0`（D76 登录窗）；都缺 `xdg-utils`（`src/Lunarward/Features/GameSetting/GameSettingPage.xaml.cs:771` 直接 `Process.Start("xdg-open")`）。
- **更根本的一条**：`packaging/.gitignore` 写的是 `lunarward/`（无前导斜杠 → 匹配任意层级），把 `packaging/aur/lunarward/` **整份目录**忽略了。`git ls-files packaging/` 里没有它 —— 这份包定义**从来没进过仓库**，所以 `d095922` 补 `webkitgtk-6.0` 时只改了 `packaging/PKGBUILD`，没人发现 AUR 那两份还停在 11-11。

**决定**（用户 m02984「可以，按你建议来吧」）：

1. **依赖清单统一**为 `depends=('dotnet-runtime>=10.0' 'fontconfig' 'libx11' 'libxcb' 'xdg-utils' 'webkitgtk-6.0')`，三份一字不差，每项在注释里写明对应哪个 P/Invoke 或外部命令。
2. **删掉 `gtk3`**（源码 0 引用；GirCore 用的是 Gtk-**4**.0 绑定，`webkitgtk-6.0` 会带 `gtk4`）与 **`zstd`**（产物自带 `Lib/linux-x64/libzstd.so`）。
3. `packaging/PKGBUILD`：source 改 `git+https://github.com/QiE2035/Lunarward.git#branch=main`，`url` 改本仓库，补 `pkgver()`；注释写明「要构建本地未推送的改动就换回 `git+file://${startdir}/..#branch=main`」。**（此条已被 D91 变更**：默认改回本地源，按远端 main 构建需 `LUNARWARD_SOURCE=remote`。）
4. `aur/lunarward`：source 改 `git+…#tag=v$pkgver`（git 源用 `sha256sums=('SKIP')` 才合规），注释写明「发布前必须先打 tag」；`aur/lunarward-git` 同步依赖。
5. `-p:Version` 由 `"${pkgver}"` 改 `"${pkgver%%.r*}"` —— `0.1.0.r123.gabc` 不是合法的 `AssemblyVersion`，git 版构建会因为版本串带字母而失败。
6. `packaging/.gitignore` 的 `lunarward/` 改 `/lunarward/`（只忽略 `packaging/` 根下的 makepkg 构建目录），并补 `/pkg/`、`/src/`、`/aur/*/lunarward*/`。
7. 新增 **`scripts/test/lint-pkgbuild.sh`**（秒级、不联网、不构建）：A = 扫源码里的 P/Invoke 库名与 `ProcessStartInfo` 的命令，经「库名 → Arch 包名」表反查三份 `depends` 有没有漏（表外的库报 WARN，提示补表）；B = 三份 `depends` 必须完全一致；C = `source` 与 `sha256sums` 自洽（git 源才允许 `SKIP`；`#tag=` 源要求 tag 真实存在）。

**代价**：

1. `webkitgtk-6.0` 进硬依赖后，装包体积多约 200MB（拉 `gtk4` + `javascriptcoregtk`）——不装它登录窗就打不开，D76 已经定了这条线。
2. `aur/lunarward` 依赖 tag 存在：**发版流程多一步** `git tag v$pkgver && git push origin v$pkgver`，忘了就构建失败（lint 会 WARN 提醒）。
3. lint 的「库名 → 包名」表要人工维护：源码引入新的 `DllImport` 时先报 WARN，得有人判断要不要进 `depends`。

**验证**（2026-11-18 实测）：`makepkg --printsrcinfo` 三份全部通过、`depends` 输出一致；`bash scripts/test/lint-pkgbuild.sh` 全 PASS（1 条 WARN 是「tag v$pkgver 还不存在」的预期提醒）。真实构建/安装未做（会装到系统 `/usr`，留给用户验收）。

## D87 运行器显示名：单一来源，重名才加来源

**背景**：用户 m02653「运行环境那个页面，问题不是卡，而是操作逻辑设计的不够好」。实机取证拿到两条硬证据（2026-11-18）：

- `--list-runners` 的真实输出里有两对**完全同名**的运行器：`spritz-wine-tkg-staging-wow64-11.19-1` 与 `spritz-wine-cachyos-wow64-10.0-11` 各出现两次，分别来自 `~/.local/share/sleepy-launcher/runners/` 与 `~/.local/share/anime-game-launcher/runners/`。在编辑器下拉里就是两行一模一样的字，选哪个全靠猜。
- `/opt/spritz-wine-tkg/bin/wine` 被命名成「系统 Wine」（PATH 扫描分支里硬编码的），而方案详情页走 `RuntimeProfile.RunnerName`，从路径猜出来是 `wine` —— **同一个文件两个名字**，用户会以为选错了。

**决定**（用户 m02813「这也算一个吧，先修一下提交」）：

1. **PATH 里那个 wine 按路径命名**：新增 `Features/Runtime/RunnerNaming.cs`（纯字符串、可单测）的 `WineDisplayName(path, exe)` —— 只有 `/usr/`、`/bin/`、`/sbin/` 下的才叫「系统 Wine」，其余取 `bin` 的上一级目录名；入参先解析符号链接（`/usr/local/bin/wine → /opt/x/bin/wine` 算 /opt 的）。
2. **重名才加来源后缀**：`WithOrigin(name, path)` / `Origin(path)` —— `~/.local/share/<x>/…` → x，`/opt/<x>/…` → x。`ComponentStore.GetRunners` 返回前统一过一遍，**不重名的名字保持原样**（不把「系统 Wine」写成「系统 Wine（系统包）」）。
3. **详情页优先用组件名**：`ResolveRunnerDisplay` 先查 `ComponentStore.TryGetCachedRunners`（只读缓存，**不在 UI 线程触发扫描**，L-127），命中就用 `DisplayName`（带版本，与下拉一字不差），找不到才回落 `RunnerName`。组件列表扫完后（`LoadComponentsAsync` 末尾）就地更新那个 `TextBlock`，不重建整页、不闪。
4. **CLI 名字列改 `Name`**：`--list-runners` 原来打 `DisplayName` 又单独打一列版本，同一个版本号显示两遍。

**代价**：

- `Component.Name` 从 `init` 改成 `set`（重名消歧要后处理）。
- 首次进详情页时组件缓存可能还没就绪，会短暂显示配置里的旧名，随后自动刷成 `DisplayName`。
- 重名项名字变长（`x（anime-game-launcher）`），列宽要容得下。

**验证**：`--list-runners` 实际输出 6 条，`/opt/spritz-wine-tkg/bin/wine` → `spritz-wine-tkg`，两对重名分别带 `（sleepy-launcher）` / `（anime-game-launcher）`；截图确认详情页与编辑器下拉显示同一个名字；`ComponentNamingTests` 12 例；单测 264/0。

---

## D86 卡顿与「每次重启都重来」的根因修复：注册表改读文件、一次性提示持久化

**背景**：用户 m02315 反馈「整个启动器用起来很怪，很多细节没有 Windows 原版丝滑」，并点名「每次打开启动器都会弹一次自动搜索结果，Windows 只会首次启动弹」。取证得到四类问题：

- **A 卡**：上游 `src/Starward/` 的同步 API（`SetGenshinEnableHDR`、改画质、截图、HDR 对话框）被我们在 Linux 侧用 `GetAwaiter().GetResult()` 包住「fork wine 查注册表」的异步实现 —— `Starward_261117.log` 有 5 次 `wine reg query "HKCU\Software\miHoYo\原神" 耗时 5411/4144/4129/4109/4128 ms`，UI 线程整窗冻住（违反自家 L-127）。
- **B 一次性语义错**：三处提示用 `private static bool` 控制，进程一重启就复位 —— 自动识别（`_scanTipShown`）、Wayland 热键（`_hotkeyHintShown`）、系统代理（连标记都没有）。
- **C 数据一致性**：绑定指向已删方案时每次启动刷一条 `Log.Warning`。
- **D 日志**：`App.xaml.cs` 留着 `[dbg] scan start/done` 插桩，且「识别到 N 个已安装游戏」的 N 取 `ScanAndApply()` 返回值 —— 缓存命中时恒为 0，日志永远显示「识别到 0 个」。

**决定**（用户 m02475「先按你的建议改一版吧」）：

1. **读注册表改读文件**：新增 `Features/GameAccount/WineRegistryFiles.cs`，直接解析 `<prefix>/user.reg`（HKCU/HKU）与 `system.reg`（HKLM/HKCR）。命中就返回，不 fork；**给不出结论**（文件不存在 / 文件里没有这个键 / 根键不支持 / IO 失败）返回 `null`，`WineRegistry.GetKeyValuesAsync` 再回落 `wine reg query`。文件里「没有这个键」也回落是有意的：wine 可能还没把内存改动落盘，这时 fork 一次才准。
2. **一次性提示持久化**：新增 `Helpers/OneShotHints.cs`，`TryTake(name)` 读写 SQLite `Setting` 表的 `hint_<name>`；三处提示改为 `auto-scan` / `hotkey-wayland` / `system-proxy`。数据库不可用时退化成「每进程一次」。
3. **失效绑定只警告一次**（`profile-binding-dangling`）；回落行为本身不变，修复入口仍在「运行环境 → 方案」页的提示卡。
4. **日志**：删 `[dbg] scan start/done`，「识别到 N 个」改用 `GameInstallScanner.KnownGames.Count`。

**实测数字**（`/tmp` 探针直接用生产源码对真实 `~/.wine/user.reg` 跑，`reg query` 对照在同一台机器）：

| 路径 | `HKCU\Software\miHoYo\原神` 耗时 |
| --- | --- |
| 文件解析（首次，52 个值） | **91–115 ms** |
| `wine reg query`（wineserver 已热） | **1843 ms** |
| `wine reg query`（冷，261117 日志） | **4128–5411 ms** |

**顺带修好的两处**（都是文件版更正确）：

- `reg query` 输出里的长 `REG_BINARY` 被 `RegistryFormat.ParseLine` 只取首行（截断）；文件版按折行拼全 —— 实测 `SDK_LANG_FILE_zh-cn_0_h2597224953` 的 54,179 字节完整解出。
- `reg query` 输出中文键名是本地代码页、会乱码（实测打印成 `ԭ��`）；文件是 UTF-8 + `\x539f` 转义，解码正确。

**代价/风险**：①文件可能滞后于 wine 内存（wineserver 未落盘），所以「文件里没有这个键」一律回落 fork，宁可慢一次也不给错结论；②缓存 TTL 仍 5 分钟，与改造前一致，陈旧窗口没有变大；③首次调用 ~95 ms 仍发生在 UI 线程上（同步壳无法异步化），但已从「冻 4 秒」降到「几乎不可感知」。

**验证**：单测 **252 / 0**（新增 `WineRegistryFilesTests` 11 例：`\xHHHH` 解码、折行拼接、四种值类型、hive 分流、缺键/缺文件的回落信号）；探针实测见上表；真实配置连续 4 次启动，日志均为 `[scan] 识别到 3 个已安装游戏`、无 `[dbg]`、**无任何 `WineRegistry` 慢调用警告**、`Setting` 表 `hint_auto-scan = True` 只写一次（第二、三、四次启动不再弹）。

---

## D85 运行环境界面重排：方案为主体的 master-detail，增强并入方案详情

**背景**：D80–D84 把功能补齐后，页面仍然是「五个页签 + 一堆平铺卡片」：方案卡是一段 8–10 行的日志（状态、绑定、图形、前缀、上次启动、六个同权重的按钮），统计卡是死数字点不动，「增强」作为一个页签却要求先在下拉里挑方案，编辑器整页覆盖导致返回后选中态丢失。

**决定**（用户 m02022「按你推荐来，直接全做完」）：

1. **方案页做 master-detail**：左列 280px 方案列表（健康点 + 名字 + 「可用 · 被 N 个游戏绑定」），右侧详情分五段卡片：运行器 / 图形层（DXVK、VKD3D 各一行 + 与前缀实际比对 + 「同步到前缀」）/ 前缀（路径、占用、锁定、默认目录说明）/ 增强（一行摘要 + 「配置」）/ 最近启动（3 条）。删除、克隆、配置文本、调试 Shell 收进 `⋯` 菜单，头号动作只留「运行一次」。
2. **取消「增强」页签**：它是方案级启动选项，改为从方案详情的「配置」进入，且不再需要先选方案（D82 的下拉因此删除）。页签收敛为四个：方案 / 运行器 / 组件 / 前缀。
3. **顶部 chip 条**：`N 个方案 · 已绑定 N 个游戏 · 运行器 a/b 可用 · 图形组件 N · 前缀 N · 占用`，每个 chip 点击跳到对应页签，替掉原来四张死统计卡。
4. **编辑器就地三态**：右栏在「详情 / 编辑器 / 增强」之间切换（`Visibility` 互斥），左列与 chip 条常驻；返回即回详情并保持选中。
5. **组件页收敛**：主按钮「下载组件」，其余（导入归档、导入图形、组件源、镜像测速、下载暂存、清理未使用）收进「更多」菜单；加类别筛选（全部 / DXVK / VKD3D-Proton）。
6. **运行器页**：按来源分组（外部运行器 / 系统 / Steam / 其它启动器），保留「添加路径…」，新增「设为新方案默认」（写 `runtime-defaults.json`，`RuntimeProfileStore.CreateDefault` 读它）。
7. **前缀页可见性**：占用条（相对最大值）、所属方案名单、无主前缀标「未使用」；「N 个方案在用」改用 `EnvironmentResolver.EffectivePrefixPath` 比较（空 `PrefixPath` 的方案此前永远算不到）。
8. **组件身份匹配**：编辑器按 `Id` / `Path` / 目录前缀 / 末两段四路比对组件（运行器的 `Id` 常是安装目录、`Path` 是 `bin/wine`，只比路径会静默落回第一项，把用户的选择改掉）。

**代价**：`RuntimeEnvironmentSetting` 拆成 4 个 partial（主文件 + `.Profiles.cs` + `.Components.cs` + `.Enhancements.cs`）；`ListView` 仍不用（Uno 在星号行 + ScrollViewer 里会抽风），左列是 `StackPanel` + `Button` 手写选中态；页签继续自绘（L-153）。

**验证**：`dotnet build` Debug/Release 0 错误；单测 241 / 0；`--selftest-ui` 11 / 0；`cli-smoke` 40/0/1、`cli-extra` 32/0；`--page runtimeenv` 在临时配置目录下逐页截图：方案页（chip 条、失效绑定卡、左列两行、右栏五段）、编辑器就地打开（DXVK 落到选中项）、增强就地打开（HUD/MangoHUD/gamemode/gamescope/预设/HDR/VRR）、组件页（主按钮 + 更多 + 筛选）、前缀页（占用条 + 所属方案）、运行器页（分组 + 设为默认）。

---

## D84 运行环境可用性打磨：一致性提示、清理未使用、共用提示、启动历史、预设与键盘可达

- **确认方式**：明确（用户 m01742「直接把所有优化都做完吧」，指上一轮交付的 A–I 建议表）
- **背景**：D80–D83 把页面结构、增强开关与图形层关系修好了，但「选了组件、前缀里其实是另一版」这类不一致仍然只能靠人肉比对；组件页留着一堆没人用的受管组件；多个游戏共用一份方案时用户不知道自己会被抢锁；「这次为什么没起来」只存在于日志里，界面上看不到。
- **决定**（九项，A–I）：
  1. **A 一致性同步**：方案卡片调 `DescribeGraphicsSync` 比对「方案里选的图形组件」与「前缀里 `.lunarward/graphics.json` 记录的组件」，不一致时给一行说明 + 「同步到前缀」按钮（`EnsureInstalled` 重装，不改方案）。
  2. **B 清理未使用**：组件页工具条加「清理未使用」，只处理 `ComponentStore.ManagedRootOf != null`（本程序管理目录下）且没有任何方案通过 `RunnerId/DxvkId/Vkd3dId/RunnerPath` 引用的组件；外部目录（系统 / Steam / 用户自己的）一律不列不删。
  3. **C 共用提示**：`ProfileBinding.Usages(profile.Id).Count > 1` 时在卡片上写「被 N 个游戏共用同一个前缀，同时启动会抢锁失败」，并给「为某个游戏克隆一份」（`CopyProfile(clearPrefix: true)` + 重新 `Set` 绑定）。
  4. **D 常用变量**：编辑器「高级」里的环境变量框上方加「插入常用变量」下拉（8 条 `DXVK_HUD` / `MANGOHUD` / `WINEDLLOVERRIDES` / `PROTON_ENABLE_NVAPI` / `mesa_glthread` / `WINEESYNC` / `WINEFSYNC` / `DXVK_STATE_CACHE`），按 `KEY=` 查重后追加。
  5. **E 启动历史**：新增 `Features/Runtime/LaunchHistoryStore`（`$XDG_DATA_HOME/lunarward/logs/launch-history.jsonl`，一次启动一行，超 400 行压到 200 行），`GameLauncherService` 在「预检拦下 / 已启动 / 进程没起来」三处各记一条（含耗时与失败明细）；卡片显示最近 3 条，没有历史时回落 `LastKnownGoodStore` 的「上次启动成功」。
  6. **F 键盘可达**：`TabStrip.KeyDown` 支持 `Left/Right/Home/End` 切页签并移动焦点（L-153 把 `Pivot` 换成自绘页签后丢了键盘导航，这里补回来）。
  7. **G 状态灯**：沿用 D73 §7.1 的红黄绿灯（本项已实现，仅确认文案含原因）。
  8. **H 删方案可选删前缀**：删除对话框加「同时删除前缀目录（路径）」勾选，**默认不勾**、前缀不存在时禁用；勾选后先 `Directory.Delete(prefixPath, recursive: true)` 再删方案文件。
  9. **I 预设**：增强页加「HUD 预设」「分辨率预设」两个下拉与 `HDR（--hdr-enabled）`、`VRR（--adaptive-sync）` 两个开关；`Enhancements.ToggleArg(args, flag, on)` 负责增删参数 token（保序、空串回落 `-f`）。
- **代价**：
  - 启动历史是**追加写**的旁路记录，与 `logs/launch/*.json` 启动报告并存（报告只在真起来时写，历史三条路径都写）；不记录退出码与真实游玩时长（拿不到，只能记「进界到进程起来」的耗时）。
  - 「清理未使用」只认本程序管理目录，用户放在别处的组件不会被清理（这是刻意的：那些目录不归我们管）。
  - 一致性同步会真实改写前缀里的 dll，因此只在用户点按钮时做，不自动。
- **验证**：`dotnet build` Debug/Release 0 错误；单测 **241 通过 / 0 失败**（新增 `LaunchHistoryStoreTests` 8 项、`EnhancementsTests` 6 项）；`--selftest-ui` 11/0；`cli-smoke` 40/0/1、`cli-extra` 32/0；真机截图逐项确认 A（前缀和方案对不上 + 同步到前缀）、B（清理未使用列出唯一无人引用的受管 DXVK）、C（共用提示 + 克隆按钮）、E（最近启动两行）、H（前缀勾选默认不勾）、I（HUD 预设 / 分辨率预设 / HDR / VRR）、F（点概览后连按两次 `Right` 切到组件页）、D（插入 `MANGOHUD=1`）。
- **受影响文档**：`12`（运行环境行、变更历史）、`13`（L-158）、`19`（L1 计数）、`23`（§13.11）、`README`、`CHANGELOG`。

## D83 图形层拆成两个字段：DXVK 与 VKD3D-Proton 各自独立（schema 4）

**背景**：用户 m01503 指出「dxvk 和 vkd3d 应该是可以同时安装的关系，用下拉框单选不合理」。核对机制：DXVK 负责 d3d9/10/11 → Vulkan（落 `system32/d3d11.dll`、`d3d10core.dll`、`dxgi.dll`、`d3d9.dll`），VKD3D-Proton 负责 d3d12 → Vulkan（落 `d3d12.dll`、`d3d12core.dll`）；两组文件名互不重叠，**同一个前缀可以同时装、也应该同时装**（玩原神要 d3d11、玩绝区零要 d3d12）。此前模型只有单个 `GraphicsId`，UI 只能单选，另一个只能手改 JSON。

**决定**：

1. **模型拆字段**：`RuntimeProfile.GraphicsId` 删除，改为 `DxvkId` + `Vkd3dId`（各可空），`SchemaVersion` 2/3 → **4**；派生 `[JsonIgnore] bool HasGraphics`。旧 JSON 里的 `GraphicsId` 会被 `System.Text.Json` 忽略，方案需要重新选一次图形层——**开发阶段不做迁移（用户 m01589 定调）**。
2. **移除全部旧迁移代码**：`RuntimeProfile.LegacyPrefixMode/LegacyEnsureGraphics/LegacyRunnerType/LegacyEnvironment` + `MigrateFromLegacy()`、`RuntimeProfileStore.MigrateFromLegacyFolder()` 与其调用、`LegacyProfilesFolder`；`MigrateLegacy()` 降级为 `Normalize()`（只做前缀展开 + 运行器解析）。同时删掉 `LaunchRecipeTests` 里 5 个 `MigrateFromLegacy_*` 用例。理由：迁移代码保护的是「已发布用户的旧数据」，本项目还没发布，留着只会让每次改字段都要维护两条路径。
3. **前缀记录改成数组**：`<prefix>/.lunarward/graphics.json` 从「一个对象」变成「按 kind 各一条的数组」；`GraphicsInstaller` 提供 `GetAllInstalled`、`GetInstalled(prefix, kind)`、`EnsureInstalled(prefix, IEnumerable<Component>)`、`Verify()`（逐类核对，`GraphicsStatus.Items` + `IsHealthy(kind)` + `RecordOf(kind)`）、`Remove(prefix, kind?)`。换某一类版本时只删「这一类」旧记录里没被重写的文件，另一类不受影响；旧单对象格式读的时候当一条处理（4 行宽容解析，避免 dll 留在前缀里没人认领）。
4. **全链路跟随**：启动（`GameLauncherService` 把两个 id 一起装）、预检第 5 项（两类各查一遍，找不到分别报）、启动报告（`DxvkId`/`Vkd3dId` 两个字段）、编辑器（两个独立下拉，标签 `DXVK` / `VKD3D-Proton`）、`ProfileBinding.EnsureFor` 与「克隆」、组件删除的引用检查（两个 id 都算引用）、前缀行安装按钮（按前缀所属方案把两类一起装）。

**代价**：①旧方案打开后图形层是空的，要重新选（开发阶段可接受）；②`graphics.json` 格式不兼容，旧前缀里的单条记录只在读取时兼容，写入即变数组；③`GraphicsCombo` 仍按「一个下拉一类」渲染，没有做成「双选列表」的样子（两类各一行更贴近它们的真实关系：互不替代）。

**验证**（实测）：`dotnet build` Debug/Release 均 0 错误；编辑器截图确认「图形层 / DXVK（不选）/ VKD3D-Proton（不选）」两行并列；`--preflight hk4e_cn` 输出 `[Warning] 图形组件 未选择 DXVK/VKD3D…`；`--selftest-ui` 11/0；单测 **223 通过 / 0 失败**（224 − 删掉的 5 个旧迁移用例 + 4 个 `HasGraphics` 用例）；`cli-smoke` 40/0/1 跳过、`cli-extra` 32/0、两个 lint 全 PASS。

**顺带修的两处**：①L2 夹具原来靠 `profiles/` → `env/` 的目录迁移生效（`scripts/test/fixtures.sh` 写的是 `cfg/lunarward/profiles/`），迁移删掉后 4 个用例红，夹具改成直接写 `env/fakeprfl.json`（`SchemaVersion 4`、无 legacy 键）；②`--graphics-remove`（`kind == null` 全删路径）边遍历 `records` 边 `Remove` 抛 `Collection was modified` → 退出码 134，修成先拷贝再过滤（L-157）。

---

## D82 运行环境收口三件：页签自绘、图形层选择器合并、gamescope 参数体检

**背景**：D80 把运行环境做成一级页后，页面里用 `Pivot` 分页。实测（2026-11-16，本机 KDE Wayland + Uno Skia 6.7.45）**页签头收不到鼠标点击**：在「概览/运行器/组件/前缀/增强」头部单击、双击、130 ms 长按都不切换；同一位置的普通 `Button`（「配置文本」）却正常弹窗，方向键也能切页——说明不是注入问题，是 `PivotHeaderItem` 的命中区在 Skia 后端不工作。同时 D81 的编辑器只列 DXVK，VKD3D-Proton 选不到；gamescope 参数是自由文本，写错的代价是启动直接失败且没有提示。

**决定**：

1. **页签不用 `Pivot`**：`RuntimeEnvironmentSetting.xaml` 改成 `StackPanel`（5 个 `Button`，`Tag` 存序号）+ 5 个 `ScrollViewer` 用 `Visibility` 互斥；选中的按钮换成 `TabButtonSelectedStyle`（`ControlFillColorSecondaryBrush` + SemiBold），未选中 `TabButtonStyle`（透明 + 次要前景）。样式切换代替在代码里塞画刷，`ShowTab(int)` 一处收口。
2. **图形层选择器合并**：`GraphicsCombo` 参数从 `ComponentKind kind` 改成 `ComponentKind[] kinds`（当时编辑器传 `[Dxvk, Vkd3dProton]` 合在一个下拉里）。**此条已被 D83 取代**：两类拆成两个独立下拉（可以同时装，不是二选一）。
3. **gamescope 参数体检**（`Enhancements.ValidateGameScopeArgs`，纯函数）：shell 引号/重定向、`--` 之后会被交给游戏、`-W/-H` 与 `-w/-h` 必须成对。提示只出现在「增强」页状态行，**不阻断保存**（D46：先让人知道，别替人做决定）。
4. `UpdateContentWidth()` 的可见页判定从 4 个 `ScrollViewer` 扩到 5 个（新增「增强」页）。

**代价**：①页签是自己画的，键盘左右切换没有了（鼠标可用性优先；要补就得自己接 `KeyDown`）；②`Visibility` 互斥意味着 5 个 `ScrollViewer` 都存在内存里（每个页的条目数量级是十几到几十，可接受）；③参数体检只覆盖已知道的四类误解，未知参数仍原样传给 gamescope。

**验证**（实测）：`dotnet build Starward.slnx -c Debug` 0 错误；点击「增强」页签整页切换、选中态跟着移动（截图）；`游戏架构 64 位（x64）（前缀 win64）` 与增强开关在预检里各占一项；`EnhancementsTests` 24 组（含 4 组 `ValidateGameScopeArgs`）全绿，单测总数 224。

---

## D81 增强开关：HUD / gamemode / gamescope 成为方案字段（schema 3）

**背景**：这三样此前只能写进「包装命令」或环境变量的自由文本里（占位提示还写着 `mangohud --dlsym`）。对标 `an-anime-team`（`anime-launcher-sdk/src/config/schema_blanks/hud.rs`）的做法：HUD 是**枚举**而不是自由环境变量，且 gamescope 开着时不注入 MANGOHUD——两层监控叠在一起会出现重复帧数。

**决定**：

1. `RuntimeProfile.SchemaVersion` 2 → **3**，新增 `Hud`（`off|dxvk|mangohud`）、`HudOptions`、`GameMode`、`GameScope`、`GameScopeArgs` 五个字段；旧文件不迁移也能读（缺字段走默认值）。
2. 新增 `Features/Runtime/Enhancements`（纯函数）：`EnvironmentVariables()` 把 HUD 展开成 `DXVK_HUD`（默认 `fps,frametimes,gpuload`）或 `MANGOHUD=1` + `MANGOHUD_CONFIG`，**`GameScope` 开时跳过 MANGOHUD**；`Wrappers()` 顺序固定 `gamescope <args>` → `gamemoderun` → 用户自己的 Wrappers；`RequiredBinaries()` / `FindInPath()` / `Describe()` 供预检与状态行用。
3. `LaunchRecipeBuilder.Build` 里注入顺序：自动环境变量 → 增强展开 → 配方 `Env`，最后一步仍走 `MergeEnvironment`，所以配方里手写 `DXVK_HUD` 会覆盖增强并记进 `Overrides`（可观测）。
4. 预检新增第 11 项「增强开关」：开了但 PATH 里找不到 `gamescope`/`gamemoderun`/`mangohud` → Warning（gamescope 下不要求 mangohud），可一键跳到「运行环境 → 增强」。
5. 页面新增「增强」页签：选方案 → HUD 下拉 + 参数 + gamemode/gamescope 开关 + 参数 + 程序检查 + 最终命令行预览（`JoinCommandLine`），保存写回同一份 `env/<id>.json`。

**代价**：①schema 升版，旧的「包装命令」写法仍然有效（不会被自动改写，避免猜用户意图）；②`MANGOHUD_CONFIG` 与用户自己的 MangoHud 配置文件可能冲突（留空即用用户配置）；③gamescope 参数不做语义校验（只做 D82 的四类体检）。

**验证**（实测）：`EnhancementsTests` 20 组（HUD 展开、gamescope 避让、包装顺序、PATH 检查、`Build` 产出 `["gamemoderun","/usr/bin/wine",…]` + `DXVK_HUD`、配方覆盖进 `Overrides`）；`--launch-plan` 与真启动共用 `Build`，无需二次接线。

---

## D80 运行环境提为一级页：NavigationView footer、齿轮正上方

**背景**：运行环境原来挂在「启动器设置」里（`SettingPage` 的一个子导航项）。它跨游戏生效（一个方案可以被多个游戏绑定），放在某个游戏的设置域里既难找也容易误解；而且设置页的 `NavigationView` 本身带子导航，嵌套两层观感很碎。

**决定**：

1. `MainView.xaml` 增加 `<NavigationView.FooterMenuItems>`，只放一项「运行环境」（`Tag="RuntimeEnvironmentSetting"`，图标 `E7F8` 笔记本，`ToolTip` 绑 `Lang.Lunarward_RuntimeEnv_Title`）。footer 由控件画在齿轮正上方，与顶部「当前游戏域的六个页面」自然分开——顶部随游戏切换变，footer 不随游戏变。
2. `MainView.NavigateToRuntimeEnvironment()` 一处收口；`MainWindow.OpenRuntimeSettings()`（预检失败时的「去设置」）改走它，不再用 `SettingNavigation.SetTarget`；`--page runtimeenv` 深链也映射到同一页。
3. `MainView.NavigateTo` 的页面白名单（原本只放行 `SettingPage`）扩到这两个页；`--subpage RuntimeEnvironmentSetting` 的旧入口保留（`SettingPage.xaml.cs` 的 Tag 映射），只是界面上不再有那个子导航项。
4. `UiSelfTest` 增加第 11 条断言：从 footer 入口打开后 `CurrentPageType == RuntimeEnvironmentSetting` 且内容非空。
5. 页面内部结构：标题行（新建/刷新/打开配置文件夹）+ 五个页签（概览/运行器/组件/前缀/增强）+ 整页编辑器覆盖层；概览顶部四张统计卡（方案数 / 运行器可用数 / 前缀数与合计体积 / 图形层数），下面「方案 ↔ 哪些游戏在用」；**失效绑定提示卡**（`ProfileBinding.Dangling`）：绑定指向已删方案时给出「全部改绑到 X」与「清除这些绑定」两个动作——本机实测就有一例（`hk4e_cn`/`nap_cn` 指向已删除的 `c2d564e1`，一直在静默回落）。

**代价**：①左栏 footer 多一个图标（`LeftCompact` 下只有图标 + ToolTip）；②页面从 149 行涨到约 380 行 XAML，`RuntimeEnvironmentSetting.xaml.cs` 拆出 `.Enhancements.cs`；③`SettingPage` 里「设置页 10 项」的清单变成 9 项。

**验证**（实测）：footer 图标位于齿轮正上方并整页加载（截图 + 区域截图取证）；`--selftest-ui` 11 条全绿（含新断言，耗时 3116 ms）；`--page runtimeenv` 可直达。

---

## D79 命令行帮助只有一份来源：`Helpers/CliSpec.cs`

**背景**：`--help` 是一段贴在 `Program.Linux.cs` 里的手写字符串，和 46 个 `args[0] is "--x"` 分支各写一遍。同一批里就踩了两次：`--profile-ensure` 加完才发现帮助里没有；`--preflight` 的描述一直写着「六项」，实际已经是九项（现在是十项）。而 System.CommandLine 3.0.0 的扁平帮助会把「环境诊断 / 运行环境 / 组件与图形层 / 安装与更新」这种分组**挤成一长列**，可读性反而下降。

**决定**：

1. 新建 `src/Lunarward/Helpers/CliSpec.cs`：`CliSwitch(Name, Args, Description, Group)` 一条一开关，`Commands`（第一 token，57 条）+ `Modifiers`（跟在命令后面的值项，17 条）+ `Gui` 组（`--page`/`--subpage`/`--scroll`/`--viewer`/`--profile-editor`/`--window-action`）。`RenderHelp()` 按组渲染，`DisplayWidth()` 按终端列宽（CJK 记 2 列）对齐签名列。
2. `--help` 只调 `CliSpec.RenderHelp($"版本 {appVersion}")`；手写字符串整段删除。
3. 分发最前面调用 `CliSpec.TryRejectUnknown(args, …)`：只校验**第一 token**（位置参数是游戏自己的参数，`-dx11` 不能拦），未知时 exit 2 + 「最接近的是：--component-list」（Levenshtein ≤ 4）。原来的 guiFlags 白名单保留（它管「纯 GUI 开关放行」）。
4. **不引入 System.CommandLine 3.0.0**：换库的目的（帮助不漂移 + 严格校验 + 拼错提示）用一张表就达成了，而完整重写 1903 行的 `Program.Linux.cs` 会把「执行分支」和「声明」一起动，收益不成正比。表本身是「声明层」，将来要换库可机械转换。包已从 `Lunarward.csproj` 移除。
5. `scripts/test/lint-cli-help.sh` 卡三条不变量：源码里每个 `args[0] is "--x"` ⊆ 表；表里每条都出现在 `--help` 输出（给了 `LW_BIN` 才跑）；未知开关 exit 2 且带候选。

**代价**：①新增一个 lint 入口（约 0.3 s）；②`CliSpec.Commands` 与 `Program.Linux.cs` 的分支仍是两处，靠 lint 而不是类型系统保证一致；③未知开关从 exit 1（「未知命令或不完整」）改成 exit 2，脚本里若有依赖 1 的判断需要更新（本仓 `T-CL-24` 走的是「参数不足」分支，不受影响）。

**验证**（实测）：`--help` 输出 79 条全部命中（lint B）；`--componet-list` exit 2 并提示 `--component-list`（lint C/C2，L2 `T-CL-49`）；`--launch-plan /nonexistent/game.exe -dx11` 不被误判（`T-CL-50`）；`CliSpecTests` 14 组断言；`lint-ui-thread.sh` 与 `cli-smoke 40 / cli-extra 32` 全绿。

---

## D78 依赖批次：补两个真缺口，两个「看着能换」的不换

**背景**：24 号盘点给出「值得换 3 个 / 收益一般 3 个 / 不建议 4 类」。逐条落地后结论有变化，记在这里（D46：结论要有论据，推翻要留档）。

**决定**：

1. **采纳 ValveKeyValue 0.80.0.542** —— 补 `SteamLibrary`：解析 `steamapps/libraryfolders.vdf`（新旧两种格式），把每个库的 `compatibilitytools.d`（Proton）与 `steamapps/common`（GE-Proton 放在库里）都纳入扫描。原来 `ComponentStore.SteamCompatRoots()` 硬编码 4 条默认路径，**Steam 装在自定义库的机器上一个 Proton 都扫不到**。
2. **采纳 PeNet 6.1.2** —— 预检「游戏架构」项：读 PE machine（`I386`/`Amd64`/`Arm64`），与方案的前缀架构比对。x64 exe + win32 前缀 → Failed（Wine 直接 `BAD EXE FORMAT`）；x86 exe + win64 前缀 → Warning（WoW64 能跑但可能缺 32 位库）；ARM64 → Failed（需要 Windows on ARM）。
3. **SharpHook 8.0.2 不换**（spike 实测否决）：本机 KDE Wayland（`XDG_SESSION_TYPE=wayland`，`DISPLAY=:0`）下 `TaskPoolGlobalHook` 打印 enabled 但 12 秒内 0 个按键事件——`xdotool key`（XTEST）收不到，`env -u WAYLAND_DISPLAY` 强制 X11 后端也收不到；对照实验自写 `XQueryKeymap` 轮询能看到 `xdotool keydown shift`。说明 XTEST 事件对 X 客户端可见，问题在 libuiohook/XRecord 这条路上，**不是我们的用法**。结论：保留 `X11KeyState` 轮询 + `--show` 绑系统快捷键。Wayland 全局热键的正解是 portal——本机 `busctl --user introspect` 确认 KDE 暴露 `org.freedesktop.portal.GlobalShortcuts`，列为后续项（用现有 Tmds.DBus 接）。
4. **Tmds.DBus 不迁 Tmds.DBus.Protocol**：涉及 `Helpers/Portal.cs`（4 个 `[DBusInterface]`，含 FD 与请求句柄）、`Helpers/DesktopNotification.cs`、`Features/Tray/StatusNotifierTray.cs` 共 921 行，Protocol 是源生成器模型（`AddMethodHandler` / `GetMessageWriter`，没有 `CreateProxy`），而托盘/通知/portal 三条用户可见链路**没有自动化覆盖**。前置条件写下来：先有这三条的集成断言，再谈迁移。

**代价**：①两个新包（ValveKeyValue 907 KB、PeNet 846 KB，均只进主程序，不进测试时占位）；②`SteamLibrary` 只读 `libraryfolders.vdf`，不解析 `appmanifest_*.acf`（还没需求，D46：先不做没需求的东西）；③预检从 9 项变 10 项（后来又加「增强开关」，现共 11 项），弹窗更长。

**验证**（实测）：`SteamLibraryTests` 8 组（新/旧格式 VDF、尾斜杠去重、坏文本、多库合并、`CompatRoots`、`IsProtonFolder`）、`PeArchitectureTests` 24 组（含自拼最小 PE32/PE32+ 断言 `X86`/`X64`）；`dotnet test` 200 通过；预检项数 10（`grep 'Name = "'` 去重核对）。

## D77 运行环境收尾：删除有边界、身份有守卫、首次使用自己建环境

**背景**：23 号里四处写着「已实现」，逐条对账后实际对不上：受管组件没有删除入口（A6）；root 不拒跑（B5）；删环境不检查引用（C3）；游戏目录自带图形 dll 不提示（D2）；§5「首次启动自动建同名环境」的真实行为是**回落到第一份环境**。另有 §3.5 把前缀元数据写成 `<prefix>/lunarward.toml`，实际落地一直是 `.lunarward/prefix.json` + `graphics.json`。

**决定**：

1. **删环境**（C3）：先 `ProfileBinding.Usages(id)` 列引用者 → 确认框逐行写游戏名 + 「前缀目录不动」→ 删 `env/<id>.json` 后 `ClearFor(id)` 清绑定。理由：D74 之后前缀只是路径，删环境 ≠ 删数据，但「绑定指向已删环境」会静默回落（L-62），必须一起清。
2. **删组件**（A6）：只对**本程序管理的目录**开放（`ComponentStore.ManagedRootOf` → `TryDeleteManaged`）。可删范围严格限定为 `components/<分类>/<目录>` 这一层，`components/wine`、`components/dxvk` 这类分类目录本身先被判掉（`IsManagedFolder` 返回 false）——删掉就是整个运行器库没了。系统 / Steam / 手动发现的组件是**引用**，UI 上不给删除按钮。
3. **root 拒跑**（B5）：`Helpers/PosixIdentity`（`geteuid` P/Invoke，失败退回 `Environment.UserName == "root"`）→ 预检第一项「运行身份」Failed（可读文案：root 会在前缀里写出普通用户改不动的文件）；`PrefixManager.EnsurePrefix` / `CreateWithWineboot` 直接 `ThrowIfRoot`。
4. **首次使用自动建环境**（§5）：`ProfileBinding.EnsureFor(biz)` 四级决策——①已有有效绑定→直用；②有同名环境→只补绑定；③全库只有一份且没被任何游戏占用→**就地改名**（不制造近似重复）；④否则复制一份（沿用运行器/图形层/env/args，`PrefixPath` 留空 → 各用自己的默认前缀，避免两个游戏抢同一把锁）。真入口在 `GameLauncherService`，读侧入口 `--profile-ensure`。
5. **游戏目录自带 dll**（D2）：预检查 `d3d11.dll` / `dxgi.dll` / `d3d12.dll` / `d3d10core.dll`，命中给 Warning + 「打开游戏目录」。故意不查 `d3d9.dll` / `vulkan-1.dll`：老游戏自带极普遍，提示会成为噪音。
6. **§3.5 改文档不改代码**：以 `.lunarward/prefix.json`（`GameKey/ProfileId/PrefixArch/CreatedAt`）与 `.lunarward/graphics.json` 为准；`lunarward.toml` 从未实现，从设计文档里删掉。

**代价 / 边界**：①删除不可撤销（弹窗写明磁盘路径，且只覆盖我们自己管理的目录）；②容器里 uid 0 也会被拒跑，这是有意的；③`EnsureFor` 在「唯一环境已被别游戏占用」时会复制，用户可能看到两份相似环境（名字带游戏名，可自己合并）。

**验证**：`--profile-ensure` 手工三分支（首次 `created: True` / 二次 `False` / 换游戏新建且 `prefix: (启动时创建)`）；L2 `T-CL-45/46/47`；单测 145 条（新增 `ComponentSources.ProbeUrl` 3 条）；`lint-ui-thread.sh` 通过；Debug/Release 均 0 错误。

---

## D75 外部能力先走 portal，视觉上「弹层必须实底」

**背景**：两件事在同一批里暴露出来。①24 号盘点发现打开链接、选文件、截图、桌面通知都是 fork 外部命令（`xdg-open` / `kdialog` / `spectacle` / `notify-send`），在 flatpak 沙盒与纯 Wayland 会话里会静默失败；②Skia 后端没有系统模糊，`AcrylicBrush` 退化成半透明灰，对话框里壁纸文字直接透上来，浅色壁纸区基本读不清。

**决定**：

1. 打开链接、选文件、桌面通知 → **xdg-desktop-portal 优先**（`OpenURI` / `FileChooser` / `org.freedesktop.Notifications`），外部命令只做兜底；全部是 D-Bus 异步往返，不 fork。
2. 截图 → **外部工具优先**（spectacle / grim / gnome-screenshot），portal `Screenshot` 兜底。理由：静默全屏是热键体验的核心，portal 在部分桌面会弹授权/选区。
3. 弹层与对话框统一用不透明表面 `LunarwardSurfaceBrush`（Dark `#FF202020` / Light `#FFF3F3F3`），覆盖 `ContentDialog`、`FlyoutPresenter`、`MenuFlyoutPresenterBackground`、`GameLauncherSettingDialog`；叠在应用背景上的页面类面板保留半透明。
4. AVIF / JXL / HEIC 编解码交给 `Magick.NET-Q8-AnyCPU`（Skia 无对应 delegate），统一入口 `Helpers.ImageDecode`；登录窗部分**已被 D76 取代**（当时误判目标环境没有 `webkitgtk-6.0`）。

**代价**：①AppImage / 安装体积增加约 40 MB（Magick 的 linux-x64 native，publish 只留本机 RID）；②portal 调用失败时多一跳兜底，日志里会看到两条记录；③portal 的 `FileChooser` 对话框外观由桌面决定，和 kdialog 不完全一致。

**验证**（区分实测与待验收，D46）：

- 实测通过：`--image-info` 对 `.avif` / `.jxl` 报「Skia 否 / Magick 是」并给出尺寸（64×48）；Magick 往返编码 Avif 296 B / Jxl 85 B / WebP 82 B；`cli-extra` T-CL-44 覆盖 `--image-info` 失败路径（rc=1 + 「找不到文件」）；游戏设置对话框已是实底（真机截图）；游戏选择层展开时按 Esc 只收层、进程仍在（真机核对）；本机 portal 版本 `FileChooser 4 / OpenURI 5 / Screenshot 2 / Notification 1`，通知服务 `Plasma 6.8.2`。
- **待验收**：应用内「打开链接 / 选文件 / 保存 / 选目录」四条走 portal 的实际弹窗（本机 portal 可用，但未逐条点击核对）；`FlyoutPresenter` / `MenuFlyout` 的实底（同一主题资源，只核对了 `ContentDialog` 与游戏设置对话框）；桌面通知在 KDE 上的实际显示。

## D93 上游历史与 `main` 连通：`-s ours` 合并提交，树零变化

**背景**：用户问「上游有没有更新」。核对结果：上游 `main` 停在 `df8ae07`（`filecache powered by fusioncache`，2026-10-12 02:43 +0800），距上次提交（2026-08-30）已 40 天没有新提交；最新 tag `0.18.2` = `ac7c9c4`，在基线前 2 条。核对同时暴露两个结构问题：①本地仓库是 shallow 的，`df8ae07` 被 graft 成根提交（`git rev-list --count upstream/main` 只有 1）；②`main` 是一条**独立历史**（首提交 `5bf286d` 为整体导入），与上游**没有共同祖先** —— 文档里写的 `git fetch upstream && git merge upstream/main` 实际上无法三方合并，只能走 unrelated-histories 硬合，风险是把已删除的上游 Windows CI 文件重新带回来。

**决定**：

1. `git fetch --unshallow upstream` 补齐上游完整历史：1320 条提交，`.git/shallow` 移除，`.git` 56 MB → 67 MB。本机 DNS 解析不了 `github.com`（`getent` / `dig` 超时，其它站点正常），fetch 走镜像：`git -c url."https://gh-proxy.com/https://github.com/".insteadOf="https://github.com/" fetch --unshallow --no-tags upstream`。
2. 合并前先证明内容已对齐，不靠假设：`git diff df8ae07 main -- src/Starward` **0 文件**；全树 `--name-status` = **859 新增 / 6 删除 / 17 修改**（删除的是 `.github/workflows/*` 六条上游 Windows 流水线，按 D45 不维护；17 个修改是 `.gitignore`、`README.md`、`Starward.slnx`、`global.json` 与 `Starward.Language` 的 13 个资源/Designer——均为移植改动），没有任何「上游已改而 `main` 没跟」的文件。
3. 用 `-s ours` + `--allow-unrelated-histories` 建合并提交 `542458e`（父 `bf775c5`、`df8ae07`）：树与合并前**逐字节相同**，只把上游 1320 条历史接入祖先链。选 `-s ours` 而不是普通合并：普通合并以两棵无关树为输入，会把第 2 条里那 6 个已删工作流当成「上游新增」重新带回，且没有内容收益。
4. 同步流程回到文档里的形状：`git fetch upstream && git merge upstream/main`，merge base = `df8ae07`，之后就是普通三方合并。

**代价 / 边界**：①合并提交是**空 diff**（`git show --stat 542458e` 无文件），看 log 时不要误读为「合了却没改」；内容等价性由第 2 条保证；②`upstream-base` 分支与 `upstream/main` 同点，已冗余，保留作基线标记；③合并后 `main` 领先上游 118 条（117 条移植提交 + 1 条合并），推 `origin` 会把上游 1320 条历史一起发布，属独立决定，未执行；④shallow 状态下祖先判断不可用（`git merge-base` 无输出），判断内容要对**树**做 diff（`git diff <上游> main`），不要依赖 `git log <上游>..main`。

**验证**（实测，2026-11-21）：

- `git merge-base main upstream/main` → `df8ae07`；`git merge-base --is-ancestor upstream/main main` 通过。
- `git diff bf775c5 HEAD` 输出为空（0 行）；`git show --stat 542458e` 无文件变更。
- `git fsck --connectivity-only` 通过；`git rev-list --count main` = 1438（117 + 1320 + 1）。
- `git rev-list --left-right --count upstream/main...main` = `0 118`（落后 0）。
- `origin/main` 仍为 `bf775c5`，本地领先 1321 条（未推送）。

**确认方式**：明确（m00077「那就把历史连接起来」；m00113「按你说的更新下文档吧」）。

## D94 缓存页面的生命周期按「挂载次数」算，不按实例算

**背景**：用户报「开始/安装游戏按钮的状态」与下面「已安装？定位游戏 / 可移动存储未连接」提示不刷新（m00257）。复现路径：当前游戏是未安装的星布谷地（显示「安装游戏」+ 定位提示），点左上角切回已安装的原神后，按钮仍是「安装游戏」、提示仍在、游玩时长残留上一个游戏的 0h 9m。

两处「实现错位」叠加：

1. **切游戏**：`MainView.GameSelector_CurrentGameChanged` 先 `UpdateNavigationView()`（其中 `NavigateTo(SourcePageType)` 把新 `CurrentGameId` 当参数导航），再 `Send(CurrentGameChangedMessage)`。由于 `GameInfo : GameId`，`PageBase.OnNavigatedTo` 的 `is GameId` 分支已把页面的 `CurrentGameId` 改成新游戏；随后消息处理里的守卫 `if (CurrentGameId == message.GameId) return;` 直接返回，`CheckGameVersion()` 永不执行 —— 按钮状态、定位提示、游玩时长全留在上一个游戏。
2. **离开页再回来**：上游页面**从不缓存**（全仓 `NavigationCacheMode` 0 处、`CurrentGameChangedMessage` 0 处引用），每次导航新建实例，`OnLoaded` 就是完整初始化。移植侧 D60/D62 给 8 个页面加了 `NavigationCacheMode="Required"`，而 `PageBase` 沿用上游写法在**第一次** `Unloaded` 就 `Loaded -= PageEx_Loaded` —— 缓存实例第二次挂载时 `OnLoaded` 不再执行：消息不再注册、安装任务看门狗不再启动、`CheckGameVersion()` 不再跑。即离开过启动器页之后，本会话内该页彻底不再刷新。D62 自己就踩过同一类坑（banner 的 `_contentGameId != CurrentGameId` 永假，`00-decisions.md` 原 D62 条目）。

**决定**（用户 m00695：「一切冲突、偏离点等都以上游为主要目标」）：

1. `src/Lunarward/Frameworks/PageBase.cs`：`Loaded/Unloaded` 不再在第一次 Unloaded 退订；每次 `Loaded` 都跑 `OnLoaded()`；新增 `protected string? ContentGameId` 记录「页面当前渲染的是哪个游戏」，`OnNavigatedTo` 里参数指向别的游戏时更新并调用新增的 `protected virtual void OnGameChanged()`。
2. `src/Lunarward/Features/GameLauncher/GameLauncherPage.xaml.cs`：把整套「与游戏相关」的初始化抽成 `LoadGameContent()`（重置安装路径/可移动提示/预下载/DX12/区服/安装任务缓存/云游戏/账号切换器/背景，刷新 `PlayTimeButton` 与 banner，再 `InitializeGameFeature() + CheckGameVersion() + UpdateGameInstallTask() + CheckCloudGame() + InitializeGameServerAsync() + InitializeBackgameImageSwitcherAsync()`），`OnLoaded`、`OnGameChanged()`、`CurrentGameChangedMessage` 三处共用同一入口；`OnCurrentGameChanged` 守卫改为对比 `ContentGameId`；`OnLoaded` 里对 `_dispatchTimer.Tick` 用 `-= +=` 接回（第一次 Unloaded 会摘掉）。`LoadGameContent()` 整段 try/catch 记日志，避免一处异常吞掉整页刷新。
3. `src/Lunarward/Features/PlayTime/PlayTimeButton.xaml.cs`：新增 `RefreshPlayTime(GameBiz)`。`CurrentGameBiz` 是 `x:Bind` 默认 OneTime，控件不重建就读不到新值；缓存页面切游戏后必须显式刷新（实测不刷时显示 0h 0m，正确值 0h 9m）。
4. `src/Lunarward/Features/Setting/SettingPage.xaml.cs`：`LanguageChangedMessage` 的注册从 ctor 移到 `OnLoaded`。ctor 只跑一次 + `OnUnloaded` 里的 `UnregisterAll` 会让语言切换在第一次离开设置页后永久失效。

**代价 / 边界**：①`OnLoaded` 从「每实例一次」变成「每次挂载一次」，其中的订阅与计时器必须幂等（本次对 `_dispatchTimer.Tick` 做了 `-= +=`；消息注册走 `WeakReferenceMessenger` 的重复注册覆盖语义，安全）；②`LoadGameContent()` 每次挂载会多做一次游玩时长本地读取与一次 `CheckGameVersion()`（含网络版本查询，失败走既有兜底），开销在既有导航耗时（266–342 ms）量级内；③`ContentGameId` 只解决「游戏身份变化」，页面其它跨实例状态仍按上游语义处理；④上游页面永不缓存，所以本决策只影响移植侧 8 个缓存页，`src/Starward/` 零改动。

**验证**（实测，2026-11-21）：

- `dotnet build src/Lunarward/Lunarward.csproj -c Debug` 0 错误；`dotnet test Starward.slnx -c Debug` 264 通过 0 失败；`bash scripts/test/lint-ui-thread.sh` 通过。
- 场景 A（未安装 → 已安装）：DB 置 `CurrentGameBiz=hkrpg_cn`（未安装）启动，`LUNARWARD_DEV_SWITCH_GAME=hk4e_cn` 6 秒后走真实选服路径切到原神 → 按钮变「开始游戏」、banner 换成原神、游玩时长 0h 9m（修复前分别停留在「安装游戏」/旧 banner/0h 0m）。
- 场景 B（离开再回来再切未安装游戏）：原神启动器页 → 游戏设置页 → 切回启动器（缓存实例）→ 选择层切到未安装的星布谷地 → 显示「安装游戏」+「已安装？定位游戏」+ 0h 0m（修复前该页在第一次离开后不再刷新）。
- 现场清理：验证实例均已 `pkill -x Lunarward`；DB 已从 `/tmp/StarwardDatabase.db.bak` 还原。

**确认方式**：明确（m00257 报告症状；m00695 批准方案与「以上游为准」原则）。

**遗留观察（未修，另案）**：GUI 自动化过程中，游戏选择层（`GameSelector` 全背景层）在切换游戏后会停留在画面上层不消失（截图叠在启动器/设置页之上，层级半透明、内容静止）。它与本决策的状态刷新无关（层下页面内容正确），未在本轮定位；另记：导航栏对「已选中项」点击直接 return（`MainView.NavigationView_ItemInvoked` 的 `if (args.InvokedItemContainer?.IsSelected ?? false) return;`），因此停在设置页时点「启动器」无反应，需先点其它项。

## D95 审计项修复：批 1「安装 / 更新 / 预下载」按上游语义补齐

**背景**：26 号审计落地后，用户授权按模块修复全部成立项（m01511「可以，全处理了吧」）。批 1 覆盖 B01 B02 B03 B04 B09 B36 B37 B38 B39 B65 B66 B67 共 12 条，全部按上游语义实现，不新增平台行为。

**决定**：

1. `InstallGameDialog` 恢复上游的完整流程：`Loaded` → 预填默认安装路径 + 拉 `GamePackage` 算体积；类实现 `[INotifyPropertyChanged]`；语音区按 `AudioPackageScanDir` 显示并按 UI 语言默认勾选；成功启动安装后**先写安装路径**（`GameLauncherService.ChangeGameInstallPath`）、广播 `GameInstallTaskStartedMessage`，再隐藏对话框（B01/B02/B03/B04/B66/B67）。
2. 拉不到 `GameConfig` 时**不隐藏对话框**，改为显示 `Lunarward_Error_ManifestUnavailable` 并禁用开始（上游在 RPC 子进程里预热过配置，Linux 直连网络没有这条预热路径；隐藏等于「点了没反应」）。
3. `DriveHelper` 按 `/proc/mounts` 找最长前缀挂载点算可用空间，不再一律落到根分区（B09）。
4. 安装目录识别改「别名 + 区服标记 + 归一化」匹配并放宽到三层（B36）；启动扫描移到后台线程（B65）。
5. 取消/停止不再被记成失败：`Failed>0` 与 `catch` 都先看 `IsCancellationRequested`（B37）。
6. 卸载前结束占用安装目录的进程，删除时保留嵌套的用户数据与截图目录（B38）。
7. 预下载只在「已记录且目录存在」时可用，避免往自造目录写数据（B39）。

**代价边界**：`InstallGameDialog.Linux.cs` 由 110 行扩到约 420 行，与上游 XAML 的控件名强耦合（改名即编译失败，算好事）；`DeleteRecursiveExcept` 为「保留子目录」自实现，若用户把截图目录设在游戏目录深处，卸载后该子树保留。

**验证**：`dotnet build src/Lunarward/Lunarward.csproj -c Debug` 0 错误；`dotnet test Starward.slnx -c Debug` 264 通过；`lint-ui-thread.sh` 全绿；真机 vi X11 窗口截图确认安装对话框预填 `/home/qie2035/Games/hkrpg_cn`、解压空间 110.41 GB / 可用 505.87 GB、汉语默认勾选、「开始安装」可点。

**确认方式**：用户 m01511 授权全量修复；后续批次同此决策继续，逐批追加编号。

## D96 审计项修复：批 2「游戏设置 / 运行环境写入」按上游语义补齐

**背景**：D95 批 1 完成后继续按模块修复。批 2 覆盖 B05 B10 B11 B40 B41 B42 B43 B69 B70 共 9 条，核心是「设置必须写进游戏真正使用的 wine 前缀，写不进去必须报出来」。

**决定**：

1. `WineRegistry` 的作用域前缀从 `[ThreadStatic]` 改为 `AsyncLocal<string?>`：`await` 之后续体可能换线程，`ThreadStatic` 会丢作用域（L-127 同类坑）。新增同步重载 `GetKeyValues(key, winePrefix)` / `GetValue(key, name, default, winePrefix)`。
2. `GameSettingService` 的读写都显式解析游戏前缀（`ProfileBinding.Resolve` → `PrefixManager.TryGetExistingPrefix`）；无前缀抛 `Lunarward_Error_PrefixNotFound`，写失败抛 `Lunarward_Error_SetGameSettingFailed`（B43：不再静默提示「已应用」）。
3. `GameSettingPage` 每次初始化先重置三个分区开关与可见性、HDR 标志、错误行（B42）；新增 `OnGameChanged()` 重载，切游戏后重读整套设置。
4. 原神 HDR 亮度对话框改为代码构建（三个滑块 300–2000 / 100–500 / 150–550 + 保存 + 恢复默认），打开时读游戏注册表现值，保存写回目标前缀；默认值 1000/300/350（B11/B70）。Windows 版的 Win2D 实时 HDR 预览不实现，对话框内保留说明。
5. 启动原神前按 `AppConfig.EnableGenshinHDR` 重放一次 HDR 开关到目标前缀（B41）；`SetGenshinEnableHDRAsync` 找不到前缀时抛错而不是静默跳过（B69，AppConfig 的值保留，启动时会重放）。
6. 账号切换写游戏前缀，失败抛错并在切换器里弹 toast（B10/B40）。
7. GPU 信息枚举 `/sys/class/drm` 后按 `^card\d+$` 过滤（B05：`.NET` 的 searchPattern 不支持 `[0-9]`，原写法恒 0 命中）。

**代价边界**：设置读写依赖「前缀已存在」；尚未创建前缀的新用户点「应用」会看到明确报错（而不是静默成功），这是有意的取舍。HDR 对话框没有预览画面（引擎无 HDR surface），只做参数编辑。

**验证**：`dotnet build src/Lunarward/Lunarward.csproj -c Debug` 0 错误；`dotnet test Starward.slnx` 264 通过；`lint-ui-thread.sh` 通过；真机确认游戏设置页可正常渲染（分辨率 1920×1080、语音汉语、HDR 区块与「当前显示器不支持 HDR」提示）。本轮 KWin 截图接口（`ScreenShot2.Error.Cancelled`）与指针注入异常，HDR 对话框的点击验证未完成，改用代码审查与编译验证，遗留到后续 GUI 回归。

**确认方式**：用户 m01511 授权全量修复。

## D97 审计项修复：批 3「截图」按上游语义补齐

**背景**：批 3 覆盖 B06 B07 B14 B15 B16 B17 B45 B46 B47 共 9 条，集中在图库交互、截图格式设置与缩略图缓存。

**决定**：

1. 「多选」开关真正生效（B06）：勾选框只在多选模式下显示；关闭多选时清空选择。条目根 `Border` 接 `Tapped`（B07），多选时切换勾选，否则打开查看器（ItemsControl 没有 ItemClick）。
2. 批量转换只在多选模式转勾选项、非多选模式转整库；原本「没勾选就转全部」删掉（B15）；转换改 `Task.Run` + `AsyncRelayCommand`，几十张图不再冻 UI（B45）；质量按设置索引映射 80/90/100。
3. 「自动复制截图到剪贴板」在 Linux 生效（B14）：截图成功后按 `image/png` 写 `wl-copy`，回退 `xclip`；失败只记日志，不影响截图本身。
4. 图库在每次挂载时重读目录并挂 `FileSystemWatcher`（B16），Alt+D 截图或改目录后即时出现；扩展名白名单恢复 7 种（B46）。
5. 截图质量设置改为 0/1/2 三段索引并绑定 `AppConfig.ScreenCaptureEncodeQuality`（B17），格式索引绑定 `ScreenCaptureSavedFormat`，两者即时落盘。
6. 「清除缩略图缓存」指向真实目录 `<CacheDirectory>/thumb`（B47），只用实际删除与否决定提示。

**代价边界**：Linux 捕获统一 PNG（D31），格式/质量只作用于「批量转换」；剪贴板依赖 `wl-copy`/`xclip`，两者都没有时只记日志。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 264 通过；`lint-ui-thread.sh` 通过。KWin 截图/指针注入在本会话不可用，图库点击与批量转换未做真机点击验证，遗留到 GUI 回归。

**确认方式**：用户 m01511。

## D98 审计项修复：批 4「服务与帮助器」按上游语义补齐

**背景**：批 4 覆盖 B08 B27 B28 B29 B57 B58 B59 B60 B76 共 9 条，集中在更新窗口、剪贴板、深链与游玩时长会话。

**决定**：

1. 「忽略此版本」写 `AppConfig.IgnoreVersion`，`UpdateService.CheckUpdateAsync` 按 `disableIgnore` 过滤（B08）。
2. 更新窗口的「发布页面 / 完整包」超链接按 `Tag` 拼 URL 并 `OpenUrl`（B28）；`RootGrid_Loaded` 处理 `DisableAutoUpdate`，把「需手动下载」写进错误行（B59）；这两个 handler 从 `UpdateWindow.LinuxAuto.cs` 移到 `UpdateWindow.Linux.cs`（自动生成文件只留记录）。
3. 文件复制写 `text/uri-list`（`file://` URI，CRLF 分隔），不再写裸路径文本（B29）；写剪贴板整体移到线程池，UI 线程不再等外部进程（B60）。
4. 深链支持 `startgame` / `playtime` / `test` 三种 host，`playtime` 带 `pid`/`biz`（B57）。
5. 游玩时长会话结束优先按「本游戏的 exe 名 + 同一 WINEPREFIX」轮询判定，只有认不出 exe 时才退回 `wineserver -w`（B58）：同前缀还有别的 wine 程序时不再拖长时长和前缀锁。
6. `PlayTimeRecordService` 在 pid 复用场景额外比较 `/proc/<pid>/stat` 的进程启动时刻与最近 Start 记录，晚于记录就当作新会话（B76）。

**代价边界**：仍依赖 `/proc` 与 `WINEPREFIX` 环境变量；非 wine 原生启动器（proton 自带容器）下 exe 名可能不同，会退回旧判定。剪贴板 `text/uri-list` 依赖 wl-copy/xclip。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 264 通过；`lint-ui-thread.sh` 通过。

**确认方式**：用户 m01511。

## D99 审计项修复：批 5「外壳」按上游语义补齐

**背景**：批 5 覆盖 B12 B13 B44 共 3 条，都是「没有上游对应风险的 Linux 特化」造成的实现错位。

**决定**：

1. `--hide` 启动在隐藏窗口前检查 `StatusNotifierTray.Available`：托盘不可用就保持窗口可见并记 Warning（B12）。与同文件 `AppWindow_Closing` / `StartTrayAsync` 的既有策略一致。
2. 所有 `AppInstance.GetCurrent().UnregisterKey()`（MainWindow 退出分支、FileManageSetting 的更改数据目录 / 删除所有设置）改为独立 try/catch 的小函数，异常只记 Debug；备份与 `App.Current.Exit()` 不再被同一个 try 吞掉（B13）。
3. `Program.Linux.cs` 入口注册 `AppDomain.CurrentDomain.UnhandledException`，把异常与时间追加到 `AppConfig.LogFile`（取不到就只写 stderr）（B44）。

**代价边界**：托盘可用性在启动时判定一次；之后托盘消失不会再自动显示窗口（沿用既有策略）。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 264 通过；`lint-ui-thread.sh` 通过。

**确认方式**：用户 m01511。

## D100 审计项修复：批 6「热键设置」按上游语义补齐

**背景**：批 6 覆盖 B23 B24 B26 B55 B56 B74 B75，集中在热键注册、配置回读与设置页直达入口。

**决定**：

1. 无 X11（纯 Wayland）时也要把两个热键登记进内存表并回读已保存的组合（B24），设置页不再显示成空白；注册状态标记为失败并给出警告。
2. `InitializeHotkeyInput` 按注册结果显示 `HoykeyInputState`（B23），失败不再看起来正常。
3. `KeycodeFor` 补方向键、Home/End/PageUp/PageDown、Insert/Delete、锁定键、小键盘与 OEM 标点（B26）；`RegisterHotkey` 先验证键码能被轮询层识别，认不出直接返回失败，不落盘。
4. `ParseHotkey` 认单独的 `"0"` 为「用户删除」（B55）；`SaveHotkey` 只在注册成功且值变化时写（B56）；未知 id 返回 `ERROR_INVALID_PARAMETER`，不再被当成截图热键（B75）。
5. 设置页新增 `ApplyRequestedPage()`，每次挂载都消费 `SettingNavigation` 的一次性目标（B74）。

**代价边界**：仍是 X11 轮询实现；纯 Wayland 下热键不可用但会如实显示警告。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 264 通过；`lint-ui-thread.sh` 通过。

**确认方式**：用户 m01511。

## D101 审计项修复：批 7「抽卡 / 战绩」按上游语义补齐

**背景**：批 7 覆盖 B18 B19 B20 B48 B49 B50 B51，集中在缓存页复用时抽卡页与战绩页的跨游戏残留。

**决定**：

1. 抽卡页 `OnNavigatedTo` 先把 3 个 `Enable*GachaItemStats`、`IsZZZGachaStatsCardVisible` 与 5 个菜单项 `Visibility` 复位，再按当前游戏置位（B18/B19）。
2. 抽卡页新增 `OnGameChanged()`：经 DispatcherQueue 重跑 `Initialize()` + `UpdateWikiDataAsync()`，切游戏后 UID/卡池/统计按新游戏加载（B48）。
3. 战绩页新增 `OnGameChanged()`：重跑 `CheckAgreementAsync()` → `LoadGameRoles()` / `UpdateDeviceInfoAsync()` / `RefreshGameRoleHeadIconSilentlyAsync()`（B51）。
4. 自助查询页 `OnNavigatedTo` 先把三个查询列表收起（B20）。
5. 每日便签的满体力时间格式改为 `'HH\\:mm'`（XAML 生成代码里的转义冒号），不再跟随区域分隔符（B49）。
6. 战绩页未登录空态加 `Margin="248,0,0,0"`（工具箱展开宽度），不再盖住左侧导航（B50）。

**代价边界**：空态左边界按 `OpenPaneLength=248` 固定，工具箱收起时右侧会留一点空白；抽卡/战绩的 `OnGameChanged` 走 DispatcherQueue 延后一帧。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 264 通过；`lint-ui-thread.sh` 通过。XAML 里 `'HH\:mm'` 会让 Uno 生成器产生 `CS1009`，必须写成 `'HH\\:mm'`（已实测）。

**确认方式**：用户 m01511。

## D102 审计项修复：批 8「背景」按上游语义补齐

**背景**：批 8 覆盖 B21 B22 B52 B53 B54，集中在背景图库缩略图、自定义壁纸刷新、视频帧线程与坏图校验。

**决定**：

1. `CachedImage.IsThumbnail` 真正生效（B21）：视频走 `VideoThumbnailer.EnsureAsync` 的缓存缩略图；图片用 `SKCodec.GetScaledDimensions` 按目标宽度直接缩放解码，不再整文件读入（`ImageThumbnail` 依赖 Win2D/BitmapDecoder，被 csproj 排除，不能在 Linux 用）。
2. 背景守卫增加 `_backgroundDirty`（B22）：无参 `BackgroundChangedMessage` 置脏并清空建议背景缓存，换自定义壁纸后立即重跑，不再被「同一游戏」挡掉。
3. 视频帧回调的异常分支改为 `DispatcherQueue.TryEnqueue(StopVideoBackground)`（B52），不在 LibVLC 回调线程改 XAML 或 Dispose 播放器。
4. `VideoCanvas_PaintSurface` 在整个绘制期间持有 `_frameLock`（B53），`StopVideoBackground` 无法在绘制中途释放帧缓冲。
5. `CheckBackgroundFileAvailableAsync` 对图片做真实解码校验（B54）：`ImageLoader.GetImagePixelSizeAsync` 返回 0 即抛 `InvalidDataException`，坏图不再被复制进缓存与写进配置。

**代价边界**：缩略图解码失败返回 null（不退回整图）；视频仍只做存在性校验。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 264 通过；`lint-ui-thread.sh` 通过。

**确认方式**：用户 m01511。

## D103 审计项修复：批 9「启动器」按上游语义补齐

**背景**：批 9 覆盖 B30 B31 B32 B33 B34 B35 B61 B62 B63，是最后一组（76 条里 6 条已判不成立，不修）。

**决定**：

1. 缓存页切游戏时 `OnGameChanged` 已重载内容，紧随其后的 `OnLoaded` 用 `_gameChangedHandled` 跳过（B30），避免两次并发初始化。
2. `CreateGameProcessStartInfo` 增加 `GameBiz` 参数，运行时配方/前缀/退出伴随程序都按「本次启动的游戏」解析（B31；`DescribeLaunchPlan` 同步加 `gameBizHint`）。
3. 配方 `Args` 改为追加到游戏自带参数之后（B32），登录票据/DX12/弹窗参数不再被吞。
4. `ApplyWineRuntimeEnvironment` 的 `/files/` 索引先判 -1（B33），自定义 Proton 路径不再抛异常。
5. `GetGameProcessAsync` 非 Windows 走 `PlayTimeSession.FindProcessByCommandLine`（扫 `/proc/*/cmdline`），不再按 exe 名查进程（B34）。
6. `thirdPartyTool || StartGameWithCMD` 的「按进程名轮询」只在 Windows 走；Linux 一律按 pid 记时长（B35）。
7. 公告改为浏览器打开后立即把 `IsGameNoticesAlert` 置 false（B61）；`UpdateGameContentAsync` 在 await 之后复核 `_contentGameId == CurrentGameId`，旧请求不覆盖新内容（B62）。
8. 恢复设置对话框 FlipView 的纵向 `ItemsPanel`（B63），长页面可以滚到底。

**代价边界**：`/proc` 扫描只在没有进程句柄时使用（先按 pid 记录）；公告红点在浏览器未打开成功时也会熄灭（打开失败另外 toast）。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 264 通过；`lint-ui-thread.sh` 与 `lint-cli-help.sh`（A 段 51 个开关）通过。

**确认方式**：用户 m01511。

## D104 运行环境审查修复：批 A「启动链正确性」

**背景**：2026-11-23 对「运行环境」模块做只读审查（产物 `28-runtime-env-audit.md`，R01–R42），用户 m02913「全处理了」授权修复。批 A 覆盖 R01–R06：这些不是平台差异，而是「配置读到了、启动时没用上」的实现错位——用户以为在跑自己配的环境，实际换了前缀/参数且没有任何提示。

**决定**：

1. **R01 配方 env 是唯一依据**：`CreateGameProcessStartInfo` 改为整体下发 `LaunchRecipe.Environment`（继承环境 + Proton 变量 + 增强 HUD + 配方覆盖），删除单独的 `profile.Env` 回灌；`WINEPREFIX` 由 `Build` 固定成真实前缀，用户写 `WINEPREFIX` 只留覆盖告警、不再影响进程实际前缀。
2. **R02 参数只追加**：`LaunchRecipeBuilder.Build` 自己合并 `gameArgs + profile.Args`，删除「有配方参数就整体替换」的三元；调用方传游戏自带参数原始值。单测断言两者同时出现在 `Argv` 且游戏参数在前。
3. **R03 前缀创建**：`CreateWithWineboot` 先 `Directory.CreateDirectory(父目录)`；`WINEDLLOVERRIDES` 默认 `mscoree,mshtml=`（否则 wineboot 弹 wine-mono 对话框、启动链挂死）；`WaitForExit` 加 3 分钟上限，超时 `Kill(entireProcessTree)` 并抛错。启动侧不再静默降级：`EnsurePrefix` 失败记 Error + toast 明确告知「本次改用默认前缀」。
4. **R04 WINEARCH**：创建时 `info.Environment["WINEARCH"] = NormalizePrefixArch(profile.PrefixArch)` 并写进 `<prefix>/.lunarward/prefix.json`；预检优先读真实前缀（`system.reg` 的 `#arch=`）判断位宽冲突，配方值与实际不一致时给 Warning（以实际为准）。实测：wow64 版 wine 对 `WINEARCH=win32` 会直接报「not supported in wow64 mode」，现在会以明确错误呈现，而不是静默建出 win64。
5. **R05 前缀锁**：`_prefixLockStream` 单槽改为 `Dictionary<string, FileStream>` 按前缀分槽；`TryLockPrefix` 失败时启动直接按「前缀被占用」失败（预检与启动之间的 TOCTOU 一起挡住）；`Process.Start` 抛异常时先释放锁；会话清理显式接收本次前缀，不再读静态 `ActivePrefix`。
6. **R06 默认配方**：`CreateDefault` 不再把 `PrefixPath` 指向 `~/.wine`（只有用户显式设 `LUNARWARD_WINEPREFIX` 才用），空值 = `PrefixRoot/<名字>` 受管目录；默认 `Env` 带 `WINEDLLOVERRIDES=mscoree,mshtml=`。

**代价边界**：已存在的旧前缀架构不改（预检给 Warning）；wine 构建不支持目标架构时创建失败并明确报错（不再「创建成功但其实 win64」）；前缀创建失败时仍允许回退系统前缀，但必须让用户看到。

**验证**：
- `dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` **268 通过**（新增 4 条：参数追加 ×2、`#arch` 读取 ×2）；`lint-ui-thread.sh` 通过。
- 真机：`lunarward --prefix-ensure hk4e_cn <测试方案>` 在 `PrefixRoot` 不存在的情况下 9.4 s 建成 `~/.local/share/lunarward/prefix/r03test`，`system.reg` 为 `#arch=win64`、`drive_c` 存在，全程无 wine-mono 对话框（此前实测会卡住）。
- 测试方案与临时前缀已删除，用户配置未改动。

**确认方式**：用户 m02717（要求审查）+ m02913（「全处理了」）。

## D105 运行环境审查修复：批 B「持久化」

**背景**：批 B 覆盖 R07–R11（`28-runtime-env-audit.md` 的存储组）：共同点是「写失败/读失败都不出声」，用户以为改好了，重启后配置消失或被别的窗口覆盖。

**决定**：

1. **R07 保存失败要能看见**：`RuntimeProfileStore.Save/Delete` 改为返回 `bool` 并记 Error；保存/克隆/拆分/增强保存/删除的调用点按返回值提示，失败不再弹「已保存」。
2. **R08 原子写并发安全**：`JsonFile.WriteAtomic` 临时文件名改为 `path.<guid>.tmp`（固定 `.tmp` 会被两个保存者互相改名覆盖），同一路径加进程内锁串行化，改名后 `fsync` 父目录（P/Invoke `open`+`fsync`+`close`），失败清理临时文件。
3. **R09 绑定 fail-closed**：`ProfileBinding.TryLoad` 区分「文件不存在」与「解析失败」；失败先读 `.bak` 恢复，仍不行则 `LastError` 记原因、`Set` 直接抛 `InvalidOperationException` 并保留原文件——绝不用只剩一条的字典覆盖整份绑定。
4. **R10 旧目录迁移恢复**：`config/lunarward/profiles/` 存在时逐份导入 `env/`；全部成功才把旧目录改名存档（`profiles.migrated-<时间>`），有任何失败则保留旧目录、下次重试（日志记失败条数）。
5. **R11 Id 校验**：Id 必须匹配 `^[A-Za-z0-9_-]{1,32}$`，拼路径后再做前缀包含校验；加载时若 Id 与文件名不一致，以文件名为准并回写（防止「两份文件同 Id、删不干净」）。`Delete` 也走同一个 `ProfileFile`。

**代价边界**：绑定文件损坏时写操作会失败（需用户修复/删除该文件），不会自动重建；迁移失败的旧文件保留在原处，需要人工处理（日志有路径）。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` **269 通过**（新增并发原子写 1 条）；`lint-ui-thread.sh` 通过。

**确认方式**：用户 m02717（审查）+ m02913（「全处理了」）。

## D106 运行环境审查修复：批 C「组件层」

**背景**：批 C 覆盖 `28-runtime-env-audit.md` 的组件组（R12–R17、R31–R34）。共同点是「下载、校验、解压、导入四段各自为政」：镜像只对 CLI 生效、源端没给摘要也算「已校验」、续传不看 206、分片合并数错、解压只防 `..` 不防绝对路径与炸弹、导入先改名后写清单。

**决定**：

1. **R12 镜像前缀单一来源**：`ComponentDownloader.DownloadAsync(..., string? mirrorPrefix = null)` 内部 `ApplyMirror`；页面与 CLI 都传 `source.MirrorPrefix`，外部不再各自套前缀。
2. **R14 续传以服务器为准**：带 Range 的请求若返回 200（服务器忽略 Range），删除半截文件、`have=0`、重发无 Range 请求；`FileMode` 由本次是否真的续传决定，不再看本地文件大小。
3. **R31 分片合并可验证**：片数用 `ChunkCount(total, chunkSize)` 计算；缺片或长度不符立即抛错；任一分片任务失败先 `CleanupParts` 再抛，避免「半套分片 + 半截文件」留着下次复用。
4. **R34 完整性三态**：`ComponentDownloadResult` 增加 `DigestVerified`；只有源端给了期望摘要才算验证过，`DigestMatched` 只在验证通过时为 true；`ComponentImporter.ImportAsync` 增加 `expectedSha256` 参数（源端值），与本地计算值不一致抛错，`Digest.FromApi` 以「是否有期望值」为准。
5. **R32 解压边界**：绝对路径（`/`、`//`、盘符）直接拒绝而不是 `TrimStart('/')` 改写；符号链接目标校验带尾分隔符（与主路径检查一致）；`tar.xz` 解压设体积上限 `max(32 GiB, 归档大小 × 200)`，`CopyAsync` 逐块检查并响应取消。
6. **R13/R16 组件扫描**：受管运行器路径读 `manifest.Root`（`Path.Combine(dir, manifest?.Root ?? ".")`）；`wine --version` 结果按「路径 + mtime」缓存，超时 4 s 用 `WaitForExitAsync` + `Kill(entireProcessTree)`，stdout/stderr 并发读取，非 0 退出返回空串。
7. **R15 健康检查**：15 s 超时与用户取消都 Kill 进程；取消向上抛 `OperationCanceledException`，超时返回「超时」结果（两者语义分开）。
8. **R17/R37 图形层**：`CollectFiles` 识别 `x64/`、`x32/`、`x86/`（x32 优先于 x86），平铺按 64 位，目标目录按条目 `Is32Bit` 决定而不是文件名猜；`EnsureInstalled` 跳过时逐文件比 size；`Remove` 删不掉的文件保留记录（只移除真正删掉的）。
9. **R33 导入原子性**：manifest 先写进临时目录，再 `Directory.Move` 整体改名；中途失败只留临时目录（由 finally 清理），不会出现「有目录、没清单」的不可导入状态。

**代价边界**：解压上限 32 GiB 是经验值（当前组件最大约 1 GiB），超大组件需调；`DigestVerified=false` 的下载仍写入磁盘但结果会明确提示「未校验」；镜像前缀只对 `ComponentSource` 里声明了 `MirrorPrefix` 的源生效。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` **270 通过**（更新 `ResolveTarget_GuardsAgainstEscape`：绝对路径/盘符改为期望拒绝，新增盘符用例）；`lint-ui-thread.sh` 通过。下载与解压路径未做联网实测（本机无可用组件源），按代码路径与单元测试覆盖判断。

**确认方式**：用户 m02717（审查）+ m02913（「全处理了」）。

---

## D107 运行环境审查修复：批 D「页面体验与一致性」

背景：D104–D106 修完启动链、持久化与组件层后，剩余 20 条集中在页面交互、跨入口一致性与文档漂移（28 号 R18–R30、R35、R36、R38–R42）。

决定：

1. 环境装配收敛到唯一函数 `RuntimeEnvironment.Assemble`：启动、Shell、RunOnce、`--launch-plan` 预览共用（R19/R38），不再各自拼 `profile.Env`。
2. 命令包装缺程序时按「跳过并提示」处理，`LaunchRecipe.MissingWrappers` 交给调用方弹一次警告；不再让启动直接失败（R18）。
3. 页面读配置阶段不 fork `wine --version`：`EnvironmentResolver.Refresh` 只用 `ComponentStore.CachedRunners`，空缓存就跳过（R21）。
4. 前缀体积统计移到后台：`LoadPrefixes` 变异步 + `Task.Run`；方案列表与详情的 `Inspect` 传 `computeSize:false`（R20）。
5. 删除前缀前检查同路径方案，有别人在用就跳过并列出名字；路径比较统一 `GetFullPath` 口径（R22/R29 的一部分）。
6. 编辑器的启动预览把当前方案的增强项一起填进 draft（HUD/gamemode/gamescope），改名字立刻刷新预览与前缀行（R27）。
7. 保存/删除后同时刷新 chips 与失效绑定提示（R28）。
8. `LastKnownGoodStore` 从只写变可读：详情页显示「上次成功启动：…」并可一键恢复 Runner/DXVK/VKD3D；`Load` 出错先把坏文件改名 `.corrupt-*`（R25/R42）。
9. 预检「去设置」带 `profileId`：`SettingNavigation` 增加一次性 runtime 目标，页面 `OnNavigatedTo` 消费并定位方案/分组（R26）。
10. 前缀路径保存时校验绝对路径、拒绝根目录（R29）；`ContentDialog` 加防重入标志（R23）。
11. `CreateDefault` 只在「首次启动」创建（R24）；旧 `graphicsId` 按组件 `Kind` 迁移到 dxvkId/vkd3dId（R41）。
12. 名称清洗补 `.`/`..` 与空白（R35）；`SplitArgs` 只吞「转义引号/反斜杠」的反斜杠（R36）；第二实例启动异常落日志并弹提示（R39）；文档与注释里的 `prefixes/`、五种前缀模式描述同步为现状（R40）。

代价边界：`LoadPrefixes` 变异步后，前缀列表在扫盘完成后才刷新（切换页签可能先看到空再补齐）；`OnNavigatedTo` 定位只在页面首次挂载生效；恢复上次组合只恢复组件 id 与运行器路径，不改 `prefixPath` 与增强项。

验证：`dotnet build Starward.slnx -c Debug/Release` 0 错误；`dotnet test` 270 通过；`lint-ui-thread.sh`、`lint-cli-help.sh`、`lint-pkgbuild.sh` 通过。

确认方式：m02717（审查请求）、m02913（「全处理了」）。

## D108 运行环境页面 UI 审查（21 条）：术语、布局、i18n 与可达性

背景：D104–D107 修完运行环境模块的 42 条实现错位后，用户要求「把这一块的 UI 也仔细审一遍」（m03493）。真机逐页走查 + 对照 23 设计 §7/§13，发现 21 条（术语/i18n 7、布局视觉 8、交互可达性 6），用户 m03736「全部处理」。审查与逐条处置见 `29-runtime-env-ui-audit.md`。

决定：

1. 术语统一为「方案 / 运行器 / 图形层 / 前缀」四词，「配置」不再出现在用户可见文案里（含 resx 值改写）。
2. 运行环境页用户可见文案全部走 `Lang.Lunarward_RuntimeEnv_*`（新增约 110 个键，中英双语）；`RuntimeEnvironmentSetting*.cs` 的中文硬编码清零。
3. 编辑器与增强面板都改成「内容星号行 + 固定底栏」的 Grid：保存/取消、命令预览不再随表单滚走。
4. 颜色一律用主题 token（`SystemFillColorCritical/Caution/SuccessBrush`），`DescribeHealth` 不再返回 hex。
5. 长路径按目录分段缩写（`ShortenPath`）并配 ToolTip 全文；体积统一两位小数。
6. ⋯ 菜单：调试 Shell 仅 `DevSwitches.IsEnabled`（Release 不出现）；删除项标危险色；菜单项分组。
7. 编辑器加脏检查（切页签/返回先确认）；页签与 ⋯ 按钮补 `AutomationProperties`；Ctrl+N / Ctrl+S / Delete 快捷键。
8. `MainView_Loaded` 在只有 `--subpage` 时先导航 `SettingPage`，深链可直达。

文案与本地化：简体与英文（中性 `Lang.resx`）各 331 个 `Lunarward_RuntimeEnv_*` 键；繁体（zh-TW / zh-HK）用 OpenCC 从简体转换补齐 313 个键；新增 `LocalizationKeyTests` 钉住「简体 ⊆ 英文 / zh-TW / zh-HK」，以后加键只写一份测试就会报。德/西/意/日/韩/俄/泰/越等语言沿用资源回落（中性英文），不复制英文冒充翻译。

代价边界：`AddHandler(KeyDownEvent)` 在页面层拦截，焦点在文本框内时 Delete 也走页面级删除确认（目前详情页没有可编辑文本框，后续加编辑框要复查）；`ShortenPath` 只是展示层缩写，复制/ToolTip 仍是完整路径；新增键只有中英，其它语言回落英文（与既有 `Lunarward_*` 一致）。

验证：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 270 通过；`lint-ui-thread.sh` 通过；真机 1200×680 复核四页签、前缀卡、编辑器固定底栏与脏检查弹窗。

确认方式：m03493（审查请求）、m03736（「全部处理」）。

## D109 米游社工具箱页签残留与「下载组件」选择不可用

背景：用户 m04412 报两处缺陷（米游社工具箱切换游戏后页签残留；下载组件弹窗无法选择），并要求顺带排查同类问题。真机复现确认，根因如下。

决定：

1. 缓存页的「本游戏要显示哪些页签」必须**先全量收起再按当前游戏显示**：`GameRecordPage.InitializeNavigationViewItemVisibility()` 现在遍历 14 个页签全部 `Collapsed`，再按 `CurrentGameBiz` 打开本游戏的那几个；切游戏时 `OnGameChanged` 先重置页签、清 `CurrentRole`、`NavigateTo(BlankPage)`，再重新拉角色，避免旧角色/旧内容残留。
2. 缓存页的 `OnUnloaded` **不得退订 XAML 里一次连上的事件**（`NavigationViewItem_BattleChronicle.Tapped` 等）：页面实例会被复用，退订后第二次进入按钮永久失效。删掉这两条 `-=` 并留注释（与 D94 的 PageBase 生命周期约定一致）。
3. 「下载组件」弹窗不用 `ListBox` 选中态（Uno/Skia 在 `ContentDialog` 里点击不改选中），改为 `CheckBox` 行列表：勾选即入队，`已选 N 个版本` 计数，`开始下载` 按勾选数启用；支持多选排队。
4. 切换下载源必须清空旧列表并禁用「开始下载」：不同源的类型不同（DXVK / wine / vkd3d），残留列表会让归档按新源类型导入。
5. 空间预检改用 `DriveHelper.GetDriveAvailableSpace`（真实挂载点），不再 `DriveInfo(Path.GetPathRoot(...))`。
6. 新增文案键 `Lunarward_RuntimeEnv_SelectedVersions`（中/英/繁四套对齐，`LocalizationKeyTests` 守护）。

代价边界：组件列表改为逐行 CheckBox 后不再有「当前行」概念，键盘上下无法移动焦点（需要 Tab 逐行）；`已选 N 个版本` 只统计勾选数，不显示总大小预估。

验证：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 271 通过；真机：勾第 3 行 `dxvk-3.0.2` → 「已选 1 个版本」+ 开始下载可用；切源到 wine-tkg → 列表清空、开始下载变灰；米游社工具箱从原神切到绝区零 → 页签只剩 战绩/绳网日报/式舆防卫战/危局强袭战，切回原神恢复四项。

确认方式：m04412（报缺陷并要求顺带排查）。

遗留观察（未修，非本轮范围）：`BattleChronicleWindow` 标题在 KDE 上显示为 `æœ«…`（UTF-8 字节被按 Latin-1 解码），属窗口标题编码问题，需单独确认是 Uno 还是 KWin 侧；该窗口目前是 M2 占位（WebView2 未移植），内容为空白也属预期。

## D110 运行环境 UX 重做：添加组件、下载源管理、方案高级与启动预览

背景：用户 m04866/m04895 指出运行环境现有的排版、设计与交互用起来别扭，重点点了「下载组件」入口与弹窗内容、方案「高级」、组件源等；要求从使用体验出发给方案。实测取证（D110 前的界面）确认：下载弹窗未加载时是一块 190 px 空白、加载失败的提示直接暴露 `components-sources.json` / `AssetPattern` 等内部名词；标题、版本列表、`开始下载`/`取消`/`关闭` 三套按钮语义重叠；「更多」里藏着组件源与镜像测速；方案「高级」把 env / args / wrappers / notes / shell 六个纯文本框平铺在一个 Expander 里，env 无校验、包装器缺失无提示、颜色硬编码；组件源弹窗只有逐源一个镜像前缀输入框，没有启用、排序、测速、自检与恢复默认。

决定：

1. 「图形层」页签入口改为「添加组件…」+「管理下载源」两个明确动作；「更多」只留导入归档、导入图形组件、下载暂存、清理未使用——组件源与镜像测速从不明显的菜单里移出。
2. 「添加组件」弹窗重做：打开即按当前源自动加载；版本用 CheckBox 行（名称、版本、ReleaseTag、大小、已安装徽标）；勾选后底部汇总「已选 N 项 · 总体积 · 可用空间」，空间不足直接给出差额并禁用安装；底部只剩「暂停 / 安装所选 / 关闭」三个按钮；下载/解压/导入的进度与失败重试放在行内；所有异常文案经 `FormatFriendlyError` 剥离内部字段名。
3. 组件源模型加 `Enabled`，并新增 `LoadEnabled()`、`ResetUserEntries()`、`IsValidMirrorPrefix()`；下载与「添加组件」只列启用中的源。
4. 「管理下载源」独立弹窗：每源三行（启用 + 名称 + 类型 + 上下排序 / 镜像前缀 + 校验提示 / 测速 + 自检 + 结果），顶部「恢复默认源」与「全部测速」；保存时按当前顺序重写 `Rank`，只写用户覆盖文件，不改内置默认。
5. 方案「高级」按语义分组：启动行为（Shell、参数）、包装器（chips + 输入 + 添加，按 `Enhancements.FindInPath` 检测 PATH，缺失标红）、环境变量（KEY/VALUE 表格 + 添加/删除 + 常用变量下拉 + 合法性与去重校验）、备注；删掉与「增强」面板重叠的字段。
6. 方案详情新增「启动预览」卡片：与真实启动同一套解析（`LaunchRecipeBuilder` + 环境变量覆盖 + WINEPREFIX/WINEDEBUG），只读等宽文本，避免「配置看着对、启动才发现不对」。
7. 新增文案键 39 个（五套资源：中性、简体、英、繁中 zh-TW、繁中 zh-HK），繁中由 OpenCC `s2twp` / `s2hk` 生成；`LocalizationKeyTests` 继续守护键对齐。

代价边界：`RuntimeEnvironmentSetting.Components.cs` 的下载弹窗从约 280 行增至约 480 行（自绘行 + 队列 + 汇总），维护成本上升；安装仍是串行队列，不支持并行下载；「管理下载源」只编辑用户覆盖层，内置源的仓库/资产规则仍需改代码；`ComponentSourceEntry.Enabled` 为新字段，旧覆盖文件缺该字段时按默认启用。

验证：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 271 通过；`lint-ui-thread.sh` / `lint-cli-help.sh` / `lint-pkgbuild.sh` 通过。真机（pid 624590 起，frame 288,120 1200x704）：添加组件打开即加载，wine-staging 无资产时提示「这个源当前没有匹配的版本（仓库可能改名了）」；切 DXVK 源列出 10 行带「已安装」徽标；管理下载源每行启用/类型/排序/镜像/测速/自检齐全，测速返回 `4074 ms`；高级展开后为四组、环境变量「添加变量」生成 KEY/VALUE/删除行；详情滚动可见「启动预览」卡片与实际解析结果；右列详情/编辑器在窄窗口下可滚动（修复了列 Grid 缺显式星号行导致整块被裁且不滚动的排版缺陷）。

确认方式：m04866 / m04895（提出重做）、m04954（「按你的倾向，全做」，批准 P0+P1+P2）。

## D111 运行环境页第三轮打磨：字段对齐、下载源增删改查、详情交互

**背景**：D110 之后用户试用仍提三处不顺：①「高级」里各字段宽度不齐（路径框被三个按钮挤成半行）；②「管理下载源」只能改镜像，不能增删；③详情页动作/绑定 chip/快捷键可信度不够。用户 m05406「全部处理」。

**决定**：
1. 编辑器统一 `Field(label, control, hint)`：标签列 110px + 控件列 Stretch，路径框独占整行、按钮另起一行；「名称/运行器/前缀」不再重复段落标题。
2. 下载源支持增删改查：`ComponentSourceEntry.Removed` 作为内置源的墓碑（删除即写 `Removed=true`，恢复默认清空用户文件）；新增源用 `user-<8hex>`；表单校验名称/owner/repo/资产正则/镜像；搜索框过滤；行内「测速/自检/编辑/删除」另起一行，避免被弹窗裁掉。
3. 无启用源时「添加组件」给空态对话框 +「管理下载源」直达，不再只弹 toast。
4. 详情页运行器/图形层动作统一为「修改」；「已绑定 N 个游戏」可点开绑定表（逐游戏选方案，留空=跟随默认）。
5. 编辑器底部「保存并运行…」复用详情页运行路径；已安装徽标加 tooltip；⋯ 菜单分隔线改成可见点线（深色主题下 `MenuFlyoutSeparator` 不可见）。
6. 启动预览不换行、横向滚动，并加「复制」。
7. 文案全部走 `Lunarward_RuntimeEnv_*`（五套资源，繁中用 OpenCC），补 `LocalizationKeyTests` 覆盖。

**代价边界**：内置源删除用墓碑而非物理删除（用户文件仍是唯一真源，恢复默认可回）；源管理弹窗改为多行布局后更高，需要滚动；「保存并运行」会立即弹运行对话框，没保存稿时先报校验错误。

**验证**：Debug/Release `dotnet build` 0 错误；`dotnet test` 271 通过；三个 lint 通过；真机（1200x704）确认字段对齐、源管理五行全量可见（搜索/全部测速/恢复默认/新增源 + 行内四动作）、绑定弹窗可用、预览复制可用。

**确认方式**：用户 m05396（三点问题）+ m05406（「全部处理。」）。

## D112 运行环境页签合并为「组件」+ 发布列表缓存

**背景**：D111 之后用户指出「添加组件 / 管理下载源」挂在「图形层」页签下反直觉——下载源里既有 wine 运行器也有 DXVK/VKD3D，而页签却叫「图形层」。同时问是否要引入 GitHub 相关依赖。

**决定**：
1. 运行器与图形层合并为「组件」页签（三个页签：方案 / 组件 / 前缀）。页内分两段：运行器（添加路径 / 设为新方案默认 / 外部运行器清单）、图形层（添加组件 / 管理下载源 / 更多 / 筛选 / 清单）。「添加组件」与「管理下载源」只保留这一处，和下载源包含两类组件的事实对齐。
2. 页签文案用「组件」；`Components_2` 的值改回「组件」，图形层段标题仍用 `GraphicsSectionTitle`。
3. **不引入 GitHub 依赖**：现有 `HttpClient` + `JsonSerializer` 只取 `tag_name`/`assets`/`size`/`digest`，加 Octokit 只会扩大依赖面。改为把三件小事做掉：环境变量 token（已有 `LUNARWARD_GITHUB_TOKEN`/`GITHUB_TOKEN`/`GH_TOKEN`）、发布列表 10 分钟 TTL 缓存 + `ETag`/`If-None-Match`（304 复用缓存）、403/429 给出「匿名 60 次/小时」的可操作提示；源管理弹窗补一行 token 说明。

**代价边界**：缓存是进程内、按 `owner/repo|releases|pattern|kind` 键，换源配置要等 10 分钟或重启才刷新；合并后「组件」页比原来的两个页签长，需要滚动；`Tab_Runners` 删除，任何按索引切页签的外部代码（`--subpage` 或自动化脚本）需按新顺序 0/1/2。

**验证**：Debug/Release build 0 错误；`dotnet test` 271 通过；真机确认三页签、组件页运行器段与图形层段、源管理弹窗 token 提示；
token 存在时请求头带 `Bearer`（代码路径审查），403/429 走 `DescribeFailure`。

**确认方式**：用户 m05687（三点）+ m05711（「按你的建议来吧。」）。

## D113 运行环境页面视觉收口：单入口编辑、格式统一、去掉误导性元素

**背景**：D110–D112 把页面结构改完后再看一遍截图，仍有「一个编辑动作四个入口」「chip 三种数字格式」「前缀进度条没有容量含义」这类视觉层问题（2026-11-23 用户「看一下『运行环境』这一块的UI设计」→「可以」授权修）。

**决定**：
1. **编辑入口收敛为一个**：详情头部「编辑方案」改名「修改」；运行器段、图形层段的「修改」按钮删除（它们都指向同一个编辑器），前缀段保留「打开目录」（浏览动作，不是编辑）。
2. **绑定行加标签**：头部「绝区零、原神」→「绑定：绝区零、原神」，避免这行被读成普通摘要。
3. **chip 统一格式**：「运行器 6/6 可用」「图形层 6 个」「前缀 1 个 · 370.71 MB」——运行器保留分子/分母（失败数有意义），其余带「个」。
4. **运行器行去掉 `bin 目录`**：裸布局标签对用户没有意义，删掉；可用性与「N 个方案在用」保留。
5. **前缀卡去掉「占最大前缀」进度条**：它是相对最大前缀的比例，没有容量含义，满条会被读成磁盘占满；体积仍以文字给出（`370.71 MB`）。标题也不再拼目录名（原「默认（opt）· .wine」），目录只在路径行出现；单方案使用时不再重复显示方案名。
6. **「N 个方案使用」可点**：变成 HyperlinkButton，点开列出具体方案名（多于一个方案时才额外显示名单行）。
7. **左列末尾加「＋ 新建方案」**：原先 280px 清单下方大片空白，且新建只能去右上角；现在两处都能建。
8. **tooltip 补作用域**：「添加路径…」= 把一个已存在的 wine/Proton 目录加入清单（不复制、不安装）；「设为新方案默认」= 只写默认方案，其它方案不受影响。
9. **前缀页说明重写**：删掉「会坏」这类口语，改成「wine 的 C 盘目录，通常几十 GB，可能被多个方案共用」。

**代价边界**：段内「修改」按钮删除后，改运行器要点头部「修改」再进编辑器（多一次点击，但语义唯一）；进度条删除后无法一眼比较多个前缀的相对大小，只能看体积数字。

**验证**：Debug build 0 错误；真机三页签截图确认（方案页头部「修改」/绑定标签/无段内按钮、组件页运行器行无 `bin 目录`、前缀页无进度条且「1 个方案使用」可点）。

**确认方式**：用户 m05798（审阅结论）+ m05799（「可以」）。

## D114 组件页改页内分段：运行器 / 图形层

**背景**：D112 把运行器与图形层合成「组件」页签后，两个清单在 680px 窗口里堆叠：运行器 6 行占满首屏，图形层整段在折叠线以下，六个按钮平铺、权重相同，用户看不出「这里是两组东西」。用户 2026-11-23 反馈「组件页还是很奇怪」。

**决定**：
1. 不拆回两个顶层页签（D112 的依据仍成立：这两块都是「给前缀准备的东西」），改为页内分段切换：`Seg_Runners` / `Seg_Graphics` 两个自绘按钮，下方 `Pane_Runners` / `Pane_Graphics` 互斥显示。
2. 选中态用**不透明度 + 字重**表达（`Opacity 1 / 0.55`、`SemiBold / Normal`），不替换 Style——Uno 下运行时替换 Style 不会重算已应用的 `Background`（本次实测），看着像「点了没反应」。
3. 每个分段只保留一个主操作：运行器段「添加路径…」（次要动作收进 `RunnerMore`），图形层段「添加组件…」为主、「管理下载源」次之，其余进 `ComponentMore` 与筛选 ComboBox。
4. 两段说明文字各压成一行；`ExternalRunners` 改「系统 / Steam / 其它启动器（{0}）」；行内「检查」降级为低强调文字按钮（`Background=null`、无边框、12px 次要色），避免每行一个实心按钮形成噪音列。

**代价边界**：分段是页内状态，不参与导航/返回栈；切页签再回来会回到默认的「运行器」段（不记忆上次分段）。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 271 通过；`lint-ui-thread.sh` 通过；真机截图确认点「图形层」后分段标题加粗、运行器段淡出、下方显示图形层清单（`dsh-cu-area-1795436400788.jpg`）。

**确认方式**：用户 m05886 反馈 + m05901「先按你的建议改改看」。

**2026-11-23 修订（用户 m06134「把我们上一步刚处理好的按钮路径问题又弄回来了」）**：分段方案撤回，恢复 D112 的单页两段（运行器在上、图形层在下，整页滚动），保留 D114 中与分段无关的改进（说明压一行、每段一个主操作 + 其余进「更多」、行内「检查」降级为低强调文字、标题不再塞括号）。撤回理由：
1. 分段把「添加组件…」藏进二级切换——用户要的是在组件页直接看到两个清单和各自入口，而不是先点一次分段；这正是 D112 合并页签想解决的反直觉，分段把它以另一种形式引了回来。
2. 分段切换还连带暴露一个宽度 bug：`UpdateContentWidth()` 用「当前可见页的视口宽度」钉住图形层列表，切页签瞬间取到的是**上一个页签**的宽度（实测 784 vs 应有 ~1064），列表缩成一条且被 `HorizontalAlignment` 影响偏向一侧。改为只认 `Page_Components` 自己的视口、切页签后下一帧重算（`DispatcherQueue.TryEnqueue(UpdateContentWidth)`），并在取不到时放弃钉宽度而不是拿别的页签凑。

**2026-11-23 修订 2（用户 m06323「按钮还是和图形层同级，路径依旧反直觉」）**：动作按钮从两个清单里提到**组件页顶部工具条**，两个清单只剩标题、一行说明和列表。

- 依据：组件页的语义是「运行器 + 图形层」两个并列清单，`添加路径…`/`添加组件…`/`管理下载源`/`更多` 是整页动作，不是图形层的子控件；放在图形层标题下会被读成「这是图形层的东西」，这也正是 m05687 抱怨的原始问题。
- 落地：`Page_Components` 内新增顶部 `Grid`（左侧动作组 + 右侧筛选 ComboBox 右对齐）：`Button_AddRunner`、`Button_DownloadComponent`（Accent）、`Button_ManageSources`、`Button_More`（Flyout 合并：设为新方案默认 / 导入安装包 / 导入图形层 / 暂存区 / 清理未使用）；`Pane_Runners`、`Pane_Graphics` 只保留标题 + 一行说明 + `StackPanel_*`。
- `InitializeRunnerButtonTips()` 的 `Button_RunnerMore` 引用改成 `Button_More`；删掉错位的 `D114：组件页内分段` 注释。
- 验证：build 0 错误；真机 1200×704 下工具条一行放下（`dsh-cu-area-1795438496013.jpg`），滚动到图形层确认清单上方无按钮（`dsh-cu-area-1795438524425.jpg`）。

**2026-11-23 修订 3（用户 m06364「把该放到顶部的放到顶部，其他各自的放到各自标题下」+ m06378「下载源不应该是组件的下载源吗」）**：按语义层级重排组件页，取代修订 2 的「全部动作进顶部工具条」。

- 事实先对齐：`ComponentSources.Defaults` 的 5 条源 Kind 为 wine / wine / proton / graphics / graphics，即下载源服务的是**组件整体**（运行器 + 图形层），既不属于图形层，也不属于某一段。
- 层级规则：上位概念（组件、下载源）在页面级；下位概念（运行器、图形层）在段内；段内按钮只说本段名词。
- 落地：
  1. 页面顶部固定一条「下载源」信息条（`FontIcon E896` + `Text_SourcesSummary`「N 个启用 / 共 M 个」+ 一句说明 + `Button_CheckSources`「检查全部」+ `Button_ManageSources`「管理下载源…」），不再藏在任何段里。
  2. 运行器段标题行只放 `Button_AddRunner`「添加运行器…」（原「添加路径…」，名字必须说明加的是什么）与 `Button_RunnerMore`（Flyout 只含「设为新方案默认」）。
  3. 图形层段标题行只放 `Button_DownloadComponent`「添加图形层…」（Accent）、`Combo_ComponentFilter` 筛选、`Button_More`（导入安装包 / 导入图形层 / 暂存区 / 清理未使用）；「管理下载源」移出本段。
  4. 两段标题带数量（`Text_RunnersCount` / `Text_GraphicsCount`，`SectionCount` =「{0} 项」），页面头部只剩「刷新」「打开配置文件夹」。
  5. 「新建方案」从页头下移到方案段左列顶部（`StackPanel` 内 `Button_New` + 列表），删除 `LoadProfiles()` 里重复的内联 HyperlinkButton，空列表时也只有一个入口。
- 暂缓（下一批）：`添加运行器…` 与 `添加图形层…` 弹窗合并为「下载 / 本地文件 / 暂存区」三入口；本次两段仍是各自弹窗。
- 验证：build 0 错误；`dotnet test` 271 通过；`lint-ui-thread.sh`、`lint-cli-help.sh`（A 段）通过；真机 1200×860 截图确认下载源条、运行器段标题行、方案页唯一「＋ 新建方案」（`dsh-cu-area-1795446923280.jpg`）。图形层段因注入滚轮在本环境不生效（设置→高级 同样不滚，属工具限制）未能截图目视，行渲染与运行器共用同一 `CreateGraphicsRow` 路径。

**确认方式**：用户 m06397「先按这个来吧」。

**2026-11-23 修订 4（用户 m06674「继续」，落地「两个『添加…』合并三入口」）**：把两段的主操作从单一路径改成菜单，来源按段过滤。

- 运行器段「添加运行器…」下拉：**下载运行器…**（`ShowDownloadDialogAsync("runner")`，只列 Kind 为 wine / proton 的源）与**选择本地运行器…**（原 `Button_AddRunner_Click`，挑 wine 可执行文件）。
- 图形层段「添加图形层…」下拉：**下载图形层…**（只列 Kind 为 graphics 的源）、**导入本地安装包…**（`ShowImportArchiveAsync("graphics")`，不再弹类型选择框）、**从文件夹导入…**（原有 `Button_ImportGraphics_Click`）。
- 下载弹窗拆成 `ShowDownloadDialogAsync(string kind)` 一个实现 + 两个薄包装：源过滤按 Kind，标题分别是「下载运行器」「下载图形层」；安装逻辑完全共用，没有第二套下载代码。
- 「更多」精简为只放与清单无关的低频动作（下载暂存区、清理未使用），不再重复「导入安装包 / 导入图形层」。
- 与批准方案的一处偏差：**暂存区没有做成「添加」的第三个入口**。查代码后确认 `Button_Staging_Click` 只是列出 `.download/` 里未完成的分片并允许删除（取消 = 暂停，分片留着续传），并不能导入任何东西；把它挂进「添加…」会变成死路。因此「添加」只保留真实的两种来源（下载 / 本地文件），暂存区仍在「更多」里做管理。若以后做「从暂存区续传安装」，再补第三个入口。
- 文案同步：`AddPathTip` tooltip 改为「下载运行器，或把已装好的 wine 可执行文件加进清单；系统里已发现的运行器只读。」，`Runners_Notice` 一并改；新增 `DownloadRunner`/`DownloadRunnerTitle`/`PickLocalRunner`/`DownloadGraphics`/`DownloadGraphicsTitle` 五个键（五套资源）。
- 验证：build 0 错误；`dotnet test` 271 通过；`lint-ui-thread.sh` 通过；真机确认「添加运行器…」下拉两项、点「下载运行器…」后弹窗标题为「下载运行器」且源下拉只剩 wine-staging（图形层源未出现）。

## D115 运行环境审查修复：批 E「数据安全」

**背景**：第二轮全量审查（`28-runtime-env-audit.md` §6，R43–R130）用户 m07141「先提交，然后全部修复，随进度提交」授权。批 E 覆盖 R43 / R44 / R45 / R64，共同点是「静默走偏」：装好的组件其实不可执行、改个名字等于换前缀、坏 JSON 延后爆炸、保存失败仍然绑定。

**决定**：

1. **R43 解压后补执行位**：`ArchiveExtractor` 对 zip 读 `ZipArchiveEntry.ExternalAttributes >> 16` 的 Unix 权限位；tar.xz 走 SharpCompress，反射确认其 `IEntry` 没有 `Mode`（BCL `TarReader` 才有），因此解压完成后统一扫一遍目标目录，按 ELF 文件头（`\x7fELF`）给可执行文件补 `u+x`——只加位、不夺权，文本/脚本不动。下载与导入共用同一条解压路径，两条链路一起修好。
2. **R44 方案名与前缀目录解耦**：`RuntimeProfile` 新增持久化字段 `PrefixSlug`（首次生成后不再变）；`PrefixPath.DefaultPrefixDirectory` 成为唯一的默认目录推导（纯函数、可单测、仍带 R35 的越界保护）；`RuntimeProfileStore.Normalize` 给旧配置回填 slug——回填值就是原来「按名字推导」的结果，所以升级后目录不变；`EffectivePrefixPath` 优先 slug。`ProfileBinding` 的「就地改名」不再动目录。
3. **R45 null 归一**：`Normalize` 里对 `Env` / `Wrappers` / `Name` 做空值兜底（STJ 会把显式 `null` 原样赋给属性，覆盖初始化器）。
4. **R64 保存失败不绑定**：`EnsureFor` 的两处 `RuntimeProfileStore.Save` 改为检查返回值；失败时记 Warning、返回默认方案，不再写入指向不存在 id 的绑定。

**代价边界**：老配置第一次 `LoadAll` 会因补 slug 被重写一次（沿用既有的 `Normalize → changed → Save` 机制）；slug 一旦生成，改显示名不再换前缀目录——想换目录要显式填前缀路径。ELF 扫描只在解压结束后做一次（wine 包几千个文件，实测开销在几十毫秒级）。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` **276 通过**（新增 5 条：zip 权限位、zip 无权限位时的 ELF 兜底、slug 改名不换目录、旧配置按名字回退、垃圾名回退 Id）；`lint-ui-thread.sh` 通过。

**确认方式**：用户 m07141「先提交，然后全部修复，随进度提交」+ 28 文档 §6.11 批次 E。

## D116 运行环境审查修复：批 F「启动链路」

**背景**：`28-runtime-env-audit.md` §6.3 R46–R56，批 E 之后的第二批（用户 m07141「随进度提交」）。共同点是「启动链路上每一步都各自为政」：锁和 wineboot 顺序反了，建前缀的环境和启动游戏的环境不是一套，失败降级后还往全局 `~/.wine` 里装图形层。

**决定**：

1. **R46/R48 锁先于 wineboot，异常必释放**：`CreateGameProcessStartInfo` 先对 `EffectivePrefixPath` 拿锁，再在锁内 `EnsurePrefix`；`EnsurePrefix` 若返回别的路径就把锁换到真实前缀。拿锁之后的整段（图形层装配、环境组装、配方构建）包在 `try/catch` 里，失败即 `ReleasePrefixLock` 再抛——以前任何一个 NRE 都会让该前缀在本进程里永久显示「正被另一个实例使用」。
2. **R47 半成品前缀不再被当成有效**：`CreateWithWineboot` 开工前写 `<prefix>/.lunarward/init.pending`，成功才删；超时时受管前缀（`PrefixRoot` 下）直接删除半成品，外部目录保留标记。`EnsurePrefix` 见到标记就重跑 wineboot；`drive_c` 与 `system.reg` 缺一都视为「不像前缀」。
3. **R49 降级前缀不装图形层**：`EnsurePrefix` 失败后降级到 `~/.wine`/`LUNARWARD_WINEPREFIX` 时，跳过 DXVK/VKD3D 安装（以前会覆盖全局 `system32`，全机 wine 程序受影响），toast 已经提示降级。
4. **R50 Shell / RunOnce 带 gameKey**：`ProfileShellService.CreateShellStartInfo` / `TryOpenShell`、`RunOnceService.CreateStartInfo` / `Start` 增加可选 `gameKey`；设置页两处入口传 `AppConfig.CurrentGameBiz`；CLI `--shell-plan` / `--run-once` 新增 `--game <biz>`（`--run-once` 会把它从附加参数里摘掉）。默认前缀方案在 Shell 里终于能拿到正确的 `WINEPREFIX`。
5. **R55 wineboot 复用 Proton 环境**：`CreateWithWineboot` 在 4 个基础变量之后叠加 `RuntimeEnvironment.ProtonEnvironment(wine)` 与配方 `Env`，最后再写回 `WINEPREFIX` / `WINEARCH`（避免被旧配置覆盖）。建前缀与启动游戏看到同一套 `WINEDLLPATH` / `LD_LIBRARY_PATH` / `PROTONPATH`。
6. **R51–R54 解析层**：`Refresh` 不再把「不在组件表里的运行器」改写成别的 wine（外盘未挂载时保留原配置）；`DefaultPrefixDirectory` 的 Id 回退分支也做 Sanitize + 越界复核；`PrefixPath.Expand` 支持裸 `$HOME`/`${HOME}` 并把相对路径绝对化；`NormalizePrefixArch` 把 `wow64` 归为 `win64`（以前方向反了，建出 32 位前缀，64 位游戏被预检挡下）。
7. **R56 图形层安装事务化**：先全部拷到 `.lunarward/graphics-staging/<kind>`，再逐个「备份旧文件 → 替换」；中途失败按备份逆序回滚并删掉新文件，staging 无论如何清理。`Sizes` 缺失时不再算通过（同时要求文件非空）；`CollectFiles` 先判组件目录存在，批量安装不会因一个目录消失而中断。

**代价边界**：`--shell-plan` 的输出在没有 `--game` 时与以前相同（WINEPREFIX 为空），给了 `--game` 才解析真实前缀；`init.pending` 标记会让「上次超时后干脆手工修复了前缀」的用户多跑一次 wineboot，但不会删外部目录。回滚只覆盖本次写入的文件，历史残留仍由「换版本时删除 stale」处理。

8. **顺带修（L-169）**：`ClipboardHelper.Linux.cs` 启动 `wl-copy` 时补 `RedirectStandardOutput/Error`。`wl-copy` 写完剪贴板会 fork 守护进程继续持有 selection，继承来的 stdout 让 `cli-smoke` 里 `out=$(lunarward …)` 等不到 EOF、整套挂死；CI（xvfb，无 wl-copy）不暴露，本机 Wayland 必现。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test` 276 通过；`lint-ui-thread.sh`、`lint-cli-help.sh`（A 段 51 开关）通过；`cli-smoke.sh` **40 通过 / 0 失败 / 1 跳过**（跳过的是需真实网络的 `--version-status`）；`cli-extra.sh` **32 通过 / 0 失败**。

**确认方式**：用户 m07141 + 28 文档 §6.11 批次 F。

## D117 运行环境审查修复：批 G「下载与导入」

**背景**：`28-runtime-env-audit.md` §6.4 R57–R63。共同点是下载/导入只覆盖「源端守规矩」的路径：服务器忽略 Range、代理截断连接、用户点暂停、归档顶层多一层目录、摘要换了算法、资产名带路径分隔符、并发下载同一资产——每一种都会把错误变成「白耗流量」或「看起来装好了但列表里没有」。

**决定**：
1. 分片必须回 206 且 `Content-Range` 起点匹配，否则立刻抛 `RangeRejectedException` 降级，不再「先写 20 GB 再说」；拷贝按请求长度截断。
2. 用户暂停/空闲超时取消时保留分片（R58 与 UI「已暂停，分片已保留」一致），只有非取消失败才清理。
3. 图形层判定抽成 `GraphicsLayouts`，导入、落盘复核、组件发现三处共用；导入后复核失败就回滚目录。
4. 解压上限统一：总字节按「归档 + 512 MB 起、25 倍压缩比、8 GiB 封顶」，另加 500 000 条目与 4 GiB 单文件上限，zip/tar/tar.xz 全走同一套。
5. digest 只认 `sha256:`，其它算法置空并在日志里说明（避免把 `sha512:` 当 sha256 比）。
6. 资产名先 `GetFileName` 归一化并拒绝路径分隔符/点开头，再用 `.download/<name>.lock`（`FileShare.None`）做跨进程互斥。
7. 放弃 `HttpClient.Timeout = 30 min`，改 60 秒无进度看门狗：大包不再被总时长掐断，卡住的连接也不会永远挂着。

**代价边界**：看门狗是「无进度 60 秒」而不是「总时长上限」，源端持续慢慢挤字节时会一直下（用户可暂停）；压缩比上限对极端高压缩比的合法归档可能误杀，错误信息会写明上限值。

**验证**：`dotnet build` 0 错误；`dotnet test` **280 通过**（新增 `GraphicsLayoutsTests` 4 条）；`lint-ui-thread.sh` 通过；`cli-smoke` 40/0/1、`cli-extra` 32/0。

**确认方式**：用户 m07141「先提交，然后全部修复，随进度提交」；批次划分见 `28-runtime-env-audit.md` §6.11。

## D118 运行环境审查修复：批 H「方案、绑定与持久化」

**背景**：`28-runtime-env-audit.md` §6.5 的 R65–R76（12 条）集中在方案复制、绑定落盘与配置读取上，症状都是「配置看起来没错，但不生效」或「静默丢配置」。用户 m07141 批准全量修复、按批提交。

**决定**

1. **R65**：新游戏自动建方案改用 `RuntimeProfile.CopyWithNewId()` 逐字段克隆（含 `Hud/HudOptions/GameMode/GameScope/GameScopeArgs`），并加反射测试——以后新增字段忘了复制会直接红，而不是再让 HUD 静默消失。
2. **R66**：旧目录迁移时目标文件已存在，不再删源，改名 `<file>.migrated-conflict-<ts>` 并 `LogWarning`。
3. **R67**：`LegacyGraphicsId` 扫不到组件时保留 id（只记日志、不落盘），外置盘未挂载不再永久清空用户选择。
4. **R68**：默认方案改为显式标记 `RuntimeDefaults.DefaultProfileId`；方案详情页 ⋯ 菜单加「设为默认环境」，默认方案在标题旁显示徽标。未标记时退回排序首项（兼容老配置）。
5. **R69**：方案名统一走 `RuntimeProfile.NormalizeName`（trim、折叠空白、去路径分隔符与控制字符、限长 64），保存时自动去重（` (2)`、` (3)`…），避免重名方案共用前缀目录。
6. **R70/R71**：`ProfileBinding.Set` 改为返回 bool、不再抛异常（`async void` 里抛即崩）；绑定文件损坏时 `IsReadable` 返回 false，删除方案流程先停下并说明原因，`ClearFor` 返回 -1 而不是 0。
7. **R72/R73**：id 比较全链路 `Ordinal`；新方案 id 从 8 位十六进制改为 16 位（旧 8 位文件照常读取）。
8. **R74/R75/R76**：读取配置开 `PropertyNameCaseInsensitive`；`SchemaVersion` 高于当前值时跳过加载并保留文件；只有 `JsonException` 才把文件改名留证，`IOException/UnauthorizedAccessException` 保留原文件；诊断包 `bindings.json` 改为导出 `{path, lastError, ok, error, bindings, raw}`。

**代价边界**

- 名字上限 64，重名自动加序号；用户仍可在编辑器里改成想要的显示名（保存时同样过规范化）。
- 16 位 id 只影响新建方案；已有 8 位 id 的方案不改名、不迁移（文件名仍是真值）。
- 「默认环境」是显式选择，不再随 locale / 改名漂移；旧配置没有标记时行为与以前一致（排序首项）。

**验证**：`dotnet build src/Lunarward/Lunarward.csproj -c Debug` 0 错误；`dotnet test Starward.slnx` **286 通过**（新增 `CopyWithNewId` 正反两条 + `NormalizeName` 4 例）；`lint-ui-thread.sh` 通过；`cli-smoke` 40/0/1；`cli-extra` 32/0。

**确认方式**：用户 m07141「先提交，然后全部修复，随进度提交」。

## D119 运行环境审查修复：批 I「UI 线程与资源」

**背景**：`28-runtime-env-audit.md` §6.6 的 R77–R85 与 §6.7 的 R93（共 10 条）集中在「界面上不该做的事」：同步 fork wine、在 UI 线程全盘扫描、读路径写盘、键盘处理器解挂不掉、`async void` 抛到同步上下文、每个请求新建 `HttpClient`。症状是卡顿、偶发崩溃和难以复现的状态错乱。用户 m07141 批准全量修复、按批提交。

**决定**

1. **R77**：`Button_AddRunner_Click` 的 `ReadRunnerVersion` 改到线程池执行——它是同步壳（内部 `GetAwaiter().GetResult()`），wine 挂住时最多冻 4 秒界面。
2. **R78/R79**：打开编辑器不再在 UI 线程同步扫描：`OpenEditorAsync` 先 `Task.Run` 取运行器/图形层列表，再 `BuildEditor` 建控件；详情区与启动预览只读 `ComponentStore.CachedRunners/CachedGraphics`，冷缓存时退化成 Id/路径，不在建行时触发扫描。
3. **R80**：`RuntimeProfileStore.LoadAll` 不再回写文件（此前发现「读配置顺手 Save」，而 LoadAll 会被设置页/游戏设置页在 UI 线程直接调用）；规范化只发生在内存里，下一次显式保存才落盘。
4. **R81**：`RuntimeEnvironmentSetting` 在 `OnUnloaded` 里 `RemoveHandler` 解挂窗口级快捷键处理器，键处理守卫改为 `Visibility && IsLoaded && XamlRoot != null`。
5. **R82**：清理前缀、从前缀移除图形层改 `await Task.Run(...)`——和 2863 行「删除组件」保持同一套后台删除，不再冻界面。
6. **R83**：组件页加载加代次号，后发起的加载会让前一次结果作废（不再出现两份列表交替 `Clear/Add` 的闪烁）；`_ = LoadComponentsAsync()` 的异常不再被吞，记日志并 toast。
7. **R84**：`ComponentStore` 的运行器/图形层缓存加锁与代次号：扫描期间被 `Invalidate()` 的结果会被丢弃，不会把过期列表写回缓存。
8. **R85**：新增 `Helpers/HttpClients.Probe` 共享客户端（20 秒超时，调用方不 Dispose）；源探测与「检查全部源」最多 4 路并发（以前 `Task.WhenAll` 全量打出去，反而更容易撞 GitHub 403）。下载客户端仍单独创建（需要 `InfiniteTimeSpan` + 60 秒无进度看门狗）。
9. **R93**：`Tab_Click`、`Button_RunOnce_Click`、`Button_EditorBack_Click`、`Button_Chip_Click`、`Button_SetDefaultRunner_Click`、`Button_Sources_Click`、`Button_ManageSources_Click`、`Button_EnhanceSave_Click` 全部补 try/catch——`async void` 里抛出会直接打到同步上下文，等于进程崩。

**代价边界**

- 编辑器打开时先出现空白再填充（原本是「卡一下后一次出现」）；扫描期间用户可能看到列表还没建好，这是把 0.6 秒的冻结换成可感知的加载。
- 详情区/启动预览在缓存冷时显示 Id 或路径，而不是真实运行器名；页面首载后（`LoadComponentsAsync` 完成）即恢复。
- `LoadAll` 不再顺手落盘：如果用户手工改坏了 json 的某个字段，规范化结果要到下一次保存才写回磁盘（内存里始终是规范化后的值）。
- 源探测并发上限 4 是经验值；源多于 4 时「检查全部」耗时线性增加。

**验证**：`dotnet build src/Lunarward/Lunarward.csproj -c Debug` 0 错误；`dotnet test Starward.slnx` **286 通过**；`lint-ui-thread.sh` 全部通过；`cli-smoke` 40/0/1；`cli-extra` 32/0。

**确认方式**：用户 m07141「先提交，然后全部修复，随进度提交」。

## D120 运行环境审查修复：批 J「交互与状态」

**背景**：`28-runtime-env-audit.md` §6.7 的 R86–R92 与 §6.11 批 J 的 R94–R97（共 11 条）都在「点下去之后会发生什么」：22 处 `ContentDialog.ShowAsync()` 只有 2 处加锁、管理下载源弹窗里再叠一层、图形层 chip 跳错页、定义好的 tooltip 从没调用、「未使用」和「被 1 个方案使用」同屏、编辑器脏了切方案静默丢弃、`_saveThenRun` 泄漏到下一次保存、`RunnerPath` 比较一个归一化一个不归一、「被几个方案使用」每行读一次盘、「占用汇总」不看是哪个前缀、HUD 首项在英文里叫 Close。用户 m07141 批准全量修复、按批提交。

**决定**

1. **R86**：新增 `Helpers/DialogGate.Show(ContentDialog)`——静态 `Interlocked` 锁，已有对话框打开时直接返回 `ContentDialogResult.None`（调用方按「取消」处理），`finally` 复位；20+ 处裸 `ShowAsync()` 全部改走它。`ContentDialog` 实例一律每次新建（同一个实例不能二次 `ShowAsync`）。
2. **R87**：「管理下载源」里的编辑 / 删除 / 新增 / 恢复默认改为 `RunNestedAsync(action)`：先 `Hide()` 管理窗、`DialogGate.WaitClosedAsync()` 等门闩真正放掉（`Hide()` 到 `ShowAsync` 续体之间是异步的，不等就会被子对话框当成「已有对话框」拒掉）、执行动作、`Rebuild` 后**新建一个实例**把管理窗开回来（同一实例不能二次 `ShowAsync`，页面元素先从旧 `ScrollViewer` 摘下）。照同文件「下载弹窗 → 管理来源」的做法，不再叠第二层。
3. **R88**：chip 的 tag 恢复 0/1/2 语义——图形层 1（组件页）、前缀 2（前缀页）。
4. **R89**：构造函数 `InitializeComponent()` 之后调用 `InitializeRunnerButtonTips()`，D113 的 4 个 tooltip 生效。
5. **R90**：前缀卡片的「未使用」只在 `prefix.Managed` 时补，和 `infoText` 的「被 N 个方案使用」不再自相矛盾。
6. **R91**：脏状态扩到增强页——`Enhancements.cs` 记 `_enhanceSnapshot`（HUD 开关 / 选项 / gamemode / gamescope / 参数五行拼串），`OpenEnhance` 回填后取快照、保存成功后刷新快照；`ConfirmLeaveEditorAsync` 改为 `EditorDirty || EnhancementsDirty` 才确认；`Button_ProfileList_Click` 与 `Button_EnhanceBack_Click` 都会先过这道确认。
7. **R92**：保存处理器入口就 `runAfterSave = _saveThenRun; _saveThenRun = false;`，`RunOnceAsync` 移进独立 `try/catch`，失败报「运行一次失败」而不是「保存失败」——文件已经落盘了。
8. **R94/R95**：新增 `ComponentPathKey` / `SameComponentPath`（`ExpandPath` → `GetFullPath` → 去尾斜杠 → `Ordinal`）与 `BuildRunnerUsage`（一次 `LoadAll` 建「路径 → 方案数」字典）；删除组件与「清理未用组件」都改用 `SameComponentPath`，运行器行的使用计数改查字典，删掉每行的 `CountProfilesUsingRunner`。
9. **R96**：`Button_PrefixUsage_Click` 先看 `sender.Tag`——带 `(PrefixInspection, List<string>)` 时只列该前缀的方案（标题「使用这个前缀的方案」），无 Tag 才走全站占用汇总。
10. **R97**：新增资源键 `Lunarward_RuntimeEnv_HudOff`（en `Off` / zh-CN `关闭` / zh-HK / zh-TW `關閉`），HUD 首项不再显示 Close；`Combo_Hud` 补 `Header="HUD"` 并删掉上方硬编码 TextBlock；无选中方案时点保存 toast `NoProfilesHint`，不再静默。

**代价边界**

- `DialogGate` 是进程级单锁：真正需要两个对话框同时存在（比如把某个确认框改成非模态）时，第二个会被直接拒掉，需要改成返回值区分「被拒」与「用户取消」。
- 嵌套流程把管理窗 Hide 再重开，滚动位置与选中项会重置（列表本来就随动作重建）。
- 增强页快照是纯字符串比对，输入框里只改空白也会被当成「有改动」，会多问一次。
- 路径归一化后，`~`/尾斜杠/`..` 写法不同的历史配置会开始被识别为「在用」，可能让个别「以前能删」的组件变成「删不掉」——这是有意的方向。
- `BuildRunnerUsage` 仍然在 UI 线程做**一次** `LoadAll`（N 次变 1 次）；方案数极多时这一次读盘仍在 UI 线程，属于 §6.10 第 2 条（统一 snapshot）留待批 K。

**验证**：`dotnet build Starward.slnx -c Debug` 0 错误；`dotnet test Starward.slnx` **286 通过**；`lint-ui-thread.sh` 全部通过；`cli-smoke` 40/0/1；`cli-extra` 32/0。

**确认方式**：用户 m07141「先提交，然后全部修复，随进度提交」。

## D121 运行环境审查修复：批 K「一致性 / 文案 / 无障碍 / 死代码」

**背景**：`28-runtime-env-audit.md` §6.8/§6.9 的 R98–R130（33 条，Medium 20 + Low 13）是第二轮审查的尾巴：符号链接与路径比较、缓存与重试、锁与 `InvariantCulture`、环境变量键与参数拆分的边界、前缀写侧写、可执行位、字体族、无障碍名、死键与死代码。用户 m07141「先提交，然后全部修复，随进度提交」覆盖本批。

**决定**

1. **R98**：`ComponentStore.IsUnder(folder, path)` 统一「受管」判定——两边 `TryRealPath`（解符号链接）+ `GetFullPath` + 去尾分隔符，比较时要求落点是分隔符而不是字符串前缀。替换 5 处裸 `StartsWith`。
2. **R99**：`ManualRunners.Add/Remove` 写盘后 `ComponentStore.Invalidate()`，手动添加的运行器下一次扫描就出现。
3. **R100**：`FindWineserver(winePath, allowPathFallback)`；受管组件（`runner.Managed`）只在同目录找 `wineserver`，PATH 兜底只留给系统/手动添加的，避免「缺 wineserver」被本机别的 wine 冒充。
4. **R101**：MangoHud 不再当必需二进制——实际只靠 `MANGOHUD=1` 加 Vulkan 隐式层，改为 `Enhancements.HasMangoHudLayer()` 扫三处 `implicit_layer.d` 与 `VK_ADD_IMPLICIT_LAYER_PATH`；`MissingRequirements()` 抽出来，预检与设置页共用文案。
5. **R102**：`ComponentNaming.Sanitize` 保留 Unicode 字母数字（俄文/中文游戏名不再变 `-`），超 80 字符截 71 + `-` + SHA256 前 8 位，避免长名截断后撞名。
6. **R103**：源只持久化**差异**——`SaveUserEntries` 过滤 `SameAsDefault(entry)`，内置源不因保存而固化成用户副本，墓碑（`Removed`）不会丢。
7. **R104**：`IsArchive` 复用 `ArchiveExtractor.IsSupported`（`.zip` 也算），`PrefersZstd` 只对 `.tar.zst` 为 true（`.tar.gz` 不是 zstd）。
8. **R105**：资产缓存 64 项上限 + 过期/最旧淘汰；请求读 `X-RateLimit-Remaining/Reset` 记日志；5xx 退避 600 ms 重试一次，4xx 不重试，网络异常翻成「连不上源：…」。
9. **R106/R107**：`OutputCapture.Redact` 改成「保留键、替换值」（`Authorization: Bearer xyz` → `Authorization=***`）；日志目录跟随 `XDG_DATA_HOME`（自带 `DataHome()`，不依赖主程序的 internal 助手），子进程管道显式 UTF-8，`Attach` 末尾后台 `WaitForExit` 把管道尾部冲干净。
10. **R108**：`WINEDEBUG=-all` 用 `TryAdd`，用户配方里写了就不覆盖；`PrependLibraryPath()` 收敛「前置 + 保留旧值」一份实现，启动链与 CLI 共用。
11. **R109/R110**：env 键按 `^[A-Za-z_][A-Za-z0-9_]*$` 校验，已知键大小写归一（`wineprefix` 与 `WINEPREFIX` 变一个键），非法键记进 `LaunchRecipe.InvalidEnvironmentKeys` 供预检提示；游戏参数与配方参数各自 `SplitArgs` 后再拼 `argv`，引号不再跨层。
12. **R111**：`LastKnownGood` 读-改-写加进程锁，记 `EffectivePrefixPath`（不再记 null 表示「默认前缀」），`RunnerVersion` 走 `CachedRunnerVersion` 不 fork wine；时间戳 `InvariantCulture`。
13. **R112**：启动历史追加 + 压缩在同一把锁里，`FileMode.Append` + 短重试吸收并发；展示时间与时长全部显式 `InvariantCulture`（de-DE 的 `1,5 s`、th-TH 的佛历年份都不再出现）。
14. **R113**：「已安装」判定改 `NormalizeVersion`（去 `v` 前缀）+ `OrdinalIgnoreCase` 精确相等，只允许段边界匹配，装了 `10.0` 不再把 `1.0` 当已装。
15. **R114/R115/R116**：`BindingCount` 只数非空白值；`RuntimeProfileStore.Delete` 对不存在的文件返回 false（调用方提示 `ProfileFileMissing`，不再假装删成功）；`RuntimeDefaults` 写失败弹 toast，载荷加 `SchemaVersion = 1`。
16. **R117**：`Mirror` / `、` / `Selected` 三类硬编码文案进资源键（`FormMirror` / `ListSeparator` / `TabSelected`）。**字号统一不做**：76 处都是 code-behind 现造 TextBlock 的字号数字，收敛成 Style 需要逐个赋 Style 且与主题样式叠加冲突，收益仅代码整洁。
17. **R118/R119**：删 43 个无引用语言键；删 `ParseWrappers`/`ParseEnv`/`_editorWrappers`/`_enhanceProfiles`、四个无订阅按钮（克隆给游戏、导入包总入口、旧来源入口、镜像测速）、`ShowImportArchiveAsync` 的类型选择分支（收窄成 `ShowImportArchiveGraphicsAsync`）。
18. **R120**：页签数与页签序号的注释改对；`PrefixManager` 文件头不再列已删除的模式；`PeArchitecture` 拆开叠在一起的 summary，`NormalizePrefixArch` 注明「判断已有前缀能否跑 exe 用 `KnownArch`」；删无调用者且与 `EffectivePrefixPath` 冲突的 `GraphicsInstaller.ResolvePrefixPath`。
19. **R121/R122/R123**：`TryGetExistingPrefix` 去掉没用的 `gameKey`；`WriteMetadata` 早退条件补 `PrefixArch` 比较，改架构会更新侧写；`IsUnderPrefixRoot` 走 `ComponentStore.IsUnder`，`EnsurePrefix` 两条日志分别对应「父目录在」与「父目录也不在」，`ClonePrefix` 对空目录名抛 `ArgumentException`、等待加 10 分钟超时并杀进程树。
20. **R124**：`Enhancements.IsExecutableFile()` 检查 Unix 可执行位，`FindInPath` 与 `ProfileShellService.FindExecutable` 共用——预检说 Ok 就真能启动。
21. **R125**：系统 Wine 只认 `/usr/bin`、`/usr/sbin`、`/bin`、`/sbin`（`/usr/local/bin/wine` 不再冒充系统 Wine）；`Origin` 认得 `~/.local/share`、`/opt`、`/run/media/<user>`、`/media/<user>`、`/mnt`，同名 Wine 不再混成一个。
22. **R126**：Steam 库路径被丢弃时记日志（不再静默），`IsProtonFolder` 的注释改对（GE-Proton 不以 `Proton` 开头，两条前缀都要判）。**VDF 库存在性探测不做**——要扫整个 Steam 目录，属于新功能，另开条目。
23. **R127**：`system.reg` 读到第一个 `[` 段落就停（上限 2000 行），`#arch=` 不再被前 32 行卡住；新增 `KnownArch()`（win32 / win64 / 未知），`Conflict` 用已知架构判断，ARM64 + win32 前缀会报冲突；探测失败不再一律当 win64。
24. **R128**：`CopyProfile` 复制 `LegacyGraphicsId`；自检结果按运行器记一次（`_autoCheckedRunners`），每次保存不再重跑 `wine --version`；自检 toast 等「已保存」那条消失后再弹（`afterSaveToast`）。
25. **R129**：去掉 Linux 上不存在的 `Consolas` / `Consolas, monospace`（整段不渲染）与 `Segoe MDL2 Assets`（⋯ 字型缺失），省略号改文本、预览区用主题等宽样式。
26. **R130**：三个页签、列表状态点、详情状态点补 `AutomationProperties.Name`；`·  ·  ·` 分隔线名字置空（Uno 没有 `IsInAccessibleTree`），读屏不再念「点 点 点」。

**代价边界**

- `IsUnder` 会解符号链接：用户把前缀软链到别的盘时，「受管/非受管」判定按真实路径算，这是有意的（否则受管组件可被软链绕过）。
- R103 只存差异后，改过内置源字段的条目仍会整条落盘，`SameAsDefault` 逐字段比较，新增默认字段时需要同步更新这个比较。
- 资产缓存上限 64 对「一次预下载很多版本」会开始淘汰旧资产，再触发一次下载；限流头只记日志，不做自动等待。
- `_autoCheckedRunners` 是进程内记忆：重启后每个运行器再自检一次（结果只影响提示，不影响启动）。
- 自检 toast 延迟 3.5 秒，保存特别快时用户会觉得「自检结果慢半拍」，换来不叠两条 InfoBar。
- `KnownArch` 只认 win32/win64/wow64，其它值一律「未知」，`Conflict` 对未知不再报冲突（宁可不报也不错报）。

**验证**：`dotnet build Starward.slnx -c Debug` 与 `-c Release` 均 0 错误；`dotnet test Starward.slnx` **305 通过**（新增 `MergeEnvironment_DropsInvalidKeysAndCanonicalizesKnownOnes`、`BuildArgv_SplitsGameAndRecipeArgsSeparately`、`Sanitize_KeepsUnicodeLetters`、`Sanitize_TruncatesLongNamesWithHash`、`TryDetectPrefixArch_ReadsArchBeyondFirst32Lines`、`TryDetectPrefixArch_StopsAtFirstSection`、`OutputCaptureTests` 的 7 条断言）；`lint-ui-thread.sh` 全 PASS；`cli-smoke` 40/0/1；`cli-extra` 32/0。

**确认方式**：用户 m07141「先提交，然后全部修复，随进度提交」；m00600「后续记得随时更新进度」。

### D122 · CI 覆盖率门限口径修正与真实用例补齐（2026-11-24）

**背景**：`e18cf39`（D121 批 K）推送后 CI 只有 **Coverage (L1)** 失败：`行覆盖 82.03%（3942/4805）` 低于 85%。回头用 git worktree 逐个提交跑 `coverage.sh`，发现从 `83ebeb1`（D116 批 F）起就一直在 84.5%–84.8%，门限是勉强过的；批 K 让它彻底掉下去。查报告时又发现更根本的问题：覆盖率口径把**测试代码自己**算进了分母。

**决定**

1. **口径修正**：`tests/coverlet.runsettings` 加 `<ExcludeByFile>**/tests/Lunarward.Tests/*.cs</ExcludeByFile>`，报告只统计 `src/Lunarward/`。
   - 为什么必须改：测试工程用「链接源码」编译（D37），`IncludeTestAssembly=true` 不得不开；但这样一来 26 个测试文件（4312/4348 = 99.17% 覆盖）也进了分母，占约三分之一。历史报告里的 82%–88% 全是掺水数——真实 src 只有 **81.58%**（2454/3008），加测试会同时抬高分母里最容易被覆盖的那部分，等于自己给自己刷分。
2. **门限数值不动**：`coverage.sh` 与 CI 仍是 `85`。修正口径后靠补**真实用例**达标（实测 85.30%），不用「调低阈值」掩盖问题。
3. **新增用例**（305 → **379** 条）：`ComponentSourceFetchTests`（24 条：JSON 解析、缓存命中/淘汰、ETag 条件请求、5xx 重试一次、限流头、网络异常、ProbeAsync/ProbeAllAsync、源覆盖写盘）、`RuntimeStorageTests`（18 条：输出捕获落盘/过滤/Attach、启动历史往返/压缩/坏文件、InvariantCulture 时间与时长）、`RunnerNamingTests`（17 条：系统目录判定、`/opt`/`~/.local/share`/`/run/media`/`/media`/`/mnt`/家目录的来源段、`WithOrigin`）、`EnhancementsTests`（+6 条：`HudText`、`RequiredBinaries`、MangoHud 层探测、`IsExecutableFile`、`Describe` 各分支）、`GameVersionMarkerTests`（+2 条）、`PrefixPathTests`（+2 条）。
4. **顺带修的真 bug**：`ComponentSources.ListAssetsAsync` 从没把响应头里的 ETag 写进资产缓存（写的是进函数时的 `cachedEtag`，首次必为 null），于是 `If-None-Match` 永远发不出去、304 分支是死代码。改为在响应处取 `response.Headers.ETag` 并入缓存。
5. **口径写进文档**：`19-test-plan.md` 新增「覆盖率口径（D122）」小节，写明分母 = 链接集、不含 `tests/`、不覆盖未链接的主程序代码；改口径要同时改该节与 D122。

**代价边界**

- 分母只含 24 个链接进测试工程的文件（3008 行），主程序里没链接的 UI/服务代码（6000+ 行）不在统计内——覆盖率因此**不代表全仓**，只代表「可单测的那部分」。
- 排除测试文件后历史数字不可直接比较（88.97% → 85.30% 是口径变化，不是覆盖退化）。
- MangoHud 层探测会扫系统目录，测试机装了 MangoHud 时「缺层」分支断言不到，相关用例按实际探测结果分支（`HasMangoHudLayer` 的正向判定稳定）。
- 补的这些用例大量走真实临时目录（`XDG_DATA_HOME` / `XDG_CONFIG_HOME` / `/tmp`），跑得比纯逻辑用例慢，全量从 ~1.2 s 涨到 ~1.5 s。

**验证**：`bash scripts/test/coverage.sh 85` → `行覆盖 85.30%（2566/3008）  分支覆盖 78.84%  类 52` PASS；`dotnet test Starward.slnx` **379 通过**；`lint-ui-thread.sh` 全 PASS。

**确认方式**：用户 m00928「先等一下，CI失败了，看看怎么回事」；随后在选项里选「排除测试文件 + 再补真实用例到 85%」。

## D123 背景视频：VAAPI 硬解走媒体选项 + 合成零拷贝（2026-11-24）

**背景**：用户报「Lunarward 常驻 1.2 核」。定位到背景视频壁纸：`Features/Background/VideoBackgroundPlayer.cs` 用 libvlc `--vout=vmem` 软件解码 1920×1080 VP9 30fps，每帧再经「Skia 画 `_composed` → `Marshal.Copy` 到 `_pixels` → `WriteableBitmap`」两次整帧拷贝。代码里原有一句结论「加 `--avcodec-hw=vaapi` 没收益」，实测是参数传错层级（见 L-172）。

**决定**：

1. 硬解选项改在 `Media` 上：`media.AddOption(":avcodec-hw=vaapi")`；不探测驱动，靠 VLC 自己回退。
2. 留开发/回退开关 `LUNARWARD_VIDEO_HWDEC=0`（默认开），个别驱动花屏时用户可自行退回软解，不必等发版。
3. 合成缓冲 `_pixels` 在 `Start()` 里分配并 `GCHandle` 固定，`Display()` 用 `SKSurface.Create(info, ptr, rowBytes)` 直接画进去；不再需要 `_composed` 中间位图，省掉每帧一次 8.3MB 的 `Marshal.Copy`。
4. 无主题图（`_overlay is null`）时仍走 `Marshal.Copy`——那条路本来只有一次拷贝，再套 Skia surface 反而更慢。

**代价边界**：

- NVIDIA 的 VAAPI 是 NVDEC 解码后下载回内存，省不掉 Skia 合成与上屏，所以到不了「接近 0 核」；要零拷贝得给 Uno 塞原生视频 sink，不在本次范围。
- 每帧新建一个 `SKSurface`（raster，包在已有缓冲上）会带来一次小对象分配；实测 20 秒窗口内 GC 无异常增长（RSS 稳定，`.NET BGC` 10–11% 与改动前持平）。
- `vaapi_drm` 需要 `/dev/dri/renderD128` 可访问；沙箱/Flatpak 环境下会回退软解，行为等同改动前。
- 显式写 `vaapi` 意味着 Intel/AMD/NVIDIA 都走 VA-API（三家在 Linux 上都是这个抽象）；只用 Vulkan-only 驱动的极端情况会回退软解。

**实测**（真机 RTX 3050 Mobile，1920×1080 VP9 壁纸，窗口 1500×845，20 秒 `/proc` 采样）：

| 版本 | 平均 CPU | 主要线程 |
| --- | --- | --- |
| 改动前（软解 + 两次拷贝） | **112%** | `av:vp9:df0` 58%，渲染 21% |
| 改动后 + `LUNARWARD_VIDEO_HWDEC=0` | **100%** | `av:vp9` 6 线程合计 ~79% |
| 改动后（默认，VAAPI） | **74%** | `av:vp9:df0` 42%，渲染 22% |

**确认方式**：用户 m01253「试试 VAAPI（或者说硬解码），其它三点都不符合要求」，m01364「可以」。