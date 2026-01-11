namespace Storyboard.Application.Services;

public sealed record ProjectSummary(
    string Id,
    string Name,
    DateTimeOffset UpdatedAt,
    int TotalShots,
    int CompletedShots,
    int HasImages);

public sealed record ProjectState(
    string Id,
    string Name,
    string? SelectedVideoPath,
    bool HasVideoFile,
    string VideoFileDuration,
    string VideoFileResolution,
    string VideoFileFps,
    int ExtractModeIndex,
    int FrameCount,
    double TimeInterval,
    double DetectionSensitivity,
    IReadOnlyList<ShotState> Shots);

public sealed record ShotState(
    int ShotNumber,
    double Duration,
    string FirstFramePrompt,
    string LastFramePrompt,
    string ShotType,
    string CoreContent,
    string ActionCommand,
    string SceneSettings,
    string SelectedModel,
    string? FirstFrameImagePath,
    string? LastFrameImagePath,
    string? GeneratedVideoPath,
    string? MaterialThumbnailPath,
    string? MaterialFilePath);
