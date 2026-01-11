namespace Storyboard.Application.Abstractions;

public interface IFinalRenderService
{
    Task<string> RenderAsync(IReadOnlyList<string> clipPaths, CancellationToken cancellationToken, IProgress<double>? progress = null);
}
