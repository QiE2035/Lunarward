using Microsoft.Extensions.Logging;
using Starward.Features.RPC;
using Starward.GameInstall;
using System;
using System.Threading.Tasks;


namespace Starward.Features.Setting;

public sealed partial class DownloadSetting : PageBase
{

    private readonly ILogger<DownloadSetting> _logger = AppConfig.GetLogger<DownloadSetting>();



    public DownloadSetting()
    {
        this.InitializeComponent();
    }



<<<<<<< /tmp/_ours

    #region 默认安装文件夹



    /// <summary>
    /// 默认安装文件夹
    /// </summary>
    public string? DefaultInstallPath { get; set => SetProperty(ref field, value); }



    /// <summary>
    /// 初始化默认安装路径
    /// </summary>
    private void InitializeDefaultInstallPath()
    {
        try
        {
            string? path = AppConfig.DefaultGameInstallationPath;
            if (Directory.Exists(path))
            {
                DefaultInstallPath = path;
            }
            else
            {
                AppConfig.DefaultGameInstallationPath = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get default intall path");
        }
    }



    /// <summary>
    /// 更改默认安装路径
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task ChangeDefaultInstallPathAsync()
    {
        try
        {
            var path = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (Directory.Exists(path))
            {
                DefaultInstallPath = path;
                AppConfig.DefaultGameInstallationPath = path;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change default install path");
        }
    }



    /// <summary>
    /// 打开默认安装路径
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenDefaultInstallPathAsync()
    {
        try
        {
            if (Directory.Exists(DefaultInstallPath))
            {
                Starward.Helpers.OpenUrl.Open(DefaultInstallPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open default install path");
        }
    }



    /// <summary>
    /// 删除默认安装路径
    /// </summary>
    [RelayCommand]
    private void DeleteDefaultInstallPath()
    {
        DefaultInstallPath = null;
        AppConfig.DefaultGameInstallationPath = null;
    }



    #endregion




    #region 硬链接



    public bool EnableHardLink
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.EnableHardLink = value;
            }
        }
    } = AppConfig.EnableHardLink;



    #endregion




||||||| /tmp/_base

    #region 默认安装文件夹



    /// <summary>
    /// 默认安装文件夹
    /// </summary>
    public string? DefaultInstallPath { get; set => SetProperty(ref field, value); }



    /// <summary>
    /// 初始化默认安装路径
    /// </summary>
    private void InitializeDefaultInstallPath()
    {
        try
        {
            string? path = AppConfig.DefaultGameInstallationPath;
            if (Directory.Exists(path))
            {
                DefaultInstallPath = path;
            }
            else
            {
                AppConfig.DefaultGameInstallationPath = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get default intall path");
        }
    }



    /// <summary>
    /// 更改默认安装路径
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task ChangeDefaultInstallPathAsync()
    {
        try
        {
            var path = await FileDialogHelper.PickFolderAsync(this.XamlRoot);
            if (Directory.Exists(path))
            {
                DefaultInstallPath = path;
                AppConfig.DefaultGameInstallationPath = path;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to change default install path");
        }
    }



    /// <summary>
    /// 打开默认安装路径
    /// </summary>
    /// <returns></returns>
    [RelayCommand]
    private async Task OpenDefaultInstallPathAsync()
    {
        try
        {
            if (Directory.Exists(DefaultInstallPath))
            {
                await Launcher.LaunchUriAsync(new Uri(DefaultInstallPath));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open default install path");
        }
    }



    /// <summary>
    /// 删除默认安装路径
    /// </summary>
    [RelayCommand]
    private void DeleteDefaultInstallPath()
    {
        DefaultInstallPath = null;
        AppConfig.DefaultGameInstallationPath = null;
    }



    #endregion




    #region 硬链接



    public bool EnableHardLink
    {
        get; set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.EnableHardLink = value;
            }
        }
    } = AppConfig.EnableHardLink;



    #endregion




=======
>>>>>>> /tmp/_theirs
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
<<<<<<< /tmp/_ours
            // Lunarward: 限速由 RPC 安装器执行；D25 下 M1 无下载流水线，值仅写入配置供后续使用
            await Task.CompletedTask;
||||||| /tmp/_base
            if (RpcService.CheckRpcServerRunning())
            {
                var client = RpcService.CreateRpcClient<GameInstaller.GameInstallerClient>();
                int limit = Math.Clamp(value * 1024, 0, int.MaxValue);
                await client.SetRateLimiterAsync(new RateLimiterMessage { BytesPerSecond = limit }, deadline: DateTime.UtcNow.AddSeconds(3));
            }
=======
            if (RpcService.CheckRpcServerRunning())
            {
                var client = RpcService.CreateRpcClient<GameInstaller.GameInstallerClient>();
                int limit = Math.Clamp(value * 1024, 0, int.MaxValue);
                await client.SetRateLimiterAsync(new RateLimiterMessage { Enable = limit > 0, BytesPerSecond = limit }, deadline: DateTime.UtcNow.AddSeconds(3));
            }
>>>>>>> /tmp/_theirs
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Set rate limiter");
        }
    }


    #endregion



}
