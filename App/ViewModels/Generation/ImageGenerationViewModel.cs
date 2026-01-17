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
        // TODO: 实现批量图像生成逻辑
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
                return;
            }

            // 构建完整提示词（包含专业参数）
            var fullPrompt = BuildImagePrompt(shot, prompt);

            // 创建图像生成任务
            _jobQueue.Enqueue(
                isFirstFrame ? GenerationJobType.ImageFirst : GenerationJobType.ImageLast,
                shot.ShotNumber,
                async (ct, progress) =>
                {
                    var (width, height) = ParseImageSize(shot.ImageSize);

                    // 创建图像生成请求
                    var request = new ImageGenerationRequest(
                        Prompt: fullPrompt,
                        Model: "default", // TODO: 从配置中获取模型
                        Width: width,
                        Height: height,
                        Style: "AI",
                        NegativePrompt: shot.NegativePrompt,
                        LightingType: shot.LightingType,
                        TimeOfDay: shot.TimeOfDay,
                        Composition: shot.Composition,
                        ColorStyle: shot.ColorStyle
                    );

                    var imagePath = await _imageGenerationService.GenerateImageAsync(
                        request,
                        outputDirectory: null,
                        filePrefix: $"shot_{shot.ShotNumber}_{(isFirstFrame ? "first" : "last")}",
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
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "图像生成异常: Shot {ShotNumber}, IsFirstFrame: {IsFirstFrame}",
                shot.ShotNumber, isFirstFrame);
            _messenger.Send(new ImageGenerationCompletedMessage(shot, isFirstFrame, false, null));
        }
        finally
        {
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
