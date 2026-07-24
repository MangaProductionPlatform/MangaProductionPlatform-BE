using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class SubmissionRepository : ISubmissionRepository
{
    private readonly AppDbContext _db;

    public SubmissionRepository(IDbContextProvider provider)
        => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task<SeriesSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SeriesSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct);

    /// <summary>Lấy TẤT CẢ submissions — dùng cho Admin Dashboard stats.</summary>
    public async System.Threading.Tasks.Task<IEnumerable<SeriesSubmission>> GetAllAsync(CancellationToken ct = default)
        => await _db.SeriesSubmissions.ToListAsync(ct);

    /// <summary>
    /// Pessimistic row-level lock — translates to:
    ///   SELECT ... FROM "SeriesSubmissions" WHERE "Id" = {id} FOR UPDATE
    /// Must be called inside an open transaction.
    /// Guarantees only one concurrent thread proceeds with vote counting and
    /// status transitions for this submission at a time.
    /// </summary>
    public async System.Threading.Tasks.Task<SeriesSubmission?> GetByIdForUpdateAsync(
        Guid id, CancellationToken ct = default)
    {
        // EF Core does not have native FOR UPDATE support.
        // We use FromSqlRaw to issue the lock. The query respects the active transaction.
        // IgnoreQueryFilters is NOT needed here — soft-delete filter is fine.
        var results = await _db.SeriesSubmissions
            .FromSqlRaw(
                @"SELECT * FROM ""SeriesSubmissions"" WHERE ""Id"" = {0} AND ""IsDeleted"" = false FOR UPDATE",
                id)
            .ToListAsync(ct);

        return results.FirstOrDefault();
    }

    public async System.Threading.Tasks.Task<IEnumerable<SeriesSubmission>> GetBySubmitterIdAsync(Guid submitterId, CancellationToken ct = default)
        => await _db.SeriesSubmissions
            .Where(s => s.SubmitterId == submitterId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Lấy submissions Pending_EB_Review — dùng cho EIC hoặc generic queue.
    /// </summary>
    public async System.Threading.Tasks.Task<IEnumerable<SeriesSubmission>> GetRecommendedQueueAsync(CancellationToken ct = default)
        => await _db.SeriesSubmissions
            .Where(s => s.Status == SubmissionStatus.Pending_EB_Review)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Queue cho Editor-in-Chief: Conflict_Escalated đứng trước (ưu tiên cao),
    /// sau đó Pending_EB_Review theo thời gian tạo.
    /// </summary>
    public async System.Threading.Tasks.Task<IEnumerable<SeriesSubmission>> GetEICQueueAsync(CancellationToken ct = default)
        => await _db.SeriesSubmissions
            .Where(s => s.Status == SubmissionStatus.Conflict_Escalated
                     || s.Status == SubmissionStatus.Pending_EB_Review)
            .OrderByDescending(s => s.Status == SubmissionStatus.Conflict_Escalated)  // Conflict_Escalated first
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(ct);

    /// <summary>
    /// Queue cho Editorial Board: Pending_EB_Review mà editor chưa vote trong vòng hiện tại.
    ///
    /// CRITICAL CORRECTNESS: The NOT EXISTS check matches BOTH SubmissionId AND the
    /// submission's CurrentRound — this is a server-side LEFT JOIN / NOT EXISTS query
    /// so that an editor who voted in Round 1 is correctly shown Round 2 submissions
    /// after a REQ_REVISION + re-submit cycle.
    ///
    /// Generated SQL equivalent:
    ///   SELECT s.* FROM SeriesSubmissions s
    ///   WHERE s.Status = 'Pending_EB_Review'
    ///   AND NOT EXISTS (
    ///     SELECT 1 FROM SubmissionVotes v
    ///     WHERE v.SubmissionId = s.Id
    ///       AND v.EditorId = @editorId
    ///       AND v.RoundNumber = s.CurrentRound   -- key: matches the live round
    ///   )
    ///   ORDER BY s.CreatedAt
    /// </summary>
    public async System.Threading.Tasks.Task<IEnumerable<SeriesSubmission>> GetPendingQueueNotVotedByAsync(
        Guid editorId, CancellationToken ct = default)
    {
        // Single server-side query using a LEFT JOIN / NOT EXISTS anti-join pattern.
        // EF Core translates the subquery into NOT EXISTS efficiently on PostgreSQL.
        return await _db.SeriesSubmissions
            .Where(s => s.Status == SubmissionStatus.Pending_EB_Review
                && !_db.SubmissionVotes.Any(v =>
                    v.SubmissionId == s.Id
                    && v.EditorId == editorId
                    && v.RoundNumber == s.CurrentRound))  // ← bound to s.CurrentRound, not a constant
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task<bool> HasActiveSubmissionAsync(Guid submitterId, string title, CancellationToken ct = default)
        => await _db.SeriesSubmissions.AnyAsync(s =>
            s.SubmitterId == submitterId &&
            s.Title.ToLower() == title.ToLower() &&
            s.Status != SubmissionStatus.EB_Approved &&
            s.Status != SubmissionStatus.EB_Rejected,
            ct);

    public async System.Threading.Tasks.Task AddAsync(SeriesSubmission submission, CancellationToken ct = default)
        => await _db.SeriesSubmissions.AddAsync(submission, ct);

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    // ── Feedback Pins ─────────────────────────────────────────────────────────

    public async System.Threading.Tasks.Task<IEnumerable<SubmissionFeedbackPin>> GetActivePinsBySubmissionIdAsync(
        Guid submissionId, CancellationToken ct = default)
        => await _db.SubmissionFeedbackPins
            .Where(p => p.SubmissionId == submissionId && !p.IsArchived)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<SubmissionFeedbackPin>> GetAllPinsBySubmissionIdAsync(
        Guid submissionId, CancellationToken ct = default)
        => await _db.SubmissionFeedbackPins
            .Where(p => p.SubmissionId == submissionId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task AddPinAsync(SubmissionFeedbackPin pin, CancellationToken ct = default)
        => await _db.SubmissionFeedbackPins.AddAsync(pin, ct);

    // ── Submission Votes ──────────────────────────────────────────────────────

    public System.Threading.Tasks.Task<bool> HasVotedAsync(
        Guid submissionId, Guid editorId, int roundNumber, CancellationToken ct = default)
        => _db.SubmissionVotes.AnyAsync(
            v => v.SubmissionId == submissionId && v.EditorId == editorId && v.RoundNumber == roundNumber, ct);

    public async System.Threading.Tasks.Task<IEnumerable<SubmissionVote>> GetVotesByRoundAsync(
        Guid submissionId, int roundNumber, CancellationToken ct = default)
        => await _db.SubmissionVotes
            .Where(v => v.SubmissionId == submissionId && v.RoundNumber == roundNumber)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task AddVoteAsync(SubmissionVote vote, CancellationToken ct = default)
        => await _db.SubmissionVotes.AddAsync(vote, ct);

    /// <summary>
    /// Deletes in-progress votes for a specific round of a submission.
    /// Called by Admin force-action handlers before committing a status override,
    /// to keep the SubmissionVotes table consistent with the final submission state.
    /// </summary>
    public async System.Threading.Tasks.Task DeleteVotesByRoundAsync(
        Guid submissionId, int roundNumber, CancellationToken ct = default)
    {
        var votes = await _db.SubmissionVotes
            .Where(v => v.SubmissionId == submissionId && v.RoundNumber == roundNumber)
            .ToListAsync(ct);

        if (votes.Count > 0)
        {
            _db.SubmissionVotes.RemoveRange(votes);
            // Caller is responsible for SaveChanges inside the wrapping transaction.
        }
    }

    /// <summary>
    /// Deletes ALL votes for a submission across all rounds.
    /// Used when Admin permanently closes a submission (approved / rejected),
    /// ensuring no dangling vote history contradicts the terminal state.
    /// </summary>
    public async System.Threading.Tasks.Task DeleteAllVotesAsync(
        Guid submissionId, CancellationToken ct = default)
    {
        var votes = await _db.SubmissionVotes
            .Where(v => v.SubmissionId == submissionId)
            .ToListAsync(ct);

        if (votes.Count > 0)
            _db.SubmissionVotes.RemoveRange(votes);
        // Caller is responsible for SaveChanges inside the wrapping transaction.
    }

    public async System.Threading.Tasks.Task AssignEditorialReviewersAsync(
        Guid submissionId, int roundNumber, string authorName, CancellationToken ct = default)
    {
        var submission = await _db.SeriesSubmissions.FindAsync([submissionId], ct)
            ?? throw new KeyNotFoundException($"Submission {submissionId} not found.");

        var ebReviewers = await _db.Users
            .Where(u => u.AccountStatus == MangaERP.Identity.Domain.Enums.AccountStatus.Active && !u.IsDeleted &&
                (u.Role == MangaERP.Identity.Domain.Enums.UserRole.EditorialBoard || u.UserRoles.Any(ur => ur.Role.Name == MangaERP.Identity.Domain.Enums.RoleNames.EditorialBoard)))
            .OrderBy(u => u.Id)
            .ToListAsync(ct);

        if (ebReviewers.Count < 2)
            throw new InvalidOperationException("Hệ thống yêu cầu ít nhất 2 thành viên Ban Biên Tập (Editorial Board) đang hoạt động để phân công duyệt bản thảo.");

        var reviewer1 = ebReviewers[0];
        var reviewer2 = ebReviewers[1];

        var existingAssignments = await _db.EditorialReviewAssignments
            .Where(a => a.WorkType == EditorialWorkType.SeriesSubmission && a.WorkId == submissionId && a.RoundNumber == roundNumber)
            .ToListAsync(ct);

        if (!existingAssignments.Any(a => a.ReviewerId == reviewer1.Id))
        {
            _db.EditorialReviewAssignments.Add(
                EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, submissionId, roundNumber, reviewer1.Id));
        }

        if (!existingAssignments.Any(a => a.ReviewerId == reviewer2.Id))
        {
            _db.EditorialReviewAssignments.Add(
                EditorialReviewAssignment.Assign(EditorialWorkType.SeriesSubmission, submissionId, roundNumber, reviewer2.Id));
        }

        var notifTitle = "Bản thảo mới cần duyệt";
        var notifMsg = $"Bản thảo \"{submission.Title}\" của Tác giả {authorName} đang chờ đánh giá từ Ban Biên Tập.";

        // [DEMO] Broadcast to ALL active EB members so any EB account can see and vote.
        // In production, replace ebReviewers with new[] { reviewer1, reviewer2 } to notify only assigned reviewers.
        foreach (var reviewer in ebReviewers)
        {
            var notifExists = await _db.Notifications.AnyAsync(n =>
                n.ReceiverId == reviewer.Id &&
                n.RelatedEntityId == submissionId &&
                n.NotifyType == "EditorialReviewAssignment" &&
                n.Message.Contains($"Vòng {roundNumber}"), ct);

            if (!notifExists)
            {
                _db.Notifications.Add(new MangaERP.Publishing.Domain.Entities.Notification
                {
                    ReceiverId = reviewer.Id,
                    Title = notifTitle,
                    Message = $"{notifMsg} (Vòng {roundNumber})",
                    NotifyType = "EditorialReviewAssignment",
                    RelatedEntityId = submissionId,
                    RelatedEntityType = "SeriesSubmission",
                    TargetUrl = "/editorial-workflow/reviews"
                });
            }
        }
    }
}
