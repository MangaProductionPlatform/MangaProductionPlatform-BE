using MediatR;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;

namespace MangaERP.Series.Application.Queries.GetSeriesAnalytics;

public record GetSeriesAnalyticsQuery(Guid SeriesId, Guid RequesterId, string RequesterRole) : IRequest<SeriesAnalyticsDto>;

public record SeriesAnalyticsDto(
    Guid SeriesId,
    int TotalViews,
    int TotalVotes,
    List<MonthlyTrendDto> MonthlyTrends,
    List<ChapterPublishTrendDto> ChapterTrends
);

public record MonthlyTrendDto(string Month, int Views, int Votes);
public record ChapterPublishTrendDto(string Month, int PublishedChapters);

public class GetSeriesAnalyticsHandler : IRequestHandler<GetSeriesAnalyticsQuery, SeriesAnalyticsDto>
{
    private readonly ISeriesRepository _seriesRepo;
    // Assuming there are repositories for analytics/ranking to fetch these.
    // If not, we will mock them for now or use the generic DbContext.

    public GetSeriesAnalyticsHandler(ISeriesRepository seriesRepo)
    {
        _seriesRepo = seriesRepo;
    }

    public async Task<SeriesAnalyticsDto> Handle(GetSeriesAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var series = await _seriesRepo.GetByIdAsync(request.SeriesId, cancellationToken);
        if (series == null)
            throw new KeyNotFoundException($"Series {request.SeriesId} not found.");

        if (request.RequesterRole == "Mangaka" && series.AuthorId != request.RequesterId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xem phân tích của bộ truyện này.");
        }

        // TODO: Implement actual data fetching from Ranking and Chapters.
        // For now, return mock data since Ranking module is 'Planned'.
        
        var mockMonthly = new List<MonthlyTrendDto>
        {
            new MonthlyTrendDto("2026-01", 1000, 50),
            new MonthlyTrendDto("2026-02", 1500, 80),
            new MonthlyTrendDto("2026-03", 2000, 120),
            new MonthlyTrendDto("2026-04", 1800, 90),
            new MonthlyTrendDto("2026-05", 2500, 150),
            new MonthlyTrendDto("2026-06", 3000, 200)
        };

        var mockChapterTrends = new List<ChapterPublishTrendDto>
        {
            new ChapterPublishTrendDto("2026-01", 1),
            new ChapterPublishTrendDto("2026-02", 1),
            new ChapterPublishTrendDto("2026-03", 2),
            new ChapterPublishTrendDto("2026-04", 1),
            new ChapterPublishTrendDto("2026-05", 2),
            new ChapterPublishTrendDto("2026-06", 2)
        };

        return new SeriesAnalyticsDto(
            request.SeriesId,
            mockMonthly.Sum(m => m.Views),
            mockMonthly.Sum(m => m.Votes),
            mockMonthly,
            mockChapterTrends
        );
    }
}
