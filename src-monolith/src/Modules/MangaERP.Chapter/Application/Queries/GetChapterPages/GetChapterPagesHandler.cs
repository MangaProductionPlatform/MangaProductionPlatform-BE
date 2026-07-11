using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Queries.GetChapterPages;

public record GetChapterPagesQuery(Guid RequesterId, Guid ChapterId) : IRequest<IEnumerable<ChapterPageDto>>;

public record ChapterPageDto(
    Guid PageTaskId,
    int PageNumber,
    Guid? AssignedAssistantId,
    string? Description,
    string TaskStatus,
    string? RegionMask,
    string TaskType,
    string BaseImageUrl,
    string? PreviewCompositeUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public class GetChapterPagesHandler : IRequestHandler<GetChapterPagesQuery, IEnumerable<ChapterPageDto>>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetChapterPagesHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<IEnumerable<ChapterPageDto>> Handle(GetChapterPagesQuery query, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(query.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {query.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        if (series.AuthorId != query.RequesterId && chapter.AssignedEditorId != query.RequesterId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this chapter's pages.");
        }

        var pages = await _pageTaskRepo.GetByChapterIdAsync(chapter.Id, ct);

        return pages.Select(page => new ChapterPageDto(
            page.Id,
            page.PageNumber,
            page.AssignedAssistantId,
            page.Description,
            page.TaskStatus.ToString(),
            page.RegionMask,
            page.TaskType.ToString(),
            page.BaseImageUrl,
            page.PreviewPage?.CompositeFileUrl,
            page.CreatedAt,
            page.UpdatedAt
        ));
    }
}