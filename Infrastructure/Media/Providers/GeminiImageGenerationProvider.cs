using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class GeminiImageGenerationProvider : IImageGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public GeminiImageGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
    }

    private GeminiImageConfig Config => _configMonitor.CurrentValue.Image.Gemini;

    public ImageProviderType ProviderType => ImageProviderType.Gemini;
    public string DisplayName => "Gemini";
    public bool IsConfigured => Config.Enabled
        && !string.IsNullOrWhiteSpace(Config.ApiKey)
        && !string.IsNullOrWhiteSpace(Config.DefaultModel);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "imagen-3.0-generate-002",
        "imagen-3.0-fast-generate-001",
        "gemini-2.0-flash-image-generation"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.ImageGeneration, "MaxResolution: 2048x2048", "image/png")
    };

    public async Task<ImageGenerationResult> GenerateAsync(
        ImageGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = Config;
        if (!IsConfigured)
            throw new InvalidOperationException("Gemini 图片生成未配置。");

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new InvalidOperationException("图片生成提示词为空。");

        var model = string.IsNullOrWhiteSpace(request.Model) ? cfg.DefaultModel : request.Model;

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(cfg.Endpoint),
            Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds)
        };

        var prompt = request.Prompt.Trim();

        var imagesPayload = new
        {
            prompt = new { text = prompt },
            numberOfImages = 1
        };

        var response = await httpClient.PostAsync(
            $"/models/{model}:generateImages?key={cfg.ApiKey}",
            new StringContent(JsonSerializer.Serialize(imagesPayload), Encoding.UTF8, "application/json"),
            cancellationToken).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            if (TryExtractImageBytes(body, out var bytes, out var extension))
                return new ImageGenerationResult(bytes, extension, model);
        }

        var contentPayload = new Dictionary<string, object?>
        {
            ["contents"] = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = prompt } }
                }
            },
            ["generationConfig"] = new
            {
                responseMimeType = cfg.ResponseMimeType
            }
        };

        var fallbackResponse = await httpClient.PostAsync(
            $"/models/{model}:generateContent?key={cfg.ApiKey}",
            new StringContent(JsonSerializer.Serialize(contentPayload), Encoding.UTF8, "application/json"),
            cancellationToken).ConfigureAwait(false);

        var fallbackBody = await fallbackResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!fallbackResponse.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini 图片生成失败: {fallbackBody}");

        if (!TryExtractImageBytes(fallbackBody, out var fallbackBytes, out var fallbackExt))
            throw new InvalidOperationException("Gemini 图片生成返回缺少图片数据。");

        return new ImageGenerationResult(fallbackBytes, fallbackExt, model);
    }

    private static bool TryExtractImageBytes(string json, out byte[] bytes, out string extension)
    {
        bytes = Array.Empty<byte>();
        extension = ".png";

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("generatedImages", out var generatedImages) &&
            generatedImages.ValueKind == JsonValueKind.Array &&
            generatedImages.GetArrayLength() > 0)
        {
            var image = generatedImages[0];
            if (TryReadBase64(image, "imageBytes", out bytes) ||
                TryReadBase64(image, "bytesBase64Encoded", out bytes))
            {
                return true;
            }
        }

        if (root.TryGetProperty("candidates", out var candidates) &&
            candidates.ValueKind == JsonValueKind.Array &&
            candidates.GetArrayLength() > 0)
        {
            var candidate = candidates[0];
            if (candidate.TryGetProperty("content", out var content) &&
                content.TryGetProperty("parts", out var parts) &&
                parts.ValueKind == JsonValueKind.Array)
            {
                foreach (var part in parts.EnumerateArray())
                {
                    if (TryReadInlineData(part, out bytes, out extension))
                        return true;
                }
            }
        }

        return false;
    }

    private static bool TryReadInlineData(JsonElement part, out byte[] bytes, out string extension)
    {
        bytes = Array.Empty<byte>();
        extension = ".png";

        if (part.TryGetProperty("inlineData", out var inlineData) ||
            part.TryGetProperty("inline_data", out inlineData))
        {
            if (inlineData.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
            {
                bytes = Convert.FromBase64String(data.GetString() ?? string.Empty);

                if (inlineData.TryGetProperty("mimeType", out var mimeType) ||
                    inlineData.TryGetProperty("mime_type", out mimeType))
                {
                    extension = MimeToExtension(mimeType.GetString());
                }

                return bytes.Length > 0;
            }
        }

        return false;
    }

    private static bool TryReadBase64(JsonElement element, string property, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (element.TryGetProperty(property, out var data) && data.ValueKind == JsonValueKind.String)
        {
            bytes = Convert.FromBase64String(data.GetString() ?? string.Empty);
            return bytes.Length > 0;
        }

        return false;
    }

    private static string MimeToExtension(string? mimeType)
    {
        return mimeType?.ToLowerInvariant() switch
        {
            "image/jpeg" => ".jpg",
            "image/jpg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".png"
        };
    }
}
