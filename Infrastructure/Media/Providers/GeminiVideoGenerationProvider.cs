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

public sealed class GeminiVideoGenerationProvider : IVideoGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public GeminiVideoGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
    }

    private GeminiVideoConfig Config => _configMonitor.CurrentValue.Video.Gemini;

    public VideoProviderType ProviderType => VideoProviderType.Gemini;
    public string DisplayName => "Gemini (Veo)";
    public bool IsConfigured => Config.Enabled
        && !string.IsNullOrWhiteSpace(Config.ApiKey)
        && !string.IsNullOrWhiteSpace(Config.DefaultModel);

    public IReadOnlyList<string> SupportedModels => new[]
    {
        "veo-2.0-generate-001",
        "veo-3.1-generate-001"
    };

    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.VideoGeneration, "Seconds: 4/8/12", "video/mp4")
    };

    public async Task GenerateAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var cfg = Config;
        if (!IsConfigured)
            throw new InvalidOperationException("Gemini 视频生成未配置。");

        var model = string.IsNullOrWhiteSpace(request.Model) ? cfg.DefaultModel : request.Model;
        var prompt = BuildPrompt(request.Shot);
        var seconds = ResolveSeconds(request.Shot.Duration);

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(cfg.Endpoint),
            Timeout = TimeSpan.FromSeconds(cfg.TimeoutSeconds)
        };

        var payload = new Dictionary<string, object?>
        {
            ["prompt"] = prompt,
            ["seconds"] = seconds
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(
            $"/models/{model}:generateVideo?key={cfg.ApiKey}",
            content,
            cancellationToken).ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Gemini 视频生成失败: {responseBody}");

        if (TryExtractVideo(responseBody, out var bytes, out var url))
        {
            await WriteVideoAsync(request.OutputPath, bytes, url, cancellationToken).ConfigureAwait(false);
            return;
        }

        var operationName = ExtractOperationName(responseBody);
        if (string.IsNullOrWhiteSpace(operationName))
            throw new InvalidOperationException("Gemini 视频生成返回缺少任务信息。");

        var finalBody = await PollOperationAsync(httpClient, operationName, cfg.ApiKey, cancellationToken).ConfigureAwait(false);
        if (!TryExtractVideo(finalBody, out var finalBytes, out var finalUrl))
            throw new InvalidOperationException("Gemini 视频生成未返回可下载的结果。");

        await WriteVideoAsync(request.OutputPath, finalBytes, finalUrl, cancellationToken).ConfigureAwait(false);
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
            return 4;

        var target = (int)Math.Round(duration);
        var options = new[] { 4, 8, 12 };
        return options.OrderBy(v => Math.Abs(v - target)).First();
    }

    private static bool TryExtractVideo(string json, out byte[]? bytes, out string? url)
    {
        bytes = null;
        url = null;

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (TryExtractFromElement(root, out bytes, out url))
            return true;

        if (root.TryGetProperty("response", out var response) && TryExtractFromElement(response, out bytes, out url))
            return true;

        return false;
    }

    private static bool TryExtractFromElement(JsonElement element, out byte[]? bytes, out string? url)
    {
        bytes = null;
        url = null;

        if (element.TryGetProperty("video", out var video) ||
            element.TryGetProperty("videos", out video))
        {
            if (video.ValueKind == JsonValueKind.Array && video.GetArrayLength() > 0)
                video = video[0];

            if (video.ValueKind == JsonValueKind.Object)
            {
                if (TryReadBase64(video, "data", out bytes) ||
                    TryReadBase64(video, "videoBytes", out bytes) ||
                    TryReadBase64(video, "bytesBase64Encoded", out bytes))
                {
                    return true;
                }

                if (TryReadString(video, "url", out url) ||
                    TryReadString(video, "uri", out url))
                {
                    return true;
                }
            }
        }

        if (element.TryGetProperty("output", out var output) &&
            output.ValueKind == JsonValueKind.Array &&
            output.GetArrayLength() > 0 &&
            output[0].ValueKind == JsonValueKind.String)
        {
            url = output[0].GetString();
            return !string.IsNullOrWhiteSpace(url);
        }

        return false;
    }

    private static bool TryReadBase64(JsonElement element, string property, out byte[]? bytes)
    {
        bytes = null;
        if (element.TryGetProperty(property, out var data) && data.ValueKind == JsonValueKind.String)
        {
            var value = data.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                bytes = Convert.FromBase64String(value);
                return bytes.Length > 0;
            }
        }

        return false;
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

    private static string? ExtractOperationName(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            return name.GetString();

        if (root.TryGetProperty("operation", out var operation) &&
            operation.TryGetProperty("name", out var opName) &&
            opName.ValueKind == JsonValueKind.String)
            return opName.GetString();

        return null;
    }

    private static async Task<string> PollOperationAsync(
        HttpClient httpClient,
        string operationName,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(120);
        var start = DateTimeOffset.UtcNow;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            var response = await httpClient.GetAsync($"/{operationName}?key={apiKey}", cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                continue;

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("done", out var done) && done.ValueKind == JsonValueKind.True)
                return body;
        }

        throw new InvalidOperationException("Gemini 视频生成超时。");
    }

    private static async Task WriteVideoAsync(
        string outputPath,
        byte[]? bytes,
        string? url,
        CancellationToken cancellationToken)
    {
        if (bytes != null && bytes.Length > 0)
        {
            await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            using var httpClient = new HttpClient();
            var data = await httpClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
            await File.WriteAllBytesAsync(outputPath, data, cancellationToken).ConfigureAwait(false);
            return;
        }

        throw new InvalidOperationException("Gemini 视频生成结果为空。");
    }
}
