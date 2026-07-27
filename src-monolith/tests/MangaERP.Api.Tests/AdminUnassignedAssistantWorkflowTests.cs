using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Identity.Infrastructure.Repositories;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Shared.Infrastructure.Services;
using MangaERP.Studio.Application.Commands.AssignAssistantToMangaka;
using MangaERP.Studio.Application.Commands.ManageCollaboration;
using MangaERP.Studio.Application.Queries.GetAdminUnassignedAssistants;
using MangaERP.Studio.Application.Queries.GetMyAssistants;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using PageTaskEntity = MangaERP.Chapter.Domain.Entities.PageTask;
using TaskAssignmentAttempt = MangaERP.Task.Domain.Entities.TaskAssignmentAttempt;
using TaskAssignmentAttemptStatus = MangaERP.Task.Domain.Entities.TaskAssignmentAttemptStatus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MangaERP.Api.Tests;

public class AdminUnassignedAssistantWorkflowTests
{
    private sealed class TestDbContextProvider(AppDbContext context) : IDbContextProvider
    {
        public object GetDbContext() => context;
    }

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration GetTestConfig() => new ConfigurationBuilder().Build();

    [Fact]
    public async System.Threading.Tasks.Task GetAdminUnassignedAssistants_ReturnsOnlyTrulyFreeAssistants_WithHistory()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var prevMangaka = new User
        {
            Id = Guid.NewGuid(),
            Username = "prev_mgk",
            Email = "prev@test.local",
            FullName = "Previous Mangaka Sensei",
            PasswordHash = "hash",
            Role = UserRole.Mangaka,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(prevMangaka);

        // 1. Assistant Free (newly provisioned)
        var astNewFree = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_new_free",
            Email = "newfree@test.local",
            FullName = "New Free Assistant",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 2. Assistant Free (Ended collaboration)
        var astEndedFree = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_ended_free",
            Email = "endedfree@test.local",
            FullName = "Ended Free Assistant",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 3. Assistant Free (Rejected/Cancelled collaboration)
        var astCancelledFree = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_cancelled_free",
            Email = "cancelfree@test.local",
            FullName = "Cancelled Free Assistant",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 4. Assistant Non-Free (Accepted collaboration)
        var astAccepted = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_accepted",
            Email = "accepted@test.local",
            FullName = "Accepted Assistant",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 5. Assistant Non-Free (Suspended collaboration)
        var astSuspended = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_suspended",
            Email = "suspended@test.local",
            FullName = "Suspended Assistant",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 6. Assistant Non-Free (EndingRequested collaboration)
        var astEndingReq = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_ending_req",
            Email = "endingreq@test.local",
            FullName = "EndingRequested Assistant",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // 7. Assistant Inactive
        var astInactive = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_inactive",
            Email = "inactive@test.local",
            FullName = "Inactive Assistant",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.PendingActivation,
            CreatedAt = DateTime.UtcNow
        };

        // 8. Assistant Deleted
        var astDeleted = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_deleted",
            Email = "deleted@test.local",
            FullName = "Deleted Assistant",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(astNewFree, astEndedFree, astCancelledFree, astAccepted, astSuspended, astEndingReq, astInactive, astDeleted);

        // Add collaborations
        var endedCollab = new MangakaAssistantCollaboration(prevMangaka.Id, astEndedFree.Id, Guid.NewGuid(), DateTime.UtcNow);
        endedCollab.End("Contract finished", prevMangaka.Id, DateTime.UtcNow.AddDays(-5));

        var acceptedCollab = new MangakaAssistantCollaboration(prevMangaka.Id, astAccepted.Id, Guid.NewGuid(), DateTime.UtcNow);

        var suspendedCollab = new MangakaAssistantCollaboration(prevMangaka.Id, astSuspended.Id, Guid.NewGuid(), DateTime.UtcNow);
        suspendedCollab.Suspend(CollaborationSuspensionMode.SuspendAllAccess, "Temp pause", DateTime.UtcNow);

        var endingReqCollab = new MangakaAssistantCollaboration(prevMangaka.Id, astEndingReq.Id, Guid.NewGuid(), DateTime.UtcNow);
        endingReqCollab.RequestEnding(DateTime.UtcNow);

        db.MangakaAssistantCollaborations.AddRange(endedCollab, acceptedCollab, suspendedCollab, endingReqCollab);
        await db.SaveChangesAsync();

        var repo = new StudioInvitationRepository(provider);
        var handler = new GetAdminUnassignedAssistantsHandler(repo);

        var result = await handler.Handle(new GetAdminUnassignedAssistantsQuery(), default);

        Assert.NotNull(result);
        Assert.Equal(3, result.Assistants.Count);

        var ids = result.Assistants.Select(x => x.AssistantId).ToList();
        Assert.Contains(astNewFree.Id, ids);
        Assert.Contains(astEndedFree.Id, ids);
        Assert.Contains(astCancelledFree.Id, ids);

        Assert.DoesNotContain(astAccepted.Id, ids);
        Assert.DoesNotContain(astSuspended.Id, ids);
        Assert.DoesNotContain(astEndingReq.Id, ids);
        Assert.DoesNotContain(astInactive.Id, ids);
        Assert.DoesNotContain(astDeleted.Id, ids);

        var endedItem = result.Assistants.First(x => x.AssistantId == astEndedFree.Id);
        Assert.Equal(prevMangaka.Id, endedItem.PreviousMangakaId);
        Assert.Equal("Previous Mangaka Sensei", endedItem.PreviousMangakaName);
        Assert.NotNull(endedItem.LastCollaborationEndedAt);
        Assert.True(endedItem.IsAssignable);
    }

    [Fact]
    public async System.Threading.Tasks.Task AdminAssignAssistantToMangaka_FlowVerification()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var adminId = Guid.NewGuid();
        var mangaka = new User
        {
            Id = Guid.NewGuid(),
            Username = "mgk_new",
            Email = "mgk_new@test.local",
            PasswordHash = "hash",
            Role = UserRole.Mangaka,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var assistant = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_to_assign",
            Email = "ast_assign@test.local",
            FullName = "Assistant To Assign",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(mangaka, assistant);
        await db.SaveChangesAsync();

        var repo = new StudioInvitationRepository(provider);
        var handler = new AssignAssistantToMangakaHandler(repo);

        // 1. Assign free assistant to Mangaka
        var assignResult = await handler.Handle(new AssignAssistantToMangakaCommand(assistant.Id, mangaka.Id, adminId, "Assigning to new studio"), default);

        Assert.NotNull(assignResult);
        Assert.Equal(assistant.Id, assignResult.AssistantId);
        Assert.Equal(mangaka.Id, assignResult.MangakaId);
        Assert.Equal("Accepted", assignResult.Status);

        // 2. Verify Assistant disappears from unassigned pool
        var unassignedHandler = new GetAdminUnassignedAssistantsHandler(repo);
        var unassignedResult = await unassignedHandler.Handle(new GetAdminUnassignedAssistantsQuery(), default);
        Assert.DoesNotContain(unassignedResult.Assistants, x => x.AssistantId == assistant.Id);

        // 3. Verify Assistant appears in My Assistants of new Mangaka
        var myAssistantsHandler = new GetMyAssistantsHandler(
            repo,
            new SeriesAccessGrantRepository(provider),
            new UserRepository(provider),
            GetTestConfig());
        var myAssistantsResult = await myAssistantsHandler.Handle(new GetMyAssistantsQuery(mangaka.Id), default);
        Assert.Single(myAssistantsResult.Assistants);
        Assert.Equal(assistant.Id, myAssistantsResult.Assistants[0].AssistantId);
        Assert.Empty(myAssistantsResult.Assistants[0].SeriesAccess); // No SeriesAccessGrant auto-created

        // 4. Verify assigning an already assigned assistant throws ASSISTANT_NOT_UNASSIGNED (409)
        var ex = await Assert.ThrowsAsync<AdminAssignException>(() =>
            handler.Handle(new AssignAssistantToMangakaCommand(assistant.Id, mangaka.Id, adminId, "Duplicate assign"), default));
        Assert.Equal("ASSISTANT_NOT_UNASSIGNED", ex.ErrorCode);
        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task AdminAssignAssistantToMangaka_ValidationErrors()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var adminId = Guid.NewGuid();

        var mangakaActive = new User { Id = Guid.NewGuid(), Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var mangakaInactive = new User { Id = Guid.NewGuid(), Role = UserRole.Mangaka, AccountStatus = AccountStatus.PendingActivation };
        var userNotMangaka = new User { Id = Guid.NewGuid(), Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };

        var astActive = new User { Id = Guid.NewGuid(), Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var astInactive = new User { Id = Guid.NewGuid(), Role = UserRole.Assistant, AccountStatus = AccountStatus.PendingActivation };

        db.Users.AddRange(mangakaActive, mangakaInactive, userNotMangaka, astActive, astInactive);
        await db.SaveChangesAsync();

        var repo = new StudioInvitationRepository(provider);
        var handler = new AssignAssistantToMangakaHandler(repo);

        // Assistant not found
        var ex1 = await Assert.ThrowsAsync<AdminAssignException>(() =>
            handler.Handle(new AssignAssistantToMangakaCommand(Guid.NewGuid(), mangakaActive.Id, adminId, null), default));
        Assert.Equal("ASSISTANT_NOT_FOUND", ex1.ErrorCode);
        Assert.Equal(404, ex1.StatusCode);

        // Assistant inactive
        var ex2 = await Assert.ThrowsAsync<AdminAssignException>(() =>
            handler.Handle(new AssignAssistantToMangakaCommand(astInactive.Id, mangakaActive.Id, adminId, null), default));
        Assert.Equal("ASSISTANT_NOT_ACTIVE", ex2.ErrorCode);
        Assert.Equal(400, ex2.StatusCode);

        // Target mangaka not found
        var ex3 = await Assert.ThrowsAsync<AdminAssignException>(() =>
            handler.Handle(new AssignAssistantToMangakaCommand(astActive.Id, Guid.NewGuid(), adminId, null), default));
        Assert.Equal("TARGET_MANGAKA_NOT_FOUND", ex3.ErrorCode);
        Assert.Equal(404, ex3.StatusCode);

        // Target user not mangaka
        var ex4 = await Assert.ThrowsAsync<AdminAssignException>(() =>
            handler.Handle(new AssignAssistantToMangakaCommand(astActive.Id, userNotMangaka.Id, adminId, null), default));
        Assert.Equal("TARGET_USER_NOT_MANGAKA", ex4.ErrorCode);
        Assert.Equal(400, ex4.StatusCode);

        // Target mangaka inactive
        var ex5 = await Assert.ThrowsAsync<AdminAssignException>(() =>
            handler.Handle(new AssignAssistantToMangakaCommand(astActive.Id, mangakaInactive.Id, adminId, null), default));
        Assert.Equal("TARGET_MANGAKA_INACTIVE", ex5.ErrorCode);
        Assert.Equal(400, ex5.StatusCode);
    }

    [Fact]
    public async System.Threading.Tasks.Task EndCollaboration_RevokesGrants_CascadesTasks_ReturnsAssistantToFreePool()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        var mangaka = new User { Id = mangakaId, Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var assistant = new User { Id = assistantId, Role = UserRole.Assistant, AccountStatus = AccountStatus.Active, FullName = "End Test Ast" };
        db.Users.AddRange(mangaka, assistant);

        // Active collaboration
        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.Add(collab);

        // Active grant
        var grant = SeriesAccessGrant.Create(collab.Id, seriesId, mangakaId);
        db.SeriesAccessGrants.Add(grant);

        // Active chapter and task
        var series = MangaSeries.Create(mangakaId, null, "Test Series End", "Desc", "Genre", null);
        typeof(MangaSeries).GetProperty("Id")!.SetValue(series, seriesId);
        db.MangaSeries.Add(series);

        var chapter = ChapterEntity.Create(seriesId, "Ch 1", 1, 10);
        typeof(ChapterEntity).GetProperty("Id")!.SetValue(chapter, chapterId);
        db.Chapters.Add(chapter);

        var task = new PageTaskEntity
        {
            ChapterId = chapterId,
            PageNumber = 1,
            TaskType = PageTaskType.Inking,
            TaskStatus = PageTaskStatus.Incomplete,
            AssignedAssistantId = assistantId
        };
        db.PageTasks.Add(task);

        var attempt = TaskAssignmentAttempt.CreateAccepted(task.Id, assistantId, collab.Id, 1, mangakaId, DateTime.UtcNow, "Direct", DateTime.UtcNow.AddDays(1));
        db.TaskAssignmentAttempts.Add(attempt);

        await db.SaveChangesAsync();

        var repo = new StudioInvitationRepository(provider);
        var revocationService = new StudioTaskRevocationService(provider);
        var mockNotifications = new Moq.Mock<INotificationService>();

        var endHandler = new EndCollaborationHandler(repo, revocationService, mockNotifications.Object);

        // End collaboration
        await endHandler.Handle(new EndCollaborationCommand(collab.Id, mangakaId, false, "Contract Ended", collab.ConcurrencyToken), default);

        // Verification 1: Collaboration is Ended
        var updatedCollab = await db.MangakaAssistantCollaborations.FindAsync(collab.Id);
        Assert.NotNull(updatedCollab);
        Assert.Equal(CollaborationStatus.Ended, updatedCollab.Status);

        // Verification 2: Active SeriesAccessGrant is revoked
        var updatedGrant = await db.SeriesAccessGrants.FindAsync(grant.Id);
        Assert.NotNull(updatedGrant);
        Assert.False(updatedGrant.IsActive);
        Assert.NotNull(updatedGrant.RevokedAt);

        // Verification 3: Active Task is set to ReassignmentRequired (no orphan tasks)
        var updatedTask = await db.PageTasks.FindAsync(task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(PageTaskStatus.ReassignmentRequired, updatedTask.TaskStatus);

        // Verification 4: Active attempt is Cancelled
        var updatedAttempt = await db.TaskAssignmentAttempts.FindAsync(attempt.Id);
        Assert.NotNull(updatedAttempt);
        Assert.Equal(TaskAssignmentAttemptStatus.Cancelled, updatedAttempt.Status);

        // Verification 5: Assistant is returned to Admin free pool
        var adminUnassignedHandler = new GetAdminUnassignedAssistantsHandler(repo);
        var freePool = await adminUnassignedHandler.Handle(new GetAdminUnassignedAssistantsQuery(), default);
        Assert.Single(freePool.Assistants);
        Assert.Equal(assistantId, freePool.Assistants[0].AssistantId);
    }
}
