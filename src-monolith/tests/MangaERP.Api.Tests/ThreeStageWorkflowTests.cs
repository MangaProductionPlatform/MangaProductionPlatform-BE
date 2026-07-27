using MangaERP.Identity.Application.Commands.ProvisionAccount;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Identity.Infrastructure.Repositories;
using MangaERP.Identity.Infrastructure.Services;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Shared.Infrastructure.Services;
using MangaERP.Studio.Application.Commands.SeriesAccess;
using MangaERP.Studio.Application.Queries.GetMyAssistants;
using MangaERP.Studio.Application.Queries.GetAssistantDetail;
using MangaERP.Studio.Domain.Entities;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Task.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace MangaERP.Api.Tests;

public class ThreeStageWorkflowTests
{
    private sealed class TestDbContextProvider(AppDbContext context) : IDbContextProvider
    {
        public object GetDbContext() => context;
    }

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static IConfiguration GetTestConfig() => new ConfigurationBuilder().Build();

    [Fact]
    public async System.Threading.Tasks.Task Stage1_AdminProvisionAssistant_Success_WhenManagingMangakaIdIsProvided()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var mangaka = new User
        {
            Id = Guid.NewGuid(),
            Username = "mangaka1.mng@company.com",
            Email = "mangaka1.mng@company.com",
            Role = UserRole.Mangaka,
            AccountStatus = AccountStatus.Active
        };
        db.Users.Add(mangaka);
        await db.SaveChangesAsync();

        var userRepo = new UserRepository(provider);
        var invTokenRepo = new InvitationTokenRepository(provider);
        var tokenService = new Mock<ITokenService>();
        tokenService.Setup(x => x.GenerateInvitationToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("fake-token");
        var emailService = new Mock<IEmailService>();
        emailService.Setup(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(System.Threading.Tasks.Task.CompletedTask);
        var usernameGen = new Mock<IUsernameGenerator>();
        usernameGen.Setup(u => u.GenerateAsync(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>())).ReturnsAsync("ast.gen@company.com");

        var collabService = new AssistantCollaborationProvisionService(provider);
        var handler = new ProvisionAccountHandler(
            userRepo, invTokenRepo, tokenService.Object, emailService.Object, usernameGen.Object, collabService, provider, GetTestConfig());

        // Test 1: Admin creates Assistant with valid managingMangakaId
        var result = await handler.Handle(new ProvisionAccountCommand(
            "Assistant New", "assistant@gmail.com", UserRole.Assistant, null, null, mangaka.Id), default);

        Assert.NotNull(result);
        Assert.Equal("assistant@gmail.com", result.PersonalEmail);

        // Verify collaboration created in Accepted status
        var collab = await db.MangakaAssistantCollaborations.FirstOrDefaultAsync(c => c.AssistantId == result.UserId);
        Assert.NotNull(collab);
        Assert.Equal(mangaka.Id, collab.MangakaId);
        Assert.Equal(CollaborationStatus.Accepted, collab.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task Stage1_AdminProvisionAssistant_Succeeds_WhenManagingMangakaIdIsOptional()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var usernameGenMock = new Mock<IUsernameGenerator>();
        usernameGenMock.Setup(x => x.GenerateAsync(It.IsAny<string>(), UserRole.Assistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync("ast.new@company.com");

        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(System.Threading.Tasks.Task.CompletedTask);

        var tokenServiceMock = new Mock<ITokenService>();
        tokenServiceMock.Setup(t => t.GenerateInvitationToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("valid-invitation-jwt-token");

        var handler = new ProvisionAccountHandler(
            new UserRepository(provider), new InvitationTokenRepository(provider),
            tokenServiceMock.Object, emailServiceMock.Object, usernameGenMock.Object,
            new AssistantCollaborationProvisionService(provider), provider, GetTestConfig());

        // Test: Role Assistant without managingMangakaId succeeds and creates unassigned Assistant account
        var result = await handler.Handle(new ProvisionAccountCommand("Assistant New", "ast@gmail.com", UserRole.Assistant, null, null, null), default);

        Assert.NotNull(result);
        Assert.Equal("ast.new@company.com", result.GeneratedUsername);
        Assert.False(db.MangakaAssistantCollaborations.Any(c => c.AssistantId == result.UserId));
    }

    [Fact]
    public async System.Threading.Tasks.Task Stage1_AdminProvisionAssistant_Fails_WhenMangakaDoesNotExistOrInactiveOrNotMangakaRole()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var tantou = new User { Id = Guid.NewGuid(), Username = "tantou@co.com", Email = "tantou@co.com", Role = UserRole.TantouEditor, AccountStatus = AccountStatus.Active };
        var inactiveMangaka = new User { Id = Guid.NewGuid(), Username = "inact@co.com", Email = "inact@co.com", Role = UserRole.Mangaka, AccountStatus = AccountStatus.PendingActivation };
        db.Users.AddRange(tantou, inactiveMangaka);
        await db.SaveChangesAsync();

        var handler = new ProvisionAccountHandler(
            new UserRepository(provider), new InvitationTokenRepository(provider),
            Mock.Of<ITokenService>(), Mock.Of<IEmailService>(), Mock.Of<IUsernameGenerator>(),
            new AssistantCollaborationProvisionService(provider), provider, GetTestConfig());

        // Test 3: Mangaka does not exist
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ProvisionAccountCommand("Ast 1", "ast1@gmail.com", UserRole.Assistant, null, null, Guid.NewGuid()), default));

        // Test 4: Target user is Tantou (not Mangaka)
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ProvisionAccountCommand("Ast 2", "ast2@gmail.com", UserRole.Assistant, null, null, tantou.Id), default));

        // Test 5: Mangaka inactive
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new ProvisionAccountCommand("Ast 3", "ast3@gmail.com", UserRole.Assistant, null, null, inactiveMangaka.Id), default));
    }

    [Fact]
    public async System.Threading.Tasks.Task Stage2_GetMyAssistants_ReturnsOnlyCurrentMangakaAssistants_AndCorrectSeriesGrants()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var mangakaAId = Guid.NewGuid();
        var mangakaBId = Guid.NewGuid();

        var astA1 = new User { Id = Guid.NewGuid(), Username = "astA1.ast@company.com", Email = "astA1.ast@company.com", PersonalEmail = "astA1@gmail.com", Role = UserRole.Assistant, FullName = "Ast A1", AccountStatus = AccountStatus.Active };
        var astA2 = new User { Id = Guid.NewGuid(), Username = "astA2.ast@company.com", Email = "astA2.ast@company.com", PersonalEmail = "astA2@gmail.com", Role = UserRole.Assistant, FullName = "Ast A2", AccountStatus = AccountStatus.Active };
        var astB1 = new User { Id = Guid.NewGuid(), Username = "astB1.ast@company.com", Email = "astB1.ast@company.com", PersonalEmail = "astB1@gmail.com", Role = UserRole.Assistant, FullName = "Ast B1", AccountStatus = AccountStatus.Active };
        var astEnded = new User { Id = Guid.NewGuid(), Username = "astEnded.ast@company.com", Email = "astEnded.ast@company.com", PersonalEmail = "astEnded@gmail.com", Role = UserRole.Assistant, FullName = "Ast Ended", AccountStatus = AccountStatus.Active };

        db.Users.AddRange(astA1, astA2, astB1, astEnded);

        var collabA1 = new MangakaAssistantCollaboration(mangakaAId, astA1.Id, Guid.NewGuid(), DateTime.UtcNow);
        var collabA2 = new MangakaAssistantCollaboration(mangakaAId, astA2.Id, Guid.NewGuid(), DateTime.UtcNow);
        var collabB1 = new MangakaAssistantCollaboration(mangakaBId, astB1.Id, Guid.NewGuid(), DateTime.UtcNow);
        var collabEnded = new MangakaAssistantCollaboration(mangakaAId, astEnded.Id, Guid.NewGuid(), DateTime.UtcNow);
        collabEnded.End("Ended partnership", mangakaAId, DateTime.UtcNow);

        db.MangakaAssistantCollaborations.AddRange(collabA1, collabA2, collabB1, collabEnded);

        var series1Id = Guid.NewGuid();
        var grant1 = SeriesAccessGrant.Create(collabA1.Id, series1Id, mangakaAId);
        db.SeriesAccessGrants.Add(grant1);

        await db.SaveChangesAsync();

        var handler = new GetMyAssistantsHandler(
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider), new UserRepository(provider), GetTestConfig());

        // Test 9: My Assistants returns only current Mangaka A's active collaborations
        var responseA = await handler.Handle(new GetMyAssistantsQuery(mangakaAId), default);

        Assert.NotNull(responseA);
        Assert.Equal(2, responseA.Assistants.Count);

        // Test 10: Mangaka B's assistant (astB1) is NOT returned for Mangaka A
        Assert.DoesNotContain(responseA.Assistants, a => a.AssistantId == astB1.Id);

        // Test 11: Ended collaboration is NOT returned
        Assert.DoesNotContain(responseA.Assistants, a => a.AssistantId == astEnded.Id);

        // Test 12: SeriesAccessGrant is correctly mapped
        var dtoA1 = Assert.Single(responseA.Assistants, a => a.AssistantId == astA1.Id);
        Assert.Equal("Accepted", dtoA1.CollaborationStatus);
        var seriesAccess = Assert.Single(dtoA1.SeriesAccess);
        Assert.Equal(series1Id, seriesAccess.SeriesId);
        Assert.True(seriesAccess.IsActive);
    }

    [Fact]
    public async System.Threading.Tasks.Task RevokeSeriesAccess_CascadesAndMarksTasksReassignmentRequired_WithoutOrphans()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        var assistant = new User { Id = assistantId, Username = "ast.rev@co.com", Email = "ast.rev@co.com", PersonalEmail = "ast.rev@gmail.com", Role = UserRole.Assistant, FullName = "Ast Rev", AccountStatus = AccountStatus.Active };
        db.Users.Add(assistant);

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.Add(collab);

        var series = MangaSeries.Create(mangakaId, null, "Test Series", "Desc", null, null);
        typeof(MangaSeries).GetProperty("Id")!.SetValue(series, seriesId);
        db.MangaSeries.Add(series);

        var grant = SeriesAccessGrant.Create(collab.Id, seriesId, mangakaId);
        db.SeriesAccessGrants.Add(grant);

        var chapter = ChapterEntity.Create(seriesId, "Ch1", 1, 10);
        typeof(ChapterEntity).GetProperty("Id")!.SetValue(chapter, chapterId);
        db.Chapters.Add(chapter);

        var task = PageTask.CreatePending(chapterId, 1, "https://example.com/base.png");
        task.AssignDirect(assistantId, "Do lineart", DateTime.UtcNow.AddDays(1));
        db.PageTasks.Add(task);

        var attempt = TaskAssignmentAttempt.CreateAccepted(task.Id, assistantId, collab.Id, 1, mangakaId, DateTime.UtcNow, "Direct", DateTime.UtcNow.AddDays(1));
        db.TaskAssignmentAttempts.Add(attempt);

        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var revocationService = new StudioTaskRevocationService(provider);
        var handler = new RevokeSeriesAccessHandler(
            new StudioInvitationRepository(provider),
            new SeriesAccessGrantRepository(provider),
            new SeriesRepository(provider),
            notifMock.Object,
            revocationService);

        await handler.Handle(new RevokeSeriesAccessCommand(collab.Id, seriesId, mangakaId, "Revoking access"), default);

        // Verify grant revoked
        var updatedGrant = await db.SeriesAccessGrants.FirstOrDefaultAsync(g => g.Id == grant.Id);
        Assert.NotNull(updatedGrant);
        Assert.False(updatedGrant.IsActive);
        Assert.NotNull(updatedGrant.RevokedAt);

        // Verify task reassignment required & AssignedAssistantId cleared (no orphan task)
        var updatedTask = await db.PageTasks.FirstOrDefaultAsync(t => t.Id == task.Id);
        Assert.NotNull(updatedTask);
        Assert.Equal(PageTaskStatus.ReassignmentRequired, updatedTask.TaskStatus);
        Assert.Null(updatedTask.AssignedAssistantId);

        // Verify attempt cancelled
        var updatedAttempt = await db.TaskAssignmentAttempts.FirstOrDefaultAsync(a => a.Id == attempt.Id);
        Assert.NotNull(updatedAttempt);
        Assert.Equal(TaskAssignmentAttemptStatus.Cancelled, updatedAttempt.Status);

        // Verify notifications sent to assistant and mangaka
        notifMock.Verify(n => n.NotifyCollaborationEventAsync(assistantId, "SeriesAccessRevoked", It.IsAny<string>(), It.IsAny<string>(), seriesId, It.IsAny<CancellationToken>()), Times.Once);
        notifMock.Verify(n => n.NotifyCollaborationEventAsync(mangakaId, "SeriesAccessRevoked", It.IsAny<string>(), It.IsAny<string>(), seriesId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task GetAssistantDetail_EnforcesCrossMangakaIsolation_AndReturnsCorrectDetail()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var mangakaAId = Guid.NewGuid();
        var mangakaBId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();

        var assistant = new User { Id = assistantId, Username = "ast.det@co.com", Email = "ast.det@co.com", PersonalEmail = "ast.det@gmail.com", Role = UserRole.Assistant, FullName = "Ast Detail", AccountStatus = AccountStatus.Active };
        db.Users.Add(assistant);

        var collabA = new MangakaAssistantCollaboration(mangakaAId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.Add(collabA);

        await db.SaveChangesAsync();

        var handler = new GetAssistantDetailHandler(
            new StudioInvitationRepository(provider),
            new SeriesAccessGrantRepository(provider),
            new UserRepository(provider),
            GetTestConfig());

        // Mangaka A can view detail
        var detailA = await handler.Handle(new GetAssistantDetailQuery(mangakaAId, assistantId), default);
        Assert.NotNull(detailA);
        Assert.Equal(assistantId, detailA.AssistantId);
        Assert.Equal("Ast Detail", detailA.DisplayName);
        Assert.Equal("Accepted", detailA.CollaborationStatus);

        // Mangaka B cannot view detail (throws UnauthorizedAccessException)
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new GetAssistantDetailQuery(mangakaBId, assistantId), default));
    }

    [Fact]
    public async System.Threading.Tasks.Task GetUnassignedAssistants_ReturnsOnlyFreeAssistants()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var mangakaId = Guid.NewGuid();
        var mangaka = new User
        {
            Id = mangakaId,
            Username = "mangaka_unassigned",
            Email = "mgk_u@test.local",
            PasswordHash = "hash",
            Role = UserRole.Mangaka,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(mangaka);

        // Assistant 1: Free (never had a collaboration)
        var astFree1 = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_free_1",
            Email = "ast_free_1@test.local",
            FullName = "Assistant Free 1",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // Assistant 2: Occupied with another Mangaka (Status = Accepted)
        var astOccupied = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_occupied",
            Email = "ast_occupied@test.local",
            FullName = "Assistant Occupied",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        // Assistant 3: Free (previous collaboration is Ended)
        var astEnded = new User
        {
            Id = Guid.NewGuid(),
            Username = "ast_ended",
            Email = "ast_ended@test.local",
            FullName = "Assistant Ended",
            PasswordHash = "hash",
            Role = UserRole.Assistant,
            AccountStatus = AccountStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.AddRange(astFree1, astOccupied, astEnded);

        // Active collaboration for astOccupied (created with Status = Accepted by default)
        var activeCollab = new MangakaAssistantCollaboration(Guid.NewGuid(), astOccupied.Id, Guid.NewGuid(), DateTime.UtcNow);

        // Ended collaboration for astEnded
        var endedCollab = new MangakaAssistantCollaboration(Guid.NewGuid(), astEnded.Id, Guid.NewGuid(), DateTime.UtcNow);
        endedCollab.End("Ended contract", mangakaId, DateTime.UtcNow);

        db.MangakaAssistantCollaborations.AddRange(activeCollab, endedCollab);
        await db.SaveChangesAsync();

        var repo = new StudioInvitationRepository(provider);
        var handler = new MangaERP.Studio.Application.Queries.GetUnassignedAssistants.GetUnassignedAssistantsHandler(repo);

        var result = await handler.Handle(new MangaERP.Studio.Application.Queries.GetUnassignedAssistants.GetUnassignedAssistantsQuery(mangakaId), default);

        Assert.NotNull(result);
        Assert.Equal(2, result.UnassignedAssistants.Count);
        Assert.Contains(result.UnassignedAssistants, x => x.UserId == astFree1.Id);
        Assert.Contains(result.UnassignedAssistants, x => x.UserId == astEnded.Id);
        Assert.DoesNotContain(result.UnassignedAssistants, x => x.UserId == astOccupied.Id);
    }
}
