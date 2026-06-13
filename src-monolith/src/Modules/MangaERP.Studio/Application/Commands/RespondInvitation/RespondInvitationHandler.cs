using MediatR;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;

namespace MangaERP.Studio.Application.Commands.RespondInvitation;

// ── Accept ────────────────────────────────────────────────────────────────────

public record AcceptInvitationCommand(Guid InvitationId, Guid AssistantUserId)
    : IRequest<Unit>;

public class AcceptInvitationHandler : IRequestHandler<AcceptInvitationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    public AcceptInvitationHandler(IStudioInvitationRepository repo) => _repo = repo;

    public async Task<Unit> Handle(AcceptInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _repo.GetByIdAsync(request.InvitationId, ct)
            ?? throw new KeyNotFoundException($"Lời mời {request.InvitationId} không tồn tại.");

        if (invitation.AssistantUserId != request.AssistantUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền xử lý lời mời này.");

        if (invitation.Status != StudioInvitationStatus.Pending)
            throw new InvalidOperationException($"Lời mời đang ở trạng thái {invitation.Status}, không thể chấp nhận.");

        if (invitation.ExpiresAt < DateTime.UtcNow)
        {
            invitation.Status = StudioInvitationStatus.Expired;
            await _repo.UpdateAsync(invitation, ct);
            await _repo.SaveChangesAsync(ct);
            throw new InvalidOperationException("Lời mời đã hết hạn.");
        }

        invitation.Status = StudioInvitationStatus.Accepted;
        invitation.RespondedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(invitation, ct);
        await _repo.SaveChangesAsync(ct);

        return Unit.Value;
    }
}

// ── Decline ───────────────────────────────────────────────────────────────────

public record DeclineInvitationCommand(Guid InvitationId, Guid AssistantUserId)
    : IRequest<Unit>;

public class DeclineInvitationHandler : IRequestHandler<DeclineInvitationCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    public DeclineInvitationHandler(IStudioInvitationRepository repo) => _repo = repo;

    public async Task<Unit> Handle(DeclineInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _repo.GetByIdAsync(request.InvitationId, ct)
            ?? throw new KeyNotFoundException($"Lời mời {request.InvitationId} không tồn tại.");

        if (invitation.AssistantUserId != request.AssistantUserId)
            throw new UnauthorizedAccessException("Bạn không có quyền xử lý lời mời này.");

        if (invitation.Status != StudioInvitationStatus.Pending)
            throw new InvalidOperationException($"Lời mời đang ở trạng thái {invitation.Status}, không thể từ chối.");

        invitation.Status = StudioInvitationStatus.Declined;
        invitation.RespondedAt = DateTime.UtcNow;
        await _repo.UpdateAsync(invitation, ct);
        await _repo.SaveChangesAsync(ct);

        return Unit.Value;
    }
}
