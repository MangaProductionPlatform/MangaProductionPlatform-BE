using MediatR;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Series.Application.Ports;
using System.Net.Mail;

namespace MangaERP.Studio.Application.Commands.InviteAssistant;

/// <summary>
/// Command mời một Assistant (bằng email) vào studio của Mangaka cho một Series.
/// Backend tự phân nhánh TH1 / TH2 dựa trên trạng thái tài khoản của email đó.
/// </summary>
public record InviteAssistantCommand(
    Guid MangakaId,
    Guid SeriesId,
    string AssistantEmail,
    string? Message
) : IRequest<InviteAssistantResult>;

public record InviteAssistantResult(
    Guid InvitationId,
    string AssistantEmail,
    string Case,           // "NewAccount" | "ExistingAccount"
    string StatusMessage
);

public class InviteAssistantHandler : IRequestHandler<InviteAssistantCommand, InviteAssistantResult>
{
    private readonly IStudioInvitationRepository _invitationRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly IStudioIdentityService _identityService;
    private readonly ISeriesRepository _seriesRepo;

    public InviteAssistantHandler(
        IStudioInvitationRepository invitationRepo,
        ISeriesAccessGrantRepository grantRepo,
        IStudioIdentityService identityService,
        ISeriesRepository seriesRepo)
    {
        _invitationRepo = invitationRepo;
        _grantRepo = grantRepo;
        _identityService = identityService;
        _seriesRepo = seriesRepo;
    }

    public async Task<InviteAssistantResult> Handle(InviteAssistantCommand request, CancellationToken cancellationToken)
    {
        var personalEmail = request.AssistantEmail.Trim().ToLowerInvariant();
        // Invitation identity is an exact, complete personal email. Do not
        // allow partial values to reach account lookup/provisioning (the
        // frontend debounce is only a convenience; the backend is authoritative).
        if (!IsCompleteEmail(personalEmail))
            throw new InvalidOperationException("A complete, valid personal email is required.");
        if (await _identityService.IsInternalEmailAsync(personalEmail, cancellationToken))
            throw new InvalidOperationException("Use the Assistant's personal email; internal email addresses are not accepted.");

        // Validate series thuộc Mangaka này
        var series = await _seriesRepo.GetByIdAsync(request.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {request.SeriesId} không tồn tại.");

        if (series.AuthorId != request.MangakaId)
            throw new UnauthorizedAccessException("Bạn không có quyền mời Assistant vào studio này.");

        // ── Phân nhánh TH1 / TH2 ──────────────────────────────────────
        var invitations = await _invitationRepo.GetBySeriesIdAsync(request.SeriesId, cancellationToken);
        var pendingForMangaka = await _invitationRepo.HasPendingForMangakaEmailAsync(request.MangakaId, personalEmail, cancellationToken);
        if (pendingForMangaka || invitations.Any(x => x.Status == StudioInvitationStatus.Pending &&
            string.Equals(string.IsNullOrWhiteSpace(x.NormalizedAssistantEmail)
                ? x.AssistantEmail.Trim().ToLowerInvariant()
                : x.NormalizedAssistantEmail,
                personalEmail,
                StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("A pending invitation already exists for this personal email and series.");

        var existingAssistantId = await _identityService.FindActiveAssistantByEmailAsync(personalEmail, cancellationToken);

        if (existingAssistantId.HasValue)
        {
            var myCollabs = await _invitationRepo.GetNonEndedCollaborationsByMangakaAsync(request.MangakaId, cancellationToken);
            var existingCollab = myCollabs.FirstOrDefault(c => c.AssistantId == existingAssistantId.Value);

            if (existingCollab == null && await _invitationRepo.HasNonEndedCollaborationAsync(existingAssistantId.Value, cancellationToken))
            {
                throw new MangaERP.Shared.Domain.Exceptions.ConflictException("The Assistant already has a non-ended collaboration with another Mangaka.");
            }
        }

        string resultCase;
        string statusMsg;
        Guid? assignedUserId = null;
        string? activationToken = null;

        if (existingAssistantId is null)
        {
            // TH1: Email chưa có tài khoản → provision + gửi email kích hoạt
            var (newUserId, token) = await _identityService.ProvisionAssistantAccountAsync(
                personalEmail,
                fullName: null,
                invitingMangakaName: series.Title,
                cancellationToken);

            assignedUserId = newUserId;
            activationToken = token;
            resultCase = "NewAccount";
            statusMsg = "Registration delivery was requested. The Assistant must activate, sign in, and accept the pending series invitation.";
        }
        else
        {
            // TH2: Email đã có tài khoản Active Assistant → gửi push notification
            assignedUserId = existingAssistantId;
            resultCase = "ExistingAccount";
            statusMsg = "The durable invitation was created and is waiting for the Assistant to accept or decline.";
        }

        // ── Tạo và lưu invitation record ──────────────────────────────
        var invitation = new StudioInvitation
        {
            InviterMangakaId = request.MangakaId,
            SeriesId = request.SeriesId,
            AssistantEmail = personalEmail,
            NormalizedAssistantEmail = personalEmail,
            AssistantUserId = assignedUserId,
            Message = request.Message,
            IsNewAccountFlow = existingAssistantId is null,
            ActivationToken = activationToken,
            Status = StudioInvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        if (existingAssistantId is null)
            invitation.MarkRegistrationDeliveryPending();
        await _invitationRepo.AddAsync(invitation, cancellationToken);
        // Commit invitation before notification delivery.
        await _invitationRepo.SaveChangesAsync(cancellationToken);

        // TH2: gửi notification SAU khi đã có invitationId
        await _identityService.SendStudioInvitationNotificationAsync(
            assignedUserId!.Value,
            invitation.Id,
            mangakaName: series.Title,
            seriesTitle: series.Title,
            cancellationToken);

        await _invitationRepo.SaveChangesAsync(cancellationToken);

        if (existingAssistantId is null && assignedUserId.HasValue && activationToken is not null)
        {
            try
            {
                await _identityService.SendAssistantRegistrationEmailAsync(
                    assignedUserId.Value, activationToken, cancellationToken);
                invitation.MarkRegistrationDeliverySent();
            }
            catch (Exception ex)
            {
                invitation.MarkRegistrationDeliveryFailed(ex.Message);
                statusMsg = "The invitation is durable, but registration delivery failed and must be retried.";
            }
            await _invitationRepo.UpdateAsync(invitation, cancellationToken);
            await _invitationRepo.SaveChangesAsync(cancellationToken);
        }

        if (existingAssistantId is not null)
            await _identityService.DeliverStudioInvitationRealtimeAsync(
                existingAssistantId.Value, invitation.Id, series.Title, cancellationToken);

        return new InviteAssistantResult(invitation.Id, personalEmail, resultCase, statusMsg);
    }

    private static bool IsCompleteEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            var parsed = new MailAddress(value);
            return parsed.Address.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException) { return false; }
    }
}
