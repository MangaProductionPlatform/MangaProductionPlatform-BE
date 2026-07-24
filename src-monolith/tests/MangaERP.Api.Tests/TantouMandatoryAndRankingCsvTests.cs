using MangaERP.Identity.Application.Commands.ProvisionAccount;
using MangaERP.Identity.Application.Commands.UpdateAccountStatus;
using MangaERP.Identity.Application.Commands.DeleteAccount;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Ranking.Application.Commands.ImportRankingCsv;
using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using System.Text;
using Task = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class TantouMandatoryAndRankingCsvTests
{
    [Fact]
    public async System.Threading.Tasks.Task ProvisionMangaka_WithoutTantou_ThrowsInvalidOperationException()
    {
        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(r => r.PersonalEmailExistsActiveOrPendingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new ProvisionAccountHandler(
            userRepoMock.Object,
            Mock.Of<IInvitationTokenRepository>(),
            Mock.Of<ITokenService>(),
            Mock.Of<IEmailService>(),
            Mock.Of<IUsernameGenerator>(),
            Mock.Of<IConfiguration>());

        var command = new ProvisionAccountCommand("Nguyễn Văn A", "mangaka@test.local", UserRole.Mangaka, null, null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async System.Threading.Tasks.Task ProvisionMangaka_WithActiveTantou_Succeeds()
    {
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
            Mock.Of<IConfiguration>());

        var command = new ProvisionAccountCommand("Nguyễn Văn A", "mangaka@test.local", UserRole.Mangaka, null, tantouId);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("anguyen.mgk@company.com", result.GeneratedUsername);
        userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u => u.ManagingTantouId == tantouId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task DeletingTantou_WithManagedMangaka_ThrowsInvalidOperationException()
    {
        var tantouId = Guid.NewGuid();
        var tantouUser = new User
        {
            Id = tantouId,
            Role = UserRole.TantouEditor,
            AccountStatus = AccountStatus.Active
        };

        var userRepoMock = new Mock<IUserRepository>();
        userRepoMock.Setup(r => r.GetByIdAsync(tantouId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tantouUser);
        userRepoMock.Setup(r => r.HasAssignedMangakasAsync(tantouId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new DeleteAccountHandler(userRepoMock.Object, Mock.Of<IRefreshTokenRepository>());

        var command = new DeleteAccountCommand(tantouId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async System.Threading.Tasks.Task ImportRankingCsv_ValidContent_DryRun_ReturnsSuccess()
    {
        var seriesId = Guid.NewGuid();
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("SeriesId,Rank,Score,Views,Likes");
        csvBuilder.AppendLine($"{seriesId},1,95.5,1000,500");

        var rankingRepoMock = new Mock<IRankingRepository>();
        rankingRepoMock.Setup(r => r.GetValidSeriesIdsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<Guid> { seriesId });

        var handler = new ImportRankingCsvHandler(rankingRepoMock.Object);

        var command = new ImportRankingCsvCommand(
            UploaderId: Guid.NewGuid(),
            Filename: "rankings.csv",
            FileBytes: Encoding.UTF8.GetBytes(csvBuilder.ToString()),
            Period: RankingPeriod.Weekly,
            DryRun: true);

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.Success);
        Assert.True(result.IsDryRun);
        Assert.Equal(1, result.TotalRows);
        Assert.Empty(result.ValidationErrors);
    }
}
