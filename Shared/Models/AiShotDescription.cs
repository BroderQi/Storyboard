namespace Storyboard.Models;

public sealed record AiShotDescription(
    string ShotType,
    string CoreContent,
    string ActionCommand,
    string SceneSettings,
    string FirstFramePrompt,
    string LastFramePrompt,
    double? DurationSeconds = null);
