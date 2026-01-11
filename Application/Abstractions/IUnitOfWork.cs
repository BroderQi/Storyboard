namespace Storyboard.Application.Abstractions;

/// <summary>
/// EF Core DbContext is the Unit of Work. This wrapper exists to keep a clean boundary
/// and allow creating/disposal per operation in a desktop app without scoped DI.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IProjectRepository Projects { get; }
    IShotRepository Shots { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
