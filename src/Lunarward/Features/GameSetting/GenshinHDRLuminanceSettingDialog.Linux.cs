// Lunarward：原神 HDR 亮度对话框。
// Windows 版用 Win2D 做实时 HDR 预览（SDR/HDR 对比画面），Linux 侧没有等价的 HDR surface，
// 因此这里用代码构建同一个对话框的「可操作部分」：三个亮度滑块 + 保存/恢复默认，
// 打开时读游戏注册表里的现值，保存时写回游戏真正使用的前缀（D95/B11、B70）。
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Starward.Core;
using Starward.Language;
using System;
using System.Threading.Tasks;

namespace Starward.Features.GameSetting;

public sealed partial class GenshinHDRLuminanceSettingDialog : ContentDialog
{

    private const int DefaultMaxLuminance = 1000;
    private const int DefaultSceneLuminance = 300;
    private const int DefaultUILuminance = 350;

    private readonly Slider _sliderMax = CreateSlider(300, 2000, DefaultMaxLuminance);
    private readonly Slider _sliderScene = CreateSlider(100, 500, DefaultSceneLuminance);
    private readonly Slider _sliderUI = CreateSlider(150, 550, DefaultUILuminance);
    private readonly TextBlock _valueMax = CreateValueText(DefaultMaxLuminance);
    private readonly TextBlock _valueScene = CreateValueText(DefaultSceneLuminance);
    private readonly TextBlock _valueUI = CreateValueText(DefaultUILuminance);


    public GameBiz CurrentGameBiz { get; set; }


    public GenshinHDRLuminanceSettingDialog()
    {
        Title = Lang.GenshinHDRLuminanceSettingDialog_MonitorInformation;
        PrimaryButtonText = Lang.Common_Save;
        CloseButtonText = Lang.Common_Cancel;
        DefaultButton = ContentDialogButton.Primary;
        Content = BuildContent();
        PrimaryButtonClick += OnPrimaryButtonClick;
        Opened += OnOpened;
        _sliderMax.ValueChanged += (_, e) => _valueMax.Text = ((int) e.NewValue).ToString();
        _sliderScene.ValueChanged += (_, e) => _valueScene.Text = ((int) e.NewValue).ToString();
        _sliderUI.ValueChanged += (_, e) => _valueUI.Text = ((int) e.NewValue).ToString();
    }


    private static Slider CreateSlider(double min, double max, double value) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = value,
        StepFrequency = 10,
        Width = 320,
    };


    private static TextBlock CreateValueText(int value) => new()
    {
        Text = value.ToString(),
        MinWidth = 56,
        VerticalAlignment = VerticalAlignment.Center,
    };


    private static Grid CreateRow(string label, Slider slider, TextBlock value)
    {
        var grid = new Grid { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(text, 0);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(value, 2);
        grid.Children.Add(text);
        grid.Children.Add(slider);
        grid.Children.Add(value);
        return grid;
    }


    private UIElement BuildContent()
    {
        var panel = new StackPanel { Spacing = 16, MinWidth = 560 };
        panel.Children.Add(new TextBlock
        {
            Text = "Linux：HDR 预览暂不可用，只能调整写入游戏的亮度参数。",
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(CreateRow(Lang.GenshinHDRLuminanceSettingDialog_MaxLuminance, _sliderMax, _valueMax));
        panel.Children.Add(CreateRow(Lang.GenshinHDRLuminanceSettingDialog_SceneryBrightness, _sliderScene, _valueScene));
        panel.Children.Add(CreateRow(Lang.GenshinHDRLuminanceSettingDialog_UIBrightness, _sliderUI, _valueUI));
        panel.Children.Add(new Button
        {
            Content = Lang.Common_RestoreDefaults,
            Command = ResetCommand,
            HorizontalAlignment = HorizontalAlignment.Left,
        });
        return panel;
    }


    private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
    {
        try
        {
            var (max, scene, ui) = await GameSettingService.GetGenshinHDRLuminanceAsync(CurrentGameBiz);
            _sliderMax.Value = max;
            _sliderScene.Value = scene;
            _sliderUI.Value = ui;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Read Genshin HDR luminance failed");
        }
    }


    private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            await GameSettingService.SetGenshinHDRLuminanceAsync(CurrentGameBiz, (int) _sliderMax.Value, (int) _sliderScene.Value, (int) _sliderUI.Value);
        }
        catch (Exception ex)
        {
            args.Cancel = true;
            Serilog.Log.Warning(ex, "Write Genshin HDR luminance failed");
            Helpers.InAppToast.MainWindow?.Error(ex.Message, null, 6000);
        }
        finally
        {
            deferral.Complete();
        }
    }


    [RelayCommand]
    private void Reset()
    {
        _sliderMax.Value = DefaultMaxLuminance;
        _sliderScene.Value = DefaultSceneLuminance;
        _sliderUI.Value = DefaultUILuminance;
    }

}
