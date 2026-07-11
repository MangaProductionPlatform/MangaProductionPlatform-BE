using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.QA.Application.Ports;
using MangaERP.Series.Application.Ports;

namespace MangaERP.QA.Application.Queries;

public record GetBugPinByTaskQuery(Guid PageTaskId, Guid RequesterId) : IRequest<BugPinDto?>;

public class GetBugPinByTaskHandler : IRequestHandler<GetBugPinByTaskQuery, BugPinDto?>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetBugPinByTaskHandler(
        IBugPinRepository bugPinRepo,
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo)
    {
        _bugPinRepo = bugPinRepo;
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<BugPinDto?> Handle(GetBugPinByTaskQuery request, CancellationToken cancellationToken)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(request.PageTaskId, cancellationToken)
            ?? throw new KeyNotFoundException($"PageTask {request.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {pageTask.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        if (pageTask.AssignedAssistantId != request.RequesterId && series.AuthorId != request.RequesterId)
            throw new UnauthorizedAccessException("You do not have permission to view the QA pin for this task.");

        var pins = await _bugPinRepo.GetByPageTaskIdAsync(request.PageTaskId, cancellationToken);
        var activePin = pins
            .Where(p => p.Status is "Open" or "InFixing")
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefault();

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
