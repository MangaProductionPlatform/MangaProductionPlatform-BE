using FluentValidation;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Submission.Application.Commands.RequestRevision;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record FeedbackPinInput(
    string PageIdentifier,
    double CoordinateX,
    double CoordinateY,
    string Comment,
    FeedbackPinCategory Category);

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// [ADMIN FORCE-REVISION] Override: Pending_EB_Review | Conflict_Escalated → Requires_Revision.
/// Kèm Visual Feedback Pins.
/// ReviewerId được controller trích từ JWT claim.
///
/// STATE CLEANUP: Nếu submission đang trong luồng bỏ phiếu dở dang,
/// toàn bộ phiếu bầu trong CurrentRound sẽ bị XÓA trước khi chốt trạng thái.
/// Đồng thời CurrentRound sẽ KHÔNG được tăng (khác với EIC resolve-conflict)
/// vì Admin chưa hoàn thành vòng — Mangaka phải sửa và re-submit để vòng mới bắt đầu.
/// </summary>
public record RequestRevisionCommand(
    Guid SubmissionId,
    Guid ReviewerId,         // extracted from JWT by controller (Admin)
    string ActorRole,
    string FeedbackMessage,
    List<FeedbackPinInput> Pins  // Visual feedback pins trên canvas
) : IRequest<RequestRevisionResult>;

public record RequestRevisionResult(
    Guid SubmissionId,
    string NewStatus,
    string FeedbackMessage,
    int PinCount,
    DateTime ReviewedAt,
    int VotesClearedCount);   // informational: how many dangling votes were purged

// ── Handler ───────────────────────────────────────────────────────────────────

public class RequestRevisionHandler
    : IRequestHandler<RequestRevisionCommand, RequestRevisionResult>
{
    private readonly ISubmissionRepository _repo;
    private readonly IDbContextProvider _dbContextProvider;
    private readonly INotificationService _notificationService;

    public RequestRevisionHandler(
        ISubmissionRepository repo,
        IDbContextProvider dbContextProvider,
        INotificationService notificationService)
    {
        _repo = repo;
        _dbContextProvider = dbContextProvider;
        _notificationService = notificationService;
    }

public async Task<RequestRevisionResult> Handle(
        RequestRevisionCommand cmd, CancellationToken ct)
    {
        throw new NotSupportedException("RequestRevision is not a valid Editorial Board decision.");
        var db = (DbContext)_dbContextProvider.GetDbContext();
        var strategy = db.Database.CreateExecutionStrategy();

        Guid submitterId = Guid.Empty;
        int newPinCount = 0;
        RequestRevisionResult? result = null;

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // ── Load submission inside the transaction ────────────────────────
            var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
                ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

            // ── State validation: Admin can only force-revision active review states ──
            // Reject terminal states and Draft (nonsensical to request revision on Draft).
            if (submission.Status != SubmissionStatus.Pending_EB_Review &&
                submission.Status != SubmissionStatus.Conflict_Escalated)
                throw new InvalidOperationException(
                    $"Admin chỉ có thể force-revision khi bản thảo đang ở Pending_EB_Review hoặc Conflict_Escalated. " +
                    $"Trạng thái hiện tại: {submission.Status}");

            // ── STATE CLEANUP: purge dangling in-progress votes ───────────────
            var danglingVotes = (await _repo.GetVotesByRoundAsync(
                cmd.SubmissionId, submission.CurrentRound, ct)).ToList();
            int clearedCount = danglingVotes.Count;

            if (clearedCount > 0)
                await _repo.DeleteVotesByRoundAsync(cmd.SubmissionId, submission.CurrentRound, ct);

            // ── Archive old feedback pins ────────────────────────────────────
            var existingPins = await _repo.GetActivePinsBySubmissionIdAsync(cmd.SubmissionId, ct);
            foreach (var pin in existingPins)
                pin.Archive();

            // ── Create new feedback pins ─────────────────────────────────────
            var newPins = cmd.Pins.Select(p => SubmissionFeedbackPin.Create(
                submission.Id, p.PageIdentifier, p.CoordinateX, p.CoordinateY,
                p.Comment, p.Category, cmd.ReviewerId
            )).ToList();

            foreach (var pin in newPins)
                await _repo.AddPinAsync(pin, ct);

            // ── Domain state transition (guards ActorRole + Status) ──────────
            // Note: Admin is passed as "EditorialBoard" role string to reuse the
            // existing domain guard logic which checks ActorRole == "EditorialBoard".
            submission.RequestRevision(cmd.ActorRole, cmd.ReviewerId, cmd.FeedbackMessage);

            submitterId = submission.SubmitterId;
            newPinCount = newPins.Count;

            // ── Persist all changes atomically ───────────────────────────────
            await _repo.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            result = new RequestRevisionResult(
                submission.Id,
                submission.Status.ToString(),
                cmd.FeedbackMessage,
                newPins.Count,
                submission.ReviewedAt!.Value,
                VotesClearedCount: clearedCount);
        });

        // Send notification AFTER successful commit (outside transaction)
        await _notificationService.NotifySubmissionRevisionAsync(
            receiverId:   submitterId,
            submissionId: cmd.SubmissionId,
            message:      $"[Admin] {newPinCount} feedback pin(s) added. {cmd.FeedbackMessage}",
            pinCount:     newPinCount,
            targetUrl:    "/mangaka/submissions",
            ct:           ct);

        return result!;
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class RequestRevisionValidator : AbstractValidator<RequestRevisionCommand>
{
    public RequestRevisionValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.ReviewerId).NotEmpty();
        RuleFor(x => x.FeedbackMessage)
            .NotEmpty().WithMessage("Feedback message is required when requesting revision.")
            .MinimumLength(10).WithMessage("Feedback must be at least 10 characters.")
            .MaximumLength(2000).WithMessage("Feedback must not exceed 2000 characters.");
        RuleFor(x => x.Pins).NotNull().WithMessage("Pins collection is required.");
        RuleForEach(x => x.Pins).ChildRules(pin =>
        {
            pin.RuleFor(p => p.PageIdentifier).NotEmpty().WithMessage("Page identifier is required.");
            pin.RuleFor(p => p.CoordinateX).InclusiveBetween(0, 100).WithMessage("X coordinate must be 0-100.");
            pin.RuleFor(p => p.CoordinateY).InclusiveBetween(0, 100).WithMessage("Y coordinate must be 0-100.");
            pin.RuleFor(p => p.Comment).NotEmpty().MaximumLength(2000);
        });
    }
}
