using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Storyboard.Services;

public interface IFinalRenderService
{
    Task<string> RenderAsync(IReadOnlyList<string> clipPaths, CancellationToken cancellationToken, IProgress<double>? progress = null);
}

public sealed class FinalRenderService : IFinalRenderService
{
    private readonly ILogger<FinalRenderService> _logger;

    public FinalRenderService(ILogger<FinalRenderService> logger)
    {
        _logger = logger;
    }

    public async Task<string> RenderAsync(IReadOnlyList<string> clipPaths, CancellationToken cancellationToken, IProgress<double>? progress = null)
    {
        if (clipPaths == null || clipPaths.Count == 0)
            throw new ArgumentException("没有可用于合成的视频片段", nameof(clipPaths));

        var missing = clipPaths.Where(p => string.IsNullOrWhiteSpace(p) || !File.Exists(p)).ToList();
        if (missing.Count > 0)
            throw new FileNotFoundException($"存在缺失的视频片段，无法合成：\n{string.Join("\n", missing)}");

        var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, $"final_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        var listFile = Path.Combine(outputDir, $"concat_{Guid.NewGuid():N}.txt");

        try
        {
            // ffmpeg concat demuxer list
            // Use absolute paths, escape single quotes
            var sb = new StringBuilder();
            foreach (var p in clipPaths)
            {
                var abs = Path.GetFullPath(p);
                sb.Append("file '");
                sb.Append(abs.Replace("'", "'\\''"));
                sb.AppendLine("'");
            }
            await File.WriteAllTextAsync(listFile, sb.ToString(), Encoding.UTF8, cancellationToken).ConfigureAwait(false);

            progress?.Report(0);

            // Try stream copy first for speed
            var copyArgs = $"-y -hide_banner -loglevel error -f concat -safe 0 -i \"{listFile}\" -c copy \"{outputPath}\"";
            var (code1, _out1, err1) = await RunProcessCaptureAsync(FfmpegLocator.GetFfmpegPath(), copyArgs, cancellationToken).ConfigureAwait(false);
            if (code1 != 0)
            {
                _logger.LogWarning("ffmpeg concat copy 失败，将尝试重编码。原因: {Error}", err1);

                // Re-encode for compatibility
                var reArgs = $"-y -hide_banner -loglevel error -f concat -safe 0 -i \"{listFile}\" -c:v libx264 -preset veryfast -crf 20 -c:a aac -movflags +faststart \"{outputPath}\"";
                var (code2, _out2, err2) = await RunProcessCaptureAsync(FfmpegLocator.GetFfmpegPath(), reArgs, cancellationToken).ConfigureAwait(false);
                if (code2 != 0)
                    throw new InvalidOperationException($"ffmpeg 合成失败（请确保已安装 ffmpeg 并加入 PATH）。\n{err2}");
            }

            if (!File.Exists(outputPath))
                throw new InvalidOperationException("合成完成但未找到输出文件。");

            progress?.Report(1);
            return outputPath;
        }
        finally
        {
            try { if (File.Exists(listFile)) File.Delete(listFile); } catch { }
        }
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
