using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Serilog;
using Starward.Features;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;


namespace Starward.Features.Setting;

// LUNARWARD-DEVIATION: 上游 0.19.0 删除了 AdvancedSetting 里的「URL Protocol」与「RPC 服务」两块。
// - RPC：上游把 RPC 子进程（src/Starward.RPC 项目）整个删除，AppConfig.KeepRpcServerRunningInBackground
//   也随之为 [Obsolete(..., true)]；本页不再引用 _rpcService / Starward.Features.RPC。
// - URL Protocol：上游把注册交给安装器，页面不再暴露开关；移植侧仍有 `--register-protocol` /
//   `--unregister-protocol` / `--protocol-status` 三个 CLI 入口（Program.Linux.cs），能力没有丢失。
// 只保留 Linux 独有的「诊断包」（D61）与「运行输出」（D27）两个入口。
public sealed partial class AdvancedSetting : PageBase
{

    private readonly ILogger<AdvancedSetting> _logger = AppConfig.GetLogger<AdvancedSetting>();



    public AdvancedSetting()
    {
        this.InitializeComponent();
    }



    private async void Button_ViewOutput_Click(object sender, RoutedEventArgs e)
    {
        var text = new TextBlock
        {
            // 不指定族名：Windows 专属族名（Cascadia Mono / Consolas）在 Linux 上不保证命中，
            // 而逗号分隔的多族名在 Uno 上是否被拆开解析没有定论（L-171）；等宽只是视觉诉求，交给主题默认字体。
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
            Title = Starward.Language.Lang.Lunarward_Diagnose_OutputTitle,
            Content = scroll,
            PrimaryButtonText = Starward.Language.Lang.Lunarward_Diagnose_Refresh,
            SecondaryButtonText = Starward.Language.Lang.Lunarward_Diagnose_OpenLogFolder,
            CloseButtonText = Starward.Language.Lang.Lunarward_Diagnose_Close,
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


    /// <summary>D61：Linux 上没有「获得管理员权限」这回事，用一个真能用的动作代替——生成诊断包。</summary>
    private async void Button_CreateDiagnose_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string file = await Helpers.DiagnosticBundle.CreateAsync(logger: _logger);
            Helpers.InAppToast.MainWindow?.Success((string?)null, $"诊断包已生成：{file}", 8000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create diagnostic bundle failed");
            Helpers.InAppToast.MainWindow?.Error((string?)null, string.Format(Starward.Language.Lang.Lunarward_Diagnose_Failed, ex.Message), 8000);
        }
    }


    private void Button_OpenDiagnoseFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloads);
            Helpers.OpenUrl.Open(downloads);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Open Downloads failed");
        }
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
