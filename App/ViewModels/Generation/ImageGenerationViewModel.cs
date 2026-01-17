using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Domain.Entities;
using Storyboard.Messages;
using Storyboard.Models;
using Storyboard.Infrastructure.Media;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Generation;

/// <summary>
/// 图像生成 ViewModel - 负责首帧/尾帧生成
/// </summary>
public partial class ImageGenerationViewModel : ObservableObject
{
    private readonly IImageGenerationService _imageGenerationService;
    private readonly IJobQueueService _jobQueue;
    private readonly IMessenger _messenger;
    private readonly ILogger<ImageGenerationViewModel> _logger;

    [ObservableProperty]
    private int _generatedImagesCount;

    public ImageGenerationViewModel(
        IImageGenerationService imageGenerationService,
        IJobQueueService jobQueue,
        IMessenger messenger,
        ILogger<ImageGenerationViewModel> logger)
    {
        _imageGenerationService = imageGenerationService;
        _jobQueue = jobQueue;
        _messenger = messenger;
        _logger = logger;

        // 订阅图像生成请求消息
        _messenger.Register<ImageGenerationRequestedMessage>(this, OnImageGenerationRequested);
    }

    [RelayCommand]
    private async Task BatchGenerateImages()
    {
        _logger.LogInformation("开始批量生成图像");

        // 查询所有镜头
        var query = new GetAllShotsQuery();
        _messenger.Send(query);
        var shots = query.Shots;

        if (shots == null || shots.Count == 0)
        {
            _logger.LogWarning("没有镜头可生成图像");
            return;
        }

        var queuedCount = 0;
        foreach (var shot in shots)
        {
            // 生成首帧图像
            if (string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) && !shot.IsFirstFrameGenerating)
            {
                if (!string.IsNullOrWhiteSpace(shot.FirstFramePrompt))
                {
                    _messenger.Send(new ImageGenerationRequestedMessage(shot, true));
                    queuedCount++;
                }
            }

            // 生成尾帧图像
            if (string.IsNullOrWhiteSpace(shot.LastFrameImagePath) && !shot.IsLastFrameGenerating)
            {
                if (!string.IsNullOrWhiteSpace(shot.LastFramePrompt))
                {
                    _messenger.Send(new ImageGenerationRequestedMessage(shot, false));
                    queuedCount++;
                }
            }
        }

        _logger.LogInformation("批量生成图像: 已加入队列 {Count} 个任务", queuedCount);
    }

    private async void OnImageGenerationRequested(object recipient, ImageGenerationRequestedMessage message)
    {
        var shot = message.Shot;
        var isFirstFrame = message.IsFirstFrame;

        try
        {
            if (isFirstFrame)
                shot.IsFirstFrameGenerating = true;
            else
                shot.IsLastFrameGenerating = true;

            var prompt = isFirstFrame ? shot.FirstFramePrompt : shot.LastFramePrompt;
            if (string.IsNullOrWhiteSpace(prompt))
            {
                _logger.LogWarning("提示词为空，无法生成图像: Shot {ShotNumber}, IsFirstFrame: {IsFirstFrame}",
                    shot.ShotNumber, isFirstFrame);

                // 重置生成状态
                if (isFirstFrame)
                    shot.IsFirstFrameGenerating = false;
                else
                    shot.IsLastFrameGenerating = false;
                return;
            }

            // 创建图像生成任务
            _jobQueue.Enqueue(
                isFirstFrame ? GenerationJobType.ImageFirst : GenerationJobType.ImageLast,
                shot.ShotNumber,
                async (ct, progress) =>
                {
                    try
                    {
                        // 使用新的 GenerateFrameImageAsync 方法
                        var imagePath = await _imageGenerationService.GenerateFrameImageAsync(
                            shot,
                            isFirstFrame,
                            outputDirectory: null,
                            cancellationToken: ct);

                        if (!string.IsNullOrWhiteSpace(imagePath))
                        {
                            // 保存图像路径
                            if (isFirstFrame)
                                shot.FirstFrameImagePath = imagePath;
                            else
                                shot.LastFrameImagePath = imagePath;

                            // 添加到资产列表
                            var asset = new ShotAssetItem
                            {
                                FilePath = imagePath,
                                ThumbnailPath = imagePath,
                                Type = isFirstFrame ? ShotAssetType.FirstFrameImage : ShotAssetType.LastFrameImage,
                                CreatedAt = DateTime.Now,
                                IsSelected = true
                            };

                            if (isFirstFrame)
                                shot.FirstFrameAssets.Add(asset);
                            else
                                shot.LastFrameAssets.Add(asset);

                            GeneratedImagesCount++;

                            _messenger.Send(new ImageGenerationCompletedMessage(shot, isFirstFrame, true, imagePath));
                            _logger.LogInformation("图像生成成功: Shot {ShotNumber}, IsFirstFrame: {IsFirstFrame}",
                                shot.ShotNumber, isFirstFrame);
                        }
                        else
                        {
                            _messenger.Send(new ImageGenerationCompletedMessage(shot, isFirstFrame, false, null));
                            _logger.LogWarning("图像生成失败: Shot {ShotNumber}, IsFirstFrame: {IsFirstFrame}",
                                shot.ShotNumber, isFirstFrame);
                        }
                    }
                    finally
                    {
                        // 任务完成后重置生成状态
                        if (isFirstFrame)
                            shot.IsFirstFrameGenerating = false;
                        else
                            shot.IsLastFrameGenerating = false;
                    }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图像生成异常: Shot {ShotNumber}, IsFirstFrame: {IsFirstFrame}",
                shot.ShotNumber, isFirstFrame);
            _messenger.Send(new ImageGenerationCompletedMessage(shot, isFirstFrame, false, null));

            // 异常时重置生成状态
            if (isFirstFrame)
                shot.IsFirstFrameGenerating = false;
            else
                shot.IsLastFrameGenerating = false;
        }
    }

    private string BuildImagePrompt(ShotItem shot, string basePrompt)
    {
        var parts = new List<string> { basePrompt };

        if (!string.IsNullOrWhiteSpace(shot.LightingType))
            parts.Add(shot.LightingType);
        if (!string.IsNullOrWhiteSpace(shot.TimeOfDay))
            parts.Add(shot.TimeOfDay);
        if (!string.IsNullOrWhiteSpace(shot.Composition))
            parts.Add(shot.Composition);
        if (!string.IsNullOrWhiteSpace(shot.ColorStyle))
            parts.Add(shot.ColorStyle);
        if (!string.IsNullOrWhiteSpace(shot.LensType))
            parts.Add(shot.LensType);

        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private (int width, int height) ParseImageSize(string? sizeStr)
    {
        if (string.IsNullOrWhiteSpace(sizeStr))
            return (1024, 1024);

        var parts = sizeStr.Split('x', 'X', '×');
        if (parts.Length == 2 &&
            int.TryParse(parts[0].Trim(), out var w) &&
            int.TryParse(parts[1].Trim(), out var h))
        {
            return (w, h);
        }

        return (1024, 1024);
    }
}
