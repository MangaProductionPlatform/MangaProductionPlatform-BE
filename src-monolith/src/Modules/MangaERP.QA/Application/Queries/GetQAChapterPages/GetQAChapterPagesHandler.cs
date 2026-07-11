using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Queries.GetQAChapterPages;

/// <summary>
/// Query to load pages (with preview images) for a chapter during QA review.
/// Unlike the Chapter module's GetChapterPages, this endpoint authorizes based on
/// whether the requesting editor has an active QA session — not AssignedEditorId.
/// </summary>
public record GetQAChapterPagesQuery(Guid ChapterId, Guid RequesterId) : IRequest<IEnumerable<QAChapterPageDto>>;

public record QAChapterPageDto(
    Guid PageTaskId,
    int PageNumber,
    string? Description,
    string TaskStatus,
    string? RegionMask,
    string TaskType,
    string BaseImageUrl,
    string? PreviewCompositeUrl,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public class GetQAChapterPagesHandler : IRequestHandler<GetQAChapterPagesQuery, IEnumerable<QAChapterPageDto>>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IQASessionRepository _qaSessionRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetQAChapterPagesHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        IQASessionRepository qaSessionRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _qaSessionRepo = qaSessionRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<IEnumerable<QAChapterPageDto>> Handle(GetQAChapterPagesQuery query, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(query.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {query.ChapterId} not found.");

        // Authorization: allow if editor is assigned to chapter, mangaka of series, OR has an active QA session
        var isAssignedEditor = chapter.AssignedEditorId == query.RequesterId;
        
        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");
        var isAuthor = series.AuthorId == query.RequesterId;

        if (!isAssignedEditor && !isAuthor)
        {
            var session = await _qaSessionRepo.GetByChapterIdAsync(query.ChapterId, ct);
            var hasActiveSession = session is not null
                && session.EditorId == query.RequesterId
                && session.Status == "InProgress";

            if (!hasActiveSession)
            {
                throw new UnauthorizedAccessException(
                    "You are not authorized to view this chapter's pages. " +
                    "You must be the assigned editor, the mangaka, or have an active QA session.");
            }
        }

        var pages = await _pageTaskRepo.GetByChapterIdAsync(chapter.Id, ct);

        return pages.Select(page => new QAChapterPageDto(
            page.Id,
            page.PageNumber,
            page.Description,
            page.TaskStatus.ToString(),
            page.RegionMask,
            page.TaskType.ToString(),
            page.BaseImageUrl,
            page.PreviewPage?.CompositeFileUrl,
            page.CreatedAt,
            page.UpdatedAt
        )).OrderBy(p => p.PageNumber).ToList();
    }
}
