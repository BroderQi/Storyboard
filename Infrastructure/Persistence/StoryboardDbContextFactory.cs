using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Storyboard.Infrastructure.Persistence;

public class StoryboardDbContextFactory : IDesignTimeDbContextFactory<StoryboardDbContext>
{
    public StoryboardDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StoryboardDbContext>();

        // 使用实际的数据库路径用于迁移
        var dbPath = Path.Combine("bin", "Debug", "net8.0", "Data", "storyboard.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new StoryboardDbContext(optionsBuilder.Options);
    }
}
