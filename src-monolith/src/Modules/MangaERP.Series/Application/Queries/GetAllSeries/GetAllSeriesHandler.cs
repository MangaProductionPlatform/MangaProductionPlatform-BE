using MangaERP.Series.Application.Ports;
using MangaERP.Series.Application.Queries.GetMySeries;
using MangaERP.Series.Domain.Entities;
using MediatR;

namespace MangaERP.Series.Application.Queries.GetAllSeries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Admin/EB/EIC xem toàn bộ series. TantouEditor chỉ xem series của Mangaka mình quản lý.
/// Hỗ trợ filter tùy chọn theo SeriesStatus.
/// Route: GET /api/v1/series?status=Active|Hiatus|Cancelled
/// </summary>
public record GetAllSeriesQuery(
    Guid RequesterId,
    string RequesterRole,
    SeriesStatus? StatusFilter = null)
    : IRequest<IEnumerable<SeriesSummaryDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetAllSeriesHandler
    : IRequestHandler<GetAllSeriesQuery, IEnumerable<SeriesSummaryDto>>
{
    private readonly ISeriesRepository _repo;

    public GetAllSeriesHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<IEnumerable<SeriesSummaryDto>> Handle(
        GetAllSeriesQuery query, CancellationToken ct)
    {
        var all = query.RequesterRole == "TantouEditor"
            ? await _repo.GetByManagingTantouIdAsync(query.RequesterId, ct)
            : await _repo.GetAllAsync(ct);

        if (query.StatusFilter.HasValue)
            all = all.Where(s => s.Status == query.StatusFilter.Value);

        return all.Select(s => new SeriesSummaryDto(
            s.Id,
            s.Title,
            s.Genre,
            s.CoverImageUrl,
            s.Status.ToString(),
            s.SubmissionId,
            s.CreatedAt));
    }
}
