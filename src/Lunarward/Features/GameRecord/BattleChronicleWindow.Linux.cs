// Lunarward：战绩窗口上游用 WebView2 承载 BBS 页面，Linux 侧 WebView2 的 cookie/JS 能力不足（G0-3b / D29）。
// 先提供一个讲清楚「为什么没有、什么时候有」的占位窗口，登录与 BBS 页面在 M2 用独立 WebKitGTK 窗口实现。
// L-183：以前这里是 `Content = new Grid();`，用户点「战绩」只看到一片空白，以为是程序坏了。
using Starward.Features;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Starward.Features.GameRecord;

public sealed partial class BattleChronicleWindow : WindowBase
{
    /// <summary>当前展示的角色（占位，等 BBS 页面移植后填充）。</summary>
    public Starward.Core.GameRecord.GameRecordRole? CurrentRole { get; set; }

    public Microsoft.UI.Xaml.Window? OwnerWindow { get; set; }

    private readonly TextBlock _roleText;

    public BattleChronicleWindow()
    {
        Title = "战绩";
        _roleText = new TextBlock
        {
            Opacity = 0.7,
            Margin = new Thickness(0, 8, 0, 0),
        };
        var panel = new StackPanel
        {
            Spacing = 8,
            MaxWidth = 420,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        panel.Children.Add(new FontIcon
        {
            Glyph = "\uE9D9",
            FontSize = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Linux 版暂时不能打开战绩页面",
            FontSize = 18,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        panel.Children.Add(new TextBlock
        {
            Text = "上游用 WebView2 承载米游社页面，Linux 版要等独立的 WebKitGTK 窗口（里程碑 M2）。" +
                   "角色数据本身已经能刷新，只是战绩详情页还打不开。",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Opacity = 0.8,
        });
        panel.Children.Add(_roleText);
        Content = new Grid
        {
            Padding = new Thickness(24),
            Children = { panel },
        };
    }

    public void ShowBattleChronicle(Microsoft.UI.Xaml.Window owner, Starward.Core.GameRecord.GameRecordRole? role)
    {
        OwnerWindow = owner;
        CurrentRole = role;
        _roleText.Text = role is null
            ? ""
            : $"当前角色：{role.Nickname}（UID {role.Uid}）";
        Activate();
    }

    public string? DocumentTitle { get; set; }
}
