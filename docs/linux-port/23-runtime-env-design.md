# 23 · 运行环境模块：从头设计（v3 · 定稿）

> 状态：**方向已确认，未开工**（2026-11-14，用户 m21984「你的建议都不错，按你的建议来吧」）。
> 取代：`16-runtime-components.md`（v2 极简）、`17-runtime-environment-ui.md`（v2 逻辑与 UI）——两份保留作为历史，一切以本文为准。
> 相关决议：D58（运行组件）、D63（运行环境 P1–P4）、D61（删模板库）、D64（冗余清理）、D72（UI 线程不许 fork）；本文新增 **D73**。
> 本文只写设计。任何实现都必须先在本文件里把字段/行为定下来，再由用户在对话中明确授权开工。

---

## 0 · 一页摘要

三个概念，一个页面，八个字段。

| 概念 | 一句话 | 谁是事实 |
| --- | --- | --- |
| **组件** | 磁盘上一坨东西的事实描述：运行器（wine/Proton）或图形层（DXVK/VKD3D） | 探测结果 + manifest |
| **环境**（配方） | 一份启动配方：用哪个运行器 + 哪个图形层 + 哪个前缀 + env + 参数 + 命令包装 | 用户写的 JSON |
| **绑定** | 游戏 → 环境，多对一 | 用户选的 |

前缀不是概念，是环境里的一个**路径字段**加一段只读检查。

### 自洽（本文的硬性目标）

> 在一台干净的 Arch 上装完 Lunarward，**不依赖 Steam、不依赖别的启动器、不依赖发行版打包的 wine**，仅靠本程序内的操作就能把游戏跑起来。

自洽链条上必需的三样东西，全部由本程序提供：

1. **运行器** —— 受管下载（wine-staging 预编译为默认项）+ 系统/Steam/umu/手动目录发现
2. **图形层** —— 受管下载（DXVK / VKD3D-Proton）+ 装进前缀
3. **前缀** —— `wineboot -u` 自动新建，或指向已有目录

游戏本体由 Sophon 安装链路提供（已实现）。

**自洽的边界**：32 位基础库、Vulkan loader、显卡驱动在系统里，本程序不代装、不假装能装。缺依赖时必须**由我们指出缺什么**，见 §2.5。

---

## 1 · 概念与边界

### 1.1 组件（Component）

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | string | 稳定标识：受管为 `<kind>-<name>-<version>`，其余为路径哈希前 8 位 |
| `kind` | `wine` \| `proton` \| `graphics` | 决定性字段，见 §2.1 |
| `name` / `version` | string | 展示用；版本优先从二进制/资源读，读不到退回目录名 |
| `root` | path | 用于拼路径的根（`bin/wine` 或 `files/bin/wine` 的父目录） |
| `origin` | `managed` \| `system` \| `steam` \| `umu` \| `manual` | 决定「能不能删、能不能校验」 |
| `health` | 派生 | 见 §2.5，不入盘 |
| `notes` | string? | 用户备注 |

规则：
- `origin != managed` 的组件**只读引用**：不复制、不修改、不删除、不算占用。
- 只有 `origin == managed` 的组件参与「校验 / 删除 / 清理」。
- `kind` 由探测给默认值，**用户可覆盖**；Proton 判错代价高（启动即崩），所以该字段在详情里显式可见。

### 1.2 环境（Environment，即"配方"）

八字段，见 §4。环境是**唯一**的运行依据；启动计划由环境展开而来（`--launch-plan` 输出即展开结果）。

### 1.3 绑定（Binding）

`gameBiz → environmentId`。多对一是正常状态（「多个游戏共用一份」是主流用法），不是特例。

### 1.4 前缀（Prefix）

一个目录。不是模式枚举：不再是 `template/dedicated/shared/external/ephemeral`，只有「这个路径是什么」，以及围绕它的三个动作。

---

## 2 · 组件模型

### 2.1 三类组件与探测规则

**① `wine`（普通 wine 构建）**
接受三形态：`bin/wine` 所在目录 / wine 可执行文件本身 / 解压出来的多层目录（自动向下找 `bin/wine`）。
探测：`wine --version` 的版本串；`lib/wine/i386-unix` 或 `lib32` 是否存在于构建内（32 位支持）；目录可写性。

**② `proton`（Proton 目录）**
识别 `files/bin/wine` 布局。**Proton 不是可执行启动器**，脱离 Steam 运行需要 umu 或自拼 `STEAM_COMPAT_*`。
界面必须在选中时写明：「Proton 需要 Steam 或 umu；自洽模式下推荐用 wine 类运行器」。

**③ `graphics`（DXVK / VKD3D-Proton）**
接受一个目录，探测：
- DXVK：`d3d11.dll`、`dxgi.dll`、`d3d10core.dll`（可选 `d3d9.dll`）
- VKD3D-Proton：`d3d12.dll`、`d3d12core.dll`
- 布局：新旧包结构不同（`x64/` + `x32/`，或直接平铺）
- 版本：优先读 dll 版本资源，读不到退回目录名

**不匹配就直说**：「这个目录里没找到 `d3d11.dll`，不像 DXVK」，不要把不匹配的东西列进下拉。

**不接受**作为组件的：`gamescope`、`mangohud`、`gamemoderun`（属于 §4 的命令包装）、单个 `.exe` 辅助工具（属于后启动/伴随）。

### 2.2 受管目录与 manifest

```
~/.local/share/lunarward/components/
  wine/<name>-<version>/
  graphics/<name>-<version>/
  <name>-<version>/manifest.json
```

`manifest.json` 字段：

```json
{
  "schemaVersion": 1,
  "id": "wine-staging-10.13",
  "kind": "wine",
  "name": "wine-staging",
  "version": "10.13",
  "root": ".",
  "layout": "bin-wine",
  "origin": "managed",
  "source": {
    "url": "https://github.com/<owner>/<repo>/releases/download/.../wine-staging-10.13-x86_64.tar.xz",
    "mirrorPrefix": "",
    "downloadedAt": "2026-11-14T09:00:00+08:00"
  },
  "digest": { "algo": "sha256", "value": "…", "fromApi": true },
  "layoutNormalized": true,
  "notes": null
}
```

`layout` 取值：`bin-wine` / `files-bin-wine` / `bare-exe`。`root` 是按 layout 找到的根，相对 `components/<name>-<version>/`。

**布局归一化**是必须的一步：各家预编译包解压出来的层级不同（`wine-tkg-10.13/bin/wine`、`wine-staging-10.13/bin/wine`、`GE-Proton9-20/files/bin/wine`）。探测出 `root` 后写进 manifest，运行器路径按 `root` 拼——不归一化的话后面每个构建都要特判。

### 2.3 下载源（可配置，不写死）

默认三条，全部走 GitHub Releases API（`/repos/{owner}/{repo}/releases`，按 asset 名匹配 `.tar.xz` / `.tar.gz`），不爬网页、不依赖任何私有 API：

| 优先级 | 名称 | 用途 |
| --- | --- | --- |
| 1（默认一键项） | wine-staging 预编译 | 最接近上游，兼容性好 |
| 2 | wine-tkg 预编译 | 性能好，补丁多 |
| 3 | GE-Proton | 需要 Steam 或 umu（见 §2.1） |

界面支持新增自定义源：`{owner}/{repo}` + asset 匹配模式 + `kind`。

**镜像前缀**：源条目带 `mirrorPrefix`（默认空 = 直连），可填 `https://<mirror>/`；**下载失败时把真实 URL 原样打出来**，让人能自己 `curl` 试。

**校验**：Releases API 若提供 `digest` 则用它；没有就首次下载算 SHA-256 记入 manifest（TOFU），之后按记录校验——保证「同一个版本不会悄悄变」。

**下载可靠性**：可取消；解压前用 `df` 估算所需空间，不够就**拒绝并报差多少**，不要解压到一半失败；解压到临时目录，成功后原子 `move`（`Helpers/JsonFile.cs` 同款原子思路）。

**不自动更新运行器**：只在组件页标「有新版本」。自动换运行器是毁体验的典型做法。

**并发下载（用户 m22018 明确要求：不许串行）**

单个大包（wine 构建 400–700 MB）必须分片并行；这是与 an-anime-team 那类「一条流慢慢拉」的主要差距。

| 项 | 决定 |
| --- | --- |
| 探测 | 先发 `HEAD`，看 `Accept-Ranges: bytes` + `Content-Length` |
| 支持 Range | 切片并行，默认 **4 连接**（可配置 1–16），片大小 8 MiB；每片独立 `Range` 请求写到 `.part.<n>` |
| 不支持 Range | 退回单流，并在组件页提示「该源不支持分片，可能较慢」 |
| 断点续传 | `.part.<n>` 保留；重启后按已完成片数续传（与安装链路 `InstallPipeline.RunResumableAsync` 同一思路） |
| 校验 | 全部片落地后按偏移合并 → 整体 SHA-256 一次；与应用内 `digest` 比对，不一致删掉重来 |
| 多组件同时下载 | **排队，一次只下一样**（避免互相抢带宽）；队列显示在组件页 |
| 解压 | 优先选 `.tar.zst` / `.tar.gz` asset（zstd 解压快且可多线程）；只有 `.tar.xz` 时把 `xz -d -T0` + `tar -xf` 作为外部命令在后台线程跑，不用托管解压库（慢且无多线程） |
| 并发上限 | 单文件 4 连接；同一时刻最多 1 个文件在下载。实测基线：安装链路 5 并发做到 14 MB/s，故 4 连接足以打满常见宽带 |

已知风险：部分镜像会限速或直接拒绝多连接 → 连续两个片 429/403 就自动降为 1 连接，并在组件页记一行「已降为单连接（源限制）」。


### 2.4 本地 tarball 导入（不新增概念）

「添加路径…」的对话框允许选**压缩包**：
- 选到目录 → 按 `manual` 只读引用；
- 选到 `.tar.xz` / `.tar.gz` → 解压到 `components/`、布局归一化、写 manifest、按 `managed` 处理。

不新增按钮，只在文件过滤里多两个扩展名。

### 2.5 自检与依赖缺失（不映射发行版包名）

组件首次被选用时跑一次 `wine --version`；组件页提供「重新检测」。**不在每次启动时跑**（启动前检查已覆盖）。

- 成功 → `health = ok`，记录版本串。
- 失败 → `health = broken`，健康状态里只放**一句概括**（「wine 启动失败，退出码 127」），点开是**只读、等宽、可全选复制**的文本框，内容是 stderr 原文；旁边一个「复制」按钮。

**不做包名映射**（`lib32-foo` / `libfoo:i386` / `-devel` 的差异会把用户带到错的地方）。不猜、不翻译、不建议。

### 2.6 组件页结构

```
运行器
  ── 受管（可删除、可校验）──
     wine-staging 10.13      [已下载]  [重新检测] [删除]
     GE-Proton9-20           [需要 Steam 或 umu]
  ── 只读引用（不复制、不修改）──
     /usr/bin/wine 10.0      (系统包)
     Proton 9.0             (Steam)
     ~/wine-custom          (手动)
  [添加路径…]  [下载运行器…]

图形层
  ── 受管 ──
     dxvk 2.7   vkd3d-proton 2.14
  ── 只读引用 ──
     ~/dxvk-nightly          (手动)
  [添加路径…]  [下载图形层…]

受管组件占用 1.4 GB   [清理未使用的组件]
```

扫不到「受管」那一段时，第一行就是一个按钮「下载一个运行器」——这是自洽的入口。

---

## 3 · 前缀

### 3.1 语义

前缀 = 目录。三个动作：**新建**（`wineboot -u`）、**指向已有**（外部前缀，我们只往里装 dll 并记录，不改结构）、**从…复制**（`cp -a --reflink=auto`，实测 650 MB / 0.15 s）。

「隔离 / 共享 / 临时」不再是要预先选定的模式：两个环境指同一个路径就是共享，用完删就是临时。**用户不选模式，只给路径。**

### 3.2 架构（`WINEARCH`）

新建时选一次（默认 `win64`），之后作为**只读字段**显示——建完改不了，只能重建。32 位支持缺失会建出残缺前缀，这时明确报「这台机器缺 32 位支持（multilib），建议先装」。

### 3.3 属主与 root

wine 不允许 root 运行。启动时若 `geteuid() == 0` 直接拒绝并说明原因（当前未实现）。

### 3.4 占用

已有 `<prefix>/.lunarward/lock`（`FileShare.None`）做真互斥。界面必须有对应表达：
- 环境卡片：「正在被《绝区零》使用」
- 第二个游戏点启动：「这个前缀正在被使用，共用前缀无法同时启动」+ 按钮「改用独立前缀」

### 3.5 前缀里的记录

只写两样：装进去的 dll（已有 `graphics.json`）和一份 `<prefix>/lunarward.toml` 侧写（谁装的、能回滚）。除此之外不碰注册表结构、不改目录名。

### 3.6 体积

前缀体积后台算、10 分钟缓存（`PrefixManager.PrefixInspection`）。显示在环境详情，不放在列表卡片上（避免每次进页面都扫盘）。

---

## 4 · 环境（配方）

### 4.1 字段（八个）

| # | 字段 | 类型 | 默认 | 说明 |
| --- | --- | --- | --- | --- |
| 1 | `name` | string | 必填 | 展示名 |
| 2 | `runnerId` | string | 首个可用运行器 | 引用的组件 id |
| 3 | `graphicsId` | string? | `null` | `null` = 不装图形层 |
| 4 | `prefixPath` | path | `~/.local/share/lunarward/prefixes/<name>` | 前缀目录 |
| 5 | `env` | map | `{ "WINEDLLOVERRIDES": "mscoree,mshtml=" }` | 见 §4.3，**可见可删** |
| 6 | `args` | string | `""` | 启动参数（原样拼给游戏 exe） |
| 7 | `wrappers` | string[] | `[]` | 命令包装，见 §4.4 |
| 8 | `notes` | string? | `null` | 备注 |

`prefixArch` 不是配置项：新建时选一次并写进前缀侧写，之后只读显示。

### 4.2 环境操作

保存 / 复制这份配方 / 删除配方 / 导出 JSON（**不提供导入 CLI**——一次复制 + 改两个字段就够）。

删除时若被绑定引用：列出游戏名再确认，并把绑定清空（不静默改绑到别的环境）。

### 4.3 env 优先级（三层）

```
运行器自动注入  →  配方 env  →  进程环境
```

- 配方 env **覆盖**自动注入（用户显式优先）。
- 覆盖了自动注入的 key 要**标红并说明**：「你覆盖了自动注入的 `WINEDLLPATH`」——不静默。
- `WINEDLLOVERRIDES=mscoree,mshtml=` 是配方的**可见默认值**（避免 wine-mono/wine-gecko 弹窗），不是藏起来的代码。

### 4.4 命令包装（wrappers）

不是「一个字符串塞进去」，而是一个有序列表，每项一个命令前缀，按顺序拼在 `wine` **前面**。界面显示最终 argv 预览。

这条的意义：`--launch-plan` 的输出是唯一真相，界面与 CLI 看同一份展开结果。

---

## 5 · 绑定与首次使用

- 绑定存 `gameBiz → environmentId`，按 **id**（不是名字），重命名配方不影响绑定。
- 游戏设置页加一行「使用环境：`genshin` ▾」，默认就是同名那份。
- **新游戏第一次启动**：自动建一份同名环境并提示「已为《原神》创建环境 `genshin`，可在运行环境里修改或改用其它」。理由：没有环境的游戏根本启动不了，强制手动选择等于抬高「能跑」的门槛。

---

## 6 · 检查与诊断

点「启动」时跑一次，四层结果**归并成一张按严重程度排序的清单**，每项带一个动作按钮（复用 D59 已实现的 `PreflightAction`）：

| 层 | 判据 | 典型动作 |
| --- | --- | --- |
| 组件层 | 文件在不在、可执行、版本、Proton 布局 | 打开组件页 / 重新下载 |
| 前缀层 | 存在、可写、`drive_c`、未被占用、架构匹配 | 打开目录 / 重建前缀 |
| 图形层 | dll 齐不齐、与记录是否一致、是否被游戏目录自带 dll 覆盖 | 安装 / 重装图形层 |
| 启动层 | argv、进程是否起来、存活是否超时、注册表读取是否报错 | 查看展开命令 / 查看日志 |

**游戏目录自带 dll 检测**：启动前扫游戏目录里的 `d3d11.dll` / `dxgi.dll` / `d3d12.dll`，有就提示「游戏目录自带的 dll 会覆盖图形层，可能不是你期望的效果」。只提示，不自动删。

**last-good（单字段，不是历史）**：
`{ runnerId, runnerVersion, graphicsId, prefixPath, succeededAt }` 存在环境侧，卡片上显示「上次成功：11-14 21:03」。出问题时给一个「用上次能跑的组合重新启动」按钮。

---

## 7 · 页面

### 7.1 运行环境（唯一主页面）

上面一排环境卡片，每张四行：名字、运行器（名称+版本）、图形层（或「无」）、前缀（`~/…` 缩写）。右上角一个灯：

- 🟢 这套能跑
- 🟡 缺组件但能装（点进去有下载入口）
- 🔴 配置错（悬停一句原因）

点开是表单（§4.1 八个字段一屏放得下），底部：保存 / 复制 / 删除。前缀路径右侧：「打开目录」「检查」。

**左栏不出现「前缀管理」入口**——前缀是字段。

### 7.2 组件页

见 §2.6。

### 7.3 游戏设置页

只加一行下拉 + 一个「运行诊断」按钮（已有）。

### 7.4 环境详情里的「文本视图」

只读，显示规范化后的 JSON，配「复制」「导出文件」。**不做文本编辑**——两个编辑器改同一份数据必然互相覆盖。

---

## 8 · 文件与持久化

```
~/.config/lunarward/
  env/<id>.json              # 每环境一个文件；删除/分享/回滚粒度自然
  env-bindings.json
  components-sources.json    # 下载源（含镜像前缀），可手改
```

`env/<id>.json` 样例（写全量，不写默认值以外的隐式修正）：

```json
{
  "schemaVersion": 1,
  "id": "genshin",
  "name": "genshin",
  "runnerId": "wine-staging-10.13",
  "graphicsId": "dxvk-2.7",
  "prefixPath": "~/.local/share/lunarward/prefixes/genshin",
  "env": { "WINEDLLOVERRIDES": "mscoree,mshtml=" },
  "args": "",
  "wrappers": [],
  "notes": null
}
```

**两条持久化硬规则**：

1. **保存即全量**：一次保存写的就是当前完整状态，不做增量合并，不「顺手修正」用户没碰的字段（否则 diff 全是噪声）。
2. **没有隐藏字段**：界面能影响运行的每一项，导出文件里都必须能看到；文件里有的字段，界面必须有地方改（可以藏在高级里，但不能没有）。

**派生数据全部可丢**：探测结果、健康状态、前缀体积、last-good 之外的缓存放 `~/.cache/lunarward/`，删掉能重建。

**文件不存 UI 状态**（窗口尺寸、滚动位置等另放）。

---

## 9 · 明确不做

| 不做的 | 理由 |
| --- | --- |
| `extends` 覆盖层 | 复制一次配方就够，链式继承的合并顺序永远是争论来源 |
| 矩阵试跑（`env matrix`） | 一年用不了几次，且有 CLI |
| Run 历史独立存储 | 日志文件本来就有；只留一个 `last-good` 字段 |
| 导入 CLI（`env import`） | 一个「复制配方」+ 改两个字段就够 |
| 自动更新运行器 | 自动换运行器是毁体验的经典做法 |
| 依赖映射成发行版包名 | 映射错会把用户带到错的地方；给原文 |
| 向导式首次配置 | 过度封装 |
| 前缀作为左栏入口 | 它是环境里的字段 |
| 模板库 / `template` 模式 | 已被「从…复制」覆盖 |
| `ephemeral` 作为模式 | 变成「用完删」这个动作 |
| 日志回读 API | `--output-tail` 与日志文件已够 |

---

## 10 · 验收项（可测）

**A 组件**
- A1 空环境（PATH 无 wine、无 Steam、无 umu）下组件页只剩「下载运行器」入口，且可完成下载 → 归一化 → 可用
- A2 布局归一化对三种包结构（`bin/wine` / `files/bin/wine` / 裸 exe）都能定位
- A3 手动导入手部 tarball 走受管流程，manifest 字段完整
- A4 下载可取消；磁盘不足时拒绝并报差额
- A5 镜像前缀生效；下载失败打真实 URL
- A6 组件被配方引用时删除需二次确认并列出引用者
- A7 分片并行下载：`Accept-Ranges` 源实测连接数 = 4，总耗时明显低于单流；中断后续传接上
- A8 源不支持 Range 时自动退回单流并提示；被限速时自动降为单连接

**B 前缀**
- B1 新建（`wineboot -u`）+ `WINEDLLOVERRIDES` 默认值生效（无 mono/gecko 弹窗）
- B2 从已有前缀复制（reflink）后可用
- B3 指向外部前缀：不修改结构，只装 dll 并记录
- B4 共用前缀第二个启动被拒并给「改用独立前缀」按钮
- B5 root 运行被拒并说明

**C 环境**
- C1 八个字段可改、可留空，留空显示「默认/继承」
- C2 复制配方生成新 id 且不与原名冲突
- C3 删除被引用配方：列出游戏、清空绑定
- C4 env 覆盖自动注入时标红说明
- C5 wrappers 顺序正确，`--launch-plan` 与界面预览一致

**D 检查与诊断**
- D1 四层失败项都能指到层并给动作
- D2 游戏目录自带 dll 被提示
- D3 last-good 显示与「用上次能跑的组合」可用
- D4 组件 broken 时给原文（等宽、只读、可复制），不含包名建议

**E 持久化**
- E1 保存全量：改一个字段不动其它字段的文件内容
- E2 无隐藏字段：界面所有可影响运行的项都能在导出 JSON 里找到
- E3 删除 `~/.cache/lunarward/` 后功能正常（只重算）

---

## 11 · 待补细节（实现前再定）

1. `env` 的键是否区分大小写（wine 在 Linux 下大小写敏感）——倾向按原样保存、原样传。
2. Proton 自洽跑通的具体环境（`STEAM_COMPAT_*` 套装 vs 内置 umu）——需要实测后再定。
3. `wrappers` 是否支持带参数的项（`gamescope -W 2560 -H 1440 --`）——倾向支持，存字符串数组 + 拼接时不加 shell 解释。
4. ~~组件下载的并发与限速~~ → 已定案，见 §2.3「并发下载」（分片并行 4 连接 + 断点续传 + 限速降级）。
5. 前缀侧写 `<prefix>/lunarward.toml` 是否改成 JSON（与其它文件一致）。

---

## 12 · 阶段 1 实施记录（组件层，2026-11-14）

对应 §10 验收 A1–A8。文件：

| 文件 | 职责 |
| --- | --- |
| `src/Lunarward/Features/Runtime/ComponentManifest.cs` | `manifest.json` 读写（`SchemaVersion=1`）+ `ComponentLayouts.FindRunnerRoot` 布局归一化（`bin` / `files-bin` / `bare-exe`） |
| `src/Lunarward/Features/Runtime/ComponentDownloader.cs` | HEAD 探 Range → 8 MiB 分片 × 4 连接并行 → 250 ms 采样进度 → 429/403 降级单流 → 合并后 SHA-256 → 原子改名 |
| `src/Lunarward/Features/Runtime/ComponentSources.cs` | 五个默认源（wine-staging / wine-tkg / GE-Proton / DXVK / VKD3D-Proton）+ 用户覆盖 `components-sources.json` |
| `src/Lunarward/Features/Runtime/ComponentImporter.cs` | 系统 `tar` 解压 → 单层目录摊平 → 布局识别 → 搬进 `components/<wine|graphics>/<名字>-<版本>` → 写 manifest |
| `src/Lunarward/Features/Runtime/ComponentHealth.cs` | 一句话结论 + stdout/stderr 原文 + 退出码（不映射发行版包名） |
| `ComponentStore.cs`（改） | 受管目录优先读 manifest（名字/版本/布局），图形层扫描补 `components/graphics`，`AddGraphics` 也走 `RealPath` 去重 |

CLI：`--component-scan`、`--component-health <id|名字>`、`--component-import <归档> --kind wine|graphics`、`--component-download <源 id>`。

实测（2026-11-14，本机 CachyOS）：

- `--component-download dxvk` → `dxvk-3.1.1.tar.gz` 18,041,512 字节，4 连接，`digest 一致`，导入为 `graphics/dxvk-3.1.1`，`x64/d3d11.dll` 就位。
- 归档若只包一层同名目录，会被摊平（否则会得到 `graphics/dxvk-3.1.1/dxvk-3.1.1/`）。
- 同一组件经 `~/.local/share/Lunarward`（数据目录符号链接）曾列两遍 → `AddGraphics` 改走 `RealPath` 后合并。
- `--component-health` 对假运行器与真 wine 均给出原文：`wine-11.19-1-g78e32ba1d3d (Staging)`。

尚未做（进入阶段 2 前）：下载取消按钮与磁盘空间预检（§2.3）、首次选用时的自动自检时机（§2.5）、`components-sources.json` 的编辑界面。

---

## 13. 阶段 2/3 实施记录（2026-11-14）

### 13.1 模型层

| 文件 | 变化 |
| --- | --- |
| `Features/Runtime/RuntimeProfile.cs`（重写） | 八字段落地：`RunnerId` / `GraphicsId` / `PrefixPath` / `env` / `Args` / `Wrappers` / `Notes`（+ `Shell` / `GameOutput` / `PrefixArch`）。旧字段降级为迁移用 `Legacy*`，`MigrateFromLegacy()` 纯内存、可单测 |
| `Features/Runtime/LaunchRecipe.cs`（新） | `LaunchRecipeBuilder.SplitArgs / BuildArgv / MergeEnvironment / Build / JoinCommandLine`：argv 与 env 的唯一构造函数，启动、预览、`--launch-plan` 三处共用 |
| `Features/Runtime/EnvironmentResolver.cs`（新） | `ResolveRunner`（id → 路径 → 首个可用）/ `Refresh` / `EffectivePrefixPath`（显式路径优先，否则 `PrefixRoot/<名字>`） |
| `Features/Runtime/RuntimeProfileStore.cs`（重写） | 存到 `~/.config/lunarward/env/<id>.json`；`LoadAll` 自动把 `profiles/` 迁过来并改名为 `profiles.migrated-<时间戳>` |
| `Features/Runtime/PrefixManager.cs` | `EnsurePrefix(profile, gameKey, logger)` 改成路径式：不存在就 `wineboot -u` 建，存在但缺 `drive_c` 直接抛可读异常（不再猜「模式」）。`PrefixMode` 已无引用 |

### 13.2 启动链路

- `GameLauncherService.Linux.cs` 一律经 `LaunchRecipeBuilder.Build` 生成 `ProcessStartInfo`；配方里显式写了 `WINEPREFIX` 且与真实前缀不同时，保留真实前缀并把这次覆盖记进 `LaunchReport` + 一行 `LogWarning`。
- `LaunchReport` 的 Profile 段改记 `RunnerId/RunnerPath/PrefixPath/PrefixArch/GraphicsId/Args/Wrappers`。
- `LaunchPreflight` 增加 `PreflightLayer{Component,Prefix,Graphics,Launch}`；清单按「层 → 严重程度」排序，前缀项统一说「启动时创建」。
- `--launch-plan <exe> [--profile <id|名字>]` 打印配方、运行器、wine 路径、前缀（不存在时标「启动时创建」）、完整命令行、每个 env 与覆盖项。

### 13.3 页面

- 卡片四行：名称 + 状态灯（红 = 运行器不可用或前缀缺 `drive_c`；黄 = 前缀未创建/被占用；绿 = 可启动）、摘要、图形层与启动参数/包装、前缀占用（后台 `Task.Run(PrefixManager.Inspect)` 回填）+ 上次启动 + 按钮。
- 组件区拆「受管 / 外部」两段（运行器与图形层都拆），来源标签保留。
- 编辑器：「启动预览」段显示将要执行的命令行与冲突 env（红色），文本视图对话框显示完整 JSON（等宽、只读、可复制、可导出）。
- 游戏设置页沿用既有「运行环境」下拉 + 绑定失效 InfoBar。

### 13.4 验证

| 项 | 结果 |
| --- | --- |
| `dotnet test` | 119 通过（新增 5 条迁移用例） |
| `scripts/test/cli-extra.sh` | 22/22（新增 T-CL-40 `--launch-plan`、T-CL-41 `--profile` 不存在） |
| `scripts/test/cli-smoke.sh` | 40/0/1 |
| `scripts/test/lint-ui-thread.sh` | 三规则 PASS |
| `scripts/test/coverage.sh` | 行 88.50%（1679/1897）、分支 71.02% |
| `--selftest-ui` | 10/10 |
| 真机 `--launch-plan` | 旧 `profiles/` 自动迁移到 `env/8db5b3e2.json`，旧目录改名 `profiles.migrated-20261114124806` |

### 13.5 仍未做

- 下载取消按钮、磁盘空间预检、首次选用时的自动自检（阶段 1 遗留）。
- 「使用环境」在游戏内的检查清单是否按 §6 四层归并再收一遍：现有 `LaunchPreflight` + 可点修复已覆盖，验收时按体感决定。
- 前缀体积只在卡片上显示（后台算），没有做「按前缀分组统计总占用」的汇总页。

### 13.6 真机复检（2026-11-14，1440p 屏 1200×676 窗口）

- 方案卡片、前缀卡片、组件两分区、编辑器的「启动预览」与「高级」折叠均正常渲染。
- 「配置文本（只读）」的多行 `TextBox` 只画第一行（`{`）→ 已换成 `ScrollViewer` + `TextBlock`（L-141）；前缀卡片的图形层状态补上「图形层：」前缀。
- 未验证（留给验收）：`--component-download` 之外的下载取消按钮（未实现）、游戏设置页下拉在真实游戏上的联动。

### 13.7 组件健康入口（2026-11-14 补）

- §2.6 要求的「组件健康」之前只在 CLI（`--component-health`）有落点，页面里没有入口 → 现在每一行组件（运行器与图形层都算）右侧都有「检查」按钮。
- 点开是一个对话框：标题「健康检查 · <名字>」，第一行是一句概括（`可用 (wine-11.19-… (Staging))` / `不可用：<原因>`），第二行是 `退出码 · 耗时 · 路径`，下面是等宽、只读、可复制的原文（命令行 + `exit:` + `stdout:` / `stderr:`），按钮「复制原文 / 关闭」。
- 按 m21977 的口径：**不做发行版包名映射**，只给原文，避免映射误差把关键信息吃掉；检查是后台线程跑的（`ComponentHealth.CheckAsync` 用 `WaitForExitAsync` + 15 s 超时），不占 UI 线程（硬规则 10）。
- 实测（系统 Wine）：概括 `可用 (wine-11.19-1-g78e32ba1d3d (Staging))`、`退出码 0 · 耗时 4 ms`、原文含 `$ /opt/spritz-wine-tkg/bin/wine --version` 与 `stdout: wine-11.19-…`。

### 13.8 解压换库 + 下载/导入入口（2026-11-14 补，24 号 P1）

- **解压不再是外部进程**：新增 `src/Lunarward/Features/Runtime/ArchiveExtractor.cs`，`.tar` / `.tar.gz` / `.tgz` / `.tar.zst` 用 BCL `TarReader` + `GZipStream` / `ZstdSharp.DecompressionStream`，`.tar.xz` 走 `SharpCompress` 的 `ReaderFactory`，`.zip` 走 `ZipFile`。行为差异：可取消（`CancellationToken` 逐块生效）、有进度（`ArchiveProgress(Entries, Bytes)`）、路径逃逸在解压前就被拒（`ResolveTarget` 对绝对路径与 `..` 抛 `InvalidDataException`）。这同时满足硬规则 10（UI 线程不 fork 外部进程）。
- **`ComponentImporter` 语义不变**：签名加一个 `IProgress<ArchiveProgress>? progress`，其余（单层目录摊平、`FindRunnerRoot` 判定布局、写 manifest、`ComponentStore.Invalidate()`）原样。
- **页面补上两个入口**（`RuntimeEnvironmentSetting.xaml` 组件区按钮行）：
  - 「下载组件…」：选源 → `加载版本列表`（`ComponentSources.ListAssetsAsync`）→ 选归档 → `开始下载`。下载前做空间预检（归档 ×2 + 64 MB 余量，`DriveInfo.AvailableFreeSpace`）；下载中显示「已下 / 总量 / 连接数 / 速度」；**取消 = 暂停**（`.part.N` 保留，下次同一版本接着下，`Resumed` 会显示「续传」）；下完自动解压导入并删归档。
  - 「导入归档…」：选本地归档 → 校验扩展名 → 问「运行器 / 图形层」→ 导入。对应 m21977「手动已经覆盖到了吗」那条：不新增概念，只是让「添加路径」这类动作也吃得下压缩包。
- **限流要说人话**：GitHub API 匿名每小时 60 次，共享出口（代理 / NAT）很容易被算满。`ComponentSources.ListAssetsAsync` 现在把非 2xx 变成 `ComponentSourceException`，`DescribeFailure(403, …)` 给出「设 `LUNARWARD_GITHUB_TOKEN` 或给源配镜像前缀」的建议并附原文；UI 与 `--component-list` 都显示这段话，而不是笼统的「没有可用归档」。支持 `LUNARWARD_GITHUB_TOKEN` / `GITHUB_TOKEN` / `GH_TOKEN`。
- 新增 CLI `--component-list <源 id>`（打印名称 / MB / 版本），便于在无法用 GUI 时确认源是否可用。
- 验证：单测 119 → 142（`ArchiveExtractor` 往返/逃逸/取消/不支持格式、`DescribeFailure` 四种）；`cli-extra` 22 → 24；真机三种归档（`.tar.gz` / `.tar.xz` / `.tar.zst`）导入成功；真机「导入归档…」走 `kdialog` 选 `/tmp/wine-localtest-1.0.tar.gz` → 选「运行器」→ 磁盘出现 `components/wine/wine-localtest-1.0/{bin,manifest.json}`；真机「下载组件…」在 GymHub 限流的网络下正确显示 403 的说明文字（见 L-143）。
- 仍未做：下载队列（多组件排队）、镜像自动测速、断点续传的「暂存列表」界面（当前靠分片文件名隐式续传）。
