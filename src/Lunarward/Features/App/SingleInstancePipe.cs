// Lunarward：单实例「第二次启动」的通知通道。
//
// 文件锁只回答「有没有人在跑」；用户再点一次图标时想要的是「把窗口叫到前面」，所以还要一条通道。
// 用 UNIX domain socket（~/.local/state/lunarward/lunarward.sock），协议简单到只有几行文本：
//   show                 唤起主窗口
//   exit                 退出主实例
//   startgame<TAB>biz    在主实例里启动游戏（`lunarward://startgame?biz=hk4e_cn` 深链要用）
// 不做双向通信，主实例处理完就关连接——这类需求不值得上 gRPC。
using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Serilog;

namespace Starward.Features.SingleInstance;

internal static class SingleInstancePipe
{
    private const string ShowCommand = "show";
    private const string ExitCommand = "exit";
    private const string StartGameCommand = "startgame";

    private static string SocketPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "state", "lunarward", "lunarward.sock");

    /// <summary>
    /// 第二个实例调用：把意图转发给主实例。
    /// 返回 false 表示「没人接」——通常是上次异常退出留下的陈旧锁，调用方应据此自救（L-72）。
    /// </summary>
    public static bool NotifyPrimary(string[] args)
    {
        try
        {
            // 深链（lunarward://startgame?biz=…）先展开成内部命令行，再决定转发哪条指令。
            args = App.ExpandUrlArgument(args);
            string command = args.FirstOrDefault() switch
            {
                "exit" => ExitCommand,
                "startgame" when GetArgValue(args, "--biz") is string biz && !string.IsNullOrWhiteSpace(biz)
                    => $"{StartGameCommand}\t{biz}",
                _ => ShowCommand,
            };
            using var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            client.Connect(new UnixDomainSocketEndPoint(SocketPath));
            client.Send(Encoding.UTF8.GetBytes(command));
            client.Shutdown(SocketShutdown.Send);
            return true;
        }
        catch (Exception ex)
        {
            // 主实例可能正在退出，socket 还没建好或已经没了；这类竞态不值得打扰用户。
            Log.Debug(ex, "Notify primary instance failed");
            return false;
        }
    }


    /// <summary>清掉残留的 socket 文件（陈旧锁自救用）。</summary>
    public static void RemoveStaleSocket()
    {
        try
        {
            if (File.Exists(SocketPath))
            {
                File.Delete(SocketPath);
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Remove stale socket failed");
        }
    }

    private static string? GetArgValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }


    /// <summary>主实例调用：监听并处理 show/exit/startgame。监听失败只降级为「唤起无效」，不影响启动。</summary>
    public static void StartListening(Action onShow, Action onExit, Action<string>? onStartGame = null)
    {
        try
        {
            string? dir = Path.GetDirectoryName(SocketPath);
            if (dir is not null)
            {
                Directory.CreateDirectory(dir);
            }
            if (File.Exists(SocketPath))
            {
                File.Delete(SocketPath);
            }
            var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(SocketPath));
            listener.Listen(4);
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        using Socket client = await listener.AcceptAsync();
                        var buffer = new byte[256];
                        int read = await client.ReceiveAsync(buffer, SocketFlags.None);
                        string command = Encoding.UTF8.GetString(buffer, 0, read).Trim();
                        if (command == ExitCommand)
                        {
                            onExit();
                        }
                        else if (command.StartsWith(StartGameCommand + "\t", StringComparison.Ordinal) && onStartGame is not null)
                        {
                            onStartGame(command[(StartGameCommand.Length + 1)..].Trim());
                        }
                        else
                        {
                            onShow();
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Debug(ex, "Single instance listener loop error");
                        await Task.Delay(200);
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Single instance socket unavailable");
        }
    }
}
