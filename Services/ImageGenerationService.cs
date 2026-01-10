namespace Storyboard.Services;

public interface IImageGenerationService
{
    Task<string> GenerateImageAsync(string prompt, string model);
}

public class ImageGenerationService : IImageGenerationService
{
    public async Task<string> GenerateImageAsync(string prompt, string model)
    {
        // 模拟图像生成延迟
        await Task.Delay(3000);

        // TODO: 实现真实的 AI 图像生成逻辑
        // 这里返回一个占位符路径
        return $"generated_image_{DateTime.Now:yyyyMMddHHmmss}.png";
    }
}
