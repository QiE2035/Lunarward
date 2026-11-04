// Lunarward：Linux 剪贴板优先用 Uno/平台 API；这里用 wl-copy/xclip 兜底（Wayland 优先）。
using System;
using System.Diagnostics;
using System.Linq;

namespace Starward.Helpers;

public static class ClipboardHelper
{
    /// <summary>把若干文件的本地路径以 URI 列表形式放进剪贴板（文件管理器可识别）。</summary>
    public static void SetStorageItems(Windows.ApplicationModel.DataTransfer.DataPackageOperation operation, params global::Windows.Storage.IStorageItem[] items)
    {
        var paths = items.Select(x => x.Path).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        if (paths.Length > 0)
        {
            SetText(string.Join("\n", paths));
        }
    }

    public static void SetText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        foreach (var (cmd, args) in new[]
        {
            ("wl-copy", new[] { "--" }),
            ("xclip", new[] { "-selection", "clipboard" }),
            ("xsel", new[] { "--clipboard", "--input" }),
        })
        {
            try
            {
                var psi = new ProcessStartInfo(cmd) { RedirectStandardInput = true, UseShellExecute = false };
                foreach (var a in args)
                {
                    psi.ArgumentList.Add(a);
                }
                using var p = Process.Start(psi);
                if (p is null)
                {
                    continue;
                }
                p.StandardInput.Write(text);
                p.StandardInput.Close();
                p.WaitForExit(2000);
                if (p.ExitCode == 0)
                {
                    return;
                }
            }
            catch
            {
                // 尝试下一个工具
            }
        }
        Debug.WriteLine("[ClipboardHelper] no clipboard tool available");
    }
}
