using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Task.Domain.Entities;

public sealed class TaskProgressUpdate : Entity
{
    public Guid TaskId { get; private set; }
    public Guid AssignmentAttemptId { get; private set; }
    public Guid AssistantId { get; private set; }
    public int ProgressPercent { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private TaskProgressUpdate() { }

    public TaskProgressUpdate(Guid taskId, Guid assignmentAttemptId, Guid assistantId, int progressPercent, string? note, Guid createdByUserId)
    {
        if (taskId == Guid.Empty || assignmentAttemptId == Guid.Empty || assistantId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Required identifiers must not be empty.");

        if (progressPercent < 0 || progressPercent > 100)
            throw new ArgumentException("Progress percent must be between 0 and 100.");

        TaskId = taskId;
        AssignmentAttemptId = assignmentAttemptId;
        AssistantId = assistantId;
        ProgressPercent = progressPercent;
        Note = note?.Trim();
        CreatedAt = DateTime.UtcNow;
        CreatedByUserId = createdByUserId;
    }
}
