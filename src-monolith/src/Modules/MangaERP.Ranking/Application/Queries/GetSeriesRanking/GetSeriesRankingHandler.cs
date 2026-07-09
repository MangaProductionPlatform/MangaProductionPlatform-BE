using MediatR;
using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Ranking.Application.Queries.GetRankings;

namespace MangaERP.Ranking.Application.Queries.GetSeriesRanking;

public record GetSeriesRankingQuery(Guid SeriesId, RankingPeriod Period) : IRequest<RankingItemDto?>;

public class GetSeriesRankingHandler : IRequestHandler<GetSeriesRankingQuery, RankingItemDto?>
{
    private readonly IRankingRepository _rankingRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetSeriesRankingHandler(IRankingRepository rankingRepo, ISeriesRepository seriesRepo)
    {
        _rankingRepo = rankingRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<RankingItemDto?> Handle(GetSeriesRankingQuery request, CancellationToken cancellationToken)
    {
        var snapshot = await _rankingRepo.GetBySeriesIdAsync(request.SeriesId, request.Period, cancellationToken);
        if (snapshot == null) return null;

        var series = await _seriesRepo.GetByIdAsync(snapshot.SeriesId, cancellationToken);
        if (series == null) return null;

        return new RankingItemDto(
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
        );
    }
}
