using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Linq;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class OpenAIVideoGenerationProvider : IVideoGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public OpenAIVideoGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
    }

    private OpenAIVideoConfig Config => _configMonitor.CurrentValue.Video.OpenAI;

    public VideoProviderType ProviderType => VideoProviderType.OpenAI;
    public string DisplayName => "OpenAI (Sora)";
    public bool IsConfigured => Config.Enabled
        && !string.IsNullOrWhiteSpace(Config.ApiKey)
        && !string.IsNullOrWhiteSpace(Config.DefaultModel);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "sora-2",
        "sora-2-pro",
        "sora-2-2025-10-06",
        "sora-2-pro-2025-10-06",
        "sora-2-2025-12-08"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.VideoGeneration, "Seconds: 4/8/12", "video/mp4")
    };

    public async Task GenerateAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var cfg = Config;
        if (!IsConfigured)
            throw new InvalidOperationException("OpenAI 视频生成未配置。");

        var model = string.IsNullOrWhiteSpace(request.Model) ? cfg.DefaultModel : request.Model;
        var prompt = BuildPrompt(request.Shot);
        var seconds = ResolveSeconds(request.Shot.Duration);
        var size = ResolveSize(request.Width, request.Height);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(cfg.Endpoint),
            Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds)
        };

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey);

        var payload = new Dictionary<string, object?>
        {
            ["prompt"] = prompt,
            ["model"] = model,
            ["seconds"] = seconds,
            ["size"] = size
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("/videos", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OpenAI 视频生成失败: {responseBody}");

        var videoId = ExtractVideoId(responseBody);
        if (string.IsNullOrWhiteSpace(videoId))
            throw new InvalidOperationException("OpenAI 视频生成返回缺少任务 ID。");

        var status = ExtractStatus(responseBody);
        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
        {
            status = await PollStatusAsync(httpClient, videoId, cancellationToken).ConfigureAwait(false);
        }

        if (!string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"OpenAI 视频生成未完成，状态: {status}");

        var videoBytes = await DownloadVideoAsync(httpClient, videoId, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(request.OutputPath, videoBytes, cancellationToken).ConfigureAwait(false);
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

    private static string ResolveSeconds(double duration)
    {
        if (duration <= 0)
            return "4";

        var target = (int)Math.Round(duration);
        var options = new[] { 4, 8, 12 };
        var closest = options.OrderBy(v => Math.Abs(v - target)).First();
        return closest.ToString();
    }

    private static string ResolveSize(int width, int height)
    {
        if (width <= 0 || height <= 0)
            return "1280x720";

        var isPortrait = height > width;
        return isPortrait ? "720x1280" : "1280x720";
    }

    private static string? ExtractVideoId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    private static string ExtractStatus(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("status", out var status) ? status.GetString() ?? string.Empty : string.Empty;
    }

    private static async Task<string> PollStatusAsync(HttpClient httpClient, string videoId, CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(120);
        var start = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);

            var response = await httpClient.GetAsync($"/videos/{videoId}", cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                continue;

            var status = ExtractStatus(body);
            if (string.Equals(status, "completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }
        }

        return "timeout";
    }

    private static async Task<byte[]> DownloadVideoAsync(HttpClient httpClient, string videoId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/videos/{videoId}/content");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/binary"));

        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }
}
