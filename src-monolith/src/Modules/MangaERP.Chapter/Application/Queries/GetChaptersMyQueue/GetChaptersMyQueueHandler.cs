using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Queries.GetChaptersMyQueue;

public record GetChaptersMyQueueQuery(Guid EditorId) : IRequest<IEnumerable<ChapterQueueItemDto>>;

public record ChapterQueueItemDto(
    Guid ChapterId,
    Guid SeriesId,
    string SeriesTitle,
    string Title,
    decimal ChapterNumber,
    string Status,
    int TotalPages,
    int ApprovedPages,
    double ProgressPercent,
    DateTime CreatedAt);

public class GetChaptersMyQueueHandler : IRequestHandler<GetChaptersMyQueueQuery, IEnumerable<ChapterQueueItemDto>>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetChaptersMyQueueHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<IEnumerable<ChapterQueueItemDto>> Handle(GetChaptersMyQueueQuery query, CancellationToken ct)
    {
        var chapters = await _chapterRepo.GetByEditorIdAsync(query.EditorId, ct);
        var result = new List<ChapterQueueItemDto>();

        foreach (var chapter in chapters)
        {
            var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct);
            var seriesTitle = series?.Title ?? "Unknown Series";

            var approved = await _pageTaskRepo.CountApprovedPagesAsync(chapter.Id, ct);
            var progress = chapter.TotalPages > 0
                ? Math.Round(approved * 100.0 / chapter.TotalPages, 1)
                : 0;

            result.Add(new ChapterQueueItemDto(
                chapter.Id,
                chapter.SeriesId,
                seriesTitle,
                chapter.Title,
                chapter.ChapterNumber,
                chapter.Status.ToString(),
                chapter.TotalPages,
                approved,
                progress,
                chapter.CreatedAt));
        }

        return result.OrderByDescending(c => c.CreatedAt);
    }
}
