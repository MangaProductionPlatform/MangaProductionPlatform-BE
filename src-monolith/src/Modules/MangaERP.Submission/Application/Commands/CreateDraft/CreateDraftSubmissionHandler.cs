using FluentValidation;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Commands.CreateDraft;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka tạo draft proposal mới. ManuscriptUrl có thể null — sẽ upload sau.
/// SubmitterId được controller trích từ JWT claim.
/// </summary>
public record CreateDraftSubmissionCommand(
    Guid SubmitterId,
    string Title,
    string? Description,
    string? Genre,
    string? CoverImageUrl,
    string? ManuscriptUrl
) : IRequest<CreateDraftResult>;

public record CreateDraftResult(Guid SubmissionId, string Title, string Status);

// ── Handler ───────────────────────────────────────────────────────────────────

public class CreateDraftSubmissionHandler
    : IRequestHandler<CreateDraftSubmissionCommand, CreateDraftResult>
{
    private readonly ISubmissionRepository _repo;

    public CreateDraftSubmissionHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<CreateDraftResult> Handle(
        CreateDraftSubmissionCommand cmd, CancellationToken ct)
    {
        var submission = SeriesSubmission.CreateDraft(
            submitterId:   cmd.SubmitterId,
            title:         cmd.Title,
            description:   cmd.Description,
            genre:         cmd.Genre,
            coverImageUrl: cmd.CoverImageUrl,
            manuscriptUrl: cmd.ManuscriptUrl);

        await _repo.AddAsync(submission, ct);
        await _repo.SaveChangesAsync(ct);

        return new CreateDraftResult(
            submission.Id,
            submission.Title,
            submission.Status.ToString());
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class CreateDraftSubmissionValidator
    : AbstractValidator<CreateDraftSubmissionCommand>
{
    public CreateDraftSubmissionValidator()
    {
        RuleFor(x => x.SubmitterId)
            .NotEmpty().WithMessage("SubmitterId is required.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x.Genre)
            .MaximumLength(100).WithMessage("Genre must not exceed 100 characters.")
            .When(x => x.Genre is not null);

        RuleFor(x => x.CoverImageUrl)
            .MaximumLength(2048).WithMessage("CoverImageUrl must not exceed 2048 characters.")
            .When(x => x.CoverImageUrl is not null);

        RuleFor(x => x.ManuscriptUrl)
            .MaximumLength(2048).WithMessage("ManuscriptUrl must not exceed 2048 characters.")
            .When(x => x.ManuscriptUrl is not null);
    }
}
