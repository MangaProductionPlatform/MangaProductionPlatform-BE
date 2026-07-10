using MediatR;
using MangaERP.QA.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;

namespace MangaERP.QA.Application.Queries;

public record GetBugPinsQuery(Guid ChapterId, Guid RequesterId) : IRequest<IEnumerable<BugPinDto>>;

public record BugPinDto(
    Guid Id,
    Guid ChapterId,
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

public class GetBugPinsHandler : IRequestHandler<GetBugPinsQuery, IEnumerable<BugPinDto>>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IQASessionRepository _qaSessionRepo;

    public GetBugPinsHandler(
        IBugPinRepository bugPinRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IQASessionRepository qaSessionRepo)
    {
        _bugPinRepo = bugPinRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _qaSessionRepo = qaSessionRepo;
    }

    public async Task<IEnumerable<BugPinDto>> Handle(GetBugPinsQuery request, CancellationToken cancellationToken)
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
            p.Severity,
            p.Category,
            p.BatchToken,
            p.Status,
            p.ResolvedAt,
            p.CreatedAt
        )).ToList();
    }
}
