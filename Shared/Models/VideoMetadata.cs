namespace Storyboard.Models;

public sealed record VideoMetadata(
    string VideoPath,
    double DurationSeconds,
    double Fps,
    int Width,
    int Height,
    long? FrameCount);
