using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Studio.Application.Commands.SeriesAccess;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Studio.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using Moq;
using STTask = System.Threading.Tasks.Task;

namespace MangaERP.Api.Tests;

public class SeriesAccessGrantTests
{
    private readonly Mock<IStudioInvitationRepository> _collabRepo = new();
    private readonly Mock<ISeriesAccessGrantRepository> _grantRepo = new();
    private readonly Mock<ISeriesRepository> _seriesRepo = new();
    private readonly Mock<INotificationService> _notifications = new();

    private readonly GrantSeriesAccessHandler _grantHandler;
    private readonly RevokeSeriesAccessHandler _revokeHandler;

    public SeriesAccessGrantTests()
    {
        _grantHandler = new GrantSeriesAccessHandler(
            _collabRepo.Object, _grantRepo.Object, _seriesRepo.Object, _notifications.Object);
        _revokeHandler = new RevokeSeriesAccessHandler(
            _collabRepo.Object, _grantRepo.Object, _seriesRepo.Object, _notifications.Object);
    }

    [Fact]
    public async STTask ActiveCollaboration_CanReceiveSeriesGrant()
    {
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var collabId = Guid.NewGuid();

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        var series = MangaSeries.Create(mangakaId, null, "Test Series", "Desc", null, null);

        _collabRepo.Setup(r => r.GetCollaborationAsync(collabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collab);
        _seriesRepo.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _grantRepo.Setup(r => r.GetActiveGrantAsync(collabId, seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SeriesAccessGrant?)null);

        var result = await _grantHandler.Handle(new GrantSeriesAccessCommand(collabId, seriesId, mangakaId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(seriesId, result.SeriesId);
        Assert.True(result.IsActive);
        _grantRepo.Verify(r => r.AddAsync(It.IsAny<SeriesAccessGrant>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async STTask SuspendedCollaboration_CannotReceiveSeriesGrant()
    {
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var collabId = Guid.NewGuid();

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        collab.Suspend(CollaborationSuspensionMode.SuspendNewAssignments, "testing", DateTime.UtcNow);

        _collabRepo.Setup(r => r.GetCollaborationAsync(collabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collab);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _grantHandler.Handle(new GrantSeriesAccessCommand(collabId, seriesId, mangakaId), CancellationToken.None));
    }

    [Fact]
    public async STTask DuplicateActiveGrant_ThrowsConflictException()
    {
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var collabId = Guid.NewGuid();

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        var series = MangaSeries.Create(mangakaId, null, "Test Series", "Desc", null, null);
        var existingGrant = SeriesAccessGrant.Create(collabId, seriesId, mangakaId);

        _collabRepo.Setup(r => r.GetCollaborationAsync(collabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collab);
        _seriesRepo.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _grantRepo.Setup(r => r.GetActiveGrantAsync(collabId, seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGrant);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _grantHandler.Handle(new GrantSeriesAccessCommand(collabId, seriesId, mangakaId), CancellationToken.None));
    }

    [Fact]
    public async STTask RevokedGrant_SetsRevokedProperties()
    {
        var mangakaId = Guid.NewGuid();
        var assistantId = Guid.NewGuid();
        var seriesId = Guid.NewGuid();
        var collabId = Guid.NewGuid();

        var collab = new MangakaAssistantCollaboration(mangakaId, assistantId, Guid.NewGuid(), DateTime.UtcNow);
        var series = MangaSeries.Create(mangakaId, null, "Test Series", "Desc", null, null);
        var activeGrant = SeriesAccessGrant.Create(collabId, seriesId, mangakaId);

        _collabRepo.Setup(r => r.GetCollaborationAsync(collabId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(collab);
        _seriesRepo.Setup(r => r.GetByIdAsync(seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(series);
        _grantRepo.Setup(r => r.GetActiveGrantAsync(collabId, seriesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeGrant);

        await _revokeHandler.Handle(new RevokeSeriesAccessCommand(collabId, seriesId, mangakaId, "No longer needed"), CancellationToken.None);

        Assert.False(activeGrant.IsActive);
        Assert.NotNull(activeGrant.RevokedAt);
        Assert.Equal("No longer needed", activeGrant.RevokeReason);
        _grantRepo.Verify(r => r.UpdateAsync(activeGrant, It.IsAny<CancellationToken>()), Times.Once);
    }
}
