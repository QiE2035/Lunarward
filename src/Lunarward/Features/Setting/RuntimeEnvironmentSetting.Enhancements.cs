// Lunarward：「增强」页签（D81）：HUD / gamemode / gamescope。
// 这些开关是方案级字段，所以页签顶部先选方案；保存后写回 env/<id>.json，与编辑器里的「启动参数 / 环境变量」并存。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Starward.Features.Runtime;

namespace Starward.Features.Setting;


public sealed partial class RuntimeEnvironmentSetting
{

    /// <summary>回填控件时置位，避免 TextChanged / Toggled 又把它写回模型。</summary>
    private bool _enhanceLoading;

    private List<RuntimeProfile> _enhanceProfiles = new();


    private void InitializeEnhance()
    {
        Combo_Hud.Items.Clear();
        Combo_Hud.Items.Add(Lang.Lunarward_RuntimeEnv_Close2);
        Combo_Hud.Items.Add("DXVK HUD");
        Combo_Hud.Items.Add("MangoHud");
        Combo_Hud.SelectedIndex = 0;
        // I：HUD / gamescope 预设（下标与 Enhancements.*Preset* 数组对齐，0 代表「不预设」）
        Combo_HudPreset.Items.Clear();
        Combo_HudPreset.Items.Add(Lang.Lunarward_RuntimeEnv_PresetNone);
        Combo_HudPreset.Items.Add(Lang.Lunarward_RuntimeEnv_HudPresetFps);
        Combo_HudPreset.Items.Add(Lang.Lunarward_RuntimeEnv_HudPresetFull);
        Combo_HudPreset.Items.Add(Lang.Lunarward_RuntimeEnv_HudPresetMinimal);
        Combo_HudPreset.Items.Add(Lang.Lunarward_RuntimeEnv_HudPresetGpu);
        Combo_HudPreset.SelectedIndex = 0;
        Combo_GameScopePreset.Items.Clear();
        Combo_GameScopePreset.Items.Add(Lang.Lunarward_RuntimeEnv_PresetNone);
        Combo_GameScopePreset.Items.Add(Lang.Lunarward_RuntimeEnv_Preset1080p);
        Combo_GameScopePreset.Items.Add(Lang.Lunarward_RuntimeEnv_Preset1440p);
        Combo_GameScopePreset.Items.Add(Lang.Lunarward_RuntimeEnv_Preset720pWindow);
        Combo_GameScopePreset.SelectedIndex = 0;
    }


    /// <summary>D85：从方案详情的「配置」进来。原来这里是独立页签 + 顶部方案下拉，
    /// 现在直接沿用左列的选中项，进来就是这份方案的开关。</summary>
    private void OpenEnhance(RuntimeProfile profile)
    {
        _selectedProfileId = profile.Id;
        UpdateProfileListSelection();
        Text_EnhanceTitle.Text = profile.Name;
        LoadEnhanceFields(profile);
        ShowEnhancePane();
    }


    private void LoadEnhanceFields(RuntimeProfile? profile)
    {
        _enhanceLoading = true;
        if (profile is null)
        {
            Combo_Hud.SelectedIndex = 0;
            Box_HudOptions.Text = "";
            Toggle_GameMode.IsOn = false;
            Toggle_GameScope.IsOn = false;
            Box_GameScopeArgs.Text = "";
            Toggle_Hdr.IsOn = false;
            Toggle_Vrr.IsOn = false;
        }
        else
        {
            Combo_Hud.SelectedIndex = Enhancements.NormalizeHud(profile.Hud) switch
            {
                Enhancements.HudDxvk => 1,
                Enhancements.HudMangoHud => 2,
                _ => 0,
            };
            Box_HudOptions.Text = profile.HudOptions ?? "";
            Toggle_GameMode.IsOn = profile.GameMode;
            Toggle_GameScope.IsOn = profile.GameScope;
            Box_GameScopeArgs.Text = profile.GameScopeArgs ?? "";
            string scopeArgs = profile.GameScopeArgs ?? "";
            Toggle_Hdr.IsOn = scopeArgs.Contains("--hdr-enabled", StringComparison.Ordinal);
            Toggle_Vrr.IsOn = scopeArgs.Contains("--adaptive-sync", StringComparison.Ordinal);
        }
        _enhanceLoading = false;
        UpdateEnhanceSummary();
    }


    /// <summary>把控件上的值套到一个临时副本上（不落盘），用于状态与预览。</summary>
    private RuntimeProfile? ComposeEnhanceDraft()
    {
        RuntimeProfile? profile = SelectedProfileOrNull();
        if (profile is null)
        {
            return null;
        }
        var draft = new RuntimeProfile
        {
            Id = profile.Id,
            Name = profile.Name,
            RunnerId = profile.RunnerId,
            RunnerPath = profile.RunnerPath,
            PrefixPath = profile.PrefixPath,
            Hud = Combo_Hud.SelectedIndex switch { 1 => Enhancements.HudDxvk, 2 => Enhancements.HudMangoHud, _ => Enhancements.HudOff },
            HudOptions = Box_HudOptions.Text?.Trim() ?? "",
            GameMode = Toggle_GameMode.IsOn,
            GameScope = Toggle_GameScope.IsOn,
            GameScopeArgs = Box_GameScopeArgs.Text?.Trim() ?? "",
            Args = profile.Args,
            Wrappers = profile.Wrappers,
        };
        return draft;
    }


    private void UpdateEnhanceSummary()
    {
        RuntimeProfile? draft = ComposeEnhanceDraft();
        if (draft is null)
        {
            Text_EnhanceStatus.Text = Lang.Lunarward_RuntimeEnv_NoProfilesHint;
            Text_EnhancePreview.Text = "";
            Text_HudHint.Text = "";
            Combo_Hud.IsEnabled = false;
            return;
        }
        Combo_Hud.IsEnabled = true;

        string hud = Enhancements.NormalizeHud(draft.Hud);
        Text_HudHint.Text = hud switch
        {
            Enhancements.HudDxvk => string.IsNullOrWhiteSpace(draft.HudOptions)
                ? $"留空则用默认：{Enhancements.DefaultDxvkHud}（DXVK_HUD）"
                : Lang.Lunarward_RuntimeEnv_HudDxvkHint,
            Enhancements.HudMangoHud => draft.GameScope
                ? Lang.Lunarward_RuntimeEnv_HudMangoScopeHint
                : (string.IsNullOrWhiteSpace(draft.HudOptions)
                    ? Lang.Lunarward_RuntimeEnv_HudMangoHint
                    : Lang.Lunarward_RuntimeEnv_HudMangoConfig),
            _ => "",
        };
        Box_HudOptions.IsEnabled = hud != Enhancements.HudOff;
        Box_GameScopeArgs.IsEnabled = draft.GameScope;

        var status = new List<string>();
        foreach (string binary in Enhancements.RequiredBinaries(draft))
        {
            string? found = Enhancements.FindInPath(binary);
            status.Add(found is null ? $"✗ {binary}（PATH 里没找到）" : $"✓ {binary} · {found}");
        }
        if (draft.GameScope || !string.IsNullOrWhiteSpace(draft.GameScopeArgs))
        {
            foreach (string problem in Enhancements.ValidateGameScopeArgs(draft.GameScopeArgs))
            {
                status.Add("⚠ " + problem);
            }
        }
        Text_EnhanceStatus.Text = status.Count == 0 ? "" : string.Join("\n", status);

        List<string> argv = LaunchRecipeBuilder.BuildArgv(Enhancements.Wrappers(draft), "wine", "game.exe", draft.Args);
        Text_EnhancePreview.Text = LaunchRecipeBuilder.JoinCommandLine(argv);
    }


    private void Combo_Hud_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_enhanceLoading)
        {
            UpdateEnhanceSummary();
        }
    }


    private void Box_HudOptions_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_enhanceLoading)
        {
            UpdateEnhanceSummary();
        }
    }


    private void Box_GameScopeArgs_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_enhanceLoading)
        {
            UpdateEnhanceSummary();
        }
    }


    private void Toggle_GameMode_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_enhanceLoading)
        {
            UpdateEnhanceSummary();
        }
    }


    private void Toggle_GameScope_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_enhanceLoading)
        {
            UpdateEnhanceSummary();
        }
    }


    private void Button_EnhanceReload_Click(object sender, RoutedEventArgs e)
    {
        LoadEnhanceFields(SelectedProfileOrNull());
    }


    /// <summary>I：HUD 预设把参数填进框里（选完复位，避免和手改的值打架）。</summary>
    private void Combo_HudPreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_enhanceLoading || Combo_HudPreset.SelectedIndex <= 0)
        {
            return;
        }
        int index = Combo_HudPreset.SelectedIndex;
        if (index < Enhancements.HudPresetOptions.Length)
        {
            Box_HudOptions.Text = Enhancements.HudPresetOptions[index];
        }
        Combo_HudPreset.SelectedIndex = 0;
    }


    /// <summary>I：分辨率预设顺便把 gamescope 打开。</summary>
    private void Combo_GameScopePreset_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_enhanceLoading || Combo_GameScopePreset.SelectedIndex <= 0)
        {
            return;
        }
        int index = Combo_GameScopePreset.SelectedIndex;
        if (index < Enhancements.GameScopePresetArgs.Length)
        {
            Box_GameScopeArgs.Text = Enhancements.GameScopePresetArgs[index];
        }
        if (!Toggle_GameScope.IsOn)
        {
            Toggle_GameScope.IsOn = true;
        }
        Combo_GameScopePreset.SelectedIndex = 0;
    }


    private void Toggle_Hdr_Toggled(object sender, RoutedEventArgs e)
    {
        if (_enhanceLoading)
        {
            return;
        }
        Box_GameScopeArgs.Text = Enhancements.ToggleArg(Box_GameScopeArgs.Text, "--hdr-enabled", Toggle_Hdr.IsOn);
    }


    private void Toggle_Vrr_Toggled(object sender, RoutedEventArgs e)
    {
        if (_enhanceLoading)
        {
            return;
        }
        Box_GameScopeArgs.Text = Enhancements.ToggleArg(Box_GameScopeArgs.Text, "--adaptive-sync", Toggle_Vrr.IsOn);
    }


    private async void Button_EnhanceSave_Click(object sender, RoutedEventArgs e)
    {
        RuntimeProfile? profile = SelectedProfileOrNull();
        if (profile is null)
        {
            return;
        }
        profile.Hud = Combo_Hud.SelectedIndex switch { 1 => Enhancements.HudDxvk, 2 => Enhancements.HudMangoHud, _ => Enhancements.HudOff };
        profile.HudOptions = Box_HudOptions.Text?.Trim() ?? "";
        profile.GameMode = Toggle_GameMode.IsOn;
        profile.GameScope = Toggle_GameScope.IsOn;
        profile.GameScopeArgs = Box_GameScopeArgs.Text?.Trim() ?? "";
        if (!RuntimeProfileStore.Save(profile, _logger))
        {
            Helpers.InAppToast.MainWindow?.Error(Lang.Lunarward_RuntimeEnv_SaveFailed, null, 4000);
            return;
        }
        Helpers.InAppToast.MainWindow?.Success($"已保存「{profile.Name}」的增强设置", Enhancements.Describe(profile), 4000);
        LoadProfiles();
        await RefreshChipsAsync();
    }

}
