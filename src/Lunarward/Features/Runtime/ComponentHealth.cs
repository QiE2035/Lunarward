// Lunarward：组件健康检查（D73 §2.5）。
//
// 只做「跑一下、把原话给你看」：一句概括 + 等宽只读可复制原文，不做发行版包名映射
// （映射会漏信息，用户明确要求展示原文）。
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Runtime;


public sealed class ComponentHealthResult
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public bool Ok { get; init; }

    /// <summary>一句话结论，例如「可用（wine-9.22）」「启动失败（退出码 1）」。</summary>
    public string Summary { get; init; } = "";

    /// <summary>命令原文 + stdout/stderr 原文，等宽显示给用户复制。</summary>
    public string Output { get; init; } = "";

    public int ExitCode { get; init; }

    public TimeSpan Elapsed { get; init; }
}


public static class ComponentHealth
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);


    /// <summary>探测一个组件。耗时可能到秒级，调用方负责放到后台线程。</summary>
    public static async Task<ComponentHealthResult> CheckAsync(Component component, ILogger? logger = null,
        CancellationToken ct = default)
    {
        if (component.Kind is ComponentKind.Dxvk or ComponentKind.Vkd3dProton)
        {
            return CheckGraphics(component);
        }
        return await CheckRunnerAsync(component, logger, ct).ConfigureAwait(false);
    }


    private static ComponentHealthResult CheckGraphics(Component component)
    {
        string x64 = Path.Combine(component.Path, "x64");
        string? dll = Directory.Exists(x64)
            ? Path.Combine(x64, component.Kind == ComponentKind.Dxvk ? "d3d11.dll" : "d3d12.dll")
            : Path.Combine(component.Path, component.Kind == ComponentKind.Dxvk ? "d3d11.dll" : "d3d12.dll");
        bool ok = File.Exists(dll);
        var builder = new StringBuilder();
        builder.Append("path: ").AppendLine(component.Path);
        builder.Append("expect: ").AppendLine(dll);
        builder.Append("exists: ").AppendLine(ok ? "yes" : "no");
        return new ComponentHealthResult
        {
            Id = component.Id,
            Name = component.DisplayName,
            Ok = ok,
            Summary = ok ? "文件齐全" : "缺少文件",
            Output = builder.ToString(),
            ExitCode = ok ? 0 : 1,
        };
    }


    private static async Task<ComponentHealthResult> CheckRunnerAsync(Component component, ILogger? logger,
        CancellationToken ct)
    {
        string command = $"{component.Path} --version";
        var builder = new StringBuilder();
        builder.Append("$ ").AppendLine(command);
        var stopwatch = Stopwatch.StartNew();
        int exitCode = -1;
        try
        {
            var startInfo = new ProcessStartInfo(component.Path)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("--version");
            // 前缀无关：--version 不需要 WINEPREFIX，显式给一个不存在的值也无作用，
            // 这里干脆不设，避免误建或误用某个前缀。
            startInfo.Environment.Remove("WINEPREFIX");
            using var process = new Process { StartInfo = startInfo };
            process.Start();
            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            Task<string> stderrTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(Timeout);
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            string stdout = (await stdoutTask.ConfigureAwait(false)).Trim();
            string stderr = (await stderrTask.ConfigureAwait(false)).Trim();
            exitCode = process.ExitCode;
            builder.Append("exit: ").AppendLine(exitCode.ToString());
            if (stdout.Length > 0)
            {
                builder.Append("stdout:").AppendLine().AppendLine(stdout);
            }
            if (stderr.Length > 0)
            {
                builder.Append("stderr:").AppendLine().AppendLine(stderr);
            }
            stopwatch.Stop();
            string version = stdout.Length > 0 ? stdout.Split('\n')[0] : "";
            bool ok = exitCode == 0 && version.Contains("wine", StringComparison.OrdinalIgnoreCase);
            return new ComponentHealthResult
            {
                Id = component.Id,
                Name = component.DisplayName,
                Ok = ok,
                Summary = ok ? $"可用（{version}）" : $"不可用（退出码 {exitCode}）",
                Output = builder.ToString(),
                ExitCode = exitCode,
                Elapsed = stopwatch.Elapsed,
            };
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            builder.Append("timeout: 15 秒内没有返回").AppendLine();
            logger?.LogWarning("[components] 健康检查超时：{Id}", component.Id);
            return new ComponentHealthResult
            {
                Id = component.Id,
                Name = component.DisplayName,
                Ok = false,
                Summary = "超时（15 秒）",
                Output = builder.ToString(),
                ExitCode = exitCode,
                Elapsed = stopwatch.Elapsed,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            builder.Append("exception: ").AppendLine(ex.GetType().Name + ": " + ex.Message);
            logger?.LogWarning(ex, "[components] 健康检查失败：{Id}", component.Id);
            return new ComponentHealthResult
            {
                Id = component.Id,
                Name = component.DisplayName,
                Ok = false,
                Summary = "启动失败（见原文）",
                Output = builder.ToString(),
                ExitCode = exitCode,
                Elapsed = stopwatch.Elapsed,
            };
        }
    }
}
