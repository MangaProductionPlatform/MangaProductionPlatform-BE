using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Queries.GetTaskDetail;

public record GetTaskDetailQuery(Guid RequesterId, Guid PageTaskId) : IRequest<TaskDetailDto>;

public record TaskDetailDto(
    Guid PageTaskId,
    Guid ChapterId,
    int PageNumber,
    Guid? AssignedAssistantId,
    string? Description,
    string TaskStatus,
    string? RegionMask,
    string TaskType,
    string BaseImageUrl,
    string? PreviewCompositeUrl,
    DateTime? Deadline,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public class GetTaskDetailHandler : IRequestHandler<GetTaskDetailQuery, TaskDetailDto>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetTaskDetailHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<TaskDetailDto> Handle(GetTaskDetailQuery query, CancellationToken ct)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(query.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Page task {query.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {pageTask.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // Mangaka sở hữu, editor được gán, hoặc Assistant được giao việc mới được xem
        if (series.AuthorId != query.RequesterId &&
            chapter.AssignedEditorId != query.RequesterId &&
            pageTask.AssignedAssistantId != query.RequesterId)
        {
            throw new UnauthorizedAccessException("You are not authorized to view this task's details.");
        }

        return new TaskDetailDto(
            pageTask.Id,
            pageTask.ChapterId,
            pageTask.PageNumber,
            pageTask.AssignedAssistantId,
            pageTask.Description,
            pageTask.TaskStatus.ToString(),
            pageTask.RegionMask,
            pageTask.TaskType.ToString(),
            pageTask.BaseImageUrl,
            pageTask.PreviewPage?.CompositeFileUrl,
            pageTask.Deadline,
            pageTask.CreatedAt,
            pageTask.UpdatedAt
        );
    }
}
