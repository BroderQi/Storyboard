using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class StableDiffusionApiImageGenerationProvider : IImageGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public StableDiffusionApiImageGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
    }

    private StableDiffusionApiImageConfig Config => _configMonitor.CurrentValue.Image.StableDiffusionApi;

    public ImageProviderType ProviderType => ImageProviderType.StableDiffusionApi;
    public string DisplayName => "Stable Diffusion API";
    public bool IsConfigured => Config.Enabled && !string.IsNullOrWhiteSpace(Config.ApiKey);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "stabilityai/stable-diffusion-3.5-large",
        "stabilityai/stable-diffusion-3.5-medium",
        "runwayml/stable-diffusion-v1-5"
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
            throw new InvalidOperationException("Stable Diffusion API 图片生成未配置。");

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new InvalidOperationException("图片生成提示词为空。");

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(cfg.Endpoint),
            Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds)
        };

        var payload = new Dictionary<string, object?>
        {
            ["key"] = cfg.ApiKey,
            ["prompt"] = request.Prompt.Trim(),
            ["negative_prompt"] = cfg.NegativePrompt,
            ["width"] = request.Width,
            ["height"] = request.Height,
            ["samples"] = 1
        };

        if (!string.IsNullOrWhiteSpace(request.Model))
            payload["model"] = request.Model;

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("/text2img", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Stable Diffusion API 图片生成失败: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array ||
            output.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Stable Diffusion API 图片生成返回为空。");
        }

        var url = output[0].GetString();
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Stable Diffusion API 图片生成结果缺少输出地址。");

        var bytes = await DownloadBinaryAsync(url, cancellationToken).ConfigureAwait(false);
        var extension = Path.GetExtension(url);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        return new ImageGenerationResult(bytes, extension, request.Model);
    }

    private static async Task<byte[]> DownloadBinaryAsync(string url, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        return await httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
    }
}
