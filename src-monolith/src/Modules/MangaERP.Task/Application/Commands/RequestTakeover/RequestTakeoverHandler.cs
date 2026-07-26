using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MangaERP.Task.Domain.Enums;
using MediatR;

namespace MangaERP.Task.Application.Commands.RequestTakeover;

public record RequestTakeoverCommand(
    Guid PageTaskId,
    Guid ActorUserId,
    string Reason,
    TimeSpan? WorkDuration = null
) : IRequest<RequestTakeoverResult>;

public record RequestTakeoverResult(
    Guid PageTaskId,
    Guid BackupAssistantId,
    Guid AttemptId,
    string TakeoverStatus,
    DateTime ResponseDeadline
);

public class RequestTakeoverHandler : IRequestHandler<RequestTakeoverCommand, RequestTakeoverResult>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly INotificationService _notificationService;

    public RequestTakeoverHandler(
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        INotificationService notificationService)
    {
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _attemptRepo = attemptRepo;
        _notificationService = notificationService;
    }

    public async Task<RequestTakeoverResult> Handle(RequestTakeoverCommand request, CancellationToken ct)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(request.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"PageTask {request.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {pageTask.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        // Authorization: Strictly ONLY the Mangaka owner of the series can activate takeover.
        // Backup Assistants, Tantou Editors, and other users are forbidden (throw UnauthorizedAccessException -> 403).
        if (request.ActorUserId != series.AuthorId)
        {
            throw new UnauthorizedAccessException("Only the Mangaka owner of the series can activate takeover.");
        }

        if (pageTask.BackupAssistantId is null)
            throw new InvalidOperationException("No Backup Assistant is assigned to this task.");

        var backupAssistantId = pageTask.BackupAssistantId.Value;

        // Supersede active attempt of old Primary to release old Primary workload
        var activePrimaryAttempt = (await _attemptRepo.GetByTaskIdAsync(pageTask.Id, ct))
            .FirstOrDefault(a => a.AssistantId != backupAssistantId && (a.Status == TaskAssignmentAttemptStatus.PendingAcceptance || a.Status == TaskAssignmentAttemptStatus.Accepted));

        if (activePrimaryAttempt is not null)
        {
            activePrimaryAttempt.Supersede(DateTime.UtcNow, $"Superseded for Backup takeover: {request.Reason}");
            await _attemptRepo.UpdateAsync(activePrimaryAttempt, ct);
        }

        pageTask.RequestTakeover(request.Reason);
        var now = DateTime.UtcNow;
        var newDeadline = request.WorkDuration.HasValue ? now.Add(request.WorkDuration.Value) : (pageTask.Deadline ?? now.AddDays(2));
        pageTask.AcceptTakeover(backupAssistantId, now, newDeadline);

        // Update task pointers: Backup is promoted to Primary
        pageTask.AssignedAssistantId = backupAssistantId;
        pageTask.PrimaryAssistantId = backupAssistantId;
        pageTask.BackupAssistantId = null;

        await _pageTaskRepo.UpdateAsync(pageTask, ct);

        // Find existing backup attempt or create primary attempt for promoted backup
        var backupAttempt = (await _attemptRepo.GetByTaskIdAsync(pageTask.Id, ct))
            .FirstOrDefault(a => a.AssistantId == backupAssistantId && (a.Status == TaskAssignmentAttemptStatus.Accepted || a.Status == TaskAssignmentAttemptStatus.PendingAcceptance));

        if (backupAttempt is null)
        {
            var existingAttempts = await _attemptRepo.GetByTaskIdAsync(pageTask.Id, ct);
            int attemptNumber = existingAttempts.Count() + 1;
            backupAttempt = TaskAssignmentAttempt.CreatePending(
                taskId: pageTask.Id,
                assistantId: backupAssistantId,
                collaborationId: Guid.NewGuid(),
                attemptNumber: attemptNumber,
                assignedByUserId: request.ActorUserId,
                expiresAt: now.AddHours(24),
                assignmentRole: "Primary",
                responseDeadline: now.AddHours(24),
                workDeadline: newDeadline,
                previousAttemptId: activePrimaryAttempt?.Id);
            backupAttempt.Accept(backupAssistantId, now);
            await _attemptRepo.AddAsync(backupAttempt, ct);
        }
        else
        {
            if (backupAttempt.Status == TaskAssignmentAttemptStatus.PendingAcceptance)
            {
                backupAttempt.Accept(backupAssistantId, now);
            }
            await _attemptRepo.UpdateAsync(backupAttempt, ct);
        }

        await _attemptRepo.SaveChangesAsync(ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        await _notificationService.NotifyTaskAssignedAsync(
            backupAssistantId, pageTask.Id, pageTask.PageNumber, ct);

        return new RequestTakeoverResult(
            pageTask.Id,
            backupAssistantId,
            backupAttempt.Id,
            pageTask.TakeoverStatus ?? "TakeoverCompleted",
            now.AddHours(24));
    }
}
