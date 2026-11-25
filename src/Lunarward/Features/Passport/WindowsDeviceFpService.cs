// LUNARWARD-DEVIATION: 上游同名文件用注册表 + DXGI + SMBIOS 固件表取设备信息（Windows 专属）。
// Linux 侧改从 /proc、/sys、/etc/machine-id 取同样的字段，SDK 缓存值落到数据目录的 JSON 文件。
// 类名沿用上游的 WindowsDeviceFpService，避免调用点（MainView / HypLoginWindow / DI 注册）分叉。D128
using Microsoft.Extensions.Logging;
using Starward.Core;
using Starward.Core.DeviceFp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Passport;

internal partial class WindowsDeviceFpService
{

    /// <summary>
    /// SDK 缓存文件（对应上游的 HKEY_CURRENT_USER\Software\miHoYoSDK）
    /// </summary>
    private static readonly string SdkStorePath = Path.Combine(AppConfig.CacheFolder, "device-sdk.json");

    private static readonly object SdkStoreLock = new();

    private readonly DeviceFpClient _client;

    private readonly ILogger<WindowsDeviceFpService> _logger = AppConfig.GetLogger<WindowsDeviceFpService>();


    public WindowsDeviceFpService(DeviceFpClient deviceFpClient)
    {
        _client = deviceFpClient;
    }



    /// <summary>
    /// 初始化设备信息
    /// </summary>
    public async Task InitializeDeviceFpAsync(ServerRegion region, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(GetSdkValue("MIHOYOSDK_DEVICE_FP")))
        {
            var extList = await _client.GetExtListAsync(region, DevicePlatform.PC, cancellationToken).ConfigureAwait(false);
            var request = CreateRequest(extList);
            request.AppName = region switch
            {
                ServerRegion.miHoYo => "HYP_cn",
                ServerRegion.HoYoverse => "HYP_global",
                _ => throw new NotSupportedException($"Region '{region}' is not supported.")
            };
            var result = await _client.GetDeviceFpAsync(region, request, cancellationToken).ConfigureAwait(false);
            SaveSdkValueIfMissing("MIHOYOSDK_DEVICE_FP", result.DeviceFp);
        }
    }



    /// <summary>
    /// 登录窗口的 deviceId。与 <see cref="CreateRequest"/> 的取值一致，取缓存，没有就按 HYP 的算法生成。
    /// </summary>
    public string GetDeviceId()
    {
        return GetSdkValue("MIHOYOSDK_DEVICE_ID") ?? GenerateDeviceId();
    }


    /// <summary>
    /// 登录窗口的 deviceFp。取缓存，没有就用占位值。
    /// </summary>
    public string GetDeviceFp()
    {
        return GetSdkValue("MIHOYOSDK_DEVICE_FP") ?? RandomNumberGenerator.GetString("0123456789", 10);
    }


    /// <summary>
    /// 构造 getFp 请求体：device_id / seed / device_fp 取缓存或默认值，
    /// </summary>
    public DeviceFpRequest CreateRequest(DeviceExtListResult extList)
    {
        var request = new DeviceFpRequest
        {
            DeviceId = GetSdkValue("MIHOYOSDK_DEVICE_ID") ?? GenerateDeviceId(),
            SeedId = GetSdkValue("MIHOYOSDK_SEED_ID") ?? RandomNumberGenerator.GetHexString(16, true),
            SeedTime = GetSdkValue("MIHOYOSDK_SEED_TIME") ?? DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString(),
            Platform = "3",
            DeviceFp = GetSdkValue("MIHOYOSDK_DEVICE_FP") ?? RandomNumberGenerator.GetString("0123456789", 10),
            ExtFields = JsonSerializer.Serialize(CollectExtFields(extList.ExtList ?? [])),
        };

        // 和 SDK 一样，只在值不存在时回写，避免覆盖启动器/游戏已经写好的数据
        SaveSdkValueIfMissing("MIHOYOSDK_SEED_ID", request.SeedId);
        SaveSdkValueIfMissing("MIHOYOSDK_SEED_TIME", request.SeedTime);
        SaveSdkValueIfMissing("MIHOYOSDK_DEVICE_UID", GetExtFieldDeviceUid());

        return request;
    }


    private Dictionary<string, string> CollectExtFields(IEnumerable<string> names)
    {
        var gpu = GetGpuInfo();
        var memory = GetMemoryInfo();
        var rom = GetRomInfo();
        var fields = new Dictionary<string, string>();
        foreach (string name in names)
        {
            fields[name] = name switch
            {
                "osVersion" => GetOsVersion(),
                "cpuName" => ReadCpuInfo("model name") ?? "unknown",
                "cpuCores" => Environment.ProcessorCount.ToString(),
                "cpuFrequency" => ReadCpuInfo("cpu MHz") ?? "unknown",
                "cpuType" => ReadFile("/sys/devices/system/cpu/cpu0/cpufreq/scaling_driver") ?? "unknown",
                "deviceModel" => ReadFile("/sys/class/dmi/id/board_name") ?? ReadFile("/sys/class/dmi/id/product_name") ?? "unknown",
                "deviceName" or "systemName" => Environment.MachineName,
                "deviceType" => "Desktop",
                "deviceUID" => GetExtFieldDeviceUid(),
                "gpuName" => gpu.Name,
                "gpuVendor" => gpu.Vendor,
                "gpuVendorID" => gpu.VendorId,
                "gpuID" => gpu.DeviceId,
                "gpuMemory" => gpu.Memory,
                "gpuVersion" => gpu.Version,
                "gpuAPI" => gpu.Api,
                "isGpuMultiTread" => "false",
                "memorySize" or "ramCapacity" => memory.Total,
                "ramRemain" => memory.Available,
                "romCapacity" => rom.Total,
                "romRemain" => rom.Available,
                "screenSize" => GetScreenSize(),
                "addressMAC" => GetMacAddress(),
                "packageVersion" => AppConfig.AppVersion,
                // 其余是移动端/浏览器字段
                _ => "unknown",
            };
        }
        return fields;
    }


    private string GetExtFieldDeviceUid()
    {
        return GetSdkValue("MIHOYOSDK_DEVICE_UID") ?? GetMachineGuid();
    }


    private static string? GetSdkValue(string name)
    {
        try
        {
            lock (SdkStoreLock)
            {
                if (ReadStore() is { } store && store.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
        }
        catch (Exception)
        {
        }
        return null;
    }


    private static void SaveSdkValueIfMissing(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || GetSdkValue(name) is not null)
        {
            return;
        }
        try
        {
            lock (SdkStoreLock)
            {
                var store = ReadStore() ?? new Dictionary<string, string>();
                store[name] = value;
                Directory.CreateDirectory(Path.GetDirectoryName(SdkStorePath)!);
                string text = JsonSerializer.Serialize(store, SdkJsonOptions);
                string temp = SdkStorePath + ".tmp";
                File.WriteAllText(temp, text);
                File.Move(temp, SdkStorePath, true);
            }
        }
        catch (Exception)
        {
        }
    }


    private static readonly JsonSerializerOptions SdkJsonOptions = new() { WriteIndented = true };


    private static Dictionary<string, string>? ReadStore()
    {
        if (!File.Exists(SdkStorePath))
        {
            return null;
        }
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SdkStorePath));
    }


    /// <summary>
    /// 系统唯一标识，对应 Windows 的 MachineGuid。
    /// </summary>
    private static string GetMachineGuid()
    {
        return ReadFile("/etc/machine-id") ?? ReadFile("/var/lib/dbus/machine-id") ?? "";
    }


    /// <summary>
    /// 生成设备号：<c>SHA1(主板序列号 + BIOS 序列号 + machine-id)</c> 的 40 位小写十六进制，再拼接当前毫秒。
    /// <para>
    /// 序列号取 SMBIOS 的 /sys/class/dmi/id 下的 board_serial 与 product_serial，与 Windows 侧
    /// Win32_BaseBoard / Win32_BIOS 的 SerialNumber 对应。任一序列号缺失时退化成 machine-id。
    /// </para>
    /// </summary>
    private static string GenerateDeviceId()
    {
        string boardSerial = ReadFile("/sys/class/dmi/id/board_serial") ?? "";
        string biosSerial = ReadFile("/sys/class/dmi/id/product_serial") ?? "";
        string productId = GetMachineGuid();

        string raw;
        if (string.IsNullOrEmpty(boardSerial) || string.IsNullOrEmpty(biosSerial))
        {
            raw = productId;
        }
        else
        {
            raw = boardSerial + biosSerial + productId;
        }

        byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant() + DateTimeOffset.Now.ToUnixTimeMilliseconds();
    }


    private static string GetOsVersion()
    {
        string? prettyName = null;
        string? osRelease = ReadFile("/etc/os-release");
        if (osRelease is not null)
        {
            foreach (string line in osRelease.Split('\n'))
            {
                if (line.StartsWith("PRETTY_NAME=", StringComparison.Ordinal))
                {
                    prettyName = line["PRETTY_NAME=".Length..].Trim().Trim('"');
                    break;
                }
            }
        }
        return prettyName ?? $"Linux {Environment.OSVersion.Version}";
    }


    /// <summary>
    /// GPU 信息：/proc/driver/nvidia 能给 NVIDIA 的型号，其它厂商退回 PCI vendor/device id。
    /// </summary>
    private readonly record struct GpuInfo(string Name, string Vendor, string VendorId, string DeviceId, string Memory, string Version, string Api);


    private static GpuInfo GetGpuInfo()
    {
        string? vendorId = ReadFile("/sys/class/drm/card0/device/vendor");
        string? deviceId = ReadFile("/sys/class/drm/card0/device/device");
        uint vendor = ParseHexId(vendorId);
        var (name, memory) = GetGpuNameAndMemory();
        return new GpuInfo(
            name ?? "unknown",
            GetGpuVendorName(vendor),
            vendor == 0 ? "unknown" : vendor.ToString(),
            ParseHexId(deviceId) is uint device and not 0 ? device.ToString() : "unknown",
            memory ?? "unknown",
            "OpenGL",
            "OpenGL");
    }


    /// <summary>
    /// NVIDIA 专有驱动会在 /proc/driver/nvidia/gpus/&lt;pci&gt;/information 里写型号；
    /// 显存只有驱动暴露的 sysfs 节点（AMD）或 nvidia-smi（不 fork）可用，取不到就 unknown。
    /// </summary>
    private static (string? Name, string? Memory) GetGpuNameAndMemory()
    {
        try
        {
            string gpusRoot = "/proc/driver/nvidia/gpus";
            if (Directory.Exists(gpusRoot))
            {
                foreach (string dir in Directory.EnumerateDirectories(gpusRoot))
                {
                    string? info = ReadFile(Path.Combine(dir, "information"));
                    if (info is null)
                    {
                        continue;
                    }
                    string? model = null;
                    foreach (string line in info.Split('\n'))
                    {
                        if (line.StartsWith("Model:", StringComparison.Ordinal))
                        {
                            model = line["Model:".Length..].Trim();
                            break;
                        }
                    }
                    string? vram = ReadFile("/sys/class/drm/card0/device/mem_info_vram_total");
                    return (model, vram is null ? null : (long.Parse(vram) >> 20).ToString());
                }
            }
            string? amdMemory = ReadFile("/sys/class/drm/card0/device/mem_info_vram_total");
            if (amdMemory is not null)
            {
                return (null, (long.Parse(amdMemory) >> 20).ToString());
            }
        }
        catch (Exception)
        {
        }
        return (null, null);
    }


    private static uint ParseHexId(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }
        text = text.Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }
        return uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out uint value) ? value : 0;
    }


    /// <summary>
    /// PCI VendorId 到厂商名的映射，和 SDK 一致。
    /// </summary>
    private static string GetGpuVendorName(uint vendorId) => vendorId switch
    {
        0x10DE or 0x12D2 => "NVIDIA",
        0x1002 => "AMD",
        0x163C or 0x8086 => "Intel",
        _ => "unknown",
    };


    private static (string Total, string Available) GetMemoryInfo()
    {
        try
        {
            string? memInfo = ReadFile("/proc/meminfo");
            if (memInfo is not null)
            {
                string total = "unknown";
                string available = "unknown";
                foreach (string line in memInfo.Split('\n'))
                {
                    if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                    {
                        total = (ParseKilobytes(line) >> 10).ToString();
                    }
                    else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                    {
                        available = (ParseKilobytes(line) >> 10).ToString();
                    }
                }
                return (total, available);
            }
        }
        catch (Exception)
        {
        }
        return ("unknown", "unknown");
    }


    private static long ParseKilobytes(string line)
    {
        string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && long.TryParse(parts[1], out long kb) ? kb : 0;
    }


    private static (string Total, string Available) GetRomInfo()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(AppConfig.CacheFolder) ?? "/");
            return ((drive.TotalSize >> 20).ToString(), (drive.AvailableFreeSpace >> 20).ToString());
        }
        catch (Exception)
        {
        }
        return ("unknown", "unknown");
    }


    /// <summary>
    /// Uno 的 X11 后端没有 DisplayArea 这类查询，屏幕尺寸取 xrandr 的值会 fork 进程（硬规则 10），这里留 unknown。
    /// </summary>
    private static string GetScreenSize()
    {
        return "unknown";
    }


    private static string GetMacAddress()
    {
        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }
                byte[] bytes = networkInterface.GetPhysicalAddress().GetAddressBytes();
                if (bytes.Length == 6)
                {
                    return Convert.ToHexString(bytes);
                }
            }
        }
        catch { }
        return "FFFFFFFFFFFF";
    }


    /// <summary>
    /// 读 /proc 里 cpuinfo 的字段值（第一处出现）。
    /// </summary>
    private static string? ReadCpuInfo(string key)
    {
        string? cpuInfo = ReadFile("/proc/cpuinfo");
        if (cpuInfo is null)
        {
            return null;
        }
        foreach (string line in cpuInfo.Split('\n'))
        {
            if (line.StartsWith(key, StringComparison.Ordinal) && line.Length > key.Length && line[key.Length] == ':')
            {
                string value = line[(key.Length + 1)..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        return null;
    }


    private static string? ReadFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                string text = File.ReadAllText(path).Trim();
                return string.IsNullOrWhiteSpace(text) ? null : text;
            }
        }
        catch (Exception)
        {
        }
        return null;
    }


}
