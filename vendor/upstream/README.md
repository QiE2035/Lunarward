# vendor/upstream — 上游 `Starward.*` 自研包源码镜像（只读）

本目录是**上游源码的镜像**，用于「补全源码后重新决策」（D21 修订）与后续移植参考。
**不是本项目的源码**，不参与任何构建，不修改上游文件。

## 来源

| 项 | 值 |
|---|---|
| 仓库 | `https://github.com/Scighost/Starward` |
| 分支 | `large-resource` |
| 提交 | `302832787ad166b71971cd33bb5fac9d96c2ab19` |
| 拉取时间 | 2026-11-01 |
| 许可 | 主仓 `LICENSE` 为 MIT（Copyright 2023 Scighost）；**该分支未单独放置 LICENSE 文件**（见 Q-032） |

## 内容

| 路径 | 内容 | 文件数 | 体积 |
|---|---|---:|---:|
| `Starward.Codec/` | AVIF（libavif 1.3.0）、JPEG XL（libjxl 0.11.1）、UltraHDR（libultrahdr 1.4.0）、ICC（Little-CMS 2.17）、PNG chunk 解析、`VP9Decoder`（Windows MFT）的 **C# P/Invoke 包装层** + `Starward.Codec.csproj` | 195 | ~1.6 MB |
| `Starward.NativeLib/` | `SharpSevenZip`（fork，COM + `kernel32!LoadLibrary` 加载 `7z.dll`）、`Snap.HPatch`（纯 `LibraryImport` 调 libhpatch）+ `Starward.NativeLib.csproj` | 35 | ~0.2 MB |
| `Starward.Assets/Assets/` | 字体（`Segoe Fluent Icons.ttf`、`PhotosIcons.ttf`、`XboxFluent2.ttf`）、图标、CSS、模板 | 166 | ~11 MB |
| `MANIFEST.sha256.txt` | `Codec`/`NativeLib` 全部文件 SHA256 + `Assets` 文件清单 | — | — |

## 刻意**未**纳入的内容

| 上游路径 | 原因 |
|---|---|
| `Starward.Codec/runtimes/win-{x64,x86,arm64}/native/*` | Windows DLL（`avif.dll`、`jxl.dll`、`uhdr.dll`、`lcms2.dll`、`brotli*.dll`、`libyuv.dll`、`VP9DecoderMFT.dll`、`*.exe`），Linux 上不可用；对应 Linux 库走系统包 / AUR / 自建（Q-033） |
| `Starward.NativeLib/runtimes/win-*/native/*` | 同上（`7z.dll`、`Snap.HPatch.dll`） |
| `Starward.Codec/VP9Decoder/` | 仅 Windows MFT 使用，移植时删除（D21） |
| `VP9DecoderMFT/`（分支顶层） | C++/MFT 工程，与 Linux 无关 |
| `img/`、`logo.ai` | 站点图与设计源文件，与移植无关 |

`Assets` 部分因体积较大直接从本机 NuGet 缓存复制（`~/.nuget/packages/starward.assets/0.6.5/contentFiles/any/any/Assets`），与仓库内容一致。

## 使用约束

1. 本目录**只读**：任何移植改动都落在新项目（如 `src/Lunarward.Codec`），不回溯修改这里；
2. 纳入构建前需先确认许可（Q-032），并在 `THIRD-PARTY-NOTICES` 记明上游与原生库许可；
3. 上游更新时整体替换本目录并重新生成 `MANIFEST.sha256.txt`，不做手工合并。
