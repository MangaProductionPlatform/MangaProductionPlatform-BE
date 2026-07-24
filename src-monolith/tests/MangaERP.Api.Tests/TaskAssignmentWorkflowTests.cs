using MangaERP.Task.Domain.Entities;
using MangaERP.Chapter.Domain.Entities;

namespace MangaERP.Api.Tests;

public class TaskAssignmentWorkflowTests
{
    [Fact]
    public void TaskAssignmentAttempt_CreatePending_SetsInitialProperties()
    {
        var taskId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var collaborationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(24);

        var attempt = TaskAssignmentAttempt.CreatePending(
            taskId, assistantId, collaborationId, attemptNumber: 1, actorId, expiresAt: expiresAt);

        Assert.Equal(taskId, attempt.TaskId);
        Assert.Equal(assistantId, attempt.AssistantId);
        Assert.Equal(TaskAssignmentAttemptStatus.PendingAcceptance, attempt.Status);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Null(attempt.AcceptedAt);
        Assert.Null(attempt.DeclinedAt);
    }

    [Fact]
    public void TaskAssignmentAttempt_Accept_SetsAcceptedAtAndStatus()
    {
        var taskId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var collaborationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var attempt = TaskAssignmentAttempt.CreatePending(
            taskId, assistantId, collaborationId, attemptNumber: 1, actorId, expiresAt: now.AddHours(24));

        attempt.Accept(assistantId, now);

        Assert.Equal(TaskAssignmentAttemptStatus.Accepted, attempt.Status);
        Assert.Equal(now, attempt.AcceptedAt);
        Assert.Equal(now, attempt.RespondedAt);
    }

    [Fact]
    public void TaskAssignmentAttempt_Reject_SetsDeclinedAtAndReason()
    {
        var taskId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var collaborationId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var attempt = TaskAssignmentAttempt.CreatePending(
            taskId, assistantId, collaborationId, attemptNumber: 1, actorId, expiresAt: now.AddHours(24));

        attempt.Reject(assistantId, "Too busy with other tasks", now);

        Assert.Equal(TaskAssignmentAttemptStatus.Rejected, attempt.Status);
        Assert.Equal(now, attempt.DeclinedAt);
        Assert.Equal("Too busy with other tasks", attempt.DeclineReason);
    }

    [Fact]
    public void PageTask_MarkReassignmentRequired_TransitionsStatus()
    {
        var task = PageTask.CreatePending(Guid.NewGuid(), 1, "http://example.com/base.png");
        task.MarkReassignmentRequired("All candidate assistants declined");

        Assert.Equal(PageTaskStatus.ReassignmentRequired, task.TaskStatus);
        Assert.Equal("All candidate assistants declined", task.ReassignmentReason);
        Assert.NotNull(task.ReassignmentRequiredAt);
    }
}
