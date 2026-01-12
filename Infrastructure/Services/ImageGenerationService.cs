using System.IO;
using System.Linq;
using SkiaSharp;
using Storyboard.Application.Abstractions;

namespace Storyboard.Infrastructure.Services;

public sealed class ImageGenerationService : IImageGenerationService
{
    public async Task<string> GenerateImageAsync(
        string prompt,
        string model,
        string? outputDirectory = null,
        string? filePrefix = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();

        var outDir = string.IsNullOrWhiteSpace(outputDirectory)
            ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "images")
            : outputDirectory;
        Directory.CreateDirectory(outDir);

        var safePrefix = string.IsNullOrWhiteSpace(filePrefix) ? "image" : filePrefix;
        var filePath = Path.Combine(outDir, $"{safePrefix}_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

        var width = 1024;
        var height = 576;

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888));
        var canvas = surface.Canvas;

        var color = (model ?? string.Empty).Contains("pika", StringComparison.OrdinalIgnoreCase)
            ? new SKColor(0x93, 0xC5, 0xFD)
            : new SKColor(0xBB, 0xF7, 0xD0);

        canvas.Clear(color);

        using var paint = new SKPaint
        {
            Color = new SKColor(0x11, 0x11, 0x11),
            IsAntialias = true,
            TextSize = 28,
            Typeface = SKTypeface.FromFamilyName("Segoe UI")
        };

        var infoText = string.IsNullOrWhiteSpace(prompt) ? "Prompt: (empty)" : $"Prompt: {prompt}";
        var lines = WrapText(infoText, 60).Take(6).ToArray();
        var y = 48f;
        foreach (var line in lines)
        {
            canvas.DrawText(line, 32, y, paint);
            y += 34;
        }

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(fs);

        return filePath;
    }

    private static IEnumerable<string> WrapText(string text, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        for (var i = 0; i < text.Length; i += maxChars)
        {
            var len = Math.Min(maxChars, text.Length - i);
            yield return text.Substring(i, len);
        }
    }
}
