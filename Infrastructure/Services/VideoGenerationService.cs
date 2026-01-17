using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Media;
using Storyboard.Models;
using VideoGenerationRequest = Storyboard.Infrastructure.Media.VideoGenerationRequest;

namespace Storyboard.Infrastructure.Services;

public sealed class VideoGenerationService : IVideoGenerationService
{
    private readonly IEnumerable<IVideoGenerationProvider> _providers;
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<VideoGenerationService> _logger;

    public VideoGenerationService(
        IEnumerable<IVideoGenerationProvider> providers,
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        ILogger<VideoGenerationService> logger)
    {
        _providers = providers;
        _configMonitor = configMonitor;
        _logger = logger;
    }

    public async Task<string> GenerateVideoAsync(
        ShotItem shot,
        string? outputDirectory = null,
        string? filePrefix = null,
        CancellationToken cancellationToken = default)
    {
        if (shot == null)
            throw new ArgumentNullException(nameof(shot));

        _logger.LogInformation("VideoGenerationService.GenerateVideoAsync 开始 - Shot {ShotNumber}", shot.ShotNumber);

        var outDir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "shots")
            : outputDirectory;
        Directory.CreateDirectory(outDir);
        _logger.LogInformation("输出目录: {OutputDir}", outDir);

        var safePrefix = string.IsNullOrWhiteSpace(filePrefix) ? $"shot_{shot.ShotNumber:000}" : filePrefix;
        var outputPath = Path.Combine(outDir, $"{safePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.mp4");
        _logger.LogInformation("输出路径: {OutputPath}", outputPath);

        var aiConfig = _configMonitor.CurrentValue;
        var videoConfig = aiConfig.Video;
        _logger.LogInformation("正在解析视频提供商...");

        var provider = ResolveProvider(videoConfig);
        _logger.LogInformation("使用提供商: {Provider}", provider.DisplayName);

        var model = ResolveModel(provider, shot.SelectedModel, aiConfig);
        _logger.LogInformation("使用模型: {Model}", model);

        var (width, height) = ResolveDimensions(videoConfig.Volcengine.Resolution);
        _logger.LogInformation("分辨率: {Width}x{Height}", width, height);

        var request = new VideoGenerationRequest(
            shot,
            outputPath,
            model,
            width,
            height,
            0,
            0,
            0,
            false);

        _logger.LogInformation("调用提供商生成视频...");
        try
        {
            await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("提供商生成完成");
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("视频生成任务已取消");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "提供商生成视频时发生异常 - 类型: {ExceptionType}, 消息: {Message}, 堆栈: {StackTrace}",
                ex.GetType().Name, ex.Message, ex.StackTrace);
            throw;
        }

        if (!File.Exists(outputPath))
        {
            _logger.LogError("视频文件未找到: {OutputPath}", outputPath);
            throw new InvalidOperationException("分镜视频生成完成但未找到输出文件。");
        }

        _logger.LogInformation("视频生成成功: {OutputPath}", outputPath);
        return outputPath;
    }

    private IVideoGenerationProvider ResolveProvider(VideoServicesConfiguration config)
    {
        var selected = _providers.FirstOrDefault(p => p.ProviderType == config.DefaultProvider && p.IsConfigured);
        if (selected != null)
            return selected;

        var fallback = _providers.FirstOrDefault(p => p.IsConfigured);
        if (fallback == null)
            throw new InvalidOperationException("没有可用的视频生成提供商。");

        _logger.LogWarning("默认视频提供商不可用，已切换到 {Provider}", fallback.DisplayName);
        return fallback;
    }

    private static string ResolveModel(IVideoGenerationProvider provider, string model, AIServicesConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(model) &&
            provider.SupportedModels.Any(m => string.Equals(m, model, StringComparison.OrdinalIgnoreCase)))
            return model;

        if (config.Defaults.Video.Provider == AIProviderType.Volcengine &&
            !string.IsNullOrWhiteSpace(config.Defaults.Video.Model))
        {
            return config.Defaults.Video.Model;
        }

        var providerConfig = config.Providers.Volcengine;
        if (string.IsNullOrWhiteSpace(providerConfig.DefaultModels.Video))
            throw new InvalidOperationException("No default video model configured for Volcengine.");

        return providerConfig.DefaultModels.Video;
    }

    private static (int Width, int Height) ResolveDimensions(string? resolution)
    {
        if (string.IsNullOrWhiteSpace(resolution))
            return (1920, 1080);

        var normalized = resolution.Trim();

        // Try to parse custom resolution format "WIDTHxHEIGHT"
        if (normalized.Contains('x', StringComparison.OrdinalIgnoreCase))
        {
            var parts = normalized.Split(new[] { 'x', 'X' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 &&
                int.TryParse(parts[0].Trim(), out var width) &&
                int.TryParse(parts[1].Trim(), out var height) &&
                width > 0 && height > 0)
            {
                return (width, height);
            }
        }

        // Fallback to preset values
        var lowerNormalized = normalized.ToLowerInvariant();
        return lowerNormalized switch
        {
            "4k" => (3840, 2160),
            "2k" => (2560, 1440),
            "1080p" => (1920, 1080),
            "720p" => (1280, 720),
            "480p" => (864, 480),
            "540p" => (960, 540),
            _ => (1920, 1080)
        };
    }
}
