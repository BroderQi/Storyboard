using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Domain.Entities;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Generation;

/// <summary>
/// 视频生成 ViewModel - 负责视频生成
/// </summary>
public partial class VideoGenerationViewModel : ObservableObject
{
    private readonly IVideoGenerationService _videoGenerationService;
    private readonly IJobQueueService _jobQueue;
    private readonly IMessenger _messenger;
    private readonly ILogger<VideoGenerationViewModel> _logger;

    [ObservableProperty]
    private int _generatedVideosCount;

    public VideoGenerationViewModel(
        IVideoGenerationService videoGenerationService,
        IJobQueueService jobQueue,
        IMessenger messenger,
        ILogger<VideoGenerationViewModel> logger)
    {
        _videoGenerationService = videoGenerationService;
        _jobQueue = jobQueue;
        _messenger = messenger;
        _logger = logger;

        // 订阅视频生成请求消息
        _messenger.Register<VideoGenerationRequestedMessage>(this, OnVideoGenerationRequested);
    }

    [RelayCommand]
    private async Task BatchGenerateVideos()
    {
        _logger.LogInformation("开始批量生成视频");
        // TODO: 实现批量视频生成逻辑
    }

    private async void OnVideoGenerationRequested(object recipient, VideoGenerationRequestedMessage message)
    {
        var shot = message.Shot;

        try
        {
            shot.IsVideoGenerating = true;

            var prompt = shot.VideoPrompt;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                _logger.LogWarning("视频提示词为空，无法生成视频: Shot {ShotNumber}", shot.ShotNumber);
                return;
            }

            // 构建完整提示词（包含专业参数）
            var fullPrompt = BuildVideoPrompt(shot, prompt);

            // 创建视频生成任务
            _jobQueue.Enqueue(
                GenerationJobType.Video,
                shot.ShotNumber,
                async (ct, progress) =>
                {
                    var videoPath = await _videoGenerationService.GenerateVideoAsync(
                        shot,
                        outputDirectory: null,
                        filePrefix: $"shot_{shot.ShotNumber}_video",
                        cancellationToken: ct);

                    if (!string.IsNullOrWhiteSpace(videoPath))
                    {
                        // 保存视频路径
                        shot.GeneratedVideoPath = videoPath;

                        // 添加到资产列表
                        var asset = new ShotAssetItem
                        {
                            FilePath = videoPath,
                            ThumbnailPath = videoPath, // TODO: 生成缩略图
                            Type = ShotAssetType.GeneratedVideo,
                            CreatedAt = DateTime.Now,
                            IsSelected = true
                        };

                        shot.VideoAssets.Add(asset);

                        GeneratedVideosCount++;

                        _messenger.Send(new VideoGenerationCompletedMessage(shot, true, videoPath));
                        _logger.LogInformation("视频生成成功: Shot {ShotNumber}", shot.ShotNumber);
                    }
                    else
                    {
                        _messenger.Send(new VideoGenerationCompletedMessage(shot, false, null));
                        _logger.LogWarning("视频生成失败: Shot {ShotNumber}",
                            shot.ShotNumber);
                    }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "视频生成异常: Shot {ShotNumber}", shot.ShotNumber);
            _messenger.Send(new VideoGenerationCompletedMessage(shot, false, null));
        }
        finally
        {
            shot.IsVideoGenerating = false;
        }
    }

    private string BuildVideoPrompt(ShotItem shot, string basePrompt)
    {
        var parts = new List<string> { basePrompt };

        if (!string.IsNullOrWhiteSpace(shot.CameraMovement))
            parts.Add(shot.CameraMovement);
        if (!string.IsNullOrWhiteSpace(shot.ShootingStyle))
            parts.Add(shot.ShootingStyle);
        if (!string.IsNullOrWhiteSpace(shot.VideoEffect))
            parts.Add(shot.VideoEffect);

        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
