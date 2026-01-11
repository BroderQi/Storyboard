namespace Storyboard.Services;

using System.IO;
using SkiaSharp;

public interface IImageGenerationService
{
    Task<string> GenerateImageAsync(string prompt, string model);
}

public class ImageGenerationService : IImageGenerationService
{
    public async Task<string> GenerateImageAsync(string prompt, string model)
    {
        // 最小可用：生成一个本地占位 PNG 文件（避免返回不存在的路径）
        await Task.Yield();

        var outDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output", "images");
        Directory.CreateDirectory(outDir);

        var filePath = Path.Combine(outDir, $"image_{DateTime.Now:yyyyMMdd_HHmmss_fff}.png");

        var width = 1024;
        var height = 576;

        // 使用 SkiaSharp 替代 WPF 的 WriteableBitmap
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888));
        var canvas = surface.Canvas;

        // 简单填充背景色（根据 model 做一点点区分，方便肉眼确认）
        var color = (model ?? string.Empty).Contains("pika", StringComparison.OrdinalIgnoreCase)
            ? new SKColor(0x93, 0xC5, 0xFD) // light blue
            : new SKColor(0xBB, 0xF7, 0xD0); // light green

        canvas.Clear(color);

        // 保存为 PNG
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        data.SaveTo(fs);

        return filePath;
    }
}
