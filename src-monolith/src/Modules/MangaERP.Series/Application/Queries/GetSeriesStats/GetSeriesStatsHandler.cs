using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MediatR;

namespace MangaERP.Series.Application.Queries.GetSeriesStats;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Internal query — Series module tự tổng hợp stats của mình.
/// Được gọi bởi GetAdminDashboardHandler và GetBoardReportsHandler ở Api layer.
/// KHÔNG leak ISeriesRepository ra ngoài module.
/// </summary>
public record GetSeriesStatsQuery(DateTime? StartDate = null, DateTime? EndDate = null) : IRequest<SeriesStatsResult>;

public record SeriesStatsResult(
    int TotalSeries,
    int Active,
    int Hiatus,
    int Cancelled,
    int PendingCancellationRequests,
    // Dùng thêm cho BoardReports
    int CancellationApprovedLast30Days,
    int CancellationRejectedLast30Days
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetSeriesStatsHandler : IRequestHandler<GetSeriesStatsQuery, SeriesStatsResult>
{
    private readonly ISeriesRepository _repo;

    public GetSeriesStatsHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<SeriesStatsResult> Handle(GetSeriesStatsQuery request, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var query = (await _repo.GetAllAsync(ct)).AsQueryable();

        if (request.StartDate.HasValue)
            query = query.Where(s => s.CreatedAt >= request.StartDate.Value);
        if (request.EndDate.HasValue)
            query = query.Where(s => s.CreatedAt <= request.EndDate.Value);

        var all = query.ToList();

        return new SeriesStatsResult(
            TotalSeries:                     all.Count,
            Active:                          all.Count(s => s.Status == SeriesStatus.Active),
            Hiatus:                          all.Count(s => s.Status == SeriesStatus.Hiatus),
            Cancelled:                       all.Count(s => s.Status == SeriesStatus.Cancelled),
            PendingCancellationRequests:     all.Count(s => s.CancellationStatus == CancellationRequestStatus.Pending),
            CancellationApprovedLast30Days:  all.Count(s =>
                s.CancellationStatus == CancellationRequestStatus.Approved &&
                s.CancellationReviewedAt >= cutoff),
            CancellationRejectedLast30Days:  all.Count(s =>
                s.CancellationStatus == CancellationRequestStatus.Rejected &&
                s.CancellationReviewedAt >= cutoff)
        );
    }
}
