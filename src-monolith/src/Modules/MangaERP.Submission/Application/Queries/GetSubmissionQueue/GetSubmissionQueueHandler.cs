using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Application.Queries.GetMySubmissions;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetSubmissionQueue;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Lấy danh sách submission queue cho Editorial Board / Admin.
/// Trả về các submissions có trạng thái Pending_EB_Review.
/// </summary>
public record GetSubmissionQueueQuery(
    string RequesterRole     // "EditorialBoard" or "Admin"
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
        if (query.RequesterRole != "EditorialBoard" && query.RequesterRole != "Admin")
        {
            throw new UnauthorizedAccessException("You do not have access to the submission queue.");
        }

        var submissions = await _repo.GetRecommendedQueueAsync(ct);

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
