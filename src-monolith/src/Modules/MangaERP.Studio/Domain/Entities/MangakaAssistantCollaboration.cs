namespace MangaERP.Studio.Domain.Entities;

public enum CollaborationStatus
{
    Active,
    Suspended,
    EndingRequested,
    Ended
}

public enum CollaborationSuspensionMode
{
    SuspendNewAssignments,
    SuspendAllAccess
}

public enum CollaborationEventType
{
    CollaborationActivated,
    CollaborationSuspended,
    SuspensionModeChanged,
    CollaborationReactivated,
    EndingRequested,
    CollaborationEnded,
    AdminOverride
}

public sealed class MangakaAssistantCollaboration
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid MangakaId { get; private set; }
    public Guid AssistantId { get; private set; }
    public Guid InvitationId { get; private set; }
    public CollaborationStatus Status { get; private set; } = CollaborationStatus.Active;
    public CollaborationSuspensionMode? SuspensionMode { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? SuspendedAt { get; private set; }
    public string? SuspensionReason { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public string? EndReason { get; private set; }
    public Guid? TerminatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    private MangakaAssistantCollaboration() { }

    public MangakaAssistantCollaboration(Guid mangakaId, Guid assistantId, Guid invitationId, DateTime now)
    {
        if (mangakaId == Guid.Empty || assistantId == Guid.Empty || invitationId == Guid.Empty)
            throw new ArgumentException("Mangaka, Assistant, and invitation are required.");
        MangakaId = mangakaId;
        AssistantId = assistantId;
        InvitationId = invitationId;
        StartedAt = CreatedAt = UpdatedAt = now;
    }

    public void Suspend(CollaborationSuspensionMode mode, string reason, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Suspension reason is required.");
        if (Status != CollaborationStatus.Active) throw new InvalidOperationException("Only active collaborations can be suspended.");
        Status = CollaborationStatus.Suspended;
        SuspensionMode = mode;
        SuspendedAt = now;
        SuspensionReason = reason.Trim();
        Touch(now);
    }

    public void ChangeSuspensionMode(CollaborationSuspensionMode mode, string reason, DateTime now)
    {
        if (Status != CollaborationStatus.Suspended) throw new InvalidOperationException("Only suspended collaborations have a suspension mode.");
        if (mode == SuspensionMode) throw new InvalidOperationException("The collaboration is already in the requested suspension mode.");
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Suspension reason is required.");
        SuspensionMode = mode;
        SuspendedAt = now;
        SuspensionReason = reason.Trim();
        Touch(now);
    }

    public void Reactivate(DateTime now)
    {
        if (Status != CollaborationStatus.Suspended) throw new InvalidOperationException("Only suspended collaborations can be reactivated.");
        Status = CollaborationStatus.Active;
        SuspensionMode = null;
        SuspendedAt = null;
        SuspensionReason = null;
        Touch(now);
    }

    public void RequestEnding(DateTime now)
    {
        if (Status != CollaborationStatus.Active && Status != CollaborationStatus.Suspended)
            throw new InvalidOperationException("Only active or suspended collaborations can request ending.");
        Status = CollaborationStatus.EndingRequested;
        Touch(now);
    }

    public void End(string reason, Guid actorId, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(reason) || actorId == Guid.Empty) throw new ArgumentException("End reason and actor are required.");
        if (Status == CollaborationStatus.Ended) throw new InvalidOperationException("Collaboration is already ended.");
        if (Status != CollaborationStatus.EndingRequested && Status != CollaborationStatus.Active && Status != CollaborationStatus.Suspended)
            throw new InvalidOperationException("Collaboration cannot be ended from its current state.");
        Status = CollaborationStatus.Ended;
        EndedAt = now;
        EndReason = reason.Trim();
        TerminatedByUserId = actorId;
        SuspensionMode = null;
        Touch(now);
    }

    private void Touch(DateTime now)
    {
        UpdatedAt = now;
        ConcurrencyToken = Guid.NewGuid();
    }
}

public sealed class CollaborationEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid CollaborationId { get; private set; }
    public CollaborationEventType EventType { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public string? Reason { get; private set; }
    public string? DetailsJson { get; private set; }
    public string? CorrelationId { get; private set; }

    private CollaborationEvent() { }

    public CollaborationEvent(Guid collaborationId, CollaborationEventType eventType, Guid actorUserId,
        DateTime occurredAt, string? reason = null, string? detailsJson = null, string? correlationId = null)
    {
        CollaborationId = collaborationId;
        EventType = eventType;
        ActorUserId = actorUserId;
        OccurredAt = occurredAt;
        Reason = reason;
        DetailsJson = detailsJson;
        CorrelationId = correlationId;
    }
}
