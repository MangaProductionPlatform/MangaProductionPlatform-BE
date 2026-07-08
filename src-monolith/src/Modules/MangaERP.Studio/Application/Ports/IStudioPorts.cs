using MangaERP.Studio.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MangaERP.Studio.Application.Ports;

public interface IStudioInvitationRepository
{
    System.Threading.Tasks.Task<StudioInvitation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<StudioInvitation>> GetPendingByAssistantUserIdAsync(Guid assistantUserId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<StudioInvitation>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<StudioMemberInfo>> GetActiveMembersWithUsersBySeriesIdAsync(Guid seriesId, CancellationToken ct = default);
    System.Threading.Tasks.Task<StudioInvitation?> GetByActivationTokenAsync(string token, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(StudioInvitation invitation, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(StudioInvitation invitation, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// Port for production-task cleanup triggered by Studio membership changes.
/// Implemented by the task/chapter side so Studio does not depend on task internals.
/// </summary>
public interface IStudioTaskRevocationService
{
    System.Threading.Tasks.Task RevokeActiveTasksForRemovedMemberAsync(
        Guid seriesId,
        Guid assistantId,
        CancellationToken ct = default);
}

/// <summary>
/// Projection kết hợp thông tin invitation + user profile của Assistant đang hoạt động trong studio.
/// </summary>
public record StudioMemberInfo(
    Guid InvitationId,
    Guid AssistantUserId,
    string AssistantEmail,
    string? FullName,
    string? AvatarUrl,
    string? PenName,
    string InvitationStatus,
    DateTime JoinedAt   // RespondedAt (thời điểm chấp nhận), hoặc CreatedAt nếu IsNewAccountFlow
);

/// <summary>
/// Cổng giao tiếp với Identity module để kiểm tra / tạo user mới.
/// Tránh circular reference giữa Studio và Identity.
/// </summary>
public interface IStudioIdentityService
{
    /// Kiểm tra email đã có tài khoản Active chưa (trả về UserId nếu có)
    System.Threading.Tasks.Task<Guid?> FindActiveAssistantByEmailAsync(string email, CancellationToken ct = default);

    /// Provision tài khoản Assistant mới (TH1) — tương đương AdminController.ProvisionAccount
    /// Trả về UserId mới tạo và activation token
    System.Threading.Tasks.Task<(Guid userId, string activationToken)> ProvisionAssistantAccountAsync(
        string email, string? fullName, string invitingMangakaName, CancellationToken ct = default);

    /// Gửi push notification cho Assistant đã có tài khoản (TH2)
    System.Threading.Tasks.Task SendStudioInvitationNotificationAsync(
        Guid receiverUserId, Guid invitationId, string mangakaName, string seriesTitle, CancellationToken ct = default);
}
