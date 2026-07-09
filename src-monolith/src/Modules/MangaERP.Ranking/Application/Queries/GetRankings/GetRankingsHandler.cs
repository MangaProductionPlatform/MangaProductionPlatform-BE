using MediatR;
using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Series.Application.Ports;

namespace MangaERP.Ranking.Application.Queries.GetRankings;

public record GetRankingsQuery(RankingPeriod Period, int Limit) : IRequest<RankingListDto>;

public record RankingListDto(
    string Period,
    DateTime GeneratedAt,
    IEnumerable<RankingItemDto> Items
);

public record RankingItemDto(
    int Rank, 
    Guid SeriesId, 
    string Title, 
    string? CoverImageUrl,
    double Score, 
    int Views, 
    int Likes, 
    int Favorites, 
    int Comments, 
    double TrendScore
);

public class GetRankingsHandler : IRequestHandler<GetRankingsQuery, RankingListDto>
{
    private readonly IRankingRepository _rankingRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetRankingsHandler(IRankingRepository rankingRepo, ISeriesRepository seriesRepo)
    {
        _rankingRepo = rankingRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<RankingListDto> Handle(GetRankingsQuery request, CancellationToken cancellationToken)
    {
        var snapshots = await _rankingRepo.GetLatestAsync(request.Period, request.Limit, cancellationToken);
        
        var generatedAt = snapshots.FirstOrDefault()?.SnapshotDate ?? DateTime.UtcNow;

        var items = new List<RankingItemDto>();
        foreach (var snapshot in snapshots)
        {
            var series = await _seriesRepo.GetByIdAsync(snapshot.SeriesId, cancellationToken);
            if (series != null)
            {
                items.Add(new RankingItemDto(
                    snapshot.Rank,
                    snapshot.SeriesId,
                    series.Title,
                    series.CoverImageUrl,
                    snapshot.Score,
                    snapshot.Views,
                    snapshot.Likes,
                    snapshot.Favorites,
                    snapshot.Comments,
                    snapshot.TrendScore
                ));
            }
        }

        return new RankingListDto(request.Period.ToString(), generatedAt, items);
    }
}
