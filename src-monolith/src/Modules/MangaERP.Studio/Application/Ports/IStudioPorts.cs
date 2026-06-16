using MangaERP.Studio.Domain.Entities;

namespace MangaERP.Studio.Application.Ports;

public interface IStudioInvitationRepository
{
    Task<StudioInvitation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IEnumerable<StudioInvitation>> GetPendingByAssistantUserIdAsync(Guid assistantUserId, CancellationToken ct = default);
    Task<IEnumerable<StudioInvitation>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default);
    Task<StudioInvitation?> GetByActivationTokenAsync(string token, CancellationToken ct = default);
    Task AddAsync(StudioInvitation invitation, CancellationToken ct = default);
    Task UpdateAsync(StudioInvitation invitation, CancellationToken ct = default);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// Cổng giao tiếp với Identity module để kiểm tra / tạo user mới.
/// Tránh circular reference giữa Studio và Identity.
/// </summary>
public interface IStudioIdentityService
{
    /// Kiểm tra email đã có tài khoản Active chưa (trả về UserId nếu có)
    Task<Guid?> FindActiveAssistantByEmailAsync(string email, CancellationToken ct = default);

    /// Provision tài khoản Assistant mới (TH1) — tương đương AdminController.ProvisionAccount
    /// Trả về UserId mới tạo và activation token
    Task<(Guid userId, string activationToken)> ProvisionAssistantAccountAsync(
        string email, string? fullName, string invitingMangakaName, CancellationToken ct = default);

    /// Gửi push notification cho Assistant đã có tài khoản (TH2)
    Task SendStudioInvitationNotificationAsync(
        Guid receiverUserId, Guid invitationId, string mangakaName, string seriesTitle, CancellationToken ct = default);
}
