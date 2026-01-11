namespace Storyboard.Application.Services;

public interface IProjectStore
{
    Task<IReadOnlyList<ProjectSummary>> GetRecentAsync(CancellationToken cancellationToken = default);
    Task<ProjectState?> LoadAsync(string projectId, CancellationToken cancellationToken = default);
    Task<string> CreateAsync(string projectName, CancellationToken cancellationToken = default);
    Task SaveAsync(ProjectState state, CancellationToken cancellationToken = default);
    Task DeleteAsync(string projectId, CancellationToken cancellationToken = default);
}
