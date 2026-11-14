// Lunarward：启动报告（每次启动写一份 JSON，便于事后对照「到底用了什么」）。
using Starward.Core;
using Starward.Features.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Starward.Features.GameLauncher;


public static class LaunchReport
{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };


    public static string LogFolder
    {
        get
        {
            string data = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } x
                ? x
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(data, "lunarward", "logs", "launch");
        }
    }


    public static string? Write(GameBiz biz, string exe, string? arg, RuntimeProfile? profile, PreflightResult preflight, ProcessStartInfo info, bool viaWine)
    {
        try
        {
            Directory.CreateDirectory(LogFolder);
            var report = new
            {
                Time = DateTime.Now,
                Game = biz.Value,
                GameName = biz.ToGameName(),
                Exe = exe,
                Arguments = arg,
                Profile = profile is null ? null : new
                {
                    profile.Id,
                    profile.Name,
                    profile.RunnerId,
                    profile.RunnerPath,
                    profile.PrefixPath,
                    profile.PrefixArch,
                    profile.GraphicsId,
                    profile.Args,
                    profile.Wrappers,
                },
                Command = info.FileName,
                Argv = info.ArgumentList.ToArray(),
                WorkingDirectory = info.WorkingDirectory,
                ViaWine = viaWine,
                Env = info.Environment
                    .Where(x => x.Key is "WINEPREFIX" or "WINEDEBUG" or "LUNARWARD_WINE" or "LUNARWARD_PROFILE" or "LUNARWARD_PROFILE_ID" or "DXVK_HUD" or "DXVK_STATE_CACHE" or "MANGOHUD")
                    .ToDictionary(x => x.Key, x => x.Value),
                Preflight = preflight.Items.Select(x => new { x.Name, Status = x.Status.ToString(), x.Detail, x.Fix }).ToArray(),
                CanLaunch = preflight.CanLaunch,
            };
            string path = Path.Combine(LogFolder, $"{DateTime.Now:yyyyMMdd-HHmmss}-{biz.Value}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
            return path;
        }
        catch
        {
            return null;
        }
    }

}
