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

    [Fact]
    public async System.Threading.Tasks.Task CandidateApi_ReturnsAvailableAndUnavailableCandidates_WithCorrectCodesAndSorting()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var assistant1Id = Guid.NewGuid(); // Available
        var assistant2Id = Guid.NewGuid(); // Workload limit reached

        var mangaka = new User { Id = mangakaId, Username = "mangaka", FullName = "Mangaka One", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var assistant1 = new User { Id = assistant1Id, Username = "ast1", FullName = "Assistant One", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var assistant2 = new User { Id = assistant2Id, Username = "ast2", FullName = "Assistant Two", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };

        db.Users.AddRange(mangaka, assistant1, assistant2);

        var series = MangaSeries.Create(mangakaId, null, "Candidate Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = new PageTask { ChapterId = chapter.Id, PageNumber = 1, TaskStatus = PageTaskStatus.Pending };
        db.PageTasks.Add(task);

        // Collaborations
        var collab1 = new MangakaAssistantCollaboration(mangakaId, assistant1Id, Guid.NewGuid(), DateTime.UtcNow);
        var collab2 = new MangakaAssistantCollaboration(mangakaId, assistant2Id, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collab1, collab2);

        // Grants
        var grant1 = SeriesAccessGrant.Create(collab1.Id, series.Id, mangakaId);
        var grant2 = SeriesAccessGrant.Create(collab2.Id, series.Id, mangakaId);
        db.SeriesAccessGrants.AddRange(grant1, grant2);

        // Assistant 2 has 3 active tasks (workload maxed out)
        for (int i = 0; i < 3; i++)
        {
            var otherTask = new PageTask { ChapterId = chapter.Id, PageNumber = i + 2, TaskStatus = PageTaskStatus.Incomplete, AssignedAssistantId = assistant2Id, PrimaryAssistantId = assistant2Id };
            db.PageTasks.Add(otherTask);
            var attempt = TaskAssignmentAttempt.CreatePending(otherTask.Id, assistant2Id, collab2.Id, 1, mangakaId);
            attempt.Accept(assistant2Id, DateTime.UtcNow);
            db.TaskAssignmentAttempts.Add(attempt);
        }

        await db.SaveChangesAsync();

        var taskRepo = new PageTaskRepository(provider);
        var chapterRepo = new ChapterRepository(provider);
        var seriesRepo = new SeriesRepository(provider);
        var studioRepo = new StudioInvitationRepository(provider);
        var grantRepo = new SeriesAccessGrantRepository(provider);
        var userRepo = new UserRepository(provider);
        var attemptRepo = new TaskAssignmentRepository(provider);
        var config = GetTestConfig(3);

        var handler = new GetAssistantCandidatesHandler(
            taskRepo, chapterRepo, seriesRepo, studioRepo, grantRepo, userRepo, attemptRepo, config);

        // Act
        var query = new GetAssistantCandidatesQuery(task.Id, mangakaId);
        var result = await handler.Handle(query, default);

        // Assert
        Assert.Single(result.AvailableAssistants);
        Assert.Equal(assistant1Id, result.AvailableAssistants[0].AssistantId);
        Assert.True(result.AvailableAssistants[0].IsAvailable);
        Assert.Equal("Available", result.AvailableAssistants[0].AvailabilityCode);

        Assert.Single(result.UnavailableAssistants);
        Assert.Equal(assistant2Id, result.UnavailableAssistants[0].AssistantId);
        Assert.False(result.UnavailableAssistants[0].IsAvailable);
        Assert.Equal("WorkloadLimitReached", result.UnavailableAssistants[0].AvailabilityCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task AssignTask_AtomicPrimaryAndBackup_CreatesBothAttemptsAndUpdatesTask()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var backupId = Guid.NewGuid();

        var mangaka = new User { Id = mangakaId, Username = "mangaka", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var primary = new User { Id = primaryId, Username = "primary", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var backup = new User { Id = backupId, Username = "backup", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(mangaka, primary, backup);

        var series = MangaSeries.Create(mangakaId, null, "Atomic Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = new PageTask { ChapterId = chapter.Id, PageNumber = 1, TaskStatus = PageTaskStatus.Pending };
        db.PageTasks.Add(task);

        var collab1 = new MangakaAssistantCollaboration(mangakaId, primaryId, Guid.NewGuid(), DateTime.UtcNow);
        var collab2 = new MangakaAssistantCollaboration(mangakaId, backupId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collab1, collab2);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab1.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab2.Id, series.Id, mangakaId));

        await db.SaveChangesAsync();

        var taskRepo = new PageTaskRepository(provider);
        var chapterRepo = new ChapterRepository(provider);
        var seriesRepo = new SeriesRepository(provider);
        var studioRepo = new StudioInvitationRepository(provider);
        var grantRepo = new SeriesAccessGrantRepository(provider);
        var attemptRepo = new TaskAssignmentRepository(provider);
        var notificationsMock = new Mock<MangaERP.Shared.Application.Ports.INotificationService>();
        var config = GetTestConfig(3);

        var handler = new AssignTaskToAssistantHandler(
            taskRepo, chapterRepo, seriesRepo, studioRepo, grantRepo, attemptRepo, notificationsMock.Object, config);

        var command = new AssignTaskToAssistantCommand(
            task.Id, primaryId, backupId, mangakaId, "Do page 1 artwork", DateTime.UtcNow.AddDays(2));

        // Act
        var result = await handler.Handle(command, default);

        // Assert
        Assert.NotNull(result.PrimaryAttempt);
        Assert.NotNull(result.BackupAttempt);

        Assert.Equal("Primary", result.PrimaryAttempt.AssignmentRole);
        Assert.Equal("Backup", result.BackupAttempt.AssignmentRole);

        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(primaryId, updatedTask.PrimaryAssistantId);
        Assert.Equal(backupId, updatedTask.BackupAssistantId);
        Assert.Equal(PageTaskStatus.PendingAcceptance, updatedTask.TaskStatus);
    }

    [Fact]
    public async System.Threading.Tasks.Task PrimaryReject_WhenBackupAccepted_DoesNotAutoTakeover_BackupStaysStandby()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var primaryId = Guid.NewGuid();
        var backupId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Reject Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = new PageTask
        {
            ChapterId = chapter.Id,
            PageNumber = 1,
            TaskStatus = PageTaskStatus.PendingAcceptance,
            PrimaryAssistantId = primaryId,
            BackupAssistantId = backupId,
            AssignedAssistantId = primaryId
        };
        db.PageTasks.Add(task);

        var collab1 = new MangakaAssistantCollaboration(mangakaId, primaryId, Guid.NewGuid(), DateTime.UtcNow);
        var collab2 = new MangakaAssistantCollaboration(mangakaId, backupId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collab1, collab2);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab1.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab2.Id, series.Id, mangakaId));

        var primaryAttempt = TaskAssignmentAttempt.CreatePending(task.Id, primaryId, collab1.Id, 1, mangakaId, assignmentRole: "Primary");
        var backupAttempt = TaskAssignmentAttempt.CreatePending(task.Id, backupId, collab2.Id, 2, mangakaId, assignmentRole: "Backup");
        backupAttempt.Accept(backupId, DateTime.UtcNow); // Backup has accepted standby
        db.TaskAssignmentAttempts.AddRange(primaryAttempt, backupAttempt);

        await db.SaveChangesAsync();

        var attemptRepo = new TaskAssignmentRepository(provider);
        var taskRepo = new PageTaskRepository(provider);
        var studioRepo = new StudioInvitationRepository(provider);
        var grantRepo = new SeriesAccessGrantRepository(provider);
        var chapterRepo = new ChapterRepository(provider);
        var notificationsMock = new Mock<MangaERP.Shared.Application.Ports.INotificationService>();

        var respondHandler = new RespondTaskAssignmentHandler(
            attemptRepo, taskRepo, studioRepo, grantRepo, chapterRepo, notificationsMock.Object);

        // Act: Primary Rejects
        var primaryResult = await respondHandler.Handle(
            new RespondTaskAssignmentCommand(primaryAttempt.Id, false, "Too busy with another manga", primaryId, primaryAttempt.ConcurrencyToken), default);

        // Assert
        Assert.Equal("Rejected", primaryResult.Status);

        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask);

        // Rule Requirement 1: Task status becomes ReassignmentRequired (NOT InProgress/Takeover)
        Assert.Equal(PageTaskStatus.ReassignmentRequired, updatedTask.TaskStatus);
        Assert.Null(updatedTask.AssignedAssistantId); // Workload released for Primary
        Assert.Equal(backupId, updatedTask.BackupAssistantId); // Backup remains in standby!

        // Verify notification was sent to Mangaka owner
        notificationsMock.Verify(n => n.NotifyCollaborationEventAsync(
            mangakaId,
            "TaskAssignmentRejected",
            It.IsAny<string>(),
            It.IsAny<string>(),
            task.Id,
            It.IsAny<System.Threading.CancellationToken>()), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task Takeover_OnlyOwnerMangakaCanTrigger_BackupAndTantouBlocked()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var tantouId = Guid.NewGuid();
        var backupId = Guid.NewGuid();
        var otherAssistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Takeover Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = new PageTask
        {
            ChapterId = chapter.Id,
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Incomplete,
            PrimaryAssistantId = Guid.NewGuid(),
            BackupAssistantId = backupId,
            AssignedAssistantId = Guid.NewGuid()
        };
        db.PageTasks.Add(task);
        await db.SaveChangesAsync();

        var pageTaskRepo = new PageTaskRepository(provider);
        var chapterRepo = new ChapterRepository(provider);
        var seriesRepo = new SeriesRepository(provider);
        var attemptRepo = new TaskAssignmentRepository(provider);
        var notificationsMock = new Mock<MangaERP.Shared.Application.Ports.INotificationService>();

        var handler = new RequestTakeoverHandler(
            pageTaskRepo, chapterRepo, seriesRepo, attemptRepo, notificationsMock.Object);

        // Act & Assert 1: Backup Assistant attempting takeover throws UnauthorizedAccessException (403)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new RequestTakeoverCommand(task.Id, backupId, "I want to takeover"), default));

        // Act & Assert 2: Tantou Editor attempting takeover throws UnauthorizedAccessException (403)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new RequestTakeoverCommand(task.Id, tantouId, "Tantou takeover"), default));

        // Act & Assert 3: Other Assistant attempting takeover throws UnauthorizedAccessException (403)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new RequestTakeoverCommand(task.Id, otherAssistantId, "Other assistant takeover"), default));

        // Act 4: Owner Mangaka triggering takeover -> SUCCEEDS!
        var result = await handler.Handle(new RequestTakeoverCommand(task.Id, mangakaId, "Mangaka triggering backup takeover"), default);
        Assert.NotNull(result);
        Assert.Equal(backupId, result.BackupAssistantId);
    }

    [Fact]
    public async System.Threading.Tasks.Task CancelAndRecreate_WhenTaskHasProgressUpdates_WithoutConfirm_IsBlocked()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var series = MangaSeries.Create(mangakaId, null, "Progress Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = new PageTask
        {
            ChapterId = chapter.Id,
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Incomplete,
            ProgressPercent = 50 // Progress update exists
        };
        db.PageTasks.Add(task);
        await db.SaveChangesAsync();

        var chapterRepo = new ChapterRepository(provider);
        var taskRepo = new PageTaskRepository(provider);
        var seriesRepo = new SeriesRepository(provider);

        var handler = new CancelAndRecreateTaskHandler(chapterRepo, taskRepo, seriesRepo);

        // Act & Assert: Without confirmProgressLoss = true, attempt fails
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CancelAndRecreateTaskCommand(mangakaId, task.Id, "Reason", ConfirmProgressLoss: false), default));
    }

    [Fact]
    public async System.Threading.Tasks.Task CancelAndRecreate_WhenTaskHasProgressUpdates_WithConfirm_Succeeds_RetainsPageNumber()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var series = MangaSeries.Create(mangakaId, null, "Progress Series 2", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = new PageTask
        {
            ChapterId = chapter.Id,
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Incomplete,
            ProgressPercent = 50
        };
        db.PageTasks.Add(task);
        await db.SaveChangesAsync();

        var chapterRepo = new ChapterRepository(provider);
        var taskRepo = new PageTaskRepository(provider);
        var seriesRepo = new SeriesRepository(provider);

        var handler = new CancelAndRecreateTaskHandler(chapterRepo, taskRepo, seriesRepo);

        // Act: With ConfirmProgressLoss = true -> SUCCEEDS!
        var result = await handler.Handle(new CancelAndRecreateTaskCommand(mangakaId, task.Id, "Recreating task", ConfirmProgressLoss: true), default);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.PageNumber);

        var oldTaskDb = await db.PageTasks.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == task.Id);
        Assert.NotNull(oldTaskDb);
        Assert.True(oldTaskDb.IsDeleted);
        Assert.Equal(1, oldTaskDb.PageNumber); // PageNumber retained!

        var newTaskDb = await db.PageTasks.FirstOrDefaultAsync(p => p.Id == result.NewPageTaskId);
        Assert.NotNull(newTaskDb);
        Assert.False(newTaskDb.IsDeleted);
        Assert.Equal(1, newTaskDb.PageNumber); // New task takes original PageNumber!
        Assert.Null(newTaskDb.AssignedAssistantId);
        Assert.Null(newTaskDb.WorkStartedAt);
        Assert.Equal(PageTaskStatus.Pending, newTaskDb.TaskStatus);
    }

    [Fact]
    public async System.Threading.Tasks.Task HalfwayDeadlineWarning_SendsOnlyOnce_When50PercentElapsed()
    {
        // Arrange
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
            WorkStartedAt = now.AddHours(-10), // Started 10 hours ago
            Deadline = now.AddHours(2)         // Total window 12 hours -> 10/12 = 83.3% elapsed (> 50%)
        };
        db.PageTasks.Add(task);
        await db.SaveChangesAsync();

        var taskRepo = new PageTaskRepository(provider);
        var chapterRepo = new ChapterRepository(provider);
        var seriesRepo = new SeriesRepository(provider);
        var notificationsMock = new Mock<MangaERP.Shared.Application.Ports.INotificationService>();

        var handler = new CheckHalfwayDeadlineWarningsHandler(taskRepo, chapterRepo, seriesRepo, notificationsMock.Object);

        // Act 1: Run warning checker for the first time
        int warningsSent1 = await handler.Handle(new CheckHalfwayDeadlineWarningsCommand(), default);

        // Assert 1: Exactly 1 warning sent
        Assert.Equal(1, warningsSent1);
        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask!.HalfwayWarningSentAt);

        // Act 2: Run warning checker again (Idempotency check)
        int warningsSent2 = await handler.Handle(new CheckHalfwayDeadlineWarningsCommand(), default);

        // Assert 2: 0 warnings sent on second run (idempotent!)
        Assert.Equal(0, warningsSent2);
    }

    [Fact]
    public async System.Threading.Tasks.Task ReassignTask_AtomicSuccess_SupersedesOldAttempts_ResetsWorkStartedAt_UpdatesWorkload()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var oldPrimaryId = Guid.NewGuid();
        var newPrimaryId = Guid.NewGuid();
        var newBackupId = Guid.NewGuid();

        var mangaka = new User { Id = mangakaId, Username = "mangaka", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var oldPrimary = new User { Id = oldPrimaryId, Username = "oldP", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var newPrimary = new User { Id = newPrimaryId, Username = "newP", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var newBackup = new User { Id = newBackupId, Username = "newB", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(mangaka, oldPrimary, newPrimary, newBackup);

        var series = MangaSeries.Create(mangakaId, null, "Reassign Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = new PageTask
        {
            ChapterId = chapter.Id,
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Incomplete,
            WorkStartedAt = DateTime.UtcNow.AddDays(-1),
            PrimaryAssistantId = oldPrimaryId,
            AssignedAssistantId = oldPrimaryId
        };
        db.PageTasks.Add(task);

        var collabOld = new MangakaAssistantCollaboration(mangakaId, oldPrimaryId, Guid.NewGuid(), DateTime.UtcNow);
        var collabNewP = new MangakaAssistantCollaboration(mangakaId, newPrimaryId, Guid.NewGuid(), DateTime.UtcNow);
        var collabNewB = new MangakaAssistantCollaboration(mangakaId, newBackupId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabOld, collabNewP, collabNewB);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOld.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabNewP.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabNewB.Id, series.Id, mangakaId));

        var oldAttempt = TaskAssignmentAttempt.CreatePending(task.Id, oldPrimaryId, collabOld.Id, 1, mangakaId, assignmentRole: "Primary");
        oldAttempt.Accept(oldPrimaryId, DateTime.UtcNow.AddDays(-1));
        db.TaskAssignmentAttempts.Add(oldAttempt);

        await db.SaveChangesAsync();

        var taskRepo = new PageTaskRepository(provider);
        var chapterRepo = new ChapterRepository(provider);
        var seriesRepo = new SeriesRepository(provider);
        var studioRepo = new StudioInvitationRepository(provider);
        var grantRepo = new SeriesAccessGrantRepository(provider);
        var attemptRepo = new TaskAssignmentRepository(provider);
        var notificationsMock = new Mock<MangaERP.Shared.Application.Ports.INotificationService>();
        var config = GetTestConfig(3);

        var handler = new ReassignTaskHandler(
            taskRepo, chapterRepo, seriesRepo, studioRepo, grantRepo, attemptRepo, notificationsMock.Object, config, provider);

        var command = new ReassignTaskCommand(
            task.Id, newPrimaryId, newBackupId, mangakaId, "Reassigning for performance issues");

        // Act
        var result = await handler.Handle(command, default);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.PrimaryAttempt);
        Assert.NotNull(result.BackupAttempt);

        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(PageTaskStatus.PendingAcceptance, updatedTask.TaskStatus);
        Assert.Null(updatedTask.WorkStartedAt); // WorkStartedAt reset to null!
        Assert.Equal(newPrimaryId, updatedTask.PrimaryAssistantId);
        Assert.Equal(newBackupId, updatedTask.BackupAssistantId);

        var oldAttemptDb = await db.TaskAssignmentAttempts.FindAsync(oldAttempt.Id);
        Assert.Equal(TaskAssignmentAttemptStatus.Superseded, oldAttemptDb!.Status); // Old attempt superseded!

        int oldWorkload = await attemptRepo.GetActiveWorkloadCountAsync(oldPrimaryId, default);
        Assert.Equal(0, oldWorkload); // Old workload released!

        int newPrimaryWorkload = await attemptRepo.GetActiveWorkloadCountAsync(newPrimaryId, default);
        Assert.Equal(1, newPrimaryWorkload); // New pending workload increased!
    }

    [Fact]
    public async System.Threading.Tasks.Task ReassignTask_InvalidBackup_RollsBackEverything_OldAttemptNotSuperseded()
    {
        // Arrange
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var oldPrimaryId = Guid.NewGuid();
        var newPrimaryId = Guid.NewGuid();
        var invalidBackupId = Guid.NewGuid(); // No collaboration with Mangaka

        var mangaka = new User { Id = mangakaId, Username = "mangaka", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var oldPrimary = new User { Id = oldPrimaryId, Username = "oldP", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var newPrimary = new User { Id = newPrimaryId, Username = "newP", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(mangaka, oldPrimary, newPrimary);

        var series = MangaSeries.Create(mangakaId, null, "Rollback Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var task = new PageTask
        {
            ChapterId = chapter.Id,
            PageNumber = 1,
            TaskStatus = PageTaskStatus.Incomplete,
            WorkStartedAt = DateTime.UtcNow.AddDays(-1),
            PrimaryAssistantId = oldPrimaryId,
            AssignedAssistantId = oldPrimaryId
        };
        db.PageTasks.Add(task);

        var collabOld = new MangakaAssistantCollaboration(mangakaId, oldPrimaryId, Guid.NewGuid(), DateTime.UtcNow);
        var collabNewP = new MangakaAssistantCollaboration(mangakaId, newPrimaryId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabOld, collabNewP);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOld.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabNewP.Id, series.Id, mangakaId));

        var oldAttempt = TaskAssignmentAttempt.CreatePending(task.Id, oldPrimaryId, collabOld.Id, 1, mangakaId, assignmentRole: "Primary");
        oldAttempt.Accept(oldPrimaryId, DateTime.UtcNow.AddDays(-1));
        db.TaskAssignmentAttempts.Add(oldAttempt);

        await db.SaveChangesAsync();

        var taskRepo = new PageTaskRepository(provider);
        var chapterRepo = new ChapterRepository(provider);
        var seriesRepo = new SeriesRepository(provider);
        var studioRepo = new StudioInvitationRepository(provider);
        var grantRepo = new SeriesAccessGrantRepository(provider);
        var attemptRepo = new TaskAssignmentRepository(provider);
        var notificationsMock = new Mock<MangaERP.Shared.Application.Ports.INotificationService>();
        var config = GetTestConfig(3);

        var handler = new ReassignTaskHandler(
            taskRepo, chapterRepo, seriesRepo, studioRepo, grantRepo, attemptRepo, notificationsMock.Object, config, provider);

        var command = new ReassignTaskCommand(
            task.Id, newPrimaryId, invalidBackupId, mangakaId, "Reassign with invalid backup");

        // Act & Assert
        await Assert.ThrowsAsync<MangaERP.Shared.Domain.Exceptions.ConflictException>(() =>
            handler.Handle(command, default));

        var oldAttemptDb = await db.TaskAssignmentAttempts.FindAsync(oldAttempt.Id);
        Assert.Equal(TaskAssignmentAttemptStatus.Accepted, oldAttemptDb!.Status); // Old attempt NOT superseded!

        var taskDb = await db.PageTasks.FindAsync(task.Id);
        Assert.Equal(PageTaskStatus.Incomplete, taskDb!.TaskStatus); // Task state unchanged!
        Assert.Equal(oldPrimaryId, taskDb.AssignedAssistantId);
    }
}
