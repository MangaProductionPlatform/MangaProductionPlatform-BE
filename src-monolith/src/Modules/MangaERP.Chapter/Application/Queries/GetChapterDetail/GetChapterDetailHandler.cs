using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Queries.GetChapterDetail;

public record GetChapterDetailQuery(Guid RequesterId, Guid ChapterId) : IRequest<ChapterDetailDto>;

public record ChapterDetailDto(
    Guid ChapterId,
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    string Status,
    int TotalPages,
    int ApprovedPages,
    double ProgressPercent,
    IEnumerable<PageTaskDetailDto> Pages);

public record PageTaskDetailDto(
    Guid PageTaskId,
    int PageNumber,
    string TaskStatus,
    Guid? AssignedAssistantId,
    string? TaskDescription,
    string? PreviewCompositeUrl);

public class GetChapterDetailHandler : IRequestHandler<GetChapterDetailQuery, ChapterDetailDto>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetChapterDetailHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<ChapterDetailDto> Handle(GetChapterDetailQuery query, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetWithPagesAndLayersAsync(query.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {query.ChapterId} not found.");

        _ = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        var pages = await _pageTaskRepo.GetByChapterIdAsync(chapter.Id, ct);
        var pageDtos = pages.Select(page => new PageTaskDetailDto(
            page.Id,
            page.PageNumber,
            page.TaskStatus.ToString(),
            page.AssignedAssistantId,
            page.TaskDescription,
            page.PreviewPage?.CompositeFileUrl)).ToList();

        var approved = await _pageTaskRepo.CountApprovedPagesAsync(chapter.Id, ct);
        var progress = chapter.TotalPages > 0
            ? Math.Round(approved * 100.0 / chapter.TotalPages, 1)
            : 0;

        return new ChapterDetailDto(
            chapter.Id,
            chapter.SeriesId,
            chapter.Title,
            chapter.ChapterNumber,
            chapter.Status.ToString(),
            chapter.TotalPages,
            approved,
            progress,
            pageDtos);
    }
}
