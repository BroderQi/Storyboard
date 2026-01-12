using Microsoft.Extensions.Logging;
using Storyboard.Application.Abstractions;
using Storyboard.Infrastructure.Media;
using Storyboard.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Storyboard.Infrastructure.Services;

public sealed class VideoAnalysisService : IVideoAnalysisService, IVideoMetadataService
{
    private readonly ILogger<VideoAnalysisService> _logger;

    public VideoAnalysisService(ILogger<VideoAnalysisService> logger)
    {
        _logger = logger;
    }

    public async Task<VideoAnalysisResult> AnalyzeVideoAsync(string videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
            throw new ArgumentException("视频路径为空", nameof(videoPath));

        if (!File.Exists(videoPath))
            throw new FileNotFoundException("视频文件不存在", videoPath);

        var probe = await ProbeAsync(videoPath, CancellationToken.None).ConfigureAwait(false);
        var sceneCuts = await TryDetectSceneCutsAsync(videoPath, probe.DurationSeconds).ConfigureAwait(false);
        var segments = BuildSegments(probe.DurationSeconds, sceneCuts);

        var shots = BuildHeuristicShots(segments);

        for (int i = 0; i < shots.Count; i++)
            shots[i].ShotNumber = i + 1;

        var total = shots.Sum(s => s.Duration);
        if (probe.DurationSeconds > 0 && total > 0)
        {
            var delta = probe.DurationSeconds - total;
            if (Math.Abs(delta) > 0.01)
                shots[^1].Duration = Math.Max(0.1, shots[^1].Duration + delta);
        }

        return new VideoAnalysisResult
        {
            VideoPath = videoPath,
            TotalDuration = probe.DurationSeconds,
            Fps = probe.Fps,
            Width = probe.Width,
            Height = probe.Height,
            Shots = shots,
            AnalyzedAt = DateTime.Now
        };
    }

    private sealed record VideoProbe(double DurationSeconds, double Fps, int Width, int Height, long? FrameCount);

    public async Task<VideoMetadata> GetMetadataAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
            throw new ArgumentException("视频路径为空", nameof(videoPath));

        if (!File.Exists(videoPath))
            throw new FileNotFoundException("视频文件不存在", videoPath);

        var probe = await ProbeAsync(videoPath, cancellationToken).ConfigureAwait(false);
        return new VideoMetadata(
            videoPath,
            probe.DurationSeconds,
            probe.Fps,
            probe.Width,
            probe.Height,
            probe.FrameCount);
    }

    private async Task<VideoProbe> ProbeAsync(string videoPath, CancellationToken cancellationToken)
    {
        var args = $"-v error -print_format json -show_format -show_streams \"{videoPath}\"";
        var (exitCode, stdout, stderr) = await RunProcessCaptureAsync(FfmpegLocator.GetFfprobePath(), args, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
            throw new InvalidOperationException($"ffprobe 失败（请确保已安装 ffmpeg/ffprobe 并加入 PATH）。\n{stderr}");

        using var doc = JsonDocument.Parse(stdout);

        double duration = 0;
        if (doc.RootElement.TryGetProperty("format", out var format) &&
            format.TryGetProperty("duration", out var durEl) &&
            double.TryParse(durEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
        {
            duration = dur;
        }

        int width = 0;
        int height = 0;
        double fps = 0;
        long? frames = null;

        if (doc.RootElement.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in streams.EnumerateArray())
            {
                if (s.TryGetProperty("codec_type", out var typeEl) && typeEl.GetString() == "video")
                {
                    if (s.TryGetProperty("width", out var wEl) && wEl.TryGetInt32(out var w)) width = w;
                    if (s.TryGetProperty("height", out var hEl) && hEl.TryGetInt32(out var h)) height = h;

                    if (s.TryGetProperty("r_frame_rate", out var rEl))
                    {
                        fps = TryParseFraction(rEl.GetString());
                    }

                    if (s.TryGetProperty("nb_frames", out var nfEl))
                    {
                        if (long.TryParse(nfEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var nf))
                            frames = nf;
                    }
                    break;
                }
            }
        }

        if (duration <= 0)
            throw new InvalidOperationException("无法解析视频时长（ffprobe 未返回 duration）。");

        return new VideoProbe(duration, fps, width, height, frames);
    }

    private static double TryParseFraction(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        var parts = value.Split('/');
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var n) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var d) &&
            d != 0)
        {
            return n / d;
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
            return v;

        return 0;
    }

    private async Task<List<double>> TryDetectSceneCutsAsync(string videoPath, double totalDuration)
    {
        var cuts = new List<double>();

        var args = $"-hide_banner -i \"{videoPath}\" -vf \"select='gt(scene,0.35)',showinfo\" -an -f null -";

        var (exitCode, _stdout, err) = await RunProcessCaptureAsync(FfmpegLocator.GetFfmpegPath(), args, CancellationToken.None, onStderrLine: line =>
        {
            var idx = line.IndexOf("pts_time:", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var start = idx + "pts_time:".Length;
                var end = start;
                while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '.' || line[end] == '-'))
                    end++;
                var num = line[start..end];
                if (double.TryParse(num, NumberStyles.Float, CultureInfo.InvariantCulture, out var t))
                {
                    if (t > 0.05 && t < totalDuration - 0.05)
                        cuts.Add(t);
                }
            }
        }).ConfigureAwait(false);

        if (exitCode != 0)
        {
            _logger.LogWarning("ffmpeg 场景检测失败，将降级为等间隔切分。原因: {Error}", err);
            return new List<double>();
        }

        return cuts
            .Distinct()
            .Where(t => t > 0)
            .OrderBy(t => t)
            .ToList();
    }

    private static List<(double Start, double End)> BuildSegments(double durationSeconds, List<double> cuts)
    {
        var points = new List<double> { 0 };
        points.AddRange(cuts);
        points.Add(durationSeconds);

        points = points
            .Distinct()
            .Where(t => t >= 0 && t <= durationSeconds)
            .OrderBy(t => t)
            .ToList();

        var segments = new List<(double Start, double End)>();
        for (int i = 0; i < points.Count - 1; i++)
        {
            var a = points[i];
            var b = points[i + 1];
            if (b - a < 0.2)
                continue;
            segments.Add((a, b));
        }

        if (segments.Count == 0)
        {
            var step = durationSeconds <= 15 ? 3.0 : 5.0;
            var t = 0.0;
            while (t < durationSeconds - 0.01)
            {
                var end = Math.Min(durationSeconds, t + step);
                segments.Add((t, end));
                t = end;
            }
        }

        for (int i = segments.Count - 2; i >= 0; i--)
        {
            var seg = segments[i];
            if (seg.End - seg.Start < 0.6)
            {
                var next = segments[i + 1];
                segments[i] = (seg.Start, next.End);
                segments.RemoveAt(i + 1);
            }
        }

        return segments;
    }

    private static List<ShotItem> BuildHeuristicShots(List<(double Start, double End)> segments)
    {
        var list = new List<ShotItem>();
        var i = 1;
        foreach (var (Start, End) in segments)
        {
            var dur = Math.Max(0.5, End - Start);
            list.Add(new ShotItem(i++)
            {
                Duration = dur,
                StartTime = Start,
                EndTime = End,
                ShotType = dur > 4 ? "远景" : "中景",
                CoreContent = "画面内容（待分析）",
                ActionCommand = "镜头平稳推进",
                SceneSettings = "自然光/室内外（待补充）",
                FirstFramePrompt = "首帧提示词（待生成）",
                LastFramePrompt = "尾帧提示词（待生成）",
                SelectedModel = "RunwayGen3"
            });
        }
        return list;
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
