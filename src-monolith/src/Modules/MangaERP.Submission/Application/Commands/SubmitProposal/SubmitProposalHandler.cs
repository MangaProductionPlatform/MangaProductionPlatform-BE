using FluentValidation;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.SubmitProposal;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka nộp draft lần đầu: Draft → Pending.
/// Yêu cầu ManuscriptUrl phải đã có trong entity trước khi gọi.
/// </summary>
public record SubmitProposalCommand(
    Guid SubmissionId,
    Guid SubmitterId         // extracted from JWT by controller
) : IRequest<SubmitProposalResult>;

public record SubmitProposalResult(Guid SubmissionId, string NewStatus);

// ── Handler ───────────────────────────────────────────────────────────────────

public class SubmitProposalHandler
    : IRequestHandler<SubmitProposalCommand, SubmitProposalResult>
{
    private readonly ISubmissionRepository _repo;

    public SubmitProposalHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<SubmitProposalResult> Handle(
        SubmitProposalCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        // Authorization guard: chỉ chủ sở hữu mới được submit
        if (submission.SubmitterId != cmd.SubmitterId)
            throw new UnauthorizedAccessException("You are not the owner of this submission.");

        submission.SubmitDraft();   // Domain: Draft → Pending; guards ManuscriptUrl

        await _repo.SaveChangesAsync(ct);

        return new SubmitProposalResult(submission.Id, submission.Status.ToString());
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class SubmitProposalValidator : AbstractValidator<SubmitProposalCommand>
{
    public SubmitProposalValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.SubmitterId).NotEmpty();
    }
}
