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
    Guid PrimaryAssistantId,
    Guid? BackupAssistantId,
    Guid ActorUserId,
    string? Description = null,
    DateTime? Deadline = null,
    TimeSpan? Duration = null,
    DateTime? ResponseDeadline = null) : IRequest<AssignTaskResultDto>
{
    // Backwards compatibility constructor
    public AssignTaskToAssistantCommand(
        Guid taskId,
        Guid assistantId,
        Guid actorUserId,
        string? description,
        DateTime? deadline,
        TimeSpan? duration)
        : this(taskId, assistantId, null, actorUserId, description, deadline, duration, null) { }
}

public record AssignTaskResultDto(
    Guid TaskId,
    TaskAssignmentAttemptDto PrimaryAttempt,
    TaskAssignmentAttemptDto? BackupAttempt);

public record RespondTaskAssignmentCommand(
    Guid AttemptId,
    bool Accept,
    string? RejectionReason,
    Guid ActorUserId,
    Guid ExpectedConcurrencyToken) : IRequest<TaskAssignmentAttemptDto>;

public record GetTaskAssignmentHistoryQuery(Guid TaskId, Guid ActorUserId) : IRequest<TaskAssignmentHistoryResponseDto>;
public record GetAssistantWorkloadQuery(Guid AssistantId, Guid ActorUserId) : IRequest<AssistantWorkloadDto>;

public record TaskAssignmentAttemptDto(
    Guid Id,
    Guid TaskId,
    Guid AssistantId,
    Guid CollaborationId,
    int AttemptNumber,
    string Status,
    string AssignmentRole,
    DateTime AssignedAt,
    DateTime? RespondedAt,
    DateTime? AcceptedAt,
    DateTime? RejectedAt,
    string? RejectionReason,
    DateTime? ExpiresAt,
    Guid AssignedByUserId,
    Guid ConcurrencyToken);

public record TaskAssignmentHistoryResponseDto(
    TaskAssignmentAttemptDto? CurrentPrimary,
    TaskAssignmentAttemptDto? CurrentBackup,
    IEnumerable<TaskAssignmentAttemptDto> History);

public record AssistantWorkloadDto(
    Guid AssistantId,
    int CurrentActiveAssignments,
    int MaximumActiveAssignments,
    bool IsCapacityReached);

public sealed class AssignTaskToAssistantHandler : IRequestHandler<AssignTaskToAssistantCommand, AssignTaskResultDto>
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

    public async Task<AssignTaskResultDto> Handle(AssignTaskToAssistantCommand request, CancellationToken ct)
    {
        var task = await _taskRepo.GetByIdAsync(request.TaskId, ct)
            ?? throw new EntityNotFoundException("PageTask", request.TaskId);

        var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
            ?? throw new EntityNotFoundException("Chapter", task.ChapterId);

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new EntityNotFoundException("MangaSeries", chapter.SeriesId);

        if (series.AuthorId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the Mangaka who owns the series can assign tasks.");

        if (request.BackupAssistantId.HasValue && request.BackupAssistantId.Value == request.PrimaryAssistantId)
            throw new ConflictException("Primary and Backup assistant cannot be the same person.");

        int maxWorkload = _config.GetValue<int?>("AssistantWorkload:MaximumActiveAssignments") ?? 3;

        // 1. Validate Primary Assistant
        var primaryCollab = await _collabRepo.GetNonEndedCollaborationByAssistantAsync(request.PrimaryAssistantId, ct)
            ?? throw new ConflictException("Primary assistant has no collaboration with this Mangaka.");

        if (primaryCollab.MangakaId != request.ActorUserId)
            throw new ConflictException("Primary assistant collaboration is with a different Mangaka.");

        if (primaryCollab.Status != CollaborationStatus.Active)
            throw new ConflictException($"Cannot assign task to primary assistant in collaboration status '{primaryCollab.Status}'.");

        var primaryGrant = await _grantRepo.GetActiveGrantAsync(primaryCollab.Id, series.Id, ct);
        if (primaryGrant == null)
            throw new ConflictException("Primary assistant does not have active series access for this series.");

        int primaryWorkload = await _attemptRepo.GetActiveWorkloadCountAsync(request.PrimaryAssistantId, ct);
        if (primaryWorkload >= maxWorkload)
            throw new ConflictException($"Primary assistant has reached maximum active task capacity ({maxWorkload}).");

        // 2. Validate Optional Backup Assistant
        MangakaAssistantCollaboration? backupCollab = null;
        if (request.BackupAssistantId.HasValue)
        {
            backupCollab = await _collabRepo.GetNonEndedCollaborationByAssistantAsync(request.BackupAssistantId.Value, ct)
                ?? throw new ConflictException("Backup assistant has no collaboration with this Mangaka.");

            if (backupCollab.MangakaId != request.ActorUserId)
                throw new ConflictException("Backup assistant collaboration is with a different Mangaka.");

            if (backupCollab.Status != CollaborationStatus.Active)
                throw new ConflictException($"Cannot assign task to backup assistant in collaboration status '{backupCollab.Status}'.");

            var backupGrant = await _grantRepo.GetActiveGrantAsync(backupCollab.Id, series.Id, ct);
            if (backupGrant == null)
                throw new ConflictException("Backup assistant does not have active series access for this series.");

            int backupWorkload = await _attemptRepo.GetActiveWorkloadCountAsync(request.BackupAssistantId.Value, ct);
            if (backupWorkload >= maxWorkload)
                throw new ConflictException($"Backup assistant has reached maximum active task capacity ({maxWorkload}).");
        }

        // 3. Conflict checks on Task
        var activeAttempts = await _attemptRepo.GetByTaskIdAsync(task.Id, ct);
        if (activeAttempts.Any(a => a.AssignmentRole == "Primary" && (a.Status == TaskAssignmentAttemptStatus.PendingAcceptance || a.Status == TaskAssignmentAttemptStatus.Accepted)))
            throw new ConflictException("Task already has an active Primary assignment attempt.");

        if (request.BackupAssistantId.HasValue && activeAttempts.Any(a => a.AssignmentRole == "Backup" && (a.Status == TaskAssignmentAttemptStatus.PendingAcceptance || a.Status == TaskAssignmentAttemptStatus.Accepted)))
            throw new ConflictException("Task already has an active Backup assignment attempt.");

        int maxAttemptNumber = await _attemptRepo.GetMaxAttemptNumberAsync(task.Id, ct);

        // 4. Create Primary Attempt
        int primaryAttemptNumber = maxAttemptNumber + 1;
        var primaryAttempt = TaskAssignmentAttempt.CreatePending(
            task.Id,
            request.PrimaryAssistantId,
            primaryCollab.Id,
            primaryAttemptNumber,
            request.ActorUserId,
            expiresAt: request.ResponseDeadline,
            assignmentRole: "Primary",
            responseDeadline: request.ResponseDeadline);

        await _attemptRepo.AddAsync(primaryAttempt, ct);

        // 5. Create Backup Attempt if provided
        TaskAssignmentAttempt? backupAttempt = null;
        if (request.BackupAssistantId.HasValue && backupCollab != null)
        {
            int backupAttemptNumber = maxAttemptNumber + 2;
            backupAttempt = TaskAssignmentAttempt.CreatePending(
                task.Id,
                request.BackupAssistantId.Value,
                backupCollab.Id,
                backupAttemptNumber,
                request.ActorUserId,
                expiresAt: request.ResponseDeadline,
                assignmentRole: "Backup",
                responseDeadline: request.ResponseDeadline);

            await _attemptRepo.AddAsync(backupAttempt, ct);
        }

        // 6. Update Task state
        task.AssignPrimaryAndBackup(request.PrimaryAssistantId, request.BackupAssistantId, request.Description, request.Deadline);
        task.CurrentAssignmentAttemptId = primaryAttempt.Id;

        await _taskRepo.UpdateAsync(task, ct);
        await _attemptRepo.SaveChangesAsync(ct);

        // 7. Send Notifications
        await _notifications.NotifyCollaborationEventAsync(
            request.PrimaryAssistantId,
            "TaskAssigned",
            "New Task Assigned (Primary)",
            $"You have been assigned as Primary assistant for task #{task.PageNumber} in chapter '{chapter.Title}'.",
            task.Id,
            ct);

        if (request.BackupAssistantId.HasValue)
        {
            await _notifications.NotifyCollaborationEventAsync(
                request.BackupAssistantId.Value,
                "BackupTaskAssigned",
                "New Task Assigned (Backup)",
                $"You have been assigned as Backup assistant for task #{task.PageNumber} in chapter '{chapter.Title}'.",
                task.Id,
                ct);
        }

        var primaryDto = MapToAttemptDto(primaryAttempt);
        var backupDto = backupAttempt != null ? MapToAttemptDto(backupAttempt) : null;

        return new AssignTaskResultDto(task.Id, primaryDto, backupDto);
    }

    private static TaskAssignmentAttemptDto MapToAttemptDto(TaskAssignmentAttempt attempt)
    {
        return new TaskAssignmentAttemptDto(
            attempt.Id,
            attempt.TaskId,
            attempt.AssistantId,
            attempt.CollaborationId,
            attempt.AttemptNumber,
            attempt.Status.ToString(),
            attempt.AssignmentRole,
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
        else if (attempt.AssignmentRole == "Backup")
        {
            if (request.Accept)
            {
                if (collaboration.Status != CollaborationStatus.Active)
                    throw new ConflictException($"Cannot accept assignment when collaboration is in status '{collaboration.Status}'.");

                var grant = await _grantRepo.GetActiveGrantAsync(collaboration.Id, chapter.SeriesId, ct);
                if (grant == null)
                    throw new ConflictException("Cannot accept assignment because series access grant is no longer active.");

                attempt.Accept(request.ActorUserId, now);
                // Backup remains confirmed standby, Primary remains assigned executor
                await _attemptRepo.UpdateAsync(attempt, ct);
                await _attemptRepo.SaveChangesAsync(ct);

                await _notifications.NotifyCollaborationEventAsync(
                    collaboration.MangakaId,
                    "BackupAssignmentAccepted",
                    "Backup Assignment Accepted",
                    $"Backup assistant accepted standby assignment for task #{task.PageNumber} in chapter '{chapter.Title}'.",
                    task.Id,
                    ct);
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.RejectionReason))
                    throw new ArgumentException("Rejection reason is required when rejecting an assignment.");

                attempt.Reject(request.ActorUserId, request.RejectionReason, now);
                task.BackupAssistantId = null;

                await _attemptRepo.UpdateAsync(attempt, ct);
                await _taskRepo.UpdateAsync(task, ct);
                await _attemptRepo.SaveChangesAsync(ct);

                await _notifications.NotifyCollaborationEventAsync(
                    collaboration.MangakaId,
                    "BackupAssignmentRejected",
                    "Backup Assignment Rejected",
                    $"Backup assistant rejected assignment for task #{task.PageNumber} in chapter '{chapter.Title}'. Reason: {request.RejectionReason.Trim()}",
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
                $"Primary assistant accepted task #{task.PageNumber} in chapter '{chapter.Title}'.",
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
                $"Primary assistant rejected task #{task.PageNumber} in chapter '{chapter.Title}'. Reason: {request.RejectionReason.Trim()}",
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
            attempt.AssignmentRole,
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

public sealed class GetTaskAssignmentHistoryHandler : IRequestHandler<GetTaskAssignmentHistoryQuery, TaskAssignmentHistoryResponseDto>
{
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;

    public GetTaskAssignmentHistoryHandler(ITaskAssignmentAttemptRepository attemptRepo)
    {
        _attemptRepo = attemptRepo;
    }

    public async Task<TaskAssignmentHistoryResponseDto> Handle(GetTaskAssignmentHistoryQuery request, CancellationToken ct)
    {
        var attempts = (await _attemptRepo.GetByTaskIdAsync(request.TaskId, ct)).ToList();

        var dtoList = attempts.Select(a => new TaskAssignmentAttemptDto(
            a.Id,
            a.TaskId,
            a.AssistantId,
            a.CollaborationId,
            a.AttemptNumber,
            a.Status.ToString(),
            a.AssignmentRole,
            a.AssignedAt,
            a.RespondedAt,
            a.AcceptedAt,
            a.RejectedAt,
            a.RejectionReason,
            a.ExpiresAt,
            a.AssignedByUserId,
            a.ConcurrencyToken)).ToList();

        var currentPrimary = dtoList.FirstOrDefault(a => a.AssignmentRole == "Primary" && (a.Status == "PendingAcceptance" || a.Status == "Accepted"));
        var currentBackup = dtoList.FirstOrDefault(a => (a.AssignmentRole == "Backup" || a.AssignmentRole == "BackupTakeover") && (a.Status == "PendingAcceptance" || a.Status == "Accepted"));

        return new TaskAssignmentHistoryResponseDto(currentPrimary, currentBackup, dtoList);
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
