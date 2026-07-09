using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class RankingRepository : IRankingRepository
{
    private readonly AppDbContext _db;

    public RankingRepository(IDbContextProvider provider)
    {
        _db = (AppDbContext)provider.GetDbContext();
    }

    public async System.Threading.Tasks.Task<IEnumerable<RankingSnapshot>> GetLatestAsync(RankingPeriod period, int limit, CancellationToken ct = default)
    {
        // Find the latest snapshot date for this period
        var latestDate = await _db.RankingSnapshots
            .Where(r => r.Period == period)
            .MaxAsync(r => (DateTime?)r.SnapshotDate, ct);

        if (latestDate == null) return Enumerable.Empty<RankingSnapshot>();

        return await _db.RankingSnapshots
            .Where(r => r.Period == period && r.SnapshotDate == latestDate)
            .OrderBy(r => r.Rank)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task<RankingSnapshot?> GetBySeriesIdAsync(Guid seriesId, RankingPeriod period, CancellationToken ct = default)
    {
        var latestDate = await _db.RankingSnapshots
            .Where(r => r.Period == period)
            .MaxAsync(r => (DateTime?)r.SnapshotDate, ct);

        if (latestDate == null) return null;

        return await _db.RankingSnapshots
            .FirstOrDefaultAsync(r => r.SeriesId == seriesId && r.Period == period && r.SnapshotDate == latestDate, ct);
    }

    public async System.Threading.Tasks.Task ReplaceSnapshotAsync(RankingPeriod period, IEnumerable<RankingSnapshot> snapshots, CancellationToken ct = default)
    {
        var snapshotDate = snapshots.FirstOrDefault()?.SnapshotDate ?? DateTime.UtcNow;

        // Optionally delete old snapshots for the exact same date to prevent duplicates if refreshed multiple times a day
        var existingForDate = await _db.RankingSnapshots
            .Where(r => r.Period == period && r.SnapshotDate.Date == snapshotDate.Date)
            .ToListAsync(ct);
            
        if (existingForDate.Any())
        {
            _db.RankingSnapshots.RemoveRange(existingForDate);
        }

        await _db.RankingSnapshots.AddRangeAsync(snapshots, ct);
    }

    public async System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
