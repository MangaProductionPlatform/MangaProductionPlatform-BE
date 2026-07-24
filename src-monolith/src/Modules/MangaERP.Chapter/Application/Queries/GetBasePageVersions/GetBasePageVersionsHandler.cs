using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Queries.GetBasePageVersions;

public record GetBasePageVersionsQuery(Guid RequesterId, Guid PageTaskId) : IRequest<IEnumerable<BasePageVersionDto>>;

public record BasePageVersionDto(
    Guid Id,
    int VersionNumber,
    string BaseImageUrl,
    Guid UpdatedByUserId,
    DateTime CreatedAt
);

public class GetBasePageVersionsHandler : IRequestHandler<GetBasePageVersionsQuery, IEnumerable<BasePageVersionDto>>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetBasePageVersionsHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<IEnumerable<BasePageVersionDto>> Handle(GetBasePageVersionsQuery query, CancellationToken ct)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(query.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Page task {query.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {pageTask.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // Mangaka who owns the series, assigned Editor, or assigned Assistant can view base page versions
        if (series.AuthorId != query.RequesterId &&
            chapter.AssignedEditorId != query.RequesterId &&
            pageTask.AssignedAssistantId != query.RequesterId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view base page versions for this task.");
        }

        var versions = pageTask.BasePageVersions ?? new List<Domain.Entities.BasePageVersion>();

        return versions
            .OrderBy(v => v.VersionNumber)
            .Select(v => new BasePageVersionDto(
                v.Id,
                v.VersionNumber,
                v.BaseImageUrl,
                v.UpdatedByUserId,
                v.CreatedAt
            ));
    }
}
