// Lunarward：为某个 Profile 打开调试 Shell（P4 痛点；D28 的入口 A）。
// 约定：环境变量与游戏启动完全一致，方便排查「游戏为什么起不来」。
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;


namespace Starward.Features.Runtime;


public static class ProfileShellService
{

    /// <summary>用户偏好/系统里可用的终端模拟器（按优先级）。</summary>
    private static readonly (string Executable, string[] PrefixArgs)[] Terminals =
    [
        ("konsole", ["-e"]),
        ("kitty", []),
        ("alacritty", ["-e"]),
        ("wezterm", ["start", "--"]),
        ("gnome-terminal", ["--"]),
        ("xfce4-terminal", ["-x"]),
        ("foot", []),
        ("xterm", ["-e"]),
    ];


    public static string ResolveShell(RuntimeProfile profile)
    {
        if (!string.IsNullOrWhiteSpace(profile.Shell) && File.Exists(profile.Shell))
        {
            return profile.Shell;
        }
        string userShell = Environment.GetEnvironmentVariable("SHELL") ?? "";
        if (!string.IsNullOrWhiteSpace(userShell) && File.Exists(userShell))
        {
            return userShell;
        }
        return "/bin/bash";
    }


    /// <summary>拼出调试 Shell 的进程参数（不启动）。</summary>
    public static ProcessStartInfo? CreateShellStartInfo(RuntimeProfile profile)
    {
        (string executable, string[] prefixArgs)? terminal = null;
        foreach ((string exe, string[] args) in Terminals)
        {
            string? path = FindExecutable(exe);
            if (path is not null)
            {
                terminal = (path, args);
                break;
            }
        }
        if (terminal is null)
        {
            return null;
        }

        var info = new ProcessStartInfo(terminal.Value.executable)
        {
            UseShellExecute = false,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };
        foreach (string arg in terminal.Value.prefixArgs)
        {
            info.ArgumentList.Add(arg);
        }
        info.ArgumentList.Add(ResolveShell(profile));

        foreach ((string key, string value) in BuildEnvironment(profile))
        {
            info.Environment[key] = value;
        }
        return info;
    }


    /// <summary>把 Profile 映射成一份环境变量，供调试 Shell 与后启动程序共用。</summary>
    /// <param name="gameKey">
    /// 游戏 key。给了就按这个 Profile 的前缀模式解析出「这次真正会用的前缀」——
    /// dedicated/template/ephemeral 的路径并不是 profile.PrefixPath（后者常为空），
    /// 只认 PrefixPath 会让后启动工具跑进 ~/.wine，跟游戏完全不在一个前缀（L-62）。
    /// </param>
    public static IReadOnlyDictionary<string, string> BuildEnvironment(RuntimeProfile profile, string? gameKey = null)
    {
        Dictionary<string, string> env = new();
        if (!string.IsNullOrWhiteSpace(profile.RunnerPath))
        {
            env["LUNARWARD_WINE"] = profile.RunnerPath;
        }
        string? prefix = null;
        if (!string.IsNullOrWhiteSpace(gameKey))
        {
            prefix = PrefixManager.TryGetExistingPrefix(profile, gameKey);
        }
        prefix ??= PrefixManager.ExpandPath(profile.PrefixPath);
        if (!string.IsNullOrWhiteSpace(prefix) && Directory.Exists(prefix))
        {
            env["WINEPREFIX"] = prefix;
        }
        env["WINEDEBUG"] = "-all";
        env["LUNARWARD_PROFILE"] = profile.Name;
        env["LUNARWARD_PROFILE_ID"] = profile.Id;
        foreach ((string key, string value) in profile.Env)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                env[key] = value;
            }
        }
        return env;
    }


    public static bool TryOpenShell(RuntimeProfile profile, ILogger? logger = null)
    {
        try
        {
            ProcessStartInfo? info = CreateShellStartInfo(profile);
            if (info is null)
            {
                logger?.LogWarning("No terminal emulator found for debug shell.");
                return false;
            }
            Process.Start(info);
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Open debug shell failed.");
            return false;
        }
    }


    private static string? FindExecutable(string name)
    {
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return null;
        }
        foreach (string dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (Exception)
            {
                // ignore malformed PATH entries
            }
        }
        return null;
    }

}
