# Lunarward 文档总览（Starward 的 Linux 移植）

- **最后更新**：2026-11-13
- **当前阶段**：功能移植已完成（M1–M3 主体 + 运行环境 D58/D63），正在做**测试与 UI 审查**（`19` / `20` / `21` / `22`）
- **分支**：`linux-port`；`src/Starward/` 保持 **0 改动**（上游可随时 `git fetch upstream && git merge upstream/main`）
- **用户文档**：[`docs/user/faq.md`](../user/faq.md)、[`docs/user/runtime-environment.md`](../user/runtime-environment.md)
- **施工授权**：本目录任何文档都**不构成开工授权**。只有 `00-decisions.md` 里标「已确认」的条目、且用户在对话中明确说「可以开始做 X」时才允许改源码；未明确的一律视为未授权（D46 第 6 条：批准范围不得扩大）。

---

## 1. 现在有什么（能力快照）

| 模块 | 状态 | 关键实现 / 入口 | 记录 |
| --- | --- | --- | --- |
| 主界面（启动器页） | ✅ | 背景（静态/视频）、公告/资讯、开始/暂停/更新按钮 | `21` `22` |
| 游戏选择 | ✅ | 固定图标条 + 全屏选择层（`GameSelector`） | D56 |
| 游戏设置 | ✅ | 分辨率/图形/HDR/语音 + 运行配置 + 后启动程序 + 诊断 | D61 D63 |
| 截图 | ✅ | `spectacle`/`grim`/`gnome-screenshot` 降级链、图库、批量转换 | D31 |
| 抽卡记录 / 战绩 | ✅（未登录为空态） | `GachaLog` / `GameRecord` / `SelfQuery` | D61 |
| 账号登录 | ✅（外部 WebKitGTK 助手） | `WebLoginService`（cookie 0600） | D32 |
| 安装/更新/修复/预下载 | ✅ | Sophon 分块 + 差分补丁 + chunk 复用 + 断点续传 | D41 D42 |
| 运行环境（Linux 新增） | ✅ | 运行器 / 方案 / 前缀 / 图形层（DXVK·VKD3D） | D58 D63 |
| 伴随启动 / 一次性运行 / 调试 Shell | ✅ | `CompanionRunner`、`RunOnceService`、`ProfileShellService` | D24 D64 |
| 托盘 / 热键 / 窗口显隐 | ✅ | StatusNotifierItem、X11 轮询热键、libxcb 显隐 | D43 D47 |
| 单实例 / 深链 / 通知 / 诊断包 | ✅ | `SingleInstancePipe`、`lunarward://`、`notify-send`、`--diagnose` | D50 D59 |
| 打包 | ✅ | AUR（`lunarward`、`lunarward-git`）+ AppImage（CI） | D59 |
| 悬浮窗 / 手柄 / RPC / 云游戏·战绩·BBS 桥 | ⬜ M4 | 占位，明确暂不做 | `12` `19` |

---

## 2. 文档清单与状态

| 文档 | 状态 | 用途 |
| --- | --- | --- |
| `00-decisions.md` | **当前（唯一决策源）** | D0–D70 的结论、理由、代价、变更记录 |
| `01-requirements.md` | 当前 | 需求 R-01…R-18 与验收口径 |
| `02-architecture.md` | 当前 | 分层、平台层接口、进程模型 |
| `03-runtime-model.md` | 当前 | 运行环境数据模型（组件/方案/前缀/启动计划） |
| `04-porting-matrix.md` | 当前 | Windows 依赖 → Linux 手段的对照矩阵 |
| `05-dependencies.md` | 当前 | 依赖准入与许可证清单（版本随 `Directory.Packages.props` 更新） |
| `06-risks.md` | 当前 | 风险与回退条件 |
| `07-roadmap.md` | 当前 | M0–M4 里程碑与门禁 |
| `08-open-questions.md` | 参考 | 已归档的未决问题池（新问题先写这里） |
| `09-profile-and-companion-ui.md` | **历史** | 早期 Profile/伴随 UI 设想；实际实现见 `16`/`17`/D63 |
| `10-xaml-compat.md` | 参考 | 上游 XAML 与 Uno 的兼容性体检 |
| `11-m1-0-api-gaps.md` | **历史** | M1-0 首编译缺口快照；现状以 `12` 为准 |
| `12-shims-and-gaps.md` | **当前（唯一状态登记表）** | 每处替身/缺口的真实状态（D33） |
| `13-lessons.md` | **当前（唯一坑记录）** | L-01…L-137 的实测结论与复现方法 |
| `15-performance.md` | **历史** | 早期性能基线；现以 `19` §9 + `perf-baseline.json` + `21` 为准 |
| `16-runtime-components.md` | 历史 | 运行组件 v2 极简（D58）：**已被 23 取代** |
| `17-runtime-environment-ui.md` | 历史 | 运行环境 UI 三段式（D63）：**已被 23 取代** |
| `18-upstream-deviations.md` | 当前 | 移植偏差原则、登记标记、上游同步流程 |
| `18-upstream-table.md` | 当前 | 81 个同名差异文件的自动分类与复核进度 |
| `19-test-plan.md` | **当前（唯一测试计划）** | 227 条用例（215 T-* + 12 RG-*）、L1/L1.5/L2/L3 分层、性能预算、缺陷流程 |
| `20-l3-report.md` | 进行中 | L3 真机用例首轮结果与证据 |
| `21-ui-audit.md` | 进行中 | UI 审查与性能记录（含撤回条目） |
| `22-port-verification.md` | 进行中 | 纯移植部分验证（布局/交互/性能） |
| `23-runtime-env-design.md` | **当前（D73 定稿）** | 运行环境从头设计：三概念 / 自洽硬目标 / 受管组件 / 八字段 / 验收项 |
| `screenshots/` | 证据 | 截图证据（文件名即场景） |
| `perf-baseline.json` | 数据 | 性能基线快照 |

> 维护规则：**同一主题只有一份「当前」文档**。新结论写进当前文档；旧文档不再改动，只加「历史」标记并在上表更新状态。

---

## 3. 阅读路径

- **新接手/想知道现在能干什么** → `README`（本文）→ `12` → `22`/`21`
- **要改代码** → `02` → `03`（运行环境）→ `13`（先看有没有踩过）→ `00`（D 编号与代价）
- **要查某处是不是「真的做了」** → `12`（唯一状态表）
- **要加测试** → `19`（先登记用例 ID）
- **要排查线上问题** → `docs/user/faq.md` + `--diagnose` 诊断包

---

## 4. 工作流程（强制）

1. **先讨论，后落文档**：新细节先进 `08-open-questions.md`（带编号），不改 `00-decisions.md`。
2. **用户确认后才升级为决策**：写进 `00-decisions.md`（结论/理由/代价/未做），`08` 标「已归档 → Dxx」。
3. **文档先行于代码**：实现前，行为必须已在 `01`/`03`/`17` 中有可验收描述。
4. **未明确即不做**：未授权的细节不得「顺手实现」；**批准范围不得扩大**（D46 第 6 条）。
5. **结论必须有论据**：能复现、区分实测与推断、可视化问题要量化、猜错要撤回并留档（D46）。
6. **决策不覆盖，只追加**：改结论时新增变更记录行，保留旧结论与改判原因。
7. **文档与代码同源**：行为变化的提交必须同步文档，否则视为未完成。
8. **偏差要标记**：偏离上游的地方在源码里写 `// LUNARWARD-DEVIATION: 原因`，并在 `12` 登记。

---

## 5. 术语表（中英对照）

| 中文 | 英文 | 含义 |
| --- | --- | --- |
| 组件 | Component | 可寻址的版本化产物：wine / proton / dxvk / vkd3d-proton / dxvk-nvapi |
| 运行器 | Runner | 真正执行游戏的运行时：系统 wine、Steam 的 `files/bin/wine`、手动添加路径 |
| 前缀 | Prefix | Wine prefix（`drive_c` + 注册表）；本项目的“前缀来源”= 新建 / 复制自现有 / 使用已有目录 / 临时 |
| 方案 | Profile | 可命名的启动配方：运行器 + 图形层 + 前缀 + env / Shell / 输出级别 |
| 前缀来源 | Prefix source | `dedicated`（新建）/ `template`（复制自现有）/ `external`（使用已有目录）/ `ephemeral`（临时） |
| 图形层 | Graphics layer | 装进前缀的 DXVK / VKD3D-PROTON 一组 DLL |
| 后启动程序 | Companion | 游戏启动后（或退出后）按条件触发的辅助工具 |
| 一次性运行 | Run once | 在与目标游戏相同的环境里跑一次任意程序（`RunOnceService`、`--run-once`） |
| 调试 Shell | Debug shell | 在同一环境下打开交互式终端 |
| 启动报告 | Launch report | `logs/launch/<时间>-<biz>.json`，含预检逐项结果 |
| 上次可用 | Last known good | `~/.config/lunarward/last-good.json`，用于一键回退 |

---

## 6. 消息引用约定

文档用 `mNNNNN` 指代当时的用户消息，便于回溯原始意图；只引用**决策性**消息，不引用过程性对话。

| 引用 | 内容 |
| --- | --- |
| `m00001` | 首条需求：5 个痛点 + 4 条硬要求 |
| `m00115` | 「Linux 必须可用，Windows 可用性不做强要求」 |
| `m00119` | 「真机验收改为原神与绝区零」 |
| `m17231` | 「仅 Linux；移植而非重写，代码贴近上游」+ 运行环境四类来源要求 |
| `m18410` | 「观感一致指排版；字体/模糊等细节差异可接受」 |
