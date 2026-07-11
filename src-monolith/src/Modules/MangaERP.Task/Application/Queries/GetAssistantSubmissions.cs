using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;

namespace MangaERP.Task.Application.Queries.GetAssistantSubmissions;

public record GetAssistantSubmissionsQuery(Guid AssistantId) : IRequest<IEnumerable<AssistantSubmissionDto>>;

public record AssistantSubmissionDto(
    Guid LayerId,
    Guid PageTaskId,
    Guid ChapterId,
    string ChapterTitle,
    int PageNumber,
    string LayerType,
    int Version,
    string Status, // Approved, Reviewing, RevisionAlert
    DateTime SubmittedAt,
    string FileUrlOptimized,
    string? RejectionNote
);

public class GetAssistantSubmissionsHandler : IRequestHandler<GetAssistantSubmissionsQuery, IEnumerable<AssistantSubmissionDto>>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IArtworkLayerRepository _layerRepo;

    public GetAssistantSubmissionsHandler(
        IPageTaskRepository pageTaskRepo,
        IArtworkLayerRepository layerRepo)
    {
        _pageTaskRepo = pageTaskRepo;
        _layerRepo = layerRepo;
    }

    public async Task<IEnumerable<AssistantSubmissionDto>> Handle(GetAssistantSubmissionsQuery request, CancellationToken cancellationToken)
    {
        // For simplicity, we fetch all tasks for the assistant, then all layers for those tasks submitted by them.
        var tasks = await _pageTaskRepo.GetByAssistantAsync(request.AssistantId, cancellationToken);
        var result = new List<AssistantSubmissionDto>();

        foreach (var task in tasks)
        {
            var layers = await _layerRepo.GetByPageTaskIdAsync(task.Id, cancellationToken);
            var assistantLayers = layers.Where(l => l.AssistantId == request.AssistantId);

            foreach (var layer in assistantLayers)
            {
                // Try to infer layer status. If it's the current version, it matches task status or 'Approved' if task moved on.
                // Or just use the task status if it's the current version, otherwise 'Replaced'.
                string layerStatus = layer.IsCurrentVersion ? task.TaskStatus.ToString() : "Archived/Replaced";
                
                result.Add(new AssistantSubmissionDto(
                    layer.Id,
                    task.Id,
                    task.ChapterId,
                    task.Chapter?.Title ?? string.Empty,
                    task.PageNumber,
                    layer.LayerType,
                    layer.Version,
                    layerStatus,
                    layer.SubmittedAt ?? layer.CreatedAt,
                    layer.FileUrlOptimized ?? layer.FileUrlOriginal,
                    layer.RejectionNote
                ));
            }
        }

        return result.OrderByDescending(r => r.SubmittedAt).ToList();
    }
}
