// Lunarward：上游 ImageLoader 依赖 Win2D/Starward.Codec（不可移植），这里先实现截图流程用得到的
// 像素尺寸查询，解码交给 SkiaSharp（Uno 在 Linux 上本来就依赖它）。
using SkiaSharp;
using System.IO;
using System.Threading.Tasks;

namespace Starward.Features.Codec;

public static partial class ImageLoader
{
    public static Task<(uint Width, uint Height)> GetImagePixelSizeAsync(string filePath)
    {
        return Task.Run(() =>
        {
            using var stream = File.OpenRead(filePath);
            using var codec = SKCodec.Create(stream);
            if (codec is null)
            {
                return (0u, 0u);
            }
            var info = codec.Info;
            return ((uint)info.Width, (uint)info.Height);
        });
    }
}
