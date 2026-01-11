namespace Storyboard.Services;

using System.IO;

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
        var dpi = 96;

        var wb = new System.Windows.Media.Imaging.WriteableBitmap(
            width,
            height,
            dpi,
            dpi,
            System.Windows.Media.PixelFormats.Bgra32,
            null);

        // 简单填充背景色（根据 model 做一点点区分，方便肉眼确认）
        var color = (model ?? string.Empty).Contains("pika", StringComparison.OrdinalIgnoreCase)
            ? System.Windows.Media.Color.FromRgb(0x93, 0xC5, 0xFD) // light blue
            : System.Windows.Media.Color.FromRgb(0xBB, 0xF7, 0xD0); // light green

        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = 0xFF;
        }

        wb.WritePixels(new System.Windows.Int32Rect(0, 0, width, height), pixels, width * 4, 0);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(wb));

        await using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            encoder.Save(fs);
        }

        return filePath;
    }
}
