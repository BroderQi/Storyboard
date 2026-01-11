using System.IO;
using SkiaSharp;
using Storyboard.Application.Abstractions;

namespace Storyboard.Infrastructure.Services;

public sealed class ImageGenerationService : IImageGenerationService
{
    public async Task<string> GenerateImageAsync(string prompt, string model)
    {
        await Task.Yield();

        var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "images");
        Directory.CreateDirectory(outDir);

        var filePath = Path.Combine(outDir, $"image_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

        var width = 1024;
        var height = 576;

        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888));
        var canvas = surface.Canvas;

        var color = (model ?? string.Empty).Contains("pika", StringComparison.OrdinalIgnoreCase)
            ? new SKColor(0x93, 0xC5, 0xFD)
            : new SKColor(0xBB, 0xF7, 0xD0);

        canvas.Clear(color);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(fs);

        return filePath;
    }
}
