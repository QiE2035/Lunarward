# Lunarward 常见问题

> 本文面向使用者。设计层面的取舍见 `docs/linux-port/`，实现替身与缺口见
> `docs/linux-port/12-shims-and-gaps.md`。

## 1. 启动器能做什么、不能做什么

能做：识别已安装的米哈游游戏、用 wine/Proton 启动、管理多套「运行配置（Profile）」、
管理每个游戏的后启动程序（含伴随启动）、调试 Shell、抽卡记录、游戏设置、截图捕获与图库、
托盘常驻、`lunarward://` 协议、启动前预检与启动报告。

不能做（Linux 上确实做不到或还没做）：游戏内悬浮窗（帧率/时钟）、截图图库的日期分组与
游戏内截图悬浮、自更新下载（走发行版包管理器）、Windows 版功能（本移植只保证 Linux）。

## 2. 第一次运行很慢，正常吗？

正常。首次启动如果选的是 `dedicated`/`shared` 前缀模式，会跑一次 `wineboot` 初始化前缀，
实测约 **18 秒 / 630 MB**。用 `template` 模式克隆已有前缀则通常 **0.1–0.3 秒**（btrfs/XFS 上
走 reflink），这也是推荐做法：先手工把一套前缀调好，再「把现有前缀做成模板」。

## 3. 提示「启动前检查未通过」

点开始游戏会弹窗逐项列出检查结果，格式是「✓ / ! / ✕ 项目 — 说明」，失败项下面还有一行修法。
常见几项：

| 项 | 失败原因 | 怎么修 |
| --- | --- | --- |
| 游戏可执行文件 | 路径不对 / 游戏没装 | 游戏设置里重新指定安装目录 |
| Wine 运行器 | 配置里写死的路径不存在 | 重新选择运行器，或清空路径改用自动探测 |
| 前缀 | `external` 模式指到的目录不存在 | 改路径，或换成 `template`/`dedicated` |
| 前缀可写 | 权限不足 | 检查目录属主 |
| 前缀占用 | 同一前缀已有游戏在跑 | 等它结束；或给这套配置单独前缀 |
| 图形组件 | 配置指定了 DXVK 但没安装 | 在运行环境页「安装到前缀」，或取消勾选 |

想在不开界面的情况下复现同样的检查：

```bash
lunarward --preflight hk4e_cn ~/games/hk4e_cn/YuanShen.exe
```

## 4. 游戏启动后退出了 / 黑屏

先看启动报告：每次通过 Lunarward 启动都会写一份 JSON 到

```
~/.local/share/lunarward/logs/launch/<日期>-<游戏>.json
```

里面有最终命令行、`WINEPREFIX`、环境变量（已过滤敏感项）、预检结果。
几个高频原因：

- **反作弊（ACE）拒绝启动**：部分游戏在 wine 下需要额外配置，Lunarward 不会绕过反作弊，
  也不会替你做注入；这类情况只能等上游方案。
- **缺 DXVK**：图形组件没装进前缀时，游戏可能黑屏或崩溃。在运行环境页把 DXVK 装到该前缀。
- **多层 fork**：不要让 `gamemoderun`/`mangohud`/`gamescope` 之外再套一层会 fork 的包装器，
  游戏必须是 wine 的第一个进程，否则容易崩。Lunarward 自己不会套 shell。
- **Proton 直调 wine 的环境**：Lunarward 会按 Proton 目录补 `WINEDLLPATH`/`LD_LIBRARY_PATH`，
  如果你手工改过命令行，注意别把这些覆盖掉。

## 5. 视频壁纸不显示 / 黑屏

视频壁纸依赖系统的 `libvlc`（VLC 的库）。没装或不可用时，Lunarward **回退到静态壁纸**，
不会假装在播。装 `vlc` 后再重启即可。

另外 Wayland 会话下如果出现黑屏，`WEBKIT_DISABLE_DMABUF_RENDERER=1` 只与内嵌网页/登录窗有关，
壁纸不受影响；如果整个窗口都黑，先看 `~/.local/share/lunarward/log/Starward_*.log` 有没有
渲染相关异常。

## 6. 登录窗口打不开 / 打了没反应

登录走独立窗口（系统 WebKitGTK），需要：

```bash
# Arch
sudo pacman -S webkit2gtk-4.1 python-gobject
```

没有这些依赖时，登录页会明确提示助手不可用，不会静默失败。

## 7. 全局热键（Alt+S / Alt+D）没用

- 全局热键依赖 X11。**Wayland 原生会话下拿不到全局键盘状态**，UI 会显示注册失败。
  可以在 `GDK_BACKEND=x11` 下运行（XWayland），或暂时不用热键。
- 合成按键（例如脚本用 `xdotool key` 12ms 的按压）可能抓不到；真人按键没问题。
- 最小化后按 Alt+S 唤回依赖 `xdotool`；没装时可能唤不回，托盘菜单「显示主窗口」是同样的路径。

## 8. 托盘没有图标

托盘用 `StatusNotifierItem`（freedesktop 标准）。KDE 正常；GNOME 需要
`gnome-shell-extension-appindicator`；某些面板/自定义 conky 不显示 SNI 是面板那边的问题。
用 `busctl --user call org.kde.StatusNotifierItem-<pid>-1 ...` 能确认服务在跑。

## 9. 截图没反应

Alt+D 依次尝试 `spectacle -b -n -f`、`grim`、`gnome-screenshot`。三个都没有时会提示
「没有可用的截图工具」。默认存到 `~/图片/Lunarward`。

## 10. 游戏没被自动识别

自动识别只看这些位置（可执行文件名 + 目录名）：

```
~/games  ~/Games  ~/Games/miHoYo  Steam/steamapps/common  $LUNARWARD_GAME_ROOTS
```

目录名要和 gameId 一致（`hk4e_cn`、`hkrpg_cn`、`nap_cn` 等），否则会漏检——这是刻意的：
`YuanShen.exe` 同时属于国服和 B 服，只按 exe 名会认错。识别不到就在「自动查找」里手工指定。

## 11. 怎么排查自己跑的命令

```bash
lunarward --launch-plan ~/games/hk4e_cn/YuanShen.exe "-popupwindow"   # 只解析不启动
lunarward --preflight hk4e_cn ~/games/hk4e_cn/YuanShen.exe             # 六项检查
lunarward --companion-plan hk4e_cn                                     # 后启动程序命令行
lunarward --output-tail 200                                            # 最近输出
lunarward --url-plan "lunarward://startgame?biz=hk4e_cn"               # 协议展开
lunarward --graphics-list                                              # 检测到的图形组件
lunarward --update-check                                               # AUR 版本检查
```

## 12. 报问题时请带上

1. 启动报告 `~/.local/share/lunarward/logs/launch/*.json`（最近一次）；
2. `~/.local/share/lunarward/log/Starward_*.log` 末尾 100 行；
3. `lunarward --preflight <gameKey> <exe>` 的输出；
4. 发行版、桌面环境（X11/Wayland）、wine/Proton 版本。

启动报告与日志里 token/cookie/auth 一类字段会被打码，但仍建议自己扫一眼再贴出来。
