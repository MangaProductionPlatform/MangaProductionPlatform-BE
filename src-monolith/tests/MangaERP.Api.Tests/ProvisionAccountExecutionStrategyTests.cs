using MangaERP.Identity.Application.Commands.ProvisionAccount;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Identity.Infrastructure.Repositories;
using MangaERP.Identity.Infrastructure.Services;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Shared.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
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
    public async STTask ProvisionAccount_AssistantRole_Success_WithCollaboration()
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

        // Verify collaboration created
        var collab = await db.MangakaAssistantCollaborations.FirstOrDefaultAsync(c => c.AssistantId == result.UserId);
        Assert.NotNull(collab);
        Assert.Equal(mangaka.Id, collab.MangakaId);
        Assert.Equal(MangaERP.Studio.Domain.Entities.CollaborationStatus.Accepted, collab.Status);
    }

    [Fact]
    public async STTask ProvisionAccount_ExistingEmail_ThrowsUserAlreadyExistsException_AndDoesNotSendEmail()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var existingUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "user.exist@company.com",
            Email = "user.exist@company.com",
            PersonalEmail = "duplicate@gmail.com",
            NormalizedPersonalEmail = "duplicate@gmail.com",
            Role = UserRole.Mangaka,
            AccountStatus = AccountStatus.Active
        };
        db.Users.Add(existingUser);
        await db.SaveChangesAsync();

        var userRepo = new UserRepository(provider);
        var invTokenRepo = new InvitationTokenRepository(provider);
        var emailServiceMock = new Mock<IEmailService>();

        var handler = new ProvisionAccountHandler(
            userRepo, invTokenRepo, Mock.Of<ITokenService>(), emailServiceMock.Object, Mock.Of<IUsernameGenerator>(),
            new AssistantCollaborationProvisionService(provider), provider, GetTestConfig());

        var command = new ProvisionAccountCommand(
            FullName: "Duplicate User",
            PersonalEmail: "duplicate@gmail.com",
            Role: UserRole.Mangaka,
            ManagingTantouId: Guid.NewGuid()
        );

        await Assert.ThrowsAsync<UserAlreadyExistsException>(() => handler.Handle(command, default));

        // Verify no email was dispatched
        emailServiceMock.Verify(e => e.SendInvitationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async STTask ProvisionAccount_MissingTantouForMangaka_ThrowsInvalidOperationException_AndNoDbChanges()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var handler = new ProvisionAccountHandler(
            new UserRepository(provider), new InvitationTokenRepository(provider),
            Mock.Of<ITokenService>(), Mock.Of<IEmailService>(), Mock.Of<IUsernameGenerator>(),
            new AssistantCollaborationProvisionService(provider), provider, GetTestConfig());

        var command = new ProvisionAccountCommand(
            FullName: "No Tantou Mangaka",
            PersonalEmail: "notantou@gmail.com",
            Role: UserRole.Mangaka,
            ManagingTantouId: null
        );

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, default));

        Assert.Empty(db.Users.Where(u => u.PersonalEmail == "notantou@gmail.com"));
        Assert.Empty(db.InvitationTokens);
    }
}
