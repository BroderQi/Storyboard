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
using Storyboard.Models;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class VolcengineVideoGenerationProvider : IVideoGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;
    private readonly ILogger<VolcengineVideoGenerationProvider> _logger;

    public VolcengineVideoGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor, ILogger<VolcengineVideoGenerationProvider> logger)
    {
        _configMonitor = configMonitor;
        _logger = logger;
    }

    private AIProviderConfiguration ProviderConfig => _configMonitor.CurrentValue.Providers.Volcengine;
    private VolcengineVideoConfig VideoConfig => _configMonitor.CurrentValue.Video.Volcengine;

    public VideoProviderType ProviderType => VideoProviderType.Volcengine;
    public string DisplayName => "Volcengine";
    public bool IsConfigured => ProviderConfig.Enabled
        && !string.IsNullOrWhiteSpace(ProviderConfig.ApiKey)
        && !string.IsNullOrWhiteSpace(ProviderConfig.Endpoint);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "doubao-seedance-1-5-pro-251215",
        "doubao-seedance-1-0-pro-250528",
        "doubao-seedance-1-0-pro-fast-250521",
        "doubao-seedance-1-0-lite-250521"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.VideoGeneration, "Async task", "video/mp4")
    };

    public async Task GenerateAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting video generation. Request: {Request}", JsonSerializer.Serialize(request));

        var providerConfig = ProviderConfig;
        if (!IsConfigured)
            throw new InvalidOperationException("Volcengine video generation is not configured.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? providerConfig.DefaultModels.Video
            : request.Model;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("No video model configured for Volcengine.");

        var prompt = BuildPrompt(request.Shot);
        var contentItems = BuildContentItems(prompt, request.Shot);
        var payload = BuildPayload(model, contentItems, request.Shot.Duration);

        using var httpClient = new HttpClient
        {
            BaseAddress = BuildBaseAddress(providerConfig.Endpoint),
            Timeout = TimeSpan.FromSeconds(providerConfig.TimeoutSeconds)
        };

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync("contents/generations/tasks", content, cancellationToken).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Video generation task creation response. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Volcengine video generation failed: {responseBody}");

        var taskId = ExtractTaskId(responseBody);
        if (string.IsNullOrWhiteSpace(taskId))
            throw new InvalidOperationException("Volcengine video generation did not return a task id.");

        var status = ExtractStatus(responseBody);
        if (!string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            status = await PollStatusAsync(httpClient, taskId, providerConfig.TimeoutSeconds, cancellationToken)
                .ConfigureAwait(false);
        }

        if (!string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Volcengine video generation did not succeed. Status: {status}");

        var finalBody = await GetTaskAsync(httpClient, taskId, cancellationToken).ConfigureAwait(false);
        var videoUrl = ExtractVideoUrl(finalBody);
        if (string.IsNullOrWhiteSpace(videoUrl))
            throw new InvalidOperationException("Volcengine video generation result did not include a video url.");

        _logger.LogInformation("Video generation final result. TaskId: {TaskId}, VideoUrl: {VideoUrl}", taskId, videoUrl);

        var videoBytes = await DownloadBytesAsync(videoUrl, cancellationToken).ConfigureAwait(false);
        await File.WriteAllBytesAsync(request.OutputPath, videoBytes, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Video generation completed. OutputPath: {OutputPath}, BytesLength: {Length}", request.OutputPath, videoBytes.Length);
    }

    private Dictionary<string, object?> BuildPayload(
        string model,
        List<Dictionary<string, object?>> contentItems,
        double shotDuration)
    {
        var config = VideoConfig;
        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["content"] = contentItems
        };

        if (!string.IsNullOrWhiteSpace(config.Resolution))
            payload["resolution"] = config.Resolution.Trim();

        if (!string.IsNullOrWhiteSpace(config.Ratio))
            payload["ratio"] = config.Ratio.Trim();

        var duration = ResolveDurationSeconds(config.DurationSeconds, shotDuration);
        if (duration > 0)
            payload["duration"] = duration;

        if (config.Frames.HasValue && config.Frames.Value > 0)
            payload["frames"] = config.Frames.Value;

        if (config.Seed.HasValue)
            payload["seed"] = config.Seed.Value;

        if (config.CameraFixed.HasValue)
            payload["camera_fixed"] = config.CameraFixed.Value;

        payload["watermark"] = config.Watermark;
        payload["return_last_frame"] = config.ReturnLastFrame;

        if (!string.IsNullOrWhiteSpace(config.ServiceTier))
            payload["service_tier"] = config.ServiceTier.Trim();

        payload["generate_audio"] = config.GenerateAudio;
        payload["draft"] = config.Draft;

        return payload;
    }

    private static List<Dictionary<string, object?>> BuildContentItems(string prompt, ShotItem shot)
    {
        var items = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["type"] = "text",
                ["text"] = prompt
            }
        };

        if (!string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) && File.Exists(shot.FirstFrameImagePath))
        {
            items.Add(new Dictionary<string, object?>
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = ToDataUrl(shot.FirstFrameImagePath)
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(shot.LastFrameImagePath) && File.Exists(shot.LastFrameImagePath))
        {
            items.Add(new Dictionary<string, object?>
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = ToDataUrl(shot.LastFrameImagePath)
                }
            });
        }

        return items;
    }

    private static string BuildPrompt(ShotItem shot)
    {
        var parts = new List<string>
        {
            shot.CoreContent,
            shot.SceneSettings,
            shot.ActionCommand,
            shot.FirstFramePrompt,
            shot.LastFramePrompt
        };

        var prompt = string.Join(", ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        return string.IsNullOrWhiteSpace(prompt) ? "Cinematic video." : prompt;
    }

    private static int ResolveDurationSeconds(double configDuration, double shotDuration)
    {
        var duration = configDuration > 0 ? configDuration : shotDuration;
        if (duration <= 0)
            return 0;

        return (int)Math.Round(duration);
    }

    private static string? ExtractTaskId(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    private static string ExtractStatus(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("status", out var status)
            ? status.GetString() ?? string.Empty
            : string.Empty;
    }

    private static async Task<string> PollStatusAsync(
        HttpClient httpClient,
        string taskId,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(Math.Max(30, timeoutSeconds));
        var start = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            var body = await GetTaskAsync(httpClient, taskId, cancellationToken).ConfigureAwait(false);
            var status = ExtractStatus(body);

            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }
        }

        return "timeout";
    }

    private static async Task<string> GetTaskAsync(HttpClient httpClient, string taskId, CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync($"contents/generations/tasks/{taskId}", cancellationToken)
            .ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string? ExtractVideoUrl(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("content", out var content) &&
            content.TryGetProperty("video_url", out var videoUrl) &&
            videoUrl.ValueKind == JsonValueKind.String)
        {
            return videoUrl.GetString();
        }

        return null;
    }

    private static async Task<byte[]> DownloadBytesAsync(string url, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();
        return await httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
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

    private static Uri BuildBaseAddress(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new InvalidOperationException("Endpoint is required.");

        var normalized = endpoint.TrimEnd('/');
        if (normalized.EndsWith("/api/v3", StringComparison.OrdinalIgnoreCase))
            return new Uri($"{normalized}/");

        return new Uri($"{normalized}/api/v3/");
    }
}
