using Storyboard.Domain.Entities;

namespace Storyboard.Application.Abstractions;

public interface IShotRepository
{
    IQueryable<Shot> Query();
    IQueryable<Shot> QueryByProject(string projectId);
    Task AddRangeAsync(IEnumerable<Shot> shots, CancellationToken cancellationToken = default);
    void RemoveRange(IEnumerable<Shot> shots);
}
