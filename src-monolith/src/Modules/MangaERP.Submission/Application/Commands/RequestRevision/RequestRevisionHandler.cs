using FluentValidation;
using MangaERP.Shared.Application.Ports;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Commands.RequestRevision;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record FeedbackPinInput(
    string PageIdentifier,
    double CoordinateX,
    double CoordinateY,
    string Comment,
    FeedbackPinCategory Category);

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Editorial Board yêu cầu Mangaka chỉnh sửa kèm Visual Feedback Pins.
/// Domain entity guard: chỉ chấp nhận khi trạng thái Pending_EB_Review.
/// ReviewerId được controller trích từ JWT claim.
/// </summary>
public record RequestRevisionCommand(
    Guid SubmissionId,
    Guid ReviewerId,         // extracted from JWT by controller (EditorialBoard member)
    string ActorRole,
    string FeedbackMessage,
    List<FeedbackPinInput> Pins  // Visual feedback pins trên canvas
) : IRequest<RequestRevisionResult>;

public record RequestRevisionResult(
    Guid SubmissionId,
    string NewStatus,
    string FeedbackMessage,
    int PinCount,
    DateTime ReviewedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public class RequestRevisionHandler
    : IRequestHandler<RequestRevisionCommand, RequestRevisionResult>
{
    private readonly ISubmissionRepository _repo;
    private readonly INotificationService _notificationService;

    public RequestRevisionHandler(ISubmissionRepository repo, INotificationService notificationService)
    {
        _repo = repo;
        _notificationService = notificationService;
    }

    public async Task<RequestRevisionResult> Handle(
        RequestRevisionCommand cmd, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(cmd.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {cmd.SubmissionId} not found.");

        // 1. Archive existing active pins from previous revision rounds
        var existingPins = await _repo.GetActivePinsBySubmissionIdAsync(cmd.SubmissionId, ct);
        foreach (var pin in existingPins)
            pin.Archive();

        // 2. Create new feedback pins
        var newPins = cmd.Pins.Select(p => SubmissionFeedbackPin.Create(
            submission.Id, p.PageIdentifier, p.CoordinateX, p.CoordinateY,
            p.Comment, p.Category, cmd.ReviewerId
        )).ToList();

        foreach (var pin in newPins)
            await _repo.AddPinAsync(pin, ct);

        // 3. Domain state transition (guards ActorRole + Status)
        submission.RequestRevision(cmd.ActorRole, cmd.ReviewerId, cmd.FeedbackMessage);

        // 4. Persist all changes atomically
        await _repo.SaveChangesAsync(ct);

        // 5. Send notification with deep-link AFTER successful commit
        string targetUrl = $"/mangaka/submissions";

        await _notificationService.NotifySubmissionRevisionAsync(
            receiverId: submission.SubmitterId,
            submissionId: submission.Id,
            message: $"Editorial Board pinned {newPins.Count} area(s) needing adjustments on your manuscript.",
            pinCount: newPins.Count,
            targetUrl: targetUrl,
            ct: ct);

        return new RequestRevisionResult(
            submission.Id,
            submission.Status.ToString(),
            cmd.FeedbackMessage,
            newPins.Count,
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
        RuleFor(x => x.Pins).NotNull().WithMessage("Pins collection is required.");
        RuleForEach(x => x.Pins).ChildRules(pin =>
        {
            pin.RuleFor(p => p.PageIdentifier).NotEmpty().WithMessage("Page identifier is required.");
            pin.RuleFor(p => p.CoordinateX).InclusiveBetween(0, 100).WithMessage("X coordinate must be 0-100.");
            pin.RuleFor(p => p.CoordinateY).InclusiveBetween(0, 100).WithMessage("Y coordinate must be 0-100.");
            pin.RuleFor(p => p.Comment).NotEmpty().MaximumLength(2000);
        });
    }
}
