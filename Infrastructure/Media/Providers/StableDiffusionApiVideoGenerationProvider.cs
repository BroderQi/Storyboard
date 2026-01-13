using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class StableDiffusionApiVideoGenerationProvider : IVideoGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public StableDiffusionApiVideoGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
    }

    private StableDiffusionApiVideoConfig Config => _configMonitor.CurrentValue.Video.StableDiffusionApi;

    public VideoProviderType ProviderType => VideoProviderType.StableDiffusionApi;
    public string DisplayName => "Stable Diffusion API";
    public bool IsConfigured => Config.Enabled && !string.IsNullOrWhiteSpace(Config.ApiKey);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "text2video-v5"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.VideoGeneration, "Seconds: 2-6", "video/mp4")
    };

    public async Task GenerateAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var cfg = Config;
        if (!IsConfigured)
            throw new InvalidOperationException("Stable Diffusion API 视频生成未配置。");

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(cfg.Endpoint),
            Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds)
        };

        var payload = new Dictionary<string, object?>
        {
            ["key"] = cfg.ApiKey,
            ["prompt"] = BuildPrompt(request.Shot),
            ["negative_prompt"] = cfg.NegativePrompt,
            ["scheduler"] = cfg.Scheduler,
            ["seconds"] = ResolveSeconds(request.Shot.Duration)
        };

        if (!string.IsNullOrWhiteSpace(request.Model))
            payload["model"] = request.Model;

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("/text2video", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Stable Diffusion API 视频生成失败: {responseBody}");

        using var doc = JsonDocument.Parse(responseBody);
        if (!doc.RootElement.TryGetProperty("output", out var output) ||
            output.ValueKind != JsonValueKind.Array ||
            output.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Stable Diffusion API 视频生成返回为空。");
        }

        var url = output[0].GetString();
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Stable Diffusion API 视频生成结果缺少输出地址。");

        using var downloadClient = new HttpClient();
        var bytes = await downloadClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(request.OutputPath, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildPrompt(Storyboard.Models.ShotItem shot)
    {
        var parts = new List<string>
        {
            shot.CoreContent,
            shot.SceneSettings,
            shot.ActionCommand,
            shot.FirstFramePrompt,
            shot.LastFramePrompt
        };

        var prompt = string.Join("，", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        return string.IsNullOrWhiteSpace(prompt) ? "Cinematic video." : prompt;
    }

    private static int ResolveSeconds(double duration)
    {
        if (duration <= 0)
            return 3;

        var target = (int)Math.Round(duration);
        return Math.Max(2, Math.Min(6, target));
    }
}
