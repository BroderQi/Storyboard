using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface IVideoAnalysisService
{
    Task<VideoAnalysisResult> AnalyzeVideoAsync(string videoPath);
}
