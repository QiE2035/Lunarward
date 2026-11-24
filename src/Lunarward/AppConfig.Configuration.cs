<<<<<<< /tmp/_ours
||||||| /tmp/_base
using Microsoft.Win32;
=======
﻿using Microsoft.Win32;
>>>>>>> /tmp/_theirs
using Starward.Features.Database;
using Starward.GameInstall;
using Starward.Helpers;
using Starward.Setup.Core;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
<<<<<<< /tmp/_ours
using System.Threading.Tasks;
||||||| /tmp/_base
using System.Threading.Tasks;
using Vanara.PInvoke;
=======
using Vanara.PInvoke;
>>>>>>> /tmp/_theirs

namespace Starward;

public static partial class AppConfig
{

    public static string? StarwardPortableLauncherExecutePath { get; private set; }

    public static string AppVersion { get; private set; }

    public static InstallType InstallType { get; set; }

    public static bool IsPortable => InstallType is InstallType.Portable;

    public static bool IsAppInRemovableStorage { get; private set; }

    /// <summary>
    /// 应用数据目录（名字沿用上游的 CacheFolder，实际是「数据」而非纯缓存）。
    /// 纯缓存放 <see cref="CacheDirectory"/>（D57）。
    /// </summary>
    public static string CacheFolder { get; private set; }


    /// <summary>纯缓存目录：XDG_CACHE_HOME/lunarward（默认 ~/.cache/lunarward）。删掉不影响任何用户数据。</summary>
    public static string CacheDirectory { get; private set; }

    public static string ConfigPath { get; private set; }

    public static string? Language { get; set; }

    public static string? UserDataFolder { get; set; }

    public static bool IsAdmin { get; private set; }

    public static string LogFile { get; private set; }

    /// <summary>XDG 数据根目录（~/.local/share）。</summary>
    /// <summary>XDG 缓存根目录（~/.cache）。</summary>
    internal static string LinuxCacheHome
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            return string.IsNullOrWhiteSpace(xdg)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache")
                : xdg;
        }
    }


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




    public static void CheckEnviroment()
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
                    ShowNoPermissionMessageBox(parentFolder);
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

            CacheDirectory = Path.Combine(LinuxCacheHome, "lunarward");
            try
            {
                Directory.CreateDirectory(CacheDirectory);
            }
            catch
            {
            }
            GameInstallEnv.CacheFolder = CacheFolder;

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
                    GameFileDatabase.SetDatabase(userDataFolder);
                    LoadConfiguration();
                }
                else
                {
<<<<<<< /tmp/_ours
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
||||||| /tmp/_base
                    await new NoPermissionWindow(userDataFolder).WaitAsync();
                    Environment.Exit(0);
                }
            }
            else
            {
                if (await new WelcomeWindow().WaitAsync())
                {
                    LoadConfiguration();
                }
                else
                {
                    Environment.Exit(0);
                }
=======
                    ShowNoPermissionMessageBox(userDataFolder);
                    Environment.Exit(0);
                }
>>>>>>> /tmp/_theirs
            }
        }
        catch (Exception ex)
        {
<<<<<<< /tmp/_ours
            System.Diagnostics.Debug.WriteLine($"{Lang.AppConfig_AnUnknownIssueOccurredDuringInitialization}\n{ex.Message}");
||||||| /tmp/_base
            User32.MessageBox(HWND.NULL, $"{Lang.AppConfig_AnUnknownIssueOccurredDuringInitialization}\n{ex.Message}", "Starward", User32.MB_FLAGS.MB_OK);
=======
            User32.MessageBox(HWND.NULL, $"{Lang.AppConfig_AnUnknownIssueOccurredDuringInitialization}\n{ex.Message}", "Starward", User32.MB_FLAGS.MB_OK);
            ClipboardHelper.SetText(ex.ToString());
>>>>>>> /tmp/_theirs
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


    private static void ShowNoPermissionMessageBox(string folder)
    {
        User32.MessageBox(HWND.NULL, $"{Lang.NoPermissionWindow_NoPermissionToFolderRestartOrClose}\n{folder}", "Starward", User32.MB_FLAGS.MB_OK);
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


    private static void LoadConfigurationFromConfigFile(string path)
    {
        if (File.Exists(path))
        {
            string text = File.ReadAllText(path);
            string lang = Regex.Match(text, @"Language=(.+)").Groups[1].Value.Trim();
            SetLanguage(lang);
        }
    }


<<<<<<< /tmp/_ours
    public static void LoadConfigurationFromStore()
||||||| /tmp/_base
    public static void LoadConfigurationFromRegistry()
=======
    private static void LoadConfigurationFromRegistry()
>>>>>>> /tmp/_theirs
    {
<<<<<<< /tmp/_ours
        string? lang = LinuxConfig.Get("Language")?.Trim();
        EnableLoginAuthTicket = LinuxConfig.Get("EnableLoginAuthTicket") is "1" or "True" or "true";
        stoken = LinuxConfig.Get("stoken")?.Trim();
        mid = LinuxConfig.Get("mid")?.Trim();
||||||| /tmp/_base
#if DEBUG
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Starward.Debug");
#else
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Starward");
#endif
        string? lang = (key.GetValue("Language") as string)?.Trim();
        EnableLoginAuthTicket = key.GetValue("EnableLoginAuthTicket") is 1;
        stoken = (key.GetValue("stoken") as string)?.Trim();
        mid = (key.GetValue("mid") as string)?.Trim();
=======
#if DEBUG
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Starward.Debug");
#else
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Starward");
#endif
        string? lang = (key.GetValue("Language") as string)?.Trim();
>>>>>>> /tmp/_theirs
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


<<<<<<< /tmp/_ours
    public static void SaveConfigurationToStore()
||||||| /tmp/_base
    public static void SaveConfigurationToRegistry()
=======
    private static void SaveConfigurationToRegistry()
>>>>>>> /tmp/_theirs
    {
        try
        {
            LinuxConfig.Set("Language", Language);
            if (!string.IsNullOrWhiteSpace(UserDataFolder))
            {
                LinuxConfig.Set("UserDataFolder", UserDataFolder);
            }
<<<<<<< /tmp/_ours
            if (EnableLoginAuthTicket.HasValue)
            {
                LinuxConfig.Set("EnableLoginAuthTicket", EnableLoginAuthTicket.Value ? "1" : "0");
            }
            LinuxConfig.Set("stoken", stoken);
            LinuxConfig.Set("mid", mid);
            LinuxConfig.Save();
||||||| /tmp/_base
            if (EnableLoginAuthTicket.HasValue)
            {
                key.SetValue("EnableLoginAuthTicket", EnableLoginAuthTicket.Value ? 1 : 0);
            }
            if (!string.IsNullOrWhiteSpace(stoken))
            {
                key.SetValue("stoken", stoken);
            }
            if (!string.IsNullOrWhiteSpace(mid))
            {
                key.SetValue("mid", mid);
            }
=======
>>>>>>> /tmp/_theirs
        }
        catch { }
    }


    private static void SaveConfigurationToConfigFile()
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
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, sb.ToString());
        }
        catch { }
    }



}
