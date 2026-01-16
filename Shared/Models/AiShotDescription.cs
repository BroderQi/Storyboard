namespace Storyboard.Models;

public sealed record AiShotDescription(
    string ShotType,
    string CoreContent,
    string ActionCommand,
    string SceneSettings,
    string FirstFramePrompt,
    string LastFramePrompt,
    double? DurationSeconds = null,
    // Image professional parameters
    string? Composition = null,
    string? LightingType = null,
    string? TimeOfDay = null,
    string? ColorStyle = null,
    string? NegativePrompt = null,
    // Video parameters
    string? VideoPrompt = null,
    string? SceneDescription = null,
    string? ActionDescription = null,
    string? StyleDescription = null,
    string? CameraMovement = null,
    string? ShootingStyle = null,
    string? VideoEffect = null,
    string? VideoNegativePrompt = null);
