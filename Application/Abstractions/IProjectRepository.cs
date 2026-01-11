using Storyboard.Domain.Entities;

namespace Storyboard.Application.Abstractions;

/// <summary>
/// Thin repository: basic aggregate root access. Query is exposed as IQueryable
/// to keep the repository weak and avoid method explosion.
/// </summary>
public interface IProjectRepository
{
    IQueryable<Project> Query();
    Task<Project?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task AddAsync(Project project, CancellationToken cancellationToken = default);
    void Remove(Project project);
}
