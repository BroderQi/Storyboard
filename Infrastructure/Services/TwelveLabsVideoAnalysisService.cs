using Microsoft.Extensions.Logging;
using Storyboard.AI.Core;
using Storyboard.AI.Providers;
using Storyboard.Application.Abstractions;
using Storyboard.Models;

namespace Storyboard.Infrastructure.Services;

/// <summary>
/// 用 TwelveLabs Pegasus 视频理解丰富分镜描述的装饰器。
///
/// 复用内层 <see cref="IVideoAnalysisService"/>（ffprobe/ffmpeg 场景切分得到真实镜头时长），
/// 然后在配置了 API Key 时调用 Pegasus，把每个镜头的占位文案
/// （如“画面内容（待分析）”）替换为真实的场景/景别描述。
///
/// 默认关闭：未设置 <c>TWELVELABS_API_KEY</c> 时直接透传内层结果，行为完全不变。
///
/// Opt-in decorator that enriches storyboard shot descriptions with TwelveLabs Pegasus.
/// It reuses the inner heuristic analyzer for real shot timings, then (only when an API key
/// is configured) uses Pegasus to replace placeholder shot text with real scene labels.
/// With no key configured, it transparently passes through the original result.
/// </summary>
public sealed class TwelveLabsVideoAnalysisService : IVideoAnalysisService, IVideoMetadataService
{
    // 内层服务产生的占位文案；仅当字段仍是占位/空白时才覆盖，避免覆盖用户已编辑内容。
    private const string PlaceholderCoreContent = "画面内容（待分析）";

    private const string ShotPrompt =
        "You are labeling one shot of a storyboard. In Chinese, reply with exactly three lines, no extra text:\n" +
        "核心内容: <one concise sentence describing what happens in frame>\n" +
        "景别: <one of 远景/全景/中景/近景/特写>\n" +
        "场景: <setting and lighting in a few words>";

    private readonly IVideoAnalysisService _inner;
    private readonly TwelveLabsOptions _options;
    private readonly ILogger<TwelveLabsVideoAnalysisService> _logger;

    public TwelveLabsVideoAnalysisService(
        IVideoAnalysisService inner,
        TwelveLabsOptions options,
        ILogger<TwelveLabsVideoAnalysisService> logger)
    {
        _inner = inner;
        _options = options;
        _logger = logger;
    }

    public Task<VideoMetadata> GetMetadataAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        // 元数据探测与 Pegasus 无关，直接透传给内层（同时实现 IVideoMetadataService）。
        return _inner is IVideoMetadataService meta
            ? meta.GetMetadataAsync(videoPath, cancellationToken)
            : throw new NotSupportedException("Inner analysis service does not provide metadata.");
    }

    public async Task<VideoAnalysisResult> AnalyzeVideoAsync(string videoPath)
    {
        var result = await _inner.AnalyzeVideoAsync(videoPath).ConfigureAwait(false);

        if (!_options.IsEnabled || result.Shots.Count == 0)
            return result; // 未启用 → 原样返回（非破坏性默认）。

        try
        {
            await EnrichWithPegasusAsync(videoPath, result).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Pegasus 是可选增强，任何失败都不应破坏既有的启发式结果。
            _logger.LogWarning(ex, "TwelveLabs Pegasus enrichment failed; returning heuristic result.");
        }

        return result;
    }

    private async Task EnrichWithPegasusAsync(string videoPath, VideoAnalysisResult result)
    {
        var client = new TwelveLabsPegasusClient(_options, _logger);

        _logger.LogInformation("TwelveLabs: uploading {Path} for Pegasus analysis.", videoPath);
        var assetId = await client.UploadAssetAsync(videoPath, CancellationToken.None).ConfigureAwait(false);
        if (assetId == null)
            return; // 上传失败/过大已在客户端记录日志，保留启发式结果。

        foreach (var shot in result.Shots)
        {
            // 仅丰富仍为占位/空白的字段，避免覆盖用户已编辑内容。
            if (!IsPlaceholder(shot))
                continue;

            var text = await client.AnalyzeAssetAsync(
                assetId, ShotPrompt, shot.StartTime, shot.EndTime, CancellationToken.None).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            ApplyPegasusText(shot, text);
        }
    }

    private static bool IsPlaceholder(ShotItem shot) =>
        string.IsNullOrWhiteSpace(shot.CoreContent) ||
        shot.CoreContent == PlaceholderCoreContent;

    /// <summary>把 Pegasus 的三行输出解析到对应字段；解析失败时整体写入核心内容。</summary>
    private static void ApplyPegasusText(ShotItem shot, string text)
    {
        var matched = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (TryStrip(line, "核心内容", out var core)) { shot.CoreContent = core; matched = true; }
            else if (TryStrip(line, "景别", out var shotType)) { shot.ShotType = shotType; matched = true; }
            else if (TryStrip(line, "场景", out var scene)) { shot.SceneSettings = scene; matched = true; }
        }

        if (!matched)
            shot.CoreContent = text.Trim();
    }

    private static bool TryStrip(string line, string label, out string value)
    {
        value = string.Empty;
        var idx = line.IndexOf(label, StringComparison.Ordinal);
        if (idx < 0)
            return false;

        var rest = line[(idx + label.Length)..].TrimStart('：', ':', ' ', '\t');
        if (string.IsNullOrWhiteSpace(rest))
            return false;

        value = rest.Trim();
        return true;
    }
}
