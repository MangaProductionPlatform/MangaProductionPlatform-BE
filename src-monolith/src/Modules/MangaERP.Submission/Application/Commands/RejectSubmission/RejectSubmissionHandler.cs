using FluentValidation;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Submission.Application.Commands.RejectSubmission;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// [ADMIN FORCE-REJECT] Override: Pending_EB_Review | Conflict_Escalated → EB_Rejected.
/// ReviewerId được controller trích từ JWT claim.
///
/// STATE CLEANUP: Nếu submission đang trong luồng bỏ phiếu dở dang
/// (có phiếu bầu ở CurrentRound), toàn bộ phiếu bầu trong vòng hiện tại
/// sẽ bị XÓA trước khi chốt trạng thái — tránh mâu thuẫn dữ liệu trong SubmissionVotes.
/// </summary>
public record RejectSubmissionCommand(
    Guid SubmissionId,
    Guid ReviewerId,         // extracted from JWT by controller (Admin)
    string ActorRole,
    string FeedbackMessage
) : IRequest<RejectSubmissionResult>;

public record RejectSubmissionResult(
    Guid SubmissionId,
    string NewStatus,
    string FeedbackMessage,
    DateTime ReviewedAt,
    int VotesClearedCount);   // informational: how many dangling votes were purged

// ── Handler ───────────────────────────────────────────────────────────────────

public class RejectSubmissionHandler
    : IRequestHandler<RejectSubmissionCommand, RejectSubmissionResult>
{
    private readonly ISubmissionRepository _repo;
    private readonly IDbContextProvider _dbContextProvider;
    private readonly INotificationService _notificationService;

    public RejectSubmissionHandler(
        ISubmissionRepository repo,
        IDbContextProvider dbContextProvider,
        INotificationService notificationService)
    {
        _repo = repo;
        _dbContextProvider = dbContextProvider;
        _notificationService = notificationService;
    }

    public async Task<RejectSubmissionResult> Handle(
        RejectSubmissionCommand cmd, CancellationToken ct)
    {
        var db = (DbContext)_dbContextProvider.GetDbContext();
        var strategy = db.Database.CreateExecutionStrategy();

        Guid submitterId = Guid.Empty;
        RejectSubmissionResult? result = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // ── Load submission inside the transaction ────────────────────────
            var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
                ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

            // ── State validation: Admin can only force-reject active review states ──
            // Terminal states (EB_Approved, EB_Rejected) must NOT be overridden.
            // Draft and Requires_Revision have no in-progress votes; guard them too
            // since a force-reject there signals a misuse of the admin API.
            if (submission.Status != SubmissionStatus.Pending_EB_Review &&
                submission.Status != SubmissionStatus.Conflict_Escalated)
                throw new InvalidOperationException(
                    $"Admin chỉ có thể force-reject khi bản thảo đang ở Pending_EB_Review hoặc Conflict_Escalated. " +
                    $"Trạng thái hiện tại: {submission.Status}");

            // ── STATE CLEANUP: purge dangling in-progress votes ───────────────
            var danglingVotes = (await _repo.GetVotesByRoundAsync(
                cmd.SubmissionId, submission.CurrentRound, ct)).ToList();
            int clearedCount = danglingVotes.Count;

            if (clearedCount > 0)
                await _repo.DeleteVotesByRoundAsync(cmd.SubmissionId, submission.CurrentRound, ct);

            // ── Domain state transition ──────────────────────────────────────
            submission.Reject(cmd.ActorRole, cmd.ReviewerId, cmd.FeedbackMessage);
            submitterId = submission.SubmitterId;

            await _repo.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            result = new RejectSubmissionResult(
                submission.Id,
                submission.Status.ToString(),
                cmd.FeedbackMessage,
                submission.ReviewedAt!.Value,
                VotesClearedCount: clearedCount);
        });

        // Send notification AFTER successful commit (outside transaction)
        await _notificationService.NotifySubmissionRejectedAsync(
            receiverId:      submitterId,
            submissionId:    cmd.SubmissionId,
            feedbackMessage: cmd.FeedbackMessage,
            ct:              ct);

        return result!;
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class RejectSubmissionValidator : AbstractValidator<RejectSubmissionCommand>
{
    public RejectSubmissionValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
        RuleFor(x => x.FeedbackMessage)
            .NotEmpty().WithMessage("Feedback message is required when rejecting.")
            .MinimumLength(10).WithMessage("Feedback must be at least 10 characters.")
            .MaximumLength(2000).WithMessage("Feedback must not exceed 2000 characters.");
    }
}
