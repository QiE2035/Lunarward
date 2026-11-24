# 28 · 「运行环境」模块审查报告

> 日期：2026-11-23　范围：`src/Lunarward/Features/Runtime/`（25 文件）、`Features/Setting/RuntimeEnvironmentSetting.*`（4 个 partial，4040 行）、`Features/GameLauncher/LaunchPreflight.cs`、`GameLauncherService(.Linux).cs` 的启动链。
> 方法：本人读码 + 4 组只读子代理（页面 / 启动 / 存储 / 组件），结论逐条回到源码核对。
> 状态：2026-11-23 用户 m02913「全处理了」→ 42 条全部修完（批 A D104 / 批 B D105 / 批 C D106 / 批 D D107），进度见各条 🛠 标记。
> 第二轮：2026-11-24 用户 m06812 要求「逐文件再仔细审查一遍，包括逻辑层、UI」，新增 R43–R130（**全部未修**，等拍板），见 §6；编号与第一轮连续，互不覆盖。
> 设计依据：`23-runtime-env-design.md`（605 行）、D73–D85 / D80 / D82 / D95–D103。

## 0 · 结论摘要

| 严重度 | 条数 | 其中已亲自核对 |
| --- | --- | --- |
| Critical（功能静默失效 / 可丢数据） | 8 | 8 |
| High（错误行为、可复现故障） | 9 | 6 |
| Medium（体验与一致性、边界） | 13 | 7 |
| Low / 文档漂移 | 6 | 4 |

最需要先修的三件事：**①配方环境变量（含 HUD）从未下发，且 `profile.Env` 还能覆盖真实 `WINEPREFIX`；②`LaunchRecipe` 仍整体替换游戏参数，登录票据被吞；③前缀创建不建父目录 + 失败静默回退 `~/.wine`**——这三条叠加，会让「选了运行环境」实际跑在另一个前缀、用另一套参数，用户完全看不出来。

---

## 1 · Critical

### R01　（🛠 已修复，D104 批 A）配方 env / 增强开关从未下发，且 `profile.Env` 覆盖真实前缀 ✔已核对
- 位置：`Features/GameLauncher/GameLauncherService.Linux.cs:307-334`；`Features/Runtime/LaunchRecipe.cs:171-200`。
- 证据：`info.Environment["WINEPREFIX"] = prefix;`（:307）之后，:324-330 把 `profile.Env` 逐条写回 `info.Environment`，**可以覆盖 WINEPREFIX**；而 `LaunchRecipeBuilder.Build` 产出的 `recipe.Environment`（含 Proton 变量、Enhancements 的 `DXVK_HUD` / `MANGOHUD` / `MANGOHUD_CONFIG`）直到 :335 只用于取 `Argv`，**没有任何一处写进进程环境**。:331-334 只把 `recipe.Overrides` 打成一行 `LogWarning`。
- 影响：HUD / MANGOHUD 开关全静默失效（界面和 `--launch-plan` 却显示这些变量）；配方 env 里写 `WINEPREFIX=/x` 时，加锁、装 DXVK、预检、启动报告都用 A，wine 实际跑在 B。
- 建议：启动侧改为整体应用 `recipe.Environment`（它已合并继承环境 + Proton + 增强 + 配方覆盖），删除单独的 `profile.Env` 回灌；`LaunchReport` 增加「实际前缀」与「env 覆盖」字段。

### R02　（🛠 已修复，D104 批 A）`LaunchRecipe` 仍整体替换游戏参数，登录票据被吞 ✔已核对
- 位置：`Features/Runtime/LaunchRecipe.cs:201`；调用方 `GameLauncherService.Linux.cs:321`。
- 证据：调用方已拼好 `effectiveArgs = profile.Args + arg`（:321，B32/D103 修的是这里），但 `Build` 里 `string.IsNullOrWhiteSpace(profile?.Args) ? gameArgs : profile!.Args` 又用 `profile.Args` **整体替换**了传入值 → `login_auth_ticket=…`、`-popupwindow`、`-use-d3d12` 全部丢失。
- 影响：只要方案里填了「启动参数」，扫码登录回填与 DX12 开关就失效，且无提示。26 号文档 B32 标了 🛠，实际只修了一半。
- 建议：`Build` 改为纯追加（`profile.Args + gameArgs`），删三元；补单测断言「配方参数与游戏参数同时出现在 Argv」。

### R03　（🛠 已修复，D104 批 A）新建前缀不建父目录，失败静默回退 `~/.wine` ✔已核对
- 位置：`Features/Runtime/PrefixManager.cs:291-300`、`:378-397`；`GameLauncherService.Linux.cs:252-258`。
- 证据：`EnsurePrefix` 只判断路径不存在就 `CreateWithWineboot`，后者只设 `WINEPREFIX`/`WINEDEBUG` 后起 `wineboot -u`；`PrefixManager` 里只有 `ClonePrefix`（:354）和侧写（:439）建过目录。`PrefixRoot` 不存在时 wine 报 `chdir … 没有那个文件或目录`，`EnsurePrefix` 抛 IOException，调用方只 `LogWarning` 后 `ResolvePrefix` 回退到 `~/.wine`/环境变量前缀。
- 影响：干净系统上首个托管前缀永远建不出来，游戏跑进 `~/.wine`；预检和报告都显示「用了你配的环境」，实际不是。
- 建议：`CreateWithWineboot` 开头 `Directory.CreateDirectory(Path.GetDirectoryName(path)!)`；创建失败不要在启动链里降级，转成预检失败 + 明确动作。

### R04　（🛠 已修复，D104 批 A）`WINEARCH` 全链路从未传给 wineboot，`PrefixArch` 是纯配置字段 ✔已核对
- 位置：`PrefixManager.cs:378-397`、`PeArchitecture.cs`、`LaunchPreflight.cs:318-330`。
- 证据：全仓 `WINEARCH` 只出现在注释里；`CreateWithWineboot` 只设两个环境变量；预检拿 `profile.PrefixArch`（默认 `win64`）与 exe 位宽比较，**从不读真实前缀的架构**。
- 影响：32 位游戏选 win32 也还是 win64 前缀；64 位 exe 配上误填 win32 的配方会被预检直接拦死，而改回 win64 只是改配置、前缀没变。
- 建议：创建时写 `WINEARCH=NormalizePrefixArch(...)` 并落进 `prefix.json`；预检优先读真实架构，读不到降级为 Warning。

### R05　（🛠 已修复，D104 批 A）前缀锁：单槽覆盖 + 启动异常时泄漏 + 返回值被丢弃 ✔已核对
- 位置：`GameLauncherService.Linux.cs:103-125`、`:252-258`、`:181`；`GameLauncherService.cs:473`。
- 证据：`_prefixLockStream` 是 `static FileStream?` 单槽，`ReleasePrefixLock` 唯一调用点是会话结束回调的 finally（:181）；`Process.Start` 抛 `Win32Exception`（缺 32 位 loader、ENOENT）时锁不释放；`TryLockPrefix(prefix);`（:252）返回值无人处理；降级前缀不再取锁。
- 影响：启动失败一次后同前缀游戏再也起不来（只能重启启动器）；两个不同前缀的游戏先后启动时，旧锁被覆盖、A 的退出回调会 Dispose B 的流。
- 建议：改成 `Dictionary<string, FileStream>`；拿锁放在「确定要启动」的最后一步并 try/finally；false 时按「前缀被占用」失败，而不是静默继续。

### R06　（🛠 已修复，D104 批 A）默认配方把 `PrefixPath` 指向 `~/.wine`，污染全局前缀 ✔已核对
- 位置：`RuntimeProfileStore.cs:165-190`。
- 证据：`LUNARWARD_WINEPREFIX` → 否则 `~/.wine`（存在才用），随后 `PrefixPath = prefix`；默认 `Env` 为空（设计要求可见的 `WINEDLLOVERRIDES=mscoree,mshtml=`）。
- 影响：首次使用就把 DXVK 的 d3d11/dxgi 覆盖进 `~/.wine/drive_c/windows/system32`，并把侧写写进 `~/.wine/.lunarward/`——其它 wine 应用被污染；首次启动仍弹 wine-mono/gecko。
- 建议：默认 `PrefixPath` 留空（空 = `PrefixRoot/<名字>`，D74 第 1 条）；`Env` 带默认 `WINEDLLOVERRIDES`。

### R07　（🛠 已修复，D105 批 B）配置保存失败仍提示「已保存」 ✔已核对
- 位置：`RuntimeProfileStore.cs:124-141`；调用方 `RuntimeEnvironmentSetting.xaml.cs:1336-1338`、`Enhancements.cs:291`；`ProfileBinding.cs:215-216`。
- 证据：`Save` 是 `void`，catch 只 `LogWarning`；调用方无条件 `Success(Lang.Lunarward_RuntimeEnv_Saved)`；`EnsureFor` 保存失败后仍写绑定 → 每次启动新建一份悬挂配方。
- 建议：`Save` 返回 bool 或抛；UI 按结果提示；`EnsureFor` 失败不写绑定。

### R08　（🛠 已修复，D105 批 B）JSON 原子写用固定 `.tmp` 名且无锁，GUI/CLI 并发会发布半截文件 ✔已核对
- 位置：`Helpers/JsonFile.cs:20-45`；并发入口 `App.xaml.cs:70`（CLI 分派在单实例锁之前）。
- 证据：`string temp = path + ".tmp";` + `FileShare.None` + `File.Move(temp, path, overwrite)`；无进程内锁，`Move` 后不 fsync 父目录。GUI 开着时 `--profile-ensure` / `--launch` 会同时写 `env/<id>.json.tmp`、`profile-bindings.json.tmp`：A 的 Move 可能把 B 正在写的同址 tmp 改名成目标文件，B 再 Move 直接 `FileNotFoundException`（被 `RuntimeProfileStore.cs:137-140` 吞掉），期间目标文件是半截 JSON，下次 `LoadAll` 会把它改名 `.corrupt-*`。
- 建议：临时名带 Guid、进程内锁保护「备份 + 改名」、Move 后 fsync 父目录；失败必须让调用方知道。

---

## 2 · High

### R09　（🛠 已修复，D105 批 B）绑定文件读失败一次，下一次 `Set` 就抹掉全部绑定 ✔已核对
- 位置：`ProfileBinding.cs:51-84`。
- 证据：`Load()` catch 后返回空字典；`Set` 直接用该字典写回。半截文件（R08）、权限、磁盘满都会触发。
- 建议：fail-closed——区分「文件不存在」与「存在但读不了」，后者抛错并保留原文件；`Set` 前把旧文件复制成 `.bak`。

### R10　（🛠 已修复，D105 批 B）旧的 `profiles/` → `env/` 迁移代码整段被删 ✔已核对（git 证据）
- 位置：`RuntimeProfileStore.cs:61-102`（现只扫 `EnvironmentsFolder`）；`git log -S MigrateFromLegacyFolder`。
- 证据：`2b79261` 里有 `MigrateFromLegacyFolder`（113-153，末尾 `Directory.Move` 到 `profiles.migrated-*`），`3d2e9ba` 起消失；23 号设计 §8 与 D58 都承诺自动迁移。
- 影响：老用户升级后 `env/` 为空 → `CreateDefault()` 生成新 id 的默认方案，`gameBiz→旧 id` 绑定全部悬挂，每局静默用新方案（可能不同运行器/前缀）启动。
- 建议：恢复迁移（逐份解析成功再改名旧目录，失败文件留在原目录），或至少在「旧目录存在而新目录为空」时弹一次迁移确认。

### R11　（🛠 已修复，D105 批 B）`profile.Id` 未校验就拼路径，可写/删 `env/` 之外的文件 ✔已核对
- 位置：`RuntimeProfileStore.cs:134`、`:148`；`RuntimeEnvironmentSetting.xaml.cs:2328`。
- 证据：`Path.Combine(EnvironmentsFolder, profile.Id + ".json")`，而 `Id` 可被 JSON 文件内容覆盖（`RuntimeProfile.cs:21`）；`"id": "/home/u/.config/autostart/x"` 或 `"../../a"` 会逃逸。
- 建议：保存/删除前校验 `^[A-Za-z0-9_-]{1,64}$`，并 `Path.GetFullPath` 后校验前缀仍在 `EnvironmentsFolder` 内。

### R12　组件下载：GUI 不走镜像，CLI 走（同一功能两种表现） ✔子代理核对

**🛠 已修复（D106 批 C，2026-11-23）**：见 D106 对应条目；下方「建议」已按实现记录。
- 位置：`ComponentDownloader.cs:97`、`:82-92`；调用方 `RuntimeEnvironmentSetting.Components.cs:196` vs `Program.Linux.cs:1551`。
- 证据：`string actual = ApplyMirror(url, null);` 把 mirrorPrefix 写死 null，`DownloadAsync` 也没有该参数；页面不套镜像，只有 CLI 调 `ApplyMirror(..., source.MirrorPrefix)`。
- 建议：`DownloadAsync` 增加 `mirrorPrefix` 参数并在内部统一处理；补一条「页面下载走镜像」的 L1 测试。

### R13　组件扫描不用 `manifest.Root`，root≠"." 的组件导入后消失 ✔子代理核对

**🛠 已修复（D106 批 C，2026-11-23）**：见 D106 对应条目；下方「建议」已按实现记录。
- 位置：`ComponentStore.cs:232`；`ComponentImport` 写 `Root`（`ComponentImporter.cs:78`、`:97`）。
- 证据：`ComponentLayouts.WinePath(dir, manifest?.Layout ?? "")` 把组件目录当 root；全仓没有读取 `manifest.Root` 的地方。
- 建议：`WinePath(Path.Combine(dir, manifest?.Root ?? "."), layout)`；补「归档内 wine 在子目录」的导入→扫描测试。

### R14　单流续传不校验 206，服务器忽略 Range 时把全文追加到半截文件 ✔子代理核对

**🛠 已修复（D106 批 C，2026-11-23）**：见 D106 对应条目；下方「建议」已按实现记录。
- 位置：`ComponentDownloader.cs:395-410`。
- 证据：`resumed = have>0 && total>0 && have<total`；只要 `have>0` 就以 `FileMode.Append` 打开，`:399-401` 只 `EnsureSuccessStatusCode()`，不检查 `PartialContent`；total 为 null 时同样追加。
- 建议：追加前要求 `StatusCode == PartialContent`，否则 `FileMode.Create` 重下并删旧文件。

### R15　`ComponentHealth` 的 15 s 超时只写进返回值，从不 Kill 进程 ✔子代理核对

**🛠 已修复（D106 批 C，2026-11-23）**：见 D106 对应条目；下方「建议」已按实现记录。
- 位置：`ComponentHealth.cs:99-129`。
- 证据：`CancelAfter(15s)` + `WaitForExitAsync(token)`，catch 里只 append 文本；stdout/stderr 用 `CancellationToken.None` 读。
- 影响：wine 卡住后进程常驻；无法区分「超时」与「用户取消」。
- 建议：超时分支 `Kill(entireProcessTree:true)`，按 `ct.IsCancellationRequested` 区分。

### R16　`ComponentStore` 的 `wine --version` 4 s 超时形同虚设 ✔子代理核对

**🛠 已修复（D106 批 C，2026-11-23）**：见 D106 对应条目；下方「建议」已按实现记录。
- 位置：`ComponentStore.cs:390-400`。
- 证据：先 `ReadToEnd()` 再 `WaitForExit(4000)`（返回值被忽略）；stderr 重定向但不读；`GetRunners` 对每个运行器都跑一次，且处于保存后的 UI 线程链路上。
- 建议：`WaitForExitAsync` + `CancelAfter`，两路流并发读，超时 Kill；运行器版本结果加真实缓存。

### R17　图形层不识别 DXVK 的 `x32/` 目录，32 位 dll 永不安装 ✔已核对

**🛠 已修复（D106 批 C，2026-11-23）**：见 D106 对应条目；下方「建议」已按实现记录。
- 位置：`GraphicsInstaller.cs:362-383`、`:334-372`。
- 证据：只判断 `x64/` 与 `x86/`；DXVK 2.x 发布物是 `x64/ + x32/`（VKD3D-Proton 才是 `x86/`）。另外目录选择靠 `target.EndsWith("32.dll") || target.StartsWith("x86")`（:170），名字里恰好含 32 的 64 位 dll 会落进 syswow64。
- 建议：`x86`/`x32` 同时识别，按来源目录而不是文件名决定目标。

### R18　（🛠 已修复，D107 批 D）gamescope/gamemoderun 无条件拼进 argv，缺程序时启动直接失败 ✔子代理核对
- 位置：`Enhancements.cs:97-101`；`LaunchPreflight.cs:364-375`；`GameLauncherService.Linux.cs:335`。
- 证据：wrappers 直接 `"gamescope"`/`"gamemoderun"`，不 `FindInPath`；预检只给 Warning；启动时 `Process.Start` 抛 Win32Exception（并把 R05 的锁一起泄漏）。
- 建议：找不到的 wrapper 跳过并给可见提示，或预检判 Failed 并带「打开运行环境」。

### R19　（🛠 已修复，D107 批 D）「Shell / RunOnce」与真实启动不是同一套环境 ✔子代理核对
- 位置：`RunOnceService.cs:64`；`ProfileShellService.cs:89-116`。
- 证据：只拼 `profile.Env`，不应用 `recipe.Environment`；`PrefixPath` 为空的方案直接没有 `WINEPREFIX`（落进 `~/.wine`），也没有 Proton 的 `WINEDLLPATH`/`LD_LIBRARY_PATH`/`PROTONPATH` 与 HUD；全程不取前缀锁。两个文件的开头都写着「环境变量与游戏启动完全一致」。
- 建议：抽出唯一的环境装配函数给「启动 / Shell / RunOnce / 伴随启动」共用，锁作为参数传入。

---

## 3 · Medium

- **R20（🛠 已修复 D107 批 D）**　前缀体积在 UI 线程扫盘**：`RuntimeEnvironmentSetting.xaml.cs:545-567 LoadPrefixes()` 同步调 `PrefixManager.EnumerateKnownPrefixes(profiles)`（默认 `computeSize:true`，递归 stat），ctor `:41` 与保存/删除/克隆都会走到；同页 `RefreshChipsAsync`、占用汇总都已后台。设计 §3.6 明确「后台算、10 分钟缓存、不放列表卡片」。另有 `Profiles.cs:41/:210` 每方案 `Inspect(computeSize:true)`，同一前缀被算三遍。建议：列表 `computeSize:false`，选中时后台算。
- **R21（🛠 已修复 D107 批 D）**　保存后 `ComponentStore.Invalidate()` → `LoadAll` → 每个运行器 `wine --version` 冷 fork，全在 UI 线程**（`xaml.cs:1336`；同类 `:39/:51/:531/:548/:1915-1918`）。违反硬规则 10。建议：`LoadAll` 只展开路径、页面 `await Task.Run(...)`；版本结果真缓存。
- **R22（🛠 已修复 D107 批 D）**　删除配方勾「连前缀一起删」会连坐共用前缀**（`xaml.cs:2291-2346`、`store #5`）：只查绑定用量，不查其它方案是否指向同一路径；克隆默认共享前缀。建议：删前统计同路径方案数，>1 时列出并禁止一键删。
- **R23（🛠 已修复 D107 批 D）**　`ContentDialog.ShowAsync` 未防重入**（`xaml.cs:298/:693/:1818`）：连点两个检查类按钮时第二次抛 `InvalidOperationException`（async void 未处理）。建议：加 `_dialogOpen` 守卫 + try。
- **R24（🛠 已修复 D107 批 D）**　删除最后一份方案又被自动重建**（`RuntimeProfileStore.cs:95-99`）：`LoadAll` 见 0 份就 `CreateDefault()`，「删了又回来」，页面空列表分支不可达。建议：只在首次启动创建。
- **R25（🛠 已修复 D107 批 D）**　`LastKnownGoodStore` 只写不读**（写：`GameLauncherService.cs:493`；读：仅 `DiagnosticBundle`）：设计 §6「上次成功：11-14 21:03 / 用上次能跑的组合重新启动」未实现；且条目缺 `runnerId`/`graphicsId`，`PrefixPath` 记的是未展开值。建议：补字段与消费方。
- **R26（🛠 已修复 D107 批 D）**　预检「去设置」不带 profileId/tab**：页面无 `OnNavigatedTo`，永远选第一份方案 → §13.10「跳到 运行环境 → 增强」未实现。
- **R27（🛠 已修复 D107 批 D）**　编辑器预览的 draft 不含 Enhancements**（`xaml.cs:1198-1204`）：预览看不到 gamemode/mangohud；名字 `TextChanged` 也不刷新预览与前缀行（`:1060-1064`）。
- **R28（🛠 已修复 D107 批 D）**　保存/删除/克隆后不刷新 chips 与失效绑定提示**（`:1336/:2258/:2347`；`RefreshChipsAsync` 只在 ctor 调）。
- **R29（🛠 已修复 D107 批 D）**　前缀路径保存零校验**（`:1315-1326`）：`/`、相对路径、`..`、不可写目录都收，错误延后到 `EnsurePrefix`。
- **R30（🛠 已修复 D107 批 D）**　`IsPrefixLocked` 把「没权限」也报成「被另一个实例使用」**（`PrefixManager.cs:67-75`；`LaunchPreflight.cs:232-245`）：动作是「查看占用进程」，权限问题下是死胡同。
- **R31　组件分片失败被吞、MergeParts 以目录文件数当片数、缺片静默跳过、失败后不清理分片**（`ComponentDownloader.cs:256-270/:328/:441-456`）：一次 403/429 最终以「长度不一致」暴露，且重试必复现。建议：连续失败计数、任一片失败即抛并清理；合并上界用 `ChunkCount`，缺片立即抛。　— **🛠 已修复（D106 批 C）**
- **R32　`ArchiveExtractor` 三处边界**：符号链接校验少尾部分隔符（`:257-262` 与 `:234` 不一致）；绝对路径条目被静默改写成相对路径（`:228`，注释却说拒绝）；`tar.xz` 分支无总大小上限与逐块取消（`:171`，xz 炸弹可无限膨胀，页面 df 预检只按压缩包×2 估算）。　— **🛠 已修复（D106 批 C）**
- **R33　组件导入语义不一致**（`ComponentImporter.cs:49-52` vs `Program.Linux.cs:1580-1583`：页面算失败、CLI 算跳过；且先 `Directory.Move` 再写 manifest，中间失败留下不可再导入的目录）。建议：统一语义 + 先写临时目录再整体 rename。　— **🛠 已修复（D106 批 C）**
- **R34　下载完整性标注失真**（`ComponentDownloader.cs:140-142/:141`；`ComponentImporter.cs:107-109`）：拿不到源端 digest 时不做校验却返回 `DigestMatched=true`；`FromApi = !string.IsNullOrEmpty(sourceUrl)` 把本地自算哈希标成「源端提供」。建议：加参数区分期望值与自算值；未校验时 `DigestMatched=false` 并提示。　— **🛠 已修复（D106 批 C）**

---

## 4 · Low / 文档漂移

- **R35（🛠 已修复 D107 批 D）**　`ComponentNaming.Sanitize` 不移除 `.`/`..`**（`ComponentManifest.cs:188-195`）；`EnvironmentResolver.EffectivePrefixPath`（`:95-103`）直接 `Path.Combine` → 方案名 `..` 把前缀算到 `~/.local/share/lunarward`，`.` 算到 PrefixRoot。建议：名称白名单化（`^[A-Za-z0-9_-]{1,64}$`）+ 拼完校验。
- **R36（🛠 已修复 D107 批 D）**　`LaunchRecipeBuilder.SplitArgs` 对引号外的 `\` 做转义**（`LaunchRecipe.cs:62-117`）：`C:\Games\...` 这类参数会被吃掉反斜杠。建议：引号外不处理反斜杠（与 `ArgumentList` 语义一致）。
- **R37　`GraphicsInstaller` 两处一致性**：`EnsureInstalled` 的「同 Id 跳过」不看文件大小（`:141-150`，`Verify` 却校验 size）；`Remove(kind)` 在文件删除失败时仍删记录（`:300-330`）→ 无主 dll。
- **R38（🛠 已修复 D107 批 D）**　`--launch-plan` 与真实启动不同源**（`GameLauncherService.Linux.cs:434-452`）：plan 用 `ResolveRunner` + 原始 arg，真实启动用 `profile.RunnerPath` + effectiveArgs，两者命令行必然不同（叠加 R02）。建议：plan 复用 `CreateGameProcessStartInfo`，只打印不启动。
- **R39（🛠 已修复 D107 批 D）**　第二实例深链启动的 Task 被丢弃**（`App.xaml.cs:168`）：`Task ignoredLaunch = …StartGameAsync(gameId);` 无 await/catch，预检失败与启动异常无 UI 反馈。
- **R40（🛠 已修复 D107 批 D）**　文档 / 注释漂移**：默认前缀代码是 `~/.local/share/lunarward/prefix`，设计 §4.1/§8 写 `prefixes/`；`PrefixManager.cs:1-12` 文件头仍列 D73 已删除的五种模式；`ProfileBinding` 文件名是 `profile-bindings.json`，设计 §8 写 `env-bindings.json`（且无迁移）。
- **R41（🛠 已修复 D107 批 D）**　旧 `graphicsId` 读入即丢、`RunnerPath` 缓存字段被持久化**（`RuntimeProfile.cs:100-103`、`store #14`）：升级用户不会收到「图形层需重选」提示；导出配置会带上别处绝对路径。
- **R42（🛠 已修复 D107 批 D）**　`LastKnownGoodStore.Load` 出错后用空表覆盖**（`LastKnownGoodStore.cs:58-77/:106-107`）；`LaunchHistoryStore` 截断用非原子 `WriteAllLines`、并发追加抛 IOException 被吞（`LaunchHistoryStore.cs:167-175/:107`）。

---

## 5 · 建议修复批次（供拍板）

1. **批 A（启动链正确性，Critical）**：R01 R02 R03 R04 R05 R06 —— 改动集中在 `GameLauncherService.Linux.cs`、`LaunchRecipe.cs`、`PrefixManager.cs`、`RuntimeProfileStore.cs`。
2. **批 B（持久化安全）**：R07 R08 R09 R10 R11 —— `JsonFile.cs`、`RuntimeProfileStore.cs`、`ProfileBinding.cs` + 迁移恢复。
3. **批 C（组件层）**：R12 R13 R14 R15 R16 R31 R32 R33 R34 —— `Component*`、`ArchiveExtractor.cs`。
4. **批 D（页面体验与一致性）**：R20–R30、R35、R38–R42。

每批仍然：`dotnet build Starward.slnx`（Debug+Release）→ `dotnet test` → `lint-ui-thread.sh` → 逐条补 26/27 状态与证据 → 单独提交。

---

## 6 · 第二轮全量审查（2026-11-24，R43–R130）

> 触发：用户 m06812「再逐文件仔细审查一遍所有『运行环境』相关代码，包括逻辑层，UI 等等，找出所有存在的细节问题，以及可改进的地方，并给出相应的意见」。
> 范围：`src/Lunarward/Features/Runtime/*.cs`（25 文件 6977 行）、`Features/Setting/RuntimeEnvironmentSetting*.cs`（5 个 partial 6156 行）、`RuntimeEnvironmentSetting.xaml`（572 行），并回查 `GameLauncher/GameLauncherService(.Linux).cs`、`LaunchPreflight.cs`、`GameSettingPage.xaml.cs`、`Helpers/JsonFile.cs` 等调用方。
> 方法：4 组只读子代理分工通读（页面 / 组件与下载 / 启动与解析 / 方案与绑定）+ 本人 grep 自查 + 抽查关键断言；标 ✔已核对的条目由本人回源码逐行确认。初稿中标「推断」的 7 条已于 2026-11-24 逐条取证并订正（结论与实测见 §6.12）：其中 R55 的「建前缀失败」被实测否定、R77 的「死锁面」被代码否定、R94 的适用范围收窄到「手改文件」。仍未实测的只剩「外部服务端行为」（R61 触发、R62 可达性）。
> 状态：**批 E–I 已修**（批 E：R43/R44/R45/R64，D115；批 F：R46–R56，D116；批 G：R57–R63，D117；批 H：R65–R76，D118；批 I：R77–R85、R93，D119）；其余未修，按 §6.11 批次推进，改完在对应条目标 🛠 并写决策号。

### 6.1 结论摘要

| 严重度 | 条数 | 代表问题 |
| --- | --- | --- |
| Critical（默认路径必现失效 / 静默丢数据） | 3 | 下载的 wine 没有执行位；首启改名换前缀；JSON `null` 覆盖集合字段 |
| High（可复现故障 / 丢配置 / 卡界面 / 崩） | 52 | 前缀锁顺序与泄漏、取消下载删分片、增强开关漏拷、UI 线程扫盘与 fork wine、弹窗叠层 |
| Medium（体验、一致性、边界） | 20 | 默认源整对象覆盖、`LD_LIBRARY_PATH` 覆盖、mangohud 误判、`Sanitize` 折叠非 ASCII |
| Low（注释 / 死代码 / 文案 / 无障碍） | 13 | 死资源键约 50 个、4 个无订阅按钮、`Consolas` 字体与 `· · ·` 分隔线 |

先修顺序建议：**R43 → R44 → R45 → R64**（四条都是「静默丢东西」，用户不报也查不出来），再按 §6.9 分批。

### 6.2 Critical

**R43　（🛠 已修复，D115 批 E）解压 tar.xz / zip 不写 Unix 权限位，下载来的 wine 没有执行权限** ✔已核对
- 位置：`src/Lunarward/Features/Runtime/ArchiveExtractor.cs:147`（`TrySetMode(target, entry.Mode)` 只在 tar 分支）、`:173-176`（xz 分支）、`:194-200`（zip 分支）。
- 证据：全仓 `TrySetMode` 仅此一处调用；`ComponentStore.cs:503-506` 用 `File.GetUnixFileMode(...) & anyExec` 判定可执行。
- 影响：默认源 `wine-staging`（Kron4ek/Wine-Builds）与 `GE-Proton` 都只发 `.tar.xz`，所以「下载 → 安装」后 `bin/wine` 是 0644，组件行立刻显示「没有可执行权限」，绑定后启动失败；`.zip` 导入同理。现有 `ArchiveExtractorTests` 无权限位用例，测不出来。
- 建议：xz 从 `TarReader`/`IEntry` 取 Mode、zip 用 `ZipArchiveEntry.ExternalAttributes >> 16` 转 `UnixFileMode`，统一走 `TrySetMode`；`ComponentImporter.ImportAsync` 收尾对 `bin/wine`、`bin/wine64`、`files/bin/wine`、`wineserver*`、`*.so*` 兜底补 `u+x`（只补位、不夺权）；补两条 L1 测试（tar.xz / zip 解出后 `bin/wine` 有执行位）。

**R44　（🛠 已修复，D115 批 E）首次启动任意游戏会「就地改名」方案，顺带换掉前缀目录** ✔已核对
- 位置：`Features/Runtime/ProfileBinding.cs:249-257`；`Features/Runtime/EnvironmentResolver.cs:99-113`（`EffectivePrefixPath`）。
- 证据：`all.Count == 1 && Usages(fallback.Id).Count == 0` 时 `fallback.Name = gameName`；`EffectivePrefixPath` 在 `PrefixPath` 为空时 `Path.Combine(root, ComponentNaming.Sanitize(name).ToLowerInvariant())`，即前缀目录由**方案名**推导。
- 影响：默认方案 `PrefixPath` 本就为空，「第一次启动某游戏」= 改名 = 换前缀目录。此前用调试 Shell / 一次性运行 / `--prefix-ensure` 在该前缀装过的 DXVK、注册表、字体全留在孤儿目录，启动时重建空目录——用户看到「图形层突然没了」，磁盘上还多一份前缀。`Usages()` 只统计绑定，探测不到 Shell / RunOnce 留下的前缀。
- 建议：方案名与前缀目录解耦——`RuntimeProfile` 增不可变 `PrefixSlug`（首次生成后不再变），`EffectivePrefixPath` 优先用它；或改名时若旧目录存在就 `Directory.Move` 并在 UI 明确提示；改名事件写进 `LaunchHistoryStore`。

**R45　（🛠 已修复，D115 批 E）JSON 里显式 `null` 覆盖集合初始化器，运行期 NRE 且 `LoadAll` 抓不到** ✔已核对
- 位置：`Features/Runtime/RuntimeProfile.cs:56`（`Env { get; set; } = new()`）、`:64`（`Wrappers = new()`）；消费方 `RuntimeEnvironment.cs:49`、`Enhancements.cs:103`、`LaunchRecipe.cs:217`（`profile?.Env.TryGetValue` 的 `?.` 只保护 `profile`）。
- 证据：缺字段安全（初始化器保留），但 `"env": null` / `"wrappers": null` 由 STJ 直接赋 null，反序列化成功 → `LoadAll` 的 try/catch 不触发，故障延后到启动/枚举时爆 NRE，栈里看不出是哪个 JSON。
- 建议：`RuntimeProfileStore` 反序列化后 `Normalize()` 里 `profile.Env ??= new(); profile.Wrappers ??= new(); profile.Name ??= "";`；读取处统一空字典兜底；补反序列化测试（`{"env":null}` 不抛）。

### 6.3 High · 启动链路与解析

**R46　wineboot 在拿前缀锁之前执行，两实例并发写同一前缀（TOCTOU）（🛠 已修复，D116 批 F）**
- 位置：`Features/Runtime/PrefixManager.cs:302-316`（`EnsurePrefix` 无条件建）；`Features/GameLauncher/GameLauncherService.Linux.cs:291`（`EnsurePrefix`）与 `:310`（`TryLockPrefix`）。
- 影响：GUI 启动与 `--prefix-ensure` / `--launch`（或双开）同时首启同一方案 → 两个 wineboot 同时写注册表与 `drive_c`，前缀半损坏且两边都继续启动。锁的意义（R05）被结构性地让位给锁外操作。
- 建议：先按 `EffectivePrefixPath` 拿锁，再在锁内做「存在性判断 + wineboot」；或 wineboot 到 `<prefix>.tmp-<pid>` 后原子 rename。

**R47　wineboot 超时被 Kill 后的半成品前缀被永久当作有效（🛠 已修复，D116 批 F）**
- 位置：`PrefixManager.cs:302-320`（超时 `Kill(entireProcessTree)` 后直接 throw，不清理）、`:432-446`。
- 影响：wineboot 卡在 wine-mono/Gecko 或运行器不可执行（3 分钟超时）时目录只剩骨架；下次 `EnsurePrefix` 只校验 `drive_c` 存在 → 拿缺 `system.reg` 的前缀跑，症状是「首次失败后每次启动都慢或报怪错」。
- 建议：wineboot 前写 `<prefix>/.lunarward/init.pending`，成功后删除；`EnsurePrefix` 见 pending 或无 `system.reg` 就重跑并告警；超时对受管路径删除半成品。

**R48　前缀锁取得后到 `Process.Start` 之间的异常都不释放锁（🛠 已修复，D116 批 F）**
- 位置：`GameLauncherService.Linux.cs:310,473-485`（只有 `catch { ReleasePrefixLock(...); throw; }` 包住 `Process.Start`）。
- 影响：上述 NRE、`Assemble`/`Build`/`info.Environment` 上抛的任何异常 → `_prefixLocks` 里的 `FileStream` 陪进程活到退出，该前缀此后一律「正被另一个实例使用」，只能重启应用。
- 建议：`CreateGameProcessStartInfo` 整体 try/catch，失败即 `ReleasePrefixLock` 后 rethrow。

**R49　前缀校验失败降级到 `~/.wine`，随后仍把 DXVK/VKD3D 装进这个降级前缀（🛠 已修复，D116 批 F）**
- 位置：`GameLauncherService.Linux.cs:296-305`（catch 里 `prefix = ResolvePrefix(...)`）、`:317-351`（图形层用同一个 `prefix` 装机）。
- 影响：前缀缺 `drive_c`、无写权限或 wineboot 失败时，d3d9/d3d10core/d3d11/dxgi 被覆盖进全局 `~/.wine/system32`，全机 wine 程序受影响，存档也落 `~/.wine`；只在 catcher 里一条 toast。
- 建议：降级路径跳过图形层安装（或只装进受管目录）并写进启动报告；更彻底的是不降级（保持 R03「失败即中止」）。

**R50　调试 Shell / 一次性运行不传 gameKey，默认前缀方案拿不到 WINEPREFIX（🛠 已修复，D116 批 F）**
- 位置：`Features/Runtime/ProfileShellService.cs:75,89-93`；`RunOnceService.cs:64`；签名 `BuildEnvironment(RuntimeProfile profile, string? gameKey = null)`。
- 影响：`PrefixPath` 为空的方案（`RuntimeProfileStore.cs:306-312` 默认即空）→ Shell 里 `$WINEPREFIX` 为空，wine 用 `~/.wine`；用户在「同一环境」跑的补丁/安装器全落进全局前缀，游戏随后在 `prefixes/<name>` 看不到。
- 建议：调用点传 gameKey（或 `Assemble(profile, gameKey, EnvironmentResolver.EffectivePrefixPath(profile))`）。

**R51　兜底选出的「第一个可用运行器」被写回 `RunnerId`（🛠 已修复，D116 批 F）**
- 位置：`EnvironmentResolver.cs:89-94`（`FirstOrDefault(x => x.Usable == true) ?? FirstOrDefault()`）+ 保存路径。
- 影响：外盘 Proton 未挂载、组件扫描不完整时，用户下次保存任意设置就被换成别的 wine，界面无提示（只有 Debug/Info 日志）。
- 建议：只有「id 在表里但文件缺失/不可用」才改写；不在表里保留原值并提示「未挂载/未扫描到」。

**R52　`profile.Id` 衍生的回退前缀路径没做越界校验（🛠 已修复，D116 批 F）**
- 位置：`EnvironmentResolver.cs:108-113`（`Path.Combine(root, string.IsNullOrWhiteSpace(profile?.Id) ? "default" : profile!.Id)`，无 `StartsWith` 复核；name 分支有）。
- 影响：手改 `<id>.json` 的 `"Id":"../x"`（或绝对路径 Id 丢弃 root）→ 前缀落到 PrefixRoot 外，`Managed=false`，图形层可被装进任意目录，`--prefix-remove` 也删不到。
- 建议：回退分支同样 `Sanitize` + 越界复核，非法 Id 用 `"default"`。

**R53　`PrefixPath` 只展开 `~/`、`$HOME/`、`${HOME}/`，裸 `$HOME` 与相对路径不绝对化（🛠 已修复，D116 批 F）**
- 位置：`Features/Runtime/PrefixPath.cs:25-42`。
- 影响：`$HOME` 得到字面量 → 在**当前工作目录**建名为 `$HOME` 的前缀；`"myprefix"` 这种相对路径随 CWD 漂移（wine 记绝对路径，换个 CWD 启动就是另一个前缀）。
- 建议：展开后 `Path.IsPathRooted` 校验，否则 `GetFullPath` 并提示；补齐裸 `$HOME`。

**R54　`wow64` 被归一成 `win32`，方向反了（🛠 已修复，D116 批 F）**
- 位置：`Features/Runtime/PeArchitecture.cs:118`（`return value is "win32" or "wow64" ? "win32" : "win64";`），该值在 `PrefixManager.cs:424` 作为 `WINEARCH` 交给 wineboot。
- 影响：配方写 `PrefixArch=wow64`（语义 = 64 位前缀 + WoW64）→ 实际建出 32 位前缀 → 64 位游戏被预检判 `BAD EXE FORMAT`（`:133`）挡住；wine 本身也不接受 `wow64` 这个值。
- 建议：`"wow64" => "win64"`，或明确拒绝并提示只支持 `win32`/`win64`。

**R55　wineboot 不套 Proton 运行时环境，与启动游戏时不一致（🛠 已修复，D116 批 F）**
- 位置：`PrefixManager.cs:393-449`（只设 `WINEPREFIX`/`WINEDEBUG`/`WINEARCH`/`WINEDLLOVERRIDES`）；对照 `RuntimeEnvironment.cs:75-131` 已有 `ProtonEnvironment`。
- 影响：建前缀时子进程只拿到 4 个变量，没有 `WINEDLLPATH`/`LD_LIBRARY_PATH`/`PROTONPATH`。2026-11-24 实测（本机 Proton `cachyos-11.0`，`/usr/share/steam/compatibilitytools.d/proton-cachyos-slr`）：用与代码完全相同的变量跑 `wine wineboot -u`，11.5 s 建成前缀（650 MB）→ 「建前缀失败/超时」不成立；实际差异是 wineboot 与游戏启动时环境不一致（缺 Proton 自带 dll 的搜索路径），后续在 winecfg / 安装器里跑的组件可能与游戏里选择不同 dll，属一致性缺陷。
- 建议：wineboot 复用 `ProtonEnvironment(wine)` + 配方 env（至少 PATH / LD_LIBRARY_PATH / WINEDLLPATH）。

**R56　`GraphicsInstaller` 无事务、无回滚、完整性判定依赖 `Sizes`（🛠 已修复，D116 批 F）**
- 位置：`GraphicsInstaller.cs:160-199`（先覆盖 dll、再删旧残留、最后写记录）、`:155,208-218`（`.bak` 只写不读，全仓无读取方）、`:146-153`（`Sizes` 缺失即视为通过）、`:387-391`（组件目录不存在时 `EnumerateFiles` 直接抛，批量循环中断）。
- 影响：copy 失败 → 新旧 dll 混搭（BAD EXE FORMAT）而界面仍显示已安装；装机失败/装错版本无回滚入口；被补丁覆盖成 0 字节的 dll 判「已安装」；组件目录被删时前一个装好、后面静默跳过。
- 建议：先写 `<prefix>/.lunarward/staging` 再整体替换，记录写成功后再删 stale；实现 `RestoreLastKnownGraphics(prefix)` 或在卸载/重装入口暴露 `.bak`；`Sizes` 缺失时补算回写（最低要求「存在且长度 > 0」）；`CollectFiles` 先 `Directory.Exists`。

### 6.4 High · 下载与导入

**R57　分片请求不校验 206，也不按 `length` 截断读取（🛠 已修复，D117 批 G）**
- 位置：`ComponentDownloader.cs:365-378`。
- 影响：镜像/代理忽略 `Range` 直接返回 200 + 完整归档时，4 条连接各自把 20+ GB 写一遍才发现「分片长度不符」，白耗带宽与磁盘（瞬时写满分区）后才降级。
- 建议：要求 `StatusCode == 206` 且 `Content-Range` 起始偏移匹配，否则立即抛（不要浪费一次完整下载）；拷贝循环累计到 `length` 即停，多余字节丢弃并 warning。

**R58　用户取消也会删光全部分片，与「已暂停，分片已保留」相反（🛠 已修复，D117 批 G）**
- 位置：`ComponentDownloader.cs:314-322`（`catch (OperationCanceledException) when (!ct.IsCancellationRequested)` 只处理内部取消；后一支 `catch { CleanupParts(...); throw; }` 接住用户取消）；UI 文案 `RuntimeEnvironmentSetting.Components.cs:418/443` → `PausedKeepParts`。
- 影响：点「暂停」或关窗取消后已下的 10 GB 分片全删，下次从头开始；`Resumed` 只在单连接路径可能为 true。UI 与实际行为完全相反。
- 建议：单独一支 `catch (OperationCanceledException) when (ct.IsCancellationRequested)` 保留分片，只对非取消失败 `CleanupParts`；文案与真实语义对齐。

**R59　导入侧「像图形层」判定与发现侧布局判定不一致，导入成功但组件不可见（🛠 已修复，D117 批 G）**
- 位置：`ComponentImporter.cs:93`（递归 `EnumerateFiles(..., AllDirectories)` 找 d3d11/dxgi/...）vs `ComponentStore.cs:320-331`（只看 `<root>/x64/d3d11.dll` 等固定路径）。
- 影响：归档顶层不是「唯一目录」时（如 `dxvk-2.3.1/` 旁边还有 README/LICENSE → `root = "."`），dll 实际在 `temp/dxvk-2.3.1/x64/`，导入判定通过、manifest 写成，但 `GetGraphics` 永远看不到；重试又被「组件目录已存在」（`ComponentImporter.cs:60-63`）挡住。
- 建议：抽公共 `GraphicsLayout.Probe(dir)` 供导入校验与 `GetGraphics` 共用；导入完成前复核，失败回滚并明确报「根下缺 x64/d3d11.dll」。

**R60　解压体积上限：zip 无上限，xz 至少 32 GiB 才拦（🛠 已修复，D117 批 G）**
- 位置：`ArchiveExtractor.cs:158`（`Math.Max(32L << 30, file.Length * 200)`，只有 xz 用）、`:195`（zip 不传 `sizeLimit`）。
- 影响：恶意资产或 zip 炸弹可写满 `$XDG_DATA_HOME` 所在分区。
- 建议：三种格式统一传 limit；再加「条目数上限」「单文件上限」；数值改为 `Math.Min(绝对上限如 8 GiB, archive.Length * 合理倍数)`，对 xz 之外做压缩比检查。

**R61　非 `sha256:` 的 digest 被原样当作 sha256 使用（🛠 已修复，D117 批 G）**
- 位置：`ComponentSources.cs:389-393`（不匹配 `sha256:` 前缀时 `sha` 保持 `sha512:...` 原串），`ComponentDownloader.cs:149` 拿它和 `sha256=` 比较。
- 影响：代码路径确定——只有 `sha256:` 前缀会被剥掉，其它算法原串直接进 `Sha256`，再和 `sha256=` 比较必不相等（`ComponentSources.cs:389-393`、`ComponentDownloader.cs:149`）。GitHub 当前只发 sha256 属外部事实（离线无法验证），所以这是前向兼容缺陷：源端一旦换算法，下载 100% 完成后报「SHA-256 不一致」并删文件，永远下不完。
- 建议：只接受 `sha256:` 才赋 `Sha256`，其它算法置 null 并在 UI 标「无源端摘要」；错误文案区分「算法不支持」。

**R62　`fileName` 未校验即拼路径；同一文件并发下载无互斥（🛠 已修复，D117 批 G）**
- 位置：`ComponentDownloader.cs:99`（`Path.Combine(targetDirectory, fileName)`，`fileName` 来自 `asset.Name`）、`:216-345`、`:464-465`（分片名只由 `fileName` 决定）。
- 影响：代码未设防——`asset.Name` 直接进 `Path.Combine`，下载/解压路径全仓没有 `GetFileName`（`ComponentDownloader.cs:99`），含 `..`/`/` 的名字可写出目标目录之外；能否触发取决于服务端返回的资产名（GitHub 官方禁止 `/` 属外部约束，离线不能验证），按「输入未校验」定性。并发：GUI 队列与 CLI 同时下同一资产会互相覆盖分片、`MergeParts` 拼出坏文件（代码可证，未实测并发现象）。
- 建议：`Path.GetFileName` + 拒绝空/分隔符/`.` 开头，写入前 `GetFullPath` 复核仍在目标目录内；`workDir/<fileName>.lock` 以 `FileShare.None` 做互斥，或分片名加会话 GUID。

**R63　单连接下载无收尾长度校验；客户端总超时 30 分钟会掐断大包（🛠 已修复，D117 批 G）**
- 位置：`ComponentDownloader.cs:397-455`（结束后不比对字节数与 `total`）、`RuntimeEnvironmentSetting.Components.cs:354`（`HttpClient { Timeout = 30min }`）。
- 影响：代理截断的半截文件被当完整文件（无源端摘要时拦不住）；降级单连接后 20+ GB 归档在 <12 MB/s 网络下必被取消。
- 建议：结束时校验 `长度 == total`，不符删重下；大文件用 `Timeout.InfiniteTimeSpan` + 自实现「60 秒无进度即超时」。

### 6.5 High · 方案、绑定与持久化

**R64　（🛠 已修复，D115 批 E）`RuntimeProfileStore.Save` 的返回值被丢弃，绑定写道不存在的方案** ✔已核对
- 位置：`ProfileBinding.cs:274-275`（`RuntimeProfileStore.Save(created, logger); Set(b, created.Id);`）。
- 影响：磁盘满/权限/目录被删时文件没写成，绑定照样写入 → `Resolve` 指向不存在的 id，`Dangling` 出现悬空记录；用户反复看到「已创建运行环境」但启动仍用兜底方案。
- 建议：`if (!RuntimeProfileStore.Save(...)) { LogWarning; return (fallback, false, …); }`，或 Save 失败抛出让上层提示。

**R65　（🛠 已修复，D118 批 H）新游戏方案漏拷增强开关，MangoHud/gamemode/gamescope 静默关闭**
- 位置：`ProfileBinding.cs:259-273`（只复制 `Name/RunnerId/RunnerPath/DxvkId/Vkd3dId/Env/Args/Wrappers/PrefixArch/Shell/GameOutput/Notes`），`RuntimeProfile.cs:72-88` 的 `Hud/HudOptions/GameMode/GameScope/GameScopeArgs` 走默认。
- 影响：默认方案开好 HUD + gamemode，第一次启动新游戏时复制出的方案全关；预检「增强开关」项还显示 Ok，用户看不出为什么 HUD 没了。
- 建议：实现 `RuntimeProfile.CopyWithNewId()` 逐字段复制 + 单测；或加「字段清单 + 反射断言」防止下次加字段再漏。

**R66　（🛠 已修复，D118 批 H）迁移时目标文件已存在则直接删源，方案静默丢失**
- 位置：`RuntimeProfileStore.cs:263-268`（`if (!File.Exists(target)) WriteAtomic(...); File.Delete(file);`）。
- 影响：id 撞车（复制过 env.json、上次迁移中断、手工拷回）时旧目录方案被删，新文件是另一份内容，日志仍打 "Migrated"。
- 建议：目标存在时不删源，改名 `<name>.migrated-conflict-<ts>.json` 并 `LogWarning`。

**R67　（🛠 已修复，D118 批 H）`LegacyGraphicsId` 一次扫不到就永久清零**
- 位置：`RuntimeProfileStore.cs:166-188`（`ComponentStore.GetGraphics()` 找不到就 `LegacyGraphicsId = null; changed = true;`），`:119-122` 落盘。
- 影响：外置盘/移动硬盘未挂载、`~/.local/share/<launcher>` 临时挪走 → 用户已选图形层被**永久**清空（不是回退默认，是丢配置）。
- 建议：只在有卸载证据（`ComponentHealth` 记录）时清；否则保留 id 只标 unknown，或连续 N 次 LoadAll 都找不到才清。

**R68　（🛠 已修复，D118 批 H）「默认方案」没有显式标记，靠 `CurrentCulture` 排序首项**
- 位置：`RuntimeProfileStore.cs:145`（`OrderBy(x => x.Name, StringComparer.CurrentCulture)`）、`ProfileBinding.cs:305`（`Resolve` 兜底 `all[0]`）。
- 影响：默认方案随 locale、改名、新增方案漂移；CJK/大小写排序结果不稳定（`a` vs `A` 因 culture 而异）。
- 建议：`RuntimeDefaults` 增 `DefaultProfileId`（或 `RuntimeProfile.IsDefault`），`Resolve` 用它；展示排序显式 `StringComparer.Ordinal`/`InvariantCulture`。

**R69　（🛠 已修复，D118 批 H）方案重名无校验 + 前缀由名推导，两份方案共用同一前缀**
- 位置：`RuntimeProfileStore.cs:196-212`（`Save` 只 `ExpandPath(PrefixPath)`）、`RuntimeEnvironmentSetting.xaml.cs:1693-1700`（保存只 `Trim()`）。
- 影响：两个同名方案（或改名撞上）且 `PrefixPath` 都空 → 同一目录；装/删 DXVK 互相影响，`EnsureFor` 的按名匹配（`:238-241`）随机选中一份。
- 建议：`Save` 对 `Name` trim、空值回落、长度上限（64）、拒绝 `/\:*?"<>|` 与控制字符，重名自动加 ` (2)`；`EffectivePrefixPath` 改用不可变 slug（见 R44）。

**R70　（🛠 已修复，D118 批 H）`ProfileBinding.Set` 抛异常，UI 与启动路径都没接**
- 位置：`ProfileBinding.cs:125-141`（`throw new InvalidOperationException("游戏绑定文件已损坏…")`）；调用点 `GameSettingPage.xaml.cs:690`、`RuntimeEnvironmentSetting.Profiles.cs:680-684` 无 try。
- 影响：绑定文件被手改坏后，用户在下拉框选一次方案即未处理异常（`async void` 场景 = 崩）。
- 建议：`Set` 改返回 `(bool ok, string? error)`，或三处统一 try + toast；启动路径单独 catch 后降级「不绑定、用兜底方案」并 Warning。

**R71　（🛠 已修复，D118 批 H）`ClearFor` 在绑定文件损坏时静默返回 0，删除流程据此误判**
- 位置：`ProfileBinding.cs:183-204`（`Load()` 吞错返回空字典 → `keys.Count == 0` 直接返回）；调用点 `RuntimeEnvironmentSetting.xaml.cs:2748`。
- 影响：对话框说「未被任何游戏使用」，用户确认后方案文件已删（`:2804`），`ClearFor` 又清 0 条 → 绑定里留下悬空 id。
- 建议：`Load` 增加 `TryLoad` 语义分流；`Usages/ClearFor` 在损坏时返回错误码，UI 明确「无法判断，先修复绑定文件」。

**R72　（🛠 已修复，D118 批 H）大小写语义不一致（`OrdinalIgnoreCase` vs `Ordinal`）**
- 位置：`ProfileBinding.cs:153`（`HashSet<string>(..., StringComparer.OrdinalIgnoreCase)`）vs `:291`（`p.Id == id`）。
- 影响：手改 Id 大小写后，`Dangling` 认为有效、`Resolve` 认为悬空，两处 UI 结论相反。
- 建议：id 全链路统一 `Ordinal`（机器生成 ASCII），或在 `LoadAll` 里规范化大小写。

**R73　（🛠 已修复，D118 批 H）8 位十六进制（32 bit）id，复制文件即撞车**
- 位置：`RuntimeProfile.cs:21`（`Guid.NewGuid().ToString("n")[..8]`）、`RuntimeProfileStore.cs:105-117`（以文件名反推 id）。
- 影响：`env/` 出现两份同 id 文件时 `LoadAll` 两份都读，`Save` 都写 `ProfileFile(Id)` 同一路径静默互相覆盖，`Delete` 只删一个。
- 建议：id 用 16 位以上或完整 Guid；`LoadAll` 检出重复 id 并改名隔离。

**R74　（🛠 已修复，D118 批 H）无 schema 门禁、无大小写兼容读取**
- 位置：`RuntimeProfileStore.cs:99`（`JsonOptions` 无 `PropertyNameCaseInsensitive`）、`RuntimeProfile.cs:18`（`SchemaVersion = 4` 全仓无读取方）。
- 影响：手改成 camelCase 后所有字段静默变默认值——不是报错，是「配置看起来没错但没生效」；未来改结构也无版本入口。
- 建议：`PropertyNameCaseInsensitive = true`；升版本时写迁移分支，读到 `SchemaVersion > 当前` 拒绝加载并保留原文件。

**R75　（🛠 已修复，D118 批 H）任何读取异常都当成内容损坏改名；下拉用方案名 + 按索引二次 LoadAll**
- 位置：`RuntimeProfileStore.cs:129-136`（`catch { File.Move(file, $"{file}.corrupt-…") }`）；`GameSettingPage.xaml.cs:822-828`（`Items.Add(profile.Name)`；`LoadAll()[SelectedIndex - 1].Id`）。
- 影响：权限/磁盘/网络盘瞬时错误把**好文件**改名，方案在列表里消失；重名方案不可区分，对话框停留期间列表变化 → 索引错位绑到别的方案或越界。
- 建议：区分 `JsonException`（真损坏改名）与 `IOException/UnauthorizedAccessException`（记日志、保留原名、跳过）；下拉放 `(Id, Name)` 视图模型，保存直接用 Id。

**R76　（🛠 已修复，D118 批 H）诊断包里的 bindings.json 在损坏时导出 `{}`，不附错误**
- 位置：`ProfileBinding.cs:26-36`（`JsonSerializer.Serialize(Load())`，`LastError` 不进文件）；`Helpers/DiagnosticBundle.cs:47` 原样写入。
- 影响：用户报「绑定丢了」，诊断包里只有 `{}`，看不出是损坏还是真空。
- 建议：导出 `{ "lastError": …, "bindings": {…}, "raw": "…" }`。

### 6.6 High · UI 线程、资源与并发

**R77　（🛠 已修复，D119 批 I）「添加运行器」在 UI 线程 `ReadRunnerVersion(...).GetAwaiter().GetResult()`** ✔已核对
- 位置：`RuntimeEnvironmentSetting.xaml.cs:944` → `ComponentStore.cs:404`（同步等待 `ReadRunnerVersionAsync`）。
- 影响：`ReadRunnerVersion` 是同步壳（`ComponentStore.cs:404` 的 `.GetAwaiter().GetResult()`），handler 在 UI 线程 → 阻塞。2026-11-24 实测：正常 wine `--version` 12 ms（`wineserver -k` 后第一次也只 12 ms），不会出现 4 秒冻；上限由 4 s 超时兜（`ComponentStore.cs:434`），挂住的 wine 会冻满 4 s。**死锁面不成立**：`ReadRunnerVersionAsync` 的 `await` 都带 `ConfigureAwait(false)`（`:439/451/454`），延续在线程池执行，`GetResult()` 能返回。L-127 的 4 秒冻来自另一处（`WineRegistry.GetValue`）。
- 建议：提供 `ReadRunnerVersionAsync`，handler 里 `await Task.Run(...)`。

**R78　（🛠 已修复，D119 批 I）打开编辑器同步全盘扫描（新建 / 修改 / Ctrl+N 三条入口）**
- 位置：`xaml.cs:1118-1119`（`ComponentStore.GetRunners(false, …)` / `GetGraphics(false, …)`）；`:2422-2425` 的 `Button_New_Click` 无 try/catch。
- 影响：2026-11-24 实测 `--list-components`（13 个组件）：进程启动 0.1 s + 扫描 0.6 s，冷热同量级 → 本机约 0.7 s，不是初稿的 2–8 s；随组件数与 Steam 库数量线性增长。与 642 行的自述「不能在 UI 线程上做」仍然冲突，结论保留、量级订正。
- 建议：`OpenEditor` 改 `async Task`，先 `await Task.Run(GetRunners/GetGraphics)` 再建控件（或复用 `LoadComponentsAsync` 的缓存）；补 try/catch。

**R79　（🛠 已修复，D119 批 I）点方案行 → 同步扫盘 + 全层 `Verify`**
- 位置：`xaml.cs:2497`（`ComponentStore.GetGraphics()`）、`Profiles.cs:864`（`GetRunners()`）、`xaml.cs:855`（每行 `GraphicsInstaller.Verify`）。
- 影响：每次点行 = 一次 `GetGraphics()` 扫描（本机 ≈0.6 s）+ 每个前缀一次 `Verify` 文件遍历；`Invalidate()` 之后落到冷缓存。
- 建议：详情区用 `TryGetCachedRunners` 拿显示名（`Profiles.cs:338-352` 已是这个思路），拿不到显示 Id；`Verify` 结果按前缀 mtime 缓存。

**R80　（🛠 已修复，D119 批 I）`LoadAll` 在读路径里写盘，且被 UI 线程直接调用**
- 位置：`RuntimeProfileStore.cs:119-122`（`if (changed) Save(profile, logger);` 丢弃返回值）；调用点 `xaml.cs:137/303/739/762/1683`、`GameSettingPage.xaml.cs:636/672/822`、`GameLauncherService.cs:429`。
- 影响：违反「UI 线程不许同步重 IO」（硬规则 10）；设置页每次刷新、游戏设置页每次打开都触发目录遍历 + 可能的写盘，外置库/网络盘秒级卡顿。
- 建议：拆成纯读 `LoadAll()` 与 `NormalizeAndSave()` 两步，UI 侧 `await Task.Run(...)`；复用 `ComponentStore` 的缓存扫描结果。

**R81　（🛠 已修复，D119 批 I）键盘处理器挂到窗口根、全仓 0 处 `RemoveHandler`，整页泄漏**
- 位置：`xaml.cs:252-259`（`root.AddHandler(KeyDownEvent, new KeyEventHandler(OnShortcutKeyDown), true)`；页面无 `OnUnloaded` 重写，`PageBase.cs:33-51` 已提供钩子）。
- 影响：守卫不生效有代码证据——页面自身的 `Visibility` 在 `RuntimeEnvironmentSetting*.cs` 里从未被赋值（只有子面板赋值），Frame 换内容不会改它；处理器挂在 `XamlRoot.Content`（窗口根），页面无 `OnUnloaded` 重写（`PageBase.cs:76-85` 空实现）、无 `RemoveHandler` → 旧实例被窗口根强引用、继续收快捷键。具体表现「一次 Ctrl+N 开多份编辑器」未实测：旧实例控件已脱离可视树，能确定的是内存泄漏 + 旧实例回调仍会执行（重复确认框 / 重复写盘路径）。
- 建议：`OnUnloaded` 里 `RemoveHandler`（保留委托实例）；守卫改为判断本页仍在可视树（`XamlRoot is not null && IsLoaded`）。

**R82　（🛠 已修复，D119 批 I）UI 线程递归删前缀 / 删图形层 / 逐前缀 Verify**
- 位置：`xaml.cs:1813`（`Directory.Delete(path, recursive: true)`）、`:1060`（`GraphicsInstaller.Remove`）、`:1073-1082`（`Verify`）、`:855`（每行 Verify，`LoadPrefixesAsync` 填充循环在 UI 线程）。
- 影响：同文件 2114 行删组件时已写「删目录可能几百 MB：一律后台跑（L-127）」并用 `Task.Run`，此处却在前台删，同一操作两种实现。
- 建议：统一 `await Task.Run(...)`；`Verify` 结果缓存。

**R83　（🛠 已修复，D119 批 I）`_ = LoadComponentsAsync()` 无取消 / 无代号 / 无 try**
- 位置：`xaml.cs:639-732`。
- 影响：连点筛选或反复切页签 → 并行扫描 + `Children.Clear()`+Add，可能重复行/闪烁；异常被即发即忘吞掉，列表停在上一次内容。
- 建议：try/catch + 日志 + 错误空态；用 `CancellationTokenSource` 或版本号丢弃过期结果。

**R84　（🛠 已修复，D119 批 I）组件三处静态缓存无同步**
- 位置：`ComponentStore.cs:154-167`、`:298-303`、`:683-687`；`Invalidate()` 与进行中的扫描存在竞态。
- 影响：页面 `Task.Run` + 下载弹窗 + CLI 并发时重复全量扫描并互相覆盖，旧结果可能写回。
- 建议：`SemaphoreSlim` / `Lazy<T>` 单飞，`Invalidate` 只递增版本号。

**R85　（🛠 已修复，D119 批 I）每次操作 `new HttpClient`，`Task.WhenAll` 无并发上限**
- 位置：`xaml.cs:2386`、`Components.cs:793/818/981/1108`、`:2395`。
- 影响：连点「检查全部源」不断创建/丢弃连接池；源多时瞬时打满网络。
- 建议：静态共享 `HttpClient`（或 `IHttpClientFactory`）+ `SemaphoreSlim` 限流。

### 6.7 High · 交互与状态

**R86　22 处 `ContentDialog.ShowAsync()` 只有 2 处受 `_dialogOpen` 守卫** ✔已核对
- 位置：`Components.cs` 10 处、`xaml.cs` 10 处、`Profiles.cs` 2 处；受保护仅 `xaml.cs:482`、`:2220`；`:32` 注释自己写「连点检查类按钮时第二次 ShowAsync 会直接抛」。
- 影响：未保护的 20 处含高频按钮（`xaml.cs:1053` 移除图形层、`:1380` 离开编辑器、`:1806` 清理前缀、`:2109` 删除组件、`:2605` 克隆到游戏、`:2670` 清理组件、`:2798` 删除方案、`:619` 运行一次；`Profiles.cs:675/824`；`Components.cs:851/955/1012/1043/1155/1256/1366`），连点即未捕获异常（`async void` = 崩）。
- 建议：抽 `TryShowDialogAsync(ContentDialog)` 统一加锁（计数 + `finally` 复位），全部替换。

**R87　「管理下载源」对话框未关闭时，编辑 / 删除 / 恢复默认再弹一层**
- 位置：`Components.cs:837`、`:851`、`:1004-1012`（外层 `:1043` 才 `ShowAsync`）；对照同文件 `468-471` 处理「下载弹窗 → 管理来源」时显式 `dialog.Hide()` 再开第二个。
- 影响：代码注释已记录该结论——`xaml.cs:31-33`「ContentDialog 同一时间只允许一个；连点检查类按钮时第二次 ShowAsync 会直接抛。」（R23 真机观察）。管理下载源对话框未关时再点「编辑 / 删除」即叠第二个弹窗：抛 `Only a single ContentDialog can be open`，或新弹窗压在下面收不到输入（Uno 行为未逐一实测，但作者在别处已用 `_dialogOpen` 规避）。
- 建议：调用前 `dialog.Hide()`、关闭后重开（照 468-471），或改页内编辑区。

**R88　「图形层」chip 跳到前缀页** ✔已核对
- 位置：`Profiles.cs:583`（`CreateChip(…ChipGraphics…, 2, …)`）+ `xaml.cs:358-362`（`pages = [Page_Profiles, Page_Components, ScrollViewer_Prefixes]`、`Math.Clamp(index, 0, 2)`）。
- 影响：点顶部「图形层 6 个」打开**前缀页**；点「前缀 N」（tag 3）被 clamp 成 2 反而正确。
- 建议：chip tag 只保留 0/1/2 语义（方案/组件/前缀），或改私有枚举。

**R89　`InitializeRunnerButtonTips()` 定义但从未调用** ✔已核对
- 位置：`xaml.cs:124`（全仓无调用点）。
- 影响：D113 加的 4 个按钮 tooltip（`Button_AddRunner`/`Button_SetDefaultRunner`/`Button_RunnerMore`/`Button_More`）全部没生效。
- 建议：构造函数 `InitializeComponent()` 之后调用。

**R90　「未使用」与「被 1 个方案使用」同时显示**
- 位置：`xaml.cs:900-908`（`owners.Count <= 1 && prefix.Exists` 时加「未使用」）vs `:817-819`（infoText 已写「被 {used} 个方案使用」）、`:810`（标题写 `owners[0]`）。
- 影响：恰好一个方案在用（最常见）时卡片同时出现「方案 A」「被 1 个方案使用」「未使用」；`owners.Count == 0 && !Managed` 时标题「外部前缀」下面又重复「未使用」。
- 建议：删除该分支，或改成 `owners.Count == 0 && prefix.Managed`。

**R91　编辑器有改动时点左列别的方案，静默丢弃**
- 位置：`Profiles.cs:95-106`（`ShowProfileDetail` + `ShowDetailPane()`，不走 `ConfirmLeaveEditorAsync`）；增强页 `Enhancements.cs:183`（`Button_EnhanceBack_Click`）与切页签同样无确认。
- 影响：未保存改动丢失；`Tab_Click`（`:340-347`）切页签会确认，这里不会；增强页完全无 dirty 概念。
- 建议：`Button_ProfileList_Click` 开头也走 `ConfirmLeaveEditorAsync()`；增强页复用同一确认。

**R92　`_saveThenRun` 只在成功路径复位；运行一次失败报「保存失败」**
- 位置：`xaml.cs:1717-1733`。
- 影响：`Save` 失败 → `return` 时 `_saveThenRun` 仍 true，之后点普通保存会莫名弹出「运行一次」选择框；`RunOnceAsync` 抛异常被同一 try 捕获，用户看到「保存失败」而文件其实已写盘。
- 建议：入口先取出并清零 `_saveThenRun`；`RunOnceAsync` 移出 try 或按阶段给不同文案。

**R93　（🛠 已修复，D119 批 I）7 个 `async void` 处理器无 try/catch**
- 位置：`xaml.cs:336`（`Tab_Click`）、`:580`（`Button_RunOnce_Click`）；`Profiles.cs:174`、`:613`、`:786`；`Components.cs:639`、`:641`；`Enhancements.cs:278`（共 33 个 async void 中的高危项）。
- 影响：`ConfirmLeaveEditorAsync` / `ShowAsync` 任一抛出即到同步上下文 → 崩。
- 建议：统一 try/catch + 日志 + toast（或 `async Task` + 小外壳）。

**R94　删除组件时「是否在用」未 Normalize，可删掉正在用的运行器**
- 位置：`xaml.cs:2063-2090`（`profile.RunnerPath == component.Path`）；对照 `:153-169`、`:741-743` 的 `ExpandPath` + `GetFullPath` + 去尾斜杠。
- 影响：比较用 `Ordinal` 原始字符串（`:2084`），同页其它地方先 `ExpandPath`+`GetFullPath` 归一（`:741`、`:1000`、`:1487`）。实测 `RunnerPath` 写入点全部来自组件路径（`xaml.cs:1689`、`EnvironmentResolver.cs:94`、`RuntimeProfileStore.cs:320`），编辑器里 `RunnerPath` 是只读 Hint（`Profiles.cs:366`）→ 只有手改或旧版 JSON 才会出现 `~`/尾斜杠/`..`；一旦出现即漏判，对话框说「未被使用」，确认后删掉正在用的运行器。
- 建议：统一 `NormalizePrefix(ExpandPath(profile.RunnerPath))`。

**R95　每个运行器行都 `LoadAll` 一次，构建行时全量读盘**
- 位置：`xaml.cs:738-747`（`CountProfilesUsingRunner` 内部 `RuntimeProfileStore.LoadAll`）。
- 影响：N 个运行器 = N 次 `LoadAll`（enumerate + 读全部 JSON + 可能的迁移写盘），全在 UI 线程；本机 `env/` 为空时成本可忽略，方案数上来后线性叠加。
- 建议：`LoadComponentsAsync` 开头取一次快照，按 `NormalizePrefix` 建 `Dictionary<string,int>` 传进去。

**R96　点「被 N 个方案使用」弹出的是全站汇总**
- 位置：`Profiles.cs:826-831`（`Tag = (prefix, owners)`）+ `Components.cs:1310-1372`（`Button_PrefixUsage_Click` 完全没用 `sender`）。
- 影响：用户想看「谁在用这个前缀」，得到的是一张无关的全量占用表。
- 建议：handler 先判 `Tag`，有 Tag 显示该前缀 owners，无 Tag 才走汇总。

**R97　HUD 用「关闭/Close」当选项；HUD 下拉无 Header；无方案时保存静默**
- 位置：`Enhancements.cs:28`（`Combo_Hud.Items.Add(Lang…Close2)`，该键在英文即 "Close"）、`RuntimeEnvironmentSetting.xaml:284-287`（`Combo_Hud` 无 `Header`）、`Enhancements.cs:278-284`（无选中方案时 `return`，无提示）。
- 影响：英文界面 HUD 首项显示 "Close"（应为 Off）；用户只能靠旁边硬编码的 "HUD" 猜字段含义；保存无反馈。
- 建议：新增 `HudOff` 键；补 `Header`；无方案时禁用整块或 toast 提示。

### 6.8 Medium

| 编号 | 问题 | 位置 | 建议 |
| --- | --- | --- | --- |
| R98 | 受管判定用 `StartsWith` 前缀且不解析符号链接 | `ComponentStore.cs:103,256,340` | 抽 `IsUnder(folder, path)`：两边 `GetFullPath` + 结尾分隔符比较，统一 RealPath |
| R99 | `ManualRunners.Add/Remove` 写完不 `Invalidate()` | `ComponentStore.cs:1020-1030`；调用 `xaml.cs:953` | Add/Remove 末尾调 `Invalidate()` |
| R100 | `FindWineserver` 的 PATH 兜底让「缺 wineserver」判定失效 | `ComponentStore.cs:487,517` | 受管组件只在同目录找，PATH 兜底只用于系统/手动添加 |
| R101 | 把 `mangohud` 当必需二进制（实际只用 `MANGOHUD=1`） | `Enhancements.cs:231-234` | 改为检查 Vulkan layer manifest 是否存在 |
| R102 | `Sanitize` 把非 ASCII 非中日韩字符折成 `-`，无长度上限 | `ComponentManifest.cs:188-202` | 保留 Unicode 字母数字、截断 80 字符 + 短哈希防碰撞 |
| R103 | 源覆盖是整对象替换；UI 保存把内置源固化成用户副本，丢 `Removed` | `ComponentSources.cs:171-186`；`Components.cs:1084-1095` | 默认值打底 + 字段级覆盖，或只持久化差异字段 |
| R104 | `IsArchive` 不含 `.zip`；`PrefersZstd` 对 `.tar.gz` 也 true | `ComponentSources.cs:499-509` | `IsArchive` 复用 `ArchiveExtractor.IsSupported`；`PrefersZstd` 只对 `.tar.zst` 为 true（改测试） |
| R105 | 资产缓存只增不减、无限流头处理、5xx 不重试 | `ComponentSources.cs:290-292,421` | 加容量/过期淘汰；读 `X-RateLimit-*`；5xx 与网络错误做一次退避重试 |
| R106 | `OutputCapture.Redact` 替换的是关键词而非值，`token=abc` → `token=***=abc` | `OutputCapture.cs:36,65`（当前无调用者） | 正则改 `(?i)\b(token\|secret\|cookie\|auth(?:orization)?)\s*[=:]\s*\S+` → `$1=***` |
| R107 | 输出日志忽略 `XDG_DATA_HOME`；不设 `StandardOutputEncoding`；进程退出不 `WaitForExit` | `OutputCapture.cs:71-93` | 共用路径助手；显式 UTF-8；退出后 flush 尾部 |
| R108 | `WINEDEBUG=-all` 无条件写；`LD_LIBRARY_PATH` 整段替换（启动链）而 CLI 路径是前置 | `RuntimeEnvironment.cs:44,122`；`GameLauncherService.Linux.cs:425-441` | 仅在未指定时设默认；收敛一份「前置 + 保留旧值」实现 |
| R109 | env 键 `Ordinal` 且不校验字符集（`WINEPREFIX`/`wineprefix` 视为两个键） | `LaunchRecipe.cs:166-184` | 键按 `^[A-Za-z_][A-Za-z0-9_]*$` 校验，已知键大小写归一，值拒 `\0` |
| R110 | 游戏参数与配方参数先拼字符串再 `SplitArgs`，引号跨层 | `LaunchRecipe.cs:228` | 各自 `SplitArgs` 后 `Concat`，不回炉 |
| R111 | `LastKnownGood` 读-改-写无锁；默认前缀方案记 null；启动链同步 fork `wine --version` | `LastKnownGoodStore.cs:107-134,121` | 记 `EffectivePrefixPath`；写加文件锁或合并写；Remember 改后台 |
| R112 | 启动历史追加 + 压缩无跨进程锁；`Culture` 敏感格式化（de-DE 得 `1,5 s`，th-TH 佛历年份） | `LaunchHistoryStore.cs:108-109,62`；`LastKnownGoodStore.cs:167` | 追加友好格式；`InvariantCulture` 格式化 |
| R113 | 下载表「已安装」判定用双向模糊匹配：`candidate.Contains(version) \|\| version.Contains(candidate)`，装了 10.0 时 1.0 也命中 | `Components.cs:536-537` | 规范化后精确比较（`OrdinalIgnoreCase`），或只允许「前缀 + 段边界」匹配 |
| R114 | `BindingCount` 计空字符串值 | `ProfileBinding.cs:146` | `Count(x => !string.IsNullOrWhiteSpace(x.Value))` |
| R115 | `Delete` 对不存在文件返回 true | `RuntimeProfileStore.cs:216-232` | 返回 false + 日志 |
| R116 | 默认运行器写失败只 `Log.Warning`，无 `SchemaVersion` | `RuntimeDefaults.cs:38-55` | 写失败 toast；加版本字段 |
| R117 | 文案/字号硬编码：`FormRow("Mirror", …)`、`"、".Join`、内联 `FontSize` 10 处、`SetItemStatus(..., "Selected")` | `Components.cs:900,566,712,846`；`xaml.cs:370`；`xaml` 10 处 | 补资源键 `BuildMirror/ListSeparator/TabSelected`；字号进样式 |

### 6.9 Low / 文档漂移

- **R118**　死资源：`Lunarward_RuntimeEnv_*` 在 zh-CN resx 约 50 个键无代码/XAML 引用（`AddPath`、`NewProfileInline`、`AddComponents`、`GraphicsEdit`、`MirrorSpeed`、`PrefixShare` 等）；代码引用的键均有定义。建议清理或标注保留原因。
- **R119**　死代码：`ParseWrappers`（`xaml.cs:1736`）、`ParseEnv`（`:1755`）、字段 `_editorWrappers`（`:1104`）、`_enhanceProfiles`（`Enhancements.cs:22`）全仓无引用；`Button_CloneForGame_Click`（`:2567`）、`Button_ImportArchive_Click`（`Components.cs:549`）、`Button_Sources_Click`（`:639`）、`Button_SourceSpeed_Click`（`:1102`）无 XAML/代码订阅（连带 `ShowImportArchiveAsync:570-597` 的类型选择分支永不执行）；`xaml.cs:1333-1336` 是空 `if` 注释块。
- **R120**　注释漂移：`xaml.cs:329`（「0 概览/1 运行器/2 组件/3 前缀/4 增强」）与 `:355`（「页签只剩四个」）都与实际三个页签不符；`PrefixManager.cs:1-12` 文件头仍列已删除模式；`PeArchitecture.cs:75-79` 两个 `<summary>` 挂在同一方法（`NormalizePrefixArch` 无文档）；`ResolvePrefixPath`（`GraphicsInstaller.cs:416-423`）无调用者且语义与 `EffectivePrefixPath` 冲突。
- **R121**　`PrefixManager.TryGetExistingPrefix(profile, gameKey)` 的 `gameKey` 参数完全没用（`PrefixManager.cs:351-355`），文档与实现不符——也是 R50 的根因之一。
- **R122**　`PrefixManager.WriteMetadata` 早退（`:471-489`）导致 `PrefixArch` 改了也不更新侧写；诊断包（`DiagnosticBundle.cs:158`）会带出旧值。
- **R123**　`IsUnderPrefixRoot` 纯字符串前缀比较（`PrefixManager.cs:236-248`）；`ClonePrefix` 在目标无目录名时 `CreateDirectory("")` 抛 `ArgumentException`，且 `WaitForExit` 无超时（`:359-389`）；`:310` 的日志文案与条件不匹配。
- **R124**　`FindExecutable`（`ProfileShellService.cs:117-140`）与 `FindInPath`（`Enhancements.cs:240-260`）都不检查可执行位 → 预检说 Ok、启动 `Win32Exception`。
- **R125**　`RunnerNaming`：`/usr/local/bin/wine` 被当系统 Wine（`:21-27`）；`Origin` 只认 `~/.local/share` 与 `/opt`（`:40-62`），`/run/media`、Flatpak、`~/Games` 下重名不可区分。
- **R126**　`SteamLibrary`：非 `/` 路径静默丢弃（`:83-87`，双系统 NTFS 值无日志）；VDF 库路径不校验存在性（`:101-146`）；`IsProtonFolder` 第二条件冗余（`:164-169`）。
- **R127**　`PeArchitecture`：只读 `system.reg` 前 32 行找 `#arch=`（`:80-108`），探测失败一律当 win64；ARM64 + win32 前缀不报冲突（`:128-143`）。
- **R128**　`CopyProfile`（`xaml.cs:2459-2482`）未复制 `LegacyGraphicsId`（若为有意请注明）；保存后 `AutoCheckRunnerAsync` 每次重跑无「已自检」记忆；两条 toast 同时弹（`:1716`/`:2146`）。
- **R129**　`Profiles.cs:404/840`、`xaml:349` 指定 Linux 不存在的 `Consolas`/`"Consolas, monospace"`，与 `xaml.cs:441` 注释自相矛盾；`Profiles.cs:285` 用 `"\uE712"` + `Segoe MDL2 Assets` 画 ⋯。
- **R130**　无障碍：页签、`✕` 删除按钮、健康状态点（8px `Ellipse` + ToolTip）都无 `AutomationProperties.Name`；`MenuFlyoutItem { Content = "·  ·  ·", IsEnabled = false }` 当分隔线会被读屏播报。

### 6.10 横切建议

1. **抽 `LunarwardPaths`**：XDG 路径现在 4 份实现（`RuntimeProfileStore.cs:40-49`、`ProfileBinding.cs:39-48`、`RuntimeDefaults.cs:25-34`、`PrefixManager`），先收这个「唯一真源」再修具体 bug。
2. **「扫描一次，传快照」**：方案列表、运行器占用、前缀使用共用一个 `RuntimeSnapshot`（一次 `LoadAll` + 一次 `GetRunners/GetGraphics`），消掉 R79/R80/R95 一类重复读盘。
3. **统一弹窗入口** `TryShowDialogAsync`：一把锁 + `finally` 复位，替换 22 处裸 `ShowAsync()`（R86/R87）。
4. **前缀目录与方案名解耦**（R44）单独一个提交先落，属于数据安全项。
5. **补三类 L1 测试**：解压权限位（tar.xz/zip）、`{"env":null}` 反序列化、前缀「先锁后建」顺序——都能写成纯逻辑测试。

### 6.11 建议修复批次（供拍板）

| 批次 | 条目 | 主题 | 理由 |
| --- | --- | --- | --- |
| E | R43 R44 R45 R64 | 数据安全 | 都是「静默丢东西」，用户不报也难查 |
| F | R46–R56 | 启动链路 | 直接影响能不能启动、会不会污染 `~/.wine` |
| G | R57–R63 | 下载与导入 | 影响主流程成功率与磁盘安全 |
| H | R65–R76 | 方案与绑定 | 丢配置、绑定悬空、默认方案漂移 |
| I | R77–R85 R93 | UI 线程与资源 | 卡顿/崩溃类投诉，含 4 处同步 fork wine |
| J | R86–R92 R94–R97 | 交互与状态 | 连点崩溃、chip 跳错、状态自相矛盾 |
| K | R98–R130 | 一致性 / 文案 / 无障碍 / 死代码 | 低风险，可顺手做 |

每批流程不变：改前在本节标 📝，改完标 🛠 + 提交号；`dotnet build Starward.slnx`（Debug）→ `dotnet test` → `lint-ui-thread.sh`（动了 UI 事件处理器时）→ 真机过一遍受影响的页面 → 更新 `13-lessons.md`（新坑）与 `CHANGELOG.md`。

### 6.12 证据与测量记录（2026-11-24）

本节是初稿中标「推断」处的取证结果，以及把定性描述换成实测数字。环境：本机 eDP-1 1536×864 @1.25，Debug 构建，`--list-components` 走同一套 `ComponentStore`。

| 结论 | 证据 | 取证方式 |
| --- | --- | --- |
| R55 wineboot 不套 Proton 环境 | 代码只设 4 个变量（`PrefixManager.cs:418-427`）；**实测用同样变量建前缀成功**：11.5 s、650 MB | 用 `env -i HOME=… WINEPREFIX=… WINEDEBUG=-all WINEARCH=win64 WINEDLLOVERRIDES=mscoree,mshtml= <proton>/files/bin/wine wineboot -u` 跑一遍（Proton cachyos-11.0） |
| R55 结论订正 | 「建前缀失败」不成立；真实缺陷是环境不一致 | 同上，进程退出码 0、`drive_c` 存在 |
| R61 digest 非 sha256 | 代码只剥 `sha256:` 前缀，其它算法原串进 `Sha256` 再与 `sha256=` 比较 | 读 `ComponentSources.cs:389-393`、`ComponentDownloader.cs:149`；触发取决于源端（未联网验证） |
| R62 fileName 未校验 | 全仓下载路径无 `GetFileName`；`Path.Combine(targetDirectory, fileName)` 直接拼接 | `grep -n "GetFileName" src/Lunarward/Features/Runtime/ComponentDownloader.cs` → 0 |
| R77 不阻塞死锁 | `ReadRunnerVersionAsync` 全部 `await … ConfigureAwait(false)`；实测 wine `--version` 12 ms（冷/热） | 代码 + `wineserver -k` 后计时 |
| R78 页面首次加载量级 | 启动 0.1 s + 扫描 0.6 s（13 组件，冷热同量级） | `time Lunarward --list-components` |
| R81 页面 Visibility 从不赋值 | 三个 partial 文件中无 `this.Visibility =`；只有子面板赋值 | `grep -n "\.Visibility = " RuntimeEnvironmentSetting*.cs`（人工过滤） |
| R81 无卸载清理 | 页面无 `OnUnloaded` 重写；`PageBase.cs:76-85` 为空 | 读代码 |
| R87 弹窗不可叠 | 代码注释 `xaml.cs:31-33` 记录了真机观察（第二次 `ShowAsync` 抛） | 读代码注释 |
| R94 归一化不一致 | `:2084` 原文比较 vs `:741/:1000/:1487` 先归一 | 读代码 |
| R95 目录大小成本 | 20 000 文件 `du -sh --apparent-size` 30 ms；按此 200 000 ≈ 0.3 s，不是「30–90 s」 | `du` 计时（stat 级，.NET `FileInfo` 同阶） |
| R84/R85 并发 | 静态缓存与 `Task.WhenAll` 无信号量，属代码结构事实 | 读代码 |

仍属推断（无法离线取证）：R61 的「源端换算法」、R62 的「服务端给恶意资产名」、Uno 下第二个 ContentDialog 的确切表现（抛或静默吞输入）。这三条已在正文限定为「外部条件」或「未实测」。
