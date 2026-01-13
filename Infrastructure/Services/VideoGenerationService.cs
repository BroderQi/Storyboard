using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Media;
using Storyboard.Models;

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

        var outDir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "shots")
            : outputDirectory;
        Directory.CreateDirectory(outDir);

        var safePrefix = string.IsNullOrWhiteSpace(filePrefix) ? $"shot_{shot.ShotNumber:000}" : filePrefix;
        var outputPath = Path.Combine(outDir, $"{safePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.mp4");

        var config = _configMonitor.CurrentValue.Video;
        var provider = ResolveProvider(config);
        var settings = config.Local;
        var model = ResolveModel(provider, shot.SelectedModel, config);

        var request = new VideoGenerationRequest(
            shot,
            outputPath,
            model,
            settings.Width,
            settings.Height,
            settings.Fps,
            settings.BitrateKbps,
            settings.TransitionSeconds,
            settings.UseKenBurns);

        await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);

        if (!File.Exists(outputPath))
            throw new InvalidOperationException("分镜视频生成完成但未找到输出文件。");

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

    private static string ResolveModel(IVideoGenerationProvider provider, string model, VideoServicesConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(model) &&
            provider.SupportedModels.Any(m => string.Equals(m, model, StringComparison.OrdinalIgnoreCase)))
            return model;

        return provider.ProviderType switch
        {
            VideoProviderType.OpenAI => config.OpenAI.DefaultModel,
            VideoProviderType.Gemini => config.Gemini.DefaultModel,
            VideoProviderType.StableDiffusionApi => config.StableDiffusionApi.DefaultModel,
            _ => "local"
        };
    }
}
