using MangaERP.Studio.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

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

    public StudioIdentityService(
        IDbContextProvider provider,
        ITokenService tokenService,
        IEmailService emailService,
        IUsernameGenerator usernameGenerator,
        IConfiguration config)
    {
        _db = (AppDbContext)provider.GetDbContext();
        _tokenService = tokenService;
        _emailService = emailService;
        _usernameGenerator = usernameGenerator;
        _config = config;
    }

    /// <summary>
    /// Kiểm tra email đã có tài khoản Assistant Active chưa.
    /// Trả về UserId nếu tìm thấy, null nếu chưa có.
    /// </summary>
    public async System.Threading.Tasks.Task<Guid?> FindActiveAssistantByEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(
            u => (u.Email == email || u.PersonalEmail == email)
                 && u.Role == UserRole.Assistant
                 && u.AccountStatus == AccountStatus.Active
                 && !u.IsDeleted,
            ct);

        return user?.Id;
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
        var baseUrl = _config["Invitation:ActivationBaseUrl"] ?? "https://company.com/activate";
        var activationLink = $"{baseUrl}?token={Uri.EscapeDataString(jwtToken)}";
        await _emailService.SendInvitationEmailAsync(
            email, activationLink, username,
            fullName ?? "Assistant", ct);

        await _db.SaveChangesAsync(ct);

        return (user.Id, jwtToken);
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
}
