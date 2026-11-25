# Changelog

## 2026-11-25 —— 上游 GameInstall 库的三处 Windows-only 代码移植到 Linux（D131）

- **更新时不再白下载**：硬链接复用以前在 Linux 上直接判「不支持」（上游只认 NTFS/ReFS），现在按文件系统判断（ext4/btrfs/xfs 等可用），用 `.NET 11` 内建的 `File.CreateHardLink` 建链接；失败一律回退下载，最坏情况与之前相同。
- **截图备份**：不再用「路径根相同」判断同卷（Linux 上恒真），改成「文件系统是否支持硬链接」，跨卷自动回退复制。
- **热更新路径修正**：上游把 `@"Persistent\ChannelName"` 这类子目录写成单个字面量，Linux 上反斜杠会变成文件名字符，永远匹配不到；11 处统一拆成多段 `Path.Combine`（L-188）。
- **热更新在 Linux 上仍不启用**（`CheckUpdateAsync` 直接返回 false）：服务器名要读 Wine 注册表、打补丁要能在 Linux 上建硬链接，两块补齐之前半可用状态更危险。
- 新增 `HardLinkCompatTests` 7 条（共享 inode、已存在链接、目标缺失、跨文件系统等），`dotnet test` 352 通过。

## 2026-11-25 —— 0.19.0 编译收口：Skia 桥、toolkit 替身与启动链修复（D130）

- **视频壁纸换渲染桥**：Uno 7 下 `SkiaSharp.Views.*` 没有可用资产（`UNOB0023`），改用 `WriteableBitmap` 双缓冲交替贴给 `Image`；同时迁移 SkiaSharp 4 的 `SKSamplingOptions` API。
- **toolkit 包不再引用**：`CommunityToolkit.WinUI.*` 在 Uno 工程里会选到 Windows 资产导致两套 `DependencyObject`，恢复手写替身并补齐 `WrapLayout`（真折行）/`EqualPanel`/`Segmented`/`FindDescendant`。
- **修复「启动没反应」**：GUI 路径补 `InitEnviroment()`（此前只有 CLI 分支调用，导致每次启动弹欢迎向导且日志目录为 null）；补回 `CompanionRunner`/`GameRecordService`/`GameAuthLoginService` 的 DI 注册。
- **修复 `--preflight` 报 `no such table: GameEntry`**：碰数据库的 CLI 分支统一先做环境初始化（0.19.0 起安装路径在 SQLite 里）。
- **测试工程跟到 net11**：`dotnet test` 340 通过（删掉 5 个指向已删 Sophon 文件的用例）；`--selftest-ui` 在 xvfb 下 11/11 PASS。

## 2026-11-25 —— 手写补齐实现审计：空壳让位真实实现（D129）

- **战绩窗口不再空白**：点「战绩」以前弹一个纯白窗口；现在显示「Linux 版暂时不能打开战绩页面」+ 原因 + 当前角色（`BattleChronicleWindow.Linux.cs`）。
- **所有窗口都有图标**：`WindowBase.SetIcon()` 从空实现改为复用主窗口那套 libxcb `_NET_WM_ICON` 写入（`Helpers/AppIcon`），任务栏/Alt-Tab 不再是通用方块。
- **深浅色跟随桌面**：`ShouldAppsUseDarkMode()`/`ShouldSystemUseDarkMode()` 以前恒 `true`（浅色桌面的 Gacha/更新/过场窗口被强制深色），改为读 GTK `settings.ini` / KDE `kdeglobals` 推断（新文件 `Helpers/SystemThemePreference.cs`，只读文件、不 fork）。
- **「立即更新」可点且有解释**：按钮以前恒禁用、点了也没反应；现在点击给出「请用包管理器（AUR / AppImage）升级」，并且走 `SetErrorMessage` 才会真正刷新界面。
- **子窗口 DPI 不再算错**：`XamlRootExtension.GetAppWindow()` 以前一律回落到主窗口，现在先按 `XamlRoot` 从 `WindowBase` 的窗口登记表反查真正的 `AppWindow`。
- 文档按实测重写：`12-shims-and-gaps.md` 的替身规模 36→33、删掉 D128 已删的 4 行、基类路径改名、截图查看器降级为 🟡、补记 user32 P/Invoke 与兜底风险；`18-upstream-deviations.md` 同名差异文件 88→80（L-183）。

---

## 2026-11-25 —— 合并上游 0.19.0：切到 .NET 11 + Uno 7（D128）

- **运行时对齐上游**：SDK 11.0.100（`rollForward latestFeature`）、Uno.Sdk 7.0.22、TFM `net11.0-desktop`；`Microsoft.Extensions.*` 等一批依赖跟到 11.0.0。留在 net10 要给上游的 `ZstandardStream`/`ReadOnlyMemoryStream` 打 BCL 补丁、还要反编译只发 net11 资产的 HPatch，属于「适配」而非「移植」。
- **安装/热更改成上游实现**：`Starward.GameInstall` 库 TFM 钉 `net11.0` 真移植进来，UI 侧 `GameInstallService` 重写为**进程内 facade**（公开 API 与上游逐字一致），插进 `AddStarwardGameInstall()`。
- **删掉移植自研 Sophon 管线**（`Features/GameInstall/Sophon/*` 2663 行 + `GameInstallService.Linux.cs` 657 行 + `Hi3Helper.Sophon`/`ZstdNet` 依赖 + `libzstd.so` 构建 hack），CLI 同步删掉 `--install-run` 等 7 个开关；离线假 Sophon 服务（`chunk-smoke.sh`/`fake-sophon.py`）与 CI 步骤一并退役。
- 基类跟随上游：`Frameworks/PageBase.cs`→`Features/PageBase.cs`、`Frameworks/WindowEx.cs`→`Features/WindowBase.cs`（类名 `WindowBase`），D94 的 `OnGameChanged()` 页面缓存钩子补回上游版 `PageBase`。
- 背景、战绩、选择器、公告、设置页按 0.19.0 新模型重做（`GameBackgroundModel`、启动器多配置、8 个分游戏战绩客户端、`ContentDialogBase`）；手写替身 `ToolkitControls`/`InteractivityCompat`/`ToolkitCompat` 停用，改吃真实 toolkit 包（L-179）。
- 修掉迁移期踩到的真 bug：`WrapPanel` 无 `ItemWidth/ItemHeight` 导致背景页不再自适应列宽、`ItemContainerTransitions="{x:Null}"` 让 Uno XAML 编译器崩、`git checkout csproj` 冲掉未提交改动（L-181/L-182）。
- 新增 Linux 版设备指纹 `Features/Passport/WindowsDeviceFpService.cs`（machine-id + `/sys/class/dmi/id` + `/proc/cpuinfo` + NVIDIA/Vulkan 探测，替代上游 DXGI/注册表实现）。

## 2026-11-24 —— 评估 NV12 直出与零拷贝：两条都不做（D127）

- NV12 直出（libvlc 给 YUV、我们转 BGRA）在隔离 POC 里省一半解码 CPU，但接进应用后更贵：VLC 的 RV32 转换是 SIMD 汇编，我们的是标量 C#，Release 同机实测 RV32 70% → NV12 92%。代码已回退，不留开关（D127）。
- 顺带查出 libvlc 的 NV12 会写超 `1.5 × W × H`（1080p 多 6 行），按理论大小分配会稳定段错误，记进 L-177。
- 零拷贝 POC：SkiaSharp 用 `GRContext.CreateGl` + `GRBackendTexture` + `SKImage.FromTexture` 能直接包外部 GL 纹理（`IsTextureBacked=True`），NVIDIA 与 Mesa 两套 EGL 都跑通；但 libvlc 3 的 `vmem` 没有 dmabuf 出口、Uno 渲染循环也没有注入外部图像的入口，端到端仍要回读，所以不做（L-178 / D127）。

## 2026-11-24 —— 背景视频的帧率改成「量出来的」，去掉写死的 30 fps（D126）

- 以前没设 `LUNARWARD_VIDEO_FPS` 就按 `1000 / 30` 限帧，等于替视频决定帧率：24 fps 素材被当成「帧来得慢、不用限」，60 fps 素材被砍掉一半。用户指出这违背「视频播放要和视频本身一致」。
- 试过直接从 libvlc 取：`MediaPlayer.Fps` 在 vmem 输出下恒 0；`Media.Parse` 之后 VP9/WebM 的 `VideoTrack.FrameRateNum/Den` 也是 0；`ffprobe` 要 fork 外部进程，UI 线程不许。结论写进 L-176。
- 改成量 vmem 帧到达节奏：环形缓冲 16 帧，样本 ≥8 帧后取「跨度 /(n-1)」当每帧周期，打 8 折当合成间隔，10–240 fps 之外当作异常不限帧。落定后打一行 `video background: measured {period} ms/frame` 便于排障。
- `LUNARWARD_VIDEO_FPS` 仍可显式覆盖。真机 1920×1080 VP9 壁纸：量出 30.0 ms/帧，CPU 65–68%（与 D123 的 74% 同量级，不作为收益结论）。

## 2026-11-24 —— 覆盖率再加一道「增量行」门（D125）

- 总量门限对局部稀释不敏感：D121 一次加 147 行零测试代码，总量只掉 2.5pp，补两条无关断言就能抬回绿色。`coverage.sh` 因此增加增量模式：`LW_COVERAGE_DIFF_BASE` + `git diff --unified=0` 取本次新增行，与 cobertura 逐行命中求交，只判「新增行里被覆盖的比例」（阈值默认同总量 85%）。
- 先跑历史数据再上线：D121 批 K 命中 **52.43%（162/309）→ 红**（正是要拦的），D120/D123 的改动都在未链接文件里（明确打印「无可测新增行」后跳过），D122/D124 为 100%。
- CI：`actions/checkout` 改 `fetch-depth: 0`，coverage 步骤传 `github.event.before || pull_request.base.sha`。局限写进测试计划：不在这 23 个链接文件里的改动永远逃过两道门。

## 2026-11-24 —— 文档全量校准：40 余处错误结论与漂移的数字（D124）

审查文档本身也需要审查：本轮把 `docs/linux-port/` 14 份逐条对着代码复核，改掉四类错误。

- **把用法错误写成平台限制**：`AppBackground.Linux.cs:87` 说「Uno/Skia 的 `Image` 不重绘像素更新」，实际是复用同一个 `Bitmap` 实例不重采样（L-71 的修法一直在用），还误引了 L-87；同类还有 L-24（`IsTextTrimmedChanged` 6.7.359 自带）、L-74（`Launcher.LaunchUriAsync` 在 Skia/X11 有真实实现，不是「恒失败」）、L-171（`Consolas` / `Segoe MDL2 Assets` 本机其实有字形，`ttf-wps-fonts` 提供）。
- **数字漂移**：`24` 的 48 处 `Process.Start` → 存活 **43**（50 字面量 − 9 死代码 + 2 非字面量）；`18` 的 81 个同名文件 → **88**；`19` 的 230 条 → **237**（225 T + 12 RG）；`12` 的 33 个 `*.Linux.cs` → **36**、55 条排除 → **53**、XAML 差 7 处 → **32 实质 + 5 仅编码 + 6 缺失**；README 的上游领先 118 → **165/2**。
- **互相矛盾**：`23` 一边写「不用于是托管解压、调 xz/tar」一边写 BCL TarReader；manifest `layout` 取值照抄必解析失败；页签数 5/4 与实现的 3 不符；`wine reg query` 冷热耗时在三处分别是 1.1 s / 4–5 s / 4128–5411 ms，统一为「热 0.13–0.2 s、冷 1–5 s（机器相关）」。
- **编号失序**：`13` 有两个 L-119；错序的 L-160 / L-162 分别改为 **L-173 / L-174 / L-175**，标题内标注原编号并附勘误表。

机制上补一道闸：新增 `scripts/test/lint-doc-status.sh`（秒级、不构建）校验 L-*/T-* 编号唯一、`12` 的替身集合与 csproj 排除条数、`18` 的同名文件数、文档份数，已挂进 CI 的 Lint 段（`--strict`）。测试计划与 README 里的例数改成实测口径：`cli-smoke` 41 例（40 通过 + 1 跳过）、`cli-extra` 32 例、`--selftest-ui` 11 条。

## 2026-11-24 —— 背景视频改用硬件解码（D123）

- 背景视频壁纸以前给 libvlc 传 `--avcodec-hw=vaapi` 不生效（那是输入级配置，只能作为**媒体选项**）——所以旧结论「硬解没收益」是假的。改成 `media.AddOption(":avcodec-hw=vaapi")` 后日志才真正出现 `using hw decoder module "vaapi_drm"`；没有 VAAPI 的机器由 VLC 自己回退软解。
- 合成路径少拷一次整帧：`_pixels` 固定后由 `SKSurface` 直接绘制，去掉中间 `_composed` 位图与每帧 8.3MB 的 `Marshal.Copy`。
- 留 `LUNARWARD_VIDEO_HWDEC=0` 开关，遇到花屏/绿屏可退回软解。
- 实测（1920×1080 VP9 壁纸，20 秒采样）：整体 **112% → 74% CPU**；其中「软解但只拷一次」= 100%，说明硬解贡献 −26pp、零拷贝贡献 −12pp。

## 2026-11-24 —— CI 覆盖率门限口径修正 + 真实用例补齐（D122）

- 覆盖率报告不再把测试文件自己算进分母：口径只留 `src/Lunarward/`（`tests/coverlet.runsettings` 加 `ExcludeByFile`）。修正前报告里的 82%–88% 掺了「测试代码几乎全覆盖」的水分，真实 src 覆盖只有 81.58%；修正后实测 **85.30%**，门限仍是 85%（不靠调阈值过关）。
- 补齐真实用例（305 → **379** 条）：下载源 JSON 解析 / 缓存命中与淘汰 / ETag 条件请求 / 5xx 重试 / 限流头 / 网络异常（新增 `ComponentSourceFetchTests` 24 条）；输出捕获与启动历史落盘、压缩、坏文件容错、时间与时长的不变文化（新增 `RuntimeStorageTests` 18 条）；运行器显示名与来源段（新增 `RunnerNamingTests` 17 条）；增强项展开、可执行位、缺什么的口径、版本标记、前缀路径边界（补 10 条）。
- 顺带修一个真 bug：资产缓存从来没记住响应头里的 `ETag`（写的是进函数时的 null），`If-None-Match` 永远发不出去、304 分支是死代码。
- `19-test-plan.md` 写清口径：分母 = 链接集、不含 `tests/`、不覆盖未链接的主程序代码。
- 顺手修一条 CI 假失败：`Describe` 的用例原先写死 `09:30`（+08:00 展示），CI 跑在 UTC 拿到 `01:30` 就红；改为按同一 instant 的本机时区算期望值，并顺带断言 de-DE 下 `DurationMs` 仍显示 `1.5 s` 而不是 `1,5 s`（`38b5f191`）。

## 2026-11-24 —— 运行环境审查修复：批 K「一致性 / 文案 / 无障碍 / 死代码」（D121）

- 「受管组件」改用真实路径比较：软链、尾斜杠、`..` 写法的前缀不再被误判为非受管（R98）；手动添加的运行器写入后立即出现，不必重启（R99）。
- 缺 `wineserver` 的自定义运行器不再借本机系统 Wine 顶替，预检如实报错（R100）。
- MangoHud 改看 Vulkan 隐式层，不再因为系统没装 `mangohud` 可执行文件就判定「增强不可用」（R101）。
- 俄文 / 中文游戏名解压后目录名保留原字母，超长名截断也不再重名（R102）。
- 改过内置下载源的字段后保存，不再把内置源整体固化成用户副本、墓碑不丢（R103）。
- `.zip` 安装包认得出，`.tar.gz` 不再被当成 zstd 分块下载（R104）。
- 资产缓存最多 64 项并淘汰旧文件；源返回 5xx 自动重试一次；网络不通报「连不上源」而不是笼统失败（R105）。
- 启动日志里 `token=…` / `password: …` 的值被替换成 `***`，键名保留（R106）；日志目录跟随 `XDG_DATA_HOME`，中文输出不再乱码，进程退出后最后几行不再丢（R107）。
- 用户配方里自己写了 `WINEDEBUG` 就不覆盖；`LD_LIBRARY_PATH` 统一「前置 + 保留旧值」，不会把系统库路径整段抹掉（R108）。
- 环境变量键非法（带空格/中文/等号）会被拒收并提示，`wineprefix` 与 `WINEPREFIX` 不再算两个键（R109）；游戏参数与配方参数各自拆词，带引号的参数不再串味（R110）。
- 启动历史与「上次可运行配置」的读写加了锁，并发启动不再互相覆盖；时间与时长固定用不变文化，非中文系统不再是 `1,5 s` 或佛历年份（R112）。
- 下载表「已安装」改为规范化后精确匹配，装了 10.0 不会再显示 1.0 已装（R113）。
- 配置文件已不存在的方案，删除时如实报「文件不存在」而不是假装成功（R115）；默认运行器写失败会提示（R116）。
- 「镜像前缀」「列表分隔符」「页签已选中」三处文案进语言资源（R117）。
- 清理 43 个无引用语言键、四个从没有入口的按钮与若干死方法（R118/R119）；几处过时注释与重复文档注释改对（R120）。
- 前缀侧写会跟着架构变更更新，诊断包不再带旧架构；克隆前缀对空目录名给出明确报错并带超时（R122/R123）。
- 预检只把真正可执行的 `wine`/`gamemoderun` 当可用，不再出现「预检通过、启动报 Win32Exception」（R124）。
- `/usr/local/bin/wine` 不再被当成系统 Wine；`/run/media`、`/media`、`/mnt` 下的 Wine 能区分来源（R125）。
- Steam 库路径被忽略时写日志（R126）；前缀架构探测不再只读 32 行，ARM64 上 win32 前缀会报冲突（R127）。
- 克隆方案保留「旧版单 graphicsId」信息；同一运行器一次运行只自检一次，自检提示不再和「已保存」叠在一起（R128）。
- 去掉 Linux 上不存在的 `Consolas` / `Segoe MDL2` 字体：前缀路径、启动预览、⋯ 按钮不再整段空白（R129）。
- 运行环境页的页签、健康状态点补上无障碍名，菜单分隔线不再被读屏念「点 点 点」（R130）。
- 验证：单元测试 305；cli-smoke 40/0/1；cli-extra 32/0；lint 通过；Debug/Release 构建 0 错误。

## 2026-11-24 —— 运行环境审查修复：批 J「交互与状态」（D120）

- 新增 `DialogGate` 统一弹窗入口：已有对话框打开时后续点击直接忽略，连点「检查/清理/删除」类按钮不再抛 `Only a single ContentDialog can be open` 而崩（R86）。
- 「管理下载源」里的编辑 / 删除 / 恢复默认先收起管理窗再执行，操作完重开，不再叠第二层弹窗（R87）。
- 顶部「图形层 N 个」chip 跳到组件页，不再误跳到前缀页（R88）。
- 运行器/组件的 4 个按钮 tooltip 真正生效（此前初始化方法定义了但从没调用，R89）。
- 前缀卡片不再同时显示「未使用」和「被 1 个方案使用」（R90）。
- 编辑器与增强页有未保存改动时，点左列别的方案或从增强页返回都会先确认，不再静默丢草稿（R91）。
- 保存失败后点普通保存不再莫名弹出「运行一次」；「运行一次」失败不再冒充「保存失败」（R92）。
- 删除组件与「清理未用组件」统一归一化路径比较，手改过 `~`/尾斜杠的配置不会再把正在用的运行器当「未使用」删掉（R94）。
- 运行器行的「被 N 个方案使用」改为一次读盘算全表，不再每个运行器读一遍配置（R95）。
- 点某个前缀的「被 N 个方案使用」只列这个前缀的方案，不再抛全站占用汇总（R96）。
- HUD 首项改为「关闭 / Off」（英文界面不再显示 Close），HUD 下拉补字段名，没选方案时保存给提示（R97）。
- 验证：单元测试 286；cli-smoke 40/0/1；cli-extra 32/0；lint 通过。

## 2026-11-24 —— 运行环境审查修复：批 I「UI 线程与资源」（D119）

- 「添加运行器」不再在 UI 线程等 `wine --version`；打开编辑器改为后台扫描后建控件（R77/R78）。
- 详情区与启动预览只读组件缓存，冷缓存时显示 Id/路径而不是触发全盘扫描（R79）。
- 读配置不再顺手写盘（R80）；快捷键处理器在页面卸载时解挂，不会越积越多（R81）。
- 清理前缀/移除图形层改后台执行（R82）；组件页并发加载用代次号去重，异常不再被吞（R83）。
- 组件缓存加锁 + 代次号，过期扫描结果不再覆盖新缓存（R84）。
- 源探测共用客户端并限制 4 路并发（R85）；8 个 `async void` 处理器补 try/catch（R93）。

## 2026-11-24 —— 运行环境审查修复：批 H「方案、绑定与持久化」（D118）

- 新游戏自动建方案改为逐字段克隆，HUD / gamemode / gamescope 不再被静默关掉（R65）。
- 旧目录迁移遇到同名文件不删源，改名留档并告警（R66）；历史图形层 id 扫不到时保留，外置盘未挂载不再丢配置（R67）。
- 默认方案改为显式标记（方案页 ⋯ →「设为默认环境」），不再随排序/locale 漂移；默认方案显示徽标（R68）。
- 方案名规范化 + 自动去重，重名方案不再共用前缀目录（R69）。
- 写绑定失败改为返回值 + 界面提示；绑定文件损坏时删除方案先停止，不再误判「没人在用」（R70/R71）。
- id 比较统一 `Ordinal`，新 id 16 位；读配置大小写兼容、带 schema 门禁（R72–R74）。
- 配置读取异常分流：内容损坏才改名留证，磁盘/权限错误保留原文件；诊断包导出绑定错误与原始文本（R75/R76）。
- 验证：单元测试 286；cli-smoke 40/0/1；cli-extra 32/0；lint 通过。

## 2026-11-24 —— 运行环境审查修复：批 G「下载与导入」（D117）

- 分片下载要求 206 + `Content-Range` 起点匹配，镜像忽略 Range 时立刻降级；按分片长度截断写入。
- 暂停/取消不再删分片（与「已暂停，分片已保留」一致），只有真正的失败才清理。
- 图形层「像不像」判定统一到 `GraphicsLayouts`，导入后复核，失败回滚，不再出现「导入成功但列表看不到」。
- 解压统一上限：总字节（归档 + 512 MB 起、25 倍压缩比、8 GiB 封顶）、50 万条目、单文件 4 GiB，zip/tar/tar.xz 一致。
- digest 只接受 `sha256:`，其它算法按「无源端摘要」处理，不再必然报 SHA-256 不一致。
- 资产名归一化（拒绝路径分隔符与点开头），并用锁文件对同一文件做跨进程互斥。
- 下载超时改成 60 秒无进度看门狗，不再用 30 分钟总超时掐断大包。

## 2026-11-24 —— 运行环境审查修复：批 F「启动链路」（D116）

- 前缀锁改到 wineboot 之前拿，双开不再同时初始化同一前缀；拿锁后任何异常都会释放锁，不再出现「前缀永久被占用」。
- wineboot 先写 `init.pending`、成功才删；超时的受管半成品前缀会被清理，下次启动自动重建，缺 `system.reg` 的目录不再当成有效前缀。
- 前缀校验失败降级到 `~/.wine` 时不再往里装 DXVK/VKD3D，避免污染全局前缀。
- 调试 Shell 与「运行一次」按当前游戏解析真实前缀（CLI 用 `--game <biz>`），不再是空的 `WINEPREFIX`。
- wineboot 复用 Proton 的 `WINEDLLPATH` / `LD_LIBRARY_PATH` / `PROTONPATH`，建前缀与启动游戏环境一致。
- 图形层安装改为 staging + 备份回滚，装一半失败不会留下新旧 dll 混搭；文件大小记录缺失不再判「已安装」。
- 顺带修：Wayland 下复制剪贴板启动的 `wl-copy` 不再继承应用 stdout，`cli-smoke` 的 `out=$(…)` 不再被守护进程拖住（L-169）。

## 2026-11-24 —— 运行环境审查修复：批 E「数据安全」（D115）

- 下载/导入的组件解压后补齐执行位：zip 读外层 Unix 属性，tar.xz 按 ELF 文件头兜底补 u+x（SharpCompress 不暴露 Mode），默认源 wine-staging 装完不再显示「没有可执行权限」。
- 方案加稳定的 `PrefixSlug`：前缀目录不再随显示名变化，改名不再把已装的 DXVK/注册表留在孤儿目录；旧配置升级时自动回填，目录不变。
- 配方 JSON 里显式 `null` 的 `env`/`wrappers`/`name` 在读入时归一，不再延后成运行期 NRE。
- 首次使用自动建方案的保存失败时不再落绑定，明确退回默认环境并记日志。

## 2026-11-23 —— 组件页「添加」改为分段来源菜单（D114 修订 4）

- 「添加运行器…」：下载运行器… / 选择本地运行器…；「添加图形层…」：下载图形层… / 导入本地安装包… / 从文件夹导入…。
- 下载弹窗按段过滤源（运行器 = wine / proton，图形层 = graphics），标题随之变化；「更多」不再重复导入项。
- 暂存区不放进「添加」：它只列出未完成的分片并可删除，不能导入。

## 2026-11-23 —— 组件页按语义层级重排（D114 修订 3）

- 顶部独立「下载源」信息条：启用/总数一目了然，附「检查全部」与「管理下载源…」（下载源服务运行器与图形层两类组件，不再是图形层专属）。
- 运行器段标题行只留「添加运行器…」与「更多（设为新方案默认）」；图形层段标题行只留「添加图形层…」、筛选与「更多（导入/暂存区/清理）」。
- 两段标题带数量；「新建方案」从页头移到方案段顶部，去掉重复的内联入口。

## 2026-11-23 —— 组件页撤回页内分段，恢复单页并修正列表宽度（D114 修订）

- 组件页恢复「运行器在上、图形层在下」的单页结构，「添加组件…」「管理下载源」不再藏在分段里（用户反馈分段又把按钮路径弄回反直觉）。
- 修正图形层列表宽度：原先按「当前可见页签的视口宽度」钉宽度，切页签时会拿到上一个页签的值（784），列表缩窄并偏向一侧；现在只认组件页自己的视口，切页签后下一帧重算。
- 动作按钮从清单里提到组件页顶部工具条：「添加路径…」「添加组件…」「管理下载源」「更多」属于整页，不再读成图形层的子控件；两个清单只留标题、说明与列表。

## 2026-11-23 —— 组件页改页内分段（D114，已由上方修订撤回）

- 运行器与图形层不再上下堆叠，改为页内分段切换，首屏只出现一组清单。
- 分段选中态改用不透明度 + 字重（避开 Uno 运行时替换 Style 不刷新 Background 的问题）。
- 每段只留一个主操作，其余动作收进「更多」；行内「检查」降级为文字按钮。

本文件按日期记录实现层面的关键变更（对用户可见的行为变化为主）。版本号尚未发布，当前处于 WIP 阶段。

## 2026-11-23 —— 运行环境页面视觉收口（D113）

- 编辑入口统一为一个「修改」；绑定行加标签；chip 数字格式统一（运行器 6/6、图形层 6 个、前缀 1 个 · 体积）。
- 运行器行去掉 `bin 目录`；前缀卡去掉误导性的「占最大前缀」进度条，标题不再拼目录名。
- 「N 个方案使用」可点开方案名单；左列新增「＋ 新建方案」；添加路径 / 设为默认补 tooltip。

## 2026-11-23 —— 运行环境页签合并为「组件」，发布列表加缓存（D112）

- 运行器与图形层合并成「组件」页签（方案 / 组件 / 前缀），「添加组件」「管理下载源」只留这一处。
- 发布列表 10 分钟 TTL + ETag/If-None-Match，重复打开不再请求 GitHub；403/429 提示匿名 60 次/小时与 token 用法。
- 源管理弹窗补 `LUNARWARD_GITHUB_TOKEN` 说明；不引入新的 GitHub 客户端依赖。

## 2026-11-23 —— 运行环境页第三轮打磨（D111）

- 编辑器字段统一栅格（标签 110px + 控件 Stretch），前缀路径独占整行、按钮另起一行。
- 下载源支持新增/排序/编辑/删除/恢复默认，内置源删除用墓碑；表单校验名称、owner/repo、资产正则与镜像地址。
- 「添加组件」无启用源时给空态对话框并可一键打开源管理。
- 详情页动作统一「修改」，「已绑定 N 个游戏」点开绑定表；新增「保存并运行…」；启动预览横向滚动 + 复制。
- 已安装徽标补 tooltip；⋯ 菜单分隔线在深色主题下可见。

## 2026-11-23 —— 运行环境 UX 重做：添加组件、下载源管理、高级分组与启动预览（D110）

- 图形层入口改为「添加组件…」+「管理下载源」；组件源与镜像测速从「更多」菜单移出。
- 添加组件：打开即加载，版本改勾选行（版本/大小/已安装徽标），底部汇总「已选 N 项 · 总体积 · 可用空间」，行内进度与失败重试，按钮收敛为「暂停 / 安装所选 / 关闭」；错误文案不再暴露 `AssetPattern` 之类的内部名词。
- 组件源：新增启用开关、上下排序、镜像前缀校验、单行测速、源自检、全部测速、恢复默认（`管理下载源` 独立弹窗，保存按当前顺序重写 Rank）。
- 方案「高级」重排为 启动行为 / 包装器 / 环境变量 / 备注：环境变量改 KEY/VALUE 表格并校验（合法名、去重），包装器改 chips 并按 PATH 标红缺失项，删掉与「增强」重叠的字段。
- 方案详情新增「启动预览」卡片：与真实启动同源解析（命令行 + 环境变量覆盖），只读显示。
- 修复右侧详情/编辑器在窄窗口下不滚动：列 Grid 补显式星号行，内容不再被整块裁掉。
- 新增 39 个 `Lunarward_RuntimeEnv_*` 文案键（中性/简/英/繁 TW/繁 HK），繁中由 OpenCC 生成。

## 2026-11-23 —— 米游社工具箱页签残留与下载组件选择修复（D109）

- 米游社工具箱：切游戏时先把 14 个游戏页签全部收起，再按当前游戏显示；同时清空旧角色/旧内容并回到空白页，切回原神不再残留绝区零页签。
- 米游社工具箱：缓存页不再在 `OnUnloaded` 退订 XAML 事件，「战绩」「更新设备信息」第二次进入仍可用。
- 下载组件：`ListBox` 多选在 `ContentDialog`（Uno/Skia）里点不动，改为 CheckBox 行列表，勾选即入队并显示「已选 N 个版本」；`开始下载` 按勾选数启用。
- 下载组件：切换下载源清空旧列表并禁用开始，避免把 DXVK 归档按 wine 类型导入。
- 下载组件：空间预检改用真实挂载点（`DriveHelper.GetDriveAvailableSpace`）。
- 新增 `Lunarward_RuntimeEnv_SelectedVersions` 文案（中/英/繁四套对齐）。

## 2026-11-23 —— 运行环境页面 UI 审查修复（D108）

- 术语统一为「方案 / 运行器 / 图形层 / 前缀」，resx 里的「配置」「组件」同步改词；页面文案全部走 `Lunarward_RuntimeEnv_*`（新增约 110 键，中英双语）。
- 编辑器与增强面板改为「内容滚动 + 固定底栏」：保存/取消、命令预览不再随表单滚走。
- 颜色改主题 token（不再硬编码 `#C42B1C` 等）；体积统一两位小数；长路径按目录缩写并带 ToolTip 全文。
- 前缀卡标题改为「{方案} · {目录}」，占用条自适应宽度并显示「占最大前缀 N%」。
- ⋯ 菜单分组、删除项标红、调试 Shell 仅开发构建可见；编辑器加未保存修改确认；页签/⋯ 补自动化名；Ctrl+N / Ctrl+S / Delete 快捷键。
- `--subpage RuntimeEnvironmentSetting` 可直达运行环境页。
- 21 条审查项与逐条处置见 `docs/linux-port/29-runtime-env-ui-audit.md`。
- 文案本地化：简体/英文共 331 键，繁体（zh-TW / zh-HK）用 OpenCC 补齐 313 键；新增 `LocalizationKeyTests` 保证三套对齐；其它语言沿用资源回落。

## 2026-11-23 —— 运行环境审查修复批 D：页面体验与一致性（D107）

- 环境装配收敛到 `RuntimeEnvironment.Assemble`，启动 / Shell / RunOnce / 预览同源。
- 命令包装缺程序时跳过并提示，不再让启动失败。
- 读配置不再 fork `wine --version`；前缀体积统计移到后台。
- 删除前缀前检查共用方案；路径比较统一 `GetFullPath` 口径；保存时校验绝对路径。
- 编辑器预览带上增强项；改名即时刷新；保存/删除后刷新 chips。
- 详情页显示「上次成功启动」并可一键恢复；坏文件改名留证。
- 预检「去设置」定位到方案；`ContentDialog` 防重入；默认方案只在首次启动创建。
- 旧 `graphicsId` 迁移；名称清洗与 `SplitArgs` 反斜杠边界修正；第二实例启动异常可见。

## 2026-11-23 —— 运行环境审查修复批 C：组件层（D106）

- 组件下载的镜像前缀由下载器统一处理，页面与 CLI 行为一致（R12）。
- 续传遇到服务器忽略 Range（200）时自动重下，不再把全文接在半截文件后面（R14）。
- 分片下载按 `ChunkCount` 校验片数与长度，失败先清理再报错（R31）。
- 完整性标注区分「源端给了摘要」与「自己算的摘要」，导入时复核源端值（R34）。
- 解压拒绝绝对路径、符号链接校验补齐、`tar.xz` 增加体积上限与取消（R32）。
- 受管运行器扫描读 `manifest.Root`，`wine --version` 加缓存与超时 Kill（R13/R16）。
- 健康检查超时与取消都结束进程，语义分开（R15）。
- 图形层识别 `x32/`，按条目位数安装；跳过/删除以实际文件为准（R17/R37）。
- 组件导入先写清单再整体改名，避免中间态（R33）。

## 2026-11-23 —— 运行环境审查修复批 B：持久化（D105）

- 保存/删除运行环境失败时会明确报错，不再无条件弹「已保存」（R07）。
- 原子写改用唯一临时文件名 + 路径锁 + 目录 fsync，两个窗口同时保存同一条配置不再互相覆盖（R08）。
- 游戏绑定文件损坏时先尝试 `.bak` 恢复，仍失败则拒绝写入并提示，不会用残缺字典覆盖其余绑定（R09）。
- 恢复旧 `profiles/` 目录的迁移：逐份导入，全部成功才归档旧目录（R10）。
- 运行环境 Id 增加格式与路径越界校验，加载时以文件名为准纠正 Id（R11）。


- 配方 env（含 DXVK_HUD/MANGOHUD 等增强开关）真正下发；用户在配方里写 `WINEPREFIX` 不再把「加锁/装 DXVK 用的前缀」和进程实际前缀拆成两个（R01）。
- 配方启动参数改为追加，不再整体替换游戏自带参数（扫码登录回填、DX12、弹窗参数不再丢）（R02）。
- 新建前缀先建父目录、默认屏蔽 wine-mono 对话框、3 分钟超时强杀；创建失败不再静默回退（R03）。
- `WINEARCH` 真正传给 wineboot 并落进前缀侧写；预检读真实前缀架构，配置不一致时警告（R04）。
- 前缀锁改为按前缀分槽，启动失败即释放，占用时直接拒绝启动（R05）。
- 新默认配方不再指向 `~/.wine`，默认带 `WINEDLLOVERRIDES`（R06）。

## 2026-11-23 —— 运行环境审查修复批 A：启动链（D104）

## 2026-11-22 —— 背景图库缩略图不再弹出 VLC 窗口（L-166）

- 视频缩略图改为 `--vout=vmem` + 视频回调抽帧，libvlc 不再为每个抽帧自建顶层窗口。
- 同一视频的并发抽帧去重（图库页与 `CachedImage` 会同时请求），失败结果缓存 10 分钟。
- 缩略图统一落在 `<CacheDirectory>/bg-thumbs/`，成功后原子改名。

## 2026-11-22 —— 修复审计批 9：启动器（D103）

- 切游戏时启动器页不再重复初始化（B30）。
- 运行时配方/前缀/伴随程序按本次启动的游戏解析，deep link/CLI 启动正确（B31）。
- 配方启动参数改为追加，登录回填与 DX12 参数不再丢失（B32）。
- 自定义 Proton 路径不再触发越界异常（B33）。
- Linux 上按 `/proc` 命令行找游戏进程，`游戏运行中`状态可用（B34）。
- 第三方工具/CMD 启动时游玩时长与运行状态正常记录（B35）。
- 公告打开后红点立即熄灭（B61）；快速切游戏不再显示上一款内容（B62）。
- 游戏设置对话框长页面可以滚到底（B63）。

## 2026-11-22 —— 修复审计批 8：背景（D102）

- 图库视频/大图走缩略图通道，不再整文件读进内存（B21）。
- 换自定义壁纸后背景立即刷新，不再等切游戏（B22）。
- 视频帧异常时回到 UI 线程停止播放器，避免跨线程崩溃（B52）。
- 绘制帧时持有锁，避免用到已释放的位图（B53）。
- 选到解不开的图片会明确报错，不再落盘成黑壁纸（B54）。

## 2026-11-22 —— 修复审计批 7：抽卡与战绩（D101）

- 抽卡页跨游戏不再残留统计卡与同步菜单入口（B18/B19）。
- 切游戏后抽卡页重新加载 UID、卡池与统计（B48）。
- 战绩页切游戏后重新加载角色与设备信息（B51）。
- 自助查询页只显示当前游戏的查询列表（B20）。
- 满体力时间固定用冒号分隔，不受区域设置影响（B49）。
- 未登录空态不再盖住左侧工具箱（B50）。

## 2026-11-22 —— 修复审计批 6：热键设置（D100）

- 无 X11 时设置页也能看到已保存的热键，注册失败显示警告（B23/B24）。
- 方向键、Home/End/PageUp/PageDown、小键盘与标点可以注册，认不出的键直接报失败（B26）。
- 配置里的 "0"（用户删除）被正确识别，不再被默认值复活（B55）。
- 热键只在注册成功且值变化时落盘；未知 id 不再覆盖截图热键（B56/B75）。
- 设置页缓存后，「直达子页」请求仍能生效（B74）。

## 2026-11-22 —— 修复审计批 5：外壳（D99）

- `--hide` 在托盘不可用的会话里保持窗口可见，不再变成隐形进程（B12）。
- 退出 / 改数据目录 / 删除设置时 `UnregisterKey` 失败不再阻断备份与退出（B13）。
- 新增 `AppDomain.UnhandledException` 落盘，后台与启动早期崩溃也有堆栈（B44）。

## 2026-11-22 —— 修复审计批 4：更新窗口、剪贴板、深链与游玩时长（D98）

- 「忽略此版本」真正落盘，同一版本不再反复弹窗（B08）。
- 更新窗的发布页/完整包超链接可点，「需手动下载」会显示提示（B28/B59）。
- 复制文件写 text/uri-list，文件管理器能粘贴出文件；剪贴板写入不再卡 UI（B29/B60）。
- 深链支持 playtime/test，游玩时长可由外部按 pid 记录（B57）。
- 同前缀还有别的 wine 程序时，游玩时长与前缀锁不再被拖后（B58）。
- pid 复用时按进程启动时刻排除旧会话，避免时长虚高（B76）。

## 2026-11-22 —— 修复审计批 3：截图（D97）

- 「多选」开关生效：勾选框只在多选时显示，点条目多选时切换选择、否则打开查看器（B06/B07）。
- 批量转换不再「没选就转全部」，并移到线程池执行，转换时窗口不再卡死（B15/B45）。
- 「自动复制截图到剪贴板」在 Linux 生效（wl-copy / xclip）（B14）。
- 图库每次进入重读目录并监听文件变化，支持 jxr/webp/heic/avif/jxl（B16/B46）。
- 截图质量改为 0/1/2 三段索引并即时保存，格式同理（B17）。
- 「清除缩略图缓存」指向真实缓存目录，不再假报成功（B47）。

## 2026-11-22 —— 修复审计批 2：游戏设置与运行环境写入（D96）

- 游戏设置读写落到游戏真正使用的 wine 前缀（B10）；写失败不再静默「已应用」（B43）。
- 设置页切游戏先清空上一款游戏的可见/可编辑状态，并重读整套设置（B42）。
- 原神 HDR 亮度对话框恢复为可操作（三个亮度滑块 + 保存/恢复默认 + 回读现值）（B11/B70）。
- 启动原神前重放 HDR 开关到目标前缀；找不到前缀时明确报错（B41/B69）。
- 切换账号写目标前缀，失败弹提示（B40）。
- GPU 信息正确枚举 `/sys/class/drm` 的 card* 目录（B05）。

## 2026-11-22 —— 修复审计批 1：安装/更新/预下载（D95）

- 安装对话框恢复默认路径预填、压缩/解压体积、可用空间、语音包选择（B01/B02/B03/B04/B66/B67）。
- 可用空间按目标路径所在挂载点计算，不再一律取根分区（B09）。
- 安装目录识别支持「Genshin Impact」「Star Rail」这类真实目录名，搜索深度到三层（B36）。
- 停止/取消不再被记录成失败并弹错误通知（B37）。
- 卸载前结束占用进程，并保留嵌套在安装目录里的用户数据与截图目录（B38）。
- 预下载只在已记录安装路径时可用（B39）；启动扫描移入后台线程（B65）。

## 2026-11-21 —— 缓存页面按挂载次数重新初始化，修正切游戏后按钮与提示不刷新（D94）

用户报告：切回已安装的游戏后，右下角仍显示「安装游戏」，下面还有「已安装？定位游戏」提示，游玩时长也停在上一个游戏。两处实现错位叠加：

- 切游戏时 `MainView` 先导航再广播消息；`PageBase.OnNavigatedTo` 已把页面的 `CurrentGameId` 改成新值（`GameInfo : GameId`），消息处理里的 `if (CurrentGameId == message.GameId) return;` 于是直接返回，`CheckGameVersion()` 从不执行。
- 移植侧给 8 个页面加了 `NavigationCacheMode="Required"`，但 `PageBase` 在第一次 `Unloaded` 就退订 `Loaded`，缓存实例第二次挂载时 `OnLoaded` 不再执行 —— 离开过启动器页后本会话内该页不再刷新。

**改动**：

- `Frameworks/PageBase.cs`：`Loaded/Unloaded` 常驻不退订，每次挂载都跑 `OnLoaded()`；新增 `ContentGameId` 与虚方法 `OnGameChanged()`，导航参数换游戏时触发。
- `GameLauncherPage`：抽 `LoadGameContent()` 统一做整套游戏相关初始化（重置安装路径/提示/预下载/DX12/区服/安装任务/背景，刷新游玩时长与 banner，再跑 `InitializeGameFeature/CheckGameVersion/UpdateGameInstallTask/CheckCloudGame/InitializeGameServerAsync`），`OnLoaded`、`OnGameChanged()`、`CurrentGameChangedMessage` 三处共用；守卫改为对比 `ContentGameId`；`_dispatchTimer.Tick` 用 `-= +=` 接回。
- `PlayTimeButton`：新增 `RefreshPlayTime(GameBiz)`（`CurrentGameBiz` 是 OneTime `x:Bind`，缓存页切游戏后必须显式刷新）。
- `SettingPage`：`LanguageChangedMessage` 注册从 ctor 移到 `OnLoaded`（否则第一次离开设置页后语言切换永久失效）。
- `AGENTS.md`：新增硬规则 11「以上游为准」与「GUI 自动化避坑（`cu_*`）」7 条。

**实测**：Debug 构建 0 错误；264 条单测全绿；`lint-ui-thread.sh` 通过；真机两个场景（切到已安装游戏、离开再回来后切到未安装游戏）按钮/提示/时长/banner 均正确。

## 2026-11-21 —— 上游历史与 `main` 连通（D93）

核对上游发现 `main` 是一条独立历史（首提交 `5bf286d` 为整体导入），与上游 `df8ae07` 没有共同祖先，文档里写的 `git fetch upstream && git merge upstream/main` 实际无法三方合并。

**改动**：

- `git fetch --unshallow upstream` 补齐上游完整 1320 条历史（本机 DNS 解析不了 github.com，fetch 走 `gh-proxy.com` 镜像）；`.git` 56 MB → 67 MB，`.git/shallow` 移除。
- 合并提交 `542458e`（父 `bf775c5` + `df8ae07`，`-s ours`）：树零变化，只把上游历史接入祖先链。
- 之后 `git merge upstream/main` 以 `df8ae07` 为 merge base，是普通三方合并。

**实测**：合并前 `git diff df8ae07 main -- src/Starward` 0 文件（全树 859 新增 / 6 删除 / 17 修改，均为移植侧）；`git diff bf775c5 HEAD` 空；`merge-base` = `df8ae07`；`git fsck` 通过；`main` 1438 条提交，相对上游落后 0、领先 118。

**未推送**：`origin/main..HEAD` 1321 条，推上去会连带发布上游历史，单独决定。

## 2026-11-21 —— PKGBUILD 本地打包不再重复源码与构建（D92）

用户指出 `packaging/PKGBUILD`「会重复源码和构建」：一次本地打包原来会在 `packaging/` 下留下一份 235 MB 的源码克隆（`src/lunarward`）和一份 140 MB 的 publish 中间产物（`src/publish`），而工作区本来就有这两样东西。

**改动**：

- 本地模式改用 `source=()` + `prepare()` 软链工作区（`ln -sfn "$startdir/.." "$srcdir/lunarward"`）：源码零复制，`dotnet publish` 复用工作区已有的 `obj/` 做增量编译。
- `package()` 里 `cp -a` 改成 `mv`，省掉 140 MB 中间副本。
- `dotnet publish` 加 `-p:DebugType=none`（与 CI 一致，`Lunarward.pdb` 不进包）。
- `lint-pkgbuild.sh` C 节：行首匹配改为容忍缩进（原 `^source=` 对 if/else 里的 `source=` 完全失效，把空 source 误报成「tarball 源有真实哈希」），并识别 `source=()` 空源。

**实测**：`makepkg -f` 首次 34 s、第二次 **10 s**；打包后 `packaging/src` 只剩 4 KB；包内 391 个文件、无 `.pdb`；解包后 `Lunarward --version` 正常、`/usr/bin/lunarward` 的 sh 包装可用。

**行为变化**：本地模式打进包的现在是**工作区当前文件（含未提交改动）**，不再是「最后一个提交」——打包验证本来就该验证当前代码；AUR 两份包不受影响，仍从远端取。

## 2026-11-20 —— 文档整理：删掉 17 份完成使命的文档，数字对齐实现

用户要求「整理文档、删过时内容、更新最新情况」。这轮只动文档，源码零改动。

**删了 17 份**（`docs/linux-port/`）：`01`–`11`、`15`–`17`、`20`–`22`。它们是 2026-11-05 的规划（需求/架构/运行模型/移植矩阵/依赖准入/风险/路线图/未决问题/XAML 体检）、M1-0 与早期运行环境 UI 快照、以及三份「进行中」但实际早已停更的执行记录。内容已被 `12`（状态）、`13`（坑）、`19`（测试）、`23`（运行环境设计）、`24`（依赖）取代。原文可从 git 历史取回（`git log --diff-filter=D -- docs/linux-port/`）。

**数字对齐实现**（实测）：

| 项 | 文档原写 | 实际 |
| --- | --- | --- |
| 领先 `upstream/main` | 106 提交 | **115** |
| L1 单测 | 145 / 200 | **264** |
| 测试用例总数 | 200 | **230**（218 `T-*` + 12 `RG-*`） |
| 启动前预检 | 10 项 | **11 项**（多出「增强开关」） |
| 决策范围 | D0–D84、根 README 写 D0–D70 | **D0–D91** |

**修掉的矛盾 / 死链**

- `23-runtime-env-design.md` 头部还写着「状态：方向已确认，未开工」→ 改为「已实现并持续维护」（D73–D85 已全部落地）。
- `00-decisions.md` 引用了不存在的 `16-icon-mapping.md`、已删的 `17-runtime-environment-ui.md` → 头部加统一说明：正文里的 `01`–`17`/`20`–`22` 编号保留以便回溯，原文见 git。
- `12` / `19` / `23` 里指向已删文档的引用改成内联说明。
- `docs/user/runtime-environment.md` 按 D80/D85 后的实际界面重写（一级页 + 四页签 + 「前缀只是路径字段」+ 11 项预检）。

**AGENTS.md** §6 文档地图改成「活跃文档只有 9 份」+ 删除清单 + 回溯方法。

## 2026-11-19（三）—— `packaging/PKGBUILD` 默认打本地（D91）

用户要求「`packaging/PKGBUILD` 默认走本地」。D88 把它改成拉远端 `main`，结果想验证本地改动反而必须先 push —— 顺序反了。

**改动**

- `packaging/PKGBUILD` 的 source 默认 **`git+file://${startdir}/..#branch=main`**（克隆本地仓库 HEAD，不用 push）；`LUNARWARD_SOURCE=remote makepkg -f` 切回远端 `main`。
- AUR 两份保持远端（必须能被 AUR 构建机取到）。
- 注释和 `packaging/README.md` 都写明：git 源只含**已提交**内容，改完先 `git commit`。
- `scripts/test/lint-pkgbuild.sh` 的 C 节支持「source 引用变量」的写法（`git+` 在赋值行里也算 VCS 源）。

**实测**：默认 `--printsrcinfo` 得到 `git+file:///…/packaging/..#branch=main`，`LUNARWARD_SOURCE=remote` 得到远端地址；`makepkg --verifysource` 真克隆成功，克隆体 `114` 提交 / `4937610`，与本地一致；`lint-pkgbuild.sh` 通过。

## 2026-11-19（二）—— CI 上传可运行产物（D90）

用户要「CI 应该上传编译产物，可运行的」。此前 CI 只 build + 测试，不产出任何能跑的东西，想试就跑必须自己 clone + 装 SDK + build。

**改动**

- `.github/workflows/linux.yml` 末尾新增三步：`dotnet publish -c Release -r linux-x64 --self-contained true -p:DebugType=none` → 打成 `lunarward-linux-x64-<sha7>.tar.gz` → `upload-artifact`（名 `lunarward-linux-x64`，保留 7 天）。
- 产物里附 `RUN.txt`：怎么解压、怎么跑、还缺哪些系统库、这次构建的 commit 与版本。
- PR 不产出（`if: github.event_name != 'pull_request'`），只在 `push main` / `workflow_dispatch` 上传。

**为什么是 self-contained**：产物 226 MB、tar.gz 92 MB，解压即跑，不需要下载方装 `dotnet-runtime`。正式发行仍走 AUR / AppImage（framework-dependent，D88），两者的定位不同：CI 产物是「立刻能试」，AUR 是「给用户的正式件」。

**实测**：本地 publish 后 `./Lunarward --version` → `Lunarward 0.1.0.0`（exit 0）、`--help` 正常；framework-dependent 对照 147 MB / tar.gz 59 MB，同样可运行。

**CI 端到端**（run 44499063793）：artifact 81 MB → 解压 215 MB → `--version` / `--help` / `--profile-ensure` 全部正常，包内含 `libcoreclr.so`，确认自带运行时。

## 2026-11-19 —— AUR 编译不过：`global.json` 把 SDK 版本钉太高（D89）

用户报「global.json 导致 aur 编译不过，好像是 dotnet sdk 版本不匹配」。查下来确实是：

- `src/Lunarward/global.json` 的 `sdk.version` 是 **`10.0.302`**，根 `global.json` 是 `10.0.300`。`rollForward: latestMinor` **只向上滚、不向下兼容** —— 装 10.0.404 的机器能编译，装 10.0.1xx / 10.0.2xx 的打包环境直接报 `A compatible .NET SDK was not found. Requested SDK version: 10.0.302`。
- CI 一直是绿的，因为 `actions/setup-dotnet` 用 `dotnet-version: '10.0.x'` 装最新版，永远高于这个下限 —— 正好把它掩盖了。

**改动**

- 5 处 `global.json` 的 `sdk.version` 统一降到 **`10.0.100`**（.NET 10 首发 feature band），`rollForward` 保持 `latestMinor`；根与 `src/Lunarward` 两份就地加注释说明原因（`global.json` 是 JSONC，`.NET` 接受 `//`）。
- `scripts/test/lint-pkgbuild.sh` 新增 **D 节**：所有 `global.json` 的 `sdk.version` 最后一段必须是 `100`、写了 `sdk.version` 就必须有 `rollForward`、三份 PKGBUILD 的 `makedepends` 必须有 `dotnet-sdk`。
- 三份 PKGBUILD 的 `build()` 注释与 `packaging/README.md` 同步改说法（不再写「钉的是 SDK 10.0.300」）。

**验证**：`dotnet --version` 在仓库根 / `src/Lunarward` / `tests` 都解析到 10.0.404；Debug/Release 构建 0 错误；单测 264/0；`lint-pkgbuild.sh` 四节全 PASS。**待验收**：Arch 打包机上真实编译（本机只有 10.0.404，无法实测低版本 SDK）。

---

## 2026-11-18（三）—— Arch 打包的问题（D88）

用户问「arch打包是不是还是有问题」。核对三份 PKGBUILD，确实是，而且有三层：

**构建层**

- `packaging/PKGBUILD` 的 `source` 指向 `#branch=linux-port` —— 这条分支 11-13 就停用了，构建出来是五天前的代码；`url` 还写的是上游仓库。
- `packaging/aur/lunarward/PKGBUILD` 的 `source` 指向 `archive/v0.1.0.tar.gz`，而仓库 **0 个 tag** —— 必然 404。
- `-p:Version="${pkgver}"` 对 git 包的 `0.1.0.r123.gabc` 不合法（`AssemblyVersion` 不吃字母）。

**依赖层**

- 三份都缺 `libxcb`（`X11Window.cs` 的运行时 P/Invoke，`libx11` 不覆盖它）与 `xdg-utils`（`xdg-open`）。
- 两个 AUR 包缺 `webkitgtk-6.0` —— D76 登录窗打不开。
- 多余的：`gtk3`（源码 0 引用，GirCore 用的是 Gtk-4.0）、`zstd`（产物自带 `libzstd.so`）。

**根因层**

- `packaging/.gitignore` 写的是 `lunarward/`，无前导斜杠 → 匹配任意层级 → 把 `packaging/aur/lunarward/` 整份目录忽略了。`git ls-files packaging/` 里没有它：**这份包定义从来没进过仓库**，所以没人发现它一直缺依赖。
- 依赖清单在三个地方各写一遍，`d095922` 补 `webkitgtk-6.0` 时只改了其中一份。

改法：

- 三份 `depends` 统一为 `dotnet-runtime>=10.0`、`fontconfig`、`libx11`、`libxcb`、`xdg-utils`、`webkitgtk-6.0`，每项在注释里写明对应哪个 P/Invoke 或外部命令。
- `source` 分别改为 `#branch=main` / `#tag=v$pkgver`，`url` 改本仓库，补 `pkgver()`。
- `.gitignore` 改 `/lunarward/`，AUR 包定义得以纳入版本管理。
- 新增 `scripts/test/lint-pkgbuild.sh`：A 扫源码反查 depends 覆盖、B 三份必须一致、C source 与 `sha256sums` 自洽（git 源才允许 `SKIP`，tag 源要求 tag 存在）。

`packaging/README.md` 重写：三份包各自的定位、发版流程（先打 tag 再填 pkgver）、硬依赖表与「为什么不写 gtk3/zstd」。

## 2026-11-18（二）—— 运行器名字对不上的问题（D87）

用户反馈「运行环境那页操作逻辑不够好」，实机取证到两条硬证据：

- **有两对完全同名的运行器**：`spritz-wine-tkg-staging-wow64-11.19-1` 与 `spritz-wine-cachyos-wow64-10.0-11` 各出现两次（`~/.local/share/sleepy-launcher/runners/` 与 `~/.local/share/anime-game-launcher/runners/` 各带一份）。编辑器下拉里就是两行一模一样的字，选哪个全靠猜。
- **同一个文件两个名字**：`/opt/spritz-wine-tkg/bin/wine` 被命名成「系统 Wine」，而详情页按配置猜出来是 `wine`。

改法：

- 新增 `RunnerNaming`：PATH 里那个 wine 只有装在 `/usr`、`/bin`、`/sbin` 才叫「系统 Wine」，其余用目录名（`/opt/spritz-wine-tkg/bin/wine` → `spritz-wine-tkg`）；**只在真的重名时**才追加来源后缀（`x（anime-game-launcher）`），不重名的名字保持原样。
- 详情页的运行器名字优先用组件扫描结果里的 `DisplayName`（带版本，与下拉一字不差），找不到才回落配置里的旧名；组件列表扫完后就地更新，不重建整页。
- `--list-runners` 的名字列改用 `Name`——版本已经单独一列，`DisplayName` 会让版本号显示两遍。

验证：单测 **264 / 0**（新增 12 例覆盖命名与来源消歧）；`--list-runners` 实际输出 6 条、`/opt` 那条显示为 `spritz-wine-tkg`、两对重名分别带 `（sleepy-launcher）`/`（anime-game-launcher）`；截图确认详情页与编辑器下拉是同一个名字；`cli-smoke` 40/0/1、`cli-extra` 32/0、`--selftest-ui` 11/0、两个 lint 全 PASS。

## 2026-11-18 —— 丝滑度修复：查注册表不再 fork wine，提示不再每次重启重来（D86）

用户反馈「整体用起来很怪，不如 Windows 原版丝滑，每次打开都弹一次自动识别」。取证后按 A（卡）→ B（烦）→ C/D（一致性/日志）修了一版：

- **查注册表改读文件**：新增 `WineRegistryFiles` 直接解析 `<prefix>/user.reg` / `system.reg`，命中就不 fork；只有「文件给不出结论」时才回落 `wine reg query`。
  - 同一键实测：文件解析 **91–115 ms**，`wine reg query` 热 **1843 ms**、冷 **4128–5411 ms**。
  - 顺带修好两处：长 `REG_BINARY` 在 `reg query` 输出里被首行截断（文件版按折行拼全，实测 54 KB 值完整解出）；`reg query` 的中文键名乱码，文件是 UTF-8 正确解码。
- **三条提示改为「只提醒一次」**：自动识别游戏、Wayland 全局热键不可用、检测到系统代理。原来用进程级 `static bool`，等于每次启动都重来。新机制写 SQLite `Setting` 表的 `hint_<name>`。
- **失效绑定只警告一次**：绑定指向已删方案时仍会回落（行为不变），但不再每次启动刷一条 warning；修复入口在「运行环境 → 方案」页的提示卡。
- **清理**：删掉 `[dbg] scan start/done` 插桩；「识别到 N 个已安装游戏」改用 `KnownGames.Count`（原来取 `ScanAndApply()` 返回值，缓存命中时恒为 0）。

验证：单测 **252 / 0**（新增 11 例覆盖注册表文件解析的转义/折行/回落信号）；`cli-smoke` 40/0/1、`cli-extra` 32/0；`--selftest-ui` 11/0；`lint-ui-thread`、`lint-cli-help` 全 PASS；真实配置连续 4 次启动确认日志为「识别到 3 个」、无 `WineRegistry` 慢调用、`hint_auto-scan` 只写一次。

## 2026-11-17（一）——运行环境界面重排：方案为主体，增强并入详情（D85）

页面不再是「五个页签 + 一屏卡片」，改成以方案为中心的 master-detail：

- **方案页**：左列方案列表（健康点 + 名字 + 「可用 · 被 N 个游戏绑定」），右栏五段卡片（运行器 / 图形层 / 前缀 / 增强 / 最近启动）。删除、克隆、配置文本、调试 Shell 收进 `⋯`，头号动作只留「运行一次」。
- **取消「增强」页签**：它本来就是方案级启动选项，现在从方案详情的「配置」进入，不必先在下拉里挑方案（页签剩四个：方案 / 运行器 / 组件 / 前缀）。
- **chip 条**：`N 个方案 · 已绑定 N 个游戏 · 运行器 a/b · 图形组件 N · 前缀 N · 占用`，每个都能点着跳页签，替掉原来的死统计卡。
- **编辑器就地打开**：右栏在「详情 / 编辑器 / 增强」之间切换，左列与 chip 条常驻，返回后选中态不丢。
- **组件页收敛**：「下载组件」为主按钮，导入归档 / 导入图形 / 组件源 / 镜像测速 / 下载暂存 / 清理未使用进「更多」；加类别筛选。
- **运行器页**：按来源分组，新增「设为新方案默认」（`runtime-defaults.json`）。
- **前缀页**：占用条、所属方案名单、无主前缀标「未使用」；「N 个方案在用」改用 `EffectivePrefixPath` 比较。
- **修**：编辑器按路径匹配组件会静默落回第一项（把已选图形层改掉），改为四路比对（L-159）。

验证：`dotnet build` Debug/Release 0 错误；单测 241 / 0；`--selftest-ui` 11 / 0；`cli-smoke` 40/0/1、`cli-extra` 32/0；`lint-ui-thread`、`lint-cli-help` 全 PASS；`--page runtimeenv` 逐页截图确认。

## 2026-11-16（三）——运行环境可用性打磨：一致性提示、清理未使用、启动历史（D84）

页面结构（D80–D83）搭好之后，把「用起来还硌手」的地方一次补齐：

- **A 一致性**：方案卡片会比对「选的图形组件」与「前缀里实际装的」，不一致给一行说明 + 「同步到前缀」（只重装，不改方案）。
- **B 清理未使用**：组件页「清理未使用」只清本程序管理目录里没人引用的组件，外部目录（系统 / Steam / 自己的）不列不删。
- **C 共用提示**：一份方案被多个游戏绑定时明确写出「同时启动会抢锁失败」，并给「为某个游戏克隆一份」（前缀留空，各自新建）。
- **D 常用变量**：编辑器「高级」里加「插入常用变量」，8 条常用 `KEY=VALUE` 一键插入（按 key 去重）。
- **E 启动历史**：新增 `logs/launch-history.jsonl`，预检拦下 / 已启动 / 进程没起来三种结果都记（含耗时与明细），卡片显示最近 3 条。
- **F 键盘可达**：页签支持 `Left/Right/Home/End` 切换（自绘页签补回键盘导航）。
- **H 删方案**：删除对话框可勾选「同时删除前缀目录」，默认不勾、前缀不存在时禁用。
- **I 预设**：增强页加 HUD 预设、分辨率预设与 HDR / VRR 开关。

验证：`dotnet build` Debug/Release 0 错误；单测 **241 通过 / 0 失败**；`--selftest-ui` 11/0；`cli-smoke` 40/0/1、`cli-extra` 32/0；真机逐项截图确认。

## 2026-11-16（二）——图形层拆成两层：DXVK 与 VKD3D-Proton 可同时装（schema 4）

此前一份方案只能选一个图形组件，而 DXVK（d3d9/10/11 → Vulkan）与 VKD3D-Proton（d3d12 → Vulkan）**是两个互不替代的层**，落在前缀里的文件名也不重叠——玩原神要 d3d11、玩绝区零要 d3d12，本来就该两个一起装。按 D83：

- **模型拆字段**：`GraphicsId` → `DxvkId` + `Vkd3dId`，`SchemaVersion` 3 → 4。旧方案里的 `GraphicsId` 被忽略，打开后需要在「编辑」里重新选一次（**开发阶段不做数据迁移**：同时删掉 `Legacy*` 字段、`MigrateFromLegacy()`、`profiles/` → `env/` 的目录迁移、以及 5 个 `MigrateFromLegacy_*` 单测）。
- **前缀记录改成数组**：`<prefix>/.lunarward/graphics.json` 变成「按 kind 各一条」，换 DXVK 版本不会碰 VKD3D 的文件；旧单对象格式读取时兼容，写入即升级。
- **全链路跟随**：启动时两类一起装、预检对两类各查一遍、启动报告记两个 id、编辑器两个独立下拉、`EnsureFor`/克隆/组件删除的引用检查都按两个 id 处理、前缀行「安装到前缀」按所属方案把两类一起装。
- **编辑器里一行一类**：`DXVK`、`VKD3D-Proton` 各一个下拉，比塞进一个「都要」的下拉更贴近真实关系（互不替代）。

验证：`dotnet build Starward.slnx -c Debug` 0 错误；`--selftest-ui` 11/0；单测 **223 通过 / 0 失败**（224 − 5 + 4）；`--preflight hk4e_cn` 的「图形组件」项按两类分别报。

## 2026-11-16（一）——运行环境重做：一级页、五个页签、HUD / gamemode / gamescope

按 D80–D82 把「运行环境」从设置页里的一个子页提成一页真正能用的界面，并补上此前只能手写包装命令的增强开关。

- **入口提到 footer**（D80）：运行环境现在是 `NavigationView` 底部第一个图标（齿轮正上方），不再挂在「启动器设置」里。它跨游戏生效（一份方案可被多个游戏绑定），放在单个游戏的设置域里既难找也容易误解。`MainWindow.OpenRuntimeSettings()`、预检失败后的「去设置」、`--page runtimeenv` 三处统一走 `MainView.NavigateToRuntimeEnvironment()`；`MainView.NavigateTo` 的页面白名单扩到这两页（否则会被**静默**换回启动器页，L-152）。
- **页面分成五个页签**：概览 / 运行器 / 组件 / 前缀 / 增强。概览四张统计卡（方案数、运行器「可用/发现」、前缀数与合计体积、图形层数）+ 方案卡片（含「使用中的游戏」）+ **失效绑定提示卡**：绑定指向已删方案时列出「游戏 —— 原方案 id」，可一键「全部改绑到 X」或「清除这些绑定」。本机就有一例（`hk4e_cn`/`nap_cn` 指向已删除的 `c2d564e1`，一直在静默回落到第一份方案）。
- **页签自绘**（D82）：`Pivot` 的页签头在 Uno Skia 6.7 下收不到鼠标点击（单击/双击/长按都不切，方向键却能切；同窗口的普通按钮点击正常），改成 5 个 `Button` + 选中样式 + `Visibility` 互斥，`ShowTab(int)` 一处收口（L-153）。
- **增强开关**（D81，`RuntimeProfile.SchemaVersion` 2 → 3）：新增 `Hud`（关闭 / `DXVK_HUD` / MangoHud）、`HudOptions`、`GameMode`、`GameScope`、`GameScopeArgs`。新增 `Features/Runtime/Enhancements`（纯函数）：展开环境变量、拼包装顺序（`gamescope` → `gamemoderun` → 用户自己的 wrappers）、查 PATH、生成一句话描述。**gamescope 开着时不注入 MANGOHUD**（两层监控叠一起会重复帧数）。注入顺序：自动环境变量 → 增强 → 配方 `Env`，所以配方里手写 `DXVK_HUD` 仍会覆盖增强并在启动报告里记为覆盖项。
- **预检新增第 11 项「增强开关」**：开了 `gamescope`/`gamemoderun`/`mangohud` 但 PATH 里找不到 → Warning，可一键跳到「运行环境 → 增强」。
- **图形层编辑器可选 VKD3D-Proton**（D82）：此前只列 DXVK，VKD3D 只能手改 JSON；现在同一个下拉里两类都列，条目显示「名字 · 版本 · 类别」。
- **gamescope 参数体检**（D82）：提示 shell 引号/重定向、`--` 之后会被交给游戏、`-W/-H` 与 `-w/-h` 必须成对。只提示不阻断保存。
- **硬编码中文迁 `Lang`**：运行环境页面（XAML + 三个 code-behind partial）里 155 条不同的中文界面文案全部换成 `Lang.Lunarward_RuntimeEnv_*`，共 206 处；`Lang.resx` 补英文、`Lang.zh-CN.resx` 补中文、`Lang.Lunarward.cs` 补属性，各 171 条。剩下的两处是**故意不动的**：一条是 `ComponentStore` 返回的提示（源头在那边），一条是解压进度的日志模板（`{Entries}/{Bytes}` 占位）。健康检查的「失败」判定从文本匹配改成 `result.Ok`（原来靠 `Summary.Contains("失败")`，一改文案就失效）。
- **测试**：`EnhancementsTests` 24 项（HUD 展开、gamescope 避让、包装顺序、PATH 检查、`Build` 产出、配方覆盖、参数体检）；`UiSelfTest` 加第 11 条（footer 入口能打开运行环境页）。门禁：单测 **224 通过**、`--selftest-ui` 11 条、Debug 构建 0 错误。
- **文档**：新增 D80/D81/D82、L-152/L-153/L-154；`12` 号运行环境/预检/设置页三行改状态（预检 10 → 11 项）；`23` 号 §7.1 重写 + §13.10；`19` 现状行改 224 / 11；README 快照与阶段行。

## 2026-11-15（三）——依赖批次：补两个真缺口，两个「看着能换」的不换

按 `24` 的盘点结论逐条落地（D78 / D79）。四条有结论，两条落地。

- **Steam 自定义库不再是盲区**（ValveKeyValue 0.80.0.542）：新增 `Features/Runtime/SteamLibrary`，解析 `steamapps/libraryfolders.vdf`（旧格式 `"1" "/path"` 与新格式 `"1" { "path" "/path" }` 都认），把每个库的 `steamapps/compatibilitytools.d` 与 `steamapps/common`（GE-Proton 常放这里）一起纳入扫描；`ComponentStore.SteamCompatRoots()` 从硬编码 4 条默认路径改成走它。此前 Steam 装在自定义库的机器上**一个 Proton 都扫不到**。
- **预检第 10 项「游戏架构」**（PeNet 6.1.2）：新增 `Features/Runtime/PeArchitecture`（PE machine → `X86/X64/Arm64`），x64 exe + win32 前缀 → Failed（Wine 直接 `BAD EXE FORMAT`）、x86 exe + win64 前缀 → WoW64 Warning、ARM64 → Failed。架构项归 Component 层，可一键跳到运行环境设置。
- **SharpHook 不换**（spike 实测否决）：本机 KDE Wayland 下 `TaskPoolGlobalHook` enabled 却 12 秒 0 事件，`xdotool`（XTEST）与强制 X11 后端都收不到；对照自写 `XQueryKeymap` 能看到按键。结论：保留 `X11KeyState` 轮询；Wayland 全局热键的正解是 `org.freedesktop.portal.GlobalShortcuts`（本机已暴露），列为后续项。
- **Tmds.DBus → Tmds.DBus.Protocol 不迁**：921 行、4 个 `[DBusInterface]`（含 FD 与请求句柄），Protocol 是源生成器模型且托盘/通知/portal 三条用户可见链路无自动化覆盖；前置条件写进 D78（先有集成断言）。
- **CLI 帮助改为单一来源**（D79，替代 System.CommandLine 重写）：新增 `Helpers/CliSpec.cs`——79 个开关（命令 57 + 修饰 17 + 界面直跳 6，含 `-h`/`-v`）声明在表里，`--help` 由 `RenderHelp()` 按组渲染并按终端列宽（CJK 记 2 列）对齐；分发最前面 `TryRejectUnknown` 只校验第一 token，未知开关 exit 2 并给最接近的候选（Levenshtein ≤ 4）。`Program.Linux.cs` 里那段 50 行手写帮助整段删除；`System.CommandLine` 包已移除（扁平 help 会丢掉分组可读性）。
- **新增 lint-cli-help.sh**：卡四条不变量——源码里每个 `args[0] is "--x"` ⊆ 表；表里每条都出现在 `--help`；未知开关 exit 2；拼错一个字符能给出正确候选。`lint-ui-thread.sh` 继续管 guiFlags 白名单。
- **测试**：新增 `SteamLibraryTests`（8 组）、`PeArchitectureTests`（24 组，含自拼最小 PE32/PE32+）、`CliSpecTests`（14 组）；`cli-extra` 新增 `T-CL-48/48b/49/50`。门禁：单测 **200 通过**、`cli-smoke 40`、`cli-extra 32`、两个 lint 全绿、Debug/Release 均 0 错误。
- **文档**：新增 D78（依赖批次）与 D79（CLI 声明表）、L-151（四个坑）、`24` 号 P5 执行记录、`12` 号组件/预检行（预检 9 → 10 项）、`19` 现状行、README 快照与 §1.1、AGENTS 自查清单。

## 2026-11-15（二）——运行环境收尾：删除有边界、身份有守卫、首次使用自己建环境

`docs/linux-port/23` 里写着「已实现」但实际对不上的五处，连同 P1–P3 遗留项一次做掉（D77）。

- **删环境先列引用者**：`ProfileBinding.Usages(id)` → 确认框逐行写游戏名 + 「前缀目录不动」→ 删 `env/<id>.json` 后 `ClearFor(id)` 清绑定，toast 报清了几条。此前是直接 `File.Delete`，绑定残留会静默回落第一份环境（L-62）。
- **受管组件删除**：组件行新增「删除」，只在 `ComponentStore.ManagedRootOf` 非空时出现；`TryDeleteManaged` 把可删范围钉在 `components/<分类>/<目录>` 这一层，`components/wine` / `components/dxvk` 这类**分类目录先被判掉**（判错就是整个运行器库没了）；被环境引用时二次确认并列出方案名；系统 / Steam / 手动发现的组件只做引用，不给删除。
- **root 拒跑**：新增 `Helpers/PosixIdentity`（`geteuid` P/Invoke，失败退回 `Environment.UserName`），预检第一项「运行身份」直接拦；`PrefixManager.EnsurePrefix` / `CreateWithWineboot` 另加 `ThrowIfRoot` 兜底。
- **游戏目录自带图形 dll**：预检新增「游戏自带图形 dll」，查 `d3d11/dxgi/d3d12/d3d10core.dll`，Warning + 动作「打开游戏目录」（新增 `PreflightAction.OpenGameFolder`）；刻意不查 d3d9/vulkan-1，老游戏自带太普遍。
- **首次使用自动建环境**（23 号 §5 之前只有文档）：`ProfileBinding.EnsureFor(biz)` 四级——已绑定直用 / 有同名只补绑定 / 全库唯一且没人在用就**就地改名** / 否则复制一份（`PrefixPath` 留空，各用自己的前缀不抢锁）。真入口在 `GameLauncherService`，观测入口 `--profile-ensure <biz>`（L2 `T-CL-45..47`）。
- **首次选用自动自检**：编辑器保存时若该运行器 `Usable is null`，后台跑一次 `ComponentHealth.CheckAsync` 并 toast 结果（不挡保存）。
- **下载队列**：版本列表改多选，逐个 下载 → 导入 → 删归档，进度「队列 i/n」，单个失败不影响后续，取消=暂停（分片保留可续传）。
- **镜像测速 / 组件源编辑 / 暂存列表 / 前缀占用汇总**：新增四个入口——`ComponentSources.ProbeAllAsync`（HEAD 首字节耗时，`ProbeUrl` 走 `MirrorPrefix` 或 repo 主页）、逐源改镜像前缀并写 `components-sources.json`、扫 `components/.download/*.part.N` 按前缀分组显示可删、`EnumerateKnownPrefixes(computeSize: true)` 出总量与逐份体积。
- **文档修正**：§3.5 的 `<prefix>/lunarward.toml` 改成实际落地的 `.lunarward/prefix.json` + `graphics.json`（toml 从未实现）；§3.3 / §4.2 / §5 / §6 标为已实现；新增 §13.9 收尾表与 D77、L-150。
- 验证：Debug/Release 均 0 错误；单测 **145**（新增 `ComponentSources.ProbeUrl` 3 条）；`cli-smoke` 40/0/1 跳过、`cli-extra` 28/0；`lint-ui-thread` 通过。**真机验收待做**：root 场景、删组件、首次启动建环境。

## 2026-11-15（一）——文档校准：把「现状」对齐到 `main` @ `d095922`

纯文档整理，**无代码改动**（`git status --porcelain src/` 为空）。

- **`docs/linux-port/README.md`**：头部三行（最后更新 / 当前阶段 / 分支）与能力快照按现状重写——分支改 `main`（`linux-port` 自 11-13 停用）、相对 `upstream/main` 领先 106 提交落后 1 个、阶段改为「功能批已收口、剩验收」；快照补 `WebKitGTK 6.0` 登录（D76）、组件层/八字段环境（D73/D74）、portal 与图片格式（D75）、`ArchiveExtractor`（24 号 P1）；文档表里 `00` 补到 D76、`13` 补到 L-149、`19` 指向执行记录、`23` 标 §13 为实施记录、`24` 标 P1–P4 全 ✅。
- **新增 `README.md` §1.1「未完成 / 待验收」**：把散在 23 号 §13.5/§13.8、`19` 附录 L3、`22` §3 的欠账汇总成一张索引（自动自检时机、源编辑界面、下载队列、暂存列表、前缀汇总页；真实米游社登录；T-UI-33；P2-1/P2-2）。
- **`12-shims-and-gaps.md` 表头**：校准日期 → 2026-11-15，分支 → `main`，规模按 `find`/`grep` 实测改为 36 个 `*.Linux.cs`、7 个 `*.LinuxAuto.cs`、`Compat/` 2 项、53 条 `<Compile Remove>`（旧值 33/8/6/55 已过时）。
- **`19-test-plan.md`**：删掉「L1 单测 36 条；L2/L3/L1.5 尚未搭建」这条 11-12 的陈旧现状，改为实测：L1 142、L1.5 10、L2 `cli-smoke` 37 + `cli-extra` 32 + `chunk-smoke` 6 + lint 三规则；§5.8 标题 25 → 27 条（`T-CL-42/43/44` 早已登记，只是标题没跟着改）。
- **`23-runtime-env-design.md`**：§12 里「系统 `tar` 解压」改为 `ArchiveExtractor`；「尚未做：下载取消按钮与磁盘空间预检」改为已了结（§13.8 / 24 号 P1），只剩「自检时机 + 源编辑界面」。
- **`AGENTS.md`**：分支改 `main`；CLI 冒烟示例 34/25 → 37/32；§5 的 L2 入口补齐 `cli-extra` / `lint-ui-thread` / `perf`。

## 2026-11-14（九）——运行环境阶段 2/3：前缀只是路径、环境八字段、页面重做（D74）

- 「前缀模式」整个取消：`PrefixPath` 空 = 默认路径（首次启动创建），有值 = 用那个路径；`template` / `ephemeral` / `shared` 一并移除，旧配置自动迁移（旧目录改名 `profiles.migrated-<时间戳>`）。
- 环境配置存到 `~/.config/lunarward/env/<id>.json`，字段收敛为名称 / 运行器 / 图形层 / 前缀路径 / env / 启动参数 / 命令包装 / 备注。
- 启动链路统一走 `LaunchRecipeBuilder`：argv 与 env 只有一份生成逻辑，启动、编辑器预览、`--launch-plan` 三处共用；配方覆盖自动注入的变量会在界面标红并写进启动报告。
- 运行环境页：卡片加状态灯（可启动 / 待创建或被占用 / 不可用）、前缀占用大小、上次启动；组件区拆「受管 / 外部」；新增「配置文本（只读）」与「启动预览」。
- `--launch-plan <exe> [--profile <id|名字>]`：打印配方、运行器、wine、前缀、完整命令行与每个 env 的来源。
- 测试：单测 104 → 119（5 条迁移用例），`cli-extra` 19 → 22，覆盖率行 88.50% / 分支 71.02%。

## 2026-11-14（七）——运行环境阶段 1：组件层（D73）

- 新增受管组件层：`manifest.json`（布局/来源/digest）+ 布局归一化（`bin` / `files-bin` / `bare-exe`），导入后由 `ComponentStore` 直接读 manifest，不再靠猜目录结构。
- 组件下载：HEAD 探 `Accept-Ranges` → 8 MiB 分片 × 4 连接并行（`.part.<n>` 可续传）→ 429/403 连续两次自动降级单流 → 合并后校验 SHA-256 → 原子改名。实测 DXVK 17 MB / 4 连接 / digest 一致。
- 组件导入：系统 `tar` 解压（先自动识别，失败再 `--zstd`），只有一层同名目录时摊平，非运行器/非图形层的归档直接拒绝并说明原因。
- 组件健康：一句话结论 + 等宽可复制的 stdout/stderr 原文 + 退出码，不做发行版包名映射（用户明确要求）。
- CLI：`--component-scan` / `--component-health` / `--component-import` / `--component-download`。
- 顺带修掉两个既有问题：图形层扫描不解析符号链接导致同一组件列两遍（`~/.local/share/Lunarward` 是链接）；`--component-health` 名字匹配区分大小写。
- L1 新增 23 条（分片边界、镜像前缀、三种布局、manifest 往返、命名规则），单测 62 → 85；L2 新增 `T-CL-36/37/38/39`（源清单 / 导入 / 健康 / 缺参数），cli-extra 16 → 19。

## 2026-11-14（六）——数据库备份 CLI、启动卡顿进基线

- 新增 `--database-backup <输出.zip>`：Vacuum + 单文件归档，归档里只有一个 `LunarwardDatabase_<时间>.db`，临时目录用完即删（`DatabaseService.BackupToArchive`）。这条同时补上 RG-07 的自动化入口。
- `perf.sh` 新增 `startup_stall_ms`（看门狗测冷启动卡顿，绝对上限 1500 ms），并修掉「基线格式漂移导致比较恒为真」——`perf-baseline.json` 早先是扁平结构，比较分支只读 `envs[host]`，取不到就「只记录不判定」，这条 Perf smoke 从建立起就没可能失败（L-135）；没有 `/usr/bin/time` 时不再让 `awk` 报错，没有 xvfb 但有 `DISPLAY` 时直接跑真实显示。
- L2 新增 `T-CL-33/34/35`（备份单文件 / 连续备份不套娃 / 缺参数返回 1），cli-extra 13 → 16 条。
- 自检第 9 条改为「IsTextTrimmedChanged 不抛异常」，不再把 xvfb 下不触发判成失败（L-134）。

## 2026-11-14（五）——把真实缺陷钉成回归用例

- 新增 **lint 层**：`scripts/test/lint-ui-thread.sh`（3 条规则，<1 s，进 CI）。A：`*.xaml.cs` 不许 `WaitForExit` / `.GetAwaiter().GetResult()` / `.Wait()`（D72 那 4 秒的根源）；B：`*.Linux.cs` 不许留单行空事件处理器（L-132 那两处空 stub），故意不接管的写 `// LINT-ALLOW: 原因`；C：`DevSwitches` 的每个参数必须在 `Program.Linux.cs` 的 `guiFlags` 里（`--page` 曾被守卫拦掉）。
- 修正 3 处既有违规：`guiFlags` 补上 `--subpage/--game/--scroll/--viewer/--profile-editor/--window-action`，10 个原本是 `{ }` 的事件处理器按「为什么不管」写清注释。
- L1.5 新增第 10 条断言「Pivot 换源按约定顺序不抛异常」（RG-01）；顺带用最小复现确认 Uno 在容器未重建时抛的是 **NRE**（`Pivot.cs:301`），与真机上的越界异常同源，记 L-133。
- L1 新增 `BuildRegFileBytes` 的 BOM 回归（RG-06），`WineRegistry` 改用它，单测 61 → 62。
- `19-test-plan.md` 新增 §12「由真实缺陷反推的回归用例」：12 条 RG 用例，标明断言/层/自动化状态（9 条已自动化，3 条待补）。
- 修 CI 误红：`IsTextTrimmedChanged` 自检在真机能触发、在 CI 的 xvfb 不触发，把一个功能可用的提交判成失败。断言改为只钉「挂事件 + 强制布局 + 改宽度不抛异常」，触发与否写进 detail（**L-134**：自检不要依赖「可选事件是否触发」，那类只能进 L3 人工项）。

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


## 2026-11-14（八）— 组件层覆盖率补齐

- 修：`Coverage (L1)` 从 74.42% 回到 **87.44% 行 / 66.16% 分支**（34 个类），CI 恢复绿。
- 加：`ComponentDownloadMath`（分片算术 / `.part.<n>` 命名 / 镜像前缀拼接）从下载器抽出，测试工程改为链接它与 `ComponentSources`，下载编排不再进覆盖率口径（靠 `--component-download` 手工实测 + `cli-extra` 兜）。
- 加：`ComponentSources` 单测 11 条（默认源排序、同 id 覆盖、新 id 追加、损坏文件回落、归档判定、zstd 偏好、版本抠取）；单测总数 85 → 104。
- 顺带：`ComponentDownloader.cs` 补回被脚本误删的 `ProbeAsync` 与 `RangeRejectedException`（L-137）。

## 2026-11-14（十四）——登录窗换成「本程序 + WebKitGTK 6.0」

- **登录不再依赖 python3**（D76）：新增 `--webkit-login --url <登录页> --output <cookies.json> [--timeout 秒]`（`Features/GameRecord/WebKitLoginHost.cs`，GirCore 0.8.1 + GTK4 + WebKitGTK 6.0），`WebLoginService.LoginAsync` 用 `Environment.ProcessPath` 拉起自己；**没有 `libwebkitgtk-6.0` 时自动退回原 python3 助手**，输出格式不变。
- **cookie 取法**：`CookieManager.SetPersistentStorage` + 每 2 秒读 WebKit 的 `moz_cookies`（GirCore 只生成了 `*Finish`，没有异步 cookie API）；命中登录标记即导出关窗，关窗与超时也导出。
- **撤回一条旧结论**：D32/D75 曾以「本机没有 `libwebkitgtk-6.0`」判定登录窗不改 GirCore——`extra/webkitgtk-6.0` 一直在仓库里，是论据本身错了（D46）。A/B 两个 spike 都跑通，选 A（独立进程）以避免 Uno 自带 GTK3 与 GTK4 同进程并存、以及 GTK 标题栏跟系统配色走的观感问题。
- 实测：本地起带 `Set-Cookie: ltuid_v2 / cookie_token_v2` 的 http 服务，开窗后 2 秒内导出 `{"Cookies":{"ltuid_v2":"123456789","cookie_token_v2":"tok_abc"},"Time":"2026-11-14T21:16:50"}`，退出码 0。**真实米游社登录待验收**。
- **打包**：`packaging/PKGBUILD` 把 `webkitgtk-6.0` 并入 `depends`（pkgrel 1 → 2），`webkit2gtk-4.1` / `python-gobject` 降为 `optdepends` 并注明「仅在没有 webkitgtk-6.0 时需要」。
- 文档：新增 D76、L-149；24 号「登录窗」一行由「不改（有据）」改为已落地并写清撤回原因。

## 2026-11-14（十三）——外部能力走 portal、图片格式补齐、弹层实底

- **打开链接 / 选文件 / 桌面通知改走 xdg-desktop-portal**（D75）：`OpenUrl` 先发 `org.freedesktop.portal.OpenURI`，`FileDialogHelper` 先发 `FileChooser`（filters / 多选 / 目录 / 保存建议名），`DesktopNotification` 直接 D-Bus `org.freedesktop.Notifications`；外部命令（`xdg-open` / `kdialog` / `zenity` / `notify-send`）全部降级为兜底。
- **截图 portal 兜底**：`spectacle` / `grim` / `gnome-screenshot` 都不在时走 `org.freedesktop.portal.Screenshot`，出图后搬进截图目录。
- **AVIF / JXL / HEIC 支持**：新增 `Helpers/MagickCodec.cs`（Magick.NET）与 `Helpers/ImageDecode.cs`（Skia 优先，AVIF/JXL 走 Magick）；壁纸、缩略图、取色、截图转换、`GetImagePixelSizeAsync` 全部统一到这个入口。新增 `--image-info <图片>` 自检。
- **弹层与对话框改成实底**（L-148）：`ContentDialog` / `FlyoutPresenter` / `MenuFlyoutPresenter` / 游戏设置对话框用新的 `LunarwardSurfaceBrush`（Dark `#FF202020` / Light `#FFF3F3F3`），不再让壁纸文字透上来。
- **Esc 只退一层**（L-147）：选择层或下拉浮层打开时 Esc 先关它们，都关掉才把窗口隐藏到托盘。
- **顺手项**：`xdotool` 唤醒窗口改用进程内 xcb（`X11Window.Activate`，EWMH `_NET_ACTIVE_WINDOW`），`xdotool` 仅兜底；`ProcessQuery` 不再 fork `fuser`/`lsof`，改为扫 `/proc/*/fd`。
- 测试：`cli-extra` 24 → 25（新增 `T-CL-44`）；单测 142 不变；lint 三绿。文档：24 号执行记录、D75、L-145～L-148。

## 2026-11-14（十二）——解压换库 + 组件下载/导入入口（24 号 P1）

- 解压不再 fork `tar`/`xz`：新增 `ArchiveExtractor`（BCL `TarReader` + `GZipStream` + `ZstdSharp` + `SharpCompress` 处理 `.tar.xz` + `ZipFile`），支持 `.tar` / `.tar.gz` / `.tgz` / `.tar.zst` / `.tar.xz` / `.zip`，可取消、有进度、解压前拦路径逃逸（L-144）。
- 运行环境页新增「下载组件…」：选源 → 列版本 → 下载（显示已下/总量/连接数/速度，**取消 = 暂停**，分片保留可续传）→ 自动解压导入；下载前做磁盘空间预检。
- 新增「导入归档…」：选本地压缩包（`kdialog`），选「运行器 / 图形层」后导入，不新增概念。
- GitHub API 匿名限流（403）不再伪装成「源里没有归档」：改为抛 `ComponentSourceException`，界面与 `--component-list` 都显示「限流 + 设 `LUNARWARD_GITHUB_TOKEN` 或配镜像前缀 + 原文」（L-143）；顺带支持 `GITHUB_TOKEN` / `GH_TOKEN`。
- 新增 CLI `--component-list <源 id>`（名称 / MB / 版本）。
- 测试：单测 142、`cli-extra` 24（新增 T-CL-42/43）。

## 2026-11-14（十一）——组件健康进页面

- 运行环境页的每一行组件（受管/外部运行器、图形层）右侧加「检查」按钮：后台跑 `ComponentHealth`，弹「健康检查 · <名字>」对话框，给一句概括 + 退出码/耗时/路径 + 等宽只读可复制的原文，按钮「复制原文 / 关闭」。
- 不做发行版包名映射（m21977 口径），只呈现原文，避免映射丢信息。
- 实测系统 Wine：`可用 (wine-11.19-1-g78e32ba1d3d (Staging))`、`退出码 0 · 耗时 4 ms`。
## 2026-11-14（十）——运行环境页真机复检：配置文本渲染、前缀状态文案

- 「配置文本（只读）」原来用运行时 `TextBox` 显示 JSON，Skia 后端只画出第一行（实测只剩一个 `{`，而「复制」拿到的是完整内容）→ 改为 `ScrollViewer` + 可选中 `TextBlock`，14 行 JSON 完整可见（L-141）。
- 前缀卡片里的图形层状态由「未安装」改为「图形层：未安装」，避免和前缀本身的状态混淆。
- 真机复检其余部分：方案卡片（状态灯 / 前缀占用 370.7 MB / 上次启动 / 按钮组）、组件区「受管（1 组）/ 外部（2 组）」、编辑器「启动预览」`/opt/spritz-wine-tkg/bin/wine '<游戏>.exe'`、高级折叠与保存/取消 均正常。
- 记录 L-142：截图物理像素与逻辑坐标的换算、以及滚动优先用键盘而不是滚轮。
## 2026-11-13（十一）

- 修掉每次切换游戏必现的 `ArgumentOutOfRangeException`（`Pivot.SynchronizeSelectedItem` 拿旧索引索引未重建的 header 容器，L-126/D71）：赋值 `PostGroups` 前把 `Pivot_Posts.SelectedIndex` 置 `-1`，赋值后再设回 0。真机切 4 次游戏，异常 0 次（修复前每次必现），资讯区不再停留在上一个游戏的状态。

## 2026-11-13（十）

- 视频壁纸看门狗从 2 秒放宽到 5 秒。VP9 1080p 首帧正常需要 2–3 秒（VLC 缓冲 ~1.0 s + 解码 + 8 MB 帧拷贝），旧阈值把正常起播也报成 `video started but no frame`，导致 F-21 被误判为回归；真机复测三次帧差 39.70%–48.13%，视频一直正常。
- 教训 L-125：超时阈值按「正常路径最慢值 × 1.5」定。

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

- 修：`GameSettingPage` 内容 Grid 在异步初始化后显式 `InvalidateMeasure()`，解决「运行配置」与 HDR 分区重叠（D67 / L-173 / `22` P0-2）。
- 更正记录：`22` 的 P0-2 原判断「右列横向溢出」经像素测量否掉；P1-2 澄清为视口边缘的正常半行。


## 2026-11-12——切换页面不再卡

- 页面加 `NavigationCacheMode`：启动器 / 游戏设置 / 截图 / 抽卡 / 战绩 / 自助查询 / 设置 再进都不再重建（实测 1729 ms → 24–46 ms）。
- 游戏设置页的注册表读取（分辨率 / 帧率 / 语音语言）挪到后台线程：首次进入 1729 ms → 216 ms。

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

## 2026-11-12（四）

- 修：切换游戏后左下角公告/资讯仍是上一个游戏的内容（新增 `CurrentGameChangedMessage`、页面与 banner 属性 setter 重载、Pivot 先置空再赋值）。
- 修：从其它页切回「启动器」卡几秒（背景层加同游戏守卫、先用本地图秒出画面、建议背景超时 1.5 s）。
- 开发入口：`LUNARWARD_DEV_SWITCH_GAME=<biz>`（DEBUG）按真实选服路径切游戏，便于复现。

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

## 2026-11-11

- 图库与游戏选择层改为按可用宽度均分（D56），去掉无效的横向滚动；
- 组件扫描改为特征探测 + 可配扫描根，覆盖 `/opt`、Steam `compatibilitytools.d`、Flatpak 与第三方启动器；新增 `--list-components`；
- 游戏扫描支持配置根与 24 小时增量缓存；
- `~/.local/share/Lunarward` 合并为指向 `lunarward` 的符号链接，`WelcomeWindow` 默认值改小写；
- 背景图库的视频条目改用 libVLC 抽首帧做缩略图（不再黑块）。

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
