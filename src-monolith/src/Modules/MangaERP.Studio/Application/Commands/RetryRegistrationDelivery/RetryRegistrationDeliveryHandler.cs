using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MediatR;

namespace MangaERP.Studio.Application.Commands.RetryRegistrationDelivery;

public record RetryRegistrationDeliveryCommand(Guid InvitationId, Guid MangakaId)
    : IRequest<RetryRegistrationDeliveryResult>;

public record RetryRegistrationDeliveryResult(Guid InvitationId, string DeliveryStatus);

public class RetryRegistrationDeliveryHandler
    : IRequestHandler<RetryRegistrationDeliveryCommand, RetryRegistrationDeliveryResult>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly IStudioIdentityService _identity;

    public RetryRegistrationDeliveryHandler(IStudioInvitationRepository repo, IStudioIdentityService identity)
    {
        _repo = repo;
        _identity = identity;
    }

    public async Task<RetryRegistrationDeliveryResult> Handle(
        RetryRegistrationDeliveryCommand request, CancellationToken ct)
    {
        var invitation = await _repo.GetByIdAsync(request.InvitationId, ct)
            ?? throw new KeyNotFoundException("Studio invitation not found.");
        if (invitation.InviterMangakaId != request.MangakaId)
            throw new UnauthorizedAccessException("Only the inviting Mangaka can retry registration delivery.");
        if (!invitation.IsNewAccountFlow || invitation.Status != StudioInvitationStatus.Pending ||
            !invitation.AssistantUserId.HasValue || string.IsNullOrWhiteSpace(invitation.ActivationToken))
            throw new InvalidOperationException("This invitation has no retryable registration delivery.");

        try
        {
            await _identity.SendAssistantRegistrationEmailAsync(
                invitation.AssistantUserId.Value, invitation.ActivationToken, ct);
            invitation.MarkRegistrationDeliverySent();
        }
        catch (Exception ex)
        {
            invitation.MarkRegistrationDeliveryFailed(ex.Message);
        }

        await _repo.UpdateAsync(invitation, ct);
        await _repo.SaveChangesAsync(ct);
        return new(invitation.Id, invitation.RegistrationDeliveryStatus.ToString());
    }
}
