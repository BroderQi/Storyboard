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
        var aiConfig = _configMonitor.CurrentValue;
        var imageConfig = aiConfig.Image;
        var provider = ResolveProvider(imageConfig);
        var (width, height) = ResolveSize(imageConfig.Volcengine);
        var resolvedModel = ResolveModel(provider, model, aiConfig);

        var request = new ImageGenerationRequest(
            prompt,
            resolvedModel,
            width,
            height,
            "AI");

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

    private static (int Width, int Height) ResolveSize(VolcengineImageConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Size))
            return (2048, 2048);

        var size = config.Size.Trim();
        if (TryParseSize(size, out var width, out var height))
            return (width, height);

        if (string.Equals(size, "1K", StringComparison.OrdinalIgnoreCase))
            return (1024, 1024);
        if (string.Equals(size, "2K", StringComparison.OrdinalIgnoreCase))
            return (2048, 2048);
        if (string.Equals(size, "4K", StringComparison.OrdinalIgnoreCase))
            return (4096, 4096);

        return (2048, 2048);
    }

    private static string ResolveModel(IImageGenerationProvider provider, string model, AIServicesConfiguration config)
    {
        if (!string.IsNullOrWhiteSpace(model) &&
            provider.SupportedModels.Any(m => string.Equals(m, model, StringComparison.OrdinalIgnoreCase)))
        {
            return model;
        }

        if (config.Defaults.Image.Provider == AIProviderType.Volcengine &&
            !string.IsNullOrWhiteSpace(config.Defaults.Image.Model))
        {
            return config.Defaults.Image.Model;
        }

        var providerConfig = config.Providers.Volcengine;
        if (string.IsNullOrWhiteSpace(providerConfig.DefaultModels.Image))
            throw new InvalidOperationException("No default image model configured for Volcengine.");

        return providerConfig.DefaultModels.Image;
    }

    private static bool TryParseSize(string? size, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(size))
            return false;

        var parts = size.Split('x', 'X');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out width) &&
            int.TryParse(parts[1], out height) &&
            width > 0 && height > 0)
            return true;

        width = 0;
        height = 0;
        return false;
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return ".png";

        return extension.StartsWith('.') ? extension : $".{extension}";
    }
}
