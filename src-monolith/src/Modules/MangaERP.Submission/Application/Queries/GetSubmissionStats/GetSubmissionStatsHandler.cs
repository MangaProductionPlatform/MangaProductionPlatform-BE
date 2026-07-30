using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetSubmissionStats;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Internal query — Submission module tự tổng hợp stats của mình.
/// Được gọi bởi GetAdminDashboardHandler và GetBoardReportsHandler ở Api layer.
/// KHÔNG leak ISubmissionRepository ra ngoài module.
/// </summary>
public record GetSubmissionStatsQuery(DateTime? StartDate = null, DateTime? EndDate = null) : IRequest<SubmissionStatsResult>;

public record SubmissionStatsResult(
    int TotalSubmissions,
    int Draft,
    int PendingEBReview,
    int RequiresRevision,
    int ConflictEscalated,
    int EBApproved,
    int EBRejected,
    // Dùng thêm cho BoardReports
    int ApprovedLast30Days,
    int RejectedLast30Days
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetSubmissionStatsHandler : IRequestHandler<GetSubmissionStatsQuery, SubmissionStatsResult>
{
    private readonly ISubmissionRepository _repo;

    public GetSubmissionStatsHandler(ISubmissionRepository repo) => _repo = repo;

    public async Task<SubmissionStatsResult> Handle(GetSubmissionStatsQuery request, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        var query = (await _repo.GetAllAsync(ct)).AsQueryable();

        if (request.StartDate.HasValue)
            query = query.Where(s => s.CreatedAt >= request.StartDate.Value.Date);
        if (request.EndDate.HasValue)
        {
            var nextDay = request.EndDate.Value.Date.AddDays(1);
            query = query.Where(s => s.CreatedAt < nextDay);
        }

        var all = query.ToList();

        return new SubmissionStatsResult(
            TotalSubmissions:  all.Count,
            Draft:             all.Count(s => s.Status == SubmissionStatus.Draft),
            PendingEBReview:   all.Count(s => s.Status == SubmissionStatus.Pending_EB_Review || s.Status == SubmissionStatus.Pending_Tantou_Review),
            RequiresRevision:  all.Count(s => s.Status == SubmissionStatus.Requires_Revision || s.Status == SubmissionStatus.Tantou_Revision_Required || s.Status == SubmissionStatus.Mangaka_Revision_Required || s.Status == SubmissionStatus.Editorial_Rejected_To_Tantou),
            ConflictEscalated: all.Count(s => s.Status == SubmissionStatus.Conflict_Escalated),
            EBApproved:        all.Count(s => s.Status == SubmissionStatus.EB_Approved),
            EBRejected:        all.Count(s => s.Status == SubmissionStatus.EB_Rejected),
            ApprovedLast30Days: all.Count(s =>
                s.Status == SubmissionStatus.EB_Approved && s.ReviewedAt >= cutoff),
            RejectedLast30Days: all.Count(s =>
                s.Status == SubmissionStatus.EB_Rejected && s.ReviewedAt >= cutoff)
        );
    }
}
