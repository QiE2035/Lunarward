# 架构设计

- **状态**：v0.1（M0 PoC 后校准）
- **最后更新**：2026-11-01
- **前置**：`00-decisions.md` D0–D20、`01-requirements.md` R-01–R-16

---

## 1. 总体形状

```
┌─────────────────────────────────────────────────────────────────┐
│ starward  (GUI 可执行)                                          │
│  ├─ Starward (Uno 单头, 复用上游 82 XAML / 业务 Features)        │
│  ├─ Lunarward.Platform.Linux  (平台服务实现)                      │
│  └─ Lunarward.Runtime         (运行环境引擎, 无 UI 依赖)          │
└───────────────┬─────────────────────────────────────────────────┘
                │ 生成 LaunchPlan + 拉起
                ▼
┌─────────────────────────────────────────────────────────────────┐
│ lunarward-cli  (独立小可执行, 无 GUI 依赖)                        │
│  ├─ run      : exec 链启动游戏 (gamescope→gamemoderun→wine)      │
│  ├─ shell    : 与环境一致的调试终端 / 单次命令                    │
│  └─ agent    : 监控代理, 触发伴随项, 写 LaunchReport             │
└───────────────┬─────────────────────────────────────────────────┘
                │ exec (不 fork 常驻)
                ▼
        gamescope → gamemoderun → mangohud → wine → Game.exe
```

> 命名约定（D22）：上游既有项目保留原名 `src/Starward*` 以降低上游冲突；**新增**项目统一用 `Lunarward.*`；用户可见二进制为 `lunarward` / `lunarward-cli`，内部程序集名不必与产品名一致。

**关键约束**：GUI 不参与启动链；`lunarward-cli run` 使用 `execve` 逐级替换自身，最终 `wine` 是游戏进程的父级。这样既保住「游戏是 wine 的第一个进程」（R-06），又让伴随项由**独立**的 `agent` 进程负责（R-07）。

---

## 2. 项目划分

| 项目 | 来源 | 目标框架 | 职责 | 可复用度 |
|---|---|---|---|---|
| `src/Starward.Core` | 上游原样 | `net10.0` | 米哈游 API、分包下载、战绩、抽卡、公告、SelfQuery、本地化模型 | ≈95%（仅改注册表/路径/UA 处） |
| `src/Starward.Language` | 上游原样 | `net10.0-windows` → `net10.0` | 37 个 resx 的多语言资源 | 100% |
| `src/Starward` | 上游改造 | `net10.0` (Uno.Sdk) | GUI：XAML 页面、ViewModel、Features 业务逻辑 | ≈65% 原样 |
| `src/Lunarward.Platform.Linux` | **新增** | `net10.0` | `IPlatformService` 及全部平台接口的 Linux 实现 | 0（新写） |
| `src/Lunarward.Runtime` | **新增** | `net10.0` | 组件库、运行环境解析、前缀管理、启动计划、报告、迁移导入 | 0（新写，核心资产） |
| `src/Lunarward.Cli` | **新增** | `net10.0` | `run` / `shell` / `agent` 三个子命令 | 0（新写，薄壳） |
| `src/Starward.RPC` | 上游改造 | `net10.0` | 增量安装/卸载服务；命名管道 → Unix domain socket | ≈70% |
| `src/Starward.Setup` | **不移植** | — | Windows 安装器 | 0（D1/Δ5） |
| `src/Starward.Launcher` | **不移植** | — | C++ 引导程序（单实例、DPI、协议注册） | 0 |

**依赖方向（只能单向）**：

```
Cli ──► Runtime ──► Core
GUI(Starward) ──► Runtime ──► Core
GUI(Starward) ──► Platform.Linux ──► Core
Platform.Linux ──► Runtime     (允许：平台层需要读组件/前缀信息)
Runtime ✗► Platform.Linux      (禁止：运行环境引擎不得依赖 UI/平台实现)
Core ✗► 任何 UI 项目
```

规则：
1. `Lunarward.Runtime` 不引用 `Microsoft.UI.Xaml`、不引用 Uno、不引用任何 GTK/D-Bus 实现（D-Bus 通知走 `Platform.Linux`）；
2. `Starward.Core` 保持零 Linux 改动（唯一例外：`Environment`/注册表适配，见 `04-porting-matrix.md`）；
3. 平台接口一律定义在 `src/Starward/Platform/Abstractions/`，Linux 实现注册在 DI 容器。

---

## 3. 平台抽象层

### 3.1 接口清单

| 接口 | 职责 | Windows 原实现 | Linux 实现要点 |
|---|---|---|---|
| `IPlatformService` | 聚合入口（等价上游 `AppConfig.Service` 平台位） | WinAppSDK | 组合下列实现 |
| `IWindowService` | 窗口创建/尺寸/最小化到托盘/全屏/标题栏 | `Microsoft.UI.Windowing` | Uno `Window` + 无障碍属性；托盘见 `ITrayService` |
| `ITrayService` | 托盘图标与菜单 | `H.NotifyIcon` | `org.kde.StatusNotifierItem` over D-Bus |
| `INotificationService` | 系统通知 | `AppNotificationManager` | `org.freedesktop.Notifications` |
| `IBackdropService` | Mica/亚克力背景 | `SystemBackdropHelper`（DWM） | 探测合成器模糊（KWin/Wayland），不支持则纯色/渐变降级 |
| `IFileDialogService` | 文件/目录选择 | `FileDialogNative`（COM） | `xdg-desktop-portal`（FileChooser） |
| `IProcessService` | 进程启动/监控/环境快照 | `Process.Start` + Win32 | `posix_spawn`/`execve` 封装 + `/proc` 读取 |
| `ISingleInstanceService` | 单实例与激活转发 | `AppInstance`/命名互斥 | `flock` + `$XDG_RUNTIME_DIR/starward/instance.lock` + Unix socket 转发 |
| `IUrlProtocolService` | `starward://` 注册与解析 | 注册表 | `.desktop` MimeType + `xdg-mime` |
| `IPowerService` | 阻止休眠/唤醒事件 | Win32 `SetThreadExecutionState`/`Microsoft.Win32` | `org.freedesktop.login1` Inhibit；唤醒事件可选 |
| `IGamepadService` | 手柄与虚拟输入 | `SharpGameInput` + `InputSimulator` | evdev 读取 + uinput 虚拟设备（M4） |
| `IScreenCaptureService` | 屏幕捕获 | `GraphicsCapturePicker` | `xdg-desktop-portal` ScreenCast + PipeWire（M4） |
| `IHotkeyService` | 全局热键 | `RegisterHotKey` | X11 `XGrabKey`；Wayland 降级为应用内（D11） |
| `IWebContentService` | 内嵌网页（登录/公告/云游戏） | WebView2 | Uno WebView 兼容层或系统浏览器（D12） |
| `IStorageService` | 配置与缓存根路径 | `LocalApplicationData` | XDG 规范（D17） |
| `IThemeService` | 主题/强调色/材质 | WinAppSDK 主题 API | GTK/KDE 主题探测 + 内置深色默认 |
| `IRegistryService` | 游戏注册表写入/查询 | Win32 注册表 | 由 wine 前缀内 `reg` 命令或读取 `system.reg` 实现 |
| `IStartupService` | 开机自启 | 计划任务 | `~/.config/autostart/*.desktop` |
| `ILogService` | 日志落盘与查看 | Serilog + Windows 路径 | Serilog + `~/.local/state/starward/logs` |
| `IOutputCaptureService` | 进程输出捕获、环形缓冲、日志面板事件、脱敏 | 无（新增，D27） | `Process.RedirectStandard*` + 事件流 + 文件 sink |
| `ITerminalLauncherService` | 用外部终端 `exec` 目标程序（游戏/条目/调试 shell），并提供 PID 反查 | 无（新增，D27/D28） | 读 `$TERMINAL`/配置表，`--exec`/`-e` 形态适配 alacritty/kitty/konsole/wezterm/foot |
| `IShellService` | 在指定 Profile 环境开交互式 shell | 无（新增，D28） | `setsid` + `exec`，prefix/env 取自 Profile |

**规则**：Windows 实现**直接删除**，不保留 `#if WINDOWS` 分支（D3）。若个别接口必须保留双实现（例如 `IStorageService` 在开发机上跑单元测试），则以「命名实现 + DI 选择」而非条件编译。

### 3.2 DI 注册

```csharp
services.AddStarwardRuntime();            // Runtime 引擎
services.AddStarwardLinuxPlatform();      // 平台实现（本项目唯一实现集）
services.AddStarwardFeatures();           // 上游 Features（原样注册）
```

---

## 4. 进程模型

### 4.1 参与进程

| 进程 | 生命周期 | 作用 | 备注 |
|---|---|---|---|
| `lunarward` | 常驻（可托盘） | GUI；解析启动计划、拉起 `lunarward-cli run`、接收报告 | 上游 `App.xaml.cs` 结构基本不变 |
| `lunarward-cli run` | 极短（被 exec 替换） | 校验 → 钩子前置 → `exec` 游戏链 | **不 fork**，避免多一层父进程 |
| `lunarward-cli agent` | 与游戏同寿 | 监控游戏、按条件触发伴随项、写报告、退出归因 | 由 GUI 或 `run` 拉起（detached） |
| 游戏进程树 | 游戏生命周期 | — | `wine`/`wine64` 为父级 |

### 4.2 启动时序

```
GUI                         cli run                agent              wine/game
 │ 1 解析 Profile+Game → LaunchPlan
 │ 2 预检(prefix 占用/DXVK/组件存在)
 │ 3 准备 prefix(克隆/DXVK 就位)
 ├──4 spawn cli run(plan.json)──►│
 │                              │ 5 前置 hook(user/pre)
 │                              │ 6 写 session 记录(pid/lock)
 │                              ├──7 spawn agent──►│
 │                              │ 8 exec 包装器链
 │                              ├────────────────────────────────►│ 9 游戏启动
 │                              │                              │ ◄─10 agent: on_game_started
 │                              │                              │    触发伴随项(注入型/独立型)
 │  11 GUI 读取 session 心跳 ◄──┴──────────── agent 写 report ───┘
 │ 12 游戏退出 → agent 归因 → 写 LaunchReport → 通知 GUI
```

### 4.3 单实例与并发

1. **GUI 单实例**：`flock($XDG_RUNTIME_DIR/starward/instance.lock, LOCK_EX|LOCK_NB)`；失败则通过 `/tmp`-不可用场景下的 `$XDG_RUNTIME_DIR/starward/ipc.sock` 发送 `activate`/`open-url`。
2. **同 prefix 互斥**：`flock(<prefix>/.starward-lock)`，加锁失败时读锁文件内的 `pid/plan/session` 并提示占用者（R-04 判据 2）。
3. **wineserver 残留检测**：启动前检查 `WINEPREFIX` 下是否存在该前缀的活跃 `wineserver` 或同 prefix 的 `wine*` 进程（读 `/proc/*/environ` 匹配 `WINEPREFIX=`），命中则阻断。

### 4.4 为什么 agent 必须独立

用户的核心约束是「游戏可能必须是 wine 的第一个进程，否则崩溃」（m00001 P3）。这意味着**不能在游戏前面挂一个常驻父进程**。因此：

- 启动链全部 `exec`，父进程即调用者（GUI 或终端）；
- 需要「游戏启动后」的能力时，只能由**另一个独立进程**旁观（`agent`），它不参与游戏进程树；
- 注入型工具（patch/hook/inject）的自动触发因此被设计为「agent 在检测到窗口映射后执行，工具自行注入」——注入本身仍由工具负责，本项目只解决**时机**与**环境一致性**。

---

## 5. 运行环境引擎（Lunarward.Runtime）

### 5.1 模块

| 模块 | 类 | 职责 |
|---|---|---|
| 组件库 | `ComponentStore` | 登记/扫描/指纹/引用计数/删除保护（R-02） |
| 组件扫描器 | `ComponentScanner` | 系统 PATH、Steam、`compatibilitytools.d`、Lutris、Bottles、Heroic、自定义目录（R-05） |
| 前缀管理 | `PrefixManager` | 模板创建、reflink 克隆、锁、健康检查、占用检测（R-04） |
| 图形组件 | `GraphicsComponentInstaller` | DXVK/VKD3D get-or-install + 按前缀就位（D6） |
| 环境合成 | `EnvironmentComposer` | env 继承与覆盖、变量展开、路径规范化 |
| 计划解析 | `LaunchPlanBuilder` | 上述产物 → 可预览/可复现的 `LaunchPlan` |
| 执行器 | `LaunchExecutor` | 校验、钩子、spawn cli、记录 session |
| 会话 | `LaunchSessionStore` | session 元数据、心跳、报告归档 |
| 报告 | `LaunchReportWriter` | 时间线、退出归因、脱敏（R-08） |
| 诊断 | `EnvironmentDoctor` | 前缀健康检查、组件一致性、常见故障模式 |
| 迁移 | `MigrationImporter` | anime-game-launcher / sleepy-launcher / Lutris / Bottles 导入 |
| 终端 | `ShellLauncher` | 环境一致 shell / 单次命令 / 前缀工具（R-09） |

### 5.2 数据流

```
ComponentStore ─┐
Profile ────────┼─► EnvironmentComposer ─► LaunchPlan ─► LaunchExecutor ─► cli run
GameEntry ──────┤                              │
PrefixManager ──┘                              └─► PlanPreview(UI) / plan.json / 报告
```

### 5.3 数据存放（XDG，D17）

| 路径 | 内容 | 是否可删 |
|---|---|---|
| `~/.config/starward/config.json` | 全局设置（上游 `AppConfig` 迁移而来） | 否 |
| `~/.config/starward/profiles/*.json` | 运行环境 | 否 |
| `~/.config/starward/games/*.json` | 游戏条目（安装路径、绑定 profile、游戏级覆盖） | 否 |
| `~/.local/share/starward/components/index.json` | 组件索引 | 可重建 |
| `~/.local/share/starward/components/<kind>/<id>-<ver>/` | **导入**的组件产物 | 可重建 |
| `~/.local/share/starward/prefixes/<name>/` | 本项目创建的前缀 | 否（含用户数据） |
| `~/.local/state/starward/sessions/` | 会话与报告 | 可清理 |
| `~/.cache/starward/` | 下载缓存、图片缓存、日志轮转 | 是 |
| `$XDG_RUNTIME_DIR/starward/` | 锁、Unix socket、运行期元数据 | 是（自动） |

**注意**：外部组件（Steam、系统、自定义路径）只登记**指针**，不复制进 `components/`；只有用户显式「导入」时才落地。

---

## 6. 线程与异步规则

1. UI 线程只做绑定；所有文件/进程/网络操作 `async`，`ConfigureAwait(false)` 在 Runtime 内一律使用。
2. `LaunchPlanBuilder` 必须**纯函数式**：相同输入产生相同输出（便于报告复现与快照测试）。
3. `PrefixManager` 的克隆/删除必须串行化（`SemaphoreSlim(1)` + 跨进程 `flock`）。
4. 监控（agent）使用 `inotify`/`/proc` 轮询结合：优先 `pidfd`（Linux 5.3+），退化到 `/proc/<pid>` 轮询 200ms。

---

## 7. 故障隔离

| 子系统 | 故障后果 | 隔离手段 |
|---|---|---|
| 网页登录组件 | 登录失败 | 降级为系统浏览器 + cookie 导入（D12） |
| 托盘/通知 | 无托盘 | 保留主窗口常驻；通知降级为应用内提示 |
| 截图/Overlay | 功能缺失 | 特性开关默认关闭（D14） |
| HDR 处理 | 图片偏色 | 强制 SDR 路径（D13） |
| 手柄映射 | 无虚拟输入 | 仅保留读取，隐藏虚拟输入入口 |
| 组件扫描 | 组件列表为空 | 允许手填路径兜底 |

---

## 8. 与上游的关系（D2）

1. 上游目录 `src/Starward/Features/**` 尽量少改：平台调用先抽接口，再把 `Service` 的实现替换为 Linux 实现；
2. `src/Starward.Core` 零改动优先；必须改时集中到 `Core/Linux/*.partial.cs` 或最小 `#if` 之外的适配器；
3. 新增文件统一放 `Platform/`、`Runtime/`、`Features/Runtime*`（新功能页）下，避免与上游同名；
4. 每个上游同步提交，先跑 `07-roadmap.md` 的回归清单再合并。
