using STTask = System.Threading.Tasks.Task;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Studio.Application.Commands.InviteAssistant;
using MangaERP.Studio.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Domain.Exceptions;
using Moq;

namespace MangaERP.Api.Tests;

public class CollaborationLifecycleTests
{
    [Fact]
    public async STTask InviteAssistant_ValidEmail_CreatesPendingInvitation()
    {
        var invitationRepoMock = new Mock<IStudioInvitationRepository>();
        var identityServiceMock = new Mock<IStudioIdentityService>();
        var seriesRepoMock = new Mock<ISeriesRepository>();

        var mangakaId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var series = MangaSeries.Create(mangakaId, null, "Test Series", "Synopsis", null, null);

        seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        invitationRepoMock.Setup(r => r.GetBySeriesIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<StudioInvitation>());
        identityServiceMock.Setup(i => i.FindActiveAssistantByEmailAsync("assistant@gmail.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid?)null);
        identityServiceMock.Setup(i => i.ProvisionAssistantAccountAsync("assistant@gmail.com", null, series.Title, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid.NewGuid(), "token-123"));

        var handler = new InviteAssistantHandler(invitationRepoMock.Object, identityServiceMock.Object, seriesRepoMock.Object);
        var result = await handler.Handle(new InviteAssistantCommand(mangakaId, seriesId, "assistant@gmail.com", "Join my team"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("assistant@gmail.com", result.AssistantEmail);
        Assert.Equal("NewAccount", result.Case);
        invitationRepoMock.Verify(r => r.AddAsync(It.IsAny<StudioInvitation>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async STTask InviteAssistant_AssistantWithActiveCollaboration_ThrowsConflictException()
    {
        var invitationRepoMock = new Mock<IStudioInvitationRepository>();
        var identityServiceMock = new Mock<IStudioIdentityService>();
        var seriesRepoMock = new Mock<ISeriesRepository>();

        var mangakaId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var series = MangaSeries.Create(mangakaId, null, "Test Series", "Synopsis", null, null);

        seriesRepoMock.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        identityServiceMock.Setup(i => i.FindActiveAssistantByEmailAsync("active@gmail.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(assistantId);
        invitationRepoMock.Setup(r => r.HasNonEndedCollaborationAsync(assistantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new InviteAssistantHandler(invitationRepoMock.Object, identityServiceMock.Object, seriesRepoMock.Object);

        await Assert.ThrowsAsync<ConflictException>(() =>
            handler.Handle(new InviteAssistantCommand(mangakaId, seriesId, "active@gmail.com", null), CancellationToken.None));
    }
}
