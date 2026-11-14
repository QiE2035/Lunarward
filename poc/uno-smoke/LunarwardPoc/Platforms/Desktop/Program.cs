using Uno.UI.Hosting;

namespace LunarwardPoc;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.InitializeLogging();

        // B 方案 spike：--gtkweb 时在同一进程里额外开一个 Gtk4 + WebKitGTK 窗口
        if (Array.Exists(args, a => a == "--gtkweb"))
        {
            GtkWebProbe.Start("https://example.com", seconds: 90);
        }

        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseX11()
            .UseLinuxFrameBuffer()
            .UseMacOS()
            .UseWin32()
            .Build();

        host.Run();
    }
}
