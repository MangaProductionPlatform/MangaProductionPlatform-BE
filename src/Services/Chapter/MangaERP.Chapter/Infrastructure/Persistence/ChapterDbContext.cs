using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Chapter.Infrastructure.Persistence;

public class ChapterDbContext : BaseDbContext
{
    public ChapterDbContext(DbContextOptions<ChapterDbContext> options) : base(options) { }

    public DbSet<ChapterEntity> Chapters => Set<ChapterEntity>();
    public DbSet<PageTask> PageTasks => Set<PageTask>();
    public DbSet<PreviewPage> PreviewPages => Set<PreviewPage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Soft delete global query filters
        modelBuilder.Entity<ChapterEntity>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<PageTask>().HasQueryFilter(pt => !pt.IsDeleted);

        // Chapters
        modelBuilder.Entity<ChapterEntity>(entity =>
        {
            entity.ToTable("Chapters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(256);
            entity.Property(e => e.ChapterNumber).HasColumnType("decimal(5,2)");
            entity.Property(e => e.Status)
                .HasConversion(v => v.ToString(), v => Enum.Parse<ChapterStatus>(v))
                .HasMaxLength(50);
            entity.Property(e => e.IssueType).HasMaxLength(50);
            entity.HasMany(c => c.PageTasks)
                .WithOne(pt => pt.Chapter)
                .HasForeignKey(pt => pt.ChapterId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PageTasks [FIX-7 CRITICAL]
        modelBuilder.Entity<PageTask>(entity =>
        {
            entity.ToTable("PageTasks");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ChapterId, e.PageNumber }).IsUnique();
            entity.Property(e => e.TaskStatus)
                .HasConversion(v => v.ToString(), v => Enum.Parse<PageTaskStatus>(v))
                .HasMaxLength(50);
            entity.HasOne(pt => pt.PreviewPage)
                .WithOne(pp => pp.PageTask)
                .HasForeignKey<PreviewPage>(pp => pp.PageTaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // PreviewPages [FIX-9]
        modelBuilder.Entity<PreviewPage>(entity =>
        {
            entity.ToTable("PreviewPages");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PageTaskId).IsUnique(); // 1-to-1 constraint
            entity.Property(e => e.CompositeFileUrl).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.ProductionFileUrl).HasMaxLength(2048);
        });
    }
}
