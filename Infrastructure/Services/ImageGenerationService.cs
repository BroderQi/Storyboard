using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Services;

public sealed class ImageGenerationService : IImageGenerationService
{
    private readonly IEnumerable<IImageGenerationProvider> _providers;
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<ImageGenerationService> _logger;

    public ImageGenerationService(
        IEnumerable<IImageGenerationProvider> providers,
        IOptionsMonitor<AIServicesConfiguration> configMonitor,
        ILogger<ImageGenerationService> logger)
    {
        _providers = providers;
        _configMonitor = configMonitor;
        _logger = logger;
    }

    public async Task<string> GenerateImageAsync(
        string prompt,
        string model,
        string? outputDirectory = null,
        string? filePrefix = null,
        CancellationToken cancellationToken = default)
    {
        var outDir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "images")
            : outputDirectory;
        Directory.CreateDirectory(outDir);

        var safePrefix = string.IsNullOrWhiteSpace(filePrefix) ? "image" : filePrefix;
        var config = _configMonitor.CurrentValue.Image;
        var provider = ResolveProvider(config);
        var (width, height) = ResolveSize(provider, config);
        var style = provider.ProviderType == ImageProviderType.Local ? config.Local.Style : "AI";
        var resolvedModel = ResolveModel(provider, model, config);

        var request = new ImageGenerationRequest(
            prompt,
            resolvedModel,
            width,
            height,
            style);

        var result = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        var extension = NormalizeExtension(result.FileExtension);
        var filePath = Path.Combine(outDir, $"{safePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}");

        await File.WriteAllBytesAsync(filePath, result.ImageBytes, cancellationToken).ConfigureAwait(false);
        return filePath;
    }

    private IImageGenerationProvider ResolveProvider(ImageServicesConfiguration config)
    {
        var selected = _providers.FirstOrDefault(p => p.ProviderType == config.DefaultProvider && p.IsConfigured);
        if (selected != null)
            return selected;

        var fallback = _providers.FirstOrDefault(p => p.IsConfigured);
        if (fallback == null)
            throw new InvalidOperationException("没有可用的图片生成提供商。");

        _logger.LogWarning("默认图片提供商不可用，已切换到 {Provider}", fallback.DisplayName);
        return fallback;
    }

    private static (int Width, int Height) ResolveSize(IImageGenerationProvider provider, ImageServicesConfiguration config)
    {
        return provider.ProviderType switch
        {
            ImageProviderType.OpenAI => ParseSize(config.OpenAI.Size, 1024, 1024),
            ImageProviderType.Gemini => (Math.Max(320, config.Local.Width), Math.Max(240, config.Local.Height)),
            ImageProviderType.StableDiffusionApi => (Math.Max(320, config.Local.Width), Math.Max(240, config.Local.Height)),
            _ => (Math.Max(320, config.Local.Width), Math.Max(240, config.Local.Height))
        };
    }

    private static string ResolveModel(IImageGenerationProvider provider, string model, ImageServicesConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(model) &&
            provider.SupportedModels.Any(m => string.Equals(m, model, StringComparison.OrdinalIgnoreCase)))
        {
            return model;
        }

        return provider.ProviderType switch
        {
            ImageProviderType.OpenAI => config.OpenAI.DefaultModel,
            ImageProviderType.Gemini => config.Gemini.DefaultModel,
            ImageProviderType.StableDiffusionApi => config.StableDiffusionApi.DefaultModel,
            _ => "local"
        };
    }

    private static (int Width, int Height) ParseSize(string? size, int fallbackWidth, int fallbackHeight)
    {
        if (string.IsNullOrWhiteSpace(size))
            return (fallbackWidth, fallbackHeight);

        var parts = size.Split('x', 'X');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var w) &&
            int.TryParse(parts[1], out var h) &&
            w > 0 && h > 0)
            return (w, h);

        return (fallbackWidth, fallbackHeight);
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ".png";

        return extension.StartsWith('.') ? extension : $".{extension}";
    }
}
