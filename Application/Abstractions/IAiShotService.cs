using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface IAiShotService
{
    Task<AiShotDescription> AnalyzeShotAsync(
        AiShotAnalysisRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiShotDescription>> GenerateShotsFromTextAsync(
        string prompt,
        CancellationToken cancellationToken = default);
}
