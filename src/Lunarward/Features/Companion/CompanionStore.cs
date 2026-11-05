// Lunarward：后启动程序的持久化。每个游戏一份 JSON，放在 XDG 配置目录下。
using Starward.Helpers;
using Starward.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Starward.Features.Companion;

public static class CompanionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private static readonly Dictionary<string, List<CompanionEntry>> _cache = new();

    public static string Folder
    {
        get
        {
            string baseDir = System.Environment.GetEnvironmentVariable("XDG_CONFIG_HOME")
                ?? System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), ".config");
            return System.IO.Path.Combine(baseDir, "lunarward", "companions");
        }
    }

    private static string FileOf(GameBiz biz) => System.IO.Path.Combine(Folder, $"{biz.Value}.json");

    public static IReadOnlyList<CompanionEntry> Load(GameBiz biz)
    {
        if (_cache.TryGetValue(biz.Value, out var cached))
        {
            return cached;
        }
        List<CompanionEntry> list = new();
        try
        {
            string file = FileOf(biz);
            if (File.Exists(file))
            {
                list = JsonSerializer.Deserialize<List<CompanionEntry>>(File.ReadAllText(file), JsonOptions) ?? new();
            }
        }
        catch
        {
            list = new();
        }
        _cache[biz.Value] = list;
        return list;
    }

    public static void Save(GameBiz biz, IEnumerable<CompanionEntry> entries)
    {
        List<CompanionEntry> list = new(entries);
        _cache[biz.Value] = list;
        Directory.CreateDirectory(Folder);
        JsonFile.WriteAtomic(FileOf(biz), list, JsonOptions);
    }

    public static void Upsert(GameBiz biz, CompanionEntry entry)
    {
        List<CompanionEntry> list = new(Load(biz));
        int index = list.FindIndex(x => x.Id == entry.Id);
        if (index >= 0)
        {
            list[index] = entry;
        }
        else
        {
            list.Add(entry);
        }
        Save(biz, list);
    }

    public static void Remove(GameBiz biz, string id)
    {
        List<CompanionEntry> list = new(Load(biz));
        list.RemoveAll(x => x.Id == id);
        Save(biz, list);
    }

    public static void Invalidate(GameBiz biz) => _cache.Remove(biz.Value);
}
