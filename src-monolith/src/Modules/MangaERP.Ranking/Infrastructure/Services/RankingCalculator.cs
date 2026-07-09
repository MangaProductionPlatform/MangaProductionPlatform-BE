using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Chapter.Application.Ports;

namespace MangaERP.Ranking.Infrastructure.Services;

public class RankingCalculator : IRankingCalculator
{
    private readonly ISeriesRepository _seriesRepo;
    private readonly IChapterRepository _chapterRepo;

    public RankingCalculator(ISeriesRepository seriesRepo, IChapterRepository chapterRepo)
    {
        _seriesRepo = seriesRepo;
        _chapterRepo = chapterRepo;
    }

    public async Task<IEnumerable<RankingSnapshot>> CalculateAsync(RankingPeriod period, int maxItems, CancellationToken ct = default)
    {
        var allSeries = await _seriesRepo.GetAllAsync(ct);
        var snapshots = new List<RankingSnapshot>();
        var snapshotDate = DateTime.UtcNow;
        var thresholdDate = GetThresholdDate(period, snapshotDate);

        foreach (var series in allSeries)
        {
            var chapters = await _chapterRepo.GetBySeriesIdAsync(series.Id, ct);
            var publishedChapters = chapters.Where(c => c.Status == MangaERP.Chapter.Domain.Entities.ChapterStatus.Approved && c.ScheduledPublishAt.HasValue && c.ScheduledPublishAt.Value <= snapshotDate).ToList();

            var publishedCount = publishedChapters.Count;
            var recentPublishedCount = publishedChapters.Count(c => c.ScheduledPublishAt.Value >= thresholdDate);

            // Initial simple scoring algorithm
            double score = (publishedCount * 10) + (recentPublishedCount * 20);

            // If score is 0, we can skip or include with 0 score depending on requirements. 
            // We'll include them to have a complete ranking.
            
            var snapshot = new RankingSnapshot
            {
                SeriesId = series.Id,
                Score = score,
                Views = 0, // Placeholder until interaction metrics are implemented
                Likes = 0,
                Favorites = 0,
                Comments = 0,
                TrendScore = score > 0 ? score * 1.5 : 0, // Simple trend multiplier
                Period = period,
                SnapshotDate = snapshotDate,
                CreatedAt = snapshotDate
            };
            
            snapshots.Add(snapshot);
        }

        // Sort descending by score, take maxItems, assign ranks
        var rankedSnapshots = snapshots
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.TrendScore)
            .Take(maxItems)
            .ToList();

        for (int i = 0; i < rankedSnapshots.Count; i++)
        {
            rankedSnapshots[i].Rank = i + 1;
        }

        return rankedSnapshots;
    }

    private DateTime GetThresholdDate(RankingPeriod period, DateTime now)
    {
        return period switch
        {
            RankingPeriod.Daily => now.AddDays(-1),
            RankingPeriod.Weekly => now.AddDays(-7),
            RankingPeriod.Monthly => now.AddMonths(-1),
            RankingPeriod.AllTime => DateTime.MinValue,
            _ => DateTime.MinValue
        };
    }
}
