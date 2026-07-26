using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Task.Application.Commands.TaskAssignment;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;

namespace MangaERP.Task.Application.Commands.ReassignTask;

public record ReassignTaskCommand(
    Guid TaskId,
    Guid PrimaryAssistantId,
    Guid? BackupAssistantId,
    Guid ActorUserId,
    string Reason,
    DateTime? ResponseDeadline = null,
    string? Note = null) : IRequest<AssignTaskResultDto>;

public class ReassignTaskHandler : IRequestHandler<ReassignTaskCommand, AssignTaskResultDto>
{
    private readonly IPageTaskRepository _taskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly ITaskAssignmentAttemptRepository _attemptRepo;
    private readonly INotificationService _notifications;
    private readonly IConfiguration _config;
    private readonly IDbContextProvider _dbContextProvider;

    public ReassignTaskHandler(
        IPageTaskRepository taskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo,
        ITaskAssignmentAttemptRepository attemptRepo,
        INotificationService notifications,
        IConfiguration config,
        IDbContextProvider dbContextProvider)
    {
        _taskRepo = taskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _collabRepo = collabRepo;
        _grantRepo = grantRepo;
        _attemptRepo = attemptRepo;
        _notifications = notifications;
        _config = config;
        _dbContextProvider = dbContextProvider;
    }

    public async Task<AssignTaskResultDto> Handle(ReassignTaskCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new ArgumentException("Reassignment reason is required.", nameof(request.Reason));

        var dbContext = _dbContextProvider?.GetDbContext() as DbContext;
        IDbContextTransaction? transaction = null;

        if (dbContext != null && dbContext.Database.CurrentTransaction == null && dbContext.Database.ProviderName != "Microsoft.EntityFrameworkCore.InMemory")
        {
            transaction = await dbContext.Database.BeginTransactionAsync(ct);
        }

        try
        {
            // 1. Load task, chapter, and series
            var task = await _taskRepo.GetByIdAsync(request.TaskId, ct)
                ?? throw new EntityNotFoundException("PageTask", request.TaskId);

            var chapter = await _chapterRepo.GetByIdAsync(task.ChapterId, ct)
                ?? throw new EntityNotFoundException("Chapter", task.ChapterId);

            var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
                ?? throw new EntityNotFoundException("MangaSeries", chapter.SeriesId);

            // 2. Authorize owner Mangaka
            if (series.AuthorId != request.ActorUserId)
                throw new UnauthorizedAccessException("Only the Mangaka who owns the series can reassign tasks.");

            // 5. Validate Primary and Backup are not the same person
            if (request.BackupAssistantId.HasValue && request.BackupAssistantId.Value == request.PrimaryAssistantId)
                throw new ConflictException("Primary and Backup assistant cannot be the same person.");

            int maxWorkload = _config.GetValue<int?>("AssistantWorkload:MaximumActiveAssignments") ?? 3;

            // 3, 6, 7, 8, 9. Validate Primary Assistant (Active collaboration, SeriesAccessGrant, Workload)
            var primaryCollab = await _collabRepo.GetNonEndedCollaborationByAssistantAsync(request.PrimaryAssistantId, ct)
                ?? throw new ConflictException("Primary assistant has no collaboration with this Mangaka.");

            if (primaryCollab.MangakaId != request.ActorUserId)
                throw new ConflictException("Primary assistant collaboration is with a different Mangaka.");

            if (primaryCollab.Status != CollaborationStatus.Active)
                throw new ConflictException($"Cannot assign task to primary assistant in collaboration status '{primaryCollab.Status}'.");

            var primaryGrant = await _grantRepo.GetActiveGrantAsync(primaryCollab.Id, series.Id, ct);
            if (primaryGrant == null)
                throw new ConflictException("Primary assistant does not have active series access for this series.");

            // Exclude current task workload because active attempts on this task will be superseded
            int primaryWorkload = await _attemptRepo.GetActiveWorkloadCountAsync(request.PrimaryAssistantId, ct, excludeTaskId: task.Id);
            if (primaryWorkload >= maxWorkload)
                throw new ConflictException($"Primary assistant has reached maximum active task capacity ({maxWorkload}).");

            // 4, 6, 7, 8, 9. Validate Optional Backup Assistant
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

                int backupWorkload = await _attemptRepo.GetActiveWorkloadCountAsync(request.BackupAssistantId.Value, ct, excludeTaskId: task.Id);
                if (backupWorkload >= maxWorkload)
                    throw new ConflictException($"Backup assistant has reached maximum active task capacity ({maxWorkload}).");
            }

            DateTime now = DateTime.UtcNow;

            // 11, 12. Supersede old active attempts and release old workload
            var activeAttempts = await _attemptRepo.GetByTaskIdAsync(task.Id, ct);
            foreach (var attempt in activeAttempts.Where(a => a.Status == TaskAssignmentAttemptStatus.PendingAcceptance || a.Status == TaskAssignmentAttemptStatus.Accepted))
            {
                attempt.Supersede(now, $"Superseded for reassignment: {request.Reason}");
                await _attemptRepo.UpdateAsync(attempt, ct);

                await _notifications.NotifyCollaborationEventAsync(
                    attempt.AssistantId,
                    "AssignmentSuperseded",
                    "Assignment Superseded",
                    $"Your assignment for task #{task.PageNumber} in chapter '{chapter.Title}' was superseded for reassignment.",
                    task.Id,
                    ct);
            }

            int maxAttemptNumber = await _attemptRepo.GetMaxAttemptNumberAsync(task.Id, ct);

            // 13, 15. Create new Primary attempt
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

            // 14, 15. Create optional Backup attempt
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

            // 16, 17. Set both to PendingAcceptance, reset WorkStartedAt = null, set task status to PendingAcceptance
            task.AssignPrimaryAndBackup(request.PrimaryAssistantId, request.BackupAssistantId, request.Note ?? request.Reason, null);
            task.CurrentAssignmentAttemptId = primaryAttempt.Id;
            task.ReassignmentReason = request.Reason;
            task.ReassignmentRequiredAt = now;
            task.WorkStartedAt = null;

            await _taskRepo.UpdateAsync(task, ct);

            // 18. Persist notifications
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

            // 19. Commit single SaveChanges within transaction
            await _attemptRepo.SaveChangesAsync(ct);

            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }

            var primaryDto = MapToAttemptDto(primaryAttempt);
            var backupDto = backupAttempt != null ? MapToAttemptDto(backupAttempt) : null;

            return new AssignTaskResultDto(task.Id, primaryDto, backupDto);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync(ct);
            }
            throw;
        }
        finally
        {
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
        }
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
