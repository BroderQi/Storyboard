using 分镜大师.Models;

namespace 分镜大师.Services;

public interface IVideoAnalysisService
{
    Task<VideoAnalysisResult> AnalyzeVideoAsync(string videoPath);
}

public class VideoAnalysisService : IVideoAnalysisService
{
    public async Task<VideoAnalysisResult> AnalyzeVideoAsync(string videoPath)
    {
        // 模拟 AI 分析延迟
        await Task.Delay(2000);

        // TODO: 实现真实的 AI 视频分析逻辑
        // 这里返回示例数据
        var result = new VideoAnalysisResult
        {
            VideoPath = videoPath,
            TotalDuration = 9.5,
            Shots = new List<ShotItem>
            {
                new ShotItem(1)
                {
                    Duration = 3.5,
                    FirstFramePrompt = "清晨的城市街道，阳光透过高楼洒下",
                    LastFramePrompt = "镜头缓缓推进到咖啡店门口",
                    ShotType = "推镜",
                    CoreContent = "城市街景",
                    ActionCommand = "缓慢推进",
                    SceneSettings = "早晨，暖色调，自然光",
                    SelectedModel = "RunwayGen3"
                },
                new ShotItem(2)
                {
                    Duration = 2,
                    FirstFramePrompt = "咖啡师正在制作拿铁，特写镜头",
                    LastFramePrompt = "拉花完成，呈现美图案",
                    ShotType = "特写",
                    CoreContent = "咖啡制作",
                    ActionCommand = "稳定拍摄",
                    SceneSettings = "室内，暖光，浅景深",
                    SelectedModel = "Pika"
                },
                new ShotItem(3)
                {
                    Duration = 4,
                    FirstFramePrompt = "顾客坐在窗边，手握咖啡杯",
                    LastFramePrompt = "望向窗外，露出微笑",
                    ShotType = "中景",
                    CoreContent = "人物场景",
                    ActionCommand = "自然转头",
                    SceneSettings = "窗边，逆光，柔和",
                    SelectedModel = "RunwayGen3"
                }
            }
        };

        return result;
    }
}
