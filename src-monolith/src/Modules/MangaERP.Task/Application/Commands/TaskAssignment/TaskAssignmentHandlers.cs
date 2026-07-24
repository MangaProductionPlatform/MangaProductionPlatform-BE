using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Task.Application.Commands.TaskAssignment;

public record AssignTaskToAssistantCommand(
    Guid TaskId,
    Guid AssistantId,
    Guid ActorUserId,
    string? Description,
    DateTime? Deadline,
    TimeSpan? Duration = null) : IRequest<TaskAssignmentAttemptDto>;

public record RespondTaskAssignmentCommand(
    Guid AttemptId,
    bool Accept,
    string? RejectionReason,
    Guid ActorUserId,
    Guid ExpectedConcurrencyToken) : IRequest<TaskAssignmentAttemptDto>;

public record GetTaskAssignmentHistoryQuery(Guid TaskId, Guid ActorUserId) : IRequest<IEnumerable<TaskAssignmentAttemptDto>>;
public record GetAssistantWorkloadQuery(Guid AssistantId, Guid ActorUserId) : IRequest<AssistantWorkloadDto>;

public record TaskAssignmentAttemptDto(
    Guid Id,
    Guid TaskId,
    Guid AssistantId,
    Guid CollaborationId,
    int AttemptNumber,
    string Status,
    DateTime AssignedAt,
    DateTime? RespondedAt,
    DateTime? AcceptedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    DateTime? ExpiresAt,
    Guid AssignedByUserId,
    Guid ConcurrencyToken);

public record AssistantWorkloadDto(
    Guid AssistantId,
    int CurrentActiveAssignments,
    int MaximumActiveAssignments,
    bool IsCapacityReached);

public sealed class AssignTaskToAssistantHandler : IRequestHandler<AssignTaskToAssistantCommand, TaskAssignmentAttemptDto>
{
    private readonly IPageTaskRepository _taskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _config;

    public AssignTaskToAssistantHandler(
        IPageTaskRepository taskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        INotificationService notifications,
        IConfiguration config)
    {
        _taskRepo = taskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _collabRepo = collabRepo;
        _grantRepo = grantRepo;
        _attemptRepo = attemptRepo;
        _notifications = notifications;
        _config = config;
    }

    public async Task<TaskAssignmentAttemptDto> Handle(AssignTaskToAssistantCommand request, CancellationToken ct)
    {
        var task = await _taskRepo.GetByIdAsync(request.TaskId, ct)
            ?? throw new EntityNotFoundException("PageTask", request.TaskId);

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
            ?? throw new EntityNotFoundException("Chapter", task.ChapterId);

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new EntityNotFoundException("MangaSeries", chapter.SeriesId);

        if (series.AuthorId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the Mangaka who owns the series can assign tasks.");

        var collaboration = await _collabRepo.GetNonEndedCollaborationByAssistantAsync(request.AssistantId, ct)
            ?? throw new ConflictException("Assistant has no non-ended collaboration with this Mangaka.");

        if (collaboration.MangakaId != request.ActorUserId)
            throw new ConflictException("Assistant collaboration is with a different Mangaka.");

        if (collaboration.Status != CollaborationStatus.Active)
            throw new ConflictException($"Cannot assign task to assistant in collaboration status '{collaboration.Status}'.");

        var grant = await _grantRepo.GetActiveGrantAsync(collaboration.Id, series.Id, ct);
        if (grant == null)
            throw new ConflictException("Assistant does not have active series access for this series.");

        // Check current pending or accepted attempt for task
        var pendingAttempt = await _attemptRepo.GetPendingByTaskIdAsync(task.Id, ct);
        if (pendingAttempt != null)
            throw new ConflictException("Task already has a pending assignment attempt.");

        var acceptedAttempt = await _attemptRepo.GetAcceptedByTaskIdAsync(task.Id, ct);
        if (acceptedAttempt != null)
            throw new ConflictException("Task already has an accepted assignment.");

        // Workload capacity check
        int maxWorkload = _config.GetValue<int?>("AssistantWorkload:MaximumActiveAssignments") ?? 3;
        int currentWorkload = await _attemptRepo.GetActiveWorkloadCountAsync(request.AssistantId, ct);
        if (currentWorkload >= maxWorkload)
            throw new ConflictException($"Assistant has reached maximum active task capacity ({maxWorkload}).");

        int nextAttemptNumber = await _attemptRepo.GetMaxAttemptNumberAsync(task.Id, ct) + 1;

        var attempt = TaskAssignmentAttempt.CreatePending(
            task.Id,
            request.AssistantId,
            collaboration.Id,
            nextAttemptNumber,
            request.ActorUserId);

        task.AssignPending(request.AssistantId, request.Description, request.Deadline);

        await _attemptRepo.AddAsync(attempt, ct);
        await _taskRepo.UpdateAsync(task, ct);
        await _attemptRepo.SaveChangesAsync(ct);

        await _notifications.NotifyCollaborationEventAsync(
            request.AssistantId,
            "TaskAssigned",
            "New Task Assigned",
            $"You have been assigned task #{task.PageNumber} in chapter '{chapter.Title}'.",
            task.Id,
            ct);

        return new TaskAssignmentAttemptDto(
            attempt.Id,
            attempt.TaskId,
            attempt.AssistantId,
            attempt.CollaborationId,
            attempt.AttemptNumber,
            attempt.Status.ToString(),
            attempt.AssignedAt,
            attempt.RespondedAt,
            attempt.AcceptedAt,
            attempt.RejectedAt,
            attempt.RejectionReason,
            attempt.ExpiresAt,
            attempt.AssignedByUserId,
            attempt.ConcurrencyToken);
    }
}

public sealed class RespondTaskAssignmentHandler : IRequestHandler<RespondTaskAssignmentCommand, TaskAssignmentAttemptDto>
{
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly IPageTaskRepository _taskRepo;
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly INotificationService _notifications;

    public RespondTaskAssignmentHandler(
        ITaskAssignmentAttemptRepository attemptRepo,
        IPageTaskRepository taskRepo,
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo,
        IChapterRepository chapterRepo,
        INotificationService notifications)
    {
        _attemptRepo = attemptRepo;
        _taskRepo = taskRepo;
        _collabRepo = collabRepo;
        _grantRepo = grantRepo;
        _chapterRepo = chapterRepo;
        _notifications = notifications;
    }

    public async Task<TaskAssignmentAttemptDto> Handle(RespondTaskAssignmentCommand request, CancellationToken ct)
    {
        var attempt = await _attemptRepo.GetByIdAsync(request.AttemptId, ct)
            ?? throw new EntityNotFoundException("TaskAssignmentAttempt", request.AttemptId);

        if (request.ExpectedConcurrencyToken != Guid.Empty && attempt.ConcurrencyToken != request.ExpectedConcurrencyToken)
            throw new ConflictException("The assignment attempt changed concurrently. Refresh and retry.");

        if (attempt.AssistantId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the assigned assistant can respond to this assignment attempt.");

        if (attempt.Status != TaskAssignmentAttemptStatus.PendingAcceptance)
            throw new ConflictException($"Assignment attempt is in status '{attempt.Status}' and cannot be responded to.");

        var task = await _taskRepo.GetByIdAsync(attempt.TaskId, ct)
            ?? throw new EntityNotFoundException("PageTask", attempt.TaskId);

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
            ?? throw new EntityNotFoundException("Chapter", task.ChapterId);

        var collaboration = await _collabRepo.GetCollaborationAsync(attempt.CollaborationId, ct)
            ?? throw new EntityNotFoundException("Collaboration", attempt.CollaborationId);

        DateTime now = DateTime.UtcNow;

        if (attempt.AssignmentRole == "BackupTakeover")
        {
            if (task.TaskStatus == MangaERP.Chapter.Domain.Entities.PageTaskStatus.Approved)
                throw new ConflictException("Task has already been completed and approved.");

            if (request.Accept)
            {
                attempt.Accept(request.ActorUserId, now);
                DateTime newDeadline = attempt.WorkDeadline ?? task.Deadline ?? now.AddDays(3);
                task.AcceptTakeover(attempt.AssistantId, now, newDeadline);
                task.CurrentAssignmentAttemptId = attempt.Id;

                await _attemptRepo.UpdateAsync(attempt, ct);
                await _taskRepo.UpdateAsync(task, ct);
                await _attemptRepo.SaveChangesAsync(ct);

                await _notifications.NotifyCollaborationEventAsync(
                    collaboration.MangakaId,
                    "BackupTakeoverAccepted",
                    "Backup Takeover Accepted",
                    $"Backup assistant accepted takeover for task #{task.PageNumber} in chapter '{chapter.Title}'. New deadline: {newDeadline:g}",
                    task.Id,
                    ct);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                    throw new ArgumentException("Rejection reason is required when rejecting a takeover assignment.");

                attempt.Reject(request.ActorUserId, request.RejectionReason, now);
                task.MarkReassignmentRequired($"Backup takeover rejected: {request.RejectionReason.Trim()}");

                await _attemptRepo.UpdateAsync(attempt, ct);
                await _taskRepo.UpdateAsync(task, ct);
                await _attemptRepo.SaveChangesAsync(ct);

                await _notifications.NotifyCollaborationEventAsync(
                    collaboration.MangakaId,
                    "BackupTakeoverFailed",
                    "Backup Takeover Rejected",
                    $"Backup assistant rejected takeover for task #{task.PageNumber} in chapter '{chapter.Title}'. Reassignment required.",
                    task.Id,
                    ct);
            }
        }
        else if (request.Accept)
        {
            if (collaboration.Status != CollaborationStatus.Active)
                throw new ConflictException($"Cannot accept assignment when collaboration is in status '{collaboration.Status}'.");

            var grant = await _grantRepo.GetActiveGrantAsync(collaboration.Id, chapter.SeriesId, ct);
            if (grant == null)
                throw new ConflictException("Cannot accept assignment because series access grant is no longer active.");

            attempt.Accept(request.ActorUserId, now);
            task.AcceptAssignment(now);
            task.CurrentAssignmentAttemptId = attempt.Id;

            await _attemptRepo.UpdateAsync(attempt, ct);
            await _taskRepo.UpdateAsync(task, ct);
            await _attemptRepo.SaveChangesAsync(ct);

            await _notifications.NotifyCollaborationEventAsync(
                collaboration.MangakaId,
                "TaskAssignmentAccepted",
                "Task Assignment Accepted",
                $"Assistant accepted task #{task.PageNumber} in chapter '{chapter.Title}'.",
                task.Id,
                ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RejectionReason))
                throw new ArgumentException("Rejection reason is required when rejecting an assignment.");

            attempt.Reject(request.ActorUserId, request.RejectionReason, now);
            task.RejectAssignment();

            await _attemptRepo.UpdateAsync(attempt, ct);
            await _taskRepo.UpdateAsync(task, ct);
            await _attemptRepo.SaveChangesAsync(ct);

            await _notifications.NotifyCollaborationEventAsync(
                collaboration.MangakaId,
                "TaskAssignmentRejected",
                "Task Assignment Rejected",
                $"Assistant rejected task #{task.PageNumber} in chapter '{chapter.Title}'. Reason: {request.RejectionReason.Trim()}",
                task.Id,
                ct);
        }

        return new TaskAssignmentAttemptDto(
            attempt.Id,
            attempt.TaskId,
            attempt.AssistantId,
            attempt.CollaborationId,
            attempt.AttemptNumber,
            attempt.Status.ToString(),
            attempt.AssignedAt,
            attempt.RespondedAt,
            attempt.AcceptedAt,
            attempt.RejectedAt,
            attempt.RejectionReason,
            attempt.ExpiresAt,
            attempt.AssignedByUserId,
            attempt.ConcurrencyToken);
    }
}

public sealed class GetTaskAssignmentHistoryHandler : IRequestHandler<GetTaskAssignmentHistoryQuery, IEnumerable<TaskAssignmentAttemptDto>>
{
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;

    public GetTaskAssignmentHistoryHandler(ITaskAssignmentAttemptRepository attemptRepo)
    {
        _attemptRepo = attemptRepo;
    }

    public async Task<IEnumerable<TaskAssignmentAttemptDto>> Handle(GetTaskAssignmentHistoryQuery request, CancellationToken ct)
    {
        var attempts = await _attemptRepo.GetByTaskIdAsync(request.TaskId, ct);
        return attempts.Select(a => new TaskAssignmentAttemptDto(
            a.Id,
            a.TaskId,
            a.AssistantId,
            a.CollaborationId,
            a.AttemptNumber,
            a.Status.ToString(),
            a.AssignedAt,
            a.RespondedAt,
            a.AcceptedAt,
            a.RejectedAt,
            a.RejectionReason,
            a.ExpiresAt,
            a.AssignedByUserId,
            a.ConcurrencyToken));
    }
}

public sealed class GetAssistantWorkloadHandler : IRequestHandler<GetAssistantWorkloadQuery, AssistantWorkloadDto>
{
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly IConfiguration _config;

    public GetAssistantWorkloadHandler(ITaskAssignmentAttemptRepository attemptRepo, IConfiguration config)
    {
        _attemptRepo = attemptRepo;
        _config = config;
    }

    public async Task<AssistantWorkloadDto> Handle(GetAssistantWorkloadQuery request, CancellationToken ct)
    {
        int maxWorkload = _config.GetValue<int?>("AssistantWorkload:MaximumActiveAssignments") ?? 3;
        int currentWorkload = await _attemptRepo.GetActiveWorkloadCountAsync(request.AssistantId, ct);

        return new AssistantWorkloadDto(
            request.AssistantId,
            currentWorkload,
            maxWorkload,
            currentWorkload >= maxWorkload);
    }
}
