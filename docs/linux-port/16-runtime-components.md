# 16 · 运行组件与运行环境模型（设计稿，v1）

> 状态：**设计稿，未实施**。本文件只描述目标形态与迁移路径，不构成开工授权（见 `README.md` 的「施工授权」）。
> 相关决议：D58（本文）、D1/D2（单头 Linux）、D30（兼容层边界）、D39（并发与占用）、D44（健壮性加固）、D52（启动链路缺陷）。
> 相关现状文档：`03-runtime-model.md`（v0.1 数据模型）、`09-profile-and-companion-ui.md`（列表与字段）。

---

## 0 · 一页摘要

组件管理不是「一个列表 + 几个下拉框」，它要回答四个问题：**用什么跑**（runner）、**怎么渲染**（graphics）、
**住哪儿**（prefix）、**怎么起**（env）。前三个是依赖，第四个是推导结果。所以它的正确身份是
**依赖解析 + 环境构建 + 生命周期管理**，和包管理器做的是同类工作。

本设计的四条主张：

1. **组件要有强身份**：`(kind, 名字, 版本, 内容哈希, 来源, 能力集)`。路径会变、名字会重、版本号会说谎，只有哈希能回答「是不是同一份」。
2. **默认不重复存**：保留策略分 `reference`（零额外空间，只记身份 + 配方）/ `hardlink`（同分区零额外空间，留住旧 inode）/ `copy`（跨分区或用户强制）。**不得假定 btrfs / reflink**。
3. **方案 = 声明 + 锁**：`profile.json` 表达意图（可跟随），`profile.lock.json` 表达事实（钉死到哈希）。解析一次，结果可审计。
4. **回收以可达性为准绳**（pnpm 式）：从四类根（方案锁 / 前缀 manifest / 运行租约 / `keep`）做标记-清扫，**可达即保留**，删除只由 `--gc` 显式触发并需确认；LRU 只用于纯缓存。

一句话：**它不是一个设置页，而是一个小型依赖管理器，恰好管理 wine 生态。**

---

## 1 · 问题与定位

### 1.1 现状

| 部件 | 现在是什么 | 不足 |
|---|---|---|
| `Features/Runtime/RuntimeProfile.cs` | runner + graphics + prefix + env 四合一的对象，直接存最终值 | 无法表达复用/继承；无法解释「为什么用这个」；无版本锁定 |
| `PrefixManager` | 5 种模式（template/dedicated/shared/external/ephemeral）+ `.lunarward/prefix.json` | 前缀里装过什么（winetricks/reg/dll override）不可知、不可重建 |
| `ComponentStore` | 扫几个已知路径凑出列表；`Component` = `Kind/Id/Name/Version/Path/Managed` | 无哈希、无来源、无能力，无法校验也无法判断「是不是同一份」 |
| `GraphicsInstaller` | 把 DXVK/VKD3D 的 dll 复制进前缀 `system32/syswow64` | 换版本必须动前缀；回滚依赖记忆 |
| `LaunchPreflight` | 6 项独立检查 | 不是「锁 vs 现实」的统一对比，解释力弱 |
| `GameLauncherService.Linux` | 算一组 env → `wine <exe>` | env 来源不可解释，排查只能读代码 |

### 1.2 目标（按价值排序）

1. 任何时刻都能回答：**这个游戏现在用的是哪一份 wine/DXVK/前缀，它变了吗。**
2. 外部变化（pacman/AUR 升级、用户手工改动、目录被删）**在启动前**暴露，而不是进游戏黑屏。
3. 换版本、回滚、复用不需要重建前缀。
4. 所有「拿进来的东西」都说得清来源与许可证。
5. 空间可回收，且**删错了不会毁掉别人正在用的东西**。

### 1.3 非目标（本期不做）

- 沙箱 / 容器化（umu 的 SteamLinuxRuntime 容器化运行时不在本期）
- 游戏本体版本管理、Steam 库管理
- 自动「修好一切」的魔法：出问题给**事实与选项**，不替用户决定

---

## 2 · 组件模型

### 2.1 分类

| 类别 `kind` | 例子 | 特点 |
|---|---|---|
| `runner` | wine 10.0、Proton 9.0、umu | 可执行，版本敏感，与 prefix 强耦合 |
| `graphics` | DXVK 3.1.1、VKD3D-Proton 2.14 | 纯 dll，可叠加，与 prefix 可解耦 |
| `tool` | winetricks、protontricks、winecfg | 辅助，可缺 |
| `runtime` | Steam Linux Runtime、mono/gecko | 被动依赖 |

### 2.2 身份（identity）

```jsonc
{
  "kind": "runner",
  "id": "spritz-wine-cachyos-wow64",   // 逻辑名，稳定但可重名
  "version": "10.0-11",                // 展示用，来自 wine --version 或包元数据
  "hash": "sha256:9f2c…",              // 强身份：关键文件集合的哈希
  "source": "aur:spritz-wine-cachyos-wow64",  // 来源坐标
  "path": "/opt/spritz-wine-tkg",      // 当前位置（可空：仅存的条目）
  "managed": false,                    // 是否在我们的仓里
  "capabilities": ["wow64", "esync", "fsync", "ntsync"],
  "size": 2147483648
}
```

**哈希怎么算**（不能对整个目录做，几百 MB 太贵）：

```
hash = sha256( 排序后的 [
  相对路径 + 文件大小 + 文件内容哈希的截断（对关键文件）
] )
```

关键文件清单按类别固定：

| 类别 | 关键文件 |
|---|---|
| runner | `bin/wine`、`bin/wine64`、`lib/wine/x86_64-unix/ntdll.so`、`files/bin/wine`（Proton）、`version`/`proton` 脚本 |
| graphics | `x64/d3d11.dll`、`x64/dxgi.dll`、`x32/d3d9.dll`、`x64/libvkd3d-proton.so` 等 4–8 个 |
| tool | 可执行文件本体 + `--version` 输出哈希 |

计算是**惰性**的：快路径先看「包元数据 / mtime / size」，变了才算哈希（详见 §4.3）。

### 2.3 能力集（capabilities）

能力集是给解析器用的，不是给人看的：

| 能力 | 探测方式 | 用途 |
|---|---|---|
| `wow64` / `wow64-stripped` | 存在 `lib/wine/i386-unix` 或 `syswow64` | 32 位游戏可用性 |
| `esync` / `fsync` / `ntsync` | `bin/wine --version` + 构建标记；`ntsync` 需内核支持 | 可开对应 env |
| `in-tree-dxvk` | runner 目录内有 `x64/d3d11.dll` | 不必再叠加 DXVK |
| `winetricks-ok` | `winetricks --version` 能跑 | tweaks 可用性 |
| `needs-sniper-runtime` | Proton 的 `compatibilitytool.vdf` / `toolmanifest.vdf` | 需要 Steam Linux Runtime |

解析阶段用能力集做校验：声明 `require: ["wow64"]` 而候选缺 `wow64` → **启动前**报错并列出候选差异。

---

## 3 · 发现层与仓层

### 3.1 两层分离

```
发现层（只读，扫）
  ├─ PATH 里的 wine
  ├─ /usr/share/steam/compatibilitytools.d/*、/usr/local/share/steam/…
  ├─ ~/.local/share/Steam/compatibilitytools.d/*
  ├─ /opt/*（特征探测）
  ├─ SteamLinuxRuntime / ~/.steam/root/…
  └─ 我们自己仓里的条目

仓层（可写，承诺）
  ~/.local/share/lunarward/components/
    index.json             id/version/来源/能力 → hash
    by-hash/<hash前12>/    条目实体（或硬链接树，见 §4.2）
    refs.json              可选缓存；**不作为真相**（见 §8.2）
```

规则：

- **发现层的东西只是候选**，不进仓，不参与「稳定性承诺」。
- 用户决定「我要固定用这个」时才在仓里建条目（策略见 §4）。
- 扫描要**特征探测**而不是固定路径白名单：`bin/wine`、`bin/wine64`、`files/bin/wine`、`proton` 脚本、`x64/d3d11.dll`；深度 ≤ 4，访问上限 20000（现有 D56 已这样做，保留）。
- 扫描结果带**来源标签**，UI 必须显示：`系统包（pacman 管理，升级会变）` / `Steam 工具` / `我们的仓` / `手动放置`。

### 3.2 去重

同 `hash` 即同一份：仓里只存一份，`index.json` 允许多个 `(id, version)` 映射到同一 hash。
「同一份 DXVK 被三个方案引用」在存储上只占一份。

---

## 4 · 保留策略：默认不复制

### 4.1 三档策略

| 策略 | 额外空间 | 机制 | 何时用 |
|---|---|---|---|
| `reference` | 0 | 只记 `(path, hash, source)` + 配方 | **默认**。系统包、Steam 工具、我们不想管的 |
| `hardlink` | ≈0（同 inode） | `cp -al` 建硬链接树进 `by-hash/<hash>/` | 同分区、想留住旧版本、不想占空间 |
| `copy` | 实际大小 | reflink 优先，否则普通复制 | 跨文件系统、或用户明确要求 |

### 4.2 为什么硬链接是对的机制

- **不需要 CoW**：ext4/xfs/btrfs 都支持，`du` 不计额外空间（同 inode）。
- **对包管理器升级的语义恰好有利**：pacman 升级包时是**替换文件（rename）**，不是原地改写 → 旧 inode 被我们的硬链接持有，**旧版本继续可用**。
- **破坏场景可控**：若某个包真的原地改写（罕见），§4.3 的校验会发现「这份已经不是当初那份」，直接报出来而不是静默用错。

跨文件系统（`/usr` 与 `$HOME` 不同分区）时硬链接不可用 → 降级为 `reference` + 配方；用户若坚持要留住旧版本，再显式选 `copy`（UI 显示预计占用）。

**前缀快照**同理，见 §6.4 的三档降级。

### 4.3 配方（recipe）：不复制也能「以后拿得到」

```json
{
  "source": "aur:spritz-wine-cachyos-wow64",
  "version": "10.0-11",
  "artifacts": [
    { "url": "https://…/spritz-wine-10.0-11-x86_64.pkg.tar.zst", "sha256": "…" },
    { "url": "https://mirror…/spritz-wine-10.0-11-x86_64.pkg.tar.zst", "sha256": "…" }
  ],
  "note": "AUR 老版本无法从官方仓库回取；冲这份 URL 或本地缓存"
}
```

- 任何策略下都记配方（`reference` 尤其依赖它）。
- 「以后拿得到」= 来源可寻址，而不是本地必有副本。这与 Nix 的 derivation、Homebrew 的 bottle 是同一思路。
- 配方缺失或全部 URL 失效 → 该条目进入 **`missing`** 状态（§8.3），启动前提示，而不是崩溃。

### 4.4 校验成本分层

| 层 | 触发 | 成本 |
|---|---|---|
| L0 元数据 | 每次启动 | 毫秒：`stat` 大小/mtime + 包元数据（见下） |
| L1 包查询 | 系统包 | `pacman -Qo <path>` 取「包名 + 版本」（毫秒级）；AUR 走 `pacman -Qm`/本地 `.SRCINFO` 缓存 |
| L2 关键文件哈希 | L0/L1 显示变化时 | 几 MB 级，几十毫秒 |
| L3 全量校验 | 用户点「校验」 | 秒级，UI 显示进度 |

结果回写 `profile.lock.json` 的 `verified_at`，下次先看它。

---

## 5 · 方案：声明 + 锁

### 5.1 两份文件

**声明**（人写，`~/.config/lunarward/profiles/<name>.json`）——表达意图，可以含糊：

```jsonc
{
  "name": "主力",
  "runner":   { "use": "wine", "prefer": "latest-stable", "require": ["wow64"] },
  "graphics": { "use": "dxvk", "version": "3.1.x", "inject": "overlay" },
  "prefix":   { "base": "hk4e-cn", "mode": "snapshot" },
  "env":      { "DXVK_HUD": "0" },
  "tweaks":   ["winetricks vcrun2022"]
}
```

**锁**（机器写，`<name>.lock.json`）——表达事实，必须精确：

```jsonc
{
  "resolved_at": "2026-11-11T12:00:00Z",
  "runner":   { "id": "wine-10.0-9f2c", "hash": "sha256:9f2c…",
                "path": "components/by-hash/9f2c…", "retain": "hardlink",
                "source": "managed(from aur:spritz-wine-cachyos-wow64#10.0-11)" },
  "graphics": { "id": "dxvk-3.1.1", "hash": "sha256:41ab…",
                "path": "/usr/share/dxvk", "retain": "reference", "inject": "overlay" },
  "prefix":   { "path": "…/prefix/hk4e-cn", "wine_at_create": "wine-10.0",
                "manifest": "…/hk4e-cn/manifest.json" },
  "verified_at": "2026-11-11T12:00:00Z",
  "health": { "last_ok": "2026-11-11T09:12:00Z", "same_lock_days": 7 }
}
```

**为什么必须两份**：声明可以跟随（`latest-stable`），锁必须钉死（`9f2c…`）；解析是一次可审计的动作；
同一份声明配不同的锁 = 两套可复现的环境，差异一眼可见。

### 5.2 解析规则（三条，按优先级）

1. **显式 > 继承 > 默认**（方案可以 `extends` 另一个方案，只写差异）
2. **能力校验**（`require` × `capabilities`，不满足即错，并给出候选差异）
3. **冲突检测**（不能选两个 graphics；`inject: inprefix` 与 `overlay` 冲突需明确；`prefix.base` 与 `prefix.mode=external` 冲突）

### 5.3 解析要留「解释」

```
runner: wine-10.0  ← 声明 prefer=latest-stable；候选 [10.0, 11.0]；
                      11.0 缺 wow64（声明 require:["wow64"]）→ 选 10.0
graphics: dxvk-3.1.1 ← 声明 version=3.1.x；匹配到唯一 3.1.1
prefix: hk4e-cn     ← 声明 base=hk4e-cn
```

解释结果写进锁的 `explain` 数组（人类可读字符串），UI 与 `--explain <profile>` 都能打印。
三个月后看一眼就知道「为什么没用最新的」——这句解释值千金。

---

## 6 · 前缀：从目录到可重建的产物

### 6.1 两个身份

前缀既是文件系统，也是**一堆不可逆操作的结果**（winetricks、reg、dll override、wineboot）。
所以前缀必须带 **manifest**：

```jsonc
{
  "base": "hk4e-cn",
  "created_with": { "runner": "wine-10.0-9f2c" },
  "operations": [
    { "do": "wineboot", "when": "2026-10-20T…" },
    { "do": "winetricks", "verbs": ["vcrun2022", "corefonts"] },
    { "do": "reg", "key": "HKCU\\Software\\miHoYo\\…", "value": "…" },
    { "do": "graphics", "mode": "inprefix", "dxvk": "3.1.1" },
    { "do": "wineboot_update", "from": "wine-10.0", "to": "wine-11.0" }
  ]
}
```

manifest 带来三件事：**重建**（base 丢了按顺序重放）、**校验**（与现状对比，发现手工改动）、**迁移**（换 base 时知道要重放什么）。

### 6.2 生命周期动作

| 动作 | 语义 |
|---|---|
| `create` | 从 base 建：wineboot + 重放 operations，写 manifest |
| `adopt` | 接管已有（含外部）前缀：只读扫描、**不写** manifest（避免污染别人的前缀） |
| `upgrade` | runner 大版本变化：先快照 → `wineboot -u` → 追加 manifest 记录 |
| `snapshot` / `restore` | 变更前拍，三档实现见 §6.4 |
| `clone` | 从 base 或快照派生（reflink 优先，退硬链接树） |
| `verify` | manifest vs 实际 |
| `discard` | 删除 + 释放占用 + 解除引用 |

### 6.3 与 graphics 解耦

- **`inject: overlay`（默认）**：`WINEDLLPATH` 叠加 + `WINEDLLOVERRIDES`，**前缀一个字节都不动**；换 DXVK 只改锁里的指针。
- **`inject: inprefix`**：复制 dll 进 `system32/syswow64`，必须记入 manifest（含版本），才能回滚；少数只认 in-prefix dll 的游戏/反作弊才用。

### 6.4 快照的三档降级

| 优先级 | 条件 | 机制 | 代价 |
|---|---|---|---|
| 1 | 前缀在 btrfs 子卷上 | `btrfs subvolume snapshot -r` | 0.1 秒、几 MB |
| 2 | 同分区、非 btrfs | 硬链接树（`cp -al`） | 快、≈0 空间；**注意**：若快照内的文件被原地改写会污染共享 inode，因此快照只用于「回滚」，且回滚前先 `verify` |
| 3 | 跨分区 / 需要绝对隔离 | `tar --zstd` 归档 | 慢、等于实际大小；UI 显示预计占用，用户确认 |

保留策略：每个前缀保留最近 3 张回滚点（不是 LRU 自动删「身份」，见 §8）。

---

## 7 · 启动：env 分层与可解释

### 7.1 分层合成

```
基础（wine 必要项）
  < runner 预设（esync/fsync、mono、dll override 基线）
  < graphics 注入（WINEDLLPATH / WINEDLLOVERRIDES）
  < prefix 相关（WINEPREFIX、驱动器映射、PROTONPATH 等）
  < 方案 env
  < 游戏/临时覆盖
  < CLI
```

每层带来源标签，`--print-env <biz>` 输出：

```
WINEPREFIX=…/prefix/hk4e-cn            ← prefix
WINEDLLPATH=/usr/share/dxvk/x64:…      ← graphics(overlay)
WINEDLLOVERRIDES=d3d11,dxgi=n,b        ← graphics(dxvk)
DXVK_HUD=0                             ← profile.env
```

这条能力对排查是决定性的：现在只能读代码猜。

### 7.2 预检 = 「锁 vs 现实」

不再是 6 项互不相关的检查，而是统一问题：**锁里写的东西还成立吗？**

| 检查 | 失败表现 | 处理 |
|---|---|---|
| 组件存在 | `missing` | 提示配方重取 / 换用系统版本 / 解除引用 |
| 哈希一致 | `drift` | 提示「X 从 A 变成 B」，提供继续/回滚 |
| 前缀与 runner 版本配对 | `version-mismatch` | 提示 `wineboot -u`（先快照） |
| 前缀 manifest 与实际 | `modified` | 展示差异，询问「以现状为准更新 manifest / 保持原样」 |
| 前缀占用 | `locked` | 显示占用者（pid/游戏名），等待或强制 |
| 磁盘空间 | `no-space` | 显示需求量与可用量 |

所有结果都进 `logs/launch/<时间>-<biz>.json`（现有 `LaunchReport` 机制保留）。

---

## 8 · 回收：可达性 GC（不是计数器）

> 这一节按 pnpm 的 store 语义校准过：pnpm 的 store 是**内容寻址 + 硬链接共享**，清理靠
> **可达性标记**（`pnpm store prune`）而不是时间或计数器。我们的组件仓与它同构，
> 所以用同一套语义，避免「维护计数器」带来的漂移问题。

### 8.1 根（root）：从哪些地方出发遍历

pnpm 的根是「有 `package.json` 的项目目录」。我们的根有四类，全部可直接从磁盘扫出来：

| 根 | 位置 | 类比 |
|---|---|---|
| 方案锁 | `profiles/*.lock.json` 里的组件 hash | pnpm 项目里的依赖声明 |
| 前缀 manifest | `prefix/*/manifest.json` 记录的 runner / inprefix graphics | 「这个项目实际装过的东西」 |
| 运行租约 | 运行中的游戏持有 `components/by-hash/<hash>/.lease`（pid + 启动时间 + 游戏 biz） | pnpm 无对应物（它不知道谁在跑） |
| 用户 `keep` | 组件仓里的显式标记 | Nix 的 `gcroot` 符号链接 |

### 8.2 规则

> **从根可达，或存在有效 lease，或带 keep → 永不自动删除。**

- 引用关系**不落盘**（没有计数器、没有 `refs.json` 真相）；每次 GC 从 §8.1 的根出发做一次
  **标记-清扫（mark-and-sweep）**。这样「删了方案文件」「改了锁」「进程崩了」都不会让状态说谎。
- 只有 **不可达** 的条目才进入「可回收」候选；**删除必须显式触发**（`--gc`）并**由用户确认**，
  `--gc --dry-run` 先列清单：被谁引用过、最后可用时间、占用空间。默认**不自动清理**（学 `pnpm store prune`）。
- 候选排序只按 **引用数（=0）+ 占用空间**，**不按时间**。「最近没用」不作为删除理由；
  「最近在用」也不代表还在用——用户数（引用图中的父节点数）才是唯一依据。
- `refcount` 这个词只保留在 UI 展示层（例如「被 3 个方案引用」），不作为内部状态。
- **LRU 只用于纯缓存**：缩略图、下载临时文件、合成壁纸——丢了大不了重算。**绝不用于删除「身份」。**

### 8.3 完整性校验（对应 `pnpm store status`）

`--verify-store`：遍历仓内条目，按 `index.json` 的 `(id, version) → hash` 与文件清单逐个校验
（默认抽检，`--full` 全量），报出「哈希不符 / 文件缺失 / 索引残留」三类问题。
损坏条目不自动删，标记为 `broken` 并给出「用配方重取 / 解除引用」两条路。

### 8.4 `missing` / `broken` 是一等状态，不是异常

`reference` 策略下，「锁里写了某个 hash，但那份东西已经不在了」是**常态**（系统升级替换、用户删目录、AUR 包被移除）。所以：

```
组件 dxvk-3.1.1 (sha256:41ab…) — 缺失
  被 2 个方案引用（主力、测试）
  最后可用：2026-10-28（来源：/usr/share/dxvk）
  [ 用配方重新获取 ]  [ 改用系统当前的 3.2.0 ]  [ 解除引用 ]
```

启动前把 `missing` 直接摆到用户面前，比等黑屏后再排查便宜得多。这与 pnpm 里「项目引用了 store 里不存在的包」
（悬空链接）是同一类状态，处理方式也一致：**报出来 + 给修复动作**，不静默。

### 8.5 共享语义：删到最后一个才真正释放

多个方案引用同一份内容时，仓里只有一份实体（`hardlink` 策略下是多条硬链接指向同一 inode）。
删掉一个方案**不释放空间**，删到最后一个才释放——这与 pnpm 删掉一个项目后 store 空间不立刻减少完全同构。
因此空间报告必须区分：

| 口径 | 含义 |
|---|---|
| 名义占用 | 条目大小之和（会重复计） |
| 实际占用 | 按 inode 去重后的真实磁盘占用 |
| 可回收 | 不可达条目的**实际**占用之和（去重后） |

计算用 `st_dev + st_ino` 去重，配合 `st_nlink` 判断「是否还有别的方案在共享」。

---

## 9 · 组件来源（provider）与下载

### 9.1 Provider 模型

市场不做成「我们维护一个列表」，而是 **provider（来源）+ 标准化索引**：

```
~/.config/lunarward/providers/
  official.json        默认源（可以只是若干 upstream 的指针）
  custom-<name>.json   用户添加：https URL / 本地目录 / file://
```

索引条目是纯数据，谁都能写：

```jsonc
{
  "id": "dxvk", "kind": "graphics", "version": "3.1.1",
  "platform": "linux-x86_64",
  "files": [ { "name": "x64/d3d11.dll", "size": 1234, "sha256": "…" } ],
  "urls": ["https://github.com/doitsujin/dxvk/releases/download/v3.1.1/dxvk-3.1.1.tar.gz"],
  "mirrors": ["…"],
  "license": "Zlib",
  "notes": "…"
}
```

### 9.2 硬要求

- **没有 `sha256` 的条目不接受**（宁可不装，也不装没校验的东西）；
- **多来源合并去重**：同 `(id, version, sha256)` 视为同一份；
- **编码/来源可信度**：`https` 正常；`http://` 明确警告；若源提供签名则做可选校验；
- **断点/重试/并发限流**；安装前用 `DriveHelper.GetDriveAvailableSpace` 检查空间；
- **记录 provenance**：装进来的条目带上「来自哪个 provider、何时、原始 URL、sha256」，UI 可查；
- **离线镜像**：本地目录 provider 是一等公民（受限网络下最实用）。

### 9.3 与参考计数的关系

下载安装 = 在仓里创建条目（`retain: copy` 或 `hardlink`，视解压结果），并且**不自动删除**：
即使没有任何方案引用，只要用户没删、`keep` 未关，就保留（下载物是「用户的资产」，不是缓存）。

---

## 10 · UI：三个视图，一个心智模型

**改声明 → 看解析 → 管产物。**

| 视图 | 展示 | 允许的操作 |
|---|---|---|
| **组件** | 发现到的 + 仓里的：来源/版本/哈希/大小/能力/被谁引用/是否 `missing` | 重新扫描、校验、复制进仓（hardlink/copy）、保留（keep）、删除（被引用时拦） |
| **来源** | provider 列表、更新时间、条目数、可信度 | 添加/移除来源、刷新索引、查看某条目的配方 |
| **方案** | 声明（可编辑）+ 解析结果（只读）+ 解释 + 健康状态 | 改声明、重新解析、锁定/跟随、导出/导入、复制方案 |
| **前缀** | base 与实例：manifest 摘要、大小、最后使用、绑定游戏、快照 | 创建、接管、升级（先快照）、快照/回滚、打开 shell、winecfg、winetricks、删除 |

游戏设置页只留一行「用哪个方案」+ 可选覆盖项。

---

## 11 · 与现有实现的映射与迁移

| 现在 | 目标 | 迁移方式 |
|---|---|---|
| `RuntimeProfile`（最终值） | `profile.json` 声明 + `profile.lock.json` 锁 | 首次解析现有 Profile 生成锁（`retain: reference`），字段全部保留，UI 不变 |
| `Component`（`Kind/Id/Name/Version/Path/Managed`） | 增加 `Hash/Source/Capabilities/Retain/LastSeen` | 老数据迁移时字段可空，首次扫描补全 |
| `PrefixMode` 5 种 | 保留模式 + `base`/`snapshot` 概念 + manifest | 现有前缀补写最小 manifest（`created_with` 尽量从 `.lunarward/prefix.json` 推出），推不出就标 `unknown` |
| `GraphicsInstaller`（复制 dll） | `overlay` 默认，`inprefix` 显式 | 现有 `graphics.json` 迁移为 manifest 的 `graphics` 操作 |
| `LaunchPreflight` 6 项 | 「锁 vs 现实」统一校验 | 现有检查作为子项保留，输出结构不变（`LaunchReport` 兼容） |
| 无 GC | refcount + lease + keep | 新增 `--gc --dry-run` |
| `PrefixManager.EnsurePrefix` | 生命周期动作 | 保留现有 `template/dedicated/shared/external/ephemeral` 语义，内部改调生命周期方法 |

**兼容原则**：配置文件**向后兼容**（旧 `profiles/*.json` 仍能读），锁文件**可重建**（删了能重新解析），
manifest **可缺省**（缺了只影响 `verify`，不影响启动）。

---

## 12 · 分期

| 阶段 | 内容 | 产出 | 依赖 |
|---|---|---|---|
| **P0** | `Component` 加 `hash/source/capabilities`；L0/L1 校验；`--list-components --json` | 能回答「这是什么、哪来的、变了吗」 | 无 |
| **P1** | `profile.lock.json` + 解析器 + `explain`；启动前 drift 检测与提示 | 外部变化在启动前暴露 | P0 |
| **P2** | 保留策略 `reference/hardlink/copy` + 配方；`missing` 状态与 UI 入口 | 「以后拿得到」不依赖复制 | P0/P1 |
| **P3** | 前缀 manifest + 生命周期动作（create/adopt/upgrade/snapshot/restore/verify） | 前缀可重建、可回滚 | P1 |
| **P4** | 图形栈 `overlay` 默认 + `WINEDLLPATH`；env 分层与 `--print-env` | 换 DXVK 不动前缀；env 可解释 | P1 |
| **P5** | provider/索引 + 下载（多来源、sha256、provenance） | 不再手动找包 | P2 |
| **P6** | 三个视图的 UI、空间报告、`--gc` | 完整体验 | P2–P5 |

**分水岭是 P0 + P1**：做完之后「我在用什么、它变了吗」就已经回答了，后面每一项都是增益而非前提。

---

## 13 · 风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| 硬链接快照被原地改写污染 | 回滚拿到的是「混合态」 | 快照只用于回滚；回滚前 `verify`（哈希）；文档明确告知 |
| `reference` 策略下组件频繁 `missing` | 启动被拦、体验差 | `missing` 一等状态 + 一键重取 + 可选升级为 `hardlink` |
| 哈希计算拖慢启动 | 启动变慢 | L0/L1 快路径 + `verified_at` 缓存 + L2 只在变化时 |
| AUR 老版本彻底不可得 | 无法回滚 runner | 提前 `hardlink`；配方多镜像；记录「下次升级前建议保留」 |
| provider 索引被投毒 | 装到恶意包 | 强制 sha256；`http` 警告；签名可选；UI 展示来源与许可证 |
| 迁移丢失现有配置 | 用户数据 | 迁移只读不改旧文件；新文件并存；`--migrate --dry-run` |
| manifest 与实际长期不一致 | `verify` 噪声 | 允许「以现状为准」一键更新 manifest；不一致只告警不阻断 |

---

## 14 · 待决问题（需用户拍板）

| # | 问题 | 备选 | 现状 |
|---|---|---|---|
| Q1 | 默认保留策略 | `reference`（默认，省空间） / `hardlink`（默认，稳） | 建议 `reference`，UI 首次固定时提示可升级为 `hardlink` |
| Q2 | 游戏设置页是否允许覆盖组件（不改方案） | 允许 / 不允许 | 建议允许「临时覆盖」，但启动报告里标红 |
| Q3 | `inprefix` 与 `overlay` 的默认 | overlay / inprefix | 建议 overlay，兼容问题再切 |
| Q4 | 是否把 `Provider` 配置放进 `config.json` 还是独立目录 | 独立目录 / 合并 | 建议独立 `providers/`，便于分享与版本管理 |
| Q5 | `--print-env` 是否显示敏感值（token 等） | 显示 / 脱敏 | 建议默认脱敏，`--print-env --raw` 才显示 |

---

## 15 · 附：一页速查

```
组件 = (kind, id, version, sha256, source, capabilities)
保留 = reference(0) / hardlink(≈0) / copy(实际)   ← 默认 reference + 配方
方案 = profile.json(声明) + profile.lock.json(锁)
解析 = 显式 > 继承 > 默认；能力校验；冲突检测；写 explain
前缀 = base + manifest(operations) + 生命周期(create/adopt/upgrade/snapshot/restore/clone/verify/discard)
注入 = overlay(WINEDLLPATH) 默认 / inprefix 显式
启动 = env 分层 + --print-env 可解释；预检 = 锁 vs 现实
回收 = 可达性 GC（标记-清扫）+ lease + keep(gcroot)；不可达才是候选；删除需 --gc 显式确认
候选排序 = 引用数 + 占用空间（不看时间）；LRU 只用于纯缓存
缺失/损坏 = 一等状态（不是异常），给配方重取/换版本/解除引用
市场 = provider + 索引(sha256 必需) + 多来源 + provenance + 离线镜像
```
