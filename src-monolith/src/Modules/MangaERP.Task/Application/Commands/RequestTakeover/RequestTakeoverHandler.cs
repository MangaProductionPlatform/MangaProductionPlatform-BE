using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MangaERP.Studio.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Identity.Application.Ports;

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
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly ICollaborationAuthorizationService _collaborationAuth;
    private readonly INotificationService _notificationService;

    public RequestTakeoverHandler(
        IPageTaskRepository pageTaskRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        ICollaborationAuthorizationService collaborationAuth,
        INotificationService notificationService)
    {
        _pageTaskRepo = pageTaskRepo;
        _attemptRepo = attemptRepo;
        _collaborationAuth = collaborationAuth;
        _notificationService = notificationService;
    }

    public async Task<RequestTakeoverResult> Handle(RequestTakeoverCommand request, CancellationToken ct)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(request.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"PageTask {request.PageTaskId} not found.");

        if (pageTask.BackupAssistantId is null)
            throw new InvalidOperationException("No Backup Assistant is assigned to this task.");

        var backupAssistantId = pageTask.BackupAssistantId.Value;

        // Cancel previous pending/accepted attempt of Primary
        var activeAttempt = await _attemptRepo.GetAcceptedByTaskIdAsync(pageTask.Id, ct)
            ?? await _attemptRepo.GetPendingByTaskIdAsync(pageTask.Id, ct);

        if (activeAttempt is not null)
        {
            activeAttempt.Cancel(DateTime.UtcNow, $"Cancelled for Backup takeover: {request.Reason}");
            await _attemptRepo.UpdateAsync(activeAttempt, ct);
        }

        pageTask.RequestTakeover(request.Reason);
        await _pageTaskRepo.UpdateAsync(pageTask, ct);

        var existingAttempts = await _attemptRepo.GetByTaskIdAsync(pageTask.Id, ct);
        int attemptNumber = existingAttempts.Count() + 1;

        var responseDeadline = DateTime.UtcNow.AddHours(24);
        var takeoverAttempt = TaskAssignmentAttempt.CreatePending(
            taskId: pageTask.Id,
            assistantId: backupAssistantId,
            collaborationId: Guid.NewGuid(), // Collaboration validated via auth
            attemptNumber: attemptNumber,
            assignedByUserId: request.ActorUserId,
            expiresAt: responseDeadline,
            assignmentRole: "BackupTakeover",
            responseDeadline: responseDeadline,
            workDeadline: request.WorkDuration.HasValue ? DateTime.UtcNow.Add(request.WorkDuration.Value) : pageTask.Deadline,
            previousAttemptId: activeAttempt?.Id);

        await _attemptRepo.AddAsync(takeoverAttempt, ct);
        await _attemptRepo.SaveChangesAsync(ct);
        await _pageTaskRepo.SaveChangesAsync(ct);

        await _notificationService.NotifyTaskAssignedAsync(
            backupAssistantId, pageTask.Id, pageTask.PageNumber, ct);

        return new RequestTakeoverResult(
            pageTask.Id,
            backupAssistantId,
            takeoverAttempt.Id,
            pageTask.TakeoverStatus ?? "TakeoverRequested",
            responseDeadline);
    }
}
