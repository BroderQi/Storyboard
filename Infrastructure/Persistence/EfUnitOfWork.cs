using Microsoft.EntityFrameworkCore;
using Storyboard.Application.Abstractions;

namespace Storyboard.Infrastructure.Persistence;

internal sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly StoryboardDbContext _db;

    public EfUnitOfWork(StoryboardDbContext db)
    {
        _db = db;
        Projects = new EfProjectRepository(_db);
        Shots = new EfShotRepository(_db);
    }

    public IProjectRepository Projects { get; }
    public IShotRepository Shots { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    public ValueTask DisposeAsync() => _db.DisposeAsync();
}

public sealed class EfUnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IDbContextFactory<StoryboardDbContext> _dbFactory;

    public EfUnitOfWorkFactory(IDbContextFactory<StoryboardDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IUnitOfWork> CreateAsync(CancellationToken cancellationToken = default)
    {
        var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        return new EfUnitOfWork(db);
    }
}
