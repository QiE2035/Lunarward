// Lunarward：`--install-run` / `--install-fetch` 的实现（Sophon 分块下载 + 文件级断点续传）。

using Hi3Helper.Sophon;
using Hi3Helper.Sophon.Structs;
using Starward.Core;
using Starward.Core.HoYoPlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Starward.Features.GameInstall.Sophon;


internal static class InstallDownloadRunner
{

    public static async Task RunAsync(GameBiz biz, string? onlyFile, bool preDownload, bool all, long limitMb, int parallel, string? targetDir, bool writeVersion, string? installDirOverride = null)
    {
        try
        {
            await AppConfig.CheckEnviromentAsync();
            string? installPath = installDirOverride ?? AppConfig.GetGameInstallPath(biz);
            if (installPath is null)
            {
                Console.WriteLine($"没有记录 {biz} 的安装路径，请先让启动器识别游戏。");
                return;
            }
            string outputRoot = targetDir ?? installPath;
            Directory.CreateDirectory(outputRoot);

            GameId gameId = GameId.FromGameBiz(biz);
            var hoYoPlayClient = AppConfig.GetService<HoYoPlayClient>();
            string launcherId = LauncherId.FromGameId(gameId)!;
            GameBranch? branch = await hoYoPlayClient.GetGameBranchAsync(launcherId, "zh-cn", gameId);
            GameBranchPackage? package = preDownload ? branch?.PreDownload : branch?.Main;
            if (package is null)
            {
                Console.WriteLine("取不到版本信息（网络或接口异常）。");
                return;
            }

            var fields = new List<string> { "game" };
            fields.AddRange(SophonClient.DetectInstalledAudioFields(installPath));

            using var httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            long budget = all ? long.MaxValue : limitMb * 1024L * 1024L;
            Console.WriteLine($"开始下载到 {outputRoot}（并行 {parallel}{(all ? "" : $"，上限 {limitMb} MB")}）");
            Action<InstallProgress> report = progress => Console.Write($"\r{progress.FinishedFiles}/{progress.TotalFiles} 文件，{InstallPlanner.FormatSize(progress.FinishedBytes)} / {InstallPlanner.FormatSize(progress.TotalBytes)}   ");
            InstallPipelineResult result;
            SophonBuildInfo? build = null;
            try
            {
                build = await new SophonClient(hoYoPlayClient).GetBuildInfoAsync(gameId, preDownload, fields);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"取 chunk 清单失败，退回整文件下载：{ex.Message}");
            }
            if (build is not null)
            {
                // 可续传路径：中断后重跑会接着已完成的块继续。
                result = await InstallPipeline.RunResumableAsync(httpClient, build, fields, outputRoot, budget, onlyFile, report, isPaused: null);
            }
            else
            {
                result = await InstallPipeline.RunAsync(httpClient, package, fields, outputRoot, preDownload, budget, parallel, onlyFile, report, isPaused: null);
            }
            Console.WriteLine();
            Console.WriteLine($"完成：成功 {result.Downloaded}，跳过 {result.Skipped}，失败 {result.Failed}");
            foreach (string error in result.Errors)
            {
                Console.WriteLine($"  ERR {error}");
            }
            int failed = result.Failed;
            bool inPlace =  string.Equals(Path.GetFullPath(outputRoot), Path.GetFullPath(installPath), StringComparison.Ordinal);
            if (result.LimitReached)
            {
                // 限额只是「跑一段就停」，绝不能被当成装完（L-66）。
                Console.WriteLine("版本标记未写入（已按 --limit-mb 停止，安装未完成）");
            }
            else if (failed == 0 && onlyFile is null && (inPlace || writeVersion))
            {
                // 文件落齐之后才写版本标记：写着「最新」但文件没下全，会让修复流程误判。
                List<string> written = GameVersionMarker.WriteAll(installPath, package.Tag);
                if (written.Count == 0)
                {
                    Console.Error.WriteLine($"版本标记未写入：{installPath} 里没有 config.ini（目录可能不是游戏目录，或是全新安装）");
                }
                else
                {
                    Console.WriteLine($"版本标记：{string.Join("，", written)}");
                }
            }
            else if (onlyFile is null)
            {
                Console.WriteLine($"版本标记未写入（失败 {failed}，输出目录{(inPlace ? "即安装目录" : "不是安装目录")}）");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"下载失败：{ex}");
        }
    }

}
