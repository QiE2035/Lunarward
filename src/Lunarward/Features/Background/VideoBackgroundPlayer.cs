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
    private const int VideoWidth = 960;
    private const int VideoHeight = 540;

    private static readonly object InitLock = new();
    private static bool _initialized;
    private static bool _available;

    private readonly byte[] _frame = new byte[VideoWidth * VideoHeight * 4];
    private readonly SemaphoreSlim _frameSemaphore = new(1, 1);
    private GCHandle _pin;

    // 每帧复用：30fps 下若每帧 new SKBitmap + byte[] 会产生 ~60MB/s 垃圾（用户明确抱怨过内存）。
    private SKBitmap? _composed;
    private byte[]? _pixels;

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
            _libvlc = new LibVLC("--no-audio", "--quiet", "--vout=vmem", "--input-repeat=65535");
            _player = new MediaPlayer(_libvlc);
            _player.SetVideoFormat("RV32", VideoWidth, VideoHeight, VideoWidth * 4);
            _player.SetVideoCallbacks(Lock, Unlock, Display);
            using var media = new Media(_libvlc, new Uri(Path.GetFullPath(videoPath)));
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
                _composed ??= new SKBitmap(VideoWidth, VideoHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
                _pixels ??= new byte[VideoWidth * VideoHeight * 4];
                var info = new SKImageInfo(VideoWidth, VideoHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
                using var video = SKImage.FromPixels(info, _pin.AddrOfPinnedObject(), VideoWidth * 4);
                using (var canvas = new SKCanvas(_composed))
                {
                    canvas.DrawImage(video, new SKRect(0, 0, VideoWidth, VideoHeight));
                    if (_overlay is not null)
                    {
                        // 上游就是把主题图整幅拉伸到视频帧上（标题文字就在这张图里）。
                        canvas.DrawBitmap(_overlay, new SKRect(0, 0, VideoWidth, VideoHeight));
                    }
                }
                Marshal.Copy(_composed.GetPixels(), _pixels, 0, _pixels.Length);
                FrameReady?.Invoke(_pixels, VideoWidth, VideoHeight);
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
        _composed?.Dispose();
        _composed = null;
        _pixels = null;
        _frameSemaphore.Dispose();
    }
}
