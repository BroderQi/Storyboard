using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Storyboard.AI.Core;
using Storyboard.Models;

namespace Storyboard.Infrastructure.Media;

public sealed record VideoGenerationRequest(
    ShotItem Shot,
    string OutputPath,
    string Model,
    int Width,
    int Height,
    int Fps,
    int BitrateKbps,
    double TransitionSeconds,
    bool UseKenBurns);

public interface IVideoGenerationProvider
{
    VideoProviderType ProviderType { get; }
    string DisplayName { get; }
    bool IsConfigured { get; }
    IReadOnlyList<string> SupportedModels { get; }
    IReadOnlyList<ProviderCapabilityDeclaration> CapabilityDeclarations { get; }
    Task GenerateAsync(VideoGenerationRequest request, CancellationToken cancellationToken = default);
}
