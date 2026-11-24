// Lunarward：视频壁纸播放器（M3）。
//
// 上游用 Windows.Media.Playback.MediaPlayer + Win2D：帧回调里 CopyFrameToVideoSurface，
// 再把「主题图」（游戏海报，通常带标题文字）整幅拉伸画到视频帧上，作为 Image.Source。
//
// Linux 侧的等价实现：
//   LibVLCSharp 的 vmem 回调拿 BGRA 帧（poc/video-spike 已验证）
//   → SkiaSharp 把帧缩放 + 主题图拉伸合成到目标位图
//   → 写进 WriteableBitmap 交给 Image。
//
// 依赖：系统 libvlc（Arch 装 vlc）。缺失时 IsAvailable=false，调用方退回静态壁纸。
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using LibVLCSharp.Shared;
using SkiaSharp;

namespace Starward.Features.Background;

internal sealed class VideoBackgroundPlayer : IDisposable
{
    // 960x540 在 1500x845 DIP（1875x1056 物理像素）上被放大，实测看起来明显发虚；
    // 提升到 1080p 让 VLC 直接输出接近显示尺寸的帧。
    private const int VideoWidth = 1920;
    private const int VideoHeight = 1080;

    private static readonly object InitLock = new();
    private static bool _initialized;
    private static bool _available;

    private readonly byte[] _frame = new byte[VideoWidth * VideoHeight * 4];
    private readonly SemaphoreSlim _frameSemaphore = new(1, 1);
    private GCHandle _pin;

    // 交给界面层的最终 BGRA 缓冲。以前是每帧 new → 大量垃圾，后来复用一个托管数组、
    // 现在连它一起固定住，让 Skia 直接把合成结果写进来（每帧少一次 8.3MB 的 Marshal.Copy）。
    private byte[]? _pixels;
    private GCHandle _pixelPin;

    /// <summary>
    /// 是否请求 VAAPI 硬解（默认开）。个别驱动上硬解会花屏/绿屏，用户可以用
    /// <c>LUNARWARD_VIDEO_HWDEC=0</c> 退回软解，不用等我们发版。
    /// </summary>
    private static readonly bool HardwareDecode = Environment.GetEnvironmentVariable("LUNARWARD_VIDEO_HWDEC") is not "0";

    private LibVLC? _libvlc;
    private MediaPlayer? _player;
    private SKBitmap? _overlay;
    private volatile bool _disposed;

    /// <summary>合成完成后抛出的 BGRA 像素；调用方负责回到 UI 线程写入 WriteableBitmap。</summary>
    public event Action<byte[], int, int>? FrameReady;

    public int Width => VideoWidth;

    public int Height => VideoHeight;

    /// <summary>系统是否有可用的 libvlc（没有就当视频壁纸不存在）。</summary>
    public static bool IsAvailable
    {
        get
        {
            lock (InitLock)
            {
                if (!_initialized)
                {
                    _initialized = true;
                    try
                    {
                        LibVLCSharp.Shared.Core.Initialize();
                        _available = true;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "libvlc unavailable, video background disabled");
                        _available = false;
                    }
                }
                return _available;
            }
        }
    }

    /// <summary>设置主题图（海报，带游戏标题文字），会被拉伸到整幅视频帧上，与上游一致。</summary>
    public void SetOverlay(string? imagePath)
    {
        try
        {
            _overlay?.Dispose();
            _overlay = null;
            if (!string.IsNullOrWhiteSpace(imagePath) && File.Exists(imagePath))
            {
                using var data = SKData.Create(imagePath);
                _overlay = SKBitmap.Decode(data);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "load video overlay failed: {path}", imagePath);
        }
    }

    public bool Start(string videoPath)
    {
        if (!IsAvailable || string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            return false;
        }
        try
        {
            Stop();
            _pin = GCHandle.Alloc(_frame, GCHandleType.Pinned);
            // 合成缓冲固定住：Skia 直接往上画，省掉以前「画进 _composed 再 Marshal.Copy 整帧」的第二次拷贝。
            _pixels = new byte[VideoWidth * VideoHeight * 4];
            _pixelPin = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
            _libvlc = new LibVLC("--no-audio", "--quiet", "--vout=vmem", "--input-repeat=65535");
            _player = new MediaPlayer(_libvlc);
            _player.SetVideoFormat("RV32", VideoWidth, VideoHeight, VideoWidth * 4);
            _player.SetVideoCallbacks(Lock, Unlock, Display);
            using var media = new Media(_libvlc, new Uri(Path.GetFullPath(videoPath)));
            if (HardwareDecode)
            {
                // 必须写成「媒体选项」：--avcodec-hw 当 libvlc 实例参数传进去会被忽略——
                // 日志里是 `looking for hw decoder module matching "none"` → `no hw decoder modules matched`，
                // 也就是根本没有硬解，这正是以前「加了 VAAPI 但 CPU 没降」的原因。
                // 走媒体选项后日志变成 `using hw decoder module "vaapi_drm"`；没有 VAAPI 的机器
                // VLC 自己回退软解，不需要我们探测。
                media.AddOption(":avcodec-hw=vaapi");
            }
            _player.Play(media);
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "start video background failed");
            Stop();
            return false;
        }
    }

    /// <summary>窗口隐藏/锁屏时暂停播放（上游 MainWindowStateChangedMessage 语义），帧停住但保留最后一帧。</summary>
    public void Pause()
    {
        try
        {
            _player?.SetPause(true);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "pause video background failed");
        }
    }


    /// <summary>窗口重新激活时继续播放。</summary>
    public void Resume()
    {
        try
        {
            _player?.SetPause(false);
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "resume video background failed");
        }
    }


    public void Stop()
    {
        try
        {
            _player?.Stop();
        }
        catch { }
        _player?.Dispose();
        _player = null;
        _libvlc?.Dispose();
        _libvlc = null;
        if (_pin.IsAllocated)
        {
            _pin.Free();
        }
        if (_pixelPin.IsAllocated)
        {
            _pixelPin.Free();
        }
    }

    private IntPtr Lock(IntPtr opaque, IntPtr planes)
    {
        // libvlc 给的是「plane 指针数组」，必须把地址写进 planes[0]（写成返回值会让它往 null 写）。
        IntPtr ptr = _pin.AddrOfPinnedObject();
        Marshal.WriteIntPtr(planes, ptr);
        return ptr;
    }

    private void Unlock(IntPtr opaque, IntPtr picture, IntPtr planes) { }


    private void Display(IntPtr opaque, IntPtr picture)
    {
        // 跨原生边界：任何托管异常都不能漏出去，否则进程直接 abort。
        try
        {
            if (_disposed || !_frameSemaphore.Wait(0))
            {
                return;
            }
            try
            {
                byte[]? pixels = _pixels;
                if (pixels is null || !_pixelPin.IsAllocated)
                {
                    return;
                }
                if (_overlay is null)
                {
                    // 没有主题图时不需要走 Skia：直接把解码缓冲拷成托管数组，省掉 2MP 的 drawImage。
                    Marshal.Copy(_pin.AddrOfPinnedObject(), pixels, 0, pixels.Length);
                }
                else
                {
                    var info = new SKImageInfo(VideoWidth, VideoHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
                    using var video = SKImage.FromPixels(info, _pin.AddrOfPinnedObject(), VideoWidth * 4);
                    // 直接画进交给界面层的缓冲：以前是先画进 _composed 再整帧拷过来，白拷一次。
                    using var surface = SKSurface.Create(info, _pixelPin.AddrOfPinnedObject(), VideoWidth * 4);
                    if (surface is null)
                    {
                        Marshal.Copy(_pin.AddrOfPinnedObject(), pixels, 0, pixels.Length);
                    }
                    else
                    {
                        SKCanvas canvas = surface.Canvas;
                        canvas.DrawImage(video, new SKRect(0, 0, VideoWidth, VideoHeight));
                        // 上游就是把主题图整幅拉伸到视频帧上（标题文字就在这张图里）。
                        canvas.DrawBitmap(_overlay, new SKRect(0, 0, VideoWidth, VideoHeight));
                        canvas.Flush();
                    }
                }
                FrameReady?.Invoke(pixels, VideoWidth, VideoHeight);
            }
            finally
            {
                _frameSemaphore.Release();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "video frame composite failed");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        Stop();
        _overlay?.Dispose();
        _overlay = null;
        _pixels = null;
        _frameSemaphore.Dispose();
    }
}
