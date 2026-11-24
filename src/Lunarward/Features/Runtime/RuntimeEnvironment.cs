// Lunarward 运行环境的「环境变量装配」唯一入口（R19）。
//
// 以前启动器、调试 Shell、RunOnce、伴随启动各拼一套：只有启动器会算 Proton 变量，
// 增强开关（DXVK_HUD / MANGOHUD）则谁都算不全，于是「Shell 里跑得通、启动器里不生效」。
// 现在所有入口都从 Assemble() 取同一份结果；启动器再交给 LaunchRecipeBuilder.Build 叠配方覆盖。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Starward.Features.Runtime;


public static class RuntimeEnvironment
{

    /// <summary>
    /// R108：把库目录**前置**到 <c>LD_LIBRARY_PATH</c>，并保留调用方原有的值。
    /// 以前这里整段替换（`= 新目录`），启动链另一处是前置——同一份配方的库在两条路径上顺序不一样。
    /// </summary>
    public static void PrependLibraryPath(IDictionary<string, string> env, IEnumerable<string> directories)
    {
        string joined = string.Join(":", directories.Where(x => !string.IsNullOrEmpty(x)));
        if (joined.Length == 0 || env is null)
        {
            return;
        }
        string? existing = env.TryGetValue("LD_LIBRARY_PATH", out string? old) ? old : null;
        env["LD_LIBRARY_PATH"] = string.IsNullOrEmpty(existing) ? joined : joined + ":" + existing;
    }


    /// <summary>
    /// 装配进程环境变量的「自动注入层」：运行器/前缀/语言/增强开关/Proton 运行时变量 + 配方 env。
    /// </summary>
    /// <param name="profile">运行环境配方，可为 null（系统 wine 兜底）。</param>
    /// <param name="gameKey">游戏 key；给了就按该游戏实际会用的前缀解析。</param>
    /// <param name="prefixOverride">启动器已经算好的前缀（含 EnsurePrefix 的结果），优先使用。</param>
    public static Dictionary<string, string> Assemble(RuntimeProfile? profile, string? gameKey = null, string? prefixOverride = null)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (profile is not null && !string.IsNullOrWhiteSpace(profile.RunnerPath))
        {
            env["LUNARWARD_WINE"] = profile.RunnerPath;
        }

        string? prefix = prefixOverride;
        if (string.IsNullOrWhiteSpace(prefix) && profile is not null)
        {
            if (!string.IsNullOrWhiteSpace(gameKey))
            {
                prefix = PrefixManager.TryGetExistingPrefix(profile);
            }
            prefix ??= PrefixManager.ExpandPath(profile.PrefixPath);
        }
        if (!string.IsNullOrWhiteSpace(prefix) && Directory.Exists(prefix))
        {
            env["WINEPREFIX"] = prefix;
        }

        // R108：只在用户/配方没指定时给默认值——以前无条件写，连 `WINEDEBUG=+loaddll` 排错也会被悄悄冲掉。
        env.TryAdd("WINEDEBUG", "-all");
        if (profile is not null)
        {
            env["LUNARWARD_PROFILE"] = profile.Name;
            env["LUNARWARD_PROFILE_ID"] = profile.Id;
            foreach ((string key, string value) in profile.Env)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    env[key] = value;
                }
            }
            // 增强开关与配方 env 同层：配方里手写的同名变量还能再覆盖（Build 会标红）。
            foreach ((string key, string value) in Enhancements.EnvironmentVariables(profile))
            {
                env[key] = value;
            }
            foreach ((string key, string value) in ProtonEnvironment(profile.RunnerPath))
            {
                env[key] = value;
            }
        }
        return env;
    }


    /// <summary>
    /// Proton 的运行器是 <c>.../files/bin/wine</c>，需要补 WINEDLLPATH / LD_LIBRARY_PATH / PROTONPATH，
    /// 否则 wine 找不到 Proton 自带的 builtin dll 与库。不是 Proton 布局时返回空表。
    /// </summary>
    public static Dictionary<string, string> ProtonEnvironment(string? winePath)
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(winePath))
        {
            return env;
        }
        string full;
        try
        {
            full = Path.GetFullPath(winePath);
        }
        catch
        {
            return env;
        }
        int idx = full.IndexOf("/steamapps/common/", StringComparison.Ordinal);
        if (idx < 0)
        {
            return env;
        }
        // wine = <steam>/steamapps/common/<tool>/files/bin/wine
        int filesIndex = full.IndexOf("/files/", StringComparison.Ordinal);
        if (filesIndex < 0)
        {
            return env;
        }
        string steamRoot = full[..idx];
        string toolRoot = full[..filesIndex];
        string files = Path.Combine(toolRoot, "files");

        string[] dllPathCandidates = { Path.Combine(files, "lib", "wine"), Path.Combine(files, "lib64", "wine") };
        string? dllPath = Array.Find(dllPathCandidates, Directory.Exists);
        if (dllPath is not null)
        {
            env["WINEDLLPATH"] = dllPath;
        }

        string[] libCandidates =
        {
            Path.Combine(files, "lib", "x86_64-linux-gnu"),
            Path.Combine(files, "lib", "i386-linux-gnu"),
            Path.Combine(files, "lib64"),
            Path.Combine(files, "lib"),
        };
        PrependLibraryPath(env, Array.FindAll(libCandidates, Directory.Exists));

        env["STEAM_COMPAT_CLIENT_INSTALL_PATH"] = steamRoot;
        env["STEAM_COMPAT_INSTALL_PATH"] = toolRoot;
        env["PROTONPATH"] = toolRoot;
        return env;
    }

}
