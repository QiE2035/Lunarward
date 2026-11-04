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
        AppWindow.TitleBar.ExtendsContentIntoTitleBar = true;
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



    public override void Show()
    {
        base.Show();
        _ = ResizeAfterShownAsync();
    }


    /// <summary>Lunarward：X11 后端在 map 之后才应用默认尺寸，map 完再补一次（L-35）。</summary>
    private async System.Threading.Tasks.Task ResizeAfterShownAsync()
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(300);
            double scale = Content?.XamlRoot?.RasterizationScale ?? 1;
            if (scale <= 0)
            {
                scale = 1;
            }
            AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = (int)Math.Round(1200 * scale), Height = (int)Math.Round(676 * scale) });
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "[bgwin] resize failed");
        }
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
