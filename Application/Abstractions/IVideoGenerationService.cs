using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface IVideoGenerationService
{
    Task<string> GenerateVideoAsync(
        ShotItem shot,
        string? outputDirectory = null,
        string? filePrefix = null,
        CancellationToken cancellationToken = default);
}
