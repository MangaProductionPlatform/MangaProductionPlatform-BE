using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Studio.Application.Commands.InviteAssistant;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using Moq;

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

        var result = await new InviteAssistantHandler(repo.Object, identity.Object, SeriesRepository(series).Object)
            .Handle(new(mangaka, series.Id, "  Person@Example.COM ", null), default);

        Assert.Equal("person@example.com", result.AssistantEmail);
        Assert.Equal("person@example.com", captured!.NormalizedAssistantEmail);
        Assert.Equal(["invitation", "notification", "commit", "registration", "delivery-state", "commit"], actions);
        Assert.Equal(userId, captured.AssistantUserId);
        Assert.Equal("token", captured.ActivationToken);
        Assert.Equal(RegistrationDeliveryStatus.Sent, captured.RegistrationDeliveryStatus);
    }

    [Fact]
    public async System.Threading.Tasks.Task InternalEmailAndDuplicatePendingInvitationAreRejected()
    {
        var mangaka = Guid.NewGuid();
        var series = MangaSeries.Create(mangaka, null, "Series", null, null, null);
        var identity = new Mock<IStudioIdentityService>();
        identity.Setup(x => x.IsInternalEmailAsync("internal@company.test", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var firstHandler = new InviteAssistantHandler(Repository(series.Id, []).Object, identity.Object, SeriesRepository(series).Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => firstHandler.Handle(new(mangaka, series.Id, "internal@company.test", null), default));

        identity.Setup(x => x.IsInternalEmailAsync("person@example.com", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var pending = new StudioInvitation { SeriesId = series.Id, AssistantEmail = "PERSON@example.com", NormalizedAssistantEmail = "person@example.com", Status = StudioInvitationStatus.Pending };
        var duplicateHandler = new InviteAssistantHandler(Repository(series.Id, [pending]).Object, identity.Object, SeriesRepository(series).Object);
        await Assert.ThrowsAsync<InvalidOperationException>(() => duplicateHandler.Handle(new(mangaka, series.Id, "person@example.com", null), default));
        identity.Verify(x => x.FindActiveAssistantByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async System.Threading.Tasks.Task InvitationRequiresSeriesOwnership()
    {
        var series = MangaSeries.Create(Guid.NewGuid(), null, "Series", null, null, null);
        var identity = new Mock<IStudioIdentityService>();
        identity.Setup(x => x.IsInternalEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var handler = new InviteAssistantHandler(Repository(series.Id, []).Object, identity.Object, SeriesRepository(series).Object);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(new(Guid.NewGuid(), series.Id, "person@example.com", null), default));
    }

    [Theory]
    [InlineData("person")]
    [InlineData("person@")]
    [InlineData("@example.com")]
    public async System.Threading.Tasks.Task InvitationRejectsIncompleteEmailBeforeLookup(string email)
    {
        var mangaka = Guid.NewGuid();
        var series = MangaSeries.Create(mangaka, null, "Series", null, null, null);
        var identity = new Mock<IStudioIdentityService>();
        var handler = new InviteAssistantHandler(Repository(series.Id, []).Object, identity.Object, SeriesRepository(series).Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new(mangaka, series.Id, email, null), default));
        identity.Verify(x => x.IsInternalEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        identity.Verify(x => x.FindActiveAssistantByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<IStudioInvitationRepository> Repository(Guid seriesId, IEnumerable<StudioInvitation> invitations)
    {
        var mock = new Mock<IStudioInvitationRepository>();
        mock.Setup(x => x.GetBySeriesIdAsync(seriesId, It.IsAny<CancellationToken>())).ReturnsAsync(invitations);
        mock.Setup(x => x.AddAsync(It.IsAny<StudioInvitation>(), It.IsAny<CancellationToken>())).Returns(System.Threading.Tasks.Task.CompletedTask);
        mock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return mock;
    }

    private static Mock<ISeriesRepository> SeriesRepository(MangaSeries series)
    {
        var mock = new Mock<ISeriesRepository>();
        mock.Setup(x => x.GetByIdAsync(series.Id, It.IsAny<CancellationToken>())).ReturnsAsync(series);
        return mock;
    }
}
