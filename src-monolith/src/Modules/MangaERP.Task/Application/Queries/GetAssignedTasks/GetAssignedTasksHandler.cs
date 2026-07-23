using MangaERP.Chapter.Application.Ports;
using MangaERP.Task.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetAssignedTasks;

public record GetAssignedTasksQuery(
    Guid AssistantId,
    string? StatusFilter
) : IRequest<IEnumerable<AssignedTaskDto>>;

public record AssignedTaskDto(
    Guid PageTaskId,
    Guid ChapterId,
    string ChapterTitle,
    decimal ChapterNumber,
    int PageNumber,
    string TaskStatus,
    string? CurrentLayerType,
    int? CurrentLayerVersion,
    DateTime UpdatedAt,
    string? ChapterCoverImageUrl,
    string BaseImageUrl,
    string? Description,
    DateTime? Deadline,
    string TaskType);

public class GetAssignedTasksHandler : IRequestHandler<GetAssignedTasksQuery, IEnumerable<AssignedTaskDto>>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IArtworkLayerRepository _layerRepo;

    public GetAssignedTasksHandler(
        IPageTaskRepository pageTaskRepo,
        IArtworkLayerRepository layerRepo)
    {
        _pageTaskRepo = pageTaskRepo;
        _layerRepo = layerRepo;
    }

    public async Task<IEnumerable<AssignedTaskDto>> Handle(GetAssignedTasksQuery query, CancellationToken ct)
    {
        var tasks = await _pageTaskRepo.GetByAssistantAsync(query.AssistantId, ct);
        var result = new List<AssignedTaskDto>();

        foreach (var task in tasks)
        {
            if (!string.IsNullOrWhiteSpace(query.StatusFilter)
                && !task.TaskStatus.ToString().Equals(query.StatusFilter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var chapter = task.Chapter; // Đã được Include sẵn bởi GetByAssistantAsync
            var layer = await _layerRepo.GetCurrentByPageTaskIdAsync(task.Id, ct);

            result.Add(new AssignedTaskDto(
                task.Id,
                task.ChapterId,
                chapter?.Title ?? string.Empty,
                chapter?.ChapterNumber ?? 0,
                task.PageNumber,
                task.TaskStatus.ToString(),
                layer?.LayerType,
                layer?.Version,
                task.UpdatedAt,
                chapter?.CoverImageUrl,
                task.BaseImageUrl,
                task.Description,
                task.Deadline,
                task.TaskType.ToString()));
        }

        return result;
    }
}
