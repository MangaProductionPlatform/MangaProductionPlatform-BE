using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Series.Application.Queries.GetCancellationQueue;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// EB/EIC/Admin lấy danh sách series đang có yêu cầu hủy chờ duyệt.
/// Route: GET /api/v1/series/cancellation-queue
/// </summary>
public record GetCancellationQueueQuery : IRequest<IEnumerable<CancellationQueueItemDto>>;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CancellationQueueItemDto(
    Guid SeriesId,
    string Title,
    string? Genre,
    string? CoverImageUrl,
    Guid AuthorId,
    string SeriesStatus,
    string CancellationStatus,
    string CancellationReason,
    Guid RequestedById,
    DateTime RequestedAt
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetCancellationQueueHandler
    : IRequestHandler<GetCancellationQueueQuery, IEnumerable<CancellationQueueItemDto>>
{
    private readonly ISeriesRepository _repo;

    public GetCancellationQueueHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<IEnumerable<CancellationQueueItemDto>> Handle(
        GetCancellationQueueQuery request, CancellationToken ct)
    {
        var queue = await _repo.GetCancellationQueueAsync(ct);

        return queue.Select(s => new CancellationQueueItemDto(
            s.Id,
            s.Title,
            s.Genre,
            s.CoverImageUrl,
            s.AuthorId,
            s.Status.ToString(),
            s.CancellationStatus.ToString(),
            s.CancellationReason ?? string.Empty,
            s.CancellationRequestedById!.Value,
            s.CancellationRequestedAt!.Value
        ));
    }
}
