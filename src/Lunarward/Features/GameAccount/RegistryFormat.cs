// Lunarward：`wine reg` 输出/输入格式的纯函数集合。
// 单独成文件是为了让单元测试只链接它（不牵进 wine 进程、前缀、日志等运行时依赖）。
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Starward.Features.GameAccount;

internal static class RegistryFormat
{
    /// <summary>HKEY_CURRENT_USER\... 归一成 HKCU\...（reg.exe 两种都认，统一后日志和缓存键更短）。</summary>
    internal static string NormalizeKey(string key)
    {
        if (key.StartsWith("HKEY_CURRENT_USER\\", StringComparison.OrdinalIgnoreCase))
        {
            return "HKCU\\" + key["HKEY_CURRENT_USER\\".Length..];
        }
        if (key.StartsWith("HKEY_LOCAL_MACHINE\\", StringComparison.OrdinalIgnoreCase))
        {
            return "HKLM\\" + key["HKEY_LOCAL_MACHINE\\".Length..];
        }
        return key;
    }


    internal static string ToHex(byte[] bytes) => string.Join(",", bytes.Select(b => b.ToString("x2")));


    /// <summary>解析 `reg query` 的整键输出（每行一个值）。</summary>
    internal static Dictionary<string, object?> ParseValues(string output)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in output.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }
            if (trimmed.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("(Default)", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            object? value = ParseLine(line, out string? name);
            // `reg query` 会把默认值打印成 `(Default)    REG_SZ    xxx`，它不是有名字的值
            if (string.IsNullOrEmpty(name) || name!.Equals("(Default)", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            result[name!] = value;
        }
        return result;
    }


    internal static object? ParseLine(string line, out string? name)
    {
        // 形如：  MIHOYOSDK_ADL_PROD_CN_h3123967166    REG_BINARY    01,02,03
        //        App_LastUserID_h2841727341          REG_DWORD     0x64
        name = null;
        int typeIndex = line.IndexOf("REG_", StringComparison.Ordinal);
        if (typeIndex <= 0)
        {
            return null;
        }
        name = line[..typeIndex].Trim();
        if (name.Length == 0)
        {
            name = null;
            return null;
        }
        int typeEnd = line.IndexOfAny([' ', '\t'], typeIndex);
        string type = typeEnd < 0 ? line[typeIndex..].Trim() : line[typeIndex..typeEnd].Trim();
        string data = typeEnd < 0 ? "" : line[typeEnd..].Trim();

        switch (type)
        {
            case "REG_BINARY":
                // 长二进制会被 reg.exe 折行，续行以 \ 结尾；这里只取首行，调用方按需自行处理。
                data = data.TrimEnd('\\').Trim();
                if (data.Length == 0)
                {
                    return Array.Empty<byte>();
                }
                try
                {
                    return data.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => byte.Parse(x.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                        .ToArray();
                }
                catch (FormatException)
                {
                    return null;
                }
            case "REG_DWORD":
                var hex = data.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? data[2..] : data;
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var dword))
                {
                    return (int)dword;
                }
                return null;
            case "REG_SZ":
            case "REG_EXPAND_SZ":
                return data.Trim('"');
            default:
                return null;
        }
    }


    /// <summary>生成 reg import 用的 .reg 文本（调用方负责按 UTF-16LE 落盘，wine 只认这种编码）。</summary>
    internal static string BuildRegFile(string key, IReadOnlyDictionary<string, object> values)
    {
        var builder = new StringBuilder();
        builder.Append("Windows Registry Editor Version 5.00\r\n\r\n");
        builder.Append('[').Append(key).Append("]\r\n");
        foreach (var (name, value) in values)
        {
            switch (value)
            {
                case byte[] bytes:
                    builder.Append('"').Append(name).Append("\"=hex:").Append(ToHex(bytes)).Append("\r\n");
                    break;
                case int or long or uint:
                    var dword = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
                    builder.Append('"').Append(name).Append("\"=dword:").Append(dword.ToString("x8", CultureInfo.InvariantCulture)).Append("\r\n");
                    break;
                default:
                    builder.Append('"').Append(name).Append("\"=\"").Append(value?.ToString()?.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"\r\n");
                    break;
            }
        }
        return builder.ToString();
    }
}
