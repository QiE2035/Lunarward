# UI 审查与性能记录（2026-11-12，进行中）

重点：**UI 缺陷 + 性能**。真机环境：KDE Plasma 6 / Wayland+XWayland，eDP-1 1536×864 @1.25，窗口 client `(288,148) 1200×676`（DIP==px）。操作走 `cu_batch`。

## 1. 已确认并修复

| 编号 | 级别 | 现象 | 根因 | 修法 |
| --- | --- | --- | --- | --- |
| F-13 | P0 | 游戏设置页「诊断」压住「分辨率」 | 新增 `StackPanel` 缺 `Grid.Row`，默认第 0 行第 0 列 | 补第 4 个 `RowDefinition`，诊断区 `Grid.Row="3"` + `ColumnSpan="2"` |
| F-15 | P1 | 截图页恒提示「截图文件夹不存在」 | 页面回退到 `<数据目录>/Screenshots`，实际落盘在 `ScreenCaptureLinux.DefaultFolder` | 页面优先取 `DefaultFolder` |

均已随 `ca720be` 提交，重测通过。

## 2. 已确认、待修

| 编号 | 级别 | 现象 | 证据 | 建议修法 |
| --- | --- | --- | --- | --- |
| F-16 | P1（可读性） | 数据类页面（抽卡记录、账号记录、战绩）没有实底，启动器的 banner 大字「万序衍盈 / VERSION 7.2」直接透在页面标题与内容后面 | 截图 `/tmp/ui-13-gacha.jpg`（「祈愿记录」与背景大字重叠）、`/tmp/ui-15-setting.jpg`（「账号记录」同上） | 与设置页同样处理：这几页根容器加 `ApplicationPageBackgroundThemeBrush`（D61 已定的做法），或统一给 `PageBase` 提供可切换的实底 |
| F-17 | P2（可用性） | 账号记录页主体区域全空，只有标题与右上「输入 URL」按钮；与抽卡记录页的「没有抽卡记录」空态不一致 | `/tmp/ui-16-setting.jpg` | 补空态层：说明 + 「输入 URL」引导（复用 `ShowRecordEmptyState` 的写法） |
| F-18 | P2（一致性） | 侧边栏底部齿轮（设置入口）点击无响应 | 点击 `(304,716)` 后仍在账号记录页；疑为 hover 才显示/命中区偏小 | 需要确认是否 hover 显隐；若是，改为常显并扩大命中区到 44×44 |

## 2b. 复查追加（2026-11-12 晚）

- **F-16 复核**：抽卡记录页那张「万序衍盈」大图属于**页面自身的祈愿 banner**，不是壁纸透出（与 `/tmp/ui-13-gacha.jpg` 比对确认），因此该页不算缺陷。**真正的透出只发生在账号记录/战绩这类纯内容页**（`/tmp/ui-f16-account.jpg`：标题「账号记录」与壁纸叠在一起，且没有任何丙烯底）。
- **F-19（P0，新发现）**：非启动器页上，左上角「已固定」的游戏选择器图标（原神 / 星穹铁道两个头像）仍然显示，直接压在页面标题区（`/tmp/ui-f16-account.jpg` 顶部 `(215,113)` 一带）。根因是固定态图标条不受页面切换控制——`GameIconsAreaVisible` 只看 `IsPinned`，没考虑当前是否为启动器页。

尝试过的对齐方案（已回滚，记录在此避免重复）：

- 想用「覆盖 `CustomAcrylicBrush` / `CustomOverlayAcrylicBrush` 为等效实色」一次性解决透出（上游 `AcrylicBrush` 在 Uno/Skia 下渲染为完全透明，Windows 的模糊由 DWM 负责）。实测**无效**：账号记录页仍然透明，说明这些页根本没用到该资源，`Border_OverlayMask` 也只是上游为过渡动效准备的遮罩。已 `git checkout` 回滚 `App.xaml`、删除 `AcrylicFallback.xaml`。
- 结论：F-16 的正解不是改画刷，而是让「非启动器页」有自己的底（要么按 D61 加实底，要么给 `PageBase` 提供统一容器）。

## 2c. 对齐上游后的再判定（2026-11-12 晚）

三条都回到上游代码比对，结论有修正：

- **F-19 撤回**：左上角两个游戏头像所在的位置是窗口顶部 48 DIP 的表头条。上游 `MainView.xaml:141` 的 `<Frame Margin="0,48,0,0" />` 与我们的完全一致，页面本来就从这个条下面开始，图标没有压住页面标题——是我在缩略图上目测错了。
- **F-16 定性**：上游这些页面（`GachaLogPage`、`GameRecordPage` 等）根容器都是透明，靠 Windows 的 DWM 丙烯（模糊+染色）保证可读；Uno/Skia 不实现丙烯模糊，所以同一个 XAML 在 Linux 上就变成「壁纸原样透出」。这不是代码写错，而是**引擎能力的软差异**。
  - 两个可选方向：①保持上游 XAML 零改动，把这条记进 D3 软差异（推荐，符合「优先对齐上游」）；②像设置页那样给这几个页面加实底（D61 做法），可读性更好但又是新的偏差。
  - 已尝试并回滚的方案：把 `CustomAcrylicBrush` 覆盖成等效实色——无效，因为这些页面没用到该资源（详见 2b）。
- **F-18 待定**：底部齿轮点击无响应尚未复现确认（可能是 hover 才显示/命中区偏小），下一轮用 `cu_batch` 逐点验证。

## 3. 静态扫描的高风险点（尚无用例覆盖）

| 项 | 数量 | 风险 |
| --- | --- | --- |
| 页面没有 `ScrollViewer` | `SettingPage.xaml`、`ToolboxSetting.xaml`、`GameRecordPage.xaml`、`LoginPage.xaml`、`BattleChronicleWindow.xaml`、`HotkeyInput.xaml` | 窗口高度小于内容时会被裁掉且无法滚动（L-106 同类问题） |
| 固定宽度的 XAML `Width="≥100"` | 100 处 | 小窗口/大字号下可能溢出（`GameSelector` 已踩过） |
| 长文本 `TextBlock` 未设 `TextTrimming`/`TextWrapping` | 84 处 | 路径、长名字会顶出容器（L-101/L-109 同类） |
| 仍引用 Win32 API | `SimulateInputTipsWindow.xaml.cs`（`User32.SetWindowLong`、`WS_EX_TOPMOST`） | M4 手柄/悬浮窗范围，Linux 无效但不崩；需在入口置灰（手柄页已做） |

## 4. 性能记录

| 指标 | 实测 | 说明 |
| --- | --- | --- |
| 冷启动到窗口可见 | **2066 ms** | 预算 2500 ms |
| 启动器 ↔ 游戏设置切页 | 首次 ~216 ms、再次 33 ms、回启动器 24–46 ms | D60 修完后的值 |
| 切页背景重算 | 0 次 | `_appliedGameId` 守卫生效，日志无新 `core` |
| 视频壁纸空闲 CPU | ~1.15 核（VP9 解码 ~0.55、Skia 绘制 ~0.35） | 用户已知并接受「先保持现状」 |
| 图库打开 | 一次性解码 16 张 2560×1440 → 已加 `DecodePixelWidth` | L-96 |

## 5. 下一步

1. 修 F-16（数据页实底）、F-17（账号记录空态）、F-18（设置入口命中）。
2. 把 L3 的页面巡检做成可回归的脚本：每页截一张图 + 用像素判据断言「标题区不与背景大字重叠」「内容区非纯背景」。
3. 逐页做「小窗口（1200×676 以下）与大窗口（最大化）」两档检查，专治第 3 节里的裁剪与溢出。
