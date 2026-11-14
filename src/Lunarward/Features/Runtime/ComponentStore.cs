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

    /// <summary>
    /// 运行器布局：<c>bin</c>（…/bin/wine）或 <c>files-bin</c>（…/files/bin/wine，Proton 形态）。
    /// 非运行器为空。布局决定启动时要不要补库路径（D58）。
    /// </summary>
    public string Layout { get; init; } = "";

    /// <summary>是否可用；null = 未判定。判定只看「有无版本、有无配套 wineserver」，不建前缀、无副作用。</summary>
    public bool? Usable { get; set; }

    /// <summary>不可用的原因（可用时为空），直接显示给用户。</summary>
    public string UsabilityReason { get; set; } = "";

    /// <summary>图形组件的内容指纹（x64/d3d11.dll 或 d3d12.dll 的 SHA-256 前 12 位）。用于同内容分组（D63 §5）。</summary>
    public string ContentHash { get; set; } = "";

    /// <summary>
    /// 来源标记（D63 §2 五类来源）：`managed` 启动器自下载/导入、`system` 系统包管理器、
    /// `launcher` 其它启动器、`steam` Steam、`manual` 用户手动添加的路径。
    /// 只影响显示与排序，不影响能不能用。
    /// </summary>
    public string Source { get; set; } = "";

    public string SourceText => Source switch
    {
        "managed" => "启动器",
        "system" => "系统包",
        "launcher" => "其它启动器",
        "steam" => "Steam",
        "manual" => "手动",
        _ => "",
    };

    public string LayoutText => Layout switch
    {
        "files-bin" => "files/bin",
        "bin" => "bin",
        _ => "",
    };

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


internal static class ComponentSource
{
    /// <summary>
    /// 按路径猜来源（D63 §2）：只用于显示与排序，不参与「能不能用」的判断。
    /// 顺序：本程序目录 → Steam → 其它启动器 → 系统（/usr、/opt、PATH 等）。
    /// </summary>
    public static string Guess(string path, bool managed)
    {
        if (managed || path.StartsWith(ComponentStore.ComponentsFolder, StringComparison.Ordinal))
        {
            return "managed";
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.Contains("/steamapps/", StringComparison.Ordinal)
            || path.Contains("compatibilitytools.d", StringComparison.Ordinal)
            || path.Contains("/.steam/", StringComparison.Ordinal))
        {
            return "steam";
        }
        foreach (string launcher in OtherLauncherFolders)
        {
            string prefix = Path.Combine(home, ".local", "share", launcher);
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return "launcher";
            }
        }
        return "system";
    }

    /// <summary>常见第三方启动器在 ~/.local/share 下的目录名（只用于判来源）。</summary>
    private static readonly string[] OtherLauncherFolders =
    {
        "sleepy-launcher",
        "anime-game-launcher",
        "honkers-launcher",
        "honkai-star-rail-launcher",
        "lutris",
        "bottles",
        "heroic",
        "umu",
    };
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

        void AddRunner(string path, ComponentKind kind, string name, bool managed, string? source = null,
            string? version = null)
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
                Layout = DetectLayout(full),
                Version = version ?? "",
                Source = source ?? ComponentSource.Guess(full, managed),
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
                // 受管组件优先读 manifest（D73）：布局、名字、版本都来自导入/下载时写下的记录
                ComponentManifest? manifest = ComponentManifestStore.TryRead(dir);
                string wine = ComponentLayouts.WinePath(dir, manifest?.Layout ?? "");
                if (string.IsNullOrEmpty(wine))
                {
                    wine = Path.Combine(dir, "bin", "wine");
                }
                AddRunner(wine, manifest?.Kind == "proton" ? ComponentKind.Proton : ComponentKind.Wine,
                    string.IsNullOrWhiteSpace(manifest?.Name) ? Path.GetFileName(dir) : manifest!.Name,
                    true, null, string.IsNullOrWhiteSpace(manifest?.Version) ? null : manifest!.Version);
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

        // 5) 用户手动添加的路径（D63）：只登记，不复制、不探测目录树
        foreach (string manual in ManualRunners.Load())
        {
            string name = Path.GetFileName(Path.GetDirectoryName(manual.TrimEnd('/'))) ?? manual;
            AddRunner(manual, ComponentKind.Wine, name, false, "manual");
        }

        // 版本号 + 可用性：`wine --version` 结果按真实路径缓存（首次约 0.2–1 s，之后走缓存）。
        // 可用性判定只做两件无副作用的事：读版本、找同套 wineserver——**不创建前缀**。
        foreach (Component runner in list)
        {
            if (string.IsNullOrWhiteSpace(runner.Version))
            {
                runner.Version = ReadRunnerVersion(runner.Path) ?? "";
            }
            (bool usable, string reason) = EvaluateRunner(runner);
            runner.Usable = usable;
            runner.UsabilityReason = reason;
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

        void AddGraphics(string dir, ComponentKind kind, ComponentManifest? manifest = null)
        {
            string full;
            try
            {
                // 必须解析符号链接：~/.local/share/Lunarward 是本程序数据目录的链接，
                // 不解析就会把同一个组件列两遍（2026-11-14 实测）。
                full = RealPath(dir);
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
                Name = string.IsNullOrWhiteSpace(manifest?.Name) ? Path.GetFileName(full.TrimEnd('/')) : manifest!.Name,
                Path = full,
                Managed = full.StartsWith(ComponentsFolder, StringComparison.Ordinal),
                Version = manifest?.Version ?? "",
                Source = ComponentSource.Guess(full, full.StartsWith(ComponentsFolder, StringComparison.Ordinal)),
            });
        }

        // 系统级 DXVK（Arch: /usr/share/dxvk、/usr/lib/dxvk、pacman 的 dxvk-mingw）
        foreach (string root in new[] { "/usr/share/dxvk", "/usr/lib/dxvk", "/usr/local/share/dxvk", "/opt/dxvk" })
        {
            AddGraphics(root, ComponentKind.Dxvk);
        }
        // 本程序管理目录
        foreach (string sub in new[] { "dxvk", "vkd3d-proton", "graphics" })
        {
            string root = Path.Combine(ComponentsFolder, sub);
            if (!Directory.Exists(root))
            {
                continue;
            }
            foreach (string dir in SafeDirs(root))
            {
                ComponentKind kind = sub switch
                {
                    "dxvk" => ComponentKind.Dxvk,
                    "vkd3d-proton" => ComponentKind.Vkd3dProton,
                    _ => File.Exists(Path.Combine(dir, "x64", "d3d12.dll")) || File.Exists(Path.Combine(dir, "d3d12.dll"))
                        ? ComponentKind.Vkd3dProton
                        : ComponentKind.Dxvk,
                };
                AddGraphics(dir, kind, ComponentManifestStore.TryRead(dir));
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

    /// <summary>从可执行文件路径判断布局：…/files/bin/wine → files-bin，…/bin/wine → bin。</summary>
    private static string DetectLayout(string winePath)
    {
        string dir = Path.GetDirectoryName(winePath) ?? "";
        if (Path.GetFileName(dir) != "bin")
        {
            return "";
        }
        return Path.GetFileName(Path.GetDirectoryName(dir) ?? "") == "files" ? "files-bin" : "bin";
    }


    /// <summary>找与 <paramref name="winePath"/> 配套的 wineserver：同目录优先，其次 PATH。</summary>
    public static string? FindWineserver(string winePath)
    {
        string? dir = Path.GetDirectoryName(winePath);
        if (dir is not null)
        {
            foreach (string name in new[] { "wineserver", "wineserver64" })
            {
                string candidate = Path.Combine(dir, name);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        return FindInPath("wineserver");
    }


    /// <summary>
    /// 判断一个运行器能不能用。只做**无副作用**的检查（不创建、不修改任何前缀）：可执行、读得到版本、有配套 wineserver。
    /// 更严格的可用性要靠首次启动验证（D58：扫描阶段不下结论）。
    /// </summary>
    public static (bool Usable, string Reason) EvaluateRunner(Component runner)
    {
        if (!File.Exists(runner.Path))
        {
            return (false, "找不到可执行文件");
        }
        try
        {
            const UnixFileMode anyExec = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
            if ((File.GetUnixFileMode(runner.Path) & anyExec) == 0)
            {
                return (false, "没有可执行权限");
            }
        }
        catch
        {
            // 权限位读不到就跳过这条，不影响后续判断
        }
        if (string.IsNullOrWhiteSpace(runner.Version))
        {
            return (false, "`wine --version` 没有输出（可能缺库或无法执行）");
        }
        if (FindWineserver(runner.Path) is null)
        {
            return (false, "找不到配套的 wineserver（游玩时长与前缀锁会不准）");
        }
        return (true, "");
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


    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> HashCache = new();


    /// <summary>图形组件的内容指纹：取 x64 里最关键的那个 dll 做 SHA-256，取不到返回空。</summary>
    private static string ComputeGraphicsHash(string directory)
    {
        string[] candidates =
        [
            Path.Combine(directory, "x64", "d3d11.dll"),
            Path.Combine(directory, "x64", "d3d12.dll"),
            Path.Combine(directory, "d3d11.dll"),
            Path.Combine(directory, "d3d12.dll"),
        ];
        foreach (string candidate in candidates)
        {
            try
            {
                if (!File.Exists(candidate))
                {
                    continue;
                }
                string key = candidate + "|" + new FileInfo(candidate).Length + "|" + File.GetLastWriteTimeUtc(candidate).Ticks;
                if (HashCache.TryGetValue(key, out string? cached))
                {
                    return cached;
                }
                using FileStream stream = File.OpenRead(candidate);
                byte[] hash = System.Security.Cryptography.SHA256.HashData(stream);
                string text = Convert.ToHexString(hash)[..12];
                HashCache[key] = text;
                return text;
            }
            catch
            {
                return "";
            }
        }
        return "";
    }


    /// <summary>给图形组件补内容指纹，并返回「同内容折叠」后的分组；取不到指纹时按 Id 分组。</summary>
    public static IReadOnlyList<List<Component>> GroupGraphics(IReadOnlyList<Component> graphics, bool computeHash = true)
    {
        bool skipHash = Environment.GetEnvironmentVariable("LUNARWARD_NO_COMPONENT_HASH") == "1";
        Dictionary<string, List<Component>> groups = new(StringComparer.Ordinal);
        List<string> order = new();
        foreach (Component graphic in graphics)
        {
            if (computeHash && !skipHash && string.IsNullOrEmpty(graphic.ContentHash))
            {
                graphic.ContentHash = ComputeGraphicsHash(graphic.Path);
            }
            string key = string.IsNullOrEmpty(graphic.ContentHash) ? "id:" + graphic.Id : "sha:" + graphic.ContentHash;
            if (!groups.TryGetValue(key, out List<Component>? list))
            {
                list = new List<Component>();
                groups[key] = list;
                order.Add(key);
            }
            list.Add(graphic);
        }
        List<List<Component>> result = new();
        foreach (string key in order)
        {
            result.Add(groups[key]);
        }
        return result;
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

/// <summary>
/// 用户手动添加的运行器（`~/.config/lunarward/manual-runners.json`）。
/// 只存可执行文件路径；不复制、不修改，删掉清单里的条目就等于放弃引用。
/// </summary>
public static class ManualRunners
{

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.DoNotVerify),
        "lunarward", "manual-runners.json");


    public static IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }
            List<string>? list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(File.ReadAllText(FilePath));
            return list?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }


    public static void Add(string winePath)
    {
        List<string> list = Load().ToList();
        string full = Path.GetFullPath(winePath);
        if (!list.Any(x => string.Equals(Path.GetFullPath(x), full, StringComparison.Ordinal)))
        {
            list.Add(full);
            Helpers.JsonFile.WriteAtomic(FilePath, list);
        }
    }


    public static void Remove(string winePath)
    {
        List<string> list = Load().ToList();
        string full = Path.GetFullPath(winePath);
        if (list.RemoveAll(x => string.Equals(Path.GetFullPath(x), full, StringComparison.Ordinal)) > 0)
        {
            Helpers.JsonFile.WriteAtomic(FilePath, list);
        }
    }

}
