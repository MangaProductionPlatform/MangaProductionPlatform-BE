using FluentValidation;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.RejectSubmission;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Từ chối submission — chỉ dùng bởi Editorial Board.
/// Domain entity đã guard: chỉ chấp nhận khi trạng thái Pending_EB_Review.
/// ReviewerId được controller trích từ JWT claim.
/// </summary>
public record RejectSubmissionCommand(
    Guid SubmissionId,
    Guid ReviewerId,         // extracted from JWT by controller (Editor or Board member)
    string ActorRole,
    string FeedbackMessage
) : IRequest<RejectSubmissionResult>;

public record RejectSubmissionResult(
    Guid SubmissionId,
    string NewStatus,
    string FeedbackMessage,
    DateTime ReviewedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public class RejectSubmissionHandler
    : IRequestHandler<RejectSubmissionCommand, RejectSubmissionResult>
{
    private readonly ISubmissionRepository _repo;
    private readonly INotificationService _notificationService;

    public RejectSubmissionHandler(ISubmissionRepository repo, INotificationService notificationService)
    {
        _repo = repo;
        _notificationService = notificationService;
    }

    public async Task<RejectSubmissionResult> Handle(
        RejectSubmissionCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        // Domain guards using ActorRole
        submission.Reject(cmd.ActorRole, cmd.ReviewerId, cmd.FeedbackMessage);

        await _repo.SaveChangesAsync(ct);

        // Send notification AFTER successful save
        await _notificationService.NotifySubmissionRejectedAsync(
            receiverId:      submission.SubmitterId,
            submissionId:    submission.Id,
            feedbackMessage: cmd.FeedbackMessage,
            ct:              ct);

        return new RejectSubmissionResult(
            submission.Id,
            submission.Status.ToString(),
            cmd.FeedbackMessage,
            submission.ReviewedAt!.Value);
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
