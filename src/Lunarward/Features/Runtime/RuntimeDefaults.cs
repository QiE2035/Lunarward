// Lunarward：运行环境的全局默认值（D85）——目前只有一项：新建方案默认用哪个运行器。
// 位置：$XDG_CONFIG_HOME|~/.config/lunarward/runtime-defaults.json
// 为什么不塞进每份配方：这是「所有新方案的起点」，配方自己那份是覆盖，混在一起会让「恢复默认」没有参照。
using Starward.Helpers;
using System;
using System.IO;
using System.Text.Json;


namespace Starward.Features.Runtime;


public static class RuntimeDefaults
{


    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };


    public static string FilePath
    {
        get
        {
            string configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } custom
                ? custom
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(configHome, "lunarward", "runtime-defaults.json");
        }
    }


    /// <summary>新建方案默认用的运行器（存 <see cref="Component.Id"/>，外部运行器就是可执行文件路径）。</summary>
    public static string? DefaultRunnerId
    {
        get => Read()?.DefaultRunnerId;
        set
        {
            Payload payload = Read() ?? new Payload();
            payload.DefaultRunnerId = string.IsNullOrWhiteSpace(value) ? null : value;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                JsonFile.WriteAtomic(FilePath, payload, JsonOptions);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "save runtime defaults failed");
            }
        }
    }


    private static Payload? Read()
    {
        try
        {
            return File.Exists(FilePath) ? JsonFile.ReadOrRecover<Payload>(FilePath, JsonOptions) : null;
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "read runtime defaults failed");
            return null;
        }
    }


    private sealed class Payload
    {
        public string? DefaultRunnerId { get; set; }
    }


}
