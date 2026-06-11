using FluentValidation;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.UpdateManuscript;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka cập nhật ManuscriptUrl — dùng khi còn Draft hoặc sau khi nhận RevisionRequired.
/// Sau khi update manuscript trong trạng thái RevisionRequired,
/// Mangaka cần gọi ReSubmitProposalCommand để chính thức nộp lại.
/// </summary>
public record UpdateManuscriptCommand(
    Guid SubmissionId,
    Guid SubmitterId,        // extracted from JWT by controller
    string ManuscriptUrl
) : IRequest<UpdateManuscriptResult>;

public record UpdateManuscriptResult(Guid SubmissionId, string ManuscriptUrl, string Status);

// ── Handler ───────────────────────────────────────────────────────────────────

public class UpdateManuscriptHandler
    : IRequestHandler<UpdateManuscriptCommand, UpdateManuscriptResult>
{
    private readonly ISubmissionRepository _repo;

    public UpdateManuscriptHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<UpdateManuscriptResult> Handle(
        UpdateManuscriptCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        if (submission.SubmitterId != cmd.SubmitterId)
            throw new UnauthorizedAccessException("You are not the owner of this submission.");

        submission.UpdateManuscript(cmd.ManuscriptUrl);

        await _repo.SaveChangesAsync(ct);

        return new UpdateManuscriptResult(
            submission.Id,
            cmd.ManuscriptUrl,
            submission.Status.ToString());
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class UpdateManuscriptValidator : AbstractValidator<UpdateManuscriptCommand>
{
    public UpdateManuscriptValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.SubmitterId).NotEmpty();
        RuleFor(x => x.ManuscriptUrl)
            .NotEmpty().WithMessage("ManuscriptUrl is required.")
            .MaximumLength(2048).WithMessage("ManuscriptUrl must not exceed 2048 characters.")
            .Must(url => Uri.TryCreate(url, UriKind.Absolute, out _))
                .WithMessage("ManuscriptUrl must be a valid URL.");
    }
}
