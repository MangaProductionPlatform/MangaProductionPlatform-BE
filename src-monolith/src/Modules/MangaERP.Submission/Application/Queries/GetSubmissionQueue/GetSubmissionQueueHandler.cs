using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Application.Queries.GetMySubmissions;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetSubmissionQueue;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Lấy danh sách submission queue:
/// - TantouEditor: thấy Pending + UnderReview
/// - EditorialBoard: thấy RecommendedToBoard
/// </summary>
public record GetSubmissionQueueQuery(
    string RequesterRole     // "TantouEditor" or "EditorialBoard"
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
        var submissions = query.RequesterRole switch
        {
            "TantouEditor"   => await _repo.GetPendingQueueAsync(ct),
            "EditorialBoard" => await _repo.GetRecommendedQueueAsync(ct),
            "Admin"          => (await _repo.GetPendingQueueAsync(ct))
                                    .Concat(await _repo.GetRecommendedQueueAsync(ct)),
            _ => throw new UnauthorizedAccessException("You do not have access to the submission queue.")
        };

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
