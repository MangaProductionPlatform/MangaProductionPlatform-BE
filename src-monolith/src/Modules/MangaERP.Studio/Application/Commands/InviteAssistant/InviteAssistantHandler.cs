using MediatR;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Series.Application.Ports;

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
    private readonly IStudioIdentityService _identityService;
    private readonly ISeriesRepository _seriesRepo;

    public InviteAssistantHandler(
        IStudioInvitationRepository invitationRepo,
        IStudioIdentityService identityService,
        ISeriesRepository seriesRepo)
    {
        _invitationRepo = invitationRepo;
        _identityService = identityService;
        _seriesRepo = seriesRepo;
    }

    public async Task<InviteAssistantResult> Handle(InviteAssistantCommand request, CancellationToken cancellationToken)
    {
        // Validate series thuộc Mangaka này
        var series = await _seriesRepo.GetByIdAsync(request.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {request.SeriesId} không tồn tại.");

        if (series.AuthorId != request.MangakaId)
            throw new UnauthorizedAccessException("Bạn không có quyền mời Assistant vào studio này.");

        // ── Phân nhánh TH1 / TH2 ──────────────────────────────────────
        var existingAssistantId = await _identityService.FindActiveAssistantByEmailAsync(
            request.AssistantEmail, cancellationToken);

        string resultCase;
        string statusMsg;
        Guid? assignedUserId = null;
        string? activationToken = null;

        if (existingAssistantId is null)
        {
            // TH1: Email chưa có tài khoản → provision + gửi email kích hoạt
            var (newUserId, token) = await _identityService.ProvisionAssistantAccountAsync(
                request.AssistantEmail,
                fullName: null,
                invitingMangakaName: series.Title,
                cancellationToken);

            assignedUserId = newUserId;
            activationToken = token;
            resultCase = "NewAccount";
            statusMsg = "Email kích hoạt đã được gửi. Assistant sẽ tự động được thêm vào studio sau khi tạo mật khẩu xong.";
        }
        else
        {
            // TH2: Email đã có tài khoản Active Assistant → gửi push notification
            assignedUserId = existingAssistantId;
            resultCase = "ExistingAccount";
            statusMsg = "Lời mời đã được gửi. Chờ Assistant xác nhận.";
        }

        // ── Tạo và lưu invitation record ──────────────────────────────
        var invitation = new StudioInvitation
        {
            InviterMangakaId = request.MangakaId,
            SeriesId = request.SeriesId,
            AssistantEmail = request.AssistantEmail,
            AssistantUserId = assignedUserId,
            Message = request.Message,
            IsNewAccountFlow = existingAssistantId is null,
            ActivationToken = activationToken,
            Status = StudioInvitationStatus.Pending,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        };
        await _invitationRepo.AddAsync(invitation, cancellationToken);

        // TH2: gửi notification SAU khi đã có invitationId
        if (existingAssistantId is not null)
        {
            await _identityService.SendStudioInvitationNotificationAsync(
                existingAssistantId.Value,
                invitation.Id,
                mangakaName: series.Title,    // dùng tạm title; thực tế nên dùng FullName của Mangaka
                seriesTitle: series.Title,
                cancellationToken);
        }

        await _invitationRepo.SaveChangesAsync(cancellationToken);

        return new InviteAssistantResult(invitation.Id, request.AssistantEmail, resultCase, statusMsg);
    }
}
