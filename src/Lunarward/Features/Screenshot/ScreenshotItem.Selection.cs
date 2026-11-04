// Lunarward：给上游 ScreenshotItem 追加「是否选中」（上游 ScreenshotItem 是 partial，见 ScreenshotItem.cs）。
// 多选不依赖 GridView 的 SelectionMode（该页已换成 ItemsControl，见 L-48），改为条目自带布尔状态。
using CommunityToolkit.Mvvm.ComponentModel;

namespace Starward.Features.Screenshot;

public partial class ScreenshotItem
{
    private bool _isSelected;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
