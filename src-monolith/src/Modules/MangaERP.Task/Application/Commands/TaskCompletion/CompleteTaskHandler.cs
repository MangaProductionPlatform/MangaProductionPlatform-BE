using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Entities;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Studio.Application.Ports;
using MangaERP.Task.Application.Ports;
using System.Threading.Tasks;

namespace MangaERP.Task.Application.Commands.TaskCompletion;

public record CompleteTaskCommand(Guid TaskId, Guid ActorUserId) : IRequest<TaskCompletionResultDto>;

public record TaskCompletionResultDto(
    Guid TaskId,
    string TaskStatus,
    int ProgressPercent,
    DateTime CompletedAt);

public sealed class CompleteTaskHandler : IRequestHandler<CompleteTaskCommand, TaskCompletionResultDto>
{
    private readonly IPageTaskRepository _taskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly ICollaborationAuthorizationService _authService;
    private readonly INotificationService _notifications;
    private readonly IAuditEventRepository _auditRepo;

    public CompleteTaskHandler(
        IPageTaskRepository taskRepo,
        IChapterRepository chapterRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        ICollaborationAuthorizationService authService,
        INotificationService notifications,
        IAuditEventRepository auditRepo)
    {
        _taskRepo = taskRepo;
        _chapterRepo = chapterRepo;
        _attemptRepo = attemptRepo;
        _authService = authService;
        _notifications = notifications;
        _auditRepo = auditRepo;
    }

    public async Task<TaskCompletionResultDto> Handle(CompleteTaskCommand request, CancellationToken ct)
    {
        bool canComplete = await _authService.CanCompleteTaskAsync(request.ActorUserId, request.TaskId, ct);
        if (!canComplete)
            throw new UnauthorizedAccessException("You are not authorized to complete this task.");

        var task = await _taskRepo.GetByIdAsync(request.TaskId, ct)
            ?? throw new EntityNotFoundException("PageTask", request.TaskId);

        var acceptedAttempt = await _attemptRepo.GetAcceptedByTaskIdAsync(request.TaskId, ct)
            ?? throw new ConflictException("Task has no active accepted assignment attempt.");

        if (acceptedAttempt.AssistantId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the assigned assistant can complete this task.");

        DateTime now = DateTime.UtcNow;
        task.CompleteTask(now);

        var audit = new AuditEvent(
            "TaskCompleted",
            request.ActorUserId,
            "PageTask",
            task.Id,
            acceptedAttempt.CollaborationId,
            taskId: task.Id,
            metadataJson: "{\"status\":\"Reviewing\",\"progressPercent\":100}");

        await _taskRepo.UpdateAsync(task, ct);
        await _auditRepo.AddAsync(audit, ct);
        await _auditRepo.SaveChangesAsync(ct);

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct);

        await _notifications.NotifyCollaborationEventAsync(
            acceptedAttempt.AssignedByUserId,
            "TaskCompleted",
            "Task Completed by Assistant",
            $"Assistant completed task #{task.PageNumber} in chapter '{chapter?.Title}'. Ready for review.",
            task.Id,
            ct);

        return new TaskCompletionResultDto(
            task.Id,
            task.TaskStatus.ToString(),
            task.ProgressPercent,
            now);
    }
}
