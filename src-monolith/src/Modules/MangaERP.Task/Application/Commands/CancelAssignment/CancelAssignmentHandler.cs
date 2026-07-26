using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;

namespace MangaERP.Task.Application.Commands.CancelAssignment;

public record CancelAssignmentCommand(
    Guid AttemptId,
    Guid ActorUserId,
    string? Reason = null) : IRequest<bool>;

public class CancelAssignmentHandler : IRequestHandler<CancelAssignmentCommand, bool>
{
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly IPageTaskRepository _taskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly INotificationService _notifications;

    public CancelAssignmentHandler(
        ITaskAssignmentAttemptRepository attemptRepo,
        IPageTaskRepository taskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        INotificationService notifications)
    {
        _attemptRepo = attemptRepo;
        _taskRepo = taskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _notifications = notifications;
    }

    public async Task<bool> Handle(CancelAssignmentCommand request, CancellationToken ct)
    {
        var attempt = await _attemptRepo.GetByIdAsync(request.AttemptId, ct)
            ?? throw new EntityNotFoundException("TaskAssignmentAttempt", request.AttemptId);

        var task = await _taskRepo.GetByIdAsync(attempt.TaskId, ct)
            ?? throw new EntityNotFoundException("PageTask", attempt.TaskId);

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
            ?? throw new EntityNotFoundException("Chapter", task.ChapterId);

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new EntityNotFoundException("MangaSeries", chapter.SeriesId);

        if (series.AuthorId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the Mangaka who owns the series can cancel assignment attempts.");

        if (attempt.Status != TaskAssignmentAttemptStatus.PendingAcceptance)
            throw new InvalidOperationException($"Only PendingAcceptance attempts can be cancelled. Current status is '{attempt.Status}'.");

        attempt.Cancel(DateTime.UtcNow, request.Reason ?? "Cancelled by Mangaka");
        await _attemptRepo.UpdateAsync(attempt, ct);

        if (attempt.AssignmentRole == "Primary")
        {
            task.PrimaryAssistantId = null;
            if (task.AssignedAssistantId == attempt.AssistantId)
            {
                task.AssignedAssistantId = null;
                task.TaskStatus = PageTaskStatus.ReassignmentRequired;
                task.ReassignmentReason = request.Reason ?? "Primary assignment cancelled by Mangaka.";
            }
            await _taskRepo.UpdateAsync(task, ct);
        }
        else if (attempt.AssignmentRole == "Backup")
        {
            if (task.BackupAssistantId == attempt.AssistantId)
            {
                task.BackupAssistantId = null;
                await _taskRepo.UpdateAsync(task, ct);
            }
        }

        await _attemptRepo.SaveChangesAsync(ct);
        await _taskRepo.SaveChangesAsync(ct);

        await _notifications.NotifyCollaborationEventAsync(
            attempt.AssistantId,
            "AssignmentCancelled",
            "Assignment Cancelled",
            $"Your assignment for task #{task.PageNumber} in chapter '{chapter.Title}' was cancelled by the Mangaka.",
            task.Id,
            ct);

        return true;
    }
}
