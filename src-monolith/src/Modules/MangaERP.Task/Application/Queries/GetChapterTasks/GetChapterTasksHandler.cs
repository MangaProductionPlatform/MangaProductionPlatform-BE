using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Task.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetChapterTasks;

public record GetChapterTasksQuery(Guid MangakaId, Guid ChapterId) : IRequest<IEnumerable<ChapterTaskDto>>;

public record ChapterTaskDto(
    Guid PageTaskId,
    int PageNumber,
    string TaskStatus,
    Guid? AssignedAssistantId,
    string? CurrentLayerType,
    int? CurrentLayerVersion,
    string? RejectionNote,
    DateTime UpdatedAt,
    string? Description,
    string TaskType);

public class GetChapterTasksHandler : IRequestHandler<GetChapterTasksQuery, IEnumerable<ChapterTaskDto>>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IArtworkLayerRepository _layerRepo;

    public GetChapterTasksHandler(
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IArtworkLayerRepository layerRepo)
    {
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _layerRepo = layerRepo;
    }

    public async Task<IEnumerable<ChapterTaskDto>> Handle(GetChapterTasksQuery query, CancellationToken ct)
    {
        var chapter = await _chapterRepo.GetByIdAsync(query.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {query.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(query.MangakaId, series.AuthorId);

        var tasks = await _pageTaskRepo.GetByChapterIdAsync(query.ChapterId, ct);
        var result = new List<ChapterTaskDto>();

        foreach (var task in tasks)
        {
            var layer = await _layerRepo.GetCurrentByPageTaskIdAsync(task.Id, ct);
            result.Add(new ChapterTaskDto(
                task.Id,
                task.PageNumber,
                task.TaskStatus.ToString(),
                task.AssignedAssistantId,
                layer?.LayerType,
                layer?.Version,
                layer?.RejectionNote,
                task.UpdatedAt,
                task.Description,
                task.TaskType.ToString()));
        }

        return result;
    }
}
