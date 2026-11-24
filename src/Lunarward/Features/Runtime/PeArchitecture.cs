using System;
using System.IO;
using PeNet;

namespace Starward.Features.Runtime;

/// <summary>可执行文件的位宽（D78）。</summary>
public enum PeArch
{
    Unknown,
    X86,
    X64,
    Arm64,
}


/// <summary>
/// 读 PE 头判断游戏可执行文件的位宽，用于启动前比对 Wine 前缀架构（D78）。
///
/// 之前预检只看「文件在不在、能不能跑」，不看位宽：64 位 exe 塞进 `WINEARCH=win32` 的前缀
/// 会直接 `BAD EXE FORMAT`，用户看到的是 wine 的一句英文报错。这里把判断提到预检里。
/// </summary>
internal static class PeArchitecture
{
    /// <summary>读 PE 头；不是 PE 文件、读不了、被加密壳改坏一律返回 <see cref="PeArch.Unknown"/>。</summary>
    public static PeArch Describe(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return PeArch.Unknown;
        }
        try
        {
            // 非 PE 文件会让 PeFile 构造抛 PeNetParsingException，由外层 catch 兜住
            var pe = new PeFile(path);
            try
            {
                string? machine = pe.ImageNtHeaders?.FileHeader.Machine.ToString();
                switch (machine)
                {
                    case "I386":
                        return PeArch.X86;
                    case "Amd64":
                        return PeArch.X64;
                    case "Arm64":
                        return PeArch.Arm64;
                }
            }
            catch (Exception)
            {
                // ImageNtHeaders 解析失败时退回 Is64Bit
            }
            if (pe.Is64Bit)
            {
                return PeArch.X64;
            }
            return pe.Is32Bit ? PeArch.X86 : PeArch.Unknown;
        }
        catch (Exception)
        {
            return PeArch.Unknown;
        }
    }


    public static string Text(PeArch arch) => arch switch
    {
        PeArch.X86 => "32 位（x86）",
        PeArch.X64 => "64 位（x64）",
        PeArch.Arm64 => "ARM64",
        _ => "未知",
    };


    /// <summary>
    /// 读真实前缀的架构（R04）：wine 把 WINEARCH 写进 <c>&lt;prefix&gt;/system.reg</c> 的 <c>#arch=win32</c> 行，
    /// win64 前缀同样有一行（wine 3.0 起）。读不到（前缀还没建、文件被改坏）返回 null，由调用方决定降级策略。
    /// </summary>
    public static string? TryDetectPrefixArch(string? prefixPath)
    {
        if (string.IsNullOrWhiteSpace(prefixPath))
        {
            return null;
        }
        try
        {
            string systemReg = Path.Combine(prefixPath, "system.reg");
            if (!File.Exists(systemReg))
            {
                return null;
            }
            // R127：以前只看前 32 行。system.reg 的头部注释行数随 wine 版本和补丁变化（winetricks、
            // 手动改过的前缀会多出几十行），32 行外的那句 #arch= 就等于没读。改成扫到第一个
            // `[` 小节为止（真正的注册表内容从这里开始，后面不可能再有 #arch），并留 2000 行硬上限。
            using var reader = new StreamReader(systemReg);
            for (int i = 0; i < 2000 && reader.ReadLine() is { } line; i++)
            {
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    return null;
                }
                if (line.StartsWith("#arch=", StringComparison.OrdinalIgnoreCase))
                {
                    string value = line["#arch=".Length..].Trim().ToLowerInvariant();
                    return value is "win32" or "win64" ? value : null;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Debug(ex, "Read prefix arch failed: {prefix}", prefixPath);
        }
        return null;
    }


    /// <summary>
    /// 前缀架构归一化：`win32` / `win64`，其它值（含空）按 win64 处理——这是**新建**前缀时的默认。
    /// 判断「已有前缀能不能跑这个 exe」要用 <see cref="KnownArch"/>，不要用这个。
    /// </summary>
    public static string NormalizePrefixArch(string? prefixArch)
    {
        if (string.IsNullOrWhiteSpace(prefixArch))
        {
            return "win64";
        }
        string value = prefixArch.Trim().ToLowerInvariant();
        // R54：wow64 的语义是「64 位前缀 + WoW64」，之前被归一成 win32，反向建出 32 位前缀，
        // 64 位游戏随后被预检判 BAD EXE FORMAT；而且 wine 本身不接受 "wow64" 这个值。
        return value == "win32" ? "win32" : "win64";
    }


    /// <summary>
    /// 位宽与前缀架构冲突时返回说明（Fix 建议），一致或无法判断时返回 null。
    ///
    /// 只拦真正会失败的组合：64 位程序放进 win32 前缀（`BAD EXE FORMAT`）。
    /// 反过来 32 位程序进 win64 前缀多数能跑（wine 9 起的 WoW64），只作提示由调用方决定是否 Warning。
    /// </summary>
    /// <summary>
    /// R127：把「配置/侧写里的架构」读成确定值；空白和认不出的值返回 null（= 不知道）。
    /// 冲突判断不能像 <see cref="NormalizePrefixArch"/> 那样一律当 win64——前缀读不出来时
    /// 64 位程序会被判「没问题」，然后把用户送进 BAD EXE FORMAT。
    /// </summary>
    public static string? KnownArch(string? prefixArch)
    {
        if (string.IsNullOrWhiteSpace(prefixArch))
        {
            return null;
        }
        return prefixArch.Trim().ToLowerInvariant() switch
        {
            "win32" => "win32",
            "win64" or "wow64" => "win64",
            _ => null,
        };
    }


    public static (string Problem, string Fix)? Conflict(PeArch exe, string? prefixArch)
    {
        string? arch = KnownArch(prefixArch);
        if (arch is null)
        {
            return null;
        }
        switch (exe)
        {
            case PeArch.X64 when arch == "win32":
                return (
                    "64 位程序放进 WINEARCH=win32 的前缀会直接 BAD EXE FORMAT，游戏启动不了",
                    "把这个环境的前缀架构改成 win64（或换一个 win64 前缀）");
            // R127：ARM64 在 win32 前缀里同样跑不了，以前被 `arch != "win32"` 放过，预检只剩一句「通过」。
            case PeArch.Arm64:
                return (
                    "ARM64 程序需要 Windows on ARM 运行环境，x86-64 版 wine 跑不了",
                    "换 ARM64 版 wine/Proton，或改用 x64 版游戏");
        }
        return null;
    }


    /// <summary>32 位程序 + win64 前缀：多数能跑，只在旧 wine 上会失败，作为提示。</summary>
    public static string? SoftNotice(PeArch exe, string? prefixArch)
    {
        return exe == PeArch.X86 && KnownArch(prefixArch) == "win64"
            ? "32 位程序要走在 WoW64 里；wine 9 之前需要单独的 32 位支持，启动报错时就换成 win32 前缀"
            : null;
    }
}
