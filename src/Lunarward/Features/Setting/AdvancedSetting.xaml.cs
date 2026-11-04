using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Starward.Features.RPC;
using Starward.Features.UrlProtocol;
using Starward.Frameworks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.System;


namespace Starward.Features.Setting;

public sealed partial class AdvancedSetting : PageBase
{

    private readonly ILogger<AdvancedSetting> _logger = AppConfig.GetLogger<AdvancedSetting>();


    private readonly RpcService _rpcService = AppConfig.GetService<RpcService>();


    public AdvancedSetting()
    {
        this.InitializeComponent();
    }



    protected override void OnLoaded()
    {
        _ = GetRpcServerStateAsync();
        CheckUrlProtocol();
    }





    #region URL Protocol



    [ObservableProperty]
    public bool _EnableUrlProtocol;


    partial void OnEnableUrlProtocolChanged(bool value)
    {
        try
        {
            if (value)
            {
                UrlProtocolService.RegisterProtocol();
            }
            else
            {
                UrlProtocolService.UnregisterProtocol();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Enable url protocol changed");
        }
    }



    private async void CheckUrlProtocol()
    {
        try
        {
            var status = await Launcher.QueryUriSupportAsync(new Uri("starward://"), LaunchQuerySupportType.Uri);
#pragma warning disable MVVMTK0034 // Direct field reference to [ObservableProperty] backing field
            _EnableUrlProtocol = status is LaunchQuerySupportStatus.Available;
#pragma warning restore MVVMTK0034 // Direct field reference to [ObservableProperty] backing field
            OnPropertyChanged(nameof(EnableUrlProtocol));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check url protocol");
        }
    }




    [RelayCommand]
    private async Task TestUrlProtocolAsync()
    {
        try
        {
            await Launcher.LaunchUriAsync(new Uri("starward://test"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test url protocol");
        }
    }


    #endregion




    #region RPC


    public bool KeepRpcServerRunningInBackground
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                AppConfig.KeepRpcServerRunningInBackground = value;
                SetRpcServerRunning(value);
            }
        }
    } = AppConfig.KeepRpcServerRunningInBackground;



    private void SetRpcServerRunning(bool value)
    {
        try
        {
            _rpcService.KeepRunningOnExited(value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Set rpc server running");
        }
    }



    public int RPCServerProcessId { get; set => SetProperty(ref field, value); }



    private async Task GetRpcServerStateAsync()
    {
        try
        {
            RPCServerProcessId = 0;
            StackPanel_RpcState_NotRunning.Visibility = Visibility.Collapsed;
            StackPanel_RpcState_Running.Visibility = Visibility.Collapsed;
            StackPanel_RpcState_CannotConnect.Visibility = Visibility.Collapsed;
            if (RpcService.CheckRpcServerRunning())
            {
                var info = await _rpcService.GetRpcServerInfoAsync(DateTime.UtcNow.AddSeconds(3));
                RPCServerProcessId = info.ProcessId;
                StackPanel_RpcState_Running.Visibility = Visibility.Visible;
            }
            else
            {
                StackPanel_RpcState_NotRunning.Visibility = Visibility.Visible;
            }
        }
        catch (OperationCanceledException ex)
        {
            int sessionId = Process.GetCurrentProcess().SessionId;
            var process = Process.GetProcessesByName("Starward.RPC").FirstOrDefault(x => x.SessionId == sessionId);
            if (process != null)
            {
                RPCServerProcessId = process.Id;
                StackPanel_RpcState_CannotConnect.Visibility = Visibility.Visible;
            }
            else
            {
                StackPanel_RpcState_NotRunning.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get rpc server state");
        }
    }


    [RelayCommand]
    private async Task RunRpcServerAsync()
    {
        try
        {
            await _rpcService.EnsureRpcServerRunningAsync();
            await GetRpcServerStateAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run rpc server");
        }
    }



    [RelayCommand]
    private async Task StopRpcServerAsync()
    {
        try
        {
            await _rpcService.StopRpcServerAsync(DateTime.UtcNow.AddSeconds(3));
            await Task.Delay(1000);
            await GetRpcServerStateAsync();
        }
        catch (Exception ex)
        {
            try
            {
                var p = Process.GetProcessById(RPCServerProcessId);
                p.Kill();
                await Task.Delay(1000);
                await GetRpcServerStateAsync();
            }
            catch { }
        }
    }





    #endregion


    private async void Button_ViewOutput_Click(object sender, RoutedEventArgs e)
    {
        var text = new TextBlock
        {
            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Cascadia Mono, Consolas, monospace"),
            FontSize = 12,
            IsTextSelectionEnabled = true,
            TextWrapping = TextWrapping.NoWrap,
            Text = BuildOutputText(),
        };
        var scroll = new ScrollViewer
        {
            MaxHeight = 460,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = text,
        };
        var dialog = new ContentDialog
        {
            Title = "最近运行输出",
            Content = scroll,
            PrimaryButtonText = "刷新",
            SecondaryButtonText = "打开日志文件夹",
            CloseButtonText = "关闭",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result is ContentDialogResult.Primary)
        {
            // 重新打开以显示最新内容（对话框内容不可变，重开比原地刷新可靠）
            Button_ViewOutput_Click(sender, e);
        }
        else if (result is ContentDialogResult.Secondary)
        {
            OpenOutputFolder();
        }
    }


    private static string BuildOutputText()
    {
        IReadOnlyList<Features.Runtime.OutputLine> lines = Features.Runtime.OutputCapture.Recent(500);
        if (lines.Count == 0)
        {
            return "（还没有捕获到输出。启动一次游戏或「后启动程序」后，这里会出现它们写到 stdout/stderr 的内容。）";
        }
        var sb = new StringBuilder();
        foreach (Features.Runtime.OutputLine line in lines)
        {
            sb.Append(line.Time.ToString("HH:mm:ss.fff")).Append(" [").Append(line.Scope).Append(']').Append(line.IsError ? " [err] " : " ").AppendLine(line.Text);
        }
        return sb.ToString();
    }


    private void Button_OpenOutputFolder_Click(object sender, RoutedEventArgs e) => OpenOutputFolder();


    private static void OpenOutputFolder()
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "lunarward", "logs", "output");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("xdg-open", folder) { UseShellExecute = false });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "open output folder failed");
        }
    }


}
