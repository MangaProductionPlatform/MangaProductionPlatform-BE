using MediatR;
using MangaERP.Shared.Application.Ports;
using MangaERP.Studio.Application.Ports;

namespace MangaERP.Studio.Application.Commands.RespondInvitation;

public record AcceptInvitationCommand(Guid InvitationId, Guid AssistantUserId) : IRequest<Unit>;

public sealed class AcceptInvitationHandler : IRequestHandler<AcceptInvitationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly INotificationService _notifications;

    public AcceptInvitationHandler(IStudioInvitationRepository repo, INotificationService notifications)
    {
        _repo = repo;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(AcceptInvitationCommand request, CancellationToken ct)
    {
        var collaboration = await _repo.AcceptInvitationAsync(
            request.InvitationId, request.AssistantUserId, request.AssistantUserId,
            DateTime.UtcNow, null, ct);

        await _notifications.NotifyCollaborationEventAsync(
            collaboration.MangakaId, "CollaborationActivated", "Collaboration activated",
            "The Assistant accepted the invitation and collaboration is now active.", collaboration.Id, ct);
        await _notifications.NotifyCollaborationEventAsync(
            collaboration.AssistantId, "InvitationAccepted", "Invitation accepted",
            "Your Mangaka collaboration is now active.", collaboration.Id, ct);

        return Unit.Value;
    }
}

public record DeclineInvitationCommand(Guid InvitationId, Guid AssistantUserId) : IRequest<Unit>;

public sealed class DeclineInvitationHandler : IRequestHandler<DeclineInvitationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly INotificationService _notifications;

    public DeclineInvitationHandler(IStudioInvitationRepository repo, INotificationService notifications)
    {
        _repo = repo;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(DeclineInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _repo.GetByIdAsync(request.InvitationId, ct)
            ?? throw new KeyNotFoundException("Invitation was not found.");
        if (invitation.AssistantUserId != request.AssistantUserId)
            throw new UnauthorizedAccessException("You cannot process this invitation.");
        if (invitation.Status != Domain.Entities.StudioInvitationStatus.Pending)
            throw new MangaERP.Shared.Domain.Exceptions.ConflictException("This invitation has already been processed.");

        invitation.Status = Domain.Entities.StudioInvitationStatus.Declined;
        invitation.RespondedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(invitation, ct);
        await _repo.SaveChangesAsync(ct);
        await _notifications.NotifyCollaborationEventAsync(
            invitation.InviterMangakaId, "InvitationDeclined", "Invitation declined",
            "The Assistant declined your invitation.", invitation.Id, ct);
        return Unit.Value;
    }
}
