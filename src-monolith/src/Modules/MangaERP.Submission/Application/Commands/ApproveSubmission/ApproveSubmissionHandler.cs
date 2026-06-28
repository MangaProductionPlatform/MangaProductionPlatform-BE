using FluentValidation;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Ports;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Submission.Application.Commands.ApproveSubmission;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// [ADMIN FORCE-APPROVE] Override: Pending_EB_Review | Conflict_Escalated → EB_Approved.
/// Đồng thời tạo MangaSeries và gán Tantou Editor phụ trách trong cùng một DB transaction.
/// ReviewerId được controller trích từ JWT claim.
///
/// STATE CLEANUP: Nếu submission đang trong luồng bỏ phiếu dở dang
/// (có phiếu bầu ở CurrentRound), toàn bộ phiếu bầu trong vòng hiện tại
/// sẽ bị XÓA trước khi chốt trạng thái — tránh mâu thuẫn dữ liệu trong SubmissionVotes.
/// </summary>
public record ApproveSubmissionCommand(
    Guid SubmissionId,
    Guid ReviewerId          // extracted from JWT by controller (Admin)
) : IRequest<ApproveSubmissionResult>;

public record ApproveSubmissionResult(
    Guid SubmissionId,
    Guid SeriesId,
    Guid AssignedEditorId,
    string SubmissionStatus,
    string SeriesStatus,
    DateTime ApprovedAt,
    int VotesClearedCount);   // informational: how many dangling votes were purged

// ── Handler ───────────────────────────────────────────────────────────────────

public class ApproveSubmissionHandler
    : IRequestHandler<ApproveSubmissionCommand, ApproveSubmissionResult>
{
    private readonly ISubmissionRepository _submissionRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;
    private readonly IDbContextProvider _dbContextProvider;
    private readonly INotificationService _notificationService;

    public ApproveSubmissionHandler(
        ISubmissionRepository submissionRepo,
        ISeriesRepository seriesRepo,
        IUserRepository userRepo,
        IDbContextProvider dbContextProvider,
        INotificationService notificationService)
    {
        _submissionRepo = submissionRepo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
        _dbContextProvider = dbContextProvider;
        _notificationService = notificationService;
    }

    public async Task<ApproveSubmissionResult> Handle(
        ApproveSubmissionCommand cmd, CancellationToken ct)
    {
        var db = (DbContext)_dbContextProvider.GetDbContext();

        // ── Idempotency guard ─────────────────────────────────────────────────
        var existingSeries = await _seriesRepo.GetBySubmissionIdAsync(cmd.SubmissionId, ct);
        if (existingSeries is not null)
        {
            var submissionForSeries = await _submissionRepo.GetByIdAsync(cmd.SubmissionId, ct);
            Guid assignedTeId = Guid.Empty;
            if (submissionForSeries is not null)
            {
                var author = await _userRepo.GetByIdAsync(submissionForSeries.SubmitterId, ct);
                assignedTeId = author?.ManagingTantouId ?? Guid.Empty;
            }
            return new ApproveSubmissionResult(
                cmd.SubmissionId,
                existingSeries.Id,
                assignedTeId,
                "EB_Approved",
                existingSeries.Status.ToString(),
                existingSeries.CreatedAt,
                VotesClearedCount: 0);
        }

        // ── Load & validate submission ────────────────────────────────────────
        var submission = await _submissionRepo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        // State guard: Admin can force-approve from Pending_EB_Review or Conflict_Escalated only.
        // All other terminal states should be rejected to prevent data corruption.
        if (submission.Status != Domain.Entities.SubmissionStatus.Pending_EB_Review &&
            submission.Status != Domain.Entities.SubmissionStatus.Conflict_Escalated)
            throw new InvalidOperationException(
                $"Admin chỉ có thể force-approve khi bản thảo đang ở Pending_EB_Review hoặc Conflict_Escalated. " +
                $"Trạng thái hiện tại: {submission.Status}");

        // ── Load active editors outside transaction ───────────────────────────
        var allTE = await _userRepo.GetByRoleAsync(UserRole.TantouEditor, ct);
        var activeTE = allTE.Where(u => u.AccountStatus == AccountStatus.Active).ToList();
        if (!activeTE.Any())
            throw new InvalidOperationException("Không có Tantou Editor nào đang hoạt động để gán.");

        var activeTeIds = activeTE.Select(te => te.Id).ToList();

        // ── Domain state transition BEFORE opening the transaction ────────────
        // ApproveByEIC is used for both Conflict_Escalated and Pending_EB_Review override,
        // because both need the same transition to EB_Approved.
        if (submission.Status == Domain.Entities.SubmissionStatus.Conflict_Escalated)
            submission.ApproveByEIC(cmd.ReviewerId);
        else
            submission.Approve(cmd.ReviewerId);

        // ── Atomic transaction via ExecutionStrategy ──────────────────────────
        var strategy = db.Database.CreateExecutionStrategy();

        ApproveSubmissionResult? result = null;
        int clearedVotesCount = 0;
        Guid selectedTeId = Guid.Empty;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // ── STATE CLEANUP: purge dangling in-progress votes ───────────────
            // Count them first so we can report back how many were cleaned.
            var danglingVotes = (await _submissionRepo.GetVotesByRoundAsync(
                cmd.SubmissionId, submission.CurrentRound, ct)).ToList();
            clearedVotesCount = danglingVotes.Count;

            if (clearedVotesCount > 0)
            {
                // Remove in-progress votes for the current round so they don't
                // contradict the force-approved terminal state.
                await _submissionRepo.DeleteVotesByRoundAsync(
                    cmd.SubmissionId, submission.CurrentRound, ct);
            }

            // Query loads inside the transaction to get freshest data
            var loads = await _userRepo.GetTantouEditorsLoadAsync(activeTeIds, ct);
            var selectedTe = activeTE
                .Select(te => new { Editor = te, Load = loads.GetValueOrDefault(te.Id, 0) })
                .OrderBy(x => x.Load)
                .ThenBy(x => x.Editor.CreatedAt)
                .Select(x => x.Editor)
                .First();

            // Create MangaSeries linked to this submission and Mangaka
            var series = MangaSeries.Create(
                authorId:      submission.SubmitterId,
                submissionId:  submission.Id,
                title:         submission.Title,
                description:   submission.Description,
                genre:         submission.Genre,
                coverImageUrl: submission.CoverImageUrl);

            // Assign Tantou Editor to Mangaka
            var mangaka = await _userRepo.GetByIdAsync(submission.SubmitterId, ct)
                ?? throw new InvalidOperationException($"Mangaka {submission.SubmitterId} not found.");
            mangaka.ManagingTantouId = selectedTe.Id;

            // Persist all changes in the same transaction
            await _seriesRepo.AddAsync(series, ct);
            await _userRepo.UpdateAsync(mangaka, ct);
            await _submissionRepo.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            result = new ApproveSubmissionResult(
                submission.Id,
                series.Id,
                selectedTe.Id,
                submission.Status.ToString(),
                series.Status.ToString(),
                submission.ReviewedAt!.Value,
                VotesClearedCount: clearedVotesCount);

            selectedTeId = selectedTe.Id;
        });

        // Send notification AFTER successful commit
        await _notificationService.NotifySubmissionApprovedAsync(
            receiverId:   submission.SubmitterId,
            submissionId: submission.Id,
            seriesId:     result!.SeriesId,
            seriesTitle:  submission.Title,
            ct:           ct);

        // [Mốc 3] Notify Tantou Editor được gán phụ trách tác phẩm mới
        var mangaka = await _userRepo.GetByIdAsync(submission.SubmitterId, ct);
        await _notificationService.NotifyTantouEditorAssignedAsync(
            tantouEditorId: selectedTeId,
            submissionId:   submission.Id,
            seriesTitle:    submission.Title,
            authorName:     mangaka?.FullName ?? "Không rõ",
            ct:             ct);

        return result!;
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class ApproveSubmissionValidator : AbstractValidator<ApproveSubmissionCommand>
{
    public ApproveSubmissionValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
    }
}
