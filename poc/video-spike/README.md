# video-spike · 背景视频可行性尖峰

验证「LibVLCSharp 的 vmem 回调 → SkiaSharp 位图」这条路能不能把视频帧拿到托管侧，
以便后续在 `AppBackground` 里当动态壁纸（D31 / M3）。

## 运行

```bash
ffmpeg -v error -f lavfi -i "testsrc=size=640x360:rate=30:duration=3" -pix_fmt yuv420p -y /tmp/lw-test-video.mp4
DISPLAY=:0 dotnet run -c Release
```

成功标志：`captured frame #12` 与 `saved=True`，输出 `/tmp/lw-video-frame.png`（640x360 彩条）。

## 结论（2026-11-03）

- ✅ 可用：本机 `vlc 3.0.24`（`/usr/lib/libvlc.so.5`）+ `LibVLCSharp 3.9.4` + `SkiaSharp 3.119.0`（含 `SkiaSharp.NativeAssets.Linux.NoDependencies`）。
- 关键点：`SetVideoFormat("RV32", w, h, w*4)` + `SetVideoCallbacks(lock, unlock, display)`，
  **lock 回调必须把缓冲区地址写进 `planes[0]`**（`Marshal.WriteIntPtr(planes, ptr)`），
  写成返回值会让 libvlc 往 null 写 → 段错误。
- 回调跨原生边界，任何托管异常都会 abort 进程：`display` 里整段 try/catch。
- 缓冲区固定一次即可；播放结束必须先 `player.Stop()` 再解固定。
- 依赖成本：运行时需要系统有 libvlc（Arch 装 `vlc`），属于**可选依赖**——没有就保持静态壁纸。
