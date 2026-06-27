using Microsoft.Extensions.Logging;
using Storyboard.AI.Core;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Storyboard.AI.Providers;

/// <summary>
/// TwelveLabs Pegasus 视频理解的最小 REST 客户端。
/// .NET 无官方 SDK，遵循其它 provider 的做法直接调用 v1.3 REST。
///
/// 流程：上传本地视频为 asset（POST /assets，method=direct）→ 用 asset_id 调用
/// POST /analyze 拿到自然语言描述。Pegasus 1.5 不接受裸 video_id，必须用 url 或 asset_id。
///
/// Minimal REST client for TwelveLabs Pegasus. There is no official .NET SDK, so this
/// follows the existing providers' pattern and calls the v1.3 REST API directly.
/// </summary>
public sealed class TwelveLabsPegasusClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TwelveLabsOptions _options;
    private readonly ILogger _logger;

    public TwelveLabsPegasusClient(TwelveLabsOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    private HttpClient CreateClient()
    {
        var baseAddress = _options.Endpoint.EndsWith('/') ? _options.Endpoint : _options.Endpoint + "/";
        var client = new HttpClient
        {
            BaseAddress = new Uri(baseAddress),
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds)
        };
        client.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        return client;
    }

    /// <summary>
    /// 上传本地视频，返回处于 ready 状态的 asset_id；失败返回 null。
    /// </summary>
    public async Task<string?> UploadAssetAsync(string videoPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(videoPath))
        {
            _logger.LogWarning("TwelveLabs: video file not found, skipping: {Path}", videoPath);
            return null;
        }

        var length = new FileInfo(videoPath).Length;
        if (length > _options.MaxUploadBytes)
        {
            _logger.LogWarning(
                "TwelveLabs: video {Path} is {Size}MB, exceeds direct-upload limit ({Limit}MB); skipping Pegasus enrichment.",
                videoPath, length / (1024 * 1024), _options.MaxUploadBytes / (1024 * 1024));
            return null;
        }

        using var client = CreateClient();
        await using var stream = File.OpenRead(videoPath);
        using var form = new MultipartFormDataContent
        {
            { new StringContent("direct"), "method" }
        };
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        form.Add(fileContent, "file", Path.GetFileName(videoPath));

        using var response = await client.PostAsync("assets", form, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TwelveLabs asset upload failed ({Status}): {Body}", (int)response.StatusCode, body);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var assetId = root.TryGetProperty("_id", out var id1) ? id1.GetString()
            : root.TryGetProperty("asset_id", out var id2) ? id2.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(assetId))
        {
            _logger.LogWarning("TwelveLabs asset upload returned no id: {Body}", body);
            return null;
        }

        return await WaitForAssetReadyAsync(client, assetId!, cancellationToken).ConfigureAwait(false) ? assetId : null;
    }

    private async Task<bool> WaitForAssetReadyAsync(HttpClient client, string assetId, CancellationToken cancellationToken)
    {
        // 轮询直至 ready（或超时窗口耗尽）。
        for (var attempt = 0; attempt < 60; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var response = await client.GetAsync($"assets/{assetId}", cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("TwelveLabs asset status check failed ({Status}): {Body}", (int)response.StatusCode, body);
                return false;
            }

            using var doc = JsonDocument.Parse(body);
            var status = doc.RootElement.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (string.Equals(status, "ready", StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("TwelveLabs asset {AssetId} failed to process.", assetId);
                return false;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("TwelveLabs asset {AssetId} did not become ready in time.", assetId);
        return false;
    }

    /// <summary>
    /// 对已上传的 asset 运行 Pegasus 分析，返回生成文本；失败返回 null。
    /// startTime/endTime 为可选的剪辑窗口（Pegasus 要求窗口 ≥ 4 秒）。
    /// </summary>
    public async Task<string?> AnalyzeAssetAsync(
        string assetId,
        string prompt,
        double? startTime,
        double? endTime,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model_name"] = _options.Model,
            ["video"] = new Dictionary<string, object?> { ["type"] = "asset_id", ["asset_id"] = assetId },
            ["prompt"] = prompt,
            ["max_tokens"] = 512,
            // 非流式：返回单个 {"id","data",...} 对象（data 即生成文本）。
            // 默认流式会返回 NDJSON 事件流，这里不需要。
            ["stream"] = false
        };

        // Pegasus 要求分析窗口 ≥ 4 秒，否则不附加窗口（分析整段）。
        if (startTime is { } start && endTime is { } end && end - start >= 4.0)
        {
            payload["start_time"] = start;
            payload["end_time"] = end;
        }

        using var client = CreateClient();
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("analyze", content, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TwelveLabs analyze failed ({Status}): {Body}", (int)response.StatusCode, body);
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        var text = doc.RootElement.TryGetProperty("data", out var data) ? data.GetString() : null;
        return string.IsNullOrWhiteSpace(text) ? null : text!.Trim();
    }
}
