using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.Chapter.Application.Queries;

// ─── DTOs ────────────────────────────────────────────────────────────────────
public record ChapterDto(
    Guid Id, Guid SeriesId, string Title, decimal ChapterNumber,
    int TotalPages, string Status, Guid? AssignedEditorId,
    DateTime? ScheduledPublishAt, DateTime? PublishedAt, DateTime CreatedAt);

public record PageTaskDto(
    Guid Id, int PageNumber, string Status,
    Guid? AssignedAssistantId, DateTime CreatedAt, DateTime UpdatedAt);

// ─── List chapters by series ─────────────────────────────────────────────────
public record GetChaptersBySeriesQuery(Guid SeriesId) : IRequest<IEnumerable<ChapterDto>>;

public class GetChaptersBySeriesHandler : IRequestHandler<GetChaptersBySeriesQuery, IEnumerable<ChapterDto>>
{
    private readonly IChapterRepository _repository;

    public GetChaptersBySeriesHandler(IChapterRepository repository) => _repository = repository;

    public async Task<IEnumerable<ChapterDto>> Handle(GetChaptersBySeriesQuery request, CancellationToken cancellationToken)
    {
        var chapters = await _repository.GetBySeriesIdAsync(request.SeriesId, cancellationToken);
        return chapters.Select(c => new ChapterDto(
            c.Id, c.SeriesId, c.Title, c.ChapterNumber, c.TotalPages,
            c.Status.ToString(), c.AssignedEditorId,
            c.ScheduledPublishAt, c.PublishedAt, c.CreatedAt));
    }
}

// ─── Get chapter detail with pages ───────────────────────────────────────────
public record GetChapterDetailQuery(Guid ChapterId) : IRequest<ChapterDetailDto?>;

public record ChapterDetailDto(ChapterDto Chapter, IEnumerable<PageTaskDto> Pages);

public class GetChapterDetailHandler : IRequestHandler<GetChapterDetailQuery, ChapterDetailDto?>
{
    private readonly IChapterRepository _chapterRepository;
    private readonly IPageTaskRepository _pageTaskRepository;

    public GetChapterDetailHandler(IChapterRepository chapterRepository, IPageTaskRepository pageTaskRepository)
    {
        _chapterRepository = chapterRepository;
        _pageTaskRepository = pageTaskRepository;
    }

    public async Task<ChapterDetailDto?> Handle(GetChapterDetailQuery request, CancellationToken cancellationToken)
    {
        var chapter = await _chapterRepository.GetByIdAsync(request.ChapterId, cancellationToken);
        if (chapter is null) return null;

        var pages = await _pageTaskRepository.GetByChapterIdAsync(request.ChapterId, cancellationToken);
        var chapterDto = new ChapterDto(
            chapter.Id, chapter.SeriesId, chapter.Title, chapter.ChapterNumber,
            chapter.TotalPages, chapter.Status.ToString(), chapter.AssignedEditorId,
            chapter.ScheduledPublishAt, chapter.PublishedAt, chapter.CreatedAt);

        var pageDtos = pages.Select(p => new PageTaskDto(
            p.Id, p.PageNumber, p.TaskStatus.ToString(),
            p.AssignedAssistantId, p.CreatedAt, p.UpdatedAt));

        return new ChapterDetailDto(chapterDto, pageDtos);
    }
}
