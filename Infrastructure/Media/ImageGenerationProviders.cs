using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Storyboard.AI.Core;

namespace Storyboard.Infrastructure.Media;

public sealed record ImageGenerationRequest(
    string Prompt,
    string Model,
    int Width,
    int Height,
    string Style);

public sealed record ImageGenerationResult(
    byte[] ImageBytes,
    string FileExtension,
    string? ModelUsed);

public interface IImageGenerationProvider
{
    ImageProviderType ProviderType { get; }
    string DisplayName { get; }
    bool IsConfigured { get; }
    IReadOnlyList<string> SupportedModels { get; }
    IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations { get; }
    Task<ImageGenerationResult> GenerateAsync(ImageGenerationRequest request, CancellationToken cancellationToken = default);
}
