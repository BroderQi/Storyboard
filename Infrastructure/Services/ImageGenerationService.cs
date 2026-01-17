using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Media;
using Storyboard.Models;

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

    public async Task<string> GenerateImageAsync(
        ImageGenerationRequest request,
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

        // Use the request directly - it already contains all parameters
        var result = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        var extension = NormalizeExtension(result.FileExtension);
        var filePath = Path.Combine(outDir, $"{safePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}");

        await File.WriteAllBytesAsync(filePath, result.ImageBytes, cancellationToken).ConfigureAwait(false);
        return filePath;
    }

    public async Task<string> GenerateFrameImageAsync(
        ShotItem shot,
        bool isFirstFrame,
        string? outputDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (shot == null)
            throw new ArgumentNullException(nameof(shot));

        var prompt = isFirstFrame ? shot.FirstFramePrompt : shot.LastFramePrompt;
        if (string.IsNullOrWhiteSpace(prompt))
            throw new InvalidOperationException($"{(isFirstFrame ? "首帧" : "尾帧")}提示词为空。");

        var outDir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "shots", $"shot_{shot.ShotNumber:000}", isFirstFrame ? "first_frame" : "last_frame")
            : outputDirectory;
        Directory.CreateDirectory(outDir);

        var aiConfig = _configMonitor.CurrentValue;
        var imageConfig = aiConfig.Image;
        var provider = ResolveProvider(imageConfig);

        // Resolve size: use ImageSize if provided, otherwise use ImageQuality
        string? sizeParam = null;
        int width = 0;
        int height = 0;

        if (!string.IsNullOrWhiteSpace(shot.ImageSize))
        {
            // User provided custom size (e.g., "1920x1080")
            sizeParam = shot.ImageSize.Trim();
            if (TryParseSize(sizeParam, out width, out height))
            {
                // Successfully parsed custom size
            }
            else
            {
                // Invalid format, use as-is (API will handle it)
                width = 2048;
                height = 2048;
            }
        }
        else if (!string.IsNullOrWhiteSpace(shot.ImageQuality))
        {
            // Use quality level (1K/2K/4K)
            sizeParam = shot.ImageQuality.Trim();
            // Set default dimensions based on quality
            if (string.Equals(sizeParam, "1K", StringComparison.OrdinalIgnoreCase))
            {
                width = 1024;
                height = 1024;
            }
            else if (string.Equals(sizeParam, "4K", StringComparison.OrdinalIgnoreCase))
            {
                width = 4096;
                height = 4096;
            }
            else // Default to 2K
            {
                width = 2048;
                height = 2048;
            }
        }
        else
        {
            // No size specified, use default from config
            (width, height) = ResolveSize(imageConfig.Volcengine);
            sizeParam = imageConfig.Volcengine.Size;
        }

        var model = ResolveModel(provider, shot.SelectedModel, aiConfig);

        // Get frame-specific parameters
        var shotType = isFirstFrame ? shot.FirstFrameShotType : shot.LastFrameShotType;
        var composition = isFirstFrame ? shot.FirstFrameComposition : shot.LastFrameComposition;
        var lightingType = isFirstFrame ? shot.FirstFrameLightingType : shot.LastFrameLightingType;
        var timeOfDay = isFirstFrame ? shot.FirstFrameTimeOfDay : shot.LastFrameTimeOfDay;
        var colorStyle = isFirstFrame ? shot.FirstFrameColorStyle : shot.LastFrameColorStyle;
        var negativePrompt = isFirstFrame ? shot.FirstFrameNegativePrompt : shot.LastFrameNegativePrompt;

        // Build enhanced prompt with professional parameters
        var enhancedPrompt = BuildEnhancedPrompt(prompt, shotType, composition, lightingType, timeOfDay, colorStyle);

        var request = new ImageGenerationRequest(
            enhancedPrompt,
            model,
            width,
            height,
            "AI",
            shotType,
            composition,
            lightingType,
            timeOfDay,
            colorStyle,
            negativePrompt,
            shot.AspectRatio,
            ReferenceImagePaths: null,
            SequentialGeneration: false,
            MaxImages: null,
            Size: sizeParam,
            Watermark: shot.ImageWatermark);

        var result = await provider.GenerateAsync(request, cancellationToken).ConfigureAwait(false);
        var extension = NormalizeExtension(result.FileExtension);
        var filePrefix = isFirstFrame ? "first_frame" : "last_frame";
        var filePath = Path.Combine(outDir, $"{filePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}");

        await File.WriteAllBytesAsync(filePath, result.ImageBytes, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Generated {FrameType} for shot {ShotNumber}: {FilePath}",
            isFirstFrame ? "首帧" : "尾帧", shot.ShotNumber, filePath);

        return filePath;
    }

    private static string BuildEnhancedPrompt(
        string basePrompt,
        string? shotType,
        string? composition,
        string? lightingType,
        string? timeOfDay,
        string? colorStyle)
    {
        var parts = new List<string> { basePrompt };

        if (!string.IsNullOrWhiteSpace(shotType))
            parts.Add($"景别: {shotType}");
        if (!string.IsNullOrWhiteSpace(composition))
            parts.Add($"构图: {composition}");
        if (!string.IsNullOrWhiteSpace(lightingType))
            parts.Add($"光线: {lightingType}");
        if (!string.IsNullOrWhiteSpace(timeOfDay))
            parts.Add($"时间: {timeOfDay}");
        if (!string.IsNullOrWhiteSpace(colorStyle))
            parts.Add($"色调: {colorStyle}");

        return string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
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
