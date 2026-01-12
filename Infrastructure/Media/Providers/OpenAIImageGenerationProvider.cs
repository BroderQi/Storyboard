using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class OpenAIImageGenerationProvider : IImageGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public OpenAIImageGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
    }

    private OpenAIImageConfig Config => _configMonitor.CurrentValue.Image.OpenAI;

    public ImageProviderType ProviderType => ImageProviderType.OpenAI;
    public string DisplayName => "OpenAI";
    public bool IsConfigured => Config.Enabled
        && !string.IsNullOrWhiteSpace(Config.ApiKey)
        && !string.IsNullOrWhiteSpace(Config.DefaultModel);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "gpt-image-1",
        "dall-e-3",
        "dall-e-2"
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
            throw new InvalidOperationException("OpenAI 图片生成未配置。");

        var model = string.IsNullOrWhiteSpace(request.Model) ? cfg.DefaultModel : request.Model;
        var size = $"{request.Width}x{request.Height}";

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(cfg.Endpoint),
            Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds)
        };

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey);

        if (string.IsNullOrWhiteSpace(request.Prompt))
            throw new InvalidOperationException("图片生成提示词为空。");

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["prompt"] = request.Prompt.Trim(),
            ["size"] = size,
            ["response_format"] = "b64_json"
        };

        if (!string.IsNullOrWhiteSpace(cfg.Quality))
            payload["quality"] = cfg.Quality;

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("/images/generations", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI 图片生成失败: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        var data = doc.RootElement.GetProperty("data");
        if (data.ValueKind != JsonValueKind.Array || data.GetArrayLength() == 0)
            throw new InvalidOperationException("OpenAI 图片生成返回为空。");

        var base64 = data[0].GetProperty("b64_json").GetString();
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException("OpenAI 图片生成结果缺少 b64_json。");

        var bytes = Convert.FromBase64String(base64);
        return new ImageGenerationResult(bytes, ".png", model);
    }
}
