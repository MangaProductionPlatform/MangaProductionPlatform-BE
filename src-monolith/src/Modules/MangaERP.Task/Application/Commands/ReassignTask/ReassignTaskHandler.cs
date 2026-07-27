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
    Guid NewAssistantId,
    Guid ActorUserId,
    string Reason,
    DateTime? Deadline = null,
    DateTime? ResponseDeadline = null,
    string? Description = null) : IRequest<AssignTaskResultDto>
{
    public ReassignTaskCommand(
        Guid taskId,
        Guid primaryAssistantId,
        Guid? backupAssistantId,
        Guid actorUserId,
        string reason,
        DateTime? responseDeadline = null,
        string? description = null)
        : this(taskId, primaryAssistantId, actorUserId, reason, null, responseDeadline, description) { }
}

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

            int maxWorkload = _config.GetValue<int?>("AssistantWorkload:MaximumActiveAssignments") ?? 3;

            // 3. Validate New Assistant (Active collaboration, SeriesAccessGrant, Workload)
            var newCollab = await _collabRepo.GetNonEndedCollaborationByAssistantAsync(request.NewAssistantId, ct)
                ?? throw new ConflictException("New assistant has no collaboration with this Mangaka.");

            if (newCollab.MangakaId != request.ActorUserId)
                throw new ConflictException("New assistant collaboration is with a different Mangaka.");

            if (newCollab.Status != CollaborationStatus.Active)
                throw new ConflictException($"Cannot assign task to assistant in collaboration status '{newCollab.Status}'.");

            var grant = await _grantRepo.GetActiveGrantAsync(newCollab.Id, series.Id, ct);
            if (grant == null)
                throw new ConflictException("New assistant does not have active series access for this series.");

            int newWorkload = await _attemptRepo.GetActiveWorkloadCountAsync(request.NewAssistantId, ct, excludeTaskId: task.Id);
            if (newWorkload >= maxWorkload)
                throw new ConflictException($"New assistant has reached maximum active task capacity ({maxWorkload}).");

            // Check if there is already a pending replacement attempt on this task
            var existingAttempts = await _attemptRepo.GetByTaskIdAsync(task.Id, ct);
            if (existingAttempts.Any(a => a.Status == TaskAssignmentAttemptStatus.PendingAcceptance))
                throw new ConflictException("Task already has a pending replacement assignment attempt.");

            DateTime now = DateTime.UtcNow;
            int maxAttemptNumber = await _attemptRepo.GetMaxAttemptNumberAsync(task.Id, ct);

            // Create replacement attempt (PendingAcceptance)
            int attemptNumber = maxAttemptNumber + 1;
            var replacementAttempt = TaskAssignmentAttempt.CreatePending(
                task.Id,
                request.NewAssistantId,
                newCollab.Id,
                attemptNumber,
                request.ActorUserId,
                expiresAt: request.ResponseDeadline,
                assignmentRole: "Direct",
                responseDeadline: request.ResponseDeadline,
                workDeadline: request.Deadline);

            await _attemptRepo.AddAsync(replacementAttempt, ct);

            // Preserve existing task data & WorkStartedAt.
            // Do NOT update AssignedAssistantId to new assistant before Accept.
            if (request.Deadline.HasValue)
            {
                task.Deadline = request.Deadline.Value;
            }
            if (!string.IsNullOrWhiteSpace(request.Description))
            {
                task.Description = request.Description;
            }
            task.ReassignmentReason = request.Reason;
            task.ReassignmentRequiredAt = now;

            await _taskRepo.UpdateAsync(task, ct);

            // Notify candidate new assistant
            await _notifications.NotifyCollaborationEventAsync(
                request.NewAssistantId,
                "TaskReassignmentRequested",
                "Task Reassignment Invitation",
                $"You have been invited to take over task #{task.PageNumber} in chapter '{chapter.Title}'.",
                task.Id,
                ct);

            await _attemptRepo.SaveChangesAsync(ct);

            if (transaction != null)
            {
                await transaction.CommitAsync(ct);
            }

            var attemptDto = MapToAttemptDto(replacementAttempt);
            return new AssignTaskResultDto(task.Id, attemptDto, null);
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
