// Lunarward：更新完成后的版本标记。
//
// 米哈游启动器靠三个地方判断「本地是哪个版本」：
//   - `<安装目录>/config.ini` 的 `game_version=`
//   - `<安装目录>/Audio_<语种>_pkg_version`（每个语音包一个文件，内容是版本号）
// 漏写这两个，会出现「文件已经是最新，但启动器仍认为该更新」的循环。

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Starward.Features.GameInstall.Sophon;


public static class GameVersionMarker
{

    /// <summary>语音类别 → 音频清单文件名（只用于诊断，不写入）。</summary>
    private static readonly Dictionary<string, string> AudioVersionFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["zh-cn"] = "Audio_Chinese_pkg_version",
        ["en-us"] = "Audio_English(US)_pkg_version",
        ["ja-jp"] = "Audio_Japanese_pkg_version",
        ["ko-kr"] = "Audio_Korean_pkg_version",
    };


    public static string? ReadGameVersion(string installPath)
    {
        string config = Path.Combine(installPath, "config.ini");
        if (!File.Exists(config))
        {
            return null;
        }
        string? version = null;
        foreach (string line in File.ReadLines(config))
        {
            if (line.StartsWith("game_version=", StringComparison.Ordinal))
            {
                version = line["game_version=".Length..].Trim();
            }
        }
        return string.IsNullOrEmpty(version) ? null : version;
    }


    /// <summary>
    /// 写 `config.ini` 的 `game_version`：只替换这一行，不重写整个文件（避免丢掉渠道等配置）。
    /// </summary>
    public static bool WriteGameVersion(string installPath, string version)
    {
        string config = Path.Combine(installPath, "config.ini");
        if (!File.Exists(config))
        {
            return false;
        }
        string[] lines = File.ReadAllLines(config);
        bool replaced = false;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("game_version=", StringComparison.Ordinal))
            {
                lines[i] = $"game_version={version}";
                replaced = true;
            }
        }
        if (!replaced)
        {
            var list = new List<string>(lines) { $"game_version={version}" };
            lines = list.ToArray();
        }
        string tmp = config + ".lunarward-tmp";
        File.WriteAllLines(tmp, lines);
        File.Move(tmp, config, overwrite: true);
        return true;
    }


    public static string? ReadAudioVersion(string installPath, string audioField)
    {
        if (!AudioVersionFiles.TryGetValue(audioField, out string? file))
        {
            return null;
        }
        string path = Path.Combine(installPath, file);
        return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
    }


    /// <summary>
    /// 写版本标记。**只写 `config.ini` 的 `game_version`**：`Audio_*_pkg_version` 不是版本字符串，
    /// 而是每个语音包的文件清单（JSON lines，含 md5/fileSize），乱写会让下一次音频更新校验失败。
    /// </summary>
    public static List<string> WriteAll(string installPath, string version)
    {
        var written = new List<string>();
        if (WriteGameVersion(installPath, version))
        {
            written.Add($"config.ini: game_version={version}");
        }
        return written;
    }


    /// <summary>
    /// 打印当前版本标记（诊断用）。
    /// </summary>
    public static string Describe(string installPath)
    {
        var parts = new List<string> { $"game_version={ReadGameVersion(installPath) ?? "(缺失)"}" };
        foreach ((string field, string file) in AudioVersionFiles)
        {
            string? value = ReadAudioVersion(installPath, field);
            if (value is not null)
            {
                int lines = value.Count(c => c == '\n');
                parts.Add($"{file}=清单 {lines} 行");
            }
        }
        return string.Join("  ", parts);
    }

}
