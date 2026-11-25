// LUNARWARD-DEVIATION: 上游的 GenshinHotUpdateService.GetServerName 读 HKCU 注册表拿 selectedServerName
// （`Registry.GetValue(@"HKEY_CURRENT_USER\Software\miHoYo\Genshin Impact", "GENERAL_DATA_h2389025596", null)`）。
// Linux 上没有 Windows 注册表，但 wine 把整棵树落盘成 <prefix>/user.reg 纯文本，直接读文件即可（D132）。
//
// 只做这一件事，所以刻意比 Lunarward 那边的 WineRegistryFiles（通用键值解析 + reg query 回落）窄：
// 这里只要「某个键下的某个 REG_BINARY 值」，没有回落路径（跑热更新时前缀必然存在，读不到就当没有）。
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text;

namespace Starward.GameInstall.Service;

/// <summary>
/// 只读解析 wine 前缀里的 <c>user.reg</c>，供热更新读取游戏自己写入的注册表值。
/// </summary>
public static class WinePrefixRegistry
{
    /// <summary>从安装目录往上找 wine 前缀（含 <c>drive_c</c> 与 <c>user.reg</c> 的那层）。找不到返回 null。</summary>
    public static string? FindPrefix(string? installPath)
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }
        try
        {
            DirectoryInfo? dir = new(Path.GetFullPath(installPath));
            if (!dir.Exists && dir.Parent is not null)
            {
                // 传进来的是文件路径（或还不存在的目录）时从父目录开始
                dir = dir.Parent;
            }
            for (int depth = 0; dir is not null && depth < 16; depth++, dir = dir.Parent)
            {
                if (LooksLikePrefix(dir.FullName))
                {
                    return dir.FullName;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }
        return null;
    }

    private static bool LooksLikePrefix(string path)
    {
        try
        {
            return File.Exists(Path.Combine(path, "user.reg")) && Directory.Exists(Path.Combine(path, "drive_c"));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// 读取 <paramref name="prefix"/>/user.reg 中 <paramref name="keyPath"/> 键下名为
    /// <paramref name="valueName"/> 的 REG_BINARY 值。读不到（无前缀/无文件/无该键/类型不是 hex）返回 null。
    /// </summary>
    public static byte[]? ReadUserRegBinary(string? prefix, string keyPath, string valueName)
    {
        if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(valueName))
        {
            return null;
        }
        string file = Path.Combine(prefix, "user.reg");
        if (!File.Exists(file))
        {
            return null;
        }
        try
        {
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (!TryParseSectionName(line, out string section))
                {
                    continue;
                }
                if (!section.Equals(keyPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return ReadBinaryValue(reader, valueName);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
        }
        return null;
    }

    /// <summary>键段行形如 <c>[Software\\miHoYo\\Genshin Impact] 1750000000</c>。</summary>
    private static bool TryParseSectionName(string line, out string section)
    {
        section = "";
        if (line.Length < 3 || line[0] != '[')
        {
            return false;
        }
        int end = line.LastIndexOf(']');
        if (end <= 1)
        {
            return false;
        }
        section = Unescape(line[1..end]);
        return true;
    }

    private static byte[]? ReadBinaryValue(TextReader reader, string valueName)
    {
        string? pending = null;
        string? raw;
        while ((raw = reader.ReadLine()) is not null)
        {
            if (raw.Length > 0 && raw[0] == '[')
            {
                return null;
            }
            string line = raw.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }
            if (pending is not null)
            {
                line = pending + line;
                pending = null;
            }
            if (line[^1] == '\\')
            {
                // 长二进制在文件里折行：行尾反斜杠，下一行是缩进后的后续字节
                pending = line[..^1];
                continue;
            }
            if (!line.StartsWith('"'))
            {
                continue;
            }
            int close = FindClosingQuote(line, 1);
            if (close < 0)
            {
                continue;
            }
            if (!Unescape(line[1..close]).Equals(valueName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            int equals = line.IndexOf('=', close + 1);
            if (equals < 0)
            {
                return null;
            }
            return ParseHexBytes(line[(equals + 1)..].Trim());
        }
        return null;
    }

    /// <summary>解析 <c>hex:7b,22,...</c>；<c>hex(2)</c> / <c>dword:</c> 等类型一律返回 null（本题只关心 REG_BINARY）。</summary>
    private static byte[]? ParseHexBytes(string data)
    {
        if (!data.StartsWith("hex:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        string[] parts = data[4..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var bytes = new List<byte>(parts.Length);
        foreach (string part in parts)
        {
            if (!byte.TryParse(part, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte value))
            {
                return null;
            }
            bytes.Add(value);
        }
        return bytes.ToArray();
    }

    private static int FindClosingQuote(string line, int start)
    {
        for (int i = start; i < line.Length; i++)
        {
            if (line[i] == '\\')
            {
                i++;
                continue;
            }
            if (line[i] == '"')
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>还原 wine 的转义：<c>\\</c>、<c>\xNNNN</c>（UTF-16 码元）、<c>\n</c>、<c>\r</c>、<c>\0</c>、<c>\"</c>。</summary>
    private static string Unescape(string text)
    {
        if (text.IndexOf('\\') < 0)
        {
            return text;
        }
        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch != '\\' || i + 1 >= text.Length)
            {
                builder.Append(ch);
                continue;
            }
            char next = text[++i];
            switch (next)
            {
                case '\\':
                    builder.Append('\\');
                    break;
                case '"':
                    builder.Append('"');
                    break;
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case '0':
                    builder.Append('\0');
                    break;
                case 'x':
                case 'X':
                    if (TryParseHexChar(text, ref i, out char decoded))
                    {
                        builder.Append(decoded);
                    }
                    break;
                default:
                    builder.Append(next);
                    break;
            }
        }
        return builder.ToString();
    }

    private static bool TryParseHexChar(string text, ref int index, out char value)
    {
        value = '\0';
        int start = index + 1;
        int length = 0;
        while (length < 4 && start + length < text.Length && Uri.IsHexDigit(text[start + length]))
        {
            length++;
        }
        if (length == 0)
        {
            return false;
        }
        value = (char)int.Parse(text.AsSpan(start, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        index = start + length - 1;
        return true;
    }
}
