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
using MangaERP.Task.Presentation.Controllers;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

    // Scenario 1, 2, 3, 4, 5: Direct Assign sets AssignedAssistantId, Incomplete status, WorkStartedAt, Accepted attempt, and increases workload immediately
    [Fact]
    public async System.Threading.Tasks.Task Scenario01_to_05_DirectAssign_SetsAllFieldsAndWorkloadImmediately()
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

        var notificationsMock = new Mock<INotificationService>();
        var handler = new AssignTaskToAssistantHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new TaskAssignmentRepository(provider), notificationsMock.Object, GetTestConfig(3));

        var command = new AssignTaskToAssistantCommand(task.Id, assistantId, mangakaId, "Do page 1", DateTime.UtcNow.AddDays(2));
        var result = await handler.Handle(command, default);

        Assert.NotNull(result.Attempt);
        Assert.Equal("Accepted", result.Attempt.Status);
        Assert.Equal("Direct", result.Attempt.AssignmentRole);

        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask);

        Assert.Equal(assistantId, updatedTask.AssignedAssistantId);
        Assert.Equal(PageTaskStatus.Incomplete, updatedTask.TaskStatus);
        Assert.NotNull(updatedTask.WorkStartedAt);

        var attemptRepo = new TaskAssignmentRepository(provider);
        int workload = await attemptRepo.GetActiveWorkloadCountAsync(assistantId, default);
        Assert.Equal(1, workload);

        notificationsMock.Verify(n => n.NotifyCollaborationEventAsync(
            assistantId, "TaskAssigned", "New Task Assigned", It.IsAny<string>(), task.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // Scenario 6 & 25: Respond endpoint returns 410 Gone / No task-level Accept/Reject contract
    [Fact]
    public void Scenario06_25_RespondEndpoint_Returns410Gone()
    {
        var controller = new TaskAssignmentsController(new Mock<IMediator>().Object);
        var response = controller.RespondTaskAssignment(Guid.NewGuid(), new RespondTaskAssignmentRequest(true, null, null));

        var statusCodeResult = Assert.IsType<ObjectResult>(response);
        Assert.Equal(StatusCodes.Status410Gone, statusCodeResult.StatusCode);
    }

    // Scenario 7-17: Direct Reassign changes executor immediately and preserves TaskId, WorkStartedAt, deadline, progress, artwork layers, write access
    [Fact]
    public async System.Threading.Tasks.Task Scenario07_to_17_DirectReassign_UpdatesExecutorAndPreservesTaskData()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);
        var authService = new CollaborationAuthorizationService(db);

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
        task.AssignDirect(oldAssistantId, "Initial task", initialDeadline, initialWorkStartedAt);
        task.SubmitProgress(45);
        db.PageTasks.Add(task);

        var collabOld = new MangakaAssistantCollaboration(mangakaId, oldAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        var collabNew = new MangakaAssistantCollaboration(mangakaId, newAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabOld, collabNew);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOld.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabNew.Id, series.Id, mangakaId));

        var attempt1 = TaskAssignmentAttempt.CreateAccepted(task.Id, oldAssistantId, collabOld.Id, 1, mangakaId, initialWorkStartedAt);
        db.TaskAssignmentAttempts.Add(attempt1);

        await db.SaveChangesAsync();

        Assert.True(await authService.CanSubmitProgressAsync(oldAssistantId, task.Id));
        Assert.False(await authService.CanSubmitProgressAsync(newAssistantId, task.Id));

        var notificationsMock = new Mock<INotificationService>();
        var reassignHandler = new ReassignTaskHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new TaskAssignmentRepository(provider), notificationsMock.Object, GetTestConfig(3), provider);

        var reassignResult = await reassignHandler.Handle(new ReassignTaskCommand(task.Id, newAssistantId, mangakaId, "Needs help with background"), default);

        Assert.NotNull(reassignResult.Attempt);
        Assert.Equal("Accepted", reassignResult.Attempt.Status);

        var attempt1Db = await db.TaskAssignmentAttempts.FindAsync(attempt1.Id);
        Assert.Equal(TaskAssignmentAttemptStatus.Superseded, attempt1Db!.Status);

        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask);

        Assert.Equal(newAssistantId, updatedTask.AssignedAssistantId);
        Assert.Equal(PageTaskStatus.Incomplete, updatedTask.TaskStatus);

        Assert.Equal(task.Id, updatedTask.Id);
        Assert.Equal(45, updatedTask.ProgressPercent);
        Assert.Equal(initialWorkStartedAt, updatedTask.WorkStartedAt);
        Assert.Equal(initialDeadline, updatedTask.Deadline);

        Assert.False(await authService.CanSubmitProgressAsync(oldAssistantId, task.Id));
        Assert.True(await authService.CanSubmitProgressAsync(newAssistantId, task.Id));
    }

    // Cancel-and-Recreate Test 1 & 2 & 9 & 10: Recreate AssistantAbandonedTask stores PreviousAssignedAssistantId, links new task to old task, sets unassigned & retains history
    [Fact]
    public async System.Threading.Tasks.Task CancelAndRecreate_AssistantRelatedCategory_StoresLinkAndExclusionData()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var oldAssistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Exclusion Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var oldTask = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        oldTask.AssignDirect(oldAssistantId, "Old task desc", DateTime.UtcNow.AddDays(2));
        db.PageTasks.Add(oldTask);
        await db.SaveChangesAsync();

        var handler = new CancelAndRecreateTaskHandler(
            new ChapterRepository(provider), new PageTaskRepository(provider), new SeriesRepository(provider));

        var cmd = new CancelAndRecreateTaskCommand(mangakaId, oldTask.Id, TaskCancellationCategory.AssistantAbandonedTask, "Assistant left without completing");
        var result = await handler.Handle(cmd, default);

        var newTask = await db.PageTasks.FindAsync(result.NewPageTaskId);
        Assert.NotNull(newTask);

        // Test 1 & 2: Link & PreviousAssignedAssistantId stored
        Assert.Equal(oldTask.Id, newTask.RecreatedFromTaskId);
        Assert.Equal(oldAssistantId, newTask.PreviousAssignedAssistantId);
        Assert.Equal(TaskCancellationCategory.AssistantAbandonedTask, newTask.CancellationCategory);

        // Test 9: New task is unassigned
        Assert.Null(newTask.AssignedAssistantId);
        Assert.Equal(PageTaskStatus.Pending, newTask.TaskStatus);

        // Test 10: Old task history preserved (Soft deleted & Cancelled status)
        var cancelledOldTask = db.PageTasks.IgnoreQueryFilters().FirstOrDefault(t => t.Id == oldTask.Id);
        Assert.NotNull(cancelledOldTask);
        Assert.True(cancelledOldTask.IsDeleted);
        Assert.Equal(PageTaskStatus.Cancelled, cancelledOldTask.TaskStatus);
    }

    // Cancel-and-Recreate Test 3 & 4 & 11: Candidate API returns previous assistant in unavailable list with PreviousTaskAssigneeExcluded code, allows other assistant
    [Fact]
    public async System.Threading.Tasks.Task CancelAndRecreate_CandidateApi_ExcludesPreviousAssistant_AndAllowsOtherAssistant()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var oldAssistantId = Guid.NewGuid();
        var otherAssistantId = Guid.NewGuid();

        var mangaka = new User { Id = mangakaId, Username = "mangaka", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var oldAst = new User { Id = oldAssistantId, Username = "old_ast", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var otherAst = new User { Id = otherAssistantId, Username = "other_ast", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(mangaka, oldAst, otherAst);

        var series = MangaSeries.Create(mangakaId, null, "Candidate Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var collabOld = new MangakaAssistantCollaboration(mangakaId, oldAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        var collabOther = new MangakaAssistantCollaboration(mangakaId, otherAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabOld, collabOther);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOld.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOther.Id, series.Id, mangakaId));

        var recreatedTask = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        recreatedTask.RecreatedFromTaskId = Guid.NewGuid();
        recreatedTask.PreviousAssignedAssistantId = oldAssistantId;
        recreatedTask.CancellationCategory = TaskCancellationCategory.AssistantAbandonedTask;
        db.PageTasks.Add(recreatedTask);

        await db.SaveChangesAsync();

        var candidateHandler = new GetAssistantCandidatesHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new UserRepository(provider), new TaskAssignmentRepository(provider), GetTestConfig(3));

        var result = await candidateHandler.Handle(new GetAssistantCandidatesQuery(recreatedTask.Id, mangakaId), default);

        // Test 3 & 4: Old assistant is in UnavailableAssistants with PreviousTaskAssigneeExcluded
        var excludedCandidate = result.UnavailableAssistants.FirstOrDefault(a => a.AssistantId == oldAssistantId);
        Assert.NotNull(excludedCandidate);
        Assert.False(excludedCandidate.IsAvailable);
        Assert.Equal("PreviousTaskAssigneeExcluded", excludedCandidate.AvailabilityCode);
        Assert.Equal("This assistant was removed from the previous version of this task.", excludedCandidate.AvailabilityReason);

        // Test 11: Other assistant is in AvailableAssistants
        var availableCandidate = result.AvailableAssistants.FirstOrDefault(a => a.AssistantId == otherAssistantId);
        Assert.NotNull(availableCandidate);
        Assert.True(availableCandidate.IsAvailable);
    }

    // Cancel-and-Recreate Test 5 & 6: Assign and Reassign handlers block previous assistant via manual API request
    [Fact]
    public async System.Threading.Tasks.Task CancelAndRecreate_AssignAndReassign_EnforceExclusionRule()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var oldAssistantId = Guid.NewGuid();
        var newAssistantId = Guid.NewGuid();

        var series = MangaSeries.Create(mangakaId, null, "Enforcement Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var collabOld = new MangakaAssistantCollaboration(mangakaId, oldAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        var collabNew = new MangakaAssistantCollaboration(mangakaId, newAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collabOld, collabNew);

        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabOld.Id, series.Id, mangakaId));
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collabNew.Id, series.Id, mangakaId));

        var recreatedTask = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base.png");
        recreatedTask.RecreatedFromTaskId = Guid.NewGuid();
        recreatedTask.PreviousAssignedAssistantId = oldAssistantId;
        recreatedTask.CancellationCategory = TaskCancellationCategory.AssistantFailedToStart;
        db.PageTasks.Add(recreatedTask);

        await db.SaveChangesAsync();

        var assignHandler = new AssignTaskToAssistantHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new TaskAssignmentRepository(provider), new Mock<INotificationService>().Object, GetTestConfig(3));

        // Test 5: Assign blocks manual request for old assistant
        var assignEx = await Assert.ThrowsAsync<MangaERP.Shared.Domain.Exceptions.ConflictException>(() =>
            assignHandler.Handle(new AssignTaskToAssistantCommand(recreatedTask.Id, oldAssistantId, mangakaId, "Manual assign"), default));
        Assert.Contains("PREVIOUS_TASK_ASSIGNEE_EXCLUDED", assignEx.Message);

        // Assign to new assistant first so task is active
        await assignHandler.Handle(new AssignTaskToAssistantCommand(recreatedTask.Id, newAssistantId, mangakaId, "Assign to new assistant"), default);

        var reassignHandler = new ReassignTaskHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new TaskAssignmentRepository(provider), new Mock<INotificationService>().Object, GetTestConfig(3), provider);

        // Test 6: Reassign blocks manual request for old assistant
        var reassignEx = await Assert.ThrowsAsync<MangaERP.Shared.Domain.Exceptions.ConflictException>(() =>
            reassignHandler.Handle(new ReassignTaskCommand(recreatedTask.Id, oldAssistantId, mangakaId, "Manual reassign back to old assistant"), default));
        Assert.Contains("PREVIOUS_TASK_ASSIGNEE_EXCLUDED", reassignEx.Message);
    }

    // Cancel-and-Recreate Test 7 & 8: Recreate with WrongBaseImage or WrongTaskType does NOT exclude previous assistant
    [Fact]
    public async System.Threading.Tasks.Task CancelAndRecreate_TaskRelatedCategories_DoesNotExcludePreviousAssistant()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var oldAssistantId = Guid.NewGuid();

        var mangaka = new User { Id = mangakaId, Username = "mangaka", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var ast = new User { Id = oldAssistantId, Username = "ast", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(mangaka, ast);

        var series = MangaSeries.Create(mangakaId, null, "Task Issue Series", "Desc", "Genre", null);
        var chapter = ChapterEntity.Create(series.Id, "Chapter 1", 1.0m, 10);
        db.MangaSeries.Add(series);
        db.Chapters.Add(chapter);

        var collab = new MangakaAssistantCollaboration(mangakaId, oldAssistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.Add(collab);
        db.SeriesAccessGrants.Add(SeriesAccessGrant.Create(collab.Id, series.Id, mangakaId));

        var taskWrongBaseImage = PageTask.CreatePending(chapter.Id, 1, "https://example.com/base1.png");
        taskWrongBaseImage.RecreatedFromTaskId = Guid.NewGuid();
        taskWrongBaseImage.PreviousAssignedAssistantId = oldAssistantId;
        taskWrongBaseImage.CancellationCategory = TaskCancellationCategory.WrongBaseImage; // Test 7

        var taskWrongTaskType = PageTask.CreatePending(chapter.Id, 2, "https://example.com/base2.png");
        taskWrongTaskType.RecreatedFromTaskId = Guid.NewGuid();
        taskWrongTaskType.PreviousAssignedAssistantId = oldAssistantId;
        taskWrongTaskType.CancellationCategory = TaskCancellationCategory.WrongTaskType; // Test 8

        db.PageTasks.AddRange(taskWrongBaseImage, taskWrongTaskType);
        await db.SaveChangesAsync();

        var candidateHandler = new GetAssistantCandidatesHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new UserRepository(provider), new TaskAssignmentRepository(provider), GetTestConfig(3));

        var res1 = await candidateHandler.Handle(new GetAssistantCandidatesQuery(taskWrongBaseImage.Id, mangakaId), default);
        var res2 = await candidateHandler.Handle(new GetAssistantCandidatesQuery(taskWrongTaskType.Id, mangakaId), default);

        // Assistant is available for both task-related recreate tasks!
        Assert.Single(res1.AvailableAssistants);
        Assert.Equal(oldAssistantId, res1.AvailableAssistants[0].AssistantId);

        Assert.Single(res2.AvailableAssistants);
        Assert.Equal(oldAssistantId, res2.AvailableAssistants[0].AssistantId);
    }

    [Fact]
    public async System.Threading.Tasks.Task CandidateApi_EnforcesDataIsolation_ScopeBoundaries_AndClassification()
    {
        using var db = GetInMemoryDbContext();
        var provider = new TestDbContextProvider(db);

        var mangakaAId = Guid.NewGuid();
        var mangakaBId = Guid.NewGuid();

        var ast1_ActiveAndAvailable = Guid.NewGuid();
        var ast2_MangakaBAssistant = Guid.NewGuid();
        var ast3_NoCollab = Guid.NewGuid();
        var ast4_EndedCollab = Guid.NewGuid();
        var ast5_SuspendedCollab = Guid.NewGuid();
        var ast6_NoSeriesGrant = Guid.NewGuid();
        var ast7_MaxWorkload = Guid.NewGuid();
        var ast8_AccountInactive = Guid.NewGuid();
        var ast9_PreviousExcluded = Guid.NewGuid();

        db.Users.AddRange(
            new User { Id = mangakaAId, Username = "mangakaA", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active },
            new User { Id = mangakaBId, Username = "mangakaB", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active },
            new User { Id = ast1_ActiveAndAvailable, Username = "ast1", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active },
            new User { Id = ast2_MangakaBAssistant, Username = "ast2", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active },
            new User { Id = ast3_NoCollab, Username = "ast3", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active },
            new User { Id = ast4_EndedCollab, Username = "ast4", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active },
            new User { Id = ast5_SuspendedCollab, Username = "ast5", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active },
            new User { Id = ast6_NoSeriesGrant, Username = "ast6", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active },
            new User { Id = ast7_MaxWorkload, Username = "ast7", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active },
            new User { Id = ast8_AccountInactive, Username = "ast8", Role = UserRole.Assistant, AccountStatus = AccountStatus.Suspended },
            new User { Id = ast9_PreviousExcluded, Username = "ast9", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active }
        );

        var seriesA = MangaSeries.Create(mangakaAId, null, "Series A", "Desc", "Genre", null);
        var seriesB = MangaSeries.Create(mangakaBId, null, "Series B", "Desc", "Genre", null);
        var chapterA = ChapterEntity.Create(seriesA.Id, "Chapter A1", 1.0m, 10);
        db.MangaSeries.AddRange(seriesA, seriesB);
        db.Chapters.Add(chapterA);

        // Collabs
        var collab1 = new MangakaAssistantCollaboration(mangakaAId, ast1_ActiveAndAvailable, Guid.NewGuid(), DateTime.UtcNow);
        var collab2 = new MangakaAssistantCollaboration(mangakaBId, ast2_MangakaBAssistant, Guid.NewGuid(), DateTime.UtcNow);
        var collab4 = new MangakaAssistantCollaboration(mangakaAId, ast4_EndedCollab, Guid.NewGuid(), DateTime.UtcNow);
        collab4.End("ended test", mangakaAId, DateTime.UtcNow);
        var collab5 = new MangakaAssistantCollaboration(mangakaAId, ast5_SuspendedCollab, Guid.NewGuid(), DateTime.UtcNow);
        collab5.Suspend(CollaborationSuspensionMode.SuspendAllAccess, "suspended test", DateTime.UtcNow);
        var collab6 = new MangakaAssistantCollaboration(mangakaAId, ast6_NoSeriesGrant, Guid.NewGuid(), DateTime.UtcNow);
        var collab7 = new MangakaAssistantCollaboration(mangakaAId, ast7_MaxWorkload, Guid.NewGuid(), DateTime.UtcNow);
        var collab8 = new MangakaAssistantCollaboration(mangakaAId, ast8_AccountInactive, Guid.NewGuid(), DateTime.UtcNow);
        var collab9 = new MangakaAssistantCollaboration(mangakaAId, ast9_PreviousExcluded, Guid.NewGuid(), DateTime.UtcNow);

        db.MangakaAssistantCollaborations.AddRange(collab1, collab2, collab4, collab5, collab6, collab7, collab8, collab9);

        // Grants
        db.SeriesAccessGrants.AddRange(
            SeriesAccessGrant.Create(collab1.Id, seriesA.Id, mangakaAId),
            SeriesAccessGrant.Create(collab5.Id, seriesA.Id, mangakaAId),
            SeriesAccessGrant.Create(collab7.Id, seriesA.Id, mangakaAId),
            SeriesAccessGrant.Create(collab8.Id, seriesA.Id, mangakaAId),
            SeriesAccessGrant.Create(collab9.Id, seriesA.Id, mangakaAId)
        );

        // Max Workload setup for ast7 (3 active tasks)
        for (int i = 1; i <= 3; i++)
        {
            var dummyTask = PageTask.CreatePending(chapterA.Id, i + 10, $"https://example.com/{i}.png");
            dummyTask.AssignDirect(ast7_MaxWorkload, "Work", DateTime.UtcNow.AddDays(1), DateTime.UtcNow);
            db.PageTasks.Add(dummyTask);
            db.TaskAssignmentAttempts.Add(TaskAssignmentAttempt.CreateAccepted(dummyTask.Id, ast7_MaxWorkload, collab7.Id, 1, mangakaAId, DateTime.UtcNow, "Direct", DateTime.UtcNow.AddDays(1)));
        }

        // Excluded task setup for ast9
        var task = PageTask.CreatePending(chapterA.Id, 1, "https://example.com/base.png");
        task.RecreatedFromTaskId = Guid.NewGuid();
        task.PreviousAssignedAssistantId = ast9_PreviousExcluded;
        task.CancellationCategory = TaskCancellationCategory.AssistantAbandonedTask;
        db.PageTasks.Add(task);

        await db.SaveChangesAsync();

        var candidateHandler = new GetAssistantCandidatesHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new UserRepository(provider), new TaskAssignmentRepository(provider), GetTestConfig(3));

        // Test 11: Non-owner calling query throws UnauthorizedAccessException (403)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            candidateHandler.Handle(new GetAssistantCandidatesQuery(task.Id, mangakaBId), default));

        var result = await candidateHandler.Handle(new GetAssistantCandidatesQuery(task.Id, mangakaAId), default);

        // Test 1: Only A's assistants are considered
        // Test 2: Mangaka B's assistant (ast2) is NOT present
        Assert.DoesNotContain(result.AvailableAssistants, a => a.AssistantId == ast2_MangakaBAssistant);
        Assert.DoesNotContain(result.UnavailableAssistants, a => a.AssistantId == ast2_MangakaBAssistant);

        // Test 3: Uncollaborated assistant (ast3) is NOT present
        Assert.DoesNotContain(result.AvailableAssistants, a => a.AssistantId == ast3_NoCollab);
        Assert.DoesNotContain(result.UnavailableAssistants, a => a.AssistantId == ast3_NoCollab);

        // Test 4: Ended collaboration (ast4) is NOT present
        Assert.DoesNotContain(result.AvailableAssistants, a => a.AssistantId == ast4_EndedCollab);
        Assert.DoesNotContain(result.UnavailableAssistants, a => a.AssistantId == ast4_EndedCollab);

        // Test 6: ast1 is in availableAssistants
        var ast1Res = Assert.Single(result.AvailableAssistants, a => a.AssistantId == ast1_ActiveAndAvailable);
        Assert.True(ast1Res.IsAvailable);

        // Test 5: Suspended collab (ast5) in unavailableAssistants with CollaborationInactive
        var ast5Res = Assert.Single(result.UnavailableAssistants, a => a.AssistantId == ast5_SuspendedCollab);
        Assert.False(ast5Res.IsAvailable);
        Assert.Equal("CollaborationInactive", ast5Res.AvailabilityCode);

        // Test 7: Missing SeriesAccessGrant (ast6) in unavailableAssistants with SeriesAccessMissing
        var ast6Res = Assert.Single(result.UnavailableAssistants, a => a.AssistantId == ast6_NoSeriesGrant);
        Assert.False(ast6Res.IsAvailable);
        Assert.Equal("SeriesAccessMissing", ast6Res.AvailabilityCode);

        // Test 8: Max workload (ast7) in unavailableAssistants with WorkloadLimitReached
        var ast7Res = Assert.Single(result.UnavailableAssistants, a => a.AssistantId == ast7_MaxWorkload);
        Assert.False(ast7Res.IsAvailable);
        Assert.Equal("WorkloadLimitReached", ast7Res.AvailabilityCode);

        // Test 9: Account inactive (ast8) in unavailableAssistants with AccountInactive
        var ast8Res = Assert.Single(result.UnavailableAssistants, a => a.AssistantId == ast8_AccountInactive);
        Assert.False(ast8Res.IsAvailable);
        Assert.Equal("AccountInactive", ast8Res.AvailabilityCode);

        // Test 10: PreviousTaskAssigneeExcluded (ast9) in unavailableAssistants for Task endpoint
        var ast9Res = Assert.Single(result.UnavailableAssistants, a => a.AssistantId == ast9_PreviousExcluded);
        Assert.False(ast9Res.IsAvailable);
        Assert.Equal("PreviousTaskAssigneeExcluded", ast9Res.AvailabilityCode);

        // --- CHAPTER PRE-CREATE CANDIDATE ENDPOINT TESTS ---
        var chapterCandidateHandler = new GetChapterAssistantCandidatesHandler(
            new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new UserRepository(provider), new TaskAssignmentRepository(provider), GetTestConfig(3));

        // Test 2: Non-owner calling chapter candidate API throws UnauthorizedAccessException (403)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            chapterCandidateHandler.Handle(new GetChapterAssistantCandidatesQuery(chapterA.Id, mangakaBId), default));

        // Test 3: Non-existent chapter returns 404 EntityNotFoundException
        await Assert.ThrowsAsync<MangaERP.Shared.Domain.Exceptions.EntityNotFoundException>(() =>
            chapterCandidateHandler.Handle(new GetChapterAssistantCandidatesQuery(Guid.NewGuid(), mangakaAId), default));

        // Test 1: Owner Mangaka calling chapter candidate API succeeds
        var chapterResult = await chapterCandidateHandler.Handle(new GetChapterAssistantCandidatesQuery(chapterA.Id, mangakaAId), default);

        Assert.Equal(chapterA.Id, chapterResult.ChapterId);
        Assert.Equal(seriesA.Id, chapterResult.SeriesId);

        // Test 12: Chapter endpoint does NOT apply PreviousTaskAssigneeExcluded -> ast9 is in AvailableAssistants for pre-create!
        var ast9ChapterRes = Assert.Single(chapterResult.AvailableAssistants, a => a.AssistantId == ast9_PreviousExcluded);
        Assert.True(ast9ChapterRes.IsAvailable);

        // Test 4 & 5: Scoping holds (no B assistants, no uncollaborated, no ended)
        Assert.DoesNotContain(chapterResult.AvailableAssistants, a => a.AssistantId == ast2_MangakaBAssistant || a.AssistantId == ast3_NoCollab || a.AssistantId == ast4_EndedCollab);
        Assert.DoesNotContain(chapterResult.UnavailableAssistants, a => a.AssistantId == ast2_MangakaBAssistant || a.AssistantId == ast3_NoCollab || a.AssistantId == ast4_EndedCollab);
    }
}
