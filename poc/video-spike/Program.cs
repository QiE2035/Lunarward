// Lunarward 背景视频尖峰：验证「LibVLCSharp + vmem 回调」能否把视频帧拿到托管侧，
// 并直接填进 SkiaSharp 位图（后续接 Uno 的 WriteableBitmap 就能当壁纸）。
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using SkiaSharp;

internal static class Program
{
    private const int Width = 640;
    private const int Height = 360;

    private static readonly byte[] Buffer = new byte[Width * Height * 4];

    // 整个播放期间只固定一次：回调里重新 Alloc/Free 会和 libvlc 的线程撞车。
    private static readonly GCHandle _pin = GCHandle.Alloc(Buffer, GCHandleType.Pinned);
    private static int _frames;

    private static int Main()
    {
        Core.Initialize();

        using var libvlc = new LibVLC("--no-audio", "--quiet", "--vout=vmem");
        using var player = new MediaPlayer(libvlc);

        player.SetVideoFormat("RV32", Width, Height, (uint)(Width * 4));
        player.SetVideoCallbacks(Lock, Unlock, Display);

        using var media = new Media(libvlc, new Uri("file:///tmp/lw-test-video.mp4"));
        player.Play(media);

        DateTime deadline = DateTime.UtcNow.AddSeconds(20);
        while (Volatile.Read(ref _frames) < 12 && DateTime.UtcNow < deadline)
        {
            Thread.Sleep(100);
        }

        Console.WriteLine($"frames={_frames} saved={File.Exists("/tmp/lw-video-frame.png")}");
        // 先停播放器再解固定，否则 libvlc 的回调会写到已释放的缓冲区（段错误）。
        player.Stop();
        Thread.Sleep(300);
        if (_pin.IsAllocated)
        {
            _pin.Free();
        }
        return 0;
    }

    /// <summary>
    /// 关键点：libvlc 传进来的是「plane 指针数组」，回调必须把缓冲区地址写进 planes[0]，
    /// 而不是把它当成返回值——写成返回 IntPtr 会让 libvlc 往 null 写，直接段错误。
    /// </summary>
    private static IntPtr Lock(IntPtr opaque, IntPtr planes)
    {
        Marshal.WriteIntPtr(planes, _pin.AddrOfPinnedObject());
        return _pin.AddrOfPinnedObject();
    }

    private static void Unlock(IntPtr opaque, IntPtr picture, IntPtr planes) { }

    private static void Display(IntPtr opaque, IntPtr picture)
    {
        // 回调是跨原生边界的：任何托管异常都不能漏出去，否则进程直接 abort。
        try
        {
            int frames = Interlocked.Increment(ref _frames);
            if (frames != 12)
            {
                return;
            }
            using var bmp = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            Marshal.Copy(Buffer, 0, bmp.GetPixels(), Buffer.Length);
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 90);
            File.WriteAllBytes("/tmp/lw-video-frame.png", data.ToArray());
            Console.WriteLine($"captured frame #{frames}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("display failed: " + ex.Message);
            Interlocked.Exchange(ref _frames, 12);
        }
    }
}
