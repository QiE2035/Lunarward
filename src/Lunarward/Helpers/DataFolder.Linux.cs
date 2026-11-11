// Lunarward：把「同一个应用的两个数据目录」合并成一个（D56）。
//
// 现象：~/.local/share 下同时出现 `lunarward`（我们的 XDG 数据）与 `Lunarward`
// （Uno 的 RemoteControlClient 按程序集名拼出来的，只放一个 Settings/Local.dat）。
// 原因：Uno 的 ApplicationData 目录名取自程序集名（大写 L），我们自己的路径用小写。
// 做法：以小写为准；把大写目录里的文件合并进来，再把大写目录换成指向小写的符号链接，
// 这样 Uno 之后写进来的东西也落在同一个目录里，ls 不会再看到两份。
using System;
using System.IO;

namespace Starward.Helpers;

internal static class DataFolder
{

    public const string AppFolderName = "lunarward";


    public static string DataHome
    {
        get
        {
            string? xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            if (!string.IsNullOrWhiteSpace(xdg))
            {
                return xdg;
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }
    }


    /// <summary>把小写的 `lunarward` 定为唯一目录，大写 `Lunarward` 变成它的符号链接。</summary>
    public static void Unify(string? logger = null)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return;
            }
            string home = DataHome;
            string lower = Path.Combine(home, AppFolderName);
            string upper = Path.Combine(home, "Lunarward");
            Directory.CreateDirectory(lower);

            if (!Directory.Exists(upper))
            {
                Directory.CreateSymbolicLink(upper, lower);
                Serilog.Log.Debug("data folder: created symlink {upper} -> {lower}", upper, lower);
                return;
            }
            if (new DirectoryInfo(upper).LinkTarget is not null)
            {
                return;
            }
            // 真实目录：把内容搬进小写目录（只搬不存在的文件），然后换成符号链接
            foreach (string file in Directory.EnumerateFiles(upper, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(upper, file);
                string destination = Path.Combine(lower, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (!File.Exists(destination))
                {
                    File.Move(file, destination);
                }
            }
            Directory.Delete(upper, recursive: true);
            Directory.CreateSymbolicLink(upper, lower);
            Serilog.Log.Information("data folder: merged {upper} into {lower} and replaced it with a symlink", upper, lower);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "data folder: unify failed");
        }
    }

}
