using Microsoft.Extensions.Logging;
using Storyboard.AI;
using Storyboard.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Storyboard.Services;

public interface IVideoAnalysisService
{
    Task<VideoAnalysisResult> AnalyzeVideoAsync(string videoPath);
}

public class VideoAnalysisService : IVideoAnalysisService
{
    private readonly ILogger<VideoAnalysisService> _logger;
    private readonly AIServiceManager _ai;

    public VideoAnalysisService(ILogger<VideoAnalysisService> logger, AIServiceManager ai)
    {
        _logger = logger;
        _ai = ai;
    }

    public async Task<VideoAnalysisResult> AnalyzeVideoAsync(string videoPath)
    {
        if (string.IsNullOrWhiteSpace(videoPath))
            throw new ArgumentException("视频路径为空", nameof(videoPath));

        if (!File.Exists(videoPath))
            throw new FileNotFoundException("视频文件不存在", videoPath);

        var probe = await ProbeAsync(videoPath).ConfigureAwait(false);
        var sceneCuts = await TryDetectSceneCutsAsync(videoPath, probe.DurationSeconds).ConfigureAwait(false);
        var segments = BuildSegments(probe.DurationSeconds, sceneCuts);

        // 尽量让分镜数量稳定、可控
        var targetShotCount = Math.Clamp(segments.Count, 1, 30);

        var shots = await TryGenerateShotsWithLlmAsync(probe, segments, targetShotCount).ConfigureAwait(false)
                    ?? BuildHeuristicShots(segments);

        // 兜底：镜头号 + 总时长
        for (int i = 0; i < shots.Count; i++)
            shots[i].ShotNumber = i + 1;

        var total = shots.Sum(s => s.Duration);
        if (probe.DurationSeconds > 0 && total > 0)
        {
            // 将最后一个镜头修正到精确总时长（允许编码误差）
            var delta = probe.DurationSeconds - total;
            if (Math.Abs(delta) > 0.01)
                shots[^1].Duration = Math.Max(0.1, shots[^1].Duration + delta);
        }

        return new VideoAnalysisResult
        {
            VideoPath = videoPath,
            TotalDuration = probe.DurationSeconds,
            Shots = shots,
            AnalyzedAt = DateTime.Now
        };
    }

    private sealed record VideoProbe(double DurationSeconds, double Fps, int Width, int Height, long? FrameCount);

    private async Task<VideoProbe> ProbeAsync(string videoPath)
    {
        // ffprobe -print_format json -show_format -show_streams
        var args = $"-v error -print_format json -show_format -show_streams \"{videoPath}\"";
        var (exitCode, stdout, stderr) = await RunProcessCaptureAsync(FfmpegLocator.GetFfprobePath(), args, CancellationToken.None).ConfigureAwait(false);
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

                    // r_frame_rate like "30000/1001"
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
        // 使用 ffmpeg scene detection（解析 showinfo 输出中的 pts_time）
        // NOTE: 输出在 stderr
        var cuts = new List<double>();
        var stderr = new StringBuilder();

        // 阈值取 0.35，避免切得太碎；可按需调
        var args = $"-hide_banner -i \"{videoPath}\" -vf \"select='gt(scene,0.35)',showinfo\" -an -f null -";

        var (exitCode, _stdout, err) = await RunProcessCaptureAsync(FfmpegLocator.GetFfmpegPath(), args, CancellationToken.None, onStderrLine: line =>
        {
            stderr.AppendLine(line);
            // showinfo: ... pts_time:12.345
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
                    // 过滤掉太靠近开头/结尾的切点
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

        // 若没有有效切点，等间隔切分（稳定）
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

        // 合并过短段
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

    private async Task<List<ShotItem>?> TryGenerateShotsWithLlmAsync(VideoProbe probe, List<(double Start, double End)> segments, int targetShotCount)
    {
        try
        {
            // 如果没有可用 provider，直接跳过
            if (!_ai.GetAvailableProviders().Any())
                return null;

            // 拼一个稳定、严格的 JSON 协议；模型温度尽量低
            var system = "你是一个分镜师。你必须仅输出严格 JSON，不要输出任何解释性文字。";
            var sb = new StringBuilder();
            sb.AppendLine("你将收到一个视频的基础元数据和候选切点（秒）。你不能凭空编造具体人物/地点/物体细节；只能生成抽象但可用的分镜脚本字段。\n");
            sb.AppendLine("请输出 JSON，格式如下（字段名必须完全一致）：");
            sb.AppendLine("{");
            sb.AppendLine("  \"shots\": [");
            sb.AppendLine("    {");
            sb.AppendLine("      \"duration\": 3.5,");
            sb.AppendLine("      \"shotType\": \"中景\",");
            sb.AppendLine("      \"coreContent\": \"内容摘要（抽象）\",");
            sb.AppendLine("      \"actionCommand\": \"运镜/动作建议\",");
            sb.AppendLine("      \"sceneSettings\": \"光线/色调/时间氛围\",");
            sb.AppendLine("      \"firstFramePrompt\": \"首帧图片提示词（中英可混合）\",");
            sb.AppendLine("      \"lastFramePrompt\": \"尾帧图片提示词（中英可混合）\"");
            sb.AppendLine("    }");
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine($"视频时长(秒): {probe.DurationSeconds:F3}");
            sb.AppendLine($"分辨率: {probe.Width}x{probe.Height}");
            sb.AppendLine($"帧率: {(probe.Fps <= 0 ? "unknown" : probe.Fps.ToString("F3", CultureInfo.InvariantCulture))}");
            sb.AppendLine($"候选切分段数: {segments.Count}，目标输出镜头数: {targetShotCount}");
            sb.AppendLine();
            sb.AppendLine("候选切分段(秒)：");
            foreach (var seg in segments.Take(60))
            {
                sb.AppendLine($"- [{seg.Start:F3}, {seg.End:F3}] (duration={(seg.End - seg.Start):F3})");
            }
            sb.AppendLine();
            sb.AppendLine("要求：");
            sb.AppendLine($"1) 必须输出 shots 数组，长度必须等于 {targetShotCount}。");
            sb.AppendLine("2) duration 为正数；总和应接近视频时长（允许少量误差）。");
            sb.AppendLine("3) 不要输出多余字段，不要包含 markdown。");

            var response = await _ai.ChatDirectAsync(
                userMessage: sb.ToString(),
                systemMessage: system,
                temperature: 0.2,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);

            return TryParseShotsJson(response, targetShotCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM 分镜结构化输出失败，将使用本地规则生成。" );
            return null;
        }
    }

    private static List<ShotItem>? TryParseShotsJson(string text, int targetShotCount)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // 兼容模型可能包裹了前后文本：尝试截取第一个 { 到最后一个 }
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
            return null;

        var json = text[start..(end + 1)];
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("shots", out var shotsEl) || shotsEl.ValueKind != JsonValueKind.Array)
            return null;

        var list = new List<ShotItem>();
        foreach (var s in shotsEl.EnumerateArray())
        {
            var shot = new ShotItem(list.Count + 1);
            if (s.TryGetProperty("duration", out var dEl) && dEl.TryGetDouble(out var d) && d > 0) shot.Duration = d;
            if (s.TryGetProperty("shotType", out var stEl)) shot.ShotType = stEl.GetString() ?? string.Empty;
            if (s.TryGetProperty("coreContent", out var ccEl)) shot.CoreContent = ccEl.GetString() ?? string.Empty;
            if (s.TryGetProperty("actionCommand", out var acEl)) shot.ActionCommand = acEl.GetString() ?? string.Empty;
            if (s.TryGetProperty("sceneSettings", out var ssEl)) shot.SceneSettings = ssEl.GetString() ?? string.Empty;
            if (s.TryGetProperty("firstFramePrompt", out var ffEl)) shot.FirstFramePrompt = ffEl.GetString() ?? string.Empty;
            if (s.TryGetProperty("lastFramePrompt", out var lfEl)) shot.LastFramePrompt = lfEl.GetString() ?? string.Empty;
            list.Add(shot);
        }

        // 强制长度稳定
        if (list.Count == 0)
            return null;

        if (list.Count > targetShotCount)
            list = list.Take(targetShotCount).ToList();
        else
        {
            while (list.Count < targetShotCount)
                list.Add(new ShotItem(list.Count + 1) { Duration = list[^1].Duration, FirstFramePrompt = list[^1].FirstFramePrompt, LastFramePrompt = list[^1].LastFramePrompt, ShotType = list[^1].ShotType, CoreContent = list[^1].CoreContent, ActionCommand = list[^1].ActionCommand, SceneSettings = list[^1].SceneSettings, SelectedModel = list[^1].SelectedModel });
        }

        return list;
    }

    private static List<ShotItem> BuildHeuristicShots(List<(double Start, double End)> segments)
    {
        var shots = new List<ShotItem>();
        for (int i = 0; i < segments.Count; i++)
        {
            var seg = segments[i];
            var duration = Math.Max(0.1, seg.End - seg.Start);

            shots.Add(new ShotItem(i + 1)
            {
                Duration = duration,
                ShotType = "中景",
                CoreContent = "（基于切点的抽象分镜）",
                ActionCommand = "稳定拍摄",
                SceneSettings = "自然光/中性色调",
                FirstFramePrompt = $"Storyboard frame {i + 1}, abstract scene, cinematic still, high quality",
                LastFramePrompt = $"Storyboard frame {i + 1} ending, abstract scene, cinematic still, high quality",
                SelectedModel = "RunwayGen3"
            });
        }

        return shots;
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessCaptureAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken,
        Action<string>? onStdoutLine = null,
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
            onStdoutLine?.Invoke(e.Data);
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
