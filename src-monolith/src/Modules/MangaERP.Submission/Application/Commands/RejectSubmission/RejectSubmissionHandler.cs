using FluentValidation;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.RejectSubmission;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Từ chối submission — dùng bởi cả Tantou Editor VÀ Editorial Board.
/// Controller phân biệt quyền: TantouEditor chỉ reject Pending/UnderReview;
/// EditorialBoard chỉ reject RecommendedToBoard.
/// Domain entity đã guard các trường hợp không hợp lệ.
/// ReviewerId được controller trích từ JWT claim.
/// </summary>
public record RejectSubmissionCommand(
    Guid SubmissionId,
    Guid ReviewerId,         // extracted from JWT by controller (Editor or Board member)
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

    public RejectSubmissionHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<RejectSubmissionResult> Handle(
        RejectSubmissionCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        // Domain guards: không reject Draft hoặc Approved
        submission.Reject(cmd.ReviewerId, cmd.FeedbackMessage);

        await _repo.SaveChangesAsync(ct);

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
