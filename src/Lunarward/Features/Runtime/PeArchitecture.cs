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


    /// <summary>前缀架构归一化：`win32` / `win64`，其它值按 win64 处理（D73 的默认）。</summary>
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
            using var reader = new StreamReader(systemReg);
            for (int i = 0; i < 32 && reader.ReadLine() is { } line; i++)
            {
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


    public static string NormalizePrefixArch(string? prefixArch)
    {
        if (string.IsNullOrWhiteSpace(prefixArch))
        {
            return "win64";
        }
        string value = prefixArch.Trim().ToLowerInvariant();
        return value is "win32" or "wow64" ? "win32" : "win64";
    }


    /// <summary>
    /// 位宽与前缀架构冲突时返回说明（Fix 建议），一致或无法判断时返回 null。
    ///
    /// 只拦真正会失败的组合：64 位程序放进 win32 前缀（`BAD EXE FORMAT`）。
    /// 反过来 32 位程序进 win64 前缀多数能跑（wine 9 起的 WoW64），只作提示由调用方决定是否 Warning。
    /// </summary>
    public static (string Problem, string Fix)? Conflict(PeArch exe, string? prefixArch)
    {
        string arch = NormalizePrefixArch(prefixArch);
        switch (exe)
        {
            case PeArch.X64 when arch == "win32":
                return (
                    "64 位程序放进 WINEARCH=win32 的前缀会直接 BAD EXE FORMAT，游戏启动不了",
                    "把这个环境的前缀架构改成 win64（或换一个 win64 前缀）");
            case PeArch.Arm64 when arch != "win32":
                return (
                    "ARM64 程序需要 Windows on ARM 运行环境，x86-64 版 wine 跑不了",
                    "换 ARM64 版 wine/Proton，或改用 x64 版游戏");
        }
        return null;
    }


    /// <summary>32 位程序 + win64 前缀：多数能跑，只在旧 wine 上会失败，作为提示。</summary>
    public static string? SoftNotice(PeArch exe, string? prefixArch)
    {
        return exe == PeArch.X86 && NormalizePrefixArch(prefixArch) == "win64"
            ? "32 位程序要走在 WoW64 里；wine 9 之前需要单独的 32 位支持，启动报错时就换成 win32 前缀"
            : null;
    }
}
