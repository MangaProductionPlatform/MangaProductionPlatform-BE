using MangaERP.Identity.Application.Commands.ProvisionAccount;
using MangaERP.Identity.Application.Commands.UpdateAccountStatus;
using MangaERP.Identity.Application.Commands.DeleteAccount;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Ranking.Application.Commands.ImportRankingCsv;
using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class TantouMandatoryAndRankingCsvTests
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

    [Fact]
    public async System.Threading.Tasks.Task ProvisionMangaka_WithoutTantou_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(r => r.PersonalEmailExistsActiveOrPendingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new ProvisionAccountHandler(
            userRepoMock.Object,
            Mock.Of<IInvitationTokenRepository>(),
            Mock.Of<ITokenService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<IUsernameGenerator>(),
            Mock.Of<IAssistantCollaborationProvisionPort>(),
            provider,
            Mock.Of<IConfiguration>());

        var command = new ProvisionAccountCommand("Nguyễn Văn A", "mangaka@test.local", UserRole.Mangaka, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async System.Threading.Tasks.Task ProvisionMangaka_WithActiveTantou_Succeeds()
    {
        using var db = CreateInMemoryDb();
        var provider = new TestDbContextProvider(db);

        var tantouId = Guid.NewGuid();
        var tantouUser = new User
        {
            Id = tantouId,
            Role = UserRole.TantouEditor,
            AccountStatus = AccountStatus.Active,
            IsDeleted = false
        };

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(r => r.PersonalEmailExistsActiveOrPendingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        userRepoMock.Setup(r => r.GetByIdAsync(tantouId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tantouUser);

        var usernameGenMock = new Mock<IUsernameGenerator>();
        usernameGenMock.Setup(g => g.GenerateAsync(It.IsAny<string>(), It.IsAny<UserRole>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("anguyen.mgk@company.com");

        var tokenServiceMock = new Mock<ITokenService>();
        tokenServiceMock.Setup(t => t.GenerateInvitationToken(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("token");

        var handler = new ProvisionAccountHandler(
            userRepoMock.Object,
            Mock.Of<IInvitationTokenRepository>(),
            tokenServiceMock.Object,
            Mock.Of<IEmailService>(),
            usernameGenMock.Object,
            Mock.Of<IAssistantCollaborationProvisionPort>(),
            provider,
            Mock.Of<IConfiguration>());

        var command = new ProvisionAccountCommand("Nguyễn Văn A", "mangaka@test.local", UserRole.Mangaka, null, tantouId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("anguyen.mgk@company.com", result.GeneratedUsername);
    }
}
