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
    IReadOnlyList<ShotAssetState> Assets,
    // Image generation parameters
    string ImageSize = "",
    string NegativePrompt = "",
    // Image professional parameters
    string AspectRatio = "",
    string LightingType = "",
    string TimeOfDay = "",
    string Composition = "",
    string ColorStyle = "",
    string LensType = "",
    // Video generation parameters
    string VideoPrompt = "",
    string SceneDescription = "",
    string ActionDescription = "",
    string StyleDescription = "",
    string VideoNegativePrompt = "",
    // Video professional parameters
    string CameraMovement = "",
    string ShootingStyle = "",
    string VideoEffect = "",
    string VideoResolution = "",
    string VideoRatio = "",
    int VideoFrames = 0,
    bool UseFirstFrameReference = true,
    bool UseLastFrameReference = false,
    int? Seed = null,
    bool CameraFixed = false,
    bool Watermark = false);

public sealed record ShotAssetState(
    Domain.Entities.ShotAssetType Type,
    string FilePath,
    string? ThumbnailPath,
    string? VideoThumbnailPath,
    string? Prompt,
    string? Model,
    DateTimeOffset CreatedAt);
