using MangaERP.Studio.Domain.Entities;
using MangaERP.Studio.Application.Commands.RespondInvitation;
using MangaERP.Studio.Application.Ports;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using Moq;
using Xunit;

namespace MangaERP.Api.Tests;

public class CollaborationFoundationTests
{
    [Fact]
    public void SuspendedCollaborationRequiresModeAndEndedIsOnlyReleasingState()
    {
        var collaboration = new MangakaAssistantCollaboration(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow);
        collaboration.Suspend(CollaborationSuspensionMode.SuspendNewAssignments, "policy", DateTime.UtcNow);

        Assert.Equal(CollaborationStatus.Suspended, collaboration.Status);
        Assert.Equal(CollaborationSuspensionMode.SuspendNewAssignments, collaboration.SuspensionMode);

        collaboration.End("done", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(CollaborationStatus.Ended, collaboration.Status);
        Assert.Null(collaboration.SuspensionMode);
    }

    [Fact]
    public async System.Threading.Tasks.Task AcceptInvitation_CreatesSingleNonEndedCollaboration_AndAuditAndNotification()
    {
        var mangaka = Guid.NewGuid();
        var assistant = Guid.NewGuid();
        var series = Guid.NewGuid();
        var invitation = new StudioInvitation
        {
            InviterMangakaId = mangaka,
            SeriesId = series,
            AssistantEmail = "assistant@example.com",
            NormalizedAssistantEmail = "ASSISTANT@EXAMPLE.COM",
            AssistantUserId = assistant,
            Status = StudioInvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var collab = new MangakaAssistantCollaboration(mangaka, assistant, invitation.Id, DateTime.UtcNow);

        var repo = new Mock<IStudioInvitationRepository>();
        repo.Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);
        repo.Setup(x => x.HasNonEndedCollaborationAsync(assistant, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        repo.Setup(x => x.AcceptInvitationAsync(invitation.Id, assistant, assistant, It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(collab);

        var notifications = new Mock<INotificationService>();
        var handler = new AcceptInvitationHandler(repo.Object, notifications.Object);

        var response = await handler.Handle(new(invitation.Id, assistant), default);

        Assert.Equal(MediatR.Unit.Value, response);
        repo.Verify(x => x.AcceptInvitationAsync(invitation.Id, assistant, assistant, It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(x => x.NotifyCollaborationEventAsync(mangaka, "CollaborationActivated", "Collaboration activated", It.IsAny<string>(), collab.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async System.Threading.Tasks.Task AcceptInvitation_AssistantAlreadyOccupied_ThrowsConflictException()
    {
        var mangaka = Guid.NewGuid();
        var assistant = Guid.NewGuid();
        var series = Guid.NewGuid();
        var invitation = new StudioInvitation
        {
            InviterMangakaId = mangaka,
            SeriesId = series,
            AssistantEmail = "assistant@example.com",
            NormalizedAssistantEmail = "ASSISTANT@EXAMPLE.COM",
            AssistantUserId = assistant,
            Status = StudioInvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var repo = new Mock<IStudioInvitationRepository>();
        repo.Setup(x => x.GetByIdAsync(invitation.Id, It.IsAny<CancellationToken>())).ReturnsAsync(invitation);
        repo.Setup(x => x.HasNonEndedCollaborationAsync(assistant, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repo.Setup(x => x.AcceptInvitationAsync(invitation.Id, assistant, assistant, It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Assistant already has an active collaboration."));

        var notifications = new Mock<INotificationService>();
        var handler = new AcceptInvitationHandler(repo.Object, notifications.Object);

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new(invitation.Id, assistant), default));
    }
}
