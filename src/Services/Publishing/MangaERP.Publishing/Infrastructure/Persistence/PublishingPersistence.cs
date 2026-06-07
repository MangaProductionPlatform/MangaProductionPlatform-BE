using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using MangaERP.Publishing.Application;
using MangaERP.Publishing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Publishing.Infrastructure.Persistence;

public class PublishingDbContext : BaseDbContext
{
    public PublishingDbContext(DbContextOptions<PublishingDbContext> options) : base(options) { }

    public DbSet<PublicationRecord> PublicationRecords => Set<PublicationRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PublicationRecord>(entity =>
        {
            entity.ToTable("PublicationRecords");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IssueType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PublicationUrl).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.CacheKey).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.ChapterId).HasDatabaseName("IX_PublicationRecords_ChapterId");
            entity.HasIndex(e => e.SeriesId).HasDatabaseName("IX_PublicationRecords_SeriesId");
        });
    }
}

public class PublicationRepository : IPublicationRepository
{
    private readonly PublishingDbContext _context;

    public PublicationRepository(PublishingDbContext context) => _context = context;

    public async Task<PublicationRecord?> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default)
        => await _context.PublicationRecords
            .FirstOrDefaultAsync(r => r.ChapterId == chapterId, cancellationToken);

    public async Task<IEnumerable<PublicationRecord>> GetBySeriesIdAsync(Guid seriesId, CancellationToken cancellationToken = default)
        => await _context.PublicationRecords
            .Where(r => r.SeriesId == seriesId)
            .OrderByDescending(r => r.PublishedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(PublicationRecord record, CancellationToken cancellationToken = default)
    {
        await _context.PublicationRecords.AddAsync(record, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
