using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Lunarward.Tests;

/// <summary>
/// D108：运行环境页新增了一大批 Lunarward_RuntimeEnv_* 键。简体（Lang.zh-CN.resx）与英文（Lang.resx）
/// 是完整的一套，繁体（zh-TW / zh-HK）由 OpenCC 转换补齐；这里用测试把「三套必须对齐」钉住，
/// 以后谁加了键只写一份，测试会直接指出来。其它语言沿用 .NET 资源回落（中性英文），不参与比对。
/// </summary>
public class LocalizationKeyTests
{
    [Fact]
    public void RuntimeEnvironmentKeys_ArePresentInNeutral_AndTraditionalChinese()
    {
        string languageDir = FindLanguageDirectory();
        HashSet<string> source = Keys(Path.Combine(languageDir, "Lang.zh-CN.resx"), "Lunarward_RuntimeEnv_");
        Assert.True(source.Count > 300, $"zh-CN 里的 Lunarward_RuntimeEnv_* 键只有 {source.Count} 个，预期 300+");

        foreach ((string file, string label) in new[]
                 {
                     ("Lang.resx", "中性(英文)"),
                     ("Lang.zh-TW.resx", "繁体(台湾)"),
                     ("Lang.zh-HK.resx", "繁体(香港)"),
                 })
        {
            HashSet<string> keys = Keys(Path.Combine(languageDir, file), "Lunarward_RuntimeEnv_");
            string[] missing = source.Except(keys).OrderBy(x => x).ToArray();
            Assert.True(missing.Length == 0,
                $"{label} {file} 缺少 {missing.Length} 个键：{string.Join(", ", missing.Take(10))}");
        }
    }


    private static HashSet<string> Keys(string path, string prefix)
    {
        XDocument doc = XDocument.Load(path);
        return doc.Root!.Elements("data")
            .Select(x => (string?)x.Attribute("name"))
            .Where(x => x is not null && x.StartsWith(prefix, StringComparison.Ordinal))
            .Select(x => x!)
            .ToHashSet(StringComparer.Ordinal);
    }


    private static string FindLanguageDirectory()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, "src", "Starward.Language");
            if (File.Exists(Path.Combine(candidate, "Lang.zh-CN.resx")))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("找不到 src/Starward.Language");
    }
}
