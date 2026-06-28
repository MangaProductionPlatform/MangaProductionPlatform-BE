using FluentValidation;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Commands.RequestRevision;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MangaERP.Submission.Domain.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace MangaERP.Submission.Application.Commands.CastVote;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record CastVoteRequest(
    string VoteType,           // "APPROVE" | "REJECT" | "REQ_REVISION"
    string? Comment,
    List<FeedbackPinInput>? FeedbackPins);

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Editorial Board thành viên bỏ phiếu cho một submission đang Pending_EB_Review.
/// Sau khi phiếu thứ 3 được ghi nhận, hệ thống tự động chạy Aggregation Logic.
///
/// CONCURRENCY PROTECTION:
/// Toàn bộ logic đọc-ghi phiếu bầu và chốt trạng thái được bọc trong một
/// PostgreSQL Serializable transaction. Hơn nữa, submission row được khóa
/// ngay từ đầu transaction bằng SELECT ... FOR UPDATE (GetByIdForUpdateAsync),
/// đảm bảo tại một thời điểm chỉ DUY NHẤT 1 thread xử lý phiếu bầu cho
/// submission đó — loại bỏ hoàn toàn khả năng aggregation logic chạy 2 lần.
/// </summary>
public record CastVoteCommand(
    Guid SubmissionId,
    Guid EditorId,           // extracted from JWT by controller
    VoteType VoteType,
    string? Comment,
    List<FeedbackPinInput> FeedbackPins
) : IRequest<CastVoteResult>;

public record CastVoteResult(
    Guid SubmissionId,
    string SubmissionStatus,
    int TotalVotesInRound,
    string? AggregationOutcome,  // null if still waiting, "MAJORITY_APPROVE", "MAJORITY_REJECT", "MAJORITY_REVISION", "CONFLICT_ESCALATED"
    int RoundNumber
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class CastVoteHandler : IRequestHandler<CastVoteCommand, CastVoteResult>
{
    private readonly ISubmissionRepository _submissionRepo;
    private readonly IUserRepository _userRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IDbContextProvider _dbContextProvider;
    private readonly INotificationService _notificationService;

    public CastVoteHandler(
        ISubmissionRepository submissionRepo,
        IUserRepository userRepo,
        ISeriesRepository seriesRepo,
        IDbContextProvider dbContextProvider,
        INotificationService notificationService)
    {
        _submissionRepo = submissionRepo;
        _userRepo = userRepo;
        _seriesRepo = seriesRepo;
        _dbContextProvider = dbContextProvider;
        _notificationService = notificationService;
    }

    public async Task<CastVoteResult> Handle(CastVoteCommand cmd, CancellationToken ct)
    {
        var db = (DbContext)_dbContextProvider.GetDbContext();
        // NpgsqlRetryingExecutionStrategy is required to wrap manual transactions.
        var strategy = db.Database.CreateExecutionStrategy();

        CastVoteResult? result = null;
        string? postCommitAction = null;
        SeriesSubmission? loadedSubmission = null;

        await strategy.ExecuteAsync(async () =>
        {
            // ── Open transaction ──────────────────────────────────────────────────
            // The IsolationLevel is effectively serialized by the FOR UPDATE lock below.
            // PostgreSQL's default ReadCommitted + row-level FOR UPDATE lock guarantees
            // that only ONE concurrent thread can hold the lock on this submission row,
            // making explicit Serializable isolation redundant for this specific use case.
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // ── 1. PESSIMISTIC LOCK: SELECT ... FOR UPDATE ────────────────────────
            // This is the critical guard. GetByIdForUpdateAsync issues:
            //   SELECT * FROM "SeriesSubmissions" WHERE "Id" = @id FOR UPDATE
            // The row is exclusively locked until tx.CommitAsync(). Any other thread
            // calling this for the same SubmissionId will wait here — serialize access.
            var submission = await _submissionRepo.GetByIdForUpdateAsync(cmd.SubmissionId, ct)
                ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

            // ── 2. Validate state ─────────────────────────────────────────────────
            if (submission.Status != SubmissionStatus.Pending_EB_Review)
                throw new InvalidStateTransitionException(
                    $"Chỉ có thể vote khi bản thảo ở trạng thái Pending_EB_Review. " +
                    $"Trạng thái hiện tại: {submission.Status}");

            // ── 3. Validate RBAC role ─────────────────────────────────────────────
            // Note: RBAC check is done INSIDE the transaction so the user record is
            // also read under the same consistent snapshot.
            var hasRole = await _userRepo.HasRbacRoleAsync(cmd.EditorId, RoleNames.EditorialBoard, ct);
            if (!hasRole)
                throw new UnauthorizedAccessException("Chỉ thành viên Editorial Board mới có thể bỏ phiếu.");

            // ── 4. Check duplicate vote in this round ─────────────────────────────
            // Because the submission row is locked, submission.CurrentRound is stable —
            // no other thread can increment it while we hold the lock.
            var alreadyVoted = await _submissionRepo.HasVotedAsync(
                cmd.SubmissionId, cmd.EditorId, submission.CurrentRound, ct);
            if (alreadyVoted)
                throw new InvalidOperationException(
                    $"Bạn đã bỏ phiếu cho bản thảo này trong vòng {submission.CurrentRound} rồi.");

            // ── 5. Save vote ──────────────────────────────────────────────────────
            var vote = SubmissionVote.Create(
                cmd.SubmissionId, cmd.EditorId, cmd.VoteType, cmd.Comment, submission.CurrentRound);
            await _submissionRepo.AddVoteAsync(vote, ct);

            // Save feedback pins for REQ_REVISION votes
            if (cmd.VoteType == VoteType.REQ_REVISION && cmd.FeedbackPins.Count > 0)
            {
                var existingPins = await _submissionRepo.GetActivePinsBySubmissionIdAsync(cmd.SubmissionId, ct);
                foreach (var pin in existingPins) pin.Archive();

                foreach (var p in cmd.FeedbackPins)
                {
                    var pin = SubmissionFeedbackPin.Create(
                        cmd.SubmissionId, p.PageIdentifier, p.CoordinateX, p.CoordinateY,
                        p.Comment, p.Category, cmd.EditorId);
                    await _submissionRepo.AddPinAsync(pin, ct);
                }
            }

            // Persist vote record BEFORE counting — the FOR UPDATE lock prevents
            // another thread from concurrently reaching this point for the same submission.
            await _submissionRepo.SaveChangesAsync(ct);

            // ── 6. Count votes in this round ──────────────────────────────────────
            // Because we hold the FOR UPDATE lock, this count reflects the AUTHORITATIVE
            // state — no other vote can have been inserted concurrently for this row.
            var allVotes = (await _submissionRepo.GetVotesByRoundAsync(
                cmd.SubmissionId, submission.CurrentRound, ct)).ToList();

            int totalVotes = allVotes.Count;

            if (totalVotes < 3)
            {
                // Not enough votes yet — keep status as-is.
                // Store submission ref so we can query remaining editors post-commit.
                loadedSubmission = null; // Mốc 2 uses a separate path (postCommitAction == null)
                result = new CastVoteResult(
                    submission.Id,
                    submission.Status.ToString(),
                    totalVotes,
                    null,
                    submission.CurrentRound);
            }
            else
            {
                // ── 7. Aggregation Logic (runs exactly once, guaranteed by the lock) ──
                var approveCount  = allVotes.Count(v => v.VoteType == VoteType.APPROVE);
                var rejectCount   = allVotes.Count(v => v.VoteType == VoteType.REJECT);
                var revisionCount = allVotes.Count(v => v.VoteType == VoteType.REQ_REVISION);

                string outcome;

                if (approveCount >= 2)
                {
                    // Majority APPROVE → EB_Approved
                    outcome = "MAJORITY_APPROVE";
                    postCommitAction = "APPROVE";
                    submission.Approve(cmd.EditorId);
                }
                else if (rejectCount >= 2)
                {
                    // Majority REJECT → EB_Rejected
                    outcome = "MAJORITY_REJECT";
                    postCommitAction = "REJECT";
                    var feedbackMsg = allVotes
                        .Where(v => v.VoteType == VoteType.REJECT && v.Comment != null)
                        .Select(v => v.Comment!)
                        .FirstOrDefault() ?? "Bản thảo bị từ chối bởi đa số Ban Biên Tập.";
                    submission.Reject("EditorialBoard", cmd.EditorId, feedbackMsg);
                }
                else if (revisionCount >= 2)
                {
                    // Majority REVISION → Requires_Revision
                    outcome = "MAJORITY_REVISION";
                    postCommitAction = "REVISION";
                    var feedbackMsg = allVotes
                        .Where(v => v.VoteType == VoteType.REQ_REVISION && v.Comment != null)
                        .Select(v => v.Comment!)
                        .FirstOrDefault() ?? "Ban Biên Tập yêu cầu chỉnh sửa bản thảo.";
                    submission.RequestRevision("EditorialBoard", cmd.EditorId, feedbackMsg);
                }
                else
                {
                    // 1-1-1 deadlock → Conflict_Escalated
                    outcome = "CONFLICT_ESCALATED";
                    postCommitAction = "CONFLICT";
                    submission.EscalateConflict();
                }

                await _submissionRepo.SaveChangesAsync(ct);

                loadedSubmission = submission;
                result = new CastVoteResult(
                    submission.Id,
                    submission.Status.ToString(),
                    totalVotes,
                    outcome,
                    submission.CurrentRound);
            }

            // Release the FOR UPDATE lock by committing.
            await tx.CommitAsync(ct);
        });

        // ── 8. Post-commit side effects (outside transaction) ─────────────────────
        if (postCommitAction == "APPROVE" && loadedSubmission != null)
        {
            await HandleApprovePostCommitAsync(loadedSubmission, ct);
        }
        else if (postCommitAction == "REJECT" && loadedSubmission != null)
        {
            await _notificationService.NotifySubmissionRejectedAsync(
                receiverId:      loadedSubmission.SubmitterId,
                submissionId:    loadedSubmission.Id,
                feedbackMessage: loadedSubmission.FeedbackMessage ?? "Bị từ chối.",
                ct:              ct);
        }
        else if (postCommitAction == "REVISION" && loadedSubmission != null)
        {
            await _notificationService.NotifySubmissionRevisionAsync(
                receiverId:  loadedSubmission.SubmitterId,
                submissionId: loadedSubmission.Id,
                message:     loadedSubmission.FeedbackMessage ?? "Yêu cầu chỉnh sửa.",
                pinCount:    0,
                targetUrl:   "/mangaka/submissions",
                ct:          ct);
        }
        else if (postCommitAction == "CONFLICT" && loadedSubmission != null)
        {
            // [Mốc 4] Tranh chấp 1-1-1 → notify toàn bộ Editor-in-Chief
            var author = await _userRepo.GetByIdAsync(loadedSubmission.SubmitterId, ct);
            await _notificationService.NotifyConflictEscalatedToEicAsync(
                submissionId:    loadedSubmission.Id,
                submissionTitle: loadedSubmission.Title,
                authorName:      author?.FullName ?? "Không rõ",
                ct:              ct);
        }
        else if (postCommitAction == null && result != null && result.TotalVotesInRound < 3
                 && loadedSubmission == null)
        {
            // [Mốc 2] Chưa đủ 3 phiếu → notify các EB members chưa vote trong round này.
            // Tính danh sách: lấy tất cả EB rồi trừ những người đã vote.
            await NotifyRemainingEditorsAsync(cmd, result.TotalVotesInRound, ct);
        }

        return result!;
    }

    /// <summary>
    /// Post-commit: creates MangaSeries + load-balance assigns TantouEditor.
    /// Runs in a separate transaction AFTER the vote transaction has committed,
    /// so a failure here does not roll back the approved submission status.
    /// </summary>
    private async Task HandleApprovePostCommitAsync(SeriesSubmission submission, CancellationToken ct)
    {
        var db = (DbContext)_dbContextProvider.GetDbContext();
        var strategy = db.Database.CreateExecutionStrategy();

        Guid seriesId = Guid.Empty;
        Guid selectedTeId = Guid.Empty;

        await strategy.ExecuteAsync(async () =>
        {
            // Serializable isolation: prevents two concurrent vote-completions from reading
            // the same stale TE load data and assigning to the same editor.
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var allTE = await _userRepo.GetByRoleAsync(UserRole.TantouEditor, ct);
            var activeTE = allTE.Where(u => u.AccountStatus == AccountStatus.Active).ToList();
            if (!activeTE.Any())
                throw new InvalidOperationException("Không có Tantou Editor nào đang hoạt động để gán.");

            var activeTeIds = activeTE.Select(te => te.Id).ToList();
            var loads = await _userRepo.GetTantouEditorsLoadAsync(activeTeIds, ct);
            var selectedTe = activeTE
                .Select(te => new { Editor = te, Load = loads.GetValueOrDefault(te.Id, 0) })
                .OrderBy(x => x.Load).ThenBy(x => x.Editor.CreatedAt)
                .Select(x => x.Editor)
                .First();

            var series = MangaSeries.Create(
                authorId:      submission.SubmitterId,
                submissionId:  submission.Id,
                title:         submission.Title,
                description:   submission.Description,
                genre:         submission.Genre,
                coverImageUrl: submission.CoverImageUrl);

            var mangaka = await _userRepo.GetByIdAsync(submission.SubmitterId, ct)
                ?? throw new InvalidOperationException($"Mangaka {submission.SubmitterId} not found.");
            mangaka.ManagingTantouId = selectedTe.Id;

            await _seriesRepo.AddAsync(series, ct);
            await _userRepo.UpdateAsync(mangaka, ct);

            seriesId = series.Id;
            selectedTeId = selectedTe.Id;

            await tx.CommitAsync(ct);
        });

        await _notificationService.NotifySubmissionApprovedAsync(
            receiverId:   submission.SubmitterId,
            submissionId: submission.Id,
            seriesId:     seriesId,
            seriesTitle:  submission.Title,
            ct:           ct);

        // [Mốc 3] Notify Tantou Editor được gán phụ trách
        var mangakaUser = await _userRepo.GetByIdAsync(submission.SubmitterId, ct);
        await _notificationService.NotifyTantouEditorAssignedAsync(
            tantouEditorId: selectedTeId,
            submissionId:   submission.Id,
            seriesTitle:    submission.Title,
            authorName:     mangakaUser?.FullName ?? "Không rõ",
            ct:             ct);
    }

    /// <summary>
    /// [Mốc 2] Helper: Tính danh sách EB chưa vote trong round hiện tại rồi gửi notify.
    /// Chạy sau khi transaction commit xong — an toàn vì chỉ đọc dữ liệu.
    /// </summary>
    private async Task NotifyRemainingEditorsAsync(
        CastVoteCommand cmd, int currentVoteCount, CancellationToken ct)
    {
        // Load voter name
        var voter = await _userRepo.GetByIdAsync(cmd.EditorId, ct);
        var voterName = voter?.FullName ?? "Một thành viên";

        // Load submission to get round number and title
        var submission = await _submissionRepo.GetByIdAsync(cmd.SubmissionId, ct);
        if (submission is null) return;

        // Load all EB members via UserRole enum
        var allEbMembers = await _userRepo.GetByRoleAsync(
            MangaERP.Identity.Domain.Enums.UserRole.EditorialBoard, ct);

        // Load all votes already cast in the current round
        var votesInRound = await _submissionRepo.GetVotesByRoundAsync(
            cmd.SubmissionId, submission.CurrentRound, ct);
        var votedEditorIds = votesInRound.Select(v => v.EditorId).ToHashSet();

        // Remaining = all EB members who have NOT voted yet (and exclude the current voter)
        var remainingIds = allEbMembers
            .Where(u => !votedEditorIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToList();

        if (!remainingIds.Any()) return;

        await _notificationService.NotifyVoteCastToRemainingEditorsAsync(
            submissionId:       cmd.SubmissionId,
            submissionTitle:    submission.Title,
            voterName:          voterName,
            currentVoteCount:   currentVoteCount,
            totalRequired:      3,
            remainingEditorIds: remainingIds,
            ct:                 ct);
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class CastVoteValidator : AbstractValidator<CastVoteCommand>
{
    public CastVoteValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
        RuleFor(x => x.Comment)
            .MaximumLength(2000).WithMessage("Comment không được vượt quá 2000 ký tự.");
        RuleForEach(x => x.FeedbackPins).ChildRules(pin =>
        {
            pin.RuleFor(p => p.PageIdentifier).NotEmpty();
            pin.RuleFor(p => p.CoordinateX).InclusiveBetween(0, 100);
            pin.RuleFor(p => p.CoordinateY).InclusiveBetween(0, 100);
            pin.RuleFor(p => p.Comment).NotEmpty().MaximumLength(2000);
        });
    }
}
