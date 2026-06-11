using FluentValidation;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.UpdateDraftMetadata;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka cập nhật metadata của draft (Title, Description, Genre, CoverImageUrl).
/// Chỉ hoạt động khi submission đang Draft hoặc RevisionRequired.
/// </summary>
public record UpdateDraftMetadataCommand(
    Guid SubmissionId,
    Guid SubmitterId,        // extracted from JWT by controller
    string Title,
    string? Description,
    string? Genre,
    string? CoverImageUrl
) : IRequest<UpdateDraftMetadataResult>;

public record UpdateDraftMetadataResult(Guid SubmissionId, string Title, string Status);

// ── Handler ───────────────────────────────────────────────────────────────────

public class UpdateDraftMetadataHandler
    : IRequestHandler<UpdateDraftMetadataCommand, UpdateDraftMetadataResult>
{
    private readonly ISubmissionRepository _repo;

    public UpdateDraftMetadataHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<UpdateDraftMetadataResult> Handle(
        UpdateDraftMetadataCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        if (submission.SubmitterId != cmd.SubmitterId)
            throw new UnauthorizedAccessException("You are not the owner of this submission.");

        submission.UpdateDraftMetadata(
            cmd.Title, cmd.Description, cmd.Genre, cmd.CoverImageUrl);

        await _repo.SaveChangesAsync(ct);

        return new UpdateDraftMetadataResult(
            submission.Id, submission.Title, submission.Status.ToString());
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class UpdateDraftMetadataValidator
    : AbstractValidator<UpdateDraftMetadataCommand>
{
    public UpdateDraftMetadataValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.SubmitterId).NotEmpty();
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200);
        RuleFor(x => x.Description)
            .MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.Genre)
            .MaximumLength(100).When(x => x.Genre is not null);
        RuleFor(x => x.CoverImageUrl)
            .MaximumLength(2048).When(x => x.CoverImageUrl is not null);
    }
}
