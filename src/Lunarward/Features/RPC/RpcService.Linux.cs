// Lunarward：上游 RPC 走 Windows 命名管道 + gRPC（Starward.RPC 为 net10.0-windows）。
// D6 决定改用 Unix domain socket / 进程内实现；在 M2 落地前，这里提供同签名的最小实现，
// 使页面与 DI 可编译、运行时不崩（RPC 相关功能视为不可用）。
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Starward.Features.RPC;

public sealed class RpcServerInfo
{
    public int ProcessId { get; set; }

    public DateTime StartTime { get; set; }
}

internal class RpcService
{
    private readonly ILogger<RpcService> _logger;

    public RpcService(ILogger<RpcService> logger)
    {
        _logger = logger;
    }

    public static bool CheckRpcServerRunning() => false;

    public Task<RpcServerInfo?> GetRpcServerInfoAsync(DateTime deadline) => Task.FromResult<RpcServerInfo?>(null);

    public Task<bool> EnsureRpcServerRunningAsync() => Task.FromResult(false);

    public void TrySetEnviromentAsync()
    {
        _logger.LogDebug("RPC is not available on Linux yet (see D6)");
    }

    public void KeepRunningOnExited(bool value, bool noLongerChange = false) { }

    public Task StopRpcServerAsync(DateTime deadline) => Task.CompletedTask;

    /// <summary>下载限速等 RPC 功能在 Linux 侧暂未接入（D6）。</summary>
    public static T CreateRpcClient<T>() where T : class => throw new NotSupportedException("RPC is not available on Linux yet (see D6)");
}
