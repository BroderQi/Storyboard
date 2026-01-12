using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface IVideoMetadataService
{
    Task<VideoMetadata> GetMetadataAsync(string videoPath, CancellationToken cancellationToken = default);
}
