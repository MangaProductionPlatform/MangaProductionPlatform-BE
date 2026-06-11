using FluentValidation;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.ReSubmitProposal;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka nộp lại sau khi đã chỉnh sửa theo yêu cầu: RevisionRequired → Pending.
/// Khác với SubmitProposalCommand (Draft → Pending).
/// </summary>
public record ReSubmitProposalCommand(
    Guid SubmissionId,
    Guid SubmitterId         // extracted from JWT by controller
) : IRequest<ReSubmitProposalResult>;

public record ReSubmitProposalResult(Guid SubmissionId, string NewStatus);

// ── Handler ───────────────────────────────────────────────────────────────────

public class ReSubmitProposalHandler
    : IRequestHandler<ReSubmitProposalCommand, ReSubmitProposalResult>
{
    private readonly ISubmissionRepository _repo;

    public ReSubmitProposalHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<ReSubmitProposalResult> Handle(
        ReSubmitProposalCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        if (submission.SubmitterId != cmd.SubmitterId)
            throw new UnauthorizedAccessException("You are not the owner of this submission.");

        submission.ReSubmit();   // Domain: RevisionRequired → Pending; clears FeedbackMessage

        await _repo.SaveChangesAsync(ct);

        return new ReSubmitProposalResult(submission.Id, submission.Status.ToString());
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class ReSubmitProposalValidator : AbstractValidator<ReSubmitProposalCommand>
{
    public ReSubmitProposalValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.SubmitterId).NotEmpty();
    }
}
