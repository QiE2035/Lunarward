using Starward.Core;
using Starward.Features.GameAccount;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameSetting;

internal static class GameSettingService
{


    private const string GENERAL_DATA_h2389025596 = "GENERAL_DATA_h2389025596";
    private const string Screenmanager_Is_Fullscreen_mode_h3981298716 = "Screenmanager Is Fullscreen mode_h3981298716";
    private const string Screenmanager_Resolution_Height_h2627697771 = "Screenmanager Resolution Height_h2627697771";
    private const string Screenmanager_Resolution_Width_h182942802 = "Screenmanager Resolution Width_h182942802";


    private const string AudioSettings_BGMVolume_h240914409 = "AudioSettings_BGMVolume_h240914409";
    private const string AudioSettings_MasterVolume_h1622207037 = "AudioSettings_MasterVolume_h1622207037";
    private const string AudioSettings_SFXVolume_h2753520268 = "AudioSettings_SFXVolume_h2753520268";
    private const string AudioSettings_VOVolume_h805685304 = "AudioSettings_VOVolume_h805685304";
    private const string GraphicsSettings_GraphicsQuality_h523255858 = "GraphicsSettings_GraphicsQuality_h523255858";
    private const string GraphicsSettings_Model_h2986158309 = "GraphicsSettings_Model_h2986158309";
    private const string GraphicsSettings_PCResolution_h431323223 = "GraphicsSettings_PCResolution_h431323223";
    // cn en jp kr
    private const string LanguageSettings_LocalAudioLanguage_h882585060 = "LanguageSettings_LocalAudioLanguage_h882585060";
    private const string LanguageSettings_LocalTextLanguage_h2764291023 = "LanguageSettings_LocalTextLanguage_h2764291023";
    // 1 full, 3 window
    private const string Screenmanager_Fullscreen_mode_h3630240806 = "Screenmanager Fullscreen mode_h3630240806";


    private const string GENERAL_DATA_V2_ScreenSettingData_h1916288658 = "GENERAL_DATA_V2_ScreenSettingData_h1916288658";

    private const string WINDOWS_HDR_ON_h3132281285 = "WINDOWS_HDR_ON_h3132281285";


    public static GraphicsSettings_PCResolution_h431323223? GetGameResolutionSetting(GameBiz biz)
    {
        var keyPath = biz.GetGameRegistryKey();
        if (biz.ToGame().Value is GameBiz.bh3 or GameBiz.hkrpg)
        {
            var keyName = biz.ToGame().Value switch
            {
                GameBiz.bh3 => GENERAL_DATA_V2_ScreenSettingData_h1916288658,
                GameBiz.hkrpg => GraphicsSettings_PCResolution_h431323223,
                _ => ""
            };
            var data = WineRegistry.GetValue(keyPath, keyName, null) as byte[];
            if (data is not null)
            {
                var str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                return JsonSerializer.Deserialize<GraphicsSettings_PCResolution_h431323223>(str);
            }
        }
        if (biz.ToGame() == GameBiz.hk4e)
        {
            var fullScreen = (int)(WineRegistry.GetValue(keyPath, Screenmanager_Is_Fullscreen_mode_h3981298716, 0) ?? 0) != 0;
            var width = (int)(WineRegistry.GetValue(keyPath, Screenmanager_Resolution_Width_h182942802, 0) ?? 0);
            var height = (int)(WineRegistry.GetValue(keyPath, Screenmanager_Resolution_Height_h2627697771, 0) ?? 0);
            if (width * height > 0)
            {
                return new GraphicsSettings_PCResolution_h431323223 { Width = width, Height = height, IsFullScreen = fullScreen };
            }
        }
        if (biz.ToGame() == GameBiz.nap)
        {
            var fullScreen = (int)(WineRegistry.GetValue(keyPath, Screenmanager_Fullscreen_mode_h3630240806, 0) ?? 0) != 3;
            var width = (int)(WineRegistry.GetValue(keyPath, Screenmanager_Resolution_Width_h182942802, 0) ?? 0);
            var height = (int)(WineRegistry.GetValue(keyPath, Screenmanager_Resolution_Height_h2627697771, 0) ?? 0);
            if (width * height > 0)
            {
                return new GraphicsSettings_PCResolution_h431323223 { Width = width, Height = height, IsFullScreen = fullScreen };
            }
        }
        return null;
    }



    public static void SetGameResolutionSetting(GameBiz biz, GraphicsSettings_PCResolution_h431323223 model)
        => SetGameResolutionSettingAsync(biz, model).GetAwaiter().GetResult();



    /// <summary>UI 路径必须用这个：写注册表是一次 `wine reg import` fork，同步版会卡住 UI（L-127）。</summary>
    public static Task SetGameResolutionSettingAsync(GameBiz biz, GraphicsSettings_PCResolution_h431323223 model, CancellationToken cancellationToken = default)
    {
        var values = BuildResolutionSettingValues(biz, model, out string keyPath);
        return values.Count == 0 ? Task.CompletedTask : WineRegistry.SetValuesAsync(keyPath, values, null, cancellationToken);
    }


    internal static Dictionary<string, object> BuildResolutionSettingValues(GameBiz biz, GraphicsSettings_PCResolution_h431323223 model, out string keyPath)
    {
        keyPath = biz.GetGameRegistryKey();
        var values = new Dictionary<string, object>();
        switch (biz.ToGame().Value)
        {
            case GameBiz.bh3:
                values[GENERAL_DATA_V2_ScreenSettingData_h1916288658] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model) + "\0");
                values[Screenmanager_Is_Fullscreen_mode_h3981298716] = model.IsFullScreen ? 1 : 0;
                values[Screenmanager_Resolution_Width_h182942802] = model.Width;
                values[Screenmanager_Resolution_Height_h2627697771] = model.Height;
                break;
            case GameBiz.hkrpg:
                values[GraphicsSettings_PCResolution_h431323223] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(model) + "\0");
                values[Screenmanager_Fullscreen_mode_h3630240806] = model.IsFullScreen ? 1 : 3;
                values[Screenmanager_Resolution_Width_h182942802] = model.Width;
                values[Screenmanager_Resolution_Height_h2627697771] = model.Height;
                break;
            case GameBiz.hk4e:
                values[Screenmanager_Is_Fullscreen_mode_h3981298716] = model.IsFullScreen ? 1 : 0;
                values[Screenmanager_Resolution_Width_h182942802] = model.Width;
                values[Screenmanager_Resolution_Height_h2627697771] = model.Height;
                break;
            case GameBiz.nap:
                values[Screenmanager_Fullscreen_mode_h3630240806] = model.IsFullScreen ? 1 : 3;
                values[Screenmanager_Resolution_Width_h182942802] = model.Width;
                values[Screenmanager_Resolution_Height_h2627697771] = model.Height;
                break;
        }
        return values;
    }




    public static int? GetGameVoiceLanguageSetting(GameBiz biz)
    {
        var keyPath = biz.GetGameRegistryKey();
        if (biz.ToGame() == GameBiz.hk4e)
        {
            var data = WineRegistry.GetValue(keyPath, GENERAL_DATA_h2389025596, null) as byte[];
            if (data is not null)
            {
                var str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                var node = JsonNode.Parse(str);
                return node?["deviceVoiceLanguageType"]?.GetValue<int>();
            }
        }
        if (biz.ToGame() == GameBiz.hkrpg)
        {
            var data = WineRegistry.GetValue(keyPath, LanguageSettings_LocalAudioLanguage_h882585060, null) as byte[];
            if (data is not null)
            {
                var str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                return str switch
                {
                    "cn" => 0,
                    "en" => 1,
                    "jp" => 2,
                    "kr" => 3,
                    _ => null,
                };
            }
        }
        return null;
    }


    public static void SetGameVoiceLanguageSetting(GameBiz biz, int lang)
        => SetGameVoiceLanguageSettingAsync(biz, lang).GetAwaiter().GetResult();



    public static async Task SetGameVoiceLanguageSettingAsync(GameBiz biz, int lang, CancellationToken cancellationToken = default)
    {
        string keyPath = biz.GetGameRegistryKey();
        if (biz.Game is GameBiz.hk4e)
        {
            // hk4e 是「读 JSON → 改一个字段 → 写回」，需要先异步读一次（同一键，命中缓存时不 fork）
            var values = await WineRegistry.GetKeyValuesAsync(keyPath, null, cancellationToken).ConfigureAwait(false);
            JsonNode? node = null;
            if (values.TryGetValue(GENERAL_DATA_h2389025596, out object? raw) && raw is byte[] data)
            {
                var str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                node = JsonNode.Parse(str);
                node?["deviceVoiceLanguageType"] = lang;
            }
            string value = $"{node?.ToJsonString() ?? ($$"""{"deviceVoiceLanguageType":{{lang}}}""")}\0";
            await WineRegistry.SetValueAsync(keyPath, GENERAL_DATA_h2389025596, Encoding.UTF8.GetBytes(value), null, cancellationToken).ConfigureAwait(false);
        }
        if (biz.Game is GameBiz.hkrpg)
        {
            var str = lang switch
            {
                0 => "cn\0",
                1 => "en\0",
                2 => "jp\0",
                3 => "kr\0",
                _ => null,
            };
            if (!string.IsNullOrWhiteSpace(str))
            {
                await WineRegistry.SetValueAsync(keyPath, LanguageSettings_LocalAudioLanguage_h882585060, Encoding.UTF8.GetBytes(str), null, cancellationToken).ConfigureAwait(false);
            }
        }
    }



    public static int GetStarRailFPSIndex(GameBiz biz)
    {
        if (biz.Game is GameBiz.hkrpg)
        {
            string key = biz.GetGameRegistryKey();
            byte[]? data = WineRegistry.GetValue(key, GraphicsSettings_Model_h2986158309, null) as byte[];
            if (data is not null)
            {
                string str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                JsonNode? node = JsonNode.Parse(str);
                if (node is not null)
                {
                    int fps = node["FPS"]?.GetValue<int>() ?? 60;
                    return fps switch
                    {
                        30 => 0,
                        120 => 2,
                        _ => 1,
                    };
                }
            }
        }
        return 1;
    }


    public static void SetStarRailFPSIndex(GameBiz biz, int fpsIndex)
        => SetStarRailFPSIndexAsync(biz, fpsIndex).GetAwaiter().GetResult();



    public static async Task SetStarRailFPSIndexAsync(GameBiz biz, int fpsIndex, CancellationToken cancellationToken = default)
    {
        if (biz.Game is not GameBiz.hkrpg)
        {
            return;
        }
        int fps = fpsIndex switch
        {
            0 => 30,
            1 => 60,
            2 => 120,
            _ => 60,
        };
        string key = biz.GetGameRegistryKey();
        var values = await WineRegistry.GetKeyValuesAsync(key, null, cancellationToken).ConfigureAwait(false);
        JsonNode? node = null;
        if (values.TryGetValue(GraphicsSettings_Model_h2986158309, out object? raw) && raw is byte[] data)
        {
            string str = Encoding.UTF8.GetString(data).TrimEnd('\0');
            node = JsonNode.Parse(str);
            node?["FPS"] = fps;
        }
        string? value = node?.ToJsonString();
        if (string.IsNullOrWhiteSpace(value))
        {
            value = $$"""
                {"FPS":{{fps}},"EnableVSync":false,"RenderScale":1.0,"ResolutionQuality":3,"ShadowQuality":3,"LightQuality":3,"CharacterQuality":3,"EnvDetailQuality":3,"ReflectionQuality":3,"SFXQuality":3,"BloomQuality":3,"AAMode":1,"EnableMetalFXSU":false,"EnableHalfResTransparent":false,"EnableSelfShadow":1,"DlssQuality":0}
                """;
        }
        value += "\0";
        // 画质预设设置为0（自定义）时，帧率设置方能生效；两个值合成一次 reg import
        var payload = new Dictionary<string, object>
        {
            [GraphicsSettings_GraphicsQuality_h523255858] = 0,
            [GraphicsSettings_Model_h2986158309] = Encoding.UTF8.GetBytes(value),
        };
        await WineRegistry.SetValuesAsync(key, payload, null, cancellationToken).ConfigureAwait(false);
    }



    public static void SetGenshinEnableHDR(GameBiz biz, bool enableHDR, string? winePrefix = null)
        => SetGenshinEnableHDRAsync(biz, enableHDR, winePrefix).GetAwaiter().GetResult();



    public static async Task SetGenshinEnableHDRAsync(GameBiz biz, bool enableHDR, string? winePrefix = null, CancellationToken cancellationToken = default)
    {
        if (biz.Game is not GameBiz.hk4e)
        {
            return;
        }
        // 必须写进游戏真正使用的前缀，否则写的是 ~/.wine，游戏读不到（L-62）。
        string? prefix = winePrefix ?? ResolveGamePrefix(biz);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            Serilog.Log.Information("SetGenshinEnableHDR: 找不到游戏前缀，跳过（等下次启动或用设置页重写）");
            return;
        }
        string key = biz.GetGameRegistryKey();
        await WineRegistry.SetValueAsync(key, WINDOWS_HDR_ON_h3132281285, enableHDR ? 1 : 0, prefix, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>按游戏绑定的 Profile 解析「已经存在」的前缀；不会为了写设置去创建/克隆前缀。</summary>
    private static string? ResolveGamePrefix(GameBiz biz)
    {
        try
        {
            Features.Runtime.RuntimeProfile profile = Features.Runtime.ProfileBinding.Resolve(biz);
            return Features.Runtime.PrefixManager.TryGetExistingPrefix(profile, biz.Value);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "ResolveGamePrefix failed for {biz}", biz.Value);
            return null;
        }
    }





    public static (int MaxLuminance, int SceneLuminance, int UILuminance) GetGenshinHDRLuminance(GameBiz biz)
        => GetGenshinHDRLuminanceAsync(biz).GetAwaiter().GetResult();



    public static async Task<(int MaxLuminance, int SceneLuminance, int UILuminance)> GetGenshinHDRLuminanceAsync(GameBiz biz, CancellationToken cancellationToken = default)
    {
        int max = 1000, scene = 300, ui = 350;
        if (biz.Game is GameBiz.hk4e)
        {
            string key = biz.GetGameRegistryKey();
            var values = await WineRegistry.GetKeyValuesAsync(key, null, cancellationToken).ConfigureAwait(false);
            byte[]? data = values.TryGetValue(GENERAL_DATA_h2389025596, out object? raw) ? raw as byte[] : null;
            if (data is not null)
            {
                string str = Encoding.UTF8.GetString(data).TrimEnd('\0');
                JsonNode? node = JsonNode.Parse(str);
                if (node is not null)
                {
                    max = (int)(node["maxLuminosity"]?.GetValue<float>() ?? 1000);
                    scene = (int)(node["scenePaperWhite"]?.GetValue<float>() ?? 300);
                    ui = (int)(node["uiPaperWhite"]?.GetValue<float>() ?? 350);
                }
            }
        }
        max = Math.Clamp(max, 300, 2000);
        scene = Math.Clamp(scene, 100, 500);
        ui = Math.Clamp(ui, 150, 550);
        return (max, scene, ui);
    }



    public static void SetGenshinHDRLuminance(GameBiz biz, int maxLuminance, int sceneLuminance, int uiLuminance)
        => SetGenshinHDRLuminanceAsync(biz, maxLuminance, sceneLuminance, uiLuminance).GetAwaiter().GetResult();



    public static async Task SetGenshinHDRLuminanceAsync(GameBiz biz, int maxLuminance, int sceneLuminance, int uiLuminance, CancellationToken cancellationToken = default)
    {
        maxLuminance = Math.Clamp(maxLuminance, 300, 2000);
        sceneLuminance = Math.Clamp(sceneLuminance, 100, 500);
        uiLuminance = Math.Clamp(uiLuminance, 150, 550);
        if (biz.Game is not GameBiz.hk4e)
        {
            return;
        }
        string key = biz.GetGameRegistryKey();
        var values = await WineRegistry.GetKeyValuesAsync(key, null, cancellationToken).ConfigureAwait(false);
        JsonNode? node = null;
        if (values.TryGetValue(GENERAL_DATA_h2389025596, out object? raw) && raw is byte[] data)
        {
            string str = Encoding.UTF8.GetString(data).TrimEnd('\0');
            node = JsonNode.Parse(str);
            node?["maxLuminosity"] = maxLuminance;
            node?["scenePaperWhite"] = sceneLuminance;
            node?["uiPaperWhite"] = uiLuminance;
        }
        string value = (node?.ToJsonString() ?? $"{{\"maxLuminosity\":{maxLuminance},\"scenePaperWhite\":{sceneLuminance},\"uiPaperWhite\":{uiLuminance}}}") + "\0";
        await WineRegistry.SetValueAsync(key, GENERAL_DATA_h2389025596, Encoding.UTF8.GetBytes(value), null, cancellationToken).ConfigureAwait(false);
    }
}
