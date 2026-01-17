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

/// <summary>
/// 视频生成模式
/// </summary>
public enum VideoGenerationMode
{
    /// <summary>
    /// 文本生成视频
    /// </summary>
    TextToVideo,

    /// <summary>
    /// 图片生成视频 - 基于首帧
    /// </summary>
    ImageToVideoFirstFrame,

    /// <summary>
    /// 图片生成视频 - 基于首尾帧
    /// </summary>
    ImageToVideoFirstLastFrame,

    /// <summary>
    /// 图片生成视频 - 基于参考图
    /// </summary>
    ImageToVideoReference
}

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
        _logger.LogInformation("Starting video generation for Shot {ShotNumber}, Model: {Model}, OutputPath: {OutputPath}",
            request.Shot.ShotNumber, request.Model, request.OutputPath);

        var shot = request.Shot;

        // Determine generation mode based on user configuration
        var mode = DetermineGenerationMode(shot);

        _logger.LogInformation("Video generation mode: {Mode}", mode);

        switch (mode)
        {
            case VideoGenerationMode.TextToVideo:
                await GenerateTextToVideoAsync(request, cancellationToken);
                break;

            case VideoGenerationMode.ImageToVideoFirstFrame:
                await GenerateImageToVideoFirstFrameAsync(request, cancellationToken);
                break;

            case VideoGenerationMode.ImageToVideoFirstLastFrame:
                await GenerateImageToVideoFirstLastFrameAsync(request, cancellationToken);
                break;

            case VideoGenerationMode.ImageToVideoReference:
                await GenerateImageToVideoReferenceAsync(request, cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"Unsupported video generation mode: {mode}");
        }
    }

    /// <summary>
    /// 文本生成视频 (Text-to-Video)
    /// 适用模型: Seedance 1.5 Pro, 1.0 Pro, 1.0 Lite T2V
    /// </summary>
    private async Task GenerateTextToVideoAsync(VideoGenerationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating video from text prompt");

        var shot = request.Shot;
        var prompt = BuildPrompt(shot);

        var contentItems = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["type"] = "text",
                ["text"] = prompt
            }
        };

        await ExecuteVideoGenerationAsync(request, contentItems, cancellationToken);
    }

    /// <summary>
    /// 图片生成视频 - 基于首帧 (Image-to-Video with First Frame)
    /// 适用模型: Seedance 1.5 Pro, 1.0 Pro, 1.0 Lite I2V
    /// 支持音频生成 (仅 1.5 Pro)
    /// </summary>
    private async Task GenerateImageToVideoFirstFrameAsync(VideoGenerationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating video from first frame image");

        var shot = request.Shot;
        var prompt = BuildPrompt(shot);

        if (string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) || !File.Exists(shot.FirstFrameImagePath))
            throw new InvalidOperationException("首帧图片路径无效或文件不存在");

        _logger.LogInformation("首帧图片路径: {Path}", shot.FirstFrameImagePath);
        _logger.LogInformation("正在将图片转换为 Data URL...");

        string dataUrl;
        try
        {
            dataUrl = ToDataUrl(shot.FirstFrameImagePath);
            _logger.LogInformation("图片转换成功，Data URL 长度: {Length}", dataUrl.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "转换图片为 Data URL 时发生异常: {Message}", ex.Message);
            throw new InvalidOperationException($"无法读取首帧图片: {ex.Message}", ex);
        }

        var contentItems = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["type"] = "text",
                ["text"] = prompt
            },
            new()
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = dataUrl
                },
                ["role"] = "first_frame"
            }
        };

        _logger.LogInformation("准备调用 ExecuteVideoGenerationAsync...");
        await ExecuteVideoGenerationAsync(request, contentItems, cancellationToken);
    }

    /// <summary>
    /// 图片生成视频 - 基于首尾帧 (Image-to-Video with First and Last Frame)
    /// 适用模型: Seedance 1.5 Pro, 1.0 Pro, 1.0 Lite I2V
    /// 支持音频生成 (仅 1.5 Pro)
    /// 实现首尾帧之间的平滑过渡动画
    /// </summary>
    private async Task GenerateImageToVideoFirstLastFrameAsync(VideoGenerationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating video from first and last frame images");

        var shot = request.Shot;
        var prompt = BuildPrompt(shot);

        if (string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) || !File.Exists(shot.FirstFrameImagePath))
            throw new InvalidOperationException("首帧图片路径无效或文件不存在");

        if (string.IsNullOrWhiteSpace(shot.LastFrameImagePath) || !File.Exists(shot.LastFrameImagePath))
            throw new InvalidOperationException("尾帧图片路径无效或文件不存在");

        var contentItems = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["type"] = "text",
                ["text"] = prompt
            },
            new()
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = ToDataUrl(shot.FirstFrameImagePath)
                },
                ["role"] = "first_frame"
            },
            new()
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = ToDataUrl(shot.LastFrameImagePath)
                },
                ["role"] = "last_frame"
            }
        };

        await ExecuteVideoGenerationAsync(request, contentItems, cancellationToken);
    }

    /// <summary>
    /// 图片生成视频 - 基于参考图 (Image-to-Video with Reference Images)
    /// 适用模型: Seedance 1.0 Lite I2V
    /// 支持 1-4 张参考图，精准提取参考图的关键特征
    /// 注意: 参考图模式不支持 resolution, ratio, camera_fixed 参数
    /// </summary>
    private async Task GenerateImageToVideoReferenceAsync(VideoGenerationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating video from reference images");

        var shot = request.Shot;
        var prompt = BuildPrompt(shot);

        // TODO: 需要在 ShotItem 中添加 ReferenceImagePaths 属性来支持多张参考图
        // 目前暂时使用首帧图作为参考图
        if (string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) || !File.Exists(shot.FirstFrameImagePath))
            throw new InvalidOperationException("参考图片路径无效或文件不存在");

        var contentItems = new List<Dictionary<string, object?>>
        {
            new()
            {
                ["type"] = "text",
                ["text"] = prompt
            },
            new()
            {
                ["type"] = "image_url",
                ["image_url"] = new Dictionary<string, object?>
                {
                    ["url"] = ToDataUrl(shot.FirstFrameImagePath)
                },
                ["role"] = "reference_image"
            }
        };

        // 参考图模式需要特殊处理参数
        await ExecuteVideoGenerationAsync(request, contentItems, cancellationToken, isReferenceMode: true);
    }

    /// <summary>
    /// 执行视频生成的核心方法
    /// </summary>
    private async Task ExecuteVideoGenerationAsync(
        VideoGenerationRequest request,
        List<Dictionary<string, object?>> contentItems,
        CancellationToken cancellationToken,
        bool isReferenceMode = false)
    {
        _logger.LogInformation("ExecuteVideoGenerationAsync 开始执行");

        var providerConfig = ProviderConfig;
        if (!IsConfigured)
            throw new InvalidOperationException("Volcengine video generation is not configured.");

        var model = string.IsNullOrWhiteSpace(request.Model)
            ? providerConfig.DefaultModels.Video
            : request.Model;
        if (string.IsNullOrWhiteSpace(model))
            throw new InvalidOperationException("No video model configured for Volcengine.");

        _logger.LogInformation("正在构建请求 payload...");
        var payload = BuildPayload(model, contentItems, request.Shot.Duration, request.Shot, isReferenceMode);
        _logger.LogInformation("Payload 构建完成");

        _logger.LogInformation("正在创建 HTTP 客户端...");
        using var httpClient = new HttpClient
        {
            BaseAddress = BuildBaseAddress(providerConfig.Endpoint),
            Timeout = TimeSpan.FromSeconds(providerConfig.TimeoutSeconds)
        };

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);

        _logger.LogInformation("正在序列化 payload 并发送请求到火山引擎 API...");
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        try
        {
            var response = await httpClient.PostAsync("contents/generations/tasks", content, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("收到 API 响应，状态码: {StatusCode}", response.StatusCode);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Video generation task creation response. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = ExtractErrorMessage(responseBody);
                // 创建一个不包含 base64 图片数据的 payload 副本用于日志
                var payloadForLog = CreatePayloadForLogging(payload);
                _logger.LogError("Video generation failed for model {Model}. Error: {Error}. Payload: {Payload}",
                    model, errorMessage, JsonSerializer.Serialize(payloadForLog));
                throw new InvalidOperationException($"视频生成失败 (模型: {model}): {errorMessage}\n\n提示：不同模型支持的参数不同，请检查模型文档或调整配置参数。");
            }

            var taskId = ExtractTaskId(responseBody);
            if (string.IsNullOrWhiteSpace(taskId))
                throw new InvalidOperationException("Volcengine video generation did not return a task id.");

            _logger.LogInformation("任务已创建，TaskId: {TaskId}，正在轮询状态...", taskId);

            var status = ExtractStatus(responseBody);
            if (!string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
            {
                status = await PollStatusAsync(httpClient, taskId, providerConfig.TimeoutSeconds, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Volcengine video generation did not succeed. Status: {status}");

            _logger.LogInformation("任务已完成，正在获取视频 URL...");
            var finalBody = await GetTaskAsync(httpClient, taskId, cancellationToken).ConfigureAwait(false);
            var videoUrl = ExtractVideoUrl(finalBody);
            if (string.IsNullOrWhiteSpace(videoUrl))
                throw new InvalidOperationException("Volcengine video generation result did not include a video url.");

            _logger.LogInformation("Video generation final result. TaskId: {TaskId}, VideoUrl: {VideoUrl}", taskId, videoUrl);

            _logger.LogInformation("正在下载视频文件...");
            var videoBytes = await DownloadBytesAsync(videoUrl, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("视频下载完成，大小: {Size} bytes，正在保存到文件...", videoBytes.Length);

            await File.WriteAllBytesAsync(request.OutputPath, videoBytes, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Video generation completed. OutputPath: {OutputPath}, BytesLength: {Length}", request.OutputPath, videoBytes.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecuteVideoGenerationAsync 执行过程中发生异常: {Message}", ex.Message);
            throw;
        }
    }

    /// <summary>
    /// 删除或取消视频生成任务
    /// </summary>
    public async Task<bool> CancelTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Cancelling video generation task: {TaskId}", taskId);

        var providerConfig = ProviderConfig;
        if (!IsConfigured)
            throw new InvalidOperationException("Volcengine video generation is not configured.");

        using var httpClient = new HttpClient
        {
            BaseAddress = BuildBaseAddress(providerConfig.Endpoint),
            Timeout = TimeSpan.FromSeconds(providerConfig.TimeoutSeconds)
        };

        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey);

        try
        {
            var response = await httpClient.DeleteAsync($"contents/generations/tasks/{taskId}", cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Task cancellation response. Status: {Status}, Body: {Body}", response.StatusCode, responseBody);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully cancelled task: {TaskId}", taskId);
                return true;
            }
            else
            {
                var errorMessage = ExtractErrorMessage(responseBody);
                _logger.LogWarning("Failed to cancel task {TaskId}: {Error}", taskId, errorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while cancelling task {TaskId}", taskId);
            return false;
        }
    }

    /// <summary>
    /// 确定视频生成模式
    /// </summary>
    private static VideoGenerationMode DetermineGenerationMode(ShotItem shot)
    {
        // 优先级: 参考图 > 首尾帧 > 首帧 > 纯文本

        // 如果用户明确选择使用参考图模式
        if (shot.UseReferenceImages && !string.IsNullOrWhiteSpace(shot.FirstFrameImagePath))
        {
            return VideoGenerationMode.ImageToVideoReference;
        }

        // 如果同时有首帧和尾帧
        if (shot.UseFirstFrameReference && shot.UseLastFrameReference &&
            !string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) &&
            !string.IsNullOrWhiteSpace(shot.LastFrameImagePath))
        {
            return VideoGenerationMode.ImageToVideoFirstLastFrame;
        }

        // 如果只有首帧
        if (shot.UseFirstFrameReference && !string.IsNullOrWhiteSpace(shot.FirstFrameImagePath))
        {
            return VideoGenerationMode.ImageToVideoFirstFrame;
        }

        // 默认为纯文本生成
        return VideoGenerationMode.TextToVideo;
    }

    private Dictionary<string, object?> BuildPayload(
        string model,
        List<Dictionary<string, object?>> contentItems,
        double shotDuration,
        ShotItem shot,
        bool isReferenceMode = false)
    {
        var config = VideoConfig;

        // Decide task_type based on model name or presence of image items.
        var hasImageItems = contentItems.Any(ci => ci.TryGetValue("type", out var t) && string.Equals(t as string, "image_url", StringComparison.OrdinalIgnoreCase));
        var modelLower = (model ?? string.Empty).ToLowerInvariant();
        string taskType;
        if (modelLower.Contains("i2v"))
            taskType = "i2v";
        else if (modelLower.Contains("t2v") || modelLower.Contains("text2video"))
            taskType = "t2v";
        else
            taskType = hasImageItems ? "i2v" : "t2v";

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["task_type"] = taskType,
            ["content"] = contentItems
        };

        // Strategy: Send all parameters that user configured, let the API decide what's valid
        // Different models support different parameters, and we can't maintain all combinations
        // If API rejects a parameter, it will return a clear error message

        // 参考图模式不支持 resolution, ratio, camera_fixed 参数
        // lite-i2v 模型不支持 resolution 参数
        var isLiteI2vModel = modelLower.Contains("lite") && modelLower.Contains("i2v");

        if (!isReferenceMode)
        {
            // Resolution - lite-i2v 模型不支持此参数
            if (!isLiteI2vModel)
            {
                var resolution = !string.IsNullOrWhiteSpace(shot.VideoResolution) ? shot.VideoResolution : config.Resolution;
                if (!string.IsNullOrWhiteSpace(resolution))
                    payload["resolution"] = resolution.Trim();
            }

            var ratio = !string.IsNullOrWhiteSpace(shot.VideoRatio) ? shot.VideoRatio : config.Ratio;
            if (!string.IsNullOrWhiteSpace(ratio))
                payload["ratio"] = ratio.Trim();

            // Camera fixed from shot or config
            if (shot.CameraFixed || (config.CameraFixed ?? false))
                payload["camera_fixed"] = true;
        }

        // Duration and Frames are mutually exclusive - prefer frames if specified
        var frames = shot.VideoFrames > 0 ? shot.VideoFrames : (config.Frames ?? 0);
        if (frames > 0)
        {
            payload["frames"] = frames;
        }
        else
        {
            // Only use duration if frames is not specified
            var duration = ResolveDurationSeconds(config.DurationSeconds, shotDuration);
            if (duration > 0)
                payload["duration"] = duration;
        }

        // Seed from shot or config
        var seed = shot.Seed ?? config.Seed;
        if (seed.HasValue)
            payload["seed"] = seed.Value;

        // Watermark from shot or config
        var watermark = shot.Watermark || config.Watermark;
        payload["watermark"] = watermark;

        payload["return_last_frame"] = config.ReturnLastFrame;

        if (!string.IsNullOrWhiteSpace(config.ServiceTier))
            payload["service_tier"] = config.ServiceTier.Trim();

        // Generate audio - only add if user wants it and model supports it
        if (shot.GenerateAudio && ModelSupportsGenerateAudio(model))
            payload["generate_audio"] = true;

        payload["draft"] = config.Draft;

        // Add negative prompt if available
        if (!string.IsNullOrWhiteSpace(shot.VideoNegativePrompt))
            payload["negative_prompt"] = shot.VideoNegativePrompt.Trim();

        return payload;
    }

    private static string BuildPrompt(ShotItem shot)
    {
        // Use VideoPrompt if available, otherwise build from other fields
        if (!string.IsNullOrWhiteSpace(shot.VideoPrompt))
        {
            var parts = new List<string> { shot.VideoPrompt.Trim() };

            // Add professional parameters if available
            if (!string.IsNullOrWhiteSpace(shot.CameraMovement))
                parts.Add($"运镜: {shot.CameraMovement}");
            if (!string.IsNullOrWhiteSpace(shot.ShootingStyle))
                parts.Add($"风格: {shot.ShootingStyle}");
            if (!string.IsNullOrWhiteSpace(shot.VideoEffect))
                parts.Add($"特效: {shot.VideoEffect}");

            return string.Join(", ", parts);
        }

        // Fallback to building from individual fields
        var fallbackParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(shot.SceneDescription))
            fallbackParts.Add(shot.SceneDescription);
        if (!string.IsNullOrWhiteSpace(shot.ActionDescription))
            fallbackParts.Add(shot.ActionDescription);
        if (!string.IsNullOrWhiteSpace(shot.StyleDescription))
            fallbackParts.Add(shot.StyleDescription);
        if (!string.IsNullOrWhiteSpace(shot.CoreContent))
            fallbackParts.Add(shot.CoreContent);
        if (!string.IsNullOrWhiteSpace(shot.SceneSettings))
            fallbackParts.Add(shot.SceneSettings);
        if (!string.IsNullOrWhiteSpace(shot.ActionCommand))
            fallbackParts.Add(shot.ActionCommand);

        var prompt = string.Join(", ", fallbackParts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        return string.IsNullOrWhiteSpace(prompt) ? "Cinematic video." : prompt;
    }

    private static int ResolveDurationSeconds(double configDuration, double shotDuration)
    {
        var duration = configDuration > 0 ? configDuration : shotDuration;
        if (duration <= 0)
            return 0;

        return (int)Math.Round(duration);
    }

    private static string ExtractErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                var message = error.TryGetProperty("message", out var msg) ? msg.GetString() : null;
                var code = error.TryGetProperty("code", out var c) ? c.GetString() : null;
                var param = error.TryGetProperty("param", out var p) ? p.GetString() : null;

                if (!string.IsNullOrWhiteSpace(message))
                {
                    if (!string.IsNullOrWhiteSpace(param))
                        return $"{message} (参数: {param})";
                    if (!string.IsNullOrWhiteSpace(code))
                        return $"{message} (错误码: {code})";
                    return message;
                }
            }
        }
        catch
        {
            // If parsing fails, return the raw response
        }
        return json;
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
        var lastStatus = string.Empty;

        while (DateTimeOffset.UtcNow - start < timeout)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            var body = await GetTaskAsync(httpClient, taskId, cancellationToken).ConfigureAwait(false);
            var status = ExtractStatus(body);
            lastStatus = status;

            if (string.Equals(status, "succeeded", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "failed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "expired", StringComparison.OrdinalIgnoreCase))
            {
                return status;
            }
        }

        // Timeout occurred - throw exception with detailed information
        var elapsed = DateTimeOffset.UtcNow - start;
        throw new TimeoutException(
            $"视频生成任务 {taskId} 在 {elapsed.TotalSeconds:F1} 秒后超时。" +
            $"最后已知状态: {lastStatus}。" +
            $"请稍后使用任务ID手动查询状态，或增加超时时间配置。");
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

    private static bool ModelSupportsGenerateAudio(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return false;

        return model.Contains("seedance-1-5-pro", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 创建用于日志记录的 payload 副本，将 base64 图片数据替换为占位符
    /// </summary>
    private static Dictionary<string, object?> CreatePayloadForLogging(Dictionary<string, object?> payload)
    {
        var logPayload = new Dictionary<string, object?>(payload);

        if (logPayload.TryGetValue("content", out var contentObj) && contentObj is List<Dictionary<string, object?>> contentList)
        {
            var logContentList = new List<Dictionary<string, object?>>();
            foreach (var item in contentList)
            {
                var logItem = new Dictionary<string, object?>(item);

                // 如果是图片类型，替换 base64 数据
                if (logItem.TryGetValue("type", out var type) && type is string typeStr && typeStr == "image_url")
                {
                    if (logItem.TryGetValue("image_url", out var imageUrlObj) && imageUrlObj is Dictionary<string, object?> imageUrl)
                    {
                        var logImageUrl = new Dictionary<string, object?>(imageUrl);
                        if (logImageUrl.TryGetValue("url", out var url) && url is string urlStr && urlStr.StartsWith("data:"))
                        {
                            // 替换 base64 数据为占位符
                            var mimeType = urlStr.Split(';')[0].Replace("data:", "");
                            logImageUrl["url"] = $"<base64-image:{mimeType}>";
                        }
                        logItem["image_url"] = logImageUrl;
                    }
                }

                logContentList.Add(logItem);
            }
            logPayload["content"] = logContentList;
        }

        return logPayload;
    }
}
