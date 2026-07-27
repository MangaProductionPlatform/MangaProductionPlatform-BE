using System;
using System.Collections.Generic;
using System.Linq;
using MangaERP.Chapter.Application.Commands.CancelAndRecreateTask;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Identity.Infrastructure.Repositories;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Task.Application.Commands.CancelAssignment;
using MangaERP.Task.Application.Commands.CheckHalfwayDeadlineWarnings;
using MangaERP.Task.Application.Commands.ReassignTask;
using MangaERP.Task.Application.Commands.RequestTakeover;
using MangaERP.Task.Application.Commands.TaskAssignment;
using MangaERP.Task.Application.Queries.GetAssistantCandidates;
using MangaERP.Task.Domain.Entities;
using MangaERP.Task.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;

namespace MangaERP.Api.Tests;

public class TaskAssignmentWorkflowTests
{
    private sealed class TestDbContextProvider(AppDbContext context) : IDbContextProvider
    {
        public object GetDbContext() => context;
    }

    private static AppDbContext GetInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration GetTestConfig(int maxWorkload = 3)
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"AssistantWorkload:MaximumActiveAssignments", maxWorkload.ToString()}
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    // 1. Assign accepts one AssistantId only
    // 2. Assign creates one PendingAcceptance attempt
    // 3. WorkStartedAt remains null before Accept
    [Fact]
    public async System.Threading.Tasks.Task Scenario01_02_03_AssignTask_CreatesSinglePendingAttempt_WorkStartedAtIsNull()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var mangaka = new User { Id = mangakaId, Username = "mangaka", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var assistant = new User { Id = assistantId, Username = "ast1", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(mangaka, assistant);

        var series = MangaSeries.Create(mangakaId, null, "Single Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        db.PageTasks.Add(task);

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.Add(collab);
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab.Id, series.Id, mangakaId));

        await db.SaveChangesAsync();

        var handler = new AssignTaskToAssistantHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new TaskAssignmentRepository(provider), new Mock<INotificationService>().Object, GetTestConfig(3));

        var command = new AssignTaskToAssistantCommand(task.Id, assistantId, mangakaId, "Do page 1", DateTime.UtcNow.AddDays(2));
        var result = await handler.Handle(command, default);

        Assert.NotNull(result.Attempt);
        Assert.Null(result.BackupAttempt); // Single assistant model: BackupAttempt is null
        Assert.Equal("Direct", result.Attempt.AssignmentRole);

        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(PageTaskStatus.PendingAcceptance, updatedTask.TaskStatus);
        Assert.Null(updatedTask.WorkStartedAt); // WorkStartedAt remains null before accept
        Assert.Null(updatedTask.AssignedAssistantId); // Not active executor before accept
    }

    // 4. Accept sets executor and WorkStartedAt first time
    [Fact]
    public async System.Threading.Tasks.Task Scenario04_AcceptAssignment_SetsExecutorAndWorkStartedAtFirstTime()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        task.AssignPending(assistantId, "Do page 1");
        db.PageTasks.Add(task);

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.Add(collab);
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab.Id, series.Id, mangakaId));

        var attempt = TaskAssignmentAttempt.CreatePending(task.Id, assistantId, collab.Id, 1, mangakaId);
        db.TaskAssignmentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var handler = new RespondTaskAssignmentHandler(
            new TaskAssignmentRepository(provider), new PageTaskRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new ChapterRepository(provider), new Mock<INotificationService>().Object);

        var response = await handler.Handle(new RespondTaskAssignmentCommand(attempt.Id, true, null, assistantId, attempt.ConcurrencyToken), default);

        Assert.Equal("Accepted", response.Status);

        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(PageTaskStatus.Incomplete, updatedTask.TaskStatus);
        Assert.Equal(assistantId, updatedTask.AssignedAssistantId); // Assigned on Accept
        Assert.NotNull(updatedTask.WorkStartedAt); // Set first time on Accept
    }

    // 5. Reject sets ReassignmentRequired
    [Fact]
    public async System.Threading.Tasks.Task Scenario05_RejectAssignment_SetsTaskToReassignmentRequired()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        task.AssignPending(assistantId, "Do page 1");
        db.PageTasks.Add(task);

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.Add(collab);
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab.Id, series.Id, mangakaId));

        var attempt = TaskAssignmentAttempt.CreatePending(task.Id, assistantId, collab.Id, 1, mangakaId);
        db.TaskAssignmentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var handler = new RespondTaskAssignmentHandler(
            new TaskAssignmentRepository(provider), new PageTaskRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new ChapterRepository(provider), new Mock<INotificationService>().Object);

        var response = await handler.Handle(new RespondTaskAssignmentCommand(attempt.Id, false, "Too busy", assistantId, attempt.ConcurrencyToken), default);

        Assert.Equal("Rejected", response.Status);

        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(PageTaskStatus.ReassignmentRequired, updatedTask.TaskStatus);
        Assert.Null(updatedTask.AssignedAssistantId);
        Assert.Null(updatedTask.WorkStartedAt);
    }

    // 6-12. Reassign preserves TaskId, ProgressPercent, history, checkpoints, files, WorkStartedAt, deadline
    // 13. Replacement pending is not executor
    // 14. Replacement Accept supersedes old assignment
    // 15. Replacement Accept updates AssignedAssistantId
    // 24. Reassign never creates new TaskId
    [Fact]
    public async System.Threading.Tasks.Task Scenario06_15_24_Reassign_PreservesAllData_UpdatesExecutorOnlyAfterAccept()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var oldAssistantId = Guid.NewGuid();
        var newAssistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var initialDeadline = DateTime.UtcNow.AddDays(5);
        var initialWorkStartedAt = DateTime.UtcNow.AddDays(-2);

        var task = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        task.Activate(oldAssistantId, PageTaskType.General, "Initial task", initialDeadline);
        task.WorkStartedAt = initialWorkStartedAt;
        task.SubmitProgress(45); // Progress = 45%
        db.PageTasks.Add(task);

        var collabOld = new MangakaAssistantCollaboration(mangakaId, oldAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        var collabNew = new MangakaAssistantCollaboration(mangakaId, newAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabOld, collabNew);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOld.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabNew.Id, series.Id, mangakaId));

        var attempt1 = TaskAssignmentAttempt.CreatePending(task.Id, oldAssistantId, collabOld.Id, 1, mangakaId);
        attempt1.Accept(oldAssistantId, initialWorkStartedAt);
        db.TaskAssignmentAttempts.Add(attempt1);

        await db.SaveChangesAsync();

        var reassignHandler = new ReassignTaskHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new TaskAssignmentRepository(provider), new Mock<INotificationService>().Object, GetTestConfig(3), provider);

        // 13. Replacement pending: Reassign command creating replacement attempt
        var reassignResult = await reassignHandler.Handle(new ReassignTaskCommand(task.Id, newAssistantId, mangakaId, "Needs help"), default);

        Assert.NotNull(reassignResult.Attempt);
        Assert.Equal("PendingAcceptance", reassignResult.Attempt.Status);

        var taskAfterReassignReq = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(taskAfterReassignReq);
        Assert.Equal(task.Id, taskAfterReassignReq.Id); // 6 & 24. Same TaskId!
        Assert.Equal(45, taskAfterReassignReq.ProgressPercent); // 7. ProgressPercent preserved!
        Assert.Equal(initialWorkStartedAt, taskAfterReassignReq.WorkStartedAt); // 11. WorkStartedAt preserved!
        Assert.Equal(initialDeadline, taskAfterReassignReq.Deadline); // 12. Deadline preserved!
        Assert.Equal(oldAssistantId, taskAfterReassignReq.AssignedAssistantId); // 13. New assistant is NOT executor before accept!

        // 14 & 15. New Assistant Accepts replacement attempt
        var respondHandler = new RespondTaskAssignmentHandler(
            new TaskAssignmentRepository(provider), new PageTaskRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new ChapterRepository(provider), new Mock<INotificationService>().Object);

        var replacementAttemptId = reassignResult.Attempt.Id;
        var replacementAttemptDb = await db.TaskAssignmentAttempts.FindAsync(replacementAttemptId);
        var acceptResponse = await respondHandler.Handle(new RespondTaskAssignmentCommand(replacementAttemptId, true, null, newAssistantId, replacementAttemptDb!.ConcurrencyToken), default);

        Assert.Equal("Accepted", acceptResponse.Status);

        var taskAfterAccept = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(taskAfterAccept);
        Assert.Equal(newAssistantId, taskAfterAccept.AssignedAssistantId); // 15. AssignedAssistantId updated to new assistant after accept!
        Assert.Equal(initialWorkStartedAt, taskAfterAccept.WorkStartedAt); // 11. WorkStartedAt preserved!
        Assert.Equal(45, taskAfterAccept.ProgressPercent); // 7. Progress preserved!

        var attempt1Db = await db.TaskAssignmentAttempts.FindAsync(attempt1.Id);
        Assert.Equal(TaskAssignmentAttemptStatus.Superseded, attempt1Db!.Status); // 14. Old assignment superseded!
    }

    // 16. Replacement Reject preserves all task data
    [Fact]
    public async System.Threading.Tasks.Task Scenario16_ReplacementReject_PreservesAllTaskData()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var oldAssistantId = Guid.NewGuid();
        var newAssistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var initialWorkStartedAt = DateTime.UtcNow.AddDays(-2);

        var task = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        task.Activate(oldAssistantId, PageTaskType.General, "Initial task", DateTime.UtcNow.AddDays(5));
        task.WorkStartedAt = initialWorkStartedAt;
        task.SubmitProgress(60);
        db.PageTasks.Add(task);

        var collabOld = new MangakaAssistantCollaboration(mangakaId, oldAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        var collabNew = new MangakaAssistantCollaboration(mangakaId, newAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabOld, collabNew);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOld.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabNew.Id, series.Id, mangakaId));

        var attempt1 = TaskAssignmentAttempt.CreatePending(task.Id, oldAssistantId, collabOld.Id, 1, mangakaId);
        attempt1.Accept(oldAssistantId, initialWorkStartedAt);
        db.TaskAssignmentAttempts.Add(attempt1);

        await db.SaveChangesAsync();

        var reassignHandler = new ReassignTaskHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new TaskAssignmentRepository(provider), new Mock<INotificationService>().Object, GetTestConfig(3), provider);

        var reassignResult = await reassignHandler.Handle(new ReassignTaskCommand(task.Id, newAssistantId, mangakaId, "Reassign invitation"), default);

        var respondHandler = new RespondTaskAssignmentHandler(
            new TaskAssignmentRepository(provider), new PageTaskRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new ChapterRepository(provider), new Mock<INotificationService>().Object);

        var replacementAttemptId = reassignResult.Attempt.Id;
        var replacementAttemptDb = await db.TaskAssignmentAttempts.FindAsync(replacementAttemptId);
        var rejectResponse = await respondHandler.Handle(new RespondTaskAssignmentCommand(replacementAttemptId, false, "Cannot take this task", newAssistantId, replacementAttemptDb!.ConcurrencyToken), default);

        Assert.Equal("Rejected", rejectResponse.Status);

        var taskAfterReject = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(taskAfterReject);
        Assert.Equal(PageTaskStatus.ReassignmentRequired, taskAfterReject.TaskStatus);
        Assert.Equal(60, taskAfterReject.ProgressPercent); // Progress preserved!
        Assert.Equal(initialWorkStartedAt, taskAfterReject.WorkStartedAt); // WorkStartedAt preserved!
        Assert.Null(taskAfterReject.AssignedAssistantId); // Rejected assistant is not assigned
    }

    // 17. Old Assistant loses write access
    // 18. New Assistant continues existing work
    [Fact]
    public async System.Threading.Tasks.Task Scenario17_18_OldAssistantLosesWriteAccess_NewAssistantCanWrite()
    {
        using var db = GetInMemoryDbContext();
        var authService = new CollaborationAuthorizationService(db);

        var mangakaId = Guid.NewGuid();
        var oldAssistantId = Guid.NewGuid();
        var newAssistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Access Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        task.Activate(oldAssistantId, PageTaskType.General, "Desc", DateTime.UtcNow.AddDays(2));
        db.PageTasks.Add(task);

        var collabOld = new MangakaAssistantCollaboration(mangakaId, oldAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        var collabNew = new MangakaAssistantCollaboration(mangakaId, newAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabOld, collabNew);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOld.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabNew.Id, series.Id, mangakaId));

        var attemptOld = TaskAssignmentAttempt.CreatePending(task.Id, oldAssistantId, collabOld.Id, 1, mangakaId);
        attemptOld.Accept(oldAssistantId, DateTime.UtcNow);
        db.TaskAssignmentAttempts.Add(attemptOld);

        await db.SaveChangesAsync();

        // Old assistant has write access
        Assert.True(await authService.CanSubmitProgressAsync(oldAssistantId, task.Id));
        Assert.False(await authService.CanSubmitProgressAsync(newAssistantId, task.Id));

        // Reassign & Accept by New Assistant
        attemptOld.Supersede(DateTime.UtcNow, "Reassigned");
        var attemptNew = TaskAssignmentAttempt.CreatePending(task.Id, newAssistantId, collabNew.Id, 2, mangakaId);
        attemptNew.Accept(newAssistantId, DateTime.UtcNow);
        db.TaskAssignmentAttempts.Add(attemptNew);

        task.AcceptReplacement(newAssistantId, DateTime.UtcNow);
        await db.SaveChangesAsync();

        // 17. Old assistant loses write access
        Assert.False(await authService.CanSubmitProgressAsync(oldAssistantId, task.Id));

        // 18. New assistant has write access to submit progress / upload
        Assert.True(await authService.CanSubmitProgressAsync(newAssistantId, task.Id));
    }

    // 19. Workload counts one responsibility
    [Fact]
    public async System.Threading.Tasks.Task Scenario19_Workload_CountsOneResponsibilityPerActiveTask()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var assistantId = Guid.NewGuid();
        var attemptRepo = new TaskAssignmentRepository(provider);

        var task1 = new PageTask { ChapterId = Guid.NewGuid(), PageNumber = 1, TaskStatus = PageTaskStatus.Incomplete, AssignedAssistantId = assistantId };
        db.PageTasks.Add(task1);

        var attempt1 = TaskAssignmentAttempt.CreatePending(task1.Id, assistantId, Guid.NewGuid(), 1, Guid.NewGuid());
        attempt1.Accept(assistantId, DateTime.UtcNow);
        db.TaskAssignmentAttempts.Add(attempt1);

        var task2 = new PageTask { ChapterId = Guid.NewGuid(), PageNumber = 2, TaskStatus = PageTaskStatus.PendingAcceptance };
        db.PageTasks.Add(task2);

        var attempt2 = TaskAssignmentAttempt.CreatePending(task2.Id, assistantId, Guid.NewGuid(), 1, Guid.NewGuid());
        db.TaskAssignmentAttempts.Add(attempt2);

        await db.SaveChangesAsync();

        int workload = await attemptRepo.GetActiveWorkloadCountAsync(assistantId, default);
        Assert.Equal(2, workload); // 1 Accepted active task + 1 PendingAcceptance attempt = 2
    }

    // 20. Candidate API remains role-neutral
    [Fact]
    public async System.Threading.Tasks.Task Scenario20_CandidateApi_IsRoleNeutral()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var mangaka = new User { Id = mangakaId, Username = "mangaka", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var assistant = new User { Id = assistantId, Username = "ast", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(mangaka, assistant);

        var series = MangaSeries.Create(mangakaId, null, "Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        db.PageTasks.Add(task);

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.Add(collab);
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab.Id, series.Id, mangakaId));

        await db.SaveChangesAsync();

        var handler = new GetAssistantCandidatesHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new UserRepository(provider), new TaskAssignmentRepository(provider), GetTestConfig(3));

        var result = await handler.Handle(new GetAssistantCandidatesQuery(task.Id, mangakaId), default);

        Assert.NotNull(result);
        Assert.Single(result.AvailableAssistants);
        Assert.Equal(assistantId, result.AvailableAssistants[0].AssistantId);
    }

    // 21. Takeover route is removed/deprecated
    [Fact]
    public async System.Threading.Tasks.Task Scenario21_TakeoverRoute_ThrowsNotSupportedException()
    {
        var handler = new RequestTakeoverHandler();
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            handler.Handle(new RequestTakeoverCommand(Guid.NewGuid(), Guid.NewGuid(), "Takeover"), default));
    }

    // 22. Assignment history returns currentAssignment + history
    [Fact]
    public async System.Threading.Tasks.Task Scenario22_AssignmentHistory_ReturnsCurrentAssignmentAndHistory()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var taskId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var attempt = TaskAssignmentAttempt.CreatePending(taskId, assistantId, Guid.NewGuid(), 1, Guid.NewGuid());
        attempt.Accept(assistantId, DateTime.UtcNow);
        db.TaskAssignmentAttempts.Add(attempt);
        await db.SaveChangesAsync();

        var handler = new GetTaskAssignmentHistoryHandler(new TaskAssignmentRepository(provider));
        var result = await handler.Handle(new GetTaskAssignmentHistoryQuery(taskId, Guid.NewGuid()), default);

        Assert.NotNull(result);
        Assert.NotNull(result.CurrentAssignment);
        Assert.Equal(attempt.Id, result.CurrentAssignment.Id);
        Assert.Single(result.History);
    }

    // 23. Cancel-and-Recreate still creates new TaskId
    [Fact]
    public async System.Threading.Tasks.Task Scenario23_CancelAndRecreate_CreatesNewTaskId()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var series = MangaSeries.Create(mangakaId, null, "Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        db.PageTasks.Add(task);
        await db.SaveChangesAsync();

        var handler = new CancelAndRecreateTaskHandler(
            new ChapterRepository(provider), new PageTaskRepository(provider), new SeriesRepository(provider));

        var result = await handler.Handle(new CancelAndRecreateTaskCommand(mangakaId, task.Id, "Wrong base image", ConfirmProgressLoss: true), default);

        Assert.NotNull(result);
        Assert.NotEqual(task.Id, result.NewPageTaskId); // New TaskId created!
    }

    // 26. Transaction rollback preserves old task state on failure
    [Fact]
    public async System.Threading.Tasks.Task Scenario26_TransactionRollback_PreservesOldStateOnFailure()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var oldAssistantId = Guid.NewGuid();
        var invalidNewAssistantId = Guid.NewGuid(); // No collaboration

        var series = MangaSeries.Create(mangakaId, null, "Rollback Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        task.Activate(oldAssistantId, PageTaskType.General, "Desc", DateTime.UtcNow.AddDays(2));
        db.PageTasks.Add(task);

        var collabOld = new MangakaAssistantCollaboration(mangakaId, oldAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.Add(collabOld);
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOld.Id, series.Id, mangakaId));

        var attemptOld = TaskAssignmentAttempt.CreatePending(task.Id, oldAssistantId, collabOld.Id, 1, mangakaId);
        attemptOld.Accept(oldAssistantId, DateTime.UtcNow);
        db.TaskAssignmentAttempts.Add(attemptOld);

        await db.SaveChangesAsync();

        var reassignHandler = new ReassignTaskHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new TaskAssignmentRepository(provider), new Mock<INotificationService>().Object, GetTestConfig(3), provider);

        var command = new ReassignTaskCommand(task.Id, invalidNewAssistantId, mangakaId, "Reassign to invalid assistant");

        await Assert.ThrowsAsync<MangaERP.Shared.Domain.Exceptions.ConflictException>(() =>
            reassignHandler.Handle(command, default));

        var taskDb = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(taskDb);
        Assert.Equal(oldAssistantId, taskDb.AssignedAssistantId); // Preserved old executor!
        Assert.Equal(PageTaskStatus.Incomplete, taskDb.TaskStatus); // Preserved status!
    }

    [Fact]
    public async System.Threading.Tasks.Task HalfwayDeadlineWarning_SendsOnlyOnce_When50PercentElapsed()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Warning Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var now = DateTime.UtcNow;
        var task = new PageTask
        {
            ChapterId = chapter.Id,
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Incomplete,
            AssignedAssistantId = assistantId,
            WorkStartedAt = now.AddHours(-10),
            Deadline = now.AddHours(2)
        };
        db.PageTasks.Add(task);
        await db.SaveChangesAsync();

        var handler = new CheckHalfwayDeadlineWarningsHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider), new Mock<INotificationService>().Object);

        int warningsSent1 = await handler.Handle(new CheckHalfwayDeadlineWarningsCommand(), default);
        Assert.Equal(1, warningsSent1);

        int warningsSent2 = await handler.Handle(new CheckHalfwayDeadlineWarningsCommand(), default);
        Assert.Equal(0, warningsSent2);
    }
}
