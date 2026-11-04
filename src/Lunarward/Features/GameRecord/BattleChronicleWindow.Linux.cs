// Lunarward：战绩窗口上游用 WebView2 承载 BBS 页面，Linux 侧 WebView2 的 cookie/JS 能力不足（G0-3b / D29）。
// 先提供空窗口占位，登录与 BBS 页面在 M2 用独立 WebKitGTK 窗口实现。
using Starward.Frameworks;
using Microsoft.UI.Xaml.Controls;

namespace Starward.Features.GameRecord;

public sealed partial class BattleChronicleWindow : WindowEx
{
    /// <summary>当前展示的角色（占位，等 BBS 页面移植后填充）。</summary>
    public Starward.Core.GameRecord.GameRecordRole? CurrentRole { get; set; }

    public Microsoft.UI.Xaml.Window? OwnerWindow { get; set; }

    public BattleChronicleWindow()
    {
        Title = "战绩";
        Content = new Grid();
    }

    public void ShowBattleChronicle(Microsoft.UI.Xaml.Window owner, Starward.Core.GameRecord.GameRecordRole? role)
    {
        OwnerWindow = owner;
        CurrentRole = role;
        Activate();
    }

    public string? DocumentTitle { get; set; }
}
