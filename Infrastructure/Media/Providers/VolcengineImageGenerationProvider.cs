using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class VolcengineImageGenerationProvider : IImageGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public VolcengineImageGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
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
        var size = ResolveSize(imageConfig, request.Width, request.Height);
        var responseFormat = string.IsNullOrWhiteSpace(imageConfig.ResponseFormat)
            ? "b64_json"
            : imageConfig.ResponseFormat.Trim();
        if (imageConfig.Stream)
            throw new InvalidOperationException("Streaming image generation is not supported.");

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(providerConfig.Endpoint),
            Timeout = TimeSpan.FromSeconds(providerConfig.TimeoutSeconds)
        };

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["prompt"] = request.Prompt.Trim(),
            ["size"] = size,
            ["response_format"] = responseFormat,
            ["stream"] = false,
            ["watermark"] = imageConfig.Watermark
        };

        if (!string.IsNullOrWhiteSpace(imageConfig.SequentialImageGeneration))
            payload["sequential_image_generation"] = imageConfig.SequentialImageGeneration;

        if (imageConfig.SequentialMaxImages.HasValue && imageConfig.SequentialMaxImages.Value > 0)
        {
            payload["sequential_image_generation_options"] = new Dictionary<string, object?>
            {
                ["max_images"] = imageConfig.SequentialMaxImages.Value
            };
        }

        if (!string.IsNullOrWhiteSpace(imageConfig.OptimizePromptMode))
        {
            payload["optimize_prompt_options"] = new Dictionary<string, object?>
            {
                ["mode"] = imageConfig.OptimizePromptMode
            };
        }

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("images/generations", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Volcengine image generation failed: {responseBody}");

        return await ParseImageResultAsync(responseBody, model, cancellationToken).ConfigureAwait(false);
    }

    private static string ResolveSize(VolcengineImageConfig config, int width, int height)
    {
        if (!string.IsNullOrWhiteSpace(config.Size))
            return config.Size.Trim();

        if (width > 0 && height > 0)
            return $"{width}x{height}";

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
}
