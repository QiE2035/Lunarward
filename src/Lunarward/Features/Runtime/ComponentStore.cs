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
