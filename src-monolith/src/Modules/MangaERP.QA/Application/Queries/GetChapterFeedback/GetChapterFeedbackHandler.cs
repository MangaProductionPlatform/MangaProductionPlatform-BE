using MediatR;
using MangaERP.QA.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;

namespace MangaERP.QA.Application.Queries.GetChapterFeedback;

public record GetChapterFeedbackQuery(Guid ChapterId, Guid RequesterId) : IRequest<ChapterFeedbackDto>;

public record ChapterFeedbackDto(
    Guid ChapterId,
    IEnumerable<FeedbackBatchDto> Batches
);

public record FeedbackBatchDto(
    Guid BatchToken,
    DateTime SentAt,
    IEnumerable<FeedbackPinDto> Pins
);

public record FeedbackPinDto(
    Guid Id,
    Guid PageTaskId,
    decimal CoordinateX,
    decimal CoordinateY,
    string NoteMessage,
    string? IssueType,
    string Status,
    DateTime CreatedAt
);

public class GetChapterFeedbackHandler : IRequestHandler<GetChapterFeedbackQuery, ChapterFeedbackDto>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IQASessionRepository _qaSessionRepo;

    public GetChapterFeedbackHandler(
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

    public async Task<ChapterFeedbackDto> Handle(GetChapterFeedbackQuery request, CancellationToken cancellationToken)
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

        var allPins = await _bugPinRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);

        var batches = allPins
            .GroupBy(p => p.BatchToken)
            .Select(g => new FeedbackBatchDto(
                g.Key,
                g.Max(p => p.CreatedAt),
                g.Select(p => new FeedbackPinDto(
                    p.Id,
                    p.PageTaskId,
                    p.CoordinateX,
                    p.CoordinateY,
                    p.NoteMessage,
                    p.IssueType,
                    p.Status,
                    p.CreatedAt
                )).ToList()
            ))
            .OrderByDescending(b => b.SentAt)
            .ToList();

        return new ChapterFeedbackDto(request.ChapterId, batches);
    }
}
