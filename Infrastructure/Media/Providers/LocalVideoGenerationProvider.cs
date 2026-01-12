using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Extensions.Options;
using Storyboard.AI.Core;
using Storyboard.Infrastructure.Media;
using Storyboard.Models;

namespace Storyboard.Infrastructure.Media.Providers;

public sealed class LocalVideoGenerationProvider : IVideoGenerationProvider
{
    private readonly IOptionsMonitor<AIServicesConfiguration> _configMonitor;

    public LocalVideoGenerationProvider(IOptionsMonitor<AIServicesConfiguration> configMonitor)
    {
        _configMonitor = configMonitor;
    }

    public VideoProviderType ProviderType => VideoProviderType.Local;
    public string DisplayName => "本地合成";
    public bool IsConfigured => _configMonitor.CurrentValue.Video.Local.Enabled;
    public IReadOnlyList<string> SupportedModels => new[] { "local" };
    public IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations => new[]
    {
        new ProviderCapabilityDeclaration(AIProviderCapability.VideoGeneration, "MaxDuration: 120s", "video/mp4")
    };

    public async Task GenerateAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default)
    {
        var shot = request.Shot ?? throw new ArgumentNullException(nameof(request.Shot));
        var duration = Math.Max(0.2, shot.Duration);
        var width = Math.Max(320, request.Width);
        var height = Math.Max(240, request.Height);
        var fps = Math.Clamp(request.Fps, 12, 60);
        var bitrate = Math.Clamp(request.BitrateKbps, 800, 20000);
        var transition = Math.Clamp(request.TransitionSeconds, 0.2, Math.Min(1.5, duration / 2.0));

        var hasFirst = !string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) && File.Exists(shot.FirstFrameImagePath);
        var hasLast = !string.IsNullOrWhiteSpace(shot.LastFrameImagePath) && File.Exists(shot.LastFrameImagePath);

        var outputPath = request.OutputPath;
        var dur = duration.ToString("0.###", CultureInfo.InvariantCulture);
        var scalePad = $"scale={width}:{height}:force_original_aspect_ratio=decrease,pad={width}:{height}:(ow-iw)/2:(oh-ih)/2";

        string args;
        if (hasFirst && hasLast && duration >= transition + 0.1)
        {
            var transitionText = transition.ToString("0.###", CultureInfo.InvariantCulture);
            var offset = Math.Max(0.1, duration - transition).ToString("0.###", CultureInfo.InvariantCulture);
            args = $"-y -hide_banner -loglevel error -loop 1 -i \"{shot.FirstFrameImagePath}\" -loop 1 -i \"{shot.LastFrameImagePath}\" -t {dur} -r {fps} -filter_complex \"[0:v]{scalePad},format=yuv420p[first];[1:v]{scalePad},format=yuv420p[last];[first][last]xfade=transition=fade:duration={transitionText}:offset={offset},format=yuv420p\" -an -c:v libx264 -preset medium -crf 20 -b:v {bitrate}k \"{outputPath}\"";
        }
        else if (hasFirst)
        {
            var frames = (int)Math.Ceiling(duration * fps);
            var zoomFilter = request.UseKenBurns
                ? $"zoompan=z='min(1.0+0.002*on,1.15)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={frames}:s={width}x{height},fps={fps},format=yuv420p"
                : $"{scalePad},format=yuv420p";

            args = $"-y -hide_banner -loglevel error -loop 1 -i \"{shot.FirstFrameImagePath}\" -t {dur} -r {fps} -vf \"{zoomFilter}\" -an -c:v libx264 -preset medium -crf 20 -b:v {bitrate}k \"{outputPath}\"";
        }
        else if (hasLast)
        {
            var frames = (int)Math.Ceiling(duration * fps);
            var zoomFilter = request.UseKenBurns
                ? $"zoompan=z='min(1.0+0.002*on,1.15)':x='iw/2-(iw/zoom/2)':y='ih/2-(ih/zoom/2)':d={frames}:s={width}x{height},fps={fps},format=yuv420p"
                : $"{scalePad},format=yuv420p";

            args = $"-y -hide_banner -loglevel error -loop 1 -i \"{shot.LastFrameImagePath}\" -t {dur} -r {fps} -vf \"{zoomFilter}\" -an -c:v libx264 -preset medium -crf 20 -b:v {bitrate}k \"{outputPath}\"";
        }
        else
        {
            args = $"-y -hide_banner -loglevel error -f lavfi -i color=c=black:s={width}x{height}:r={fps} -t {dur} -vf format=yuv420p -an -c:v libx264 -preset medium -crf 20 -b:v {bitrate}k \"{outputPath}\"";
        }

        var (exitCode, _stdout, stderr) = await RunProcessCaptureAsync(FfmpegLocator.GetFfmpegPath(), args, cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
            throw new InvalidOperationException($"ffmpeg 生成分镜视频失败。\n{stderr}");

        if (!File.Exists(outputPath))
            throw new InvalidOperationException("分镜视频生成完成但未找到输出文件。");
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunProcessCaptureAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
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
        };

        if (!proc.Start())
            throw new InvalidOperationException($"无法启动进程: {fileName}");

        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (proc.ExitCode, stdout.ToString(), stderr.ToString());
    }
}
