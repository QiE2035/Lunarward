## 2026-11-14（五）——把真实缺陷钉成回归用例

- 新增 **lint 层**：`scripts/test/lint-ui-thread.sh`（3 条规则，<1 s，进 CI）。A：`*.xaml.cs` 不许 `WaitForExit` / `.GetAwaiter().GetResult()` / `.Wait()`（D72 那 4 秒的根源）；B：`*.Linux.cs` 不许留单行空事件处理器（L-132 那两处空 stub），故意不接管的写 `// LINT-ALLOW: 原因`；C：`DevSwitches` 的每个参数必须在 `Program.Linux.cs` 的 `guiFlags` 里（`--page` 曾被守卫拦掉）。
- 修正 3 处既有违规：`guiFlags` 补上 `--subpage/--game/--scroll/--viewer/--profile-editor/--window-action`，10 个原本是 `{ }` 的事件处理器按「为什么不管」写清注释。
- L1.5 新增第 10 条断言「Pivot 换源按约定顺序不抛异常」（RG-01）；顺带用最小复现确认 Uno 在容器未重建时抛的是 **NRE**（`Pivot.cs:301`），与真机上的越界异常同源，记 L-133。
- L1 新增 `BuildRegFileBytes` 的 BOM 回归（RG-06），`WineRegistry` 改用它，单测 61 → 62。
- `19-test-plan.md` 新增 §12「由真实缺陷反推的回归用例」：12 条 RG 用例，标明断言/层/自动化状态（9 条已自动化，3 条待补）。

## 2026-11-14（四）——截断自适应回加、三处文案入 i18n

- **`IsTextTrimmedChanged` 全部回加（7 处）**：`ScreenshotSetting.xaml`、`GachaStatsCard.xaml`、`ZZZGachaStatsCard.xaml`、`InstallGameDialog.xaml`、`ApocalypticShadowPage.xaml`、`ForgottenHallPage.xaml`、`PureFictionPage.xaml`。L-24 当年判定「Uno 没有这个事件」已过时：Uno 6.7.359 的程序集里有 `add_IsTextTrimmedChanged`，且自检第 9 条实测**事件在 Skia 后端会触发**。
- `ScreenshotSetting.Linux.cs` 与 `InstallGameDialog.Linux.cs` 里原本是空 stub（上游实现所在 `.xaml.cs` 在 `<Compile Remove>`）→ 补回上游逻辑：被截断一次就缩一号字（最小 12）。
- `UiSelfTest` 新增第 9 条断言「IsTextTrimmedChanged 在 Skia 后端会触发」（40×20 容器 + `MaxLines=1` + 超长文本，等 1.5 s），`--selftest-ui` 9/9 通过。
- 三处硬编码中文入 `Lang`：热键说明、手柄页 InfoBar 标题/正文、战绩空态标题/正文/按钮，共 6 个键 × **12 个 resx**（en + zh-CN/zh-HK/zh-TW + de/es/it/ja/ko/ru/th/vi），并三处 XAML 改为 `x:Bind lang:Lang.Lunarward_*`。
- 启动组成实测（决定不做 `x:Load` 结构性优化）：`MainView` 通胀 360 ms（内含 `GameSelector` 64 ms）+ `GameLauncherPage` 201 ms（内含 `StartGameButton` 70 ms、`GameBannerAndPost` 21 ms）。可延迟的部分只有 85 ms 量级，而 `GameSelector` 在构造函数里就要建游戏图标区、是首屏一部分，`x:Load` 会让首屏图标空着 → 收益 6% 但改动共享 XAML 且改变观感，**不做**，数据入 `19-test-plan.md` §9。

## 2026-11-14（三）——偏差复核与两处真实缺陷

- 26 个「小改动」上游偏差全部复核完（A×19 / B×4 / C×3，死文件 0），结论落 `docs/linux-port/18-upstream-table.md`。
- 修：手动备份把整个 `DatabaseBackup` 目录打进 zip（历次备份自我嵌套、`.db` 多一层）→ 改为 `DatabaseService.CreateArchive` 单文件压缩。
- 修：`StartGameButton` 用 `progress > 0` 吞掉 `0.0%` 那一帧 → 统一为 `total > 0`（与 `PreDownloadButton` 一致）。
- 修：`GeneralSetting` 悬空分隔线、`GameSelector` 无人使用的 `x:Name`。
- 记：`Helpers/PointerCursor.cs` 的 `partial` 是必需的（去掉报 CS0260），复核建议「回退」不成立，见 L-130。
- 测：冷启动 1.04–1.24 s 分层（XAML 通胀 0.58 s + 首帧 0.46 s）；游戏设置对话框首次 170 ms / 再次 45 ms。基线已入 `19-test-plan.md` §9。

## 2026-11-14（二）——同类同步重活收口（D72 补充 / L-129）

- 按六类清完「该异步没异步」，剩下三处「一次点击卡一下」也挪到后台：导入图形组件时的整目录拷贝（~100 MB）、`GraphicsInstaller.EnsureInstalled` 的三个 UI 调用点（拷 5 个 DLL + SHA-256）、`ComponentStore` 首次扫描 PATH 与所有 Steam 库。
- 运行环境页的组件列表改成异步装载（先 `Task.Run` 扫描，`await` 之后再建行），冷启动看门狗只剩启动时一次平台渲染卡顿（1244 ms），页面内容与来源标签照旧。
- 教训 L-129：同类问题要按类搜（同步等异步 / 同步子进程 / 同步目录 IO / 同步网络 / 静态构造 / `[ThreadStatic]` 跨 await），并写清「哪些可以留」的判断标准。

## 2026-11-14（一）——切换游戏不再卡 4 秒（D72 / L-127）

- 切到原神 / 星穹铁道 / 崩坏3 时整窗静止 4 秒的问题修掉了：根因是 `wine reg query` 同步 fork 在 UI 线程上（`wineserver` 冷启动一次就要 ~4 秒），绝区零不卡只是因为那条代码路径对 `nap` 直接返回 0。
- 注册表访问全部改异步：一次 `reg query` 取整键（原来一个值一次 fork）、多值写入合并成一次 `wine reg import`，并按「前缀 + 键」缓存（命中 5 分钟，键不存在 60 秒，写操作后失效）。
- 创建/检查前缀（`wineboot` 十几秒、`cp` 克隆）与启动预检也挪到后台线程，游戏设置页的「应用」不再卡界面。
- 实测（`LUNARWARD_UI_WATCHDOG=1`）：修复前启动 1024+4224 ms、切原神 4047 ms、切星穹 4346 ms、切绝区零 0 次；修复后只剩启动时一次 ~1 秒的平台渲染卡顿，切游戏 0 次，切绝区零行为不变。
- 新增 `WineRegistry` 的 `reg` 文本纯函数（`RegistryFormat`）与 12 条单元测试；纠正一个坑：`wine reg import` 的 `.reg` 必须带 UTF-16LE BOM，否则静默什么都不写（L-128）。
- 顺带修复：`--page`（DEBUG 调试入口）被上一轮加的「未知命令」守卫拦掉。


## 2026-11-13（十一）

- 修掉每次切换游戏必现的 `ArgumentOutOfRangeException`（`Pivot.SynchronizeSelectedItem` 拿旧索引索引未重建的 header 容器，L-126/D71）：赋值 `PostGroups` 前把 `Pivot_Posts.SelectedIndex` 置 `-1`，赋值后再设回 0。真机切 4 次游戏，异常 0 次（修复前每次必现），资讯区不再停留在上一个游戏的状态。

## 2026-11-13（十）

- 视频壁纸看门狗从 2 秒放宽到 5 秒。VP9 1080p 首帧正常需要 2–3 秒（VLC 缓冲 ~1.0 s + 解码 + 8 MB 帧拷贝），旧阈值把正常起播也报成 `video started but no frame`，导致 F-21 被误判为回归；真机复测三次帧差 39.70%–48.13%，视频一直正常。
- 教训 L-125：超时阈值按「正常路径最慢值 × 1.5」定。

## 2026-11-12（三）——UI 复审后的 9 处修复（D61）

- 设置页 / 游戏设置页改为实底，游戏背景不再透到表单文字后面。
- 键盘快捷键、截图设置：去掉 Windows「需要管理员权限」口径，改成 X11 轮询 + Wayland 用系统快捷键的说明；新增 `lunarward --screenshot`。
- 移除常规页里 Windows 系统设置的两条 Hyperlink（透明 / 动画效果）。
- 运行环境页：删掉「M2 补齐」「前缀模板库」，前缀模式改称「前缀来源」（新建 / 复制自现有前缀 / 使用已有目录 / 临时）。
- 关闭窗口选项：文案改「隐藏到系统托盘」。
- 高级页新增「诊断」：一键生成诊断包 + 打开下载目录；RPC 区标注「暂未实现」。
- 战绩页空态：未登录/无角色时给出说明与登录按钮。
- 游戏设置页：绑定失效时顶部告警，并可直接重新选运行环境。
- 手柄增强页：顶部提示「暂未支持」并置灰开关。

# Changelog

本文件按日期记录实现层面的关键变更（对用户可见的行为变化为主）。版本号尚未发布，当前处于 WIP 阶段。

## 2026-11-12——切换页面不再卡

- 页面加 `NavigationCacheMode`：启动器 / 游戏设置 / 截图 / 抽卡 / 战绩 / 自助查询 / 设置 再进都不再重建（实测 1729 ms → 24–46 ms）。
- 游戏设置页的注册表读取（分辨率 / 帧率 / 语音语言）挪到后台线程：首次进入 1729 ms → 216 ms。

## 2026-11-11（三）——可观测性与打包

- 预检项不再只是文字：失败项直接给按钮（打开游戏设置 / 打开运行环境 / 打开前缀目录 / 重建前缀 / 安装图形组件 / 查看占用进程）。
- 新增 `lunarward --diagnose --out DIR [--game BIZ]`：一键导出环境、运行器、方案、前缀状态、启动报告、日志尾部与预检结果（敏感变量打码）。
- 窗口补上 `_NET_WM_ICON`（Uno 的 X11 后端不设，之前任务栏只有占位方块）。
- 新建/克隆的前缀改为 `0700`。
- 打包跟随代码：新增 `packaging/aur/lunarward-git`、`packaging/aur/lunarward` 与 `.github/workflows/appimage.yml`（tag 触发产出 AppImage）。
- 单测 24 → 36（路径展开 `PrefixPath`、原子写 `JsonFile`）。
- 新增用户向文档 `docs/user/runtime-environment.md`；README 注明只在 X11/XWayland 验证。

## 2026-11-11（二）

- 纯缓存与数据分离（D57）：缩略图（`bg-thumbs`、`thumb`）迁到 `~/.cache/lunarward`，并纳入 14 天清理。

## 2026-11-11（运行组件 v2 极简）

- 运行组件模块按「极简版」重做（D58 v2）：只回答「有哪些运行器 / 这个游戏用哪个 / 怎么换回来」
- 运行器列表新增布局（`bin` / `files/bin`）与可用性判定（无副作用：不建前缀），不可用时直接显示原因
- 方案编辑器：提示换运行器会触发一次 `wineboot -u`；显示「上次可用」并可一键切回
- 启动成功后写 `~/.config/lunarward/last-good.json`（原子写）
- 新增 CLI：`--list-runners [--json]`
- 撤回 v1 的锁文件 / 漂移检测 / 内容寻址仓 / 可达性 GC / 组件市场

## 2026-11-11（文档·v1 设计稿，已撤回）

- 新增 `docs/linux-port/16-runtime-components.md`：运行组件与运行环境的目标形态（D58，设计稿，未实施）
- 要点：组件强身份（哈希/来源/能力）、`profile.json`+`lock.json`、保留策略 `reference/hardlink/copy`（不假定 CoW）、
  前缀 manifest 与生命周期、`overlay` 注入默认、**可达性 GC（pnpm 式标记-清扫，不是计数器）**、`missing`/`broken` 一等状态、provider 多来源市场
- §8 措辞校准：refcount → 可达性 GC；补 `--verify-store`（↔ `pnpm store status`）、删除显式触发（↔ `pnpm store prune`）、共享 inode 的三种空间口径
- §2.4/§4/§7.2 范围校准：只管启动器自己造的（受管），外部组件（pacman/Steam/手动）只发现、只展示版本、只查存在；外部漂移视为预期行为，不告警不拦截；砍掉外部的哈希/校验/保留/配方/GC

## 2026-11-06

- 非 M4 收尾（D51）：逐条定论 14 项缺口，见 `docs/linux-port/00-decisions.md`
- 登录前置检查：缺 `python3` / `python-gobject` / `webkit2gtk-4.1` 时给出可操作提示，不再静默失败

## 2026-11-06（D50）

- 托盘不可用时「关闭窗口」直接退出并提示，不再变成关不掉的窗口
- 桌面通知：安装 / 预下载 / 修复 / 更新完成与失败、截图保存
- 补齐 de-DE / es-ES / it-IT / ru-RU / th-TH / vi-VN 六种语言；修复「配置里的语言不生效」
- 强调色：按背景图（静态）或海报（视频）取主色，写入应用与主题字典

## 2026-11-06（D49）

- 图标：7 处缺字形码位替换为 Uno 自带 Symbols 字体中的等价字形
- 视频暂停立即显示静态图（预加载合成结果）
- 窗口底座统一：子窗口交还窗口管理器标题栏，尺寸夹到屏幕 90% 并居中

## 2026-11-05（D47 / D48）

- 窗口隐藏 / 唤回改用进程内 libxcb（`xcb_unmap_window` / `xcb_map_window` + `_NET_ACTIVE_WINDOW`）
- 托盘：补 `Properties.GetAll`（KDE 下菜单与图标）、修正 dbusmenu `GetLayout` 签名
- 视频背景随窗口隐藏暂停、唤回恢复；静态背景合成标题层；视频帧改用 `SKXamlCanvas` 渲染

## 2026-11-05（D44 / D45 / D46）

- 健壮性加固：原子写、预检只读、安装目录与空间校验、单实例回调回 UI 线程、CLI 退出码、日志与临时文件保留策略
- CI 改为只验证 Linux 侧（删除上游 6 个 Windows 工作流，新增 `.github/workflows/linux.yml`）
- 协作规则：结论必须有论据（禁止臆测）；批准范围不得扩大

## 2026-11-03 ~ 11-04

- 安装 / 更新：Sophon 分块下载、差分补丁、chunk 级复用、断点续传、暂停/继续/停止
- 运行环境：Profile（wine/Proton/umu/custom + 前缀 5 模式）、组件探测、伴随启动、输出捕获、游玩时长
- 界面：主窗口、游戏页、截图图库与查看器、背景图库、设置页、单实例与深链

## 2026-11-11

- 图库与游戏选择层改为按可用宽度均分（D56），去掉无效的横向滚动；
- 组件扫描改为特征探测 + 可配扫描根，覆盖 `/opt`、Steam `compatibilitytools.d`、Flatpak 与第三方启动器；新增 `--list-components`；
- 游戏扫描支持配置根与 24 小时增量缓存；
- `~/.local/share/Lunarward` 合并为指向 `lunarward` 的符号链接，`WelcomeWindow` 默认值改小写；
- 背景图库的视频条目改用 libVLC 抽首帧做缩略图（不再黑块）。

## 2026-11-12（四）

- 修：切换游戏后左下角公告/资讯仍是上一个游戏的内容（新增 `CurrentGameChangedMessage`、页面与 banner 属性 setter 重载、Pivot 先置空再赋值）。
- 修：从其它页切回「启动器」卡几秒（背景层加同游戏守卫、先用本地图秒出画面、建议背景超时 1.5 s）。
- 开发入口：`LUNARWARD_DEV_SWITCH_GAME=<biz>`（DEBUG）按真实选服路径切游戏，便于复现。

## 2026-11-13 — 文档整理与协作约定

- 重写 `docs/linux-port/README.md`：能力快照（12 个模块状态）、文档清单与状态（区分「当前/参考/历史」）、阅读路径、8 条强制工作流程。
- 给 `09` / `11` / `15` 加「历史」状态横幅，避免被当成现行设计；`12` 加「最后校准 2026-11-12」与指向 `21`/`22` 的说明。
- `22-port-verification.md`：更正缩放误判（按 1:1 处理并标注待确认），撤回 P1-1，P1-2/P2-2 降级为待复核。
- 新增仓库根 `AGENTS.md`：项目定位、构建与测试命令、环境坑、代码/文档地图、协作规则与工具约定。

## 2026-11-13（四）── 视频壁纸「静止」定性与加固

- 定性 F-21：不是渲染缺陷。上游 `GetSuggestedGameBackgroundAsync` 在「背景列表无新增」时复用上次背景并置 `StopVideo = true`（看过的背景不自动播视频）；强制视频路径实测 CPU 0.96 核、帧正常。
- 加固：建议背景超时 1.5 s → 4 s 并重试一次；拿不到背景时保持现有画面（不再被兜底图覆盖）；失败缓存 30 s；调试入口记状态；新增「起播 2 秒无帧」看门狗日志。
- 修正 20 号 L3 报告里对 F-21 的定性，新增 D68 / L-120。

## 2026-11-13（三）── 测试面补齐

- 覆盖率可度量：新增 `tests/coverlet.runsettings`（`IncludeTestAssembly=true`）与 `scripts/test/coverage.sh`，行覆盖 **90.39%**（800/885）、分支 **77.95%**；CI 出 cobertura 并设 85% 阈值。
- L2 补充命令面：新增 `scripts/test/cli-extra.sh`（13 条），覆盖 `--shell-plan/--url-plan/--companion-*/--prefix-*/--runner-check/--convert-screenshots` 与假 Sophon 的 `--install-plan/--patch-plan/--install-fetch/--update-run`；已接入 CI。
- 修 bug：不完整/未知的子命令不再静默进 GUI（`--companion-run` 少一个参数就会「命令没反应、反而弹窗口」），现在打印「未知命令或不完整」并返回 1。
- 修 bug：`--install-fetch/--install-run` 新增 `--install-dir`，不必先让启动器把安装路径写进设置。
- L3 补跑：切换游戏（选择层）、打开图库、1000px 断点均通过；发现 **F-21**（视频壁纸起播后画面不推进，0.00% 帧差、CPU 0.01 核）。
- 修 bug：`--preflight` 结论为「不要启动」时返回 1（F-22），脚本可当门禁。
- 上游同步支撑：新增 `scripts/list-deviations.sh`（81 个同名文件偏差）与 `scripts/sync-upstream.sh`（冲突时对「已偏差文件」默认保留本仓），文档 `18-upstream-deviations.md`。

## 2026-11-13（二）— 游戏设置页布局重叠

- 修：`GameSettingPage` 内容 Grid 在异步初始化后显式 `InvalidateMeasure()`，解决「运行配置」与 HDR 分区重叠（D67 / L-119 / `22` P0-2）。
- 更正记录：`22` 的 P0-2 原判断「右列横向溢出」经像素测量否掉；P1-2 澄清为视口边缘的正常半行。

