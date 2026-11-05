// Lunarward：上游把少量配置存在 Windows 注册表（Software\Starward），Linux 侧改存
// ~/.config/lunarward/config.json（D22 的 XDG 约定），键名保持一致以便导入导出。
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Starward;

internal static class LinuxConfig
{
    private static Dictionary<string, string?>? _values;

    public static string ConfigDirectory
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var root = string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
                : xdg;
            return Path.Combine(root, "lunarward");
        }
    }

    public static string ConfigFile => Path.Combine(ConfigDirectory, "config.json");

    private static Dictionary<string, string?> Values
    {
        get
        {
            if (_values is not null)
            {
                return _values;
            }
            try
            {
                if (File.Exists(ConfigFile))
                {
                    _values = JsonSerializer.Deserialize<Dictionary<string, string?>>(File.ReadAllText(ConfigFile)) ?? new();
                }
                else
                {
                    _values = new();
                }
            }
            catch
            {
                _values = new();
            }
            return _values;
        }
    }

    public static string? Get(string key) => Values.TryGetValue(key, out var v) ? v : null;

    public static void Set(string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Values.Remove(key);
        }
        else
        {
            Values[key] = value;
        }
    }

    public static void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            JsonFile.WriteAtomic(ConfigFile, Values, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            // 配置写入失败不应阻断启动
        }
    }
}
