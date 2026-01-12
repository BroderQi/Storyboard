namespace Storyboard.Models;

public sealed record FrameExtractionResult(
    IReadOnlyList<ExtractedFrame> Frames,
    double TotalDurationSeconds);
