# 28 · 「运行环境」模块审查报告

> 日期：2026-11-23　范围：`src/Lunarward/Features/Runtime/`（25 文件）、`Features/Setting/RuntimeEnvironmentSetting.*`（4 个 partial，4040 行）、`Features/GameLauncher/LaunchPreflight.cs`、`GameLauncherService(.Linux).cs` 的启动链。
> 方法：本人读码 + 4 组只读子代理（页面 / 启动 / 存储 / 组件），结论逐条回到源码核对。
> 状态：2026-11-23 用户 m02913「全处理了」→ 按批修复，进度见各条 `- 🛠` 行；决策 D104（批 A）起。
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
- 位置：`ComponentDownloader.cs:97`、`:82-92`；调用方 `RuntimeEnvironmentSetting.Components.cs:196` vs `Program.Linux.cs:1551`。
- 证据：`string actual = ApplyMirror(url, null);` 把 mirrorPrefix 写死 null，`DownloadAsync` 也没有该参数；页面不套镜像，只有 CLI 调 `ApplyMirror(..., source.MirrorPrefix)`。
- 建议：`DownloadAsync` 增加 `mirrorPrefix` 参数并在内部统一处理；补一条「页面下载走镜像」的 L1 测试。

### R13　组件扫描不用 `manifest.Root`，root≠"." 的组件导入后消失 ✔子代理核对
- 位置：`ComponentStore.cs:232`；`ComponentImport` 写 `Root`（`ComponentImporter.cs:78`、`:97`）。
- 证据：`ComponentLayouts.WinePath(dir, manifest?.Layout ?? "")` 把组件目录当 root；全仓没有读取 `manifest.Root` 的地方。
- 建议：`WinePath(Path.Combine(dir, manifest?.Root ?? "."), layout)`；补「归档内 wine 在子目录」的导入→扫描测试。

### R14　单流续传不校验 206，服务器忽略 Range 时把全文追加到半截文件 ✔子代理核对
- 位置：`ComponentDownloader.cs:395-410`。
- 证据：`resumed = have>0 && total>0 && have<total`；只要 `have>0` 就以 `FileMode.Append` 打开，`:399-401` 只 `EnsureSuccessStatusCode()`，不检查 `PartialContent`；total 为 null 时同样追加。
- 建议：追加前要求 `StatusCode == PartialContent`，否则 `FileMode.Create` 重下并删旧文件。

### R15　`ComponentHealth` 的 15 s 超时只写进返回值，从不 Kill 进程 ✔子代理核对
- 位置：`ComponentHealth.cs:99-129`。
- 证据：`CancelAfter(15s)` + `WaitForExitAsync(token)`，catch 里只 append 文本；stdout/stderr 用 `CancellationToken.None` 读。
- 影响：wine 卡住后进程常驻；无法区分「超时」与「用户取消」。
- 建议：超时分支 `Kill(entireProcessTree:true)`，按 `ct.IsCancellationRequested` 区分。

### R16　`ComponentStore` 的 `wine --version` 4 s 超时形同虚设 ✔子代理核对
- 位置：`ComponentStore.cs:390-400`。
- 证据：先 `ReadToEnd()` 再 `WaitForExit(4000)`（返回值被忽略）；stderr 重定向但不读；`GetRunners` 对每个运行器都跑一次，且处于保存后的 UI 线程链路上。
- 建议：`WaitForExitAsync` + `CancelAfter`，两路流并发读，超时 Kill；运行器版本结果加真实缓存。

### R17　图形层不识别 DXVK 的 `x32/` 目录，32 位 dll 永不安装 ✔已核对
- 位置：`GraphicsInstaller.cs:362-383`、`:334-372`。
- 证据：只判断 `x64/` 与 `x86/`；DXVK 2.x 发布物是 `x64/ + x32/`（VKD3D-Proton 才是 `x86/`）。另外目录选择靠 `target.EndsWith("32.dll") || target.StartsWith("x86")`（:170），名字里恰好含 32 的 64 位 dll 会落进 syswow64。
- 建议：`x86`/`x32` 同时识别，按来源目录而不是文件名决定目标。

### R18　gamescope/gamemoderun 无条件拼进 argv，缺程序时启动直接失败 ✔子代理核对
- 位置：`Enhancements.cs:97-101`；`LaunchPreflight.cs:364-375`；`GameLauncherService.Linux.cs:335`。
- 证据：wrappers 直接 `"gamescope"`/`"gamemoderun"`，不 `FindInPath`；预检只给 Warning；启动时 `Process.Start` 抛 Win32Exception（并把 R05 的锁一起泄漏）。
- 建议：找不到的 wrapper 跳过并给可见提示，或预检判 Failed 并带「打开运行环境」。

### R19　「Shell / RunOnce」与真实启动不是同一套环境 ✔子代理核对
- 位置：`RunOnceService.cs:64`；`ProfileShellService.cs:89-116`。
- 证据：只拼 `profile.Env`，不应用 `recipe.Environment`；`PrefixPath` 为空的方案直接没有 `WINEPREFIX`（落进 `~/.wine`），也没有 Proton 的 `WINEDLLPATH`/`LD_LIBRARY_PATH`/`PROTONPATH` 与 HUD；全程不取前缀锁。两个文件的开头都写着「环境变量与游戏启动完全一致」。
- 建议：抽出唯一的环境装配函数给「启动 / Shell / RunOnce / 伴随启动」共用，锁作为参数传入。

---

## 3 · Medium

- **R20　前缀体积在 UI 线程扫盘**：`RuntimeEnvironmentSetting.xaml.cs:545-567 LoadPrefixes()` 同步调 `PrefixManager.EnumerateKnownPrefixes(profiles)`（默认 `computeSize:true`，递归 stat），ctor `:41` 与保存/删除/克隆都会走到；同页 `RefreshChipsAsync`、占用汇总都已后台。设计 §3.6 明确「后台算、10 分钟缓存、不放列表卡片」。另有 `Profiles.cs:41/:210` 每方案 `Inspect(computeSize:true)`，同一前缀被算三遍。建议：列表 `computeSize:false`，选中时后台算。
- **R21　保存后 `ComponentStore.Invalidate()` → `LoadAll` → 每个运行器 `wine --version` 冷 fork，全在 UI 线程**（`xaml.cs:1336`；同类 `:39/:51/:531/:548/:1915-1918`）。违反硬规则 10。建议：`LoadAll` 只展开路径、页面 `await Task.Run(...)`；版本结果真缓存。
- **R22　删除配方勾「连前缀一起删」会连坐共用前缀**（`xaml.cs:2291-2346`、`store #5`）：只查绑定用量，不查其它方案是否指向同一路径；克隆默认共享前缀。建议：删前统计同路径方案数，>1 时列出并禁止一键删。
- **R23　`ContentDialog.ShowAsync` 未防重入**（`xaml.cs:298/:693/:1818`）：连点两个检查类按钮时第二次抛 `InvalidOperationException`（async void 未处理）。建议：加 `_dialogOpen` 守卫 + try。
- **R24　删除最后一份方案又被自动重建**（`RuntimeProfileStore.cs:95-99`）：`LoadAll` 见 0 份就 `CreateDefault()`，「删了又回来」，页面空列表分支不可达。建议：只在首次启动创建。
- **R25　`LastKnownGoodStore` 只写不读**（写：`GameLauncherService.cs:493`；读：仅 `DiagnosticBundle`）：设计 §6「上次成功：11-14 21:03 / 用上次能跑的组合重新启动」未实现；且条目缺 `runnerId`/`graphicsId`，`PrefixPath` 记的是未展开值。建议：补字段与消费方。
- **R26　预检「去设置」不带 profileId/tab**：页面无 `OnNavigatedTo`，永远选第一份方案 → §13.10「跳到 运行环境 → 增强」未实现。
- **R27　编辑器预览的 draft 不含 Enhancements**（`xaml.cs:1198-1204`）：预览看不到 gamemode/mangohud；名字 `TextChanged` 也不刷新预览与前缀行（`:1060-1064`）。
- **R28　保存/删除/克隆后不刷新 chips 与失效绑定提示**（`:1336/:2258/:2347`；`RefreshChipsAsync` 只在 ctor 调）。
- **R29　前缀路径保存零校验**（`:1315-1326`）：`/`、相对路径、`..`、不可写目录都收，错误延后到 `EnsurePrefix`。
- **R30　`IsPrefixLocked` 把「没权限」也报成「被另一个实例使用」**（`PrefixManager.cs:67-75`；`LaunchPreflight.cs:232-245`）：动作是「查看占用进程」，权限问题下是死胡同。
- **R31　组件分片失败被吞、MergeParts 以目录文件数当片数、缺片静默跳过、失败后不清理分片**（`ComponentDownloader.cs:256-270/:328/:441-456`）：一次 403/429 最终以「长度不一致」暴露，且重试必复现。建议：连续失败计数、任一片失败即抛并清理；合并上界用 `ChunkCount`，缺片立即抛。
- **R32　`ArchiveExtractor` 三处边界**：符号链接校验少尾部分隔符（`:257-262` 与 `:234` 不一致）；绝对路径条目被静默改写成相对路径（`:228`，注释却说拒绝）；`tar.xz` 分支无总大小上限与逐块取消（`:171`，xz 炸弹可无限膨胀，页面 df 预检只按压缩包×2 估算）。
- **R33　组件导入语义不一致**（`ComponentImporter.cs:49-52` vs `Program.Linux.cs:1580-1583`：页面算失败、CLI 算跳过；且先 `Directory.Move` 再写 manifest，中间失败留下不可再导入的目录）。建议：统一语义 + 先写临时目录再整体 rename。
- **R34　下载完整性标注失真**（`ComponentDownloader.cs:140-142/:141`；`ComponentImporter.cs:107-109`）：拿不到源端 digest 时不做校验却返回 `DigestMatched=true`；`FromApi = !string.IsNullOrEmpty(sourceUrl)` 把本地自算哈希标成「源端提供」。建议：加参数区分期望值与自算值；未校验时 `DigestMatched=false` 并提示。

---

## 4 · Low / 文档漂移

- **R35　`ComponentNaming.Sanitize` 不移除 `.`/`..`**（`ComponentManifest.cs:188-195`）；`EnvironmentResolver.EffectivePrefixPath`（`:95-103`）直接 `Path.Combine` → 方案名 `..` 把前缀算到 `~/.local/share/lunarward`，`.` 算到 PrefixRoot。建议：名称白名单化（`^[A-Za-z0-9_-]{1,64}$`）+ 拼完校验。
- **R36　`LaunchRecipeBuilder.SplitArgs` 对引号外的 `\` 做转义**（`LaunchRecipe.cs:62-117`）：`C:\Games\...` 这类参数会被吃掉反斜杠。建议：引号外不处理反斜杠（与 `ArgumentList` 语义一致）。
- **R37　`GraphicsInstaller` 两处一致性**：`EnsureInstalled` 的「同 Id 跳过」不看文件大小（`:141-150`，`Verify` 却校验 size）；`Remove(kind)` 在文件删除失败时仍删记录（`:300-330`）→ 无主 dll。
- **R38　`--launch-plan` 与真实启动不同源**（`GameLauncherService.Linux.cs:434-452`）：plan 用 `ResolveRunner` + 原始 arg，真实启动用 `profile.RunnerPath` + effectiveArgs，两者命令行必然不同（叠加 R02）。建议：plan 复用 `CreateGameProcessStartInfo`，只打印不启动。
- **R39　第二实例深链启动的 Task 被丢弃**（`App.xaml.cs:168`）：`Task ignoredLaunch = …StartGameAsync(gameId);` 无 await/catch，预检失败与启动异常无 UI 反馈。
- **R40　文档 / 注释漂移**：默认前缀代码是 `~/.local/share/lunarward/prefix`，设计 §4.1/§8 写 `prefixes/`；`PrefixManager.cs:1-12` 文件头仍列 D73 已删除的五种模式；`ProfileBinding` 文件名是 `profile-bindings.json`，设计 §8 写 `env-bindings.json`（且无迁移）。
- **R41　旧 `graphicsId` 读入即丢、`RunnerPath` 缓存字段被持久化**（`RuntimeProfile.cs:100-103`、`store #14`）：升级用户不会收到「图形层需重选」提示；导出配置会带上别处绝对路径。
- **R42　`LastKnownGoodStore.Load` 出错后用空表覆盖**（`LastKnownGoodStore.cs:58-77/:106-107`）；`LaunchHistoryStore` 截断用非原子 `WriteAllLines`、并发追加抛 IOException 被吞（`LaunchHistoryStore.cs:167-175/:107`）。

---

## 5 · 建议修复批次（供拍板）

1. **批 A（启动链正确性，Critical）**：R01 R02 R03 R04 R05 R06 —— 改动集中在 `GameLauncherService.Linux.cs`、`LaunchRecipe.cs`、`PrefixManager.cs`、`RuntimeProfileStore.cs`。
2. **批 B（持久化安全）**：R07 R08 R09 R10 R11 —— `JsonFile.cs`、`RuntimeProfileStore.cs`、`ProfileBinding.cs` + 迁移恢复。
3. **批 C（组件层）**：R12 R13 R14 R15 R16 R31 R32 R33 R34 —— `Component*`、`ArchiveExtractor.cs`。
4. **批 D（页面体验与一致性）**：R20–R30、R35、R38–R42。

每批仍然：`dotnet build Starward.slnx`（Debug+Release）→ `dotnet test` → `lint-ui-thread.sh` → 逐条补 26/27 状态与证据 → 单独提交。
