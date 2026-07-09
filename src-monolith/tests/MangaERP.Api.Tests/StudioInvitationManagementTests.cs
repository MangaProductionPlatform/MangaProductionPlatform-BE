using MangaERP.Studio.Application.Commands.CancelInvitation;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using Moq;
using Xunit;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class StudioInvitationManagementTests
{
    private readonly Mock<IStudioInvitationRepository> _repoMock = new();
    private readonly CancelInvitationHandler _handler;

    public StudioInvitationManagementTests()
    {
        _handler = new CancelInvitationHandler(_repoMock.Object);
    }

    [Fact]
    public async STTask Handle_InvitationNotFound_ThrowsKeyNotFoundException()
    {
        var id = Guid.NewGuid();
        var command = new CancelInvitationCommand(id, Guid.NewGuid());

        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StudioInvitation)null!);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_UnauthorizedMangaka_ThrowsUnauthorizedAccessException()
    {
        var id = Guid.NewGuid();
        var mangakaId = Guid.NewGuid();
        var inviterId = Guid.NewGuid();
        var invitation = new StudioInvitation
        {
            Id = id,
            InviterMangakaId = inviterId,
            Status = StudioInvitationStatus.Pending
        };

        var command = new CancelInvitationCommand(id, mangakaId);

        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_NotPendingStatus_ThrowsInvalidOperationException()
    {
        var id = Guid.NewGuid();
        var mangakaId = Guid.NewGuid();
        var invitation = new StudioInvitation
        {
            Id = id,
            InviterMangakaId = mangakaId,
            Status = StudioInvitationStatus.Accepted
        };

        var command = new CancelInvitationCommand(id, mangakaId);

        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async STTask Handle_ValidRequest_CancelsInvitationSuccessfully()
    {
        var id = Guid.NewGuid();
        var mangakaId = Guid.NewGuid();
        var invitation = new StudioInvitation
        {
            Id = id,
            InviterMangakaId = mangakaId,
            Status = StudioInvitationStatus.Pending
        };

        var command = new CancelInvitationCommand(id, mangakaId);

        _repoMock.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(StudioInvitationStatus.Cancelled, invitation.Status);
        _repoMock.Verify(r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
