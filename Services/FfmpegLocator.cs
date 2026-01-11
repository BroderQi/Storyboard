using System.IO;

namespace Storyboard.Services;

internal static class FfmpegLocator
{
    public static string GetFfmpegPath()
        => GetToolPath("ffmpeg.exe") ?? "ffmpeg";

    public static string GetFfprobePath()
        => GetToolPath("ffprobe.exe") ?? "ffprobe";

    private static string? GetToolPath(string exeName)
    {
        // Prefer shipping binaries next to the app.
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(baseDir, exeName),
            Path.Combine(baseDir, "ffmpeg", exeName),
        };

        foreach (var p in candidates)
        {
            try
            {
                if (File.Exists(p))
                    return p;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }
}
