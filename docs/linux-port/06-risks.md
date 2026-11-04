# 风险登记

- **状态**：v0.1
- **最后更新**：2026-11-01
- **规则**：每条风险必须有「触发条件」与「回退动作」；触发即执行，不需再讨论。

等级：**S** = 项目可能不成立；**A** = 颠覆性返工；**B** = 局部重做；**C** = 可承受。

---

## 1. 风险总表

| ID | 风险 | 概率 | 影响 | 等级 | 触发条件（可观测） | 缓解 | 回退 |
|---|---|---|---|---|---|---|---|
| R-01 | Uno Linux 后端无法承载 Starward 的 XAML（兼容缺口/性能/崩溃） | 中 | 极高 | **S** | M0 中抽样页面渲染失败率 > 20%，或启动即崩 | 先用最小页面集验证；优先修 Uno 缺口；用 `CommunityToolkit` 替代缺失控件 | 停止 Uno 路线 → 单独立项评估 Avalonia（D1 回退） |
| R-02 | Uno 内置 WebView2 在 Linux 上「能渲染、拿不到值」，登录链路断 | 中 | 高 | **B** | M0 实测：渲染/导航 OK，但 `CookieManager`、`Settings`、`Profile` 未实现，`ExecuteScriptAsync` 直接崩进程 | 内嵌 WebView 只承担展示（公告/BBS）；登录取值走独立 WebKitGTK 窗口或 passport API；两条路都已验证或已在社区被验证 | 全量改走「系统浏览器/独立 WebKit 窗口 + cookie 导入」（D12 原案） |
| R-03 | ACE 反作弊在 wine 下拒绝启动（原神/绝区零） | 中 | 极高 | **S** | 真机启动出现 ACE 报错或静默退出 | 严格保持「游戏是 wine 首进程」；比对社区已知可用参数；不注入任何东西 | 明确记录「当前不可用」，把验收游戏改为可控范围（需用户决策） |
| R-04 | 反作弊把「伴随启动/注入型工具」判定为作弊导致封号 | 中 | 极高 | **A** | 社区出现封号报告；或游戏内弹出异常 | 默认全部关闭；注入型条目在 UI 上强警示；不提供任何注入实现（只做编排）；首次启用需二次确认 | 移除注入型支持，仅保留纯外部工具 |
| R-05 | 前缀克隆在不支持 reflink 的文件系统上过慢（ext4/NTFS） | 高 | 中 | **B** | 克隆 > 10s 或 > 5GB 占用 | 启动前探测文件系统；UI 显示预估；建议用户把前缀库放 btrfs/XFS | 提供「共享前缀」模式作为默认，模板模式标注「推荐 btrfs」 |
| R-06 | 上游快速演进导致 cherry-pick 冲突不可控 | 高 | 中 | **B** | 单个上游 release 的冲突文件 > 30 | 平台层独立目录；上游文件少改；单向 cherry-pick | 冻结上游（锁定 0.18.x），只做安全更新 |
| R-07 | 米哈游 API（Hyperion/HoYoPlay/BBS）变更导致战绩/公告/登录失效 | 中 | 高 | **A** | 接口返回错误码/结构变化 | 复用上游 `Starward.Core` 已实现的容错；跟随上游修复 | 局部功能降级（隐藏入口），核心启动不受影响 |
| R-08 | 启动链多一层 fork 破坏「游戏是首进程」 | 中 | 高 | **A** | `/proc` 显示游戏父进程为 `proton`/`umu-run`/`sh` | 强制 `exec`；提供「直连模式」；计划预览显示进程链 | 只保留 `wine` 直连路径，隐藏 `proton run`/`umu` |
| R-09 | 伴随启动被滥用（外挂/修改器）带来法律与道德风险 | 中 | 高 | **B** | 社区把本项目当作外挂启动器 | 文档明确边界；不实现注入；不内置任何修改器 | 移除伴随启动的注入类模式 |
| R-10 | 托盘/通知在不同桌面（GNOME/KDE/Hyprland）表现不一致 | 高 | 低 | **C** | 某桌面下无托盘图标/点击无效 | 分桌面矩阵测试；GNOME 提示安装 AppIndicator 扩展 | 降级为「窗口常驻 + 应用内提示」 |
| R-11 | Wayland 下窗口管理/全局热键能力缺失 | 高 | 中 | **B** | 热键不生效、窗口无法定位 | Wayland 原生 + XWayland 双路径；热键降级为应用内 | 默认走 XWayland；文档说明 |
| R-12 | 图形组件（DXVK/VKD3D）版本矩阵爆炸，组合测试成本高 | 高 | 中 | **B** | 用户报告「某组合黑屏」频发 | 组件指纹 + 启动报告归因；提供「已知良好」预设 | 只推荐 2–3 个经真机验证的组合 |
| R-13 | 打包与分发（AUR）维护成本、依赖漂移 | 中 | 中 | **C** | 用户构建失败率上升 | 提供 tarball 自更新；CI 构建产物 | 只发 tarball |
| R-14 | 项目范围膨胀（想做的太多）导致长期不可交付 | 高 | 高 | **A** | 里程碑连续两次延期 | 严格按 `07-roadmap.md` 划范围；非目标清单（N-01–N-06）不可协商 | 砍 M3/M4，锁定 M1/M2 交付 |
| R-15 | 上游自研包可移植性差异（Codec 可移植 / NativeLib 半可移植 / Win2D 与 GameInput 不可移植） | 已确认 | 中 | **B**（原为 A，补全源码后下调） | 实测：`Codec` 是跨平台原生库（libavif/libjxl/libultrahdr/lcms2/brotli/libyuv）的 C# 包装层，只 `VP9Decoder` 是 Windows MFT；`Snap.HPatch` 纯 P/Invoke 可跨平台；`SharpSevenZip` 依赖 `kernel32!LoadLibrary`+COM；`Win2D`=D2D 投影、`SharpGameInput`=GameInput 绑定 | ① `Lunarward.Codec` 取源码自建 + DllImport 名称映射 + Linux 原生库；② 7z 换 `SharpCompress`；③ Win2D 按接口换 SkiaSharp（3 个 D2D1 效果重写）；④ 手柄裁剪到 M4 | 若 `libultrahdr` 等原生库在发行版缺失且构建复杂 → HDR gainmap 走 CPU 降级、其余功能不受影响（不会回退到「只显示缩略图」） |

---

## 2. 关键风险详解

### R-01 Uno 后端（最高优先级）

**为什么危险**：Starward 用了 82 个 XAML、126 个文件依赖 `Microsoft.UI.Xaml`，其中包含 NavigationView、TabView、InfoBar、TeachingTip、AnimatedIcon、Mica 背景等 WinUI 特有控件。Uno 的 Linux 后端虽然声称覆盖大部分，但实际项目里总会有 5–20% 的「看起来能用、用起来不对」。

**M0 门禁的关键取样**（必须全部渲染成功才算过）：

1. `ViewHost/MainView.xaml`（导航 + 内容宿主）
2. `GameLauncher/GameLauncherPage.xaml`（复杂布局 + 图片 + 进度）
3. `Setting/SettingPage.xaml`（表单 + 分组 + 开关）
4. `GameRecord/GameRecordPage.xaml`（TabView + 数据网格）
5. `Gacha/GachaPage.xaml`（列表 + 图表）
6. 一个 `ContentDialog`、一个 `TeachingTip`、一个 `InfoBar`

**判定**：任何一项出现「布局错乱 / 控件缺失 / 绑定不生效 / 崩溃」，先计入缺口并尝试替代；缺口 > 20% 触发回退。

### R-03 / R-04 反作弊

这两条决定了项目的「可用边界」，必须诚实沟通：

1. **本项目不提供任何绕过反作弊的实现**；所有 patch/hook/inject 能力都由第三方工具提供，本项目只负责时机与环境；
2. 「伴随启动」默认关闭，首次开启有明确风险提示；
3. 如果真机上 ACE 拒绝启动（R-03 触发），**不是通过加大投入就能解决**的问题，需要用户重新确认验收范围；
4. 任何时候不承诺「一定不被封号」。
| R-16 | 登录助手是外部 python 进程 | M1 起 | 中 | **B** | `WebLoginService` 拉起 `python3 login.py`（PyGObject + WebKitGTK 2.54），用户环境若缺 `python-gobject`/`webkit2gtk` 则登录不可用；且存在进程间协议与超时管理成本 | ① 依赖检测：启动时检查 `python3 -c 'import gi'` 与 `sec-login` 可用性，缺失则 UI 明确提示而非静默失败；② 打包时把依赖写进 AUR/AppImage 依赖项；③ M2 换 GirCore 进程内实现（D32），接口不变 | 若 M2 的 GirCore 路线受阻 → 保留外部助手但改为随包分发的静态 Python 环境，或退回直调 passport API（D29 备选②） |

---

## 3. 监控与复核

| 风险 | 复核节点 | 复核方式 |
|---|---|---|
| R-01 | M0 结束 | 门禁清单逐项截图留档 |
| R-02 | M0 中段 | 登录 PoC 走通「打开→登录→拿到 cookie→关闭」 |
| R-03 | M1 首个可运行版本 | 真机启动 `hk4e_cn` + `nap_cn` 各 10 分钟 |
| R-04 | 每次伴随启动功能变更 | 默认值与提示文案评审 |
| R-05 | M1 | 在用户实际文件系统上测克隆耗时 |
| R-06 | 每 2 周 | 上游 release 对比 |
| R-07 | 每次上游同步后 | 冒烟：公告/战绩/抽卡各取一次 |
| R-08 | 每个里程碑 | 检查进程树（`ps -o pid,ppid,comm`） |
| R-12 | M2 | 组合矩阵抽样测试 |
| R-14 | 每月 | 对照 `07-roadmap.md` 检查进度 |

---

## 4. 否决式条件（出现即重新立项）

1. M0 门禁连续两次不通过；
2. 真机两款游戏在 M1 结束时仍无法进入主界面，且原因在 wine/反作弊层面（非本项目可控）；
3. Uno 后端出现「无法通过修改本项目解决的架构性缺陷」。
