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
