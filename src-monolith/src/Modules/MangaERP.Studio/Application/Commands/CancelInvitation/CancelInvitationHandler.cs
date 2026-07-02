using MediatR;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;

namespace MangaERP.Studio.Application.Commands.CancelInvitation;

public record CancelInvitationCommand(Guid InvitationId, Guid MangakaId) : IRequest<Unit>;

public class CancelInvitationHandler : IRequestHandler<CancelInvitationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;

    public CancelInvitationHandler(IStudioInvitationRepository repo)
    {
        _repo = repo;
    }

    public async Task<Unit> Handle(CancelInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _repo.GetByIdAsync(request.InvitationId, ct)
            ?? throw new KeyNotFoundException($"Lời mời {request.InvitationId} không tồn tại.");

        if (invitation.InviterMangakaId != request.MangakaId)
            throw new UnauthorizedAccessException("Bạn không có quyền hủy lời mời này.");

        if (invitation.Status != StudioInvitationStatus.Pending)
            throw new InvalidOperationException($"Lời mời đã ở trạng thái {invitation.Status}, không thể hủy.");

        invitation.Status = StudioInvitationStatus.Cancelled;
        invitation.RespondedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(invitation, ct);
        await _repo.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
