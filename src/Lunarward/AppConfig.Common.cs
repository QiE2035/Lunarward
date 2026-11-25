using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Timers;

namespace Starward;

public static partial class AppConfig
{

    public static readonly JsonSerializerOptions JsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string StarwardExecutePath => Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "Starward.exe");

    public static Guid DeviceId { get; private set; }

    public static Guid SessionId { get; private set; }


    static AppConfig()
    {
        // Lunarward：Linux 没有注册表，设备标识改用 DMI（BIOS 版本）+ /etc/machine-id（等价 MachineGuid）。
        DeviceId = new(MD5.HashData(Encoding.UTF8.GetBytes($"{ReadFirstLine("/sys/class/dmi/id/bios_version")}{ReadMachineId()}{Environment.MachineName}")));
        SessionId = Guid.CreateVersion7();
    }


    private static Timer? _gcTimer;


    public static void StartGCTimer()
    {
        if (_gcTimer is null)
        {
            _gcTimer = new(TimeSpan.FromSeconds(60));
            _gcTimer.Elapsed += (_, _) => GC.Collect();
        }
        _gcTimer.Start();
    }



    #region Emoji


    public static Uri EmojiPaimon = new Uri("ms-appx:///Assets/Image/UI_EmotionIcon5.png");

    public static Uri EmojiPom = new Uri("ms-appx:///Assets/Image/20008.png");

    public static Uri EmojiAI = new Uri("ms-appx:///Assets/Image/bdfd19c3bdad27a395890755bb60b162.png");

    public static Uri EmojiBangboo = new Uri("ms-appx:///Assets/Image/pamu.db6c2c7b.png");


    #endregion



}


// LUNARWARD-DEVIATION: 上游用注册表 MachineGuid + WMI 固件表拼设备 ID（AppConfig.Common 的 Windows 实现），
// Linux 侧改为 DMI BIOS 版本 + /etc/machine-id（D128）。
public static partial class AppConfig
{

    private static string ReadFirstLine(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using var reader = new StreamReader(path);
                return (reader.ReadLine() ?? "").Trim();
            }
        }
        catch { }
        return "";
    }


    private static string ReadMachineId()
    {
        string machineId = ReadFirstLine("/etc/machine-id");
        if (string.IsNullOrWhiteSpace(machineId))
        {
            machineId = ReadFirstLine("/var/lib/dbus/machine-id");
        }
        return machineId;
    }

}
