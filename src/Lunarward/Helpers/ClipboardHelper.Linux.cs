// Lunarward：Linux 剪贴板优先用 Uno/平台 API；这里用 wl-copy/xclip 兜底（Wayland 优先）。
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Starward.Helpers;

public static class ClipboardHelper
{
    /// <summary>把若干文件的本地路径以 URI 列表形式放进剪贴板（文件管理器可识别）。</summary>
    public static void SetStorageItems(Windows.ApplicationModel.DataTransfer.DataPackageOperation operation, params global::Windows.Storage.IStorageItem[] items)
    {
        var paths = items.Select(x => x.Path).Where(x => !string.IsNullOrEmpty(x)).ToArray();
        if (paths.Length == 0)
        {
            return;
        }
        // B29：文件管理器只认 text/uri-list（file:// URI），纯路径文本粘不出文件。
        string uriList = string.Join("\r\n", paths.Select(ToFileUri));
        _ = System.Threading.Tasks.Task.Run(() => TryWriteClipboard(uriList, "text/uri-list"));
    }


    private static string ToFileUri(string path)
    {
        string full = Path.GetFullPath(path);
        string escaped = string.Join("/", full.Split('/').Select(Uri.EscapeDataString));
        return "file://" + escaped;
    }


    private static void TryWriteClipboard(string text, string? mime = null)
    {
        foreach (var (cmd, args) in new[]
        {
            ("wl-copy", mime is null ? new[] { "--" } : new[] { "--type", mime }),
            ("xclip", mime is null ? new[] { "-selection", "clipboard" } : new[] { "-selection", "clipboard", "-t", mime }),
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

    /// <summary>B14：把 PNG 文件按 image/png 放进剪贴板（wl-copy / xclip）。返回是否成功。</summary>
    public static bool SetImageFile(string path)
    {
        if (!File.Exists(path))
        {
            return false;
        }
        try
        {
            var wl = new ProcessStartInfo("wl-copy") { RedirectStandardInput = true, UseShellExecute = false };
            wl.ArgumentList.Add("--type");
            wl.ArgumentList.Add("image/png");
            using (var p = Process.Start(wl))
            {
                if (p is not null)
                {
                    using (var input = File.OpenRead(path))
                    {
                        input.CopyTo(p.StandardInput.BaseStream);
                    }
                    p.StandardInput.Close();
                    p.WaitForExit(3000);
                    if (p.ExitCode == 0)
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
        }
        try
        {
            var xclip = new ProcessStartInfo("xclip") { UseShellExecute = false };
            foreach (var a in new[] { "-selection", "clipboard", "-t", "image/png", "-i" })
            {
                xclip.ArgumentList.Add(a);
            }
            xclip.ArgumentList.Add(path);
            using var p2 = Process.Start(xclip);
            if (p2 is not null)
            {
                p2.WaitForExit(3000);
                return p2.ExitCode == 0;
            }
        }
        catch
        {
        }
        return false;
    }


    /// <summary>B60：复制文本不能在 UI 线程等外部进程（每个候选最多 2s）。</summary>
    public static void SetText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }
        string value = text;
        _ = System.Threading.Tasks.Task.Run(() => TryWriteClipboard(value));
    }
}
