using FluentValidation;
using MangaERP.Submission.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Commands.RecommendToBoard;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Tantou Editor recommend submission lên Editorial Board:
/// Pending_TE_Review → Pending_EB_Review.
/// EditorId được controller trích từ JWT claim.
/// </summary>
public record RecommendToBoardCommand(
    Guid SubmissionId,
    Guid EditorId,           // extracted from JWT by controller
    string RecommendationMessage
) : IRequest<RecommendToBoardResult>;

public record RecommendToBoardResult(
    Guid SubmissionId,
    string NewStatus,
    string RecommendationMessage);

// ── Handler ───────────────────────────────────────────────────────────────────

public class RecommendToBoardHandler
    : IRequestHandler<RecommendToBoardCommand, RecommendToBoardResult>
{
    private readonly ISubmissionRepository _repo;

    public RecommendToBoardHandler(ISubmissionRepository repo)
        => _repo = repo;

    public async Task<RecommendToBoardResult> Handle(
        RecommendToBoardCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        // Domain: Pending_TE_Review → Pending_EB_Review
        submission.RecommendToBoard(cmd.EditorId, cmd.RecommendationMessage);

        await _repo.SaveChangesAsync(ct);

        return new RecommendToBoardResult(
            submission.Id,
            submission.Status.ToString(),
            cmd.RecommendationMessage);
    }
}

// ── Validator ─────────────────────────────────────────────────────────────────

public class RecommendToBoardValidator : AbstractValidator<RecommendToBoardCommand>
{
    public RecommendToBoardValidator()
    {
        RuleFor(x => x.SubmissionId).NotEmpty();
        RuleFor(x => x.EditorId).NotEmpty();
        RuleFor(x => x.RecommendationMessage)
            .NotEmpty().WithMessage("Recommendation message is required.")
            .MinimumLength(10).WithMessage("Recommendation message must be at least 10 characters.")
            .MaximumLength(2000).WithMessage("Recommendation message must not exceed 2000 characters.");
    }
}
