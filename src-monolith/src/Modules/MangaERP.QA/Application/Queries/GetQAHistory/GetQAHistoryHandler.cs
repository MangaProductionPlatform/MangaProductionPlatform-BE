using MediatR;
using MangaERP.QA.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;

namespace MangaERP.QA.Application.Queries.GetQAHistory;

public record GetQAHistoryQuery(Guid ChapterId, Guid RequesterId) : IRequest<QAHistoryDto>;

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
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetQAHistoryHandler(
        IQASessionRepository qaSessionRepo,
        IBugPinRepository bugPinRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo)
    {
        _qaSessionRepo = qaSessionRepo;
        _bugPinRepo = bugPinRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<QAHistoryDto> Handle(GetQAHistoryQuery request, CancellationToken cancellationToken)
    {
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");
        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");
        var session = await _qaSessionRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);

        var isAssignedEditor = chapter.AssignedEditorId == request.RequesterId;
        var isAuthor = series.AuthorId == request.RequesterId;
        var hasActiveSession = session != null && session.EditorId == request.RequesterId && session.Status == "InProgress";

        if (!isAssignedEditor && !isAuthor && !hasActiveSession)
            throw new UnauthorizedAccessException("Bạn không có quyền truy cập thông tin QA của chương này.");

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
