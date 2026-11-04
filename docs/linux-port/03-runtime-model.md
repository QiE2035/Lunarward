# 运行环境数据模型（Runtime Model）

- **状态**：v0.1（字段随 M1 实现可能调整）
- **最后更新**：2026-11-01
- **覆盖需求**：R-02 / R-03 / R-04 / R-05 / R-06 / R-07 / R-09
- **实现位置**：`src/Lunarward.Runtime/Models/`

---

## 1. 概念关系

```
Component ──引用──► Profile ──绑定──► GameEntry ──解析──► LaunchPlan ──执行──► LaunchSession
    ▲                 │                                        │                    │
    │                 ├─ Runner                              └─ Companion[] ──────┘
    └─ 来源指针        ├─ PrefixRef                                               └─ LaunchReport
                      ├─ Env / Wrappers / Hooks
                      └─ Companion[]
```

- **Component** 是「磁盘上的一份产物」，内容寻址、不可变。
- **Profile** 是「一份配方」，引用组件版本，可被多个游戏共享。
- **GameEntry** 是「一个游戏」，覆盖少量游戏级差异。
- **LaunchPlan** 是「一次解析结果」，纯数据、可序列化、可预览。
- **LaunchSession** 是「一次真实启动」，记录计划快照与运行时事实。

---

## 2. Component

### 2.1 类型

| `kind` | 含义 | 典型 `sourceType` |
|---|---|---|
| `wine` | wine 构建 | 系统包、Proton、自编译 |
| `proton` | Proton 构建（本质是 wine + 脚本） | Steam、`compatibilitytools.d`、GE-Proton |
| `dxvk` | D3D9/10/11 → Vulkan | 本地导入、GitHub release |
| `vkd3d-proton` | D3D12 → Vulkan | 本地导入、GitHub release |
| `dxvk-nvapi` | NVIDIA DLSS/NGX 兼容层 | 本地导入 |
| `prefix-template` | 前缀模板（tar.zst 或目录） | 用户导出 |

### 2.2 模型

```jsonc
{
  "id": "wine-ge-proton-10-9",
  "kind": "wine",
  "name": "GE-Proton10-9 (wine)",
  "version": "10.9",
  "sourceType": "external",              // external | imported | downloaded | template
  "path": "/home/u/.local/share/Steam/compatibilitytools.d/GE-Proton10-9",
  "binary": {                            // 解析后的关键可执行
    "loader":    "files/bin/wine64",
    "boot":      "files/bin/wineboot",
    "server":    "files/bin/wineserver",
    "arch":      "win64"                 // win64 | win32 | unknown
  },
  "fingerprint": {
    "algo": "sha256-tree-1",             // 目录树哈希（仅元数据 + 关键文件）
    "value": "9f1c…",
    "sizeBytes": 812345678,
    "fileCount": 4211
  },
  "discoveredFrom": "steam",
  "managed": false,                      // false = 仅指针，不归本项目管理
  "createdAt": "2026-11-01T08:00:00+08:00",
  "lastVerifiedAt": "2026-11-01T08:00:00+08:00"
}
```

**规则**：

1. `fingerprint` 不要求全树哈希（太慢）：对 wine/proton 只取 `loader/boot/server` 三个二进制的 `mtime+size+sha256`，对 dxvk 取 `.dll` 集合。
2. `managed=false` 的组件在 UI 上标注「外部」，删除/移动由用户负责；启动时发现消失 → 该 Profile 标记 `unavailable`（R-05 判据 3）。
3. 组件索引文件 `components/index.json` 只存元数据，产物在 `components/<kind>/<id>/` 下。

---

## 3. Prefix

### 3.1 模式（D5）

| 模式 | 含义 | 磁盘 | 适用 |
|---|---|---|---|
| `template` | 基础模板 + 启动前克隆新实例 | 新目录（reflink） | **默认**：组合测试互不干扰 |
| `dedicated` | 本项目管理的固定前缀 | 固定目录 | 主力游戏 |
| `shared` | 多个 Profile 共用同一前缀 | 固定目录 | 省空间、共享已装组件 |
| `external` | 直接引用已有前缀（anime-game-launcher/sleepy-launcher/`~/.wine`） | 只读登记 | 迁移期 |
| `ephemeral` | 每次全新临时前缀，退出后丢弃 | `$XDG_CACHE_HOME` | 试毒/一次性验证 |

> 「可选隔离」的落地：用户**同一游戏**可以建两个 GameEntry 分别指向两个 Profile（一个 `dedicated`、一个 `template`），互不干扰；见 R-04 判据 1。

### 3.2 模型

```jsonc
{
  "mode": "template",
  "name": "genshin-ace-base",
  "templateId": "prefix-template-win64-fontfix",   // mode=template 时必需
  "path": "/home/u/.local/share/starward/prefixes/genshin-ace-base",
  "architecture": "win64",
  "prepare": {
    "ensureWineprefix": true,           // 若目录不存在则 wineboot -i
    "reflink": "auto",                  // auto|always|never → cp -a --reflink=auto
    "timeoutSeconds": 120
  },
  "lock": { "enabled": true },           // flock(<prefix>/.starward-lock)
  "healthCheck": {
    "enabled": true,
    "checks": ["system.reg", "drive_c/windows", "dxvk-presence"]
  }
}
```

**克隆成本目标**（R-04 判据 1）：btrfs/XFS 上 reflink 克隆 < 2s；ext4 退化为普通复制，UI 必须提前提示预估体积与时延。

---

## 4. Profile（运行环境）

### 4.1 完整示例

```jsonc
{
  "id": "profile-genshin-ace",
  "name": "原神 · ACE + GE-Proton",
  "description": "绝区零/原神共用；DXVK 2.7，启用 ACE 兼容参数",
  "runner": {
    "type": "wine",                     // wine | proton | umu | custom
    "componentId": "wine-ge-proton-10-9",
    "binary": null,                     // custom 时使用
    "args": ["-norestore"],
    "env": {
      "WINEDLLOVERRIDES": "winemenubuilder.exe=d",
      "DXVK_HUD": "0",
      "WINEESYNC": "1",
      "WINEFSYNC": "1"
    }
  },
  "graphics": {
    "dxvk":         { "componentId": "dxvk-2.7.1", "installToPrefix": true },
    "vkd3dProton":  { "componentId": null, "installToPrefix": false },
    "dxvkNvapi":    { "componentId": null }
  },
  "prefix": { /* 见第 3 节 */ },
  "wrappers": [                         // 由外到内 exec 顺序
    { "kind": "gamescope", "enabled": true,
      "args": ["-W", "2560", "-H", "1440", "-f", "-r", "120"] },
    { "kind": "gamemoderun", "enabled": true, "args": [] },
    { "kind": "mangohud", "enabled": false, "args": [] },
    { "kind": "custom", "command": "/usr/bin/env", "args": ["MANGOHUD=1"], "enabled": false }
  ],
  "cwd": "{game.installPath}",
  "command": "{game.executable}",       // 可被 GameEntry 覆盖
  "args": [],
  "hooks": {
    "preLaunch":  [ { "command": "/usr/bin/notify-send", "args": ["-a", "Starward", "启动中"] } ],
    "postExit":   [ { "command": "/usr/bin/true" } ]
  },
  "companions": [ /* 见第 6 节 */ ],
  "gameOutput": "silent",                 // 游戏本体输出可见性：silent|panel|terminal（D27，见 09 §5）
  "shell": {
    "program": "zsh",                    // bash|zsh|fish|custom
    "args": ["-i"],
    "extraEnv": { "STARWARD_SHELL": "1" }
  },
  "tags": ["ace", "genshin", "experimental"],
  "createdAt": "2026-11-01T08:00:00+08:00",
  "updatedAt": "2026-11-01T08:00:00+08:00"
}
```

### 4.2 Runner 类型

| `type` | 启动方式 | 说明 |
|---|---|---|
| `wine` | `<wine>/bin/wine <args> <exe>` | 最通用；本项目主要路径 |
| `proton` | `<proton>/proton run <exe>` | 自动带 `STEAM_COMPAT_*`；**注意** Proton 的 `run` 会再 fork，见下 |
| `umu` | `umu-run <exe>`（`GAMEID`/`PROTONPATH` 入 env） | 等价便捷；GPL 依赖只做进程调用（D16） |
| `custom` | 用户给完整命令模板 | 逃生舱 |

**重要**：`proton run` 与 `umu-run` 内部会 fork/exec，可能破坏「游戏是 wine 第一个进程」。因此：

- `runner.type=proton` 时**默认**取 `proton/files/bin/wine` 直连（与 Proton 官方脚本等价的关键 env 由 Starward 注入），并在 UI 上标注「直连模式」；
- `runner.type=umu` 保留为「实验性」，启动报告中标注「可能多一层父进程」。

### 4.3 环境变量合并优先级（后者覆盖前者）

```
1. 进程继承环境（用户 shell/Autostart）
2. Starward 基础注入：WINEPREFIX、WINEARCH、WINEDLLOVERRIDES(默认)、STARWARD_SESSION_ID
3. Runner 内置：Proton/umu 需要的 STEAM_COMPAT_* 等
4. Profile.runner.env
5. Profile.wrappers[*].env（按由外到内顺序）
6. GameEntry.env
7. LaunchPlan.env（最终快照）
```

**变量展开**：值中允许 `{game.installPath}`、`{game.executable}`、`{game.name}`、`{runner.binDir}`、`{prefix.path}`、`{home}`、`{env:HOME}`。展开发生在**生成 LaunchPlan 时**，报告中保存展开后的结果与原始模板。

**路径规范化**：所有路径字段同时支持 `~` 与相对路径（相对 `cwd`）；解析后统一 `Path.GetFullPath` 并记录。

---

## 5. GameEntry

```jsonc
{
  "id": "hk4e_cn",
  "name": "原神（国服）",
  "gameBiz": "hk4e_cn",
  "installPath": "/home/u/games/hk4e_cn",
  "executable": "YuanShen.exe",          // 或 GenshinImpact.exe
  "profileId": "profile-genshin-ace",
  "env": {
    "STARWARD_GAME": "genshin"
  },
  "args": [],
  "preferredPrefixMode": null,           // 覆盖 Profile.prefix.mode（可选）
  "companions": [
    {
      "name": "FPS 解锁补丁",
      "enabled": true,
      "companion": true,                 // ←「伴随启动」开关：随游戏自动执行
      "mode": "inject",
      "trigger": { "type": "after_delay", "delayMs": 4000 },
      "target": { "processName": "YuanShen.exe" }
    }
  ],
  "playtime": { "total": 123456, "lastLaunchAt": "2026-11-01T08:00:00+08:00" },
  "installedVersion": "5.1.0"
}
```

**游戏级覆盖**只允许：`env`、`args`、`command`、`prefix.mode`、`companions`。其余一律跟随 Profile，避免配置漂移。

---

## 6. Companion（伴随启动）

### 6.1 模型

```jsonc
{
  "name": "Genshin UID Patch",
  "enabled": true,
  "companion": true,                     // false = 仅手动「后启动」
  "mode": "inject",                      // process | inject | script
  "command": "/home/u/tools/uid-patch/tool",
  "args": ["--target", "{game.executable}"],
  "cwd": null,
  "env": {},
  "trigger": { "type": "on_window_mapped", "windowMatch": { "titleContains": "原神" } },
  "timeoutMs": 15000,
  "onFailure": "notify",                 // ignore | notify | abort-following
  "runOnce": true,
  "output": "follow",                    // silent|panel|terminal|follow（D27；follow=跟随全局默认）
  "logFile": "{report.dir}/companion-uid-patch.log"
}
```

### 6.2 触发类型

| `type` | 参数 | 语义 |
|---|---|---|
| `on_game_started` | — | agent 认为主进程已进入可交互期（见 6.3） |
| `after_delay` | `delayMs` | 自 `on_game_started` 起延时 |
| `on_window_mapped` | `windowMatch{titleContains,classContains}` | 匹配窗口出现（X11/`wlr-foreign-toplevel`；失败降级 `on_game_started + 1s`） |
| `on_log_match` | `path` + `regex` | 监听 wine 输出或指定日志，命中即触发 |
| `on_game_exit` | — | 游戏退出后（清理类工具） |

### 6.3 `on_game_started` 的判定

依次尝试，命中即认为「启动」：

1. `pidfd` 存活 + 主进程 `stat` 状态非 `Z`；
2. 出现首个属于该游戏进程树的窗口（X11 `_NET_WM_PID` / `wlr-foreign-toplevel`）；
3. 超时上限 `min(Profile.timeout, 60s)` 后按「已启动但不确信」继续执行 `after_delay` 类条目，并在报告中标注 `startedConfidence=weak`。

### 6.4 注入型 vs 独立进程型

| `mode` | 谁负责注入 | 本项目负责 |
|---|---|---|
| `inject` | 工具自己（通过 `ptrace`/`dlopen`/游戏内 DLL 等） | 只负责**时机**与**环境一致**，不注入、不修改游戏内存 |
| `process` | 不涉及 | 直接 spawn，记录退出码 |
| `script` | 用户脚本 | 只提供 env/变量 |

> 明确边界：本项目**不实现**内存修改/封包注入能力，只做编排。这是法律与反作弊风险的最低线（见 `06-risks.md` R-09）。

### 6.5 输出可见性（D27）

三级 × 三作用域的完整模型见 `09-profile-and-companion-ui.md` §5，这里只记引擎侧约定：

| 约定 | 内容 |
|---|---|
| 捕获 | **永远开启**。`stdout`/`stderr` 用 `ProcessStartInfo.RedirectStandard*` 逐行读取，同时写文件与环形缓冲（5000 行/进程，超出丢最旧） |
| 级别 | `silent` 仅落盘；`panel` 额外推送事件到 UI；`terminal` 不重定向，交给终端窗口 |
| 真源 | **日志文件是唯一真源**，`panel` 只是视图；UI 重建历史时从文件读 |
| 脱敏 | 面板默认替换 `(?i)(token|secret|password|passwd|cookie|auth|session_?id)` 的值为 `***`；原文写入磁盘（目录权限 0700） |
| 终端 exec | `terminal --exec wine …` 必须**直 exec**，不套 `bash -c`；否则真实子进程 PID 需要 `/proc/<pid>/task/*/children` 或 agent 上报反查（见 09 §6） |
| 会话隔离 | 游戏进程 `setsid`（新 session），启动器忽略 `SIGHUP`；否则关闭终端会连带终止游戏 |
| 退出显示 | 外部终端窗口在子进程退出后**保留**，标题追加 `[已退出 code=N]`，由用户手动关闭 |


---

## 7. LaunchPlan

```jsonc
{
  "planId": "lp-20261101-083000-7f3a",
  "createdAt": "2026-11-01T08:30:00+08:00",
  "game": { "id": "hk4e_cn", "name": "原神（国服）", "installPath": "…", "executable": "YuanShen.exe" },
  "profile": { "id": "profile-genshin-ace", "name": "…", "snapshotHash": "sha256:…" },
  "runner": { "type": "wine", "binary": "…/bin/wine", "componentId": "wine-ge-proton-10-9" },
  "prefix": {
    "mode": "template",
    "path": "…/prefixes/hk4e_cn-20261101-083000",
    "templateId": "prefix-template-win64-fontfix",
    "clonedFrom": "…/templates/prefix-template-win64-fontfix",
    "reflink": true
  },
  "chain": [                              // 从外到内的 exec 链，可直接复制粘贴
    { "argv": ["gamescope", "-W", "2560", "-H", "1440", "-f", "-r", "120"] },
    { "argv": ["gamemoderun"] },
    { "argv": ["…/bin/wine", "YuanShen.exe"] }
  ],
  "argv": ["gamescope", "…"],
  "env": { "WINEPREFIX": "…", "DXVK_HUD": "0" },
  "cwd": "/home/u/games/hk4e_cn",
  "companions": [ /* 展开后的伴随项 */ ],
  "warnings": [
    { "code": "external-component", "message": "wine 来自 Steam compatibilitytools.d，移动该目录会导致启动失败" }
  ],
  "hash": "sha256:…"
}
```

**规则**：

1. `LaunchPlan` 是**唯一**执行入口：GUI「启动」与终端「复制命令」都使用同一结果，保证可复现（R-06 判据 2）。
2. `hash` 覆盖 `argv/env/cwd/prefix`，用于报告去重与「同配置对比」。
3. `warnings` 不阻断启动，`errors` 才阻断（见第 9 节）。

---

## 8. LaunchSession / LaunchReport

```jsonc
{
  "sessionId": "s-20261101-083000-7f3a",
  "planId": "lp-…",
  "planHash": "sha256:…",
  "startedAt": "2026-11-01T08:30:00+08:00",
  "endedAt": "2026-11-01T09:12:33+08:00",
  "pid": 41234,
  "exit": { "code": 0, "signal": null, "classification": "clean" },
  "timeline": [
    { "at": "08:30:00.010", "phase": "precheck", "detail": "ok" },
    { "at": "08:30:00.240", "phase": "prefix-cloned", "detail": "reflink=auto 0.21s" },
    { "at": "08:30:00.900", "phase": "dxvk-installed", "detail": "dxvk-2.7.1 → system32" },
    { "at": "08:30:01.020", "phase": "exec", "detail": "gamescope" },
    { "at": "08:30:07.400", "phase": "game-started", "detail": "confidence=strong" },
    { "at": "08:30:11.500", "phase": "companion", "detail": "uid-patch exit=0" }
  ],
  "components": [
    { "kind": "wine", "id": "wine-ge-proton-10-9", "fingerprint": "9f1c…" },
    { "kind": "dxvk", "id": "dxvk-2.7.1", "fingerprint": "ab12…" }
  ],
  "warnings": [],
  "redaction": { "applied": ["steam-id", "token"], "note": "环境变量中的 *_TOKEN 已脱敏" }
}
```

**归因分类**（`exit.classification`）：

| 值 | 判定依据 |
|---|---|
| `clean` | 退出码 0 |
| `antcheat-blocked` | 游戏日志/ACE 日志出现反作弊拒绝关键字 |
| `wine-crash` | 非 0 退出 + 存在 wine 错误日志 |
| `missing-component` | 启动前检查失败 |
| `prefix-locked` | 同前缀被占用 |
| `timeout` | agent 超过 `maxSessionMinutes` 未见游戏窗口 |
| `killed` | 被 SIGTERM/SIGKILL |

**脱敏规则**（R-08 判据 3）：`env` 中键名匹配 `(?i)(token|secret|password|passwd|cookie|auth|session_?id)` 的值替换为 `***`；cookie 文件只记录路径不记录内容。

---

## 9. 校验与错误码

`LaunchPlanBuilder` 在校验阶段产生结构化诊断，供 UI 展示：

| 代码 | 级别 | 含义 | 建议动作 |
|---|---|---|---|
| `E-PREFIX-LOCKED` | error | 前缀被其他会话占用 | 显示占用会话/进程 |
| `E-RUNNER-MISSING` | error | 运行器二进制不存在 | 重新扫描组件 |
| `E-EXE-MISSING` | error | 游戏可执行文件不存在 | 修正安装路径 |
| `E-WINE-ARCH-MISMATCH` | error | win32 前缀配 64 位 exe（或反之） | 提示切换前缀架构 |
| `E-DXVK-NOT-INSTALLED` | error | 声明要 DXVK 但前缀内无 d3d11/dxgi | 一键就位 |
| `W-EXTERNAL-COMPONENT` | warning | 组件是外部指针，可能被外部工具改动 | 提示「导入」以固化 |
| `W-PROTON-FORK` | warning | 使用 `proton run`/`umu` 可能多一层父进程 | 建议直连模式 |
| `W-ACE-UNKNOWN` | warning | 反作弊状态未知 | 见 `06-risks.md` |
| `W-NO-REFKLINK` | warning | 文件系统不支持 reflink，克隆将复制 | 提示体积/时间 |

---

## 10. 与既有启动器的迁移映射

| 来源 | 可读取 | 映射 |
|---|---|---|
| anime-game-launcher | `~/.local/share/anime-game-launcher/config.json`、`prefix/`、`dxvks/`、`wine/` | prefix → `external`；dxvk/wine → `managed=false` 组件 |
| sleepy-launcher | 同上结构 | 同上 |
| Lutris | `~/.local/share/lutris/`、`games/*.yml` | 提取 wine/prefix/env → 生成 Profile 草稿 |
| Bottles | `~/.var/app/...`/`~/.local/share/bottles/bottles/*` | 提取 bottle 前缀 → `external` |
| Heroic | `~/.config/heroic/` | 提取 wine 版本 |
| Steam | `~/.local/share/Steam/compatibilitytools.d/*` | 扫描为 proton/wine 组件 |

迁移一律**生成草稿并预览**，不直接写配置（R-05 判据 2）。
