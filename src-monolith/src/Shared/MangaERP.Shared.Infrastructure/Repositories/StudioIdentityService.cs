using MangaERP.Studio.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MangaERP.Shared.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace MangaERP.Shared.Infrastructure.Repositories;

/// <summary>
/// Adapter tích hợp Studio module với Identity module.
/// Thực hiện: kiểm tra user, provision tài khoản mới, gửi notification.
/// </summary>
public class StudioIdentityService : IStudioIdentityService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IUsernameGenerator _usernameGenerator;
    private readonly IConfiguration _config;
    private readonly IHubContext<NotificationHub> _hub;

    public StudioIdentityService(
        IDbContextProvider provider,
        ITokenService tokenService,
        IEmailService emailService,
        IUsernameGenerator usernameGenerator,
        IConfiguration config,
        IHubContext<NotificationHub> hub)
    {
        _db = (AppDbContext)provider.GetDbContext();
        _tokenService = tokenService;
        _emailService = emailService;
        _usernameGenerator = usernameGenerator;
        _config = config;
        _hub = hub;
    }

    /// <summary>
    /// Kiểm tra email đã có tài khoản Assistant Active chưa.
    /// Trả về UserId nếu tìm thấy, null nếu chưa có.
    /// </summary>
    public async System.Threading.Tasks.Task<Guid?> FindActiveAssistantByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLower();
        var matches = await _db.Users
            .Where(u => u.NormalizedPersonalEmail == normalized && !u.IsDeleted)
            .ToListAsync(ct);
        if (matches.Count > 1)
            throw new InvalidOperationException("Duplicate accounts exist for this personal email. Contact an administrator.");
        var user = matches.SingleOrDefault();
        if (user is null) return null;
        if (user.Role != UserRole.Assistant)
            throw new InvalidOperationException("This personal email belongs to a non-Assistant account.");
        if (user.AccountStatus is AccountStatus.Suspended or AccountStatus.Deactivated)
            throw new InvalidOperationException("This Assistant account is not available for invitations.");
        return user.Id;
    }

    public System.Threading.Tasks.Task<bool> IsInternalEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLower();
        return _db.Users.AnyAsync(u => u.Email.ToLower() == normalized || u.Username.ToLower() == normalized, ct);
    }

    /// <summary>
    /// TH1: Email chưa có tài khoản → provision tài khoản Assistant PendingActivation
    /// + gửi email kích hoạt kèm link chứa activation token.
    /// Trả về (userId, activationToken).
    /// </summary>
    public async System.Threading.Tasks.Task<(Guid userId, string activationToken)> ProvisionAssistantAccountAsync(
        string email, string? fullName, string invitingMangakaName, CancellationToken ct = default)
    {
        // Sinh username dạng assistant@company.com
        var username = await _usernameGenerator.GenerateAsync(
            fullName ?? email.Split('@')[0], UserRole.Assistant, ct);

        var user = new User
        {
            Username = username,
            Email = username,
            PersonalEmail = email,
            NormalizedPersonalEmail = email.Trim().ToLowerInvariant(),
            PasswordHash = string.Empty,
            Role = UserRole.Assistant,
            FullName = fullName,
            AccountStatus = AccountStatus.PendingActivation,
            CreatedAt = DateTime.UtcNow
        };
        await _db.Users.AddAsync(user, ct);

        // Tạo invitation token JWT (tái dùng ITokenService của Identity)
        var jwtToken = _tokenService.GenerateInvitationToken(
            user.Id, email, username, UserRole.Assistant.ToString());

        // Persist InvitationToken (bảng dùng chung với Admin provision flow)
        var invToken = new InvitationToken
        {
            Token = jwtToken,
            UserId = user.Id,
            PersonalEmail = email,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsUsed = false,
            CreatedAt = DateTime.UtcNow
        };
        await _db.InvitationTokens.AddAsync(invToken, ct);

        // Gửi email kích hoạt
        return (user.Id, jwtToken);
    }

    public async System.Threading.Tasks.Task SendAssistantRegistrationEmailAsync(
        Guid userId, string activationToken, CancellationToken ct = default)
    {
        var user = await _db.Users.SingleAsync(x => x.Id == userId, ct);
        var baseUrl = _config["Invitation:ActivationBaseUrl"] ?? "https://company.com/activate";
        var activationLink = $"{baseUrl}?token={Uri.EscapeDataString(activationToken)}";
        await _emailService.SendInvitationEmailAsync(
            user.PersonalEmail!, activationLink, user.Username, user.FullName ?? "Assistant", ct);
    }

    /// <summary>
    /// TH2: Gửi push notification (lưu DB) cho Assistant đã có tài khoản.
    /// </summary>
    public async System.Threading.Tasks.Task SendStudioInvitationNotificationAsync(
        Guid receiverUserId, Guid invitationId, string mangakaName, string seriesTitle, CancellationToken ct = default)
    {
        var notification = new MangaERP.Publishing.Domain.Entities.Notification
        {
            ReceiverId = receiverUserId,
            Title = $"Lời mời tham gia studio: {seriesTitle}",
            Message = $"{mangakaName} đã mời bạn vào studio. Vào mục 'Lời mời' để chấp nhận hoặc từ chối.",
            NotifyType = "StudioInvitation",
            RelatedEntityId = invitationId,
            RelatedEntityType = "StudioInvitation",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        await _db.Notifications.AddAsync(notification, ct);
        // SaveChanges sẽ được gọi sau trong InviteAssistantHandler
    }

    public System.Threading.Tasks.Task DeliverStudioInvitationRealtimeAsync(
        Guid receiverUserId, Guid invitationId, string seriesTitle, CancellationToken ct = default) =>
        _hub.Clients.User(receiverUserId.ToString()).SendAsync("ReceiveNotification", new
        {
            title = $"Studio invitation: {seriesTitle}",
            message = "A new studio invitation is available.",
            notifyType = "StudioInvitation",
            relatedEntityId = invitationId
        }, ct);
}
