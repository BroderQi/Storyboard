namespace Storyboard.Models;

public class VideoAnalysisResult
{
    public string VideoPath { get; set; } = string.Empty;
    public double TotalDuration { get; set; }
    public List<ShotItem> Shots { get; set; } = new();
    public DateTime AnalyzedAt { get; set; } = DateTime.Now;
}
