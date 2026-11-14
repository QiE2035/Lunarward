# 16 · 运行组件模块（V2 · 极简）

> 状态：**v2 已定稿并开始实施**（2026-11-11，用户拍板 m15086「先按这个来吧，先做出来偷体验一下」）。
> ⚠️ **已被 `23-runtime-env-design.md`（v3，D73，2026-11-14）取代**：本文保留作历史记录，概念与页面结构以 23 号为准。

> **v1（依赖解析 + 锁文件 + 可达性 GC + 组件市场）已撤回**：那套设计按「频繁试组合」优化，与真实使用方式不符——组合一旦定下就长期不动，变动来源是 pacman/AUR。
> 相关决议：D58（本文）、D1/D2（单头 Linux）、D30（兼容层边界）、D39（并发与占用）、D52（启动链路缺陷）、D35（开发入口不进发行版）。
> 现状实现：`Features/Runtime/{RuntimeProfile,RuntimeProfileStore,PrefixManager,ComponentStore,GraphicsInstaller,ProfileBinding,LastKnownGoodStore}.cs`、`Features/GameLauncher/{LaunchPreflight,GameLauncherService}.cs`。

---

## 0 · 一页摘要

这个模块只回答三个问题：

1. **我这儿有哪些能用的运行器？**（哪个 wine、哪个版本、在哪、能不能跑）
2. **这个游戏现在用的是哪一个？**（配着哪个前缀、哪个 DXVK）
3. **出问题我怎么换、怎么退回去？**

它不是「组件仓库管理器」。凡是这三问用不上的（锁文件、漂移检测、组件市场、内容寻址仓库、可达性 GC），**先不做**。

职责边界：**管清单与选择，不管安装**。wine 装不装、装哪个版本，是 pacman/AUR 的事；我们只负责「发现它们、说清它们能不能用、让用户选一个、并且能换回来」。

---

## 1 · 数据三层

| 层 | 是什么 | 存哪 |
| --- | --- | --- |
| 运行器（runner） | 一个可执行体 + 事实（路径、版本、布局、能否用） | 扫描得到，不落盘（每次扫描重算，`ComponentStore` 内部缓存） |
| 方案（profile） | 选哪个运行器 + 哪个前缀 + 少量 env + 要不要 DXVK | `~/.config/lunarward/profiles/<id>.json` |
| 前缀（prefix） | 一个真实目录，属于某个方案 | 由前缀模式决定（模板克隆 / 独占 / 共享 / 外部 / 临时） |

**方案引用运行器，前缀属于方案。** 不做「组件仓 → 引用 → 锁文件」那一层。

---

## 2 · 七个动作（按使用频率排序）

1. **扫描**：列出现有运行器（PATH、`/opt`、`/usr/share/steam`、`~/.local/share`、Steam `compatibilitytools.d`、各启动器 runners）。缓存结果，`重新扫描` 按钮强制刷新。
2. **选择**：在方案里选运行器。选中即做**无副作用**校验（见 §4）。
3. **建前缀**：从空目录新建、从模板克隆、或指向已有目录（external）。
4. **启动**：把方案翻译成环境变量；启动前预检（运行器存在、前缀像前缀、未被占用、图形组件可用）。
5. **切换**：换运行器 → 界面明确提示「现有前缀会在下次启动时走一次 `wineboot -u`」，不偷偷换。
6. **回退**：每次启动成功都记下「上次能用的组合」（运行器路径 + 版本 + 前缀 + 时间），方案编辑器里一键切回。
7. **清理**：只删**我们自己创建**的前缀（带确认），不碰 external 目录。

---

## 3 · 界面只有三处

| 位置 | 内容 |
| --- | --- |
| 设置 → 运行环境 → 组件 | 运行器列表（名称/版本/布局/可用性 + 原因）、图形组件、`重新扫描`、扫描根 |
| 设置 → 运行环境 → 方案 | 名称、运行器（下拉或手填）、前缀模式与路径、env、调试 Shell、图形组件、「上次可用 + 切回」 |
| 游戏设置 → 运行 | 该游戏用哪个方案、预检结果、诊断按钮 |

不做：组件市场页、仓库页、锁文件可视化、依赖图。

---

## 4 · 可用性判定（扫描阶段，无副作用）

| 检查 | 失败时的提示 |
| --- | --- |
| 可执行文件存在 | 找不到可执行文件 |
| 有执行位（`File.GetUnixFileMode`） | 没有可执行权限 |
| `wine --version` 有输出 | `wine --version` 没有输出（可能缺库或无法执行） |
| 找到**同套** `wineserver`（同目录，或 PATH） | 找不到配套的 wineserver（游玩时长与前缀锁会不准） |

**扫描阶段不创建、不修改任何前缀**（`wine cmd /c exit` 在没有前缀时会顺手建一个，所以不做）。真正的可用性由首次启动验证——启动成功即写入「上次可用」。

---

## 5 · 四个必须做对的现实细节

1. **两种布局**：`bin/wine` 与 `files/bin/wine`（Proton 形态），后者要补库路径（`WINEDLLPATH` / `LD_LIBRARY_PATH`）。
2. **`wineserver` 必须同套**：否则 `wineserver -w` 等错进程，游玩时长与前缀锁会失真（L-93）。
3. **`WINEPREFIX` 末尾斜杠会让 wine 直接退出**（`launching from steam.exe stub!`，L-92）——统一 `TrimEnd('/')`。
4. **换 DXVK 用 `WINEDLLPATH` 叠加**，不把 dll 拷贝进前缀——前缀不动，随时能换回来（逐步落地）。

---

## 6 · 明确不做（v1 撤回项）

- **锁文件 / 漂移检测 / 哈希校验**：组合长期固定，漂移源是包管理器，检测出来也做不了什么。只在列表里显示当前版本。
- **组件市场 / 多 provider 下载**：真实需求很少，将来若做，做成「固定某个版本」这一个动作即可。
- **可达性 GC / 内容寻址仓库**：只有我们创建的前缀需要清理，按目录 + 确认足够。
- **自动安装/切换 DXVK 到前缀**：先把 `WINEDLLPATH` 路径打通再谈。

---

## 7 · 本轮已实现（2026-11-11）

- `ComponentStore`：`Component` 增加 `Layout`（`bin` / `files-bin`）、`Usable`、`UsabilityReason`；新增 `DetectLayout`、`FindWineserver`、`EvaluateRunner`；`GetRunners` 扫描后逐条判定。
- 组件列表行显示「可用 / 原因（布局）」。
- 方案编辑器：运行器下方固定提示「换运行器会触发一次 `wineboot -u`」；有「上次可用」时显示摘要 + `切回` 按钮。
- 启动成功后写 `~/.config/lunarward/last-good.json`（`LastKnownGoodStore`，原子写）。
- CLI：`--list-runners [--json]`，一次看清扫描根、版本、布局、可用性与 wineserver 位置。

实测（开发机）：

```
  ✓ 系统 Wine      wine-11.19-1-g78e32ba1d3d (Staging)  bin       /opt/spritz-wine-tkg/bin/wine
  ✓ proton-cachyos-slr  wine-11.0 (CachyOS)             files/bin /usr/share/steam/compatibilitytools.d/proton-cachyos-slr/files/bin/wine
  ✓ spritz-wine-tkg-staging-wow64-11.19-1  …            bin       ~/.local/share/sleepy-launcher/runners/…/bin/wine
```

---

## 8 · 后续（有反馈再说）

1. `WINEDLLPATH` 叠加（DXVK 不入前缀）。
2. 游戏设置页显示「本游戏实际会用的运行器 + 预检结果」。
3. 「固定版本」动作：把某个系统 wine 复制进 `components/wine/<版本>/` 并让方案引用它（防 pacman 升级打坏环境）。
4. 前缀健康检查（`drive_c` / `system.reg` 缺失提示已在预检里）。
