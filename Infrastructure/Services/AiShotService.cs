using Microsoft.Extensions.Logging;
using Storyboard.AI;
using Storyboard.Application.Abstractions;
using Storyboard.Models;
using System.Text.Json;
using System.Text;
using SkiaSharp;

namespace Storyboard.Infrastructure.Services;

public sealed class AiShotService : IAiShotService
{
    private readonly AIServiceManager _ai;
    private readonly ILogger<AiShotService> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    public AiShotService(AIServiceManager ai, ILogger<AiShotService> logger)
    {
        _ai = ai;
        _logger = logger;
    }

    public async Task<AiShotDescription> AnalyzeShotAsync(
        AiShotAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync().ConfigureAwait(false);

        // 使用多模态消息，将素材图片直接发送给AI进行视觉分析
        var response = await _ai.ChatWithImageAsync(
            "shot_analysis",
            request.FirstFramePath,
            BuildExistingContext(request),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var json = ExtractJson(response);
        return ParseShotDescription(json);
    }

    public async Task<IReadOnlyList<AiShotDescription>> GenerateShotsFromTextAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return Array.Empty<AiShotDescription>();

        await EnsureInitializedAsync().ConfigureAwait(false);

        var parameters = new Dictionary<string, object>
        {
            ["story_text"] = prompt.Trim()
        };

        var response = await _ai.ChatAsync("text_to_shots", parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        var json = ExtractJson(response);
        return ParseShotList(json);
    }

    private async Task EnsureInitializedAsync()
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            await _ai.InitializeAsync().ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private static string BuildExistingContext(AiShotAnalysisRequest request)
    {
        var sb = new StringBuilder();
        AppendIfPresent(sb, "镜头类型", request.ExistingShotType);
        AppendIfPresent(sb, "核心画面", request.ExistingCoreContent);
        AppendIfPresent(sb, "动作指令", request.ExistingActionCommand);
        AppendIfPresent(sb, "场景设定", request.ExistingSceneSettings);
        AppendIfPresent(sb, "首帧提示词", request.ExistingFirstFramePrompt);
        AppendIfPresent(sb, "尾帧提示词", request.ExistingLastFramePrompt);
        return sb.Length == 0 ? "无" : sb.ToString();
    }

    private static void AppendIfPresent(StringBuilder sb, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        sb.Append(label);
        sb.Append(": ");
        sb.AppendLine(value.Trim());
    }

    private string DescribeImage(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            return "未提供图片";

        try
        {
            using var stream = File.OpenRead(imagePath);
            using var bitmap = SKBitmap.Decode(stream);
            if (bitmap == null)
                return "无法解析图片";

            var width = bitmap.Width;
            var height = bitmap.Height;
            var orientation = width >= height ? "横向" : "纵向";

            var (avgColor, brightness) = GetAverageColor(bitmap);
            var colorHex = $"#{avgColor.Red:X2}{avgColor.Green:X2}{avgColor.Blue:X2}";
            return $"尺寸 {width}x{height}，{orientation}，平均色 {colorHex}，亮度 {brightness:0.00}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取图片失败: {Path}", imagePath);
            return "图片读取失败";
        }
    }

    private static (SKColor Color, double Brightness) GetAverageColor(SKBitmap bitmap)
    {
        var stepX = Math.Max(1, bitmap.Width / 64);
        var stepY = Math.Max(1, bitmap.Height / 64);

        long r = 0;
        long g = 0;
        long b = 0;
        long count = 0;

        for (var y = 0; y < bitmap.Height; y += stepY)
        {
            for (var x = 0; x < bitmap.Width; x += stepX)
            {
                var color = bitmap.GetPixel(x, y);
                r += color.Red;
                g += color.Green;
                b += color.Blue;
                count++;
            }
        }

        if (count == 0)
            return (new SKColor(0, 0, 0), 0);

        var avg = new SKColor((byte)(r / count), (byte)(g / count), (byte)(b / count));
        var brightness = (0.299 * avg.Red + 0.587 * avg.Green + 0.114 * avg.Blue) / 255.0;
        return (avg, brightness);
    }

    private static string ExtractJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("AI 未返回内容。");

        try
        {
            JsonDocument.Parse(content);
            return content;
        }
        catch
        {
            // Try to extract JSON object or array from mixed content.
            var startObj = content.IndexOf('{');
            var startArr = content.IndexOf('[');
            var start = startObj >= 0 && startArr >= 0 ? Math.Min(startObj, startArr) : Math.Max(startObj, startArr);
            if (start < 0)
                throw new InvalidOperationException("AI 返回内容不包含 JSON。");

            var endObj = content.LastIndexOf('}');
            var endArr = content.LastIndexOf(']');
            var end = endObj >= 0 && endArr >= 0 ? Math.Max(endObj, endArr) : Math.Max(endObj, endArr);
            if (end < start)
                throw new InvalidOperationException("AI 返回内容无法解析为 JSON。");

            var slice = content[start..(end + 1)];
            JsonDocument.Parse(slice);
            return slice;
        }
    }

    private static AiShotDescription ParseShotDescription(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("AI 返回格式不正确（需要 JSON 对象）。");

        return new AiShotDescription(
            GetString(root, "shotType", "shot_type", "镜头类型"),
            GetString(root, "coreContent", "core_content", "核心画面"),
            GetString(root, "actionCommand", "action_command", "动作指令"),
            GetString(root, "sceneSettings", "scene_settings", "场景设定"),
            GetString(root, "firstFramePrompt", "first_frame_prompt", "首帧提示词"),
            GetString(root, "lastFramePrompt", "last_frame_prompt", "尾帧提示词"),
            DurationSeconds: null,
            // Image professional parameters
            Composition: GetStringOrNull(root, "composition", "构图"),
            LightingType: GetStringOrNull(root, "lightingType", "lighting_type", "光线类型"),
            TimeOfDay: GetStringOrNull(root, "timeOfDay", "time_of_day", "时间段"),
            ColorStyle: GetStringOrNull(root, "colorStyle", "color_style", "色调风格"),
            NegativePrompt: GetStringOrNull(root, "negativePrompt", "negative_prompt", "负面提示词"),
            // Video parameters
            VideoPrompt: GetStringOrNull(root, "videoPrompt", "video_prompt", "视频提示词"),
            SceneDescription: GetStringOrNull(root, "sceneDescription", "scene_description", "场景描述"),
            ActionDescription: GetStringOrNull(root, "actionDescription", "action_description", "动作描述"),
            StyleDescription: GetStringOrNull(root, "styleDescription", "style_description", "风格描述"),
            CameraMovement: GetStringOrNull(root, "cameraMovement", "camera_movement", "运镜方式"),
            ShootingStyle: GetStringOrNull(root, "shootingStyle", "shooting_style", "拍摄风格"),
            VideoEffect: GetStringOrNull(root, "videoEffect", "video_effect", "视频特效"),
            VideoNegativePrompt: GetStringOrNull(root, "videoNegativePrompt", "video_negative_prompt", "视频负面提示词"),
            // Additional parameters
            ImageSize: GetStringOrNull(root, "imageSize", "image_size", "图片尺寸"),
            VideoResolution: GetStringOrNull(root, "videoResolution", "video_resolution", "视频分辨率"),
            VideoRatio: GetStringOrNull(root, "videoRatio", "video_ratio", "视频比例"));
    }

    private static IReadOnlyList<AiShotDescription> ParseShotList(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        JsonElement arrayElement;
        if (root.ValueKind == JsonValueKind.Array)
        {
            arrayElement = root;
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("shots", out var shots))
        {
            arrayElement = shots;
        }
        else
        {
            throw new InvalidOperationException("AI 返回格式不正确（需要 JSON 数组）。");
        }

        if (arrayElement.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("AI 返回格式不正确（需要 JSON 数组）。");

        var list = new List<AiShotDescription>();
        foreach (var item in arrayElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var duration = GetDouble(item, "duration", "durationSeconds", "时长");
            list.Add(new AiShotDescription(
                GetString(item, "shotType", "shot_type", "镜头类型"),
                GetString(item, "coreContent", "core_content", "核心画面"),
                GetString(item, "actionCommand", "action_command", "动作指令"),
                GetString(item, "sceneSettings", "scene_settings", "场景设定"),
                GetString(item, "firstFramePrompt", "first_frame_prompt", "首帧提示词"),
                GetString(item, "lastFramePrompt", "last_frame_prompt", "尾帧提示词"),
                duration));
        }

        return list;
    }

    private static string GetString(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string? GetStringOrNull(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
            {
                var value = prop.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }
        }

        return null;
    }

    private static double? GetDouble(JsonElement obj, params string[] names)
    {
        foreach (var name in names)
        {
            if (!obj.TryGetProperty(name, out var prop))
                continue;

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDouble(out var v))
                return v;

            if (prop.ValueKind == JsonValueKind.String && double.TryParse(prop.GetString(), out var vs))
                return vs;
        }

        return null;
    }
}
