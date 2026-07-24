using MangaERP.Shared.Domain.Abstractions;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Task.Domain.Entities;

public enum TaskAssignmentAttemptStatus
{
    PendingAcceptance,
    Accepted,
    Rejected,
    Expired,
    Cancelled
}

public class TaskAssignmentAttempt : AggregateRoot
{
    public Guid TaskId { get; private set; }
    public Guid AssistantId { get; private set; }
    public Guid CollaborationId { get; private set; }
    public int AttemptNumber { get; private set; }
    public TaskAssignmentAttemptStatus Status { get; private set; } = TaskAssignmentAttemptStatus.PendingAcceptance;
    public DateTime AssignedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public DateTime? DeclinedAt => RejectedAt;
    public string? RejectionReason { get; private set; }
    public string? DeclineReason => RejectionReason;
    public DateTime? ExpiresAt { get; private set; }
    public Guid AssignedByUserId { get; private set; }
    public string AssignmentRole { get; private set; } = "Primary"; // "Primary" | "BackupTakeover"
    public DateTime? ResponseDeadline { get; private set; }
    public DateTime? WorkDeadline { get; private set; }
    public Guid? PreviousAttemptId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    private TaskAssignmentAttempt() { }

    public static TaskAssignmentAttempt CreatePending(
        Guid taskId,
        Guid assistantId,
        Guid collaborationId,
        int attemptNumber,
        Guid assignedByUserId,
        DateTime? expiresAt = null,
        string assignmentRole = "Primary",
        DateTime? responseDeadline = null,
        DateTime? workDeadline = null,
        Guid? previousAttemptId = null)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("TaskId is required.", nameof(taskId));
        if (assistantId == Guid.Empty) throw new ArgumentException("AssistantId is required.", nameof(assistantId));
        if (collaborationId == Guid.Empty) throw new ArgumentException("CollaborationId is required.", nameof(collaborationId));
        if (attemptNumber <= 0) throw new ArgumentException("AttemptNumber must be > 0.", nameof(attemptNumber));
        if (assignedByUserId == Guid.Empty) throw new ArgumentException("AssignedByUserId is required.", nameof(assignedByUserId));

        return new TaskAssignmentAttempt
        {
            Id = Guid.NewGuid(),
            TaskId = taskId,
            AssistantId = assistantId,
            CollaborationId = collaborationId,
            AttemptNumber = attemptNumber,
            Status = TaskAssignmentAttemptStatus.PendingAcceptance,
            AssignedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt ?? responseDeadline,
            AssignedByUserId = assignedByUserId,
            AssignmentRole = string.IsNullOrWhiteSpace(assignmentRole) ? "Primary" : assignmentRole,
            ResponseDeadline = responseDeadline ?? expiresAt,
            WorkDeadline = workDeadline,
            PreviousAttemptId = previousAttemptId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    public void Accept(Guid actorId, DateTime now)
    {
        if (Status != TaskAssignmentAttemptStatus.PendingAcceptance)
            throw new InvalidOperationException($"Cannot accept assignment attempt in status '{Status}'.");
        if (actorId != AssistantId)
            throw new UnauthorizedAccessException("Only the assigned assistant can accept this assignment attempt.");
        if (ExpiresAt.HasValue && ExpiresAt.Value <= now)
            throw new InvalidOperationException("Assignment attempt has expired.");

        Status = TaskAssignmentAttemptStatus.Accepted;
        RespondedAt = now;
        AcceptedAt = now;
        UpdatedAt = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Reject(Guid actorId, string reason, DateTime now)
    {
        if (Status != TaskAssignmentAttemptStatus.PendingAcceptance)
            throw new InvalidOperationException($"Cannot reject assignment attempt in status '{Status}'.");
        if (actorId != AssistantId)
            throw new UnauthorizedAccessException("Only the assigned assistant can reject this assignment attempt.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required and cannot be empty.", nameof(reason));

        Status = TaskAssignmentAttemptStatus.Rejected;
        RespondedAt = now;
        RejectedAt = now;
        RejectionReason = reason.Trim();
        UpdatedAt = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Cancel(DateTime now, string? reason = null)
    {
        if (Status != TaskAssignmentAttemptStatus.PendingAcceptance)
            return;

        Status = TaskAssignmentAttemptStatus.Cancelled;
        RespondedAt = now;
        RejectionReason = reason;
        UpdatedAt = now;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Expire(DateTime now)
    {
        if (Status != TaskAssignmentAttemptStatus.PendingAcceptance)
            return;

        Status = TaskAssignmentAttemptStatus.Expired;
        RespondedAt = now;
        UpdatedAt = now;
        ConcurrencyToken = Guid.NewGuid();
    }
}
