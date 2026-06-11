using FluentValidation;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.StartReview;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Tantou Editor nhận xét submission: Pending → UnderReview.
/// EditorId được controller trích từ JWT claim.
/// </summary>
public record StartReviewCommand(
    Guid SubmissionId,
    Guid EditorId            // extracted from JWT by controller
) : IRequest<StartReviewResult>;

public record StartReviewResult(Guid SubmissionId, string NewStatus, Guid AssignedEditorId);

// ── Handler ───────────────────────────────────────────────────────────────────

public class StartReviewHandler
    : IRequestHandler<StartReviewCommand, StartReviewResult>
{
    private readonly ISubmissionRepository _repo;

    public StartReviewHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<StartReviewResult> Handle(
        StartReviewCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        submission.StartReview(cmd.EditorId);   // Domain: Pending → UnderReview

        await _repo.SaveChangesAsync(ct);

        return new StartReviewResult(
            submission.Id,
            submission.Status.ToString(),
            cmd.EditorId);
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class StartReviewValidator : AbstractValidator<StartReviewCommand>
{
    public StartReviewValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
    }
}
