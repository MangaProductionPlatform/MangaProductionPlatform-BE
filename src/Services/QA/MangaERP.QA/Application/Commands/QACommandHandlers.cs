using MediatR;
using MangaERP.QA.Application.Ports;
using MangaERP.QA.Domain.Entities;

namespace MangaERP.QA.Application.Commands;

// ─── Create Bug Pin (MF3 Step 3) ────────────────────────────────────────────
public record CreateBugPinCommand(
    Guid ChapterId,
    Guid PageTaskId,
    Guid EditorId,
    decimal CoordinateX,
    decimal CoordinateY,
    string NoteMessage,
    IssueType? IssueType,
    Guid BatchToken) : IRequest<CreateBugPinResult>;

public record CreateBugPinResult(Guid BugPinId, Guid BatchToken);

public class CreateBugPinHandler : IRequestHandler<CreateBugPinCommand, CreateBugPinResult>
{
    private readonly IBugPinRepository _repository;

    public CreateBugPinHandler(IBugPinRepository repository) => _repository = repository;

    public async Task<CreateBugPinResult> Handle(CreateBugPinCommand request, CancellationToken cancellationToken)
    {
        if (request.CoordinateX is < 0 or > 100 || request.CoordinateY is < 0 or > 100)
            throw new ArgumentOutOfRangeException("Coordinates must be between 0.00 and 100.00.");

        var pin = new BugPin
        {
            ChapterId = request.ChapterId,
            PageTaskId = request.PageTaskId,
            EditorId = request.EditorId,
            CoordinateX = request.CoordinateX,
            CoordinateY = request.CoordinateY,
            NoteMessage = request.NoteMessage,
            IssueType = request.IssueType,
            BatchToken = request.BatchToken,
            Status = BugStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.AddAsync(pin, cancellationToken);
        return new CreateBugPinResult(pin.Id, pin.BatchToken);
    }
}

// ─── Approve Chapter (MF3 Step 5) ───────────────────────────────────────────
public record ApproveChapterQACommand(
    Guid ChapterId,
    Guid EditorId) : IRequest;

public class ApproveChapterQAHandler : IRequestHandler<ApproveChapterQACommand>
{
    private readonly IBugPinRepository _bugPinRepository;
    private readonly IQASessionRepository _sessionRepository;
    private readonly BuildingBlocks.Infrastructure.Messaging.IEventBus _eventBus;

    public ApproveChapterQAHandler(
        IBugPinRepository bugPinRepository,
        IQASessionRepository sessionRepository,
        BuildingBlocks.Infrastructure.Messaging.IEventBus eventBus)
    {
        _bugPinRepository = bugPinRepository;
        _sessionRepository = sessionRepository;
        _eventBus = eventBus;
    }

    public async Task Handle(ApproveChapterQACommand request, CancellationToken cancellationToken)
    {
        // Resolve all remaining open pins
        await _bugPinRepository.ResolveAllForChapterAsync(request.ChapterId, cancellationToken);

        // Update QA session
        var session = await _sessionRepository.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        if (session is not null)
        {
            session.IsApproved = true;
            session.ApprovedAt = DateTime.UtcNow;
            await _sessionRepository.UpdateAsync(session, cancellationToken);
        }

        // Publish ChapterApprovedEvent — consumed by Publishing service
        var evt = new BuildingBlocks.Contracts.IntegrationEvents.ChapterApprovedEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            request.ChapterId, Guid.Empty, request.EditorId);
        await _eventBus.PublishAsync(evt, cancellationToken);
    }
}

// ─── Get bug pins for chapter ────────────────────────────────────────────────
public record GetBugPinsQuery(Guid ChapterId) : IRequest<IEnumerable<BugPinDto>>;

public record BugPinDto(
    Guid Id, Guid PageTaskId, decimal CoordinateX, decimal CoordinateY,
    string NoteMessage, string? IssueType, string Status,
    Guid BatchToken, DateTime? ResolvedAt, DateTime CreatedAt);

public class GetBugPinsHandler : IRequestHandler<GetBugPinsQuery, IEnumerable<BugPinDto>>
{
    private readonly IBugPinRepository _repository;

    public GetBugPinsHandler(IBugPinRepository repository) => _repository = repository;

    public async Task<IEnumerable<BugPinDto>> Handle(GetBugPinsQuery request, CancellationToken cancellationToken)
    {
        var pins = await _repository.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        return pins.Select(p => new BugPinDto(
            p.Id, p.PageTaskId, p.CoordinateX, p.CoordinateY,
            p.NoteMessage, p.IssueType?.ToString(), p.Status.ToString(),
            p.BatchToken, p.ResolvedAt, p.CreatedAt));
    }
}
