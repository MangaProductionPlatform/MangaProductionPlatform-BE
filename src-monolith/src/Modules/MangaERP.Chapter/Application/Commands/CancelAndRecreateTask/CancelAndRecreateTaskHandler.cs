using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Commands.CancelAndRecreateTask;

public record CancelAndRecreateTaskCommand(
    Guid MangakaId,
    Guid PageTaskId,
    TaskCancellationCategory CancellationCategory = TaskCancellationCategory.OtherTaskIssue,
    string? Reason = null,
    bool ConfirmProgressLoss = false,
    bool CopyTaskDetails = true
) : IRequest<CancelAndRecreateTaskResult>
{
    public CancelAndRecreateTaskCommand(Guid mangakaId, Guid pageTaskId)
        : this(mangakaId, pageTaskId, TaskCancellationCategory.OtherTaskIssue, null, false, true) { }

    public CancelAndRecreateTaskCommand(Guid mangakaId, Guid pageTaskId, string? reason, bool confirmProgressLoss, bool copyTaskDetails)
        : this(mangakaId, pageTaskId, TaskCancellationCategory.OtherTaskIssue, reason, confirmProgressLoss, copyTaskDetails) { }
}

public record CancelAndRecreateTaskResult(
    Guid CancelledTaskId,
    Guid NewPageTaskId,
    string Status,
    int PageNumber,
    string BaseImageUrl
)
{
    public Guid OldPageTaskId => CancelledTaskId;
}

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

        // Rule 1: Always block cancel-and-recreate if task already has real artwork submissions or is completed/reviewing
        bool hasArtworkSubmissions = await _pageTaskRepo.HasSubmissionsAsync(oldTask.Id, ct);
        if (hasArtworkSubmissions ||
            oldTask.TaskStatus == PageTaskStatus.Approved ||
            oldTask.TaskStatus == PageTaskStatus.Reviewing)
        {
            throw new InvalidOperationException("Cannot cancel and recreate a task that already has artwork submissions or is completed.");
        }

        // Rule 2: If task has progress updates (but no artwork submissions), confirmProgressLoss is required
        bool hasProgress = oldTask.ProgressPercent > 0 || await _pageTaskRepo.HasProgressUpdatesAsync(oldTask.Id, ct);
        if (hasProgress && !cmd.ConfirmProgressLoss)
        {
            throw new InvalidOperationException("Task has progress updates. Set confirmProgressLoss to true to confirm progress deletion and recreate task.");
        }

        int originalPageNumber = oldTask.PageNumber;
        Guid? previousAssistantId = oldTask.AssignedAssistantId;
        DateTime now = DateTime.UtcNow;

        // 1. Soft-delete the old task (preserves original PageNumber, assignment history and audit data)
        oldTask.TaskStatus = PageTaskStatus.Cancelled;
        oldTask.CancellationCategory = cmd.CancellationCategory;
        oldTask.CancellationReason = cmd.Reason;
        oldTask.RecreatedAt = now;
        oldTask.RecreatedByUserId = cmd.MangakaId;
        oldTask.IsDeleted = true;
        oldTask.DeletedAt = now;
        oldTask.UpdatedAt = now;

        await _pageTaskRepo.UpdateAsync(oldTask, ct);

        // 2. Create the new PageTask with link back to old task and previous assistant info
        string baseImage = !string.IsNullOrWhiteSpace(oldTask.BaseImageUrl) ? oldTask.BaseImageUrl : "https://example.com/page-placeholder.png";
        var newTask = PageTask.CreatePending(oldTask.ChapterId, originalPageNumber, baseImage);

        if (cmd.CopyTaskDetails)
        {
            newTask.Description = oldTask.Description;
            newTask.Deadline = oldTask.Deadline;
            newTask.TaskType = oldTask.TaskType;
        }

        newTask.AssignedAssistantId = null;
        newTask.PrimaryAssistantId = null;
        newTask.BackupAssistantId = null;
        newTask.CurrentAssignmentAttemptId = null;
        newTask.WorkStartedAt = null;
        newTask.ProgressPercent = 0;
        newTask.HalfwayWarningSentAt = null;

        // Structured Cancellation Link & Data
        newTask.RecreatedFromTaskId = oldTask.Id;
        newTask.PreviousAssignedAssistantId = previousAssistantId;
        newTask.CancellationCategory = cmd.CancellationCategory;
        newTask.CancellationReason = cmd.Reason;
        newTask.RecreatedAt = now;
        newTask.RecreatedByUserId = cmd.MangakaId;

        await _pageTaskRepo.AddAsync(newTask, ct);

        // Save changes for both update and create
        await _pageTaskRepo.SaveChangesAsync(ct);

        return new CancelAndRecreateTaskResult(
            oldTask.Id,
            newTask.Id,
            newTask.TaskStatus.ToString(),
            newTask.PageNumber,
            newTask.BaseImageUrl
        );
    }
}
