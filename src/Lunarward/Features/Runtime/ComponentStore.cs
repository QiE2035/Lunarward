// Lunarward：组件（运行器 / DXVK / VKD3D）探测。只读扫描，不下载、不修改前缀。
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Starward.Features.Runtime;

public enum ComponentKind
{
    Wine,
    Proton,
    Dxvk,
    Vkd3dProton,
}

/// <summary>一个可被 Profile 引用的组件。</summary>
public sealed class Component
{
    public ComponentKind Kind { get; init; }

    /// <summary>稳定标识，写进 Profile。</summary>
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    /// <summary>版本号，取不到时为空。</summary>
    public string Version { get; set; } = "";

    /// <summary>wine 可执行文件，或 DXVK 的 dll 目录。</summary>
    public string Path { get; init; } = "";

    /// <summary>true = 由本程序管理（在 ~/.local/share/lunarward 下）；false = 外部目录。</summary>
    public bool Managed { get; init; }

    public string KindText => Kind switch
    {
        ComponentKind.Wine => "Wine",
        ComponentKind.Proton => "Proton",
        ComponentKind.Dxvk => "DXVK",
        ComponentKind.Vkd3dProton => "VKD3D-Proton",
        _ => Kind.ToString(),
    };

    public string DisplayName => string.IsNullOrWhiteSpace(Version) ? Name : $"{Name}  ({Version})";

    public override string ToString() => DisplayName;
}


public static class ComponentStore
{
    /// <summary>本程序自己的组件目录：~/.local/share/lunarward/components</summary>
    public static string ComponentsFolder
    {
        get
        {
            string dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            return Path.Combine(dataHome, "lunarward", "components");
        }
    }

    private static List<Component>? _runners;

    private static List<Component>? _graphics;

    /// <summary>扫描可用的 wine / proton 运行器（结果有缓存，刷新时传 force）。</summary>
    public static IReadOnlyList<Component> GetRunners(bool force = false, ILogger? logger = null)
    {
        if (_runners is not null && !force)
        {
            return _runners;
        }
        List<Component> list = new();
        HashSet<string> seen = new(StringComparer.Ordinal);

        void AddRunner(string path, ComponentKind kind, string name, bool managed)
        {
            string full;
            try
            {
                full = RealPath(path);
            }
            catch
            {
                return;
            }
            if (!File.Exists(full) || !seen.Add(full))
            {
                return;
            }
            list.Add(new Component
            {
                Kind = kind,
                Id = full,
                Name = name,
                Path = full,
                Managed = managed,
            });
        }

        // 1) 系统 PATH 里的 wine
        foreach (string exe in new[] { "wine", "wine64" })
        {
            string? found = FindInPath(exe);
            if (found is not null)
            {
                AddRunner(found, ComponentKind.Wine, exe == "wine" ? "系统 Wine" : "系统 Wine (64)", false);
            }
        }

        // 2) Steam 的 Proton
        foreach (string root in SteamCompatRoots())
        {
            if (!Directory.Exists(root))
            {
                continue;
            }
            foreach (string dir in SafeDirs(root))
            {
                string wine = Path.Combine(dir, "files", "bin", "wine");
                AddRunner(wine, ComponentKind.Proton, Path.GetFileName(dir), false);
            }
        }

        // 3) 本程序管理目录下的运行器
        string managed = Path.Combine(ComponentsFolder, "wine");
        if (Directory.Exists(managed))
        {
            foreach (string dir in SafeDirs(managed))
            {
                string wine = Path.Combine(dir, "bin", "wine");
                AddRunner(wine, ComponentKind.Wine, Path.GetFileName(dir), true);
            }
        }

        // 4) 特征探测：扫描根下任何「像运行器」的目录（D56），覆盖第三方启动器
        foreach (string root in ScanRoots)
        {
            foreach ((string wine, string name, bool proton) in DiscoverRunners(root))
            {
                AddRunner(wine, proton ? ComponentKind.Proton : ComponentKind.Wine, name, wine.StartsWith(ComponentsFolder, StringComparison.Ordinal));
            }
        }

        // 版本号：`wine --version` 结果按真实路径缓存（首次约 0.2–1 s，之后走缓存）
        foreach (Component runner in list)
        {
            if (string.IsNullOrWhiteSpace(runner.Version))
            {
                runner.Version = ReadRunnerVersion(runner.Path) ?? "";
            }
        }
        _runners = list;
        logger?.LogInformation("Found {count} runner(s)", list.Count);
        return list;
    }

    /// <summary>扫描可用的 DXVK / VKD3D-Proton（目录里要有 d3d11.dll 或 d3d12.dll）。</summary>
    public static IReadOnlyList<Component> GetGraphics(bool force = false, ILogger? logger = null)
    {
        if (_graphics is not null && !force)
        {
            return _graphics;
        }
        List<Component> list = new();
        HashSet<string> seen = new(StringComparer.Ordinal);

        void AddGraphics(string dir, ComponentKind kind)
        {
            string full;
            try
            {
                full = Path.GetFullPath(dir);
            }
            catch
            {
                return;
            }
            string marker = kind is ComponentKind.Dxvk
                ? Path.Combine(full, "x64", "d3d11.dll")
                : Path.Combine(full, "x64", "d3d12.dll");
            if (!File.Exists(marker))
            {
                // 兼容"平铺"的目录结构（直接在根上放 d3d11.dll）
                marker = kind is ComponentKind.Dxvk
                    ? Path.Combine(full, "d3d11.dll")
                    : Path.Combine(full, "d3d12.dll");
            }
            if (!File.Exists(marker) || !seen.Add(full))
            {
                return;
            }
            list.Add(new Component
            {
                Kind = kind,
                Id = full,
                Name = Path.GetFileName(full.TrimEnd('/')),
                Path = full,
                Managed = full.StartsWith(ComponentsFolder, StringComparison.Ordinal),
            });
        }

        // 系统级 DXVK（Arch: /usr/share/dxvk、/usr/lib/dxvk、pacman 的 dxvk-mingw）
        foreach (string root in new[] { "/usr/share/dxvk", "/usr/lib/dxvk", "/usr/local/share/dxvk", "/opt/dxvk" })
        {
            AddGraphics(root, ComponentKind.Dxvk);
        }
        // 本程序管理目录
        foreach (string sub in new[] { "dxvk", "vkd3d-proton" })
        {
            string root = Path.Combine(ComponentsFolder, sub);
            if (!Directory.Exists(root))
            {
                continue;
            }
            foreach (string dir in SafeDirs(root))
            {
                AddGraphics(dir, sub == "dxvk" ? ComponentKind.Dxvk : ComponentKind.Vkd3dProton);
            }
        }
        // 特征探测：扫描根下任何含 x64/d3d11.dll（或 d3d12.dll）的目录（D56）
        foreach (string root in ScanRoots)
        {
            foreach (string dir in DiscoverGraphicsDirs(root))
            {
                ComponentKind kind = File.Exists(Path.Combine(dir, "x64", "d3d12.dll"))
                    ? ComponentKind.Vkd3dProton
                    : ComponentKind.Dxvk;
                AddGraphics(dir, kind);
            }
        }
        // Steam 兼容工具目录里可能自带 dxvk（Proton 自带，这里只做提示，不重复安装）
        _graphics = list;
        logger?.LogInformation("Found {count} graphics component(s)", list.Count);
        return list;
    }

    /// <summary>读 wine 版本（`wine --version`，失败返回空）。</summary>
    public static string ReadRunnerVersion(string winePath)
    {
        try
        {
            ProcessStartInfo info = new(winePath)
            {
                ArgumentList = { "--version" },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            info.Environment["WINEDEBUG"] = "-all";
            using Process? process = Process.Start(info);
            if (process is null)
            {
                return "";
            }
            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(4000);
            return output;
        }
        catch
        {
            return "";
        }
    }

    /// <summary>解析符号链接后的真实路径：Steam 的 ~/.steam/steam 与 ~/.local/share/Steam 指向同一份，必须去重。</summary>
    /// <summary>
    /// 规范化到「真实路径」。
    /// L-38 的教训：只对文件本身 ResolveLinkTarget 不够——`~/.steam/steam` 是**目录**符号链接，
    /// 于是同一份 Proton 会以两个路径出现两次。这里把目录链逐段解析掉。
    /// </summary>
    private static string RealPath(string path)
    {
        try
        {
            string full = Path.GetFullPath(path);
            FileSystemInfo? fileTarget = File.ResolveLinkTarget(full, returnFinalTarget: true);
            if (fileTarget is not null)
            {
                return fileTarget.FullName;
            }
            string? dir = Path.GetDirectoryName(full);
            if (dir is null)
            {
                return full;
            }
            var segments = new Stack<string>();
            DirectoryInfo? current = new(dir);
            while (current is not null)
            {
                if (current.LinkTarget is not null)
                {
                    FileSystemInfo? resolved = current.ResolveLinkTarget(returnFinalTarget: true);
                    if (resolved is DirectoryInfo resolvedDir)
                    {
                        current = resolvedDir;
                        continue;
                    }
                }
                segments.Push(current.Name);
                current = current.Parent;
            }
            string result = "/";
            while (segments.Count > 0)
            {
                result = Path.Combine(result, segments.Pop());
            }
            return Path.Combine(result, Path.GetFileName(full));
        }
        catch
        {
            return Path.GetFullPath(path);
        }
    }


    public static void Invalidate()
    {
        _runners = null;
        _graphics = null;
    }


    private static IEnumerable<string> SteamCompatRoots()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        yield return Path.Combine(home, ".local/share/Steam/steamapps/common");
        yield return Path.Combine(home, ".steam/steam/steamapps/common");
        yield return Path.Combine(home, ".local/share/Steam/steamapps/compatibilitytools.d");
        yield return Path.Combine(home, ".steam/steam/steamapps/compatibilitytools.d");
    }

    /// <summary>默认扫描根（D56）：覆盖系统目录、Steam、Flatpak 与常见第三方启动器。</summary>
    public static string[] DefaultScanRoots()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[]
        {
            "/opt",
            "/usr/share/steam",
            "/usr/lib",
            "/usr/local/share",
            Path.Combine(home, ".local/share"),
            Path.Combine(home, ".local/share/Steam"),
            Path.Combine(home, ".steam/steam"),
            Path.Combine(home, ".var/app/com.valvesoftware.Steam"),
        };
    }


    /// <summary>
    /// 用户配置的扫描根（设置里可改，`:` 或换行分隔；空则用默认）。
    /// </summary>
    public static IReadOnlyList<string> ScanRoots
    {
        get
        {
            try
            {
                string? raw = AppConfig.GetValue<string>(null, "ComponentScanRoots");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    string[] custom = raw.Split(new[] { ':', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(x => x.Trim())
                                         .Select(ExpandHome)
                                         .Where(Directory.Exists)
                                         .ToArray();
                    if (custom.Length > 0)
                    {
                        return custom;
                    }
                }
            }
            catch
            {
            }
            return DefaultScanRoots();
        }
    }


    public static void SetScanRoots(IEnumerable<string> roots)
    {
        AppConfig.SetValue(string.Join(Environment.NewLine, roots.Where(x => !string.IsNullOrWhiteSpace(x))), "ComponentScanRoots");
        Invalidate();
    }


    private static string ExpandHome(string path)
    {
        if (path == "~" || path.StartsWith("~/", StringComparison.Ordinal))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + path[1..];
        }
        return path;
    }


    /// <summary>
    /// 按「特征」在根下找运行器（D56）：不再按厂商目录硬编码，任何目录只要有
    /// bin/wine、bin/wine64、files/bin/wine 或 proton 脚本就算一个运行器。
    /// </summary>
    /// <returns>(可执行文件路径, 名字, 是否 Proton 布局)</returns>
    public static IEnumerable<(string Wine, string Name, bool Proton)> DiscoverRunners(string root, int maxDepth = 4)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }
        HashSet<string> skip = new(StringComparer.OrdinalIgnoreCase)
        {
            "drive_c", "dosdevices", "node_modules", ".git", "shadercache", "steamapps", "dist", "__pycache__",
        };
        var pending = new Stack<(string Dir, int Depth)>();
        pending.Push((root, 0));
        int visited = 0;
        while (pending.Count > 0)
        {
            (string dir, int depth) = pending.Pop();
            if (++visited > 20000)
            {
                yield break;
            }
            // 本层特征
            string wine = Path.Combine(dir, "bin", "wine");
            if (!File.Exists(wine))
            {
                wine = Path.Combine(dir, "bin", "wine64");
            }
            if (!File.Exists(wine))
            {
                wine = Path.Combine(dir, "files", "bin", "wine");
            }
            if (File.Exists(wine))
            {
                bool proton = wine.Contains("/files/bin/wine", StringComparison.Ordinal);
                yield return (wine, Path.GetFileName(dir.TrimEnd('/')), proton);
                continue;
            }
            string protonScript = Path.Combine(dir, "proton");
            if (File.Exists(protonScript))
            {
                string filesWine = Path.Combine(dir, "files", "bin", "wine");
                yield return (File.Exists(filesWine) ? filesWine : protonScript, Path.GetFileName(dir.TrimEnd('/')), true);
                continue;
            }
            if (depth >= maxDepth)
            {
                continue;
            }
            foreach (string child in SafeDirs(dir))
            {
                string name = Path.GetFileName(child);
                if (skip.Contains(name) || name.StartsWith('.'))
                {
                    continue;
                }
                pending.Push((child, depth + 1));
            }
        }
    }


    /// <summary>在扫描根下找 DXVK/VKD3D 目录（含 x64/d3d11.dll 或 x64/d3d12.dll）。</summary>
    public static IEnumerable<string> DiscoverGraphicsDirs(string root, int maxDepth = 4)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }
        var pending = new Stack<(string Dir, int Depth)>();
        pending.Push((root, 0));
        int visited = 0;
        while (pending.Count > 0)
        {
            (string dir, int depth) = pending.Pop();
            if (++visited > 20000)
            {
                yield break;
            }
            if (File.Exists(Path.Combine(dir, "x64", "d3d11.dll")) || File.Exists(Path.Combine(dir, "x64", "d3d12.dll")))
            {
                yield return dir;
                continue;
            }
            if (depth >= maxDepth)
            {
                continue;
            }
            foreach (string child in SafeDirs(dir))
            {
                string name = Path.GetFileName(child);
                if (name.StartsWith('.') || name is "drive_c" or "dosdevices" or "node_modules" or ".git")
                {
                    continue;
                }
                pending.Push((child, depth + 1));
            }
        }
    }


    private static IEnumerable<string> SafeDirs(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string? FindInPath(string exe)
    {
        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }
        foreach (string dir in path.Split(':', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = Path.Combine(dir, exe);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}
