// Lunarward：上游托盘用 H.NotifyIcon.WinUI（基于 Windows 消息循环 + Shell_NotifyIcon）。
// Linux 的托盘是 org.kde.StatusNotifierItem（D-Bus），G0-6 已验证 Tmds.DBus 可行。
// M2 会用一个真正实现 StatusNotifierItem 的控件替换本类；这里先提供同名占位控件，
// 让 SystemTrayWindow.xaml 保持原样编译，运行时托盘图标不显示（不假装已实现）。
using Microsoft.UI.Xaml.Controls;
using System.Windows.Input;

namespace H.NotifyIcon;

public class TaskbarIcon : Control
{
    public object? IconSource { get; set; }

    public string? ToolTipText { get; set; }

    public bool ContextMenuMode { get; set; }

    public bool NoLeftClickDelay { get; set; }

    public ICommand? LeftClickCommand { get; set; }

    public ICommand? RightClickCommand { get; set; }

    public ICommand? DoubleClickCommand { get; set; }

    public object? Icon { get; set; }

    public void ForceCreate() { }

    public void Dispose() { }
}
