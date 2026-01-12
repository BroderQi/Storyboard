using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Media;
using Storyboard.Models;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Storyboard.Infrastructure.Services;

public sealed class FrameExtractionService : IFrameExtractionService
{
    private readonly ILogger<FrameExtractionService> _logger;
    private readonly IVideoMetadataService _metadataService;

    public FrameExtractionService(IVideoMetadataService metadataService, ILogger<FrameExtractionService> logger)
    {
        _metadataService = metadataService;
        _logger = logger;
    }

    public async Task<FrameExtractionResult> ExtractAsync(
        FrameExtractionRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.VideoPath))
            throw new ArgumentException("视频路径为空", nameof(request));

        if (!File.Exists(request.VideoPath))
            throw new FileNotFoundException("视频文件不存在", request.VideoPath);

        var metadata = await _metadataService.GetMetadataAsync(request.VideoPath, cancellationToken).ConfigureAwait(false);
        var timestamps = await BuildTimestampsAsync(request, metadata.DurationSeconds, cancellationToken).ConfigureAwait(false);

        if (timestamps.Count == 0)
            throw new InvalidOperationException("未能生成抽帧时间点，请调整参数后重试。");

        var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "frames", request.ProjectId);
        Directory.CreateDirectory(outDir);

        var frames = new List<ExtractedFrame>();
        for (var i = 0; i < timestamps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var timestamp = Math.Clamp(timestamps[i], 0, Math.Max(0, metadata.DurationSeconds));
            var ts = timestamp.ToString("0.###", CultureInfo.InvariantCulture);
            var filePath = Path.Combine(outDir, $"frame_{i + 1:000}_{timestamp:0.000}.jpg");

            var args = $"-y -hide_banner -loglevel error -ss {ts} -i \"{request.VideoPath}\" -frames:v 1 -q:v 2 \"{filePath}\"";
            var (exit, _stdout, stderr) = await RunProcessCaptureAsync(
                FfmpegLocator.GetFfmpegPath(),
                args,
                cancellationToken).ConfigureAwait(false);

            if (exit != 0 || !File.Exists(filePath))
            {
                _logger.LogWarning("抽帧失败: {Error}", stderr);
                continue;
            }

            frames.Add(new ExtractedFrame(i + 1, timestamp, filePath));
            progress?.Report((double)(i + 1) / timestamps.Count);
        }

        if (frames.Count == 0)
            throw new InvalidOperationException("抽帧失败：未生成任何图片。");

        return new FrameExtractionResult(frames, metadata.DurationSeconds);
    }

    private async Task<List<double>> BuildTimestampsAsync(
        FrameExtractionRequest request,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        var mode = request.Mode;
        if (durationSeconds <= 0)
            return new List<double>();

        switch (mode)
        {
            case FrameExtractionMode.FixedCount:
                return BuildFixedCountTimestamps(durationSeconds, request.FrameCount, includeEdges: false);
            case FrameExtractionMode.DynamicInterval:
                return BuildDynamicIntervalTimestamps(durationSeconds, request.FrameCount);
            case FrameExtractionMode.FixedInterval:
                return BuildFixedIntervalTimestamps(durationSeconds, request.TimeIntervalMs);
            case FrameExtractionMode.Keyframe:
                return await BuildKeyframeTimestampsAsync(request.VideoPath, durationSeconds, request.DetectionSensitivity, cancellationToken).ConfigureAwait(false);
            default:
                return BuildFixedCountTimestamps(durationSeconds, request.FrameCount, includeEdges: false);
        }
    }

    private static List<double> BuildFixedCountTimestamps(double durationSeconds, int frameCount, bool includeEdges)
    {
        var count = Math.Clamp(frameCount, 1, 500);
        var list = new List<double>(count);
        var divisor = includeEdges ? Math.Max(1, count - 1) : count + 1;
        var step = durationSeconds / divisor;

        for (var i = 0; i < count; i++)
        {
            var t = includeEdges ? step * i : step * (i + 1);
            if (t >= 0 && t <= durationSeconds)
                list.Add(t);
        }

        return list;
    }

    private static List<double> BuildDynamicIntervalTimestamps(double durationSeconds, int frameCount)
    {
        var count = Math.Clamp(frameCount, 1, 500);
        var list = new List<double>(count);
        var interval = durationSeconds / count;

        for (var i = 0; i < count; i++)
        {
            var t = interval * (i + 0.5);
            if (t >= 0 && t <= durationSeconds)
                list.Add(t);
        }

        return list;
    }

    private static List<double> BuildFixedIntervalTimestamps(double durationSeconds, double intervalMs)
    {
        var intervalSec = Math.Max(0.1, intervalMs / 1000.0);
        var list = new List<double>();
        var t = 0.0;
        var count = 0;

        while (t <= durationSeconds && count < 100)
        {
            list.Add(t);
            t += intervalSec;
            count++;
        }

        return list;
    }

    private async Task<List<double>> BuildKeyframeTimestampsAsync(
        string videoPath,
        double durationSeconds,
        double sensitivity,
        CancellationToken cancellationToken)
    {
        var threshold = Math.Clamp(sensitivity, 0.05, 0.95);
        var cuts = new List<double>();
        var args = $"-hide_banner -i \"{videoPath}\" -vf \"select='gt(scene,{threshold.ToString("0.###", CultureInfo.InvariantCulture)})',showinfo\" -an -f null -";

        var (exitCode, _stdout, stderr) = await RunProcessCaptureAsync(
            FfmpegLocator.GetFfmpegPath(),
            args,
            cancellationToken,
            onStderrLine: line =>
            {
                var idx = line.IndexOf("pts_time:", StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return;

                var start = idx + "pts_time:".Length;
                var end = start;
                while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.' || line[end] == '-'))
                    end++;

                var num = line[start..end];
                if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
                {
                    if (t > 0.05 && t < durationSeconds - 0.05)
                        cuts.Add(t);
                }
            }).ConfigureAwait(false);

        if (exitCode != 0)
        {
            _logger.LogWarning("关键帧检测失败，将降级为均匀抽帧。原因: {Error}", stderr);
            return BuildFixedCountTimestamps(durationSeconds, 12, includeEdges: false);
        }

        return cuts
            .Distinct()
            .OrderBy(t => t)
            .ToList();
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessCaptureAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken,
        Action<string>? onStderrLine = null)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stdout.AppendLine(e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            stderr.AppendLine(e.Data);
            onStderrLine?.Invoke(e.Data);
        };

        if (!proc.Start())
            throw new InvalidOperationException($"无法启动进程: {fileName}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
