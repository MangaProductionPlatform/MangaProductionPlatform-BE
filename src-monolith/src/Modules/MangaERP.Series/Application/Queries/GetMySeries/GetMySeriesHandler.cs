using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MediatR;

namespace MangaERP.Series.Application.Queries.GetMySeries;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka lấy danh sách series của mình (đã được Board approve).
/// AuthorId được controller trích từ JWT claim.
/// </summary>
public record GetMySeriesQuery(Guid AuthorId) : IRequest<IEnumerable<SeriesSummaryDto>>;

public record SeriesSummaryDto(
    Guid   Id,
    string Title,
    string? Genre,
    string? CoverImageUrl,
    string  Status,
    Guid?   SubmissionId,
    DateTime CreatedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetMySeriesHandler
    : IRequestHandler<GetMySeriesQuery, IEnumerable<SeriesSummaryDto>>
{
    private readonly ISeriesRepository _repo;

    public GetMySeriesHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<IEnumerable<SeriesSummaryDto>> Handle(
        GetMySeriesQuery query, CancellationToken ct)
    {
        var series = await _repo.GetByAuthorIdAsync(query.AuthorId, ct);

        return series.Select(s => new SeriesSummaryDto(
            s.Id,
            s.Title,
            s.Genre,
            s.CoverImageUrl,
            s.Status.ToString(),
            s.SubmissionId,
            s.CreatedAt));
    }
}
