using MediatR;
using MangaERP.QA.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;

namespace MangaERP.QA.Application.Queries.GetChapterQaSummary;

public record GetChapterQaSummaryQuery(Guid ChapterId, Guid RequesterId) : IRequest<ChapterQaSummaryDto?>;

public record ChapterQaSummaryDto(
    Guid ChapterId,
    int TotalPins,
    int OpenPins,
    int InFixingPins,
    int FixedPins,
    int ResolvedPins,
    bool CanApprove,
    string? SessionStatus
);

public class GetChapterQaSummaryHandler : IRequestHandler<GetChapterQaSummaryQuery, ChapterQaSummaryDto?>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IQASessionRepository _qaSessionRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetChapterQaSummaryHandler(
        IBugPinRepository bugPinRepo,
        IQASessionRepository qaSessionRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo)
    {
        _bugPinRepo = bugPinRepo;
        _qaSessionRepo = qaSessionRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<ChapterQaSummaryDto?> Handle(GetChapterQaSummaryQuery request, CancellationToken cancellationToken)
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

        var pins = (await _bugPinRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken)).ToList();

        var totalPins = pins.Count;
        var openPins = pins.Count(p => p.Status == "Open");
        var inFixingPins = pins.Count(p => p.Status == "InFixing");
        var fixedPins = pins.Count(p => p.Status == "Fixed");
        var resolvedPins = pins.Count(p => p.Status == "Resolved");

        // Can approve only when all pins are resolved (or there are no pins at all)
        var canApprove = totalPins == 0 || pins.All(p => p.Status == "Resolved");

        return new ChapterQaSummaryDto(
            request.ChapterId,
            totalPins,
            openPins,
            inFixingPins,
            fixedPins,
            resolvedPins,
            canApprove,
            session?.Status
        );
    }
}
