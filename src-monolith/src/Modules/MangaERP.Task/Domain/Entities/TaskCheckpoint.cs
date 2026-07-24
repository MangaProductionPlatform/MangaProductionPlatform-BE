using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Task.Domain.Entities;

public enum CheckpointStatus
{
    Upcoming,
    Met,
    Missed,
    Overdue
}

public sealed class TaskCheckpoint : Entity
{
    public Guid TaskId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int TargetPercent { get; private set; }
    public int OffsetMinutesFromAcceptance { get; private set; }
    public bool IsRequired { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;

    private TaskCheckpoint() { }

    public TaskCheckpoint(Guid taskId, string name, int targetPercent, int offsetMinutesFromAcceptance, bool isRequired = true)
    {
        if (taskId == Guid.Empty) throw new ArgumentException("TaskId is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Checkpoint name is required.");
        if (targetPercent < 1 || targetPercent > 100) throw new ArgumentException("Target percent must be between 1 and 100.");
        if (offsetMinutesFromAcceptance <= 0) throw new ArgumentException("Offset minutes must be positive.");

        TaskId = taskId;
        Name = name.Trim();
        TargetPercent = targetPercent;
        OffsetMinutesFromAcceptance = offsetMinutesFromAcceptance;
        IsRequired = isRequired;
    }

    public CheckpointStatus ComputeStatus(DateTime? acceptedAt, int currentProgressPercent, DateTime now)
    {
        if (currentProgressPercent >= TargetPercent)
            return CheckpointStatus.Met;

        if (!acceptedAt.HasValue)
            return CheckpointStatus.Upcoming;

        DateTime dueAt = acceptedAt.Value.AddMinutes(OffsetMinutesFromAcceptance);
        return now <= dueAt ? CheckpointStatus.Upcoming : CheckpointStatus.Missed;
    }
}
