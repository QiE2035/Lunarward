using Microsoft.Extensions.Logging;
using Starward.Features.RPC;
using Starward.GameInstall;
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Starward.Features;
using Starward.Helpers;
using Starward.RPC.GameInstall;
using System.IO;
using Windows.System;


namespace Starward.Features.Setting;

public sealed partial class DownloadSetting : PageBase
{

    private readonly ILogger<DownloadSetting> _logger = AppConfig.GetLogger<DownloadSetting>();



    public DownloadSetting()
    {
        this.InitializeComponent();
    }



    #region 下载限速



    /// <summary>
    /// 下载限速
    /// </summary>
    public int SpeedLimit
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.SpeedLimitKBPerSecond = value;
                _ = SetRateLimiterAsync(value);
            }
        }
    } = AppConfig.SpeedLimitKBPerSecond;



    private async Task SetRateLimiterAsync(int value)
    {
        try
        {
            // Lunarward: 限速由 RPC 安装器执行；D25 下 M1 无下载流水线，值仅写入配置供后续使用
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Set rate limiter");
        }
    }


    #endregion



}
