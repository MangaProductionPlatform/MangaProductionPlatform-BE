using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using MangaERP.Ranking.Application;
using MangaERP.Ranking.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Ranking.Infrastructure.Persistence;

public class RankingDbContext : BaseDbContext
{
    public RankingDbContext(DbContextOptions<RankingDbContext> options) : base(options) { }

    public DbSet<VoteData> VoteData => Set<VoteData>();
    public DbSet<RankingSnapshot> RankingSnapshots => Set<RankingSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VoteData>(entity =>
        {
            entity.ToTable("VoteData");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VotePeriod).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => new { e.SeriesId, e.VotePeriod })
                .HasDatabaseName("IX_VoteData_Series_Period");
        });

        modelBuilder.Entity<RankingSnapshot>(entity =>
        {
            entity.ToTable("RankingSnapshots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.VotePeriod).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => new { e.VotePeriod, e.Rank })
                .HasDatabaseName("IX_RankingSnapshots_Period_Rank");
        });
    }
}

// ─── Repositories ─────────────────────────────────────────────────────────────

public class VoteDataRepository : IVoteDataRepository
{
    private readonly RankingDbContext _context;

    public VoteDataRepository(RankingDbContext context) => _context = context;

    public async Task AddAsync(VoteData voteData, CancellationToken cancellationToken = default)
    {
        await _context.VoteData.AddAsync(voteData, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<VoteData>> GetByPeriodAsync(string votePeriod, CancellationToken cancellationToken = default)
        => await _context.VoteData
            .Where(v => v.VotePeriod == votePeriod)
            .ToListAsync(cancellationToken);
}

public class RankingSnapshotRepository : IRankingSnapshotRepository
{
    private readonly RankingDbContext _context;

    public RankingSnapshotRepository(RankingDbContext context) => _context = context;

    public async Task AddAsync(RankingSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        await _context.RankingSnapshots.AddAsync(snapshot, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<RankingSnapshot>> GetByPeriodAsync(string votePeriod, CancellationToken cancellationToken = default)
        => await _context.RankingSnapshots
            .Where(s => s.VotePeriod == votePeriod)
            .OrderBy(s => s.Rank)
            .ToListAsync(cancellationToken);
}
