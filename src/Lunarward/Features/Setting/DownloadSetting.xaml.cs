using Microsoft.Extensions.Logging;
using Starward.GameInstall;
using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Starward.Features;
using Starward.Helpers;
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
            // D134：以前这里是个空壳（注释还停留在「限速由 RPC 安装器执行」的 D25 时代），
            // 用户改了滑块要等下次开始安装才生效。现在直接下发到进程内的 GameInstallRunner。
            int limit = Math.Clamp(value * 1024, 0, int.MaxValue);
            var runner = AppConfig.GetService<Starward.GameInstall.GameInstallRunner>();
            runner.SetRateLimiter(limit > 0, limit);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Set rate limiter");
        }
    }


    #endregion



}
