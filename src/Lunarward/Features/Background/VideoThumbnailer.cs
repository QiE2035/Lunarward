// Lunarward：给背景图库里的视频条目抽首帧当缩略图（L-98）。
//
// 背景：`CachedImage` 的 IsThumbnail/PngThumbnail 在 Linux 实现里只是占位属性，
// 图库直接把 .webm 交给 SkiaSharp 解码必然失败，于是视频条目是黑块。这里复用
// 壁纸播放器同一个解码器（LibVLC）抽一帧，落成 png 缓存，图库读缓存图。
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibVLCSharp.Shared;

namespace Starward.Features.Background;

internal static class VideoThumbnailer
{

    private static readonly SemaphoreSlim _gate = new(2, 2);


    public static string ThumbnailFolder => Path.Combine(AppConfig.CacheDirectory, "bg-thumbs");


    public static string ThumbnailPath(string videoPath)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(videoPath)));
        return Path.Combine(ThumbnailFolder, Convert.ToHexString(hash)[..16].ToLowerInvariant() + ".png");
    }


    /// <summary>确保该视频有缩略图，返回 png 路径（失败返回 null）。</summary>
    public static async Task<string?> EnsureAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        try
        {
            string target = ThumbnailPath(videoPath);
            if (File.Exists(target) && new FileInfo(target).Length > 0)
            {
                return target;
            }
            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (File.Exists(target) && new FileInfo(target).Length > 0)
                {
                    return target;
                }
                return await Task.Run(() => Capture(videoPath, target), cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "video thumbnail failed: {file}", videoPath);
            return null;
        }
    }


    private static string? Capture(string videoPath, string target)
    {
        if (!VideoBackgroundPlayer.IsAvailable)
        {
            return null;
        }
        string temporary = target + ".tmp.png";
        try
        {
            Directory.CreateDirectory(ThumbnailFolder);
            using var libvlc = new LibVLC("--no-audio", "--quiet", "--no-video-title-show");
            using var player = new MediaPlayer(libvlc);
            using var media = new Media(libvlc, new Uri(Path.GetFullPath(videoPath)));
            media.AddOption(":start-time=1");
            player.Play(media);
            DateTime deadline = DateTime.UtcNow.AddSeconds(8);
            while (DateTime.UtcNow < deadline && player.Time < 600)
            {
                Thread.Sleep(120);
            }
            bool ok = player.TakeSnapshot(0, temporary, 480, 270);
            player.Stop();
            if (ok && File.Exists(temporary) && new FileInfo(temporary).Length > 0)
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
        }
        return null;
    }


    public static void ClearCache()
    {
        try
        {
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
