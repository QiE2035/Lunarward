# AGENTS.md · Lunarward（Starward 的 Linux 移植）

给在这个仓库里干活的人 / Agent 的操作手册。**先读第 1、2 节再动手**；其余按需查。

---

## 1. 项目定位

- **Lunarward** = [Starward](https://github.com/Scighost/Starward) 的 **Linux 移植**（C# / .NET 10 / [Uno Platform](https://platform.uno/) + Skia，`net10.0-desktop`）。
- 目标：在 Linux 上**尽量还原上游 Windows 版**的功能、排版与操作逻辑；引擎能力缺口记作「软差异」，不改上游 XAML 去硬凑。
- 在这之上新增 **Linux 专属模块**：运行环境（wine / Proton / DXVK / VKD3D / 前缀）、伴随启动、一次性运行、调试 Shell、托盘、热键、X11 窗口显隐。
- 上游 `src/Starward/` **一个字节都不改**；上游同步方式：`git fetch upstream && git merge upstream/main`。
- 分支：`linux-port`（远端 `origin` = 你的 fork；`upstream` = Scighost/Starward）。

---

### GUI 自动化工具的代价（2026-11-13 实测）

`kwin-mcp` 的隔离会话是一个**嵌套 KWin 合成器**：实测额外占用 `kwin_wayland --virtual` ≈28% CPU + 会话内 `Xwayland` ≈8%，而且它的画面要再由用户的主合成器合成一次。用户反馈「开着会话时桌面每 10 秒卡 1–2 秒，关掉立刻恢复」。

规则：

1. **不要**在用户正常使用机器（尤其游戏运行时）长开会话；需要 GUI 验证时先做一次短测，测完立即 `session_stop`。
2. 只测「页面耗时/行为断言」这类不需要人眼看的项时，优先用应用内日志（临时 `[tempdiag]` + 跑完删除），不要开合成会话。
3. 必须开会话时，先说明预计占用，并在同一轮内关掉；遗留 `<defunct>` 子进程要一起清。
4. 会话内跑 X11-only 应用要先起 `Xwayland :N` 并把 `DISPLAY=:N` 传进去；本机 `Xwayland -screen` 参数不支持，窗口会按 640×480 被 clamp。


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

---

## 3. 构建 / 测试 / 运行

```bash
# 构建（仓库根，slnx 已经是 Linux-only 的四个工程）
dotnet build Starward.slnx -c Debug
dotnet build Starward.slnx -c Release

# 单元测试（当前 49 条）
dotnet test Starward.slnx -c Debug

# 运行（开发构建）
./src/Lunarward/bin/Debug/net10.0-desktop/Lunarward

# 进程内 UI 自检（8 条断言，CI 里跑在 xvfb 上）
LUNARWARD_SELFTEST=1 ./src/Lunarward/bin/Debug/net10.0-desktop/Lunarward --selftest-ui

# L2 CLI 冒烟（cli-smoke 34 条 + cli-extra 13 条，假 wine/假前缀/假 DXVK，全部走临时 XDG 目录）
LW_BIN=$PWD/src/Lunarward/bin/Debug/net10.0-desktop/Lunarward bash scripts/test/cli-smoke.sh
LW_BIN=$PWD/src/Lunarward/bin/Debug/net10.0-desktop/Lunarward bash scripts/test/cli-extra.sh

# 离线安装链路（假 Sophon 服务 + zstd 分块）
bash scripts/test/chunk-smoke.sh
```

**环境坑（踩过，别再踩）**

| 坑 | 现象 | 处理 |
| --- | --- | --- |
| 根 `global.json` 钉 SDK `10.0.300` + `msbuild-sdks` | 在错误目录执行 dotnet 会报 `MSB4236 找不到 SDK "Uno.Sdk"` | 一律**在仓库根**执行；子目录另有 `global.json` 时以最近者为准 |
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
  Runtime/                    运行环境：ComponentStore、PrefixManager、GraphicsInstaller、
                              RuntimeProfileStore、ProfileBinding、LastKnownGoodStore、RunOnceService、ProfileShellService
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
| L2 | CLI + 夹具 | `scripts/test/cli-smoke.sh`、`chunk-smoke.sh` | 子命令、预检、安装管线、离线 Sophon |
| L3 | 真机 GUI | 人工 + `cu_*` 工具 | 排版、交互、性能、视觉 |

覆盖率现实：测试工程**链接源码**而非引用程序集，`coverlet` 测不到主程序（line-rate 0）。要覆盖率得先改 `ProjectReference` + `InternalsVisibleTo`。

**GUI 自动化约定**：能用 `cu_*`（dsh-cu）就不要 `xdotool`——`cu_batch` 一次调用完成「激活→点击→等待→截图」，`cu_screenshot` 取图，`cu_windows waitFor` 等窗口。`xdotool` 只保留「毫秒级轮询窗口出现」。跑 GUI 用例前先看 `cu_status.session.locked`；**跑完立刻 `pkill -x Lunarward`**，别让测试实例留在用户桌面上。

---

## 6. 文档地图（`docs/linux-port/`）

- `README.md` — 总览与状态（**先看这个**）
- `00-decisions.md` — D0–D72 决策（唯一决策源；改结论要追加变更行）
- `12-shims-and-gaps.md` — 实现状态登记（唯一）
- `13-lessons.md` — L-01…L-129 坑与复现方法（唯一）
- `18-upstream-deviations.md` / `18-upstream-table.md` — 与上游的偏差原则与 81 个同名差异文件清单（`scripts/list-deviations.sh` 生成）
- `19-test-plan.md` — 测试计划（唯一，210 条用例）；`20-l3-report.md`/`21-ui-audit.md`/`22-port-verification.md` — 执行记录
- `16-runtime-components.md` / `17-runtime-environment-ui.md` — 运行环境设计与 UI
- `09` / `11` / `15` — **历史文档**，不要据此改代码
- `docs/user/` — 给用户的 FAQ 与运行环境说明

---

## 7. 提交 / CI

- 提交信息：`type(scope): 摘要` + 必要时的正文（说明根因与验证方式）；一个提交只做一件事。
- CI（`.github/workflows/linux.yml`）：Release 构建 → L1 测试 → L2 CLI 冒烟 → L1.5 自检（xvfb）。**必须绿**；`gh run list --repo QiE2035/Lunarward`（不加 `--repo` 会被 `upstream` 解析到上游）。
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
- [ ] `bash scripts/test/cli-smoke.sh` 与 `cli-extra.sh` 全绿（改了 CLI 时必跑）
- [ ] 没有残留 `[xx-diag]` 日志 / 临时 `DevSwitches` / `/tmp` 调试代码
- [ ] `src/Starward/` 无改动（`git status --porcelain src/Starward`）
- [ ] 文档已同步（决策 / 坑 / CHANGELOG 至少一处）
- [ ] 测试实例已关闭（`pgrep -x Lunarward` 无输出）
