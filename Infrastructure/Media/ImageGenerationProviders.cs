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
    string Style,
    // Professional parameters
    string? ShotType = null,
    string? Composition = null,
    string? LightingType = null,
    string? TimeOfDay = null,
    string? ColorStyle = null,
    string? NegativePrompt = null,
    string? AspectRatio = null,
    // Image-to-image parameters
    List<string>? ReferenceImagePaths = null,
    bool SequentialGeneration = false,
    int? MaxImages = null,
    // Size and watermark parameters
    string? Size = null,
    bool Watermark = false);

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
