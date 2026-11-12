// Lunarward：一键导出「启动诊断包」。
//
// 目的：用户报「起不来」时，不再靠来回问十轮。
// 内容只读、只包含运行环境相关的信息；所有疑似令牌/密码的环境变量都会打码。
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Features.GameLauncher;
using Starward.Features.Runtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Starward.Helpers;


public static class DiagnosticBundle
{

    private static readonly string[] SensitiveKeywords =
    {
        "TOKEN", "SECRET", "PASSWORD", "PASSWD", "COOKIE", "AUTH", "TICKET", "KEY", "SESSION",
    };


    public static async Task<string> CreateAsync(string? outputDirectory = null, GameBiz? gameBiz = null, ILogger? logger = null)
    {
        string folder = outputDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(folder);
        string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        string zipPath = Path.Combine(folder, $"lunarward-diagnose-{stamp}.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        AddText(zip, "README.txt", BuildReadme(gameBiz));
        AddText(zip, "environment.txt", BuildEnvironment());
        AddText(zip, "runners.txt", BuildRunners());
        AddText(zip, "profiles.json", BuildProfiles());
        AddText(zip, "bindings.json", ProfileBinding.Describe());
        AddText(zip, "last-good.json", ReadOrEmpty(LastKnownGoodStore.FilePath));
        AddText(zip, "prefixes.txt", BuildPrefixes(logger));
        AddText(zip, "launch-reports.txt", BuildRecentLaunchReports());
        AddText(zip, "log-tail.txt", ReadLogTail(250));
        // 永远写一份 preflight.txt：没有指定游戏时写清楚「怎么指定」，而不是让压缩包里少一个文件
        AddText(zip, "preflight.txt", gameBiz is null
            ? "没有指定游戏。用 `lunarward --diagnose --game <gameKey>` 可附带该游戏的启动预检。\n"
            : BuildPreflight(gameBiz.Value, logger));
        await Task.CompletedTask;
        return zipPath;
    }


    private static string BuildReadme(GameBiz? biz)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Lunarward 启动诊断包");
        sb.AppendLine("生成时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        sb.AppendLine("游戏：" + (biz?.Value ?? "(未指定)"));
        sb.AppendLine("应用版本：" + (typeof(AppConfig).Assembly.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown"));
        sb.AppendLine();
        sb.AppendLine("包含：环境变量（已打码）、运行器清单、方案、绑定、上次可用、前缀信息、最近启动报告、日志尾部。");
        sb.AppendLine("不含：游戏本体、存档、登录凭证、截图。");
        sb.AppendLine();
        sb.AppendLine("排查顺序建议：preflight.txt → launch-reports.txt → log-tail.txt。");
        return sb.ToString();
    }


    private static string BuildEnvironment()
    {
        var sb = new StringBuilder();
        sb.AppendLine("OS: " + Environment.OSVersion);
        sb.AppendLine("Kernel: " + ReadFirstLine("/proc/sys/kernel/osrelease"));
        sb.AppendLine("Session: " + (Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? "?"));
        sb.AppendLine("Desktop: " + (Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "?"));
        sb.AppendLine("DISPLAY: " + (Environment.GetEnvironmentVariable("DISPLAY") ?? "(none)"));
        sb.AppendLine("WAYLAND_DISPLAY: " + (Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "(none)"));
        sb.AppendLine("XDG_DATA_HOME: " + (Environment.GetEnvironmentVariable("XDG_DATA_HOME") ?? "(default)"));
        sb.AppendLine("PrefixRoot: " + PrefixManager.PrefixRoot);
        sb.AppendLine();
        sb.AppendLine("--- /etc/os-release ---");
        sb.AppendLine(ReadOrEmpty("/etc/os-release"));
        sb.AppendLine("--- wine 相关环境变量（已打码）---");
        foreach (var pair in Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
                     .Where(x => x.Key is string k && (k.StartsWith("WINE", StringComparison.Ordinal) || k.StartsWith("DXVK", StringComparison.Ordinal) || k.StartsWith("VKD3D", StringComparison.Ordinal) || k.StartsWith("LUNARWARD", StringComparison.Ordinal)))
                     .OrderBy(x => (string)x.Key))
        {
            sb.AppendLine($"{pair.Key}={Mask(pair.Key as string, pair.Value as string)}");
        }
        return sb.ToString();
    }


    private static string BuildRunners()
    {
        try
        {
            var sb = new StringBuilder();
            foreach (Component runner in ComponentStore.GetRunners())
            {
                sb.AppendLine($"[{runner.KindText}] {runner.Name}  {runner.Version}");
                sb.AppendLine($"    path: {runner.Path}");
                sb.AppendLine($"    layout: {runner.LayoutText}  usable: {runner.Usable} {runner.UsabilityReason}");
            }
            sb.AppendLine();
            foreach (Component graphics in ComponentStore.GetGraphics())
            {
                sb.AppendLine($"[{graphics.KindText}] {graphics.Name}  {graphics.Version}");
                sb.AppendLine($"    path: {graphics.Path}");
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "扫描组件失败：" + ex;
        }
    }


    private static string BuildProfiles()
    {
        try
        {
            var profiles = RuntimeProfileStore.LoadAll();
            return JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return "读取方案失败：" + ex;
        }
    }


    private static string BuildPrefixes(ILogger? logger)
    {
        var sb = new StringBuilder();
        try
        {
            foreach (RuntimeProfile profile in RuntimeProfileStore.LoadAll())
            {
                sb.AppendLine($"=== {profile.Name} ({profile.Id}) mode={profile.PrefixMode} path={profile.PrefixPath}");
                string? prefix = TryResolveAnyPrefix(profile);
                if (prefix is null)
                {
                    sb.AppendLine("    (尚未创建)");
                    continue;
                }
                sb.AppendLine("    dir: " + prefix);
                sb.AppendLine("    prefix.json: " + ReadOrDefault(Path.Combine(prefix, ".lunarward", "prefix.json"), "(无)"));
                sb.AppendLine("    graphics.json: " + ReadOrDefault(Path.Combine(prefix, ".lunarward", "graphics.json"), "(无)"));
                sb.AppendLine("    d3d11.dll: " + File.Exists(Path.Combine(prefix, "drive_c", "windows", "system32", "d3d11.dll")));
                sb.AppendLine("    d3d12.dll: " + File.Exists(Path.Combine(prefix, "drive_c", "windows", "system32", "d3d12.dll")));
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine("读取前缀失败：" + ex);
        }
        return sb.ToString();
    }


    private static string? TryResolveAnyPrefix(RuntimeProfile profile)
    {
        // 设置类操作用「只解析不创建」的路径；游戏键未知时用方案里显式路径兜底。
        string? path = PrefixManager.ExpandPath(profile.PrefixPath);
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
        {
            return path;
        }
        foreach (string biz in new[] { "hk4e_cn", "hkrpg_cn", "nap_cn", "bh3_global" })
        {
            string? existing = PrefixManager.TryGetExistingPrefix(profile, biz);
            if (existing is not null)
            {
                return existing;
            }
        }
        return null;
    }


    private static string BuildRecentLaunchReports()
    {
        try
        {
            string dir = Path.Combine(AppConfig.CacheFolder ?? ".", "logs", "launch");
            if (!Directory.Exists(dir))
            {
                return "(没有启动报告)";
            }
            var sb = new StringBuilder();
            foreach (string file in Directory.GetFiles(dir, "*.json").OrderByDescending(File.GetLastWriteTimeUtc).Take(3))
            {
                sb.AppendLine($"--- {Path.GetFileName(file)} ---");
                sb.AppendLine(ReadOrEmpty(file));
                sb.AppendLine();
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "读取启动报告失败：" + ex;
        }
    }


    private static string ReadLogTail(int lines)
    {
        try
        {
            string dir = Path.Combine(AppConfig.CacheFolder ?? ".", "log");
            if (!Directory.Exists(dir))
            {
                return "(没有日志)";
            }
            string? file = Directory.GetFiles(dir, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (file is null)
            {
                return "(没有日志)";
            }
            string[] all = File.ReadAllLines(file);
            return string.Join(Environment.NewLine, all.Skip(Math.Max(0, all.Length - lines)));
        }
        catch (Exception ex)
        {
            return "读取日志失败：" + ex;
        }
    }


    private static string BuildPreflight(GameBiz biz, ILogger? logger)
    {
        try
        {
            string? folder = GameLauncherService.GetGameInstallPath(biz);
            if (folder is null)
            {
                return "游戏未安装或安装路径未设置。";
            }
            string exe = Path.Combine(folder, GerExeName(biz));
            RuntimeProfile? profile = ProfileBinding.Resolve(biz);
            PreflightResult result = LaunchPreflight.Check(biz, exe, profile, logger);
            var sb = new StringBuilder();
            sb.AppendLine("canLaunch: " + result.CanLaunch);
            foreach (PreflightItem item in result.Items)
            {
                sb.AppendLine($"[{item.Status}] {item.Name}: {item.Detail}" + (string.IsNullOrEmpty(item.Fix) ? "" : $"  (修法: {item.Fix})"));
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            return "预检失败：" + ex;
        }
    }


    /// <summary>按 GameBiz 猜可执行文件名（与启动流水线同一张表，取不到就退化成空串）。</summary>
    private static string GerExeName(GameBiz biz) => biz.Value switch
    {
        "hk4e_cn" or "hk4e_global" => "YuanShen.exe",
        "hkrpg_cn" or "hkrpg_global" => "StarRail.exe",
        "nap_cn" or "nap_global" => "ZenlessZoneZero.exe",
        "bh3_global" => "BH3.exe",
        _ => "",
    };


    private static string Mask(string? key, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }
        if (key is not null && SensitiveKeywords.Any(k => key.Contains(k, StringComparison.OrdinalIgnoreCase)))
        {
            return "***";
        }
        return value;
    }


    private static string ReadFirstLine(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadLines(path).FirstOrDefault() ?? "" : "";
        }
        catch
        {
            return "";
        }
    }


    private static string ReadOrDefault(string path, string fallback)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path).Trim() : fallback;
        }
        catch
        {
            return fallback;
        }
    }


    private static string ReadOrEmpty(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }
        catch
        {
            return "";
        }
    }


    private static void AddText(ZipArchive zip, string name, string content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }

}
