# 路线图与门禁

- **状态**：v0.1（按 D1/Δ7「先能玩、后好看」重排）
- **最后更新**：2026-11-01
- **真机验收游戏**：原神 `hk4e_cn`、绝区零 `nap_cn`（D15 / m00119）

---

## 0. 里程碑总览

| 里程碑 | 主题 | 目标 | 门禁 |
|---|---|---|---|
| **M0** | 可行性门禁 PoC | 证明「Uno + Linux + wine 直启 + 登录」四件事成立 | 6 项硬门禁全过 |
| **M1** | 能玩（核心闭环） | 两款真机游戏可启动、可游玩、可退出；运行环境引擎可用 | 真机 10 分钟不崩 |
| **M2** | 能用（体验补齐） | 设置/战绩/抽卡/时长/托盘/通知/更新可用 | 日常使用一周无阻断 |
| **M3** | 好看（保真打磨） | 视觉与交互对齐上游；`Lunarward.Codec` 移植完成 + Win2D 绘制层换 SkiaSharp | 视觉清单全过 |
| **M4** | 增强（可选） | Overlay/截图捕获/手柄/伴随启动进阶 | 单独评估 |

---

## M0 · 可行性门禁 PoC

**范围**：只验证，不追求功能完整；允许硬编码。

**交付物**：

1. `poc/uno-xaml`：把 6 个代表性 XAML 页面在 Uno Linux 下渲染（见 `06-risks.md` R-01 取样清单），附截图；
2. `poc/webview-login`：`CoreWebView2` 兼容层或备选方案，走通「打开登录页 → 登录 → 取到 cookie」；
3. `poc/wine-exec`：`lunarward-cli run` 的最简实现，`exec` 链启动 `hk4e_cn`，`ps` 验证游戏父进程是 `wine`；
4. `poc/prefix-reflink`：模板克隆 1 次，记录耗时与体积；
5. `poc/tray-notify`：D-Bus 托盘图标 + 一条通知；
6. 缺口清单：Uno 缺失/异常控件表 + 替代方案。

**门禁（全部通过才进 M1）**：

| # | 判据 |
|---|---|
| G0-1 | 无 WinAppSDK / WebView2 Runtime / MSIX 任何残留依赖，`ldd` 无 Windows 库 |
| G0-2 | 6 个取样页面渲染成功率 ≥ 80%，且崩溃 0 次 |
| G0-3 | 登录流程走通（任一方案），cookie 可持久化到磁盘 |
| G0-4 | `hk4e_cn` 可进入主界面；`ps` 证明游戏父进程是 `wine` |
| G0-5 | 前缀 reflink 克隆在用户实际文件系统上 < 2s（或明确记录退化数据） |
| G0-6 | 托盘 + 通知在 KDE 与 Hyprland（或 XWayland）下均可见 |
| G0-7 | `Starward.Assets` 中的 `Segoe Fluent Icons.ttf` 在 Uno Linux 下能正常渲染图标（图标全部变方框即不通过） |
| G0-8 | `Lunarward.Codec` 尖峰：`net10.0` 下用系统 `libavif`/`libjxl` 成功解码一张 AVIF 与一张 JXL（只验证 P/Invoke 映射通路，不求全，≤1 天） |
| G0-9 | 上游高频 XAML 写法可编译可渲染 | `poc/uno-smoke/LunarwardPoc --page=compat`，11 项全过 |

**当前状态**（2026-11-01）：G0-1 / G0-2 / G0-3 / G0-5 / G0-6 / G0-8 **已完成**，G0-7 有条件下的 PASS（改判见 D26），G0-4 待用户安排真机时间。实测记录与复现脚本见 `poc/GATES.md`。

**不通过的处理**：按 `06-risks.md` 第 4 节执行回退或否决。

---

## M1 · 能玩（核心闭环）

**范围**：`GameLauncher` 主干 + Runtime 引擎 + CLI。

**功能清单**：

| 模块 | 内容 |
|---|---|
| 组件库 | 扫描（系统/Steam/自定义）、登记、指纹、引用计数、删除保护 |
| 运行环境 | 创建/编辑/复制/导入导出；runner、env、wrappers、prefix |
| 前缀 | 5 种模式；模板创建；克隆；锁；占用检测 |
| 图形组件 | DXVK get-or-install 到前缀；版本选择 |
| 启动 | 计划预览、预检、exec、session 记录 |
| 监控 | agent：存活监控、退出归因、时长统计 |
| 调试终端 | 环境一致 shell + 单次命令 |
| 报告 | 时间线、组件指纹、归因、脱敏 |
| 迁移 | anime-game-launcher / sleepy-launcher 导入（只读生成草稿） |
| 最小 UI | 游戏页（复用上游）+ 运行环境管理页 + 计划预览页 |

**门禁**：

| # | 判据 |
|---|---|
| G1-1 | `hk4e_cn` 连续游玩 ≥ 10 分钟不崩溃，可正常退出 |
| G1-2 | `nap_cn` 同上，且 ACE 不报错 |
| G1-3 | 同一游戏两个不同 Profile（不同 wine+dxvk）可分别启动，互不污染 |
| G1-4 | 同一 wine 目录被 3 个 Profile 引用，磁盘只有一份 |
| G1-5 | 调试终端 `env` 与启动计划 `env` 逐项一致 |
| G1-6 | 每次启动产出报告，且报告可归因到「成功/崩溃/反作弊/组件缺失」之一 |

---


### M1-0 · 上游代码真实接入（已完成 2026-11-02）

- 建 `src/Lunarward/`（复制 `src/Starward/`，上游零改动），`Uno.Sdk 6.7.30` + `net10.0-desktop` + `SkiaRenderer`。
- 结果：**146 / 232 个 .cs 零错误**；C# 剩余缺口只有 8 个类型 / 52 条错误（见 `11-m1-0-api-gaps.md`）。
- XAML 全量尝试：616 条错误，其中 **63% 来自 6 个共享控件/包**（`CachedImage` 280、`WindowEx` 34、`Segmented` 30、`WrapPanel` 18、`Interaction` 12、`CanvasSwapChainPanel` 6）。
- 依赖硬发现：`Microsoft.Xaml.Behaviors.WinUI.Managed` 仅 `net8.0-windows`（不可用）；`CommunityToolkit.WinUI.Controls.*` 同；替换为 `Uno.Microsoft.Xaml.Behaviors.Interactivity.WinUI` + 自实现控件。

### M1-1 · 补共享控件与平台服务（进行中，2026-11-02）

1. ✅ `CachedImage` Linux 实现（280 条清零）；✅ 其余共享件（Segmented/WrapPanel/Shadow/Interactivity/WindowEx）已完成，XAML 错误 616 → 238；
2. `WindowEx` → `IWindowService`；
3. Behaviors 包替换 + `Segmented`/`WrapPanel` 自实现；
4. `HoYoPlayService`/`PlayTimeRecordService`/`GameAccountService` 的 `Windows.Storage` → `System.IO`；
5. 全量打开 XAML 再跑一次，得到页面级剩余缺口。

## M2 · 能用（体验补齐）

| 模块 | 内容 |
|---|---|
| 设置 | 上游设置页逐项替换平台实现（XDG 路径、主题、自启、热键降级） |
| 登录态 | 独立 WebKitGTK 登录窗（D29）；M1 用外部助手进程取 cookie（D32），M2 换 GirCore 进程内实现（R-16） |
| 战绩/抽卡 | 复用 `Starward.Core` 的 `GameRecordClient`/`GachaLogClient`，登录态来自上一条；WebView2 只做展示 |
| 公告 | 复用 `GameNotice` |
| 游玩时长 | 基于 session 数据 |
| 托盘/通知 | 实现 `org.kde.StatusNotifierItem`（Tmds.DBus，G0-6 已验证）+ `org.freedesktop.Notifications`；替换 M1 的 `TaskbarIcon` 占位（D30） |
| 更新 | 版本检查 + tarball 自更新（AUR 提示） |
| 单实例 | URL scheme 激活转发 |
| 伴随启动 | 5 种触发条件 + 独立进程型工具（注入型仅配置，不做实现） |
| 两个 CRUD 列表 | Profile 列表（全局）、后启动程序列表（按游戏）+ 编辑页 + 拖拽排序（D28） |
| 输出可见性 | 三级 × 三作用域、实时日志面板、`Ctrl+Shift+L`、外部终端 exec、SIGHUP 隔离（D27） |

**门禁**：

| # | 判据 |
|---|---|
| G2-1 | 连续日常使用 7 天，无阻断性 bug |
| G2-2 | 设置项 100% 可操作，无「点了没反应」 |
| G2-3 | 托盘/通知在 KDE 正常；Hyprland 有明确降级提示 |
| G2-4 | 伴随启动可配置并成功触发一个外部工具 |

---

## M3 · 好看（保真打磨）

| 模块 | 内容 |
|---|---|
| 视觉对齐 | 对照 golden screenshots 逐页修正；补齐动效 |
| 图像管线 | ① `Lunarward.Codec` 源码迁入 + `net10.0` + 删 `VP9Decoder` + Linux 原生库与 DllImport 映射；② Win2D 绘制原语换 SkiaSharp（缩略图、缓存、查看器、批量转换、3 个 D2D1 效果用 SkSL/CPU） |
| 背景 | 图片背景完整；视频背景（LibVLCSharp）可选 |
| 主题 | 深色/浅色、强调色、模糊降级策略 |
| 截图 | ① 捕获走 portal `Screenshot` / `grim` / `spectacle -b`（D31，不碰 PipeWire）；② `ImageViewWindow2` 等查看/转换页用 SkiaSharp 重画；③ `ScreenshotSetting`（HDR 色彩管理）整页重做 |
| 细节 | 圆角/阴影/间距/字号对齐 |

**门禁**：

| # | 判据 |
|---|---|
| G3-1 | `04-porting-matrix.md` 第 5 节「硬对齐项」全部通过 |
| G3-2 | golden screenshots 差异清单为空或有记录的原因 |
| G3-3 | 图像处理功能与上游行为一致（AVIF/JXL/UltraHDR 解码走 Codec；HDR10 显示仍降级为 SDR） |

---

## M4 · 增强（可选，单独决策）

- 游戏内 Overlay（portal + PipeWire）
- 屏幕捕获与游戏截图（含 Wine Vulkan 窗口）
- 手柄读取 + 虚拟输入（evdev + uinput）
- HDR10/uHDR gainmap（SkSL / CPU 路径）
- 云游戏（WebView 方案）
- NativeAOT 打包评估

> M4 任何一项开工前必须单独立项，不得默认进行。

---

## 每里程碑的回归清单（必跑）

```
[ ] 冷启动 GUI < 3s（NVMe）
[ ] hk4e_cn 启动 → 主界面 → 退出
[ ] nap_cn  启动 → 主界面 → 退出
[ ] 同 Profile 二次启动（验证无残酒/wineserver 泄漏）
[ ] 两个 Profile 并发启动（验证锁）
[ ] 调试终端 env 对比
[ ] 启动报告可读
[ ] 托盘/通知
[ ] 升级路径：旧配置可迁移
```

---

## 工期与人力假设

| 阶段 | 乐观 | 保守 | 关键假设 |
|---|---:|---:|---|
| M0 | 1 周 | 2 周 | 单人有 .NET + Linux 经验 |
| M1 | 6 周 | 10 周 | Uno 缺口可控 |
| M2 | 4 周 | 6 周 | 登录方案已定型 |
| M3 | 2–3 周 | 4 周 | Codec 移植顺利；Win2D 效果重写是变量 |
| M4 | — | — | 单独评估 |

**合计（M0–M3）**：14–23 周。

---

## 与上游的关系节奏（D19）

| 频率 | 动作 |
|---|---|
| 每 2 周 | 检查上游 release / 重要 commit，评估 cherry-pick |
| 每 minor 版本 | 更新 `Starward.Core`/`Language`（低风险）+ 评估 UI 变更 |
| 每次 cherry-pick | 跑完整回归清单后才合并 |
| 上游大版本 | 先立 ADR，再动 |
