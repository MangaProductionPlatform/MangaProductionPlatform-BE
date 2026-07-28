using MangaERP.Identity.Application.Commands.ProvisionAccount;
using MangaERP.Identity.Application.Commands.ResendActivation;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Identity.Infrastructure.Repositories;
using MangaERP.Identity.Infrastructure.Services;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Task.Application.Commands.ReassignTask;
using MangaERP.Task.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Reflection;
using Xunit;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using PageTaskEntity = MangaERP.Chapter.Domain.Entities.PageTask;
using PageTaskType = MangaERP.Chapter.Domain.Entities.PageTaskType;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class ProvisionAccountExecutionStrategyTests
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
    public async STTask ProvisionAccount_MangakaRole_Success_UnderExecutionStrategy()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var tantou = new User
        {
            Id = Guid.NewGuid(),
            Username = "tantou1.te@company.com",
            Email = "tantou1.te@company.com",
            Role = UserRole.TantouEditor,
            AccountStatus = AccountStatus.Active
        };
        db.Users.Add(tantou);
        await db.SaveChangesAsync();

        var userRepo = new UserRepository(provider);
        var invTokenRepo = new InvitationTokenRepository(provider);

        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(STTask.CompletedTask);

        var tokenServiceMock = new Mock<ITokenService>();
        tokenServiceMock.Setup(t => t.GenerateInvitationToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("invitation-token-mangaka");

        var usernameGenMock = new Mock<IUsernameGenerator>();
        usernameGenMock.Setup(u => u.GenerateAsync(It.IsAny<string>(), UserRole.Mangaka, It.IsAny<CancellationToken>()))
            .ReturnsAsync("mangaka.new@company.com");

        var collabService = new AssistantCollaborationProvisionService(provider);

        var handler = new ProvisionAccountHandler(
            userRepo, invTokenRepo, tokenServiceMock.Object, emailServiceMock.Object, usernameGenMock.Object, collabService, provider, GetTestConfig());

        var command = new ProvisionAccountCommand(
            FullName: "Mangaka Test",
            PersonalEmail: "mangaka.test@gmail.com",
            Role: UserRole.Mangaka,
            PhoneNumber: "0901234567",
            ManagingTantouId: tantou.Id
        );

        var result = await handler.Handle(command, default);

        Assert.NotNull(result);
        Assert.Equal("mangaka.new@company.com", result.GeneratedUsername);
        Assert.Equal("mangaka.test@gmail.com", result.PersonalEmail);
        Assert.Equal("Mangaka", result.Role);
        Assert.Equal("PendingActivation", result.Status);
        Assert.Null(result.Warning);

        // Verify User persisted in DB
        var userInDb = await db.Users.FirstOrDefaultAsync(u => u.Id == result.UserId);
        Assert.NotNull(userInDb);
        Assert.Equal("Mangaka Test", userInDb.FullName);
        Assert.Equal(tantou.Id, userInDb.ManagingTantouId);

        // Verify InvitationToken persisted in DB
        var tokenInDb = await db.InvitationTokens.FirstOrDefaultAsync(t => t.UserId == result.UserId);
        Assert.NotNull(tokenInDb);
        Assert.Equal("invitation-token-mangaka", tokenInDb.Token);

        // Verify email service called exactly once after commit
        emailServiceMock.Verify(e => e.SendInvitationEmailAsync(
            "mangaka.test@gmail.com", It.Is<string>(link => link.Contains("invitation-token-mangaka")), "mangaka.new@company.com", "Mangaka Test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async STTask ProvisionAccount_AssistantRole_WithManagingMangakaId_CreatesAcceptedCollaboration()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var mangaka = new User
        {
            Id = Guid.NewGuid(),
            Username = "mangaka.owner@company.com",
            Email = "mangaka.owner@company.com",
            Role = UserRole.Mangaka,
            AccountStatus = AccountStatus.Active
        };
        db.Users.Add(mangaka);
        await db.SaveChangesAsync();

        var userRepo = new UserRepository(provider);
        var invTokenRepo = new InvitationTokenRepository(provider);

        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(STTask.CompletedTask);

        var tokenServiceMock = new Mock<ITokenService>();
        tokenServiceMock.Setup(t => t.GenerateInvitationToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("invitation-token-assistant");

        var usernameGenMock = new Mock<IUsernameGenerator>();
        usernameGenMock.Setup(u => u.GenerateAsync(It.IsAny<string>(), UserRole.Assistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync("assistant.new@company.com");

        var collabService = new AssistantCollaborationProvisionService(provider);

        var handler = new ProvisionAccountHandler(
            userRepo, invTokenRepo, tokenServiceMock.Object, emailServiceMock.Object, usernameGenMock.Object, collabService, provider, GetTestConfig());

        var command = new ProvisionAccountCommand(
            FullName: "Assistant Test",
            PersonalEmail: "assistant.test@gmail.com",
            Role: UserRole.Assistant,
            PhoneNumber: null,
            ManagingTantouId: null,
            ManagingMangakaId: mangaka.Id
        );

        var result = await handler.Handle(command, default);

        Assert.NotNull(result);
        Assert.Equal("assistant.new@company.com", result.GeneratedUsername);
        Assert.Equal("assistant.test@gmail.com", result.PersonalEmail);

        // Verify collaboration created with status Accepted
        var collab = await db.MangakaAssistantCollaborations.FirstOrDefaultAsync(c => c.AssistantId == result.UserId);
        Assert.NotNull(collab);
        Assert.Equal(mangaka.Id, collab.MangakaId);
        Assert.Equal(CollaborationStatus.Accepted, collab.Status);
    }

    [Fact]
    public async STTask ProvisionAccount_AssistantRole_WithoutManagingMangakaId_CreatesFreeAssistant_EntersUnassignedPool()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var userRepo = new UserRepository(provider);
        var invTokenRepo = new InvitationTokenRepository(provider);

        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(STTask.CompletedTask);

        var tokenServiceMock = new Mock<ITokenService>();
        tokenServiceMock.Setup(t => t.GenerateInvitationToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("invitation-token-free-assistant");

        var usernameGenMock = new Mock<IUsernameGenerator>();
        usernameGenMock.Setup(u => u.GenerateAsync(It.IsAny<string>(), UserRole.Assistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync("free.ast@company.com");

        var collabService = new AssistantCollaborationProvisionService(provider);

        var handler = new ProvisionAccountHandler(
            userRepo, invTokenRepo, tokenServiceMock.Object, emailServiceMock.Object, usernameGenMock.Object, collabService, provider, GetTestConfig());

        var command = new ProvisionAccountCommand(
            FullName: "Free Assistant",
            PersonalEmail: "free.ast@gmail.com",
            Role: UserRole.Assistant,
            PhoneNumber: null,
            ManagingTantouId: null,
            ManagingMangakaId: null
        );

        var result = await handler.Handle(command, default);

        Assert.NotNull(result);
        Assert.Equal("free.ast@company.com", result.GeneratedUsername);
        Assert.Equal("free.ast@gmail.com", result.PersonalEmail);

        // Verify NO collaboration, NO SeriesAccessGrant, NO Task created
        Assert.Empty(db.MangakaAssistantCollaborations.Where(c => c.AssistantId == result.UserId));
        Assert.Empty(db.SeriesAccessGrants);
        Assert.Empty(db.PageTasks.Where(t => t.AssignedAssistantId == result.UserId));

        // When user transitions to Active, verify unassigned assistants query returns this user
        var createdUser = await db.Users.FirstAsync(u => u.Id == result.UserId);
        createdUser.AccountStatus = AccountStatus.Active;
        await db.SaveChangesAsync();

        var studioRepo = new StudioInvitationRepository(provider);
        var unassigned = await studioRepo.GetAdminUnassignedAssistantsAsync(default);

        Assert.Contains(unassigned, a => a.AssistantId == result.UserId && a.DisplayName == "Free Assistant");
    }

    [Fact]
    public async STTask ProvisionAccount_EmailFailureAfterDbCommit_PreservesUser_AndReturnsWarning()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var tantou = new User
        {
            Id = Guid.NewGuid(),
            Username = "tantou2.te@company.com",
            Email = "tantou2.te@company.com",
            Role = UserRole.TantouEditor,
            AccountStatus = AccountStatus.Active
        };
        db.Users.Add(tantou);
        await db.SaveChangesAsync();

        var userRepo = new UserRepository(provider);
        var invTokenRepo = new InvitationTokenRepository(provider);

        // Mock email service to throw an exception
        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Net.Mail.SmtpException("SMTP connection timeout"));

        var tokenServiceMock = new Mock<ITokenService>();
        tokenServiceMock.Setup(t => t.GenerateInvitationToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("token-email-failed");

        var usernameGenMock = new Mock<IUsernameGenerator>();
        usernameGenMock.Setup(u => u.GenerateAsync(It.IsAny<string>(), UserRole.Mangaka, It.IsAny<CancellationToken>()))
            .ReturnsAsync("mangaka.emailfail@company.com");

        var handler = new ProvisionAccountHandler(
            userRepo, invTokenRepo, tokenServiceMock.Object, emailServiceMock.Object, usernameGenMock.Object,
            new AssistantCollaborationProvisionService(provider), provider, GetTestConfig());

        var command = new ProvisionAccountCommand(
            FullName: "Email Fail Test",
            PersonalEmail: "emailfail@gmail.com",
            Role: UserRole.Mangaka,
            ManagingTantouId: tantou.Id
        );

        var result = await handler.Handle(command, default);

        // Result should indicate 201 success with warning message
        Assert.NotNull(result);
        Assert.Equal("mangaka.emailfail@company.com", result.GeneratedUsername);
        Assert.NotNull(result.Warning);
        Assert.Contains("activation email failed to send", result.Warning);

        // Verify User was NOT deleted and remains in PendingActivation state
        var userInDb = await db.Users.FirstOrDefaultAsync(u => u.Id == result.UserId);
        Assert.NotNull(userInDb);
        Assert.Equal(AccountStatus.PendingActivation, userInDb.AccountStatus);

        // Verify Token was NOT deleted
        var tokenInDb = await db.InvitationTokens.FirstOrDefaultAsync(t => t.UserId == result.UserId);
        Assert.NotNull(tokenInDb);

        // Verify retrying request throws UserAlreadyExistsException and does not duplicate User
        await Assert.ThrowsAsync<UserAlreadyExistsException>(() => handler.Handle(command, default));
        Assert.Equal(1, db.Users.Count(u => u.PersonalEmail == "emailfail@gmail.com"));
        Assert.Equal(1, db.InvitationTokens.Count(t => t.UserId == result.UserId));
    }

    [Fact]
    public async STTask ResendActivation_Success_SendsActivationEmailForExistingPendingUser()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "pending.user@company.com",
            Email = "pending.user@company.com",
            PersonalEmail = "pending.user@gmail.com",
            NormalizedPersonalEmail = "pending.user@gmail.com",
            FullName = "Pending User",
            Role = UserRole.Mangaka,
            AccountStatus = AccountStatus.PendingActivation,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var userRepo = new UserRepository(provider);
        var invTokenRepo = new InvitationTokenRepository(provider);

        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(STTask.CompletedTask);

        var tokenServiceMock = new Mock<ITokenService>();
        tokenServiceMock.Setup(t => t.GenerateInvitationToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("resent-invitation-token");

        var handler = new ResendActivationHandler(userRepo, invTokenRepo, tokenServiceMock.Object, emailServiceMock.Object, GetTestConfig());
        await handler.Handle(new ResendActivationCommand(user.Id), default);

        emailServiceMock.Verify(e => e.SendInvitationEmailAsync(
            "pending.user@gmail.com", It.Is<string>(link => link.Contains("resent-invitation-token")), "pending.user@company.com", "Pending User", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async STTask ReassignTask_UnderExecutionStrategy_PreservesState_AndBlocksCrossMangaka()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        Guid mangaka1Id = Guid.NewGuid();
        Guid mangaka2Id = Guid.NewGuid();
        Guid assistant1Id = Guid.NewGuid();
        Guid assistant2Id = Guid.NewGuid();
        Guid seriesId = Guid.NewGuid();
        Guid chapterId = Guid.NewGuid();
        Guid taskId = Guid.NewGuid();

        var mangaka1 = new User { Id = mangaka1Id, Username = "mng1@co.com", Email = "mng1@co.com", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var mangaka2 = new User { Id = mangaka2Id, Username = "mng2@co.com", Email = "mng2@co.com", Role = UserRole.Mangaka, AccountStatus = AccountStatus.Active };
        var ast1 = new User { Id = assistant1Id, Username = "ast1@co.com", Email = "ast1@co.com", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        var ast2 = new User { Id = assistant2Id, Username = "ast2@co.com", Email = "ast2@co.com", Role = UserRole.Assistant, AccountStatus = AccountStatus.Active };
        db.Users.AddRange(mangaka1, mangaka2, ast1, ast2);

        var collab1 = new MangakaAssistantCollaboration(mangaka1Id, assistant1Id, Guid.NewGuid(), DateTime.UtcNow);
        var collab2 = new MangakaAssistantCollaboration(mangaka1Id, assistant2Id, Guid.NewGuid(), DateTime.UtcNow);
        db.MangakaAssistantCollaborations.AddRange(collab1, collab2);

        var series = MangaSeries.Create(mangaka1Id, null, "Reassign Test Series", "Desc", "Action", null);
        GetEntityIdProperty(typeof(MangaSeries)).SetValue(series, seriesId);
        db.MangaSeries.Add(series);

        var grant1 = SeriesAccessGrant.Create(collab1.Id, seriesId, mangaka1Id);
        var grant2 = SeriesAccessGrant.Create(collab2.Id, seriesId, mangaka1Id);
        db.SeriesAccessGrants.AddRange(grant1, grant2);

        var chapter = ChapterEntity.Create(seriesId, "Ch 1", 1, 10);
        GetEntityIdProperty(typeof(ChapterEntity)).SetValue(chapter, chapterId);
        db.Chapters.Add(chapter);

        var task = new PageTaskEntity
        {
            ChapterId = chapterId,
            PageNumber = 1,
            TaskType = PageTaskType.Background,
            Description = "Initial Task",
            Deadline = DateTime.UtcNow.AddDays(3),
            AssignedAssistantId = assistant1Id
        };
        typeof(PageTaskEntity).GetProperty("BaseImageUrl", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(task, "http://example.com/base.png");
        GetEntityIdProperty(typeof(PageTaskEntity)).SetValue(task, taskId);
        db.PageTasks.Add(task);

        var attempt1 = TaskAssignmentAttempt.CreateAccepted(taskId, assistant1Id, collab1.Id, 1, mangaka1Id, assignedAt: DateTime.UtcNow, assignmentRole: "Direct", workDeadline: DateTime.UtcNow.AddDays(3));
        db.TaskAssignmentAttempts.Add(attempt1);
        task.CurrentAssignmentAttemptId = attempt1.Id;

        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();

        var handler = new ReassignTaskHandler(
            new PageTaskRepository(provider), new ChapterRepository(provider), new SeriesRepository(provider),
            new StudioInvitationRepository(provider), new SeriesAccessGrantRepository(provider),
            new TaskAssignmentRepository(provider), notifMock.Object, GetTestConfig(), provider);

        // Test 1: Cross-Mangaka assignment blocked (mangaka2 owns no series)
        var crossCmd = new ReassignTaskCommand(taskId, assistant2Id, mangaka2Id, "Reassign reason");
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(crossCmd, default));

        // Test 2: Valid Reassign by owner Mangaka
        var validCmd = new ReassignTaskCommand(taskId, assistant2Id, mangaka1Id, "Valid reassignment");
        var reassignResult = await handler.Handle(validCmd, default);

        Assert.NotNull(reassignResult);
        Assert.Equal(taskId, reassignResult.TaskId);
        Assert.Equal(assistant2Id, reassignResult.Attempt.AssistantId);
        Assert.Equal("Accepted", reassignResult.Attempt.Status);

        // Verify old attempt superseded and new attempt active
        var attemptsInDb = await db.TaskAssignmentAttempts.Where(a => a.TaskId == taskId).ToListAsync();
        Assert.Equal(2, attemptsInDb.Count);
        Assert.Equal(TaskAssignmentAttemptStatus.Superseded, attemptsInDb.First(a => a.AssistantId == assistant1Id).Status);
        Assert.Equal(TaskAssignmentAttemptStatus.Accepted, attemptsInDb.First(a => a.AssistantId == assistant2Id).Status);

        // Verify task updated
        var updatedTask = await db.PageTasks.FirstAsync(t => t.Id == taskId);
        Assert.Equal(assistant2Id, updatedTask.AssignedAssistantId);
    }

    private static PropertyInfo GetEntityIdProperty(Type type)
    {
        var prop = type.GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop != null) return prop;
        return type.BaseType!.GetProperty("Id", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!;
    }
}
