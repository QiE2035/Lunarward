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
| 2026-11-03 | 新增 D41 chunk 级复用（增量更新主力机制） | 实测差分只覆盖 101 文件，需把剩余变更压到官方量级（m08787「要」） | `00` D41、`src/Lunarward/Features/GameInstall/Sophon/ChunkReuse*.cs`、`InstallPipeline.RunChunkReuseAsync` |
| 2026-11-05 | 新增 D44 全量审查后的健壮性加固（原子写、只读预检、空间/目录校验、前缀清理挂 wine 会话、单实例回调回 UI 线程、CLI 失败退出码、日志保留） | 用户 m10479「能修的就都修了吧」；审查清单见 m10478 汇报 | `00` D44、`13` L-75–L-81、`12` |
| 2026-11-04 | 新增 D43 主窗口 chrome 与背景切换的 Linux 形态 | 用户真机反馈 #1/#3/#5，Uno/X11 无对应系统行为 | `00` D43、`13` L-69–L-74、`12` |


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
