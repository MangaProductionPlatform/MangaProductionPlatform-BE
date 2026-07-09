using MangaERP.Ranking.Domain.Entities;

namespace MangaERP.Ranking.Application.Ports;

public interface IRankingRepository
{
    Task<IEnumerable<RankingSnapshot>> GetLatestAsync(RankingPeriod period, int limit, CancellationToken ct = default);
    Task<RankingSnapshot?> GetBySeriesIdAsync(Guid seriesId, RankingPeriod period, CancellationToken ct = default);
    Task ReplaceSnapshotAsync(RankingPeriod period, IEnumerable<RankingSnapshot> snapshots, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IRankingCalculator
{
    Task<IEnumerable<RankingSnapshot>> CalculateAsync(RankingPeriod period, int maxItems, CancellationToken ct = default);
}
