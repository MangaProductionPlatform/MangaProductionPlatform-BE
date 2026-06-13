using FluentValidation;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.RequestRevision;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Yêu cầu Mangaka chỉnh sửa — dùng bởi cả Tantou Editor VÀ Editorial Board.
/// Domain entity guard: không được request revision trên Draft, Approved, hoặc Rejected.
/// ReviewerId được controller trích từ JWT claim.
/// </summary>
public record RequestRevisionCommand(
    Guid SubmissionId,
    Guid ReviewerId,         // extracted from JWT by controller (Editor or Board member)
    string ActorRole,
    string FeedbackMessage
) : IRequest<RequestRevisionResult>;

public record RequestRevisionResult(
    Guid SubmissionId,
    string NewStatus,
    string FeedbackMessage,
    DateTime ReviewedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public class RequestRevisionHandler
    : IRequestHandler<RequestRevisionCommand, RequestRevisionResult>
{
    private readonly ISubmissionRepository _repo;

    public RequestRevisionHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<RequestRevisionResult> Handle(
        RequestRevisionCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        // Domain guards using ActorRole
        submission.RequestRevision(cmd.ActorRole, cmd.ReviewerId, cmd.FeedbackMessage);

        await _repo.SaveChangesAsync(ct);

        return new RequestRevisionResult(
            submission.Id,
            submission.Status.ToString(),
            cmd.FeedbackMessage,
            submission.ReviewedAt!.Value);
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
    }
}
