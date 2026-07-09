using MangaERP.Segmentation.Application.Ports;
using MangaERP.Segmentation.Domain.Entities;
using MediatR;

namespace MangaERP.Segmentation.Application.Queries.GetMySegmentationTasks;

public record GetMySegmentationTasksQuery(
    Guid CurrentUserId,
    SegmentationTaskStatus? StatusFilter,
    int Page,
    int PageSize
) : IRequest<PagedSegmentationTaskResult>;

public record SegmentationTaskDto(
    Guid TaskId,
    Guid PageId,
    string TaskType,
    string Status,
    string MaskRle,
    int[] Bbox,
    string? Note,
    Guid AssignedToUserId,
    string? AssignedToUserRole,
    Guid CreatedByUserId,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    int? OriginalWidth,
    int? OriginalHeight);

public record PagedSegmentationTaskResult(
    IEnumerable<SegmentationTaskDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public class GetMySegmentationTasksHandler
    : IRequestHandler<GetMySegmentationTasksQuery, PagedSegmentationTaskResult>
{
    private readonly ISegmentationTaskRepository _repo;

    public GetMySegmentationTasksHandler(ISegmentationTaskRepository repo)
        => _repo = repo;

    public async Task<PagedSegmentationTaskResult> Handle(
        GetMySegmentationTasksQuery query,
        CancellationToken ct)
    {
        // Enforce page constraints
        var page = Math.Max(query.Page, 1);
        var safePageSize = Math.Clamp(query.PageSize, 1, 100);

        var (items, totalCount) = await _repo.GetByAssignedUserAsync(
            query.CurrentUserId,
            query.StatusFilter,
            page,
            safePageSize,
            ct);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / safePageSize);

        var dtos = items.Select(t => new SegmentationTaskDto(
            t.Id,
            t.PageId,
            t.TaskType.ToString(),
            t.Status.ToString(),
            t.MaskRle,
            t.Bbox,
            t.Note,
            t.AssignedToUserId,
            t.AssignedToUserRole,
            t.CreatedByUserId,
            t.CreatedAt,
            t.CompletedAt,
            t.OriginalWidth,
            t.OriginalHeight));

        return new PagedSegmentationTaskResult(dtos, page, safePageSize, totalCount, totalPages);
    }
}
