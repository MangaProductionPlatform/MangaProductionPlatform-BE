namespace MangaERP.Shared.Domain.Entities;

public sealed class AuditEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Action { get; private set; } = string.Empty;
    public Guid ActorUserId { get; private set; }
    public string TargetType { get; private set; } = string.Empty;
    public Guid TargetId { get; private set; }
    public Guid? CollaborationId { get; private set; }
    public Guid? SeriesId { get; private set; }
    public Guid? TaskId { get; private set; }
    public DateTime Timestamp { get; private set; } = DateTime.UtcNow;
    public string? MetadataJson { get; private set; }

    private AuditEvent() { }

    public AuditEvent(string action, Guid actorUserId, string targetType, Guid targetId,
        Guid? collaborationId = null, Guid? seriesId = null, Guid? taskId = null, string? metadataJson = null)
    {
        if (string.IsNullOrWhiteSpace(action) || actorUserId == Guid.Empty || string.IsNullOrWhiteSpace(targetType))
            throw new ArgumentException("Action, ActorUserId, and TargetType are required.");

        Action = action.Trim();
        ActorUserId = actorUserId;
        TargetType = targetType.Trim();
        TargetId = targetId;
        CollaborationId = collaborationId;
        SeriesId = seriesId;
        TaskId = taskId;
        Timestamp = DateTime.UtcNow;
        MetadataJson = metadataJson;
    }
}
