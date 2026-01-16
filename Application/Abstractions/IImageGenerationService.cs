using Storyboard.Infrastructure.Media;

namespace Storyboard.Application.Abstractions;

public interface IImageGenerationService
{
    Task<string> GenerateImageAsync(
        string prompt,
        string model,
        string? outputDirectory = null,
        string? filePrefix = null,
        CancellationToken cancellationToken = default);

    Task<string> GenerateImageAsync(
        ImageGenerationRequest request,
        string? outputDirectory = null,
        string? filePrefix = null,
        CancellationToken cancellationToken = default);
}
