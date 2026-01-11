namespace Storyboard.Domain.Entities;

public sealed class Project
{
    public string Id { get; set; } = default!;
    public string Name { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public string? SelectedVideoPath { get; set; }
    public bool HasVideoFile { get; set; }
    public string VideoFileDuration { get; set; } = "--:--";
    public string VideoFileResolution { get; set; } = "-- x --";
    public string VideoFileFps { get; set; } = "--";

    public int ExtractModeIndex { get; set; }
    public int FrameCount { get; set; } = 10;
    public double TimeInterval { get; set; } = 1000;
    public double DetectionSensitivity { get; set; } = 0.5;

    public List<Shot> Shots { get; set; } = new();
}
