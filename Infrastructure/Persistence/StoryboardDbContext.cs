using Microsoft.EntityFrameworkCore;
using Storyboard.Domain.Entities;

namespace Storyboard.Infrastructure.Persistence;

public sealed class StoryboardDbContext : DbContext
{
    public StoryboardDbContext(DbContextOptions<StoryboardDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Shot> Shots => Set<Shot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(b =>
        {
            b.ToTable("Projects");
            b.HasKey(p => p.Id);
            b.Property(p => p.Id).HasMaxLength(32);
            b.Property(p => p.Name).HasMaxLength(200);
            b.HasMany(p => p.Shots)
                .WithOne(s => s.Project)
                .HasForeignKey(s => s.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(p => p.UpdatedAt);
        });

        modelBuilder.Entity<Shot>(b =>
        {
            b.ToTable("Shots");
            b.HasKey(s => s.Id);
            b.Property(s => s.ProjectId).HasMaxLength(32);
            b.HasIndex(s => new { s.ProjectId, s.ShotNumber }).IsUnique();

            b.Property(s => s.FirstFramePrompt);
            b.Property(s => s.LastFramePrompt);
            b.Property(s => s.ShotType);
            b.Property(s => s.CoreContent);
            b.Property(s => s.ActionCommand);
            b.Property(s => s.SceneSettings);
            b.Property(s => s.SelectedModel);
        });
    }
}
