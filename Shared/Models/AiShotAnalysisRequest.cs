namespace Storyboard.Models;

public sealed record AiShotAnalysisRequest(
    string? FirstFramePath,
    string? LastFramePath,
    string? ExistingShotType,
    string? ExistingCoreContent,
    string? ExistingActionCommand,
    string? ExistingSceneSettings,
    string? ExistingFirstFramePrompt,
    string? ExistingLastFramePrompt);
