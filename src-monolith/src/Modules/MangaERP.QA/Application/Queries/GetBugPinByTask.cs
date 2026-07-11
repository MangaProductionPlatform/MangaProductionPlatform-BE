using MediatR;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Queries;

public record GetBugPinByTaskQuery(Guid PageTaskId, Guid RequesterId) : IRequest<BugPinDto?>;

public class GetBugPinByTaskHandler : IRequestHandler<GetBugPinByTaskQuery, BugPinDto?>
{
    private readonly IBugPinRepository _bugPinRepo;

    public GetBugPinByTaskHandler(IBugPinRepository bugPinRepo)
    {
        _bugPinRepo = bugPinRepo;
    }

    public async Task<BugPinDto?> Handle(GetBugPinByTaskQuery request, CancellationToken cancellationToken)
    {
        // Get all pins for this task.
        var pins = await _bugPinRepo.GetByPageTaskIdAsync(request.PageTaskId, cancellationToken);
        var activePin = pins.OrderByDescending(p => p.CreatedAt).FirstOrDefault(p => p.Status != "Resolved" && p.Status != "Fixed");
        
        if (activePin == null) activePin = pins.OrderByDescending(p => p.CreatedAt).FirstOrDefault();

        if (activePin == null) return null;

        return new BugPinDto(
            activePin.Id,
            activePin.ChapterId,
            activePin.PageTaskId,
            activePin.EditorId,
            activePin.CoordinateX,
            activePin.CoordinateY,
            activePin.NoteMessage,
            activePin.IssueType,
            activePin.Severity,
            activePin.Category,
            activePin.BatchToken,
            activePin.Status,
            activePin.ResolvedAt,
            activePin.CreatedAt
        );
    }
}
