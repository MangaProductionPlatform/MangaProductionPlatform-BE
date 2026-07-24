using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MediatR;

namespace MangaERP.Series.Application.Queries.GetSeriesDetail;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Lấy chi tiết một MangaSeries theo Id.
/// Mangaka chỉ có thể xem series của mình (authorId guard trong handler).
/// Admin/EditorialBoard/EditorInChief có thể xem tất cả. TantouEditor chỉ xem series của Mangaka mình quản lý.
/// </summary>
public record GetSeriesDetailQuery(
    Guid   SeriesId,
    Guid   RequesterId,
    string RequesterRole
) : IRequest<SeriesDetailDto>;

public record SeriesDetailDto(
    Guid    Id,
    string  Title,
    string? Description,
    string? Genre,
    string? CoverImageUrl,
    string  Status,
    Guid    AuthorId,
    Guid?   SubmissionId,
    DateTime CreatedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetSeriesDetailHandler
    : IRequestHandler<GetSeriesDetailQuery, SeriesDetailDto>
{
    private readonly ISeriesRepository _repo;

    public GetSeriesDetailHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<SeriesDetailDto> Handle(
        GetSeriesDetailQuery query, CancellationToken ct)
    {
        var series = await _repo.GetByIdAsync(query.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {query.SeriesId} not found.");

        if (query.RequesterRole == "TantouEditor")
        {
            var isManaged = await _repo.IsManagedByTantouAsync(query.SeriesId, query.RequesterId, ct);
            if (!isManaged)
                throw new UnauthorizedAccessException("You are not assigned to manage this series.");
        }
        else
        {
            var canViewAll = query.RequesterRole is "Admin" or "EditorialBoard" or "EditorInChief";
            if (!canViewAll && series.AuthorId != query.RequesterId)
                throw new UnauthorizedAccessException("You are not allowed to view this series.");
        }

        return new SeriesDetailDto(
            series.Id,
            series.Title,
            series.Description,
            series.Genre,
            MangaERP.Shared.Application.Helpers.MediaUrlSanitizer.Sanitize(series.CoverImageUrl),
            series.Status.ToString(),
            series.AuthorId,
            series.SubmissionId,
            series.CreatedAt);
    }
}
