namespace Storyboard.Models;

public sealed record AiShotAnalysisRequest(
    string? MaterialImagePath,
    string? ExistingShotType,
    string? ExistingCoreContent,
    string? ExistingActionCommand,
    string? ExistingSceneSettings,
    string? ExistingFirstFramePrompt,
    string? ExistingLastFramePrompt);
