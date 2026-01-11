using Storyboard.Models;

namespace Storyboard.Application.Abstractions;

public interface IVideoGenerationService
{
    Task<string> GenerateVideoAsync(ShotItem shot);
}
