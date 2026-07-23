using MediatR;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Shared.Application.Ports;

namespace MangaERP.Studio.Application.Commands.CancelInvitation;

public record CancelInvitationCommand(Guid InvitationId, Guid MangakaId) : IRequest<Unit>;

public class CancelInvitationHandler : IRequestHandler<CancelInvitationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly INotificationService _notifications;

    public CancelInvitationHandler(IStudioInvitationRepository repo, INotificationService notifications)
    {
        _repo = repo;
        _notifications = notifications;
    }

    public CancelInvitationHandler(IStudioInvitationRepository repo) : this(repo, null!) { }

    public async Task<Unit> Handle(CancelInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _repo.GetByIdAsync(request.InvitationId, ct)
            ?? throw new KeyNotFoundException($"Lời mời {request.InvitationId} không tồn tại.");

        if (invitation.InviterMangakaId != request.MangakaId)
            throw new UnauthorizedAccessException("Bạn không có quyền hủy lời mời này.");

        if (invitation.Status != StudioInvitationStatus.Pending)
            throw new InvalidOperationException($"Lời mời đã ở trạng thái {invitation.Status}, không thể hủy.");

        invitation.Status = StudioInvitationStatus.Revoked;
        invitation.RespondedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(invitation, ct);
        await _repo.SaveChangesAsync(ct);

        if (invitation.AssistantUserId.HasValue && _notifications is not null)
        {
            await _notifications.NotifyCollaborationEventAsync(
                invitation.AssistantUserId.Value, "InvitationRevoked", "Invitation revoked",
                "The Mangaka revoked the pending invitation.", invitation.Id, ct);
        }

        return Unit.Value;
    }
}
