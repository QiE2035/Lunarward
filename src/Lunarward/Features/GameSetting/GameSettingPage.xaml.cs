using CommunityToolkit.Mvvm.Input;
using Starward.Language;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Graphics.Display;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Starward.Core;
using Starward.Features.GameLauncher;
using Starward.Features.GameSelector;
using Starward.Frameworks;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;


namespace Starward.Features.GameSetting;

public sealed partial class GameSettingPage : PageBase
{

    private readonly ILogger<GameSettingPage> _logger = AppConfig.GetLogger<GameSettingPage>();

    private readonly GameLauncherService _gameLauncherService = AppConfig.GetService<GameLauncherService>();



    public GameSettingPage()
    {
        this.InitializeComponent();
    }




    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        Image_Emoji.Source = CurrentGameBiz.ToGame().Value switch
        {
            GameBiz.bh3 => new BitmapImage(AppConfig.EmojiAI),
            GameBiz.hk4e => new BitmapImage(AppConfig.EmojiPaimon),
            GameBiz.hkrpg => new BitmapImage(AppConfig.EmojiPom),
            GameBiz.nap => new BitmapImage(AppConfig.EmojiBangboo),
            _ => null,
        };
        RefreshProfileSelector();
        RefreshCompanions();
        if (CurrentGameId.GameBiz == GameBiz.bh3_global)
        {
            CurrentGameBiz = CurrentGameId.Id switch
            {
                "g0mMIvshDb" => GameBiz.bh3_jp,
                "uxB4MC7nzC" => GameBiz.bh3_kr,
                "bxPTXSET5t" => GameBiz.bh3_os,
                "wkE5P5WsIf" => GameBiz.bh3_asia,
                _ => GameBiz.bh3_global,
            };
        }
    }


    protected override async void OnLoaded()
    {
        try
        {
            InitializeResolutionItem();
        }
        catch (Exception ex)
        {
            // Lunarward：Linux 上 DisplayArea/ContentIsland 不可用，分辨率列表先留空（见 13-lessons.md）
        }
        await InitializeGameSettingAsync();
        // Lunarward：截图验证用（--scroll <px> / LUNARWARD_SCROLL），见 Features/App/DevSwitches.cs
        if (Starward.Features.Dev.DevSwitches.Scroll is int offset && offset > 0)
        {
            ScrollViewer_Main.ChangeView(null, offset, null, true);
        }
    }


    protected override void OnUnloaded()
    {
        if (_displayInformation is not null)
        {
            _displayInformation.AdvancedColorInfoChanged -= _displayInformation_AdvancedColorInfoChanged;
            _displayInformation.Dispose();
            _displayInformation = null!;
        }
    }


    public bool IsBaseSettingEnable { get; set => SetProperty(ref field, value); }

    public bool IsLanguageSettingEnable { get; set => SetProperty(ref field, value); }

    public bool IsGraphicsSettingEnable { get; set => SetProperty(ref field, value); }

    public bool IsApplyButtonEnable { get; set => SetProperty(ref field, value); }

    public string ErrorMessage { get; set => SetProperty(ref field, value); } = Lang.GameSettingPage_SettingNotEffect; // 游戏运行时应用的设置无法生效





    public string? StartArgument
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.SetStartArgument(CurrentGameBiz, value);
            }
        }
    }


    public bool EnableFullScreen
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public bool UsePopupWindow
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }



    public bool EnableCustomResolution { get; set => SetProperty(ref field, value); }


    public int ResolutionWidth
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public int ResolutionHeight
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public int LanguageIndex
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public int StarRailFpsIndex
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public bool EnableGenshinHDR
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                IsApplyButtonEnable = true;
            }
        }
    }


    public bool HDRNotSupported { get; set => SetProperty(ref field, value); }

    public bool HDRNotEnabled { get; set => SetProperty(ref field, value); }


    private async Task InitializeGameSettingAsync()
    {
        try
        {
            var localVersion = await _gameLauncherService.GetLocalGameVersionAsync(CurrentGameId);
            if (localVersion is null)
            {
                StackPanel_Emoji.Visibility = Visibility.Visible;
                return;
            }
            IsBaseSettingEnable = true;
            if (CurrentGameBiz.ToGame().Value is GameBiz.hk4e or GameBiz.hkrpg)
            {
                IsLanguageSettingEnable = true;
            }
            if (CurrentGameBiz.Game is GameBiz.hkrpg)
            {
                IsGraphicsSettingEnable = true;
                StackPanel_StarRailFPS.Visibility = Visibility.Visible;
                StarRailFpsIndex = GameSettingService.GetStarRailFPSIndex(CurrentGameBiz);
            }
            if (CurrentGameBiz.Game is GameBiz.hk4e)
            {
                IsGraphicsSettingEnable = true;
                StackPanel_GenshinHDR.Visibility = Visibility.Visible;
                EnableGenshinHDR = AppConfig.EnableGenshinHDR;
                try
                {
                    // Lunarward：Microsoft.Graphics.Display / AdvancedColorInfo 在 Uno Linux 未实现，
                    // 拿不到 HDR 状态时按「不支持」处理（见 12-shims-and-gaps.md、13-lessons.md）
                    var appWindow = this.XamlRoot.GetAppWindow();
                    if (appWindow is not null)
                    {
                        _displayInformation = DisplayInformation.CreateForWindowId(appWindow.Id);
                        _displayInformation.AdvancedColorInfoChanged += _displayInformation_AdvancedColorInfoChanged;
                        UpdateHdrState(_displayInformation);
                    }
                    else
                    {
                        HDRNotSupported = true;
                    }
                }
                catch (Exception ex)
                {
                    HDRNotSupported = true;
                }
            }
            StartArgument = AppConfig.GetStartArgument(CurrentGameBiz);
            UsePopupWindow = AppConfig.GetUsePopupWindow(CurrentGameBiz);
            var resolutionSetting = GameSettingService.GetGameResolutionSetting(CurrentGameBiz);
            if (resolutionSetting != null)
            {
                EnableFullScreen = resolutionSetting.IsFullScreen;
                if (resolutionSetting.Width * resolutionSetting.Height > 0)
                {
                    ResolutionWidth = resolutionSetting.Width;
                    ResolutionHeight = resolutionSetting.Height;
                    EnableCustomResolution = !UpdateResolutionComboBoxSelection(ResolutionWidth, ResolutionHeight);
                }
                else
                {
                    ComboBox_Resolution.SelectedIndex = 0;
                }
            }
            else
            {
                ComboBox_Resolution.SelectedIndex = 0;
            }
            if (IsLanguageSettingEnable)
            {
                var langSetting = GameSettingService.GetGameVoiceLanguageSetting(CurrentGameBiz);
                if (langSetting != null)
                {
                    LanguageIndex = langSetting.Value;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Initialize Game Setting");
        }
        finally
        {
            IsApplyButtonEnable = false;
        }
    }



    private void InitializeResolutionItem()
    {
        var display = DisplayArea.GetFromWindowId(this.XamlRoot.ContentIslandEnvironment.AppWindowId, DisplayAreaFallback.Nearest);
        var width = display.OuterBounds.Width;
        var height = display.OuterBounds.Height;
        var list = Resolutions.Where(x => x.Width <= width && x.Height <= height).ToList();
        if (list.Count == 0)
        {
            list.Add((width, height));
        }
        else
        {
            if (list[0].Width != width || list[0].Height != height)
            {
                list.Add((width, height));
            }
        }
        foreach (var item in list)
        {
            ComboBox_Resolution.Items.Add(new ComboBoxItem
            {
                Content = $"{item.Width} × {item.Height}",
            });
        }
    }




    private static List<(int Width, int Height)> Resolutions = new List<(int Width, int Height)>()
    {
        (3840 , 2160),
        (2560 , 1600),
        (2560 , 1440),
        (2048 , 1536),
        (1920 , 1440),
        (1920 , 1200),
        (1920 , 1080),
        (1680 , 1050),
        (1600 , 1200),
        (1440 , 900 ),
        (1280 , 960 ),
        (1280 , 800 ),
        (1280 , 720 ),
        (1152 , 864 ),
        (1024 , 768 ),
        (800  , 600 ),
        (640  , 480 ),
    };



    private void ComboBox_Resolution_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is ComboBoxItem item)
        {
            if (item.Content is string str)
            {
                var split = str.Split('×', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (split.Length == 2)
                {
                    if (int.TryParse(split[0], out int width) && int.TryParse(split[1], out int height))
                    {
                        ResolutionWidth = width;
                        ResolutionHeight = height;
                        IsApplyButtonEnable = true;
                    }
                }
            }
        }
    }



    private bool UpdateResolutionComboBoxSelection(int width, int height)
    {
        foreach (ComboBoxItem item in ComboBox_Resolution.Items)
        {
            if (item.Content is string str)
            {
                var split = str.Split('×', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (split.Length == 2)
                {
                    if (int.TryParse(split[0], out int _width) && int.TryParse(split[1], out int _height))
                    {
                        if (width == _width && height == _height)
                        {
                            ComboBox_Resolution.SelectedItem = item;
                            return true;
                        }
                    }
                }
            }
        }
        return false;
    }


    [RelayCommand]
    private async Task OpenGenshinHDRLumianceSettingWindow()
    {
        try
        {
            WeakReferenceMessenger.Default.Send(new MainWindowDragRectAdaptToGameIconMessage(true));
            await new GenshinHDRLuminanceSettingDialog { XamlRoot = this.XamlRoot, CurrentGameBiz = this.CurrentGameBiz }.ShowAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
        finally
        {
            WeakReferenceMessenger.Default.Send(new MainWindowDragRectAdaptToGameIconMessage());
        }
    }



    [RelayCommand]
    private void ApplySetting()
    {
        try
        {
            if (IsBaseSettingEnable)
            {
                if (ResolutionWidth <= 0 || ResolutionHeight <= 0)
                {
                    // 分辨率必须大于0
                    ErrorMessage = Lang.GameSettingPage_ResolutionMustBeGreaterThan0;
                    return;
                }
                var model = new GraphicsSettings_PCResolution_h431323223
                {
                    IsFullScreen = EnableFullScreen,
                    Width = ResolutionWidth,
                    Height = ResolutionHeight,
                };
                GameSettingService.SetGameResolutionSetting(CurrentGameBiz, model);
                AppConfig.SetUsePopupWindow(CurrentGameBiz, UsePopupWindow);
            }
            if (IsLanguageSettingEnable)
            {
                GameSettingService.SetGameVoiceLanguageSetting(CurrentGameBiz, LanguageIndex);
            }
            if (IsGraphicsSettingEnable)
            {
                if (CurrentGameBiz.Game is GameBiz.hkrpg)
                {
                    GameSettingService.SetStarRailFPSIndex(CurrentGameBiz, StarRailFpsIndex);
                }
                if (CurrentGameBiz.Game is GameBiz.hk4e)
                {
                    AppConfig.EnableGenshinHDR = EnableGenshinHDR;
                    GameSettingService.SetGenshinEnableHDR(CurrentGameBiz, EnableGenshinHDR);
                }
            }
            // 游戏运行时应用的设置无法生效
            ErrorMessage = Lang.GameSettingPage_SettingNotEffect;
            IsApplyButtonEnable = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _logger.LogError(ex, "Apply Setting");
        }
    }



    private DisplayInformation _displayInformation;

    private void _displayInformation_AdvancedColorInfoChanged(DisplayInformation sender, object args)
    {
        UpdateHdrState(sender);
    }


    private void UpdateHdrState(DisplayInformation displayInformation)
    {
        try
        {
            HDRNotEnabled = false;
            HDRNotSupported = false;
            var info = displayInformation.GetAdvancedColorInfo();
            if (!info.IsAdvancedColorKindAvailable(DisplayAdvancedColorKind.HighDynamicRange))
            {
                HDRNotSupported = true;
            }
            else if (info.CurrentAdvancedColorKind is not DisplayAdvancedColorKind.HighDynamicRange)
            {
                HDRNotEnabled = true;
            }
        }
        catch { }
    }




    #region 后启动程序（D24/D28）

    private readonly Starward.Features.Companion.CompanionRunner _companionRunner = AppConfig.GetService<Starward.Features.Companion.CompanionRunner>();


    /// <summary>进入页面时刷新后启动程序列表。</summary>
    private bool _profileSelectorReady;


    private void RefreshProfileSelector()
    {
        _profileSelectorReady = false;
        IReadOnlyList<Starward.Features.Runtime.RuntimeProfile> profiles = Starward.Features.Runtime.RuntimeProfileStore.LoadAll();
        ComboBox_Profile.ItemsSource = profiles;
        Starward.Core.GameBiz biz = AppConfig.CurrentGameBiz;
        Starward.Features.Runtime.RuntimeProfile? current = Starward.Features.Runtime.ProfileBinding.Resolve(biz);
        ComboBox_Profile.SelectedItem = current is null ? null : profiles.FirstOrDefault(p => p.Id == current.Id);
        _profileSelectorReady = true;
    }


    private void ComboBox_Profile_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_profileSelectorReady)
        {
            return;
        }
        if (ComboBox_Profile.SelectedItem is Starward.Features.Runtime.RuntimeProfile profile)
        {
            Starward.Features.Runtime.ProfileBinding.Set(AppConfig.CurrentGameBiz, profile.Id);
            Helpers.InAppToast.MainWindow?.Success((string?)null, $"已切换为「{profile.Name}」", 3000);
        }
    }


    private void RefreshCompanions()
    {
        StackPanel panel = Panel_Companions;
        panel.Children.Clear();
        IReadOnlyList<Starward.Features.Companion.CompanionEntry> entries = Starward.Features.Companion.CompanionStore.Load(CurrentGameBiz);
        TextBlock_CompanionEmpty.Visibility = entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (Starward.Features.Companion.CompanionEntry entry in entries)
        {
            panel.Children.Add(CreateCompanionRow(entry));
        }
    }


    private Border CreateCompanionRow(Starward.Features.Companion.CompanionEntry entry)
    {
        StackPanel texts = new() { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock { Text = entry.Name, FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        texts.Children.Add(new TextBlock
        {
            Text = $"{entry.DisplayArguments}    ·    {entry.DisplayProfile}",
            FontSize = 12,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextTrimming = TextTrimming.CharacterEllipsis,
            HorizontalAlignment = HorizontalAlignment.Left,
            MaxWidth = 520,
        });
        texts.Children.Add(new TextBlock
        {
            Text = entry.Enabled ? (entry.Companion ? $"伴随启动：开 · {entry.TriggerText}" : "伴随启动：关") : "已禁用",
            FontSize = 12,
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorTertiaryBrush"],
        });

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Button runButton = new() { Content = Lang.Lunarward_Companion_LaunchNow };
        runButton.Click += async (_, _) =>
        {
            try
            {
                _companionRunner.StartEntry(CurrentGameBiz, entry);
                Helpers.InAppToast.MainWindow?.Success(null, $"已启动 {entry.Name}", 4000);
            }
            catch (Exception ex)
            {
                AppConfig.GetLogger<GameSettingPage>().LogError(ex, "Start companion failed");
                Helpers.InAppToast.MainWindow?.Error((string?)null, $"启动失败：{ex.Message}");
            }
            await Task.CompletedTask;
        };
        Button editButton = new() { Content = Lang.Common_Edit };
        editButton.Click += async (_, _) => await ShowCompanionEditorAsync(entry);
        Button deleteButton = new() { Content = Lang.Common_Delete };
        deleteButton.Click += async (_, _) =>
        {
            Starward.Features.Companion.CompanionStore.Remove(CurrentGameBiz, entry.Id);
            RefreshProfileSelector();
        RefreshCompanions();
            await Task.CompletedTask;
        };
        buttons.Children.Add(runButton);
        buttons.Children.Add(editButton);
        buttons.Children.Add(deleteButton);

        Grid grid = new() { ColumnSpacing = 12 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(texts);
        Grid.SetColumn(buttons, 1);
        grid.Children.Add(buttons);

        return new Border
        {
            Padding = new Thickness(16, 12, 16, 12),
            CornerRadius = new CornerRadius(8),
            Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
            Child = grid,
        };
    }


    private async void Button_AddCompanion_Click(object sender, RoutedEventArgs e)
    {
        Starward.Features.Companion.CompanionEntry entry = new() { Name = "新建后启动程序" };
        await ShowCompanionEditorAsync(entry, isNew: true);
    }


    private void Button_OpenCompanionFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(Starward.Features.Companion.CompanionStore.Folder);
        Process.Start(new ProcessStartInfo("xdg-open") { ArgumentList = { Starward.Features.Companion.CompanionStore.Folder }, UseShellExecute = false });
    }


    private async Task ShowCompanionEditorAsync(Starward.Features.Companion.CompanionEntry entry, bool isNew = false)
    {
        TextBox nameBox = new() { Header = Lang.Lunarward_Field_Name, Text = entry.Name };
        TextBox pathBox = new() { Header = "可执行文件或脚本路径", Text = entry.Path, PlaceholderText = "/path/to/tool" };
        Button browseButton = new() { Content = "浏览…", VerticalAlignment = VerticalAlignment.Bottom };
        browseButton.Click += async (_, _) =>
        {
            string? file = await Helpers.FileDialogHelper.PickSingleFileAsync(XamlRoot, ("所有文件", "*"));
            if (!string.IsNullOrWhiteSpace(file))
            {
                pathBox.Text = file;
            }
        };
        Grid pathGrid = new() { ColumnSpacing = 8 };
        pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        pathGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(browseButton, 1);
        pathGrid.Children.Add(pathBox);
        pathGrid.Children.Add(browseButton);

        TextBox argsBox = new() { Header = Lang.Lunarward_Field_Arguments, Text = entry.Arguments, PlaceholderText = "--flag value" };
        NumberBox delayBox = new() { Header = "延迟启动（毫秒）", Value = entry.DelayMs, Minimum = 0, Maximum = 600000, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };

        ComboBox profileBox = new() { Header = "运行环境（Profile）" };
        profileBox.Items.Add("跟随游戏 Profile");
        foreach (Starward.Features.Runtime.RuntimeProfile profile in Starward.Features.Runtime.RuntimeProfileStore.LoadAll())
        {
            profileBox.Items.Add(profile.Name);
            if (profile.Id == entry.ProfileId)
            {
                profileBox.SelectedIndex = profileBox.Items.Count - 1;
            }
        }
        if (profileBox.SelectedIndex < 0)
        {
            profileBox.SelectedIndex = 0;
        }

        ComboBox triggerCombo = new() { Header = Lang.Lunarward_Companion_Trigger, HorizontalAlignment = HorizontalAlignment.Stretch };
        int triggerIndex = 0;
        string[] triggers = { "on_game_started", "after_delay", "on_game_exit" };
        string[] triggerLabels = { Lang.Lunarward_Companion_TriggerAfterStart, "游戏启动后延迟（见下方延迟毫秒）", Lang.Lunarward_Companion_TriggerOnExit };
        for (int i = 0; i < triggers.Length; i++)
        {
            triggerCombo.Items.Add(new ComboBoxItem { Content = triggerLabels[i], Tag = triggers[i] });
            if (entry.Trigger == triggers[i])
            {
                triggerIndex = i;
            }
        }
        triggerCombo.SelectedIndex = triggerIndex;

        ToggleSwitch companionSwitch = new() { Header = Lang.Lunarward_Companion_On, OnContent = "满足触发时机时自动运行", OffContent = "仅手动启动", IsOn = entry.Companion };
        ToggleSwitch enabledSwitch = new() { Header = "启用", OnContent = "启用", OffContent = "禁用", IsOn = entry.Enabled };

        StackPanel content = new() { Spacing = 12, MinWidth = 480 };
        content.Children.Add(nameBox);
        content.Children.Add(pathGrid);
        content.Children.Add(argsBox);
        content.Children.Add(profileBox);
        content.Children.Add(delayBox);
        content.Children.Add(triggerCombo);
        content.Children.Add(companionSwitch);
        content.Children.Add(enabledSwitch);
        content.Children.Add(new TextBlock
        {
            Text = "「伴随启动」会把条目在游戏启动后自动拉起，适合补丁 / 钩子 / 注入类工具；开启前请确认该工具不会被反作弊判定。",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        ContentDialog dialog = new()
        {
            Title = isNew ? "添加后启动程序" : "编辑后启动程序",
            Content = new ScrollViewer { Content = content, MaxHeight = 520 },
            PrimaryButtonText = "保存",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
        };

        bool companionWasOff = !entry.Companion;
        ContentDialogResult result = await dialog.ShowAsync();
        if (result is not ContentDialogResult.Primary)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(pathBox.Text))
        {
            Helpers.InAppToast.MainWindow?.Warning(null, "请填写可执行文件路径");
            return;
        }
        // D24：开启「伴随启动」需二次确认
        if (companionSwitch.IsOn && companionWasOff)
        {
            ContentDialog confirm = new()
            {
                Title = "开启伴随启动？",
                Content = $"游戏启动后会自动运行「{nameBox.Text}」。如果它注入游戏进程或修改内存，可能被反作弊判定为异常，风险自负。",
                PrimaryButtonText = "开启",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot,
            };
            if (await confirm.ShowAsync() is not ContentDialogResult.Primary)
            {
                companionSwitch.IsOn = false;
            }
        }

        entry.Name = string.IsNullOrWhiteSpace(nameBox.Text) ? System.IO.Path.GetFileName(pathBox.Text) : nameBox.Text;
        entry.Path = pathBox.Text.Trim();
        entry.Arguments = argsBox.Text ?? "";
        entry.DelayMs = (int)(double.IsNaN(delayBox.Value) ? 0 : delayBox.Value);
        entry.ProfileId = profileBox.SelectedIndex <= 0
            ? null
            : Starward.Features.Runtime.RuntimeProfileStore.LoadAll()[profileBox.SelectedIndex - 1].Id;
        entry.Companion = companionSwitch.IsOn;
        if (triggerCombo.SelectedItem is ComboBoxItem selectedTrigger && selectedTrigger.Tag is string trigger)
        {
            entry.Trigger = trigger;
        }
        entry.Enabled = enabledSwitch.IsOn;

        Starward.Features.Companion.CompanionStore.Upsert(CurrentGameBiz, entry);
        RefreshProfileSelector();
        RefreshCompanions();
    }

    #endregion


}
