using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace LunarwardPoc;

public sealed partial class CompatPage : UserControl, INotifyPropertyChanged
{
    private string _title = "初始标题";
    private bool _isReady = true;
    private int _count = 3;

    public CompatPage()
    {
        this.InitializeComponent();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ScrollTo(double y) => Scroller.ChangeView(null, y, null, true);

    public string Title
    {
        get => _title;
        set { _title = value; Raise(); }
    }

    public bool IsReady
    {
        get => _isReady;
        set { _isReady = value; Raise(); }
    }

    public int Count
    {
        get => _count;
        set { _count = value; Raise(); }
    }

    public List<string> Items { get; } = new() { "条目 A", "条目 B", "条目 C" };

    public string Describe(int n) => $"Count = {n}（x:Bind 函数返回值）";

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public void ShowProbeDialog() => OnShowDialog(this, new RoutedEventArgs());

    private async void OnShowDialog(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "ContentDialog 探针",
            Content = "这是 ContentDialog 在 Uno/Linux 下的渲染测试。",
            CloseButtonText = "关闭",
            XamlRoot = this.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private void OnShowTip(object sender, RoutedEventArgs e) => Tip.IsOpen = true;
}

public sealed partial class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, string language)
        => value is true ? "转换器：true → 是" : "转换器：false → 否";

    public object ConvertBack(object value, System.Type targetType, object parameter, string language)
        => value is string s && s.Contains("是");
}
