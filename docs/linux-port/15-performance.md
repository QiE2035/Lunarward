# 15 · 性能基线（M3）

> 目的：给后续优化一个**可比较的起点**，不是「达标证明」。目前没有上游 Windows 版同机对照，所以这些数字只用于回答"以后改坏了没有"。

## 测量方法

```bash
# 1) 启动到启动器页（选中原神）
env DISPLAY=:0 GDK_BACKEND=x11 ./bin/Release/net10.0-desktop/Lunarward --game hk4e_cn &

# 2) 每 5 秒采一次 RSS / 累计 CPU / 线程数
pid=$(pgrep -x Lunarward)
while sleep 5; do ps -o rss=,%cpu=,nlwp= -p $pid; done

# 3) 空闲瞬时 CPU（ps 的 %cpu 是生命周期均值，不能当瞬时值）
top -b -n 2 -d 2 -p $pid
```

环境：CachyOS / KDE Wayland（XWayland 运行）/ 1920x1080 @1.25 缩放 / btrfs / Ryzen + 独显，Skia 默认后端。

## 结果

| 时间 | Debug RSS | Debug 累计 CPU | Release RSS | Release 累计 CPU |
|---|---|---|---|---|
| 5 s | 446 MB | 66.5% | 439 MB | 64.7% |
| 15 s | 459 MB | 36.5% | 444 MB | 33.4% |
| 30 s | 465 MB | 23.1% | 453 MB | 21.6% |
| 40 s | 469 MB | 19.3% | — | — |

- 线程数：启动阶段 30 → 稳定后 19。
- **空闲瞬时 CPU：3.5%**（Release，`top` 两次采样，两次间隔 2 s）。
- Debug 与 Release 的内存量级差别很小（<20 MB），主要开销不在 JIT。

## 已知偏差与说明

1. **没有上游对照**：Windows 版 Starward 在同一台机器上不可运行，无法给出"是否比上游重"的结论。若以后有 Windows 机器，应补一组同尺寸窗口、同页面、同背景的对照。
2. **RSS 包含 GPU/驱动侧映射**：Uno + Skia 的纹理、字体缓存都在进程内，465 MB 对 .NET GUI 应用偏高，但其中多少是托管堆、多少是原生缓存**尚未拆分**（未跑 `dotnet-counters` / `dotnet-gcdump`）。
3. **视频壁纸另计**：开启视频背景时 RSS 约 +70 MB（960x540 三份缓冲 + libvlc 解码器），帧率/掉帧未测。
4. **首次启动**（无缓存、要拉公告与游戏列表）比上述数字更慢，未纳入本表。

## 后续（按收益）

1. 用 `dotnet-counters` 采 `gc-heap-size` / `working-set`，拆出托管堆占比；
2. 用 `perf record` 看空闲时 3.5% CPU 花在哪（Skia 重绘？后台定时器？）；
3. 给视频壁纸测帧率（`--page installprogress` 同层的调试入口已有，可复用来压测）；
4. 若有 Windows 对照机，补一组基线后再谈"达标"。
