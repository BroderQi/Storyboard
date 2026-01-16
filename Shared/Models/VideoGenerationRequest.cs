namespace Storyboard.Models;

public sealed record VideoGenerationRequest(
    string Prompt,
    double DurationSeconds,
    string? Model = null,
    string? OutputDirectory = null,
    string? FilePrefix = null,
    // Reference images
    string? FirstFrameImagePath = null,
    string? LastFrameImagePath = null,
    bool UseFirstFrameReference = false,
    bool UseLastFrameReference = false,
    // Professional parameters
    string? SceneDescription = null,
    string? ActionDescription = null,
    string? StyleDescription = null,
    string? CameraMovement = null,
    string? ShootingStyle = null,
    string? VideoEffect = null,
    string? NegativePrompt = null,
    // Technical parameters
    string? VideoResolution = null,
    string? VideoRatio = null,
    int? VideoFrames = null,
    int? Seed = null,
    bool CameraFixed = false,
    bool Watermark = false);
