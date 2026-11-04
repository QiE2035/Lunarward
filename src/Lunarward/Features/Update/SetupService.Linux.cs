// Lunarward：D5 决定不移植 Starward.Setup（Windows 安装/更新器）。
// 这里保留类型以维持 DI 与页面引用，实际更新逻辑后续由 AUR/AppImage 自更新替代。
using Microsoft.Extensions.Logging;
using Starward.Setup.Core;
using System.Net.Http;

namespace Starward.Features.Update;

public class SetupService
{
    public SetupService(ILogger<SetupService> logger, HttpClient httpClient, ReleaseClient releaseClient)
    {
    }

    public long SetupTotalBytes { get; private set; }

    public long SetupDownloadBytes { get; private set; }
}
