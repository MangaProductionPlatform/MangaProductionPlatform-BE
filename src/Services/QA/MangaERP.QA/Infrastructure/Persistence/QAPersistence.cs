using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using MangaERP.QA.Application.Ports;
using MangaERP.QA.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.QA.Infrastructure.Persistence;

public class QADbContext : BaseDbContext
{
    public QADbContext(DbContextOptions<QADbContext> options) : base(options) { }

    public DbSet<BugPin> BugPins => Set<BugPin>();
    public DbSet<QASession> QASessions => Set<QASession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // BugPins [FIX-10 CRITICAL]
        modelBuilder.Entity<BugPin>(entity =>
        {
            entity.ToTable("BugPins");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NoteMessage).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.CoordinateX).HasColumnType("decimal(5,2)");
            entity.Property(e => e.CoordinateY).HasColumnType("decimal(5,2)");
            entity.Property(e => e.IssueType)
                .HasConversion(v => v.HasValue ? v.Value.ToString() : null,
                               v => v != null ? Enum.Parse<IssueType>(v) : (IssueType?)null)
                .HasMaxLength(50);
            entity.Property(e => e.Status)
                .HasConversion(v => v.ToString(), v => Enum.Parse<BugStatus>(v))
                .HasMaxLength(50);
            // Performance index: load all pins for a chapter without double JOIN
            entity.HasIndex(e => e.ChapterId).HasDatabaseName("IX_BugPins_ChapterId");
            entity.HasIndex(e => e.BatchToken).HasDatabaseName("IX_BugPins_BatchToken");
        });

        // QASessions
        modelBuilder.Entity<QASession>(entity =>
        {
            entity.ToTable("QASessions");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ChapterId).IsUnique();
        });
    }
}

// ─── Repositories ─────────────────────────────────────────────────────────────

public class BugPinRepository : IBugPinRepository
{
    private readonly QADbContext _context;

    public BugPinRepository(QADbContext context) => _context = context;

    public async Task<BugPin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.BugPins.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IEnumerable<BugPin>> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default)
        => await _context.BugPins
            .Where(p => p.ChapterId == chapterId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<BugPin>> GetByBatchTokenAsync(Guid batchToken, CancellationToken cancellationToken = default)
        => await _context.BugPins
            .Where(p => p.BatchToken == batchToken)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasUnresolvedPinsAsync(Guid chapterId, CancellationToken cancellationToken = default)
        => await _context.BugPins.AnyAsync(
            p => p.ChapterId == chapterId && p.Status != BugStatus.Resolved, cancellationToken);

    public async Task AddAsync(BugPin bugPin, CancellationToken cancellationToken = default)
    {
        await _context.BugPins.AddAsync(bugPin, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BugPin bugPin, CancellationToken cancellationToken = default)
    {
        _context.BugPins.Update(bugPin);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(BugPin bugPin, CancellationToken cancellationToken = default)
    {
        _context.BugPins.Remove(bugPin);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ResolveAllForChapterAsync(Guid chapterId, CancellationToken cancellationToken = default)
    {
        var openPins = await _context.BugPins
            .Where(p => p.ChapterId == chapterId && p.Status != BugStatus.Resolved)
            .ToListAsync(cancellationToken);

        foreach (var pin in openPins)
            pin.Resolve();

        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class QASessionRepository : IQASessionRepository
{
    private readonly QADbContext _context;

    public QASessionRepository(QADbContext context) => _context = context;

    public async Task<QASession?> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default)
        => await _context.QASessions.FirstOrDefaultAsync(s => s.ChapterId == chapterId, cancellationToken);

    public async Task AddAsync(QASession session, CancellationToken cancellationToken = default)
    {
        await _context.QASessions.AddAsync(session, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(QASession session, CancellationToken cancellationToken = default)
    {
        _context.QASessions.Update(session);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
