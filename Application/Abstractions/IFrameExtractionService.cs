using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface IFrameExtractionService
{
    Task<FrameExtractionResult> ExtractAsync(
        FrameExtractionRequest request,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
