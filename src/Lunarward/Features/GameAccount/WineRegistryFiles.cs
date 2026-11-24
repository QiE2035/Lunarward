// Lunarward：直接解析 wine 的注册表文件（<prefix>/user.reg、system.reg），替代一次 `wine reg query` 的 fork。
//
// 为什么要有这条路（D86、L-160）：
//   每次 `wine reg query` 都是一次 fork + wineserver 冷启动，实测冷路径 1–5 秒（本机 1.1 s，历史另测 4–5 s），
//   热 0.13–0.2 秒，口径见 WineRegistry 头注释。
//   上游 src/Starward/ 里的同步 API（GameSettingService.SetGameResolutionSetting / SetGenshinEnableHDR 等）没法异步化，
//   我们在 Linux 侧只能用 `GetAwaiter().GetResult()` 包一层，于是「改个画质」「启动原神」都要在 UI 线程上等 fork。
//   注册表本体就在前缀里、是纯文本，直接读文件就是毫秒级 —— 从根上消掉这条 fork。
//
// 与 `reg query` 输出的语义差异（两处都是文件版更正确）：
//   1. 长 REG_BINARY 在文件里折行（行尾是 `,` 加反斜杠续行），这里按折行拼回完整字节；
//      而 RegistryFormat.ParseLine 解析 `reg query` 输出时只取首行，会把长值截断。
//   2. 键名里的非 ASCII 字符在文件里写成 \x539f\x795e 这种形式（「原神」），这里解码回原文再做匹配。
//
// 单独成文件是为了让单元测试只链接它（不牵进 wine 进程、前缀、日志等运行时依赖）。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Starward.Features.GameAccount;

internal static class WineRegistryFiles
{
    /// <summary>
    /// 尝试从前缀的注册表文件里读出一个键的全部值。
    /// <para>返回 null = 这个方法给不出结论（根键不支持 / 文件不存在 / 文件里没有这个键 / IO 失败），调用方应回落到 <c>wine reg query</c>。</para>
    /// <para>返回字典（可能为空）= 文件里确实有这一段，结果可直接采用。</para>
    /// "文件里没有这个键" 也返回 null 是有意的：wine 可能还没把内存里的改动落盘，
    /// 这时候 fork 一次 `reg query` 拿到的才是准的。
    /// </summary>
    internal static Dictionary<string, object?>? TryReadKeyValues(string? prefix, string? normalizedKey, out string? reason)
    {
        reason = null;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            reason = "没有前缀";
            return null;
        }
        if (string.IsNullOrWhiteSpace(normalizedKey) || !TrySplitHive(normalizedKey, out string hive, out string relative) || relative.Length == 0)
        {
            reason = "根键不支持文件读取";
            return null;
        }
        string file = hive == "HKLM" ? "system.reg" : "user.reg";
        string path = Path.Combine(prefix, file);
        if (!File.Exists(path))
        {
            reason = "注册表文件不存在";
            return null;
        }
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 64 * 1024, FileOptions.SequentialScan);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (line.Length < 3 || line[0] != '[')
                {
                    continue;
                }
                int end = line.LastIndexOf(']');
                if (end <= 1)
                {
                    continue;
                }
                string sectionKey = Unescape(line.AsSpan(1, end - 1).ToString(), escapeControls: false);
                if (!sectionKey.Equals(relative, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                return ReadSection(reader);
            }
            reason = "注册表文件里没有这个键";
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            reason = ex.Message;
            return null;
        }
    }


    /// <summary>把 HKCU\Software\miHoYo\原神 拆成 (HKCU, Software\miHoYo\原神)。</summary>
    private static bool TrySplitHive(string key, out string hive, out string relative)
    {
        hive = "";
        relative = "";
        int separator = key.IndexOf('\\');
        if (separator <= 0)
        {
            return false;
        }
        string root = key[..separator].ToUpperInvariant();
        if (root is not ("HKCU" or "HKLM" or "HKU" or "HKCR"))
        {
            return false;
        }
        hive = root == "HKCR" ? "HKLM" : root;
        relative = key[(separator + 1)..];
        return true;
    }


    /// <summary>读取当前键段里的所有值，遇到下一个 [ 键或文件尾就停。</summary>
    private static Dictionary<string, object?> ReadSection(TextReader reader)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        string? pending = null;
        while (true)
        {
            string? raw = reader.ReadLine();
            if (raw is null)
            {
                break;
            }
            if (raw.Length > 0 && raw[0] == '[')
            {
                break;
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
                // 长二进制折行：行尾反斜杠，下一行是缩进后的后续字节
                pending = line[..^1];
                continue;
            }
            TryParseValueLine(line, values);
        }
        if (pending is not null)
        {
            TryParseValueLine(pending, values);
        }
        return values;
    }


    private static void TryParseValueLine(string line, Dictionary<string, object?> values)
    {
        string name;
        string data;
        if (line[0] == '"')
        {
            int close = FindClosingQuote(line, 1);
            if (close < 0)
            {
                return;
            }
            name = Unescape(line[1..close], escapeControls: false);
            int equals = line.IndexOf('=', close + 1);
            if (equals < 0)
            {
                return;
            }
            data = line[(equals + 1)..].Trim();
        }
        else if (line[0] == '@')
        {
            int equals = line.IndexOf('=');
            if (equals < 0)
            {
                return;
            }
            name = "";
            data = line[(equals + 1)..].Trim();
        }
        else
        {
            return;
        }
        // 默认值（@=）没有名字，与 RegistryFormat.ParseValues 一致地跳过
        if (name.Length == 0)
        {
            return;
        }
        object? value = ParseData(data);
        if (value is not null)
        {
            values[name] = value;
        }
    }


    /// <summary>解析等号右边的数据，返回类型与 RegistryFormat.ParseValues 对齐（byte[] / int / string）。</summary>
    private static object? ParseData(string data)
    {
        if (data.Length == 0)
        {
            return null;
        }
        if (data[0] == '"')
        {
            int end = FindClosingQuote(data, 1);
            if (end < 0)
            {
                end = data.Length;
            }
            return Unescape(data[1..end], escapeControls: true);
        }
        if (data.StartsWith("dword:", StringComparison.OrdinalIgnoreCase))
        {
            string hex = data[6..].Trim();
            return uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint dword) ? (int)dword : null;
        }
        if (data.StartsWith("hex", StringComparison.OrdinalIgnoreCase))
        {
            int colon = data.IndexOf(':');
            if (colon < 0)
            {
                return null;
            }
            string type = data[..colon].Trim();
            byte[] bytes = ParseHexBytes(data[(colon + 1)..]);
            return type switch
            {
                "hex" => bytes,
                // REG_EXPAND_SZ：UTF-16LE，结尾带 NUL
                "hex(2)" => TrimAtNull(Encoding.Unicode.GetString(bytes)),
                _ => null,
            };
        }
        return null;
    }


    private static byte[] ParseHexBytes(string data)
    {
        var bytes = new List<byte>(data.Length / 3 + 1);
        foreach (string part in data.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            string token = part.Trim();
            if (token.Length == 0)
            {
                continue;
            }
            if (byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                bytes.Add(b);
            }
        }
        return bytes.ToArray();
    }


    private static string TrimAtNull(string text)
    {
        int index = text.IndexOf('\0');
        return index < 0 ? text : text[..index];
    }


    /// <summary>找第一个没有被反斜杠转义的引号。</summary>
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


    /// <summary>
    /// wine 注册表文件里的转义：<c>\\</c> 是反斜杠、<c>\x539f</c> 是码位 0x539F（4 位十六进制）、
    /// 字符串值另外还有 <c>\"</c> <c>\n</c> <c>\r</c> <c>\0</c>。
    /// </summary>
    internal static string Unescape(string text, bool escapeControls)
    {
        if (text.IndexOf('\\') < 0)
        {
            return text;
        }
        var builder = new StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c != '\\' || i + 1 >= text.Length)
            {
                builder.Append(c);
                continue;
            }
            char next = text[i + 1];
            if (next == '\\')
            {
                builder.Append('\\');
                i++;
            }
            else if (next == 'x' && i + 6 <= text.Length
                     && TryParseHex4(text.AsSpan(i + 2, 4), out char decoded))
            {
                builder.Append(decoded);
                i += 5;
            }
            else if (escapeControls && next == 'n')
            {
                builder.Append('\n');
                i++;
            }
            else if (escapeControls && next == 'r')
            {
                builder.Append('\r');
                i++;
            }
            else if (escapeControls && next == '0')
            {
                builder.Append('\0');
                i++;
            }
            else
            {
                // \" 之类的「转义成自己」
                builder.Append(next);
                i++;
            }
        }
        return builder.ToString();
    }


    private static bool TryParseHex4(ReadOnlySpan<char> span, out char value)
    {
        value = '\0';
        if (span.Length != 4)
        {
            return false;
        }
        int result = 0;
        foreach (char c in span)
        {
            int digit = c switch
            {
                >= '0' and <= '9' => c - '0',
                >= 'a' and <= 'f' => c - 'a' + 10,
                >= 'A' and <= 'F' => c - 'A' + 10,
                _ => -1,
            };
            if (digit < 0)
            {
                return false;
            }
            result = (result << 4) | digit;
        }
        value = (char)result;
        return true;
    }
}
