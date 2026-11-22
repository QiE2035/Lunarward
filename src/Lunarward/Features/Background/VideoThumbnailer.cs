// Lunarward：给背景图库里的视频条目抽首帧当缩略图（L-98）。
//
// 背景：`CachedImage` 的 IsThumbnail/PngThumbnail 在 Linux 实现里只是占位属性，
// 图库直接把 .webm 交给 SkiaSharp 解码必然失败，于是视频条目是黑块。这里复用
// 壁纸播放器同一个解码路径抽一帧，落成 png 缓存，图库读缓存图。
//
// 2026-11-22（B21 回归）：早期版本用默认 vout + TakeSnapshot，libvlc 会为每个视频
// 打开一个真实窗口，翻图库时满屏闪 VLC 窗口。现在与 VideoBackgroundPlayer 一致，
// 走 `--vout=vmem` + 像素回调，全程无窗口；同一路径的抽帧还会去重（图库和
// CachedImage 会同时请求），失败结果短期缓存，避免坏文件反复起解码。
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using SkiaSharp;

namespace Starward.Features.Background;

internal static class VideoThumbnailer
{

    private const int ThumbWidth = 480;
    private const int ThumbHeight = 270;

    private const int RowBytes = ThumbWidth * 4;

    /// <summary>同时抽帧的并发数：vmem 解码是纯 CPU，2 个足够又不至于卡住图库滚动。</summary>
    private static readonly SemaphoreSlim _gate = new(2, 2);

    /// <summary>同一文件的抽帧任务只跑一次（页面批量生成与 CachedImage 会重复请求）。</summary>
    private static readonly ConcurrentDictionary<string, Task<string?>> _inFlight = new(StringComparer.Ordinal);

    /// <summary>失败结果短期缓存，避免每次打开图库都为同一个坏文件重新解码。</summary>
    private static readonly ConcurrentDictionary<string, DateTime> _failed = new(StringComparer.Ordinal);

    private static readonly TimeSpan FailedRetention = TimeSpan.FromMinutes(10);


    public static string ThumbnailFolder => Path.Combine(AppConfig.CacheDirectory, "bg-thumbs");


    public static string ThumbnailPath(string videoPath)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(videoPath)));
        return Path.Combine(ThumbnailFolder, Convert.ToHexString(hash)[..16].ToLowerInvariant() + ".png");
    }


    /// <summary>确保该视频有缩略图，返回 png 路径（失败返回 null）。</summary>
    public static Task<string?> EnsureAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
        {
            return Task.FromResult<string?>(null);
        }
        string key = Path.GetFullPath(videoPath);
        string target = ThumbnailPath(key);
        if (File.Exists(target) && new FileInfo(target).Length > 0)
        {
            return Task.FromResult<string?>(target);
        }
        if (_failed.TryGetValue(key, out DateTime failedAt))
        {
            if (DateTime.UtcNow - failedAt < FailedRetention)
            {
                return Task.FromResult<string?>(null);
            }
            _failed.TryRemove(key, out _);
        }
        Task<string?> task = _inFlight.GetOrAdd(key, k => CaptureTrackedAsync(k, target, cancellationToken));
        return task;
    }


    private static async Task<string?> CaptureTrackedAsync(string key, string target, CancellationToken cancellationToken)
    {
        try
        {
            string? result = await CaptureAsync(key, target, cancellationToken).ConfigureAwait(false);
            if (result is null)
            {
                _failed[key] = DateTime.UtcNow;
            }
            return result;
        }
        finally
        {
            _inFlight.TryRemove(key, out _);
        }
    }


    private static async Task<string?> CaptureAsync(string key, string target, CancellationToken cancellationToken)
    {
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (File.Exists(target) && new FileInfo(target).Length > 0)
                {
                    return target;
                }
                return await Task.Run(() => Capture(key, target), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "video thumbnail failed: {file}", key);
            return null;
        }
    }


    /// <summary>vmem 抽帧：不开任何窗口，等第一帧到达后把 BGRA 缓冲编码成 png。</summary>
    private static string? Capture(string videoPath, string target)
    {
        if (!VideoBackgroundPlayer.IsAvailable)
        {
            return null;
        }
        string temporary = target + ".tmp.png";
        byte[] buffer = new byte[RowBytes * ThumbHeight];
        GCHandle pin = default;
        using ManualResetEventSlim frameReady = new(false);
        try
        {
            Directory.CreateDirectory(ThumbnailFolder);
            pin = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            IntPtr framePtr = pin.AddrOfPinnedObject();
            using var libvlc = new LibVLC("--no-audio", "--quiet", "--no-video-title-show", "--no-osd", "--vout=vmem");
            using var player = new MediaPlayer(libvlc);
            player.SetVideoFormat("RV32", ThumbWidth, ThumbHeight, RowBytes);
            player.SetVideoCallbacks(
                (_, planes) =>
                {
                    Marshal.WriteIntPtr(planes, framePtr);
                    return framePtr;
                },
                (_, _, _) => { },
                (_, _) =>
                {
                    try
                    {
                        frameReady.Set();
                    }
                    catch
                    {
                    }
                });
            using var media = new Media(libvlc, new Uri(videoPath));
            media.AddOption(":start-time=1");
            player.Play(media);
            if (!frameReady.Wait(TimeSpan.FromSeconds(6)))
            {
                Serilog.Log.Debug("video thumbnail: no frame within timeout: {file}", Path.GetFileName(videoPath));
                return null;
            }
            player.Stop();
            var info = new SKImageInfo(ThumbWidth, ThumbHeight, SKColorType.Bgra8888, SKAlphaType.Opaque);
            using var image = SKImage.FromPixelCopy(info, buffer, RowBytes);
            using var data = image.Encode(SKEncodedImageFormat.Png, 90);
            using (FileStream stream = File.Create(temporary))
            {
                data.SaveTo(stream);
            }
            if (File.Exists(temporary) && new FileInfo(temporary).Length > 0)
            {
                File.Move(temporary, target, overwrite: true);
                Serilog.Log.Debug("video thumbnail: {video} -> {png}", Path.GetFileName(videoPath), Path.GetFileName(target));
                return target;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "video thumbnail capture failed: {file}", videoPath);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
            catch
            {
            }
            if (pin.IsAllocated)
            {
                pin.Free();
            }
        }
        return null;
    }


    public static void ClearCache()
    {
        try
        {
            _failed.Clear();
            if (Directory.Exists(ThumbnailFolder))
            {
                Directory.Delete(ThumbnailFolder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "clear video thumbnails failed");
        }
    }

}
