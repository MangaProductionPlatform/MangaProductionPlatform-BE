using MangaERP.Segmentation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Segmentation.Infrastructure;

public class SegmentationDbContext : DbContext
{
    public SegmentationDbContext(DbContextOptions<SegmentationDbContext> options) : base(options)
    {
    }

    public DbSet<SegmentationTask> SegmentationTasks => Set<SegmentationTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<SegmentationTask>(entity =>
        {
            entity.ToTable("SegmentationTasks");
            entity.HasKey(e => e.Id);
            
            // Indexing for query performance
            entity.HasIndex(e => e.AssignedToUserId);
            entity.HasIndex(e => e.Status);
        });
    }
}
