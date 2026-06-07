using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using MangaERP.Task.Domain.Entities;
using MangaERP.Task.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Task.Infrastructure.Persistence;

public class TaskDbContext : BaseDbContext
{
    public TaskDbContext(DbContextOptions<TaskDbContext> options) : base(options) { }

    public DbSet<ArtworkLayer> ArtworkLayers => Set<ArtworkLayer>();
    public DbSet<AssistantInvitation> AssistantInvitations => Set<AssistantInvitation>();
    public DbSet<ChapterTeam> ChapterTeams => Set<ChapterTeam>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ArtworkLayers [FIX-8]
        modelBuilder.Entity<ArtworkLayer>(entity =>
        {
            entity.ToTable("ArtworkLayers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.LayerType)
                .HasConversion(v => v.ToString(), v => Enum.Parse<LayerType>(v))
                .HasMaxLength(50);
            entity.Property(e => e.FileUrlOriginal).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.FileUrlOptimized).HasMaxLength(2048);
            entity.Property(e => e.RejectionNote).HasMaxLength(4000);

            entity.HasIndex(e => new { e.PageTaskId, e.IsCurrentVersion })
                .HasFilter("[IsCurrentVersion] = 1")
                .HasDatabaseName("IX_ArtworkLayers_CurrentVersion");
        });

        // AssistantInvitations [FIX-5]
        modelBuilder.Entity<AssistantInvitation>(entity =>
        {
            entity.ToTable("AssistantInvitations");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InvitationToken).IsUnique();
            entity.Property(e => e.AssignedRole).IsRequired().HasMaxLength(100); // [FIX-5]
        });

        // ChapterTeams [FIX-6]
        modelBuilder.Entity<ChapterTeam>(entity =>
        {
            entity.ToTable("ChapterTeams");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ChapterId, e.UserId, e.AssignedRole }).IsUnique();
            entity.Property(e => e.AssignedRole).IsRequired().HasMaxLength(100);
        });
    }
}
