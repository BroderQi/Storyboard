using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Storyboard.Application.Abstractions;
using Storyboard.Application.Services;
using Storyboard.Infrastructure.Persistence;

namespace Storyboard.Infrastructure.DependencyInjection;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddStoryboardPersistence(this IServiceCollection services, string sqliteDbPath)
    {
        var connectionString = $"Data Source={sqliteDbPath}";

        services.AddDbContextFactory<StoryboardDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        });

        services.AddSingleton<IUnitOfWorkFactory, EfUnitOfWorkFactory>();
        services.AddSingleton<IProjectStore, ProjectStore>();

        return services;
    }
}
