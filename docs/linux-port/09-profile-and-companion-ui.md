# 09 · Profile 与后启动程序：信息架构与交互

> **状态：历史文档（2026-11-13 标注）。** 本文是早期的 Profile / 伴随启动 UI 设想；实际实现见 `16-runtime-components.md`（v2 极简）与 `17-runtime-environment-ui.md`（三段式 UI，D63）。保留用于回溯早期取舍，不要据此改代码。


> 本文只描述**界面与交互**（数据模型见 `03-runtime-model.md`，平台接口见 `02-architecture.md`）。
> 原则：凡是 Starward 已有的交互，一律沿用它的表达方式（`NavigationView` + `SettingsCard` + `ContentDialog` + 列表卡片），不自创控件语言（D3）。

## 1. 两个 CRUD 列表的归属（D28）

| 列表 | 位置 | 理由 |
|---|---|---|
| **Profile（运行环境）** | 设置 → 运行环境 → Profile 列表 | Profile 的价值是跨游戏复用（P2 痛点），放游戏内会立刻退化成「每游戏一套」，违背设计目标 |
| **后启动程序（Companion）** | 游戏设置页内的一个分区「后启动程序」 | 条目强依赖具体游戏的可执行文件与反作弊行为，天然属于游戏；放主导航会让导航项膨胀 |

补充动作：

- Companion 列表支持 **`从其他游戏导入`** 与 **`另存为模板`**，避免原神 / 绝区零 重复填写。
- Profile 列表支持 **`复制`**（用于快速造出「同 prefix、换 DXVK 版本」的对照实验，对应 P2 的「测不同组合」痛点）。

## 2. Profile 列表

### 2.1 行布局

```
[图标] Profile 名称                          [wine 10.12] [DXVK 2.4] [模板前缀]   ⟨打开调试 shell⟩  ⋮
       描述（可空，一行截断）                                                           |
                                                                                        +-- 编辑
                                                                                        +-- 复制
                                                                                        +-- 删除
                                                                                        +-- 查看启动日志
```

- 徽章只显示**关键三项**：运行器 / 图形组件 / 前缀模式。其余（env、wrappers、hooks）进编辑页，不在列表里堆。
- 正在被某个游戏引用时，删除按钮要点出「被 N 个游戏使用」并要求确认。
- 「查看启动日志」直接跳到该 Profile 最近一次 `LaunchSession` 的报告（对应 `03` §8）。

### 2.2 编辑页字段

用 `ContentDialog`（与 Starward 的 `GameLauncherSettingDialog` 一致），分三组：

**基础**

| 字段 | 控件 | 说明 |
|---|---|---|
| `name` | TextBox | 唯一，建议 `游戏-特性` 命名（如 `genshin-dxvk-nvapi`） |
| `description` | TextBox | 可空 |
| `runner.type` | ComboBox | `wine` / `proton` / `umu`（实验性）/ `custom` |
| `runner.componentId` | ComboBox + 「浏览」 | 从组件库选；`managed=false` 的外部组件显示为「外部：/path」 |

**图形与前缀**

| 字段 | 控件 | 说明 |
|---|---|---|
| `graphics.dxvk` / `vkd3dProton` / `dxvkNvapi` | ComboBox（可空） | 选择要覆盖的组件版本；空 = 用前缀内已有版本 |
| `prefix.mode` | ComboBox | `template` / `dedicated` / `shared` / `external` / `ephemeral` |
| `prefix.path` | TextBox + 「浏览」 | `external` 与 `shared` 时必填 |
| `prefix.templateId` | ComboBox | `template` 模式必填 |
| `wrappers` | 可多选列表 | `gamescope` / `gamemoderun` / `mangohud` / 自定义命令 |

**高级**

| 字段 | 控件 | 说明 |
|---|---|---|
| `env` | 键值对表格（增删行） | 叠加在游戏默认 env 之上，优先级见 `03` §4.3 |
| `hooks.preLaunch` / `postExit` | 命令行列表 | 可留空 |
| `shell` | ComboBox | `bash` / `zsh` / `fish` / 自定义，供「打开调试 shell」与 `terminal` 输出使用 |
| `gameOutput` | ComboBox | 三级可见性，默认 `silent`（见 §5） |

## 3. 后启动程序（Companion）列表

### 3.1 行布局

```
[图标] 条目名称                     [profile: 继承]  [伴随 ●]  ⟨▶ 手动启动⟩   ⋮
       命令摘要（一行，超长省略）                                                  +-- 编辑
                                                                                 +-- 复制
                                                                                 +-- 删除
                                                                                 +-- 查看日志
```

- `伴随` 开关的含义与 D24 一致：**只决定「游戏启动后是否自动运行」**，与手动按钮互相独立。
- 注入型条目在名称右侧加一个红色 `高风险` 标签；首次开启伴随时要二次确认。
- 拖拽排序直接决定伴随条目的启动顺序（见 §4.2）。

### 3.2 条目编辑页字段

| 字段 | 控件 | 说明 |
|---|---|---|
| `name` / `icon` | TextBox / 文件选择 | 图标可留空，留空用默认工具图标 |
| `command` | TextBox + 「浏览」 | 可执行文件绝对路径，或 PATH 中的命令名 |
| `args` | TextBox（多行） | 支持变量展开：`{game.installPath}`、`{profile.prefix}`、`{profile.wine}` |
| `cwd` | TextBox | 默认 = prefix 根目录 |
| `env` | 键值对表格 | 叠加在所选 Profile 的 env 之上 |
| `profileRef` | ComboBox | `inherit`（跟游戏同一 Profile）/ 指定另一个 Profile |
| `override` | 三个独立开关 | `command` / `args` / `env` 各自可选「替换」而非「叠加」 |
| `trigger` | ComboBox | `manual` / `companion` |
| `delay` | 数字（秒） | 仅 `companion` 有意义；注入类建议 3–8 秒 |
| `terminal` / `output` | ComboBox | 三级可见性，默认「跟随全局」（见 §5） |
| `waitExit` | ToggleSwitch | 是否等待该进程结束再继续下一条 |
| `timeout` | 数字（秒） | 超时kill；0 = 不限 |
| `runOnce` | ToggleSwitch | 每次游戏启动最多跑一次（默认开） |
| `risk` | 自动推断 + 可覆盖 | `safe` / `injection`，仅用于 UI 提示与确认，不改变执行方式 |

## 4. 启动时序与冲突处理

### 4.1 手动启动

- 游戏**未运行**：直接按该条目解析出的 `LaunchPlan` 执行。
- 游戏**运行中**：prefix 被 flock 占用，此时
  - 命令**不写 prefix**（纯外部工具，如 `mangohud` 包装器、截图脚本）→ 允许，提示「已跳过 prefix 锁」；
  - 命令**需要写 prefix** → 弹三选一：`等游戏退出后运行` / `取消` / `强制运行（危险，明确提示可能损坏前缀）`。

### 4.2 伴随启动

- 条目按列表顺序**串行**启动，默认间隔 1 s（可配）。
- `waitExit=true` 的条目会阻塞后续条目，直到它退出或超时——用于「必须先注入成功，才能起下一个」的场景。
- 某条失败时：默认**继续后续条目**并记入 `LaunchReport.warnings`；可在全局设置里改成「失败即中止」。
- 多个条目引用**不同 Profile** 时，各自用自己的 prefix 与 flock，互不阻塞（这正是 P3「同 profile 或独立 profile」的落点）。

## 5. 输出可见性：三级 × 三作用域（D27）

### 5.1 三级

| 级别 | UI 文案 | 行为 |
|---|---|---|
| `silent` | 不显示 | 输出只进日志文件（`~/.local/state/lunarward/logs/`） |
| `panel` | 应用内日志 | 打开**实时日志面板**（环形缓冲 5000 行，可暂停 / 继续 / 复制 / 清空 / 过滤） |
| `terminal` | 外部终端 | 用 Profile 配置的终端 `exec` 目标程序，得到真实终端窗口 |

**捕获永远开启**，三级只影响「显示方式」。理由：事后排障不能依赖「当时有没有开终端」。

### 5.2 三个作用域

| 作用域 | 默认 | 配置位置 | 说明 |
|---|---|---|---|
| 游戏本体 | `silent`（始终捕获） | Profile 编辑页 → `gameOutput` | 排查 DXVK / wine fixme 时切 `panel` 最实用 |
| 后启动程序条目 | 跟随全局 | 条目编辑页 → `output` | 注入类建议 `panel`，便于确认注入结果 |
| 启动器本体 | `silent` | 设置 → 高级 → `开发者模式`；命令行 `--verbose` | 开启后主界面出现「运行日志」入口，级别降到 Debug |

全局默认值：设置 → 运行环境 → `默认输出可见性`；快捷键 `Ctrl+Shift+L` 直接调出实时日志面板。

### 5.3 实时日志面板

- 每个正在运行的 `LaunchSession` 一个标签页；`全部` 标签页按时间合并。
- 行前缀带 `[game]` / `[entry:name]` / `[launcher]` 来源标记与级别颜色。
- 默认**脱敏**：`(?i)(token|secret|password|passwd|cookie|auth|session_?id)` 的值打码，需要看原文时点「显示敏感内容」（二次确认）。

## 6. 外部终端执行（`terminal`）

- 调用形式固定为「终端 `-e` + 目标命令」，**中间不套 shell**，保证 `wine` 的父进程是终端本身而不是 `bash`。
- 由于终端会成为父进程，**真实子进程 PID 不能靠 `Process.Id`**：
  - 优先用 `--title=lunarward:<planId>` 标记窗口，再通过 `/proc` 反查 `pidfd`；
  - 或由 agent 上报（`03` §8 的 LaunchReport 路径）。
- 终端窗口在游戏退出后**保留**，标题追加 `[已退出 code=N]`（用户 2026-11-01 确认）；面板与文件日志不受影响。
- 终端不可用（未安装 / 无 `-e` 兼容参数）时降级为 `panel` 并给出 `W-TERMINAL-UNAVAILABLE`。

## 7. 启动器自身的输出（TTY 语义）

用户 2026-11-01 提出「从终端启动就正常输出，从 KDE 启动就没有」。这在 Linux 上**基本自动成立**，但不是「什么都不用做」：

1. **TTY 检测而非假设**：用 `Console.IsOutputRedirected` / `isatty(1)` 判断，只有真正的终端才挂 Console sink；否则不写 stdout。
2. **文件日志永远是唯一真源**：桌面启动时 stdout 的去向不可靠（可能进 systemd user journal、可能丢弃），不能作为日志载体。
3. **防 EPIPE 阻塞**：若被某个不读书的管道包住（`| head`、某些 wrapper），写满缓冲区会让进程**卡死**。Console sink 必须捕获写入异常并**自我禁用**，不能让日志拖死启动流程。
4. **SIGHUP**：从终端启动的启动器，若用户关掉终端，默认会收到 `SIGHUP` 并退出——游戏可能被连带杀死。处理方式：
   - 启动器自身在初始化后忽略 `SIGHUP`；
   - 游戏/伴随进程放入**独立 session**（`setsid`）或独立进程组，脱离终端控制；
   - 这样「关终端」不再影响正在运行的游戏。
5. **行为矩阵**：

| 启动方式 | 控制台输出 | 文件日志 | 实时面板 |
|---|---|---|---|
| 终端 `lunarward` | 有（Information） | 有 | 可开 |
| 终端 `lunarward --verbose` | 有（Debug） | 有 | 可开 |
| `.desktop` / KDE 菜单 | 无（stdout 不挂 sink） | 有 | 可开 |
| 开发者模式开启 | 无 | 有（Debug） | 默认打开 |

> 结论：用户的直觉方向正确，但「忽略 SIGHUP + 不阻塞 stdout + 文件日志兜底」三件事必须显式实现，否则会出现「关终端把游戏一起关了」这类难排查的问题。

## 8. 与上游 UI 的一致性约束

- 列表项、编辑对话框、徽章、确认提示一律复用上游样式与文案骨架（`Starward.Language` 的 resx 结构），新增文案走同一条本地化管线。
- 新增的「运行环境」「后启动程序」分区，在视觉上应当像**上游自带的设置项**一样（同样的卡片圆角、同样的 `SettingsCard` 行高），不引入第二套设计语言。
- 这些页面**不参与** golden screenshots 的「与 Windows 逐像素对齐」判定（它们在上游没有对应物），但参与「风格一致性」人工评审。

## 9. 未决项

| 编号 | 问题 | 影响 |
|---|---|---|
| Q-035 | `从其他游戏导入` 时，同名条目如何处理（覆盖 / 跳过 / 重命名） | 交互细节 |
| Q-036 | 伴随条目失败时「继续 / 中止」的默认值 | 需与 D24 的安全默认一起定 |
| Q-037 | 是否为 `injection` 类条目提供「试运行」按钮（只校验路径与环境，不真正启动） | 降低误操作 |
| Q-038 | 实时日志面板是否允许把某段输出「另存为」附件到 LaunchReport | 排障体验 |
