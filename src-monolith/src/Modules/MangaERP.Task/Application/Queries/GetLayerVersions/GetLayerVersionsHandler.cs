using MangaERP.Task.Application.Ports;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetLayerVersions;

public record GetLayerVersionsQuery(Guid PageTaskId, string LayerType, Guid RequesterId, string RequesterRole) : IRequest<IEnumerable<LayerVersionDto>>;

public record LayerVersionDto(
    Guid LayerId,
    int Version,
    string FileUrlOriginal,
    string FileUrlOptimized,
    bool IsCurrentVersion,
    DateTime CreatedAt);

public class GetLayerVersionsHandler : IRequestHandler<GetLayerVersionsQuery, IEnumerable<LayerVersionDto>>
{
    private readonly IArtworkLayerRepository _layerRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IUserRepository _userRepo;

    public GetLayerVersionsHandler(
        IArtworkLayerRepository layerRepo,
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IUserRepository userRepo)
    {
        _layerRepo = layerRepo;
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _userRepo = userRepo;
    }

    public async Task<IEnumerable<LayerVersionDto>> Handle(GetLayerVersionsQuery query, CancellationToken ct)
    {
        var task = await _pageTaskRepo.GetByIdAsync(query.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Task {query.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {task.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        var isAuthorized = false;
        if (query.RequesterRole.Equals("Mangaka", StringComparison.OrdinalIgnoreCase))
        {
            isAuthorized = (series.AuthorId == query.RequesterId);
        }
        else if (query.RequesterRole.Equals("TantouEditor", StringComparison.OrdinalIgnoreCase))
        {
            var mangaka = await _userRepo.GetByIdAsync(series.AuthorId, ct);
            isAuthorized = (mangaka != null && mangaka.ManagingTantouId == query.RequesterId);
        }
        else if (query.RequesterRole.Equals("Assistant", StringComparison.OrdinalIgnoreCase))
        {
            isAuthorized = (task.AssignedAssistantId == query.RequesterId);
        }
        else if (query.RequesterRole.Equals("EditorInChief", StringComparison.OrdinalIgnoreCase) ||
                 query.RequesterRole.Equals("EditorialBoard", StringComparison.OrdinalIgnoreCase) ||
                 query.RequesterRole.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            isAuthorized = true;
        }

        if (!isAuthorized)
        {
            throw new UnauthorizedAccessException("You are not authorized to view the layer versions for this task.");
        }

        var layers = await _layerRepo.GetByPageTaskIdAsync(query.PageTaskId, ct);
        
        var filtered = layers
            .Where(l => l.LayerType.Equals(query.LayerType, StringComparison.OrdinalIgnoreCase))
            .Select(l => new LayerVersionDto(
                l.Id,
                l.Version,
                l.FileUrlOriginal,
                l.FileUrlOptimized,
                l.IsCurrentVersion,
                l.CreatedAt))
            .OrderByDescending(l => l.Version);

        return filtered;
    }
}
