namespace Storyboard.Application.Abstractions;

public interface IImageGenerationService
{
    Task<string> GenerateImageAsync(string prompt, string model);
}
