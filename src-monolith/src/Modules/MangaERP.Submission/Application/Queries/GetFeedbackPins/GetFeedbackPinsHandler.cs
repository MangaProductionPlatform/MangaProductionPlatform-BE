using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetFeedbackPins;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record FeedbackPinDto(
    Guid Id,
    string PageIdentifier,
    double CoordinateX,
    double CoordinateY,
    string Comment,
    string Category,
    Guid CreatedByUserId,
    bool IsArchived,
    DateTime CreatedAt);

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Lấy danh sách feedback pins của submission.
/// IncludeArchived = false (default): chỉ trả active pins cho canvas hiện tại.
/// IncludeArchived = true: trả tất cả pins kèm lịch sử revision rounds.
/// </summary>
public record GetFeedbackPinsQuery(
    Guid SubmissionId,
    Guid RequesterId,
    string RequesterRole,
    bool IncludeArchived = false
) : IRequest<IEnumerable<FeedbackPinDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetFeedbackPinsHandler
    : IRequestHandler<GetFeedbackPinsQuery, IEnumerable<FeedbackPinDto>>
{
    private readonly ISubmissionRepository _repo;

    public GetFeedbackPinsHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<IEnumerable<FeedbackPinDto>> Handle(
        GetFeedbackPinsQuery query, CancellationToken ct)
    {
        // 1. Validate submission exists and requester has access
        var submission = await _repo.GetByIdAsync(query.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {query.SubmissionId} not found.");

        if (query.RequesterRole == "Mangaka" && submission.SubmitterId != query.RequesterId)
            throw new UnauthorizedAccessException("You are not the owner of this submission.");

        // 2. Fetch pins based on archive filter
        IEnumerable<SubmissionFeedbackPin> pins;
        if (query.IncludeArchived)
        {
            pins = await _repo.GetAllPinsBySubmissionIdAsync(query.SubmissionId, ct);
        }
        else
        {
            pins = await _repo.GetActivePinsBySubmissionIdAsync(query.SubmissionId, ct);
        }

        // 3. Map to DTOs
        return pins.Select(p => new FeedbackPinDto(
            p.Id,
            p.PageIdentifier,
            p.CoordinateX,
            p.CoordinateY,
            p.Comment,
            p.Category.ToString(),
            p.CreatedByUserId,
            p.IsArchived,
            p.CreatedAt));
    }
}
