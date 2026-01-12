namespace Storyboard.Models;

public sealed record ExtractedFrame(
    int Index,
    double TimestampSeconds,
    string FilePath);
