# 决策记录（ADR）

- **状态**：生效中（D0–D28）
- **最后更新**：2026-11-01
- **规则**：本文件是唯一施工依据。新增决策只能追加；变更必须记录「变更原因 + 影响范围 + 受影响文档」。
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
| 上游同步流程未定（Q-003） | 已写入文档 | `upstream` 远程指向 Scighost/Starward，`upstream-base` 记录移植基线 `df8ae07`；同步时 `git fetch upstream && git merge upstream/main`（冲突只在 `src/Starward/`，`src/Lunarward/` 不受影响），随后按 `12-shims-and-gaps.md` 复核 55 条排除与 7 处 XAML 差异 | 每次上游发版时执行 |
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


**P1 落地（2026-11-12，提交 `?`）**：三段落成且真机核对通过。

- 数据层：`PrefixManager.PrefixInspection` + `Inspect(path)`（`Exists`/`HasDriveC`/`HasRegistry`/`Locked`/`SizeBytes`/`Managed`/`GameKey`/`ProfileId`/`Mode`）+ `EnumerateKnownPrefixes(profiles)`；目录大小带 10 分钟缓存（`ConcurrentDictionary<string,(long,DateTime)>`），`IgnoreInaccessible` + 跳过 reparse point，单个文件读不到不影响整页。
- 运行器：`ComponentStore.ManualRunners`（`~/.config/lunarward/manual-runners.json`）作为第五段扫描来源，`[添加路径…]` 选文件 → `ReadRunnerVersion` 探测 → 落盘 → 刷新；行徽标追加「N 个方案在用」（现算 `profile.RunnerPath` 与组件路径全等比较）。
- 方案：沿用现有行 + 旧编辑对话框（P2 再改独立页）。
- 前缀：单独成段，行显示 `模式 · 游戏` / `大小 · N 个方案使用 · 状态` / 绝对路径 / `[打开目录]`，仅「我们的 + 无人引用 + 未被占用」才出现 `[清理]`（二次确认）；外部前缀只读。
- 未做（按分期）：方案独立编辑页（P2）、图形层分组与安装/移除/校验（P3）、`lastLogin` 徽标（P4）、SHA 去重（P3）。
- 代价：前缀大小首次进页会遍历一次（本机 `.wine` 370 MB 约 0.2 s）；新文案仍是中文字面量（沿用 D61 口径，i18n 时统一收）。
