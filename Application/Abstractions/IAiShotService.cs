using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface IAiShotService
{
    Task<AiShotDescription> AnalyzeShotAsync(
        AiShotAnalysisRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiShotDescription>> GenerateShotsFromTextAsync(
        string prompt,
        string? creativeGoal = null,
        string? targetAudience = null,
        string? videoTone = null,
        string? keyMessage = null,
        CancellationToken cancellationToken = default);
}
