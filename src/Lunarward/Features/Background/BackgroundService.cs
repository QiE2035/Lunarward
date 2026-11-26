using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Starward.Core.HoYoPlay;
using Starward.GameInstall;
using Starward.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.Background;

public class BackgroundService
{

    private readonly ILogger<BackgroundService> _logger;

    private readonly HoYoPlayService _hoYoPlayService;

    private readonly HttpClient _httpClient;



    public BackgroundService(ILogger<BackgroundService> logger, HoYoPlayService hoYoPlayService, HttpClient httpClient)
    {
        _logger = logger;
        _hoYoPlayService = hoYoPlayService;
        _httpClient = httpClient;
    }



    /// <summary>
    /// 获取自定义背景图文件路径
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    public static bool TryGetCustomBgFilePath(GameId gameId, [NotNullWhen(true)] out string? path)
    {
        path = null;
        if (gameId is null)
        {
            return false;
        }
        path = Path.Join(AppConfig.UserDataFolder, "bg", AppConfig.GetCustomBg(gameId));
        return File.Exists(path);
    }


    /// <summary>
    /// 文件是否是支持的视频格式，支持 mp4、mkv、flv、webm
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public static bool FileIsSupportedVideo(string? name)
    {
        return Path.GetExtension(name) switch
        {
            ".mp4" or ".mkv" or ".webm" => true,
            _ => false,
        };
    }


    /// <summary>
    /// 获取已缓存的背景图文件路径
    /// </summary>
    /// <param name="gameId"></param>
    /// <returns></returns>
    public static string? GetLastAppBackgroundFile(GameId gameId)
    {
        string? name = AppConfig.GetBg(gameId);
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }
        string? path = Path.Join(AppConfig.CacheFolder, "bg", name);
        if (File.Exists(path))
        {
            return path;
        }
        path = Path.Join(AppConfig.UserDataFolder, "bg", name);
        if (File.Exists(path))
        {
            return path;
        }
        return null;
    }



    /// <summary>
    /// 游戏背景图
    /// </summary>
    /// <param name="gameId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<(List<GameBackgroundModel> List, GameBackgroundModel? Selected)> GetGameBackgroundsAsync(GameId gameId, CancellationToken cancellationToken = default)
    {
        GameBackgroundInfo? backgroundInfo = await _hoYoPlayService.GetGameBackgroundInfoAsync(gameId, cancellationToken);
        List<GameBackground> backgrounds = backgroundInfo?.Backgrounds?.ToList() ?? [];
        if (await _hoYoPlayService.GetGameInfoAsync(gameId, cancellationToken) is GameInfo gameInfo)
        {
            backgrounds.Add(new GameBackground
            {
                Id = gameInfo.Id,
                Background = new GameImage { Url = gameInfo.Display.Background?.Url! },
                Icon = new(),
                Video = new(),
                Theme = new(),
                Type = GameBackground.BACKGROUND_TYPE_UNSPECIFIED,
            });
        }
        List<GameBackgroundModel> models = backgrounds.Select(x => new GameBackgroundModel(x)).ToList();
        if (models.Count is 0)
        {
            return (models, null);
        }

        // 这个设置的格式为：$"{index};{id1},{type1};{id2},{type2}"
        string? last = AppConfig.GetGameBackground(gameId);
        if (string.IsNullOrWhiteSpace(last))
        {
            return (models, models?.FirstOrDefault());
        }

        int index = 0;
        int i = last.IndexOf(';');
        if (i >= 0)
        {
            _ = int.TryParse(last.AsSpan().Slice(0, i), out index);
        }

        List<(string Id, int Type)> lastIds = new();
        foreach (Match match in Regex.Matches(last, @"([0-9A-Za-z]+),(\d)"))
        {
            string id = match.Groups[1].Value;
            int.TryParse(match.Groups[2].Value, out int type);
            lastIds.Add((id, type));
        }

        foreach (var model in models)
        {
            if (lastIds.FirstOrDefault(x => x.Id == model.Id) is (string id, int type))
            {
                model.Last = true;
                model.Type = Math.Clamp(type, 0, 1);
            }
        }

        if (models.FirstOrDefault(x => !x.Last) is GameBackgroundModel model1)
        {
            return (models, model1);
        }
        else if (models.Count > index)
        {
            return (models, models[index]);
        }
        else
        {
            return (models, models.FirstOrDefault());
        }
    }




    public async Task<string> GetBackgroundFileAsync(string url, CancellationToken cancellationToken = default)
    {
        string file = Path.Combine(AppConfig.CacheFolder, "bg", Path.GetFileName(url));
        if (!File.Exists(file))
        {
            Directory.CreateDirectory(Path.Combine(AppConfig.CacheFolder, "bg"));
            string tmp = file + ".tmp";
            using var fs = File.Open(tmp, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite);
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new RangeHeaderValue(fs.Length, null);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            fs.Position = response.Content.Headers.ContentRange?.From ?? 0;
            using var hs = await response.Content.ReadAsStreamAsync(cancellationToken);
            await hs.CopyToAsync(fs, cancellationToken);
            fs.Dispose();
            File.Move(tmp, file, true);
        }
        return file;
    }




    /// <summary>
    /// 获取默认的背景图文件路径
    /// </summary>
    /// <returns></returns>
    public static string? GetFallbackBackgroundImage()
    {
        string baseFolder = AppContext.BaseDirectory;
        // LUNARWARD-DEVIATION: 上游写死的是 Windows 反斜杠路径 @"Assets\Image\UI_CutScene_1130320101A.png"，
        // Linux 下 Path 分隔符是 '/'，原样拼接 File.Exists 恒为 false（壁纸永远回退不到兜底图），改用 Path.Combine。
        string path = Path.Combine(baseFolder, "Assets", "Image", "UI_CutScene_1130320101A.png");
        return File.Exists(path) ? path : null;
    }



    #region Pick Custom Background


    /// <summary>
    /// 更改自定义背景图文件，保存在 UserData\bg，返回文件名
    /// </summary>
    /// <param name="xamlRoot"></param>
    /// <returns></returns>
    public async Task<string?> ChangeCustomBackgroundFileAsync(XamlRoot xamlRoot)
    {
        string? file = await PickBackgroundFileAsync(xamlRoot);
        if (file is null)
        {
            return null;
        }
        await CheckBackgroundFileAvailableAsync(file);
        string bg = Path.Join(AppConfig.UserDataFolder, "bg");
        Directory.CreateDirectory(bg);
        string name = Path.GetFileName(file);
        string path = Path.Combine(bg, name);
        if (path != file)
        {
            File.Copy(file, path, true);
        }
        return name;
    }



    /// <summary>
    /// 更改自定义背景图文件，保存在 UserData\bg，返回文件名
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    public static async Task<string?> ChangeCustomBackgroundFileAsync(string? file)
    {
        if (string.IsNullOrWhiteSpace(file))
        {
            return null;
        }
        string bg = Path.Join(AppConfig.UserDataFolder, "bg");
        string name = Path.GetFileName(file);
        // LUNARWARD-DEVIATION: 上游这个重载收 StorageFile（走 MediaSource/BitmapDecoder 判定视频与可解码性），
        // Uno/Skia 没有 WinRT StorageFile，移植侧收本地路径，并用 System.IO + ImageLoader 做同样的校验。
        await CheckBackgroundFileAvailableAsync(file);
        if (Path.GetDirectoryName(Path.GetFullPath(file)) != Path.GetFullPath(bg))
        {
            Directory.CreateDirectory(bg);
            string path = Path.Combine(bg, name);
            File.Copy(file, path, true);
        }
        return name;
    }


    /// <summary>
    /// 选择背景图文件
    /// </summary>
    /// <param name="xamlRoot"></param>
    /// <returns></returns>
    private async Task<string?> PickBackgroundFileAsync(XamlRoot xamlRoot)
    {
        var filter = new (string, string)[]
            {
                ("Image", ".bmp"),
                ("Image", ".jpg"),
                ("Image", ".png"),
                ("Image", ".webp"),
                ("Image", ".avif"),
                ("Image", ".jxl"),
                ("Video", ".mp4"),
                ("Video", ".mkv"),
                ("Video", ".webm"),
            };
        return await FileDialogHelper.PickSingleFileAsync(xamlRoot, filter);
    }



    /// <summary>
    /// 检查背景图文件是否可用
    /// </summary>
    /// <param name="file"></param>
    /// <returns></returns>
    private static async Task CheckBackgroundFileAvailableAsync(string file)
    {
        if (!File.Exists(file))
        {
            throw new FileNotFoundException("background file not found", file);
        }
        var info = new FileInfo(file);
        if (info.Length <= 0)
        {
            throw new InvalidDataException("background file is empty");
        }
        // B54：图片必须真的解得开才允许落盘，否则壁纸变黑且已持久化。
        // GetImagePixelSizeAsync 自己就在后台线程解码（SKCodec），这里直接 await，
        // 不要再套 Task.Run + GetAwaiter().GetResult() 的嵌套同步等待。
        string extension = Path.GetExtension(file).ToLowerInvariant();
        if (extension is ".mp4" or ".mkv" or ".webm" or ".mov" or ".avi")
        {
            // D134：上游对视频也是真校验（MediaSource.OpenAsync 打不开就抛），以前这里只看扩展名，
            // 坏视频会被照常复制进 bg/ 并持久化。libvlc 打开一次就是起一个播放器实例，不适合放在选文件路径上，
            // 所以退一步只校验容器魔数——能挡掉改后缀的图片、空文件、截断文件里的大多数。
            if (!LooksLikeVideoContainer(file))
            {
                throw new InvalidDataException("background file is not a playable video container");
            }
        }
        else
        {
            var (width, height) = await Starward.Features.Codec.ImageLoader.GetImagePixelSizeAsync(file);
            if (width == 0 || height == 0)
            {
                throw new InvalidDataException("background image cannot be decoded");
            }
        }
    }


    /// <summary>
    /// D134：按容器头判断「这确实是视频」。只做魔数，不做解码（解码要起 libvlc 实例，代价不适合放这里）。
    /// </summary>
    private static bool LooksLikeVideoContainer(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            Span<byte> head = stackalloc byte[12];
            int read = stream.ReadAtLeast(head, head.Length, throwOnEndOfStream: false);
            if (read < 12)
            {
                return false;
            }
            // Matroska / WebM：EBML 头 1A 45 DF A3
            if (head[0] == 0x1A && head[1] == 0x45 && head[2] == 0xDF && head[3] == 0xA3)
            {
                return true;
            }
            // MP4 / MOV：第 4 字节起是 "ftyp"
            if (head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70)
            {
                return true;
            }
            // AVI："RIFF" + 8 字节后 "AVI "
            if (head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46
                && head[8] == 0x41 && head[9] == 0x56 && head[10] == 0x49 && head[11] == 0x20)
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "read video container header failed: {file}", file);
            return false;
        }
    }


    #endregion


}
