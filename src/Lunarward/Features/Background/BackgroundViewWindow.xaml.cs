using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Starward.Frameworks;
using System;
using System.ComponentModel;
using System.Diagnostics;
using Windows.System;


namespace Starward.Features.Background;

[INotifyPropertyChanged]
public sealed partial class BackgroundViewWindow : WindowEx
{


    public BackgroundViewWindow()
    {
        this.InitializeComponent();
        InitializeWindow();
        WeakReferenceMessenger.Default.Register<AccentColorChangedMessage>(this, OnAccentColorChanged);
    }



    private void InitializeWindow()
    {
        Title = "Starward - Background Viewer";
        AppWindow.Title = "Starward - Background Viewer";
        AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
        SystemBackdrop = new DesktopAcrylicBackdrop();
        Content.KeyDown += Content_KeyDown;
        Closed += BackgroundViewWindow_Closed;
        AdaptTitleBarButtonColorToActuallTheme();
        SetIcon();
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMaximizable = false;
            presenter.IsResizable = false;
        }
    }



    public override void CenterInScreen(int? width = null, int? height = null)
    {
        // Lunarward：Uno X11 下 MoveAndResize 是空操作，位置交给窗口管理器，只按 DIP 设尺寸（L-35）。
        base.CenterInScreen(width ?? 1200, height ?? 676);
    }



    protected override (int Width, int Height)? DefaultWindowSizeInDips => (1200, 676);



    public override void Show()
    {
        // 尺寸与「超出屏幕就缩回并居中」由 WindowEx.Show() 统一处理（D49），这里不再自己 Resize，
        // 否则两次 Resize 会互相打架（真机表现为窗口突然变大、右/下边被推到屏幕外）。
        base.Show();
    }



    private void OnAccentColorChanged(object _, AccentColorChangedMessage __)
    {
        FrameworkElement ele = (FrameworkElement)Content;
        ele.RequestedTheme = ele.ActualTheme switch
        {
            ElementTheme.Light => ElementTheme.Dark,
            ElementTheme.Dark => ElementTheme.Light,
            _ => ElementTheme.Default,
        };
        ele.RequestedTheme = ElementTheme.Default;
    }



    private void Content_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key is VirtualKey.Escape)
        {
            Close();
        }
    }


    private void BackgroundViewWindow_Closed(object sender, WindowEventArgs args)
    {
        try
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
            Content.KeyDown -= Content_KeyDown;
            Closed -= BackgroundViewWindow_Closed;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }



}
