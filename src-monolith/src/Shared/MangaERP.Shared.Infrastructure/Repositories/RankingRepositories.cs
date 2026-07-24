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

    public async System.Threading.Tasks.Task<HashSet<Guid>> GetValidSeriesIdsAsync(CancellationToken ct = default)
    {
        var ids = await _db.MangaSeries.Select(s => s.Id).ToListAsync(ct);
        return new HashSet<Guid>(ids);
    }

    public async System.Threading.Tasks.Task RecordFailedBatchAsync(RankingImportBatch batch, CancellationToken ct = default)
    {
        _db.RankingImportBatches.Add(batch);
        await _db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task ImportBatchAsync(RankingImportBatch batch, IEnumerable<RankingSnapshot> snapshots, CancellationToken ct = default)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = _db.Database.IsRelational()
                ? await _db.Database.BeginTransactionAsync(ct)
                : null;

            var oldSnapshots = await _db.RankingSnapshots
                .Where(s => s.Period == batch.Period)
                .ToListAsync(ct);
            _db.RankingSnapshots.RemoveRange(oldSnapshots);

            await _db.RankingSnapshots.AddRangeAsync(snapshots, ct);
            _db.RankingImportBatches.Add(batch);

            _db.SystemAuditLogs.Add(new SystemAuditLog
            {
                ActorId = batch.UploaderId,
                ActionName = "IMPORT_RANKING_CSV",
                EntityType = "RankingImportBatch",
                EntityId = batch.Id,
                Description = $"Imported {snapshots.Count()} ranking snapshots for period {batch.Period}.",
                Timestamp = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);
        });
    }

    public async System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
