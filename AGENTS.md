# AGENTS.md · Lunarward（Starward 的 Linux 移植）

给在这个仓库里干活的人 / Agent 的操作手册。**先读第 1、2 节再动手**；其余按需查。

---

## 1. 项目定位

- **Lunarward** = [Starward](https://github.com/Scighost/Starward) 的 **Linux 移植**（C# / .NET 10 / [Uno Platform](https://platform.uno/) + Skia，`net10.0-desktop`）。
- 目标：在 Linux 上**尽量还原上游 Windows 版**的功能、排版与操作逻辑；引擎能力缺口记作「软差异」，不改上游 XAML 去硬凑。
- 在这之上新增 **Linux 专属模块**：运行环境（wine / Proton / DXVK / VKD3D / 前缀）、伴随启动、一次性运行、调试 Shell、托盘、热键、X11 窗口显隐。
- 上游 `src/Starward/` **一个字节都不改**；上游同步方式：`git fetch upstream && git merge upstream/main`（2026-11-21 起 `main` 与上游历史已连通，merge base = `df8ae07`，是普通三方合并；见 D93）。
- 分支：**`main`** 为当前开发分支（`linux-port` 自 2026-11-13 起停用，仅留作历史；远端 `origin` = 你的 fork；`upstream` = Scighost/Starward）。

---

### GUI 自动化工具的代价（2026-11-13 实测）

`kwin-mcp` 的隔离会话是一个**嵌套 KWin 合成器**：实测额外占用 `kwin_wayland --virtual` ≈28% CPU + 会话内 `Xwayland` ≈8%，而且它的画面要再由用户的主合成器合成一次。用户反馈「开着会话时桌面每 10 秒卡 1–2 秒，关掉立刻恢复」。

规则：

1. **不要**在用户正常使用机器（尤其游戏运行时）长开会话；需要 GUI 验证时先做一次短测，测完立即 `session_stop`。
2. 只测「页面耗时/行为断言」这类不需要人眼看的项时，优先用应用内日志（临时 `[tempdiag]` + 跑完删除），不要开合成会话。
3. 必须开会话时，先说明预计占用，并在同一轮内关掉；遗留 `<defunct>` 子进程要一起清。
4. 会话内跑 X11-only 应用要先起 `Xwayland :N` 并把 `DISPLAY=:N` 传进去；本机 `Xwayland -screen` 参数不支持，窗口会按 640×480 被 clamp。

### GUI 自动化避坑（`cu_*`，2026-11-21 实测）

Lunarward 是 Uno/Skia 应用，**不发布 AT-SPI 树**：`cu_a11y_find` / `cu_a11y_tree` 对它恒返回 0 条命中，别在它上面找控件，直接按截图坐标点。踩过的坑与替代做法：

1. **别用 a11y 定位**：Uno 窗口中 `cu_a11y_find` 恒空；控件位置只能从截图读，窗口坐标每次用 `cu_windows` 现取（实测同一实例出现过 frame 原点 300 与 400 两种）。
2. **窗口截图可能失败**：`cu_screenshot target=window app=Lunarward` 报 `KWin returned unsupported image format 18`；改 `target=workspace` 再按 frame 原点换算（默认 `maxEdge=1000`，1600 宽的屏 → 逻辑坐标 = 像素 × 1.6）。
3. **`activate` 不等于置顶**：`window.control activate` 返回 `active: true` 后截图仍可能别的窗口在前，盲点会落到那个窗口上；点之前用 `cu_windows` 取 frame + 截图确认目标窗口覆盖那些坐标。
4. **一次批量做完**：`activate → click → sleep → screenshot` 用 `cu_batch` 一次调用；分步调用之间窗口会动、状态漂移，而且更慢。
5. **优先驱动内部状态**：能靠 `LUNARWARD_DEV_*` / 环境变量（如 `LUNARWARD_DEV_SELECTOR=1` 自动展开选择层）走内部流程的，不要模拟点击；Wayland 合成点击不可靠（L-36）。
6. **判状态先看日志**：`~/.local/share/lunarward/log/` + 临时 `[lw-diag]` 打点，比截图肉眼判断可靠；打点用完 `git checkout --` 撤回（硬规则 7）。
7. **收尾查进程**：`pgrep -x Lunarward` 必须为空，用 `pkill -x` 而不是 `pkill -f`（硬规则 8）。


## 2. 硬规则（违反即返工）

1. **未授权不动手**：任何改动前先给方案（做什么 / 怎么验证 / 风险），得到明确同意再执行。
2. **批准范围不得扩大**：批准「加个边距」不等于批准「顺手重构布局」；超出范围要先问（D46 第 6 条）。
3. **结论必须有论据**：区分「实测」与「推断」，写清复现方式；可视化问题给像素/数值；猜错要撤回并留档（D46）。
4. **`src/Starward/` 零改动**：需要不同行为就在 `src/Lunarward/**/*.Linux.cs` 里重写同名能力；确需改共享文件，加 `// LUNARWARD-DEVIATION: 原因` 并在 `docs/linux-port/12-shims-and-gaps.md` 登记。
5. **文档与代码同源**：行为变化的提交必须同步文档（`00-decisions.md` 追加决策、`13-lessons.md` 记坑、`CHANGELOG.md` 记版本），否则视为未完成。
6. **危险入口不进发行版**：调试/开发入口一律 `#if DEBUG` 或 `DevSwitches`，Release 构建里不得残留副作用命令（D35）。
7. **不留临时插桩**：`[xx-diag]` 日志、`LUNARWARD_DEV_*` 钩子、临时脚本，提交前删掉或降级为 `Debug`。
8. **不要 `pkill -f`**：会杀掉自己的 shell；用 `pkill -x Lunarward`。
9. **用完随手关**：手动启动的实例、`--selftest-ui` 进程、cli-smoke 起的假服务，验证一结束就关（`pkill -x Lunarward`），不要留在用户桌面上吃资源、抢单实例锁、继续写日志。下一轮动手前先 `pgrep -x Lunarward` 确认干净。
10. **UI 线程不许 fork 外部进程**（`Process.Start` + `WaitForExit`）：wine 冷启动 4 秒会整窗冻住（L-127）。注册表/前缀/预检一律走后台线程 + 异步 API。
11. **以上游为准**：行为、排版、交互一律以 `src/Starward/` 的实现为目标。发现移植侧与上游「实现错位」时，先按上游语义改回来；确实必须偏离的（平台 API 缺失、引擎软差异）要写明原因与边界，并登记到决策文档 / `18-upstream-deviations.md`（2026-11-21 用户明确指示）。

---

## 3. 构建 / 测试 / 运行

```bash
# 构建（仓库根，slnx 已经是 Linux-only 的四个工程）
dotnet build Starward.slnx -c Debug
dotnet build Starward.slnx -c Release

# 单元测试（当前 286 条）
dotnet test Starward.slnx -c Debug

# 运行（开发构建）
./src/Lunarward/bin/Debug/net10.0-desktop/Lunarward

# 进程内 UI 自检（11 条断言，CI 里跑在 xvfb 上）
LUNARWARD_SELFTEST=1 ./src/Lunarward/bin/Debug/net10.0-desktop/Lunarward --selftest-ui

# L2 CLI 冒烟（cli-smoke 40 例 + cli-extra 32 例，假 wine/假前缀/假 DXVK，全部走临时 XDG 目录）
LW_BIN=$PWD/src/Lunarward/bin/Debug/net10.0-desktop/Lunarward bash scripts/test/cli-smoke.sh
LW_BIN=$PWD/src/Lunarward/bin/Debug/net10.0-desktop/Lunarward bash scripts/test/cli-extra.sh

# 静态规则检查（UI 线程不许同步阻塞 / 不许留空 stub / 命令白名单，秒级、不启动应用）
bash scripts/test/lint-ui-thread.sh
LW_BIN=$PWD/src/Lunarward/bin/Debug/net10.0-desktop/Lunarward bash scripts/test/lint-cli-help.sh

# Arch 打包：三份 PKGBUILD 的 depends 与源码里的原生依赖是否对得上（秒级、不联网）
# （packaging/PKGBUILD 默认就地构建本地工作区：软链源码、复用 obj/，含未提交改动；
#  按远端 main 构建：LUNARWARD_SOURCE=remote makepkg -f。D91/D92）
bash scripts/test/lint-pkgbuild.sh

# 离线安装链路（假 Sophon 服务 + zstd 分块）
bash scripts/test/chunk-smoke.sh
```

**环境坑（踩过，别再踩）**

| 坑 | 现象 | 处理 |
| --- | --- | --- |
| 根 `global.json` 钉 SDK `10.0.100` + `msbuild-sdks` | 在错误目录执行 dotnet 会报 `MSB4236 找不到 SDK "Uno.Sdk"`；SDK 低于下限时报 `A compatible .NET SDK was not found` | 一律**在仓库根**执行；子目录另有 `global.json` 时以最近者为准；下限只写 `x.y.100`，别钉具体 feature band（D89 / L-163） |
| `api.nuget.org` 在本机大包 EOF | `restore` 卡死/失败 | 用 `src/Lunarward/NuGet.config` 的华为云镜像（已配置 `<clear/>` 单源） |
| X 授权文件会轮换 | `Authorization required` / `xdpyinfo` 失败 | `export XAUTHORITY=$(ls /run/user/1000/xauth_* \| head -1) DISPLAY=:0` |
| Wayland 会话 | 全局热键不可用、`xdotool` 部分失效 | 热键是 X11 轮询；Wayland 下用 `lunarward --show` 绑系统快捷键 |
| `/tmp` 是 tmpfs | 拷 100 GB 游戏进 `/tmp` 会把内存吃光 | 测试克隆放 `~/games`，用 `cp -a --reflink=auto` |
| 界面卡顿时不要猜 | — | 先看 `~/.local/share/lunarward/log/`，用 `dotnet-trace`/`perf` 取数 |

---

## 4. 代码地图（`src/Lunarward/`）

```
App.xaml(.cs)                 应用启动、单实例、托盘、深链、语言
Program.Linux.cs              CLI 入口（60+ 开关/子命令，headless 可用）
Frameworks/                   平台底座：WindowEx.Linux（窗口 chrome/尺寸/显隐）
Helpers/                      X11Window(libxcb)、AppIcon、OpenUrl、JsonFile(原子写)、
                              DesktopNotification、DiagnosticBundle、ProcessQuery、Housekeeping、X11KeyState
Features/
  ViewHost/                   MainWindow、MainView、导航、页面缓存
  GameLauncher/               启动器页、游戏设置页、公告/资讯、预检与启动
  GameInstall/                Sophon 安装/更新/修复/预下载（InstallPipeline、UpdateOrchestrator、Chunk*）
  Runtime/                    运行环境：ComponentStore、PrefixManager、GraphicsInstaller、LaunchRecipe、
                              EnvironmentResolver（D73 阶段 2/3）、
                              ComponentManifest/ComponentDownloader/ComponentSources/
                              ComponentImporter/ComponentHealth（D73 阶段 1）、
                              RuntimeProfileStore（env/<id>.json）、ProfileBinding、LastKnownGoodStore、RunOnceService、ProfileShellService
  Companion/                  伴随启动（后启动程序）
  Background/                 背景（静态/视频/图库/缩略图/强调色）
  Screenshot/  GachaLog/  GameRecord/  Setting/  Tray/  UrlProtocol/  Dev/
Controls/                     CachedImage（含 DecodePixelWidth）、其它自定义控件
Assets/                       图标、字体（注意：Uno 自带 Symbols 字体会覆盖部分码位）
```

**共享 vs Linux 专属**：上游同名文件被 `<Compile Remove>` 排除后，由同目录 `*.Linux.cs` 提供实现。判断某处「真的做了没有」→ 查 `docs/linux-port/12-shims-and-gaps.md`（唯一状态表）。

---

## 5. 测试体系（`docs/linux-port/19-test-plan.md` 是唯一计划）

| 层 | 形态 | 入口 | 适用 |
| --- | --- | --- | --- |
| L1 | xunit 纯逻辑 | `dotnet test Starward.slnx` | 算法/解析/路径/原子写 |
| L1.5 | 进程内 UI 断言 | `--selftest-ui` | 窗口/导航/资源/布局存在性 |
| L2 | CLI + 夹具 | `scripts/test/cli-smoke.sh`、`cli-extra.sh`、`chunk-smoke.sh`、`lint-ui-thread.sh`、`lint-cli-help.sh`、`lint-pkgbuild.sh`、`perf.sh` | 子命令、预检、安装管线、离线 Sophon、静态规则、启动卡顿 |
| L3 | 真机 GUI | 人工 + `cu_*` 工具 | 排版、交互、性能、视觉 |

覆盖率现实：测试工程**链接源码**而非引用程序集，`coverlet` 测不到主程序（line-rate 0）。要覆盖率得先改 `ProjectReference` + `InternalsVisibleTo`。

**GUI 自动化约定**：能用 `cu_*`（dsh-cu）就不要 `xdotool`——`cu_batch` 一次调用完成「激活→点击→等待→截图」，`cu_screenshot` 取图，`cu_windows waitFor` 等窗口。`xdotool` 只保留「毫秒级轮询窗口出现」。跑 GUI 用例前先看 `cu_status.session.locked`；**跑完立刻 `pkill -x Lunarward`**，别让测试实例留在用户桌面上。

---

## 6. 文档地图（`docs/linux-port/`）

活跃文档**共 14 份**（2026-11-21 增补 `25`/`26`/`27`，2026-11-23 增补 `28`/`29`）：

- `README.md` — 总览与状态（**先看这个**）
- `00-decisions.md` — D0–D120 决策（唯一决策源；改结论要追加变更行）
- `12-shims-and-gaps.md` — 实现状态登记（唯一）
- `13-lessons.md` — L-01…L-170 坑与复现方法（唯一）
- `18-upstream-deviations.md` / `18-upstream-table.md` — 与上游的偏差原则与同名差异文件清单（`scripts/list-deviations.sh` 生成；逐点以 `25` 为准）
- `19-test-plan.md` — 测试计划（唯一，230 条：218 T-* + 12 RG-*）
- `23-runtime-env-design.md` — **运行环境设计的唯一依据**（D73：组件层 / 前缀只是路径 / 八字段 / 四层检查 / 页面）
- `24-dependency-audit.md` — 依赖盘点（48 处外部进程判定、可换库项、建议批次）：换依赖前先看这张表
- `25-deviation-register.md` — **逐点偏离总账**（265 点 / 114 文件、类别 + 必要性 + 可否回流、53 条排除清单、60 条可避免清单（2026-11-22 全量复核））：判断「这个偏离是否必须」先查它
- `26-misalignment-audit.md` — **实现错位 bug 候选**（76 条，2026-11-22 已全部读码复核：成立 51 / 部分成立 19 / 不成立 6；**已修复 70 条**，D95–D103；`B01`…`B76` 编号）：修 bug 前先对号，修完把 ◐/○ 改成 ✅ 并补证据
- `27-audit-report.md` — **审计可读版（修复前快照）**：按模块讲每条「哪里错、上游怎么写、用户看到什么」，与 26 同编号；想快速理解看这份，当前状态看 `26`
- `28-runtime-env-audit.md` — **运行环境模块审查**：第一轮 R01–R42 已按批次 A–D 修复（D104–D107）；第二轮 2026-11-24 复审 R43–R130（3 Critical / 52 High / 20 Medium / 13 Low，**批 E–J 已修**（R43–R97，D115–D120），其余按 §6.11 批次 K 推进）：动 `Features/Runtime/` 或运行环境页之前先看它
- `29-runtime-env-ui-audit.md` — **运行环境页面 UI 审查**（2026-11-23，21 条：术语/i18n / 布局视觉 / 交互可达性，D108 全部修完）：改运行环境页 XAML 或要动文案/配色之前先看它
- `docs/user/` — 给用户的 FAQ 与运行环境说明
- `screenshots/` / `perf-baseline.json` — 截图证据与性能基线数据

已删除 17 份完成使命的文档（`01`–`11`、`15`–`17`、`20`–`22`：需求/架构规划、M1-0 快照、早期运行环境 UI 设计、一次性执行记录）。需要回溯：`git log --diff-filter=D -- docs/linux-port/`。

## 7. 提交 / CI

- 提交信息：`type(scope): 摘要` + 必要时的正文（说明根因与验证方式）；一个提交只做一件事。
- CI（`.github/workflows/linux.yml`）：Release 构建 → L1 测试 → L2 CLI 冒烟 → L1.5 自检（xvfb）→ 上传可运行产物（`lunarward-linux-x64`，self-contained tar.gz，D90）。**必须绿**；`gh run list --repo QiE2035/Lunarward`（不加 `--repo` 会被 `upstream` 解析到上游）。
- AppImage 打包走 `workflow_dispatch` 或 tag（`.github/workflows/appimage.yml`）。
- 版本号在 `src/Lunarward/Lunarward.csproj`；对外可见变更写进 `CHANGELOG.md`。

---

## 8. 已知缺口（别当成 bug 去「顺手修」）

- 悬浮窗、手柄映射、RPC 服务、云游戏/战绩/BBS 桥 → **M4 占位，明确暂不做**。
- 公告窗改为「浏览器打开」（`GameNoticeWindow` 已删除）。
- 引擎能力差异（记软差异，不改上游 XAML）：无 DWM 丙烯（部分页面壁纸透字）、`ms-appx` 含空格字体不加载、`PipsPager` 模板不响应、`Window.Hide()` 空实现（已用 libxcb 补）、`AppWindow.Id` 恒 `0x1`。
- 真机待验收：原神/绝区零真实启动、真实登录回填、游玩时长记录。

---

## 9. 快速自查（提交前）

- [ ] `dotnet build Starward.slnx -c Release` 0 错误
- [ ] `dotnet test Starward.slnx` 全绿
- [ ] `bash scripts/test/lint-ui-thread.sh` 全绿（动了 UI code-behind / `*.Linux.cs` 事件处理器 / CLI 开关时必跑）
- [ ] `bash scripts/test/lint-cli-help.sh` 全绿（加了/改了 CLI 开关时必跑：`Helpers/CliSpec.cs` 是帮助唯一来源，D79）
- [ ] `bash scripts/test/lint-pkgbuild.sh` 全绿（改了原生依赖、`packaging/` 或 `global.json` 时必跑：三份 PKGBUILD 的 depends 要与源码对上，SDK 下限只写 `x.y.100`，D88/D89）
- [ ] `bash scripts/test/cli-smoke.sh` 与 `cli-extra.sh` 全绿（改了 CLI 时必跑）
- [ ] 没有残留 `[xx-diag]` 日志 / 临时 `DevSwitches` / `/tmp` 调试代码
- [ ] `src/Starward/` 无改动（`git status --porcelain src/Starward`）
- [ ] 文档已同步（决策 / 坑 / CHANGELOG 至少一处）
- [ ] 测试实例已关闭（`pgrep -x Lunarward` 无输出）
