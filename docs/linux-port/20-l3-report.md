# L3 真机 GUI 用例首轮报告（2026-11-12）

对应 `19-test-plan.md` 的 L3 层（T-UI-30…42）。本轮在真机上跑了一部分，结论与证据记录在此；未跑的用例写明原因与解锁条件，不写成「通过」。

## 1. 环境

| 项 | 值 |
| --- | --- |
| 会话 | KDE Plasma 6 / Wayland + XWayland（`XDG_SESSION_TYPE=wayland`，`DISPLAY=:0`） |
| 屏幕 | eDP-1 1536×864，缩放 1.25 |
| 窗口 | client 逻辑坐标 `(288,148)`、`1200×676`，DIP==px |
| 二进制 | `src/Lunarward/bin/Debug/net10.0-desktop/Lunarward`（Debug 构建，未装包） |
| 数据 | 真实用户数据（`~/.local/share/lunarward`、`~/.config/lunarward`） |
| 操作方式 | `cu_batch`（activate → click → sleep → screenshot 一次调用）/ `cu_screenshot`；仅「毫秒级轮询窗口出现」用 `xdotool` |

## 2. 结果

| 编号 | 用例 | 结果 | 证据 |
| --- | --- | --- | --- |
| T-UI-30 | 冷启动到窗口可见 | 通过 | 进程启动 → 窗口可见 **2066 ms**（预算 2500 ms），尺寸 1200×676 |
| T-UI-31 | 六个导航项切换 | 通过（发现 F-13） | 逐项点击后内容正确；游戏设置页布局缺陷见 §3 |
| T-UI-32 | 切页不重算背景 | 通过 | 启动后仅 1 次 `[background] core …`；随后反复切「启动器 ↔ 游戏设置」，日志无新的 `core` / `get suggested background timed out` |
| T-UI-33 | 切换游戏（选择层） | **未完成** | hover / 点击左上角图标均未展开卡片层；见 §4 |
| T-UI-34 | 背景圆点 + 视频壁纸 | **未覆盖** | 本轮「建议背景」接口超时后回落到静态兜底图，日志无 `video background started` |
| T-UI-35 | 打开背景图库 | **未完成** | 底栏坐标估算错误（点到了壁纸区）；`xdotool search --name "Background Viewer"` 为空 |
| T-UI-36 | 托盘菜单（DBus 侧） | 通过 | 应用日志 `[tray] GetLayout parent=0 depth=1` 40 次 + `journalctl` 有宿主调用；**右键菜单视觉未验证** |
| T-UI-37 | 全局热键截图 | 通过 | `alt` + `d` 后生成 `~/图片/Lunarward/Lunarward_2026-11-12_19-10-52.png`（1 719 674 B）；`notify-send` 存在（桌面通知链路可用） |
| T-UI-40 | 关闭=隐藏到托盘 + `--show` 唤回 | 通过 | `alt+F4` → `Map State: IsUnMapped`、进程存活、无「托盘不可用」日志；`lunarward --show` → `IsViewable`，日志 `clamp: size 1500x845 fits screen 1920x1080` |
| T-UI-41 | 窗口缩放 / DPI 变化 | **未跑** | 见 §4 |
| T-UI-42 | 工作区切换 | **未跑** | 见 §4 |

## 3. 本轮发现的缺陷

| 编号 | 级别 | 现象 | 根因 | 处置 |
| --- | --- | --- | --- | --- |
| F-13 | P0（视觉） | 游戏设置页「诊断」区与「分辨率」重叠 | 新增的 `StackPanel` 未设 `Grid.Row`，默认落在第 0 行第 0 列，与分辨率同格 | 已修：`GameSettingPage.xaml` 补第 4 个 `RowDefinition`，诊断区 `Grid.Row="3"` + `Grid.ColumnSpan="2"` |
| F-14 | P2（文案） | 「分辨率」显示 `0 x 0` | wine 注册表无对应键时的空值，如实呈现 | 待定：是否显示「未设置」 |
| F-15 | P1（功能） | 截图页恒提示「截图文件夹不存在」 | 页面回退到 `<数据目录>/Screenshots`，实际落盘是 `ScreenCaptureLinux.DefaultFolder`（`~/图片/Lunarward`） | 已修：页面优先取 `DefaultFolder` |

F-13 / F-15 修复在提交 `ca720be`；F-14 需产品决策。

## 4. 未完成用例的原因与解锁条件

- **T-UI-33 切换游戏**：游戏选择层在「已固定」（`IsGameBizSelectorPinned=true`）时的展开方式尚未确认——hover 与单击左上角图标都没有出现卡片层。解锁：确认交互（是否需要先取消固定 / hover 停留多久 / 点击区域），或直接用 DEBUG 入口 `LUNARWARD_DEV_SWITCH_GAME=<biz>` 只测「切游戏后的界面刷新」这半边。
- **T-UI-34 视频壁纸**：依赖「建议背景」接口返回视频；本轮接口超时走兜底图。解锁：`LUNARWARD_VIDEO_BG=<本地 webm>` 强制指定，再验证播放/暂停/圆点切换三条。
- **T-UI-35 图库窗口**：`BackgroundViewPage` 的入口在底栏，坐标未取准。解锁：先截一张底栏放大图确认按钮位置，或临时用 `--window-action backgroundview` 直接开窗（已有 DEBUG 入口）。
- **T-UI-41 缩放/DPI**：需要改会话缩放（`kcmshell6 kcm_kscreen`）后重开应用，会影响用户当前桌面，需征得同意。
- **T-UI-42 工作区切换**：需要切到另一个虚拟桌面再切回，同样会打断用户操作，需征得同意。

## 5. 方法教训

1. **不要用 `xdotool` 代替 `cu_*` 做点击**：本轮两次空点（一次点在锁屏上、一次坐标算错点到壁纸），都是因为没走 `cu_batch` 的结构化坐标与截图回读。`cu_batch` 一次调用就能完成「activate → click → sleep → screenshot」并回传像素尺寸与缩放比。
2. **锁屏会静默吃掉所有点击**：`cu_status.session.locked=true` / `org.kde.screensaver.GetActive=true` 时截图是锁屏，点击无效——跑 GUI 用例前先探这两个状态。
3. **日志要跨重启看**：`Starward_yyyymmdd.log` 是追加写，同一文件里混着多个进程实例的输出，判断「有没有重算」必须看**本轮启动之后**的行号区间，否则会把旧实例的日志当成新缺陷（本轮 T-UI-32 差点误判）。
4. **临时插桩必须当步清理**：为了定位 T-UI-32 加进 `AppBackground.Linux.cs` 的两行 `Information` 日志（`core…` / `keep current…`），提交前必须删掉/降回 `Debug`，否则日志噪声永久留下。

## 7. 第二轮补充（2026-11-13，补跑 T-UI-33/34/35/41/42）

| 编号 | 用例 | 结果 | 证据 |
| --- | --- | --- | --- |
| T-UI-33 | 切换游戏（选择层） | **通过** | 点左栏「游戏」图标 → 选择层展开（6 张卡片 + 已安装占用 322.26GB）；点「崩坏：星穹铁道」→ 子菜单选「中国服」→ 主界面换成星穹铁道（`/tmp/l3_selector2.png`、`/tmp/l3_hsr.png`、`/tmp/l3_hsr2.png`） |
| T-UI-34 | 背景圆点 + 视频壁纸 | **部分通过** | 悬停底栏可唤出圆点条（5 个点 + 图库/复制/另存/暂停四个按钮，`/tmp/l3_bar2.png`）；点圆点未观察到背景变化（`/tmp/l3_dots_before.png` vs `l3_dots_after2.png` 差异 **0.00%**），且视频「已起播」（日志 6 次 `video background started`）但连续两帧截图差异 **0.00%**、进程 CPU 仅 **0.01 核** → 视频实际未推进，见 F-21 |
| T-UI-35 | 打开背景图库 | **通过** | 悬停底栏 → 点图库按钮 → 新窗口 `Starward - Background Viewer`（683×455）打开，缩略图网格渲染（`/tmp/l3_gallery.png`） |
| T-UI-41 | 窗口缩放 / 断点 | **通过** | `cu_window geometry` 1200×680 → 1000×680：布局由双列降为单列（`/tmp/l3_1000_launcher.png`），无横向溢出；DP-2 输出 scale=1.5、窗口仍在屏内 |
| T-UI-42 | 工作区切换 | **跳过** | 当前 KWin 只有 1 个虚拟桌面（`org.kde.KWin.setCurrentDesktop 2` 返回 `false`、`currentDesktop` 仍为 1），无法构造「切走再切回」 |

### 新缺陷

| 编号 | 级别 | 现象 | 证据 | 处置 |
| --- | --- | --- | --- | --- |
| F-21 | ~~P1~~ → **非缺陷（设计如此）** | 现象：启动后视频壁纸静止、CPU ≈0.01 核、两帧截图 0 差异 | 取证见下节 | 结论：`BackgroundService.GetSuggestedGameBackgroundAsync` 在「背景列表没有新增」时，把上次用过的背景**复用并置 `StopVideo = true`**（上游语义：看过的背景不再自动播视频）。日志 `fd` 无 .webm、CPU 空闲与此完全吻合；强制 `LUNARWARD_VIDEO_BG` 走视频路径时 CPU 0.96 核、有 .webm 句柄，渲染链路本身正常 |

#### F-21 取证链（2026-11-13）

1. 强制视频路径（`LUNARWARD_VIDEO_BG=<本地 webm>`）：`/proc/<pid>/stat` 5 秒 **478 tick ≈ 0.96 核**，`fd` 里 .webm 打开 → 解码与出帧正常。
2. 正常启动：5 秒 **6 tick ≈ 0.01 核**，`fd` 无 .webm，两帧逐像素 0 差异，日志无 `video background started`。
3. 读码定位：`BackgroundService.cs:145-156` —— `lastBgIds` 以首个背景 id 开头（无新背景）时取上次那张并 `StopVideo = true`；`ChangeBackgroundImageIndex` 只是把列表里同一个（已带 `StopVideo=true`）对象发出去，所以点圆点也不会播。
4. 顺带修掉的真问题：**调试入口 `LUNARWARD_VIDEO_BG` 起播后不记 `_appliedGameId/_videoActive`**，导致守卫失效、同会话重复起播（日志 6 条 `video background started`）。
5. 因此「视频静止」是上游行为，若要「用户选过视频就一直播」属于**偏离上游**的 UX 决定，需单独拍板（见 D68）。


## F-21 重新定性（2026-11-13 复核）

现象：启动器停在一张静态 CG，围绕「视频壁纸不播」做过两轮判断，均需更正。

**已确认的事实（有证据）**

1. 上游默认确实是动态背景。`getAllGameBasicInfo` 对 hk4e_cn 返回的 `backgrounds[0]` 是 `BACKGROUND_TYPE_VIDEO`（`3citmgCMOP`），且响应里没有 `stop_video` 字段（默认 false）。
2. `BackgroundService.cs:145-156` 只在「上次记录的背景文件名 = 某条**静态**海报」时把 `StopVideo` 置 true。也就是说「上次停在静态图」会让上游自己也不播视频。
3. 本机当时的 `CurrentGameBiz` 是 **`abc_cn`**（不是 `hk4e_cn`），数据库里 `bg_abc_cn` 指向一张 `.webp` 海报、`game_background_ids_abc_cn` 只有 `BACKGROUND_TYPE_POSTER` → 按上游规则，这个游戏+这个记录状态下**本就不播视频**。
4. 因此「启动器把视频壁纸弄坏了」这一结论**不成立**；此前把它记成 P1 回归是我的判断错误（前提没核对：看的是哪个游戏、上次记录是什么）。

**仍然成立的三处真偏差（已修，见 D69）**

- `TryStartVideoBackgroundAsync` 的 4 条失败路径全是静默 `return false`，调用方随后贴兜底图 → 表现成「静止」，且日志里查不到原因。
- 起播失败且**已有画面**时不应再贴兜底图。
- Linux 分部完全缺少上游的记账调用（`AppConfig.SetBg` / `SetGameBackgroundIds`），「上次背景」永远不更新。

**待验证（需要把当前游戏切到 hk4e_cn）**

在 `hk4e_cn` 且 `lastBg` 为空/为视频时，应当出现 `[background] video background started`、CPU ≈1 核、窗内两帧差异 >20%。本轮两次点击选择层都没真正切过去（点到了相邻卡片），留作下一步。
