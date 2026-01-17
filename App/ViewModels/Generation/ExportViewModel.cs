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

            // 创建导出任务
            _jobQueue.Enqueue(
                GenerationJobType.FullRender,
                0,
                async (ct, progress) =>
                {
                    // TODO: 获取所有镜头的视频路径
                    // 这需要从 ShotListViewModel 获取
                    var videoClips = new System.Collections.Generic.List<string>();

                    try
                    {
                        var resultPath = await _finalRenderService.RenderAsync(
                            videoClips,
                            ct,
                            progress);

                        if (!string.IsNullOrWhiteSpace(resultPath))
                        {
                            _messenger.Send(new ExportCompletedMessage(true, resultPath));
                            _logger.LogInformation("视频导出成功: {OutputPath}", resultPath);
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
        // TODO: 从 ShotListViewModel 获取镜头数据
        // 暂时设置为 false
        CanExportVideo = false;
    }
}
