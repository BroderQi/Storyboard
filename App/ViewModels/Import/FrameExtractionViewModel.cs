using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Media;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Storyboard.ViewModels.Import;

/// <summary>
/// 抽帧 ViewModel - 负责视频抽帧配置和执行
/// </summary>
public partial class FrameExtractionViewModel : ObservableObject
{
    private readonly IFrameExtractionService _frameExtractionService;
    private readonly IVideoMetadataService _videoMetadataService;
    private readonly IMessenger _messenger;
    private readonly ILogger<FrameExtractionViewModel> _logger;

    [ObservableProperty]
    private int _extractModeIndex = 0; // 0=Fixed, 1=Dynamic, 2=Interval, 3=Keyframe

    [ObservableProperty]
    private int _frameCount = 10;

    [ObservableProperty]
    private double _timeInterval = 5.0;

    [ObservableProperty]
    private double _detectionSensitivity = 0.3;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private string? _currentProjectId;

    [ObservableProperty]
    private string? _currentVideoPath;

    public bool IsFixedOrDynamicMode => ExtractModeIndex == 0 || ExtractModeIndex == 1;
    public bool IsIntervalMode => ExtractModeIndex == 2;
    public bool IsDynamicMode => ExtractModeIndex == 1;
    public bool IsKeyframeMode => ExtractModeIndex == 3;

    partial void OnExtractModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsFixedOrDynamicMode));
        OnPropertyChanged(nameof(IsIntervalMode));
        OnPropertyChanged(nameof(IsDynamicMode));
        OnPropertyChanged(nameof(IsKeyframeMode));
    }

    public FrameExtractionViewModel(
        IFrameExtractionService frameExtractionService,
        IVideoMetadataService videoMetadataService,
        IMessenger messenger,
        ILogger<FrameExtractionViewModel> logger)
    {
        _frameExtractionService = frameExtractionService;
        _videoMetadataService = videoMetadataService;
        _messenger = messenger;
        _logger = logger;

        // 订阅项目关闭消息
        _messenger.Register<ProjectCreatedMessage>(this, OnProjectCreated);
        _messenger.Register<ProjectOpenedMessage>(this, OnProjectOpened);
        _messenger.Register<ProjectDataLoadedMessage>(this, OnProjectDataLoaded);
        _messenger.Register<ProjectClosedMessage>(this, OnProjectClosed);
        _messenger.Register<VideoImportedMessage>(this, OnVideoImported);
    }

    [RelayCommand]
    private async Task ExtractFrames()
    {
        // 从消息总线获取当前视频路径和项目ID
        // 这需要通过其他方式传递，暂时使用临时方案
        var videoPath = GetCurrentVideoPath();
        var projectId = GetCurrentProjectId();

        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
        {
            _logger.LogWarning("请先导入视频文件");
            return;
        }

        if (string.IsNullOrWhiteSpace(projectId))
        {
            projectId = Guid.NewGuid().ToString("N");
        }

        try
        {
            IsAnalyzing = true;

            var mode = (FrameExtractionMode)ExtractModeIndex;
            var request = new FrameExtractionRequest(
                videoPath,
                projectId,
                mode,
                FrameCount,
                TimeInterval,
                DetectionSensitivity);

            var progress = new Progress<double>(p =>
            {
                _logger.LogInformation("抽帧进度: {Progress}%", Math.Round(p * 100));
            });

            var result = await _frameExtractionService.ExtractAsync(request, progress).ConfigureAwait(false);
            var metadata = await _videoMetadataService.GetMetadataAsync(videoPath).ConfigureAwait(false);

            // 构建镜头列表
            var shots = BuildShotsFromFrames(result.Frames, metadata.DurationSeconds);

            _logger.LogInformation("抽帧完成，生成 {Count} 个分镜", shots.Count);

            // 发送抽帧完成消息（包含镜头列表）
            _messenger.Send(new FramesExtractedMessage(shots));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "抽帧失败");
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private List<ShotItem> BuildShotsFromFrames(IReadOnlyList<ExtractedFrame> frames, double totalDuration)
    {
        var shots = new List<ShotItem>();

        if (frames.Count == 0)
            return shots;

        var ordered = frames.OrderBy(f => f.TimestampSeconds).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var prevTime = i == 0 ? 0 : ordered[i - 1].TimestampSeconds;
            var nextTime = i == ordered.Count - 1 ? totalDuration : ordered[i + 1].TimestampSeconds;
            var start = i == 0 ? 0 : (prevTime + current.TimestampSeconds) / 2.0;
            var end = i == ordered.Count - 1 ? totalDuration : (current.TimestampSeconds + nextTime) / 2.0;
            if (end < start)
                end = start + 0.5;

            var duration = Math.Max(0.5, end - start);

            // 提取素材信息
            var materialInfo = ExtractMaterialInfo(current.FilePath);

            var shot = new ShotItem(i + 1)
            {
                Duration = duration,
                StartTime = start,
                EndTime = end,
                ShotType = duration > 4 ? "远景" : "中景",
                CoreContent = "抽帧生成镜头",
                ActionCommand = "待补充",
                SceneSettings = "待补充",
                FirstFramePrompt = string.Empty,
                LastFramePrompt = string.Empty,
                SelectedModel = string.Empty,
                MaterialFilePath = current.FilePath,
                MaterialThumbnailPath = current.FilePath,
                MaterialResolution = materialInfo.Resolution,
                MaterialFileSize = materialInfo.FileSize,
                MaterialFormat = materialInfo.Format,
                MaterialColorTone = materialInfo.ColorTone,
                MaterialBrightness = materialInfo.Brightness
            };

            shots.Add(shot);
        }

        return shots;
    }

    private (string Resolution, string FileSize, string Format, string ColorTone, string Brightness) ExtractMaterialInfo(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return ("未知", "未知", "未知", "未知", "未知");

            var fileInfo = new FileInfo(filePath);
            var format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();
            var fileSize = FormatFileSize(fileInfo.Length);

            using var stream = File.OpenRead(filePath);
            using var bitmap = SKBitmap.Decode(stream);

            if (bitmap == null)
                return ($"{0}x{0}", fileSize, format, "未知", "未知");

            var resolution = $"{bitmap.Width}x{bitmap.Height}";
            var (colorTone, brightness) = AnalyzeImageColor(bitmap);

            return (resolution, fileSize, format, colorTone, brightness);
        }
        catch
        {
            return ("未知", "未知", "未知", "未知", "未知");
        }
    }

    private (string ColorTone, string Brightness) AnalyzeImageColor(SKBitmap bitmap)
    {
        long totalR = 0, totalG = 0, totalB = 0;
        int sampleCount = 0;
        int step = Math.Max(1, bitmap.Width / 100);

        for (int y = 0; y < bitmap.Height; y += step)
        {
            for (int x = 0; x < bitmap.Width; x += step)
            {
                var pixel = bitmap.GetPixel(x, y);
                totalR += pixel.Red;
                totalG += pixel.Green;
                totalB += pixel.Blue;
                sampleCount++;
            }
        }

        if (sampleCount == 0)
            return ("中性", "中等");

        var avgR = totalR / sampleCount;
        var avgG = totalG / sampleCount;
        var avgB = totalB / sampleCount;

        var brightness = (avgR + avgG + avgB) / (3.0 * 255.0);

        string colorTone;
        if (avgR > avgG && avgR > avgB)
            colorTone = avgR - Math.Max(avgG, avgB) > 30 ? "暖色调" : "中性";
        else if (avgB > avgR && avgB > avgG)
            colorTone = avgB - Math.Max(avgR, avgG) > 30 ? "冷色调" : "中性";
        else
            colorTone = "中性";

        string brightnessLevel = brightness switch
        {
            < 0.3 => "暗",
            < 0.7 => "中等",
            _ => "亮"
        };

        return (colorTone, brightnessLevel);
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private void OnProjectCreated(object recipient, ProjectCreatedMessage message)
    {
        CurrentProjectId = message.ProjectId;
        CurrentVideoPath = null;

        _logger.LogInformation("项目创建完成，准备抽帧: {ProjectId}", message.ProjectId);
    }

    private void OnProjectOpened(object recipient, ProjectOpenedMessage message)
    {
        CurrentProjectId = message.ProjectId;

        _logger.LogInformation("项目打开，准备抽帧: {ProjectId}", message.ProjectId);
    }

    private void OnProjectDataLoaded(object recipient, ProjectDataLoadedMessage message)
    {
        var state = message.ProjectState;

        CurrentProjectId = state.Id;
        CurrentVideoPath = state.SelectedVideoPath;

        ExtractModeIndex = state.ExtractModeIndex;
        FrameCount = state.FrameCount;
        TimeInterval = state.TimeInterval;
        DetectionSensitivity = state.DetectionSensitivity;

        _logger.LogInformation("项目帧提取参数加载完成");
    }

    private void OnProjectClosed(object recipient, ProjectClosedMessage message)
    {
        // 清空项目信息
        CurrentProjectId = null;
        CurrentVideoPath = null;

        // 重置抽帧参数
        ExtractModeIndex = 0;
        FrameCount = 10;
        TimeInterval = 5.0;
        DetectionSensitivity = 0.3;
    }

    private void OnVideoImported(object recipient, VideoImportedMessage message)
    {
        CurrentVideoPath = message.VideoPath;

        _logger.LogInformation("视频导入完成，更新抽帧路径: {VideoPath}", message.VideoPath);
    }

    // 临时方法 - 需要通过其他方式获取
    private string? GetCurrentVideoPath()
    {
        return CurrentVideoPath;
    }

    private string? GetCurrentProjectId()
    {
        return CurrentProjectId;
    }
}
