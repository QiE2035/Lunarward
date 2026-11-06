using Starward.Features.Database;
using Starward.Features.ViewHost;
using Starward.Helpers;
using Starward.Setup.Core;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Starward;

public static partial class AppConfig
{

    public static string? StarwardPortableLauncherExecutePath { get; private set; }

    public static string AppVersion { get; private set; }

    public static InstallType InstallType { get; set; }

    public static bool IsPortable => InstallType is InstallType.Portable;

    public static bool IsAppInRemovableStorage { get; private set; }

    public static string CacheFolder { get; private set; }

    public static string ConfigPath { get; private set; }

    public static string? Language { get; set; }

    public static string? UserDataFolder { get; set; }

    public static bool IsAdmin { get; private set; }

    public static string LogFile { get; private set; }

    /// <summary>XDG 数据根目录（~/.local/share）。</summary>
    internal static string LinuxDataHome
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            return string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : xdg;
        }
    }


    public static bool? EnableLoginAuthTicket { get; set; }

    public static string? stoken { get; set; }

    public static string? mid { get; set; }




    public static async Task CheckEnviromentAsync()
    {
        try
        {
            // Lunarward：版本号缺失会让 User-Agent 变成 "Starward.Debug/"（非法 header，直接炸在 HttpClient 构造），
            // 这里兜底一个合法值（L-26）。
            AppVersion = typeof(AppConfig).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (string.IsNullOrWhiteSpace(AppVersion))
            {
                AppVersion = typeof(AppConfig).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            }
            // Lunarward：Linux 下暂不识别「可移动存储」，统一按固定盘处理（M2 用 /proc/mounts 细化）。
            IsAppInRemovableStorage = false;

            string? parentFolder = new DirectoryInfo(AppContext.BaseDirectory).Parent?.FullName;
            string portableExe = Path.Join(parentFolder, "Starward.exe");
            string portableVersion = Path.Join(parentFolder, "version.ini");

            if (Directory.Exists(parentFolder) && (File.Exists(portableExe) || File.Exists(portableVersion)))
            {
                InstallType = InstallType.Portable;
                StarwardPortableLauncherExecutePath = portableExe;
                if (!HaveWritePermission(parentFolder))
                {
                    await new NoPermissionWindow(parentFolder).WaitAsync();
                    Environment.Exit(0);
                }
            }

            if (IsAppInRemovableStorage && IsPortable)
            {
                CacheFolder = Path.Combine(parentFolder!, ".cache");
                ConfigPath = Path.Combine(parentFolder!, "config.ini");
            }
            else if (IsAppInRemovableStorage)
            {
                CacheFolder = Path.Combine(Path.GetPathRoot(AppContext.BaseDirectory)!, ".StarwardCache");
                ConfigPath = Path.Combine(CacheFolder, "config.ini");
            }
            else if (IsPortable)
            {
                CacheFolder = Path.Combine(LinuxDataHome, "lunarward");
                ConfigPath = Path.Combine(parentFolder!, "config.ini");
            }
            else
            {
                CacheFolder = Path.Combine(LinuxDataHome, "lunarward");
            }

            string? userDataFolder = null;
            if (string.IsNullOrWhiteSpace(ConfigPath))
            {
                userDataFolder = LinuxConfig.Get("UserDataFolder")?.Trim();
            }
            else if (File.Exists(ConfigPath))
            {
                string text = File.ReadAllText(ConfigPath);
                userDataFolder = Regex.Match(text, @"UserDataFolder=(.+)").Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(userDataFolder) && !Path.IsPathFullyQualified(userDataFolder))
                {
                    userDataFolder = Path.GetFullPath(userDataFolder, Path.GetDirectoryName(ConfigPath)!);
                }
            }

            if (Directory.Exists(userDataFolder))
            {
                if (HaveWritePermission(userDataFolder))
                {
                    UserDataFolder = userDataFolder;
                    DatabaseService.SetDatabase(userDataFolder);
                    LoadConfiguration();
                }
                else
                {
                    await new NoPermissionWindow(userDataFolder).WaitAsync();
                    Environment.Exit(0);
                }
            }
            else
            {
                // Lunarward：Linux 首次运行不弹欢迎向导，直接使用默认数据目录（D22）
                UserDataFolder = Path.Combine(LinuxDataHome, "lunarward");
                Directory.CreateDirectory(UserDataFolder);
                DatabaseService.SetDatabase(UserDataFolder);
                LoadConfiguration();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{Lang.AppConfig_AnUnknownIssueOccurredDuringInitialization}\n{ex.Message}");
            Environment.Exit(0);
        }
    }


    private static bool HaveWritePermission(string folder)
    {
        try
        {
            string random = Path.Combine(folder, Guid.CreateVersion7().ToString());
            File.WriteAllBytes(random, "Write permission test."u8);
            File.Delete(random);
            return true;
        }
        catch
        {
            return false;
        }
    }



    public static void SetLanguage(string? lang)
    {
        try
        {
            CultureInfo culture;
            if (!string.IsNullOrWhiteSpace(lang))
            {
                culture = new CultureInfo(lang);
                Language = lang;
            }
            else
            {
                culture = CultureInfo.InstalledUICulture;
                Language = null;
            }
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            // Uno 的界面线程在 SetLanguage 之前就已创建，只改 CurrentUICulture 不会影响它
            // （实测：config.json 里写 de-DE 仍是中文，改用 LANG=de_DE 才变德文）。
            // Lang 的属性显式传入 resourceCulture，给它一个固定 culture 就能跨线程生效。
            Starward.Language.Lang.Culture = culture;
            Starward.Core.Localization.CoreLang.Culture = culture;
            Serilog.Log.Information("UI language: {culture} (setting: {setting})", culture.Name, Language ?? "(system)");
        }
        catch { }
    }



    public static void LoadConfiguration()
    {
        try
        {
            Directory.CreateDirectory(CacheFolder);
            FileCache.Initialize(Path.Combine(CacheFolder, "cache"));
            var webviewFolder = Path.Combine(CacheFolder, "webview");
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", webviewFolder, EnvironmentVariableTarget.Process);

            IsAdmin = Environment.IsPrivilegedProcess;

            if (string.IsNullOrWhiteSpace(ConfigPath))
            {
                LoadConfigurationFromStore();
            }
            else
            {
                LoadConfigurationFromConfigFile(ConfigPath);
            }
        }
        catch { }
    }


    public static void LoadConfigurationFromConfigFile(string path)
    {
        if (File.Exists(path))
        {
            string text = File.ReadAllText(path);
            string lang = Regex.Match(text, @"Language=(.+)").Groups[1].Value.Trim();
            bool.TryParse(Regex.Match(text, @"EnableLoginAuthTicket=(.+)").Groups[1].Value.Trim(), out bool enabled);
            EnableLoginAuthTicket = enabled;
            stoken = Regex.Match(text, @"stoken=(.+)").Groups[1].Value.Trim();
            mid = Regex.Match(text, @"mid=(.+)").Groups[1].Value.Trim();
            SetLanguage(lang);
        }
    }


    public static void LoadConfigurationFromStore()
    {
        string? lang = LinuxConfig.Get("Language")?.Trim();
        EnableLoginAuthTicket = LinuxConfig.Get("EnableLoginAuthTicket") is "1" or "True" or "true";
        stoken = LinuxConfig.Get("stoken")?.Trim();
        mid = LinuxConfig.Get("mid")?.Trim();
        SetLanguage(lang);
    }


    public static void SaveConfiguration()
    {
        if (string.IsNullOrWhiteSpace(ConfigPath))
        {
            SaveConfigurationToStore();
        }
        else
        {
            SaveConfigurationToConfigFile();
        }
    }


    public static void SaveConfigurationToStore()
    {
        try
        {
            LinuxConfig.Set("Language", Language);
            if (!string.IsNullOrWhiteSpace(UserDataFolder))
            {
                LinuxConfig.Set("UserDataFolder", UserDataFolder);
            }
            if (EnableLoginAuthTicket.HasValue)
            {
                LinuxConfig.Set("EnableLoginAuthTicket", EnableLoginAuthTicket.Value ? "1" : "0");
            }
            LinuxConfig.Set("stoken", stoken);
            LinuxConfig.Set("mid", mid);
            LinuxConfig.Save();
        }
        catch { }
    }


    public static void SaveConfigurationToConfigFile()
    {
        try
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(UserDataFolder))
            {
                string dataFolder = UserDataFolder;
                string? parentFolder = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrWhiteSpace(parentFolder) && UserDataFolder.StartsWith(parentFolder))
                {
                    dataFolder = Path.GetRelativePath(parentFolder, UserDataFolder);
                }
                sb.AppendLine($"Language={Language}");
                sb.AppendLine($"UserDataFolder={dataFolder}");
            }
            else
            {
                sb.AppendLine($"Language={Language}");
                sb.AppendLine($"UserDataFolder=");
            }
            if (EnableLoginAuthTicket.HasValue)
            {
                sb.AppendLine($"{nameof(EnableLoginAuthTicket)}={EnableLoginAuthTicket}");
            }
            if (!string.IsNullOrWhiteSpace(stoken))
            {
                sb.AppendLine($"{nameof(stoken)}={stoken}");
            }
            if (!string.IsNullOrWhiteSpace(mid))
            {
                sb.AppendLine($"{nameof(mid)}={mid}");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, sb.ToString());
        }
        catch { }
    }









}
