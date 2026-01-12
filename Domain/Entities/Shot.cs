namespace Storyboard.Domain.Entities;

public sealed class Shot
{
    public long Id { get; set; }

    public string ProjectId { get; set; } = default!;
    public Project Project { get; set; } = default!;

    public int ShotNumber { get; set; }
    public double Duration { get; set; }
    public double StartTime { get; set; }
    public double EndTime { get; set; }

    public string FirstFramePrompt { get; set; } = string.Empty;
    public string LastFramePrompt { get; set; } = string.Empty;
    public string ShotType { get; set; } = string.Empty;
    public string CoreContent { get; set; } = string.Empty;
    public string ActionCommand { get; set; } = string.Empty;
    public string SceneSettings { get; set; } = string.Empty;
    public string SelectedModel { get; set; } = string.Empty;

    public string? FirstFrameImagePath { get; set; }
    public string? LastFrameImagePath { get; set; }
    public string? GeneratedVideoPath { get; set; }

    public string? MaterialThumbnailPath { get; set; }
    public string? MaterialFilePath { get; set; }

    public List<ShotAsset> Assets { get; set; } = new();
}
