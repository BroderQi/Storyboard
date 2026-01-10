using 分镜大师.Models;

namespace 分镜大师.Services;

public interface IVideoGenerationService
{
    Task<string> GenerateVideoAsync(ShotItem shot);
}

public class VideoGenerationService : IVideoGenerationService
{
    public async Task<string> GenerateVideoAsync(ShotItem shot)
    {
        // 模拟视频生成延迟
        await Task.Delay(5000);

        // TODO: 实现真实的 AI 视频生成逻辑
        // 这里返回一个占位符路径
        return $"generated_video_{shot.ShotNumber}_{DateTime.Now:yyyyMMddHHmmss}.mp4";
    }
}
