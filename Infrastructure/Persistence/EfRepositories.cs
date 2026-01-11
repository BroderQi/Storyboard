using Microsoft.EntityFrameworkCore;
using Storyboard.Application.Abstractions;
using Storyboard.Domain.Entities;

namespace Storyboard.Infrastructure.Persistence;

internal sealed class EfProjectRepository : IProjectRepository
{
    private readonly StoryboardDbContext _db;

    public EfProjectRepository(StoryboardDbContext db) => _db = db;

    public IQueryable<Project> Query() => _db.Projects;

    public Task<Project?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
        => _db.Projects.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task AddAsync(Project project, CancellationToken cancellationToken = default)
        => _db.Projects.AddAsync(project, cancellationToken).AsTask();

    public void Remove(Project project) => _db.Projects.Remove(project);
}

internal sealed class EfShotRepository : IShotRepository
{
    private readonly StoryboardDbContext _db;

    public EfShotRepository(StoryboardDbContext db) => _db = db;

    public IQueryable<Shot> Query() => _db.Shots;

    public IQueryable<Shot> QueryByProject(string projectId)
        => _db.Shots.Where(s => s.ProjectId == projectId);

    public Task AddRangeAsync(IEnumerable<Shot> shots, CancellationToken cancellationToken = default)
        => _db.Shots.AddRangeAsync(shots, cancellationToken);

    public void RemoveRange(IEnumerable<Shot> shots) => _db.Shots.RemoveRange(shots);
}
