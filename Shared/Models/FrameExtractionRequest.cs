namespace Storyboard.Models;

public sealed record FrameExtractionRequest(
    string VideoPath,
    string ProjectId,
    FrameExtractionMode Mode,
    int FrameCount,
    double TimeIntervalMs,
    double DetectionSensitivity);
