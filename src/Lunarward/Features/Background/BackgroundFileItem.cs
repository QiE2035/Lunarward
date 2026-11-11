using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.IO;

namespace Starward.Features.Background;

public partial class BackgroundFileItem : ObservableObject
{

    public string FilePath { get; set; }

    public string FileInfo { get; set; }

    public long FileSize { get; set; }

    public DateTime CreationTime { get; set; }

    public bool IsVideo { get; set; }


    /// <summary>视频条目的首帧 png（由 VideoThumbnailer 生成，可能为空）。</summary>
    public string? ThumbnailPath
    {
        get => field; set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(DisplayPath));
            }
        }
    }


    /// <summary>图库实际加载的地址：视频用首帧，其余用原文件。</summary>
    public string DisplayPath => ThumbnailPath ?? FilePath;


    public BackgroundFileItem(string file)
    {
        FilePath = file;
        var info = new FileInfo(file);
        FileSize = info.Length;
        CreationTime = info.LastWriteTime;
        FileInfo = GetFileInfo(info);
        IsVideo = BackgroundService.FileIsSupportedVideo(file);
    }


    private static string GetFileInfo(FileInfo info)
    {
        const double KB = 1 << 10, MB = 1 << 20;
        string ext = info.Extension.Replace(".", "").ToUpper();
        string size = info.Length >= MB ? $"{info.Length / MB:F2} MB" : $"{info.Length / KB:F2} KB";
        return $"{ext}  {size}".Trim();
    }


}

