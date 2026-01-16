using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Storyboard.Infrastructure.Persistence;

public class StoryboardDbContextFactory : IDesignTimeDbContextFactory<StoryboardDbContext>
{
    public StoryboardDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StoryboardDbContext>();

        // 使用临时的 SQLite 数据库用于迁移生成
        optionsBuilder.UseSqlite("Data Source=temp.db");

        return new StoryboardDbContext(optionsBuilder.Options);
    }
}
