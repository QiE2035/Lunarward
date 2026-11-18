# 打包

给 Arch 系用的安装产物（PKGBUILD）、桌面入口、图标，以及 AppImage 的构建脚本。

## 三份 PKGBUILD 各管什么

| 文件 | source | 用途 |
| --- | --- | --- |
| `PKGBUILD` | `git+https://…#branch=main` | **仓库里的构建模板**。在 `packaging/` 里 `makepkg -si` 就能构建并安装当前 main |
| `aur/lunarward/PKGBUILD` | `git+https://…#tag=v$pkgver` | **AUR 稳定版**，跟 tag。仓库里没有对应 tag 时构建会失败，发布前先打 tag |
| `aur/lunarward-git/PKGBUILD` | `git+https://…#branch=main` | **AUR 开发版**，跟最新提交，`pkgver()` 自动带 `r<提交数>.g<短 hash>` |

要构建**本地未推送**的改动，把 `PKGBUILD` 的 source 换成：

```bash
source=("$pkgname::git+file://${startdir}/..#branch=main")
```

## Arch / CachyOS

```bash
cd packaging
makepkg -si          # 构建并安装（装到 /usr/lib/lunarward，入口 /usr/bin/lunarward）
```

依赖清单由 `scripts/test/lint-pkgbuild.sh` 静态校验（扫源码里的 P/Invoke 库名与外部命令，反查 depends）：改了原生依赖就同步三份 PKGBUILD，否则 lint 会红。

```bash
bash scripts/test/lint-pkgbuild.sh
```

**硬依赖**（缺了就不可用，不能靠降级兜）：

| 包 | 谁要 |
| --- | --- |
| `dotnet-runtime>=10.0` | 框架依赖发布。Arch 上 `dotnet-runtime-bin` provides 它 |
| `fontconfig` | Skia 文本渲染（连带 freetype2 / expat / brotli / libpng / bzip2） |
| `libx11` | `X11KeyState` 的 P/Invoke（`libX11.so.6`） |
| `libxcb` | `X11Window` 的 P/Invoke（`libxcb.so.1`），窗口显隐与唤回 |
| `xdg-utils` | `OpenUrl` 走 `xdg-open`，协议注册走 `xdg-mime` |
| `webkitgtk-6.0` | 登录窗（D76） |

**不写进 depends 的**：`gtk3`（源码 0 引用，GirCore 用的是 Gtk-4.0 绑定，`webkitgtk-6.0` 会带 `gtk4`）、`zstd`（产物自带 `Lib/linux-x64/libzstd.so`）。

构建脚本 `cd` 到 `src/Lunarward` 再 `dotnet publish`：`dotnet` 是从**当前工作目录**向上找 `global.json` 的，仓库根那份钉的是 SDK `10.0.300`，在别处执行会报 `A compatible .NET SDK was not found`。

产物是 framework-dependent，运行时依赖 `dotnet-runtime`（10.x）。想自包含改 `--self-contained true`（体积约 +90MB，不再依赖系统 dotnet）。

## 运行时可选依赖

| 组件 | 用在哪 | 缺失时行为 |
| --- | --- | --- |
| `wine` / Proton | 启动游戏 | 预检直接失败并给出修法，不会静默启动失败 |
| `vlc` | 视频壁纸与视频预览 | 回退静态壁纸（D33：诚实降级） |
| `xdotool` | `libxcb` 不可用时唤回最小化窗口 | 回退 `Window.Activate()`，最小化后可能唤不回 |
| `webkit2gtk-4.1` + `python-gobject` | 登录窗的 python3 兜底路径 | 提示助手不可用（有 `webkitgtk-6.0` 时用不到） |
| `kdialog` / `zenity` | 选择文件/目录 | 对话框退化为手动输入路径 |
| `spectacle` / `grim` / `gnome-screenshot` | 截图 | 提示没有可用截图工具 |
| `xdg-desktop-portal` | 截图与文件选择走 portal（D75） | 回退到命令行工具 |

## 发版流程

1. 改 `src/Lunarward/Lunarward.csproj` 的版本号，写 `CHANGELOG.md`。
2. `git tag v0.1.1 && git push origin v0.1.1` —— **必须**，`aur/lunarward` 的 source 指向 `v$pkgver`，没有 tag 会 404。
3. 同步改 `aur/lunarward/PKGBUILD` 的 `pkgver` 与 `pkgrel`。
4. `makepkg --printsrcinfo > .SRCINFO`（在 `aur/lunarward/` 里），连同 PKGBUILD 一起推到 AUR。

## AppImage

```bash
./packaging/build-appimage.sh          # 装了 appimagetool 就顺带出 .AppImage
SKIP_APPIMAGE=1 ./packaging/build-appimage.sh   # 只出 AppDir（build/Lunarward.AppDir）
```

AppDir 已经验证可直接运行（`build/Lunarward.AppDir/AppRun --game hk4e_cn`）。要产出 `.AppImage`
需要 `appimagetool`；没有时脚本会打印手动下载命令。AppImage 是 **framework-dependent** 的，
目标机仍需 `dotnet-runtime`；想要真正免依赖请把 `--self-contained false` 改成 `true`。

## 其他发行版

`makepkg` 只是最省事的一条路。`dotnet publish -c Release -r linux-x64 --self-contained false` 的输出目录
（约 100MB，含 `Assets/` 与各语言资源）本身就是可直接运行的目录树，放进 `/opt/lunarward` 再配
`packaging/lunarward.desktop` 与 `packaging/lunarward.png` 即可。
