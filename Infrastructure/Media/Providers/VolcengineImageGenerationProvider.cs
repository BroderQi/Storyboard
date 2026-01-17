using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class VolcengineImageGenerationProvider : IImageGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<VolcengineImageGenerationProvider> _logger;

    public VolcengineImageGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<VolcengineImageGenerationProvider> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    private AIProviderConfiguration ProviderConfig => _configMonitor.CurrentValue.Providers.Volcengine;
    private VolcengineImageConfig ImageConfig => _configMonitor.CurrentValue.Image.Volcengine;

    public ImageProviderType ProviderType => ImageProviderType.Volcengine;
    public string DisplayName => "Volcengine";
    public bool IsConfigured => ProviderConfig.Enabled
        && !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey)
        && !string.IsNullOrWhiteSpace(ProviderConfig.Endpoint);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "doubao-seedream-4-5-251128",
        "doubao-seedream-4-0-250828"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.ImageGeneration, "Size: 1K/2K/4K or custom", "image/jpeg")
    };

    public async Task<ImageGenerationResult> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting image generation. Request: {Request}", JsonSerializer.Serialize(request));

        // Determine generation mode based on request parameters
        var hasReferenceImages = request.ReferenceImagePaths != null &&
                                 request.ReferenceImagePaths.Any(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
        var isSequentialGeneration = request.SequentialGeneration ||
                                     (!string.IsNullOrWhiteSpace(ImageConfig.SequentialImageGeneration) &&
                                      ImageConfig.SequentialImageGeneration != "disabled");

        if (isSequentialGeneration)
        {
            // 组图输出（多图输出）
            if (hasReferenceImages)
            {
                var imageCount = request.ReferenceImagePaths!.Count(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
                if (imageCount > 1)
                {
                    // 多图生组图
                    return await GenerateSequentialImagesFromMultipleImagesAsync(request, cancellationToken);
                }
                else
                {
                    // 单图生组图
                    return await GenerateSequentialImagesFromSingleImageAsync(request, cancellationToken);
                }
            }
            else
            {
                // 文生组图
                return await GenerateSequentialImagesFromTextAsync(request, cancellationToken);
            }
        }
        else
        {
            if (hasReferenceImages)
            {
                var imageCount = request.ReferenceImagePaths!.Count(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
                if (imageCount > 1)
                {
                    // 多图融合（多图输入单图输出）
                    return await GenerateImageFromMultipleImagesAsync(request, cancellationToken);
                }
                else
                {
                    // 图文生图（单图输入单图输出）
                    return await GenerateImageFromSingleImageAsync(request, cancellationToken);
                }
            }
            else
            {
                // 文生图（纯文本输入单图输出）
                return await GenerateImageFromTextAsync(request, cancellationToken);
            }
        }
    }

    /// <summary>
    /// 文生图（纯文本输入单图输出）
    /// Text-to-image generation: pure text input, single image output
    /// </summary>
    private async Task<ImageGenerationResult> GenerateImageFromTextAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Text-to-image generation mode");

        var payload = BuildBasePayload(request);
        payload["sequential_image_generation"] = "disabled";

        return await ExecuteGenerationAsync(payload, request.Model, cancellationToken);
    }

    /// <summary>
    /// 图文生图（单图输入单图输出）
    /// Image-to-image generation: single image input, single image output
    /// </summary>
    private async Task<ImageGenerationResult> GenerateImageFromSingleImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Single image-to-image generation mode");

        var validImage = request.ReferenceImagePaths!
            .First(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

        var payload = BuildBasePayload(request);
        payload["image"] = ToDataUrl(validImage);
        payload["sequential_image_generation"] = "disabled";

        return await ExecuteGenerationAsync(payload, request.Model, cancellationToken);
    }

    /// <summary>
    /// 多图融合（多图输入单图输出）
    /// Multi-image fusion: multiple images input, single image output
    /// </summary>
    private async Task<ImageGenerationResult> GenerateImageFromMultipleImagesAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Multiple images fusion mode");

        var validImages = request.ReferenceImagePaths!
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .ToList();

        var payload = BuildBasePayload(request);
        payload["image"] = validImages.Select(ToDataUrl).ToArray();
        payload["sequential_image_generation"] = "disabled";

        return await ExecuteGenerationAsync(payload, request.Model, cancellationToken);
    }

    /// <summary>
    /// 文生组图（纯文本输入多图输出）
    /// Text-to-sequential-images: pure text input, multiple images output
    /// </summary>
    private async Task<ImageGenerationResult> GenerateSequentialImagesFromTextAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Text-to-sequential-images generation mode");

        var payload = BuildBasePayload(request);
        payload["sequential_image_generation"] = "auto";
        AddSequentialGenerationOptions(payload, request);

        return await ExecuteGenerationAsync(payload, request.Model, cancellationToken);
    }

    /// <summary>
    /// 单图生组图（单图输入多图输出）
    /// Single-image-to-sequential-images: single image input, multiple images output
    /// </summary>
    private async Task<ImageGenerationResult> GenerateSequentialImagesFromSingleImageAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Single image-to-sequential-images generation mode");

        var validImage = request.ReferenceImagePaths!
            .First(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));

        var payload = BuildBasePayload(request);
        payload["image"] = ToDataUrl(validImage);
        payload["sequential_image_generation"] = "auto";
        AddSequentialGenerationOptions(payload, request);

        return await ExecuteGenerationAsync(payload, request.Model, cancellationToken);
    }

    /// <summary>
    /// 多图生组图（多图输入多图输出）
    /// Multiple-images-to-sequential-images: multiple images input, multiple images output
    /// </summary>
    private async Task<ImageGenerationResult> GenerateSequentialImagesFromMultipleImagesAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Multiple images-to-sequential-images generation mode");

        var validImages = request.ReferenceImagePaths!
            .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
            .ToList();

        var payload = BuildBasePayload(request);
        payload["image"] = validImages.Select(ToDataUrl).ToArray();
        payload["sequential_image_generation"] = "auto";
        AddSequentialGenerationOptions(payload, request);

        return await ExecuteGenerationAsync(payload, request.Model, cancellationToken);
    }

    private Dictionary<string, object?> BuildBasePayload(ImageGenerationRequest request)
    {
        var providerConfig = ProviderConfig;
        if (!IsConfigured)
            throw new InvalidOperationException("Volcengine image generation is not configured.");

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new InvalidOperationException("Image prompt is empty.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? providerConfig.DefaultModels.Image
            : request.Model;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("No image model configured for Volcengine.");

        var imageConfig = ImageConfig;

        // Resolve size: prioritize request.Size, then fall back to Width/Height, then config
        string size;
        if (!string.IsNullOrWhiteSpace(request.Size))
        {
            // Use the size from request (e.g., "2K", "1920x1080")
            size = ValidateAndAdjustSize(request.Size.Trim());
        }
        else
        {
            // Fall back to Width/Height or config
            size = ResolveSize(imageConfig, request.Width, request.Height);
        }

        var responseFormat = string.IsNullOrWhiteSpace(imageConfig.ResponseFormat)
            ? "b64_json"
            : imageConfig.ResponseFormat.Trim();
        if (imageConfig.Stream)
            throw new InvalidOperationException("Streaming image generation is not supported.");

        // Use watermark from request, fall back to config
        var watermark = request.Watermark || imageConfig.Watermark;

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["prompt"] = request.Prompt.Trim(),
            ["size"] = size,
            ["response_format"] = responseFormat,
            ["stream"] = false,
            ["watermark"] = watermark
        };

        // Add negative prompt if provided
        if (!string.IsNullOrWhiteSpace(request.NegativePrompt))
        {
            payload["negative_prompt"] = request.NegativePrompt.Trim();
        }

        // Add optimize prompt options if configured
        if (!string.IsNullOrWhiteSpace(imageConfig.OptimizePromptMode))
        {
            payload["optimize_prompt_options"] = new Dictionary<string, object?>
            {
                ["mode"] = imageConfig.OptimizePromptMode
            };
        }

        return payload;
    }

    private void AddSequentialGenerationOptions(Dictionary<string, object?> payload, ImageGenerationRequest request)
    {
        var imageConfig = ImageConfig;

        if (request.MaxImages.HasValue && request.MaxImages.Value > 0)
        {
            payload["sequential_image_generation_options"] = new Dictionary<string, object?>
            {
                ["max_images"] = request.MaxImages.Value
            };
        }
        else if (imageConfig.SequentialMaxImages.HasValue && imageConfig.SequentialMaxImages.Value > 0)
        {
            payload["sequential_image_generation_options"] = new Dictionary<string, object?>
            {
                ["max_images"] = imageConfig.SequentialMaxImages.Value
            };
        }
    }

    private async Task<ImageGenerationResult> ExecuteGenerationAsync(
        Dictionary<string, object?> payload,
        string? requestModel,
        CancellationToken cancellationToken)
    {
        var providerConfig = ProviderConfig;
        var model = string.IsNullOrWhiteSpace(requestModel)
            ? providerConfig.DefaultModels.Image
            : requestModel;

        using var httpClient = new HttpClient
        {
            BaseAddress = BuildBaseAddress(providerConfig.Endpoint),
            Timeout = TimeSpan.FromSeconds(providerConfig.TimeoutSeconds)
        };

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("images/generations", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Image generation API response. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Volcengine image generation failed: {responseBody}");

        var result = await ParseImageResultAsync(responseBody, model!, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Image generation completed. Result: Model={Model}, Extension={Extension}, BytesLength={Length}", result.ModelUsed, result.FileExtension, result.ImageBytes.Length);

        return result;
    }

    private static string ResolveSize(VolcengineImageConfig config, int width, int height)
    {
        if (!string.IsNullOrWhiteSpace(config.Size))
            return ValidateAndAdjustSize(config.Size.Trim());

        if (width > 0 && height > 0)
            return ValidateAndAdjustSize($"{width}x{height}");

        return "2048x2048";
    }

    /// <summary>
    /// Validates and adjusts image size to meet Volcengine API minimum requirement of 3,686,400 pixels.
    /// </summary>
    private static string ValidateAndAdjustSize(string size)
    {
        const int MinPixels = 3686400; // Volcengine API minimum

        // If it's a preset like "1K", "2K", "4K", return as-is (API handles these)
        if (size.EndsWith("K", StringComparison.OrdinalIgnoreCase))
            return size;

        // Try to parse WxH format
        var parts = size.Split('x', 'X');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out var width) &&
            int.TryParse(parts[1], out var height) &&
            width > 0 && height > 0)
        {
            var pixels = width * height;

            // If size meets minimum, return as-is
            if (pixels >= MinPixels)
                return size;

            // Size is too small, scale up proportionally to meet minimum
            var scale = Math.Sqrt((double)MinPixels / pixels);
            var newWidth = (int)Math.Ceiling(width * scale);
            var newHeight = (int)Math.Ceiling(height * scale);

            // Ensure we meet the minimum after rounding
            while (newWidth * newHeight < MinPixels)
            {
                if (newWidth <= newHeight)
                    newWidth++;
                else
                    newHeight++;
            }

            var adjustedSize = $"{newWidth}x{newHeight}";
            // Log warning about adjustment (in production, use proper logging)
            System.Diagnostics.Debug.WriteLine($"[Volcengine] Size {size} ({pixels:N0} pixels) adjusted to {adjustedSize} ({newWidth * newHeight:N0} pixels) to meet minimum requirement of {MinPixels:N0} pixels");

            return adjustedSize;
        }

        // Invalid format, return default
        return "2048x2048";
    }

    private static async Task<ImageGenerationResult> ParseImageResultAsync(
        string responseBody,
        string modelUsed,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array ||
            data.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Volcengine image generation returned empty data.");
        }

        var item = data[0];
        if (TryReadString(item, "b64_json", out var base64))
        {
            var bytes = Convert.FromBase64String(base64!);
            return new ImageGenerationResult(bytes, ".jpg", modelUsed);
        }

        if (TryReadString(item, "url", out var url))
        {
            var bytes = await DownloadBytesAsync(url!, cancellationToken).ConfigureAwait(false);
            var extension = ResolveExtensionFromUrl(url!) ?? ".jpg";
            return new ImageGenerationResult(bytes, extension, modelUsed);
        }

        throw new InvalidOperationException("Volcengine image generation returned no image content.");
    }

    private static async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        return await httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
    }

    private static bool TryReadString(JsonElement element, string property, out string? value)
    {
        value = null;
        if (element.TryGetProperty(property, out var data) && data.ValueKind == JsonValueKind.String)
        {
            value = data.GetString();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }

    private static string? ResolveExtensionFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var ext = Path.GetExtension(uri.AbsolutePath);
            if (!string.IsNullOrWhiteSpace(ext))
                return ext;
        }

        return null;
    }

    private static Uri BuildBaseAddress(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("Endpoint is required.");

        var normalized = endpoint.TrimEnd('/');
        if (normalized.EndsWith("/api/v3", StringComparison.OrdinalIgnoreCase))
            return new Uri($"{normalized}/");

        return new Uri($"{normalized}/api/v3/");
    }

    private static string ToDataUrl(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        var base64 = Convert.ToBase64String(bytes);
        var mime = GetMimeType(filePath);
        return $"data:{mime};base64,{base64}";
    }

    private static string GetMimeType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".tif" => "image/tiff",
            ".tiff" => "image/tiff",
            _ => "application/octet-stream"
        };
    }
}
