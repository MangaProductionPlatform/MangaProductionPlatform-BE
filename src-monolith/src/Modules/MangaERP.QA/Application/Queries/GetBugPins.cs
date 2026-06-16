using MediatR;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Queries;

public record GetBugPinsQuery(Guid ChapterId) : IRequest<IEnumerable<BugPinDto>>;

public record BugPinDto(
    Guid Id,
    Guid ChapterId,
    Guid PageTaskId,
    Guid EditorId,
    decimal CoordinateX,
    decimal CoordinateY,
    string NoteMessage,
    string? IssueType,
    Guid BatchToken,
    string Status,
    DateTime? ResolvedAt,
    DateTime CreatedAt
);

public class GetBugPinsHandler : IRequestHandler<GetBugPinsQuery, IEnumerable<BugPinDto>>
{
    private readonly IBugPinRepository _bugPinRepo;

    public GetBugPinsHandler(IBugPinRepository bugPinRepo)
    {
        _bugPinRepo = bugPinRepo;
    }

    public async Task<IEnumerable<BugPinDto>> Handle(GetBugPinsQuery request, CancellationToken cancellationToken)
    {
        var pins = await _bugPinRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        return pins.Select(p => new BugPinDto(
            p.Id,
            p.ChapterId,
            p.PageTaskId,
            p.EditorId,
            p.CoordinateX,
            p.CoordinateY,
            p.NoteMessage,
            p.IssueType,
            p.BatchToken,
            p.Status,
            p.ResolvedAt,
            p.CreatedAt
        )).ToList();
    }
}
