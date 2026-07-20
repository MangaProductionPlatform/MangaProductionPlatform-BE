using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetMySubmissions;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka lấy danh sách submissions của mình, có thể filter theo status.
/// </summary>
public record GetMySubmissionsQuery(
    Guid SubmitterId,
    string? StatusFilter = null          // optional: "Draft", "Pending", "Approved", etc.
) : IRequest<IEnumerable<SubmissionSummaryDto>>;

public record SubmissionSummaryDto(
    Guid Id,
    string Title,
    string? Genre,
    string Status,
    string? FeedbackMessage,
    DateTime CreatedAt,
    DateTime? ReviewedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetMySubmissionsHandler
    : IRequestHandler<GetMySubmissionsQuery, IEnumerable<SubmissionSummaryDto>>
{
    private readonly ISubmissionRepository _repo;

    public GetMySubmissionsHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<IEnumerable<SubmissionSummaryDto>> Handle(
        GetMySubmissionsQuery query, CancellationToken ct)
    {
        var submissions = await _repo.GetBySubmitterIdAsync(query.SubmitterId, ct);

        // Optional client-side filter by status string
        if (!string.IsNullOrWhiteSpace(query.StatusFilter)
            && Enum.TryParse<SubmissionStatus>(query.StatusFilter, ignoreCase: true, out var parsedStatus))
        {
            submissions = submissions.Where(s => s.Status == parsedStatus);
        }

        return submissions.Select(s => new SubmissionSummaryDto(
            s.Id,
            s.Title,
            s.Genre,
            s.Status.ToString(),
            s.Status is SubmissionStatus.Tantou_Revision_Required or SubmissionStatus.Mangaka_Revision_Required
                ? s.TantouGuidance
                : null,
            s.CreatedAt,
            s.ReviewedAt));
    }
}
