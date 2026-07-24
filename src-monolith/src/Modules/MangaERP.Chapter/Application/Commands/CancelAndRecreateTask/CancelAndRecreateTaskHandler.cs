using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.CancelAndRecreateTask;

public record CancelAndRecreateTaskCommand(
    Guid MangakaId,
    Guid PageTaskId
) : IRequest<CancelAndRecreateTaskResult>;

public record CancelAndRecreateTaskResult(
    Guid OldPageTaskId,
    Guid NewPageTaskId,
    int PageNumber,
    string BaseImageUrl,
    string TaskStatus
);

public class CancelAndRecreateTaskHandler : IRequestHandler<CancelAndRecreateTaskCommand, CancelAndRecreateTaskResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public CancelAndRecreateTaskHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<CancelAndRecreateTaskResult> Handle(CancelAndRecreateTaskCommand cmd, CancellationToken ct)
    {
        var oldTask = await _pageTaskRepo.GetByIdAsync(cmd.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Page task {cmd.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(oldTask.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {oldTask.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // Only the author of the series can cancel and recreate tasks
        if (series.AuthorId != cmd.MangakaId)
            throw new UnauthorizedAccessException("Only the author of the series can cancel and recreate page tasks.");

        int originalPageNumber = oldTask.PageNumber;
        int nextNegativePageNumber = await _pageTaskRepo.GetNextNegativePageNumberAsync(oldTask.ChapterId, ct);

        // 1. Soft-delete the old task and change its page number to negative to bypass Unique Index constraint (ChapterId, PageNumber)
        oldTask.PageNumber = nextNegativePageNumber;
        oldTask.IsDeleted = true;
        oldTask.DeletedAt = DateTime.UtcNow;
        oldTask.UpdatedAt = DateTime.UtcNow;

        await _pageTaskRepo.UpdateAsync(oldTask, ct);

        // 2. Create the new PageTask with the original page number
        var newTask = PageTask.CreatePending(oldTask.ChapterId, originalPageNumber, oldTask.BaseImageUrl);
        await _pageTaskRepo.AddAsync(newTask, ct);

        // Save changes for both update and create
        await _pageTaskRepo.SaveChangesAsync(ct);

        return new CancelAndRecreateTaskResult(
            oldTask.Id,
            newTask.Id,
            newTask.PageNumber,
            newTask.BaseImageUrl,
            newTask.TaskStatus.ToString()
        );
    }
}
