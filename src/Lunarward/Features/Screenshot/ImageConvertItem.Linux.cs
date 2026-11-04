// Lunarward：截图转换条目依赖 Windows.Graphics.Imaging（被排除），先保留最小占位。
using CommunityToolkit.Mvvm.ComponentModel;

namespace Starward.Features.Screenshot;

public partial class ImageConvertItem : ObservableObject
{
    public string FileName { get; set; } = "";

    public string FilePath { get; set; } = "";

    public bool IsSelected { get; set; }
}
