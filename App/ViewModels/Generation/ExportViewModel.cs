using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Generation;

/// <summary>
/// 导出 ViewModel - 负责最终视频合成和导出
/// </summary>
public partial class ExportViewModel : ObservableObject
{
    private readonly IFinalRenderService _finalRenderService;
    private readonly IJobQueueService _jobQueue;
    private readonly IMessenger _messenger;
    private readonly ILogger<ExportViewModel> _logger;

    [ObservableProperty]
    private bool _isExportDialogOpen;

    [ObservableProperty]
    private bool _canExportVideo;

    public ExportViewModel(
        IFinalRenderService finalRenderService,
        IJobQueueService jobQueue,
        IMessenger messenger,
        ILogger<ExportViewModel> logger)
    {
        _finalRenderService = finalRenderService;
        _jobQueue = jobQueue;
        _messenger = messenger;
        _logger = logger;

        // 订阅镜头变更消息以更新导出状态
        _messenger.Register<ShotAddedMessage>(this, (r, m) => UpdateCanExportVideo());
        _messenger.Register<ShotDeletedMessage>(this, (r, m) => UpdateCanExportVideo());
        _messenger.Register<VideoGenerationCompletedMessage>(this, (r, m) => UpdateCanExportVideo());
    }

    [RelayCommand]
    private void ShowExportDialog()
    {
        IsExportDialogOpen = true;
    }

    [RelayCommand]
    private async Task ExportVideo(string? outputPath)
    {
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            _logger.LogWarning("导出路径为空");
            return;
        }

        try
        {
            _logger.LogInformation("开始导出视频到: {OutputPath}", outputPath);

            // 查询所有镜头
            var query = new GetAllShotsQuery();
            _messenger.Send(query);
            var shots = query.Shots;

            if (shots == null || shots.Count == 0)
            {
                _logger.LogWarning("没有镜头可导出");
                _messenger.Send(new ExportCompletedMessage(false, null));
                return;
            }

            // 获取所有已完成的镜头视频路径（跳过未完成的）
            var videoClips = shots
                .OrderBy(s => s.ShotNumber)
                .Select(s => s.GeneratedVideoPath)
                .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                .ToList();

            if (videoClips.Count == 0)
            {
                _logger.LogWarning("没有已生成的视频可导出");
                _messenger.Send(new ExportCompletedMessage(false, null));
                return;
            }

            // 记录导出信息
            var completedCount = videoClips.Count;
            var totalCount = shots.Count;
            if (completedCount < totalCount)
            {
                _logger.LogInformation("导出已完成的分镜: {Completed}/{Total}，跳过 {Skipped} 个未完成的分镜",
                    completedCount, totalCount, totalCount - completedCount);
            }
            else
            {
                _logger.LogInformation("导出所有分镜: {Total}/{Total}", totalCount);
            }

            // 创建导出任务
            _jobQueue.Enqueue(
                GenerationJobType.FullRender,
                0,
                async (ct, progress) =>
                {
                    try
                    {
                        var resultPath = await _finalRenderService.RenderAsync(
                            videoClips!,
                            ct,
                            progress);

                        if (!string.IsNullOrWhiteSpace(resultPath))
                        {
                            // 复制到目标路径
                            var dir = Path.GetDirectoryName(outputPath);
                            if (!string.IsNullOrWhiteSpace(dir))
                                Directory.CreateDirectory(dir);

                            File.Copy(resultPath, outputPath, overwrite: true);

                            _messenger.Send(new ExportCompletedMessage(true, outputPath));
                            _logger.LogInformation("视频导出成功: {OutputPath}", outputPath);

                            // 打开文件所在文件夹
                            try
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = "explorer.exe",
                                    Arguments = $"/select,\"{outputPath}\"",
                                    UseShellExecute = true
                                });
                            }
                            catch { }
                        }
                        else
                        {
                            _messenger.Send(new ExportCompletedMessage(false, null));
                            _logger.LogWarning("视频导出失败: 返回路径为空");
                        }
                    }
                    catch (Exception ex)
                    {
                        _messenger.Send(new ExportCompletedMessage(false, null));
                        _logger.LogError(ex, "视频导出失败");
                        throw;
                    }
                });

            IsExportDialogOpen = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "视频导出异常");
            _messenger.Send(new ExportCompletedMessage(false, null));
        }
    }

    private void UpdateCanExportVideo()
    {
        // 查询所有镜头
        var query = new GetAllShotsQuery();
        _messenger.Send(query);
        var shots = query.Shots;

        if (shots == null || shots.Count == 0)
        {
            CanExportVideo = false;
            return;
        }

        // 只要有至少一个镜头有生成的视频就可以导出
        var hasAnyVideo = shots.Any(s =>
            !string.IsNullOrWhiteSpace(s.GeneratedVideoPath) &&
            File.Exists(s.GeneratedVideoPath));

        CanExportVideo = hasAnyVideo;
    }
}
