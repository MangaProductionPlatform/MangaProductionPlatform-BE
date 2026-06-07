using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Series.Infrastructure.Persistence;

public class SeriesDbContext : BaseDbContext
{
    public SeriesDbContext(DbContextOptions<SeriesDbContext> options) : base(options) { }

    public DbSet<MangaSeries> MangaSeries => Set<MangaSeries>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Soft delete global filter [FIX-3]
        modelBuilder.Entity<MangaSeries>().HasQueryFilter(s => !s.IsDeleted);

        modelBuilder.Entity<MangaSeries>(entity =>
        {
            entity.ToTable("MangaSeries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Genre).HasMaxLength(100);
            entity.Property(e => e.CoverImageUrl).HasMaxLength(2048);
            entity.Property(e => e.Status)
                .HasConversion(v => v.ToString(), v => Enum.Parse<SeriesStatus>(v))
                .HasMaxLength(50);
            entity.HasIndex(e => e.AuthorId).HasDatabaseName("IX_MangaSeries_AuthorId");
        });
    }
}

public class SeriesRepository : ISeriesRepository
{
    private readonly SeriesDbContext _context;

    public SeriesRepository(SeriesDbContext context) => _context = context;

    public async Task<MangaSeries?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.MangaSeries.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IEnumerable<MangaSeries>> GetByAuthorIdAsync(Guid authorId, CancellationToken cancellationToken = default)
        => await _context.MangaSeries
            .Where(s => s.AuthorId == authorId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<MangaSeries>> GetAllActiveAsync(CancellationToken cancellationToken = default)
        => await _context.MangaSeries
            .Where(s => s.Status == SeriesStatus.Active)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(MangaSeries series, CancellationToken cancellationToken = default)
    {
        await _context.MangaSeries.AddAsync(series, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MangaSeries series, CancellationToken cancellationToken = default)
    {
        _context.MangaSeries.Update(series);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
