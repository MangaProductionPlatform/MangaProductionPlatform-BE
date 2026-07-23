using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Entities;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Studio.Application.Ports;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using System.Threading.Tasks;

namespace MangaERP.Task.Application.Commands.TaskProgress;

public record SubmitTaskProgressCommand(
    Guid TaskId,
    int ProgressPercent,
    string? Note,
    Guid ActorUserId) : IRequest<TaskProgressDto>;

public record GetTaskProgressHistoryQuery(Guid TaskId, Guid ActorUserId) : IRequest<IEnumerable<TaskProgressDto>>;

public record TaskProgressDto(
    Guid Id,
    Guid TaskId,
    Guid AssignmentAttemptId,
    Guid AssistantId,
    int ProgressPercent,
    string? Note,
    DateTime CreatedAt);

public sealed class SubmitTaskProgressHandler : IRequestHandler<SubmitTaskProgressCommand, TaskProgressDto>
{
    private readonly IPageTaskRepository _taskRepo;
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly ITaskProgressRepository _progressRepo;
    private readonly ICollaborationAuthorizationService _authService;
    private readonly INotificationService _notifications;
    private readonly IAuditEventRepository _auditRepo;

    public SubmitTaskProgressHandler(
        IPageTaskRepository taskRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        ITaskProgressRepository progressRepo,
        ICollaborationAuthorizationService authService,
        INotificationService notifications,
        IAuditEventRepository auditRepo)
    {
        _taskRepo = taskRepo;
        _attemptRepo = attemptRepo;
        _progressRepo = progressRepo;
        _authService = authService;
        _notifications = notifications;
        _auditRepo = auditRepo;
    }

    public async Task<TaskProgressDto> Handle(SubmitTaskProgressCommand request, CancellationToken ct)
    {
        bool canSubmit = await _authService.CanSubmitProgressAsync(request.ActorUserId, request.TaskId, ct);
        if (!canSubmit)
            throw new UnauthorizedAccessException("You are not authorized to submit progress for this task.");

        var task = await _taskRepo.GetByIdAsync(request.TaskId, ct)
            ?? throw new EntityNotFoundException("PageTask", request.TaskId);

        var acceptedAttempt = await _attemptRepo.GetAcceptedByTaskIdAsync(request.TaskId, ct)
            ?? throw new ConflictException("Task has no active accepted assignment attempt.");

        if (acceptedAttempt.AssistantId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the assigned assistant can submit progress.");

        task.SubmitProgress(request.ProgressPercent);

        var progressUpdate = new TaskProgressUpdate(
            task.Id,
            acceptedAttempt.Id,
            request.ActorUserId,
            request.ProgressPercent,
            request.Note,
            request.ActorUserId);

        var audit = new AuditEvent(
            "TaskProgressSubmitted",
            request.ActorUserId,
            "PageTask",
            task.Id,
            acceptedAttempt.CollaborationId,
            taskId: task.Id,
            metadataJson: $"{{\"progressPercent\":{request.ProgressPercent}}}");

        await _progressRepo.AddAsync(progressUpdate, ct);
        await _taskRepo.UpdateAsync(task, ct);
        await _auditRepo.AddAsync(audit, ct);
        await _progressRepo.SaveChangesAsync(ct);

        await _notifications.NotifyCollaborationEventAsync(
            acceptedAttempt.AssignedByUserId,
            "TaskProgressUpdated",
            "Task Progress Updated",
            $"Assistant updated progress to {request.ProgressPercent}% on task #{task.PageNumber}.",
            task.Id,
            ct);

        return new TaskProgressDto(
            progressUpdate.Id,
            progressUpdate.TaskId,
            progressUpdate.AssignmentAttemptId,
            progressUpdate.AssistantId,
            progressUpdate.ProgressPercent,
            progressUpdate.Note,
            progressUpdate.CreatedAt);
    }
}

public sealed class GetTaskProgressHistoryHandler : IRequestHandler<GetTaskProgressHistoryQuery, IEnumerable<TaskProgressDto>>
{
    private readonly IPageTaskRepository _taskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ITaskProgressRepository _progressRepo;
    private readonly ICollaborationAuthorizationService _authService;

    public GetTaskProgressHistoryHandler(
        IPageTaskRepository taskRepo,
        IChapterRepository chapterRepo,
        IStudioInvitationRepository collabRepo,
        ITaskProgressRepository progressRepo,
        ICollaborationAuthorizationService authService)
    {
        _taskRepo = taskRepo;
        _chapterRepo = chapterRepo;
        _collabRepo = collabRepo;
        _progressRepo = progressRepo;
        _authService = authService;
    }

    public async Task<IEnumerable<TaskProgressDto>> Handle(GetTaskProgressHistoryQuery request, CancellationToken ct)
    {
        bool canAccess = await _authService.CanAccessTaskAsync(request.ActorUserId, request.TaskId, ct);
        if (!canAccess)
        {
            var task = await _taskRepo.GetByIdAsync(request.TaskId, ct);
            if (task != null)
            {
                var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct);
                if (chapter != null)
                {
                    bool isMangaka = await _collabRepo.GetNonEndedCollaborationByAssistantAsync(request.ActorUserId, ct) == null;
                    if (!isMangaka)
                        throw new UnauthorizedAccessException("You are not authorized to view progress history for this task.");
                }
            }
        }

        var updates = await _progressRepo.GetByTaskIdAsync(request.TaskId, ct);
        return updates.Select(u => new TaskProgressDto(
            u.Id, u.TaskId, u.AssignmentAttemptId, u.AssistantId, u.ProgressPercent, u.Note, u.CreatedAt));
    }
}
