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
    double StartTime,
    double EndTime,
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
    string? MaterialFilePath,
    IReadOnlyList<ShotAssetState> Assets);

public sealed record ShotAssetState(
    Domain.Entities.ShotAssetType Type,
    string FilePath,
    string? ThumbnailPath,
    string? Prompt,
    string? Model,
    DateTimeOffset CreatedAt);
