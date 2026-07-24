using FluentValidation;
using MangaERP.Identity.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.ReSubmitProposal;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka nộp lại sau khi đã chỉnh sửa theo yêu cầu: RevisionRequired → Pending_EB_Review.
/// Tự động gán 2 Reviewers thuộc Editorial Board cho vòng mới và lưu persistent notification.
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
    private readonly IUserRepository _userRepo;
    private readonly INotificationService _notificationService;

    public ReSubmitProposalHandler(
        ISubmissionRepository repo,
        IUserRepository userRepo,
        INotificationService notificationService)
    {
        _repo = repo;
        _userRepo = userRepo;
        _notificationService = notificationService;
    }

    public async Task<ReSubmitProposalResult> Handle(
        ReSubmitProposalCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        if (submission.SubmitterId != cmd.SubmitterId)
            throw new UnauthorizedAccessException("You are not the owner of this submission.");

        var author = await _userRepo.GetByIdAsync(cmd.SubmitterId, ct)
            ?? throw new KeyNotFoundException("Submission owner not found.");

        submission.ReSubmit();   // Domain: Requires_Revision / Rejected → Pending_EB_Review; clears FeedbackMessage
        await _repo.AssignEditorialReviewersAsync(submission.Id, submission.CurrentRound, author.FullName, ct);
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
