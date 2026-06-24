using MangaERP.Identity.Domain.Enums;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Application.Queries.GetMySubmissions;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetSubmissionQueue;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Lấy danh sách submission queue.
/// - EDITORIAL_BOARD: chỉ trả về Pending_EB_Review mà user chưa vote trong vòng hiện tại.
/// - EDITOR_IN_CHIEF: trả về Conflict_Escalated (ưu tiên đầu) + Pending_EB_Review.
/// - ADMIN: trả về tất cả Pending_EB_Review (full view).
/// RequesterRole phải khớp với RoleNames constants.
/// </summary>
public record GetSubmissionQueueQuery(
    string RequesterRole,    // RoleNames constant e.g. "EDITORIAL_BOARD"
    Guid RequesterId         // UserId to filter un-voted submissions for EB
) : IRequest<IEnumerable<SubmissionSummaryDto>>;

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetSubmissionQueueHandler
    : IRequestHandler<GetSubmissionQueueQuery, IEnumerable<SubmissionSummaryDto>>
{
    private readonly ISubmissionRepository _repo;

    public GetSubmissionQueueHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<IEnumerable<SubmissionSummaryDto>> Handle(
        GetSubmissionQueueQuery query, CancellationToken ct)
    {
        IEnumerable<SeriesSubmission> submissions;

        switch (query.RequesterRole)
        {
            case RoleNames.EditorialBoard:
                // Only return submissions this editor hasn't voted on in the current round
                submissions = await _repo.GetPendingQueueNotVotedByAsync(query.RequesterId, ct);
                break;

            case RoleNames.EditorInChief:
                // Conflict_Escalated first (priority), then Pending_EB_Review
                submissions = await _repo.GetEICQueueAsync(ct);
                break;

            case RoleNames.Admin:
                // Full view of all pending submissions
                submissions = await _repo.GetRecommendedQueueAsync(ct);
                break;

            default:
                throw new UnauthorizedAccessException("You do not have access to the submission queue.");
        }

        return submissions.Select(s => new SubmissionSummaryDto(
            s.Id,
            s.Title,
            s.Genre,
            s.Status.ToString(),
            s.FeedbackMessage,
            s.CreatedAt,
            s.ReviewedAt));
    }
}
