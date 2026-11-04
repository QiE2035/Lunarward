# 打包

给发行版用的安装产物：桌面入口、图标、Arch（AUR 风格）PKGBUILD。

## Arch / CachyOS

```bash
cd packaging
makepkg -si          # 构建并安装（会拷到 /usr/lib/lunarward）
```

注意事项：

- **必须在 `packaging/` 目录里执行**，`source=()` 指向本仓库的 `linux-port` 分支；发布到 AUR 时改成
  `source=("git+https://github.com/<你>/Lunarward.git#branch=main")` 并把 `pkgver` 交给 `pkgver()`。
- 构建脚本 `cd` 到 `src/Lunarward` 再 `dotnet publish`：`dotnet` 是**从当前工作目录**向上找 `global.json` 的，
  仓库根那份钉的是上游要求的 SDK `10.0.400`，直接 `dotnet publish src/...` 会报 `A compatible .NET SDK was not found`。
- 产物是 framework-dependent，运行时依赖 `dotnet-runtime`（10.x）。想自包含改 `--self-contained true`
  （体积约 +90MB，但不再依赖系统 dotnet）。

## 运行时可选依赖

| 组件 | 用在哪 | 缺失时行为 |
| --- | --- | --- |
| `wine` / Proton | 启动游戏 | 预检直接失败并给出修法，不会静默启动失败 |
| `libvlc` | 视频壁纸 | 回退静态壁纸（D33：诚实降级） |
| `xdotool` | 全局热键唤回最小化窗口 | 回退 `Window.Activate()`，最小化后可能唤不回 |
| `webkit2gtk-4.1` + `python-gobject` | 米游社/HoYoLAB 登录 | 登录页提示助手不可用 |
| `kdialog` 或 `zenity` | 选择文件/目录 | 对话框退化为手动输入路径 |
| `spectacle` / `grim` / `gnome-screenshot` | 截图 | Alt+D 提示没有可用截图工具 |

## AppImage

```bash
./packaging/build-appimage.sh          # 装了就 appimagetool 就顺带出 .AppImage
SKIP_APPIMAGE=1 ./packaging/build-appimage.sh   # 只出 AppDir（build/Lunarward.AppDir）
```

AppDir 已经验证可直接运行（`build/Lunarward.AppDir/AppRun --game hk4e_cn`）。要产出 `.AppImage`
需要 `appimagetool`；没有时脚本会打印手动下载命令。AppImage 是 **framework-dependent** 的，
目标机仍需 `dotnet-runtime`；想要真正免依赖请把 `--self-contained false` 改成 `true`。

## 其他发行版

`makepkg` 只是最省事的一条路。`dotnet publish -c Release -r linux-x64 --self-contained false` 的输出目录
（约 100MB，含 `Assets/` 与各语言资源）本身就是可直接运行的目录树，放进 `/opt/lunarward` 再配
`packaging/lunarward.desktop` 与 `packaging/lunarward.png` 即可；AppImage 打包留待 M4。
