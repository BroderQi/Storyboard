using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Domain.Entities;
using Storyboard.Infrastructure.Media;
using Storyboard.Messages;
using Storyboard.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
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

        // 查询所有镜头
        var query = new GetAllShotsQuery();
        _messenger.Send(query);
        var shots = query.Shots;

        if (shots == null || shots.Count == 0)
        {
            _logger.LogWarning("没有镜头可生成视频");
            return;
        }

        var queuedCount = 0;
        foreach (var shot in shots)
        {
            // 跳过已经生成视频的镜头
            if (!string.IsNullOrWhiteSpace(shot.GeneratedVideoPath) && System.IO.File.Exists(shot.GeneratedVideoPath))
            {
                _logger.LogInformation("跳过已生成视频的镜头: Shot {ShotNumber}", shot.ShotNumber);
                continue;
            }

            // 跳过正在生成的镜头
            if (shot.IsVideoGenerating)
            {
                _logger.LogInformation("跳过正在生成的镜头: Shot {ShotNumber}", shot.ShotNumber);
                continue;
            }

            // 发送视频生成请求消息
            _messenger.Send(new VideoGenerationRequestedMessage(shot));
            queuedCount++;
        }

        _logger.LogInformation("批量生成视频: 已加入队列 {Count} 个镜头", queuedCount);
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

            // 创建视频生成任务
            _jobQueue.Enqueue(
                GenerationJobType.Video,
                shot.ShotNumber,
                async (ct, progress) =>
                {
                    var videoPath = await _videoGenerationService.GenerateVideoAsync(
                        shot,
                        outputDirectory: null,
                        filePrefix: $"shot_{shot.ShotNumber:000}_video",
                        cancellationToken: ct);

                    if (!string.IsNullOrWhiteSpace(videoPath))
                    {
                        // 生成视频缩略图
                        var thumbnailPath = await TryCreateVideoThumbnailAsync(videoPath, ct);

                        // 保存视频路径
                        shot.GeneratedVideoPath = videoPath;

                        // 添加到资产列表
                        var asset = new ShotAssetItem
                        {
                            FilePath = videoPath,
                            ThumbnailPath = null,
                            VideoThumbnailPath = thumbnailPath,
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
                        _logger.LogWarning("视频生成失败: Shot {ShotNumber}", shot.ShotNumber);
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

    /// <summary>
    /// 为视频生成缩略图
    /// </summary>
    private async Task<string?> TryCreateVideoThumbnailAsync(string videoPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(videoPath) || !File.Exists(videoPath))
            return null;

        try
        {
            // 获取项目输出目录
            var query = new GetCurrentProjectIdQuery();
            _messenger.Send(query);
            var projectId = query.ProjectId ?? "temp";

            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "projects", projectId, "video-thumbnails");
            Directory.CreateDirectory(outputDir);

            var baseName = Path.GetFileNameWithoutExtension(videoPath);
            var thumbPath = Path.Combine(outputDir, $"{baseName}_thumb.jpg");

            // 使用 FFmpeg 提取视频第一帧作为缩略图
            var args = $"-y -hide_banner -loglevel error -ss 0.2 -i \"{videoPath}\" -frames:v 1 -q:v 2 \"{thumbPath}\"";
            var (exitCode, stdout, stderr) = await RunProcessCaptureAsync(
                FfmpegLocator.GetFfmpegPath(),
                args,
                cancellationToken);

            if (exitCode != 0 || !File.Exists(thumbPath))
            {
                _logger.LogWarning("视频缩略图生成失败: {Error}", stderr);
                return null;
            }

            _logger.LogInformation("视频缩略图生成成功: {ThumbnailPath}", thumbPath);
            return thumbPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "生成视频缩略图时发生异常: {VideoPath}", videoPath);
            return null;
        }
    }

    /// <summary>
    /// 运行外部进程并捕获输出
    /// </summary>
    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessCaptureAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stdout.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                stderr.AppendLine(e.Data);
        };

        if (!proc.Start())
            throw new InvalidOperationException($"无法启动进程: {fileName}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(cancellationToken);
        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
