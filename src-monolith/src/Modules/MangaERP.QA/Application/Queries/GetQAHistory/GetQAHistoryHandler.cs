using MediatR;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Queries.GetQAHistory;

public record GetQAHistoryQuery(Guid ChapterId) : IRequest<QAHistoryDto>;

public record QAHistoryDto(
    Guid ChapterId,
    IEnumerable<QASessionHistoryDto> Sessions,
    IEnumerable<BugPinHistoryDto> Pins
);

public record QASessionHistoryDto(
    Guid Id,
    Guid EditorId,
    string Status,
    bool IsApproved,
    DateTime? ApprovedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt
);

public record BugPinHistoryDto(
    Guid Id,
    Guid PageTaskId,
    Guid EditorId,
    decimal CoordinateX,
    decimal CoordinateY,
    string NoteMessage,
    string? IssueType,
    string Severity,
    string? Category,
    Guid BatchToken,
    string Status,
    DateTime? ResolvedAt,
    DateTime CreatedAt
);

public class GetQAHistoryHandler : IRequestHandler<GetQAHistoryQuery, QAHistoryDto>
{
    private readonly IQASessionRepository _qaSessionRepo;
    private readonly IBugPinRepository _bugPinRepo;

    public GetQAHistoryHandler(IQASessionRepository qaSessionRepo, IBugPinRepository bugPinRepo)
    {
        _qaSessionRepo = qaSessionRepo;
        _bugPinRepo = bugPinRepo;
    }

    public async Task<QAHistoryDto> Handle(GetQAHistoryQuery request, CancellationToken cancellationToken)
    {
        var sessions = await _qaSessionRepo.GetAllByChapterIdAsync(request.ChapterId, cancellationToken);
        var pins = await _bugPinRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);

        var sessionDtos = sessions.OrderBy(s => s.CreatedAt).Select(s => new QASessionHistoryDto(
            s.Id,
            s.EditorId,
            s.Status,
            s.IsApproved,
            s.ApprovedAt,
            s.CompletedAt,
            s.CreatedAt
        )).ToList();

        var pinDtos = pins.OrderBy(p => p.CreatedAt).Select(p => new BugPinHistoryDto(
            p.Id,
            p.PageTaskId,
            p.EditorId,
            p.CoordinateX,
            p.CoordinateY,
            p.NoteMessage,
            p.IssueType,
            p.Severity,
            p.Category,
            p.BatchToken,
            p.Status,
            p.ResolvedAt,
            p.CreatedAt
        )).ToList();

        return new QAHistoryDto(request.ChapterId, sessionDtos, pinDtos);
    }
}
