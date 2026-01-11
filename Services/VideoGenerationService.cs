using Storyboard.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Storyboard.Services;

public interface IVideoGenerationService
{
    Task<string> GenerateVideoAsync(ShotItem shot);
}

public class VideoGenerationService : IVideoGenerationService
{
    public async Task<string> GenerateVideoAsync(ShotItem shot)
    {
        if (shot == null)
            throw new ArgumentNullException(nameof(shot));

        await Task.Yield();

        var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "shots");
        Directory.CreateDirectory(outDir);

        var duration = Math.Max(0.2, shot.Duration);
        var outputPath = Path.Combine(outDir, $"shot_{shot.ShotNumber:000}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.mp4");

        // 最小可用：用首帧图生成一段静帧视频（真实落盘，便于后续 concat）
        string args;
        if (!string.IsNullOrWhiteSpace(shot.FirstFrameImagePath) && File.Exists(shot.FirstFrameImagePath))
        {
            var dur = duration.ToString("0.###", CultureInfo.InvariantCulture);
            args = $"-y -hide_banner -loglevel error -loop 1 -i \"{shot.FirstFrameImagePath}\" -t {dur} -r 30 -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2,format=yuv420p\" -an \"{outputPath}\"";
        }
        else
        {
            // 没有首帧图时，生成一个纯色测试视频
            var dur = duration.ToString("0.###", CultureInfo.InvariantCulture);
            args = $"-y -hide_banner -loglevel error -f lavfi -i color=c=black:s=1280x720:r=30 -t {dur} -vf format=yuv420p -an \"{outputPath}\"";
        }

        var (exitCode, _stdout, stderr) = await RunProcessCaptureAsync(FfmpegLocator.GetFfmpegPath(), args, CancellationToken.None).ConfigureAwait(false);
        if (exitCode != 0)
            throw new InvalidOperationException($"ffmpeg 生成分镜视频失败（请确保已安装 ffmpeg 并加入 PATH）。\n{stderr}");

        if (!File.Exists(outputPath))
            throw new InvalidOperationException("分镜视频生成完成但未找到输出文件。");

        return outputPath;
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
