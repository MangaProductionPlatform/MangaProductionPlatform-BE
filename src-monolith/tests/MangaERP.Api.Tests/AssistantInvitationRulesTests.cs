using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Studio.Application.Commands.InviteAssistant;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using Moq;
using Xunit;

namespace MangaERP.Api.Tests;

public class AssistantInvitationRulesTests
{
    [Fact]
    public async System.Threading.Tasks.Task NewAccountInvitationNormalizesEmailAndPersistsBeforeRegistrationDelivery()
    {
        var mangaka = Guid.NewGuid();
        var series = MangaSeries.Create(mangaka, null, "Series", null, null, null);
        var userId = Guid.NewGuid();
        var actions = new List<string>();
        StudioInvitation? captured = null;
        var repo = Repository(series.Id, []);
        repo.Setup(x => x.AddAsync(It.IsAny<StudioInvitation>(), It.IsAny<CancellationToken>()))
            .Callback<StudioInvitation, CancellationToken>((x, _) => { captured = x; actions.Add("invitation"); })
            .Returns(System.Threading.Tasks.Task.CompletedTask);
        repo.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => actions.Add("commit")).ReturnsAsync(1);
        repo.Setup(x => x.UpdateAsync(It.IsAny<StudioInvitation>(), It.IsAny<CancellationToken>()))
            .Callback(() => actions.Add("delivery-state")).Returns(System.Threading.Tasks.Task.CompletedTask);
        var identity = new Mock<IStudioIdentityService>();
        identity.Setup(x => x.IsInternalEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        identity.Setup(x => x.FindActiveAssistantByEmailAsync("person@example.com", It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);
        identity.Setup(x => x.ProvisionAssistantAccountAsync("person@example.com", null, series.Title, It.IsAny<CancellationToken>())).ReturnsAsync((userId, "token"));
        identity.Setup(x => x.SendStudioInvitationNotificationAsync(userId, It.IsAny<Guid>(), It.IsAny<string>(), series.Title, It.IsAny<CancellationToken>()))
            .Callback(() => actions.Add("notification")).Returns(System.Threading.Tasks.Task.CompletedTask);
        identity.Setup(x => x.SendAssistantRegistrationEmailAsync(userId, "token", It.IsAny<CancellationToken>()))
            .Callback(() => actions.Add("registration")).Returns(System.Threading.Tasks.Task.CompletedTask);

        var grantRepo = new Mock<ISeriesAccessGrantRepository>();
        var result = await new InviteAssistantHandler(repo.Object, grantRepo.Object, identity.Object, SeriesRepository(series).Object)
            .Handle(new(mangaka, series.Id, "  Person@Example.COM ", null), default);

        Assert.Equal("person@example.com", result.AssistantEmail);
        Assert.Equal("NewAccount", result.Case);
        Assert.Equal("person@example.com", captured?.NormalizedAssistantEmail);
    }

    [Fact]
    public async System.Threading.Tasks.Task InternalEmployeeAddressIsRejected()
    {
        var mangaka = Guid.NewGuid();
        var series = MangaSeries.Create(mangaka, null, "Series", null, null, null);
        var identity = new Mock<IStudioIdentityService>();
        identity.Setup(x => x.IsInternalEmailAsync("editor@company.internal", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var grantRepo = new Mock<ISeriesAccessGrantRepository>();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new InviteAssistantHandler(Repository(series.Id, []).Object, grantRepo.Object, identity.Object, SeriesRepository(series).Object)
                .Handle(new(mangaka, series.Id, "editor@company.internal", null), default));
    }

    [Fact]
    public async System.Threading.Tasks.Task NonEndedCollaborationPreventsNewInvitationForSameAssistant()
    {
        var mangaka = Guid.NewGuid();
        var series = MangaSeries.Create(mangaka, null, "Series", null, null, null);
        var assistantId = Guid.NewGuid();
        var repo = Repository(series.Id, []);
        repo.Setup(x => x.HasNonEndedCollaborationAsync(assistantId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(x => x.GetNonEndedCollaborationsByMangakaAsync(mangaka, It.IsAny<CancellationToken>())).ReturnsAsync(Enumerable.Empty<MangakaAssistantCollaboration>());
        var identity = new Mock<IStudioIdentityService>();
        identity.Setup(x => x.IsInternalEmailAsync("assistant@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        identity.Setup(x => x.FindActiveAssistantByEmailAsync("assistant@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(assistantId);
        var grantRepo = new Mock<ISeriesAccessGrantRepository>();

        await Assert.ThrowsAsync<MangaERP.Shared.Domain.Exceptions.ConflictException>(() =>
            new InviteAssistantHandler(repo.Object, grantRepo.Object, identity.Object, SeriesRepository(series).Object)
                .Handle(new(mangaka, series.Id, "assistant@example.com", null), default));
    }

    private static Mock<IStudioInvitationRepository> Repository(Guid seriesId, IEnumerable<StudioInvitation> invitations)
    {
        var mock = new Mock<IStudioInvitationRepository>();
        mock.Setup(x => x.GetBySeriesIdAsync(seriesId, It.IsAny<CancellationToken>())).ReturnsAsync(invitations);
        mock.Setup(x => x.HasPendingForMangakaEmailAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        return mock;
    }

    private static Mock<ISeriesRepository> SeriesRepository(MangaSeries series)
    {
        var mock = new Mock<ISeriesRepository>();
        mock.Setup(x => x.GetByIdAsync(series.Id, It.IsAny<CancellationToken>())).ReturnsAsync(series);
        return mock;
    }
}
