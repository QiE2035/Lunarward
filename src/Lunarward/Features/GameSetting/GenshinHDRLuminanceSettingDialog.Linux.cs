// Lunarward：原神 HDR 亮度对话框在 Windows 上用 Win2D 实时合成 HDR 预览（SDR/HDR 对比画面）。
// Linux 侧暂无等价的 HDR 预览渲染（Uno/Skia 尚无 HDR surface），这里保留对话框结构与
// 全部绑定成员，预览区域留空并提示「需要 HDR 预览时再实现」。D21/D31 记为已知缺口。
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Core;
using System.Threading.Tasks;

namespace Starward.Features.GameSetting;

public sealed partial class GenshinHDRLuminanceSettingDialog : ContentDialog
{
    public GameBiz CurrentGameBiz { get; set; }

    public string? DisplayInfomation { get; set; } = "Linux：HDR 预览暂不可用";

    public bool HDRNotSupported { get; set; }

    public bool HDRNotEnabled => !HDREnabled;

    public bool HDREnabled { get; set; }

    public bool MoraVisibility => false;

    public int MaxLuminance { get; set; } = 1000;

    public int UILuminance { get; set; } = 200;

    public int SceneLuminance { get; set; } = 400;

    [RelayCommand]
    private async Task SaveAsync()
    {
        // 与上游一致：写游戏注册表里的 HDR 亮度（Linux 下由 WineRegistry 落到 wine prefix）
        await GameSettingService.SetGenshinHDRLuminanceAsync(CurrentGameBiz, MaxLuminance, SceneLuminance, UILuminance);
        Hide();
    }

    [RelayCommand]
    private void Reset()
    {
        MaxLuminance = 1000;
        UILuminance = 200;
        SceneLuminance = 400;
    }

    [RelayCommand]
    private void AutoAdjust() => Reset();

    [RelayCommand]
    private void Close() => Hide();
}
