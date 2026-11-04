# 引用与致谢

Lunarward 是 [Starward](https://github.com/Scighost/Starward) 的 Linux 移植，代码、UI 与业务逻辑大量
来自上游；同时使用了以下第三方项目。本文件记录「引用了谁、以什么方式引用、许可证是什么」，方便
审查与后续同步。

- 本项目遵循上游的 **MIT** 许可，见 [`LICENSE`](./LICENSE)（Copyright 2023 Scighost）。
- 本项目是**非官方**移植，与米哈游 / HoYoverse、Starward 作者均无隶属关系。

## 一、代码来源

| 项目 | 许可证 | 与本项目的关系 |
| --- | --- | --- |
| [Starward](https://github.com/Scighost/Starward) | MIT | UI（XAML）、ViewModel、游戏数据、抽卡记录、截图等绝大部分业务代码的来源。`src/Starward/` 原样保留，Linux 版在其上以 `src/Lunarward/` + `*.Linux.cs` 分部类的方式移植；详见 [`docs/linux-port/04-porting-matrix.md`](./docs/linux-port/04-porting-matrix.md)。 |
| [an-anime-team](https://github.com/an-anime-team) 系列启动器 | GPL-3.0 | **仅作设计参考**（wine / DXVK / wineprefix 的组织方式、游戏启动参数）。未复制其代码，本项目与 GPL 代码不混合。 |

> 移植基线：上游 Starward 提交 `df8ae07b5ad4c9c6cb70d288488ce8073ce01478`（2026-10-12，
> "filecache powered by fusioncache"）。本仓库历史为重新初始化的单提交历史，上游提交号仅作追溯用。

## 二、直接依赖（编译期，均通过 NuGet 引入）

| 依赖 | 许可证 | 用途 |
| --- | --- | --- |
| [Uno Platform](https://github.com/unoplatform/uno)（`Uno.Sdk`，Skia + X11 后端） | Apache-2.0 | 在 Linux 上承载上游的 WinUI 3 XAML 与控件树 |
| [Hi3Helper.Sophon](https://github.com/CollapseLauncher/Hi3Helper.Sophon) | MIT | 米哈游 Sophon 安装器协议的清单解析、分块下载与 HDiffPatch 差分应用 |
| [ZstdSharp.Port](https://github.com/oleg-st/ZstdSharp) | MIT | 纯托管 zstd 解压（安装器的 chunk 压缩） |
| [Scighost.WinUI](https://github.com/Scighost/Scighost.WinUI) | MIT | 上游使用的 `CachedImage` 等控件（Linux 下有替换实现） |
| [LibVLCSharp](https://github.com/videolan/libvlcsharp) + libVLC | LGPL-2.1 | 视频壁纸播放（通过 Uno 传递依赖引入；libVLC 由系统包提供，不打包） |
| [SkiaSharp](https://github.com/mono/SkiaSharp) | MIT | 图像解码、截图看图、视频帧合成 |
| [Tmds.DBus](https://github.com/tmds/Tmds.DBus) | MIT | 托盘 StatusNotifierItem / DBus 菜单 |
| [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) / [CommunityToolkit.WinUI](https://github.com/CommunityToolkit/WindowsCommunityToolkit) | MIT | MVVM、控件与行为 |
| [FusionCache](https://github.com/ZiggyCreatures/FusionCache) | MIT | 上游的本地缓存（`filecache`） |
| [Serilog](https://github.com/serilog/serilog)（`Serilog.Extensions.Logging`、`Sinks.File`） | Apache-2.0 | 日志 |
| [Dapper](https://github.com/DapperLib/Dapper) + `Microsoft.Data.Sqlite` | Apache-2.0 / MIT | 本地 SQLite 数据访问 |
| [MiniExcel](https://github.com/mini-software/MiniExcel) | Apache-2.0 | UIGF 抽卡记录导入导出 |
| [NuGet.Versioning](https://github.com/NuGet/NuGet.Client) | Apache-2.0 | AUR 版本比较（更新检查） |
| `System.IO.Hashing`、`Microsoft.Extensions.*` | MIT | MD5/XXH、配置与 HTTP |

## 三、运行时依赖（系统提供，不打包）

| 组件 | 用途 | 缺失时的行为 |
| --- | --- | --- |
| wine / Proton / umu-launcher | 运行 Windows 游戏 | 启动前预检直接报错 |
| DXVK / VKD3D-Proton | 图形转换层 | 可选，未安装则用 wine 自带 d3d |
| `xdg-open`（或 `gio`） | 打开外部链接、目录 | 回退到直接报错日志 |
| `xdotool` | X11 下唤回游戏窗口 | 无法唤回，其余功能正常 |
| `spectacle` / `grim` / `gnome-screenshot` | 截图 | 依次降级，全部缺失则提示 |
| `kdialog` / `zenity` | 文件选择对话框 | 依次降级 |
| `python3` + `python-gobject` + WebKitGTK | 抽卡记录登录助手窗口 | 无法登录，其余功能正常 |
| `libvlc` | 视频壁纸 | 静默退回静态壁纸 |

## 四、资源与商标

- 游戏图标、启动器横幅、背景图、公告等内容由游戏客户端或官方 CDN 提供，**不随本仓库分发**；
  相关版权归米哈游 / HoYoverse 所有，本项目仅按官方接口取用。
- 上游 Starward 自带的图标与品牌资源版权归其作者，本仓库仅因移植沿用。
- 「原神」「崩坏：星穹铁道」「绝区零」「崩坏3」等名称与标识为米哈游 / HoYoverse 的商标，
  本项目与官方无关联。
